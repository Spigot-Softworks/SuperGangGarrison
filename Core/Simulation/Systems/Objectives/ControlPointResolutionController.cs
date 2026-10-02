namespace OpenGarrison.Core;

internal sealed class ControlPointResolutionController
{
    private readonly IMatchObjectiveHost _host;

    public ControlPointResolutionController(IMatchObjectiveHost host)
    {
        _host = host;
    }

    public void AdvanceResolution()
    {
        if (_host.MatchState.IsEnded)
        {
            return;
        }

        if (_host.ShouldDeferVipObjectiveResolution())
        {
            return;
        }

        if (_host.Objectives.ControlPoints.SetupMode && _host.Objectives.ControlPoints.SetupTicksRemaining > 0)
        {
            _host.Objectives.ControlPoints.SetupTicksRemaining -= 1;
            var ticksPerSecond = _host.Config.TicksPerSecond;
            if (_host.Objectives.ControlPoints.SetupTicksRemaining == ticksPerSecond * 6
                || _host.Objectives.ControlPoints.SetupTicksRemaining == ticksPerSecond * 5
                || _host.Objectives.ControlPoints.SetupTicksRemaining == ticksPerSecond * 4
                || _host.Objectives.ControlPoints.SetupTicksRemaining == ticksPerSecond * 3)
            {
                _host.RegisterWorldSoundEvent("CountDown1Snd", _host.LocalPlayer.X, _host.LocalPlayer.Y);
            }
            else if (_host.Objectives.ControlPoints.SetupTicksRemaining == ticksPerSecond * 2)
            {
                _host.RegisterWorldSoundEvent("CountDown2Snd", _host.LocalPlayer.X, _host.LocalPlayer.Y);
            }
            else if (_host.Objectives.ControlPoints.SetupTicksRemaining == ticksPerSecond)
            {
                _host.ApplyControlPointSetupMatchRules();
                _host.MatchState = _host.MatchState with { TimeRemainingTicks = _host.MatchRules.TimeLimitTicks };
                _host.RegisterWorldSoundEvent("SirenSnd", _host.LocalPlayer.X, _host.LocalPlayer.Y);
            }
        }

        _host.UpdateControlPointSetupGates();

        if (_host.MatchState.TimeRemainingTicks > 0)
        {
            _host.MatchState = _host.MatchState with { TimeRemainingTicks = _host.MatchState.TimeRemainingTicks - 1 };
        }

        var overtimeActive = _host.MatchState.TimeRemainingTicks <= 0 && _host.Objectives.ControlPoints.Points.Any(point => point.CappingTicks > 0f);
        if (overtimeActive && !_host.MatchState.IsOvertime)
        {
            _host.MatchState = _host.MatchState with { Phase = MatchPhase.Overtime, WinnerTeam = null };
        }

        var winner = ResolveWinner(overtimeActive);
        if (winner.HasValue)
        {
            _host.TryEndRound(winner, "control_point_objective");
            return;
        }

        if (_host.MatchState.TimeRemainingTicks <= 0 && !overtimeActive)
        {
            _host.TryEndRound(null, "control_point_time_limit");
        }
        else if (!overtimeActive && _host.MatchState.IsOvertime)
        {
            _host.MatchState = _host.MatchState with { Phase = MatchPhase.Running, WinnerTeam = null };
        }
    }

    private PlayerTeam? ResolveWinner(bool overtimeActive)
    {
        if (_host.Objectives.ControlPoints.Points.Count == 0)
        {
            return null;
        }

        if (!_host.Objectives.ControlPoints.SetupMode)
        {
            var firstTeam = _host.Objectives.ControlPoints.Points[0].Team;
            var lastTeam = _host.Objectives.ControlPoints.Points[^1].Team;
            if (firstTeam.HasValue && lastTeam.HasValue && firstTeam.Value == lastTeam.Value)
            {
                return firstTeam.Value;
            }

            return null;
        }

        var finalTeam = _host.Objectives.ControlPoints.Points[^1].Team;
        if (finalTeam == PlayerTeam.Red)
        {
            return PlayerTeam.Red;
        }

        if (_host.MatchState.TimeRemainingTicks <= 0 && !overtimeActive)
        {
            return PlayerTeam.Blue;
        }

        return null;
    }
}
