#nullable enable

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public interface INetworkClientMessageTransport : IDisposable
{
    bool HasPendingMessages { get; }
    bool IsLoopbackConnection { get; }
    string RemoteDescription { get; }
    // Player-hosted games can pause packet delivery while their host loads a map.
    // Zero preserves the standard connection timeouts for existing transports.
    int ReceiveTimeoutMilliseconds => 0;

    bool TryReceive(out byte[] payload);
    bool TryConsumeDisconnectReason(out string reason);
    void Send(byte[] payload);
}

public interface IPlaybackMessageTransport : INetworkClientMessageTransport
{
    bool IsPaused { get; }
    float PlaybackRate { get; }
    int CurrentTick { get; }
    int TotalTicks { get; }
    string PlaybackDisplayName { get; }
    string PlaybackServerName { get; }
    string PlaybackMapName { get; }
    DateTime? PlaybackDateUtc { get; }

    void SetPaused(bool paused);
    void TogglePaused();
    void SetPlaybackRate(float playbackRate);
}

/// <summary>Ephemeral audio may be dropped under backpressure rather than delaying gameplay or accumulating old speech.</summary>
public interface INetworkClientAudioMessageTransport
{
    void SendAudio(byte[] payload);
}

public interface ISeekablePlaybackMessageTransport : IPlaybackMessageTransport
{
    int PositionMilliseconds { get; }
    int DurationMilliseconds { get; }
    bool IsSeekCatchUpPending { get; }

    ISeekablePlaybackMessageTransport CreateSeekedPlayback(int positionMilliseconds);
}

internal sealed class UdpNetworkClientMessageTransport : INetworkClientMessageTransport
{
    private const int SioUdpConnReset = -1744830452;

    private readonly UdpClient _udpClient;
    private readonly IPEndPoint _serverEndPoint;
    private readonly UdpFragmentSendCache _fragmentSendCache = new();
    private readonly UdpFragmentReassembler _fragmentReassembler = new();
    private readonly UdpRepairPacer _repairPacer = new();
    private readonly UdpPathMtuDiscovery _pathMtu = new();

    private UdpNetworkClientMessageTransport(UdpClient udpClient, IPEndPoint serverEndPoint)
    {
        _udpClient = udpClient;
        _serverEndPoint = serverEndPoint;
    }

    public bool HasPendingMessages
    {
        get
        {
            if (_udpClient.Available == 0)
            {
                ServiceDueFragmentNacks();
                ServiceDueRepairs();
                ServiceDuePathMtuProbe(Environment.TickCount64);
            }
            return _udpClient.Available > 0;
        }
    }

    public bool IsLoopbackConnection
    {
        get
        {
            var address = _serverEndPoint.Address;
            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }

            return IPAddress.IsLoopback(address);
        }
    }

    public string RemoteDescription => $"{_serverEndPoint.Address}:{_serverEndPoint.Port}";

    public static bool TryConnect(string host, int port, out INetworkClientMessageTransport? transport, out string error)
    {
        transport = null;
        error = string.Empty;

        try
        {
            var addresses = Dns.GetHostAddresses(host);
            var address = addresses.FirstOrDefault(candidate => candidate.AddressFamily == AddressFamily.InterNetwork)
                ?? addresses.FirstOrDefault();
            if (address is null)
            {
                error = $"could not resolve host {host}";
                return false;
            }

            var serverEndPoint = new IPEndPoint(address, port);
            var udpClient = new UdpClient(0);
            udpClient.Client.Blocking = false;
            EnableDontFragmentWhenSupported(udpClient.Client);
            TryDisableUdpConnectionReset(udpClient.Client);
            transport = new UdpNetworkClientMessageTransport(udpClient, serverEndPoint);
            return true;
        }
        catch (SocketException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TryReceive(out byte[] payload)
    {
        payload = [];
        IPEndPoint remoteEndPoint = new(IPAddress.Any, 0);
        var receivedPayload = _udpClient.Receive(ref remoteEndPoint);
        if (!EndpointsEqual(remoteEndPoint, _serverEndPoint))
        {
            return true;
        }

        var peerKey = remoteEndPoint.ToString();
        if (!UdpFragmentation.IsFramed(receivedPayload))
        {
            payload = receivedPayload;
            return true;
        }
        if (!UdpFragmentation.TryDecode(receivedPayload, out var frame)) return true;
        if (frame.IsMtuProbe)
        {
            var ack = UdpFragmentation.CreateMtuAck(frame.MessageId, frame.TotalLength);
            TrySendControlDatagram(ack);
            return true;
        }
        if (frame.IsMtuAck)
        {
            _pathMtu.TryAcceptAck(peerKey, frame, Environment.TickCount64, out _);
            return true;
        }
        if (frame.IsNack)
        {
            var now = Environment.TickCount64;
            var limit = _pathMtu.GetDatagramLimit(peerKey, now);
            _repairPacer.Enqueue(peerKey, _fragmentSendCache.TryRepair(peerKey, receivedPayload, now, limit), now);
            return true;
        }

        _fragmentReassembler.TryAccept(peerKey, receivedPayload, Environment.TickCount64, out var completePayload);
        if (completePayload is null) return true;
        payload = completePayload;
        return true;
    }

    public void Send(byte[] payload)
    {
        var peerKey = _serverEndPoint.ToString();
        var now = Environment.TickCount64;
        var datagrams = _fragmentSendCache.CacheAndFragment(peerKey, payload, now, _pathMtu.GetDatagramLimit(peerKey, now));
        try
        {
            foreach (var datagram in datagrams) _udpClient.Send(datagram, datagram.Length, _serverEndPoint);
        }
        finally
        {
            ServiceDuePathMtuProbe(now);
            ServiceDueRepairs();
        }
    }

    public bool TryConsumeDisconnectReason(out string reason)
    {
        reason = string.Empty;
        return false;
    }

    public void Dispose()
    {
        _udpClient.Dispose();
    }

    private void ServiceDueFragmentNacks()
    {
        var now = Environment.TickCount64;
        foreach (var (peerKey, packet) in _fragmentReassembler.GetDueNacks(now))
            if (StringComparer.Ordinal.Equals(peerKey, _serverEndPoint.ToString())) _repairPacer.Enqueue(peerKey, [packet], now);
    }

    private void ServiceDueRepairs()
    {
        var now = Environment.TickCount64;
        if (!_repairPacer.TryDequeueDue(now, out var peerKey, out var packet)
            || packet is null || !StringComparer.Ordinal.Equals(peerKey, _serverEndPoint.ToString())
            || !UdpFragmentation.TryDecode(packet, out var frame)) return;
        var isCurrent = frame.IsNack
            ? _fragmentReassembler.IsCurrentNack(peerKey, packet, now)
            : _fragmentSendCache.IsCurrentFrame(peerKey, packet, now);
        if (isCurrent) TrySendControlDatagram(packet);
    }

    private void ServiceDuePathMtuProbe(long nowMilliseconds)
    {
        if (_pathMtu.TryCreateProbe(_serverEndPoint.ToString(), nowMilliseconds, out var packet) && packet is not null)
            TrySendControlDatagram(packet);
    }

    private void TrySendControlDatagram(byte[] packet)
    {
        try
        {
            _udpClient.Send(packet, packet.Length, _serverEndPoint);
        }
        catch (SocketException)
        {
            // Repairs and path probes are best-effort; fresh application sends retain their failure behavior.
        }
    }

    private static bool EndpointsEqual(IPEndPoint left, IPEndPoint right)
    {
        return left.Address.Equals(right.Address) && left.Port == right.Port;
    }

    private static void TryDisableUdpConnectionReset(Socket socket)
    {
        try
        {
            socket.IOControl((IOControlCode)SioUdpConnReset, [0, 0, 0, 0], null);
        }
        catch (PlatformNotSupportedException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }

    private static void EnableDontFragmentWhenSupported(Socket socket)
    {
        try
        {
            if (socket.AddressFamily == AddressFamily.InterNetwork) socket.DontFragment = true;
            else if (socket.AddressFamily == AddressFamily.InterNetworkV6)
                socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.DontFragment, true);
        }
        catch (SocketException)
        {
            // Some IPv6 stacks do not expose a per-socket no-fragment option.
        }
        catch (NotSupportedException)
        {
        }
    }
}
