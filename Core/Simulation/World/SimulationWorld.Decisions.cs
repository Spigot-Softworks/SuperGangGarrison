namespace OpenGarrison.Core;

// The plugin decision surface. Interceptor state lives on DecisionGate; these
// properties are the public API the server plugin host and tests set.
public sealed partial class SimulationWorld
{
    public Func<WorldSpawnDecisionRequest, WorldDecisionResult>? SpawnDecisionInterceptor { get => DecisionGate.SpawnDecisionInterceptor; set => DecisionGate.SpawnDecisionInterceptor = value; }

    public Func<WorldDamageDecisionRequest, WorldDecisionResult>? DamageDecisionInterceptor { get => DecisionGate.DamageDecisionInterceptor; set => DecisionGate.DamageDecisionInterceptor = value; }

    public Func<WorldDeathDecisionRequest, WorldDecisionResult>? DeathDecisionInterceptor { get => DecisionGate.DeathDecisionInterceptor; set => DecisionGate.DeathDecisionInterceptor = value; }

    public Func<WorldPickupDecisionRequest, WorldDecisionResult>? PickupDecisionInterceptor { get => DecisionGate.PickupDecisionInterceptor; set => DecisionGate.PickupDecisionInterceptor = value; }

    public Func<WorldScoreDecisionRequest, WorldDecisionResult>? ScoreDecisionInterceptor { get => DecisionGate.ScoreDecisionInterceptor; set => DecisionGate.ScoreDecisionInterceptor = value; }

    public Func<WorldRoundEndDecisionRequest, WorldDecisionResult>? RoundEndDecisionInterceptor { get => DecisionGate.RoundEndDecisionInterceptor; set => DecisionGate.RoundEndDecisionInterceptor = value; }

    private bool ShouldCancelDamage(DamageTargetKind targetKind, int targetEntityId, int targetPlayerId, PlayerTeam? targetTeam, PlayerEntity? attacker, int amount, bool wouldBeFatal, float x, float y)
        => DecisionGate.ShouldCancelDamage(targetKind, targetEntityId, targetPlayerId, targetTeam, attacker, amount, wouldBeFatal, x, y);
    private bool ShouldCancelDeath(PlayerEntity player, bool gibbed, PlayerEntity? killer, string? weaponSpriteName)
        => DecisionGate.ShouldCancelDeath(player, gibbed, killer, weaponSpriteName);
    private bool TryEndRound(PlayerTeam? winnerTeam, string reason)
        => DecisionGate.TryEndRound(winnerTeam, reason);
}
