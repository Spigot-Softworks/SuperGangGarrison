using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private bool TryExplodeLastToDieMedicJavelin(
        MedicHealNeedleProjectileEntity needle)
    {
        if (!needle.TryMarkLastToDieJavelinExploded())
        {
            return false;
        }

        var explosionX = needle.X;
        var explosionY = needle.Y;
        var blastRadius = ResolveExplosiveSplashRadius(
            LastToDieDerivedModifiers.MedicJavelinBlastRadius);
        RegisterWorldSoundEvent("ExplosionSnd", explosionX, explosionY, needle.OwnerId);
        RegisterVisualEffect("Explosion", explosionX, explosionY);
        if (ClientPredictionMode)
        {
            return true;
        }

        var owner = FindPlayerById(needle.OwnerId);
        foreach (var target in EnumerateSimulatedPlayers().ToArray())
        {
            if (!target.IsAlive || target.Id == needle.OwnerId)
            {
                continue;
            }

            var distance = GetExplosionDistanceToPlayer(
                this,
                target,
                explosionX,
                explosionY);
            if (distance > blastRadius)
            {
                continue;
            }

            GetExplosionDirection(
                target,
                explosionX,
                explosionY,
                out var targetCenterDeltaX,
                out var targetCenterDeltaY,
                out _);
            if (!HasObstacleLineOfSight(
                    explosionX,
                    explosionY,
                    explosionX + targetCenterDeltaX,
                    explosionY + targetCenterDeltaY))
            {
                continue;
            }

            var distanceFraction = Math.Clamp(
                distance / blastRadius,
                0f,
                1f);
            if (target.Team == needle.Team)
            {
                ApplyLastToDieMedicJavelinAllyEffect(
                    needle,
                    owner,
                    target,
                    explosionX,
                    explosionY,
                    distanceFraction);
                continue;
            }

            ApplyLastToDieMedicJavelinEnemyEffect(
                needle,
                owner,
                target,
                explosionX,
                explosionY,
                distanceFraction);
        }

        return true;
    }

    private void ApplyLastToDieMedicJavelinAllyEffect(
        MedicHealNeedleProjectileEntity needle,
        PlayerEntity? owner,
        PlayerEntity target,
        float explosionX,
        float explosionY,
        float distanceFraction)
    {
        if (needle.AppliesLastToDieHailMary)
        {
            _ = target.RefreshLastToDieMedicHailMaryInvulnerability(
                Math.Max(
                    1,
                    (int)Math.Ceiling(
                        LastToDieDerivedModifiers.MedicHailMaryInvulnerabilitySeconds
                            * Math.Max(1, Config.TicksPerSecond))));
        }

        var healing = LastToDieDerivedModifiers.MedicJavelinAllyCenterHealing
            + ((LastToDieDerivedModifiers.MedicJavelinAllyEdgeHealing
                - LastToDieDerivedModifiers.MedicJavelinAllyCenterHealing)
                * distanceFraction);
        var appliedHealing = ApplyHealingWithFeedback(
            target,
            Math.Max(0f, healing),
            "HealSnd",
            explosionX,
            explosionY);
        if (appliedHealing <= 0
            || owner is not
            {
                IsAlive: true,
                ClassId: PlayerClass.Medic,
            }
            || owner.Team != needle.Team)
        {
            return;
        }

        AwardHealingPoints(owner, appliedHealing);
        ApplyLastToDieMedicHomeostasis(owner, appliedHealing);
    }

    private void ApplyLastToDieMedicJavelinEnemyEffect(
        MedicHealNeedleProjectileEntity needle,
        PlayerEntity? owner,
        PlayerEntity target,
        float explosionX,
        float explosionY,
        float distanceFraction)
    {
        var damage = LastToDieDerivedModifiers.MedicJavelinEnemyCenterDamage
            + ((LastToDieDerivedModifiers.MedicJavelinEnemyEdgeDamage
                - LastToDieDerivedModifiers.MedicJavelinEnemyCenterDamage)
                * distanceFraction);
        damage *= needle.CriticalDamageMultiplier;
        if (needle.AppliesLastToDieNeurotoxin && target.IsServerStunned)
        {
            damage *= LastToDieDerivedModifiers.MedicNeurotoxinPreStunnedDamageMultiplier;
        }

        damage = MathF.Max(ExplosiveSplashMinimumDamage, damage);

        var resolution = ResolvePlayerDamageWithContext(
            target,
            Math.Max(1, (int)MathF.Round(damage)),
            owner,
            PlayerEntity.SpyDamageRevealAlpha,
            DamageEventFlags.None,
            civvieUmbrellaThreatSourceX: explosionX,
            civvieUmbrellaThreatSourceY: explosionY,
            civvieUmbrellaCriticalBoost: needle.IsCritical,
            civvieUmbrellaUseLiveAttackerCriticalBoost: false,
            additionalTraits: PlayerDamageTraits.DirectProjectile
                | PlayerDamageTraits.MedicKritzM2
                | PlayerDamageTraits.Explosive,
            attackerWasGrounded: owner?.IsGrounded,
            targetWasGrounded: target.IsGrounded,
            sourceEntityId: needle.Id,
            attackId: unchecked((ulong)(uint)needle.Id),
            attackerPlayerIdOverride: needle.OwnerId);
        if (resolution.ShouldApplyOnHitEffects
            && needle.AppliesLastToDieNeurotoxin)
        {
            _ = TryApplyLastToDieStatusEffect(
                target.Id,
                needle.OwnerId,
                LastToDieStatusEffectSpec.Stun(
                    LastToDieStatusEffectIds.MedicNeurotoxinStun,
                    LastToDieDerivedModifiers.MedicNeurotoxinStunSeconds
                        * Math.Max(1, Config.TicksPerSecond)));
        }

        if (resolution.WasFatal)
        {
            KillPlayer(target, killer: owner, weaponSpriteName: "NeedleKL");
        }
    }
}
