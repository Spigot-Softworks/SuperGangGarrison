namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private WeaponFireHandler WeaponHandler => _weaponFireHandler ??= new WeaponFireHandler(this);
    private WeaponFireHandler? _weaponFireHandler;

    private sealed partial class WeaponFireHandler
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
            RocketCombatDefinition? rocketCombat = null,
            bool explodeImmediately = false,
            bool canGrantExperimentalInstantReloadOnHit = true,
            string? killFeedWeaponSpriteNameOverride = null)
        {
            _world.SpawnRocket(owner, x, y, speed, directionRadians, rocketCombat, explodeImmediately, canGrantExperimentalInstantReloadOnHit, killFeedWeaponSpriteNameOverride);
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

    }
}

