namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed class RuntimeArenaUpdateController
    {
        private readonly SimulationWorld _world;

        public RuntimeArenaUpdateController(SimulationWorld world)
        {
            _world = world;
        }

        public void AdvanceObjectives()
        {
            if (_world.MatchState.IsEnded)
            {
                return;
            }

            if (_world._arenaUnlockTicksRemaining > 0)
            {
                _world._arenaUnlockTicksRemaining -= 1;
            }

            var redCappers = _world.CountPlayersInArenaCaptureZone(PlayerTeam.Red);
            var blueCappers = _world.CountPlayersInArenaCaptureZone(PlayerTeam.Blue);
            var defended = redCappers > 0 && blueCappers > 0;
            PlayerTeam? capTeam = null;
            var cappers = 0;

            if (redCappers > 0 && blueCappers == 0 && _world._arenaPointTeam != PlayerTeam.Red)
            {
                capTeam = PlayerTeam.Red;
                cappers = redCappers;
            }
            else if (blueCappers > 0 && redCappers == 0 && _world._arenaPointTeam != PlayerTeam.Blue)
            {
                capTeam = PlayerTeam.Blue;
                cappers = blueCappers;
            }

            if (_world._arenaCappingTicks > 0f && _world._arenaCappingTeam != capTeam)
            {
                cappers = 0;
            }
            else if (_world._arenaPointTeam.HasValue && capTeam == _world._arenaPointTeam.Value)
            {
                cappers = 0;
            }

            _world._arenaCappers = cappers;

            var capStrength = 0f;
            for (var index = 1; index <= cappers; index += 1)
            {
                capStrength += index <= 2 ? 1f : 0.5f;
            }

            if (_world._arenaUnlockTicksRemaining > 0)
            {
                _world._arenaCappingTicks = 0f;
                _world._arenaCappingTeam = null;
                return;
            }

            if (capTeam.HasValue && cappers > 0 && _world._arenaCappingTicks < ArenaPointCapTimeTicksDefault)
            {
                _world._arenaCappingTicks += capStrength;
                _world._arenaCappingTeam = capTeam;
            }
            else if (_world._arenaCappingTicks > 0f && cappers == 0 && !defended)
            {
                _world._arenaCappingTicks -= 1f;
                if (_world._arenaPointTeam == PlayerTeam.Blue)
                {
                    _world._arenaCappingTicks -= blueCappers * 0.5f;
                }
                else if (_world._arenaPointTeam == PlayerTeam.Red)
                {
                    _world._arenaCappingTicks -= redCappers * 0.5f;
                }
            }

            if (_world._arenaCappingTicks <= 0f)
            {
                _world._arenaCappingTicks = 0f;
                _world._arenaCappingTeam = null;
                return;
            }

            if (_world._arenaCappingTicks >= ArenaPointCapTimeTicksDefault && _world._arenaCappingTeam.HasValue)
            {
                _world._arenaPointTeam = _world._arenaCappingTeam.Value;
                _world.MatchState = _world.MatchState with { Phase = MatchPhase.Ended, WinnerTeam = _world._arenaPointTeam.Value };
                if (_world._arenaPointTeam.Value == PlayerTeam.Red)
                {
                    _world._arenaRedConsecutiveWins += 1;
                    _world._arenaBlueConsecutiveWins = 0;
                }
                else
                {
                    _world._arenaBlueConsecutiveWins += 1;
                    _world._arenaRedConsecutiveWins = 0;
                }

                _world.QueuePendingMapChange();
            }
        }
    }
}
