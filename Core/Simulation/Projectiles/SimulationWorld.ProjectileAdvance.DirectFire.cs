using OpenGarrison.Core.LastToDie;

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

            shot.AdvanceOneTick(ResolveProjectileGravityScale());
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
                    var targetWasGrounded = hitResult.HitPlayer.IsGrounded;
                    RegisterBloodEffect(hitResult.HitPlayer.X, hitResult.HitPlayer.Y, MathF.Atan2(directionY, directionX) * (180f / MathF.PI) - 180f);
                    if (sourceSentry is not null && owner is not null)
                    {
                        ApplyExperimentalSentryPlayerHit(
                            sourceSentry,
                            owner,
                            hitResult.HitPlayer,
                            (int)MathF.Round(shot.DamageValue * shot.CriticalDamageMultiplier),
                            PlayerDamageTraits.DirectProjectile,
                            criticalBoost: PlayerEntity.IsCriticalDamageMultiplierBoosted(shot.CriticalDamageMultiplier),
                            useLiveAttackerCriticalBoost: false,
                            threatSourceX: shot.PreviousX,
                            threatSourceY: shot.PreviousY,
                            knockbackPayload: shot.PlayerKnockbackPayload,
                            impactDirectionX: directionX,
                            impactDirectionY: directionY);
                    }
                    else
                    {
                        var hitDamage = ApplyExperimentalAirshotDamageMultiplier(owner, hitResult.HitPlayer, (int)MathF.Round(shot.DamageValue * shot.CriticalDamageMultiplier), out var damageFlags);
                        var resolution = ResolvePlayerDamageWithContext(
                                hitResult.HitPlayer,
                                hitDamage,
                                owner,
                                PlayerEntity.SpyDamageRevealAlpha,
                                damageFlags,
                                civvieUmbrellaThreatSourceX: shot.PreviousX,
                                civvieUmbrellaThreatSourceY: shot.PreviousY,
                                civvieUmbrellaCriticalBoost: PlayerEntity.IsCriticalDamageMultiplierBoosted(shot.CriticalDamageMultiplier),
                                civvieUmbrellaUseLiveAttackerCriticalBoost: false,
                                additionalTraits: PlayerDamageTraits.Bullet | PlayerDamageTraits.DirectProjectile,
                                targetWasGrounded: targetWasGrounded);
                        if (resolution.ShouldApplyOnHitEffects && hitResult.HitPlayer.IsAlive)
                        {
                            BulletKnockbackRules.Apply(
                                hitResult.HitPlayer,
                                directionX,
                                directionY,
                                shot.PlayerKnockbackPayload);
                            if (shot.PlayerSlowMovementMultiplier.HasValue && shot.PlayerSlowRefreshTicks > 0)
                            {
                                hitResult.HitPlayer.RefreshDirectFireSlow(
                                    shot.PlayerSlowRefreshTicks,
                                    shot.PlayerSlowMovementMultiplier.Value);
                            }
                        }

                        if (resolution.WasFatal)
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
                        var targetWasGrounded = hitResult.HitPlayer.IsGrounded;
                        RegisterBloodEffect(hitResult.HitPlayer.X, hitResult.HitPlayer.Y, MathF.Atan2(directionY, directionX) * (180f / MathF.PI) - 180f, 6);
                        if (!hitResult.HitPlayer.IsUbered)
                        {
                            hitResult.HitPlayer.AddImpulse(
                                blade.VelocityX * 0.4f * LegacyMovementModel.SourceTicksPerSecond,
                                blade.VelocityY * 0.4f * LegacyMovementModel.SourceTicksPerSecond);
                        }
                        var hitDamage = ApplyExperimentalAirshotDamageMultiplier(owner, hitResult.HitPlayer, (int)MathF.Round(blade.HitDamage * blade.CriticalDamageMultiplier), out var damageFlags);
                        if (ApplyPlayerDamageWithContext(
                                hitResult.HitPlayer,
                                hitDamage,
                                owner,
                                PlayerEntity.SpyDamageRevealAlpha,
                                damageFlags,
                                civvieUmbrellaCriticalBoost: PlayerEntity.IsCriticalDamageMultiplierBoosted(blade.CriticalDamageMultiplier),
                                civvieUmbrellaUseLiveAttackerCriticalBoost: false,
                                additionalTraits: PlayerDamageTraits.DirectProjectile,
                                targetWasGrounded: targetWasGrounded))
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

            needle.AdvanceOneTick(ResolveProjectileGravityScale());
            var movementX = needle.X - needle.PreviousX;
            var movementY = needle.Y - needle.PreviousY;
            var movementDistance = MathF.Sqrt((movementX * movementX) + (movementY * movementY));
            if (movementDistance <= 0.0001f)
            {
                if (needle is MedicHealNeedleProjectileEntity
                    {
                        IsLastToDieJavelinFuseExpired: true,
                    } expiredJavelin)
                {
                    _ = TryExplodeLastToDieMedicJavelin(expiredJavelin);
                }

                if (needle.IsExpired)
                {
                    RemoveNeedleAt(needleIndex);
                }

                continue;
            }

            var directionX = movementX / movementDistance;
            var directionY = movementY / movementDistance;
            var movementEndX = needle.X;
            var movementEndY = needle.Y;
            needle.PrepareRaycastProbe();
            var piercedPlayerCount = 0;
            while (true)
            {
                var hit = needle is MedicHealNeedleProjectileEntity healNeedle
                    ? GetNearestMedicHealNeedleHit(healNeedle, directionX, directionY, movementDistance)
                    : GetNearestNeedleHit(needle, directionX, directionY, movementDistance);
                if (!hit.HasValue)
                {
                    RegisterCombatTrace(needle.PreviousX, needle.PreviousY, directionX, directionY, movementDistance, false);
                    break;
                }

                var hitResult = hit.Value;
                var owner = FindPlayerById(needle.OwnerId);
                if (needle.HitProbeForwardOffset > 0f)
                {
                    needle.GetBasePositionFromProbeHit(hitResult.HitX, hitResult.HitY, directionX, directionY, out var baseX, out var baseY);
                    needle.MoveTo(baseX, baseY);
                }
                else
                {
                    needle.MoveTo(hitResult.HitX, hitResult.HitY);
                }
                RegisterCombatTrace(needle.PreviousX, needle.PreviousY, directionX, directionY, hitResult.Distance, hitResult.HitPlayer is not null);
                var registerImpactEffect = false;
                var continuePiercingArrow = false;
                if (hitResult.HitPlayer is not null
                    && needle is ArrowProjectileEntity guardianArrow
                    && guardianArrow.AppliesLastToDieGuardian
                    && hitResult.HitPlayer.Team == guardianArrow.Team)
                {
                    if (owner is not null)
                    {
                        _ = TryApplyLastToDieSniperGuardian(owner, hitResult.HitPlayer);
                    }
                }
                else if (hitResult.HitPlayer is not null
                    && needle is MedicHealNeedleProjectileEntity medicHealNeedle
                    && hitResult.HitPlayer.Team == medicHealNeedle.Team)
                {
                    ApplyMedicHealNeedleTeammateHit(owner, hitResult.HitPlayer, medicHealNeedle);
                }
                else if (hitResult.HitPlayer is not null)
                {
                    RegisterBloodEffect(hitResult.HitPlayer.X, hitResult.HitPlayer.Y, MathF.Atan2(directionY, directionX) * (180f / MathF.PI) - 180f);
                    var arrowPayload = needle as ArrowProjectileEntity;
                    var capturedDamageMultiplier = arrowPayload?.LastToDieGhostDamageMultiplier ?? 1f;
                    var executesFromDecapitator = arrowPayload is
                    {
                        AppliesLastToDieDecapitator: true,
                        IsLastToDieDecapitatorFullyCharged: true,
                    } && hitResult.IsLastToDieHeadshot;
                    var hitDamage = ApplyExperimentalAirshotDamageMultiplier(
                        owner,
                        hitResult.HitPlayer,
                        (int)MathF.Round(needle.Damage * needle.CriticalDamageMultiplier * capturedDamageMultiplier),
                        out var damageFlags);
                    var wasStunnedBeforeMedicKritzM2Hit = needle is MedicHealNeedleProjectileEntity
                    {
                        AppliesLastToDieNeurotoxin: true,
                    } && hitResult.HitPlayer.IsServerStunned;
                    if (wasStunnedBeforeMedicKritzM2Hit)
                    {
                        hitDamage = checked(
                            hitDamage
                                * LastToDieDerivedModifiers.MedicNeurotoxinPreStunnedDamageMultiplier);
                    }

                    var resolution = ResolvePlayerDamageWithContext(
                            hitResult.HitPlayer,
                            hitDamage,
                            owner,
                            PlayerEntity.SpyDamageRevealAlpha,
                            damageFlags,
                            civvieUmbrellaCriticalBoost: PlayerEntity.IsCriticalDamageMultiplierBoosted(needle.CriticalDamageMultiplier),
                            civvieUmbrellaUseLiveAttackerCriticalBoost: false,
                            additionalTraits: PlayerDamageTraits.DirectProjectile
                                | (needle is MedicHealNeedleProjectileEntity
                                    ? PlayerDamageTraits.MedicKritzM2
                                    : PlayerDamageTraits.None)
                                | (needle is ArrowProjectileEntity
                                ? PlayerDamageTraits.EstablishLastToDieSpotted
                                    | PlayerDamageTraits.BenefitFromLastToDieSpotted
                                    | PlayerDamageTraits.LastToDieOverkillerEligible
                                    | (executesFromDecapitator
                                        ? PlayerDamageTraits.ExecuteAfterDefenses
                                        : PlayerDamageTraits.None)
                                : PlayerDamageTraits.None),
                            attackerWasGrounded: owner?.IsGrounded,
                            targetWasGrounded: hitResult.HitPlayer.IsGrounded,
                            sourceEntityId: needle.Id,
                            attackId: unchecked((ulong)(uint)needle.Id),
                            attackerPlayerIdOverride: needle.OwnerId);
                    if (resolution.ShouldApplyOnHitEffects
                        && needle is MedicHealNeedleProjectileEntity
                        {
                            AppliesLastToDieNeurotoxin: true,
                        })
                    {
                        _ = TryApplyLastToDieStatusEffect(
                            hitResult.HitPlayer.Id,
                            needle.OwnerId,
                            LastToDieStatusEffectSpec.Stun(
                                LastToDieStatusEffectIds.MedicNeurotoxinStun,
                                LastToDieDerivedModifiers.MedicNeurotoxinStunSeconds
                                    * Math.Max(1, Config.TicksPerSecond)));
                    }
                    if (resolution.ShouldApplyOnHitEffects
                        && owner is not null
                        && arrowPayload is not null)
                    {
                        TryApplyLastToDieSniperStatusPayload(
                            owner,
                            hitResult.HitPlayer,
                            arrowPayload.AppliesLastToDieTranqDarts,
                            arrowPayload.LastToDiePoisonDamagePerSecond);
                    }

                    var attachedDecapitatedHeadThisHit = false;
                    if (resolution.WasFatal)
                    {
                        var killFeedSprite = needle is ArrowProjectileEntity ? "BowKL"
                            : needle is NailProjectileEntity ? "NailgunKL"
                            : needle is MedicHealNeedleProjectileEntity ? "NeedleKL"
                            : needle.KillFeedWeaponSpriteName;
                        KillPlayer(
                            hitResult.HitPlayer,
                            killer: owner,
                            weaponSpriteName: killFeedSprite,
                            deadBodyAnimationKind: executesFromDecapitator
                                ? DeadBodyAnimationKind.Decapitated
                                : DeadBodyAnimationKind.Default);
                        if (executesFromDecapitator && !hitResult.HitPlayer.IsAlive)
                        {
                            attachedDecapitatedHeadThisHit = arrowPayload!.TryAttachLastToDieDecapitatedHead(
                                hitResult.HitPlayer.ClassId,
                                hitResult.HitPlayer.Team);
                            if (!attachedDecapitatedHeadThisHit)
                            {
                                TrySpawnExperimentalDemoknightDecapitationRemains(
                                    hitResult.HitPlayer,
                                    directionX,
                                    directionY);
                            }
                        }
                    }

                    if (needle is ArrowProjectileEntity piercingArrow
                        && (piercingArrow.PiercesPlayers || attachedDecapitatedHeadThisHit)
                        && hitResult.HitPlayer.Team != piercingArrow.Team)
                    {
                        piercingArrow.MarkPlayerPierced(hitResult.HitPlayer.Id);
                        continuePiercingArrow = true;
                    }
                }
                else if (hitResult.HitSentry is not null && ApplySentryDamage(hitResult.HitSentry, (int)MathF.Round(needle.Damage * needle.CriticalDamageMultiplier * ((needle as ArrowProjectileEntity)?.LastToDieGhostDamageMultiplier ?? 1f)), owner))
                {
                    DestroySentry(hitResult.HitSentry, owner);
                }
                else if (hitResult.HitGenerator is not null)
                {
                    TryDamageGenerator(hitResult.HitGenerator.Team, needle.Damage * needle.CriticalDamageMultiplier * ((needle as ArrowProjectileEntity)?.LastToDieGhostDamageMultiplier ?? 1f), owner);
                }
                else if (hitResult.HitJumpPad is not null)
                {
                    hitResult.HitJumpPad.TakeDamage((int)MathF.Round(needle.Damage * needle.CriticalDamageMultiplier * ((needle as ArrowProjectileEntity)?.LastToDieGhostDamageMultiplier ?? 1f)));
                }
                else if (TryHandleProjectileDamageableZoneHit(hitResult, needle.Damage * needle.CriticalDamageMultiplier * ((needle as ArrowProjectileEntity)?.LastToDieGhostDamageMultiplier ?? 1f), needle.Team))
                {
                    registerImpactEffect = true;
                }
                else
                {
                    registerImpactEffect = true;
                }

                if (needle is ArrowProjectileEntity explosiveArrow
                    && explosiveArrow.IsLastToDieExplosiveTipArmed)
                {
                    _ = TryExplodeLastToDieSniperArrow(
                        explosiveArrow,
                        hitResult.HitX,
                        hitResult.HitY);
                    explosiveArrow.Destroy();
                    break;
                }

                if (needle is MedicHealNeedleProjectileEntity
                    {
                        AppliesLastToDieJavelin: true,
                    } javelin)
                {
                    _ = javelin.TryAnchorLastToDieJavelin(
                        hitResult.HitX,
                        hitResult.HitY);
                    if (hitResult.HitPlayer is null)
                    {
                        RegisterImpactEffect(
                            hitResult.HitX,
                            hitResult.HitY,
                            MathF.Atan2(directionY, directionX) * (180f / MathF.PI));
                    }

                    if (javelin.IsLastToDieJavelinFuseExpired)
                    {
                        _ = TryExplodeLastToDieMedicJavelin(javelin);
                    }

                    break;
                }

                if (hitResult.HitPlayer is null)
                {
                    if (needle is ArrowProjectileEntity
                        || registerImpactEffect)
                    {
                        RegisterArrowOrImpactEffect(needle, hitResult.HitX, hitResult.HitY, directionX, directionY);
                    }
                }

                if (continuePiercingArrow)
                {
                    needle.MoveTo(movementEndX, movementEndY);
                    piercedPlayerCount += 1;
                    if (piercedPlayerCount < 64)
                    {
                        continue;
                    }
                }

                if (needle is not ArrowProjectileEntity { IsLanded: true })
                {
                    needle.Destroy();
                }
                break;
            }

            if (needle is MedicHealNeedleProjectileEntity
                {
                    IsLastToDieJavelinFuseExpired: true,
                } fuseExpiredJavelin)
            {
                _ = TryExplodeLastToDieMedicJavelin(fuseExpiredJavelin);
            }

            if (needle.IsExpired)
            {
                RemoveNeedleAt(needleIndex);
            }
        }
    }

    private void RegisterArrowOrImpactEffect(
        NeedleProjectileEntity needle,
        float hitX,
        float hitY,
        float directionX,
        float directionY)
    {
        if (needle is ArrowProjectileEntity arrow)
        {
            RegisterStuckArrowEffect(hitX, hitY, directionX, directionY, arrow);
            return;
        }

        RegisterImpactEffect(hitX, hitY, MathF.Atan2(directionY, directionX) * (180f / MathF.PI));
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

            shot.AdvanceOneTick(ResolveProjectileGravityScale());
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
                if (hitResult.HitPlayer is not null)
                {
                    var resolution = ResolveRevolverPlayerHit(
                        shot,
                        owner,
                        hitResult.HitPlayer,
                        shot.PreviousX,
                        shot.PreviousY,
                        hitResult.HitX,
                        hitResult.HitY);
                    if (!ClientPredictionMode
                        && shot.LastToDieProfile.RicochetEnabled
                        && resolution.AppliedHealthDamage > 0)
                    {
                        ResolveLastToDieRicochetChain(shot, owner, hitResult.HitPlayer);
                    }
                }
                else
                {
                    RegisterCombatTrace(
                        shot.PreviousX,
                        shot.PreviousY,
                        directionX,
                        directionY,
                        hitResult.Distance,
                        false,
                        isCritical: shot.IsCritical);
                    if (hitResult.HitSentry is not null && ApplySentryDamage(hitResult.HitSentry, (int)MathF.Round(shot.DamageValue * shot.CriticalDamageMultiplier), owner))
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
                }

                shot.Destroy();
            }
            else
            {
                RegisterCombatTrace(
                    shot.PreviousX,
                    shot.PreviousY,
                    directionX,
                    directionY,
                    movementDistance,
                    false,
                    isCritical: shot.IsCritical);
            }

            if (shot.IsExpired)
            {
                RemoveRevolverShotAt(shotIndex);
            }
        }
    }

    private PlayerDamageResolution ResolveRevolverPlayerHit(
        RevolverProjectileEntity shot,
        PlayerEntity? owner,
        PlayerEntity target,
        float originX,
        float originY,
        float hitX,
        float hitY)
    {
        var segmentX = hitX - originX;
        var segmentY = hitY - originY;
        var segmentDistance = MathF.Sqrt((segmentX * segmentX) + (segmentY * segmentY));
        if (segmentDistance <= 0.0001f)
        {
            return default;
        }

        var directionX = segmentX / segmentDistance;
        var directionY = segmentY / segmentDistance;
        var isCritical = shot.IsCritical
            || (shot.LastToDieProfile.ExecutionerEnabled
                && target.Health
                    < target.MaxHealth * LastToDieSpyRevolverProfile.ExecutionerHealthThreshold);
        RegisterCombatTrace(
            originX,
            originY,
            directionX,
            directionY,
            segmentDistance,
            hitCharacter: true,
            isCritical: isCritical);

        var criticalDamageMultiplier = shot.IsCritical
            ? shot.CriticalDamageMultiplier
            : isCritical
                ? ExperimentalGameplaySettings.KritzCriticalDamageMultiplier
                : 1f;
        var hitDamage = ApplyExperimentalAirshotDamageMultiplier(
            owner,
            target,
            (int)MathF.Round(shot.DamageValue * criticalDamageMultiplier),
            out var damageFlags);
        var traits = PlayerDamageTraits.CanEvade
            | PlayerDamageTraits.CanApplyOnHitEffects
            | PlayerDamageTraits.CanReflect
            | PlayerDamageTraits.Bullet
            | PlayerDamageTraits.DirectProjectile;
        if (isCritical)
        {
            traits |= PlayerDamageTraits.Critical;
        }

        var resolution = ResolvePlayerDamage(
            target,
            new PlayerDamageRequest(
                PlayerDamageApplicationKind.Instant,
                hitDamage,
                owner,
                PlayerEntity.SpyDamageRevealAlpha,
                damageFlags,
                traits,
                AllowOsmosisHealOwnedSentries: true,
                new PlayerDamageUmbrellaOptions(
                    AllowBlock: true,
                    ThreatSourceX: originX,
                    ThreatSourceY: originY,
                    CriticalBoost: isCritical,
                    UseLiveAttackerCriticalBoost: false),
                SourceEntityId: shot.Id,
                AttackId: unchecked((ulong)(uint)shot.Id)));
        if (resolution.ShouldApplyOnHitEffects)
        {
            if (target.IsAlive)
            {
                BulletKnockbackRules.Apply(
                    target,
                    directionX,
                    directionY,
                    shot.PlayerKnockbackPayload);
            }
            RegisterBloodEffect(
                target.X,
                target.Y,
                MathF.Atan2(directionY, directionX) * (180f / MathF.PI) - 180f);
            if (!resolution.WasFatal)
            {
                ApplyLastToDieRevolverOnHitEffects(shot, target, directionX, directionY);
            }
        }

        if (resolution.WasFatal)
        {
            KillPlayer(
                target,
                killer: owner,
                weaponSpriteName: shot.KillFeedWeaponSpriteNameOverride ?? "RevolverKL");
        }

        return resolution;
    }

    private void ResolveLastToDieRicochetChain(
        RevolverProjectileEntity shot,
        PlayerEntity? owner,
        PlayerEntity initialTarget)
    {
        var visitedPlayerIds = new HashSet<int> { initialTarget.Id };
        var originX = initialTarget.X;
        var originY = GetLastToDieRicochetFocusY(initialTarget);
        for (var bounce = 0;
             bounce < LastToDieSpyRevolverProfile.RicochetMaximumBounces;
             bounce += 1)
        {
            var target = FindLastToDieRicochetTarget(
                shot,
                originX,
                originY,
                visitedPlayerIds);
            if (target is null)
            {
                return;
            }

            visitedPlayerIds.Add(target.Id);
            var targetX = target.X;
            var targetY = GetLastToDieRicochetFocusY(target);
            var resolution = ResolveRevolverPlayerHit(
                shot,
                owner,
                target,
                originX,
                originY,
                targetX,
                targetY);
            if (resolution.AppliedHealthDamage <= 0)
            {
                return;
            }

            originX = targetX;
            originY = targetY;
        }
    }

    private PlayerEntity? FindLastToDieRicochetTarget(
        RevolverProjectileEntity shot,
        float originX,
        float originY,
        IReadOnlySet<int> visitedPlayerIds)
    {
        var maximumDistanceSquared = LastToDieSpyRevolverProfile.RicochetTargetRadius
            * LastToDieSpyRevolverProfile.RicochetTargetRadius;
        var nearestDistanceSquared = float.PositiveInfinity;
        PlayerEntity? nearest = null;
        foreach (var candidate in EnumerateSimulatedPlayers())
        {
            if (!candidate.IsAlive
                || candidate.Id == shot.OwnerId
                || candidate.Team == shot.Team
                || visitedPlayerIds.Contains(candidate.Id)
                || !CanTeamDamagePlayer(shot.Team, shot.OwnerId, candidate)
                || (candidate.IsSpyCloaked
                    && (!candidate.IsSpyVisibleToEnemies
                        || candidate.SpyCloakAlpha <= 0.0001f)))
            {
                continue;
            }

            var targetX = candidate.X;
            var targetY = GetLastToDieRicochetFocusY(candidate);
            var deltaX = targetX - originX;
            var deltaY = targetY - originY;
            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (distanceSquared <= 0.0001f
                || distanceSquared > maximumDistanceSquared
                || IsProjectilePathBlocked(originX, originY, targetX, targetY, shot.Team)
                || (distanceSquared > nearestDistanceSquared
                    || (distanceSquared == nearestDistanceSquared
                        && nearest is not null
                        && candidate.Id >= nearest.Id)))
            {
                continue;
            }

            nearest = candidate;
            nearestDistanceSquared = distanceSquared;
        }

        return nearest;
    }

    private static float GetLastToDieRicochetFocusY(PlayerEntity player)
        => player.Y - (player.Height / 4f);

    private void ApplyLastToDieRevolverOnHitEffects(
        RevolverProjectileEntity shot,
        PlayerEntity target,
        float directionX,
        float directionY)
    {
        var profile = shot.LastToDieProfile;
        if (profile.BleedDamagePerSecond > 0f)
        {
            TryApplyLastToDieStatusEffect(
                target.Id,
                shot.OwnerId,
                LastToDieStatusEffectSpec.Bleed(
                    LastToDieStatusEffectIds.SpyBlunderbussBleed,
                    GetSimulationTicksFromSourceTicks(
                        LastToDieSpyRevolverProfile.BlunderbussBleedDurationSourceTicks),
                profile.BleedDamagePerSecond));
        }

        if (shot.AppliesLuckyStrikeStun)
        {
            TryApplyLastToDieStatusEffect(
                target.Id,
                shot.OwnerId,
                LastToDieStatusEffectSpec.Stun(
                    LastToDieStatusEffectIds.SpyLuckyStrikeStun,
                    GetSimulationTicksFromSourceTicks(
                        LastToDieSpyRevolverProfile.LuckyStrikeStunDurationSourceTicks)));
        }

        if (!profile.RubberBulletsEnabled || target.IsUbered)
        {
            return;
        }

        target.AddImpulse(0f, LastToDieSpyRevolverProfile.RubberBulletsUpwardImpulsePerSecond);
        TryApplyLastToDieStatusEffect(
            target.Id,
            shot.OwnerId,
            LastToDieStatusEffectSpec.Slow(
                LastToDieStatusEffectIds.SpyRubberBulletsSlow,
                GetSimulationTicksFromSourceTicks(
                    LastToDieSpyRevolverProfile.RubberBulletsSlowDurationSourceTicks),
                LastToDieSpyRevolverProfile.RubberBulletsMovementMultiplier));
    }
}
