using System;

namespace OpenGarrison.Core;

public readonly record struct CustomMapBuilderParallaxLayer(
    int Index,
    string ResourceName,
    float XFactor = 1f,
    float YFactor = 1f,
    bool Visible = true)
{
    public const int MinIndex = 0;
    public const int MaxIndex = 6;

    public CustomMapBuilderParallaxLayer NormalizeForEditing()
    {
        return this with
        {
            Index = Math.Clamp(Index, MinIndex, MaxIndex),
            ResourceName = ResourceName.Trim(),
            XFactor = NormalizeFactor(XFactor),
            YFactor = NormalizeFactor(YFactor),
        };
    }

    private static float NormalizeFactor(float factor)
    {
        return float.IsFinite(factor) ? Math.Clamp(factor, 0f, 10f) : 1f;
    }
}
