namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void AdvanceFlames()
    {
        var deltaSeconds = (float)Config.FixedDeltaSeconds;
        var flameAirLifetimeTicks = GetSimulationTicksFromSourceTicks(FlameProjectileEntity.AirLifetimeTicks);
        for (var flameIndex = _flames.Count - 1; flameIndex >= 0; flameIndex -= 1)
        {
            var flame = _flames[flameIndex];
            flame.AdvanceOneTick(deltaSeconds, _configuredGravityScale);
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
                    var playerDied = ApplyPlayerContinuousDamage(hitPlayer, flame.DirectHitDamageValue, owner);
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

                    if (flame.HitPlayerCount >= FlameProjectileEntity.PenetrationCap && !flame.IsPerseverant)
                    {
                        flame.Destroy();
                    }
                    else
                    {
                        flame.RegisterHitPlayer(hitPlayer.Id);
                        flame.MoveTo(hitResult.HitX + directionX, hitResult.HitY + directionY);
                    }
                }
                else if (hitResult.HitSentry is not null && ApplySentryDamage(hitResult.HitSentry, (int)flame.DirectHitDamageValue, FindPlayerById(flame.OwnerId)))
                {
                    DestroySentry(hitResult.HitSentry, FindPlayerById(flame.OwnerId));
                    flame.Destroy();
                }
                else if (hitResult.HitGenerator is not null)
                {
                    TryDamageGenerator(hitResult.HitGenerator.Team, (int)flame.DirectHitDamageValue, FindPlayerById(flame.OwnerId));
                    flame.Destroy();
                }
                else
                {
                    flame.Destroy();
                }

                RegisterCombatTrace(flame.PreviousX, flame.PreviousY, directionX, directionY, hitResult.Distance, hitResult.HitPlayer is not null);
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
                    var hitDamage = ApplyExperimentalAirshotDamageMultiplier(owner, hitResult.HitPlayer, FlareProjectileEntity.DamagePerHit, out var damageFlags);
                    var playerDied = ApplyPlayerDamage(hitResult.HitPlayer, hitDamage, owner, PlayerEntity.SpyDamageRevealAlpha, damageFlags);
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
                    DestroySentry(hitResult.HitSentry, FindPlayerById(flare.OwnerId));
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
            mine.AdvanceOneTick(_configuredGravityScale);
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
            var hitX = hitResult.HitX;
            var hitY = hitResult.HitY;
            if (!hitResult.DestroyOnHit)
            {
                // Stickies in GG2 back out of solid geometry before arming, which keeps their
                // center on the playable side of the surface for consistent sticky jumps.
                var backoffDistance = MathF.Min(hitResult.Distance, MineProjectileEntity.EnvironmentCollisionBackoffDistance);
                hitX -= directionX * backoffDistance;
                hitY -= directionY * backoffDistance;
            }

            mine.MoveTo(hitX, hitY);
            if (hitResult.DestroyOnHit)
            {
                RemoveMineAt(mineIndex);
                continue;
            }

            mine.Stick();
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
}
