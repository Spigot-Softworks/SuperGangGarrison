using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Core;

public enum MapLightFlicker
{
    Steady,
    Flicker,
    Pulse,
}

/// <summary>A placed point light. Presentation only; only visible when the map has lighting on.</summary>
public readonly record struct MapLightMarker(
    float X,
    float Y,
    MapLightingColor Color,
    float Radius,
    int Intensity,
    MapLightFlicker Flicker)
{
    public MapLightMarker Scale(float scale) => this with { X = X * scale, Y = Y * scale, Radius = Radius * scale };
}

public static class MapLightMetadata
{
    public const string EntityType = "light";
    public const string ColorKey = "color";
    public const string RadiusKey = "radius";
    public const string IntensityKey = "intensity";
    public const string FlickerKey = "flicker";

    public const float DefaultRadius = 160f;
    public const float MinRadius = 8f;
    public const float MaxRadius = 2000f;
    public const int DefaultIntensity = 100;
    public static MapLightingColor DefaultColor => new(255, 207, 128);

    public static bool IsLightEntityType(string? type)
    {
        return !string.IsNullOrWhiteSpace(type)
            && type.Equals(EntityType, StringComparison.OrdinalIgnoreCase);
    }

    public static MapLightFlicker ParseFlicker(string? value)
    {
        if (string.Equals(value?.Trim(), "flicker", StringComparison.OrdinalIgnoreCase))
        {
            return MapLightFlicker.Flicker;
        }

        if (string.Equals(value?.Trim(), "pulse", StringComparison.OrdinalIgnoreCase))
        {
            return MapLightFlicker.Pulse;
        }

        return MapLightFlicker.Steady;
    }

    public static string ToFlickerValue(MapLightFlicker flicker) => flicker switch
    {
        MapLightFlicker.Flicker => "flicker",
        MapLightFlicker.Pulse => "pulse",
        _ => "steady",
    };

    public static string CycleFlickerValue(string? current) =>
        ToFlickerValue((MapLightFlicker)(((int)ParseFlicker(current) + 1) % 3));

    public static MapLightMarker FromProperties(float x, float y, IReadOnlyDictionary<string, string> properties)
    {
        var color = properties.TryGetValue(ColorKey, out var rawColor) && MapLightingColor.TryParse(rawColor, out var parsedColor)
            ? parsedColor
            : DefaultColor;
        var radius = properties.TryGetValue(RadiusKey, out var rawRadius)
            && float.TryParse(rawRadius.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedRadius)
            && float.IsFinite(parsedRadius)
                ? Math.Clamp(parsedRadius, MinRadius, MaxRadius)
                : DefaultRadius;
        var intensity = properties.TryGetValue(IntensityKey, out var rawIntensity)
            && int.TryParse(rawIntensity.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedIntensity)
                ? Math.Clamp(parsedIntensity, 0, 200)
                : DefaultIntensity;
        properties.TryGetValue(FlickerKey, out var flicker);
        return new MapLightMarker(x, y, color, radius, intensity, ParseFlicker(flicker));
    }
}

internal sealed class MapLightMapEntityRuntimeImporter : ICustomMapEntityRuntimeImporter
{
    public string EntityType => MapLightMetadata.EntityType;

    public bool TryImport(CustomMapEntityImportArgs args, CustomMapEntityImportContext context)
    {
        if (!MapLightMetadata.IsLightEntityType(args.Type))
        {
            return false;
        }

        context.MapLights.Add(MapLightMetadata.FromProperties(args.X, args.Y, args.Properties));
        return true;
    }
}
