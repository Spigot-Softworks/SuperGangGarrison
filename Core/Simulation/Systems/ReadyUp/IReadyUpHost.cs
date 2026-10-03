namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="ReadyUpSystem"/> needs from the world coordinator.
/// </summary>
internal interface IReadyUpHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    ObjectiveRulesSystem ObjectiveRules { get; }
    ObjectiveStateStore Objectives { get; }
    CompetitiveReadyUpState ReadyUpState { get; }

    void ResetModeStateForNewRound();
    void RestartCurrentRound(bool preservePlayerStats, bool enterCompetitiveSkirmish = true);
}
