#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private static readonly Color GarrisonBuilderForegroundSpritePlaceholderColor = new(180, 120, 220, 220);

    private void SyncGarrisonBuilderForegroundSpriteResources()
    {
        var resources = new Dictionary<string, CustomMapBuilderResource>(
            _builderDocument.Resources,
            StringComparer.OrdinalIgnoreCase);
        var changed = false;
        for (var index = 0; index < _builderEntities.Count; index += 1)
        {
            var entity = _builderEntities[index];
            if (!ForegroundSpriteMetadata.IsForegroundSpriteEntityType(entity.Type))
            {
                continue;
            }

            if (!entity.Properties.TryGetValue(ForegroundSpriteMetadata.ImagePropertyKey, out var imageValue)
                || string.IsNullOrWhiteSpace(imageValue))
            {
                continue;
            }

            if (resources.ContainsKey(imageValue))
            {
                continue;
            }

            if (!File.Exists(imageValue))
            {
                continue;
            }

            var resourceName = ForegroundSpriteMetadata.FindOrAssignImageResource(imageValue, resources);
            if (!entity.Properties.TryGetValue(ForegroundSpriteMetadata.ImagePropertyKey, out var existing)
                || !existing.Equals(resourceName, StringComparison.OrdinalIgnoreCase))
            {
                var properties = new Dictionary<string, string>(entity.Properties, StringComparer.OrdinalIgnoreCase)
                {
                    [ForegroundSpriteMetadata.ImagePropertyKey] = resourceName,
                };
                _builderEntities[index] = entity with { Properties = properties };
                changed = true;
            }
        }

        if (!changed && resources.Count == _builderDocument.Resources.Count)
        {
            return;
        }

        _builderDocument = _builderDocument with { Resources = resources };
    }

    private void DrawGarrisonBuilderForegroundSpritesForLayer(ForegroundSpriteLayerKind layer)
    {
        var camera = _builderUseModernUi ? _builderCamera : Vector2.Zero;
        foreach (var (_, entity) in EnumerateGarrisonBuilderForegroundSpritesForLayer(layer))
        {
            DrawGarrisonBuilderForegroundSpriteEntity(entity, camera, Color.White);
        }
    }

    private IEnumerable<(int Index, CustomMapBuilderEntity Entity)> EnumerateGarrisonBuilderForegroundSpritesForLayer(
        ForegroundSpriteLayerKind layer)
    {
        var matches = new List<(int Index, CustomMapBuilderEntity Entity, int RelativeZ)>();
        for (var index = 0; index < _builderEntities.Count; index += 1)
        {
            if (IsGarrisonBuilderEntityHidden(index))
            {
                continue;
            }

            var entity = _builderEntities[index];
            if (!ForegroundSpriteMetadata.IsForegroundSpriteEntityType(entity.Type))
            {
                continue;
            }

            entity.Properties.TryGetValue(ForegroundSpriteMetadata.LayerPropertyKey, out var layerValue);
            if (ForegroundSpriteMetadata.ParseLayer(layerValue) != layer)
            {
                continue;
            }

            entity.Properties.TryGetValue(ForegroundSpriteMetadata.RelativeZPropertyKey, out var relativeZValue);
            matches.Add((index, entity, ForegroundSpriteMetadata.ParseRelativeZ(relativeZValue)));
        }

        return matches
            .OrderBy(static entry => entry.RelativeZ)
            .ThenBy(static entry => entry.Index)
            .Select(static entry => (entry.Index, entry.Entity));
    }

    private void DrawGarrisonBuilderForegroundSpriteEntity(CustomMapBuilderEntity entity, Vector2 camera, Color overlayTint)
    {
        if (!TryGetGarrisonBuilderForegroundSpriteDrawBounds(entity, out var centerX, out var centerY, out var width, out var height))
        {
            return;
        }

        Vector2 screenPosition;
        if (_builderUseModernUi)
        {
            screenPosition = BuilderWorldToScreen(new Vector2(centerX, centerY));
        }
        else
        {
            screenPosition = new Vector2(centerX, centerY) - camera;
        }

        var screenScale = _builderUseModernUi ? GetGarrisonBuilderMapVisualScale() : 1f;
        var screenWidth = MathF.Max(1f, width * screenScale);
        var screenHeight = MathF.Max(1f, height * screenScale);
        var drawRect = new Rectangle(
            (int)MathF.Floor(screenPosition.X - (screenWidth * 0.5f)),
            (int)MathF.Floor(screenPosition.Y - (screenHeight * 0.5f)),
            Math.Max(1, (int)MathF.Ceiling(screenWidth)),
            Math.Max(1, (int)MathF.Ceiling(screenHeight)));

        var configuration = ForegroundSpriteMetadata.ParseConfiguration(entity.Properties);
        var tint = Color.White;
        if (configuration.Jungle)
        {
            tint = Color.White * configuration.OutsideOpacity;
        }

        tint = new Color(tint.ToVector4() * overlayTint.ToVector4());

        if (configuration.HasImage
            && entity.Properties.TryGetValue(ForegroundSpriteMetadata.ImagePropertyKey, out var resourceName)
            && !string.IsNullOrWhiteSpace(resourceName))
        {
            var texture = GetGarrisonBuilderResourceTexture(resourceName.Trim());
            if (texture is not null)
            {
                if (configuration.Tile)
                {
                    var tileScreenWidth = MathF.Max(1f, texture.Width * configuration.Scale * screenScale);
                    var tileScreenHeight = MathF.Max(1f, texture.Height * configuration.Scale * screenScale);
                    MapSpriteTileRendering.DrawTiledSprite(
                        _spriteBatch,
                        texture,
                        drawRect,
                        tileScreenWidth,
                        tileScreenHeight,
                        configuration.TileAnchor,
                        tint);
                }
                else
                {
                    _spriteBatch.Draw(texture, drawRect, tint);
                }

                return;
            }
        }

        _spriteBatch.Draw(_pixel, drawRect, Color.Lerp(GarrisonBuilderForegroundSpritePlaceholderColor, overlayTint, overlayTint.A / 255f));
        DrawGarrisonBuilderRectangleOutline(drawRect, Color.Black * 0.65f);
    }

    private bool TryGetGarrisonBuilderForegroundSpriteDrawBounds(
        CustomMapBuilderEntity entity,
        out float centerX,
        out float centerY,
        out float width,
        out float height)
    {
        centerX = centerY = width = height = 0f;
        if (!ForegroundSpriteMetadata.IsForegroundSpriteEntityType(entity.Type))
        {
            return false;
        }

        centerX = entity.X;
        centerY = entity.Y;
        var configuration = ForegroundSpriteMetadata.ParseConfiguration(entity.Properties);
        var pixelWidth = 42;
        var pixelHeight = 42;
        if (configuration.HasImage
            && entity.Properties.TryGetValue(ForegroundSpriteMetadata.ImagePropertyKey, out var resourceName)
            && !string.IsNullOrWhiteSpace(resourceName)
            && _builderDocument.Resources.TryGetValue(resourceName.Trim(), out var resource)
            && CustomMapBuilderResourceCodec.TryGetResourceBytes(resource, out var bytes)
            && ForegroundSpriteMetadata.TryParsePngDimensions(bytes, out var decodedWidth, out var decodedHeight))
        {
            pixelWidth = decodedWidth;
            pixelHeight = decodedHeight;
        }

        (width, height) = ForegroundSpriteMetadata.ResolveWorldDimensions(
            pixelWidth,
            pixelHeight,
            configuration.Scale,
            configuration);
        return width > 0f && height > 0f;
    }

    private bool TryGetGarrisonBuilderForegroundSpriteWorldBounds(
        CustomMapBuilderEntity entity,
        out float left,
        out float top,
        out float width,
        out float height)
    {
        left = top = width = height = 0f;
        if (!TryGetGarrisonBuilderForegroundSpriteDrawBounds(entity, out var centerX, out var centerY, out width, out height))
        {
            return false;
        }

        var configuration = ForegroundSpriteMetadata.ParseConfiguration(entity.Properties);
        var pixelWidth = Math.Max(1, (int)MathF.Round(width / MathF.Max(0.001f, configuration.Scale)));
        var pixelHeight = Math.Max(1, (int)MathF.Round(height / MathF.Max(0.001f, configuration.Scale)));
        if (TryGetGarrisonBuilderForegroundSpritePixelSize(entity, out var decodedWidth, out var decodedHeight))
        {
            pixelWidth = decodedWidth;
            pixelHeight = decodedHeight;
        }

        (left, top, width, height) = ForegroundSpriteMetadata.ResolveWorldBounds(
            centerX,
            centerY,
            pixelWidth,
            pixelHeight,
            configuration.Scale,
            configuration);
        return true;
    }

    private bool TryGetGarrisonBuilderForegroundSpritePixelSize(
        CustomMapBuilderEntity entity,
        out int width,
        out int height)
    {
        width = height = 0;
        if (!entity.Properties.TryGetValue(ForegroundSpriteMetadata.ImagePropertyKey, out var resourceName)
            || string.IsNullOrWhiteSpace(resourceName)
            || !_builderDocument.Resources.TryGetValue(resourceName.Trim(), out var resource)
            || !CustomMapBuilderResourceCodec.TryGetResourceBytes(resource, out var bytes)
            || !ForegroundSpriteMetadata.TryParsePngDimensions(bytes, out width, out height))
        {
            return false;
        }

        return true;
    }

    private bool TryDrawGarrisonBuilderForegroundSpriteEntity(
        CustomMapBuilderEntityDefinition definition,
        CustomMapBuilderEntity entity,
        Color overlayTint)
    {
        if (!ForegroundSpriteMetadata.IsForegroundSpriteEntityType(definition.Type))
        {
            return false;
        }

        DrawGarrisonBuilderForegroundSpriteEntity(
            entity,
            _builderUseModernUi ? _builderCamera : Vector2.Zero,
            overlayTint);
        return true;
    }

    private bool IsGarrisonBuilderForegroundSpriteResizable(CustomMapBuilderEntity entity)
    {
        if (!ForegroundSpriteMetadata.IsForegroundSpriteEntityType(entity.Type))
        {
            return false;
        }

        var configuration = ForegroundSpriteMetadata.ParseConfiguration(entity.Properties);
        return configuration.HasImage;
    }

    private static bool IsGarrisonBuilderForegroundSpritePropertyKey(string key)
    {
        return key.Equals(ForegroundSpriteMetadata.ImagePropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(ForegroundSpriteMetadata.LayerPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(ForegroundSpriteMetadata.RelativeZPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(ForegroundSpriteMetadata.ScalePropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapSpriteTileMetadata.TilePropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapSpriteTileMetadata.TileAnchorPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(ForegroundSpriteMetadata.JunglePropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(ForegroundSpriteMetadata.OutsideOpacityPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(ForegroundSpriteMetadata.InsideOpacityPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(ForegroundSpriteMetadata.BoundaryPropertyKey, StringComparison.OrdinalIgnoreCase);
    }

    private List<string> OrderGarrisonBuilderForegroundSpritePropertyRows(List<string> rows)
    {
        string[] order =
        [
            ForegroundSpriteMetadata.ImagePropertyKey,
            ForegroundSpriteMetadata.LayerPropertyKey,
            ForegroundSpriteMetadata.RelativeZPropertyKey,
            ForegroundSpriteMetadata.ScalePropertyKey,
            MapSpriteTileMetadata.TilePropertyKey,
            MapSpriteTileMetadata.TileAnchorPropertyKey,
            ForegroundSpriteMetadata.JunglePropertyKey,
            ForegroundSpriteMetadata.OutsideOpacityPropertyKey,
            ForegroundSpriteMetadata.InsideOpacityPropertyKey,
            ForegroundSpriteMetadata.BoundaryPropertyKey,
        ];
        var ordered = new List<string>(rows.Count);
        foreach (var key in order)
        {
            if (!ForegroundSpriteMetadata.ShouldShowJungleDependentProperty(_builderPropertyEditorValues, key)
                || !MapSpriteTileMetadata.ShouldShowTileDependentProperty(_builderPropertyEditorValues, key))
            {
                continue;
            }

            var match = rows.FirstOrDefault(existing => existing.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                ordered.Add(match);
            }
        }

        foreach (var key in rows)
        {
            if (!ForegroundSpriteMetadata.ShouldShowJungleDependentProperty(_builderPropertyEditorValues, key)
                || !MapSpriteTileMetadata.ShouldShowTileDependentProperty(_builderPropertyEditorValues, key))
            {
                continue;
            }

            if (!ordered.Any(existing => existing.Equals(key, StringComparison.OrdinalIgnoreCase)))
            {
                ordered.Add(key);
            }
        }

        return ordered;
    }

    private bool TryHandleGarrisonBuilderForegroundSpritePropertyClick(string key, string value)
    {
        if (!TryGetGarrisonBuilderEditedEntityType(out var entityType)
            || !ForegroundSpriteMetadata.IsForegroundSpriteEntityType(entityType))
        {
            return false;
        }

        if (key.Equals(ForegroundSpriteMetadata.LayerPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            _builderPropertyEditorValues[key] = ForegroundSpriteMetadata.CycleLayerPropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
            return true;
        }

        if (key.Equals(ForegroundSpriteMetadata.BoundaryPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            _builderPropertyEditorValues[key] = ForegroundSpriteMetadata.CycleBoundaryPropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
            return true;
        }

        if (key.Equals(ForegroundSpriteMetadata.JunglePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            var next = !ForegroundSpriteMetadata.ParseJungle(value);
            _builderPropertyEditorValues[key] = ForegroundSpriteMetadata.ToJunglePropertyValue(next);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
            return true;
        }

        if (key.Equals(ForegroundSpriteMetadata.ImagePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            TryChooseGarrisonBuilderForegroundSpriteImage();
            return true;
        }

        if (TryCycleGarrisonBuilderMapSpriteTileAnchorProperty(key, value))
        {
            return true;
        }

        return false;
    }

    private bool TryGetGarrisonBuilderForegroundSpritePixelSizeFromEditor(out int pixelWidth, out int pixelHeight)
    {
        pixelWidth = 42;
        pixelHeight = 42;
        if (!_builderPropertyEditorValues.TryGetValue(ForegroundSpriteMetadata.ImagePropertyKey, out var resourceName)
            || string.IsNullOrWhiteSpace(resourceName)
            || !_builderDocument.Resources.TryGetValue(resourceName.Trim(), out var resource)
            || !CustomMapBuilderResourceCodec.TryGetResourceBytes(resource, out var bytes)
            || !ForegroundSpriteMetadata.TryParsePngDimensions(bytes, out pixelWidth, out pixelHeight))
        {
            return false;
        }

        return true;
    }

    private void TryChooseGarrisonBuilderForegroundSpriteImage()
    {
        if (!TryChooseGarrisonBuilderFile(
                "Select foreground sprite image",
                "Image files (*.png;*.gif)|*.png;*.gif|PNG files (*.png)|*.png|GIF files (*.gif)|*.gif|All files (*.*)|*.*",
                _builderResourcePathBuffer,
                out var selectedPath))
        {
            return;
        }

        RecordGarrisonBuilderHistory();
        _builderResourcePathBuffer = selectedPath;
        var resources = new Dictionary<string, CustomMapBuilderResource>(
            _builderDocument.Resources,
            StringComparer.OrdinalIgnoreCase);
        var resourceName = ForegroundSpriteMetadata.FindOrAssignImageResource(selectedPath, resources);
        _builderDocument = _builderDocument with { Resources = resources };
        _builderPropertyEditorValues[ForegroundSpriteMetadata.ImagePropertyKey] = resourceName;
        ApplyGarrisonBuilderPropertyEditorLivePreview();
        MarkGarrisonBuilderPropertyEditorChanged();
        _builderDirty = true;
        _builderStatus = $"foreground sprite {resourceName}";
    }

    private string GetGarrisonBuilderForegroundSpritePropertyDisplayLabel(string key, string value)
    {
        if (key.Equals(ForegroundSpriteMetadata.ImagePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(value) ? "Image: none" : $"Image: {value}";
        }

        if (key.Equals(ForegroundSpriteMetadata.LayerPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Layer: {ForegroundSpriteMetadata.GetLayerDisplayLabel(value)}";
        }

        if (key.Equals(ForegroundSpriteMetadata.RelativeZPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return "Relative Z";
        }

        if (key.Equals(ForegroundSpriteMetadata.ScalePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Scale: {ForegroundSpriteMetadata.ParseScale(value):0.###}";
        }

        if (key.Equals(MapSpriteTileMetadata.TilePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return "Tile";
        }

        if (key.Equals(MapSpriteTileMetadata.TileAnchorPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Tile anchor: {MapSpriteTileMetadata.GetTileAnchorDisplayLabel(value)}";
        }

        if (key.Equals(ForegroundSpriteMetadata.JunglePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return "Jungle";
        }

        if (key.Equals(ForegroundSpriteMetadata.OutsideOpacityPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Outside opacity: {ForegroundSpriteMetadata.ParseOpacity(value, ForegroundSpriteMetadata.DefaultOutsideOpacity):0.##}";
        }

        if (key.Equals(ForegroundSpriteMetadata.InsideOpacityPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Inside opacity: {ForegroundSpriteMetadata.ParseOpacity(value, ForegroundSpriteMetadata.DefaultInsideOpacity):0.##}";
        }

        if (key.Equals(ForegroundSpriteMetadata.BoundaryPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Boundary: {ForegroundSpriteMetadata.GetBoundaryDisplayLabel(value)}";
        }

        return value;
    }

    private bool IsGarrisonBuilderForegroundSpriteRelativeZPropertyRow(string key)
    {
        return key.Equals(ForegroundSpriteMetadata.RelativeZPropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var entityType)
            && ForegroundSpriteMetadata.IsForegroundSpriteEntityType(entityType);
    }

    private bool IsGarrisonBuilderForegroundSpriteOpacityPropertyRow(string key)
    {
        return (key.Equals(ForegroundSpriteMetadata.OutsideOpacityPropertyKey, StringComparison.OrdinalIgnoreCase)
                || key.Equals(ForegroundSpriteMetadata.InsideOpacityPropertyKey, StringComparison.OrdinalIgnoreCase))
            && TryGetGarrisonBuilderEditedEntityType(out var entityType)
            && ForegroundSpriteMetadata.IsForegroundSpriteEntityType(entityType);
    }

    private void GetGarrisonBuilderForegroundSpriteRelativeZSliderLayout(
        Rectangle rowBounds,
        string value,
        float textScale,
        out string sliderDisplay,
        out Rectangle sliderBounds,
        out Rectangle digitBounds)
    {
        var relativeZ = ForegroundSpriteMetadata.ParseRelativeZ(value);
        sliderDisplay = $"< {relativeZ,2} >";
        var sliderWidth = MeasureBitmapFontWidth(sliderDisplay, textScale);
        const float rightPadding = 8f;
        var sliderX = rowBounds.Right - rightPadding - sliderWidth;
        var textHeight = MeasureBitmapFontHeight(textScale);
        var textY = rowBounds.Y + MathF.Max(2f, ((rowBounds.Height - textHeight) * 0.5f) - 1f);
        sliderBounds = new Rectangle(
            (int)MathF.Floor(sliderX),
            (int)MathF.Floor(textY),
            (int)MathF.Ceiling(sliderWidth),
            (int)MathF.Ceiling(textHeight));
        var digitText = relativeZ.ToString();
        var digitWidth = MeasureBitmapFontWidth(digitText, textScale);
        var digitX = sliderBounds.X + ((sliderBounds.Width - digitWidth) * 0.5f);
        digitBounds = new Rectangle(
            (int)MathF.Floor(digitX),
            sliderBounds.Y,
            (int)MathF.Ceiling(digitWidth),
            sliderBounds.Height);
    }

    private void DrawGarrisonBuilderForegroundSpriteRelativeZPropertyRow(
        Rectangle rowBounds,
        string value,
        MouseState mouse,
        float textScale,
        bool hovered)
    {
        GetGarrisonBuilderForegroundSpriteRelativeZSliderLayout(
            rowBounds,
            value,
            textScale,
            out var sliderDisplay,
            out var sliderBounds,
            out _);
        var textY = rowBounds.Y + MathF.Max(2f, ((rowBounds.Height - MeasureBitmapFontHeight(textScale)) * 0.5f) - 1f);
        var labelColor = _builderUseModernUi ? Color.White : Color.Black;
        var label = "Relative Z";
        var labelMaxWidth = MathF.Max(40f, sliderBounds.X - rowBounds.X - 10f);
        var displayLabel = TruncateGarrisonBuilderPropertyLabel(label, labelMaxWidth, textScale);
        if (_builderUseModernUi)
        {
            DrawBitmapFontText(displayLabel, new Vector2(rowBounds.X + 6f, textY), labelColor, textScale);
            DrawBitmapFontText(sliderDisplay, new Vector2(sliderBounds.X, textY), labelColor, textScale);
        }
        else
        {
            DrawGarrisonBuilderText(displayLabel, rowBounds.Location.ToVector2() + new Vector2(4f, 3f), labelColor, textScale);
            DrawGarrisonBuilderText(sliderDisplay, new Vector2(sliderBounds.X, rowBounds.Y + 3f), labelColor, textScale);
        }
    }

    private void GetGarrisonBuilderForegroundSpriteOpacitySliderLayout(
        Rectangle rowBounds,
        string value,
        float fallback,
        float textScale,
        out string sliderDisplay,
        out Rectangle sliderBounds,
        out Rectangle digitBounds)
    {
        var opacityPercent = (int)MathF.Round(ForegroundSpriteMetadata.ParseOpacity(value, fallback) * 100f);
        sliderDisplay = $"< {opacityPercent,3}% >";
        var sliderWidth = MeasureBitmapFontWidth(sliderDisplay, textScale);
        const float rightPadding = 8f;
        var sliderX = rowBounds.Right - rightPadding - sliderWidth;
        var textHeight = MeasureBitmapFontHeight(textScale);
        var textY = rowBounds.Y + MathF.Max(2f, ((rowBounds.Height - textHeight) * 0.5f) - 1f);
        sliderBounds = new Rectangle(
            (int)MathF.Floor(sliderX),
            (int)MathF.Floor(textY),
            (int)MathF.Ceiling(sliderWidth),
            (int)MathF.Ceiling(textHeight));
        var digitText = $"{opacityPercent}%";
        var digitWidth = MeasureBitmapFontWidth(digitText, textScale);
        var digitX = sliderBounds.X + ((sliderBounds.Width - digitWidth) * 0.5f);
        digitBounds = new Rectangle(
            (int)MathF.Floor(digitX),
            sliderBounds.Y,
            (int)MathF.Ceiling(digitWidth),
            sliderBounds.Height);
    }

    private bool TryHandleGarrisonBuilderForegroundSpriteRelativeZClick(
        string key,
        string value,
        Rectangle rowBounds,
        Point position)
    {
        if (!IsGarrisonBuilderForegroundSpriteRelativeZPropertyRow(key))
        {
            return false;
        }

        GetGarrisonBuilderForegroundSpriteRelativeZSliderLayout(
            rowBounds,
            value,
            GetGarrisonBuilderPropertyRowTextScale(),
            out _,
            out var sliderBounds,
            out var digitBounds);
        if (!sliderBounds.Contains(position))
        {
            return false;
        }

        if (digitBounds.Contains(position))
        {
            BeginGarrisonBuilderPropertyTextEdit(key, value, GarrisonBuilderPropertyEditMode.EditValue);
            return true;
        }

        var delta = position.X < sliderBounds.X + (sliderBounds.Width / 2) ? -1 : 1;
        _builderPropertyEditorValues[key] = ForegroundSpriteMetadata.ToRelativeZPropertyValue(
            ForegroundSpriteMetadata.ParseRelativeZ(value) + delta);
        ApplyGarrisonBuilderPropertyEditorLivePreview();
        MarkGarrisonBuilderPropertyEditorChanged();
        return true;
    }

    private bool TryHandleGarrisonBuilderForegroundSpriteOpacityClick(
        string key,
        string value,
        Rectangle rowBounds,
        Point position)
    {
        if (!IsGarrisonBuilderForegroundSpriteOpacityPropertyRow(key))
        {
            return false;
        }

        var fallback = key.Equals(ForegroundSpriteMetadata.InsideOpacityPropertyKey, StringComparison.OrdinalIgnoreCase)
            ? ForegroundSpriteMetadata.DefaultInsideOpacity
            : ForegroundSpriteMetadata.DefaultOutsideOpacity;
        GetGarrisonBuilderForegroundSpriteOpacitySliderLayout(
            rowBounds,
            value,
            fallback,
            GetGarrisonBuilderPropertyRowTextScale(),
            out _,
            out var sliderBounds,
            out var digitBounds);
        if (!sliderBounds.Contains(position))
        {
            return false;
        }

        if (digitBounds.Contains(position))
        {
            BeginGarrisonBuilderPropertyTextEdit(key, value, GarrisonBuilderPropertyEditMode.EditValue);
            return true;
        }

        var current = ForegroundSpriteMetadata.ParseOpacity(value, fallback);
        var delta = position.X < sliderBounds.X + (sliderBounds.Width / 2) ? -0.05f : 0.05f;
        _builderPropertyEditorValues[key] = ForegroundSpriteMetadata.ToOpacityPropertyValue(current + delta);
        ApplyGarrisonBuilderPropertyEditorLivePreview();
        MarkGarrisonBuilderPropertyEditorChanged();
        return true;
    }

    private void ApplyGarrisonBuilderForegroundSpriteResizeDrag(Vector2 world)
    {
        if (_builderSelectedEntityIndex < 0 || _builderSelectedEntityIndex >= _builderEntities.Count)
        {
            return;
        }

        world = SnapGarrisonBuilderPoint(world);
        var entity = _builderEntities[_builderSelectedEntityIndex];
        var configuration = ForegroundSpriteMetadata.ParseConfiguration(entity.Properties);
        if (configuration.Tile)
        {
            ApplyGarrisonBuilderMapSpriteTileResizeDrag(entity, _builderSelectedEntityIndex, world);
            return;
        }

        if (!TryGetGarrisonBuilderForegroundSpriteWorldBounds(entity, out var left, out var top, out var width, out var height)
            || !TryGetGarrisonBuilderForegroundSpritePixelSize(entity, out var pixelWidth, out var pixelHeight))
        {
            return;
        }

        var aspectRatio = pixelWidth / MathF.Max(1f, (float)pixelHeight);
        var startRight = _builderResizeStartLeft + _builderResizeStartWidth;
        var startBottom = _builderResizeStartTop + _builderResizeStartHeight;
        var centerX = _builderResizeStartLeft + (_builderResizeStartWidth * 0.5f);
        var centerY = _builderResizeStartTop + (_builderResizeStartHeight * 0.5f);
        var newLeft = _builderResizeStartLeft;
        var newTop = _builderResizeStartTop;
        var newWidth = _builderResizeStartWidth;
        var newHeight = _builderResizeStartHeight;
        const float minSize = 8f;

        if (_builderCtrlHeld)
        {
            ApplyGarrisonBuilderCenterResize(
                world,
                centerX,
                centerY,
                minSize,
                minSize,
                lockAspect: true,
                aspectRatio,
                ref newLeft,
                ref newTop,
                ref newWidth,
                ref newHeight);
        }
        else
        {
            switch (_builderActiveResizeHandle)
            {
                case GarrisonBuilderResizeHandle.BottomRight:
                    newWidth = world.X - _builderResizeStartLeft;
                    newHeight = world.Y - _builderResizeStartTop;
                    break;
                case GarrisonBuilderResizeHandle.TopLeft:
                    newLeft = world.X;
                    newTop = world.Y;
                    newWidth = startRight - newLeft;
                    newHeight = startBottom - newTop;
                    break;
                case GarrisonBuilderResizeHandle.TopRight:
                    newTop = world.Y;
                    newWidth = world.X - _builderResizeStartLeft;
                    newHeight = startBottom - newTop;
                    break;
                case GarrisonBuilderResizeHandle.BottomLeft:
                    newLeft = world.X;
                    newWidth = startRight - newLeft;
                    newHeight = world.Y - _builderResizeStartTop;
                    break;
                default:
                    return;
            }

            ApplyGarrisonBuilderAspectLockedCornerResize(
                ref newLeft,
                ref newTop,
                ref newWidth,
                ref newHeight,
                _builderActiveResizeHandle,
                _builderResizeStartLeft,
                _builderResizeStartTop,
                startRight,
                startBottom,
                aspectRatio);
        }

        newWidth = MathF.Max(minSize, newWidth);
        newHeight = MathF.Max(minSize, newHeight);
        var newScale = MathF.Max(0.01f, newWidth / pixelWidth);
        var properties = new Dictionary<string, string>(entity.Properties, StringComparer.OrdinalIgnoreCase)
        {
            [ForegroundSpriteMetadata.ScalePropertyKey] = ForegroundSpriteMetadata.ToScalePropertyValue(newScale),
        };
        var updatedCenterX = newLeft + (newWidth * 0.5f);
        var updatedCenterY = newTop + (newHeight * 0.5f);
        _builderEntities[_builderSelectedEntityIndex] = (entity with
        {
            X = updatedCenterX,
            Y = updatedCenterY,
            Properties = properties,
        }).NormalizeForEditing();
        if (_builderPropertyEditorValues.ContainsKey(ForegroundSpriteMetadata.ScalePropertyKey))
        {
            _builderPropertyEditorValues[ForegroundSpriteMetadata.ScalePropertyKey] =
                ForegroundSpriteMetadata.ToScalePropertyValue(newScale);
        }
    }
}
