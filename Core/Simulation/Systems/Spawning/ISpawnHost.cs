namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="SpawnSystem"/> needs from the world coordinator.
/// </summary>
internal interface ISpawnHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    ClassRulesSystem ClassRules { get; }
    DecisionGate DecisionGate { get; }
    PracticeDummyState DummyState { get; }
    PlayerEntity EnemyPlayer { get; }
    bool EnemyPlayerEnabled { get; }
    ExperimentalRulesSystem ExperimentalRules { get; }
    PlayerEntity FriendlyDummy { get; }
    bool FriendlyDummyEnabled { get; }
    LastToDieRulesSystem LastToDieRules { get; }
    MatchLifecycleState Lifecycle { get; }
    LocalSimulationState LocalState { get; }
    MatchRules MatchRules { get; }
    NetworkPlayerSystem NetworkPlayers { get; }
    ObjectiveRulesSystem ObjectiveRules { get; }
    ObjectiveStateStore Objectives { get; }
    NetworkPlayerRegistry PlayerRegistry { get; }
    PracticeDummySystem PracticeDummies { get; }
    RoomEffectsSystem RoomEffects { get; }
    WorldEffectsSystem WorldEffects { get; }

}
