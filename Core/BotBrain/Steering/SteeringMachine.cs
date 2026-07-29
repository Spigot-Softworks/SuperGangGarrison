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
    private const float RuntimeLaunchCenterTolerance = 1f;
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
        var initialPathIndex = path?.CurrentIndex ?? -1;

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

        // A waypoint handoff can expose the next OG2 contact during this
        // steering update, after the controller's runtime resolver has
        // already run for the previous edge. Do not launch the newly-entered
        // contact from a stale/in-flight state; let the next think resolve its
        // measured recipe from the live state first.
        if (path.CurrentIndex > initialPathIndex
            && IsAwaitingRuntimeContactResolution(path))
        {
            return output;
        }

        var targetNode = graph.GetNode(path.CurrentNode);
        var dx = targetNode.X - player.X;
        var dy = targetNode.Y - player.Y;
        UpdateState(player);
        UpdateStuckDetection(player);

        var hasCurrentEdge = path.TryGetCurrentEdge(out var currentEdge);
        var edgeKind = hasCurrentEdge ? currentEdge.Kind : NavEdgeKind.Walk;
        var edgeTicks = UpdateCurrentEdgeTimer(player, path, hasCurrentEdge);
        if (hasCurrentEdge && currentEdge.Kind == NavEdgeKind.Jump && edgeTicks == 0)
        {
            // The preceding walk edge may have committed the opposite
            // direction. A measured jump run-up must begin from this edge's
            // launch direction, not from stale corridor momentum.
            _commitTicksRemaining = 0;
            _commitDirectionX = 0f;
        }
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
                    ResolveJumpTriggerTick(currentEdge),
                    edgeTicks,
                    ShouldAssistSemanticWalkClimb(player, currentEdge),
                    RequiresCertifiedRunup(player, currentEdge, level.Mode),
                    currentEdge.ProbeTicks > 0 && currentEdge.JumpTriggerTick > 0,
                    RequiresLaunchReadinessWait(player, path, currentEdge, level.Mode),
                    currentEdge.IsOg2Contact,
                    currentEdge.IsRuntimeResolved,
                    currentEdge.IsOg2Contact
                        && _currentEdgeLandedAfterAirborne
                        && graph.IsOnAcceptedCompletionSurface(player.X, player.Y, currentEdge.Completion),
                    currentEdge.LaunchRecipe,
                    ref output);
                break;
            case SteeringState.Airborne:
            case SteeringState.Falling:
                SteerAirborne(
                    player,
                    edgeKind,
                    currentEdge.Completion,
                    steeringDx,
                    dy,
                    currentEdge.IsOg2Contact,
                    currentEdge.ProbeMoveDirectionX,
                    currentEdge.JumpTriggerTick,
                    edgeTicks,
                    currentEdge.LaunchRecipe,
                    ref output);
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

        var hasCertifiedContactEdge = hasCurrentEdge
            && currentEdge.IsOg2Contact
            && currentEdge.LaunchRecipe.HasRecipe;
        var hasOg2ContactEdge = hasCurrentEdge && currentEdge.IsOg2Contact;
        if (_stuckEscapePhase > 0 && !hasCertifiedContactEdge && !hasOg2ContactEdge)
        {
            ApplyStuckEscape(player, ref output);
        }
        if (!hasCertifiedContactEdge && !hasOg2ContactEdge)
        {
            ApplyPressedBlockerHop(player, level, team, ref output);
        }

        // A jump edge's launch direction is part of its OG2 proof. Do not let
        // the short walking commitment from the preceding edge turn a right
        // launch into a left launch (or vice versa).
        if (hasCurrentEdge && currentEdge.Kind == NavEdgeKind.Jump && output.Jump)
        {
            _commitTicksRemaining = 0;
            _commitDirectionX = 0f;
        }

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

        // A certified launch window is a stronger movement contract than the
        // short directional commitment used for ordinary corridor steering.
        // If the bot enters the band above the measured launch speed, cancel
        // the stale commitment before it can overwrite the braking direction
        // selected by SteerGrounded and carry the bot past the recipe window.
        if (hasCurrentEdge
            && currentEdge.Kind == NavEdgeKind.Jump
            && RequiresCertifiedRunup(player, currentEdge, level.Mode)
            && player.IsGrounded
            && IsInLaunchPositionWindow(player, currentEdge.LaunchRecipe)
            && (currentEdge.IsRuntimeResolved
                || player.HorizontalSpeed > currentEdge.LaunchRecipe.LaunchMaxHorizontalSpeed))
        {
            // Runtime contacts may enter the measured band carrying the
            // opposite commitment from the preceding edge. Let the resolver
            // own the run-up direction until it reaches the measured launch
            // state; otherwise a Heavy/diagonal stair handoff can oscillate
            // forever without ever satisfying the runtime schedule.
            _commitTicksRemaining = 0;
            _commitDirectionX = 0f;
        }

        TrackJumpRequest(edgeKind, output);
        var runtimeContactOwnsRunup = hasCurrentEdge
            && currentEdge.Kind == NavEdgeKind.Jump
            && currentEdge.IsRuntimeResolved
            && currentEdge.LaunchRecipe.HasRecipe;
        if (runtimeContactOwnsRunup)
        {
            // A runtime contact is measured from the current live state. A
            // short commitment inherited from the preceding edge would make
            // the executor diverge from that measured run-up before the
            // launch tick is reached.
            _commitTicksRemaining = 0;
            _commitDirectionX = 0f;
        }
        else
        {
            ApplyCommitment(ref output);
        }
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
        if (path.CurrentIndex == 0
            && player.IsGrounded
            && targetNode.SurfaceId.HasValue
            && MathF.Abs(dx) <= WaypointReachRadius)
        {
            // Surface nodes use a class-neutral envelope Y. A grounded bot
            // can therefore be attached to the correct surface while its
            // class-specific standing Y differs from the node by the body
            // envelope. Initial attachment is horizontal/surface-based.
            return true;
        }

        if (distSq < WaypointReachRadius * WaypointReachRadius)
        {
            if (!player.IsGrounded
                && path.TryGetIncomingEdge(path.CurrentIndex, out var incomingEdge)
                && incomingEdge.RequiresGroundedContinuation)
            {
                return false;
            }

            if (!player.IsGrounded && NextEdgeRequiresGroundedContact(path))
            {
                // A fall/drop can satisfy the current waypoint while the
                // player is still airborne above its destination surface. Do
                // not hand the route to a grounded OG2 contact until the live
                // body has actually landed on that surface.
                return false;
            }

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
            && !ShouldDeferContactHandoff(path, edge, distSq)
            && (player.IsGrounded
                ? graph.IsEdgeCompletionSatisfied(player.X, player.Y, edge.Completion)
                    || IsNearGroundedContinuationCompletion(player, edge, level)
                : player.ClassId != PlayerClass.Heavy
                    && (IsAirborneCompletionContinuation(player, graph, edge)
                        || IsNearGroundedContinuationCompletion(player, edge, level)));
    }

    private static bool ShouldDeferContactHandoff(NavPath path, NavEdge edge, float distanceSquared)
    {
        if (distanceSquared <= WaypointReachRadius * WaypointReachRadius
            || !edge.IsOg2Contact
            || edge.Kind != NavEdgeKind.Jump
            || path.CurrentIndex + 1 >= path.Count
            || !path.TryGetIncomingEdge(path.CurrentIndex + 1, out var nextEdge)
            || !nextEdge.IsOg2Contact
            || nextEdge.Kind != NavEdgeKind.Jump
            || edge.ProbeMoveDirectionX == 0f
            || nextEdge.ProbeMoveDirectionX == 0f)
        {
            return false;
        }

        // A reverse-direction contact chain needs the current landing node as
        // its state handoff. Advancing from the outer completion band can
        // feed the next recipe the preceding jump's residual momentum before
        // the bot has actually reached the node the probe started from.
        return MathF.Sign(edge.ProbeMoveDirectionX)
            != MathF.Sign(nextEdge.ProbeMoveDirectionX);
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
        edge.IsOg2Contact
        && edge.Kind is NavEdgeKind.Jump or NavEdgeKind.Walk
        && !edge.RequiresGroundedContinuation
        && (edge.Kind == NavEdgeKind.Walk || player.VerticalSpeed >= -15f)
        && graph.IsEdgeCompletionSatisfied(player.X, player.Y, edge.Completion with
        {
            MaxY = edge.Completion.MaxY + AirborneCompletionContinuationSlack,
        });

    private static bool NextEdgeRequiresGroundedLaunch(NavPath path) =>
        path.CurrentIndex + 1 < path.Count
        && path.TryGetIncomingEdge(path.CurrentIndex + 1, out var nextEdge)
        && RequiresVerticalCertifiedLaunch(nextEdge);

    private static bool NextEdgeRequiresGroundedContact(NavPath path) =>
        path.CurrentIndex + 1 < path.Count
        && path.TryGetIncomingEdge(path.CurrentIndex + 1, out var nextEdge)
        && nextEdge.IsOg2Contact
        && nextEdge.LaunchRecipe.StartGrounded;

    private static bool IsAwaitingRuntimeContactResolution(NavPath path) =>
        path.CurrentIndex > 0
        && path.TryGetCurrentEdge(out var edge)
        && edge.IsOg2Contact
        && edge.LaunchRecipe.HasRecipe
        && !edge.IsRuntimeResolved;

    private static int ResolveJumpTriggerTick(NavEdge edge) =>
        edge.Kind == NavEdgeKind.Jump
            ? edge.JumpTriggerTick
            : 0;

    private static bool RequiresCertifiedRunup(PlayerEntity player, NavEdge edge, GameModeKind mode) =>
        edge.Kind == NavEdgeKind.Jump
        && edge.LaunchRecipe.HasRecipe
        && edge.ProbeMoveDirectionX != 0f
        && (edge.IsOg2Contact
            || player.ClassId == PlayerClass.Soldier
            || player.ClassId == PlayerClass.Sniper
            || (mode == GameModeKind.CaptureTheFlag && player.ClassId == PlayerClass.Engineer)
            || (mode == GameModeKind.CaptureTheFlag && player.ClassId == PlayerClass.Heavy));

    private bool RequiresLaunchReadinessWait(
        PlayerEntity player,
        NavPath path,
        NavEdge edge,
        GameModeKind mode)
    {
        if (!RequiresCertifiedRunup(player, edge, mode)
            || !_edgeStartGrounded
            || path.CurrentIndex < 2
            || !edge.LaunchRecipe.HasRecipe)
        {
            return false;
        }

        var launchDirection = MathF.Sign(edge.LaunchRecipe.ExpectedMoveDirectionX);
        if (launchDirection == 0f
            || (_edgeStartHorizontalSpeed * launchDirection) >= -8f
            || !path.TryGetIncomingEdge(path.CurrentIndex - 1, out var previousEdge)
            || previousEdge.ProbeMoveDirectionX == 0f)
        {
            return false;
        }

        // A predecessor travelling against the next jump's launch direction
        // needs a real braking/setup phase. The isolated OG2 contact proof is
        // still authoritative; this only prevents the executor from firing
        // the recorded tick before the live state reaches that proof.
        return MathF.Sign(previousEdge.ProbeMoveDirectionX) != launchDirection;
    }

    private static bool RequiresVerticalCertifiedLaunch(NavEdge edge) =>
        edge.Completion.HasWindow
        && edge.LaunchRecipe.HasRecipe
        && edge.LaunchRecipe.LaunchMinY - edge.Completion.MaxY >= 20f;

    private int UpdateCurrentEdgeTimer(
        PlayerEntity player,
        NavPath path,
        bool hasCurrentEdge)
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
                && incomingEdge.RequiresGroundedContinuation)
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
            && edge.IsOg2Contact
            && edge.LaunchRecipe.HasRecipe
            && player.IsGrounded
            && !_currentEdgeLandedAfterAirborne)
        {
            var launchDirection = MathF.Sign(edge.LaunchRecipe.ExpectedMoveDirectionX);
            if (launchDirection != 0f)
            {
                var launchCenterX = (edge.LaunchRecipe.LaunchMinX + edge.LaunchRecipe.LaunchMaxX) * 0.5f;
                var launchDx = launchCenterX - player.X;
                if (edge.IsRuntimeResolved)
                {
                    // The runtime probe is the executable schedule for this
                    // edge: it holds the measured launch direction until the
                    // measured jump tick. Counter-steering against the live
                    // speed window changes that schedule and can oscillate
                    // forever around the launch band.
                    return launchDirection * MathF.Max(JumpTriggerDistance, MathF.Abs(launchDx));
                }

                var inLaunchWindow = IsInLaunchPositionWindow(player, edge.LaunchRecipe);
                var launchGateDistance = launchDirection > 0f
                    ? edge.LaunchRecipe.LaunchMinX - player.X
                    : player.X - edge.LaunchRecipe.LaunchMaxX;
                var oneTickTravelDistance = MathF.Abs(player.HorizontalSpeed)
                    / LegacyMovementModel.SourceTicksPerSecond;
                if (player.IsGrounded
                    && player.HorizontalSpeed * launchDirection > edge.LaunchRecipe.LaunchMaxHorizontalSpeed
                    && launchGateDistance > 0f
                    && launchGateDistance <= MathF.Max(4f, oneTickTravelDistance * 1.25f))
                {
                    // The launch band is narrow enough that waiting until the
                    // player is inside it can be one fixed update too late.
                    // Begin the measured counter-steer when the next update
                    // would enter the band above its certified speed.
                    suppressJumpUntilLaunch = true;
                    return -launchDirection * JumpTriggerDistance;
                }

                if (!inLaunchWindow
                    || !IsInLaunchSpeedWindow(player, edge.LaunchRecipe))
                {
                    suppressJumpUntilLaunch = true;
                    if (inLaunchWindow)
                    {
                        var tooSlowInLaunchDirection = launchDirection > 0f
                            ? player.HorizontalSpeed < edge.LaunchRecipe.LaunchMinHorizontalSpeed
                            : player.HorizontalSpeed > edge.LaunchRecipe.LaunchMaxHorizontalSpeed;
                        return tooSlowInLaunchDirection
                            ? launchDirection * JumpTriggerDistance
                            : -launchDirection * JumpTriggerDistance;
                    }

                    // Keep the contact's launch direction active even when
                    // the center is inside the steering dead zone. A single
                    // neutral update here can bleed enough speed to make the
                    // measured launch state unreachable before the timer.
                    return MathF.Abs(launchDx) > HorizontalDeadZone
                        ? launchDx
                        : launchDirection * (HorizontalDeadZone + 1f);
                }
            }
        }

        // Do not counter-steer into the completion-center X when the next
        // certified jump continues in the same direction. That reversal can
        // leave a live bot on the landing surface with momentum opposite to
        // the next launch recipe, even though both contacts are individually
        // valid in the OG2 probe.
        if (edge.Kind == NavEdgeKind.Jump
            && !player.IsGrounded
            && edge.ProbeMoveDirectionX != 0f
            && path.CurrentIndex + 1 < path.Count
            && path.TryGetIncomingEdge(path.CurrentIndex + 1, out var nextEdge)
            && nextEdge.Kind == NavEdgeKind.Jump
            && nextEdge.ProbeMoveDirectionX != 0f
            && MathF.Sign(nextEdge.ProbeMoveDirectionX) == MathF.Sign(edge.ProbeMoveDirectionX)
            && player.X >= edge.Completion.MinX - 24f
            && player.X <= edge.Completion.MaxX + 24f
            && player.Y >= edge.Completion.MinY - 48f
            && player.Y <= edge.Completion.MaxY + 24f)
        {
            return edge.ProbeMoveDirectionX * MathF.Max(JumpTriggerDistance, MathF.Abs(waypointDx));
        }

        if (edge.Kind == NavEdgeKind.Jump
            && player.IsGrounded
            && player.Y > edge.Completion.MaxY + 8f
            && MathF.Abs(waypointDx) > JumpTriggerDistance
            && path.CurrentIndex > 0)
        {
            var launchNode = graph.GetNode(path.GetWaypoint(path.CurrentIndex - 1));
            // Contact edges carry a measured launch band which is not
            // necessarily centered on the source node. This is especially
            // important on the reverse OG2 stair chain: the source node is
            // the landing point of the preceding step, while the certified
            // run-up begins several pixels back toward the next jump. Pulling
            // back to the source node after entering that band makes the
            // executor oscillate forever and invalidates an otherwise valid
            // contact proof.
            var launchTargetX = edge.IsOg2Contact && edge.LaunchRecipe.HasRecipe
                ? (edge.LaunchRecipe.LaunchMinX + edge.LaunchRecipe.LaunchMaxX) * 0.5f
                : launchNode.X;
            var launchDx = launchTargetX - player.X;
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
            var isNearLaunchWindow = IsNearCertifiedLaunchWindow(player, edge.LaunchRecipe, travelDirection);
            if (travelDirection != 0f
                && !isNearLaunchWindow
                && signedDistanceFromLaunch < -JumpLaunchGateTolerance)
            {
                suppressJumpUntilLaunch = true;
                return launchNode.X - player.X;
            }

            if (travelDirection != 0f
                && RequiresCertifiedRunup(player, edge, mode)
                && !IsInLaunchPositionWindow(player, edge.LaunchRecipe)
                        && (isNearLaunchWindow
                            || (signedDistanceFromLaunch > ResolveCertifiedLaunchForwardTolerance(edge.JumpTriggerTick)
                        && player.Y > edge.Completion.MaxY + 8f
                        && MathF.Abs(waypointDx) > JumpTriggerDistance)))
            {
                suppressJumpUntilLaunch = true;
                return isNearLaunchWindow
                    ? (edge.LaunchRecipe.LaunchMinX + edge.LaunchRecipe.LaunchMaxX) * 0.5f - player.X
                    : edge.IsOg2Contact && edge.LaunchRecipe.HasRecipe
                        ? (edge.LaunchRecipe.LaunchMinX + edge.LaunchRecipe.LaunchMaxX) * 0.5f - player.X
                        : launchNode.X - player.X;
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
            RuntimeResolved: edge.IsRuntimeResolved,
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
            || (!edge.IsRuntimeResolved && !player.IsCarryingIntel)
            || (!edge.IsRuntimeResolved
                && player.ClassId != PlayerClass.Heavy
                && player.ClassId != PlayerClass.Soldier)
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
        if (edge.Kind == NavEdgeKind.Walk
            && !player.IsGrounded
            && player.VerticalSpeed > 0f
            && targetDistance <= WaypointReachRadius * 2f)
        {
            // A walk contact can be entered while the previous transition is
            // still settling. If the body is descending toward the target,
            // give the live collision state a chance to land before declaring
            // the corridor edge lost.
            return;
        }

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
        if (edge.Kind == NavEdgeKind.Walk && edge.ProbeTicks > 0)
        {
            // Contact-first alpha walk links carry the measured OG2 sweep
            // duration. Long stair chains are still physically validated, but
            // they cannot fit the legacy fixed 60-tick waypoint budget.
            return int.Clamp(edge.ProbeTicks + CertifiedEdgeRetrySlackTicks, MaximumWalkEdgeTicks, MaximumCertifiedEdgeTicks);
        }

        if (edge.Kind == NavEdgeKind.Walk)
        {
            return MaximumWalkEdgeTicks;
        }

        if (edge.Kind == NavEdgeKind.Jump && edge.LaunchRecipe.HasRecipe)
        {
            // A measured contact includes a grounded run-up before the jump.
            // Runtime entry can inherit different momentum and spend part of
            // the edge reacquiring its launch state, so probe duration alone
            // is not a valid watchdog. Keep the bound finite, but allow the
            // full certified execution budget for late landings.
            return MaximumCertifiedEdgeTicks;
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
        bool requiresMeasuredRunup,
        bool waitForLaunchRecipe,
        bool isOg2Contact,
        bool isRuntimeResolved,
        bool holdContactLanding,
        NavEdgeLaunchRecipe launchRecipe,
        ref SteeringOutput output)
    {
        var moveDir = GetMoveDirection(dx);

        switch (edgeKind)
        {
            case NavEdgeKind.Jump:
                output.MoveDirection = moveDir;
                if (holdContactLanding)
                {
                    // The OG2 contact already landed on the certified target
                    // surface. Finish the measured completion window instead
                    // of re-firing the jump from a slightly early landing.
                    break;
                }

                var recipeReady = launchRecipe.HasRecipe
                    && IsLaunchRecipeReady(player, launchRecipe, moveDir);
                var runtimeLaunchCenterX = launchRecipe.HasRecipe
                    ? (launchRecipe.LaunchMinX + launchRecipe.LaunchMaxX) * 0.5f
                    : player.X;
                var runtimeLaunchCenterY = launchRecipe.HasRecipe
                    ? (launchRecipe.LaunchMinY + launchRecipe.LaunchMaxY) * 0.5f
                    : player.Y;
                var runtimeAtMeasuredLaunchState = launchRecipe.HasRecipe
                    && MathF.Abs(player.X - runtimeLaunchCenterX) <= RuntimeLaunchCenterTolerance
                    && MathF.Abs(player.Y - runtimeLaunchCenterY) <= RuntimeLaunchCenterTolerance
                    && IsInLaunchSpeedWindow(player, launchRecipe);
                var runtimeScheduleReady = isRuntimeResolved
                    && edgeTicks >= Math.Max(0, jumpTriggerTick)
                    && runtimeAtMeasuredLaunchState;
                var measuredContactLaunchWindow = isOg2Contact
                    && launchRecipe.HasRecipe
                    && IsInLaunchPositionWindow(player, launchRecipe)
                    && edgeTicks >= Math.Max(0, jumpTriggerTick - 4);
                if (requiresCertifiedRunup
                    && !isRuntimeResolved
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
                    || requiresMeasuredRunup
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
                var jumpTimingSatisfied = isOg2Contact && launchRecipe.HasRecipe
                    // A live-resolved contact carries a probe schedule that
                    // starts from the actual entry state. Honor that measured
                    // tick and the launch state together; otherwise steering
                    // can choose a different jump frame than the successful
                    // runtime probe and land below the completion surface.
                    ? (isRuntimeResolved
                        ? runtimeScheduleReady
                        : recipeReady)
                    : waitForLaunchRecipe
                        ? edgeTicks >= jumpTriggerTick
                            && recipeReady
                        : edgeTicks >= jumpTriggerTick
                            || measuredContactLaunchWindow
                            || (jumpTriggerTick <= 0 && recipeReady);
                if (jumpTimingSatisfied
                    && (isRuntimeResolved || recipeReady || runupSatisfied)
                    && (isRuntimeResolved || recipeReady
                        || (!suppressJumpUntilLaunch
                            && (MathF.Abs(dx) <= JumpTriggerDistance
                                || dy <= TargetAboveJumpThreshold
                                || dy >= -TargetAboveJumpThreshold
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
                var jumpableObstacleAhead = !isOg2Contact
                    && IsJumpableObstacleAhead(player, level, team, moveDir);
                if (assistSemanticWalkClimb && edgeTicks >= 0)
                {
                    output.Jump = true;
                }

                if (!isOg2Contact
                    && (jumpableObstacleAhead
                    || (WouldHitWall(player, level, team, moveDir) && dy <= TargetAboveJumpThreshold))
                    )
                {
                    output.Jump = true;
                }

                if (!isOg2Contact
                    && dy <= TargetAboveJumpThreshold
                    && IsApproachingCliff(player, level, team, moveDir))
                {
                    output.Jump = true;
                }
                break;
        }
    }

    private static bool ShouldAssistSemanticWalkClimb(PlayerEntity player, NavEdge edge) =>
        edge.Kind == NavEdgeKind.Walk
        && !edge.IsOg2Contact
        && edge.Completion.HasWindow
        && player.IsGrounded
        && player.Y > edge.Completion.MaxY + LandedBelowCompletionVerticalSlack;

    private static void SteerAirborne(
        PlayerEntity player,
        NavEdgeKind edgeKind,
        NavEdgeCompletion completion,
        float dx,
        float dy,
        bool isOg2Contact,
        float probeMoveDirectionX,
        int jumpTriggerTick,
        int edgeTicks,
        NavEdgeLaunchRecipe launchRecipe,
        ref SteeringOutput output)
    {
        output.MoveDirection = isOg2Contact && probeMoveDirectionX != 0f
            ? MathF.Sign(probeMoveDirectionX)
            : GetMoveDirection(dx);

        // Runtime contact proof may select a class-specific horizontal control
        // schedule. Match the probe after the launch input has been consumed;
        // otherwise a faster class can invalidate the successful landing by
        // continuing to hold the approach direction through the air.
        if (isOg2Contact
            && edgeKind == NavEdgeKind.Jump
            && launchRecipe.HasRecipe
            && launchRecipe.AirControlMode != NavEdgeAirControlMode.HoldDirection
            && edgeTicks > jumpTriggerTick + launchRecipe.AirControlHoldTicks)
        {
            output.MoveDirection = launchRecipe.AirControlMode == NavEdgeAirControlMode.CounterSteer
                ? -output.MoveDirection
                : 0f;
        }

        // Some OG2 contacts are intentionally discovered as a walk-off
        // followed by an air jump. Their edge entry is grounded, but the
        // recorded jump input must be consumed after the bot leaves support.
        // Keep that distinction explicit instead of converting the contact
        // into an ordinary grounded jump or waiting for a generic recovery
        // hop during descent.
        if (isOg2Contact
            && edgeKind == NavEdgeKind.Jump
            && launchRecipe.HasRecipe
            && !launchRecipe.JumpStartsGrounded
            && jumpTriggerTick >= 0
            && edgeTicks >= jumpTriggerTick
            && player.RemainingAirJumps > 0)
        {
            output.Jump = true;
        }

        // Airborne movement keeps residual horizontal momentum. Once a
        // measured jump is inside its completion approach band, counter-steer
        // that momentum instead of continuing to accelerate through the
        // landing window. This is especially important for movement profiles
        // whose live entry speed differs from the isolated OG2 contact probe.
        if (!isOg2Contact
            && edgeKind == NavEdgeKind.Jump
            && completion.HasWindow
            && player.HorizontalSpeed != 0f
            && (output.MoveDirection == 0f
                || MathF.Sign(player.HorizontalSpeed) == output.MoveDirection)
            && MathF.Abs(dx) <= MathF.Max(48f, MathF.Abs(player.HorizontalSpeed) * 0.45f))
        {
            output.MoveDirection = -MathF.Sign(player.HorizontalSpeed);
        }

        if ((edgeKind == NavEdgeKind.Jump
                || edgeKind == NavEdgeKind.Walk && completion.HasWindow)
            && !(isOg2Contact
                && launchRecipe.HasRecipe
                && launchRecipe.StartGrounded
                && !player.IsGrounded)
            && player.RemainingAirJumps > 0
            && dy < -8f
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

    private static bool IsNearCertifiedLaunchWindow(
        PlayerEntity player,
        NavEdgeLaunchRecipe recipe,
        float travelDirection)
    {
        if (!recipe.HasRecipe || travelDirection == 0f)
        {
            return false;
        }

        const float recoveryBand = 24f;
        return travelDirection < 0f
            ? player.X > recipe.LaunchMaxX && player.X - recipe.LaunchMaxX <= recoveryBand
            : player.X < recipe.LaunchMinX && recipe.LaunchMinX - player.X <= recoveryBand;
    }

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
    bool RuntimeResolved,
    bool SuppressJumpUntilLaunch,
    float RequestedMoveDirection,
    float FinalMoveDirection,
    bool RequestedJump,
    bool FinalJump,
    float SteeringDx);
