using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IObjectiveRulesHost
{
    void IObjectiveRulesHost.ApplyExplosionImpulse(PlayerEntity player, float originX, float originY, float impulse) => ExplosionGeometry.ApplyExplosionImpulse(player, originX, originY, impulse);
    int IObjectiveRulesHost.BlueCaps { get => BlueCaps; set => BlueCaps = value; }
    IReadOnlyList<BubbleProjectileEntity> IObjectiveRulesHost.Bubbles => Bubbles;
    CombatSystem IObjectiveRulesHost.Combat => Combat;
    bool IObjectiveRulesHost.CompetitiveObjectivesLocked => ReadyUp.CompetitiveObjectivesLocked;
    SimulationConfig IObjectiveRulesHost.Config => Config;
    float IObjectiveRulesHost.ConfiguredCaptureSpeedMultiplierPerPlayer => ConfiguredCaptureSpeedMultiplierPerPlayer;
    DecisionGate IObjectiveRulesHost.DecisionGate => DecisionGate;
    IEnumerable<PlayerEntity> IObjectiveRulesHost.EnumerateSimulatedPlayers() => EnumerateSimulatedPlayers();
    ExperimentalRulesSystem IObjectiveRulesHost.ExperimentalRules => ExperimentalRules;
    ExplosionRulesSystem IObjectiveRulesHost.ExplosionRules => ExplosionRules;
    long IObjectiveRulesHost.Frame => Frame;
    PlayerTeam IObjectiveRulesHost.GetOpposingTeam(PlayerTeam team) => team == PlayerTeam.Blue ? PlayerTeam.Red : PlayerTeam.Blue;
    bool IObjectiveRulesHost.IsVipModeActive => VipRules.IsVipModeActive;
    KillFeedSystem IObjectiveRulesHost.KillFeed => KillFeed;
    LastToDieRulesSystem IObjectiveRulesHost.LastToDieRules => LastToDieRules;
    SimpleLevel IObjectiveRulesHost.Level => Level;
    PlayerEntity IObjectiveRulesHost.LocalPlayer => LocalPlayer;
    PlayerTeam IObjectiveRulesHost.LocalPlayerTeam => LocalPlayerTeam;
    MapLogicSystem IObjectiveRulesHost.MapLogic => MapLogic;
    MapRuntimeState IObjectiveRulesHost.MapRuntime => MapRuntime;
    MatchRules IObjectiveRulesHost.MatchRules { get => MatchRules; set => MatchRules = value; }
    MatchState IObjectiveRulesHost.MatchState { get => MatchState; set => MatchState = value; }
    IReadOnlyList<MineProjectileEntity> IObjectiveRulesHost.Mines => Mines;
    bool IObjectiveRulesHost.NearlyEqual(float left, float right) => MathF.Abs(left - right) <= 0.01f;
    ObjectiveStateStore IObjectiveRulesHost.Objectives => Objectives;
    PlayerDeathSystem IObjectiveRulesHost.PlayerDeaths => PlayerDeaths;
    int IObjectiveRulesHost.RedCaps { get => RedCaps; set => RedCaps = value; }
    void IObjectiveRulesHost.RemoveBubbleAt(int index) => Projectiles.RemoveBubbleAt(index);
    IReadOnlyList<RocketProjectileEntity> IObjectiveRulesHost.Rockets => Rockets;
    Func<WorldScoreDecisionRequest, WorldDecisionResult>? IObjectiveRulesHost.ScoreDecisionInterceptor => ScoreDecisionInterceptor;
    ScorekeepingSystem IObjectiveRulesHost.Scorekeeping => Scorekeeping;
    StructureSystem IObjectiveRulesHost.Structures => Structures;
    VipRulesSystem IObjectiveRulesHost.VipRules => VipRules;
    WorldEffectsSystem IObjectiveRulesHost.WorldEffects => WorldEffects;
    WorldObjectStore IObjectiveRulesHost.WorldObjects => WorldObjects;
}
