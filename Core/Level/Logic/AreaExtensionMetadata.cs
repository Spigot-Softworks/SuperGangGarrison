using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

public enum AreaExtensionKind
{
    None,
    PlayerTrigger,
    Teleport,
    ForegroundSprite,
}

public readonly record struct AreaExtensionConfiguration(
    int ParentRoomObjectIndex,
    AreaExtensionKind Kind);

public static class AreaExtensionMetadata
{
    public const string AreaEntityType = "logicArea";
    public const string ExtendsPropertyKey = "extends";
    public const float DefaultZoneWidth = 42f;
    public const float DefaultZoneHeight = 42f;
    public const float MinZoneExtent = 12f;

    public static bool IsAreaEntityType(string? type)
    {
        return !string.IsNullOrWhiteSpace(type)
            && type.Equals(AreaEntityType, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsExtendableAreaEntityType(string? type)
    {
        return type.Equals(PlayerTriggerMetadata.PlayerTriggerEntityType, StringComparison.OrdinalIgnoreCase)
            || type.Equals(TeleportMetadata.TeleportEntityType, StringComparison.OrdinalIgnoreCase)
            || ForegroundSpriteMetadata.IsForegroundSpriteEntityType(type)
            || IsAreaEntityType(type);
    }

    public static (float Width, float Height) ResolveZoneDimensions(float xScale, float yScale)
    {
        var width = DefaultZoneWidth * MathF.Abs(xScale <= 0f ? 1f : xScale);
        var height = DefaultZoneHeight * MathF.Abs(yScale <= 0f ? 1f : yScale);
        return (MathF.Max(MinZoneExtent, width), MathF.Max(MinZoneExtent, height));
    }

    public static bool TryResolveParentRoomObjectIndex(
        IReadOnlyList<RoomObjectMarker> roomObjects,
        string? extendsRef,
        out int parentRoomObjectIndex,
        out AreaExtensionKind kind)
    {
        parentRoomObjectIndex = -1;
        kind = AreaExtensionKind.None;
        if (!MapLogicEntityReference.TryParseEntityRef(extendsRef, out var entityType, out var x, out var y))
        {
            return false;
        }

        if (entityType.Equals(PlayerTriggerMetadata.PlayerTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            kind = AreaExtensionKind.PlayerTrigger;
            return TryFindRoomObjectAtPosition(
                roomObjects,
                RoomObjectType.PlayerTriggerZone,
                entityType,
                x,
                y,
                out parentRoomObjectIndex);
        }

        if (entityType.Equals(TeleportMetadata.TeleportEntityType, StringComparison.OrdinalIgnoreCase))
        {
            kind = AreaExtensionKind.Teleport;
            return TryFindRoomObjectAtPosition(
                roomObjects,
                RoomObjectType.TeleportZone,
                entityType,
                x,
                y,
                out parentRoomObjectIndex);
        }

        if (ForegroundSpriteMetadata.IsForegroundSpriteEntityType(entityType))
        {
            kind = AreaExtensionKind.ForegroundSprite;
            return TryFindRoomObjectAtPosition(
                roomObjects,
                RoomObjectType.ForegroundSprite,
                entityType,
                x,
                y,
                out parentRoomObjectIndex);
        }

        if (IsAreaEntityType(entityType))
        {
            if (!TryFindRoomObjectAtPosition(
                    roomObjects,
                    RoomObjectType.AreaExtension,
                    entityType,
                    x,
                    y,
                    out parentRoomObjectIndex))
            {
                return false;
            }

            kind = roomObjects[parentRoomObjectIndex].AreaExtension.Kind;
            return kind != AreaExtensionKind.None;
        }

        return false;
    }

    public static int[] CollectPlayerTriggerZoneIndices(
        IReadOnlyList<RoomObjectMarker> roomObjects,
        string logicKey,
        int primaryRoomObjectIndex)
    {
        var indices = new List<int>(4);
        if (primaryRoomObjectIndex >= 0)
        {
            indices.Add(primaryRoomObjectIndex);
        }

        for (var index = 0; index < roomObjects.Count; index += 1)
        {
            if (index == primaryRoomObjectIndex)
            {
                continue;
            }

            var marker = roomObjects[index];
            if (marker.Type != RoomObjectType.AreaExtension
                || marker.AreaExtension.Kind != AreaExtensionKind.PlayerTrigger)
            {
                continue;
            }

            if (!BelongsToPlayerTriggerRoot(roomObjects, marker.AreaExtension.ParentRoomObjectIndex, logicKey))
            {
                continue;
            }

            indices.Add(index);
        }

        return indices.ToArray();
    }

    public static bool BelongsToPlayerTriggerRoot(
        IReadOnlyList<RoomObjectMarker> roomObjects,
        int roomObjectIndex,
        string logicKey)
    {
        var visited = new HashSet<int>();
        while (roomObjectIndex >= 0 && roomObjectIndex < roomObjects.Count)
        {
            if (!visited.Add(roomObjectIndex))
            {
                return false;
            }

            var marker = roomObjects[roomObjectIndex];
            if (marker.Type == RoomObjectType.PlayerTriggerZone
                && marker.SourceName.Equals(logicKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (marker.Type != RoomObjectType.AreaExtension)
            {
                return false;
            }

            roomObjectIndex = marker.AreaExtension.ParentRoomObjectIndex;
        }

        return false;
    }

    public static void EnableAllExtensionMasks(
        IReadOnlyList<RoomObjectMarker> roomObjects,
        int primaryRoomObjectIndex,
        bool[] roomObjectActiveMask)
    {
        for (var index = 0; index < roomObjects.Count; index += 1)
        {
            var marker = roomObjects[index];
            if (marker.Type != RoomObjectType.AreaExtension)
            {
                continue;
            }

            if (marker.AreaExtension.ParentRoomObjectIndex == primaryRoomObjectIndex
                && index < roomObjectActiveMask.Length)
            {
                roomObjectActiveMask[index] = true;
            }
        }
    }

    public static bool IsPrimaryExtendableZone(IReadOnlyList<RoomObjectMarker> roomObjects, int roomObjectIndex)
    {
        if (roomObjectIndex < 0 || roomObjectIndex >= roomObjects.Count)
        {
            return false;
        }

        return roomObjects[roomObjectIndex].Type
            is RoomObjectType.PlayerTriggerZone
            or RoomObjectType.TeleportZone
            or RoomObjectType.ForegroundSprite;
    }

    public static bool BelongsToForegroundSpriteRoot(
        IReadOnlyList<RoomObjectMarker> roomObjects,
        int roomObjectIndex,
        int primaryForegroundIndex)
    {
        var visited = new HashSet<int>();
        while (roomObjectIndex >= 0 && roomObjectIndex < roomObjects.Count)
        {
            if (!visited.Add(roomObjectIndex))
            {
                return false;
            }

            if (roomObjectIndex == primaryForegroundIndex)
            {
                return true;
            }

            var marker = roomObjects[roomObjectIndex];
            if (marker.Type == RoomObjectType.ForegroundSprite)
            {
                return false;
            }

            if (marker.Type != RoomObjectType.AreaExtension
                || marker.AreaExtension.Kind != AreaExtensionKind.ForegroundSprite)
            {
                return false;
            }

            roomObjectIndex = marker.AreaExtension.ParentRoomObjectIndex;
        }

        return false;
    }

    public static int[] CollectForegroundSpriteExtensionIndices(
        IReadOnlyList<RoomObjectMarker> roomObjects,
        int primaryForegroundIndex)
    {
        var indices = new List<int>(4);
        for (var index = 0; index < roomObjects.Count; index += 1)
        {
            if (index == primaryForegroundIndex)
            {
                continue;
            }

            var marker = roomObjects[index];
            if (marker.Type != RoomObjectType.AreaExtension
                || marker.AreaExtension.Kind != AreaExtensionKind.ForegroundSprite)
            {
                continue;
            }

            if (!BelongsToForegroundSpriteRoot(
                    roomObjects,
                    marker.AreaExtension.ParentRoomObjectIndex,
                    primaryForegroundIndex))
            {
                continue;
            }

            indices.Add(index);
        }

        return indices.ToArray();
    }

    public static bool TryGetEffectiveTeleportZone(
        IReadOnlyList<RoomObjectMarker> roomObjects,
        int roomObjectIndex,
        out RoomObjectMarker teleportZone)
    {
        teleportZone = default;
        if (roomObjectIndex < 0 || roomObjectIndex >= roomObjects.Count)
        {
            return false;
        }

        var marker = roomObjects[roomObjectIndex];
        if (marker.Type == RoomObjectType.TeleportZone)
        {
            teleportZone = marker;
            return marker.TeleportZone.HasExit;
        }

        if (marker.Type != RoomObjectType.AreaExtension
            || marker.AreaExtension.Kind != AreaExtensionKind.Teleport)
        {
            return false;
        }

        var parentIndex = marker.AreaExtension.ParentRoomObjectIndex;
        if (parentIndex < 0 || parentIndex >= roomObjects.Count)
        {
            return false;
        }

        var parent = roomObjects[parentIndex];
        if (parent.Type == RoomObjectType.TeleportZone)
        {
            teleportZone = parent;
            return parent.TeleportZone.HasExit;
        }

        if (parent.Type == RoomObjectType.AreaExtension)
        {
            return TryGetEffectiveTeleportZone(roomObjects, parentIndex, out teleportZone);
        }

        return false;
    }

    public static bool IsPointInsideMarker(float x, float y, in RoomObjectMarker marker)
    {
        return x >= marker.Left
            && x <= marker.Right
            && y >= marker.Top
            && y <= marker.Bottom;
    }

    private static bool TryFindRoomObjectAtPosition(
        IReadOnlyList<RoomObjectMarker> roomObjects,
        RoomObjectType expectedType,
        string entityType,
        float x,
        float y,
        out int roomObjectIndex)
    {
        roomObjectIndex = -1;
        var bestDistanceSquared = float.MaxValue;
        for (var index = 0; index < roomObjects.Count; index += 1)
        {
            var marker = roomObjects[index];
            if (marker.Type != expectedType)
            {
                continue;
            }

            if (!MapLogicEntityReference.EntityTypeMatchesRoomObject(entityType, marker))
            {
                continue;
            }

            var compareX = marker.CenterX;
            var compareY = marker.CenterY;
            var deltaX = compareX - x;
            var deltaY = compareY - y;
            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (distanceSquared >= MapLogicEntityReference.PositionToleranceSquared
                || distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            roomObjectIndex = index;
        }

        return roomObjectIndex >= 0;
    }
}
