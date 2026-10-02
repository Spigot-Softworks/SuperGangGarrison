namespace OpenGarrison.Core;

// Short names for the shared math helpers, kept for the remaining world partials.
public sealed partial class SimulationWorld
{
    private static float DegreesToRadians(float degrees)
    {
        return SimulationMath.DegreesToRadians(degrees);
    }

    private static float NormalizeAngleDegrees(float degrees)
        => SimulationMath.NormalizeAngleDegrees(degrees);

    private static float DistanceBetween(float x1, float y1, float x2, float y2)
    {
        return SimulationMath.DistanceBetween(x1, y1, x2, y2);
    }

    private static float GetStabOriginX(StabMaskEntity mask, float directionX)
    {
        return SimulationMath.GetStabOriginX(mask, directionX);
    }

    private static float GetStabOriginY(StabMaskEntity mask, float directionY)
    {
        return SimulationMath.GetStabOriginY(mask, directionY);
    }

    private static float PointDirectionDegrees(float x1, float y1, float x2, float y2)
    {
        return SimulationMath.PointDirectionDegrees(x1, y1, x2, y2);
    }

    private static PlayerTeam GetOpposingTeam(PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? PlayerTeam.Red : PlayerTeam.Blue;
    }
}
