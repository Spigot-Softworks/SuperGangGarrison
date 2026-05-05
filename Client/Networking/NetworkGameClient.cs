#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
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

    public readonly record struct SendDiagnostics(
        long PacketsSent,
        long BytesSent,
        long HelloMessagesSent,
        long InputMessagesSent,
        long ControlMessagesSent,
        long SnapshotAckMessagesSent);

    private const int WsaConnReset = 10054;
    private const int SioUdpConnReset = -1744830452;
    private const long HelloRetryMilliseconds = 500;
    private const long WelcomeTimeoutMilliseconds = 4000;
    private const long ConnectedTimeoutMilliseconds = 5000;
    private const long LocalWelcomeTimeoutMilliseconds = 30000;
    private const long LocalConnectedTimeoutMilliseconds = 30000;
    private const int MaxTrackedInputRoundTrips = 512;

    [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "The client transport seam must support browser WebSocket adapters.")]
    private INetworkClientMessageTransport? _transport;
    private uint _nextInputSequence = 1;
    private uint _nextControlSequence = 1;
    private int _pendingChatBubbleFrameIndex = -1;
    private readonly Dictionary<ControlCommandKind, PendingControlCommand> _pendingControlCommands = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Queue<PendingPacket> _pendingOutboundPackets = new();
    private readonly Queue<PendingMessage> _pendingInboundMessages = new();
    private readonly Queue<TrackedInputRoundTrip> _trackedInputRoundTrips = new();
    private readonly Dictionary<uint, long> _trackedInputRoundTripTimes = new();
    private readonly Queue<PendingDelayedInput> _pendingDelayedInputs = new();
    private ulong _networkInputTick;
    private string? _pendingHelloPlayerName;
    private ulong _pendingHelloBadgeMask;
    private long _connectStartedAtMilliseconds = -1;
    private long _lastHelloSentAtMilliseconds = -1;
    private long _lastServerMessageReceivedAtMilliseconds = -1;
    private string? _lastDisconnectReason;
    private OpenGarrisonDemoRecordingWriter? _demoRecorder;
    private string? _armedDemoRecordingPath;
    private int _demoRecordingRecordedSnapshots;
    private ulong _demoRecordingFirstSnapshotFrame;
    private bool _demoRecordingFirstSnapshotFrameInitialized;
    private int _demoRecordingLastDueMilliseconds;
    private long _demoRecordingStartedAtMilliseconds = -1;
    private string? _lastCompletedDemoRecordingNotice;

    public bool CollectDiagnostics { get; set; }

    // Delay outbound input by a small number of fixed gameplay ticks on remote connections
    // to give the remote server a chance to catch up and reduce visible position correction.
    public int NetworkInputDelayTicks { get; set; } = 2;
    public bool IsConnected => _transport is not null;
    public bool IsAwaitingWelcome => IsConnected && LocalPlayerSlot == 0;
    public bool IsSpectator => IsConnected && LocalPlayerSlot >= SimulationWorld.FirstSpectatorSlot;
    public bool IsReplayConnection { get; private set; }

    public byte LocalPlayerSlot { get; private set; }
    public string? ServerDescription { get; private set; }
    public int ServerMaxPlayerCount { get; private set; }
    public int SimulatedLatencyMilliseconds { get; private set; }
    public int EstimatedPingMilliseconds { get; private set; } = -1;
    public ReceiveDiagnostics LastReceiveDiagnostics { get; private set; }
    public SendDiagnostics TotalSendDiagnostics { get; private set; }

    public bool Connect(string host, int port, string playerName, ulong badgeMask, out string error)
    {
        error = string.Empty;
        var armedDemoRecordingPath = _demoRecorder is null ? _armedDemoRecordingPath : null;
        Disconnect();
        if (!string.IsNullOrWhiteSpace(armedDemoRecordingPath))
        {
            _armedDemoRecordingPath = armedDemoRecordingPath;
        }

        try
        {
            if (!NetworkClientMessageTransportRegistry.TryConnect(host, port, out var transport, out error) || transport is null)
            {
                return false;
            }

            return Connect(transport, playerName, badgeMask, out error);
        }
        catch (SocketException ex)
        {
            Disconnect();
            error = ex.Message;
            return false;
        }
    }

    public bool Connect(INetworkClientMessageTransport transport, string playerName, ulong badgeMask, out string error)
    {
        error = string.Empty;
        ArgumentNullException.ThrowIfNull(transport);
        var armedDemoRecordingPath = _demoRecorder is null ? _armedDemoRecordingPath : null;
        Disconnect();
        if (!string.IsNullOrWhiteSpace(armedDemoRecordingPath))
        {
            _armedDemoRecordingPath = armedDemoRecordingPath;
        }

        _transport = transport;
        IsReplayConnection = transport is ReDsmReplayTransport
            || transport.RemoteDescription.StartsWith("replay:", StringComparison.OrdinalIgnoreCase);
        NetworkInputDelayTicks = transport.IsLoopbackConnection ? 0 : 2;
        _pendingHelloPlayerName = playerName;
        _pendingHelloBadgeMask = badgeMask;
        _connectStartedAtMilliseconds = _clock.ElapsedMilliseconds;
        _lastHelloSentAtMilliseconds = -1;
        LocalPlayerSlot = 0;
        ServerMaxPlayerCount = 0;
        SendHello();
        ServerDescription = transport.RemoteDescription;
        return true;
    }

    public void Disconnect()
    {
        FinalizeDemoRecording(saveRecording: true, completedByDisconnect: true);
        _transport?.Dispose();
        _transport = null;
        _nextInputSequence = 1;
        _nextControlSequence = 1;
        _pendingChatBubbleFrameIndex = -1;
        _pendingControlCommands.Clear();
        _pendingOutboundPackets.Clear();
        _pendingInboundMessages.Clear();
        _pendingDelayedInputs.Clear();
        _trackedInputRoundTrips.Clear();
        _trackedInputRoundTripTimes.Clear();
        _networkInputTick = 0;
        IsReplayConnection = false;
        LocalPlayerSlot = 0;
        ServerDescription = null;
        ServerMaxPlayerCount = 0;
        _pendingHelloPlayerName = null;
        _pendingHelloBadgeMask = 0UL;
        _connectStartedAtMilliseconds = -1;
        _lastHelloSentAtMilliseconds = -1;
        _lastServerMessageReceivedAtMilliseconds = -1;
        EstimatedPingMilliseconds = -1;
        LastReceiveDiagnostics = default;
        TotalSendDiagnostics = default;
    }

    public bool TryToggleReplayPause(out bool isPaused, out string error)
    {
        if (_transport is not IPlaybackMessageTransport replayTransport)
        {
            isPaused = false;
            error = "no replay is currently playing.";
            return false;
        }

        replayTransport.TogglePaused();
        isPaused = replayTransport.IsPaused;
        error = string.Empty;
        return true;
    }

    public bool TrySetReplayPaused(bool paused, out string error)
    {
        if (_transport is not IPlaybackMessageTransport replayTransport)
        {
            error = "no replay is currently playing.";
            return false;
        }

        replayTransport.SetPaused(paused);
        error = string.Empty;
        return true;
    }

    public bool TrySetReplayPlaybackRate(float playbackRate, out float appliedPlaybackRate, out string error)
    {
        if (_transport is not IPlaybackMessageTransport replayTransport)
        {
            appliedPlaybackRate = 1f;
            error = "no replay is currently playing.";
            return false;
        }

        replayTransport.SetPlaybackRate(playbackRate);
        appliedPlaybackRate = replayTransport.PlaybackRate;
        error = string.Empty;
        return true;
    }

    public bool TryGetReplayStatus(out string status)
    {
        if (_transport is not IPlaybackMessageTransport replayTransport)
        {
            status = "no replay is currently playing.";
            return false;
        }

        var pauseLabel = replayTransport.IsPaused ? "paused" : "playing";
        status =
            $"replay {pauseLabel} tick={replayTransport.CurrentTick}/{replayTransport.TotalTicks} speed={(replayTransport.PlaybackRate * 100f).ToString("0", CultureInfo.InvariantCulture)}%";
        return true;
    }

    public bool TryStartDemoRecording(string demoPath, string remoteDescription, byte[]? initialWelcomePayload, out string status, out string error)
    {
        status = string.Empty;
        error = string.Empty;

        if (OperatingSystem.IsBrowser())
        {
            error = "demo recording is unavailable in the browser runtime.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(demoPath))
        {
            error = "demo recording requires an output path.";
            return false;
        }

        if (_transport is IPlaybackMessageTransport || IsReplayConnection)
        {
            error = "demo recording is unavailable while playing a replay or demo.";
            return false;
        }

        if (_demoRecorder is not null || !string.IsNullOrWhiteSpace(_armedDemoRecordingPath))
        {
            error = "demo recording is already active.";
            return false;
        }

        var resolvedPath = Path.GetFullPath(demoPath.Trim().Trim('"'));
        if (initialWelcomePayload is not null)
        {
            return TryCreateActiveDemoRecorder(resolvedPath, remoteDescription, initialWelcomePayload, out status, out error);
        }

        _armedDemoRecordingPath = resolvedPath;
        status = $"demo recording armed: {resolvedPath}";
        return true;
    }

    public bool TryStopDemoRecording(bool saveRecording, out string status, out string error)
    {
        status = string.Empty;
        error = string.Empty;

        if (_demoRecorder is null && string.IsNullOrWhiteSpace(_armedDemoRecordingPath))
        {
            error = "no demo recording is active.";
            return false;
        }

        status = FinalizeDemoRecording(saveRecording, completedByDisconnect: false);
        return true;
    }

    public bool TryGetDemoRecordingStatus(out string status)
    {
        if (_demoRecorder is not null)
        {
            status =
                $"demo recording active path={_demoRecorder.FinalPath} messages={_demoRecorder.MessageCount} " +
                $"snapshots={_demoRecordingRecordedSnapshots} bytes={_demoRecorder.PayloadByteCount}";
            return true;
        }

        if (!string.IsNullOrWhiteSpace(_armedDemoRecordingPath))
        {
            status = $"demo recording armed path={_armedDemoRecordingPath}";
            return true;
        }

        status = "no demo recording is active.";
        return false;
    }

    public bool TryConsumeCompletedDemoRecordingNotice(out string notice)
    {
        if (string.IsNullOrWhiteSpace(_lastCompletedDemoRecordingNotice))
        {
            notice = string.Empty;
            return false;
        }

        notice = _lastCompletedDemoRecordingNotice;
        _lastCompletedDemoRecordingNotice = null;
        return true;
    }

    public void SetLocalPlayerSlot(byte slot)
    {
        LocalPlayerSlot = slot;
        _pendingHelloPlayerName = null;
        _connectStartedAtMilliseconds = -1;
        _lastHelloSentAtMilliseconds = -1;
        _lastServerMessageReceivedAtMilliseconds = _clock.ElapsedMilliseconds;
    }

    public void SetServerDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return;
        }

        ServerDescription = description.Trim();
    }

    public void SetServerMaxPlayerCount(int maxPlayerCount)
    {
        ServerMaxPlayerCount = Math.Max(0, maxPlayerCount);
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

    public void QueueGameplayLoadoutSelection(string loadoutId)
    {
        if (string.IsNullOrWhiteSpace(loadoutId))
        {
            return;
        }

        QueueControlCommand(ControlCommandKind.SelectGameplayLoadout, 0, loadoutId.Trim());
    }

    public void ClearPendingClassSelection()
    {
        _pendingControlCommands.Remove(ControlCommandKind.SelectClass);
    }

    public void ClearPendingGameplayLoadoutSelection()
    {
        _pendingControlCommands.Remove(ControlCommandKind.SelectGameplayLoadout);
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

    public void UpdatePlayerProfile(string playerName, ulong badgeMask)
    {
        _pendingHelloPlayerName = playerName;
        _pendingHelloBadgeMask = badgeMask;
        if (!IsConnected || IsAwaitingWelcome)
        {
            return;
        }

        Send(new PlayerProfileUpdateMessage(playerName, badgeMask));
    }

    public void SendPluginMessage(
        string sourcePluginId,
        string targetPluginId,
        string messageType,
        string payload,
        PluginMessagePayloadFormat payloadFormat,
        ushort schemaVersion)
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(sourcePluginId) || string.IsNullOrWhiteSpace(targetPluginId) || string.IsNullOrWhiteSpace(messageType))
        {
            return;
        }

        Send(new ClientPluginMessage(
            sourcePluginId.Trim(),
            targetPluginId.Trim(),
            messageType.Trim(),
            payload ?? string.Empty,
            payloadFormat,
            schemaVersion));
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
        if (input.FireSecondaryWeapon) buttons |= InputButtons.FireSecondaryWeapon;
        if (input.InteractWeapon) buttons |= InputButtons.InteractWeapon;

        SendPendingControlCommands();
        var sequence = _nextInputSequence++;
        var inputMessage = new InputStateMessage(sequence, buttons, input.AimWorldX, input.AimWorldY, _pendingChatBubbleFrameIndex);
        if (NetworkInputDelayTicks > 0 && !IsLoopbackConnection())
        {
            _pendingDelayedInputs.Enqueue(new PendingDelayedInput(_networkInputTick + (ulong)NetworkInputDelayTicks, inputMessage, sequence));
        }
        else
        {
            TrackInputRoundTrip(sequence);
            Send(inputMessage);
        }

        _pendingChatBubbleFrameIndex = -1;
        return sequence;
    }

    public void AcknowledgeProcessedInput(uint sequence)
    {
        if (sequence == 0 || _trackedInputRoundTrips.Count == 0)
        {
            return;
        }

        var nowMilliseconds = _clock.ElapsedMilliseconds;
        while (_trackedInputRoundTrips.Count > 0 && _trackedInputRoundTrips.Peek().Sequence <= sequence)
        {
            var tracked = _trackedInputRoundTrips.Dequeue();
            if (!_trackedInputRoundTripTimes.Remove(tracked.Sequence, out var sentAtMilliseconds))
            {
                continue;
            }

            if (tracked.Sequence == sequence)
            {
                EstimatedPingMilliseconds = (int)Math.Clamp(nowMilliseconds - sentAtMilliseconds, 0L, int.MaxValue);
            }
        }
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

    // Advance the input send cadence and flush any input packets that were delayed
    // long enough to match the configured input lag buffer.
    public void AdvanceNetworkInputTick()
    {
        _networkInputTick += 1;
        while (_pendingDelayedInputs.Count > 0 && _pendingDelayedInputs.Peek().DueTick <= _networkInputTick)
        {
            var pending = _pendingDelayedInputs.Dequeue();
            TrackInputRoundTrip(pending.Sequence);
            Send(pending.Message);
        }
    }

    private sealed record PendingDelayedInput(ulong DueTick, IProtocolMessage Message, uint Sequence);

    public IEnumerable<IProtocolMessage> ReceiveMessages()
    {
        var transport = _transport;
        if (!IsConnected || transport is null)
        {
            LastReceiveDiagnostics = default;
            return [];
        }

        FlushHandshakeState();
        FlushTransportState();
        FlushPendingOutboundPackets();
        transport = _transport;
        if (!IsConnected || transport is null)
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
        while (transport.HasPendingMessages)
        {
            try
            {
                if (!transport.TryReceive(out var payload))
                {
                    continue;
                }

                if (collectDiagnostics)
                {
                    packetsRead += 1;
                    bytesRead += payload.Length;
                    maxPayloadBytes = Math.Max(maxPayloadBytes, payload.Length);
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
                CaptureInboundDemoMessage(transport, message, payload);
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

        FlushTransportState();
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
        var transport = _transport;
        if (transport is null)
        {
            return;
        }

        var payload = ProtocolCodec.Serialize(message);
        RecordSendDiagnostics(message, payload.Length);
        if (SimulatedLatencyMilliseconds > 0)
        {
            _pendingOutboundPackets.Enqueue(new PendingPacket(_clock.ElapsedMilliseconds + SimulatedLatencyMilliseconds, payload));
            FlushPendingOutboundPackets();
            return;
        }

        transport.Send(payload);
    }

    private void CaptureInboundDemoMessage(INetworkClientMessageTransport transport, IProtocolMessage message, byte[] payload)
    {
        if (_demoRecorder is null)
        {
            if (message is not WelcomeMessage || string.IsNullOrWhiteSpace(_armedDemoRecordingPath))
            {
                return;
            }

            if (!TryCreateActiveDemoRecorder(
                    _armedDemoRecordingPath,
                    ServerDescription ?? transport.RemoteDescription,
                    payload,
                    out _,
                    out var activationError))
            {
                _lastCompletedDemoRecordingNotice = $"demo recording failed: {activationError}";
            }

            return;
        }

        if (!ShouldRecordDemoMessage(message))
        {
            return;
        }

        var dueMilliseconds = ResolveDemoMessageDueMilliseconds(message);
        _demoRecorder.AppendMessage(dueMilliseconds, payload);
        if (message is SnapshotMessage)
        {
            _demoRecordingRecordedSnapshots += 1;
        }
    }

    private bool TryCreateActiveDemoRecorder(
        string resolvedPath,
        string remoteDescription,
        byte[] initialWelcomePayload,
        out string status,
        out string error)
    {
        status = string.Empty;
        error = string.Empty;

        try
        {
            ResetDemoRecordingTimingState();
            var resolvedRemoteDescription = string.IsNullOrWhiteSpace(remoteDescription)
                ? "demo-recording"
                : remoteDescription.Trim();
            var recorder = new OpenGarrisonDemoRecordingWriter(resolvedPath, resolvedRemoteDescription, "Demo ended.");
            recorder.AppendMessage(0, initialWelcomePayload);
            _demoRecordingStartedAtMilliseconds = _clock.ElapsedMilliseconds;
            _demoRecorder = recorder;
            _armedDemoRecordingPath = null;
            status = $"demo recording started: {resolvedPath}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            ResetDemoRecordingTimingState();
            _demoRecorder?.Dispose();
            _demoRecorder = null;
            _armedDemoRecordingPath = null;
            error = ex.Message;
            return false;
        }
    }

    private int ResolveDemoMessageDueMilliseconds(IProtocolMessage message)
    {
        if (message is WelcomeMessage)
        {
            _demoRecordingLastDueMilliseconds = 0;
            return 0;
        }

        if (message is SnapshotMessage snapshot)
        {
            if (!_demoRecordingFirstSnapshotFrameInitialized)
            {
                _demoRecordingFirstSnapshotFrame = snapshot.Frame;
                _demoRecordingFirstSnapshotFrameInitialized = true;
                _demoRecordingLastDueMilliseconds = Math.Max(_demoRecordingLastDueMilliseconds, 0);
                return _demoRecordingLastDueMilliseconds;
            }

            var effectiveTickRate = snapshot.TickRate > 0 ? snapshot.TickRate : SimulationConfig.DefaultTicksPerSecond;
            var frameDelta = snapshot.Frame > _demoRecordingFirstSnapshotFrame
                ? snapshot.Frame - _demoRecordingFirstSnapshotFrame
                : 0UL;
            var snapshotDueMilliseconds = (int)Math.Clamp(
                Math.Round(frameDelta * 1000d / effectiveTickRate, MidpointRounding.AwayFromZero),
                0d,
                int.MaxValue);
            _demoRecordingLastDueMilliseconds = Math.Max(_demoRecordingLastDueMilliseconds, snapshotDueMilliseconds);
            return _demoRecordingLastDueMilliseconds;
        }

        var startMilliseconds = _demoRecordingStartedAtMilliseconds >= 0
            ? _demoRecordingStartedAtMilliseconds
            : _clock.ElapsedMilliseconds;
        var elapsedMilliseconds = (int)Math.Clamp(_clock.ElapsedMilliseconds - startMilliseconds, 0L, int.MaxValue);
        _demoRecordingLastDueMilliseconds = Math.Max(_demoRecordingLastDueMilliseconds, elapsedMilliseconds);
        return _demoRecordingLastDueMilliseconds;
    }

    private string FinalizeDemoRecording(bool saveRecording, bool completedByDisconnect)
    {
        var armedPath = _armedDemoRecordingPath;
        if (_demoRecorder is null)
        {
            if (string.IsNullOrWhiteSpace(armedPath))
            {
                return string.Empty;
            }

            _armedDemoRecordingPath = null;
            return saveRecording
                ? $"demo recording canceled before any welcome payload was captured ({armedPath})"
                : $"demo recording canceled ({armedPath})";
        }

        var recorder = _demoRecorder;
        _demoRecorder = null;
        _armedDemoRecordingPath = null;

        try
        {
            var finalPath = recorder.FinalPath;
            var messageCount = recorder.MessageCount;
            var payloadBytes = recorder.PayloadByteCount;
            var snapshotCount = _demoRecordingRecordedSnapshots;
            if (!saveRecording || snapshotCount <= 0 || messageCount <= 1)
            {
                recorder.Discard();
                var discardedMessage = !saveRecording
                    ? $"demo recording canceled ({finalPath})"
                    : $"demo recording discarded: no snapshots were captured ({finalPath})";
                ResetDemoRecordingTimingState();
                if (completedByDisconnect)
                {
                    _lastCompletedDemoRecordingNotice = discardedMessage;
                }

                return discardedMessage;
            }

            recorder.Complete();
            var completionMessage =
                $"demo recording saved: {finalPath} messages={messageCount} snapshots={snapshotCount} bytes={payloadBytes}";
            ResetDemoRecordingTimingState();
            if (completedByDisconnect)
            {
                _lastCompletedDemoRecordingNotice = completionMessage;
            }

            return completionMessage;
        }
        finally
        {
            ResetDemoRecordingTimingState();
        }
    }

    private void ResetDemoRecordingTimingState()
    {
        _demoRecordingRecordedSnapshots = 0;
        _demoRecordingFirstSnapshotFrame = 0;
        _demoRecordingFirstSnapshotFrameInitialized = false;
        _demoRecordingLastDueMilliseconds = 0;
        _demoRecordingStartedAtMilliseconds = -1;
    }

    private static bool ShouldRecordDemoMessage(IProtocolMessage message)
    {
        return message is SnapshotMessage
            or ChatRelayMessage
            or AutoBalanceNoticeMessage
            or SessionSlotChangedMessage
            or ControlAckMessage
            or ServerPluginMessage;
    }

    public void Dispose()
    {
        Disconnect();
    }

    private void QueueControlCommand(ControlCommandKind kind, byte value, string textValue = "")
    {
        _pendingControlCommands[kind] = new PendingControlCommand(_nextControlSequence++, kind, value, textValue);
    }

    private void TrackInputRoundTrip(uint sequence)
    {
        var sentAtMilliseconds = _clock.ElapsedMilliseconds;
        _trackedInputRoundTrips.Enqueue(new TrackedInputRoundTrip(sequence, sentAtMilliseconds));
        _trackedInputRoundTripTimes[sequence] = sentAtMilliseconds;
        while (_trackedInputRoundTrips.Count > MaxTrackedInputRoundTrips)
        {
            var dropped = _trackedInputRoundTrips.Dequeue();
            _trackedInputRoundTripTimes.Remove(dropped.Sequence);
        }
    }

    private void SendPendingControlCommands()
    {
        if (!IsConnected)
        {
            return;
        }

        foreach (var pending in _pendingControlCommands.Values)
        {
            Send(new ControlCommandMessage(pending.Sequence, pending.Kind, pending.Value, pending.TextValue));
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
                _transport?.Send(pending.Payload);
            }
        }
    }

    private void FlushPendingOutboundPackets()
    {
        var transport = _transport;
        if (transport is null)
        {
            _pendingOutboundPackets.Clear();
            return;
        }

        while (_pendingOutboundPackets.Count > 0 && _pendingOutboundPackets.Peek().ReleaseAtMilliseconds <= _clock.ElapsedMilliseconds)
        {
            var pending = _pendingOutboundPackets.Dequeue();
            transport.Send(pending.Payload);
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

        Send(new HelloMessage(_pendingHelloPlayerName, ProtocolVersion.Current, _pendingHelloBadgeMask));
        _lastHelloSentAtMilliseconds = _clock.ElapsedMilliseconds;
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
        return _transport?.IsLoopbackConnection == true;
    }

    private void FlushTransportState()
    {
        var transport = _transport;
        if (transport is null || !transport.TryConsumeDisconnectReason(out var reason))
        {
            return;
        }

        _lastDisconnectReason = string.IsNullOrWhiteSpace(reason)
            ? "Connection closed."
            : reason;
        Disconnect();
    }

    private void RecordSendDiagnostics(IProtocolMessage message, int payloadBytes)
    {
        var current = TotalSendDiagnostics;
        TotalSendDiagnostics = current with
        {
            PacketsSent = current.PacketsSent + 1,
            BytesSent = current.BytesSent + Math.Max(0, payloadBytes),
            HelloMessagesSent = current.HelloMessagesSent + (message is HelloMessage ? 1 : 0),
            InputMessagesSent = current.InputMessagesSent + (message is InputStateMessage ? 1 : 0),
            ControlMessagesSent = current.ControlMessagesSent + (message is ControlCommandMessage ? 1 : 0),
            SnapshotAckMessagesSent = current.SnapshotAckMessagesSent + (message is SnapshotAckMessage ? 1 : 0),
        };
    }

    private sealed record PendingControlCommand(uint Sequence, ControlCommandKind Kind, byte Value, string TextValue);
    private sealed record TrackedInputRoundTrip(uint Sequence, long SentAtMilliseconds);
    private sealed record PendingPacket(long ReleaseAtMilliseconds, byte[] Payload);
    private sealed record PendingMessage(long ReleaseAtMilliseconds, IProtocolMessage Message);
}



