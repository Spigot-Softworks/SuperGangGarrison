#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>
/// Builder conveniences for lights: a colour swatch and palette on colour rows, one-click
/// presets (lamp, siren, laser), and drag handles on a selected light
/// for its aim and sweep arc.
/// </summary>
public partial class Game1
{
    /// <summary>Pseudo property row (never saved) that applies the next light preset.</summary>
    private const string GarrisonBuilderLightPresetKey = "$lightPreset";

    private const int GarrisonBuilderLightPaletteColumns = 6;
    private const int GarrisonBuilderLightPaletteCell = 22;

    private static readonly (string Name, string Hex)[] GarrisonBuilderLightPalette =
    [
        ("Warm white", "ffcf80"),
        ("Tungsten", "ffb46b"),
        ("Sodium", "ff9a2e"),
        ("Candle", "ff7a30"),
        ("Moonlight", "a8c4ff"),
        ("Daylight", "f4f0e0"),
        ("Emergency red", "ff2020"),
        ("Emergency blue", "2040ff"),
        ("Amber", "ffa000"),
        ("Laser green", "20ff40"),
        ("Laser red", "ff2010"),
        ("Laser blue", "2080ff"),
        ("Magenta", "ff40c0"),
        ("Cyan", "40e0ff"),
        ("Violet", "9040ff"),
        ("Acid green", "a0ff20"),
        ("Fire", "ff5010"),
        ("White", "ffffff"),
    ];

    /// <summary>Three base looks applied after resetting the previous light's visual settings.</summary>
    private static readonly (string Name, (string Key, string Value)[] Values)[] GarrisonBuilderLightPresets =
    [
        ("Lamp", new (string, string)[]
        {
            (MapLightMetadata.KindKey, "lamp"), (MapLightMetadata.ColorKey, "ffcf80"),
            (MapLightMetadata.RadiusKey, "160"), (MapLightMetadata.IntensityKey, "100"),
        }),
        ("Siren", new (string, string)[]
        {
            (MapLightMetadata.KindKey, "lamp"), (MapLightMetadata.ColorKey, "ff2020"), (MapLightMetadata.AltColorKey, "2040ff"),
            (MapLightMetadata.FlickerKey, "siren"), (MapLightMetadata.SirenStyleKey, "beacon"), (MapLightMetadata.SpeedKey, "90"),
            (MapLightMetadata.RadiusKey, "240"), (MapLightMetadata.IntensityKey, "150"),
        }),
        ("Laser", new (string, string)[]
        {
            (MapLightMetadata.KindKey, "laser"), (MapLightMetadata.ColorKey, "20ff40"),
            (MapLightMetadata.WidthKey, "2"), (MapLightMetadata.RadiusKey, "600"), (MapLightMetadata.HazeKey, "100"),
        }),
    ];

    private int _builderLightPresetIndex = -1;
    private string? _builderLightPaletteKey;
    private Rectangle _builderLightPaletteAnchor;

    private enum GarrisonBuilderLightHandle
    {
        None,
        Aim,
        ArcStart,
        ArcEnd,
    }

    private GarrisonBuilderLightHandle _builderLightHandle = GarrisonBuilderLightHandle.None;
    private int _builderLightHandleEntityIndex = -1;

    private static string GetGarrisonBuilderLightColorName(string key) => key switch
    {
        _ when key.Equals(MapLightMetadata.AltColorKey, StringComparison.OrdinalIgnoreCase) => "secondary colour",
        _ when key.Equals(MapLightMetadata.AltColor2Key, StringComparison.OrdinalIgnoreCase) => "tertiary colour",
        _ => "primary colour",
    };

    private static bool IsGarrisonBuilderLightColorKey(string key) =>
        key.Equals(MapLightMetadata.ColorKey, StringComparison.OrdinalIgnoreCase)
        || key.Equals(MapLightMetadata.AltColorKey, StringComparison.OrdinalIgnoreCase)
        || key.Equals(MapLightMetadata.AltColor2Key, StringComparison.OrdinalIgnoreCase);

    // ---- Presets ----------------------------------------------------------------

    /// <summary>Applies the next preset to the light being edited.</summary>
    private void ApplyNextGarrisonBuilderLightPreset()
    {
        _builderLightPresetIndex = (_builderLightPresetIndex + 1) % GarrisonBuilderLightPresets.Length;
        var (name, values) = GarrisonBuilderLightPresets[_builderLightPresetIndex];
        // Reset the previous look so music, motion and damage cannot leak between presets.
        CustomMapBuilderEntityCatalog.TryGetDefinition(MapLightMetadata.EntityType, out var definition);
        foreach (var pair in definition.DefaultProperties)
        {
            if (!MapLightMetadata.IsLogicInputKey(pair.Key)
                && pair.Key != MapLightMetadata.AltColorKey && pair.Key != MapLightMetadata.AltColor2Key && pair.Key != MapLightMetadata.ColorActionKey
                && pair.Key != MapLightMetadata.MaskKey && pair.Key != MapLightMetadata.MaskGlowRadiusKey && pair.Key != MapLightMetadata.StartOnKey)
                _builderPropertyEditorValues[pair.Key] = pair.Value;
        }
        foreach (var (key, value) in values)
        {
            _builderPropertyEditorValues[key] = value;
        }

        ApplyGarrisonBuilderPropertyEditorLivePreview();
        MarkGarrisonBuilderPropertyEditorChanged();
        _builderStatus = $"applied preset: {name}";
    }

    private string FormatGarrisonBuilderLightPresetLabel()
    {
        var next = GarrisonBuilderLightPresets[(_builderLightPresetIndex + 1) % GarrisonBuilderLightPresets.Length].Name;
        return $"Apply preset: {next}";
    }

    // ---- Colour swatch and palette ----------------------------------------------

    private static Rectangle GetGarrisonBuilderLightSwatchBounds(Rectangle rowBounds)
    {
        const int width = 22;
        const int height = 14;
        return new Rectangle(rowBounds.Right - 8 - width, rowBounds.Y + Math.Max(2, (rowBounds.Height - height) / 2), width, height);
    }

    /// <summary>The colour of a colour row as a swatch at its right end (crossed out when none).</summary>
    private void DrawGarrisonBuilderLightColorSwatch(Rectangle rowBounds, string key, string value, MouseState mouse)
    {
        if (!IsGarrisonBuilderLightColorKey(key) || !IsEditingGarrisonBuilderLightEntity())
        {
            return;
        }

        var swatch = GetGarrisonBuilderLightSwatchBounds(rowBounds);
        var hovered = swatch.Contains(mouse.Position);
        _spriteBatch.Draw(_pixel, swatch, hovered ? Color.White : new Color(20, 18, 16));
        var inner = new Rectangle(swatch.X + 1, swatch.Y + 1, swatch.Width - 2, swatch.Height - 2);
        if (MapLightingColor.TryParse(value, out var color))
        {
            _spriteBatch.Draw(_pixel, inner, new Color(color.R, color.G, color.B));
        }
        else
        {
            _spriteBatch.Draw(_pixel, inner, new Color(60, 56, 52));
            for (var step = 0; step < inner.Width; step += 1)
            {
                var y = inner.Y + (int)(step * (inner.Height - 1) / (float)Math.Max(1, inner.Width - 1));
                _spriteBatch.Draw(_pixel, new Rectangle(inner.X + step, y, 1, 1), new Color(200, 70, 60));
            }
        }
    }

    /// <summary>A click on a colour row's swatch opens the palette (the rest of the row still edits the hex).</summary>
    private bool TryHandleGarrisonBuilderLightColorRowClick(string key, Rectangle rowBounds, Point position)
    {
        if (!_builderUseModernUi
            || !IsGarrisonBuilderLightColorKey(key)
            || !IsEditingGarrisonBuilderLightEntity()
            || !ContainsWithMargin(GetGarrisonBuilderLightSwatchBounds(rowBounds), position, 4))
        {
            return false;
        }

        _builderLightPaletteKey = key;
        _builderLightPaletteAnchor = rowBounds;
        _builderStatus = $"select a {GetGarrisonBuilderLightColorName(key)}";
        return true;
    }

    private static bool ContainsWithMargin(Rectangle bounds, Point position, int margin) =>
        new Rectangle(bounds.X - margin, bounds.Y - margin, bounds.Width + (margin * 2), bounds.Height + (margin * 2)).Contains(position);

    private bool HasGarrisonBuilderLightPaletteEntryForNone =>
        _builderLightPaletteKey is not null && !_builderLightPaletteKey.Equals(MapLightMetadata.ColorKey, StringComparison.OrdinalIgnoreCase);

    private Rectangle GetGarrisonBuilderLightPaletteBounds()
    {
        var cells = GarrisonBuilderLightPalette.Length + (HasGarrisonBuilderLightPaletteEntryForNone ? 1 : 0);
        var rows = (cells + GarrisonBuilderLightPaletteColumns - 1) / GarrisonBuilderLightPaletteColumns;
        var width = (GarrisonBuilderLightPaletteColumns * GarrisonBuilderLightPaletteCell) + 12;
        var height = (rows * GarrisonBuilderLightPaletteCell) + 30;
        var x = Math.Clamp(_builderLightPaletteAnchor.Right - width, 4, Math.Max(4, BuilderViewportWidth - width - 4));
        var y = _builderLightPaletteAnchor.Bottom + 2;
        if (y + height > BuilderViewportHeight - 4)
        {
            y = Math.Max(4, _builderLightPaletteAnchor.Y - height - 2);
        }

        return new Rectangle(x, y, width, height);
    }

    private Rectangle GetGarrisonBuilderLightPaletteCellBounds(Rectangle palette, int cell)
    {
        var column = cell % GarrisonBuilderLightPaletteColumns;
        var row = cell / GarrisonBuilderLightPaletteColumns;
        return new Rectangle(
            palette.X + 6 + (column * GarrisonBuilderLightPaletteCell),
            palette.Y + 24 + (row * GarrisonBuilderLightPaletteCell),
            GarrisonBuilderLightPaletteCell - 3,
            GarrisonBuilderLightPaletteCell - 3);
    }

    /// <summary>Clicks while the palette is open. Returns true when it consumed the click.</summary>
    private bool TryHandleGarrisonBuilderLightPaletteClick(Point position, bool leftClick)
    {
        if (_builderLightPaletteKey is null)
        {
            return false;
        }

        var key = _builderLightPaletteKey;
        if (!IsEditingGarrisonBuilderLightEntity())
        {
            _builderLightPaletteKey = null;
            return false;
        }

        var palette = GetGarrisonBuilderLightPaletteBounds();
        if (!leftClick || !palette.Contains(position))
        {
            _builderLightPaletteKey = null;
            _builderStatus = "colour selection cancelled";
            return true;
        }

        var cells = GarrisonBuilderLightPalette.Length + (HasGarrisonBuilderLightPaletteEntryForNone ? 1 : 0);
        for (var cell = 0; cell < cells; cell += 1)
        {
            if (!GetGarrisonBuilderLightPaletteCellBounds(palette, cell).Contains(position))
            {
                continue;
            }

            var none = cell >= GarrisonBuilderLightPalette.Length;
            _builderPropertyEditorValues[key] = none ? string.Empty : GarrisonBuilderLightPalette[cell].Hex;
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
            _builderStatus = none
                ? $"{GetGarrisonBuilderLightColorName(key)} cleared"
                : $"{GetGarrisonBuilderLightColorName(key)}: {GarrisonBuilderLightPalette[cell].Name.ToLowerInvariant()}";
            _builderLightPaletteKey = null;
            return true;
        }

        return true;
    }

    private void DrawGarrisonBuilderLightPalette(MouseState mouse)
    {
        if (_builderLightPaletteKey is null || !_builderUseModernUi)
        {
            return;
        }

        if (!IsEditingGarrisonBuilderLightEntity())
        {
            _builderLightPaletteKey = null;
            return;
        }

        var palette = GetGarrisonBuilderLightPaletteBounds();
        DrawMenuPanelBackdrop(palette, 0.98f);
        var textScale = GetModernBuilderTextScale(0.72f);
        var hoveredName = "Colour palette";
        var cells = GarrisonBuilderLightPalette.Length + (HasGarrisonBuilderLightPaletteEntryForNone ? 1 : 0);
        for (var cell = 0; cell < cells; cell += 1)
        {
            var bounds = GetGarrisonBuilderLightPaletteCellBounds(palette, cell);
            var hovered = bounds.Contains(mouse.Position);
            _spriteBatch.Draw(_pixel, bounds, hovered ? Color.White : new Color(20, 18, 16));
            var inner = new Rectangle(bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 4);
            if (cell >= GarrisonBuilderLightPalette.Length)
            {
                _spriteBatch.Draw(_pixel, inner, new Color(60, 56, 52));
                for (var step = 0; step < inner.Width; step += 1)
                {
                    _spriteBatch.Draw(_pixel, new Rectangle(inner.X + step, inner.Y + (step * inner.Height / Math.Max(1, inner.Width)), 1, 1), new Color(200, 70, 60));
                }

                if (hovered)
                {
                    hoveredName = "None (clear)";
                }

                continue;
            }

            MapLightingColor.TryParse(GarrisonBuilderLightPalette[cell].Hex, out var color);
            _spriteBatch.Draw(_pixel, inner, new Color(color.R, color.G, color.B));
            if (hovered)
            {
                hoveredName = $"{GarrisonBuilderLightPalette[cell].Name}  #{GarrisonBuilderLightPalette[cell].Hex.ToUpperInvariant()}";
            }
        }

        DrawBitmapFontText(hoveredName, new Vector2(palette.X + 6f, palette.Y + 5f), new Color(230, 220, 200), textScale);
    }

    // ---- Aim and sweep-arc handles --------------------------------------------------

    private bool TryGetGarrisonBuilderSelectedLight(out int index, out MapLightMarker light)
    {
        index = _builderSelectedEntityIndex;
        light = default;
        if (index < 0 || index >= _builderEntities.Count || !MapLightMetadata.IsLightEntityType(_builderEntities[index].Type))
        {
            return false;
        }

        var entity = _builderEntities[index];
        light = MapLightMetadata.FromProperties(entity.X, entity.Y, entity.Properties);
        return true;
    }

    /// <summary>Whether a light points somewhere (so it has an aim to drag).</summary>
    private static bool HasGarrisonBuilderLightAim(in MapLightMarker light) =>
        light.Style.Kind != MapLightKind.Lamp || MapLightMetadata.IsDirectional(light.Spread);

    private static bool HasGarrisonBuilderLightArc(in MapLightMarker light) =>
        HasGarrisonBuilderLightAim(light)
        && light.Flicker != MapLightFlicker.Siren
        && light.Style.EffectiveMotion is MapLightMotion.Sweep or MapLightMotion.Bounce or MapLightMotion.Wander or MapLightMotion.Scan or MapLightMotion.Beat;

    /// <summary>Screen positions of the selected light's handles.</summary>
    private Dictionary<GarrisonBuilderLightHandle, Vector2> GetGarrisonBuilderLightHandlePoints(in MapLightMarker light)
    {
        var handles = new Dictionary<GarrisonBuilderLightHandle, Vector2>();
        if (!HasGarrisonBuilderLightAim(light))
        {
            return handles;
        }

        var center = BuilderWorldToScreen(new Vector2(light.X, light.Y));
        var edge = BuilderWorldToScreen(new Vector2(light.X + light.Radius, light.Y));
        var distance = Math.Clamp(MathF.Abs(edge.X - center.X) * 0.5f, 30f, 90f);
        Vector2 At(float degrees, float length)
        {
            var radians = -degrees * (MathF.PI / 180f);
            return center + (new Vector2(MathF.Cos(radians), MathF.Sin(radians)) * length);
        }

        var aim = MapLightMetadata.GetAimDirection(light);
        handles[GarrisonBuilderLightHandle.Aim] = At(aim, distance);
        if (HasGarrisonBuilderLightArc(light))
        {
            var half = light.Style.Arc * 0.5f;
            handles[GarrisonBuilderLightHandle.ArcStart] = At(aim - half, distance * 0.8f);
            handles[GarrisonBuilderLightHandle.ArcEnd] = At(aim + half, distance * 0.8f);
        }

        return handles;
    }

    /// <summary>Handles on the selected light: a round knob for its aim, diamonds for its sweep arc.</summary>
    private void DrawGarrisonBuilderLightHandles(CustomMapBuilderEntity entity)
    {
        if (!TryGetGarrisonBuilderSelectedLight(out var index, out var light) || _builderEntities[index] != entity)
        {
            return;
        }

        var center = BuilderWorldToScreen(new Vector2(light.X, light.Y));
        foreach (var (handle, point) in GetGarrisonBuilderLightHandlePoints(light))
        {
            var active = _builderLightHandle == handle && _builderLightHandleEntityIndex == index;
            var color = handle == GarrisonBuilderLightHandle.Aim ? new Color(255, 220, 120) : new Color(140, 210, 255);
            // A thin line back to the light.
            var steps = (int)Vector2.Distance(center, point) / 4;
            for (var step = 2; step < steps; step += 1)
            {
                var dot = Vector2.Lerp(center, point, step / (float)steps);
                _spriteBatch.Draw(_pixel, new Rectangle((int)dot.X, (int)dot.Y, 1, 1), color * 0.6f);
            }

            var size = active ? 9 : 7;
            var knob = new Rectangle((int)point.X - (size / 2), (int)point.Y - (size / 2), size, size);
            _spriteBatch.Draw(_pixel, new Rectangle(knob.X - 1, knob.Y - 1, knob.Width + 2, knob.Height + 2), new Color(20, 18, 16));
            _spriteBatch.Draw(_pixel, knob, color);
        }
    }

    /// <summary>Starts dragging a handle of the selected light, if the click is on one.</summary>
    private bool TryBeginGarrisonBuilderLightHandleDrag(Point screenPosition)
    {
        if (!TryGetGarrisonBuilderSelectedLight(out var index, out var light))
        {
            return false;
        }

        foreach (var (handle, point) in GetGarrisonBuilderLightHandlePoints(light))
        {
            var bounds = new Rectangle((int)point.X - 7, (int)point.Y - 7, 14, 14);
            if (!bounds.Contains(screenPosition))
            {
                continue;
            }

            CloseGarrisonBuilderEntityContextMenu();
            RecordGarrisonBuilderHistory();
            _builderLightHandle = handle;
            _builderLightHandleEntityIndex = index;
            _builderStatus = handle == GarrisonBuilderLightHandle.Aim
                ? "drag to aim; hold Shift to snap to 15 degrees"
                : "drag to set the sweep arc; hold Shift to snap to 15 degrees";
            return true;
        }

        return false;
    }

    private bool IsGarrisonBuilderLightHandleDragging => _builderLightHandle != GarrisonBuilderLightHandle.None;

    private void ApplyGarrisonBuilderLightHandleDrag(Vector2 world)
    {
        var index = _builderLightHandleEntityIndex;
        if (index < 0 || index >= _builderEntities.Count)
        {
            EndGarrisonBuilderLightHandleDrag();
            return;
        }

        var entity = _builderEntities[index];
        var light = MapLightMetadata.FromProperties(entity.X, entity.Y, entity.Properties);
        var deltaX = world.X - light.X;
        var deltaY = world.Y - light.Y;
        if ((deltaX * deltaX) + (deltaY * deltaY) < 4f)
        {
            return;
        }

        // Map directions count counter-clockwise with y up.
        var degrees = MapLightMetadata.NormalizeDirection(MathF.Atan2(-deltaY, deltaX) * (180f / MathF.PI));
        var step = _builderShiftHeld ? 15f : 1f;
        degrees = MapLightMetadata.NormalizeDirection(MathF.Round(degrees / step) * step);
        if (_builderLightHandle == GarrisonBuilderLightHandle.Aim)
        {
            // A mirrored light stores its unmirrored direction; the handle shows where it really points.
            var stored = light.Style.Mirror ? MapLightMetadata.MirrorDirection(degrees) : degrees;
            SetGarrisonBuilderLightProperty(index, MapLightMetadata.DirectionKey, stored.ToString("0.##", CultureInfo.InvariantCulture));
            _builderStatus = $"aim: {degrees:0} degrees";
            return;
        }

        var difference = MathF.Abs(((degrees - MapLightMetadata.GetAimDirection(light) + 540f) % 360f) - 180f);
        var arc = Math.Clamp(MathF.Round(difference * 2f / step) * step, 0f, 360f);
        SetGarrisonBuilderLightProperty(index, MapLightMetadata.ArcKey, arc.ToString("0.##", CultureInfo.InvariantCulture));
        _builderStatus = $"sweep arc: {arc:0} degrees";
    }

    private void EndGarrisonBuilderLightHandleDrag()
    {
        if (_builderLightHandle == GarrisonBuilderLightHandle.None)
        {
            return;
        }

        _builderLightHandle = GarrisonBuilderLightHandle.None;
        _builderLightHandleEntityIndex = -1;
        UpdateGarrisonBuilderDocumentEntities();
        _builderDirty = true;
    }

    /// <summary>Sets one property of a placed light (and the open property editor, if it shows that light).</summary>
    private void SetGarrisonBuilderLightProperty(int entityIndex, string key, string value)
    {
        if (entityIndex < 0 || entityIndex >= _builderEntities.Count)
        {
            return;
        }

        var entity = _builderEntities[entityIndex];
        var properties = new Dictionary<string, string>(entity.Properties, StringComparer.OrdinalIgnoreCase)
        {
            [key] = value,
        };
        _builderEntities[entityIndex] = (entity with { Properties = properties }).NormalizeForEditing();
        if (_builderPropertyTarget == GarrisonBuilderPropertyTarget.SelectedMapEntity && _builderSelectedEntityIndex == entityIndex)
        {
            _builderPropertyEditorValues[key] = value;
        }
    }
}
