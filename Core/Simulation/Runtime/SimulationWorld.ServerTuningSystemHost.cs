namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IServerTuningHost
{
    PracticeDummyState IServerTuningHost.DummyState => DummyState;
    PlayerEntity IServerTuningHost.EnemyPlayer => EnemyPlayer;
    PlayerEntity IServerTuningHost.FriendlyDummy => FriendlyDummy;
    PlayerTeam IServerTuningHost.GetNetworkPlayerConfiguredTeam(byte slot)
        => NetworkPlayerRules.GetNetworkPlayerConfiguredTeam(slot);
    PlayerEntity IServerTuningHost.LocalPlayer => LocalPlayer;
    MatchSettingsState IServerTuningHost.MatchSettings => MatchSettings;
    NetworkPlayerRegistry IServerTuningHost.PlayerRegistry => PlayerRegistry;
    SpawnPoint IServerTuningHost.ReserveSpawn(PlayerEntity player, PlayerTeam team)
        => Spawns.ReserveSpawn(player, team);
    SpawnPoint IServerTuningHost.ReserveSpawn(PlayerEntity player, PlayerTeam team, byte slot)
        => Spawns.ReserveSpawn(player, team, slot);
    bool IServerTuningHost.TryGetNetworkPlayer(byte slot, out PlayerEntity player)
        => NetworkPlayerRules.TryGetNetworkPlayer(slot, out player);
}
