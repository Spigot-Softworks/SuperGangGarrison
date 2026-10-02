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
    float IObjectiveRulesHost.ConfiguredCaptureSpeedMultiplierPerPlayer => ConfiguredCaptureSpeedMultiplierPerPlayer;
    long IObjectiveRulesHost.Frame => Frame;
    bool IObjectiveRulesHost.IsVipModeActive => IsVipModeActive;
    SimpleLevel IObjectiveRulesHost.Level => Level;
    PlayerEntity IObjectiveRulesHost.LocalPlayer => LocalPlayer;
    PlayerTeam IObjectiveRulesHost.LocalPlayerTeam => LocalPlayerTeam;
    MapRuntimeState IObjectiveRulesHost.MapRuntime => MapRuntime;
    MatchRules IObjectiveRulesHost.MatchRules { get => MatchRules; set => MatchRules = value; }
    MatchState IObjectiveRulesHost.MatchState { get => MatchState; set => MatchState = value; }
    IReadOnlyList<MineProjectileEntity> IObjectiveRulesHost.Mines => Mines;
    ObjectiveStateStore IObjectiveRulesHost.Objectives => Objectives;
    int IObjectiveRulesHost.RedCaps { get => RedCaps; set => RedCaps = value; }
    IReadOnlyList<RocketProjectileEntity> IObjectiveRulesHost.Rockets => Rockets;
    Func<WorldScoreDecisionRequest, WorldDecisionResult>? IObjectiveRulesHost.ScoreDecisionInterceptor => ScoreDecisionInterceptor;
    WorldObjectStore IObjectiveRulesHost.WorldObjects => WorldObjects;

    void IObjectiveRulesHost.ApplyDeadBodyExplosionImpulse(float originX, float originY, float blastRadius, float maxImpulse, float? falloffRadius) => ExplosionRules.ApplyDeadBodyExplosionImpulse(originX, originY, blastRadius, maxImpulse, falloffRadius);
    void IObjectiveRulesHost.ApplyExplosionImpulse(PlayerEntity player, float originX, float originY, float impulse) => ApplyExplosionImpulse(player, originX, originY, impulse);
    bool IObjectiveRulesHost.ApplyGeneratorDamage(GeneratorState target, float damage, PlayerEntity? attacker) => ApplyGeneratorDamage(target, damage, attacker);
    void IObjectiveRulesHost.ApplyPlayerGibExplosionImpulse(float originX, float originY, float blastRadius, float maxImpulse, float? falloffRadius) => ExplosionRules.ApplyPlayerGibExplosionImpulse(originX, originY, blastRadius, maxImpulse, falloffRadius);
    void IObjectiveRulesHost.AwardObjectiveCapturePoints(PlayerEntity player) => Scorekeeping.AwardObjectiveCapturePoints(player);
    bool IObjectiveRulesHost.CanPlayerAffectControlPointInVipMode() => VipRules.CanPlayerAffectControlPointInVipMode();
    bool IObjectiveRulesHost.CanPlayerCaptureControlPointsWhileUbered(PlayerEntity player) => LastToDieRules.CanPlayerCaptureControlPointsWhileUbered(player);
    bool IObjectiveRulesHost.CanPlayerCaptureInVipMode(PlayerEntity player) => VipRules.CanPlayerCaptureInVipMode(player);
    bool IObjectiveRulesHost.CanPlayerContributeToControlPoint(PlayerEntity player) => LastToDieRules.CanPlayerContributeToControlPoint(player);
    bool IObjectiveRulesHost.CanPlayerPauseVipCaptureDecay(PlayerEntity player) => VipRules.CanPlayerPauseVipCaptureDecay(player);
    void IObjectiveRulesHost.DestroySentry(SentryEntity sentry, PlayerEntity? attacker) => Structures.DestroySentry(sentry, attacker);
    IEnumerable<PlayerEntity> IObjectiveRulesHost.EnumerateSimulatedPlayers() => EnumerateSimulatedPlayers();
    void IObjectiveRulesHost.EvaluateMapLogicGraph(bool resetStatefulNodes) => MapLogic.EvaluateMapLogicGraph(resetStatefulNodes);
    void IObjectiveRulesHost.ExplodeMine(MineProjectileEntity mine, bool triggerNearbyMines) => ExplosionRules.ExplodeMine(mine, triggerNearbyMines);
    void IObjectiveRulesHost.ExplodeRocket(RocketProjectileEntity rocket, PlayerEntity? directHitPlayer, SentryEntity? directHitSentry, GeneratorState? directHitGenerator, int directHitDamageableZoneRoomObjectIndex) => ExplosionRules.ExplodeRocket(rocket, directHitPlayer, directHitSentry, directHitGenerator, directHitDamageableZoneRoomObjectIndex);
    MineProjectileEntity? IObjectiveRulesHost.FindMineById(int mineId) => ExplosionRules.FindMineById(mineId);
    ExperimentalGameplaySettings IObjectiveRulesHost.GetLastToDieGameplaySettings(PlayerEntity? player) => LastToDieRules.GetLastToDieGameplaySettings(player);
    PlayerTeam IObjectiveRulesHost.GetOpposingTeam(PlayerTeam team) => GetOpposingTeam(team);
    bool IObjectiveRulesHost.IsExperimentalPracticePowerOwner(PlayerEntity? player) => ExperimentalRules.IsExperimentalPracticePowerOwner(player);
    bool IObjectiveRulesHost.IsLastToDieGameplaySettingEnabled(Func<ExperimentalGameplaySettings, bool> selector) => LastToDieRules.IsLastToDieGameplaySettingEnabled(selector);
    void IObjectiveRulesHost.KillPlayer(PlayerEntity player, bool gibbed, PlayerEntity? killer, string? weaponSpriteName, DeadBodyAnimationKind deadBodyAnimationKind, string? deathCamMessage, SentryEntity? deathCamSentry, string? killFeedMessage, bool createDeathCam, bool spawnRemains, bool forceCorpseRemains, bool recordKillFeed, int assistingPlayerIdOverride, bool completingLastToDieSpyAfterlifeDeath) => PlayerDeaths.KillPlayer(player, gibbed, killer, weaponSpriteName, deadBodyAnimationKind, deathCamMessage, deathCamSentry, killFeedMessage, createDeathCam, spawnRemains, forceCorpseRemains, recordKillFeed, assistingPlayerIdOverride, completingLastToDieSpyAfterlifeDeath);
    bool IObjectiveRulesHost.NearlyEqual(float left, float right) => NearlyEqual(left, right);
    void IObjectiveRulesHost.RecordControlPointCapturedObjectiveLog(PlayerTeam team, IReadOnlyCollection<int> capperIds) => KillFeedRules.RecordControlPointCapturedObjectiveLog(team, capperIds);
    void IObjectiveRulesHost.RecordControlPointDefendedObjectiveLog(PlayerTeam team, IReadOnlyCollection<int> defenderIds) => KillFeedRules.RecordControlPointDefendedObjectiveLog(team, defenderIds);
    void IObjectiveRulesHost.RecordGeneratorDestroyedObjectiveLog(PlayerTeam team) => KillFeedRules.RecordGeneratorDestroyedObjectiveLog(team);
    void IObjectiveRulesHost.RecordIntelCapturedObjectiveLog(PlayerEntity player) => KillFeedRules.RecordIntelCapturedObjectiveLog(player);
    void IObjectiveRulesHost.RecordIntelDroppedObjectiveLog(PlayerEntity player) => KillFeedRules.RecordIntelDroppedObjectiveLog(player);
    void IObjectiveRulesHost.RecordIntelPickedUpObjectiveLog(PlayerEntity player) => KillFeedRules.RecordIntelPickedUpObjectiveLog(player);
    void IObjectiveRulesHost.RefreshMapLogicRuntimeIfControlPointInputsChanged() => MapLogic.RefreshMapLogicRuntimeIfControlPointInputsChanged();
    void IObjectiveRulesHost.RegisterVisualEffect(string effectName, float x, float y, float directionDegrees, int count, bool normalizeDirection) => WorldEffects.RegisterVisualEffect(effectName, x, y, directionDegrees, count, normalizeDirection);
    void IObjectiveRulesHost.RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId) => WorldEffects.RegisterWorldSoundEvent(soundName, x, y, sourcePlayerId);
    void IObjectiveRulesHost.RemoveBubbleAt(int index) => RemoveBubbleAt(index);
    bool IObjectiveRulesHost.ShouldCancelPickup(WorldPickupKind kind, PlayerEntity player, int pickupEntityId, string pickupValue, float x, float y) => ShouldCancelPickup(kind, player, pickupEntityId, pickupValue, x, y);
    void IObjectiveRulesHost.SyncMapLogicRuntimeFromAuthoritativeControlPoints(bool newRound) => MapLogic.SyncMapLogicRuntimeFromAuthoritativeControlPoints(newRound);
    void IObjectiveRulesHost.TickMapLogicTimersOncePerFrame() => MapLogic.TickMapLogicTimersOncePerFrame();
    bool IObjectiveRulesHost.TryAwardTeamScore(PlayerTeam team, int delta, string reason, int actorPlayerId) => TryAwardTeamScore(team, delta, reason, actorPlayerId);
    bool IObjectiveRulesHost.TryEndRound(PlayerTeam? winnerTeam, string reason) => TryEndRound(winnerTeam, reason);
}
