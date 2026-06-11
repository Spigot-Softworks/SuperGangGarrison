using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Core;

public enum MapSpriteTileAnchor
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    Center,
}

public static class MapSpriteTileMetadata
{
    public const string TilePropertyKey = "tile";
    public const string TileAnchorPropertyKey = "tileAnchor";
    public const string TileAreaWidthPropertyKey = "tileAreaWidth";
    public const string TileAreaHeightPropertyKey = "tileAreaHeight";
    public const string TileAnchorTopLeftValue = "topLeft";
    public const string TileAnchorTopRightValue = "topRight";
    public const string TileAnchorBottomLeftValue = "bottomLeft";
    public const string TileAnchorBottomRightValue = "bottomRight";
    public const string TileAnchorCenterValue = "center";

    public static bool IsTileDependentPropertyKey(string key)
    {
        return key.Equals(TileAnchorPropertyKey, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ShouldShowTileDependentProperty(
        IReadOnlyDictionary<string, string>? properties,
        string key)
    {
        if (!IsTileDependentPropertyKey(key))
        {
            return true;
        }

        return ParseTile(properties?.TryGetValue(TilePropertyKey, out var tile) == true ? tile : null);
    }

    public static void EnsurePlacementDefaults(IDictionary<string, string> properties)
    {
        if (!properties.ContainsKey(TilePropertyKey))
        {
            properties[TilePropertyKey] = ToTilePropertyValue(false);
        }

        if (!properties.ContainsKey(TileAnchorPropertyKey))
        {
            properties[TileAnchorPropertyKey] = TileAnchorTopLeftValue;
        }
    }

    public static bool ParseTile(string? value)
    {
        return value?.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) == true
            || value?.Trim().Equals("1", StringComparison.OrdinalIgnoreCase) == true;
    }

    public static string ToTilePropertyValue(bool tile)
    {
        return tile ? "true" : "false";
    }

    public static MapSpriteTileAnchor ParseTileAnchor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return MapSpriteTileAnchor.TopLeft;
        }

        if (value.Equals(TileAnchorTopRightValue, StringComparison.OrdinalIgnoreCase))
        {
            return MapSpriteTileAnchor.TopRight;
        }

        if (value.Equals(TileAnchorBottomLeftValue, StringComparison.OrdinalIgnoreCase))
        {
            return MapSpriteTileAnchor.BottomLeft;
        }

        if (value.Equals(TileAnchorBottomRightValue, StringComparison.OrdinalIgnoreCase))
        {
            return MapSpriteTileAnchor.BottomRight;
        }

        if (value.Equals(TileAnchorCenterValue, StringComparison.OrdinalIgnoreCase))
        {
            return MapSpriteTileAnchor.Center;
        }

        return MapSpriteTileAnchor.TopLeft;
    }

    public static string ToTileAnchorPropertyValue(MapSpriteTileAnchor anchor)
    {
        return anchor switch
        {
            MapSpriteTileAnchor.TopRight => TileAnchorTopRightValue,
            MapSpriteTileAnchor.BottomLeft => TileAnchorBottomLeftValue,
            MapSpriteTileAnchor.BottomRight => TileAnchorBottomRightValue,
            MapSpriteTileAnchor.Center => TileAnchorCenterValue,
            _ => TileAnchorTopLeftValue,
        };
    }

    public static string CycleTileAnchorPropertyValue(string? current)
    {
        var next = ((int)ParseTileAnchor(current) + 1) % 5;
        return ToTileAnchorPropertyValue((MapSpriteTileAnchor)next);
    }

    public static string GetTileAnchorDisplayLabel(string? value)
    {
        return ParseTileAnchor(value) switch
        {
            MapSpriteTileAnchor.TopRight => "Top right",
            MapSpriteTileAnchor.BottomLeft => "Bot left",
            MapSpriteTileAnchor.BottomRight => "Bot right",
            MapSpriteTileAnchor.Center => "Centre",
            _ => "Top left",
        };
    }

    public static float ParseTileAreaDimension(string? value)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && parsed > 0f
            ? parsed
            : 0f;
    }

    public static string ToTileAreaDimensionPropertyValue(float value)
    {
        return Math.Max(1f, value).ToString("0.###", CultureInfo.InvariantCulture);
    }

    public static (float Width, float Height) ResolveWorldDimensions(
        int pixelWidth,
        int pixelHeight,
        float scale,
        bool tile,
        float tileAreaWidth,
        float tileAreaHeight)
    {
        if (tile && tileAreaWidth > 0f && tileAreaHeight > 0f)
        {
            return (tileAreaWidth, tileAreaHeight);
        }

        var safeScale = scale <= 0f ? 1f : scale;
        return (
            MathF.Max(1f, pixelWidth * safeScale),
            MathF.Max(1f, pixelHeight * safeScale));
    }

    public static (float Left, float Top, float Width, float Height) ResolveWorldBounds(
        float centerX,
        float centerY,
        int pixelWidth,
        int pixelHeight,
        float scale,
        bool tile,
        float tileAreaWidth,
        float tileAreaHeight)
    {
        var (width, height) = ResolveWorldDimensions(
            pixelWidth,
            pixelHeight,
            scale,
            tile,
            tileAreaWidth,
            tileAreaHeight);
        return (centerX - (width * 0.5f), centerY - (height * 0.5f), width, height);
    }

    public static (float AnchorX, float AnchorY) ResolveAnchorTopLeft(
        float areaLeft,
        float areaTop,
        float areaWidth,
        float areaHeight,
        float tileWidth,
        float tileHeight,
        MapSpriteTileAnchor anchor)
    {
        return anchor switch
        {
            MapSpriteTileAnchor.TopRight => (areaLeft + areaWidth - tileWidth, areaTop),
            MapSpriteTileAnchor.BottomLeft => (areaLeft, areaTop + areaHeight - tileHeight),
            MapSpriteTileAnchor.BottomRight => (areaLeft + areaWidth - tileWidth, areaTop + areaHeight - tileHeight),
            MapSpriteTileAnchor.Center => (
                areaLeft + ((areaWidth - tileWidth) * 0.5f),
                areaTop + ((areaHeight - tileHeight) * 0.5f)),
            _ => (areaLeft, areaTop),
        };
    }
}
