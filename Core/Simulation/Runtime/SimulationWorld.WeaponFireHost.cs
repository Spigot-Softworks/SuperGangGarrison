using OpenGarrison.Core.LastToDie;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IWeaponFireHost
{
    SimpleLevel IWeaponFireHost.Level => Level;
    WorldBounds IWeaponFireHost.Bounds => Bounds;
    SimulationConfig IWeaponFireHost.Config => Config;
    ExperimentalGameplaySettings IWeaponFireHost.ExperimentalGameplaySettings => ExperimentalGameplaySettings;
    bool IWeaponFireHost.RandomSpreadEnabled => RandomSpreadEnabled;
    Random IWeaponFireHost.Random => Randoms.Gameplay;
    WorldObjectStore IWeaponFireHost.WorldObjects => WorldObjects;
    IReadOnlyList<FlareProjectileEntity> IWeaponFireHost.Flares => Flares;
    IReadOnlyList<MineProjectileEntity> IWeaponFireHost.Mines => Mines;
    IReadOnlyList<RocketProjectileEntity> IWeaponFireHost.Rockets => Rockets;

    IEnumerable<PlayerEntity> IWeaponFireHost.EnumerateSimulatedPlayers() => EnumerateSimulatedPlayers();
    float IWeaponFireHost.ApplyExperimentalProjectileSpeedMultiplier(PlayerEntity attacker, float launchSpeed) => ExperimentalRules.ApplyExperimentalProjectileSpeedMultiplier(attacker, launchSpeed);
    (float VelocityX, float VelocityY) IWeaponFireHost.ApplyExperimentalProjectileSpeedMultiplier(PlayerEntity attacker, float launchVelocityX, float launchVelocityY) => ExperimentalRules.ApplyExperimentalProjectileSpeedMultiplier(attacker, launchVelocityX, launchVelocityY);
    RocketCombatDefinition IWeaponFireHost.ApplyExperimentalSoldierRocketCombat(PlayerEntity attacker, RocketCombatDefinition? rocketCombat) => ExperimentalRules.ApplyExperimentalSoldierRocketCombat(attacker, rocketCombat);
    float IWeaponFireHost.ApplyExperimentalSoldierRocketLaunchSpeed(PlayerEntity attacker, float launchSpeed) => ExperimentalRules.ApplyExperimentalSoldierRocketLaunchSpeed(attacker, launchSpeed);
    bool IWeaponFireHost.ApplyPlayerDamageWithContext(PlayerEntity target, int damage, PlayerEntity? attacker, float spyRevealAlpha, DamageEventFlags damageFlags, bool allowOsmosisHealOwnedSentries, bool allowCivvieUmbrellaShield, float? civvieUmbrellaThreatSourceX, float? civvieUmbrellaThreatSourceY, int? civvieUmbrellaDrainTicks, bool civvieUmbrellaCriticalBoost, bool civvieUmbrellaUseLiveAttackerCriticalBoost, PlayerDamageTraits additionalTraits, bool? attackerWasGrounded, bool? targetWasGrounded, int sourceEntityId, ulong attackId, int attackerPlayerIdOverride) => ApplyPlayerDamageWithContext(target, damage, attacker, spyRevealAlpha, damageFlags, allowOsmosisHealOwnedSentries, allowCivvieUmbrellaShield, civvieUmbrellaThreatSourceX, civvieUmbrellaThreatSourceY, civvieUmbrellaDrainTicks, civvieUmbrellaCriticalBoost, civvieUmbrellaUseLiveAttackerCriticalBoost, additionalTraits, attackerWasGrounded, targetWasGrounded, sourceEntityId, attackId, attackerPlayerIdOverride);
    bool IWeaponFireHost.ApplySentryDamage(SentryEntity target, int damage, PlayerEntity? attacker) => ApplySentryDamage(target, damage, attacker);
    bool IWeaponFireHost.CanTeamDamagePlayer(PlayerTeam attackerTeam, int attackerId, PlayerEntity target) => DamageRules.CanTeamDamagePlayer(attackerTeam, attackerId, target);
    int IWeaponFireHost.CountOwnedMines(int ownerId) => CountOwnedMines(ownerId);
    void IWeaponFireHost.DestroySentry(SentryEntity sentry, PlayerEntity? attacker) => Structures.DestroySentry(sentry, attacker);
    void IWeaponFireHost.ExplodeOldestMine(int ownerId, bool triggerNearbyMines) => ExplosionRules.ExplodeOldestMine(ownerId, triggerNearbyMines);
    void IWeaponFireHost.GetCachedPlayerPresentationHitBounds(PlayerEntity player, out float left, out float top, out float right, out float bottom) => PresentationBounds.GetCachedPlayerPresentationHitBounds(player, out left, out top, out right, out bottom);
    int IWeaponFireHost.GetDeterministicSpreadShotIndex(int attackerId) => GetDeterministicSpreadShotIndex(attackerId);
    ExperimentalGameplaySettings IWeaponFireHost.GetLastToDieGameplaySettings(PlayerEntity? player) => LastToDieRules.GetLastToDieGameplaySettings(player);
    int IWeaponFireHost.GetSimulationTicksFromSourceTicks(float sourceTicks) => GetSimulationTicksFromSourceTicks(sourceTicks);
    bool IWeaponFireHost.IsExperimentalPracticePowerOwner(PlayerEntity? player) => ExperimentalRules.IsExperimentalPracticePowerOwner(player);
    bool IWeaponFireHost.IsFlameSpawnBlocked(float originX, float originY, float spawnX, float spawnY, PlayerTeam team) => IsFlameSpawnBlocked(originX, originY, spawnX, spawnY, team);
    bool IWeaponFireHost.IsProjectileSpawnBlocked(float originX, float originY, float targetX, float targetY, PlayerTeam shotTeam) => IsProjectileSpawnBlocked(originX, originY, targetX, targetY, shotTeam);
    void IWeaponFireHost.KillPlayer(PlayerEntity player, bool gibbed, PlayerEntity? killer, string? weaponSpriteName, DeadBodyAnimationKind deadBodyAnimationKind, string? deathCamMessage, SentryEntity? deathCamSentry, string? killFeedMessage, bool createDeathCam, bool spawnRemains, bool forceCorpseRemains, bool recordKillFeed, int assistingPlayerIdOverride, bool completingLastToDieSpyAfterlifeDeath) => PlayerDeaths.KillPlayer(player, gibbed, killer, weaponSpriteName, deadBodyAnimationKind, deathCamMessage, deathCamSentry, killFeedMessage, createDeathCam, spawnRemains, forceCorpseRemains, recordKillFeed, assistingPlayerIdOverride, completingLastToDieSpyAfterlifeDeath);
    void IWeaponFireHost.QueueExperimentalSoldierFinalRocketBurst(PlayerEntity owner, float x, float y, float speed, float directionRadians, RocketCombatDefinition? rocketCombat, float directHitHealAmount, bool canGrantExperimentalInstantReloadOnHit, float knockbackScale, bool canIgniteTargets, bool enableStingerTracking, string? killFeedWeaponSpriteNameOverride) => ExperimentalRules.QueueExperimentalSoldierFinalRocketBurst(owner, x, y, speed, directionRadians, rocketCombat, directHitHealAmount, canGrantExperimentalInstantReloadOnHit, knockbackScale, canIgniteTargets, enableStingerTracking, killFeedWeaponSpriteNameOverride);
    void IWeaponFireHost.RegisterBloodEffect(float x, float y, float directionDegrees, int count) => WorldEffects.RegisterBloodEffect(x, y, directionDegrees, count);
    void IWeaponFireHost.RegisterCombatTrace(float originX, float originY, float directionX, float directionY, float distance, bool hitCharacter, PlayerTeam team, bool isSniperTracer, bool isCritical) => WorldEffects.RegisterCombatTrace(originX, originY, directionX, directionY, distance, hitCharacter, team, isSniperTracer, isCritical);
    void IWeaponFireHost.RegisterImpactEffect(float x, float y, float directionDegrees) => WorldEffects.RegisterImpactEffect(x, y, directionDegrees);
    void IWeaponFireHost.RegisterSoundEvent(PlayerEntity attacker, string soundName) => WorldEffects.RegisterSoundEvent(attacker, soundName);
    void IWeaponFireHost.ResolveDragonRageProjectileOutcome(FlareProjectileEntity flare, bool hitTarget) => ResolveDragonRageProjectileOutcome(flare, hitTarget);
    OrderedRifleHitResult IWeaponFireHost.ResolveOrderedRifleHits(PlayerEntity attacker, float originX, float originY, float directionX, float directionY, float maxDistance, RifleTracePolicy policy) => ResolveOrderedRifleHits(attacker, originX, originY, directionX, directionY, maxDistance, policy);
    PlayerDamageResolution IWeaponFireHost.ResolvePlayerDamage(PlayerEntity target, in PlayerDamageRequest request) => ResolvePlayerDamage(target, request);
    RifleHitResult IWeaponFireHost.ResolveRifleHit(PlayerEntity attacker, float directionX, float directionY, float maxDistance) => ResolveRifleHit(attacker, directionX, directionY, maxDistance);
    RifleHitResult IWeaponFireHost.ResolveRifleHit(PlayerEntity attacker, float originX, float originY, float directionX, float directionY, float maxDistance) => ResolveRifleHit(attacker, originX, originY, directionX, directionY, maxDistance);
    void IWeaponFireHost.SpawnArrow(PlayerEntity owner, float x, float y, float velocityX, float velocityY, int damage, float fakeSpeedMultiplier) => SpawnArrow(owner, x, y, velocityX, velocityY, damage, fakeSpeedMultiplier);
    void IWeaponFireHost.SpawnBlade(PlayerEntity owner, float x, float y, float velocityX, float velocityY, int hitDamage, int lifetimeTicks) => SpawnBlade(owner, x, y, velocityX, velocityY, hitDamage, lifetimeTicks);
    void IWeaponFireHost.SpawnBubble(PlayerEntity owner, float x, float y, float velocityX, float velocityY) => SpawnBubble(owner, x, y, velocityX, velocityY);
    void IWeaponFireHost.SpawnFlame(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float directHitDamage, float burnDamagePerTick) => SpawnFlame(owner, x, y, velocityX, velocityY, directHitDamage, burnDamagePerTick);
    void IWeaponFireHost.SpawnFlare(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float damagePerHit, string killFeedWeaponSpriteName, FlareProjectileStyle style, int lifetimeTicks) => SpawnFlare(owner, x, y, velocityX, velocityY, damagePerHit, killFeedWeaponSpriteName, style, lifetimeTicks);
    void IWeaponFireHost.SpawnGrenade(PlayerEntity owner, float x, float y, float velocityX, float velocityY, string? killFeedWeaponSpriteNameOverride) => SpawnGrenade(owner, x, y, velocityX, velocityY, killFeedWeaponSpriteNameOverride);
    void IWeaponFireHost.SpawnGrenade(PlayerEntity owner, float x, float y, float velocityX, float velocityY, string? killFeedWeaponSpriteNameOverride, bool isStrongDrink, int fuseTicks, float initialSpinSpeed, float gravityPerTick) => SpawnGrenade(owner, x, y, velocityX, velocityY, killFeedWeaponSpriteNameOverride, isStrongDrink, fuseTicks, initialSpinSpeed, gravityPerTick);
    void IWeaponFireHost.SpawnMedicHealNeedle(PlayerEntity owner, float x, float y, float velocityX, float velocityY, int healPerHit, int enemyDamagePerHit) => SpawnMedicHealNeedle(owner, x, y, velocityX, velocityY, healPerHit, enemyDamagePerHit);
    void IWeaponFireHost.SpawnMine(PlayerEntity owner, float x, float y, float velocityX, float velocityY, string? killFeedWeaponSpriteNameOverride) => SpawnMine(owner, x, y, velocityX, velocityY, killFeedWeaponSpriteNameOverride);
    void IWeaponFireHost.SpawnNail(PlayerEntity owner, float x, float y, float velocityX, float velocityY) => SpawnNail(owner, x, y, velocityX, velocityY);
    void IWeaponFireHost.SpawnNeedle(PlayerEntity owner, float x, float y, float velocityX, float velocityY, int damagePerHit, string killFeedWeaponSpriteName) => SpawnNeedle(owner, x, y, velocityX, velocityY, damagePerHit, killFeedWeaponSpriteName);
    void IWeaponFireHost.SpawnQueuedLastToDieSniperArrow(PlayerEntity owner, float x, float y, in LastToDieSniperVolleyState volley) => SpawnQueuedLastToDieSniperArrow(owner, x, y, volley);
    void IWeaponFireHost.SpawnRevolverShot(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float damagePerHit, string? killFeedWeaponSpriteNameOverride, LastToDieSpyRevolverProfile? lastToDieProfile, bool forceCritical, bool appliesLuckyStrikeStun, float playerKnockbackImpulse, float playerKnockbackAirborneVerticalScale, float playerKnockbackGroundedVerticalScale) => SpawnRevolverShot(owner, x, y, velocityX, velocityY, damagePerHit, killFeedWeaponSpriteNameOverride, lastToDieProfile, forceCritical, appliesLuckyStrikeStun, playerKnockbackImpulse, playerKnockbackAirborneVerticalScale, playerKnockbackGroundedVerticalScale);
    void IWeaponFireHost.SpawnRocket(PlayerEntity owner, float x, float y, float speed, float directionRadians, RocketCombatDefinition? rocketCombat, float directHitHealAmount, bool explodeImmediately, bool canGrantExperimentalInstantReloadOnHit, float knockbackScale, bool canIgniteTargets, bool enableExperimentalStingerTracking, bool enableExperimentalCaveatTracking, float experimentalVisualScale, int experimentalTrackingLockTicksRemaining, bool isBallistic, float ballisticGravityPerTick, bool suppressSmokeTrail, string? killFeedWeaponSpriteNameOverride) => SpawnRocket(owner, x, y, speed, directionRadians, rocketCombat, directHitHealAmount, explodeImmediately, canGrantExperimentalInstantReloadOnHit, knockbackScale, canIgniteTargets, enableExperimentalStingerTracking, enableExperimentalCaveatTracking, experimentalVisualScale, experimentalTrackingLockTicksRemaining, isBallistic, ballisticGravityPerTick, suppressSmokeTrail, killFeedWeaponSpriteNameOverride);
    void IWeaponFireHost.SpawnShot(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float damagePerHit, bool forceGibOnKill, string? killFeedWeaponSpriteNameOverride, int? sourceSentryId, bool applyExperimentalEngineerSentryPerkEffects, float playerKnockbackScale, float? playerSlowMovementMultiplier, int playerSlowRefreshTicks, float? playerKnockbackImpulse, float playerKnockbackAirborneVerticalScale, float playerKnockbackGroundedVerticalScale, bool isBoomstickPellet) => SpawnShot(owner, x, y, velocityX, velocityY, damagePerHit, forceGibOnKill, killFeedWeaponSpriteNameOverride, sourceSentryId, applyExperimentalEngineerSentryPerkEffects, playerKnockbackScale, playerSlowMovementMultiplier, playerSlowRefreshTicks, playerKnockbackImpulse, playerKnockbackAirborneVerticalScale, playerKnockbackGroundedVerticalScale, isBoomstickPellet);
    bool IWeaponFireHost.TryApplyLastToDieSniperGuardian(PlayerEntity sniper, PlayerEntity target) => LastToDieRules.TryApplyLastToDieSniperGuardian(sniper, target);
    void IWeaponFireHost.TryApplyLastToDieSniperStatusPayload(PlayerEntity source, PlayerEntity target, bool appliesTranqDarts, float poisonTipDamagePerSecond) => LastToDieRules.TryApplyLastToDieSniperStatusPayload(source, target, appliesTranqDarts, poisonTipDamagePerSecond);
    bool IWeaponFireHost.TryDamageGenerator(PlayerTeam targetTeam, float damage, PlayerEntity? attacker) => ObjectiveRules.TryDamageGenerator(targetTeam, damage, attacker);
    bool IWeaponFireHost.TryExplodeLastToDieSniperRifleImpact(PlayerEntity owner, float x, float y, bool isCritical, float criticalDamageMultiplier) => LastToDieRules.TryExplodeLastToDieSniperRifleImpact(owner, x, y, isCritical, criticalDamageMultiplier);
    bool IWeaponFireHost.TryFindWhippingCordTerrainContact(PlayerEntity player, float aimWorldX, float aimWorldY, out float contactX, out float contactY) => TryFindWhippingCordTerrainContact(player, aimWorldX, aimWorldY, out contactX, out contactY);
    bool IWeaponFireHost.TryFindWhippingCordTerrainContact(PlayerEntity player, GameplayItemDefinition item, float aimWorldX, float aimWorldY, out float contactX, out float contactY) => TryFindWhippingCordTerrainContact(player, item, aimWorldX, aimWorldY, out contactX, out contactY);
    bool IWeaponFireHost.TryLatchWhippingCordToTerrain(PlayerEntity player, float aimWorldX, float aimWorldY) => TryLatchWhippingCordToTerrain(player, aimWorldX, aimWorldY);
    bool IWeaponFireHost.TryRollLastToDieSpyDeadlyCritical(PlayerEntity attacker) => LastToDieRules.TryRollLastToDieSpyDeadlyCritical(attacker);
    bool IWeaponFireHost.TryShootFriendlyStrongDrink(PlayerTeam shooterTeam, PlayerClass shooterClass, int shooterOwnerId, float originX, float originY, float directionX, float directionY, float maxDistance, int fireParticleCount) => TryShootFriendlyStrongDrink(shooterTeam, shooterClass, shooterOwnerId, originX, originY, directionX, directionY, maxDistance, fireParticleCount);
    void IWeaponFireHost.TrySpawnExperimentalDemoknightDecapitationRemains(PlayerEntity victim, float launchDirectionX, float launchDirectionY) => PlayerRemains.TrySpawnExperimentalDemoknightDecapitationRemains(victim, launchDirectionX, launchDirectionY);

    GameplayPrimaryWeaponResult IWeaponFireHost.ExecutePrimaryWeaponExecutor(
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
        string killFeedWeaponSpriteName)
    {
        return executor.Handle(new GameplayPrimaryWeaponContext
        {
            World = this,
            Player = player,
            Weapon = weapon,
            ItemId = itemId,
            BehaviorId = behaviorId,
            WeaponClassId = weaponClassId,
            SourceX = sourceX,
            SourceY = sourceY,
            AimWorldX = aimWorldX,
            AimWorldY = aimWorldY,
            DirectionX = directionX,
            DirectionY = directionY,
            DirectionRadians = directionRadians,
            KillFeedWeaponSpriteName = killFeedWeaponSpriteName,
        });
    }
}