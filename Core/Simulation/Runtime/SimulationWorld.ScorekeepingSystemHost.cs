namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IScorekeepingHost
{
    MatchState IScorekeepingHost.MatchState => MatchState;
}
