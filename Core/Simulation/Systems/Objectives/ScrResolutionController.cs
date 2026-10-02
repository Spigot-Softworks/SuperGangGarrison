namespace OpenGarrison.Core;

internal sealed class ScrResolutionController
{
    private readonly IMatchObjectiveHost _host;

    public ScrResolutionController(IMatchObjectiveHost host)
    {
        _host = host;
    }

    public void AdvanceResolution()
    {
        if (_host.MatchState.IsEnded)
        {
            return;
        }

        if (_host.TryEvaluateScrThresholdCrossing(isRoundStart: false))
        {
            return;
        }

        if (_host.MatchState.TimeRemainingTicks > 0)
        {
            _host.MatchState = _host.MatchState with
            {
                TimeRemainingTicks = _host.MatchState.TimeRemainingTicks - 1,
            };
            if (_host.MatchState.TimeRemainingTicks > 0)
            {
                _host.UpdateScrQualificationTracking();
                return;
            }
        }

        _host.TryEndRound(
            _host.Level.ScrSettings.ResolveRoundEndWinner(_host.RedCaps, _host.BlueCaps),
            "scr_time_limit");
    }
}
