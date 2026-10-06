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

/// <summary>How a placed light fades toward its edge.</summary>
public enum MapLightFalloff
{
    /// <summary>Follow the map's lighting falloff (smooth or retro).</summary>
    Map,
    Smooth,
    Retro,
}

/// <summary>A placed point light. Presentation only; only visible when the map has lighting on.</summary>
public readonly record struct MapLightMarker(
    float X,
    float Y,
    MapLightingColor Color,
    float Radius,
    int Intensity,
    MapLightFlicker Flicker,
    MapLightFalloff Falloff = MapLightFalloff.Map,
    float Direction = 0f,
    float Spread = MapLightMetadata.FullSpread)
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
    public const string FalloffKey = "falloff";
    public const string DirectionKey = "direction";
    public const string SpreadKey = "spread";

    /// <summary>A spread this wide (or wider) is an ordinary round light.</summary>
    public const float FullSpread = 360f;
    public const float MinSpread = 10f;

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

    public static MapLightFalloff ParseFalloff(string? value)
    {
        var text = value?.Trim();
        if (string.Equals(text, "smooth", StringComparison.OrdinalIgnoreCase))
        {
            return MapLightFalloff.Smooth;
        }

        if (string.Equals(text, "retro", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "bands", StringComparison.OrdinalIgnoreCase))
        {
            return MapLightFalloff.Retro;
        }

        return MapLightFalloff.Map;
    }

    public static string ToFalloffValue(MapLightFalloff falloff) => falloff switch
    {
        MapLightFalloff.Smooth => "smooth",
        MapLightFalloff.Retro => "retro",
        _ => "map",
    };

    /// <summary>Map -> Smooth -> Retro -> Map.</summary>
    public static string CycleFalloffValue(string? current) =>
        ToFalloffValue((MapLightFalloff)(((int)ParseFalloff(current) + 1) % 3));

    /// <summary>
    /// Whether this light draws with retro bands, given the map's own falloff style.
    /// </summary>
    public static bool UsesRetroBands(MapLightFalloff falloff, bool mapBanded) => falloff switch
    {
        MapLightFalloff.Smooth => false,
        MapLightFalloff.Retro => true,
        _ => mapBanded,
    };

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
        properties.TryGetValue(FalloffKey, out var falloff);
        var direction = properties.TryGetValue(DirectionKey, out var rawDirection)
            && float.TryParse(rawDirection.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDirection)
            && float.IsFinite(parsedDirection)
                ? NormalizeDirection(parsedDirection)
                : 0f;
        var spread = properties.TryGetValue(SpreadKey, out var rawSpread)
            && float.TryParse(rawSpread.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedSpread)
            && float.IsFinite(parsedSpread)
                ? Math.Clamp(parsedSpread, MinSpread, FullSpread)
                : FullSpread;
        return new MapLightMarker(x, y, color, radius, intensity, ParseFlicker(flicker), ParseFalloff(falloff), direction, spread);
    }

    /// <summary>Degrees in [0, 360): 0 = right, 90 = up, 180 = left, 270 = down.</summary>
    public static float NormalizeDirection(float degrees)
    {
        var result = degrees % 360f;
        return result < 0f ? result + 360f : result;
    }

    /// <summary>True for a spotlight (a cone narrower than a full circle).</summary>
    public static bool IsDirectional(float spread) => spread < FullSpread - 5f;

    /// <summary>World-space unit vector for a direction (screen y points down).</summary>
    public static (float X, float Y) GetDirectionVector(float degrees)
    {
        var radians = degrees * (MathF.PI / 180f);
        return (MathF.Cos(radians), -MathF.Sin(radians));
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
