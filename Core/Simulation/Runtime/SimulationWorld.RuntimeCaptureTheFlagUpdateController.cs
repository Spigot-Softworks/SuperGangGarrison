namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed class RuntimeCaptureTheFlagUpdateController
    {
        private readonly SimulationWorld _world;

        public RuntimeCaptureTheFlagUpdateController(SimulationWorld world)
        {
            _world = world;
        }

        public void AdvanceObjectives()
        {
            if (_world.MatchState.IsEnded)
            {
                return;
            }

            var redWasDropped = _world.RedIntel.IsDropped;
            var blueWasDropped = _world.BlueIntel.IsDropped;
            _world.RedIntel.AdvanceTick();
            _world.BlueIntel.AdvanceTick();
            if (redWasDropped && _world.RedIntel.IsAtBase)
            {
                _world.RegisterWorldSoundEvent("IntelDropSnd", _world.RedIntel.X, _world.RedIntel.Y);
                _world.RecordIntelReturnedObjectiveLog(PlayerTeam.Red);
            }

            if (blueWasDropped && _world.BlueIntel.IsAtBase)
            {
                _world.RegisterWorldSoundEvent("IntelDropSnd", _world.BlueIntel.X, _world.BlueIntel.Y);
                _world.RecordIntelReturnedObjectiveLog(PlayerTeam.Blue);
            }

            foreach (var player in _world.EnumerateSimulatedPlayers())
            {
                if (!player.IsAlive)
                {
                    continue;
                }

                _world.TryPickUpEnemyIntel(player);
                _world.TryScoreCarriedIntel(player);
            }
        }
    }
}
