#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void ApplyGarrisonBuilderMapSpriteTileResizeDrag(
        CustomMapBuilderEntity entity,
        int entityIndex,
        Vector2 world)
    {
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
                lockAspect: false,
                aspectRatio: 1f,
                ref newLeft,
                ref newTop,
                ref newWidth,
                ref newHeight);
        }
        else
        {
            switch (_builderActiveResizeHandle)
            {
                case GarrisonBuilderResizeHandle.TopLeft:
                    newLeft = world.X;
                    newTop = world.Y;
                    newWidth = startRight - newLeft;
                    newHeight = startBottom - newTop;
                    break;
                case GarrisonBuilderResizeHandle.Top:
                    newTop = world.Y;
                    newHeight = startBottom - newTop;
                    break;
                case GarrisonBuilderResizeHandle.TopRight:
                    newTop = world.Y;
                    newWidth = world.X - _builderResizeStartLeft;
                    newHeight = startBottom - newTop;
                    break;
                case GarrisonBuilderResizeHandle.Right:
                    newWidth = world.X - _builderResizeStartLeft;
                    break;
                case GarrisonBuilderResizeHandle.BottomRight:
                    newWidth = world.X - _builderResizeStartLeft;
                    newHeight = world.Y - _builderResizeStartTop;
                    break;
                case GarrisonBuilderResizeHandle.Bottom:
                    newHeight = world.Y - _builderResizeStartTop;
                    break;
                case GarrisonBuilderResizeHandle.BottomLeft:
                    newLeft = world.X;
                    newWidth = startRight - newLeft;
                    newHeight = world.Y - _builderResizeStartTop;
                    break;
                case GarrisonBuilderResizeHandle.Left:
                    newLeft = world.X;
                    newWidth = startRight - newLeft;
                    break;
                default:
                    return;
            }
        }

        ClampGarrisonBuilderResizeBounds(
            minSize,
            minSize,
            startRight,
            startBottom,
            centerX,
            centerY,
            ref newLeft,
            ref newTop,
            ref newWidth,
            ref newHeight);
        var properties = new Dictionary<string, string>(entity.Properties, StringComparer.OrdinalIgnoreCase)
        {
            [MapSpriteTileMetadata.TileAreaWidthPropertyKey] =
                MapSpriteTileMetadata.ToTileAreaDimensionPropertyValue(newWidth),
            [MapSpriteTileMetadata.TileAreaHeightPropertyKey] =
                MapSpriteTileMetadata.ToTileAreaDimensionPropertyValue(newHeight),
        };
        var updatedCenterX = newLeft + (newWidth * 0.5f);
        var updatedCenterY = newTop + (newHeight * 0.5f);
        _builderEntities[entityIndex] = (entity with
        {
            X = updatedCenterX,
            Y = updatedCenterY,
            Properties = properties,
        }).NormalizeForEditing();
        _builderPropertyEditorValues[MapSpriteTileMetadata.TileAreaWidthPropertyKey] =
            properties[MapSpriteTileMetadata.TileAreaWidthPropertyKey];
        _builderPropertyEditorValues[MapSpriteTileMetadata.TileAreaHeightPropertyKey] =
            properties[MapSpriteTileMetadata.TileAreaHeightPropertyKey];
    }

    private void InitializeGarrisonBuilderMapSpriteTileArea(
        int pixelWidth,
        int pixelHeight,
        float scale)
    {
        var (width, height) = MapSpriteTileMetadata.ResolveWorldDimensions(
            pixelWidth,
            pixelHeight,
            scale,
            tile: false,
            tileAreaWidth: 0f,
            tileAreaHeight: 0f);
        _builderPropertyEditorValues[MapSpriteTileMetadata.TileAreaWidthPropertyKey] =
            MapSpriteTileMetadata.ToTileAreaDimensionPropertyValue(width);
        _builderPropertyEditorValues[MapSpriteTileMetadata.TileAreaHeightPropertyKey] =
            MapSpriteTileMetadata.ToTileAreaDimensionPropertyValue(height);
    }

    private bool TryToggleGarrisonBuilderMapSpriteTileProperty(
        string key,
        string value,
        int pixelWidth,
        int pixelHeight,
        float scale)
    {
        if (!key.Equals(MapSpriteTileMetadata.TilePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var next = !MapSpriteTileMetadata.ParseTile(value);
        _builderPropertyEditorValues[key] = MapSpriteTileMetadata.ToTilePropertyValue(next);
        if (next)
        {
            InitializeGarrisonBuilderMapSpriteTileArea(pixelWidth, pixelHeight, scale);
        }

        ApplyGarrisonBuilderPropertyEditorLivePreview();
        MarkGarrisonBuilderPropertyEditorChanged();
        return true;
    }

    private bool TryCycleGarrisonBuilderMapSpriteTileAnchorProperty(string key, string value)
    {
        if (!key.Equals(MapSpriteTileMetadata.TileAnchorPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        _builderPropertyEditorValues[key] = MapSpriteTileMetadata.CycleTileAnchorPropertyValue(value);
        ApplyGarrisonBuilderPropertyEditorLivePreview();
        MarkGarrisonBuilderPropertyEditorChanged();
        return true;
    }
}
