using OpenGarrison.MLBot.Contracts;

namespace OpenGarrison.MLBot.Policies;

public sealed class LocalTraversalRecoveryPolicyRuntime : IMLBotPolicyRuntime, IDisposable
{
    private const int GroundBackoffTicks = 10;
    private const int GroundCommitTicks = 22;
    private const int AirBackoffTicks = 14;
    private const int AirCommitTicks = 16;
    private const float WallDistance = 10f;
    private const float PinnedVelocityX = 8f;
    private const float PinnedDeltaX = 1.25f;

    private readonly IMLBotPolicyRuntime _innerPolicy;
    private int _backoffTicksRemaining;
    private int _commitTicksRemaining;
    private int _backoffDirection;
    private int _commitDirection;
    private bool _airRecovery;

    public LocalTraversalRecoveryPolicyRuntime(IMLBotPolicyRuntime innerPolicy)
    {
        _innerPolicy = innerPolicy;
    }

    public MLBotAction Evaluate(in MLBotObservation observation)
    {
        var innerAction = _innerPolicy.Evaluate(observation);
        if (!CanRecover(observation))
        {
            Reset();
            return innerAction;
        }

        if (!IsCurrentSequenceUseful(observation))
        {
            Reset();
        }

        if (_backoffTicksRemaining <= 0 && _commitTicksRemaining <= 0 && ShouldStartRecovery(observation, innerAction))
        {
            StartRecovery(observation, innerAction);
        }

        if (_backoffTicksRemaining > 0)
        {
            _backoffTicksRemaining -= 1;
            return BuildRecoveryAction(observation, _backoffDirection, jump: _airRecovery && CanJump(observation));
        }

        if (_commitTicksRemaining > 0)
        {
            _commitTicksRemaining -= 1;
            var jump = CanJump(observation)
                && (observation.Objective.RelativeY < -18f
                    || observation.Probes.ForwardFootObstacleDistance <= WallDistance
                    || observation.TerrainAffordance.HasBestUpwardLanding);
            return BuildRecoveryAction(observation, _commitDirection, jump);
        }

        return innerAction;
    }

    public void Dispose()
    {
        if (_innerPolicy is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private static bool CanRecover(in MLBotObservation observation)
    {
        return observation.Objective.HasObjective
            && !observation.IsRespawning
            && observation.TaskPhase != MLBotTaskPhase.None;
    }

    private bool IsCurrentSequenceUseful(in MLBotObservation observation)
    {
        if (_backoffTicksRemaining <= 0 && _commitTicksRemaining <= 0)
        {
            return true;
        }

        var intendedDirection = _backoffTicksRemaining > 0 ? _backoffDirection : _commitDirection;
        return intendedDirection != 0
            && observation.Objective.HasObjective
            && !HasMovedPastObjective(observation, intendedDirection);
    }

    private static bool HasMovedPastObjective(in MLBotObservation observation, int intendedDirection)
    {
        return MathF.Sign(observation.Objective.RelativeX) != 0f
            && MathF.Sign(observation.Objective.RelativeX) == -intendedDirection
            && MathF.Abs(observation.Objective.RelativeX) <= 24f
            && MathF.Abs(observation.Objective.RelativeY) <= 32f;
    }

    private static bool ShouldStartRecovery(in MLBotObservation observation, in MLBotAction action)
    {
        if (action.MoveDirection == 0)
        {
            return IsMakingNoProgress(observation)
                && MathF.Abs(observation.Objective.RelativeX) > 12f;
        }

        var movingLeft = action.MoveDirection < 0;
        var blockedInMoveDirection = movingLeft
            ? observation.Probes.LeftFootObstacleDistance <= WallDistance || observation.Probes.TouchingLeftWall
            : observation.Probes.RightFootObstacleDistance <= WallDistance || observation.Probes.TouchingRightWall;

        if (observation.IsGrounded)
        {
            return blockedInMoveDirection
                && (observation.StuckTicks >= 8f
                    || (MathF.Abs(observation.VelocityX) <= PinnedVelocityX && MathF.Abs(observation.PreviousPositionDeltaX) <= PinnedDeltaX));
        }

        return blockedInMoveDirection
            && MathF.Abs(observation.VelocityX) <= PinnedVelocityX
            && MathF.Abs(observation.PreviousPositionDeltaX) <= PinnedDeltaX
            && MathF.Abs(observation.PreviousVelocityX) <= PinnedVelocityX;
    }

    private static bool IsMakingNoProgress(in MLBotObservation observation)
    {
        return observation.StuckTicks >= 8f
            || (MathF.Abs(observation.VelocityX) <= PinnedVelocityX
                && MathF.Abs(observation.PreviousPositionDeltaX) <= PinnedDeltaX
                && observation.ObjectiveDistanceDelta <= 0.5f
                && observation.PreviousObjectiveDistanceDelta <= 0.5f);
    }

    private void StartRecovery(in MLBotObservation observation, in MLBotAction action)
    {
        var desiredDirection = ResolveDesiredDirection(observation, action);
        var blockedDirection = action.MoveDirection != 0 ? action.MoveDirection : desiredDirection;
        _backoffDirection = -Math.Sign(blockedDirection == 0 ? desiredDirection : blockedDirection);
        if (_backoffDirection == 0)
        {
            _backoffDirection = -desiredDirection;
        }

        _commitDirection = desiredDirection != 0 ? desiredDirection : -_backoffDirection;
        _airRecovery = !observation.IsGrounded;
        _backoffTicksRemaining = _airRecovery ? AirBackoffTicks : GroundBackoffTicks;
        _commitTicksRemaining = _airRecovery ? AirCommitTicks : GroundCommitTicks;
    }

    private static int ResolveDesiredDirection(in MLBotObservation observation, in MLBotAction action)
    {
        var terrain = observation.TerrainAffordance;
        if (terrain.HasBestUpwardLanding && observation.Objective.RelativeY < -24f)
        {
            var terrainDirection = Math.Sign(terrain.BestUpwardLandingDirection);
            if (terrainDirection != 0)
            {
                return terrainDirection;
            }
        }

        var objectiveDirection = Math.Sign(observation.Objective.RelativeX);
        if (objectiveDirection != 0)
        {
            return objectiveDirection;
        }

        if (action.MoveDirection != 0)
        {
            return action.MoveDirection;
        }

        return observation.FacingDirectionX >= 0f ? 1 : -1;
    }

    private static MLBotAction BuildRecoveryAction(in MLBotObservation observation, int moveDirection, bool jump)
    {
        return new MLBotAction(
            MoveDirection: Math.Clamp(moveDirection, -1, 1),
            Jump: jump,
            Crouch: false,
            FirePrimary: false,
            FireSecondary: false,
            DropIntel: false,
            AimWorldX: observation.Objective.HasObjective ? observation.Objective.WorldX : observation.BotX,
            AimWorldY: observation.Objective.HasObjective ? observation.Objective.WorldY : observation.BotY);
    }

    private static bool CanJump(in MLBotObservation observation)
    {
        return observation.IsGrounded || observation.RemainingAirJumps > 0;
    }

    private void Reset()
    {
        _backoffTicksRemaining = 0;
        _commitTicksRemaining = 0;
        _backoffDirection = 0;
        _commitDirection = 0;
        _airRecovery = false;
    }
}
