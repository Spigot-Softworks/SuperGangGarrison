namespace OpenGarrison.Core;

internal sealed class ArenaResolutionController
{
    private readonly IMatchObjectiveHost _host;

    public ArenaResolutionController(IMatchObjectiveHost host)
    {
        _host = host;
    }

    public void AdvanceResolution()
    {
        if (_host.MatchState.IsEnded)
        {
            return;
        }

        var redAlive = _host.ArenaRedAliveCount;
        var blueAlive = _host.ArenaBlueAliveCount;
        var redPlayers = _host.ArenaRedPlayerCount;
        var bluePlayers = _host.ArenaBluePlayerCount;

        if (redPlayers > 0 && bluePlayers > 0)
        {
            if (redAlive == 0 && blueAlive > 0)
            {
                EndArenaRound(PlayerTeam.Blue);
                return;
            }

            if (blueAlive == 0 && redAlive > 0)
            {
                EndArenaRound(PlayerTeam.Red);
                return;
            }
        }

        if (_host.MatchState.TimeRemainingTicks > 0)
        {
            _host.MatchState = _host.MatchState with { TimeRemainingTicks = _host.MatchState.TimeRemainingTicks - 1 };
            if (_host.MatchState.TimeRemainingTicks > 0)
            {
                return;
            }
        }

        if (redAlive > 0 && blueAlive > 0 && redPlayers > 0 && bluePlayers > 0)
        {
            _host.MatchState = _host.MatchState with { Phase = MatchPhase.Overtime, WinnerTeam = null };
            return;
        }

        _host.TryEndRound(null, "arena_time_limit");
    }

    private void EndArenaRound(PlayerTeam winner)
    {
        if (_host.MatchState.IsEnded)
        {
            return;
        }

        if (!_host.TryEndRound(winner, "arena_elimination"))
        {
            return;
        }

        _host.Objectives.Arena.RecordRoundWin(winner);

    }
}
