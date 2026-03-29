using OpenGarrison.Core;

namespace OpenGarrison.BotAI;

public sealed class BotController
{
    private const float ObjectiveArrivalDistance = 56f;
    private const float PatrolDistance = 36f;
    private const float MovementDeadZone = 18f;
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
    private const float RouteRepathDistance = 120f;
    private const float RouteStartNodeSearchDistance = 144f;
    private const float RouteGoalNodeSearchDistance = 220f;
    private const float StuckMoveDistanceSquared = 9f;
    private const int StuckTickThreshold = 24;
    private const int UnstickTicksDefault = 16;
    private const int JumpCooldownTicksDefault = 16;
    private const int StrafeTicksMin = 18;
    private const int StrafeTicksMax = 42;
    private const int RouteRefreshTicksDefault = 20;
    private const float LowHealthRetreatFraction = 0.3f;

    private readonly Dictionary<byte, BotMemory> _memoryBySlot = new();
    private readonly Dictionary<string, BotNavigationRuntimeGraph> _navigationGraphsByKey = new(StringComparer.Ordinal);
    private readonly Random _random = new(1337);

    public bool CollectDiagnostics { get; set; }

    public BotControllerDiagnosticsSnapshot LastDiagnostics { get; private set; } = BotControllerDiagnosticsSnapshot.Empty;

    public void Reset()
    {
        _memoryBySlot.Clear();
        LastDiagnostics = BotControllerDiagnosticsSnapshot.Empty;
    }

    public IReadOnlyDictionary<byte, PlayerInputSnapshot> BuildInputs(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots,
        IReadOnlyDictionary<BotNavigationProfile, BotNavigationAsset>? navigationAssets = null)
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
        var navigationGraphsByProfile = BuildNavigationGraphs(navigationAssets);
        var rolesBySlot = AssignRoles(world, controlledPlayers);
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
            TickMemory(memory, player);

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

            var role = rolesBySlot.GetValueOrDefault(slot, BotRole.AttackObjective);
            inputs[slot] = BuildInputForBot(
                world,
                entry.Value.ControlledSlot,
                player,
                allPlayers,
                navigationGraphsByProfile.GetValueOrDefault(BotNavigationProfiles.GetProfileForClass(entry.Value.ControlledSlot.ClassId)),
                role,
                memory,
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
        out BotControllerDiagnosticsEntry diagnosticsEntry)
    {
        var healTarget = FindBestHealTarget(world, player, controlledSlot.Team, allPlayers, memory);
        var isSeekingCabinet = TryGetHealingCabinetDestination(world, player, out var cabinetDestination);
        var destination = isSeekingCabinet
            ? cabinetDestination
            : ResolveDestination(world, player, controlledSlot.Team, role, healTarget);
        var navigationDecision = ResolveNavigationDecision(player, controlledSlot.ClassId, destination, navigationGraph, memory);
        var movementDestination = navigationDecision.MovementTarget;
        var enemyTarget = FindBestEnemyTarget(world, player, controlledSlot.Team, role, destination, allPlayers, memory);
        var hasVisibleEnemy = enemyTarget is not null && HasCombatLineOfSight(world, player, enemyTarget);

        var aimTarget = ResolveAimTarget(player, movementDestination, enemyTarget, healTarget);
        var horizontal = ResolveHorizontalMovement(
            world,
            player,
            movementDestination,
            enemyTarget,
            healTarget,
            hasVisibleEnemy,
            navigationDecision,
            memory);
        var jump = ResolveJump(
            world,
            player,
            movementDestination,
            enemyTarget,
            healTarget,
            horizontal,
            hasVisibleEnemy,
            navigationDecision,
            memory);
        var firePrimary = ResolvePrimaryFire(world, player, enemyTarget, healTarget);
        var fireSecondary = ResolveSecondaryFire(player, healTarget);

        memory.LastRequestedHorizontal = horizontal;
        diagnosticsEntry = CreateDiagnosticsEntry(
            world,
            controlledSlot,
            player,
            role,
            memory,
            healTarget,
            enemyTarget,
            hasVisibleEnemy,
            isSeekingCabinet,
            movementDestination,
            navigationDecision);

        return new PlayerInputSnapshot(
            Left: horizontal < 0,
            Right: horizontal > 0,
            Up: jump,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: firePrimary,
            FireSecondary: fireSecondary,
            AimWorldX: aimTarget.X,
            AimWorldY: aimTarget.Y,
            DebugKill: false);
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

    private static Dictionary<byte, BotRole> AssignRoles(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledPlayerState> controlledPlayers)
    {
        var roles = new Dictionary<byte, BotRole>();
        if (controlledPlayers.Count == 0)
        {
            return roles;
        }

        foreach (var teamGroup in controlledPlayers.Values
                     .GroupBy(static state => state.ControlledSlot.Team)
                     .Select(static group => group.OrderBy(state => state.ControlledSlot.Slot).ToArray()))
        {
            AssignTeamRoles(world, teamGroup, roles);
        }

        return roles;
    }

    private static void AssignTeamRoles(
        SimulationWorld world,
        IReadOnlyList<ControlledPlayerState> teamPlayers,
        Dictionary<byte, BotRole> roles)
    {
        if (teamPlayers.Count == 0)
        {
            return;
        }

        var team = teamPlayers[0].ControlledSlot.Team;
        var allyCarrier = FindCarrier(teamPlayers.Select(static player => player.Player));
        var enemyCarrier = FindEnemyCarrier(world, team);

        for (var index = 0; index < teamPlayers.Count; index += 1)
        {
            var state = teamPlayers[index];
            var slot = state.ControlledSlot.Slot;
            if (state.Player.IsCarryingIntel)
            {
                roles[slot] = BotRole.ReturnWithIntel;
                continue;
            }

            if (enemyCarrier is not null && index == 0)
            {
                roles[slot] = BotRole.HuntCarrier;
                continue;
            }

            if (allyCarrier is not null && !ReferenceEquals(allyCarrier, state.Player) && index == 0)
            {
                roles[slot] = BotRole.EscortCarrier;
                continue;
            }

            roles[slot] = world.MatchRules.Mode switch
            {
                GameModeKind.Arena => BotRole.ContestArena,
                GameModeKind.CaptureTheFlag when teamPlayers.Count >= 3 && index == teamPlayers.Count - 1 => BotRole.DefendObjective,
                GameModeKind.ControlPoint when teamPlayers.Count >= 3 && index == teamPlayers.Count - 1 => BotRole.DefendObjective,
                GameModeKind.Generator when teamPlayers.Count >= 3 && index == teamPlayers.Count - 1 => BotRole.DefendObjective,
                _ => BotRole.AttackObjective,
            };
        }
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

    private Dictionary<BotNavigationProfile, BotNavigationRuntimeGraph> BuildNavigationGraphs(
        IReadOnlyDictionary<BotNavigationProfile, BotNavigationAsset>? navigationAssets)
    {
        var graphs = new Dictionary<BotNavigationProfile, BotNavigationRuntimeGraph>();
        if (navigationAssets is null || navigationAssets.Count == 0)
        {
            return graphs;
        }

        foreach (var entry in navigationAssets)
        {
            var asset = entry.Value;
            var cacheKey = $"{asset.LevelName}|{asset.MapAreaIndex}|{asset.Profile}|{asset.LevelFingerprint}";
            if (!_navigationGraphsByKey.TryGetValue(cacheKey, out var graph))
            {
                graph = new BotNavigationRuntimeGraph(asset);
                _navigationGraphsByKey[cacheKey] = graph;
            }

            graphs[entry.Key] = graph;
        }

        return graphs;
    }

    private NavigationDecision ResolveNavigationDecision(
        PlayerEntity player,
        PlayerClass classId,
        (float X, float Y) destination,
        BotNavigationRuntimeGraph? navigationGraph,
        BotMemory memory)
    {
        if (navigationGraph is null)
        {
            ClearNavigationRoute(memory);
            return new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "direct");
        }

        if (!navigationGraph.TryFindNearestNode(destination.X, destination.Y, RouteGoalNodeSearchDistance, out var goalNode))
        {
            ClearNavigationRoute(memory);
            return new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "goal-miss");
        }

        if (!navigationGraph.TryFindNearestNode(player.X, player.Y, RouteStartNodeSearchDistance, out var startNode))
        {
            ClearNavigationRoute(memory);
            return new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "start-miss");
        }

        var goalMoved = DistanceSquared(destination.X, destination.Y, memory.RouteGoalX, memory.RouteGoalY)
            > RouteNodeArrivalDistance * RouteNodeArrivalDistance;
        if (memory.RouteRefreshTicks > 0)
        {
            memory.RouteRefreshTicks -= 1;
        }

        var requiresRepath = memory.RouteNodeIds is null
            || memory.RouteNodeIds.Length == 0
            || !string.Equals(memory.NavigationGraphKey, navigationGraph.CacheKey, StringComparison.Ordinal)
            || memory.RouteGoalNodeId != goalNode.Id
            || goalMoved
            || memory.RouteRefreshTicks <= 0
            || !TryGetCurrentRouteNode(navigationGraph, memory, out var currentRouteNode)
            || DistanceSquared(player.X, player.Y, currentRouteNode.X, currentRouteNode.Y) > RouteRepathDistance * RouteRepathDistance;

        if (requiresRepath)
        {
            var route = navigationGraph.FindRoute(startNode.Id, goalNode.Id);
            if (route is null || route.Length <= 1)
            {
                ClearNavigationRoute(memory);
                memory.NavigationGraphKey = navigationGraph.CacheKey;
                memory.RouteGoalNodeId = goalNode.Id;
                memory.RouteGoalX = destination.X;
                memory.RouteGoalY = destination.Y;
                memory.RouteRefreshTicks = RouteRefreshTicksDefault;
                return new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "route-miss");
            }

            memory.NavigationGraphKey = navigationGraph.CacheKey;
            memory.RouteNodeIds = route;
            memory.RouteIndex = 1;
            memory.RouteGoalNodeId = goalNode.Id;
            memory.RouteGoalX = destination.X;
            memory.RouteGoalY = destination.Y;
            memory.RouteRefreshTicks = RouteRefreshTicksDefault;
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

        if (TryResolveTraversalDecision(player, destination, navigationGraph, memory, previousNodeId, nextNode, edge, out var traversalDecision))
        {
            return traversalDecision;
        }

        return new NavigationDecision((nextNode.X, nextNode.Y), HasRoute: true, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: GetRouteLabel(memory, edge.Kind));
    }

    private static bool TryResolveTraversalDecision(
        PlayerEntity player,
        (float X, float Y) destination,
        BotNavigationRuntimeGraph navigationGraph,
        BotMemory memory,
        int previousNodeId,
        BotNavigationNode nextNode,
        BotNavigationEdge edge,
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

            if (DistanceSquared(player.X, player.Y, sourceNode.X, sourceNode.Y) > RouteNodeArrivalDistance * RouteNodeArrivalDistance)
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

            if (TryConsumeTraversalExecution(memory, out var forcedHorizontalDirection, out var forceJump))
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
            && DistanceSquared(player.X, player.Y, currentNode.X, currentNode.Y) <= RouteNodeArrivalDistance * RouteNodeArrivalDistance)
        {
            memory.RouteIndex += 1;
        }
    }

    private static bool TryGetCurrentRouteNode(BotNavigationRuntimeGraph navigationGraph, BotMemory memory, out BotNavigationNode node)
    {
        node = default!;
        return memory.RouteNodeIds is not null
            && memory.RouteIndex >= 0
            && memory.RouteIndex < memory.RouteNodeIds.Length
            && navigationGraph.TryGetNode(memory.RouteNodeIds[memory.RouteIndex], out node);
    }

    private static void ClearNavigationRoute(BotMemory memory)
    {
        ClearTraversalExecution(memory);
        memory.RouteNodeIds = null;
        memory.RouteIndex = 0;
        memory.RouteGoalNodeId = -1;
        memory.RouteRefreshTicks = 0;
        memory.RouteGoalX = 0f;
        memory.RouteGoalY = 0f;
        memory.NavigationGraphKey = string.Empty;
    }

    private static void BeginTraversalExecution(BotMemory memory, int fromNodeId, int toNodeId, BotNavigationEdge edge)
    {
        memory.ActiveTraversalFromNodeId = fromNodeId;
        memory.ActiveTraversalToNodeId = toNodeId;
        memory.ActiveTraversalKind = edge.Kind;
        memory.ActiveTraversalTape = edge.InputTape;
        memory.ActiveTraversalFrameIndex = 0;
        memory.ActiveTraversalFrameTicksRemaining = edge.InputTape.Count == 0
            ? 0
            : Math.Max(1, edge.InputTape[0].Ticks);
    }

    private static bool IsExecutingTraversal(BotMemory memory, int fromNodeId, int toNodeId)
    {
        return memory.ActiveTraversalTape is not null
            && memory.ActiveTraversalFrameIndex >= 0
            && memory.ActiveTraversalFrameIndex < memory.ActiveTraversalTape.Count
            && memory.ActiveTraversalFromNodeId == fromNodeId
            && memory.ActiveTraversalToNodeId == toNodeId;
    }

    private static bool TryConsumeTraversalExecution(BotMemory memory, out int forcedHorizontalDirection, out bool forceJump)
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

        memory.ActiveTraversalFrameTicksRemaining -= 1;
        if (memory.ActiveTraversalFrameTicksRemaining > 0)
        {
            return true;
        }

        memory.ActiveTraversalFrameIndex += 1;
        if (memory.ActiveTraversalTape is null
            || memory.ActiveTraversalFrameIndex >= memory.ActiveTraversalTape.Count)
        {
            ClearTraversalExecution(memory);
            return true;
        }

        memory.ActiveTraversalFrameTicksRemaining = Math.Max(1, memory.ActiveTraversalTape[memory.ActiveTraversalFrameIndex].Ticks);
        return true;
    }

    private static void ClearTraversalExecution(BotMemory memory)
    {
        memory.ActiveTraversalFromNodeId = -1;
        memory.ActiveTraversalToNodeId = -1;
        memory.ActiveTraversalKind = BotNavigationTraversalKind.Walk;
        memory.ActiveTraversalTape = null;
        memory.ActiveTraversalFrameIndex = 0;
        memory.ActiveTraversalFrameTicksRemaining = 0;
    }

    private static string GetRouteLabel(BotMemory memory, BotNavigationTraversalKind traversalKind)
    {
        var step = memory.RouteIndex;
        var totalSteps = memory.RouteNodeIds is null ? 0 : Math.Max(0, memory.RouteNodeIds.Length - 1);
        return traversalKind switch
        {
            BotNavigationTraversalKind.Drop => $"r{step}/{totalSteps}:drop",
            BotNavigationTraversalKind.Jump => $"r{step}/{totalSteps}:jump",
            _ => $"r{step}/{totalSteps}",
        };
    }

    private (float X, float Y) ResolveDestination(
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
        BotMemory memory)
    {
        var stickyTarget = TryResolveStickyTarget(allPlayers, GetOpposingTeam(team), memory.TargetPlayerId);
        if (stickyTarget is not null && ShouldKeepStickyTarget(world, player, stickyTarget, memory))
        {
            memory.TargetLockTicksRemaining = Math.Max(memory.TargetLockTicksRemaining, 12);
            return stickyTarget;
        }

        PlayerEntity? bestTarget = null;
        var bestScore = float.MaxValue;

        for (var index = 0; index < allPlayers.Count; index += 1)
        {
            var candidate = allPlayers[index];
            if (!candidate.IsAlive || candidate.Team == team)
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
        memory.TargetLockTicksRemaining = bestTarget is null ? 0 : 18;
        return bestTarget;
    }

    private PlayerEntity? FindBestHealTarget(
        SimulationWorld world,
        PlayerEntity player,
        PlayerTeam team,
        IReadOnlyList<PlayerEntity> allPlayers,
        BotMemory memory)
    {
        if (player.ClassId != PlayerClass.Medic)
        {
            return null;
        }

        var stickyTarget = TryResolveStickyTarget(allPlayers, team, memory.HealTargetPlayerId);
        if (stickyTarget is not null
            && stickyTarget.Team == team
            && stickyTarget.IsAlive
            && NeedsHealing(stickyTarget)
            && DistanceSquared(player.X, player.Y, stickyTarget.X, stickyTarget.Y) <= HealTargetSeekDistance * HealTargetSeekDistance)
        {
            memory.HealTargetLockTicksRemaining = Math.Max(memory.HealTargetLockTicksRemaining, 12);
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
        memory.HealTargetLockTicksRemaining = bestTarget is null ? 0 : 20;
        return bestTarget;
    }

    private (float X, float Y) ResolveAimTarget(
        PlayerEntity player,
        (float X, float Y) destination,
        PlayerEntity? enemyTarget,
        PlayerEntity? healTarget)
    {
        if (healTarget is not null)
        {
            return (healTarget.X, GetAimTargetY(healTarget));
        }

        if (enemyTarget is not null)
        {
            return (enemyTarget.X, GetAimTargetY(enemyTarget));
        }

        return (destination.X, destination.Y - player.Height * AimChestOffsetFraction);
    }

    private int ResolveHorizontalMovement(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination,
        PlayerEntity? enemyTarget,
        PlayerEntity? healTarget,
        bool hasVisibleEnemy,
        NavigationDecision navigationDecision,
        BotMemory memory)
    {
        if (memory.UnstickTicks > 0)
        {
            return memory.UnstickDirection;
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
            return ResolveCombatMovement(player, enemyTarget, memory);
        }

        if (navigationDecision.ForcedHorizontalDirection != 0)
        {
            return navigationDecision.ForcedHorizontalDirection;
        }

        if (DistanceSquared(player.X, player.Y, destination.X, destination.Y) <= ObjectiveArrivalDistance * ObjectiveArrivalDistance)
        {
            return ResolvePatrolMovement(memory, destination.X - player.X);
        }

        return GetMoveDirection(destination.X - player.X);
    }

    private int ResolveCombatMovement(PlayerEntity player, PlayerEntity enemyTarget, BotMemory memory)
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
            memory.StrafeTicksRemaining = _random.Next(StrafeTicksMin, StrafeTicksMax + 1);
        }

        memory.StrafeTicksRemaining -= 1;
        return memory.StrafeDirection;
    }

    private int ResolvePatrolMovement(BotMemory memory, float destinationOffsetX)
    {
        if (MathF.Abs(destinationOffsetX) > PatrolDistance)
        {
            return GetMoveDirection(destinationOffsetX);
        }

        if (memory.StrafeTicksRemaining <= 0)
        {
            memory.StrafeDirection = GetRandomDirection();
            memory.StrafeTicksRemaining = _random.Next(StrafeTicksMin, StrafeTicksMax + 1);
        }

        memory.StrafeTicksRemaining -= 1;
        return memory.StrafeDirection;
    }

    private bool ResolveJump(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination,
        PlayerEntity? enemyTarget,
        PlayerEntity? healTarget,
        int horizontal,
        bool hasVisibleEnemy,
        NavigationDecision navigationDecision,
        BotMemory memory)
    {
        if (navigationDecision.ForceJump)
        {
            memory.JumpCooldownTicks = JumpCooldownTicksDefault;
            return true;
        }

        if (memory.JumpCooldownTicks > 0)
        {
            return false;
        }

        if (memory.UnstickTicks > 0 && player.IsGrounded)
        {
            memory.JumpCooldownTicks = JumpCooldownTicksDefault;
            return true;
        }

        var movementTarget = healTarget is not null && player.ClassId == PlayerClass.Medic
            ? (healTarget.X, healTarget.Y)
            : enemyTarget is not null && hasVisibleEnemy
                ? (enemyTarget.X, enemyTarget.Y)
                : destination;

        if (movementTarget.Y < player.Y - 24f && player.IsGrounded)
        {
            memory.JumpCooldownTicks = JumpCooldownTicksDefault;
            return true;
        }

        if (horizontal != 0 && WouldMoveIntoObstacle(world, player, horizontal))
        {
            memory.JumpCooldownTicks = JumpCooldownTicksDefault;
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
        PlayerEntity? enemyTarget,
        PlayerEntity? healTarget)
    {
        if (player.ClassId == PlayerClass.Medic && healTarget is not null)
        {
            if (!NeedsHealing(healTarget))
            {
                return false;
            }

            if (!HasCombatLineOfSight(world, player, healTarget))
            {
                return false;
            }

            return DistanceBetween(player.X, player.Y, healTarget.X, healTarget.Y) <= 220f;
        }

        if (enemyTarget is null)
        {
            return false;
        }

        if (!HasCombatLineOfSight(world, player, enemyTarget))
        {
            return false;
        }

        var distance = DistanceBetween(player.X, player.Y, enemyTarget.X, enemyTarget.Y);
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

    private static bool ResolveSecondaryFire(PlayerEntity player, PlayerEntity? healTarget)
    {
        if (player.ClassId != PlayerClass.Medic || healTarget is null)
        {
            return false;
        }

        return player.IsMedicUberReady
            && healTarget.Health < healTarget.MaxHealth / 2
            && DistanceBetween(player.X, player.Y, healTarget.X, healTarget.Y) <= 200f;
    }

    private bool TryGetHealingCabinetDestination(SimulationWorld world, PlayerEntity player, out (float X, float Y) destination)
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
            PlayerClass.Heavy => (110f, 220f),
            PlayerClass.Soldier => (150f, 320f),
            PlayerClass.Medic => (100f, 170f),
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
        if (!target.IsAlive || target.Team == player.Team || memory.TargetLockTicksRemaining <= 0)
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
        PlayerEntity? enemyTarget,
        bool hasVisibleEnemy,
        bool isSeekingCabinet,
        (float X, float Y) destination,
        NavigationDecision navigationDecision)
    {
        var focusKind = ResolveFocusKind(healTarget, enemyTarget, isSeekingCabinet);
        var focusLabel = ResolveFocusLabel(world, controlledSlot.Team, role, focusKind, healTarget, enemyTarget);
        var state = ResolveStateKind(
            player,
            memory,
            healTarget,
            enemyTarget,
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
        PlayerEntity? enemyTarget,
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

        if (enemyTarget is not null && hasVisibleEnemy)
        {
            var preferredRange = GetPreferredCombatRange(player.ClassId);
            var distance = DistanceBetween(player.X, player.Y, enemyTarget.X, enemyTarget.Y);
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
        PlayerEntity? enemyTarget,
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

        return enemyTarget is not null ? BotFocusKind.Enemy : BotFocusKind.Objective;
    }

    private static string ResolveFocusLabel(
        SimulationWorld world,
        PlayerTeam team,
        BotRole role,
        BotFocusKind focusKind,
        PlayerEntity? healTarget,
        PlayerEntity? enemyTarget)
    {
        return focusKind switch
        {
            BotFocusKind.HealingCabinet => "cabinet",
            BotFocusKind.HealTarget => healTarget?.DisplayName ?? "ally",
            BotFocusKind.Enemy => enemyTarget?.DisplayName ?? "enemy",
            _ => DescribeObjectiveFocus(world, team, role),
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
            _ => team == PlayerTeam.Blue ? "push red" : "push blu",
        };
    }

    private void TickMemory(BotMemory memory, PlayerEntity player)
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

        if (memory.UnstickTicks > 0)
        {
            memory.UnstickTicks -= 1;
        }

        if (!memory.HasObservedPosition)
        {
            memory.LastObservedX = player.X;
            memory.LastObservedY = player.Y;
            memory.HasObservedPosition = true;
            return;
        }

        var movedDistanceSquared = DistanceSquared(player.X, player.Y, memory.LastObservedX, memory.LastObservedY);
        if (memory.LastRequestedHorizontal != 0 && movedDistanceSquared < StuckMoveDistanceSquared)
        {
            memory.StuckTicks += 1;
        }
        else
        {
            memory.StuckTicks = 0;
        }

        if (memory.StuckTicks >= StuckTickThreshold)
        {
            ClearNavigationRoute(memory);
            memory.UnstickTicks = UnstickTicksDefault;
            memory.UnstickDirection = memory.LastRequestedHorizontal == 0
                ? GetRandomDirection()
                : -memory.LastRequestedHorizontal;
            memory.StrafeTicksRemaining = 0;
            memory.StuckTicks = 0;
        }

        memory.LastObservedX = player.X;
        memory.LastObservedY = player.Y;
    }

    private void ResetTransientState(BotMemory memory, bool keepObservedPosition)
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
            memory.LastObservedX = 0f;
            memory.LastObservedY = 0f;
        }
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
        foreach (var solid in world.Level.Solids)
        {
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
        return leftA < rightB
            && rightA > leftB
            && topA < bottomB
            && bottomA > topB;
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

    private static int GetMoveDirection(float deltaX)
    {
        if (MathF.Abs(deltaX) <= MovementDeadZone)
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

    private sealed class BotMemory
    {
        public bool HasObservedPosition { get; set; }

        public float LastObservedX { get; set; }

        public float LastObservedY { get; set; }

        public int LastRequestedHorizontal { get; set; }

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

        public int[]? RouteNodeIds { get; set; }

        public int RouteIndex { get; set; }

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

        public int ActiveTraversalFrameTicksRemaining { get; set; }
    }

    private readonly record struct NavigationDecision(
        (float X, float Y) MovementTarget,
        bool HasRoute,
        int ForcedHorizontalDirection,
        bool ForceJump,
        bool LocksMovement,
        string Label);

    private readonly record struct ControlledPlayerState(ControlledBotSlot ControlledSlot, PlayerEntity Player);
}
