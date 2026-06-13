using System.Diagnostics.CodeAnalysis;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private readonly RuntimeController _runtimeController;
    private readonly RuntimeQueryController _runtimeQueryController;
    public const int MaxPlayableNetworkPlayers = 20;
    public const byte LocalPlayerSlot = 1;
    public const byte FirstSpectatorSlot = 128;
    public static IReadOnlyList<byte> NetworkPlayerSlots { get; } = Enumerable.Range(1, MaxPlayableNetworkPlayers).Select(static value => (byte)value).ToArray();
    private const string DefaultLocalPlayerName = "Player 1";
    private const string DefaultEnemyPlayerName = "Player 2";
    private const string DefaultFriendlyDummyName = "Player 3";
    private const int DefaultRespawnSeconds = 5;
    private const int DefaultTimeLimitMinutes = 15;
    private const int DefaultCapLimit = 5;
    private const int DefaultTeamDeathmatchKillLimit = 30;
    private const int ArenaPointCapTimeTicksDefault = 300;
    private const int ArenaPointUnlockTicksDefault = 1800;
    private const int PendingMapChangeTicks = 300;
    private const string ClassChangeKillFeedSuffix = " bid farewell, cruel world!";
    private const int CombatTraceLifetimeTicks = 3;
    private const int KillFeedLifetimeTicks = 150;
    private const int KillFeedLocalInvolvedLifetimeTicks = 300;
    private const int DefaultGibLevel = 3;
    private const int LocalProjectileTerminationSuppressionTicks = 12;
    private const int NetworkProjectileRemovalSuppressionTicks = 180;
    private readonly Dictionary<int, SimulationEntity> _entities = new();
    private readonly List<CombatTrace> _combatTraces = new();
    private readonly List<SniperAimIndicator> _sniperAimIndicators = new();
    private readonly List<KillFeedEntry> _killFeed = new();
    private readonly List<ShotProjectileEntity> _shots = new();
    private readonly List<BubbleProjectileEntity> _bubbles = new();
    private readonly List<BladeProjectileEntity> _blades = new();
    private readonly List<NeedleProjectileEntity> _needles = new();
    private readonly List<RevolverProjectileEntity> _revolverShots = new();
    private readonly List<StabAnimEntity> _stabAnimations = new();
    private readonly List<StabMaskEntity> _stabMasks = new();
    private readonly List<FlameProjectileEntity> _flames = new();
    private readonly List<FlareProjectileEntity> _flares = new();
    private readonly List<RocketProjectileEntity> _rockets = new();
    private readonly List<int> _pendingNewRocketIds = new();
    private readonly List<MineProjectileEntity> _mines = new();
    private readonly List<GrenadeProjectileEntity> _grenades = new();
    private readonly List<SentryEntity> _sentries = new();
    private readonly List<JumpPadEntity> _jumpPads = new();
    private readonly List<CivilDefenseTurretEntity> _civilDefenseTurrets = new();
    private readonly List<PlayerGibEntity> _playerGibs = new();
    private readonly List<BloodDropEntity> _bloodDrops = new();
    private readonly List<HealthPackEntity> _healthPacks = new();
    private readonly CivvieMoneyTrailTracker _civvieMoneyTrailTracker = new();
    private readonly List<DroppedWeaponEntity> _droppedWeapons = new();
    private readonly List<DeadBodyEntity> _deadBodies = new();
    private readonly List<SentryGibEntity> _sentryGibs = new();
    private readonly List<JumpPadGibEntity> _jumpPadGibs = new();
    private readonly List<MovingPlatformRuntimeState> _movingPlatforms = new();
    private readonly List<GeneratorState> _generators = new();
    private readonly List<WorldSoundEvent> _pendingSoundEvents = new();
    private readonly List<WorldVisualEvent> _pendingVisualEvents = new();
    private readonly List<WorldDamageEvent> _pendingDamageEvents = new();
    private readonly List<WorldGibSpawnEvent> _pendingGibSpawnEvents = new();
    private readonly List<WorldRocketSpawnEvent> _pendingRocketSpawnEvents = new();
    private readonly List<WorldHealingEvent> _pendingHealingEvents = new();
    private readonly Queue<DangerCloseExplosionRequest> _pendingDangerCloseExplosions = new();
    private readonly List<PlayerEntity> _remoteSnapshotPlayers = new();
    private readonly List<PlayerEntity> _remoteSnapshotScoreboardPlayers = new();
    private readonly List<ScoreboardSpectatorEntry> _spectators = new();
    private readonly Dictionary<byte, PlayerEntity> _remoteSnapshotPlayersBySlot = new();
    private readonly HashSet<int> _snapshotSeenEntityIds = new();
    private readonly List<int> _snapshotStaleEntityIds = new();
    private readonly HashSet<ulong> _processedNetworkGibSpawnEventIds = new();
    private readonly HashSet<ulong> _processedImmediateNetworkRocketSpawnEventIds = new();
    private readonly Dictionary<int, int> _presentedNetworkGibDeathCountsByPlayerId = new();
    private readonly HashSet<byte> _snapshotSeenRemotePlayerSlots = new();
    private readonly List<byte> _snapshotStaleRemotePlayerSlots = new();
    private readonly HashSet<byte> _remoteSnapshotAwaitingJoinSlots = new();
    private readonly HashSet<int> _remoteSnapshotAwaitingJoinPlayerIds = new();
    private readonly Dictionary<byte, PlayerEntity> _additionalNetworkPlayersBySlot = new();
    private readonly Dictionary<int, PlayerEntity> _activeNetworkPlayersById = new();
    private readonly Dictionary<int, byte> _networkPlayerSlotsByPlayerId = new();
    private readonly HashSet<byte> _enabledAdditionalNetworkPlayerSlots = new();
    private readonly Dictionary<byte, CharacterClassDefinition> _additionalNetworkPlayerClassDefinitions = new();
    private readonly Dictionary<byte, PlayerInputSnapshot> _additionalNetworkPlayerInputs = new();
    private readonly Dictionary<byte, PlayerInputSnapshot> _additionalNetworkPlayerPreviousInputs = new();
    private readonly Dictionary<byte, bool> _additionalNetworkPlayerAwaitingJoin = new();
    private readonly Dictionary<byte, int> _additionalNetworkPlayerRespawnTicks = new();
    private readonly Dictionary<byte, int> _networkPlayerPingMillisecondsBySlot = new();
    private readonly Dictionary<byte, PlayerTeam> _additionalNetworkPlayerTeams = new();
    private readonly HashSet<byte> _pendingNetworkPlayerTeamSelections = new();
    private readonly Dictionary<byte, SpawnPoint> _networkPlayerSpawnOverrides = new();
    private readonly Dictionary<byte, float> _networkPlayerMovementSpeedScaleOverrides = new();
    private readonly Dictionary<byte, float> _networkPlayerGravityScaleOverrides = new();
    private readonly Dictionary<byte, int> _networkPlayerMaxHealthOverrides = new();
    private readonly Dictionary<byte, LocalDeathCamState> _networkPlayerDeathCams = new();
    private readonly HashSet<int> _lastToDieDroneSentryIds = new();
    private readonly HashSet<int> _clientPredictedProjectileIds = new();
    private int? _authoritativeLocalPlayerId;
    private readonly HashSet<int> _terminatedProjectileIds = new();
    private readonly Dictionary<int, long> _terminatedProjectileExpiryFrames = new();
    private readonly ClientSnapshotStringCache _snapshotStringCache = new();
    private readonly Random _random = new(1337);
    private int _configuredTimeLimitMinutes = DefaultTimeLimitMinutes;
    private int _configuredCapLimit = DefaultCapLimit;
    private int _configuredRespawnSeconds = DefaultRespawnSeconds;
    private int _configuredRespawnTicks = DefaultRespawnSeconds * SimulationConfig.DefaultTicksPerSecond;
    private float _configuredPlayerScale = 1f;
    private float _configuredMapScale = 1f;
    private float _configuredMovementSpeedScale = 1f;
    private float _configuredProjectileSpeedScale = 1f;
    private float _configuredDamageScale = 1f;
    private float _configuredGravityScale = 1f;
    private float _configuredHorizontalSpeedClampPerTick = LegacyMovementModel.MaxStepSpeedPerTick;
    private float _configuredVerticalSpeedClampPerTick = LegacyMovementModel.MaxStepSpeedPerTick;
    private bool _roundEndFriendlyFireEnabled;
    private readonly Dictionary<int, int> _deterministicSpreadShotIndexByPlayerId = new();
    private CharacterClassDefinition _localPlayerClassDefinition = CharacterClassCatalog.Scout;
    private CharacterClassDefinition _enemyDummyClassDefinition = CharacterClassCatalog.Scout;
    private readonly CharacterClassDefinition _friendlyDummyClassDefinition = CharacterClassCatalog.Heavy;
    private PlayerTeam _enemyDummyTeam = PlayerTeam.Blue;
    private PlayerInputSnapshot _localInput;
    private PlayerInputSnapshot _previousLocalInput;
    private PlayerInputSnapshot _enemyInput;
    private PlayerInputSnapshot _previousEnemyInput;
    private bool _enemyInputOverrideActive;
    private int _enemyDummyRespawnTicks;
    private int _nextEntityId = 1;
    private int _nextRedSpawnIndex;
    private int _nextBlueSpawnIndex;
    private int _enemyStrafeDirection = -1;
    private int _enemyStrafeTicksRemaining;
    private readonly List<int> _killFeedEntryLifetimes = new();
    private ulong _nextKillFeedEventId = 1;
    private long _lastKillFeedRecordedFrame = -1;
    private int _pendingMapChangeTicks = -1;
    private bool _mapChangeReady;
    private bool _autoRestartOnMapChange = true;
    private bool _localPlayerAwaitingJoin;
    private PlayerTeam? _arenaPointTeam;
    private PlayerTeam? _arenaCappingTeam;
    private float _arenaCappingTicks;
    private int _arenaCappers;
    private int _arenaUnlockTicksRemaining;
    private int _arenaRedConsecutiveWins;
    private int _arenaBlueConsecutiveWins;
    private bool _processingDangerCloseExplosions;
    private readonly List<ControlPointState> _controlPoints = new();
    private readonly List<ControlPointZone> _controlPointZones = new();
    private bool _controlPointSetupMode;
    private int _controlPointSetupTicksRemaining;
    private readonly Dictionary<PlayerTeam, byte> _vipSlotsByTeam = new();
    private readonly Dictionary<PlayerTeam, byte> _preferredVipSlotsByTeam = new();
    private bool _practiceVipRulesEnabled;
    private int _vipWarmupTicksRemaining;
    private int _vipAssignmentVersion;
    private int _vipRoundStartVersion;

    public long Frame { get; private set; }

    public double SimulationTimeSeconds => Frame * Config.FixedDeltaSeconds;

    public SimulationConfig Config { get; }

    public IReadOnlyDictionary<int, SimulationEntity> Entities => _entities;

    public SimpleLevel Level { get; private set; }

    public WorldBounds Bounds => Level.Bounds;

    public PlayerEntity LocalPlayer { get; }

    public PlayerTeam LocalPlayerTeam { get; private set; } = PlayerTeam.Red;

    public PlayerEntity EnemyPlayer { get; }

    public PlayerEntity FriendlyDummy { get; }

    public int RedCaps { get; private set; }

    public int BlueCaps { get; private set; }

    public int SpectatorCount { get; private set; }

    public IReadOnlyList<ScoreboardSpectatorEntry> Spectators => _spectators;

    public MatchRules MatchRules { get; private set; }

    public MatchState MatchState { get; private set; }

    public ExperimentalGameplaySettings ExperimentalGameplaySettings { get; private set; } = new();

    public bool RandomSpreadEnabled { get; set; } = true;

    public bool SniperAimIndicatorEnabled { get; set; } = true;

    /// <summary>
    /// When true, only the local player and projectiles are simulated.
    /// Other network players, match logic, and structures are skipped.
    /// Used for client-side prediction in multiplayer.
    /// </summary>
    public bool ClientPredictionMode { get; set; }

    public bool LocalGoreEffectsEnabled { get; set; } = true;

    public int GetDeterministicSpreadShotIndex(int attackerId)
    {
        var index = _deterministicSpreadShotIndexByPlayerId.TryGetValue(attackerId, out var currentIndex)
            ? currentIndex
            : 0;
        _deterministicSpreadShotIndexByPlayerId[attackerId] = index + 1;
        return index;
    }

    public int MapChangeTicksRemaining => _pendingMapChangeTicks;

    public bool IsMapChangePending => _pendingMapChangeTicks >= 0;

    public bool IsMapChangeReady => _mapChangeReady;

    public bool AutoRestartOnMapChange
    {
        get => _autoRestartOnMapChange;
        set => _autoRestartOnMapChange = value;
    }

    public int ConfiguredRespawnSeconds => _configuredRespawnSeconds;

    public float ConfiguredPlayerScale => _configuredPlayerScale;

    public float ConfiguredMapScale => _configuredMapScale;

    public float ConfiguredMovementSpeedScale => _configuredMovementSpeedScale;

    public float ConfiguredProjectileSpeedScale => _configuredProjectileSpeedScale;

    public float ConfiguredDamageScale => _configuredDamageScale;

    public float ConfiguredGravityScale => _configuredGravityScale;

    public float ConfiguredHorizontalSpeedClampPerTick => _configuredHorizontalSpeedClampPerTick;

    public float ConfiguredVerticalSpeedClampPerTick => _configuredVerticalSpeedClampPerTick;

    public bool RoundEndFriendlyFireEnabled => _roundEndFriendlyFireEnabled;

    public int LocalPlayerRespawnTicks { get; private set; }

    public bool LocalPlayerAwaitingJoin => _localPlayerAwaitingJoin;

    public LocalDeathCamState? LocalDeathCam { get; private set; }

    public IReadOnlyList<KillFeedEntry> KillFeed => _killFeed;

    public IReadOnlyList<CombatTrace> CombatTraces => _combatTraces;

    public IReadOnlyList<SniperAimIndicator> SniperAimIndicators => _sniperAimIndicators;

    public IReadOnlyList<ShotProjectileEntity> Shots => _shots;

    public IReadOnlyList<BubbleProjectileEntity> Bubbles => _bubbles;

    public IReadOnlyList<BladeProjectileEntity> Blades => _blades;

    public IReadOnlyList<NeedleProjectileEntity> Needles => _needles;

    public IReadOnlyList<RevolverProjectileEntity> RevolverShots => _revolverShots;

    public IReadOnlyList<StabAnimEntity> StabAnimations => _stabAnimations;

    public IReadOnlyList<StabMaskEntity> StabMasks => _stabMasks;

    public IReadOnlyList<FlameProjectileEntity> Flames => _flames;

    public IReadOnlyList<FlareProjectileEntity> Flares => _flares;

    public IReadOnlyList<RocketProjectileEntity> Rockets => _rockets;

    public IReadOnlyList<MineProjectileEntity> Mines => _mines;

    public IReadOnlyList<GrenadeProjectileEntity> Grenades => _grenades;

    public IReadOnlyList<SentryEntity> Sentries => _sentries;

    public IReadOnlyList<JumpPadEntity> JumpPads => _jumpPads;

    public IReadOnlyList<CivilDefenseTurretEntity> CivilDefenseTurrets => _civilDefenseTurrets;

    public IReadOnlyList<MovingPlatformRuntimeState> MovingPlatforms => _movingPlatforms;

    public IReadOnlyList<PlayerGibEntity> PlayerGibs => _playerGibs;

    public IReadOnlyList<BloodDropEntity> BloodDrops => _bloodDrops;

    public IReadOnlyList<HealthPackEntity> HealthPacks => _healthPacks;

    public IReadOnlyList<DroppedWeaponEntity> DroppedWeapons => _droppedWeapons;

    public IReadOnlyList<DeadBodyEntity> DeadBodies => _deadBodies;

    public IReadOnlyList<SentryGibEntity> SentryGibs => _sentryGibs;

    public IReadOnlyList<JumpPadGibEntity> JumpPadGibs => _jumpPadGibs;

    public IReadOnlyList<WorldSoundEvent> PendingSoundEvents => _pendingSoundEvents;

    public IReadOnlyList<WorldVisualEvent> PendingVisualEvents => _pendingVisualEvents;

    public IReadOnlyList<WorldDamageEvent> PendingDamageEvents => _pendingDamageEvents;

    public IReadOnlyList<WorldRocketSpawnEvent> PendingRocketSpawnEvents => _pendingRocketSpawnEvents;

    public IReadOnlyList<WorldHealingEvent> PendingHealingEvents => _pendingHealingEvents;

    public IReadOnlyList<PlayerEntity> RemoteSnapshotPlayers => _remoteSnapshotPlayers;

    public IReadOnlyList<PlayerEntity> RemoteSnapshotScoreboardPlayers => _remoteSnapshotScoreboardPlayers;

    public IReadOnlySet<byte> RemoteSnapshotAwaitingJoinSlots => _remoteSnapshotAwaitingJoinSlots;

    public bool EnemyPlayerEnabled { get; private set; } = true;

    public bool IsRemoteSnapshotPlayerAwaitingJoin(PlayerEntity player)
        => _remoteSnapshotAwaitingJoinPlayerIds.Contains(player.Id);

    public bool FriendlyDummyEnabled { get; private set; }

    public LocalDeathCamState? GetNetworkPlayerDeathCam(byte slot)
    {
        LocalDeathCamState? deathCam;
        if (slot == LocalPlayerSlot)
        {
            deathCam = LocalDeathCam;
        }
        else
        {
            deathCam = _networkPlayerDeathCams.GetValueOrDefault(slot);
        }

        return deathCam is null ? null : ResolveTrackedDeathCamFocus(deathCam);
    }

    public PlayerTeam? ArenaPointTeam => _arenaPointTeam;

    public PlayerTeam? ArenaCappingTeam => _arenaCappingTeam;

    public float ArenaCappingTicks => _arenaCappingTicks;

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Kept as an instance property to preserve the public simulation API.")]
    public int ArenaPointCapTimeTicks => ArenaPointCapTimeTicksDefault;

    public int ArenaCappers => _arenaCappers;

    public int ArenaUnlockTicksRemaining => _arenaUnlockTicksRemaining;

    public bool ArenaPointLocked => MatchRules.Mode == GameModeKind.Arena && _arenaUnlockTicksRemaining > 0;

    public int ArenaRedConsecutiveWins => _arenaRedConsecutiveWins;

    public int ArenaBlueConsecutiveWins => _arenaBlueConsecutiveWins;

    public int ArenaRedAliveCount => CountAlivePlayers(PlayerTeam.Red);

    public int ArenaBlueAliveCount => CountAlivePlayers(PlayerTeam.Blue);

    public int ArenaRedPlayerCount => CountPlayers(PlayerTeam.Red);

    public int ArenaBluePlayerCount => CountPlayers(PlayerTeam.Blue);

    public bool IsPlayerHumiliated(PlayerEntity player)
    {
        if (IsPracticeDummy(player))
        {
            return true;
        }

        if (IsExperimentalRageHumiliationActiveForPlayer(player))
        {
            return true;
        }

        if (!MatchState.IsEnded)
        {
            return false;
        }

        return !MatchState.WinnerTeam.HasValue || player.Team != MatchState.WinnerTeam.Value;
    }

    public IReadOnlyList<ControlPointState> ControlPoints => _controlPoints;

    public bool ControlPointSetupActive => _controlPointSetupMode && _controlPointSetupTicksRemaining > 0;

    public int ControlPointSetupTicksRemaining => _controlPointSetupTicksRemaining;

    public SimulationWorld(SimulationConfig? config = null)
    {
        _runtimeController = new RuntimeController(this);
        _runtimeQueryController = new RuntimeQueryController(this);
        Config = config ?? new SimulationConfig();
        Level = SimpleLevelFactory.CreateScoutPrototypeLevel(_configuredMapScale);
        RedIntel = CreateIntelState(PlayerTeam.Red);
        BlueIntel = CreateIntelState(PlayerTeam.Blue);
        MatchRules = CreateDefaultMatchRules(Level.Mode);
        MatchState = CreateInitialMatchState(MatchRules);
        LocalPlayer = new PlayerEntity(AllocateEntityId(), _localPlayerClassDefinition, DefaultLocalPlayerName);
        LocalPlayer.SetPlayerScale(_configuredPlayerScale);
        ApplyServerGameplayTuning(LocalPlayerSlot, LocalPlayer);
        var initialSpawn = ReserveSpawn(LocalPlayer, LocalPlayerTeam);
        SpawnPlayerResolved(LocalPlayer, LocalPlayerTeam, initialSpawn);
        _entities.Add(LocalPlayer.Id, LocalPlayer);
        _activeNetworkPlayersById[LocalPlayer.Id] = LocalPlayer;
        _networkPlayerSlotsByPlayerId[LocalPlayer.Id] = LocalPlayerSlot;
        EnemyPlayer = new PlayerEntity(AllocateEntityId(), _enemyDummyClassDefinition, DefaultEnemyPlayerName);
        EnemyPlayer.SetPlayerScale(_configuredPlayerScale);
        ApplyServerGameplayTuning(slot: 0, EnemyPlayer);
        if (Config.EnableLocalDummies && Config.EnableEnemyTrainingDummy)
        {
            var enemySpawn = ReserveSpawn(EnemyPlayer, _enemyDummyTeam);
            SpawnPlayerResolved(EnemyPlayer, _enemyDummyTeam, enemySpawn);
            EnemyPlayerEnabled = true;
        }
        else
        {
            EnemyPlayerEnabled = false;
            EnemyPlayer.Kill();
        }
        _entities.Add(EnemyPlayer.Id, EnemyPlayer);
        FriendlyDummy = new PlayerEntity(AllocateEntityId(), _friendlyDummyClassDefinition, DefaultFriendlyDummyName);
        FriendlyDummy.SetPlayerScale(_configuredPlayerScale);
        ApplyServerGameplayTuning(slot: 0, FriendlyDummy);
        FriendlyDummy.Kill();
        _entities.Add(FriendlyDummy.Id, FriendlyDummy);
    }

    public void ConfigureExperimentalGameplaySettings(ExperimentalGameplaySettings settings)
    {
        ExperimentalGameplaySettings = settings ?? new ExperimentalGameplaySettings();
        if (!ExperimentalGameplaySettings.EnableRage)
        {
            _experimentalRageEnemyHumiliationTicksRemaining = 0;
            LocalPlayer.ClearRageState();
        }

        if (!ExperimentalGameplaySettings.EnableEnemyHealthPackDrops)
        {
            ClearHealthPacks();
        }
        if (!ExperimentalGameplaySettings.EnableEnemyDroppedWeapons)
        {
            ClearDroppedWeapons();
        }

        SyncExperimentalGameplayLoadouts();
    }

    private void SyncExperimentalGameplayLoadouts()
    {
        for (var index = 0; index < NetworkPlayerSlots.Count; index += 1)
        {
            var slot = NetworkPlayerSlots[index];
            if (IsNetworkPlayerEnabled(slot) && TryGetNetworkPlayer(slot, out var player))
            {
                SyncExperimentalGameplayLoadout(slot, player);
            }
        }
    }


    public void SetLocalHealth(int health)
    {
        if (health <= 0)
        {
            ForceKillLocalPlayer();
            return;
        }

        if (!LocalPlayer.IsAlive)
        {
            ForceRespawnLocalPlayer();
        }

        LocalPlayer.ForceSetHealth(health);
    }

    public void SetLocalAmmo(int shells)
    {
        LocalPlayer.ForceSetAmmo(shells);
    }

    public void TeleportLocalPlayer(float x, float y)
    {
        if (!LocalPlayer.IsAlive)
        {
            ForceRespawnLocalPlayer();
        }

        LocalPlayer.TeleportTo(
            Bounds.ClampX(x, LocalPlayer.Width),
            Bounds.ClampY(y, LocalPlayer.Height));
    }

    public string GetImportSummary()
    {
        return $"level={Level.Name} imported={Level.ImportedFromSource} bounds={Bounds.Width}x{Bounds.Height} redSpawns={Level.RedSpawns.Count} blueSpawns={Level.BlueSpawns.Count} intelBases={Level.IntelBases.Count} roomObjects={Level.RoomObjects.Count} solids={Level.Solids.Count} unsupported={Level.UnsupportedSourceEntities.Count}";
    }

    public string GetEngineerSummary()
    {
        return $"class={LocalPlayer.ClassName} metal={LocalPlayer.Metal:F1}/{LocalPlayer.MaxMetal:F1} sentries={_sentries.Count} gibs={_sentryGibs.Count}";
    }

    public bool TrySetLocalClass(PlayerClass playerClass)
    {
        return CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(playerClass, out var binding)
            && TrySetLocalClass(binding.ClassId);
    }

    public bool TrySetLocalClass(string gameplayClassId)
    {
        var definition = CharacterClassCatalog.GetDefinition(gameplayClassId);
        if (string.Equals(definition.GameplayClassId, GetNetworkPlayerClassDefinition(LocalPlayerSlot).GameplayClassId, StringComparison.Ordinal))
        {
            // Allow same-class selection to commit a pending team swap.
            // Use TryApplyNetworkPlayerClassChange so spawn-room and respawn-timer rules are respected.
            return (LocalPlayer.Team != GetNetworkPlayerConfiguredTeam(LocalPlayerSlot)
                    || HasPendingNetworkPlayerTeamSelection(LocalPlayerSlot))
                && TryApplyNetworkPlayerClassChange(LocalPlayerSlot, definition);
        }

        return TryApplyNetworkPlayerClassChange(LocalPlayerSlot, definition);
    }

    public bool TrySetEnemyClass(PlayerClass playerClass)
    {
        if (!CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(playerClass, out var binding))
        {
            return false;
        }

        var definition = CharacterClassCatalog.GetDefinition(binding.ClassId);
        var wasPracticeCombatDummy = _practiceCombatDummyMode != PracticeCombatDummyMode.None;
        if (definition.Id == _enemyDummyClassDefinition.Id && !wasPracticeCombatDummy)
        {
            return false;
        }

        DisablePracticeCombatDummyMode(resetStats: true);
        _enemyDummyClassDefinition = definition;
        EnemyPlayer.SetClassDefinition(definition);
        if (EnemyPlayerEnabled)
        {
            var spawn = ReserveSpawn(EnemyPlayer, _enemyDummyTeam);
            SpawnPlayerResolved(EnemyPlayer, _enemyDummyTeam, spawn);
        }
        return true;
    }

    public IReadOnlyList<WorldSoundEvent> DrainPendingSoundEvents()
    {
        if (_pendingSoundEvents.Count == 0)
        {
            return [];
        }

        var sounds = _pendingSoundEvents.ToArray();
        _pendingSoundEvents.Clear();
        return sounds;
    }

    public IReadOnlyList<WorldVisualEvent> DrainPendingVisualEvents()
    {
        if (_pendingVisualEvents.Count == 0)
        {
            return [];
        }

        var visuals = _pendingVisualEvents.ToArray();
        _pendingVisualEvents.Clear();
        return visuals;
    }

    public IReadOnlyList<WorldDamageEvent> DrainPendingDamageEvents()
    {
        if (_pendingDamageEvents.Count == 0)
        {
            return [];
        }

        var damageEvents = _pendingDamageEvents.ToArray();
        _pendingDamageEvents.Clear();
        return damageEvents;
    }

    public IReadOnlyList<WorldGibSpawnEvent> DrainPendingGibSpawnEvents()
    {
        if (_pendingGibSpawnEvents.Count == 0)
        {
            return [];
        }

        var gibSpawnEvents = _pendingGibSpawnEvents.ToArray();
        _pendingGibSpawnEvents.Clear();
        return gibSpawnEvents;
    }

    public IReadOnlyList<WorldRocketSpawnEvent> DrainPendingRocketSpawnEvents()
    {
        if (_pendingRocketSpawnEvents.Count == 0)
        {
            return [];
        }

        var rocketSpawnEvents = _pendingRocketSpawnEvents.ToArray();
        _pendingRocketSpawnEvents.Clear();
        return rocketSpawnEvents;
    }

    public IReadOnlyList<WorldHealingEvent> DrainPendingHealingEvents()
    {
        if (_pendingHealingEvents.Count == 0)
        {
            return [];
        }

        var healingEvents = _pendingHealingEvents.ToArray();
        _pendingHealingEvents.Clear();
        return healingEvents;
    }

    private int AllocateEntityId()
    {
        return _nextEntityId++;
    }

    private MatchRules CreateDefaultMatchRules(GameModeKind mode)
    {
        var timeLimitTicks = _configuredTimeLimitMinutes * Config.TicksPerSecond * 60;
        var capLimit = mode == GameModeKind.TeamDeathmatch && _configuredCapLimit == DefaultCapLimit
            ? DefaultTeamDeathmatchKillLimit
            : _configuredCapLimit;
        return new MatchRules(mode, _configuredTimeLimitMinutes, timeLimitTicks, capLimit);
    }

    private static MatchState CreateInitialMatchState(MatchRules rules)
    {
        return new MatchState(MatchPhase.Running, rules.TimeLimitTicks, null);
    }

    private void SyncExperimentalGameplayLoadout(byte slot, PlayerEntity player)
    {
        var specialAbilitiesEnabled = ExperimentalGameplaySettings.EnableSecondaryAbilities;
        if (slot != LocalPlayerSlot)
        {
            player.SetExperimentalDemoknightEnabled(false);
            player.SetExperimentalPassiveMovementSpeedMultiplier(global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultPassiveMovementSpeedMultiplier);
            player.SetExperimentalJumpHeightMultiplier(global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultPassiveJumpHeightMultiplier);
            player.SetExperimentalBonusAirJumps(0);
            player.SetExperimentalDemoknightSwordRangeMultiplier(global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultDemoknightSwordRangeMultiplier);
            player.SetExperimentalDemoknightSwordBaseDamage(global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultDemoknightSwordBaseDamage);
            player.SetExperimentalDemoknightSwordDamageMultiplier(global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultDemoknightSwordDamageMultiplier);
            player.SetExperimentalDemoknightSwordCooldownMultiplier(global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultDemoknightSwordCooldownMultiplier);
            player.SetExperimentalDemoknightChargeRechargeMultiplier(global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultDemoknightChargeRechargeMultiplier);
            player.SetExperimentalSoldierAmmoRegeneratesWhileSwappedOut(false);
            player.SetExperimentalSelfDamageHealing(false);
            player.SetExperimentalSoldierInfiniteAmmoDuringRage(false);
            player.SetExperimentalReloadSpeedMultiplier(global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultReloadSpeedMultiplier);
            player.SetExperimentalDemoknightChargeFullControlEnabled(false);
            player.ConfigureExperimentalDemoknightPostRageRegeneration(0f);
            player.StartExperimentalDemoknightPostRageRegeneration(0);
            player.SetExperimentalOffhandWeapon(specialAbilitiesEnabled
                ? ResolveGameplaySecondaryWeapon(player, allowSoldierShotgun: false, allowSoldierShotgunLtd: false)
                : null);
            player.SetAcquiredWeapon(null);
            ApplyNetworkPlayerMaxHealthOverride(slot, player, refillHealth: false);
            return;
        }

        player.SetExperimentalDemoknightEnabled(
            ExperimentalGameplaySettings.EnableDemoknightKit
            && player.ClassId == PlayerClass.Demoman);
        player.SetExperimentalPassiveMovementSpeedMultiplier(ExperimentalGameplaySettings.PassiveMovementSpeedMultiplier);
        player.SetExperimentalJumpHeightMultiplier(ExperimentalGameplaySettings.PassiveJumpHeightMultiplier);
        player.SetExperimentalBonusAirJumps(ExperimentalGameplaySettings.PassiveBonusAirJumps);
        player.SetExperimentalDemoknightSwordRangeMultiplier(ExperimentalGameplaySettings.DemoknightSwordRangeMultiplier);
        player.SetExperimentalDemoknightSwordBaseDamage(ExperimentalGameplaySettings.DemoknightSwordBaseDamage);
        player.SetExperimentalDemoknightSwordDamageMultiplier(ExperimentalGameplaySettings.DemoknightSwordDamageMultiplier);
        player.SetExperimentalDemoknightSwordCooldownMultiplier(ExperimentalGameplaySettings.DemoknightSwordCooldownMultiplier);
        player.SetExperimentalDemoknightChargeRechargeMultiplier(ExperimentalGameplaySettings.DemoknightChargeRechargeMultiplier);
        player.SetExperimentalSoldierAmmoRegeneratesWhileSwappedOut(
            ExperimentalGameplaySettings.EnableSoldierAmmoRegeneratesWhileSwappedOut
            && player.ClassId == PlayerClass.Soldier);
        player.SetExperimentalSelfDamageHealing(
            ExperimentalGameplaySettings.EnableSelfDamageHealing
            && player.ClassId == PlayerClass.Soldier);
        player.SetExperimentalSoldierInfiniteAmmoDuringRage(
            ExperimentalGameplaySettings.EnableSoldierInfiniteAmmoDuringRage
            && player.ClassId == PlayerClass.Soldier);
        player.SetExperimentalReloadSpeedMultiplier(
            player.ClassId == PlayerClass.Soldier
                ? ExperimentalGameplaySettings.ReloadSpeedMultiplierValue
                : global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultReloadSpeedMultiplier);
        player.SetExperimentalDemoknightChargeFullControlEnabled(
            ExperimentalGameplaySettings.EnableDemoknightFullControlDuringCharge
            && player.ClassId == PlayerClass.Demoman);
        if (ExperimentalGameplaySettings.EnableDemoknightPostRageRegeneration
            && player.ClassId == PlayerClass.Demoman)
        {
            player.ConfigureExperimentalDemoknightPostRageRegeneration(
                global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultDemoknightPostRageRegenerationPerSecond);
        }
        else
        {
            player.ConfigureExperimentalDemoknightPostRageRegeneration(0f);
            player.StartExperimentalDemoknightPostRageRegeneration(0);
        }
        player.SetExperimentalOffhandWeapon(specialAbilitiesEnabled
            ? ResolveGameplaySecondaryWeapon(
                player,
                allowSoldierShotgun: ExperimentalGameplaySettings.EnableSoldierShotgunSecondaryWeapon,
                allowSoldierShotgunLtd: ExperimentalGameplaySettings.EnableSoldierShotgunLtdPerk)
            : null);
        if (!ExperimentalGameplaySettings.EnableEnemyDroppedWeapons
            || player.ClassId != PlayerClass.Soldier)
        {
            player.SetAcquiredWeapon(null);
        }

        ApplyNetworkPlayerMaxHealthOverride(slot, player, refillHealth: false);
    }

    private static PrimaryWeaponDefinition? ResolveGameplaySecondaryWeapon(
        PlayerEntity player,
        bool allowSoldierShotgun,
        bool allowSoldierShotgunLtd)
    {
        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        var secondaryItemId = player.GameplayLoadoutState.SecondaryItemId;
        if (!string.IsNullOrWhiteSpace(secondaryItemId))
        {
            var secondaryItem = runtimeRegistry.GetRequiredItem(secondaryItemId);
            if (runtimeRegistry.TryGetPrimaryWeaponBinding(secondaryItem.BehaviorId, out _))
            {
                return runtimeRegistry.CreatePrimaryWeaponDefinition(secondaryItem);
            }
        }

        // Also check utility slot for weapons (e.g., Demoman grenade launcher)
        var utilityItemId = player.GameplayLoadoutState.UtilityItemId;
        if (!string.IsNullOrWhiteSpace(utilityItemId))
        {
            var utilityItem = runtimeRegistry.GetRequiredItem(utilityItemId);
            if (runtimeRegistry.TryGetPrimaryWeaponBinding(utilityItem.BehaviorId, out _))
            {
                return runtimeRegistry.CreatePrimaryWeaponDefinition(utilityItem);
            }
        }

        if (allowSoldierShotgunLtd && player.ClassId == PlayerClass.Soldier)
        {
            return CharacterClassCatalog.SoldierShotgunLtd;
        }

        return allowSoldierShotgun && player.ClassId == PlayerClass.Soldier
            ? CharacterClassCatalog.SoldierShotgun
            : null;
    }

}
