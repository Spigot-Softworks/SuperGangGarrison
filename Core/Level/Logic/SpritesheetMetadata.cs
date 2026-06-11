using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;

namespace OpenGarrison.Core;

public enum SpritesheetLoopingMode
{
    Loop,
    Reverse,
    PlayOnce,
}

public readonly record struct SpritesheetFrameRect(int X, int Y, int Width, int Height);

public readonly record struct SpritesheetConfiguration(
    string ImageResourceName,
    CustomMapSpriteLayerKind Layer,
    int ZOrder,
    float Scale,
    int Columns,
    int Rows,
    bool Autostart,
    string StartInputRef,
    string StopInputRef,
    bool Autoplay,
    string NextFrameInputRef,
    int Framerate,
    SpritesheetLoopingMode LoopingMode)
{
    public bool HasImage => !string.IsNullOrWhiteSpace(ImageResourceName);

    public int FrameCount => Math.Max(1, Math.Max(1, Columns) * Math.Max(1, Rows));
}

public static class SpritesheetMetadata
{
    public const string SpritesheetEntityType = "logicSpritesheet";
    public const string ImagePropertyKey = "image";
    public const string LayerPropertyKey = "layer";
    public const string ZOrderPropertyKey = "zOrder";
    public const string ScalePropertyKey = "scale";
    public const string ColumnsPropertyKey = "columns";
    public const string RowsPropertyKey = "rows";
    public const string AutostartPropertyKey = "autostart";
    public const string StartInputPropertyKey = "startInput";
    public const string StopInputPropertyKey = "stopInput";
    public const string AutoplayPropertyKey = "autoplay";
    public const string NextFrameInputPropertyKey = "nextFrameInput";
    public const string FrameratePropertyKey = "framerate";
    public const string LoopingModePropertyKey = "loopingMode";
    public const string LayerBgPropertyValue = "bg";
    public const string LayerFgPropertyValue = "fg";
    public const string LoopingModeLoopValue = "loop";
    public const string LoopingModeReverseValue = "reverse";
    public const string LoopingModePlayOnceValue = "playOnce";
    public const int MinZOrder = 0;
    public const int MaxZOrder = 100;
    public const int MinFramerate = 1;
    public const int MaxFramerate = 30;
    public const int DefaultFramerate = 10;
    public const int DefaultColumns = 1;
    public const int DefaultRows = 1;

    public static bool IsSpritesheetEntityType(string? type)
    {
        return !string.IsNullOrWhiteSpace(type)
            && type.Equals(SpritesheetEntityType, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsLogicInputPropertyKey(string key)
    {
        return key.Equals(StartInputPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(StopInputPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(NextFrameInputPropertyKey, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ShouldShowProperty(
        IReadOnlyDictionary<string, string>? properties,
        string key)
    {
        if (key.Equals(StartInputPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return !ParseAutostart(properties?.TryGetValue(AutostartPropertyKey, out var autostart) == true ? autostart : null);
        }

        if (key.Equals(NextFrameInputPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return !ParseAutoplay(properties?.TryGetValue(AutoplayPropertyKey, out var autoplay) == true ? autoplay : null);
        }

        if (key.Equals(FrameratePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return ParseAutoplay(properties?.TryGetValue(AutoplayPropertyKey, out var autoplay) == true ? autoplay : null);
        }

        return true;
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

        if (!properties.ContainsKey(ColumnsPropertyKey))
        {
            properties[ColumnsPropertyKey] = DefaultColumns.ToString(CultureInfo.InvariantCulture);
        }

        if (!properties.ContainsKey(RowsPropertyKey))
        {
            properties[RowsPropertyKey] = DefaultRows.ToString(CultureInfo.InvariantCulture);
        }

        if (!properties.ContainsKey(AutostartPropertyKey))
        {
            properties[AutostartPropertyKey] = "true";
        }

        if (!properties.ContainsKey(AutoplayPropertyKey))
        {
            properties[AutoplayPropertyKey] = "true";
        }

        if (!properties.ContainsKey(FrameratePropertyKey))
        {
            properties[FrameratePropertyKey] = DefaultFramerate.ToString(CultureInfo.InvariantCulture);
        }

        if (!properties.ContainsKey(LoopingModePropertyKey))
        {
            properties[LoopingModePropertyKey] = LoopingModeLoopValue;
        }

        properties.TryAdd(StartInputPropertyKey, string.Empty);
        properties.TryAdd(StopInputPropertyKey, string.Empty);
        properties.TryAdd(NextFrameInputPropertyKey, string.Empty);
    }

    public static SpritesheetConfiguration ParseConfiguration(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null)
        {
            return DefaultConfiguration();
        }

        properties.TryGetValue(ImagePropertyKey, out var image);
        properties.TryGetValue(LayerPropertyKey, out var layer);
        properties.TryGetValue(ZOrderPropertyKey, out var zOrder);
        properties.TryGetValue(ScalePropertyKey, out var scale);
        properties.TryGetValue(ColumnsPropertyKey, out var columns);
        properties.TryGetValue(RowsPropertyKey, out var rows);
        properties.TryGetValue(AutostartPropertyKey, out var autostart);
        properties.TryGetValue(StartInputPropertyKey, out var startInput);
        properties.TryGetValue(StopInputPropertyKey, out var stopInput);
        properties.TryGetValue(AutoplayPropertyKey, out var autoplay);
        properties.TryGetValue(NextFrameInputPropertyKey, out var nextFrameInput);
        properties.TryGetValue(FrameratePropertyKey, out var framerate);
        properties.TryGetValue(LoopingModePropertyKey, out var loopingMode);
        return new SpritesheetConfiguration(
            image?.Trim() ?? string.Empty,
            CustomMapCustomSpriteMetadata.ParseLayer(layer),
            CustomMapCustomSpriteMetadata.ParseZOrder(zOrder),
            CustomMapCustomSpriteMetadata.ParseScale(scale),
            ParseGridDimension(columns, DefaultColumns),
            ParseGridDimension(rows, DefaultRows),
            ParseAutostart(autostart),
            startInput?.Trim() ?? string.Empty,
            stopInput?.Trim() ?? string.Empty,
            ParseAutoplay(autoplay),
            nextFrameInput?.Trim() ?? string.Empty,
            ParseFramerate(framerate),
            ParseLoopingMode(loopingMode));
    }

    public static bool TryParsePngDimensions(byte[] bytes, out int width, out int height)
    {
        return CustomMapCustomSpriteMetadata.TryParsePngDimensions(bytes, out width, out height);
    }

    public static (int FramePixelWidth, int FramePixelHeight) ResolveFramePixelDimensions(
        int imagePixelWidth,
        int imagePixelHeight,
        SpritesheetConfiguration configuration)
    {
        var columns = Math.Max(1, configuration.Columns);
        var rows = Math.Max(1, configuration.Rows);
        return (
            Math.Max(1, imagePixelWidth / columns),
            Math.Max(1, imagePixelHeight / rows));
    }

    public static (float Width, float Height) ResolveWorldDimensions(
        int imagePixelWidth,
        int imagePixelHeight,
        float scale,
        SpritesheetConfiguration configuration)
    {
        var (frameWidth, frameHeight) = ResolveFramePixelDimensions(imagePixelWidth, imagePixelHeight, configuration);
        return (
            MathF.Max(1f, frameWidth * scale),
            MathF.Max(1f, frameHeight * scale));
    }

    public static (int Column, int Row) ResolveFrameGridPosition(int frameIndex, SpritesheetConfiguration configuration)
    {
        var columns = Math.Max(1, configuration.Columns);
        var rows = Math.Max(1, configuration.Rows);
        var frameCount = columns * rows;
        var clamped = Math.Clamp(frameIndex, 0, frameCount - 1);
        return (clamped % columns, clamped / columns);
    }

    public static SpritesheetFrameRect ResolveFrameSourceRectangle(
        int imagePixelWidth,
        int imagePixelHeight,
        int frameIndex,
        SpritesheetConfiguration configuration)
    {
        var (framePixelWidth, framePixelHeight) = ResolveFramePixelDimensions(imagePixelWidth, imagePixelHeight, configuration);
        var (column, row) = ResolveFrameGridPosition(frameIndex, configuration);
        return new SpritesheetFrameRect(
            column * framePixelWidth,
            row * framePixelHeight,
            framePixelWidth,
            framePixelHeight);
    }

    public static string FindOrAssignImageResource(
        string imagePath,
        IDictionary<string, CustomMapBuilderResource> resources)
    {
        return CustomMapCustomSpriteMetadata.FindOrAssignImageResource(imagePath, resources);
    }

    public static string ToScalePropertyValue(float scale) => CustomMapCustomSpriteMetadata.ToScalePropertyValue(scale);

    public static string ToZOrderPropertyValue(int zOrder) => CustomMapCustomSpriteMetadata.ToZOrderPropertyValue(zOrder);

    public static string CycleZOrderPropertyValue(string? current, int delta = 1)
        => CustomMapCustomSpriteMetadata.CycleZOrderPropertyValue(current, delta);

    public static string CycleLayerPropertyValue(string? current)
        => CustomMapCustomSpriteMetadata.CycleLayerPropertyValue(current);

    public static string GetLayerDisplayLabel(string? value) => CustomMapCustomSpriteMetadata.GetLayerDisplayLabel(value);

    public static string ToAutostartPropertyValue(bool value) => value ? "true" : "false";

    public static string ToAutoplayPropertyValue(bool value) => value ? "true" : "false";

    public static bool ParseAutostart(string? value)
    {
        return string.IsNullOrWhiteSpace(value) || ParseBool(value, fallback: true);
    }

    public static bool ParseAutoplay(string? value)
    {
        return string.IsNullOrWhiteSpace(value) || ParseBool(value, fallback: true);
    }

    public static string CycleAutostartPropertyValue(string? current)
    {
        return ToAutostartPropertyValue(!ParseAutostart(current));
    }

    public static string CycleAutoplayPropertyValue(string? current)
    {
        return ToAutoplayPropertyValue(!ParseAutoplay(current));
    }

    public static int ParseFramerate(string? value)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return DefaultFramerate;
        }

        return Math.Clamp(parsed, MinFramerate, MaxFramerate);
    }

    public static string ToFrameratePropertyValue(int value)
    {
        return Math.Clamp(value, MinFramerate, MaxFramerate).ToString(CultureInfo.InvariantCulture);
    }

    public static string CycleFrameratePropertyValue(string? current, int delta = 1)
    {
        return ToFrameratePropertyValue(ParseFramerate(current) + delta);
    }

    public static SpritesheetLoopingMode ParseLoopingMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return SpritesheetLoopingMode.Loop;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            LoopingModeReverseValue => SpritesheetLoopingMode.Reverse,
            LoopingModePlayOnceValue or "playonce" or "none" => SpritesheetLoopingMode.PlayOnce,
            _ => SpritesheetLoopingMode.Loop,
        };
    }

    public static string ToLoopingModePropertyValue(SpritesheetLoopingMode mode)
    {
        return mode switch
        {
            SpritesheetLoopingMode.Reverse => LoopingModeReverseValue,
            SpritesheetLoopingMode.PlayOnce => LoopingModePlayOnceValue,
            _ => LoopingModeLoopValue,
        };
    }

    public static string CycleLoopingModePropertyValue(string? current)
    {
        return ToLoopingModePropertyValue(ParseLoopingMode(current) switch
        {
            SpritesheetLoopingMode.Loop => SpritesheetLoopingMode.Reverse,
            SpritesheetLoopingMode.Reverse => SpritesheetLoopingMode.PlayOnce,
            _ => SpritesheetLoopingMode.Loop,
        });
    }

    public static string GetLoopingModeDisplayLabel(string? value)
    {
        return ParseLoopingMode(value) switch
        {
            SpritesheetLoopingMode.Reverse => "Reverse",
            SpritesheetLoopingMode.PlayOnce => "Play once",
            _ => "Loop",
        };
    }

    public static int ParseGridDimension(string? value, int fallback)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return fallback;
        }

        return Math.Clamp(parsed, 1, 64);
    }

    public static string ToGridDimensionPropertyValue(int value)
    {
        return Math.Clamp(value, 1, 64).ToString(CultureInfo.InvariantCulture);
    }

    public static string CycleGridDimensionPropertyValue(string? current, int fallback, int delta = 1)
    {
        return ToGridDimensionPropertyValue(ParseGridDimension(current, fallback) + delta);
    }

    private static SpritesheetConfiguration DefaultConfiguration()
    {
        return new SpritesheetConfiguration(
            string.Empty,
            CustomMapSpriteLayerKind.Bg,
            0,
            1f,
            DefaultColumns,
            DefaultRows,
            true,
            string.Empty,
            string.Empty,
            true,
            string.Empty,
            DefaultFramerate,
            SpritesheetLoopingMode.Loop);
    }

    private static bool ParseBool(string value, bool fallback)
    {
        if (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.Equals("false", StringComparison.OrdinalIgnoreCase) || value.Equals("0", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return fallback;
    }
}
