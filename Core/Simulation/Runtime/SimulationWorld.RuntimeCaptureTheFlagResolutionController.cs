namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed class RuntimeCaptureTheFlagResolutionController
    {
        private readonly SimulationWorld _world;

        public RuntimeCaptureTheFlagResolutionController(SimulationWorld world)
        {
            _world = world;
        }

        public void AdvanceResolution()
        {
            if (_world.MatchState.IsEnded)
            {
                return;
            }

            var capWinner = GetCapLimitWinner();
            if (capWinner.HasValue)
            {
                _world.MatchState = _world.MatchState with { Phase = MatchPhase.Ended, WinnerTeam = capWinner };
                _world.QueuePendingMapChange();
                return;
            }

            if (_world.MatchState.Phase == MatchPhase.Overtime)
            {
                if (!AreObjectivesSettled())
                {
                    return;
                }

                _world.MatchState = _world.MatchState with
                {
                    Phase = MatchPhase.Ended,
                    WinnerTeam = GetHigherCapWinner(),
                };
                _world.QueuePendingMapChange();
                return;
            }

            if (_world.MatchState.TimeRemainingTicks > 0)
            {
                _world.MatchState = _world.MatchState with { TimeRemainingTicks = _world.MatchState.TimeRemainingTicks - 1 };
                if (_world.MatchState.TimeRemainingTicks > 0)
                {
                    return;
                }
            }

            if (AreObjectivesSettled())
            {
                _world.MatchState = _world.MatchState with { Phase = MatchPhase.Ended, WinnerTeam = GetHigherCapWinner() };
                _world.QueuePendingMapChange();
                return;
            }

            _world.MatchState = _world.MatchState with { Phase = MatchPhase.Overtime, WinnerTeam = null };
        }

        private bool AreObjectivesSettled()
        {
            return _world.IsIntelAtHome(_world.RedIntel) && _world.IsIntelAtHome(_world.BlueIntel);
        }

        private PlayerTeam? GetCapLimitWinner()
        {
            if (_world.RedCaps >= _world.MatchRules.CapLimit)
            {
                return PlayerTeam.Red;
            }

            if (_world.BlueCaps >= _world.MatchRules.CapLimit)
            {
                return PlayerTeam.Blue;
            }

            return null;
        }

        private PlayerTeam? GetHigherCapWinner()
        {
            if (_world.RedCaps > _world.BlueCaps)
            {
                return PlayerTeam.Red;
            }

            if (_world.BlueCaps > _world.RedCaps)
            {
                return PlayerTeam.Blue;
            }

            return null;
        }
    }
}
