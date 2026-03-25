namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private WeaponFireHandler WeaponHandler => _weaponFireHandler ??= new WeaponFireHandler(this);
    private WeaponFireHandler? _weaponFireHandler;

    private sealed class WeaponFireHandler
    {
        private readonly SimulationWorld _world;

        public WeaponFireHandler(SimulationWorld world)
        {
            _world = world;
        }

        private Random _random => _world._random;

        private SimulationConfig Config => _world.Config;

        private void RegisterCombatTrace(
            float originX,
            float originY,
            float directionX,
            float directionY,
            float distance,
            bool hitCharacter,
            PlayerTeam team = PlayerTeam.Red,
            bool isSniperTracer = false)
        {
            _world.RegisterCombatTrace(originX, originY, directionX, directionY, distance, hitCharacter, team, isSniperTracer);
        }

        private void RegisterBloodEffect(float x, float y, float directionDegrees, int count = 1)
        {
            _world.RegisterBloodEffect(x, y, directionDegrees, count);
        }

        private void RegisterSoundEvent(PlayerEntity attacker, string soundName)
        {
            _world.RegisterSoundEvent(attacker, soundName);
        }

        private void KillPlayer(PlayerEntity player, bool gibbed = false, PlayerEntity? killer = null, string? weaponSpriteName = null)
        {
            _world.KillPlayer(player, gibbed, killer, weaponSpriteName);
        }

        private void DestroySentry(SentryEntity sentry)
        {
            _world.DestroySentry(sentry);
        }

        private int CountOwnedMines(int ownerId)
        {
            return _world.CountOwnedMines(ownerId);
        }

        private bool IsFlameSpawnBlocked(PlayerEntity attacker, float spawnX, float spawnY)
        {
            return _world.IsFlameSpawnBlocked(attacker, spawnX, spawnY);
        }

        private RifleHitResult ResolveRifleHit(PlayerEntity attacker, float directionX, float directionY, float maxDistance)
        {
            return _world.ResolveRifleHit(attacker, directionX, directionY, maxDistance);
        }

        private void SpawnShot(PlayerEntity owner, float x, float y, float velocityX, float velocityY)
        {
            _world.SpawnShot(owner, x, y, velocityX, velocityY);
        }

        private void SpawnBubble(PlayerEntity owner, float x, float y, float velocityX, float velocityY)
        {
            _world.SpawnBubble(owner, x, y, velocityX, velocityY);
        }

        private void SpawnBlade(PlayerEntity owner, float x, float y, float velocityX, float velocityY, int hitDamage)
        {
            _world.SpawnBlade(owner, x, y, velocityX, velocityY, hitDamage);
        }

        private void SpawnFlame(PlayerEntity owner, float x, float y, float velocityX, float velocityY)
        {
            _world.SpawnFlame(owner, x, y, velocityX, velocityY);
        }

        private void SpawnRocket(PlayerEntity owner, float x, float y, float speed, float directionRadians, bool explodeImmediately = false)
        {
            _world.SpawnRocket(owner, x, y, speed, directionRadians, explodeImmediately);
        }

        private void SpawnRevolverShot(PlayerEntity owner, float x, float y, float velocityX, float velocityY)
        {
            _world.SpawnRevolverShot(owner, x, y, velocityX, velocityY);
        }

        private void SpawnMine(PlayerEntity owner, float x, float y, float velocityX, float velocityY)
        {
            _world.SpawnMine(owner, x, y, velocityX, velocityY);
        }

        private void SpawnNeedle(PlayerEntity owner, float x, float y, float velocityX, float velocityY)
        {
            _world.SpawnNeedle(owner, x, y, velocityX, velocityY);
        }

        private static float DegreesToRadians(float degrees)
        {
            return SimulationWorld.DegreesToRadians(degrees);
        }

        private static float PointDirectionDegrees(float x1, float y1, float x2, float y2)
        {
            return SimulationWorld.PointDirectionDegrees(x1, y1, x2, y2);
        }
        public void FirePrimaryWeapon(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            RegisterLocalPrimaryFireSound(attacker);
            switch (attacker.PrimaryWeapon.Kind)
            {
                case PrimaryWeaponKind.FlameThrower:
                    FireFlamethrower(attacker, aimWorldX, aimWorldY);
                    break;
                case PrimaryWeaponKind.Blade:
                    FireBladeBubble(attacker, aimWorldX, aimWorldY);
                    break;
                case PrimaryWeaponKind.Minigun:
                    FireMinigun(attacker, aimWorldX, aimWorldY);
                    break;
                case PrimaryWeaponKind.MineLauncher:
                    FireMineLauncher(attacker, aimWorldX, aimWorldY);
                    break;
                case PrimaryWeaponKind.Revolver:
                    FireRevolver(attacker, aimWorldX, aimWorldY);
                    break;
                case PrimaryWeaponKind.Rifle:
                    FireRifle(attacker, aimWorldX, aimWorldY);
                    break;
                case PrimaryWeaponKind.RocketLauncher:
                    FireRocketLauncher(attacker, aimWorldX, aimWorldY);
                    break;
                default:
                    FirePelletWeapon(attacker, aimWorldX, aimWorldY);
                    break;
            }
        }
    
        private void FireMinigun(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            var aimDeltaX = aimWorldX - attacker.X;
            var aimDeltaY = aimWorldY - attacker.Y;
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
            SpawnShot(
                attacker,
                attacker.X + directionX * 20f,
                attacker.Y + 12f + directionY * 20f,
                directionX * shotSpeed + (attacker.HorizontalSpeed * (float)Config.FixedDeltaSeconds),
                directionY * shotSpeed);
        }
    
        private void FireRifle(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            const float rifleDistance = 2000f;
    
            var aimDeltaX = aimWorldX - attacker.X;
            var aimDeltaY = aimWorldY - attacker.Y;
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
            var result = ResolveRifleHit(attacker, directionX, directionY, rifleDistance);
            RegisterCombatTrace(attacker.X, attacker.Y, directionX, directionY, result.Distance, result.HitPlayer is not null, attacker.Team, isSniperTracer: true);
            var damage = attacker.GetSniperRifleDamage();
            if (result.HitPlayer is not null)
            {
                RegisterBloodEffect(result.HitPlayer.X, result.HitPlayer.Y, PointDirectionDegrees(attacker.X, attacker.Y, result.HitPlayer.X, result.HitPlayer.Y) - 180f);
                if (result.HitPlayer.ApplyDamage(damage, PlayerEntity.SpySniperRevealAlpha))
                {
                    KillPlayer(result.HitPlayer, killer: attacker, weaponSpriteName: "RifleKL");
                }
            }
            else if (result.HitSentry is not null && result.HitSentry.ApplyDamage(damage))
            {
                DestroySentry(result.HitSentry);
            }
            else if (result.HitGenerator is not null)
            {
                _world.TryDamageGenerator(result.HitGenerator.Team, damage);
            }
        }

        public void FireQuoteBlade(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            RegisterSoundEvent(attacker, "BladeSnd");
            var aimDeltaX = aimWorldX - attacker.X;
            var aimDeltaY = aimWorldY - attacker.Y;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var directionRadians = MathF.Atan2(aimDeltaY, aimDeltaX);
            var directionX = MathF.Cos(directionRadians);
            var directionY = MathF.Sin(directionRadians);
            var bladePower = attacker.CurrentShells;
            var bonusDamage = (int)MathF.Floor((15f / 100f) * bladePower + 3f);
            var hitDamage = 3 + bonusDamage;
            SpawnBlade(
                attacker,
                attacker.X + directionX * 10f,
                attacker.Y + directionY * 10f,
                directionX * 12f,
                directionY * 12f,
                hitDamage);
        }
    
        private void FirePelletWeapon(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            var aimDeltaX = aimWorldX - attacker.X;
            var aimDeltaY = aimWorldY - attacker.Y;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }
    
            var baseAngle = MathF.Atan2(aimDeltaY, aimDeltaX);
            for (var pelletIndex = 0; pelletIndex < attacker.PrimaryWeapon.ProjectilesPerShot; pelletIndex += 1)
            {
                var spreadRadians = DegreesToRadians((_random.NextSingle() * 2f - 1f) * attacker.PrimaryWeapon.SpreadDegrees);
                var pelletAngle = baseAngle + spreadRadians;
                var directionX = MathF.Cos(pelletAngle);
                var directionY = MathF.Sin(pelletAngle);
                var pelletSpeed = attacker.PrimaryWeapon.MinShotSpeed + (_random.NextSingle() * attacker.PrimaryWeapon.AdditionalRandomShotSpeed);
                SpawnShot(
                    attacker,
                    attacker.X,
                    attacker.Y,
                    directionX * pelletSpeed + (attacker.HorizontalSpeed * (float)Config.FixedDeltaSeconds),
                    directionY * pelletSpeed);
            }
        }
    
        private void FireFlamethrower(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            var aimDeltaX = aimWorldX - attacker.X;
            var aimDeltaY = aimWorldY - attacker.Y;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var baseAngle = MathF.Atan2(aimDeltaY, aimDeltaX);
            var directionX = MathF.Cos(baseAngle);
            var directionY = MathF.Sin(baseAngle);
            var spawnX = attacker.X + directionX * 25f;
            var spawnY = attacker.Y + directionY * 25f;
            if (IsFlameSpawnBlocked(attacker, spawnX, spawnY))
            {
                return;
            }

            var spreadSign = MathF.Sign((_random.NextSingle() * 2f) - 1f);
            var spreadDegrees = spreadSign * MathF.Pow(_random.NextSingle() * 3f, 1.8f);
            var maxRunSpeed = MathF.Max(0.0001f, attacker.MaxRunSpeed);
            spreadDegrees *= 1f - (attacker.HorizontalSpeed / maxRunSpeed);
            var flameAngle = baseAngle + DegreesToRadians(spreadDegrees);
            var flameSpeed = 6.5f + (_random.NextSingle() * 3.5f);
            SpawnFlame(
                attacker,
                spawnX,
                spawnY,
                MathF.Cos(flameAngle) * flameSpeed + (attacker.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond),
                MathF.Sin(flameAngle) * flameSpeed + (attacker.VerticalSpeed / LegacyMovementModel.SourceTicksPerSecond));
        }

        private void FireBladeBubble(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            var aimDeltaX = aimWorldX - attacker.X;
            var aimDeltaY = aimWorldY - attacker.Y;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var directionRadians = MathF.Atan2(aimDeltaY, aimDeltaX);
            var directionX = MathF.Cos(directionRadians);
            var directionY = MathF.Sin(directionRadians);
            var bubbleSpeed = 10f;
            SpawnBubble(
                attacker,
                attacker.X + directionX * 8f,
                attacker.Y + directionY * 8f,
                directionX * bubbleSpeed + (attacker.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond),
                directionY * bubbleSpeed + (attacker.VerticalSpeed / LegacyMovementModel.SourceTicksPerSecond));
        }
    
        private void FireRocketLauncher(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            var aimDeltaX = aimWorldX - attacker.X;
            var aimDeltaY = aimWorldY - attacker.Y;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var directionRadians = MathF.Atan2(aimDeltaY, aimDeltaX);
            var spawnX = attacker.X + MathF.Cos(directionRadians) * 20f;
            var spawnY = attacker.Y + MathF.Sin(directionRadians) * 20f;
            var explodeImmediately = _world.IsProjectileSpawnBlocked(attacker.X, attacker.Y, spawnX, spawnY);
            SpawnRocket(attacker, spawnX, spawnY, attacker.PrimaryWeapon.MinShotSpeed, directionRadians, explodeImmediately);
        }
    
        private void FireRevolver(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            var aimDeltaX = aimWorldX - attacker.X;
            var aimDeltaY = aimWorldY - attacker.Y + 1f;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }
    
            var directionRadians = MathF.Atan2(aimDeltaY, aimDeltaX);
            var spreadRadians = DegreesToRadians((_random.NextSingle() * 2f - 1f) * attacker.PrimaryWeapon.SpreadDegrees);
            var bulletAngle = directionRadians + spreadRadians;
            SpawnRevolverShot(
                attacker,
                attacker.X,
                attacker.Y - 5f,
                MathF.Cos(bulletAngle) * attacker.PrimaryWeapon.MinShotSpeed + (attacker.HorizontalSpeed * (float)Config.FixedDeltaSeconds),
                MathF.Sin(bulletAngle) * attacker.PrimaryWeapon.MinShotSpeed);
        }
    
        private void FireMineLauncher(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            if (CountOwnedMines(attacker.Id) >= attacker.PrimaryWeapon.MaxAmmo)
            {
                return;
            }
    
            var aimDeltaX = aimWorldX - attacker.X;
            var aimDeltaY = aimWorldY - attacker.Y;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }
    
            var directionRadians = MathF.Atan2(aimDeltaY, aimDeltaX);
            var spawnX = attacker.X + MathF.Cos(directionRadians) * 10f;
            var spawnY = attacker.Y + MathF.Sin(directionRadians) * 10f;
            SpawnMine(
                attacker,
                spawnX,
                spawnY,
                MathF.Cos(directionRadians) * attacker.PrimaryWeapon.MinShotSpeed,
                MathF.Sin(directionRadians) * attacker.PrimaryWeapon.MinShotSpeed);
        }
    
        public void FireMedicNeedle(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            RegisterSoundEvent(attacker, "MedichaingunSnd");
            var aimDeltaX = aimWorldX - attacker.X;
            var aimDeltaY = aimWorldY - attacker.Y;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }
    
            var directionRadians = MathF.Atan2(aimDeltaY, aimDeltaX);
            var speed = 7f + (_random.NextSingle() * 3f);
            SpawnNeedle(
                attacker,
                attacker.X,
                attacker.Y + 1f,
                MathF.Cos(directionRadians) * speed + (attacker.HorizontalSpeed * (float)Config.FixedDeltaSeconds),
                MathF.Sin(directionRadians) * speed);
        }
    
        private void RegisterLocalPrimaryFireSound(PlayerEntity attacker)
        {
            switch (attacker.PrimaryWeapon.Kind)
            {
                case PrimaryWeaponKind.PelletGun:
                    RegisterSoundEvent(attacker, "ShotgunSnd");
                    break;
                case PrimaryWeaponKind.FlameThrower:
                    RegisterSoundEvent(attacker, "FlamethrowerSnd");
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

