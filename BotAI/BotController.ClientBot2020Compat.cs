using OpenGarrison.Core;

namespace OpenGarrison.BotAI;

public sealed partial class ModernPracticeBotController
{
    private NavigationDecision ResolveClientBot2020CompatNavigationDecision(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination,
        ClientBotNavPoints navPoints,
        BotMemory memory,
        BotTimingProfile timing,
        ModernPathSelection objectiveSelection)
    {
        var state = memory.ClientBot2020Compat ??= new ClientBot2020CompatState();
        var captureHoldState = ResolveModernCaptureHoldState(player, objectiveSelection, memory, timing.FixedDeltaSeconds);
        memory.NavigationGraphKey = navPoints.CacheKey;
        memory.RouteGoalX = destination.X;
        memory.RouteGoalY = destination.Y;
        if (captureHoldState.SuppressNavigation)
        {
            memory.RouteGoalNodeId = state.GoalPointId;
            state.NextPoint = -1;
            state.NextPoint2 = -1;
            state.NextPoint3 = -1;
            state.NoNextPointTicks = 0;
            state.StickyNextTicks = 0;
            state.ClosestPointX = player.X;
            state.ClosestPointY = player.Bottom;
            state.DebugDecisionReason = "capture_zone_hold";
            SyncClientBot2020CompatStateToMemory(state, memory);
            return new NavigationDecision(
                (player.X, player.Bottom),
                HasRoute: true,
                ForcedHorizontalDirection: 0,
                ForceJump: false,
                LocksMovement: false,
                Label: "compat:capture_zone_hold",
                TraversalKind: BotNavigationTraversalKind.Walk,
                MovementTargetUsesFeetCoordinates: true,
                CaptureHoldActive: true);
        }

        var useRoute = !objectiveSelection.AllowDirectPath
            || MathF.Abs(player.X - destination.X) > 200f
            || MathF.Abs(player.Y - destination.Y) > 80f
            || CompatLineHitsSolid(world, player, destination.X, destination.Y, player.X, player.Y);
        if (!useRoute)
        {
            memory.RouteGoalNodeId = state.GoalPointId;
            state.ClosestPointX = destination.X;
            state.ClosestPointY = destination.Y;
            state.DebugDecisionReason = "direct_target";
            state.CurrentPoint = -1;
            state.NextPoint = -1;
            state.NextPoint2 = -1;
            state.NextPoint3 = -1;
            SyncClientBot2020CompatStateToMemory(state, memory);
            return new NavigationDecision(destination, HasRoute: true, ForcedHorizontalDirection: 0, ForceJump: false, LocksMovement: false, Label: "compat:direct_target");
        }

        var pathKey = $"{destination.X:0.###}|{destination.Y:0.###}";
        if (!string.Equals(pathKey, state.OldPathPointKey, StringComparison.Ordinal))
        {
            state.OldPathPointKey = pathKey;
            if (TryFindNearestCompatGoalPoint(navPoints, destination, out var closestPointId))
            {
                if (closestPointId != state.OldClosestPoint
                    && (state.OldClosestPoint < 0
                        || !navPoints.TryGetPoint(state.OldClosestPoint, out var oldClosestNode)
                        || !navPoints.TryGetPoint(closestPointId, out var closestNode)
                        || DistanceBetween(oldClosestNode.X, oldClosestNode.Y, closestNode.X, closestNode.Y) > 120f))
                {
                    state.OldClosestPoint = closestPointId;
                    state.GoalPointId = closestPointId;
                    state.GoalWeights = navPoints.GetGoalWeights(closestPointId, ModernMaximumWeightDepth);
                    state.NavMapDone = state.GoalWeights is not null;
                }
            }
        }
        memory.RouteGoalNodeId = state.GoalPointId;

        if (state.GoalWeights is not null && state.NavMapDone)
        {
            TryAcquireCompatCurrentPoint(world, player, navPoints, state);
            if (state.CurrentPoint >= 0)
            {
                var goalWeights = state.GoalWeights;
                var pointWeight1 = GetModernGoalWeight(goalWeights, state.CurrentPoint);

                if (pointWeight1 <= 1)
                {
                    state.ClosestPointX = destination.X;
                    state.ClosestPointY = destination.Y;
                    state.DebugDecisionReason = "direct_target";
                }
                else
                {
                    TrySelectCompatNextPoint(world, player, destination, navPoints, goalWeights, state, timing);
                    TryPopulateCompatLookahead(navPoints, goalWeights, state);
                }

                UpdateCompatNoNextPoint(navPoints, state, timing);
                TryForceCompatNeighborStep(world, player, destination, navPoints, goalWeights, state, timing);
                TryCompatReacquireCollision(world, player, navPoints, state);
            }
            else
            {
                state.NextPoint = -1;
                state.NextPoint2 = -1;
                state.NextPoint3 = -1;
                state.ClosestPointX = destination.X;
                state.ClosestPointY = destination.Y;
                state.DebugDecisionReason = "missing_currentpoint";
            }
        }
        else
        {
            memory.RouteGoalNodeId = -1;
            state.ClosestPointX = destination.X;
            state.ClosestPointY = destination.Y;
            state.DebugDecisionReason = "direct_target_nonode";
        }

        SyncClientBot2020CompatStateToMemory(state, memory);
        var movementTarget = (state.ClosestPointX, state.ClosestPointY);
        return new NavigationDecision(
            movementTarget,
            HasRoute: true,
            ForcedHorizontalDirection: 0,
            ForceJump: false,
            LocksMovement: false,
            Label: $"compat:{state.DebugDecisionReason}",
            TraversalKind: BotNavigationTraversalKind.Walk,
            MovementTargetUsesFeetCoordinates: true);
    }

    private static void SyncClientBot2020CompatStateToMemory(ClientBot2020CompatState state, BotMemory memory)
    {
        memory.CurrentPointId = state.CurrentPoint;
        memory.NextPointId = state.NextPoint;
        memory.NextPoint2Id = state.NextPoint2;
        memory.NextPoint3Id = state.NextPoint3;
        memory.PreviousCurrentPointId = state.PreviousCurrentPoint;
        memory.PreviousNextPointId = state.PreviousNextPoint;
        memory.LastCommittedPointId = state.LastCommittedPoint;
        memory.SecondLastCommittedPointId = state.SecondLastCommittedPoint;
        memory.StickyCurrentPointId = state.StickyCurrentPoint;
        memory.StickyNextPointId = state.StickyNextPoint;
        memory.StickyNextTicksRemaining = state.StickyNextTicks;
        memory.NavChurnCurrentPointId = state.NavChurnCurrentPoint;
        memory.NavChurnTicks = state.NavChurnTicks;
        memory.NavChurnSwitchTicks = state.NavChurnSwitchTicks;
        memory.NavChurnLockPointId = state.NavChurnLockPoint;
        memory.NavChurnLockTicksRemaining = state.NavChurnLockTicks;
        memory.NavChurnStartX = state.NavChurnStartX;
        memory.NavChurnStartY = state.NavChurnStartY;
        memory.NoNextPointTicks = state.NoNextPointTicks;
        memory.LoopBacktrackTicks = state.LoopBacktrackTicks;
        memory.SecondAnchorBlockPointId = state.SecondAnchorBlockPoint;
        memory.SecondAnchorBlockTicksRemaining = state.SecondAnchorBlockTicks;
        memory.SecondAnchorCooldownTicksRemaining = state.SecondAnchorCooldownTicks;
        memory.ModernStuckTicks = state.StuckTicks;
        memory.ModernDropGapTicks = state.DropGapTicks;
        memory.ModernPreviousTargetDistance = state.PreviousTargetDistance;
        memory.HasModernClosestPointTarget = true;
        memory.ModernClosestPointTargetX = state.ClosestPointX;
        memory.ModernClosestPointTargetY = state.ClosestPointY;
    }

    private static void SyncMemoryToClientBot2020CompatState(BotMemory memory)
    {
        if (memory.ClientBot2020Compat is not ClientBot2020CompatState state)
        {
            return;
        }

        state.CurrentPoint = memory.CurrentPointId;
        state.NextPoint = memory.NextPointId;
        state.NextPoint2 = memory.NextPoint2Id;
        state.NextPoint3 = memory.NextPoint3Id;
        state.PreviousCurrentPoint = memory.PreviousCurrentPointId;
        state.PreviousNextPoint = memory.PreviousNextPointId;
        state.LastCommittedPoint = memory.LastCommittedPointId;
        state.SecondLastCommittedPoint = memory.SecondLastCommittedPointId;
        state.StickyCurrentPoint = memory.StickyCurrentPointId;
        state.StickyNextPoint = memory.StickyNextPointId;
        state.StickyNextTicks = memory.StickyNextTicksRemaining;
        state.NavChurnCurrentPoint = memory.NavChurnCurrentPointId;
        state.NavChurnTicks = memory.NavChurnTicks;
        state.NavChurnSwitchTicks = memory.NavChurnSwitchTicks;
        state.NavChurnLockPoint = memory.NavChurnLockPointId;
        state.NavChurnLockTicks = memory.NavChurnLockTicksRemaining;
        state.NavChurnStartX = memory.NavChurnStartX;
        state.NavChurnStartY = memory.NavChurnStartY;
        state.NoNextPointTicks = memory.NoNextPointTicks;
        state.LoopBacktrackTicks = memory.LoopBacktrackTicks;
        state.SecondAnchorCooldownTicks = memory.SecondAnchorCooldownTicksRemaining;
        state.SecondAnchorBlockPoint = memory.SecondAnchorBlockPointId;
        state.SecondAnchorBlockTicks = memory.SecondAnchorBlockTicksRemaining;
        state.StuckTicks = memory.ModernStuckTicks;
        state.DropGapTicks = memory.ModernDropGapTicks;
        state.PreviousTargetDistance = memory.ModernPreviousTargetDistance;
        if (memory.HasModernClosestPointTarget)
        {
            state.ClosestPointX = memory.ModernClosestPointTargetX;
            state.ClosestPointY = memory.ModernClosestPointTargetY;
        }
    }

    private NavigationDecision ApplyClientBot2020CompatFrameState(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination,
        ClientBotNavPoints navPoints,
        BotMemory memory,
        NavigationDecision navigationDecision)
    {
        var state = memory.ClientBot2020Compat ??= new ClientBot2020CompatState();
        SyncMemoryToClientBot2020CompatState(memory);
        var selectionReason = state.DebugDecisionReason;
        UpdateClientBot2020CompatTargetProgress(player, memory, state);
        if (state.GoalWeights is not null
            && state.CurrentPoint >= 0
            && navPoints.TryGetPoint(state.CurrentPoint, out var currentNode)
            && TryApplyClientBot2020CompatReanchor(world, player, destination, navPoints, state.GoalWeights, memory, state, currentNode, out selectionReason))
        {
            state.DebugDecisionReason = selectionReason;
        }

        if (TryPromoteClientBot2020CompatQueuedPoint(player, navPoints, state, out selectionReason))
        {
            state.DebugDecisionReason = selectionReason;
        }

        ApplyClientBot2020CompatClosestPointAnticipation(world, player, navPoints, state, ref selectionReason);
        state.DebugDecisionReason = selectionReason;
        SyncClientBot2020CompatStateToMemory(state, memory);
        return new NavigationDecision(
            (state.ClosestPointX, state.ClosestPointY),
            navigationDecision.HasRoute,
            navigationDecision.ForcedHorizontalDirection,
            navigationDecision.ForceJump,
            navigationDecision.LocksMovement,
            $"compat:{state.DebugDecisionReason}",
            navigationDecision.TraversalKind,
            navigationDecision.MovementTargetUsesFeetCoordinates,
            navigationDecision.CaptureHoldActive);
    }

    private void FinalizeClientBot2020CompatFrameState(BotMemory memory)
    {
        if (memory.ClientBot2020Compat is not ClientBot2020CompatState state)
        {
            return;
        }

        state.PreviousCurrentPoint = state.CurrentPoint;
        state.PreviousNextPoint = state.NextPoint;
        SyncClientBot2020CompatStateToMemory(state, memory);
    }

    private static void UpdateClientBot2020CompatTargetProgress(
        PlayerEntity player,
        BotMemory memory,
        ClientBot2020CompatState state)
    {
        var currentTargetDistance = DistanceBetween(player.X, player.Y, state.ClosestPointX, state.ClosestPointY);
        var movedSinceLastFrame = memory.HasObservedPosition
            ? DistanceBetween(player.X, player.Y, memory.PreviousObservedX, memory.PreviousObservedY)
            : 0f;
        if ((currentTargetDistance + 1f) < state.PreviousTargetDistance || movedSinceLastFrame > 1.25f)
        {
            state.StuckTicks = Math.Max(0, state.StuckTicks - ModernNoNextTickDecay);
        }
        else
        {
            state.StuckTicks += 1;
        }

        state.PreviousTargetDistance = currentTargetDistance;
    }

    private static bool TryApplyClientBot2020CompatReanchor(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination,
        ClientBotNavPoints navPoints,
        int[] goalWeights,
        BotMemory memory,
        ClientBot2020CompatState state,
        BotNavigationNode currentNode,
        out string selectionReason)
    {
        selectionReason = state.DebugDecisionReason;
        var horizontalSpeed = player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond;
        var hasGroundContact = CompatHasGroundContact(world, player);
        var currentNodeFeetY = GetModernNodeFeetY(navPoints, currentNode);
        var verticalGapToPoint = player.Bottom - currentNodeFeetY;
        if (verticalGapToPoint > 36f)
        {
            state.DropGapTicks += 1;
        }
        else
        {
            state.DropGapTicks = Math.Max(0, state.DropGapTicks - ModernNoNextTickDecay);
        }

        var allowReanchor = memory.ReanchorTicksRemaining <= 0;
        var reanchorNeeded = false;
        if (allowReanchor
            && (verticalGapToPoint > 104f
                || (verticalGapToPoint > 52f
                    && hasGroundContact
                    && MathF.Abs(horizontalSpeed) < 0.35f
                    && state.DropGapTicks > 12)))
        {
            reanchorNeeded = true;
            selectionReason = "drop_reanchor";
        }

        if (!reanchorNeeded
            && allowReanchor
            && DistanceSquared(player.X, player.Y, currentNode.X, currentNodeFeetY) > ModernCurrentPointReanchorDistance * ModernCurrentPointReanchorDistance
            && state.StuckTicks > 8)
        {
            reanchorNeeded = true;
            selectionReason = "far_reanchor";
        }

        if (!reanchorNeeded
            && allowReanchor
            && state.SecondAnchorCooldownTicks <= 0
            && state.CurrentPoint == state.NavChurnCurrentPoint
            && state.NavChurnTicks > 12
            && DistanceSquared(player.X, player.Y, state.NavChurnStartX, state.NavChurnStartY) < 60f * 60f
            && state.StuckTicks > 10
            && TryResolveClientBot2020CompatSecondAnchorPoint(world, player, destination, navPoints, goalWeights, state, currentNode, out var secondAnchorPointId))
        {
            var failedPointId = state.CurrentPoint;
            state.CurrentPoint = secondAnchorPointId;
            state.NextPoint = -1;
            state.NextPoint2 = -1;
            state.NextPoint3 = -1;
            state.StuckTicks = 0;
            state.NoNextPointTicks = 0;
            state.LoopBacktrackTicks = 0;
            state.StickyNextTicks = 0;
            state.NavChurnTicks = 0;
            state.NavChurnSwitchTicks = 0;
            state.NavChurnLockTicks = 0;
            state.NavChurnLockPoint = -1;
            state.NavChurnCurrentPoint = state.CurrentPoint;
            state.NavChurnStartX = player.X;
            state.NavChurnStartY = player.Y;
            state.PreviousTargetDistance = float.PositiveInfinity;
            state.SecondAnchorCooldownTicks = 90;
            state.SecondAnchorBlockPoint = failedPointId;
            state.SecondAnchorBlockTicks = 55;
            memory.ReanchorTicksRemaining = Math.Max(memory.ReanchorTicksRemaining, 20);
            state.OldPathPointKey = $"{Environment.TickCount64}_{Random.Shared.Next(1_000_000)}";
            selectionReason = "second_anchor_backtrack";
            return true;
        }

        if (!reanchorNeeded && allowReanchor && state.StuckTicks >= 28)
        {
            reanchorNeeded = true;
            selectionReason = "stuck_reanchor";
        }

        if (!reanchorNeeded)
        {
            return false;
        }

        state.CurrentPoint = -1;
        state.NextPoint = -1;
        state.NextPoint2 = -1;
        state.NextPoint3 = -1;
        state.StuckTicks = 0;
        state.DropGapTicks = 0;
        state.NoNextPointTicks = 0;
        state.LoopBacktrackTicks = 0;
        state.StickyNextTicks = 0;
        memory.ReanchorTicksRemaining = 45;
        state.PreviousTargetDistance = float.PositiveInfinity;
        state.OldPathPointKey = $"{Environment.TickCount64}_{Random.Shared.Next(1_000_000)}";
        TryAcquireNearestCompatCurrentPointIgnoringSight(player, navPoints, state);
        return true;
    }

    private static bool TryResolveClientBot2020CompatSecondAnchorPoint(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination,
        ClientBotNavPoints navPoints,
        int[] goalWeights,
        ClientBot2020CompatState state,
        BotNavigationNode currentNode,
        out int secondAnchorPointId)
    {
        secondAnchorPointId = -1;
        if (state.SecondLastCommittedPoint >= 0 && state.SecondLastCommittedPoint != currentNode.Id)
        {
            secondAnchorPointId = state.SecondLastCommittedPoint;
            return true;
        }

        if (state.LastCommittedPoint >= 0 && state.LastCommittedPoint != currentNode.Id)
        {
            secondAnchorPointId = state.LastCommittedPoint;
            return true;
        }

        if (state.NextPoint2 >= 0 && state.NextPoint2 != currentNode.Id)
        {
            secondAnchorPointId = state.NextPoint2;
            return true;
        }

        if (!navPoints.TryGetOutgoingConnections(currentNode.Id, out var outgoingEdges))
        {
            return false;
        }

        GetModernClassJumpProfile(player.ClassId, out var classJumpRange, out _, out var classJumpHeightTotal);
        var currentFeetY = GetModernNodeFeetY(navPoints, currentNode);
        var bestScore = float.PositiveInfinity;
        for (var edgeIndex = 0; edgeIndex < outgoingEdges.Count; edgeIndex += 1)
        {
            var edge = outgoingEdges[edgeIndex];
            if (!navPoints.TryGetPoint(edge.ToNodeId, out var candidateNode)
                || candidateNode.Id == currentNode.Id
                || candidateNode.Id == state.NextPoint)
            {
                continue;
            }

            var candidateFeetY = GetModernNodeFeetY(navPoints, candidateNode);
            var secondAnchorRise = MathF.Max(0f, currentFeetY - candidateFeetY);
            var secondAnchorRun = MathF.Abs(candidateNode.X - currentNode.X);
            var secondAnchorUsable = secondAnchorRise <= classJumpHeightTotal;
            if (secondAnchorUsable && secondAnchorRun > classJumpRange)
            {
                secondAnchorUsable = CompatLineHitsSolid(world, player, currentNode.X, currentFeetY + 2f, candidateNode.X, candidateFeetY + 2f);
            }

            if (!secondAnchorUsable)
            {
                continue;
            }

            var candidateScore = (GetModernGoalWeight(goalWeights, candidateNode.Id) * 1000f)
                + DistanceBetween(candidateNode.X, candidateFeetY, destination.X, destination.Y);
            if (candidateNode.Id == state.PreviousCurrentPoint || candidateNode.Id == state.PreviousNextPoint)
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

    private static bool TryPromoteClientBot2020CompatQueuedPoint(
        PlayerEntity player,
        ClientBotNavPoints navPoints,
        ClientBot2020CompatState state,
        out string selectionReason)
    {
        selectionReason = state.DebugDecisionReason;
        if (state.NextPoint < 0 || !navPoints.TryGetPoint(state.NextPoint, out var nextNode))
        {
            return false;
        }

        if (DistanceSquared(player.X, player.Y, nextNode.X, GetModernNodeFeetY(navPoints, nextNode)) >= ModernPointArrivalDistance * ModernPointArrivalDistance)
        {
            return false;
        }

        if (state.SecondAnchorBlockTicks > 0
            && state.NextPoint == state.SecondAnchorBlockPoint
            && state.CurrentPoint != state.SecondAnchorBlockPoint)
        {
            state.NextPoint = -1;
            state.NextPoint2 = -1;
            state.NextPoint3 = -1;
            selectionReason = "second_anchor_block_return";
            return true;
        }

        state.SecondLastCommittedPoint = state.LastCommittedPoint;
        state.LastCommittedPoint = state.CurrentPoint;
        state.CurrentPoint = state.NextPoint;
        if (state.NextPoint2 >= 0 && navPoints.TryGetPoint(state.NextPoint2, out var nextNode2))
        {
            state.NextPoint = state.NextPoint2;
            state.NextPoint2 = state.NextPoint3;
            state.NextPoint3 = -1;
            state.ClosestPointX = nextNode2.X;
            state.ClosestPointY = GetModernNodeFeetY(navPoints, nextNode2);
            selectionReason = "promote_np2";
        }
        else
        {
            state.NextPoint = -1;
            selectionReason = "promote_np";
        }

        return true;
    }

    private static void ApplyClientBot2020CompatClosestPointAnticipation(
        SimulationWorld world,
        PlayerEntity player,
        ClientBotNavPoints navPoints,
        ClientBot2020CompatState state,
        ref string selectionReason)
    {
        if (state.NextPoint < 0
            || state.NextPoint2 < 0
            || !navPoints.TryGetPoint(state.NextPoint, out var nextNode)
            || !navPoints.TryGetPoint(state.NextPoint2, out var secondNextNode))
        {
            return;
        }

        var nextFeetY = GetModernNodeFeetY(navPoints, nextNode);
        var groundedForAnticipate = CompatHasGroundContact(world, player);
        var horizontalSpeed = player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond;
        if (DistanceSquared(player.X, player.Y, nextNode.X, nextFeetY) >= ModernPointLookaheadDistance * ModernPointLookaheadDistance
            || !groundedForAnticipate
            || MathF.Abs(horizontalSpeed) <= ModernPointLookaheadMinimumSpeed)
        {
            return;
        }

        var secondFeetY = GetModernNodeFeetY(navPoints, secondNextNode);
        state.ClosestPointX = secondNextNode.X;
        state.ClosestPointY = secondFeetY;
        selectionReason = "anticipate_np2";
        if (state.NextPoint3 >= 0
            && navPoints.TryGetPoint(state.NextPoint3, out var thirdNextNode)
            && DistanceSquared(player.X, player.Y, secondNextNode.X, secondFeetY) < 20f * 20f)
        {
            state.ClosestPointX = thirdNextNode.X;
            state.ClosestPointY = GetModernNodeFeetY(navPoints, thirdNextNode);
            selectionReason = "anticipate_np3";
        }
    }

    private static bool TryFindNearestCompatGoalPoint(ClientBotNavPoints navPoints, (float X, float Y) destination, out int pointId)
    {
        pointId = -1;
        var minDistance = float.PositiveInfinity;
        for (var index = 0; index < navPoints.Points.Count; index += 1)
        {
            var node = navPoints.Points[index];
            var distance = DistanceBetween(node.X, node.Y, destination.X, destination.Y);
            if (distance >= minDistance)
            {
                continue;
            }

            minDistance = distance;
            pointId = node.Id;
        }

        return pointId >= 0;
    }

    private static void TryAcquireCompatCurrentPoint(
        SimulationWorld world,
        PlayerEntity player,
        ClientBotNavPoints navPoints,
        ClientBot2020CompatState state)
    {
        if (state.CurrentPoint >= 0
            && state.CurrentPoint < navPoints.Points.Count)
        {
            return;
        }

        state.CurrentPoint = -1;
        var localMinDistance = float.PositiveInfinity;
        for (var index = 0; index < navPoints.Points.Count; index += 1)
        {
            if (state.SecondAnchorBlockTicks > 0 && index == state.SecondAnchorBlockPoint)
            {
                continue;
            }

            var candidate = navPoints.Points[index];
            var localDistance = DistanceBetween(candidate.X, candidate.Y, player.X, player.Y);
            if (localDistance >= localMinDistance)
            {
                continue;
            }

            if (CompatLineHitsSolid(world, player, candidate.X, GetModernNodeFeetY(navPoints, candidate) - 12f, player.X, player.Y))
            {
                continue;
            }

            localMinDistance = localDistance;
            state.CurrentPoint = candidate.Id;
        }

        if (state.CurrentPoint >= 0)
        {
            state.DebugDecisionReason = "acquire_point";
            return;
        }

        localMinDistance = float.PositiveInfinity;
        for (var index = 0; index < navPoints.Points.Count; index += 1)
        {
            if (state.SecondAnchorBlockTicks > 0 && index == state.SecondAnchorBlockPoint)
            {
                continue;
            }

            var candidate = navPoints.Points[index];
            var localDistance = DistanceBetween(candidate.X, candidate.Y, player.X, player.Y);
            if (localDistance >= localMinDistance)
            {
                continue;
            }

            localMinDistance = localDistance;
            state.CurrentPoint = candidate.Id;
        }

        if (state.CurrentPoint >= 0)
        {
            state.DebugDecisionReason = "acquire_point";
        }
    }

    private static void TryAcquireNearestCompatCurrentPointIgnoringSight(
        PlayerEntity player,
        ClientBotNavPoints navPoints,
        ClientBot2020CompatState state)
    {
        state.CurrentPoint = -1;
        var localMinDistance = float.PositiveInfinity;
        for (var index = 0; index < navPoints.Points.Count; index += 1)
        {
            if (state.SecondAnchorBlockTicks > 0 && index == state.SecondAnchorBlockPoint)
            {
                continue;
            }

            var candidate = navPoints.Points[index];
            var localDistance = DistanceBetween(candidate.X, candidate.Y, player.X, player.Y);
            if (localDistance >= localMinDistance)
            {
                continue;
            }

            localMinDistance = localDistance;
            state.CurrentPoint = candidate.Id;
        }
    }

    private static void TrySelectCompatNextPoint(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination,
        ClientBotNavPoints navPoints,
        int[] goalWeights,
        ClientBot2020CompatState state,
        BotTimingProfile timing)
    {
        state.NextPoint = -1;
        state.NextPoint2 = -1;
        state.NextPoint3 = -1;
        if (!navPoints.TryGetPoint(state.CurrentPoint, out var currentNode)
            || !navPoints.TryGetOutgoingConnections(state.CurrentPoint, out var outgoing))
        {
            state.ClosestPointX = destination.X;
            state.ClosestPointY = destination.Y;
            state.DebugDecisionReason = "missing_currentpoint";
            return;
        }

        GetModernClassJumpProfile(player.ClassId, out var classJumpRange, out var classJumpHeight, out var classJumpHeightTotal);
        var pointWeight1 = GetModernGoalWeight(goalWeights, currentNode.Id);
        var bestNextPoint = -1;
        var bestNextScore = float.PositiveInfinity;
        var bestNextX = currentNode.X;
        var currentFeetY = GetModernNodeFeetY(navPoints, currentNode);
        var bestNextY = currentFeetY;
        var secondNextPoint = -1;
        var secondNextScore = float.PositiveInfinity;
        var secondNextX = bestNextX;
        var secondNextY = bestNextY;
        var feetY = player.Bottom;
        var horizontalSpeedSource = player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond;

        for (var edgeIndex = 0; edgeIndex < outgoing.Count; edgeIndex += 1)
        {
            var edge = outgoing[edgeIndex];
            if (!navPoints.TryGetPoint(edge.ToNodeId, out var candidateNode))
            {
                continue;
            }

            var pointWeight2 = GetModernGoalWeight(goalWeights, candidateNode.Id);
            if (pointWeight2 >= pointWeight1 || navPoints.IsReverseBlocked(candidateNode.Id, currentNode.Id))
            {
                continue;
            }

            var xPoint2 = candidateNode.X;
            var yPoint2 = GetModernNodeFeetY(navPoints, candidateNode);
            var jumpRiseNeeded = MathF.Max(0f, currentFeetY - yPoint2);
            var jumpDistanceNeeded = MathF.Abs(xPoint2 - currentNode.X);
            var connectionUsable = jumpRiseNeeded <= classJumpHeightTotal;
            if (connectionUsable && jumpDistanceNeeded > classJumpRange)
            {
                connectionUsable = CompatLineHitsSolid(world, player, currentNode.X, currentFeetY + 2f, xPoint2, yPoint2 + 2f);
            }

            if (!connectionUsable)
            {
                continue;
            }

            var candidateScore = pointWeight2 * 1000f + DistanceBetween(xPoint2, yPoint2, destination.X, destination.Y);
            if (candidateNode.Id == state.PreviousCurrentPoint)
            {
                candidateScore += 220f;
            }

            if (candidateNode.Id == state.PreviousNextPoint)
            {
                candidateScore -= 120f;
            }

            if (candidateNode.Id == state.LastCommittedPoint)
            {
                candidateScore += 340f;
            }

            if (state.CurrentPoint == state.PreviousCurrentPoint && candidateNode.Id != state.PreviousNextPoint && MathF.Abs(horizontalSpeedSource) < 1.2f)
            {
                candidateScore += 220f;
            }

            if (state.SecondAnchorBlockTicks > 0 && candidateNode.Id == state.SecondAnchorBlockPoint && state.CurrentPoint != state.SecondAnchorBlockPoint)
            {
                candidateScore += 5000f;
            }

            var candidateRiseFromPlayer = feetY - yPoint2;
            var candidateRunFromPlayer = MathF.Abs(xPoint2 - player.X);
            if (state.CurrentPoint == state.PreviousCurrentPoint && MathF.Abs(horizontalSpeedSource) < 1.2f && candidateRunFromPlayer < 96f)
            {
                var lipProbeDirection = MathF.Sign(xPoint2 - player.X);
                if (lipProbeDirection != 0f)
                {
                    var lipProbeX = player.X + (7f * lipProbeDirection);
                    var lipFrontFootBlocked = CompatLineHitsSolid(world, player, lipProbeX, feetY + 3f, lipProbeX, feetY - 1f);
                    var lipHeadRoom = !CompatLineHitsSolid(world, player, lipProbeX, feetY - (classJumpHeight + 2f), lipProbeX, feetY - 12f);
                    if (lipFrontFootBlocked && lipHeadRoom && candidateRiseFromPlayer >= 0f && candidateRiseFromPlayer <= 10f)
                    {
                        candidateScore += 320f;
                    }

                    if (lipFrontFootBlocked && lipHeadRoom && yPoint2 >= currentFeetY + 2f)
                    {
                        candidateScore -= 110f;
                    }
                }
            }

            if (candidateScore < bestNextScore)
            {
                secondNextPoint = bestNextPoint;
                secondNextScore = bestNextScore;
                secondNextX = bestNextX;
                secondNextY = bestNextY;
                bestNextPoint = candidateNode.Id;
                bestNextScore = candidateScore;
                bestNextX = xPoint2;
                bestNextY = yPoint2;
            }
            else if (candidateScore < secondNextScore)
            {
                secondNextPoint = candidateNode.Id;
                secondNextScore = candidateScore;
                secondNextX = xPoint2;
                secondNextY = yPoint2;
            }
        }

        if (bestNextPoint < 0)
        {
            state.NextPoint = -1;
            state.NextPoint2 = -1;
            state.NextPoint3 = -1;
            state.ClosestPointX = destination.X;
            state.ClosestPointY = destination.Y;
            state.DebugDecisionReason = "no_next_direct";
            return;
        }

        state.NextPoint = bestNextPoint;
        state.ClosestPointX = bestNextX;
        state.ClosestPointY = bestNextY;
        state.DebugDecisionReason = "nav_step";

        UpdateCompatChurn(navPoints, player, state);
        if (state.NavChurnLockTicks > 0
            && state.NavChurnLockPoint >= 0
            && state.NavChurnCurrentPoint == state.CurrentPoint
            && state.NextPoint != state.NavChurnLockPoint
            && navPoints.TryGetConnection(state.CurrentPoint, state.NavChurnLockPoint, out _)
            && navPoints.TryGetPoint(state.NavChurnLockPoint, out var churnNode))
        {
            state.NextPoint = state.NavChurnLockPoint;
            state.ClosestPointX = churnNode.X;
            state.ClosestPointY = GetModernNodeFeetY(navPoints, churnNode);
            state.DebugDecisionReason = "nav_step_churn_lock";
        }

        if (state.StickyNextTicks > 0
            && state.StickyCurrentPoint == state.CurrentPoint
            && state.StickyNextPoint >= 0
            && state.NextPoint != state.StickyNextPoint
            && secondNextPoint == state.StickyNextPoint
            && MathF.Abs(horizontalSpeedSource) < 1.2f
            && navPoints.TryGetPoint(state.StickyNextPoint, out var stickySecond))
        {
            state.NextPoint = state.StickyNextPoint;
            state.ClosestPointX = stickySecond.X;
            state.ClosestPointY = GetModernNodeFeetY(navPoints, stickySecond);
            state.DebugDecisionReason = "nav_step_sticky";
        }

        if (state.StickyNextTicks > 0
            && state.StickyCurrentPoint == state.CurrentPoint
            && state.StickyNextPoint >= 0
            && state.NextPoint != state.StickyNextPoint
            && MathF.Abs(horizontalSpeedSource) < 1.2f
            && navPoints.TryGetConnection(state.CurrentPoint, state.StickyNextPoint, out _)
            && navPoints.TryGetPoint(state.StickyNextPoint, out var stickyDirect))
        {
            state.NextPoint = state.StickyNextPoint;
            state.ClosestPointX = stickyDirect.X;
            state.ClosestPointY = GetModernNodeFeetY(navPoints, stickyDirect);
            state.DebugDecisionReason = "nav_step_sticky";
        }

        if (state.CurrentPoint == state.PreviousNextPoint && state.NextPoint == state.PreviousCurrentPoint)
        {
            state.LoopBacktrackTicks += 1;
            if (state.LoopBacktrackTicks > 3 && secondNextPoint >= 0)
            {
                state.NextPoint = secondNextPoint;
                state.ClosestPointX = secondNextX;
                state.ClosestPointY = secondNextY;
                state.DebugDecisionReason = "nav_step_backtrack_avoid";
            }
        }
        else
        {
            state.LoopBacktrackTicks = Math.Max(0, state.LoopBacktrackTicks - 2);
        }

        state.NextPoint2 = -1;
        state.NextPoint3 = -1;
        state.StickyCurrentPoint = state.CurrentPoint;
        state.StickyNextPoint = state.NextPoint;
        state.StickyNextTicks = state.StuckTicks > 6 ? 0 : 10;
    }

    private static void UpdateCompatChurn(ClientBotNavPoints navPoints, PlayerEntity player, ClientBot2020CompatState state)
    {
        if (state.CurrentPoint != state.NavChurnCurrentPoint)
        {
            state.NavChurnCurrentPoint = state.CurrentPoint;
            state.NavChurnTicks = 0;
            state.NavChurnSwitchTicks = 0;
            state.NavChurnLockPoint = -1;
            state.NavChurnLockTicks = 0;
            state.NavChurnStartX = player.X;
            state.NavChurnStartY = player.Y;
        }
        else
        {
            state.NavChurnTicks += 1;
            if (state.PreviousNextPoint >= 0 && state.NextPoint != state.PreviousNextPoint)
            {
                state.NavChurnSwitchTicks += 1;
            }
            else
            {
                state.NavChurnSwitchTicks = Math.Max(0, state.NavChurnSwitchTicks - 1);
            }

            if (state.NavChurnLockTicks <= 0
                && state.NavChurnTicks > 16
                && state.NavChurnSwitchTicks > 7
                && DistanceBetween(player.X, player.Y, state.NavChurnStartX, state.NavChurnStartY) < 52f
                && state.PreviousNextPoint >= 0
                && navPoints.TryGetConnection(state.CurrentPoint, state.PreviousNextPoint, out _))
            {
                state.NavChurnLockPoint = state.PreviousNextPoint;
                state.NavChurnLockTicks = 18;
            }
        }
    }

    private static void TryPopulateCompatLookahead(ClientBotNavPoints navPoints, int[] goalWeights, ClientBot2020CompatState state)
    {
        state.NextPoint2 = -1;
        state.NextPoint3 = -1;
        if (state.NextPoint < 0
            || !navPoints.TryGetOutgoingConnections(state.NextPoint, out var nextOutgoing))
        {
            return;
        }

        var lookaheadPointWeight = GetModernGoalWeight(goalWeights, state.NextPoint);
        for (var index = 0; index < nextOutgoing.Count; index += 1)
        {
            var lookaheadNode = nextOutgoing[index].ToNodeId;
            var lookaheadWeight = GetModernGoalWeight(goalWeights, lookaheadNode);
            if (lookaheadWeight < lookaheadPointWeight)
            {
                state.NextPoint2 = lookaheadNode;
                lookaheadPointWeight = lookaheadWeight;
            }
        }

        if (state.NextPoint2 < 0
            || !navPoints.TryGetOutgoingConnections(state.NextPoint2, out var secondOutgoing))
        {
            return;
        }

        lookaheadPointWeight = GetModernGoalWeight(goalWeights, state.NextPoint2);
        for (var index = 0; index < secondOutgoing.Count; index += 1)
        {
            var lookaheadNode = secondOutgoing[index].ToNodeId;
            var lookaheadWeight = GetModernGoalWeight(goalWeights, lookaheadNode);
            if (lookaheadWeight < lookaheadPointWeight)
            {
                state.NextPoint3 = lookaheadNode;
                lookaheadPointWeight = lookaheadWeight;
            }
        }
    }

    private static void UpdateCompatNoNextPoint(ClientBotNavPoints navPoints, ClientBot2020CompatState state, BotTimingProfile timing)
    {
        _ = navPoints;
        _ = timing;
        if (state.CurrentPoint >= 0 && state.NextPoint < 0)
        {
            state.NoNextPointTicks += 1;
        }
        else
        {
            state.NoNextPointTicks = Math.Max(0, state.NoNextPointTicks - 2);
        }
    }

    private static void TryForceCompatNeighborStep(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination,
        ClientBotNavPoints navPoints,
        int[]? goalWeights,
        ClientBot2020CompatState state,
        BotTimingProfile timing)
    {
        _ = timing;
        if (goalWeights is null
            || state.NextPoint >= 0
            || state.CurrentPoint < 0
            || state.NoNextPointTicks <= 8
            || !navPoints.TryGetPoint(state.CurrentPoint, out var currentNode)
            || !navPoints.TryGetOutgoingConnections(state.CurrentPoint, out var outgoing))
        {
            return;
        }

        var pointWeight1 = GetModernGoalWeight(goalWeights, state.CurrentPoint);
        var forcedNextPoint = -1;
        var forcedNextScore = float.PositiveInfinity;
        GetModernClassJumpProfile(player.ClassId, out var classJumpRange, out _, out var classJumpHeightTotal);
        var currentFeetY = GetModernNodeFeetY(navPoints, currentNode);
        for (var edgeIndex = 0; edgeIndex < outgoing.Count; edgeIndex += 1)
        {
            var candidateId = outgoing[edgeIndex].ToNodeId;
            if (navPoints.IsReverseBlocked(candidateId, state.CurrentPoint)
                || !navPoints.TryGetPoint(candidateId, out var candidateNode))
            {
                continue;
            }

            var xPoint2 = candidateNode.X;
            var yPoint2 = GetModernNodeFeetY(navPoints, candidateNode);
            var jumpRiseNeeded = MathF.Max(0f, currentFeetY - yPoint2);
            var jumpDistanceNeeded = MathF.Abs(xPoint2 - currentNode.X);
            var connectionUsable = jumpRiseNeeded <= classJumpHeightTotal;
            if (connectionUsable && jumpDistanceNeeded > classJumpRange)
            {
                connectionUsable = CompatLineHitsSolid(world, player, currentNode.X, currentFeetY + 2f, xPoint2, yPoint2 + 2f);
            }

            if (!connectionUsable)
            {
                continue;
            }

            var pointWeight2 = GetModernGoalWeight(goalWeights, candidateId);
            var forcedWeightPenalty = Math.Max(0, pointWeight2 - pointWeight1) * 90f;
            var forcedScore = DistanceBetween(xPoint2, yPoint2, destination.X, destination.Y) + forcedWeightPenalty;
            if (candidateId == state.PreviousCurrentPoint)
            {
                forcedScore += 120f;
            }

            if (candidateId == state.PreviousNextPoint)
            {
                forcedScore += 60f;
            }

            if (candidateId == state.LastCommittedPoint)
            {
                forcedScore += 180f;
            }

            if (forcedScore < forcedNextScore)
            {
                forcedNextScore = forcedScore;
                forcedNextPoint = candidateId;
            }
        }

        if (forcedNextPoint >= 0 && navPoints.TryGetPoint(forcedNextPoint, out var forcedNode))
        {
            state.NextPoint = forcedNextPoint;
            state.ClosestPointX = forcedNode.X;
            state.ClosestPointY = GetModernNodeFeetY(navPoints, forcedNode);
            state.NextPoint2 = -1;
            state.NextPoint3 = -1;
            state.NoNextPointTicks = Math.Max(0, state.NoNextPointTicks - 6);
            state.DebugDecisionReason = "force_neighbor_step";
        }
        else if (state.NoNextPointTicks > 24)
        {
            state.CurrentPoint = -1;
            state.OldPathPointKey = $"{Environment.TickCount}_{state.NoNextPointTicks}";
            state.NoNextPointTicks = 0;
            state.DebugDecisionReason = "force_reacquire_nonp";
        }
    }

    private static void TryCompatReacquireCollision(
        SimulationWorld world,
        PlayerEntity player,
        ClientBotNavPoints navPoints,
        ClientBot2020CompatState state)
    {
        if (state.CurrentPoint < 0)
        {
            return;
        }

        if (!navPoints.TryGetPoint(state.CurrentPoint, out var currentNode))
        {
            return;
        }

        if (!CompatLineHitsSolid(world, player, currentNode.X, GetModernNodeFeetY(navPoints, currentNode) - 12f, player.X, player.Y)
            || !CompatLineHitsSolid(world, player, state.ClosestPointX, state.ClosestPointY - 12f, player.X, player.Y))
        {
            return;
        }

        var localMinDistance = float.PositiveInfinity;
        var reacquiredPoint = -1;
        for (var index = 0; index < navPoints.Points.Count; index += 1)
        {
            var candidate = navPoints.Points[index];
            var localDistance = DistanceBetween(candidate.X, candidate.Y, player.X, player.Y);
            if (localDistance >= localMinDistance)
            {
                continue;
            }

            localMinDistance = localDistance;
            reacquiredPoint = candidate.Id;
        }

        if (reacquiredPoint >= 0)
        {
            state.CurrentPoint = reacquiredPoint;
        }

        state.DebugDecisionReason = "reacquire_collision";
    }

    private bool ResolveClientBot2020CompatJump(
        SimulationWorld world,
        PlayerEntity player,
        NavigationDecision navigationDecision,
        ClientBotNavPoints navPoints,
        BotMemory memory,
        BotTimingProfile timing,
        int horizontal)
    {
        var state = memory.ClientBot2020Compat ??= new ClientBot2020CompatState();
        memory.ModernJumpDebug = "compat:start";
        if (navigationDecision.ForceJump)
        {
            memory.ModernJumpDebug = "compat:force";
            return TriggerModernBotJump(memory, timing);
        }

        if (memory.JumpCooldownTicks > 0)
        {
            memory.ModernJumpDebug = $"compat:cooldown:{memory.JumpCooldownTicks}";
            return false;
        }

        var hasGroundContact = CompatHasGroundContact(world, player);
        if (memory.UnstickTicks > 0 && hasGroundContact)
        {
            memory.ModernJumpDebug = "compat:unstick";
            return TriggerModernBotJump(memory, timing);
        }

        GetModernClassJumpProfile(player.ClassId, out var jumpRange, out var jumpHeight, out var jumpHeightTotal);
        var targetX = state.ClosestPointX;
        var targetY = state.ClosestPointY;
        var feetY = player.Bottom;
        var horizontalSpeed = player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond;
        var previousX = memory.PreviousObservedX;
        var previousY = memory.PreviousObservedY;
        var hasPreviousPosition = memory.HasObservedPosition;

        if (hasGroundContact)
        {
            if (horizontal != 0
                && state.StuckTicks > 30
                && MathF.Abs(horizontalSpeed) <= 0.1f
                && CompatHasJumpHeadClear(world, player, jumpHeight))
            {
                state.CurrentPoint = -1;
                state.NextPoint = -1;
                memory.ModernJumpDebug = "compat:hard_stuck_jump";
                return TriggerModernBotJump(memory, timing);
            }

            if (horizontal != 0
                && state.StuckTicks > 6
                && hasPreviousPosition
                && DistanceSquared(player.X, player.Y, previousX, previousY) <= 0.04f
                && MathF.Abs(horizontalSpeed) > 0.6f
                && CompatHasJumpHeadClear(world, player, jumpHeight)
                && CompatHasForwardFootBlock(world, player, horizontal, 7f))
            {
                memory.ModernJumpDebug = "compat:stuck_pulse";
                return TriggerModernBotJump(memory, timing);
            }

            if (state.CurrentPoint >= 0
                && state.NextPoint >= 0
                && navPoints.TryGetPoint(state.CurrentPoint, out var currentNode)
                && navPoints.TryGetPoint(state.NextPoint, out var nextNode)
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
                memory.ModernJumpDebug = "compat:interp";
                return TriggerModernBotJump(memory, timing);
            }

            if (horizontal != 0
                && TryShouldUseModernLipJump(world, player, (targetX, targetY), targetY, horizontal, state.StuckTicks, jumpHeight))
            {
                memory.ModernJumpDebug = "compat:lip";
                return TriggerModernBotJump(memory, timing);
            }

            if (horizontal != 0
                && TryShouldUseModernSlopeJump(world, player, (targetX, targetY), targetY, horizontal, jumpRange, jumpHeight, jumpHeightTotal))
            {
                memory.ModernJumpDebug = "compat:slope";
                return TriggerModernBotJump(memory, timing);
            }

            if (horizontal != 0
                && TryShouldUseModernHallJump(world, player, (targetX, targetY), targetY, horizontal, jumpRange, jumpHeight))
            {
                memory.ModernJumpDebug = "compat:hall";
                return TriggerModernBotJump(memory, timing);
            }

            if (horizontal != 0 && CompatHasWaistObstacleAhead(world, player, horizontal))
            {
                if (MathF.Abs(horizontalSpeed) > 0.0001f
                    && MathF.Abs(MathF.Sign(horizontal) - MathF.Sign(horizontalSpeed)) < 2f)
                {
                    if (IsModernStairStepAhead(world, player, horizontal, horizontalSpeed))
                    {
                        memory.ModernJumpDebug = "compat:waist";
                        return TriggerModernBotJump(memory, timing);
                    }
                }
                else if (state.StuckTicks > 10
                    && CompatHasGroundAhead(world, player, horizontal, 15f)
                    && (feetY - targetY) >= 24f
                    && MathF.Abs(targetX - player.X) <= 36f
                    && CompatHasJumpHeadClear(world, player, jumpHeight)
                    && CompatIsWallStepAhead(world, player, horizontal))
                {
                    memory.ModernJumpDebug = "compat:waist_stuckrise";
                    return TriggerModernBotJump(memory, timing);
                }
            }

        if (horizontal != 0 && !CompatHasGroundAhead(world, player, horizontal, 15f))
            {
                var jumpDistanceNeeded = MathF.Abs(targetX - player.X);
                if (targetY < player.Y)
                {
                    var jumpRiseNeeded = feetY - targetY;
                    if (jumpRiseNeeded <= jumpHeightTotal
                        && jumpDistanceNeeded <= jumpRange
                        && MathF.Sign(targetX - player.X) == horizontal
                        && (MathF.Abs(horizontalSpeed) <= 0.25f || MathF.Sign(horizontalSpeed) == horizontal))
                    {
                        memory.ModernJumpDebug = "compat:hole_up";
                        return TriggerModernBotJump(memory, timing);
                    }
                }
                else if (MathF.Abs(targetY - feetY) <= 18f
                    && !CompatLineHitsSolid(world, player, targetX - (3f * horizontal), targetY, player.X + horizontalSpeed + (2f * horizontal), feetY)
                    && jumpDistanceNeeded <= jumpRange
                    && (MathF.Abs(horizontalSpeed) <= 0.25f || MathF.Sign(horizontalSpeed) == horizontal))
                {
                    memory.ModernJumpDebug = "compat:hole_flat";
                    return TriggerModernBotJump(memory, timing);
                }
            }

            var jumpLength = horizontalSpeed * player.JumpStrength;
            if (CompatLineHitsSolid(world, player, player.X + jumpLength, feetY - (jumpHeight + 4f), player.X + jumpLength, feetY - 18f)
                && !CompatPointHitsSolid(world, player, player.X + jumpLength, feetY - (jumpHeight + 12f))
                && targetY < player.Y
                && horizontal != 0
                && MathF.Abs(horizontalSpeed) > 0.0001f
                && MathF.Abs(MathF.Sign(horizontal) - MathF.Sign(horizontalSpeed)) < 2f
                && IsModernStairStepAhead(world, player, horizontal, horizontalSpeed))
            {
                memory.ModernJumpDebug = "compat:platform";
                return TriggerModernBotJump(memory, timing);
            }
        }

        if (player.ClassId == PlayerClass.Scout
            && !hasGroundContact
            && targetY < player.Y)
        {
            var jumpDistanceNeeded = MathF.Abs(targetX - player.X);
            var jumpRiseNeeded = feetY - targetY;
            if (jumpRiseNeeded <= jumpHeightTotal
                && jumpDistanceNeeded <= jumpRange
                && horizontal != 0
                && MathF.Sign(targetX - player.X) == horizontal)
            {
                memory.ModernJumpDebug = "compat:scout_air";
                return TriggerModernBotJump(memory, timing);
            }
        }

        var waistProbe = horizontal != 0 && CompatHasWaistObstacleAhead(world, player, horizontal);
        var stairProbe = horizontal != 0 && IsModernStairStepAhead(world, player, horizontal, horizontalSpeed);
        var groundAheadProbe = horizontal != 0 && CompatHasGroundAhead(world, player, horizontal, 15f);
        memory.ModernJumpDebug = $"compat:nojump:g{hasGroundContact}:h{horizontal}:dx{targetX - player.X:0}:rise{feetY - targetY:0}:hs{horizontalSpeed:0.0}:st{state.StuckTicks}:w{waistProbe}:s{stairProbe}:ga{groundAheadProbe}";
        return false;
    }

    private static bool CompatIsWallStepAhead(SimulationWorld world, PlayerEntity player, int stepDirection)
    {
        if (stepDirection == 0)
        {
            return false;
        }

        var x = player.X;
        while (!CompatPointHitsSolid(world, player, x, player.Bottom - 1f) && MathF.Abs(player.X - x) < 60f)
        {
            x += stepDirection;
        }

        return CompatPointHitsSolid(world, player, x + (5f * stepDirection), player.Bottom - 7f)
            || CompatPointHitsSolid(world, player, x + (11f * stepDirection), player.Bottom - 13f);
    }

    private ModernCombatTarget? ResolveClientBot2020CompatCombatTarget(
        SimulationWorld world,
        PlayerEntity player,
        PlayerTeam team,
        IReadOnlyList<PlayerEntity> allPlayers)
    {
        ModernCombatTarget? bestTarget = null;
        var bestDistance = 375f;
        for (var index = 0; index < world.Generators.Count; index += 1)
        {
            var candidate = world.Generators[index];
            if (candidate.Team == team || candidate.IsDestroyed)
            {
                continue;
            }

            var candidateX = candidate.Marker.CenterX;
            var candidateY = candidate.Marker.CenterY;
            if (!CompatHasObstacleLineOfSight(world, player, player.X, player.Y, candidateX, candidateY))
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

            if (!CompatHasObstacleLineOfSight(world, player, player.X, player.Y, candidate.X, candidate.Y))
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

            if (!CompatHasObstacleLineOfSight(world, player, player.X, player.Y, candidate.X, candidate.Y))
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

    private static PlayerEntity? FindBestClientBot2020CompatMedicBuddyTarget(
        PlayerEntity player,
        PlayerTeam team,
        IReadOnlyList<PlayerEntity> allPlayers)
    {
        if (player.ClassId != PlayerClass.Medic)
        {
            return null;
        }

        PlayerEntity? bestTarget = null;
        var bestDistance = float.PositiveInfinity;
        for (var index = 0; index < allPlayers.Count; index += 1)
        {
            var candidate = allPlayers[index];
            if (!candidate.IsAlive
                || candidate.Team != team
                || candidate.Id == player.Id
                || candidate.ClassId == PlayerClass.Soldier
                || candidate.IsSpyCloaked)
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

        return bestTarget;
    }

    private static bool ResolveClientBot2020CompatBuildSentry(
        SimulationWorld world,
        PlayerEntity player,
        (float X, float Y) destination)
    {
        return player.ClassId == PlayerClass.Engineer
            && !HasOwnedSentry(world, player.Id)
            && player.Metal >= 100f
            && DistanceBetween(player.X, player.Y, destination.X, destination.Y) < 60f;
    }

    private static bool ResolveClientBot2020CompatDropIntel(
        PlayerEntity player,
        PlayerEntity? medicBuddyTarget)
    {
        return medicBuddyTarget is not null
            && DistanceBetween(player.X, player.Y, medicBuddyTarget.X, medicBuddyTarget.Y) < 60f
            && medicBuddyTarget.IsCarryingIntel;
    }

    private (float X, float Y) ResolveClientBot2020CompatAimTarget(
        PlayerEntity player,
        (float X, float Y) movementDestination,
        ModernCombatTarget? combatTarget,
        PlayerEntity? healTarget)
    {
        if (healTarget is not null)
        {
            return (healTarget.X, healTarget.Y);
        }

        if (combatTarget is not ModernCombatTarget target)
        {
            var spinAngle = (Environment.TickCount64 / 1000f * 360f) % 360f;
            return (player.X + LengthDirX(64f, spinAngle), player.Y + LengthDirY(64f, spinAngle));
        }

        var aimX = target.X;
        var aimY = target.Kind == ModernCombatTargetKind.Player && target.Player is not null
            ? target.Player.Y
            : target.Y;
        var xShift = MathF.Abs(aimX - player.X);
        float compensationModifier = player.ClassId switch
        {
            PlayerClass.Scout or PlayerClass.Engineer or PlayerClass.Heavy or PlayerClass.Demoman or PlayerClass.Medic => 2f * MathF.Sqrt(xShift / 8f),
            PlayerClass.Spy => 0.8f * MathF.Sqrt(xShift / 8f),
            _ => 0f,
        };
        var aimDirection = PointDirectionDegrees(player.X, player.Y, aimX, aimY);
        if (aimDirection > 90f && aimDirection < 270f)
        {
            aimDirection -= compensationModifier;
        }
        else
        {
            aimDirection += compensationModifier;
        }

        var aimDistance = DistanceBetween(player.X, player.Y, aimX, aimY);
        return (player.X + LengthDirX(aimDistance, aimDirection), player.Y + LengthDirY(aimDistance, aimDirection));
    }

    private void ResolveClientBot2020CompatFire(
        SimulationWorld world,
        PlayerEntity player,
        IReadOnlyList<PlayerEntity> allPlayers,
        ModernCombatTarget? combatTarget,
        PlayerEntity? healTarget,
        BotMemory memory,
        bool isBeingHealed,
        BotTimingProfile timing,
        ref int horizontal,
        ref bool jump,
        out bool firePrimary,
        out bool fireSecondary)
    {
        firePrimary = false;
        fireSecondary = false;

        if (player.ClassId == PlayerClass.Medic)
        {
            firePrimary = true;
            memory.ModernBeenHealingTicks += 1;
            if (healTarget is not null
                && player.MedicHealTargetId.HasValue
                && player.MedicHealTargetId.Value != healTarget.Id
                && memory.ModernBeenHealingTicks > memory.ModernBeenHealingSwitchTicks)
            {
                firePrimary = false;
                memory.ModernBeenHealingTicks = 0;
            }

            if (healTarget is not null)
            {
                if (player.IsMedicUberReady && (healTarget.Health < 50 || player.Health < 40))
                {
                    firePrimary = true;
                    fireSecondary = true;
                }

                return;
            }
        }
        else
        {
            memory.ModernBeenHealingTicks = 0;
        }

        if (combatTarget is not ModernCombatTarget target)
        {
            if (player.ClassId == PlayerClass.Heavy && ShouldModernHeavyEat(player, isBeingHealed, ModernHeavyIdleEatHealth))
            {
                fireSecondary = true;
            }
            else if (player.ClassId == PlayerClass.Demoman)
            {
                fireSecondary = ShouldModernDetonateMines(world, player, allPlayers);
            }
            else if (player.ClassId == PlayerClass.Sniper && player.IsSniperScoped)
            {
                fireSecondary = true;
            }

            return;
        }

        var targetDistance = DistanceBetween(player.X, player.Y, target.X, target.Y);
        if (player.ClassId == PlayerClass.Sniper)
        {
            if (targetDistance >= ModernSniperDangerDistance)
            {
                if (!player.IsSniperScoped)
                {
                    fireSecondary = true;
                }
            }
            else if (player.IsSniperScoped)
            {
                fireSecondary = true;
            }
        }

        if (player.ClassId == PlayerClass.Heavy && ShouldModernHeavyEat(player, isBeingHealed, ModernHeavyCombatEatHealth))
        {
            firePrimary = false;
            fireSecondary = true;
            return;
        }

        if (player.ClassId == PlayerClass.Medic)
        {
            firePrimary = false;
            fireSecondary = true;
            return;
        }

        if (player.ClassId == PlayerClass.Sniper && player.IsSniperScoped)
        {
            if (player.SniperChargeTicks >= memory.ModernZoomToShootTicks)
            {
                memory.ModernZoomToShootTicks = NextModernZoomToShootTicks(memory.ModernCombatTicksPerSecond);
                firePrimary = true;
            }

            return;
        }

        if (player.ClassId == PlayerClass.Demoman)
        {
            firePrimary = true;
            fireSecondary = ShouldModernDetonateMines(world, player, allPlayers);
        }
        else
        {
            firePrimary = true;
        }

        ApplyModernSoldierCloseRangeAdjustment(world, player, combatTarget, memory, firePrimary, timing, ref horizontal, ref jump);
    }

    private int ResolveClientBot2020CompatHorizontalMovement(
        SimulationWorld world,
        PlayerEntity player,
        ModernPathSelection objectiveSelection,
        ClientBotNavPoints navPoints,
        BotMemory memory,
        PlayerEntity? enemyTarget)
    {
        var state = memory.ClientBot2020Compat ??= new ClientBot2020CompatState();
        memory.ModernMoveDebug = "compat:start";
        var hasGroundContact = CompatHasGroundContact(world, player);
        if (objectiveSelection.IsCaptureObjective
            && objectiveSelection.CaptureObjective is ModernCaptureObjective captureObjective)
        {
            var enemyNearbyCapture = CompatHasCaptureEnemyNearby(world, player, enemyTarget);
            var captureTargetPoint = ResolveModernCaptureSettleTarget(captureObjective);
            var captureDx = MathF.Abs(player.X - captureTargetPoint.X);
            var captureDy = MathF.Abs(player.Bottom - captureTargetPoint.Y);
            if (hasGroundContact && captureDx < 24f && captureDy < 56f && !enemyNearbyCapture)
            {
                var horizontalSpeed = player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond;
                if (MathF.Abs(horizontalSpeed) > 1.1f
                    && MathF.Sign(horizontalSpeed) == MathF.Sign(player.X - captureTargetPoint.X))
                {
                    state.Doubleback = false;
                    memory.ModernMoveDebug = "capture_brake";
                    return -(int)MathF.Sign(horizontalSpeed);
                }
            }
        }

        var targetX = state.ClosestPointX;
        var targetY = state.ClosestPointY;
        var feetY = player.Bottom;
        var horizontalSpeedSource = player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond;
        var xTolerance = state.CaptureObjectiveMode ? 12f : 20f;
        if (player.X - xTolerance > targetX
            || (player.X > targetX && (MathF.Abs(horizontalSpeedSource) < 0.05f || (feetY - 1f) > targetY)))
        {
            if (horizontalSpeedSource > 1f && (feetY - 3f) >= targetY)
            {
                state.Doubleback = true;
                memory.ModernMoveDebug = "left_doubleback";
                return 0;
            }

            if (state.Doubleback)
            {
                if (hasGroundContact || MathF.Abs(horizontalSpeedSource) < 0.05f)
                {
                    state.Doubleback = false;
                    memory.ModernMoveDebug = "left_doubleback";
                    return 0;
                }

                memory.ModernMoveDebug = "left_doubleback_wait";
                return 0;
            }

            memory.ModernMoveDebug = "left";
            return -1;
        }

        if (player.X + xTolerance < targetX
            || (player.X < targetX && (MathF.Abs(horizontalSpeedSource) < 0.05f || (feetY - 1f) > targetY)))
        {
            if (horizontalSpeedSource < -1f && (feetY - 3f) >= targetY)
            {
                state.Doubleback = true;
                memory.ModernMoveDebug = "right_doubleback";
                return 0;
            }

            if (state.Doubleback)
            {
                if (hasGroundContact || MathF.Abs(horizontalSpeedSource) < 0.05f)
                {
                    state.Doubleback = false;
                    memory.ModernMoveDebug = "right_doubleback";
                    return 0;
                }

                memory.ModernMoveDebug = "right_doubleback_wait";
                return 0;
            }

            memory.ModernMoveDebug = "right";
            return 1;
        }

        state.Doubleback = false;
        if (state.NextPoint >= 0 && navPoints.TryGetPoint(state.NextPoint, out var nextNode))
        {
            var direction = 0;
            if (player.HorizontalSpeed > 0f && player.X < nextNode.X)
            {
                direction = 1;
                memory.ModernMoveDebug = "reached_momentum_right";
            }
            else if (player.HorizontalSpeed < 0f && player.X > nextNode.X)
            {
                direction = -1;
                memory.ModernMoveDebug = "reached_momentum_left";
            }
            else
            {
                memory.ModernMoveDebug = "reached_stop";
            }

            state.SecondLastCommittedPoint = state.LastCommittedPoint;
            state.LastCommittedPoint = state.CurrentPoint;
            state.CurrentPoint = state.NextPoint;
            SyncClientBot2020CompatStateToMemory(state, memory);
            return direction;
        }

        memory.ModernMoveDebug = "no_next_stop";
        state.CurrentPoint = -1;
        SyncClientBot2020CompatStateToMemory(state, memory);
        return 0;
    }

    private static bool CompatLineHitsSolid(
        SimulationWorld world,
        PlayerEntity player,
        float originX,
        float originY,
        float targetX,
        float targetY)
    {
        var distance = DistanceBetween(originX, originY, targetX, targetY);
        var obstacleGrid = GetModernObstacleGrid(world.Level, player.Team, player.IsCarryingIntel);
        if (distance <= 0.0001f)
        {
            return obstacleGrid.ContainsPoint(originX, originY);
        }

        return obstacleGrid.LineHitsObstacle(originX, originY, targetX, targetY);
    }

    private static bool CompatPointHitsSolid(
        SimulationWorld world,
        PlayerEntity player,
        float x,
        float y)
    {
        return GetModernObstacleIndex(world.Level, player.Team, player.IsCarryingIntel).ContainsPoint(x, y);
    }

    private static bool CompatHasGroundContact(SimulationWorld world, PlayerEntity player)
    {
        var feetY = player.Bottom;
        return CompatLineHitsSolid(world, player, player.X - 6f, feetY + 3f, player.X + 6f, feetY + 3f);
    }

    private static bool CompatHasJumpHeadClear(SimulationWorld world, PlayerEntity player, float jumpHeight)
    {
        var feetY = player.Bottom;
        return !CompatLineHitsSolid(world, player, player.X, feetY - 2f, player.X, feetY - (jumpHeight + 2f));
    }

    private static bool CompatHasForwardFootBlock(SimulationWorld world, PlayerEntity player, int horizontal, float probeDistance)
    {
        var feetY = player.Bottom;
        var probeX = player.X + (probeDistance * horizontal);
        return CompatLineHitsSolid(world, player, probeX, feetY - 1f, probeX, feetY + 4f);
    }

    private static bool CompatHasGroundAhead(SimulationWorld world, PlayerEntity player, int horizontal, float probeDistance)
    {
        var feetY = player.Bottom;
        var probeX = player.X + (probeDistance * horizontal);
        return CompatLineHitsSolid(world, player, probeX, feetY, probeX, feetY + 12f);
    }

    private static bool CompatHasWaistObstacleAhead(SimulationWorld world, PlayerEntity player, int horizontal)
    {
        var probeX = player.X + (15f * horizontal);
        var legacyProbeTop = player.Y - player.CollisionBottomOffset + 6f;
        return CompatLineHitsSolid(world, player, probeX, legacyProbeTop, probeX, player.Bottom - 12f);
    }

    private static bool CompatHasObstacleLineOfSight(
        SimulationWorld world,
        PlayerEntity player,
        float originX,
        float originY,
        float targetX,
        float targetY)
    {
        return !CompatLineHitsSolid(world, player, originX, originY, targetX, targetY);
    }

    private static bool CompatHasCaptureEnemyNearby(
        SimulationWorld world,
        PlayerEntity player,
        PlayerEntity? currentEnemyTarget)
    {
        if (currentEnemyTarget is not null
            && currentEnemyTarget.IsAlive
            && currentEnemyTarget.Team != player.Team
            && DistanceBetween(player.X, player.Y, currentEnemyTarget.X, currentEnemyTarget.Y) < ModernCaptureEnemyNearbyDistance
            && CompatHasObstacleLineOfSight(world, player, currentEnemyTarget.X, currentEnemyTarget.Y, player.X, player.Y))
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

            if (CompatHasObstacleLineOfSight(world, player, candidate.X, candidate.Y, player.X, player.Y))
            {
                return true;
            }
        }

        return false;
    }
}
