namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void AdvanceShots()
    {
        for (var shotIndex = _shots.Count - 1; shotIndex >= 0; shotIndex -= 1)
        {
            var shot = _shots[shotIndex];
            if (!ShouldAdvanceProjectileForClientPrediction(shot.OwnerId))
            {
                continue;
            }

            shot.AdvanceOneTick(_configuredGravityScale);
            var movementX = shot.X - shot.PreviousX;
            var movementY = shot.Y - shot.PreviousY;
            var movementDistance = MathF.Sqrt((movementX * movementX) + (movementY * movementY));
            if (movementDistance <= 0.0001f)
            {
                if (shot.IsExpired)
                {
                    RemoveShotAt(shotIndex);
                }

                continue;
            }

            var directionX = movementX / movementDistance;
            var directionY = movementY / movementDistance;
            var hit = GetNearestShotHit(shot, directionX, directionY, movementDistance);
            if (hit.HasValue)
            {
                var hitResult = hit.Value;
                var owner = FindPlayerById(shot.OwnerId);
                var sourceSentry = TryFindExperimentalSentryShotSource(shot);
                shot.MoveTo(hitResult.HitX, hitResult.HitY);
                RegisterCombatTrace(shot.PreviousX, shot.PreviousY, directionX, directionY, hitResult.Distance, hitResult.HitPlayer is not null);
                if (hitResult.HitPlayer is not null)
                {
                    RegisterBloodEffect(hitResult.HitPlayer.X, hitResult.HitPlayer.Y, MathF.Atan2(directionY, directionX) * (180f / MathF.PI) - 180f);
                    if (sourceSentry is not null && owner is not null)
                    {
                        ApplyExperimentalSentryPlayerHit(
                            sourceSentry,
                            owner,
                            hitResult.HitPlayer,
                            (int)MathF.Round(shot.DamageValue * shot.CriticalDamageMultiplier));
                    }
                    else
                    {
                        if (!hitResult.HitPlayer.IsUbered)
                        {
                            var bulletKnockbackPerSecond = 0.5f * LegacyMovementModel.SourceTicksPerSecond;
                            if (shot.PlayerKnockbackScale > 0f)
                            {
                                hitResult.HitPlayer.AddImpulse(
                                    directionX * bulletKnockbackPerSecond * shot.PlayerKnockbackScale,
                                    directionY * bulletKnockbackPerSecond * shot.PlayerKnockbackScale);
                            }

                            if (shot.PlayerSlowMovementMultiplier.HasValue && shot.PlayerSlowRefreshTicks > 0)
                            {
                                hitResult.HitPlayer.RefreshDirectFireSlow(
                                    shot.PlayerSlowRefreshTicks,
                                    shot.PlayerSlowMovementMultiplier.Value);
                            }
                        }

                        var hitDamage = ApplyExperimentalAirshotDamageMultiplier(owner, hitResult.HitPlayer, (int)MathF.Round(shot.DamageValue * shot.CriticalDamageMultiplier), out var damageFlags);
                        if (ApplyPlayerDamage(
                                hitResult.HitPlayer,
                                hitDamage,
                                owner,
                                PlayerEntity.SpyDamageRevealAlpha,
                                damageFlags,
                                civvieUmbrellaCriticalBoost: PlayerEntity.IsCriticalDamageMultiplierBoosted(shot.CriticalDamageMultiplier)))
                        {
                            KillPlayer(
                                hitResult.HitPlayer,
                                gibbed: shot.ForceGibOnKill,
                                killer: owner,
                                weaponSpriteName: shot.KillFeedWeaponSpriteNameOverride ?? GetKillFeedWeaponSprite(owner));
                        }
                    }
                }
                else if (hitResult.HitSentry is not null)
                {
                    var sentryHealthBefore = hitResult.HitSentry.Health;
                    if (ApplySentryDamage(hitResult.HitSentry, (int)MathF.Round(shot.DamageValue * shot.CriticalDamageMultiplier), owner))
                    {
                        DestroySentry(hitResult.HitSentry, owner);
                    }

                    if (sourceSentry is not null && owner is not null)
                    {
                        ApplyExperimentalSentryDamageRewards(
                            sourceSentry,
                            owner,
                            Math.Max(0, sentryHealthBefore - hitResult.HitSentry.Health));
                    }
                }
                else if (hitResult.HitGenerator is not null)
                {
                    TryDamageGenerator(hitResult.HitGenerator.Team, shot.DamageValue * shot.CriticalDamageMultiplier, owner);
                }
                else if (hitResult.HitJumpPad is not null)
                {
                    hitResult.HitJumpPad.TakeDamage((int)MathF.Round(shot.DamageValue * shot.CriticalDamageMultiplier));
                    RegisterImpactEffect(hitResult.HitX, hitResult.HitY, MathF.Atan2(directionY, directionX) * (180f / MathF.PI));
                }
                else if (TryHandleProjectileDamageableZoneHit(hitResult, shot.DamageValue * shot.CriticalDamageMultiplier, shot.Team))
                {
                    RegisterImpactEffect(hitResult.HitX, hitResult.HitY, MathF.Atan2(directionY, directionX) * (180f / MathF.PI));
                }
                else
                {
                    RegisterImpactEffect(hitResult.HitX, hitResult.HitY, MathF.Atan2(directionY, directionX) * (180f / MathF.PI));
                }

                shot.Destroy();
            }
            else
            {
                RegisterCombatTrace(shot.PreviousX, shot.PreviousY, directionX, directionY, movementDistance, false);
            }

            if (shot.IsExpired)
            {
                RemoveShotAt(shotIndex);
            }
        }
    }

    private SentryEntity? TryFindExperimentalSentryShotSource(ShotProjectileEntity shot)
    {
        if (!shot.ApplyExperimentalEngineerSentryPerkEffects || !shot.SourceSentryId.HasValue)
        {
            return null;
        }

        for (var sentryIndex = 0; sentryIndex < _sentries.Count; sentryIndex += 1)
        {
            if (_sentries[sentryIndex].Id == shot.SourceSentryId.Value)
            {
                return _sentries[sentryIndex];
            }
        }

        return null;
    }

    private void AdvanceBlades()
    {
        for (var bladeIndex = _blades.Count - 1; bladeIndex >= 0; bladeIndex -= 1)
        {
            var blade = _blades[bladeIndex];
            if (!ShouldAdvanceProjectileForClientPrediction(blade.OwnerId))
            {
                continue;
            }

            blade.AdvanceOneTick();
            var movementX = blade.X - blade.PreviousX;
            var movementY = blade.Y - blade.PreviousY;
            var movementDistance = MathF.Sqrt((movementX * movementX) + (movementY * movementY));
            if (movementDistance > 0.0001f)
            {
                var directionX = movementX / movementDistance;
                var directionY = movementY / movementDistance;
                var hit = GetNearestBladeHit(blade, directionX, directionY, movementDistance);
                if (hit.HasValue)
                {
                    var hitResult = hit.Value;
                    var owner = FindPlayerById(blade.OwnerId);
                    blade.MoveTo(hitResult.HitX, hitResult.HitY);
                    RegisterCombatTrace(blade.PreviousX, blade.PreviousY, directionX, directionY, hitResult.Distance, hitResult.HitPlayer is not null);
                    if (hitResult.HitPlayer is not null)
                    {
                        RegisterBloodEffect(hitResult.HitPlayer.X, hitResult.HitPlayer.Y, MathF.Atan2(directionY, directionX) * (180f / MathF.PI) - 180f, 6);
                        if (!hitResult.HitPlayer.IsUbered)
                        {
                            hitResult.HitPlayer.AddImpulse(
                                blade.VelocityX * 0.4f * LegacyMovementModel.SourceTicksPerSecond,
                                blade.VelocityY * 0.4f * LegacyMovementModel.SourceTicksPerSecond);
                        }
                        var hitDamage = ApplyExperimentalAirshotDamageMultiplier(owner, hitResult.HitPlayer, (int)MathF.Round(blade.HitDamage * blade.CriticalDamageMultiplier), out var damageFlags);
                        if (ApplyPlayerDamage(
                                hitResult.HitPlayer,
                                hitDamage,
                                owner,
                                PlayerEntity.SpyDamageRevealAlpha,
                                damageFlags,
                                civvieUmbrellaCriticalBoost: PlayerEntity.IsCriticalDamageMultiplierBoosted(blade.CriticalDamageMultiplier)))
                        {
                            KillPlayer(hitResult.HitPlayer, killer: owner, weaponSpriteName: "BladeKL");
                        }
                    }
                    else if (hitResult.HitSentry is not null && ApplySentryDamage(hitResult.HitSentry, (int)MathF.Round(blade.HitDamage * blade.CriticalDamageMultiplier), owner))
                    {
                        DestroySentry(hitResult.HitSentry, owner);
                    }
                    else if (hitResult.HitGenerator is not null)
                    {
                        TryDamageGenerator(hitResult.HitGenerator.Team, blade.HitDamage * blade.CriticalDamageMultiplier, owner);
                    }
                    else if (hitResult.HitJumpPad is not null)
                    {
                        hitResult.HitJumpPad.TakeDamage((int)MathF.Round(blade.HitDamage * blade.CriticalDamageMultiplier));
                    }
                    else if (TryHandleProjectileDamageableZoneHit(hitResult, blade.HitDamage * blade.CriticalDamageMultiplier, blade.Team))
                    {
                        RegisterImpactEffect(hitResult.HitX, hitResult.HitY, MathF.Atan2(directionY, directionX) * (180f / MathF.PI));
                    }
                    else
                    {
                        RegisterImpactEffect(hitResult.HitX, hitResult.HitY, MathF.Atan2(directionY, directionX) * (180f / MathF.PI));
                    }

                    blade.Destroy();
                }
            }

            if (TryCutBubbleWithBlade(blade))
            {
                continue;
            }

            if (blade.IsExpired)
            {
                RemoveBladeAt(bladeIndex);
            }
        }
    }

    private void AdvanceNeedles()
    {
        for (var needleIndex = _needles.Count - 1; needleIndex >= 0; needleIndex -= 1)
        {
            var needle = _needles[needleIndex];
            if (!ShouldAdvanceProjectileForClientPrediction(needle.OwnerId))
            {
                continue;
            }

            needle.AdvanceOneTick(_configuredGravityScale);
            var movementX = needle.X - needle.PreviousX;
            var movementY = needle.Y - needle.PreviousY;
            var movementDistance = MathF.Sqrt((movementX * movementX) + (movementY * movementY));
            if (movementDistance <= 0.0001f)
            {
                if (needle.IsExpired)
                {
                    RemoveNeedleAt(needleIndex);
                }

                continue;
            }

            var directionX = movementX / movementDistance;
            var directionY = movementY / movementDistance;
            var hit = needle is MedicHealNeedleProjectileEntity healNeedle
                ? GetNearestMedicHealNeedleHit(healNeedle, directionX, directionY, movementDistance)
                : GetNearestNeedleHit(needle, directionX, directionY, movementDistance);
            if (hit.HasValue)
            {
                var hitResult = hit.Value;
                var owner = FindPlayerById(needle.OwnerId);
                needle.MoveTo(hitResult.HitX, hitResult.HitY);
                RegisterCombatTrace(needle.PreviousX, needle.PreviousY, directionX, directionY, hitResult.Distance, hitResult.HitPlayer is not null);
                if (hitResult.HitPlayer is not null
                    && needle is MedicHealNeedleProjectileEntity medicHealNeedle
                    && owner is not null
                    && hitResult.HitPlayer.Team == owner.Team)
                {
                    ApplyMedicHealNeedleTeammateHit(owner, hitResult.HitPlayer, medicHealNeedle);
                }
                else if (hitResult.HitPlayer is not null)
                {
                    RegisterBloodEffect(hitResult.HitPlayer.X, hitResult.HitPlayer.Y, MathF.Atan2(directionY, directionX) * (180f / MathF.PI) - 180f);
                    var hitDamage = ApplyExperimentalAirshotDamageMultiplier(owner, hitResult.HitPlayer, (int)MathF.Round(needle.Damage * needle.CriticalDamageMultiplier), out var damageFlags);
                    if (ApplyPlayerDamage(
                            hitResult.HitPlayer,
                            hitDamage,
                            owner,
                            PlayerEntity.SpyDamageRevealAlpha,
                            damageFlags,
                            civvieUmbrellaCriticalBoost: PlayerEntity.IsCriticalDamageMultiplierBoosted(needle.CriticalDamageMultiplier)))
                    {
                        var killFeedSprite = needle is NailProjectileEntity ? "NailgunKL" : needle is MedicHealNeedleProjectileEntity ? "NeedleKL" : "NeedleKL";
                        KillPlayer(hitResult.HitPlayer, killer: owner, weaponSpriteName: killFeedSprite);
                    }
                }
                else if (hitResult.HitSentry is not null && ApplySentryDamage(hitResult.HitSentry, (int)MathF.Round(needle.Damage * needle.CriticalDamageMultiplier), owner))
                {
                    DestroySentry(hitResult.HitSentry, owner);
                }
                else if (hitResult.HitGenerator is not null)
                {
                    TryDamageGenerator(hitResult.HitGenerator.Team, needle.Damage * needle.CriticalDamageMultiplier, owner);
                }
                else if (hitResult.HitJumpPad is not null)
                {
                    hitResult.HitJumpPad.TakeDamage((int)MathF.Round(needle.Damage * needle.CriticalDamageMultiplier));
                }
                else if (TryHandleProjectileDamageableZoneHit(hitResult, needle.Damage * needle.CriticalDamageMultiplier, needle.Team))
                {
                    RegisterImpactEffect(hitResult.HitX, hitResult.HitY, MathF.Atan2(directionY, directionX) * (180f / MathF.PI));
                }
                else
                {
                    RegisterImpactEffect(hitResult.HitX, hitResult.HitY, MathF.Atan2(directionY, directionX) * (180f / MathF.PI));
                }

                needle.Destroy();
            }
            else
            {
                RegisterCombatTrace(needle.PreviousX, needle.PreviousY, directionX, directionY, movementDistance, false);
            }

            if (needle.IsExpired)
            {
                RemoveNeedleAt(needleIndex);
            }
        }
    }

    private void AdvanceRevolverShots()
    {
        for (var shotIndex = _revolverShots.Count - 1; shotIndex >= 0; shotIndex -= 1)
        {
            var shot = _revolverShots[shotIndex];
            if (!ShouldAdvanceProjectileForClientPrediction(shot.OwnerId))
            {
                continue;
            }

            shot.AdvanceOneTick(_configuredGravityScale);
            var movementX = shot.X - shot.PreviousX;
            var movementY = shot.Y - shot.PreviousY;
            var movementDistance = MathF.Sqrt((movementX * movementX) + (movementY * movementY));
            if (movementDistance <= 0.0001f)
            {
                if (shot.IsExpired)
                {
                    RemoveRevolverShotAt(shotIndex);
                }

                continue;
            }

            var directionX = movementX / movementDistance;
            var directionY = movementY / movementDistance;
            var hit = GetNearestRevolverHit(shot, directionX, directionY, movementDistance);
            if (hit.HasValue)
            {
                var hitResult = hit.Value;
                var owner = FindPlayerById(shot.OwnerId);
                shot.MoveTo(hitResult.HitX, hitResult.HitY);
                RegisterCombatTrace(shot.PreviousX, shot.PreviousY, directionX, directionY, hitResult.Distance, hitResult.HitPlayer is not null);
                if (hitResult.HitPlayer is not null)
                {
                    RegisterBloodEffect(hitResult.HitPlayer.X, hitResult.HitPlayer.Y, MathF.Atan2(directionY, directionX) * (180f / MathF.PI) - 180f);
                    var hitDamage = ApplyExperimentalAirshotDamageMultiplier(owner, hitResult.HitPlayer, (int)MathF.Round(shot.DamageValue * shot.CriticalDamageMultiplier), out var damageFlags);
                    if (ApplyPlayerDamage(
                            hitResult.HitPlayer,
                            hitDamage,
                            owner,
                            PlayerEntity.SpyDamageRevealAlpha,
                            damageFlags,
                            civvieUmbrellaCriticalBoost: PlayerEntity.IsCriticalDamageMultiplierBoosted(shot.CriticalDamageMultiplier)))
                    {
                        KillPlayer(
                            hitResult.HitPlayer,
                            killer: owner,
                            weaponSpriteName: shot.KillFeedWeaponSpriteNameOverride ?? "RevolverKL");
                    }
                }
                else if (hitResult.HitSentry is not null && ApplySentryDamage(hitResult.HitSentry, (int)MathF.Round(shot.DamageValue * shot.CriticalDamageMultiplier), owner))
                {
                    DestroySentry(hitResult.HitSentry, owner);
                }
                else if (hitResult.HitGenerator is not null)
                {
                    TryDamageGenerator(hitResult.HitGenerator.Team, shot.DamageValue * shot.CriticalDamageMultiplier, owner);
                }
                else if (hitResult.HitJumpPad is not null)
                {
                    hitResult.HitJumpPad.TakeDamage((int)MathF.Round(shot.DamageValue * shot.CriticalDamageMultiplier));
                    RegisterImpactEffect(hitResult.HitX, hitResult.HitY, MathF.Atan2(directionY, directionX) * (180f / MathF.PI));
                }
                else if (TryHandleProjectileDamageableZoneHit(hitResult, shot.DamageValue * shot.CriticalDamageMultiplier, shot.Team))
                {
                    RegisterImpactEffect(hitResult.HitX, hitResult.HitY, MathF.Atan2(directionY, directionX) * (180f / MathF.PI));
                }
                else
                {
                    RegisterImpactEffect(hitResult.HitX, hitResult.HitY, MathF.Atan2(directionY, directionX) * (180f / MathF.PI));
                }

                shot.Destroy();
            }
            else
            {
                RegisterCombatTrace(shot.PreviousX, shot.PreviousY, directionX, directionY, movementDistance, false);
            }

            if (shot.IsExpired)
            {
                RemoveRevolverShotAt(shotIndex);
            }
        }
    }
}
