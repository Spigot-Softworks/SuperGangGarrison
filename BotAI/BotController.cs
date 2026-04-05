using OpenGarrison.Core;
using System.Runtime.CompilerServices;

namespace OpenGarrison.BotAI;

public sealed class ModernPracticeBotController : IPracticeBotController
{
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
    private const float TraversalStartDistance = 6f;
    private const float RoutePartialMinimumImprovementDistance = 18f;
    private const float RouteRepathDistance = 120f;
    private const float RouteStartNodeSearchDistance = 144f;
    private const float RouteGoalNodeSearchDistance = 220f;
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
    private const float ModernCurrentPointReanchorDistance = 130f;
    private const float ModernChurnDistance = 52f;
    private const float ModernCaptureZoneSeedBand = 96f;
    private const float ModernCaptureZoneGroupLinkDistance = 63f;
    private const float ModernCaptureZoneSquareHalfSize = 21f;
    private const float ModernCaptureBrakeTargetDistanceX = 24f;
    private const float ModernCaptureBrakeTargetDistanceY = 56f;
    private const float ModernCaptureEnemyNearbyDistance = 250f;
    private const float ModernObstacleCellSize = 128f;
    private const float ModernDropReclassifyDistance = 18f;
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
    private const float ModernMineThreatDistance = 400f;
    private const float ModernMineDetonationRadius = 50f;

    private readonly Dictionary<byte, BotMemory> _memoryBySlot = new();
    private readonly Dictionary<string, BotNavigationRuntimeGraph> _navigationGraphsByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<int, int> _modernSpyVisibleTicksByPlayerId = new();
    private readonly Random _random = new(1337);
    private int _modernSpyReactTicksThreshold = ModernSpyReactTimeSourceTicks;
    private static readonly ConditionalWeakTable<SimpleLevel, ModernObstacleIndex> ModernObstacleIndexByLevel = new();

    public bool CollectDiagnostics { get; set; }

    public BotControllerDiagnosticsSnapshot LastDiagnostics { get; private set; } = BotControllerDiagnosticsSnapshot.Empty;

    public void Reset()
    {
        _memoryBySlot.Clear();
        _navigationGraphsByKey.Clear();
        _modernSpyVisibleTicksByPlayerId.Clear();
        LastDiagnostics = BotControllerDiagnosticsSnapshot.Empty;
    }

    public IReadOnlyDictionary<byte, PlayerInputSnapshot> BuildInputs(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots,
        IReadOnlyDictionary<PlayerClass, BotNavigationAsset>? navigationAssets = null)
    {
        var inputs = new Dictionary<byte, PlayerInputSnapshot>();
        if (controlledSlots.Count == 0)
        {
            LastDiagnostics = BotControllerDiagnosticsSnapshot.Empty;
            return inputs;
        }

        PruneMemory(controlledSlots.Keys);

        var allPlayers = BuildPlayerRoster(world);
        var controlledPlayers = BuildControlledPlayerRoster(world, controlledSlots);
        var navigationGraphsByClass = BuildNavigationGraphs(navigationAssets);
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
            var navigationGraph = navigationGraphsByClass.GetValueOrDefault(entry.Value.ControlledSlot.ClassId);
            var useModernNavigation = navigationGraph?.BuildStrategy == BotNavigationBuildStrategy.ModernClientBotPointGraph;
            TickMemory(memory, player, timing, useModernNavigation);

            if (!player.IsAlive)
            {
                ResetTransientState(memory, keepObservedPosition: false);
                inputs[slot] = default;
                if (diagnosticsEntries is not null)
                {
                    diagnosticsEntries.Add(CreateRespawningDiagnosticsEntry(entry.Value.ControlledSlot, player));
                }
                continue;
            }

            const BotRole role = BotRole.None;
            inputs[slot] = BuildInputForBot(
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

        return inputs;
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
                new NavigationDecision((player.X, player.Y), HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "modern-nav-missing"));
            return default;
        }

        const bool useModernBehavior = true;
        EnsureModernCombatMemoryInitialized(memory, timing);
        var healTarget = FindBestHealTarget(world, player, controlledSlot.Team, allPlayers, memory, timing, useModernBehavior);
        var medicBuddyTarget = useModernBehavior
            ? FindBestModernMedicBuddyTarget(player, controlledSlot.Team, allPlayers)
            : null;
        var objectiveSelection = ResolveModernPathSelection(world, player, controlledSlot.Team, allPlayers, medicBuddyTarget);
        var isSeekingCabinet = false;
        var destination = objectiveSelection.Destination;
        var allowModernDirectPath = objectiveSelection.AllowDirectPath;
        var navigationDecision = ResolveNavigationDecision(world, player, controlledSlot.ClassId, destination, navigationGraph, memory, timing, allowModernDirectPath, objectiveSelection);
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
            memory,
            timing);
        var firePrimary = ResolvePrimaryFire(world, player, combatTarget, healTarget, memory, useModernBehavior, isBeingHealed);
        var fireSecondary = ResolveSecondaryFire(world, player, combatTarget, healTarget, allPlayers, memory, hasVisibleEnemy, useModernBehavior, isBeingHealed);
        if (useModernBehavior)
        {
            ApplyModernSoldierCloseRangeAdjustment(world, player, combatTarget, memory, firePrimary, timing, ref horizontal, ref jump);
            ApplyModernReloadDiscipline(player, memory, ref firePrimary, ref fireSecondary);
            UpdateModernCombatMemory(player, memory);
        }
        var buildSentry = ResolveBuildSentry(world, player, destination);
        var dropIntel = ResolveDropIntel(player, medicBuddyTarget);

        CommitModernFrameState(navigationGraph, memory);
        memory.LastRequestedHorizontal = horizontal;
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
            DropIntel: dropIntel);
    }

    private static Dictionary<byte, ControlledPlayerState> BuildControlledPlayerRoster(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots)
    {
        var roster = new Dictionary<byte, ControlledPlayerState>();
        foreach (var entry in controlledSlots)
        {
            if (!world.TryGetNetworkPlayer(entry.Key, out var player)
                || world.IsNetworkPlayerAwaitingJoin(entry.Key))
            {
                continue;
            }

            roster[entry.Key] = new ControlledPlayerState(entry.Value, player);
        }

        return roster;
    }

    private static List<PlayerEntity> BuildPlayerRoster(SimulationWorld world)
    {
        var players = new List<PlayerEntity>();
        foreach (var entry in world.EnumerateActiveNetworkPlayers())
        {
            players.Add(entry.Player);
        }

        if (world.EnemyPlayerEnabled)
        {
            players.Add(world.EnemyPlayer);
        }

        if (world.FriendlyDummyEnabled)
        {
            players.Add(world.FriendlyDummy);
        }

        return players;
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

    private Dictionary<PlayerClass, BotNavigationRuntimeGraph> BuildNavigationGraphs(
        IReadOnlyDictionary<PlayerClass, BotNavigationAsset>? navigationAssets)
    {
        var graphs = new Dictionary<PlayerClass, BotNavigationRuntimeGraph>();
        if (navigationAssets is null || navigationAssets.Count == 0)
        {
            return graphs;
        }

        foreach (var entry in navigationAssets)
        {
            var asset = entry.Value;
            if (asset.BuildStrategy != BotNavigationBuildStrategy.ModernClientBotPointGraph)
            {
                continue;
            }

            var scopeToken = asset.BuildStrategy == BotNavigationBuildStrategy.ModernClientBotPointGraph && !asset.ClassId.HasValue
                ? "modern"
                : asset.ClassId.HasValue
                    ? BotNavigationClasses.GetFileToken(asset.ClassId.Value)
                    : BotNavigationProfiles.GetFileToken(asset.Profile);
            var cacheKey = $"{asset.LevelName}|{asset.MapAreaIndex}|{scopeToken}|{asset.LevelFingerprint}|{asset.BuildStrategy}|{asset.BuiltUtc.Ticks}|{asset.Nodes.Count}|{asset.Edges.Count}";
            if (!_navigationGraphsByKey.TryGetValue(cacheKey, out var graph))
            {
                graph = new BotNavigationRuntimeGraph(asset);
                _navigationGraphsByKey[cacheKey] = graph;
            }

            graphs[entry.Key] = graph;
        }

        return graphs;
    }

    private static NavigationDecision ResolveNavigationDecision(
        SimulationWorld world,
        PlayerEntity player,
        PlayerClass classId,
        (float X, float Y) destination,
        BotNavigationRuntimeGraph? navigationGraph,
        BotMemory memory,
        BotTimingProfile timing,
        bool allowModernDirectPath,
        ModernPathSelection objectiveSelection)
    {
        if (navigationGraph is null)
        {
            ClearNavigationRoute(memory);
            return new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "direct");
        }

        if (navigationGraph.BuildStrategy == BotNavigationBuildStrategy.ModernClientBotPointGraph)
        {
            return ResolveModernNavigationDecision(world, player, destination, navigationGraph, memory, timing, allowModernDirectPath, objectiveSelection);
        }

        if (!navigationGraph.TryFindNearestNode(player.X, player.Y, RouteStartNodeSearchDistance, requireGroundSupport: true, out var startNode))
        {
            ClearNavigationRoute(memory);
            return new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "start-miss");
        }

        var hasExactGoalNode = navigationGraph.TryFindNearestNode(destination.X, destination.Y, RouteGoalNodeSearchDistance, requireGroundSupport: true, out var goalNode);
        var exactGoalNodeId = hasExactGoalNode ? goalNode.Id : -1;
        var goalMoved = destination.X != memory.RouteGoalX
            || destination.Y != memory.RouteGoalY;
        if (memory.RouteRefreshTicks > 0)
        {
            memory.RouteRefreshTicks -= 1;
        }

        var requiresRepath = memory.RouteNodeIds is null
            || memory.RouteNodeIds.Length == 0
            || !string.Equals(memory.NavigationGraphKey, navigationGraph.CacheKey, StringComparison.Ordinal)
            || memory.RouteGoalNodeId != exactGoalNodeId
            || goalMoved
            || memory.RouteRefreshTicks <= 0
            || !TryGetCurrentRouteNode(navigationGraph, memory, out var currentRouteNode)
            || DistanceSquared(player.X, player.Y, currentRouteNode.X, currentRouteNode.Y) > RouteRepathDistance * RouteRepathDistance;

        if (requiresRepath)
        {
            if (!TryBuildRouteToDestination(
                    navigationGraph,
                    startNode.Id,
                    exactGoalNodeId,
                    destination,
                    out var route,
                    out var routeIsPartial))
            {
                ClearNavigationRoute(memory);
                memory.NavigationGraphKey = navigationGraph.CacheKey;
                memory.RouteGoalNodeId = exactGoalNodeId;
                memory.RouteGoalX = destination.X;
                memory.RouteGoalY = destination.Y;
                memory.RouteRefreshTicks = timing.RouteRefreshTicks;
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
            memory.RouteRefreshTicks = timing.RouteRefreshTicks;
            memory.RouteIsPartial = routeIsPartial;
        }

        if (memory.RouteNodeIds is null || memory.RouteNodeIds.Length <= 1)
        {
            return new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "direct");
        }

        AdvanceRouteProgress(player, navigationGraph, memory);
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

        if (TryResolveTraversalDecision(
                player,
                destination,
                navigationGraph,
                memory,
                previousNodeId,
                nextNode,
                edge,
                timing.FixedDeltaSeconds,
                out var traversalDecision))
        {
            return traversalDecision;
        }

        return new NavigationDecision((nextNode.X, nextNode.Y), HasRoute: true, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: GetRouteLabel(memory, edge.Kind));
    }

    private static NavigationDecision ResolveModernNavigationDecision(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination,
        BotNavigationRuntimeGraph navigationGraph,
        BotMemory memory,
        BotTimingProfile timing,
        bool allowModernDirectPath,
        ModernPathSelection objectiveSelection)
    {
        var captureHoldState = ResolveModernCaptureHoldState(player, objectiveSelection, memory, timing.FixedDeltaSeconds);
        if (captureHoldState.SuppressNavigation)
        {
            memory.NextPointId = -1;
            memory.NextPoint2Id = -1;
            memory.NextPoint3Id = -1;
            memory.NoNextPointTicks = 0;
            memory.StickyNextTicksRemaining = 0;
            return new NavigationDecision(
                (player.X, player.Bottom),
                HasRoute: true,
                ForcedHorizontalDirection: 0,
                ForceJump: false,
                LocksMovement: false,
                Label: "capture_zone_hold",
                MovementTargetUsesFeetCoordinates: true,
                CaptureHoldActive: true);
        }

        if (allowModernDirectPath && HasModernDirectPath(world, player, destination))
        {
            memory.NoNextPointTicks = Math.Max(0, memory.NoNextPointTicks - ModernNoNextTickDecay);
            return new NavigationDecision(destination, HasRoute: true, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "mdirect");
        }

        var goalMoved = destination.X != memory.RouteGoalX
            || destination.Y != memory.RouteGoalY;
        if (!string.Equals(memory.NavigationGraphKey, navigationGraph.CacheKey, StringComparison.Ordinal))
        {
            ResetModernNavigationState(memory);
        }

        BotNavigationNode goalNode;
        if (memory.RouteGoalNodeId >= 0
            && !goalMoved
            && string.Equals(memory.NavigationGraphKey, navigationGraph.CacheKey, StringComparison.Ordinal)
            && navigationGraph.TryGetNode(memory.RouteGoalNodeId, out goalNode))
        {
        }
        else if (!navigationGraph.TryFindNearestNode(destination.X, destination.Y, maxDistance: 0f, requireGroundSupport: false, out goalNode))
        {
            ClearNavigationRoute(memory);
            return new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "mgoal-miss");
        }

        memory.NavigationGraphKey = navigationGraph.CacheKey;
        memory.RouteGoalNodeId = goalNode.Id;
        memory.RouteGoalX = destination.X;
        memory.RouteGoalY = destination.Y;

        var maximumGoalWeightDepth = ModernMaximumWeightDepth;
        var goalWeights = navigationGraph.GetGoalWeights(goalNode.Id, maximumGoalWeightDepth);
        if (goalWeights is null)
        {
            ClearNavigationRoute(memory);
            return new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "mweight-miss");
        }

        for (var attempt = 0; attempt < 3; attempt += 1)
        {
            if (!TryGetCurrentModernNode(navigationGraph, memory, out var currentNode)
                || ShouldReacquireModernCurrentNode(world, player, navigationGraph, goalWeights, currentNode, memory))
            {
                if (!TryAcquireModernCurrentNode(world, navigationGraph, player, memory, honorSecondAnchorBlock: true, out currentNode))
                {
                    ClearNavigationRoute(memory);
                    return new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "mstart-miss");
                }

                memory.CurrentPointId = currentNode.Id;
                memory.NextPoint2Id = -1;
                memory.NextPoint3Id = -1;
            }

            if (!TryGetCurrentModernNode(navigationGraph, memory, out currentNode))
            {
                ClearNavigationRoute(memory);
                return new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "mcurrent-miss");
            }

            var currentWeight = GetModernGoalWeight(goalWeights, currentNode.Id);
            if (currentWeight <= 1)
            {
                return new NavigationDecision(destination, HasRoute: true, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: $"m{Math.Max(1, currentWeight)}:direct");
            }

            var hasQueuedPoints = TryPopulateModernQueuedPoints(
                    world,
                    player,
                    destination,
                    navigationGraph,
                    goalWeights,
                    memory,
                    out var selectionReason);

            if (TryReacquireBlockedModernCurrentPoint(world, player, navigationGraph, goalWeights, memory, destination, out selectionReason))
            {
                continue;
            }

            if (!TryGetModernQueuedMovementTarget(world, player, navigationGraph, memory, out var movementTarget))
            {
                movementTarget = destination;
            }

            UpdateModernTargetProgress(player, memory, movementTarget);

            if (TryApplyModernReanchor(
                    world,
                    player,
                    destination,
                    navigationGraph,
                    goalWeights,
                    memory,
                    currentNode,
                    out selectionReason))
            {
                continue;
            }

            if (TryPromoteModernQueuedPoints(player, navigationGraph, memory, out selectionReason))
            {
                if (!TryGetCurrentModernNode(navigationGraph, memory, out currentNode))
                {
                    continue;
                }
            }

            if (!TryGetModernQueuedMovementTarget(world, player, navigationGraph, memory, out movementTarget))
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

            var traversalKind = BotNavigationTraversalKind.Walk;
            var nextWeight = 0;
            var forcedHorizontalDirection = 0;
            if (TryGetCurrentModernNode(navigationGraph, memory, out currentNode)
                && TryGetModernNextNode(navigationGraph, memory, out var nextNode)
                && navigationGraph.TryGetEdge(currentNode.Id, nextNode.Id, out var nextEdge))
            {
                traversalKind = GetEffectiveModernTraversalKind(currentNode, nextNode, nextEdge.Kind);
                forcedHorizontalDirection = traversalKind == BotNavigationTraversalKind.Drop
                    ? navigationGraph.GetDropDirection(currentNode.Id, nextNode.Id)
                    : 0;
                nextWeight = GetModernGoalWeight(goalWeights, nextNode.Id);
            }

            return new NavigationDecision(
                movementTarget,
                HasRoute: hasQueuedPoints || movementTarget == destination,
                ForcedHorizontalDirection: forcedHorizontalDirection,
                ForceJump: false,
                LocksMovement: false,
                Label: $"m{GetModernGoalWeight(goalWeights, currentNode.Id)}->{nextWeight}:{selectionReason}:{GetTraversalLabel(traversalKind)}",
                TraversalKind: traversalKind,
                MovementTargetUsesFeetCoordinates: !useIntelReturnFinalApproach);
        }

        ClearNavigationRoute(memory);
        return new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "mroute-miss");
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
        BotNavigationRuntimeGraph navigationGraph,
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
                GetModernNodeFeetY(navigationGraph, currentNode) - ModernPointSightYOffset))
        {
            return false;
        }

        var movementTarget = (X: memory.RouteGoalX, Y: memory.RouteGoalY);
        if (TryGetModernQueuedMovementTarget(world, player, navigationGraph, memory, out var queuedMovementTarget))
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
        BotNavigationRuntimeGraph navigationGraph,
        BotMemory memory,
        out BotNavigationNode nextNode)
    {
        nextNode = default!;
        return memory.NextPoint2Id >= 0
            && navigationGraph.TryGetNode(memory.NextPoint2Id, out nextNode);
    }

    private static bool TryGetModernThirdNextNode(
        BotNavigationRuntimeGraph navigationGraph,
        BotMemory memory,
        out BotNavigationNode nextNode)
    {
        nextNode = default!;
        return memory.NextPoint3Id >= 0
            && navigationGraph.TryGetNode(memory.NextPoint3Id, out nextNode);
    }

    private static bool TryPopulateModernQueuedPoints(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination,
        BotNavigationRuntimeGraph navigationGraph,
        int[] goalWeights,
        BotMemory memory,
        out string selectionReason)
    {
        selectionReason = "nav_step";
        memory.NextPoint2Id = -1;
        memory.NextPoint3Id = -1;

        if (!TryGetCurrentModernNode(navigationGraph, memory, out var currentNode))
        {
            return false;
        }

        if (!TrySelectModernNextEdges(
                world,
                player,
                destination,
                navigationGraph,
                goalWeights,
                memory,
                out var nextNode,
                out var nextEdge,
                out var nextWeight,
                out var secondNextNode,
                out var secondNextEdge,
                out var secondNextWeight))
        {
            if (memory.NextPointId >= 0)
            {
                selectionReason = "carry_next";
                return true;
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
                    navigationGraph,
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
                memory.CurrentPointId = -1;
                memory.NoNextPointTicks = 0;
                return false;
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
            navigationGraph,
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
        PopulateModernLookaheadPoints(world, player, navigationGraph, goalWeights, memory, currentNode, nextNode);
        memory.StickyCurrentPointId = currentNode.Id;
        memory.StickyNextPointId = nextNode.Id;
        memory.StickyNextTicksRemaining = memory.ModernStuckTicks > ModernStickyDisableStuckTicks
            ? 0
            : ModernStickyTicks;
        return true;
    }

    private static void PopulateModernLookaheadPoints(
        SimulationWorld world,
        PlayerEntity player,
        BotNavigationRuntimeGraph navigationGraph,
        int[] goalWeights,
        BotMemory memory,
        BotNavigationNode currentNode,
        BotNavigationNode nextNode)
    {
        memory.NextPoint2Id = TrySelectModernLookaheadPoint(world, player, navigationGraph, goalWeights, currentNode, nextNode, memory)
            ?.Id ?? -1;

        if (memory.NextPoint2Id >= 0
            && navigationGraph.TryGetNode(memory.NextPoint2Id, out var secondNode))
        {
            memory.NextPoint3Id = TrySelectModernLookaheadPoint(world, player, navigationGraph, goalWeights, nextNode, secondNode, memory)
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
        BotNavigationRuntimeGraph navigationGraph,
        int[] goalWeights,
        BotNavigationNode currentNode,
        BotNavigationNode fromNode,
        BotMemory memory)
    {
        if (!navigationGraph.TryGetOutgoingEdges(fromNode.Id, out var outgoingEdges))
        {
            return null;
        }

        var currentWeight = GetModernGoalWeight(goalWeights, fromNode.Id);
        var bestWeight = currentWeight;
        BotNavigationNode? bestNode = null;
        for (var edgeIndex = 0; edgeIndex < outgoingEdges.Count; edgeIndex += 1)
        {
            var edge = outgoingEdges[edgeIndex];
            if (!navigationGraph.TryGetNode(edge.ToNodeId, out var candidateNode))
            {
                continue;
            }

            if (candidateNode.Id == currentNode.Id)
            {
                continue;
            }

            var candidateWeight = GetModernGoalWeight(goalWeights, candidateNode.Id);
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
        BotNavigationRuntimeGraph navigationGraph,
        BotMemory memory,
        out (float X, float Y) movementTarget)
    {
        movementTarget = default;
        if (!TryGetModernNextNode(navigationGraph, memory, out var nextNode))
        {
            return false;
        }

        var nextFeetY = GetModernNodeFeetY(navigationGraph, nextNode);
        movementTarget = (nextNode.X, nextFeetY);
        if (TryGetModernSecondNextNode(navigationGraph, memory, out var secondNextNode)
            && DistanceSquared(player.X, player.Y, nextNode.X, nextFeetY) < ModernPointLookaheadDistance * ModernPointLookaheadDistance
            && HasModernGroundContact(world, player)
            && MathF.Abs(player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond) > ModernPointLookaheadMinimumSpeed)
        {
            var secondFeetY = GetModernNodeFeetY(navigationGraph, secondNextNode);
            movementTarget = (secondNextNode.X, secondFeetY);
            if (TryGetModernThirdNextNode(navigationGraph, memory, out var thirdNextNode)
                && DistanceSquared(player.X, player.Y, secondNextNode.X, secondFeetY) < 20f * 20f)
            {
                movementTarget = (thirdNextNode.X, GetModernNodeFeetY(navigationGraph, thirdNextNode));
            }
        }

        return true;
    }

    private static bool TryReacquireBlockedModernCurrentPoint(
        SimulationWorld world,
        PlayerEntity player,
        BotNavigationRuntimeGraph navigationGraph,
        int[] goalWeights,
        BotMemory memory,
        (float X, float Y) fallbackMovementTarget,
        out string selectionReason)
    {
        selectionReason = string.Empty;
        if (!TryGetCurrentModernNode(navigationGraph, memory, out var currentNode))
        {
            return false;
        }

        if (!TryGetModernQueuedMovementTarget(world, player, navigationGraph, memory, out var movementTarget))
        {
            movementTarget = fallbackMovementTarget;
        }

        if (HasModernObstacleLineOfSight(world, player.X, player.Y, currentNode.X, GetModernNodeFeetY(navigationGraph, currentNode) - ModernPointSightYOffset)
            || HasModernObstacleLineOfSight(world, player.X, player.Y, movementTarget.X, movementTarget.Y - ModernPointSightYOffset))
        {
            return false;
        }

        if (!TryAcquireNearestModernNode(navigationGraph, player, memory, honorSecondAnchorBlock: false, out var reacquiredNode))
        {
            return false;
        }

        memory.CurrentPointId = reacquiredNode.Id;
        memory.NextPointId = -1;
        memory.NextPoint2Id = -1;
        memory.NextPoint3Id = -1;
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
            ? DistanceBetween(player.X, player.Y, memory.LastObservedX, memory.LastObservedY)
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
        BotNavigationRuntimeGraph navigationGraph,
        int[] goalWeights,
        BotMemory memory,
        BotNavigationNode currentNode,
        out string selectionReason)
    {
        selectionReason = string.Empty;
        var horizontalSpeed = player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond;
        var hasGroundContact = HasModernGroundContact(world, player);
        var currentNodeFeetY = GetModernNodeFeetY(navigationGraph, currentNode);
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
            && TryResolveModernSecondAnchorPoint(world, player, navigationGraph, goalWeights, memory, currentNode, out var secondAnchorPointId))
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
        if (TryAcquireNearestModernNode(navigationGraph, player, memory, honorSecondAnchorBlock: true, out var reacquiredNode))
        {
            memory.CurrentPointId = reacquiredNode.Id;
        }

        return true;
    }

    private static bool TryResolveModernSecondAnchorPoint(
        SimulationWorld world,
        PlayerEntity player,
        BotNavigationRuntimeGraph navigationGraph,
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

        if (!navigationGraph.TryGetOutgoingEdges(currentNode.Id, out var outgoingEdges))
        {
            return false;
        }

        var bestScore = float.PositiveInfinity;
        for (var edgeIndex = 0; edgeIndex < outgoingEdges.Count; edgeIndex += 1)
        {
            var edge = outgoingEdges[edgeIndex];
            if (!navigationGraph.TryGetNode(edge.ToNodeId, out var candidateNode)
                || candidateNode.Id == currentNode.Id
                || candidateNode.Id == memory.NextPointId)
            {
                continue;
            }

            if (!CanUseModernTraversal(world, player, navigationGraph, currentNode, candidateNode, edge))
            {
                continue;
            }

            var candidateScore = GetModernGoalWeight(goalWeights, candidateNode.Id) * 1000f
                + DistanceBetween(candidateNode.X, GetModernNodeFeetY(navigationGraph, candidateNode), memory.RouteGoalX, memory.RouteGoalY);
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
        BotNavigationRuntimeGraph navigationGraph,
        BotMemory memory,
        out string selectionReason)
    {
        selectionReason = string.Empty;
        if (!TryGetModernNextNode(navigationGraph, memory, out var nextNode))
        {
            return false;
        }

        if (DistanceSquared(player.X, player.Y, nextNode.X, GetModernNodeFeetY(navigationGraph, nextNode)) >= ModernPointArrivalDistance * ModernPointArrivalDistance)
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
            selectionReason = "promote_np2";
        }
        else
        {
            memory.NextPointId = -1;
            selectionReason = "promote_np";
        }

        return true;
    }

    private static bool TryAcquireNearestModernNode(
        BotNavigationRuntimeGraph navigationGraph,
        PlayerEntity player,
        BotMemory memory,
        bool honorSecondAnchorBlock,
        out BotNavigationNode node)
    {
        node = default!;
        var bestDistance = float.PositiveInfinity;
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
            if (distance >= bestVisibleDistance)
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

    private static bool TrySelectModernNextEdges(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination,
        BotNavigationRuntimeGraph navigationGraph,
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

        if (!TryGetCurrentModernNode(navigationGraph, memory, out var currentNode)
            || !navigationGraph.TryGetOutgoingEdges(currentNode.Id, out var outgoingEdges))
        {
            return false;
        }

        var currentWeight = GetModernGoalWeight(goalWeights, currentNode.Id);
        if (currentWeight <= 0)
        {
            return false;
        }

        var currentFeetY = GetModernNodeFeetY(navigationGraph, currentNode);
        var feetY = player.Bottom;
        var horizontalSpeed = player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond;
        GetModernClassJumpProfile(player.ClassId, out _, out var jumpHeight, out _);
        var bestScore = float.PositiveInfinity;
        var secondScore = float.PositiveInfinity;
        for (var edgeIndex = 0; edgeIndex < outgoingEdges.Count; edgeIndex += 1)
        {
            var edge = outgoingEdges[edgeIndex];
            if (!navigationGraph.TryGetNode(edge.ToNodeId, out var candidateNode))
            {
                continue;
            }

            var candidateWeight = GetModernGoalWeight(goalWeights, candidateNode.Id);
            if (candidateWeight >= currentWeight)
            {
                continue;
            }

            if (!CanUseModernTraversal(world, player, navigationGraph, currentNode, candidateNode, edge))
            {
                continue;
            }

            var candidateFeetY = GetModernNodeFeetY(navigationGraph, candidateNode);
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

            if (candidateNode.Id == memory.SecondAnchorBlockPointId
                && memory.SecondAnchorBlockTicksRemaining > 0
                && memory.CurrentPointId != memory.SecondAnchorBlockPointId)
            {
                candidateScore += 5000f;
            }

            var candidateRunFromPlayer = MathF.Abs(candidateNode.X - player.X);
            var candidateRiseFromPlayer = feetY - candidateFeetY;
            if (memory.CurrentPointId == memory.PreviousCurrentPointId
                && MathF.Abs(horizontalSpeed) < 1.2f
                && candidateRunFromPlayer < 96f)
            {
                var lipProbeDirection = MathF.Sign(candidateNode.X - player.X);
                if (lipProbeDirection != 0f)
                {
                    var lipProbeX = player.X + (7f * lipProbeDirection);
                    var lipFrontFootBlocked = LineHitsSolid(world, lipProbeX, feetY - 1f, lipProbeX, feetY + 3f);
                    var lipHeadRoom = !LineHitsSolid(world, lipProbeX, feetY - 12f, lipProbeX, feetY - (jumpHeight + 2f));
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
        BotNavigationRuntimeGraph navigationGraph,
        int[] goalWeights,
        BotMemory memory,
        out BotNavigationNode? nextNode,
        out BotNavigationEdge? nextEdge,
        out int nextWeight)
    {
        nextNode = null;
        nextEdge = null;
        nextWeight = 0;

        if (!TryGetCurrentModernNode(navigationGraph, memory, out var currentNode)
            || !navigationGraph.TryGetOutgoingEdges(currentNode.Id, out var outgoingEdges))
        {
            return false;
        }

        var currentWeight = GetModernGoalWeight(goalWeights, currentNode.Id);
        var bestScore = float.PositiveInfinity;
        for (var edgeIndex = 0; edgeIndex < outgoingEdges.Count; edgeIndex += 1)
        {
            var edge = outgoingEdges[edgeIndex];
            if (!navigationGraph.TryGetNode(edge.ToNodeId, out var candidateNode))
            {
                continue;
            }

            if (!CanUseModernTraversal(world, player, navigationGraph, currentNode, candidateNode, edge))
            {
                continue;
            }

            var candidateWeight = GetModernGoalWeight(goalWeights, candidateNode.Id);
            var candidateScore = DistanceBetween(candidateNode.X, GetModernNodeFeetY(navigationGraph, candidateNode), destination.X, destination.Y)
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
        BotNavigationRuntimeGraph navigationGraph,
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
                && TryGetModernEdge(currentNode.Id, memory.PreviousNextPointId, navigationGraph, out _))
            {
                memory.NavChurnLockPointId = memory.PreviousNextPointId;
                memory.NavChurnLockTicksRemaining = ModernChurnLockTicks;
            }
        }

        if (memory.NavChurnLockTicksRemaining > 0
            && memory.NavChurnLockPointId >= 0
            && memory.NavChurnCurrentPointId == currentNode.Id
            && selectedNextNode.Id != memory.NavChurnLockPointId
            && TryGetModernEdge(currentNode.Id, memory.NavChurnLockPointId, navigationGraph, out var churnLockEdge)
            && navigationGraph.TryGetNode(memory.NavChurnLockPointId, out var churnLockNode))
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
            else if (TryGetModernEdge(currentNode.Id, memory.StickyNextPointId, navigationGraph, out var stickyEdge)
                     && navigationGraph.TryGetNode(memory.StickyNextPointId, out var stickyNode))
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

    private static void CommitModernFrameState(BotNavigationRuntimeGraph? navigationGraph, BotMemory memory)
    {
        if (navigationGraph?.BuildStrategy != BotNavigationBuildStrategy.ModernClientBotPointGraph)
        {
            return;
        }

        memory.PreviousCurrentPointId = memory.CurrentPointId;
        memory.PreviousNextPointId = memory.NextPointId;
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

    private static bool HasModernDirectPath(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination)
    {
        return MathF.Abs(player.X - destination.X) <= ModernPointDirectDistance
            && MathF.Abs(player.Y - destination.Y) <= ModernPointVerticalDistance
            && HasModernObstacleLineOfSight(world, player.X, player.Y, destination.X, destination.Y);
    }

    private static bool ShouldUseModernIntelReturnFinalApproach(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination)
    {
        return player.IsCarryingIntel
            && MathF.Abs(player.X - destination.X) <= ModernIntelReturnFinalApproachDistanceX
            && MathF.Abs(player.Bottom - destination.Y) <= ModernIntelReturnFinalApproachDistanceY
            && HasModernObstacleLineOfSight(world, player.X, player.Y, destination.X, destination.Y);
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
        BotNavigationRuntimeGraph navigationGraph,
        BotNavigationNode currentNode,
        BotNavigationNode candidateNode,
        BotNavigationEdge edge)
    {
        var currentFeetY = GetModernNodeFeetY(navigationGraph, currentNode);
        var candidateFeetY = GetModernNodeFeetY(navigationGraph, candidateNode);
        var jumpRiseNeeded = MathF.Max(0f, currentFeetY - candidateFeetY);
        if (jumpRiseNeeded > GetModernJumpHeight(player.ClassId))
        {
            return false;
        }

        var jumpDistanceNeeded = MathF.Abs(candidateNode.X - currentNode.X);
        if (jumpDistanceNeeded > GetModernJumpRange(player.ClassId)
            && !LineHitsSolid(world, currentNode.X, currentFeetY + 2f, candidateNode.X, candidateFeetY + 2f))
        {
            return false;
        }

        return true;
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

    private static bool TryResolveTraversalDecision(
        PlayerEntity player,
        (float X, float Y) destination,
        BotNavigationRuntimeGraph navigationGraph,
        BotMemory memory,
        int previousNodeId,
        BotNavigationNode nextNode,
        BotNavigationEdge edge,
        double simulationTickSeconds,
        out NavigationDecision decision)
    {
        if (edge.InputTape.Count > 0)
        {
            if (!navigationGraph.TryGetNode(previousNodeId, out var sourceNode))
            {
                ClearNavigationRoute(memory);
                decision = new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "jump-src");
                return true;
            }

            if (!IsExecutingTraversal(memory, previousNodeId, nextNode.Id))
            {
                ClearTraversalExecution(memory);
            }

            if (DistanceSquared(player.X, player.Y, sourceNode.X, sourceNode.Y) > TraversalStartDistance * TraversalStartDistance)
            {
                decision = new NavigationDecision((sourceNode.X, sourceNode.Y), HasRoute: true, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: true, Label: GetRouteLabel(memory, edge.Kind));
                return true;
            }

            if (!IsExecutingTraversal(memory, previousNodeId, nextNode.Id))
            {
                if (!player.IsGrounded)
                {
                    decision = new NavigationDecision((sourceNode.X, sourceNode.Y), HasRoute: true, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: true, Label: GetRouteLabel(memory, edge.Kind));
                    return true;
                }

                BeginTraversalExecution(memory, previousNodeId, nextNode.Id, edge);
            }

            if (TryConsumeTraversalExecution(memory, simulationTickSeconds, out var forcedHorizontalDirection, out var forceJump))
            {
                decision = new NavigationDecision((nextNode.X, nextNode.Y), HasRoute: true, ForcedHorizontalDirection: forcedHorizontalDirection, ForceJump: forceJump, LocksMovement: true, Label: GetRouteLabel(memory, edge.Kind));
                return true;
            }

            ClearTraversalExecution(memory);
            decision = new NavigationDecision((nextNode.X, nextNode.Y), HasRoute: true, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: GetRouteLabel(memory, edge.Kind));
            return true;
        }

        ClearTraversalExecution(memory);
        if (edge.Kind == BotNavigationTraversalKind.Drop)
        {
            if (!navigationGraph.TryGetNode(previousNodeId, out var sourceNode))
            {
                ClearNavigationRoute(memory);
                decision = new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "drop-src");
                return true;
            }

            if (DistanceSquared(player.X, player.Y, sourceNode.X, sourceNode.Y) > RouteNodeArrivalDistance * RouteNodeArrivalDistance)
            {
                decision = new NavigationDecision((sourceNode.X, sourceNode.Y), HasRoute: true, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: true, Label: GetRouteLabel(memory, edge.Kind));
                return true;
            }

            var dropDirection = navigationGraph.GetDropDirection(sourceNode.Id, nextNode.Id);
            decision = new NavigationDecision((nextNode.X, nextNode.Y), HasRoute: true, ForcedHorizontalDirection: dropDirection, ForceJump: false, LocksMovement: true, Label: GetRouteLabel(memory, edge.Kind));
            return true;
        }

        decision = default;
        return false;
    }

    private static void AdvanceRouteProgress(PlayerEntity player, BotNavigationRuntimeGraph navigationGraph, BotMemory memory)
    {
        if (memory.RouteNodeIds is null)
        {
            return;
        }

        while (memory.RouteIndex < memory.RouteNodeIds.Length
            && navigationGraph.TryGetNode(memory.RouteNodeIds[memory.RouteIndex], out var currentNode)
            && HasReachedRouteNode(player, navigationGraph, memory.RouteNodeIds, memory.RouteIndex, currentNode))
        {
            memory.RouteIndex += 1;
        }
    }

    private static bool HasReachedRouteNode(
        PlayerEntity player,
        BotNavigationRuntimeGraph navigationGraph,
        int[] routeNodeIds,
        int routeIndex,
        BotNavigationNode currentNode)
    {
        if (DistanceSquared(player.X, player.Y, currentNode.X, currentNode.Y)
            <= GetRouteNodeArrivalDistanceSquared(navigationGraph, routeNodeIds, routeIndex))
        {
            return true;
        }

        return routeIndex > 0
            && currentNode.RequiresGroundSupport
            && player.IsGrounded
            && navigationGraph.TryGetEdge(routeNodeIds[routeIndex - 1], routeNodeIds[routeIndex], out var edge)
            && edge.Kind == BotNavigationTraversalKind.Walk
            && MathF.Abs(player.X - currentNode.X) <= RouteNodeArrivalDistance
            && MathF.Abs(player.Y - currentNode.Y) <= RouteWalkApproximateArrivalHeight;
    }

    private static float GetRouteNodeArrivalDistanceSquared(
        BotNavigationRuntimeGraph navigationGraph,
        int[] routeNodeIds,
        int routeIndex)
    {
        var arrivalDistance = RouteNodeArrivalDistance;
        if (routeIndex >= 0
            && routeIndex + 1 < routeNodeIds.Length
            && navigationGraph.TryGetEdge(routeNodeIds[routeIndex], routeNodeIds[routeIndex + 1], out var edge)
            && (edge.InputTape.Count > 0 || edge.Kind == BotNavigationTraversalKind.Drop))
        {
            arrivalDistance = TraversalStartDistance;
        }

        return arrivalDistance * arrivalDistance;
    }

    private static bool TryGetCurrentRouteNode(BotNavigationRuntimeGraph navigationGraph, BotMemory memory, out BotNavigationNode node)
    {
        node = default!;
        return memory.RouteNodeIds is not null
            && memory.RouteIndex >= 0
            && memory.RouteIndex < memory.RouteNodeIds.Length
            && navigationGraph.TryGetNode(memory.RouteNodeIds[memory.RouteIndex], out node);
    }

    private static bool TryBuildRouteToDestination(
        BotNavigationRuntimeGraph navigationGraph,
        int startNodeId,
        int exactGoalNodeId,
        (float X, float Y) destination,
        out int[] route,
        out bool routeIsPartial)
    {
        route = Array.Empty<int>();
        routeIsPartial = false;

        if (exactGoalNodeId >= 0)
        {
            route = navigationGraph.FindRoute(startNodeId, exactGoalNodeId) ?? Array.Empty<int>();
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

    private static void ClearNavigationRoute(BotMemory memory)
    {
        ClearTraversalExecution(memory);
        memory.RouteNodeIds = null;
        memory.RouteIndex = 0;
        memory.RouteIsPartial = false;
        memory.RouteGoalNodeId = -1;
        memory.RouteRefreshTicks = 0;
        memory.RouteGoalX = 0f;
        memory.RouteGoalY = 0f;
        memory.NavigationGraphKey = string.Empty;
        ResetModernNavigationState(memory);
    }

    private static void ResetModernNavigationState(BotMemory memory)
    {
        memory.CurrentPointId = -1;
        memory.NextPointId = -1;
        memory.NextPoint2Id = -1;
        memory.NextPoint3Id = -1;
        memory.PreviousCurrentPointId = -1;
        memory.PreviousNextPointId = -1;
        memory.LastCommittedPointId = -1;
        memory.SecondLastCommittedPointId = -1;
        memory.NoNextPointTicks = 0;
        memory.LoopBacktrackTicks = 0;
        memory.StickyCurrentPointId = -1;
        memory.StickyNextPointId = -1;
        memory.StickyNextTicksRemaining = 0;
        memory.NavChurnCurrentPointId = -1;
        memory.NavChurnTicks = 0;
        memory.NavChurnSwitchTicks = 0;
        memory.NavChurnLockPointId = -1;
        memory.NavChurnLockTicksRemaining = 0;
        memory.NavChurnStartX = 0f;
        memory.NavChurnStartY = 0f;
        memory.ModernStuckTicks = 0;
        memory.ModernDropGapTicks = 0;
        memory.ModernPreviousTargetDistance = float.PositiveInfinity;
        memory.ReanchorTicksRemaining = 0;
        memory.SecondAnchorCooldownTicksRemaining = 0;
        memory.SecondAnchorBlockPointId = -1;
        memory.SecondAnchorBlockTicksRemaining = 0;
        memory.CaptureHoldInsideMilliseconds = 0f;
        memory.CaptureActiveGroupId = -1;
    }

    private static void BeginTraversalExecution(BotMemory memory, int fromNodeId, int toNodeId, BotNavigationEdge edge)
    {
        memory.ActiveTraversalFromNodeId = fromNodeId;
        memory.ActiveTraversalToNodeId = toNodeId;
        memory.ActiveTraversalKind = edge.Kind;
        memory.ActiveTraversalTape = edge.InputTape;
        memory.ActiveTraversalFrameIndex = 0;
        memory.ActiveTraversalFrameSecondsRemaining = edge.InputTape.Count == 0
            ? 0
            : GetFrameDurationSeconds(edge.InputTape[0]);
    }

    private static bool IsExecutingTraversal(BotMemory memory, int fromNodeId, int toNodeId)
    {
        return memory.ActiveTraversalTape is not null
            && memory.ActiveTraversalFrameIndex >= 0
            && memory.ActiveTraversalFrameIndex < memory.ActiveTraversalTape.Count
            && memory.ActiveTraversalFromNodeId == fromNodeId
            && memory.ActiveTraversalToNodeId == toNodeId;
    }

    private static bool TryConsumeTraversalExecution(
        BotMemory memory,
        double simulationTickSeconds,
        out int forcedHorizontalDirection,
        out bool forceJump)
    {
        forcedHorizontalDirection = 0;
        forceJump = false;
        if (memory.ActiveTraversalTape is null
            || memory.ActiveTraversalFrameIndex < 0
            || memory.ActiveTraversalFrameIndex >= memory.ActiveTraversalTape.Count)
        {
            return false;
        }

        var frame = memory.ActiveTraversalTape[memory.ActiveTraversalFrameIndex];
        forcedHorizontalDirection = frame.Right ? 1 : frame.Left ? -1 : 0;
        forceJump = frame.Up;

        if (memory.ActiveTraversalFrameSecondsRemaining <= 0d)
        {
            memory.ActiveTraversalFrameSecondsRemaining = GetFrameDurationSeconds(frame);
        }

        var remainingSeconds = memory.ActiveTraversalFrameSecondsRemaining - Math.Max(0d, simulationTickSeconds);
        while (remainingSeconds <= 0d)
        {
            memory.ActiveTraversalFrameIndex += 1;
            if (memory.ActiveTraversalTape is null
                || memory.ActiveTraversalFrameIndex >= memory.ActiveTraversalTape.Count)
            {
                ClearTraversalExecution(memory);
                return true;
            }

            remainingSeconds += GetFrameDurationSeconds(memory.ActiveTraversalTape[memory.ActiveTraversalFrameIndex]);
        }

        memory.ActiveTraversalFrameSecondsRemaining = remainingSeconds;
        return true;
    }

    private static void ClearTraversalExecution(BotMemory memory)
    {
        memory.ActiveTraversalFromNodeId = -1;
        memory.ActiveTraversalToNodeId = -1;
        memory.ActiveTraversalKind = BotNavigationTraversalKind.Walk;
        memory.ActiveTraversalTape = null;
        memory.ActiveTraversalFrameIndex = 0;
        memory.ActiveTraversalFrameSecondsRemaining = 0d;
    }

    private static double GetFrameDurationSeconds(BotNavigationInputFrame frame)
    {
        if (frame.DurationSeconds > 0d)
        {
            return frame.DurationSeconds;
        }

        return Math.Max(1, frame.Ticks) / (double)SimulationConfig.DefaultTicksPerSecond;
    }

    private static string GetRouteLabel(BotMemory memory, BotNavigationTraversalKind traversalKind)
    {
        var prefix = memory.RouteIsPartial ? "p" : "r";
        var step = memory.RouteIndex;
        var totalSteps = memory.RouteNodeIds is null ? 0 : Math.Max(0, memory.RouteNodeIds.Length - 1);
        return traversalKind switch
        {
            BotNavigationTraversalKind.Drop => $"{prefix}{step}/{totalSteps}:drop",
            BotNavigationTraversalKind.Jump => $"{prefix}{step}/{totalSteps}:jump",
            _ => $"{prefix}{step}/{totalSteps}",
        };
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

    private static ModernPathSelection ResolveModernPathSelection(
        SimulationWorld world,
        PlayerEntity player,
        PlayerTeam team,
        IReadOnlyList<PlayerEntity> allPlayers,
        PlayerEntity? medicBuddyTarget)
    {
        var baseSelection = world.MatchRules.Mode switch
        {
            GameModeKind.CaptureTheFlag => ResolveModernCaptureTheFlagPath(world, player, team, allPlayers),
            GameModeKind.ControlPoint => ResolveModernControlPointPath(world, player, team),
            GameModeKind.Generator => ResolveModernGeneratorPath(world, team),
            GameModeKind.Arena => ResolveModernArenaPath(world, player),
            GameModeKind.KingOfTheHill => ResolveModernKothPath(world, player),
            GameModeKind.DoubleKingOfTheHill => ResolveModernKothPath(world, player),
            _ => new ModernPathSelection((player.X, player.Y), AllowDirectPath: true),
        };

        if (player.ClassId == PlayerClass.Medic && medicBuddyTarget is not null)
        {
            return new ModernPathSelection((medicBuddyTarget.X, medicBuddyTarget.Y), AllowDirectPath: true);
        }

        return baseSelection;
    }

    private static ModernPathSelection ResolveModernCaptureTheFlagPath(
        SimulationWorld world,
        PlayerEntity player,
        PlayerTeam team,
        IReadOnlyList<PlayerEntity> allPlayers)
    {
        if (player.IsCarryingIntel)
        {
            return new ModernPathSelection(ResolveTeamAnchor(world, team, preferObjective: false), AllowDirectPath: false);
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
                    captureObjective.TargetPoint,
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
                    captureObjective.TargetPoint,
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
        ControlPointState? bestPoint = null;
        var bestDistanceSquared = float.PositiveInfinity;
        for (var index = 0; index < world.ControlPoints.Count; index += 1)
        {
            var point = world.ControlPoints[index];
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
                    captureObjective.TargetPoint,
                    AllowDirectPath: false,
                    IsCaptureObjective: true,
                    CaptureObjective: captureObjective);
            }

            return new ModernPathSelection((bestPoint.Marker.CenterX, bestPoint.Marker.CenterY), AllowDirectPath: false, IsCaptureObjective: true);
        }

        return new ModernPathSelection((player.X, player.Y), AllowDirectPath: true);
    }

    private static bool TryResolveModernCaptureObjective(
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

        captureObjective = new ModernCaptureObjective(
            (zoneCandidates[targetZoneIndex].X, zoneCandidates[targetZoneIndex].Y),
            ((selectedGroup.MinX + selectedGroup.MaxX) * 0.5f, (selectedGroup.MinY + selectedGroup.MaxY) * 0.5f),
            clusterMinX,
            clusterMaxX,
            clusterMinY,
            clusterMaxY,
            groupedPoints,
            groups);
        return true;
    }

    private static ModernCaptureHoldState ResolveModernCaptureHoldState(
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
            memory.CaptureActiveGroupId = -1;
            return new ModernCaptureHoldState(SuppressNavigation: false, ActiveGroupId: -1);
        }

        var deltaMilliseconds = (float)(fixedDeltaSeconds * 1000.0);
        if (deltaMilliseconds <= 0f || deltaMilliseconds > 200f)
        {
            deltaMilliseconds = 16f;
        }

        var insideZoneSquare = false;
        var activeGroupId = -1;
        if (player.X >= captureObjective.MinX - ModernCaptureZoneSquareHalfSize
            && player.X <= captureObjective.MaxX + ModernCaptureZoneSquareHalfSize
            && player.Y >= captureObjective.MinY - ModernCaptureZoneSquareHalfSize
            && player.Y <= captureObjective.MaxY + ModernCaptureZoneSquareHalfSize)
        {
            for (var pointIndex = 0; pointIndex < captureObjective.Points.Length; pointIndex += 1)
            {
                var point = captureObjective.Points[pointIndex];
                if (MathF.Abs(player.X - point.X) > ModernCaptureZoneSquareHalfSize
                    || MathF.Abs(player.Y - point.Y) > ModernCaptureZoneSquareHalfSize)
                {
                    continue;
                }

                insideZoneSquare = true;
                activeGroupId = point.GroupId;
                break;
            }
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

        if (insideZoneSquare)
        {
            memory.CaptureHoldInsideMilliseconds = MathF.Min(holdBufferMilliseconds, memory.CaptureHoldInsideMilliseconds + deltaMilliseconds);
        }
        else
        {
            memory.CaptureHoldInsideMilliseconds = MathF.Max(0f, memory.CaptureHoldInsideMilliseconds - (deltaMilliseconds * 1.5f));
        }

        memory.CaptureActiveGroupId = activeGroupId;
        return new ModernCaptureHoldState(
            SuppressNavigation: insideZoneSquare && memory.CaptureHoldInsideMilliseconds >= holdBufferMilliseconds,
            ActiveGroupId: activeGroupId);
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
                || candidate.ClassId == PlayerClass.Medic
                || (candidate.ClassId == PlayerClass.Spy && candidate.IsSpyCloaked)
                || ShouldIgnoreEnemyTarget(candidate))
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
        BotMemory memory,
        BotTimingProfile timing)
    {
        if (memory.UnstickTicks > 0)
        {
            return memory.UnstickDirection;
        }

        if (navigationGraph is not null
            && navigationGraph.BuildStrategy == BotNavigationBuildStrategy.ModernClientBotPointGraph)
        {
            return ResolveModernHorizontalMovement(
                world,
                player,
                destination,
                objectiveSelection,
                navigationDecision,
                navigationGraph,
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
        BotNavigationRuntimeGraph navigationGraph,
        BotMemory memory,
        PlayerEntity? enemyTarget)
    {
        var hasGroundContact = HasModernGroundContact(world, player);
        if (navigationDecision.ForcedHorizontalDirection != 0)
        {
            return navigationDecision.ForcedHorizontalDirection;
        }

        if (navigationDecision.CaptureHoldActive
            && objectiveSelection.CaptureObjective is ModernCaptureObjective captureObjective)
        {
            var enemyNearbyCapture = HasModernCaptureEnemyNearby(world, player, enemyTarget);
            var captureDx = MathF.Abs(player.X - captureObjective.TargetPoint.X);
            var captureDy = MathF.Abs(player.Bottom - captureObjective.TargetPoint.Y);
            if (hasGroundContact
                && captureDx < ModernCaptureBrakeTargetDistanceX
                && captureDy < ModernCaptureBrakeTargetDistanceY
                && !enemyNearbyCapture)
            {
                var captureHorizontalSpeed = player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond;
                var velocitySign = MathF.Sign(captureHorizontalSpeed);
                var targetSign = MathF.Sign(player.X - captureObjective.TargetPoint.X);
                if (MathF.Abs(captureHorizontalSpeed) > 1.1f
                    && velocitySign != 0f
                    && velocitySign == targetSign)
                {
                    memory.DoublebackActive = false;
                    return -(int)velocitySign;
                }

                memory.DoublebackActive = false;
                return 0;
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
                return 0;
            }

            if (memory.DoublebackActive)
            {
                if (hasGroundContact)
                {
                    memory.DoublebackActive = false;
                }

                return 0;
            }

            return -1;
        }

        if (player.X + xTolerance < destination.X
            || (player.X < destination.X
                && (MathF.Abs(horizontalSpeed) < 0.05f || (feetY - 1f) > targetY)))
        {
            if (horizontalSpeed < -1f && (feetY - 3f) >= targetY)
            {
                memory.DoublebackActive = true;
                return 0;
            }

            if (memory.DoublebackActive)
            {
                if (hasGroundContact)
                {
                    memory.DoublebackActive = false;
                }

                return 0;
            }

            return 1;
        }

        memory.DoublebackActive = false;
        if (TryGetModernNextNode(navigationGraph, memory, out var nextNode))
        {
            CommitModernReachedDestination(memory);
            if (horizontalSpeed > 0f && player.X < nextNode.X)
            {
                return 1;
            }

            if (horizontalSpeed < 0f && player.X > nextNode.X)
            {
                return -1;
            }
        }
        else if (!navigationDecision.CaptureHoldActive)
        {
            memory.CurrentPointId = -1;
        }

        return 0;
    }

    private static void CommitModernReachedDestination(BotMemory memory)
    {
        if (memory.NextPointId < 0)
        {
            return;
        }

        memory.SecondLastCommittedPointId = memory.LastCommittedPointId;
        memory.LastCommittedPointId = memory.CurrentPointId;
        memory.CurrentPointId = memory.NextPointId;
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
        BotMemory memory,
        BotTimingProfile timing)
    {
        if (navigationGraph is not null
            && navigationGraph.BuildStrategy == BotNavigationBuildStrategy.ModernClientBotPointGraph)
        {
            return ResolveModernJump(
                world,
                player,
                destination,
                horizontal,
                navigationDecision,
                navigationGraph,
                memory,
                timing);
        }

        if (navigationDecision.ForceJump)
        {
            memory.JumpCooldownTicks = timing.JumpCooldownTicks;
            return true;
        }

        if (memory.JumpCooldownTicks > 0)
        {
            return false;
        }

        if (memory.UnstickTicks > 0 && player.IsGrounded)
        {
            memory.JumpCooldownTicks = timing.JumpCooldownTicks;
            return true;
        }

        if (navigationDecision.TraversalKind == BotNavigationTraversalKind.Jump
            && player.IsGrounded)
        {
            memory.JumpCooldownTicks = timing.JumpCooldownTicks;
            return true;
        }

        if (navigationDecision.LocksMovement)
        {
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
            && (!isPureRouteMovement || navigationDecision.TraversalKind == BotNavigationTraversalKind.Jump))
        {
            memory.JumpCooldownTicks = timing.JumpCooldownTicks;
            return true;
        }

        if (horizontal != 0 && WouldMoveIntoObstacle(world, player, horizontal))
        {
            memory.JumpCooldownTicks = timing.JumpCooldownTicks;
            return true;
        }

        if (navigationDecision.ForcedHorizontalDirection != 0
            && !player.IsGrounded
            && MathF.Abs(destination.Y - player.Y) > 28f)
        {
            return false;
        }

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
        var activeSpyIds = new HashSet<int>();
        for (var index = 0; index < allPlayers.Count; index += 1)
        {
            var player = allPlayers[index];
            if (player.ClassId != PlayerClass.Spy)
            {
                continue;
            }

            activeSpyIds.Add(player.Id);
            _modernSpyVisibleTicksByPlayerId.TryGetValue(player.Id, out var visibleTicks);
            var isVisibleToBots = player.SpyCloakAlpha > 0.2f || player.IsSpyBackstabAnimating || !player.IsSpyCloaked;
            visibleTicks = isVisibleToBots
                ? Math.Min(90, visibleTicks + 1)
                : Math.Max(0, visibleTicks - 1);
            _modernSpyVisibleTicksByPlayerId[player.Id] = visibleTicks;
        }

        var staleSpyIds = new List<int>();
        foreach (var entry in _modernSpyVisibleTicksByPlayerId)
        {
            if (!activeSpyIds.Contains(entry.Key))
            {
                staleSpyIds.Add(entry.Key);
            }
        }

        for (var index = 0; index < staleSpyIds.Count; index += 1)
        {
            _modernSpyVisibleTicksByPlayerId.Remove(staleSpyIds[index]);
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

    private static void ApplyModernReloadDiscipline(PlayerEntity player, BotMemory memory, ref bool firePrimary, ref bool fireSecondary)
    {
        if (memory.ModernReloadCounterTicks <= 0)
        {
            return;
        }

        if (player.ClassId == PlayerClass.Medic)
        {
            fireSecondary = false;
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
        BotNavigationRuntimeGraph navigationGraph,
        BotMemory memory,
        BotTimingProfile timing)
    {
        if (navigationDecision.ForceJump)
        {
            return TriggerBotJump(memory, timing);
        }

        if (memory.JumpCooldownTicks > 0)
        {
            return false;
        }

        var hasGroundContact = HasModernGroundContact(world, player);
        if (memory.UnstickTicks > 0 && hasGroundContact)
        {
            return TriggerBotJump(memory, timing);
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
                return TriggerBotJump(memory, timing);
            }

            if (horizontal != 0
                && TryGetModernCurrentNode(navigationGraph, memory, out var currentNode)
                && TryGetModernNextNode(navigationGraph, memory, out var nextNode)
                && TryShouldUseModernInterpolationJump(
                    world,
                    player,
                    currentNode.X,
                    GetModernNodeFeetY(navigationGraph, currentNode),
                    nextNode.X,
                    GetModernNodeFeetY(navigationGraph, nextNode),
                    previousX,
                    previousY,
                    jumpHeight))
            {
                return TriggerBotJump(memory, timing);
            }

            if (horizontal != 0
                && TryShouldUseModernLipJump(world, player, destination, targetY, horizontal, modernStuckTicks, jumpHeight))
            {
                return TriggerBotJump(memory, timing);
            }

            if (horizontal != 0
                && TryShouldUseModernSlopeJump(world, player, destination, targetY, horizontal, jumpRange, jumpHeight, jumpHeightTotal))
            {
                return TriggerBotJump(memory, timing);
            }

            if (horizontal != 0
                && TryShouldUseModernHallJump(world, player, destination, targetY, horizontal, jumpRange, jumpHeight))
            {
                return TriggerBotJump(memory, timing);
            }

            if (horizontal != 0 && HasModernWaistObstacleAhead(world, player, horizontal))
            {
                if (MathF.Sign(horizontalSpeed) == 0f || MathF.Abs(MathF.Sign(horizontal) - MathF.Sign(horizontalSpeed)) < 2f)
                {
                    if (!IsModernStairStepAhead(world, player, horizontal, horizontalSpeed))
                    {
                        return TriggerBotJump(memory, timing);
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
                        return TriggerBotJump(memory, timing);
                    }
                }
                else if (MathF.Abs(targetY - feetY) <= 18f
                    && !LineHitsSolid(world, player.X + horizontalSpeed + (2f * horizontal), feetY, destination.X - (3f * horizontal), targetY)
                    && jumpDistanceNeeded <= jumpRange
                    && (MathF.Abs(horizontalSpeed) <= 0.25f || MathF.Sign(horizontalSpeed) == horizontal))
                {
                    return TriggerBotJump(memory, timing);
                }
            }

            var jumpLength = horizontalSpeed * player.JumpStrength;
            if (LineHitsSolid(world, player.X + jumpLength, feetY - 18f, player.X + jumpLength, feetY - (jumpHeight + 4f))
                && !PointHitsSolid(world.Level, player.X + jumpLength, feetY - (jumpHeight + 12f))
                && targetY < player.Y
                && horizontal != 0
                && MathF.Abs(MathF.Sign(horizontal) - MathF.Sign(horizontalSpeed)) < 2f
                && !IsModernStairStepAhead(world, player, horizontal, horizontalSpeed))
            {
                return TriggerBotJump(memory, timing);
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
                return TriggerBotJump(memory, timing);
            }
        }

        return false;
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
        if (LineHitsSolid(world, gunX, gunY, probeX, probeY))
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
            && HasModernJumpHeadClear(world, player, jumpHeight)
            && HasModernLeadingJumpHeadClear(world, player, horizontal, jumpHeight);
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
            && !LineHitsSolid(world, player.X, feetY - 4f, player.X + (24f * horizontal), feetY - 18f);
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
            || !HasModernJumpHeadClear(world, player, jumpHeight)
            || !HasModernLeadingJumpHeadClear(world, player, horizontal, jumpHeight))
        {
            return false;
        }

        var probeX = gunX + LengthDirX(42f, directionToTarget);
        var probeY = gunY + LengthDirY(42f, directionToTarget);
        return !LineHitsSolid(world, gunX, gunY, probeX, probeY);
    }

    private static bool HasModernJumpHeadClear(SimulationWorld world, PlayerEntity player, float jumpHeight)
    {
        var feetY = player.Bottom;
        return !LineHitsSolid(world, player.X, feetY - 2f, player.X, feetY - (jumpHeight + 2f));
    }

    private static bool HasModernLeadingJumpHeadClear(
        SimulationWorld world,
        PlayerEntity player,
        int horizontal,
        float jumpHeight)
    {
        var feetY = player.Bottom;
        var probeX = horizontal switch
        {
            > 0 => player.Right - 1f,
            < 0 => player.Left + 1f,
            _ => player.X,
        };
        return !LineHitsSolid(world, probeX, feetY - 2f, probeX, feetY - (jumpHeight + 2f));
    }

    private static bool HasModernGroundContact(SimulationWorld world, PlayerEntity player)
    {
        var feetY = player.Bottom;
        return LineHitsSolid(world, player.X - 6f, feetY + 3f, player.X + 6f, feetY + 3f);
    }

    private static bool HasModernForwardFootBlock(SimulationWorld world, PlayerEntity player, int horizontal, float probeDistance)
    {
        var feetY = player.Bottom;
        var probeX = player.X + (probeDistance * horizontal);
        return LineHitsSolid(world, probeX, feetY - 1f, probeX, feetY + 4f);
    }

    private static bool HasModernGroundAhead(SimulationWorld world, PlayerEntity player, int horizontal, float probeDistance)
    {
        var feetY = player.Bottom;
        var probeX = player.X + (probeDistance * horizontal);
        return LineHitsSolid(world, probeX, feetY, probeX, feetY + 12f);
    }

    private static bool HasModernWaistObstacleAhead(SimulationWorld world, PlayerEntity player, int horizontal)
    {
        var probeX = player.X + (15f * horizontal);
        var legacyProbeTop = player.Y - player.CollisionBottomOffset + 6f;
        return LineHitsSolid(world, probeX, legacyProbeTop, probeX, player.Bottom - 12f);
    }

    private static bool IsModernStairStepAhead(SimulationWorld world, PlayerEntity player, int horizontal, float horizontalSpeed)
    {
        if (horizontal == 0 || MathF.Abs(horizontalSpeed) <= 0.0001f)
        {
            return false;
        }

        var stepDirection = MathF.Sign(horizontalSpeed);
        var x = player.X;
        while (!PointHitsSolid(world.Level, x, player.Bottom - 1f) && MathF.Abs(player.X - x) < 60f)
        {
            x += stepDirection;
        }

        return PointHitsSolid(world.Level, x + (5f * stepDirection), player.Bottom - 7f)
            || PointHitsSolid(world.Level, x + (11f * stepDirection), player.Bottom - 13f);
    }

    private static bool PointHitsSolid(SimpleLevel level, float x, float y) => GetModernObstacleIndex(level).ContainsPoint(x, y);

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
            UnstickTicks: 0);
    }

    private static BotControllerDiagnosticsEntry CreateDiagnosticsEntry(
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
            memory.UnstickTicks);
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

        if (memory.NavChurnLockTicksRemaining > 0)
        {
            memory.NavChurnLockTicksRemaining -= 1;
            if (memory.NavChurnLockTicksRemaining <= 0)
            {
                memory.NavChurnLockPointId = -1;
                memory.NavChurnLockTicksRemaining = 0;
            }
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
        float originX,
        float originY,
        float targetX,
        float targetY)
    {
        var distance = DistanceBetween(originX, originY, targetX, targetY);
        if (distance <= 0.0001f)
        {
            return GetModernObstacleIndex(world.Level).ContainsPoint(originX, originY);
        }

        var directionX = (targetX - originX) / distance;
        var directionY = (targetY - originY) / distance;
        return GetModernObstacleIndex(world.Level).IntersectsAny(originX, originY, targetX, targetY, directionX, directionY, distance);
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
            return !GetModernObstacleIndex(world.Level).ContainsPoint(originX, originY);
        }

        var directionX = (targetX - originX) / distance;
        var directionY = (targetY - originY) / distance;
        return !GetModernObstacleIndex(world.Level).IntersectsAny(originX, originY, targetX, targetY, directionX, directionY, distance);
    }

    private static ModernObstacleIndex GetModernObstacleIndex(SimpleLevel level)
    {
        return ModernObstacleIndexByLevel.GetValue(level, static currentLevel => new ModernObstacleIndex(ModernObstacleGeometry.BuildStaticObstacles(currentLevel)));
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

    private void PruneMemory(IEnumerable<byte> activeSlots)
    {
        var activeSet = activeSlots.ToHashSet();
        var staleSlots = new List<byte>();
        foreach (var entry in _memoryBySlot)
        {
            if (!activeSet.Contains(entry.Key))
            {
                staleSlots.Add(entry.Key);
            }
        }

        for (var index = 0; index < staleSlots.Count; index += 1)
        {
            _memoryBySlot.Remove(staleSlots[index]);
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

    private sealed class BotMemory
    {
        public bool HasObservedPosition { get; set; }

        public float LastObservedX { get; set; }

        public float LastObservedY { get; set; }

        public float PreviousObservedX { get; set; }

        public float PreviousObservedY { get; set; }

        public int LastRequestedHorizontal { get; set; }

        public bool DoublebackActive { get; set; }

        public int StuckTicks { get; set; }

        public int UnstickTicks { get; set; }

        public int UnstickDirection { get; set; } = 1;

        public int JumpCooldownTicks { get; set; }

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

        public int ReanchorTicksRemaining { get; set; }

        public int SecondAnchorCooldownTicksRemaining { get; set; }

        public int SecondAnchorBlockPointId { get; set; } = -1;

        public int SecondAnchorBlockTicksRemaining { get; set; }

        public float CaptureHoldInsideMilliseconds { get; set; }

        public int CaptureActiveGroupId { get; set; } = -1;

        public int[]? RouteNodeIds { get; set; }

        public int RouteIndex { get; set; }

        public bool RouteIsPartial { get; set; }

        public int RouteGoalNodeId { get; set; } = -1;

        public int RouteRefreshTicks { get; set; }

        public float RouteGoalX { get; set; }

        public float RouteGoalY { get; set; }

        public string NavigationGraphKey { get; set; } = string.Empty;

        public int ActiveTraversalFromNodeId { get; set; } = -1;

        public int ActiveTraversalToNodeId { get; set; } = -1;

        public BotNavigationTraversalKind ActiveTraversalKind { get; set; } = BotNavigationTraversalKind.Walk;

        public IReadOnlyList<BotNavigationInputFrame>? ActiveTraversalTape { get; set; }

        public int ActiveTraversalFrameIndex { get; set; }

        public double ActiveTraversalFrameSecondsRemaining { get; set; }
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

    private readonly record struct ModernPathSelection(
        (float X, float Y) Destination,
        bool AllowDirectPath,
        bool IsCaptureObjective = false,
        ModernCaptureObjective? CaptureObjective = null);

    private readonly record struct ModernCaptureObjective(
        (float X, float Y) TargetZone,
        (float X, float Y) TargetPoint,
        float MinX,
        float MaxX,
        float MinY,
        float MaxY,
        ModernCapturePoint[] Points,
        ModernCaptureGroup[] Groups);

    private readonly record struct ModernCapturePoint(
        float X,
        float Y,
        int GroupId);

    private readonly record struct ModernCaptureGroup(
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
        int ActiveGroupId);

    private readonly record struct NavigationDecision(
        (float X, float Y) MovementTarget,
        bool HasRoute,
        int ForcedHorizontalDirection,
        bool ForceJump,
        bool LocksMovement,
        string Label,
        BotNavigationTraversalKind TraversalKind = BotNavigationTraversalKind.Walk,
        bool MovementTargetUsesFeetCoordinates = false,
        bool CaptureHoldActive = false);

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

    private readonly record struct ControlledPlayerState(ControlledBotSlot ControlledSlot, PlayerEntity Player);
}
