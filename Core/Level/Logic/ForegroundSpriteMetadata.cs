using System;
using System.Collections.Generic;
using System.Globalization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace OpenGarrison.Core;

public enum ForegroundSpriteLayerKind
{
    Bg,
    Fg,
}

public enum ForegroundSpriteBoundaryKind
{
    Box,
    Pixel,
}

public readonly record struct ForegroundSpriteConfiguration(
    string ImageResourceName,
    ForegroundSpriteLayerKind Layer,
    int RelativeZ,
    float Scale,
    bool Jungle,
    float OutsideOpacity,
    float InsideOpacity,
    ForegroundSpriteBoundaryKind Boundary,
    bool Tile,
    MapSpriteTileAnchor TileAnchor,
    float TileAreaWidth,
    float TileAreaHeight)
{
    public bool HasImage => !string.IsNullOrWhiteSpace(ImageResourceName);
}

public sealed class ForegroundSpriteHitMask
{
    public ForegroundSpriteHitMask(int pixelWidth, int pixelHeight, byte[] alphaSamples)
    {
        PixelWidth = Math.Max(1, pixelWidth);
        PixelHeight = Math.Max(1, pixelHeight);
        AlphaSamples = alphaSamples ?? Array.Empty<byte>();
    }

    public int PixelWidth { get; }

    public int PixelHeight { get; }

    public byte[] AlphaSamples { get; }

    public bool IsOpaqueAtPixel(int pixelX, int pixelY, byte alphaThreshold = 12)
    {
        if (pixelX < 0 || pixelY < 0 || pixelX >= PixelWidth || pixelY >= PixelHeight)
        {
            return false;
        }

        var index = (pixelY * PixelWidth) + pixelX;
        return index < AlphaSamples.Length && AlphaSamples[index] > alphaThreshold;
    }
}

public static class ForegroundSpriteMetadata
{
    public const string ForegroundSpriteEntityType = "logicForegroundSprite";
    public const string ImagePropertyKey = "image";
    public const string LayerPropertyKey = "layer";
    public const string RelativeZPropertyKey = "relativeZ";
    public const string ScalePropertyKey = "scale";
    public const string JunglePropertyKey = "jungle";
    public const string OutsideOpacityPropertyKey = "outsideOpacity";
    public const string InsideOpacityPropertyKey = "insideOpacity";
    public const string BoundaryPropertyKey = "boundary";
    public const string LayerBgPropertyValue = "bg";
    public const string LayerFgPropertyValue = "fg";
    public const string BoundaryBoxPropertyValue = "box";
    public const string BoundaryPixelPropertyValue = "pixel";
    public const string JungleReplicatedStateOwnerId = "foregroundJungle";
    public const int MinRelativeZ = 0;
    public const int MaxRelativeZ = 100;
    public const float DefaultOutsideOpacity = 1f;
    public const float DefaultInsideOpacity = 0.35f;

    public static bool IsForegroundSpriteEntityType(string? type)
    {
        return !string.IsNullOrWhiteSpace(type)
            && type.Equals(ForegroundSpriteEntityType, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsLogicPaletteEntityType(string? type)
    {
        return MapLogicMetadata.IsLogicEntityType(type)
            || CustomMapCustomSpriteMetadata.IsCustomSpriteEntityType(type)
            || IsForegroundSpriteEntityType(type)
            || SpritesheetMetadata.IsSpritesheetEntityType(type);
    }

    public static bool IsJungleDependentPropertyKey(string key)
    {
        return key.Equals(OutsideOpacityPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(InsideOpacityPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(BoundaryPropertyKey, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ShouldShowJungleDependentProperty(
        IReadOnlyDictionary<string, string>? properties,
        string key)
    {
        if (!IsJungleDependentPropertyKey(key))
        {
            return true;
        }

        return ParseJungle(properties?.TryGetValue(JunglePropertyKey, out var jungle) == true ? jungle : null);
    }

    public static string JungleReplicatedStateKey(int roomObjectIndex)
    {
        return $"sprite_{roomObjectIndex}";
    }

    public static void EnsurePlacementDefaults(
        IDictionary<string, string> properties,
        float defaultMapScale)
    {
        if (!properties.ContainsKey(ScalePropertyKey))
        {
            properties[ScalePropertyKey] = ToScalePropertyValue(defaultMapScale);
        }

        if (!properties.ContainsKey(LayerPropertyKey))
        {
            properties[LayerPropertyKey] = LayerBgPropertyValue;
        }

        if (!properties.ContainsKey(RelativeZPropertyKey))
        {
            properties[RelativeZPropertyKey] = "0";
        }

        if (!properties.ContainsKey(JunglePropertyKey))
        {
            properties[JunglePropertyKey] = "false";
        }

        if (!properties.ContainsKey(OutsideOpacityPropertyKey))
        {
            properties[OutsideOpacityPropertyKey] = ToOpacityPropertyValue(DefaultOutsideOpacity);
        }

        if (!properties.ContainsKey(InsideOpacityPropertyKey))
        {
            properties[InsideOpacityPropertyKey] = ToOpacityPropertyValue(DefaultInsideOpacity);
        }

        if (!properties.ContainsKey(BoundaryPropertyKey))
        {
            properties[BoundaryPropertyKey] = BoundaryBoxPropertyValue;
        }

        MapSpriteTileMetadata.EnsurePlacementDefaults(properties);
    }

    public static ForegroundSpriteConfiguration ParseConfiguration(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null)
        {
            return new ForegroundSpriteConfiguration(
                string.Empty,
                ForegroundSpriteLayerKind.Bg,
                0,
                1f,
                false,
                DefaultOutsideOpacity,
                DefaultInsideOpacity,
                ForegroundSpriteBoundaryKind.Box,
                false,
                MapSpriteTileAnchor.TopLeft,
                0f,
                0f);
        }

        properties.TryGetValue(ImagePropertyKey, out var image);
        properties.TryGetValue(LayerPropertyKey, out var layer);
        properties.TryGetValue(RelativeZPropertyKey, out var relativeZ);
        properties.TryGetValue(ScalePropertyKey, out var scale);
        properties.TryGetValue(JunglePropertyKey, out var jungle);
        properties.TryGetValue(OutsideOpacityPropertyKey, out var outsideOpacity);
        properties.TryGetValue(InsideOpacityPropertyKey, out var insideOpacity);
        properties.TryGetValue(BoundaryPropertyKey, out var boundary);
        properties.TryGetValue(MapSpriteTileMetadata.TilePropertyKey, out var tile);
        properties.TryGetValue(MapSpriteTileMetadata.TileAnchorPropertyKey, out var tileAnchor);
        properties.TryGetValue(MapSpriteTileMetadata.TileAreaWidthPropertyKey, out var tileAreaWidth);
        properties.TryGetValue(MapSpriteTileMetadata.TileAreaHeightPropertyKey, out var tileAreaHeight);
        return new ForegroundSpriteConfiguration(
            image?.Trim() ?? string.Empty,
            ParseLayer(layer),
            ParseRelativeZ(relativeZ),
            ParseScale(scale),
            ParseJungle(jungle),
            ParseOpacity(outsideOpacity, DefaultOutsideOpacity),
            ParseOpacity(insideOpacity, DefaultInsideOpacity),
            ParseBoundary(boundary),
            MapSpriteTileMetadata.ParseTile(tile),
            MapSpriteTileMetadata.ParseTileAnchor(tileAnchor),
            MapSpriteTileMetadata.ParseTileAreaDimension(tileAreaWidth),
            MapSpriteTileMetadata.ParseTileAreaDimension(tileAreaHeight));
    }

    public static ForegroundSpriteLayerKind ParseLayer(string? value)
    {
        return value?.Trim().Equals(LayerFgPropertyValue, StringComparison.OrdinalIgnoreCase) == true
            ? ForegroundSpriteLayerKind.Fg
            : ForegroundSpriteLayerKind.Bg;
    }

    public static string ToLayerPropertyValue(ForegroundSpriteLayerKind layer)
    {
        return layer == ForegroundSpriteLayerKind.Fg ? LayerFgPropertyValue : LayerBgPropertyValue;
    }

    public static string CycleLayerPropertyValue(string? current)
    {
        return ParseLayer(current) == ForegroundSpriteLayerKind.Bg
            ? LayerFgPropertyValue
            : LayerBgPropertyValue;
    }

    public static string GetLayerDisplayLabel(string? value)
    {
        return ParseLayer(value) == ForegroundSpriteLayerKind.Fg ? "FG" : "BG";
    }

    public static ForegroundSpriteBoundaryKind ParseBoundary(string? value)
    {
        return value?.Trim().Equals(BoundaryPixelPropertyValue, StringComparison.OrdinalIgnoreCase) == true
            ? ForegroundSpriteBoundaryKind.Pixel
            : ForegroundSpriteBoundaryKind.Box;
    }

    public static string ToBoundaryPropertyValue(ForegroundSpriteBoundaryKind boundary)
    {
        return boundary == ForegroundSpriteBoundaryKind.Pixel
            ? BoundaryPixelPropertyValue
            : BoundaryBoxPropertyValue;
    }

    public static string CycleBoundaryPropertyValue(string? current)
    {
        return ParseBoundary(current) == ForegroundSpriteBoundaryKind.Box
            ? BoundaryPixelPropertyValue
            : BoundaryBoxPropertyValue;
    }

    public static string GetBoundaryDisplayLabel(string? value)
    {
        return ParseBoundary(value) == ForegroundSpriteBoundaryKind.Pixel ? "Pixel-perfect" : "Box";
    }

    public static int ParseRelativeZ(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, MinRelativeZ, MaxRelativeZ)
            : 0;
    }

    public static string ToRelativeZPropertyValue(int relativeZ)
    {
        return Math.Clamp(relativeZ, MinRelativeZ, MaxRelativeZ).ToString(CultureInfo.InvariantCulture);
    }

    public static float ParseScale(string? value)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && parsed > 0f
            ? parsed
            : 1f;
    }

    public static string ToScalePropertyValue(float scale)
    {
        return Math.Max(0.001f, scale).ToString("0.###", CultureInfo.InvariantCulture);
    }

    public static bool ParseJungle(string? value)
    {
        return value?.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) == true
            || value?.Trim().Equals("1", StringComparison.OrdinalIgnoreCase) == true;
    }

    public static string ToJunglePropertyValue(bool jungle)
    {
        return jungle ? "true" : "false";
    }

    public static float ParseOpacity(string? value, float fallback)
    {
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return fallback;
        }

        return Math.Clamp(parsed, 0f, 1f);
    }

    public static string ToOpacityPropertyValue(float opacity)
    {
        return Math.Clamp(opacity, 0f, 1f).ToString("0.##", CultureInfo.InvariantCulture);
    }

    public static (float Width, float Height) ResolveWorldDimensions(
        int pixelWidth,
        int pixelHeight,
        float scale,
        ForegroundSpriteConfiguration configuration)
    {
        return MapSpriteTileMetadata.ResolveWorldDimensions(
            pixelWidth,
            pixelHeight,
            scale,
            configuration.Tile,
            configuration.TileAreaWidth,
            configuration.TileAreaHeight);
    }

    public static (float Width, float Height) ResolveWorldDimensions(int pixelWidth, int pixelHeight, float scale)
    {
        return ResolveWorldDimensions(
            pixelWidth,
            pixelHeight,
            scale,
            new ForegroundSpriteConfiguration(
                string.Empty,
                ForegroundSpriteLayerKind.Bg,
                0,
                scale,
                false,
                DefaultOutsideOpacity,
                DefaultInsideOpacity,
                ForegroundSpriteBoundaryKind.Box,
                false,
                MapSpriteTileAnchor.TopLeft,
                0f,
                0f));
    }

    public static (float Left, float Top, float Width, float Height) ResolveWorldBounds(
        float centerX,
        float centerY,
        int pixelWidth,
        int pixelHeight,
        float scale,
        ForegroundSpriteConfiguration configuration)
    {
        return MapSpriteTileMetadata.ResolveWorldBounds(
            centerX,
            centerY,
            pixelWidth,
            pixelHeight,
            scale,
            configuration.Tile,
            configuration.TileAreaWidth,
            configuration.TileAreaHeight);
    }

    public static (float Left, float Top, float Width, float Height) ResolveWorldBounds(
        float centerX,
        float centerY,
        int pixelWidth,
        int pixelHeight,
        float scale)
    {
        return ResolveWorldBounds(
            centerX,
            centerY,
            pixelWidth,
            pixelHeight,
            scale,
            new ForegroundSpriteConfiguration(
                string.Empty,
                ForegroundSpriteLayerKind.Bg,
                0,
                scale,
                false,
                DefaultOutsideOpacity,
                DefaultInsideOpacity,
                ForegroundSpriteBoundaryKind.Box,
                false,
                MapSpriteTileAnchor.TopLeft,
                0f,
                0f));
    }

    public static bool TryParsePngDimensions(byte[] bytes, out int width, out int height)
    {
        return CustomMapCustomSpriteMetadata.TryParsePngDimensions(bytes, out width, out height);
    }

    public static bool TryBuildHitMask(byte[] bytes, out ForegroundSpriteHitMask? hitMask)
    {
        hitMask = null;
        if (bytes.Length == 0)
        {
            return false;
        }

        try
        {
            using var image = Image.Load<Rgba32>(bytes);
            var alphaSamples = new byte[image.Width * image.Height];
            image.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height; y += 1)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < row.Length; x += 1)
                    {
                        alphaSamples[(y * image.Width) + x] = row[x].A;
                    }
                }
            });
            hitMask = new ForegroundSpriteHitMask(image.Width, image.Height, alphaSamples);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsPlayerInside(
        in RoomObjectMarker marker,
        float playerX,
        float playerY,
        ForegroundSpriteBoundaryKind boundary,
        ForegroundSpriteHitMask? hitMask)
    {
        if (marker.Type != RoomObjectType.ForegroundSprite)
        {
            return false;
        }

        if (boundary == ForegroundSpriteBoundaryKind.Box
            || hitMask is null)
        {
            return playerX >= marker.Left
                && playerX <= marker.Right
                && playerY >= marker.Top
                && playerY <= marker.Bottom;
        }

        if (marker.Width <= 0.01f || marker.Height <= 0.01f)
        {
            return false;
        }

        var localX = playerX - marker.Left;
        var localY = playerY - marker.Top;
        var pixelX = (int)MathF.Floor((localX / marker.Width) * hitMask.PixelWidth);
        var pixelY = (int)MathF.Floor((localY / marker.Height) * hitMask.PixelHeight);
        return hitMask.IsOpaqueAtPixel(pixelX, pixelY);
    }

    public static bool IsPlayerInsideWithExtensions(
        IReadOnlyList<RoomObjectMarker> roomObjects,
        int primaryForegroundIndex,
        float playerX,
        float playerY,
        ForegroundSpriteBoundaryKind boundary,
        ForegroundSpriteHitMask? hitMask,
        Func<int, bool> isRoomObjectActive)
    {
        if (primaryForegroundIndex < 0
            || primaryForegroundIndex >= roomObjects.Count
            || !isRoomObjectActive(primaryForegroundIndex))
        {
            return false;
        }

        var primary = roomObjects[primaryForegroundIndex];
        if (IsPlayerInside(primary, playerX, playerY, boundary, hitMask))
        {
            return true;
        }

        foreach (var extensionIndex in AreaExtensionMetadata.CollectForegroundSpriteExtensionIndices(
                     roomObjects,
                     primaryForegroundIndex))
        {
            if (!isRoomObjectActive(extensionIndex))
            {
                continue;
            }

            var extension = roomObjects[extensionIndex];
            if (AreaExtensionMetadata.IsPointInsideMarker(playerX, playerY, extension))
            {
                return true;
            }
        }

        return false;
    }

    public static string FindOrAssignImageResource(
        string sourceFilePath,
        IDictionary<string, CustomMapBuilderResource> resources)
    {
        return CustomMapCustomSpriteMetadata.FindOrAssignImageResource(sourceFilePath, resources);
    }
}
