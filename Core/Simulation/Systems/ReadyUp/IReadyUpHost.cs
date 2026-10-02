namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="ReadyUpSystem"/> needs from the world coordinator.
/// </summary>
internal interface IReadyUpHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    ObjectiveStateStore Objectives { get; }
    CompetitiveReadyUpState ReadyUpState { get; }

    void ResetModeStateForNewRound();
    void RestartCurrentRound(bool preservePlayerStats, bool enterCompetitiveSkirmish = true);
    void UpdateControlPointSetupGates();
}
