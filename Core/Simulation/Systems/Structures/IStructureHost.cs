using OpenGarrison.Core.LastToDie;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

/// <summary>
/// Narrow view of the world used by <see cref="StructureSystem"/>.
/// </summary>
internal interface IStructureHost
{
    WorldBounds Bounds { get; }
    bool ClientPredictionMode { get; }
    SimulationConfig Config { get; }
    EntityStore EntityStore { get; }
    LastToDieState LastToDieState { get; }
    SimpleLevel Level { get; }
    PlayerEntity LocalPlayer { get; }
    MatchState MatchState { get; }
    IReadOnlyList<MineProjectileEntity> Mines { get; }
    WorldObjectStore WorldObjects { get; }

    int AllocateEntityId();
    void ApplyExperimentalEngineerSentryPassiveEffects(SentryEntity sentry, PlayerEntity owner);
    void ApplyExplosionImpulse(PlayerEntity player, float originX, float originY, float impulse);
    bool ApplyPlayerDamage(PlayerEntity target, int damage, PlayerEntity? attacker, float spyRevealAlpha = 0f, DamageEventFlags damageFlags = DamageEventFlags.None, bool allowOsmosisHealOwnedSentries = true, bool allowCivvieUmbrellaShield = true, float? civvieUmbrellaThreatSourceX = null, float? civvieUmbrellaThreatSourceY = null, int? civvieUmbrellaDrainTicks = null, bool civvieUmbrellaCriticalBoost = false);
    void AwardSentryDestructionPoints(SentryEntity sentry, PlayerEntity? attacker);
    IEnumerable<PlayerEntity> EnumerateSimulatedPlayers();
    PlayerEntity? FindPlayerById(int playerId);
    bool FireExperimentalSentry(SentryEntity sentry, PlayerEntity owner, SentryTarget target, int reloadTicks, int idleResetTicks);
    float GetDamageableZoneHealth(int roomObjectIndex);
    int GetExperimentalMaxOwnedSentries(PlayerEntity player);
    int GetExperimentalOwnedSentryCount(int ownerPlayerId);
    int GetExperimentalSentryIdleResetTicks();
    int GetExperimentalSentryMaxHealth(PlayerEntity owner);
    int GetExperimentalSentryReloadTicks(PlayerEntity owner, SentryEntity sentry);
    float GetExperimentalSentryTargetRange(PlayerEntity owner);
    float GetExplosionImpulseMagnitude(PlayerEntity player, float originX, float originY, float knockbackPerTick, float distanceFactor, bool useMineVectorProfile);
    bool HasDirectLineOfSight(float originX, float originY, float targetX, float targetY, PlayerTeam targetTeam);
    bool HasObstacleLineOfSight(float originX, float originY, float targetX, float targetY);
    bool HasSentryLineOfSight(SentryEntity sentry, PlayerEntity target);
    bool IsExperimentalEngineerPriorityTarget(PlayerEntity owner, PlayerEntity candidate);
    void KillPlayer(PlayerEntity player, bool gibbed = false, PlayerEntity? killer = null, string? weaponSpriteName = null, DeadBodyAnimationKind deadBodyAnimationKind = DeadBodyAnimationKind.Default, string? deathCamMessage = null, SentryEntity? deathCamSentry = null, string? killFeedMessage = null, bool createDeathCam = true, bool spawnRemains = true, bool forceCorpseRemains = false, bool recordKillFeed = true, int assistingPlayerIdOverride = -1, bool completingLastToDieSpyAfterlifeDeath = false);
    void RegisterCombatTrace(float originX, float originY, float directionX, float directionY, float distance, bool hitCharacter, PlayerTeam team = PlayerTeam.Red, bool isSniperTracer = false, bool isCritical = false);
    void RegisterHealingEvent(PlayerEntity target, int amount);
    void RegisterImpactEffect(float x, float y, float directionDegrees);
    void RegisterVisualEffect(string effectName, float x, float y, float directionDegrees = 0f, int count = 1, bool normalizeDirection = true);
    void RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId = -1);
    bool TryDestroyNearestEnemyDefensibleProjectile(PlayerTeam team, float x, float y, float radius, out float targetX, out float targetY);
    bool TryGetNetworkPlayer(byte slot, out PlayerEntity player);
}
