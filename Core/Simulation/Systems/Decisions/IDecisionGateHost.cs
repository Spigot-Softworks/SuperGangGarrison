namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="DecisionGate"/> needs from the world coordinator.
/// </summary>
internal interface IDecisionGateHost
{
    long Frame { get; }
    int RedCaps { get; set; }
    int BlueCaps { get; set; }
    MatchRules MatchRules { get; }
    MatchState MatchState { get; set; }

    bool TryGetPlayerNetworkSlot(PlayerEntity player, out byte slot);
    void QueuePendingMapChange();
}
