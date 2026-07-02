using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Core;

public readonly record struct HealthPackSpawnMarker(
    float X,
    float Y,
    HealthPackSize Size,
    int RespawnTicks)
{
    public const int DefaultRespawnSeconds = 10;

    public static HealthPackSpawnMarker FromProperties(
        float x,
        float y,
        IReadOnlyDictionary<string, string> properties,
        int ticksPerSecond = SimulationConfig.DefaultTicksPerSecond) => new(
        x,
        y,
        HealthPackMetadata.ParseSize(properties),
        HealthPackMetadata.ParseRespawnTicks(properties, ticksPerSecond));
}

public static class HealthPackMetadata
{
    public const string HealthPackEntityType = "healthPack";
    public const string SizePropertyKey = "size";
    public const string RespawnSecondsPropertyKey = "respawnSeconds";
    public const string SmallSizeValue = "small";
    public const string MediumSizeValue = "medium";
    public const string MediumSpriteName = "MedkitMediumS";
    public const string MediumStaticSpriteName = "MedkitMediumStaticS";
    public const string SmallSpriteName = "MedkitSmallS";

    public static bool IsHealthPackEntityType(string type) =>
        type.Equals(HealthPackEntityType, StringComparison.OrdinalIgnoreCase);

    public static HealthPackSize ParseSize(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is not null
            && properties.TryGetValue(SizePropertyKey, out var value))
        {
            return ParseSize(value);
        }

        return HealthPackSize.Medium;
    }

    public static HealthPackSize ParseSize(string? value)
    {
        return value?.Trim().Equals(SmallSizeValue, StringComparison.OrdinalIgnoreCase) == true
            ? HealthPackSize.Small
            : HealthPackSize.Medium;
    }

    public static string ToSizePropertyValue(HealthPackSize size) =>
        size == HealthPackSize.Small ? SmallSizeValue : MediumSizeValue;

    public static string CycleSizePropertyValue(string? value) =>
        ToSizePropertyValue(ParseSize(value) == HealthPackSize.Small ? HealthPackSize.Medium : HealthPackSize.Small);

    public static string GetSizeDisplayLabel(string? value) =>
        ParseSize(value) == HealthPackSize.Small ? "Small" : "Medium";

    public static int ParseRespawnTicks(
        IReadOnlyDictionary<string, string>? properties,
        int ticksPerSecond = SimulationConfig.DefaultTicksPerSecond)
    {
        if (properties is not null
            && properties.TryGetValue(RespawnSecondsPropertyKey, out var value))
        {
            return ParseRespawnTicks(value, ticksPerSecond);
        }

        return HealthPackSpawnMarker.DefaultRespawnSeconds * Math.Max(1, ticksPerSecond);
    }

    public static int ParseRespawnTicks(string? value, int ticksPerSecond = SimulationConfig.DefaultTicksPerSecond)
    {
        var seconds = ParseRespawnSeconds(value);
        return Math.Max(1, (int)MathF.Round(seconds * Math.Max(1, ticksPerSecond)));
    }

    public static float ParseRespawnSeconds(string? value)
    {
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && float.IsFinite(parsed))
        {
            return MathF.Max(0.1f, parsed);
        }

        return HealthPackSpawnMarker.DefaultRespawnSeconds;
    }

    public static string ToRespawnSecondsPropertyValue(float seconds) =>
        MathF.Max(0.1f, seconds).ToString("0.###", CultureInfo.InvariantCulture);
}
