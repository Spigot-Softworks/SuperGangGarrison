#nullable enable

using System;
using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

internal sealed record HudElementLayout(
    string Id,
    HudAnchor Anchor,
    Vector2 Offset,
    Vector2 Size,
    Vector2 BoundsOffset,
    bool Visible = true,
    bool Locked = false,
    int Layer = 0,
    float Scale = 1f)
{
    public Rectangle ResolveBounds(Vector2 origin)
    {
        var scale = Math.Max(0.01f, Scale);
        return new Rectangle(
            (int)MathF.Round(origin.X + (BoundsOffset.X * scale)),
            (int)MathF.Round(origin.Y + (BoundsOffset.Y * scale)),
            Math.Max(1, (int)MathF.Round(Size.X * scale)),
            Math.Max(1, (int)MathF.Round(Size.Y * scale)));
    }
}

internal sealed class HudElementLayoutOverride
{
    public HudAnchor? Anchor { get; set; }

    public float? OffsetX { get; set; }

    public float? OffsetY { get; set; }

    public float? Scale { get; set; }

    public bool? Visible { get; set; }
}

internal readonly record struct HudResolvedElement(
    HudElementLayout Layout,
    Vector2 Origin,
    Rectangle Bounds);
