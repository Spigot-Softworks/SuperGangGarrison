namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IReadyUpHost
{
    ObjectiveRulesSystem IReadyUpHost.ObjectiveRules => ObjectiveRules;
    ObjectiveStateStore IReadyUpHost.Objectives => Objectives;
    CompetitiveReadyUpState IReadyUpHost.ReadyUpState => ReadyUpState;
    void IReadyUpHost.ResetModeStateForNewRound()
        => ResetModeStateForNewRound();
    void IReadyUpHost.RestartCurrentRound(bool preservePlayerStats, bool enterCompetitiveSkirmish)
        => RestartCurrentRound(preservePlayerStats, enterCompetitiveSkirmish);
}
