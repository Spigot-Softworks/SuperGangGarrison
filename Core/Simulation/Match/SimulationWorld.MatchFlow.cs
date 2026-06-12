namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void UpdateCaptureTheFlagState()
    {
        _runtimeController.AdvanceLegacyCaptureTheFlagState();
    }

    private void UpdateScrState()
    {
        _runtimeController.AdvanceLegacyScrState();
    }

    private void UpdateAuxiliaryControlPointStateIfNeeded()
    {
        if (!Level.ShowControlPoints
            || _controlPoints.Count == 0
            || MatchRules.Mode is GameModeKind.ControlPoint
                or GameModeKind.Scr
                or GameModeKind.KingOfTheHill
                or GameModeKind.DoubleKingOfTheHill)
        {
            return;
        }

        UpdateControlPointState();
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
