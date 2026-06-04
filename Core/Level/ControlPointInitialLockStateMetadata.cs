using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

public static class ControlPointInitialLockStateMetadata
{
    public const string PropertyKey = "initialLockState";
    public const string LockedValue = "locked";
    public const string UnlockedValue = "unlocked";

    public static bool ParseLocked(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null)
        {
            return false;
        }

        if (properties.TryGetValue(PropertyKey, out var rawValue)
            && TryParse(rawValue, out var state))
        {
            return state == ControlPointInitialLockState.Locked;
        }

        if (properties.TryGetValue("startLocked", out var legacyValue))
        {
            return IsTruthy(legacyValue);
        }

        return false;
    }

    public static string ToPropertyValue(bool locked)
    {
        return locked ? LockedValue : UnlockedValue;
    }

    public static bool TryParse(string? value, out ControlPointInitialLockState state)
    {
        if (value is null)
        {
            state = default;
            return false;
        }

        if (value.Equals(LockedValue, StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.OrdinalIgnoreCase))
        {
            state = ControlPointInitialLockState.Locked;
            return true;
        }

        if (value.Equals(UnlockedValue, StringComparison.OrdinalIgnoreCase)
            || value.Equals("false", StringComparison.OrdinalIgnoreCase)
            || value.Equals("0", StringComparison.OrdinalIgnoreCase))
        {
            state = ControlPointInitialLockState.Unlocked;
            return true;
        }

        state = default;
        return false;
    }

    public static string GetDisplayLabel(string? value)
    {
        return TryParse(value, out var state) && state == ControlPointInitialLockState.Locked
            ? "Locked"
            : "Unlocked";
    }

    public static string CyclePropertyValue(string current)
    {
        return TryParse(current, out var state) && state == ControlPointInitialLockState.Locked
            ? UnlockedValue
            : LockedValue;
    }

    private static bool IsTruthy(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }
}

public enum ControlPointInitialLockState
{
    Unlocked = 0,
    Locked = 1,
}
