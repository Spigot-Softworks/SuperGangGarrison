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

        public void FireScoutNailgun(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            RegisterSoundEvent(attacker, "MedichaingunSnd");
            FireScoutNailgun(attacker, GetSourceWeaponOrigin(attacker), aimWorldX, aimWorldY);
        }

        private void FireScoutNailgun(
            PlayerEntity attacker,
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

            var speed = _world.RandomSpreadEnabled
                ? 10f + (_random.NextSingle() * 3f)
                : 10f;

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
    }
}
