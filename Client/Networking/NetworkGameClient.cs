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
    public readonly record struct ReplayPlaybackState(
        bool IsPaused,
        float PlaybackRate,
        int CurrentTick,
        int TotalTicks,
        bool CanSeek,
        int PositionMilliseconds,
        int DurationMilliseconds,
        bool IsSeekCatchUpPending);

    internal readonly record struct ReceiveDiagnostics(
        int PacketsRead,
        int BytesRead,
        int ReleasedMessages,
        int SnapshotMessages,
        int SnapshotMaxPayloadBytes,
        int MaxPayloadBytes,
        int PendingInboundMessages,
        bool ReceiveBudgetHit,
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
    private const int MaxTrackedPingRoundTrips = 32;
    private const int MaxPendingLastToDieCommands = 128;
    private const long LastToDieCommandRetryMilliseconds = 250;
    private const int MaxReceivePacketsPerFrame = 256;
    private const double MaxReceiveMillisecondsPerFrame = 4d;
    private const long PingIntervalMilliseconds = 1000;
    private const InputButtons Protocol64OneShotInputMask =
        InputButtons.Up
        | InputButtons.BuildSentry
        | InputButtons.DestroySentry
        | InputButtons.DestroyDispenser
        | InputButtons.Taunt
        | InputButtons.FirePrimary
        | InputButtons.FireSecondary
        | InputButtons.DropIntel
        | InputButtons.UseAbility
        | InputButtons.InteractWeapon
        | InputButtons.SwapWeapon
        | InputButtons.ToggleSecondaryWeapon
        | InputButtons.ReadyUp
        | InputButtons.BuildDispenser;

    [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "The client transport seam must support browser WebSocket adapters.")]
    private INetworkClientMessageTransport? _transport;
    private uint _nextInputSequence = 1;
    private uint _nextProtocol64CommandSequence = 1;
    private ulong _nextProtocol64FrameId = 1;
    private ulong _nextProtocol64CommandId = 1;
    private ulong _nextLastToDieCommandId = 1;
    private readonly Guid _defaultClientInstanceId = Guid.NewGuid();
    private ulong _lastToDieContentReadyStageInstanceId;
    private ulong _protocol64ConnectionEpoch = 1;
    private PlayerInputSnapshot _lastProtocol64Input;
    private readonly Protocol64SchemaRegistry _protocol64Registry = Protocol64SchemaRegistryFactory.CreateDefault();
    private readonly Protocol64StateApplier _protocol64State = new();
    private readonly LastToDieReplicatedState _lastToDieState = new();
    private readonly Dictionary<ulong, PendingLastToDieCommand> _pendingLastToDieCommands = [];
    private readonly Dictionary<ulong, Protocol64InputCommandResult> _protocol64InputResults = new();
    private uint _nextControlSequence = 1;
    private int _pendingChatBubbleFrameIndex = -1;
    private readonly Dictionary<ControlCommandKind, PendingControlCommand> _pendingControlCommands = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Queue<PendingPacket> _pendingOutboundPackets = new();
    private readonly Queue<PendingMessage> _pendingInboundMessages = new();
    private readonly Queue<TrackedInputRoundTrip> _trackedInputRoundTrips = new();
    private readonly Dictionary<uint, long> _trackedInputRoundTripTimes = new();
    private readonly Queue<TrackedPingRoundTrip> _trackedPingRoundTrips = new();
    private readonly Dictionary<uint, long> _trackedPingRoundTripTimes = new();
    private readonly Queue<PendingDelayedInput> _pendingDelayedInputs = new();
    private ulong _networkInputTick;
    private uint _nextPingSequence = 1;
    private string? _pendingHelloPlayerName;
    private ulong _pendingHelloBadgeMask;
    private string _pendingHelloFriendCode = string.Empty;
    private string _pendingHelloPlayerCardJson = string.Empty;
    private ConnectionIntent _pendingHelloIntent = ConnectionIntent.Join;
    private Guid _pendingHelloClientInstanceId;
    private long _connectStartedAtMilliseconds = -1;
    private long _lastHelloSentAtMilliseconds = -1;
    private long _lastPingSentAtMilliseconds = -1;
    private long _lastServerMessageReceivedAtMilliseconds = -1;
    private string? _lastDisconnectReason;
    private OpenGarrisonDemoRecordingWriter? _demoRecorder;
    private string? _armedDemoRecordingPath;
    private bool _demoRecordingIsAutomatic;
    private int _demoRecordingRecordedSnapshots;
    private ulong _demoRecordingFirstSnapshotFrame;
    private bool _demoRecordingFirstSnapshotFrameInitialized;
    private int _demoRecordingLastDueMilliseconds;
    private long _demoRecordingStartedAtMilliseconds = -1;
    private string? _lastCompletedDemoRecordingNotice;
    private bool _hasProtocolPingSample;
    private int _smoothedPingMilliseconds = -1;

    public bool CollectDiagnostics { get; set; }

    /// <summary>
    /// Canary switch for the protocol-64 event path. Legacy transports remain
    /// available until a WebSocket/QUIC container is selected explicitly.
    /// </summary>
    public bool Protocol64ModeEnabled { get; set; }

    public Protocol64StateApplier Protocol64State => _protocol64State;

    public LastToDieReplicatedState LastToDieState => _lastToDieState;

    public bool TryGetProtocol64PlayerState(byte slot, out Protocol64PlayerState state)
        => _protocol64State.TryGetPlayerState(slot, out state);

    public void ApplyProtocol64StateToWorld(SimulationWorld world)
    {
        if (Protocol64ModeEnabled)
        {
            _protocol64State.ApplyToWorld(world);
        }
    }

    // Keep this as a diagnostics knob, but do not add deliberate input latency by default.
    public int NetworkInputDelayTicks { get; set; }
    public bool IsConnected => _transport is not null;
    public bool IsAwaitingWelcome => IsConnected && LocalPlayerSlot == 0;
    public bool IsSpectator => IsConnected && LocalPlayerSlot >= SimulationWorld.FirstSpectatorSlot;
    public bool IsReplayConnection { get; private set; }
    public bool IsDemoRecordingActive => _demoRecorder is not null || !string.IsNullOrWhiteSpace(_armedDemoRecordingPath);
    public bool IsAutomaticDemoRecordingActive => IsDemoRecordingActive && _demoRecordingIsAutomatic;

    public byte LocalPlayerSlot { get; private set; }
    public string? ServerDescription { get; private set; }
    public string? ReplayDisplayName { get; private set; }
    public string? ReplayServerName { get; private set; }
    public string? ReplayMapName { get; private set; }
    public DateTime? ReplayDateUtc { get; private set; }
    public int ServerMaxPlayerCount { get; private set; }
    public Uri? MapDownloadBaseUri { get; private set; }
    public int SimulatedLatencyMilliseconds { get; private set; }
    public int EstimatedPingMilliseconds { get; private set; } = -1;
    public int ProtocolPingMilliseconds { get; private set; } = -1;
    public int InputAckLatencyMilliseconds { get; private set; } = -1;
    public ReceiveDiagnostics LastReceiveDiagnostics { get; private set; }
    public SendDiagnostics TotalSendDiagnostics { get; private set; }

    public bool Connect(
        string host,
        int port,
        string playerName,
        ulong badgeMask,
        out string error,
        string friendCode = "",
        string playerCardJson = "",
        ConnectionIntent intent = ConnectionIntent.Join,
        Guid clientInstanceId = default)
    {
        error = string.Empty;
        var armedDemoRecordingPath = _demoRecorder is null ? _armedDemoRecordingPath : null;
        var armedDemoRecordingIsAutomatic = _demoRecorder is null && _demoRecordingIsAutomatic;
        Disconnect();
        if (!string.IsNullOrWhiteSpace(armedDemoRecordingPath))
        {
            _armedDemoRecordingPath = armedDemoRecordingPath;
            _demoRecordingIsAutomatic = armedDemoRecordingIsAutomatic;
        }

        try
        {
            var protocol64Endpoint = IsProtocol64Endpoint(host);
            var hasMapDownloadBaseUri = CustomMapSyncService.TryCreateServerDownloadBaseUri(host, port, out var mapDownloadBaseUri);
            if (!NetworkClientMessageTransportRegistry.TryConnect(host, port, out var transport, out error) || transport is null)
            {
                return false;
            }

            Protocol64ModeEnabled = protocol64Endpoint;
            if (!Connect(
                    transport,
                    playerName,
                    badgeMask,
                    out error,
                    friendCode,
                    playerCardJson,
                    intent,
                    clientInstanceId))
            {
                return false;
            }

            MapDownloadBaseUri = hasMapDownloadBaseUri ? mapDownloadBaseUri : null;
            return true;
        }
        catch (SocketException ex)
        {
            Disconnect();
            error = ex.Message;
            return false;
        }
    }

    public bool Connect(
        INetworkClientMessageTransport transport,
        string playerName,
        ulong badgeMask,
        out string error,
        string friendCode = "",
        string playerCardJson = "",
        ConnectionIntent intent = ConnectionIntent.Join,
        Guid clientInstanceId = default)
    {
        error = string.Empty;
        ArgumentNullException.ThrowIfNull(transport);
        var armedDemoRecordingPath = _demoRecorder is null ? _armedDemoRecordingPath : null;
        var armedDemoRecordingIsAutomatic = _demoRecorder is null && _demoRecordingIsAutomatic;
        Disconnect();
        if (!string.IsNullOrWhiteSpace(armedDemoRecordingPath))
        {
            _armedDemoRecordingPath = armedDemoRecordingPath;
            _demoRecordingIsAutomatic = armedDemoRecordingIsAutomatic;
        }

        _transport = transport;
        if (IsProtocol64Endpoint(transport.RemoteDescription))
        {
            Protocol64ModeEnabled = true;
        }
        if (transport is IPlaybackMessageTransport playbackTransport)
        {
            IsReplayConnection = true;
            ReplayDisplayName = string.IsNullOrWhiteSpace(playbackTransport.PlaybackDisplayName)
                ? null
                : playbackTransport.PlaybackDisplayName.Trim();
            ReplayServerName = string.IsNullOrWhiteSpace(playbackTransport.PlaybackServerName)
                ? null
                : playbackTransport.PlaybackServerName.Trim();
            ReplayMapName = string.IsNullOrWhiteSpace(playbackTransport.PlaybackMapName)
                ? null
                : playbackTransport.PlaybackMapName.Trim();
            ReplayDateUtc = playbackTransport.PlaybackDateUtc;
        }
        else
        {
            IsReplayConnection = transport.RemoteDescription.StartsWith("replay:", StringComparison.OrdinalIgnoreCase);
            ReplayDisplayName = null;
            ReplayServerName = null;
            ReplayMapName = null;
            ReplayDateUtc = null;
        }

        NetworkInputDelayTicks = 0;
        _pendingHelloPlayerName = playerName;
        _pendingHelloBadgeMask = badgeMask;
        _pendingHelloFriendCode = NormalizeSocialProfileField(friendCode, ProtocolCodec.MaxFriendCodeBytes);
        _pendingHelloPlayerCardJson = NormalizeSocialProfileField(playerCardJson, ProtocolCodec.MaxPlayerCardBytes);
        _pendingHelloIntent = intent;
        _pendingHelloClientInstanceId = clientInstanceId == Guid.Empty
            ? _defaultClientInstanceId
            : clientInstanceId;
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
        _nextProtocol64CommandSequence = 1;
        _nextProtocol64FrameId = 1;
        _nextProtocol64CommandId = 1;
        _nextLastToDieCommandId = 1;
        _lastToDieContentReadyStageInstanceId = 0;
        _protocol64ConnectionEpoch = 1;
        _lastProtocol64Input = default;
        _protocol64InputResults.Clear();
        _protocol64State.Reset();
        _lastToDieState.Reset();
        _pendingLastToDieCommands.Clear();
        _nextControlSequence = 1;
        _pendingChatBubbleFrameIndex = -1;
        _pendingControlCommands.Clear();
        _pendingOutboundPackets.Clear();
        _pendingInboundMessages.Clear();
        _pendingDelayedInputs.Clear();
        _trackedInputRoundTrips.Clear();
        _trackedInputRoundTripTimes.Clear();
        _trackedPingRoundTrips.Clear();
        _trackedPingRoundTripTimes.Clear();
        _networkInputTick = 0;
        _nextPingSequence = 1;
        IsReplayConnection = false;
        LocalPlayerSlot = 0;
        ServerDescription = null;
        MapDownloadBaseUri = null;
        ReplayDisplayName = null;
        ReplayServerName = null;
        ReplayMapName = null;
        ReplayDateUtc = null;
        ServerMaxPlayerCount = 0;
        _pendingHelloPlayerName = null;
        _pendingHelloBadgeMask = 0UL;
        _pendingHelloFriendCode = string.Empty;
        _pendingHelloPlayerCardJson = string.Empty;
        _pendingHelloIntent = ConnectionIntent.Join;
        _connectStartedAtMilliseconds = -1;
        _lastHelloSentAtMilliseconds = -1;
        _lastPingSentAtMilliseconds = -1;
        _lastServerMessageReceivedAtMilliseconds = -1;
        _hasProtocolPingSample = false;
        _smoothedPingMilliseconds = -1;
        EstimatedPingMilliseconds = -1;
        ProtocolPingMilliseconds = -1;
        InputAckLatencyMilliseconds = -1;
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

    public bool TryGetReplayPlaybackState(out ReplayPlaybackState state)
    {
        if (_transport is not IPlaybackMessageTransport replayTransport)
        {
            state = default;
            return false;
        }

        if (replayTransport is ISeekablePlaybackMessageTransport seekableTransport)
        {
            state = new ReplayPlaybackState(
                replayTransport.IsPaused,
                replayTransport.PlaybackRate,
                replayTransport.CurrentTick,
                replayTransport.TotalTicks,
                CanSeek: true,
                seekableTransport.PositionMilliseconds,
                seekableTransport.DurationMilliseconds,
                seekableTransport.IsSeekCatchUpPending);
            return true;
        }

        state = new ReplayPlaybackState(
            replayTransport.IsPaused,
            replayTransport.PlaybackRate,
            replayTransport.CurrentTick,
            replayTransport.TotalTicks,
            CanSeek: false,
            PositionMilliseconds: 0,
            DurationMilliseconds: 0,
            IsSeekCatchUpPending: false);
        return true;
    }

    public bool TryCreateSeekedReplayTransport(
        int deltaMilliseconds,
        out INetworkClientMessageTransport? transport,
        out int targetMilliseconds,
        out string error)
    {
        transport = null;
        targetMilliseconds = 0;
        error = string.Empty;
        if (_transport is not ISeekablePlaybackMessageTransport replayTransport)
        {
            error = "the active replay does not support seeking.";
            return false;
        }

        var currentPosition = replayTransport.PositionMilliseconds;
        var maximumPosition = Math.Max(0, replayTransport.DurationMilliseconds - 1);
        targetMilliseconds = (int)Math.Clamp((long)currentPosition + deltaMilliseconds, 0L, maximumPosition);
        if (targetMilliseconds == currentPosition)
        {
            error = deltaMilliseconds < 0
                ? "the replay is already at the beginning."
                : "the replay is already at the end.";
            return false;
        }

        transport = replayTransport.CreateSeekedPlayback(targetMilliseconds);
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
        status = replayTransport is ISeekablePlaybackMessageTransport seekableTransport
            ? $"replay {pauseLabel} time={FormatReplayTime(seekableTransport.PositionMilliseconds)}/{FormatReplayTime(seekableTransport.DurationMilliseconds)} " +
              $"tick={replayTransport.CurrentTick}/{replayTransport.TotalTicks} speed={(replayTransport.PlaybackRate * 100f).ToString("0", CultureInfo.InvariantCulture)}%"
            : $"replay {pauseLabel} tick={replayTransport.CurrentTick}/{replayTransport.TotalTicks} speed={(replayTransport.PlaybackRate * 100f).ToString("0", CultureInfo.InvariantCulture)}%";
        return true;
    }

    private static string FormatReplayTime(int milliseconds)
    {
        var totalSeconds = Math.Max(0, milliseconds) / 1000;
        return $"{totalSeconds / 60}:{totalSeconds % 60:00}";
    }

    public bool TryStartDemoRecording(
        string demoPath,
        string remoteDescription,
        byte[]? initialWelcomePayload,
        out string status,
        out string error,
        bool automatic = false)
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
            return TryCreateActiveDemoRecorder(
                resolvedPath,
                remoteDescription,
                initialWelcomePayload,
                automatic,
                out status,
                out error);
        }

        _armedDemoRecordingPath = resolvedPath;
        _demoRecordingIsAutomatic = automatic;
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

    public void QueueGameplayClassSelection(string gameplayClassId)
    {
        if (string.IsNullOrWhiteSpace(gameplayClassId))
        {
            return;
        }

        QueueControlCommand(ControlCommandKind.SelectClass, 0, gameplayClassId.Trim());
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

    public void SendCustomBubbleUpload(byte slot, uint revision, byte[] rgba64Pixels)
    {
        if (!IsConnected
            || IsAwaitingWelcome
            || IsReplayConnection
            || rgba64Pixels.Length != ProtocolCodec.CustomBubbleRgba64PayloadBytes)
        {
            return;
        }

        Send(new CustomBubbleUploadMessage(slot, revision, rgba64Pixels));
    }

    public void SendCustomBubbleClear()
    {
        if (!IsConnected || IsAwaitingWelcome || IsReplayConnection)
        {
            return;
        }

        Send(new CustomBubbleClearMessage(0));
    }

    public void UpdatePlayerProfile(string playerName, ulong badgeMask, string? friendCode = null, string? playerCardJson = null)
    {
        _pendingHelloPlayerName = playerName;
        _pendingHelloBadgeMask = badgeMask;
        if (friendCode is not null)
        {
            _pendingHelloFriendCode = NormalizeSocialProfileField(friendCode, ProtocolCodec.MaxFriendCodeBytes);
        }

        if (playerCardJson is not null)
        {
            _pendingHelloPlayerCardJson = NormalizeSocialProfileField(playerCardJson, ProtocolCodec.MaxPlayerCardBytes);
        }

        if (!IsConnected || IsAwaitingWelcome)
        {
            return;
        }

        Send(new PlayerProfileUpdateMessage(playerName, badgeMask, _pendingHelloFriendCode, _pendingHelloPlayerCardJson));
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

    public uint SendInput(PlayerInputSnapshot input, float aimOriginX, float aimOriginY)
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
        if (input.BuildDispenser) buttons |= InputButtons.BuildDispenser;
        if (input.DestroySentry) buttons |= InputButtons.DestroySentry;
        if (input.DestroyDispenser) buttons |= InputButtons.DestroyDispenser;
        if (input.Taunt) buttons |= InputButtons.Taunt;
        if (input.FirePrimary) buttons |= InputButtons.FirePrimary;
        if (input.FireSecondary) buttons |= InputButtons.FireSecondary;
        if (input.DropIntel) buttons |= InputButtons.DropIntel;
        if (input.UseAbility) buttons |= InputButtons.UseAbility;
        if (input.InteractWeapon) buttons |= InputButtons.InteractWeapon;
        if (input.SwapWeapon) buttons |= InputButtons.SwapWeapon;
        if (input.ToggleSecondaryWeapon) buttons |= InputButtons.ToggleSecondaryWeapon;
        if (input.ReadyUp) buttons |= InputButtons.ReadyUp;
        if (input.IsTypingChatMessage) buttons |= InputButtons.IsTypingChatMessage;

        if (Protocol64ModeEnabled)
        {
            // Team/class selections are reliable control commands queued by
            // the gameplay menus. Flush them on the protocol-64 path too;
            // otherwise QUIC sends input and receives snapshots but never
            // tells the server that this client selected a playable roster.
            SendPendingControlCommands();

            var protocol64Sequence = _nextInputSequence++;
            var heldButtons = buttons & ~Protocol64OneShotInputMask;
            TrySendProtocol64Event(new InputStateMessage(
                protocol64Sequence,
                heldButtons,
                input.AimWorldX - aimOriginX,
                input.AimWorldY - aimOriginY,
                _pendingChatBubbleFrameIndex,
                input.IsUsingBinoculars,
                input.BinocularsFocusX,
                input.BinocularsFocusY,
                EstimatedPingMilliseconds));
            if (SendProtocol64InputEdges(input, _lastProtocol64Input, protocol64Sequence, buttons, aimOriginX, aimOriginY))
            {
                _lastProtocol64Input = input;
            }
            _pendingChatBubbleFrameIndex = -1;
            return protocol64Sequence;
        }

        SendPendingControlCommands();
        var sequence = _nextInputSequence++;
        var inputMessage = new InputStateMessage(
            sequence, 
            buttons, 
            input.AimWorldX - aimOriginX, 
            input.AimWorldY - aimOriginY, 
            _pendingChatBubbleFrameIndex,
            input.IsUsingBinoculars,
            input.BinocularsFocusX,
            input.BinocularsFocusY,
            EstimatedPingMilliseconds);
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

    public ulong SendProtocol64InputCommand(
        Protocol64InputCommandKind kind,
        InputButtons heldButtons,
        float aimRelX,
        float aimRelY,
        uint clientTick = 0,
        uint inputSequence = 0)
    {
        if (!Protocol64ModeEnabled || !IsConnected)
        {
            return 0;
        }

        var command = new Protocol64InputCommand(
            _nextProtocol64CommandId++,
            inputSequence == 0 ? _nextInputSequence++ : inputSequence,
            kind,
            heldButtons,
            aimRelX,
            aimRelY,
            clientTick,
            _nextProtocol64CommandSequence++);
        return TrySendProtocol64Event(command) ? command.CommandId : 0;
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
                var inputAckMilliseconds = (int)Math.Clamp(nowMilliseconds - sentAtMilliseconds, 0L, int.MaxValue);
                InputAckLatencyMilliseconds = inputAckMilliseconds;
                if (!_hasProtocolPingSample)
                {
                    EstimatedPingMilliseconds = inputAckMilliseconds;
                }
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
        FlushLastToDieCommands();
        FlushPendingOutboundPackets();
        FlushPingState();
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
        var snapshotMaxPayloadBytes = 0;
        var maxPayloadBytes = 0;
        var deserializeMilliseconds = 0d;
        var maxDeserializeMilliseconds = 0d;
        var receiveBudgetHit = false;
        var receiveStartTimestamp = Stopwatch.GetTimestamp();
        var messages = new List<IProtocolMessage>();
        while (transport.HasPendingMessages)
        {
            if (packetsRead >= MaxReceivePacketsPerFrame
                || (packetsRead > 0
                    && Stopwatch.GetElapsedTime(receiveStartTimestamp).TotalMilliseconds >= MaxReceiveMillisecondsPerFrame))
            {
                receiveBudgetHit = true;
                break;
            }

            try
            {
                if (!transport.TryReceive(out var payload))
                {
                    break;
                }

                packetsRead += 1;
                if (collectDiagnostics)
                {
                    bytesRead += payload.Length;
                    maxPayloadBytes = Math.Max(maxPayloadBytes, payload.Length);
                }

                if (payload.Length >= sizeof(uint)
                    && System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(payload) == Protocol64FrameHeader.Magic)
                {
                    if (HandleProtocol64Frame(transport, payload) is { } protocol64Message)
                    {
                        messages.Add(protocol64Message);
                    }
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
                CaptureInboundDemoMessage(transport, message, payload);
                if (collectDiagnostics && message is SnapshotMessage)
                {
                    snapshotMessages += 1;
                    snapshotMaxPayloadBytes = Math.Max(snapshotMaxPayloadBytes, payload.Length);
                }

                if (SimulatedLatencyMilliseconds > 0)
                {
                    _pendingInboundMessages.Enqueue(new PendingMessage(_clock.ElapsedMilliseconds + SimulatedLatencyMilliseconds, message));
                }
                else if (TryHandleInternalMessage(message))
                {
                    continue;
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
        var releasedDelayedMessages = 0;
        while (releasedDelayedMessages < MaxReceivePacketsPerFrame
            && _pendingInboundMessages.Count > 0
            && _pendingInboundMessages.Peek().ReleaseAtMilliseconds <= _clock.ElapsedMilliseconds)
        {
            var message = _pendingInboundMessages.Dequeue().Message;
            releasedDelayedMessages += 1;
            if (!TryHandleInternalMessage(message))
            {
                messages.Add(message);
            }
        }

        LastReceiveDiagnostics = collectDiagnostics
            ? new ReceiveDiagnostics(
                packetsRead,
                bytesRead,
                messages.Count,
                snapshotMessages,
                snapshotMaxPayloadBytes,
                maxPayloadBytes,
                _pendingInboundMessages.Count,
                receiveBudgetHit,
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

    public bool TryGetProtocol64InputResult(
        ulong commandId,
        out Protocol64InputCommandResult result)
        => _protocol64InputResults.TryGetValue(commandId, out result!);

    public ulong SendLastToDieCommand(
        LastToDieCommandKind kind,
        string selectedId = "",
        ulong offerId = 0)
    {
        if (!IsConnected || _lastToDieState.Snapshot is not { } snapshot)
        {
            return 0;
        }

        var commandId = _nextLastToDieCommandId++;
        var command = new LastToDieCommandMessage(
            commandId,
            snapshot.RunId,
            snapshot.StructuralRevision,
            kind,
            snapshot.StageInstanceId,
            offerId,
            selectedId ?? string.Empty);
        if (_pendingLastToDieCommands.Count >= MaxPendingLastToDieCommands)
        {
            _pendingLastToDieCommands.Remove(_pendingLastToDieCommands.Keys.Min());
        }

        _pendingLastToDieCommands.Add(
            commandId,
            new PendingLastToDieCommand(command, _clock.ElapsedMilliseconds));
        Send(command);
        return commandId;
    }

    public void SendLastToDieLeave()
    {
        if (IsConnected && _lastToDieState.Snapshot is not null)
        {
            SendLastToDieCommand(LastToDieCommandKind.Leave);
        }
    }

    public void NotifyWorldSnapshotApplied(SnapshotMessage snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (_lastToDieState.Snapshot is not { } lastToDie
            || lastToDie.Phase != LastToDieWirePhase.LoadingStage
            || lastToDie.StageInstanceId == 0
            || lastToDie.BaselineStartFrame == 0
            || snapshot.Frame < lastToDie.BaselineStartFrame
            || !string.Equals(snapshot.LevelName, lastToDie.CurrentMap, StringComparison.OrdinalIgnoreCase)
            || _lastToDieContentReadyStageInstanceId == lastToDie.StageInstanceId)
        {
            return;
        }

        if (SendLastToDieCommand(
                LastToDieCommandKind.StageContentReady,
                selectedId: lastToDie.CurrentMap) != 0)
        {
            _lastToDieContentReadyStageInstanceId = lastToDie.StageInstanceId;
        }
    }

    private bool TrySendProtocol64Event(object eventValue)
    {
        var transport = _transport;
        if (!Protocol64ModeEnabled || transport is null)
        {
            return false;
        }

        var encoded = Protocol64FrameCodec.EncodeObject(
            _protocol64Registry,
            eventValue,
            _protocol64ConnectionEpoch,
            _nextProtocol64FrameId++,
            new Protocol64FrameEncodeOptions { Backend = "client" });

        if (!encoded.Succeeded || encoded.Payload is null)
        {
            return false;
        }

        var schema = _protocol64Registry.Get(encoded.Header!.SchemaId, encoded.Header.SchemaRevision);
        if (schema.Descriptor.Direction != Protocol64Direction.ClientToServer)
        {
            return false;
        }

        transport.Send(encoded.Payload);
        return true;
    }

    private bool SendProtocol64InputEdges(
        PlayerInputSnapshot input,
        PlayerInputSnapshot previous,
        uint inputSequence,
        InputButtons buttons,
        float aimOriginX,
        float aimOriginY)
    {
        var heldButtons = buttons & ~Protocol64OneShotInputMask;
        var aimRelX = input.AimWorldX - aimOriginX;
        var aimRelY = input.AimWorldY - aimOriginY;
        var allSent = true;
        allSent &= SendProtocol64Edge(input.Up && !previous.Up, Protocol64InputCommandKind.Jump, inputSequence, heldButtons, aimRelX, aimRelY);
        allSent &= SendProtocol64Edge(input.BuildSentry && !previous.BuildSentry, Protocol64InputCommandKind.BuildSentry, inputSequence, heldButtons, aimRelX, aimRelY);
        allSent &= SendProtocol64Edge(input.BuildDispenser && !previous.BuildDispenser, Protocol64InputCommandKind.BuildDispenser, inputSequence, heldButtons, aimRelX, aimRelY);
        allSent &= SendProtocol64Edge(input.DestroySentry && !previous.DestroySentry, Protocol64InputCommandKind.DestroySentry, inputSequence, heldButtons, aimRelX, aimRelY);
        allSent &= SendProtocol64Edge(input.DestroyDispenser && !previous.DestroyDispenser, Protocol64InputCommandKind.DestroyDispenser, inputSequence, heldButtons, aimRelX, aimRelY);
        allSent &= SendProtocol64Edge(input.Taunt && !previous.Taunt, Protocol64InputCommandKind.Taunt, inputSequence, heldButtons, aimRelX, aimRelY);
        allSent &= SendProtocol64Edge(input.FirePrimary && !previous.FirePrimary, Protocol64InputCommandKind.FirePrimary, inputSequence, heldButtons, aimRelX, aimRelY);
        allSent &= SendProtocol64Edge(input.FireSecondary && !previous.FireSecondary, Protocol64InputCommandKind.FireSecondary, inputSequence, heldButtons, aimRelX, aimRelY);
        allSent &= SendProtocol64Edge(input.DropIntel && !previous.DropIntel, Protocol64InputCommandKind.DropIntel, inputSequence, heldButtons, aimRelX, aimRelY);
        allSent &= SendProtocol64Edge(input.UseAbility && !previous.UseAbility, Protocol64InputCommandKind.UseAbility, inputSequence, heldButtons, aimRelX, aimRelY);
        allSent &= SendProtocol64Edge(input.InteractWeapon && !previous.InteractWeapon, Protocol64InputCommandKind.InteractWeapon, inputSequence, heldButtons, aimRelX, aimRelY);
        allSent &= SendProtocol64Edge(input.SwapWeapon && !previous.SwapWeapon, Protocol64InputCommandKind.SwapWeapon, inputSequence, heldButtons, aimRelX, aimRelY);
        allSent &= SendProtocol64Edge(input.ToggleSecondaryWeapon && !previous.ToggleSecondaryWeapon, Protocol64InputCommandKind.ToggleSecondaryWeapon, inputSequence, heldButtons, aimRelX, aimRelY);
        allSent &= SendProtocol64Edge(input.ReadyUp && !previous.ReadyUp, Protocol64InputCommandKind.ReadyUp, inputSequence, heldButtons, aimRelX, aimRelY);
        return allSent;
    }

    private bool SendProtocol64Edge(
        bool rising,
        Protocol64InputCommandKind kind,
        uint inputSequence,
        InputButtons heldButtons,
        float aimRelX,
        float aimRelY)
    {
        if (rising)
        {
            return SendProtocol64InputCommand(kind, heldButtons, aimRelX, aimRelY, unchecked((uint)_networkInputTick), inputSequence) != 0;
        }

        return true;
    }

    private IProtocolMessage? HandleProtocol64Frame(INetworkClientMessageTransport transport, byte[] payload)
    {
        var decoded = Protocol64FrameCodec.Decode(
            payload,
            _protocol64Registry,
            new Protocol64FrameDecodeOptions
            {
                Backend = "client",
                ExpectedDirection = Protocol64Direction.ServerToClient,
                FaultSink = new DelegateProtocol64FaultSink(fault =>
                {
                    Debug.WriteLine($"Protocol-64 frame ignored ({fault.Kind}): {fault.Message}");
                }),
            });

        if (!decoded.Succeeded || decoded.Event is null)
        {
            if (decoded.Header is { SchemaId: >= 32 and <= 35 })
            {
                TrySendProtocol64Event(_protocol64State.CreateResyncRequest(Protocol64StateResyncReason.InvalidState));
            }

            return null;
        }

        _lastServerMessageReceivedAtMilliseconds = _clock.ElapsedMilliseconds;
        if (decoded.Header is { } header)
        {
            _protocol64ConnectionEpoch = header.ConnectionEpoch;
        }

        CaptureInboundProtocol64DemoEvent(transport, decoded.Event, payload);

        switch (decoded.Event)
        {
            case Protocol64InputCommandResult result:
                _protocol64InputResults[result.CommandId] = result;
                TrySendProtocol64Event(new Protocol64InputCommandResultAck(result.CommandId));
                break;
            case Protocol64PlayerStateBatch players:
                SendStateRepairIfNeeded(_protocol64State.ApplyPlayerStateBatch(players));
                break;
            case Protocol64RosterState roster:
                SendStateRepairIfNeeded(_protocol64State.ApplyRosterState(roster));
                break;
            case Protocol64ProjectileState projectile:
                SendStateRepairIfNeeded(_protocol64State.ApplyProjectileState(projectile));
                break;
            case Protocol64ProjectileLifecycle lifecycle:
                SendStateRepairIfNeeded(_protocol64State.ApplyProjectileLifecycle(lifecycle));
                break;
            case Protocol64StateResyncResponse resync:
                SendStateRepairIfNeeded(_protocol64State.ApplyResyncResponse(resync));
                break;
            case LastToDieCommandResultMessage lastToDieResult:
                _lastToDieState.ApplyCommandResult(lastToDieResult);
                _pendingLastToDieCommands.Remove(lastToDieResult.CommandId);
                break;
            case LastToDieRunSnapshotMessage lastToDieSnapshot:
                var lastToDieApply = _lastToDieState.ApplySnapshot(lastToDieSnapshot);
                CompleteProvenLastToDieCommands(lastToDieSnapshot);
                if ((lastToDieApply.Kind is LastToDieSnapshotApplyKind.Applied
                        or LastToDieSnapshotApplyKind.Duplicate)
                    && _lastToDieState.CreateSnapshotAcknowledgement() is { } acknowledgement)
                {
                    TrySendProtocol64Event(acknowledgement);
                }
                break;
            case IProtocolMessage message:
                if (!TryHandleInternalMessage(message))
                {
                    return message;
                }
                break;
        }

        return null;
    }

    private void SendStateRepairIfNeeded(Protocol64StateApplyResult result)
    {
        if (result.RepairRequest is not null)
        {
            TrySendProtocol64Event(result.RepairRequest);
        }
    }

    private void Send(IProtocolMessage message)
    {
        var transport = _transport;
        if (transport is null)
        {
            return;
        }

        if (Protocol64ModeEnabled && TrySendProtocol64Event(message))
        {
            return;
        }

        var payload = ProtocolCodec.Serialize(message, GetSendCompressionSettings(message));
        RecordSendDiagnostics(message, payload.Length);
        if (SimulatedLatencyMilliseconds > 0)
        {
            _pendingOutboundPackets.Enqueue(new PendingPacket(_clock.ElapsedMilliseconds + SimulatedLatencyMilliseconds, payload));
            FlushPendingOutboundPackets();
            return;
        }

        transport.Send(payload);
    }

    private static ProtocolCompressionSettings GetSendCompressionSettings(IProtocolMessage message)
    {
        return message is CustomBubbleUploadMessage
            ? ProtocolCompressionSettings.AllMessages
            : ProtocolCompressionSettings.Default;
    }

    private static bool IsProtocol64Endpoint(string? endpoint)
        => endpoint?.StartsWith("ws64://", StringComparison.OrdinalIgnoreCase) == true
            || endpoint?.StartsWith("wss64://", StringComparison.OrdinalIgnoreCase) == true
            || endpoint?.StartsWith("quic64://", StringComparison.OrdinalIgnoreCase) == true;

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
                _demoRecordingIsAutomatic,
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

    private void CaptureInboundProtocol64DemoEvent(
        INetworkClientMessageTransport transport,
        object message,
        byte[] payload)
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
                _demoRecordingIsAutomatic,
                out _,
                out var activationError))
            {
                _lastCompletedDemoRecordingNotice = $"demo recording failed: {activationError}";
            }

            return;
        }

        if (message is WelcomeMessage)
        {
            return;
        }

        var dueMilliseconds = message is IProtocolMessage protocolMessage
            ? ResolveDemoMessageDueMilliseconds(protocolMessage)
            : ResolveDemoElapsedDueMilliseconds();
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
        bool automatic,
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
            _demoRecordingIsAutomatic = automatic;
            status = $"demo recording started: {resolvedPath}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            ResetDemoRecordingTimingState();
            _demoRecorder?.Dispose();
            _demoRecorder = null;
            _armedDemoRecordingPath = null;
            _demoRecordingIsAutomatic = false;
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

        return ResolveDemoElapsedDueMilliseconds();
    }

    private int ResolveDemoElapsedDueMilliseconds()
    {
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
            _demoRecordingIsAutomatic = false;
            return saveRecording
                ? $"demo recording canceled before any welcome payload was captured ({armedPath})"
                : $"demo recording canceled ({armedPath})";
        }

        var recorder = _demoRecorder;
        _demoRecorder = null;
        _armedDemoRecordingPath = null;
        _demoRecordingIsAutomatic = false;

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
            or PlayerSocialProfileUpdateMessage
            or CustomBubbleStateMessage
            or CustomBubbleClearMessage
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

    private void FlushPingState()
    {
        if (!IsConnected || IsAwaitingWelcome || IsReplayConnection)
        {
            return;
        }

        var nowMilliseconds = _clock.ElapsedMilliseconds;
        if (_lastPingSentAtMilliseconds >= 0
            && nowMilliseconds - _lastPingSentAtMilliseconds < PingIntervalMilliseconds)
        {
            return;
        }

        var sequence = _nextPingSequence++;
        _lastPingSentAtMilliseconds = nowMilliseconds;
        _trackedPingRoundTrips.Enqueue(new TrackedPingRoundTrip(sequence, nowMilliseconds));
        _trackedPingRoundTripTimes[sequence] = nowMilliseconds;
        while (_trackedPingRoundTrips.Count > MaxTrackedPingRoundTrips)
        {
            var dropped = _trackedPingRoundTrips.Dequeue();
            _trackedPingRoundTripTimes.Remove(dropped.Sequence);
        }

        Send(new PingRequestMessage(sequence));
    }

    private bool TryHandleInternalMessage(IProtocolMessage message)
    {
        switch (message)
        {
            case PingResponseMessage pingResponse:
                AcknowledgePing(pingResponse.Sequence);
                return true;
            case LastToDieCommandResultMessage result:
                _lastToDieState.ApplyCommandResult(result);
                _pendingLastToDieCommands.Remove(result.CommandId);
                return true;
            case LastToDieRunSnapshotMessage snapshot:
                _lastToDieState.ApplySnapshot(snapshot);
                CompleteProvenLastToDieCommands(snapshot);
                if (_lastToDieState.CreateSnapshotAcknowledgement() is { } acknowledgement)
                {
                    Send(acknowledgement);
                }

                return true;
            default:
                return false;
        }
    }

    private void AcknowledgePing(uint sequence)
    {
        if (sequence == 0 || _trackedPingRoundTrips.Count == 0)
        {
            return;
        }

        var nowMilliseconds = _clock.ElapsedMilliseconds;
        while (_trackedPingRoundTrips.Count > 0 && _trackedPingRoundTrips.Peek().Sequence <= sequence)
        {
            var tracked = _trackedPingRoundTrips.Dequeue();
            if (!_trackedPingRoundTripTimes.Remove(tracked.Sequence, out var sentAtMilliseconds))
            {
                continue;
            }

            if (tracked.Sequence == sequence)
            {
                var pingMilliseconds = (int)Math.Clamp(nowMilliseconds - sentAtMilliseconds, 0L, int.MaxValue);
                _hasProtocolPingSample = true;
                ProtocolPingMilliseconds = SmoothPingSample(pingMilliseconds);
                EstimatedPingMilliseconds = ProtocolPingMilliseconds;
            }
        }
    }

    private int SmoothPingSample(int pingMilliseconds)
    {
        if (_smoothedPingMilliseconds < 0)
        {
            _smoothedPingMilliseconds = pingMilliseconds;
        }
        else
        {
            _smoothedPingMilliseconds = (int)Math.Round((_smoothedPingMilliseconds * 3d + pingMilliseconds) / 4d);
        }

        return _smoothedPingMilliseconds;
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

    private void FlushLastToDieCommands()
    {
        if (!IsConnected || _pendingLastToDieCommands.Count == 0)
        {
            return;
        }

        var nowMilliseconds = _clock.ElapsedMilliseconds;
        foreach (var pending in _pendingLastToDieCommands.Values)
        {
            if (nowMilliseconds - pending.LastSentAtMilliseconds < LastToDieCommandRetryMilliseconds)
            {
                continue;
            }

            Send(pending.Command);
            pending.LastSentAtMilliseconds = nowMilliseconds;
        }
    }

    private void CompleteProvenLastToDieCommands(LastToDieRunSnapshotMessage snapshot)
    {
        if (_pendingLastToDieCommands.Count == 0)
        {
            return;
        }

        var localPlayer = snapshot.Players.FirstOrDefault(player => player.Slot == LocalPlayerSlot);
        var completed = new List<ulong>();
        foreach (var (commandId, pending) in _pendingLastToDieCommands)
        {
            var command = pending.Command;
            if (command.RunId != snapshot.RunId
                || snapshot.StructuralRevision < command.ExpectedStructuralRevision)
            {
                continue;
            }

            var proven = command.Kind switch
            {
                LastToDieCommandKind.RequestStart
                    => snapshot.Phase != LastToDieWirePhase.Lobby,
                LastToDieCommandKind.ChooseSurvivor
                    => string.Equals(localPlayer?.SurvivorId, command.SelectedId, StringComparison.Ordinal),
                LastToDieCommandKind.SelectReward
                    => localPlayer?.OwnedPerkIds.Contains(command.SelectedId, StringComparer.Ordinal) == true,
                LastToDieCommandKind.Ready or LastToDieCommandKind.StageContentReady
                    => localPlayer?.IsReady == true
                        || snapshot.Phase is LastToDieWirePhase.Playing
                            or LastToDieWirePhase.Won
                            or LastToDieWirePhase.Lost,
                LastToDieCommandKind.Leave
                    => localPlayer is null,
                LastToDieCommandKind.Retry
                    => localPlayer?.IsReady == true
                        || snapshot.Phase != LastToDieWirePhase.Lost,
                LastToDieCommandKind.ReturnToLobby
                    => snapshot.Phase == LastToDieWirePhase.Lobby,
                _ => false,
            };
            if (proven)
            {
                completed.Add(commandId);
            }
        }

        foreach (var commandId in completed)
        {
            _pendingLastToDieCommands.Remove(commandId);
        }
    }

    private void SendHello()
    {
        if (_pendingHelloPlayerName is null)
        {
            return;
        }

        Send(new HelloMessage(
            _pendingHelloPlayerName,
            ProtocolVersion.Current,
            _pendingHelloBadgeMask,
            _pendingHelloFriendCode,
            _pendingHelloPlayerCardJson,
            _pendingHelloIntent,
            _pendingHelloClientInstanceId));
        _lastHelloSentAtMilliseconds = _clock.ElapsedMilliseconds;
    }

    private static string NormalizeSocialProfileField(string? value, int maxBytes)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : ProtocolCodec.TruncateUtf8(value.Trim(), maxBytes);
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
    private sealed record TrackedPingRoundTrip(uint Sequence, long SentAtMilliseconds);
    private sealed record PendingPacket(long ReleaseAtMilliseconds, byte[] Payload);
    private sealed record PendingMessage(long ReleaseAtMilliseconds, IProtocolMessage Message);

    private sealed class PendingLastToDieCommand(
        LastToDieCommandMessage command,
        long lastSentAtMilliseconds)
    {
        public LastToDieCommandMessage Command { get; } = command;

        public long LastSentAtMilliseconds { get; set; } = lastSentAtMilliseconds;
    }
}



