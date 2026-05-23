namespace OpenGarrison.GameplayModding;

public sealed record GameplayItemCombatDefinition(
    string? FireSoundName = null,
    float? DirectHitDamage = null,
    float? DamagePerTick = null,
    float? DirectHitHealAmount = null,
    int? ActiveProjectileLimit = null,
    GameplayRocketCombatDefinition? Rocket = null,
    float? PlayerKnockbackScale = null,
    float? PlayerSlowMovementMultiplier = null,
    int? PlayerSlowRefreshSourceTicks = null);
