namespace OpenGarrison.Core;

/// <summary>
/// Pure geometry helpers shared by the world and the combat systems. Keeping them here lets
/// systems use them without referencing <see cref="SimulationWorld"/>.
/// </summary>
internal static class SimulationMath
{
    public const float PyroAirblastMineSpeedFloor = 28f / 3f;

    public static float DegreesToRadians(float degrees)
    {
        return degrees * (MathF.PI / 180f);
    }

    public static float NormalizeAngleDegrees(float degrees)
    {
        while (degrees < 0f)
        {
            degrees += 360f;
        }

        while (degrees >= 360f)
        {
            degrees -= 360f;
        }

        return degrees;
    }

    public static float DistanceBetween(float x1, float y1, float x2, float y2)
    {
        var deltaX = x2 - x1;
        var deltaY = y2 - y1;
        return MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    public static float GetStabOriginX(StabMaskEntity mask, float directionX)
    {
        return mask.X + directionX * StabMaskEntity.StartOffset;
    }

    public static float GetStabOriginY(StabMaskEntity mask, float directionY)
    {
        return mask.Y + directionY * StabMaskEntity.StartOffset;
    }

    public static float PointDirectionDegrees(float x1, float y1, float x2, float y2)
    {
        var degrees = DeterministicMath.Atan2(y2 - y1, x2 - x1) * (180f / MathF.PI);
        if (degrees < 0f)
        {
            degrees += 360f;
        }

        return degrees;
    }

    public static float PointDirectionRadians(float x1, float y1, float x2, float y2, float fallbackDirectionX)
    {
        var deltaX = x2 - x1;
        var deltaY = y2 - y1;
        if (deltaX == 0f && deltaY == 0f)
        {
            deltaX = fallbackDirectionX == 0f ? 1f : fallbackDirectionX;
        }

        return DeterministicMath.Atan2(deltaY, deltaX);
    }
}