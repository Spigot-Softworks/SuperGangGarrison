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

    private void SpawnBubble(PlayerEntity owner, float x, float y, float velocityX, float velocityY)
        => Projectiles.SpawnBubble(owner, x, y, velocityX, velocityY);

    private void SpawnBlade(PlayerEntity owner, float x, float y, float velocityX, float velocityY, int hitDamage, int lifetimeTicks = PlayerEntity.QuoteBladeLifetimeTicks)
        => Projectiles.SpawnBlade(owner, x, y, velocityX, velocityY, hitDamage, lifetimeTicks);

    private void SpawnNail(PlayerEntity owner, float x, float y, float velocityX, float velocityY)
        => Projectiles.SpawnNail(owner, x, y, velocityX, velocityY);

    private void SpawnArrow(PlayerEntity owner, float x, float y, float velocityX, float velocityY, int damage, float fakeSpeedMultiplier)
        => Projectiles.SpawnArrow(owner, x, y, velocityX, velocityY, damage, fakeSpeedMultiplier);

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

    private void SpawnStabAnimation(PlayerEntity owner, float directionDegrees)
        => Projectiles.SpawnStabAnimation(owner, directionDegrees);

    private void SpawnStabMask(PlayerEntity owner, float directionDegrees)
        => Projectiles.SpawnStabMask(owner, directionDegrees);

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

    private void SpawnMine(PlayerEntity owner, float x, float y, float velocityX, float velocityY, string? killFeedWeaponSpriteNameOverride = null)
        => Projectiles.SpawnMine(owner, x, y, velocityX, velocityY, killFeedWeaponSpriteNameOverride);

    private void SpawnGrenade(PlayerEntity owner, float x, float y, float velocityX, float velocityY, string? killFeedWeaponSpriteNameOverride = null)
        => Projectiles.SpawnGrenade(owner, x, y, velocityX, velocityY, killFeedWeaponSpriteNameOverride);

    private void SpawnGrenade(
        PlayerEntity owner,
        float x,
        float y,
        float velocityX,
        float velocityY,
        string? killFeedWeaponSpriteNameOverride,
        bool isStrongDrink,
        int fuseTicks,
        float initialSpinSpeed,
        float gravityPerTick = GrenadeProjectileEntity.StrongDrinkGravityPerTick)
        => Projectiles.SpawnGrenade(owner, x, y, velocityX, velocityY, killFeedWeaponSpriteNameOverride, isStrongDrink, fuseTicks, initialSpinSpeed, gravityPerTick);

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

    private void SpawnQueuedLastToDieSniperArrow(PlayerEntity owner, float x, float y, in LastToDieSniperVolleyState volley)
        => Projectiles.SpawnQueuedLastToDieSniperArrow(owner, x, y, volley);

    private void AdvancePendingRocketsForOwner(int ownerId) => Projectiles.AdvancePendingRocketsForOwner(ownerId);

    private void RemoveOwnedProjectiles(int ownerId) => Projectiles.RemoveOwnedProjectiles(ownerId);
    private void RemoveOwnedMines(int ownerId) => Projectiles.RemoveOwnedMines(ownerId);
    private void RemoveOwnedSpyArtifacts(int ownerId) => Projectiles.RemoveOwnedSpyArtifacts(ownerId);
    private int CountOwnedMines(int ownerId) => Projectiles.CountOwnedMines(ownerId);
    private bool ShouldTrackSnapshotProjectileForClientPrediction(int ownerId) => Projectiles.ShouldTrackSnapshotProjectileForClientPrediction(ownerId);
    private int GetSimulationTicksFromSourceTicks(float sourceTicks) => Projectiles.GetSimulationTicksFromSourceTicks(sourceTicks);
    private void RemoveOwnedSentries(int ownerId) => Projectiles.RemoveOwnedSentries(ownerId);
    private void RemoveShotAt(int index) => Projectiles.RemoveShotAt(index);
    private void RemoveBladeAt(int index) => Projectiles.RemoveBladeAt(index);
    private void RemoveNeedleAt(int index) => Projectiles.RemoveNeedleAt(index);
    private void RemoveRevolverShotAt(int index) => Projectiles.RemoveRevolverShotAt(index);
    private void RemoveBubbleAt(int index) => Projectiles.RemoveBubbleAt(index);
    private void RemoveRocketAt(int index) => Projectiles.RemoveRocketAt(index);
    private void RemoveFlameAt(int index) => Projectiles.RemoveFlameAt(index);
    private void RemoveFlareAt(int index) => Projectiles.RemoveFlareAt(index);
    private void RemoveMineAt(int index) => Projectiles.RemoveMineAt(index);
    private void ExplodeGrenade(GrenadeProjectileEntity grenade, PlayerEntity? directHitPlayer = null, SimulationEntity? directHitBuilding = null, int directHitDamageableZoneIndex = -1)
        => Projectiles.ExplodeGrenade(grenade, directHitPlayer, directHitBuilding, directHitDamageableZoneIndex);
    private void ResolveDragonRageProjectileOutcome(FlareProjectileEntity flare, bool hitTarget)
        => Projectiles.ResolveDragonRageProjectileOutcome(flare, hitTarget);
    private void SetProjectileSpawnBlockedDebug(float x, float y, float width, float height, string objectName)
        => Projectiles.SetProjectileSpawnBlockedDebug(x, y, width, height, objectName);

    private static bool CircleIntersectsRectangle(float circleX, float circleY, float radius, float left, float top, float right, float bottom)
        => ProjectileSystem.CircleIntersectsRectangle(circleX, circleY, radius, left, top, right, bottom);
}
