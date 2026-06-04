using System.Buffers;

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
        var maxEngagementRange = ResolveMaxEngagementRange(self);
        var maxEngagementDistanceSquared = maxEngagementRange * maxEngagementRange;
        var candidates = ArrayPool<BotBrainTargetCandidate>.Shared.Rent(16);
        var candidateCount = 0;
        var sequence = 0;

        try
        {
            foreach (var generator in world.Generators)
            {
                if (generator.Team == ownTeam || generator.IsDestroyed)
                {
                    continue;
                }

                var targetX = generator.Marker.CenterX;
                var targetY = generator.Marker.CenterY;
                var distanceSquared = DistanceSquared(self.X, self.Y, targetX, targetY);
                if (distanceSquared >= maxEngagementDistanceSquared)
                {
                    continue;
                }

                AddCandidate(new BotBrainCombatTarget(
                    BotBrainCombatTargetKind.Generator,
                    generator.Team,
                    targetX,
                    targetY,
                    Generator: generator), distanceSquared);
            }

            foreach (var candidate in CombatDecisionResolver.EnumeratePlayers(world))
            {
                if (!IsValidTarget(candidate, self, opposingTeam))
                {
                    continue;
                }

                var distanceSquared = DistanceSquared(self.X, self.Y, candidate.X, candidate.Y);
                if (distanceSquared >= maxEngagementDistanceSquared)
                {
                    continue;
                }

                AddCandidate(new BotBrainCombatTarget(
                    BotBrainCombatTargetKind.Player,
                    candidate.Team,
                    candidate.X,
                    candidate.Y,
                    Player: candidate), distanceSquared);
            }

            foreach (var sentry in world.Sentries)
            {
                if (sentry.Team == ownTeam || sentry.Health <= 0)
                {
                    continue;
                }

                var distanceSquared = DistanceSquared(self.X, self.Y, sentry.X, sentry.Y);
                if (distanceSquared >= maxEngagementDistanceSquared)
                {
                    continue;
                }

                AddCandidate(new BotBrainCombatTarget(
                    BotBrainCombatTargetKind.Sentry,
                    sentry.Team,
                    sentry.X,
                    sentry.Y,
                    Sentry: sentry), distanceSquared);
            }

            if (candidateCount == 0)
            {
                return null;
            }

            if (candidateCount > 1)
            {
                Array.Sort(candidates, 0, candidateCount, BotBrainTargetCandidateDistanceComparer.Instance);
            }

            for (var index = 0; index < candidateCount; index += 1)
            {
                var target = candidates[index].Target;
                if (CombatDecisionResolver.HasCombatLineOfSight(world, self.X, self.Y, target.X, target.Y))
                {
                    return target;
                }
            }

            return null;
        }
        finally
        {
            Array.Clear(candidates, 0, candidateCount);
            ArrayPool<BotBrainTargetCandidate>.Shared.Return(candidates);
        }

        void AddCandidate(BotBrainCombatTarget target, float distanceSquared)
        {
            if (candidateCount == candidates.Length)
            {
                var replacement = ArrayPool<BotBrainTargetCandidate>.Shared.Rent(candidates.Length * 2);
                Array.Copy(candidates, replacement, candidates.Length);
                Array.Clear(candidates, 0, candidateCount);
                ArrayPool<BotBrainTargetCandidate>.Shared.Return(candidates);
                candidates = replacement;
            }

            candidates[candidateCount] = new BotBrainTargetCandidate(target, distanceSquared, sequence++);
            candidateCount += 1;
        }
    }

    private readonly record struct BotBrainTargetCandidate(
        BotBrainCombatTarget Target,
        float DistanceSquared,
        int Sequence);

    private sealed class BotBrainTargetCandidateDistanceComparer : IComparer<BotBrainTargetCandidate>
    {
        public static readonly BotBrainTargetCandidateDistanceComparer Instance = new();

        public int Compare(BotBrainTargetCandidate left, BotBrainTargetCandidate right)
        {
            var distanceComparison = left.DistanceSquared.CompareTo(right.DistanceSquared);
            return distanceComparison != 0
                ? distanceComparison
                : left.Sequence.CompareTo(right.Sequence);
        }
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

    private static float DistanceSquared(float ax, float ay, float bx, float by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        return (dx * dx) + (dy * dy);
    }

    private static float ResolveMaxEngagementRange(PlayerEntity self)
    {
        return self.ClassId == PlayerClass.Sniper ? 760f : MaxEngagementRange;
    }
}
