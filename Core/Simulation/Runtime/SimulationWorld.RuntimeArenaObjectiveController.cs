namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed class RuntimeArenaObjectiveController
    {
        private readonly RuntimeArenaUpdateController _updateController;
        private readonly RuntimeArenaResolutionController _resolutionController;

        public RuntimeArenaObjectiveController(SimulationWorld world)
        {
            _updateController = new RuntimeArenaUpdateController(world);
            _resolutionController = new RuntimeArenaResolutionController(world);
        }

        public void AdvanceObjectives()
        {
            _updateController.AdvanceObjectives();
        }

        public void AdvanceResolution()
        {
            _resolutionController.AdvanceResolution();
        }
    }
}
