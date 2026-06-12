namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private bool _scrRedWasQualified;
    private bool _scrBlueWasQualified;
    private MapLogicActivatorRuntimeState _logicScoreTriggerRuntimeState = new();

    internal bool TryModifyTeamScore(PlayerTeam team, int delta, string reason, int actorPlayerId = -1)
    {
        if (MatchRules.Mode != GameModeKind.Scr || delta == 0 || MatchState.IsEnded)
        {
            return false;
        }

        var current = team == PlayerTeam.Red ? RedCaps : BlueCaps;
        var next = ScrMapSettingsMetadata.ClampScore(current + delta);
        var appliedDelta = next - current;
        if (appliedDelta == 0)
        {
            return false;
        }

        var interceptor = ScoreDecisionInterceptor;
        if (interceptor is not null
            && interceptor(new WorldScoreDecisionRequest(
                Frame,
                team,
                appliedDelta,
                RedCaps,
                BlueCaps,
                actorPlayerId,
                reason)).IsCancelled)
        {
            return false;
        }

        if (team == PlayerTeam.Red)
        {
            RedCaps = next;
        }
        else if (team == PlayerTeam.Blue)
        {
            BlueCaps = next;
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
        if (MatchRules.Mode != GameModeKind.Scr || MatchState.IsEnded)
        {
            return false;
        }

        var settings = Level.ScrSettings;
        var redQualifies = settings.TeamQualifiesForThreshold(PlayerTeam.Red, RedCaps);
        var blueQualifies = settings.TeamQualifiesForThreshold(PlayerTeam.Blue, BlueCaps);

        if (isRoundStart)
        {
            if (redQualifies && blueQualifies)
            {
                return TryEndRound(settings.ResolveRoundEndWinner(RedCaps, BlueCaps), "scr_start_tiebreak");
            }

            if (redQualifies)
            {
                return TryEndRound(PlayerTeam.Red, "scr_start_threshold");
            }

            if (blueQualifies)
            {
                return TryEndRound(PlayerTeam.Blue, "scr_start_threshold");
            }

            UpdateScrQualificationTracking();
            return false;
        }

        if (!_scrRedWasQualified && redQualifies)
        {
            UpdateScrQualificationTracking();
            return TryEndRound(PlayerTeam.Red, "scr_threshold");
        }

        if (!_scrBlueWasQualified && blueQualifies)
        {
            UpdateScrQualificationTracking();
            return TryEndRound(PlayerTeam.Blue, "scr_threshold");
        }

        UpdateScrQualificationTracking();
        return false;
    }

    private void ApplyScrLevelMatchSettings()
    {
        if (MatchRules.Mode != GameModeKind.Scr)
        {
            return;
        }

        MatchRules = MatchRules with
        {
            CapLimit = ScrMapSettingsMetadata.ClampScore(Level.ScrSettings.ScoreToWin),
        };
    }

    private void ApplyScrStartingScores()
    {
        if (MatchRules.Mode != GameModeKind.Scr)
        {
            return;
        }

        var settings = Level.ScrSettings;
        RedCaps = ScrMapSettingsMetadata.ClampScore(settings.RedStartingScore);
        BlueCaps = ScrMapSettingsMetadata.ClampScore(settings.BlueStartingScore);
        ResetScrQualificationTracking();
    }

    private void FinalizeScrRoundStart()
    {
        if (MatchRules.Mode != GameModeKind.Scr)
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
        if (MatchRules.Mode != GameModeKind.Scr)
        {
            return;
        }

        var settings = Level.ScrSettings;
        _scrRedWasQualified = settings.TeamQualifiesForThreshold(PlayerTeam.Red, RedCaps);
        _scrBlueWasQualified = settings.TeamQualifiesForThreshold(PlayerTeam.Blue, BlueCaps);
    }

    private void ResetScrQualificationTracking()
    {
        _scrRedWasQualified = false;
        _scrBlueWasQualified = false;
        UpdateScrQualificationTracking();
    }
}
