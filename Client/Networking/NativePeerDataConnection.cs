#if !BROWSER_KNI
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SIPSorcery.Net;

namespace OpenGarrison.Client;

internal sealed class NativePeerDataConnection : IPeerDataConnection
{
    static NativePeerDataConnection()
    {
        // SIPSorcery performs blocking DTLS handshakes on pool threads. Reserve capacity
        // for ICE/socket callbacks while several room peers negotiate at the same time.
        ThreadPool.GetMinThreads(out var workers, out var completion);
        ThreadPool.SetMinThreads(Math.Max(workers, 32), completion);
    }
    private readonly RTCPeerConnection _connection;
    private readonly PeerPacketFraming _framing;
    private readonly Action<string> _signal;
    private readonly SemaphoreSlim _signalGate = new(1, 1);
    private readonly Queue<RTCIceCandidateInit> _pendingIce = new();
    private RTCDataChannel? _channel;
    private bool _remoteDescriptionSet, _disposed;
    private string _error = "";
    public override string ToString() => $"RTC: {_connection.connectionState}, ICE: {_connection.iceConnectionState}, remote SDP: {_remoteDescriptionSet}, channel: {_channel?.readyState}, error: {_error}";
    public bool IsOpen => !_disposed && _channel?.readyState == RTCDataChannelState.open;
    public NativePeerDataConnection(bool offerer, JsonElement iceServers, Action<string> signal, Action<byte[]> receive)
    {
        _signal = signal;
        _framing = new(receive);
        var servers = new List<RTCIceServer>();
        if (iceServers.ValueKind == JsonValueKind.Array)
            foreach (var item in iceServers.EnumerateArray())
                if (item.TryGetProperty("urls", out var urls))
                {
                    IEnumerable<JsonElement> addresses = urls.ValueKind == JsonValueKind.Array ? urls.EnumerateArray() : new[] { urls };
                    foreach (var address in addresses)
                    if (address.ValueKind == JsonValueKind.String)
                    servers.Add(new RTCIceServer { urls = address.GetString(),
                        username = item.TryGetProperty("username", out var user) ? user.GetString() : null,
                        credential = item.TryGetProperty("credential", out var password) ? password.GetString() : null });
                }
        _connection = new RTCPeerConnection(new RTCConfiguration { iceServers = servers });
        _connection.onicecandidate += candidate =>
        {
            if (candidate is not null && !_disposed)
                _signal(JsonSerializer.Serialize(new { candidate = candidate.candidate, sdpMid = candidate.sdpMid, sdpMLineIndex = candidate.sdpMLineIndex }));
        };
        _connection.ondatachannel += BindChannel;
        if (offerer) _ = OfferAsync();
    }
    private void BindChannel(RTCDataChannel channel)
    {
        if (_disposed || channel.label != "og2") { channel.close(); return; }
        _channel = channel;
        channel.onmessage += (_, _, bytes) => { lock (_framing) _framing.Receive(bytes); };
    }
    private async Task OfferAsync()
    {
        try
        {
            BindChannel(await _connection.createDataChannel("og2", new RTCDataChannelInit { ordered = true }));
            var offer = _connection.createOffer();
            await _connection.setLocalDescription(offer);
            if (!_disposed) _signal(JsonSerializer.Serialize(new { type = "offer", sdp = offer.sdp }));
        }
        catch (Exception ex) { _error = ex.Message; Dispose(); }
    }
    public void ApplySignal(string signal) => _ = ApplySignalAsync(signal);
    private async Task ApplySignalAsync(string signal)
    {
        await _signalGate.WaitAsync();
        try
        {
            if (_disposed) return;
            using var document = JsonDocument.Parse(signal);
            var root = document.RootElement;
            if (root.TryGetProperty("type", out var type))
            {
                var description = new RTCSessionDescriptionInit { type = type.GetString() == "offer" ? RTCSdpType.offer : RTCSdpType.answer,
                    sdp = root.GetProperty("sdp").GetString() };
                var result = _connection.setRemoteDescription(description);
                if (result != SetDescriptionResultEnum.OK) { _error = "Remote description: " + result; Dispose(); return; }
                _remoteDescriptionSet = true;
                while (_pendingIce.TryDequeue(out var ice)) _connection.addIceCandidate(ice);
                if (description.type == RTCSdpType.offer)
                {
                    var answer = _connection.createAnswer();
                    await _connection.setLocalDescription(answer);
                    _signal(JsonSerializer.Serialize(new { type = "answer", sdp = answer.sdp }));
                }
            }
            else if (root.TryGetProperty("candidate", out var candidate))
            {
                var ice = new RTCIceCandidateInit { candidate = candidate.GetString(),
                    sdpMid = root.TryGetProperty("sdpMid", out var mid) ? mid.GetString() : "0",
                    sdpMLineIndex = root.TryGetProperty("sdpMLineIndex", out var index) && index.ValueKind == JsonValueKind.Number ? index.GetUInt16() : (ushort)0 };
                if (_remoteDescriptionSet) _connection.addIceCandidate(ice);
                else if (_pendingIce.Count < 128) _pendingIce.Enqueue(ice);
            }
        }
        catch (Exception ex) { _error = ex.Message; Dispose(); }
        finally { _signalGate.Release(); }
    }
    public bool TrySend(byte[] packet)
    {
        if (!IsOpen || _channel!.bufferedAmount + (ulong)packet.Length > 8UL * 1024 * 1024) return false;
        try { lock (_framing) _framing.Send(packet, bytes => _channel!.send(bytes)); return true; }
        catch (Exception) { Dispose(); return false; }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _connection.Close("Room connection closed.");
        _connection.Dispose();
    }
}
#endif
