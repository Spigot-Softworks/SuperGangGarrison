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
                killFeedWeaponSpriteNameOverride);
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
            float pelletSpawnDistance = 15f)
        {
            var weaponOrigin = GetSourceWeaponOrigin(attacker, weaponClassId);
            var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
            var aimDeltaY = aimWorldY - weaponOrigin.BaseY;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var baseAngle = MathF.Atan2(aimDeltaY, aimDeltaX);
            for (var pelletIndex = 0; pelletIndex < weaponDefinition.ProjectilesPerShot; pelletIndex += 1)
            {
                var spreadRadians = GetWeaponSpreadRadians(attacker.Id, weaponDefinition.SpreadDegrees, pelletIndex, weaponDefinition.ProjectilesPerShot);
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
                    killFeedWeaponSpriteNameOverride);
            }
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
            SpawnRocket(
                attacker,
                spawnX,
                spawnY,
                _world.ApplyExperimentalSoldierRocketLaunchSpeed(attacker, weaponDefinition.MinShotSpeed),
                directionRadians,
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
            var (launchedVelocityX, launchedVelocityY) = _world.ApplyExperimentalProjectileSpeedMultiplier(
                attacker,
                MathF.Cos(bulletAngle) * weaponDefinition.MinShotSpeed,
                MathF.Sin(bulletAngle) * weaponDefinition.MinShotSpeed);
            SpawnRevolverShot(
                attacker,
                weaponOrigin.BaseX,
                shotOriginY,
                launchedVelocityX + (attacker.HorizontalSpeed * (float)Config.FixedDeltaSeconds),
                launchedVelocityY,
                weaponDefinition.DirectHitDamage ?? RevolverProjectileEntity.DamagePerHit,
                killFeedWeaponSpriteNameOverride);
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
                _world.ExplodeOldestMine(attacker.Id);
            }

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
            var (launchedVelocityX, launchedVelocityY) = _world.ApplyExperimentalProjectileSpeedMultiplier(
                attacker,
                MathF.Cos(directionRadians) * weaponDefinition.MinShotSpeed,
                MathF.Sin(directionRadians) * weaponDefinition.MinShotSpeed);
            SpawnMine(
                attacker,
                spawnX,
                spawnY,
                launchedVelocityX,
                launchedVelocityY,
                killFeedWeaponSpriteNameOverride);
        }
    }
}
