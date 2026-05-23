namespace OpenGarrison.GameplayModding;

public sealed record GameplayItemCombatDefinition(
    string? FireSoundName = null,
    float? DirectHitDamage = null,
    float? DamagePerTick = null,
    float? DirectHitHealAmount = null,
    int? ActiveProjectileLimit = null,
    GameplayRocketCombatDefinition? Rocket = null);
