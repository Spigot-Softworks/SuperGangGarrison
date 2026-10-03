namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="DecisionGate"/> needs from the world coordinator.
/// </summary>
internal interface IDecisionGateHost
{
    int BlueCaps { get; set; }
    long Frame { get; }
    MatchRules MatchRules { get; }
    MatchState MatchState { get; set; }
    NetworkPlayerSystem NetworkPlayers { get; }
    MapLifecycleSystem MapLifecycle { get; }
    int RedCaps { get; set; }
}
