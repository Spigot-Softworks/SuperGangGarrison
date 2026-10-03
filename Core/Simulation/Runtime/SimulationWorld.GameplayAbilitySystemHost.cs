using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IGameplayAbilityHost
{
    AirblastRulesSystem IGameplayAbilityHost.AirblastRules => AirblastRules;
    bool IGameplayAbilityHost.ApplyPlayerContinuousDamage(PlayerEntity target, float damage, PlayerEntity? attacker, float spyRevealAlpha, DamageEventFlags damageFlags, bool allowOsmosisHealOwnedSentries, bool allowCivvieUmbrellaShield, float? civvieUmbrellaThreatSourceX, float? civvieUmbrellaThreatSourceY, int? civvieUmbrellaDrainTicks, bool civvieUmbrellaCriticalBoost) => ApplyPlayerContinuousDamage(target, damage, attacker, spyRevealAlpha, damageFlags, allowOsmosisHealOwnedSentries, allowCivvieUmbrellaShield, civvieUmbrellaThreatSourceX, civvieUmbrellaThreatSourceY, civvieUmbrellaDrainTicks, civvieUmbrellaCriticalBoost);
    SimulationConfig IGameplayAbilityHost.Config => Config;
    DamageRulesSystem IGameplayAbilityHost.DamageRules => DamageRules;
    EntityStore IGameplayAbilityHost.EntityStore => EntityStore;
    IEnumerable<PlayerEntity> IGameplayAbilityHost.EnumerateSimulatedPlayers() => EnumerateSimulatedPlayers();
    ExperimentalGameplaySettings IGameplayAbilityHost.ExperimentalGameplaySettings { get => ExperimentalGameplaySettings; set => ExperimentalGameplaySettings = value; }
    ExperimentalRulesSystem IGameplayAbilityHost.ExperimentalRules => ExperimentalRules;
    ExplosionRulesSystem IGameplayAbilityHost.ExplosionRules => ExplosionRules;
    PlayerEntity? IGameplayAbilityHost.FindPlayerById(int playerId) => FindPlayerById(playerId);
    void IGameplayAbilityHost.FireMedicKritzHealNeedle(PlayerEntity attacker, float aimWorldX, float aimWorldY, int healPerHit, int enemyDamagePerHit, float projectileSpeed, float spreadDegrees) => FireMedicKritzHealNeedle(attacker, aimWorldX, aimWorldY, healPerHit, enemyDamagePerHit, projectileSpeed, spreadDegrees);
    long IGameplayAbilityHost.Frame => Frame;
    float IGameplayAbilityHost.GetExperimentalGhostDashImpulse() => ExperimentalRulesSystem.GetExperimentalGhostDashImpulse();
    float IGameplayAbilityHost.GetHeavyGhostDashImpulse() => ExperimentalRulesSystem.GetHeavyGhostDashImpulse();
    LastToDieRulesSystem IGameplayAbilityHost.LastToDieRules => LastToDieRules;
    MovementSystem IGameplayAbilityHost.Movement => Movement;
    PlayerDeathSystem IGameplayAbilityHost.PlayerDeaths => PlayerDeaths;
    PlayerInputSystem IGameplayAbilityHost.PlayerInput => PlayerInput;
    PresentationEventLog IGameplayAbilityHost.PresentationEvents => PresentationEvents;
    ProjectileSystem IGameplayAbilityHost.Projectiles => Projectiles;
    ScorekeepingSystem IGameplayAbilityHost.Scorekeeping => Scorekeeping;
    void IGameplayAbilityHost.SpawnFlame(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float directHitDamage, float burnDamagePerTick) => SpawnFlame(owner, x, y, velocityX, velocityY, directHitDamage, burnDamagePerTick);
    void IGameplayAbilityHost.SpawnFlare(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float damagePerHit, string killFeedWeaponSpriteName, FlareProjectileStyle style, int lifetimeTicks) => SpawnFlare(owner, x, y, velocityX, velocityY, damagePerHit, killFeedWeaponSpriteName, style, lifetimeTicks);
    void IGameplayAbilityHost.SpawnNeedle(PlayerEntity owner, float x, float y, float velocityX, float velocityY, int damagePerHit, string killFeedWeaponSpriteName) => SpawnNeedle(owner, x, y, velocityX, velocityY, damagePerHit, killFeedWeaponSpriteName);
    void IGameplayAbilityHost.SpawnRevolverShot(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float damagePerHit, string? killFeedWeaponSpriteNameOverride, LastToDieSpyRevolverProfile? lastToDieProfile, bool forceCritical, bool appliesLuckyStrikeStun, float playerKnockbackImpulse, float playerKnockbackAirborneVerticalScale, float playerKnockbackGroundedVerticalScale) => SpawnRevolverShot(owner, x, y, velocityX, velocityY, damagePerHit, killFeedWeaponSpriteNameOverride, lastToDieProfile, forceCritical, appliesLuckyStrikeStun, playerKnockbackImpulse, playerKnockbackAirborneVerticalScale, playerKnockbackGroundedVerticalScale);
    void IGameplayAbilityHost.SpawnRocket(PlayerEntity owner, float x, float y, float speed, float directionRadians, RocketCombatDefinition? rocketCombat, float directHitHealAmount, bool explodeImmediately, bool canGrantExperimentalInstantReloadOnHit, float knockbackScale, bool canIgniteTargets, bool enableExperimentalStingerTracking, bool enableExperimentalCaveatTracking, float experimentalVisualScale, int experimentalTrackingLockTicksRemaining, bool isBallistic, float ballisticGravityPerTick, bool suppressSmokeTrail, string? killFeedWeaponSpriteNameOverride) => SpawnRocket(owner, x, y, speed, directionRadians, rocketCombat, directHitHealAmount, explodeImmediately, canGrantExperimentalInstantReloadOnHit, knockbackScale, canIgniteTargets, enableExperimentalStingerTracking, enableExperimentalCaveatTracking, experimentalVisualScale, experimentalTrackingLockTicksRemaining, isBallistic, ballisticGravityPerTick, suppressSmokeTrail, killFeedWeaponSpriteNameOverride);
    void IGameplayAbilityHost.SpawnShot(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float damagePerHit, bool forceGibOnKill, string? killFeedWeaponSpriteNameOverride, int? sourceSentryId, bool applyExperimentalEngineerSentryPerkEffects, float playerKnockbackScale, float? playerSlowMovementMultiplier, int playerSlowRefreshTicks, float? playerKnockbackImpulse, float playerKnockbackAirborneVerticalScale, float playerKnockbackGroundedVerticalScale, bool isBoomstickPellet) => SpawnShot(owner, x, y, velocityX, velocityY, damagePerHit, forceGibOnKill, killFeedWeaponSpriteNameOverride, sourceSentryId, applyExperimentalEngineerSentryPerkEffects, playerKnockbackScale, playerSlowMovementMultiplier, playerSlowRefreshTicks, playerKnockbackImpulse, playerKnockbackAirborneVerticalScale, playerKnockbackGroundedVerticalScale, isBoomstickPellet);
    StructureSystem IGameplayAbilityHost.Structures => Structures;
    bool IGameplayAbilityHost.UpdateMedicKritzBeam(PlayerEntity medic, float aimWorldX, float aimWorldY, float maxRange, float damagePerSecond, float chargePerTick) => SupportRules.UpdateMedicKritzBeam(medic, aimWorldX, aimWorldY, maxRange, damagePerSecond, chargePerTick);
    WeaponFireHandler IGameplayAbilityHost.WeaponHandler => WeaponHandler;
    WorldEffectsSystem IGameplayAbilityHost.WorldEffects => WorldEffects;
}
