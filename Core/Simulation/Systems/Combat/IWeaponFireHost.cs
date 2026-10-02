using OpenGarrison.Core.LastToDie;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

/// <summary>
/// Narrow view of the world used by WeaponFireHandler to spawn projectiles, resolve hits, and apply damage.
/// </summary>
internal interface IWeaponFireHost
{
    SimpleLevel Level { get; }
    WorldBounds Bounds { get; }
    SimulationConfig Config { get; }
    ExperimentalGameplaySettings ExperimentalGameplaySettings { get; }
    bool RandomSpreadEnabled { get; }
    Random Random { get; }
    WorldObjectStore WorldObjects { get; }
    IReadOnlyList<FlareProjectileEntity> Flares { get; }
    IReadOnlyList<MineProjectileEntity> Mines { get; }
    IReadOnlyList<RocketProjectileEntity> Rockets { get; }

    IEnumerable<PlayerEntity> EnumerateSimulatedPlayers();
    float ApplyExperimentalProjectileSpeedMultiplier(PlayerEntity attacker, float launchSpeed);
    (float VelocityX, float VelocityY) ApplyExperimentalProjectileSpeedMultiplier(PlayerEntity attacker, float launchVelocityX, float launchVelocityY);
    RocketCombatDefinition ApplyExperimentalSoldierRocketCombat(PlayerEntity attacker, RocketCombatDefinition? rocketCombat);
    float ApplyExperimentalSoldierRocketLaunchSpeed(PlayerEntity attacker, float launchSpeed);
    bool ApplyPlayerDamageWithContext(PlayerEntity target, int damage, PlayerEntity? attacker, float spyRevealAlpha = 0f, DamageEventFlags damageFlags = DamageEventFlags.None, bool allowOsmosisHealOwnedSentries = true, bool allowCivvieUmbrellaShield = true, float? civvieUmbrellaThreatSourceX = null, float? civvieUmbrellaThreatSourceY = null, int? civvieUmbrellaDrainTicks = null, bool civvieUmbrellaCriticalBoost = false, bool civvieUmbrellaUseLiveAttackerCriticalBoost = true, PlayerDamageTraits additionalTraits = PlayerDamageTraits.None, bool? attackerWasGrounded = null, bool? targetWasGrounded = null, int sourceEntityId = 0, ulong attackId = 0, int attackerPlayerIdOverride = -1);
    bool ApplySentryDamage(SentryEntity target, int damage, PlayerEntity? attacker);
    bool CanTeamDamagePlayer(PlayerTeam attackerTeam, int attackerId, PlayerEntity target);
    int CountOwnedMines(int ownerId);
    void DestroySentry(SentryEntity sentry, PlayerEntity? attacker = null);
    void ExplodeOldestMine(int ownerId, bool triggerNearbyMines = true);
    void GetCachedPlayerPresentationHitBounds(PlayerEntity player, out float left, out float top, out float right, out float bottom);
    int GetDeterministicSpreadShotIndex(int attackerId);
    ExperimentalGameplaySettings GetLastToDieGameplaySettings(PlayerEntity? player);
    int GetSimulationTicksFromSourceTicks(float sourceTicks);
    bool IsExperimentalPracticePowerOwner(PlayerEntity? player);
    bool IsFlameSpawnBlocked(float originX, float originY, float spawnX, float spawnY, PlayerTeam team);
    bool IsProjectileSpawnBlocked(float originX, float originY, float targetX, float targetY, PlayerTeam shotTeam);
    void KillPlayer(PlayerEntity player, bool gibbed = false, PlayerEntity? killer = null, string? weaponSpriteName = null, DeadBodyAnimationKind deadBodyAnimationKind = DeadBodyAnimationKind.Default, string? deathCamMessage = null, SentryEntity? deathCamSentry = null, string? killFeedMessage = null, bool createDeathCam = true, bool spawnRemains = true, bool forceCorpseRemains = false, bool recordKillFeed = true, int assistingPlayerIdOverride = -1, bool completingLastToDieSpyAfterlifeDeath = false);
    void QueueExperimentalSoldierFinalRocketBurst(PlayerEntity owner, float x, float y, float speed, float directionRadians, RocketCombatDefinition? rocketCombat, float directHitHealAmount, bool canGrantExperimentalInstantReloadOnHit, float knockbackScale, bool canIgniteTargets, bool enableStingerTracking, string? killFeedWeaponSpriteNameOverride);
    void RegisterBloodEffect(float x, float y, float directionDegrees, int count = 1);
    void RegisterCombatTrace(float originX, float originY, float directionX, float directionY, float distance, bool hitCharacter, PlayerTeam team = PlayerTeam.Red, bool isSniperTracer = false, bool isCritical = false);
    void RegisterImpactEffect(float x, float y, float directionDegrees);
    void RegisterSoundEvent(PlayerEntity attacker, string soundName);
    void ResolveDragonRageProjectileOutcome(FlareProjectileEntity flare, bool hitTarget);
    OrderedRifleHitResult ResolveOrderedRifleHits(PlayerEntity attacker, float originX, float originY, float directionX, float directionY, float maxDistance, RifleTracePolicy policy);
    PlayerDamageResolution ResolvePlayerDamage(PlayerEntity target, in PlayerDamageRequest request);
    RifleHitResult ResolveRifleHit(PlayerEntity attacker, float directionX, float directionY, float maxDistance);
    RifleHitResult ResolveRifleHit(PlayerEntity attacker, float originX, float originY, float directionX, float directionY, float maxDistance);
    void SpawnArrow(PlayerEntity owner, float x, float y, float velocityX, float velocityY, int damage, float fakeSpeedMultiplier);
    void SpawnBlade(PlayerEntity owner, float x, float y, float velocityX, float velocityY, int hitDamage, int lifetimeTicks = PlayerEntity.QuoteBladeLifetimeTicks);
    void SpawnBubble(PlayerEntity owner, float x, float y, float velocityX, float velocityY);
    void SpawnFlame(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float directHitDamage = FlameProjectileEntity.DirectHitDamage, float burnDamagePerTick = FlameProjectileEntity.BurnDamagePerTick);
    void SpawnFlare(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float damagePerHit = FlareProjectileEntity.DefaultDamagePerHit, string killFeedWeaponSpriteName = "FlareKL", FlareProjectileStyle style = FlareProjectileStyle.Standard, int lifetimeTicks = FlareProjectileEntity.LifetimeTicks);
    void SpawnGrenade(PlayerEntity owner, float x, float y, float velocityX, float velocityY, string? killFeedWeaponSpriteNameOverride = null);
    void SpawnGrenade(PlayerEntity owner, float x, float y, float velocityX, float velocityY, string? killFeedWeaponSpriteNameOverride, bool isStrongDrink, int fuseTicks, float initialSpinSpeed, float gravityPerTick = GrenadeProjectileEntity.StrongDrinkGravityPerTick);
    void SpawnMedicHealNeedle(PlayerEntity owner, float x, float y, float velocityX, float velocityY, int healPerHit = MedicHealNeedleProjectileEntity.DefaultHealPerHit, int enemyDamagePerHit = MedicHealNeedleProjectileEntity.DefaultEnemyDamagePerHit);
    void SpawnMine(PlayerEntity owner, float x, float y, float velocityX, float velocityY, string? killFeedWeaponSpriteNameOverride = null);
    void SpawnNail(PlayerEntity owner, float x, float y, float velocityX, float velocityY);
    void SpawnNeedle(PlayerEntity owner, float x, float y, float velocityX, float velocityY, int damagePerHit = NeedleProjectileEntity.DamagePerHit, string killFeedWeaponSpriteName = "NeedleKL");
    void SpawnQueuedLastToDieSniperArrow(PlayerEntity owner, float x, float y, in LastToDieSniperVolleyState volley);
    void SpawnRevolverShot(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float damagePerHit = RevolverProjectileEntity.DamagePerHit, string? killFeedWeaponSpriteNameOverride = null, LastToDieSpyRevolverProfile? lastToDieProfile = null, bool forceCritical = false, bool appliesLuckyStrikeStun = false, float playerKnockbackImpulse = 0f, float playerKnockbackAirborneVerticalScale = 1f, float playerKnockbackGroundedVerticalScale = 1f);
    void SpawnRocket(PlayerEntity owner, float x, float y, float speed, float directionRadians, RocketCombatDefinition? rocketCombat = null, float directHitHealAmount = 0f, bool explodeImmediately = false, bool canGrantExperimentalInstantReloadOnHit = true, float knockbackScale = 1f, bool canIgniteTargets = false, bool enableExperimentalStingerTracking = false, bool enableExperimentalCaveatTracking = false, float experimentalVisualScale = 1f, int experimentalTrackingLockTicksRemaining = 0, bool isBallistic = false, float ballisticGravityPerTick = 0f, bool suppressSmokeTrail = false, string? killFeedWeaponSpriteNameOverride = null);
    void SpawnShot(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float damagePerHit = ShotProjectileEntity.DamagePerHit, bool forceGibOnKill = false, string? killFeedWeaponSpriteNameOverride = null, int? sourceSentryId = null, bool applyExperimentalEngineerSentryPerkEffects = false, float playerKnockbackScale = 1f, float? playerSlowMovementMultiplier = null, int playerSlowRefreshTicks = 0, float? playerKnockbackImpulse = null, float playerKnockbackAirborneVerticalScale = 1f, float playerKnockbackGroundedVerticalScale = 1f, bool isBoomstickPellet = false);
    bool TryApplyLastToDieSniperGuardian(PlayerEntity sniper, PlayerEntity target);
    void TryApplyLastToDieSniperStatusPayload(PlayerEntity source, PlayerEntity target, bool appliesTranqDarts, float poisonTipDamagePerSecond);
    bool TryDamageGenerator(PlayerTeam targetTeam, float damage, PlayerEntity? attacker = null);
    bool TryExplodeLastToDieSniperRifleImpact(PlayerEntity owner, float x, float y, bool isCritical, float criticalDamageMultiplier);
    bool TryFindWhippingCordTerrainContact(PlayerEntity player, float aimWorldX, float aimWorldY, out float contactX, out float contactY);
    bool TryFindWhippingCordTerrainContact(PlayerEntity player, GameplayItemDefinition item, float aimWorldX, float aimWorldY, out float contactX, out float contactY);
    bool TryLatchWhippingCordToTerrain(PlayerEntity player, float aimWorldX, float aimWorldY);
    bool TryRollLastToDieSpyDeadlyCritical(PlayerEntity attacker);
    bool TryShootFriendlyStrongDrink(PlayerTeam shooterTeam, PlayerClass shooterClass, int shooterOwnerId, float originX, float originY, float directionX, float directionY, float maxDistance, int fireParticleCount);
    void TrySpawnExperimentalDemoknightDecapitationRemains(PlayerEntity victim, float launchDirectionX, float launchDirectionY);

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