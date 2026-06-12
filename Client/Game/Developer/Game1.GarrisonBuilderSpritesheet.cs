#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private static readonly Color GarrisonBuilderSpritesheetPlaceholderColor = new(160, 130, 200, 220);

    private void SyncGarrisonBuilderSpritesheetResources()
    {
        var resources = new Dictionary<string, CustomMapBuilderResource>(
            _builderDocument.Resources,
            StringComparer.OrdinalIgnoreCase);
        var changed = false;
        for (var index = 0; index < _builderEntities.Count; index += 1)
        {
            var entity = _builderEntities[index];
            if (!SpritesheetMetadata.IsSpritesheetEntityType(entity.Type))
            {
                continue;
            }

            if (!entity.Properties.TryGetValue(SpritesheetMetadata.ImagePropertyKey, out var imageValue)
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

            var resourceName = SpritesheetMetadata.FindOrAssignImageResource(imageValue, resources);
            if (!entity.Properties.TryGetValue(SpritesheetMetadata.ImagePropertyKey, out var existing)
                || !existing.Equals(resourceName, StringComparison.OrdinalIgnoreCase))
            {
                var properties = new Dictionary<string, string>(entity.Properties, StringComparer.OrdinalIgnoreCase)
                {
                    [SpritesheetMetadata.ImagePropertyKey] = resourceName,
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

    private void DrawGarrisonBuilderSpritesheetsForLayer(CustomMapSpriteLayerKind layer)
    {
        var camera = _builderUseModernUi ? _builderCamera : Vector2.Zero;
        var viewportWidth = _builderUseModernUi
            ? GetModernGarrisonBuilderMapViewport().Width
            : GraphicsDevice.Viewport.Width;
        var viewportHeight = _builderUseModernUi
            ? GetModernGarrisonBuilderMapViewport().Height
            : GraphicsDevice.Viewport.Height;
        var mapScale = GetGarrisonBuilderMapVisualScale();
        var parallaxLayers = _builderDocument.ParallaxLayers;

        foreach (var (_, entity) in EnumerateGarrisonBuilderSpritesheetsForLayer(layer))
        {
            DrawGarrisonBuilderSpritesheetEntity(
                entity,
                camera,
                viewportWidth,
                viewportHeight,
                mapScale,
                parallaxLayers,
                Color.White);
        }
    }

    private IEnumerable<(int Index, CustomMapBuilderEntity Entity)> EnumerateGarrisonBuilderSpritesheetsForLayer(
        CustomMapSpriteLayerKind layer)
    {
        var matches = new List<(int Index, CustomMapBuilderEntity Entity, int ZOrder)>();
        for (var index = 0; index < _builderEntities.Count; index += 1)
        {
            if (IsGarrisonBuilderEntityHidden(index))
            {
                continue;
            }

            var entity = _builderEntities[index];
            if (!SpritesheetMetadata.IsSpritesheetEntityType(entity.Type))
            {
                continue;
            }

            entity.Properties.TryGetValue(SpritesheetMetadata.LayerPropertyKey, out var layerValue);
            if (CustomMapCustomSpriteMetadata.ParseLayer(layerValue) != layer)
            {
                continue;
            }

            entity.Properties.TryGetValue(SpritesheetMetadata.ZOrderPropertyKey, out var zOrderValue);
            matches.Add((index, entity, CustomMapCustomSpriteMetadata.ParseZOrder(zOrderValue)));
        }

        return matches
            .OrderBy(static entry => entry.ZOrder)
            .ThenBy(static entry => entry.Index)
            .Select(static entry => (entry.Index, entry.Entity));
    }

    private void DrawGarrisonBuilderSpritesheetEntity(
        CustomMapBuilderEntity entity,
        Vector2 camera,
        int viewportWidth,
        int viewportHeight,
        float mapScale,
        IReadOnlyList<CustomMapBuilderParallaxLayer> parallaxLayers,
        Color tint)
    {
        if (!TryGetGarrisonBuilderSpritesheetDrawBounds(entity, out var centerX, out var centerY, out var width, out var height))
        {
            return;
        }

        entity.Properties.TryGetValue(SpritesheetMetadata.LayerPropertyKey, out var layerValue);
        var layer = CustomMapCustomSpriteMetadata.ParseLayer(layerValue);
        var (relX, relY) = CustomMapSpriteParallax.WorldToScreen(
            centerX,
            centerY,
            layer,
            camera.X,
            camera.Y,
            viewportWidth,
            viewportHeight,
            parallaxLayers);
        var screenPosition = _builderUseModernUi
            ? BuilderWorldToScreen(new Vector2(relX + camera.X, relY + camera.Y))
            : new Vector2(relX, relY);
        var screenScale = _builderUseModernUi ? GetGarrisonBuilderMapVisualScale() : 1f;
        var screenWidth = MathF.Max(1f, width * screenScale);
        var screenHeight = MathF.Max(1f, height * screenScale);
        var drawRect = new Rectangle(
            (int)MathF.Floor(screenPosition.X - (screenWidth * 0.5f)),
            (int)MathF.Floor(screenPosition.Y - (screenHeight * 0.5f)),
            Math.Max(1, (int)MathF.Ceiling(screenWidth)),
            Math.Max(1, (int)MathF.Ceiling(screenHeight)));

        var configuration = SpritesheetMetadata.ParseConfiguration(entity.Properties);
        if (configuration.HasImage
            && entity.Properties.TryGetValue(SpritesheetMetadata.ImagePropertyKey, out var resourceName)
            && !string.IsNullOrWhiteSpace(resourceName)
            && _builderDocument.Resources.TryGetValue(resourceName.Trim(), out var resource)
            && CustomMapBuilderResourceCodec.TryGetResourceBytes(resource, out var bytes)
            && SpritesheetMetadata.TryParsePngDimensions(bytes, out var imageWidth, out var imageHeight))
        {
            var texture = GetGarrisonBuilderResourceTexture(resourceName.Trim());
            if (texture is not null)
            {
                var source = SpritesheetMetadata.ResolveFrameSourceRectangle(imageWidth, imageHeight, 0, configuration);
                _spriteBatch.Draw(
                    texture,
                    drawRect,
                    new Rectangle(source.X, source.Y, source.Width, source.Height),
                    tint);
                return;
            }
        }

        _spriteBatch.Draw(_pixel, drawRect, Color.Lerp(GarrisonBuilderSpritesheetPlaceholderColor, tint, tint.A / 255f));
    }

    private bool TryDrawGarrisonBuilderSpritesheetEntity(
        CustomMapBuilderEntityDefinition definition,
        CustomMapBuilderEntity entity,
        Color tint)
    {
        if (!SpritesheetMetadata.IsSpritesheetEntityType(definition.Type))
        {
            return false;
        }

        DrawGarrisonBuilderSpritesheetEntity(
            entity,
            _builderUseModernUi ? _builderCamera : Vector2.Zero,
            _builderUseModernUi ? GetModernGarrisonBuilderMapViewport().Width : GraphicsDevice.Viewport.Width,
            _builderUseModernUi ? GetModernGarrisonBuilderMapViewport().Height : GraphicsDevice.Viewport.Height,
            GetGarrisonBuilderMapVisualScale(),
            _builderDocument.ParallaxLayers,
            tint);
        return true;
    }

    private bool IsGarrisonBuilderSpritesheetResizable(CustomMapBuilderEntity entity)
    {
        return SpritesheetMetadata.IsSpritesheetEntityType(entity.Type)
            && SpritesheetMetadata.ParseConfiguration(entity.Properties).HasImage;
    }

    private static bool IsGarrisonBuilderSpritesheetPropertyKey(string key)
    {
        return key.Equals(SpritesheetMetadata.ImagePropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(SpritesheetMetadata.LayerPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(SpritesheetMetadata.ZOrderPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(SpritesheetMetadata.ScalePropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(SpritesheetMetadata.ColumnsPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(SpritesheetMetadata.RowsPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(SpritesheetMetadata.AutostartPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(SpritesheetMetadata.StartInputPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(SpritesheetMetadata.StopInputPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(SpritesheetMetadata.AutoplayPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(SpritesheetMetadata.NextFrameInputPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(SpritesheetMetadata.FrameratePropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(SpritesheetMetadata.LoopingModePropertyKey, StringComparison.OrdinalIgnoreCase);
    }

    private List<string> OrderGarrisonBuilderSpritesheetPropertyRows(List<string> rows)
    {
        string[] order =
        [
            SpritesheetMetadata.ImagePropertyKey,
            SpritesheetMetadata.LayerPropertyKey,
            SpritesheetMetadata.ZOrderPropertyKey,
            SpritesheetMetadata.ScalePropertyKey,
            SpritesheetMetadata.ColumnsPropertyKey,
            SpritesheetMetadata.RowsPropertyKey,
            SpritesheetMetadata.AutostartPropertyKey,
            SpritesheetMetadata.StartInputPropertyKey,
            SpritesheetMetadata.StopInputPropertyKey,
            SpritesheetMetadata.AutoplayPropertyKey,
            SpritesheetMetadata.NextFrameInputPropertyKey,
            SpritesheetMetadata.FrameratePropertyKey,
            SpritesheetMetadata.LoopingModePropertyKey,
        ];
        var ordered = new List<string>(order.Length);
        foreach (var key in order)
        {
            if (!SpritesheetMetadata.ShouldShowProperty(_builderPropertyEditorValues, key))
            {
                continue;
            }

            _builderPropertyEditorValues.TryAdd(key, string.Empty);
            var match = rows.FirstOrDefault(existing => existing.Equals(key, StringComparison.OrdinalIgnoreCase))
                ?? key;
            if (!ordered.Any(existing => existing.Equals(match, StringComparison.OrdinalIgnoreCase)))
            {
                ordered.Add(match);
            }
        }

        foreach (var key in rows)
        {
            if (!ordered.Any(existing => existing.Equals(key, StringComparison.OrdinalIgnoreCase)))
            {
                ordered.Add(key);
            }
        }

        return ordered;
    }

    private string GetGarrisonBuilderSpritesheetPropertyDisplayLabel(string key, string value)
    {
        if (key.Equals(SpritesheetMetadata.LayerPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Layer: {SpritesheetMetadata.GetLayerDisplayLabel(value)}";
        }

        if (key.Equals(SpritesheetMetadata.LoopingModePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Looping: {SpritesheetMetadata.GetLoopingModeDisplayLabel(value)}";
        }

        if (key.Equals(SpritesheetMetadata.ImagePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(value) ? "Image: (none)" : $"Image: {value}";
        }

        if (key.Equals(SpritesheetMetadata.ZOrderPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Z order: {CustomMapCustomSpriteMetadata.ParseZOrder(value)}";
        }

        if (key.Equals(SpritesheetMetadata.ScalePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Scale: {CustomMapCustomSpriteMetadata.ParseScale(value):0.###}";
        }

        if (key.Equals(SpritesheetMetadata.ColumnsPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Columns: {SpritesheetMetadata.ParseGridDimension(value, SpritesheetMetadata.DefaultColumns)}";
        }

        if (key.Equals(SpritesheetMetadata.RowsPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Rows: {SpritesheetMetadata.ParseGridDimension(value, SpritesheetMetadata.DefaultRows)}";
        }

        if (key.Equals(SpritesheetMetadata.AutostartPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return "Autostart";
        }

        if (key.Equals(SpritesheetMetadata.AutoplayPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return "Autoplay";
        }

        if (key.Equals(SpritesheetMetadata.FrameratePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Framerate: {SpritesheetMetadata.ParseFramerate(value)}";
        }

        if (SpritesheetMetadata.IsLogicInputPropertyKey(key))
        {
            return FormatGarrisonBuilderLogicPropertyRowLabel(key, value);
        }

        return $"{key}: {value}";
    }

    private bool TryHandleGarrisonBuilderSpritesheetPropertyClick(string key, string value)
    {
        if (!TryGetGarrisonBuilderEditedEntityType(out var entityType)
            || !SpritesheetMetadata.IsSpritesheetEntityType(entityType))
        {
            return false;
        }

        if (key.Equals(SpritesheetMetadata.LayerPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            _builderPropertyEditorValues[key] = SpritesheetMetadata.CycleLayerPropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
            return true;
        }

        if (key.Equals(SpritesheetMetadata.LoopingModePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            _builderPropertyEditorValues[key] = SpritesheetMetadata.CycleLoopingModePropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
            return true;
        }

        if (key.Equals(SpritesheetMetadata.ImagePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            TryChooseGarrisonBuilderSpritesheetImage();
            return true;
        }

        if (key.Equals(SpritesheetMetadata.AutostartPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            _builderPropertyEditorValues[key] = SpritesheetMetadata.CycleAutostartPropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
            ClampGarrisonBuilderPropertyEditorScrollIndex();
            return true;
        }

        if (key.Equals(SpritesheetMetadata.AutoplayPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            _builderPropertyEditorValues[key] = SpritesheetMetadata.CycleAutoplayPropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
            ClampGarrisonBuilderPropertyEditorScrollIndex();
            return true;
        }

        if (SpritesheetMetadata.IsLogicInputPropertyKey(key))
        {
            BeginGarrisonBuilderLogicMapPick(key);
            return true;
        }

        return false;
    }

    private void TryChooseGarrisonBuilderSpritesheetImage()
    {
        if (!TryChooseGarrisonBuilderFile(
                "Load spritesheet image",
                "Image files (*.png;*.gif)|*.png;*.gif|PNG files (*.png)|*.png|GIF files (*.gif)|*.gif|All files (*.*)|*.*",
                string.Empty,
                out var selectedPath))
        {
            return;
        }

        var resources = new Dictionary<string, CustomMapBuilderResource>(_builderDocument.Resources, StringComparer.OrdinalIgnoreCase);
        var resourceName = SpritesheetMetadata.FindOrAssignImageResource(selectedPath, resources);
        _builderDocument = _builderDocument with { Resources = resources };
        _builderPropertyEditorValues[SpritesheetMetadata.ImagePropertyKey] = resourceName;
        ApplyGarrisonBuilderPropertyEditorLivePreview();
        MarkGarrisonBuilderPropertyEditorChanged();
    }

    private bool TryGetGarrisonBuilderSpritesheetDrawBounds(
        CustomMapBuilderEntity entity,
        out float centerX,
        out float centerY,
        out float width,
        out float height)
    {
        centerX = entity.X;
        centerY = entity.Y;
        width = 42f;
        height = 42f;
        var configuration = SpritesheetMetadata.ParseConfiguration(entity.Properties);
        if (!TryGetGarrisonBuilderSpritesheetImageDimensions(entity, configuration, out var imageWidth, out var imageHeight))
        {
            return true;
        }

        (width, height) = SpritesheetMetadata.ResolveWorldDimensions(imageWidth, imageHeight, configuration.Scale, configuration);
        return true;
    }

    private bool TryGetGarrisonBuilderSpritesheetWorldBounds(
        CustomMapBuilderEntity entity,
        out float left,
        out float top,
        out float width,
        out float height)
    {
        if (!TryGetGarrisonBuilderSpritesheetDrawBounds(entity, out var centerX, out var centerY, out width, out height))
        {
            left = top = 0f;
            return false;
        }

        left = centerX - (width * 0.5f);
        top = centerY - (height * 0.5f);
        return true;
    }

    private bool TryGetGarrisonBuilderSpritesheetImageDimensions(
        CustomMapBuilderEntity entity,
        SpritesheetConfiguration configuration,
        out int imageWidth,
        out int imageHeight)
    {
        imageWidth = 0;
        imageHeight = 0;
        return configuration.HasImage
            && entity.Properties.TryGetValue(SpritesheetMetadata.ImagePropertyKey, out var resourceName)
            && !string.IsNullOrWhiteSpace(resourceName)
            && _builderDocument.Resources.TryGetValue(resourceName.Trim(), out var resource)
            && CustomMapBuilderResourceCodec.TryGetResourceBytes(resource, out var bytes)
            && SpritesheetMetadata.TryParsePngDimensions(bytes, out imageWidth, out imageHeight);
    }

    private bool IsGarrisonBuilderSpritesheetZOrderPropertyRow(string key)
    {
        return key.Equals(SpritesheetMetadata.ZOrderPropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var entityType)
            && SpritesheetMetadata.IsSpritesheetEntityType(entityType);
    }

    private bool IsGarrisonBuilderSpritesheetFrameratePropertyRow(string key)
    {
        return key.Equals(SpritesheetMetadata.FrameratePropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var entityType)
            && SpritesheetMetadata.IsSpritesheetEntityType(entityType);
    }

    private bool IsGarrisonBuilderSpritesheetGridPropertyRow(string key)
    {
        return (key.Equals(SpritesheetMetadata.ColumnsPropertyKey, StringComparison.OrdinalIgnoreCase)
                || key.Equals(SpritesheetMetadata.RowsPropertyKey, StringComparison.OrdinalIgnoreCase))
            && TryGetGarrisonBuilderEditedEntityType(out var entityType)
            && SpritesheetMetadata.IsSpritesheetEntityType(entityType);
    }

    private bool TryHandleGarrisonBuilderSpritesheetZOrderClick(string key, string value, Rectangle rowBounds, Point position)
    {
        if (!IsGarrisonBuilderSpritesheetZOrderPropertyRow(key))
        {
            return false;
        }

        GetGarrisonBuilderCustomSpriteZOrderSliderLayout(
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
            BeginGarrisonBuilderPropertyTextEdit(
                SpritesheetMetadata.ZOrderPropertyKey,
                SpritesheetMetadata.ToZOrderPropertyValue(CustomMapCustomSpriteMetadata.ParseZOrder(value)),
                GarrisonBuilderPropertyEditMode.EditValue);
            return true;
        }

        var delta = position.X < sliderBounds.X + (sliderBounds.Width / 2) ? -1 : 1;
        _builderPropertyEditorValues[key] = SpritesheetMetadata.CycleZOrderPropertyValue(value, delta);
        ApplyGarrisonBuilderPropertyEditorLivePreview();
        MarkGarrisonBuilderPropertyEditorChanged();
        return true;
    }

    private bool TryHandleGarrisonBuilderSpritesheetFramerateClick(string key, string value, Rectangle rowBounds, Point position)
    {
        if (!IsGarrisonBuilderSpritesheetFrameratePropertyRow(key))
        {
            return false;
        }

        GetGarrisonBuilderSpritesheetFramerateSliderLayout(
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
            BeginGarrisonBuilderPropertyTextEdit(
                SpritesheetMetadata.FrameratePropertyKey,
                SpritesheetMetadata.ToFrameratePropertyValue(SpritesheetMetadata.ParseFramerate(value)),
                GarrisonBuilderPropertyEditMode.EditValue);
            return true;
        }

        var delta = position.X < sliderBounds.X + (sliderBounds.Width / 2) ? -1 : 1;
        _builderPropertyEditorValues[key] = SpritesheetMetadata.CycleFrameratePropertyValue(value, delta);
        ApplyGarrisonBuilderPropertyEditorLivePreview();
        MarkGarrisonBuilderPropertyEditorChanged();
        return true;
    }

    private bool TryHandleGarrisonBuilderSpritesheetGridClick(string key, string value, Rectangle rowBounds, Point position)
    {
        if (!IsGarrisonBuilderSpritesheetGridPropertyRow(key))
        {
            return false;
        }

        GetGarrisonBuilderSpritesheetFramerateSliderLayout(
            rowBounds,
            value,
            GetGarrisonBuilderPropertyRowTextScale(),
            out _,
            out var sliderBounds,
            out _);
        if (!sliderBounds.Contains(position))
        {
            return false;
        }

        var fallback = key.Equals(SpritesheetMetadata.ColumnsPropertyKey, StringComparison.OrdinalIgnoreCase)
            ? SpritesheetMetadata.DefaultColumns
            : SpritesheetMetadata.DefaultRows;
        var delta = position.X < sliderBounds.X + (sliderBounds.Width / 2) ? -1 : 1;
        _builderPropertyEditorValues[key] = SpritesheetMetadata.CycleGridDimensionPropertyValue(value, fallback, delta);
        ApplyGarrisonBuilderPropertyEditorLivePreview();
        MarkGarrisonBuilderPropertyEditorChanged();
        return true;
    }

    private void GetGarrisonBuilderSpritesheetFramerateSliderLayout(
        Rectangle rowBounds,
        string value,
        float textScale,
        out string sliderDisplay,
        out Rectangle sliderBounds,
        out Rectangle digitBounds)
    {
        GetGarrisonBuilderCustomSpriteZOrderSliderLayout(rowBounds, value, textScale, out sliderDisplay, out sliderBounds, out digitBounds);
    }

    private void DrawGarrisonBuilderSpritesheetZOrderPropertyRow(
        Rectangle rowBounds,
        string value,
        MouseState mouse,
        float textScale,
        bool hovered)
    {
        DrawGarrisonBuilderCustomSpriteZOrderPropertyRow(rowBounds, value, mouse, textScale, hovered);
    }

    private void DrawGarrisonBuilderSpritesheetFrameratePropertyRow(
        Rectangle rowBounds,
        string value,
        MouseState mouse,
        float textScale,
        bool hovered)
    {
        DrawGarrisonBuilderSpritesheetNumericSliderPropertyRow(
            rowBounds,
            "Framerate",
            value,
            textScale);
    }

    private void DrawGarrisonBuilderSpritesheetGridPropertyRow(
        Rectangle rowBounds,
        string key,
        string value,
        MouseState mouse,
        float textScale,
        bool hovered)
    {
        var label = key.Equals(SpritesheetMetadata.ColumnsPropertyKey, StringComparison.OrdinalIgnoreCase)
            ? "Columns"
            : "Rows";
        DrawGarrisonBuilderSpritesheetNumericSliderPropertyRow(
            rowBounds,
            label,
            value,
            textScale);
    }

    private void DrawGarrisonBuilderSpritesheetNumericSliderPropertyRow(
        Rectangle rowBounds,
        string label,
        string value,
        float textScale)
    {
        GetGarrisonBuilderSpritesheetFramerateSliderLayout(
            rowBounds,
            value,
            textScale,
            out var sliderDisplay,
            out var sliderBounds,
            out _);
        var textY = rowBounds.Y + MathF.Max(2f, ((rowBounds.Height - MeasureBitmapFontHeight(textScale)) * 0.5f) - 1f);
        var labelColor = _builderUseModernUi ? Color.White : Color.Black;
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

    private void ApplyGarrisonBuilderSpritesheetResizeDrag(Vector2 world)
    {
        if (_builderSelectedEntityIndex < 0 || _builderSelectedEntityIndex >= _builderEntities.Count)
        {
            return;
        }

        world = SnapGarrisonBuilderPoint(world);
        var entity = _builderEntities[_builderSelectedEntityIndex];
        if (!TryGetGarrisonBuilderSpritesheetWorldBounds(entity, out var left, out var top, out var width, out var height)
            || !TryGetGarrisonBuilderSpritesheetImageDimensions(
                entity,
                SpritesheetMetadata.ParseConfiguration(entity.Properties),
                out var imageWidth,
                out var imageHeight))
        {
            return;
        }

        var configuration = SpritesheetMetadata.ParseConfiguration(entity.Properties);
        var (frameWidth, frameHeight) = SpritesheetMetadata.ResolveFramePixelDimensions(imageWidth, imageHeight, configuration);
        var aspectRatio = frameWidth / MathF.Max(1f, (float)frameHeight);
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
        var newScale = MathF.Max(0.01f, newWidth / frameWidth);
        var properties = new Dictionary<string, string>(entity.Properties, StringComparer.OrdinalIgnoreCase)
        {
            [SpritesheetMetadata.ScalePropertyKey] = SpritesheetMetadata.ToScalePropertyValue(newScale),
        };
        var updatedCenterX = newLeft + (newWidth * 0.5f);
        var updatedCenterY = newTop + (newHeight * 0.5f);
        _builderEntities[_builderSelectedEntityIndex] = (entity with
        {
            X = updatedCenterX,
            Y = updatedCenterY,
            Properties = properties,
        }).NormalizeForEditing();
        if (_builderPropertyEditorValues.ContainsKey(SpritesheetMetadata.ScalePropertyKey))
        {
            _builderPropertyEditorValues[SpritesheetMetadata.ScalePropertyKey] = SpritesheetMetadata.ToScalePropertyValue(newScale);
        }
    }
}
