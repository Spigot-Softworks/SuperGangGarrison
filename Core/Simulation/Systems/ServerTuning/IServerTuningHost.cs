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
    NetworkPlayerRegistry PlayerRegistry { get; }

    PlayerTeam GetNetworkPlayerConfiguredTeam(byte slot);
    SpawnPoint ReserveSpawn(PlayerEntity player, PlayerTeam team);
    SpawnPoint ReserveSpawn(PlayerEntity player, PlayerTeam team, byte slot);
    bool TryGetNetworkPlayer(byte slot, out PlayerEntity player);
}
