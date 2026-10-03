namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : ISpawnHost
{
    ClassRulesSystem ISpawnHost.ClassRules => ClassRules;
    DecisionGate ISpawnHost.Decisions => Decisions;
    PracticeDummyState ISpawnHost.DummyState => DummyState;
    PlayerEntity ISpawnHost.EnemyPlayer => EnemyPlayer;
    bool ISpawnHost.EnemyPlayerEnabled => EnemyPlayerEnabled;
    ExperimentalRulesSystem ISpawnHost.ExperimentalRules => ExperimentalRules;
    PlayerEntity ISpawnHost.FriendlyDummy => FriendlyDummy;
    bool ISpawnHost.FriendlyDummyEnabled => FriendlyDummyEnabled;
    LastToDieRulesSystem ISpawnHost.LastToDieRules => LastToDieRules;
    MatchLifecycleState ISpawnHost.Lifecycle => Lifecycle;
    LocalSimulationState ISpawnHost.LocalState => LocalState;
    MatchRules ISpawnHost.MatchRules => MatchRules;
    NetworkPlayerSystem ISpawnHost.NetworkPlayerRules => NetworkPlayerRules;
    ObjectiveRulesSystem ISpawnHost.ObjectiveRules => ObjectiveRules;
    ObjectiveStateStore ISpawnHost.Objectives => Objectives;
    NetworkPlayerRegistry ISpawnHost.PlayerRegistry => PlayerRegistry;
    PracticeDummySystem ISpawnHost.PracticeDummies => PracticeDummies;
    RoomEffectsSystem ISpawnHost.RoomEffects => RoomEffects;
    WorldEffectsSystem ISpawnHost.WorldEffects => WorldEffects;
}
