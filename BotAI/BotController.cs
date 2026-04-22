using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using System.Runtime.CompilerServices;

namespace OpenGarrison.BotAI;

public sealed partial class ModernPracticeBotController : IPracticeBotController
{
    public static CompatNavPointSource CompatNavPointSourceOverride { get; set; } = CompatNavPointSource.Auto;

    private const float ObjectiveArrivalDistance = 56f;
    private const float PatrolDistance = 36f;
    private const float MovementDeadZone = 18f;
    private const float RouteMovementDeadZone = 4f;
    private const float NearbyEnemyDistance = 260f;
    private const float VisibleTargetSeekDistance = 900f;
    private const float CabinetSeekHealthFraction = 0.45f;
    private const float CabinetSeekDistance = 320f;
    private const float HealTargetSeekDistance = 360f;
    private const float AimChestOffsetFraction = 0.3f;
    private const float WallProbeDistance = 18f;
    private const float WallProbeThickness = 3f;
    private const float WallProbeBottomInset = 4f;
    private const float RouteNodeArrivalDistance = 18f;
    private const float RouteWalkApproximateArrivalHeight = 64f;
    private const float RouteWalkArrivalFeetHeight = 32f;
    private const float RouteWalkRecoveryJumpFeetDrop = 24f;
    private const float RouteWalkRecoveryFlatHeightDelta = 16f;
    private const float TraversalStartDistance = 6f;
    private const float RoutePartialMinimumImprovementDistance = 18f;
    private const float RouteRepathDistance = 120f;
    private const float RouteStartNodeSearchDistance = 144f;
    private const float RouteGoalNodeSearchDistance = 220f;
    private const float RouteProgressImprovementDistance = 3f;
    private const float RouteObjectiveProgressImprovementDistance = 6f;
    private const int RouteNoProgressTicksBeforeReplan = 600;
    private const int RouteObjectiveNoProgressTicksBeforeReplan = 1200;
    private const int RouteBlockedEdgeTicksDefault = 7200;
    private const float ModernWeightedCurrentNodeSearchDistance = 220f;
    private const float ModernPointArrivalDistance = 34f;
    private const float ModernPointDirectDistance = 350f;
    private const float ModernPointVerticalDistance = 100f;
    private const int ModernMaximumWeightDepth = 130;
    private const float ModernCandidatePreviousPointPenalty = 220f;
    private const float ModernCandidatePreviousNextBonus = 120f;
    private const float ModernCandidateCommittedPointPenalty = 340f;
    private const float ModernForcedWeightPenalty = 90f;
    private const float ModernForcedPreviousCurrentPenalty = 120f;
    private const float ModernForcedPreviousNextPenalty = 60f;
    private const float ModernForcedCommittedPenalty = 180f;
    private const float ModernPointLookaheadDistance = 24f;
    private const float ModernPointLookaheadMinimumSpeed = 0.35f;
    private const float ModernPointSightYOffset = 12f;
    private const float ModernIntelReturnFinalApproachDistanceX = 160f;
    private const float ModernIntelReturnFinalApproachDistanceY = 140f;
    private const float ModernIntelReturnGraphFinishDistanceX = 640f;
    private const float ModernIntelReturnGraphFinishDistanceY = 640f;
    private const float ModernIntelReturnGraphFinishDistance = 640f;
    private const float ModernIntelReturnGraphReleaseDistanceX = 760f;
    private const float ModernIntelReturnGraphReleaseDistanceY = 760f;
    private const float ModernIntelReturnDirectArrivalDistance = 88f;
    private const float ModernIntelMarkerSize = 24f;
    private const int ModernIntelReturnDirectLatchTicks = 90;
    private const float ModernIntelReturnBlockedEdgeBypassDistanceX = 760f;
    private const float ModernIntelReturnBlockedEdgeBypassDistanceY = 760f;
    private const float ModernCurrentPointReanchorDistance = 130f;
    private const float ModernChurnDistance = 52f;
    private const float ModernCaptureZoneSeedBand = 96f;
    private const float ModernCaptureZoneGroupLinkDistance = 63f;
    private const float ModernCaptureZoneSquareHalfSize = 21f;
    private const float ModernCaptureBrakeTargetDistanceX = 24f;
    private const float ModernCaptureBrakeTargetDistanceY = 56f;
    private const float ModernCaptureEnemyNearbyDistance = 250f;
    private const float ModernCaptureRetainMilliseconds = 1000f;
    private const float ModernCaptureProgressRetainDistance = 220f;
    private const float ModernObstacleCellSize = 128f;
    private const float ModernDropReclassifyDistance = 18f;
    private const int ModernJumpCooldownSourceTicks = 10;
    private const int ModernNoNextTicksBeforeForceNeighbor = 8;
    private const int ModernNoNextTicksBeforeReacquire = 24;
    private const int ModernNoNextTickDecay = 2;
    private const int ModernNoNextTickRecovery = 6;
    private const int ModernStickyDisableStuckTicks = 6;
    private const int ModernStickyTicks = 10;
    private const int ModernLoopBacktrackTicks = 3;
    private const int ModernChurnObservationTicks = 16;
    private const int ModernChurnSwitchTicks = 7;
    private const int ModernChurnLockTicks = 18;
    private const float StuckMoveDistanceSquared = 9f;
    private const int StuckTickThreshold = 24;
    private const int UnstickTicksDefault = 16;
    private const int JumpCooldownTicksDefault = 16;
    private const int StrafeTicksMin = 18;
    private const int StrafeTicksMax = 42;
    private const int RouteRefreshTicksDefault = 20;
    private const int StickyTargetRefreshTicksDefault = 12;
    private const int EnemyTargetLockTicksDefault = 18;
    private const int HealTargetLockTicksDefault = 20;
    private const float PreferredScoreRouteAcquireDistance = 320f;
    private const int PreferredScoreRouteMissBudget = 6;
    private const int PreferredScoreRouteMissCooldownTicksDefault = 240;
    private const float LowHealthRetreatFraction = 0.3f;
    private const float ModernEnemySeeDistance = 375f;
    private const int ModernSpyReactTimeSourceTicks = 35;
    private const int ModernBeenHealingSwitchSourceTicks = 20;
    private const int ModernZoomToShootMinSourceTicks = 50;
    private const int ModernZoomToShootMaxSourceTicks = 105;
    private const int ModernSoldierCloseReloadSourceTicks = 5;
    private const float ModernSniperDangerDistance = 150f;
    private const int ModernHeavyIdleEatHealth = 100;
    private const int ModernHeavyCombatEatHealth = 30;
    private const float ModernCloseCombatDistance = 170f;
    private const float ModernSoldierShotgunDistance = 260f;
    private const float ModernMineThreatDistance = 400f;
    private const float ModernMineDetonationRadius = 50f;

    private readonly Dictionary<byte, BotMemory> _memoryBySlot = new();
    private readonly Dictionary<byte, PlayerInputSnapshot> _inputsBuffer = new();
    private readonly Dictionary<byte, ControlledPlayerState> _controlledPlayersBuffer = new();
    private readonly Dictionary<string, BotNavigationRuntimeGraph> _navigationGraphsByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<BotNavigationInputFrame>?> _runtimeValidatedJumpTapeByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ClientBotNavPoints> _clientBotNavPointsByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BotNavigationHintAsset?> _clientBotHintAssetsByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BotNavigationScoreRouteAsset?> _clientBotScoreRouteAssetsByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<PlayerClass, BotNavigationRuntimeGraph> _navigationGraphsByClassBuffer = new();
    private readonly object _navigationGraphLock = new();
    private readonly Dictionary<int, int> _modernSpyVisibleTicksByPlayerId = new();
    private readonly List<PlayerEntity> _allPlayersBuffer = new();
    private readonly HashSet<int> _activeSpyIdsBuffer = new();
    private readonly List<int> _staleSpyIdsBuffer = new();
    private readonly List<byte> _staleMemorySlotsBuffer = new();
    private readonly Random _random = new(1337);
    private int _modernSpyReactTicksThreshold = ModernSpyReactTimeSourceTicks;
    private int _lastRequestedHorizontalForDiagnostics;
    private string _lastMoveDebugForDiagnostics = string.Empty;
    private bool _lastRequestedJumpForDiagnostics;
    private string _lastJumpDebugForDiagnostics = string.Empty;
    private static readonly ConditionalWeakTable<SimpleLevel, ModernObstacleIndex> ModernObstacleIndexByLevel = new();
    private static readonly ConditionalWeakTable<SimpleLevel, Dictionary<int, ModernObstacleIndex>> ModernPlayerObstacleIndicesByLevel = new();
    private static readonly ConditionalWeakTable<SimpleLevel, ModernObstacleGrid> ModernObstacleGridByLevel = new();
    private static readonly ConditionalWeakTable<SimpleLevel, Dictionary<int, ModernObstacleGrid>> ModernPlayerObstacleGridsByLevel = new();
    public bool CollectDiagnostics { get; set; }

    public BotControllerDiagnosticsSnapshot LastDiagnostics { get; private set; } = BotControllerDiagnosticsSnapshot.Empty;

    public void Reset()
    {
        _memoryBySlot.Clear();
        lock (_navigationGraphLock)
        {
            _navigationGraphsByKey.Clear();
            _clientBotNavPointsByKey.Clear();
            _clientBotHintAssetsByKey.Clear();
            _clientBotScoreRouteAssetsByKey.Clear();
            _navigationGraphsByClassBuffer.Clear();
            _runtimeValidatedJumpTapeByKey.Clear();
        }
        _modernSpyVisibleTicksByPlayerId.Clear();
        LastDiagnostics = BotControllerDiagnosticsSnapshot.Empty;
    }

    public IReadOnlyDictionary<byte, PlayerInputSnapshot> BuildInputs(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots)
    {
        _inputsBuffer.Clear();
        if (controlledSlots.Count == 0)
        {
            LastDiagnostics = BotControllerDiagnosticsSnapshot.Empty;
            return _inputsBuffer;
        }

        PruneMemory(controlledSlots);

        var allPlayers = BuildPlayerRoster(world);
        var controlledPlayers = BuildControlledPlayerRoster(world, controlledSlots);
        var timing = CreateTimingProfile(world.Config);
        _modernSpyReactTicksThreshold = ScaleBotTicks(ModernSpyReactTimeSourceTicks, timing.TicksPerSecond);
        UpdateModernSpyVisibilityMemory(allPlayers);
        var diagnosticsEntries = CollectDiagnostics
            ? new List<BotControllerDiagnosticsEntry>(controlledPlayers.Count)
            : null;
        var aliveBotCount = 0;
        var visibleEnemyCount = 0;
        var healFocusCount = 0;
        var cabinetSeekCount = 0;
        var unstickCount = 0;

        foreach (var entry in controlledPlayers)
        {
            var slot = entry.Key;
            var player = entry.Value.Player;
            var memory = GetMemory(slot);
            BotNavigationRuntimeGraph? navigationGraph = null;
            const bool useModernNavigation = true;
            TickMemory(memory, player, timing, useModernNavigation);

            if (!player.IsAlive)
            {
                ResetTransientState(memory, keepObservedPosition: false);
                _inputsBuffer[slot] = default;
                if (diagnosticsEntries is not null)
                {
                    diagnosticsEntries.Add(CreateRespawningDiagnosticsEntry(entry.Value.ControlledSlot, player));
                }
                continue;
            }

            var role = ResolveModernBotRole(world, player, entry.Value.ControlledSlot.Team, allPlayers);
            _inputsBuffer[slot] = BuildInputForBot(
                world,
                entry.Value.ControlledSlot,
                player,
                allPlayers,
                navigationGraph,
                role,
                memory,
                timing,
                out var diagnosticsEntry);
            if (diagnosticsEntries is not null)
            {
                diagnosticsEntries.Add(diagnosticsEntry);
                aliveBotCount += 1;
                visibleEnemyCount += diagnosticsEntry.HasVisibleEnemy ? 1 : 0;
                healFocusCount += diagnosticsEntry.FocusKind == BotFocusKind.HealTarget ? 1 : 0;
                cabinetSeekCount += diagnosticsEntry.FocusKind == BotFocusKind.HealingCabinet ? 1 : 0;
                unstickCount += diagnosticsEntry.State == BotStateKind.Unstick ? 1 : 0;
            }
        }

        LastDiagnostics = diagnosticsEntries is null
            ? BotControllerDiagnosticsSnapshot.Empty
            : new BotControllerDiagnosticsSnapshot(
                diagnosticsEntries,
                aliveBotCount,
                visibleEnemyCount,
                healFocusCount,
                cabinetSeekCount,
                unstickCount);

        return _inputsBuffer;
    }

    private PlayerInputSnapshot BuildInputForBot(
        SimulationWorld world,
        ControlledBotSlot controlledSlot,
        PlayerEntity player,
        IReadOnlyList<PlayerEntity> allPlayers,
        BotNavigationRuntimeGraph? navigationGraph,
        BotRole role,
        BotMemory memory,
        BotTimingProfile timing,
        out BotControllerDiagnosticsEntry diagnosticsEntry)
    {
        return controlledSlot.PathMode switch
        {
            BotPathMode.ModernHybrid => BuildInputForModernHybridPath(
                world,
                controlledSlot,
                player,
                allPlayers,
                navigationGraph,
                role,
                memory,
                timing,
                out diagnosticsEntry),
            BotPathMode.ModernGraphRoute => BuildInputForModernGraphRoutePath(
                world,
                controlledSlot,
                player,
                allPlayers,
                role,
                memory,
                timing,
                out diagnosticsEntry),
            BotPathMode.ModernGraphValidator => BuildInputForModernGraphValidatorPath(
                world,
                controlledSlot,
                player,
                allPlayers,
                role,
                memory,
                timing,
                out diagnosticsEntry),
            BotPathMode.ClientBot2020Compat => BuildInputForClientBot2020CompatPath(
                world,
                controlledSlot,
                player,
                allPlayers,
                navigationGraph,
                role,
                memory,
                timing,
                out diagnosticsEntry),
            _ => BuildInputForModernHybridPath(
                world,
                controlledSlot,
                player,
                allPlayers,
                navigationGraph,
                role,
                memory,
                timing,
                out diagnosticsEntry),
        };
    }

    private PlayerInputSnapshot BuildInputForClientBot2020CompatPath(
        SimulationWorld world,
        ControlledBotSlot controlledSlot,
        PlayerEntity player,
        IReadOnlyList<PlayerEntity> allPlayers,
        BotNavigationRuntimeGraph? navigationGraph,
        BotRole role,
        BotMemory memory,
        BotTimingProfile timing,
        out BotControllerDiagnosticsEntry diagnosticsEntry)
    {
        var clientBotNavPoints = GetCompatNavPoints(world.Level);

        if (navigationGraph is not null
            && navigationGraph.BuildStrategy != BotNavigationBuildStrategy.ModernClientBotPointGraph)
        {
            ResetTransientState(memory, keepObservedPosition: false);
            diagnosticsEntry = CreateDiagnosticsEntry(
                world,
                controlledSlot,
                player,
                role,
                memory,
                healTarget: null,
                combatTarget: null,
                hasVisibleEnemy: false,
                isSeekingCabinet: false,
                destination: (player.X, player.Y),
                new NavigationDecision((player.X, player.Y), HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "compat-nav-missing"));
            return default;
        }

        EnsureModernCombatMemoryInitialized(memory, timing);
        EnsureClientBot2020CompatStateInitialized(memory);
        SyncMemoryToClientBot2020CompatState(memory);
        var healTarget = FindBestModernHealTarget(world, player, controlledSlot.Team, allPlayers, memory, timing);
        var medicBuddyTarget = FindBestClientBot2020CompatMedicBuddyTarget(player, controlledSlot.Team, allPlayers);
        var objectiveSelection = ResolveClientBot2020CompatPathSelection(
            world,
            player,
            controlledSlot.Team,
            allPlayers,
            role,
            medicBuddyTarget,
            clientBotNavPoints,
            memory,
            timing);
        var isSeekingCabinet = false;
        var destination = objectiveSelection.Destination;
        var navigationDecision = ResolveClientBot2020CompatNavigationDecision(
            world,
            player,
            destination,
            clientBotNavPoints,
            memory,
            timing,
            objectiveSelection);
        navigationDecision = ApplyClientBot2020CompatFrameState(
            world,
            player,
            destination,
            clientBotNavPoints,
            memory,
            navigationDecision);
        navigationDecision = ApplyClientBot2020CompatFallbackRoute(
            world,
            player,
            objectiveSelection,
            memory,
            timing,
            navigationDecision);
        var movementDestination = navigationDecision.MovementTarget;
        var combatTarget = ResolveClientBot2020CompatCombatTarget(world, player, controlledSlot.Team, allPlayers);
        var enemyTarget = combatTarget is { Kind: ModernCombatTargetKind.Player, Player: not null }
            ? combatTarget.Value.Player
            : null;
        var hasVisibleEnemy = combatTarget is not null;
        var isBeingHealed = IsPlayerBeingHealed(allPlayers, player);

        var aimTarget = ResolveClientBot2020CompatAimTarget(player, movementDestination, combatTarget, healTarget);
        var horizontal = ResolveClientBot2020CompatHorizontalMovement(
            world,
            player,
            objectiveSelection,
            clientBotNavPoints,
            memory,
            enemyTarget);
        var jump = ResolveClientBot2020CompatJump(
            world,
            player,
            navigationDecision,
            clientBotNavPoints,
            memory,
            timing,
            horizontal);
        ResolveClientBot2020CompatFire(
            world,
            player,
            allPlayers,
            combatTarget,
            healTarget,
            memory,
            isBeingHealed,
            timing,
            ref horizontal,
            ref jump,
            out var firePrimary,
            out var fireSecondary);
        var fireSecondaryWeapon = false;
        ApplyModernReloadDiscipline(player, memory, ref firePrimary, ref fireSecondary, ref fireSecondaryWeapon);
        UpdateModernCombatMemory(player, memory);
        var buildSentry = ResolveClientBot2020CompatBuildSentry(world, player, destination);
        var dropIntel = ResolveClientBot2020CompatDropIntel(player, medicBuddyTarget);

        FinalizeClientBot2020CompatFrameState(memory);
        memory.LastRequestedHorizontal = horizontal;
        _lastRequestedHorizontalForDiagnostics = horizontal;
        _lastMoveDebugForDiagnostics = memory.ModernMoveDebug;
        _lastRequestedJumpForDiagnostics = jump;
        _lastJumpDebugForDiagnostics = memory.ModernJumpDebug;
        diagnosticsEntry = CreateDiagnosticsEntry(
            world,
            controlledSlot,
            player,
            role,
            memory,
            healTarget,
            combatTarget,
            hasVisibleEnemy,
            isSeekingCabinet,
            movementDestination,
            navigationDecision);

        return new PlayerInputSnapshot(
            Left: horizontal < 0,
            Right: horizontal > 0,
            Up: jump,
            Down: false,
            BuildSentry: buildSentry,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: firePrimary,
            FireSecondary: fireSecondary,
            AimWorldX: aimTarget.X,
            AimWorldY: aimTarget.Y,
            DebugKill: false,
            DropIntel: dropIntel,
            FireSecondaryWeapon: fireSecondaryWeapon);
    }

    private PlayerInputSnapshot BuildInputForModernHybridPath(
        SimulationWorld world,
        ControlledBotSlot controlledSlot,
        PlayerEntity player,
        IReadOnlyList<PlayerEntity> allPlayers,
        BotNavigationRuntimeGraph? navigationGraph,
        BotRole role,
        BotMemory memory,
        BotTimingProfile timing,
        out BotControllerDiagnosticsEntry diagnosticsEntry)
    {
        var clientBotNavPoints = GetOrCreateClientBotNavPoints(world.Level);

        if (navigationGraph is not null
            && navigationGraph.BuildStrategy != BotNavigationBuildStrategy.ModernClientBotPointGraph)
        {
            ResetTransientState(memory, keepObservedPosition: false);
            diagnosticsEntry = CreateDiagnosticsEntry(
                world,
                controlledSlot,
                player,
                role,
                memory,
                healTarget: null,
                combatTarget: null,
                hasVisibleEnemy: false,
                isSeekingCabinet: false,
                destination: (player.X, player.Y),
                new NavigationDecision((player.X, player.Y), HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "modern-nav-missing"));
            return default;
        }

        const bool useModernBehavior = true;
        EnsureModernCombatMemoryInitialized(memory, timing);
        var healTarget = FindBestHealTarget(world, player, controlledSlot.Team, allPlayers, memory, timing, useModernBehavior);
        var medicBuddyTarget = useModernBehavior
            ? FindBestModernMedicBuddyTarget(player, controlledSlot.Team, allPlayers)
            : null;
        var objectiveSelection = ResolveModernPathSelection(
            world,
            player,
            controlledSlot.Team,
            allPlayers,
            role,
            medicBuddyTarget);
        var isSeekingCabinet = false;
        var destination = objectiveSelection.Destination;
        var allowModernDirectPath = objectiveSelection.AllowDirectPath;
        var navigationDecision = ResolveNavigationDecision(world, player, controlledSlot.ClassId, destination, navigationGraph, clientBotNavPoints, memory, timing, allowModernDirectPath, objectiveSelection);
        var movementDestination = navigationDecision.MovementTarget;
        var enemyTarget = FindBestEnemyTarget(world, player, controlledSlot.Team, role, destination, allPlayers, memory, timing, useModernBehavior);
        var combatTarget = useModernBehavior
            ? FindBestModernCombatTarget(world, player, controlledSlot.Team, allPlayers)
            : enemyTarget is not null
                ? new ModernCombatTarget(ModernCombatTargetKind.Player, enemyTarget.Team, enemyTarget.X, enemyTarget.Y, Player: enemyTarget)
                : null;
        var hasVisibleEnemy = combatTarget is not null;
        var isBeingHealed = IsPlayerBeingHealed(allPlayers, player);

        var aimTarget = ResolveAimTarget(world, player, movementDestination, combatTarget, healTarget, useModernBehavior);
        var horizontal = ResolveHorizontalMovement(
            world,
            player,
            movementDestination,
            objectiveSelection,
            enemyTarget,
            healTarget,
            hasVisibleEnemy,
            navigationDecision,
            navigationGraph,
            clientBotNavPoints,
            memory,
            timing);
        var jump = ResolveJump(
            world,
            player,
            movementDestination,
            enemyTarget,
            healTarget,
            horizontal,
            hasVisibleEnemy,
            navigationDecision,
            navigationGraph,
            clientBotNavPoints,
            memory,
            timing);
        var firePrimary = ResolvePrimaryFire(world, player, combatTarget, healTarget, memory, useModernBehavior, isBeingHealed);
        var fireSecondary = ResolveSecondaryFire(world, player, combatTarget, healTarget, allPlayers, memory, hasVisibleEnemy, useModernBehavior, isBeingHealed);
        var fireSecondaryWeapon = ResolveSecondaryWeaponFireFromLoadout(
            player,
            combatTarget?.X,
            combatTarget?.Y,
            healTarget,
            firePrimary,
            fireSecondary);
        if (useModernBehavior)
        {
            ApplyModernSoldierCloseRangeAdjustment(world, player, combatTarget, memory, firePrimary, timing, ref horizontal, ref jump);
            ApplyModernReloadDiscipline(player, memory, ref firePrimary, ref fireSecondary, ref fireSecondaryWeapon);
            UpdateModernCombatMemory(player, memory);
        }
        var buildSentry = ResolveBuildSentry(world, player, destination);
        var dropIntel = ResolveDropIntel(player, medicBuddyTarget);

        CommitModernFrameState(clientBotNavPoints is not null, memory);
        memory.LastRequestedHorizontal = horizontal;
        _lastRequestedHorizontalForDiagnostics = horizontal;
        _lastMoveDebugForDiagnostics = memory.ModernMoveDebug;
        _lastRequestedJumpForDiagnostics = jump;
        _lastJumpDebugForDiagnostics = memory.ModernJumpDebug;
        diagnosticsEntry = CreateDiagnosticsEntry(
            world,
            controlledSlot,
            player,
            role,
            memory,
            healTarget,
            combatTarget,
            hasVisibleEnemy,
            isSeekingCabinet,
            movementDestination,
            navigationDecision);

        return new PlayerInputSnapshot(
            Left: horizontal < 0,
            Right: horizontal > 0,
            Up: jump,
            Down: false,
            BuildSentry: buildSentry,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: firePrimary,
            FireSecondary: fireSecondary,
            AimWorldX: aimTarget.X,
            AimWorldY: aimTarget.Y,
            DebugKill: false,
            DropIntel: dropIntel,
            FireSecondaryWeapon: fireSecondaryWeapon);
    }

    private PlayerInputSnapshot BuildInputForModernGraphRoutePath(
        SimulationWorld world,
        ControlledBotSlot controlledSlot,
        PlayerEntity player,
        IReadOnlyList<PlayerEntity> allPlayers,
        BotRole role,
        BotMemory memory,
        BotTimingProfile timing,
        out BotControllerDiagnosticsEntry diagnosticsEntry)
    {
        var navigationGraph = GetCompatNavigationGraph(world.Level);
        if (navigationGraph is null
            || navigationGraph.BuildStrategy != BotNavigationBuildStrategy.ModernClientBotPointGraph)
        {
            ResetTransientState(memory, keepObservedPosition: false);
            diagnosticsEntry = CreateDiagnosticsEntry(
                world,
                controlledSlot,
                player,
                role,
                memory,
                healTarget: null,
                combatTarget: null,
                hasVisibleEnemy: false,
                isSeekingCabinet: false,
                destination: (player.X, player.Y),
                new NavigationDecision((player.X, player.Y), HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "graph-nav-missing"));
            return default;
        }

        const bool useModernBehavior = true;
        EnsureModernCombatMemoryInitialized(memory, timing);
        var healTarget = FindBestHealTarget(world, player, controlledSlot.Team, allPlayers, memory, timing, useModernBehavior);
        var medicBuddyTarget = FindBestModernMedicBuddyTarget(player, controlledSlot.Team, allPlayers);
        var objectiveSelection = ResolveModernPathSelection(
            world,
            player,
            controlledSlot.Team,
            allPlayers,
            role,
            medicBuddyTarget);
        var isSeekingCabinet = false;
        var destination = objectiveSelection.Destination;
        var navigationDecision = ResolveNavigationDecision(
            world,
            player,
            controlledSlot.ClassId,
            destination,
            navigationGraph,
            clientBotNavPoints: null,
            memory,
            timing,
            allowModernDirectPath: false,
            objectiveSelection);
        var movementDestination = navigationDecision.MovementTarget;
        var enemyTarget = FindBestEnemyTarget(world, player, controlledSlot.Team, role, destination, allPlayers, memory, timing, useModernBehavior);
        var combatTarget = FindBestModernCombatTarget(world, player, controlledSlot.Team, allPlayers);
        var hasVisibleEnemy = combatTarget is not null;
        var isBeingHealed = IsPlayerBeingHealed(allPlayers, player);

        var aimTarget = ResolveAimTarget(world, player, movementDestination, combatTarget, healTarget, useModernBehavior);
        var horizontal = ResolveHorizontalMovement(
            world,
            player,
            movementDestination,
            objectiveSelection,
            enemyTarget,
            healTarget,
            hasVisibleEnemy,
            navigationDecision,
            navigationGraph,
            clientBotNavPoints: null,
            memory,
            timing);
        var jump = ResolveJump(
            world,
            player,
            movementDestination,
            enemyTarget,
            healTarget,
            horizontal,
            hasVisibleEnemy,
            navigationDecision,
            navigationGraph,
            clientBotNavPoints: null,
            memory,
            timing);
        var firePrimary = ResolvePrimaryFire(world, player, combatTarget, healTarget, memory, useModernBehavior, isBeingHealed);
        var fireSecondary = ResolveSecondaryFire(world, player, combatTarget, healTarget, allPlayers, memory, hasVisibleEnemy, useModernBehavior, isBeingHealed);
        var fireSecondaryWeapon = ResolveSecondaryWeaponFireFromLoadout(
            player,
            combatTarget?.X,
            combatTarget?.Y,
            healTarget,
            firePrimary,
            fireSecondary);
        ApplyModernSoldierCloseRangeAdjustment(world, player, combatTarget, memory, firePrimary, timing, ref horizontal, ref jump);
        ApplyModernReloadDiscipline(player, memory, ref firePrimary, ref fireSecondary, ref fireSecondaryWeapon);
        UpdateModernCombatMemory(player, memory);
        var buildSentry = ResolveBuildSentry(world, player, destination);
        var dropIntel = ResolveDropIntel(player, medicBuddyTarget);

        CommitModernFrameState(useModernClientBotPath: false, memory);
        memory.LastRequestedHorizontal = horizontal;
        _lastRequestedHorizontalForDiagnostics = horizontal;
        _lastMoveDebugForDiagnostics = memory.ModernMoveDebug;
        _lastRequestedJumpForDiagnostics = jump;
        _lastJumpDebugForDiagnostics = memory.ModernJumpDebug;
        diagnosticsEntry = CreateDiagnosticsEntry(
            world,
            controlledSlot,
            player,
            role,
            memory,
            healTarget,
            combatTarget,
            hasVisibleEnemy,
            isSeekingCabinet,
            movementDestination,
            navigationDecision);

        return new PlayerInputSnapshot(
            Left: horizontal < 0,
            Right: horizontal > 0,
            Up: jump,
            Down: false,
            BuildSentry: buildSentry,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: firePrimary,
            FireSecondary: fireSecondary,
            AimWorldX: aimTarget.X,
            AimWorldY: aimTarget.Y,
            DebugKill: false,
            DropIntel: dropIntel,
            FireSecondaryWeapon: fireSecondaryWeapon);
    }

    private PlayerInputSnapshot BuildInputForModernGraphValidatorPath(
        SimulationWorld world,
        ControlledBotSlot controlledSlot,
        PlayerEntity player,
        IReadOnlyList<PlayerEntity> allPlayers,
        BotRole role,
        BotMemory memory,
        BotTimingProfile timing,
        out BotControllerDiagnosticsEntry diagnosticsEntry)
    {
        var navigationGraph = GetCompatNavigationGraph(world.Level);
        if (navigationGraph is null
            || navigationGraph.BuildStrategy != BotNavigationBuildStrategy.ModernClientBotPointGraph)
        {
            ResetTransientState(memory, keepObservedPosition: false);
            diagnosticsEntry = CreateDiagnosticsEntry(
                world,
                controlledSlot,
                player,
                role,
                memory,
                healTarget: null,
                combatTarget: null,
                hasVisibleEnemy: false,
                isSeekingCabinet: false,
                destination: (player.X, player.Y),
                new NavigationDecision((player.X, player.Y), HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "validator-nav-missing"));
            return default;
        }

        const bool useModernBehavior = true;
        EnsureModernCombatMemoryInitialized(memory, timing);
        var healTarget = FindBestHealTarget(world, player, controlledSlot.Team, allPlayers, memory, timing, useModernBehavior);
        var medicBuddyTarget = FindBestModernMedicBuddyTarget(player, controlledSlot.Team, allPlayers);
        var objectiveSelection = ResolveModernPathSelection(
            world,
            player,
            controlledSlot.Team,
            allPlayers,
            role,
            medicBuddyTarget);
        var isSeekingCabinet = false;
        var destination = objectiveSelection.Destination;
        var navigationDecision = ResolveValidatedGraphNavigationDecision(
            world,
            player,
            controlledSlot.ClassId,
            destination,
            navigationGraph,
            memory,
            timing,
            objectiveSelection);
        var movementDestination = navigationDecision.MovementTarget;
        var enemyTarget = FindBestEnemyTarget(world, player, controlledSlot.Team, role, destination, allPlayers, memory, timing, useModernBehavior);
        var combatTarget = FindBestModernCombatTarget(world, player, controlledSlot.Team, allPlayers);
        var hasVisibleEnemy = combatTarget is not null;
        var isBeingHealed = IsPlayerBeingHealed(allPlayers, player);

        var aimTarget = ResolveAimTarget(world, player, movementDestination, combatTarget, healTarget, useModernBehavior);
        var horizontal = ResolveHorizontalMovement(
            world,
            player,
            movementDestination,
            objectiveSelection,
            enemyTarget,
            healTarget,
            hasVisibleEnemy,
            navigationDecision,
            navigationGraph,
            clientBotNavPoints: null,
            memory,
            timing);
        var jump = ResolveJump(
            world,
            player,
            movementDestination,
            enemyTarget,
            healTarget,
            horizontal,
            hasVisibleEnemy,
            navigationDecision,
            navigationGraph,
            clientBotNavPoints: null,
            memory,
            timing);
        var firePrimary = ResolvePrimaryFire(world, player, combatTarget, healTarget, memory, useModernBehavior, isBeingHealed);
        var fireSecondary = ResolveSecondaryFire(world, player, combatTarget, healTarget, allPlayers, memory, hasVisibleEnemy, useModernBehavior, isBeingHealed);
        var fireSecondaryWeapon = ResolveSecondaryWeaponFireFromLoadout(
            player,
            combatTarget?.X,
            combatTarget?.Y,
            healTarget,
            firePrimary,
            fireSecondary);
        ApplyModernSoldierCloseRangeAdjustment(world, player, combatTarget, memory, firePrimary, timing, ref horizontal, ref jump);
        ApplyModernReloadDiscipline(player, memory, ref firePrimary, ref fireSecondary, ref fireSecondaryWeapon);
        UpdateModernCombatMemory(player, memory);
        var buildSentry = ResolveBuildSentry(world, player, destination);
        var dropIntel = ResolveDropIntel(player, medicBuddyTarget);

        CommitModernFrameState(useModernClientBotPath: false, memory);
        memory.LastRequestedHorizontal = horizontal;
        _lastRequestedHorizontalForDiagnostics = horizontal;
        _lastMoveDebugForDiagnostics = memory.ModernMoveDebug;
        _lastRequestedJumpForDiagnostics = jump;
        _lastJumpDebugForDiagnostics = memory.ModernJumpDebug;
        diagnosticsEntry = CreateDiagnosticsEntry(
            world,
            controlledSlot,
            player,
            role,
            memory,
            healTarget,
            combatTarget,
            hasVisibleEnemy,
            isSeekingCabinet,
            movementDestination,
            navigationDecision);

        return new PlayerInputSnapshot(
            Left: horizontal < 0,
            Right: horizontal > 0,
            Up: jump,
            Down: false,
            BuildSentry: buildSentry,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: firePrimary,
            FireSecondary: fireSecondary,
            AimWorldX: aimTarget.X,
            AimWorldY: aimTarget.Y,
            DebugKill: false,
            DropIntel: dropIntel,
            FireSecondaryWeapon: fireSecondaryWeapon);
    }

    private Dictionary<byte, ControlledPlayerState> BuildControlledPlayerRoster(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots)
    {
        _controlledPlayersBuffer.Clear();
        foreach (var entry in controlledSlots)
        {
            if (!world.TryGetNetworkPlayer(entry.Key, out var player)
                || world.IsNetworkPlayerAwaitingJoin(entry.Key))
            {
                continue;
            }

            _controlledPlayersBuffer[entry.Key] = new ControlledPlayerState(entry.Value, player);
        }

        return _controlledPlayersBuffer;
    }

    private List<PlayerEntity> BuildPlayerRoster(SimulationWorld world)
    {
        _allPlayersBuffer.Clear();
        foreach (var entry in world.EnumerateActiveNetworkPlayers())
        {
            _allPlayersBuffer.Add(entry.Player);
        }

        if (world.EnemyPlayerEnabled)
        {
            _allPlayersBuffer.Add(world.EnemyPlayer);
        }

        if (world.FriendlyDummyEnabled)
        {
            _allPlayersBuffer.Add(world.FriendlyDummy);
        }

        return _allPlayersBuffer;
    }

    private static PlayerEntity? FindCarrier(IEnumerable<PlayerEntity> players)
    {
        foreach (var player in players)
        {
            if (player.IsAlive && player.IsCarryingIntel)
            {
                return player;
            }
        }

        return null;
    }

    private static PlayerEntity? FindEnemyCarrier(SimulationWorld world, PlayerTeam team)
    {
        foreach (var entry in world.EnumerateActiveNetworkPlayers())
        {
            if (entry.Player.IsAlive
                && entry.Player.Team != team
                && entry.Player.IsCarryingIntel)
            {
                return entry.Player;
            }
        }

        if (world.EnemyPlayerEnabled
            && world.EnemyPlayer.IsAlive
            && world.EnemyPlayer.Team != team
            && world.EnemyPlayer.IsCarryingIntel)
        {
            return world.EnemyPlayer;
        }

        if (world.FriendlyDummyEnabled
            && world.FriendlyDummy.IsAlive
            && world.FriendlyDummy.Team != team
            && world.FriendlyDummy.IsCarryingIntel)
        {
            return world.FriendlyDummy;
        }

        return null;
    }

    private ClientBotNavPoints GetOrCreateClientBotNavPoints(SimpleLevel level)
    {
        ClientBotNavMeshResolver.TryResolveCacheKey(level, out var cacheKey, out var originalMeshPath);
        lock (_navigationGraphLock)
        {
            if (!_clientBotNavPointsByKey.TryGetValue(cacheKey, out var navPoints))
            {
                navPoints = string.IsNullOrWhiteSpace(originalMeshPath)
                    ? ClientBotNavPoints.Build(level)
                    : OriginalClientBotNavMeshStore.Load(level, originalMeshPath);
                _clientBotNavPointsByKey[cacheKey] = navPoints;
            }

            return navPoints;
        }
    }

    private ClientBotNavPoints GetOrCreateDiscoveredClientBotNavPoints(SimpleLevel level)
    {
        var cacheKey = $"{level.Name}|{level.MapAreaIndex}|clientbot-navpoints|{level.Bounds.Width}x{level.Bounds.Height}|{level.Solids.Count}";
        lock (_navigationGraphLock)
        {
            if (!_clientBotNavPointsByKey.TryGetValue(cacheKey, out var navPoints))
            {
                navPoints = ClientBotNavPoints.Build(level);
                _clientBotNavPointsByKey[cacheKey] = navPoints;
            }

            return navPoints;
        }
    }

    private ClientBotNavPoints GetOrCreateClientBotNavPointsFromGeneratedAsset(SimpleLevel level)
    {
        var loadResult = BotNavigationAssetStore.LoadForLevel(
            level,
            useModernRuntimeGeneration: true,
            allowSynchronousGeneration: true,
            preferFreshModernGeneration: false);
        var asset = loadResult.Assets.Values.FirstOrDefault();
        return asset is null
            ? GetOrCreateDiscoveredClientBotNavPoints(level)
            : GetOrCreateClientBotNavPointsFromAsset(asset, level);
    }

    private ClientBotNavPoints GetOrCreateClientBotNavPointsFromFreshGeneratedAsset(SimpleLevel level)
    {
        var fingerprint = BotNavigationLevelFingerprint.Compute(level);
        var cacheKey = $"{level.Name}|{level.MapAreaIndex}|clientbot-navpoints-generated-fresh|{fingerprint}";
        lock (_navigationGraphLock)
        {
            if (_clientBotNavPointsByKey.TryGetValue(cacheKey, out var cachedNavPoints))
            {
                return cachedNavPoints;
            }
        }

        var loadResult = BotNavigationAssetStore.LoadForLevel(
            level,
            useModernRuntimeGeneration: true,
            allowSynchronousGeneration: true,
            preferFreshModernGeneration: true);
        var asset = loadResult.Assets.Values.FirstOrDefault();
        var navPoints = asset is null
            ? GetOrCreateDiscoveredClientBotNavPoints(level)
            : GetOrCreateClientBotNavPointsFromAsset(asset, level);
        lock (_navigationGraphLock)
        {
            _clientBotNavPointsByKey[cacheKey] = navPoints;
        }

        return navPoints;
    }

    private ClientBotNavPoints GetOrCreateClientBotNavPointsFromShippedModernAsset(SimpleLevel level)
    {
        return BotNavigationAssetStore.TryLoadModernShippedAsset(level, out var asset, out _, out _, out _)
            && asset is not null
            ? GetOrCreateClientBotNavPointsFromAsset(asset, level)
            : GetOrCreateDiscoveredClientBotNavPoints(level);
    }

    private ClientBotNavPoints GetCompatNavPoints(SimpleLevel level)
    {
        return CompatNavPointSourceOverride switch
        {
            CompatNavPointSource.DiscoveredLevel => GetOrCreateDiscoveredClientBotNavPoints(level),
            CompatNavPointSource.GeneratedAssetCached => GetOrCreateClientBotNavPointsFromGeneratedAsset(level),
            CompatNavPointSource.GeneratedAssetFresh => GetOrCreateClientBotNavPointsFromFreshGeneratedAsset(level),
            CompatNavPointSource.ShippedModernAsset => GetOrCreateClientBotNavPointsFromShippedModernAsset(level),
            _ => GetOrCreateClientBotNavPointsFromFreshGeneratedAsset(level),
        };
    }

    private BotNavigationRuntimeGraph? GetCompatNavigationGraph(SimpleLevel level)
    {
        return CompatNavPointSourceOverride switch
        {
            CompatNavPointSource.GeneratedAssetCached => GetOrCreateNavigationGraphFromGeneratedAsset(level, preferFreshModernGeneration: false),
            CompatNavPointSource.ShippedModernAsset => GetOrCreateNavigationGraphFromShippedModernAsset(level),
            _ => GetOrCreateNavigationGraphFromGeneratedAsset(level, preferFreshModernGeneration: true),
        };
    }

    private BotNavigationRuntimeGraph? GetOrCreateNavigationGraphFromGeneratedAsset(SimpleLevel level, bool preferFreshModernGeneration)
    {
        var fingerprint = BotNavigationLevelFingerprint.Compute(level);
        var cacheKey = $"{level.Name}|{level.MapAreaIndex}|modern-runtime-graph|{fingerprint}|fresh:{preferFreshModernGeneration}";
        lock (_navigationGraphLock)
        {
            if (_navigationGraphsByKey.TryGetValue(cacheKey, out var cachedGraph))
            {
                return cachedGraph;
            }
        }

        var loadResult = BotNavigationAssetStore.LoadForLevel(
            level,
            useModernRuntimeGeneration: true,
            allowSynchronousGeneration: true,
            preferFreshModernGeneration: preferFreshModernGeneration);
        var asset = loadResult.Assets.Values.FirstOrDefault();
        if (asset is null)
        {
            return null;
        }

        var graph = new BotNavigationRuntimeGraph(asset);
        lock (_navigationGraphLock)
        {
            _navigationGraphsByKey[cacheKey] = graph;
        }

        return graph;
    }

    private BotNavigationRuntimeGraph? GetOrCreateNavigationGraphFromShippedModernAsset(SimpleLevel level)
    {
        if (!BotNavigationAssetStore.TryLoadModernShippedAsset(level, out var asset, out _, out _, out _)
            || asset is null)
        {
            return GetOrCreateNavigationGraphFromGeneratedAsset(level, preferFreshModernGeneration: true);
        }

        var cacheKey = $"{asset.LevelName}|{asset.MapAreaIndex}|modern-runtime-graph-shipped|{asset.LevelFingerprint}|{asset.Nodes.Count}|{asset.Edges.Count}|{asset.BuiltUtc.Ticks}";
        lock (_navigationGraphLock)
        {
            if (!_navigationGraphsByKey.TryGetValue(cacheKey, out var graph))
            {
                graph = new BotNavigationRuntimeGraph(asset);
                _navigationGraphsByKey[cacheKey] = graph;
            }

            return graph;
        }
    }

    private BotNavigationHintAsset? GetCompatHintAsset(SimpleLevel level)
    {
        var cacheKey = $"{level.Name}|{level.MapAreaIndex}|botnavhints";
        lock (_navigationGraphLock)
        {
            if (!_clientBotHintAssetsByKey.TryGetValue(cacheKey, out var hintAsset))
            {
                hintAsset = BotNavigationHintStore.Load(level);
                _clientBotHintAssetsByKey[cacheKey] = hintAsset;
            }

            return hintAsset;
        }
    }

    private BotNavigationScoreRouteAsset? GetScoreRouteAsset(SimpleLevel level)
    {
        var cacheKey = $"{level.Name}|{level.MapAreaIndex}|botnavscore";
        lock (_navigationGraphLock)
        {
            if (!_clientBotScoreRouteAssetsByKey.TryGetValue(cacheKey, out var scoreRouteAsset))
            {
                scoreRouteAsset = BotNavigationScoreRouteStore.Load(level);
                _clientBotScoreRouteAssetsByKey[cacheKey] = scoreRouteAsset;
            }

            return scoreRouteAsset;
        }
    }

    private ClientBotNavPoints GetOrCreateClientBotNavPointsFromAsset(BotNavigationAsset asset, SimpleLevel level)
    {
        var cacheKey = $"{asset.LevelName}|{asset.MapAreaIndex}|clientbot-navpoints-asset|{asset.LevelFingerprint}|{asset.Nodes.Count}|{asset.Edges.Count}";
        lock (_navigationGraphLock)
        {
            if (!_clientBotNavPointsByKey.TryGetValue(cacheKey, out var navPoints))
            {
                navPoints = ClientBotNavPoints.Build(asset, level);
                _clientBotNavPointsByKey[cacheKey] = navPoints;
            }

            return navPoints;
        }
    }

    private NavigationDecision ResolveValidatedGraphNavigationDecision(
        SimulationWorld world,
        PlayerEntity player,
        PlayerClass classId,
        (float X, float Y) destination,
        BotNavigationRuntimeGraph navigationGraph,
        BotMemory memory,
        BotTimingProfile timing,
        ModernPathSelection objectiveSelection)
    {
        var captureHoldState = ResolveModernCaptureHoldState(world, player, objectiveSelection, memory, timing.FixedDeltaSeconds);
        if (captureHoldState.SuppressNavigation)
        {
            ClearTraversalExecution(memory);
            ResetRouteProgress(memory);
            SetModernClosestPointTarget(memory, captureHoldState.TargetX, captureHoldState.TargetY);
            return new NavigationDecision(
                (captureHoldState.TargetX, captureHoldState.TargetY),
                HasRoute: true,
                ForcedHorizontalDirection: 0,
                ForceJump: false,
                LocksMovement: false,
                Label: "capture_zone_hold",
                MovementTargetUsesFeetCoordinates: true,
                CaptureHoldActive: true);
        }

        if (ShouldUseModernGraphIntelReturnFinishOverride(world, player, destination, memory))
        {
            ClearNavigationRoute(memory);
            SetModernClosestPointTarget(memory, destination.X, destination.Y);
            return new NavigationDecision(
                destination,
                HasRoute: false,
                ForcedHorizontalDirection: 0,
                ForceJump: false,
                LocksMovement: false,
                Label: "intel_home_direct");
        }

        var playerFeet = GetModernPlayerFeetY(player);
        var hasStartNode = TryFindModernRouteStartNode(world, player, navigationGraph, destination, out var startNode);

        var hasExactGoalNode = TryFindModernRouteGoalNode(world, player, navigationGraph, destination, out var goalNode);
        var exactGoalNodeId = hasExactGoalNode ? goalNode.Id : -1;
        var goalMoved = destination.X != memory.RouteGoalX
            || destination.Y != memory.RouteGoalY;
        var routeGoalChanged = memory.RouteGoalNodeId != exactGoalNodeId
            || goalMoved;
        if (memory.RouteRefreshTicks > 0)
        {
            memory.RouteRefreshTicks -= 1;
        }

        Func<BotNavigationEdge, bool> edgeFilter = edge => CanUseValidatedGraphRouteEdge(world, player, classId, navigationGraph, edge, memory);
        var hasCurrentRouteNode = TryGetCurrentRouteNode(navigationGraph, memory, out _);
        var holdCurrentGraphRoute = !player.IsGrounded
            || memory.RouteIsPartial
            || memory.ActiveTraversalTape is not null;
        var deferRouteGoalRepath = holdCurrentGraphRoute
            && hasCurrentRouteNode
            && ShouldDelayAirborneJumpChainHandoffForRoute(classId, objectiveSelection);
        if (!hasStartNode && !(holdCurrentGraphRoute && hasCurrentRouteNode))
        {
            ClearNavigationRoute(memory);
            return new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "start-miss");
        }

        var requiresRepath = memory.RouteNodeIds is null
            || memory.RouteNodeIds.Length <= 1
            || !string.Equals(memory.NavigationGraphKey, navigationGraph.CacheKey, StringComparison.Ordinal)
            || (!deferRouteGoalRepath && routeGoalChanged)
            || !hasCurrentRouteNode
            || memory.RouteRefreshTicks <= 0;
        if (requiresRepath)
        {
            if (!hasStartNode)
            {
                ClearNavigationRoute(memory);
                return new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "start-miss");
            }

            var resetObjectiveProgress = memory.RouteNodeIds is null
                || memory.RouteNodeIds.Length <= 1
                || !string.Equals(memory.NavigationGraphKey, navigationGraph.CacheKey, StringComparison.Ordinal)
                || routeGoalChanged;
            if (!TryBuildValidatedRouteToDestination(
                    navigationGraph,
                    startNode.Id,
                    exactGoalNodeId,
                    destination,
                    edgeFilter,
                    out var route,
                    out var routeIsPartial))
            {
                ClearNavigationRoute(memory);
                memory.NavigationGraphKey = navigationGraph.CacheKey;
                memory.RouteGoalNodeId = exactGoalNodeId;
                memory.RouteGoalX = destination.X;
                memory.RouteGoalY = destination.Y;
                memory.RouteRefreshTicks = Math.Max(1, timing.RouteRefreshTicks / 2);
                return new NavigationDecision(
                    destination,
                    HasRoute: false,
                    ForcedHorizontalDirection: 0,
                    ForceJump: false,
                    LocksMovement: false,
                    Label: hasExactGoalNode ? "route-miss" : "goal-miss");
            }

            memory.NavigationGraphKey = navigationGraph.CacheKey;
            memory.RouteNodeIds = route;
            memory.RouteIndex = 1;
            memory.RouteGoalNodeId = exactGoalNodeId;
            memory.RouteGoalX = destination.X;
            memory.RouteGoalY = destination.Y;
            memory.RouteRefreshTicks = routeIsPartial
                ? Math.Max(1, timing.RouteRefreshTicks / 2)
                : timing.RouteRefreshTicks;
            memory.RouteIsPartial = routeIsPartial;
            if (resetObjectiveProgress)
            {
                ResetRouteProgress(memory);
            }
            else
            {
                ResetRouteEdgeProgress(memory);
            }
        }

        if (memory.RouteNodeIds is null || memory.RouteNodeIds.Length <= 1)
        {
            return new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "direct");
        }

        AdvanceRouteProgress(player, navigationGraph, memory, ShouldDelayAirborneJumpChainHandoffForRoute(classId, objectiveSelection));
        if (!TryGetCurrentRouteNode(navigationGraph, memory, out var nextNode))
        {
            ClearNavigationRoute(memory);
            return new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "route-end");
        }

        var previousNodeId = memory.RouteNodeIds[Math.Max(0, memory.RouteIndex - 1)];
        if (!navigationGraph.TryGetEdge(previousNodeId, nextNode.Id, out var edge))
        {
            ClearTraversalExecution(memory);
            return new NavigationDecision((nextNode.X, nextNode.Y), HasRoute: true, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: GetRouteLabel(memory, BotNavigationTraversalKind.Walk));
        }

        if (!TryGetValidatedEdgeForExecution(world, player, classId, navigationGraph, memory, edge, out var validatedEdge))
        {
            BlockRouteEdge(memory, previousNodeId, nextNode.Id);
            memory.NavigationGraphKey = navigationGraph.CacheKey;
            memory.RouteGoalNodeId = exactGoalNodeId;
            memory.RouteGoalX = destination.X;
            memory.RouteGoalY = destination.Y;
            memory.RouteRefreshTicks = Math.Max(1, timing.RouteRefreshTicks / 2);
            return new NavigationDecision(
                destination,
                HasRoute: false,
                ForcedHorizontalDirection: 0,
                ForceJump: false,
                LocksMovement: false,
                Label: "route-replan");
        }

        TryGetValidatedEdgeForExecution(world, player, classId, navigationGraph, memory, edge, out var executionEdge);

        memory.CurrentPointId = previousNodeId;
        memory.NextPointId = nextNode.Id;
        memory.NextPoint2Id = memory.RouteNodeIds is not null && memory.RouteIndex + 1 < memory.RouteNodeIds.Length
            ? memory.RouteNodeIds[memory.RouteIndex + 1]
            : -1;
        memory.NextPoint3Id = memory.RouteNodeIds is not null && memory.RouteIndex + 2 < memory.RouteNodeIds.Length
            ? memory.RouteNodeIds[memory.RouteIndex + 2]
            : -1;
        SetModernClosestPointTarget(memory, nextNode.X, nextNode.Y);
        UpdateModernTargetProgress(player, memory, (nextNode.X, nextNode.Y));
        if (ShouldBlockCurrentRouteEdge(player, navigationGraph, memory, previousNodeId, nextNode, validatedEdge, destination))
        {
            BlockRouteEdge(memory, previousNodeId, nextNode.Id);
            memory.NavigationGraphKey = navigationGraph.CacheKey;
            memory.RouteGoalNodeId = exactGoalNodeId;
            memory.RouteGoalX = destination.X;
            memory.RouteGoalY = destination.Y;
            memory.RouteRefreshTicks = Math.Max(1, timing.RouteRefreshTicks / 2);
            return new NavigationDecision(
                destination,
                HasRoute: false,
                ForcedHorizontalDirection: 0,
                ForceJump: false,
                LocksMovement: false,
                Label: "route-replan");
        }

        if (TryResolveTraversalDecision(
                world,
                player,
                destination,
                navigationGraph,
                memory,
                previousNodeId,
                nextNode,
                validatedEdge,
                timing.FixedDeltaSeconds,
                out var traversalDecision))
        {
            return traversalDecision;
        }

        var forceWalkRecoveryJump = ShouldForceWalkRouteRecoveryJump(player, navigationGraph, previousNodeId, nextNode, validatedEdge);
        return new NavigationDecision((nextNode.X, nextNode.Y), HasRoute: true, ForcedHorizontalDirection: 0, ForceJump: forceWalkRecoveryJump, LocksMovement: false, Label: GetRouteLabel(memory, validatedEdge.Kind), TraversalKind: validatedEdge.Kind);
    }

    private static bool TryBuildValidatedRouteToDestination(
        BotNavigationRuntimeGraph navigationGraph,
        int startNodeId,
        int exactGoalNodeId,
        (float X, float Y) destination,
        Func<BotNavigationEdge, bool> edgeFilter,
        out int[] route,
        out bool routeIsPartial)
    {
        route = Array.Empty<int>();
        routeIsPartial = false;
        if (exactGoalNodeId >= 0)
        {
            route = navigationGraph.FindRoute(startNodeId, exactGoalNodeId, edgeFilter) ?? Array.Empty<int>();
            if (route.Length > 1)
            {
                return true;
            }
        }

        if (navigationGraph.TryFindRouteToGoalRadius(
                startNodeId,
                destination.X,
                destination.Y,
                RouteGoalNodeSearchDistance,
                edgeFilter,
                out route,
                out _)
            && route.Length > 1)
        {
            return true;
        }

        if (navigationGraph.TryFindBestPartialRoute(
                startNodeId,
                destination.X,
                destination.Y,
                RoutePartialMinimumImprovementDistance,
                edgeFilter,
                out route,
                out _)
            && route.Length > 1)
        {
            routeIsPartial = true;
            return true;
        }

        route = Array.Empty<int>();
        return false;
    }

    private bool TryGetValidatedEdgeForExecution(
        SimulationWorld world,
        PlayerEntity player,
        PlayerClass classId,
        BotNavigationRuntimeGraph navigationGraph,
        BotMemory memory,
        BotNavigationEdge edge,
        out BotNavigationEdge validatedEdge)
    {
        validatedEdge = edge;
        if (edge.Kind != BotNavigationTraversalKind.Jump || edge.InputTape.Count > 0)
        {
            return true;
        }

        var requiresRuntimeProof = RequiresRuntimeJumpProof(world, player, navigationGraph, edge);
        var routeEdgeFailureCount = memory.RouteBlockedEdgeFailureCountsByKey is not null
            && memory.RouteBlockedEdgeFailureCountsByKey.TryGetValue(GetRouteEdgeKey(edge.FromNodeId, edge.ToNodeId), out var failureCount)
                ? failureCount
                : 0;
        if (routeEdgeFailureCount <= 0 && !requiresRuntimeProof)
        {
            return true;
        }

        if (!TryGetRuntimeValidatedJumpTape(world, player, classId, navigationGraph, edge.FromNodeId, edge.ToNodeId, out var runtimeTape))
        {
            return false;
        }

        if (routeEdgeFailureCount <= 0)
        {
            return true;
        }

        if (!ShouldUseRuntimeValidatedJumpTape(player, navigationGraph, edge.FromNodeId, runtimeTape))
        {
            return true;
        }

        validatedEdge = new BotNavigationEdge
        {
            FromNodeId = edge.FromNodeId,
            ToNodeId = edge.ToNodeId,
            Kind = edge.Kind,
            Cost = edge.Cost,
            InputTape = runtimeTape,
        };
        return true;
    }

    private static bool RequiresRuntimeJumpProof(
        SimulationWorld world,
        PlayerEntity player,
        BotNavigationRuntimeGraph navigationGraph,
        BotNavigationEdge edge)
    {
        if (!navigationGraph.TryGetNode(edge.FromNodeId, out var fromNode)
            || !navigationGraph.TryGetNode(edge.ToNodeId, out var toNode))
        {
            return false;
        }

        var fromFeetY = GetModernNodeFeetY(navigationGraph, fromNode);
        var toFeetY = GetModernNodeFeetY(navigationGraph, toNode);
        if (player.ClassId != PlayerClass.Scout
            && LineHitsSolid(world, player, fromNode.X, fromFeetY + 2f, toNode.X, toFeetY + 2f))
        {
            return false;
        }

        return fromFeetY - toFeetY >= 64f;
    }

    private static bool ShouldDelayAirborneJumpChainHandoffForRoute(
        PlayerClass classId,
        ModernPathSelection objectiveSelection)
    {
        return objectiveSelection.IsCaptureObjective
            && BotNavigationProfiles.GetProfileForClass(classId) != BotNavigationProfile.Light;
    }

    private static bool ShouldUseRuntimeValidatedJumpTape(
        PlayerEntity player,
        BotNavigationRuntimeGraph navigationGraph,
        int fromNodeId,
        IReadOnlyList<BotNavigationInputFrame> runtimeTape)
    {
        if (runtimeTape.Count == 0)
        {
            return false;
        }

        if (!navigationGraph.TryGetNode(fromNodeId, out var sourceNode))
        {
            return false;
        }

        if (!HasReachedTraversalWindow(
                player.X,
                player.Bottom,
                player.IsGrounded,
                sourceNode.X,
                sourceNode.Y,
                TraversalStartDistance,
                TraversalStartDistance,
                requireGroundedArrival: true))
        {
            return true;
        }

        var preJumpTicks = 0;
        for (var index = 0; index < runtimeTape.Count; index += 1)
        {
            var frame = runtimeTape[index];
            if (frame.Up)
            {
                break;
            }

            preJumpTicks += frame.Ticks > 0
                ? frame.Ticks
                : Math.Max(1, (int)Math.Round(frame.DurationSeconds * SimulationConfig.DefaultTicksPerSecond));
        }

        return preJumpTicks <= 4;
    }

    private bool CanUseValidatedGraphRouteEdge(
        SimulationWorld world,
        PlayerEntity player,
        PlayerClass classId,
        BotNavigationRuntimeGraph navigationGraph,
        BotNavigationEdge edge,
        BotMemory memory)
    {
        if ((!ShouldBypassIntelReturnRouteEdgeBlock(player, memory)
                && IsRouteEdgeTemporarilyBlocked(memory, edge.FromNodeId, edge.ToNodeId))
            || navigationGraph.IsReverseOnlyTraversalBlocked(edge.ToNodeId, edge.FromNodeId)
            || !navigationGraph.TryGetNode(edge.FromNodeId, out _)
            || !navigationGraph.TryGetNode(edge.ToNodeId, out _))
        {
            return false;
        }

        if (edge.Kind != BotNavigationTraversalKind.Jump)
        {
            return true;
        }

        if (edge.InputTape.Count > 0)
        {
            return true;
        }

        return true;
    }

    private bool TryGetRuntimeValidatedJumpTape(
        SimulationWorld world,
        PlayerEntity player,
        PlayerClass classId,
        BotNavigationRuntimeGraph navigationGraph,
        int fromNodeId,
        int toNodeId,
        out IReadOnlyList<BotNavigationInputFrame> tape)
    {
        tape = Array.Empty<BotNavigationInputFrame>();
        var cacheKey = BuildRuntimeValidatedJumpTapeKey(navigationGraph, classId, player.Team, fromNodeId, toNodeId);
        lock (_navigationGraphLock)
        {
            if (_runtimeValidatedJumpTapeByKey.TryGetValue(cacheKey, out var cachedTape))
            {
                if (cachedTape is { Count: > 0 })
                {
                    tape = cachedTape;
                    return true;
                }

                return false;
            }
        }

        if (!navigationGraph.TryGetNode(fromNodeId, out var fromNode)
            || !navigationGraph.TryGetNode(toNodeId, out var toNode))
        {
            lock (_navigationGraphLock)
            {
                _runtimeValidatedJumpTapeByKey[cacheKey] = null;
            }

            return false;
        }

        var classDefinition = BotNavigationClasses.GetDefinition(classId);
        var profile = BotNavigationProfiles.GetProfileForClass(classId);
        var sourceY = fromNode.Y - classDefinition.CollisionBottom;
        var targetY = toNode.Y - classDefinition.CollisionBottom;
        if (!BotNavigationMovementValidator.TryBuildJumpTape(
                world.Level,
                classDefinition,
                profile,
                fromNode.X,
                sourceY,
                toNode.X,
                targetY,
                player.Team,
                out var generatedTape,
                out _)
            || generatedTape.Count == 0)
        {
            lock (_navigationGraphLock)
            {
                _runtimeValidatedJumpTapeByKey[cacheKey] = null;
            }

            return false;
        }

        lock (_navigationGraphLock)
        {
            _runtimeValidatedJumpTapeByKey[cacheKey] = generatedTape;
        }

        tape = generatedTape;
        return true;
    }

    private static string BuildRuntimeValidatedJumpTapeKey(
        BotNavigationRuntimeGraph navigationGraph,
        PlayerClass classId,
        PlayerTeam team,
        int fromNodeId,
        int toNodeId)
    {
        return $"{navigationGraph.CacheKey}|{(int)classId}|{(int)team}|{fromNodeId}->{toNodeId}";
    }

    private NavigationDecision ResolveNavigationDecision(
        SimulationWorld world,
        PlayerEntity player,
        PlayerClass classId,
        (float X, float Y) destination,
        BotNavigationRuntimeGraph? navigationGraph,
        ClientBotNavPoints? clientBotNavPoints,
        BotMemory memory,
        BotTimingProfile timing,
        bool allowModernDirectPath,
        ModernPathSelection objectiveSelection)
    {
        if (clientBotNavPoints is not null
            && (navigationGraph is null
                || navigationGraph.BuildStrategy == BotNavigationBuildStrategy.ModernClientBotPointGraph))
        {
            return ResolveModernNavigationDecision(world, player, destination, clientBotNavPoints, memory, timing, allowModernDirectPath, objectiveSelection);
        }

        var captureHoldState = ResolveModernCaptureHoldState(world, player, objectiveSelection, memory, timing.FixedDeltaSeconds);
        if (captureHoldState.SuppressNavigation)
        {
            ClearTraversalExecution(memory);
            ResetRouteProgress(memory);
            SetModernClosestPointTarget(memory, captureHoldState.TargetX, captureHoldState.TargetY);
            return new NavigationDecision(
                (captureHoldState.TargetX, captureHoldState.TargetY),
                HasRoute: true,
                ForcedHorizontalDirection: 0,
                ForceJump: false,
                LocksMovement: false,
                Label: "capture_zone_hold",
                MovementTargetUsesFeetCoordinates: true,
                CaptureHoldActive: true);
        }

        if (navigationGraph is null)
        {
            ClearNavigationRoute(memory);
            return new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "direct");
        }

        if (ShouldUseModernGraphIntelReturnFinishOverride(world, player, destination, memory))
        {
            ClearNavigationRoute(memory);
            SetModernClosestPointTarget(memory, destination.X, destination.Y);
            return new NavigationDecision(
                destination,
                HasRoute: false,
                ForcedHorizontalDirection: 0,
                ForceJump: false,
                LocksMovement: false,
                Label: "intel_home_direct");
        }

        var preferredScoreRouteActive = TryApplyPreferredScoreRoute(
            world,
            player,
            classId,
            navigationGraph,
            objectiveSelection,
            memory,
            out var preferredScoreRouteEligible);
        if (preferredScoreRouteActive)
        {
            ResetPreferredScoreRouteMissTracking(memory);
        }

        var playerFeet = GetModernPlayerFeetY(player);
        var hasStartNode = TryFindModernRouteStartNode(world, player, navigationGraph, destination, out var startNode);

        var hasExactGoalNode = TryFindModernRouteGoalNode(world, player, navigationGraph, destination, out var goalNode);
        var exactGoalNodeId = hasExactGoalNode ? goalNode.Id : -1;
        var goalMoved = destination.X != memory.RouteGoalX
            || destination.Y != memory.RouteGoalY;
        var routeGoalChanged = memory.RouteGoalNodeId != exactGoalNodeId
            || goalMoved;
        if (memory.RouteRefreshTicks > 0)
        {
            memory.RouteRefreshTicks -= 1;
        }

        Func<BotNavigationEdge, bool> edgeFilter = edge => CanUseUniversalGraphRouteEdge(world, player, navigationGraph, edge, memory);
        var hasCurrentRouteNode = TryGetCurrentRouteNode(navigationGraph, memory, out var currentRouteNode);
        var hasCurrentRouteEdge = false;
        BotNavigationEdge currentRouteEdge = default!;
        if (hasCurrentRouteNode
            && memory.RouteNodeIds is not null
            && memory.RouteIndex > 0)
        {
            var currentRouteFromNodeId = memory.RouteNodeIds[Math.Max(0, memory.RouteIndex - 1)];
            hasCurrentRouteEdge = navigationGraph.TryGetEdge(currentRouteFromNodeId, currentRouteNode.Id, out currentRouteEdge);
        }

        var currentRouteDistanceExceeded = hasCurrentRouteNode
            && DistanceSquared(player.X, playerFeet, currentRouteNode.X, currentRouteNode.Y) > RouteRepathDistance * RouteRepathDistance;
        var holdCurrentGraphRoute = !player.IsGrounded
            || memory.RouteIsPartial
            || memory.ActiveTraversalTape is not null;
        var deferRouteGoalRepath = holdCurrentGraphRoute
            && hasCurrentRouteNode
            && ShouldDelayAirborneJumpChainHandoffForRoute(classId, objectiveSelection);
        if (!hasStartNode && !(holdCurrentGraphRoute && hasCurrentRouteNode))
        {
            ClearNavigationRoute(memory);
            return new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "start-miss");
        }

        var requiresRepath = !preferredScoreRouteActive
            && (memory.RouteNodeIds is null
                || memory.RouteNodeIds.Length == 0
                || !string.Equals(memory.NavigationGraphKey, navigationGraph.CacheKey, StringComparison.Ordinal)
                || (!deferRouteGoalRepath && routeGoalChanged)
                || (!holdCurrentGraphRoute && memory.RouteRefreshTicks <= 0)
                || !hasCurrentRouteNode
                || (!holdCurrentGraphRoute && currentRouteDistanceExceeded));

        if (requiresRepath)
        {
            if (!hasStartNode)
            {
                ClearNavigationRoute(memory);
                return new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "start-miss");
            }

            var resetObjectiveProgress = memory.RouteNodeIds is null
                || memory.RouteNodeIds.Length == 0
                || !string.Equals(memory.NavigationGraphKey, navigationGraph.CacheKey, StringComparison.Ordinal)
                || routeGoalChanged;
            if (!TryBuildRouteToDestination(
                    navigationGraph,
                    startNode.Id,
                    exactGoalNodeId,
                    destination,
                    edgeFilter,
                    edge => CanUseBroadFallbackGraphRouteEdge(player, navigationGraph, edge, memory),
                    out var route,
                    out var routeIsPartial))
            {
                if (hasExactGoalNode && preferredScoreRouteEligible)
                {
                    RegisterPreferredScoreRouteMiss(memory);
                }
                ClearNavigationRoute(memory);
                memory.NavigationGraphKey = navigationGraph.CacheKey;
                memory.RouteGoalNodeId = exactGoalNodeId;
                memory.RouteGoalX = destination.X;
                memory.RouteGoalY = destination.Y;
                memory.RouteRefreshTicks = routeIsPartial
                    ? Math.Max(timing.RouteRefreshTicks, timing.RouteRefreshTicks * 4)
                    : timing.RouteRefreshTicks;
                return new NavigationDecision(
                    destination,
                    HasRoute: false,
                    ForcedHorizontalDirection: 0,
                    ForceJump: false,
                    LocksMovement: false,
                    Label: hasExactGoalNode ? "route-miss" : "goal-miss");
            }

            memory.NavigationGraphKey = navigationGraph.CacheKey;
            memory.RouteNodeIds = route;
            memory.RouteIndex = 1;
            memory.RouteGoalNodeId = exactGoalNodeId;
            memory.RouteGoalX = destination.X;
            memory.RouteGoalY = destination.Y;
            memory.RouteRefreshTicks = routeIsPartial
                ? Math.Max(timing.RouteRefreshTicks, timing.RouteRefreshTicks * 4)
                : timing.RouteRefreshTicks;
            memory.RouteIsPartial = routeIsPartial;
            ResetPreferredScoreRouteMissTracking(memory);
            if (resetObjectiveProgress)
            {
                ResetRouteProgress(memory);
            }
            else
            {
                ResetRouteEdgeProgress(memory);
            }
        }

        if (memory.RouteNodeIds is null || memory.RouteNodeIds.Length <= 1)
        {
            return new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "direct");
        }

        AdvanceRouteProgress(player, navigationGraph, memory, ShouldDelayAirborneJumpChainHandoffForRoute(classId, objectiveSelection));
        if (!TryGetCurrentRouteNode(navigationGraph, memory, out var nextNode))
        {
            ClearNavigationRoute(memory);
            return new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "route-end");
        }

        var previousNodeId = memory.RouteNodeIds[Math.Max(0, memory.RouteIndex - 1)];
        if (!navigationGraph.TryGetEdge(previousNodeId, nextNode.Id, out var edge))
        {
            ClearTraversalExecution(memory);
            return new NavigationDecision((nextNode.X, nextNode.Y), HasRoute: true, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: GetRouteLabel(memory, BotNavigationTraversalKind.Walk));
        }

        if (!TryGetValidatedEdgeForExecution(world, player, classId, navigationGraph, memory, edge, out var executionEdge))
        {
            BlockRouteEdge(memory, previousNodeId, nextNode.Id);
            memory.NavigationGraphKey = navigationGraph.CacheKey;
            memory.RouteGoalNodeId = exactGoalNodeId;
            memory.RouteGoalX = destination.X;
            memory.RouteGoalY = destination.Y;
            memory.RouteRefreshTicks = Math.Max(1, timing.RouteRefreshTicks / 2);
            return new NavigationDecision(
                destination,
                HasRoute: false,
                ForcedHorizontalDirection: 0,
                ForceJump: false,
                LocksMovement: false,
                Label: "route-replan");
        }

        memory.CurrentPointId = previousNodeId;
        memory.NextPointId = nextNode.Id;
        memory.NextPoint2Id = memory.RouteNodeIds is not null && memory.RouteIndex + 1 < memory.RouteNodeIds.Length
            ? memory.RouteNodeIds[memory.RouteIndex + 1]
            : -1;
        memory.NextPoint3Id = memory.RouteNodeIds is not null && memory.RouteIndex + 2 < memory.RouteNodeIds.Length
            ? memory.RouteNodeIds[memory.RouteIndex + 2]
            : -1;
        SetModernClosestPointTarget(memory, nextNode.X, nextNode.Y);
        UpdateModernTargetProgress(player, memory, (nextNode.X, nextNode.Y));
        if (ShouldBlockCurrentRouteEdge(player, navigationGraph, memory, previousNodeId, nextNode, executionEdge, destination))
        {
            var blockedPreferredScoreRouteKey = memory.PreferredScoreRouteKey;
            ClearNavigationRoute(memory);
            var blockedByPreferredRoute = !string.IsNullOrWhiteSpace(blockedPreferredScoreRouteKey);
            if (!string.IsNullOrWhiteSpace(blockedPreferredScoreRouteKey))
            {
                BlockPreferredScoreRoute(memory, blockedPreferredScoreRouteKey);
            }
            if (!blockedByPreferredRoute)
            {
                BlockRouteEdge(memory, previousNodeId, nextNode.Id);
            }
            memory.NavigationGraphKey = navigationGraph.CacheKey;
            memory.RouteGoalNodeId = exactGoalNodeId;
            memory.RouteGoalX = destination.X;
            memory.RouteGoalY = destination.Y;
            memory.RouteRefreshTicks = Math.Max(1, timing.RouteRefreshTicks / 2);
            return new NavigationDecision(
                destination,
                HasRoute: false,
                ForcedHorizontalDirection: 0,
                ForceJump: false,
                LocksMovement: false,
                Label: "route-replan");
        }

        if (TryResolveTraversalDecision(
                world,
                player,
                destination,
                navigationGraph,
                memory,
                previousNodeId,
                nextNode,
                executionEdge,
                timing.FixedDeltaSeconds,
                out var traversalDecision))
        {
            return traversalDecision;
        }

        var forceWalkRecoveryJump = ShouldForceWalkRouteRecoveryJump(player, navigationGraph, previousNodeId, nextNode, executionEdge);
        return new NavigationDecision((nextNode.X, nextNode.Y), HasRoute: true, ForcedHorizontalDirection: 0, ForceJump: forceWalkRecoveryJump, LocksMovement: false, Label: GetRouteLabel(memory, executionEdge.Kind), TraversalKind: executionEdge.Kind);
    }

    private bool TryApplyPreferredScoreRoute(
        SimulationWorld world,
        PlayerEntity player,
        PlayerClass classId,
        BotNavigationRuntimeGraph navigationGraph,
        ModernPathSelection objectiveSelection,
        BotMemory memory,
        out bool preferredScoreRouteEligible)
    {
        preferredScoreRouteEligible = false;
        if (memory.PreferredScoreRouteMissCooldownTicks > 0)
        {
            ClearActivePreferredScoreRoute(memory);
            return false;
        }

        if (memory.PreferredScoreRouteBlockedTicks > 0)
        {
            ClearActivePreferredScoreRoute(memory);
            return false;
        }

        if (!TryResolvePreferredScoreRoutePhase(world, player, objectiveSelection, out var phase))
        {
            ClearActivePreferredScoreRoute(memory);
            return false;
        }

        var scoreRouteAsset = GetScoreRouteAsset(world.Level);
        if (scoreRouteAsset is null || scoreRouteAsset.Routes.Count == 0)
        {
            ClearActivePreferredScoreRoute(memory);
            return false;
        }

        preferredScoreRouteEligible = true;
        var profile = BotNavigationProfiles.GetProfileForClass(classId);
        if (memory.ActivePreferredScoreRoute is { } activeRoute
            && activeRoute.Team == player.Team
            && activeRoute.Profile == profile
            && activeRoute.Phase == phase
            && !IsPreferredScoreRouteBlocked(memory, activeRoute.Key)
            && ShouldHoldActivePreferredScoreRoute(player, navigationGraph, memory, activeRoute))
        {
            memory.PreferredScoreRouteKey = activeRoute.Key;
            memory.PreferredScoreRouteLabel = string.IsNullOrWhiteSpace(activeRoute.Label)
                ? activeRoute.Key
                : activeRoute.Label;
            memory.ActivePreferredScoreRoute = activeRoute;
            ResetPreferredScoreRouteMissTracking(memory);
            return true;
        }

        if (!TryFindModernRouteStartNode(
                world,
                player,
                navigationGraph,
                objectiveSelection.Destination,
                out var preferredRouteNearestNode))
        {
            ClearActivePreferredScoreRoute(memory);
            return false;
        }

        BotNavigationScoreRouteEntry? bestRoute = null;
        var bestRouteStartIndex = -1;
        var bestDistanceSquared = float.PositiveInfinity;
        for (var routeIndex = 0; routeIndex < scoreRouteAsset.Routes.Count; routeIndex += 1)
        {
            var candidate = scoreRouteAsset.Routes[routeIndex];
            if (candidate.Team != player.Team
                || candidate.Profile != profile
                || candidate.Phase != phase
                || candidate.RouteNodeIds.Count <= 1
                || IsPreferredScoreRouteBlocked(memory, candidate.Key))
            {
                continue;
            }

            if (!TryFindPreferredScoreRouteStartIndex(
                    player,
                    navigationGraph,
                    candidate.RouteNodeIds,
                    preferredRouteNearestNode.Id,
                    out var startIndex,
                    out var distanceSquared))
            {
                continue;
            }

            if (distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestRoute = candidate;
            bestRouteStartIndex = startIndex;
            bestDistanceSquared = distanceSquared;
        }

        if (bestRoute is null || bestRouteStartIndex < 0)
        {
            ClearActivePreferredScoreRoute(memory);
            return false;
        }

        if (ShouldHoldActivePreferredScoreRoute(player, navigationGraph, memory, bestRoute))
        {
            memory.ActivePreferredScoreRoute ??= bestRoute;
            ResetPreferredScoreRouteMissTracking(memory);
            return true;
        }

        memory.RouteNodeIds = bestRoute.RouteNodeIds.ToArray();
        memory.RouteIndex = Math.Clamp(bestRouteStartIndex + 1, 1, memory.RouteNodeIds.Length - 1);
        memory.RouteIsPartial = false;
        memory.RouteGoalNodeId = bestRoute.GoalNodeId;
        memory.RouteGoalX = bestRoute.GoalX;
        memory.RouteGoalY = bestRoute.GoalY;
        memory.RouteRefreshTicks = int.MaxValue / 4;
        memory.NavigationGraphKey = navigationGraph.CacheKey;
        memory.PreferredScoreRouteKey = bestRoute.Key;
        memory.PreferredScoreRouteLabel = string.IsNullOrWhiteSpace(bestRoute.Label)
            ? bestRoute.Key
            : bestRoute.Label;
        memory.ActivePreferredScoreRoute = bestRoute;
        ResetPreferredScoreRouteMissTracking(memory);
        ResetRouteProgress(memory);
        ClearTraversalExecution(memory);
        return true;
    }

    private static bool TryResolvePreferredScoreRoutePhase(
        SimulationWorld world,
        PlayerEntity player,
        ModernPathSelection objectiveSelection,
        out BotNavigationScoreRoutePhase phase)
    {
        phase = BotNavigationScoreRoutePhase.None;
        if (objectiveSelection.AllowDirectPath)
        {
            return false;
        }

        if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag)
        {
            phase = player.IsCarryingIntel
                ? BotNavigationScoreRoutePhase.ReturnIntel
                : BotNavigationScoreRoutePhase.AttackIntel;
            return true;
        }

        if (objectiveSelection.IsCaptureObjective)
        {
            phase = BotNavigationScoreRoutePhase.CaptureObjective;
            return true;
        }

        return false;
    }

    private static bool TryFindPreferredScoreRouteStartIndex(
        PlayerEntity player,
        BotNavigationRuntimeGraph navigationGraph,
        IReadOnlyList<int> routeNodeIds,
        int nearestRouteNodeId,
        out int startIndex,
        out float bestDistanceSquared)
    {
        startIndex = -1;
        bestDistanceSquared = PreferredScoreRouteAcquireDistance * PreferredScoreRouteAcquireDistance;
        for (var index = 0; index < routeNodeIds.Count - 1; index += 1)
        {
            if (routeNodeIds[index] != nearestRouteNodeId)
            {
                continue;
            }

            if (!navigationGraph.TryGetNode(routeNodeIds[index], out var node))
            {
                continue;
            }

            if (navigationGraph.TryGetEdge(routeNodeIds[index], routeNodeIds[index + 1], out var edge)
                && edge.Kind == BotNavigationTraversalKind.Jump
                && edge.InputTape.Count == 0)
            {
                continue;
            }

            var distanceSquared = DistanceSquared(player.X, GetModernPlayerFeetY(player), node.X, node.Y);
            if (distanceSquared > bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            startIndex = index;
        }

        return startIndex >= 0;
    }

    private static bool ShouldHoldActivePreferredScoreRoute(
        PlayerEntity player,
        BotNavigationRuntimeGraph navigationGraph,
        BotMemory memory,
        BotNavigationScoreRouteEntry bestRoute)
    {
        if (!string.Equals(memory.PreferredScoreRouteKey, bestRoute.Key, StringComparison.Ordinal)
            || memory.RouteNodeIds is null
            || memory.RouteNodeIds.Length <= 1
            || !string.Equals(memory.NavigationGraphKey, navigationGraph.CacheKey, StringComparison.Ordinal))
        {
            return false;
        }

        if (memory.ActiveTraversalTape is not null || !player.IsGrounded)
        {
            return true;
        }

        var playerFeet = GetModernPlayerFeetY(player);
        if (TryGetCurrentRouteNode(navigationGraph, memory, out var currentRouteNode)
            && DistanceSquared(player.X, playerFeet, currentRouteNode.X, currentRouteNode.Y)
                <= RouteRepathDistance * RouteRepathDistance)
        {
            return true;
        }

        if (memory.RouteIndex <= 0
            || memory.RouteNodeIds is null
            || memory.RouteIndex >= memory.RouteNodeIds.Length)
        {
            return false;
        }

        var previousNodeId = memory.RouteNodeIds[memory.RouteIndex - 1];
        var currentNodeId = memory.RouteNodeIds[memory.RouteIndex];
        if (!navigationGraph.TryGetNode(previousNodeId, out var sourceNode)
            || !TryGetPreferredScoreRouteSegment(memory, previousNodeId, currentNodeId, out var preferredSegment)
            || preferredSegment.InputTape.Count == 0)
        {
            return false;
        }

        var startToleranceX = MathF.Max(TraversalStartDistance, preferredSegment.StartToleranceX);
        var startToleranceY = MathF.Max(TraversalStartDistance, preferredSegment.StartToleranceY);
        return HasReachedTraversalWindow(
            player.X,
            player.Bottom,
            player.IsGrounded,
            sourceNode.X,
            sourceNode.Y,
            startToleranceX,
            startToleranceY,
            requireGroundedArrival: true);
    }

    private static void BlockPreferredScoreRoute(BotMemory memory, string routeKey)
    {
        memory.PreferredScoreRouteBlockedKey = routeKey;
        memory.PreferredScoreRouteBlockedTicks = RouteBlockedEdgeTicksDefault;
    }

    private static void RegisterPreferredScoreRouteMiss(BotMemory memory)
    {
        memory.PreferredScoreRouteRouteMissStreak += 1;
        if (memory.PreferredScoreRouteRouteMissStreak < PreferredScoreRouteMissBudget)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(memory.PreferredScoreRouteKey))
        {
            BlockPreferredScoreRoute(memory, memory.PreferredScoreRouteKey);
        }

        memory.PreferredScoreRouteMissCooldownTicks = PreferredScoreRouteMissCooldownTicksDefault;
        memory.PreferredScoreRouteRouteMissStreak = 0;
        memory.NavigationIssueLabel = "preferred_route_miss_cooldown";
        ClearActivePreferredScoreRoute(memory);
    }

    private static void ResetPreferredScoreRouteMissTracking(BotMemory memory)
    {
        memory.PreferredScoreRouteRouteMissStreak = 0;
    }

    private static bool IsPreferredScoreRouteBlocked(BotMemory memory, string routeKey)
    {
        return memory.PreferredScoreRouteBlockedTicks > 0
            && string.Equals(memory.PreferredScoreRouteBlockedKey, routeKey, StringComparison.Ordinal);
    }

    private static void ClearActivePreferredScoreRoute(BotMemory memory)
    {
        memory.PreferredScoreRouteKey = string.Empty;
        memory.PreferredScoreRouteLabel = string.Empty;
        memory.ActivePreferredScoreRoute = null;
    }

    private static NavigationDecision ResolveModernNavigationDecision(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination,
        ClientBotNavPoints navPoints,
        BotMemory memory,
        BotTimingProfile timing,
        bool allowModernDirectPath,
        ModernPathSelection objectiveSelection)
    {
        var captureHoldState = ResolveModernCaptureHoldState(world, player, objectiveSelection, memory, timing.FixedDeltaSeconds);
        if (captureHoldState.SuppressNavigation)
        {
            memory.NextPointId = -1;
            memory.NextPoint2Id = -1;
            memory.NextPoint3Id = -1;
            memory.NoNextPointTicks = 0;
            memory.StickyNextTicksRemaining = 0;
            SetModernClosestPointTarget(memory, captureHoldState.TargetX, captureHoldState.TargetY);
            return new NavigationDecision(
                (captureHoldState.TargetX, captureHoldState.TargetY),
                HasRoute: true,
                ForcedHorizontalDirection: 0,
                ForceJump: false,
                LocksMovement: false,
                Label: "capture_zone_hold",
                MovementTargetUsesFeetCoordinates: true,
                CaptureHoldActive: true);
        }

        _ = allowModernDirectPath;

        var goalMoved = destination.X != memory.RouteGoalX
            || destination.Y != memory.RouteGoalY;
        if (!string.Equals(memory.NavigationGraphKey, navPoints.CacheKey, StringComparison.Ordinal))
        {
            ResetModernNavigationState(memory);
        }

        BotNavigationNode goalNode;
        if (memory.RouteGoalNodeId >= 0
            && !goalMoved
            && string.Equals(memory.NavigationGraphKey, navPoints.CacheKey, StringComparison.Ordinal)
            && navPoints.TryGetPoint(memory.RouteGoalNodeId, out goalNode))
        {
        }
        else if (!navPoints.TryFindNearestPoint(destination.X, destination.Y, out goalNode))
        {
            ClearNavigationRoute(memory);
            return new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "mgoal-miss");
        }

        memory.NavigationGraphKey = navPoints.CacheKey;
        memory.RouteGoalNodeId = goalNode.Id;
        memory.RouteGoalX = destination.X;
        memory.RouteGoalY = destination.Y;

        var maximumGoalWeightDepth = ModernMaximumWeightDepth;
        var goalWeights = navPoints.GetGoalWeights(goalNode.Id, maximumGoalWeightDepth, player.ClassId);
        if (goalWeights is null)
        {
            ClearNavigationRoute(memory);
            return new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "mweight-miss");
        }

        for (var attempt = 0; attempt < 3; attempt += 1)
        {
            if (!TryGetCurrentModernNode(navPoints, memory, out var currentNode))
            {
                if (!TryAcquireModernCurrentNode(world, navPoints, player, memory, honorSecondAnchorBlock: true, out currentNode))
                {
                    ClearNavigationRoute(memory);
                    return new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "mstart-miss");
                }

                memory.CurrentPointId = currentNode.Id;
                memory.NextPointId = -1;
                memory.NextPoint2Id = -1;
                memory.NextPoint3Id = -1;
            }

            if (!TryGetCurrentModernNode(navPoints, memory, out currentNode))
            {
                ClearNavigationRoute(memory);
                return new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "mcurrent-miss");
            }

            var currentWeight = GetModernGoalWeight(goalWeights, currentNode.Id);
            if (currentWeight == 1)
            {
                var directTarget = objectiveSelection.CaptureObjective is ModernCaptureObjective captureObjective
                    ? captureObjective.TargetZone
                    : destination;
                SetModernClosestPointTarget(memory, directTarget.X, directTarget.Y);
                return new NavigationDecision(directTarget, HasRoute: true, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "m1:direct");
            }

            if (currentWeight <= 0)
            {
                memory.CurrentPointId = -1;
                memory.NextPointId = -1;
                memory.NextPoint2Id = -1;
                memory.NextPoint3Id = -1;
                memory.RouteGoalNodeId = -1;
                continue;
            }

            var hasQueuedPoints = TryPopulateModernQueuedPoints(
                    world,
                    player,
                    destination,
                    navPoints,
                    goalWeights,
                    memory,
                    out var selectionReason);

            var hasCurrentNode = TryGetCurrentModernNode(navPoints, memory, out currentNode);

            if (hasCurrentNode
                && TryReacquireBlockedModernCurrentPoint(world, player, navPoints, goalWeights, memory, destination, out selectionReason))
            {
                hasCurrentNode = TryGetCurrentModernNode(navPoints, memory, out currentNode);
            }

            if (!TryGetModernClosestPointTarget(memory, out var movementTarget))
            {
                movementTarget = destination;
            }

            UpdateModernTargetProgress(player, memory, movementTarget);

            if (hasCurrentNode
                && TryApplyModernReanchor(
                    world,
                    player,
                    destination,
                    navPoints,
                    goalWeights,
                    memory,
                    currentNode,
                    out selectionReason))
            {
                hasQueuedPoints = false;
            }

            if (TryPromoteModernQueuedPoints(player, navPoints, memory, out selectionReason))
            {
                if (!TryGetCurrentModernNode(navPoints, memory, out currentNode))
                {
                    continue;
                }
            }

            ApplyModernClosestPointAnticipation(world, player, navPoints, memory, ref selectionReason);

            if (!TryGetModernClosestPointTarget(memory, out movementTarget))
            {
                movementTarget = destination;

                if (!hasQueuedPoints)
                {
                    selectionReason = "missing_nextpoint";
                }
            }

            var useIntelReturnFinalApproach = ShouldUseModernIntelReturnFinalApproach(world, player, destination);
            if (useIntelReturnFinalApproach)
            {
                movementTarget = destination;
                selectionReason = string.IsNullOrWhiteSpace(selectionReason)
                    ? "intel_home_direct"
                    : $"{selectionReason}:intel_home_direct";
            }

            var labelCurrentWeight = 0;
            var nextWeight = 0;
            if (TryGetCurrentModernNode(navPoints, memory, out currentNode))
            {
                labelCurrentWeight = GetModernGoalWeight(goalWeights, currentNode.Id);
                if (TryGetModernNextNode(navPoints, memory, out var nextNode)
                    && navPoints.TryGetConnection(currentNode.Id, nextNode.Id, out var nextEdge))
                {
                    nextWeight = GetModernGoalWeight(goalWeights, nextNode.Id);
                }
            }

            SetModernClosestPointTarget(memory, movementTarget.X, movementTarget.Y);
            return new NavigationDecision(
                movementTarget,
                HasRoute: hasQueuedPoints || movementTarget == destination,
                ForcedHorizontalDirection: 0,
                ForceJump: false,
                LocksMovement: false,
                Label: $"m{labelCurrentWeight}->{nextWeight}:{selectionReason}:nav",
                TraversalKind: BotNavigationTraversalKind.Walk,
                MovementTargetUsesFeetCoordinates: !useIntelReturnFinalApproach);
        }

        ClearNavigationRoute(memory);
        return new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "mroute-miss");
    }

    private static bool TryGetCurrentModernNode(
        ClientBotNavPoints navPoints,
        BotMemory memory,
        out BotNavigationNode node)
    {
        node = default!;
        return memory.CurrentPointId >= 0
            && navPoints.TryGetPoint(memory.CurrentPointId, out node);
    }

    private static bool TryGetCurrentModernNode(
        BotNavigationRuntimeGraph navigationGraph,
        BotMemory memory,
        out BotNavigationNode node)
    {
        node = default!;
        return memory.CurrentPointId >= 0
            && navigationGraph.TryGetNode(memory.CurrentPointId, out node);
    }

    private static bool ShouldReacquireModernCurrentNode(
        SimulationWorld world,
        PlayerEntity player,
        ClientBotNavPoints navPoints,
        int[] goalWeights,
        BotNavigationNode currentNode,
        BotMemory memory)
    {
        _ = goalWeights;

        if (HasModernObstacleLineOfSight(
                world,
                player.X,
                player.Y,
                currentNode.X,
                GetModernNodeFeetY(navPoints, currentNode) - ModernPointSightYOffset))
        {
            return false;
        }

        var movementTarget = (X: memory.RouteGoalX, Y: memory.RouteGoalY);
        if (TryGetModernQueuedMovementTarget(world, player, navPoints, memory, out var queuedMovementTarget))
        {
            movementTarget = queuedMovementTarget;
        }

        return !HasModernObstacleLineOfSight(
            world,
            player.X,
            player.Y,
            movementTarget.X,
            movementTarget.Y - ModernPointSightYOffset);
    }

    private static bool TryGetModernSecondNextNode(
        ClientBotNavPoints navPoints,
        BotMemory memory,
        out BotNavigationNode nextNode)
    {
        nextNode = default!;
        return memory.NextPoint2Id >= 0
            && navPoints.TryGetPoint(memory.NextPoint2Id, out nextNode);
    }

    private static bool TryGetModernThirdNextNode(
        ClientBotNavPoints navPoints,
        BotMemory memory,
        out BotNavigationNode nextNode)
    {
        nextNode = default!;
        return memory.NextPoint3Id >= 0
            && navPoints.TryGetPoint(memory.NextPoint3Id, out nextNode);
    }

    private static bool TryPopulateModernQueuedPoints(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination,
        ClientBotNavPoints navPoints,
        int[] goalWeights,
        BotMemory memory,
        out string selectionReason)
    {
        selectionReason = "nav_step";
        if (!TryGetCurrentModernNode(navPoints, memory, out var currentNode))
        {
            memory.NextPointId = -1;
            memory.NextPoint2Id = -1;
            memory.NextPoint3Id = -1;
            SetModernClosestPointTarget(memory, destination.X, destination.Y);
            selectionReason = "missing_currentpoint";
            return false;
        }

        // GML clears nextPoint2/nextPoint3 at the top of every bot step before route selection.
        memory.NextPoint2Id = -1;
        memory.NextPoint3Id = -1;

        var selectedFreshNextPoint = TrySelectModernNextEdges(
            world,
            player,
            destination,
            navPoints,
            goalWeights,
            memory,
            out var nextNode,
            out var nextEdge,
            out var nextWeight,
            out var secondNextNode,
            out var secondNextEdge,
            out var secondNextWeight);
        if (!selectedFreshNextPoint)
        {
            if (memory.NextPointId >= 0 && memory.NextPointId != memory.CurrentPointId)
            {
                memory.NoNextPointTicks = Math.Max(0, memory.NoNextPointTicks - ModernNoNextTickDecay);
                return true;
            }

            if (memory.NextPointId == memory.CurrentPointId)
            {
                memory.NextPointId = -1;
                memory.NextPoint2Id = -1;
                memory.NextPoint3Id = -1;
            }

            if (memory.CurrentPointId >= 0)
            {
                memory.NoNextPointTicks += 1;
            }
            else
            {
                memory.NoNextPointTicks = Math.Max(0, memory.NoNextPointTicks - ModernNoNextTickDecay);
            }

            if (memory.NoNextPointTicks > ModernNoNextTicksBeforeForceNeighbor
                && TrySelectModernForcedNextEdge(
                    world,
                    player,
                    destination,
                    navPoints,
                    goalWeights,
                    memory,
                    out nextNode,
                    out nextEdge,
                    out nextWeight))
            {
                memory.NoNextPointTicks = Math.Max(0, memory.NoNextPointTicks - ModernNoNextTickRecovery);
                selectionReason = "force_neighbor_step";
            }
            else if (memory.NoNextPointTicks > ModernNoNextTicksBeforeReacquire)
            {
                if (TrySuppressModernNoNextBranch(world, player, navPoints, goalWeights, memory))
                {
                    selectionReason = "no_next_branch_block";
                    if (!TryGetCurrentModernNode(navPoints, memory, out currentNode))
                    {
                        return false;
                    }

                    selectedFreshNextPoint = TrySelectModernNextEdges(
                        world,
                        player,
                        destination,
                        navPoints,
                        goalWeights,
                        memory,
                        out nextNode,
                        out nextEdge,
                        out nextWeight,
                        out secondNextNode,
                        out secondNextEdge,
                        out secondNextWeight);
                    if (!selectedFreshNextPoint)
                    {
                        return false;
                    }
                }
                else
                {
                    memory.CurrentPointId = -1;
                    memory.RouteGoalNodeId = -1;
                    memory.NoNextPointTicks = 0;
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
        else
        {
            memory.NoNextPointTicks = Math.Max(0, memory.NoNextPointTicks - ModernNoNextTickDecay);
        }

        if (nextNode is null || nextEdge is null)
        {
            return false;
        }

        selectionReason = ApplyModernSelectionBiases(
            player,
            navPoints,
            memory,
            currentNode,
            nextNode,
            nextEdge,
            nextWeight,
            secondNextNode,
            secondNextEdge,
            secondNextWeight,
            ref nextNode,
            ref nextEdge,
            ref nextWeight,
            selectionReason);
        if (nextNode is null || nextEdge is null)
        {
            return false;
        }

        memory.NextPointId = nextNode.Id;
        SetModernClosestPointTarget(memory, nextNode.X, GetModernNodeFeetY(navPoints, nextNode));
        if (selectionReason == "force_neighbor_step")
        {
            memory.NextPoint2Id = -1;
            memory.NextPoint3Id = -1;
        }
        else
        {
            PopulateModernLookaheadPoints(world, player, navPoints, goalWeights, memory, currentNode, nextNode);
        }

        memory.StickyCurrentPointId = currentNode.Id;
        memory.StickyNextPointId = nextNode.Id;
        memory.StickyNextTicksRemaining = memory.ModernStuckTicks > ModernStickyDisableStuckTicks
            ? 0
            : ModernStickyTicks;
        return true;
    }

    private static bool TrySuppressModernNoNextBranch(
        SimulationWorld world,
        PlayerEntity player,
        ClientBotNavPoints navPoints,
        int[] goalWeights,
        BotMemory memory)
    {
        var failedPointId = memory.CurrentPointId;
        if (failedPointId < 0)
        {
            return false;
        }

        memory.SecondAnchorBlockPointId = failedPointId;
        memory.SecondAnchorBlockTicksRemaining = Math.Max(memory.SecondAnchorBlockTicksRemaining, 72);
        memory.SecondAnchorCooldownTicksRemaining = Math.Max(memory.SecondAnchorCooldownTicksRemaining, 72);
        memory.CurrentPointId = -1;
        memory.NextPointId = -1;
        memory.NextPoint2Id = -1;
        memory.NextPoint3Id = -1;
        memory.StickyNextTicksRemaining = 0;
        memory.NavChurnTicks = 0;
        memory.NavChurnSwitchTicks = 0;
        memory.NavChurnLockTicksRemaining = 0;
        memory.NavChurnLockPointId = -1;
        memory.NoNextPointTicks = 0;
        memory.ModernPreviousTargetDistance = float.PositiveInfinity;

        if (!TryAcquireModernWeightedCurrentNode(world, navPoints, player, goalWeights, memory, out var reacquiredNode)
            && !TryAcquireNearestModernNode(navPoints, player, memory, honorSecondAnchorBlock: true, out reacquiredNode))
        {
            return false;
        }

        memory.CurrentPointId = reacquiredNode.Id;
        return memory.CurrentPointId != failedPointId;
    }

    private static void PopulateModernLookaheadPoints(
        SimulationWorld world,
        PlayerEntity player,
        ClientBotNavPoints navPoints,
        int[] goalWeights,
        BotMemory memory,
        BotNavigationNode currentNode,
        BotNavigationNode nextNode)
    {
        memory.NextPoint2Id = TrySelectModernLookaheadPoint(world, player, navPoints, goalWeights, currentNode, nextNode, memory)
            ?.Id ?? -1;

        if (memory.NextPoint2Id >= 0
            && navPoints.TryGetPoint(memory.NextPoint2Id, out var secondNode))
        {
            memory.NextPoint3Id = TrySelectModernLookaheadPoint(world, player, navPoints, goalWeights, nextNode, secondNode, memory)
                ?.Id ?? -1;
        }
        else
        {
            memory.NextPoint3Id = -1;
        }
    }

    private static BotNavigationNode? TrySelectModernLookaheadPoint(
        SimulationWorld world,
        PlayerEntity player,
        ClientBotNavPoints navPoints,
        int[] goalWeights,
        BotNavigationNode currentNode,
        BotNavigationNode fromNode,
        BotMemory memory)
    {
        if (!navPoints.TryGetOutgoingConnections(fromNode.Id, out var outgoingEdges))
        {
            return null;
        }

        var currentWeight = GetModernGoalWeight(goalWeights, fromNode.Id);
        var bestWeight = currentWeight;
        BotNavigationNode? bestNode = null;
        for (var edgeIndex = 0; edgeIndex < outgoingEdges.Count; edgeIndex += 1)
        {
            var edge = outgoingEdges[edgeIndex];
            if (!navPoints.TryGetPoint(edge.ToNodeId, out var candidateNode))
            {
                continue;
            }

            if (candidateNode.Id == currentNode.Id)
            {
                continue;
            }

            var candidateWeight = GetModernGoalWeight(goalWeights, candidateNode.Id);
            if (candidateWeight <= 0)
            {
                continue;
            }

            if (candidateWeight >= bestWeight)
            {
                continue;
            }

            bestWeight = candidateWeight;
            bestNode = candidateNode;
        }

        return bestNode;
    }

    private static bool TryGetModernQueuedMovementTarget(
        SimulationWorld world,
        PlayerEntity player,
        ClientBotNavPoints navPoints,
        BotMemory memory,
        out (float X, float Y) movementTarget)
    {
        movementTarget = default;
        if (!TryGetModernNextNode(navPoints, memory, out var nextNode))
        {
            return false;
        }

        var nextFeetY = GetModernNodeFeetY(navPoints, nextNode);
        movementTarget = (nextNode.X, nextFeetY);

        if (TryGetModernSecondNextNode(navPoints, memory, out var secondNextNode)
            && DistanceSquared(player.X, player.Y, nextNode.X, nextFeetY) < ModernPointLookaheadDistance * ModernPointLookaheadDistance
            && HasModernGroundContact(world, player)
            && MathF.Abs(player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond) > ModernPointLookaheadMinimumSpeed)
        {
            var secondFeetY = GetModernNodeFeetY(navPoints, secondNextNode);
            movementTarget = (secondNextNode.X, secondFeetY);
            if (TryGetModernThirdNextNode(navPoints, memory, out var thirdNextNode)
                && DistanceSquared(player.X, player.Y, secondNextNode.X, secondFeetY) < 20f * 20f)
            {
                movementTarget = (thirdNextNode.X, GetModernNodeFeetY(navPoints, thirdNextNode));
            }
        }

        return true;
    }

    private static void SetModernClosestPointTarget(BotMemory memory, float x, float y)
    {
        memory.HasModernClosestPointTarget = true;
        memory.ModernClosestPointTargetX = x;
        memory.ModernClosestPointTargetY = y;
    }

    private static bool TryGetModernClosestPointTarget(BotMemory memory, out (float X, float Y) target)
    {
        if (memory.HasModernClosestPointTarget)
        {
            target = (memory.ModernClosestPointTargetX, memory.ModernClosestPointTargetY);
            return true;
        }

        target = default;
        return false;
    }

    private static bool TryReacquireBlockedModernCurrentPoint(
        SimulationWorld world,
        PlayerEntity player,
        ClientBotNavPoints navPoints,
        int[] goalWeights,
        BotMemory memory,
        (float X, float Y) fallbackMovementTarget,
        out string selectionReason)
    {
        selectionReason = string.Empty;
        if (!TryGetCurrentModernNode(navPoints, memory, out var currentNode))
        {
            return false;
        }

        var movementTarget = TryGetModernClosestPointTarget(memory, out var closestPointTarget)
            ? closestPointTarget
            : fallbackMovementTarget;

        if (HasModernObstacleLineOfSight(world, player.X, player.Y, currentNode.X, GetModernNodeFeetY(navPoints, currentNode) - ModernPointSightYOffset)
            || HasModernObstacleLineOfSight(world, player.X, player.Y, movementTarget.X, movementTarget.Y - ModernPointSightYOffset))
        {
            return false;
        }

        if (!TryAcquireNearestModernNode(navPoints, player, memory, honorSecondAnchorBlock: false, out var reacquiredNode))
        {
            return false;
        }

        memory.CurrentPointId = reacquiredNode.Id;
        selectionReason = "reacquire_collision";
        return true;
    }

    private static void UpdateModernTargetProgress(
        PlayerEntity player,
        BotMemory memory,
        (float X, float Y) movementTarget)
    {
        var currentTargetDistance = DistanceBetween(player.X, player.Y, movementTarget.X, movementTarget.Y);
        var movedSinceLastFrame = memory.HasObservedPosition
            ? DistanceBetween(player.X, player.Y, memory.PreviousObservedX, memory.PreviousObservedY)
            : 0f;

        if ((currentTargetDistance + 1f) < memory.ModernPreviousTargetDistance || movedSinceLastFrame > 1.25f)
        {
            memory.ModernStuckTicks = Math.Max(0, memory.ModernStuckTicks - ModernNoNextTickDecay);
        }
        else
        {
            memory.ModernStuckTicks += 1;
        }

        memory.ModernPreviousTargetDistance = currentTargetDistance;
    }

    private static bool TryApplyModernReanchor(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination,
        ClientBotNavPoints navPoints,
        int[] goalWeights,
        BotMemory memory,
        BotNavigationNode currentNode,
        out string selectionReason)
    {
        selectionReason = string.Empty;
        var horizontalSpeed = player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond;
        var hasGroundContact = HasModernGroundContact(world, player);
        var currentNodeFeetY = GetModernNodeFeetY(navPoints, currentNode);
        var verticalGapToPoint = player.Bottom - currentNodeFeetY;
        if (verticalGapToPoint > 36f)
        {
            memory.ModernDropGapTicks += 1;
        }
        else
        {
            memory.ModernDropGapTicks = Math.Max(0, memory.ModernDropGapTicks - ModernNoNextTickDecay);
        }

        var allowReanchor = memory.ReanchorTicksRemaining <= 0;
        var reanchorNeeded = false;
        if (allowReanchor
            && (verticalGapToPoint > 104f
                || (verticalGapToPoint > 52f
                    && hasGroundContact
                    && MathF.Abs(horizontalSpeed) < 0.35f
                    && memory.ModernDropGapTicks > 12)))
        {
            reanchorNeeded = true;
            selectionReason = "drop_reanchor";
        }

        if (!reanchorNeeded
            && allowReanchor
            && DistanceSquared(player.X, player.Y, currentNode.X, currentNodeFeetY) > ModernCurrentPointReanchorDistance * ModernCurrentPointReanchorDistance
            && memory.ModernStuckTicks > 8)
        {
            reanchorNeeded = true;
            selectionReason = "far_reanchor";
        }

        if (!reanchorNeeded
            && allowReanchor
            && memory.SecondAnchorCooldownTicksRemaining <= 0
            && memory.CurrentPointId == memory.NavChurnCurrentPointId
            && memory.NavChurnTicks > 12
            && DistanceSquared(player.X, player.Y, memory.NavChurnStartX, memory.NavChurnStartY) < 60f * 60f
            && memory.ModernStuckTicks > 10
            && TryResolveModernSecondAnchorPoint(world, player, navPoints, goalWeights, memory, currentNode, out var secondAnchorPointId))
        {
            var failedPointId = memory.CurrentPointId;
            memory.CurrentPointId = secondAnchorPointId;
            memory.NextPointId = -1;
            memory.NextPoint2Id = -1;
            memory.NextPoint3Id = -1;
            memory.ModernStuckTicks = 0;
            memory.NoNextPointTicks = 0;
            memory.LoopBacktrackTicks = 0;
            memory.StickyNextTicksRemaining = 0;
            memory.NavChurnTicks = 0;
            memory.NavChurnSwitchTicks = 0;
            memory.NavChurnLockTicksRemaining = 0;
            memory.NavChurnLockPointId = -1;
            memory.NavChurnCurrentPointId = memory.CurrentPointId;
            memory.NavChurnStartX = player.X;
            memory.NavChurnStartY = player.Y;
            memory.ModernPreviousTargetDistance = float.PositiveInfinity;
            memory.SecondAnchorCooldownTicksRemaining = 90;
            memory.SecondAnchorBlockPointId = failedPointId;
            memory.SecondAnchorBlockTicksRemaining = 55;
            memory.ReanchorTicksRemaining = Math.Max(memory.ReanchorTicksRemaining, 20);
            memory.RouteGoalNodeId = -1;
            selectionReason = "second_anchor_backtrack";
            return true;
        }

        if (!reanchorNeeded && allowReanchor && memory.ModernStuckTicks >= 28)
        {
            reanchorNeeded = true;
            selectionReason = "stuck_reanchor";
        }

        if (!reanchorNeeded)
        {
            return false;
        }

        memory.CurrentPointId = -1;
        memory.NextPointId = -1;
        memory.NextPoint2Id = -1;
        memory.NextPoint3Id = -1;
        memory.ModernStuckTicks = 0;
        memory.ModernDropGapTicks = 0;
        memory.NoNextPointTicks = 0;
        memory.LoopBacktrackTicks = 0;
        memory.StickyNextTicksRemaining = 0;
        memory.ReanchorTicksRemaining = 45;
        memory.ModernPreviousTargetDistance = float.PositiveInfinity;
        memory.RouteGoalNodeId = -1;
        if (TryAcquireNearestModernNode(navPoints, player, memory, honorSecondAnchorBlock: true, out var reacquiredNode))
        {
            memory.CurrentPointId = reacquiredNode.Id;
        }

        return true;
    }

    private static bool TryResolveModernSecondAnchorPoint(
        SimulationWorld world,
        PlayerEntity player,
        ClientBotNavPoints navPoints,
        int[] goalWeights,
        BotMemory memory,
        BotNavigationNode currentNode,
        out int secondAnchorPointId)
    {
        secondAnchorPointId = -1;
        if (memory.SecondLastCommittedPointId >= 0 && memory.SecondLastCommittedPointId != currentNode.Id)
        {
            secondAnchorPointId = memory.SecondLastCommittedPointId;
            return true;
        }

        if (memory.LastCommittedPointId >= 0 && memory.LastCommittedPointId != currentNode.Id)
        {
            secondAnchorPointId = memory.LastCommittedPointId;
            return true;
        }

        if (memory.NextPoint2Id >= 0 && memory.NextPoint2Id != currentNode.Id)
        {
            secondAnchorPointId = memory.NextPoint2Id;
            return true;
        }

        if (!navPoints.TryGetOutgoingConnections(currentNode.Id, out var outgoingEdges))
        {
            return false;
        }

        var bestScore = float.PositiveInfinity;
        for (var edgeIndex = 0; edgeIndex < outgoingEdges.Count; edgeIndex += 1)
        {
            var edge = outgoingEdges[edgeIndex];
            if (!navPoints.TryGetPoint(edge.ToNodeId, out var candidateNode)
                || candidateNode.Id == currentNode.Id
                || candidateNode.Id == memory.NextPointId)
            {
                continue;
            }

            if (!CanUseModernTraversal(world, player, navPoints, currentNode, candidateNode, edge))
            {
                continue;
            }

            var candidateScore = GetModernGoalWeight(goalWeights, candidateNode.Id) * 1000f
                + DistanceBetween(candidateNode.X, GetModernNodeFeetY(navPoints, candidateNode), memory.RouteGoalX, memory.RouteGoalY);
            if (candidateNode.Id == memory.PreviousCurrentPointId || candidateNode.Id == memory.PreviousNextPointId)
            {
                candidateScore += 200f;
            }

            if (candidateScore < bestScore)
            {
                bestScore = candidateScore;
                secondAnchorPointId = candidateNode.Id;
            }
        }

        return secondAnchorPointId >= 0;
    }

    private static bool TryPromoteModernQueuedPoints(
        PlayerEntity player,
        ClientBotNavPoints navPoints,
        BotMemory memory,
        out string selectionReason)
    {
        selectionReason = string.Empty;
        if (!TryGetModernNextNode(navPoints, memory, out var nextNode))
        {
            return false;
        }

        if (DistanceSquared(player.X, player.Y, nextNode.X, GetModernNodeFeetY(navPoints, nextNode)) >= ModernPointArrivalDistance * ModernPointArrivalDistance)
        {
            return false;
        }

        if (memory.SecondAnchorBlockTicksRemaining > 0
            && memory.NextPointId == memory.SecondAnchorBlockPointId
            && memory.CurrentPointId != memory.SecondAnchorBlockPointId)
        {
            memory.NextPointId = -1;
            memory.NextPoint2Id = -1;
            memory.NextPoint3Id = -1;
            selectionReason = "second_anchor_block_return";
            return true;
        }

        memory.SecondLastCommittedPointId = memory.LastCommittedPointId;
        memory.LastCommittedPointId = memory.CurrentPointId;
        memory.CurrentPointId = memory.NextPointId;
        if (memory.NextPoint2Id >= 0)
        {
            memory.NextPointId = memory.NextPoint2Id;
            memory.NextPoint2Id = memory.NextPoint3Id;
            memory.NextPoint3Id = -1;
            if (TryGetModernNextNode(navPoints, memory, out var promotedNextNode))
            {
                SetModernClosestPointTarget(memory, promotedNextNode.X, GetModernNodeFeetY(navPoints, promotedNextNode));
            }
            selectionReason = "promote_np2";
        }
        else
        {
            memory.NextPointId = -1;
            selectionReason = "promote_np";
        }

        return true;
    }

    private static void ApplyModernClosestPointAnticipation(
        SimulationWorld world,
        PlayerEntity player,
        ClientBotNavPoints navPoints,
        BotMemory memory,
        ref string selectionReason)
    {
        if (!TryGetModernNextNode(navPoints, memory, out var nextNode)
            || !TryGetModernSecondNextNode(navPoints, memory, out var secondNextNode))
        {
            return;
        }

        var nextFeetY = GetModernNodeFeetY(navPoints, nextNode);
        var groundedForAnticipate = HasModernGroundContact(world, player);
        var horizontalSpeed = player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond;
        if (DistanceSquared(player.X, player.Y, nextNode.X, nextFeetY) >= ModernPointLookaheadDistance * ModernPointLookaheadDistance
            || !groundedForAnticipate
            || MathF.Abs(horizontalSpeed) <= ModernPointLookaheadMinimumSpeed)
        {
            return;
        }

        var secondFeetY = GetModernNodeFeetY(navPoints, secondNextNode);
        SetModernClosestPointTarget(memory, secondNextNode.X, secondFeetY);
        selectionReason = "anticipate_np2";

        if (TryGetModernThirdNextNode(navPoints, memory, out var thirdNextNode)
            && DistanceSquared(player.X, player.Y, secondNextNode.X, secondFeetY) < 20f * 20f)
        {
            SetModernClosestPointTarget(memory, thirdNextNode.X, GetModernNodeFeetY(navPoints, thirdNextNode));
            selectionReason = "anticipate_np3";
        }
    }

    private static bool TryAcquireNearestModernNode(
        ClientBotNavPoints navPoints,
        PlayerEntity player,
        BotMemory memory,
        bool honorSecondAnchorBlock,
        out BotNavigationNode node)
    {
        node = default!;
        var bestDistance = float.PositiveInfinity;
        for (var index = 0; index < navPoints.Points.Count; index += 1)
        {
            var candidate = navPoints.Points[index];
            if (honorSecondAnchorBlock
                && memory.SecondAnchorBlockTicksRemaining > 0
                && candidate.Id == memory.SecondAnchorBlockPointId)
            {
                continue;
            }

            var distance = DistanceBetween(player.X, player.Y, candidate.X, GetModernNodeFeetY(navPoints, candidate));
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            node = candidate;
        }

        return bestDistance < float.PositiveInfinity;
    }

    private static bool TryAcquireModernCurrentNode(
        SimulationWorld world,
        BotNavigationRuntimeGraph navigationGraph,
        PlayerEntity player,
        BotMemory memory,
        bool honorSecondAnchorBlock,
        out BotNavigationNode node)
    {
        node = default!;
        var bestVisibleDistance = float.PositiveInfinity;
        for (var index = 0; index < navigationGraph.Nodes.Count; index += 1)
        {
            var candidate = navigationGraph.Nodes[index];
            if (honorSecondAnchorBlock
                && memory.SecondAnchorBlockTicksRemaining > 0
                && candidate.Id == memory.SecondAnchorBlockPointId)
            {
                continue;
            }

            var distance = DistanceBetween(player.X, player.Y, candidate.X, GetModernNodeFeetY(navigationGraph, candidate));
            if (distance >= bestVisibleDistance || !HasNodeLineOfSightToPlayer(world, player, navigationGraph, candidate))
            {
                continue;
            }

            bestVisibleDistance = distance;
            node = candidate;
        }

        if (bestVisibleDistance < float.PositiveInfinity)
        {
            return true;
        }

        var bestFallbackDistance = float.PositiveInfinity;
        for (var index = 0; index < navigationGraph.Nodes.Count; index += 1)
        {
            var candidate = navigationGraph.Nodes[index];
            if (honorSecondAnchorBlock
                && memory.SecondAnchorBlockTicksRemaining > 0
                && candidate.Id == memory.SecondAnchorBlockPointId)
            {
                continue;
            }

            var distance = DistanceBetween(player.X, player.Y, candidate.X, GetModernNodeFeetY(navigationGraph, candidate));
            if (distance >= bestFallbackDistance)
            {
                continue;
            }

            bestFallbackDistance = distance;
            node = candidate;
        }

        return bestFallbackDistance < float.PositiveInfinity;
    }

    private static bool TryAcquireModernCurrentNode(
        SimulationWorld world,
        ClientBotNavPoints navPoints,
        PlayerEntity player,
        BotMemory memory,
        bool honorSecondAnchorBlock,
        out BotNavigationNode node)
    {
        node = default!;
        var bestVisibleDistance = float.PositiveInfinity;
        for (var index = 0; index < navPoints.Points.Count; index += 1)
        {
            var candidate = navPoints.Points[index];
            if (honorSecondAnchorBlock
                && memory.SecondAnchorBlockTicksRemaining > 0
                && candidate.Id == memory.SecondAnchorBlockPointId)
            {
                continue;
            }

            var distance = DistanceBetween(player.X, player.Y, candidate.X, GetModernNodeFeetY(navPoints, candidate));
            if (distance >= bestVisibleDistance)
            {
                continue;
            }

            if (!HasNodeLineOfSightToPlayer(world, player, navPoints, candidate))
            {
                continue;
            }

            bestVisibleDistance = distance;
            node = candidate;
        }

        if (bestVisibleDistance < float.PositiveInfinity)
        {
            return true;
        }

        var bestFallbackDistance = float.PositiveInfinity;
        for (var index = 0; index < navPoints.Points.Count; index += 1)
        {
            var candidate = navPoints.Points[index];
            if (honorSecondAnchorBlock
                && memory.SecondAnchorBlockTicksRemaining > 0
                && candidate.Id == memory.SecondAnchorBlockPointId)
            {
                continue;
            }

            var distance = DistanceBetween(player.X, player.Y, candidate.X, GetModernNodeFeetY(navPoints, candidate));
            if (distance >= bestFallbackDistance)
            {
                continue;
            }

            bestFallbackDistance = distance;
            node = candidate;
        }

        return bestFallbackDistance < float.PositiveInfinity;
    }

    private static bool TryAcquireModernWeightedCurrentNode(
        SimulationWorld world,
        BotNavigationRuntimeGraph navigationGraph,
        PlayerEntity player,
        int[] goalWeights,
        BotMemory memory,
        out BotNavigationNode node)
    {
        node = default!;
        var bestVisibleDistance = float.PositiveInfinity;
        for (var index = 0; index < navigationGraph.Nodes.Count; index += 1)
        {
            var candidate = navigationGraph.Nodes[index];
            if (GetModernGoalWeight(goalWeights, candidate.Id) <= 0
                || memory.SecondAnchorBlockTicksRemaining > 0 && candidate.Id == memory.SecondAnchorBlockPointId)
            {
                continue;
            }

            var distance = DistanceBetween(player.X, player.Y, candidate.X, GetModernNodeFeetY(navigationGraph, candidate));
            if (distance >= bestVisibleDistance || distance > ModernWeightedCurrentNodeSearchDistance)
            {
                continue;
            }

            if (!HasNodeLineOfSightToPlayer(world, player, navigationGraph, candidate))
            {
                continue;
            }

            bestVisibleDistance = distance;
            node = candidate;
        }

        if (bestVisibleDistance < float.PositiveInfinity)
        {
            return true;
        }

        var bestFallbackDistance = float.PositiveInfinity;
        for (var index = 0; index < navigationGraph.Nodes.Count; index += 1)
        {
            var candidate = navigationGraph.Nodes[index];
            if (GetModernGoalWeight(goalWeights, candidate.Id) <= 0
                || memory.SecondAnchorBlockTicksRemaining > 0 && candidate.Id == memory.SecondAnchorBlockPointId)
            {
                continue;
            }

            var distance = DistanceBetween(player.X, player.Y, candidate.X, GetModernNodeFeetY(navigationGraph, candidate));
            if (distance >= bestFallbackDistance || distance > ModernWeightedCurrentNodeSearchDistance)
            {
                continue;
            }

            bestFallbackDistance = distance;
            node = candidate;
        }

        return bestFallbackDistance < float.PositiveInfinity;
    }

    private static bool TryAcquireModernWeightedCurrentNode(
        SimulationWorld world,
        ClientBotNavPoints navPoints,
        PlayerEntity player,
        int[] goalWeights,
        BotMemory memory,
        out BotNavigationNode node)
    {
        node = default!;
        var bestVisibleDistance = float.PositiveInfinity;
        for (var index = 0; index < navPoints.Points.Count; index += 1)
        {
            var candidate = navPoints.Points[index];
            if (GetModernGoalWeight(goalWeights, candidate.Id) <= 0
                || memory.SecondAnchorBlockTicksRemaining > 0 && candidate.Id == memory.SecondAnchorBlockPointId)
            {
                continue;
            }

            var distance = DistanceBetween(player.X, player.Y, candidate.X, GetModernNodeFeetY(navPoints, candidate));
            if (distance >= bestVisibleDistance || distance > ModernWeightedCurrentNodeSearchDistance)
            {
                continue;
            }

            if (!HasNodeLineOfSightToPlayer(world, player, navPoints, candidate))
            {
                continue;
            }

            bestVisibleDistance = distance;
            node = candidate;
        }

        if (bestVisibleDistance < float.PositiveInfinity)
        {
            return true;
        }

        var bestFallbackDistance = float.PositiveInfinity;
        for (var index = 0; index < navPoints.Points.Count; index += 1)
        {
            var candidate = navPoints.Points[index];
            if (GetModernGoalWeight(goalWeights, candidate.Id) <= 0
                || memory.SecondAnchorBlockTicksRemaining > 0 && candidate.Id == memory.SecondAnchorBlockPointId)
            {
                continue;
            }

            var distance = DistanceBetween(player.X, player.Y, candidate.X, GetModernNodeFeetY(navPoints, candidate));
            if (distance >= bestFallbackDistance || distance > ModernWeightedCurrentNodeSearchDistance)
            {
                continue;
            }

            bestFallbackDistance = distance;
            node = candidate;
        }

        return bestFallbackDistance < float.PositiveInfinity;
    }

    private static bool TrySelectModernNextEdges(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination,
        ClientBotNavPoints navPoints,
        int[] goalWeights,
        BotMemory memory,
        out BotNavigationNode? nextNode,
        out BotNavigationEdge? nextEdge,
        out int nextWeight,
        out BotNavigationNode? secondNextNode,
        out BotNavigationEdge? secondNextEdge,
        out int secondNextWeight)
    {
        nextNode = null;
        nextEdge = null;
        nextWeight = 0;
        secondNextNode = null;
        secondNextEdge = null;
        secondNextWeight = 0;

        if (!TryGetCurrentModernNode(navPoints, memory, out var currentNode)
            || !navPoints.TryGetOutgoingConnections(currentNode.Id, out var outgoingEdges))
        {
            return false;
        }

        var currentWeight = GetModernGoalWeight(goalWeights, currentNode.Id);
        if (currentWeight <= 0)
        {
            return false;
        }

        var currentFeetY = GetModernNodeFeetY(navPoints, currentNode);
        var feetY = player.Bottom;
        var horizontalSpeed = player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond;
        GetModernClassJumpProfile(player.ClassId, out _, out var jumpHeight, out var jumpHeightTotal);
        var bestScore = float.PositiveInfinity;
        var secondScore = float.PositiveInfinity;
        for (var edgeIndex = 0; edgeIndex < outgoingEdges.Count; edgeIndex += 1)
        {
            var edge = outgoingEdges[edgeIndex];
            if (!navPoints.TryGetPoint(edge.ToNodeId, out var candidateNode))
            {
                continue;
            }

            var candidateWeight = GetModernGoalWeight(goalWeights, candidateNode.Id);

            var candidateFeetY = GetModernNodeFeetY(navPoints, candidateNode);
            var candidateRunFromPlayer = MathF.Abs(candidateNode.X - player.X);
            var candidateRiseFromPlayer = feetY - candidateFeetY;
            if (candidateWeight <= 0)
            {
                continue;
            }

            if (candidateWeight >= currentWeight)
            {
                continue;
            }

            if (navPoints.IsReverseBlocked(candidateNode.Id, currentNode.Id))
            {
                continue;
            }

            if (!CanUseModernTraversal(world, player, navPoints, currentNode, candidateNode, edge))
            {
                continue;
            }

            var candidateScore = candidateWeight * 1000f
                + DistanceBetween(candidateNode.X, candidateFeetY, destination.X, destination.Y);

            if (candidateNode.Id == memory.PreviousCurrentPointId)
            {
                candidateScore += ModernCandidatePreviousPointPenalty;
            }

            if (candidateNode.Id == memory.PreviousNextPointId)
            {
                candidateScore -= ModernCandidatePreviousNextBonus;
            }

            if (candidateNode.Id == memory.LastCommittedPointId)
            {
                candidateScore += ModernCandidateCommittedPointPenalty;
            }

            if (memory.CurrentPointId == memory.PreviousCurrentPointId
                && candidateNode.Id != memory.PreviousNextPointId
                && MathF.Abs(player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond) < 1.2f)
            {
                candidateScore += ModernCandidatePreviousPointPenalty;
            }

            if (memory.SecondAnchorBlockTicksRemaining > 0
                && candidateNode.Id == memory.SecondAnchorBlockPointId
                && memory.CurrentPointId != memory.SecondAnchorBlockPointId)
            {
                candidateScore += 5000f;
            }

            if (memory.CurrentPointId == memory.PreviousCurrentPointId
                && MathF.Abs(horizontalSpeed) < 1.2f
                && candidateRunFromPlayer < 96f)
            {
                var lipProbeDirection = MathF.Sign(candidateNode.X - player.X);
                if (lipProbeDirection != 0f)
                {
                    var lipProbeX = player.X + (7f * lipProbeDirection);
                    var lipFrontFootBlocked = LineHitsSolid(world, player, lipProbeX, feetY - 1f, lipProbeX, feetY + 3f);
                    var lipHeadRoom = !LineHitsSolid(world, player, lipProbeX, feetY - 12f, lipProbeX, feetY - (jumpHeight + 2f));
                    if (lipFrontFootBlocked && lipHeadRoom)
                    {
                        if (candidateRiseFromPlayer >= 0f && candidateRiseFromPlayer <= 10f)
                        {
                            candidateScore += 320f;
                        }

                        if (candidateFeetY >= currentFeetY + 2f)
                        {
                            candidateScore -= 110f;
                        }
                    }
                }
            }

            if (candidateScore < bestScore)
            {
                secondScore = bestScore;
                secondNextNode = nextNode;
                secondNextEdge = nextEdge;
                secondNextWeight = nextWeight;

                bestScore = candidateScore;
                nextNode = candidateNode;
                nextEdge = edge;
                nextWeight = candidateWeight;
            }
            else if (candidateScore < secondScore)
            {
                secondScore = candidateScore;
                secondNextNode = candidateNode;
                secondNextEdge = edge;
                secondNextWeight = candidateWeight;
            }
        }

        return nextNode is not null && nextEdge is not null;
    }

    private static bool TrySelectModernForcedNextEdge(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination,
        ClientBotNavPoints navPoints,
        int[] goalWeights,
        BotMemory memory,
        out BotNavigationNode? nextNode,
        out BotNavigationEdge? nextEdge,
        out int nextWeight)
    {
        nextNode = null;
        nextEdge = null;
        nextWeight = 0;

        if (!TryGetCurrentModernNode(navPoints, memory, out var currentNode)
            || !navPoints.TryGetOutgoingConnections(currentNode.Id, out var outgoingEdges))
        {
            return false;
        }

        var currentWeight = GetModernGoalWeight(goalWeights, currentNode.Id);
        var bestScore = float.PositiveInfinity;
        for (var edgeIndex = 0; edgeIndex < outgoingEdges.Count; edgeIndex += 1)
        {
            var edge = outgoingEdges[edgeIndex];
            if (!navPoints.TryGetPoint(edge.ToNodeId, out var candidateNode))
            {
                continue;
            }

            if (navPoints.IsReverseBlocked(candidateNode.Id, currentNode.Id))
            {
                continue;
            }

            if (!CanUseModernTraversal(world, player, navPoints, currentNode, candidateNode, edge))
            {
                continue;
            }

            var candidateWeight = GetModernGoalWeight(goalWeights, candidateNode.Id);
            if (candidateWeight <= 0)
            {
                continue;
            }

            var candidateScore = DistanceBetween(candidateNode.X, GetModernNodeFeetY(navPoints, candidateNode), destination.X, destination.Y)
                + (Math.Max(0, candidateWeight - currentWeight) * ModernForcedWeightPenalty);
            if (candidateNode.Id == memory.PreviousCurrentPointId)
            {
                candidateScore += ModernForcedPreviousCurrentPenalty;
            }

            if (candidateNode.Id == memory.PreviousNextPointId)
            {
                candidateScore += ModernForcedPreviousNextPenalty;
            }

            if (candidateNode.Id == memory.LastCommittedPointId)
            {
                candidateScore += ModernForcedCommittedPenalty;
            }

            if (candidateScore >= bestScore)
            {
                continue;
            }

            bestScore = candidateScore;
            nextNode = candidateNode;
            nextEdge = edge;
            nextWeight = candidateWeight;
        }

        return nextNode is not null && nextEdge is not null;
    }

    private static string ApplyModernSelectionBiases(
        PlayerEntity player,
        ClientBotNavPoints navPoints,
        BotMemory memory,
        BotNavigationNode currentNode,
        BotNavigationNode selectedNextNode,
        BotNavigationEdge selectedNextEdge,
        int selectedNextWeight,
        BotNavigationNode? secondNextNode,
        BotNavigationEdge? secondNextEdge,
        int secondNextWeight,
        ref BotNavigationNode? nextNode,
        ref BotNavigationEdge? nextEdge,
        ref int nextWeight,
        string reason)
    {
        if (currentNode.Id != memory.NavChurnCurrentPointId)
        {
            memory.NavChurnCurrentPointId = currentNode.Id;
            memory.NavChurnTicks = 0;
            memory.NavChurnSwitchTicks = 0;
            memory.NavChurnLockPointId = -1;
            memory.NavChurnLockTicksRemaining = 0;
            memory.NavChurnStartX = player.X;
            memory.NavChurnStartY = player.Y;
        }
        else
        {
            memory.NavChurnTicks += 1;
            if (memory.PreviousNextPointId >= 0 && selectedNextNode.Id != memory.PreviousNextPointId)
            {
                memory.NavChurnSwitchTicks += 1;
            }
            else
            {
                memory.NavChurnSwitchTicks = Math.Max(0, memory.NavChurnSwitchTicks - 1);
            }

            if (memory.NavChurnLockTicksRemaining <= 0
                && memory.NavChurnTicks > ModernChurnObservationTicks
                && memory.NavChurnSwitchTicks > ModernChurnSwitchTicks
                && DistanceSquared(player.X, player.Y, memory.NavChurnStartX, memory.NavChurnStartY) < ModernChurnDistance * ModernChurnDistance
                && memory.PreviousNextPointId >= 0
                && TryGetModernEdge(currentNode.Id, memory.PreviousNextPointId, navPoints, out _))
            {
                memory.NavChurnLockPointId = memory.PreviousNextPointId;
                memory.NavChurnLockTicksRemaining = ModernChurnLockTicks;
            }
        }

        if (memory.NavChurnLockTicksRemaining > 0
            && memory.NavChurnLockPointId >= 0
            && memory.NavChurnCurrentPointId == currentNode.Id
            && selectedNextNode.Id != memory.NavChurnLockPointId
            && TryGetModernEdge(currentNode.Id, memory.NavChurnLockPointId, navPoints, out var churnLockEdge)
            && navPoints.TryGetPoint(memory.NavChurnLockPointId, out var churnLockNode))
        {
            nextNode = churnLockNode;
            nextEdge = churnLockEdge;
            nextWeight = selectedNextWeight;
            reason = "nav_step_churn_lock";
        }

        if (memory.StickyNextTicksRemaining > 0
            && memory.StickyCurrentPointId == currentNode.Id
            && memory.StickyNextPointId >= 0
            && (nextNode is null || nextNode.Id != memory.StickyNextPointId)
            && MathF.Abs(player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond) < 1.2f)
        {
            if (secondNextNode is not null && secondNextNode.Id == memory.StickyNextPointId && secondNextEdge is not null)
            {
                nextNode = secondNextNode;
                nextEdge = secondNextEdge;
                nextWeight = secondNextWeight;
                reason = "nav_step_sticky";
            }
            else if (TryGetModernEdge(currentNode.Id, memory.StickyNextPointId, navPoints, out var stickyEdge)
                     && navPoints.TryGetPoint(memory.StickyNextPointId, out var stickyNode))
            {
                nextNode = stickyNode;
                nextEdge = stickyEdge;
                nextWeight = selectedNextWeight;
                reason = "nav_step_sticky";
            }
        }

        if (memory.CurrentPointId == memory.PreviousNextPointId
            && nextNode is not null
            && nextNode.Id == memory.PreviousCurrentPointId)
        {
            memory.LoopBacktrackTicks += 1;
            if (memory.LoopBacktrackTicks > ModernLoopBacktrackTicks
                && secondNextNode is not null
                && secondNextEdge is not null)
            {
                nextNode = secondNextNode;
                nextEdge = secondNextEdge;
                nextWeight = secondNextWeight;
                reason = "nav_step_backtrack_avoid";
            }
        }
        else
        {
            memory.LoopBacktrackTicks = Math.Max(0, memory.LoopBacktrackTicks - ModernNoNextTickDecay);
        }

        return reason;
    }

    private static (float X, float Y) ResolveModernMovementTarget(
        PlayerEntity player,
        BotNavigationRuntimeGraph navigationGraph,
        BotNavigationNode nextNode,
        BotNavigationNode? secondNextNode)
    {
        if (secondNextNode is not null
            && DistanceSquared(player.X, player.Y, nextNode.X, GetModernNodeFeetY(navigationGraph, nextNode)) < ModernPointLookaheadDistance * ModernPointLookaheadDistance
            && player.IsGrounded
            && MathF.Abs(player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond) > ModernPointLookaheadMinimumSpeed)
        {
            return (secondNextNode.X, GetModernNodeFeetY(navigationGraph, secondNextNode));
        }

        return (nextNode.X, GetModernNodeFeetY(navigationGraph, nextNode));
    }

    private static void CommitModernProgress(BotMemory memory, int currentNodeId, int nextNodeId)
    {
        memory.PreviousCurrentPointId = currentNodeId;
        memory.PreviousNextPointId = nextNodeId;
        memory.LastCommittedPointId = currentNodeId;
        memory.CurrentPointId = nextNodeId;
    }

    private static void CommitModernFrameState(bool useModernClientBotPath, BotMemory memory)
    {
        if (!useModernClientBotPath)
        {
            return;
        }

        memory.PreviousCurrentPointId = memory.CurrentPointId;
        memory.PreviousNextPointId = memory.NextPointId;
    }

    private static bool TryGetModernEdge(
        int fromNodeId,
        int toNodeId,
        ClientBotNavPoints navPoints,
        out BotNavigationEdge edge)
    {
        return navPoints.TryGetConnection(fromNodeId, toNodeId, out edge);
    }

    private static bool TryGetModernEdge(
        int fromNodeId,
        int toNodeId,
        BotNavigationRuntimeGraph navigationGraph,
        out BotNavigationEdge edge)
    {
        return navigationGraph.TryGetEdge(fromNodeId, toNodeId, out edge);
    }

    private static int GetModernGoalWeight(int[] goalWeights, int nodeId)
    {
        return nodeId >= 0 && nodeId < goalWeights.Length
            ? goalWeights[nodeId]
            : 0;
    }

    private static bool TryResolveReachableModernGoalNode(
        BotNavigationRuntimeGraph navigationGraph,
        int currentNodeId,
        (float X, float Y) destination,
        out BotNavigationNode goalNode,
        out int[] goalWeights,
        out int currentWeight)
    {
        goalNode = default!;
        goalWeights = Array.Empty<int>();
        currentWeight = 0;
        if (!navigationGraph.TryFindRouteToGoalRadius(
                currentNodeId,
                destination.X,
                destination.Y,
                RouteGoalNodeSearchDistance,
                out _,
                out var goalNodeId)
            || !navigationGraph.TryGetNode(goalNodeId, out goalNode))
        {
            return false;
        }

        var expandedGoalWeights = navigationGraph.GetGoalWeights(
            goalNode.Id,
            maximumDepth: Math.Max(ModernMaximumWeightDepth, navigationGraph.Nodes.Count));
        currentWeight = expandedGoalWeights is null
            ? 0
            : GetModernGoalWeight(expandedGoalWeights, currentNodeId);
        if (expandedGoalWeights is null || currentWeight <= 0)
        {
            return false;
        }

        goalWeights = expandedGoalWeights;
        return true;
    }

    private static bool HasModernDirectPath(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination)
    {
        return MathF.Abs(player.X - destination.X) <= ModernPointDirectDistance
            && MathF.Abs(player.Y - destination.Y) <= ModernPointVerticalDistance
            && HasModernObstacleLineOfSight(world, player.X, player.Y, destination.X, destination.Y);
    }

    private static bool TryFindModernRouteGoalNode(
        SimulationWorld world,
        PlayerEntity player,
        BotNavigationRuntimeGraph navigationGraph,
        (float X, float Y) destination,
        out BotNavigationNode goalNode)
    {
        var hasDefaultGoalNode = navigationGraph.TryFindNearestNode(
            destination.X,
            destination.Y,
            RouteGoalNodeSearchDistance,
            requireGroundSupport: true,
            out var defaultGoalNode);

        if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag
            && player.IsCarryingIntel
            && hasDefaultGoalNode
            && IsModernIntelReturnScoreApproachNode(player, defaultGoalNode, destination))
        {
            goalNode = defaultGoalNode;
            return true;
        }

        if (TryFindModernIntelReturnGoalNode(world, player, navigationGraph, destination, out goalNode))
        {
            return true;
        }

        goalNode = defaultGoalNode;
        return hasDefaultGoalNode;
    }

    private static bool TryFindModernRouteStartNode(
        SimulationWorld world,
        PlayerEntity player,
        BotNavigationRuntimeGraph navigationGraph,
        (float X, float Y) destination,
        out BotNavigationNode startNode)
    {
        var hasDefaultStartNode = navigationGraph.TryFindNearestNode(
            player.X,
            GetModernPlayerFeetY(player),
            RouteStartNodeSearchDistance,
            requireGroundSupport: true,
            out var defaultStartNode);

        if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag
            && player.IsCarryingIntel
            && PlayerOverlapsIntelMarkerY(player, destination.Y)
            && hasDefaultStartNode
            && PlayerFeetOverlapIntelMarkerY(player, defaultStartNode.Y, destination.Y))
        {
            startNode = defaultStartNode;
            return true;
        }

        if (TryFindModernIntelReturnStartNode(world, player, navigationGraph, destination, out startNode))
        {
            return true;
        }

        startNode = defaultStartNode;
        return hasDefaultStartNode;
    }

    private static bool TryFindModernIntelReturnStartNode(
        SimulationWorld world,
        PlayerEntity player,
        BotNavigationRuntimeGraph navigationGraph,
        (float X, float Y) destination,
        out BotNavigationNode startNode)
    {
        startNode = default!;
        if (world.MatchRules.Mode != GameModeKind.CaptureTheFlag
            || !player.IsCarryingIntel
            || MathF.Abs(player.X - destination.X) > ModernIntelReturnFinalApproachDistanceX
            || !PlayerOverlapsIntelMarkerY(player, destination.Y))
        {
            return false;
        }

        var playerFeet = GetModernPlayerFeetY(player);
        var bestScore = float.PositiveInfinity;
        for (var index = 0; index < navigationGraph.Nodes.Count; index += 1)
        {
            var candidate = navigationGraph.Nodes[index];
            if (!candidate.RequiresGroundSupport
                || !PlayerFeetOverlapIntelMarkerY(player, candidate.Y, destination.Y))
            {
                continue;
            }

            var distanceToPlayer = DistanceBetween(candidate.X, candidate.Y, player.X, playerFeet);
            if (distanceToPlayer > RouteGoalNodeSearchDistance)
            {
                continue;
            }

            var horizontalScore = MathF.Abs(candidate.X - player.X);
            var verticalScore = MathF.Abs(candidate.Y - playerFeet) * 4f;
            var destinationScore = DistanceBetween(candidate.X, candidate.Y, destination.X, destination.Y) * 0.05f;
            var score = horizontalScore + verticalScore + destinationScore;
            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            startNode = candidate;
        }

        return float.IsFinite(bestScore);
    }

    private static bool TryFindModernIntelReturnGoalNode(
        SimulationWorld world,
        PlayerEntity player,
        BotNavigationRuntimeGraph navigationGraph,
        (float X, float Y) destination,
        out BotNavigationNode goalNode)
    {
        goalNode = default!;
        if (world.MatchRules.Mode != GameModeKind.CaptureTheFlag || !player.IsCarryingIntel)
        {
            return false;
        }

        var bestScore = float.PositiveInfinity;
        var desiredFeetY = destination.Y + player.CollisionBottomOffset - player.CollisionTopOffset;
        for (var index = 0; index < navigationGraph.Nodes.Count; index += 1)
        {
            var candidate = navigationGraph.Nodes[index];
            if (!candidate.RequiresGroundSupport)
            {
                continue;
            }

            var distanceToHome = DistanceBetween(candidate.X, candidate.Y, destination.X, destination.Y);
            if (distanceToHome > RouteGoalNodeSearchDistance
                || !PlayerFeetOverlapIntelMarkerY(player, candidate.Y, destination.Y))
            {
                continue;
            }

            var horizontalDistance = MathF.Abs(candidate.X - destination.X);
            if (horizontalDistance > ModernIntelReturnFinalApproachDistanceX)
            {
                continue;
            }

            var verticalScore = MathF.Abs(candidate.Y - desiredFeetY);
            var score = (verticalScore * 4f) + horizontalDistance + (distanceToHome * 0.05f);
            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            goalNode = candidate;
        }

        return float.IsFinite(bestScore);
    }

    private static bool IsModernIntelReturnScoreApproachNode(
        PlayerEntity player,
        BotNavigationNode node,
        (float X, float Y) destination)
    {
        return node.RequiresGroundSupport
            && MathF.Abs(node.X - destination.X) <= ModernIntelReturnFinalApproachDistanceX
            && DistanceBetween(node.X, node.Y, destination.X, destination.Y) <= RouteGoalNodeSearchDistance
            && PlayerFeetOverlapIntelMarkerY(player, node.Y, destination.Y);
    }

    private static bool PlayerOverlapsIntelMarkerY(PlayerEntity player, float markerY)
    {
        return PlayerFeetOverlapIntelMarkerY(player, player.Bottom, markerY);
    }

    private static bool PlayerFeetOverlapIntelMarkerY(PlayerEntity player, float feetY, float markerY)
    {
        var playerTop = feetY - player.CollisionBottomOffset + player.CollisionTopOffset;
        var markerTop = markerY - (ModernIntelMarkerSize / 2f);
        var markerBottom = markerY + (ModernIntelMarkerSize / 2f);
        return playerTop < markerBottom && feetY > markerTop;
    }

    private static bool ShouldUseModernIntelReturnFinalApproach(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination)
    {
        return player.IsCarryingIntel
            && MathF.Abs(player.X - destination.X) <= ModernIntelReturnFinalApproachDistanceX
            && MathF.Abs(player.Bottom - destination.Y) <= ModernIntelReturnFinalApproachDistanceY
            && DistanceBetween(player.X, player.Bottom, destination.X, destination.Y) <= ModernIntelReturnDirectArrivalDistance
            && HasModernObstacleLineOfSight(world, player.X, player.Y, destination.X, destination.Y);
    }

    private static bool ShouldUseModernGraphIntelReturnFinishOverride(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination,
        BotMemory memory)
    {
        if (!player.IsCarryingIntel)
        {
            memory.IntelReturnDirectTicksRemaining = 0;
            return false;
        }

        var deltaX = MathF.Abs(player.X - destination.X);
        var deltaY = MathF.Abs(player.Bottom - destination.Y);
        var feetDistanceToHome = DistanceBetween(player.X, player.Bottom, destination.X, destination.Y);
        if (memory.IntelReturnDirectTicksRemaining > 0)
        {
            if (deltaX <= ModernIntelReturnFinalApproachDistanceX
                && deltaY <= ModernIntelReturnFinalApproachDistanceY
                && feetDistanceToHome <= ModernIntelReturnDirectArrivalDistance
                && HasModernObstacleLineOfSight(world, player.X, player.Y, destination.X, destination.Y))
            {
                return true;
            }

            memory.IntelReturnDirectTicksRemaining = 0;
        }

        if (ShouldUseModernIntelReturnFinalApproach(world, player, destination))
        {
            memory.IntelReturnDirectTicksRemaining = ModernIntelReturnDirectLatchTicks;
            return true;
        }

        if (deltaX > ModernIntelReturnGraphFinishDistanceX
            || deltaY > ModernIntelReturnGraphFinishDistanceY)
        {
            return false;
        }

        if (deltaX <= ModernIntelReturnFinalApproachDistanceX
            && deltaY <= ModernIntelReturnFinalApproachDistanceY)
        {
            if (feetDistanceToHome <= ModernIntelReturnDirectArrivalDistance)
            {
                memory.IntelReturnDirectTicksRemaining = ModernIntelReturnDirectLatchTicks;
                return true;
            }

            return false;
        }

        var returnRouteStalled = memory.RouteNoProgressTicks >= RouteNoProgressTicksBeforeReplan
            || memory.RouteObjectiveNoProgressTicks >= RouteObjectiveNoProgressTicksBeforeReplan;
        if (!returnRouteStalled
            || deltaX > ModernIntelReturnFinalApproachDistanceX
            || deltaY > ModernIntelReturnFinalApproachDistanceY
            || feetDistanceToHome > ModernIntelReturnDirectArrivalDistance
            || !HasModernObstacleLineOfSight(world, player.X, player.Y, destination.X, destination.Y))
        {
            return false;
        }

        memory.IntelReturnDirectTicksRemaining = ModernIntelReturnDirectLatchTicks;
        return true;
    }

    private static bool ShouldBypassIntelReturnRouteEdgeBlock(PlayerEntity player, BotMemory memory)
    {
        if (!player.IsCarryingIntel
            || (memory.RouteGoalX == 0f && memory.RouteGoalY == 0f))
        {
            return false;
        }

        return MathF.Abs(player.X - memory.RouteGoalX) <= ModernIntelReturnBlockedEdgeBypassDistanceX
            && MathF.Abs(player.Bottom - memory.RouteGoalY) <= ModernIntelReturnBlockedEdgeBypassDistanceY;
    }

    private static bool HasNodeLineOfSightToPlayer(
        SimulationWorld world,
        PlayerEntity player,
        ClientBotNavPoints navPoints,
        BotNavigationNode node)
    {
        return HasModernObstacleLineOfSight(world, player.X, player.Y, node.X, GetModernNodeFeetY(navPoints, node) - ModernPointSightYOffset);
    }

    private static bool HasNodeLineOfSightToPlayer(
        SimulationWorld world,
        PlayerEntity player,
        BotNavigationRuntimeGraph navigationGraph,
        BotNavigationNode node)
    {
        return HasModernObstacleLineOfSight(world, player.X, player.Y, node.X, GetModernNodeFeetY(navigationGraph, node) - ModernPointSightYOffset);
    }

    private static bool CanUseModernTraversal(
        SimulationWorld world,
        PlayerEntity player,
        ClientBotNavPoints navPoints,
        BotNavigationNode currentNode,
        BotNavigationNode candidateNode,
        BotNavigationEdge edge)
    {
        GetModernClassJumpProfile(player.ClassId, out var jumpRange, out _, out var jumpHeightTotal);
        var currentFeetY = GetModernNodeFeetY(navPoints, currentNode);
        var candidateFeetY = GetModernNodeFeetY(navPoints, candidateNode);
        var jumpRiseNeeded = MathF.Max(0f, currentFeetY - candidateFeetY);
        if (jumpRiseNeeded > jumpHeightTotal)
        {
            return false;
        }

        var jumpDistanceNeeded = MathF.Abs(candidateNode.X - currentNode.X);
        if (jumpDistanceNeeded > jumpRange
            && !LineHitsSolid(world, player, currentNode.X, currentFeetY + 2f, candidateNode.X, candidateFeetY + 2f))
        {
            return false;
        }

        return true;
    }

    private static bool CanUseModernTraversal(
        SimulationWorld world,
        PlayerEntity player,
        BotNavigationRuntimeGraph navigationGraph,
        BotNavigationNode currentNode,
        BotNavigationNode candidateNode,
        BotNavigationEdge edge)
    {
        GetModernClassJumpProfile(player.ClassId, out var jumpRange, out _, out var jumpHeightTotal);
        var currentFeetY = GetModernNodeFeetY(navigationGraph, currentNode);
        var candidateFeetY = GetModernNodeFeetY(navigationGraph, candidateNode);
        var jumpRiseNeeded = MathF.Max(0f, currentFeetY - candidateFeetY);
        if (jumpRiseNeeded > jumpHeightTotal)
        {
            return false;
        }

        var jumpDistanceNeeded = MathF.Abs(candidateNode.X - currentNode.X);
        if (jumpDistanceNeeded > jumpRange
            && !LineHitsSolid(world, player, currentNode.X, currentFeetY + 2f, candidateNode.X, candidateFeetY + 2f))
        {
            return false;
        }

        return true;
    }

    private static bool CanUseUniversalGraphRouteEdge(
        SimulationWorld world,
        PlayerEntity player,
        BotNavigationRuntimeGraph navigationGraph,
        BotNavigationEdge edge,
        BotMemory memory)
    {
        if ((!ShouldBypassIntelReturnRouteEdgeBlock(player, memory)
                && IsRouteEdgeTemporarilyBlocked(memory, edge.FromNodeId, edge.ToNodeId))
            || navigationGraph.IsReverseOnlyTraversalBlocked(edge.ToNodeId, edge.FromNodeId)
            || !navigationGraph.TryGetNode(edge.FromNodeId, out var fromNode)
            || !navigationGraph.TryGetNode(edge.ToNodeId, out var toNode))
        {
            return false;
        }

        if (edge.InputTape.Count > 0 || edge.Kind == BotNavigationTraversalKind.Drop)
        {
            return true;
        }

        GetModernClassJumpProfile(player.ClassId, out var jumpRange, out _, out var jumpHeightTotal);
        var fromFeetY = GetModernNodeFeetY(navigationGraph, fromNode);
        var toFeetY = GetModernNodeFeetY(navigationGraph, toNode);
        var hasTerrainBetween = LineHitsSolid(world, player, fromNode.X, fromFeetY + 2f, toNode.X, toFeetY + 2f);
        var jumpRiseNeeded = MathF.Max(0f, fromFeetY - toFeetY);
        if (jumpRiseNeeded > jumpHeightTotal && !hasTerrainBetween)
        {
            return false;
        }

        var jumpDistanceNeeded = MathF.Abs(toNode.X - fromNode.X);
        if (jumpDistanceNeeded > jumpRange && !hasTerrainBetween)
        {
            return false;
        }

        return true;
    }

    private static bool CanUseBroadFallbackGraphRouteEdge(
        PlayerEntity player,
        BotNavigationRuntimeGraph navigationGraph,
        BotNavigationEdge edge,
        BotMemory memory)
    {
        return (ShouldBypassIntelReturnRouteEdgeBlock(player, memory)
                || !IsRouteEdgeTemporarilyBlocked(memory, edge.FromNodeId, edge.ToNodeId))
            && !navigationGraph.IsReverseOnlyTraversalBlocked(edge.ToNodeId, edge.FromNodeId)
            && navigationGraph.TryGetNode(edge.FromNodeId, out _)
            && navigationGraph.TryGetNode(edge.ToNodeId, out _);
    }


    private static float GetModernJumpRange(PlayerClass classId)
    {
        return classId switch
        {
            PlayerClass.Scout => 168f,
            PlayerClass.Heavy => 96f,
            _ => 120f,
        };
    }

    private static float GetModernJumpHeight(PlayerClass classId)
    {
        return classId switch
        {
            PlayerClass.Scout => 120f,
            _ => 60f,
        };
    }

    private static float GetModernNodeFeetY(BotNavigationRuntimeGraph navigationGraph, BotNavigationNode node)
    {
        _ = navigationGraph;
        return node.Y;
    }

    private static float GetModernNodeFeetY(ClientBotNavPoints navPoints, BotNavigationNode node)
    {
        _ = navPoints;
        return node.Y;
    }

    private static float GetModernPlayerFeetY(PlayerEntity player)
    {
        return player.Bottom;
    }

    private static string GetTraversalLabel(BotNavigationTraversalKind traversalKind)
    {
        return traversalKind switch
        {
            BotNavigationTraversalKind.Drop => "drop",
            BotNavigationTraversalKind.Jump => "jump",
            _ => "walk",
        };
    }

    private static BotNavigationTraversalKind GetEffectiveModernTraversalKind(
        BotNavigationNode currentNode,
        BotNavigationNode nextNode,
        BotNavigationTraversalKind traversalKind)
    {
        return traversalKind == BotNavigationTraversalKind.Jump
            && (nextNode.Y - currentNode.Y) >= ModernDropReclassifyDistance
            ? BotNavigationTraversalKind.Drop
            : traversalKind;
    }

    private static (float X, float Y) ResolveDestination(
        SimulationWorld world,
        PlayerEntity player,
        PlayerTeam team,
        BotRole role,
        PlayerEntity? healTarget)
    {
        if (player.ClassId == PlayerClass.Medic && healTarget is not null)
        {
            return (healTarget.X, healTarget.Y);
        }

        if (role == BotRole.ReturnWithIntel)
        {
            return ResolveTeamAnchor(world, team, preferObjective: false);
        }

        if (role == BotRole.EscortCarrier)
        {
            var allyCarrier = FindCarrier(world.EnumerateActiveNetworkPlayers()
                .Where(entry => entry.Player.Team == team)
                .Select(entry => entry.Player));
            if (allyCarrier is not null)
            {
                return (allyCarrier.X, allyCarrier.Y);
            }
        }

        if (role == BotRole.HuntCarrier)
        {
            var enemyCarrier = FindEnemyCarrier(world, team);
            if (enemyCarrier is not null)
            {
                return (enemyCarrier.X, enemyCarrier.Y);
            }
        }

        return world.MatchRules.Mode switch
        {
            GameModeKind.CaptureTheFlag => ResolveCaptureTheFlagDestination(world, team, role),
            GameModeKind.ControlPoint => ResolveControlPointDestination(world, team, role),
            GameModeKind.Generator => ResolveGeneratorDestination(world, team, role),
            GameModeKind.Arena => ResolveArenaDestination(world),
            GameModeKind.KingOfTheHill => ResolveKothDestination(world, team, role),
            GameModeKind.DoubleKingOfTheHill => ResolveKothDestination(world, team, role),
            GameModeKind.TeamDeathmatch => ResolveTeamDeathmatchDestination(world, team, role),
            _ => ResolveTeamAnchor(world, team, preferObjective: role != BotRole.DefendObjective),
        };
    }

    private static (float X, float Y) ResolveCaptureTheFlagDestination(SimulationWorld world, PlayerTeam team, BotRole role)
    {
        if (role == BotRole.DefendObjective)
        {
            var ownIntel = GetTeamIntel(world, team);
            if (!ownIntel.IsAtBase || ownIntel.IsDropped)
            {
                return (ownIntel.X, ownIntel.Y);
            }

            return ResolveTeamAnchor(world, team, preferObjective: false);
        }

        var enemyIntel = GetTeamIntel(world, GetOpposingTeam(team));
        return (enemyIntel.X, enemyIntel.Y);
    }

    private static (float X, float Y) ResolveControlPointDestination(SimulationWorld world, PlayerTeam team, BotRole role)
    {
        if (world.ControlPoints.Count == 0)
        {
            return ResolveTeamAnchor(world, team, preferObjective: role != BotRole.DefendObjective);
        }

        if (role == BotRole.DefendObjective)
        {
            foreach (var point in world.ControlPoints)
            {
                if (point.Team == team
                    && point.CappingTeam.HasValue
                    && point.CappingTeam.Value != team)
                {
                    return (point.Marker.CenterX, point.Marker.CenterY);
                }
            }

            var defendedPoint = team == PlayerTeam.Red
                ? world.ControlPoints.Where(point => point.Team == team).OrderByDescending(static point => point.Index).FirstOrDefault()
                : world.ControlPoints.Where(point => point.Team == team).OrderBy(static point => point.Index).FirstOrDefault();
            if (defendedPoint is not null)
            {
                return (defendedPoint.Marker.CenterX, defendedPoint.Marker.CenterY);
            }
        }

        var attackPoint = team == PlayerTeam.Red
            ? world.ControlPoints.Where(point => !point.IsLocked && point.Team != team).OrderBy(static point => point.Index).FirstOrDefault()
            : world.ControlPoints.Where(point => !point.IsLocked && point.Team != team).OrderByDescending(static point => point.Index).FirstOrDefault();
        if (attackPoint is not null)
        {
            return (attackPoint.Marker.CenterX, attackPoint.Marker.CenterY);
        }

        return ResolveTeamAnchor(world, team, preferObjective: true);
    }

    private static (float X, float Y) ResolveGeneratorDestination(SimulationWorld world, PlayerTeam team, BotRole role)
    {
        var generator = role == BotRole.DefendObjective
            ? world.GetGenerator(team)
            : world.GetGenerator(GetOpposingTeam(team));
        if (generator is not null)
        {
            return (generator.Marker.CenterX, generator.Marker.CenterY);
        }

        return ResolveTeamAnchor(world, team, preferObjective: role != BotRole.DefendObjective);
    }

    private static (float X, float Y) ResolveArenaDestination(SimulationWorld world)
    {
        var arenaPoint = world.Level.GetFirstRoomObject(RoomObjectType.ArenaControlPoint);
        if (arenaPoint.HasValue)
        {
            return (arenaPoint.Value.CenterX, arenaPoint.Value.CenterY);
        }

        return GetLevelCenter(world);
    }

    private static (float X, float Y) ResolveKothDestination(SimulationWorld world, PlayerTeam team, BotRole role)
    {
        if (world.ControlPoints.Count == 0)
        {
            return ResolveTeamDeathmatchDestination(world, team, role);
        }

        if (world.MatchRules.Mode == GameModeKind.KingOfTheHill)
        {
            var point = GetSingleKothPoint(world);
            if (point is not null)
            {
                return (point.Marker.CenterX, point.Marker.CenterY);
            }

            return GetLevelCenter(world);
        }

        var ownPoint = GetDualKothPoint(world, team);
        var enemyPoint = GetDualKothPoint(world, GetOpposingTeam(team));
        if (role == BotRole.DefendObjective
            && ownPoint is not null
            && (ownPoint.Team != team || (ownPoint.CappingTeam.HasValue && ownPoint.CappingTeam.Value != team)))
        {
            return (ownPoint.Marker.CenterX, ownPoint.Marker.CenterY);
        }

        if (enemyPoint is not null)
        {
            return (enemyPoint.Marker.CenterX, enemyPoint.Marker.CenterY);
        }

        if (ownPoint is not null)
        {
            return (ownPoint.Marker.CenterX, ownPoint.Marker.CenterY);
        }

        return GetLevelCenter(world);
    }

    private static (float X, float Y) ResolveTeamDeathmatchDestination(SimulationWorld world, PlayerTeam team, BotRole role)
    {
        if (role == BotRole.DefendObjective)
        {
            return ResolveTeamAnchor(world, team, preferObjective: false);
        }

        var enemySpawn = world.Level.GetSpawn(GetOpposingTeam(team), 0);
        return (enemySpawn.X, enemySpawn.Y);
    }

    private static ControlPointState? GetSingleKothPoint(SimulationWorld world)
    {
        for (var index = 0; index < world.ControlPoints.Count; index += 1)
        {
            var point = world.ControlPoints[index];
            if (point.Marker.IsSingleKothControlPoint())
            {
                return point;
            }
        }

        return world.ControlPoints.Count > 0 ? world.ControlPoints[0] : null;
    }

    private static ControlPointState? GetNearestKothPoint(SimulationWorld world, PlayerEntity player)
    {
        ControlPointState? nearestPoint = null;
        var nearestDistance = float.PositiveInfinity;
        for (var index = 0; index < world.ControlPoints.Count; index += 1)
        {
            var point = world.ControlPoints[index];
            var distance = DistanceSquared(player.X, player.Y, point.Marker.CenterX, point.Marker.CenterY);
            if (distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = distance;
            nearestPoint = point;
        }

        return nearestPoint;
    }

    private static ControlPointState? GetDualKothPoint(SimulationWorld world, PlayerTeam homeTeam)
    {
        for (var index = 0; index < world.ControlPoints.Count; index += 1)
        {
            var point = world.ControlPoints[index];
            if ((homeTeam == PlayerTeam.Red && point.Marker.IsRedKothControlPoint())
                || (homeTeam == PlayerTeam.Blue && point.Marker.IsBlueKothControlPoint()))
            {
                return point;
            }
        }

        return null;
    }

    internal static BotRole ResolveModernBotRole(
        SimulationWorld world,
        PlayerEntity player,
        PlayerTeam team,
        IReadOnlyList<PlayerEntity> allPlayers)
    {
        if (player.IsCarryingIntel)
        {
            return BotRole.ReturnWithIntel;
        }

        if (world.MatchRules.Mode != GameModeKind.CaptureTheFlag)
        {
            return BotRole.None;
        }

        var allyCarrier = FindCarrier(allPlayers.Where(candidate => candidate.Team == team && !ReferenceEquals(candidate, player)));
        if (allyCarrier is not null)
        {
            return BotRole.EscortCarrier;
        }

        var enemyCarrier = FindCarrier(allPlayers.Where(candidate => candidate.Team != team));
        return enemyCarrier is not null
            ? BotRole.HuntCarrier
            : BotRole.None;
    }

    private static ModernPathSelection ResolveModernPathSelection(
        SimulationWorld world,
        PlayerEntity player,
        PlayerTeam team,
        IReadOnlyList<PlayerEntity> allPlayers,
        BotRole role,
        PlayerEntity? medicBuddyTarget)
    {
        var baseSelection = world.MatchRules.Mode switch
        {
            GameModeKind.CaptureTheFlag => ResolveModernCaptureTheFlagPath(world, player, team, allPlayers, role),
            GameModeKind.ControlPoint => ResolveModernControlPointPath(world, player, team),
            GameModeKind.Generator => ResolveModernGeneratorPath(world, team),
            GameModeKind.Arena => ResolveModernArenaPath(world, player),
            GameModeKind.KingOfTheHill => ResolveModernKothPath(world, player),
            GameModeKind.DoubleKingOfTheHill => ResolveModernKothPath(world, player),
            _ => new ModernPathSelection((player.X, player.Y), AllowDirectPath: true),
        };

        return baseSelection;
    }

    private static void EnsureClientBot2020CompatStateInitialized(BotMemory memory)
    {
        memory.ClientBot2020Compat ??= new ClientBot2020CompatState();
    }

    private static ModernPathSelection ResolveClientBot2020CompatPathSelection(
        SimulationWorld world,
        PlayerEntity player,
        PlayerTeam team,
        IReadOnlyList<PlayerEntity> allPlayers,
        BotRole role,
        PlayerEntity? medicBuddyTarget,
        ClientBotNavPoints navPoints,
        BotMemory memory,
        BotTimingProfile timing)
    {
        _ = navPoints;
        _ = timing;
        var state = memory.ClientBot2020Compat ??= new ClientBot2020CompatState();
        state.Defending = false;
        state.CaptureObjectiveMode = false;
        state.PathPointIsZone = false;

        if (state.CaptureZoneRefreshTicksRemaining <= 0)
        {
            state.CaptureZoneRefreshTicksRemaining = 120;
            state.HasCaptureZoneObject = world.Level.GetRoomObjects(RoomObjectType.CaptureZone).Count > 0;
        }
        else
        {
            state.CaptureZoneRefreshTicksRemaining = Math.Max(0, state.CaptureZoneRefreshTicksRemaining - 1);
        }

        if (world.Level.GetRoomObjects(RoomObjectType.ArenaControlPoint).Count > 0)
        {
            state.CaptureObjectiveMode = true;
            var pathPoint = world.Level
                .GetRoomObjects(RoomObjectType.ArenaControlPoint)
                .OrderBy(point => DistanceSquared(player.X, player.Y, point.CenterX, point.CenterY))
                .First();
            if (state.HasCaptureZoneObject
                && TryResolveModernCaptureObjective(world, player, pathPoint, out var captureObjective))
            {
                state.PathPointIsZone = true;
                return new ModernPathSelection(
                    captureObjective.TargetZone,
                    AllowDirectPath: false,
                    IsCaptureObjective: true,
                    CaptureObjective: captureObjective);
            }

            return new ModernPathSelection((pathPoint.CenterX, pathPoint.CenterY), AllowDirectPath: false, IsCaptureObjective: true);
        }

        var nearestKothPoint = world.MatchRules.Mode is GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill
            ? GetNearestKothPoint(world, player)
            : null;
        if (nearestKothPoint is not null)
        {
            // clientBot uses instance_nearest(..., KothControlPoint) for both KOTH variants.
            state.CaptureObjectiveMode = true;
            if (state.HasCaptureZoneObject
                && TryResolveModernCaptureObjective(world, player, nearestKothPoint.Marker, out var captureObjective))
            {
                state.PathPointIsZone = true;
                return new ModernPathSelection(
                    captureObjective.TargetZone,
                    AllowDirectPath: false,
                    IsCaptureObjective: true,
                    CaptureObjective: captureObjective);
            }

            return new ModernPathSelection((nearestKothPoint.Marker.CenterX, nearestKothPoint.Marker.CenterY), AllowDirectPath: false, IsCaptureObjective: true);
        }

        if (world.Level.GetIntelBase(PlayerTeam.Red).HasValue || world.Level.GetIntelBase(PlayerTeam.Blue).HasValue)
        {
            if (player.IsCarryingIntel)
            {
                return new ModernPathSelection(ResolveTeamAnchor(world, team, preferObjective: false), AllowDirectPath: false);
            }

            // Source uses defending=false here, so attack path remains the default.
            var enemyCarrier = FindCarrier(allPlayers.Where(candidate => candidate.Team != team));
            if (enemyCarrier is not null && role == BotRole.HuntCarrier)
            {
                return new ModernPathSelection((enemyCarrier.X, enemyCarrier.Y), AllowDirectPath: true);
            }

            var allyCarrier = FindCarrier(allPlayers.Where(candidate => candidate.Team == team && !ReferenceEquals(candidate, player)));
            if (allyCarrier is not null && role == BotRole.EscortCarrier)
            {
                return new ModernPathSelection((allyCarrier.X, allyCarrier.Y), AllowDirectPath: true);
            }

            var enemyIntel = GetTeamIntel(world, GetOpposingTeam(team));
            if (enemyIntel.IsCarried)
            {
                allyCarrier = FindCarrier(allPlayers.Where(candidate => candidate.Team == team));
                if (allyCarrier is not null && !ReferenceEquals(allyCarrier, player))
                {
                    return new ModernPathSelection((allyCarrier.X, allyCarrier.Y), AllowDirectPath: true);
                }
            }

            return new ModernPathSelection((enemyIntel.X, enemyIntel.Y), AllowDirectPath: false);
        }

        var generator = world.GetGenerator(GetOpposingTeam(team));
        if (generator is not null)
        {
            return new ModernPathSelection((generator.Marker.CenterX, generator.Marker.CenterY), AllowDirectPath: false);
        }

        if (world.ControlPoints.Count > 0)
        {
            state.CaptureObjectiveMode = true;
            ControlPointState? chosenPoint = null;
            var chosenDistance = float.PositiveInfinity;
            for (var index = 0; index < world.ControlPoints.Count; index += 1)
            {
                var point = world.ControlPoints[index];
                if (point.IsLocked || point.Team == team)
                {
                    continue;
                }

                var pointDistance = DistanceBetween(point.Marker.CenterX, point.Marker.CenterY, player.X, player.Y);
                if (pointDistance >= chosenDistance)
                {
                    continue;
                }

                chosenDistance = pointDistance;
                chosenPoint = point;
            }

            if (chosenPoint is not null)
            {
                if (state.HasCaptureZoneObject
                    && TryResolveModernCaptureObjective(world, player, chosenPoint.Marker, out var captureObjective))
                {
                    state.PathPointIsZone = true;
                    return new ModernPathSelection(
                        captureObjective.TargetZone,
                        AllowDirectPath: false,
                        IsCaptureObjective: true,
                        CaptureObjective: captureObjective);
                }

                return new ModernPathSelection((chosenPoint.Marker.CenterX, chosenPoint.Marker.CenterY), AllowDirectPath: false, IsCaptureObjective: true);
            }
        }

        if (medicBuddyTarget is not null)
        {
            return new ModernPathSelection((medicBuddyTarget.X, medicBuddyTarget.Y), AllowDirectPath: true);
        }

        return new ModernPathSelection((player.X, player.Y), AllowDirectPath: true);
    }

    private static ModernPathSelection ResolveModernCaptureTheFlagPath(
        SimulationWorld world,
        PlayerEntity player,
        PlayerTeam team,
        IReadOnlyList<PlayerEntity> allPlayers,
        BotRole role)
    {
        if (role == BotRole.ReturnWithIntel || player.IsCarryingIntel)
        {
            return new ModernPathSelection(ResolveTeamAnchor(world, team, preferObjective: false), AllowDirectPath: false);
        }

        if (role == BotRole.EscortCarrier)
        {
            var allyCarrier = FindCarrier(allPlayers.Where(candidate => candidate.Team == team && !ReferenceEquals(candidate, player)));
            if (allyCarrier is not null)
            {
                return new ModernPathSelection((allyCarrier.X, allyCarrier.Y), AllowDirectPath: true);
            }
        }

        if (role == BotRole.HuntCarrier)
        {
            var enemyCarrier = FindCarrier(allPlayers.Where(candidate => candidate.Team != team));
            if (enemyCarrier is not null)
            {
                return new ModernPathSelection((enemyCarrier.X, enemyCarrier.Y), AllowDirectPath: true);
            }
        }

        var enemyIntel = GetTeamIntel(world, GetOpposingTeam(team));
        if (enemyIntel.IsCarried)
        {
            var allyCarrier = FindCarrier(allPlayers.Where(candidate => candidate.Team == team));
            if (allyCarrier is not null && !ReferenceEquals(allyCarrier, player))
            {
                return new ModernPathSelection((allyCarrier.X, allyCarrier.Y), AllowDirectPath: true);
            }
        }

        return new ModernPathSelection((enemyIntel.X, enemyIntel.Y), AllowDirectPath: false);
    }

    private static ModernPathSelection ResolveModernControlPointPath(
        SimulationWorld world,
        PlayerEntity player,
        PlayerTeam team)
    {
        ControlPointState? bestPoint = null;
        var bestDistanceSquared = float.PositiveInfinity;
        for (var index = 0; index < world.ControlPoints.Count; index += 1)
        {
            var point = world.ControlPoints[index];
            if (point.IsLocked || point.Team == team)
            {
                continue;
            }

            var distanceSquared = DistanceSquared(player.X, player.Y, point.Marker.CenterX, point.Marker.CenterY);
            if (distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            bestPoint = point;
        }

        if (bestPoint is not null)
        {
            if (TryResolveModernCaptureObjective(world, player, bestPoint.Marker, out var captureObjective))
            {
                return new ModernPathSelection(
                    captureObjective.TargetZone,
                    AllowDirectPath: false,
                    IsCaptureObjective: true,
                    CaptureObjective: captureObjective);
            }

            return new ModernPathSelection((bestPoint.Marker.CenterX, bestPoint.Marker.CenterY), AllowDirectPath: false, IsCaptureObjective: true);
        }

        return new ModernPathSelection((player.X, player.Y), AllowDirectPath: true);
    }

    private static ModernPathSelection ResolveModernGeneratorPath(
        SimulationWorld world,
        PlayerTeam team)
    {
        var generator = world.GetGenerator(GetOpposingTeam(team));
        return generator is not null
            ? new ModernPathSelection((generator.Marker.CenterX, generator.Marker.CenterY), AllowDirectPath: false)
            : new ModernPathSelection((world.Level.Bounds.Width * 0.5f, world.Level.Bounds.Height * 0.5f), AllowDirectPath: false);
    }

    private static ModernPathSelection ResolveModernArenaPath(
        SimulationWorld world,
        PlayerEntity player)
    {
        var bestPoint = world.Level
            .GetRoomObjects(RoomObjectType.ArenaControlPoint)
            .OrderBy(point => DistanceSquared(player.X, player.Y, point.CenterX, point.CenterY))
            .FirstOrDefault();
        if (bestPoint.Type == RoomObjectType.ArenaControlPoint)
        {
            if (TryResolveModernCaptureObjective(world, player, bestPoint, out var captureObjective))
            {
                return new ModernPathSelection(
                    captureObjective.TargetZone,
                    AllowDirectPath: false,
                    IsCaptureObjective: true,
                    CaptureObjective: captureObjective);
            }

            return new ModernPathSelection((bestPoint.CenterX, bestPoint.CenterY), AllowDirectPath: false, IsCaptureObjective: true);
        }

        return new ModernPathSelection((player.X, player.Y), AllowDirectPath: true);
    }

    private static ModernPathSelection ResolveModernKothPath(
        SimulationWorld world,
        PlayerEntity player)
    {
        var bestPoint = world.MatchRules.Mode == GameModeKind.DoubleKingOfTheHill
            ? GetDualKothPoint(world, player.Team)
            : GetSingleKothPoint(world);

        if (bestPoint is not null)
        {
            if (TryResolveModernCaptureObjective(world, player, bestPoint.Marker, out var captureObjective))
            {
                return new ModernPathSelection(
                    captureObjective.TargetZone,
                    AllowDirectPath: false,
                    IsCaptureObjective: true,
                    CaptureObjective: captureObjective);
            }

            return new ModernPathSelection((bestPoint.Marker.CenterX, bestPoint.Marker.CenterY), AllowDirectPath: false, IsCaptureObjective: true);
        }

        return new ModernPathSelection((player.X, player.Y), AllowDirectPath: true);
    }

    internal static bool TryResolveModernCaptureObjective(
        SimulationWorld world,
        PlayerEntity player,
        RoomObjectMarker controlPointMarker,
        out ModernCaptureObjective captureObjective)
    {
        captureObjective = default;
        var captureZones = world.Level.GetRoomObjects(RoomObjectType.CaptureZone);
        if (captureZones.Count == 0)
        {
            return false;
        }

        var nearestZoneIndex = -1;
        var nearestZoneDistance = float.PositiveInfinity;
        var zoneCandidates = new ModernCaptureZoneCandidate[captureZones.Count];
        for (var zoneIndex = 0; zoneIndex < captureZones.Count; zoneIndex += 1)
        {
            var zone = captureZones[zoneIndex];
            var candidate = new ModernCaptureZoneCandidate(
                zone.CenterX,
                zone.CenterY,
                DistanceBetween(zone.CenterX, zone.CenterY, controlPointMarker.CenterX, controlPointMarker.CenterY));
            zoneCandidates[zoneIndex] = candidate;

            if (candidate.DistanceToControlPoint >= nearestZoneDistance)
            {
                continue;
            }

            nearestZoneDistance = candidate.DistanceToControlPoint;
            nearestZoneIndex = zoneIndex;
        }

        if (nearestZoneIndex < 0)
        {
            return false;
        }

        var includedZones = new bool[zoneCandidates.Length];
        var zoneClusterPointIndices = new int[zoneCandidates.Length];
        Array.Fill(zoneClusterPointIndices, -1);

        var seedNeighborDistance = float.PositiveInfinity;
        var seedCount = 0;
        var seedZone = zoneCandidates[nearestZoneIndex];
        for (var zoneIndex = 0; zoneIndex < zoneCandidates.Length; zoneIndex += 1)
        {
            var candidate = zoneCandidates[zoneIndex];
            if (candidate.DistanceToControlPoint <= nearestZoneDistance + ModernCaptureZoneSeedBand)
            {
                includedZones[zoneIndex] = true;
                seedCount += 1;
            }

            if (zoneIndex == nearestZoneIndex)
            {
                continue;
            }

            var candidateDistance = DistanceBetween(seedZone.X, seedZone.Y, candidate.X, candidate.Y);
            if (candidateDistance > 0f && candidateDistance < seedNeighborDistance)
            {
                seedNeighborDistance = candidateDistance;
            }
        }

        if (seedNeighborDistance == float.PositiveInfinity)
        {
            seedNeighborDistance = 12f;
        }

        var linkDistance = MathF.Max(12f, MathF.Min(54f, seedNeighborDistance * 2.25f));
        if (seedCount <= 0)
        {
            includedZones[nearestZoneIndex] = true;
        }

        for (var pass = 0; pass < 128; pass += 1)
        {
            var changed = false;
            for (var zoneIndex = 0; zoneIndex < zoneCandidates.Length; zoneIndex += 1)
            {
                if (includedZones[zoneIndex])
                {
                    continue;
                }

                for (var neighborIndex = 0; neighborIndex < zoneCandidates.Length; neighborIndex += 1)
                {
                    if (!includedZones[neighborIndex])
                    {
                        continue;
                    }

                    if (DistanceBetween(
                            zoneCandidates[zoneIndex].X,
                            zoneCandidates[zoneIndex].Y,
                            zoneCandidates[neighborIndex].X,
                            zoneCandidates[neighborIndex].Y) > linkDistance)
                    {
                        continue;
                    }

                    includedZones[zoneIndex] = true;
                    changed = true;
                    break;
                }
            }

            if (!changed)
            {
                break;
            }
        }

        var clusterMinX = float.PositiveInfinity;
        var clusterMaxX = float.NegativeInfinity;
        var clusterMinY = float.PositiveInfinity;
        var clusterMaxY = float.NegativeInfinity;
        var groupedPoints = new ModernCapturePoint[zoneCandidates.Length];
        var groupedPointCount = 0;
        for (var zoneIndex = 0; zoneIndex < zoneCandidates.Length; zoneIndex += 1)
        {
            if (!includedZones[zoneIndex])
            {
                continue;
            }

            var candidate = zoneCandidates[zoneIndex];
            zoneClusterPointIndices[zoneIndex] = groupedPointCount;
            groupedPoints[groupedPointCount] = new ModernCapturePoint(candidate.X, candidate.Y, GroupId: -1);
            groupedPointCount += 1;
            clusterMinX = MathF.Min(clusterMinX, candidate.X);
            clusterMaxX = MathF.Max(clusterMaxX, candidate.X);
            clusterMinY = MathF.Min(clusterMinY, candidate.Y);
            clusterMaxY = MathF.Max(clusterMaxY, candidate.Y);
        }

        if (groupedPointCount <= 0)
        {
            return false;
        }

        Array.Resize(ref groupedPoints, groupedPointCount);
        var groupCount = 0;
        for (var pointIndex = 0; pointIndex < groupedPoints.Length; pointIndex += 1)
        {
            if (groupedPoints[pointIndex].GroupId >= 0)
            {
                continue;
            }

            groupedPoints[pointIndex] = groupedPoints[pointIndex] with { GroupId = groupCount };
            var groupChanged = true;
            while (groupChanged)
            {
                groupChanged = false;
                for (var pointA = 0; pointA < groupedPoints.Length; pointA += 1)
                {
                    if (groupedPoints[pointA].GroupId != groupCount)
                    {
                        continue;
                    }

                    for (var pointB = 0; pointB < groupedPoints.Length; pointB += 1)
                    {
                        if (groupedPoints[pointB].GroupId >= 0)
                        {
                            continue;
                        }

                        if (DistanceBetween(
                                groupedPoints[pointA].X,
                                groupedPoints[pointA].Y,
                                groupedPoints[pointB].X,
                                groupedPoints[pointB].Y) > ModernCaptureZoneGroupLinkDistance)
                        {
                            continue;
                        }

                        groupedPoints[pointB] = groupedPoints[pointB] with { GroupId = groupCount };
                        groupChanged = true;
                    }
                }
            }

            groupCount += 1;
        }

        var groups = new ModernCaptureGroup[groupCount];
        for (var groupIndex = 0; groupIndex < groups.Length; groupIndex += 1)
        {
            groups[groupIndex] = new ModernCaptureGroup(
                groupIndex,
                float.PositiveInfinity,
                float.NegativeInfinity,
                float.PositiveInfinity,
                float.NegativeInfinity,
                PointCount: 0,
                NearestDistanceToBot: float.PositiveInfinity);
        }

        for (var pointIndex = 0; pointIndex < groupedPoints.Length; pointIndex += 1)
        {
            var point = groupedPoints[pointIndex];
            var group = groups[point.GroupId];
            groups[point.GroupId] = group with
            {
                MinX = MathF.Min(group.MinX, point.X),
                MaxX = MathF.Max(group.MaxX, point.X),
                MinY = MathF.Min(group.MinY, point.Y),
                MaxY = MathF.Max(group.MaxY, point.Y),
                PointCount = group.PointCount + 1,
                NearestDistanceToBot = MathF.Min(group.NearestDistanceToBot, DistanceBetween(player.X, player.Y, point.X, point.Y)),
            };
        }

        var targetGroupId = -1;
        var targetGroupDistance = float.PositiveInfinity;
        var tiePick = Math.Abs(player.Id) % Math.Max(1, groups.Length);
        for (var groupIndex = 0; groupIndex < groups.Length; groupIndex += 1)
        {
            var group = groups[groupIndex];
            if (group.PointCount <= 0)
            {
                continue;
            }

            if (group.NearestDistanceToBot < targetGroupDistance - 1f
                || (MathF.Abs(group.NearestDistanceToBot - targetGroupDistance) <= 1f && groupIndex == tiePick))
            {
                targetGroupDistance = group.NearestDistanceToBot;
                targetGroupId = groupIndex;
            }
        }

        if (targetGroupId < 0)
        {
            return false;
        }

        var selectedGroup = groups[targetGroupId];
        var targetZoneIndex = -1;
        var targetZoneDistance = float.PositiveInfinity;
        for (var zoneIndex = 0; zoneIndex < zoneCandidates.Length; zoneIndex += 1)
        {
            if (!includedZones[zoneIndex])
            {
                continue;
            }

            var clusterPointIndex = zoneClusterPointIndices[zoneIndex];
            if (clusterPointIndex < 0 || groupedPoints[clusterPointIndex].GroupId != targetGroupId)
            {
                continue;
            }

            var distanceToBot = DistanceBetween(player.X, player.Y, zoneCandidates[zoneIndex].X, zoneCandidates[zoneIndex].Y);
            if (distanceToBot >= targetZoneDistance)
            {
                continue;
            }

            targetZoneDistance = distanceToBot;
            targetZoneIndex = zoneIndex;
        }

        if (targetZoneIndex < 0)
        {
            return false;
        }

        var selectedGroupCenter = (
            X: (selectedGroup.MinX + selectedGroup.MaxX) * 0.5f,
            Y: (selectedGroup.MinY + selectedGroup.MaxY) * 0.5f);

        captureObjective = new ModernCaptureObjective(
            (zoneCandidates[targetZoneIndex].X, zoneCandidates[targetZoneIndex].Y),
            selectedGroupCenter,
            clusterMinX,
            clusterMaxX,
            clusterMinY,
            clusterMaxY,
            groupedPoints,
            groups);
        return true;
    }

    internal static bool TryResolveModernRouteAwareCaptureSelection(
        SimulationWorld world,
        PlayerEntity player,
        ClientBotNavPoints navPoints,
        BotMemory memory,
        ModernPathSelection selection,
        out ModernPathSelection routeAwareSelection)
    {
        routeAwareSelection = default;
        if (!selection.IsCaptureObjective
            || selection.CaptureObjective is not ModernCaptureObjective captureObjective
            || captureObjective.Points.Length == 0
            || captureObjective.Groups.Length == 0)
        {
            return false;
        }

        if (!string.Equals(memory.NavigationGraphKey, navPoints.CacheKey, StringComparison.Ordinal))
        {
            ResetModernNavigationState(memory);
            memory.NavigationGraphKey = navPoints.CacheKey;
        }

        if (!TryGetCurrentModernNode(navPoints, memory, out var currentNode))
        {
            if (!TryAcquireModernCurrentNode(
                    world,
                    navPoints,
                    player,
                    memory,
                    honorSecondAnchorBlock: true,
                    out currentNode))
            {
                return false;
            }

            memory.CurrentPointId = currentNode.Id;
            memory.NextPointId = -1;
            memory.NextPoint2Id = -1;
            memory.NextPoint3Id = -1;
        }

        var bestScore = float.PositiveInfinity;
        var bestPoint = default(ModernCapturePoint);
        var bestGroup = default(ModernCaptureGroup);
        var found = false;
        var evaluatedPointIds = new HashSet<int>();
        for (var pointIndex = 0; pointIndex < captureObjective.Points.Length; pointIndex += 1)
        {
            var point = captureObjective.Points[pointIndex];
            if (point.GroupId < 0 || point.GroupId >= captureObjective.Groups.Length)
            {
                continue;
            }

            if (!navPoints.TryFindNearestPoint(point.X, point.Y, out var candidateGoalNode)
                || !evaluatedPointIds.Add(candidateGoalNode.Id))
            {
                continue;
            }

            if (MathF.Abs(GetModernNodeFeetY(navPoints, candidateGoalNode) - point.Y) > ModernPointVerticalDistance)
            {
                continue;
            }

            var routeLength = 1f;
            if (candidateGoalNode.Id != currentNode.Id)
            {
                var goalWeights = navPoints.GetGoalWeights(candidateGoalNode.Id, ModernMaximumWeightDepth, player.ClassId);
                if (goalWeights is null)
                {
                    continue;
                }

                var currentWeight = GetModernGoalWeight(goalWeights, currentNode.Id);
                if (currentWeight <= 0)
                {
                    continue;
                }

                routeLength = currentWeight;
            }

            var group = captureObjective.Groups[point.GroupId];
            var groupCenterX = (group.MinX + group.MaxX) * 0.5f;
            var groupCenterY = (group.MinY + group.MaxY) * 0.5f;
            var score = (routeLength * 1000f)
                + DistanceBetween(player.X, player.Y, point.X, point.Y)
                + DistanceBetween(candidateGoalNode.X, GetModernNodeFeetY(navPoints, candidateGoalNode), point.X, point.Y)
                + (DistanceBetween(point.X, point.Y, groupCenterX, groupCenterY) * 0.25f);

            if (point.GroupId == memory.CaptureActiveGroupId)
            {
                score -= 150f;
            }

            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestPoint = point;
            bestGroup = group;
            found = true;
        }

        if (!found)
        {
            return false;
        }

        var adjustedObjective = captureObjective with
        {
            TargetZone = (bestPoint.X, bestPoint.Y),
            TargetPoint = (
                X: (bestGroup.MinX + bestGroup.MaxX) * 0.5f,
                Y: (bestGroup.MinY + bestGroup.MaxY) * 0.5f),
        };
        routeAwareSelection = selection with
        {
            Destination = adjustedObjective.TargetZone,
            CaptureObjective = adjustedObjective,
        };
        return true;
    }

    private static ModernCaptureHoldState ResolveModernCaptureHoldState(
        SimulationWorld world,
        PlayerEntity player,
        ModernPathSelection objectiveSelection,
        BotMemory memory,
        double fixedDeltaSeconds)
    {
        if (!objectiveSelection.IsCaptureObjective
            || objectiveSelection.CaptureObjective is not ModernCaptureObjective captureObjective
            || captureObjective.Points.Length == 0)
        {
            memory.CaptureHoldInsideMilliseconds = 0f;
            memory.CaptureHoldRetainMilliseconds = 0f;
            memory.CaptureActiveGroupId = -1;
            return new ModernCaptureHoldState(SuppressNavigation: false, ActiveGroupId: -1, player.X, player.Bottom);
        }

        var deltaMilliseconds = (float)(fixedDeltaSeconds * 1000.0);
        if (deltaMilliseconds <= 0f || deltaMilliseconds > 200f)
        {
            deltaMilliseconds = 16f;
        }

        var insideZoneSquare = false;
        var activeGroupId = -1;
        var feetY = player.Bottom;
        if (player.X >= captureObjective.MinX - ModernCaptureZoneSquareHalfSize
            && player.X <= captureObjective.MaxX + ModernCaptureZoneSquareHalfSize
            && feetY >= captureObjective.MinY - ModernCaptureZoneSquareHalfSize
            && feetY <= captureObjective.MaxY + ModernCaptureZoneSquareHalfSize)
        {
            for (var pointIndex = 0; pointIndex < captureObjective.Points.Length; pointIndex += 1)
            {
                var point = captureObjective.Points[pointIndex];
                if (MathF.Abs(player.X - point.X) > ModernCaptureZoneSquareHalfSize
                    || MathF.Abs(feetY - point.Y) > ModernCaptureZoneSquareHalfSize)
                {
                    continue;
                }

                insideZoneSquare = true;
                activeGroupId = point.GroupId;
                break;
            }
        }

        var insideActualCaptureZone = TryResolveActualCaptureHoldState(
            world,
            player,
            captureObjective,
            out var actualCaptureGroupId,
            out var activeControlPoint,
            out var teamProgressNearCapture);
        if (activeGroupId < 0 && actualCaptureGroupId >= 0)
        {
            activeGroupId = actualCaptureGroupId;
        }

        var holdBufferMilliseconds = 500f;
        if (activeGroupId >= 0 && activeGroupId < captureObjective.Groups.Length)
        {
            var group = captureObjective.Groups[activeGroupId];
            var groupWidth = group.MaxX - group.MinX;
            var groupHeight = group.MaxY - group.MinY;
            var narrowSpan = MathF.Min(groupWidth, groupHeight);
            holdBufferMilliseconds = MathF.Max(120f, MathF.Min(500f, 120f + (narrowSpan * 2f)));
        }

        if (insideZoneSquare || insideActualCaptureZone)
        {
            memory.CaptureHoldInsideMilliseconds = MathF.Min(holdBufferMilliseconds, memory.CaptureHoldInsideMilliseconds + deltaMilliseconds);
        }
        else
        {
            memory.CaptureHoldInsideMilliseconds = MathF.Max(0f, memory.CaptureHoldInsideMilliseconds - (deltaMilliseconds * 1.5f));
        }

        if (insideActualCaptureZone || teamProgressNearCapture)
        {
            memory.CaptureHoldRetainMilliseconds = ModernCaptureRetainMilliseconds;
        }
        else
        {
            memory.CaptureHoldRetainMilliseconds = MathF.Max(0f, memory.CaptureHoldRetainMilliseconds - deltaMilliseconds);
        }

        var suppressNavigation = insideActualCaptureZone
            || teamProgressNearCapture
            || memory.CaptureHoldRetainMilliseconds > 0f
            || (insideZoneSquare && memory.CaptureHoldInsideMilliseconds >= holdBufferMilliseconds);
        var captureSettleTarget = ResolveModernCaptureSettleTarget(captureObjective);
        var target = insideActualCaptureZone || insideZoneSquare
            ? (X: player.X, Y: player.Bottom)
            : captureSettleTarget;

        memory.CaptureActiveGroupId = activeGroupId;
        return new ModernCaptureHoldState(
            SuppressNavigation: suppressNavigation,
            ActiveGroupId: activeGroupId,
            TargetX: target.X,
            TargetY: target.Y);
    }

    private static bool TryResolveActualCaptureHoldState(
        SimulationWorld world,
        PlayerEntity player,
        ModernCaptureObjective captureObjective,
        out int activeGroupId,
        out ControlPointState? activeControlPoint,
        out bool teamProgressNearCapture)
    {
        activeGroupId = -1;
        activeControlPoint = null;
        teamProgressNearCapture = false;
        if (!TryFindNearestControlPoint(world, captureObjective.TargetPoint.X, captureObjective.TargetPoint.Y, out var targetControlPoint))
        {
            return false;
        }

        activeControlPoint = targetControlPoint;
        var teamCappers = player.Team == PlayerTeam.Red
            ? targetControlPoint.RedCappers
            : targetControlPoint.BlueCappers;
        var targetDistance = DistanceBetween(player.X, player.Bottom, captureObjective.TargetPoint.X, captureObjective.TargetPoint.Y);
        teamProgressNearCapture = targetDistance <= ModernCaptureProgressRetainDistance
            && (teamCappers > 0
                || (targetControlPoint.CappingTeam == player.Team && targetControlPoint.CappingTicks > 0f));

        foreach (var zone in world.Level.GetRoomObjects(RoomObjectType.CaptureZone))
        {
            if (!TryFindNearestControlPoint(world, zone.CenterX, zone.CenterY, out var zoneControlPoint)
                || zoneControlPoint.Index != targetControlPoint.Index
                || !player.IntersectsMarker(zone.CenterX, zone.CenterY, zone.Width, zone.Height))
            {
                continue;
            }

            activeGroupId = FindNearestCaptureGroupId(captureObjective, player.X, player.Bottom);
            return true;
        }

        return false;
    }

    private static bool TryFindNearestControlPoint(
        SimulationWorld world,
        float x,
        float y,
        out ControlPointState controlPoint)
    {
        controlPoint = default!;
        var bestDistanceSquared = float.PositiveInfinity;
        for (var index = 0; index < world.ControlPoints.Count; index += 1)
        {
            var candidate = world.ControlPoints[index];
            var distanceSquared = DistanceSquared(x, y, candidate.Marker.CenterX, candidate.Marker.CenterY);
            if (distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            controlPoint = candidate;
            bestDistanceSquared = distanceSquared;
        }

        return !float.IsPositiveInfinity(bestDistanceSquared);
    }

    private static int FindNearestCaptureGroupId(ModernCaptureObjective captureObjective, float x, float y)
    {
        var bestGroupId = -1;
        var bestDistanceSquared = float.PositiveInfinity;
        for (var pointIndex = 0; pointIndex < captureObjective.Points.Length; pointIndex += 1)
        {
            var point = captureObjective.Points[pointIndex];
            if (point.GroupId < 0 || point.GroupId >= captureObjective.Groups.Length)
            {
                continue;
            }

            var distanceSquared = DistanceSquared(x, y, point.X, point.Y);
            if (distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestGroupId = point.GroupId;
            bestDistanceSquared = distanceSquared;
        }

        return bestGroupId;
    }

    private static (float X, float Y) ResolveModernCaptureSettleTarget(ModernCaptureObjective captureObjective)
    {
        for (var pointIndex = 0; pointIndex < captureObjective.Points.Length; pointIndex += 1)
        {
            var point = captureObjective.Points[pointIndex];
            if (point.X != captureObjective.TargetZone.X
                || point.Y != captureObjective.TargetZone.Y
                || point.GroupId < 0
                || point.GroupId >= captureObjective.Groups.Length)
            {
                continue;
            }

            var group = captureObjective.Groups[point.GroupId];
            return (
                X: (group.MinX + group.MaxX) * 0.5f,
                Y: (group.MinY + group.MaxY) * 0.5f);
        }

        return captureObjective.TargetPoint;
    }

    private static bool HasModernCaptureEnemyNearby(
        SimulationWorld world,
        PlayerEntity player,
        PlayerEntity? currentEnemyTarget)
    {
        if (currentEnemyTarget is not null
            && currentEnemyTarget.IsAlive
            && currentEnemyTarget.Team != player.Team
            && DistanceBetween(player.X, player.Y, currentEnemyTarget.X, currentEnemyTarget.Y) < ModernCaptureEnemyNearbyDistance
            && HasModernObstacleLineOfSight(world, currentEnemyTarget.X, currentEnemyTarget.Y, player.X, player.Y))
        {
            return true;
        }

        foreach (var candidate in world.RemoteSnapshotPlayers)
        {
            if (!candidate.IsAlive
                || candidate.Team == player.Team
                || DistanceBetween(player.X, player.Y, candidate.X, candidate.Y) >= ModernCaptureEnemyNearbyDistance)
            {
                continue;
            }

            if (HasModernObstacleLineOfSight(world, candidate.X, candidate.Y, player.X, player.Y))
            {
                return true;
            }
        }

        return false;
    }

    private static (float X, float Y) ResolveTeamAnchor(SimulationWorld world, PlayerTeam team, bool preferObjective)
    {
        if (preferObjective)
        {
            var enemyBase = world.Level.GetIntelBase(GetOpposingTeam(team));
            if (enemyBase.HasValue)
            {
                return (enemyBase.Value.X, enemyBase.Value.Y);
            }
        }

        var ownBase = world.Level.GetIntelBase(team);
        if (ownBase.HasValue)
        {
            return (ownBase.Value.X, ownBase.Value.Y);
        }

        var spawn = world.Level.GetSpawn(team, 0);
        return (spawn.X, spawn.Y);
    }

    private PlayerEntity? FindBestEnemyTarget(
        SimulationWorld world,
        PlayerEntity player,
        PlayerTeam team,
        BotRole role,
        (float X, float Y) destination,
        IReadOnlyList<PlayerEntity> allPlayers,
        BotMemory memory,
        BotTimingProfile timing,
        bool useModernBehavior)
    {
        if (useModernBehavior)
        {
            return FindBestModernEnemyTarget(world, player, team, allPlayers, memory, timing);
        }

        var stickyTarget = TryResolveStickyTarget(allPlayers, GetOpposingTeam(team), memory.TargetPlayerId);
        if (stickyTarget is not null && ShouldKeepStickyTarget(world, player, stickyTarget, memory))
        {
            memory.TargetLockTicksRemaining = Math.Max(memory.TargetLockTicksRemaining, timing.StickyTargetRefreshTicks);
            return stickyTarget;
        }

        PlayerEntity? bestTarget = null;
        var bestScore = float.MaxValue;

        for (var index = 0; index < allPlayers.Count; index += 1)
        {
            var candidate = allPlayers[index];
            if (!candidate.IsAlive || candidate.Team == team || ShouldIgnoreEnemyTarget(candidate))
            {
                continue;
            }

            var distanceSquared = DistanceSquared(player.X, player.Y, candidate.X, candidate.Y);
            if (distanceSquared > VisibleTargetSeekDistance * VisibleTargetSeekDistance)
            {
                continue;
            }

            var visible = HasCombatLineOfSight(world, player, candidate);
            if (!visible && distanceSquared > NearbyEnemyDistance * NearbyEnemyDistance)
            {
                continue;
            }

            var score = distanceSquared;
            if (!visible)
            {
                score += 250_000f;
            }

            if (candidate.IsCarryingIntel)
            {
                score -= 300_000f;
            }

            score -= (1f - GetHealthFraction(candidate)) * 20_000f;
            score += DistanceSquared(candidate.X, candidate.Y, destination.X, destination.Y) * 0.15f;

            if (role == BotRole.HuntCarrier && candidate.IsCarryingIntel)
            {
                score -= 200_000f;
            }

            if (role == BotRole.DefendObjective)
            {
                score -= DistanceSquared(candidate.X, candidate.Y, destination.X, destination.Y) * 0.2f;
            }

            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = candidate;
            }
        }

        memory.TargetPlayerId = bestTarget?.Id ?? -1;
        memory.TargetLockTicksRemaining = bestTarget is null ? 0 : timing.TargetLockTicks;
        return bestTarget;
    }

    private PlayerEntity? FindBestModernEnemyTarget(
        SimulationWorld world,
        PlayerEntity player,
        PlayerTeam team,
        IReadOnlyList<PlayerEntity> allPlayers,
        BotMemory memory,
        BotTimingProfile timing)
    {
        PlayerEntity? bestTarget = null;
        var bestDistance = ModernEnemySeeDistance;

        for (var index = 0; index < allPlayers.Count; index += 1)
        {
            var candidate = allPlayers[index];
            if (!candidate.IsAlive || candidate.Team == team || ShouldIgnoreModernEnemyTarget(candidate))
            {
                continue;
            }

            if (!HasModernObstacleLineOfSight(world, player.X, player.Y, candidate.X, candidate.Y))
            {
                continue;
            }

            var distance = DistanceBetween(player.X, player.Y, candidate.X, candidate.Y);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestTarget = candidate;
        }

        memory.TargetPlayerId = bestTarget?.Id ?? -1;
        memory.TargetLockTicksRemaining = bestTarget is null ? 0 : timing.TargetLockTicks;
        return bestTarget;
    }

    private ModernCombatTarget? FindBestModernCombatTarget(
        SimulationWorld world,
        PlayerEntity player,
        PlayerTeam team,
        IReadOnlyList<PlayerEntity> allPlayers)
    {
        ModernCombatTarget? bestTarget = null;
        var bestDistance = ModernEnemySeeDistance;

        for (var index = 0; index < world.Generators.Count; index += 1)
        {
            var candidate = world.Generators[index];
            if (candidate.Team == team || candidate.IsDestroyed)
            {
                continue;
            }

            var candidateX = candidate.Marker.CenterX;
            var candidateY = candidate.Marker.CenterY;
            if (!HasModernObstacleLineOfSight(world, player.X, player.Y, candidateX, candidateY))
            {
                continue;
            }

            var distance = DistanceBetween(player.X, player.Y, candidateX, candidateY);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestTarget = new ModernCombatTarget(ModernCombatTargetKind.Generator, candidate.Team, candidateX, candidateY, Generator: candidate);
        }

        for (var index = 0; index < allPlayers.Count; index += 1)
        {
            var candidate = allPlayers[index];
            if (!candidate.IsAlive || candidate.Team == team || ShouldIgnoreModernEnemyTarget(candidate))
            {
                continue;
            }

            if (!HasModernObstacleLineOfSight(world, player.X, player.Y, candidate.X, candidate.Y))
            {
                continue;
            }

            var distance = DistanceBetween(player.X, player.Y, candidate.X, candidate.Y);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestTarget = new ModernCombatTarget(ModernCombatTargetKind.Player, candidate.Team, candidate.X, candidate.Y, Player: candidate);
        }

        for (var index = 0; index < world.Sentries.Count; index += 1)
        {
            var candidate = world.Sentries[index];
            if (candidate.Team == team)
            {
                continue;
            }

            if (!HasModernObstacleLineOfSight(world, player.X, player.Y, candidate.X, candidate.Y))
            {
                continue;
            }

            var distance = DistanceBetween(player.X, player.Y, candidate.X, candidate.Y);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestTarget = new ModernCombatTarget(ModernCombatTargetKind.Sentry, candidate.Team, candidate.X, candidate.Y, Sentry: candidate);
        }

        return bestTarget;
    }

    private static PlayerEntity? FindBestHealTarget(
        SimulationWorld world,
        PlayerEntity player,
        PlayerTeam team,
        IReadOnlyList<PlayerEntity> allPlayers,
        BotMemory memory,
        BotTimingProfile timing,
        bool useModernBehavior)
    {
        if (player.ClassId != PlayerClass.Medic)
        {
            return null;
        }

        if (useModernBehavior)
        {
            return FindBestModernHealTarget(world, player, team, allPlayers, memory, timing);
        }

        var stickyTarget = TryResolveStickyTarget(allPlayers, team, memory.HealTargetPlayerId);
        if (stickyTarget is not null
            && stickyTarget.Team == team
            && stickyTarget.IsAlive
            && NeedsHealing(stickyTarget)
            && DistanceSquared(player.X, player.Y, stickyTarget.X, stickyTarget.Y) <= HealTargetSeekDistance * HealTargetSeekDistance)
        {
            memory.HealTargetLockTicksRemaining = Math.Max(memory.HealTargetLockTicksRemaining, timing.StickyTargetRefreshTicks);
            return stickyTarget;
        }

        PlayerEntity? bestTarget = null;
        var bestScore = float.MaxValue;

        for (var index = 0; index < allPlayers.Count; index += 1)
        {
            var candidate = allPlayers[index];
            if (!candidate.IsAlive
                || candidate.Team != team
                || ReferenceEquals(candidate, player))
            {
                continue;
            }

            if (!NeedsHealing(candidate))
            {
                continue;
            }

            var distanceSquared = DistanceSquared(player.X, player.Y, candidate.X, candidate.Y);
            if (distanceSquared > HealTargetSeekDistance * HealTargetSeekDistance)
            {
                continue;
            }

            var score = distanceSquared;
            score -= (1f - GetHealthFraction(candidate)) * 60_000f;
            if (candidate.IsCarryingIntel)
            {
                score -= 90_000f;
            }

            if (!HasCombatLineOfSight(world, player, candidate))
            {
                score += 40_000f;
            }

            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = candidate;
            }
        }

        memory.HealTargetPlayerId = bestTarget?.Id ?? -1;
        memory.HealTargetLockTicksRemaining = bestTarget is null ? 0 : timing.HealTargetLockTicks;
        return bestTarget;
    }

    private static PlayerEntity? FindBestModernHealTarget(
        SimulationWorld world,
        PlayerEntity player,
        PlayerTeam team,
        IReadOnlyList<PlayerEntity> allPlayers,
        BotMemory memory,
        BotTimingProfile timing)
    {
        PlayerEntity? bestTarget = null;
        var bestScore = float.PositiveInfinity;

        for (var index = 0; index < allPlayers.Count; index += 1)
        {
            var candidate = allPlayers[index];
            if (!candidate.IsAlive
                || candidate.Team != team
                || ReferenceEquals(candidate, player))
            {
                continue;
            }

            if (!HasModernObstacleLineOfSight(world, player.X, player.Y, candidate.X, candidate.Y))
            {
                continue;
            }

            var distance = DistanceBetween(player.X, player.Y, candidate.X, candidate.Y);
            var score = (distance / 1000f) + (GetHealthFraction(candidate) * 2f);
            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestTarget = candidate;
        }

        memory.HealTargetPlayerId = bestTarget?.Id ?? -1;
        memory.HealTargetLockTicksRemaining = bestTarget is null ? 0 : timing.HealTargetLockTicks;
        return bestTarget;
    }

    private static PlayerEntity? FindBestModernMedicBuddyTarget(
        PlayerEntity player,
        PlayerTeam team,
        IReadOnlyList<PlayerEntity> allPlayers)
    {
        if (player.ClassId != PlayerClass.Medic)
        {
            return null;
        }

        PlayerEntity? bestBuddyTarget = null;
        var bestDistance = float.PositiveInfinity;
        for (var index = 0; index < allPlayers.Count; index += 1)
        {
            var candidate = allPlayers[index];
            if (!candidate.IsAlive
                || candidate.Team != team
                || candidate.Id == player.Id
                || candidate.ClassId == PlayerClass.Soldier
                || (candidate.ClassId == PlayerClass.Spy && candidate.IsSpyCloaked))
            {
                continue;
            }

            var distance = DistanceBetween(player.X, player.Y, candidate.X, candidate.Y);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestBuddyTarget = candidate;
        }

        return bestBuddyTarget;
    }

    private static (float X, float Y) ResolveAimTarget(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination,
        ModernCombatTarget? combatTarget,
        PlayerEntity? healTarget,
        bool useModernBehavior)
    {
        if (useModernBehavior)
        {
            return ResolveModernAimTarget(world, player, combatTarget, healTarget);
        }

        if (healTarget is not null)
        {
            return (healTarget.X, useModernBehavior ? healTarget.Y : GetAimTargetY(healTarget));
        }

        if (combatTarget is ModernCombatTarget target)
        {
            var targetY = target.Kind == ModernCombatTargetKind.Player && target.Player is not null
                ? (useModernBehavior ? target.Player.Y : GetAimTargetY(target.Player))
                : target.Y;
            return (target.X, targetY);
        }

        return (destination.X, destination.Y - player.Height * AimChestOffsetFraction);
    }

    private static (float X, float Y) ResolveModernAimTarget(
        SimulationWorld world,
        PlayerEntity player,
        ModernCombatTarget? combatTarget,
        PlayerEntity? healTarget)
    {
        if (healTarget is not null)
        {
            return ApplyModernAimCompensation(player, healTarget.X, healTarget.Y);
        }

        if (combatTarget is ModernCombatTarget target)
        {
            var targetY = target.Kind == ModernCombatTargetKind.Player && target.Player is not null
                ? target.Player.Y
                : target.Y;
            return ApplyModernAimCompensation(player, target.X, targetY);
        }

        var gunspinDegrees = (float)((world.SimulationTimeSeconds * 360d) % 360d);
        return PointAtDirection(player.X, player.Y, gunspinDegrees, 96f);
    }

    private static (float X, float Y) ApplyModernAimCompensation(PlayerEntity player, float targetX, float targetY)
    {
        var deltaX = targetX - player.X;
        var deltaY = targetY - player.Y;
        var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (distance <= 0.0001f)
        {
            return (targetX, targetY);
        }

        var compensationDegrees = player.ClassId switch
        {
            PlayerClass.Scout or PlayerClass.Engineer or PlayerClass.Heavy or PlayerClass.Demoman or PlayerClass.Medic
                => 2f * MathF.Sqrt(MathF.Abs(deltaX) / 8f),
            PlayerClass.Spy => 0.8f * MathF.Sqrt(MathF.Abs(deltaX) / 8f),
            _ => 0f,
        };

        if (compensationDegrees <= 0f)
        {
            return (targetX, targetY);
        }

        var directionDegrees = RadiansToDegrees(MathF.Atan2(deltaY, deltaX));
        if (directionDegrees < 0f)
        {
            directionDegrees += 360f;
        }

        directionDegrees = directionDegrees > 90f && directionDegrees < 270f
            ? directionDegrees - compensationDegrees
            : directionDegrees + compensationDegrees;
        return PointAtDirection(player.X, player.Y, directionDegrees, distance);
    }

    private static float RadiansToDegrees(float radians)
    {
        return radians * (180f / MathF.PI);
    }

    private static (float X, float Y) PointAtDirection(float originX, float originY, float directionDegrees, float distance)
    {
        var radians = directionDegrees * (MathF.PI / 180f);
        return (
            originX + (MathF.Cos(radians) * distance),
            originY + (MathF.Sin(radians) * distance));
    }

    private int ResolveHorizontalMovement(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination,
        ModernPathSelection objectiveSelection,
        PlayerEntity? enemyTarget,
        PlayerEntity? healTarget,
        bool hasVisibleEnemy,
        NavigationDecision navigationDecision,
        BotNavigationRuntimeGraph? navigationGraph,
        ClientBotNavPoints? clientBotNavPoints,
        BotMemory memory,
        BotTimingProfile timing)
    {
        if (memory.UnstickTicks > 0)
        {
            return memory.UnstickDirection;
        }

        if (clientBotNavPoints is not null)
        {
            return ResolveModernHorizontalMovement(
                world,
                player,
                destination,
                objectiveSelection,
                navigationDecision,
                clientBotNavPoints,
                memory,
                enemyTarget);
        }

        if (!navigationDecision.LocksMovement && player.ClassId == PlayerClass.Medic && healTarget is not null)
        {
            var healDistance = DistanceBetween(player.X, player.Y, healTarget.X, healTarget.Y);
            if (healDistance > 120f)
            {
                return GetMoveDirection(healTarget.X - player.X);
            }
        }

        if (!navigationDecision.LocksMovement && enemyTarget is not null && hasVisibleEnemy)
        {
            return ResolveCombatMovement(player, enemyTarget, memory, timing);
        }

        if (navigationDecision.ForcedHorizontalDirection != 0)
        {
            return navigationDecision.ForcedHorizontalDirection;
        }

        if (navigationDecision.LocksMovement)
        {
            return GetMoveDirection(destination.X - player.X, RouteMovementDeadZone);
        }

        if (navigationDecision.HasRoute)
        {
            return GetMoveDirection(destination.X - player.X, RouteMovementDeadZone);
        }

        if (DistanceSquared(player.X, player.Y, destination.X, destination.Y) <= ObjectiveArrivalDistance * ObjectiveArrivalDistance)
        {
            return ResolvePatrolMovement(memory, destination.X - player.X, timing);
        }

        return GetMoveDirection(destination.X - player.X);
    }

    private static int ResolveModernHorizontalMovement(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination,
        ModernPathSelection objectiveSelection,
        NavigationDecision navigationDecision,
        ClientBotNavPoints navPoints,
        BotMemory memory,
        PlayerEntity? enemyTarget)
    {
        memory.ModernMoveDebug = "modern:start";
        var hasGroundContact = HasModernGroundContact(world, player);
        if (objectiveSelection.IsCaptureObjective
            && objectiveSelection.CaptureObjective is ModernCaptureObjective captureObjective)
        {
            var enemyNearbyCapture = HasModernCaptureEnemyNearby(world, player, enemyTarget);
            var captureTargetPoint = ResolveModernCaptureSettleTarget(captureObjective);
            var captureDx = MathF.Abs(player.X - captureTargetPoint.X);
            var captureDy = MathF.Abs(player.Bottom - captureTargetPoint.Y);
            if (hasGroundContact
                && captureDx < ModernCaptureBrakeTargetDistanceX
                && captureDy < ModernCaptureBrakeTargetDistanceY
                && !enemyNearbyCapture)
            {
                var captureHorizontalSpeed = player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond;
                var velocitySign = MathF.Sign(captureHorizontalSpeed);
                var targetSign = MathF.Sign(player.X - captureTargetPoint.X);
                if (MathF.Abs(captureHorizontalSpeed) > 1.1f
                    && velocitySign != 0f
                    && velocitySign == targetSign)
                {
                    memory.DoublebackActive = false;
                    memory.ModernMoveDebug = "capture_brake";
                    return -(int)velocitySign;
                }
                memory.DoublebackActive = false;
            }
        }

        var targetY = destination.Y;
        var feetY = player.Bottom;
        var horizontalSpeed = player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond;
        var xTolerance = objectiveSelection.IsCaptureObjective ? 12f : 20f;
        if (player.X - xTolerance > destination.X
            || (player.X > destination.X
                && (MathF.Abs(horizontalSpeed) < 0.05f || (feetY - 1f) > targetY)))
        {
            if (horizontalSpeed > 1f && (feetY - 3f) >= targetY)
            {
                memory.DoublebackActive = true;
                memory.ModernMoveDebug = "left_doubleback";
                return 0;
            }

            if (memory.DoublebackActive)
            {
                if (hasGroundContact)
                {
                    memory.DoublebackActive = false;
                }
                else
                {
                    memory.ModernMoveDebug = "left_doubleback_wait";
                    return 0;
                }
            }

            memory.ModernMoveDebug = "left";
            return -1;
        }

        if (player.X + xTolerance < destination.X
            || (player.X < destination.X
                && (MathF.Abs(horizontalSpeed) < 0.05f || (feetY - 1f) > targetY)))
        {
            if (horizontalSpeed < -1f && (feetY - 3f) >= targetY)
            {
                memory.DoublebackActive = true;
                memory.ModernMoveDebug = "right_doubleback";
                return 0;
            }

            if (memory.DoublebackActive)
            {
                if (hasGroundContact)
                {
                    memory.DoublebackActive = false;
                }
                else
                {
                    memory.ModernMoveDebug = "right_doubleback_wait";
                    return 0;
                }
            }

            memory.ModernMoveDebug = "right";
            return 1;
        }

        memory.DoublebackActive = false;
        if (TryGetModernNextNode(navPoints, memory, out var nextNode))
        {
            var movementDirection = 0;
            if (player.HorizontalSpeed > 0f && player.X < nextNode.X)
            {
                movementDirection = 1;
                memory.ModernMoveDebug = "reached_momentum_right";
            }
            else if (player.HorizontalSpeed < 0f && player.X > nextNode.X)
            {
                movementDirection = -1;
                memory.ModernMoveDebug = "reached_momentum_left";
            }
            else
            {
                memory.ModernMoveDebug = "reached_stop";
            }

            memory.SecondLastCommittedPointId = memory.LastCommittedPointId;
            memory.LastCommittedPointId = memory.CurrentPointId;
            memory.CurrentPointId = memory.NextPointId;
            return movementDirection;
        }

        if (!navigationDecision.CaptureHoldActive)
        {
            memory.CurrentPointId = -1;
        }

        memory.ModernMoveDebug = "no_next_stop";
        return 0;
    }

    private int ResolveCombatMovement(PlayerEntity player, PlayerEntity enemyTarget, BotMemory memory, BotTimingProfile timing)
    {
        var preferredRange = GetPreferredCombatRange(player.ClassId);
        var distance = DistanceBetween(player.X, player.Y, enemyTarget.X, enemyTarget.Y);
        if (distance < preferredRange.Min)
        {
            return GetMoveDirection(player.X - enemyTarget.X);
        }

        if (distance > preferredRange.Max)
        {
            return GetMoveDirection(enemyTarget.X - player.X);
        }

        if (memory.StrafeTicksRemaining <= 0)
        {
            memory.StrafeDirection = GetRandomDirection();
            memory.StrafeTicksRemaining = GetRandomTicksInRange(timing.StrafeTicksMin, timing.StrafeTicksMax);
        }

        memory.StrafeTicksRemaining -= 1;
        return memory.StrafeDirection;
    }

    private int ResolvePatrolMovement(BotMemory memory, float destinationOffsetX, BotTimingProfile timing)
    {
        if (MathF.Abs(destinationOffsetX) > PatrolDistance)
        {
            return GetMoveDirection(destinationOffsetX);
        }

        if (memory.StrafeTicksRemaining <= 0)
        {
            memory.StrafeDirection = GetRandomDirection();
            memory.StrafeTicksRemaining = GetRandomTicksInRange(timing.StrafeTicksMin, timing.StrafeTicksMax);
        }

        memory.StrafeTicksRemaining -= 1;
        return memory.StrafeDirection;
    }

    private static bool ResolveJump(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination,
        PlayerEntity? enemyTarget,
        PlayerEntity? healTarget,
        int horizontal,
        bool hasVisibleEnemy,
        NavigationDecision navigationDecision,
        BotNavigationRuntimeGraph? navigationGraph,
        ClientBotNavPoints? clientBotNavPoints,
        BotMemory memory,
        BotTimingProfile timing)
    {
        memory.ModernJumpDebug = "start";
        if (clientBotNavPoints is not null)
        {
            return ResolveModernJump(
                world,
                player,
                destination,
                horizontal,
                navigationDecision,
                clientBotNavPoints,
                memory,
                timing);
        }

        if (navigationDecision.ForceJump)
        {
            memory.ModernJumpDebug = "force";
            memory.JumpCooldownTicks = timing.JumpCooldownTicks;
            return true;
        }

        if (memory.JumpCooldownTicks > 0)
        {
            memory.ModernJumpDebug = $"cooldown:{memory.JumpCooldownTicks}:kind{navigationDecision.TraversalKind}:t{navigationDecision.UsesTraversalTape}";
            return false;
        }

        if (memory.UnstickTicks > 0 && player.IsGrounded)
        {
            memory.ModernJumpDebug = "unstick";
            memory.JumpCooldownTicks = timing.JumpCooldownTicks;
            return true;
        }

        if (navigationDecision.TraversalKind == BotNavigationTraversalKind.Jump
            && !navigationDecision.UsesTraversalTape
            && player.IsGrounded)
        {
            memory.ModernJumpDebug = "route_jump";
            memory.JumpCooldownTicks = timing.JumpCooldownTicks;
            return true;
        }

        if (navigationDecision.LocksMovement)
        {
            memory.ModernJumpDebug = $"locked:kind{navigationDecision.TraversalKind}:t{navigationDecision.UsesTraversalTape}";
            return false;
        }

        var movementTarget = healTarget is not null && player.ClassId == PlayerClass.Medic
            ? (healTarget.X, healTarget.Y)
            : enemyTarget is not null && hasVisibleEnemy
                ? (enemyTarget.X, enemyTarget.Y)
                : destination;

        var isPureRouteMovement = navigationDecision.HasRoute
            && healTarget is null
            && (!hasVisibleEnemy || enemyTarget is null);
        if (movementTarget.Y < player.Y - 24f
            && player.IsGrounded
            && (!isPureRouteMovement
                || clientBotNavPoints is not null
                || navigationDecision.TraversalKind == BotNavigationTraversalKind.Jump))
        {
            memory.ModernJumpDebug = "rise_jump";
            memory.JumpCooldownTicks = timing.JumpCooldownTicks;
            return true;
        }

        if (horizontal != 0 && WouldMoveIntoObstacle(world, player, horizontal))
        {
            memory.ModernJumpDebug = "obstacle_jump";
            memory.JumpCooldownTicks = timing.JumpCooldownTicks;
            return true;
        }

        if (navigationDecision.ForcedHorizontalDirection != 0
            && !player.IsGrounded
            && MathF.Abs(destination.Y - player.Y) > 28f)
        {
            memory.ModernJumpDebug = "airborne_commit";
            return false;
        }

        memory.ModernJumpDebug = $"nojump:kind{navigationDecision.TraversalKind}:t{navigationDecision.UsesTraversalTape}:lock{navigationDecision.LocksMovement}:g{player.IsGrounded}:h{horizontal}";
        return false;
    }

    private bool ResolvePrimaryFire(
        SimulationWorld world,
        PlayerEntity player,
        ModernCombatTarget? combatTarget,
        PlayerEntity? healTarget,
        BotMemory memory,
        bool useModernBehavior,
        bool isBeingHealed)
    {
        if (useModernBehavior)
        {
            return ResolveModernPrimaryFire(player, combatTarget, healTarget, memory, isBeingHealed);
        }

        if (player.ClassId == PlayerClass.Medic && healTarget is not null)
        {
            if (!NeedsHealing(healTarget))
            {
                return false;
            }

            return HasCombatLineOfSight(world, player, healTarget)
                && DistanceBetween(player.X, player.Y, healTarget.X, healTarget.Y) <= 220f;
        }

        if (combatTarget is not ModernCombatTarget target)
        {
            return false;
        }

        if (!HasCombatTargetLineOfSight(world, player, target, useModernBehavior))
        {
            return false;
        }

        var distance = DistanceBetween(player.X, player.Y, target.X, target.Y);
        if (target.Kind == ModernCombatTargetKind.Player
            && player.ClassId == PlayerClass.Spy
            && player.IsSpyCloaked)
        {
            return distance <= 64f;
        }

        if (player.ClassId == PlayerClass.Sniper && !player.IsSniperScoped && distance >= 320f)
        {
            return false;
        }

        return player.PrimaryWeapon.Kind switch
        {
            PrimaryWeaponKind.PelletGun => distance <= 280f,
            PrimaryWeaponKind.Minigun => distance <= 340f,
            PrimaryWeaponKind.Rifle => distance <= 900f,
            PrimaryWeaponKind.Revolver => distance <= 480f,
            PrimaryWeaponKind.FlameThrower => distance <= 150f,
            PrimaryWeaponKind.RocketLauncher => distance >= 120f && distance <= 520f,
            PrimaryWeaponKind.MineLauncher => distance >= 90f && distance <= 260f,
            PrimaryWeaponKind.Blade => distance <= 64f,
            PrimaryWeaponKind.Medigun => false,
            _ => false,
        };
    }

    private bool ResolveSecondaryFire(
        SimulationWorld world,
        PlayerEntity player,
        ModernCombatTarget? combatTarget,
        PlayerEntity? healTarget,
        IReadOnlyList<PlayerEntity> allPlayers,
        BotMemory memory,
        bool hasVisibleEnemy,
        bool useModernBehavior,
        bool isBeingHealed)
    {
        if (useModernBehavior)
        {
            return ResolveModernSecondaryFire(world, player, combatTarget, healTarget, allPlayers, memory, isBeingHealed);
        }

        switch (player.ClassId)
        {
            case PlayerClass.Medic:
                if (healTarget is not null)
                {
                    return player.IsMedicUberReady
                        && DistanceBetween(player.X, player.Y, healTarget.X, healTarget.Y) <= 200f
                        && (healTarget.Health < healTarget.MaxHealth / 2
                            || GetHealthFraction(player) < LowHealthRetreatFraction
                            || (combatTarget is not null
                                && hasVisibleEnemy
                                && DistanceBetween(player.X, player.Y, combatTarget.Value.X, combatTarget.Value.Y) <= 220f));
                }

                return combatTarget is not null
                    && hasVisibleEnemy
                    && DistanceBetween(player.X, player.Y, combatTarget.Value.X, combatTarget.Value.Y) <= 420f;

            case PlayerClass.Sniper:
                if (combatTarget is null)
                {
                    return player.IsSniperScoped;
                }

                var sniperDistance = DistanceBetween(player.X, player.Y, combatTarget.Value.X, combatTarget.Value.Y);
                return !player.IsSniperScoped
                    ? hasVisibleEnemy && sniperDistance >= 320f
                    : !hasVisibleEnemy || sniperDistance < 180f;

            case PlayerClass.Spy:
                if (player.IsCarryingIntel)
                {
                    return false;
                }

                if (player.IsSpyCloaked)
                {
                    return combatTarget is not null
                        && hasVisibleEnemy
                        && DistanceBetween(player.X, player.Y, combatTarget.Value.X, combatTarget.Value.Y) > 96f
                        && !player.IsSpyBackstabAnimating;
                }

                return combatTarget is null || !hasVisibleEnemy;

            case PlayerClass.Quote:
                return combatTarget is not null
                    && hasVisibleEnemy
                    && DistanceBetween(player.X, player.Y, combatTarget.Value.X, combatTarget.Value.Y) is > 80f and <= 220f;

            default:
                return false;
        }
    }

    private bool ResolveModernPrimaryFire(
        PlayerEntity player,
        ModernCombatTarget? combatTarget,
        PlayerEntity? healTarget,
        BotMemory memory,
        bool isBeingHealed)
    {
        if (player.ClassId == PlayerClass.Medic)
        {
            memory.ModernBeenHealingTicks += 1;
            var firePrimary = true;
            if (healTarget is not null
                && player.MedicHealTargetId.HasValue
                && player.MedicHealTargetId.Value != healTarget.Id
                && memory.ModernBeenHealingTicks > memory.ModernBeenHealingSwitchTicks)
            {
                memory.ModernBeenHealingTicks = 0;
                firePrimary = false;
            }

            if (healTarget is null && combatTarget is not null)
            {
                firePrimary = false;
            }

            return firePrimary;
        }

        memory.ModernBeenHealingTicks = 0;
        if (combatTarget is null)
        {
            return false;
        }

        if (player.ClassId == PlayerClass.Heavy && ShouldModernHeavyEat(player, isBeingHealed, ModernHeavyCombatEatHealth))
        {
            return false;
        }

        if (player.ClassId == PlayerClass.Sniper && player.IsSniperScoped)
        {
            if (player.SniperChargeTicks >= memory.ModernZoomToShootTicks)
            {
                memory.ModernZoomToShootTicks = NextModernZoomToShootTicks(memory.ModernCombatTicksPerSecond);
                return true;
            }

            return false;
        }

        if (player.ClassId == PlayerClass.Demoman)
        {
            return true;
        }

        return true;
    }

    private bool ResolveModernSecondaryFire(
        SimulationWorld world,
        PlayerEntity player,
        ModernCombatTarget? combatTarget,
        PlayerEntity? healTarget,
        IReadOnlyList<PlayerEntity> allPlayers,
        BotMemory memory,
        bool isBeingHealed)
    {
        if (player.ClassId == PlayerClass.Medic)
        {
            if (healTarget is not null)
            {
                return player.IsMedicUberReady
                    && (healTarget.Health < 50
                        || player.Health < 40);
            }

            return combatTarget is not null;
        }

        if (player.ClassId == PlayerClass.Heavy)
        {
            if (combatTarget is null)
            {
                return ShouldModernHeavyEat(player, isBeingHealed, ModernHeavyIdleEatHealth);
            }

            return ShouldModernHeavyEat(player, isBeingHealed, ModernHeavyCombatEatHealth);
        }

        if (player.ClassId == PlayerClass.Sniper)
        {
            if (combatTarget is null)
            {
                return player.IsSniperScoped;
            }

            var distance = DistanceBetween(player.X, player.Y, combatTarget.Value.X, combatTarget.Value.Y);
            if (distance >= ModernSniperDangerDistance)
            {
                return !player.IsSniperScoped;
            }

            if (player.IsSniperScoped)
            {
                memory.ModernZoomToShootTicks = NextModernZoomToShootTicks(memory.ModernCombatTicksPerSecond);
                return true;
            }

            return false;
        }

        if (player.ClassId == PlayerClass.Demoman)
        {
            return ShouldModernDetonateMines(world, player, allPlayers);
        }

        return false;
    }

    internal static bool ResolveSecondaryWeaponFireFromLoadout(
        PlayerEntity player,
        float? combatTargetX,
        float? combatTargetY,
        PlayerEntity? healTarget,
        bool firePrimary,
        bool fireSecondary)
    {
        if (player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.MedicUber))
        {
            return player.IsMedicUberReady
                && (player.Health < 40 || healTarget is { Health: < 50 });
        }

        if (player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.PyroUtility))
        {
            return combatTargetX.HasValue
                && combatTargetY.HasValue
                && player.CanFirePyroAirblast()
                && DistanceBetween(player.X, player.Y, combatTargetX.Value, combatTargetY.Value) <= ModernCloseCombatDistance;
        }

        if (player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.SoldierSecondaryWeapon))
        {
            return combatTargetX.HasValue
                && combatTargetY.HasValue
                && !fireSecondary
                && (!firePrimary || player.CurrentShells <= 0 || player.PrimaryCooldownTicks > 0)
                && player.ExperimentalOffhandCurrentShells > 0
                && DistanceBetween(player.X, player.Y, combatTargetX.Value, combatTargetY.Value) <= ModernSoldierShotgunDistance;
        }

        return false;
    }

    private bool ShouldIgnoreModernEnemyTarget(PlayerEntity player)
    {
        if (player.ClassId != PlayerClass.Spy)
        {
            return false;
        }

        if (!_modernSpyVisibleTicksByPlayerId.TryGetValue(player.Id, out var visibleTicks))
        {
            return true;
        }

        return visibleTicks < _modernSpyReactTicksThreshold;
    }

    private void UpdateModernSpyVisibilityMemory(List<PlayerEntity> allPlayers)
    {
        _activeSpyIdsBuffer.Clear();
        for (var index = 0; index < allPlayers.Count; index += 1)
        {
            var player = allPlayers[index];
            if (player.ClassId != PlayerClass.Spy)
            {
                continue;
            }

            _activeSpyIdsBuffer.Add(player.Id);
            _modernSpyVisibleTicksByPlayerId.TryGetValue(player.Id, out var visibleTicks);
            var isVisibleToBots = player.SpyCloakAlpha > 0.2f || player.IsSpyBackstabAnimating || !player.IsSpyCloaked;
            visibleTicks = isVisibleToBots
                ? Math.Min(90, visibleTicks + 1)
                : Math.Max(0, visibleTicks - 1);
            _modernSpyVisibleTicksByPlayerId[player.Id] = visibleTicks;
        }

        _staleSpyIdsBuffer.Clear();
        foreach (var entry in _modernSpyVisibleTicksByPlayerId)
        {
            if (!_activeSpyIdsBuffer.Contains(entry.Key))
            {
                _staleSpyIdsBuffer.Add(entry.Key);
            }
        }

        for (var index = 0; index < _staleSpyIdsBuffer.Count; index += 1)
        {
            _modernSpyVisibleTicksByPlayerId.Remove(_staleSpyIdsBuffer[index]);
        }
    }

    private static bool IsPlayerBeingHealed(IReadOnlyList<PlayerEntity> allPlayers, PlayerEntity player)
    {
        for (var index = 0; index < allPlayers.Count; index += 1)
        {
            var candidate = allPlayers[index];
            if (!candidate.IsAlive
                || candidate.Team != player.Team
                || candidate.ClassId != PlayerClass.Medic
                || !candidate.IsMedicHealing
                || candidate.MedicHealTargetId != player.Id)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool ShouldModernHeavyEat(PlayerEntity player, bool isBeingHealed, int healthThreshold)
    {
        return player.ClassId == PlayerClass.Heavy
            && !isBeingHealed
            && !player.IsHeavyEating
            && player.HeavyEatCooldownTicksRemaining <= 0
            && player.Health <= healthThreshold;
    }

    private bool ShouldModernDetonateMines(
        SimulationWorld world,
        PlayerEntity player,
        IReadOnlyList<PlayerEntity> allPlayers)
    {
        var ownedMineCount = 0;
        for (var mineIndex = 0; mineIndex < world.Mines.Count; mineIndex += 1)
        {
            var mine = world.Mines[mineIndex];
            if (mine.OwnerId != player.Id || mine.IsDestroyed)
            {
                continue;
            }

            ownedMineCount += 1;
            if (DistanceBetween(player.X, player.Y, mine.X, mine.Y) >= ModernMineThreatDistance)
            {
                continue;
            }

            for (var playerIndex = 0; playerIndex < allPlayers.Count; playerIndex += 1)
            {
                var candidate = allPlayers[playerIndex];
                if (!candidate.IsAlive || candidate.Id == player.Id)
                {
                    continue;
                }

                if (DistanceBetween(candidate.X, candidate.Y, mine.X, mine.Y) <= ModernMineDetonationRadius)
                {
                    return true;
                }
            }
        }

        if (ownedMineCount >= player.PrimaryWeapon.MaxAmmo)
        {
            var roll = _random.Next(0, 101);
            return roll is >= 70 and <= 71;
        }

        return false;
    }

    private static void ApplyModernReloadDiscipline(
        PlayerEntity player,
        BotMemory memory,
        ref bool firePrimary,
        ref bool fireSecondary,
        ref bool fireSecondaryWeapon)
    {
        if (memory.ModernReloadCounterTicks <= 0)
        {
            return;
        }

        if (player.ClassId == PlayerClass.Medic)
        {
            fireSecondary = false;
            fireSecondaryWeapon = false;
            return;
        }

        firePrimary = false;
    }

    private static void ApplyModernSoldierCloseRangeAdjustment(
        SimulationWorld world,
        PlayerEntity player,
        ModernCombatTarget? combatTarget,
        BotMemory memory,
        bool firePrimary,
        BotTimingProfile timing,
        ref int horizontal,
        ref bool jump)
    {
        if (player.ClassId != PlayerClass.Soldier
            || combatTarget is not ModernCombatTarget target
            || !firePrimary)
        {
            return;
        }

        var closeReloadThreshold = ScaleBotTicks(ModernSoldierCloseReloadSourceTicks, timing.TicksPerSecond);
        if (memory.ModernReloadCounterTicks >= closeReloadThreshold
            || player.PrimaryCooldownTicks >= closeReloadThreshold)
        {
            return;
        }

        var enemyDistance = DistanceBetween(player.X, player.Y, target.X, target.Y);
        if (enemyDistance >= 30f)
        {
            return;
        }

        jump = true;
        horizontal = player.X > target.X ? -1 : 1;
    }

    private static void UpdateModernCombatMemory(PlayerEntity player, BotMemory memory)
    {
        if (memory.ModernReloadCounterTicks > 0)
        {
            memory.ModernReloadCounterTicks -= 1;
            if (player.CurrentShells >= player.PrimaryWeapon.MaxAmmo)
            {
                memory.ModernReloadCounterTicks = 0;
            }

            return;
        }

        if (player.PrimaryWeapon.Kind == PrimaryWeaponKind.Rifle)
        {
            return;
        }

        if (player.CurrentShells <= 0)
        {
            memory.ModernReloadCounterTicks = (3 * player.PrimaryWeapon.ReloadDelayTicks) + player.PrimaryWeapon.AmmoReloadTicks;
            return;
        }

        if (player.PrimaryWeapon.Kind == PrimaryWeaponKind.FlameThrower && player.CurrentShells < 3)
        {
            memory.ModernReloadCounterTicks = 4 * player.PrimaryWeapon.AmmoReloadTicks;
            return;
        }

        if (player.PrimaryWeapon.Kind == PrimaryWeaponKind.Minigun && player.CurrentShells < 3)
        {
            memory.ModernReloadCounterTicks = 6 * player.PrimaryWeapon.AmmoReloadTicks;
        }
    }

    private static void EnsureModernCombatMemoryInitialized(BotMemory memory, BotTimingProfile timing)
    {
        if (memory.ModernBeenHealingSwitchTicks <= 0)
        {
            memory.ModernBeenHealingSwitchTicks = ScaleBotTicks(ModernBeenHealingSwitchSourceTicks, timing.TicksPerSecond);
        }

        if (memory.ModernZoomToShootTicks <= 0)
        {
            memory.ModernZoomToShootTicks = ScaleBotTicks(ModernZoomToShootMinSourceTicks, timing.TicksPerSecond);
        }

        memory.ModernCombatTicksPerSecond = timing.TicksPerSecond;
    }

    private int NextModernZoomToShootTicks(int ticksPerSecond)
    {
        var minimumTicks = ScaleBotTicks(ModernZoomToShootMinSourceTicks, ticksPerSecond);
        var maximumTicks = ScaleBotTicks(ModernZoomToShootMaxSourceTicks, ticksPerSecond);
        return _random.Next(minimumTicks, maximumTicks + 1);
    }

    private static bool ResolveBuildSentry(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination)
    {
        if (player.ClassId != PlayerClass.Engineer
            || player.IsCarryingIntel
            || player.Metal < 100f
            || !player.IsGrounded
            || HasOwnedSentry(world, player.Id))
        {
            return false;
        }

        var objectiveDistance = DistanceBetween(player.X, player.Y, destination.X, destination.Y);
        return objectiveDistance < 60f;
    }

    private static bool ResolveDropIntel(PlayerEntity player, PlayerEntity? medicBuddyTarget)
    {
        return player.ClassId == PlayerClass.Medic
            && player.IsCarryingIntel
            && medicBuddyTarget is not null
            && DistanceBetween(player.X, player.Y, medicBuddyTarget.X, medicBuddyTarget.Y) < 60f;
    }

    private static bool HasOwnedSentry(SimulationWorld world, int ownerPlayerId)
    {
        for (var index = 0; index < world.Sentries.Count; index += 1)
        {
            if (world.Sentries[index].OwnerPlayerId == ownerPlayerId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ResolveModernJump(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination,
        int horizontal,
        NavigationDecision navigationDecision,
        ClientBotNavPoints navPoints,
        BotMemory memory,
        BotTimingProfile timing)
    {
        memory.ModernJumpDebug = "start";
        if (navigationDecision.ForceJump)
        {
            memory.ModernJumpDebug = "force";
            return TriggerModernBotJump(memory, timing);
        }

        if (memory.JumpCooldownTicks > 0)
        {
            memory.ModernJumpDebug = $"cooldown:{memory.JumpCooldownTicks}";
            return false;
        }

        var hasGroundContact = HasModernGroundContact(world, player);
        if (memory.UnstickTicks > 0 && hasGroundContact)
        {
            memory.ModernJumpDebug = "unstick";
            return TriggerModernBotJump(memory, timing);
        }

        GetModernClassJumpProfile(player.ClassId, out var jumpRange, out var jumpHeight, out var jumpHeightTotal);

        var feetY = player.Bottom;
        var targetY = destination.Y;
        var horizontalSpeed = player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond;
        var modernStuckTicks = Math.Max(memory.ModernStuckTicks, memory.StuckTicks);
        var previousX = memory.PreviousObservedX;
        var previousY = memory.PreviousObservedY;
        var hasPreviousPosition = memory.HasObservedPosition;

        if (hasGroundContact)
        {
            if (horizontal != 0
                && modernStuckTicks > 6
                && hasPreviousPosition
                && DistanceSquared(player.X, player.Y, previousX, previousY) <= 0.04f
                && MathF.Abs(horizontalSpeed) > 0.6f
                && HasModernJumpHeadClear(world, player, jumpHeight)
                && HasModernForwardFootBlock(world, player, horizontal, 7f))
            {
                memory.ModernJumpDebug = "stuck_pulse";
                return TriggerModernBotJump(memory, timing);
            }

            if (horizontal != 0
                && ShouldDelayModernRouteJumpLaunch(
                    world.Level,
                    player,
                    destination.X,
                    targetY,
                    horizontal,
                    hasGroundContact,
                    jumpRange,
                    jumpHeightTotal,
                    out var jumpDelayReason))
            {
                memory.ModernJumpDebug = jumpDelayReason;
                return false;
            }

            if (horizontal != 0
                && TryGetCurrentModernNode(navPoints, memory, out var currentNode)
                && TryGetModernNextNode(navPoints, memory, out var nextNode)
                && TryShouldUseModernInterpolationJump(
                    world,
                    player,
                    currentNode.X,
                    GetModernNodeFeetY(navPoints, currentNode),
                    nextNode.X,
                    GetModernNodeFeetY(navPoints, nextNode),
                    previousX,
                    previousY,
                    jumpHeight))
            {
                memory.ModernJumpDebug = "interp";
                return TriggerModernBotJump(memory, timing);
            }

            if (horizontal != 0
                && TryShouldUseModernLipJump(world, player, destination, targetY, horizontal, modernStuckTicks, jumpHeight))
            {
                memory.ModernJumpDebug = "lip";
                return TriggerModernBotJump(memory, timing);
            }

            if (horizontal != 0
                && TryShouldUseModernSlopeJump(world, player, destination, targetY, horizontal, jumpRange, jumpHeight, jumpHeightTotal))
            {
                memory.ModernJumpDebug = "slope";
                return TriggerModernBotJump(memory, timing);
            }

            if (horizontal != 0
                && TryShouldUseModernHallJump(world, player, destination, targetY, horizontal, jumpRange, jumpHeight))
            {
                memory.ModernJumpDebug = "hall";
                return TriggerModernBotJump(memory, timing);
            }

            if (horizontal != 0 && HasModernWaistObstacleAhead(world, player, horizontal))
            {
                if (MathF.Sign(horizontalSpeed) == 0f || MathF.Abs(MathF.Sign(horizontal) - MathF.Sign(horizontalSpeed)) < 2f)
                {
                    if (IsModernStairStepAhead(world, player, horizontal, horizontalSpeed))
                    {
                        memory.ModernJumpDebug = "waist";
                        return TriggerModernBotJump(memory, timing);
                    }
                }
            }

            if (horizontal != 0 && !HasModernGroundAhead(world, player, horizontal, 15f))
            {
                var jumpDistanceNeeded = MathF.Abs(destination.X - player.X);
                if (targetY < player.Y)
                {
                    var jumpRiseNeeded = feetY - targetY;
                    if (jumpRiseNeeded <= jumpHeightTotal
                        && jumpDistanceNeeded <= jumpRange
                        && MathF.Sign(destination.X - player.X) == horizontal
                        && (MathF.Abs(horizontalSpeed) <= 0.25f || MathF.Sign(horizontalSpeed) == horizontal))
                    {
                        memory.ModernJumpDebug = "hole_up";
                        return TriggerModernBotJump(memory, timing);
                    }
                }
                else if (MathF.Abs(targetY - feetY) <= 18f
                    && !LineHitsSolid(world, player, player.X + horizontalSpeed + (2f * horizontal), feetY, destination.X - (3f * horizontal), targetY)
                    && jumpDistanceNeeded <= jumpRange
                    && (MathF.Abs(horizontalSpeed) <= 0.25f || MathF.Sign(horizontalSpeed) == horizontal))
                {
                    memory.ModernJumpDebug = "hole_flat";
                    return TriggerModernBotJump(memory, timing);
                }
            }

            var jumpLength = horizontalSpeed * player.JumpStrength;
        if (LineHitsSolid(world, player, player.X + jumpLength, feetY - 18f, player.X + jumpLength, feetY - (jumpHeight + 4f))
            && !PointHitsSolid(world, player, player.X + jumpLength, feetY - (jumpHeight + 12f))
                && targetY < player.Y
                && horizontal != 0
                && MathF.Abs(MathF.Sign(horizontal) - MathF.Sign(horizontalSpeed)) < 2f
                && IsModernStairStepAhead(world, player, horizontal, horizontalSpeed))
            {
                memory.ModernJumpDebug = "platform";
                return TriggerModernBotJump(memory, timing);
            }
        }

        if (player.ClassId == PlayerClass.Scout
            && !hasGroundContact
            && targetY < player.Y)
        {
            var jumpDistanceNeeded = MathF.Abs(destination.X - player.X);
            var jumpRiseNeeded = feetY - targetY;
            if (jumpRiseNeeded <= jumpHeightTotal
                && jumpDistanceNeeded <= jumpRange
                && horizontal != 0
                && MathF.Sign(destination.X - player.X) == horizontal)
            {
                memory.ModernJumpDebug = "scout_air";
                return TriggerModernBotJump(memory, timing);
            }
        }

        memory.ModernJumpDebug = $"nojump:g{hasGroundContact}:h{horizontal}:dx{destination.X - player.X:0}:rise{feetY - targetY:0}:hs{horizontalSpeed:0.0}:st{modernStuckTicks}";
        return false;
    }

    private static bool HasModernQueuedLowerDropContinuation(ClientBotNavPoints navPoints, BotMemory memory, float feetY)
    {
        if (!TryGetModernNextNode(navPoints, memory, out var nextNode)
            || !TryGetModernSecondNextNode(navPoints, memory, out var secondNextNode))
        {
            return false;
        }

        var nextFeetY = GetModernNodeFeetY(navPoints, nextNode);
        var secondFeetY = GetModernNodeFeetY(navPoints, secondNextNode);
        return MathF.Abs(nextFeetY - feetY) <= 24f
            && secondFeetY > nextFeetY + RouteWalkApproximateArrivalHeight;
    }

    private static bool IsMovingTowardModernTarget(
        PlayerEntity player,
        (float X, float Y) destination,
        int horizontal,
        float horizontalSpeed)
    {
        return MathF.Sign(destination.X - player.X) == horizontal
            && (MathF.Abs(horizontalSpeed) < 0.3f || MathF.Sign(horizontalSpeed) == horizontal);
    }

    private static bool TriggerModernBotJump(BotMemory memory, BotTimingProfile timing)
    {
        memory.JumpCooldownTicks = ScaleBotTicks(ModernJumpCooldownSourceTicks, timing.TicksPerSecond);
        return true;
    }

    private static bool ShouldDelayModernRouteJumpLaunch(
        SimpleLevel level,
        PlayerEntity player,
        float targetX,
        float targetFeetY,
        int horizontal,
        bool hasGroundContact,
        float jumpRange,
        float jumpHeightTotal,
        out string delayReason)
    {
        delayReason = string.Empty;
        if (!hasGroundContact || horizontal == 0)
        {
            return false;
        }

        var run = MathF.Abs(targetX - player.X);
        var rise = player.Bottom - targetFeetY;
        if (rise < 14f
            || rise > jumpHeightTotal + 8f
            || run < 24f
            || run > jumpRange + 36f
            || MathF.Sign(targetX - player.X) != horizontal)
        {
            return false;
        }

        var classDefinition = BotNavigationClasses.GetDefinition(player.ClassId);
        var profile = BotNavigationProfiles.GetProfileForClass(player.ClassId);
        var targetY = targetFeetY - classDefinition.CollisionBottom;
        if (!BotNavigationMovementValidator.TryBuildJumpTape(
                level,
                classDefinition,
                profile,
                player.X,
                player.Y,
                targetX,
                targetY,
                player.Team,
                out var tape,
                out _)
            || tape.Count == 0)
        {
            return false;
        }

        var firstFrame = tape[0];
        if (firstFrame.Up)
        {
            return false;
        }

        var waitTicks = firstFrame.Ticks > 0
            ? firstFrame.Ticks
            : (int)Math.Ceiling(firstFrame.DurationSeconds * SimulationConfig.DefaultTicksPerSecond);
        if (waitTicks <= 1)
        {
            return false;
        }

        delayReason = $"jump_wait:{waitTicks}:dx{targetX - player.X:0}:rise{rise:0}";
        return true;
    }

    private static bool TriggerBotJump(BotMemory memory, BotTimingProfile timing)
    {
        memory.JumpCooldownTicks = timing.JumpCooldownTicks;
        return true;
    }

    private static bool TryGetModernCurrentNode(
        BotNavigationRuntimeGraph navigationGraph,
        BotMemory memory,
        out BotNavigationNode currentNode)
    {
        currentNode = default!;
        return memory.CurrentPointId >= 0
            && navigationGraph.TryGetNode(memory.CurrentPointId, out currentNode);
    }

    private static bool TryGetModernNextNode(
        ClientBotNavPoints navPoints,
        BotMemory memory,
        out BotNavigationNode nextNode)
    {
        nextNode = default!;
        return memory.NextPointId >= 0
            && navPoints.TryGetPoint(memory.NextPointId, out nextNode);
    }

    private static bool TryGetModernNextNode(
        BotNavigationRuntimeGraph navigationGraph,
        BotMemory memory,
        out BotNavigationNode nextNode)
    {
        nextNode = default!;
        return memory.NextPointId >= 0
            && navigationGraph.TryGetNode(memory.NextPointId, out nextNode);
    }

    private static bool TryShouldUseModernInterpolationJump(
        SimulationWorld world,
        PlayerEntity player,
        float currentPointX,
        float currentPointFeetY,
        float nextPointX,
        float nextPointFeetY,
        float previousX,
        float previousY,
        float jumpHeight)
    {
        var gunX = player.X;
        var gunY = player.Y - MathF.Max(2f, player.CollisionBottomOffset * 0.35f);
        var lineDistance = DistanceBetween(gunX, gunY, nextPointX, nextPointFeetY);
        var riseToTarget = gunY - nextPointFeetY;
        var angleFromUp = MathF.Abs(NormalizeDegrees(PointDirectionDegrees(gunX, gunY, nextPointX, nextPointFeetY) - 90f));
        var checkCount = Math.Clamp((int)MathF.Floor(DistanceBetween(currentPointX, currentPointFeetY, nextPointX, nextPointFeetY) / 24f) - 1, 0, 8);
        if (checkCount <= 0
            || lineDistance > 56f
            || riseToTarget <= 0f
            || angleFromUp > 35f)
        {
            return false;
        }

        var directionToTarget = PointDirectionDegrees(gunX, gunY, nextPointX, nextPointFeetY);
        var probeX = gunX + LengthDirX(42f, directionToTarget);
        var probeY = gunY + LengthDirY(42f, directionToTarget);
        if (LineHitsSolid(world, player, gunX, gunY, probeX, probeY))
        {
            return false;
        }

        for (var checkIndex = 1; checkIndex <= checkCount; checkIndex += 1)
        {
            var sampleX = currentPointX + ((nextPointX - currentPointX) * checkIndex / (checkCount + 1f));
            var sampleY = currentPointFeetY + ((nextPointFeetY - currentPointFeetY) * checkIndex / (checkCount + 1f));
            var passedX = (previousX <= sampleX && player.X >= sampleX) || (previousX >= sampleX && player.X <= sampleX);
            var passedY = (previousY <= sampleY && player.Y >= sampleY) || (previousY >= sampleY && player.Y <= sampleY);
            var nearPoint = DistanceBetween(player.X, player.Y, sampleX, sampleY) <= 12f;
            if (passedX || passedY || nearPoint)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryShouldUseModernLipJump(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination,
        float targetY,
        int horizontal,
        int stuckTicks,
        float jumpHeight)
    {
        if (stuckTicks <= 5)
        {
            return false;
        }

        var feetY = player.Bottom;
        var distanceToTarget = DistanceBetween(player.X, player.Y, destination.X, targetY);
        var riseToTarget = feetY - targetY;
        var horizontalSpeed = player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond;
        var movingToward = MathF.Sign(destination.X - player.X) == horizontal
            && (MathF.Abs(horizontalSpeed) < 1.5f || MathF.Sign(horizontalSpeed) == horizontal);
        return distanceToTarget <= 84f
            && riseToTarget >= 0f
            && riseToTarget <= 18f
            && movingToward
            && HasModernForwardFootBlock(world, player, horizontal, 8f)
            && HasModernJumpHeadClear(world, player, jumpHeight);
    }

    private static bool TryShouldUseModernSlopeJump(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination,
        float targetY,
        int horizontal,
        float jumpRange,
        float jumpHeight,
        float jumpHeightTotal)
    {
        var feetY = player.Bottom;
        var dxToTarget = destination.X - player.X;
        var rise = MathF.Max(0f, feetY - targetY);
        var run = MathF.Abs(dxToTarget);
        var distanceToTarget = DistanceBetween(player.X, player.Y, destination.X, targetY);
        var horizontalSpeed = player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond;
        var movingToward = MathF.Sign(dxToTarget) == horizontal
            && (MathF.Abs(horizontalSpeed) < 0.3f || MathF.Sign(horizontalSpeed) == horizontal);
        return movingToward
            && distanceToTarget > 34f
            && rise >= 14f
            && run >= 4f
            && rise > run
            && rise <= jumpHeightTotal
            && run <= jumpRange
            && HasModernJumpHeadClear(world, player, jumpHeight)
            && !LineHitsSolid(world, player, player.X, feetY - 4f, player.X + (24f * horizontal), feetY - 18f);
    }

    private static bool TryShouldUseModernHallJump(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination,
        float targetY,
        int horizontal,
        float jumpRange,
        float jumpHeight)
    {
        var gunX = player.X;
        var gunY = player.Y - MathF.Max(2f, player.CollisionBottomOffset * 0.35f);
        var distanceToTarget = DistanceBetween(gunX, gunY, destination.X, targetY);
        var directionToTarget = PointDirectionDegrees(gunX, gunY, destination.X, targetY);
        var angleFromUp = MathF.Abs(NormalizeDegrees(directionToTarget - 90f));
        var riseToTarget = gunY - targetY;
        var horizontalSpeed = player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond;
        var movingToward = MathF.Sign(destination.X - player.X) == horizontal
            && (MathF.Abs(horizontalSpeed) < 0.3f || MathF.Sign(horizontalSpeed) == horizontal);
        if (distanceToTarget > jumpRange
            || riseToTarget < 18f
            || angleFromUp > 48f
            || !movingToward
            || !HasModernJumpHeadClear(world, player, jumpHeight))
        {
            return false;
        }

        var probeX = gunX + LengthDirX(42f, directionToTarget);
        var probeY = gunY + LengthDirY(42f, directionToTarget);
        return !LineHitsSolid(world, player, gunX, gunY, probeX, probeY);
    }

    private static bool HasModernJumpHeadClear(SimulationWorld world, PlayerEntity player, float jumpHeight)
    {
        var feetY = player.Bottom;
        return !LineHitsSolid(world, player, player.X, feetY - 2f, player.X, feetY - (jumpHeight + 2f));
    }

    private static bool HasModernGroundContact(SimulationWorld world, PlayerEntity player)
    {
        var feetY = player.Bottom;
        return LineHitsSolid(world, player, player.X - 6f, feetY + 3f, player.X + 6f, feetY + 3f);
    }

    private static bool HasModernForwardFootBlock(SimulationWorld world, PlayerEntity player, int horizontal, float probeDistance)
    {
        var feetY = player.Bottom;
        var probeX = player.X + (probeDistance * horizontal);
        return LineHitsSolid(world, player, probeX, feetY - 1f, probeX, feetY + 4f);
    }

    private static bool HasModernGroundAhead(SimulationWorld world, PlayerEntity player, int horizontal, float probeDistance)
    {
        var feetY = player.Bottom;
        var probeX = player.X + (probeDistance * horizontal);
        return LineHitsSolid(world, player, probeX, feetY, probeX, feetY + 12f);
    }

    private static bool HasModernWaistObstacleAhead(SimulationWorld world, PlayerEntity player, int horizontal)
    {
        var probeX = player.X + (15f * horizontal);
        var legacyProbeTop = player.Y - player.CollisionBottomOffset + 6f;
        return LineHitsSolid(world, player, probeX, legacyProbeTop, probeX, player.Bottom - 12f);
    }

    private static bool IsModernStairStepAhead(SimulationWorld world, PlayerEntity player, int horizontal, float horizontalSpeed)
    {
        if (horizontal == 0 || MathF.Abs(horizontalSpeed) <= 0.0001f)
        {
            return false;
        }

        var stepDirection = MathF.Sign(horizontalSpeed);
        var x = player.X;
        while (!PointHitsSolid(world, player, x, player.Bottom - 1f) && MathF.Abs(player.X - x) < 60f)
        {
            x += stepDirection;
        }

        return PointHitsSolid(world, player, x + (5f * stepDirection), player.Bottom - 7f)
            || PointHitsSolid(world, player, x + (11f * stepDirection), player.Bottom - 13f);
    }

    private static bool PointHitsSolid(
        SimulationWorld world,
        PlayerEntity player,
        float x,
        float y)
    {
        return GetModernObstacleIndex(world.Level, player.Team, player.IsCarryingIntel).ContainsPoint(x, y);
    }

    private static float PointDirectionDegrees(float originX, float originY, float targetX, float targetY)
    {
        var radians = MathF.Atan2(originY - targetY, targetX - originX);
        return radians * (180f / MathF.PI);
    }

    private static float NormalizeDegrees(float degrees)
    {
        var normalized = (degrees + 540f) % 360f;
        return normalized - 180f;
    }

    private static float LengthDirX(float length, float directionDegrees)
    {
        var radians = directionDegrees * (MathF.PI / 180f);
        return MathF.Cos(radians) * length;
    }

    private static float LengthDirY(float length, float directionDegrees)
    {
        var radians = directionDegrees * (MathF.PI / 180f);
        return -MathF.Sin(radians) * length;
    }

    private static void GetModernClassJumpProfile(
        PlayerClass classId,
        out float jumpRange,
        out float jumpHeight,
        out float jumpHeightTotal)
    {
        jumpRange = 120f;
        jumpHeight = 60f;
        jumpHeightTotal = 60f;
        switch (classId)
        {
            case PlayerClass.Scout:
                jumpRange = 168f;
                jumpHeight = 60f;
                jumpHeightTotal = 120f;
                break;

            case PlayerClass.Heavy:
                jumpRange = 96f;
                jumpHeight = 60f;
                jumpHeightTotal = 60f;
                break;
        }
    }

    private static bool TryGetHealingCabinetDestination(SimulationWorld world, PlayerEntity player, out (float X, float Y) destination)
    {
        destination = default;
        if (player.IsCarryingIntel || GetHealthFraction(player) > CabinetSeekHealthFraction)
        {
            return false;
        }

        RoomObjectMarker? bestCabinet = null;
        var bestDistanceSquared = CabinetSeekDistance * CabinetSeekDistance;
        foreach (var cabinet in world.Level.GetRoomObjects(RoomObjectType.HealingCabinet))
        {
            var distanceSquared = DistanceSquared(player.X, player.Y, cabinet.CenterX, cabinet.CenterY);
            if (distanceSquared > bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            bestCabinet = cabinet;
        }

        if (!bestCabinet.HasValue)
        {
            return false;
        }

        destination = (bestCabinet.Value.CenterX, bestCabinet.Value.CenterY);
        return true;
    }

    private static TeamIntelligenceState GetTeamIntel(SimulationWorld world, PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? world.BlueIntel : world.RedIntel;
    }

    private static (float Min, float Max) GetPreferredCombatRange(PlayerClass classId)
    {
        return classId switch
        {
            PlayerClass.Scout => (80f, 160f),
            PlayerClass.Engineer => (110f, 220f),
            PlayerClass.Pyro => (40f, 130f),
            PlayerClass.Demoman => (120f, 260f),
            PlayerClass.Heavy => (110f, 220f),
            PlayerClass.Soldier => (150f, 320f),
            PlayerClass.Sniper => (220f, 520f),
            PlayerClass.Medic => (100f, 170f),
            PlayerClass.Spy => (110f, 260f),
            PlayerClass.Quote => (90f, 200f),
            _ => (90f, 180f),
        };
    }

    private static float GetAimTargetY(PlayerEntity player)
    {
        return player.Y - player.Height * AimChestOffsetFraction;
    }

    private static bool NeedsHealing(PlayerEntity player)
    {
        return player.Health < player.MaxHealth
            || player.IsBurning
            || player.IsCarryingIntel
            || GetHealthFraction(player) < LowHealthRetreatFraction;
    }

    private static bool ShouldKeepStickyTarget(
        SimulationWorld world,
        PlayerEntity player,
        PlayerEntity target,
        BotMemory memory)
    {
        if (!target.IsAlive
            || target.Team == player.Team
            || memory.TargetLockTicksRemaining <= 0
            || ShouldIgnoreEnemyTarget(target))
        {
            return false;
        }

        if (DistanceSquared(player.X, player.Y, target.X, target.Y) > VisibleTargetSeekDistance * VisibleTargetSeekDistance)
        {
            return false;
        }

        return HasCombatLineOfSight(world, player, target)
            || DistanceSquared(player.X, player.Y, target.X, target.Y) <= NearbyEnemyDistance * NearbyEnemyDistance;
    }

    private static PlayerEntity? TryResolveStickyTarget(
        IReadOnlyList<PlayerEntity> allPlayers,
        PlayerTeam expectedTeam,
        int targetPlayerId)
    {
        if (targetPlayerId <= 0)
        {
            return null;
        }

        for (var index = 0; index < allPlayers.Count; index += 1)
        {
            var player = allPlayers[index];
            if (player.Id == targetPlayerId && player.Team == expectedTeam)
            {
                return player;
            }
        }

        return null;
    }

    private static BotControllerDiagnosticsEntry CreateRespawningDiagnosticsEntry(
        ControlledBotSlot controlledSlot,
        PlayerEntity player)
    {
        return new BotControllerDiagnosticsEntry(
            controlledSlot.Slot,
            player.DisplayName,
            controlledSlot.Team,
            controlledSlot.ClassId,
            BotRole.None,
            BotStateKind.Respawning,
            BotFocusKind.None,
            string.Empty,
            string.Empty,
            HasVisibleEnemy: false,
            player.Health,
            player.MaxHealth,
            StuckTicks: 0,
            ModernStuckTicks: 0,
            UnstickTicks: 0,
            CurrentPointId: -1,
            NextPointId: -1,
            NextPoint2Id: -1,
            MovementTargetX: player.X,
            MovementTargetY: player.Y,
            RequestedHorizontal: 0,
            MoveDebug: string.Empty,
            RequestedJump: false,
            JumpDebug: string.Empty,
            RouteGoalNodeId: -1,
            RouteGoalX: player.X,
            RouteGoalY: player.Y,
            PreviousCurrentPointId: -1,
            PreviousNextPointId: -1,
            IsGrounded: player.IsGrounded,
            ProbeGrounded: false,
            SecondAnchorBlockPointId: -1,
            SecondAnchorBlockTicksRemaining: 0,
            NoNextPointTicks: 0,
            FallbackRouteLabel: string.Empty,
            FallbackTriggerLabel: string.Empty,
            NavigationIssueLabel: string.Empty,
            BranchFromPointId: -1,
            BranchToPointId: -1,
            BranchTicks: 0,
            BranchNoProgressTicks: 0,
            DirectTargetTicks: 0,
            DirectTargetNoProgressTicks: 0);
    }

    private BotControllerDiagnosticsEntry CreateDiagnosticsEntry(
        SimulationWorld world,
        ControlledBotSlot controlledSlot,
        PlayerEntity player,
        BotRole role,
        BotMemory memory,
        PlayerEntity? healTarget,
        ModernCombatTarget? combatTarget,
        bool hasVisibleEnemy,
        bool isSeekingCabinet,
        (float X, float Y) destination,
        NavigationDecision navigationDecision)
    {
        var focusKind = ResolveFocusKind(healTarget, combatTarget, isSeekingCabinet);
        var focusLabel = ResolveFocusLabel(world, controlledSlot.Team, role, focusKind, healTarget, combatTarget);
        var state = ResolveStateKind(
            player,
            memory,
            healTarget,
            combatTarget,
            hasVisibleEnemy,
            isSeekingCabinet,
            destination);
        return new BotControllerDiagnosticsEntry(
            controlledSlot.Slot,
            player.DisplayName,
            controlledSlot.Team,
            controlledSlot.ClassId,
            role,
            state,
            focusKind,
            focusLabel,
            navigationDecision.Label,
            hasVisibleEnemy,
            player.Health,
            player.MaxHealth,
            memory.StuckTicks,
            memory.ModernStuckTicks,
            memory.UnstickTicks,
            memory.CurrentPointId,
            memory.NextPointId,
            memory.NextPoint2Id,
            navigationDecision.MovementTarget.X,
            navigationDecision.MovementTarget.Y,
            _lastRequestedHorizontalForDiagnostics,
            _lastMoveDebugForDiagnostics,
            _lastRequestedJumpForDiagnostics,
            _lastJumpDebugForDiagnostics,
            memory.RouteGoalNodeId,
            memory.RouteGoalX,
            memory.RouteGoalY,
            memory.PreviousCurrentPointId,
            memory.PreviousNextPointId,
            player.IsGrounded,
            HasModernGroundContact(world, player),
            memory.SecondAnchorBlockPointId,
            memory.SecondAnchorBlockTicksRemaining,
            memory.NoNextPointTicks,
            memory.FallbackRouteLabel,
            memory.FallbackTriggerLabel,
            memory.NavigationIssueLabel,
            memory.BranchDiagnosticCurrentPointId,
            memory.BranchDiagnosticNextPointId,
            memory.BranchDiagnosticTicks,
            memory.BranchDiagnosticNoProgressTicks,
            memory.DirectTargetTicks,
            memory.DirectTargetNoProgressTicks);
    }

    private static BotStateKind ResolveStateKind(
        PlayerEntity player,
        BotMemory memory,
        PlayerEntity? healTarget,
        ModernCombatTarget? combatTarget,
        bool hasVisibleEnemy,
        bool isSeekingCabinet,
        (float X, float Y) destination)
    {
        if (memory.UnstickTicks > 0)
        {
            return BotStateKind.Unstick;
        }

        if (isSeekingCabinet)
        {
            return BotStateKind.SeekHealingCabinet;
        }

        if (player.ClassId == PlayerClass.Medic && healTarget is not null)
        {
            return BotStateKind.HealAlly;
        }

        if (combatTarget is ModernCombatTarget target && hasVisibleEnemy)
        {
            var preferredRange = GetPreferredCombatRange(player.ClassId);
            var distance = DistanceBetween(player.X, player.Y, target.X, target.Y);
            if (distance < preferredRange.Min)
            {
                return BotStateKind.CombatRetreat;
            }

            if (distance > preferredRange.Max)
            {
                return BotStateKind.CombatAdvance;
            }

            return BotStateKind.CombatStrafe;
        }

        if (DistanceSquared(player.X, player.Y, destination.X, destination.Y) <= ObjectiveArrivalDistance * ObjectiveArrivalDistance)
        {
            return BotStateKind.Patrol;
        }

        return BotStateKind.TravelObjective;
    }

    private static BotFocusKind ResolveFocusKind(
        PlayerEntity? healTarget,
        ModernCombatTarget? combatTarget,
        bool isSeekingCabinet)
    {
        if (isSeekingCabinet)
        {
            return BotFocusKind.HealingCabinet;
        }

        if (healTarget is not null)
        {
            return BotFocusKind.HealTarget;
        }

        return combatTarget is not null ? BotFocusKind.Enemy : BotFocusKind.Objective;
    }

    private static string ResolveFocusLabel(
        SimulationWorld world,
        PlayerTeam team,
        BotRole role,
        BotFocusKind focusKind,
        PlayerEntity? healTarget,
        ModernCombatTarget? combatTarget)
    {
        return focusKind switch
        {
            BotFocusKind.HealingCabinet => "cabinet",
            BotFocusKind.HealTarget => healTarget?.DisplayName ?? "ally",
            BotFocusKind.Enemy => GetCombatTargetLabel(combatTarget),
            _ => DescribeObjectiveFocus(world, team, role),
        };
    }

    private static bool HasCombatTargetLineOfSight(
        SimulationWorld world,
        PlayerEntity player,
        ModernCombatTarget target,
        bool useModernBehavior)
    {
        if (useModernBehavior)
        {
            return HasModernObstacleLineOfSight(world, player.X, player.Y, target.X, target.Y);
        }

        return target.Kind == ModernCombatTargetKind.Player
            && target.Player is not null
            && HasCombatLineOfSight(world, player, target.Player);
    }

    private static string GetCombatTargetLabel(ModernCombatTarget? combatTarget)
    {
        if (combatTarget is not ModernCombatTarget target)
        {
            return "enemy";
        }

        return target.Kind switch
        {
            ModernCombatTargetKind.Player => target.Player?.DisplayName ?? "enemy",
            ModernCombatTargetKind.Sentry => "sentry",
            ModernCombatTargetKind.Generator => "generator",
            _ => "enemy",
        };
    }

    private static string DescribeObjectiveFocus(SimulationWorld world, PlayerTeam team, BotRole role)
    {
        if (role == BotRole.ReturnWithIntel)
        {
            return "return home";
        }

        if (role == BotRole.EscortCarrier)
        {
            return "escort carrier";
        }

        if (role == BotRole.HuntCarrier)
        {
            return "hunt carrier";
        }

        return world.MatchRules.Mode switch
        {
            GameModeKind.CaptureTheFlag => role == BotRole.DefendObjective ? "defend intel" : "enemy intel",
            GameModeKind.ControlPoint => role == BotRole.DefendObjective ? "defend point" : "capture point",
            GameModeKind.Generator => role == BotRole.DefendObjective ? "defend gen" : "attack gen",
            GameModeKind.Arena => "arena point",
            GameModeKind.KingOfTheHill => "koth hill",
            GameModeKind.DoubleKingOfTheHill => role == BotRole.DefendObjective ? "defend hill" : "enemy hill",
            GameModeKind.TeamDeathmatch => "hunt enemies",
            _ => team == PlayerTeam.Blue ? "push red" : "push blu",
        };
    }

    private void TickMemory(BotMemory memory, PlayerEntity player, BotTimingProfile timing, bool useModernNavigation)
    {
        DecayRouteBlockedEdges(memory);

        if (memory.JumpCooldownTicks > 0)
        {
            memory.JumpCooldownTicks -= 1;
        }

        if (memory.TargetLockTicksRemaining > 0)
        {
            memory.TargetLockTicksRemaining -= 1;
        }

        if (memory.HealTargetLockTicksRemaining > 0)
        {
            memory.HealTargetLockTicksRemaining -= 1;
        }

        if (memory.StickyNextTicksRemaining > 0)
        {
            memory.StickyNextTicksRemaining -= 1;
        }

        if (memory.ReanchorTicksRemaining > 0)
        {
            memory.ReanchorTicksRemaining -= 1;
        }

        if (memory.SecondAnchorCooldownTicksRemaining > 0)
        {
            memory.SecondAnchorCooldownTicksRemaining -= 1;
        }

        if (memory.SecondAnchorBlockTicksRemaining > 0)
        {
            memory.SecondAnchorBlockTicksRemaining -= 1;
            if (memory.SecondAnchorBlockTicksRemaining <= 0)
            {
                memory.SecondAnchorBlockPointId = -1;
                memory.SecondAnchorBlockTicksRemaining = 0;
            }
        }

        if (memory.PreferredScoreRouteBlockedTicks > 0)
        {
            memory.PreferredScoreRouteBlockedTicks -= 1;
            if (memory.PreferredScoreRouteBlockedTicks <= 0)
            {
                memory.PreferredScoreRouteBlockedKey = string.Empty;
                memory.PreferredScoreRouteBlockedTicks = 0;
            }
        }

        if (memory.PreferredScoreRouteMissCooldownTicks > 0)
        {
            memory.PreferredScoreRouteMissCooldownTicks -= 1;
            if (memory.PreferredScoreRouteMissCooldownTicks <= 0)
            {
                memory.PreferredScoreRouteMissCooldownTicks = 0;
            }
        }

        if (memory.LastTraversalCompletionTicks > 0)
        {
            memory.LastTraversalCompletionTicks -= 1;
        }

        if (memory.NavChurnLockTicksRemaining > 0)
        {
            memory.NavChurnLockTicksRemaining -= 1;
            if (memory.NavChurnLockTicksRemaining <= 0)
            {
                memory.NavChurnLockPointId = -1;
                memory.NavChurnLockTicksRemaining = 0;
            }
        }

        if (memory.IntelReturnDirectTicksRemaining > 0)
        {
            memory.IntelReturnDirectTicksRemaining -= 1;
        }

        if (!player.IsCarryingIntel)
        {
            memory.IntelReturnDirectTicksRemaining = 0;
        }

        if (!useModernNavigation && memory.UnstickTicks > 0)
        {
            memory.UnstickTicks -= 1;
        }
        else if (useModernNavigation)
        {
            memory.UnstickTicks = 0;
            memory.StuckTicks = 0;
        }

        if (!memory.HasObservedPosition)
        {
            memory.PreviousObservedX = player.X;
            memory.PreviousObservedY = player.Y;
            memory.LastObservedX = player.X;
            memory.LastObservedY = player.Y;
            memory.HasObservedPosition = true;
            return;
        }

        var movedDistanceSquared = DistanceSquared(player.X, player.Y, memory.LastObservedX, memory.LastObservedY);
        if (useModernNavigation)
        {
            memory.StuckTicks = 0;
        }
        else if (memory.LastRequestedHorizontal != 0 && movedDistanceSquared < StuckMoveDistanceSquared)
        {
            memory.StuckTicks += 1;
        }
        else
        {
            memory.StuckTicks = 0;
        }

        if (!useModernNavigation && memory.StuckTicks >= timing.StuckTickThreshold)
        {
            ClearNavigationRoute(memory);
            memory.UnstickTicks = timing.UnstickTicks;
            memory.UnstickDirection = memory.LastRequestedHorizontal == 0
                ? GetRandomDirection()
                : -memory.LastRequestedHorizontal;
            memory.StrafeTicksRemaining = 0;
            memory.StuckTicks = 0;
        }

        memory.PreviousObservedX = memory.LastObservedX;
        memory.PreviousObservedY = memory.LastObservedY;
        memory.LastObservedX = player.X;
        memory.LastObservedY = player.Y;
    }

    private static void ResetTransientState(BotMemory memory, bool keepObservedPosition)
    {
        ClearNavigationRoute(memory);
        memory.StuckTicks = 0;
        memory.UnstickTicks = 0;
        memory.LastRequestedHorizontal = 0;
        memory.StrafeTicksRemaining = 0;
        memory.TargetLockTicksRemaining = 0;
        memory.HealTargetLockTicksRemaining = 0;
        memory.TargetPlayerId = -1;
        memory.HealTargetPlayerId = -1;
        if (!keepObservedPosition)
        {
            memory.HasObservedPosition = false;
            memory.PreviousObservedX = 0f;
            memory.PreviousObservedY = 0f;
            memory.LastObservedX = 0f;
            memory.LastObservedY = 0f;
        }

        memory.DoublebackActive = false;
        memory.ModernBeenHealingTicks = 0;
        memory.ModernReloadCounterTicks = 0;
        memory.ModernZoomToShootTicks = 0;
        memory.ModernBeenHealingSwitchTicks = 0;
        memory.IntelReturnDirectTicksRemaining = 0;
        memory.PreferredScoreRouteRouteMissStreak = 0;
        memory.PreferredScoreRouteMissCooldownTicks = 0;
        memory.RouteBlockedEdgeTicksByKey?.Clear();
        memory.RouteBlockedEdgeFailureCountsByKey?.Clear();
    }

    private static bool WouldMoveIntoObstacle(SimulationWorld world, PlayerEntity player, int horizontalDirection)
    {
        if (horizontalDirection == 0)
        {
            return false;
        }

        var probeLeft = horizontalDirection > 0
            ? player.Right + WallProbeDistance
            : player.Left - WallProbeDistance - WallProbeThickness;
        var probeRight = probeLeft + WallProbeThickness;
        var probeTop = player.Top;
        var probeBottom = player.Bottom - WallProbeBottomInset;

        foreach (var solid in world.Level.Solids)
        {
            if (RectanglesOverlap(probeLeft, probeTop, probeRight, probeBottom, solid.Left, solid.Top, solid.Right, solid.Bottom))
            {
                return true;
            }
        }

        foreach (var gate in world.Level.GetBlockingTeamGates(player.Team, player.IsCarryingIntel))
        {
            if (RectanglesOverlap(probeLeft, probeTop, probeRight, probeBottom, gate.Left, gate.Top, gate.Right, gate.Bottom))
            {
                return true;
            }
        }

        foreach (var wall in world.Level.GetRoomObjects(RoomObjectType.PlayerWall))
        {
            if (RectanglesOverlap(probeLeft, probeTop, probeRight, probeBottom, wall.Left, wall.Top, wall.Right, wall.Bottom))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasCombatLineOfSight(SimulationWorld world, PlayerEntity origin, PlayerEntity target)
    {
        return HasLineOfSight(
            world,
            origin.X,
            origin.Y,
            target.X,
            GetAimTargetY(target),
            origin.Team,
            origin.IsCarryingIntel);
    }

    private static bool HasLineOfSight(
        SimulationWorld world,
        float originX,
        float originY,
        float targetX,
        float targetY,
        PlayerTeam team,
        bool carryingIntel)
    {
        var distance = DistanceBetween(originX, originY, targetX, targetY);
        if (distance <= 0.0001f)
        {
            return true;
        }

        var directionX = (targetX - originX) / distance;
        var directionY = (targetY - originY) / distance;
        var lineLeft = MathF.Min(originX, targetX);
        var lineTop = MathF.Min(originY, targetY);
        var lineRight = MathF.Max(originX, targetX);
        var lineBottom = MathF.Max(originY, targetY);
        foreach (var solid in world.Level.Solids)
        {
            if (!RectanglesOverlap(lineLeft, lineTop, lineRight, lineBottom, solid.Left, solid.Top, solid.Right, solid.Bottom))
            {
                continue;
            }

            if (GetRayIntersectionDistanceWithRectangle(
                originX,
                originY,
                directionX,
                directionY,
                solid.Left,
                solid.Top,
                solid.Right,
                solid.Bottom,
                distance).HasValue)
            {
                return false;
            }
        }

        foreach (var gate in world.Level.GetBlockingTeamGates(team, carryingIntel))
        {
            if (!RectanglesOverlap(lineLeft, lineTop, lineRight, lineBottom, gate.Left, gate.Top, gate.Right, gate.Bottom))
            {
                continue;
            }

            if (GetRayIntersectionDistanceWithRectangle(
                originX,
                originY,
                directionX,
                directionY,
                gate.Left,
                gate.Top,
                gate.Right,
                gate.Bottom,
                distance).HasValue)
            {
                return false;
            }
        }

        foreach (var wall in world.Level.RoomObjects)
        {
            if (wall.Type != RoomObjectType.PlayerWall && wall.Type != RoomObjectType.BulletWall)
            {
                continue;
            }

            if (!RectanglesOverlap(lineLeft, lineTop, lineRight, lineBottom, wall.Left, wall.Top, wall.Right, wall.Bottom))
            {
                continue;
            }

            if (GetRayIntersectionDistanceWithRectangle(
                originX,
                originY,
                directionX,
                directionY,
                wall.Left,
                wall.Top,
                wall.Right,
                wall.Bottom,
                distance).HasValue)
            {
                return false;
            }
        }

        return true;
    }

    private static bool LineHitsSolid(
        SimulationWorld world,
        PlayerEntity player,
        float originX,
        float originY,
        float targetX,
        float targetY)
    {
        var distance = DistanceBetween(originX, originY, targetX, targetY);
        if (distance <= 0.0001f)
        {
            return GetModernObstacleGrid(world.Level, player.Team, player.IsCarryingIntel).ContainsPoint(originX, originY);
        }

        return GetModernObstacleGrid(world.Level, player.Team, player.IsCarryingIntel).LineHitsObstacle(originX, originY, targetX, targetY);
    }

    private static bool HasModernObstacleLineOfSight(
        SimulationWorld world,
        float originX,
        float originY,
        float targetX,
        float targetY)
    {
        var distance = DistanceBetween(originX, originY, targetX, targetY);
        if (distance <= 0.0001f)
        {
            return !GetModernObstacleGrid(world.Level).ContainsPoint(originX, originY);
        }

        return !GetModernObstacleGrid(world.Level).LineHitsObstacle(originX, originY, targetX, targetY);
    }

    private static ModernObstacleIndex GetModernObstacleIndex(SimpleLevel level)
    {
        return ModernObstacleIndexByLevel.GetValue(level, static currentLevel => new ModernObstacleIndex(ModernObstacleGeometry.BuildStaticObstacles(currentLevel)));
    }

    private static ModernObstacleIndex GetModernObstacleIndex(SimpleLevel level, PlayerTeam team, bool carryingIntel)
    {
        var cache = ModernPlayerObstacleIndicesByLevel.GetValue(level, static _ => new Dictionary<int, ModernObstacleIndex>());
        var cacheKey = ((int)team << 1) | (carryingIntel ? 1 : 0);
        if (cache.TryGetValue(cacheKey, out var index))
        {
            return index;
        }

        index = new ModernObstacleIndex(ModernObstacleGeometry.BuildRuntimePlayerObstacles(level, team, carryingIntel));
        cache[cacheKey] = index;
        return index;
    }

    private static ModernObstacleGrid GetModernObstacleGrid(SimpleLevel level)
    {
        return ModernObstacleGridByLevel.GetValue(level, static currentLevel => new ModernObstacleGrid(currentLevel.Bounds, ModernObstacleGeometry.BuildStaticObstacles(currentLevel)));
    }

    private static ModernObstacleGrid GetModernObstacleGrid(SimpleLevel level, PlayerTeam team, bool carryingIntel)
    {
        var cache = ModernPlayerObstacleGridsByLevel.GetValue(level, static _ => new Dictionary<int, ModernObstacleGrid>());
        var cacheKey = ((int)team << 1) | (carryingIntel ? 1 : 0);
        if (cache.TryGetValue(cacheKey, out var grid))
        {
            return grid;
        }

        grid = new ModernObstacleGrid(level.Bounds, ModernObstacleGeometry.BuildRuntimePlayerObstacles(level, team, carryingIntel));
        cache[cacheKey] = grid;
        return grid;
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
        float tMin;
        float tMax;

        if (MathF.Abs(directionX) < epsilon)
        {
            if (originX < left || originX > right)
            {
                return null;
            }

            tMin = float.NegativeInfinity;
            tMax = float.PositiveInfinity;
        }
        else
        {
            var invDirectionX = 1f / directionX;
            var tx1 = (left - originX) * invDirectionX;
            var tx2 = (right - originX) * invDirectionX;
            tMin = MathF.Min(tx1, tx2);
            tMax = MathF.Max(tx1, tx2);
        }

        float tyMin;
        float tyMax;
        if (MathF.Abs(directionY) < epsilon)
        {
            if (originY < top || originY > bottom)
            {
                return null;
            }

            tyMin = float.NegativeInfinity;
            tyMax = float.PositiveInfinity;
        }
        else
        {
            var invDirectionY = 1f / directionY;
            var ty1 = (top - originY) * invDirectionY;
            var ty2 = (bottom - originY) * invDirectionY;
            tyMin = MathF.Min(ty1, ty2);
            tyMax = MathF.Max(ty1, ty2);
        }

        var entryDistance = MathF.Max(tMin, tyMin);
        var exitDistance = MathF.Min(tMax, tyMax);
        if (exitDistance < 0f || entryDistance > exitDistance || entryDistance > maxDistance)
        {
            return null;
        }

        return entryDistance < 0f ? 0f : entryDistance;
    }

    private static bool RectanglesOverlap(
        float leftA,
        float topA,
        float rightA,
        float bottomA,
        float leftB,
        float topB,
        float rightB,
        float bottomB)
    {
        return leftA <= rightB
            && rightA >= leftB
            && topA <= bottomB
            && bottomA >= topB;
    }

    private void PruneMemory(IReadOnlyDictionary<byte, ControlledBotSlot> activeSlots)
    {
        _staleMemorySlotsBuffer.Clear();
        foreach (var entry in _memoryBySlot)
        {
            if (!activeSlots.ContainsKey(entry.Key))
            {
                _staleMemorySlotsBuffer.Add(entry.Key);
            }
        }

        for (var index = 0; index < _staleMemorySlotsBuffer.Count; index += 1)
        {
            _memoryBySlot.Remove(_staleMemorySlotsBuffer[index]);
        }
    }

    private BotMemory GetMemory(byte slot)
    {
        if (_memoryBySlot.TryGetValue(slot, out var memory))
        {
            return memory;
        }

        memory = new BotMemory { StrafeDirection = slot % 2 == 0 ? 1 : -1, UnstickDirection = 1 };
        _memoryBySlot[slot] = memory;
        return memory;
    }

    private int GetRandomDirection()
    {
        return _random.Next(2) == 0 ? -1 : 1;
    }

    private int GetRandomTicksInRange(int minimumTicks, int maximumTicks)
    {
        var minimum = Math.Max(1, Math.Min(minimumTicks, maximumTicks));
        var maximum = Math.Max(minimum, maximumTicks);
        return _random.Next(minimum, maximum + 1);
    }

    private static int GetMoveDirection(float deltaX)
    {
        return GetMoveDirection(deltaX, MovementDeadZone);
    }

    private static int GetMoveDirection(float deltaX, float deadZone)
    {
        if (MathF.Abs(deltaX) <= deadZone)
        {
            return 0;
        }

        return deltaX > 0f ? 1 : -1;
    }

    private static float GetHealthFraction(PlayerEntity player)
    {
        return player.MaxHealth <= 0
            ? 0f
            : Math.Clamp(player.Health / (float)player.MaxHealth, 0f, 1f);
    }

    private static (float X, float Y) GetLevelCenter(SimulationWorld world)
    {
        return (
            world.Level.Bounds.Width / 2f,
            world.Level.Bounds.Height / 2f);
    }

    private static PlayerTeam GetOpposingTeam(PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? PlayerTeam.Red : PlayerTeam.Blue;
    }

    private static bool ShouldIgnoreEnemyTarget(PlayerEntity player)
    {
        return player.ClassId == PlayerClass.Spy
            && player.IsSpyCloaked
            && !player.IsSpyVisibleToEnemies;
    }

    private static float DistanceBetween(float ax, float ay, float bx, float by)
    {
        return MathF.Sqrt(DistanceSquared(ax, ay, bx, by));
    }

    private static float DistanceSquared(float ax, float ay, float bx, float by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        return (dx * dx) + (dy * dy);
    }

    private static BotTimingProfile CreateTimingProfile(SimulationConfig config)
    {
        var ticksPerSecond = SimulationConfig.NormalizeTicksPerSecond(config.TicksPerSecond);
        return new BotTimingProfile(
            config.FixedDeltaSeconds,
            ticksPerSecond,
            ScaleBotTicks(StuckTickThreshold, ticksPerSecond),
            ScaleBotTicks(UnstickTicksDefault, ticksPerSecond),
            ScaleBotTicks(JumpCooldownTicksDefault, ticksPerSecond),
            ScaleBotTicks(StrafeTicksMin, ticksPerSecond),
            ScaleBotTicks(StrafeTicksMax, ticksPerSecond),
            ScaleBotTicks(RouteRefreshTicksDefault, ticksPerSecond),
            ScaleBotTicks(StickyTargetRefreshTicksDefault, ticksPerSecond),
            ScaleBotTicks(EnemyTargetLockTicksDefault, ticksPerSecond),
            ScaleBotTicks(HealTargetLockTicksDefault, ticksPerSecond));
    }

    private static int ScaleBotTicks(int authoredTicks, int ticksPerSecond)
    {
        if (authoredTicks <= 0)
        {
            return 0;
        }

        var scaledTicks = authoredTicks * (ticksPerSecond / (double)SimulationConfig.DefaultTicksPerSecond);
        return Math.Max(1, (int)Math.Ceiling(scaledTicks));
    }

    internal sealed class BotMemory
    {
        public bool HasObservedPosition { get; set; }

        public float LastObservedX { get; set; }

        public float LastObservedY { get; set; }

        public float PreviousObservedX { get; set; }

        public float PreviousObservedY { get; set; }

        public int LastRequestedHorizontal { get; set; }

        public string ModernMoveDebug { get; set; } = string.Empty;

        public bool DoublebackActive { get; set; }

        public int StuckTicks { get; set; }

        public int UnstickTicks { get; set; }

        public int UnstickDirection { get; set; } = 1;

        public int JumpCooldownTicks { get; set; }

        public string ModernJumpDebug { get; set; } = string.Empty;

        public int StrafeDirection { get; set; } = 1;

        public int StrafeTicksRemaining { get; set; }

        public int TargetPlayerId { get; set; } = -1;

        public int TargetLockTicksRemaining { get; set; }

        public int HealTargetPlayerId { get; set; } = -1;

        public int HealTargetLockTicksRemaining { get; set; }

        public int ModernBeenHealingTicks { get; set; }

        public int ModernBeenHealingSwitchTicks { get; set; }

        public int ModernZoomToShootTicks { get; set; }

        public int ModernReloadCounterTicks { get; set; }

        public int ModernCombatTicksPerSecond { get; set; } = SimulationConfig.DefaultTicksPerSecond;

        public int CurrentPointId { get; set; } = -1;

        public int NextPointId { get; set; } = -1;

        public int NextPoint2Id { get; set; } = -1;

        public int NextPoint3Id { get; set; } = -1;

        public int PreviousCurrentPointId { get; set; } = -1;

        public int PreviousNextPointId { get; set; } = -1;

        public int LastCommittedPointId { get; set; } = -1;

        public int SecondLastCommittedPointId { get; set; } = -1;

        public int BlockedPointId { get; set; } = -1;

        public int BlockedPointTicksRemaining { get; set; }

        public int NoNextPointTicks { get; set; }

        public int LoopBacktrackTicks { get; set; }

        public int StickyCurrentPointId { get; set; } = -1;

        public int StickyNextPointId { get; set; } = -1;

        public int StickyNextTicksRemaining { get; set; }

        public int NavChurnCurrentPointId { get; set; } = -1;

        public int NavChurnTicks { get; set; }

        public int NavChurnSwitchTicks { get; set; }

        public int NavChurnLockPointId { get; set; } = -1;

        public int NavChurnLockTicksRemaining { get; set; }

        public float NavChurnStartX { get; set; }

        public float NavChurnStartY { get; set; }

        public int ModernStuckTicks { get; set; }

        public int ModernDropGapTicks { get; set; }

        public float ModernPreviousTargetDistance { get; set; } = float.PositiveInfinity;

        public bool HasModernClosestPointTarget { get; set; }

        public float ModernClosestPointTargetX { get; set; }

        public float ModernClosestPointTargetY { get; set; }

        public int ReanchorTicksRemaining { get; set; }

        public int SecondAnchorCooldownTicksRemaining { get; set; }

        public int SecondAnchorBlockPointId { get; set; } = -1;

        public int SecondAnchorBlockTicksRemaining { get; set; }

        public string FallbackRouteLabel { get; set; } = string.Empty;

        public string FallbackTriggerLabel { get; set; } = string.Empty;

        public string NavigationIssueLabel { get; set; } = string.Empty;

        public int BranchDiagnosticCurrentPointId { get; set; } = -1;

        public int BranchDiagnosticNextPointId { get; set; } = -1;

        public int BranchDiagnosticTicks { get; set; }

        public int BranchDiagnosticNoProgressTicks { get; set; }

        public int DirectTargetTicks { get; set; }

        public int DirectTargetNoProgressTicks { get; set; }

        public float CaptureHoldInsideMilliseconds { get; set; }

        public float CaptureHoldRetainMilliseconds { get; set; }

        public int CaptureActiveGroupId { get; set; } = -1;

        public int[]? RouteNodeIds { get; set; }

        public int RouteIndex { get; set; }

        public bool RouteIsPartial { get; set; }

        public int RouteGoalNodeId { get; set; } = -1;

        public int RouteRefreshTicks { get; set; }

        public float RouteGoalX { get; set; }

        public float RouteGoalY { get; set; }

        public string NavigationGraphKey { get; set; } = string.Empty;

        public int RouteProgressFromNodeId { get; set; } = -1;

        public int RouteProgressToNodeId { get; set; } = -1;

        public float RouteProgressBestDistance { get; set; } = float.PositiveInfinity;

        public int RouteNoProgressTicks { get; set; }

        public float RouteObjectiveBestDistance { get; set; } = float.PositiveInfinity;

        public int RouteObjectiveNoProgressTicks { get; set; }

        public int IntelReturnDirectTicksRemaining { get; set; }

        public Dictionary<long, int>? RouteBlockedEdgeTicksByKey { get; set; }

        public Dictionary<long, int>? RouteBlockedEdgeFailureCountsByKey { get; set; }

        public string PreferredScoreRouteKey { get; set; } = string.Empty;

        public string PreferredScoreRouteLabel { get; set; } = string.Empty;

        public BotNavigationScoreRouteEntry? ActivePreferredScoreRoute { get; set; }

        public string PreferredScoreRouteBlockedKey { get; set; } = string.Empty;

        public int PreferredScoreRouteBlockedTicks { get; set; }

        public int PreferredScoreRouteRouteMissStreak { get; set; }

        public int PreferredScoreRouteMissCooldownTicks { get; set; }

        public int ActiveTraversalFromNodeId { get; set; } = -1;

        public int ActiveTraversalToNodeId { get; set; } = -1;

        public BotNavigationTraversalKind ActiveTraversalKind { get; set; } = BotNavigationTraversalKind.Walk;

        public IReadOnlyList<BotNavigationInputFrame>? ActiveTraversalTape { get; set; }

        public int ActiveTraversalFrameIndex { get; set; }

        public double ActiveTraversalFrameSecondsRemaining { get; set; }

        public int LastTraversalFromNodeId { get; set; } = -1;

        public int LastTraversalToNodeId { get; set; } = -1;

        public bool LastTraversalUsedTape { get; set; }

        public int LastTraversalCompletionTicks { get; set; }

        public ClientBot2020CompatState? ClientBot2020Compat { get; set; }
    }

    internal sealed class ClientBot2020CompatState
    {
        public bool Defending { get; set; }

        public bool CaptureObjectiveMode { get; set; }

        public bool PathPointIsZone { get; set; }

        public bool HasCaptureZoneObject { get; set; }

        public int CaptureZoneRefreshTicksRemaining { get; set; }

        public string OldPathPointKey { get; set; } = string.Empty;

        public int OldClosestPoint { get; set; } = -1;

        public int GoalPointId { get; set; } = -1;

        public int[]? GoalWeights { get; set; }

        public bool NavMapDone { get; set; }

        public int CurrentPoint { get; set; } = -1;

        public int NextPoint { get; set; } = -1;

        public int NextPoint2 { get; set; } = -1;

        public int NextPoint3 { get; set; } = -1;

        public int PreviousCurrentPoint { get; set; } = -1;

        public int PreviousNextPoint { get; set; } = -1;

        public int LastCommittedPoint { get; set; } = -1;

        public int SecondLastCommittedPoint { get; set; } = -1;

        public int StickyCurrentPoint { get; set; } = -1;

        public int StickyNextPoint { get; set; } = -1;

        public int StickyNextTicks { get; set; }

        public int NavChurnCurrentPoint { get; set; } = -1;

        public int NavChurnTicks { get; set; }

        public int NavChurnSwitchTicks { get; set; }

        public int NavChurnLockPoint { get; set; } = -1;

        public int NavChurnLockTicks { get; set; }

        public float NavChurnStartX { get; set; }

        public float NavChurnStartY { get; set; }

        public int NoNextPointTicks { get; set; }

        public int LoopBacktrackTicks { get; set; }

        public int SecondAnchorCooldownTicks { get; set; }

        public int SecondAnchorBlockPoint { get; set; } = -1;

        public int SecondAnchorBlockTicks { get; set; }

        public int StuckTicks { get; set; }

        public int DropGapTicks { get; set; }

        public float PreviousTargetDistance { get; set; } = float.PositiveInfinity;

        public float ClosestPointX { get; set; }

        public float ClosestPointY { get; set; }

        public string DebugDecisionReason { get; set; } = string.Empty;

        public bool Doubleback { get; set; }

        public string ActiveFallbackEntryLabel { get; set; } = string.Empty;

        public string ActiveFallbackTargetLabel { get; set; } = string.Empty;

        public string ActiveFallbackTriggerLabel { get; set; } = string.Empty;

        public int ActiveFallbackRouteIndex { get; set; }

        public int ActiveFallbackTicks { get; set; }

        public int ActiveFallbackNoProgressTicks { get; set; }

        public float ActiveFallbackPreviousTargetDistance { get; set; } = float.PositiveInfinity;

        public BotNavigationTraversalKind ActiveFallbackTraversalKind { get; set; } = BotNavigationTraversalKind.Walk;

        public string ActiveFallbackTraversalFromLabel { get; set; } = string.Empty;

        public string ActiveFallbackTraversalToLabel { get; set; } = string.Empty;

        public IReadOnlyList<BotNavigationInputFrame>? ActiveFallbackTraversalTape { get; set; }

        public int ActiveFallbackTraversalFrameIndex { get; set; }

        public double ActiveFallbackTraversalFrameSecondsRemaining { get; set; }

        public string NavigationIssueLabel { get; set; } = string.Empty;

        public int BranchDiagnosticCurrentPoint { get; set; } = -1;

        public int BranchDiagnosticNextPoint { get; set; } = -1;

        public string BranchDiagnosticReason { get; set; } = string.Empty;

        public float BranchDiagnosticTargetX { get; set; }

        public float BranchDiagnosticTargetY { get; set; }

        public int BranchDiagnosticTicks { get; set; }

        public int BranchDiagnosticNoProgressTicks { get; set; }

        public float BranchDiagnosticBestDistance { get; set; } = float.PositiveInfinity;

        public int DirectTargetTicks { get; set; }

        public int DirectTargetNoProgressTicks { get; set; }
    }

    private readonly record struct BotTimingProfile(
        double FixedDeltaSeconds,
        int TicksPerSecond,
        int StuckTickThreshold,
        int UnstickTicks,
        int JumpCooldownTicks,
        int StrafeTicksMin,
        int StrafeTicksMax,
        int RouteRefreshTicks,
        int StickyTargetRefreshTicks,
        int TargetLockTicks,
        int HealTargetLockTicks);

    private enum ModernCombatTargetKind
    {
        Player,
        Sentry,
        Generator,
    }

    private readonly record struct ModernCombatTarget(
        ModernCombatTargetKind Kind,
        PlayerTeam Team,
        float X,
        float Y,
        PlayerEntity? Player = null,
        SentryEntity? Sentry = null,
        GeneratorState? Generator = null);

    internal readonly record struct ModernPathSelection(
        (float X, float Y) Destination,
        bool AllowDirectPath,
        bool IsCaptureObjective = false,
        ModernCaptureObjective? CaptureObjective = null);

    internal readonly record struct ModernCaptureObjective(
        (float X, float Y) TargetZone,
        (float X, float Y) TargetPoint,
        float MinX,
        float MaxX,
        float MinY,
        float MaxY,
        ModernCapturePoint[] Points,
        ModernCaptureGroup[] Groups);

    internal readonly record struct ModernCapturePoint(
        float X,
        float Y,
        int GroupId);

    internal readonly record struct ModernCaptureGroup(
        int GroupId,
        float MinX,
        float MaxX,
        float MinY,
        float MaxY,
        int PointCount,
        float NearestDistanceToBot);

    private readonly record struct ModernCaptureZoneCandidate(
        float X,
        float Y,
        float DistanceToControlPoint);

    private readonly record struct ModernCaptureHoldState(
        bool SuppressNavigation,
        int ActiveGroupId,
        float TargetX,
        float TargetY);

    private readonly record struct NavigationDecision(
        (float X, float Y) MovementTarget,
        bool HasRoute,
        int ForcedHorizontalDirection,
        bool ForceJump,
        bool LocksMovement,
        string Label,
        BotNavigationTraversalKind TraversalKind = BotNavigationTraversalKind.Walk,
        bool MovementTargetUsesFeetCoordinates = false,
        bool CaptureHoldActive = false,
        bool UsesTraversalTape = false);

    private sealed class ModernObstacleIndex
    {
        private readonly LevelSolid[] _solids;
        private readonly Dictionary<long, int[]> _solidIdsByCellKey;
        private readonly int[] _visitMarks;
        private int _queryStamp;

        public ModernObstacleIndex(IReadOnlyList<LevelSolid> solids)
        {
            _solids = solids.Count == 0 ? Array.Empty<LevelSolid>() : solids.ToArray();
            _visitMarks = new int[_solids.Length];

            var idsByCell = new Dictionary<long, List<int>>();
            for (var solidIndex = 0; solidIndex < _solids.Length; solidIndex += 1)
            {
                var solid = _solids[solidIndex];
                var minCellX = GetCellCoordinate(solid.Left);
                var maxCellX = GetCellCoordinate(solid.Right);
                var minCellY = GetCellCoordinate(solid.Top);
                var maxCellY = GetCellCoordinate(solid.Bottom);
                for (var cellY = minCellY; cellY <= maxCellY; cellY += 1)
                {
                    for (var cellX = minCellX; cellX <= maxCellX; cellX += 1)
                    {
                        var cellKey = GetCellKey(cellX, cellY);
                        if (!idsByCell.TryGetValue(cellKey, out var solidIds))
                        {
                            solidIds = new List<int>();
                            idsByCell[cellKey] = solidIds;
                        }

                        solidIds.Add(solidIndex);
                    }
                }
            }

            _solidIdsByCellKey = new Dictionary<long, int[]>(idsByCell.Count);
            foreach (var entry in idsByCell)
            {
                _solidIdsByCellKey[entry.Key] = entry.Value.ToArray();
            }
        }

        public bool IntersectsAny(
            float originX,
            float originY,
            float targetX,
            float targetY,
            float directionX,
            float directionY,
            float maxDistance)
        {
            if (_solids.Length == 0)
            {
                return false;
            }

            _queryStamp += 1;
            if (_queryStamp == int.MaxValue)
            {
                Array.Clear(_visitMarks);
                _queryStamp = 1;
            }

            var lineLeft = MathF.Min(originX, targetX);
            var lineTop = MathF.Min(originY, targetY);
            var lineRight = MathF.Max(originX, targetX);
            var lineBottom = MathF.Max(originY, targetY);
            var minCellX = GetCellCoordinate(lineLeft);
            var maxCellX = GetCellCoordinate(lineRight);
            var minCellY = GetCellCoordinate(lineTop);
            var maxCellY = GetCellCoordinate(lineBottom);
            for (var cellY = minCellY; cellY <= maxCellY; cellY += 1)
            {
                for (var cellX = minCellX; cellX <= maxCellX; cellX += 1)
                {
                    if (!_solidIdsByCellKey.TryGetValue(GetCellKey(cellX, cellY), out var solidIds))
                    {
                        continue;
                    }

                    for (var index = 0; index < solidIds.Length; index += 1)
                    {
                        var solidId = solidIds[index];
                        if (_visitMarks[solidId] == _queryStamp)
                        {
                            continue;
                        }

                        _visitMarks[solidId] = _queryStamp;
                        var solid = _solids[solidId];
                        if (!RectanglesOverlap(lineLeft, lineTop, lineRight, lineBottom, solid.Left, solid.Top, solid.Right, solid.Bottom))
                        {
                            continue;
                        }

                        if (GetRayIntersectionDistanceWithRectangle(
                                originX,
                                originY,
                                directionX,
                                directionY,
                                solid.Left,
                                solid.Top,
                                solid.Right,
                                solid.Bottom,
                                maxDistance).HasValue)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public bool ContainsPoint(float x, float y)
        {
            if (_solids.Length == 0)
            {
                return false;
            }

            var cellKey = GetCellKey(GetCellCoordinate(x), GetCellCoordinate(y));
            if (!_solidIdsByCellKey.TryGetValue(cellKey, out var solidIds))
            {
                return false;
            }

            for (var index = 0; index < solidIds.Length; index += 1)
            {
                var solid = _solids[solidIds[index]];
                if (x >= solid.Left && x <= solid.Right && y >= solid.Top && y <= solid.Bottom)
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetCellCoordinate(float coordinate)
        {
            return (int)MathF.Floor(coordinate / ModernObstacleCellSize);
        }

        private static long GetCellKey(int cellX, int cellY)
        {
            return ((long)cellX << 32) ^ (uint)cellY;
        }
    }

    private sealed class ModernObstacleGrid
    {
        private readonly bool[] _blocked;

        public ModernObstacleGrid(WorldBounds bounds, IReadOnlyList<LevelSolid> solids)
        {
            Width = (int)MathF.Ceiling(bounds.Width);
            Height = (int)MathF.Ceiling(bounds.Height);
            _blocked = new bool[(Width + 1) * (Height + 1)];
            for (var solidIndex = 0; solidIndex < solids.Count; solidIndex += 1)
            {
                var solid = solids[solidIndex];
                var left = Math.Max(0, (int)MathF.Floor(solid.Left));
                var top = Math.Max(0, (int)MathF.Floor(solid.Top));
                var right = Math.Min(Width, (int)MathF.Ceiling(solid.Right));
                var bottom = Math.Min(Height, (int)MathF.Ceiling(solid.Bottom));
                for (var y = top; y < bottom; y += 1)
                {
                    var rowOffset = y * (Width + 1);
                    for (var x = left; x < right; x += 1)
                    {
                        _blocked[rowOffset + x] = true;
                    }
                }
            }
        }

        public int Width { get; }

        public int Height { get; }

        public bool ContainsPoint(float x, float y)
        {
            var ix = (int)MathF.Floor(x);
            var iy = (int)MathF.Floor(y);
            if (ix < 0 || iy < 0 || ix >= Width || iy >= Height)
            {
                return false;
            }

            return _blocked[(iy * (Width + 1)) + ix];
        }

        public bool LineHitsObstacle(float originX, float originY, float targetX, float targetY)
        {
            var deltaX = targetX - originX;
            var deltaY = targetY - originY;
            var steps = (int)Math.Ceiling(Math.Max(Math.Abs(deltaX), Math.Abs(deltaY)));
            if (steps <= 0)
            {
                return ContainsPoint(originX, originY);
            }

            for (var step = 0; step <= steps; step += 1)
            {
                var t = step / (float)steps;
                if (ContainsPoint(originX + (deltaX * t), originY + (deltaY * t)))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private readonly record struct ControlledPlayerState(ControlledBotSlot ControlledSlot, PlayerEntity Player);
}
