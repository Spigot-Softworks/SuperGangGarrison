namespace OpenGarrison.Core.BotBrain;

/// <summary>
/// Converts a NavPath into raw Left/Right/Up/Down steering intent.
/// </summary>
public sealed class SteeringMachine
{
    private const int MinCommitTicks = 3;
    private const int StuckDetectionWindow = 15;
    private const float StuckDistanceThreshold = 2f;
    private const float WaypointReachRadius = NavGraphBuilder.WaypointArrivalRadius;
    private const int WaypointLookaheadSkipCount = 4;
    private const float WaypointLookaheadReachMultiplier = 1.5f;
    private const float InitialWalkAttachmentVerticalTolerance = 24f;
    private const float EdgeProbeDistance = 18f;
    private const float JumpTriggerDistance = 32f;
    private const float JumpLaunchGateTolerance = 4f;
    private const float CertifiedLaunchForwardTolerance = 16f;
    private const float ShortCertifiedLaunchForwardTolerance = 24f;
    private const float DropTriggerDistance = 18f;
    private const float HorizontalDeadZone = 5f;
    private const float TargetAboveJumpThreshold = -8f;
    private const int JumpRetryCooldownTicks = 6;
    private const int FastObstacleJumpRetryCooldownTicks = 2;
    private const int PressedBlockerHopTicks = 3;
    private const float MinimumDelayedJumpRunupSpeed = 80f;
    private const int MaximumDelayedJumpRunupTicks = 18;
    private const int GroundedContinuationRecoveryTicks = 45;
    private const int LandedBelowCompletionRecoveryTicks = 90;
    private const int LandedBelowCompletionFastFailSlackTicks = 8;
    private const float LandedBelowCompletionVerticalSlack = 8f;
    private const int GroundedWalkBelowTargetFastFailTicks = 8;
    private const float GroundedWalkBelowTargetVerticalSlack = 48f;
    private const float GroundedContinuationCompletionSlack = 8f;
    private const float AirborneCompletionContinuationSlack = 8f;
    private const int MaximumCertifiedEdgeTicks = 120;
    private const int CertifiedEdgeRetrySlackTicks = 36;
    private const int MaximumUncertifiedTraversalEdgeTicks = 180;
    private const int MaximumWalkEdgeTicks = 60;

    private SteeringState _state = SteeringState.Grounded;
    private float _commitDirectionX;
    private int _commitTicksRemaining;
    private float _stuckCheckX;
    private float _stuckCheckY;
    private int _stuckTicks;
    private int _stuckEscapePhase;
    private int _stuckEscapeTicks;
    private int _pressedBlockerTicks;
    private int _jumpRetryCooldownTicks;
    private int _trackedFromNode = -1;
    private int _trackedToNode = -1;
    private int _currentEdgeTicks;
    private bool _currentEdgeWasAirborne;
    private bool _currentEdgeLandedAfterAirborne;
    private bool _currentEdgeJumpRequested;
    private EdgeExecutionPhase _edgePhase = EdgeExecutionPhase.None;
    private int _edgePhaseTicks;
    private bool _edgeStartGrounded;
    private float _edgeStartX;
    private float _edgeStartY;
    private float _edgeStartHorizontalSpeed;
    private float _edgeStartVerticalSpeed;

    public SteeringOutput Update(
        PlayerEntity player,
        NavGraph graph,
        NavPath? path,
        SimpleLevel level,
        PlayerTeam team)
    {
        var output = new SteeringOutput();

        if (path is null || path.IsComplete || !player.IsAlive)
        {
            _pressedBlockerTicks = 0;
            return output;
        }

        TrySkipInitialWalkAttachment(player, graph, path);
        if (TrySkipPassedWalkWaypoint(player, graph, path))
        {
            _stuckTicks = 0;
            _stuckEscapePhase = 0;
            _pressedBlockerTicks = 0;
            _jumpRetryCooldownTicks = 0;
        }

        TryAdvanceToReachedFutureWaypoint(player, graph, path);
        if (ShouldAdvanceWaypoint(player, graph, path, level))
        {
            path.Advance();
            if (path.IsComplete)
            {
                return output;
            }

            _stuckTicks = 0;
            _stuckEscapePhase = 0;
            _pressedBlockerTicks = 0;
            _jumpRetryCooldownTicks = 0;
        }

        var targetNode = graph.GetNode(path.CurrentNode);
        var dx = targetNode.X - player.X;
        var dy = targetNode.Y - player.Y;
        UpdateState(player);
        UpdateStuckDetection(player);

        var hasCurrentEdge = path.TryGetCurrentEdge(out var currentEdge);
        var edgeKind = hasCurrentEdge ? currentEdge.Kind : NavEdgeKind.Walk;
        var edgeTicks = UpdateCurrentEdgeTimer(player, path, hasCurrentEdge);
        UpdateCurrentEdgePhase(player, hasCurrentEdge);
        if (hasCurrentEdge)
        {
            UpdateCurrentEdgeExecutionPhase(player, graph, path, currentEdge);
            TryFailExpiredEdge(player, graph, path, currentEdge, edgeTicks, level.Mode, ref output);
        }

        var suppressJumpUntilLaunch = false;
        var steeringDx = hasCurrentEdge
            ? ResolveEdgeSteeringDx(player, level.Mode, graph, path, currentEdge, dx, out suppressJumpUntilLaunch)
            : dx;
        output.State = _state;
        output.EdgeKind = edgeKind;

        switch (_state)
        {
            case SteeringState.Grounded:
                SteerGrounded(
                    player,
                    level,
                    team,
                    edgeKind,
                    steeringDx,
                    dy,
                    suppressJumpUntilLaunch,
                    ResolveJumpTriggerTick(player, currentEdge),
                    edgeTicks,
                    ShouldAssistSemanticWalkClimb(player, currentEdge),
                    RequiresCertifiedRunup(player, currentEdge, level.Mode),
                    currentEdge.LaunchRecipe,
                    ref output);
                break;
            case SteeringState.Airborne:
            case SteeringState.Falling:
                SteerAirborne(player, edgeKind, steeringDx, dy, ref output);
                break;
            case SteeringState.Recovery:
                SteerRecovery(player, steeringDx, ref output);
                break;
        }

        if (hasCurrentEdge && currentEdge.LaunchRecipe.HasRecipe)
        {
            output.RecipeTrace = CreateRecipeTrace(
                player,
                path,
                currentEdge,
                edgeTicks,
                steeringDx,
                suppressJumpUntilLaunch,
                output.MoveDirection,
                output.Jump,
                output.Jump);
        }

        if (_stuckEscapePhase > 0)
        {
            ApplyStuckEscape(player, ref output);
        }
        ApplyPressedBlockerHop(player, level, team, ref output);

        var fastJumpRetry = output.Jump
            && player.IsGrounded
            && ShouldUseFastJumpRetry(player, level, team, output.MoveDirection);
        ApplyJumpPulse(ref output, fastJumpRetry);
        if (output.RecipeTrace.HasRecipe)
        {
            output.RecipeTrace = output.RecipeTrace with
            {
                FinalMoveDirection = output.MoveDirection,
                FinalJump = output.Jump,
            };
        }

        TrackJumpRequest(edgeKind, output);
        ApplyCommitment(ref output);
        return output;
    }

    public void Reset()
    {
        _state = SteeringState.Grounded;
        _commitDirectionX = 0f;
        _commitTicksRemaining = 0;
        _stuckCheckX = 0f;
        _stuckCheckY = 0f;
        _stuckTicks = 0;
        _stuckEscapePhase = 0;
        _stuckEscapeTicks = 0;
        _pressedBlockerTicks = 0;
        _jumpRetryCooldownTicks = 0;
        _trackedFromNode = -1;
        _trackedToNode = -1;
        _currentEdgeTicks = 0;
        _currentEdgeWasAirborne = false;
        _currentEdgeLandedAfterAirborne = false;
        _currentEdgeJumpRequested = false;
        _edgePhase = EdgeExecutionPhase.None;
        _edgePhaseTicks = 0;
        _edgeStartGrounded = false;
        _edgeStartX = 0f;
        _edgeStartY = 0f;
        _edgeStartHorizontalSpeed = 0f;
        _edgeStartVerticalSpeed = 0f;
    }

    private void UpdateState(PlayerEntity player)
    {
        if (player.MovementState != LegacyMovementState.None)
        {
            _state = SteeringState.Recovery;
            return;
        }

        if (player.IsGrounded)
        {
            _state = SteeringState.Grounded;
            return;
        }

        _state = player.VerticalSpeed >= 0f ? SteeringState.Falling : SteeringState.Airborne;
    }

    private void UpdateStuckDetection(PlayerEntity player)
    {
        _stuckTicks += 1;
        if (_stuckTicks < StuckDetectionWindow)
        {
            return;
        }

        var movedX = MathF.Abs(player.X - _stuckCheckX);
        var movedY = MathF.Abs(player.Y - _stuckCheckY);
        if (movedX < StuckDistanceThreshold && movedY < StuckDistanceThreshold)
        {
            _stuckEscapePhase = Math.Min(_stuckEscapePhase + 1, 3);
            _stuckEscapeTicks = 0;
        }
        else
        {
            _stuckEscapePhase = 0;
        }

        _stuckCheckX = player.X;
        _stuckCheckY = player.Y;
        _stuckTicks = 0;
    }

    private bool ShouldAdvanceWaypoint(PlayerEntity player, NavGraph graph, NavPath path, SimpleLevel level)
    {
        var targetNode = graph.GetNode(path.CurrentNode);
        var dx = targetNode.X - player.X;
        var dy = targetNode.Y - player.Y;
        var distSq = (dx * dx) + (dy * dy);
        if (distSq < WaypointReachRadius * WaypointReachRadius)
        {
            if (!player.IsGrounded
                && player.ClassId != PlayerClass.Heavy
                && NextEdgeRequiresGroundedLaunch(path))
            {
                return false;
            }

            return true;
        }

        return path.TryGetCurrentEdge(out var edge)
            && edge.Completion.HasWindow
            && (player.IsGrounded
                ? graph.IsEdgeCompletionSatisfied(player.X, player.Y, edge.Completion)
                    || IsNearGroundedContinuationCompletion(player, edge, level)
                : player.ClassId != PlayerClass.Heavy
                    && (IsAirborneCompletionContinuation(player, graph, edge)
                        || IsNearGroundedContinuationCompletion(player, edge, level)));
    }

    private bool IsNearGroundedContinuationCompletion(PlayerEntity player, NavEdge edge, SimpleLevel level) =>
        level.Name.Equals("ClassicWell", StringComparison.OrdinalIgnoreCase)
        && edge.Kind == NavEdgeKind.Jump
        && edge.RequiresGroundedContinuation
        && _currentEdgeWasAirborne
        && player.X >= edge.Completion.MinX - GroundedContinuationCompletionSlack
        && player.X <= edge.Completion.MaxX + GroundedContinuationCompletionSlack
        && player.Y >= edge.Completion.MinY - GroundedContinuationCompletionSlack
        && player.Y <= edge.Completion.MaxY + GroundedContinuationCompletionSlack;

    private static bool IsAirborneCompletionContinuation(PlayerEntity player, NavGraph graph, NavEdge edge) =>
        edge.Kind == NavEdgeKind.Jump
        && !edge.RequiresGroundedContinuation
        && player.VerticalSpeed >= -15f
        && graph.IsEdgeCompletionSatisfied(player.X, player.Y, edge.Completion with
        {
            MaxY = edge.Completion.MaxY + AirborneCompletionContinuationSlack,
        });

    private static bool NextEdgeRequiresGroundedLaunch(NavPath path) =>
        path.CurrentIndex + 1 < path.Count
        && path.TryGetIncomingEdge(path.CurrentIndex + 1, out var nextEdge)
        && RequiresVerticalCertifiedLaunch(nextEdge);

    private static int ResolveJumpTriggerTick(PlayerEntity player, NavEdge edge)
    {
        if (edge.Kind != NavEdgeKind.Jump)
        {
            return 0;
        }

        return edge.JumpTriggerTick;
    }

    private static bool RequiresCertifiedRunup(PlayerEntity player, NavEdge edge, GameModeKind mode) =>
        edge.Kind == NavEdgeKind.Jump
        && edge.LaunchRecipe.HasRecipe
        && edge.ProbeMoveDirectionX != 0f
        && (player.ClassId == PlayerClass.Soldier
            || (mode == GameModeKind.CaptureTheFlag && player.ClassId == PlayerClass.Engineer)
            || (mode == GameModeKind.CaptureTheFlag && player.ClassId == PlayerClass.Heavy)
            || RequiresVerticalCertifiedLaunch(edge));

    private static bool RequiresVerticalCertifiedLaunch(NavEdge edge) =>
        edge.Completion.HasWindow
        && edge.LaunchRecipe.HasRecipe
        && edge.LaunchRecipe.LaunchMinY - edge.Completion.MaxY >= 20f;

    private int UpdateCurrentEdgeTimer(PlayerEntity player, NavPath path, bool hasCurrentEdge)
    {
        if (!hasCurrentEdge || path.CurrentIndex <= 0)
        {
            _trackedFromNode = -1;
            _trackedToNode = -1;
            _currentEdgeTicks = 0;
            _currentEdgeWasAirborne = false;
            _currentEdgeLandedAfterAirborne = false;
            _currentEdgeJumpRequested = false;
            _edgePhase = EdgeExecutionPhase.None;
            _edgePhaseTicks = 0;
            _edgeStartGrounded = false;
            _edgeStartX = 0f;
            _edgeStartY = 0f;
            _edgeStartHorizontalSpeed = 0f;
            _edgeStartVerticalSpeed = 0f;
            return _currentEdgeTicks;
        }

        var fromNode = path.GetWaypoint(path.CurrentIndex - 1);
        var toNode = path.CurrentNode;
        if (fromNode != _trackedFromNode || toNode != _trackedToNode)
        {
            _trackedFromNode = fromNode;
            _trackedToNode = toNode;
            _currentEdgeTicks = 0;
            _currentEdgeWasAirborne = false;
            _currentEdgeLandedAfterAirborne = false;
            _currentEdgeJumpRequested = false;
            _edgePhase = EdgeExecutionPhase.None;
            _edgePhaseTicks = 0;
            _edgeStartGrounded = player.IsGrounded;
            _edgeStartX = player.X;
            _edgeStartY = player.Y;
            _edgeStartHorizontalSpeed = player.HorizontalSpeed;
            _edgeStartVerticalSpeed = player.VerticalSpeed;
            return _currentEdgeTicks;
        }

        _currentEdgeTicks += 1;
        return _currentEdgeTicks;
    }

    private void UpdateCurrentEdgePhase(PlayerEntity player, bool hasCurrentEdge)
    {
        if (!hasCurrentEdge)
        {
            return;
        }

        if (!player.IsGrounded)
        {
            _currentEdgeWasAirborne = true;
            return;
        }

        if (_currentEdgeWasAirborne)
        {
            _currentEdgeLandedAfterAirborne = true;
        }
    }

    private static void TryAdvanceToReachedFutureWaypoint(PlayerEntity player, NavGraph graph, NavPath path)
    {
        if (path.CurrentIndex + 1 >= path.Count)
        {
            return;
        }

        var currentNode = graph.GetNode(path.CurrentNode);
        var currentDistance = Distance(player.X, player.Y, currentNode.X, currentNode.Y);
        var bestIndex = -1;
        var bestDistance = currentDistance;
        var maxIndex = Math.Min(path.Count - 1, path.CurrentIndex + WaypointLookaheadSkipCount);
        var reachRadius = WaypointReachRadius * WaypointLookaheadReachMultiplier;
        for (var index = path.CurrentIndex + 1; index <= maxIndex; index += 1)
        {
            if (path.TryGetIncomingEdge(index, out var incomingEdge)
                && incomingEdge.Kind == NavEdgeKind.Jump
                && incomingEdge.LaunchRecipe.HasRecipe
                && player.IsGrounded)
            {
                break;
            }

            var node = graph.GetNode(path.GetWaypoint(index));
            var distance = Distance(player.X, player.Y, node.X, node.Y);
            if (distance >= bestDistance || distance > reachRadius)
            {
                continue;
            }

            bestDistance = distance;
            bestIndex = index;
        }

        while (bestIndex > path.CurrentIndex)
        {
            path.Advance();
        }
    }

    private static bool TrySkipPassedWalkWaypoint(PlayerEntity player, NavGraph graph, NavPath path)
    {
        var advanced = false;
        if (!player.IsGrounded)
        {
            return false;
        }

        while (path.CurrentIndex > 0
            && path.CurrentIndex + 1 < path.Count
            && path.TryGetIncomingEdge(path.CurrentIndex, out var incomingEdge)
            && incomingEdge.Kind == NavEdgeKind.Walk
            && path.TryGetIncomingEdge(path.CurrentIndex + 1, out var outgoingEdge)
            && outgoingEdge.Kind == NavEdgeKind.Walk)
        {
            var previousNode = graph.GetNode(path.GetWaypoint(path.CurrentIndex - 1));
            var currentNode = graph.GetNode(path.CurrentNode);
            var nextNode = graph.GetNode(path.GetWaypoint(path.CurrentIndex + 1));
            if (!previousNode.SurfaceId.HasValue
                || previousNode.SurfaceId != currentNode.SurfaceId
                || currentNode.SurfaceId != nextNode.SurfaceId
                || MathF.Abs(player.Y - currentNode.Y) > InitialWalkAttachmentVerticalTolerance)
            {
                break;
            }

            var incomingDx = currentNode.X - previousNode.X;
            var outgoingDx = nextNode.X - currentNode.X;
            if (MathF.Abs(incomingDx) <= HorizontalDeadZone
                || MathF.Abs(outgoingDx) <= HorizontalDeadZone)
            {
                break;
            }

            var direction = MathF.Sign(incomingDx);
            if (MathF.Sign(outgoingDx) != direction)
            {
                break;
            }

            var progressPastCurrent = (player.X - currentNode.X) * direction;
            if (progressPastCurrent <= WaypointReachRadius)
            {
                break;
            }

            path.Advance();
            advanced = true;
        }

        return advanced;
    }

    private static void TrySkipInitialWalkAttachment(PlayerEntity player, NavGraph graph, NavPath path)
    {
        if (path.CurrentIndex != 0 || path.Count < 2 || !player.IsGrounded)
        {
            return;
        }

        while (path.CurrentIndex + 1 < path.Count
            && path.TryGetIncomingEdge(path.CurrentIndex + 1, out var nextEdge)
            && nextEdge.Kind == NavEdgeKind.Walk)
        {
            var fromNode = graph.GetNode(path.CurrentNode);
            var toNode = graph.GetNode(path.GetWaypoint(path.CurrentIndex + 1));
            if (!fromNode.SurfaceId.HasValue
                || fromNode.SurfaceId != toNode.SurfaceId
                || MathF.Abs(player.Y - fromNode.Y) > InitialWalkAttachmentVerticalTolerance)
            {
                return;
            }

            var edgeDx = toNode.X - fromNode.X;
            if (MathF.Abs(edgeDx) <= HorizontalDeadZone)
            {
                return;
            }

            var edgeDirection = MathF.Sign(edgeDx);
            var progressFromStart = (player.X - fromNode.X) * edgeDirection;
            if (progressFromStart < -WaypointReachRadius)
            {
                return;
            }

            path.Advance();
        }
    }

    private void UpdateCurrentEdgeExecutionPhase(PlayerEntity player, NavGraph graph, NavPath path, NavEdge edge)
    {
        if (_edgePhase != EdgeExecutionPhase.None)
        {
            _edgePhaseTicks += 1;
            if (ShouldExitEdgeExecutionPhase(player, graph, edge))
            {
                _edgePhase = EdgeExecutionPhase.None;
                _edgePhaseTicks = 0;
            }

            return;
        }

        if (ShouldEnterLandedBelowCompletionPhase(player, graph, path, edge))
        {
            _edgePhase = EdgeExecutionPhase.LandedBelowCompletion;
            _edgePhaseTicks = 0;
        }
    }

    private float ResolveEdgeSteeringDx(
        PlayerEntity player,
        GameModeKind mode,
        NavGraph graph,
        NavPath path,
        NavEdge edge,
        float waypointDx,
        out bool suppressJumpUntilLaunch)
    {
        suppressJumpUntilLaunch = false;
        if (IsExperimentalFallDropdownSteeringEnabled()
            && edge.Kind is (NavEdgeKind.Fall or NavEdgeKind.Dropdown)
            && player.IsGrounded
            && path.CurrentIndex > 0)
        {
            var launchNode = graph.GetNode(path.GetWaypoint(path.CurrentIndex - 1));
            var targetNode = graph.GetNode(path.CurrentNode);
            var travelDirection = edge.ProbeMoveDirectionX != 0f
                ? MathF.Sign(edge.ProbeMoveDirectionX)
                : MathF.Sign(targetNode.X - launchNode.X);
            if (travelDirection != 0f)
            {
                return travelDirection * MathF.Max(JumpTriggerDistance * 2f, MathF.Abs(waypointDx) + JumpTriggerDistance);
            }
        }

        if (!edge.Completion.HasWindow)
        {
            return waypointDx;
        }

        if (edge.Kind == NavEdgeKind.Walk)
        {
            if (ShouldAssistSemanticWalkClimb(player, edge))
            {
                var walkCompletionCenterX = (edge.Completion.MinX + edge.Completion.MaxX) * 0.5f;
                var walkCompletionDx = walkCompletionCenterX - player.X;
                return MathF.Abs(walkCompletionDx) > HorizontalDeadZone
                    ? walkCompletionDx
                    : waypointDx;
            }

            return waypointDx;
        }

        if (_edgePhase == EdgeExecutionPhase.LandedBelowCompletion)
        {
            if (!edge.RequiresGroundedContinuation
                && player.X >= edge.Completion.MinX
                && player.X <= edge.Completion.MaxX
                && MathF.Abs(waypointDx) > HorizontalDeadZone)
            {
                return waypointDx;
            }

            return edge.ProbeMoveDirectionX * MathF.Max(JumpTriggerDistance * 2f, MathF.Abs(waypointDx));
        }

        if (edge.Kind == NavEdgeKind.Jump
            && player.IsGrounded
            && player.Y > edge.Completion.MaxY + 8f
            && path.CurrentIndex > 0)
        {
            var launchNode = graph.GetNode(path.GetWaypoint(path.CurrentIndex - 1));
            var launchDx = launchNode.X - player.X;
            if (MathF.Abs(launchDx) > JumpTriggerDistance)
            {
                suppressJumpUntilLaunch = true;
                return launchDx;
            }
        }

        if (edge.Kind == NavEdgeKind.Jump
            && player.IsGrounded
            && path.CurrentIndex > 0)
        {
            var launchNode = graph.GetNode(path.GetWaypoint(path.CurrentIndex - 1));
            var targetNode = graph.GetNode(path.CurrentNode);
            var travelDirection = MathF.Sign(targetNode.X - launchNode.X);
            var signedDistanceFromLaunch = (player.X - launchNode.X) * travelDirection;
            if (travelDirection != 0f && signedDistanceFromLaunch < -JumpLaunchGateTolerance)
            {
                suppressJumpUntilLaunch = true;
                return launchNode.X - player.X;
            }

            if (travelDirection != 0f
                && RequiresCertifiedRunup(player, edge, mode)
                && !IsInLaunchPositionWindow(player, edge.LaunchRecipe)
                && signedDistanceFromLaunch > ResolveCertifiedLaunchForwardTolerance(edge.JumpTriggerTick)
                && player.Y > edge.Completion.MaxY + 8f)
            {
                suppressJumpUntilLaunch = true;
                return launchNode.X - player.X;
            }
        }

        var completionCenterX = (edge.Completion.MinX + edge.Completion.MaxX) * 0.5f;
        var completionDx = completionCenterX - player.X;
        if (edge.Kind == NavEdgeKind.Jump && MathF.Abs(completionDx) > HorizontalDeadZone)
        {
            return completionDx;
        }

        if (MathF.Abs(waypointDx) > HorizontalDeadZone)
        {
            return waypointDx;
        }

        return MathF.Abs(completionDx) > HorizontalDeadZone
            ? completionDx
            : waypointDx;
    }

    private SteeringRecipeTrace CreateRecipeTrace(
        PlayerEntity player,
        NavPath path,
        NavEdge edge,
        int edgeTicks,
        float steeringDx,
        bool suppressJumpUntilLaunch,
        float requestedMoveDirection,
        bool requestedJump,
        bool finalJump)
    {
        var recipe = edge.LaunchRecipe;
        var fromNode = path.CurrentIndex > 0 ? path.GetWaypoint(path.CurrentIndex - 1) : -1;
        var toNode = path.CurrentNode;
        var inLaunchXWindow = player.X >= recipe.LaunchMinX && player.X <= recipe.LaunchMaxX;
        var inLaunchYWindow = player.Y >= recipe.LaunchMinY && player.Y <= recipe.LaunchMaxY;
        var inLaunchSpeedWindow = player.HorizontalSpeed >= recipe.LaunchMinHorizontalSpeed
            && player.HorizontalSpeed <= recipe.LaunchMaxHorizontalSpeed;
        var expectedMoveDirection = MathF.Sign(recipe.ExpectedMoveDirectionX);
        var directionMatches = expectedMoveDirection == 0f
            || requestedMoveDirection == expectedMoveDirection
            || MathF.Sign(player.HorizontalSpeed) == expectedMoveDirection;
        var startMatches = _edgeStartGrounded == recipe.StartGrounded;
        var ready = startMatches
            && (!recipe.StartGrounded || player.IsGrounded)
            && inLaunchXWindow
            && inLaunchYWindow
            && inLaunchSpeedWindow
            && directionMatches;

        return new SteeringRecipeTrace(
            HasRecipe: true,
            FromNode: fromNode,
            ToNode: toNode,
            EdgeTicks: edgeTicks,
            StartGrounded: _edgeStartGrounded,
            StartX: _edgeStartX,
            StartY: _edgeStartY,
            StartHorizontalSpeed: _edgeStartHorizontalSpeed,
            StartVerticalSpeed: _edgeStartVerticalSpeed,
            RecipeStartGrounded: recipe.StartGrounded,
            RecipeLaunchTick: recipe.LaunchTick,
            RecipeLaunchMinX: recipe.LaunchMinX,
            RecipeLaunchMaxX: recipe.LaunchMaxX,
            RecipeLaunchMinY: recipe.LaunchMinY,
            RecipeLaunchMaxY: recipe.LaunchMaxY,
            RecipeLaunchMinHorizontalSpeed: recipe.LaunchMinHorizontalSpeed,
            RecipeLaunchMaxHorizontalSpeed: recipe.LaunchMaxHorizontalSpeed,
            RecipeExpectedMoveDirectionX: expectedMoveDirection,
            CurrentGrounded: player.IsGrounded,
            CurrentX: player.X,
            CurrentY: player.Y,
            CurrentHorizontalSpeed: player.HorizontalSpeed,
            CurrentVerticalSpeed: player.VerticalSpeed,
            InLaunchXWindow: inLaunchXWindow,
            InLaunchYWindow: inLaunchYWindow,
            InLaunchSpeedWindow: inLaunchSpeedWindow,
            DirectionMatches: directionMatches,
            StartMatches: startMatches,
            RecipeReady: ready,
            SuppressJumpUntilLaunch: suppressJumpUntilLaunch,
            RequestedMoveDirection: requestedMoveDirection,
            FinalMoveDirection: requestedMoveDirection,
            RequestedJump: requestedJump,
            FinalJump: finalJump,
            SteeringDx: steeringDx);
    }

    private bool ShouldEnterLandedBelowCompletionPhase(
        PlayerEntity player,
        NavGraph graph,
        NavPath path,
        NavEdge edge)
    {
        if (edge.Kind != NavEdgeKind.Jump
            || edge.ProbeMoveDirectionX == 0f
            || edge.ProbeTicks <= 0
            || !_currentEdgeLandedAfterAirborne
            || !_currentEdgeJumpRequested
            || !player.IsGrounded
            || !player.IsCarryingIntel
            || (player.ClassId != PlayerClass.Heavy && player.ClassId != PlayerClass.Soldier)
            || player.Y <= edge.Completion.MaxY + LandedBelowCompletionVerticalSlack
            || path.CurrentIndex <= 0)
        {
            return false;
        }

        if (graph.IsEdgeCompletionSatisfied(player.X, player.Y, edge.Completion))
        {
            return false;
        }

        return edge.RequiresGroundedContinuation
            || edge.JumpTriggerTick > 0;
    }

    private bool ShouldExitEdgeExecutionPhase(PlayerEntity player, NavGraph graph, NavEdge edge)
    {
        if (graph.IsEdgeCompletionSatisfied(player.X, player.Y, edge.Completion))
        {
            return true;
        }

        var maxTicks = edge.RequiresGroundedContinuation
            ? GroundedContinuationRecoveryTicks
            : LandedBelowCompletionRecoveryTicks;
        return _edgePhaseTicks > maxTicks;
    }

    private void TryFailExpiredEdge(
        PlayerEntity player,
        NavGraph graph,
        NavPath path,
        NavEdge edge,
        int edgeTicks,
        GameModeKind mode,
        ref SteeringOutput output)
    {
        if (path.CurrentIndex <= 0
            || graph.IsEdgeCompletionSatisfied(player.X, player.Y, edge.Completion))
        {
            return;
        }

        var fromNode = path.GetWaypoint(path.CurrentIndex - 1);
        var toNode = path.CurrentNode;
        var targetNode = graph.GetNode(toNode);
        if (ShouldFastFailGroundedWalkBelowTarget(player, edge, targetNode, edgeTicks))
        {
            output.RequestRepath = true;
            output.FailedEdge = new SteeringFailedEdge(
                HasFailure: true,
                FromNode: fromNode,
                ToNode: toNode,
                Kind: edge.Kind,
                EdgeTicks: edgeTicks,
                Reason: "walk_timeout");
            return;
        }

        if (ShouldFastFailLandedBelowCompletion(player, edge, edgeTicks, mode))
        {
            output.RequestRepath = true;
            output.FailedEdge = new SteeringFailedEdge(
                HasFailure: true,
                FromNode: fromNode,
                ToNode: toNode,
                Kind: edge.Kind,
                EdgeTicks: edgeTicks,
                Reason: "landed_below_completion");
            return;
        }

        var targetDistance = MathF.Sqrt(
            ((targetNode.X - player.X) * (targetNode.X - player.X))
            + ((targetNode.Y - player.Y) * (targetNode.Y - player.Y)));
        var maxTicks = ResolveMaximumEdgeTicks(edge);
        if (edgeTicks < maxTicks)
        {
            return;
        }

        output.RequestRepath = true;
        output.FailedEdge = new SteeringFailedEdge(
            HasFailure: true,
            FromNode: fromNode,
            ToNode: toNode,
            Kind: edge.Kind,
            EdgeTicks: edgeTicks,
            Reason: ResolveEdgeFailureReason(player, edge, targetDistance));
    }

    private static bool IsExperimentalFallDropdownSteeringEnabled() =>
        Environment.GetEnvironmentVariable("BOTBRAIN_EXPERIMENTAL_FALL_DROPDOWN_STEERING") is "1" or "true" or "TRUE";

    private bool ShouldFastFailLandedBelowCompletion(PlayerEntity player, NavEdge edge, int edgeTicks, GameModeKind mode)
    {
        return _edgePhase == EdgeExecutionPhase.None
            && mode == GameModeKind.CaptureTheFlag
            && edge.Kind == NavEdgeKind.Jump
            && edge.Completion.HasWindow
            && _currentEdgeLandedAfterAirborne
            && _currentEdgeJumpRequested
            && player.IsGrounded
            && player.ClassId is not PlayerClass.Soldier and not PlayerClass.Heavy
            && !edge.Completion.Contains(player.X, player.Y)
            && player.Y > edge.Completion.MaxY + LandedBelowCompletionVerticalSlack
            && edgeTicks >= Math.Max(0, ResolveMaximumEdgeTicks(edge) - LandedBelowCompletionFastFailSlackTicks);
    }

    private static bool ShouldFastFailGroundedWalkBelowTarget(PlayerEntity player, NavEdge edge, NavNode targetNode, int edgeTicks)
    {
        return edge.Kind == NavEdgeKind.Walk
            && !edge.Completion.HasWindow
            && player.IsGrounded
            && player.ClassId is not PlayerClass.Soldier and not PlayerClass.Heavy
            && player.Y > targetNode.Y + GroundedWalkBelowTargetVerticalSlack
            && edgeTicks >= GroundedWalkBelowTargetFastFailTicks;
    }

    private static string ResolveEdgeFailureReason(PlayerEntity player, NavEdge edge, float targetDistance)
    {
        if (edge.Kind == NavEdgeKind.Jump && edge.Completion.HasWindow && player.IsGrounded && !edge.Completion.Contains(player.X, player.Y))
        {
            return player.Y > edge.Completion.MaxY + LandedBelowCompletionVerticalSlack
                ? "landed_below_completion"
                : "missed_completion";
        }

        if (edge.Kind is NavEdgeKind.Fall or NavEdgeKind.Dropdown)
        {
            return player.IsGrounded ? "wrong_fall_landing" : "fall_not_completing";
        }

        if (edge.Kind == NavEdgeKind.Walk)
        {
            return player.IsGrounded ? "walk_timeout" : "walk_airborne_timeout";
        }

        return targetDistance > WaypointReachRadius * 2f
            ? "edge_timeout_far"
            : "edge_timeout_near";
    }

    private static int ResolveMaximumEdgeTicks(NavEdge edge)
    {
        if (edge.Kind == NavEdgeKind.Walk)
        {
            return MaximumWalkEdgeTicks;
        }

        if (!edge.Completion.HasWindow)
        {
            return MaximumUncertifiedTraversalEdgeTicks;
        }

        if (edge.ProbeTicks <= 0)
        {
            return MaximumCertifiedEdgeTicks;
        }

        return int.Clamp(edge.ProbeTicks + CertifiedEdgeRetrySlackTicks, 45, MaximumCertifiedEdgeTicks);
    }

    private static void SteerGrounded(
        PlayerEntity player,
        SimpleLevel level,
        PlayerTeam team,
        NavEdgeKind edgeKind,
        float dx,
        float dy,
        bool suppressJumpUntilLaunch,
        int jumpTriggerTick,
        int edgeTicks,
        bool assistSemanticWalkClimb,
        bool requiresCertifiedRunup,
        NavEdgeLaunchRecipe launchRecipe,
        ref SteeringOutput output)
    {
        var moveDir = GetMoveDirection(dx);

        switch (edgeKind)
        {
            case NavEdgeKind.Jump:
                output.MoveDirection = moveDir;
                var jumpDelaySatisfied = edgeTicks >= jumpTriggerTick;
                if (requiresCertifiedRunup
                    && launchRecipe.HasRecipe
                    && IsInLaunchPositionWindow(player, launchRecipe)
                    && !IsInLaunchSpeedWindow(player, launchRecipe))
                {
                    output.MoveDirection = player.HorizontalSpeed > launchRecipe.LaunchMaxHorizontalSpeed
                        ? -1f
                        : player.HorizontalSpeed < launchRecipe.LaunchMinHorizontalSpeed
                            ? 1f
                            : output.MoveDirection;
                    break;
                }

                var needsRunup = player.ClassId == PlayerClass.Soldier
                    || requiresCertifiedRunup
                    || (player.ClassId == PlayerClass.Heavy && moveDir < 0f);
                var minimumRunupSpeed = requiresCertifiedRunup
                    ? ResolveCertifiedRunupSpeed(jumpTriggerTick)
                    : MinimumDelayedJumpRunupSpeed;
                var runupSatisfied = !needsRunup
                    || jumpTriggerTick <= 0
                    || moveDir == 0f
                    || (player.HorizontalSpeed * moveDir) >= minimumRunupSpeed
                    || (!requiresCertifiedRunup
                        && edgeTicks >= jumpTriggerTick + MaximumDelayedJumpRunupTicks);
                var recipeReady = !requiresCertifiedRunup && jumpDelaySatisfied
                    || (edgeTicks >= launchRecipe.LaunchTick
                        && IsLaunchRecipeReady(player, launchRecipe, moveDir));
                if (jumpDelaySatisfied
                    && (recipeReady || runupSatisfied)
                    && (MathF.Abs(dx) <= JumpTriggerDistance
                    || recipeReady
                    || (!suppressJumpUntilLaunch
                        && (dy <= TargetAboveJumpThreshold
                            || IsApproachingCliff(player, level, team, moveDir)
                            || WouldHitWall(player, level, team, moveDir)))))
                {
                    output.Jump = true;
                }
                break;

            case NavEdgeKind.Dropdown:
                output.MoveDirection = moveDir;
                if (MathF.Abs(dx) <= DropTriggerDistance)
                {
                    output.DropDown = true;
                }
                break;

            case NavEdgeKind.Fall:
                output.MoveDirection = moveDir != 0f ? moveDir : MathF.Sign(dx);
                break;

            default:
                output.MoveDirection = moveDir;
                var jumpableObstacleAhead = IsJumpableObstacleAhead(player, level, team, moveDir);
                if (assistSemanticWalkClimb && edgeTicks >= 2)
                {
                    output.Jump = true;
                }

                if (jumpableObstacleAhead
                    || (WouldHitWall(player, level, team, moveDir) && dy <= TargetAboveJumpThreshold))
                {
                    output.Jump = true;
                }

                if (dy <= TargetAboveJumpThreshold && IsApproachingCliff(player, level, team, moveDir))
                {
                    output.Jump = true;
                }
                break;
        }
    }

    private static bool ShouldAssistSemanticWalkClimb(PlayerEntity player, NavEdge edge) =>
        edge.Kind == NavEdgeKind.Walk
        && edge.Completion.HasWindow
        && player.IsGrounded
        && player.Y > edge.Completion.MaxY + LandedBelowCompletionVerticalSlack;

    private static void SteerAirborne(
        PlayerEntity player,
        NavEdgeKind edgeKind,
        float dx,
        float dy,
        ref SteeringOutput output)
    {
        output.MoveDirection = GetMoveDirection(dx);

        if (edgeKind == NavEdgeKind.Jump
            && player.RemainingAirJumps > 0
            && dy < -24f
            && player.VerticalSpeed > 0f)
        {
            output.Jump = true;
        }
    }

    private static void SteerRecovery(PlayerEntity player, float dx, ref SteeringOutput output)
    {
        if (MathF.Abs(dx) > 16f)
        {
            output.MoveDirection = MathF.Sign(dx);
            return;
        }

        if (!player.IsGrounded)
        {
            output.MoveDirection = player.FacingDirectionX;
        }
    }

    private bool ShouldUseFastJumpRetry(PlayerEntity player, SimpleLevel level, PlayerTeam team, float moveDirection) =>
        _stuckEscapePhase > 0
        || IsJumpableObstacleAhead(player, level, team, moveDirection);

    private void ApplyStuckEscape(PlayerEntity player, ref SteeringOutput output)
    {
        _stuckEscapeTicks += 1;
        switch (_stuckEscapePhase)
        {
            case 1:
                if (_stuckEscapeTicks <= 5)
                {
                    output.Jump = true;
                }
                break;
            case 2:
                if (_stuckEscapeTicks <= 8)
                {
                    output.MoveDirection = output.MoveDirection == 0f
                        ? (player.FacingDirectionX > 0f ? -1f : 1f)
                        : -output.MoveDirection;
                    output.Jump = true;
                }
                break;
            case 3:
                if (_stuckEscapeTicks <= 10)
                {
                    output.MoveDirection = player.FacingDirectionX > 0f ? -1f : 1f;
                    output.Jump = true;
                    output.RequestRepath = true;
                }
                break;
        }
    }

    private void ApplyPressedBlockerHop(
        PlayerEntity player,
        SimpleLevel level,
        PlayerTeam team,
        ref SteeringOutput output)
    {
        if (!player.IsGrounded
            || output.MoveDirection == 0f
            || !IsJumpableObstacleAhead(player, level, team, output.MoveDirection))
        {
            _pressedBlockerTicks = 0;
            return;
        }

        _pressedBlockerTicks += 1;
        if (_pressedBlockerTicks >= PressedBlockerHopTicks)
        {
            output.Jump = true;
        }
    }

    private void ApplyJumpPulse(ref SteeringOutput output, bool fastRetry)
    {
        if (_jumpRetryCooldownTicks > 0)
        {
            _jumpRetryCooldownTicks -= 1;
        }

        if (!output.Jump)
        {
            return;
        }

        if (fastRetry && _jumpRetryCooldownTicks > FastObstacleJumpRetryCooldownTicks)
        {
            _jumpRetryCooldownTicks = FastObstacleJumpRetryCooldownTicks;
        }

        if (_jumpRetryCooldownTicks > 0)
        {
            output.Jump = false;
            return;
        }

        _jumpRetryCooldownTicks = fastRetry ? FastObstacleJumpRetryCooldownTicks : JumpRetryCooldownTicks;
    }

    private void TrackJumpRequest(NavEdgeKind edgeKind, SteeringOutput output)
    {
        if (edgeKind == NavEdgeKind.Jump && output.Jump)
        {
            _currentEdgeJumpRequested = true;
        }
    }

    private void ApplyCommitment(ref SteeringOutput output)
    {
        if (_commitTicksRemaining > 0)
        {
            _commitTicksRemaining -= 1;
            if (output.MoveDirection != 0f && output.MoveDirection != _commitDirectionX)
            {
                output.MoveDirection = _commitDirectionX;
            }

            return;
        }

        if (output.MoveDirection != 0f && output.MoveDirection != _commitDirectionX)
        {
            _commitDirectionX = output.MoveDirection;
            _commitTicksRemaining = MinCommitTicks;
        }
    }

    private static bool IsApproachingCliff(PlayerEntity player, SimpleLevel level, PlayerTeam team, float direction)
    {
        if (direction == 0f)
        {
            return false;
        }

        var probeX = player.X + (direction * EdgeProbeDistance);
        return player.CanOccupy(level, team, probeX, player.Y)
            && player.CanOccupy(level, team, probeX, player.Y + 13f);
    }

    private static bool WouldHitWall(PlayerEntity player, SimpleLevel level, PlayerTeam team, float direction)
    {
        if (direction == 0f)
        {
            return false;
        }

        return !player.CanOccupy(level, team, player.X + (direction * 4f), player.Y);
    }

    private static bool IsJumpableObstacleAhead(PlayerEntity player, SimpleLevel level, PlayerTeam team, float direction)
    {
        if (direction == 0f
            || player.CanOccupy(level, team, player.X + (direction * 4f), player.Y))
        {
            return false;
        }

        return CanClearObstacleAtLift(player, level, team, direction, 16f)
            || CanClearObstacleAtLift(player, level, team, direction, 28f)
            || CanClearObstacleAtLift(player, level, team, direction, 40f);
    }

    private static bool CanClearObstacleAtLift(
        PlayerEntity player,
        SimpleLevel level,
        PlayerTeam team,
        float direction,
        float lift)
    {
        var liftedY = player.Y - lift;
        return player.CanOccupy(level, team, player.X, liftedY)
            && player.CanOccupy(level, team, player.X + (direction * 4f), liftedY)
            && player.CanOccupy(level, team, player.X + (direction * EdgeProbeDistance), liftedY);
    }

    private static float GetMoveDirection(float dx)
    {
        return MathF.Abs(dx) <= HorizontalDeadZone ? 0f : MathF.Sign(dx);
    }

    private static float Distance(float ax, float ay, float bx, float by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static bool IsLaunchRecipeReady(PlayerEntity player, NavEdgeLaunchRecipe recipe, float moveDirection)
    {
        if (!recipe.HasRecipe || !recipe.ContainsLaunchState(player))
        {
            return false;
        }

        var expectedMoveDirection = MathF.Sign(recipe.ExpectedMoveDirectionX);
        return expectedMoveDirection == 0f
            || moveDirection == expectedMoveDirection
            || MathF.Sign(player.HorizontalSpeed) == expectedMoveDirection;
    }

    private static bool IsInLaunchPositionWindow(PlayerEntity player, NavEdgeLaunchRecipe recipe) =>
        recipe.HasRecipe
        && player.X >= recipe.LaunchMinX
        && player.X <= recipe.LaunchMaxX
        && player.Y >= recipe.LaunchMinY
        && player.Y <= recipe.LaunchMaxY;

    private static bool IsInLaunchSpeedWindow(PlayerEntity player, NavEdgeLaunchRecipe recipe) =>
        recipe.HasRecipe
        && player.HorizontalSpeed >= recipe.LaunchMinHorizontalSpeed
        && player.HorizontalSpeed <= recipe.LaunchMaxHorizontalSpeed;

    private static float ResolveCertifiedRunupSpeed(int jumpTriggerTick) =>
        jumpTriggerTick <= 3 ? 55f : MinimumDelayedJumpRunupSpeed;

    private static float ResolveCertifiedLaunchForwardTolerance(int jumpTriggerTick) =>
        jumpTriggerTick <= 3 ? ShortCertifiedLaunchForwardTolerance : CertifiedLaunchForwardTolerance;

}

public enum SteeringState : byte
{
    Grounded = 0,
    Airborne = 1,
    Falling = 2,
    Recovery = 3,
}

internal enum EdgeExecutionPhase : byte
{
    None = 0,
    LandedBelowCompletion = 1,
}

public struct SteeringOutput
{
    public float MoveDirection { get; set; }

    public bool Jump { get; set; }

    public bool DropDown { get; set; }

    public bool HasAimOverride { get; set; }

    public float AimOverrideX { get; set; }

    public float AimOverrideY { get; set; }

    public bool RequestRepath { get; set; }

    public SteeringState State { get; set; }

    public NavEdgeKind EdgeKind { get; set; }

    public SteeringRecipeTrace RecipeTrace { get; set; }

    public SteeringFailedEdge FailedEdge { get; set; }
}

public readonly record struct SteeringFailedEdge(
    bool HasFailure,
    int FromNode,
    int ToNode,
    NavEdgeKind Kind,
    int EdgeTicks,
    string Reason);

public readonly record struct SteeringRecipeTrace(
    bool HasRecipe,
    int FromNode,
    int ToNode,
    int EdgeTicks,
    bool StartGrounded,
    float StartX,
    float StartY,
    float StartHorizontalSpeed,
    float StartVerticalSpeed,
    bool RecipeStartGrounded,
    int RecipeLaunchTick,
    float RecipeLaunchMinX,
    float RecipeLaunchMaxX,
    float RecipeLaunchMinY,
    float RecipeLaunchMaxY,
    float RecipeLaunchMinHorizontalSpeed,
    float RecipeLaunchMaxHorizontalSpeed,
    float RecipeExpectedMoveDirectionX,
    bool CurrentGrounded,
    float CurrentX,
    float CurrentY,
    float CurrentHorizontalSpeed,
    float CurrentVerticalSpeed,
    bool InLaunchXWindow,
    bool InLaunchYWindow,
    bool InLaunchSpeedWindow,
    bool DirectionMatches,
    bool StartMatches,
    bool RecipeReady,
    bool SuppressJumpUntilLaunch,
    float RequestedMoveDirection,
    float FinalMoveDirection,
    bool RequestedJump,
    bool FinalJump,
    float SteeringDx);
