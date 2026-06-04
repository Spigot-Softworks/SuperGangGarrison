using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

public static class MapTeleportRuntimePatch
{
    public static void ApplyExitLinks(
        IList<RoomObjectMarker> roomObjects,
        IReadOnlyList<MapImportedEntity> entities)
    {
        if (roomObjects.Count == 0 || entities.Count == 0)
        {
            return;
        }

        var propertiesByPosition = BuildTeleportPropertiesLookup(entities);
        for (var index = 0; index < roomObjects.Count; index += 1)
        {
            var marker = roomObjects[index];
            if (marker.Type != RoomObjectType.TeleportZone)
            {
                continue;
            }

            if (!TryFindTeleportProperties(marker, propertiesByPosition, out var properties))
            {
                continue;
            }

            roomObjects[index] = marker with
            {
                TeleportZone = TeleportMetadata.ParseZoneConfiguration(properties, roomObjects),
            };
        }
    }

    private static Dictionary<(string Type, int X, int Y), IReadOnlyDictionary<string, string>> BuildTeleportPropertiesLookup(
        IReadOnlyList<MapImportedEntity> entities)
    {
        var lookup = new Dictionary<(string Type, int X, int Y), IReadOnlyDictionary<string, string>>();
        for (var index = 0; index < entities.Count; index += 1)
        {
            var entity = entities[index];
            if (!entity.Type.Equals(TeleportMetadata.TeleportEntityType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var key = ((string Type, int X, int Y))(
                entity.Type,
                (int)MathF.Round(entity.X),
                (int)MathF.Round(entity.Y));
            lookup[key] = entity.Properties;
        }

        return lookup;
    }

    private static bool TryFindTeleportProperties(
        RoomObjectMarker marker,
        IReadOnlyDictionary<(string Type, int X, int Y), IReadOnlyDictionary<string, string>> lookup,
        out IReadOnlyDictionary<string, string> properties)
    {
        properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new (int X, int Y)[]
        {
            ((int)MathF.Round(marker.X), (int)MathF.Round(marker.Y)),
            ((int)MathF.Round(marker.CenterX), (int)MathF.Round(marker.CenterY)),
        };

        for (var index = 0; index < candidates.Length; index += 1)
        {
            var candidate = candidates[index];
            var key = (TeleportMetadata.TeleportEntityType, candidate.X, candidate.Y);
            if (lookup.TryGetValue(key, out var found))
            {
                properties = found;
                return true;
            }
        }

        return false;
    }
}
