using OpenGarrison.Core.LastToDie;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

/// <summary>
/// Narrow view of the world used by <see cref="GameplayAbilitySystem"/>.
/// </summary>
internal partial interface IGameplayAbilityHost
{
    SimulationConfig Config { get; }
    EntityStore EntityStore { get; }
    ExperimentalGameplaySettings ExperimentalGameplaySettings { get; set; }
    long Frame { get; }
    MovementSystem Movement { get; }
    PresentationEventLog PresentationEvents { get; }
    WeaponFireHandler WeaponHandler { get; }

    void ApplyExperimentalPassivePlayerEffects(PlayerEntity player);
    int ApplyHealingWithFeedback(PlayerEntity target, float healing, string? soundName = null, float soundX = 0f, float soundY = 0f);
    bool ApplyPlayerContinuousDamage(PlayerEntity target, float damage, PlayerEntity? attacker, float spyRevealAlpha = 0f, DamageEventFlags damageFlags = DamageEventFlags.None, bool allowOsmosisHealOwnedSentries = true, bool allowCivvieUmbrellaShield = true, float? civvieUmbrellaThreatSourceX = null, float? civvieUmbrellaThreatSourceY = null, int? civvieUmbrellaDrainTicks = null, bool civvieUmbrellaCriticalBoost = false);
    void AwardMedicUberActivationPoints(PlayerEntity player);
    bool CanTeamDamagePlayer(PlayerTeam attackerTeam, int attackerId, PlayerEntity target);
    void DetonateOwnedMines(int ownerId);
    IEnumerable<PlayerEntity> EnumerateSimulatedPlayers();
    PlayerEntity? FindPlayerById(int playerId);
    void FireMedicKritzHealNeedle(PlayerEntity attacker, float aimWorldX, float aimWorldY, int healPerHit = MedicHealNeedleProjectileEntity.DefaultHealPerHit, int enemyDamagePerHit = MedicHealNeedleProjectileEntity.DefaultEnemyDamagePerHit, float projectileSpeed = MedicHealNeedleProjectileEntity.DefaultProjectileSpeed, float spreadDegrees = MedicHealNeedleProjectileEntity.DefaultSpreadDegrees);
    void FireMedicNeedle(PlayerEntity attacker, float aimWorldX, float aimWorldY);
    int GetExperimentalGhostDashCooldownTicks();
    int GetExperimentalGhostDashDurationTicks();
    float GetExperimentalGhostDashImpulse();
    int GetHeavyGhostDashCooldownTicks();
    int GetHeavyGhostDashDurationTicks();
    float GetHeavyGhostDashImpulse();
    int GetHeavyGhostDashMovementDurationTicks();
    ExperimentalGameplaySettings GetLastToDieGameplaySettings(PlayerEntity? player);
    void HandleEngineerPdaSentryCommand(PlayerEntity player);
    void KillPlayer(PlayerEntity player, bool gibbed = false, PlayerEntity? killer = null, string? weaponSpriteName = null, DeadBodyAnimationKind deadBodyAnimationKind = DeadBodyAnimationKind.Default, string? deathCamMessage = null, SentryEntity? deathCamSentry = null, string? killFeedMessage = null, bool createDeathCam = true, bool spawnRemains = true, bool forceCorpseRemains = false, bool recordKillFeed = true, int assistingPlayerIdOverride = -1, bool completingLastToDieSpyAfterlifeDeath = false);
    void RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId = -1);
    void SpawnBlade(PlayerEntity owner, float x, float y, float velocityX, float velocityY, int hitDamage, int lifetimeTicks = PlayerEntity.QuoteBladeLifetimeTicks);
    void SpawnBubble(PlayerEntity owner, float x, float y, float velocityX, float velocityY);
    void SpawnFlame(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float directHitDamage = FlameProjectileEntity.DirectHitDamage, float burnDamagePerTick = FlameProjectileEntity.BurnDamagePerTick);
    void SpawnFlare(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float damagePerHit = FlareProjectileEntity.DefaultDamagePerHit, string killFeedWeaponSpriteName = "FlareKL", FlareProjectileStyle style = FlareProjectileStyle.Standard, int lifetimeTicks = FlareProjectileEntity.LifetimeTicks);
    void SpawnGrenade(PlayerEntity owner, float x, float y, float velocityX, float velocityY, string? killFeedWeaponSpriteNameOverride = null);
    void SpawnGrenade(PlayerEntity owner, float x, float y, float velocityX, float velocityY, string? killFeedWeaponSpriteNameOverride, bool isStrongDrink, int fuseTicks, float initialSpinSpeed, float gravityPerTick = GrenadeProjectileEntity.StrongDrinkGravityPerTick);
    void SpawnMine(PlayerEntity owner, float x, float y, float velocityX, float velocityY, string? killFeedWeaponSpriteNameOverride = null);
    void SpawnNeedle(PlayerEntity owner, float x, float y, float velocityX, float velocityY, int damagePerHit = NeedleProjectileEntity.DamagePerHit, string killFeedWeaponSpriteName = "NeedleKL");
    void SpawnRevolverShot(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float damagePerHit = RevolverProjectileEntity.DamagePerHit, string? killFeedWeaponSpriteNameOverride = null, LastToDieSpyRevolverProfile? lastToDieProfile = null, bool forceCritical = false, bool appliesLuckyStrikeStun = false, float playerKnockbackImpulse = 0f, float playerKnockbackAirborneVerticalScale = 1f, float playerKnockbackGroundedVerticalScale = 1f);
    void SpawnRocket(PlayerEntity owner, float x, float y, float speed, float directionRadians, RocketCombatDefinition? rocketCombat = null, float directHitHealAmount = 0f, bool explodeImmediately = false, bool canGrantExperimentalInstantReloadOnHit = true, float knockbackScale = 1f, bool canIgniteTargets = false, bool enableExperimentalStingerTracking = false, bool enableExperimentalCaveatTracking = false, float experimentalVisualScale = 1f, int experimentalTrackingLockTicksRemaining = 0, bool isBallistic = false, float ballisticGravityPerTick = 0f, bool suppressSmokeTrail = false, string? killFeedWeaponSpriteNameOverride = null);
    void SpawnShot(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float damagePerHit = ShotProjectileEntity.DamagePerHit, bool forceGibOnKill = false, string? killFeedWeaponSpriteNameOverride = null, int? sourceSentryId = null, bool applyExperimentalEngineerSentryPerkEffects = false, float playerKnockbackScale = 1f, float? playerSlowMovementMultiplier = null, int playerSlowRefreshTicks = 0, float? playerKnockbackImpulse = null, float playerKnockbackAirborneVerticalScale = 1f, float playerKnockbackGroundedVerticalScale = 1f, bool isBoomstickPellet = false);
    void TriggerCivvieUmbrellaAirblast(PlayerEntity player, float aimWorldX, float aimWorldY);
    void TriggerPyroAirblast(PlayerEntity player, float aimWorldX, float aimWorldY);
    void TriggerPyroSelfAirblast(PlayerEntity player, float aimWorldX, float aimWorldY);
    bool TryBuildJumpPad(PlayerEntity player, bool ignoreMetalCost = false);
    bool TryDestroyJumpPad(PlayerEntity player);
    bool TryHandleExperimentalEngineerDestinyPunctuatorBlast(PlayerEntity player, PlayerInputSnapshot input);
    bool TryHandleExperimentalRageActivation(PlayerEntity player);
    bool TryHandleExperimentalSoldierCivilDefenseTurret(PlayerEntity player);
    bool TryHandleExperimentalSoldierStingerDetonation(PlayerEntity player);
    bool TryHandleExperimentalSoldierThundergunner(PlayerEntity player, PlayerInputSnapshot input);
    bool UpdateMedicKritzBeam(PlayerEntity medic, float aimWorldX, float aimWorldY, float maxRange, float damagePerSecond, float chargePerTick);
}
