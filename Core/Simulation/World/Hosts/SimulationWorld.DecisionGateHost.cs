namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IDecisionGateHost
{
    int IDecisionGateHost.BlueCaps { get => BlueCaps; set => BlueCaps = value; }
    long IDecisionGateHost.Frame => Frame;
    MatchRules IDecisionGateHost.MatchRules => MatchRules;
    MatchState IDecisionGateHost.MatchState { get => MatchState; set => MatchState = value; }
    NetworkPlayerSystem IDecisionGateHost.NetworkPlayers => NetworkPlayers;
    void IDecisionGateHost.QueuePendingMapChange() => QueuePendingMapChange();
    int IDecisionGateHost.RedCaps { get => RedCaps; set => RedCaps = value; }
}
