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

        private void RegisterImpactEffect(float x, float y, float directionDegrees)
        {
            _world.RegisterImpactEffect(x, y, directionDegrees);
        }

        private void RegisterSoundEvent(PlayerEntity attacker, string soundName)
        {
            _world.RegisterSoundEvent(attacker, soundName);
        }

        private bool ApplyPlayerDamage(PlayerEntity target, int damage, PlayerEntity? attacker, float spyRevealAlpha = 0f)
        {
            return _world.ApplyPlayerDamage(target, damage, attacker, spyRevealAlpha);
        }

        private bool ApplySentryDamage(SentryEntity sentry, int damage, PlayerEntity? attacker)
        {
            return _world.ApplySentryDamage(sentry, damage, attacker);
        }

        private bool TryDamageGenerator(PlayerTeam targetTeam, float damage, PlayerEntity? attacker)
        {
            return _world.TryDamageGenerator(targetTeam, damage, attacker);
        }

        private void KillPlayer(
            PlayerEntity player,
            bool gibbed = false,
            PlayerEntity? killer = null,
            string? weaponSpriteName = null,
            DeadBodyAnimationKind deadBodyAnimationKind = DeadBodyAnimationKind.Default)
        {
            _world.KillPlayer(player, gibbed, killer, weaponSpriteName, deadBodyAnimationKind);
        }

        private void DestroySentry(SentryEntity sentry, PlayerEntity? attacker = null)
        {
            _world.DestroySentry(sentry, attacker);
        }

        private int CountOwnedMines(int ownerId)
        {
            return _world.CountOwnedMines(ownerId);
        }

        private bool IsFlameSpawnBlocked(float originX, float originY, float spawnX, float spawnY, PlayerTeam team)
        {
            return _world.IsFlameSpawnBlocked(originX, originY, spawnX, spawnY, team);
        }

        private RifleHitResult ResolveRifleHit(PlayerEntity attacker, float directionX, float directionY, float maxDistance)
        {
            return _world.ResolveRifleHit(attacker, directionX, directionY, maxDistance);
        }

        private RifleHitResult ResolveRifleHit(PlayerEntity attacker, float originX, float originY, float directionX, float directionY, float maxDistance)
        {
            return _world.ResolveRifleHit(attacker, originX, originY, directionX, directionY, maxDistance);
        }

        private void SpawnShot(PlayerEntity owner, float x, float y, float velocityX, float velocityY, string? killFeedWeaponSpriteNameOverride = null)
        {
            _world.SpawnShot(owner, x, y, velocityX, velocityY, killFeedWeaponSpriteNameOverride);
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

        private void SpawnFlare(PlayerEntity owner, float x, float y, float velocityX, float velocityY)
        {
            _world.SpawnFlare(owner, x, y, velocityX, velocityY);
        }

        private void SpawnRocket(
            PlayerEntity owner,
            float x,
            float y,
            float speed,
            float directionRadians,
            bool explodeImmediately = false,
            bool canGrantExperimentalInstantReloadOnHit = true,
            string? killFeedWeaponSpriteNameOverride = null)
        {
            _world.SpawnRocket(owner, x, y, speed, directionRadians, explodeImmediately, canGrantExperimentalInstantReloadOnHit, killFeedWeaponSpriteNameOverride);
        }

        private void SpawnRevolverShot(PlayerEntity owner, float x, float y, float velocityX, float velocityY, string? killFeedWeaponSpriteNameOverride = null)
        {
            _world.SpawnRevolverShot(owner, x, y, velocityX, velocityY, killFeedWeaponSpriteNameOverride);
        }

        private void SpawnMine(PlayerEntity owner, float x, float y, float velocityX, float velocityY, string? killFeedWeaponSpriteNameOverride = null)
        {
            _world.SpawnMine(owner, x, y, velocityX, velocityY, killFeedWeaponSpriteNameOverride);
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

        private readonly record struct SourceWeaponOrigin(float BaseX, float BaseY, float WeaponYOffset, float EquipmentOffset);

        private SourceWeaponOrigin GetSourceWeaponOrigin(PlayerEntity attacker)
        {
            return GetSourceWeaponOrigin(attacker, attacker.ClassId);
        }

        private SourceWeaponOrigin GetSourceWeaponOrigin(PlayerEntity attacker, PlayerClass weaponClassId)
        {
            return new SourceWeaponOrigin(
                MathF.Round(attacker.X),
                MathF.Round(attacker.Y),
                GetSourceWeaponYOffset(weaponClassId),
                GetSourceEquipmentOffset(attacker));
        }

        private static float GetPyroOriginY(SourceWeaponOrigin weaponOrigin)
        {
            return weaponOrigin.BaseY + weaponOrigin.WeaponYOffset + weaponOrigin.EquipmentOffset;
        }

        public (float X, float Y) GetPyroSecondaryOrigin(PlayerEntity attacker)
        {
            var weaponClassId = attacker.IsAcquiredWeaponEquipped && attacker.AcquiredWeaponClassId == PlayerClass.Pyro
                ? PlayerClass.Pyro
                : attacker.ClassId;
            var weaponOrigin = GetSourceWeaponOrigin(attacker, weaponClassId);
            return (weaponOrigin.BaseX, GetPyroOriginY(weaponOrigin));
        }

        private static float GetSourceWeaponYOffset(PlayerClass classId)
        {
            return classId switch
            {
                PlayerClass.Scout => -4f,
                PlayerClass.Engineer => -2f,
                PlayerClass.Pyro => 4f,
                PlayerClass.Soldier => -10f,
                PlayerClass.Demoman => -2f,
                PlayerClass.Heavy => 0f,
                PlayerClass.Sniper => -8f,
                PlayerClass.Medic => 0f,
                PlayerClass.Spy => -6f,
                PlayerClass.Quote => -3f,
                _ => 0f,
            };
        }

        private float GetSourceEquipmentOffset(PlayerEntity attacker)
        {
            var horizontalSourceStepSpeed = MathF.Abs(attacker.HorizontalSpeed) / LegacyMovementModel.SourceTicksPerSecond;
            if (!attacker.IsGrounded
                || attacker.IsTaunting
                || attacker.ClassId == PlayerClass.Quote
                || attacker.IsSniperScoped
                || horizontalSourceStepSpeed >= 0.2f)
            {
                return 0f;
            }

            // The simulation does not track the body animation phase yet, so this keeps the stable pose-based
            // portion of the source equipment offset aligned without introducing guessed run-bob phases.
            return GetSourceLeanYOffset(attacker);
        }

        private float GetSourceLeanYOffset(PlayerEntity attacker)
        {
            var bottom = attacker.Bottom + 2f;
            var openRight = !IsPointBlockedForPlayer(attacker, attacker.X + 6f, bottom)
                && !IsPointBlockedForPlayer(attacker, attacker.X + 2f, bottom);
            var openLeft = !IsPointBlockedForPlayer(attacker, attacker.X - 7f, bottom)
                && !IsPointBlockedForPlayer(attacker, attacker.X - 3f, bottom);

            if (openRight && openLeft)
            {
                openRight = !IsPointBlockedForPlayer(attacker, attacker.Right - 1f, bottom);
                openLeft = !IsPointBlockedForPlayer(attacker, attacker.Left, bottom);
            }

            return openRight ^ openLeft ? 6f : 0f;
        }

        private bool IsPointBlockedForPlayer(PlayerEntity player, float x, float y)
        {
            foreach (var solid in _world.Level.Solids)
            {
                if (x >= solid.Left && x < solid.Right && y >= solid.Top && y < solid.Bottom)
                {
                    return true;
                }
            }

            foreach (var gate in _world.Level.GetBlockingTeamGates(player.Team, player.IsCarryingIntel))
            {
                if (x >= gate.Left && x < gate.Right && y >= gate.Top && y < gate.Bottom)
                {
                    return true;
                }
            }

            foreach (var wall in _world.Level.GetRoomObjects(RoomObjectType.PlayerWall))
            {
                if (x >= wall.Left && x < wall.Right && y >= wall.Top && y < wall.Bottom)
                {
                    return true;
                }
            }

            return false;
        }

        public void FirePrimaryWeapon(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            if (attacker.PrimaryWeapon.Kind != PrimaryWeaponKind.FlameThrower)
            {
                RegisterWeaponFireSound(attacker, attacker.PrimaryWeapon);
            }

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
                    FirePelletWeapon(attacker, attacker.PrimaryWeapon, aimWorldX, aimWorldY, attacker.ClassId);
                    break;
            }
        }

        public void FireExperimentalSoldierShotgun(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            var weaponDefinition = attacker.ExperimentalOffhandWeapon ?? CharacterClassCatalog.Shotgun;
            RegisterWeaponFireSound(attacker, weaponDefinition);
            FirePelletWeapon(
                attacker,
                weaponDefinition,
                aimWorldX,
                aimWorldY,
                PlayerClass.Engineer,
                killFeedWeaponSpriteNameOverride: "ShotgunKL");
        }

        public void FireAcquiredWeapon(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            var weaponClassId = attacker.AcquiredWeaponClassId;
            var weaponDefinition = attacker.AcquiredWeapon;
            if (!weaponClassId.HasValue || weaponDefinition is null)
            {
                return;
            }

            var killFeedWeaponSpriteNameOverride = CharacterClassCatalog.GetPrimaryWeaponKillFeedSprite(weaponClassId.Value);
            switch (weaponDefinition.Kind)
            {
                case PrimaryWeaponKind.FlameThrower:
                    FireFlamethrower(attacker, weaponClassId.Value, aimWorldX, aimWorldY);
                    break;
                case PrimaryWeaponKind.Minigun:
                    RegisterWeaponFireSound(attacker, weaponDefinition);
                    FireMinigun(attacker, weaponDefinition, weaponClassId.Value, aimWorldX, aimWorldY, killFeedWeaponSpriteNameOverride);
                    break;
                case PrimaryWeaponKind.MineLauncher:
                    RegisterWeaponFireSound(attacker, weaponDefinition);
                    FireMineLauncher(attacker, weaponDefinition, weaponClassId.Value, aimWorldX, aimWorldY, killFeedWeaponSpriteNameOverride);
                    break;
                case PrimaryWeaponKind.Revolver:
                    RegisterWeaponFireSound(attacker, weaponDefinition);
                    FireRevolver(attacker, weaponDefinition, weaponClassId.Value, aimWorldX, aimWorldY, killFeedWeaponSpriteNameOverride);
                    break;
                case PrimaryWeaponKind.Rifle:
                    RegisterWeaponFireSound(attacker, weaponDefinition);
                    FireRifle(attacker, weaponClassId.Value, aimWorldX, aimWorldY, killFeedWeaponSpriteNameOverride);
                    break;
                case PrimaryWeaponKind.RocketLauncher:
                    RegisterWeaponFireSound(attacker, weaponDefinition);
                    FireRocketLauncher(attacker, weaponDefinition, weaponClassId.Value, aimWorldX, aimWorldY, killFeedWeaponSpriteNameOverride);
                    break;
                case PrimaryWeaponKind.PelletGun:
                    RegisterWeaponFireSound(attacker, weaponDefinition);
                    FirePelletWeapon(attacker, weaponDefinition, aimWorldX, aimWorldY, weaponClassId.Value, killFeedWeaponSpriteNameOverride);
                    break;
            }
        }

        public bool TryFirePyroPrimaryWeapon(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            if (!attacker.TryPreparePyroPrimaryFireAttempt())
            {
                return false;
            }

            var shouldStartLoopSound = attacker.PyroFlameLoopTicksRemaining <= 0;
            if (!FireFlamethrower(attacker, aimWorldX, aimWorldY))
            {
                return false;
            }

            attacker.CommitPyroPrimaryWeaponShot();
            if (shouldStartLoopSound)
            {
                RegisterSoundEvent(attacker, "FlamethrowerSnd");
            }

            return true;
        }
    
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

        public void FireQuoteBlade(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            RegisterSoundEvent(attacker, "BladeSnd");
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
            var bladePower = attacker.CurrentShells;
            var bonusDamage = (int)MathF.Floor((15f / 100f) * bladePower + 3f);
            var hitDamage = 3 + bonusDamage;
            var inheritedVelocityX = attacker.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond;
            var inheritedVelocityY = attacker.VerticalSpeed / LegacyMovementModel.SourceTicksPerSecond;
            var (launchedVelocityX, launchedVelocityY) = _world.ApplyExperimentalProjectileSpeedMultiplier(
                attacker,
                directionX * 12f,
                directionY * 12f);
            SpawnBlade(
                attacker,
                weaponOrigin.BaseX + directionX * 5f,
                weaponOrigin.BaseY + directionY * 5f,
                launchedVelocityX + inheritedVelocityX,
                launchedVelocityY + inheritedVelocityY,
                hitDamage);
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

        private bool FireFlamethrower(PlayerEntity attacker, PlayerClass weaponClassId, float aimWorldX, float aimWorldY)
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
                explodeImmediately);
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
    
        private void RegisterWeaponFireSound(PlayerEntity attacker, PrimaryWeaponDefinition weaponDefinition)
        {
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

