namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed class RuntimeArenaResolutionController
    {
        private readonly SimulationWorld _world;

        public RuntimeArenaResolutionController(SimulationWorld world)
        {
            _world = world;
        }

        public void AdvanceResolution()
        {
            if (_world.MatchState.IsEnded)
            {
                return;
            }

            var redAlive = _world.ArenaRedAliveCount;
            var blueAlive = _world.ArenaBlueAliveCount;
            var redPlayers = _world.ArenaRedPlayerCount;
            var bluePlayers = _world.ArenaBluePlayerCount;

            if (redPlayers > 0 && bluePlayers > 0)
            {
                if (redAlive == 0 && blueAlive > 0)
                {
                    EndArenaRound(PlayerTeam.Blue);
                    return;
                }

                if (blueAlive == 0 && redAlive > 0)
                {
                    EndArenaRound(PlayerTeam.Red);
                    return;
                }
            }

            if (_world.MatchState.TimeRemainingTicks > 0)
            {
                _world.MatchState = _world.MatchState with { TimeRemainingTicks = _world.MatchState.TimeRemainingTicks - 1 };
                if (_world.MatchState.TimeRemainingTicks > 0)
                {
                    return;
                }
            }

            if (redAlive > 0 && blueAlive > 0 && redPlayers > 0 && bluePlayers > 0)
            {
                _world.MatchState = _world.MatchState with { Phase = MatchPhase.Overtime, WinnerTeam = null };
                return;
            }

            _world.MatchState = _world.MatchState with { Phase = MatchPhase.Ended, WinnerTeam = null };
            _world.QueuePendingMapChange();
        }

        private void EndArenaRound(PlayerTeam winner)
        {
            if (_world.MatchState.IsEnded)
            {
                return;
            }

            if (winner == PlayerTeam.Red)
            {
                _world._arenaRedConsecutiveWins += 1;
                _world._arenaBlueConsecutiveWins = 0;
            }
            else
            {
                _world._arenaBlueConsecutiveWins += 1;
                _world._arenaRedConsecutiveWins = 0;
            }

            _world.MatchState = _world.MatchState with { Phase = MatchPhase.Ended, WinnerTeam = winner };
            _world.QueuePendingMapChange();
        }
    }
}
