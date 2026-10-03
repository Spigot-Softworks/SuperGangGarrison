namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public const float BoomstickExplosionDamage = 25f;
    public const float BoomstickBlastRadius = RocketProjectileEntity.BlastRadius * 0.25f;

    internal void ExplodeBoomstickPellet(ShotProjectileEntity shot)
    {
        // The shot already applied its five direct damage. Reuse rocket splash
        // handling for knockback, buildings, prediction, and replicated effects.
        var blast = new RocketProjectileEntity(
            shot.Id,
            shot.Team,
            shot.OwnerId,
            shot.X,
            shot.Y,
            0f,
            0f,
            new RocketCombatDefinition(
                DirectHitDamage: 0,
                ExplosionDamage: BoomstickExplosionDamage,
                BlastRadius: BoomstickBlastRadius,
                MinimumSplashDamage: 0f),
            killFeedWeaponSpriteNameOverride: shot.KillFeedWeaponSpriteNameOverride);
        ExplosionRules.ExplodeRocket(blast, directHitPlayer: null, directHitSentry: null, directHitGenerator: null);
    }
}
