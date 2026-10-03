using OpenGarrison.Core.LastToDie;

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
    CombatSystem IWeaponFireHost.Combat => Combat;
    DamageRulesSystem IWeaponFireHost.DamageRules => DamageRules;
    ExperimentalRulesSystem IWeaponFireHost.ExperimentalRules => ExperimentalRules;
    ExplosionRulesSystem IWeaponFireHost.ExplosionRules => ExplosionRules;
    CombatResolver IWeaponFireHost.GeometryResolver => GeometryResolver;
    LastToDieRulesSystem IWeaponFireHost.LastToDieRules => LastToDieRules;
    MovementSystem IWeaponFireHost.Movement => Movement;
    ObjectiveRulesSystem IWeaponFireHost.ObjectiveRules => ObjectiveRules;
    PlayerDeathSystem IWeaponFireHost.PlayerDeaths => PlayerDeaths;
    PlayerRemainsSystem IWeaponFireHost.PlayerRemains => PlayerRemains;
    PlayerPresentationBoundsSystem IWeaponFireHost.PresentationBounds => PresentationBounds;
    ProjectileSystem IWeaponFireHost.Projectiles => Projectiles;
    StructureSystem IWeaponFireHost.Structures => Structures;
    WorldEffectsSystem IWeaponFireHost.WorldEffects => WorldEffects;

    IEnumerable<PlayerEntity> IWeaponFireHost.EnumerateSimulatedPlayers() => EnumerateSimulatedPlayers();
    bool IWeaponFireHost.ApplyPlayerDamageWithContext(PlayerEntity target, int damage, PlayerEntity? attacker, float spyRevealAlpha, DamageEventFlags damageFlags, bool allowOsmosisHealOwnedSentries, bool allowCivvieUmbrellaShield, float? civvieUmbrellaThreatSourceX, float? civvieUmbrellaThreatSourceY, int? civvieUmbrellaDrainTicks, bool civvieUmbrellaCriticalBoost, bool civvieUmbrellaUseLiveAttackerCriticalBoost, PlayerDamageTraits additionalTraits, bool? attackerWasGrounded, bool? targetWasGrounded, int sourceEntityId, ulong attackId, int attackerPlayerIdOverride) => Combat.ApplyPlayerDamageWithContext(target, damage, attacker, spyRevealAlpha, damageFlags, allowOsmosisHealOwnedSentries, allowCivvieUmbrellaShield, civvieUmbrellaThreatSourceX, civvieUmbrellaThreatSourceY, civvieUmbrellaDrainTicks, civvieUmbrellaCriticalBoost, civvieUmbrellaUseLiveAttackerCriticalBoost, additionalTraits, attackerWasGrounded, targetWasGrounded, sourceEntityId, attackId, attackerPlayerIdOverride);
    int IWeaponFireHost.GetDeterministicSpreadShotIndex(int attackerId) => GetDeterministicSpreadShotIndex(attackerId);
    OrderedRifleHitResult IWeaponFireHost.ResolveOrderedRifleHits(PlayerEntity attacker, float originX, float originY, float directionX, float directionY, float maxDistance, RifleTracePolicy policy) => GeometryResolver.ResolveOrderedRifleHits(attacker, originX, originY, directionX, directionY, maxDistance, policy);
    void IWeaponFireHost.SpawnFlame(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float directHitDamage, float burnDamagePerTick) => Projectiles.SpawnFlame(owner, x, y, velocityX, velocityY, directHitDamage, burnDamagePerTick);
    void IWeaponFireHost.SpawnFlare(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float damagePerHit, string killFeedWeaponSpriteName, FlareProjectileStyle style, int lifetimeTicks) => Projectiles.SpawnFlare(owner, x, y, velocityX, velocityY, damagePerHit, killFeedWeaponSpriteName, style, lifetimeTicks);
    void IWeaponFireHost.SpawnMedicHealNeedle(PlayerEntity owner, float x, float y, float velocityX, float velocityY, int healPerHit, int enemyDamagePerHit) => Projectiles.SpawnMedicHealNeedle(owner, x, y, velocityX, velocityY, healPerHit, enemyDamagePerHit);
    void IWeaponFireHost.SpawnNeedle(PlayerEntity owner, float x, float y, float velocityX, float velocityY, int damagePerHit, string killFeedWeaponSpriteName) => Projectiles.SpawnNeedle(owner, x, y, velocityX, velocityY, damagePerHit, killFeedWeaponSpriteName);
    void IWeaponFireHost.SpawnRevolverShot(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float damagePerHit, string? killFeedWeaponSpriteNameOverride, LastToDieSpyRevolverProfile? lastToDieProfile, bool forceCritical, bool appliesLuckyStrikeStun, float playerKnockbackImpulse, float playerKnockbackAirborneVerticalScale, float playerKnockbackGroundedVerticalScale) => Projectiles.SpawnRevolverShot(owner, x, y, velocityX, velocityY, damagePerHit, killFeedWeaponSpriteNameOverride, lastToDieProfile, forceCritical, appliesLuckyStrikeStun, playerKnockbackImpulse, playerKnockbackAirborneVerticalScale, playerKnockbackGroundedVerticalScale);
    void IWeaponFireHost.SpawnRocket(PlayerEntity owner, float x, float y, float speed, float directionRadians, RocketCombatDefinition? rocketCombat, float directHitHealAmount, bool explodeImmediately, bool canGrantExperimentalInstantReloadOnHit, float knockbackScale, bool canIgniteTargets, bool enableExperimentalStingerTracking, bool enableExperimentalCaveatTracking, float experimentalVisualScale, int experimentalTrackingLockTicksRemaining, bool isBallistic, float ballisticGravityPerTick, bool suppressSmokeTrail, string? killFeedWeaponSpriteNameOverride) => Projectiles.SpawnRocket(owner, x, y, speed, directionRadians, rocketCombat, directHitHealAmount, explodeImmediately, canGrantExperimentalInstantReloadOnHit, knockbackScale, canIgniteTargets, enableExperimentalStingerTracking, enableExperimentalCaveatTracking, experimentalVisualScale, experimentalTrackingLockTicksRemaining, isBallistic, ballisticGravityPerTick, suppressSmokeTrail, killFeedWeaponSpriteNameOverride);
    void IWeaponFireHost.SpawnShot(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float damagePerHit, bool forceGibOnKill, string? killFeedWeaponSpriteNameOverride, int? sourceSentryId, bool applyExperimentalEngineerSentryPerkEffects, float playerKnockbackScale, float? playerSlowMovementMultiplier, int playerSlowRefreshTicks, float? playerKnockbackImpulse, float playerKnockbackAirborneVerticalScale, float playerKnockbackGroundedVerticalScale, bool isBoomstickPellet) => Projectiles.SpawnShot(owner, x, y, velocityX, velocityY, damagePerHit, forceGibOnKill, killFeedWeaponSpriteNameOverride, sourceSentryId, applyExperimentalEngineerSentryPerkEffects, playerKnockbackScale, playerSlowMovementMultiplier, playerSlowRefreshTicks, playerKnockbackImpulse, playerKnockbackAirborneVerticalScale, playerKnockbackGroundedVerticalScale, isBoomstickPellet);
    bool IWeaponFireHost.TryShootFriendlyStrongDrink(PlayerTeam shooterTeam, PlayerClass shooterClass, int shooterOwnerId, float originX, float originY, float directionX, float directionY, float maxDistance, int fireParticleCount) => Projectiles.TryShootFriendlyStrongDrink(shooterTeam, shooterClass, shooterOwnerId, originX, originY, directionX, directionY, maxDistance, fireParticleCount);

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