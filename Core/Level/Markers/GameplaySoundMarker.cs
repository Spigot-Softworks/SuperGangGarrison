using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

public enum GameplaySoundMode
{
    Sound,
    Music,
}

public readonly record struct GameplaySoundMarker(
    float X,
    float Y,
    string TriggerRef,
    string SoundName,
    GameplaySoundMode Mode,
    bool Crossfade,
    float CrossfadeSeconds,
    int TriggerNodeIndex = -1)
{
    public bool UsesTrigger => TriggerNodeIndex >= 0;

    public GameplaySoundMarker WithTriggerNodeIndex(int triggerNodeIndex) =>
        this with { TriggerNodeIndex = triggerNodeIndex };
}

public static class GameplaySoundMetadata
{
    public const string EntityType = "gameplaySound";
    public const string TriggerPropertyKey = "trigger";
    public const string SoundPropertyKey = "sound";
    public const string ModePropertyKey = "mode";
    public const string CrossfadePropertyKey = "crossfade";
    public const string CrossfadeSecondsPropertyKey = "crossfadeSeconds";

    public const string DefaultProperties =
        "trigger=;sound=;mode=sound;crossfade=true;crossfadeSeconds=1.5";

    public static bool IsGameplaySoundEntityType(string type) =>
        type.Equals(EntityType, StringComparison.OrdinalIgnoreCase);

    public static GameplaySoundMarker FromProperties(
        float x,
        float y,
        IReadOnlyDictionary<string, string> properties) => new(
        x,
        y,
        ReadProperty(properties, TriggerPropertyKey, string.Empty),
        ReadProperty(properties, SoundPropertyKey, string.Empty),
        ParseMode(properties),
        ParseBool(properties, CrossfadePropertyKey, true),
        ParsePositiveFloat(properties, CrossfadeSecondsPropertyKey, 1.5f, 0f, 60f));

    public static GameplaySoundMode ParseMode(IReadOnlyDictionary<string, string>? properties) =>
        ReadOptional(properties, ModePropertyKey) switch
        {
            string value when value.Equals("music", StringComparison.OrdinalIgnoreCase)
                || value.Equals("track", StringComparison.OrdinalIgnoreCase) => GameplaySoundMode.Music,
            _ => GameplaySoundMode.Sound,
        };

    public static string CycleModePropertyValue(string? value) =>
        ParseMode(ToDictionary(ModePropertyKey, value)) == GameplaySoundMode.Sound ? "music" : "sound";

    public static string CycleCrossfadePropertyValue(string? value) =>
        ParseBool(ToDictionary(CrossfadePropertyKey, value), CrossfadePropertyKey, true) ? "false" : "true";

    public static string GetModeDisplayLabel(string? value) =>
        ParseMode(ToDictionary(ModePropertyKey, value)) == GameplaySoundMode.Music ? "Music" : "Sound";

    private static string ReadProperty(IReadOnlyDictionary<string, string> properties, string key, string fallback) =>
        properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;

    private static string? ReadOptional(IReadOnlyDictionary<string, string>? properties, string key) =>
        properties is not null
            && properties.TryGetValue(key, out var value)
            && !string.IsNullOrWhiteSpace(value)
                ? value.Trim()
                : null;

    private static bool ParseBool(IReadOnlyDictionary<string, string>? properties, string key, bool fallback)
    {
        var value = ReadOptional(properties, key);
        if (value is null)
        {
            return fallback;
        }

        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    private static float ParsePositiveFloat(
        IReadOnlyDictionary<string, string> properties,
        string key,
        float fallback,
        float min,
        float max)
    {
        if (!properties.TryGetValue(key, out var value)
            || !float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            || !float.IsFinite(parsed))
        {
            return fallback;
        }

        return float.Clamp(parsed, min, max);
    }

    private static IReadOnlyDictionary<string, string> ToDictionary(string key, string? value) =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [key] = value ?? string.Empty,
        };
}
