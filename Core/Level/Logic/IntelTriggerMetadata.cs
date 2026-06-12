using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

public enum IntelTriggerIntelFilter
{
    Any,
    Red,
    Blue,
}

public enum IntelTriggerLatchState
{
    AtBase,
    Carried,
    Dropped,
}

public enum IntelTriggerPhase
{
    AtBase,
    Carried,
    Dropped,
}

public static class IntelTriggerMetadata
{
    public const string IntelTriggerEntityType = "logicIntelTrigger";
    public const string IntelPropertyKey = "intel";
    public const string TriggerWhenPropertyKey = "triggerWhen";
    public const string OnPickupPropertyKey = "onPickup";
    public const string OnDropPropertyKey = "onDrop";
    public const string OnCapturePropertyKey = "onCapture";
    public const string OnResetPropertyKey = "onReset";

    public const string IntelAnyPropertyValue = "any";
    public const string IntelRedPropertyValue = "red";
    public const string IntelBluePropertyValue = "blue";

    public const string TriggerWhenAtBasePropertyValue = "atBase";
    public const string TriggerWhenCarriedPropertyValue = "carried";
    public const string TriggerWhenDroppedPropertyValue = "dropped";

    public static bool IsIntelTriggerEntityType(string? type)
    {
        return !string.IsNullOrWhiteSpace(type)
            && type.Equals(IntelTriggerEntityType, StringComparison.OrdinalIgnoreCase);
    }

    public static IntelTriggerPhase GetPhase(TeamIntelligenceState intel)
    {
        if (intel.IsAtBase)
        {
            return IntelTriggerPhase.AtBase;
        }

        if (intel.IsDropped)
        {
            return IntelTriggerPhase.Dropped;
        }

        return IntelTriggerPhase.Carried;
    }

    public static bool TryParseIntelFilter(string? value, out IntelTriggerIntelFilter filter)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals(IntelAnyPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            filter = IntelTriggerIntelFilter.Any;
            return true;
        }

        if (value.Equals(IntelRedPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            filter = IntelTriggerIntelFilter.Red;
            return true;
        }

        if (value.Equals(IntelBluePropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            filter = IntelTriggerIntelFilter.Blue;
            return true;
        }

        filter = default;
        return false;
    }

    public static IntelTriggerIntelFilter ParseIntelFilter(IReadOnlyDictionary<string, string>? properties)
    {
        return properties is not null
            && properties.TryGetValue(IntelPropertyKey, out var value)
            && TryParseIntelFilter(value, out var filter)
            ? filter
            : IntelTriggerIntelFilter.Any;
    }

    public static string ToIntelPropertyValue(IntelTriggerIntelFilter filter)
    {
        return filter switch
        {
            IntelTriggerIntelFilter.Red => IntelRedPropertyValue,
            IntelTriggerIntelFilter.Blue => IntelBluePropertyValue,
            _ => IntelAnyPropertyValue,
        };
    }

    public static string CycleIntelPropertyValue(string? current)
    {
        if (!TryParseIntelFilter(current, out var filter))
        {
            filter = IntelTriggerIntelFilter.Any;
        }

        var next = (IntelTriggerIntelFilter)(((int)filter + 1) % 3);
        return ToIntelPropertyValue(next);
    }

    public static string GetIntelDisplayLabel(string? value)
    {
        return TryParseIntelFilter(value, out var filter)
            ? filter switch
            {
                IntelTriggerIntelFilter.Red => "Red",
                IntelTriggerIntelFilter.Blue => "Blue",
                _ => "Any",
            }
            : "Any";
    }

    public static bool TryParseLatchState(string? value, out IntelTriggerLatchState state)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals(TriggerWhenAtBasePropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            state = IntelTriggerLatchState.AtBase;
            return true;
        }

        if (value.Equals(TriggerWhenCarriedPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            state = IntelTriggerLatchState.Carried;
            return true;
        }

        if (value.Equals(TriggerWhenDroppedPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            state = IntelTriggerLatchState.Dropped;
            return true;
        }

        state = default;
        return false;
    }

    public static IntelTriggerLatchState ParseLatchState(IReadOnlyDictionary<string, string>? properties)
    {
        return properties is not null
            && properties.TryGetValue(TriggerWhenPropertyKey, out var value)
            && TryParseLatchState(value, out var state)
            ? state
            : IntelTriggerLatchState.AtBase;
    }

    public static string ToLatchStatePropertyValue(IntelTriggerLatchState state)
    {
        return state switch
        {
            IntelTriggerLatchState.Carried => TriggerWhenCarriedPropertyValue,
            IntelTriggerLatchState.Dropped => TriggerWhenDroppedPropertyValue,
            _ => TriggerWhenAtBasePropertyValue,
        };
    }

    public static string CycleLatchStatePropertyValue(string? current)
    {
        if (!TryParseLatchState(current, out var state))
        {
            state = IntelTriggerLatchState.AtBase;
        }

        var next = (IntelTriggerLatchState)(((int)state + 1) % 3);
        return ToLatchStatePropertyValue(next);
    }

    public static string GetLatchStateDisplayLabel(string? value)
    {
        return TryParseLatchState(value, out var state)
            ? state switch
            {
                IntelTriggerLatchState.Carried => "Carried",
                IntelTriggerLatchState.Dropped => "Dropped",
                _ => "At base",
            }
            : "At base";
    }

    public static bool ParseOnPickup(IReadOnlyDictionary<string, string>? properties)
    {
        return ParseBoolProperty(properties, OnPickupPropertyKey);
    }

    public static bool ParseOnDrop(IReadOnlyDictionary<string, string>? properties)
    {
        return ParseBoolProperty(properties, OnDropPropertyKey);
    }

    public static bool ParseOnCapture(IReadOnlyDictionary<string, string>? properties)
    {
        return ParseBoolProperty(properties, OnCapturePropertyKey);
    }

    public static bool ParseOnReset(IReadOnlyDictionary<string, string>? properties)
    {
        return ParseBoolProperty(properties, OnResetPropertyKey);
    }

    public static bool MatchesLatchState(TeamIntelligenceState intel, IntelTriggerLatchState latchState)
    {
        return latchState switch
        {
            IntelTriggerLatchState.AtBase => intel.IsAtBase,
            IntelTriggerLatchState.Carried => intel.IsCarried,
            IntelTriggerLatchState.Dropped => intel.IsDropped,
            _ => false,
        };
    }

    public static bool DetectImpulseEdge(
        IntelTriggerPhase previous,
        IntelTriggerPhase current,
        bool onPickup,
        bool onDrop,
        bool onCapture,
        bool onReset)
    {
        if (onPickup && current == IntelTriggerPhase.Carried && previous != IntelTriggerPhase.Carried)
        {
            return true;
        }

        if (onDrop && previous == IntelTriggerPhase.Carried && current == IntelTriggerPhase.Dropped)
        {
            return true;
        }

        if (onCapture && previous == IntelTriggerPhase.Carried && current == IntelTriggerPhase.AtBase)
        {
            return true;
        }

        if (onReset && previous == IntelTriggerPhase.Dropped && current == IntelTriggerPhase.AtBase)
        {
            return true;
        }

        return false;
    }

    public static void ApplyImpulseDefaults(Dictionary<string, string> properties)
    {
        properties.TryAdd(OnPickupPropertyKey, "true");
        properties.TryAdd(OnDropPropertyKey, "false");
        properties.TryAdd(OnCapturePropertyKey, "false");
        properties.TryAdd(OnResetPropertyKey, "false");
    }

    public static void ApplyLatchDefaults(Dictionary<string, string> properties)
    {
        properties.TryAdd(TriggerWhenPropertyKey, TriggerWhenAtBasePropertyValue);
    }

    private static bool ParseBoolProperty(IReadOnlyDictionary<string, string>? properties, string key)
    {
        return properties is not null
            && properties.TryGetValue(key, out var value)
            && DamageTriggerMetadata.ParseBoolProperty(value);
    }
}
