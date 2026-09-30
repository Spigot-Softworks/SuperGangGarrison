namespace OpenGarrison.Core;

public sealed partial class ProjectileSystem
{
    public void AdvanceFlames()
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
            if (flame.IsGrounded)
            {
                if (TryIgniteEnemiesTouchingGroundedFlame(flame))
                {
                    // Grounded pools keep burning until expiry.
                }

                if (flame.IsExpired)
                {
                    RemoveFlameAt(flameIndex);
                }

                continue;
            }

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
                    if (flame.SettlesOnGround)
                    {
                        IgniteStrongDrinkAfterburn(hitPlayer, flame.OwnerId);
                        flame.RegisterHitPlayer(hitPlayer.Id);
                        flame.MoveTo(hitResult.HitX + directionX, hitResult.HitY + directionY);
                    }
                    else
                    {
                        var shieldChargeBefore = hitPlayer.CivvieUmbrellaChargeTicks;
                        var infiltrateBlockedFlame = hitPlayer.IsLastToDieSpyInfiltrateProjectileImmune;
                        var playerDied = ApplyPlayerContinuousDamageWithContext(
                            hitPlayer,
                            flame.DirectHitDamageValue * flame.CriticalDamageMultiplier,
                            owner,
                            civvieUmbrellaThreatSourceX: flame.PreviousX,
                            civvieUmbrellaThreatSourceY: flame.PreviousY,
                            civvieUmbrellaCriticalBoost: PlayerEntity.IsCriticalDamageMultiplierBoosted(flame.CriticalDamageMultiplier),
                            civvieUmbrellaUseLiveAttackerCriticalBoost: false,
                            additionalTraits: PlayerDamageTraits.DirectProjectile);
                        var umbrellaBlockedFlame = hitPlayer.CivvieUmbrellaChargeTicks < shieldChargeBefore;
                        if (playerDied)
                        {
                            KillPlayer(hitPlayer, killer: owner, weaponSpriteName: "FlameKL");
                        }
                        else if (!umbrellaBlockedFlame && !infiltrateBlockedFlame)
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
                else if (flame.SettlesOnGround)
                {
                    flame.SettleOnGround(hitResult.HitX, hitResult.HitY);
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

    private bool TryIgniteEnemiesTouchingGroundedFlame(FlameProjectileEntity flame)
    {
        var ignitedAny = false;
        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive
                || player.Team == flame.Team
                || !CanTeamDamagePlayer(flame.Team, flame.OwnerId, player))
            {
                continue;
            }

            if (!CircleIntersectsPlayer(this, flame.X, flame.Y, GrenadeProjectileEntity.StrongDrinkCollisionRadius, player))
            {
                continue;
            }

            IgniteStrongDrinkAfterburn(player, flame.OwnerId);
            ignitedAny = true;
        }

        return ignitedAny;
    }

    private static void IgniteStrongDrinkAfterburn(PlayerEntity player, int ownerPlayerId)
    {
        player.IgniteAfterburn(
            ownerPlayerId,
            PlayerEntity.BurnDefaultMaxDurationSourceTicks,
            PlayerEntity.BurnMaxIntensity,
            afterburnFalloff: false,
            burnFalloffAmount: 0f,
            killFeedWeaponSpriteName: GrenadeProjectileEntity.StrongDrinkFireKillFeedSpriteName);
    }

    public void AdvanceFlares()
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
                    ResolveDragonRageProjectileOutcome(flare, hitTarget: false);
                    RemoveFlareAt(flareIndex);
                }

                continue;
            }

            var directionX = movementX / movementDistance;
            var directionY = movementY / movementDistance;
            var endX = flare.X;
            var endY = flare.Y;
            // Compute world contacts once: piercing must not repeatedly damage room objects.
            var blockingHit = GetNearestFlareHit(flare, directionX, directionY, movementDistance, includePlayers: false);
            var bubbleHit = GetNearestEnemyBubbleHit(flare.PreviousX, flare.PreviousY, directionX, directionY, movementDistance, flare.Team);
            while (true)
            {
                var hit = GetNearestFlarePlayerHit(flare, directionX, directionY, movementDistance, blockingHit);
                var bubbleDistance = bubbleHit?.Distance ?? float.MaxValue;
                var hitDistance = hit?.Distance ?? float.MaxValue;
                if (bubbleHit is not null && bubbleDistance <= hitDistance)
                {
                    flare.MoveTo(bubbleHit.Value.HitX, bubbleHit.Value.HitY);
                    RegisterCombatTrace(flare.PreviousX, flare.PreviousY, directionX, directionY, bubbleHit.Value.Distance, false);
                    RemoveBubbleAt(bubbleHit.Value.BubbleIndex);
                    ResolveDragonRageProjectileOutcome(flare, hitTarget: false);
                    flare.Destroy();
                }
                else if (hit.HasValue)
                {
                    var hitResult = hit.Value;
                    var owner = FindPlayerById(flare.OwnerId);
                    var hitTarget = hitResult.HitSentry is not null
                        || hitResult.HitGenerator is not null
                        || hitResult.HitJumpPad is not null;
                    flare.MoveTo(hitResult.HitX, hitResult.HitY);
                    RegisterCombatTrace(flare.PreviousX, flare.PreviousY, directionX, directionY, hitResult.Distance, hitResult.HitPlayer is not null);
                    RegisterWorldSoundEvent("FlareImpactSnd", hitResult.HitX, hitResult.HitY, flare.OwnerId);
                    if (hitResult.HitPlayer is not null)
                    {
                        var infiltrateBlockedFlare =
                            hitResult.HitPlayer.IsLastToDieSpyInfiltrateProjectileImmune;
                        if (infiltrateBlockedFlare
                            || !TryAbsorbCivvieUmbrellaProjectileContact(
                                hitResult.HitPlayer,
                                flare.OwnerId,
                                hitResult.HitX,
                                hitResult.HitY,
                                criticalBoost: PlayerEntity.IsCriticalDamageMultiplierBoosted(flare.CriticalDamageMultiplier)))
                        {
                            if (!infiltrateBlockedFlare)
                            {
                                hitTarget = true;
                                RegisterBloodEffect(hitResult.HitPlayer.X, hitResult.HitPlayer.Y, DeterministicMath.Atan2(directionY, directionX) * (180f / MathF.PI) - 180f);
                            }

                            var hitDamage = ApplyExperimentalAirshotDamageMultiplier(owner, hitResult.HitPlayer, (int)MathF.Round(flare.DamagePerHit * flare.CriticalDamageMultiplier), out var damageFlags);
                            var playerDied = ApplyPlayerDamageWithContext(
                                hitResult.HitPlayer,
                                hitDamage,
                                owner,
                                PlayerEntity.SpyDamageRevealAlpha,
                                damageFlags,
                                allowCivvieUmbrellaShield: false,
                                civvieUmbrellaCriticalBoost: PlayerEntity.IsCriticalDamageMultiplierBoosted(flare.CriticalDamageMultiplier),
                                civvieUmbrellaUseLiveAttackerCriticalBoost: false,
                                additionalTraits: PlayerDamageTraits.DirectProjectile);
                            if (playerDied)
                            {
                                KillPlayer(hitResult.HitPlayer, killer: owner, weaponSpriteName: flare.KillFeedWeaponSpriteName);
                            }
                            else if (!infiltrateBlockedFlare)
                            {
                                hitResult.HitPlayer.IgniteAfterburn(
                                    flare.OwnerId,
                                    FlareProjectileEntity.BurnDurationIncreaseSourceTicks,
                                    FlareProjectileEntity.BurnIntensityIncrease,
                                    FlareProjectileEntity.AfterburnFalloff,
                                    burnFalloffAmount: 0f,
                                    killFeedWeaponSpriteName: flare.KillFeedWeaponSpriteName);
                            }
                        }
                    }
                    else if (hitResult.HitSentry is not null && ApplySentryDamage(hitResult.HitSentry, (int)MathF.Round(flare.DamagePerHit * flare.CriticalDamageMultiplier), owner))
                    {
                        DestroySentry(hitResult.HitSentry, owner);
                    }
                    else if (hitResult.HitGenerator is not null)
                    {
                        TryDamageGenerator(hitResult.HitGenerator.Team, flare.DamagePerHit * flare.CriticalDamageMultiplier, owner);
                    }
                    else if (hitResult.HitJumpPad is not null)
                    {
                        hitResult.HitJumpPad.TakeDamage((int)MathF.Round(flare.DamagePerHit * flare.CriticalDamageMultiplier));
                    }

                    if (flare.IsDragonRageSlug && hitResult.HitPlayer is { } piercedPlayer && hitTarget)
                    {
                        flare.RecordPlayerHit(piercedPlayer.Id);
                        ResolveDragonRageProjectileOutcome(flare, hitTarget: true);
                        flare.MoveTo(endX, endY);
                        continue;
                    }
                    ResolveDragonRageProjectileOutcome(flare, hitTarget);
                    flare.Destroy();
                }
                else
                {
                    RegisterCombatTrace(flare.PreviousX, flare.PreviousY, directionX, directionY, movementDistance, false);
                }
                break;
            }

            if (flare.IsExpired)
            {
                ResolveDragonRageProjectileOutcome(flare, hitTarget: false);
                RemoveFlareAt(flareIndex);
            }
        }
    }

    internal void ResolveDragonRageProjectileOutcome(FlareProjectileEntity flare, bool hitTarget)
    {
        if (!flare.IsDragonRageSlug
            || flare.DragonRageShotSequence <= 0
            || FindPlayerById(flare.OwnerId) is not { } owner)
        {
            return;
        }

        owner.ResolveDragonRageShot(
            flare.DragonRageShotSequence,
            hitTarget,
            flare.InitialLifetimeTicks - Math.Max(0, flare.TicksRemaining));
    }

    public void AdvanceMines()
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
            if (TryInterceptWithCivilDefenseTurret(mine.Team, mine.PreviousX, mine.PreviousY,
                    directionX, directionY, MathF.Min(movementDistance, hit?.Distance ?? movementDistance)))
            {
                RemoveMineAt(mineIndex);
                continue;
            }
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

    public void AdvanceGrenades()
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
                if (grenade.IsStrongDrink)
                {
                    // Strong Drink despawns quietly when the fuse runs out.
                    grenade.Destroy();
                    RemoveGrenadeAt(grenadeIndex);
                }
                else
                {
                    ExplodeGrenade(grenade);
                    RemoveGrenadeAt(grenadeIndex);
                }

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
                if (grenade.IsStrongDrink)
                {
                    ApplyStrongDrinkDirectHit(grenade, directHitPlayer);
                    RemoveGrenadeAt(grenadeIndex);
                }
                else
                {
                    ExplodeGrenade(grenade, directHitPlayer: directHitPlayer);
                    RemoveGrenadeAt(grenadeIndex);
                }

                continue;
            }

            if (CheckGrenadeBuildingCollision(grenade, out var directHitBuilding))
            {
                if (grenade.IsStrongDrink)
                {
                    // Strong Drink shatters harmlessly on buildings / env props.
                    RegisterStrongDrinkShatterEffect(grenade.X, grenade.Y, grenade.Team);
                    grenade.Destroy();
                    RemoveGrenadeAt(grenadeIndex);
                }
                else
                {
                    ExplodeGrenade(grenade, directHitBuilding: directHitBuilding);
                    RemoveGrenadeAt(grenadeIndex);
                }

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
                if (grenade.IsStrongDrink)
                {
                    RegisterStrongDrinkShatterEffect(grenade.X, grenade.Y, grenade.Team);
                    grenade.Destroy();
                    RemoveGrenadeAt(grenadeIndex);
                }
                else
                {
                    ExplodeGrenade(grenade, directHitDamageableZoneIndex: damageableZoneIndex);
                    RemoveGrenadeAt(grenadeIndex);
                }

                continue;
            }

            // Swept environment collision — bounce off walls (Strong Drink shatters instead)
            if (movementDistance > 0.0001f)
            {
                var envHit = GetNearestGrenadeEnvironmentHit(grenade, directionX, directionY, movementDistance);
                if (envHit.HasValue)
                {
                    if (grenade.IsStrongDrink)
                    {
                        grenade.MoveTo(envHit.Value.HitX, envHit.Value.HitY);
                        var burstDegrees = DeterministicMath.Atan2(envHit.Value.NormalY, envHit.Value.NormalX) * (180f / MathF.PI);
                        RegisterStrongDrinkShatterEffect(grenade.X, grenade.Y, grenade.Team, burstDegrees);
                        grenade.Destroy();
                        RemoveGrenadeAt(grenadeIndex);
                        continue;
                    }

                    // Place the grenade at the hit surface, backed off slightly so it doesn't embed
                    var backoffX = -directionX * GrenadeProjectileEntity.EnvironmentCollisionBackoffDistance;
                    var backoffY = -directionY * GrenadeProjectileEntity.EnvironmentCollisionBackoffDistance;
                    grenade.MoveTo(envHit.Value.HitX + backoffX, envHit.Value.HitY + backoffY);
                    grenade.Bounce(envHit.Value.NormalX, envHit.Value.NormalY);
                    // Visual-only random spin after bounce; magnitude scales with impact speed so slow-rolling grenades don't spin
                    const float rotationImpulseReferenceSpeed = 12f;
                    var speedFactor = float.Min(1f, movementDistance / rotationImpulseReferenceSpeed);
                    var impulse = (_random.NextSingle() - 0.5f) * 0.9f * speedFactor;
                    grenade.ApplyRotationImpulse(impulse);
                }
            }
        }
    }

    private void ApplyStrongDrinkDirectHit(GrenadeProjectileEntity grenade, PlayerEntity target)
    {
        grenade.MoveTo(target.X, target.Y);
        RegisterStrongDrinkShatterEffect(grenade.X, grenade.Y, grenade.Team);
        RegisterWorldSoundEvent(
            GrenadeProjectileEntity.StrongDrinkCrashSoundName,
            grenade.X,
            grenade.Y,
            grenade.OwnerId);
        var owner = FindPlayerById(grenade.OwnerId);
        if (!target.IsAlive || !CanTeamDamagePlayer(grenade.Team, grenade.OwnerId, target))
        {
            grenade.Destroy();
            return;
        }

        RegisterBloodEffect(target.X, target.Y, PointDirectionDegrees(grenade.PreviousX, grenade.PreviousY, target.X, target.Y) - 180f);
        var damage = Math.Max(1, (int)MathF.Round(GrenadeProjectileEntity.StrongDrinkDirectHitDamage * grenade.CriticalDamageMultiplier));
        if (ApplyPlayerDamageWithContext(
                target,
                damage,
                owner,
                PlayerEntity.SpyDamageRevealAlpha,
                civvieUmbrellaThreatSourceX: grenade.PreviousX,
                civvieUmbrellaThreatSourceY: grenade.PreviousY,
                civvieUmbrellaCriticalBoost: PlayerEntity.IsCriticalDamageMultiplierBoosted(grenade.CriticalDamageMultiplier),
                civvieUmbrellaUseLiveAttackerCriticalBoost: false,
                additionalTraits: PlayerDamageTraits.DirectProjectile))
        {
            KillPlayer(
                target,
                killer: owner,
                weaponSpriteName: grenade.KillFeedWeaponSpriteNameOverride
                    ?? GrenadeProjectileEntity.StrongDrinkKillFeedSpriteName);
        }

        grenade.Destroy();
    }

    internal bool TryShootFriendlyStrongDrink(
        PlayerTeam shooterTeam,
        PlayerClass shooterClass,
        int shooterOwnerId,
        float originX,
        float originY,
        float directionX,
        float directionY,
        float maxDistance,
        int fireParticleCount)
    {
        if (shooterClass != PlayerClass.Sniper)
        {
            return false;
        }

        var hit = GetNearestFriendlyStrongDrinkHit(shooterTeam, originX, originY, directionX, directionY, maxDistance);
        if (hit is null)
        {
            return false;
        }

        ExplodeStrongDrinkFromShot(hit.Value.GrenadeIndex, fireParticleCount);
        return true;
    }

    private readonly record struct StrongDrinkHitResult(int GrenadeIndex, float Distance, float HitX, float HitY);

    private StrongDrinkHitResult? GetNearestFriendlyStrongDrinkHit(
        PlayerTeam team,
        float originX,
        float originY,
        float directionX,
        float directionY,
        float maxDistance)
    {
        StrongDrinkHitResult? nearest = null;
        for (var grenadeIndex = 0; grenadeIndex < _grenades.Count; grenadeIndex += 1)
        {
            var grenade = _grenades[grenadeIndex];
            if (!grenade.IsStrongDrink || grenade.Team != team || grenade.IsDestroyed)
            {
                continue;
            }

            var half = GrenadeProjectileEntity.StrongDrinkHitboxHalfExtent;
            var distance = GetRayIntersectionDistanceWithAxisAlignedSquare(
                originX,
                originY,
                directionX,
                directionY,
                grenade.X,
                grenade.Y,
                half,
                maxDistance);
            if (!distance.HasValue)
            {
                continue;
            }

            if (nearest is not null && nearest.Value.Distance <= distance.Value)
            {
                continue;
            }

            nearest = new StrongDrinkHitResult(
                grenadeIndex,
                distance.Value,
                originX + directionX * distance.Value,
                originY + directionY * distance.Value);
        }

        return nearest;
    }

    private static float? GetRayIntersectionDistanceWithAxisAlignedSquare(
        float originX,
        float originY,
        float directionX,
        float directionY,
        float centerX,
        float centerY,
        float halfExtent,
        float maxDistance)
    {
        var left = centerX - halfExtent;
        var top = centerY - halfExtent;
        var right = centerX + halfExtent;
        var bottom = centerY + halfExtent;
        const float epsilon = 0.0001f;
        var tMin = float.NegativeInfinity;
        var tMax = float.PositiveInfinity;

        if (MathF.Abs(directionX) < epsilon)
        {
            if (originX < left || originX > right)
            {
                return null;
            }
        }
        else
        {
            var invX = 1f / directionX;
            var t1 = (left - originX) * invX;
            var t2 = (right - originX) * invX;
            if (t1 > t2)
            {
                (t1, t2) = (t2, t1);
            }

            tMin = MathF.Max(tMin, t1);
            tMax = MathF.Min(tMax, t2);
            if (tMin > tMax)
            {
                return null;
            }
        }

        if (MathF.Abs(directionY) < epsilon)
        {
            if (originY < top || originY > bottom)
            {
                return null;
            }
        }
        else
        {
            var invY = 1f / directionY;
            var t1 = (top - originY) * invY;
            var t2 = (bottom - originY) * invY;
            if (t1 > t2)
            {
                (t1, t2) = (t2, t1);
            }

            tMin = MathF.Max(tMin, t1);
            tMax = MathF.Min(tMax, t2);
            if (tMin > tMax)
            {
                return null;
            }
        }

        var hitDistance = tMin >= 0f ? tMin : tMax;
        if (hitDistance < 0f || hitDistance > maxDistance)
        {
            return null;
        }

        return hitDistance;
    }

    private void ExplodeStrongDrinkFromShot(int grenadeIndex, int fireParticleCount)
    {
        if (grenadeIndex < 0 || grenadeIndex >= _grenades.Count)
        {
            return;
        }

        var grenade = _grenades[grenadeIndex];
        if (!grenade.IsStrongDrink)
        {
            return;
        }

        var owner = FindPlayerById(grenade.OwnerId);
        var particleCount = fireParticleCount > 0
            ? fireParticleCount
            : GrenadeProjectileEntity.StrongDrinkDefaultFireParticleCount;

        // Rocket-style splash (same 30 / 65 as rockets) via existing grenade explode helpers.
        grenade.HydrateStrongDrink(true);
        RegisterStrongDrinkShatterEffect(grenade.X, grenade.Y, grenade.Team);
        RegisterWorldSoundEvent(
            GrenadeProjectileEntity.StrongDrinkCrashSoundName,
            grenade.X,
            grenade.Y,
            grenade.OwnerId);
        ExplodeGrenade(grenade);
        SpawnStrongDrinkFireRain(owner ?? FindPlayerById(grenade.OwnerId), grenade.X, grenade.Y, particleCount);
        RemoveGrenadeAt(grenadeIndex);
    }

    private void SpawnStrongDrinkFireRain(PlayerEntity? owner, float centerX, float centerY, int particleCount)
    {
        if (owner is null || particleCount <= 0)
        {
            return;
        }

        // Long air failsafe only — grounded lifetime is applied when SettleOnGround runs.
        var airTicks = GetSimulationTicksFromSourceTicks(GrenadeProjectileEntity.StrongDrinkAirFailsafeLifetimeTicks);
        var groundedTicks = GetSimulationTicksFromSourceTicks(GrenadeProjectileEntity.StrongDrinkGroundedFlameLifetimeTicks);
        for (var index = 0; index < particleCount; index += 1)
        {
            // Erupt around the bottle blast point, then fall and settle on the ground.
            var angle = _random.NextSingle() * MathF.Tau;
            var radiusFactor = MathF.Sqrt(_random.NextSingle());
            var radius = radiusFactor * (GrenadeProjectileEntity.StrongDrinkFireHorizontalSpread * 0.5f);
            var spawnX = centerX + DeterministicMath.Cos(angle) * radius;
            var spawnY = centerY
                + ((_random.NextSingle() - 0.5f) * GrenadeProjectileEntity.StrongDrinkFireSpawnJitterY);
            var burstSpeed = GrenadeProjectileEntity.StrongDrinkFireBurstSpeedMin
                + (_random.NextSingle()
                    * (GrenadeProjectileEntity.StrongDrinkFireBurstSpeedMax - GrenadeProjectileEntity.StrongDrinkFireBurstSpeedMin));
            var velocityX = DeterministicMath.Cos(angle) * burstSpeed * (0.45f + (radiusFactor * 0.55f))
                + (_random.NextSingle() - 0.5f) * GrenadeProjectileEntity.StrongDrinkFireDriftSpeed;
            var velocityY = GrenadeProjectileEntity.StrongDrinkFireUpwardBurstMin
                + (_random.NextSingle()
                    * (GrenadeProjectileEntity.StrongDrinkFireUpwardBurstMax - GrenadeProjectileEntity.StrongDrinkFireUpwardBurstMin));
            SpawnStrongDrinkSettlingFlame(
                owner,
                spawnX,
                spawnY,
                velocityX,
                velocityY,
                airTicks,
                groundedTicks);
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
            if (jumpPad.IsNeutral || jumpPad.IsDead || jumpPad.Team == grenade.Team)
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

    internal void ExplodeGrenade(
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
        var blastRadius = ResolveExplosiveSplashRadius(
            GrenadeProjectileEntity.BlastRadius
                * MathF.Max(0.1f, owner?.LastToDieUniversalModifiers.ExplosionScale ?? 1f));

        RegisterWorldSoundEvent("ExplosionSnd", grenade.X, grenade.Y);
        RegisterVisualEffect("Explosion", grenade.X, grenade.Y);
        ApplyDeadBodyExplosionImpulse(grenade.X, grenade.Y, blastRadius * 0.75f, 10f, blastRadius);
        ApplyPlayerGibExplosionImpulse(grenade.X, grenade.Y, blastRadius * 0.75f, 15f, blastRadius);
        RegisterExplosionTraces(grenade.X, grenade.Y);

        // Damage players
        var playersSnapshot = EnumerateSimulatedPlayers().ToArray();
        var attackerWasGrounded = owner?.IsGrounded;
        var groundedByPlayerId = playersSnapshot.ToDictionary(
            static player => player.Id,
            static player => player.IsGrounded);
        foreach (var player in playersSnapshot)
        {
            if (!player.IsAlive)
            {
                continue;
            }

            var distance = GetExplosionDistanceToPlayer(this, player, grenade.X, grenade.Y);
            if (distance >= blastRadius)
            {
                continue;
            }

            if (ShouldIgnoreFriendlyGroundedBlast(player, grenade.Team, grenade.OwnerId))
            {
                continue;
            }

            var factor = 1f - (distance / blastRadius);
            if (factor <= 0f)
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

                var damage = ResolveExplosiveSplashDamage(maxSplashDamage, factor);
                if (ApplyPlayerContinuousDamageWithContext(
                        player,
                        damage,
                        owner,
                        PlayerEntity.SpyMineRevealAlpha,
                        civvieUmbrellaThreatSourceX: grenade.X,
                        civvieUmbrellaThreatSourceY: grenade.Y,
                        civvieUmbrellaDrainTicks: PlayerEntity.GetCivvieUmbrellaSplashExplosionDrainTicksFromDamage(damage, maxSplashDamage),
                        civvieUmbrellaCriticalBoost: PlayerEntity.IsCriticalDamageMultiplierBoosted(critMultiplier),
                        civvieUmbrellaUseLiveAttackerCriticalBoost: false,
                        attackerWasGrounded: attackerWasGrounded,
                        targetWasGrounded: groundedByPlayerId[player.Id]))
                {
                    KillPlayer(
                        player,
                        gibbed: true,
                        killer: owner,
                        weaponSpriteName: ResolveGrenadeKillFeedSpriteName(grenade));
                }
            }
        }

        if (directHitPlayer is not null)
        {
            ApplyGrenadeDirectImpactDamage(
                grenade,
                owner,
                directHitPlayer,
                attackerWasGrounded,
                groundedByPlayerId.GetValueOrDefault(directHitPlayer.Id, directHitPlayer.IsGrounded));
        }

        // Damage sentries
        for (var sentryIndex = _sentries.Count - 1; sentryIndex >= 0; sentryIndex -= 1)
        {
            var sentry = _sentries[sentryIndex];
            var distance = DistanceBetween(grenade.X, grenade.Y, sentry.X, sentry.Y);
            if (distance >= blastRadius || sentry.Team == grenade.Team)
            {
                continue;
            }

            var factor = 1f - (distance / blastRadius);
            if (factor <= 0f)
            {
                continue;
            }

            if (ReferenceEquals(sentry, directHitBuilding))
            {
                continue;
            }

            var damage = ResolveExplosiveSplashDamage(
                grenade.ExplosionDamage * GrenadeProjectileEntity.SentryDamageMultiplier * grenade.CriticalDamageMultiplier,
                factor);
            if (ApplySentryDamage(sentry, (int)MathF.Ceiling(damage), owner))
            {
                DestroySentry(sentry, owner);
            }
        }

        // Damage jump pads with the same structure multiplier used by sticky
        // and rocket splash. Neutral map pads are valid targets for either
        // team; team-owned pads retain friendly-fire protection.
        for (var jumpPadIndex = _jumpPads.Count - 1; jumpPadIndex >= 0; jumpPadIndex -= 1)
        {
            var jumpPad = _jumpPads[jumpPadIndex];
            var distance = DistanceBetween(grenade.X, grenade.Y, jumpPad.X, jumpPad.Y);
            if (distance >= blastRadius || (!jumpPad.IsNeutral && jumpPad.Team == grenade.Team) || jumpPad.IsDead)
            {
                continue;
            }

            var factor = 1f - (distance / blastRadius);
            if (factor <= 0f)
            {
                continue;
            }

            if (ReferenceEquals(jumpPad, directHitBuilding))
            {
                continue;
            }

            var damage = ResolveExplosiveSplashDamage(
                grenade.ExplosionDamage * ExplosiveJumpPadDamageMultiplier * grenade.CriticalDamageMultiplier,
                factor);
            jumpPad.TakeDamage((int)MathF.Ceiling(damage));
            if (jumpPad.IsDead)
            {
                DestroyJumpPad(jumpPad);
            }
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
            blastRadius,
            grenade.ExplosionDamage * grenade.CriticalDamageMultiplier,
            0f,
            directHitDamageableZoneIndex,
            grenade.Team,
            ExplosiveSplashMinimumDamage);
    }

    private void ApplyGrenadeDirectImpactDamage(
        GrenadeProjectileEntity grenade,
        PlayerEntity? owner,
        PlayerEntity target,
        bool? attackerWasGrounded,
        bool targetWasGrounded)
    {
        if (!target.IsAlive || !CanTeamDamagePlayer(grenade.Team, grenade.OwnerId, target))
        {
            return;
        }

        RegisterBloodEffect(target.X, target.Y, PointDirectionDegrees(grenade.X, grenade.Y, target.X, target.Y) - 180f, 3);
        var damage = Math.Max(1, (int)MathF.Round(GrenadeProjectileEntity.DirectHitDamage * grenade.CriticalDamageMultiplier));
        if (ApplyPlayerDamageWithContext(
                target,
                damage,
                owner,
                PlayerEntity.SpyMineRevealAlpha,
                civvieUmbrellaThreatSourceX: grenade.PreviousX,
                civvieUmbrellaThreatSourceY: grenade.PreviousY,
                civvieUmbrellaDrainTicks: PlayerEntity.CivvieUmbrellaDirectExplosionDrainTicks,
                civvieUmbrellaCriticalBoost: PlayerEntity.IsCriticalDamageMultiplierBoosted(grenade.CriticalDamageMultiplier),
                civvieUmbrellaUseLiveAttackerCriticalBoost: false,
                additionalTraits: PlayerDamageTraits.DirectProjectile,
                attackerWasGrounded: attackerWasGrounded,
                targetWasGrounded: targetWasGrounded))
        {
            KillPlayer(
                target,
                gibbed: true,
                killer: owner,
                weaponSpriteName: ResolveGrenadeKillFeedSpriteName(grenade));
        }
    }

    private static string ResolveGrenadeKillFeedSpriteName(GrenadeProjectileEntity grenade)
    {
        if (grenade.IsStrongDrink)
        {
            // Shot/splash/fire-rain explosion kills use the fire bottle icon; direct throws keep SniperBottleKL.
            return GrenadeProjectileEntity.StrongDrinkFireKillFeedSpriteName;
        }

        return grenade.KillFeedWeaponSpriteNameOverride ?? "GrenadeLauncherKL";
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
            if (jumpPad.IsDead || (!jumpPad.IsNeutral && jumpPad.Team == grenade.Team))
            {
                return;
            }

            jumpPad.TakeDamage(damage);
        }
    }

    internal void RemoveGrenadeAt(int grenadeIndex)
    {
        var grenade = _grenades[grenadeIndex];
        EntityStore.Remove(grenade.Id);
        MarkProjectileTerminated(grenade.Id);
        _grenades.RemoveAt(grenadeIndex);
    }

    internal void RemoveFlameAt(int flameIndex)
    {
        var flame = _flames[flameIndex];
        EntityStore.Remove(flame.Id);
        MarkProjectileTerminated(flame.Id);
        _flames.RemoveAt(flameIndex);
    }

    internal void RemoveFlareAt(int flareIndex)
    {
        var flare = _flares[flareIndex];
        EntityStore.Remove(flare.Id);
        MarkProjectileTerminated(flare.Id);
        _flares.RemoveAt(flareIndex);
    }

    internal void RemoveMineAt(int mineIndex)
    {
        var mine = _mines[mineIndex];
        EntityStore.Remove(mine.Id);
        MarkProjectileTerminated(mine.Id);
        _mines.RemoveAt(mineIndex);
    }
}
