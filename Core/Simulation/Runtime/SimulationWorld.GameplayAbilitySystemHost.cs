using OpenGarrison.Core.LastToDie;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IGameplayAbilityHost
{
    SimulationConfig IGameplayAbilityHost.Config => Config;
    EntityStore IGameplayAbilityHost.EntityStore => EntityStore;
    ExperimentalGameplaySettings IGameplayAbilityHost.ExperimentalGameplaySettings { get => ExperimentalGameplaySettings; set => ExperimentalGameplaySettings = value; }
    long IGameplayAbilityHost.Frame => Frame;
    MovementSystem IGameplayAbilityHost.Movement => Movement;
    PresentationEventLog IGameplayAbilityHost.PresentationEvents => PresentationEvents;
    WeaponFireHandler IGameplayAbilityHost.WeaponHandler => WeaponHandler;

    void IGameplayAbilityHost.ApplyExperimentalPassivePlayerEffects(PlayerEntity player) => ExperimentalRules.ApplyExperimentalPassivePlayerEffects(player);
    int IGameplayAbilityHost.ApplyHealingWithFeedback(PlayerEntity target, float healing, string? soundName, float soundX, float soundY) => DamageRules.ApplyHealingWithFeedback(target, healing, soundName, soundX, soundY);
    bool IGameplayAbilityHost.ApplyPlayerContinuousDamage(PlayerEntity target, float damage, PlayerEntity? attacker, float spyRevealAlpha, DamageEventFlags damageFlags, bool allowOsmosisHealOwnedSentries, bool allowCivvieUmbrellaShield, float? civvieUmbrellaThreatSourceX, float? civvieUmbrellaThreatSourceY, int? civvieUmbrellaDrainTicks, bool civvieUmbrellaCriticalBoost) => ApplyPlayerContinuousDamage(target, damage, attacker, spyRevealAlpha, damageFlags, allowOsmosisHealOwnedSentries, allowCivvieUmbrellaShield, civvieUmbrellaThreatSourceX, civvieUmbrellaThreatSourceY, civvieUmbrellaDrainTicks, civvieUmbrellaCriticalBoost);
    void IGameplayAbilityHost.AwardMedicUberActivationPoints(PlayerEntity player) => Scorekeeping.AwardMedicUberActivationPoints(player);
    bool IGameplayAbilityHost.CanTeamDamagePlayer(PlayerTeam attackerTeam, int attackerId, PlayerEntity target) => DamageRules.CanTeamDamagePlayer(attackerTeam, attackerId, target);
    void IGameplayAbilityHost.DetonateOwnedMines(int ownerId) => ExplosionRules.DetonateOwnedMines(ownerId);
    IEnumerable<PlayerEntity> IGameplayAbilityHost.EnumerateSimulatedPlayers() => EnumerateSimulatedPlayers();
    PlayerEntity? IGameplayAbilityHost.FindPlayerById(int playerId) => FindPlayerById(playerId);
    void IGameplayAbilityHost.FireMedicKritzHealNeedle(PlayerEntity attacker, float aimWorldX, float aimWorldY, int healPerHit, int enemyDamagePerHit, float projectileSpeed, float spreadDegrees) => FireMedicKritzHealNeedle(attacker, aimWorldX, aimWorldY, healPerHit, enemyDamagePerHit, projectileSpeed, spreadDegrees);
    void IGameplayAbilityHost.FireMedicNeedle(PlayerEntity attacker, float aimWorldX, float aimWorldY) => FireMedicNeedle(attacker, aimWorldX, aimWorldY);
    int IGameplayAbilityHost.GetExperimentalGhostDashCooldownTicks() => ExperimentalRules.GetExperimentalGhostDashCooldownTicks();
    int IGameplayAbilityHost.GetExperimentalGhostDashDurationTicks() => ExperimentalRules.GetExperimentalGhostDashDurationTicks();
    float IGameplayAbilityHost.GetExperimentalGhostDashImpulse() => ExperimentalRulesSystem.GetExperimentalGhostDashImpulse();
    int IGameplayAbilityHost.GetHeavyGhostDashCooldownTicks() => ExperimentalRules.GetHeavyGhostDashCooldownTicks();
    int IGameplayAbilityHost.GetHeavyGhostDashDurationTicks() => ExperimentalRules.GetHeavyGhostDashDurationTicks();
    float IGameplayAbilityHost.GetHeavyGhostDashImpulse() => ExperimentalRulesSystem.GetHeavyGhostDashImpulse();
    int IGameplayAbilityHost.GetHeavyGhostDashMovementDurationTicks() => ExperimentalRules.GetHeavyGhostDashMovementDurationTicks();
    ExperimentalGameplaySettings IGameplayAbilityHost.GetLastToDieGameplaySettings(PlayerEntity? player) => LastToDieRules.GetLastToDieGameplaySettings(player);
    void IGameplayAbilityHost.HandleEngineerPdaSentryCommand(PlayerEntity player) => PlayerInput.HandleEngineerPdaSentryCommand(player);
    void IGameplayAbilityHost.KillPlayer(PlayerEntity player, bool gibbed, PlayerEntity? killer, string? weaponSpriteName, DeadBodyAnimationKind deadBodyAnimationKind, string? deathCamMessage, SentryEntity? deathCamSentry, string? killFeedMessage, bool createDeathCam, bool spawnRemains, bool forceCorpseRemains, bool recordKillFeed, int assistingPlayerIdOverride, bool completingLastToDieSpyAfterlifeDeath) => PlayerDeaths.KillPlayer(player, gibbed, killer, weaponSpriteName, deadBodyAnimationKind, deathCamMessage, deathCamSentry, killFeedMessage, createDeathCam, spawnRemains, forceCorpseRemains, recordKillFeed, assistingPlayerIdOverride, completingLastToDieSpyAfterlifeDeath);
    void IGameplayAbilityHost.RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId) => WorldEffects.RegisterWorldSoundEvent(soundName, x, y, sourcePlayerId);
    void IGameplayAbilityHost.SpawnBlade(PlayerEntity owner, float x, float y, float velocityX, float velocityY, int hitDamage, int lifetimeTicks) => SpawnBlade(owner, x, y, velocityX, velocityY, hitDamage, lifetimeTicks);
    void IGameplayAbilityHost.SpawnBubble(PlayerEntity owner, float x, float y, float velocityX, float velocityY) => SpawnBubble(owner, x, y, velocityX, velocityY);
    void IGameplayAbilityHost.SpawnFlame(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float directHitDamage, float burnDamagePerTick) => SpawnFlame(owner, x, y, velocityX, velocityY, directHitDamage, burnDamagePerTick);
    void IGameplayAbilityHost.SpawnFlare(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float damagePerHit, string killFeedWeaponSpriteName, FlareProjectileStyle style, int lifetimeTicks) => SpawnFlare(owner, x, y, velocityX, velocityY, damagePerHit, killFeedWeaponSpriteName, style, lifetimeTicks);
    void IGameplayAbilityHost.SpawnGrenade(PlayerEntity owner, float x, float y, float velocityX, float velocityY, string? killFeedWeaponSpriteNameOverride) => SpawnGrenade(owner, x, y, velocityX, velocityY, killFeedWeaponSpriteNameOverride);
    void IGameplayAbilityHost.SpawnGrenade(PlayerEntity owner, float x, float y, float velocityX, float velocityY, string? killFeedWeaponSpriteNameOverride, bool isStrongDrink, int fuseTicks, float initialSpinSpeed, float gravityPerTick) => SpawnGrenade(owner, x, y, velocityX, velocityY, killFeedWeaponSpriteNameOverride, isStrongDrink, fuseTicks, initialSpinSpeed, gravityPerTick);
    void IGameplayAbilityHost.SpawnMine(PlayerEntity owner, float x, float y, float velocityX, float velocityY, string? killFeedWeaponSpriteNameOverride) => SpawnMine(owner, x, y, velocityX, velocityY, killFeedWeaponSpriteNameOverride);
    void IGameplayAbilityHost.SpawnNeedle(PlayerEntity owner, float x, float y, float velocityX, float velocityY, int damagePerHit, string killFeedWeaponSpriteName) => SpawnNeedle(owner, x, y, velocityX, velocityY, damagePerHit, killFeedWeaponSpriteName);
    void IGameplayAbilityHost.SpawnRevolverShot(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float damagePerHit, string? killFeedWeaponSpriteNameOverride, LastToDieSpyRevolverProfile? lastToDieProfile, bool forceCritical, bool appliesLuckyStrikeStun, float playerKnockbackImpulse, float playerKnockbackAirborneVerticalScale, float playerKnockbackGroundedVerticalScale) => SpawnRevolverShot(owner, x, y, velocityX, velocityY, damagePerHit, killFeedWeaponSpriteNameOverride, lastToDieProfile, forceCritical, appliesLuckyStrikeStun, playerKnockbackImpulse, playerKnockbackAirborneVerticalScale, playerKnockbackGroundedVerticalScale);
    void IGameplayAbilityHost.SpawnRocket(PlayerEntity owner, float x, float y, float speed, float directionRadians, RocketCombatDefinition? rocketCombat, float directHitHealAmount, bool explodeImmediately, bool canGrantExperimentalInstantReloadOnHit, float knockbackScale, bool canIgniteTargets, bool enableExperimentalStingerTracking, bool enableExperimentalCaveatTracking, float experimentalVisualScale, int experimentalTrackingLockTicksRemaining, bool isBallistic, float ballisticGravityPerTick, bool suppressSmokeTrail, string? killFeedWeaponSpriteNameOverride) => SpawnRocket(owner, x, y, speed, directionRadians, rocketCombat, directHitHealAmount, explodeImmediately, canGrantExperimentalInstantReloadOnHit, knockbackScale, canIgniteTargets, enableExperimentalStingerTracking, enableExperimentalCaveatTracking, experimentalVisualScale, experimentalTrackingLockTicksRemaining, isBallistic, ballisticGravityPerTick, suppressSmokeTrail, killFeedWeaponSpriteNameOverride);
    void IGameplayAbilityHost.SpawnShot(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float damagePerHit, bool forceGibOnKill, string? killFeedWeaponSpriteNameOverride, int? sourceSentryId, bool applyExperimentalEngineerSentryPerkEffects, float playerKnockbackScale, float? playerSlowMovementMultiplier, int playerSlowRefreshTicks, float? playerKnockbackImpulse, float playerKnockbackAirborneVerticalScale, float playerKnockbackGroundedVerticalScale, bool isBoomstickPellet) => SpawnShot(owner, x, y, velocityX, velocityY, damagePerHit, forceGibOnKill, killFeedWeaponSpriteNameOverride, sourceSentryId, applyExperimentalEngineerSentryPerkEffects, playerKnockbackScale, playerSlowMovementMultiplier, playerSlowRefreshTicks, playerKnockbackImpulse, playerKnockbackAirborneVerticalScale, playerKnockbackGroundedVerticalScale, isBoomstickPellet);
    void IGameplayAbilityHost.TriggerCivvieUmbrellaAirblast(PlayerEntity player, float aimWorldX, float aimWorldY) => AirblastRules.TriggerCivvieUmbrellaAirblast(player, aimWorldX, aimWorldY);
    void IGameplayAbilityHost.TriggerPyroAirblast(PlayerEntity player, float aimWorldX, float aimWorldY) => AirblastRules.TriggerPyroAirblast(player, aimWorldX, aimWorldY);
    void IGameplayAbilityHost.TriggerPyroSelfAirblast(PlayerEntity player, float aimWorldX, float aimWorldY) => AirblastRules.TriggerPyroSelfAirblast(player, aimWorldX, aimWorldY);
    bool IGameplayAbilityHost.TryBuildJumpPad(PlayerEntity player, bool ignoreMetalCost) => Structures.TryBuildJumpPad(player, ignoreMetalCost);
    bool IGameplayAbilityHost.TryDestroyJumpPad(PlayerEntity player) => Structures.TryDestroyJumpPad(player);
    bool IGameplayAbilityHost.TryHandleExperimentalEngineerDestinyPunctuatorBlast(PlayerEntity player, PlayerInputSnapshot input) => PlayerInput.TryHandleExperimentalEngineerDestinyPunctuatorBlast(player, input);
    bool IGameplayAbilityHost.TryHandleExperimentalRageActivation(PlayerEntity player) => ExperimentalRules.TryHandleExperimentalRageActivation(player);
    bool IGameplayAbilityHost.TryHandleExperimentalSoldierCivilDefenseTurret(PlayerEntity player) => PlayerInput.TryHandleExperimentalSoldierCivilDefenseTurret(player);
    bool IGameplayAbilityHost.TryHandleExperimentalSoldierStingerDetonation(PlayerEntity player) => PlayerInput.TryHandleExperimentalSoldierStingerDetonation(player);
    bool IGameplayAbilityHost.TryHandleExperimentalSoldierThundergunner(PlayerEntity player, PlayerInputSnapshot input) => PlayerInput.TryHandleExperimentalSoldierThundergunner(player, input);
    bool IGameplayAbilityHost.UpdateMedicKritzBeam(PlayerEntity medic, float aimWorldX, float aimWorldY, float maxRange, float damagePerSecond, float chargePerTick) => SupportRules.UpdateMedicKritzBeam(medic, aimWorldX, aimWorldY, maxRange, damagePerSecond, chargePerTick);
}
