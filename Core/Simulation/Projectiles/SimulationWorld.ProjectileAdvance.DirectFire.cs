namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void AdvanceShots()
    {
        for (var shotIndex = _shots.Count - 1; shotIndex >= 0; shotIndex -= 1)
        {
            var shot = _shots[shotIndex];
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
                shot.MoveTo(hitResult.HitX, hitResult.HitY);
                RegisterCombatTrace(shot.PreviousX, shot.PreviousY, directionX, directionY, hitResult.Distance, hitResult.HitPlayer is not null);
                if (hitResult.HitPlayer is not null)
                {
                    RegisterBloodEffect(hitResult.HitPlayer.X, hitResult.HitPlayer.Y, MathF.Atan2(directionY, directionX) * (180f / MathF.PI) - 180f);
                    var owner = FindPlayerById(shot.OwnerId);
                    var hitDamage = ApplyExperimentalAirshotDamageMultiplier(owner, hitResult.HitPlayer, (int)MathF.Round(shot.DamageValue), out var damageFlags);
                    if (ApplyPlayerDamage(hitResult.HitPlayer, hitDamage, owner, PlayerEntity.SpyDamageRevealAlpha, damageFlags))
                    {
                        KillPlayer(
                            hitResult.HitPlayer,
                            killer: owner,
                            weaponSpriteName: shot.KillFeedWeaponSpriteNameOverride ?? GetKillFeedWeaponSprite(owner));
                    }
                }
                else if (hitResult.HitSentry is not null && ApplySentryDamage(hitResult.HitSentry, (int)MathF.Round(shot.DamageValue), FindPlayerById(shot.OwnerId)))
                {
                    DestroySentry(hitResult.HitSentry, FindPlayerById(shot.OwnerId));
                }
                else if (hitResult.HitGenerator is not null)
                {
                    TryDamageGenerator(hitResult.HitGenerator.Team, shot.DamageValue, FindPlayerById(shot.OwnerId));
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

    private void AdvanceBlades()
    {
        for (var bladeIndex = _blades.Count - 1; bladeIndex >= 0; bladeIndex -= 1)
        {
            var blade = _blades[bladeIndex];
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
                    blade.MoveTo(hitResult.HitX, hitResult.HitY);
                    RegisterCombatTrace(blade.PreviousX, blade.PreviousY, directionX, directionY, hitResult.Distance, hitResult.HitPlayer is not null);
                    if (hitResult.HitPlayer is not null)
                    {
                        RegisterBloodEffect(hitResult.HitPlayer.X, hitResult.HitPlayer.Y, MathF.Atan2(directionY, directionX) * (180f / MathF.PI) - 180f, 6);
                        hitResult.HitPlayer.AddImpulse(
                            blade.VelocityX * 0.4f * LegacyMovementModel.SourceTicksPerSecond,
                            blade.VelocityY * 0.4f * LegacyMovementModel.SourceTicksPerSecond);
                        var owner = FindPlayerById(blade.OwnerId);
                        var hitDamage = ApplyExperimentalAirshotDamageMultiplier(owner, hitResult.HitPlayer, blade.HitDamage, out var damageFlags);
                        if (ApplyPlayerDamage(hitResult.HitPlayer, hitDamage, owner, PlayerEntity.SpyDamageRevealAlpha, damageFlags))
                        {
                            KillPlayer(hitResult.HitPlayer, killer: owner, weaponSpriteName: "BladeKL");
                        }
                    }
                    else if (hitResult.HitSentry is not null && ApplySentryDamage(hitResult.HitSentry, blade.HitDamage, FindPlayerById(blade.OwnerId)))
                    {
                        DestroySentry(hitResult.HitSentry, FindPlayerById(blade.OwnerId));
                    }
                    else if (hitResult.HitGenerator is not null)
                    {
                        TryDamageGenerator(hitResult.HitGenerator.Team, blade.HitDamage, FindPlayerById(blade.OwnerId));
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
            var hit = GetNearestNeedleHit(needle, directionX, directionY, movementDistance);
            if (hit.HasValue)
            {
                var hitResult = hit.Value;
                needle.MoveTo(hitResult.HitX, hitResult.HitY);
                RegisterCombatTrace(needle.PreviousX, needle.PreviousY, directionX, directionY, hitResult.Distance, hitResult.HitPlayer is not null);
                if (hitResult.HitPlayer is not null)
                {
                    RegisterBloodEffect(hitResult.HitPlayer.X, hitResult.HitPlayer.Y, MathF.Atan2(directionY, directionX) * (180f / MathF.PI) - 180f);
                    var owner = FindPlayerById(needle.OwnerId);
                    var hitDamage = ApplyExperimentalAirshotDamageMultiplier(owner, hitResult.HitPlayer, NeedleProjectileEntity.DamagePerHit, out var damageFlags);
                    if (ApplyPlayerDamage(hitResult.HitPlayer, hitDamage, owner, PlayerEntity.SpyDamageRevealAlpha, damageFlags))
                    {
                        KillPlayer(hitResult.HitPlayer, killer: owner, weaponSpriteName: "NeedleKL");
                    }
                }
                else if (hitResult.HitSentry is not null && ApplySentryDamage(hitResult.HitSentry, NeedleProjectileEntity.DamagePerHit, FindPlayerById(needle.OwnerId)))
                {
                    DestroySentry(hitResult.HitSentry, FindPlayerById(needle.OwnerId));
                }
                else if (hitResult.HitGenerator is not null)
                {
                    TryDamageGenerator(hitResult.HitGenerator.Team, NeedleProjectileEntity.DamagePerHit, FindPlayerById(needle.OwnerId));
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
                shot.MoveTo(hitResult.HitX, hitResult.HitY);
                RegisterCombatTrace(shot.PreviousX, shot.PreviousY, directionX, directionY, hitResult.Distance, hitResult.HitPlayer is not null);
                if (hitResult.HitPlayer is not null)
                {
                    RegisterBloodEffect(hitResult.HitPlayer.X, hitResult.HitPlayer.Y, MathF.Atan2(directionY, directionX) * (180f / MathF.PI) - 180f);
                    var owner = FindPlayerById(shot.OwnerId);
                    var hitDamage = ApplyExperimentalAirshotDamageMultiplier(owner, hitResult.HitPlayer, (int)MathF.Round(shot.DamageValue), out var damageFlags);
                    if (ApplyPlayerDamage(hitResult.HitPlayer, hitDamage, owner, PlayerEntity.SpyDamageRevealAlpha, damageFlags))
                    {
                        KillPlayer(
                            hitResult.HitPlayer,
                            killer: owner,
                            weaponSpriteName: shot.KillFeedWeaponSpriteNameOverride ?? "RevolverKL");
                    }
                }
                else if (hitResult.HitSentry is not null && ApplySentryDamage(hitResult.HitSentry, (int)MathF.Round(shot.DamageValue), FindPlayerById(shot.OwnerId)))
                {
                    DestroySentry(hitResult.HitSentry, FindPlayerById(shot.OwnerId));
                }
                else if (hitResult.HitGenerator is not null)
                {
                    TryDamageGenerator(hitResult.HitGenerator.Team, shot.DamageValue, FindPlayerById(shot.OwnerId));
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
