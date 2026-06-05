using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Core;

public enum MapLogicSignalMode
{
    Latch,
    Impulse,
}

public enum MapLogicCpCaptureDetectMode
{
    AnyCapture,
    RedCapture,
    BlueCapture,
}

public enum MapLogicPlayerDetectMode
{
    PlayerEnter,
    PlayerExit,
}

public static class MapLogicSignalMetadata
{
    public const string SignalPropertyKey = "signal";
    public const string DetectPropertyKey = "detect";
    public const string PeriodPropertyKey = "period";

    public const string LatchPropertyValue = "latch";
    public const string ImpulsePropertyValue = "impulse";

    public const string AnyCapturePropertyValue = "anyCapture";
    public const string RedCapturePropertyValue = "redCapture";
    public const string BlueCapturePropertyValue = "blueCapture";

    public const string PlayerEnterPropertyValue = "playerEnter";
    public const string PlayerExitPropertyValue = "playerExit";

    public const float DefaultPeriodSeconds = 1f;

    public static bool SupportsSignalProperty(string? entityType)
    {
        return !string.IsNullOrWhiteSpace(entityType)
            && (entityType.Equals(MapLogicMetadata.CpTriggerEntityType, StringComparison.OrdinalIgnoreCase)
                || entityType.Equals(MapLogicMetadata.PlayerTriggerEntityType, StringComparison.OrdinalIgnoreCase)
                || entityType.Equals(MapLogicMetadata.TimerEntityType, StringComparison.OrdinalIgnoreCase)
                || entityType.Equals(MapLogicMetadata.OscillatorEntityType, StringComparison.OrdinalIgnoreCase)
                || DamageTriggerMetadata.IsDamageTriggerEntityType(entityType));
    }

    public static MapLogicSignalMode ParseSignalMode(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null
            || !properties.TryGetValue(SignalPropertyKey, out var value))
        {
            return MapLogicSignalMode.Latch;
        }

        return ParseSignalMode(value);
    }

    public static MapLogicSignalMode ParseSignalMode(string? value)
    {
        return value?.Trim().Equals(ImpulsePropertyValue, StringComparison.OrdinalIgnoreCase) == true
            ? MapLogicSignalMode.Impulse
            : MapLogicSignalMode.Latch;
    }

    public static string ToSignalPropertyValue(MapLogicSignalMode mode)
    {
        return mode == MapLogicSignalMode.Impulse ? ImpulsePropertyValue : LatchPropertyValue;
    }

    public static string CycleSignalPropertyValue(string? current)
    {
        return ParseSignalMode(current) == MapLogicSignalMode.Latch
            ? ImpulsePropertyValue
            : LatchPropertyValue;
    }

    public static string GetSignalDisplayLabel(string? value)
    {
        return ParseSignalMode(value) == MapLogicSignalMode.Impulse ? "Impulse" : "Latch";
    }

    public static MapLogicCpCaptureDetectMode ParseCpCaptureDetectMode(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null
            || !properties.TryGetValue(DetectPropertyKey, out var value))
        {
            return MapLogicCpCaptureDetectMode.AnyCapture;
        }

        return ParseCpCaptureDetectMode(value);
    }

    public static MapLogicCpCaptureDetectMode ParseCpCaptureDetectMode(string? value)
    {
        if (value?.Trim().Equals(RedCapturePropertyValue, StringComparison.OrdinalIgnoreCase) == true)
        {
            return MapLogicCpCaptureDetectMode.RedCapture;
        }

        if (value?.Trim().Equals(BlueCapturePropertyValue, StringComparison.OrdinalIgnoreCase) == true)
        {
            return MapLogicCpCaptureDetectMode.BlueCapture;
        }

        return MapLogicCpCaptureDetectMode.AnyCapture;
    }

    public static string ToCpCaptureDetectPropertyValue(MapLogicCpCaptureDetectMode mode)
    {
        return mode switch
        {
            MapLogicCpCaptureDetectMode.RedCapture => RedCapturePropertyValue,
            MapLogicCpCaptureDetectMode.BlueCapture => BlueCapturePropertyValue,
            _ => AnyCapturePropertyValue,
        };
    }

    public static string CycleCpCaptureDetectPropertyValue(string? current)
    {
        return ToCpCaptureDetectPropertyValue((MapLogicCpCaptureDetectMode)(((int)ParseCpCaptureDetectMode(current) + 1) % 3));
    }

    public static string GetCpCaptureDetectDisplayLabel(string? value)
    {
        return ParseCpCaptureDetectMode(value) switch
        {
            MapLogicCpCaptureDetectMode.RedCapture => "Red capture",
            MapLogicCpCaptureDetectMode.BlueCapture => "Blue capture",
            _ => "Any capture",
        };
    }

    public static MapLogicPlayerDetectMode ParsePlayerDetectMode(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null
            || !properties.TryGetValue(DetectPropertyKey, out var value))
        {
            return MapLogicPlayerDetectMode.PlayerEnter;
        }

        return ParsePlayerDetectMode(value);
    }

    public static MapLogicPlayerDetectMode ParsePlayerDetectMode(string? value)
    {
        return value?.Trim().Equals(PlayerExitPropertyValue, StringComparison.OrdinalIgnoreCase) == true
            ? MapLogicPlayerDetectMode.PlayerExit
            : MapLogicPlayerDetectMode.PlayerEnter;
    }

    public static string ToPlayerDetectPropertyValue(MapLogicPlayerDetectMode mode)
    {
        return mode == MapLogicPlayerDetectMode.PlayerExit
            ? PlayerExitPropertyValue
            : PlayerEnterPropertyValue;
    }

    public static string CyclePlayerDetectPropertyValue(string? current)
    {
        return ParsePlayerDetectMode(current) == MapLogicPlayerDetectMode.PlayerEnter
            ? PlayerExitPropertyValue
            : PlayerEnterPropertyValue;
    }

    public static string GetPlayerDetectDisplayLabel(string? value)
    {
        return ParsePlayerDetectMode(value) == MapLogicPlayerDetectMode.PlayerExit
            ? "Player exit"
            : "Player enter";
    }

    public static float ParsePeriodSeconds(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null
            || !properties.TryGetValue(PeriodPropertyKey, out var value))
        {
            return DefaultPeriodSeconds;
        }

        return ParsePeriodSeconds(value);
    }

    public static float ParsePeriodSeconds(string? value)
    {
        return float.TryParse(value?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Max(0f, parsed)
            : DefaultPeriodSeconds;
    }

    public static string ToPeriodPropertyValue(float seconds)
    {
        return MapLogicMetadata.ToCountdownSecondsPropertyValue(Math.Max(0f, seconds));
    }

    public static bool IsDamageTriggerModeAvailable(
        IReadOnlyDictionary<string, string>? properties,
        string modeKey)
    {
        if (ParseSignalMode(properties) == MapLogicSignalMode.Impulse)
        {
            return true;
        }

        return modeKey.Equals(DamageTriggerMetadata.TriggerBelowThresholdPropertyKey, StringComparison.OrdinalIgnoreCase)
            || modeKey.Equals(DamageTriggerMetadata.TriggerWhenDestroyedPropertyKey, StringComparison.OrdinalIgnoreCase);
    }

    public static void ApplySignalModeSelection(
        Dictionary<string, string> properties,
        string entityType,
        MapLogicSignalMode mode)
    {
        properties[SignalPropertyKey] = ToSignalPropertyValue(mode);
        if (mode == MapLogicSignalMode.Impulse)
        {
            if (entityType.Equals(MapLogicMetadata.CpTriggerEntityType, StringComparison.OrdinalIgnoreCase)
                && !properties.ContainsKey(DetectPropertyKey))
            {
                properties[DetectPropertyKey] = AnyCapturePropertyValue;
            }

            if (entityType.Equals(MapLogicMetadata.PlayerTriggerEntityType, StringComparison.OrdinalIgnoreCase)
                && !properties.ContainsKey(DetectPropertyKey))
            {
                properties[DetectPropertyKey] = PlayerEnterPropertyValue;
            }

            if (entityType.Equals(MapLogicMetadata.OscillatorEntityType, StringComparison.OrdinalIgnoreCase)
                && !properties.ContainsKey(PeriodPropertyKey))
            {
                properties[PeriodPropertyKey] = ToPeriodPropertyValue(DefaultPeriodSeconds);
            }

            if (DamageTriggerMetadata.IsDamageTriggerEntityType(entityType)
                && !DamageTriggerMetadata.TryGetActiveExclusiveModeKey(properties, out _))
            {
                DamageTriggerMetadata.ApplyExclusiveModeSelection(
                    properties,
                    DamageTriggerMetadata.TriggerBelowThresholdPropertyKey);
            }

            return;
        }

        if (DamageTriggerMetadata.IsDamageTriggerEntityType(entityType)
            && (DamageTriggerMetadata.ParseTriggerOnHeal(properties)
                || DamageTriggerMetadata.ParseTriggerOnAnyDamage(properties)))
        {
            DamageTriggerMetadata.ApplyExclusiveModeSelection(
                properties,
                DamageTriggerMetadata.TriggerBelowThresholdPropertyKey);
        }
    }
}
