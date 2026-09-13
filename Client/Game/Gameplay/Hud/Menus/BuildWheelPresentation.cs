#nullable enable

using System;
using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

internal static class BuildWheelPresentation
{
    public const float SpriteCenter = 100f;
    public const float IconRadius = 66f;

    public static Vector2 GetIconOffset(int slot) => slot switch
    {
        1 => new Vector2(0f, -IconRadius),
        2 => new Vector2(IconRadius, 0f),
        3 => new Vector2(0f, IconRadius),
        4 => new Vector2(-IconRadius, 0f),
        _ => Vector2.Zero,
    };

    public static int GetIconFrame(int slot, bool blue, bool exists, bool canBuild)
    {
        var icon = slot switch { 1 => 1, 2 => 2, 3 => 3, _ => 0 };
        return (exists ? (blue ? 26 : 18) : canBuild ? (blue ? 22 : 14) : 30) + icon;
    }

    // Centers of the 32x36 icon tiles in the supplied 201x201 frames.
    // Use the authored positions: loose desktop textures do not always carry
    // opaque-bound metadata, whereas browser atlas frames do.
    public static Vector2 GetIconOrigin(int frameIndex) => frameIndex switch
    {
        30 => new Vector2(165f, 101f),
        31 => new Vector2(100f, 34f),
        32 => new Vector2(36f, 101f),
        33 => new Vector2(100f, 167f),
        _ => new Vector2(100f, 101f),
    };

    public static HudResolvedElement ConstrainToViewport(HudElementLayout layout, Vector2 origin, int width, int height)
    {
        const float margin = 8f;
        const float labelHeight = 16f;
        var scale = MathF.Max(0.01f, MathF.Min(layout.Scale,
            MathF.Min((width - margin * 2) / 201f, (height - margin * 2 - labelHeight) / 210f)));
        // Include the label below the wheel, and its width at small HUD scales.
        var left = MathF.Max(SpriteCenter * scale, 90f) + margin;
        var right = MathF.Max(101f * scale, 90f) + margin;
        var top = SpriteCenter * scale + margin;
        var bottom = 110f * scale + labelHeight + margin;
        origin = new Vector2(
            Math.Clamp(origin.X, left, MathF.Max(left, width - right)),
            Math.Clamp(origin.Y, top, MathF.Max(top, height - bottom)));
        layout = layout with { Scale = scale };
        return new HudResolvedElement(layout, origin, layout.ResolveBounds(origin));
    }
}
