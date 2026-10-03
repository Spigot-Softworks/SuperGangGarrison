using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

/// <summary>
/// Narrow view of the world used by <see cref="ObjectiveRulesSystem"/>.
/// </summary>
internal interface IObjectiveRulesHost
{
    int BlueCaps { get; set; }
    IReadOnlyList<BubbleProjectileEntity> Bubbles { get; }
    CombatSystem Combat { get; }
    bool CompetitiveObjectivesLocked { get; }
    SimulationConfig Config { get; }
    float ConfiguredCaptureSpeedMultiplierPerPlayer { get; }
    DecisionGate DecisionGate { get; }
    ExperimentalRulesSystem ExperimentalRules { get; }
    ExplosionRulesSystem ExplosionRules { get; }
    long Frame { get; }
    bool IsVipModeActive { get; }
    KillFeedSystem KillFeed { get; }
    LastToDieRulesSystem LastToDieRules { get; }
    SimpleLevel Level { get; }
    PlayerEntity LocalPlayer { get; }
    PlayerTeam LocalPlayerTeam { get; }
    MapLogicSystem MapLogic { get; }
    MapRuntimeState MapRuntime { get; }
    MatchRules MatchRules { get; set; }
    MatchState MatchState { get; set; }
    IReadOnlyList<MineProjectileEntity> Mines { get; }
    ObjectiveStateStore Objectives { get; }
    PlayerDeathSystem PlayerDeaths { get; }
    int RedCaps { get; set; }
    IReadOnlyList<RocketProjectileEntity> Rockets { get; }
    Func<WorldScoreDecisionRequest, WorldDecisionResult>? ScoreDecisionInterceptor { get; }
    ScorekeepingSystem Scorekeeping { get; }
    StructureSystem Structures { get; }
    VipRulesSystem VipRules { get; }
    WorldEffectsSystem WorldEffects { get; }
    WorldObjectStore WorldObjects { get; }

    void ApplyExplosionImpulse(PlayerEntity player, float originX, float originY, float impulse);
    IEnumerable<PlayerEntity> EnumerateSimulatedPlayers();
    PlayerTeam GetOpposingTeam(PlayerTeam team);
    bool NearlyEqual(float left, float right);
    void RemoveBubbleAt(int index);
}
