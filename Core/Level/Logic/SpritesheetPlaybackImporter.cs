using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

public static class SpritesheetPlaybackImporter
{
    public static SpritesheetPlaybackSet BuildFromRoomObjects(
        IReadOnlyList<RoomObjectMarker> roomObjects,
        MapLogicGraph graph)
    {
        if (roomObjects.Count == 0)
        {
            return SpritesheetPlaybackSet.Empty;
        }

        var entries = new List<SpritesheetPlaybackEntry>();
        for (var index = 0; index < roomObjects.Count; index += 1)
        {
            var marker = roomObjects[index];
            if (marker.Type != RoomObjectType.Spritesheet)
            {
                continue;
            }

            var configuration = marker.Spritesheet;
            entries.Add(new SpritesheetPlaybackEntry(
                index,
                MapLogicGraphImporter.ResolveLogicSignalNodeIndex(graph, configuration.StartInputRef),
                MapLogicGraphImporter.ResolveLogicSignalNodeIndex(graph, configuration.StopInputRef),
                MapLogicGraphImporter.ResolveLogicSignalNodeIndex(graph, configuration.NextFrameInputRef),
                configuration));
        }

        return entries.Count == 0
            ? SpritesheetPlaybackSet.Empty
            : new SpritesheetPlaybackSet(entries);
    }
}
