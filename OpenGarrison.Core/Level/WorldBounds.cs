using System;

namespace OpenGarrison.Core;

public readonly record struct WorldBounds(float Width, float Height)
{
    public float ClampX(float x, float entityWidth)
    {
        var halfWidth = entityWidth / 2f;
        return Math.Clamp(x, halfWidth, Width - halfWidth);
    }

    public float ClampY(float y, float entityHeight)
    {
        var halfHeight = entityHeight / 2f;
        return Math.Clamp(y, halfHeight, Height - halfHeight);
    }
}
