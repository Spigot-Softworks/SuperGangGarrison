using System;
using System.Globalization;

namespace OpenGarrison.Core;

public static class ControlPointIndexMetadata
{
    public const string PropertyKey = "index";
    public const int MinIndex = 1;
    public const int MaxIndex = 5;
    public const string DefaultPropertyValue = "1";

    public static int ClampIndex(int index)
    {
        return Math.Clamp(index, MinIndex, MaxIndex);
    }

    public static bool TryParse(string? value, out int index)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            index = ClampIndex(parsed);
            return true;
        }

        index = MinIndex;
        return false;
    }

    public static string ToPropertyValue(int index)
    {
        return ClampIndex(index).ToString(CultureInfo.InvariantCulture);
    }

    public static string CyclePropertyValue(string current)
    {
        var index = TryParse(current, out var parsed) ? parsed : MinIndex;
        var next = index >= MaxIndex ? MinIndex : index + 1;
        return ToPropertyValue(next);
    }

    public static string GetDisplayLabel(string? propertyValue)
    {
        return TryParse(propertyValue, out var index)
            ? $"#{index}"
            : $"#{DefaultPropertyValue}";
    }
}
