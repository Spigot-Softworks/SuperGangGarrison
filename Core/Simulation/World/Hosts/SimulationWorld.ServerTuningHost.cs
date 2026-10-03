namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IServerTuningHost
{
    PracticeDummyState IServerTuningHost.DummyState => DummyState;
    PlayerEntity IServerTuningHost.EnemyPlayer => EnemyPlayer;
    PlayerEntity IServerTuningHost.FriendlyDummy => FriendlyDummy;
    PlayerEntity IServerTuningHost.LocalPlayer => LocalPlayer;
    MatchSettingsState IServerTuningHost.MatchSettings => MatchSettings;
    NetworkPlayerSystem IServerTuningHost.NetworkPlayers => NetworkPlayers;
    NetworkPlayerRegistry IServerTuningHost.PlayerRegistry => PlayerRegistry;
    SpawnSystem IServerTuningHost.Spawns => Spawns;
}
