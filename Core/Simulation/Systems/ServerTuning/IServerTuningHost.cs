namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="ServerTuningSystem"/> needs from the world coordinator.
/// </summary>
internal interface IServerTuningHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    PracticeDummyState DummyState { get; }
    PlayerEntity EnemyPlayer { get; }
    PlayerEntity FriendlyDummy { get; }
    PlayerEntity LocalPlayer { get; }
    MatchSettingsState MatchSettings { get; }
    NetworkPlayerSystem NetworkPlayerRules { get; }
    NetworkPlayerRegistry PlayerRegistry { get; }
    SpawnSystem Spawns { get; }

}
