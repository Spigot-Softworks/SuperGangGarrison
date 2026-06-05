using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

/// <summary>
/// Maps exported entity coordinates to runtime room-object top-left positions.
/// Most entities use top-left anchors. Resizable logic zones in Garrison Builder store center coordinates.
/// </summary>
public static class CustomMapEntityPlacementAnchor
{
    public static bool UsesCenterOrigin(IReadOnlyDictionary<string, string>? metadata)
    {
        _ = metadata;
        return false;
    }

    public static bool UsesCenterPlacementAnchor(string? entityType)
    {
        return !string.IsNullOrWhiteSpace(entityType)
            && (entityType.Equals(AreaExtensionMetadata.AreaEntityType, StringComparison.OrdinalIgnoreCase)
                || entityType.Equals(PlayerTriggerMetadata.PlayerTriggerEntityType, StringComparison.OrdinalIgnoreCase)
                || entityType.Equals(DamageableMetadata.DamageableEntityType, StringComparison.OrdinalIgnoreCase));
    }

    public static (float X, float Y) ToTopLeft(float x, float y, float width, float height, bool useCenterOrigin)
    {
        return useCenterOrigin
            ? (x - (width / 2f), y - (height / 2f))
            : (x, y);
    }

    public static (float X, float Y) ToTopLeft(
        string? entityType,
        float x,
        float y,
        float width,
        float height,
        bool useCenterOrigin)
    {
        return ToTopLeft(x, y, width, height, useCenterOrigin || UsesCenterPlacementAnchor(entityType));
    }
}
