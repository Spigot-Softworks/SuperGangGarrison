using System.Collections.Generic;

namespace OpenGarrison.Core;

public static class BotSpawnRuntimePatch
{
    public static BotSpawnMarker[] ResolveTriggerSignals(
        IReadOnlyList<BotSpawnMarker> botSpawns,
        MapLogicGraph graph)
    {
        if (botSpawns.Count == 0)
        {
            return [];
        }

        var resolved = new BotSpawnMarker[botSpawns.Count];
        for (var index = 0; index < botSpawns.Count; index += 1)
        {
            var marker = botSpawns[index];
            resolved[index] = marker.WithLogicNodeIndices(
                MapLogicGraphImporter.ResolveLogicSignalNodeIndex(graph, marker.TriggerRef),
                MapLogicGraphImporter.ResolveLogicSignalNodeIndex(graph, marker.DeathTriggerRef));
        }

        return resolved;
    }
}
