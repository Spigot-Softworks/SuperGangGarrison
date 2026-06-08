namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed class RuntimeScrResolutionController
    {
        private readonly SimulationWorld _world;

        public RuntimeScrResolutionController(SimulationWorld world)
        {
            _world = world;
        }

        public void AdvanceResolution()
        {
            if (_world.MatchState.IsEnded)
            {
                return;
            }

            if (_world.TryEvaluateScrThresholdCrossing(isRoundStart: false))
            {
                return;
            }

            if (_world.MatchState.TimeRemainingTicks > 0)
            {
                _world.MatchState = _world.MatchState with
                {
                    TimeRemainingTicks = _world.MatchState.TimeRemainingTicks - 1,
                };
                if (_world.MatchState.TimeRemainingTicks > 0)
                {
                    _world.UpdateScrQualificationTracking();
                    return;
                }
            }

            _world.TryEndRound(
                _world.Level.ScrSettings.ResolveRoundEndWinner(_world.RedCaps, _world.BlueCaps),
                "scr_time_limit");
        }
    }
}
