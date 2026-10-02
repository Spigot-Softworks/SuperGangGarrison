namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public void AdvanceOneTick()
    {
        _runtime.Tick();
    }

    private int CountPlayers(PlayerTeam team)
    {
        return _playerCounts.CountPlayers(team);
    }

    private int CountAlivePlayers(PlayerTeam team)
    {
        return _playerCounts.CountAlivePlayers(team);
    }

    private int CountPlayersInArenaCaptureZone(PlayerTeam team)
    {
        return _playerCounts.CountPlayersInArenaCaptureZone(team);
    }
}
