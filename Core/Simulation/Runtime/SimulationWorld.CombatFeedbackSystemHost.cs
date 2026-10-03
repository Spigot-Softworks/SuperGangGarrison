namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : ICombatFeedbackHost
{
    CombatRuntimeState ICombatFeedbackHost.CombatRuntime => CombatRuntime;
    bool ICombatFeedbackHost.IsNetworkPlayerActive(byte slot)
        => IsNetworkPlayerActive(slot);
    KillFeedSystem ICombatFeedbackHost.KillFeed => KillFeed;
    LastToDieRulesSystem ICombatFeedbackHost.LastToDieRules => LastToDieRules;
    PlayerEntity ICombatFeedbackHost.LocalPlayer => LocalPlayer;
    NetworkPlayerSystem ICombatFeedbackHost.NetworkPlayers => NetworkPlayers;
    SimulationRandomStreams ICombatFeedbackHost.Randoms => Randoms;
    void ICombatFeedbackHost.SpawnFlame(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float directHitDamage, float burnDamagePerTick)
        => Projectiles.SpawnFlame(owner, x, y, velocityX, velocityY, directHitDamage, burnDamagePerTick);
}
