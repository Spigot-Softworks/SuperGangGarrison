namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed class RuntimeController
    {
        private readonly SimulationWorld _world;
        private readonly RuntimePhaseController _phaseController;

        public RuntimeController(SimulationWorld world)
        {
            _world = world;
            _phaseController = new RuntimePhaseController(world);
        }

        public void AdvanceOneTick()
        {
            if (_world.AdvancePendingMapChange())
            {
                _world.Frame += 1;
                return;
            }

            _phaseController.AdvancePrePlayerSimulationPhase();
            _phaseController.AdvancePlayerSimulationPhase();
            _phaseController.AdvancePostPlayerSimulationPhase();
            _world._previousLocalInput = _world._localInput;
            _world.Frame += 1;
        }

        public void AdvanceLegacyMatchState()
        {
            _phaseController.AdvanceLegacyMatchState();
        }

        public void AdvanceLegacyControlPointMatchState()
        {
            _phaseController.AdvanceLegacyControlPointMatchState();
        }

        public void AdvanceLegacyKothMatchState()
        {
            _phaseController.AdvanceLegacyKothMatchState();
        }

        public void AdvanceLegacyGeneratorMatchState()
        {
            _phaseController.AdvanceLegacyGeneratorMatchState();
        }

        public void AdvanceLegacyCaptureTheFlagState()
        {
            _phaseController.AdvanceLegacyCaptureTheFlagState();
        }

        public void AdvanceLegacyArenaState()
        {
            _phaseController.AdvanceLegacyArenaState();
        }

    }
}
