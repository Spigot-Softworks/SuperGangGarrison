using OpenGarrison.Core;
using OpenGarrison.MLBot.Contracts;

namespace OpenGarrison.MLBot;

public sealed class MLBotObservationRuntimeState
{
    public bool HasPreviousSample { get; set; }

    public float PreviousX { get; set; }

    public float PreviousY { get; set; }

    public float PreviousVelocityX { get; set; }

    public float PreviousVelocityY { get; set; }

    public float PreviousFacingDirectionX { get; set; }

    public bool PreviousIsGrounded { get; set; }

    public int PreviousRemainingAirJumps { get; set; }

    public float PreviousObjectiveDistance { get; set; }

    public float PreviousObjectiveDistanceDelta { get; set; }

    public float PreviousNavigationDistance { get; set; }

    public float PreviousNavigationDistanceDelta { get; set; }

    public int PreviousControlPointObjectiveIndex { get; set; }

    public float PreviousControlPointCaptureProgress { get; set; }

    public bool PreviousOnControlPointObjective { get; set; }

    public float TimeOnControlPointTicks { get; set; }

    public float TimeSinceLeftControlPointTicks { get; set; }

    public string? ScoreRouteKey { get; set; }

    public MLBotTaskPhase ScoreRoutePhase { get; set; }

    public int ScoreRouteIndex { get; set; }

    public string? TraversalSegmentKey { get; set; }

    public float TraversalSegmentTicks { get; set; }

    public Dictionary<string, int> TraversalAttemptCountsByKey { get; } = new(StringComparer.Ordinal);

    public float StuckTicks { get; set; }

    public float AirborneTicks { get; set; }

    public float JumpTicks { get; set; }

    public int PreviousMoveInput { get; set; }

    public bool PreviousJumpPressed { get; set; }

    public bool PreviousJumpHeld { get; set; }

    public bool PreviousDropInput { get; set; }

    public bool PreviousActionFirePrimary { get; set; }

    public bool PreviousActionFireSecondary { get; set; }

    public bool PreviousActionDropIntel { get; set; }

    public float FramesSinceJumpPressed { get; set; } = MLBotFeatureVectorizer.ShortTickScale;

    public float FramesSinceJumpReleased { get; set; } = MLBotFeatureVectorizer.ShortTickScale;
}

public static class MLBotObservationRuntimeStateTracker
{
    public static void Reset(MLBotObservationRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        state.HasPreviousSample = false;
        state.PreviousX = 0f;
        state.PreviousY = 0f;
        state.PreviousVelocityX = 0f;
        state.PreviousVelocityY = 0f;
        state.PreviousFacingDirectionX = 0f;
        state.PreviousIsGrounded = false;
        state.PreviousRemainingAirJumps = 0;
        state.PreviousObjectiveDistance = 0f;
        state.PreviousObjectiveDistanceDelta = 0f;
        state.PreviousNavigationDistance = 0f;
        state.PreviousNavigationDistanceDelta = 0f;
        state.PreviousControlPointObjectiveIndex = 0;
        state.PreviousControlPointCaptureProgress = 0f;
        state.PreviousOnControlPointObjective = false;
        state.TimeOnControlPointTicks = 0f;
        state.TimeSinceLeftControlPointTicks = 0f;
        state.ScoreRouteKey = null;
        state.ScoreRoutePhase = MLBotTaskPhase.None;
        state.ScoreRouteIndex = -1;
        state.TraversalSegmentKey = null;
        state.TraversalSegmentTicks = 0f;
        state.TraversalAttemptCountsByKey.Clear();
        state.StuckTicks = 0f;
        state.AirborneTicks = 0f;
        state.JumpTicks = 0f;
        state.PreviousMoveInput = 0;
        state.PreviousJumpPressed = false;
        state.PreviousJumpHeld = false;
        state.PreviousDropInput = false;
        state.PreviousActionFirePrimary = false;
        state.PreviousActionFireSecondary = false;
        state.PreviousActionDropIntel = false;
        state.FramesSinceJumpPressed = MLBotFeatureVectorizer.ShortTickScale;
        state.FramesSinceJumpReleased = MLBotFeatureVectorizer.ShortTickScale;
    }

    public static void SeedEpisodeStart(MLBotObservationRuntimeState state, MLBotEpisodeConfig config, PlayerEntity player)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(player);

        var hasSeed = config.StartPreviousMoveInput.HasValue
            || config.StartPreviousJumpHeld.HasValue
            || config.StartPreviousDropInput.HasValue
            || config.StartPreviousFirePrimary.HasValue
            || config.StartPreviousFireSecondary.HasValue
            || config.StartPreviousPositionDeltaX.HasValue
            || config.StartPreviousPositionDeltaY.HasValue
            || config.StartPreviousVelocityX.HasValue
            || config.StartPreviousVelocityY.HasValue
            || config.StartPreviousFacingDirectionX.HasValue
            || config.StartPreviousIsGrounded.HasValue
            || config.StartObjectiveDistance.HasValue
            || config.StartObjectiveDistanceDelta.HasValue
            || config.StartPreviousObjectiveDistanceDelta.HasValue
            || config.StartAirborneTicks.HasValue
            || config.StartJumpTicks.HasValue
            || config.StartFramesSinceJumpPressed.HasValue
            || config.StartFramesSinceJumpReleased.HasValue;
        if (!hasSeed)
        {
            return;
        }

        state.HasPreviousSample = true;
        state.PreviousX = player.X - (config.StartPreviousPositionDeltaX ?? 0f);
        state.PreviousY = player.Y - (config.StartPreviousPositionDeltaY ?? 0f);
        state.PreviousVelocityX = config.StartPreviousVelocityX ?? 0f;
        state.PreviousVelocityY = config.StartPreviousVelocityY ?? 0f;
        state.PreviousFacingDirectionX = config.StartPreviousFacingDirectionX ?? 0f;
        state.PreviousIsGrounded = config.StartPreviousIsGrounded ?? false;
        state.PreviousRemainingAirJumps = player.RemainingAirJumps;
        state.PreviousObjectiveDistance = MathF.Max(
            0f,
            (config.StartObjectiveDistance ?? 0f) + (config.StartObjectiveDistanceDelta ?? 0f));
        state.PreviousObjectiveDistanceDelta = config.StartPreviousObjectiveDistanceDelta ?? 0f;
        state.StuckTicks = 0f;
        state.AirborneTicks = config.StartAirborneTicks ?? 0f;
        state.JumpTicks = config.StartJumpTicks ?? 0f;
        state.PreviousMoveInput = config.StartPreviousMoveInput ?? 0;
        state.PreviousJumpPressed = false;
        state.PreviousJumpHeld = config.StartPreviousJumpHeld ?? false;
        state.PreviousDropInput = config.StartPreviousDropInput ?? false;
        state.PreviousActionFirePrimary = config.StartPreviousFirePrimary ?? false;
        state.PreviousActionFireSecondary = config.StartPreviousFireSecondary ?? false;
        state.FramesSinceJumpPressed = config.StartFramesSinceJumpPressed ?? MLBotFeatureVectorizer.ShortTickScale;
        state.FramesSinceJumpReleased = config.StartFramesSinceJumpReleased ?? MLBotFeatureVectorizer.ShortTickScale;
    }

    public static (float SegmentTicks, float AttemptCount) NoteTraversalSegment(MLBotObservationRuntimeState state, string? segmentKey)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (string.IsNullOrWhiteSpace(segmentKey))
        {
            state.TraversalSegmentKey = null;
            state.TraversalSegmentTicks = 0f;
            return (0f, 0f);
        }

        if (string.Equals(state.TraversalSegmentKey, segmentKey, StringComparison.Ordinal))
        {
            state.TraversalSegmentTicks += 1f;
        }
        else
        {
            state.TraversalSegmentKey = segmentKey;
            state.TraversalSegmentTicks = 0f;
            state.TraversalAttemptCountsByKey.TryGetValue(segmentKey, out var attemptCount);
            state.TraversalAttemptCountsByKey[segmentKey] = attemptCount + 1;
        }

        return (state.TraversalSegmentTicks, state.TraversalAttemptCountsByKey[segmentKey]);
    }

    public static void RecordAction(MLBotObservationRuntimeState state, in MLBotAction action)
    {
        ArgumentNullException.ThrowIfNull(state);

        var jumpWasHeld = state.PreviousJumpHeld;
        state.PreviousMoveInput = action.MoveDirection;
        state.PreviousJumpPressed = action.Jump && !jumpWasHeld;
        state.PreviousJumpHeld = action.Jump;
        state.PreviousDropInput = action.Crouch;
        state.PreviousActionFirePrimary = action.FirePrimary;
        state.PreviousActionFireSecondary = action.FireSecondary;
        state.PreviousActionDropIntel = action.DropIntel;

        if (action.Jump)
        {
            state.FramesSinceJumpPressed = state.PreviousJumpPressed
                ? 0f
                : state.FramesSinceJumpPressed + 1f;
            state.FramesSinceJumpReleased += 1f;
        }
        else
        {
            state.FramesSinceJumpPressed += 1f;
            state.FramesSinceJumpReleased = jumpWasHeld ? 0f : state.FramesSinceJumpReleased + 1f;
        }
    }

    public static void Update(MLBotObservationRuntimeState state, in MLBotObservation observation, PlayerEntity player)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(player);

        if (!state.HasPreviousSample)
        {
            state.StuckTicks = 0f;
        }
        else
        {
            var movedDistanceSquared = (player.X - state.PreviousX) * (player.X - state.PreviousX)
                + (player.Y - state.PreviousY) * (player.Y - state.PreviousY);
            var objectiveProgressed = observation.ObjectiveDistanceDelta > 0.5f;
            var navigationDistance = GetNavigationDistance(observation);
            var navigationProgressed = state.PreviousNavigationDistance > 0f
                && state.PreviousNavigationDistance - navigationDistance > 0.5f;
            if (!player.IsAlive)
            {
                state.StuckTicks = 0f;
            }
            else if (movedDistanceSquared < 4f && !objectiveProgressed && !navigationProgressed)
            {
                state.StuckTicks += 1f;
            }
            else
            {
                state.StuckTicks = 0f;
            }
        }

        var justJumped = player.IsAlive
            && player.VerticalSpeed < -1f
            && (state.PreviousIsGrounded || player.RemainingAirJumps < state.PreviousRemainingAirJumps);

        if (!player.IsAlive)
        {
            state.AirborneTicks = 0f;
            state.JumpTicks = 0f;
        }
        else
        {
            state.AirborneTicks = player.IsGrounded ? 0f : state.AirborneTicks + 1f;
            state.JumpTicks = justJumped ? 0f : state.JumpTicks + 1f;
        }

        state.HasPreviousSample = true;
        state.PreviousX = player.X;
        state.PreviousY = player.Y;
        state.PreviousVelocityX = player.HorizontalSpeed;
        state.PreviousVelocityY = player.VerticalSpeed;
        state.PreviousFacingDirectionX = player.FacingDirectionX;
        state.PreviousIsGrounded = player.IsGrounded;
        state.PreviousRemainingAirJumps = player.RemainingAirJumps;
        state.PreviousObjectiveDistance = observation.ObjectiveDistance;
        state.PreviousObjectiveDistanceDelta = observation.ObjectiveDistanceDelta;
        var currentNavigationDistance = GetNavigationDistance(observation);
        state.PreviousNavigationDistanceDelta = state.PreviousNavigationDistance <= 0f
            ? 0f
            : state.PreviousNavigationDistance - currentNavigationDistance;
        state.PreviousNavigationDistance = currentNavigationDistance;
        state.PreviousControlPointObjectiveIndex = observation.ControlPointObjective.Index;
        state.PreviousControlPointCaptureProgress = observation.ControlPointObjective.CaptureProgress;
        state.PreviousOnControlPointObjective = observation.ControlPointObjective.IsPlayerInCaptureZone;
        state.TimeOnControlPointTicks = observation.ControlPointObjective.TimeOnPointTicks;
        state.TimeSinceLeftControlPointTicks = observation.ControlPointObjective.TimeSinceLeftPointTicks;
    }

    private static float GetNavigationDistance(in MLBotObservation observation)
    {
        return observation.Waypoint is { HasWaypoint: true, IsFinalWaypoint: false }
            ? observation.Waypoint.Distance
            : observation.ObjectiveDistance;
    }
}
