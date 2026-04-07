namespace OpenGarrison.GameplayModding;

public sealed record GameplayItemCombatDefinition(
    string? FireSoundName = null,
    float? DirectHitDamage = null,
    float? DamagePerTick = null,
    float? DirectHitHealAmount = null,
    GameplayRocketCombatDefinition? Rocket = null);
