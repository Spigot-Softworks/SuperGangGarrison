using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

/// <summary>
/// Narrow view of the world used by WeaponFireHandler to spawn projectiles, resolve hits, and apply damage.
/// </summary>
internal interface IWeaponFireHost
{
    WorldBounds Bounds { get; }
    CombatSystem Combat { get; }
    SimulationConfig Config { get; }
    DamageRulesSystem DamageRules { get; }
    ExperimentalGameplaySettings ExperimentalGameplaySettings { get; }
    ExperimentalRulesSystem ExperimentalRules { get; }
    ExplosionRulesSystem ExplosionRules { get; }
    IReadOnlyList<FlareProjectileEntity> Flares { get; }
    CombatResolver GeometryResolver { get; }
    LastToDieRulesSystem LastToDieRules { get; }
    SimpleLevel Level { get; }
    IReadOnlyList<MineProjectileEntity> Mines { get; }
    MovementSystem Movement { get; }
    ObjectiveRulesSystem ObjectiveRules { get; }
    PlayerDeathSystem PlayerDeaths { get; }
    PlayerRemainsSystem PlayerRemains { get; }
    PlayerPresentationBoundsSystem PresentationBounds { get; }
    ProjectileSystem Projectiles { get; }
    Random Random { get; }
    bool RandomSpreadEnabled { get; }
    IReadOnlyList<RocketProjectileEntity> Rockets { get; }
    StructureSystem Structures { get; }
    WorldEffectsSystem WorldEffects { get; }
    WorldObjectStore WorldObjects { get; }

    IEnumerable<PlayerEntity> EnumerateSimulatedPlayers();
    bool ApplyPlayerDamageWithContext(PlayerEntity target, int damage, PlayerEntity? attacker, float spyRevealAlpha = 0f, DamageEventFlags damageFlags = DamageEventFlags.None, bool allowOsmosisHealOwnedSentries = true, bool allowCivvieUmbrellaShield = true, float? civvieUmbrellaThreatSourceX = null, float? civvieUmbrellaThreatSourceY = null, int? civvieUmbrellaDrainTicks = null, bool civvieUmbrellaCriticalBoost = false, bool civvieUmbrellaUseLiveAttackerCriticalBoost = true, PlayerDamageTraits additionalTraits = PlayerDamageTraits.None, bool? attackerWasGrounded = null, bool? targetWasGrounded = null, int sourceEntityId = 0, ulong attackId = 0, int attackerPlayerIdOverride = -1);
    int GetDeterministicSpreadShotIndex(int attackerId);
    OrderedRifleHitResult ResolveOrderedRifleHits(PlayerEntity attacker, float originX, float originY, float directionX, float directionY, float maxDistance, RifleTracePolicy policy);
    void SpawnFlame(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float directHitDamage = FlameProjectileEntity.DirectHitDamage, float burnDamagePerTick = FlameProjectileEntity.BurnDamagePerTick);
    void SpawnFlare(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float damagePerHit = FlareProjectileEntity.DefaultDamagePerHit, string killFeedWeaponSpriteName = "FlareKL", FlareProjectileStyle style = FlareProjectileStyle.Standard, int lifetimeTicks = FlareProjectileEntity.LifetimeTicks);
    void SpawnMedicHealNeedle(PlayerEntity owner, float x, float y, float velocityX, float velocityY, int healPerHit = MedicHealNeedleProjectileEntity.DefaultHealPerHit, int enemyDamagePerHit = MedicHealNeedleProjectileEntity.DefaultEnemyDamagePerHit);
    void SpawnNeedle(PlayerEntity owner, float x, float y, float velocityX, float velocityY, int damagePerHit = NeedleProjectileEntity.DamagePerHit, string killFeedWeaponSpriteName = "NeedleKL");
    void SpawnRevolverShot(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float damagePerHit = RevolverProjectileEntity.DamagePerHit, string? killFeedWeaponSpriteNameOverride = null, LastToDieSpyRevolverProfile? lastToDieProfile = null, bool forceCritical = false, bool appliesLuckyStrikeStun = false, float playerKnockbackImpulse = 0f, float playerKnockbackAirborneVerticalScale = 1f, float playerKnockbackGroundedVerticalScale = 1f);
    void SpawnRocket(PlayerEntity owner, float x, float y, float speed, float directionRadians, RocketCombatDefinition? rocketCombat = null, float directHitHealAmount = 0f, bool explodeImmediately = false, bool canGrantExperimentalInstantReloadOnHit = true, float knockbackScale = 1f, bool canIgniteTargets = false, bool enableExperimentalStingerTracking = false, bool enableExperimentalCaveatTracking = false, float experimentalVisualScale = 1f, int experimentalTrackingLockTicksRemaining = 0, bool isBallistic = false, float ballisticGravityPerTick = 0f, bool suppressSmokeTrail = false, string? killFeedWeaponSpriteNameOverride = null);
    void SpawnShot(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float damagePerHit = ShotProjectileEntity.DamagePerHit, bool forceGibOnKill = false, string? killFeedWeaponSpriteNameOverride = null, int? sourceSentryId = null, bool applyExperimentalEngineerSentryPerkEffects = false, float playerKnockbackScale = 1f, float? playerSlowMovementMultiplier = null, int playerSlowRefreshTicks = 0, float? playerKnockbackImpulse = null, float playerKnockbackAirborneVerticalScale = 1f, float playerKnockbackGroundedVerticalScale = 1f, bool isBoomstickPellet = false);
    bool TryShootFriendlyStrongDrink(PlayerTeam shooterTeam, PlayerClass shooterClass, int shooterOwnerId, float originX, float originY, float directionX, float directionY, float maxDistance, int fireParticleCount);

    // The mod-facing GameplayPrimaryWeaponContext exposes the world itself, so the world builds the
    // context on the handler's behalf instead of handing itself to the system.
    GameplayPrimaryWeaponResult ExecutePrimaryWeaponExecutor(
        IGameplayPrimaryWeaponExecutor executor,
        PlayerEntity player,
        PrimaryWeaponDefinition weapon,
        string itemId,
        string behaviorId,
        PlayerClass weaponClassId,
        float sourceX,
        float sourceY,
        float aimWorldX,
        float aimWorldY,
        float directionX,
        float directionY,
        float directionRadians,
        string killFeedWeaponSpriteName);
}