using System;

namespace OpenGarrison.Core;

public static class RadialWheelSelection
{
    public const float CenterRadius = 30f;

    // Angles are clockwise from up. Slot zero is the neutral center.
    public static int GetSlot(float directionDegrees, float distance, int sectors, float rotationDegrees = 0f)
    {
        if (distance < CenterRadius || !float.IsFinite(distance) || !float.IsFinite(directionDegrees) || sectors <= 0)
            return 0;
        var angle = ((directionDegrees + rotationDegrees) % 360f + 360f) % 360f;
        return Math.Clamp((int)(angle / (360f / sectors)) + 1, 1, sectors);
    }
}
