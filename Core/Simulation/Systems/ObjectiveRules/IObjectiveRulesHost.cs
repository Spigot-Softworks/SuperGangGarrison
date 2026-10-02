using OpenGarrison.Core.LastToDie;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

/// <summary>
/// Narrow view of the world used by <see cref="ObjectiveRulesSystem"/>.
/// </summary>
internal interface IObjectiveRulesHost
{
    int BlueCaps { get; set; }
    IReadOnlyList<BubbleProjectileEntity> Bubbles { get; }
    bool CompetitiveObjectivesLocked { get; }
    SimulationConfig Config { get; }
    float ConfiguredCaptureSpeedMultiplierPerPlayer { get; }
    long Frame { get; }
    bool IsVipModeActive { get; }
    SimpleLevel Level { get; }
    PlayerEntity LocalPlayer { get; }
    PlayerTeam LocalPlayerTeam { get; }
    MapRuntimeState MapRuntime { get; }
    MatchRules MatchRules { get; set; }
    MatchState MatchState { get; set; }
    IReadOnlyList<MineProjectileEntity> Mines { get; }
    ObjectiveStateStore Objectives { get; }
    int RedCaps { get; set; }
    IReadOnlyList<RocketProjectileEntity> Rockets { get; }
    Func<WorldScoreDecisionRequest, WorldDecisionResult>? ScoreDecisionInterceptor { get; }
    WorldObjectStore WorldObjects { get; }

    void ApplyDeadBodyExplosionImpulse(float originX, float originY, float blastRadius, float maxImpulse, float? falloffRadius = null);
    void ApplyExplosionImpulse(PlayerEntity player, float originX, float originY, float impulse);
    bool ApplyGeneratorDamage(GeneratorState target, float damage, PlayerEntity? attacker);
    void ApplyPlayerGibExplosionImpulse(float originX, float originY, float blastRadius, float maxImpulse, float? falloffRadius = null);
    void AwardObjectiveCapturePoints(PlayerEntity player);
    bool CanPlayerAffectControlPointInVipMode();
    bool CanPlayerCaptureControlPointsWhileUbered(PlayerEntity player);
    bool CanPlayerCaptureInVipMode(PlayerEntity player);
    bool CanPlayerContributeToControlPoint(PlayerEntity player);
    bool CanPlayerPauseVipCaptureDecay(PlayerEntity player);
    void DestroySentry(SentryEntity sentry, PlayerEntity? attacker = null);
    IEnumerable<PlayerEntity> EnumerateSimulatedPlayers();
    void EvaluateMapLogicGraph(bool resetStatefulNodes = true);
    void ExplodeMine(MineProjectileEntity mine, bool triggerNearbyMines = true);
    void ExplodeRocket(RocketProjectileEntity rocket, PlayerEntity? directHitPlayer, SentryEntity? directHitSentry, GeneratorState? directHitGenerator, int directHitDamageableZoneRoomObjectIndex = -1);
    MineProjectileEntity? FindMineById(int mineId);
    ExperimentalGameplaySettings GetLastToDieGameplaySettings(PlayerEntity? player);
    PlayerTeam GetOpposingTeam(PlayerTeam team);
    bool IsExperimentalPracticePowerOwner(PlayerEntity? player);
    bool IsLastToDieGameplaySettingEnabled(Func<ExperimentalGameplaySettings, bool> selector);
    void KillPlayer(PlayerEntity player, bool gibbed = false, PlayerEntity? killer = null, string? weaponSpriteName = null, DeadBodyAnimationKind deadBodyAnimationKind = DeadBodyAnimationKind.Default, string? deathCamMessage = null, SentryEntity? deathCamSentry = null, string? killFeedMessage = null, bool createDeathCam = true, bool spawnRemains = true, bool forceCorpseRemains = false, bool recordKillFeed = true, int assistingPlayerIdOverride = -1, bool completingLastToDieSpyAfterlifeDeath = false);
    bool NearlyEqual(float left, float right);
    void RecordControlPointCapturedObjectiveLog(PlayerTeam team, IReadOnlyCollection<int> capperIds);
    void RecordControlPointDefendedObjectiveLog(PlayerTeam team, IReadOnlyCollection<int> defenderIds);
    void RecordGeneratorDestroyedObjectiveLog(PlayerTeam team);
    void RecordIntelCapturedObjectiveLog(PlayerEntity player);
    void RecordIntelDroppedObjectiveLog(PlayerEntity player);
    void RecordIntelPickedUpObjectiveLog(PlayerEntity player);
    void RefreshMapLogicRuntimeIfControlPointInputsChanged();
    void RegisterVisualEffect(string effectName, float x, float y, float directionDegrees = 0f, int count = 1, bool normalizeDirection = true);
    void RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId = -1);
    void RemoveBubbleAt(int index);
    bool ShouldCancelPickup(WorldPickupKind kind, PlayerEntity player, int pickupEntityId, string pickupValue, float x, float y);
    void SyncMapLogicRuntimeFromAuthoritativeControlPoints(bool newRound);
    void TickMapLogicTimersOncePerFrame();
    bool TryAwardTeamScore(PlayerTeam team, int delta, string reason, int actorPlayerId = -1);
    bool TryEndRound(PlayerTeam? winnerTeam, string reason);
}
