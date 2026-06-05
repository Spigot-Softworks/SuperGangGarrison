using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Core;

public static class MapLogicEntityReference
{
    public const float PositionTolerance = 0.75f;

    public const float PositionToleranceSquared = PositionTolerance * PositionTolerance;

    public static string FormatEntityRef(string entityType, float x, float y)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"entity:{entityType}@{x},{y}");
    }

    public static string FormatEntityRef(string entityType, string mapEntityId)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"entity:{entityType.Trim()}@id:{mapEntityId.Trim()}");
    }

    public static string FormatEntityRef(CustomMapBuilderEntity entity)
    {
        return FormatEntityRef(entity.Type, MapLogicMetadata.EnsureMapEntityId(entity.Properties));
    }

    public static bool TryParseEntityRef(string? value, out string entityType, out float x, out float y)
    {
        return TryParseEntityRef(value, out entityType, out x, out y, out _);
    }

    public static bool TryParseEntityRef(
        string? value,
        out string entityType,
        out float x,
        out float y,
        out string? mapEntityId)
    {
        entityType = string.Empty;
        x = 0f;
        y = 0f;
        mapEntityId = null;
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
        if (coordinates.StartsWith("id:", StringComparison.OrdinalIgnoreCase))
        {
            mapEntityId = coordinates[3..].Trim();
            return entityType.Length > 0 && mapEntityId.Length > 0;
        }

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
        return TryResolveRoomObjectIndex(roomObjects, entityRef, out roomObjectIndex, importedEntities: null);
    }

    public static bool TryResolveRoomObjectIndex(
        IReadOnlyList<RoomObjectMarker> roomObjects,
        string? entityRef,
        out int roomObjectIndex,
        IReadOnlyList<MapImportedEntity>? importedEntities)
    {
        roomObjectIndex = -1;
        if (!TryParseEntityRef(entityRef, out var entityType, out var x, out var y, out var mapEntityId))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(mapEntityId)
            && importedEntities is not null
            && TryResolveRoomObjectIndexByMapEntityId(
                roomObjects,
                importedEntities,
                entityType,
                mapEntityId,
                out roomObjectIndex))
        {
            return true;
        }

        return TryResolveRoomObjectIndexByPosition(roomObjects, entityType, x, y, out roomObjectIndex);
    }

    public static bool TryFindBuilderEntityIndex(
        IReadOnlyList<CustomMapBuilderEntity> entities,
        string? entityRef,
        out int entityIndex)
    {
        entityIndex = -1;
        if (!TryParseEntityRef(entityRef, out var entityType, out var x, out var y, out var mapEntityId))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(mapEntityId))
        {
            for (var index = 0; index < entities.Count; index += 1)
            {
                var entity = entities[index];
                if (!entity.Type.Equals(entityType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var candidateId = MapLogicMetadata.EnsureMapEntityId(entity.Properties);
                if (candidateId.Equals(mapEntityId, StringComparison.OrdinalIgnoreCase))
                {
                    entityIndex = index;
                    return true;
                }
            }

            return false;
        }

        return TryFindBuilderEntityIndexByPosition(entities, entityType, x, y, out entityIndex);
    }

    public static bool EntityTypeMatchesRoomObject(string entityType, RoomObjectMarker marker)
    {
        if (marker.SourceName.Equals(entityType, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (entityType.Equals(PlayerTriggerMetadata.PlayerTriggerEntityType, StringComparison.OrdinalIgnoreCase)
            && marker.Type == RoomObjectType.PlayerTriggerZone)
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

        if (AreaExtensionMetadata.IsAreaEntityType(entityType)
            && marker.Type == RoomObjectType.AreaExtension)
        {
            return true;
        }

        if (DamageableMetadata.IsDamageableEntityType(entityType)
            && marker.Type == RoomObjectType.DamageableZone)
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

    private static bool TryResolveRoomObjectIndexByMapEntityId(
        IReadOnlyList<RoomObjectMarker> roomObjects,
        IReadOnlyList<MapImportedEntity> importedEntities,
        string entityType,
        string mapEntityId,
        out int roomObjectIndex)
    {
        roomObjectIndex = -1;
        for (var index = 0; index < importedEntities.Count; index += 1)
        {
            var entity = importedEntities[index];
            if (!entity.Type.Equals(entityType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var candidateId = MapLogicMetadata.EnsureMapEntityId(entity.Properties);
            if (!candidateId.Equals(mapEntityId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return TryResolveRoomObjectIndexByPosition(
                roomObjects,
                entityType,
                entity.X,
                entity.Y,
                out roomObjectIndex);
        }

        return false;
    }

    private static bool TryResolveRoomObjectIndexByPosition(
        IReadOnlyList<RoomObjectMarker> roomObjects,
        string entityType,
        float x,
        float y,
        out int roomObjectIndex)
    {
        roomObjectIndex = -1;
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
            if (distanceSquared > PositionToleranceSquared
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

    private static bool TryFindBuilderEntityIndexByPosition(
        IReadOnlyList<CustomMapBuilderEntity> entities,
        string entityType,
        float x,
        float y,
        out int entityIndex)
    {
        entityIndex = -1;
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
            if (distanceSquared > PositionToleranceSquared
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

    private static readonly Dictionary<string, string> EmptyProperties =
        new(StringComparer.OrdinalIgnoreCase);
}
