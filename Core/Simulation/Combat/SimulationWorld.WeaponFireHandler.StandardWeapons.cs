namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed partial class WeaponFireHandler
    {
        private void FireBladeBubble(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            var weaponOrigin = GetSourceWeaponOrigin(attacker);
            var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
            var aimDeltaY = aimWorldY - weaponOrigin.BaseY;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var directionRadians = MathF.Atan2(aimDeltaY, aimDeltaX);
            var directionX = MathF.Cos(directionRadians);
            var directionY = MathF.Sin(directionRadians);
            var bubbleSpeed = 10f;
            var (launchedVelocityX, launchedVelocityY) = _world.ApplyExperimentalProjectileSpeedMultiplier(
                attacker,
                directionX * bubbleSpeed,
                directionY * bubbleSpeed);
            SpawnBubble(
                attacker,
                weaponOrigin.BaseX + directionX * 8f,
                weaponOrigin.BaseY + directionY * 8f,
                launchedVelocityX + (attacker.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond),
                launchedVelocityY + (attacker.VerticalSpeed / LegacyMovementModel.SourceTicksPerSecond));
        }

        public void FireMedicNeedle(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            RegisterSoundEvent(attacker, "MedichaingunSnd");
            FireMedicNeedle(attacker, GetSourceWeaponOrigin(attacker), aimWorldX, aimWorldY);
        }

        public void FireScoutNailgun(PlayerEntity attacker, PrimaryWeaponDefinition weapon, float aimWorldX, float aimWorldY)
        {
            RegisterSoundEvent(attacker, "MedichaingunSnd");
            FireScoutNailgun(attacker, weapon, GetSourceWeaponOrigin(attacker), aimWorldX, aimWorldY);
        }

        private void FireScoutNailgun(
            PlayerEntity attacker,
            PrimaryWeaponDefinition weapon,
            SourceWeaponOrigin weaponOrigin,
            float aimWorldX,
            float aimWorldY)
        {
            var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
            var aimDeltaY = aimWorldY - weaponOrigin.BaseY;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var aimRadians = MathF.Atan2(aimDeltaY, aimDeltaX);
            var facingScale = MathF.Cos(aimRadians) < 0f ? -1f : 1f;

            // Nailgun weapon sprite values (from weapon.scout-nailgun.json and NailgunS.json):
            // weaponOffsetX = -7, weaponOffsetY = 0, originX = 8, originY = 3
            const float nailgunWeaponOffsetX = -7f;
            const float nailgunWeaponOffsetY = 0f;
            const float nailgunSpriteOriginX = 8f;
            const float nailgunSpriteOriginY = 3f;

            var spawnX = weaponOrigin.BaseX + ((nailgunWeaponOffsetX + nailgunSpriteOriginX) * facingScale);
            var spawnY = weaponOrigin.BaseY + weaponOrigin.WeaponYOffset + (nailgunWeaponOffsetY + weaponOrigin.EquipmentOffset + nailgunSpriteOriginY);

            const float nailSpriteHeightOffset = -2f;
            spawnY += nailSpriteHeightOffset;

            var shotAimDeltaX = aimWorldX - spawnX;
            var shotAimDeltaY = aimWorldY - spawnY;
            if (shotAimDeltaX == 0f && shotAimDeltaY == 0f)
            {
                shotAimDeltaX = facingScale;
            }

            var directionRadians = MathF.Atan2(shotAimDeltaY, shotAimDeltaX);
            if (!_world.RandomSpreadEnabled)
            {
                directionRadians += GetDeterministicContinuousSpreadRadians(attacker.Id, 4f);
            }

            var minSpeed = MathF.Max(0f, weapon.MinShotSpeed);
            var additionalRandomSpeed = MathF.Max(0f, weapon.AdditionalRandomShotSpeed);
            var speed = _world.RandomSpreadEnabled
                ? minSpeed + (_random.NextSingle() * additionalRandomSpeed)
                : minSpeed;

            var (launchedVelocityX, launchedVelocityY) = _world.ApplyExperimentalProjectileSpeedMultiplier(
                attacker,
                MathF.Cos(directionRadians) * speed,
                MathF.Sin(directionRadians) * speed);
            SpawnNail(
                attacker,
                spawnX,
                spawnY,
                launchedVelocityX + (attacker.HorizontalSpeed * (float)Config.FixedDeltaSeconds),
                launchedVelocityY);
        }

        public void FireAcquiredMedicNeedle(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            RegisterSoundEvent(attacker, "MedichaingunSnd");
            FireMedicNeedle(attacker, GetSourceWeaponOrigin(attacker, PlayerClass.Medic), aimWorldX, aimWorldY);
        }

        private void FireMedicNeedle(
            PlayerEntity attacker,
            SourceWeaponOrigin weaponOrigin,
            float aimWorldX,
            float aimWorldY)
        {
            // Calculate aim direction to determine facing
            var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
            var aimDeltaY = aimWorldY - weaponOrigin.BaseY;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var aimRadians = MathF.Atan2(aimDeltaY, aimDeltaX);
            var facingScale = MathF.Cos(aimRadians) < 0f ? -1f : 1f;

            // Medigun weapon sprite values (from weapon.medigun.json and MedigunS.json):
            // weaponOffsetX = -7, weaponOffsetY = 0, originX = 8, originY = 3
            // This matches the healing beam anchor calculation exactly
            const float medicWeaponOffsetX = -7f;
            const float medicWeaponOffsetY = 0f;
            const float medicWeaponSpriteOriginX = 8f;
            const float medicWeaponSpriteOriginY = 3f;

            var spawnX = weaponOrigin.BaseX + ((medicWeaponOffsetX + medicWeaponSpriteOriginX) * facingScale);
            var spawnY = weaponOrigin.BaseY + weaponOrigin.WeaponYOffset + (medicWeaponOffsetY + weaponOrigin.EquipmentOffset + medicWeaponSpriteOriginY);

            // Needle sprite has origin at (0, 0) - top left corner
            // Adjust spawn position so needle appears centered at the weapon anchor
            // Needle sprite is roughly 2-3 pixels tall, so offset down by half
            const float needleSpriteHeightOffset = -2f;
            spawnY += needleSpriteHeightOffset;

            // Calculate firing direction from spawn point
            var shotAimDeltaX = aimWorldX - spawnX;
            var shotAimDeltaY = aimWorldY - spawnY;
            if (shotAimDeltaX == 0f && shotAimDeltaY == 0f)
            {
                shotAimDeltaX = facingScale;
            }

            var directionRadians = MathF.Atan2(shotAimDeltaY, shotAimDeltaX);
            if (!_world.RandomSpreadEnabled)
            {
                directionRadians += GetDeterministicContinuousSpreadRadians(attacker.Id, 4f);
            }

            var speed = _world.RandomSpreadEnabled
                ? 7f + (_random.NextSingle() * 3f)
                : 7f;

            var (launchedVelocityX, launchedVelocityY) = _world.ApplyExperimentalProjectileSpeedMultiplier(
                attacker,
                MathF.Cos(directionRadians) * speed,
                MathF.Sin(directionRadians) * speed);
            SpawnNeedle(
                attacker,
                spawnX,
                spawnY,
                launchedVelocityX + (attacker.HorizontalSpeed * (float)Config.FixedDeltaSeconds),
                launchedVelocityY);
        }

        public void FireMedicKritzHealNeedle(
            PlayerEntity attacker,
            PrimaryWeaponDefinition weaponDefinition,
            float aimWorldX,
            float aimWorldY,
            int healPerHit = MedicHealNeedleProjectileEntity.DefaultHealPerHit,
            int enemyDamagePerHit = MedicHealNeedleProjectileEntity.DefaultEnemyDamagePerHit)
        {
            RegisterSoundEvent(attacker, "MedichaingunSnd");
            var weaponOrigin = GetSourceWeaponOrigin(attacker, PlayerClass.Medic);
            var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
            var aimDeltaY = aimWorldY - weaponOrigin.BaseY;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var aimRadians = MathF.Atan2(aimDeltaY, aimDeltaX);
            var facingScale = MathF.Cos(aimRadians) < 0f ? -1f : 1f;
            const float medicWeaponOffsetX = -7f;
            const float medicWeaponOffsetY = 0f;
            const float medicWeaponSpriteOriginX = 8f;
            const float medicWeaponSpriteOriginY = 3f;
            var shotOriginX = weaponOrigin.BaseX + ((medicWeaponOffsetX + medicWeaponSpriteOriginX) * facingScale);
            var shotOriginY = weaponOrigin.BaseY + weaponOrigin.WeaponYOffset + (medicWeaponOffsetY + weaponOrigin.EquipmentOffset + medicWeaponSpriteOriginY) - 2f;
            var barrelForwardOffset = 18f;
            var spreadRadians = GetWeaponSpreadRadians(attacker.Id, weaponDefinition.SpreadDegrees);
            var directionRadians = MathF.Atan2(aimWorldY - shotOriginY, aimWorldX - shotOriginX) + spreadRadians;
            var nominalSpawnX = shotOriginX + MathF.Cos(directionRadians) * barrelForwardOffset;
            var nominalSpawnY = shotOriginY + MathF.Sin(directionRadians) * barrelForwardOffset;
            var spawnBlocked = _world.IsProjectileSpawnBlocked(shotOriginX, shotOriginY, nominalSpawnX, nominalSpawnY, attacker.Team);
            var (finalVelocityX, finalVelocityY) = _world.ApplyExperimentalProjectileSpeedMultiplier(
                attacker,
                MathF.Cos(directionRadians) * weaponDefinition.MinShotSpeed,
                MathF.Sin(directionRadians) * weaponDefinition.MinShotSpeed);
            SpawnMedicHealNeedle(
                attacker,
                spawnBlocked ? shotOriginX : nominalSpawnX,
                spawnBlocked ? shotOriginY : nominalSpawnY,
                finalVelocityX + (attacker.HorizontalSpeed * (float)Config.FixedDeltaSeconds),
                finalVelocityY,
                healPerHit,
                enemyDamagePerHit);
        }
    }
}
