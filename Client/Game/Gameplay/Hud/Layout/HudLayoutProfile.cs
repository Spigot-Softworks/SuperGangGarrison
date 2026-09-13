#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

internal sealed class HudLayoutProfile
{
    public const float MinElementScale = 0.5f;
    public const float MaxElementScale = 3f;
    public const float MinHudOpacity = 0.2f;
    public const float MaxHudOpacity = 1f;

    public Dictionary<string, HudElementLayoutOverride> Overrides { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, HudElementLayoutOverride> UnknownOverrides { get; } = new(StringComparer.Ordinal);

    private readonly Dictionary<string, HudElementLayout> _runtimeDefaults = new(StringComparer.Ordinal);

    public bool GridVisible { get; set; } = true;

    public bool SnapEnabled { get; set; } = true;

    public int MinorGridSize { get; set; } = 16;

    public int MajorGridSize { get; set; } = 64;

    private float _hudOpacity = MaxHudOpacity;

    public float HudOpacity
    {
        get => _hudOpacity;
        set => _hudOpacity = NormalizeHudOpacity(value);
    }

    public static float NormalizeHudOpacity(float opacity)
    {
        if (float.IsNaN(opacity) || float.IsInfinity(opacity))
        {
            return MaxHudOpacity;
        }

        return Math.Clamp(opacity, MinHudOpacity, MaxHudOpacity);
    }

    public IReadOnlyDictionary<string, HudElementLayout> Defaults { get; } = HudLayoutDefaults.Create();

    public void ClearRuntimeDefaults()
    {
        _runtimeDefaults.Clear();
    }

    public void SetRuntimeDefault(HudElementLayout layout)
    {
        _runtimeDefaults[layout.Id] = layout;

        // Dynamic HUD widgets are not part of the startup defaults, so their
        // saved overrides initially load as unknown. Promote an override as
        // soon as its runtime definition appears; otherwise a hidden dynamic
        // widget cannot be found in the editor after restarting the game.
        if (UnknownOverrides.Remove(layout.Id, out var savedOverride))
        {
            Overrides[layout.Id] = savedOverride;
        }
    }

    public bool TryResolve(string id, int viewportWidth, int viewportHeight, out HudResolvedElement resolved)
    {
        if (!TryGetDefaultLayout(id, out var defaultLayout))
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
        resolved = ResolveElement(layout, origin, viewportWidth, viewportHeight);
        return true;
    }

    public bool TryResolveEvenIfHidden(string id, int viewportWidth, int viewportHeight, out HudResolvedElement resolved)
    {
        if (!TryGetDefaultLayout(id, out var defaultLayout))
        {
            resolved = default;
            return false;
        }

        var layout = ApplyOverride(defaultLayout);
        var origin = HudLayoutResolver.ResolveOrigin(layout.Anchor, layout.Offset, viewportWidth, viewportHeight);
        resolved = ResolveElement(layout, origin, viewportWidth, viewportHeight);
        return true;
    }

    private static HudResolvedElement ResolveElement(HudElementLayout layout, Vector2 origin, int width, int height)
        => layout.Id == HudElementId.ClassEngineerBuildMenu
            ? BuildWheelPresentation.ConstrainToViewport(layout, origin, width, height)
            : new HudResolvedElement(layout, origin, layout.ResolveBounds(origin));

    public void SetElementOrigin(string id, Vector2 origin, int viewportWidth, int viewportHeight)
    {
        if (!TryGetDefaultLayout(id, out var defaultLayout))
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
        UnknownOverrides.Remove(id);
    }

    public bool SetElementScale(string id, float scale)
    {
        if (!TryGetDefaultLayout(id, out var defaultLayout))
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
        UnknownOverrides.Remove(id);
        return true;
    }

    public bool SetElementVisibility(string id, bool visible)
    {
        if (!TryGetDefaultLayout(id, out var defaultLayout))
        {
            return false;
        }

        var current = ApplyOverride(defaultLayout);
        Overrides[id] = new HudElementLayoutOverride
        {
            Anchor = current.Anchor,
            OffsetX = current.Offset.X,
            OffsetY = current.Offset.Y,
            Scale = current.Scale,
            Visible = visible,
        };
        UnknownOverrides.Remove(id);
        return true;
    }

    public void ResetElements()
    {
        Overrides.Clear();
        UnknownOverrides.Clear();
    }

    private HudElementLayout ApplyOverride(HudElementLayout defaultLayout)
    {
        if (!Overrides.TryGetValue(defaultLayout.Id, out var entry)
            && !UnknownOverrides.TryGetValue(defaultLayout.Id, out entry))
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

    private bool TryGetDefaultLayout(string id, out HudElementLayout defaultLayout)
    {
        if (_runtimeDefaults.TryGetValue(id, out defaultLayout!))
        {
            return true;
        }

        return Defaults.TryGetValue(id, out defaultLayout!);
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
