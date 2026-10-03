namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : ICombatFeedbackHost
{
    CombatRuntimeState ICombatFeedbackHost.CombatRuntime => CombatRuntime;
    bool ICombatFeedbackHost.IsNetworkPlayerActive(byte slot)
        => IsNetworkPlayerActive(slot);
    KillFeedSystem ICombatFeedbackHost.KillFeedRules => KillFeedRules;
    LastToDieRulesSystem ICombatFeedbackHost.LastToDieRules => LastToDieRules;
    PlayerEntity ICombatFeedbackHost.LocalPlayer => LocalPlayer;
    NetworkPlayerSystem ICombatFeedbackHost.NetworkPlayerRules => NetworkPlayerRules;
    SimulationRandomStreams ICombatFeedbackHost.Randoms => Randoms;
    void ICombatFeedbackHost.SpawnFlame(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float directHitDamage, float burnDamagePerTick)
        => SpawnFlame(owner, x, y, velocityX, velocityY, directHitDamage, burnDamagePerTick);
}
