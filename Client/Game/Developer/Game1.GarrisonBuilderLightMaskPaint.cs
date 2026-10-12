#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using Point = Microsoft.Xna.Framework.Point;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const string GarrisonBuilderLightPaintMaskKey = "$lightPaintMask";
    private const string GarrisonBuilderPaintMaskPreviewResource = "$paintMaskPreview";
    // A fixed backing grid allows changing the brush without resizing earlier paint.
    private const float GarrisonBuilderMaskPixelSize = 1f;
    private LightMaskPaintCanvas? _builderLightMaskCanvas;
    private Texture2D? _builderLightMaskPaintTexture;
    private Color[]? _builderLightMaskPaintPixels;
    private int _builderLightMaskPaintEntityIndex = -1;
    private Vector2 _builderLightMaskPaintOrigin;
    private string _builderLightMaskPaintColor = "ffcf80";
    private bool _builderLightMaskPaintErase;
    private bool _builderLightMaskPaintPaletteOpen;
    private bool _builderLightMaskPaintStroke;
    private int? _builderLightMaskPaintPreviousPixel;
    private int? _builderLightMaskPaintLineStart;
    private int _builderLightMaskPaintBrushSize = 2;
    private bool _builderLightMaskBrushSliderDragging;
    private string _builderLightMaskPaintEntityId = string.Empty;
    private readonly Dictionary<string, (LightMaskPaintCanvas Canvas, string Color, int BrushSize)> _builderLightMaskDrafts = new(StringComparer.OrdinalIgnoreCase);

    private Rectangle GetGarrisonBuilderMaskPaintViewport() => _builderUseModernUi
        ? GetModernGarrisonBuilderMapViewport() : new Rectangle(0, 0, BuilderViewportWidth, BuilderViewportHeight);

    private void BeginGarrisonBuilderLightMaskPainting()
    {
        if (_builderPropertyTarget != GarrisonBuilderPropertyTarget.SelectedMapEntity
            || _builderSelectedEntityIndex < 0 || _builderSelectedEntityIndex >= _builderEntities.Count)
        {
            _builderStatus = "place and select a lamp before painting its mask";
            return;
        }

        var light = MapLightMetadata.FromProperties(0, 0, _builderPropertyEditorValues);
        if (light.Style.Kind != MapLightKind.Lamp || light.Flicker == MapLightFlicker.Siren)
        {
            _builderStatus = "select the Lamp preset to paint a glow mask";
            return;
        }

        CloseGarrisonBuilderPropertyEditor(applyChanges: true);
        EnsureGarrisonBuilderMapEntityIds();
        var entity = _builderEntities[_builderSelectedEntityIndex];
        _builderLightMaskPaintEntityId = entity.Properties[MapLogicMetadata.MapEntityIdPropertyKey];
        light = MapLightMetadata.FromProperties(entity.X, entity.Y, entity.Properties);
        var side = Math.Clamp((int)MathF.Ceiling(light.Radius) * 2, 2, 4000);
        LightMaskPaintCanvas? canvas = null;
        var layer = BuilderPaintedLightMask.GetLayer(entity.Properties);
        _builderLightMaskPaintBrushSize = BuilderPaintedLightMask.GetBrushSize(entity.Properties);
        _builderLightMaskPaintColor = light.Color.ToHex();
        if (_builderLightMaskDrafts.Remove(_builderLightMaskPaintEntityId, out var draft))
        {
            canvas = draft.Canvas;
            _builderLightMaskPaintColor = draft.Color;
            _builderLightMaskPaintBrushSize = draft.BrushSize;
        }
        else if (layer.Resource is not null)
        {
            if (!_builderDocument.Resources.TryGetValue(layer.Resource, out var resource)
                || !TryGetGarrisonBuilderResourceBytes(resource, out var bytes))
            {
                _builderStatus = "painted mask resource is unavailable; existing layer retained";
                _builderLightMaskPaintEntityId = string.Empty;
                return;
            }
            try
            {
                var decoded = TextureDecodeUtility.DecodeTextureData(bytes, false);
                var mask = LightMaskTextureCache.BuildMaskPixels(decoded.PixelData);
                var pixels = new byte[mask.Length];
                for (var i = 0; i < mask.Length; i++) pixels[i] = mask[i].A;
                var factor = (int)MathF.Round(layer.PixelSize);
                var originalOrigin = LightmapRenderer.GetPaintedMaskOrigin(entity.X, entity.Y, decoded.Width, decoded.Height, layer.PixelSize);
                var expandedOrigin = LightmapRenderer.GetPaintedMaskOrigin(entity.X, entity.Y, decoded.Width * factor, decoded.Height * factor, 1);
                var extraWidth = Math.Max(0, (int)MathF.Round(expandedOrigin.X - originalOrigin.X)) * 2;
                var extraHeight = Math.Max(0, (int)MathF.Round(expandedOrigin.Y - originalOrigin.Y)) * 2;
                canvas = LightMaskPaintCanvas.ExpandSourcePixels(decoded.Width, decoded.Height, pixels, factor, extraWidth, extraHeight);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _builderStatus = $"could not reopen painted mask: {ex.Message}";
                _builderLightMaskPaintEntityId = string.Empty;
                return;
            }
        }

        canvas ??= new LightMaskPaintCanvas(side, side);
        // Saved masks and retained drafts must also follow an increased light radius.
        // Grow by padding, so previously painted pixels stay at the same world positions.
        canvas = canvas.EnsureMinimumSize(side, side);
        _builderLightMaskCanvas = canvas;
        _builderLightMaskPaintEntityIndex = _builderSelectedEntityIndex;
        _builderLightMaskPaintOrigin = LightmapRenderer.GetPaintedMaskOrigin(entity.X, entity.Y, canvas.Width, canvas.Height, GarrisonBuilderMaskPixelSize);
        _builderLightMaskPaintErase = false;
        _builderLightMaskPaintPaletteOpen = true;
        _builderLightMaskPaintLineStart = _builderLightMaskPaintPreviousPixel = null;
        _builderLightMaskBrushSliderDragging = false;
        _builderLightMaskPaintTexture = new Texture2D(GraphicsDevice, canvas.Width, canvas.Height);
        _builderLightMaskPaintPixels = new Color[canvas.Width * canvas.Height];
        RefreshGarrisonBuilderPaintMaskTexture();
        _builderStatus = "paint mask: drag to paint; Shift-click two endpoints; right-drag erases; Enter applies; Esc cancels; P palette";
    }

    private void RefreshGarrisonBuilderPaintMaskTexture()
    {
        if (_builderLightMaskCanvas is null || _builderLightMaskPaintPixels is null || _builderLightMaskPaintTexture is null) return;
        var source = _builderLightMaskCanvas.Pixels;
        for (var i = 0; i < source.Length; i++)
            _builderLightMaskPaintPixels[i] = new Color(source[i], source[i], source[i], source[i]);
        _builderLightMaskPaintTexture.SetData(_builderLightMaskPaintPixels);
    }

    private bool UpdateGarrisonBuilderLightMaskPainting(KeyboardState keyboard, MouseState mouse, float deltaSeconds)
    {
        var canvas = _builderLightMaskCanvas;
        if (canvas is null) return false;
        if (IsKeyPressed(keyboard, Keys.Escape)) { FinishGarrisonBuilderLightMaskPainting(false); return true; }
        if (IsKeyPressed(keyboard, Keys.Enter)) { FinishGarrisonBuilderLightMaskPainting(true); return true; }
        if (IsKeyPressed(keyboard, Keys.P)) _builderLightMaskPaintPaletteOpen = !_builderLightMaskPaintPaletteOpen;
        if (_builderCtrlHeld && (IsKeyPressed(keyboard, Keys.Z) || IsKeyPressed(keyboard, Keys.Y)))
        {
            EndGarrisonBuilderMaskPaintStroke();
            var redo = IsKeyPressed(keyboard, Keys.Y) || _builderShiftHeld;
            if (redo ? canvas.Redo() : canvas.Undo()) RefreshGarrisonBuilderPaintMaskTexture();
            _builderLightMaskPaintLineStart = null;
            return true;
        }

        var viewport = GetGarrisonBuilderMaskPaintViewport();
        var help = GetGarrisonBuilderMaskPaintHelpBounds(viewport);
        var palette = GetGarrisonBuilderMaskPaintPaletteBounds();
        var slider = GetGarrisonBuilderMaskBrushSliderBounds(palette);
        var leftPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
        if (mouse.LeftButton == ButtonState.Released) _builderLightMaskBrushSliderDragging = false;
        if (_builderLightMaskPaintPaletteOpen && leftPressed && slider.Contains(mouse.Position)) _builderLightMaskBrushSliderDragging = true;
        if (_builderLightMaskBrushSliderDragging)
        {
            EndGarrisonBuilderMaskPaintStroke();
            var fraction = Math.Clamp((mouse.X - slider.Left) / (float)Math.Max(1, slider.Width - 1), 0f, 1f);
            _builderLightMaskPaintBrushSize = 2 + (int)MathF.Round(fraction * 2f);
            _builderLightMaskPaintLineStart = null;
            return true;
        }
        var onPalette = _builderLightMaskPaintPaletteOpen && palette.Contains(mouse.Position);
        if (onPalette)
        {
            EndGarrisonBuilderMaskPaintStroke();
            if (leftPressed) HandleGarrisonBuilderMaskPaintPaletteClick(mouse.Position, palette);
            return true;
        }
        if (_builderUseModernUi)
        {
            UpdateModernGarrisonBuilderCamera(keyboard, mouse, deltaSeconds);
            UpdateModernGarrisonBuilderZoom(mouse);
        }
        var rightPressed = mouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Released;
        var down = mouse.LeftButton == ButtonState.Pressed || mouse.RightButton == ButtonState.Pressed;
        if (!down) { EndGarrisonBuilderMaskPaintStroke(); return true; }
        if (mouse.MiddleButton == ButtonState.Pressed || !viewport.Contains(mouse.Position) || help.Contains(mouse.Position))
        {
            EndGarrisonBuilderMaskPaintStroke();
            return true;
        }
        var world = _builderUseModernUi ? BuilderScreenToWorld(mouse.Position) : mouse.Position.ToVector2() + _builderCamera;
        if (!TryGetGarrisonBuilderMaskPaintPixel(world, out var pixel))
        {
            EndGarrisonBuilderMaskPaintStroke();
            return true;
        }
        var erase = _builderLightMaskPaintErase || mouse.RightButton == ButtonState.Pressed;
        if (_builderShiftHeld)
        {
            EndGarrisonBuilderMaskPaintStroke();
            if (!leftPressed && !rightPressed) return true;
            if (_builderLightMaskPaintLineStart is int start)
            {
                if (canvas.PaintLine(start, pixel, erase, _builderLightMaskPaintBrushSize)) RefreshGarrisonBuilderPaintMaskTexture();
                canvas.CommitStroke();
                _builderLightMaskPaintLineStart = null;
            }
            else _builderLightMaskPaintLineStart = pixel;
            return true;
        }
        if (leftPressed || rightPressed)
        {
            EndGarrisonBuilderMaskPaintStroke();
            _builderLightMaskPaintStroke = true;
            _builderLightMaskPaintLineStart = null;
        }
        if (!_builderLightMaskPaintStroke) return true;
        var changed = _builderLightMaskPaintPreviousPixel is int previous
            ? canvas.PaintLine(previous, pixel, erase, _builderLightMaskPaintBrushSize) : canvas.PaintBrush(pixel, erase, _builderLightMaskPaintBrushSize);
        _builderLightMaskPaintPreviousPixel = pixel;
        if (changed) RefreshGarrisonBuilderPaintMaskTexture();
        return true;
    }

    private void EndGarrisonBuilderMaskPaintStroke()
    {
        _builderLightMaskCanvas?.CommitStroke();
        _builderLightMaskPaintStroke = false;
        _builderLightMaskPaintPreviousPixel = null;
    }

    private bool FinishGarrisonBuilderLightMaskPainting(bool apply)
    {
        var canvas = _builderLightMaskCanvas;
        if (canvas is null) return true;
        EndGarrisonBuilderMaskPaintStroke();
        if (apply && _builderLightMaskPaintEntityIndex >= 0 && _builderLightMaskPaintEntityIndex < _builderEntities.Count)
        {
            try
            {
                using var image = new Image<Rgba32>(canvas.Width, canvas.Height);
                var pixels = canvas.Pixels;
                for (var y = 0; y < canvas.Height; y++)
                    for (var x = 0; x < canvas.Width; x++) image[x, y] = new Rgba32(255, 255, 255, pixels[y * canvas.Width + x]);
                using var stream = new MemoryStream();
                image.SaveAsPng(stream);
                var resources = new Dictionary<string, CustomMapBuilderResource>(_builderDocument.Resources, StringComparer.OrdinalIgnoreCase);
                var name = "painted_light_mask";
                var suffix = 2;
                while (resources.ContainsKey(name)) name = $"painted_light_mask_{suffix++}";
                resources[name] = new CustomMapBuilderResource(name, string.Empty, CustomMapBuilderResourceKind.GenericImage, stream.ToArray());
                var entity = _builderEntities[_builderLightMaskPaintEntityIndex];
                var properties = new Dictionary<string, string>(entity.Properties, StringComparer.OrdinalIgnoreCase);
                properties.TryGetValue(MapLightMetadata.MaskKey, out var previousMask);
                properties.TryGetValue(BuilderPaintedLightMask.ResourceKey, out var previousPaintedMask);
                properties[MapLightMetadata.MaskKey] = name;
                properties[MapLightMetadata.MaskPixelSizeKey] = GarrisonBuilderMaskPixelSize.ToString(CultureInfo.InvariantCulture);
                properties[MapLightMetadata.ColorKey] = _builderLightMaskPaintColor;
                properties[BuilderPaintedLightMask.BrushSizeKey] = _builderLightMaskPaintBrushSize.ToString(CultureInfo.InvariantCulture);
                BuilderPaintedLightMask.RememberActive(properties);
                RecordGarrisonBuilderHistory();
                _builderDocument = _builderDocument with { Resources = resources };
                _builderEntities[_builderLightMaskPaintEntityIndex] = entity with { Properties = properties };
                UpdateGarrisonBuilderDocumentEntities();
                if (!string.IsNullOrWhiteSpace(previousMask)) PruneGarrisonBuilderResourceIfUnreferenced(previousMask);
                if (!string.IsNullOrWhiteSpace(previousPaintedMask)) PruneGarrisonBuilderResourceIfUnreferenced(previousPaintedMask);
                _builderDirty = true;
                _builderStatus = "painted light mask applied";
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _builderStatus = $"could not apply mask: {ex.Message}";
                return false;
            }
        }
        else
        {
            _builderLightMaskDrafts[_builderLightMaskPaintEntityId] = (canvas, _builderLightMaskPaintColor, _builderLightMaskPaintBrushSize);
            _builderStatus = "mask painting closed; draft retained";
        }
        _builderLightMaskPaintTexture?.Dispose();
        _builderLightMaskPaintTexture = null;
        _builderLightMaskPaintPixels = null;
        _builderLightMaskCanvas = null;
        _builderLightMaskPaintPaletteOpen = _builderLightMaskPaintStroke = false;
        _builderLightMaskPaintLineStart = _builderLightMaskPaintPreviousPixel = null;
        _builderLightMaskPaintEntityIndex = -1;
        _builderLightMaskPaintEntityId = string.Empty;
        _builderLightMaskBrushSliderDragging = false;
        return true;
    }

    private Rectangle GetGarrisonBuilderMaskPaintPaletteBounds()
    {
        var viewport = GetGarrisonBuilderMaskPaintViewport();
        return new Rectangle(viewport.X + 10, viewport.Y + 10, 226, 210);
    }

    private static Rectangle GetGarrisonBuilderMaskBrushSliderBounds(Rectangle palette) =>
        new(palette.X + 12, palette.Y + 123, palette.Width - 24, 17);

    private bool TryGetGarrisonBuilderMaskPaintPixel(Vector2 world, out int pixel)
    {
        pixel = -1;
        if (_builderLightMaskCanvas is not { } canvas) return false;
        var local = (world - _builderLightMaskPaintOrigin) / GarrisonBuilderMaskPixelSize;
        if (local.X < 0 || local.Y < 0 || local.X >= canvas.Width || local.Y >= canvas.Height) return false;
        // Position snaps to the 1px canvas grid independently of the brush size.
        var x = (int)MathF.Floor(local.X);
        var y = (int)MathF.Floor(local.Y);
        pixel = y * canvas.Width + x;
        return true;
    }

    private static Rectangle GetGarrisonBuilderMaskPaintHelpBounds(Rectangle viewport) =>
        new(viewport.X + 8, viewport.Bottom - 58, Math.Min(550, viewport.Width - 16), 50);

    private static Rectangle GetGarrisonBuilderMaskPaintCell(Rectangle palette, int cell) =>
        new(palette.X + 10 + cell % 6 * 34, palette.Y + 30 + cell / 6 * 24, 29, 19);

    private void HandleGarrisonBuilderMaskPaintPaletteClick(Point position, Rectangle palette)
    {
        if (new Rectangle(palette.Right - 25, palette.Y + 4, 20, 20).Contains(position))
        { _builderLightMaskPaintPaletteOpen = false; return; }
        for (var i = 0; i < GarrisonBuilderLightPalette.Length; i++)
        {
            if (!GetGarrisonBuilderMaskPaintCell(palette, i).Contains(position)) continue;
            _builderLightMaskPaintColor = GarrisonBuilderLightPalette[i].Hex;
            _builderLightMaskPaintErase = false;
            _builderLightMaskPaintLineStart = null;
            return;
        }
        if (new Rectangle(palette.X + 10, palette.Y + 147, 98, 23).Contains(position))
        { _builderLightMaskPaintErase = false; _builderLightMaskPaintLineStart = null; }
        if (new Rectangle(palette.X + 118, palette.Y + 147, 98, 23).Contains(position))
        { _builderLightMaskPaintErase = true; _builderLightMaskPaintLineStart = null; }
        if (new Rectangle(palette.X + 10, palette.Y + 177, 98, 23).Contains(position)) FinishGarrisonBuilderLightMaskPainting(true);
        if (new Rectangle(palette.X + 118, palette.Y + 177, 98, 23).Contains(position)) FinishGarrisonBuilderLightMaskPainting(false);
    }

    private void DrawGarrisonBuilderLightMaskPainting(MouseState mouse)
    {
        var canvas = _builderLightMaskCanvas;
        if (canvas is null || _builderLightMaskPaintTexture is null) return;
        MapLightingColor.TryParse(_builderLightMaskPaintColor, out var color);
        var tint = new Color(color.R, color.G, color.B);
        var position = BuilderWorldToScreen(_builderLightMaskPaintOrigin);
        var scale = _builderUseModernUi ? GetGarrisonBuilderMapVisualScale() : 1f;
        var viewport = GetGarrisonBuilderMaskPaintViewport();
        var previousScissor = GraphicsDevice.ScissorRectangle;
        _spriteBatch.End();
        GraphicsDevice.ScissorRectangle = viewport;
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: GarrisonBuilderLightingScissorState);
        var bounds = new Rectangle((int)position.X, (int)position.Y, (int)(canvas.Width * GarrisonBuilderMaskPixelSize * scale), (int)(canvas.Height * GarrisonBuilderMaskPixelSize * scale));
        DrawGarrisonBuilderRectangleOutline(bounds, new Color(255, 230, 110));
        var palette = GetGarrisonBuilderMaskPaintPaletteBounds();
        var help = GetGarrisonBuilderMaskPaintHelpBounds(viewport);
        var world = _builderUseModernUi ? BuilderScreenToWorld(mouse.Position) : mouse.Position.ToVector2() + _builderCamera;
        if (viewport.Contains(mouse.Position) && !help.Contains(mouse.Position)
            && !(_builderLightMaskPaintPaletteOpen && palette.Contains(mouse.Position))
            && !_builderLightMaskBrushSliderDragging && mouse.MiddleButton != ButtonState.Pressed
            && TryGetGarrisonBuilderMaskPaintPixel(world, out var hoverPixel))
        {
            var hover = GetGarrisonBuilderMaskPaintBrushScreenBounds(hoverPixel, position, scale);
            var erase = _builderLightMaskPaintErase || mouse.RightButton == ButtonState.Pressed;
            _spriteBatch.Draw(_pixel, hover, (erase ? new Color(255, 90, 90) : tint) * 0.35f);
            DrawGarrisonBuilderRectangleOutline(hover, Color.White * 0.6f);
        }
        if (_builderLightMaskPaintLineStart is int start)
        {
            DrawGarrisonBuilderRectangleOutline(GetGarrisonBuilderMaskPaintBrushScreenBounds(start, position, scale), Color.White);
        }
        _spriteBatch.End();
        GraphicsDevice.ScissorRectangle = previousScissor;
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
        DrawMenuPanelBackdrop(help, 0.96f);
        DrawBitmapFontText("Paint mask: drag | right-drag erase | Shift-click endpoints", new Vector2(help.X + 8, help.Y + 7), Color.White, GetModernBuilderTextScale(0.65f));
        DrawBitmapFontText("Enter apply | Esc cancel | Ctrl-Z/Y undo/redo | P palette", new Vector2(help.X + 8, help.Y + 27), Color.White, GetModernBuilderTextScale(0.65f));
        if (!_builderLightMaskPaintPaletteOpen) return;
        DrawMenuPanelBackdrop(palette, 0.98f);
        DrawBitmapFontText("Mask / light colour", new Vector2(palette.X + 10, palette.Y + 7), Color.White, GetModernBuilderTextScale(0.65f));
        DrawBuilderMenuButton(new Rectangle(palette.Right - 25, palette.Y + 4, 20, 20), "X", false);
        for (var i = 0; i < GarrisonBuilderLightPalette.Length; i++)
        {
            var cell = GetGarrisonBuilderMaskPaintCell(palette, i);
            MapLightingColor.TryParse(GarrisonBuilderLightPalette[i].Hex, out var swatch);
            _spriteBatch.Draw(_pixel, cell, new Color(swatch.R, swatch.G, swatch.B));
            if (GarrisonBuilderLightPalette[i].Hex == _builderLightMaskPaintColor) DrawGarrisonBuilderRectangleOutline(cell, Color.White);
        }
        DrawBitmapFontText($"Size: {_builderLightMaskPaintBrushSize}px", new Vector2(palette.X + 10, palette.Y + 103), Color.White, GetModernBuilderTextScale(0.65f));
        DrawBitmapFontText("2px - 4px", new Vector2(palette.Right - 82, palette.Y + 103), new Color(190, 180, 165), GetModernBuilderTextScale(0.65f));
        var slider = GetGarrisonBuilderMaskBrushSliderBounds(palette);
        _spriteBatch.Draw(_pixel, new Rectangle(slider.X, slider.Center.Y - 2, slider.Width, 4), new Color(90, 85, 80));
        var thumbX = slider.X + (int)((slider.Width - 1) * (_builderLightMaskPaintBrushSize - 2) / 2f);
        _spriteBatch.Draw(_pixel, new Rectangle(thumbX - 3, slider.Y + 2, 7, slider.Height - 4), Color.White);
        DrawBuilderMenuButton(new Rectangle(palette.X + 10, palette.Y + 147, 98, 23), "Paint", !_builderLightMaskPaintErase);
        DrawBuilderMenuButton(new Rectangle(palette.X + 118, palette.Y + 147, 98, 23), "Erase", _builderLightMaskPaintErase);
        DrawBuilderMenuButton(new Rectangle(palette.X + 10, palette.Y + 177, 98, 23), "Apply", false);
        DrawBuilderMenuButton(new Rectangle(palette.X + 118, palette.Y + 177, 98, 23), "Cancel", false);
    }

    private Rectangle GetGarrisonBuilderMaskPaintBrushScreenBounds(int pixel, Vector2 origin, float scale)
    {
        var canvas = _builderLightMaskCanvas!;
        var x = pixel % canvas.Width;
        var y = pixel / canvas.Width;
        var width = Math.Min(_builderLightMaskPaintBrushSize, canvas.Width - x);
        var height = Math.Min(_builderLightMaskPaintBrushSize, canvas.Height - y);
        var left = origin.X + x * GarrisonBuilderMaskPixelSize * scale;
        var top = origin.Y + y * GarrisonBuilderMaskPixelSize * scale;
        return new Rectangle((int)MathF.Floor(left), (int)MathF.Floor(top),
            Math.Max(1, (int)MathF.Ceiling(left + width * GarrisonBuilderMaskPixelSize * scale) - (int)MathF.Floor(left)),
            Math.Max(1, (int)MathF.Ceiling(top + height * GarrisonBuilderMaskPixelSize * scale) - (int)MathF.Floor(top)));
    }
}
