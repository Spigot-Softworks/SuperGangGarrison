namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void ResetMovingPlatformsForLevel()
    {
        _movingPlatforms.Clear();
        for (var index = 0; index < Level.MovingPlatforms.Count; index += 1)
        {
            _movingPlatforms.Add(new MovingPlatformRuntimeState(index, Level.MovingPlatforms[index]));
        }
    }

    private void AdvanceMovingPlatforms()
    {
        if (_movingPlatforms.Count == 0)
        {
            return;
        }

        var players = EnumerateSimulatedPlayers()
            .Where(static player => player.IsAlive)
            .ToArray();
        foreach (var platform in _movingPlatforms)
        {
            TryTriggerMovingPlatform(platform, players);
            var oldLeft = platform.Left;
            var oldTop = platform.Top;
            var oldRight = platform.Right;
            var oldBottom = platform.Bottom;
            var carriedPlayers = players
                .Where(player => player.IsStandingOnMovingPlatform(oldLeft, oldTop, oldRight))
                .ToArray();

            var (deltaX, deltaY) = platform.Advance(Config.FixedDeltaSeconds);
            if (deltaX == 0f && deltaY == 0f)
            {
                continue;
            }

            foreach (var player in carriedPlayers)
            {
                player.TryMoveWithMovingPlatform(Level, player.Team, deltaX, deltaY, platform.ResetMovementState);
            }

            if (deltaY < 0f)
            {
                LiftPlayersCaughtByMovingPlatform(platform, oldLeft, oldTop, oldRight, oldBottom, players);
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

    private void ResolveMovingPlatformLanding(PlayerEntity player, float previousBottom, bool allowFallThrough)
    {
        if (_movingPlatforms.Count == 0 || !player.IsAlive)
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
