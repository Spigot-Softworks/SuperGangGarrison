using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Core;

public static class DamageTriggerMetadata
{
    public const string DamageTriggerEntityType = "logicDamageTrigger";

    public const string DamageableEntityPropertyKey = "damageableEntity";
    public const string TriggerBelowThresholdPropertyKey = "triggerBelowThreshold";
    public const string TriggerBelowPercentPropertyKey = "triggerBelowPercent";
    public const string TriggerOnAnyDamagePropertyKey = "triggerOnAnyDamage";
    public const string TriggerOnHealPropertyKey = "triggerOnHeal";
    public const string TriggerWhenDestroyedPropertyKey = "triggerWhenDestroyed";

    public const int MinTriggerBelowPercent = 0;
    public const int MaxTriggerBelowPercent = 100;
    public const int DefaultTriggerBelowPercent = 50;

    public const float DefaultAnyDamageTrueTimeSeconds = 0.25f;

    public static readonly string[] ExclusiveModePropertyKeys =
    [
        TriggerBelowThresholdPropertyKey,
        TriggerOnAnyDamagePropertyKey,
        TriggerOnHealPropertyKey,
        TriggerWhenDestroyedPropertyKey,
    ];

    public static bool IsDamageTriggerEntityType(string? type)
    {
        return !string.IsNullOrWhiteSpace(type)
            && type.Equals(DamageTriggerEntityType, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsExclusiveModePropertyKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        foreach (var modeKey in ExclusiveModePropertyKeys)
        {
            if (key.Equals(modeKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static int ParseTriggerBelowPercent(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null
            || !properties.TryGetValue(TriggerBelowPercentPropertyKey, out var value))
        {
            return DefaultTriggerBelowPercent;
        }

        return ParseTriggerBelowPercent(value);
    }

    public static int ParseTriggerBelowPercent(string? value)
    {
        if (!int.TryParse(value?.Trim().TrimEnd('%'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return DefaultTriggerBelowPercent;
        }

        return Math.Clamp(parsed, MinTriggerBelowPercent, MaxTriggerBelowPercent);
    }

    public static string ToTriggerBelowPercentPropertyValue(int percent)
    {
        return $"{Math.Clamp(percent, MinTriggerBelowPercent, MaxTriggerBelowPercent)}%";
    }

    public static int AdjustTriggerBelowPercent(string? current, int delta)
    {
        return Math.Clamp(ParseTriggerBelowPercent(current) + delta, MinTriggerBelowPercent, MaxTriggerBelowPercent);
    }

    public static bool ParseTriggerBelowThreshold(IReadOnlyDictionary<string, string>? properties)
    {
        return properties is not null
            && properties.TryGetValue(TriggerBelowThresholdPropertyKey, out var value)
            && ParseBoolProperty(value);
    }

    public static bool ParseTriggerOnAnyDamage(IReadOnlyDictionary<string, string>? properties)
    {
        return properties is not null
            && properties.TryGetValue(TriggerOnAnyDamagePropertyKey, out var value)
            && ParseBoolProperty(value);
    }

    public static bool ParseTriggerOnHeal(IReadOnlyDictionary<string, string>? properties)
    {
        return properties is not null
            && properties.TryGetValue(TriggerOnHealPropertyKey, out var value)
            && ParseBoolProperty(value);
    }

    public static bool ParseTriggerWhenDestroyed(IReadOnlyDictionary<string, string>? properties)
    {
        return properties is not null
            && properties.TryGetValue(TriggerWhenDestroyedPropertyKey, out var value)
            && ParseBoolProperty(value);
    }

    public static float ParseAnyDamageTrueTimeSeconds(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null)
        {
            return DefaultAnyDamageTrueTimeSeconds;
        }

        return properties.TryGetValue(MapLogicMetadata.TrueTimePropertyKey, out var raw)
            ? Math.Max(0f, MapLogicMetadata.ParseOscillatorIntervalSeconds(raw))
            : DefaultAnyDamageTrueTimeSeconds;
    }

    public static string ToAnyDamageTrueTimePropertyValue(float seconds)
    {
        return MapLogicMetadata.ToCountdownSecondsPropertyValue(Math.Max(0f, seconds));
    }

    public static bool TryGetActiveExclusiveModeKey(
        IReadOnlyDictionary<string, string>? properties,
        out string activeKey)
    {
        activeKey = string.Empty;
        if (properties is null)
        {
            return false;
        }

        if (ParseTriggerOnHeal(properties))
        {
            activeKey = TriggerOnHealPropertyKey;
            return true;
        }

        if (ParseTriggerOnAnyDamage(properties))
        {
            activeKey = TriggerOnAnyDamagePropertyKey;
            return true;
        }

        if (ParseTriggerWhenDestroyed(properties))
        {
            activeKey = TriggerWhenDestroyedPropertyKey;
            return true;
        }

        if (ParseTriggerBelowThreshold(properties))
        {
            activeKey = TriggerBelowThresholdPropertyKey;
            return true;
        }

        return false;
    }

    public static bool IsInactiveExclusiveModeRow(
        IReadOnlyDictionary<string, string>? properties,
        string key)
    {
        if (!IsExclusiveModePropertyKey(key)
            || !TryGetActiveExclusiveModeKey(properties, out var activeKey))
        {
            return false;
        }

        return !key.Equals(activeKey, StringComparison.OrdinalIgnoreCase);
    }

    public static void ApplyExclusiveModeSelection(
        IDictionary<string, string> properties,
        string selectedKey)
    {
        foreach (var modeKey in ExclusiveModePropertyKeys)
        {
            properties[modeKey] = modeKey.Equals(selectedKey, StringComparison.OrdinalIgnoreCase)
                ? "true"
                : "false";
        }

        if (selectedKey.Equals(TriggerOnAnyDamagePropertyKey, StringComparison.OrdinalIgnoreCase)
            && !properties.ContainsKey(MapLogicMetadata.TrueTimePropertyKey))
        {
            properties[MapLogicMetadata.TrueTimePropertyKey] =
                ToAnyDamageTrueTimePropertyValue(DefaultAnyDamageTrueTimeSeconds);
        }
    }

    public static string CycleBooleanPropertyValue(string? current)
    {
        return ParseBoolProperty(current) ? "false" : "true";
    }

    public static string GetBooleanDisplayLabel(string? value)
    {
        return ParseBoolProperty(value) ? "on" : "off";
    }

    public static string GetTriggerBelowDisplayLabel(string? value)
    {
        return ToTriggerBelowPercentPropertyValue(ParseTriggerBelowPercent(value));
    }

    public static string GetExclusiveModeDisplayLabel(string key)
    {
        if (key.Equals(TriggerBelowThresholdPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return "Trigger below threshold";
        }

        if (key.Equals(TriggerOnAnyDamagePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return "Trigger on any damage";
        }

        if (key.Equals(TriggerOnHealPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return "Trigger on heal";
        }

        if (key.Equals(TriggerWhenDestroyedPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return "Trigger when destroyed";
        }

        return key;
    }

    public static bool ParseBoolProperty(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && (value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Trim().Equals("1", StringComparison.OrdinalIgnoreCase));
    }
}
