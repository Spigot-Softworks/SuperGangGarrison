namespace OpenGarrison.Core;

internal sealed class ScoreLimitResolutionController
{
    private readonly IMatchObjectiveHost _host;

    public ScoreLimitResolutionController(IMatchObjectiveHost host)
    {
        _host = host;
    }

    public void AdvanceResolution()
    {
        if (_host.MatchState.IsEnded)
        {
            return;
        }

        var winner = GetCapLimitWinner();
        if (winner.HasValue)
        {
            _host.TryEndRound(winner, "score_limit");
            return;
        }

        if (_host.MatchState.TimeRemainingTicks > 0)
        {
            _host.MatchState = _host.MatchState with { TimeRemainingTicks = _host.MatchState.TimeRemainingTicks - 1 };
            if (_host.MatchState.TimeRemainingTicks > 0)
            {
                return;
            }
        }

        _host.TryEndRound(GetHigherCapWinner(), "time_limit");
    }

    private PlayerTeam? GetCapLimitWinner()
    {
        if (_host.RedCaps >= _host.MatchRules.CapLimit)
        {
            return PlayerTeam.Red;
        }

        if (_host.BlueCaps >= _host.MatchRules.CapLimit)
        {
            return PlayerTeam.Blue;
        }

        return null;
    }

    private PlayerTeam? GetHigherCapWinner()
    {
        if (_host.RedCaps > _host.BlueCaps)
        {
            return PlayerTeam.Red;
        }

        if (_host.BlueCaps > _host.RedCaps)
        {
            return PlayerTeam.Blue;
        }

        return null;
    }
}
