using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;

namespace OpenGarrison.Core;

public enum CustomMapSpriteLayerKind
{
    Bg,
    Layer0,
    Layer1,
    Layer2,
    Layer3,
    Layer4,
    Layer5,
    Layer6,
    Fg,
}

public readonly record struct CustomMapSpriteConfiguration(
    string ImageResourceName,
    CustomMapSpriteLayerKind Layer,
    int ZOrder,
    float Scale)
{
    public bool HasImage => !string.IsNullOrWhiteSpace(ImageResourceName);
}

public static class CustomMapCustomSpriteMetadata
{
    public const string CustomSpriteEntityType = "logicCustomSprite";
    public const string ImagePropertyKey = "image";
    public const string LayerPropertyKey = "layer";
    public const string ZOrderPropertyKey = "zOrder";
    public const string ScalePropertyKey = "scale";
    public const string LayerBgPropertyValue = "bg";
    public const string LayerFgPropertyValue = "fg";
    public const int MinZOrder = 0;
    public const int MaxZOrder = 100;

    public static bool IsCustomSpriteEntityType(string? type)
    {
        return !string.IsNullOrWhiteSpace(type)
            && type.Equals(CustomSpriteEntityType, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsLogicPaletteEntityType(string? type)
    {
        return MapLogicMetadata.IsLogicEntityType(type) || IsCustomSpriteEntityType(type);
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

        if (!properties.ContainsKey(ZOrderPropertyKey))
        {
            properties[ZOrderPropertyKey] = "0";
        }
    }

    public static CustomMapSpriteConfiguration ParseConfiguration(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null)
        {
            return new CustomMapSpriteConfiguration(string.Empty, CustomMapSpriteLayerKind.Bg, 0, 1f);
        }

        properties.TryGetValue(ImagePropertyKey, out var image);
        properties.TryGetValue(LayerPropertyKey, out var layer);
        properties.TryGetValue(ZOrderPropertyKey, out var zOrder);
        properties.TryGetValue(ScalePropertyKey, out var scale);
        return new CustomMapSpriteConfiguration(
            image?.Trim() ?? string.Empty,
            ParseLayer(layer),
            ParseZOrder(zOrder),
            ParseScale(scale));
    }

    public static bool TryParsePngDimensions(byte[] bytes, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (bytes.Length == 0)
        {
            return false;
        }

        try
        {
            using var image = Image.Load(bytes);
            width = image.Width;
            height = image.Height;
            return width > 0 && height > 0;
        }
        catch
        {
            return false;
        }
    }

    public static (float Width, float Height) ResolveWorldDimensions(
        int pixelWidth,
        int pixelHeight,
        float scale)
    {
        var safeScale = scale <= 0f ? 1f : scale;
        return (MathF.Max(1f, pixelWidth * safeScale), MathF.Max(1f, pixelHeight * safeScale));
    }

    public static (float Left, float Top, float Width, float Height) ResolveWorldBounds(
        float centerX,
        float centerY,
        int pixelWidth,
        int pixelHeight,
        float scale)
    {
        var (width, height) = ResolveWorldDimensions(pixelWidth, pixelHeight, scale);
        return (centerX - (width * 0.5f), centerY - (height * 0.5f), width, height);
    }

    public static CustomMapSpriteLayerKind ParseLayer(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals(LayerBgPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            return CustomMapSpriteLayerKind.Bg;
        }

        if (value.Equals(LayerFgPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            return CustomMapSpriteLayerKind.Fg;
        }

        if (value.StartsWith("layer", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(value.AsSpan(5), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
        {
            var clamped = Math.Clamp(index, 0, 6);
            return (CustomMapSpriteLayerKind)((int)CustomMapSpriteLayerKind.Layer0 + clamped);
        }

        return CustomMapSpriteLayerKind.Bg;
    }

    public static string ToLayerPropertyValue(CustomMapSpriteLayerKind layer)
    {
        return layer switch
        {
            CustomMapSpriteLayerKind.Fg => LayerFgPropertyValue,
            CustomMapSpriteLayerKind.Bg => LayerBgPropertyValue,
            _ when layer >= CustomMapSpriteLayerKind.Layer0 && layer <= CustomMapSpriteLayerKind.Layer6
                => $"layer{(int)layer - (int)CustomMapSpriteLayerKind.Layer0}",
            _ => LayerBgPropertyValue,
        };
    }

    public static string CycleLayerPropertyValue(string? current)
    {
        var next = (int)ParseLayer(current) + 1;
        if (next > (int)CustomMapSpriteLayerKind.Fg)
        {
            next = (int)CustomMapSpriteLayerKind.Bg;
        }

        return ToLayerPropertyValue((CustomMapSpriteLayerKind)next);
    }

    public static string GetLayerDisplayLabel(string? value)
    {
        return ParseLayer(value) switch
        {
            CustomMapSpriteLayerKind.Bg => "BG",
            CustomMapSpriteLayerKind.Fg => "FG",
            CustomMapSpriteLayerKind.Layer0 => "Layer 1",
            CustomMapSpriteLayerKind.Layer1 => "Layer 2",
            CustomMapSpriteLayerKind.Layer2 => "Layer 3",
            CustomMapSpriteLayerKind.Layer3 => "Layer 4",
            CustomMapSpriteLayerKind.Layer4 => "Layer 5",
            CustomMapSpriteLayerKind.Layer5 => "Layer 6",
            CustomMapSpriteLayerKind.Layer6 => "Layer 7",
            _ => "BG",
        };
    }

    public static int ParseZOrder(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, MinZOrder, MaxZOrder)
            : 0;
    }

    public static string ToZOrderPropertyValue(int zOrder)
    {
        return Math.Clamp(zOrder, MinZOrder, MaxZOrder).ToString(CultureInfo.InvariantCulture);
    }

    public static string CycleZOrderPropertyValue(string? current, int delta)
    {
        return ToZOrderPropertyValue(ParseZOrder(current) + delta);
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

    public static bool TryGetParallaxFactors(
        CustomMapSpriteLayerKind layer,
        IReadOnlyList<CustomMapBuilderParallaxLayer> parallaxLayers,
        out float xFactor,
        out float yFactor)
    {
        xFactor = 1f;
        yFactor = 1f;
        if (layer is < CustomMapSpriteLayerKind.Layer0 or > CustomMapSpriteLayerKind.Layer6)
        {
            return layer == CustomMapSpriteLayerKind.Bg || layer == CustomMapSpriteLayerKind.Fg;
        }

        var layerIndex = (int)layer - (int)CustomMapSpriteLayerKind.Layer0;
        for (var index = 0; index < parallaxLayers.Count; index += 1)
        {
            var entry = parallaxLayers[index];
            if (entry.Index == layerIndex)
            {
                xFactor = entry.XFactor;
                yFactor = entry.YFactor;
                return true;
            }
        }

        return true;
    }

    public static bool TryGetParallaxFactors(
        CustomMapSpriteLayerKind layer,
        IReadOnlyList<CustomMapParallaxLayer> parallaxLayers,
        out float xFactor,
        out float yFactor)
    {
        xFactor = 1f;
        yFactor = 1f;
        if (layer is < CustomMapSpriteLayerKind.Layer0 or > CustomMapSpriteLayerKind.Layer6)
        {
            return layer == CustomMapSpriteLayerKind.Bg || layer == CustomMapSpriteLayerKind.Fg;
        }

        var layerIndex = (int)layer - (int)CustomMapSpriteLayerKind.Layer0;
        for (var index = 0; index < parallaxLayers.Count; index += 1)
        {
            var entry = parallaxLayers[index];
            if (entry.Index == layerIndex)
            {
                xFactor = entry.XFactor;
                yFactor = entry.YFactor;
                return true;
            }
        }

        return true;
    }

    public static string FindOrAssignImageResource(
        string sourceFilePath,
        IDictionary<string, CustomMapBuilderResource> resources)
    {
        var fullPath = Path.GetFullPath(sourceFilePath);
        foreach (var resource in resources.Values)
        {
            if (!string.IsNullOrWhiteSpace(resource.SourcePath)
                && PathsEqual(resource.SourcePath, fullPath))
            {
                return resource.Name;
            }
        }

        var baseName = Path.GetFileNameWithoutExtension(fullPath);
        var resourceName = SanitizeResourceName(baseName);
        var suffix = 2;
        while (resources.ContainsKey(resourceName))
        {
            var existing = resources[resourceName];
            if (!string.IsNullOrWhiteSpace(existing.SourcePath)
                && PathsEqual(existing.SourcePath, fullPath))
            {
                return resourceName;
            }

            resourceName = $"{SanitizeResourceName(baseName)}_{suffix}";
            suffix += 1;
        }

        resources[resourceName] = CustomMapBuilderResourceCodec.FromFile(
            resourceName,
            fullPath,
            CustomMapBuilderResourceKind.CustomSprite);
        return resourceName;
    }

    private static string SanitizeResourceName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return sanitized.Length == 0 ? "sprite" : sanitized;
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }
}

public static class CustomMapSpriteParallax
{
    public static (float ScreenX, float ScreenY) WorldToScreen(
        float worldX,
        float worldY,
        CustomMapSpriteLayerKind layer,
        float cameraX,
        float cameraY,
        int viewportWidth,
        int viewportHeight,
        IReadOnlyList<CustomMapBuilderParallaxLayer> parallaxLayers)
    {
        return WorldToScreen(
            worldX,
            worldY,
            layer,
            cameraX,
            cameraY,
            viewportWidth,
            viewportHeight,
            CustomMapCustomSpriteMetadata.TryGetParallaxFactors(layer, parallaxLayers, out var xFactor, out var yFactor),
            xFactor,
            yFactor);
    }

    public static (float ScreenX, float ScreenY) WorldToScreen(
        float worldX,
        float worldY,
        CustomMapSpriteLayerKind layer,
        float cameraX,
        float cameraY,
        int viewportWidth,
        int viewportHeight,
        IReadOnlyList<CustomMapParallaxLayer> parallaxLayers)
    {
        CustomMapCustomSpriteMetadata.TryGetParallaxFactors(layer, parallaxLayers, out var xFactor, out var yFactor);
        return WorldToScreen(
            worldX,
            worldY,
            layer,
            cameraX,
            cameraY,
            viewportWidth,
            viewportHeight,
            hasParallaxFactors: true,
            xFactor,
            yFactor);
    }

    private static (float ScreenX, float ScreenY) WorldToScreen(
        float worldX,
        float worldY,
        CustomMapSpriteLayerKind layer,
        float cameraX,
        float cameraY,
        int viewportWidth,
        int viewportHeight,
        bool hasParallaxFactors,
        float xFactor,
        float yFactor)
    {
        _ = hasParallaxFactors;
        if (layer is CustomMapSpriteLayerKind.Bg or CustomMapSpriteLayerKind.Fg)
        {
            return (worldX - cameraX, worldY - cameraY);
        }

        var screenX = ComputeParallaxAxis(worldX, cameraX, viewportWidth, xFactor) - cameraX;
        var screenY = ComputeParallaxAxis(worldY, cameraY, viewportHeight, yFactor) - cameraY;
        return (screenX, screenY);
    }

    private static float ComputeParallaxAxis(float worldAxis, float cameraAxis, int viewportSize, float factor)
    {
        if (MathF.Abs(factor) <= 0.0001f)
        {
            return worldAxis;
        }

        var origin = cameraAxis + (viewportSize * 0.5f);
        var parallax = origin / (factor * factor);
        return worldAxis + origin - parallax;
    }
}
