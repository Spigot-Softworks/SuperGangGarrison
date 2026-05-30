namespace OpenGarrison.Core.BotBrain;

/// <summary>
/// Computes aim position (AimWorldX, AimWorldY) for the bot.
/// When engaging an enemy, aims at center-mass. When traversing, aims in movement direction.
/// </summary>
public sealed class AimResolver
{
    private readonly Random _random = new();

    /// <summary>
    /// Maximum random aim offset to prevent robotic snap-aiming.
    /// </summary>
    private const float MaxAimJitter = 4f;

    /// <summary>
    /// How far ahead to aim when traversing (no combat target).
    /// </summary>
    private const float TraversalAimAheadDistance = 120f;

    public (float AimX, float AimY) Resolve(
        PlayerEntity self,
        BotBrainCombatTarget? combatTarget,
        PlayerEntity? healTarget,
        NavGraph? graph,
        NavPath? path,
        SteeringOutput steering)
    {
        if (healTarget is not null && healTarget.IsAlive)
        {
            return ResolvePlayerAim(healTarget, applyJitter: false);
        }

        if (combatTarget is { } target)
        {
            return ResolveCombatAim(self, target);
        }

        return ResolveTraversalAim(self, graph, path, steering);
    }

    private (float AimX, float AimY) ResolveCombatAim(PlayerEntity self, BotBrainCombatTarget target)
    {
        return target.Player is { } player
            ? ResolvePlayerAim(player, applyJitter: true)
            : ApplyJitter(target.X, target.Y);
    }

    private (float AimX, float AimY) ResolvePlayerAim(PlayerEntity target, bool applyJitter)
    {
        var targetX = target.X;
        var targetY = ResolvePlayerAimFocusY(target);
        return applyJitter
            ? ApplyJitter(targetX, targetY)
            : (targetX, targetY);
    }

    private (float AimX, float AimY) ApplyJitter(float targetX, float targetY)
    {
        var jitterX = ((float)_random.NextDouble() - 0.5f) * MaxAimJitter * 2f;
        var jitterY = ((float)_random.NextDouble() - 0.5f) * MaxAimJitter * 2f;

        return (targetX + jitterX, targetY + jitterY);
    }

    private static (float AimX, float AimY) ResolveTraversalAim(
        PlayerEntity self,
        NavGraph? graph,
        NavPath? path,
        SteeringOutput steering)
    {
        if (steering.HasAimOverride)
        {
            return (steering.AimOverrideX, steering.AimOverrideY);
        }

        var neutralAimY = ResolvePlayerAimFocusY(self);

        // If we have a path, face the current waypoint horizontally without tracking
        // waypoint elevation; otherwise bots stare into floors while traversing drops.
        if (graph is not null && path is not null && !path.IsComplete)
        {
            var target = graph.GetNode(path.CurrentNode);
            return (target.X, neutralAimY);
        }

        // Otherwise, aim in the direction we're moving.
        var aimX = self.X + (steering.MoveDirection != 0f
            ? steering.MoveDirection * TraversalAimAheadDistance
            : self.FacingDirectionX * TraversalAimAheadDistance);

        return (aimX, neutralAimY);
    }

    private static float ResolvePlayerAimFocusY(PlayerEntity player) =>
        player.Y - MathF.Min(8f, player.Height * 0.25f);
}
