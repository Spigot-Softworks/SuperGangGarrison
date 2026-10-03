using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : INetworkPlayerHost
{
    int INetworkPlayerHost.AllocateEntityId() => AllocateEntityId();
    ClassRulesSystem INetworkPlayerHost.ClassRules => ClassRules;
    CombatFeedbackSystem INetworkPlayerHost.CombatFeedback => CombatFeedback;
    PracticeDummyState INetworkPlayerHost.DummyState => DummyState;
    EntityStore INetworkPlayerHost.EntityStore => EntityStore;
    IEnumerable<PlayerEntity> INetworkPlayerHost.EnumerateSimulatedPlayers() => EnumerateSimulatedPlayers();
    ExperimentalRulesSystem INetworkPlayerHost.ExperimentalRules => ExperimentalRules;
    PlayerEntity INetworkPlayerHost.FriendlyDummy => FriendlyDummy;
    bool INetworkPlayerHost.FriendlyDummyEnabled => FriendlyDummyEnabled;
    PlayerTeam INetworkPlayerHost.GetNetworkPlayerTeam(byte slot) => GetNetworkPlayerTeam(slot);
    bool INetworkPlayerHost.IsNetworkPlayerActive(byte slot) => IsNetworkPlayerActive(slot);
    LastToDieRulesSystem INetworkPlayerHost.LastToDieRules => LastToDieRules;
    LastToDieState INetworkPlayerHost.LastToDieState => LastToDieState;
    PlayerEntity INetworkPlayerHost.LocalPlayer => LocalPlayer;
    int INetworkPlayerHost.LocalPlayerRespawnTicks { get => LocalPlayerRespawnTicks; set => LocalPlayerRespawnTicks = value; }
    PlayerTeam INetworkPlayerHost.LocalPlayerTeam { get => LocalPlayerTeam; set => LocalPlayerTeam = value; }
    LocalSimulationState INetworkPlayerHost.LocalState => LocalState;
    MatchRules INetworkPlayerHost.MatchRules => MatchRules;
    MatchSettingsState INetworkPlayerHost.MatchSettings => MatchSettings;
    MovementSystem INetworkPlayerHost.Movement => Movement;
    ObjectiveRulesSystem INetworkPlayerHost.ObjectiveRules => ObjectiveRules;
    PlayerDeathSystem INetworkPlayerHost.PlayerDeaths => PlayerDeaths;
    PlayerInputSystem INetworkPlayerHost.PlayerInput => PlayerInput;
    NetworkPlayerRegistry INetworkPlayerHost.PlayerRegistry => PlayerRegistry;
    PracticeDummySystem INetworkPlayerHost.PracticeDummies => PracticeDummies;
    ProjectileSystem INetworkPlayerHost.Projectiles => Projectiles;
    ReadyUpSystem INetworkPlayerHost.ReadyUp => ReadyUp;
    RemoteSnapshotPlayerRegistry INetworkPlayerHost.RemoteSnapshots => RemoteSnapshots;
    ServerTuningSystem INetworkPlayerHost.ServerTuning => ServerTuning;
    SpawnSystem INetworkPlayerHost.Spawns => Spawns;
    bool INetworkPlayerHost.TrySetLocalClass(PlayerClass playerClass) => TrySetLocalClass(playerClass);
    bool INetworkPlayerHost.TrySetLocalClass(string gameplayClassId) => TrySetLocalClass(gameplayClassId);
}
