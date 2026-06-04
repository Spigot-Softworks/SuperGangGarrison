using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

internal static class MapLogicRuntimePatch
{
    private const float PositionTolerance = 0.75f;

    public static void ApplySpawnLogicSignals(
        IList<SpawnPoint> spawns,
        IReadOnlyList<MapImportedEntity> entities,
        MapLogicGraph graph)
    {
        if (!graph.HasNodes)
        {
            return;
        }

        foreach (var entity in entities)
        {
            if (!entity.Type.Equals("spawn", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!ReadBool(entity.Properties, "forward", false))
            {
                continue;
            }

            var nodeIndex = MapLogicGraphImporter.ResolveLogicSignalNodeIndex(
                graph,
                ReadProperty(entity.Properties, MapLogicMetadata.LogicSignalPropertyKey));
            if (nodeIndex < 0)
            {
                continue;
            }

            for (var index = 0; index < spawns.Count; index += 1)
            {
                if (!Approximately(spawns[index], entity.X, entity.Y))
                {
                    continue;
                }

                spawns[index] = spawns[index] with { LogicSignalNodeIndex = nodeIndex };
                break;
            }
        }
    }

    public static void ApplyControlPointLogicLocks(
        IList<RoomObjectMarker> roomObjects,
        IReadOnlyList<MapImportedEntity> entities,
        MapLogicGraph graph)
    {
        if (!graph.HasNodes)
        {
            return;
        }

        foreach (var entity in entities)
        {
            if (!entity.Type.Equals("controlPoint", StringComparison.OrdinalIgnoreCase)
                && !entity.Type.StartsWith("controlPoint", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lockedWhenLogic = MapLogicGraphImporter.ResolveLogicSignalNodeIndex(
                graph,
                ReadProperty(entity.Properties, MapLogicMetadata.LockedWhenLogicPropertyKey));
            var unlockedWhenLogic = MapLogicGraphImporter.ResolveLogicSignalNodeIndex(
                graph,
                ReadProperty(entity.Properties, MapLogicMetadata.UnlockedWhenLogicPropertyKey));
            if (lockedWhenLogic < 0 && unlockedWhenLogic < 0)
            {
                continue;
            }

            for (var index = 0; index < roomObjects.Count; index += 1)
            {
                var marker = roomObjects[index];
                if (marker.Type != RoomObjectType.ControlPoint || !Approximately(marker, entity.X, entity.Y))
                {
                    continue;
                }

                roomObjects[index] = marker with
                {
                    LockRules = marker.LockRules with
                    {
                        LockedWhenLogicNodeIndex = lockedWhenLogic,
                        UnlockedWhenLogicNodeIndex = unlockedWhenLogic,
                    },
                };
                break;
            }
        }
    }

    private static bool Approximately(SpawnPoint spawn, float x, float y)
    {
        return MathF.Abs(spawn.X - x) <= PositionTolerance && MathF.Abs(spawn.Y - y) <= PositionTolerance;
    }

    private static bool Approximately(RoomObjectMarker marker, float x, float y)
    {
        return MathF.Abs(marker.X - x) <= PositionTolerance && MathF.Abs(marker.Y - y) <= PositionTolerance;
    }

    private static string ReadProperty(IReadOnlyDictionary<string, string> properties, string key)
    {
        return properties.TryGetValue(key, out var value) ? value : string.Empty;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, string> properties, string key, bool fallback)
    {
        if (!properties.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Trim().Equals("1", StringComparison.OrdinalIgnoreCase);
    }
}
