using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Core;

public static class MapLogicEntityReference
{
    private const float PositionTolerance = 0.75f;

    public static string FormatEntityRef(string entityType, float x, float y)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"entity:{entityType}@{x},{y}");
    }

    public static string FormatEntityRef(CustomMapBuilderEntity entity)
    {
        return FormatEntityRef(entity.Type, entity.X, entity.Y);
    }

    public static bool TryParseEntityRef(string? value, out string entityType, out float x, out float y)
    {
        entityType = string.Empty;
        x = 0f;
        y = 0f;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (!trimmed.StartsWith("entity:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = trimmed[7..];
        var separator = payload.LastIndexOf('@');
        if (separator <= 0 || separator >= payload.Length - 1)
        {
            return false;
        }

        entityType = payload[..separator].Trim();
        var coordinates = payload[(separator + 1)..];
        var comma = coordinates.IndexOf(',');
        if (comma <= 0 || comma >= coordinates.Length - 1)
        {
            return false;
        }

        if (!float.TryParse(coordinates[..comma], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
            || !float.TryParse(coordinates[(comma + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out y))
        {
            return false;
        }

        return entityType.Length > 0;
    }

    public static bool TryResolveRoomObjectIndex(
        IReadOnlyList<RoomObjectMarker> roomObjects,
        string? entityRef,
        out int roomObjectIndex)
    {
        roomObjectIndex = -1;
        if (!TryParseEntityRef(entityRef, out var entityType, out var x, out var y))
        {
            return false;
        }

        var bestIndex = -1;
        var bestDistanceSquared = float.MaxValue;
        for (var index = 0; index < roomObjects.Count; index += 1)
        {
            var marker = roomObjects[index];
            if (!EntityTypeMatchesRoomObject(entityType, marker))
            {
                continue;
            }

            var compareX = marker.Type == RoomObjectType.CustomMapSprite ? marker.CenterX : marker.X;
            var compareY = marker.Type == RoomObjectType.CustomMapSprite ? marker.CenterY : marker.Y;
            var deltaX = compareX - x;
            var deltaY = compareY - y;
            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (distanceSquared > PositionTolerance * PositionTolerance
                || distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            bestIndex = index;
        }

        if (bestIndex < 0)
        {
            return false;
        }

        roomObjectIndex = bestIndex;
        return true;
    }

    public static bool TryFindBuilderEntityIndex(
        IReadOnlyList<CustomMapBuilderEntity> entities,
        string? entityRef,
        out int entityIndex)
    {
        entityIndex = -1;
        if (!TryParseEntityRef(entityRef, out var entityType, out var x, out var y))
        {
            return false;
        }

        var bestIndex = -1;
        var bestDistanceSquared = float.MaxValue;
        for (var index = 0; index < entities.Count; index += 1)
        {
            var entity = entities[index];
            if (!entity.Type.Equals(entityType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var deltaX = entity.X - x;
            var deltaY = entity.Y - y;
            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (distanceSquared > PositionTolerance * PositionTolerance
                || distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            bestIndex = index;
        }

        if (bestIndex < 0)
        {
            return false;
        }

        entityIndex = bestIndex;
        return true;
    }

    private static bool EntityTypeMatchesRoomObject(string entityType, RoomObjectMarker marker)
    {
        if (marker.SourceName.Equals(entityType, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (entityType.Equals(TeleportMetadata.TeleportEntityType, StringComparison.OrdinalIgnoreCase)
            && marker.Type == RoomObjectType.TeleportZone)
        {
            return true;
        }

        if (entityType.Equals(TeleportMetadata.TeleportExitEntityType, StringComparison.OrdinalIgnoreCase)
            && marker.Type == RoomObjectType.TeleportExit)
        {
            return true;
        }

        if (CustomMapCustomSpriteMetadata.IsCustomSpriteEntityType(entityType)
            && marker.Type == RoomObjectType.CustomMapSprite)
        {
            return true;
        }

        if (CustomMapRoomObjectFactory.TryCreate(
                entityType,
                marker.CenterX,
                marker.CenterY,
                1f,
                1f,
                properties: EmptyProperties,
                out var expected,
                useCenterOrigin: true)
            && expected.Type == marker.Type)
        {
            return marker.SourceName.Equals(expected.SourceName, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(expected.SourceName);
        }

        return false;
    }

    private static readonly Dictionary<string, string> EmptyProperties =
        new(StringComparer.OrdinalIgnoreCase);
}
