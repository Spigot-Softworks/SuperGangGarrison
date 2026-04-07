namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed partial class WeaponFireHandler
    {
        private void FireMinigun(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            var weaponOrigin = GetSourceWeaponOrigin(attacker);
            var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
            var aimDeltaY = aimWorldY - weaponOrigin.BaseY;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var baseAngle = MathF.Atan2(aimDeltaY, aimDeltaX);
            var spreadRadians = DegreesToRadians((_random.NextSingle() * 2f - 1f) * attacker.PrimaryWeapon.SpreadDegrees);
            var pelletAngle = baseAngle + spreadRadians;
            var directionX = MathF.Cos(pelletAngle);
            var directionY = MathF.Sin(pelletAngle);
            var shotSpeed = attacker.PrimaryWeapon.MinShotSpeed + (_random.NextSingle() * attacker.PrimaryWeapon.AdditionalRandomShotSpeed);
            var (launchedVelocityX, launchedVelocityY) = _world.ApplyExperimentalProjectileSpeedMultiplier(
                attacker,
                directionX * shotSpeed,
                directionY * shotSpeed);
            SpawnShot(
                attacker,
                weaponOrigin.BaseX + directionX * 20f,
                weaponOrigin.BaseY + 12f + directionY * 20f,
                launchedVelocityX + (attacker.HorizontalSpeed * (float)Config.FixedDeltaSeconds),
                launchedVelocityY);
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
            var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
            var aimDeltaY = aimWorldY - weaponOrigin.BaseY;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var baseAngle = MathF.Atan2(aimDeltaY, aimDeltaX);
            var spreadRadians = DegreesToRadians((_random.NextSingle() * 2f - 1f) * weaponDefinition.SpreadDegrees);
            var pelletAngle = baseAngle + spreadRadians;
            var directionX = MathF.Cos(pelletAngle);
            var directionY = MathF.Sin(pelletAngle);
            var shotSpeed = weaponDefinition.MinShotSpeed + (_random.NextSingle() * weaponDefinition.AdditionalRandomShotSpeed);
            var (launchedVelocityX, launchedVelocityY) = _world.ApplyExperimentalProjectileSpeedMultiplier(
                attacker,
                directionX * shotSpeed,
                directionY * shotSpeed);
            SpawnShot(
                attacker,
                weaponOrigin.BaseX + directionX * 20f,
                weaponOrigin.BaseY + 12f + directionY * 20f,
                launchedVelocityX + (attacker.HorizontalSpeed * (float)Config.FixedDeltaSeconds),
                launchedVelocityY,
                weaponDefinition.DirectHitDamage ?? ShotProjectileEntity.DamagePerHit,
                killFeedWeaponSpriteNameOverride);
        }

        private void FireRifle(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            const float rifleDistance = 2000f;

            var weaponOrigin = GetSourceWeaponOrigin(attacker);
            var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
            var aimDeltaY = aimWorldY - weaponOrigin.BaseY;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var distance = MathF.Sqrt((aimDeltaX * aimDeltaX) + (aimDeltaY * aimDeltaY));
            if (distance <= 0.0001f)
            {
                return;
            }

            var directionX = aimDeltaX / distance;
            var directionY = aimDeltaY / distance;
            var result = ResolveRifleHit(attacker, weaponOrigin.BaseX, weaponOrigin.BaseY, directionX, directionY, rifleDistance);
            RegisterCombatTrace(weaponOrigin.BaseX, weaponOrigin.BaseY, directionX, directionY, result.Distance, result.HitPlayer is not null, attacker.Team, isSniperTracer: true);
            var damage = attacker.GetSniperRifleDamage();
            if (result.HitPlayer is not null)
            {
                RegisterBloodEffect(result.HitPlayer.X, result.HitPlayer.Y, PointDirectionDegrees(weaponOrigin.BaseX, weaponOrigin.BaseY, result.HitPlayer.X, result.HitPlayer.Y) - 180f);
                if (ApplyPlayerDamage(result.HitPlayer, damage, attacker, PlayerEntity.SpySniperRevealAlpha))
                {
                    var deadBodyAnimationKind = damage > PlayerEntity.SniperBaseDamage
                        ? DeadBodyAnimationKind.Severe
                        : DeadBodyAnimationKind.Rifle;
                    KillPlayer(result.HitPlayer, killer: attacker, weaponSpriteName: "RifleKL", deadBodyAnimationKind: deadBodyAnimationKind);
                }
            }
            else if (result.HitSentry is not null && ApplySentryDamage(result.HitSentry, damage, attacker))
            {
                DestroySentry(result.HitSentry, attacker);
            }
            else if (result.HitGenerator is not null)
            {
                TryDamageGenerator(result.HitGenerator.Team, damage, attacker);
            }
            else if (result.Distance < rifleDistance)
            {
                RegisterImpactEffect(
                    weaponOrigin.BaseX + directionX * result.Distance,
                    weaponOrigin.BaseY + directionY * result.Distance,
                    PointDirectionDegrees(0f, 0f, directionX, directionY));
            }
        }

        private void FireRifle(
            PlayerEntity attacker,
            PlayerClass weaponClassId,
            float aimWorldX,
            float aimWorldY,
            string killFeedWeaponSpriteNameOverride)
        {
            const float rifleDistance = 2000f;

            var weaponOrigin = GetSourceWeaponOrigin(attacker, weaponClassId);
            var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
            var aimDeltaY = aimWorldY - weaponOrigin.BaseY;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var distance = MathF.Sqrt((aimDeltaX * aimDeltaX) + (aimDeltaY * aimDeltaY));
            if (distance <= 0.0001f)
            {
                return;
            }

            var directionX = aimDeltaX / distance;
            var directionY = aimDeltaY / distance;
            var result = ResolveRifleHit(attacker, weaponOrigin.BaseX, weaponOrigin.BaseY, directionX, directionY, rifleDistance);
            RegisterCombatTrace(weaponOrigin.BaseX, weaponOrigin.BaseY, directionX, directionY, result.Distance, result.HitPlayer is not null, attacker.Team, isSniperTracer: true);
            var damage = attacker.GetSniperRifleDamage();
            if (result.HitPlayer is not null)
            {
                RegisterBloodEffect(result.HitPlayer.X, result.HitPlayer.Y, PointDirectionDegrees(weaponOrigin.BaseX, weaponOrigin.BaseY, result.HitPlayer.X, result.HitPlayer.Y) - 180f);
                if (ApplyPlayerDamage(result.HitPlayer, damage, attacker, PlayerEntity.SpySniperRevealAlpha))
                {
                    var deadBodyAnimationKind = damage > PlayerEntity.SniperBaseDamage
                        ? DeadBodyAnimationKind.Severe
                        : DeadBodyAnimationKind.Rifle;
                    KillPlayer(result.HitPlayer, killer: attacker, weaponSpriteName: killFeedWeaponSpriteNameOverride, deadBodyAnimationKind: deadBodyAnimationKind);
                }
            }
            else if (result.HitSentry is not null && ApplySentryDamage(result.HitSentry, damage, attacker))
            {
                DestroySentry(result.HitSentry, attacker);
            }
            else if (result.HitGenerator is not null)
            {
                TryDamageGenerator(result.HitGenerator.Team, damage, attacker);
            }
            else if (result.Distance < rifleDistance)
            {
                RegisterImpactEffect(
                    weaponOrigin.BaseX + directionX * result.Distance,
                    weaponOrigin.BaseY + directionY * result.Distance,
                    PointDirectionDegrees(0f, 0f, directionX, directionY));
            }
        }

        private void FirePelletWeapon(
            PlayerEntity attacker,
            PrimaryWeaponDefinition weaponDefinition,
            float aimWorldX,
            float aimWorldY,
            PlayerClass weaponClassId,
            string? killFeedWeaponSpriteNameOverride = null)
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
                var spreadRadians = DegreesToRadians((_random.NextSingle() * 2f - 1f) * weaponDefinition.SpreadDegrees);
                var pelletAngle = baseAngle + spreadRadians;
                var directionX = MathF.Cos(pelletAngle);
                var directionY = MathF.Sin(pelletAngle);
                var pelletSpeed = weaponDefinition.MinShotSpeed + (_random.NextSingle() * weaponDefinition.AdditionalRandomShotSpeed);
                var (launchedVelocityX, launchedVelocityY) = _world.ApplyExperimentalProjectileSpeedMultiplier(
                    attacker,
                    directionX * pelletSpeed,
                    directionY * pelletSpeed);
            SpawnShot(
                attacker,
                weaponOrigin.BaseX + directionX * 15f,
                weaponOrigin.BaseY + directionY * 15f,
                launchedVelocityX + (attacker.HorizontalSpeed * (float)Config.FixedDeltaSeconds),
                launchedVelocityY,
                weaponDefinition.DirectHitDamage ?? ShotProjectileEntity.DamagePerHit,
                killFeedWeaponSpriteNameOverride);
            }
        }

        private bool FireFlamethrower(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            var weaponOrigin = GetSourceWeaponOrigin(attacker);
            var sourceX = weaponOrigin.BaseX;
            var sourceY = GetPyroOriginY(weaponOrigin);
            var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
            var aimDeltaY = aimWorldY - sourceY;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var baseAngle = MathF.Atan2(aimDeltaY, aimDeltaX);
            var directionX = MathF.Cos(baseAngle);
            var directionY = MathF.Sin(baseAngle);
            var spawnX = sourceX + directionX * 25f;
            var spawnY = sourceY + directionY * 25f;
            if (IsFlameSpawnBlocked(sourceX, sourceY, spawnX, spawnY, attacker.Team))
            {
                return false;
            }

            var spreadSign = MathF.Sign((_random.NextSingle() * 2f) - 1f);
            var spreadDegrees = spreadSign * MathF.Pow(_random.NextSingle() * 3f, 1.8f);
            var maxRunSpeed = MathF.Max(0.0001f, attacker.MaxRunSpeed);
            spreadDegrees *= 1f - (attacker.HorizontalSpeed / maxRunSpeed);
            var flameAngle = baseAngle + DegreesToRadians(spreadDegrees);
            var flameSpeed = 6.5f + (_random.NextSingle() * 3.5f);
            var (launchedVelocityX, launchedVelocityY) = _world.ApplyExperimentalProjectileSpeedMultiplier(
                attacker,
                MathF.Cos(flameAngle) * flameSpeed,
                MathF.Sin(flameAngle) * flameSpeed);
            SpawnFlame(
                attacker,
                spawnX,
                spawnY,
                launchedVelocityX + (attacker.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond),
                launchedVelocityY + (attacker.VerticalSpeed / LegacyMovementModel.SourceTicksPerSecond));
            return true;
        }

        private bool FireFlamethrower(
            PlayerEntity attacker,
            PrimaryWeaponDefinition weaponDefinition,
            PlayerClass weaponClassId,
            float aimWorldX,
            float aimWorldY)
        {
            var weaponOrigin = GetSourceWeaponOrigin(attacker, weaponClassId);
            var sourceX = weaponOrigin.BaseX;
            var sourceY = GetPyroOriginY(weaponOrigin);
            var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
            var aimDeltaY = aimWorldY - sourceY;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var baseAngle = MathF.Atan2(aimDeltaY, aimDeltaX);
            var directionX = MathF.Cos(baseAngle);
            var directionY = MathF.Sin(baseAngle);
            var spawnX = sourceX + directionX * 25f;
            var spawnY = sourceY + directionY * 25f;
            if (IsFlameSpawnBlocked(sourceX, sourceY, spawnX, spawnY, attacker.Team))
            {
                return false;
            }

            var spreadSign = MathF.Sign((_random.NextSingle() * 2f) - 1f);
            var spreadDegrees = spreadSign * MathF.Pow(_random.NextSingle() * 3f, 1.8f);
            var maxRunSpeed = MathF.Max(0.0001f, attacker.MaxRunSpeed);
            spreadDegrees *= 1f - (attacker.HorizontalSpeed / maxRunSpeed);
            var flameAngle = baseAngle + DegreesToRadians(spreadDegrees);
            var flameSpeed = 6.5f + (_random.NextSingle() * 3.5f);
            var (launchedVelocityX, launchedVelocityY) = _world.ApplyExperimentalProjectileSpeedMultiplier(
                attacker,
                MathF.Cos(flameAngle) * flameSpeed,
                MathF.Sin(flameAngle) * flameSpeed);
            SpawnFlame(
                attacker,
                spawnX,
                spawnY,
                launchedVelocityX + (attacker.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond),
                launchedVelocityY + (attacker.VerticalSpeed / LegacyMovementModel.SourceTicksPerSecond));
            return true;
        }

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

        private void FireRocketLauncher(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            var weaponOrigin = GetSourceWeaponOrigin(attacker);
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
            SpawnRocket(
                attacker,
                spawnX,
                spawnY,
                _world.ApplyExperimentalProjectileSpeedMultiplier(attacker, attacker.PrimaryWeapon.MinShotSpeed),
                directionRadians,
                attacker.PrimaryWeapon.RocketCombat,
                explodeImmediately: explodeImmediately);
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
            SpawnRocket(
                attacker,
                spawnX,
                spawnY,
                _world.ApplyExperimentalProjectileSpeedMultiplier(attacker, weaponDefinition.MinShotSpeed),
                directionRadians,
                weaponDefinition.RocketCombat,
                weaponDefinition.DirectHitHealAmount ?? 0f,
                explodeImmediately,
                canGrantExperimentalInstantReloadOnHit: false,
                killFeedWeaponSpriteNameOverride: killFeedWeaponSpriteNameOverride);
        }

        private void FireRevolver(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            var weaponOrigin = GetSourceWeaponOrigin(attacker);
            var shotOriginY = weaponOrigin.BaseY + weaponOrigin.WeaponYOffset + 1f;
            var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
            var aimDeltaY = aimWorldY - shotOriginY;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var directionRadians = MathF.Atan2(aimDeltaY, aimDeltaX);
            var spreadRadians = DegreesToRadians((_random.NextSingle() * 2f - 1f) * attacker.PrimaryWeapon.SpreadDegrees);
            var bulletAngle = directionRadians + spreadRadians;
            var (launchedVelocityX, launchedVelocityY) = _world.ApplyExperimentalProjectileSpeedMultiplier(
                attacker,
                MathF.Cos(bulletAngle) * attacker.PrimaryWeapon.MinShotSpeed,
                MathF.Sin(bulletAngle) * attacker.PrimaryWeapon.MinShotSpeed);
            SpawnRevolverShot(
                attacker,
                weaponOrigin.BaseX,
                shotOriginY,
                launchedVelocityX + (attacker.HorizontalSpeed * (float)Config.FixedDeltaSeconds),
                launchedVelocityY);
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
            var spreadRadians = DegreesToRadians((_random.NextSingle() * 2f - 1f) * weaponDefinition.SpreadDegrees);
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
            if (CountOwnedMines(attacker.Id) >= attacker.PrimaryWeapon.MaxAmmo)
            {
                return;
            }

            var weaponOrigin = GetSourceWeaponOrigin(attacker);
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
                MathF.Cos(directionRadians) * attacker.PrimaryWeapon.MinShotSpeed,
                MathF.Sin(directionRadians) * attacker.PrimaryWeapon.MinShotSpeed);
            SpawnMine(
                attacker,
                spawnX,
                spawnY,
                launchedVelocityX,
                launchedVelocityY);
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
                return;
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

        public void FireMedicNeedle(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            RegisterSoundEvent(attacker, "MedichaingunSnd");
            var weaponOrigin = GetSourceWeaponOrigin(attacker);
            var shotOriginY = weaponOrigin.BaseY + weaponOrigin.WeaponYOffset + 1f;
            var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
            var aimDeltaY = aimWorldY - shotOriginY;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var directionRadians = MathF.Atan2(aimDeltaY, aimDeltaX);
            var speed = 7f + (_random.NextSingle() * 3f);
            var (launchedVelocityX, launchedVelocityY) = _world.ApplyExperimentalProjectileSpeedMultiplier(
                attacker,
                MathF.Cos(directionRadians) * speed,
                MathF.Sin(directionRadians) * speed);
            SpawnNeedle(
                attacker,
                weaponOrigin.BaseX,
                shotOriginY,
                launchedVelocityX + (attacker.HorizontalSpeed * (float)Config.FixedDeltaSeconds),
                launchedVelocityY);
        }

        public void FireAcquiredMedicNeedle(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            RegisterSoundEvent(attacker, "MedichaingunSnd");
            var weaponOrigin = GetSourceWeaponOrigin(attacker, PlayerClass.Medic);
            var shotOriginY = weaponOrigin.BaseY + weaponOrigin.WeaponYOffset + 1f;
            var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
            var aimDeltaY = aimWorldY - shotOriginY;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var directionRadians = MathF.Atan2(aimDeltaY, aimDeltaX);
            var speed = 7f + (_random.NextSingle() * 3f);
            var (launchedVelocityX, launchedVelocityY) = _world.ApplyExperimentalProjectileSpeedMultiplier(
                attacker,
                MathF.Cos(directionRadians) * speed,
                MathF.Sin(directionRadians) * speed);
            SpawnNeedle(
                attacker,
                weaponOrigin.BaseX,
                shotOriginY,
                launchedVelocityX + (attacker.HorizontalSpeed * (float)Config.FixedDeltaSeconds),
                launchedVelocityY);
        }

        private void RegisterWeaponFireSound(PlayerEntity attacker, PrimaryWeaponDefinition weaponDefinition)
        {
            if (!string.IsNullOrWhiteSpace(weaponDefinition.FireSoundName))
            {
                RegisterSoundEvent(attacker, weaponDefinition.FireSoundName);
                return;
            }

            switch (weaponDefinition.Kind)
            {
                case PrimaryWeaponKind.PelletGun:
                    RegisterSoundEvent(attacker, "ShotgunSnd");
                    break;
                case PrimaryWeaponKind.RocketLauncher:
                    RegisterSoundEvent(attacker, "RocketSnd");
                    break;
                case PrimaryWeaponKind.MineLauncher:
                    RegisterSoundEvent(attacker, "MinegunSnd");
                    break;
                case PrimaryWeaponKind.Minigun:
                    RegisterSoundEvent(attacker, "ChaingunSnd");
                    break;
                case PrimaryWeaponKind.Rifle:
                    RegisterSoundEvent(attacker, "SniperSnd");
                    break;
                case PrimaryWeaponKind.Revolver:
                    RegisterSoundEvent(attacker, "RevolverSnd");
                    break;
            }
        }
    }
}
