using System;
using System.Globalization;

namespace OpenGarrison.Core;

/// <summary>
/// Forward spawn selection priority (1-4). Higher numbers are preferred when multiple forward spawns are active.
/// Independent from the linked control point index used for use-condition evaluation.
/// </summary>
public static class ForwardSpawnPriorityMetadata
{
    public const string PropertyKey = "priority";
    public const int MinPriority = 1;
    public const int MaxPriority = 4;
    public const string DefaultPropertyValue = "1";

    public static int ClampPriority(int priority)
    {
        return Math.Clamp(priority, MinPriority, MaxPriority);
    }

    public static bool TryParse(string? value, out int priority)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            priority = ClampPriority(parsed);
            return true;
        }

        priority = MinPriority;
        return false;
    }

    public static string ToPropertyValue(int priority)
    {
        return ClampPriority(priority).ToString(CultureInfo.InvariantCulture);
    }

    public static string CyclePropertyValue(string current)
    {
        var priority = TryParse(current, out var parsed) ? parsed : MinPriority;
        var next = priority >= MaxPriority ? MinPriority : priority + 1;
        return ToPropertyValue(next);
    }

    public static string GetDisplayLabel(string? propertyValue)
    {
        return TryParse(propertyValue, out var priority)
            ? $"Priority {priority}"
            : $"Priority {DefaultPropertyValue}";
    }

    /// <summary>
    /// Maps priority to numbered <c>spawnS</c> sprite frame offset (1-4 per team).
    /// </summary>
    public static int ResolveBuilderSpriteFrameOffset(int priority)
    {
        return ClampPriority(priority);
    }
}
