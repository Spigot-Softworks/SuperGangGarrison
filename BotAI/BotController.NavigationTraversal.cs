using OpenGarrison.Core;

namespace OpenGarrison.BotAI;

public sealed partial class ModernPracticeBotController
{
    private static bool TryResolveTraversalDecision(
        SimulationWorld world,
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
        var traversalKind = edge.Kind;
        var traversalTape = edge.InputTape;
        var startToleranceX = TraversalStartDistance;
        var startToleranceY = TraversalStartDistance;
        if (TryGetPreferredScoreRouteSegment(memory, previousNodeId, nextNode.Id, out var preferredSegment))
        {
            traversalKind = preferredSegment.TraversalKind;
            traversalTape = preferredSegment.InputTape.Count > 0 ? preferredSegment.InputTape : traversalTape;
            startToleranceX = MathF.Max(TraversalStartDistance, preferredSegment.StartToleranceX);
            startToleranceY = MathF.Max(TraversalStartDistance, preferredSegment.StartToleranceY);
        }

        if (traversalTape.Count > 0)
        {
            if (!navigationGraph.TryGetNode(previousNodeId, out var sourceNode))
            {
                ClearNavigationRoute(memory);
                decision = new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "jump-src");
                return true;
            }

            var isExecutingTraversal = IsExecutingTraversal(memory, previousNodeId, nextNode.Id);
            if (!isExecutingTraversal)
            {
                ClearTraversalExecution(memory);
            }

            if (!isExecutingTraversal
                && !HasReachedTraversalWindow(
                    player.X,
                    player.Bottom,
                    player.IsGrounded,
                    sourceNode.X,
                    sourceNode.Y,
                    startToleranceX,
                    startToleranceY,
                    requireGroundedArrival: true))
            {
                decision = new NavigationDecision((sourceNode.X, sourceNode.Y), HasRoute: true, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: true, Label: GetRouteLabel(memory, traversalKind), TraversalKind: traversalKind, UsesTraversalTape: true);
                return true;
            }

            if (!isExecutingTraversal)
            {
                if (!player.IsGrounded)
                {
                    decision = new NavigationDecision((sourceNode.X, sourceNode.Y), HasRoute: true, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: true, Label: GetRouteLabel(memory, traversalKind), TraversalKind: traversalKind, UsesTraversalTape: true);
                    return true;
                }

                BeginTraversalExecution(memory, previousNodeId, nextNode.Id, traversalKind, traversalTape);
            }

            if (BotNavigationMovementValidator.IsWithinTraversalLandingWindow(
                    player.X,
                    player.Bottom,
                    player.IsGrounded,
                    nextNode.X,
                    nextNode.Y,
                    requireGroundedArrival: false))
            {
                ClearTraversalExecution(memory, rememberCompletion: true);
                decision = new NavigationDecision((nextNode.X, nextNode.Y), HasRoute: true, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: GetRouteLabel(memory, traversalKind), TraversalKind: traversalKind, UsesTraversalTape: true);
                return true;
            }

            if (TryConsumeTraversalExecution(memory, simulationTickSeconds, out var forcedHorizontalDirection, out var forceJump))
            {
                decision = new NavigationDecision((nextNode.X, nextNode.Y), HasRoute: true, ForcedHorizontalDirection: forcedHorizontalDirection, ForceJump: forceJump, LocksMovement: true, Label: GetRouteLabel(memory, traversalKind), TraversalKind: traversalKind, UsesTraversalTape: true);
                return true;
            }

            ClearTraversalExecution(memory);
            decision = new NavigationDecision((nextNode.X, nextNode.Y), HasRoute: true, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: GetRouteLabel(memory, traversalKind), TraversalKind: traversalKind, UsesTraversalTape: true);
            return true;
        }

        ClearTraversalExecution(memory);
        if (traversalKind == BotNavigationTraversalKind.Drop)
        {
            if (!navigationGraph.TryGetNode(previousNodeId, out var sourceNode))
            {
                ClearNavigationRoute(memory);
                decision = new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "drop-src");
                return true;
            }

            if (DistanceSquared(player.X, player.Bottom, sourceNode.X, sourceNode.Y) > RouteNodeArrivalDistance * RouteNodeArrivalDistance)
            {
                decision = new NavigationDecision((sourceNode.X, sourceNode.Y), HasRoute: true, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: true, Label: GetRouteLabel(memory, traversalKind), TraversalKind: traversalKind);
                return true;
            }

            var dropDirection = navigationGraph.GetDropDirection(sourceNode.Id, nextNode.Id);
            decision = new NavigationDecision((nextNode.X, nextNode.Y), HasRoute: true, ForcedHorizontalDirection: dropDirection, ForceJump: false, LocksMovement: true, Label: GetRouteLabel(memory, traversalKind), TraversalKind: traversalKind);
            return true;
        }

        if (traversalKind == BotNavigationTraversalKind.Jump)
        {
            if (!navigationGraph.TryGetNode(previousNodeId, out var sourceNode))
            {
                ClearNavigationRoute(memory);
                decision = new NavigationDecision(destination, HasRoute: false, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "jump-src");
                return true;
            }

            if (BotNavigationMovementValidator.IsWithinTraversalLandingWindow(
                    player.X,
                    player.Bottom,
                    player.IsGrounded,
                    nextNode.X,
                    nextNode.Y,
                    requireGroundedArrival: false))
            {
                RememberTraversalCompletion(memory, previousNodeId, nextNode.Id, usedTape: false);
                decision = new NavigationDecision((nextNode.X, nextNode.Y), HasRoute: true, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: GetRouteLabel(memory, traversalKind), TraversalKind: traversalKind);
                return true;
            }

            decision = new NavigationDecision((nextNode.X, nextNode.Y), HasRoute: true, ForcedHorizontalDirection: GetTraversalCommitDirection(sourceNode.X, nextNode.X), ForceJump: false, LocksMovement: true, Label: GetRouteLabel(memory, traversalKind), TraversalKind: traversalKind);
            return true;
        }

        decision = default;
        return false;
    }

    private static void AdvanceRouteProgress(
        PlayerEntity player,
        BotNavigationRuntimeGraph navigationGraph,
        BotMemory memory,
        bool delayAirborneJumpChainHandoff)
    {
        if (memory.RouteNodeIds is null)
        {
            return;
        }

        while (memory.RouteIndex < memory.RouteNodeIds.Length
            && navigationGraph.TryGetNode(memory.RouteNodeIds[memory.RouteIndex], out var currentNode)
            && HasReachedRouteNode(
                player,
                navigationGraph,
                memory,
                memory.RouteNodeIds,
                memory.RouteIndex,
                currentNode,
                delayAirborneJumpChainHandoff))
        {
            if (memory.RouteIndex > 0)
            {
                ClearRouteEdgeFailure(memory, memory.RouteNodeIds[memory.RouteIndex - 1], memory.RouteNodeIds[memory.RouteIndex]);
            }

            memory.RouteIndex += 1;
        }
    }

    private static bool HasReachedRouteNode(
        PlayerEntity player,
        BotNavigationRuntimeGraph navigationGraph,
        BotMemory memory,
        int[] routeNodeIds,
        int routeIndex,
        BotNavigationNode currentNode,
        bool delayAirborneJumpChainHandoff)
    {
        var playerFeetY = GetModernPlayerFeetY(player);
        if (ShouldDelayAirborneJumpChainHandoff(player, navigationGraph, memory, routeNodeIds, routeIndex, currentNode, delayAirborneJumpChainHandoff))
        {
            return false;
        }

        if (DistanceSquared(player.X, playerFeetY, currentNode.X, currentNode.Y)
            <= GetRouteNodeArrivalDistanceSquared(navigationGraph, memory, routeNodeIds, routeIndex))
        {
            return true;
        }

        if (routeIndex > 0
            && player.IsGrounded
            && memory.ActiveTraversalTape is null
            && TryGetPreferredScoreRouteSegment(memory, routeNodeIds[routeIndex - 1], routeNodeIds[routeIndex], out var preferredSegment)
            && preferredSegment.InputTape.Count > 0
            && HasReachedTraversalWindow(
                player.X,
                player.Bottom,
                player.IsGrounded,
                currentNode.X,
                currentNode.Y,
                preferredSegment.LandingToleranceX,
                preferredSegment.LandingToleranceY,
                preferredSegment.RequireGroundedArrival))
        {
            return true;
        }

        if (routeIndex > 0
            && memory.ActiveTraversalTape is null
            && memory.LastTraversalCompletionTicks > 0
            && memory.LastTraversalFromNodeId == routeNodeIds[routeIndex - 1]
            && memory.LastTraversalToNodeId == routeNodeIds[routeIndex])
        {
            ResetLastTraversalExecution(memory);
            return true;
        }

        if (routeIndex > 0
            && memory.ActiveTraversalTape is null
            && memory.LastTraversalUsedTape
            && memory.LastTraversalFromNodeId == routeNodeIds[routeIndex - 1]
            && memory.LastTraversalToNodeId == routeNodeIds[routeIndex]
            && BotNavigationMovementValidator.IsWithinTraversalLandingWindow(
                player.X,
                player.Bottom,
                player.IsGrounded,
                currentNode.X,
                currentNode.Y,
                requireGroundedArrival: false))
        {
            ResetLastTraversalExecution(memory);
            return true;
        }

        // Generated/runtime taped traversals validate against a wider landing
        // window than the route planner's normal 6px traversal handoff radius.
        // Once the tape has finished, accept that validated landing and advance
        // the route instead of sending the bot back toward the source node.
        if (routeIndex > 0
            && memory.ActiveTraversalTape is null
            && navigationGraph.TryGetEdge(routeNodeIds[routeIndex - 1], routeNodeIds[routeIndex], out var incomingEdge)
            && incomingEdge.InputTape.Count > 0
            && BotNavigationMovementValidator.IsWithinTraversalLandingWindow(
                player.X,
                player.Bottom,
                player.IsGrounded,
                currentNode.X,
                currentNode.Y,
                requireGroundedArrival: false))
        {
            return true;
        }

        return routeIndex > 0
            && currentNode.RequiresGroundSupport
            && player.IsGrounded
            && navigationGraph.TryGetEdge(routeNodeIds[routeIndex - 1], routeNodeIds[routeIndex], out var edge)
            && edge.Kind == BotNavigationTraversalKind.Walk
            && MathF.Abs(player.X - currentNode.X) <= RouteNodeArrivalDistance
            && MathF.Abs(playerFeetY - currentNode.Y) <= RouteWalkArrivalFeetHeight;
    }

    private static bool ShouldDelayAirborneJumpChainHandoff(
        PlayerEntity player,
        BotNavigationRuntimeGraph navigationGraph,
        BotMemory memory,
        int[] routeNodeIds,
        int routeIndex,
        BotNavigationNode currentNode,
        bool delayAirborneJumpChainHandoff)
    {
        if (!delayAirborneJumpChainHandoff
            || player.IsGrounded
            || memory.ActiveTraversalTape is not null
            || routeIndex <= 0
            || !currentNode.RequiresGroundSupport)
        {
            return false;
        }

        if (routeIndex + 1 >= routeNodeIds.Length)
        {
            return true;
        }

        return navigationGraph.TryGetEdge(routeNodeIds[routeIndex], routeNodeIds[routeIndex + 1], out var outgoingEdge)
            && outgoingEdge.Kind is BotNavigationTraversalKind.Walk or BotNavigationTraversalKind.Jump;
    }

    private static float GetRouteNodeArrivalDistanceSquared(
        BotNavigationRuntimeGraph navigationGraph,
        BotMemory memory,
        int[] routeNodeIds,
        int routeIndex)
    {
        var arrivalDistance = RouteNodeArrivalDistance;
        if (routeIndex >= 0
            && routeIndex + 1 < routeNodeIds.Length
            && TryGetPreferredScoreRouteSegment(memory, routeNodeIds[routeIndex], routeNodeIds[routeIndex + 1], out var preferredSegment)
            && preferredSegment.InputTape.Count > 0)
        {
            arrivalDistance = MathF.Max(
                TraversalStartDistance,
                MathF.Max(preferredSegment.StartToleranceX, preferredSegment.StartToleranceY));
        }

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

    private static bool ShouldForceWalkRouteRecoveryJump(
        PlayerEntity player,
        BotNavigationRuntimeGraph navigationGraph,
        int previousNodeId,
        BotNavigationNode nextNode,
        BotNavigationEdge edge)
    {
        if (!player.IsGrounded
            || edge.Kind != BotNavigationTraversalKind.Walk
            || !navigationGraph.TryGetNode(previousNodeId, out var sourceNode)
            || MathF.Abs(sourceNode.Y - nextNode.Y) > RouteWalkRecoveryFlatHeightDelta)
        {
            return false;
        }

        var minX = MathF.Min(sourceNode.X, nextNode.X) - RouteNodeArrivalDistance;
        var maxX = MathF.Max(sourceNode.X, nextNode.X) + RouteNodeArrivalDistance;
        if (player.X < minX || player.X > maxX)
        {
            return false;
        }

        var segmentLengthX = nextNode.X - sourceNode.X;
        var segmentT = MathF.Abs(segmentLengthX) <= float.Epsilon
            ? 1f
            : Math.Clamp((player.X - sourceNode.X) / segmentLengthX, 0f, 1f);
        var segmentY = sourceNode.Y + ((nextNode.Y - sourceNode.Y) * segmentT);
        return GetModernPlayerFeetY(player) - segmentY >= RouteWalkRecoveryJumpFeetDrop;
    }

    private static bool TryBuildRouteToDestination(
        BotNavigationRuntimeGraph navigationGraph,
        int startNodeId,
        int exactGoalNodeId,
        (float X, float Y) destination,
        Func<BotNavigationEdge, bool>? edgeFilter,
        Func<BotNavigationEdge, bool>? fallbackEdgeFilter,
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

        if (edgeFilter is not null)
        {
            if (exactGoalNodeId >= 0)
            {
                route = navigationGraph.FindRoute(startNodeId, exactGoalNodeId, fallbackEdgeFilter) ?? Array.Empty<int>();
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
                    fallbackEdgeFilter,
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
                    fallbackEdgeFilter,
                    out route,
                    out _)
                && route.Length > 1)
            {
                routeIsPartial = true;
                return true;
            }
        }

        route = Array.Empty<int>();
        return false;
    }

    private static bool ShouldBlockCurrentRouteEdge(
        PlayerEntity player,
        BotNavigationRuntimeGraph navigationGraph,
        BotMemory memory,
        int fromNodeId,
        BotNavigationNode nextNode,
        BotNavigationEdge edge,
        (float X, float Y) destination)
    {
        var playerFeetY = GetModernPlayerFeetY(player);
        var nearIntelReturnHome = player.IsCarryingIntel
            && MathF.Abs(player.X - destination.X) <= ModernIntelReturnBlockedEdgeBypassDistanceX
            && MathF.Abs(playerFeetY - destination.Y) <= ModernIntelReturnBlockedEdgeBypassDistanceY;
        var nearObjectiveFinish = MathF.Abs(player.X - destination.X) <= RouteGoalNodeSearchDistance
            && MathF.Abs(playerFeetY - destination.Y) <= RouteGoalNodeSearchDistance;
        if (nearIntelReturnHome || nearObjectiveFinish)
        {
            memory.RouteObjectiveBestDistance = MathF.Min(
                memory.RouteObjectiveBestDistance,
                DistanceBetween(player.X, playerFeetY, destination.X, destination.Y));
            memory.RouteObjectiveNoProgressTicks = 0;
            memory.RouteNoProgressTicks = 0;
            return false;
        }

        var lenientProgressChecks = nearIntelReturnHome
            || nearObjectiveFinish;
        var progressTargetX = nextNode.X;
        var progressTargetY = nextNode.Y;
        var currentDistance = DistanceBetween(player.X, playerFeetY, progressTargetX, progressTargetY);
        if (memory.RouteProgressFromNodeId != fromNodeId
            || memory.RouteProgressToNodeId != nextNode.Id)
        {
            memory.RouteProgressFromNodeId = fromNodeId;
            memory.RouteProgressToNodeId = nextNode.Id;
            memory.RouteProgressBestDistance = currentDistance;
            memory.RouteNoProgressTicks = 0;
        }
        else if ((currentDistance + RouteProgressImprovementDistance) < memory.RouteProgressBestDistance)
        {
            memory.RouteProgressBestDistance = currentDistance;
            memory.RouteNoProgressTicks = 0;
        }
        else if (player.IsGrounded || edge.Kind == BotNavigationTraversalKind.Walk)
        {
            memory.RouteNoProgressTicks += 1;
        }

        if (lenientProgressChecks)
        {
            // Near objective completion and partial-route execution can require
            // temporary lateral/vertical movement before objective distance improves.
            // Keep edge progress checks, but avoid poisoning the graph with objective-distance stalls.
            memory.RouteObjectiveBestDistance = MathF.Min(
                memory.RouteObjectiveBestDistance,
                DistanceBetween(player.X, playerFeetY, destination.X, destination.Y));
            memory.RouteObjectiveNoProgressTicks = 0;
        }
        else
        {
            var objectiveDistance = DistanceBetween(player.X, playerFeetY, destination.X, destination.Y);
            if ((objectiveDistance + RouteObjectiveProgressImprovementDistance) < memory.RouteObjectiveBestDistance)
            {
                memory.RouteObjectiveBestDistance = objectiveDistance;
                memory.RouteObjectiveNoProgressTicks = 0;
            }
            else
            {
                memory.RouteObjectiveNoProgressTicks += 1;
            }
        }

        var edgeNoProgressLimit = lenientProgressChecks
            ? RouteNoProgressTicksBeforeReplan * 3
            : RouteNoProgressTicksBeforeReplan;
        return memory.RouteNoProgressTicks >= edgeNoProgressLimit
            || (!lenientProgressChecks
                && memory.RouteObjectiveNoProgressTicks >= RouteObjectiveNoProgressTicksBeforeReplan);
    }

    private static void BlockRouteEdge(BotMemory memory, int fromNodeId, int toNodeId)
    {
        memory.NavigationIssueLabel = $"route_edge_block:{fromNodeId}->{toNodeId}";
        ResetRouteProgress(memory);
    }

    private static void ClearRouteEdgeFailure(BotMemory memory, int fromNodeId, int toNodeId)
    {
        var key = GetRouteEdgeKey(fromNodeId, toNodeId);
        memory.RouteBlockedEdgeTicksByKey?.Remove(key);
        memory.RouteBlockedEdgeFailureCountsByKey?.Remove(key);
    }

    private static bool IsRouteEdgeTemporarilyBlocked(BotMemory memory, int fromNodeId, int toNodeId)
    {
        return memory.RouteBlockedEdgeTicksByKey is not null
            && memory.RouteBlockedEdgeTicksByKey.TryGetValue(GetRouteEdgeKey(fromNodeId, toNodeId), out var ticks)
            && ticks > 0;
    }

    private static void DecayRouteBlockedEdges(BotMemory memory)
    {
        if (memory.RouteBlockedEdgeTicksByKey is null || memory.RouteBlockedEdgeTicksByKey.Count == 0)
        {
            return;
        }

        var keys = memory.RouteBlockedEdgeTicksByKey.Keys.ToArray();
        for (var index = 0; index < keys.Length; index += 1)
        {
            var key = keys[index];
            var remainingTicks = memory.RouteBlockedEdgeTicksByKey[key] - 1;
            if (remainingTicks <= 0)
            {
                memory.RouteBlockedEdgeTicksByKey.Remove(key);
            }
            else
            {
                memory.RouteBlockedEdgeTicksByKey[key] = remainingTicks;
            }
        }
    }

    private static long GetRouteEdgeKey(int fromNodeId, int toNodeId)
    {
        return ((long)fromNodeId << 32) | (uint)toNodeId;
    }

    private static int GetTraversalCommitDirection(float sourceX, float targetX)
    {
        var deltaX = targetX - sourceX;
        if (MathF.Abs(deltaX) <= 1f)
        {
            return 0;
        }

        return deltaX > 0f ? 1 : -1;
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
        ResetRouteProgress(memory);
        ResetModernNavigationState(memory);
    }

    private static void ResetRouteProgress(BotMemory memory)
    {
        ResetRouteEdgeProgress(memory);
        memory.RouteObjectiveBestDistance = float.PositiveInfinity;
        memory.RouteObjectiveNoProgressTicks = 0;
    }

    private static void ResetRouteEdgeProgress(BotMemory memory)
    {
        memory.RouteProgressFromNodeId = -1;
        memory.RouteProgressToNodeId = -1;
        memory.RouteProgressBestDistance = float.PositiveInfinity;
        memory.RouteNoProgressTicks = 0;
    }

    private static void ResetModernNavigationState(BotMemory memory)
    {
        ClearActivePreferredScoreRoute(memory);
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
        ClearModernChainExecutor(memory);
        memory.ModernChainExecutorCooldownTicks = 0;
        memory.HasModernClosestPointTarget = false;
        memory.ModernClosestPointTargetX = 0f;
        memory.ModernClosestPointTargetY = 0f;
        memory.HasModernClosestPointTarget = false;
        memory.ModernClosestPointTargetX = 0f;
        memory.ModernClosestPointTargetY = 0f;
        memory.ReanchorTicksRemaining = 0;
        memory.SecondAnchorCooldownTicksRemaining = 0;
        memory.SecondAnchorBlockPointId = -1;
        memory.SecondAnchorBlockTicksRemaining = 0;
        memory.FallbackRouteLabel = string.Empty;
        memory.FallbackTriggerLabel = string.Empty;
        memory.NavigationIssueLabel = string.Empty;
        memory.BranchDiagnosticCurrentPointId = -1;
        memory.BranchDiagnosticNextPointId = -1;
        memory.BranchDiagnosticTicks = 0;
        memory.BranchDiagnosticNoProgressTicks = 0;
        memory.DirectTargetTicks = 0;
        memory.DirectTargetNoProgressTicks = 0;
        memory.CaptureHoldInsideMilliseconds = 0f;
        memory.CaptureActiveGroupId = -1;
    }

    private static void BeginTraversalExecution(BotMemory memory, int fromNodeId, int toNodeId, BotNavigationEdge edge)
    {
        BeginTraversalExecution(memory, fromNodeId, toNodeId, edge.Kind, edge.InputTape);
    }

    private static void BeginTraversalExecution(
        BotMemory memory,
        int fromNodeId,
        int toNodeId,
        BotNavigationTraversalKind traversalKind,
        IReadOnlyList<BotNavigationInputFrame> inputTape)
    {
        ResetLastTraversalExecution(memory);
        memory.ActiveTraversalFromNodeId = fromNodeId;
        memory.ActiveTraversalToNodeId = toNodeId;
        memory.ActiveTraversalKind = traversalKind;
        memory.ActiveTraversalTape = inputTape;
        memory.ActiveTraversalFrameIndex = 0;
        memory.ActiveTraversalFrameSecondsRemaining = inputTape.Count == 0
            ? 0
            : GetFrameDurationSeconds(inputTape[0]);
    }

    private static bool TryGetPreferredScoreRouteSegment(
        BotMemory memory,
        int fromNodeId,
        int toNodeId,
        out BotNavigationScoreRouteSegment segment)
    {
        if (memory.ActivePreferredScoreRoute?.Segments is { Count: > 0 } segments)
        {
            for (var index = 0; index < segments.Count; index += 1)
            {
                var candidate = segments[index];
                if (candidate.FromNodeId == fromNodeId && candidate.ToNodeId == toNodeId)
                {
                    segment = candidate;
                    return true;
                }
            }
        }

        segment = default!;
        return false;
    }

    private static bool HasReachedTraversalWindow(
        float currentX,
        float currentY,
        bool isGrounded,
        float targetX,
        float targetY,
        float toleranceX,
        float toleranceY,
        bool requireGroundedArrival)
    {
        return (!requireGroundedArrival || isGrounded)
            && MathF.Abs(currentX - targetX) <= toleranceX
            && MathF.Abs(currentY - targetY) <= toleranceY;
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
                ClearTraversalExecution(memory, rememberCompletion: true);
                return true;
            }

            remainingSeconds += GetFrameDurationSeconds(memory.ActiveTraversalTape[memory.ActiveTraversalFrameIndex]);
        }

        memory.ActiveTraversalFrameSecondsRemaining = remainingSeconds;
        return true;
    }

    private static void ClearTraversalExecution(BotMemory memory, bool rememberCompletion = false)
    {
        if (rememberCompletion && memory.ActiveTraversalTape is { Count: > 0 })
        {
            RememberTraversalCompletion(memory, memory.ActiveTraversalFromNodeId, memory.ActiveTraversalToNodeId, usedTape: true);
        }
        else
        {
            ResetLastTraversalExecution(memory);
        }

        memory.ActiveTraversalFromNodeId = -1;
        memory.ActiveTraversalToNodeId = -1;
        memory.ActiveTraversalKind = BotNavigationTraversalKind.Walk;
        memory.ActiveTraversalTape = null;
        memory.ActiveTraversalFrameIndex = 0;
        memory.ActiveTraversalFrameSecondsRemaining = 0d;
    }

    private static void RememberTraversalCompletion(BotMemory memory, int fromNodeId, int toNodeId, bool usedTape)
    {
        memory.LastTraversalFromNodeId = fromNodeId;
        memory.LastTraversalToNodeId = toNodeId;
        memory.LastTraversalUsedTape = usedTape;
        memory.LastTraversalCompletionTicks = 3;
    }

    private static void ResetLastTraversalExecution(BotMemory memory)
    {
        memory.LastTraversalFromNodeId = -1;
        memory.LastTraversalToNodeId = -1;
        memory.LastTraversalUsedTape = false;
        memory.LastTraversalCompletionTicks = 0;
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
}
