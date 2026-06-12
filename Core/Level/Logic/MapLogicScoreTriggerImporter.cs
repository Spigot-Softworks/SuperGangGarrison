using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

public static class MapLogicScoreTriggerImporter
{
    public static MapLogicScoreTriggerSet BuildFromEntities(
        IReadOnlyList<MapImportedEntity> entities,
        MapLogicGraph graph)
    {
        if (entities.Count == 0)
        {
            return MapLogicScoreTriggerSet.Empty;
        }

        var triggers = new List<MapLogicScoreTrigger>();
        for (var index = 0; index < entities.Count; index += 1)
        {
            var entity = entities[index];
            if (!MapLogicScoreTriggerMetadata.IsScoreTriggerEntityType(entity.Type))
            {
                continue;
            }

            var properties = entity.Properties ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var inputNodeIndex = MapLogicGraphImporter.ResolveLogicSignalNodeIndex(
                graph,
                ReadProperty(properties, MapLogicMetadata.LogicInputPropertyKey));
            if (inputNodeIndex < 0)
            {
                continue;
            }

            triggers.Add(new MapLogicScoreTrigger(
                inputNodeIndex,
                MapLogicScoreTriggerMetadata.ParseScoreTeam(properties),
                MapLogicScoreTriggerMetadata.ParseChangeMode(properties),
                MapLogicScoreTriggerMetadata.ParseValue(properties),
                MapLogicMetadata.ParseNodePriority(properties)));
        }

        return triggers.Count == 0
            ? MapLogicScoreTriggerSet.Empty
            : new MapLogicScoreTriggerSet(triggers);
    }

    public static bool ContainsScoreTriggerEntities(IReadOnlyList<MapImportedEntity> entities)
    {
        for (var index = 0; index < entities.Count; index += 1)
        {
            if (MapLogicScoreTriggerMetadata.IsScoreTriggerEntityType(entities[index].Type))
            {
                return true;
            }
        }

        return false;
    }

    private static string ReadProperty(IReadOnlyDictionary<string, string> properties, string key)
    {
        return properties.TryGetValue(key, out var value) ? value : string.Empty;
    }
}
