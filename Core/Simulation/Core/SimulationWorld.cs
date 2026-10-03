using System.Diagnostics.CodeAnalysis;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private readonly SimulationRuntime _runtime;
    private readonly PlayerCountQueries _playerCounts;
    public const int MaxPlayableNetworkPlayers = SimulationConstants.MaxPlayableNetworkPlayers;
    public const byte LocalPlayerSlot = SimulationConstants.LocalPlayerSlot;
    public const byte FirstSpectatorSlot = 128;
    public static IReadOnlyList<byte> NetworkPlayerSlots => SimulationConstants.NetworkPlayerSlots;
    private const string DefaultLocalPlayerName = SimulationConstants.DefaultLocalPlayerName;
    private const string DefaultEnemyPlayerName = "Player 2";
    private const string DefaultFriendlyDummyName = "Player 3";
    private const int DefaultRespawnSeconds = 5;
    private const int DefaultTimeLimitMinutes = 15;
    private const int DefaultCapLimit = 5;
    private const int DefaultTeamDeathmatchKillLimit = 30;
    private const int ArenaPointCapTimeTicksDefault = ArenaObjectiveState.PointCapTimeTicksDefault;
    private const int ArenaPointUnlockTicksDefault = 1800;
    private const int PendingMapChangeTicks = 300;
    private const int LocalProjectileTerminationSuppressionTicks = 12;
    public EntityStore EntityStore { get; } = new();
    public CombatSystem Combat { get; }
    public SnapshotSystem Snapshots { get; }
    public ProjectileSystem Projectiles { get; }
    internal PresentationEventLog PresentationEvents { get; } = new();
    internal WorldObjectStore WorldObjects { get; }
    internal ObjectiveStateStore Objectives { get; } = new();
    internal NetworkPlayerRegistry PlayerRegistry { get; } = new();
    internal RemoteSnapshotPlayerRegistry RemoteSnapshots { get; } = new();
    internal VipState VipState { get; } = new();
    internal CompetitiveReadyUpState ReadyUpState { get; } = new(DefaultCompetitiveSetupSeconds);
    internal PracticeDummyState DummyState { get; } = new();
    internal LastToDieState LastToDieState { get; } = new();
    internal MatchSettingsState MatchSettings { get; } = new(
        DefaultTimeLimitMinutes,
        DefaultCapLimit,
        DefaultRespawnSeconds,
        DefaultRespawnSeconds * SimulationConfig.DefaultTicksPerSecond);
    internal ClientSnapshotState ClientSnapshots { get; } = new();
    internal MapRuntimeState MapRuntime { get; } = new();
    internal CombatRuntimeState CombatRuntime { get; } = new();
    internal LocalSimulationState LocalState { get; } = new();
    internal MatchLifecycleState Lifecycle { get; } = new();
    internal SimulationRandomStreams Randoms { get; } = new();
    internal NetworkPlayerSystem NetworkPlayerRules { get; }
    internal GameplayAbilitySystem Abilities { get; }
    internal ObjectiveRulesSystem ObjectiveRules { get; }
    internal PickupSystem Pickups { get; }
    internal StructureSystem Structures { get; }
    internal LastToDieRulesSystem LastToDieRules { get; }
    internal ExperimentalRulesSystem ExperimentalRules { get; }
    internal SupportRulesSystem SupportRules { get; }
    internal ExplosionRulesSystem ExplosionRules { get; }
    internal AirblastRulesSystem AirblastRules { get; }
    internal PlayerPresentationBoundsSystem PresentationBounds { get; }
    internal CombatFeedbackSystem CombatFeedback { get; }
    internal VipRulesSystem VipRules { get; }
    internal ReadyUpSystem ReadyUp { get; }
    internal MapLogicSystem MapLogic { get; }
    internal PracticeDummySystem PracticeDummies { get; }
    internal ServerTuningSystem ServerTuning { get; }
    internal RoomEffectsSystem RoomEffects { get; }
    internal WorldEffectsSystem WorldEffects { get; }
    internal ClassRulesSystem ClassRules { get; }
    internal ScorekeepingSystem Scorekeeping { get; }
    internal AdminCommandsSystem AdminCommands { get; }
    internal KillFeedSystem KillFeedRules { get; }
    internal PlayerRemainsSystem PlayerRemains { get; }
    internal SpawnSystem Spawns { get; }
    internal PlayerDeathSystem PlayerDeaths { get; }
    internal PlayerInputSystem PlayerInput { get; }
    internal SnapshotApplySystem SnapshotApply { get; }
    internal DecisionGate Decisions { get; }
    internal DamageRulesSystem DamageRules { get; }

    public long Frame { get; private set; }

    public int SessionPresentationSeed { get; private set; }

    public double SimulationTimeSeconds => Frame * Config.FixedDeltaSeconds;

    public SimulationConfig Config { get; }

    public IReadOnlyDictionary<int, SimulationEntity> Entities => EntityStore.AsReadOnly();

    public SimpleLevel Level { get; private set; }

    public WorldBounds Bounds => Level.Bounds;

    public PlayerEntity LocalPlayer { get; }

    public PlayerTeam LocalPlayerTeam { get; private set; } = PlayerTeam.Red;

    public PlayerEntity EnemyPlayer { get; }

    public PlayerEntity FriendlyDummy { get; }

    public int RedCaps { get; private set; }

    public int BlueCaps { get; private set; }

    public int SpectatorCount { get; private set; }

    public IReadOnlyList<ScoreboardSpectatorEntry> Spectators => ClientSnapshots.Spectators;

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

    /// <summary>Client blood-drop lifetime in whole seconds.</summary>
    public int LocalBloodLifetimeSeconds { get; set; } = 9;

    public int ScaleBloodDropLifetimeTicks()
        => Math.Max(1, (int)MathF.Round(Math.Clamp(LocalBloodLifetimeSeconds, 1, 120) * Config.TicksPerSecond));

    public int GetDeterministicSpreadShotIndex(int attackerId)
    {
        var index = CombatRuntime.SpreadShotIndexByPlayerId.TryGetValue(attackerId, out var currentIndex)
            ? currentIndex
            : 0;
        CombatRuntime.SpreadShotIndexByPlayerId[attackerId] = index + 1;
        return index;
    }

    public int MapChangeTicksRemaining => Lifecycle.PendingMapChangeTicks;

    public bool IsMapChangePending => Lifecycle.PendingMapChangeTicks >= 0;

    public bool IsMapChangeReady => Lifecycle.MapChangeReady;

    public bool AutoRestartOnMapChange
    {
        get => Lifecycle.AutoRestartOnMapChange;
        set => Lifecycle.AutoRestartOnMapChange = value;
    }

    public int ConfiguredRespawnSeconds => MatchSettings.RespawnSeconds;

    public float ConfiguredPlayerScale => MatchSettings.PlayerScale;

    public float ConfiguredMapScale => MatchSettings.MapScale;

    public float ConfiguredMovementSpeedScale => MatchSettings.MovementSpeedScale;

    public float ConfiguredProjectileSpeedScale => MatchSettings.ProjectileSpeedScale;

    public float ConfiguredDamageScale => MatchSettings.DamageScale;

    public float ConfiguredGravityScale => MatchSettings.GravityScale;

    public float ConfiguredHorizontalSpeedClampPerTick => MatchSettings.HorizontalSpeedClampPerTick;

    public float ConfiguredVerticalSpeedClampPerTick => MatchSettings.VerticalSpeedClampPerTick;

    public float ConfiguredCaptureSpeedMultiplierPerPlayer => MatchSettings.CaptureSpeedMultiplierPerPlayer;

    public bool VipAllowDuplicateClasses => VipState.AllowDuplicateClasses;

    public bool RoundEndFriendlyFireEnabled => MatchSettings.RoundEndFriendlyFireEnabled;

    public int LocalPlayerRespawnTicks { get; private set; }

    public bool LocalPlayerAwaitingJoin => LocalState.PlayerAwaitingJoin;

    public LocalDeathCamState? LocalDeathCam { get; private set; }

    public IReadOnlyList<KillFeedEntry> KillFeed => PresentationEvents.KillFeed;

    public IReadOnlyList<CombatTrace> CombatTraces => PresentationEvents.CombatTraces;

    public IReadOnlyList<SniperAimIndicator> SniperAimIndicators => PresentationEvents.SniperAimIndicators;

    public IReadOnlyList<ShotProjectileEntity> Shots => Projectiles.Shots;

    public IReadOnlyList<BubbleProjectileEntity> Bubbles => Projectiles.Bubbles;

    public IReadOnlyList<BladeProjectileEntity> Blades => Projectiles.Blades;

    public IReadOnlyList<NeedleProjectileEntity> Needles => Projectiles.Needles;

    public IReadOnlyList<RevolverProjectileEntity> RevolverShots => Projectiles.RevolverShots;

    public IReadOnlyList<StabAnimEntity> StabAnimations => Projectiles.StabAnimations;

    public IReadOnlyList<StabMaskEntity> StabMasks => Projectiles.StabMasks;

    public IReadOnlyList<FlameProjectileEntity> Flames => Projectiles.Flames;

    public IReadOnlyList<FlareProjectileEntity> Flares => Projectiles.Flares;

    public IReadOnlyList<RocketProjectileEntity> Rockets => Projectiles.Rockets;

    public IReadOnlyList<MineProjectileEntity> Mines => Projectiles.Mines;

    public IReadOnlyList<GrenadeProjectileEntity> Grenades => Projectiles.Grenades;

    public IReadOnlyList<SentryEntity> Sentries => WorldObjects.Sentries;

    public IReadOnlyList<JumpPadEntity> JumpPads => WorldObjects.JumpPads;

    public IReadOnlyList<CivilDefenseTurretEntity> CivilDefenseTurrets => WorldObjects.CivilDefenseTurrets;


    public IReadOnlyList<PlayerGibEntity> PlayerGibs => WorldObjects.PlayerGibs;

    public IReadOnlyList<BloodDropEntity> BloodDrops => WorldObjects.BloodDrops;

    public IReadOnlyList<HealthPackEntity> HealthPacks => WorldObjects.HealthPacks;

    public IReadOnlyList<DroppedWeaponEntity> DroppedWeapons => WorldObjects.DroppedWeapons;

    public IReadOnlyList<DeadBodyEntity> DeadBodies => WorldObjects.DeadBodies;

    public IReadOnlyList<SentryGibEntity> SentryGibs => WorldObjects.SentryGibs;

    public IReadOnlyList<JumpPadGibEntity> JumpPadGibs => WorldObjects.JumpPadGibs;

    public IReadOnlyList<WorldSoundEvent> PendingSoundEvents => PresentationEvents.SoundEvents;

    public IReadOnlyList<WorldVisualEvent> PendingVisualEvents => PresentationEvents.VisualEvents;

    public IReadOnlyList<WorldDamageEvent> PendingDamageEvents => Combat.PendingDamageEvents;

    public IReadOnlyList<WorldRocketSpawnEvent> PendingRocketSpawnEvents => Projectiles.PendingRocketSpawnEvents;

    public IReadOnlyList<WorldHealingEvent> PendingHealingEvents => PresentationEvents.HealingEvents;

    public IReadOnlyList<PlayerEntity> RemoteSnapshotPlayers => RemoteSnapshots.Players;

    public IReadOnlyList<PlayerEntity> RemoteSnapshotScoreboardPlayers => RemoteSnapshots.ScoreboardPlayers;

    public IReadOnlySet<byte> RemoteSnapshotAwaitingJoinSlots => RemoteSnapshots.AwaitingJoinSlots;

    public bool EnemyPlayerEnabled { get; private set; } = true;

    public bool IsRemoteSnapshotPlayerAwaitingJoin(PlayerEntity player)
        => RemoteSnapshots.AwaitingJoinPlayerIds.Contains(player.Id);

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
            deathCam = PlayerRegistry.DeathCams.GetValueOrDefault(slot);
        }

        return deathCam is null ? null : PlayerDeaths.ResolveTrackedDeathCamFocus(deathCam);
    }

    public PlayerTeam? ArenaPointTeam => Objectives.Arena.PointTeam;

    public PlayerTeam? ArenaCappingTeam => Objectives.Arena.CappingTeam;

    public float ArenaCappingTicks => Objectives.Arena.CappingTicks;

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Kept as an instance property to preserve the public simulation API.")]
    public int ArenaPointCapTimeTicks => ArenaPointCapTimeTicksDefault;

    public int ArenaCappers => Objectives.Arena.Cappers;

    public int ArenaUnlockTicksRemaining => Objectives.Arena.UnlockTicksRemaining;

    public bool ArenaPointLocked => MatchRules.Mode == GameModeKind.Arena && Objectives.Arena.UnlockTicksRemaining > 0;

    public int ArenaRedConsecutiveWins => Objectives.Arena.RedConsecutiveWins;

    public int ArenaBlueConsecutiveWins => Objectives.Arena.BlueConsecutiveWins;

    public int ArenaRedAliveCount => CountAlivePlayers(PlayerTeam.Red);

    public int ArenaBlueAliveCount => CountAlivePlayers(PlayerTeam.Blue);

    public int ArenaRedPlayerCount => CountPlayers(PlayerTeam.Red);

    public int ArenaBluePlayerCount => CountPlayers(PlayerTeam.Blue);

    public bool IsPlayerHumiliated(PlayerEntity player)
    {
        if (PracticeDummies.IsPracticeDummy(player))
        {
            return true;
        }

        if (ExperimentalRules.IsExperimentalRageHumiliationActiveForPlayer(player))
        {
            return true;
        }

        if (!MatchState.IsEnded)
        {
            return false;
        }

        return !MatchState.WinnerTeam.HasValue || player.Team != MatchState.WinnerTeam.Value;
    }

    public IReadOnlyList<ControlPointState> ControlPoints => Objectives.ControlPoints.Points;

    public bool ControlPointSetupActive => Objectives.ControlPoints.SetupMode && Objectives.ControlPoints.SetupTicksRemaining > 0;

    public int ControlPointSetupTicksRemaining => Objectives.ControlPoints.SetupTicksRemaining;

    public SimulationWorld(SimulationConfig? config = null)
    {
        WorldObjects = new WorldObjectStore(EntityStore);
        Structures = new StructureSystem(this);
        Pickups = new PickupSystem(this);
        ObjectiveRules = new ObjectiveRulesSystem(this);
        Abilities = new GameplayAbilitySystem(this);
        NetworkPlayerRules = new NetworkPlayerSystem(this);
        LastToDieRules = new LastToDieRulesSystem(this);
        ExperimentalRules = new ExperimentalRulesSystem(this);
        SupportRules = new SupportRulesSystem(this);
        ExplosionRules = new ExplosionRulesSystem(this);
        AirblastRules = new AirblastRulesSystem(this);
        PresentationBounds = new PlayerPresentationBoundsSystem(this);
        CombatFeedback = new CombatFeedbackSystem(this);
        VipRules = new VipRulesSystem(this);
        ReadyUp = new ReadyUpSystem(this);
        MapLogic = new MapLogicSystem(this);
        PracticeDummies = new PracticeDummySystem(this);
        ServerTuning = new ServerTuningSystem(this);
        RoomEffects = new RoomEffectsSystem(this);
        WorldEffects = new WorldEffectsSystem(this);
        ClassRules = new ClassRulesSystem(this);
        Scorekeeping = new ScorekeepingSystem(this);
        AdminCommands = new AdminCommandsSystem(this);
        KillFeedRules = new KillFeedSystem(this);
        PlayerRemains = new PlayerRemainsSystem(this);
        Spawns = new SpawnSystem(this);
        PlayerDeaths = new PlayerDeathSystem(this);
        PlayerInput = new PlayerInputSystem(this);
        SnapshotApply = new SnapshotApplySystem(this);
        Decisions = new DecisionGate(this);
        DamageRules = new DamageRulesSystem(this);
        _runtime = new SimulationRuntime(this, new EntityTickPhase(this), new MatchTickPhase(this, new MatchObjectiveSystem(this)));
        _playerCounts = new PlayerCountQueries(this);
        Config = config ?? new SimulationConfig();
        Combat = new CombatSystem(EntityStore, this);
        Snapshots = new SnapshotSystem(EntityStore, Combat, this);
        Projectiles = new ProjectileSystem(EntityStore, Combat, this);
        Level = SimpleLevelFactory.CreateScoutPrototypeLevel(MatchSettings.MapScale);
        Movement = new MovementSystem(this);
        RedIntel = ObjectiveRules.CreateIntelState(PlayerTeam.Red);
        BlueIntel = ObjectiveRules.CreateIntelState(PlayerTeam.Blue);
        MatchRules = CreateDefaultMatchRules(Level.Mode);
        MatchState = CreateInitialMatchState(MatchRules);
        LocalPlayer = new PlayerEntity(AllocateEntityId(), LocalState.PlayerClassDefinition, DefaultLocalPlayerName);
        LocalPlayer.SetPlayerScale(MatchSettings.PlayerScale);
        ServerTuning.ApplyServerGameplayTuning(LocalPlayerSlot, LocalPlayer);
        var initialSpawn = Spawns.ReserveSpawn(LocalPlayer, LocalPlayerTeam);
        Spawns.SpawnPlayerResolved(LocalPlayer, LocalPlayerTeam, initialSpawn);
        EntityStore.Add(LocalPlayer);
        PlayerRegistry.ActivePlayersById[LocalPlayer.Id] = LocalPlayer;
        PlayerRegistry.SlotsByPlayerId[LocalPlayer.Id] = LocalPlayerSlot;
        EnemyPlayer = new PlayerEntity(AllocateEntityId(), DummyState.EnemyClassDefinition, DefaultEnemyPlayerName);
        EnemyPlayer.SetPlayerScale(MatchSettings.PlayerScale);
        ServerTuning.ApplyServerGameplayTuning(slot: 0, EnemyPlayer);
        if (Config.EnableLocalDummies && Config.EnableEnemyTrainingDummy)
        {
            var enemySpawn = Spawns.ReserveSpawn(EnemyPlayer, DummyState.EnemyTeam);
            Spawns.SpawnPlayerResolved(EnemyPlayer, DummyState.EnemyTeam, enemySpawn);
            EnemyPlayerEnabled = true;
        }
        else
        {
            EnemyPlayerEnabled = false;
            EnemyPlayer.Kill();
        }
        EntityStore.Add(EnemyPlayer);
        FriendlyDummy = new PlayerEntity(AllocateEntityId(), LocalState.FriendlyDummyClassDefinition, DefaultFriendlyDummyName);
        FriendlyDummy.SetPlayerScale(MatchSettings.PlayerScale);
        ServerTuning.ApplyServerGameplayTuning(slot: 0, FriendlyDummy);
        FriendlyDummy.Kill();
        EntityStore.Add(FriendlyDummy);
        Pickups.ResetHealthPackSpawnsForLevel();
        Structures.ResetJumpPadSpawnsForLevel();
    }

    public void SetLocalHealth(int health)
    {
        if (health <= 0)
        {
            NetworkPlayerRules.ForceKillLocalPlayer();
            return;
        }

        if (!LocalPlayer.IsAlive)
        {
            NetworkPlayerRules.ForceRespawnLocalPlayer();
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
            NetworkPlayerRules.ForceRespawnLocalPlayer();
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
        return $"class={LocalPlayer.ClassName} metal={LocalPlayer.Metal:F1}/{LocalPlayer.MaxMetal:F1} sentries={WorldObjects.Sentries.Count} gibs={WorldObjects.SentryGibs.Count}";
    }

    public bool TrySetLocalClass(PlayerClass playerClass)
    {
        return CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(playerClass, out var binding)
            && TrySetLocalClass(binding.ClassId);
    }

    public bool TrySetLocalClass(string gameplayClassId)
    {
        var definition = ClassRules.ResolveMapForcedClassDefinition(LocalPlayerSlot, CharacterClassCatalog.GetDefinition(gameplayClassId));
        if (string.Equals(definition.GameplayClassId, NetworkPlayerRules.GetNetworkPlayerClassDefinition(LocalPlayerSlot).GameplayClassId, StringComparison.Ordinal))
        {
            // Allow same-class selection to commit a pending team swap.
            // Use TryApplyNetworkPlayerClassChange so spawn-room and respawn-timer rules are respected.
            return (LocalPlayer.Team != NetworkPlayerRules.GetNetworkPlayerConfiguredTeam(LocalPlayerSlot)
                    || NetworkPlayerRules.HasPendingNetworkPlayerTeamSelection(LocalPlayerSlot))
                && NetworkPlayerRules.TryApplyNetworkPlayerClassChange(LocalPlayerSlot, definition);
        }

        return NetworkPlayerRules.TryApplyNetworkPlayerClassChange(LocalPlayerSlot, definition);
    }


    public IReadOnlyList<WorldSoundEvent> DrainPendingSoundEvents()
        => PresentationEvents.DrainSoundEvents();

    public IReadOnlyList<WorldVisualEvent> DrainPendingVisualEvents()
        => PresentationEvents.DrainVisualEvents();

    public IReadOnlyList<WorldDamageEvent> DrainPendingDamageEvents()
        => Combat.DrainPendingDamageEvents();

    public IReadOnlyList<WorldGibSpawnEvent> DrainPendingGibSpawnEvents()
        => PresentationEvents.DrainGibSpawnEvents();

    public IReadOnlyList<WorldRocketSpawnEvent> DrainPendingRocketSpawnEvents()
        => Projectiles.DrainPendingRocketSpawnEvents();

    public IReadOnlyList<WorldHealingEvent> DrainPendingHealingEvents()
        => PresentationEvents.DrainHealingEvents();

    private int AllocateEntityId()
    {
        return EntityStore.AllocateId();
    }

    private MatchRules CreateDefaultMatchRules(GameModeKind mode)
    {
        var timeLimitTicks = MatchSettings.TimeLimitMinutes * Config.TicksPerSecond * 60;
        var capLimit = mode == GameModeKind.TeamDeathmatch && MatchSettings.CapLimit == DefaultCapLimit
            ? DefaultTeamDeathmatchKillLimit
            : MatchSettings.CapLimit;
        return new MatchRules(mode, MatchSettings.TimeLimitMinutes, timeLimitTicks, capLimit);
    }

    private static MatchState CreateInitialMatchState(MatchRules rules)
    {
        return new MatchState(MatchPhase.Running, rules.TimeLimitTicks, null);
    }

    public void ConfigureExperimentalGameplaySettings(ExperimentalGameplaySettings settings)
        => ExperimentalRules.ConfigureExperimentalGameplaySettings(settings);
}
