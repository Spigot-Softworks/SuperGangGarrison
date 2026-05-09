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
    private const float MaxEngagementRange = 600f;

    /// <summary>
    /// Find the best target to engage, or null if no valid target exists.
    /// </summary>
    public static PlayerEntity? SelectTarget(
        PlayerEntity self,
        SimulationWorld world,
        PlayerTeam ownTeam)
    {
        if (!self.IsAlive)
        {
            return null;
        }

        var opposingTeam = ownTeam == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
        PlayerEntity? bestTarget = null;
        var bestDistSq = MaxEngagementRange * MaxEngagementRange;

        // Check all network player slots.
        for (var slotIndex = 0; slotIndex < SimulationWorld.NetworkPlayerSlots.Count; slotIndex++)
        {
            var slot = SimulationWorld.NetworkPlayerSlots[slotIndex];
            if (!world.TryGetNetworkPlayer(slot, out var candidate))
            {
                continue;
            }

            if (!IsValidTarget(candidate, self, opposingTeam))
            {
                continue;
            }

            var distSq = DistanceSquared(self, candidate);
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestTarget = candidate;
            }
        }

        // Also check enemy dummy if enabled.
        if (world.EnemyPlayerEnabled && IsValidTarget(world.EnemyPlayer, self, opposingTeam))
        {
            var distSq = DistanceSquared(self, world.EnemyPlayer);
            if (distSq < bestDistSq)
            {
                bestTarget = world.EnemyPlayer;
            }
        }

        return bestTarget;
    }

    private static bool IsValidTarget(PlayerEntity candidate, PlayerEntity self, PlayerTeam opposingTeam)
    {
        if (!candidate.IsAlive || candidate.Id == self.Id)
        {
            return false;
        }

        if (candidate.Team != opposingTeam)
        {
            return false;
        }

        // Don't target fully cloaked spies.
        if (candidate.ClassId == PlayerClass.Spy && candidate.IsSpyCloaked && !candidate.IsSpyVisibleToEnemies)
        {
            return false;
        }

        return true;
    }

    private static float DistanceSquared(PlayerEntity a, PlayerEntity b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return (dx * dx) + (dy * dy);
    }
}
