namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed class RuntimeKothObjectiveController
    {
        private readonly SimulationWorld _world;

        public RuntimeKothObjectiveController(SimulationWorld world)
        {
            _world = world;
        }

        public void AdvanceObjectives()
        {
            _world.UpdateControlPointState();
            _world.UpdateKothState();
        }

        public void AdvanceResolution()
        {
            _world.AdvanceKothMatchStateCore();
        }
    }
}
