using OpenGarrison.Core.LastToDie;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IObjectiveRulesHost
{
    int IObjectiveRulesHost.BlueCaps { get => BlueCaps; set => BlueCaps = value; }
    IReadOnlyList<BubbleProjectileEntity> IObjectiveRulesHost.Bubbles => Bubbles;
    bool IObjectiveRulesHost.CompetitiveObjectivesLocked => CompetitiveObjectivesLocked;
    SimulationConfig IObjectiveRulesHost.Config => Config;
    int IObjectiveRulesHost.ControlPointSetupDurationTicks => ControlPointSetupDurationTicks;
    long IObjectiveRulesHost.Frame => Frame;
    SimpleLevel IObjectiveRulesHost.Level => Level;
    PlayerEntity IObjectiveRulesHost.LocalPlayer => LocalPlayer;
    PlayerTeam IObjectiveRulesHost.LocalPlayerTeam => LocalPlayerTeam;
    MatchRules IObjectiveRulesHost.MatchRules { get => MatchRules; set => MatchRules = value; }
    MatchState IObjectiveRulesHost.MatchState { get => MatchState; set => MatchState = value; }
    IReadOnlyList<MineProjectileEntity> IObjectiveRulesHost.Mines => Mines;
    ObjectiveStateStore IObjectiveRulesHost.Objectives => Objectives;
    int IObjectiveRulesHost.RedCaps { get => RedCaps; set => RedCaps = value; }
    IReadOnlyList<RocketProjectileEntity> IObjectiveRulesHost.Rockets => Rockets;
    Func<WorldScoreDecisionRequest, WorldDecisionResult>? IObjectiveRulesHost.ScoreDecisionInterceptor => ScoreDecisionInterceptor;
    WorldObjectStore IObjectiveRulesHost.WorldObjects => WorldObjects;

    void IObjectiveRulesHost.ApplyDeadBodyExplosionImpulse(float originX, float originY, float blastRadius, float maxImpulse, float? falloffRadius) => ApplyDeadBodyExplosionImpulse(originX, originY, blastRadius, maxImpulse, falloffRadius);
    void IObjectiveRulesHost.ApplyExplosionImpulse(PlayerEntity player, float originX, float originY, float impulse) => ApplyExplosionImpulse(player, originX, originY, impulse);
    bool IObjectiveRulesHost.ApplyGeneratorDamage(GeneratorState target, float damage, PlayerEntity? attacker) => ApplyGeneratorDamage(target, damage, attacker);
    void IObjectiveRulesHost.ApplyPlayerGibExplosionImpulse(float originX, float originY, float blastRadius, float maxImpulse, float? falloffRadius) => ApplyPlayerGibExplosionImpulse(originX, originY, blastRadius, maxImpulse, falloffRadius);
    void IObjectiveRulesHost.AwardObjectiveCapturePoints(PlayerEntity player) => AwardObjectiveCapturePoints(player);
    void IObjectiveRulesHost.DestroySentry(SentryEntity sentry, PlayerEntity? attacker) => DestroySentry(sentry, attacker);
    IEnumerable<PlayerEntity> IObjectiveRulesHost.EnumerateSimulatedPlayers() => EnumerateSimulatedPlayers();
    void IObjectiveRulesHost.ExplodeMine(MineProjectileEntity mine, bool triggerNearbyMines) => ExplodeMine(mine, triggerNearbyMines);
    void IObjectiveRulesHost.ExplodeRocket(RocketProjectileEntity rocket, PlayerEntity? directHitPlayer, SentryEntity? directHitSentry, GeneratorState? directHitGenerator, int directHitDamageableZoneRoomObjectIndex) => ExplodeRocket(rocket, directHitPlayer, directHitSentry, directHitGenerator, directHitDamageableZoneRoomObjectIndex);
    MineProjectileEntity? IObjectiveRulesHost.FindMineById(int mineId) => FindMineById(mineId);
    PlayerTeam IObjectiveRulesHost.GetOpposingTeam(PlayerTeam team) => GetOpposingTeam(team);
    void IObjectiveRulesHost.InitializeControlPointsForLevel(bool evaluateLogicGraph) => InitializeControlPointsForLevel(evaluateLogicGraph);
    void IObjectiveRulesHost.KillPlayer(PlayerEntity player, bool gibbed, PlayerEntity? killer, string? weaponSpriteName, DeadBodyAnimationKind deadBodyAnimationKind, string? deathCamMessage, SentryEntity? deathCamSentry, string? killFeedMessage, bool createDeathCam, bool spawnRemains, bool forceCorpseRemains, bool recordKillFeed, int assistingPlayerIdOverride, bool completingLastToDieSpyAfterlifeDeath) => KillPlayer(player, gibbed, killer, weaponSpriteName, deadBodyAnimationKind, deathCamMessage, deathCamSentry, killFeedMessage, createDeathCam, spawnRemains, forceCorpseRemains, recordKillFeed, assistingPlayerIdOverride, completingLastToDieSpyAfterlifeDeath);
    bool IObjectiveRulesHost.NearlyEqual(float left, float right) => NearlyEqual(left, right);
    void IObjectiveRulesHost.RecordGeneratorDestroyedObjectiveLog(PlayerTeam team) => RecordGeneratorDestroyedObjectiveLog(team);
    void IObjectiveRulesHost.RecordIntelCapturedObjectiveLog(PlayerEntity player) => RecordIntelCapturedObjectiveLog(player);
    void IObjectiveRulesHost.RecordIntelDroppedObjectiveLog(PlayerEntity player) => RecordIntelDroppedObjectiveLog(player);
    void IObjectiveRulesHost.RecordIntelPickedUpObjectiveLog(PlayerEntity player) => RecordIntelPickedUpObjectiveLog(player);
    void IObjectiveRulesHost.RegisterVisualEffect(string effectName, float x, float y, float directionDegrees, int count, bool normalizeDirection) => RegisterVisualEffect(effectName, x, y, directionDegrees, count, normalizeDirection);
    void IObjectiveRulesHost.RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId) => RegisterWorldSoundEvent(soundName, x, y, sourcePlayerId);
    void IObjectiveRulesHost.RemoveBubbleAt(int index) => RemoveBubbleAt(index);
    bool IObjectiveRulesHost.ShouldCancelPickup(WorldPickupKind kind, PlayerEntity player, int pickupEntityId, string pickupValue, float x, float y) => ShouldCancelPickup(kind, player, pickupEntityId, pickupValue, x, y);
    bool IObjectiveRulesHost.ShouldEndMatchOnRedTeamIntelCapture() => ShouldEndMatchOnRedTeamIntelCapture();
    void IObjectiveRulesHost.SyncMapLogicRuntimeFromAuthoritativeControlPoints(bool newRound) => SyncMapLogicRuntimeFromAuthoritativeControlPoints(newRound);
    bool IObjectiveRulesHost.TryAwardTeamScore(PlayerTeam team, int delta, string reason, int actorPlayerId) => TryAwardTeamScore(team, delta, reason, actorPlayerId);
    bool IObjectiveRulesHost.TryEndRound(PlayerTeam? winnerTeam, string reason) => TryEndRound(winnerTeam, reason);
    void IObjectiveRulesHost.UpdateControlPointSetupGates() => UpdateControlPointSetupGates();
}
