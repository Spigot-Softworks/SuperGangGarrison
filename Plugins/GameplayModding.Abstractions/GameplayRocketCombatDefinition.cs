namespace OpenGarrison.GameplayModding;

/// <summary>
/// Rocket combat behavior for a gameplay item.
/// </summary>
/// <param name="DirectHitDamage">The direct hit damage.</param>
/// <param name="ExplosionDamage">The explosion damage.</param>
/// <param name="BlastRadius">The blast radius.</param>
/// <param name="SplashThresholdFactor">The splash threshold factor.</param>
/// <param name="MinimumSplashDamage">The minimum splash damage.</param>
/// <param name="SelfDamageMultiplier">The self damage multiplier.</param>
public sealed record GameplayRocketCombatDefinition(
    int DirectHitDamage = 25,
    float ExplosionDamage = 30f,
    float BlastRadius = 65f,
    float SplashThresholdFactor = 0.25f,
    float MinimumSplashDamage = 25f,
    float SelfDamageMultiplier = 1f);
