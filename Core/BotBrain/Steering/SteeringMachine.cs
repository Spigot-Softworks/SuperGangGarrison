namespace OpenGarrison.Core.BotBrain;

/// <summary>
/// Converts a NavPath into raw Left/Right/Up/Down steering intent.
/// </summary>
public sealed class SteeringMachine
{
    private const int MinCommitTicks = 3;
    private const int StuckDetectionWindow = 15;
    private const float StuckDistanceThreshold = 2f;
    private const int GroundedWalkProgressRepathWindows = 2;
    private const float WaypointReachRadius = NavGraphBuilder.WaypointArrivalRadius;
    private const int WaypointLookaheadSkipCount = 4;
    private const float WaypointLookaheadReachMultiplier = 1.5f;
    private const float InitialWalkAttachmentVerticalTolerance = 24f;
    private const float EdgeProbeDistance = 18f;
    private const float JumpTriggerDistance = 32f;
    // Runtime contact recipes record a launch window, not a single exact
    // pixel. Requiring the live body to revisit the probe's exact center
    // makes a valid contact miss after ordinary collision rounding or a
    // one-tick handoff from the preceding edge; the edge then waits for its
    // watchdog and repaths. Keep this inside the measured positional window
    // while allowing the live executor to use the same certified contact.
    private const float RuntimeLaunchCenterTolerance = 6f;
    private const float JumpLaunchGateTolerance = 4f;
    private const float CertifiedLaunchForwardTolerance = 16f;
    private const float ShortCertifiedLaunchForwardTolerance = 24f;
    private const float DropTriggerDistance = 18f;
    private const float HorizontalDeadZone = 5f;
    private const float TargetAboveJumpThreshold = -8f;
    private const int JumpRetryCooldownTicks = 6;
    private const int FastObstacleJumpRetryCooldownTicks = 2;
    private const int PressedBlockerHopTicks = 3;
    private const int PressedBlockerRepathTicks = 3;
    private const float MinimumDelayedJumpRunupSpeed = 80f;
    private const int MaximumDelayedJumpRunupTicks = 18;
    private const int GroundedContinuationRecoveryTicks = 45;
    private const int LandedBelowCompletionRecoveryTicks = 90;
    private const int LandedBelowCompletionFastFailSlackTicks = 8;
    private const int AlphaLandedBelowCompletionRecoveryTicks = 18;
    private const float LandedBelowCompletionVerticalSlack = 8f;
    private const int GroundedWalkBelowTargetFastFailTicks = 8;
    private const float GroundedWalkBelowTargetVerticalSlack = 48f;
    private const float GroundedContinuationCompletionSlack = 8f;
    private const float AirborneCompletionContinuationSlack = 8f;
    // A failed contact must relinquish control quickly after the bot has
    // landed away from its completion surface.  The previous four-second
    // watchdog made a knocked-off bot look inert even though the route was
    // requesting recovery.
    private const int MaximumCertifiedEdgeTicks = 72;
    private const int RuntimeResolvedEdgeSlackTicks = 12;
    private const int MinimumRuntimeResolvedEdgeTicks = 90;
    private const int MaximumRuntimeResolvedEdgeTicks = 120;
    private const float RuntimeResolvedFarBelowCompletionVerticalSlack = 144f;
    private const int CertifiedEdgeRetrySlackTicks = 36;
    private const int MaximumUncertifiedTraversalEdgeTicks = 180;
    private const int MaximumWalkEdgeTicks = 60;
    private const int TopDownStuckDetectionWindowTicks = 12;
    private const float TopDownStuckDistanceThreshold = 2.5f;
    private const float TopDownObstacleProbeDistance = 8f;
    private const int TopDownDetourCommitTicks = 10;

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
    private bool _currentEdgeCompletionSatisfiedGrounded;
    private bool _currentEdgeJumpRequested;
    private EdgeExecutionPhase _edgePhase = EdgeExecutionPhase.None;
    private int _edgePhaseTicks;
    private bool _edgeStartGrounded;
    private float _edgeStartX;
    private float _edgeStartY;
    private float _edgeStartHorizontalSpeed;
    private float _edgeStartVerticalSpeed;
    private int _walkProgressFromNode = -1;
    private int _walkProgressToNode = -1;
    private float _walkProgressCheckX;
    private float _walkProgressCheckY;
    private int _walkProgressTicks;
    private int _walkProgressStagnantWindows;
    private int _topDownTrackedNode = -1;
    private float _topDownCheckX;
    private float _topDownCheckY;
    private int _topDownStuckTicks;
    private float _topDownDetourDirection;
    private int _topDownDetourAxis;
    private int _topDownDetourTicks;

    public SteeringOutput Update(
        PlayerEntity player,
        NavGraph graph,
        NavPath? path,
        SimpleLevel level,
        PlayerTeam team)
    {
        if (level.IsTopDown)
        {
            return UpdateTopDown(player, graph, path, level, team);
        }

        var output = new SteeringOutput();
        var initialPathIndex = path?.CurrentIndex ?? -1;

        if (path is null || path.IsComplete || !player.IsAlive)
        {
            _pressedBlockerTicks = 0;
            return output;
        }

        TrySkipInitialWalkAttachment(player, graph, path);
        var initialTargetNode = graph.GetNode(path.CurrentNode);
        var initialDx = initialTargetNode.X - player.X;
        var initialDy = initialTargetNode.Y - player.Y;
        var initialDistSq = (initialDx * initialDx) + (initialDy * initialDy);
        if (path.CurrentIndex == 0
            && path.Count > 1
            && player.ClassId == PlayerClass.Quote
            && player.IsCivviePogoActive
            && !player.IsGrounded
            && !player.IsCivviePogoSuperJumpAirPhaseActive
            && player.VerticalSpeed > 0.1f
            && initialDistSq > WaypointReachRadius * WaypointReachRadius)
        {
            // A live pogo bounce can leave the bot above a different graph
            // surface than the path's initial attachment node. Reattach from
            // that bounce before steering toward a node it cannot physically
            // occupy; otherwise the civilian holds neutral input under the
            // surface and appears inert.
            output.RequestRepath = true;
            return output;
        }
        if (TrySkipPassedWalkWaypoint(player, graph, path))
        {
            _stuckTicks = 0;
            _stuckEscapePhase = 0;
            _pressedBlockerTicks = 0;
            _jumpRetryCooldownTicks = 0;
        }

        TryAdvanceToReachedFutureWaypoint(player, graph, path);
        var hasPreAdvanceEdge = path.TryGetCurrentEdge(out var preAdvanceEdge);
        var preAdvanceCompletionSatisfied = hasPreAdvanceEdge
            && (graph.IsEdgeCompletionSatisfied(player.X, player.Y, preAdvanceEdge.Completion)
                || IsCivviePogoContactCompletionSatisfied(player, preAdvanceEdge));
        var advancedWaypoint = ShouldAdvanceWaypoint(
            player,
            graph,
            path,
            level,
            preAdvanceCompletionSatisfied);
        if (advancedWaypoint)
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
        // already run for the previous edge. Keep steering the newly-entered
        // edge, but leave it unresolved so the next think can measure its
        // recipe from the live state. Returning a neutral output here drops
        // the bot's launch surface for one tick and can turn a valid grounded
        // handoff into an airborne start with stale momentum.
        if (path.CurrentIndex > initialPathIndex
            && IsAwaitingRuntimeContactResolution(path))
        {
            // Intentionally continue through the normal edge steering below.
        }

        var targetNode = graph.GetNode(path.CurrentNode);
        var dx = targetNode.X - player.X;
        var dy = targetNode.Y - player.Y;
        UpdateState(player);
        UpdateStuckDetection(player);
        if (path.CurrentIndex == 0
            && path.Count > 1
            && player.IsGrounded
            && (dx * dx) + (dy * dy) > WaypointReachRadius * WaypointReachRadius
            && _stuckEscapePhase >= 2)
        {
            // Initial attachment has no incoming edge, so the ordinary edge
            // timeout/repath watchdog never runs. A stale first node otherwise
            // leaves every class holding its initial steering decision until a
            // later event happens to invalidate the route.
            output.RequestRepath = true;
            output.FailedEdge = new SteeringFailedEdge(
                HasFailure: true,
                FromNode: path.CurrentNode,
                ToNode: path.GetWaypoint(1),
                Kind: NavEdgeKind.Walk,
                EdgeTicks: _stuckTicks,
                Reason: "initial_node_stuck");
            return output;
        }
        var hasCurrentEdge = path.TryGetCurrentEdge(out var currentEdge);
        var edgeKind = hasCurrentEdge ? currentEdge.Kind : NavEdgeKind.Walk;
        var edgeTicks = UpdateCurrentEdgeTimer(player, path, hasCurrentEdge);
        // Several phases of one steering update need the same completion
        // answer. Reuse the result already computed for waypoint handoff when
        // the edge did not change; otherwise resolve the new edge once.
        var currentEdgeCompletionSatisfied = hasCurrentEdge
            && (!advancedWaypoint
                ? preAdvanceCompletionSatisfied
                : graph.IsEdgeCompletionSatisfied(player.X, player.Y, currentEdge.Completion)
                    || IsCivviePogoContactCompletionSatisfied(player, currentEdge));
        if (hasCurrentEdge && currentEdge.Kind == NavEdgeKind.Jump && edgeTicks == 0)
        {
            // The preceding walk edge may have committed the opposite
            // direction. A measured jump run-up must begin from this edge's
            // launch direction, not from stale corridor momentum.
            _commitTicksRemaining = 0;
            _commitDirectionX = 0f;
        }
        if (hasCurrentEdge)
        {
            UpdateCurrentEdgePhase(player, currentEdge, currentEdgeCompletionSatisfied);
            UpdateCurrentEdgeExecutionPhase(player, graph, path, currentEdge, currentEdgeCompletionSatisfied);
            var awaitingRuntimeContactResolution = ShouldAwaitRuntimeContactResolution(
                player,
                graph,
                currentEdge);
            var groundedWalkProgressStalled = UpdateGroundedWalkProgress(
                player,
                path,
                currentEdge,
                edgeTicks);
            if (!awaitingRuntimeContactResolution)
            {
                TryFailExpiredEdge(player, graph, path, currentEdge, edgeTicks, level.Mode, currentEdgeCompletionSatisfied, ref output);
            }
            if (!output.RequestRepath
                && groundedWalkProgressStalled
                && path.CurrentIndex > 0)
            {
                output.RequestRepath = true;
                output.FailedEdge = new SteeringFailedEdge(
                    HasFailure: true,
                    FromNode: path.GetWaypoint(path.CurrentIndex - 1),
                    ToNode: path.CurrentNode,
                    Kind: currentEdge.Kind,
                    EdgeTicks: edgeTicks,
                    Reason: "walk_progress_stuck");
            }
            if (!output.RequestRepath
                && !awaitingRuntimeContactResolution
                && ShouldRequestEarlyContactRecovery(player, currentEdge, edgeTicks))
            {
                output.RequestRepath = true;
                output.FailedEdge = new SteeringFailedEdge(
                    HasFailure: true,
                    FromNode: path.GetWaypoint(path.CurrentIndex - 1),
                    ToNode: path.CurrentNode,
                    Kind: currentEdge.Kind,
                    EdgeTicks: edgeTicks,
                    Reason: "contact_stuck");
            }
        }

        var suppressJumpUntilLaunch = false;
        var steeringDx = hasCurrentEdge
            ? ResolveEdgeSteeringDx(player, level.Mode, graph, path, currentEdge, dx, currentEdgeCompletionSatisfied, out suppressJumpUntilLaunch)
            : dx;

        var awaitingRuntimeContactResolutionForSteering = hasCurrentEdge
            && ShouldAwaitRuntimeContactResolution(player, graph, currentEdge);
        var allowRuntimeContactRecovery = awaitingRuntimeContactResolutionForSteering
            && _stuckEscapePhase > 0
            && player.IsGrounded;
        if (awaitingRuntimeContactResolutionForSteering
            && !allowRuntimeContactRecovery)
        {
            // A static graph recipe is only a canonical hint until it has
            // been re-proved from this live entry state. Do not fire the
            // static jump while the runtime resolver is still searching: it
            // can consume the jump and land below the completion surface,
            // which is exactly the inert-under-the-point failure mode. Keep
            // the bot moving toward the measured launch state while the
            // bounded resolver retries. Once the contact is resolved, the
            // airborne executor uses the completion window or measured
            // schedule below. ResolveEdgeSteeringDx may deliberately reverse
            // the nominal route direction when the live body has already
            // overshot the narrow launch band.
            output.State = _state;
            output.EdgeKind = edgeKind;
            var launchCorrectionDirection = MathF.Sign(steeringDx);
            output.MoveDirection = launchCorrectionDirection != 0f
                ? launchCorrectionDirection
                : MathF.Sign(currentEdge.LaunchRecipe.ExpectedMoveDirectionX);
            output.Jump = false;
            output.DropDown = false;
            return output;
        }

        if (allowRuntimeContactRecovery)
        {
            // The runtime probe has not produced an executable jump yet, but
            // the body has also failed the shared stagnation detector. Let
            // the normal escape phase run below instead of trapping the bot
            // in the unresolved-contact hold branch. Clear any static jump
            // decision first: recovery owns this tick, and the measured
            // contact must not be fired from an unproved state.
            output.Jump = false;
            output.DropDown = false;
        }

        var useAirborneSteeringDx = hasCurrentEdge
            && ShouldCounterSteerForNextContact(player, graph, path, currentEdge, currentEdgeCompletionSatisfied);
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
                        && currentEdgeCompletionSatisfied,
                    // Alpha walk edges are ordinary traversal corridors. Keep
                    // the cheap static obstacle probe available on them so a
                    // crate or other map prop that was not part of the graph
                    // sample cannot turn the route into a wall hug. Certified
                    // jump/fall contacts remain recipe-driven below.
                    !graph.IsOg2Alpha || (hasCurrentEdge && edgeKind == NavEdgeKind.Walk),
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
                    currentEdge.IsRuntimeResolved,
                    useAirborneSteeringDx,
                    currentEdge.ProbeMoveDirectionX,
                    currentEdge.JumpTriggerTick,
                    edgeTicks,
                    _currentEdgeJumpRequested,
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

        var hasOg2ContactEdge = hasCurrentEdge && currentEdge.IsOg2Contact;
        var walkEdgeInsideCompletionCorridor = hasCurrentEdge
            && edgeKind == NavEdgeKind.Walk
            && (!currentEdge.Completion.HasWindow
                || currentEdge.Completion.Contains(player.X, player.Y));
        var allowLocalObstacleProbing = !graph.IsOg2Alpha
            || allowRuntimeContactRecovery
            // Walk edges are ordinary corridors, even in the alpha graph.
            // A crate or live body can invalidate their straight-line sample
            // after graph generation, so retain the cheap stuck escape for
            // walk traversal. Certified jump/fall contacts stay recipe-only.
            || walkEdgeInsideCompletionCorridor;
        // A walk contact is still a walk corridor. It may be blocked by a
        // crate, a body, or a small collision discrepancy after graph build,
        // so it must retain the cheap local escape tools. Suppress those tools
        // for a healthy OG2 jump/fall/drop contact whose launch recipe is the
        // movement contract being replayed, but reopen the cheap escape path
        // when a grounded jump contact has actually stopped outside its launch
        // window. Dynamic bodies and small collision differences must not turn
        // a certified route into a permanent wall hug.
        var allowWalkContactRecovery = walkEdgeInsideCompletionCorridor;
        var allowStuckJumpContactRecovery = hasOg2ContactEdge
            && edgeKind == NavEdgeKind.Jump
            && _stuckEscapePhase > 0
            && player.IsGrounded
            && (!currentEdge.LaunchRecipe.HasRecipe
                || !currentEdge.LaunchRecipe.ContainsLaunchState(player));
        var allowOg2ContactRecovery = allowWalkContactRecovery
            || allowStuckJumpContactRecovery
            || allowRuntimeContactRecovery;
        if (_stuckEscapePhase > 0
            && (!hasOg2ContactEdge || allowOg2ContactRecovery))
        {
            ApplyStuckEscape(player, ref output);
        }
        var allowOg2BlockerHop = allowWalkContactRecovery
            // Walk contacts may be blocked by a static prop even while the
            // route's stagnation detector is still below its recovery phase.
            // Let the existing three-tick blocker probe handle that cheap
            // case immediately; the phase-based escape remains the fallback.
            ? true
            : allowStuckJumpContactRecovery || allowRuntimeContactRecovery;
        if (allowLocalObstacleProbing && (!hasOg2ContactEdge || allowOg2BlockerHop))
        {
            ApplyPressedBlockerHop(player, level, team, ref output);
        }

        if (!output.RequestRepath
            && graph.IsOg2Alpha
            && hasCurrentEdge
            && currentEdge.Kind == NavEdgeKind.Walk
            && path.CurrentIndex > 0)
        {
            // A walk corridor can be invalidated by a static prop or a live
            // body that was absent when the graph was built. If the cheap
            // jump probe cannot clear the obstruction, do not wait for the
            // 60-tick walk watchdog: hand the failed edge back to the graph
            // and let the alternate route/recovery owner take over.
            ApplyPressedWalkBlockerRepath(
                player,
                level,
                team,
                path,
                currentEdge,
                edgeTicks,
                walkEdgeInsideCompletionCorridor,
                ref output);
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
            && allowLocalObstacleProbing
            && ShouldUseFastJumpRetry(player, level, team, output.MoveDirection);
        var civilianPogoContact = player.ClassId == PlayerClass.Quote
            && player.IsCivviePogoActive
            && hasOg2ContactEdge
            && edgeKind == NavEdgeKind.Jump;
        if (!civilianPogoContact)
        {
            ApplyJumpPulse(ref output, fastJumpRetry);
        }
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

    private SteeringOutput UpdateTopDown(
        PlayerEntity player,
        NavGraph graph,
        NavPath? path,
        SimpleLevel level,
        PlayerTeam team)
    {
        var output = new SteeringOutput
        {
            State = SteeringState.Grounded,
            EdgeKind = NavEdgeKind.Walk,
        };

        if (path is null || path.IsComplete || !player.IsAlive)
        {
            ResetTopDownProgress();
            return output;
        }

        // Top-down routes are ordinary occupancy-grid walks. Advance through
        // any nodes already under the body before selecting the next target;
        // this prevents a stale attachment node from producing a neutral tick
        // or a left/right oscillation at every diagonal waypoint.
        while (!path.IsComplete)
        {
            var node = graph.GetNode(path.CurrentNode);
            var dx = node.X - player.X;
            var dy = node.Y - player.Y;
            if ((dx * dx) + (dy * dy) > WaypointReachRadius * WaypointReachRadius)
            {
                break;
            }

            path.Advance();
        }

        if (path.IsComplete)
        {
            ResetTopDownProgress();
            return output;
        }

        var target = graph.GetNode(path.CurrentNode);
        if (_topDownTrackedNode != path.CurrentNode)
        {
            _topDownTrackedNode = path.CurrentNode;
            _topDownCheckX = player.X;
            _topDownCheckY = player.Y;
            _topDownStuckTicks = 0;
        }
        else
        {
            var movedX = MathF.Abs(player.X - _topDownCheckX);
            var movedY = MathF.Abs(player.Y - _topDownCheckY);
            if (movedX >= TopDownStuckDistanceThreshold
                || movedY >= TopDownStuckDistanceThreshold)
            {
                _topDownCheckX = player.X;
                _topDownCheckY = player.Y;
                _topDownStuckTicks = 0;
            }
            else
            {
                _topDownStuckTicks += 1;
            }
        }

        if (_topDownStuckTicks >= TopDownStuckDetectionWindowTicks)
        {
            var fromNode = path.CurrentIndex > 0
                ? path.GetWaypoint(path.CurrentIndex - 1)
                : path.CurrentNode;
            var toNode = path.CurrentIndex > 0 && path.CurrentIndex < path.Count
                ? path.CurrentNode
                : path.Count > 1
                    ? path.GetWaypoint(1)
                    : path.CurrentNode;
            output.RequestRepath = true;
            output.FailedEdge = new SteeringFailedEdge(
                HasFailure: fromNode != toNode,
                FromNode: fromNode,
                ToNode: toNode,
                Kind: NavEdgeKind.Walk,
                EdgeTicks: _topDownStuckTicks,
                Reason: "top_down_stuck");
            ResetTopDownProgress();
            return output;
        }

        var desiredDirectionX = GetMoveDirection(target.X - player.X);
        var desiredDirectionY = GetMoveDirection(target.Y - player.Y);
        (output.MoveDirection, output.MoveDirectionY) = ResolveTopDownObstacleDetour(
            player,
            level,
            team,
            desiredDirectionX,
            desiredDirectionY);
        return output;
    }

    private (float DirectionX, float DirectionY) ResolveTopDownObstacleDetour(
        PlayerEntity player,
        SimpleLevel level,
        PlayerTeam team,
        float desiredDirectionX,
        float desiredDirectionY)
    {
        if (desiredDirectionX == 0f && desiredDirectionY == 0f)
        {
            _topDownDetourDirection = 0f;
            _topDownDetourTicks = 0;
            return (0f, 0f);
        }

        var canMoveX = desiredDirectionX == 0f
            || player.CanOccupy(
                level,
                team,
                player.X + (desiredDirectionX * TopDownObstacleProbeDistance),
                player.Y);
        var canMoveY = desiredDirectionY == 0f
            || player.CanOccupy(
                level,
                team,
                player.X,
                player.Y + (desiredDirectionY * TopDownObstacleProbeDistance));

        // A detour is a short movement transaction.  Honor it before the
        // requested-axis fast paths below; otherwise a one-tick change in the
        // waypoint delta clears the commitment and sends a bot back toward
        // the blocked corner.  This was the source of the exact two-cycle
        // left/right oscillation seen on graph-backed top-down routes.
        if (_topDownDetourTicks > 0
            && _topDownDetourDirection != 0f
            && CanMoveTopDownDetour(player, level, team))
        {
            _topDownDetourTicks -= 1;
            return _topDownDetourAxis == 1
                ? (_topDownDetourDirection, 0f)
                : (0f, _topDownDetourDirection);
        }

        if (_topDownDetourTicks > 0)
        {
            // The committed lane became blocked.  Do not hold stale input;
            // the selection below will probe both sides again.
            _topDownDetourDirection = 0f;
            _topDownDetourAxis = 0;
            _topDownDetourTicks = 0;
        }

        // The movement resolver already slides one axis at a time. Do the
        // same at the steering layer when a route waypoint is immediately
        // beside a wall; otherwise the bot keeps requesting the blocked
        // axis until the stuck watchdog repaths to the same bad approach.
        if (canMoveX && canMoveY && _topDownDetourTicks <= 0)
        {
            return (desiredDirectionX, desiredDirectionY);
        }

        if (canMoveX && desiredDirectionX != 0f && desiredDirectionY != 0f)
        {
            _topDownDetourTicks = 0;
            return (desiredDirectionX, 0f);
        }

        if (canMoveY && desiredDirectionY != 0f)
        {
            _topDownDetourTicks = 0;
            return (0f, desiredDirectionY);
        }

        if (canMoveX && desiredDirectionX != 0f)
        {
            _topDownDetourTicks = 0;
            return (desiredDirectionX, 0f);
        }

        // Both the requested axis and the direct diagonal are blocked. Take
        // a short, deterministic sidestep so the bot can get around the
        // obstacle while the graph controller continues pursuing the same
        // waypoint. Alternate the side on later encounters to avoid pinning
        // a bot against a corner.
        _topDownDetourAxis = desiredDirectionX != 0f && !canMoveX && desiredDirectionY == 0f
            ? 2
            : desiredDirectionY != 0f && !canMoveY && desiredDirectionX == 0f
                ? 1
                : 1;
        var detourDirection = _topDownDetourDirection == 0f
            ? 1f
            : -_topDownDetourDirection;
        if (desiredDirectionX != 0f && !canMoveX && desiredDirectionY == 0f)
        {
            detourDirection = _topDownDetourDirection == 0f ? 1f : -_topDownDetourDirection;
        }

        if (!CanMoveTopDownDetour(player, level, team, detourDirection))
        {
            detourDirection = -detourDirection;
        }

        if (CanMoveTopDownDetour(player, level, team, detourDirection))
        {
            // Keep the selected direction for the active commitment.  The
            // old code stored the opposite sign, so the first follow-up tick
            // immediately reversed the detour and reproduced the two-cycle.
            _topDownDetourDirection = detourDirection;
            _topDownDetourTicks = TopDownDetourCommitTicks - 1;
            return _topDownDetourAxis == 1
                ? (detourDirection, 0f)
                : (0f, detourDirection);
        }

        _topDownDetourDirection = 0f;
        _topDownDetourAxis = 0;
        _topDownDetourTicks = 0;
        return (0f, 0f);
    }

    private bool CanMoveTopDownDetour(PlayerEntity player, SimpleLevel level, PlayerTeam team) =>
        CanMoveTopDownDetour(player, level, team, _topDownDetourDirection);

    private bool CanMoveTopDownDetour(
        PlayerEntity player,
        SimpleLevel level,
        PlayerTeam team,
        float direction)
    {
        if (direction == 0f || _topDownDetourAxis == 0)
        {
            return false;
        }

        return _topDownDetourAxis == 1
            ? player.CanOccupy(
                level,
                team,
                player.X + (direction * TopDownObstacleProbeDistance),
                player.Y)
            : player.CanOccupy(
                level,
                team,
                player.X,
                player.Y + (direction * TopDownObstacleProbeDistance));
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
        _walkProgressFromNode = -1;
        _walkProgressToNode = -1;
        _walkProgressCheckX = 0f;
        _walkProgressCheckY = 0f;
        _walkProgressTicks = 0;
        _walkProgressStagnantWindows = 0;
        ResetTopDownProgress();
    }

    private void ResetTopDownProgress()
    {
        _topDownTrackedNode = -1;
        _topDownCheckX = 0f;
        _topDownCheckY = 0f;
        _topDownStuckTicks = 0;
        _topDownDetourDirection = 0f;
        _topDownDetourAxis = 0;
        _topDownDetourTicks = 0;
    }

    private bool UpdateGroundedWalkProgress(
        PlayerEntity player,
        NavPath path,
        NavEdge edge,
        int edgeTicks)
    {
        if (edge.Kind != NavEdgeKind.Walk
            || !player.IsGrounded
            || path.CurrentIndex <= 0)
        {
            _walkProgressFromNode = -1;
            _walkProgressToNode = -1;
            _walkProgressTicks = 0;
            _walkProgressStagnantWindows = 0;
            return false;
        }

        var fromNode = path.GetWaypoint(path.CurrentIndex - 1);
        var toNode = path.CurrentNode;
        if (fromNode != _walkProgressFromNode || toNode != _walkProgressToNode)
        {
            _walkProgressFromNode = fromNode;
            _walkProgressToNode = toNode;
            _walkProgressCheckX = player.X;
            _walkProgressCheckY = player.Y;
            _walkProgressTicks = 0;
            _walkProgressStagnantWindows = 0;
            return false;
        }

        _walkProgressTicks += 1;
        if (_walkProgressTicks < StuckDetectionWindow)
        {
            return false;
        }

        var movedX = MathF.Abs(player.X - _walkProgressCheckX);
        var movedY = MathF.Abs(player.Y - _walkProgressCheckY);
        if (movedX < StuckDistanceThreshold && movedY < StuckDistanceThreshold)
        {
            _walkProgressStagnantWindows += 1;
        }
        else
        {
            _walkProgressStagnantWindows = 0;
        }

        _walkProgressCheckX = player.X;
        _walkProgressCheckY = player.Y;
        _walkProgressTicks = 0;
        return edgeTicks >= StuckDetectionWindow * GroundedWalkProgressRepathWindows
            && _walkProgressStagnantWindows >= GroundedWalkProgressRepathWindows;
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

    private bool ShouldAdvanceWaypoint(
        PlayerEntity player,
        NavGraph graph,
        NavPath path,
        SimpleLevel level,
        bool currentEdgeCompletionSatisfied)
    {
        var targetNode = graph.GetNode(path.CurrentNode);
        var dx = targetNode.X - player.X;
        var dy = targetNode.Y - player.Y;
        var distSq = (dx * dx) + (dy * dy);
        var civviePogoContactCompleted = path.TryGetIncomingEdge(path.CurrentIndex, out var incomingContactEdge)
            && IsCivviePogoContactCompletionSatisfied(player, incomingContactEdge);
        var civviePogoTraversalCompleted = !civviePogoContactCompleted
            && path.TryGetIncomingEdge(path.CurrentIndex, out incomingContactEdge)
            && IsCivviePogoWalkCompletionSatisfied(player, incomingContactEdge, targetNode);
        if (civviePogoContactCompleted || civviePogoTraversalCompleted)
        {
            return true;
        }
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
            if (path.CurrentIndex == 0
                && player.ClassId == PlayerClass.Quote
                && player.IsCivviePogoActive)
            {
                // A live civilian pogo bounce does not expose the ordinary
                // grounded attachment state. Once the replacement route's
                // first surface is actually under the body, hand off to its
                // next edge instead of waiting forever for IsGrounded.
                return true;
            }

            if (!player.IsGrounded
                && path.TryGetIncomingEdge(path.CurrentIndex, out var incomingEdge)
                && incomingEdge.RequiresGroundedContinuation)
            {
                // A resolved OG2 contact can enter its certified landing
                // window one physics update before collision state refreshes
                // IsGrounded. If the measured completion is already true,
                // hand the route to an ordinary corridor immediately instead
                // of making the bot fall through the landing and replay the
                // same jump forever. The next-edge grounded guard below still
                // blocks a follow-up certified contact until the body lands.
                if (!civviePogoContactCompleted
                    && (!incomingEdge.IsRuntimeResolved
                    || !currentEdgeCompletionSatisfied)
                    )
                {
                    return false;
                }
            }

            if (!player.IsGrounded
                && !civviePogoContactCompleted
                && NextEdgeRequiresGroundedContact(path))
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
            && !ShouldDeferContactHandoff(
                player,
                graph,
                path,
                edge,
                distSq,
                currentEdgeCompletionSatisfied)
            && (player.IsGrounded
                ? currentEdgeCompletionSatisfied
                    || IsNearGroundedContinuationCompletion(player, edge, level)
                : player.ClassId != PlayerClass.Heavy
                    && (civviePogoContactCompleted
                        || IsAirborneCompletionContinuation(player, graph, edge)
                        || IsNearGroundedContinuationCompletion(player, edge, level)));
    }

    private static bool ShouldDeferContactHandoff(
        PlayerEntity player,
        NavGraph graph,
        NavPath path,
        NavEdge edge,
        float distanceSquared,
        bool currentEdgeCompletionSatisfied)
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

        // A reverse-direction contact normally waits for the exact landing
        // node so the next recipe does not inherit stale momentum. The OG2
        // completion contract is the stronger fact, however: a grounded bot
        // on the certified surface/window has completed the edge even when
        // collision resolution leaves its live Y offset from the nominal
        // node. Do not deadlock that valid landing behind the node-radius
        // handoff guard.
        if (player.IsGrounded && currentEdgeCompletionSatisfied)
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

    private static bool IsCivviePogoContactCompletionSatisfied(PlayerEntity player, NavEdge edge) =>
        player.ClassId == PlayerClass.Quote
        && player.IsCivviePogoActive
        && player.IsCivviePogoSuperJumpAirPhaseActive
        && edge.IsOg2Contact
        && edge.Kind == NavEdgeKind.Jump
        && edge.Completion.Contains(player.X, player.Y);

    private static bool IsCivviePogoWalkCompletionSatisfied(
        PlayerEntity player,
        NavEdge edge,
        NavNode targetNode) =>
        player.ClassId == PlayerClass.Quote
        && player.IsCivviePogoActive
        && edge.Kind == NavEdgeKind.Walk
        && MathF.Abs(player.X - targetNode.X) <= WaypointReachRadius * 2f
        && MathF.Abs(player.Y - targetNode.Y) <= WaypointReachRadius * 2f;

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
        && !edge.IsRuntimeResolved
        && !edge.RuntimeResolutionExhausted;

    private static bool ShouldAwaitRuntimeContactResolution(
        PlayerEntity player,
        NavGraph graph,
        NavEdge edge) =>
        graph.IsOg2Alpha
        && edge.IsOg2Contact
        && edge.Kind == NavEdgeKind.Jump
        && !edge.IsRuntimeResolved
        && !edge.RuntimeResolutionExhausted
        && player.IsGrounded;

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
            _currentEdgeCompletionSatisfiedGrounded = false;
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
            _currentEdgeCompletionSatisfiedGrounded = false;
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

    private void UpdateCurrentEdgePhase(PlayerEntity player, NavEdge edge, bool completionSatisfied)
    {
        if (!player.IsGrounded)
        {
            _currentEdgeWasAirborne = true;
            return;
        }

        if (completionSatisfied)
        {
            _currentEdgeCompletionSatisfiedGrounded = true;
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

    private void UpdateCurrentEdgeExecutionPhase(
        PlayerEntity player,
        NavGraph graph,
        NavPath path,
        NavEdge edge,
        bool completionSatisfied)
    {
        if (_edgePhase != EdgeExecutionPhase.None)
        {
            _edgePhaseTicks += 1;
            if (ShouldExitEdgeExecutionPhase(player, graph, edge, completionSatisfied))
            {
                _edgePhase = EdgeExecutionPhase.None;
                _edgePhaseTicks = 0;
            }

            return;
        }

        if (ShouldEnterLandedBelowCompletionPhase(player, graph, path, edge, completionSatisfied))
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
        bool currentEdgeCompletionSatisfied,
        out bool suppressJumpUntilLaunch)
    {
        suppressJumpUntilLaunch = false;
        if (IsExperimentalFallDropdownSteeringEnabled(graph)
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

        if (edge.Kind == NavEdgeKind.Walk)
        {
            if (graph.IsOg2Alpha && edge.ProbeMoveDirectionX != 0f)
            {
                if (path.CurrentIndex > 0)
                {
                    var fromNode = graph.GetNode(path.GetWaypoint(path.CurrentIndex - 1));
                    var targetNode = graph.GetNode(path.CurrentNode);
                    if (MathF.Abs(targetNode.X - fromNode.X) <= HorizontalDeadZone)
                    {
                        // Surface relays can differ by a fraction of a pixel
                        // after class-neutral graph quantization. Their
                        // measured direction is not a traversal contract: it
                        // can point backward while the live body is still
                        // approaching the relay from the other side. Follow
                        // the live waypoint delta so a vertical handoff does
                        // not turn into a long reverse walk off the platform.
                        return waypointDx;
                    }

                    var travelDirection = MathF.Sign(edge.ProbeMoveDirectionX);
                    var passedTargetDistance = (player.X - targetNode.X) * travelDirection;
                    // A small waypoint overshoot is common when the previous
                    // tick's committed walk input carries the body past a
                    // node. Keep the measured traversal direction through
                    // that handoff; only a materially larger miss should
                    // reverse toward the waypoint.
                    if (passedTargetDistance > JumpTriggerDistance)
                    {
                        // The measured direction is authoritative only while
                        // the live body is still approaching this waypoint.
                        // Once it has crossed the target by a full waypoint
                        // radius, continuing to force that direction drives
                        // it off the support surface and creates the visible
                        // forward/backward route loop.
                        return waypointDx;
                    }
                }

                // Walk edges are directed graph contracts. Following the
                // measured direction prevents a small waypoint overshoot from
                // flipping the bot's input every think and producing the
                // visible forward/backward oscillation.
                return MathF.Sign(edge.ProbeMoveDirectionX)
                    * MathF.Max(HorizontalDeadZone + 1f, MathF.Abs(waypointDx));
            }

            if (!edge.Completion.HasWindow)
            {
                return waypointDx;
            }

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

        if (!edge.Completion.HasWindow)
        {
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
            && edge.LaunchRecipe.StartGrounded
            && !edge.IsRuntimeResolved
            && !player.IsGrounded)
        {
            // A preceding contact can hand off one fixed update before the
            // live body is marked grounded. Do not continue toward the next
            // waypoint while that handoff is airborne: residual momentum can
            // carry the bot off the certified source surface before the
            // runtime resolver gets a grounded sample. Brake/recover toward
            // the measured launch window instead.
            suppressJumpUntilLaunch = true;
            var launchCenterX = (edge.LaunchRecipe.LaunchMinX + edge.LaunchRecipe.LaunchMaxX) * 0.5f;
            return launchCenterX - player.X;
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
                var runtimeAtMeasuredLaunchState =
                    edge.IsRuntimeResolved
                    && MathF.Abs(launchDx) <= RuntimeLaunchCenterTolerance
                    && MathF.Abs(player.Y - ((edge.LaunchRecipe.LaunchMinY + edge.LaunchRecipe.LaunchMaxY) * 0.5f))
                        <= RuntimeLaunchCenterTolerance
                    && IsInLaunchSpeedWindow(player, edge.LaunchRecipe);
                if (edge.IsRuntimeResolved)
                {
                    // The runtime probe is the executable schedule for this
                    // edge. It may include a bounded counter-steer phase
                    // before the jump when the preceding contact handed off
                    // residual momentum. Replaying that phase is necessary
                    // for composed contacts; steering directly toward the
                    // launch band can otherwise overshoot it before the
                    // measured jump tick.
                    var jumpTick = Math.Max(0, edge.LaunchRecipe.LaunchTick);
                    var preLaunchBrakeTicks = Math.Clamp(
                        edge.LaunchRecipe.PreLaunchBrakeTicks,
                        0,
                        jumpTick);
                    if (preLaunchBrakeTicks > 0
                        && _currentEdgeTicks >= jumpTick - preLaunchBrakeTicks
                        && _currentEdgeTicks < jumpTick)
                    {
                        suppressJumpUntilLaunch = true;
                        return -launchDirection * MathF.Max(JumpTriggerDistance, MathF.Abs(launchDx));
                    }

                    // The runtime probe records a launch *window*, not merely
                    // a direction. If the live handoff enters past that
                    // window, multiplying the absolute distance by the route
                    // direction drives the bot farther away from the only
                    // state in which the measured jump can fire. This was
                    // especially visible on Corinth's red stair edge: fast
                    // classes crossed the window during the handoff and then
                    // continued forward indefinitely. Steer toward the
                    // measured band until the state is ready; the direction
                    // naturally becomes the route direction before the band
                    // and reverses only when the bot has overshot it.
                    if (!runtimeAtMeasuredLaunchState)
                    {
                        var launchSteeringDirection = MathF.Sign(launchDx);
                        if (launchSteeringDirection != 0f)
                        {
                            return launchSteeringDirection
                                * MathF.Max(JumpTriggerDistance, MathF.Abs(launchDx));
                        }

                        if (!IsInLaunchSpeedWindow(player, edge.LaunchRecipe))
                        {
                            var speedDirection = player.HorizontalSpeed > edge.LaunchRecipe.LaunchMaxHorizontalSpeed
                                ? -1f
                                : player.HorizontalSpeed < edge.LaunchRecipe.LaunchMinHorizontalSpeed
                                    ? 1f
                                    : launchDirection;
                            return speedDirection * JumpTriggerDistance;
                        }
                    }

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
            if (ShouldCounterSteerForNextContact(player, graph, path, edge, currentEdgeCompletionSatisfied))
            {
                var nextDirection = MathF.Sign(nextEdge.LaunchRecipe.ExpectedMoveDirectionX);
                return -nextDirection * MathF.Max(JumpTriggerDistance, MathF.Abs(waypointDx));
            }

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
        NavEdge edge,
        bool completionSatisfied)
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

        if (completionSatisfied)
        {
            return false;
        }

        return edge.RequiresGroundedContinuation
            || edge.JumpTriggerTick > 0;
    }

    private bool ShouldExitEdgeExecutionPhase(
        PlayerEntity player,
        NavGraph graph,
        NavEdge edge,
        bool completionSatisfied)
    {
        if (completionSatisfied)
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
        bool completionSatisfied,
        ref SteeringOutput output)
    {
        if (path.CurrentIndex <= 0
            || completionSatisfied)
        {
            return;
        }

        var fromNode = path.GetWaypoint(path.CurrentIndex - 1);
        var toNode = path.CurrentNode;
        var targetNode = graph.GetNode(toNode);
        if (edge.RuntimeResolutionExhausted
            && !edge.LaunchRecipe.HasRecipe)
        {
            // A runtime proof can exhaust its small search budget even when
            // the canonical OG2 contact remains executable from the current
            // support surface. Contact preparation marks that state so it
            // stops spending probe time; keep the static recipe available as
            // the bounded fallback instead of converting a probe miss into an
            // immediate route failure. Edges without a recipe have no safe
            // fallback and still fail immediately.
            output.RequestRepath = true;
            output.FailedEdge = new SteeringFailedEdge(
                HasFailure: true,
                FromNode: fromNode,
                ToNode: toNode,
                Kind: edge.Kind,
                EdgeTicks: edgeTicks,
                Reason: "runtime_contact_unavailable");
            return;
        }

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

        if (ShouldFastFailGroundedWalkBelowCompletion(player, edge, edgeTicks, graph.IsOg2Alpha))
        {
            output.RequestRepath = true;
            output.FailedEdge = new SteeringFailedEdge(
                HasFailure: true,
                FromNode: fromNode,
                ToNode: toNode,
                Kind: edge.Kind,
                EdgeTicks: edgeTicks,
                Reason: "walk_below_completion");
            return;
        }

        // A missed OG2 jump can leave the body grounded on a lower support
        // while the path still points at the upper jump completion window.
        // The normal jump watchdog is intentionally generous for launch
        // setup, but once the body has either been airborne or has had a
        // bounded launch grace period, continuing to steer at the stale
        // upper edge produces the visible back/forth stall under ledges and
        // points. Reattach from the live support instead.
        if (ShouldFastFailGroundedJumpBelowCompletion(player, edge, edgeTicks, graph.IsOg2Alpha)
            && (_currentEdgeWasAirborne || edgeTicks >= GroundedJumpBelowCompletionGraceTicks))
        {
            output.RequestRepath = true;
            output.FailedEdge = new SteeringFailedEdge(
                HasFailure: true,
                FromNode: fromNode,
                ToNode: toNode,
                Kind: edge.Kind,
                EdgeTicks: edgeTicks,
                Reason: "jump_below_completion");
            return;
        }

        if (ShouldFastFailLandedBelowCompletion(player, edge, edgeTicks, mode, graph.IsOg2Alpha))
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

        // A live prop or player body can block an ordinary alpha walk
        // corridor without making the static CanOccupy probe fail. Once the
        // shared stagnation detector has observed two consecutive windows of
        // no movement, keeping the same walk edge only produces a multi-second
        // wall-hug/freeze. Return the edge to graph recovery while the
        // obstruction is still local. Moving corridors never enter this path.
        if (graph.IsOg2Alpha
            && edge.Kind == NavEdgeKind.Walk
            && player.IsGrounded
            && _stuckEscapePhase >= 2
            && edgeTicks >= StuckDetectionWindow * 2)
        {
            output.RequestRepath = true;
            output.FailedEdge = new SteeringFailedEdge(
                HasFailure: true,
                FromNode: fromNode,
                ToNode: toNode,
                Kind: edge.Kind,
                EdgeTicks: edgeTicks,
                Reason: "walk_stuck");
            return;
        }

        // A certified jump recipe is only useful while the body is actually
        // making progress toward its launch/completion surface. If a live
        // prop, body, or stale landing state leaves a grounded bot issuing
        // the same jump edge with no displacement, the recipe itself must
        // not keep the edge alive indefinitely. Repath after two complete
        // stagnation windows; this is deliberately based on measured body
        // movement rather than class, team, target, or combat state.
        if (graph.IsOg2Alpha
            && edge.Kind == NavEdgeKind.Jump
            && player.IsGrounded
            && _stuckEscapePhase >= 2
            && edgeTicks >= StuckDetectionWindow * 2)
        {
            output.RequestRepath = true;
            output.FailedEdge = new SteeringFailedEdge(
                HasFailure: true,
                FromNode: fromNode,
                ToNode: toNode,
                Kind: edge.Kind,
                EdgeTicks: edgeTicks,
                Reason: "jump_stuck");
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

        if (edge.Kind == NavEdgeKind.Walk
            && player.ClassId == PlayerClass.Quote
            && player.IsCivviePogoActive
            && targetDistance <= WaypointReachRadius * 2f
            && MathF.Abs(player.Y - targetNode.Y) <= WaypointReachRadius * 2f)
        {
            // A pogo bounce can pass the measured walk waypoint without ever
            // exposing IsGrounded. Give the coordinate handoff above a
            // bounded extra window instead of expiring the corridor while
            // the civilian is still crossing its target surface.
            return;
        }

        if (edge.Kind == NavEdgeKind.Jump
            && edge.IsOg2Contact
            && player.ClassId == PlayerClass.Quote
            && player.IsCivviePogoActive
            && _currentEdgeWasAirborne
            && edgeTicks >= 20
            && !player.IsCivviePogoSuperJumpAirPhaseActive
            && player.VerticalSpeed < 0f)
        {
            // A civilian can clear a normal jump contact and land on a
            // different valid surface because the pogo arc is taller than
            // the canonical jump proof. Reattach immediately from that live
            // bounce instead of waiting for the stale target edge to expire.
            output.RequestRepath = true;
            output.FailedEdge = new SteeringFailedEdge(
                HasFailure: true,
                FromNode: fromNode,
                ToNode: toNode,
                Kind: edge.Kind,
                EdgeTicks: edgeTicks,
                Reason: "pogo_landed_off_route");
            return;
        }

        var maxTicks = ResolveMaximumEdgeTicks(edge);
        if (edge.Kind == NavEdgeKind.Jump
            && player.ClassId == PlayerClass.Quote
            && player.IsCivviePogoActive)
        {
            // Pogo's super-bounce has a longer flight than the normal jump
            // tape used to build this shared contact. Allow the bounce to
            // reach its landing surface before declaring the edge stale.
            maxTicks += 64;
        }
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

    private static bool IsExperimentalFallDropdownSteeringEnabled(NavGraph graph) =>
        graph.IsOg2Alpha
        || Environment.GetEnvironmentVariable("BOTBRAIN_EXPERIMENTAL_FALL_DROPDOWN_STEERING") is "1" or "true" or "TRUE";

    private bool ShouldFastFailLandedBelowCompletion(
        PlayerEntity player,
        NavEdge edge,
        int edgeTicks,
        GameModeKind mode,
        bool alphaNavigation)
    {
        var landedBelowCompletion = _currentEdgeLandedAfterAirborne
            && player.IsGrounded
            && !edge.Completion.Contains(player.X, player.Y)
            && player.Y > edge.Completion.MaxY + LandedBelowCompletionVerticalSlack;
        if (alphaNavigation
            && edge.IsOg2Contact
            && edge.Kind == NavEdgeKind.Jump
            && edge.IsRuntimeResolved
            // Runtime resolution can replace an unresolved contact on the
            // same grounded update in which the previous attempt landed
            // below the completion band. Give the newly measured schedule
            // one steering update to fire before treating that stale landing
            // flag as a fresh failure; otherwise a valid probe is discarded
            // immediately and the route repaths in a loop.
            && edgeTicks > 1
            && player.IsGrounded
            && !edge.Completion.Contains(player.X, player.Y)
            && player.Y > edge.Completion.MaxY + RuntimeResolvedFarBelowCompletionVerticalSlack)
        {
            // A resolved contact may legitimately cross intermediate stair
            // surfaces below its final completion window. Only fail early
            // when the live body is materially farther below the window than
            // any intermediate surface in the certified contact; this is a
            // knock-off/recovery state, not a launch-contract failure.
            return true;
        }

        if (alphaNavigation
            && edge.IsOg2Contact
            && edge.Kind == NavEdgeKind.Jump
            && landedBelowCompletion
            && !edge.IsRuntimeResolved
            && edgeTicks >= Math.Max(
                AlphaLandedBelowCompletionRecoveryTicks,
                edge.JumpTriggerTick + AlphaLandedBelowCompletionRecoveryTicks))
        {
            return true;
        }

        return _edgePhase == EdgeExecutionPhase.None
            && (mode == GameModeKind.CaptureTheFlag || alphaNavigation)
            && edge.Kind == NavEdgeKind.Jump
            && edge.Completion.HasWindow
            && landedBelowCompletion
            && _currentEdgeJumpRequested
            && player.IsGrounded
            && player.ClassId is not PlayerClass.Soldier and not PlayerClass.Heavy
            && edgeTicks >= Math.Max(0, ResolveMaximumEdgeTicks(edge) - LandedBelowCompletionFastFailSlackTicks);
    }

    private bool ShouldRequestEarlyContactRecovery(PlayerEntity player, NavEdge edge, int edgeTicks)
    {
        if (_stuckEscapePhase < 2
            || edge.Kind != NavEdgeKind.Jump
            || !edge.IsOg2Contact
            || !edge.LaunchRecipe.HasRecipe
            || edgeTicks < 30
            || !player.IsGrounded)
        {
            return false;
        }

        // A grounded bot sitting inside the measured launch state may simply
        // be waiting for its scheduled jump tick. Only recover early when it
        // is both stuck and outside the launch/completion contracts.
        return !edge.LaunchRecipe.ContainsLaunchState(player)
            && !edge.Completion.Contains(player.X, player.Y);
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

    private static bool ShouldFastFailGroundedWalkBelowCompletion(
        PlayerEntity player,
        NavEdge edge,
        int edgeTicks,
        bool alphaNavigation)
    {
        // A missed OG2 jump can hand the path executor a grounded body on a
        // lower surface while the next path entry is already a Walk relay on
        // the upper surface. That relay's horizontal completion band is
        // valid geometry, but it is not reachable from the body's actual
        // landing. Keeping it alive makes the bot counter-steer against the
        // stale relay for several seconds. Reattach from the live support
        // after a short bounded grace period instead.
        return alphaNavigation
            && edge.Kind == NavEdgeKind.Walk
            && edge.Completion.HasWindow
            && player.IsGrounded
            && player.Y > edge.Completion.MaxY + LandedBelowCompletionVerticalSlack
            && edgeTicks >= 12;
    }

    private const int GroundedJumpBelowCompletionGraceTicks = 30;

    private static bool ShouldFastFailGroundedJumpBelowCompletion(
        PlayerEntity player,
        NavEdge edge,
        int edgeTicks,
        bool alphaNavigation)
    {
        if (!alphaNavigation
            || edge.Kind != NavEdgeKind.Jump
            || !edge.Completion.HasWindow
            || !player.IsGrounded
            || player.Y <= edge.Completion.MaxY + LandedBelowCompletionVerticalSlack
            || edgeTicks < 12)
        {
            return false;
        }

        if (edge.LaunchRecipe.HasRecipe
            && edge.LaunchRecipe.StartGrounded
            && player.IsGrounded)
        {
            // A grounded bot can be vertically below the eventual landing
            // surface while it is still making the certified horizontal
            // run-up. That is normal for long OG2 stair relays; treating it
            // as a stale lower landing before the launch corridor is reached
            // repaths the edge before its measured jump tick and creates the
            // visible back/forth loop. Only the post-launch/near-launch case
            // belongs to this fast-fail lane.
            var launchDirection = MathF.Sign(edge.LaunchRecipe.ExpectedMoveDirectionX);
            if (launchDirection > 0f
                && player.X < edge.LaunchRecipe.LaunchMinX - JumpTriggerDistance)
            {
                return false;
            }

            if (launchDirection < 0f
                && player.X > edge.LaunchRecipe.LaunchMaxX + JumpTriggerDistance)
            {
                return false;
            }
        }

        return true;
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
            // is not a valid watchdog. A runtime-resolved contact may also
            // legitimately touch an intermediate stair surface before the
            // probe's completion tick, so give that measured horizon a small
            // bounded handoff margin instead of applying the 72-tick legacy
            // watchdog to it.
            if (edge.IsRuntimeResolved && edge.ProbeTicks > 0)
            {
                return Math.Clamp(
                    Math.Max(MinimumRuntimeResolvedEdgeTicks, edge.ProbeTicks + RuntimeResolvedEdgeSlackTicks),
                    MinimumRuntimeResolvedEdgeTicks,
                    MaximumRuntimeResolvedEdgeTicks);
            }

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
        bool allowLocalObstacleProbing,
        NavEdgeLaunchRecipe launchRecipe,
        ref SteeringOutput output)
    {
        var moveDir = GetMoveDirection(dx);

        switch (edgeKind)
        {
            case NavEdgeKind.Jump:
                output.MoveDirection = moveDir;
                if (player.ClassId == PlayerClass.Quote
                    && player.IsCivviePogoActive
                    && isOg2Contact)
                {
                    // The civilian pogo owns vertical traversal; its Up
                    // input is consumed by the landing-bounce state machine,
                    // not by TryJumpIfPossible's normal jump path.
                    output.Jump = true;
                    break;
                }

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
                var obstacleJumpReady = allowLocalObstacleProbing
                    && (IsApproachingCliff(player, level, team, moveDir)
                        || WouldHitWall(player, level, team, moveDir));
                if (jumpTimingSatisfied
                    && (isRuntimeResolved || recipeReady || runupSatisfied)
                    && (isRuntimeResolved || recipeReady
                        || (!suppressJumpUntilLaunch
                            && (MathF.Abs(dx) <= JumpTriggerDistance
                                || dy <= TargetAboveJumpThreshold
                                || dy >= -TargetAboveJumpThreshold
                                || obstacleJumpReady))))
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
                var jumpableObstacleAhead = allowLocalObstacleProbing
                    && !isOg2Contact
                    && IsJumpableObstacleAhead(player, level, team, moveDir);
                if (assistSemanticWalkClimb && edgeTicks >= 0)
                {
                    output.Jump = true;
                }

                if (allowLocalObstacleProbing
                    && !isOg2Contact
                    && (jumpableObstacleAhead
                    || (WouldHitWall(player, level, team, moveDir) && dy <= TargetAboveJumpThreshold))
                    )
                {
                    output.Jump = true;
                }

                if (allowLocalObstacleProbing
                    && !isOg2Contact
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

    private bool ShouldCounterSteerForNextContact(
        PlayerEntity player,
        NavGraph graph,
        NavPath path,
        NavEdge edge,
        bool currentEdgeCompletionSatisfied)
    {
        if (!edge.IsRuntimeResolved
            || !edge.IsOg2Contact
            || edge.Kind != NavEdgeKind.Jump
            || player.IsGrounded
            || !_currentEdgeLandedAfterAirborne
            || path.CurrentIndex + 1 >= path.Count
            || !path.TryGetIncomingEdge(path.CurrentIndex + 1, out var nextEdge)
            || !nextEdge.IsOg2Contact
            || nextEdge.Kind != NavEdgeKind.Jump
            || !nextEdge.LaunchRecipe.HasRecipe
            || edge.ProbeMoveDirectionX == 0f
            || !_currentEdgeCompletionSatisfiedGrounded
            || !currentEdgeCompletionSatisfied
            // The observed non-composable handoff is the leftward OG2 stair
            // chain: its landing surface ends immediately behind the launch
            // band, so inherited leftward momentum carries the bot off before
            // the next grounded resolver can sample it. Rightward chains have
            // enough platform runway and must retain their certified landing
            // control; do not apply this recovery to them speculatively.
            || edge.ProbeMoveDirectionX > 0f
            || nextEdge.LaunchRecipe.ExpectedMoveDirectionX == 0f
            || MathF.Sign(nextEdge.LaunchRecipe.ExpectedMoveDirectionX)
                != MathF.Sign(edge.ProbeMoveDirectionX)
            // Do not counter-steer while the current contact is still
            // outside its landing band. The earlier wider handoff margin
            // could reverse a leftward jump before it reached the target
            // surface, especially on Heavy's longer central stair relay.
            || player.X < edge.Completion.MinX - 8f
            || player.X > edge.Completion.MaxX + 8f
            || player.Y < edge.Completion.MinY - 48f
            || player.Y > edge.Completion.MaxY + 24f)
        {
            return false;
        }

        var nextLaunchCenterX =
            (nextEdge.LaunchRecipe.LaunchMinX + nextEdge.LaunchRecipe.LaunchMaxX) * 0.5f;
        if (MathF.Abs(player.X - nextLaunchCenterX) > 48f)
        {
            return false;
        }

        var nextDirection = MathF.Sign(nextEdge.LaunchRecipe.ExpectedMoveDirectionX);
        var speedInNextDirection = player.HorizontalSpeed * nextDirection;
        var maximumLaunchSpeed = MathF.Max(
            MathF.Abs(nextEdge.LaunchRecipe.LaunchMinHorizontalSpeed),
            MathF.Abs(nextEdge.LaunchRecipe.LaunchMaxHorizontalSpeed));
        return speedInNextDirection > maximumLaunchSpeed;
    }

    private static void SteerAirborne(
        PlayerEntity player,
        NavEdgeKind edgeKind,
        NavEdgeCompletion completion,
        float dx,
        float dy,
        bool isOg2Contact,
        bool isRuntimeResolved,
        bool useSteeringDx,
        float probeMoveDirectionX,
        int jumpTriggerTick,
        int edgeTicks,
        bool currentEdgeJumpRequested,
        NavEdgeLaunchRecipe launchRecipe,
        ref SteeringOutput output)
    {
        output.MoveDirection = useSteeringDx
            ? GetMoveDirection(dx)
            : isOg2Contact && probeMoveDirectionX != 0f
            ? MathF.Sign(probeMoveDirectionX)
            : GetMoveDirection(dx);

        if (isOg2Contact
            && edgeKind == NavEdgeKind.Jump
            && launchRecipe.HasRecipe
            && launchRecipe.StartGrounded
            && !isRuntimeResolved)
        {
            // Before a grounded-start contact has been runtime-resolved, an
            // airborne handoff is a recovery phase rather than the jump
            // itself. Once the jump pulse has actually been requested,
            // however, this is the contact's real flight phase and the
            // measured direction must remain authoritative; waypoint-delta
            // steering can reverse after the body crosses the nominal target
            // X and create a visible route oscillation.
            output.MoveDirection = currentEdgeJumpRequested
                ? completion.HasWindow
                    ? GetMoveDirection(
                        ((completion.MinX + completion.MaxX) * 0.5f) - player.X)
                    : GetMoveDirection(dx)
                : GetMoveDirection(dx);
        }

        // Runtime contact proof may select a class-specific horizontal control
        // schedule. Match the probe after the launch input has been consumed;
        // otherwise a faster class can invalidate the successful landing by
        // continuing to hold the approach direction through the air.
        if (isRuntimeResolved
            && isOg2Contact
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

        // Quote's civilian pogo replaces the normal jump impulse. While a
        // certified jump contact is in flight, keep the super-jump input held
        // so the next pogo landing consumes the measured contact as a
        // traversal bounce. A civilian can enter a jump edge already
        // airborne (including from the spawn bounce); waiting for the normal
        // grounded launch recipe in that state leaves it drifting into the
        // gap with no jump request at all.
        if (player.ClassId == PlayerClass.Quote
            && player.IsCivviePogoActive
            && isOg2Contact
            && edgeKind == NavEdgeKind.Jump)
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

    private void ApplyPressedWalkBlockerRepath(
        PlayerEntity player,
        SimpleLevel level,
        PlayerTeam team,
        NavPath path,
        NavEdge currentEdge,
        int edgeTicks,
        bool allowJumpableObstacleEscape,
        ref SteeringOutput output)
    {
        if (!player.IsGrounded
            || output.MoveDirection == 0f
            || player.CanOccupy(level, team, player.X + (output.MoveDirection * 4f), player.Y)
            || (allowJumpableObstacleEscape
                && IsJumpableObstacleAhead(player, level, team, output.MoveDirection)))
        {
            return;
        }

        _pressedBlockerTicks += 1;
        if (_pressedBlockerTicks < PressedBlockerRepathTicks)
        {
            return;
        }

        output.RequestRepath = true;
        output.FailedEdge = new SteeringFailedEdge(
            HasFailure: true,
            FromNode: path.GetWaypoint(path.CurrentIndex - 1),
            ToNode: path.CurrentNode,
            Kind: currentEdge.Kind,
            EdgeTicks: edgeTicks,
            Reason: "walk_blocked");
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

    public float MoveDirectionY { get; set; }

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
