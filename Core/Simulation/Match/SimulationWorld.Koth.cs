using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private int _kothRedTimerTicksRemaining;
    private int _kothBlueTimerTicksRemaining;
    private int _kothUnlockTicksRemaining;

    public bool IsKothModeActive => IsKothMode(MatchRules.Mode);

    public int KothRedTimerTicksRemaining => _kothRedTimerTicksRemaining;

    public int KothBlueTimerTicksRemaining => _kothBlueTimerTicksRemaining;

    public int KothUnlockTicksRemaining => _kothUnlockTicksRemaining;

    private static bool IsKothMode(GameModeKind mode)
    {
        return mode is GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill;
    }

    private void ResetKothStateForNewRound()
    {
        InitializeControlPointsForLevel();
        _controlPointSetupMode = false;
        _controlPointSetupTicksRemaining = 0;
        UpdateControlPointSetupGates();

        _kothRedTimerTicksRemaining = GetDefaultKothTeamTimerTicks();
        _kothBlueTimerTicksRemaining = GetDefaultKothTeamTimerTicks();
        _kothUnlockTicksRemaining = GetDefaultKothUnlockTicks();

        for (var index = 0; index < _controlPoints.Count; index += 1)
        {
            var point = _controlPoints[index];
            point.CapTimeTicks = GetDefaultKothCapTimeTicks();
            point.CappingTicks = 0f;
            point.CappingTeam = null;
            point.Cappers = 0;
            point.RedCappers = 0;
            point.BlueCappers = 0;
            point.IsLocked = true;
            point.Team = MatchRules.Mode == GameModeKind.DoubleKingOfTheHill
                ? GetInitialDualKothOwner(point.Marker)
                : null;
        }
    }

    private void UpdateKothState()
    {
        if (!IsKothMode(MatchRules.Mode) || MatchState.IsEnded || _controlPoints.Count == 0)
        {
            return;
        }

        if (_kothUnlockTicksRemaining > 0)
        {
            _kothUnlockTicksRemaining -= 1;
        }

        if (MatchRules.Mode == GameModeKind.KingOfTheHill)
        {
            var point = GetSingleKothPoint();
            if (point?.Team == PlayerTeam.Red && _kothRedTimerTicksRemaining > 0)
            {
                _kothRedTimerTicksRemaining -= 1;
            }
            else if (point?.Team == PlayerTeam.Blue && _kothBlueTimerTicksRemaining > 0)
            {
                _kothBlueTimerTicksRemaining -= 1;
            }

            return;
        }

        var redHomePoint = GetDualKothPoint(PlayerTeam.Red);
        var blueHomePoint = GetDualKothPoint(PlayerTeam.Blue);
        if (blueHomePoint?.Team == PlayerTeam.Red && _kothRedTimerTicksRemaining > 0)
        {
            _kothRedTimerTicksRemaining -= 1;
        }

        if (redHomePoint?.Team == PlayerTeam.Blue && _kothBlueTimerTicksRemaining > 0)
        {
            _kothBlueTimerTicksRemaining -= 1;
        }
    }

    private void AdvanceKothMatchState()
    {
        if (MatchState.IsEnded)
        {
            return;
        }

        if (MatchState.TimeRemainingTicks > 0)
        {
            MatchState = MatchState with { TimeRemainingTicks = MatchState.TimeRemainingTicks - 1 };
        }

        var objectiveWinner = ResolveKothObjectiveWinner();
        if (objectiveWinner.HasValue)
        {
            MatchState = MatchState with { Phase = MatchPhase.Ended, WinnerTeam = objectiveWinner };
            QueuePendingMapChange();
            return;
        }

        if (MatchState.TimeRemainingTicks > 0)
        {
            return;
        }

        MatchState = MatchState with
        {
            Phase = MatchPhase.Ended,
            WinnerTeam = GetKothTimerLeader(),
        };
        QueuePendingMapChange();
    }

    private void ApplySnapshotKoth(SnapshotMessage snapshot)
    {
        if (!IsKothMode((GameModeKind)snapshot.GameMode))
        {
            _kothRedTimerTicksRemaining = 0;
            _kothBlueTimerTicksRemaining = 0;
            _kothUnlockTicksRemaining = 0;
            return;
        }

        _kothUnlockTicksRemaining = Math.Max(0, snapshot.KothUnlockTicksRemaining);
        _kothRedTimerTicksRemaining = Math.Max(0, snapshot.KothRedTimerTicksRemaining);
        _kothBlueTimerTicksRemaining = Math.Max(0, snapshot.KothBlueTimerTicksRemaining);
    }

    private ControlPointState? GetSingleKothPoint()
    {
        for (var index = 0; index < _controlPoints.Count; index += 1)
        {
            if (_controlPoints[index].Marker.IsSingleKothControlPoint())
            {
                return _controlPoints[index];
            }
        }

        return _controlPoints.Count > 0 ? _controlPoints[0] : null;
    }

    private ControlPointState? GetDualKothPoint(PlayerTeam homeTeam)
    {
        for (var index = 0; index < _controlPoints.Count; index += 1)
        {
            var marker = _controlPoints[index].Marker;
            if ((homeTeam == PlayerTeam.Red && marker.IsRedKothControlPoint())
                || (homeTeam == PlayerTeam.Blue && marker.IsBlueKothControlPoint()))
            {
                return _controlPoints[index];
            }
        }

        return null;
    }

    private PlayerTeam? ResolveKothObjectiveWinner()
    {
        if (MatchRules.Mode == GameModeKind.KingOfTheHill)
        {
            var point = GetSingleKothPoint();
            if (point is null)
            {
                return null;
            }

            if (_kothRedTimerTicksRemaining <= 0
                && point.Team == PlayerTeam.Red
                && point.CappingTicks <= 0f
                && point.BlueCappers == 0)
            {
                return PlayerTeam.Red;
            }

            if (_kothBlueTimerTicksRemaining <= 0
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
            && _kothRedTimerTicksRemaining <= 0
            && blueHomePoint.Team == PlayerTeam.Red
            && blueHomePoint.CappingTicks <= 0f
            && blueHomePoint.BlueCappers == 0)
        {
            return PlayerTeam.Red;
        }

        if (redHomePoint is not null
            && _kothBlueTimerTicksRemaining <= 0
            && redHomePoint.Team == PlayerTeam.Blue
            && redHomePoint.CappingTicks <= 0f
            && redHomePoint.RedCappers == 0)
        {
            return PlayerTeam.Blue;
        }

        return null;
    }

    private PlayerTeam? GetKothTimerLeader()
    {
        if (_kothRedTimerTicksRemaining < _kothBlueTimerTicksRemaining)
        {
            return PlayerTeam.Red;
        }

        if (_kothBlueTimerTicksRemaining < _kothRedTimerTicksRemaining)
        {
            return PlayerTeam.Blue;
        }

        return null;
    }

    private static PlayerTeam? GetInitialDualKothOwner(RoomObjectMarker marker)
    {
        if (marker.IsRedKothControlPoint())
        {
            return PlayerTeam.Red;
        }

        if (marker.IsBlueKothControlPoint())
        {
            return PlayerTeam.Blue;
        }

        return null;
    }

    private int GetDefaultKothTeamTimerTicks()
    {
        return Config.TicksPerSecond * 180;
    }

    private int GetDefaultKothUnlockTicks()
    {
        return Config.TicksPerSecond * 30;
    }

    private int GetDefaultKothCapTimeTicks()
    {
        return Config.TicksPerSecond * 10;
    }
}
