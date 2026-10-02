namespace OpenGarrison.Core;

internal sealed partial class ObjectiveRulesSystem
{

    internal bool TryModifyTeamScore(PlayerTeam team, int delta, string reason, int actorPlayerId = -1)
    {
        if (_host.MatchRules.Mode != GameModeKind.Scr || delta == 0 || _host.MatchState.IsEnded)
        {
            return false;
        }

        var current = team == PlayerTeam.Red ? _host.RedCaps : _host.BlueCaps;
        var next = ScrMapSettingsMetadata.ClampScore(current + delta);
        var appliedDelta = next - current;
        if (appliedDelta == 0)
        {
            return false;
        }

        var interceptor = _host.ScoreDecisionInterceptor;
        if (interceptor is not null
            && interceptor(new WorldScoreDecisionRequest(
                _host.Frame,
                team,
                appliedDelta,
                _host.RedCaps,
                _host.BlueCaps,
                actorPlayerId,
                reason)).IsCancelled)
        {
            return false;
        }

        if (team == PlayerTeam.Red)
        {
            _host.RedCaps = next;
        }
        else if (team == PlayerTeam.Blue)
        {
            _host.BlueCaps = next;
        }
        else
        {
            return false;
        }

        TryEvaluateScrThresholdCrossing(isRoundStart: false);
        return true;
    }

    internal bool TryEvaluateScrThresholdCrossing(bool isRoundStart)
    {
        if (_host.MatchRules.Mode != GameModeKind.Scr || _host.MatchState.IsEnded)
        {
            return false;
        }

        var settings = _host.Level.ScrSettings;
        var redQualifies = settings.TeamQualifiesForThreshold(PlayerTeam.Red, _host.RedCaps);
        var blueQualifies = settings.TeamQualifiesForThreshold(PlayerTeam.Blue, _host.BlueCaps);

        if (isRoundStart)
        {
            if (redQualifies && blueQualifies)
            {
                return _host.TryEndRound(settings.ResolveRoundEndWinner(_host.RedCaps, _host.BlueCaps), "scr_start_tiebreak");
            }

            if (redQualifies)
            {
                return _host.TryEndRound(PlayerTeam.Red, "scr_start_threshold");
            }

            if (blueQualifies)
            {
                return _host.TryEndRound(PlayerTeam.Blue, "scr_start_threshold");
            }

            UpdateScrQualificationTracking();
            return false;
        }

        if (!_host.Objectives.Scr.RedWasQualified && redQualifies)
        {
            UpdateScrQualificationTracking();
            return _host.TryEndRound(PlayerTeam.Red, "scr_threshold");
        }

        if (!_host.Objectives.Scr.BlueWasQualified && blueQualifies)
        {
            UpdateScrQualificationTracking();
            return _host.TryEndRound(PlayerTeam.Blue, "scr_threshold");
        }

        UpdateScrQualificationTracking();
        return false;
    }

    internal void ApplyScrLevelMatchSettings()
    {
        if (_host.MatchRules.Mode != GameModeKind.Scr)
        {
            return;
        }

        _host.MatchRules = _host.MatchRules with
        {
            CapLimit = ScrMapSettingsMetadata.ClampScore(_host.Level.ScrSettings.ScoreToWin),
        };
    }

    internal void ApplyScrStartingScores()
    {
        if (_host.MatchRules.Mode != GameModeKind.Scr)
        {
            return;
        }

        var settings = _host.Level.ScrSettings;
        _host.RedCaps = ScrMapSettingsMetadata.ClampScore(settings.RedStartingScore);
        _host.BlueCaps = ScrMapSettingsMetadata.ClampScore(settings.BlueStartingScore);
        ResetScrQualificationTracking();
    }

    internal void FinalizeScrRoundStart()
    {
        if (_host.MatchRules.Mode != GameModeKind.Scr)
        {
            return;
        }

        ApplyScrLevelMatchSettings();
        ApplyScrStartingScores();
        TryEvaluateScrThresholdCrossing(isRoundStart: true);
    }

    internal void CombatTestFinalizeScrRoundStart()
    {
        FinalizeScrRoundStart();
    }

    internal void UpdateScrQualificationTracking()
    {
        if (_host.MatchRules.Mode != GameModeKind.Scr)
        {
            return;
        }

        var settings = _host.Level.ScrSettings;
        _host.Objectives.Scr.RedWasQualified = settings.TeamQualifiesForThreshold(PlayerTeam.Red, _host.RedCaps);
        _host.Objectives.Scr.BlueWasQualified = settings.TeamQualifiesForThreshold(PlayerTeam.Blue, _host.BlueCaps);
    }

    internal void ResetScrQualificationTracking()
    {
        _host.Objectives.Scr.RedWasQualified = false;
        _host.Objectives.Scr.BlueWasQualified = false;
        UpdateScrQualificationTracking();
    }
}
