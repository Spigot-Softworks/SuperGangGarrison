using System.Buffers;

namespace OpenGarrison.Core.BotBrain;

/// <summary>
/// Selects the best enemy target for the bot to engage.
/// Picks nearest alive enemy on the opposing team.
/// </summary>
public static class TargetSelector
{
    private const float MartyrProtectorPriorityRange = 375f;
    /// <summary>
    /// Maximum engagement distance. Beyond this, the bot won't try to fight.
    /// </summary>
    // Bot perception is screen-based in the legacy game. Weapon-specific
    // decisions still limit whether a shot is useful, but target acquisition
    // must not make bots blind merely because a map wall or bulletwall sits
    // between two points on the 2D playfield.
    private const float MaxEngagementRange = 1100f;

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

                var prioritizedCandidate = ResolveMartyrPriorityTarget(
                    self,
                    world,
                    candidate,
                    opposingTeam,
                    maxEngagementDistanceSquared);
                distanceSquared = DistanceSquared(
                    self.X,
                    self.Y,
                    prioritizedCandidate.X,
                    prioritizedCandidate.Y);

                AddCandidate(new BotBrainCombatTarget(
                    BotBrainCombatTargetKind.Player,
                    prioritizedCandidate.Team,
                    prioritizedCandidate.X,
                    prioritizedCandidate.Y,
                    Player: prioritizedCandidate), distanceSquared);
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

                // Dispensers are valid structure targets just like sentries. Give an
                // active dispenser a modest defensive priority so bots do not walk
                // past the source of the enemy team's healing/speed aura while
                // choosing a farther player target.
                var targetSelectionDistanceSquared = sentry.IsDispenser
                    ? distanceSquared * 0.75f
                    : distanceSquared;
                AddCandidate(new BotBrainCombatTarget(
                    BotBrainCombatTargetKind.Sentry,
                    sentry.Team,
                    sentry.X,
                    sentry.Y,
                    Sentry: sentry), targetSelectionDistanceSquared);
            }

            if (candidateCount == 0)
            {
                return null;
            }

            if (candidateCount > 1)
            {
                Array.Sort(candidates, 0, candidateCount, BotBrainTargetCandidateDistanceComparer.Instance);
            }

            // Seeing a player and being able to damage that player are separate
            // concerns. The authoritative weapon trace remains responsible for
            // stopping shots at solids, barriers, bulletwalls, and teammates.
            // Keeping acquisition screen-wide lets navigation/combat steering
            // react instead of leaving bots idle whenever a trace is blocked.
            return candidates[0].Target;
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

    internal static PlayerEntity ResolveMartyrPriorityTarget(
        PlayerEntity self,
        SimulationWorld world,
        PlayerEntity candidate,
        PlayerTeam opposingTeam,
        float maxEngagementDistanceSquared)
    {
        if (!world.TryGetLastToDieMartyrProtector(candidate, out var protector)
            || !IsValidTarget(protector, self, opposingTeam)
            || DistanceSquared(self.X, self.Y, protector.X, protector.Y)
                >= MathF.Min(maxEngagementDistanceSquared, MartyrProtectorPriorityRange * MartyrProtectorPriorityRange)
            // The protector is a special damage-priority exception. Keep the
            // exception grounded in the actual combat line so a spawn gate or
            // solid cannot make a bot abandon the visible martyr target.
            || !CombatDecisionResolver.HasCombatLineOfSight(world, self.X, self.Y, protector.X, protector.Y))
        {
            return candidate;
        }

        return protector;
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
