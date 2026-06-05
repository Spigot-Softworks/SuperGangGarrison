using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

public static class MapLogicActivatorImporter
{
    public static MapLogicActivatorSet BuildFromEntities(
        IReadOnlyList<MapImportedEntity> entities,
        IReadOnlyList<RoomObjectMarker> roomObjects,
        MapLogicGraph graph)
    {
        if (entities.Count == 0 || roomObjects.Count == 0)
        {
            return MapLogicActivatorSet.Empty;
        }

        var activators = new List<MapLogicActivator>();
        for (var index = 0; index < entities.Count; index += 1)
        {
            var entity = entities[index];
            if (!entity.Type.Equals(MapLogicMetadata.ActivatorEntityType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var properties = entity.Properties;
            if (!MapLogicEntityReference.TryResolveRoomObjectIndex(
                    roomObjects,
                    ReadProperty(properties, MapLogicMetadata.ActivatorEntityPropertyKey),
                    out var targetRoomObjectIndex,
                    entities))
            {
                continue;
            }

            var inputNodeIndex = MapLogicGraphImporter.ResolveLogicSignalNodeIndex(
                graph,
                ReadProperty(properties, MapLogicMetadata.LogicInputPropertyKey));
            if (!MapLogicMetadata.TryParseActivatorBehavior(
                    ReadProperty(properties, MapLogicMetadata.ActivatorBehaviorPropertyKey),
                    out var behavior))
            {
                behavior = MapLogicActivatorBehavior.Disable;
            }

            activators.Add(new MapLogicActivator(
                inputNodeIndex,
                targetRoomObjectIndex,
                behavior,
                ReadBool(properties, MapLogicMetadata.ActivateOnStartPropertyKey),
                MapLogicMetadata.ParseNodePriority(properties)));
        }

        return activators.Count == 0
            ? MapLogicActivatorSet.Empty
            : new MapLogicActivatorSet(activators);
    }

    private static string ReadProperty(IReadOnlyDictionary<string, string> properties, string key)
    {
        return properties.TryGetValue(key, out var value) ? value : string.Empty;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, string> properties, string key)
    {
        return properties.TryGetValue(key, out var value)
            && (value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Trim().Equals("1", StringComparison.OrdinalIgnoreCase));
    }
}
