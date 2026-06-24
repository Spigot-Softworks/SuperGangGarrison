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
            if (!ShouldAdvanceProjectileForClientPrediction(flame.OwnerId))
            {
                continue;
            }

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
                var owner = FindPlayerById(flame.OwnerId);
                flame.MoveTo(hitResult.HitX, hitResult.HitY);
                if (hitResult.HitPlayer is not null)
                {
                    var hitPlayer = hitResult.HitPlayer;
                    var shieldChargeBefore = hitPlayer.CivvieUmbrellaChargeTicks;
                    var playerDied = ApplyPlayerContinuousDamage(
                        hitPlayer,
                        flame.DirectHitDamageValue * flame.CriticalDamageMultiplier,
                        owner,
                        civvieUmbrellaThreatSourceX: flame.PreviousX,
                        civvieUmbrellaThreatSourceY: flame.PreviousY,
                        civvieUmbrellaCriticalBoost: PlayerEntity.IsCriticalDamageMultiplierBoosted(flame.CriticalDamageMultiplier));
                    var umbrellaBlockedFlame = hitPlayer.CivvieUmbrellaChargeTicks < shieldChargeBefore;
                    if (playerDied)
                    {
                        KillPlayer(hitPlayer, killer: owner, weaponSpriteName: "FlameKL");
                    }
                    else if (!umbrellaBlockedFlame)
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
                else if (hitResult.HitSentry is not null && ApplySentryDamage(hitResult.HitSentry, (int)(flame.DirectHitDamageValue * flame.CriticalDamageMultiplier), owner))
                {
                    DestroySentry(hitResult.HitSentry, owner);
                    flame.Destroy();
                }
                else if (hitResult.HitGenerator is not null)
                {
                    TryDamageGenerator(hitResult.HitGenerator.Team, (int)(flame.DirectHitDamageValue * flame.CriticalDamageMultiplier), owner);
                    flame.Destroy();
                }
                else if (hitResult.HitJumpPad is not null)
                {
                    hitResult.HitJumpPad.TakeDamage((int)(flame.DirectHitDamageValue * flame.CriticalDamageMultiplier));
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
            if (!ShouldAdvanceProjectileForClientPrediction(flare.OwnerId))
            {
                continue;
            }

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
                var owner = FindPlayerById(flare.OwnerId);
                flare.MoveTo(hitResult.HitX, hitResult.HitY);
                RegisterCombatTrace(flare.PreviousX, flare.PreviousY, directionX, directionY, hitResult.Distance, hitResult.HitPlayer is not null);
                if (hitResult.HitPlayer is not null)
                {
                    if (!TryAbsorbCivvieUmbrellaProjectileContact(
                            hitResult.HitPlayer,
                            flare.OwnerId,
                            hitResult.HitX,
                            hitResult.HitY,
                            criticalBoost: PlayerEntity.IsCriticalDamageMultiplierBoosted(flare.CriticalDamageMultiplier)))
                    {
                        RegisterBloodEffect(hitResult.HitPlayer.X, hitResult.HitPlayer.Y, MathF.Atan2(directionY, directionX) * (180f / MathF.PI) - 180f);
                        var hitDamage = ApplyExperimentalAirshotDamageMultiplier(owner, hitResult.HitPlayer, (int)MathF.Round(FlareProjectileEntity.DamagePerHit * flare.CriticalDamageMultiplier), out var damageFlags);
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
                }
                else if (hitResult.HitSentry is not null && ApplySentryDamage(hitResult.HitSentry, (int)MathF.Round(FlareProjectileEntity.DamagePerHit * flare.CriticalDamageMultiplier), owner))
                {
                    DestroySentry(hitResult.HitSentry, owner);
                }
                else if (hitResult.HitGenerator is not null)
                {
                    TryDamageGenerator(hitResult.HitGenerator.Team, FlareProjectileEntity.DamagePerHit * flare.CriticalDamageMultiplier, owner);
                }
                else if (hitResult.HitJumpPad is not null)
                {
                    hitResult.HitJumpPad.TakeDamage((int)MathF.Round(FlareProjectileEntity.DamagePerHit * flare.CriticalDamageMultiplier));
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
            if (!ShouldAdvanceProjectileForClientPrediction(mine.OwnerId))
            {
                continue;
            }

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

    private void AdvanceGrenades()
    {
        for (var grenadeIndex = _grenades.Count - 1; grenadeIndex >= 0; grenadeIndex -= 1)
        {
            var grenade = _grenades[grenadeIndex];
            if (!ShouldAdvanceProjectileForClientPrediction(grenade.OwnerId))
            {
                continue;
            }

            grenade.AdvanceOneTick(_configuredGravityScale);

            // Check if fuse has expired
            if (grenade.FuseTicksLeft <= 0)
            {
                ExplodeGrenade(grenade);
                RemoveGrenadeAt(grenadeIndex);
                continue;
            }

            // Swept movement vector
            var movementX = grenade.X - grenade.PreviousX;
            var movementY = grenade.Y - grenade.PreviousY;
            var movementDistance = MathF.Sqrt((movementX * movementX) + (movementY * movementY));
            var directionX = movementDistance > 0.0001f ? movementX / movementDistance : 0f;
            var directionY = movementDistance > 0.0001f ? movementY / movementDistance : 0f;

            // Check for player/building collisions along swept path (instant explosion)
            var directHitPlayer = movementDistance > 0.0001f
                ? GetNearestGrenadePlayerHit(grenade, directionX, directionY, movementDistance)
                : null;
            if (directHitPlayer is not null)
            {
                ExplodeGrenade(grenade, directHitPlayer: directHitPlayer);
                RemoveGrenadeAt(grenadeIndex);
                continue;
            }

            if (CheckGrenadeBuildingCollision(grenade, out var directHitBuilding))
            {
                ExplodeGrenade(grenade, directHitBuilding: directHitBuilding);
                RemoveGrenadeAt(grenadeIndex);
                continue;
            }

            if (movementDistance > 0.0001f
                && TryGetGrenadeDamageableZoneContact(
                    grenade,
                    directionX,
                    directionY,
                    movementDistance,
                    out var damageableHitX,
                    out var damageableHitY,
                    out var damageableZoneIndex))
            {
                grenade.MoveTo(damageableHitX, damageableHitY);
                ExplodeGrenade(grenade, directHitDamageableZoneIndex: damageableZoneIndex);
                RemoveGrenadeAt(grenadeIndex);
                continue;
            }

            // Swept environment collision — bounce off walls
            if (movementDistance > 0.0001f)
            {
                var envHit = GetNearestGrenadeEnvironmentHit(grenade, directionX, directionY, movementDistance);
                if (envHit.HasValue)
                {
                    // Place the grenade at the hit surface, backed off slightly so it doesn't embed
                    var backoffX = -directionX * GrenadeProjectileEntity.EnvironmentCollisionBackoffDistance;
                    var backoffY = -directionY * GrenadeProjectileEntity.EnvironmentCollisionBackoffDistance;
                    grenade.MoveTo(envHit.Value.HitX + backoffX, envHit.Value.HitY + backoffY);
                    grenade.Bounce(envHit.Value.NormalX, envHit.Value.NormalY);
                    // Visual-only random spin after bounce; magnitude scales with impact speed so slow-rolling grenades don't spin
                    const float rotationImpulseReferenceSpeed = 12f;
                    var speedFactor = float.Min(1f, movementDistance / rotationImpulseReferenceSpeed);
                    var impulse = (Random.Shared.NextSingle() - 0.5f) * 0.9f * speedFactor;
                    grenade.ApplyRotationImpulse(impulse);
                }
            }
        }
    }

    private bool CheckGrenadePlayerCollision(GrenadeProjectileEntity grenade, out PlayerEntity? hitPlayer)
    {
        hitPlayer = null;
        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive || player.Team == grenade.Team)
            {
                continue;
            }

            var deltaX = grenade.X - player.X;
            var deltaY = grenade.Y - player.Y;
            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);

            if (distanceSquared < 100f) // ~10 pixel collision radius
            {
                hitPlayer = player;
                return true;
            }
        }
        return false;
    }

    private bool CheckGrenadeBuildingCollision(GrenadeProjectileEntity grenade, out SimulationEntity? hitBuilding)
    {
        hitBuilding = null;

        // Check sentries
        foreach (var sentry in _sentries)
        {
            if (sentry.Health <= 0 || sentry.Team == grenade.Team)
            {
                continue;
            }

            var deltaX = grenade.X - sentry.X;
            var deltaY = grenade.Y - sentry.Y;
            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);

            if (distanceSquared < 400f) // ~20 pixel collision radius for sentries
            {
                hitBuilding = sentry;
                return true;
            }
        }

        // Check jump pads
        foreach (var jumpPad in _jumpPads)
        {
            if (jumpPad.IsDead || jumpPad.Team == grenade.Team)
            {
                continue;
            }

            var deltaX = grenade.X - jumpPad.X;
            var deltaY = grenade.Y - jumpPad.Y;
            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);

            if (distanceSquared < 400f) // ~20 pixel collision radius
            {
                hitBuilding = jumpPad;
                return true;
            }
        }

        return false;
    }

    private void ExplodeGrenade(
        GrenadeProjectileEntity grenade,
        PlayerEntity? directHitPlayer = null,
        SimulationEntity? directHitBuilding = null,
        int directHitDamageableZoneIndex = -1)
    {
        if (ClientPredictionMode)
        {
            RegisterWorldSoundEvent("ExplosionSnd", grenade.X, grenade.Y);
            RegisterVisualEffect("Explosion", grenade.X, grenade.Y);
            return;
        }

        var owner = FindPlayerById(grenade.OwnerId);

        RegisterWorldSoundEvent("ExplosionSnd", grenade.X, grenade.Y);
        RegisterVisualEffect("Explosion", grenade.X, grenade.Y);
        ApplyDeadBodyExplosionImpulse(grenade.X, grenade.Y, GrenadeProjectileEntity.BlastRadius * 0.75f, 10f, GrenadeProjectileEntity.BlastRadius);
        ApplyPlayerGibExplosionImpulse(grenade.X, grenade.Y, GrenadeProjectileEntity.BlastRadius * 0.75f, 15f, GrenadeProjectileEntity.BlastRadius);
        RegisterExplosionTraces(grenade.X, grenade.Y);

        // Damage players
        var playersSnapshot = EnumerateSimulatedPlayers().ToArray();
        foreach (var player in playersSnapshot)
        {
            if (!player.IsAlive)
            {
                continue;
            }

            var distance = GetExplosionDistanceToPlayer(this, player, grenade.X, grenade.Y);
            if (distance >= GrenadeProjectileEntity.BlastRadius)
            {
                continue;
            }

            if (ShouldIgnoreFriendlyGroundedBlast(player, grenade.Team, grenade.OwnerId))
            {
                continue;
            }

            var factor = 1f - (distance / GrenadeProjectileEntity.BlastRadius);
            if (factor <= GrenadeProjectileEntity.SplashThresholdFactor)
            {
                continue;
            }

            if (ShouldSkipFriendlyExplosionBoost(player, grenade.Team, grenade.OwnerId))
            {
                continue;
            }

            ApplyMineExplosionImpulse(player, grenade.X, grenade.Y, factor);
            if (player.Id == grenade.OwnerId && player.Team == grenade.Team)
            {
                player.SetMovementStateIfAirborne(LegacyMovementState.ExplosionRecovery);
            }
            else
            {
                player.SetMovementStateIfAirborne(LegacyMovementState.FriendlyJuggle);
            }

            if (CanTeamDamagePlayer(grenade.Team, grenade.OwnerId, player))
            {
                if (ReferenceEquals(player, directHitPlayer))
                {
                    continue;
                }

                RegisterBloodEffect(player.X, player.Y, PointDirectionDegrees(grenade.X, grenade.Y, player.X, player.Y) - 180f, 3);
                var critMultiplier = (player.Id == grenade.OwnerId && player.Team == grenade.Team) ? 1f : grenade.CriticalDamageMultiplier;
                var maxSplashDamage = grenade.ExplosionDamage * critMultiplier;
                if (player.Id == grenade.OwnerId && player.Team == grenade.Team)
                {
                    maxSplashDamage *= GrenadeProjectileEntity.SelfDamageScale;
                }

                var damage = maxSplashDamage * factor;
                if (ApplyPlayerContinuousDamage(
                        player,
                        damage,
                        owner,
                        PlayerEntity.SpyMineRevealAlpha,
                        civvieUmbrellaThreatSourceX: grenade.X,
                        civvieUmbrellaThreatSourceY: grenade.Y,
                        civvieUmbrellaDrainTicks: PlayerEntity.GetCivvieUmbrellaSplashExplosionDrainTicksFromDamage(damage, maxSplashDamage),
                        civvieUmbrellaCriticalBoost: PlayerEntity.IsCriticalDamageMultiplierBoosted(critMultiplier)))
                {
                    KillPlayer(
                        player,
                        gibbed: true,
                        killer: owner,
                        weaponSpriteName: grenade.KillFeedWeaponSpriteNameOverride ?? "GrenadeLauncherKL");
                }
            }
        }

        if (directHitPlayer is not null)
        {
            ApplyGrenadeDirectImpactDamage(grenade, owner, directHitPlayer);
        }

        // Damage sentries
        for (var sentryIndex = _sentries.Count - 1; sentryIndex >= 0; sentryIndex -= 1)
        {
            var sentry = _sentries[sentryIndex];
            var distance = DistanceBetween(grenade.X, grenade.Y, sentry.X, sentry.Y);
            if (distance >= GrenadeProjectileEntity.BlastRadius || sentry.Team == grenade.Team)
            {
                continue;
            }

            var factor = 1f - (distance / GrenadeProjectileEntity.BlastRadius);
            if (factor <= GrenadeProjectileEntity.SplashThresholdFactor)
            {
                continue;
            }

            if (ReferenceEquals(sentry, directHitBuilding))
            {
                continue;
            }

            var damage = grenade.ExplosionDamage * GrenadeProjectileEntity.SentryDamageMultiplier * grenade.CriticalDamageMultiplier * factor;
            if (ApplySentryDamage(sentry, (int)MathF.Ceiling(damage), owner))
            {
                DestroySentry(sentry, owner);
            }
        }

        // Damage jump pads
        for (var jumpPadIndex = _jumpPads.Count - 1; jumpPadIndex >= 0; jumpPadIndex -= 1)
        {
            var jumpPad = _jumpPads[jumpPadIndex];
            var distance = DistanceBetween(grenade.X, grenade.Y, jumpPad.X, jumpPad.Y);
            if (distance >= GrenadeProjectileEntity.BlastRadius || jumpPad.Team == grenade.Team || jumpPad.IsDead)
            {
                continue;
            }

            var factor = 1f - (distance / GrenadeProjectileEntity.BlastRadius);
            if (factor <= GrenadeProjectileEntity.SplashThresholdFactor)
            {
                continue;
            }

            if (ReferenceEquals(jumpPad, directHitBuilding))
            {
                continue;
            }

            var damage = grenade.ExplosionDamage * grenade.CriticalDamageMultiplier * factor;
            jumpPad.TakeDamage((int)MathF.Ceiling(damage));
        }

        if (directHitBuilding is not null)
        {
            ApplyGrenadeDirectImpactDamage(grenade, owner, directHitBuilding);
        }

        if (directHitDamageableZoneIndex >= 0)
        {
            TryApplyDamageableZoneDamage(
                directHitDamageableZoneIndex,
                GrenadeProjectileEntity.DirectHitDamage * grenade.CriticalDamageMultiplier,
                grenade.Team);
        }

        ApplyExplosiveDamageToDamageableZones(
            grenade.X,
            grenade.Y,
            GrenadeProjectileEntity.BlastRadius,
            grenade.ExplosionDamage * grenade.CriticalDamageMultiplier,
            GrenadeProjectileEntity.SplashThresholdFactor,
            directHitDamageableZoneIndex,
            grenade.Team);
    }

    private void ApplyGrenadeDirectImpactDamage(GrenadeProjectileEntity grenade, PlayerEntity? owner, PlayerEntity target)
    {
        if (!target.IsAlive || !CanTeamDamagePlayer(grenade.Team, grenade.OwnerId, target))
        {
            return;
        }

        RegisterBloodEffect(target.X, target.Y, PointDirectionDegrees(grenade.X, grenade.Y, target.X, target.Y) - 180f, 3);
        var damage = Math.Max(1, (int)MathF.Round(GrenadeProjectileEntity.DirectHitDamage * grenade.CriticalDamageMultiplier));
        if (ApplyPlayerDamage(
                target,
                damage,
                owner,
                PlayerEntity.SpyMineRevealAlpha,
                civvieUmbrellaThreatSourceX: grenade.PreviousX,
                civvieUmbrellaThreatSourceY: grenade.PreviousY,
                civvieUmbrellaDrainTicks: PlayerEntity.CivvieUmbrellaDirectExplosionDrainTicks,
                civvieUmbrellaCriticalBoost: PlayerEntity.IsCriticalDamageMultiplierBoosted(grenade.CriticalDamageMultiplier)))
        {
            KillPlayer(
                target,
                gibbed: true,
                killer: owner,
                weaponSpriteName: grenade.KillFeedWeaponSpriteNameOverride ?? "GrenadeLauncherKL");
        }
    }

    private void ApplyGrenadeDirectImpactDamage(GrenadeProjectileEntity grenade, PlayerEntity? owner, SimulationEntity target)
    {
        var damage = Math.Max(1, (int)MathF.Round(GrenadeProjectileEntity.DirectHitDamage * grenade.CriticalDamageMultiplier));
        if (target is SentryEntity sentry)
        {
            if (sentry.Health <= 0 || sentry.Team == grenade.Team)
            {
                return;
            }

            if (ApplySentryDamage(sentry, damage, owner))
            {
                DestroySentry(sentry, owner);
            }
        }
        else if (target is JumpPadEntity jumpPad)
        {
            if (jumpPad.IsDead || jumpPad.Team == grenade.Team)
            {
                return;
            }

            jumpPad.TakeDamage(damage);
        }
    }

    private void RemoveGrenadeAt(int grenadeIndex)
    {
        var grenade = _grenades[grenadeIndex];
        _entities.Remove(grenade.Id);
        MarkProjectileTerminated(grenade.Id);
        _grenades.RemoveAt(grenadeIndex);
    }

    private void RemoveFlameAt(int flameIndex)
    {
        var flame = _flames[flameIndex];
        _entities.Remove(flame.Id);
        MarkProjectileTerminated(flame.Id);
        _flames.RemoveAt(flameIndex);
    }

    private void RemoveFlareAt(int flareIndex)
    {
        var flare = _flares[flareIndex];
        _entities.Remove(flare.Id);
        MarkProjectileTerminated(flare.Id);
        _flares.RemoveAt(flareIndex);
    }

    private void RemoveMineAt(int mineIndex)
    {
        var mine = _mines[mineIndex];
        _entities.Remove(mine.Id);
        MarkProjectileTerminated(mine.Id);
        _mines.RemoveAt(mineIndex);
    }
}
