using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

public enum MapWeatherKind
{
    None,
    Rain,
    Snow,
    Leaves,
}

public enum MapWeatherIntensity
{
    // Medium is the zero value so default(MapWeather) equals "no weather" as parsed.
    Medium,
    Light,
    Heavy,

    /// <summary>Not just denser: a different look (snowstorm, thunderstorm, gale).</summary>
    Storm,
}

public enum MapWeatherWind
{
    Calm,
    Left,
    Right,
}

/// <summary>
/// Map-level ambient weather chosen by the map author. Purely presentational: the
/// simulation never reads it, so it cannot affect gameplay or network state.
/// </summary>
public readonly record struct MapWeather(
    MapWeatherKind Kind,
    MapWeatherIntensity Intensity = MapWeatherIntensity.Medium,
    MapWeatherWind Wind = MapWeatherWind.Calm)
{
    public static MapWeather None => default;

    public bool IsActive => Kind != MapWeatherKind.None;
}

/// <summary>
/// Reads and writes the <c>weather</c>, <c>weatherIntensity</c> and <c>weatherWind</c>
/// map metadata keys. Missing or unknown values fall back to no weather, medium
/// intensity and calm wind, so older maps and clients are unaffected.
/// </summary>
public static class MapWeatherMetadata
{
    public const string WeatherPropertyKey = "weather";
    public const string IntensityPropertyKey = "weatherIntensity";
    public const string WindPropertyKey = "weatherWind";

    public const string NonePropertyValue = "none";
    public const string RainPropertyValue = "rain";
    public const string SnowPropertyValue = "snow";
    public const string LeavesPropertyValue = "leaves";

    public const string LightPropertyValue = "light";
    public const string MediumPropertyValue = "medium";
    public const string HeavyPropertyValue = "heavy";
    public const string StormPropertyValue = "storm";

    public const string CalmPropertyValue = "calm";
    public const string LeftPropertyValue = "left";
    public const string RightPropertyValue = "right";

    public static bool IsEditableMapMetadataKey(string key)
    {
        return key.Equals(WeatherPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(IntensityPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(WindPropertyKey, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Intensity and wind only matter once a weather kind is chosen.</summary>
    public static bool IsWeatherDetailKey(string key)
    {
        return key.Equals(IntensityPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(WindPropertyKey, StringComparison.OrdinalIgnoreCase);
    }

    public static MapWeather Parse(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null)
        {
            return MapWeather.None;
        }

        metadata.TryGetValue(WeatherPropertyKey, out var kind);
        metadata.TryGetValue(IntensityPropertyKey, out var intensity);
        metadata.TryGetValue(WindPropertyKey, out var wind);
        return new MapWeather(ParseKind(kind), ParseIntensity(intensity), ParseWind(wind));
    }

    public static MapWeatherKind ParseKind(string? value)
    {
        if (string.Equals(value?.Trim(), RainPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            return MapWeatherKind.Rain;
        }

        if (string.Equals(value?.Trim(), SnowPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            return MapWeatherKind.Snow;
        }

        if (string.Equals(value?.Trim(), LeavesPropertyValue, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value?.Trim(), "autumn", StringComparison.OrdinalIgnoreCase))
        {
            return MapWeatherKind.Leaves;
        }

        return MapWeatherKind.None;
    }

    public static MapWeatherIntensity ParseIntensity(string? value)
    {
        if (string.Equals(value?.Trim(), LightPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            return MapWeatherIntensity.Light;
        }

        if (string.Equals(value?.Trim(), HeavyPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            return MapWeatherIntensity.Heavy;
        }

        if (string.Equals(value?.Trim(), StormPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            return MapWeatherIntensity.Storm;
        }

        return MapWeatherIntensity.Medium;
    }

    public static MapWeatherWind ParseWind(string? value)
    {
        if (string.Equals(value?.Trim(), LeftPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            return MapWeatherWind.Left;
        }

        if (string.Equals(value?.Trim(), RightPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            return MapWeatherWind.Right;
        }

        return MapWeatherWind.Calm;
    }

    public static string ToPropertyValue(MapWeatherKind kind) => kind switch
    {
        MapWeatherKind.Rain => RainPropertyValue,
        MapWeatherKind.Snow => SnowPropertyValue,
        MapWeatherKind.Leaves => LeavesPropertyValue,
        _ => NonePropertyValue,
    };

    public static string ToPropertyValue(MapWeatherIntensity intensity) => intensity switch
    {
        MapWeatherIntensity.Light => LightPropertyValue,
        MapWeatherIntensity.Heavy => HeavyPropertyValue,
        MapWeatherIntensity.Storm => StormPropertyValue,
        _ => MediumPropertyValue,
    };

    public static string ToPropertyValue(MapWeatherWind wind) => wind switch
    {
        MapWeatherWind.Left => LeftPropertyValue,
        MapWeatherWind.Right => RightPropertyValue,
        _ => CalmPropertyValue,
    };

    public static string CycleKindPropertyValue(string? current) =>
        ToPropertyValue((MapWeatherKind)(((int)ParseKind(current) + 1) % 4));

    public static string CycleIntensityPropertyValue(string? current) => ParseIntensity(current) switch
    {
        MapWeatherIntensity.Light => MediumPropertyValue,
        MapWeatherIntensity.Medium => HeavyPropertyValue,
        MapWeatherIntensity.Heavy => StormPropertyValue,
        _ => LightPropertyValue,
    };

    public static string CycleWindPropertyValue(string? current) =>
        ToPropertyValue((MapWeatherWind)(((int)ParseWind(current) + 1) % 3));

    /// <summary>Cycles whichever weather key is given; returns false for other keys.</summary>
    public static bool TryCyclePropertyValue(string key, string? current, out string next)
    {
        if (key.Equals(WeatherPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            next = CycleKindPropertyValue(current);
            return true;
        }

        if (key.Equals(IntensityPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            next = CycleIntensityPropertyValue(current);
            return true;
        }

        if (key.Equals(WindPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            next = CycleWindPropertyValue(current);
            return true;
        }

        next = current ?? string.Empty;
        return false;
    }

    public static string GetKindDisplayLabel(string? value) => ParseKind(value) switch
    {
        MapWeatherKind.Rain => "Rain",
        MapWeatherKind.Snow => "Snow",
        MapWeatherKind.Leaves => "Autumn leaves",
        _ => "None",
    };

    public static string GetIntensityDisplayLabel(string? value) => ParseIntensity(value) switch
    {
        MapWeatherIntensity.Light => "Light",
        MapWeatherIntensity.Heavy => "Heavy",
        MapWeatherIntensity.Storm => "Storm",
        _ => "Medium",
    };

    public static string GetWindDisplayLabel(string? value) => ParseWind(value) switch
    {
        MapWeatherWind.Left => "Blowing left",
        MapWeatherWind.Right => "Blowing right",
        _ => "Calm",
    };

    /// <summary>Row label for a weather key, or null for other keys.</summary>
    public static string? TryFormatRowLabel(string key, string? value)
    {
        if (key.Equals(WeatherPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Weather: {GetKindDisplayLabel(value)}";
        }

        if (key.Equals(IntensityPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Weather intensity: {GetIntensityDisplayLabel(value)}";
        }

        if (key.Equals(WindPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Weather wind: {GetWindDisplayLabel(value)}";
        }

        return null;
    }

    /// <summary>
    /// Writes normalised weather keys into map metadata. No weather removes all three
    /// keys so maps without weather stay byte-identical to before.
    /// </summary>
    public static void Normalize(IDictionary<string, string> metadata)
    {
        metadata.TryGetValue(WeatherPropertyKey, out var kindValue);
        var kind = ParseKind(kindValue);
        if (kind == MapWeatherKind.None)
        {
            metadata.Remove(WeatherPropertyKey);
            metadata.Remove(IntensityPropertyKey);
            metadata.Remove(WindPropertyKey);
            return;
        }

        metadata.TryGetValue(IntensityPropertyKey, out var intensityValue);
        metadata.TryGetValue(WindPropertyKey, out var windValue);
        metadata[WeatherPropertyKey] = ToPropertyValue(kind);
        metadata[IntensityPropertyKey] = ToPropertyValue(ParseIntensity(intensityValue));
        metadata[WindPropertyKey] = ToPropertyValue(ParseWind(windValue));
    }
}
