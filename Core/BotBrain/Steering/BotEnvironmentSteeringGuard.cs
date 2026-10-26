namespace OpenGarrison.Core.BotBrain;

internal sealed class BotEnvironmentSteeringGuard
{
    private SimpleLevel? _level;
    private float _checkX;
    private float _checkY;
    private long _checkFrame;
    private long _escapeUntilFrame;
    private float _escapeDirection;

    public bool StartedEscape { get; private set; }
    public string Reason { get; private set; } = string.Empty;

    public void Reset()
    {
        _level = null;
        _escapeUntilFrame = 0;
        _escapeDirection = 0;
    }

    public bool Apply(SimulationWorld world, PlayerEntity player, ref SteeringOutput steering)
    {
        StartedEscape = false;
        Reason = string.Empty;
        var level = world.Level;
        if (level.IsTopDown || !BotNavigationEnvironment.HasTraversalEffects(level))
            return false;
        if (!ReferenceEquals(_level, level))
        {
            Reset();
            _level = level;
            _checkX = player.X;
            _checkY = player.Y;
            _checkFrame = world.Frame;
        }

        if (world.Frame < _escapeUntilFrame)
        {
            SetEscape(level, player, ref steering);
            Reason = "environment_escape";
            return true;
        }

        var direction = MathF.Sign(steering.MoveDirection);
        if (direction == 0)
        {
            _checkFrame = world.Frame;
            _checkX = player.X;
            _checkY = player.Y;
            return false;
        }

        var lookAhead = MathF.Max(24f, MathF.Abs(player.HorizontalSpeed) * 0.2f + 12f);
        var opposingForce = BotNavigationEnvironment.HasOpposingForce(level,
            player.X, player.Y, player.X + direction * lookAhead, player.Y,
            player.CollisionLeftOffset, player.CollisionTopOffset,
            player.CollisionRightOffset, player.CollisionBottomOffset);
        var stagnant = false;
        if (world.Frame - _checkFrame >= world.Config.TicksPerSecond)
        {
            stagnant = MathF.Abs(player.X - _checkX) < 12f && MathF.Abs(player.Y - _checkY) < 24f
                && (opposingForce || PrimitiveDirectDrive.WouldMoveIntoObstacle(world, player, direction));
            _checkFrame = world.Frame;
            _checkX = player.X;
            _checkY = player.Y;
        }

        if (!steering.Jump && player.IsGrounded
            && BotNavigationEnvironment.HasLethalFall(level, player, player.X + direction * lookAhead))
        {
            if (CanJumpAcross(level, player, direction))
            {
                steering.Jump = true;
                steering.DropDown = false;
                Reason = "pit_jump";
                return true;
            }
            Reason = "lethal_drop";
        }
        else if (opposingForce && !steering.Jump)
            Reason = "opposing_push";
        else if (stagnant)
            Reason = "blocked_corridor";
        else
            return false;

        StartedEscape = true;
        _escapeDirection = -direction;
        _escapeUntilFrame = world.Frame + Math.Max(1, world.Config.TicksPerSecond * 3 / 4);
        SetEscape(level, player, ref steering);
        return true;
    }

    private void SetEscape(SimpleLevel level, PlayerEntity player, ref SteeringOutput steering)
    {
        steering.MoveDirection = !BotNavigationEnvironment.HasLethalFall(level, player, player.X + _escapeDirection * 28f)
            && player.CanOccupy(level, player.Team, player.X + _escapeDirection * 8f, player.Y)
            ? _escapeDirection : 0f;
        // A conveyor can press the player against the foot of an upward
        // shaft. If backing up is blocked, rise out of the horizontal push
        // instead of holding still and preventing the lift-entry jump.
        steering.Jump = steering.MoveDirection == 0f && player.IsGrounded
            && CanRiseOutOfPush(level, player);
        steering.DropDown = false;
        steering.RecipeTrace = default;
    }

    private bool CanRiseOutOfPush(SimpleLevel level, PlayerEntity player)
    {
        if (!BotNavigationEnvironment.HasOpposingForce(level,
                player.X, player.Y, player.X - _escapeDirection, player.Y,
                player.CollisionLeftOffset, player.CollisionTopOffset,
                player.CollisionRightOffset, player.CollisionBottomOffset))
            return false;
        var probe = new PlayerEntity(-901_103, player.ClassDefinition, "BotLiftEntryProbe");
        var state = player.CapturePredictionState();
        probe.RestorePredictionState(in state);
        for (var tick = 0; tick < 30; tick++)
        {
            BotNavigationEnvironment.AdvanceProbe(level, probe,
                new PlayerInputSnapshot { Up = tick == 0 }, tick == 0, player.Team);
            if (!BotNavigationEnvironment.IsSafe(level, probe))
                return false;
            if (probe.Y < player.Y - 32f
                && !BotNavigationEnvironment.HasOpposingForce(level,
                    probe.X, probe.Y, probe.X - _escapeDirection, probe.Y,
                    probe.CollisionLeftOffset, probe.CollisionTopOffset,
                    probe.CollisionRightOffset, probe.CollisionBottomOffset))
                return true;
        }
        return false;
    }

    private static bool CanJumpAcross(SimpleLevel level, PlayerEntity player, int direction)
    {
        var probe = new PlayerEntity(-901_102, player.ClassDefinition, "BotPitJumpProbe");
        var state = player.CapturePredictionState();
        probe.RestorePredictionState(in state);
        var input = new PlayerInputSnapshot { Left = direction < 0, Right = direction > 0, Up = true };
        var airborne = false;
        for (var tick = 0; tick < 60; tick++)
        {
            BotNavigationEnvironment.AdvanceProbe(level, probe, input, tick == 0, player.Team);
            input = input with { Up = false };
            if (!BotNavigationEnvironment.IsSafe(level, probe))
                return false;
            airborne |= !probe.IsGrounded;
            if (airborne && probe.IsGrounded)
                return (probe.X - player.X) * direction > 24f;
        }
        return false;
    }
}
