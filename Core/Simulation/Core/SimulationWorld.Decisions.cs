namespace OpenGarrison.Core;

// The plugin decision surface. Interceptor state lives on DecisionGate; these
// properties are the public API the server plugin host and tests set.
public sealed partial class SimulationWorld
{
    public Func<WorldSpawnDecisionRequest, WorldDecisionResult>? SpawnDecisionInterceptor { get => Decisions.SpawnDecisionInterceptor; set => Decisions.SpawnDecisionInterceptor = value; }

    public Func<WorldDamageDecisionRequest, WorldDecisionResult>? DamageDecisionInterceptor { get => Decisions.DamageDecisionInterceptor; set => Decisions.DamageDecisionInterceptor = value; }

    public Func<WorldDeathDecisionRequest, WorldDecisionResult>? DeathDecisionInterceptor { get => Decisions.DeathDecisionInterceptor; set => Decisions.DeathDecisionInterceptor = value; }

    public Func<WorldPickupDecisionRequest, WorldDecisionResult>? PickupDecisionInterceptor { get => Decisions.PickupDecisionInterceptor; set => Decisions.PickupDecisionInterceptor = value; }

    public Func<WorldScoreDecisionRequest, WorldDecisionResult>? ScoreDecisionInterceptor { get => Decisions.ScoreDecisionInterceptor; set => Decisions.ScoreDecisionInterceptor = value; }

    public Func<WorldRoundEndDecisionRequest, WorldDecisionResult>? RoundEndDecisionInterceptor { get => Decisions.RoundEndDecisionInterceptor; set => Decisions.RoundEndDecisionInterceptor = value; }

    private bool ShouldCancelDamage(DamageTargetKind targetKind, int targetEntityId, int targetPlayerId, PlayerTeam? targetTeam, PlayerEntity? attacker, int amount, bool wouldBeFatal, float x, float y)
        => Decisions.ShouldCancelDamage(targetKind, targetEntityId, targetPlayerId, targetTeam, attacker, amount, wouldBeFatal, x, y);
    private bool ShouldCancelDeath(PlayerEntity player, bool gibbed, PlayerEntity? killer, string? weaponSpriteName)
        => Decisions.ShouldCancelDeath(player, gibbed, killer, weaponSpriteName);
    private bool TryEndRound(PlayerTeam? winnerTeam, string reason)
        => Decisions.TryEndRound(winnerTeam, reason);
}
