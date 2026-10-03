using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public bool DebugHasLastRocketCollision => Projectiles.DebugHasLastRocketCollision;
    public float DebugLastRocketCollisionX => Projectiles.DebugLastRocketCollisionX;
    public float DebugLastRocketCollisionY => Projectiles.DebugLastRocketCollisionY;
    public string DebugLastRocketCollisionObjectName => Projectiles.DebugLastRocketCollisionObjectName;
    public string DebugLastRocketCollisionReason => Projectiles.DebugLastRocketCollisionReason;
    public bool DebugHasProjectileSpawnBlocked => Projectiles.DebugHasProjectileSpawnBlocked;
    public float DebugProjectileSpawnBlockedX => Projectiles.DebugProjectileSpawnBlockedX;
    public float DebugProjectileSpawnBlockedY => Projectiles.DebugProjectileSpawnBlockedY;
    public float DebugProjectileSpawnBlockedWidth => Projectiles.DebugProjectileSpawnBlockedWidth;
    public float DebugProjectileSpawnBlockedHeight => Projectiles.DebugProjectileSpawnBlockedHeight;
    public string DebugProjectileSpawnBlockedObjectName => Projectiles.DebugProjectileSpawnBlockedObjectName;

    private void SpawnShot(
        PlayerEntity owner,
        float x,
        float y,
        float velocityX,
        float velocityY,
        float damagePerHit = ShotProjectileEntity.DamagePerHit,
        bool forceGibOnKill = false,
        string? killFeedWeaponSpriteNameOverride = null,
        int? sourceSentryId = null,
        bool applyExperimentalEngineerSentryPerkEffects = false,
        float playerKnockbackScale = 1f,
        float? playerSlowMovementMultiplier = null,
        int playerSlowRefreshTicks = 0,
        float? playerKnockbackImpulse = null,
        float playerKnockbackAirborneVerticalScale = 1f,
        float playerKnockbackGroundedVerticalScale = 1f,
        bool isBoomstickPellet = false)
        => Projectiles.SpawnShot(
            owner, x, y, velocityX, velocityY, damagePerHit, forceGibOnKill,
            killFeedWeaponSpriteNameOverride, sourceSentryId,
            applyExperimentalEngineerSentryPerkEffects, playerKnockbackScale,
            playerSlowMovementMultiplier, playerSlowRefreshTicks, playerKnockbackImpulse,
            playerKnockbackAirborneVerticalScale, playerKnockbackGroundedVerticalScale,
            isBoomstickPellet);

    private void SpawnNeedle(
        PlayerEntity owner,
        float x,
        float y,
        float velocityX,
        float velocityY,
        int damagePerHit = NeedleProjectileEntity.DamagePerHit,
        string killFeedWeaponSpriteName = "NeedleKL")
        => Projectiles.SpawnNeedle(owner, x, y, velocityX, velocityY, damagePerHit, killFeedWeaponSpriteName);

    private void SpawnMedicHealNeedle(
        PlayerEntity owner,
        float x,
        float y,
        float velocityX,
        float velocityY,
        int healPerHit = MedicHealNeedleProjectileEntity.DefaultHealPerHit,
        int enemyDamagePerHit = MedicHealNeedleProjectileEntity.DefaultEnemyDamagePerHit)
        => Projectiles.SpawnMedicHealNeedle(owner, x, y, velocityX, velocityY, healPerHit, enemyDamagePerHit);

    private void SpawnRevolverShot(
        PlayerEntity owner,
        float x,
        float y,
        float velocityX,
        float velocityY,
        float damagePerHit = RevolverProjectileEntity.DamagePerHit,
        string? killFeedWeaponSpriteNameOverride = null,
        LastToDieSpyRevolverProfile? lastToDieProfile = null,
        bool forceCritical = false,
        bool appliesLuckyStrikeStun = false,
        float playerKnockbackImpulse = 0f,
        float playerKnockbackAirborneVerticalScale = 1f,
        float playerKnockbackGroundedVerticalScale = 1f)
        => Projectiles.SpawnRevolverShot(
            owner, x, y, velocityX, velocityY, damagePerHit,
            killFeedWeaponSpriteNameOverride, lastToDieProfile, forceCritical,
            appliesLuckyStrikeStun, playerKnockbackImpulse,
            playerKnockbackAirborneVerticalScale, playerKnockbackGroundedVerticalScale);

    private void SpawnFlame(
        PlayerEntity owner,
        float x,
        float y,
        float velocityX,
        float velocityY,
        float directHitDamage = FlameProjectileEntity.DirectHitDamage,
        float burnDamagePerTick = FlameProjectileEntity.BurnDamagePerTick)
        => Projectiles.SpawnFlame(owner, x, y, velocityX, velocityY, directHitDamage, burnDamagePerTick);

    private void SpawnFlare(
        PlayerEntity owner,
        float x,
        float y,
        float velocityX,
        float velocityY,
        float damagePerHit = FlareProjectileEntity.DefaultDamagePerHit,
        string killFeedWeaponSpriteName = "FlareKL",
        FlareProjectileStyle style = FlareProjectileStyle.Standard,
        int lifetimeTicks = FlareProjectileEntity.LifetimeTicks)
        => Projectiles.SpawnFlare(owner, x, y, velocityX, velocityY, damagePerHit, killFeedWeaponSpriteName, style, lifetimeTicks);

    private void SpawnRocket(
        PlayerEntity owner,
        float x,
        float y,
        float speed,
        float directionRadians,
        RocketCombatDefinition? rocketCombat = null,
        float directHitHealAmount = 0f,
        bool explodeImmediately = false,
        bool canGrantExperimentalInstantReloadOnHit = true,
        float knockbackScale = 1f,
        bool canIgniteTargets = false,
        bool enableExperimentalStingerTracking = false,
        bool enableExperimentalCaveatTracking = false,
        float experimentalVisualScale = 1f,
        int experimentalTrackingLockTicksRemaining = 0,
        bool isBallistic = false,
        float ballisticGravityPerTick = 0f,
        bool suppressSmokeTrail = false,
        string? killFeedWeaponSpriteNameOverride = null)
        => Projectiles.SpawnRocket(
            owner, x, y, speed, directionRadians, rocketCombat, directHitHealAmount,
            explodeImmediately, canGrantExperimentalInstantReloadOnHit, knockbackScale,
            canIgniteTargets, enableExperimentalStingerTracking,
            enableExperimentalCaveatTracking, experimentalVisualScale,
            experimentalTrackingLockTicksRemaining, isBallistic, ballisticGravityPerTick,
            suppressSmokeTrail, killFeedWeaponSpriteNameOverride);

    private bool TryShootFriendlyStrongDrink(
        PlayerTeam shooterTeam,
        PlayerClass shooterClass,
        int shooterOwnerId,
        float originX,
        float originY,
        float directionX,
        float directionY,
        float maxDistance,
        int fireParticleCount)
        => Projectiles.TryShootFriendlyStrongDrink(shooterTeam, shooterClass, shooterOwnerId, originX, originY, directionX, directionY, maxDistance, fireParticleCount);

    private int GetSimulationTicksFromSourceTicks(float sourceTicks) => Projectiles.GetSimulationTicksFromSourceTicks(sourceTicks);
    private void ExplodeGrenade(GrenadeProjectileEntity grenade, PlayerEntity? directHitPlayer = null, SimulationEntity? directHitBuilding = null, int directHitDamageableZoneIndex = -1)
        => Projectiles.ExplodeGrenade(grenade, directHitPlayer, directHitBuilding, directHitDamageableZoneIndex);
}
