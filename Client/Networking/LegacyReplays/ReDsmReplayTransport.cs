#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public sealed class ReDsmReplayTransport : IPlaybackMessageTransport
{
    private readonly List<ScheduledReplayPayload> _payloads;
    private readonly long _ticksPerSecond;
    private readonly string _remoteDescription;
    private readonly string _completionReason;
    private readonly string _playbackDisplayName;
    private readonly string _playbackServerName;
    private readonly string _playbackMapName;
    private readonly DateTime? _playbackDateUtc;

    private int _nextPayloadIndex;
    private bool _disconnectAvailable;
    private bool _disconnectConsumed;
    private double _accumulatedPlaybackMilliseconds;
    private long _lastPlaybackTimestamp;
    private float _playbackRate = 1f;
    private bool _isPaused;

    private ReDsmReplayTransport(
        string remoteDescription,
        string completionReason,
        string playbackDisplayName,
        string playbackServerName,
        string playbackMapName,
        DateTime? playbackDateUtc,
        List<ScheduledReplayPayload> payloads)
    {
        _remoteDescription = remoteDescription;
        _completionReason = completionReason;
        _playbackDisplayName = playbackDisplayName;
        _playbackServerName = playbackServerName;
        _playbackMapName = playbackMapName;
        _playbackDateUtc = playbackDateUtc;
        _payloads = payloads;
        _ticksPerSecond = Stopwatch.Frequency;
        _lastPlaybackTimestamp = Stopwatch.GetTimestamp();
    }

    public bool HasPendingMessages
    {
        get
        {
            if (_nextPayloadIndex >= _payloads.Count)
            {
                MarkReplayEnded();
                return false;
            }

            return GetCurrentPlaybackMilliseconds() >= _payloads[_nextPayloadIndex].DueMilliseconds;
        }
    }

    public bool IsLoopbackConnection => true;

    public string RemoteDescription => _remoteDescription;

    public bool IsPaused => _isPaused;

    public float PlaybackRate => _playbackRate;

    public int CurrentTick => _nextPayloadIndex <= 0 ? 0 : Math.Max(0, _nextPayloadIndex - 1);

    public int TotalTicks => Math.Max(0, _payloads.Count - 1);

    public string PlaybackDisplayName => _playbackDisplayName;

    public string PlaybackServerName => _playbackServerName;

    public string PlaybackMapName => _playbackMapName;

    public DateTime? PlaybackDateUtc => _playbackDateUtc;

    public bool TryReceive(out byte[] payload)
    {
        payload = [];
        if (_nextPayloadIndex >= _payloads.Count || !HasPendingMessages)
        {
            return false;
        }

        payload = _payloads[_nextPayloadIndex].Payload;
        _nextPayloadIndex += 1;
        if (_nextPayloadIndex >= _payloads.Count)
        {
            MarkReplayEnded();
        }

        return true;
    }

    public bool TryConsumeDisconnectReason(out string reason)
    {
        if (_disconnectAvailable && !_disconnectConsumed)
        {
            _disconnectConsumed = true;
            reason = _completionReason;
            return true;
        }

        reason = string.Empty;
        return false;
    }

    public void Send(byte[] payload)
    {
        // Legacy replay playback is server-authoritative and ignores client sends.
    }

    public void SetPaused(bool paused)
    {
        if (_isPaused == paused)
        {
            return;
        }

        SynchronizePlaybackClock();
        _isPaused = paused;
    }

    public void TogglePaused()
    {
        SetPaused(!_isPaused);
    }

    public void SetPlaybackRate(float playbackRate)
    {
        if (!float.IsFinite(playbackRate))
        {
            throw new ArgumentOutOfRangeException(nameof(playbackRate), "Replay playback rate must be finite.");
        }

        var clampedRate = Math.Clamp(playbackRate, 0.1f, 8f);
        SynchronizePlaybackClock();
        _playbackRate = clampedRate;
    }

    public void Dispose()
    {
    }

    public static bool TryCreate(
        string replayPath,
        out INetworkClientMessageTransport? transport,
        out string error)
    {
        transport = null;
        error = string.Empty;

        try
        {
            var translation = LoadTranslation(replayPath);
            transport = new ReDsmReplayTransport(
                $"replay:{Path.GetFileName(translation.ResolvedReplayPath)}",
                "Replay ended.",
                Path.GetFileName(translation.ResolvedReplayPath),
                translation.Replay.Header.ServerName,
                translation.Replay.Header.MapName,
                File.GetLastWriteTimeUtc(translation.ResolvedReplayPath),
                translation.Timeline.Payloads);
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryTranslateSummary(string replayPath, out string summary, out string error)
    {
        summary = string.Empty;
        error = string.Empty;

        try
        {
            var translation = LoadTranslation(replayPath);
            var snapshotCount = Math.Max(0, translation.Timeline.Payloads.Count - 1);
            summary =
                $"Replay={Path.GetFileName(translation.ResolvedReplayPath)} " +
                $"legacyMap={translation.Replay.Header.MapName} server={translation.Replay.Header.ServerName} " +
                $"tickRate={Math.Max(1, translation.Replay.Header.FramesPerSecond)} translatedPayloads={translation.Timeline.Payloads.Count} translatedSnapshots={snapshotCount}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryVerifyPlaybackSummary(string replayPath, out string summary, out string error)
    {
        summary = string.Empty;
        error = string.Empty;

        try
        {
            var translation = LoadTranslation(replayPath);
            var verification = VerifyPlayback(translation.Timeline);
            summary =
                $"Replay={Path.GetFileName(translation.ResolvedReplayPath)} " +
                $"verifiedMessages={verification.VerifiedMessageCount} appliedSnapshots={verification.AppliedSnapshotCount} " +
                $"lastFrame={verification.LastAppliedFrame} level={verification.LevelName} " +
                $"players={verification.PlayablePlayerCount} alive={verification.AlivePlayablePlayerCount} " +
                $"redCaps={verification.RedCaps} blueCaps={verification.BlueCaps}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryWriteOpenGarrisonDemo(string replayPath, string demoPath, out string summary, out string error)
    {
        summary = string.Empty;
        error = string.Empty;

        try
        {
            var translation = LoadTranslation(replayPath);
            var resolvedDemoPath = Path.GetFullPath(demoPath.Trim().Trim('"'));
            var messages = new List<OpenGarrisonDemoMessage>(translation.Timeline.Payloads.Count);
            for (var index = 0; index < translation.Timeline.Payloads.Count; index += 1)
            {
                var payload = translation.Timeline.Payloads[index];
                messages.Add(new OpenGarrisonDemoMessage(payload.DueMilliseconds, payload.Payload));
            }

            OpenGarrisonDemoFile.Write(
                resolvedDemoPath,
                new OpenGarrisonDemoFile(
                    $"demo:{Path.GetFileNameWithoutExtension(resolvedDemoPath)}",
                    "Demo ended.",
                    messages));
            summary =
                $"Replay={Path.GetFileName(translation.ResolvedReplayPath)} " +
                $"Demo={Path.GetFileName(resolvedDemoPath)} messages={messages.Count}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException)
        {
            error = ex.Message;
            return false;
        }
    }

    private static ReplayTranslationLoadResult LoadTranslation(string replayPath)
    {
        var resolvedReplayPath = ResolveReplayPath(replayPath);
        var replay = ReDsmReplayParser.Parse(resolvedReplayPath);
        var timeline = ReDsmReplayTranslator.Translate(resolvedReplayPath, replay);
        return new ReplayTranslationLoadResult(resolvedReplayPath, replay, timeline);
    }

    private static ReplayPlaybackVerificationResult VerifyPlayback(ReDsmReplayTranslator.ReplayTranslationTimeline timeline)
    {
        SimulationWorld? world = null;
        WelcomeMessage? welcome = null;
        SnapshotMessage? lastSnapshot = null;
        var verifiedMessageCount = 0;
        var appliedSnapshotCount = 0;

        for (var index = 0; index < timeline.Payloads.Count; index += 1)
        {
            var payload = timeline.Payloads[index].Payload;
            if (!ProtocolCodec.TryDeserialize(payload, out var message) || message is null)
            {
                throw new InvalidDataException($"Translated payload {index} could not be deserialized by the current protocol codec.");
            }

            verifiedMessageCount += 1;
            switch (message)
            {
                case WelcomeMessage nextWelcome:
                    if (welcome is not null)
                    {
                        throw new InvalidDataException("Translated replay emitted more than one welcome message.");
                    }

                    if (nextWelcome.Version != ProtocolVersion.Current)
                    {
                        throw new InvalidDataException(
                            $"Translated welcome uses protocol {nextWelcome.Version}, expected {ProtocolVersion.Current}.");
                    }

                    world = new SimulationWorld();
                    if (!world.TryLoadLevel(nextWelcome.LevelName, mapAreaIndex: 1, preservePlayerStats: false, mapScale: nextWelcome.MapScale))
                    {
                        throw new InvalidDataException($"Translated welcome map '{nextWelcome.LevelName}' could not be loaded.");
                    }

                    welcome = nextWelcome;
                    break;
                case SnapshotMessage snapshot:
                    if (welcome is null || world is null)
                    {
                        throw new InvalidDataException("Translated replay emitted a snapshot before the welcome message.");
                    }

                    if (!world.ApplySnapshot(snapshot, welcome.PlayerSlot))
                    {
                        throw new InvalidDataException($"Translated snapshot frame {snapshot.Frame} was rejected by SimulationWorld.");
                    }

                    lastSnapshot = snapshot;
                    appliedSnapshotCount += 1;
                    break;
                default:
                    throw new InvalidDataException(
                        $"Translated payload {index} produced unsupported message type '{message.GetType().Name}'.");
            }
        }

        if (welcome is null || world is null)
        {
            throw new InvalidDataException("Translated replay did not emit a welcome message.");
        }

        if (lastSnapshot is null)
        {
            throw new InvalidDataException("Translated replay did not emit any snapshots.");
        }

        var playablePlayerCount = lastSnapshot.Players.Count(player => !player.IsSpectator);
        var alivePlayablePlayerCount = lastSnapshot.Players.Count(player => !player.IsSpectator && player.IsAlive);
        return new ReplayPlaybackVerificationResult(
            verifiedMessageCount,
            appliedSnapshotCount,
            lastSnapshot.Frame,
            world.Level.Name,
            playablePlayerCount,
            alivePlayablePlayerCount,
            lastSnapshot.RedCaps,
            lastSnapshot.BlueCaps);
    }

    private static string ResolveReplayPath(string replayPath)
    {
        if (string.IsNullOrWhiteSpace(replayPath))
        {
            throw new ArgumentException("Replay path is required.", nameof(replayPath));
        }

        var trimmed = replayPath.Trim().Trim('"');
        var fullPath = Path.GetFullPath(trimmed);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Replay file was not found.", fullPath);
        }

        return fullPath;
    }

    private static long GetDueStopwatchTicks(int dueMilliseconds)
    {
        return dueMilliseconds <= 0
            ? 0L
            : (long)Math.Round((dueMilliseconds / 1000d) * Stopwatch.Frequency, MidpointRounding.AwayFromZero);
    }

    private double GetCurrentPlaybackMilliseconds()
    {
        if (_isPaused)
        {
            return _accumulatedPlaybackMilliseconds;
        }

        var now = Stopwatch.GetTimestamp();
        var elapsedTicks = now - _lastPlaybackTimestamp;
        return _accumulatedPlaybackMilliseconds + ((elapsedTicks * 1000d / _ticksPerSecond) * _playbackRate);
    }

    private void SynchronizePlaybackClock()
    {
        var now = Stopwatch.GetTimestamp();
        if (!_isPaused)
        {
            var elapsedTicks = now - _lastPlaybackTimestamp;
            _accumulatedPlaybackMilliseconds += (elapsedTicks * 1000d / _ticksPerSecond) * _playbackRate;
        }

        _lastPlaybackTimestamp = now;
    }

    private void MarkReplayEnded()
    {
        if (_disconnectAvailable)
        {
            return;
        }

        _disconnectAvailable = true;
    }

    private readonly record struct ReplayTranslationLoadResult(
        string ResolvedReplayPath,
        ReDsmReplayFile Replay,
        ReDsmReplayTranslator.ReplayTranslationTimeline Timeline);

    private readonly record struct ReplayPlaybackVerificationResult(
        int VerifiedMessageCount,
        int AppliedSnapshotCount,
        ulong LastAppliedFrame,
        string LevelName,
        int PlayablePlayerCount,
        int AlivePlayablePlayerCount,
        int RedCaps,
        int BlueCaps);

    private readonly record struct ScheduledReplayPayload(int DueMilliseconds, byte[] Payload);

    private static class ReDsmReplayTranslator
    {
        private const byte LegacyTeamRed = 0;
        private const byte LegacyTeamBlue = 1;
        private const byte LegacyTeamSpectator = 2;
        private const byte SnapshotTeamRed = (byte)PlayerTeam.Red;
        private const byte SnapshotTeamBlue = (byte)PlayerTeam.Blue;
        private const byte LegacyClassScout = 0;
        private const byte LegacyClassSoldier = 1;
        private const byte LegacyClassSniper = 2;
        private const byte LegacyClassDemoman = 3;
        private const byte LegacyClassMedic = 4;
        private const byte LegacyClassEngineer = 5;
        private const byte LegacyClassHeavy = 6;
        private const byte LegacyClassSpy = 7;
        private const byte LegacyClassPyro = 8;
        private const byte LegacyClassQuote = 9;
        private const byte SpectatorSlot = SimulationWorld.FirstSpectatorSlot;
        private const byte LegacyJoinUpdate = 44;
        private const byte LegacyPlayerJoin = 1;
        private const byte LegacyPlayerLeave = 2;
        private const byte LegacyPlayerChangeTeam = 3;
        private const byte LegacyPlayerChangeClass = 4;
        private const byte LegacyPlayerSpawn = 5;
        private const byte LegacyInputState = 6;
        private const byte LegacyChangeMap = 7;
        private const byte LegacyFullUpdate = 8;
        private const byte LegacyQuickUpdate = 9;
        private const byte LegacyPlayerDeath = 10;
        private const byte LegacyBuildSentry = 16;
        private const byte LegacyDestroySentry = 17;
        private const byte LegacyBalance = 18;
        private const byte LegacyChatBubble = 15;
        private const byte LegacyGrabIntel = 19;
        private const byte LegacyScoreIntel = 20;
        private const byte LegacyDropIntel = 21;
        private const byte LegacyUberCharged = 22;
        private const byte LegacyUber = 23;
        private const byte LegacyOmnomnomnom = 24;
        private const byte LegacyCapsUpdate = 28;
        private const byte LegacyArenaWaitForPlayers = 33;
        private const byte LegacyArenaEndRound = 34;
        private const byte LegacyArenaRestart = 35;
        private const byte LegacyUnlockControlPoints = 36;
        private const byte LegacyPlayerChangeName = 31;
        private const byte LegacyArenaStartRound = 40;
        private const byte LegacyToggleZoom = 41;
        private const byte LegacyReturnIntel = 42;
        private const byte LegacyMapEnd = 14;
        private const byte LegacySentryPosition = 46;
        private const byte LegacyRewardUpdate = 47;
        private const byte LegacyMessageString = 53;
        private const byte LegacyWeaponFire = 54;
        private const byte LegacyPluginPacket = 55;
        private const byte LegacyPing = 57;
        private const byte LegacyClientSettings = 58;
        private const byte LegacyKeyDown = 0x02;
        private const byte LegacyKeyRightClick = 0x08;
        private const byte LegacyKeyLeftClick = 0x10;
        private const byte LegacyKeyRight = 0x20;
        private const byte LegacyKeyLeft = 0x40;
        private const byte LegacyKeyJump = 0x80;
        private const int ChatBubbleLifetimeTicks = 60;
        private const int KillFeedLifetimeTicks = 150;
        private const int CombatTraceLifetimeTicks = 2;
        private const int DefaultLegacyTickRate = 30;
        private const int ReplayDefaultGibLevel = 3;
        private const float ReplayMineCollisionHalfExtent = 3f;
        private static readonly Encoding Latin1 = Encoding.Latin1;

            internal static ReplayTranslationTimeline Translate(string replayPath, ReDsmReplayFile replay)
            {
                var session = LegacyReplaySession.Create(replayPath, replay);
                session.ApplyJoinStatePayload(replay.Header.JoinStatePayload);

            var payloads = new List<ScheduledReplayPayload>
            {
                new(0, ProtocolCodec.Serialize(session.CreateWelcomeMessage())),
                new(0, ProtocolCodec.Serialize(session.CreateSnapshotMessage()))
            };

                for (var index = 0; index < replay.Frames.Count; index += 1)
                {
                    try
                    {
                        session.AdvanceLegacyFrame(replay.Frames[index].Payload);
                    }
                    catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException)
                    {
                        throw new InvalidDataException(
                            $"Legacy replay frame {index.ToString(CultureInfo.InvariantCulture)} failed: {ex.Message}",
                            ex);
                    }

                    var snapshot = session.CreateSnapshotMessage();
                    var dueMilliseconds = (int)Math.Round(
                        ((index + 1) * 1000d) / session.TickRate,
                    MidpointRounding.AwayFromZero);
                payloads.Add(new ScheduledReplayPayload(dueMilliseconds, ProtocolCodec.Serialize(snapshot)));
            }

            return new ReplayTranslationTimeline(payloads);
        }

        private sealed class LegacyReplaySession
        {
            private readonly string _replayPath;
            private readonly SimpleLevel _level;
            private readonly List<LegacyReplayPlayer> _players = new();
            private readonly Dictionary<int, float> _lastFacingByPlayerId = new();
            private readonly List<LegacyReplayKillFeedEntry> _killFeed = new();
            private readonly List<LegacyReplayCombatTrace> _combatTraces = new();
            private readonly List<LegacyReplayProjectileState> _shots = new();
            private readonly List<LegacyReplayProjectileState> _bubbles = new();
            private readonly List<LegacyReplayProjectileState> _blades = new();
            private readonly List<LegacyReplayProjectileState> _needles = new();
            private readonly List<LegacyReplayProjectileState> _revolverShots = new();
            private readonly List<LegacyReplayRocketState> _rockets = new();
            private readonly List<LegacyReplayFlameState> _flames = new();
            private readonly List<PlayerGibEntity> _playerGibs = new();
            private readonly List<DeadBodyEntity> _deadBodies = new();
            private readonly List<BloodDropEntity> _bloodDrops = new();
            private readonly List<SnapshotVisualEvent> _pendingVisualEvents = new();
            private readonly List<SnapshotSoundEvent> _pendingSoundEvents = new();
            private int _expectedJoinPlayerCount = -1;

            private LegacyReplaySession(
                string replayPath,
                ReDsmReplayFile replay,
                OpenGarrisonStockMapDefinition stockMap)
            {
                _replayPath = replayPath;
                ReplayHeader = replay.Header;
                StockMap = stockMap;
                TickRate = replay.Header.FramesPerSecond > 0 ? replay.Header.FramesPerSecond : DefaultLegacyTickRate;
                LegacyMapName = replay.Header.MapName;
                LevelName = stockMap.LevelName;
                GameMode = stockMap.Mode;
                _level = SimpleLevelFactory.CreateImportedLevel(stockMap.LevelName)
                    ?? throw new NotSupportedException(
                        $"Replay map '{stockMap.LevelName}' could not be imported into a stock collision level.");
                InitializeLegacyObjectiveState();
            }

            public ReDsmReplayHeader ReplayHeader { get; }
            public OpenGarrisonStockMapDefinition StockMap { get; }
            public int TickRate { get; }
            public string LegacyMapName { get; private set; } = string.Empty;
            public string LevelName { get; private set; } = string.Empty;
            public GameModeKind GameMode { get; }
            public byte MapAreaIndex { get; private set; } = 1;
            public byte MapAreaCount { get; private set; } = 1;
            public int TimeLimitTicks { get; private set; }
            public int TimeRemainingTicks { get; private set; }
            public int RedCaps { get; private set; }
            public int BlueCaps { get; private set; }
            public int RespawnSeconds { get; private set; }
            public int ControlPointSetupTicksRemaining { get; private set; }
            public int KothUnlockTicksRemaining { get; private set; }
            public int KothRedTimerTicksRemaining { get; private set; }
            public int KothBlueTimerTicksRemaining { get; private set; }
            public ulong SnapshotFrame { get; private set; } = 1;
            public byte WinnerTeam { get; private set; }
            public MatchPhase MatchPhase { get; private set; } = MatchPhase.Running;
            public LegacyReplayIntelState RedIntel { get; } = new(SnapshotTeamRed);
            public LegacyReplayIntelState BlueIntel { get; } = new(SnapshotTeamBlue);
            private readonly List<LegacyReplayControlPointState> _controlPoints = [];
            private int _arenaUnlockTicksRemaining;
            private byte _arenaPointTeamLegacy = byte.MaxValue;
            private byte _arenaCappingTeamLegacy = byte.MaxValue;
            private ushort _arenaCappingTicks;
            private byte _arenaCappers;
            private int _nextStablePlayerId = 1;
            private int _nextTransientEntityId = 1_000_000;
            private ulong _nextReplayEventId = 1;

            public static LegacyReplaySession Create(string replayPath, ReDsmReplayFile replay)
            {
                if (!OpenGarrisonStockMapCatalog.TryGetDefinition(replay.Header.MapName, out var stockMap))
                {
                    throw new NotSupportedException(
                        $"Replay map '{replay.Header.MapName}' is not currently supported. " +
                        "Only stock map replays are supported in this first legacy playback pass.");
                }

                return new LegacyReplaySession(replayPath, replay, stockMap);
            }

            public WelcomeMessage CreateWelcomeMessage()
            {
                return new WelcomeMessage(
                    ReplayHeader.ServerName,
                    ProtocolVersion.Current,
                    TickRate,
                    LevelName,
                    SpectatorSlot,
                    Math.Max(16, _players.Count),
                    IsCustomMap: false,
                    MapDownloadUrl: string.Empty,
                    MapContentHash: string.Empty,
                    MapScale: 1f);
            }

            public SnapshotMessage CreateSnapshotMessage()
            {
                var players = new List<SnapshotPlayerState>(_players.Count);
                var spectatorCount = 0;

                UpdateDerivedIntelState();

                for (var index = 0; index < _players.Count; index += 1)
                {
                    var player = _players[index];
                    var playerSnapshot = CreateSnapshotPlayerState(player, index);
                    players.Add(playerSnapshot);
                    if (playerSnapshot.IsSpectator)
                    {
                        spectatorCount += 1;
                    }
                }

                var snapshot = new SnapshotMessage(
                    Frame: SnapshotFrame++,
                    TickRate: TickRate,
                    LevelName: LevelName,
                    MapAreaIndex: MapAreaIndex,
                    MapAreaCount: MapAreaCount,
                    GameMode: (byte)GameMode,
                    MatchPhase: (byte)MatchPhase,
                    WinnerTeam: WinnerTeam,
                    TimeRemainingTicks: Math.Max(0, TimeRemainingTicks),
                    RedCaps: RedCaps,
                    BlueCaps: BlueCaps,
                    SpectatorCount: spectatorCount,
                    LastProcessedInputSequence: 0,
                    RedIntel: RedIntel.ToSnapshotState(),
                    BlueIntel: BlueIntel.ToSnapshotState(),
                    Players: players,
                    CombatTraces: CreateSnapshotCombatTraces(),
                    SniperAimIndicators: Array.Empty<SnapshotSniperAimIndicatorState>(),
                    Sentries: CreateSnapshotSentries(),
                    Shots: CreateSnapshotProjectiles(_shots),
                    Bubbles: CreateSnapshotProjectiles(_bubbles),
                    Blades: CreateSnapshotProjectiles(_blades),
                    Needles: CreateSnapshotProjectiles(_needles),
                    RevolverShots: CreateSnapshotProjectiles(_revolverShots),
                    Rockets: CreateSnapshotRockets(),
                    Flames: CreateSnapshotFlames(),
                    Flares: Array.Empty<SnapshotShotState>(),
                    Mines: CreateSnapshotMines(),
                    DeadBodies: CreateSnapshotDeadBodies(),
                    ControlPointSetupTicksRemaining: Math.Max(0, ControlPointSetupTicksRemaining),
                    KothUnlockTicksRemaining: Math.Max(0, KothUnlockTicksRemaining),
                    KothRedTimerTicksRemaining: Math.Max(0, KothRedTimerTicksRemaining),
                    KothBlueTimerTicksRemaining: Math.Max(0, KothBlueTimerTicksRemaining),
                    ControlPoints: CreateSnapshotControlPoints(),
                    Generators: Array.Empty<SnapshotGeneratorState>(),
                    LocalDeathCam: null,
                    KillFeed: CreateSnapshotKillFeed(),
                    VisualEvents: CreateSnapshotVisualEvents(),
                    DamageEvents: Array.Empty<SnapshotDamageEvent>(),
                    SoundEvents: CreateSnapshotSoundEvents())
                {
                    TimeLimitTicks = Math.Max(0, TimeLimitTicks),
                    ArenaUnlockTicksRemaining = Math.Max(0, _arenaUnlockTicksRemaining),
                    ArenaPointTeam = ToSnapshotObjectiveTeam(_arenaPointTeamLegacy),
                    ArenaCappingTeam = ToSnapshotObjectiveTeam(_arenaCappingTeamLegacy),
                    ArenaCappingTicks = _arenaCappingTicks,
                    ArenaCappers = _arenaCappers,
                    ArenaRedConsecutiveWins = GameMode == GameModeKind.Arena ? RedCaps : 0,
                    ArenaBlueConsecutiveWins = GameMode == GameModeKind.Arena ? BlueCaps : 0,
                    IsDelta = false,
                    BaselineFrame = 0,
                    PlayerGibs = CreateSnapshotPlayerGibs()
                };

                _pendingVisualEvents.Clear();
                _pendingSoundEvents.Clear();

                return snapshot;
            }

            public void ApplyJoinStatePayload(byte[] payload)
            {
                using var stream = new MemoryStream(payload, writable: false);
                using var reader = new BinaryReader(stream, Latin1, leaveOpen: true);

                ReadExpectedJoinMessageType(reader, LegacyJoinUpdate, "JOIN_UPDATE");
                ReadJoinUpdate(reader);

                ReadExpectedJoinMessageType(reader, LegacyChangeMap, "CHANGE_MAP");
                ReadChangeMap(reader);

                if (_expectedJoinPlayerCount < 0)
                {
                    throw new InvalidDataException("Legacy replay join update did not provide a valid player count.");
                }

                for (var index = 0; index < _expectedJoinPlayerCount; index += 1)
                {
                    ReadExpectedJoinMessageType(reader, LegacyPlayerJoin, "PLAYER_JOIN");
                    ReadPlayerJoin(reader);

                    ReadExpectedJoinMessageType(reader, LegacyPlayerChangeClass, "PLAYER_CHANGECLASS");
                    ReadPlayerChangeClass(reader);

                    ReadExpectedJoinMessageType(reader, LegacyPlayerChangeTeam, "PLAYER_CHANGETEAM");
                    ReadPlayerChangeTeam(reader);
                }

                ReadExpectedJoinMessageType(reader, LegacyFullUpdate, "FULL_UPDATE");
                ReadStateUpdate(reader, fullUpdate: true);
            }

            public void AdvanceLegacyFrame(byte[] payload)
            {
                TickTransientState();

                using var stream = new MemoryStream(payload, writable: false);
                using var reader = new BinaryReader(stream, Latin1, leaveOpen: true);

                while (stream.Position < stream.Length)
                {
                    ApplyLegacyMessage(reader, allowQuickState: true);
                }
            }

            private void ApplyLegacyMessage(BinaryReader reader, bool allowQuickState)
            {
                var messageType = reader.ReadByte();
                var messageStart = reader.BaseStream.Position - 1;
                ApplyLegacyMessagePayload(reader, messageType, allowQuickState, messageStart);
            }

            private void ApplyLegacyMessagePayload(BinaryReader reader, byte messageType, bool allowQuickState, long messageStart)
            {
                try
                {
                    switch (messageType)
                    {
                        case LegacyJoinUpdate:
                            ReadJoinUpdate(reader);
                            break;
                        case LegacyChangeMap:
                            ReadChangeMap(reader);
                            break;
                        case LegacyPlayerJoin:
                            ReadPlayerJoin(reader);
                            break;
                        case LegacyPlayerLeave:
                            ReadPlayerLeave(reader);
                            break;
                        case LegacyPlayerChangeTeam:
                            ReadPlayerChangeTeam(reader);
                            break;
                        case LegacyPlayerChangeClass:
                            ReadPlayerChangeClass(reader);
                            break;
                        case LegacyPlayerChangeName:
                            ReadPlayerChangeName(reader);
                            break;
                        case LegacyFullUpdate:
                            ReadStateUpdate(reader, fullUpdate: true);
                            break;
                        case LegacyQuickUpdate:
                            if (!allowQuickState)
                            {
                                throw new InvalidDataException("Unexpected quick update inside replay join-state payload.");
                            }
                            ReadStateUpdate(reader, fullUpdate: false);
                            break;
                        case LegacyInputState:
                            if (!allowQuickState)
                            {
                                throw new InvalidDataException("Unexpected input update inside replay join-state payload.");
                            }
                            ReadInputState(reader);
                            break;
                        case LegacyCapsUpdate:
                            ReadCapsUpdate(reader);
                            break;
                        case LegacyArenaWaitForPlayers:
                            ReadArenaWaitForPlayers();
                            break;
                        case LegacyArenaEndRound:
                            ReadArenaEndRound(reader);
                            break;
                        case LegacyArenaRestart:
                            ReadArenaRestart();
                            break;
                        case LegacyUnlockControlPoints:
                            ReadUnlockControlPoints();
                            break;
                        case LegacyPlayerDeath:
                            ReadPlayerDeath(reader);
                            break;
                        case LegacyBuildSentry:
                            ReadBuildSentry(reader);
                            break;
                        case LegacyDestroySentry:
                            ReadDestroySentry(reader);
                            break;
                        case LegacyBalance:
                            ReadBalance(reader);
                            break;
                        case LegacyChatBubble:
                            ReadChatBubble(reader);
                            break;
                        case LegacyGrabIntel:
                            ReadGrabIntel(reader);
                            break;
                        case LegacyDropIntel:
                            ReadDropIntel(reader);
                            break;
                        case LegacyScoreIntel:
                            ReadScoreIntel(reader);
                            break;
                        case LegacyUberCharged:
                            ReadUberCharged(reader);
                            break;
                        case LegacyUber:
                            ReadUber(reader);
                            break;
                        case LegacyOmnomnomnom:
                            ReadOmnomnomnom(reader);
                            break;
                        case LegacyReturnIntel:
                            ReadReturnIntel(reader);
                            break;
                        case LegacyToggleZoom:
                            ReadToggleZoom(reader);
                            break;
                        case LegacyArenaStartRound:
                            ReadArenaStartRound();
                            break;
                        case LegacyPlayerSpawn:
                            ReadPlayerSpawn(reader);
                            break;
                        case LegacyMapEnd:
                            ReadMapEnd(reader);
                            break;
                        case LegacySentryPosition:
                            ReadSentryPosition(reader);
                            break;
                        case LegacyRewardUpdate:
                            ReadRewardUpdate(reader);
                            break;
                        case LegacyMessageString:
                            ReadMessageString(reader);
                            break;
                        case LegacyWeaponFire:
                            ReadWeaponFire(reader);
                            break;
                        case LegacyPluginPacket:
                            ReadPluginPacket(reader);
                            break;
                        case LegacyPing:
                            break;
                        case LegacyClientSettings:
                            ReadClientSettings(reader);
                            break;
                        default:
                            throw new NotSupportedException(
                                $"Legacy replay playback does not yet support packet 0x{messageType:X2} in '{Path.GetFileName(_replayPath)}'.");
                    }
                }
                catch (Exception ex) when (ex is EndOfStreamException or IOException or InvalidDataException or NotSupportedException)
                {
                    throw new InvalidDataException(
                        $"Failed while decoding legacy packet 0x{messageType:X2} at stream offset {messageStart.ToString(CultureInfo.InvariantCulture)} in '{Path.GetFileName(_replayPath)}': {ex.Message}",
                        ex);
                }
            }

            private void ReadJoinUpdate(BinaryReader reader)
            {
                var playerCount = reader.ReadByte();
                _expectedJoinPlayerCount = playerCount;
                MapAreaIndex = reader.ReadByte();
                MapAreaCount = Math.Max(MapAreaCount, MapAreaIndex);
                if (playerCount < _players.Count)
                {
                    throw new InvalidDataException("Replay join update reported fewer players than already parsed.");
                }
            }

            private void ReadChangeMap(BinaryReader reader)
            {
                var mapName = ReadByteLengthPrefixedString(reader);
                var mapHash = ReadByteLengthPrefixedString(reader);
                LegacyMapName = mapName;
                if (!string.Equals(mapName, ReplayHeader.MapName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new NotSupportedException(
                        $"Replay payload switched to legacy map '{mapName}', which does not match the replay header map '{ReplayHeader.MapName}'.");
                }

                if (!string.IsNullOrEmpty(mapHash) && !string.IsNullOrEmpty(ReplayHeader.MapContentHash) &&
                    !string.Equals(mapHash, ReplayHeader.MapContentHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("Replay map hash does not match the replay header map hash.");
                }
            }

            private void ReadPlayerJoin(BinaryReader reader)
            {
                var name = ReadByteLengthPrefixedString(reader);
                _players.Add(new LegacyReplayPlayer(_nextStablePlayerId++, name));
            }

            private void ReadPlayerLeave(BinaryReader reader)
            {
                RemovePlayerAtIndex(reader.ReadByte());
            }

            private void ReadPlayerChangeTeam(BinaryReader reader)
            {
                var player = GetPlayerAtIndex(reader.ReadByte());
                player.Team = reader.ReadByte();
                player.HasCharacter = false;
                player.HasSentry = false;
                player.Intel = false;
                player.IsScoped = false;
                player.AirJumpsUsed = 0;
                player.WasJumpHeldLastTick = false;
            }

            private void ReadPlayerChangeClass(BinaryReader reader)
            {
                var player = GetPlayerAtIndex(reader.ReadByte());
                player.ClassId = reader.ReadByte();
                player.HasCharacter = false;
                player.HasSentry = false;
                player.IsScoped = false;
                player.AirJumpsUsed = 0;
                player.WasJumpHeldLastTick = false;
            }

            private void ReadPlayerChangeName(BinaryReader reader)
            {
                var player = GetPlayerAtIndex(reader.ReadByte());
                player.Name = ReadByteLengthPrefixedString(reader);
            }

            private void ReadPlayerSpawn(BinaryReader reader)
            {
                var player = GetPlayerAtIndex(reader.ReadByte());
                reader.ReadByte();
                reader.ReadByte();
                player.HasCharacter = true;
                player.AirJumpsUsed = 0;
                player.WasJumpHeldLastTick = false;
            }

            private void ReadPlayerDeath(BinaryReader reader)
            {
                var victim = GetPlayerAtIndex(reader.ReadByte());
                var killerIndex = reader.ReadByte();
                var assistantIndex = reader.ReadByte();
                var deathSource = reader.ReadByte();

                if (victim.HasCharacter && victim.Team is LegacyTeamRed or LegacyTeamBlue)
                {
                    SpawnReplayDeathRemains(victim, killerIndex, deathSource);
                }

                victim.HasCharacter = false;
                victim.Intel = false;
                victim.ChatBubbleTicksRemaining = 0;
                victim.IsScoped = false;
                victim.AirJumpsUsed = 0;
                victim.WasJumpHeldLastTick = false;
                victim.Deaths = (short)Math.Min(short.MaxValue, victim.Deaths + 1);

                if (killerIndex != byte.MaxValue && killerIndex < _players.Count)
                {
                    var killer = GetPlayerAtIndex(killerIndex);
                    killer.Kills = (short)Math.Min(short.MaxValue, killer.Kills + 1);
                    killer.Points += 1f;
                }

                if (assistantIndex != byte.MaxValue && assistantIndex < _players.Count)
                {
                    var assistant = GetPlayerAtIndex(assistantIndex);
                    assistant.Assists = (short)Math.Min(short.MaxValue, assistant.Assists + 1);
                    assistant.Points += 0.5f;
                }

                RecordKillFeed(victim, killerIndex != byte.MaxValue && killerIndex < _players.Count
                    ? GetPlayerAtIndex(killerIndex)
                    : null);
            }

            private void ReadBuildSentry(BinaryReader reader)
            {
                var player = GetPlayerAtIndex(reader.ReadByte());
                player.HasSentry = true;
                player.SentryBuilt = true;
                player.SentryX = reader.ReadUInt16() / 5f;
                player.SentryY = reader.ReadUInt16() / 5f;
                player.SentryFacingDirectionX = Math.Sign(reader.ReadSByte());
                if (player.SentryFacingDirectionX == 0f)
                {
                    player.SentryFacingDirectionX = 1f;
                }

                player.SentryBuilt = false;
                player.SentryHealth = 127;
                player.SentryVerticalSpeed = 0f;
                QueueSoundEvent("BuildingSentrySnd", player.SentryX, player.SentryY);
            }

            private void ReadDestroySentry(BinaryReader reader)
            {
                var owner = GetPlayerAtIndex(reader.ReadByte());
                var killerIndex = reader.ReadByte();
                var assistantIndex = reader.ReadByte();
                var damageSource = reader.ReadByte();

                owner.HasSentry = false;
                owner.SentryBuilt = false;
                owner.SentryHealth = 0;
                QueueSoundEvent("DestroyBuildingSnd", owner.SentryX, owner.SentryY);
                QueueVisualEvent("Explosion", owner.SentryX, owner.SentryY, 0f, 1);

                if (killerIndex != byte.MaxValue && killerIndex < _players.Count)
                {
                    var killer = GetPlayerAtIndex(killerIndex);
                    killer.Points += 1f;
                }

                if (assistantIndex != byte.MaxValue && assistantIndex < _players.Count)
                {
                    var assistant = GetPlayerAtIndex(assistantIndex);
                    assistant.Assists = (short)Math.Min(short.MaxValue, assistant.Assists + 1);
                    assistant.Points += 0.5f;
                }

                _ = damageSource;
            }

            private void ReadBalance(BinaryReader reader)
            {
                var playerIndex = reader.ReadByte();
                if (playerIndex == byte.MaxValue)
                {
                    return;
                }

                var player = GetPlayerAtIndex(playerIndex);
                player.ClassId = reader.ReadByte();
                player.Team = player.Team == LegacyTeamRed ? LegacyTeamBlue : LegacyTeamRed;
                player.HasCharacter = false;
                player.HasSentry = false;
                player.Intel = false;
                player.IsScoped = false;
                player.AirJumpsUsed = 0;
                player.WasJumpHeldLastTick = false;
            }

            private void ReadChatBubble(BinaryReader reader)
            {
                var player = GetPlayerAtIndex(reader.ReadByte());
                player.ChatBubbleFrameIndex = reader.ReadByte();
                player.ChatBubbleTicksRemaining = ChatBubbleLifetimeTicks;
            }

            private void ReadGrabIntel(BinaryReader reader)
            {
                var player = GetPlayerAtIndex(reader.ReadByte());
                player.Intel = true;
                if (player.Team == LegacyTeamRed)
                {
                    BlueIntel.MarkCarried(player);
                }
                else if (player.Team == LegacyTeamBlue)
                {
                    RedIntel.MarkCarried(player);
                }

                QueueSoundEvent("IntelGetSnd", player.X, player.Y);
                RecordIntelPickedUpObjectiveLog(player);
            }

            private void ReadDropIntel(BinaryReader reader)
            {
                var player = GetPlayerAtIndex(reader.ReadByte());
                if (!player.Intel)
                {
                    return;
                }

                player.Intel = false;
                if (player.Team == LegacyTeamRed)
                {
                    BlueIntel.MarkDropped(player.X, player.Y, player.IntelRechargeTicks);
                }
                else if (player.Team == LegacyTeamBlue)
                {
                    RedIntel.MarkDropped(player.X, player.Y, player.IntelRechargeTicks);
                }

                QueueSoundEvent("IntelDropSnd", player.X, player.Y);
                RecordIntelDroppedObjectiveLog(player);
            }

            private void ReadScoreIntel(BinaryReader reader)
            {
                var player = GetPlayerAtIndex(reader.ReadByte());
                player.Intel = false;
                player.Caps = (short)Math.Min(short.MaxValue, player.Caps + 1);
                player.Points += 2f;

                if (player.Team == LegacyTeamRed)
                {
                    RedCaps += 1;
                    BlueIntel.MarkReturnedToBase();
                }
                else if (player.Team == LegacyTeamBlue)
                {
                    BlueCaps += 1;
                    RedIntel.MarkReturnedToBase();
                }

                QueueSoundEvent("IntelPutSnd", player.X, player.Y);
                RecordIntelCapturedObjectiveLog(player);
            }

            private void ReadReturnIntel(BinaryReader reader)
            {
                var team = reader.ReadByte();
                if (team == LegacyTeamRed)
                {
                    RedIntel.MarkReturnedToBase();
                }
                else if (team == LegacyTeamBlue)
                {
                    BlueIntel.MarkReturnedToBase();
                }

                var intel = team == LegacyTeamRed ? RedIntel : BlueIntel;
                QueueSoundEvent("IntelDropSnd", intel.X, intel.Y);
                RecordIntelReturnedObjectiveLog((PlayerTeam)ToSnapshotTeam(team));
            }

            private void ReadUberCharged(BinaryReader reader)
            {
                var player = GetPlayerAtIndex(reader.ReadByte());
                player.ChatBubbleFrameIndex = 46;
                player.ChatBubbleTicksRemaining = ChatBubbleLifetimeTicks;
                QueueSoundEvent("UberChargedSnd", player.X, player.Y);
            }

            private void ReadUber(BinaryReader reader)
            {
                var player = GetPlayerAtIndex(reader.ReadByte());
                QueueSoundEvent("UberStartSnd", player.X, player.Y);
            }

            private void ReadOmnomnomnom(BinaryReader reader)
            {
                var player = GetPlayerAtIndex(reader.ReadByte());
                QueueSoundEvent("HeavyEatSnd", player.X, player.Y);
            }

            private void ReadToggleZoom(BinaryReader reader)
            {
                var player = GetPlayerAtIndex(reader.ReadByte());
                player.IsScoped = !player.IsScoped;
            }

            private void ReadMapEnd(BinaryReader reader)
            {
                ReadByteLengthPrefixedString(reader);
                WinnerTeam = ToSnapshotTeam(reader.ReadByte());
                MapAreaIndex = reader.ReadByte();
                MapAreaCount = Math.Max(MapAreaCount, MapAreaIndex);
                MatchPhase = MatchPhase.Ended;
            }

            private void ReadArenaWaitForPlayers()
            {
                ApplyArenaRoundReset(arenaUnlockTicksRemaining: TickRate * 60);
            }

            private void ReadArenaStartRound()
            {
                ApplyArenaRoundReset(arenaUnlockTicksRemaining: TickRate * 60);
            }

            private void ReadArenaRestart()
            {
                ApplyArenaRoundReset(arenaUnlockTicksRemaining: TickRate * 60);
            }

            private void ReadArenaEndRound(BinaryReader reader)
            {
                WinnerTeam = ToSnapshotTeam(reader.ReadByte());
                var redMvpCount = reader.ReadByte();
                var blueMvpCount = reader.ReadByte();
                RedCaps = reader.ReadByte();
                BlueCaps = reader.ReadByte();
                SkipBytes(reader, redMvpCount * 5);
                SkipBytes(reader, blueMvpCount * 5);
                MatchPhase = MatchPhase.Ended;
                _arenaCappingTeamLegacy = byte.MaxValue;
                _arenaCappingTicks = 0;
                _arenaCappers = 0;
            }

            private void ReadUnlockControlPoints()
            {
                if (GameMode == GameModeKind.Arena)
                {
                    _arenaUnlockTicksRemaining = 0;
                    return;
                }

                if (GameMode is GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill)
                {
                    KothUnlockTicksRemaining = 0;
                    for (var index = 0; index < _controlPoints.Count; index += 1)
                    {
                        var point = _controlPoints[index];
                        _controlPoints[index] = point with { IsLocked = false };
                    }

                    return;
                }

                if (GameMode == GameModeKind.ControlPoint)
                {
                    ControlPointSetupTicksRemaining = 0;
                    for (var index = 0; index < _controlPoints.Count; index += 1)
                    {
                        var point = _controlPoints[index];
                        _controlPoints[index] = point with { IsLocked = false };
                    }
                }
            }

            private void ReadSentryPosition(BinaryReader reader)
            {
                var player = GetPlayerAtIndex(reader.ReadByte());
                player.SentryX = reader.ReadUInt16() / 5f;
                player.SentryY = reader.ReadUInt16() / 5f;
                player.SentryVerticalSpeed = 0f;
            }

            private void ReadRewardUpdate(BinaryReader reader)
            {
                GetPlayerAtIndex(reader.ReadByte());
                var rewardLength = reader.ReadUInt16();
                SkipBytes(reader, rewardLength);
            }

            private void ReadMessageString(BinaryReader reader)
            {
                var message = ReadByteLengthPrefixedString(reader).Trim();
                if (string.IsNullOrWhiteSpace(message))
                {
                    return;
                }

                RecordSystemMessage(message);
            }

            private void ReadWeaponFire(BinaryReader reader)
            {
                var player = GetPlayerAtIndex(reader.ReadByte());
                player.X = reader.ReadUInt16() / 5f;
                player.Y = reader.ReadUInt16() / 5f;
                player.HorizontalSpeed = reader.ReadSByte() / 5f;
                player.VerticalSpeed = reader.ReadSByte() / 5f;
                player.AimDirectionDegrees = reader.ReadUInt16() * 360f / 65536f;
                SpawnWeaponPresentation(player);
            }

            private static void ReadPluginPacket(BinaryReader reader)
            {
                var packetLength = reader.ReadUInt16();
                SkipBytes(reader, packetLength);
            }

            private void ReadClientSettings(BinaryReader reader)
            {
                var player = GetPlayerAtIndex(reader.ReadByte());
                player.QueueJump = reader.ReadByte() != 0;
            }

            private void ReadCapsUpdate(BinaryReader reader)
            {
                var playerCount = reader.ReadByte();
                EnsurePlayerCount(playerCount);
                RedCaps = reader.ReadByte();
                BlueCaps = reader.ReadByte();
                RespawnSeconds = reader.ReadByte();
                ReadModeSpecificScoreState(reader);
            }

            private void ReadInputState(BinaryReader reader)
            {
                var playerCount = reader.ReadByte();
                EnsurePlayerCount(playerCount);

                for (var index = 0; index < playerCount; index += 1)
                {
                    var player = _players[index];
                    ReadPlayerState(reader, player, fullUpdate: false, includeQuickState: false, playerCount);
                }

                AdvancePlayersFromInputTick();
            }

            private void ReadStateUpdate(BinaryReader reader, bool fullUpdate)
            {
                if (fullUpdate)
                {
                    reader.ReadUInt16();
                }

                var playerCount = reader.ReadByte();
                EnsurePlayerCount(playerCount);

                for (var index = 0; index < playerCount; index += 1)
                {
                    var player = _players[index];
                    ReadPlayerState(reader, player, fullUpdate, includeQuickState: true, playerCount);
                }

                // The first legacy playback pass supports stock CTF maps and assumes zero moving platforms,
                // which holds for the verified classicwell sample used to prove the end-to-end path.
                if (fullUpdate)
                {
                    ReadFullUpdateTail(reader);
                }
            }

            private void ReadFullUpdateTail(BinaryReader reader)
            {
                if (GameMode == GameModeKind.CaptureTheFlag)
                {
                    ReadIntelSet(reader, RedIntel);
                    ReadIntelSet(reader, BlueIntel);
                    if (reader.BaseStream.Position >= reader.BaseStream.Length)
                    {
                        return;
                    }

                    // Full updates prefix the same scoreboard/objective trailer with one additional unused byte.
                    reader.ReadByte();
                    if (reader.BaseStream.Position + 3 > reader.BaseStream.Length)
                    {
                        SkipBytes(reader, checked((int)(reader.BaseStream.Length - reader.BaseStream.Position)));
                        return;
                    }

                    RedCaps = reader.ReadByte();
                    BlueCaps = reader.ReadByte();
                    RespawnSeconds = reader.ReadByte();
                    ReadModeSpecificScoreState(reader);
                    if (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        SkipBytes(reader, checked((int)(reader.BaseStream.Length - reader.BaseStream.Position)));
                    }

                    return;
                }

                // Legacy non-CTF full updates are still mode-specific and not cleanly documented.
                // We keep player state from the packet itself, then let the following mode heartbeat
                // packets (for example CAPS_UPDATE) authoritatively populate timers/objectives.
                if (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    SkipBytes(reader, checked((int)(reader.BaseStream.Length - reader.BaseStream.Position)));
                }
            }

            private void ReadModeSpecificScoreState(BinaryReader reader)
            {
                switch (GameMode)
                {
                    case GameModeKind.CaptureTheFlag:
                        ReadHudState(reader);
                        break;
                    case GameModeKind.TeamDeathmatch:
                        ReadTeamDeathmatchStatePayload(reader);
                        break;
                    case GameModeKind.ControlPoint:
                        ReadHudState(reader);
                        ReadControlPointStatePayload(reader);
                        break;
                    case GameModeKind.KingOfTheHill:
                        ReadKothStatePayload(reader, pointCount: 1);
                        break;
                    case GameModeKind.DoubleKingOfTheHill:
                        ReadKothStatePayload(reader, pointCount: 2);
                        break;
                    case GameModeKind.Arena:
                        ReadArenaStatePayload(reader);
                        break;
                }
            }

            private void ReadHudState(BinaryReader reader)
            {
                TimeLimitTicks = reader.ReadByte() * TickRate * 60;
                TimeRemainingTicks = checked((int)reader.ReadUInt32());
            }

            private void ReadControlPointStatePayload(BinaryReader reader)
            {
                if (_controlPoints.Count == 0)
                {
                    return;
                }

                ControlPointSetupTicksRemaining = reader.ReadUInt16();
                for (var index = 0; index < _controlPoints.Count; index += 1)
                {
                    var point = _controlPoints[index];
                    ReadObjectivePointState(reader, ref point, isLocked: ControlPointSetupTicksRemaining > 0);
                    _controlPoints[index] = point;
                }
            }

            private void ReadKothStatePayload(BinaryReader reader, int pointCount)
            {
                KothUnlockTicksRemaining = reader.ReadUInt16();
                KothRedTimerTicksRemaining = reader.ReadUInt16();
                KothBlueTimerTicksRemaining = reader.ReadUInt16();

                for (var index = 0; index < Math.Min(pointCount, _controlPoints.Count); index += 1)
                {
                    var point = _controlPoints[index];
                    ReadObjectivePointState(reader, ref point, isLocked: KothUnlockTicksRemaining > 0);
                    _controlPoints[index] = point;
                }
            }

            private void ReadTeamDeathmatchStatePayload(BinaryReader reader)
            {
                ReadHudState(reader);
                reader.ReadUInt16();
            }

            private void ReadArenaStatePayload(BinaryReader reader)
            {
                TimeLimitTicks = reader.ReadByte() * TickRate * 60;
                TimeRemainingTicks = checked((int)reader.ReadUInt32());
                _arenaUnlockTicksRemaining = reader.ReadUInt16();
                reader.ReadByte();
                _arenaPointTeamLegacy = reader.ReadByte();
                _arenaCappingTeamLegacy = reader.ReadByte();
                _arenaCappingTicks = reader.ReadUInt16();
                _arenaCappers = 0;
            }

            private static void ReadObjectivePointState(BinaryReader reader, ref LegacyReplayControlPointState point, bool isLocked)
            {
                point = point with
                {
                    TeamLegacy = reader.ReadByte(),
                    CappingTeamLegacy = reader.ReadByte(),
                    CappingTicks = reader.ReadUInt16(),
                    IsLocked = isLocked
                };
            }

            private static void ReadIntelSet(BinaryReader reader, LegacyReplayIntelState intel)
            {
                var instanceCount = reader.ReadUInt16();
                if (instanceCount > 1)
                {
                    throw new InvalidDataException("Legacy replay contained more than one intel instance for a team.");
                }

                if (instanceCount == 0)
                {
                    intel.MarkMissing();
                    return;
                }

                var x = reader.ReadUInt16() / 5f;
                var y = reader.ReadUInt16() / 5f;
                var returnTicksRemaining = reader.ReadInt16();
                intel.UpdateFieldState(x, y, returnTicksRemaining);
            }

            private void ReadPlayerState(
                BinaryReader reader,
                LegacyReplayPlayer player,
                bool fullUpdate,
                bool includeQuickState,
                int playerCount)
            {
                if (fullUpdate)
                {
                    player.Kills = reader.ReadByte();
                    player.Deaths = reader.ReadByte();
                    player.Caps = reader.ReadByte();
                    player.Assists = reader.ReadByte();
                    reader.ReadByte();
                    reader.ReadByte();
                    player.HealPoints = (short)reader.ReadUInt16();
                    reader.ReadByte();
                    reader.ReadByte();
                    reader.ReadByte();
                    player.Points = reader.ReadByte();
                    player.QueueJump = reader.ReadByte() != 0;
                    var rewardLength = reader.ReadUInt16();
                    SkipBytes(reader, rewardLength);
                    SkipBytes(reader, Math.Max(0, playerCount - 1));
                }

                var subobjects = reader.ReadByte();
                var hasCharacter = (subobjects & 0x01) != 0;
                var hasSentry = (subobjects & 0x02) != 0;
                player.HasCharacter = hasCharacter;
                player.HasSentry = hasSentry;

                if (hasCharacter)
                {
                    player.KeyState = reader.ReadByte();
                    player.AimDirectionDegrees = reader.ReadUInt16() * 360f / 65536f;
                    player.AimDistance = reader.ReadByte() * 2f;

                    if (includeQuickState)
                    {
                        player.X = reader.ReadUInt16() / 5f;
                        player.Y = reader.ReadUInt16() / 5f;
                        player.HorizontalSpeed = reader.ReadSByte() / 8.5f;
                        player.VerticalSpeed = reader.ReadSByte() / 8.5f;
                        player.Health = reader.ReadByte();
                        player.Ammo = reader.ReadByte();
                        var flags = reader.ReadByte();
                        player.IsSpyCloaked = (flags & 0x01) != 0;
                        player.MovementState = (byte)((flags >> 1) & 0x07);
                    }

                    if (fullUpdate)
                    {
                        player.AnimationOffset = reader.ReadByte();
                        var classSpecificByte = reader.ReadByte();
                        switch (player.ClassId)
                        {
                            case LegacyClassSpy:
                                player.SpyCloakAlpha = classSpecificByte / 255f;
                                break;
                            case LegacyClassMedic:
                                player.MedicUberScaled = classSpecificByte;
                                break;
                            case LegacyClassEngineer:
                                player.Metal = classSpecificByte;
                                break;
                            case LegacyClassSniper:
                                player.SniperChargeTicks = classSpecificByte;
                                player.IsScoped = classSpecificByte != 0;
                                break;
                        }

                        reader.ReadInt16();
                        player.Intel = reader.ReadByte() != 0;
                        player.IntelRechargeTicks = reader.ReadInt16();
                        ReadWeaponState(reader, player);
                    }
                }
                else
                {
                    player.Intel = false;
                    player.Mines.Clear();
                }

                if (hasSentry && (includeQuickState || fullUpdate))
                {
                    ReadSentryState(reader, player, fullUpdate);
                }
                else
                {
                    player.SentryBuilt = false;
                }
            }

            private void ReadWeaponState(BinaryReader reader, LegacyReplayPlayer player)
            {
                var previousMineStates = player.Mines
                    .Select(mine => new LegacyReplayMineState
                    {
                        Id = mine.Id,
                        X = mine.X,
                        Y = mine.Y,
                        HorizontalSpeed = mine.HorizontalSpeed,
                        VerticalSpeed = mine.VerticalSpeed,
                        IsStickied = mine.IsStickied,
                        IsTransient = mine.IsTransient,
                        TicksRemaining = mine.TicksRemaining
                    })
                    .ToList();
                player.WeaponReadyToShoot = reader.ReadByte() != 0;
                player.WeaponCooldownTicks = reader.ReadByte();
                player.WeaponAuxTicks = 0;
                player.PyroGas = 0;
                player.HeavyBullets = 0;
                player.BladePower = 0;
                player.MedicIsHealing = false;
                player.MedicHealTargetPlayerId = -1;
                player.Mines.Clear();

                switch (player.ClassId)
                {
                    case LegacyClassScout:
                    case LegacyClassSoldier:
                    case LegacyClassEngineer:
                    case LegacyClassSpy:
                        player.WeaponAuxTicks = reader.ReadByte();
                        break;
                    case LegacyClassPyro:
                        player.PyroGas = reader.ReadByte();
                        break;
                    case LegacyClassHeavy:
                        player.HeavyBullets = reader.ReadByte();
                        break;
                    case LegacyClassMedic:
                    {
                        player.WeaponAuxTicks = reader.ReadByte();
                        player.MedicIsHealing = reader.ReadByte() != 0;
                        if (!player.MedicIsHealing)
                        {
                            break;
                        }

                        var healTargetIndex = reader.ReadByte();
                        player.MedicHealTargetPlayerId = healTargetIndex == 200 || healTargetIndex >= _players.Count
                            ? -1
                            : _players[healTargetIndex].StablePlayerId;
                        break;
                    }
                    case LegacyClassDemoman:
                    {
                        var lobbedMineCount = reader.ReadByte();
                        player.WeaponAuxTicks = reader.ReadByte();
                        for (var index = 0; index < lobbedMineCount; index += 1)
                        {
                            var mine = new LegacyReplayMineState
                            {
                                Id = checked(player.StablePlayerId * 100 + index + 1),
                                X = reader.ReadUInt16() / 5f,
                                Y = reader.ReadUInt16() / 5f,
                                HorizontalSpeed = reader.ReadSByte() / 5f,
                                VerticalSpeed = reader.ReadSByte() / 5f,
                                IsStickied = reader.ReadByte() != 0,
                                TicksRemaining = 120
                            };
                            player.Mines.Add(mine);
                        }

                        RecordRemovedMineExplosions(previousMineStates, player.Mines);

                        break;
                    }
                    case LegacyClassQuote:
                        player.BladePower = reader.ReadByte();
                        break;
                }
            }

            private static void ReadSentryState(BinaryReader reader, LegacyReplayPlayer player, bool fullUpdate)
            {
                if (fullUpdate)
                {
                    player.SentryFacingDirectionX = Math.Sign(reader.ReadSByte());
                    if (player.SentryFacingDirectionX == 0f)
                    {
                        player.SentryFacingDirectionX = 1f;
                    }

                    player.SentryX = reader.ReadUInt16() / 5f;
                    player.SentryY = reader.ReadUInt16() / 5f;
                }

                var packedState = reader.ReadByte();
                player.SentryBuilt = (packedState & 0x80) != 0;
                player.SentryHealth = packedState & 0x7F;
                if (fullUpdate && !player.SentryBuilt)
                {
                    var encodedVerticalSpeed = reader.ReadByte();
                    player.SentryVerticalSpeed = encodedVerticalSpeed > 127 ? encodedVerticalSpeed - 256 : encodedVerticalSpeed;
                }
                else
                {
                    player.SentryVerticalSpeed = 0f;
                }
            }

            private void AdvancePlayersFromInputTick()
            {
                foreach (var player in _players)
                {
                    if (!player.HasCharacter || player.Team is not (LegacyTeamRed or LegacyTeamBlue))
                    {
                        continue;
                    }

                    AdvancePlayerFromInputState(player);
                }
            }

            private void AdvancePlayerFromInputState(LegacyReplayPlayer player)
            {
                var classDefinition = CharacterClassCatalog.GetDefinition(MapLegacyClassToCurrent(player.ClassId));
                ResolveEmbeddedPlayerCollision(player, classDefinition);

                var movementState = ToLegacyMovementState(player.MovementState);
                var jumpHeld = (player.KeyState & LegacyKeyJump) != 0;
                var wasGrounded = IsPlayerGrounded(player, classDefinition);
                if (wasGrounded)
                {
                    player.AirJumpsUsed = 0;
                    if (movementState != LegacyMovementState.None)
                    {
                        player.MovementState = (byte)LegacyMovementState.None;
                        movementState = LegacyMovementState.None;
                    }
                }

                var jumpRequested = jumpHeld && (!player.WasJumpHeldLastTick || player.QueueJump);
                if (jumpRequested && player.VerticalSpeed > -classDefinition.JumpStrength)
                {
                    if (wasGrounded && !IntersectsPlayerSolid(classDefinition, player.X, player.Y))
                    {
                        player.VerticalSpeed = -classDefinition.JumpStrength;
                        wasGrounded = false;
                    }
                    else if (classDefinition.MaxAirJumps > 0 && player.AirJumpsUsed < classDefinition.MaxAirJumps)
                    {
                        player.VerticalSpeed = -classDefinition.JumpStrength;
                        player.AirJumpsUsed += 1;
                        player.MovementState = 0;
                        movementState = LegacyMovementState.None;
                        wasGrounded = false;
                    }
                }

                player.WasJumpHeldLastTick = jumpHeld;

                var leftHeld = (player.KeyState & LegacyKeyLeft) != 0;
                var rightHeld = (player.KeyState & LegacyKeyRight) != 0;
                var horizontalDirection = leftHeld == rightHeld
                    ? 0f
                    : leftHeld ? -1f : 1f;
                var hasHorizontalInput = leftHeld || rightHeld;

                var nextHorizontalSpeedPerSecond = LegacyMovementModel.AdvanceHorizontalSpeed(
                    player.HorizontalSpeed * TickRate,
                    classDefinition.RunPower,
                    movementScale: 1f,
                    hasHorizontalInput,
                    horizontalDirection,
                    movementState,
                    player.Intel,
                    1f / TickRate);
                player.HorizontalSpeed = nextHorizontalSpeedPerSecond / TickRate;
                player.HorizontalSpeed = Math.Clamp(
                    player.HorizontalSpeed,
                    -LegacyMovementModel.MaxStepSpeedPerTick,
                    LegacyMovementModel.MaxStepSpeedPerTick);
                player.VerticalSpeed = Math.Clamp(
                    player.VerticalSpeed,
                    -LegacyMovementModel.MaxStepSpeedPerTick,
                    LegacyMovementModel.MaxStepSpeedPerTick);

                var applyGravity = !wasGrounded && !IntersectsPlayerSolid(classDefinition, player.X, player.Y);
                if (applyGravity)
                {
                    player.VerticalSpeed = MathF.Min(
                        LegacyMovementModel.MaxFallSpeedPerTick,
                        player.VerticalSpeed + (LegacyMovementModel.GetAirborneGravityPerTick(movementState) * 0.5f));
                }

                ResolvePlayerMovementWithCollision(player, classDefinition);

                if (!IsPlayerGrounded(player, classDefinition))
                {
                    if (applyGravity || !IntersectsPlayerSolid(classDefinition, player.X, player.Y))
                    {
                        player.VerticalSpeed = MathF.Min(
                            LegacyMovementModel.MaxFallSpeedPerTick,
                            player.VerticalSpeed + (LegacyMovementModel.GetAirborneGravityPerTick(movementState) * 0.5f));
                    }
                }
                else if (player.VerticalSpeed > 0f)
                {
                    player.VerticalSpeed = 0f;
                }
            }

            private void ResolvePlayerMovementWithCollision(LegacyReplayPlayer player, CharacterClassDefinition classDefinition)
            {
                var horizontalSpeed = player.HorizontalSpeed;
                var verticalSpeed = player.VerticalSpeed;
                var horizontalLeft = horizontalSpeed;
                var verticalLeft = verticalSpeed;
                var loopCounter = 0;

                while ((MathF.Abs(horizontalLeft) > 0.1f || MathF.Abs(verticalLeft) > 0.1f) && loopCounter < 10)
                {
                    loopCounter += 1;
                    var collisionRectified = false;
                    var previousX = player.X;
                    var previousY = player.Y;

                    MovePlayerToward(player, classDefinition, horizontalLeft, verticalLeft);
                    horizontalLeft -= player.X - previousX;
                    verticalLeft -= player.Y - previousY;

                    if (verticalLeft != 0f
                        && IntersectsPlayerSolid(classDefinition, player.X, player.Y + MathF.Sign(verticalLeft)))
                    {
                        if (verticalLeft > 0f)
                        {
                            player.MovementState = 0;
                        }

                        verticalLeft = 0f;
                        player.VerticalSpeed = 0f;
                        collisionRectified = true;
                    }

                    if (horizontalLeft != 0f
                        && IntersectsPlayerSolid(classDefinition, player.X + MathF.Sign(horizontalLeft), player.Y))
                    {
                        var stepDirection = MathF.Sign(horizontalLeft);
                        if (CanOccupyPlayerPosition(classDefinition, player.X + stepDirection, player.Y - 6f))
                        {
                            player.Y -= 6f;
                            player.MovementState = 0;
                            collisionRectified = true;
                        }
                        else if (CanOccupyPlayerPosition(classDefinition, player.X + stepDirection, player.Y + 6f)
                            && MathF.Abs(horizontalSpeed) >= MathF.Abs(verticalSpeed))
                        {
                            player.Y += 6f;
                            player.MovementState = 0;
                            collisionRectified = true;
                        }
                        else
                        {
                            horizontalLeft = 0f;
                            player.HorizontalSpeed = 0f;
                            collisionRectified = true;
                        }
                    }

                    if (!collisionRectified && (MathF.Abs(horizontalLeft) >= 1f || MathF.Abs(verticalLeft) >= 1f))
                    {
                        player.VerticalSpeed = 0f;
                        verticalLeft = 0f;
                    }
                }

                player.X = ClampPlayerX(player.X, classDefinition);
                player.Y = ClampPlayerY(player.Y, classDefinition);
            }

            private void MovePlayerToward(
                LegacyReplayPlayer player,
                CharacterClassDefinition classDefinition,
                float horizontalDistance,
                float verticalDistance)
            {
                var travelDistance = MathF.Sqrt((horizontalDistance * horizontalDistance) + (verticalDistance * verticalDistance));
                if (travelDistance <= 0.0001f)
                {
                    return;
                }

                var stepCount = Math.Max(1, (int)Math.Ceiling(travelDistance * 2f));
                var stepX = horizontalDistance / stepCount;
                var stepY = verticalDistance / stepCount;
                for (var stepIndex = 0; stepIndex < stepCount; stepIndex += 1)
                {
                    var nextX = ClampPlayerX(player.X + stepX, classDefinition);
                    var nextY = ClampPlayerY(player.Y + stepY, classDefinition);
                    if (IntersectsPlayerSolid(classDefinition, nextX, nextY))
                    {
                        break;
                    }

                    player.X = nextX;
                    player.Y = nextY;
                }
            }

            private void ResolveEmbeddedPlayerCollision(LegacyReplayPlayer player, CharacterClassDefinition classDefinition)
            {
                if (!IntersectsPlayerSolid(classDefinition, player.X, player.Y))
                {
                    return;
                }

                var width = classDefinition.CollisionRight - classDefinition.CollisionLeft;
                var height = classDefinition.CollisionBottom - classDefinition.CollisionTop;
                var bestDistance = float.MaxValue;
                var bestX = player.X;
                var bestY = player.Y;

                TryFindNearestOpenPlayerPosition(player.X, player.Y, classDefinition, 0f, -1f, Math.Max(1f, height * 0.5f), ref bestDistance, ref bestX, ref bestY);
                TryFindNearestOpenPlayerPosition(player.X, player.Y, classDefinition, 0f, 1f, Math.Max(1f, height * 0.5f), ref bestDistance, ref bestX, ref bestY);
                TryFindNearestOpenPlayerPosition(player.X, player.Y, classDefinition, 1f, 0f, Math.Max(1f, width * 0.5f), ref bestDistance, ref bestX, ref bestY);
                TryFindNearestOpenPlayerPosition(player.X, player.Y, classDefinition, -1f, 0f, Math.Max(1f, width * 0.5f), ref bestDistance, ref bestX, ref bestY);

                if (bestDistance < float.MaxValue)
                {
                    player.X = bestX;
                    player.Y = bestY;
                }
            }

            private void TryFindNearestOpenPlayerPosition(
                float originX,
                float originY,
                CharacterClassDefinition classDefinition,
                float directionX,
                float directionY,
                float maxDistance,
                ref float bestDistance,
                ref float bestX,
                ref float bestY)
            {
                for (var distance = 0.5f; distance <= maxDistance + 0.001f; distance += 0.5f)
                {
                    var candidateX = ClampPlayerX(originX + directionX * distance, classDefinition);
                    var candidateY = ClampPlayerY(originY + directionY * distance, classDefinition);
                    if (IntersectsPlayerSolid(classDefinition, candidateX, candidateY))
                    {
                        continue;
                    }

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestX = candidateX;
                        bestY = candidateY;
                    }

                    return;
                }
            }

            private void InitializeLegacyObjectiveState()
            {
                if (GameMode == GameModeKind.Arena)
                {
                    _arenaUnlockTicksRemaining = TickRate * 60;
                    _arenaPointTeamLegacy = byte.MaxValue;
                    _arenaCappingTeamLegacy = byte.MaxValue;
                    _arenaCappingTicks = 0;
                    _arenaCappers = 0;
                    return;
                }

                if (GameMode is not (GameModeKind.ControlPoint or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill))
                {
                    return;
                }

                var markers = _level.GetRoomObjects(RoomObjectType.ControlPoint);
                for (var index = 0; index < markers.Count; index += 1)
                {
                    var marker = markers[index];
                    var pointIndex = index + 1;
                    _controlPoints.Add(new LegacyReplayControlPointState(
                        pointIndex,
                        marker,
                        ResolveInitialObjectiveTeam(marker),
                        byte.MaxValue,
                        0,
                        ResolveDefaultControlPointCapTimeTicks(pointIndex, markers.Count),
                        ResolveInitialObjectiveLockState()));
                }

                if (GameMode == GameModeKind.KingOfTheHill)
                {
                    KothUnlockTicksRemaining = TickRate * 30;
                    KothRedTimerTicksRemaining = TickRate * 180;
                    KothBlueTimerTicksRemaining = TickRate * 180;
                }
                else if (GameMode == GameModeKind.DoubleKingOfTheHill)
                {
                    KothUnlockTicksRemaining = TickRate * 30;
                    KothRedTimerTicksRemaining = TickRate * 180;
                    KothBlueTimerTicksRemaining = TickRate * 180;
                }
                else if (GameMode == GameModeKind.ControlPoint && _level.GetRoomObjects(RoomObjectType.ControlPointSetupGate).Count > 0)
                {
                    ControlPointSetupTicksRemaining = TickRate * 60;
                }
            }

            private byte ResolveInitialObjectiveTeam(RoomObjectMarker marker)
            {
                if (GameMode == GameModeKind.DoubleKingOfTheHill)
                {
                    return marker.Team switch
                    {
                        PlayerTeam.Red => LegacyTeamRed,
                        PlayerTeam.Blue => LegacyTeamBlue,
                        _ => byte.MaxValue,
                    };
                }

                if (GameMode == GameModeKind.KingOfTheHill)
                {
                    return byte.MaxValue;
                }

                return marker.Team switch
                {
                    PlayerTeam.Red => LegacyTeamRed,
                    PlayerTeam.Blue => LegacyTeamBlue,
                    _ => byte.MaxValue,
                };
            }

            private bool ResolveInitialObjectiveLockState()
            {
                return GameMode is GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill;
            }

            private int ResolveDefaultControlPointCapTimeTicks(int pointIndex, int totalPoints)
            {
                if (GameMode is GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill)
                {
                    return TickRate * 10;
                }

                var hasSetupGate = _level.GetRoomObjects(RoomObjectType.ControlPointSetupGate).Count > 0;
                var baseTime = hasSetupGate ? 6f * TickRate : 7f * TickRate;
                return Math.Max(1, (int)MathF.Round(GetLegacyControlPointCapTime(totalPoints, pointIndex, baseTime, hasSetupGate)));
            }

            private static float GetLegacyControlPointCapTime(int totalPoints, int pointIndex, float baseTime, bool setupMode)
            {
                if (totalPoints <= 1)
                {
                    return baseTime * (setupMode ? 15f : 9f);
                }

                if (setupMode)
                {
                    return totalPoints switch
                    {
                        2 => pointIndex == 2 ? baseTime * 2.5f : baseTime * 10f,
                        3 => pointIndex == 3 ? baseTime * 2.5f : pointIndex == 2 ? baseTime * 5f : baseTime * 7.5f,
                        4 => pointIndex == 4 ? baseTime * 1.5f : pointIndex == 3 ? baseTime * 3f : pointIndex == 2 ? baseTime * 4.5f : baseTime * 6f,
                        _ => pointIndex == 5 ? baseTime : pointIndex == 4 ? baseTime * 2f : pointIndex == 3 ? baseTime * 3f : pointIndex == 2 ? baseTime * 4f : baseTime * 5f,
                    };
                }

                return totalPoints switch
                {
                    2 => baseTime * 4.5f,
                    3 => pointIndex == 2 ? baseTime * 4.5f : baseTime * 2.25f,
                    4 => pointIndex == 4 ? baseTime * 1.5f : pointIndex == 3 ? baseTime * 3f : pointIndex == 2 ? baseTime * 3f : baseTime * 1.5f,
                    _ => pointIndex == 5 ? baseTime : pointIndex == 4 ? baseTime * 2f : pointIndex == 3 ? baseTime * 3f : pointIndex == 2 ? baseTime * 2f : baseTime,
                };
            }

            private void ApplyArenaRoundReset(int arenaUnlockTicksRemaining)
            {
                MatchPhase = MatchPhase.Running;
                WinnerTeam = 0;
                _arenaUnlockTicksRemaining = Math.Max(0, arenaUnlockTicksRemaining);
                _arenaPointTeamLegacy = byte.MaxValue;
                _arenaCappingTeamLegacy = byte.MaxValue;
                _arenaCappingTicks = 0;
                _arenaCappers = 0;
                ClearTransientPresentationState();

                for (var index = 0; index < _players.Count; index += 1)
                {
                    var player = _players[index];
                    if (player.Team is not (LegacyTeamRed or LegacyTeamBlue))
                    {
                        player.HasCharacter = false;
                        continue;
                    }

                    player.HasCharacter = true;
                    player.Intel = false;
                    player.ChatBubbleTicksRemaining = 0;
                    player.IsScoped = false;
                    player.AirJumpsUsed = 0;
                    player.WasJumpHeldLastTick = false;
                    player.Mines.Clear();
                }
            }

            private void ClearTransientPresentationState()
            {
                _shots.Clear();
                _bubbles.Clear();
                _blades.Clear();
                _needles.Clear();
                _revolverShots.Clear();
                _rockets.Clear();
                _flames.Clear();
                _playerGibs.Clear();
                _deadBodies.Clear();
                _bloodDrops.Clear();
                _combatTraces.Clear();
                _pendingVisualEvents.Clear();
                _pendingSoundEvents.Clear();
                _killFeed.Clear();
            }

            private void UpdateDerivedIntelState()
            {
                if (RedIntel.CarrierPlayerId >= 0)
                {
                    var carrier = _players.FirstOrDefault(player => player.StablePlayerId == RedIntel.CarrierPlayerId);
                    if (carrier is not null)
                    {
                        RedIntel.MarkCarried(carrier);
                    }
                }

                if (BlueIntel.CarrierPlayerId >= 0)
                {
                    var carrier = _players.FirstOrDefault(player => player.StablePlayerId == BlueIntel.CarrierPlayerId);
                    if (carrier is not null)
                    {
                        BlueIntel.MarkCarried(carrier);
                    }
                }
            }

            private void TickTransientState()
            {
                foreach (var player in _players)
                {
                    if (player.ChatBubbleTicksRemaining > 0)
                    {
                        player.ChatBubbleTicksRemaining -= 1;
                    }

                    TickMines(player);
                }

                TickKillFeed();
                TickCombatTraces();
                TickPlayerGibs();
                TickDeadBodies();
                TickBloodDrops();
                TickBubbles();
                TickBlades();
                TickDirectFireProjectiles(_shots, ShotProjectileEntity.GravityPerTick, bloodEffectCount: 1);
                TickDirectFireProjectiles(_needles, NeedleProjectileEntity.GravityPerTick, bloodEffectCount: 1);
                TickDirectFireProjectiles(_revolverShots, RevolverProjectileEntity.GravityPerTick, bloodEffectCount: 1);
                TickRockets();
                TickFlames();
            }

            private SnapshotPlayerState CreateSnapshotPlayerState(LegacyReplayPlayer player, int listIndex)
            {
                var isPlayableTeam = player.Team == LegacyTeamRed || player.Team == LegacyTeamBlue;
                var classId = MapLegacyClassToCurrent(player.ClassId);
                var classDefinition = CharacterClassCatalog.GetDefinition(classId);
                var snapshotTeam = ToSnapshotTeam(player.Team);
                var maxAmmo = classDefinition.PrimaryWeapon.MaxAmmo;
                var ammo = ResolveSnapshotAmmo(player, classDefinition);
                var facingDirectionX = ResolveFacingDirection(player);
                var isGrounded = IsPlayerGrounded(player, classDefinition);
                var isAlive = player.HasCharacter && isPlayableTeam;
                var isSpectator = !isPlayableTeam;
                var slot = isSpectator ? (byte)(SimulationWorld.FirstSpectatorSlot + Math.Min(126, listIndex)) : (byte)(listIndex + 1);
                var (aimWorldX, aimWorldY) = ResolveReplayAimWorldTarget(player);

                return new SnapshotPlayerState(
                    Slot: slot,
                    PlayerId: player.StablePlayerId,
                    Name: player.Name,
                    Team: snapshotTeam,
                    ClassId: (byte)classId,
                    IsAlive: isAlive,
                    IsAwaitingJoin: isPlayableTeam && !player.HasCharacter,
                    IsSpectator: isSpectator,
                    RespawnTicks: 0,
                    X: player.X,
                    Y: player.Y,
                    HorizontalSpeed: player.HorizontalSpeed * TickRate,
                    VerticalSpeed: player.VerticalSpeed * TickRate,
                    Health: (short)player.Health,
                    MaxHealth: (short)classDefinition.MaxHealth,
                    Ammo: (short)ammo,
                    MaxAmmo: (short)maxAmmo,
                    Kills: player.Kills,
                    Deaths: player.Deaths,
                    Caps: player.Caps,
                    Points: player.Points,
                    HealPoints: player.HealPoints,
                    ActiveDominationCount: 0,
                    IsDominatingLocalViewer: false,
                    IsDominatedByLocalViewer: false,
                    Metal: player.Metal,
                    IsGrounded: isGrounded,
                    RemainingAirJumps: 0,
                    IsCarryingIntel: player.Intel,
                    IntelRechargeTicks: player.IntelRechargeTicks,
                    IsSpyCloaked: player.IsSpyCloaked,
                    SpyCloakAlpha: classId == PlayerClass.Spy ? player.SpyCloakAlpha : 0f,
                    IsSpySuperjumping: false,
                    SpySuperjumpHorizontalVelocity: 0f,
                    SpySuperjumpCooldownTicksRemaining: 0,
                    SpyBackstabVisualTicksRemaining: 0,
                    IsUbered: false,
                    IsKritzCritBoosted: false,
                    IsHeavyEating: false,
                    HeavyEatTicksRemaining: 0,
                    IsSniperScoped: classId == PlayerClass.Sniper && player.IsScoped,
                    IsUsingBinoculars: false,
                    BinocularsFocusX: aimWorldX,
                    BinocularsFocusY: aimWorldY,
                    FacingDirectionX: facingDirectionX,
                    AimDirectionDegrees: player.AimDirectionDegrees,
                    IsTaunting: false,
                    IsChatBubbleVisible: player.ChatBubbleTicksRemaining > 0,
                    ChatBubbleFrameIndex: player.ChatBubbleFrameIndex,
                    ChatBubbleAlpha: player.ChatBubbleTicksRemaining > 0
                        ? Math.Clamp(player.ChatBubbleTicksRemaining / (float)ChatBubbleLifetimeTicks, 0f, 1f)
                        : 0f,
                    BurnIntensity: 0f,
                    BurnDurationSourceTicks: 0f,
                    BurnDecayDelaySourceTicksRemaining: 0f,
                    BurnIntensityDecayPerSourceTick: 0f,
                    BurnedByPlayerId: -1,
                    MovementState: player.MovementState,
                    PrimaryCooldownTicks: player.WeaponReadyToShoot ? 0 : player.WeaponCooldownTicks,
                    ReloadTicksUntilNextShell: 0,
                    MedicNeedleCooldownTicks: 0,
                    MedicNeedleRefillTicks: 0,
                    PyroAirblastCooldownTicks: 0,
                    PyroFlareCooldownTicks: 0,
                    PyroPrimaryFuelScaled: classId == PlayerClass.Pyro ? player.PyroGas * PlayerEntity.PyroPrimaryFuelScale : 0,
                    IsPyroPrimaryRefilling: false,
                    PyroFlameLoopTicksRemaining: 0,
                    PyroPrimaryRequiresReleaseAfterEmpty: false,
                    HeavyEatCooldownTicksRemaining: 0,
                    Assists: player.Assists,
                    BadgeMask: 0,
                    GameplayModPackId: string.Empty,
                    GameplayLoadoutId: string.Empty,
                    GameplayPrimaryItemId: string.Empty,
                    GameplaySecondaryItemId: string.Empty,
                    GameplayUtilityItemId: string.Empty,
                    GameplayEquippedSlot: 0,
                    GameplayEquippedItemId: string.Empty,
                    GameplayAcquiredItemId: string.Empty,
                    OwnedGameplayItemIds: Array.Empty<string>(),
                    ReplicatedStates: Array.Empty<SnapshotReplicatedStateEntry>(),
                    PlayerScale: 1f,
                    AimWorldX: aimWorldX,
                    AimWorldY: aimWorldY);
            }

            private (float X, float Y) ResolveReplayAimWorldTarget(LegacyReplayPlayer player)
            {
                GetAimDirection(player.AimDirectionDegrees, out var aimDirectionX, out var aimDirectionY);
                var aimDistance = player.AimDistance > 0.01f
                    ? player.AimDistance
                    : MathF.Max(_level.Bounds.Width, _level.Bounds.Height);
                return (
                    player.X + aimDirectionX * aimDistance,
                    player.Y + aimDirectionY * aimDistance);
            }

            private List<SnapshotSentryState> CreateSnapshotSentries()
            {
                var sentries = new List<SnapshotSentryState>();
                foreach (var player in _players)
                {
                    if (!player.HasSentry || player.Team is not (LegacyTeamRed or LegacyTeamBlue))
                    {
                        continue;
                    }

                    sentries.Add(new SnapshotSentryState(
                        Id: checked(player.StablePlayerId * 1000 + 1),
                        OwnerPlayerId: player.StablePlayerId,
                        Team: ToSnapshotTeam(player.Team),
                        X: player.SentryX,
                        Y: player.SentryY,
                        Health: player.SentryHealth,
                        IsBuilt: player.SentryBuilt,
                        FacingDirectionX: player.SentryFacingDirectionX,
                        AimDirectionDegrees: 0f,
                        ShotTraceTicksRemaining: 0,
                        HasLanded: true,
                        HasActiveTarget: false,
                        LastShotTargetX: player.SentryX,
                        LastShotTargetY: player.SentryY));
                }

                return sentries;
            }

            private List<SnapshotMineState> CreateSnapshotMines()
            {
                var mines = new List<SnapshotMineState>();
                foreach (var player in _players)
                {
                    if (player.Team is not (LegacyTeamRed or LegacyTeamBlue))
                    {
                        continue;
                    }

                    for (var index = 0; index < player.Mines.Count; index += 1)
                    {
                        var mine = player.Mines[index];
                        mines.Add(new SnapshotMineState(
                            Id: mine.Id,
                            Team: ToSnapshotTeam(player.Team),
                            OwnerId: player.StablePlayerId,
                            X: mine.X,
                            Y: mine.Y,
                            VelocityX: mine.HorizontalSpeed,
                            VelocityY: mine.VerticalSpeed,
                            IsStickied: mine.IsStickied,
                            IsDestroyed: false,
                            ExplosionDamage: MineProjectileEntity.BaseExplosionDamage));
                    }
                }

                return mines;
            }

            private static List<SnapshotShotState> CreateSnapshotProjectiles(List<LegacyReplayProjectileState> projectiles)
            {
                var snapshots = new List<SnapshotShotState>(projectiles.Count);
                for (var index = 0; index < projectiles.Count; index += 1)
                {
                    var projectile = projectiles[index];
                    snapshots.Add(new SnapshotShotState(
                        Id: projectile.Id,
                        Team: projectile.Team,
                        OwnerId: projectile.OwnerId,
                        X: projectile.X,
                        Y: projectile.Y,
                        VelocityX: projectile.VelocityX,
                        VelocityY: projectile.VelocityY,
                        TicksRemaining: projectile.TicksRemaining));
                }

                return snapshots;
            }

            private List<SnapshotRocketState> CreateSnapshotRockets()
            {
                var snapshots = new List<SnapshotRocketState>(_rockets.Count);
                for (var index = 0; index < _rockets.Count; index += 1)
                {
                    var rocket = _rockets[index];
                    snapshots.Add(new SnapshotRocketState(
                        Id: rocket.Id,
                        Team: rocket.Team,
                        OwnerId: rocket.OwnerId,
                        X: rocket.X,
                        Y: rocket.Y,
                        PreviousX: rocket.PreviousX,
                        PreviousY: rocket.PreviousY,
                        DirectionRadians: rocket.DirectionRadians,
                        Speed: rocket.Speed,
                        TicksRemaining: rocket.TicksRemaining));
                }

                return snapshots;
            }

            private List<SnapshotFlameState> CreateSnapshotFlames()
            {
                var snapshots = new List<SnapshotFlameState>(_flames.Count);
                for (var index = 0; index < _flames.Count; index += 1)
                {
                    var flame = _flames[index];
                    snapshots.Add(new SnapshotFlameState(
                        Id: flame.Id,
                        Team: flame.Team,
                        OwnerId: flame.OwnerId,
                        X: flame.X,
                        Y: flame.Y,
                        PreviousX: flame.PreviousX,
                        PreviousY: flame.PreviousY,
                        VelocityX: flame.VelocityX,
                        VelocityY: flame.VelocityY,
                        TicksRemaining: flame.TicksRemaining,
                        AttachedPlayerId: -1,
                        AttachedOffsetX: 0f,
                        AttachedOffsetY: 0f));
                }

                return snapshots;
            }

            private List<SnapshotBloodDropState> CreateSnapshotBloodDrops()
            {
                var snapshots = new List<SnapshotBloodDropState>(_bloodDrops.Count);
                for (var index = 0; index < _bloodDrops.Count; index += 1)
                {
                    var bloodDrop = _bloodDrops[index];
                    snapshots.Add(new SnapshotBloodDropState(
                        bloodDrop.Id,
                        bloodDrop.X,
                        bloodDrop.Y,
                        bloodDrop.VelocityX,
                        bloodDrop.VelocityY,
                        bloodDrop.IsStuck,
                        bloodDrop.TicksRemaining,
                        bloodDrop.Scale));
                }

                return snapshots;
            }

            private List<SnapshotDeadBodyState> CreateSnapshotDeadBodies()
            {
                var snapshots = new List<SnapshotDeadBodyState>(_deadBodies.Count);
                for (var index = 0; index < _deadBodies.Count; index += 1)
                {
                    var deadBody = _deadBodies[index];
                    snapshots.Add(new SnapshotDeadBodyState(
                        deadBody.Id,
                        deadBody.SourcePlayerId,
                        (byte)deadBody.Team,
                        (byte)deadBody.ClassId,
                        (byte)deadBody.AnimationKind,
                        deadBody.X,
                        deadBody.Y,
                        deadBody.Width,
                        deadBody.Height,
                        deadBody.HorizontalSpeed,
                        deadBody.VerticalSpeed,
                        deadBody.FacingLeft,
                        deadBody.TicksRemaining));
                }

                return snapshots;
            }

            private IReadOnlyList<SnapshotControlPointState> CreateSnapshotControlPoints()
            {
                if (_controlPoints.Count == 0)
                {
                    return Array.Empty<SnapshotControlPointState>();
                }

                var snapshots = new List<SnapshotControlPointState>(_controlPoints.Count);
                for (var index = 0; index < _controlPoints.Count; index += 1)
                {
                    var point = _controlPoints[index];
                    snapshots.Add(new SnapshotControlPointState(
                        (byte)point.Index,
                        ToSnapshotObjectiveTeam(point.TeamLegacy),
                        ToSnapshotObjectiveTeam(point.CappingTeamLegacy),
                        point.CappingTicks,
                        (ushort)Math.Clamp(point.CapTimeTicks, 0, ushort.MaxValue),
                        Cappers: 0,
                        point.IsLocked));
                }

                return snapshots;
            }

            private List<SnapshotPlayerGibState> CreateSnapshotPlayerGibs()
            {
                var snapshots = new List<SnapshotPlayerGibState>(_playerGibs.Count);
                for (var index = 0; index < _playerGibs.Count; index += 1)
                {
                    var gib = _playerGibs[index];
                    snapshots.Add(new SnapshotPlayerGibState(
                        gib.Id,
                        gib.SpriteName,
                        gib.FrameIndex,
                        gib.X,
                        gib.Y,
                        gib.VelocityX,
                        gib.VelocityY,
                        gib.RotationDegrees,
                        gib.RotationSpeedDegrees,
                        gib.TicksRemaining,
                        gib.BloodChance));
                }

                return snapshots;
            }

            private List<SnapshotKillFeedEntry> CreateSnapshotKillFeed()
            {
                var snapshots = new List<SnapshotKillFeedEntry>(_killFeed.Count);
                for (var index = 0; index < _killFeed.Count; index += 1)
                {
                    snapshots.Add(_killFeed[index].Entry);
                }

                return snapshots;
            }

            private List<SnapshotCombatTraceState> CreateSnapshotCombatTraces()
            {
                var traces = new List<SnapshotCombatTraceState>(_combatTraces.Count);
                for (var index = 0; index < _combatTraces.Count; index += 1)
                {
                    var trace = _combatTraces[index];
                    traces.Add(new SnapshotCombatTraceState(
                        trace.StartX,
                        trace.StartY,
                        trace.EndX,
                        trace.EndY,
                        trace.TicksRemaining,
                        HitCharacter: trace.HitCharacter,
                        trace.Team,
                        trace.IsSniperTracer));
                }

                return traces;
            }

            private List<SnapshotVisualEvent> CreateSnapshotVisualEvents()
            {
                var snapshots = new List<SnapshotVisualEvent>(_pendingVisualEvents.Count);
                for (var index = 0; index < _pendingVisualEvents.Count; index += 1)
                {
                    snapshots.Add(_pendingVisualEvents[index]);
                }

                return snapshots;
            }

            private List<SnapshotSoundEvent> CreateSnapshotSoundEvents()
            {
                var snapshots = new List<SnapshotSoundEvent>(_pendingSoundEvents.Count);
                for (var index = 0; index < _pendingSoundEvents.Count; index += 1)
                {
                    snapshots.Add(_pendingSoundEvents[index]);
                }

                return snapshots;
            }

            private void RecordKillFeed(LegacyReplayPlayer victim, LegacyReplayPlayer? killer)
            {
                var resolvedKiller = killer ?? victim;
                var weaponSpriteName = CharacterClassCatalog.GetPrimaryWeaponKillFeedSprite(
                    MapLegacyClassToCurrent(resolvedKiller.ClassId));
                _killFeed.Add(new LegacyReplayKillFeedEntry(
                    new SnapshotKillFeedEntry(
                        KillerName: resolvedKiller.Name,
                        KillerTeam: ToSnapshotTeam(resolvedKiller.Team),
                        WeaponSpriteName: weaponSpriteName,
                        VictimName: victim.Name,
                        VictimTeam: ToSnapshotTeam(victim.Team),
                        KillerPlayerId: resolvedKiller.StablePlayerId,
                        VictimPlayerId: victim.StablePlayerId,
                        EventId: _nextReplayEventId++),
                    KillFeedLifetimeTicks));
            }

            private void RecordIntelPickedUpObjectiveLog(LegacyReplayPlayer player)
            {
                RecordObjectiveLogEntry(player.Name, ToSnapshotTeam(player.Team), "picked up the intelligence!", player.Team == LegacyTeamBlue ? "BlueCaptureS" : "RedCaptureS", player.StablePlayerId);
            }

            private void RecordIntelCapturedObjectiveLog(LegacyReplayPlayer player)
            {
                RecordObjectiveLogEntry(player.Name, ToSnapshotTeam(player.Team), "captured the intelligence!", player.Team == LegacyTeamBlue ? "BlueCaptureS" : "RedCaptureS", player.StablePlayerId);
            }

            private void RecordIntelDroppedObjectiveLog(LegacyReplayPlayer player)
            {
                RecordObjectiveLogEntry(player.Name, ToSnapshotTeam(player.Team), " dropped the intelligence!", string.Empty, player.StablePlayerId);
            }

            private void RecordIntelReturnedObjectiveLog(PlayerTeam team)
            {
                RecordObjectiveLogEntry(team == PlayerTeam.Blue ? "Blue" : "Red", (byte)team, " Intel has returned to base!", string.Empty, -1);
            }

            private void RecordSystemMessage(string messageText)
            {
                RecordObjectiveLogEntry(string.Empty, SnapshotTeamRed, messageText, string.Empty, -1);
            }

            private void RecordObjectiveLogEntry(string name, byte team, string messageText, string weaponSpriteName, int playerId)
            {
                _killFeed.Add(new LegacyReplayKillFeedEntry(
                    new SnapshotKillFeedEntry(
                        KillerName: name,
                        KillerTeam: team,
                        WeaponSpriteName: weaponSpriteName,
                        VictimName: string.Empty,
                        VictimTeam: team,
                        MessageText: messageText,
                        KillerPlayerId: playerId,
                        VictimPlayerId: -1,
                        EventId: _nextReplayEventId++),
                    KillFeedLifetimeTicks));
            }

            private void QueueWeaponSound(LegacyReplayPlayer player, CharacterClassDefinition classDefinition)
            {
                var soundName = ResolveWeaponFireSoundName(player, MapLegacyClassToCurrent(player.ClassId), classDefinition.PrimaryWeapon.Kind);

                if (!string.IsNullOrWhiteSpace(soundName))
                {
                    QueueSoundEvent(soundName, player.X, player.Y);
                }
            }

            private static string ResolveWeaponFireSoundName(LegacyReplayPlayer player, PlayerClass currentClass, PrimaryWeaponKind weaponKind)
            {
                if (currentClass == PlayerClass.Quote && (player.KeyState & LegacyKeyRightClick) != 0)
                {
                    return "BladeSnd";
                }

                if (currentClass == PlayerClass.Medic && (player.KeyState & LegacyKeyRightClick) != 0)
                {
                    return "MedichaingunSnd";
                }

                return weaponKind switch
                {
                    PrimaryWeaponKind.PelletGun => "ShotgunSnd",
                    PrimaryWeaponKind.FlameThrower => "FlamethrowerSnd",
                    PrimaryWeaponKind.RocketLauncher => "RocketSnd",
                    PrimaryWeaponKind.MineLauncher => "MinegunSnd",
                    PrimaryWeaponKind.Minigun => "ChaingunSnd",
                    PrimaryWeaponKind.Rifle => "SniperSnd",
                    PrimaryWeaponKind.Revolver => "RevolverSnd",
                    _ => string.Empty
                };
            }

            private void QueueSoundEvent(string soundName, float x, float y)
            {
                if (string.IsNullOrWhiteSpace(soundName))
                {
                    return;
                }

                _pendingSoundEvents.Add(new SnapshotSoundEvent(
                    soundName,
                    x,
                    y,
                    EventId: _nextReplayEventId++,
                    SourceFrame: SnapshotFrame));
            }

            private void QueueVisualEvent(string effectName, float x, float y, float directionDegrees, int count)
            {
                if (string.IsNullOrWhiteSpace(effectName))
                {
                    return;
                }

                _pendingVisualEvents.Add(new SnapshotVisualEvent(
                    effectName,
                    x,
                    y,
                    directionDegrees,
                    Math.Max(1, count),
                    EventId: _nextReplayEventId++));
            }

            private void SpawnWeaponPresentation(LegacyReplayPlayer player)
            {
                if (!player.HasCharacter || player.Team is not (LegacyTeamRed or LegacyTeamBlue))
                {
                    return;
                }

                var currentClass = MapLegacyClassToCurrent(player.ClassId);
                var classDefinition = CharacterClassCatalog.GetDefinition(currentClass);
                var directionRadians = -player.AimDirectionDegrees * MathF.PI / 180f;
                GetAimDirection(player.AimDirectionDegrees, out var directionX, out var directionY);
                var team = ToSnapshotTeam(player.Team);
                var weaponOrigin = GetReplaySourceWeaponOrigin(player, currentClass, classDefinition);
                var weaponKind = ResolveReplayWeaponKind(player, currentClass, classDefinition.PrimaryWeapon.Kind);
                QueueWeaponSound(player, classDefinition);

                switch (weaponKind)
                {
                    case PrimaryWeaponKind.RocketLauncher:
                    {
                        var spawnX = weaponOrigin.BaseX + directionX * 20f;
                        var spawnY = weaponOrigin.BaseY + directionY * 20f;
                        if (IsReplayProjectileSpawnBlocked(weaponOrigin.BaseX, weaponOrigin.BaseY, spawnX, spawnY))
                        {
                            QueueSoundEvent("ExplosionSnd", spawnX, spawnY);
                            break;
                        }

                        _rockets.Add(new LegacyReplayRocketState(
                            NextTransientEntityId(),
                            team,
                            player.StablePlayerId,
                            spawnX,
                            spawnY,
                            directionRadians,
                            classDefinition.PrimaryWeapon.MinShotSpeed,
                            RocketProjectileEntity.LifetimeTicks));
                        break;
                    }
                    case PrimaryWeaponKind.PelletGun:
                    {
                        var spawnDistance = 15f;
                        SpawnPelletProjectiles(
                            player,
                            classDefinition.PrimaryWeapon,
                            weaponOrigin.BaseX + directionX * spawnDistance,
                            weaponOrigin.BaseY + directionY * spawnDistance,
                            directionRadians,
                            team);
                        break;
                    }
                    case PrimaryWeaponKind.Minigun:
                    {
                        var spawnX = weaponOrigin.BaseX + directionX * 20f;
                        var spawnY = weaponOrigin.BaseY + 12f + directionY * 20f;
                        _shots.Add(new LegacyReplayProjectileState(
                            NextTransientEntityId(),
                            team,
                            player.StablePlayerId,
                            spawnX,
                            spawnY,
                            directionX * classDefinition.PrimaryWeapon.MinShotSpeed + player.HorizontalSpeed,
                            directionY * classDefinition.PrimaryWeapon.MinShotSpeed,
                            ShotProjectileEntity.LifetimeTicks));
                        break;
                    }
                    case PrimaryWeaponKind.Revolver:
                    {
                        var shotOriginY = weaponOrigin.BaseY + weaponOrigin.WeaponYOffset + 1f;
                        _revolverShots.Add(new LegacyReplayProjectileState(
                            NextTransientEntityId(),
                            team,
                            player.StablePlayerId,
                            weaponOrigin.BaseX,
                            shotOriginY,
                            directionX * classDefinition.PrimaryWeapon.MinShotSpeed + player.HorizontalSpeed,
                            directionY * classDefinition.PrimaryWeapon.MinShotSpeed,
                            RevolverProjectileEntity.LifetimeTicks));
                        break;
                    }
                    case PrimaryWeaponKind.FlameThrower:
                    {
                        const float flamethrowerPivotOffsetX = -3f;
                        const float flamethrowerPivotAdditionalYOffset = 2f;
                        const float flameSpawnDistanceFromPivot = 35f;
                        var pivotRay = GetReplayWeaponPivotRay(
                            weaponOrigin.BaseX,
                            GetReplayPyroOriginY(weaponOrigin),
                            player.AimDirectionDegrees,
                            ResolveFacingDirection(player),
                            flamethrowerPivotOffsetX,
                            flamethrowerPivotAdditionalYOffset);
                        var spawnX = pivotRay.PivotX + pivotRay.DirectionX * flameSpawnDistanceFromPivot;
                        var spawnY = pivotRay.PivotY + pivotRay.DirectionY * flameSpawnDistanceFromPivot;
                        if (IsReplayFlameSpawnBlocked(pivotRay.PivotX, pivotRay.PivotY, spawnX, spawnY, player.Team))
                        {
                            break;
                        }

                        _flames.Add(new LegacyReplayFlameState(
                            NextTransientEntityId(),
                            team,
                            player.StablePlayerId,
                            spawnX,
                            spawnY,
                            pivotRay.DirectionX * classDefinition.PrimaryWeapon.MinShotSpeed + player.HorizontalSpeed,
                            pivotRay.DirectionY * classDefinition.PrimaryWeapon.MinShotSpeed + player.VerticalSpeed,
                            FlameProjectileEntity.AirLifetimeTicks));
                        break;
                    }
                    case PrimaryWeaponKind.Medigun:
                        break;
                    case PrimaryWeaponKind.Rifle:
                        RecordCombatTrace(player, weaponOrigin.BaseX, weaponOrigin.BaseY, directionX, directionY, currentClass == PlayerClass.Sniper);
                        break;
                    case PrimaryWeaponKind.Blade:
                        if (currentClass == PlayerClass.Quote)
                        {
                            if ((player.KeyState & LegacyKeyRightClick) != 0)
                            {
                                SpawnQuoteBladeProjectile(
                                    player,
                                    weaponOrigin.BaseX + directionX * 5f,
                                    weaponOrigin.BaseY + directionY * 5f,
                                    directionX,
                                    directionY,
                                    team);
                            }
                            else
                            {
                                SpawnQuoteBubbleProjectile(
                                    player,
                                    weaponOrigin.BaseX + directionX * 8f,
                                    weaponOrigin.BaseY + directionY * 8f,
                                    directionX,
                                    directionY,
                                    team);
                            }
                        }
                        else
                        {
                            RecordCombatTrace(player, weaponOrigin.BaseX, weaponOrigin.BaseY, directionX, directionY, isSniperTracer: false);
                        }
                        break;
                    case PrimaryWeaponKind.MineLauncher:
                        SpawnTransientMine(
                            player,
                            weaponOrigin.BaseX + directionX * 10f,
                            weaponOrigin.BaseY + directionY * 10f,
                            directionX,
                            directionY);
                        break;
                    default:
                    {
                        var shotOriginY = weaponOrigin.BaseY + weaponOrigin.WeaponYOffset + 1f;
                        _needles.Add(new LegacyReplayProjectileState(
                            NextTransientEntityId(),
                            team,
                            player.StablePlayerId,
                            weaponOrigin.BaseX,
                            shotOriginY,
                            directionX * classDefinition.PrimaryWeapon.MinShotSpeed + player.HorizontalSpeed,
                            directionY * classDefinition.PrimaryWeapon.MinShotSpeed,
                            NeedleProjectileEntity.LifetimeTicks));
                        break;
                    }
                }
            }

            private void SpawnQuoteBubbleProjectile(LegacyReplayPlayer player, float sourceX, float sourceY, float directionX, float directionY, byte team)
            {
                const float bubbleSpeed = 10f;
                _bubbles.Add(new LegacyReplayProjectileState(
                    NextTransientEntityId(),
                    team,
                    player.StablePlayerId,
                    sourceX,
                    sourceY,
                    directionX * bubbleSpeed + (player.HorizontalSpeed * 0.2f),
                    directionY * bubbleSpeed + (player.VerticalSpeed * 0.2f),
                    BubbleProjectileEntity.LifetimeTicks));
            }

            private void SpawnQuoteBladeProjectile(LegacyReplayPlayer player, float sourceX, float sourceY, float directionX, float directionY, byte team)
            {
                const float bladeSpeed = 12f;
                _blades.Add(new LegacyReplayProjectileState(
                    NextTransientEntityId(),
                    team,
                    player.StablePlayerId,
                    sourceX,
                    sourceY,
                    directionX * bladeSpeed + player.HorizontalSpeed,
                    directionY * bladeSpeed + player.VerticalSpeed,
                    PlayerEntity.QuoteBladeLifetimeTicks));
            }

            private void SpawnTransientMine(LegacyReplayPlayer player, float sourceX, float sourceY, float directionX, float directionY)
            {
                player.Mines.Add(new LegacyReplayMineState
                {
                    Id = NextTransientEntityId(),
                    X = sourceX,
                    Y = sourceY,
                    HorizontalSpeed = directionX * 6f + player.HorizontalSpeed,
                    VerticalSpeed = directionY * 6f + player.VerticalSpeed,
                    IsStickied = false,
                    IsTransient = true,
                    TicksRemaining = 60
                });
            }

            private void SpawnPelletProjectiles(
                LegacyReplayPlayer player,
                PrimaryWeaponDefinition weapon,
                float sourceX,
                float sourceY,
                float baseDirectionRadians,
                byte team)
            {
                var projectileCount = Math.Max(1, weapon.ProjectilesPerShot);
                for (var pelletIndex = 0; pelletIndex < projectileCount; pelletIndex += 1)
                {
                    var spreadRadians = GetDeterministicPelletSpreadRadians(pelletIndex, projectileCount, weapon.SpreadDegrees);
                    var pelletAngle = baseDirectionRadians + spreadRadians;
                    _shots.Add(new LegacyReplayProjectileState(
                        NextTransientEntityId(),
                        team,
                        player.StablePlayerId,
                        sourceX,
                        sourceY,
                        MathF.Cos(pelletAngle) * weapon.MinShotSpeed,
                        MathF.Sin(pelletAngle) * weapon.MinShotSpeed,
                        ShotProjectileEntity.LifetimeTicks));
                }
            }

            private void RecordCombatTrace(
                LegacyReplayPlayer player,
                float sourceX,
                float sourceY,
                float directionX,
                float directionY,
                bool isSniperTracer)
            {
                const float traceDistance = 480f;
                _combatTraces.Add(new LegacyReplayCombatTrace(
                    sourceX,
                    sourceY,
                    sourceX + directionX * traceDistance,
                    sourceY + directionY * traceDistance,
                    CombatTraceLifetimeTicks,
                    hitCharacter: false,
                    ToSnapshotTeam(player.Team),
                    isSniperTracer));
            }

            private void TickKillFeed()
            {
                for (var index = _killFeed.Count - 1; index >= 0; index -= 1)
                {
                    var entry = _killFeed[index];
                    entry.TicksRemaining -= 1;
                    if (entry.TicksRemaining <= 0)
                    {
                        _killFeed.RemoveAt(index);
                    }
                }
            }

            private void TickCombatTraces()
            {
                for (var index = _combatTraces.Count - 1; index >= 0; index -= 1)
                {
                    var trace = _combatTraces[index];
                    trace.TicksRemaining -= 1;
                    if (trace.TicksRemaining <= 0)
                    {
                        _combatTraces.RemoveAt(index);
                    }
                }
            }

            private void TickPlayerGibs()
            {
                for (var index = _playerGibs.Count - 1; index >= 0; index -= 1)
                {
                    var gib = _playerGibs[index];
                    gib.Advance(_level, _level.Bounds);
                    if (!gib.IsExpired)
                    {
                        continue;
                    }

                    _playerGibs.RemoveAt(index);
                }
            }

            private void TickBloodDrops()
            {
                for (var index = _bloodDrops.Count - 1; index >= 0; index -= 1)
                {
                    var bloodDrop = _bloodDrops[index];
                    bloodDrop.Advance(_level, _level.Bounds);
                    if (!bloodDrop.IsExpired)
                    {
                        continue;
                    }

                    _bloodDrops.RemoveAt(index);
                }
            }

            private void TickDeadBodies()
            {
                for (var index = _deadBodies.Count - 1; index >= 0; index -= 1)
                {
                    var deadBody = _deadBodies[index];
                    deadBody.Advance(_level, _level.Bounds);
                    if (!deadBody.IsExpired)
                    {
                        continue;
                    }

                    _deadBodies.RemoveAt(index);
                }
            }

            private void TickBubbles()
            {
                for (var index = _bubbles.Count - 1; index >= 0; index -= 1)
                {
                    var bubble = _bubbles[index];
                    var previousX = bubble.X;
                    var previousY = bubble.Y;
                    bubble.X += bubble.VelocityX;
                    bubble.Y += bubble.VelocityY;
                    bubble.VelocityX *= 0.98f;
                    bubble.VelocityY *= 0.98f;
                    bubble.TicksRemaining -= 1;

                    if (_level.IntersectsSolid(
                            bubble.X - BubbleProjectileEntity.Radius,
                            bubble.Y - BubbleProjectileEntity.Radius,
                            bubble.X + BubbleProjectileEntity.Radius,
                            bubble.Y + BubbleProjectileEntity.Radius))
                    {
                        QueueVisualEvent("Pop", previousX, previousY, 0f, 1);
                        _bubbles.RemoveAt(index);
                        continue;
                    }

                    if (bubble.TicksRemaining <= 0)
                    {
                        QueueVisualEvent("Pop", bubble.X, bubble.Y, 0f, 1);
                        _bubbles.RemoveAt(index);
                    }
                }
            }

            private void TickBlades()
            {
                for (var index = _blades.Count - 1; index >= 0; index -= 1)
                {
                    var blade = _blades[index];
                    var previousX = blade.X;
                    var previousY = blade.Y;
                    blade.X += blade.VelocityX;
                    blade.Y += blade.VelocityY;
                    blade.TicksRemaining -= 1;

                    var movementX = blade.X - previousX;
                    var movementY = blade.Y - previousY;
                    var movementDistance = MathF.Sqrt((movementX * movementX) + (movementY * movementY));
                    if (movementDistance > 0.0001f)
                    {
                        var directionX = movementX / movementDistance;
                        var directionY = movementY / movementDistance;
                        var hit = TryResolveProjectileHit(
                            previousX,
                            previousY,
                            directionX,
                            directionY,
                            movementDistance,
                            blade.Team,
                            blade.OwnerId,
                            padding: 2f);
                        if (hit is not null)
                        {
                            if (hit.HitPlayer is not null)
                            {
                                SpawnReplayBloodEffect(hit.HitPlayer.X, hit.HitPlayer.Y, directionX, directionY, bloodEffectCount: 2);
                                QueueVisualEvent("Blood", hit.HitPlayer.X, hit.HitPlayer.Y, PointDirectionDegrees(previousX, previousY, hit.HitPlayer.X, hit.HitPlayer.Y) - 180f, 2);
                            }

                            _blades.RemoveAt(index);
                            continue;
                        }
                    }

                    if (blade.TicksRemaining <= 0)
                    {
                        _blades.RemoveAt(index);
                    }
                }
            }

            private void TickDirectFireProjectiles(
                List<LegacyReplayProjectileState> projectiles,
                float gravityPerTick,
                int bloodEffectCount)
            {
                for (var index = projectiles.Count - 1; index >= 0; index -= 1)
                {
                    var projectile = projectiles[index];
                    var previousX = projectile.X;
                    var previousY = projectile.Y;
                    projectile.X += projectile.VelocityX;
                    projectile.Y += projectile.VelocityY;
                    projectile.VelocityY += gravityPerTick;
                    projectile.TicksRemaining -= 1;

                    var movementX = projectile.X - previousX;
                    var movementY = projectile.Y - previousY;
                    var movementDistance = MathF.Sqrt((movementX * movementX) + (movementY * movementY));
                    if (movementDistance > 0.0001f)
                    {
                        var directionX = movementX / movementDistance;
                        var directionY = movementY / movementDistance;
                        var hit = TryResolveProjectileHit(
                            previousX,
                            previousY,
                            directionX,
                            directionY,
                            movementDistance,
                            projectile.Team,
                            projectile.OwnerId);
                        if (hit is not null)
                        {
                            projectile.X = hit.HitX;
                            projectile.Y = hit.HitY;
                            _combatTraces.Add(new LegacyReplayCombatTrace(
                                previousX,
                                previousY,
                                hit.HitX,
                                hit.HitY,
                                CombatTraceLifetimeTicks,
                                hitCharacter: hit.HitPlayer is not null,
                                projectile.Team,
                                isSniperTracer: false));
                            if (hit.HitPlayer is not null)
                            {
                                SpawnReplayBloodEffect(hit.HitPlayer.X, hit.HitPlayer.Y, directionX, directionY, bloodEffectCount);
                                QueueVisualEvent("Blood", hit.HitPlayer.X, hit.HitPlayer.Y, PointDirectionDegrees(previousX, previousY, hit.HitPlayer.X, hit.HitPlayer.Y) - 180f, bloodEffectCount);
                            }

                            projectiles.RemoveAt(index);
                            continue;
                        }
                    }

                    if (projectile.TicksRemaining <= 0)
                    {
                        projectiles.RemoveAt(index);
                    }
                }
            }

            private void TickRockets()
            {
                for (var index = _rockets.Count - 1; index >= 0; index -= 1)
                {
                    var rocket = _rockets[index];
                    rocket.PreviousX = rocket.X;
                    rocket.PreviousY = rocket.Y;
                    rocket.X += MathF.Cos(rocket.DirectionRadians) * rocket.Speed;
                    rocket.Y += MathF.Sin(rocket.DirectionRadians) * rocket.Speed;
                    rocket.Speed += 1f;
                    rocket.Speed *= 0.92f;
                    rocket.TicksRemaining -= 1;
                    var movementX = rocket.X - rocket.PreviousX;
                    var movementY = rocket.Y - rocket.PreviousY;
                    var movementDistance = MathF.Sqrt((movementX * movementX) + (movementY * movementY));
                    if (movementDistance > 0.0001f)
                    {
                        var directionX = movementX / movementDistance;
                        var directionY = movementY / movementDistance;
                        var hit = TryResolveProjectileHit(
                            rocket.PreviousX,
                            rocket.PreviousY,
                            directionX,
                            directionY,
                            movementDistance,
                            rocket.Team,
                            rocket.OwnerId,
                            padding: RocketProjectileEntity.MaskHalfThickness);
                        if (hit is not null)
                        {
                            rocket.X = hit.HitX;
                            rocket.Y = hit.HitY;
                            QueueSoundEvent("ExplosionSnd", hit.HitX, hit.HitY);
                            _combatTraces.Add(new LegacyReplayCombatTrace(
                                rocket.PreviousX,
                                rocket.PreviousY,
                                hit.HitX,
                                hit.HitY,
                                CombatTraceLifetimeTicks,
                                hitCharacter: hit.HitPlayer is not null,
                                rocket.Team,
                                isSniperTracer: false));
                            if (hit.HitPlayer is not null)
                            {
                                SpawnReplayBloodEffect(hit.HitPlayer.X, hit.HitPlayer.Y, directionX, directionY, bloodEffectCount: 4);
                                QueueVisualEvent("Blood", hit.HitPlayer.X, hit.HitPlayer.Y, PointDirectionDegrees(rocket.PreviousX, rocket.PreviousY, hit.HitPlayer.X, hit.HitPlayer.Y) - 180f, 4);
                            }

                            _rockets.RemoveAt(index);
                            continue;
                        }
                    }

                    if (rocket.TicksRemaining <= 0)
                    {
                        _rockets.RemoveAt(index);
                    }
                }
            }

            private void TickFlames()
            {
                for (var index = _flames.Count - 1; index >= 0; index -= 1)
                {
                    var flame = _flames[index];
                    flame.PreviousX = flame.X;
                    flame.PreviousY = flame.Y;
                    flame.X += flame.VelocityX;
                    flame.Y += flame.VelocityY;
                    flame.VelocityY += FlameProjectileEntity.GravityPerTick;
                    flame.TicksRemaining -= 1;
                    var movementX = flame.X - flame.PreviousX;
                    var movementY = flame.Y - flame.PreviousY;
                    var movementDistance = MathF.Sqrt((movementX * movementX) + (movementY * movementY));
                    if (movementDistance > 0.0001f)
                    {
                        var directionX = movementX / movementDistance;
                        var directionY = movementY / movementDistance;
                        var hit = TryResolveProjectileHit(
                            flame.PreviousX,
                            flame.PreviousY,
                            directionX,
                            directionY,
                            movementDistance,
                            flame.Team,
                            flame.OwnerId,
                            padding: 2f);
                        if (hit is not null)
                        {
                            flame.X = hit.HitX;
                            flame.Y = hit.HitY;
                            _combatTraces.Add(new LegacyReplayCombatTrace(
                                flame.PreviousX,
                                flame.PreviousY,
                                hit.HitX,
                                hit.HitY,
                                CombatTraceLifetimeTicks,
                                hitCharacter: hit.HitPlayer is not null,
                                flame.Team,
                                isSniperTracer: false));
                            if (hit.HitPlayer is not null)
                            {
                                SpawnReplayBloodEffect(hit.HitPlayer.X, hit.HitPlayer.Y, directionX, directionY, bloodEffectCount: 1);
                                QueueVisualEvent("Blood", hit.HitPlayer.X, hit.HitPlayer.Y, PointDirectionDegrees(flame.PreviousX, flame.PreviousY, hit.HitPlayer.X, hit.HitPlayer.Y) - 180f, 1);
                            }

                            _flames.RemoveAt(index);
                            continue;
                        }
                    }

                    if (flame.TicksRemaining <= 0)
                    {
                        _flames.RemoveAt(index);
                    }
                }
            }

            private void TickMines(LegacyReplayPlayer player)
            {
                for (var index = player.Mines.Count - 1; index >= 0; index -= 1)
                {
                    var mine = player.Mines[index];
                    if (mine.IsStickied)
                    {
                        if (mine.IsTransient && mine.TicksRemaining > 0)
                        {
                            mine.TicksRemaining -= 1;
                            if (mine.TicksRemaining <= 0)
                            {
                                player.Mines.RemoveAt(index);
                            }
                        }

                        continue;
                    }

                    var previousX = mine.X;
                    var previousY = mine.Y;
                    mine.X += mine.HorizontalSpeed;
                    if (_level.IntersectsSolid(
                            mine.X - ReplayMineCollisionHalfExtent,
                            mine.Y - ReplayMineCollisionHalfExtent,
                            mine.X + ReplayMineCollisionHalfExtent,
                            mine.Y + ReplayMineCollisionHalfExtent))
                    {
                        mine.X = previousX;
                        mine.HorizontalSpeed = 0f;
                        mine.IsStickied = true;
                    }

                    mine.Y += mine.VerticalSpeed;
                    if (_level.IntersectsSolid(
                            mine.X - ReplayMineCollisionHalfExtent,
                            mine.Y - ReplayMineCollisionHalfExtent,
                            mine.X + ReplayMineCollisionHalfExtent,
                            mine.Y + ReplayMineCollisionHalfExtent))
                    {
                        mine.Y = previousY;
                        mine.VerticalSpeed = 0f;
                        mine.HorizontalSpeed = 0f;
                        mine.IsStickied = true;
                    }

                    mine.VerticalSpeed = MathF.Min(MineProjectileEntity.MaxFallSpeed, mine.VerticalSpeed + MineProjectileEntity.GravityPerTick);
                    if (mine.IsTransient && mine.TicksRemaining > 0)
                    {
                        mine.TicksRemaining -= 1;
                        if (mine.TicksRemaining <= 0)
                        {
                            player.Mines.RemoveAt(index);
                        }
                    }
                }
            }

            private static int ResolveSnapshotAmmo(LegacyReplayPlayer player, CharacterClassDefinition classDefinition)
            {
                return MapLegacyClassToCurrent(player.ClassId) switch
                {
                    PlayerClass.Pyro when player.PyroGas > 0 => Math.Clamp(player.PyroGas, 0, classDefinition.PrimaryWeapon.MaxAmmo),
                    PlayerClass.Heavy when player.HeavyBullets > 0 => Math.Clamp(player.HeavyBullets, 0, classDefinition.PrimaryWeapon.MaxAmmo),
                    _ => Math.Clamp(player.Ammo, 0, classDefinition.PrimaryWeapon.MaxAmmo),
                };
            }

            private bool IsPlayerGrounded(LegacyReplayPlayer player, CharacterClassDefinition classDefinition)
            {
                if (!player.HasCharacter)
                {
                    return false;
                }

                var probeY = player.Y + classDefinition.CollisionBottom + 1f;
                var leftProbeX = player.X + classDefinition.CollisionLeft + 2f;
                var rightProbeX = player.X + classDefinition.CollisionRight - 2f;
                foreach (var solid in _level.Solids)
                {
                    if ((leftProbeX >= solid.Left && leftProbeX < solid.Right && probeY >= solid.Top && probeY < solid.Bottom)
                        || (rightProbeX >= solid.Left && rightProbeX < solid.Right && probeY >= solid.Top && probeY < solid.Bottom))
                    {
                        return true;
                    }
                }

                return Math.Abs(player.VerticalSpeed) < 0.2f;
            }

            private static PrimaryWeaponKind ResolveReplayWeaponKind(LegacyReplayPlayer player, PlayerClass currentClass, PrimaryWeaponKind primaryWeaponKind)
            {
                if (currentClass == PlayerClass.Medic && (player.KeyState & LegacyKeyRightClick) != 0)
                {
                    return PrimaryWeaponKind.PelletGun;
                }

                return primaryWeaponKind;
            }

            private ReplaySourceWeaponOrigin GetReplaySourceWeaponOrigin(
                LegacyReplayPlayer player,
                PlayerClass currentClass,
                CharacterClassDefinition classDefinition)
            {
                return new ReplaySourceWeaponOrigin(
                    MathF.Round(player.X),
                    MathF.Round(player.Y),
                    GetReplaySourceWeaponYOffset(currentClass),
                    GetReplaySourceEquipmentOffset(player, currentClass, classDefinition));
            }

            private static float GetReplaySourceWeaponYOffset(PlayerClass classId)
            {
                return classId switch
                {
                    PlayerClass.Scout => -4f,
                    PlayerClass.Engineer => -2f,
                    PlayerClass.Pyro => 4f,
                    PlayerClass.Soldier => -10f,
                    PlayerClass.Demoman => -2f,
                    PlayerClass.Heavy => 0f,
                    PlayerClass.Sniper => -8f,
                    PlayerClass.Medic => 0f,
                    PlayerClass.Spy => -6f,
                    PlayerClass.Quote => -3f,
                    _ => 0f,
                };
            }

            private float GetReplaySourceEquipmentOffset(
                LegacyReplayPlayer player,
                PlayerClass currentClass,
                CharacterClassDefinition classDefinition)
            {
                var horizontalSourceStepSpeed = MathF.Abs(player.HorizontalSpeed);
                if (!IsPlayerGrounded(player, classDefinition)
                    || currentClass == PlayerClass.Quote
                    || player.IsScoped
                    || horizontalSourceStepSpeed >= 0.2f)
                {
                    return 0f;
                }

                return GetReplaySourceLeanYOffset(player, currentClass, classDefinition);
            }

            private float GetReplaySourceLeanYOffset(
                LegacyReplayPlayer player,
                PlayerClass currentClass,
                CharacterClassDefinition classDefinition)
            {
                var bottom = player.Y + classDefinition.CollisionBottom + 2f;
                var openRight = !IsPointBlockedForReplayPlayer(player, currentClass, classDefinition, player.X + 6f, bottom)
                    && !IsPointBlockedForReplayPlayer(player, currentClass, classDefinition, player.X + 2f, bottom);
                var openLeft = !IsPointBlockedForReplayPlayer(player, currentClass, classDefinition, player.X - 7f, bottom)
                    && !IsPointBlockedForReplayPlayer(player, currentClass, classDefinition, player.X - 3f, bottom);

                if (openRight && openLeft)
                {
                    openRight = !IsPointBlockedForReplayPlayer(player, currentClass, classDefinition, player.X + classDefinition.CollisionRight - 1f, bottom);
                    openLeft = !IsPointBlockedForReplayPlayer(player, currentClass, classDefinition, player.X + classDefinition.CollisionLeft, bottom);
                }

                return openRight ^ openLeft ? 6f : 0f;
            }

            private bool IsPointBlockedForReplayPlayer(
                LegacyReplayPlayer player,
                PlayerClass currentClass,
                CharacterClassDefinition classDefinition,
                float x,
                float y)
            {
                foreach (var solid in _level.Solids)
                {
                    if (x >= solid.Left && x < solid.Right && y >= solid.Top && y < solid.Bottom)
                    {
                        return true;
                    }
                }

                var team = (PlayerTeam)ToSnapshotTeam(player.Team);
                foreach (var gate in _level.GetBlockingTeamGates(team, player.Intel))
                {
                    if (x >= gate.Left && x < gate.Right && y >= gate.Top && y < gate.Bottom)
                    {
                        return true;
                    }
                }

                foreach (var wall in _level.GetRoomObjects(RoomObjectType.PlayerWall))
                {
                    if (x >= wall.Left && x < wall.Right && y >= wall.Top && y < wall.Bottom)
                    {
                        return true;
                    }
                }

                return false;
            }

            private static float GetReplayPyroOriginY(ReplaySourceWeaponOrigin weaponOrigin)
            {
                return weaponOrigin.BaseY + weaponOrigin.WeaponYOffset + weaponOrigin.EquipmentOffset;
            }

            private static ReplayWeaponPivotRay GetReplayWeaponPivotRay(
                float originX,
                float originY,
                float aimDirectionDegrees,
                float fallbackFacingDirectionX,
                float pivotOffsetX,
                float pivotOffsetY)
            {
                GetAimDirection(aimDirectionDegrees, out var aimDirectionX, out var aimDirectionY);
                if (aimDirectionX == 0f && aimDirectionY == 0f)
                {
                    aimDirectionX = fallbackFacingDirectionX == 0f ? 1f : fallbackFacingDirectionX;
                }

                var initialAngle = MathF.Atan2(aimDirectionY, aimDirectionX);
                var facingScale = MathF.Cos(initialAngle) < 0f ? -1f : 1f;
                var pivotX = originX + pivotOffsetX * facingScale;
                var pivotY = originY + pivotOffsetY;
                var pivotDirectionX = MathF.Cos(initialAngle);
                var pivotDirectionY = MathF.Sin(initialAngle);
                return new ReplayWeaponPivotRay(pivotX, pivotY, pivotDirectionX, pivotDirectionY, initialAngle);
            }

            private bool IsReplayProjectileSpawnBlocked(float originX, float originY, float targetX, float targetY)
            {
                var distance = MathF.Sqrt((targetX - originX) * (targetX - originX) + (targetY - originY) * (targetY - originY));
                if (distance <= 0.0001f)
                {
                    return false;
                }

                var directionX = (targetX - originX) / distance;
                var directionY = (targetY - originY) / distance;
                for (var traveled = 0f; traveled <= distance; traveled += 1f)
                {
                    var sampleX = originX + directionX * traveled;
                    var sampleY = originY + directionY * traveled;
                    if (_level.IntersectsSolid(sampleX - 1f, sampleY - 1f, sampleX + 1f, sampleY + 1f))
                    {
                        return true;
                    }
                }

                return false;
            }

            private bool IsReplayFlameSpawnBlocked(float originX, float originY, float spawnX, float spawnY, byte legacyTeam)
            {
                if (IsReplayProjectileSpawnBlocked(originX, originY, spawnX, spawnY))
                {
                    return true;
                }

                var snapshotTeam = ToSnapshotTeam(legacyTeam);
                foreach (var gate in _level.GetBlockingTeamGates((PlayerTeam)snapshotTeam, false))
                {
                    if (IntersectsReplayLineRectangle(originX, originY, spawnX, spawnY, gate.Left, gate.Top, gate.Right, gate.Bottom))
                    {
                        return true;
                    }
                }

                foreach (var wall in _level.GetRoomObjects(RoomObjectType.PlayerWall))
                {
                    if (IntersectsReplayLineRectangle(originX, originY, spawnX, spawnY, wall.Left, wall.Top, wall.Right, wall.Bottom))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool IntersectsReplayLineRectangle(
                float originX,
                float originY,
                float targetX,
                float targetY,
                float left,
                float top,
                float right,
                float bottom)
            {
                var deltaX = targetX - originX;
                var deltaY = targetY - originY;
                var distance = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
                if (distance <= 0.0001f)
                {
                    return originX >= left && originX < right && originY >= top && originY < bottom;
                }

                var directionX = deltaX / distance;
                var directionY = deltaY / distance;
                for (var traveled = 0f; traveled <= distance; traveled += 1f)
                {
                    var sampleX = originX + directionX * traveled;
                    var sampleY = originY + directionY * traveled;
                    if (sampleX >= left && sampleX < right && sampleY >= top && sampleY < bottom)
                    {
                        return true;
                    }
                }

                return false;
            }

            private bool CanOccupyPlayerPosition(CharacterClassDefinition classDefinition, float x, float y)
            {
                var clampedX = ClampPlayerX(x, classDefinition);
                var clampedY = ClampPlayerY(y, classDefinition);
                return !IntersectsPlayerSolid(classDefinition, clampedX, clampedY);
            }

            private bool IntersectsPlayerSolid(CharacterClassDefinition classDefinition, float x, float y)
            {
                GetPlayerCollisionBounds(classDefinition, x, y, out var left, out var top, out var right, out var bottom);
                return _level.IntersectsSolid(left, top, right, bottom);
            }

            private float ClampPlayerX(float x, CharacterClassDefinition classDefinition)
            {
                var minX = -classDefinition.CollisionLeft;
                var maxX = _level.Bounds.Width - classDefinition.CollisionRight;
                return Math.Clamp(x, minX, maxX);
            }

            private float ClampPlayerY(float y, CharacterClassDefinition classDefinition)
            {
                var minY = -classDefinition.CollisionTop;
                var maxY = _level.Bounds.Height - classDefinition.CollisionBottom;
                return Math.Clamp(y, minY, maxY);
            }

            private static void GetPlayerCollisionBounds(
                CharacterClassDefinition classDefinition,
                float x,
                float y,
                out float left,
                out float top,
                out float right,
                out float bottom)
            {
                left = x + classDefinition.CollisionLeft;
                top = y + classDefinition.CollisionTop;
                right = x + classDefinition.CollisionRight;
                bottom = y + classDefinition.CollisionBottom;
            }

            private static void GetAimDirection(float aimDirectionDegrees, out float directionX, out float directionY)
            {
                var aimRadians = aimDirectionDegrees * MathF.PI / 180f;
                directionX = MathF.Cos(aimRadians);
                directionY = -MathF.Sin(aimRadians);
            }

            private static float PointDirectionDegrees(float x1, float y1, float x2, float y2)
            {
                var degrees = MathF.Atan2(y2 - y1, x2 - x1) * (180f / MathF.PI);
                if (degrees < 0f)
                {
                    degrees += 360f;
                }

                return degrees;
            }

            private static float GetDeterministicPelletSpreadRadians(int pelletIndex, int projectileCount, float spreadDegrees)
            {
                if (projectileCount <= 1)
                {
                    return 0f;
                }

                var normalized = pelletIndex / (float)(projectileCount - 1);
                var fraction = normalized * 2f - 1f;
                return fraction * spreadDegrees * (MathF.PI / 180f);
            }

            private readonly record struct ReplaySourceWeaponOrigin(float BaseX, float BaseY, float WeaponYOffset, float EquipmentOffset);

            private readonly record struct ReplayWeaponPivotRay(float PivotX, float PivotY, float DirectionX, float DirectionY, float AngleRadians);

            private void SpawnReplayDeathRemains(LegacyReplayPlayer victim, int killerIndex, byte deathSource)
            {
                if (ShouldSpawnReplayGibs(victim, killerIndex, deathSource))
                {
                    SpawnReplayPlayerGibs(victim);
                    QueueSoundEvent("Gibbing", victim.X, victim.Y);
                    QueueVisualEvent("GibBlood", victim.X, victim.Y, 0f, ReplayDefaultGibLevel);
                    return;
                }

                var classId = MapLegacyClassToCurrent(victim.ClassId);
                var classDefinition = CharacterClassCatalog.GetDefinition(classId);
                var animationKind = ResolveReplayDeathAnimationKind(victim, killerIndex, deathSource);
                _deadBodies.Add(new DeadBodyEntity(
                    NextTransientEntityId(),
                    victim.StablePlayerId,
                    classId,
                    (PlayerTeam)ToSnapshotTeam(victim.Team),
                    animationKind,
                    victim.X,
                    victim.Y,
                    classDefinition.Width,
                    classDefinition.Height,
                    victim.HorizontalSpeed,
                    victim.VerticalSpeed,
                    ResolveFacingDirection(victim) < 0f));

                if (killerIndex >= 0 && killerIndex < _players.Count && MapLegacyClassToCurrent(_players[killerIndex].ClassId) == PlayerClass.Spy)
                {
                    var killer = _players[killerIndex];
                    var directionDegrees = PointDirectionDegrees(killer.X, killer.Y, victim.X, victim.Y);
                    QueueVisualEvent(
                        killer.Team == LegacyTeamBlue ? "BackstabBlue" : "BackstabRed",
                        killer.X,
                        killer.Y,
                        directionDegrees,
                        killer.StablePlayerId);
                    QueueSoundEvent("KnifeSnd", killer.X, killer.Y);
                }
                else
                {
                    QueueSoundEvent(((victim.StablePlayerId + deathSource) & 1) == 0 ? "DeathSnd1" : "DeathSnd2", victim.X, victim.Y);
                }
            }

            private bool ShouldSpawnReplayGibs(LegacyReplayPlayer victim, int killerIndex, byte deathSource)
            {
                if (ReplayDefaultGibLevel <= 1 || deathSource == 0)
                {
                    return false;
                }

                if (killerIndex < 0 || killerIndex >= _players.Count)
                {
                    return true;
                }

                var killerClass = MapLegacyClassToCurrent(_players[killerIndex].ClassId);
                return killerClass is not (PlayerClass.Spy or PlayerClass.Quote or PlayerClass.Sniper);
            }

            private DeadBodyAnimationKind ResolveReplayDeathAnimationKind(LegacyReplayPlayer victim, int killerIndex, byte deathSource)
            {
                if (killerIndex >= 0 && killerIndex < _players.Count)
                {
                    var killerClass = MapLegacyClassToCurrent(_players[killerIndex].ClassId);
                    if (killerClass == PlayerClass.Sniper)
                    {
                        return DeadBodyAnimationKind.Rifle;
                    }

                    if (killerClass == PlayerClass.Spy || killerClass == PlayerClass.Quote)
                    {
                        return DeadBodyAnimationKind.Severe;
                    }
                }

                return deathSource == 0
                    ? DeadBodyAnimationKind.Default
                    : DeadBodyAnimationKind.Severe;
            }

            private void SpawnReplayBloodEffect(float x, float y, float directionX, float directionY, int bloodEffectCount)
            {
                var baseAngle = MathF.Atan2(directionY, directionX) + MathF.PI;
                var dropCount = Math.Clamp(4 + Math.Max(0, bloodEffectCount - 1), 4, 7);
                for (var index = 0; index < dropCount; index += 1)
                {
                    var spreadFactor = dropCount == 1 ? 0f : (index / (float)(dropCount - 1)) * 2f - 1f;
                    var angle = baseAngle + (spreadFactor * 22f * (MathF.PI / 180f));
                    var speed = 3f + (index % 4) * 2f;
                    _bloodDrops.Add(new BloodDropEntity(
                        NextTransientEntityId(),
                        x,
                        y,
                        MathF.Cos(angle) * speed,
                        MathF.Sin(angle) * speed));
                }
            }

            private void SpawnReplayPlayerGibs(LegacyReplayPlayer victim)
            {
                var classId = MapLegacyClassToCurrent(victim.ClassId);
                var fixedDeltaSeconds = 1f / Math.Max(1, TickRate);
                var inheritedVelocityX = victim.HorizontalSpeed * fixedDeltaSeconds;
                var inheritedVelocityY = victim.VerticalSpeed * fixedDeltaSeconds;
                SpawnReplayPlayerGibSet(victim, "GibS", ReplayDefaultGibLevel, randomFrameCount: 7, velocityRangeX: 8f, velocityRangeY: 9f, rotationRange: 72f, lifetimeTicks: 210, horizontalFriction: 0.4f, rotationFriction: 0.6f, bloodChance: 1.8f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY);
                SpawnReplayPlayerGibSet(victim, ToSnapshotTeam(victim.Team) == SnapshotTeamBlue ? "BlueClumpS" : "RedClumpS", ReplayDefaultGibLevel - 1, randomFrameCount: 4, velocityRangeX: 8f, velocityRangeY: 9f, rotationRange: 72f, lifetimeTicks: 250, horizontalFriction: 0.3f, rotationFriction: 0.4f, bloodChance: 2f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY);

                switch (classId)
                {
                    case PlayerClass.Scout:
                        SpawnReplayPlayerGibSet(victim, "HeadS", 1, frameIndex: 6, bloodChance: 1.4f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY);
                        SpawnReplayPlayerGibSet(victim, "FeetS", Math.Max(1, ReplayDefaultGibLevel - 1), frameIndex: 0, velocityRangeX: 2f, velocityRangeY: 0f, rotationRange: 6f, horizontalFriction: 0.3f, rotationFriction: 0.4f, bloodChance: 7f);
                        SpawnReplayPlayerGibSet(victim, "HandS", Math.Max(1, ReplayDefaultGibLevel - 1), frameIndex: 1, bloodChance: 5f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY);
                        break;
                    case PlayerClass.Pyro:
                        SpawnReplayPlayerGibSet(victim, "HeadS", 1, frameIndex: 7, bloodChance: 1.4f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY);
                        SpawnReplayPlayerGibSet(victim, "AccesoryS", 1, frameIndex: 4, bloodChance: 28f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY);
                        SpawnReplayPlayerGibSet(victim, "FeetS", Math.Max(1, ReplayDefaultGibLevel - 1), frameIndex: 1, velocityRangeX: 2f, velocityRangeY: 0f, rotationRange: 6f, horizontalFriction: 0.3f, rotationFriction: 0.4f, bloodChance: 7f);
                        SpawnReplayPlayerGibSet(victim, "HandS", Math.Max(1, ReplayDefaultGibLevel - 1), frameIndex: 0, bloodChance: 5f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY);
                        break;
                    case PlayerClass.Soldier:
                        SpawnReplayPlayerGibSet(victim, "HeadS", 1, frameIndex: 1, bloodChance: 1.4f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY);
                        SpawnReplayPlayerGibSet(victim, "FeetS", Math.Max(1, ReplayDefaultGibLevel - 1), frameIndex: 2, velocityRangeX: 2f, velocityRangeY: 0f, rotationRange: 6f, horizontalFriction: 0.3f, rotationFriction: 0.4f, bloodChance: 7f);
                        SpawnReplayPlayerGibSet(victim, "HandS", Math.Max(1, ReplayDefaultGibLevel - 1), frameIndex: 1, bloodChance: 5f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY);
                        SpawnReplayPlayerGibSet(victim, "AccesoryS", 1, frameIndex: ToSnapshotTeam(victim.Team) == SnapshotTeamBlue ? 2 : 1, bloodChance: 28f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY);
                        break;
                    case PlayerClass.Heavy:
                        SpawnReplayPlayerGibSet(victim, "HeadS", 1, frameIndex: 2, bloodChance: 1.4f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY);
                        SpawnReplayPlayerGibSet(victim, "FeetS", Math.Max(1, ReplayDefaultGibLevel - 1), frameIndex: 3, velocityRangeX: 2f, velocityRangeY: 0f, rotationRange: 6f, horizontalFriction: 0.3f, rotationFriction: 0.4f, bloodChance: 7f);
                        SpawnReplayPlayerGibSet(victim, "HandS", Math.Max(1, ReplayDefaultGibLevel - 1), frameIndex: 1, bloodChance: 5f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY);
                        break;
                    case PlayerClass.Demoman:
                        SpawnReplayPlayerGibSet(victim, "HeadS", 1, frameIndex: 4, bloodChance: 1.4f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY);
                        SpawnReplayPlayerGibSet(victim, "FeetS", Math.Max(1, ReplayDefaultGibLevel - 1), frameIndex: 4, velocityRangeX: 2f, velocityRangeY: 0f, rotationRange: 6f, horizontalFriction: 0.3f, rotationFriction: 0.4f, bloodChance: 7f);
                        SpawnReplayPlayerGibSet(victim, "HandS", Math.Max(1, ReplayDefaultGibLevel - 1), frameIndex: 0, bloodChance: 5f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY);
                        break;
                    case PlayerClass.Medic:
                        SpawnReplayPlayerGibSet(victim, "HeadS", 1, frameIndex: 5, bloodChance: 1.4f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY);
                        SpawnReplayPlayerGibSet(victim, "FeetS", Math.Max(1, ReplayDefaultGibLevel - 1), frameIndex: 4, velocityRangeX: 2f, velocityRangeY: 0f, rotationRange: 6f, horizontalFriction: 0.3f, rotationFriction: 0.4f, bloodChance: 7f);
                        SpawnReplayPlayerGibSet(victim, "HandS", 1, frameIndex: ToSnapshotTeam(victim.Team) == SnapshotTeamBlue ? 3 : 2, bloodChance: 5f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY);
                        break;
                    case PlayerClass.Engineer:
                        SpawnReplayPlayerGibSet(victim, "HeadS", 1, frameIndex: 8, bloodChance: 1.4f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY);
                        SpawnReplayPlayerGibSet(victim, "AccesoryS", 1, frameIndex: 3, bloodChance: 28f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY);
                        SpawnReplayPlayerGibSet(victim, "FeetS", Math.Max(1, ReplayDefaultGibLevel - 1), frameIndex: 5, velocityRangeX: 2f, velocityRangeY: 0f, rotationRange: 6f, horizontalFriction: 0.3f, rotationFriction: 0.4f, bloodChance: 7f);
                        SpawnReplayPlayerGibSet(victim, "HandS", Math.Max(1, ReplayDefaultGibLevel - 1), frameIndex: 0, bloodChance: 5f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY);
                        break;
                    case PlayerClass.Spy:
                        SpawnReplayPlayerGibSet(victim, "HeadS", 1, frameIndex: 3, bloodChance: 1.4f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY);
                        SpawnReplayPlayerGibSet(victim, "FeetS", Math.Max(1, ReplayDefaultGibLevel - 1), frameIndex: 6, velocityRangeX: 2f, velocityRangeY: 0f, rotationRange: 6f, horizontalFriction: 0.3f, rotationFriction: 0.4f, bloodChance: 7f);
                        SpawnReplayPlayerGibSet(victim, "HandS", Math.Max(1, ReplayDefaultGibLevel - 1), frameIndex: 0, bloodChance: 5f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY);
                        break;
                    case PlayerClass.Sniper:
                        SpawnReplayPlayerGibSet(victim, "HeadS", 1, frameIndex: 0, bloodChance: 1.4f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY);
                        SpawnReplayPlayerGibSet(victim, "AccesoryS", 1, frameIndex: 0, bloodChance: 28f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY);
                        SpawnReplayPlayerGibSet(victim, "FeetS", Math.Max(1, ReplayDefaultGibLevel - 1), frameIndex: 6, velocityRangeX: 2f, velocityRangeY: 0f, rotationRange: 6f, horizontalFriction: 0.3f, rotationFriction: 0.4f, bloodChance: 7f);
                        SpawnReplayPlayerGibSet(victim, "HandS", Math.Max(1, ReplayDefaultGibLevel - 1), frameIndex: 0, bloodChance: 5f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY);
                        break;
                }
            }

            private void SpawnReplayPlayerGibSet(
                LegacyReplayPlayer victim,
                string spriteName,
                int count,
                int? frameIndex = null,
                int randomFrameCount = 0,
                float velocityRangeX = 8f,
                float velocityRangeY = 9f,
                float rotationRange = 52f,
                int lifetimeTicks = 250,
                float horizontalFriction = 0.4f,
                float rotationFriction = 0.5f,
                float bloodChance = PlayerGibEntity.DefaultBloodChance,
                float inheritedVelocityX = 0f,
                float inheritedVelocityY = 0f)
            {
                for (var index = 0; index < Math.Max(0, count); index += 1)
                {
                    var resolvedFrameIndex = frameIndex ?? (randomFrameCount > 0 ? index % randomFrameCount : 0);
                    var spreadX = count <= 1 ? 0f : ((index / (float)Math.Max(1, count - 1)) * 2f - 1f) * velocityRangeX;
                    var spreadY = ((index % 3) - 1) * (velocityRangeY * 0.6f);
                    var rotationSpeed = ((index % 5) - 2) * (rotationRange / 3f);
                    _playerGibs.Add(new PlayerGibEntity(
                        NextTransientEntityId(),
                        spriteName,
                        resolvedFrameIndex,
                        victim.X,
                        victim.Y,
                        inheritedVelocityX + spreadX,
                        inheritedVelocityY + spreadY - (velocityRangeY * 0.35f),
                        rotationSpeed,
                        horizontalFriction,
                        rotationFriction,
                        lifetimeTicks,
                        bloodChance));
                }
            }

            private void RecordRemovedMineExplosions(List<LegacyReplayMineState> previousMineStates, List<LegacyReplayMineState> currentMineStates)
            {
                if (previousMineStates.Count <= currentMineStates.Count)
                {
                    return;
                }

                for (var index = currentMineStates.Count; index < previousMineStates.Count; index += 1)
                {
                    var removedMine = previousMineStates[index];
                    QueueSoundEvent("ExplosionSnd", removedMine.X, removedMine.Y);
                }
            }

            private LegacyReplayProjectileHit? TryResolveProjectileHit(
                float originX,
                float originY,
                float directionX,
                float directionY,
                float maxDistance,
                byte projectileTeam,
                int ownerId,
                float padding = 0f)
            {
                LegacyReplayProjectileHit? nearestHit = null;
                var rayBounds = new LegacyReplayRectangleHitbox(
                    MathF.Min(originX, originX + directionX * maxDistance) - padding,
                    MathF.Min(originY, originY + directionY * maxDistance) - padding,
                    MathF.Max(originX, originX + directionX * maxDistance) + padding,
                    MathF.Max(originY, originY + directionY * maxDistance) + padding);

                foreach (var solid in _level.Solids)
                {
                    if (!RayBoundsMayIntersectRectangle(rayBounds, solid.Left, solid.Top, solid.Right, solid.Bottom))
                    {
                        continue;
                    }

                    var distance = GetRayIntersectionDistanceWithRectangle(
                        originX,
                        originY,
                        directionX,
                        directionY,
                        solid.Left - padding,
                        solid.Top - padding,
                        solid.Right + padding,
                        solid.Bottom + padding,
                        maxDistance);
                    if (!distance.HasValue)
                    {
                        continue;
                    }

                    nearestHit = ResolveNearestProjectileHit(nearestHit, distance.Value, originX, originY, directionX, directionY, hitPlayer: null);
                }

                foreach (var roomObject in _level.RoomObjects)
                {
                    if (roomObject.Type is not (RoomObjectType.BulletWall or RoomObjectType.PlayerWall or RoomObjectType.TeamGate or RoomObjectType.ControlPointSetupGate))
                    {
                        continue;
                    }

                    if (!RayBoundsMayIntersectRectangle(rayBounds, roomObject.Left, roomObject.Top, roomObject.Right, roomObject.Bottom))
                    {
                        continue;
                    }

                    var distance = GetRayIntersectionDistanceWithRectangle(
                        originX,
                        originY,
                        directionX,
                        directionY,
                        roomObject.Left - padding,
                        roomObject.Top - padding,
                        roomObject.Right + padding,
                        roomObject.Bottom + padding,
                        maxDistance);
                    if (!distance.HasValue)
                    {
                        continue;
                    }

                    nearestHit = ResolveNearestProjectileHit(nearestHit, distance.Value, originX, originY, directionX, directionY, hitPlayer: null);
                }

                for (var index = 0; index < _players.Count; index += 1)
                {
                    var player = _players[index];
                    if (!player.HasCharacter
                        || player.StablePlayerId == ownerId
                        || ToSnapshotTeam(player.Team) == projectileTeam
                        || player.Team is not (LegacyTeamRed or LegacyTeamBlue))
                    {
                        continue;
                    }

                    var classDefinition = CharacterClassCatalog.GetDefinition(MapLegacyClassToCurrent(player.ClassId));
                    GetPlayerCollisionBounds(classDefinition, player.X, player.Y, out var left, out var top, out var right, out var bottom);
                    if (!RayBoundsMayIntersectRectangle(rayBounds, left, top, right, bottom))
                    {
                        continue;
                    }

                    var distance = GetRayIntersectionDistanceWithRectangle(
                        originX,
                        originY,
                        directionX,
                        directionY,
                        left - padding,
                        top - padding,
                        right + padding,
                        bottom + padding,
                        maxDistance);
                    if (!distance.HasValue)
                    {
                        continue;
                    }

                    nearestHit = ResolveNearestProjectileHit(nearestHit, distance.Value, originX, originY, directionX, directionY, player);
                }

                return nearestHit;
            }

            private static LegacyReplayProjectileHit ResolveNearestProjectileHit(
                LegacyReplayProjectileHit? current,
                float distance,
                float originX,
                float originY,
                float directionX,
                float directionY,
                LegacyReplayPlayer? hitPlayer)
            {
                if (current is not null && distance >= current.Distance)
                {
                    return current;
                }

                return new LegacyReplayProjectileHit(
                    originX + directionX * distance,
                    originY + directionY * distance,
                    distance,
                    hitPlayer);
            }

            private static float? GetRayIntersectionDistanceWithRectangle(
                float originX,
                float originY,
                float directionX,
                float directionY,
                float left,
                float top,
                float right,
                float bottom,
                float maxDistance)
            {
                const float epsilon = 0.0001f;
                var tMin = float.NegativeInfinity;
                var tMax = float.PositiveInfinity;

                if (MathF.Abs(directionX) < epsilon)
                {
                    if (originX < left || originX > right)
                    {
                        return null;
                    }
                }
                else
                {
                    var t1 = (left - originX) / directionX;
                    var t2 = (right - originX) / directionX;
                    tMin = MathF.Max(tMin, MathF.Min(t1, t2));
                    tMax = MathF.Min(tMax, MathF.Max(t1, t2));
                }

                if (MathF.Abs(directionY) < epsilon)
                {
                    if (originY < top || originY > bottom)
                    {
                        return null;
                    }
                }
                else
                {
                    var t3 = (top - originY) / directionY;
                    var t4 = (bottom - originY) / directionY;
                    tMin = MathF.Max(tMin, MathF.Min(t3, t4));
                    tMax = MathF.Min(tMax, MathF.Max(t3, t4));
                }

                if (tMax < 0f || tMin > tMax)
                {
                    return null;
                }

                var distance = tMin >= 0f ? tMin : tMax;
                return distance < 0f || distance > maxDistance ? null : distance;
            }

            private static bool RayBoundsMayIntersectRectangle(
                LegacyReplayRectangleHitbox rayBounds,
                float left,
                float top,
                float right,
                float bottom)
            {
                return rayBounds.Left <= right
                    && rayBounds.Right >= left
                    && rayBounds.Top <= bottom
                    && rayBounds.Bottom >= top;
            }

            private int NextTransientEntityId()
            {
                var nextId = _nextTransientEntityId;
                _nextTransientEntityId += 1;
                return nextId;
            }

            private float ResolveFacingDirection(LegacyReplayPlayer player)
            {
                var aimCos = MathF.Cos(player.AimDirectionDegrees * MathF.PI / 180f);
                if (MathF.Abs(aimCos) > 0.01f)
                {
                    var facing = MathF.Sign(aimCos);
                    _lastFacingByPlayerId[player.StablePlayerId] = facing;
                    return facing;
                }

                if (MathF.Abs(player.HorizontalSpeed) > 0.01f)
                {
                    var facing = MathF.Sign(player.HorizontalSpeed);
                    _lastFacingByPlayerId[player.StablePlayerId] = facing;
                    return facing;
                }

                if (_lastFacingByPlayerId.TryGetValue(player.StablePlayerId, out var lastFacing))
                {
                    return lastFacing;
                }

                return 1f;
            }

            private static LegacyMovementState ToLegacyMovementState(byte movementState)
            {
                return Enum.IsDefined(typeof(LegacyMovementState), movementState)
                    ? (LegacyMovementState)movementState
                    : LegacyMovementState.None;
            }

            private void EnsurePlayerCount(int count)
            {
                if (_players.Count != count)
                {
                    throw new InvalidDataException(
                        $"Legacy replay expected {count.ToString(CultureInfo.InvariantCulture)} players but translator tracks {_players.Count.ToString(CultureInfo.InvariantCulture)}.");
                }
            }

            private LegacyReplayPlayer GetPlayerAtIndex(int index)
            {
                if (index < 0 || index >= _players.Count)
                {
                    throw new InvalidDataException($"Legacy replay referenced invalid player index {index.ToString(CultureInfo.InvariantCulture)}.");
                }

                return _players[index];
            }

            private void RemovePlayerAtIndex(int index)
            {
                if (index < 0 || index >= _players.Count)
                {
                    throw new InvalidDataException($"Legacy replay referenced invalid player leave index {index.ToString(CultureInfo.InvariantCulture)}.");
                }

                _players.RemoveAt(index);
            }

            private static string ReadByteLengthPrefixedString(BinaryReader reader)
            {
                var byteLength = reader.ReadByte();
                return ReadString(reader, byteLength);
            }

            private static void ReadExpectedJoinMessageType(BinaryReader reader, byte expectedMessageType, string label)
            {
                var messageStart = reader.BaseStream.Position;
                var actualMessageType = reader.ReadByte();
                if (actualMessageType != expectedMessageType)
                {
                    throw new InvalidDataException(
                        $"Legacy replay join-state expected {label} (0x{expectedMessageType:X2}) at stream offset {messageStart.ToString(CultureInfo.InvariantCulture)} but found 0x{actualMessageType:X2}.");
                }
            }

            private static string ReadString(BinaryReader reader, int byteLength)
            {
                var bytes = reader.ReadBytes(byteLength);
                if (bytes.Length != byteLength)
                {
                    throw new InvalidDataException("Legacy replay string payload was truncated.");
                }

                return Latin1.GetString(bytes);
            }

            private static void SkipBytes(BinaryReader reader, int byteLength)
            {
                if (byteLength <= 0)
                {
                    return;
                }

                var bytes = reader.ReadBytes(byteLength);
                if (bytes.Length != byteLength)
                {
                    throw new InvalidDataException("Legacy replay payload was truncated.");
                }
            }

            private static byte ToSnapshotTeam(byte legacyTeam)
            {
                return legacyTeam switch
                {
                    LegacyTeamRed => SnapshotTeamRed,
                    LegacyTeamBlue => SnapshotTeamBlue,
                    _ => 0,
                };
            }

            private static byte ToSnapshotObjectiveTeam(byte legacyTeam)
            {
                return legacyTeam switch
                {
                    LegacyTeamRed => SnapshotTeamRed,
                    LegacyTeamBlue => SnapshotTeamBlue,
                    _ => 0,
                };
            }

            private static PlayerClass MapLegacyClassToCurrent(byte legacyClassId)
            {
                return legacyClassId switch
                {
                    LegacyClassScout => PlayerClass.Scout,
                    LegacyClassEngineer => PlayerClass.Engineer,
                    LegacyClassPyro => PlayerClass.Pyro,
                    LegacyClassSoldier => PlayerClass.Soldier,
                    LegacyClassDemoman => PlayerClass.Demoman,
                    LegacyClassHeavy => PlayerClass.Heavy,
                    LegacyClassSniper => PlayerClass.Sniper,
                    LegacyClassMedic => PlayerClass.Medic,
                    LegacyClassSpy => PlayerClass.Spy,
                    LegacyClassQuote => PlayerClass.Quote,
                    _ => PlayerClass.Scout,
                };
            }
        }

        private sealed class LegacyReplayPlayer
        {
            public LegacyReplayPlayer(int stablePlayerId, string name)
            {
                StablePlayerId = stablePlayerId;
                Name = name;
            }

            public int StablePlayerId { get; }
            public string Name { get; set; }
            public byte Team { get; set; }
            public byte ClassId { get; set; } = (byte)PlayerClass.Scout;
            public bool HasCharacter { get; set; }
            public bool HasSentry { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public float HorizontalSpeed { get; set; }
            public float VerticalSpeed { get; set; }
            public int Health { get; set; }
            public int Ammo { get; set; }
            public short Kills { get; set; }
            public short Deaths { get; set; }
            public short Caps { get; set; }
            public short Assists { get; set; }
            public short HealPoints { get; set; }
            public float Points { get; set; }
            public bool QueueJump { get; set; }
            public byte KeyState { get; set; }
            public float AimDirectionDegrees { get; set; }
            public float AimDistance { get; set; }
            public bool IsSpyCloaked { get; set; }
            public float SpyCloakAlpha { get; set; }
            public byte MovementState { get; set; }
            public byte AnimationOffset { get; set; }
            public bool Intel { get; set; }
            public int IntelRechargeTicks { get; set; }
            public bool IsScoped { get; set; }
            public int SniperChargeTicks { get; set; }
            public bool WeaponReadyToShoot { get; set; }
            public int WeaponCooldownTicks { get; set; }
            public int WeaponAuxTicks { get; set; }
            public int PyroGas { get; set; }
            public int HeavyBullets { get; set; }
            public int BladePower { get; set; }
            public bool MedicIsHealing { get; set; }
            public float Metal { get; set; }
            public int MedicUberScaled { get; set; }
            public int MedicHealTargetPlayerId { get; set; } = -1;
            public int ChatBubbleFrameIndex { get; set; }
            public int ChatBubbleTicksRemaining { get; set; }
            public float SentryX { get; set; }
            public float SentryY { get; set; }
            public float SentryFacingDirectionX { get; set; } = 1f;
            public float SentryVerticalSpeed { get; set; }
            public bool SentryBuilt { get; set; }
            public int SentryHealth { get; set; }
            public int AirJumpsUsed { get; set; }
            public bool WasJumpHeldLastTick { get; set; }
            public List<LegacyReplayMineState> Mines { get; } = new();
        }

        private sealed class LegacyReplayKillFeedEntry
        {
            public LegacyReplayKillFeedEntry(SnapshotKillFeedEntry entry, int ticksRemaining)
            {
                Entry = entry;
                TicksRemaining = ticksRemaining;
            }

            public SnapshotKillFeedEntry Entry { get; }
            public int TicksRemaining { get; set; }
        }

        private sealed class LegacyReplayCombatTrace
        {
            public LegacyReplayCombatTrace(
                float startX,
                float startY,
                float endX,
                float endY,
                int ticksRemaining,
                bool hitCharacter,
                byte team,
                bool isSniperTracer)
            {
                StartX = startX;
                StartY = startY;
                EndX = endX;
                EndY = endY;
                TicksRemaining = ticksRemaining;
                HitCharacter = hitCharacter;
                Team = team;
                IsSniperTracer = isSniperTracer;
            }

            public float StartX { get; }
            public float StartY { get; }
            public float EndX { get; }
            public float EndY { get; }
            public int TicksRemaining { get; set; }
            public bool HitCharacter { get; }
            public byte Team { get; }
            public bool IsSniperTracer { get; }
        }

        private readonly record struct LegacyReplayRectangleHitbox(float Left, float Top, float Right, float Bottom);

        private sealed class LegacyReplayProjectileHit
        {
            public LegacyReplayProjectileHit(float hitX, float hitY, float distance, LegacyReplayPlayer? hitPlayer)
            {
                HitX = hitX;
                HitY = hitY;
                Distance = distance;
                HitPlayer = hitPlayer;
            }

            public float HitX { get; }
            public float HitY { get; }
            public float Distance { get; }
            public LegacyReplayPlayer? HitPlayer { get; }
        }

        private sealed class LegacyReplayProjectileState
        {
            public LegacyReplayProjectileState(
                int id,
                byte team,
                int ownerId,
                float x,
                float y,
                float velocityX,
                float velocityY,
                int ticksRemaining)
            {
                Id = id;
                Team = team;
                OwnerId = ownerId;
                X = x;
                Y = y;
                VelocityX = velocityX;
                VelocityY = velocityY;
                TicksRemaining = ticksRemaining;
            }

            public int Id { get; }
            public byte Team { get; }
            public int OwnerId { get; }
            public float X { get; set; }
            public float Y { get; set; }
            public float VelocityX { get; set; }
            public float VelocityY { get; set; }
            public int TicksRemaining { get; set; }
        }

        private sealed class LegacyReplayRocketState
        {
            public LegacyReplayRocketState(
                int id,
                byte team,
                int ownerId,
                float x,
                float y,
                float directionRadians,
                float speed,
                int ticksRemaining)
            {
                Id = id;
                Team = team;
                OwnerId = ownerId;
                X = x;
                Y = y;
                PreviousX = x;
                PreviousY = y;
                DirectionRadians = directionRadians;
                Speed = speed;
                TicksRemaining = ticksRemaining;
            }

            public int Id { get; }
            public byte Team { get; }
            public int OwnerId { get; }
            public float X { get; set; }
            public float Y { get; set; }
            public float PreviousX { get; set; }
            public float PreviousY { get; set; }
            public float DirectionRadians { get; }
            public float Speed { get; set; }
            public int TicksRemaining { get; set; }
        }

        private sealed class LegacyReplayFlameState
        {
            public LegacyReplayFlameState(
                int id,
                byte team,
                int ownerId,
                float x,
                float y,
                float velocityX,
                float velocityY,
                int ticksRemaining)
            {
                Id = id;
                Team = team;
                OwnerId = ownerId;
                X = x;
                Y = y;
                PreviousX = x;
                PreviousY = y;
                VelocityX = velocityX;
                VelocityY = velocityY;
                TicksRemaining = ticksRemaining;
            }

            public int Id { get; }
            public byte Team { get; }
            public int OwnerId { get; }
            public float X { get; set; }
            public float Y { get; set; }
            public float PreviousX { get; set; }
            public float PreviousY { get; set; }
            public float VelocityX { get; set; }
            public float VelocityY { get; set; }
            public int TicksRemaining { get; set; }
        }

        private sealed class LegacyReplayMineState
        {
            public int Id { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public float HorizontalSpeed { get; set; }
            public float VerticalSpeed { get; set; }
            public bool IsStickied { get; set; }
            public bool IsTransient { get; set; }
            public int TicksRemaining { get; set; }
        }

        private sealed class LegacyReplayIntelState
        {
            public LegacyReplayIntelState(byte team)
            {
                Team = team;
            }

            public byte Team { get; }
            public float BaseX { get; private set; }
            public float BaseY { get; private set; }
            public bool HasBasePosition { get; private set; }
            public float X { get; private set; }
            public float Y { get; private set; }
            public int ReturnTicksRemaining { get; private set; }
            public bool ExistsOnField { get; private set; } = true;
            public int CarrierPlayerId { get; private set; } = -1;

            public void UpdateFieldState(float x, float y, int returnTicksRemaining)
            {
                if (!HasBasePosition && returnTicksRemaining <= 0)
                {
                    BaseX = x;
                    BaseY = y;
                    HasBasePosition = true;
                }

                X = x;
                Y = y;
                ReturnTicksRemaining = Math.Max(0, returnTicksRemaining);
                ExistsOnField = true;
                CarrierPlayerId = -1;
            }

            public void MarkMissing()
            {
                ExistsOnField = false;
            }

            public void MarkCarried(LegacyReplayPlayer carrier)
            {
                ExistsOnField = false;
                CarrierPlayerId = carrier.StablePlayerId;
                X = carrier.X;
                Y = carrier.Y;
                ReturnTicksRemaining = 0;
            }

            public void MarkDropped(float x, float y, int returnTicksRemaining)
            {
                ExistsOnField = true;
                CarrierPlayerId = -1;
                X = x;
                Y = y;
                ReturnTicksRemaining = Math.Max(0, returnTicksRemaining);
            }

            public void MarkReturnedToBase()
            {
                ExistsOnField = true;
                CarrierPlayerId = -1;
                X = BaseX;
                Y = BaseY;
                ReturnTicksRemaining = 0;
            }

            public SnapshotIntelState ToSnapshotState()
            {
                var isAtBase = HasBasePosition
                    && CarrierPlayerId < 0
                    && ExistsOnField
                    && ReturnTicksRemaining <= 0
                    && Math.Abs(X - BaseX) < 0.01f
                    && Math.Abs(Y - BaseY) < 0.01f;
                var isDropped = CarrierPlayerId < 0 && ExistsOnField && !isAtBase;
                return new SnapshotIntelState(Team, X, Y, isAtBase, isDropped, ReturnTicksRemaining);
            }
        }

        private sealed record LegacyReplayControlPointState(
            int Index,
            RoomObjectMarker Marker,
            byte TeamLegacy,
            byte CappingTeamLegacy,
            ushort CappingTicks,
            int CapTimeTicks,
            bool IsLocked);

        internal sealed record ReplayTranslationTimeline(List<ScheduledReplayPayload> Payloads);
    }
}
