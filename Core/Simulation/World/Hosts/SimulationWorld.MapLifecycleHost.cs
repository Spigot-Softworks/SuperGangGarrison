namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IMapLifecycleHost
{
    SimpleLevel IMapLifecycleHost.Level { get => Level; set => Level = value; }
    MatchRules IMapLifecycleHost.MatchRules { get => MatchRules; set => MatchRules = value; }
    MatchState IMapLifecycleHost.MatchState { get => MatchState; set => MatchState = value; }
    int IMapLifecycleHost.RedCaps { get => RedCaps; set => RedCaps = value; }
    int IMapLifecycleHost.BlueCaps { get => BlueCaps; set => BlueCaps = value; }
    int IMapLifecycleHost.SessionPresentationSeed { get => SessionPresentationSeed; set => SessionPresentationSeed = value; }
    LocalDeathCamState? IMapLifecycleHost.LocalDeathCam { get => LocalDeathCam; set => LocalDeathCam = value; }
    SimulationConfig IMapLifecycleHost.Config => Config;
    PlayerEntity IMapLifecycleHost.LocalPlayer => LocalPlayer;
    PlayerEntity IMapLifecycleHost.EnemyPlayer => EnemyPlayer;
    PlayerEntity IMapLifecycleHost.FriendlyDummy => FriendlyDummy;
    ClientSnapshotState IMapLifecycleHost.ClientSnapshots => ClientSnapshots;
    CombatRuntimeState IMapLifecycleHost.CombatRuntime => CombatRuntime;
    PracticeDummyState IMapLifecycleHost.DummyState => DummyState;
    MatchLifecycleState IMapLifecycleHost.Lifecycle => Lifecycle;
    MapRuntimeState IMapLifecycleHost.MapRuntime => MapRuntime;
    MatchSettingsState IMapLifecycleHost.MatchSettings => MatchSettings;
    ObjectiveStateStore IMapLifecycleHost.Objectives => Objectives;
    NetworkPlayerRegistry IMapLifecycleHost.PlayerRegistry => PlayerRegistry;
    PresentationEventLog IMapLifecycleHost.PresentationEvents => PresentationEvents;
    CompetitiveReadyUpState IMapLifecycleHost.ReadyUpState => ReadyUpState;
    WorldObjectStore IMapLifecycleHost.WorldObjects => WorldObjects;
    CombatSystem IMapLifecycleHost.Combat => Combat;
    CombatFeedbackSystem IMapLifecycleHost.CombatFeedback => CombatFeedback;
    MapLogicSystem IMapLifecycleHost.MapLogic => MapLogic;
    MovementSystem IMapLifecycleHost.Movement => Movement;
    NetworkPlayerSystem IMapLifecycleHost.NetworkPlayers => NetworkPlayers;
    ObjectiveRulesSystem IMapLifecycleHost.ObjectiveRules => ObjectiveRules;
    PickupSystem IMapLifecycleHost.Pickups => Pickups;
    PlayerDeathSystem IMapLifecycleHost.PlayerDeaths => PlayerDeaths;
    ProjectileSystem IMapLifecycleHost.Projectiles => Projectiles;
    ReadyUpSystem IMapLifecycleHost.ReadyUp => ReadyUp;
    SpawnSystem IMapLifecycleHost.Spawns => Spawns;
    StructureSystem IMapLifecycleHost.Structures => Structures;
    VipRulesSystem IMapLifecycleHost.VipRules => VipRules;
}
