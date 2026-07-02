using System.Collections.Generic;

namespace OpenGarrison.Core;

public static class GameplaySoundRuntimePatch
{
    public static GameplaySoundMarker[] ResolveTriggerSignals(
        IReadOnlyList<GameplaySoundMarker> sounds,
        MapLogicGraph graph)
    {
        if (sounds.Count == 0)
        {
            return [];
        }

        var resolved = new GameplaySoundMarker[sounds.Count];
        for (var index = 0; index < sounds.Count; index += 1)
        {
            var marker = sounds[index];
            resolved[index] = marker.WithTriggerNodeIndex(
                MapLogicGraphImporter.ResolveLogicSignalNodeIndex(graph, marker.TriggerRef));
        }

        return resolved;
    }
}
