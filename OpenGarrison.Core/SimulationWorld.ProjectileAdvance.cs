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
                    if (hitResult.HitPlayer.ApplyDamage(ShotProjectileEntity.DamagePerHit, PlayerEntity.SpyDamageRevealAlpha))
                    {
                        KillPlayer(hitResult.HitPlayer, killer: FindPlayerById(shot.OwnerId), weaponSpriteName: GetKillFeedWeaponSprite(FindPlayerById(shot.OwnerId)));
                    }
                }
                else if (hitResult.HitSentry is not null && hitResult.HitSentry.ApplyDamage(ShotProjectileEntity.DamagePerHit))
                {
                    DestroySentry(hitResult.HitSentry);
                }
                else if (hitResult.HitGenerator is not null)
                {
                    TryDamageGenerator(hitResult.HitGenerator.Team, ShotProjectileEntity.DamagePerHit);
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

    private void AdvanceBubbles()
    {
        var deltaSeconds = (float)Config.FixedDeltaSeconds;
        for (var bubbleIndex = _bubbles.Count - 1; bubbleIndex >= 0; bubbleIndex -= 1)
        {
            var bubble = _bubbles[bubbleIndex];
            var owner = FindPlayerById(bubble.OwnerId);
            if (owner is null || !owner.IsAlive)
            {
                RemoveBubbleAt(bubbleIndex);
                continue;
            }

            if (DistanceBetween(bubble.X, bubble.Y, owner.X, owner.Y) > BubbleProjectileEntity.MaxDistanceFromOwner)
            {
                RemoveBubbleAt(bubbleIndex);
                continue;
            }

            bubble.AdvanceOneTick(owner.X, owner.Y, owner.HorizontalSpeed, owner.VerticalSpeed, owner.AimDirectionDegrees, deltaSeconds);
            if (bubble.IsExpired || DistanceBetween(bubble.X, bubble.Y, owner.X, owner.Y) > BubbleProjectileEntity.MaxDistanceFromOwner)
            {
                RemoveBubbleAt(bubbleIndex);
                continue;
            }

            if (TryResolveBubbleEnvironmentCollision(bubble) || bubble.IsExpired)
            {
                RemoveBubbleAt(bubbleIndex);
                continue;
            }

            if (TryDamageBubbleEnemyPlayer(bubble, owner))
            {
                RemoveBubbleAt(bubbleIndex);
                continue;
            }

            ApplyBubbleSameTeamRepulsion(bubble);

            if (TryHandleBubbleProjectileCollision(bubble) || TryDamageBubbleStructureTarget(bubble, owner))
            {
                RemoveBubbleAt(bubbleIndex);
                continue;
            }

            if (bubble.IsExpired)
            {
                RemoveBubbleAt(bubbleIndex);
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
                    if (hitResult.HitPlayer.ApplyDamage(blade.HitDamage, PlayerEntity.SpyDamageRevealAlpha))
                        {
                            KillPlayer(hitResult.HitPlayer, killer: FindPlayerById(blade.OwnerId), weaponSpriteName: "BladeKL");
                        }
                    }
                    else if (hitResult.HitSentry is not null && hitResult.HitSentry.ApplyDamage(blade.HitDamage))
                    {
                        DestroySentry(hitResult.HitSentry);
                    }
                else if (hitResult.HitGenerator is not null)
                {
                    TryDamageGenerator(hitResult.HitGenerator.Team, blade.HitDamage);
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
                    if (hitResult.HitPlayer.ApplyDamage(NeedleProjectileEntity.DamagePerHit, PlayerEntity.SpyDamageRevealAlpha))
                    {
                        KillPlayer(hitResult.HitPlayer, killer: FindPlayerById(needle.OwnerId), weaponSpriteName: "NeedleKL");
                    }
                }
                else if (hitResult.HitSentry is not null && hitResult.HitSentry.ApplyDamage(NeedleProjectileEntity.DamagePerHit))
                {
                    DestroySentry(hitResult.HitSentry);
                }
                else if (hitResult.HitGenerator is not null)
                {
                    TryDamageGenerator(hitResult.HitGenerator.Team, NeedleProjectileEntity.DamagePerHit);
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
                    if (hitResult.HitPlayer.ApplyDamage(RevolverProjectileEntity.DamagePerHit, PlayerEntity.SpyDamageRevealAlpha))
                    {
                        KillPlayer(hitResult.HitPlayer, killer: FindPlayerById(shot.OwnerId), weaponSpriteName: "RevolverKL");
                    }
                }
                else if (hitResult.HitSentry is not null && hitResult.HitSentry.ApplyDamage(RevolverProjectileEntity.DamagePerHit))
                {
                    DestroySentry(hitResult.HitSentry);
                }
                else if (hitResult.HitGenerator is not null)
                {
                    TryDamageGenerator(hitResult.HitGenerator.Team, RevolverProjectileEntity.DamagePerHit);
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
                    if (hitResult.HitPlayer.ApplyDamage(StabMaskEntity.DamagePerHit, PlayerEntity.SpyDamageRevealAlpha))
                    {
                        KillPlayer(hitResult.HitPlayer, killer: owner, weaponSpriteName: "KnifeKL");
                    }
                }
                else if (hitResult.HitSentry is not null && hitResult.HitSentry.ApplyDamage(StabMaskEntity.DamagePerHit))
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
                    var playerDied = hitPlayer.ApplyContinuousDamage(FlameProjectileEntity.DirectHitDamage);
                    if (playerDied)
                    {
                        KillPlayer(hitPlayer, killer: FindPlayerById(flame.OwnerId), weaponSpriteName: "FlameKL");
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
                else if (hitResult.HitSentry is not null && hitResult.HitSentry.ApplyDamage((int)FlameProjectileEntity.DirectHitDamage))
                {
                    DestroySentry(hitResult.HitSentry);
                }
                else if (hitResult.HitGenerator is not null)
                {
                    TryDamageGenerator(hitResult.HitGenerator.Team, (int)FlameProjectileEntity.DirectHitDamage);
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

    private void AdvanceRockets()
    {
        var deltaSeconds = (float)Config.FixedDeltaSeconds;
        for (var rocketIndex = _rockets.Count - 1; rocketIndex >= 0; rocketIndex -= 1)
        {
            var rocket = _rockets[rocketIndex];
            if (FindPlayerById(rocket.RangeAnchorOwnerId) is { } rangeAnchorPlayer)
            {
                rocket.RefreshRangeOrigin(rangeAnchorPlayer.X, rangeAnchorPlayer.Y);
            }

            if (rocket.IsFading)
            {
                rocket.AdvanceFade(deltaSeconds);
                if (rocket.IsExpired)
                {
                    RemoveRocketAt(rocketIndex);
                    continue;
                }
            }
            else
            {
                rocket.TryBeginFadeFromSourceRange();
            }

            if (rocket.ExplodeImmediately)
            {
                rocket.ClearDelayedExplosion();
                if (rocket.IsFading)
                {
                    RemoveRocketAt(rocketIndex);
                }
                else
                {
                    ExplodeRocket(rocket, directHitPlayer: null, directHitSentry: null, directHitGenerator: null);
                }

                continue;
            }

            rocket.AdvanceOneTick(deltaSeconds);
            var movementX = rocket.X - rocket.PreviousX;
            var movementY = rocket.Y - rocket.PreviousY;
            var movementDistance = MathF.Sqrt((movementX * movementX) + (movementY * movementY));
            if (movementDistance <= 0.0001f)
            {
                if (rocket.IsExpired)
                {
                    RemoveRocketAt(rocketIndex);
                }

                continue;
            }

            var directionX = movementX / movementDistance;
            var directionY = movementY / movementDistance;
            var hit = GetNearestRocketHit(rocket, directionX, directionY, movementDistance);
            if (hit.HasValue)
            {
                var hitResult = hit.Value;
                rocket.MoveTo(hitResult.HitX, hitResult.HitY);
                RegisterCombatTrace(rocket.PreviousX, rocket.PreviousY, directionX, directionY, hitResult.Distance, hitResult.HitPlayer is not null);
                if (rocket.IsFading
                    && hitResult.HitPlayer is null
                    && hitResult.HitSentry is null
                    && hitResult.HitGenerator is null)
                {
                    RemoveRocketAt(rocketIndex);
                }
                else
                {
                    ExplodeRocket(rocket, hitResult.HitPlayer, hitResult.HitSentry, hitResult.HitGenerator);
                }
            }
            else
            {
                RegisterRocketFriendlyPassThroughs(rocket, directionX, directionY, movementDistance);
                if (rocket.IsExpired)
                {
                    RemoveRocketAt(rocketIndex);
                }
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
                    var playerDied = hitResult.HitPlayer.ApplyDamage(FlareProjectileEntity.DamagePerHit, PlayerEntity.SpyDamageRevealAlpha);
                    if (playerDied)
                    {
                        KillPlayer(hitResult.HitPlayer, killer: FindPlayerById(flare.OwnerId), weaponSpriteName: "FlareKL");
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
                else if (hitResult.HitSentry is not null && hitResult.HitSentry.ApplyDamage(FlareProjectileEntity.DamagePerHit))
                {
                    DestroySentry(hitResult.HitSentry);
                }
                else if (hitResult.HitGenerator is not null)
                {
                    TryDamageGenerator(hitResult.HitGenerator.Team, FlareProjectileEntity.DamagePerHit);
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

    private void RegisterRocketFriendlyPassThroughs(RocketProjectileEntity rocket, float directionX, float directionY, float maxDistance)
    {
        if (rocket.IsFading || maxDistance <= 0.0001f)
        {
            return;
        }

        var endX = rocket.PreviousX + (directionX * maxDistance);
        var endY = rocket.PreviousY + (directionY * maxDistance);
        List<(int PlayerId, float Distance)>? passThroughs = null;
        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive || player.Team != rocket.Team || player.Id == rocket.OwnerId)
            {
                continue;
            }

            var distance = GetLineIntersectionDistanceToPlayer(
                rocket.PreviousX,
                rocket.PreviousY,
                endX,
                endY,
                player,
                maxDistance);
            if (!distance.HasValue)
            {
                continue;
            }

            passThroughs ??= [];
            passThroughs.Add((player.Id, distance.Value));
        }

        if (passThroughs is null)
        {
            return;
        }

        passThroughs.Sort(static (left, right) => left.Distance.CompareTo(right.Distance));
        for (var index = 0; index < passThroughs.Count; index += 1)
        {
            rocket.TryRegisterFriendlyPassThrough(passThroughs[index].PlayerId);
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

    private void RemoveBubbleAt(int bubbleIndex)
    {
        var bubble = _bubbles[bubbleIndex];
        if (FindPlayerById(bubble.OwnerId) is { } owner)
        {
            owner.DecrementQuoteBubbleCount();
        }

        RegisterVisualEffect("Pop", bubble.X, bubble.Y);
        _entities.Remove(bubble.Id);
        _bubbles.RemoveAt(bubbleIndex);
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

    private void RemoveRocketAt(int rocketIndex)
    {
        var rocket = _rockets[rocketIndex];
        _entities.Remove(rocket.Id);
        _rockets.RemoveAt(rocketIndex);
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

    private bool IsBubbleTouchingEnvironment(BubbleProjectileEntity bubble)
    {
        return IsBubbleTouchingEnvironmentAt(bubble.X, bubble.Y);
    }

    private bool IsBubbleTouchingEnvironmentAt(float x, float y)
    {
        foreach (var solid in Level.Solids)
        {
            if (CircleIntersectsRectangle(x, y, BubbleProjectileEntity.Radius, solid.Left, solid.Top, solid.Right, solid.Bottom))
            {
                return true;
            }
        }

        foreach (var roomObject in Level.RoomObjects)
        {
            if (!IsBubbleBlockingRoomObject(roomObject))
            {
                continue;
            }

            if (CircleIntersectsRectangle(x, y, BubbleProjectileEntity.Radius, roomObject.Left, roomObject.Top, roomObject.Right, roomObject.Bottom))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryResolveBubbleEnvironmentCollision(BubbleProjectileEntity bubble)
    {
        if (!IsBubbleTouchingEnvironment(bubble))
        {
            return false;
        }

        if (!IsBubbleTouchingEnvironmentAt(bubble.X, bubble.Y + 6f))
        {
            bubble.MoveTo(bubble.X, bubble.Y + 6f);
            return false;
        }

        if (!IsBubbleTouchingEnvironmentAt(bubble.X, bubble.Y - 6f))
        {
            bubble.MoveTo(bubble.X, bubble.Y - 6f);
            return false;
        }

        var canKeepHorizontalMove = !IsBubbleTouchingEnvironmentAt(bubble.X, bubble.PreviousY);
        var canKeepVerticalMove = !IsBubbleTouchingEnvironmentAt(bubble.PreviousX, bubble.Y);
        if (canKeepHorizontalMove && !canKeepVerticalMove)
        {
            bubble.MoveTo(bubble.X, bubble.PreviousY);
            bubble.SetVelocity(bubble.VelocityX, 0f);
            return false;
        }

        if (canKeepVerticalMove && !canKeepHorizontalMove)
        {
            bubble.MoveTo(bubble.PreviousX, bubble.Y);
            bubble.SetVelocity(0f, bubble.VelocityY);
            return false;
        }

        bubble.Destroy();
        return true;
    }

    private bool IsBubbleBlockingRoomObject(RoomObjectMarker roomObject)
    {
        return roomObject.Type switch
        {
            RoomObjectType.TeamGate => true,
            RoomObjectType.ControlPointSetupGate => Level.ControlPointSetupGatesActive,
            RoomObjectType.BulletWall => true,
            _ => false,
        };
    }

    private bool TryDamageBubbleEnemyPlayer(BubbleProjectileEntity bubble, PlayerEntity owner)
    {
        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive || player.Team == bubble.Team || player.Id == bubble.OwnerId || !CircleIntersectsPlayer(bubble.X, bubble.Y, BubbleProjectileEntity.Radius, player))
            {
                continue;
            }

            if (player.ApplyContinuousDamage(BubbleProjectileEntity.DamagePerHit))
            {
                    KillPlayer(player, killer: owner, weaponSpriteName: "BladeKL");
            }

            return true;
        }

        return false;
    }

    private void ApplyBubbleSameTeamRepulsion(BubbleProjectileEntity bubble)
    {
        for (var bubbleIndex = 0; bubbleIndex < _bubbles.Count; bubbleIndex += 1)
        {
            var otherBubble = _bubbles[bubbleIndex];
            if (otherBubble.Id == bubble.Id || otherBubble.Team != bubble.Team)
            {
                continue;
            }

            if (DistanceBetween(bubble.X, bubble.Y, otherBubble.X, otherBubble.Y) > BubbleProjectileEntity.Radius * 2f)
            {
                continue;
            }

            bubble.AddRepulsionFrom(otherBubble.X, otherBubble.Y);
        }
    }

    private bool TryDamageBubbleStructureTarget(BubbleProjectileEntity bubble, PlayerEntity owner)
    {
        foreach (var sentry in _sentries)
        {
            if (sentry.Team == bubble.Team || !CircleIntersectsRectangle(bubble.X, bubble.Y, BubbleProjectileEntity.Radius, sentry.X - 12f, sentry.Y - 12f, sentry.X + 12f, sentry.Y + 12f))
            {
                continue;
            }

            if (sentry.ApplyDamage((int)MathF.Ceiling(BubbleProjectileEntity.DamagePerHit)))
            {
                DestroySentry(sentry);
            }

            return true;
        }

        for (var index = 0; index < _generators.Count; index += 1)
        {
            var generator = _generators[index];
            if (generator.Team == bubble.Team
                || generator.IsDestroyed
                || !CircleIntersectsRectangle(bubble.X, bubble.Y, BubbleProjectileEntity.Radius, generator.Marker.Left, generator.Marker.Top, generator.Marker.Right, generator.Marker.Bottom))
            {
                continue;
            }

            TryDamageGenerator(generator.Team, BubbleProjectileEntity.DamagePerHit);
            return true;
        }

        return false;
    }

    private bool TryHandleBubbleProjectileCollision(BubbleProjectileEntity bubble)
    {
        for (var rocketIndex = _rockets.Count - 1; rocketIndex >= 0; rocketIndex -= 1)
        {
            var rocket = _rockets[rocketIndex];
            if (rocket.Team != bubble.Team && DistanceBetween(bubble.X, bubble.Y, rocket.X, rocket.Y) <= 10f)
            {
                return true;
            }
        }

        for (var mineIndex = _mines.Count - 1; mineIndex >= 0; mineIndex -= 1)
        {
            var mine = _mines[mineIndex];
            if (mine.Team != bubble.Team && DistanceBetween(bubble.X, bubble.Y, mine.X, mine.Y) <= 10f)
            {
                if (!mine.IsStickied)
                {
                    mine.SetVelocity(-mine.VelocityX, -mine.VelocityY);
                }

                bubble.HalveLifetimeOrDestroy(GetSimulationTicksFromSourceTicks(10f));
                return bubble.IsExpired;
            }
        }

        for (var flameIndex = _flames.Count - 1; flameIndex >= 0; flameIndex -= 1)
        {
            var flame = _flames[flameIndex];
            if (flame.Team != bubble.Team && DistanceBetween(bubble.X, bubble.Y, flame.X, flame.Y) <= 8f)
            {
                return true;
            }
        }

        for (var flareIndex = _flares.Count - 1; flareIndex >= 0; flareIndex -= 1)
        {
            var flare = _flares[flareIndex];
            if (flare.Team != bubble.Team && DistanceBetween(bubble.X, bubble.Y, flare.X, flare.Y) <= 8f)
            {
                RemoveFlareAt(flareIndex);
                return true;
            }
        }

        for (var shotIndex = _shots.Count - 1; shotIndex >= 0; shotIndex -= 1)
        {
            if (_shots[shotIndex].Team != bubble.Team && DistanceBetween(bubble.X, bubble.Y, _shots[shotIndex].X, _shots[shotIndex].Y) <= 8f)
            {
                return true;
            }
        }

        for (var needleIndex = _needles.Count - 1; needleIndex >= 0; needleIndex -= 1)
        {
            if (_needles[needleIndex].Team != bubble.Team && DistanceBetween(bubble.X, bubble.Y, _needles[needleIndex].X, _needles[needleIndex].Y) <= 8f)
            {
                return true;
            }
        }

        for (var revolverIndex = _revolverShots.Count - 1; revolverIndex >= 0; revolverIndex -= 1)
        {
            if (_revolverShots[revolverIndex].Team != bubble.Team && DistanceBetween(bubble.X, bubble.Y, _revolverShots[revolverIndex].X, _revolverShots[revolverIndex].Y) <= 8f)
            {
                return true;
            }
        }

        for (var bladeIndex = _blades.Count - 1; bladeIndex >= 0; bladeIndex -= 1)
        {
            var blade = _blades[bladeIndex];
            if ((blade.Team != bubble.Team || blade.OwnerId == bubble.OwnerId)
                && DistanceBetween(bubble.X, bubble.Y, blade.X, blade.Y) <= 10f)
            {
                return true;
            }
        }

        for (var bubbleIndex = 0; bubbleIndex < _bubbles.Count; bubbleIndex += 1)
        {
            var otherBubble = _bubbles[bubbleIndex];
            if (otherBubble.Id == bubble.Id || otherBubble.Team == bubble.Team)
            {
                continue;
            }

            if (DistanceBetween(bubble.X, bubble.Y, otherBubble.X, otherBubble.Y) <= BubbleProjectileEntity.Radius * 2f)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryCutBubbleWithBlade(BladeProjectileEntity blade)
    {
        for (var bubbleIndex = _bubbles.Count - 1; bubbleIndex >= 0; bubbleIndex -= 1)
        {
            var bubble = _bubbles[bubbleIndex];
            if (bubble.Team == blade.Team && bubble.OwnerId != blade.OwnerId)
            {
                continue;
            }

            if (DistanceBetween(blade.X, blade.Y, bubble.X, bubble.Y) > 10f)
            {
                continue;
            }

            RemoveBubbleAt(bubbleIndex);
            return true;
        }

        return false;
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

    private readonly record struct BubbleHitResult(int BubbleIndex, float Distance, float HitX, float HitY);

    private BubbleHitResult? GetNearestEnemyBubbleHit(float originX, float originY, float directionX, float directionY, float maxDistance, PlayerTeam projectileTeam)
    {
        BubbleHitResult? nearestHit = null;
        for (var bubbleIndex = 0; bubbleIndex < _bubbles.Count; bubbleIndex += 1)
        {
            var bubble = _bubbles[bubbleIndex];
            if (bubble.Team == projectileTeam)
            {
                continue;
            }

            var distance = GetRayIntersectionDistanceWithCircle(originX, originY, directionX, directionY, bubble.X, bubble.Y, BubbleProjectileEntity.Radius, maxDistance);
            if (!distance.HasValue)
            {
                continue;
            }

            if (nearestHit.HasValue && nearestHit.Value.Distance <= distance.Value)
            {
                continue;
            }

            nearestHit = new BubbleHitResult(
                bubbleIndex,
                distance.Value,
                originX + directionX * distance.Value,
                originY + directionY * distance.Value);
        }

        return nearestHit;
    }

    private static float? GetRayIntersectionDistanceWithCircle(float originX, float originY, float directionX, float directionY, float centerX, float centerY, float radius, float maxDistance)
    {
        var offsetX = originX - centerX;
        var offsetY = originY - centerY;
        var b = 2f * ((directionX * offsetX) + (directionY * offsetY));
        var c = (offsetX * offsetX) + (offsetY * offsetY) - (radius * radius);
        var discriminant = (b * b) - 4f * c;
        if (discriminant < 0f)
        {
            return null;
        }

        var sqrtDiscriminant = MathF.Sqrt(discriminant);
        var candidateA = (-b - sqrtDiscriminant) * 0.5f;
        if (candidateA >= 0f && candidateA <= maxDistance)
        {
            return candidateA;
        }

        var candidateB = (-b + sqrtDiscriminant) * 0.5f;
        return candidateB >= 0f && candidateB <= maxDistance
            ? candidateB
            : null;
    }
}

