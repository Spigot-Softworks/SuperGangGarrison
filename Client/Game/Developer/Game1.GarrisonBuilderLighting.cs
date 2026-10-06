#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>
/// Builder lighting: the Lighting editor dialog (preset plus fine-grained tint, glow and
/// light controls with live preview), the in-editor lighting preview, and drawing of
/// placed light entities.
/// </summary>
public partial class Game1
{
    private const int GarrisonBuilderLightingMinDialogWidth = 360;
    private const int GarrisonBuilderLightingMinTrackWidth = 140;

    private static readonly string[] GarrisonBuilderLightingSliderLabels =
    [
        "Brightness", "Effect lights", "Player light", "Rim light", "Cast shadow", "  Distance", "Vignette",
        "Body shade", "  Saturation",
    ];

    private static readonly RasterizerState GarrisonBuilderLightingScissorState = new()
    {
        CullMode = CullMode.None,
        ScissorTestEnable = true,
    };

    private readonly LightmapRenderer _builderLightmap = new();
    private bool _builderLightingPreviewEnabled = true;
    private bool _builderLightingDialogOpen;
    private MapLighting _builderLightingDraft = MapLighting.None;
    private GarrisonBuilderLightingRow _builderLightingDragRow = GarrisonBuilderLightingRow.None;

    private enum GarrisonBuilderLightingRow
    {
        None,
        Preset,
        Tabs,
        SkyHeader,
        SkyRed,
        SkyGreen,
        SkyBlue,
        GroundHeader,
        GroundRed,
        GroundGreen,
        GroundBlue,
        Brightness,
        Glow,
        EffectLights,
        EffectRange,
        PlayerLight,
        PlayerRange,
        Vignette,
        Pulse,
        RimLight,
        RimWidth,
        RimWrap,
        RimColor,
        RimCustomRed,
        RimCustomGreen,
        RimCustomBlue,
        RimSource,
        BodyShade,
        BodySaturation,
        CastShadow,
        ShadowDistance,
        Style,
        Preview,
        Buttons,
    }

    private enum GarrisonBuilderLightingTab
    {
        Scene,
        Lights,
        Characters,
    }

    private static readonly (GarrisonBuilderLightingTab Tab, string Label)[] GarrisonBuilderLightingTabs =
    [
        (GarrisonBuilderLightingTab.Scene, "Scene"),
        (GarrisonBuilderLightingTab.Lights, "Lights"),
        (GarrisonBuilderLightingTab.Characters, "Characters"),
    ];

    private static readonly GarrisonBuilderLightingRow[] GarrisonBuilderLightingSceneRows =
    [
        GarrisonBuilderLightingRow.SkyHeader,
        GarrisonBuilderLightingRow.SkyRed,
        GarrisonBuilderLightingRow.SkyGreen,
        GarrisonBuilderLightingRow.SkyBlue,
        GarrisonBuilderLightingRow.GroundHeader,
        GarrisonBuilderLightingRow.GroundRed,
        GarrisonBuilderLightingRow.GroundGreen,
        GarrisonBuilderLightingRow.GroundBlue,
        GarrisonBuilderLightingRow.Brightness,
        GarrisonBuilderLightingRow.Vignette,
        GarrisonBuilderLightingRow.Pulse,
        GarrisonBuilderLightingRow.Style,
    ];

    private static readonly GarrisonBuilderLightingRow[] GarrisonBuilderLightingLightsRows =
    [
        GarrisonBuilderLightingRow.Glow,
        GarrisonBuilderLightingRow.EffectLights,
        GarrisonBuilderLightingRow.EffectRange,
        GarrisonBuilderLightingRow.PlayerLight,
        GarrisonBuilderLightingRow.PlayerRange,
    ];

    private static readonly GarrisonBuilderLightingRow[] GarrisonBuilderLightingCharacterRows =
    [
        GarrisonBuilderLightingRow.RimLight,
        GarrisonBuilderLightingRow.RimWidth,
        GarrisonBuilderLightingRow.RimWrap,
        GarrisonBuilderLightingRow.RimColor,
        GarrisonBuilderLightingRow.RimCustomRed,
        GarrisonBuilderLightingRow.RimCustomGreen,
        GarrisonBuilderLightingRow.RimCustomBlue,
        GarrisonBuilderLightingRow.RimSource,
        GarrisonBuilderLightingRow.BodyShade,
        GarrisonBuilderLightingRow.BodySaturation,
        GarrisonBuilderLightingRow.CastShadow,
        GarrisonBuilderLightingRow.ShadowDistance,
    ];

    /// <summary>Every tab gets the same height, so the dialog does not jump when switching.</summary>
    private static readonly int GarrisonBuilderLightingMaxTabRows = Math.Max(
        GarrisonBuilderLightingSceneRows.Length,
        Math.Max(GarrisonBuilderLightingLightsRows.Length, GarrisonBuilderLightingCharacterRows.Length));

    /// <summary>Preset, tab bar, the tallest tab, preview, apply/cancel.</summary>
    private static int GarrisonBuilderLightingSlotCount => GarrisonBuilderLightingMaxTabRows + 4;

    private GarrisonBuilderLightingTab _builderLightingTab = GarrisonBuilderLightingTab.Scene;

    private GarrisonBuilderLightingRow[] GetGarrisonBuilderLightingTabRows() => _builderLightingTab switch
    {
        GarrisonBuilderLightingTab.Lights => GarrisonBuilderLightingLightsRows,
        GarrisonBuilderLightingTab.Characters => GarrisonBuilderLightingCharacterRows,
        _ => GarrisonBuilderLightingSceneRows,
    };

    /// <summary>The dialog's rows and the slot (row position) each occupies.</summary>
    private List<(GarrisonBuilderLightingRow Row, int Slot)> GetGarrisonBuilderLightingLayout()
    {
        var layout = new List<(GarrisonBuilderLightingRow Row, int Slot)>
        {
            (GarrisonBuilderLightingRow.Preset, 0),
            (GarrisonBuilderLightingRow.Tabs, 1),
        };
        var tabRows = GetGarrisonBuilderLightingTabRows();
        for (var index = 0; index < tabRows.Length; index += 1)
        {
            layout.Add((tabRows[index], 2 + index));
        }

        // Preview and Apply/Cancel stay pinned to the bottom on every tab.
        layout.Add((GarrisonBuilderLightingRow.Preview, 2 + GarrisonBuilderLightingMaxTabRows));
        layout.Add((GarrisonBuilderLightingRow.Buttons, 3 + GarrisonBuilderLightingMaxTabRows));
        return layout;
    }

    /// <summary>What the editor shows: the draft while the dialog is open, else the map's saved lighting.</summary>
    private MapLighting GetGarrisonBuilderPreviewLighting() =>
        _builderLightingDialogOpen ? _builderLightingDraft : MapLightingMetadata.Parse(_builderDocument.Metadata);

    private void OpenGarrisonBuilderLightingDialog()
    {
        FinishGarrisonBuilderGestures();
        _builderLightingDraft = MapLightingMetadata.Parse(_builderDocument.Metadata);
        _builderLightingDragRow = GarrisonBuilderLightingRow.None;
        _builderLightingDialogOpen = true;
        _builderStatus = "lighting: drag sliders to adjust, Enter to apply, Esc to cancel";
    }

    private void CloseGarrisonBuilderLightingDialog(bool apply)
    {
        if (apply)
        {
            var metadata = new Dictionary<string, string>(_builderDocument.Metadata, StringComparer.OrdinalIgnoreCase);
            var before = MapLightingMetadata.Parse(_builderDocument.Metadata);
            MapLightingMetadata.Write(metadata, _builderLightingDraft);
            if (before != _builderLightingDraft)
            {
                RecordGarrisonBuilderHistory();
                _builderDocument = (_builderDocument with { Metadata = metadata }).NormalizeForEditing();
                _builderDirty = true;
            }

            _builderStatus = _builderLightingDraft.IsActive
                ? $"lighting: {MapLightingMetadata.GetPresetDisplayLabel(_builderLightingDraft.Preset).ToLowerInvariant()} (quick test to see gameplay lights)"
                : "lighting off";
        }
        else
        {
            _builderStatus = "lighting unchanged";
        }

        _builderLightingDialogOpen = false;
        _builderLightingDragRow = GarrisonBuilderLightingRow.None;
    }

    private void ToggleGarrisonBuilderLightingPreview()
    {
        _builderLightingPreviewEnabled = !_builderLightingPreviewEnabled;
        _builderStatus = _builderLightingPreviewEnabled ? "lighting preview on" : "lighting preview off";
    }

    /// <summary>
    /// Builds the editor's light map from the map viewport and placed lights. Called from the
    /// menu frame before the logical frame target is bound.
    /// </summary>
    public void PrepareGarrisonBuilderLightingPreview()
    {
        var lighting = _builderEditorEnabled && _builderUseModernUi && _builderLightingPreviewEnabled
            ? GetGarrisonBuilderPreviewLighting()
            : MapLighting.None;
        if (!lighting.IsActive)
        {
            _builderLightmap.Begin(MapLighting.None, 0f, 0f, 1f, 1f, 1f, 0d);
            return;
        }

        var viewport = GetModernGarrisonBuilderMapViewport();
        var topLeft = BuilderScreenToWorld(viewport.Location.ToVector2());
        var bottomRight = BuilderScreenToWorld(new Vector2(viewport.Right, viewport.Bottom));
        var worldHeight = BuilderDisplayToWorld(new Vector2(0f, GetGarrisonBuilderMapHeight())).Y;
        var time = _weatherClock.Elapsed.TotalSeconds;
        _builderLightmap.Begin(
            lighting,
            topLeft.X,
            topLeft.Y,
            MathF.Max(1f, bottomRight.X - topLeft.X),
            MathF.Max(1f, bottomRight.Y - topLeft.Y),
            worldHeight,
            time);
        for (var index = 0; index < _builderEntities.Count; index += 1)
        {
            var entity = _builderEntities[index];
            if (!MapLightMetadata.IsLightEntityType(entity.Type) || IsGarrisonBuilderEntityHidden(index))
            {
                continue;
            }

            var light = MapLightMetadata.FromProperties(entity.X, entity.Y, entity.Properties);
            _builderLightmap.AddLight(
                light.X,
                light.Y,
                light.Radius,
                new Color(light.Color.R, light.Color.G, light.Color.B),
                light.Intensity / 100f * LightmapRenderer.GetFlicker(light.Flicker, time, index * 7.31f),
                falloff: light.Falloff,
                directionDegrees: light.Direction,
                spreadDegrees: light.Spread);
        }

        _builderLightmap.Render(GraphicsDevice, _spriteBatch);
    }

    /// <summary>Lays the preview light map over the editor's map view (art, sprites, entities).</summary>
    private void DrawGarrisonBuilderLightingPreview(Rectangle mapViewport)
    {
        if (!_builderLightmap.HasFrame)
        {
            return;
        }

        var origin = BuilderWorldToScreen(Vector2.Zero);
        var scale = BuilderWorldToScreen(Vector2.UnitX).X - origin.X;
        if (scale <= 0f)
        {
            return;
        }

        var sampler = _builderLightmap.Lighting.Banded ? SamplerState.PointClamp : SamplerState.LinearClamp;
        var previousScissor = GraphicsDevice.ScissorRectangle;
        _spriteBatch.End();
        GraphicsDevice.ScissorRectangle = mapViewport;
        _spriteBatch.Begin(SpriteSortMode.Deferred, LightmapRenderer.Multiply2x, sampler, DepthStencilState.None, GarrisonBuilderLightingScissorState);
        _builderLightmap.DrawMultiply(_spriteBatch, scale, origin);
        _spriteBatch.End();
        if (_builderLightmap.Lighting.Glow > 0)
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, LightmapRenderer.AddOne, SamplerState.LinearClamp, DepthStencilState.None, GarrisonBuilderLightingScissorState);
            _builderLightmap.DrawGlow(_spriteBatch, scale, origin);
            _spriteBatch.End();
        }

        GraphicsDevice.ScissorRectangle = previousScissor;
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
    }

    /// <summary>
    /// Light entity: a small bulb in its colour plus a dotted outline of what it lights:
    /// a ring for a round light, or the cone's arc and edges for a spotlight.
    /// </summary>
    private void DrawGarrisonBuilderLightEntity(CustomMapBuilderEntity entity, Color tint)
    {
        var light = MapLightMetadata.FromProperties(entity.X, entity.Y, entity.Properties);
        var color = new Color(light.Color.R, light.Color.G, light.Color.B);
        var center = BuilderWorldToScreen(new Vector2(entity.X, entity.Y));
        var edge = BuilderWorldToScreen(new Vector2(entity.X + light.Radius, entity.Y));
        var screenRadius = MathF.Abs(edge.X - center.X);
        var ringColor = color * 0.55f;
        var directional = MapLightMetadata.IsDirectional(light.Spread);
        // Screen angles run clockwise (y down); map directions run counter-clockwise.
        var arcCenter = -light.Direction * (MathF.PI / 180f);
        var arcWidth = directional ? light.Spread * (MathF.PI / 180f) : MathF.Tau;
        var arcStart = arcCenter - (arcWidth * 0.5f);
        var dots = Math.Clamp((int)(screenRadius * arcWidth / 30f), 6, 96);
        for (var dot = 0; dot <= dots; dot += 1)
        {
            var angle = arcStart + (dot * arcWidth / dots);
            var point = center + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * screenRadius);
            _spriteBatch.Draw(_pixel, new Rectangle((int)point.X, (int)point.Y, 2, 2), ringColor);
        }

        if (directional)
        {
            var edgeDots = Math.Clamp((int)(screenRadius / 6f), 4, 48);
            for (var side = 0; side < 2; side += 1)
            {
                var angle = side == 0 ? arcStart : arcStart + arcWidth;
                var along = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                for (var dot = 1; dot < edgeDots; dot += 1)
                {
                    var point = center + (along * (screenRadius * dot / edgeDots));
                    _spriteBatch.Draw(_pixel, new Rectangle((int)point.X, (int)point.Y, 2, 2), ringColor);
                }
            }

            // Aim tick: a short solid line out of the bulb.
            var aim = new Vector2(MathF.Cos(arcCenter), MathF.Sin(arcCenter));
            for (var step = 6; step <= 16; step += 1)
            {
                var point = center + (aim * step);
                _spriteBatch.Draw(_pixel, new Rectangle((int)point.X, (int)point.Y, 2, 2), color);
            }
        }

        var bulb = new Rectangle((int)MathF.Round(center.X) - 4, (int)MathF.Round(center.Y) - 4, 9, 9);
        _spriteBatch.Draw(_pixel, bulb, new Color(30, 26, 22) * (tint.A / 255f));
        _spriteBatch.Draw(_pixel, new Rectangle(bulb.X + 1, bulb.Y + 1, 7, 7), color);
        _spriteBatch.Draw(_pixel, new Rectangle(bulb.X + 3, bulb.Y - 3, 3, 2), color * 0.8f);
        _spriteBatch.Draw(_pixel, new Rectangle(bulb.X + 3, bulb.Bottom + 1, 3, 2), color * 0.8f);
        _spriteBatch.Draw(_pixel, new Rectangle(bulb.X - 3, bulb.Y + 3, 2, 3), color * 0.8f);
        _spriteBatch.Draw(_pixel, new Rectangle(bulb.Right + 1, bulb.Y + 3, 2, 3), color * 0.8f);
    }

    private float GetGarrisonBuilderLightingTextScale() => GetModernBuilderTextScale(0.82f);

    /// <summary>
    /// Rows are as tall as a builder button (plus a small gap) so button text never overlaps
    /// the next row; on a very short window only the gap is given up, never the button height.
    /// </summary>
    private int GetGarrisonBuilderLightingRowHeight()
    {
        var buttonHeight = (int)GetGarrisonBuilderMinimumButtonHeight();
        var viewport = GetModernGarrisonBuilderMapViewport();
        var fitted = (viewport.Height - 16 - GetGarrisonBuilderLightingTitleHeight() - 8) / GarrisonBuilderLightingSlotCount;
        return Math.Clamp(fitted, buttonHeight, buttonHeight + 4);
    }

    private int GetGarrisonBuilderLightingTitleHeight() =>
        (int)MathF.Ceiling(MeasureBitmapFontHeight(GetModernBuilderTextScale(0.95f))) + 16;

    /// <summary>Wide enough for the longest slider label, so labels never run into their sliders.</summary>
    private int GetGarrisonBuilderLightingLabelWidth()
    {
        var scale = GetGarrisonBuilderLightingTextScale();
        var widest = 0f;
        foreach (var label in GarrisonBuilderLightingSliderLabels)
        {
            widest = MathF.Max(widest, MeasureBitmapFontWidth(label, scale));
        }

        return (int)MathF.Ceiling(widest) + 12;
    }

    private int GetGarrisonBuilderLightingValueWidth() =>
        (int)MathF.Ceiling(MeasureBitmapFontWidth("255", GetGarrisonBuilderLightingTextScale())) + 10;

    private Rectangle GetGarrisonBuilderLightingDialogBounds()
    {
        var viewport = GetModernGarrisonBuilderMapViewport();
        var wanted = 16 + GetGarrisonBuilderLightingLabelWidth() + GarrisonBuilderLightingMinTrackWidth + GetGarrisonBuilderLightingValueWidth();
        var width = Math.Min(Math.Max(GarrisonBuilderLightingMinDialogWidth, wanted), Math.Max(200, viewport.Width - 16));
        var height = GetGarrisonBuilderLightingTitleHeight() + (GarrisonBuilderLightingSlotCount * GetGarrisonBuilderLightingRowHeight()) + 8;
        var x = Math.Max(viewport.X + 8, viewport.Right - width - 8);
        return new Rectangle(x, viewport.Y + 8, width, height);
    }

    private Rectangle GetGarrisonBuilderLightingRowBounds(int slot)
    {
        var dialog = GetGarrisonBuilderLightingDialogBounds();
        var rowHeight = GetGarrisonBuilderLightingRowHeight();
        return new Rectangle(
            dialog.X + 8,
            dialog.Y + GetGarrisonBuilderLightingTitleHeight() + (slot * rowHeight),
            dialog.Width - 16,
            Math.Min(rowHeight, Math.Max((int)GetGarrisonBuilderMinimumButtonHeight(), rowHeight - 4)));
    }

    private Rectangle GetGarrisonBuilderLightingSliderTrack(Rectangle row)
    {
        var left = row.X + GetGarrisonBuilderLightingLabelWidth();
        var right = row.Right - GetGarrisonBuilderLightingValueWidth();
        return new Rectangle(left, row.Center.Y - 3, Math.Max(20, right - left), 6);
    }

    private Rectangle GetGarrisonBuilderLightingTabBounds(Rectangle row, int index)
    {
        const int Gap = 6;
        var tabWidth = (row.Width - (Gap * (GarrisonBuilderLightingTabs.Length - 1))) / GarrisonBuilderLightingTabs.Length;
        return new Rectangle(row.X + (index * (tabWidth + Gap)), row.Y, tabWidth, row.Height);
    }

    /// <summary>Returns true while the dialog owns this frame's input.</summary>
    private bool UpdateGarrisonBuilderLightingDialog(KeyboardState keyboard, MouseState mouse)
    {
        if (!_builderLightingDialogOpen)
        {
            return false;
        }

        if (IsKeyPressed(keyboard, Keys.Escape))
        {
            CloseGarrisonBuilderLightingDialog(apply: false);
            return true;
        }

        if (IsKeyPressed(keyboard, Keys.Enter))
        {
            CloseGarrisonBuilderLightingDialog(apply: true);
            return true;
        }

        var pressed = mouse.LeftButton == ButtonState.Pressed;
        var justPressed = pressed && _previousMouse.LeftButton == ButtonState.Released;
        if (_builderLightingDragRow != GarrisonBuilderLightingRow.None)
        {
            if (!pressed)
            {
                _builderLightingDragRow = GarrisonBuilderLightingRow.None;
                return true;
            }

            SetGarrisonBuilderLightingSliderFromMouse(_builderLightingDragRow, mouse.X);
            return true;
        }

        var dialog = GetGarrisonBuilderLightingDialogBounds();
        if (!dialog.Contains(mouse.Position))
        {
            // Outside clicks are swallowed by the UI click router; camera keys and zoom still work.
            return false;
        }

        if (justPressed)
        {
            HandleGarrisonBuilderLightingDialogClick(mouse.Position);
        }

        return true;
    }

    private void HandleGarrisonBuilderLightingDialogClick(Point position)
    {
        foreach (var (row, slot) in GetGarrisonBuilderLightingLayout())
        {
            var bounds = GetGarrisonBuilderLightingRowBounds(slot);
            if (!bounds.Contains(position))
            {
                continue;
            }

            switch (row)
            {
                case GarrisonBuilderLightingRow.Tabs:
                    for (var tab = 0; tab < GarrisonBuilderLightingTabs.Length; tab += 1)
                    {
                        if (GetGarrisonBuilderLightingTabBounds(bounds, tab).Contains(position))
                        {
                            _builderLightingTab = GarrisonBuilderLightingTabs[tab].Tab;
                        }
                    }

                    return;
                case GarrisonBuilderLightingRow.Preset:
                    StepGarrisonBuilderLightingPreset(position.X < bounds.Center.X ? -1 : 1);
                    return;
                case GarrisonBuilderLightingRow.RimColor:
                    EnsureGarrisonBuilderLightingDraftActive();
                    _builderLightingDraft = _builderLightingDraft with { RimColorMode = MapLightingMetadata.NextRimColorMode(_builderLightingDraft.RimColorMode) };
                    return;
                case GarrisonBuilderLightingRow.RimSource:
                    EnsureGarrisonBuilderLightingDraftActive();
                    _builderLightingDraft = _builderLightingDraft with { RimFromSky = !_builderLightingDraft.RimFromSky };
                    return;
                case GarrisonBuilderLightingRow.Style:
                    EnsureGarrisonBuilderLightingDraftActive();
                    _builderLightingDraft = _builderLightingDraft with { Banded = !_builderLightingDraft.Banded };
                    return;
                case GarrisonBuilderLightingRow.Preview:
                    ToggleGarrisonBuilderLightingPreview();
                    return;
                case GarrisonBuilderLightingRow.Buttons:
                    CloseGarrisonBuilderLightingDialog(apply: position.X < bounds.Center.X);
                    return;
                case GarrisonBuilderLightingRow.SkyHeader:
                case GarrisonBuilderLightingRow.GroundHeader:
                    return;
                default:
                    _builderLightingDragRow = row;
                    SetGarrisonBuilderLightingSliderFromMouse(row, position.X);
                    return;
            }
        }
    }

    private void StepGarrisonBuilderLightingPreset(int direction)
    {
        var presets = MapLightingMetadata.Presets;
        var current = 0;
        for (var index = 0; index < presets.Count; index += 1)
        {
            if (presets[index] == _builderLightingDraft.Preset)
            {
                current = index;
                break;
            }
        }

        var next = presets[(current + direction + presets.Count) % presets.Count];
        // Custom keeps whatever is being tuned; every other preset loads its look.
        _builderLightingDraft = next == MapLightingPreset.Custom && _builderLightingDraft.IsActive
            ? _builderLightingDraft with { Preset = MapLightingPreset.Custom }
            : MapLightingMetadata.GetPresetDefaults(next);
    }

    /// <summary>Touching a control while lighting is off starts from a neutral custom look.</summary>
    private void EnsureGarrisonBuilderLightingDraftActive()
    {
        if (!_builderLightingDraft.IsActive)
        {
            _builderLightingDraft = MapLightingMetadata.GetPresetDefaults(MapLightingPreset.Custom);
        }
    }

    private void SetGarrisonBuilderLightingSliderFromMouse(GarrisonBuilderLightingRow row, int mouseX)
    {
        // Every slider row has the same track span, so any slider slot gives the geometry.
        EnsureGarrisonBuilderLightingDraftActive();
        var track = GetGarrisonBuilderLightingSliderTrack(GetGarrisonBuilderLightingRowBounds(2));
        var fraction = Math.Clamp((mouseX - track.X) / (float)Math.Max(1, track.Width - 1), 0f, 1f);
        var minimum = GetGarrisonBuilderLightingSliderMin(row);
        var value = minimum + (int)MathF.Round(fraction * (GetGarrisonBuilderLightingSliderMax(row) - minimum));
        if (GetGarrisonBuilderLightingSliderMax(row) == MapLightingMetadata.MaxScale && Math.Abs(value - 100) <= 3)
        {
            value = 100; // snap to normal so it is easy to get back to
        }

        var percent = value;
        var channel = (byte)Math.Clamp(value, 0, 255);
        var draft = _builderLightingDraft;
        _builderLightingDraft = row switch
        {
            GarrisonBuilderLightingRow.SkyRed => draft with { SkyTint = draft.SkyTint with { R = channel } },
            GarrisonBuilderLightingRow.SkyGreen => draft with { SkyTint = draft.SkyTint with { G = channel } },
            GarrisonBuilderLightingRow.SkyBlue => draft with { SkyTint = draft.SkyTint with { B = channel } },
            GarrisonBuilderLightingRow.GroundRed => draft with { GroundTint = draft.GroundTint with { R = channel } },
            GarrisonBuilderLightingRow.GroundGreen => draft with { GroundTint = draft.GroundTint with { G = channel } },
            GarrisonBuilderLightingRow.GroundBlue => draft with { GroundTint = draft.GroundTint with { B = channel } },
            GarrisonBuilderLightingRow.Brightness => draft with { Brightness = percent },
            GarrisonBuilderLightingRow.Glow => draft with { Glow = percent },
            GarrisonBuilderLightingRow.EffectLights => draft with { EffectLights = percent },
            GarrisonBuilderLightingRow.EffectRange => draft with { EffectRange = percent },
            GarrisonBuilderLightingRow.PlayerLight => draft with { PlayerLight = percent },
            GarrisonBuilderLightingRow.PlayerRange => draft with { PlayerRange = percent },
            GarrisonBuilderLightingRow.Vignette => draft with { Vignette = percent },
            GarrisonBuilderLightingRow.Pulse => draft with { Pulse = percent },
            GarrisonBuilderLightingRow.RimLight => draft with { RimLight = percent },
            GarrisonBuilderLightingRow.RimWidth => draft with { RimWidth = percent },
            GarrisonBuilderLightingRow.RimWrap => draft with { RimWrap = percent },
            GarrisonBuilderLightingRow.CastShadow => draft with { CastShadow = percent },
            GarrisonBuilderLightingRow.ShadowDistance => draft with { ShadowDistance = percent },
            GarrisonBuilderLightingRow.BodyShade => draft with { BodyShade = percent },
            GarrisonBuilderLightingRow.BodySaturation => draft with { BodySaturation = percent },
            // Dragging a custom channel also switches the rim to the custom colour.
            GarrisonBuilderLightingRow.RimCustomRed => draft with { RimColorMode = MapRimColorMode.Custom, RimCustomColor = draft.ResolvedRimCustomColor with { R = channel } },
            GarrisonBuilderLightingRow.RimCustomGreen => draft with { RimColorMode = MapRimColorMode.Custom, RimCustomColor = draft.ResolvedRimCustomColor with { G = channel } },
            GarrisonBuilderLightingRow.RimCustomBlue => draft with { RimColorMode = MapRimColorMode.Custom, RimCustomColor = draft.ResolvedRimCustomColor with { B = channel } },
            _ => draft,
        };
    }

    /// <summary>Tint channels run 0-255; brightness, ranges and body saturation 0-200 (100 normal); the rest 0-100.</summary>
    private static int GetGarrisonBuilderLightingSliderMax(GarrisonBuilderLightingRow row) => row switch
    {
        GarrisonBuilderLightingRow.SkyRed or GarrisonBuilderLightingRow.SkyGreen or GarrisonBuilderLightingRow.SkyBlue
            or GarrisonBuilderLightingRow.GroundRed or GarrisonBuilderLightingRow.GroundGreen or GarrisonBuilderLightingRow.GroundBlue
            or GarrisonBuilderLightingRow.RimCustomRed or GarrisonBuilderLightingRow.RimCustomGreen or GarrisonBuilderLightingRow.RimCustomBlue => 255,
        GarrisonBuilderLightingRow.Brightness or GarrisonBuilderLightingRow.EffectRange or GarrisonBuilderLightingRow.PlayerRange
            or GarrisonBuilderLightingRow.BodySaturation
            => MapLightingMetadata.MaxScale,
        GarrisonBuilderLightingRow.RimWidth => MapLightingMetadata.MaxRimWidth,
        GarrisonBuilderLightingRow.ShadowDistance => MapLightingMetadata.MaxShadowDistance,
        _ => 100,
    };

    /// <summary>Rim width and shadow distance start at 1; everything else at 0.</summary>
    private static int GetGarrisonBuilderLightingSliderMin(GarrisonBuilderLightingRow row) => row switch
    {
        GarrisonBuilderLightingRow.RimWidth => MapLightingMetadata.MinRimWidth,
        GarrisonBuilderLightingRow.ShadowDistance => MapLightingMetadata.MinShadowDistance,
        _ => 0,
    };

    private void DrawGarrisonBuilderLightingDialog(MouseState mouse)
    {
        if (!_builderLightingDialogOpen)
        {
            return;
        }

        var dialog = GetGarrisonBuilderLightingDialogBounds();
        DrawMenuPanelBackdrop(dialog, 0.97f);
        var textScale = GetGarrisonBuilderLightingTextScale();
        var titleScale = GetModernBuilderTextScale(0.95f);
        DrawBitmapFontText("Lighting", new Vector2(dialog.X + 10f, dialog.Y + 8f), Color.White, titleScale);
        var draft = _builderLightingDraft;
        var active = draft.IsActive;
        if (!active)
        {
            // Explain why everything below is dimmed.
            const string Hint = "Off - pick a preset or drag any slider";
            var hintWidth = MeasureBitmapFontWidth(Hint, textScale);
            var hintX = MathF.Max(dialog.X + 10f + MeasureBitmapFontWidth("Lighting", titleScale) + 12f, dialog.Right - 10f - hintWidth);
            DrawBitmapFontText(Hint, new Vector2(hintX, dialog.Y + 10f), new Color(200, 180, 140), textScale);
        }

        foreach (var (row, slot) in GetGarrisonBuilderLightingLayout())
        {
            var bounds = GetGarrisonBuilderLightingRowBounds(slot);
            var hovered = bounds.Contains(mouse.Position);
            var textY = bounds.Y + MathF.Max(1f, (bounds.Height - MeasureBitmapFontHeight(textScale)) * 0.5f);
            switch (row)
            {
                case GarrisonBuilderLightingRow.Preset:
                    DrawBuilderMenuButton(bounds, $"<   Preset: {MapLightingMetadata.GetPresetDisplayLabel(draft.Preset)}   >", hovered);
                    break;
                case GarrisonBuilderLightingRow.Tabs:
                    for (var tab = 0; tab < GarrisonBuilderLightingTabs.Length; tab += 1)
                    {
                        var tabBounds = GetGarrisonBuilderLightingTabBounds(bounds, tab);
                        var selected = GarrisonBuilderLightingTabs[tab].Tab == _builderLightingTab;
                        DrawBuilderMenuButton(tabBounds, GarrisonBuilderLightingTabs[tab].Label, selected || tabBounds.Contains(mouse.Position));
                        if (selected)
                        {
                            // Underline the open tab.
                            _spriteBatch.Draw(_pixel, new Rectangle(tabBounds.X + 8, tabBounds.Bottom - 4, tabBounds.Width - 16, 2), new Color(255, 200, 120));
                        }
                    }

                    break;
                case GarrisonBuilderLightingRow.SkyHeader:
                case GarrisonBuilderLightingRow.GroundHeader:
                    var sky = row == GarrisonBuilderLightingRow.SkyHeader;
                    var tint = sky ? draft.SkyTint : draft.GroundTint;
                    DrawBitmapFontText(sky ? "Sky tint (top)" : "Ground tint (bottom)", new Vector2(bounds.X, textY), new Color(220, 210, 190), textScale);
                    var swatch = new Rectangle(bounds.Right - 60, bounds.Y + 2, 58, Math.Max(4, bounds.Height - 4));
                    _spriteBatch.Draw(_pixel, swatch, new Color(20, 18, 16));
                    _spriteBatch.Draw(_pixel, new Rectangle(swatch.X + 1, swatch.Y + 1, swatch.Width - 2, swatch.Height - 2), new Color(tint.R, tint.G, tint.B));
                    break;
                case GarrisonBuilderLightingRow.RimColor:
                    DrawBuilderMenuButton(bounds, $"Rim colour: {MapLightingMetadata.GetRimColorDisplayLabel(draft.RimColorMode)}", hovered, enabled: active);
                    if (draft.RimColorMode == MapRimColorMode.Custom)
                    {
                        // Swatch of the custom colour at the button's right end.
                        var rimCustom = draft.ResolvedRimCustomColor;
                        var size = Math.Max(4, bounds.Height - 8);
                        var rimSwatch = new Rectangle(bounds.Right - size - 6, bounds.Y + ((bounds.Height - size) / 2), size, size);
                        _spriteBatch.Draw(_pixel, rimSwatch, new Color(20, 18, 16));
                        _spriteBatch.Draw(_pixel, new Rectangle(rimSwatch.X + 1, rimSwatch.Y + 1, rimSwatch.Width - 2, rimSwatch.Height - 2), new Color(rimCustom.R, rimCustom.G, rimCustom.B));
                    }

                    break;
                case GarrisonBuilderLightingRow.RimSource:
                    DrawBuilderMenuButton(bounds, $"Light from: {(draft.RimFromSky ? "Sky" : "Nearest light")}", hovered, enabled: active);
                    break;
                case GarrisonBuilderLightingRow.Style:
                    DrawBuilderMenuButton(bounds, $"Falloff: {(draft.Banded ? "Retro bands" : "Smooth")}", hovered, enabled: active);
                    break;
                case GarrisonBuilderLightingRow.Preview:
                    DrawBuilderMenuButton(bounds, $"Preview in editor: {(_builderLightingPreviewEnabled ? "On" : "Off")}", hovered);
                    break;
                case GarrisonBuilderLightingRow.Buttons:
                    var half = (bounds.Width - 6) / 2;
                    var apply = new Rectangle(bounds.X, bounds.Y, half, bounds.Height);
                    var cancel = new Rectangle(apply.Right + 6, bounds.Y, half, bounds.Height);
                    DrawBuilderMenuButton(apply, "Apply", apply.Contains(mouse.Position));
                    DrawBuilderMenuButton(cancel, "Cancel", cancel.Contains(mouse.Position));
                    break;
                default:
                    // The custom colour sliders are dimmed (but still draggable) until Custom is picked.
                    var customRow = row is GarrisonBuilderLightingRow.RimCustomRed or GarrisonBuilderLightingRow.RimCustomGreen or GarrisonBuilderLightingRow.RimCustomBlue;
                    DrawGarrisonBuilderLightingSlider(row, bounds, textY, textScale, active && (!customRow || draft.RimColorMode == MapRimColorMode.Custom));
                    break;
            }
        }
    }

    private void DrawGarrisonBuilderLightingSlider(GarrisonBuilderLightingRow row, Rectangle bounds, float textY, float textScale, bool active)
    {
        var draft = _builderLightingDraft;
        var (label, value, fill) = row switch
        {
            GarrisonBuilderLightingRow.SkyRed => ("  Red", (int)draft.SkyTint.R, new Color(210, 70, 60)),
            GarrisonBuilderLightingRow.SkyGreen => ("  Green", (int)draft.SkyTint.G, new Color(80, 190, 90)),
            GarrisonBuilderLightingRow.SkyBlue => ("  Blue", (int)draft.SkyTint.B, new Color(80, 120, 220)),
            GarrisonBuilderLightingRow.GroundRed => ("  Red", (int)draft.GroundTint.R, new Color(210, 70, 60)),
            GarrisonBuilderLightingRow.GroundGreen => ("  Green", (int)draft.GroundTint.G, new Color(80, 190, 90)),
            GarrisonBuilderLightingRow.GroundBlue => ("  Blue", (int)draft.GroundTint.B, new Color(80, 120, 220)),
            GarrisonBuilderLightingRow.Brightness => ("Brightness", draft.Brightness, new Color(230, 210, 150)),
            GarrisonBuilderLightingRow.Glow => ("Glow", draft.Glow, new Color(255, 190, 110)),
            GarrisonBuilderLightingRow.EffectLights => ("Effect lights", draft.EffectLights, new Color(255, 140, 60)),
            GarrisonBuilderLightingRow.EffectRange => ("  Range", draft.EffectRange, new Color(255, 170, 100)),
            GarrisonBuilderLightingRow.PlayerLight => ("Player light", draft.PlayerLight, new Color(240, 230, 210)),
            GarrisonBuilderLightingRow.PlayerRange => ("  Range", draft.PlayerRange, new Color(220, 215, 200)),
            GarrisonBuilderLightingRow.Vignette => ("Vignette", draft.Vignette, new Color(150, 140, 130)),
            GarrisonBuilderLightingRow.Pulse => ("Pulse", draft.Pulse, new Color(150, 220, 120)),
            GarrisonBuilderLightingRow.RimLight => ("Rim light", draft.RimLight, new Color(255, 200, 140)),
            GarrisonBuilderLightingRow.RimWidth => ("  Width", draft.RimWidth, new Color(255, 215, 170)),
            GarrisonBuilderLightingRow.RimWrap => ("  Wrap", draft.RimWrap, new Color(255, 215, 170)),
            GarrisonBuilderLightingRow.RimCustomRed => ("  Red", (int)draft.ResolvedRimCustomColor.R, new Color(210, 70, 60)),
            GarrisonBuilderLightingRow.RimCustomGreen => ("  Green", (int)draft.ResolvedRimCustomColor.G, new Color(80, 190, 90)),
            GarrisonBuilderLightingRow.RimCustomBlue => ("  Blue", (int)draft.ResolvedRimCustomColor.B, new Color(80, 120, 220)),
            GarrisonBuilderLightingRow.CastShadow => ("Cast shadow", draft.CastShadow, new Color(110, 100, 120)),
            GarrisonBuilderLightingRow.ShadowDistance => ("  Distance", draft.ShadowDistance, new Color(140, 130, 150)),
            GarrisonBuilderLightingRow.BodyShade => ("Body shade", draft.BodyShade, new Color(90, 80, 96)),
            GarrisonBuilderLightingRow.BodySaturation => ("  Saturation", draft.BodySaturation, new Color(200, 120, 170)),
            _ => (string.Empty, 0, Color.White),
        };
        var max = GetGarrisonBuilderLightingSliderMax(row);
        var min = GetGarrisonBuilderLightingSliderMin(row);

        var dim = active ? 1f : 0.45f;
        DrawBitmapFontText(label, new Vector2(bounds.X, textY), new Color(220, 210, 190) * dim, textScale);
        var track = GetGarrisonBuilderLightingSliderTrack(bounds);
        var fraction = Math.Clamp((value - min) / (float)Math.Max(1, max - min), 0f, 1f);
        _spriteBatch.Draw(_pixel, track, new Color(24, 22, 20) * dim);
        _spriteBatch.Draw(_pixel, new Rectangle(track.X, track.Y, (int)MathF.Round(track.Width * fraction), track.Height), fill * (0.85f * dim));
        if (max == MapLightingMetadata.MaxScale)
        {
            // Tick at 100: the normal amount.
            var normalX = track.X + (int)MathF.Round((track.Width - 1) * (100f / max));
            _spriteBatch.Draw(_pixel, new Rectangle(normalX, track.Bottom + 1, 1, 3), new Color(200, 190, 170) * dim);
        }
        var markerX = track.X + (int)MathF.Round((track.Width - 1) * fraction);
        _spriteBatch.Draw(_pixel, new Rectangle(markerX - 1, track.Y - 3, 3, track.Height + 6), Color.White * dim);
        DrawBitmapFontText(
            value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            new Vector2(track.Right + 6f, textY),
            Color.White * dim,
            textScale);
    }
}
