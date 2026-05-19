namespace OpenGarrison.Core.BotBrain;

/// <summary>
/// Selects the best enemy target for the bot to engage.
/// Picks nearest alive enemy on the opposing team.
/// </summary>
public static class TargetSelector
{
    /// <summary>
    /// Maximum engagement distance. Beyond this, the bot won't try to fight.
    /// </summary>
    private const float MaxEngagementRange = 375f;

    /// <summary>
    /// Find the best target to engage, or null if no valid target exists.
    /// </summary>
    public static PlayerEntity? SelectTarget(
        PlayerEntity self,
        SimulationWorld world,
        PlayerTeam ownTeam)
    {
        return SelectCombatTarget(self, world, ownTeam)?.Player;
    }

    public static BotBrainCombatTarget? SelectCombatTarget(
        PlayerEntity self,
        SimulationWorld world,
        PlayerTeam ownTeam)
    {
        if (!self.IsAlive)
        {
            return null;
        }

        var opposingTeam = ownTeam == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
        BotBrainCombatTarget? bestTarget = null;
        var bestDistance = ResolveMaxEngagementRange(self);

        foreach (var generator in world.Generators)
        {
            if (generator.Team == ownTeam || generator.IsDestroyed)
            {
                continue;
            }

            var targetX = generator.Marker.CenterX;
            var targetY = generator.Marker.CenterY;
            if (!CombatDecisionResolver.HasLineOfSight(world, self.X, self.Y, targetX, targetY, self.Team, self.IsCarryingIntel))
            {
                continue;
            }

            var distance = Distance(self.X, self.Y, targetX, targetY);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestTarget = new BotBrainCombatTarget(BotBrainCombatTargetKind.Generator, generator.Team, targetX, targetY, Generator: generator);
        }

        foreach (var candidate in CombatDecisionResolver.EnumeratePlayers(world))
        {
            if (!IsValidTarget(candidate, self, opposingTeam))
            {
                continue;
            }

            if (!CombatDecisionResolver.HasLineOfSight(world, self.X, self.Y, candidate.X, candidate.Y, self.Team, self.IsCarryingIntel))
            {
                continue;
            }

            var distance = Distance(self.X, self.Y, candidate.X, candidate.Y);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestTarget = new BotBrainCombatTarget(BotBrainCombatTargetKind.Player, candidate.Team, candidate.X, candidate.Y, Player: candidate);
        }

        foreach (var sentry in world.Sentries)
        {
            if (sentry.Team == ownTeam || sentry.Health <= 0)
            {
                continue;
            }

            if (!CombatDecisionResolver.HasLineOfSight(world, self.X, self.Y, sentry.X, sentry.Y, self.Team, self.IsCarryingIntel))
            {
                continue;
            }

            var distance = Distance(self.X, self.Y, sentry.X, sentry.Y);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestTarget = new BotBrainCombatTarget(BotBrainCombatTargetKind.Sentry, sentry.Team, sentry.X, sentry.Y, Sentry: sentry);
        }

        return bestTarget;
    }

    private static bool IsValidTarget(PlayerEntity candidate, PlayerEntity self, PlayerTeam opposingTeam)
    {
        if (!candidate.IsAlive || candidate.Id == self.Id)
        {
            return false;
        }

        var treatAsFriendlyFireTarget = SimulationWorld.ShouldTreatPlayerAsExperimentalFriendlyFireTarget(self, candidate);
        if (candidate.Team != opposingTeam && !treatAsFriendlyFireTarget)
        {
            return false;
        }

        if (!CombatDecisionResolver.IsPlayerVisibleToBot(self, candidate))
        {
            return false;
        }

        return true;
    }

    private static float Distance(float ax, float ay, float bx, float by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static float ResolveMaxEngagementRange(PlayerEntity self)
    {
        return self.ClassId == PlayerClass.Sniper ? 760f : MaxEngagementRange;
    }
}
