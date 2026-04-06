namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed class RuntimeControlPointObjectiveController
    {
        private readonly SimulationWorld _world;
        private readonly RuntimeControlPointResolutionController _resolutionController;

        public RuntimeControlPointObjectiveController(SimulationWorld world)
        {
            _world = world;
            _resolutionController = new RuntimeControlPointResolutionController(world);
        }

        public void AdvanceObjectives()
        {
            _world.UpdateControlPointState();
        }

        public void AdvanceResolution()
        {
            _resolutionController.AdvanceResolution();
        }
    }
}
