namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{

    private void AdvanceShots()
    {
        for (var shotIndex = _shots.Count - 1; shotIndex >= 0; shotIndex -= 1)
        {
            var shot = _shots[shotIndex];
            shot.AdvanceOneTick();
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
                    if (ApplyPlayerDamage(hitResult.HitPlayer, ShotProjectileEntity.DamagePerHit, owner, PlayerEntity.SpyDamageRevealAlpha))
                    {
                        KillPlayer(hitResult.HitPlayer, killer: owner, weaponSpriteName: GetKillFeedWeaponSprite(owner));
                    }
                }
                else if (hitResult.HitSentry is not null && ApplySentryDamage(hitResult.HitSentry, ShotProjectileEntity.DamagePerHit, FindPlayerById(shot.OwnerId)))
                {
                    DestroySentry(hitResult.HitSentry);
                }
                else if (hitResult.HitGenerator is not null)
                {
                    TryDamageGenerator(hitResult.HitGenerator.Team, ShotProjectileEntity.DamagePerHit, FindPlayerById(shot.OwnerId));
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
                    hitResult.HitPlayer.AddImpulse(blade.VelocityX * 0.4f, blade.VelocityY * 0.4f);
                    var owner = FindPlayerById(blade.OwnerId);
                    if (ApplyPlayerDamage(hitResult.HitPlayer, blade.HitDamage, owner, PlayerEntity.SpyDamageRevealAlpha))
                        {
                            KillPlayer(hitResult.HitPlayer, killer: owner, weaponSpriteName: "BladeKL");
                        }
                    }
                    else if (hitResult.HitSentry is not null && ApplySentryDamage(hitResult.HitSentry, blade.HitDamage, FindPlayerById(blade.OwnerId)))
                    {
                        DestroySentry(hitResult.HitSentry);
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
                blade.Destroy();
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
            needle.AdvanceOneTick();
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
                    if (ApplyPlayerDamage(hitResult.HitPlayer, NeedleProjectileEntity.DamagePerHit, owner, PlayerEntity.SpyDamageRevealAlpha))
                    {
                        KillPlayer(hitResult.HitPlayer, killer: owner, weaponSpriteName: "NeedleKL");
                    }
                }
                else if (hitResult.HitSentry is not null && ApplySentryDamage(hitResult.HitSentry, NeedleProjectileEntity.DamagePerHit, FindPlayerById(needle.OwnerId)))
                {
                    DestroySentry(hitResult.HitSentry);
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
            shot.AdvanceOneTick();
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
                    if (ApplyPlayerDamage(hitResult.HitPlayer, RevolverProjectileEntity.DamagePerHit, owner, PlayerEntity.SpyDamageRevealAlpha))
                    {
                        KillPlayer(hitResult.HitPlayer, killer: owner, weaponSpriteName: "RevolverKL");
                    }
                }
                else if (hitResult.HitSentry is not null && ApplySentryDamage(hitResult.HitSentry, RevolverProjectileEntity.DamagePerHit, FindPlayerById(shot.OwnerId)))
                {
                    DestroySentry(hitResult.HitSentry);
                }
                else if (hitResult.HitGenerator is not null)
                {
                    TryDamageGenerator(hitResult.HitGenerator.Team, RevolverProjectileEntity.DamagePerHit, FindPlayerById(shot.OwnerId));
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

    private void AdvanceStabAnimations()
    {
        for (var animationIndex = _stabAnimations.Count - 1; animationIndex >= 0; animationIndex -= 1)
        {
            var animation = _stabAnimations[animationIndex];
            var owner = FindPlayerById(animation.OwnerId);
            if (owner is null || !owner.IsAlive || owner.ClassId != PlayerClass.Spy)
            {
                RemoveStabAnimationAt(animationIndex);
                continue;
            }

            animation.AdvanceOneTick(owner.X, owner.Y);
            if (animation.IsExpired)
            {
                RemoveStabAnimationAt(animationIndex);
            }
        }
    }

    private void AdvanceStabMasks()
    {
        for (var maskIndex = _stabMasks.Count - 1; maskIndex >= 0; maskIndex -= 1)
        {
            var mask = _stabMasks[maskIndex];
            var owner = FindPlayerById(mask.OwnerId);
            if (owner is null || !owner.IsAlive || owner.ClassId != PlayerClass.Spy)
            {
                RemoveStabMaskAt(maskIndex);
                continue;
            }

            mask.AdvanceOneTick(owner.X, owner.Y);
            var directionRadians = DegreesToRadians(mask.DirectionDegrees);
            var directionX = MathF.Cos(directionRadians);
            var directionY = MathF.Sin(directionRadians);
            var hit = GetNearestStabHit(mask, directionX, directionY);
            if (hit.HasValue)
            {
                var hitResult = hit.Value;
                RegisterCombatTrace(mask.X, mask.Y, directionX, directionY, hitResult.Distance, hitResult.HitPlayer is not null);
                if (hitResult.HitPlayer is not null)
                {
                    RegisterBloodEffect(hitResult.HitPlayer.X, hitResult.HitPlayer.Y, mask.DirectionDegrees - 180f, 6);
                    if (ApplyPlayerDamage(hitResult.HitPlayer, StabMaskEntity.DamagePerHit, owner, PlayerEntity.SpyDamageRevealAlpha))
                    {
                        KillPlayer(hitResult.HitPlayer, killer: owner, weaponSpriteName: "KnifeKL");
                    }
                }
                else if (hitResult.HitSentry is not null && ApplySentryDamage(hitResult.HitSentry, StabMaskEntity.DamagePerHit, owner))
                {
                    DestroySentry(hitResult.HitSentry);
                }

                mask.Destroy();
            }

            if (mask.IsExpired)
            {
                RegisterImpactEffect(
                    mask.X + MathF.Sign(directionX) * 15f,
                    mask.Y - 12f,
                    mask.DirectionDegrees);
                RemoveStabMaskAt(maskIndex);
            }
        }
    }

    private void AdvanceFlames()
    {
        var deltaSeconds = (float)Config.FixedDeltaSeconds;
        var flameAirLifetimeTicks = GetSimulationTicksFromSourceTicks(FlameProjectileEntity.AirLifetimeTicks);
        for (var flameIndex = _flames.Count - 1; flameIndex >= 0; flameIndex -= 1)
        {
            var flame = _flames[flameIndex];
            flame.AdvanceOneTick(deltaSeconds);
            var movementX = flame.X - flame.PreviousX;
            var movementY = flame.Y - flame.PreviousY;
            var movementDistance = MathF.Sqrt((movementX * movementX) + (movementY * movementY));
            if (movementDistance <= 0.0001f)
            {
                if (flame.IsExpired)
                {
                    RemoveFlameAt(flameIndex);
                }

                continue;
            }

            var directionX = movementX / movementDistance;
            var directionY = movementY / movementDistance;
            var hit = GetNearestFlameHit(flame, directionX, directionY, movementDistance);
            if (hit.HasValue)
            {
                var hitResult = hit.Value;
                flame.MoveTo(hitResult.HitX, hitResult.HitY);
                if (hitResult.HitPlayer is not null)
                {
                    var hitPlayer = hitResult.HitPlayer;
                    var owner = FindPlayerById(flame.OwnerId);
                    var playerDied = ApplyPlayerContinuousDamage(hitPlayer, FlameProjectileEntity.DirectHitDamage, owner);
                    if (playerDied)
                    {
                        KillPlayer(hitPlayer, killer: owner, weaponSpriteName: "FlameKL");
                    }
                    else
                    {
                        hitPlayer.IgniteAfterburn(
                            flame.OwnerId,
                            FlameProjectileEntity.BurnDurationIncreaseSourceTicks,
                            FlameProjectileEntity.BurnIntensityIncrease,
                            FlameProjectileEntity.AfterburnFalloff,
                            flame.GetAfterburnFalloffAmount(flameAirLifetimeTicks));
                    }
                }
                else if (hitResult.HitSentry is not null && ApplySentryDamage(hitResult.HitSentry, (int)FlameProjectileEntity.DirectHitDamage, FindPlayerById(flame.OwnerId)))
                {
                    DestroySentry(hitResult.HitSentry);
                }
                else if (hitResult.HitGenerator is not null)
                {
                    TryDamageGenerator(hitResult.HitGenerator.Team, (int)FlameProjectileEntity.DirectHitDamage, FindPlayerById(flame.OwnerId));
                }

                RegisterCombatTrace(flame.PreviousX, flame.PreviousY, directionX, directionY, hitResult.Distance, hitResult.HitPlayer is not null);
                flame.Destroy();
            }
            else
            {
                RegisterCombatTrace(flame.PreviousX, flame.PreviousY, directionX, directionY, movementDistance, false);
            }

            if (flame.IsExpired)
            {
                RemoveFlameAt(flameIndex);
            }
        }
    }

    private void AdvanceFlares()
    {
        for (var flareIndex = _flares.Count - 1; flareIndex >= 0; flareIndex -= 1)
        {
            var flare = _flares[flareIndex];
            flare.AdvanceOneTick();
            var movementX = flare.X - flare.PreviousX;
            var movementY = flare.Y - flare.PreviousY;
            var movementDistance = MathF.Sqrt((movementX * movementX) + (movementY * movementY));
            if (movementDistance <= 0.0001f)
            {
                if (flare.IsExpired)
                {
                    RemoveFlareAt(flareIndex);
                }

                continue;
            }

            var directionX = movementX / movementDistance;
            var directionY = movementY / movementDistance;
            var hit = GetNearestFlareHit(flare, directionX, directionY, movementDistance);
            var bubbleHit = GetNearestEnemyBubbleHit(flare.PreviousX, flare.PreviousY, directionX, directionY, movementDistance, flare.Team);
            var bubbleDistance = bubbleHit?.Distance ?? float.MaxValue;
            var hitDistance = hit?.Distance ?? float.MaxValue;
            if (bubbleHit is not null && bubbleDistance <= hitDistance)
            {
                flare.MoveTo(bubbleHit.Value.HitX, bubbleHit.Value.HitY);
                RegisterCombatTrace(flare.PreviousX, flare.PreviousY, directionX, directionY, bubbleHit.Value.Distance, false);
                RemoveBubbleAt(bubbleHit.Value.BubbleIndex);
                flare.Destroy();
            }
            else if (hit.HasValue)
            {
                var hitResult = hit.Value;
                flare.MoveTo(hitResult.HitX, hitResult.HitY);
                RegisterCombatTrace(flare.PreviousX, flare.PreviousY, directionX, directionY, hitResult.Distance, hitResult.HitPlayer is not null);
                if (hitResult.HitPlayer is not null)
                {
                        RegisterBloodEffect(hitResult.HitPlayer.X, hitResult.HitPlayer.Y, MathF.Atan2(directionY, directionX) * (180f / MathF.PI) - 180f);
                    var owner = FindPlayerById(flare.OwnerId);
                    var playerDied = ApplyPlayerDamage(hitResult.HitPlayer, FlareProjectileEntity.DamagePerHit, owner, PlayerEntity.SpyDamageRevealAlpha);
                    if (playerDied)
                    {
                        KillPlayer(hitResult.HitPlayer, killer: owner, weaponSpriteName: "FlareKL");
                    }
                    else
                    {
                        hitResult.HitPlayer.IgniteAfterburn(
                            flare.OwnerId,
                            FlareProjectileEntity.BurnDurationIncreaseSourceTicks,
                            FlareProjectileEntity.BurnIntensityIncrease,
                            FlareProjectileEntity.AfterburnFalloff,
                            burnFalloffAmount: 0f);
                    }
                }
                else if (hitResult.HitSentry is not null && ApplySentryDamage(hitResult.HitSentry, FlareProjectileEntity.DamagePerHit, FindPlayerById(flare.OwnerId)))
                {
                    DestroySentry(hitResult.HitSentry);
                }
                else if (hitResult.HitGenerator is not null)
                {
                    TryDamageGenerator(hitResult.HitGenerator.Team, FlareProjectileEntity.DamagePerHit, FindPlayerById(flare.OwnerId));
                }

                flare.Destroy();
            }
            else
            {
                RegisterCombatTrace(flare.PreviousX, flare.PreviousY, directionX, directionY, movementDistance, false);
            }

            if (flare.IsExpired)
            {
                RemoveFlareAt(flareIndex);
            }
        }
    }

    private void AdvanceMines()
    {
        for (var mineIndex = _mines.Count - 1; mineIndex >= 0; mineIndex -= 1)
        {
            var mine = _mines[mineIndex];
            mine.AdvanceOneTick();
            if (mine.IsStickied)
            {
                continue;
            }

            var movementX = mine.X - mine.PreviousX;
            var movementY = mine.Y - mine.PreviousY;
            var movementDistance = MathF.Sqrt((movementX * movementX) + (movementY * movementY));
            if (movementDistance <= 0.0001f)
            {
                continue;
            }

            var directionX = movementX / movementDistance;
            var directionY = movementY / movementDistance;
            var hit = GetNearestMineHit(mine, directionX, directionY, movementDistance);
            if (!hit.HasValue)
            {
                continue;
            }

            var hitResult = hit.Value;
            mine.MoveTo(hitResult.HitX, hitResult.HitY);
            if (hitResult.DestroyOnHit)
            {
                RemoveMineAt(mineIndex);
                continue;
            }

            mine.Stick();
        }
    }

    private void RemoveShotAt(int shotIndex)
    {
        var shot = _shots[shotIndex];
        _entities.Remove(shot.Id);
        _shots.RemoveAt(shotIndex);
    }

    private void RemoveBladeAt(int bladeIndex)
    {
        var blade = _blades[bladeIndex];
        if (FindPlayerById(blade.OwnerId) is { } owner)
        {
            owner.DecrementQuoteBladeCount();
        }

        _entities.Remove(blade.Id);
        _blades.RemoveAt(bladeIndex);
    }

    private void RemoveNeedleAt(int needleIndex)
    {
        var needle = _needles[needleIndex];
        _entities.Remove(needle.Id);
        _needles.RemoveAt(needleIndex);
    }

    private void RemoveRevolverShotAt(int shotIndex)
    {
        var shot = _revolverShots[shotIndex];
        _entities.Remove(shot.Id);
        _revolverShots.RemoveAt(shotIndex);
    }

    private void RemoveStabAnimationAt(int animationIndex)
    {
        var animation = _stabAnimations[animationIndex];
        _entities.Remove(animation.Id);
        _stabAnimations.RemoveAt(animationIndex);
    }

    private void RemoveStabMaskAt(int maskIndex)
    {
        var mask = _stabMasks[maskIndex];
        _entities.Remove(mask.Id);
        _stabMasks.RemoveAt(maskIndex);
    }

    private void RemoveOwnedSpyArtifacts(int ownerId)
    {
        for (var animationIndex = _stabAnimations.Count - 1; animationIndex >= 0; animationIndex -= 1)
        {
            if (_stabAnimations[animationIndex].OwnerId == ownerId)
            {
                RemoveStabAnimationAt(animationIndex);
            }
        }

        for (var maskIndex = _stabMasks.Count - 1; maskIndex >= 0; maskIndex -= 1)
        {
            if (_stabMasks[maskIndex].OwnerId == ownerId)
            {
                RemoveStabMaskAt(maskIndex);
            }
        }
    }

    private void RemoveFlameAt(int flameIndex)
    {
        var flame = _flames[flameIndex];
        _entities.Remove(flame.Id);
        _flames.RemoveAt(flameIndex);
    }

    private void RemoveFlareAt(int flareIndex)
    {
        var flare = _flares[flareIndex];
        _entities.Remove(flare.Id);
        _flares.RemoveAt(flareIndex);
    }

    private void RemoveMineAt(int mineIndex)
    {
        var mine = _mines[mineIndex];
        _entities.Remove(mine.Id);
        _mines.RemoveAt(mineIndex);
    }

    private void RemoveOwnedSentries(int ownerId)
    {
        for (var sentryIndex = _sentries.Count - 1; sentryIndex >= 0; sentryIndex -= 1)
        {
            if (_sentries[sentryIndex].OwnerPlayerId == ownerId)
            {
                DestroySentry(_sentries[sentryIndex]);
            }
        }
    }

    private void RemoveOwnedMines(int ownerId)
    {
        for (var mineIndex = _mines.Count - 1; mineIndex >= 0; mineIndex -= 1)
        {
            if (_mines[mineIndex].OwnerId == ownerId)
            {
                RemoveMineAt(mineIndex);
            }
        }
    }

    private void RemoveOwnedProjectiles(int ownerId)
    {
        for (var shotIndex = _shots.Count - 1; shotIndex >= 0; shotIndex -= 1)
        {
            if (_shots[shotIndex].OwnerId == ownerId)
            {
                RemoveShotAt(shotIndex);
            }
        }

        for (var needleIndex = _needles.Count - 1; needleIndex >= 0; needleIndex -= 1)
        {
            if (_needles[needleIndex].OwnerId == ownerId)
            {
                RemoveNeedleAt(needleIndex);
            }
        }

        for (var shotIndex = _revolverShots.Count - 1; shotIndex >= 0; shotIndex -= 1)
        {
            if (_revolverShots[shotIndex].OwnerId == ownerId)
            {
                RemoveRevolverShotAt(shotIndex);
            }
        }

        for (var bubbleIndex = _bubbles.Count - 1; bubbleIndex >= 0; bubbleIndex -= 1)
        {
            if (_bubbles[bubbleIndex].OwnerId == ownerId)
            {
                RemoveBubbleAt(bubbleIndex);
            }
        }

        for (var bladeIndex = _blades.Count - 1; bladeIndex >= 0; bladeIndex -= 1)
        {
            if (_blades[bladeIndex].OwnerId == ownerId)
            {
                RemoveBladeAt(bladeIndex);
            }
        }

        for (var rocketIndex = _rockets.Count - 1; rocketIndex >= 0; rocketIndex -= 1)
        {
            if (_rockets[rocketIndex].OwnerId == ownerId)
            {
                RemoveRocketAt(rocketIndex);
            }
        }

        for (var flameIndex = _flames.Count - 1; flameIndex >= 0; flameIndex -= 1)
        {
            if (_flames[flameIndex].OwnerId == ownerId)
            {
                RemoveFlameAt(flameIndex);
            }
        }

        for (var flareIndex = _flares.Count - 1; flareIndex >= 0; flareIndex -= 1)
        {
            if (_flares[flareIndex].OwnerId == ownerId)
            {
                RemoveFlareAt(flareIndex);
            }
        }
    }

    private int CountOwnedMines(int ownerId)
    {
        var count = 0;
        foreach (var mine in _mines)
        {
            if (mine.OwnerId == ownerId)
            {
                count += 1;
            }
        }

        return count;
    }

    private static bool CircleIntersectsPlayer(float circleX, float circleY, float radius, PlayerEntity player)
    {
        player.GetCollisionBounds(out var left, out var top, out var right, out var bottom);
        return CircleIntersectsRectangle(
            circleX,
            circleY,
            radius,
            left,
            top,
            right,
            bottom);
    }

    private static bool CircleIntersectsRectangle(float circleX, float circleY, float radius, float left, float top, float right, float bottom)
    {
        var closestX = float.Clamp(circleX, left, right);
        var closestY = float.Clamp(circleY, top, bottom);
        var deltaX = circleX - closestX;
        var deltaY = circleY - closestY;
        return (deltaX * deltaX) + (deltaY * deltaY) <= radius * radius;
    }

}

