namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IVipRulesHost
{
    ClassRulesSystem IVipRulesHost.ClassRules => ClassRules;
    bool IVipRulesHost.ControlPointSetupActive => ControlPointSetupActive;
    ExperimentalRulesSystem IVipRulesHost.ExperimentalRules => ExperimentalRules;
    PlayerEntity IVipRulesHost.LocalPlayer => LocalPlayer;
    MatchRules IVipRulesHost.MatchRules => MatchRules;
    MatchState IVipRulesHost.MatchState { get => MatchState; set => MatchState = value; }
    NetworkPlayerSystem IVipRulesHost.NetworkPlayerRules => NetworkPlayerRules;
    ObjectiveStateStore IVipRulesHost.Objectives => Objectives;
    SimulationRandomStreams IVipRulesHost.Randoms => Randoms;
    VipState IVipRulesHost.VipState => VipState;
}
