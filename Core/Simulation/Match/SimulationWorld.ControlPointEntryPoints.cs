namespace OpenGarrison.Core;

// The control-point setup/state procedures are still static helpers that take the world, so their entry points stay here.
public sealed partial class SimulationWorld
{
    internal void ResetControlPointStateForNewRound()
    {
        ControlPointSetupSystem.ResetForNewRound(this);
    }

    internal void UpdateControlPointSetupGates()
    {
        ControlPointSetupSystem.UpdateSetupGates(this);
    }

    internal void InitializeControlPointsForLevel(bool evaluateLogicGraph = true)
    {
        ControlPointSetupSystem.InitializeForLevel(this, evaluateLogicGraph);
    }

    internal void UpdateControlPointState()
    {
        ControlPointStateSystem.Update(this);
    }
}