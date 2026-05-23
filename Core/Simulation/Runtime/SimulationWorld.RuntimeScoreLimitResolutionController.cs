namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed class RuntimeScoreLimitResolutionController
    {
        private readonly SimulationWorld _world;

        public RuntimeScoreLimitResolutionController(SimulationWorld world)
        {
            _world = world;
        }

        public void AdvanceResolution()
        {
            if (_world.MatchState.IsEnded)
            {
                return;
            }

            var winner = GetCapLimitWinner();
            if (winner.HasValue)
            {
                _world.TryEndRound(winner, "score_limit");
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

            _world.TryEndRound(GetHigherCapWinner(), "time_limit");
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
