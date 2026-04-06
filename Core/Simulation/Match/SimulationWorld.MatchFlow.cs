namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void UpdateCaptureTheFlagState()
    {
        _runtimeController.AdvanceLegacyCaptureTheFlagState();
    }

    private void UpdateArenaState()
    {
        _runtimeController.AdvanceLegacyArenaState();
    }

    private void AdvanceMatchState()
    {
        _runtimeController.AdvanceLegacyMatchState();
    }

    private static bool NearlyEqual(float left, float right)
    {
        return MathF.Abs(left - right) <= 0.01f;
    }
}
