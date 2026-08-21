using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

/// <summary>
/// Map-level movement model selection. The default value intentionally remains
/// the OG2 side-scrolling platform movement model for backwards compatibility.
/// </summary>
public static class MapMovementModeMetadata
{
    public const string MovementModePropertyKey = "movementMode";
    public const string PlatformerPropertyValue = "platformer";
    public const string TopDownPropertyValue = "topdown";

    public static bool IsEditableMapMetadataKey(string key) =>
        key.Equals(MovementModePropertyKey, StringComparison.OrdinalIgnoreCase);

    public static bool IsTopDown(IReadOnlyDictionary<string, string>? metadata)
    {
        return metadata is not null
            && metadata.TryGetValue(MovementModePropertyKey, out var value)
            && value.Equals(TopDownPropertyValue, StringComparison.OrdinalIgnoreCase);
    }

    public static string ToPropertyValue(bool topDown) =>
        topDown ? TopDownPropertyValue : PlatformerPropertyValue;
}
