namespace OpenGarrison.Core.BotBrain;

/// <summary>
/// Assembles a PlayerInputSnapshot from steering output, aim position, and combat decisions.
/// This is the final step: all the bot's thinking collapses into the same input struct
/// that a human player's keyboard/mouse would produce.
/// </summary>
public static class BotInputSynthesizer
{
    /// <summary>
    /// Maximum engagement range for firing weapons.
    /// </summary>
    private const float MaxFireRange = 500f;

    /// <summary>
    /// Minimum engagement range (don't fire point-blank with explosives).
    /// </summary>
    private const float MinExplosiveFireRange = 60f;

    public static PlayerInputSnapshot Synthesize(
        PlayerEntity self,
        SteeringOutput steering,
        float aimX,
        float aimY,
        PlayerEntity? combatTarget,
        PlayerInputSnapshot previousInput)
    {
        var left = steering.MoveDirection < 0f;
        var right = steering.MoveDirection > 0f;
        var up = steering.Jump && !previousInput.Up;
        var down = steering.DropDown;

        // Combat decisions.
        var firePrimary = false;
        var fireSecondary = false;

        if (combatTarget is not null
            && combatTarget.IsAlive
            && self.IsAlive
            && steering.EdgeKind == NavEdgeKind.Walk
            && MathF.Abs(steering.MoveDirection) <= 0.1f)
        {
            var dx = combatTarget.X - self.X;
            var dy = combatTarget.Y - self.Y;
            var dist = MathF.Sqrt((dx * dx) + (dy * dy));

            if (dist <= MaxFireRange)
            {
                firePrimary = ShouldFire(self, dist);
            }
        }

        return new PlayerInputSnapshot(
            Left: left,
            Right: right,
            Up: up,
            Down: down,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: firePrimary,
            FireSecondary: fireSecondary,
            AimWorldX: aimX,
            AimWorldY: aimY,
            DebugKill: false);
    }

    private static bool ShouldFire(PlayerEntity self, float distanceToTarget)
    {
        // Don't fire if weapon is on cooldown or reloading and empty.
        if (self.PrimaryCooldownTicks > 0)
        {
            return false;
        }

        if (self.CurrentShells <= 0 && self.ReloadTicksUntilNextShell > 0)
        {
            return false;
        }

        // For explosive classes (Soldier, Demoman), don't fire too close.
        if (self.ClassId is PlayerClass.Soldier or PlayerClass.Demoman)
        {
            if (distanceToTarget < MinExplosiveFireRange)
            {
                return false;
            }
        }

        return true;
    }
}
