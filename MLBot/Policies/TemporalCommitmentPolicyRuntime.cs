using OpenGarrison.MLBot.Contracts;

namespace OpenGarrison.MLBot.Policies;

public sealed class TemporalCommitmentPolicyRuntime : IMLBotPolicyRuntime, IDisposable
{
    private readonly IMLBotPolicyRuntime _innerPolicy;
    private readonly int _moveHoldTicks;
    private readonly int _jumpHoldTicks;
    private int _committedMoveDirection;
    private int _moveTicksRemaining;
    private int _jumpTicksRemaining;

    public TemporalCommitmentPolicyRuntime(IMLBotPolicyRuntime innerPolicy, int moveHoldTicks, int jumpHoldTicks)
    {
        _innerPolicy = innerPolicy;
        _moveHoldTicks = Math.Max(0, moveHoldTicks);
        _jumpHoldTicks = Math.Max(0, jumpHoldTicks);
    }

    public MLBotAction Evaluate(in MLBotObservation observation)
    {
        var action = _innerPolicy.Evaluate(observation);
        var moveDirection = ResolveMoveDirection(action.MoveDirection);
        var jump = ResolveJump(action.Jump);
        return action with
        {
            MoveDirection = moveDirection,
            Jump = jump,
        };
    }

    public void Dispose()
    {
        if (_innerPolicy is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private int ResolveMoveDirection(int requestedMoveDirection)
    {
        if (_moveHoldTicks <= 0)
        {
            return requestedMoveDirection;
        }

        if (requestedMoveDirection != 0)
        {
            _committedMoveDirection = requestedMoveDirection;
            _moveTicksRemaining = _moveHoldTicks;
            return requestedMoveDirection;
        }

        if (_moveTicksRemaining <= 0)
        {
            _committedMoveDirection = 0;
            return 0;
        }

        _moveTicksRemaining -= 1;
        return _committedMoveDirection;
    }

    private bool ResolveJump(bool requestedJump)
    {
        if (_jumpHoldTicks <= 0)
        {
            return requestedJump;
        }

        if (requestedJump)
        {
            _jumpTicksRemaining = _jumpHoldTicks;
            return true;
        }

        if (_jumpTicksRemaining <= 0)
        {
            return false;
        }

        _jumpTicksRemaining -= 1;
        return true;
    }
}
