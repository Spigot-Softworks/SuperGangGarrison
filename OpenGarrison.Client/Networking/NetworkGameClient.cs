#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

internal sealed class NetworkGameClient : IDisposable
{
    internal readonly record struct ReceiveDiagnostics(
        int PacketsRead,
        int BytesRead,
        int ReleasedMessages,
        int SnapshotMessages,
        int MaxPayloadBytes,
        int PendingInboundMessages,
        double DeserializeMilliseconds,
        double MaxDeserializeMilliseconds);

    private const int WsaConnReset = 10054;
    private const int SioUdpConnReset = -1744830452;
    private const long HelloRetryMilliseconds = 500;
    private const long WelcomeTimeoutMilliseconds = 4000;
    private const long ConnectedTimeoutMilliseconds = 5000;
    private const long LocalWelcomeTimeoutMilliseconds = 30000;
    private const long LocalConnectedTimeoutMilliseconds = 30000;
    private UdpClient? _udpClient;
    private IPEndPoint? _serverEndPoint;
    private uint _nextInputSequence = 1;
    private uint _nextControlSequence = 1;
    private int _pendingChatBubbleFrameIndex = -1;
    private readonly Dictionary<ControlCommandKind, PendingControlCommand> _pendingControlCommands = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Queue<PendingPacket> _pendingOutboundPackets = new();
    private readonly Queue<PendingMessage> _pendingInboundMessages = new();
    private string? _pendingHelloPlayerName;
    private long _connectStartedAtMilliseconds = -1;
    private long _lastHelloSentAtMilliseconds = -1;
    private long _lastServerMessageReceivedAtMilliseconds = -1;
    private string? _lastDisconnectReason;

    public bool CollectDiagnostics { get; set; }
    public bool IsConnected => _udpClient is not null && _serverEndPoint is not null;
    public bool IsAwaitingWelcome => IsConnected && LocalPlayerSlot == 0;
    public bool IsSpectator => IsConnected && LocalPlayerSlot >= SimulationWorld.FirstSpectatorSlot;

    public byte LocalPlayerSlot { get; private set; }
    public string? ServerDescription { get; private set; }
    public int SimulatedLatencyMilliseconds { get; private set; }
    public ReceiveDiagnostics LastReceiveDiagnostics { get; private set; }

    public bool Connect(string host, int port, string playerName, out string error)
    {
        error = string.Empty;
        Disconnect();

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

            _serverEndPoint = new IPEndPoint(address, port);
            _udpClient = new UdpClient(0);
            _udpClient.Client.Blocking = false;
            TryDisableUdpConnectionReset(_udpClient.Client);
            _pendingHelloPlayerName = playerName;
            _connectStartedAtMilliseconds = _clock.ElapsedMilliseconds;
            _lastHelloSentAtMilliseconds = -1;
            LocalPlayerSlot = 0;
            SendHello();
            ServerDescription = $"{_serverEndPoint.Address}:{_serverEndPoint.Port}";
            return true;
        }
        catch (SocketException ex)
        {
            Disconnect();
            error = ex.Message;
            return false;
        }
    }

    public void Disconnect()
    {
        _udpClient?.Dispose();
        _udpClient = null;
        _serverEndPoint = null;
        _nextInputSequence = 1;
        _nextControlSequence = 1;
        _pendingChatBubbleFrameIndex = -1;
        _pendingControlCommands.Clear();
        _pendingOutboundPackets.Clear();
        _pendingInboundMessages.Clear();
        LocalPlayerSlot = 0;
        ServerDescription = null;
        _pendingHelloPlayerName = null;
        _connectStartedAtMilliseconds = -1;
        _lastHelloSentAtMilliseconds = -1;
        _lastServerMessageReceivedAtMilliseconds = -1;
        LastReceiveDiagnostics = default;
    }

    public void SetLocalPlayerSlot(byte slot)
    {
        LocalPlayerSlot = slot;
        _pendingHelloPlayerName = null;
        _connectStartedAtMilliseconds = -1;
        _lastHelloSentAtMilliseconds = -1;
        _lastServerMessageReceivedAtMilliseconds = _clock.ElapsedMilliseconds;
    }

    public void QueueChatBubble(int frameIndex)
    {
        _pendingChatBubbleFrameIndex = frameIndex;
    }

    public void QueueTeamSelection(PlayerTeam team)
    {
        QueueControlCommand(ControlCommandKind.SelectTeam, (byte)team);
    }

    public void ClearPendingTeamSelection()
    {
        _pendingControlCommands.Remove(ControlCommandKind.SelectTeam);
    }

    public void QueueClassSelection(PlayerClass playerClass)
    {
        QueueControlCommand(ControlCommandKind.SelectClass, (byte)playerClass);
    }

    public void QueueSpectateSelection()
    {
        QueueControlCommand(ControlCommandKind.Spectate, 0);
    }

    public void ClearPendingClassSelection()
    {
        _pendingControlCommands.Remove(ControlCommandKind.SelectClass);
    }

    public void SendPassword(string password)
    {
        if (!IsConnected)
        {
            return;
        }

        Send(new PasswordSubmitMessage(password));
    }

    public void SendChat(string text, bool teamOnly)
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Send(new ChatSubmitMessage(text, teamOnly));
    }

    public uint SendInput(PlayerInputSnapshot input)
    {
        if (!IsConnected)
        {
            return 0;
        }

        var buttons = InputButtons.None;
        if (input.Left) buttons |= InputButtons.Left;
        if (input.Right) buttons |= InputButtons.Right;
        if (input.Up) buttons |= InputButtons.Up;
        if (input.Down) buttons |= InputButtons.Down;
        if (input.BuildSentry) buttons |= InputButtons.BuildSentry;
        if (input.DestroySentry) buttons |= InputButtons.DestroySentry;
        if (input.Taunt) buttons |= InputButtons.Taunt;
        if (input.FirePrimary) buttons |= InputButtons.FirePrimary;
        if (input.FireSecondary) buttons |= InputButtons.FireSecondary;
        if (input.DropIntel) buttons |= InputButtons.DropIntel;

        SendPendingControlCommands();
        var sequence = _nextInputSequence++;
        Send(new InputStateMessage(sequence, buttons, input.AimWorldX, input.AimWorldY, _pendingChatBubbleFrameIndex));
        _pendingChatBubbleFrameIndex = -1;
        return sequence;
    }

    public void AcknowledgeControlCommand(uint sequence, ControlCommandKind kind)
    {
        if (_pendingControlCommands.TryGetValue(kind, out var pending) && pending.Sequence == sequence)
        {
            _pendingControlCommands.Remove(kind);
        }
    }

    public void AcknowledgeSnapshot(ulong frame)
    {
        if (!IsConnected || frame == 0)
        {
            return;
        }

        Send(new SnapshotAckMessage(frame));
    }

    public IEnumerable<IProtocolMessage> ReceiveMessages()
    {
        var udpClient = _udpClient;
        if (!IsConnected || udpClient is null)
        {
            LastReceiveDiagnostics = default;
            return [];
        }

        FlushHandshakeState();
        FlushPendingOutboundPackets();
        udpClient = _udpClient;
        if (!IsConnected || udpClient is null)
        {
            LastReceiveDiagnostics = default;
            return [];
        }

        var collectDiagnostics = CollectDiagnostics;
        var packetsRead = 0;
        var bytesRead = 0;
        var snapshotMessages = 0;
        var maxPayloadBytes = 0;
        var deserializeMilliseconds = 0d;
        var maxDeserializeMilliseconds = 0d;
        var messages = new List<IProtocolMessage>();
        while (udpClient.Available > 0)
        {
            try
            {
                IPEndPoint remoteEndPoint = new(IPAddress.Any, 0);
                var payload = udpClient.Receive(ref remoteEndPoint);
                if (collectDiagnostics)
                {
                    packetsRead += 1;
                    bytesRead += payload.Length;
                    maxPayloadBytes = Math.Max(maxPayloadBytes, payload.Length);
                }

                if (_serverEndPoint is not null && !EndpointsEqual(remoteEndPoint, _serverEndPoint))
                {
                    continue;
                }

                var deserializeStartTimestamp = collectDiagnostics ? Stopwatch.GetTimestamp() : 0L;
                var deserialized = ProtocolCodec.TryDeserialize(payload, out var message);
                if (collectDiagnostics)
                {
                    var elapsedMilliseconds = GetElapsedMilliseconds(deserializeStartTimestamp);
                    deserializeMilliseconds += elapsedMilliseconds;
                    maxDeserializeMilliseconds = Math.Max(maxDeserializeMilliseconds, elapsedMilliseconds);
                }

                if (!deserialized || message is null)
                {
                    continue;
                }

                _lastServerMessageReceivedAtMilliseconds = _clock.ElapsedMilliseconds;
                if (collectDiagnostics && message is SnapshotMessage)
                {
                    snapshotMessages += 1;
                }

                if (SimulatedLatencyMilliseconds > 0)
                {
                    _pendingInboundMessages.Enqueue(new PendingMessage(_clock.ElapsedMilliseconds + SimulatedLatencyMilliseconds, message));
                }
                else
                {
                    messages.Add(message);
                }
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset || ex.ErrorCode == WsaConnReset)
            {
                _lastDisconnectReason = "Connection reset by remote host.";
                Disconnect();
                break;
            }
        }

        FlushConnectedState();
        while (_pendingInboundMessages.Count > 0 && _pendingInboundMessages.Peek().ReleaseAtMilliseconds <= _clock.ElapsedMilliseconds)
        {
            messages.Add(_pendingInboundMessages.Dequeue().Message);
        }

        LastReceiveDiagnostics = collectDiagnostics
            ? new ReceiveDiagnostics(
                packetsRead,
                bytesRead,
                messages.Count,
                snapshotMessages,
                maxPayloadBytes,
                _pendingInboundMessages.Count,
                deserializeMilliseconds,
                maxDeserializeMilliseconds)
            : default;
        return messages;
    }

    public bool TryConsumeDisconnectReason(out string reason)
    {
        if (string.IsNullOrWhiteSpace(_lastDisconnectReason))
        {
            reason = string.Empty;
            return false;
        }

        reason = _lastDisconnectReason;
        _lastDisconnectReason = null;
        return true;
    }

    private void Send(IProtocolMessage message)
    {
        if (_udpClient is null || _serverEndPoint is null)
        {
            return;
        }

        var payload = ProtocolCodec.Serialize(message);
        if (SimulatedLatencyMilliseconds > 0)
        {
            _pendingOutboundPackets.Enqueue(new PendingPacket(_clock.ElapsedMilliseconds + SimulatedLatencyMilliseconds, payload));
            FlushPendingOutboundPackets();
            return;
        }

        _udpClient.Send(payload, payload.Length, _serverEndPoint);
    }

    public void Dispose()
    {
        Disconnect();
    }

    private void QueueControlCommand(ControlCommandKind kind, byte value)
    {
        _pendingControlCommands[kind] = new PendingControlCommand(_nextControlSequence++, kind, value);
    }

    private void SendPendingControlCommands()
    {
        if (!IsConnected)
        {
            return;
        }

        foreach (var pending in _pendingControlCommands.Values)
        {
            Send(new ControlCommandMessage(pending.Sequence, pending.Kind, pending.Value));
        }
    }

    public void SetSimulatedLatency(int milliseconds)
    {
        SimulatedLatencyMilliseconds = int.Max(milliseconds, 0);
        if (SimulatedLatencyMilliseconds == 0)
        {
            while (_pendingOutboundPackets.Count > 0)
            {
                var pending = _pendingOutboundPackets.Dequeue();
                if (_udpClient is not null && _serverEndPoint is not null)
                {
                    _udpClient.Send(pending.Payload, pending.Payload.Length, _serverEndPoint);
                }
            }
        }
    }

    private void FlushPendingOutboundPackets()
    {
        if (_udpClient is null || _serverEndPoint is null)
        {
            _pendingOutboundPackets.Clear();
            return;
        }

        while (_pendingOutboundPackets.Count > 0 && _pendingOutboundPackets.Peek().ReleaseAtMilliseconds <= _clock.ElapsedMilliseconds)
        {
            var pending = _pendingOutboundPackets.Dequeue();
            _udpClient.Send(pending.Payload, pending.Payload.Length, _serverEndPoint);
        }
    }

    private void FlushHandshakeState()
    {
        if (!IsAwaitingWelcome)
        {
            return;
        }

        var nowMilliseconds = _clock.ElapsedMilliseconds;
        if (_connectStartedAtMilliseconds >= 0
            && nowMilliseconds - _connectStartedAtMilliseconds >= GetWelcomeTimeoutMilliseconds())
        {
            _lastDisconnectReason = "Connection timed out waiting for server response.";
            Disconnect();
            return;
        }

        if (_lastHelloSentAtMilliseconds < 0 || nowMilliseconds - _lastHelloSentAtMilliseconds >= HelloRetryMilliseconds)
        {
            SendHello();
        }
    }

    private void FlushConnectedState()
    {
        if (!IsConnected || IsAwaitingWelcome)
        {
            return;
        }

        var nowMilliseconds = _clock.ElapsedMilliseconds;
        if (_lastServerMessageReceivedAtMilliseconds >= 0
            && nowMilliseconds - _lastServerMessageReceivedAtMilliseconds >= GetConnectedTimeoutMilliseconds())
        {
            _lastDisconnectReason = "Connection timed out waiting for server snapshots.";
            Disconnect();
        }
    }

    private void SendHello()
    {
        if (_pendingHelloPlayerName is null)
        {
            return;
        }

        Send(new HelloMessage(_pendingHelloPlayerName, ProtocolVersion.Current));
        _lastHelloSentAtMilliseconds = _clock.ElapsedMilliseconds;
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

    private static double GetElapsedMilliseconds(long startTimestamp)
    {
        return (Stopwatch.GetTimestamp() - startTimestamp) * 1000d / Stopwatch.Frequency;
    }

    private long GetWelcomeTimeoutMilliseconds()
    {
        return IsLoopbackConnection()
            ? LocalWelcomeTimeoutMilliseconds
            : WelcomeTimeoutMilliseconds;
    }

    private long GetConnectedTimeoutMilliseconds()
    {
        return IsLoopbackConnection()
            ? LocalConnectedTimeoutMilliseconds
            : ConnectedTimeoutMilliseconds;
    }

    private bool IsLoopbackConnection()
    {
        var address = _serverEndPoint?.Address;
        if (address is null)
        {
            return false;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return IPAddress.IsLoopback(address);
    }

    private sealed record PendingControlCommand(uint Sequence, ControlCommandKind Kind, byte Value);
    private sealed record PendingPacket(long ReleaseAtMilliseconds, byte[] Payload);
    private sealed record PendingMessage(long ReleaseAtMilliseconds, IProtocolMessage Message);
}



