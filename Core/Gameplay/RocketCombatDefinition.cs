namespace OpenGarrison.Core;

public sealed record RocketCombatDefinition(
    int DirectHitDamage = RocketProjectileEntity.DirectHitDamage,
    float ExplosionDamage = RocketProjectileEntity.ExplosionDamage,
    float BlastRadius = RocketProjectileEntity.BlastRadius,
    float SplashThresholdFactor = RocketProjectileEntity.SplashThresholdFactor);
