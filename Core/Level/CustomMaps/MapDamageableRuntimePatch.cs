using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

public static class MapDamageableRuntimePatch
{
    public static void ApplyHealWhenLinks(
        IList<RoomObjectMarker> roomObjects,
        IReadOnlyList<MapImportedEntity> entities,
        MapLogicGraph graph)
    {
        if (roomObjects.Count == 0 || entities.Count == 0 || !graph.HasNodes)
        {
            return;
        }

        var propertiesByMapEntityId = BuildDamageablePropertiesLookup(entities);
        for (var index = 0; index < roomObjects.Count; index += 1)
        {
            var marker = roomObjects[index];
            if (marker.Type != RoomObjectType.DamageableZone)
            {
                continue;
            }

            if (!propertiesByMapEntityId.TryGetValue(marker.SourceName, out var properties))
            {
                continue;
            }

            var healWhenNodeIndex = MapLogicGraphImporter.ResolveLogicSignalNodeIndex(
                graph,
                ReadProperty(properties, DamageableMetadata.HealWhenPropertyKey));
            roomObjects[index] = marker with
            {
                DamageableZone = DamageableMetadata.ParseZoneConfiguration(properties, healWhenNodeIndex),
            };
        }
    }

    private static Dictionary<string, IReadOnlyDictionary<string, string>> BuildDamageablePropertiesLookup(
        IReadOnlyList<MapImportedEntity> entities)
    {
        var lookup = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < entities.Count; index += 1)
        {
            var entity = entities[index];
            if (!DamageableMetadata.IsDamageableEntityType(entity.Type))
            {
                continue;
            }

            var properties = entity.Properties ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var mapEntityId = MapLogicMetadata.EnsureMapEntityId(properties);
            lookup[mapEntityId] = properties;
        }

        return lookup;
    }

    private static string ReadProperty(IReadOnlyDictionary<string, string> properties, string key)
    {
        return properties.TryGetValue(key, out var value) ? value : string.Empty;
    }
}
