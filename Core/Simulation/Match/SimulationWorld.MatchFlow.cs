namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void UpdateAuxiliaryControlPointStateIfNeeded()
    {
        if (!Level.ShowControlPoints
            || Objectives.ControlPoints.Points.Count == 0
            || MatchRules.Mode is GameModeKind.ControlPoint
                or GameModeKind.Scr
                or GameModeKind.KingOfTheHill
                or GameModeKind.DoubleKingOfTheHill)
        {
            return;
        }

        UpdateControlPointState();
    }

    private static bool NearlyEqual(float left, float right)
    {
        return MathF.Abs(left - right) <= 0.01f;
    }
}
