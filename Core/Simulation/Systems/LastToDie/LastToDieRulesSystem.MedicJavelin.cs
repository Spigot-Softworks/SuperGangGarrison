using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

internal sealed partial class LastToDieRulesSystem
{
    internal bool TryExplodeLastToDieMedicJavelin(
        MedicHealNeedleProjectileEntity needle)
    {
        if (!needle.TryMarkLastToDieJavelinExploded())
        {
            return false;
        }

        var explosionX = needle.X;
        var explosionY = needle.Y;
        var owner = _host.FindPlayerById(needle.OwnerId);
        var blastRadius = CombatSystem.ResolveExplosiveSplashRadius(
            LastToDieDerivedModifiers.MedicJavelinBlastRadius
                * MathF.Max(0.1f, owner?.LastToDieUniversalModifiers.ExplosionScale ?? 1f));
        _host.RegisterWorldSoundEvent("ExplosionSnd", explosionX, explosionY, needle.OwnerId);
        _host.RegisterVisualEffect("Explosion", explosionX, explosionY, 0f, 1, true);
        if (_host.ClientPredictionMode)
        {
            return true;
        }

        foreach (var target in _host.EnumerateSimulatedPlayers().ToArray())
        {
            if (!target.IsAlive || target.Id == needle.OwnerId)
            {
                continue;
            }

            var distance = GetExplosionDistanceToPlayer(target,
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
            if (!_host.GeometryResolver.HasObstacleLineOfSight(
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
                            * Math.Max(1, _host.Config.TicksPerSecond))));
        }

        var healing = LastToDieDerivedModifiers.MedicJavelinAllyCenterHealing
            + ((LastToDieDerivedModifiers.MedicJavelinAllyEdgeHealing
                - LastToDieDerivedModifiers.MedicJavelinAllyCenterHealing)
                * distanceFraction);
        var appliedHealing = _host.DamageRules.ApplyHealingWithFeedback(
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

        _host.Scorekeeping.AwardHealingPoints(owner, appliedHealing);
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

        damage = MathF.Max(CombatSystem.ExplosiveSplashMinimumDamage, damage);

        var resolution = _host.ResolvePlayerDamageWithContext(
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
                        * Math.Max(1, _host.Config.TicksPerSecond)));
        }

        if (resolution.WasFatal)
        {
            _host.PlayerDeaths.KillPlayer(target, killer: owner, weaponSpriteName: "NeedleKL");
        }
    }
}
