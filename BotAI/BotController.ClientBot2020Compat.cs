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
        var captureHoldState = ResolveModernCaptureHoldState(world, player, objectiveSelection, memory, timing.FixedDeltaSeconds);
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
            state.ClosestPointX = captureHoldState.TargetX;
            state.ClosestPointY = captureHoldState.TargetY;
            state.DebugDecisionReason = "capture_zone_hold";
            ClearClientBot2020CompatFailureInstrumentation(state);
            SyncClientBot2020CompatStateToMemory(state, memory);
            return new NavigationDecision(
                (captureHoldState.TargetX, captureHoldState.TargetY),
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
            UpdateClientBot2020CompatFailureInstrumentation(player, state);
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
                    state.GoalWeights = navPoints.GetGoalWeights(closestPointId, ModernMaximumWeightDepth, player.ClassId);
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

        UpdateClientBot2020CompatFailureInstrumentation(player, state);
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
        memory.FallbackRouteLabel = string.IsNullOrWhiteSpace(state.ActiveFallbackEntryLabel)
            ? string.Empty
            : $"{state.ActiveFallbackEntryLabel}->{state.ActiveFallbackTargetLabel}";
        memory.FallbackTriggerLabel = state.ActiveFallbackTriggerLabel;
        memory.NavigationIssueLabel = state.NavigationIssueLabel;
        memory.BranchDiagnosticCurrentPointId = state.BranchDiagnosticCurrentPoint;
        memory.BranchDiagnosticNextPointId = state.BranchDiagnosticNextPoint;
        memory.BranchDiagnosticTicks = state.BranchDiagnosticTicks;
        memory.BranchDiagnosticNoProgressTicks = state.BranchDiagnosticNoProgressTicks;
        memory.DirectTargetTicks = state.DirectTargetTicks;
        memory.DirectTargetNoProgressTicks = state.DirectTargetNoProgressTicks;
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
        state.NavigationIssueLabel = memory.NavigationIssueLabel;
        state.BranchDiagnosticCurrentPoint = memory.BranchDiagnosticCurrentPointId;
        state.BranchDiagnosticNextPoint = memory.BranchDiagnosticNextPointId;
        state.BranchDiagnosticTicks = memory.BranchDiagnosticTicks;
        state.BranchDiagnosticNoProgressTicks = memory.BranchDiagnosticNoProgressTicks;
        state.DirectTargetTicks = memory.DirectTargetTicks;
        state.DirectTargetNoProgressTicks = memory.DirectTargetNoProgressTicks;
        if (string.IsNullOrWhiteSpace(memory.FallbackRouteLabel))
        {
            state.ActiveFallbackEntryLabel = string.Empty;
            state.ActiveFallbackTargetLabel = string.Empty;
            state.ActiveFallbackTriggerLabel = string.Empty;
            state.ActiveFallbackRouteIndex = 0;
            state.ActiveFallbackTicks = 0;
            state.ActiveFallbackNoProgressTicks = 0;
            state.ActiveFallbackPreviousTargetDistance = float.PositiveInfinity;
            state.ActiveFallbackTraversalKind = BotNavigationTraversalKind.Walk;
            ClearClientBot2020CompatFallbackTraversal(state);
        }

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

    private NavigationDecision ApplyClientBot2020CompatFallbackRoute(
        SimulationWorld world,
        PlayerEntity player,
        ModernPathSelection objectiveSelection,
        BotMemory memory,
        BotTimingProfile timing,
        NavigationDecision navigationDecision)
    {
        _ = timing;
        var state = memory.ClientBot2020Compat ??= new ClientBot2020CompatState();
        var hintAsset = GetCompatHintAsset(world.Level);
        if (hintAsset is null || hintAsset.Nodes.Count == 0 || hintAsset.Links.Count == 0)
        {
            ClearClientBot2020CompatFallbackRoute(state, memory);
            return navigationDecision;
        }

        if (!TryResolveActiveCompatFallbackRoute(player, hintAsset, state, out var route)
            && TryFindCompatFallbackRoute(player, objectiveSelection, hintAsset, state, out route, out var triggerLabel))
        {
            state.ActiveFallbackEntryLabel = route.EntryLabel;
            state.ActiveFallbackTargetLabel = route.Nodes.Count > 1 ? route.Nodes[1].Label : route.Nodes[0].Label;
            state.ActiveFallbackTriggerLabel = triggerLabel;
            state.ActiveFallbackRouteIndex = IsPlayerNearHintNode(player, route.Nodes[0], player.ClassId, 24f) && route.Nodes.Count > 1 ? 1 : 0;
            state.ActiveFallbackTicks = 0;
            state.ActiveFallbackNoProgressTicks = 0;
            state.ActiveFallbackPreviousTargetDistance = float.PositiveInfinity;
            ClearClientBot2020CompatFallbackTraversal(state);
        }

        if (string.IsNullOrWhiteSpace(state.ActiveFallbackEntryLabel)
            || !TryResolveActiveCompatFallbackRoute(player, hintAsset, state, out route))
        {
            ClearClientBot2020CompatFallbackRoute(state, memory);
            return navigationDecision;
        }

        state.ActiveFallbackTicks += 1;
        if (state.ActiveFallbackTicks > 720)
        {
            ClearClientBot2020CompatFallbackRoute(state, memory);
            return navigationDecision;
        }

        var targetIndex = Math.Clamp(state.ActiveFallbackRouteIndex, 0, route.Nodes.Count - 1);
        while (targetIndex < route.Nodes.Count)
        {
            var targetNode = route.Nodes[targetIndex];
            if (!IsPlayerNearHintNode(player, targetNode, player.ClassId, GetCompatFallbackArrivalRadius(targetNode)))
            {
                break;
            }

            if (targetIndex == route.Nodes.Count - 1
                || targetNode.FallbackRole == BotNavigationHintFallbackRole.Exit)
            {
                ClearClientBot2020CompatFallbackRoute(state, memory);
                return navigationDecision;
            }

            targetIndex += 1;
        }

        if (targetIndex >= route.Nodes.Count)
        {
            ClearClientBot2020CompatFallbackRoute(state, memory);
            return navigationDecision;
        }

        var target = route.Nodes[targetIndex];
        var targetFeetY = GetCompatFallbackNodeFeetY(target, player.ClassId);
        var targetDistance = DistanceBetween(player.X, player.Y, target.X, target.Y);
        if (targetDistance + 1f < state.ActiveFallbackPreviousTargetDistance)
        {
            state.ActiveFallbackNoProgressTicks = 0;
        }
        else
        {
            state.ActiveFallbackNoProgressTicks += 1;
        }

        state.ActiveFallbackPreviousTargetDistance = targetDistance;
        if (state.ActiveFallbackNoProgressTicks > 180)
        {
            ClearClientBot2020CompatFallbackRoute(state, memory);
            return navigationDecision;
        }

        var traversalKind = BotNavigationTraversalKind.Walk;
        BotNavigationHintNode? sourceNode = null;
        BotNavigationHintLink? sourceLink = null;
        IReadOnlyList<BotNavigationInputFrame>? inputTape = null;
        if (targetIndex > 0 && targetIndex - 1 < route.Links.Count)
        {
            sourceNode = route.Nodes[targetIndex - 1];
            sourceLink = route.Links[targetIndex - 1];
            inputTape = TryFindCompatFallbackRecordedTraversal(sourceLink.RecordedTraversals, player.ClassId, out var recordedTraversal)
                ? recordedTraversal.InputTape
                : null;
            traversalKind = inputTape is { Count: > 0 }
                ? ResolveCompatFallbackRecordedTraversalKind(sourceNode.Y, target.Y, inputTape)
                : ResolveCompatFallbackTraversalKind(sourceLink, sourceNode, target);
            if (inputTape is not { Count: > 0 }
                && traversalKind == BotNavigationTraversalKind.Jump
                && sourceLink.StartJumpImmediately)
            {
                inputTape = BuildCompatFallbackImmediateJumpTape(world, player, sourceNode, target);
            }
        }

        state.ActiveFallbackRouteIndex = targetIndex;
        state.ActiveFallbackTargetLabel = target.Label;
        state.ActiveFallbackTraversalKind = traversalKind;

        if (sourceNode is not null
            && inputTape is { Count: > 0 }
            && TryResolveCompatFallbackRecordedTraversalDecision(
                world,
                player,
                sourceNode,
                target,
                inputTape,
                state,
                memory,
                timing,
                navigationDecision,
                traversalKind,
                out var recordedDecision))
        {
            return recordedDecision;
        }

        ClearClientBot2020CompatFallbackTraversal(state);
        state.ClosestPointX = target.X;
        state.ClosestPointY = targetFeetY;
        state.NextPoint = -1;
        state.NextPoint2 = -1;
        state.NextPoint3 = -1;
        state.DebugDecisionReason = $"fallback_{state.ActiveFallbackTriggerLabel}";
        SyncClientBot2020CompatStateToMemory(state, memory);

        var forceJump = traversalKind == BotNavigationTraversalKind.Jump
            && CompatHasGroundContact(world, player)
            && targetDistance > 18f;
        return new NavigationDecision(
            (target.X, targetFeetY),
            navigationDecision.HasRoute,
            navigationDecision.ForcedHorizontalDirection,
            forceJump,
            navigationDecision.LocksMovement,
            $"compat:fallback:{state.ActiveFallbackTriggerLabel}:{state.ActiveFallbackEntryLabel}->{target.Label}",
            traversalKind,
            MovementTargetUsesFeetCoordinates: true,
            navigationDecision.CaptureHoldActive);
    }

    private static bool TryResolveActiveCompatFallbackRoute(
        PlayerEntity player,
        BotNavigationHintAsset hintAsset,
        ClientBot2020CompatState state,
        out CompatFallbackRoute route)
    {
        route = default!;
        return !string.IsNullOrWhiteSpace(state.ActiveFallbackEntryLabel)
            && TryBuildCompatFallbackRoute(hintAsset, state.ActiveFallbackEntryLabel, player.ClassId, out route);
    }

    private static bool TryFindCompatFallbackRoute(
        PlayerEntity player,
        ModernPathSelection objectiveSelection,
        BotNavigationHintAsset hintAsset,
        ClientBot2020CompatState state,
        out CompatFallbackRoute route,
        out string triggerLabel)
    {
        route = default!;
        triggerLabel = string.Empty;
        for (var index = 0; index < hintAsset.Nodes.Count; index += 1)
        {
            var node = hintAsset.Nodes[index];
            if (!IsCompatFallbackEntryForPlayer(player, node)
                || !IsPlayerInCompatFallbackActivationArea(player, node, player.ClassId)
                || !TryResolveCompatFallbackTrigger(player, objectiveSelection, state, node, out triggerLabel)
                || !TryBuildCompatFallbackRoute(hintAsset, node.Label, player.ClassId, out route))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool IsCompatFallbackEntryForPlayer(PlayerEntity player, BotNavigationHintNode node)
    {
        return node.FallbackRole == BotNavigationHintFallbackRole.Entry
            && !string.IsNullOrWhiteSpace(node.Label)
            && (!node.Team.HasValue || node.Team.Value == player.Team)
            && (!node.RequiresCarryingIntel.HasValue || node.RequiresCarryingIntel.Value == player.IsCarryingIntel)
            && BotNavigationClasses.AppliesToClass(node.Classes, node.Profiles, player.ClassId);
    }

    private static bool TryResolveCompatFallbackTrigger(
        PlayerEntity player,
        ModernPathSelection objectiveSelection,
        ClientBot2020CompatState state,
        BotNavigationHintNode node,
        out string triggerLabel)
    {
        _ = objectiveSelection;
        IReadOnlyList<BotNavigationHintFallbackTrigger> triggers = node.FallbackTriggers.Count == 0
            ? new[] { BotNavigationHintFallbackTrigger.NoNext }
            : node.FallbackTriggers;
        for (var index = 0; index < triggers.Count; index += 1)
        {
            var trigger = triggers[index];
            if (!IsCompatFallbackTriggerActive(player, state, trigger))
            {
                continue;
            }

            triggerLabel = GetCompatFallbackTriggerLabel(trigger);
            return true;
        }

        triggerLabel = string.Empty;
        return false;
    }

    private static bool IsCompatFallbackTriggerActive(
        PlayerEntity player,
        ClientBot2020CompatState state,
        BotNavigationHintFallbackTrigger trigger)
    {
        return trigger switch
        {
            BotNavigationHintFallbackTrigger.NoNext => state.NoNextPointTicks > 8 || string.Equals(state.DebugDecisionReason, "no_next_direct", StringComparison.Ordinal),
            BotNavigationHintFallbackTrigger.Stuck => state.StuckTicks > 24,
            BotNavigationHintFallbackTrigger.Reacquire => state.DebugDecisionReason.StartsWith("reacquire", StringComparison.OrdinalIgnoreCase),
            BotNavigationHintFallbackTrigger.CarryReturn => player.IsCarryingIntel,
            BotNavigationHintFallbackTrigger.DirectTarget => state.DebugDecisionReason.StartsWith("direct_target", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    private static string GetCompatFallbackTriggerLabel(BotNavigationHintFallbackTrigger trigger)
    {
        return trigger switch
        {
            BotNavigationHintFallbackTrigger.Stuck => "stuck",
            BotNavigationHintFallbackTrigger.Reacquire => "reacquire",
            BotNavigationHintFallbackTrigger.CarryReturn => "carry_return",
            BotNavigationHintFallbackTrigger.DirectTarget => "direct_target",
            _ => "no_next",
        };
    }

    private static bool TryBuildCompatFallbackRoute(
        BotNavigationHintAsset hintAsset,
        string entryLabel,
        PlayerClass classId,
        out CompatFallbackRoute route)
    {
        route = default!;
        var nodesByLabel = new Dictionary<string, BotNavigationHintNode>(StringComparer.OrdinalIgnoreCase);
        for (var nodeIndex = 0; nodeIndex < hintAsset.Nodes.Count; nodeIndex += 1)
        {
            var node = hintAsset.Nodes[nodeIndex];
            if (!string.IsNullOrWhiteSpace(node.Label) && !nodesByLabel.ContainsKey(node.Label))
            {
                nodesByLabel[node.Label] = node;
            }
        }

        if (!nodesByLabel.TryGetValue(entryLabel, out var entry))
        {
            return false;
        }

        var nodes = new List<BotNavigationHintNode> { entry };
        var links = new List<BotNavigationHintLink>();
        var visitedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { entry.Label };
        var currentLabel = entry.Label;
        for (var depth = 0; depth < 32; depth += 1)
        {
            var outgoing = hintAsset.Links
                .Where(link =>
                    string.Equals(link.FromLabel, currentLabel, StringComparison.OrdinalIgnoreCase)
                    && BotNavigationClasses.AppliesToClass(link.Classes, link.Profiles, classId)
                    && nodesByLabel.ContainsKey(link.ToLabel))
                .OrderBy(static link => link.CostMultiplier)
                .ThenBy(static link => link.ToLabel, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (outgoing.Length == 0)
            {
                break;
            }

            var link = outgoing[0];
            var nextNode = nodesByLabel[link.ToLabel];
            if (!visitedLabels.Add(nextNode.Label))
            {
                break;
            }

            links.Add(link);
            nodes.Add(nextNode);
            currentLabel = nextNode.Label;
            if (nextNode.FallbackRole == BotNavigationHintFallbackRole.Exit)
            {
                break;
            }
        }

        if (nodes.Count < 2)
        {
            return false;
        }

        route = new CompatFallbackRoute(entry.Label, nodes.ToArray(), links.ToArray());
        return true;
    }

    private static float GetCompatFallbackEntryRadius(BotNavigationHintNode node)
    {
        return node.FallbackRadius > 0f ? node.FallbackRadius : 112f;
    }

    private static bool IsPlayerInCompatFallbackActivationArea(PlayerEntity player, BotNavigationHintNode node, PlayerClass classId)
    {
        if (TryGetCompatFallbackActivationZone(node, out var minX, out var minY, out var maxX, out var maxY))
        {
            var playerMinY = MathF.Min(player.Y, player.Bottom);
            var playerMaxY = MathF.Max(player.Y, player.Bottom);
            return player.X >= minX
                && player.X <= maxX
                && playerMaxY >= minY
                && playerMinY <= maxY;
        }

        return IsPlayerNearHintNode(player, node, classId, GetCompatFallbackEntryRadius(node));
    }

    private static bool TryGetCompatFallbackActivationZone(
        BotNavigationHintNode node,
        out float minX,
        out float minY,
        out float maxX,
        out float maxY)
    {
        minX = minY = maxX = maxY = 0f;
        var zone = node.FallbackActivationZone;
        if (zone is null)
        {
            return false;
        }

        minX = MathF.Min(zone.MinX, zone.MaxX);
        minY = MathF.Min(zone.MinY, zone.MaxY);
        maxX = MathF.Max(zone.MinX, zone.MaxX);
        maxY = MathF.Max(zone.MinY, zone.MaxY);
        return maxX - minX >= 1f && maxY - minY >= 1f;
    }

    private static float GetCompatFallbackArrivalRadius(BotNavigationHintNode node)
    {
        if (node.FallbackRole == BotNavigationHintFallbackRole.Exit)
        {
            return node.FallbackRadius > 0f ? node.FallbackRadius : 72f;
        }

        return MathF.Max(20f, (node.FallbackRadius > 0f ? node.FallbackRadius : 64f) * 0.35f);
    }

    private static bool IsPlayerNearHintNode(PlayerEntity player, BotNavigationHintNode node, PlayerClass classId, float radius)
    {
        _ = classId;
        return DistanceBetween(player.X, player.Y, node.X, node.Y) <= radius;
    }

    private static float GetCompatFallbackNodeFeetY(BotNavigationHintNode node, PlayerClass classId)
    {
        var classDefinition = BotNavigationClasses.GetDefinition(classId);
        return node.Y + classDefinition.CollisionBottom;
    }

    private static BotNavigationTraversalKind ResolveCompatFallbackTraversalKind(
        BotNavigationHintLink link,
        BotNavigationHintNode source,
        BotNavigationHintNode target)
    {
        return link.Traversal switch
        {
            BotNavigationHintTraversalKind.Drop => BotNavigationTraversalKind.Drop,
            BotNavigationHintTraversalKind.Jump => BotNavigationTraversalKind.Jump,
            BotNavigationHintTraversalKind.Walk => BotNavigationTraversalKind.Walk,
            _ => target.Y > source.Y + 8f
                ? BotNavigationTraversalKind.Drop
                : target.Y < source.Y - 8f
                ? BotNavigationTraversalKind.Jump
                : BotNavigationTraversalKind.Walk,
        };
    }

    private static IReadOnlyList<BotNavigationInputFrame> BuildCompatFallbackImmediateJumpTape(
        SimulationWorld world,
        PlayerEntity player,
        BotNavigationHintNode source,
        BotNavigationHintNode target)
    {
        var classDefinition = BotNavigationClasses.GetDefinition(player.ClassId);
        var profile = BotNavigationProfiles.GetProfileForClass(player.ClassId);
        if (BotNavigationMovementValidator.TryBuildHintJumpTape(
                world.Level,
                classDefinition,
                profile,
                source.X,
                source.Y,
                target.X,
                target.Y,
                player.Team,
                requireGroundedArrival: true,
                startJumpImmediately: true,
                out var tape,
                out _)
            && tape.Count > 0)
        {
            return tape;
        }

        return BotNavigationMovementValidator.BuildApproximateHintJumpTape(
            classDefinition,
            profile,
            source.X,
            source.Y,
            target.X,
            target.Y,
            startJumpImmediately: true);
    }

    private static bool TryResolveCompatFallbackRecordedTraversalDecision(
        SimulationWorld world,
        PlayerEntity player,
        BotNavigationHintNode source,
        BotNavigationHintNode target,
        IReadOnlyList<BotNavigationInputFrame> inputTape,
        ClientBot2020CompatState state,
        BotMemory memory,
        BotTimingProfile timing,
        NavigationDecision navigationDecision,
        BotNavigationTraversalKind traversalKind,
        out NavigationDecision decision)
    {
        var sourceFeetY = GetCompatFallbackNodeFeetY(source, player.ClassId);
        var targetFeetY = GetCompatFallbackNodeFeetY(target, player.ClassId);
        if (!IsExecutingCompatFallbackTraversal(state, source.Label, target.Label))
        {
            ClearClientBot2020CompatFallbackTraversal(state);
        }

        if (DistanceSquared(player.X, player.Bottom, source.X, sourceFeetY) > TraversalStartDistance * TraversalStartDistance)
        {
            state.ClosestPointX = source.X;
            state.ClosestPointY = sourceFeetY;
            state.NextPoint = -1;
            state.NextPoint2 = -1;
            state.NextPoint3 = -1;
            state.DebugDecisionReason = $"fallback_{state.ActiveFallbackTriggerLabel}_record_align";
            SyncClientBot2020CompatStateToMemory(state, memory);
            decision = new NavigationDecision(
                (source.X, sourceFeetY),
                navigationDecision.HasRoute,
                ForcedHorizontalDirection: 0,
                ForceJump: false,
                LocksMovement: true,
                Label: $"compat:fallback:{state.ActiveFallbackTriggerLabel}:{state.ActiveFallbackEntryLabel}->{target.Label}:record_align",
                traversalKind,
                MovementTargetUsesFeetCoordinates: true,
                navigationDecision.CaptureHoldActive);
            return true;
        }

        if (!IsExecutingCompatFallbackTraversal(state, source.Label, target.Label))
        {
            if (!CompatHasGroundContact(world, player))
            {
                state.ClosestPointX = source.X;
                state.ClosestPointY = sourceFeetY;
                state.NextPoint = -1;
                state.NextPoint2 = -1;
                state.NextPoint3 = -1;
                state.DebugDecisionReason = $"fallback_{state.ActiveFallbackTriggerLabel}_record_wait";
                SyncClientBot2020CompatStateToMemory(state, memory);
                decision = new NavigationDecision(
                    (source.X, sourceFeetY),
                    navigationDecision.HasRoute,
                    ForcedHorizontalDirection: 0,
                    ForceJump: false,
                    LocksMovement: true,
                    Label: $"compat:fallback:{state.ActiveFallbackTriggerLabel}:{state.ActiveFallbackEntryLabel}->{target.Label}:record_wait",
                    traversalKind,
                    MovementTargetUsesFeetCoordinates: true,
                    navigationDecision.CaptureHoldActive);
                return true;
            }

            BeginCompatFallbackTraversalExecution(state, source.Label, target.Label, traversalKind, inputTape);
        }

        if (TryConsumeCompatFallbackTraversalExecution(state, timing.FixedDeltaSeconds, out var forcedHorizontalDirection, out var forceJump))
        {
            state.ClosestPointX = target.X;
            state.ClosestPointY = targetFeetY;
            state.NextPoint = -1;
            state.NextPoint2 = -1;
            state.NextPoint3 = -1;
            state.DebugDecisionReason = $"fallback_{state.ActiveFallbackTriggerLabel}_record";
            SyncClientBot2020CompatStateToMemory(state, memory);
            decision = new NavigationDecision(
                (target.X, targetFeetY),
                navigationDecision.HasRoute,
                forcedHorizontalDirection,
                forceJump,
                LocksMovement: true,
                Label: $"compat:fallback:{state.ActiveFallbackTriggerLabel}:{state.ActiveFallbackEntryLabel}->{target.Label}:record",
                traversalKind,
                MovementTargetUsesFeetCoordinates: true,
                navigationDecision.CaptureHoldActive);
            return true;
        }

        ClearClientBot2020CompatFallbackTraversal(state);
        state.ClosestPointX = target.X;
        state.ClosestPointY = targetFeetY;
        state.NextPoint = -1;
        state.NextPoint2 = -1;
        state.NextPoint3 = -1;
        state.DebugDecisionReason = $"fallback_{state.ActiveFallbackTriggerLabel}";
        SyncClientBot2020CompatStateToMemory(state, memory);
        decision = new NavigationDecision(
            (target.X, targetFeetY),
            navigationDecision.HasRoute,
            ForcedHorizontalDirection: 0,
            ForceJump: false,
            LocksMovement: false,
            Label: $"compat:fallback:{state.ActiveFallbackTriggerLabel}:{state.ActiveFallbackEntryLabel}->{target.Label}",
            traversalKind,
            MovementTargetUsesFeetCoordinates: true,
            navigationDecision.CaptureHoldActive);
        return true;
    }

    private static bool TryFindCompatFallbackRecordedTraversal(
        IReadOnlyList<BotNavigationHintRecordedTraversal> recordedTraversals,
        PlayerClass classId,
        out BotNavigationHintRecordedTraversal recordedTraversal)
    {
        var profile = BotNavigationProfiles.GetProfileForClass(classId);
        var match = recordedTraversals.FirstOrDefault(entry => entry.ClassId == classId && entry.InputTape.Count > 0)
            ?? recordedTraversals.FirstOrDefault(entry => entry.ClassId.HasValue && BotNavigationProfiles.GetProfileForClass(entry.ClassId.Value) == profile && entry.InputTape.Count > 0)
            ?? recordedTraversals.FirstOrDefault(entry => entry.Profile == profile && entry.InputTape.Count > 0);
        recordedTraversal = match!;
        return match is not null;
    }

    private static BotNavigationTraversalKind ResolveCompatFallbackRecordedTraversalKind(
        float sourceY,
        float targetY,
        IReadOnlyList<BotNavigationInputFrame> tape)
    {
        if (tape.Any(static frame => frame.Up))
        {
            return BotNavigationTraversalKind.Jump;
        }

        return targetY > sourceY + 8f
            ? BotNavigationTraversalKind.Drop
            : BotNavigationTraversalKind.Walk;
    }

    private static void BeginCompatFallbackTraversalExecution(
        ClientBot2020CompatState state,
        string fromLabel,
        string toLabel,
        BotNavigationTraversalKind traversalKind,
        IReadOnlyList<BotNavigationInputFrame> inputTape)
    {
        state.ActiveFallbackTraversalFromLabel = fromLabel;
        state.ActiveFallbackTraversalToLabel = toLabel;
        state.ActiveFallbackTraversalKind = traversalKind;
        state.ActiveFallbackTraversalTape = inputTape;
        state.ActiveFallbackTraversalFrameIndex = 0;
        state.ActiveFallbackTraversalFrameSecondsRemaining = inputTape.Count == 0
            ? 0d
            : GetFrameDurationSeconds(inputTape[0]);
    }

    private static bool IsExecutingCompatFallbackTraversal(
        ClientBot2020CompatState state,
        string fromLabel,
        string toLabel)
    {
        return state.ActiveFallbackTraversalTape is not null
            && state.ActiveFallbackTraversalFrameIndex >= 0
            && state.ActiveFallbackTraversalFrameIndex < state.ActiveFallbackTraversalTape.Count
            && string.Equals(state.ActiveFallbackTraversalFromLabel, fromLabel, StringComparison.OrdinalIgnoreCase)
            && string.Equals(state.ActiveFallbackTraversalToLabel, toLabel, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryConsumeCompatFallbackTraversalExecution(
        ClientBot2020CompatState state,
        double simulationTickSeconds,
        out int forcedHorizontalDirection,
        out bool forceJump)
    {
        forcedHorizontalDirection = 0;
        forceJump = false;
        if (state.ActiveFallbackTraversalTape is null
            || state.ActiveFallbackTraversalFrameIndex < 0
            || state.ActiveFallbackTraversalFrameIndex >= state.ActiveFallbackTraversalTape.Count)
        {
            return false;
        }

        var frame = state.ActiveFallbackTraversalTape[state.ActiveFallbackTraversalFrameIndex];
        forcedHorizontalDirection = frame.Right ? 1 : frame.Left ? -1 : 0;
        forceJump = frame.Up;

        if (state.ActiveFallbackTraversalFrameSecondsRemaining <= 0d)
        {
            state.ActiveFallbackTraversalFrameSecondsRemaining = GetFrameDurationSeconds(frame);
        }

        var remainingSeconds = state.ActiveFallbackTraversalFrameSecondsRemaining - Math.Max(0d, simulationTickSeconds);
        while (remainingSeconds <= 0d)
        {
            state.ActiveFallbackTraversalFrameIndex += 1;
            if (state.ActiveFallbackTraversalTape is null
                || state.ActiveFallbackTraversalFrameIndex >= state.ActiveFallbackTraversalTape.Count)
            {
                ClearClientBot2020CompatFallbackTraversal(state);
                return true;
            }

            remainingSeconds += GetFrameDurationSeconds(state.ActiveFallbackTraversalTape[state.ActiveFallbackTraversalFrameIndex]);
        }

        state.ActiveFallbackTraversalFrameSecondsRemaining = remainingSeconds;
        return true;
    }

    private static void ClearClientBot2020CompatFallbackTraversal(ClientBot2020CompatState state)
    {
        state.ActiveFallbackTraversalFromLabel = string.Empty;
        state.ActiveFallbackTraversalToLabel = string.Empty;
        state.ActiveFallbackTraversalKind = BotNavigationTraversalKind.Walk;
        state.ActiveFallbackTraversalTape = null;
        state.ActiveFallbackTraversalFrameIndex = 0;
        state.ActiveFallbackTraversalFrameSecondsRemaining = 0d;
    }

    private static void ClearClientBot2020CompatFallbackRoute(ClientBot2020CompatState state, BotMemory memory)
    {
        state.ActiveFallbackEntryLabel = string.Empty;
        state.ActiveFallbackTargetLabel = string.Empty;
        state.ActiveFallbackTriggerLabel = string.Empty;
        state.ActiveFallbackRouteIndex = 0;
        state.ActiveFallbackTicks = 0;
        state.ActiveFallbackNoProgressTicks = 0;
        state.ActiveFallbackPreviousTargetDistance = float.PositiveInfinity;
        state.ActiveFallbackTraversalKind = BotNavigationTraversalKind.Walk;
        ClearClientBot2020CompatFallbackTraversal(state);
        memory.FallbackRouteLabel = string.Empty;
        memory.FallbackTriggerLabel = string.Empty;
    }

    private sealed record CompatFallbackRoute(
        string EntryLabel,
        IReadOnlyList<BotNavigationHintNode> Nodes,
        IReadOnlyList<BotNavigationHintLink> Links);

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

    private static void ClearClientBot2020CompatFailureInstrumentation(ClientBot2020CompatState state)
    {
        state.NavigationIssueLabel = string.Empty;
        state.BranchDiagnosticCurrentPoint = -1;
        state.BranchDiagnosticNextPoint = -1;
        state.BranchDiagnosticReason = string.Empty;
        state.BranchDiagnosticTargetX = 0f;
        state.BranchDiagnosticTargetY = 0f;
        state.BranchDiagnosticTicks = 0;
        state.BranchDiagnosticNoProgressTicks = 0;
        state.BranchDiagnosticBestDistance = float.PositiveInfinity;
        state.DirectTargetTicks = 0;
        state.DirectTargetNoProgressTicks = 0;
    }

    private static void UpdateClientBot2020CompatFailureInstrumentation(
        PlayerEntity player,
        ClientBot2020CompatState state)
    {
        var reason = state.DebugDecisionReason ?? string.Empty;
        var currentDistance = DistanceBetween(player.X, player.Y, state.ClosestPointX, state.ClosestPointY);
        var branchChanged = state.BranchDiagnosticCurrentPoint != state.CurrentPoint
            || state.BranchDiagnosticNextPoint != state.NextPoint
            || !string.Equals(state.BranchDiagnosticReason, reason, StringComparison.Ordinal)
            || DistanceSquared(state.BranchDiagnosticTargetX, state.BranchDiagnosticTargetY, state.ClosestPointX, state.ClosestPointY) > 16f * 16f;

        if (branchChanged)
        {
            state.BranchDiagnosticCurrentPoint = state.CurrentPoint;
            state.BranchDiagnosticNextPoint = state.NextPoint;
            state.BranchDiagnosticReason = reason;
            state.BranchDiagnosticTargetX = state.ClosestPointX;
            state.BranchDiagnosticTargetY = state.ClosestPointY;
            state.BranchDiagnosticTicks = 1;
            state.BranchDiagnosticNoProgressTicks = 0;
            state.BranchDiagnosticBestDistance = currentDistance;
        }
        else
        {
            state.BranchDiagnosticTicks += 1;
            if (currentDistance + 1f < state.BranchDiagnosticBestDistance)
            {
                state.BranchDiagnosticBestDistance = currentDistance;
                state.BranchDiagnosticNoProgressTicks = Math.Max(0, state.BranchDiagnosticNoProgressTicks - 4);
            }
            else
            {
                state.BranchDiagnosticNoProgressTicks += 1;
            }
        }

        var isDirectTarget = IsClientBot2020CompatDirectTargetReason(reason);
        if (isDirectTarget)
        {
            state.DirectTargetTicks = branchChanged ? 1 : state.DirectTargetTicks + 1;
            state.DirectTargetNoProgressTicks = state.BranchDiagnosticNoProgressTicks;
        }
        else
        {
            state.DirectTargetTicks = 0;
            state.DirectTargetNoProgressTicks = 0;
        }

        state.NavigationIssueLabel = ResolveClientBot2020CompatNavigationIssueLabel(state, isDirectTarget);
    }

    private static bool IsClientBot2020CompatDirectTargetReason(string reason)
    {
        return reason.StartsWith("direct_target", StringComparison.Ordinal)
            || string.Equals(reason, "no_next_direct", StringComparison.Ordinal)
            || string.Equals(reason, "missing_currentpoint", StringComparison.Ordinal);
    }

    private static string ResolveClientBot2020CompatNavigationIssueLabel(
        ClientBot2020CompatState state,
        bool isDirectTarget)
    {
        if (isDirectTarget && state.DirectTargetNoProgressTicks >= 30)
        {
            return "direct_np";
        }

        if (state.CurrentPoint >= 0
            && state.NextPoint < 0
            && state.NoNextPointTicks >= 12)
        {
            return "no_next_np";
        }

        if (state.BranchDiagnosticNoProgressTicks >= 60)
        {
            return "branch_np";
        }

        return string.Empty;
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

    private static bool TrySuppressCompatNoNextBranch(
        PlayerEntity player,
        ClientBotNavPoints navPoints,
        int[] goalWeights,
        ClientBot2020CompatState state)
    {
        var failedPointId = state.CurrentPoint;
        if (failedPointId < 0)
        {
            return false;
        }

        state.SecondAnchorBlockPoint = failedPointId;
        state.SecondAnchorBlockTicks = Math.Max(state.SecondAnchorBlockTicks, 72);
        state.SecondAnchorCooldownTicks = Math.Max(state.SecondAnchorCooldownTicks, 72);
        state.CurrentPoint = -1;
        state.NextPoint = -1;
        state.NextPoint2 = -1;
        state.NextPoint3 = -1;
        state.StickyNextTicks = 0;
        state.NavChurnTicks = 0;
        state.NavChurnSwitchTicks = 0;
        state.NavChurnLockTicks = 0;
        state.NavChurnLockPoint = -1;
        state.NoNextPointTicks = 0;

        if (!TryAcquireWeightedCompatCurrentPointIgnoringSight(player, navPoints, goalWeights, state))
        {
            TryAcquireNearestCompatCurrentPointIgnoringSight(player, navPoints, state);
        }

        return state.CurrentPoint >= 0 && state.CurrentPoint != failedPointId;
    }

    private static bool TryAcquireWeightedCompatCurrentPointIgnoringSight(
        PlayerEntity player,
        ClientBotNavPoints navPoints,
        int[] goalWeights,
        ClientBot2020CompatState state)
    {
        state.CurrentPoint = -1;
        var bestDistance = float.PositiveInfinity;
        for (var index = 0; index < navPoints.Points.Count; index += 1)
        {
            if (state.SecondAnchorBlockTicks > 0 && index == state.SecondAnchorBlockPoint)
            {
                continue;
            }

            var candidate = navPoints.Points[index];
            if (GetModernGoalWeight(goalWeights, candidate.Id) <= 0)
            {
                continue;
            }

            var distance = DistanceBetween(candidate.X, GetModernNodeFeetY(navPoints, candidate), player.X, player.Y);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            state.CurrentPoint = candidate.Id;
        }

        return state.CurrentPoint >= 0;
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
            if (TrySuppressCompatNoNextBranch(player, navPoints, goalWeights, state))
            {
                state.DebugDecisionReason = "no_next_branch_block";
                TrySelectCompatNextPoint(world, player, destination, navPoints, goalWeights, state, timing);
            }
            else
            {
                state.CurrentPoint = -1;
                state.OldPathPointKey = $"{Environment.TickCount}_{state.NoNextPointTicks}";
                state.NoNextPointTicks = 0;
                state.DebugDecisionReason = "force_reacquire_nonp";
            }
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

            if (horizontal != 0
                && ShouldDelayModernRouteJumpLaunch(
                    world.Level,
                    player,
                    targetX,
                    targetY,
                    horizontal,
                    hasGroundContact,
                    jumpRange,
                    jumpHeightTotal,
                    out var jumpDelayReason))
            {
                memory.ModernJumpDebug = $"compat:{jumpDelayReason}";
                return false;
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
                if (MathF.Abs(horizontalSpeed) <= 0.0001f
                    || MathF.Abs(MathF.Sign(horizontal) - MathF.Sign(horizontalSpeed)) < 2f)
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
