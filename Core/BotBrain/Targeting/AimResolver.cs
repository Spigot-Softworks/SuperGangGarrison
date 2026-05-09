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

    /// <summary>
    /// Vertical offset to aim slightly above center-mass for better hit probability.
    /// </summary>
    private const float CombatAimVerticalOffset = 0.3f;

    public (float AimX, float AimY) Resolve(
        PlayerEntity self,
        PlayerEntity? combatTarget,
        NavGraph? graph,
        NavPath? path,
        SteeringOutput steering)
    {
        if (combatTarget is not null && combatTarget.IsAlive)
        {
            return ResolveCombatAim(self, combatTarget);
        }

        return ResolveTraversalAim(self, graph, path, steering);
    }

    private (float AimX, float AimY) ResolveCombatAim(PlayerEntity self, PlayerEntity target)
    {
        // Aim at center-mass with slight upward bias and jitter.
        var targetCenterX = target.X;
        var targetCenterY = target.Y - (target.Height * CombatAimVerticalOffset);

        var jitterX = ((float)_random.NextDouble() - 0.5f) * MaxAimJitter * 2f;
        var jitterY = ((float)_random.NextDouble() - 0.5f) * MaxAimJitter * 2f;

        return (targetCenterX + jitterX, targetCenterY + jitterY);
    }

    private static (float AimX, float AimY) ResolveTraversalAim(
        PlayerEntity self,
        NavGraph? graph,
        NavPath? path,
        SteeringOutput steering)
    {
        // If we have a path, aim toward the current waypoint.
        if (graph is not null && path is not null && !path.IsComplete)
        {
            var target = graph.GetNode(path.CurrentNode);
            return (target.X, target.Y);
        }

        // Otherwise, aim in the direction we're moving.
        var aimX = self.X + (steering.MoveDirection != 0f
            ? steering.MoveDirection * TraversalAimAheadDistance
            : self.FacingDirectionX * TraversalAimAheadDistance);

        return (aimX, self.Y);
    }
}
