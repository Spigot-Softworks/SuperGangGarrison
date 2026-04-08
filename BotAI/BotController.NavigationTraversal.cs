using OpenGarrison.Core;

namespace OpenGarrison.BotAI;

public sealed partial class ModernPracticeBotController
{
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
}
