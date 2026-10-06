using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

public enum MapWeatherKind
{
    None,
    Rain,
    Snow,
    Leaves,

    /// <summary>Slow, blinking, softly glowing motes hovering near the ground.</summary>
    Fireflies,
}

/// <summary>How fireflies move and shine.</summary>
public enum MapFireflyStyle
{
    /// <summary>Real fireflies: wander, bob and blink on and off near the ground.</summary>
    Fireflies,

    /// <summary>Ambient glowing motes: float gently on the breeze and shine steadily.</summary>
    Subtle,
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
/// <param name="FireflyColor">Firefly colour (fireflies only).</param>
/// <param name="FireflyDensity">How many fireflies, 0-100 (fireflies only).</param>
/// <param name="FireflyGlow">How strongly they glow, 0-100 (fireflies only).</param>
/// <param name="FireflyStyle">Firefly motion and blinking, or gentle steady motes (fireflies only).</param>
/// <param name="RainColor">Colour of the rain drops (rain only); default is the usual pale blue.</param>
public readonly record struct MapWeather(
    MapWeatherKind Kind,
    MapWeatherIntensity Intensity = MapWeatherIntensity.Medium,
    MapWeatherWind Wind = MapWeatherWind.Calm,
    MapLightingColor FireflyColor = default,
    int FireflyDensity = MapWeatherMetadata.DefaultFireflyDensity,
    int FireflyGlow = MapWeatherMetadata.DefaultFireflyGlow,
    MapFireflyStyle FireflyStyle = MapFireflyStyle.Fireflies,
    MapLightingColor RainColor = default)
{
    public static MapWeather None => default;

    public bool IsActive => Kind != MapWeatherKind.None;

    /// <summary>The firefly colour, or the default yellow-green when none was set.</summary>
    public MapLightingColor ResolvedFireflyColor =>
        FireflyColor == default ? MapWeatherMetadata.DefaultFireflyColor : FireflyColor;

    /// <summary>The rain colour, or the default pale blue when none was set.</summary>
    public MapLightingColor ResolvedRainColor =>
        RainColor == default ? MapWeatherMetadata.DefaultRainColor : RainColor;
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
    public const string FireflyColorPropertyKey = "weatherFireflyColor";
    public const string FireflyDensityPropertyKey = "weatherFireflyDensity";
    public const string FireflyGlowPropertyKey = "weatherFireflyGlow";
    public const string FireflyStylePropertyKey = "weatherFireflyStyle";
    public const string FireflyStyleFirefliesValue = "fireflies";
    public const string FireflyStyleSubtleValue = "subtle";
    public const string RainColorPropertyKey = "weatherRainColor";

    public const int DefaultFireflyDensity = 50;
    public const int DefaultFireflyGlow = 60;

    /// <summary>Warm yellow-green, like real fireflies.</summary>
    public static MapLightingColor DefaultFireflyColor => new(214, 255, 110);

    /// <summary>The usual pale blue of the nearest rain layer; farther layers are drawn dimmer.</summary>
    public static MapLightingColor DefaultRainColor => new(186, 204, 228);

    public const string NonePropertyValue = "none";
    public const string RainPropertyValue = "rain";
    public const string SnowPropertyValue = "snow";
    public const string LeavesPropertyValue = "leaves";
    public const string FirefliesPropertyValue = "fireflies";

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
            || IsWeatherDetailKey(key);
    }

    /// <summary>Every weather key except the kind itself; they only matter once a kind is chosen.</summary>
    public static bool IsWeatherDetailKey(string key)
    {
        return key.Equals(IntensityPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(WindPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(RainColorPropertyKey, StringComparison.OrdinalIgnoreCase)
            || IsFireflyKey(key);
    }

    public static bool IsFireflyKey(string key)
    {
        return key.Equals(FireflyStylePropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(FireflyColorPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(FireflyDensityPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(FireflyGlowPropertyKey, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether a weather detail row applies to a kind: fireflies have their own colour,
    /// density and glow instead of an intensity; the others have no firefly settings.
    /// </summary>
    public static bool IsDetailKeyRelevant(string key, MapWeatherKind kind)
    {
        if (kind == MapWeatherKind.None)
        {
            return false;
        }

        if (IsFireflyKey(key))
        {
            return kind == MapWeatherKind.Fireflies;
        }

        if (key.Equals(RainColorPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return kind == MapWeatherKind.Rain;
        }

        if (key.Equals(IntensityPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return kind != MapWeatherKind.Fireflies;
        }

        return true;
    }

    /// <summary>The weather keys the map properties editor shows, in order.</summary>
    public static IReadOnlyList<string> EditorKeys { get; } =
    [
        WeatherPropertyKey,
        IntensityPropertyKey,
        WindPropertyKey,
        RainColorPropertyKey,
        FireflyStylePropertyKey,
        FireflyColorPropertyKey,
        FireflyDensityPropertyKey,
        FireflyGlowPropertyKey,
    ];

    public static MapWeather Parse(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null)
        {
            return MapWeather.None;
        }

        metadata.TryGetValue(WeatherPropertyKey, out var kind);
        metadata.TryGetValue(IntensityPropertyKey, out var intensity);
        metadata.TryGetValue(WindPropertyKey, out var wind);
        var weather = new MapWeather(ParseKind(kind), ParseIntensity(intensity), ParseWind(wind));
        if (weather.Kind == MapWeatherKind.Rain)
        {
            // Only a set colour is stored, so plain rain stays equal to its plain form.
            metadata.TryGetValue(RainColorPropertyKey, out var rainColor);
            return MapLightingColor.TryParse(rainColor, out var parsedRain) && parsedRain != DefaultRainColor
                ? weather with { RainColor = parsedRain }
                : weather;
        }

        if (weather.Kind != MapWeatherKind.Fireflies)
        {
            return weather;
        }

        // Firefly settings only exist for fireflies, so other weather stays equal to its plain form.
        metadata.TryGetValue(FireflyColorPropertyKey, out var fireflyColor);
        metadata.TryGetValue(FireflyDensityPropertyKey, out var fireflyDensity);
        metadata.TryGetValue(FireflyGlowPropertyKey, out var fireflyGlow);
        metadata.TryGetValue(FireflyStylePropertyKey, out var fireflyStyle);
        return weather with
        {
            FireflyStyle = ParseFireflyStyle(fireflyStyle),
            FireflyColor = ParseFireflyColor(fireflyColor),
            FireflyDensity = ParsePercent(fireflyDensity, DefaultFireflyDensity),
            FireflyGlow = ParsePercent(fireflyGlow, DefaultFireflyGlow),
        };
    }

    public static MapFireflyStyle ParseFireflyStyle(string? value) =>
        string.Equals(value?.Trim(), FireflyStyleSubtleValue, StringComparison.OrdinalIgnoreCase)
            ? MapFireflyStyle.Subtle
            : MapFireflyStyle.Fireflies;

    public static string ToFireflyStyleValue(MapFireflyStyle style) =>
        style == MapFireflyStyle.Subtle ? FireflyStyleSubtleValue : FireflyStyleFirefliesValue;

    public static MapLightingColor ParseRainColor(string? value) =>
        MapLightingColor.TryParse(value, out var color) ? color : DefaultRainColor;

    public static MapLightingColor ParseFireflyColor(string? value) =>
        MapLightingColor.TryParse(value, out var color) ? color : DefaultFireflyColor;

    public static int ParsePercent(string? value, int fallback) =>
        int.TryParse(value?.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, 0, 100)
            : fallback;

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

        if (string.Equals(value?.Trim(), FirefliesPropertyValue, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value?.Trim(), "firefly", StringComparison.OrdinalIgnoreCase))
        {
            return MapWeatherKind.Fireflies;
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
        MapWeatherKind.Fireflies => FirefliesPropertyValue,
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
        ToPropertyValue((MapWeatherKind)(((int)ParseKind(current) + 1) % 5));

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

        if (key.Equals(FireflyStylePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            next = ToFireflyStyleValue(ParseFireflyStyle(current) == MapFireflyStyle.Subtle ? MapFireflyStyle.Fireflies : MapFireflyStyle.Subtle);
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
        MapWeatherKind.Fireflies => "Fireflies",
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

        if (key.Equals(FireflyStylePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return ParseFireflyStyle(value) == MapFireflyStyle.Subtle
                ? "Firefly style: Subtle (gentle, steady)"
                : "Firefly style: Fireflies (wander, blink)";
        }

        if (key.Equals(FireflyColorPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Firefly colour (hex): {ParseFireflyColor(value).ToHex()}";
        }

        if (key.Equals(RainColorPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Rain colour (hex): {ParseRainColor(value).ToHex()}";
        }

        if (key.Equals(FireflyDensityPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Firefly density (0-100): {ParsePercent(value, DefaultFireflyDensity)}";
        }

        if (key.Equals(FireflyGlowPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Firefly glow (0-100): {ParsePercent(value, DefaultFireflyGlow)}";
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
            foreach (var key in EditorKeys)
            {
                metadata.Remove(key);
            }

            return;
        }

        metadata.TryGetValue(IntensityPropertyKey, out var intensityValue);
        metadata.TryGetValue(WindPropertyKey, out var windValue);
        metadata[WeatherPropertyKey] = ToPropertyValue(kind);
        metadata[IntensityPropertyKey] = ToPropertyValue(ParseIntensity(intensityValue));
        metadata[WindPropertyKey] = ToPropertyValue(ParseWind(windValue));
        metadata.TryGetValue(RainColorPropertyKey, out var rainColorValue);
        var rainColor = ParseRainColor(rainColorValue);
        if (kind == MapWeatherKind.Rain && rainColor != DefaultRainColor)
        {
            metadata[RainColorPropertyKey] = rainColor.ToHex();
        }
        else
        {
            // Default rain (and other weather) keeps maps free of the key.
            metadata.Remove(RainColorPropertyKey);
        }

        if (kind == MapWeatherKind.Fireflies)
        {
            metadata.TryGetValue(FireflyColorPropertyKey, out var colorValue);
            metadata.TryGetValue(FireflyDensityPropertyKey, out var densityValue);
            metadata.TryGetValue(FireflyGlowPropertyKey, out var glowValue);
            metadata.TryGetValue(FireflyStylePropertyKey, out var styleValue);
            metadata[FireflyStylePropertyKey] = ToFireflyStyleValue(ParseFireflyStyle(styleValue));
            metadata[FireflyColorPropertyKey] = ParseFireflyColor(colorValue).ToHex();
            metadata[FireflyDensityPropertyKey] = ParsePercent(densityValue, DefaultFireflyDensity).ToString(System.Globalization.CultureInfo.InvariantCulture);
            metadata[FireflyGlowPropertyKey] = ParsePercent(glowValue, DefaultFireflyGlow).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            // Other weather keeps maps free of firefly keys.
            metadata.Remove(FireflyStylePropertyKey);
            metadata.Remove(FireflyColorPropertyKey);
            metadata.Remove(FireflyDensityPropertyKey);
            metadata.Remove(FireflyGlowPropertyKey);
        }
    }
}
