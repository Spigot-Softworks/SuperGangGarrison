namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="MapLogicSystem"/> needs from the world coordinator.
/// </summary>
internal interface IMapLogicHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    TeamIntelligenceState BlueIntel { get; }
    bool ClientPredictionMode { get; }
    PlayerEntity LocalPlayer { get; }
    MapRuntimeState MapRuntime { get; }
    MatchRules MatchRules { get; }
    MatchState MatchState { get; }
    ObjectiveStateStore Objectives { get; }
    TeamIntelligenceState RedIntel { get; }

    bool TryModifyTeamScore(PlayerTeam team, int delta, string reason, int actorPlayerId = -1);
}
