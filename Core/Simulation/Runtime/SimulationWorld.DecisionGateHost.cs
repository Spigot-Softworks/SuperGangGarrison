namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IDecisionGateHost
{
    long IDecisionGateHost.Frame => Frame;
    int IDecisionGateHost.RedCaps { get => RedCaps; set => RedCaps = value; }
    int IDecisionGateHost.BlueCaps { get => BlueCaps; set => BlueCaps = value; }
    MatchRules IDecisionGateHost.MatchRules => MatchRules;
    MatchState IDecisionGateHost.MatchState { get => MatchState; set => MatchState = value; }

    bool IDecisionGateHost.TryGetPlayerNetworkSlot(PlayerEntity player, out byte slot) => NetworkPlayerRules.TryGetPlayerNetworkSlot(player, out slot);
    void IDecisionGateHost.QueuePendingMapChange() => QueuePendingMapChange();
}
