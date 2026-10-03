namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="MapLifecycleSystem"/> needs from the world coordinator: the
/// match-level values it replaces on a level load or round restart, the stores it
/// clears, and the systems it resets.
/// </summary>
internal interface IMapLifecycleHost
{
    SimpleLevel Level { get; set; }
    MatchRules MatchRules { get; set; }
    MatchState MatchState { get; set; }
    int RedCaps { get; set; }
    int BlueCaps { get; set; }
    int SessionPresentationSeed { get; set; }
    LocalDeathCamState? LocalDeathCam { get; set; }
    SimulationConfig Config { get; }

    PlayerEntity LocalPlayer { get; }
    PlayerEntity EnemyPlayer { get; }
    PlayerEntity FriendlyDummy { get; }

    ClientSnapshotState ClientSnapshots { get; }
    CombatRuntimeState CombatRuntime { get; }
    PracticeDummyState DummyState { get; }
    MatchLifecycleState Lifecycle { get; }
    MapRuntimeState MapRuntime { get; }
    MatchSettingsState MatchSettings { get; }
    ObjectiveStateStore Objectives { get; }
    NetworkPlayerRegistry PlayerRegistry { get; }
    PresentationEventLog PresentationEvents { get; }
    CompetitiveReadyUpState ReadyUpState { get; }
    WorldObjectStore WorldObjects { get; }

    CombatSystem Combat { get; }
    CombatFeedbackSystem CombatFeedback { get; }
    MapLogicSystem MapLogic { get; }
    MovementSystem Movement { get; }
    NetworkPlayerSystem NetworkPlayers { get; }
    ObjectiveRulesSystem ObjectiveRules { get; }
    PickupSystem Pickups { get; }
    PlayerDeathSystem PlayerDeaths { get; }
    ProjectileSystem Projectiles { get; }
    ReadyUpSystem ReadyUp { get; }
    SpawnSystem Spawns { get; }
    StructureSystem Structures { get; }
    VipRulesSystem VipRules { get; }
}
