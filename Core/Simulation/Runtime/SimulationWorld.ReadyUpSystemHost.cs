namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IReadyUpHost
{
    ObjectiveStateStore IReadyUpHost.Objectives => Objectives;
    CompetitiveReadyUpState IReadyUpHost.ReadyUpState => ReadyUpState;
    void IReadyUpHost.ResetModeStateForNewRound()
        => ResetModeStateForNewRound();
    void IReadyUpHost.RestartCurrentRound(bool preservePlayerStats, bool enterCompetitiveSkirmish)
        => RestartCurrentRound(preservePlayerStats, enterCompetitiveSkirmish);
    void IReadyUpHost.UpdateControlPointSetupGates()
        => ObjectiveRules.UpdateControlPointSetupGates();
}
