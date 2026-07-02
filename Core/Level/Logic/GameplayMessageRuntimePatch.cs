using System.Collections.Generic;

namespace OpenGarrison.Core;

public static class GameplayMessageRuntimePatch
{
    public static GameplayMessageMarker[] ResolveTriggerSignals(
        IReadOnlyList<GameplayMessageMarker> messages,
        MapLogicGraph graph) =>
        ResolveTriggerSignals(messages, graph, roomObjects: null, importedEntities: null);

    public static GameplayMessageMarker[] ResolveTriggerSignals(
        IReadOnlyList<GameplayMessageMarker> messages,
        MapLogicGraph graph,
        IList<RoomObjectMarker>? roomObjects,
        IReadOnlyList<MapImportedEntity>? importedEntities)
    {
        if (messages.Count == 0)
        {
            return [];
        }

        var resolved = new GameplayMessageMarker[messages.Count];
        for (var index = 0; index < messages.Count; index += 1)
        {
            var marker = messages[index];
            marker = marker.WithLogicNodeIndices(
                MapLogicGraphImporter.ResolveLogicSignalNodeIndex(graph, marker.TriggerRef),
                MapLogicGraphImporter.ResolveLogicSignalNodeIndex(graph, marker.OnEndTriggerRef));
            if (roomObjects is not null
                && marker.OnEndEffects.HasFlag(GameplayMessageOnEndEffects.MapTeleport)
                && TeleportMetadata.TryResolveExitPosition(
                    roomObjects,
                    importedEntities,
                    marker.OnEndTeleportExitRef,
                    out var exitX,
                    out var exitY))
            {
                marker = marker.WithOnEndTeleportPosition(exitX, exitY);
            }

            resolved[index] = marker;
        }

        return resolved;
    }
}
