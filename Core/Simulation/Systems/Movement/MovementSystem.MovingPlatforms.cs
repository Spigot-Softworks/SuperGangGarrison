namespace OpenGarrison.Core;

public sealed partial class MovementSystem
{
    private readonly List<PlayerEntity> _alivePlayersScratch = new();
    private readonly List<PlayerEntity> _carriedPlayersScratch = new();

    public void ResetMovingPlatformsForLevel()
    {
        _movingPlatforms.Clear();
        for (var index = 0; index < Level.MovingPlatforms.Count; index += 1)
        {
            _movingPlatforms.Add(new MovingPlatformRuntimeState(index, Level.MovingPlatforms[index]));
        }
    }

    public void AdvanceMovingPlatforms()
    {
        if (_movingPlatforms.Count == 0)
        {
            return;
        }

        _alivePlayersScratch.Clear();
        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (player.IsAlive)
            {
                _alivePlayersScratch.Add(player);
            }
        }

        foreach (var platform in _movingPlatforms)
        {
            TryTriggerMovingPlatform(platform, _alivePlayersScratch);
            var oldLeft = platform.Left;
            var oldTop = platform.Top;
            var oldRight = platform.Right;
            var oldBottom = platform.Bottom;
            _carriedPlayersScratch.Clear();
            foreach (var player in _alivePlayersScratch)
            {
                if (player.IsStandingOnMovingPlatform(oldLeft, oldTop, oldRight))
                {
                    _carriedPlayersScratch.Add(player);
                }
            }

            var (deltaX, deltaY) = platform.Advance(Config.FixedDeltaSeconds);
            if (deltaX == 0f && deltaY == 0f)
            {
                continue;
            }

            foreach (var player in _carriedPlayersScratch)
            {
                player.TryMoveWithMovingPlatform(Level, player.Team, deltaX, deltaY, platform.ResetMovementState);
            }

            if (deltaY < 0f)
            {
                LiftPlayersCaughtByMovingPlatform(
                    platform,
                    oldLeft,
                    oldTop,
                    oldRight,
                    oldBottom,
                    _alivePlayersScratch);
            }
        }
    }

    private static void TryTriggerMovingPlatform(MovingPlatformRuntimeState platform, IReadOnlyList<PlayerEntity> players)
    {
        if (!platform.IsStopped)
        {
            return;
        }

        foreach (var player in players)
        {
            if (!player.IsStandingOnMovingPlatform(platform.Left, platform.Top, platform.Right))
            {
                continue;
            }

            platform.TryTrigger(player.IsCarryingIntel);
            if (!platform.IsStopped)
            {
                return;
            }
        }
    }

    public void ResolveMovingPlatformLanding(PlayerEntity player, float previousBottom, bool allowFallThrough)
    {
        if (Level.IsTopDown || _movingPlatforms.Count == 0 || !player.IsAlive)
        {
            return;
        }

        foreach (var platform in _movingPlatforms)
        {
            if (player.TryLandOnMovingPlatform(
                    Level,
                    player.Team,
                    platform.Left,
                    platform.Top,
                    platform.Right,
                    previousBottom,
                    allowFallThrough,
                    platform.ResetMovementState))
            {
                platform.TryTrigger(player.IsCarryingIntel);
                return;
            }
        }
    }

    public bool HasLandedArrowGroundSupport(PlayerEntity player, bool allowFallThrough)
    {
        if (Level.IsTopDown || allowFallThrough || !player.IsAlive || player.VerticalSpeed < 0f)
        {
            return false;
        }

        foreach (var arrow in _host.EnumerateArrowProjectiles())
        {
            if (!arrow.TryGetOneWayPlatformBounds(out var left, out var top, out var right))
            {
                continue;
            }

            if (player.IsStandingOnMovingPlatform(left, top, right))
            {
                return true;
            }
        }

        return false;
    }

    public void ResolveLandedArrowLanding(PlayerEntity player, float previousBottom, bool allowFallThrough)
    {
        if (Level.IsTopDown)
        {
            return;
        }

        if (allowFallThrough || !player.IsAlive || player.IsGrounded)
        {
            return;
        }

        foreach (var arrow in _host.EnumerateArrowProjectiles())
        {
            if (!arrow.TryGetOneWayPlatformBounds(out var left, out var top, out var right))
            {
                continue;
            }

            if (player.TryLandOnMovingPlatform(
                    Level,
                    player.Team,
                    left,
                    top,
                    right,
                    previousBottom,
                    allowFallThrough,
                    resetMovementState: false))
            {
                return;
            }
        }
    }

    private void LiftPlayersCaughtByMovingPlatform(
        MovingPlatformRuntimeState platform,
        float oldLeft,
        float oldTop,
        float oldRight,
        float oldBottom,
        IReadOnlyList<PlayerEntity> players)
    {
        foreach (var player in players)
        {
            if (player.IsStandingOnMovingPlatform(oldLeft, oldTop, oldRight))
            {
                continue;
            }

            if (player.TryLiftOntoMovingPlatform(
                    Level,
                    player.Team,
                    platform.Left,
                    platform.Top,
                    platform.Right,
                    oldBottom,
                    platform.ResetMovementState))
            {
                platform.TryTrigger(player.IsCarryingIntel);
            }
        }
    }
}
