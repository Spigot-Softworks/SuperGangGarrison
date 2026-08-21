using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void SpawnShot(
        PlayerEntity owner,
        float x,
        float y,
        float velocityX,
        float velocityY,
        float damagePerHit = ShotProjectileEntity.DamagePerHit,
        bool forceGibOnKill = false,
        string? killFeedWeaponSpriteNameOverride = null,
        int? sourceSentryId = null,
        bool applyExperimentalEngineerSentryPerkEffects = false,
        float playerKnockbackScale = 1f,
        float? playerSlowMovementMultiplier = null,
        int playerSlowRefreshTicks = 0)
    {
        var shotTeam = owner.Team;
        if (sourceSentryId is int sentryId)
        {
            for (var sentryIndex = 0; sentryIndex < _sentries.Count; sentryIndex += 1)
            {
                if (_sentries[sentryIndex].Id != sentryId)
                {
                    continue;
                }

                shotTeam = _sentries[sentryIndex].Team;
                break;
            }
        }

        var shot = new ShotProjectileEntity(
            AllocateEntityId(),
            shotTeam,
            owner.Id,
            x,
            y,
            velocityX,
            velocityY,
            damagePerHit,
            forceGibOnKill,
            killFeedWeaponSpriteNameOverride,
            sourceSentryId,
            applyExperimentalEngineerSentryPerkEffects,
            playerKnockbackScale,
            playerSlowMovementMultiplier,
            playerSlowRefreshTicks);
        if (owner.IsKritzCritBoosted)
        {
            shot.SetCritical(owner.ActiveKritzCritDamageMultiplier);
        }

        _shots.Add(shot);
        _entities.Add(shot.Id, shot);
    }

    private void SpawnBubble(PlayerEntity owner, float x, float y, float velocityX, float velocityY)
    {
        var bubble = new BubbleProjectileEntity(
            AllocateEntityId(),
            owner.Team,
            owner.Id,
            x,
            y,
            velocityX,
            velocityY,
            GetSimulationTicksFromSourceTicks(BubbleProjectileEntity.LifetimeTicks));
        if (owner.IsKritzCritBoosted)
        {
            bubble.SetCritical(owner.ActiveKritzCritDamageMultiplier);
        }

        owner.IncrementQuoteBubbleCount();
        _bubbles.Add(bubble);
        _entities.Add(bubble.Id, bubble);
    }

    private void SpawnBlade(PlayerEntity owner, float x, float y, float velocityX, float velocityY, int hitDamage, int lifetimeTicks = PlayerEntity.QuoteBladeLifetimeTicks)
    {
        var blade = new BladeProjectileEntity(
            AllocateEntityId(),
            owner.Team,
            owner.Id,
            x,
            y,
            velocityX,
            velocityY,
            hitDamage,
            lifetimeTicks);
        if (owner.IsKritzCritBoosted)
        {
            blade.SetCritical(owner.ActiveKritzCritDamageMultiplier);
        }

        owner.IncrementQuoteBladeCount();
        _blades.Add(blade);
        _entities.Add(blade.Id, blade);
    }

    private void SpawnNail(PlayerEntity owner, float x, float y, float velocityX, float velocityY)
    {
        var nail = new NailProjectileEntity(
            AllocateEntityId(),
            owner.Team,
            owner.Id,
            x,
            y,
            velocityX,
            velocityY);
        if (owner.IsKritzCritBoosted)
        {
            nail.SetCritical(owner.ActiveKritzCritDamageMultiplier);
        }

        _needles.Add(nail);
        _entities.Add(nail.Id, nail);
    }

    private void SpawnArrow(PlayerEntity owner, float x, float y, float velocityX, float velocityY, int damage, float fakeSpeedMultiplier)
    {
        var sniperProfile = owner.LastToDieSniperProfile;
        var ghostDamageMultiplier = owner.CaptureLastToDieSniperGhostShot(Config.TicksPerSecond);
        var isFullyCharged = damage >= PlayerEntity.SniperBowMaxDamage;
        var chargeFraction = (fakeSpeedMultiplier - PlayerEntity.SniperBowMinFakeSpeedMultiplier)
            / (PlayerEntity.SniperBowMaxFakeSpeedMultiplier - PlayerEntity.SniperBowMinFakeSpeedMultiplier);
        var capturedDamage = sniperProfile.TranqDartsEnabled
            ? Math.Max(
                1,
                (int)MathF.Round(
                    damage * LastToDieSniperProfile.TranqDartsDirectDamageMultiplier))
            : damage;
        var payload = new LastToDieSniperArrowPayload(
            sniperProfile.GuardianEnabled,
            sniperProfile.MechanicaEnabled && isFullyCharged,
            sniperProfile.TranqDartsEnabled,
            sniperProfile.PoisonTipEnabled
                ? LastToDieSniperProfile.GetPoisonTipDamagePerSecond(chargeFraction)
                : 0f,
            ghostDamageMultiplier,
            sniperProfile.DecapitatorEnabled,
            isFullyCharged,
            sniperProfile.ExplosiveTipEnabled,
            owner.IsKritzCritBoosted,
            owner.ActiveKritzCritDamageMultiplier);
        SpawnArrowWithPayload(
            owner,
            x,
            y,
            velocityX,
            velocityY,
            capturedDamage,
            fakeSpeedMultiplier,
            payload);
        if (sniperProfile.MenageATroisEnabled && isFullyCharged)
        {
            owner.BeginLastToDieSniperVolley(
                velocityX,
                velocityY,
                capturedDamage,
                fakeSpeedMultiplier,
                payload);
        }
    }

    private void SpawnArrowWithPayload(
        PlayerEntity owner,
        float x,
        float y,
        float velocityX,
        float velocityY,
        int damage,
        float fakeSpeedMultiplier,
        in LastToDieSniperArrowPayload payload)
    {
        var arrow = new ArrowProjectileEntity(
            AllocateEntityId(),
            owner.Team,
            owner.Id,
            x,
            y,
            velocityX,
            velocityY,
            damage,
            fakeSpeedMultiplier,
            appliesLastToDieGuardian: payload.AppliesGuardian,
            piercesPlayers: payload.PiercesPlayers,
            appliesLastToDieTranqDarts: payload.AppliesTranqDarts,
            lastToDiePoisonDamagePerSecond: payload.PoisonDamagePerSecond,
            lastToDieGhostDamageMultiplier: payload.GhostDamageMultiplier,
            appliesLastToDieDecapitator: payload.AppliesDecapitator,
            isLastToDieDecapitatorFullyCharged: payload.IsDecapitatorFullyCharged,
            appliesLastToDieExplosiveTip: payload.AppliesExplosiveTip);
        if (payload.IsCritical)
        {
            arrow.SetCritical(payload.CriticalDamageMultiplier);
        }

        _needles.Add(arrow);
        _entities.Add(arrow.Id, arrow);
    }

    private void SpawnQueuedLastToDieSniperArrow(
        PlayerEntity owner,
        float x,
        float y,
        in LastToDieSniperVolleyState volley)
        => SpawnArrowWithPayload(
            owner,
            x,
            y,
            volley.VelocityX,
            volley.VelocityY,
            volley.Damage,
            volley.FakeSpeedMultiplier,
            volley.Payload);

    private void SpawnNeedle(PlayerEntity owner, float x, float y, float velocityX, float velocityY)
    {
        var needle = new NeedleProjectileEntity(
            AllocateEntityId(),
            owner.Team,
            owner.Id,
            x,
            y,
            velocityX,
            velocityY);
        if (owner.IsKritzCritBoosted)
        {
            needle.SetCritical(owner.ActiveKritzCritDamageMultiplier);
        }

        _needles.Add(needle);
        _entities.Add(needle.Id, needle);
    }

    private void SpawnMedicHealNeedle(
        PlayerEntity owner,
        float x,
        float y,
        float velocityX,
        float velocityY,
        int healPerHit = MedicHealNeedleProjectileEntity.DefaultHealPerHit,
        int enemyDamagePerHit = MedicHealNeedleProjectileEntity.DefaultEnemyDamagePerHit)
    {
        var lastToDiePayload = CaptureLastToDieMedicKritzM2Payload(owner);
        var lastToDieJavelinFuseTicks = lastToDiePayload.AppliesJavelin
            ? Math.Max(
                1,
                (int)Math.Ceiling(
                    LastToDieDerivedModifiers.MedicJavelinFuseSeconds
                        * Math.Max(1, Config.TicksPerSecond)))
            : 0;
        var needle = new MedicHealNeedleProjectileEntity(
            AllocateEntityId(),
            owner.Team,
            owner.Id,
            x,
            y,
            velocityX,
            velocityY,
            healPerHit,
            enemyDamagePerHit,
            lastToDiePayload,
            lastToDieJavelinFuseTicks);
        if (owner.IsKritzCritBoosted)
        {
            needle.SetCritical(owner.ActiveKritzCritDamageMultiplier);
        }

        _needles.Add(needle);
        _entities.Add(needle.Id, needle);
    }

    private void SpawnRevolverShot(
        PlayerEntity owner,
        float x,
        float y,
        float velocityX,
        float velocityY,
        float damagePerHit = RevolverProjectileEntity.DamagePerHit,
        string? killFeedWeaponSpriteNameOverride = null,
        global::OpenGarrison.Core.LastToDie.LastToDieSpyRevolverProfile? lastToDieProfile = null,
        bool forceCritical = false,
        bool appliesLuckyStrikeStun = false)
    {
        var shot = new RevolverProjectileEntity(
            AllocateEntityId(),
            owner.Team,
            owner.Id,
            x,
            y,
            velocityX,
            velocityY,
            damagePerHit,
            killFeedWeaponSpriteNameOverride,
            lastToDieProfile,
            appliesLuckyStrikeStun);
        if (owner.IsKritzCritBoosted)
        {
            shot.SetCritical(owner.ActiveKritzCritDamageMultiplier);
        }
        else if (forceCritical)
        {
            shot.SetCritical();
        }

        _revolverShots.Add(shot);
        _entities.Add(shot.Id, shot);
    }

    private void SpawnStabAnimation(PlayerEntity owner, float directionDegrees)
    {
        var stabAnimation = new StabAnimEntity(
            AllocateEntityId(),
            owner.Id,
            owner.Team,
            owner.X,
            owner.Y,
            directionDegrees,
            owner.LastToDieInstastabEnabled
                ? LastToDieDerivedModifiers.SpyInstastabSpeedMultiplier
                : 1);
        _stabAnimations.Add(stabAnimation);
        _entities.Add(stabAnimation.Id, stabAnimation);
        RegisterVisualEffect(
            owner.Team == PlayerTeam.Blue ? "BackstabBlue" : "BackstabRed",
            owner.X,
            owner.Y,
            directionDegrees,
            owner.Id);
    }

    private void SpawnStabMask(PlayerEntity owner, float directionDegrees)
    {
        var stabMask = new StabMaskEntity(
            AllocateEntityId(),
            owner.Id,
            owner.Team,
            owner.X,
            owner.Y,
            directionDegrees);
        _stabMasks.Add(stabMask);
        _entities.Add(stabMask.Id, stabMask);
        RegisterWorldSoundEvent("KnifeSnd", stabMask.X, stabMask.Y);
    }

    private void SpawnFlame(
        PlayerEntity owner,
        float x,
        float y,
        float velocityX,
        float velocityY,
        float directHitDamage = FlameProjectileEntity.DirectHitDamage,
        float burnDamagePerTick = FlameProjectileEntity.BurnDamagePerTick)
    {
        var flame = new FlameProjectileEntity(
            AllocateEntityId(),
            owner.Team,
            owner.Id,
            x,
            y,
            velocityX,
            velocityY,
            GetSimulationTicksFromSourceTicks(FlameProjectileEntity.AirLifetimeTicks),
            isPerseverant: _random.Next(8) == 0,
            directHitDamage: directHitDamage,
            burnDamagePerTick: burnDamagePerTick);
        if (owner.IsKritzCritBoosted)
        {
            flame.SetCritical(owner.ActiveKritzCritDamageMultiplier);
        }

        _flames.Add(flame);
        _entities.Add(flame.Id, flame);
    }

    private void SpawnFlare(PlayerEntity owner, float x, float y, float velocityX, float velocityY)
    {
        var flare = new FlareProjectileEntity(
            AllocateEntityId(),
            owner.Team,
            owner.Id,
            x,
            y,
            velocityX,
            velocityY);
        if (owner.IsKritzCritBoosted)
        {
            flare.SetCritical(owner.ActiveKritzCritDamageMultiplier);
        }

        _flares.Add(flare);
        _entities.Add(flare.Id, flare);
    }

    private void SpawnRocket(
        PlayerEntity owner,
        float x,
        float y,
        float speed,
        float directionRadians,
        RocketCombatDefinition? rocketCombat = null,
        float directHitHealAmount = 0f,
        bool explodeImmediately = false,
        bool canGrantExperimentalInstantReloadOnHit = true,
        float knockbackScale = 1f,
        bool canIgniteTargets = false,
        bool enableExperimentalStingerTracking = false,
        bool enableExperimentalCaveatTracking = false,
        float experimentalVisualScale = 1f,
        int experimentalTrackingLockTicksRemaining = 0,
        string? killFeedWeaponSpriteNameOverride = null)
    {
        var rocket = new RocketProjectileEntity(
            AllocateEntityId(),
            owner.Team,
            owner.Id,
            x,
            y,
            speed,
            directionRadians,
            rocketCombat: rocketCombat,
            rangeAnchorOwnerId: owner.Id,
            lastKnownRangeOriginX: owner.X,
            lastKnownRangeOriginY: owner.Y,
            directHitHealAmount: directHitHealAmount,
            canGrantExperimentalInstantReloadOnHit: canGrantExperimentalInstantReloadOnHit,
            knockbackScale: knockbackScale,
            canIgniteTargets: canIgniteTargets,
            enableExperimentalStingerTracking: enableExperimentalStingerTracking,
            enableExperimentalCaveatTracking: enableExperimentalCaveatTracking,
            experimentalVisualScale: experimentalVisualScale,
            experimentalTrackingLockTicksRemaining: experimentalTrackingLockTicksRemaining,
            killFeedWeaponSpriteNameOverride: killFeedWeaponSpriteNameOverride);
        if (explodeImmediately)
        {
            rocket.DelayExplosionUntilNextTick(RocketProjectileEntity.DelayedExplosionReasonSpawnBlocked);
        }

        if (owner.IsKritzCritBoosted)
        {
            rocket.SetCritical(owner.ActiveKritzCritDamageMultiplier);
        }

        _rockets.Add(rocket);
        _entities.Add(rocket.Id, rocket);
        _pendingNewRocketIds.Add(rocket.Id);
        _pendingRocketSpawnEvents.Add(new WorldRocketSpawnEvent(
            rocket.Id,
            (byte)rocket.Team,
            rocket.OwnerId,
            rocket.X,
            rocket.Y,
            rocket.PreviousX,
            rocket.PreviousY,
            rocket.DirectionRadians,
            rocket.Speed,
            rocket.TicksRemaining,
            rocket.ReducedKnockbackSourceTicksRemaining,
            rocket.ZeroKnockbackSourceTicksRemaining,
            rocket.RangeAnchorOwnerId,
            rocket.LastKnownRangeOriginX,
            rocket.LastKnownRangeOriginY,
            rocket.DistanceToTravel,
            rocket.IsFading,
            rocket.FadeSourceTicksRemaining,
            rocket.ExplodeImmediately,
            rocket.IsCritical,
            CriticalDamageMultiplier: rocket.CriticalDamageMultiplier));
    }

    private void AdvancePendingRocketsForOwner(int ownerId)
    {
        RocketProjectileSystem.AdvancePendingForOwner(this, ownerId);
    }

    private void SpawnMine(PlayerEntity owner, float x, float y, float velocityX, float velocityY, string? killFeedWeaponSpriteNameOverride = null)
    {
        var mine = new MineProjectileEntity(
            AllocateEntityId(),
            owner.Team,
            owner.Id,
            x,
            y,
            velocityX,
            velocityY,
            killFeedWeaponSpriteNameOverride,
            createdFrame: Frame);
        if (owner.IsKritzCritBoosted)
        {
            mine.SetCritical(owner.ActiveKritzCritDamageMultiplier);
        }

        _mines.Add(mine);
        _entities.Add(mine.Id, mine);
    }

    private void SpawnGrenade(PlayerEntity owner, float x, float y, float velocityX, float velocityY, string? killFeedWeaponSpriteNameOverride = null)
    {
        var grenade = new GrenadeProjectileEntity(
            AllocateEntityId(),
            owner.Team,
            owner.Id,
            x,
            y,
            velocityX,
            velocityY,
            killFeedWeaponSpriteNameOverride);
        if (owner.IsKritzCritBoosted)
        {
            grenade.SetCritical(owner.ActiveKritzCritDamageMultiplier);
        }

        _grenades.Add(grenade);
        _entities.Add(grenade.Id, grenade);
    }

    private int GetSimulationTicksFromSourceTicks(float sourceTicks)
    {
        return Math.Max(1, (int)MathF.Ceiling(sourceTicks * Config.TicksPerSecond / LegacyMovementModel.SourceTicksPerSecond));
    }
}
