namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void UpdateCaptureTheFlagState()
    {
        MatchObjectiveFlowSystem.UpdateCaptureTheFlagState(this);
    }

    private void UpdateArenaState()
    {
        MatchObjectiveFlowSystem.UpdateArenaState(this);
    }

    private void AdvanceMatchState()
    {
        MatchObjectiveFlowSystem.AdvanceMatchState(this);
    }

    private static bool NearlyEqual(float left, float right)
    {
        return MathF.Abs(left - right) <= 0.01f;
    }
}
