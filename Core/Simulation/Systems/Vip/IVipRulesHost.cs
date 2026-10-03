namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="VipRulesSystem"/> needs from the world coordinator.
/// </summary>
internal interface IVipRulesHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    ClassRulesSystem ClassRules { get; }
    bool ControlPointSetupActive { get; }
    ExperimentalRulesSystem ExperimentalRules { get; }
    PlayerEntity LocalPlayer { get; }
    MatchRules MatchRules { get; }
    MatchState MatchState { get; set; }
    NetworkPlayerSystem NetworkPlayers { get; }
    ObjectiveStateStore Objectives { get; }
    SimulationRandomStreams Randoms { get; }
    VipState VipState { get; }

}
