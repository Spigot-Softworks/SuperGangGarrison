namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public void AdvanceOneTick()
    {
        _runtimeController.AdvanceOneTick();
    }

    private int CountPlayers(PlayerTeam team)
    {
        return _runtimeQueryController.CountPlayers(team);
    }

    private int CountAlivePlayers(PlayerTeam team)
    {
        return _runtimeQueryController.CountAlivePlayers(team);
    }

    private int CountPlayersInArenaCaptureZone(PlayerTeam team)
    {
        return _runtimeQueryController.CountPlayersInArenaCaptureZone(team);
    }
}
