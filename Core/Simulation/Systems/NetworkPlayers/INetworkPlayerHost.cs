using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

/// <summary>
/// Narrow view of the world used by <see cref="NetworkPlayerSystem"/>.
/// </summary>
internal partial interface INetworkPlayerHost
{
    ClassRulesSystem ClassRules { get; }
    CombatFeedbackSystem CombatFeedback { get; }
    PracticeDummyState DummyState { get; }
    EntityStore EntityStore { get; }
    ExperimentalRulesSystem ExperimentalRules { get; }
    PlayerEntity FriendlyDummy { get; }
    bool FriendlyDummyEnabled { get; }
    LastToDieRulesSystem LastToDieRules { get; }
    LastToDieState LastToDieState { get; }
    PlayerEntity LocalPlayer { get; }
    int LocalPlayerRespawnTicks { get; set; }
    PlayerTeam LocalPlayerTeam { get; set; }
    LocalSimulationState LocalState { get; }
    MatchRules MatchRules { get; }
    MatchSettingsState MatchSettings { get; }
    MovementSystem Movement { get; }
    ObjectiveRulesSystem ObjectiveRules { get; }
    PlayerDeathSystem PlayerDeaths { get; }
    PlayerInputSystem PlayerInput { get; }
    NetworkPlayerRegistry PlayerRegistry { get; }
    PracticeDummySystem PracticeDummies { get; }
    ProjectileSystem Projectiles { get; }
    ReadyUpSystem ReadyUp { get; }
    RemoteSnapshotPlayerRegistry RemoteSnapshots { get; }
    ServerTuningSystem ServerTuning { get; }
    SpawnSystem Spawns { get; }

    int AllocateEntityId();
    IEnumerable<PlayerEntity> EnumerateSimulatedPlayers();
    PlayerTeam GetNetworkPlayerTeam(byte slot);
    bool IsNetworkPlayerActive(byte slot);
    bool TrySetLocalClass(PlayerClass playerClass);
    bool TrySetLocalClass(string gameplayClassId);
}
