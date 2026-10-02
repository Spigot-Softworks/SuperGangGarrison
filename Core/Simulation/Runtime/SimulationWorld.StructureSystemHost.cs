using OpenGarrison.Core.LastToDie;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IStructureHost
{
    WorldBounds IStructureHost.Bounds => Bounds;
    bool IStructureHost.ClientPredictionMode => ClientPredictionMode;
    SimulationConfig IStructureHost.Config => Config;
    EntityStore IStructureHost.EntityStore => EntityStore;
    LastToDieState IStructureHost.LastToDieState => LastToDieState;
    SimpleLevel IStructureHost.Level => Level;
    PlayerEntity IStructureHost.LocalPlayer => LocalPlayer;
    MatchState IStructureHost.MatchState => MatchState;
    IReadOnlyList<MineProjectileEntity> IStructureHost.Mines => Mines;
    WorldObjectStore IStructureHost.WorldObjects => WorldObjects;

    int IStructureHost.AllocateEntityId() => AllocateEntityId();
    void IStructureHost.ApplyExperimentalEngineerSentryPassiveEffects(SentryEntity sentry, PlayerEntity owner) => ExperimentalRules.ApplyExperimentalEngineerSentryPassiveEffects(sentry, owner);
    void IStructureHost.ApplyExplosionImpulse(PlayerEntity player, float originX, float originY, float impulse) => ApplyExplosionImpulse(player, originX, originY, impulse);
    bool IStructureHost.ApplyPlayerDamage(PlayerEntity target, int damage, PlayerEntity? attacker, float spyRevealAlpha, DamageEventFlags damageFlags, bool allowOsmosisHealOwnedSentries, bool allowCivvieUmbrellaShield, float? civvieUmbrellaThreatSourceX, float? civvieUmbrellaThreatSourceY, int? civvieUmbrellaDrainTicks, bool civvieUmbrellaCriticalBoost) => ApplyPlayerDamage(target, damage, attacker, spyRevealAlpha, damageFlags, allowOsmosisHealOwnedSentries, allowCivvieUmbrellaShield, civvieUmbrellaThreatSourceX, civvieUmbrellaThreatSourceY, civvieUmbrellaDrainTicks, civvieUmbrellaCriticalBoost);
    void IStructureHost.AwardSentryDestructionPoints(SentryEntity sentry, PlayerEntity? attacker) => Scorekeeping.AwardSentryDestructionPoints(sentry, attacker);
    IEnumerable<PlayerEntity> IStructureHost.EnumerateSimulatedPlayers() => EnumerateSimulatedPlayers();
    PlayerEntity? IStructureHost.FindPlayerById(int playerId) => FindPlayerById(playerId);
    bool IStructureHost.FireExperimentalSentry(SentryEntity sentry, PlayerEntity owner, SentryTarget target, int reloadTicks, int idleResetTicks) => ExperimentalRules.FireExperimentalSentry(sentry, owner, target, reloadTicks, idleResetTicks);
    float IStructureHost.GetDamageableZoneHealth(int roomObjectIndex) => MapLogic.GetDamageableZoneHealth(roomObjectIndex);
    int IStructureHost.GetExperimentalMaxOwnedSentries(PlayerEntity player) => ExperimentalRules.GetExperimentalMaxOwnedSentries(player);
    int IStructureHost.GetExperimentalOwnedSentryCount(int ownerPlayerId) => ExperimentalRules.GetExperimentalOwnedSentryCount(ownerPlayerId);
    int IStructureHost.GetExperimentalSentryIdleResetTicks() => ExperimentalRules.GetExperimentalSentryIdleResetTicks();
    int IStructureHost.GetExperimentalSentryMaxHealth(PlayerEntity owner) => ExperimentalRules.GetExperimentalSentryMaxHealth(owner);
    int IStructureHost.GetExperimentalSentryReloadTicks(PlayerEntity owner, SentryEntity sentry) => ExperimentalRules.GetExperimentalSentryReloadTicks(owner, sentry);
    float IStructureHost.GetExperimentalSentryTargetRange(PlayerEntity owner) => ExperimentalRules.GetExperimentalSentryTargetRange(owner);
    float IStructureHost.GetExplosionImpulseMagnitude(PlayerEntity player, float originX, float originY, float knockbackPerTick, float distanceFactor, bool useMineVectorProfile) => GetExplosionImpulseMagnitude(player, originX, originY, knockbackPerTick, distanceFactor, useMineVectorProfile);
    bool IStructureHost.HasDirectLineOfSight(float originX, float originY, float targetX, float targetY, PlayerTeam targetTeam) => HasDirectLineOfSight(originX, originY, targetX, targetY, targetTeam);
    bool IStructureHost.HasObstacleLineOfSight(float originX, float originY, float targetX, float targetY) => HasObstacleLineOfSight(originX, originY, targetX, targetY);
    bool IStructureHost.HasSentryLineOfSight(SentryEntity sentry, PlayerEntity target) => HasSentryLineOfSight(sentry, target);
    bool IStructureHost.IsExperimentalEngineerPriorityTarget(PlayerEntity owner, PlayerEntity candidate) => ExperimentalRules.IsExperimentalEngineerPriorityTarget(owner, candidate);
    void IStructureHost.KillPlayer(PlayerEntity player, bool gibbed, PlayerEntity? killer, string? weaponSpriteName, DeadBodyAnimationKind deadBodyAnimationKind, string? deathCamMessage, SentryEntity? deathCamSentry, string? killFeedMessage, bool createDeathCam, bool spawnRemains, bool forceCorpseRemains, bool recordKillFeed, int assistingPlayerIdOverride, bool completingLastToDieSpyAfterlifeDeath) => PlayerDeaths.KillPlayer(player, gibbed, killer, weaponSpriteName, deadBodyAnimationKind, deathCamMessage, deathCamSentry, killFeedMessage, createDeathCam, spawnRemains, forceCorpseRemains, recordKillFeed, assistingPlayerIdOverride, completingLastToDieSpyAfterlifeDeath);
    void IStructureHost.RegisterCombatTrace(float originX, float originY, float directionX, float directionY, float distance, bool hitCharacter, PlayerTeam team, bool isSniperTracer, bool isCritical) => WorldEffects.RegisterCombatTrace(originX, originY, directionX, directionY, distance, hitCharacter, team, isSniperTracer, isCritical);
    void IStructureHost.RegisterHealingEvent(PlayerEntity target, int amount) => DamageRules.RegisterHealingEvent(target, amount);
    void IStructureHost.RegisterImpactEffect(float x, float y, float directionDegrees) => WorldEffects.RegisterImpactEffect(x, y, directionDegrees);
    void IStructureHost.RegisterVisualEffect(string effectName, float x, float y, float directionDegrees, int count, bool normalizeDirection) => WorldEffects.RegisterVisualEffect(effectName, x, y, directionDegrees, count, normalizeDirection);
    void IStructureHost.RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId) => WorldEffects.RegisterWorldSoundEvent(soundName, x, y, sourcePlayerId);
    bool IStructureHost.TryDestroyNearestEnemyDefensibleProjectile(PlayerTeam team, float x, float y, float radius, out float targetX, out float targetY) => AirblastRules.TryDestroyNearestEnemyDefensibleProjectile(team, x, y, radius, out targetX, out targetY);
    bool IStructureHost.TryGetNetworkPlayer(byte slot, out PlayerEntity player) => NetworkPlayerRules.TryGetNetworkPlayer(slot, out player);
}
