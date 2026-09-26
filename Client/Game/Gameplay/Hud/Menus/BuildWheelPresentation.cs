#nullable enable

using System;
using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

internal static class BuildWheelPresentation
{
    public const float SpriteCenter = 100f;
    public const float IconRadius = 66f;
    public const int SlotCount = 3;
    public const float SelectionRotationDegrees = 60f;

    // 32x36 list-menu icons are drawn from their top-left.
    public static readonly Vector2 IconTopLeftOrigin = new(16f, 18f);

    public static Vector2 GetIconOffset(int slot)
    {
        // Slot 1 top, 2 bottom-left, 3 bottom-right — matching list-menu digit order.
        var angleDegrees = slot switch
        {
            1 => 0f,
            2 => 240f,
            3 => 120f,
            _ => 0f,
        };
        var radians = angleDegrees * (MathF.PI / 180f);
        return new Vector2(MathF.Sin(radians) * IconRadius, -MathF.Cos(radians) * IconRadius);
    }

    public static int GetChromeFrame(int slot, bool selected)
    {
        // Frames: 1 center, 2 top, 3 bottom-right, 4 bottom-left; +4 when selected.
        var baseFrame = slot switch
        {
            1 => 2,
            2 => 4,
            3 => 3,
            _ => 1,
        };
        return baseFrame + (selected ? 4 : 0);
    }

    public static string GetIconSpriteName(int slot) => slot switch
    {
        1 => "BuildMenuSentryIconS",
        2 => "BuildMenuDispenserIconS",
        3 => "BuildMenuJumpPadIconS",
        _ => "BuildMenuSentryIconS",
    };

    public static string GetSlotLabel(int slot) => slot switch
    {
        1 => "Sentry",
        2 => "Dispenser",
        3 => "Jump pad",
        _ => "Cancel",
    };

    /// <summary>
    /// Mouse angle is clockwise from up. Equal 120° wedges with the top sector centered on up.
    /// Remaps clockwise raw indices so digits match the list menu: 1 top, 2 bottom-left, 3 bottom-right.
    /// </summary>
    public static int GetSlotFromPointer(float directionDegrees, float distance)
    {
        var raw = RadialWheelSelection.GetSlot(
            directionDegrees,
            distance,
            SlotCount,
            SelectionRotationDegrees);
        return raw switch
        {
            1 => 1,
            2 => 3,
            3 => 2,
            _ => 0,
        };
    }

    public static HudResolvedElement ConstrainToViewport(HudElementLayout layout, Vector2 origin, int width, int height)
    {
        const float margin = 8f;
        const float labelHeight = 16f;
        var scale = MathF.Max(0.01f, MathF.Min(layout.Scale,
            MathF.Min((width - margin * 2) / 201f, (height - margin * 2 - labelHeight) / 210f)));
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
