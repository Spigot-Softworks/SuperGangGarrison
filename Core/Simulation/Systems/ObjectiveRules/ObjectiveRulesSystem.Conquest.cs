namespace OpenGarrison.Core;

internal sealed partial class ObjectiveRulesSystem
{
    internal void UpdatePointScoringState()
    {
        if (_host.MatchRules.Mode == GameModeKind.Conquest)
        {
            UpdateConquestState();
        }
        else
        {
            UpdateKothState();
        }
    }

    internal void AdvancePointScoringMatchState()
    {
        if (_host.MatchRules.Mode == GameModeKind.Conquest)
        {
            if (_host.MatchState.IsEnded)
            {
                return;
            }

            if (_host.RedCaps >= ConquestRules.ScoreLimit || _host.BlueCaps >= ConquestRules.ScoreLimit)
            {
                _host.DecisionGate.TryEndRound(GetConquestScoreLeader(), "conquest_score_limit");
                return;
            }

            if (_host.MatchState.TimeRemainingTicks > 0)
            {
                _host.MatchState = _host.MatchState with { TimeRemainingTicks = _host.MatchState.TimeRemainingTicks - 1 };
            }

            if (_host.MatchState.TimeRemainingTicks <= 0)
            {
                _host.DecisionGate.TryEndRound(GetConquestScoreLeader(), "conquest_time_limit");
            }
        }
        else
        {
            AdvanceKothMatchStateCore();
        }
    }

    private PlayerTeam? GetConquestScoreLeader() => _host.RedCaps == _host.BlueCaps ? null
        : _host.RedCaps > _host.BlueCaps ? PlayerTeam.Red : PlayerTeam.Blue;

    private void ResetConquestStateForNewRound()
    {
        InitializeControlPointsForLevel();
        _host.RedCaps = 0;
        _host.BlueCaps = 0;
        _host.Objectives.Koth.Clear();
        _host.Objectives.ControlPoints.SetupMode = false;
        _host.Objectives.ControlPoints.SetupTicksRemaining = 0;
        UpdateControlPointSetupGates();
        foreach (var point in _host.Objectives.ControlPoints.Points)
        {
            point.Team = null;
            point.CapTimeTicks = GetDefaultKothCapTimeTicks();
            point.IsLocked = false;
        }
    }

    private void UpdateConquestState()
    {
        if (_host.MatchState.IsEnded)
        {
            return;
        }

        var intervalTicks = _host.Config.TicksPerSecond * ConquestRules.ScoreIntervalSeconds;
        foreach (var point in _host.Objectives.ControlPoints.Points)
        {
            if (point.ConquestScoringTeam != point.Team)
            {
                point.ConquestOwnedTicks = 0;
                point.ConquestScoringTeam = point.Team;
            }

            if (!point.Team.HasValue || ++point.ConquestOwnedTicks < intervalTicks)
            {
                continue;
            }

            point.ConquestOwnedTicks = 0;
            // The existing team-score fields also carry TDM/SCR scores through snapshots.
            var score = point.Team == PlayerTeam.Red ? _host.RedCaps : _host.BlueCaps;
            _host.DecisionGate.TryAwardTeamScore(point.Team.Value,
                score < ConquestRules.ScoreLimit ? 1 : 0, "conquest_point_held");
        }
    }
}
