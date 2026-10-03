namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IMapLogicHost
{
    TeamIntelligenceState IMapLogicHost.BlueIntel => ObjectiveRules.BlueIntel;
    bool IMapLogicHost.ClientPredictionMode => ClientPredictionMode;
    PlayerEntity IMapLogicHost.LocalPlayer => LocalPlayer;
    MapRuntimeState IMapLogicHost.MapRuntime => MapRuntime;
    MatchRules IMapLogicHost.MatchRules => MatchRules;
    MatchState IMapLogicHost.MatchState => MatchState;
    ObjectiveStateStore IMapLogicHost.Objectives => Objectives;
    TeamIntelligenceState IMapLogicHost.RedIntel => ObjectiveRules.RedIntel;
    bool IMapLogicHost.TryModifyTeamScore(PlayerTeam team, int delta, string reason, int actorPlayerId)
        => ObjectiveRules.TryModifyTeamScore(team, delta, reason, actorPlayerId);
}
