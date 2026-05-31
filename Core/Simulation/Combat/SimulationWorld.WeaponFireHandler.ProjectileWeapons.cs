namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed partial class WeaponFireHandler
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
            var directionX = MathF.Cos(pelletAngle);
            var directionY = MathF.Sin(pelletAngle);
            var shotSpeed = GetWeaponShotSpeed(weaponDefinition);
            var (launchedVelocityX, launchedVelocityY) = _world.ApplyExperimentalProjectileSpeedMultiplier(
                attacker,
                directionX * shotSpeed,
                directionY * shotSpeed);

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
                    ? _world.GetSimulationTicksFromSourceTicks(weaponDefinition.PlayerSlowRefreshSourceTicks)
                    : 0);
        }

        private float GetWeaponSpreadRadians(int attackerId, float spreadDegrees, int pelletIndex = 0, int projectilesPerShot = 1)
        {
            if (_world.RandomSpreadEnabled)
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
            return _world.RandomSpreadEnabled
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
            var shotIndex = _world.GetDeterministicSpreadShotIndex(attackerId);
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
            bool forceGibOnKill = false)
        {
            var weaponOrigin = GetSourceWeaponOrigin(attacker, weaponClassId);
            var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
            var aimDeltaY = aimWorldY - weaponOrigin.BaseY;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var baseAngle = MathF.Atan2(aimDeltaY, aimDeltaX);
            var projectileCount = GetExperimentalProjectilesPerShot(
                attacker,
                weaponDefinition.ProjectilesPerShot * Math.Max(1, pelletCountMultiplier));
            for (var pelletIndex = 0; pelletIndex < projectileCount; pelletIndex += 1)
            {
                var spreadRadians = GetWeaponSpreadRadians(
                    attacker.Id,
                    weaponDefinition.SpreadDegrees * MathF.Max(0.1f, spreadMultiplier),
                    pelletIndex,
                    projectileCount);
                var pelletAngle = baseAngle + spreadRadians;
                var directionX = MathF.Cos(pelletAngle);
                var directionY = MathF.Sin(pelletAngle);
                var pelletSpeed = GetWeaponShotSpeed(weaponDefinition);
                var (launchedVelocityX, launchedVelocityY) = _world.ApplyExperimentalProjectileSpeedMultiplier(
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
                    killFeedWeaponSpriteNameOverride);
            }

            TryFireExperimentalEngineerOverkillAugment(
                attacker,
                weaponClassId,
                weaponOrigin,
                baseAngle,
                weaponDefinition,
                killFeedWeaponSpriteNameOverride);
        }

        private void TryFireExperimentalEngineerOverkillAugment(
            PlayerEntity attacker,
            PlayerClass weaponClassId,
            SourceWeaponOrigin weaponOrigin,
            float baseAngle,
            PrimaryWeaponDefinition weaponDefinition,
            string? killFeedWeaponSpriteNameOverride)
        {
            if (weaponClassId != PlayerClass.Engineer
                || !_world.IsExperimentalPracticePowerOwner(attacker)
                || !_world.ExperimentalGameplaySettings.EnableEngineerExperimentalOverkillAugment)
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
            var spawnX = weaponOrigin.BaseX + MathF.Cos(baseAngle) * 20f;
            var spawnY = weaponOrigin.BaseY + MathF.Sin(baseAngle) * 20f;
            var explodeImmediately = _world.IsProjectileSpawnBlocked(weaponOrigin.BaseX, weaponOrigin.BaseY, spawnX, spawnY);
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

                if (_world._rockets.Count > 0)
                {
                    _world._rockets[^1].SetDistanceToTravel(_world.Bounds.Width + _world.Bounds.Height);
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
                CharacterClassCatalog.GetPrimaryWeaponKillFeedSprite(attacker.ClassId));
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

            var directionRadians = MathF.Atan2(aimDeltaY, aimDeltaX);
            var spawnX = weaponOrigin.BaseX + MathF.Cos(directionRadians) * 20f;
            var spawnY = weaponOrigin.BaseY + MathF.Sin(directionRadians) * 20f;
            var explodeImmediately = _world.IsProjectileSpawnBlocked(weaponOrigin.BaseX, weaponOrigin.BaseY, spawnX, spawnY);
            var experimentalSoldierPerkOwner = _world.IsExperimentalPracticePowerOwner(attacker)
                && attacker.ClassId == PlayerClass.Soldier;
            var rocketCombat = _world.ApplyExperimentalSoldierRocketCombat(attacker, weaponDefinition.RocketCombat);
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
                    _world.ApplyExperimentalSoldierRocketLaunchSpeed(attacker, weaponDefinition.MinShotSpeed),
                    directionRadians + spreadOffset,
                    rocketCombat,
                    weaponDefinition.DirectHitHealAmount ?? 0f,
                    explodeImmediately,
                    canGrantExperimentalInstantReloadOnHit: experimentalSoldierPerkOwner
                        && _world.ExperimentalGameplaySettings.EnableSoldierInstantReload,
                    knockbackScale: experimentalSoldierPerkOwner && _world.ExperimentalGameplaySettings.EnableSoldierNapalmRockets
                        ? 0.75f
                        : 1f,
                    canIgniteTargets: experimentalSoldierPerkOwner && _world.ExperimentalGameplaySettings.EnableSoldierNapalmRockets,
                    enableExperimentalStingerTracking: experimentalSoldierPerkOwner && _world.ExperimentalGameplaySettings.EnableSoldierStingerRockets,
                    killFeedWeaponSpriteNameOverride: killFeedWeaponSpriteNameOverride);
            }

            if (experimentalSoldierPerkOwner
                && _world.ExperimentalGameplaySettings.EnableSoldierFinalClipRocketBurst
                && !attacker.LastPrimaryShotIgnoredAmmoCost
                && attacker.CurrentShells == 0)
            {
                _world.QueueExperimentalSoldierFinalRocketBurst(
                    attacker,
                    spawnX,
                    spawnY,
                    _world.ApplyExperimentalSoldierRocketLaunchSpeed(attacker, weaponDefinition.MinShotSpeed),
                    directionRadians,
                    rocketCombat,
                    weaponDefinition.DirectHitHealAmount ?? 0f,
                    canGrantExperimentalInstantReloadOnHit: _world.ExperimentalGameplaySettings.EnableSoldierInstantReload,
                    knockbackScale: experimentalSoldierPerkOwner && _world.ExperimentalGameplaySettings.EnableSoldierNapalmRockets
                        ? 0.75f
                        : 1f,
                    canIgniteTargets: experimentalSoldierPerkOwner && _world.ExperimentalGameplaySettings.EnableSoldierNapalmRockets,
                    enableStingerTracking: experimentalSoldierPerkOwner && _world.ExperimentalGameplaySettings.EnableSoldierStingerRockets,
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
                CharacterClassCatalog.GetPrimaryWeaponKillFeedSprite(attacker.ClassId));
        }

        private void FireRevolver(
            PlayerEntity attacker,
            PrimaryWeaponDefinition weaponDefinition,
            PlayerClass weaponClassId,
            float aimWorldX,
            float aimWorldY,
            string killFeedWeaponSpriteNameOverride)
        {
            var weaponOrigin = GetSourceWeaponOrigin(attacker, weaponClassId);
            var shotOriginY = weaponOrigin.BaseY + weaponOrigin.WeaponYOffset + 1f;
            var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
            var aimDeltaY = aimWorldY - shotOriginY;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var directionRadians = MathF.Atan2(aimDeltaY, aimDeltaX);
            var spreadRadians = GetWeaponSpreadRadians(attacker.Id, weaponDefinition.SpreadDegrees);
            var bulletAngle = directionRadians + spreadRadians;
            var projectileCount = GetExperimentalProjectilesPerShot(attacker, 1);
            for (var projectileIndex = 0; projectileIndex < projectileCount; projectileIndex += 1)
            {
                var spreadOffset = projectileCount == 1
                    ? 0f
                    : DegreesToRadians((projectileIndex - ((projectileCount - 1) * 0.5f)) * 6f);
                var finalAngle = bulletAngle + spreadOffset;
                var (finalVelocityX, finalVelocityY) = _world.ApplyExperimentalProjectileSpeedMultiplier(
                    attacker,
                    MathF.Cos(finalAngle) * weaponDefinition.MinShotSpeed,
                    MathF.Sin(finalAngle) * weaponDefinition.MinShotSpeed);
                SpawnRevolverShot(
                    attacker,
                    weaponOrigin.BaseX,
                    shotOriginY,
                    finalVelocityX + (attacker.HorizontalSpeed * (float)Config.FixedDeltaSeconds),
                    finalVelocityY,
                    weaponDefinition.DirectHitDamage ?? RevolverProjectileEntity.DamagePerHit,
                    killFeedWeaponSpriteNameOverride);
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
                CharacterClassCatalog.GetPrimaryWeaponKillFeedSprite(attacker.ClassId));
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
                _world.ExplodeOldestMine(attacker.Id, triggerNearbyMines: false);
            }

            var weaponOrigin = GetSourceWeaponOrigin(attacker, weaponClassId);
            var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
            var aimDeltaY = aimWorldY - weaponOrigin.BaseY;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var directionRadians = MathF.Atan2(aimDeltaY, aimDeltaX);
            var nominalSpawnX = weaponOrigin.BaseX + MathF.Cos(directionRadians) * 10f;
            var nominalSpawnY = weaponOrigin.BaseY + MathF.Sin(directionRadians) * 10f;
            var spawnBlocked = _world.IsProjectileSpawnBlocked(weaponOrigin.BaseX, weaponOrigin.BaseY, nominalSpawnX, nominalSpawnY);
            var spawnX = spawnBlocked ? weaponOrigin.BaseX : nominalSpawnX;
            var spawnY = spawnBlocked ? weaponOrigin.BaseY : nominalSpawnY;
            var projectileCount = GetExperimentalProjectilesPerShot(attacker, 1);
            for (var projectileIndex = 0; projectileIndex < projectileCount; projectileIndex += 1)
            {
                var spreadOffset = projectileCount == 1
                    ? 0f
                    : DegreesToRadians((projectileIndex - ((projectileCount - 1) * 0.5f)) * 7.5f);
                var finalAngle = directionRadians + spreadOffset;
                var (finalVelocityX, finalVelocityY) = _world.ApplyExperimentalProjectileSpeedMultiplier(
                    attacker,
                    MathF.Cos(finalAngle) * weaponDefinition.MinShotSpeed,
                    MathF.Sin(finalAngle) * weaponDefinition.MinShotSpeed);
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

            var directionRadians = MathF.Atan2(aimDeltaY, aimDeltaX);
            var spawnX = weaponOrigin.BaseX + MathF.Cos(directionRadians) * 10f;
            var spawnY = weaponOrigin.BaseY + MathF.Sin(directionRadians) * 10f;
            var projectileCount = GetExperimentalProjectilesPerShot(attacker, 1);
            for (var projectileIndex = 0; projectileIndex < projectileCount; projectileIndex += 1)
            {
                var spreadOffset = projectileCount == 1
                    ? 0f
                    : DegreesToRadians((projectileIndex - ((projectileCount - 1) * 0.5f)) * 7.5f);
                var finalAngle = directionRadians + spreadOffset;
                var (finalVelocityX, finalVelocityY) = _world.ApplyExperimentalProjectileSpeedMultiplier(
                    attacker,
                    MathF.Cos(finalAngle) * weaponDefinition.MinShotSpeed,
                    MathF.Sin(finalAngle) * weaponDefinition.MinShotSpeed);
                SpawnGrenade(
                    attacker,
                    spawnX,
                    spawnY,
                    finalVelocityX,
                    finalVelocityY,
                    killFeedWeaponSpriteNameOverride);
            }
        }
    }
}
