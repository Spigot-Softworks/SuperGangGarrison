#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

internal sealed class HudLayoutProfile
{
    public const float MinElementScale = 0.5f;
    public const float MaxElementScale = 3f;

    public Dictionary<string, HudElementLayoutOverride> Overrides { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, HudElementLayoutOverride> UnknownOverrides { get; } = new(StringComparer.Ordinal);

    public bool GridVisible { get; set; } = true;

    public bool SnapEnabled { get; set; } = true;

    public int MinorGridSize { get; set; } = 16;

    public int MajorGridSize { get; set; } = 64;

    public IReadOnlyDictionary<string, HudElementLayout> Defaults { get; } = HudLayoutDefaults.Create();

    public bool TryResolve(string id, int viewportWidth, int viewportHeight, out HudResolvedElement resolved)
    {
        if (!Defaults.TryGetValue(id, out var defaultLayout))
        {
            resolved = default;
            return false;
        }

        var layout = ApplyOverride(defaultLayout);
        if (!layout.Visible)
        {
            resolved = default;
            return false;
        }

        var origin = HudLayoutResolver.ResolveOrigin(layout.Anchor, layout.Offset, viewportWidth, viewportHeight);
        resolved = new HudResolvedElement(layout, origin, layout.ResolveBounds(origin));
        return true;
    }

    public bool TryResolveEvenIfHidden(string id, int viewportWidth, int viewportHeight, out HudResolvedElement resolved)
    {
        if (!Defaults.TryGetValue(id, out var defaultLayout))
        {
            resolved = default;
            return false;
        }

        var layout = ApplyOverride(defaultLayout);
        var origin = HudLayoutResolver.ResolveOrigin(layout.Anchor, layout.Offset, viewportWidth, viewportHeight);
        resolved = new HudResolvedElement(layout, origin, layout.ResolveBounds(origin));
        return true;
    }

    public void SetElementOrigin(string id, Vector2 origin, int viewportWidth, int viewportHeight)
    {
        if (!Defaults.TryGetValue(id, out var defaultLayout))
        {
            return;
        }

        var current = ApplyOverride(defaultLayout);
        var offset = HudLayoutResolver.OffsetFromOrigin(current.Anchor, origin, viewportWidth, viewportHeight);
        Overrides[id] = new HudElementLayoutOverride
        {
            Anchor = current.Anchor,
            OffsetX = offset.X,
            OffsetY = offset.Y,
            Scale = current.Scale,
            Visible = current.Visible,
        };
    }

    public bool SetElementScale(string id, float scale)
    {
        if (!Defaults.TryGetValue(id, out var defaultLayout))
        {
            return false;
        }

        var current = ApplyOverride(defaultLayout);
        Overrides[id] = new HudElementLayoutOverride
        {
            Anchor = current.Anchor,
            OffsetX = current.Offset.X,
            OffsetY = current.Offset.Y,
            Scale = NormalizeElementScale(scale),
            Visible = current.Visible,
        };
        return true;
    }

    public void ResetElements()
    {
        Overrides.Clear();
    }

    private HudElementLayout ApplyOverride(HudElementLayout defaultLayout)
    {
        if (!Overrides.TryGetValue(defaultLayout.Id, out var entry))
        {
            return defaultLayout;
        }

        return defaultLayout with
        {
            Anchor = entry.Anchor ?? defaultLayout.Anchor,
            Offset = new Vector2(entry.OffsetX ?? defaultLayout.Offset.X, entry.OffsetY ?? defaultLayout.Offset.Y),
            Scale = NormalizeElementScale(entry.Scale ?? defaultLayout.Scale),
            Visible = entry.Visible ?? defaultLayout.Visible,
        };
    }

    private static float NormalizeElementScale(float scale)
    {
        if (float.IsNaN(scale) || float.IsInfinity(scale))
        {
            return 1f;
        }

        return Math.Clamp(scale, MinElementScale, MaxElementScale);
    }
}
