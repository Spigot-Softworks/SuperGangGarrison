namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="CombatFeedbackSystem"/> needs from the world coordinator.
/// </summary>
internal interface ICombatFeedbackHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    CombatRuntimeState CombatRuntime { get; }
    KillFeedSystem KillFeedRules { get; }
    LastToDieRulesSystem LastToDieRules { get; }
    PlayerEntity LocalPlayer { get; }
    NetworkPlayerSystem NetworkPlayerRules { get; }
    SimulationRandomStreams Randoms { get; }

    bool IsNetworkPlayerActive(byte slot);
    void SpawnFlame(
        PlayerEntity owner,
        float x,
        float y,
        float velocityX,
        float velocityY,
        float directHitDamage = FlameProjectileEntity.DirectHitDamage,
        float burnDamagePerTick = FlameProjectileEntity.BurnDamagePerTick);
}
