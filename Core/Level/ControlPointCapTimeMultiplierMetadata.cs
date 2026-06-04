using System;
using System.Globalization;

namespace OpenGarrison.Core;

public static class ControlPointCapTimeMultiplierMetadata
{
    public const string PropertyKey = "capTimeMultiplier";
    public const string CustomPropertyKey = "capTimeMultiplierCustom";
    public const string DefaultPropertyValue = "1";

    public static bool IsCustom(IReadOnlyDictionary<string, string>? properties)
    {
        return properties is not null
            && properties.TryGetValue(CustomPropertyKey, out var rawValue)
            && IsTruthy(rawValue);
    }

    public static float ParseMultiplier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 1f;
        }

        return float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && parsed > 0f
            ? parsed
            : 1f;
    }

    public static string ToPropertyValue(float multiplier)
    {
        return multiplier.ToString("0.###", CultureInfo.InvariantCulture);
    }

    public static void MarkCustom(IDictionary<string, string> properties, float multiplier)
    {
        properties[PropertyKey] = ToPropertyValue(multiplier);
        properties[CustomPropertyKey] = "true";
    }

    public static void ClearCustom(IDictionary<string, string> properties)
    {
        properties.Remove(CustomPropertyKey);
    }

    public static (float Multiplier, bool IsCustom) Parse(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null)
        {
            return (1f, false);
        }

        var isCustom = IsCustom(properties);
        var multiplier = ParseMultiplier(
            properties.TryGetValue(PropertyKey, out var value) ? value : DefaultPropertyValue);
        return (multiplier, isCustom);
    }

    public static float ResolveAutoMultiplier(
        int totalControlPoints,
        int controlPointIndex,
        bool setupMode)
    {
        if (totalControlPoints <= 0 || controlPointIndex < ControlPointIndexMetadata.MinIndex)
        {
            return 1f;
        }

        var referenceCapTime = ControlPointCapTimeRules.GetReferenceCapTime(totalControlPoints, setupMode);
        if (referenceCapTime <= 0f)
        {
            return 1f;
        }

        var capTime = ControlPointCapTimeRules.GetCapTime(
            totalControlPoints,
            controlPointIndex,
            ControlPointCapTimeRules.DefaultBaseCapTime,
            setupMode);
        return capTime / referenceCapTime;
    }

    public static float ResolveCapTimeMultiplier(
        int totalControlPoints,
        int controlPointIndex,
        bool setupMode,
        float storedMultiplier,
        bool isCustom)
    {
        return isCustom
            ? storedMultiplier
            : ResolveAutoMultiplier(totalControlPoints, controlPointIndex, setupMode);
    }

    public static int ResolveCapTimeTicks(
        int totalControlPoints,
        int controlPointIndex,
        bool setupMode,
        float storedMultiplier,
        bool isCustom)
    {
        var referenceCapTime = ControlPointCapTimeRules.GetReferenceCapTime(totalControlPoints, setupMode);
        var multiplier = ResolveCapTimeMultiplier(
            totalControlPoints,
            controlPointIndex,
            setupMode,
            storedMultiplier,
            isCustom);
        return Math.Max(1, (int)MathF.Round(referenceCapTime * multiplier));
    }

    private static bool IsTruthy(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && (value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Trim().Equals("1", StringComparison.OrdinalIgnoreCase));
    }
}
