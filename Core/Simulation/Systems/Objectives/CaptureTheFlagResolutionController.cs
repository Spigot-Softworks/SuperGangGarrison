namespace OpenGarrison.Core;

internal sealed class CaptureTheFlagResolutionController
{
    private readonly IMatchObjectiveHost _host;

    public CaptureTheFlagResolutionController(IMatchObjectiveHost host)
    {
        _host = host;
    }

    public void AdvanceResolution()
    {
        if (_host.MatchState.IsEnded)
        {
            return;
        }

        var capWinner = GetCapLimitWinner();
        if (capWinner.HasValue)
        {
            _host.TryEndRound(capWinner, "score_limit");
            return;
        }

        if (_host.MatchState.Phase == MatchPhase.Overtime)
        {
            if (!AreObjectivesSettled())
            {
                return;
            }

            _host.TryEndRound(GetHigherCapWinner(), "overtime_settled");
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

        if (AreObjectivesSettled())
        {
            _host.TryEndRound(GetHigherCapWinner(), "time_limit");
            return;
        }

        _host.MatchState = _host.MatchState with { Phase = MatchPhase.Overtime, WinnerTeam = null };
    }

    private bool AreObjectivesSettled()
    {
        return _host.IsIntelAtHome(_host.RedIntel) && _host.IsIntelAtHome(_host.BlueIntel);
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
