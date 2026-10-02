namespace OpenGarrison.Core;

internal sealed partial class WeaponFireHandler
{
    private void FireMinigun(PlayerEntity attacker, float aimWorldX, float aimWorldY)
    {
        FireMinigun(attacker, attacker.PrimaryWeapon, attacker.ClassId, aimWorldX, aimWorldY, killFeedWeaponSpriteNameOverride: null);
    }

    private void FireMinigun(
        PlayerEntity attacker,
        PrimaryWeaponDefinition weaponDefinition,
        PlayerClass weaponClassId,
        float aimWorldX,
        float aimWorldY,
        string? killFeedWeaponSpriteNameOverride)
    {
        var weaponOrigin = GetSourceWeaponOrigin(attacker, weaponClassId);

        // Use weapon pivot system to spawn bullets relative to weapon sprite rotation
        const float minigunPivotOffsetX = 0f;
        const float minigunPivotAdditionalYOffset = 14f;
        var pivotRay = GetWeaponPivotRay(
            weaponOrigin.BaseX,
            weaponOrigin.BaseY,
            aimWorldX,
            aimWorldY,
            attacker.FacingDirectionX,
            minigunPivotOffsetX,
            minigunPivotAdditionalYOffset);

        // Spawn bullets directly from the weapon pivot, moving forward along the firing direction
        var spreadRadians = GetWeaponSpreadRadians(attacker.Id, weaponDefinition.SpreadDegrees);
        var pelletAngle = pivotRay.AngleRadians + spreadRadians;
        var directionX = DeterministicMath.Cos(pelletAngle);
        var directionY = DeterministicMath.Sin(pelletAngle);
        var shotSpeed = GetWeaponShotSpeed(weaponDefinition);
        var (launchedVelocityX, launchedVelocityY) = _host.ApplyExperimentalProjectileSpeedMultiplier(
            attacker,
            directionX * shotSpeed,
            directionY * shotSpeed);
        var knockbackPayload = BulletKnockbackRules.ResolvePayload(weaponDefinition, actualProjectileCount: 1);

        // Spawn bullets 14 pixels forward from the weapon pivot along the firing direction
        const float minigunBarrelForwardOffset = 14f;
        var spawnX = pivotRay.PivotX + (directionX * minigunBarrelForwardOffset);
        var spawnY = pivotRay.PivotY + (directionY * minigunBarrelForwardOffset);

        SpawnShot(
            attacker,
            spawnX,
            spawnY,
            launchedVelocityX + (attacker.HorizontalSpeed * (float)Config.FixedDeltaSeconds),
            launchedVelocityY,
            weaponDefinition.DirectHitDamage ?? ShotProjectileEntity.DamagePerHit,
            killFeedWeaponSpriteNameOverride: killFeedWeaponSpriteNameOverride,
            playerKnockbackScale: weaponDefinition.PlayerKnockbackScale,
            playerSlowMovementMultiplier: weaponDefinition.PlayerSlowMovementMultiplier,
            playerSlowRefreshTicks: weaponDefinition.PlayerSlowRefreshSourceTicks > 0
                ? _host.GetSimulationTicksFromSourceTicks(weaponDefinition.PlayerSlowRefreshSourceTicks)
                : 0,
            playerKnockbackImpulse: knockbackPayload.Impulse,
            playerKnockbackAirborneVerticalScale: knockbackPayload.AirborneVerticalScale,
            playerKnockbackGroundedVerticalScale: knockbackPayload.GroundedVerticalScale);
    }

    private float GetWeaponSpreadRadians(int attackerId, float spreadDegrees, int pelletIndex = 0, int projectilesPerShot = 1)
    {
        if (_host.RandomSpreadEnabled)
        {
            return DegreesToRadians((_random.NextSingle() * 2f - 1f) * spreadDegrees);
        }

        if (projectilesPerShot > 1)
        {
            return GetDeterministicPelletSpreadRadians(pelletIndex, projectilesPerShot, spreadDegrees);
        }

        return GetDeterministicContinuousSpreadRadians(attackerId, spreadDegrees);
    }

    private float GetWeaponShotSpeed(PrimaryWeaponDefinition weaponDefinition)
    {
        return _host.RandomSpreadEnabled
            ? weaponDefinition.MinShotSpeed + (_random.NextSingle() * weaponDefinition.AdditionalRandomShotSpeed)
            : weaponDefinition.MinShotSpeed;
    }

    private static float GetDeterministicPelletSpreadRadians(int pelletIndex, int projectilesPerShot, float spreadDegrees)
    {
        if (projectilesPerShot <= 1)
        {
            return 0f;
        }

        var normalized = pelletIndex / (float)(projectilesPerShot - 1);
        var fraction = normalized * 2f - 1f;
        return DegreesToRadians(fraction * spreadDegrees);
    }

    private float GetDeterministicContinuousSpreadRadians(int attackerId, float spreadDegrees)
    {
        var shotIndex = _host.GetDeterministicSpreadShotIndex(attackerId);
        if (shotIndex == 0)
        {
            return 0f;
        }

        var pattern = new[]
        {
            0, 1, -1, 2, -2, 3, -2, 4,
            -4, 3, -3, 2, -2, 1, -1, 0,
        };

        var patternIndex = (shotIndex - 1) % pattern.Length;
        var step = pattern[patternIndex];
        return DegreesToRadians((step / 4f) * spreadDegrees);
    }

    public void FireBoomstick(
        PlayerEntity attacker,
        PrimaryWeaponDefinition weaponDefinition,
        PlayerClass weaponClassId,
        float aimWorldX,
        float aimWorldY,
        string killFeedWeaponSpriteName)
    {
        FirePelletWeapon(
            attacker,
            weaponDefinition,
            aimWorldX,
            aimWorldY,
            weaponClassId,
            killFeedWeaponSpriteName,
            isBoomstickPellet: true);
    }

    private void FirePelletWeapon(
        PlayerEntity attacker,
        PrimaryWeaponDefinition weaponDefinition,
        float aimWorldX,
        float aimWorldY,
        PlayerClass weaponClassId,
        string? killFeedWeaponSpriteNameOverride = null,
        float pelletSpawnDistance = 15f,
        int pelletCountMultiplier = 1,
        float spreadMultiplier = 1f,
        bool forceGibOnKill = false,
        bool isBoomstickPellet = false)
    {
        var weaponOrigin = GetSourceWeaponOrigin(attacker, weaponClassId);
        var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
        var aimDeltaY = aimWorldY - weaponOrigin.BaseY;
        if (aimDeltaX == 0f && aimDeltaY == 0f)
        {
            aimDeltaX = attacker.FacingDirectionX;
        }

        var baseAngle = DeterministicMath.Atan2(aimDeltaY, aimDeltaX);
        var projectileCount = GetExperimentalProjectilesPerShot(
            attacker,
            weaponDefinition.ProjectilesPerShot * Math.Max(1, pelletCountMultiplier));
        var knockbackPayload = BulletKnockbackRules.ResolvePayload(weaponDefinition, projectileCount);
        for (var pelletIndex = 0; pelletIndex < projectileCount; pelletIndex += 1)
        {
            var spreadRadians = GetWeaponSpreadRadians(
                attacker.Id,
                weaponDefinition.SpreadDegrees * MathF.Max(0.1f, spreadMultiplier),
                pelletIndex,
                projectileCount);
            var pelletAngle = baseAngle + spreadRadians;
            var directionX = DeterministicMath.Cos(pelletAngle);
            var directionY = DeterministicMath.Sin(pelletAngle);
            var pelletSpeed = GetWeaponShotSpeed(weaponDefinition);
            var (launchedVelocityX, launchedVelocityY) = _host.ApplyExperimentalProjectileSpeedMultiplier(
                attacker,
                directionX * pelletSpeed,
                directionY * pelletSpeed);
            SpawnShot(
                attacker,
                weaponOrigin.BaseX + directionX * pelletSpawnDistance,
                weaponOrigin.BaseY + directionY * pelletSpawnDistance,
                launchedVelocityX + (attacker.HorizontalSpeed * (float)Config.FixedDeltaSeconds),
                launchedVelocityY,
                weaponDefinition.DirectHitDamage ?? ShotProjectileEntity.DamagePerHit,
                forceGibOnKill,
                killFeedWeaponSpriteNameOverride,
                playerKnockbackScale: weaponDefinition.PlayerKnockbackScale,
                playerSlowMovementMultiplier: weaponDefinition.PlayerSlowMovementMultiplier,
                playerSlowRefreshTicks: weaponDefinition.PlayerSlowRefreshSourceTicks > 0
                    ? _host.GetSimulationTicksFromSourceTicks(weaponDefinition.PlayerSlowRefreshSourceTicks)
                    : 0,
                playerKnockbackImpulse: knockbackPayload.Impulse,
                playerKnockbackAirborneVerticalScale: knockbackPayload.AirborneVerticalScale,
                playerKnockbackGroundedVerticalScale: knockbackPayload.GroundedVerticalScale,
                isBoomstickPellet: isBoomstickPellet);
        }

        TryFireExperimentalEngineerOverkillAugment(
            attacker,
            weaponClassId,
            weaponOrigin,
            baseAngle,
            killFeedWeaponSpriteNameOverride);
    }

    private void TryFireExperimentalEngineerOverkillAugment(
        PlayerEntity attacker,
        PlayerClass weaponClassId,
        SourceWeaponOrigin weaponOrigin,
        float baseAngle,
        string? killFeedWeaponSpriteNameOverride)
    {
        var settings = _host.GetLastToDieGameplaySettings(attacker);
        if (weaponClassId != PlayerClass.Engineer
            || !_host.IsExperimentalPracticePowerOwner(attacker)
            || !settings.EnableEngineerExperimentalOverkillAugment)
        {
            return;
        }

        var rocketCombat = new RocketCombatDefinition(
            ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorDirectHitDamage,
            ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorExplosionDamage,
            ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorBlastRadius,
            RocketProjectileEntity.SplashThresholdFactor);
        var spreadRadians = DegreesToRadians(ExperimentalGameplaySettings.DefaultEngineerExperimentalOverkillAugmentRocketSpreadDegrees);
        var lockDelayTicks = Math.Max(
            0,
            (int)MathF.Round(Config.TicksPerSecond * ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorRocketLockDelaySeconds));
        var spawnX = weaponOrigin.BaseX + DeterministicMath.Cos(baseAngle) * 20f;
        var spawnY = weaponOrigin.BaseY + DeterministicMath.Sin(baseAngle) * 20f;
        var explodeImmediately = _host.IsProjectileSpawnBlocked(weaponOrigin.BaseX, weaponOrigin.BaseY, spawnX, spawnY, attacker.Team);
        for (var rocketIndex = 0; rocketIndex < ExperimentalGameplaySettings.DefaultEngineerExperimentalOverkillAugmentRocketCount; rocketIndex += 1)
        {
            var spreadOffset = ExperimentalGameplaySettings.DefaultEngineerExperimentalOverkillAugmentRocketCount == 1
                ? 0f
                : (rocketIndex == 0 ? -spreadRadians * 0.5f : spreadRadians * 0.5f);
            SpawnRocket(
                attacker,
                spawnX,
                spawnY,
                ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorRocketSpeed,
                baseAngle + spreadOffset,
                rocketCombat,
                explodeImmediately: explodeImmediately,
                canGrantExperimentalInstantReloadOnHit: false,
                enableExperimentalCaveatTracking: true,
                experimentalVisualScale: ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorRocketRenderScale,
                experimentalTrackingLockTicksRemaining: lockDelayTicks,
                killFeedWeaponSpriteNameOverride: killFeedWeaponSpriteNameOverride ?? "ShotgunKL");

            if (_host.Rockets.Count > 0)
            {
                _host.Rockets[^1].SetDistanceToTravel(_host.Bounds.Width + _host.Bounds.Height);
            }
        }
    }

    public void FireExperimentalEngineerDestinyPunctuatorBlast(
        PlayerEntity attacker,
        float aimWorldX,
        float aimWorldY,
        int pelletCountMultiplier)
    {
        DispatchPrimaryWeaponFire(
            attacker,
            attacker.PrimaryWeapon,
            attacker.PrimaryBehaviorId,
            attacker.ClassId,
            aimWorldX,
            aimWorldY,
            pelletSpawnDistance: 20f,
            pelletCountMultiplier: Math.Max(1, pelletCountMultiplier),
            spreadMultiplier: global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerDestinyPunctuatorSpreadMultiplier,
            killFeedWeaponSpriteNameOverride: "ShotgunKL",
            forceGibOnKill: true);
    }

    private void FireRocketLauncher(PlayerEntity attacker, float aimWorldX, float aimWorldY)
    {
        FireRocketLauncher(
            attacker,
            attacker.PrimaryWeapon,
            attacker.ClassId,
            aimWorldX,
            aimWorldY,
            CharacterClassCatalog.GetPrimaryWeaponKillFeedSprite(attacker.GameplayClassId));
    }

    private void FireRocketLauncher(
        PlayerEntity attacker,
        PrimaryWeaponDefinition weaponDefinition,
        PlayerClass weaponClassId,
        float aimWorldX,
        float aimWorldY,
        string killFeedWeaponSpriteNameOverride)
    {
        var weaponOrigin = GetSourceWeaponOrigin(attacker, weaponClassId);
        var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
        var aimDeltaY = aimWorldY - weaponOrigin.BaseY;
        if (aimDeltaX == 0f && aimDeltaY == 0f)
        {
            aimDeltaX = attacker.FacingDirectionX;
        }

        var directionRadians = DeterministicMath.Atan2(aimDeltaY, aimDeltaX);
        var spawnX = weaponOrigin.BaseX + DeterministicMath.Cos(directionRadians) * 20f;
        var spawnY = weaponOrigin.BaseY + DeterministicMath.Sin(directionRadians) * 20f;
        var explodeImmediately = _host.IsProjectileSpawnBlocked(weaponOrigin.BaseX, weaponOrigin.BaseY, spawnX, spawnY, attacker.Team);
        var experimentalSoldierPerkOwner = _host.IsExperimentalPracticePowerOwner(attacker)
            && attacker.ClassId == PlayerClass.Soldier;
        var soldierSettings = _host.GetLastToDieGameplaySettings(attacker);
        var rocketCombat = _host.ApplyExperimentalSoldierRocketCombat(attacker, weaponDefinition.RocketCombat);
        var projectileCount = GetExperimentalProjectilesPerShot(attacker, 1);
        for (var projectileIndex = 0; projectileIndex < projectileCount; projectileIndex += 1)
        {
            var spreadOffset = projectileCount == 1
                ? 0f
                : DegreesToRadians((projectileIndex - ((projectileCount - 1) * 0.5f)) * 7.5f);
            SpawnRocket(
                attacker,
                spawnX,
                spawnY,
                _host.ApplyExperimentalSoldierRocketLaunchSpeed(attacker, weaponDefinition.MinShotSpeed),
                directionRadians + spreadOffset,
                rocketCombat,
                weaponDefinition.DirectHitHealAmount ?? 0f,
                explodeImmediately,
                canGrantExperimentalInstantReloadOnHit: experimentalSoldierPerkOwner
                    && soldierSettings.EnableSoldierInstantReload,
                knockbackScale: experimentalSoldierPerkOwner && soldierSettings.EnableSoldierNapalmRockets
                    ? 0.75f
                    : 1f,
                canIgniteTargets: experimentalSoldierPerkOwner && soldierSettings.EnableSoldierNapalmRockets,
                enableExperimentalStingerTracking: experimentalSoldierPerkOwner && soldierSettings.EnableSoldierStingerRockets,
                killFeedWeaponSpriteNameOverride: killFeedWeaponSpriteNameOverride);
        }

        if (experimentalSoldierPerkOwner
            && soldierSettings.EnableSoldierFinalClipRocketBurst
            && !attacker.LastPrimaryShotIgnoredAmmoCost
            && attacker.CurrentShells == 0)
        {
            _host.QueueExperimentalSoldierFinalRocketBurst(
                attacker,
                spawnX,
                spawnY,
                _host.ApplyExperimentalSoldierRocketLaunchSpeed(attacker, weaponDefinition.MinShotSpeed),
                directionRadians,
                rocketCombat,
                weaponDefinition.DirectHitHealAmount ?? 0f,
                canGrantExperimentalInstantReloadOnHit: soldierSettings.EnableSoldierInstantReload,
                knockbackScale: experimentalSoldierPerkOwner && soldierSettings.EnableSoldierNapalmRockets
                    ? 0.75f
                    : 1f,
                canIgniteTargets: experimentalSoldierPerkOwner && soldierSettings.EnableSoldierNapalmRockets,
                enableStingerTracking: experimentalSoldierPerkOwner && soldierSettings.EnableSoldierStingerRockets,
                killFeedWeaponSpriteNameOverride: killFeedWeaponSpriteNameOverride);
        }
    }

    private void FireRevolver(PlayerEntity attacker, float aimWorldX, float aimWorldY)
    {
        FireRevolver(
            attacker,
            attacker.PrimaryWeapon,
            attacker.ClassId,
            aimWorldX,
            aimWorldY,
            CharacterClassCatalog.GetPrimaryWeaponKillFeedSprite(attacker.GameplayClassId));
    }

    private void FireRevolver(
        PlayerEntity attacker,
        PrimaryWeaponDefinition weaponDefinition,
        PlayerClass weaponClassId,
        float aimWorldX,
        float aimWorldY,
        string killFeedWeaponSpriteNameOverride)
    {
        var isCivvieUmbrella = IsCivvieUmbrellaPrimary(weaponDefinition, weaponClassId);
        var weaponOrigin = GetSourceWeaponOrigin(attacker, weaponClassId);
        var barrelForwardOffset = 18f;
        var shotOriginX = weaponOrigin.BaseX;
        var shotOriginY = weaponOrigin.BaseY + weaponOrigin.WeaponYOffset + 1f;
        var directionRadians = 0f;
        if (isCivvieUmbrella)
        {
            var umbrellaRay = GetCivvieUmbrellaRay(attacker, aimWorldX, aimWorldY);
            barrelForwardOffset = CivvieUmbrellaTipForwardOffset;
            shotOriginX = umbrellaRay.SourceX;
            shotOriginY = umbrellaRay.SourceY;
            directionRadians = umbrellaRay.AimRadians;
        }
        else
        {
            var aimDeltaX = aimWorldX - shotOriginX;
            var aimDeltaY = aimWorldY - shotOriginY;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            directionRadians = DeterministicMath.Atan2(aimDeltaY, aimDeltaX);
        }

        var lastToDieProfile = attacker.ClassId == PlayerClass.Spy
            && weaponClassId == PlayerClass.Spy
            ? attacker.LastToDieSpyRevolverProfile
            : global::OpenGarrison.Core.LastToDie.LastToDieSpyRevolverProfile.Stock;
        var projectileCount = lastToDieProfile.BlunderbussRank > 0
            ? lastToDieProfile.PelletCount
            : GetExperimentalProjectilesPerShot(attacker, weaponDefinition.ProjectilesPerShot);
        var deadlyCritical = lastToDieProfile.DeadlyEnabled
            && _host.TryRollLastToDieSpyDeadlyCritical(attacker);
        var appliesLuckyStrikeStun = lastToDieProfile.LuckyStrikeEnabled
            && attacker.LastPrimaryShotAppliesLastToDieLuckyStrikeStun;
        var knockbackPayload = BulletKnockbackRules.ResolvePayload(weaponDefinition, projectileCount);
        for (var projectileIndex = 0; projectileIndex < projectileCount; projectileIndex += 1)
        {
            // Blunderbuss is a single authored volley. Its pellets form one
            // stable, evenly-spaced arc even when random weapon spread is on.
            var spreadRadians = lastToDieProfile.BlunderbussRank > 0
                ? GetDeterministicPelletSpreadRadians(
                    projectileIndex,
                    projectileCount,
                    weaponDefinition.SpreadDegrees)
                : GetWeaponSpreadRadians(
                    attacker.Id,
                    weaponDefinition.SpreadDegrees,
                    projectileIndex,
                    projectileCount);
            var finalAngle = directionRadians + spreadRadians;
            var (finalVelocityX, finalVelocityY) = _host.ApplyExperimentalProjectileSpeedMultiplier(
                attacker,
                DeterministicMath.Cos(finalAngle) * weaponDefinition.MinShotSpeed,
                DeterministicMath.Sin(finalAngle) * weaponDefinition.MinShotSpeed);
            var nominalSpawnX = shotOriginX + DeterministicMath.Cos(finalAngle) * barrelForwardOffset;
            var nominalSpawnY = shotOriginY + DeterministicMath.Sin(finalAngle) * barrelForwardOffset;
            var spawnBlocked = _host.IsProjectileSpawnBlocked(shotOriginX, shotOriginY, nominalSpawnX, nominalSpawnY, attacker.Team);
            SpawnRevolverShot(
                attacker,
                spawnBlocked ? shotOriginX : nominalSpawnX,
                spawnBlocked ? shotOriginY : nominalSpawnY,
                finalVelocityX + (attacker.HorizontalSpeed * (float)Config.FixedDeltaSeconds),
                finalVelocityY,
                weaponDefinition.DirectHitDamage ?? RevolverProjectileEntity.DamagePerHit,
                killFeedWeaponSpriteNameOverride,
                lastToDieProfile,
                deadlyCritical,
                appliesLuckyStrikeStun,
                knockbackPayload.Impulse,
                knockbackPayload.AirborneVerticalScale,
                knockbackPayload.GroundedVerticalScale);
        }
    }

    private void FireMineLauncher(PlayerEntity attacker, float aimWorldX, float aimWorldY)
    {
        FireMineLauncher(
            attacker,
            attacker.PrimaryWeapon,
            attacker.ClassId,
            aimWorldX,
            aimWorldY,
            CharacterClassCatalog.GetPrimaryWeaponKillFeedSprite(attacker.GameplayClassId));
    }

    private void FireMineLauncher(
        PlayerEntity attacker,
        PrimaryWeaponDefinition weaponDefinition,
        PlayerClass weaponClassId,
        float aimWorldX,
        float aimWorldY,
        string killFeedWeaponSpriteNameOverride)
    {
        if (CountOwnedMines(attacker.Id) >= weaponDefinition.MaxAmmo)
        {
            _host.ExplodeOldestMine(attacker.Id, triggerNearbyMines: false);
        }

        var weaponOrigin = GetSourceWeaponOrigin(attacker, weaponClassId);
        var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
        var aimDeltaY = aimWorldY - weaponOrigin.BaseY;
        if (aimDeltaX == 0f && aimDeltaY == 0f)
        {
            aimDeltaX = attacker.FacingDirectionX;
        }

        var directionRadians = DeterministicMath.Atan2(aimDeltaY, aimDeltaX);
        var nominalSpawnX = weaponOrigin.BaseX + DeterministicMath.Cos(directionRadians) * 10f;
        var nominalSpawnY = weaponOrigin.BaseY + DeterministicMath.Sin(directionRadians) * 10f;
        var spawnBlocked = _host.IsProjectileSpawnBlocked(weaponOrigin.BaseX, weaponOrigin.BaseY, nominalSpawnX, nominalSpawnY, attacker.Team);
        var spawnX = spawnBlocked ? weaponOrigin.BaseX : nominalSpawnX;
        var spawnY = spawnBlocked ? weaponOrigin.BaseY : nominalSpawnY;
        var projectileCount = GetExperimentalProjectilesPerShot(attacker, 1);
        for (var projectileIndex = 0; projectileIndex < projectileCount; projectileIndex += 1)
        {
            var spreadOffset = projectileCount == 1
                ? 0f
                : DegreesToRadians((projectileIndex - ((projectileCount - 1) * 0.5f)) * 7.5f);
            var finalAngle = directionRadians + spreadOffset;
            var (finalVelocityX, finalVelocityY) = _host.ApplyExperimentalProjectileSpeedMultiplier(
                attacker,
                DeterministicMath.Cos(finalAngle) * weaponDefinition.MinShotSpeed,
                DeterministicMath.Sin(finalAngle) * weaponDefinition.MinShotSpeed);
            SpawnMine(
                attacker,
                spawnX,
                spawnY,
                finalVelocityX,
                finalVelocityY,
                killFeedWeaponSpriteNameOverride);
        }
    }

    public void FireGrenadeLauncher(PlayerEntity attacker, float aimWorldX, float aimWorldY)
    {
        var weaponDefinition = CharacterClassCatalog.RuntimeRegistry.CreatePrimaryWeaponDefinition(
            StockGameplayModCatalog.Definition.Items["weapon.grenadelauncher"]);
        var binding = ResolvePrimaryWeaponRuntimeBinding(attacker.UtilityBehaviorId, weaponDefinition);
        TryRegisterPrimaryWeaponFireSound(attacker, weaponDefinition, binding);
        FireGrenadeLauncher(
            attacker,
            weaponDefinition,
            attacker.ClassId,
            aimWorldX,
            aimWorldY,
            "GrenadeLauncherKL");
    }

    private void FireGrenadeLauncher(
        PlayerEntity attacker,
        PrimaryWeaponDefinition weaponDefinition,
        PlayerClass weaponClassId,
        float aimWorldX,
        float aimWorldY,
        string killFeedWeaponSpriteNameOverride)
    {
        var weaponOrigin = GetSourceWeaponOrigin(attacker, weaponClassId);
        var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
        var aimDeltaY = aimWorldY - weaponOrigin.BaseY;
        if (aimDeltaX == 0f && aimDeltaY == 0f)
        {
            aimDeltaX = attacker.FacingDirectionX;
        }

        var directionRadians = DeterministicMath.Atan2(aimDeltaY, aimDeltaX);
        var spawnX = weaponOrigin.BaseX + DeterministicMath.Cos(directionRadians) * 10f;
        var spawnY = weaponOrigin.BaseY + DeterministicMath.Sin(directionRadians) * 10f;
        var projectileCount = GetExperimentalProjectilesPerShot(attacker, 1);
        for (var projectileIndex = 0; projectileIndex < projectileCount; projectileIndex += 1)
        {
            var spreadOffset = projectileCount == 1
                ? 0f
                : DegreesToRadians((projectileIndex - ((projectileCount - 1) * 0.5f)) * 7.5f);
            var finalAngle = directionRadians + spreadOffset;
            var (finalVelocityX, finalVelocityY) = _host.ApplyExperimentalProjectileSpeedMultiplier(
                attacker,
                DeterministicMath.Cos(finalAngle) * weaponDefinition.MinShotSpeed,
                DeterministicMath.Sin(finalAngle) * weaponDefinition.MinShotSpeed);
            SpawnGrenade(
                attacker,
                spawnX,
                spawnY,
                finalVelocityX,
                finalVelocityY,
                killFeedWeaponSpriteNameOverride);
        }
    }

    public void FireStrongDrink(
        PlayerEntity attacker,
        float aimWorldX,
        float aimWorldY,
        float throwSpeed,
        float chargeFraction,
        float spinSpeed,
        int fuseTicks,
        float unreachableLobBiasDegrees = PlayerEntity.StrongDrinkLobBiasDegrees,
        float gravityPerTick = GrenadeProjectileEntity.StrongDrinkGravityPerTick)
    {
        RegisterSoundEvent(attacker, "WhippingCordSwingSnd");
        var weaponOrigin = GetSourceWeaponOrigin(attacker, PlayerClass.Sniper);
        var speed = MathF.Max(0.1f, throwSpeed);
        // Seed spawn slightly toward the crosshair, then solve a through-point lob from there.
        var seedAim = DeterministicMath.Atan2(aimWorldY - weaponOrigin.BaseY, aimWorldX - weaponOrigin.BaseX);
        var spawnX = weaponOrigin.BaseX + DeterministicMath.Cos(seedAim) * 10f;
        var spawnY = weaponOrigin.BaseY + DeterministicMath.Sin(seedAim) * 10f;
        var throwRadians = PlayerEntity.ResolveStrongDrinkThrowDirection(
            spawnX,
            spawnY,
            aimWorldX,
            aimWorldY,
            speed,
            gravityPerTick,
            chargeFraction,
            unreachableLobBiasDegrees);
        var (finalVelocityX, finalVelocityY) = _host.ApplyExperimentalProjectileSpeedMultiplier(
            attacker,
            DeterministicMath.Cos(throwRadians) * speed,
            DeterministicMath.Sin(throwRadians) * speed);
        var spinMagnitude = GrenadeProjectileEntity.StrongDrinkSpinSpeedMin
            + (_random.NextSingle()
                * (GrenadeProjectileEntity.StrongDrinkSpinSpeedMax - GrenadeProjectileEntity.StrongDrinkSpinSpeedMin));
        var spinScale = spinSpeed > 0.0001f
            ? spinSpeed / GrenadeProjectileEntity.StrongDrinkDefaultSpinSpeed
            : 1f;
        var randomizedSpin = spinMagnitude * spinScale * (_random.NextSingle() < 0.5f ? -1f : 1f);
        SpawnGrenade(
            attacker,
            spawnX,
            spawnY,
            finalVelocityX,
            finalVelocityY,
            killFeedWeaponSpriteNameOverride: GrenadeProjectileEntity.StrongDrinkKillFeedSpriteName,
            isStrongDrink: true,
            fuseTicks: fuseTicks,
            initialSpinSpeed: randomizedSpin,
            gravityPerTick: gravityPerTick);
    }
}
