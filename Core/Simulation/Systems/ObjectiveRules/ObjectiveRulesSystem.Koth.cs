using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

internal sealed partial class ObjectiveRulesSystem
{
    public bool IsKothModeActive => IsKothMode(_host.MatchRules.Mode);

    public int KothRedTimerTicksRemaining => _host.Objectives.Koth.RedTimerTicksRemaining;

    public int KothBlueTimerTicksRemaining => _host.Objectives.Koth.BlueTimerTicksRemaining;

    public int KothUnlockTicksRemaining => _host.Objectives.Koth.UnlockTicksRemaining;

    internal static bool IsKothMode(GameModeKind mode)
    {
        return mode is GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill;
    }

    internal static bool IsControlPointMode(GameModeKind mode)
    {
        return mode is GameModeKind.ControlPoint or GameModeKind.Vip;
    }

    internal void ResetKothStateForNewRound()
    {
        _host.InitializeControlPointsForLevel();
        _host.Objectives.ControlPoints.SetupMode = false;
        _host.Objectives.ControlPoints.SetupTicksRemaining = 0;
        _host.UpdateControlPointSetupGates();

        _host.Objectives.Koth.RedTimerTicksRemaining = GetDefaultKothTeamTimerTicks();
        _host.Objectives.Koth.BlueTimerTicksRemaining = GetDefaultKothTeamTimerTicks();
        _host.Objectives.Koth.UnlockTicksRemaining = GetDefaultKothUnlockTicks();

        for (var index = 0; index < _host.Objectives.ControlPoints.Points.Count; index += 1)
        {
            var point = _host.Objectives.ControlPoints.Points[index];
            point.CapTimeTicks = GetDefaultKothCapTimeTicks();
            point.CappingTicks = 0f;
            point.CappingTeam = null;
            point.Cappers = 0;
            point.RedCappers = 0;
            point.BlueCappers = 0;
            point.IsLocked = true;
            var context = new ControlPointOwnershipContext(
                point.Index,
                _host.Objectives.ControlPoints.Points.Count,
                _host.Objectives.ControlPoints.SetupMode,
                _host.MatchRules.Mode);
            point.Team = ControlPointOwnershipResolver.ResolveInitialTeam(point.Marker, in context);
        }
    }

    internal void UpdateKothState()
    {
        if (!IsKothMode(_host.MatchRules.Mode) || _host.MatchState.IsEnded || _host.Objectives.ControlPoints.Points.Count == 0)
        {
            return;
        }

        if (_host.Objectives.Koth.UnlockTicksRemaining > 0)
        {
            _host.Objectives.Koth.UnlockTicksRemaining -= 1;
        }

        if (_host.MatchRules.Mode == GameModeKind.KingOfTheHill)
        {
            var point = GetSingleKothPoint();
            if (point?.Team == PlayerTeam.Red && _host.Objectives.Koth.RedTimerTicksRemaining > 0)
            {
                _host.Objectives.Koth.RedTimerTicksRemaining -= 1;
            }
            else if (point?.Team == PlayerTeam.Blue && _host.Objectives.Koth.BlueTimerTicksRemaining > 0)
            {
                _host.Objectives.Koth.BlueTimerTicksRemaining -= 1;
            }

            return;
        }

        var redHomePoint = GetDualKothPoint(PlayerTeam.Red);
        var blueHomePoint = GetDualKothPoint(PlayerTeam.Blue);
        if (blueHomePoint?.Team == PlayerTeam.Red && _host.Objectives.Koth.RedTimerTicksRemaining > 0)
        {
            _host.Objectives.Koth.RedTimerTicksRemaining -= 1;
        }

        if (redHomePoint?.Team == PlayerTeam.Blue && _host.Objectives.Koth.BlueTimerTicksRemaining > 0)
        {
            _host.Objectives.Koth.BlueTimerTicksRemaining -= 1;
        }
    }


    internal void AdvanceKothMatchStateCore()
    {
        if (_host.MatchState.IsEnded)
        {
            return;
        }

        if (_host.MatchState.TimeRemainingTicks > 0)
        {
            _host.MatchState = _host.MatchState with { TimeRemainingTicks = _host.MatchState.TimeRemainingTicks - 1 };
        }

        var objectiveWinner = ResolveKothObjectiveWinner();
        if (objectiveWinner.HasValue)
        {
            _host.TryEndRound(objectiveWinner, "koth_objective");
            return;
        }

        if (_host.MatchState.TimeRemainingTicks > 0)
        {
            return;
        }

        _host.TryEndRound(GetKothTimerLeader(), "koth_time_limit");
    }

    internal void ApplySnapshotKoth(SnapshotMessage snapshot)
    {
        if (!IsKothMode((GameModeKind)snapshot.GameMode))
        {
            _host.Objectives.Koth.RedTimerTicksRemaining = 0;
            _host.Objectives.Koth.BlueTimerTicksRemaining = 0;
            _host.Objectives.Koth.UnlockTicksRemaining = 0;
            return;
        }

        _host.Objectives.Koth.UnlockTicksRemaining = Math.Max(0, snapshot.KothUnlockTicksRemaining);
        _host.Objectives.Koth.RedTimerTicksRemaining = Math.Max(0, snapshot.KothRedTimerTicksRemaining);
        _host.Objectives.Koth.BlueTimerTicksRemaining = Math.Max(0, snapshot.KothBlueTimerTicksRemaining);
    }

    internal ControlPointState? GetSingleKothPoint()
    {
        for (var index = 0; index < _host.Objectives.ControlPoints.Points.Count; index += 1)
        {
            if (_host.Objectives.ControlPoints.Points[index].Marker.IsSingleKothControlPoint())
            {
                return _host.Objectives.ControlPoints.Points[index];
            }
        }

        return _host.Objectives.ControlPoints.Points.Count > 0 ? _host.Objectives.ControlPoints.Points[0] : null;
    }

    internal ControlPointState? GetDualKothPoint(PlayerTeam homeTeam)
    {
        for (var index = 0; index < _host.Objectives.ControlPoints.Points.Count; index += 1)
        {
            var marker = _host.Objectives.ControlPoints.Points[index].Marker;
            if ((homeTeam == PlayerTeam.Red && marker.IsRedKothControlPoint())
                || (homeTeam == PlayerTeam.Blue && marker.IsBlueKothControlPoint()))
            {
                return _host.Objectives.ControlPoints.Points[index];
            }
        }

        return null;
    }

    internal PlayerTeam? ResolveKothObjectiveWinner()
    {
        if (_host.MatchRules.Mode == GameModeKind.KingOfTheHill)
        {
            var point = GetSingleKothPoint();
            if (point is null)
            {
                return null;
            }

            if (_host.Objectives.Koth.RedTimerTicksRemaining <= 0
                && point.Team == PlayerTeam.Red
                && point.CappingTicks <= 0f
                && point.BlueCappers == 0)
            {
                return PlayerTeam.Red;
            }

            if (_host.Objectives.Koth.BlueTimerTicksRemaining <= 0
                && point.Team == PlayerTeam.Blue
                && point.CappingTicks <= 0f
                && point.RedCappers == 0)
            {
                return PlayerTeam.Blue;
            }

            return null;
        }

        var redHomePoint = GetDualKothPoint(PlayerTeam.Red);
        var blueHomePoint = GetDualKothPoint(PlayerTeam.Blue);
        if (blueHomePoint is not null
            && _host.Objectives.Koth.RedTimerTicksRemaining <= 0
            && blueHomePoint.Team == PlayerTeam.Red
            && blueHomePoint.CappingTicks <= 0f
            && blueHomePoint.BlueCappers == 0)
        {
            return PlayerTeam.Red;
        }

        if (redHomePoint is not null
            && _host.Objectives.Koth.BlueTimerTicksRemaining <= 0
            && redHomePoint.Team == PlayerTeam.Blue
            && redHomePoint.CappingTicks <= 0f
            && redHomePoint.RedCappers == 0)
        {
            return PlayerTeam.Blue;
        }

        return null;
    }

    internal PlayerTeam? GetKothTimerLeader()
    {
        if (_host.Objectives.Koth.RedTimerTicksRemaining < _host.Objectives.Koth.BlueTimerTicksRemaining)
        {
            return PlayerTeam.Red;
        }

        if (_host.Objectives.Koth.BlueTimerTicksRemaining < _host.Objectives.Koth.RedTimerTicksRemaining)
        {
            return PlayerTeam.Blue;
        }

        return null;
    }


    internal int GetDefaultKothTeamTimerTicks()
    {
        return _host.Config.TicksPerSecond * 180;
    }

    internal int GetDefaultKothUnlockTicks()
    {
        return _host.Config.TicksPerSecond * 30;
    }

    internal int GetDefaultKothCapTimeTicks()
    {
        return _host.Config.TicksPerSecond * 10;
    }
}
