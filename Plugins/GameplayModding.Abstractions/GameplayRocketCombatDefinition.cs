namespace OpenGarrison.GameplayModding;

public sealed record GameplayRocketCombatDefinition(
    int DirectHitDamage = 25,
    float ExplosionDamage = 30f,
    float BlastRadius = 65f,
    float SplashThresholdFactor = 0.25f);
