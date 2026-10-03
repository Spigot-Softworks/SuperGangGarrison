using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

/// <summary>
/// Narrow view of the world used by <see cref="GameplayAbilitySystem"/>.
/// </summary>
internal partial interface IGameplayAbilityHost
{
    AirblastRulesSystem AirblastRules { get; }
    SimulationConfig Config { get; }
    DamageRulesSystem DamageRules { get; }
    EntityStore EntityStore { get; }
    ExperimentalGameplaySettings ExperimentalGameplaySettings { get; set; }
    ExperimentalRulesSystem ExperimentalRules { get; }
    ExplosionRulesSystem ExplosionRules { get; }
    long Frame { get; }
    LastToDieRulesSystem LastToDieRules { get; }
    MovementSystem Movement { get; }
    PlayerDeathSystem PlayerDeaths { get; }
    PlayerInputSystem PlayerInput { get; }
    PresentationEventLog PresentationEvents { get; }
    ProjectileSystem Projectiles { get; }
    ScorekeepingSystem Scorekeeping { get; }
    StructureSystem Structures { get; }
    WeaponFireHandler WeaponHandler { get; }
    WorldEffectsSystem WorldEffects { get; }

    bool ApplyPlayerContinuousDamage(PlayerEntity target, float damage, PlayerEntity? attacker, float spyRevealAlpha = 0f, DamageEventFlags damageFlags = DamageEventFlags.None, bool allowOsmosisHealOwnedSentries = true, bool allowCivvieUmbrellaShield = true, float? civvieUmbrellaThreatSourceX = null, float? civvieUmbrellaThreatSourceY = null, int? civvieUmbrellaDrainTicks = null, bool civvieUmbrellaCriticalBoost = false);
    IEnumerable<PlayerEntity> EnumerateSimulatedPlayers();
    PlayerEntity? FindPlayerById(int playerId);
    void FireMedicKritzHealNeedle(PlayerEntity attacker, float aimWorldX, float aimWorldY, int healPerHit = MedicHealNeedleProjectileEntity.DefaultHealPerHit, int enemyDamagePerHit = MedicHealNeedleProjectileEntity.DefaultEnemyDamagePerHit, float projectileSpeed = MedicHealNeedleProjectileEntity.DefaultProjectileSpeed, float spreadDegrees = MedicHealNeedleProjectileEntity.DefaultSpreadDegrees);
    float GetExperimentalGhostDashImpulse();
    float GetHeavyGhostDashImpulse();
    void SpawnFlame(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float directHitDamage = FlameProjectileEntity.DirectHitDamage, float burnDamagePerTick = FlameProjectileEntity.BurnDamagePerTick);
    void SpawnFlare(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float damagePerHit = FlareProjectileEntity.DefaultDamagePerHit, string killFeedWeaponSpriteName = "FlareKL", FlareProjectileStyle style = FlareProjectileStyle.Standard, int lifetimeTicks = FlareProjectileEntity.LifetimeTicks);
    void SpawnNeedle(PlayerEntity owner, float x, float y, float velocityX, float velocityY, int damagePerHit = NeedleProjectileEntity.DamagePerHit, string killFeedWeaponSpriteName = "NeedleKL");
    void SpawnRevolverShot(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float damagePerHit = RevolverProjectileEntity.DamagePerHit, string? killFeedWeaponSpriteNameOverride = null, LastToDieSpyRevolverProfile? lastToDieProfile = null, bool forceCritical = false, bool appliesLuckyStrikeStun = false, float playerKnockbackImpulse = 0f, float playerKnockbackAirborneVerticalScale = 1f, float playerKnockbackGroundedVerticalScale = 1f);
    void SpawnRocket(PlayerEntity owner, float x, float y, float speed, float directionRadians, RocketCombatDefinition? rocketCombat = null, float directHitHealAmount = 0f, bool explodeImmediately = false, bool canGrantExperimentalInstantReloadOnHit = true, float knockbackScale = 1f, bool canIgniteTargets = false, bool enableExperimentalStingerTracking = false, bool enableExperimentalCaveatTracking = false, float experimentalVisualScale = 1f, int experimentalTrackingLockTicksRemaining = 0, bool isBallistic = false, float ballisticGravityPerTick = 0f, bool suppressSmokeTrail = false, string? killFeedWeaponSpriteNameOverride = null);
    void SpawnShot(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float damagePerHit = ShotProjectileEntity.DamagePerHit, bool forceGibOnKill = false, string? killFeedWeaponSpriteNameOverride = null, int? sourceSentryId = null, bool applyExperimentalEngineerSentryPerkEffects = false, float playerKnockbackScale = 1f, float? playerSlowMovementMultiplier = null, int playerSlowRefreshTicks = 0, float? playerKnockbackImpulse = null, float playerKnockbackAirborneVerticalScale = 1f, float playerKnockbackGroundedVerticalScale = 1f, bool isBoomstickPellet = false);
    bool UpdateMedicKritzBeam(PlayerEntity medic, float aimWorldX, float aimWorldY, float maxRange, float damagePerSecond, float chargePerTick);
}
