using System.Diagnostics.CodeAnalysis;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public const int MaxPlayableNetworkPlayers = 20;
    public const byte LocalPlayerSlot = 1;
    public const byte FirstSpectatorSlot = 128;
    public static IReadOnlyList<byte> NetworkPlayerSlots { get; } = Enumerable.Range(1, MaxPlayableNetworkPlayers).Select(static value => (byte)value).ToArray();
    private const string DefaultLocalPlayerName = "Player 1";
    private const string DefaultEnemyPlayerName = "Player 2";
    private const string DefaultFriendlyDummyName = "Player 3";
    private const int DefaultRespawnTicks = 150;
    private const int DefaultTimeLimitMinutes = 15;
    private const int DefaultCapLimit = 5;
    private const int ArenaPointCapTimeTicksDefault = 300;
    private const int ArenaPointUnlockTicksDefault = 1800;
    private const int PendingMapChangeTicks = 300;
    private const string ClassChangeKillFeedSuffix = " bid farewell, cruel world!";
    private const int CombatTraceLifetimeTicks = 3;
    private const int KillFeedLifetimeTicks = 150;
    private const int DefaultGibLevel = 3;
    private readonly Dictionary<int, SimulationEntity> _entities = new();
    private readonly List<CombatTrace> _combatTraces = new();
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
    private readonly List<MineProjectileEntity> _mines = new();
    private readonly List<SentryEntity> _sentries = new();
    private readonly List<PlayerGibEntity> _playerGibs = new();
    private readonly List<BloodDropEntity> _bloodDrops = new();
    private readonly List<DeadBodyEntity> _deadBodies = new();
    private readonly List<SentryGibEntity> _sentryGibs = new();
    private readonly List<GeneratorState> _generators = new();
    private readonly List<WorldSoundEvent> _pendingSoundEvents = new();
    private readonly List<WorldVisualEvent> _pendingVisualEvents = new();
    private readonly List<WorldDamageEvent> _pendingDamageEvents = new();
    private readonly List<PlayerEntity> _remoteSnapshotPlayers = new();
    private readonly List<string> _spectatorNames = new();
    private readonly Dictionary<byte, PlayerEntity> _remoteSnapshotPlayersBySlot = new();
    private readonly HashSet<int> _snapshotSeenEntityIds = new();
    private readonly List<int> _snapshotStaleEntityIds = new();
    private readonly HashSet<byte> _snapshotSeenRemotePlayerSlots = new();
    private readonly List<byte> _snapshotStaleRemotePlayerSlots = new();
    private readonly Dictionary<byte, PlayerEntity> _additionalNetworkPlayersBySlot = new();
    private readonly HashSet<byte> _enabledAdditionalNetworkPlayerSlots = new();
    private readonly Dictionary<byte, CharacterClassDefinition> _additionalNetworkPlayerClassDefinitions = new();
    private readonly Dictionary<byte, PlayerInputSnapshot> _additionalNetworkPlayerInputs = new();
    private readonly Dictionary<byte, PlayerInputSnapshot> _additionalNetworkPlayerPreviousInputs = new();
    private readonly Dictionary<byte, bool> _additionalNetworkPlayerAwaitingJoin = new();
    private readonly Dictionary<byte, int> _additionalNetworkPlayerRespawnTicks = new();
    private readonly Dictionary<byte, PlayerTeam> _additionalNetworkPlayerTeams = new();
    private readonly Dictionary<byte, LocalDeathCamState> _networkPlayerDeathCams = new();
    private readonly Random _random = new(1337);
    private int _configuredTimeLimitMinutes = DefaultTimeLimitMinutes;
    private int _configuredCapLimit = DefaultCapLimit;
    private int _configuredRespawnTicks = DefaultRespawnTicks;
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
    private int _killFeedTrimTicks;
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
    private readonly List<ControlPointState> _controlPoints = new();
    private readonly List<ControlPointZone> _controlPointZones = new();
    private bool _controlPointSetupMode;
    private int _controlPointSetupTicksRemaining;

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

    public IReadOnlyList<string> SpectatorNames => _spectatorNames;

    public MatchRules MatchRules { get; private set; }

    public MatchState MatchState { get; private set; }

    public int MapChangeTicksRemaining => _pendingMapChangeTicks;

    public bool IsMapChangePending => _pendingMapChangeTicks >= 0;

    public bool IsMapChangeReady => _mapChangeReady;

    public bool AutoRestartOnMapChange
    {
        get => _autoRestartOnMapChange;
        set => _autoRestartOnMapChange = value;
    }
    public int LocalPlayerRespawnTicks { get; private set; }

    public bool LocalPlayerAwaitingJoin => _localPlayerAwaitingJoin;

    public LocalDeathCamState? LocalDeathCam { get; private set; }

    public IReadOnlyList<KillFeedEntry> KillFeed => _killFeed;

    public IReadOnlyList<CombatTrace> CombatTraces => _combatTraces;

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

    public IReadOnlyList<SentryEntity> Sentries => _sentries;

    public IReadOnlyList<PlayerGibEntity> PlayerGibs => _playerGibs;

    public IReadOnlyList<BloodDropEntity> BloodDrops => _bloodDrops;

    public IReadOnlyList<DeadBodyEntity> DeadBodies => _deadBodies;

    public IReadOnlyList<SentryGibEntity> SentryGibs => _sentryGibs;

    public IReadOnlyList<WorldSoundEvent> PendingSoundEvents => _pendingSoundEvents;

    public IReadOnlyList<WorldVisualEvent> PendingVisualEvents => _pendingVisualEvents;

    public IReadOnlyList<WorldDamageEvent> PendingDamageEvents => _pendingDamageEvents;

    public IReadOnlyList<PlayerEntity> RemoteSnapshotPlayers => _remoteSnapshotPlayers;

    public bool EnemyPlayerEnabled { get; private set; } = true;

    public bool FriendlyDummyEnabled { get; private set; }

    public LocalDeathCamState? GetNetworkPlayerDeathCam(byte slot)
    {
        if (slot == LocalPlayerSlot)
        {
            return LocalDeathCam;
        }

        return _networkPlayerDeathCams.GetValueOrDefault(slot);
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
        Config = config ?? new SimulationConfig();
        Level = SimpleLevelFactory.CreateScoutPrototypeLevel();
        RedIntel = CreateIntelState(PlayerTeam.Red);
        BlueIntel = CreateIntelState(PlayerTeam.Blue);
        MatchRules = CreateDefaultMatchRules(Level.Mode);
        MatchState = CreateInitialMatchState(MatchRules);
        LocalPlayer = new PlayerEntity(AllocateEntityId(), _localPlayerClassDefinition, DefaultLocalPlayerName);
        var initialSpawn = ReserveSpawn(LocalPlayer, LocalPlayerTeam);
        SpawnPlayerResolved(LocalPlayer, LocalPlayerTeam, initialSpawn);
        _entities.Add(LocalPlayer.Id, LocalPlayer);
        EnemyPlayer = new PlayerEntity(AllocateEntityId(), _enemyDummyClassDefinition, DefaultEnemyPlayerName);
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
        FriendlyDummy.Kill();
        _entities.Add(FriendlyDummy.Id, FriendlyDummy);
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
        var definition = CharacterClassCatalog.GetDefinition(playerClass);
        if (definition.Id == GetNetworkPlayerClassDefinition(LocalPlayerSlot).Id)
        {
            return false;
        }

        return TryApplyNetworkPlayerClassChange(LocalPlayerSlot, definition);
    }

    public bool TrySetEnemyClass(PlayerClass playerClass)
    {
        var definition = CharacterClassCatalog.GetDefinition(playerClass);
        if (definition.Id == _enemyDummyClassDefinition.Id)
        {
            return false;
        }

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

    private int AllocateEntityId()
    {
        return _nextEntityId++;
    }

    private MatchRules CreateDefaultMatchRules(GameModeKind mode)
    {
        var timeLimitTicks = _configuredTimeLimitMinutes * Config.TicksPerSecond * 60;
        return new MatchRules(mode, _configuredTimeLimitMinutes, timeLimitTicks, _configuredCapLimit);
    }

    private static MatchState CreateInitialMatchState(MatchRules rules)
    {
        return new MatchState(MatchPhase.Running, rules.TimeLimitTicks, null);
    }

}
