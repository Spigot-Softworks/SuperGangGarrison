using System.Linq;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed class RuntimeControlPointResolutionController
    {
        private readonly SimulationWorld _world;

        public RuntimeControlPointResolutionController(SimulationWorld world)
        {
            _world = world;
        }

        public void AdvanceResolution()
        {
            if (_world.MatchState.IsEnded)
            {
                return;
            }

            if (_world._controlPointSetupMode && _world._controlPointSetupTicksRemaining > 0)
            {
                _world._controlPointSetupTicksRemaining -= 1;
                var ticksPerSecond = _world.Config.TicksPerSecond;
                if (_world._controlPointSetupTicksRemaining == ticksPerSecond * 6
                    || _world._controlPointSetupTicksRemaining == ticksPerSecond * 5
                    || _world._controlPointSetupTicksRemaining == ticksPerSecond * 4
                    || _world._controlPointSetupTicksRemaining == ticksPerSecond * 3)
                {
                    _world.RegisterWorldSoundEvent("CountDown1Snd", _world.LocalPlayer.X, _world.LocalPlayer.Y);
                }
                else if (_world._controlPointSetupTicksRemaining == ticksPerSecond * 2)
                {
                    _world.RegisterWorldSoundEvent("CountDown2Snd", _world.LocalPlayer.X, _world.LocalPlayer.Y);
                }
                else if (_world._controlPointSetupTicksRemaining == ticksPerSecond)
                {
                    _world.MatchState = _world.MatchState with { TimeRemainingTicks = _world.MatchRules.TimeLimitTicks };
                    _world.RegisterWorldSoundEvent("SirenSnd", _world.LocalPlayer.X, _world.LocalPlayer.Y);
                }
            }

            _world.UpdateControlPointSetupGates();

            if (_world.MatchState.TimeRemainingTicks > 0)
            {
                _world.MatchState = _world.MatchState with { TimeRemainingTicks = _world.MatchState.TimeRemainingTicks - 1 };
            }

            var overtimeActive = _world.MatchState.TimeRemainingTicks <= 0 && _world._controlPoints.Any(point => point.CappingTicks > 0f);
            if (overtimeActive && !_world.MatchState.IsOvertime)
            {
                _world.MatchState = _world.MatchState with { Phase = MatchPhase.Overtime, WinnerTeam = null };
            }

            var winner = ResolveWinner(overtimeActive);
            if (winner.HasValue)
            {
                _world.MatchState = _world.MatchState with { Phase = MatchPhase.Ended, WinnerTeam = winner };
                _world.QueuePendingMapChange();
                return;
            }

            if (_world.MatchState.TimeRemainingTicks <= 0 && !overtimeActive)
            {
                _world.MatchState = _world.MatchState with { Phase = MatchPhase.Ended, WinnerTeam = null };
                _world.QueuePendingMapChange();
            }
            else if (!overtimeActive && _world.MatchState.IsOvertime)
            {
                _world.MatchState = _world.MatchState with { Phase = MatchPhase.Running, WinnerTeam = null };
            }
        }

        private PlayerTeam? ResolveWinner(bool overtimeActive)
        {
            if (_world._controlPoints.Count == 0)
            {
                return null;
            }

            if (!_world._controlPointSetupMode)
            {
                var firstTeam = _world._controlPoints[0].Team;
                var lastTeam = _world._controlPoints[^1].Team;
                if (firstTeam.HasValue && lastTeam.HasValue && firstTeam.Value == lastTeam.Value)
                {
                    return firstTeam.Value;
                }

                return null;
            }

            var finalTeam = _world._controlPoints[^1].Team;
            if (finalTeam == PlayerTeam.Red)
            {
                return PlayerTeam.Red;
            }

            if (_world.MatchState.TimeRemainingTicks <= 0 && !overtimeActive)
            {
                return PlayerTeam.Blue;
            }

            return null;
        }
    }
}
