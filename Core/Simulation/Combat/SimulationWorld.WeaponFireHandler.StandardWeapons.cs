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
            FireMedicNeedle(attacker, null, GetSourceWeaponOrigin(attacker), aimWorldX, aimWorldY);
        }

        public void FireMedicNeedlegun(
            PlayerEntity attacker,
            PrimaryWeaponDefinition weapon,
            float aimWorldX,
            float aimWorldY)
        {
            FireMedicNeedle(attacker, weapon, GetSourceWeaponOrigin(attacker, PlayerClass.Medic), aimWorldX, aimWorldY);
        }

        public void FireFlaregun(
            PlayerEntity attacker,
            PrimaryWeaponDefinition weapon,
            float aimWorldX,
            float aimWorldY,
            string killFeedWeaponSpriteName)
        {
            var weaponOrigin = GetSourceWeaponOrigin(attacker, PlayerClass.Pyro);
            var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
            var aimDeltaY = aimWorldY - weaponOrigin.BaseY;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var directionRadians = MathF.Atan2(aimDeltaY, aimDeltaX);
            var speed = MathF.Max(0f, weapon.MinShotSpeed);
            if (_world.RandomSpreadEnabled && weapon.AdditionalRandomShotSpeed > 0f)
            {
                speed += _random.NextSingle() * weapon.AdditionalRandomShotSpeed;
            }

            var directionX = MathF.Cos(directionRadians);
            var directionY = MathF.Sin(directionRadians);
            var spawnX = weaponOrigin.BaseX + (directionX * 13f);
            var spawnY = weaponOrigin.BaseY + weaponOrigin.WeaponYOffset + weaponOrigin.EquipmentOffset + (directionY * 13f);
            if (_world.IsProjectileSpawnBlocked(
                    weaponOrigin.BaseX,
                    weaponOrigin.BaseY + weaponOrigin.WeaponYOffset + weaponOrigin.EquipmentOffset,
                    spawnX,
                    spawnY,
                    attacker.Team))
            {
                spawnX = weaponOrigin.BaseX;
                spawnY = weaponOrigin.BaseY + weaponOrigin.WeaponYOffset + weaponOrigin.EquipmentOffset;
            }

            var (velocityX, velocityY) = _world.ApplyExperimentalProjectileSpeedMultiplier(
                attacker,
                directionX * speed,
                directionY * speed);
            SpawnFlare(
                attacker,
                spawnX,
                spawnY,
                velocityX + (attacker.HorizontalSpeed * (float)Config.FixedDeltaSeconds),
                velocityY,
                weapon.DirectHitDamage ?? 20f,
                killFeedWeaponSpriteName);
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

        public void FireSniperBow(
            PlayerEntity attacker,
            PrimaryWeaponDefinition weapon,
            float aimWorldX,
            float aimWorldY,
            float velocityX,
            float velocityY,
            int damage,
            float fakeSpeedMultiplier,
            string killFeedWeaponSpriteName)
        {
            RegisterSoundEvent(attacker, weapon.FireSoundName ?? "BowSnd");
            var weaponOrigin = GetSourceWeaponOrigin(attacker);
            var pivotRay = GetWeaponPivotRay(
                weaponOrigin.BaseX,
                weaponOrigin.BaseY,
                aimWorldX,
                aimWorldY,
                attacker.FacingDirectionX,
                PlayerEntity.SniperBowPivotOffsetX,
                PlayerEntity.SniperBowPivotOffsetY + weaponOrigin.EquipmentOffset);
            var (launchedVelocityX, launchedVelocityY) = _world.ApplyExperimentalProjectileSpeedMultiplier(
                attacker,
                velocityX,
                velocityY);
            SpawnArrow(
                attacker,
                pivotRay.PivotX,
                pivotRay.PivotY,
                launchedVelocityX,
                launchedVelocityY,
                damage,
                fakeSpeedMultiplier);
            _ = killFeedWeaponSpriteName;
        }

        public void FireQueuedLastToDieSniperBowArrow(
            PlayerEntity attacker,
            in LastToDieSniperVolleyState volley)
        {
            RegisterSoundEvent(attacker, attacker.ExperimentalOffhandWeapon?.FireSoundName ?? "BowSnd");
            var weaponOrigin = GetSourceWeaponOrigin(attacker);
            var directionX = volley.VelocityX;
            var directionY = volley.VelocityY;
            if (MathF.Abs(directionX) + MathF.Abs(directionY) <= 0.0001f)
            {
                directionX = attacker.FacingDirectionX;
            }

            var pivotRay = GetWeaponPivotRay(
                weaponOrigin.BaseX,
                weaponOrigin.BaseY,
                weaponOrigin.BaseX + directionX,
                weaponOrigin.BaseY + directionY,
                attacker.FacingDirectionX,
                PlayerEntity.SniperBowPivotOffsetX,
                PlayerEntity.SniperBowPivotOffsetY + weaponOrigin.EquipmentOffset);
            _world.SpawnQueuedLastToDieSniperArrow(
                attacker,
                pivotRay.PivotX,
                pivotRay.PivotY,
                volley);
        }

        public void FireAcquiredMedicNeedle(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            RegisterSoundEvent(attacker, "MedichaingunSnd");
            FireMedicNeedle(attacker, null, GetSourceWeaponOrigin(attacker, PlayerClass.Medic), aimWorldX, aimWorldY);
        }

        private void FireMedicNeedle(
            PlayerEntity attacker,
            PrimaryWeaponDefinition? weapon,
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
            var spreadDegrees = weapon?.SpreadDegrees ?? 4f;
            if (!_world.RandomSpreadEnabled)
            {
                directionRadians += GetDeterministicContinuousSpreadRadians(attacker.Id, spreadDegrees);
            }
            else if (spreadDegrees > 0f)
            {
                directionRadians += DegreesToRadians(((_random.NextSingle() * 2f) - 1f) * spreadDegrees);
            }

            var minSpeed = MathF.Max(0f, weapon?.MinShotSpeed ?? 7f);
            var additionalSpeed = MathF.Max(0f, weapon?.AdditionalRandomShotSpeed ?? 3f);
            var speed = _world.RandomSpreadEnabled
                ? minSpeed + (_random.NextSingle() * additionalSpeed)
                : minSpeed;

            var (launchedVelocityX, launchedVelocityY) = _world.ApplyExperimentalProjectileSpeedMultiplier(
                attacker,
                MathF.Cos(directionRadians) * speed,
                MathF.Sin(directionRadians) * speed);
            SpawnNeedle(
                attacker,
                spawnX,
                spawnY,
                launchedVelocityX + (attacker.HorizontalSpeed * (float)Config.FixedDeltaSeconds),
                launchedVelocityY,
                damagePerHit: Math.Max(
                    0,
                    (int)MathF.Round(weapon?.DirectHitDamage ?? NeedleProjectileEntity.DamagePerHit)),
                killFeedWeaponSpriteName: weapon?.KillFeedWeaponSpriteName ?? "NeedleKL");
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
