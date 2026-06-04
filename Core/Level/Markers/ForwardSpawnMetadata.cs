using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Core;

public static class ForwardSpawnMetadata
{
    public const string UseWhenPropertyKey = "useWhen";
    public const string DefaultUseWhenValue = "owned";

    public static bool TryParseUseCondition(string? value, out ForwardSpawnUseCondition condition)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            condition = ForwardSpawnUseCondition.ObjectiveOwnedByTeam;
            return true;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "owned":
            case "objowned":
            case "objectiveowned":
            case "obj owned":
                condition = ForwardSpawnUseCondition.ObjectiveOwnedByTeam;
                return true;
            case "notowned":
            case "objnotowned":
            case "objectivenotowned":
            case "obj not owned":
                condition = ForwardSpawnUseCondition.ObjectiveNotOwnedByTeam;
                return true;
            case "neutral":
            case "objneutral":
            case "objectiveneutral":
            case "obj neutral":
                condition = ForwardSpawnUseCondition.ObjectiveNeutral;
                return true;
            case "enemyowned":
            case "objenemyowned":
            case "objectiveownedbyenemy":
            case "obj owned by enemy":
                condition = ForwardSpawnUseCondition.ObjectiveOwnedByEnemy;
                return true;
            default:
                condition = default;
                return false;
        }
    }

    public static string ToPropertyValue(ForwardSpawnUseCondition condition)
    {
        return condition switch
        {
            ForwardSpawnUseCondition.ObjectiveOwnedByTeam => "owned",
            ForwardSpawnUseCondition.ObjectiveNotOwnedByTeam => "notOwned",
            ForwardSpawnUseCondition.ObjectiveNeutral => "neutral",
            ForwardSpawnUseCondition.ObjectiveOwnedByEnemy => "enemyOwned",
            _ => DefaultUseWhenValue,
        };
    }

    public static string GetUseConditionDisplayLabel(ForwardSpawnUseCondition condition)
    {
        return condition switch
        {
            ForwardSpawnUseCondition.ObjectiveOwnedByTeam => "Obj owned",
            ForwardSpawnUseCondition.ObjectiveNotOwnedByTeam => "Obj not owned",
            ForwardSpawnUseCondition.ObjectiveNeutral => "Obj neutral",
            ForwardSpawnUseCondition.ObjectiveOwnedByEnemy => "Obj owned by enemy",
            _ => "Obj owned",
        };
    }

    public static string GetUseConditionDisplayLabel(string? propertyValue)
    {
        return TryParseUseCondition(propertyValue, out var condition)
            ? GetUseConditionDisplayLabel(condition)
            : propertyValue ?? string.Empty;
    }

    public static string CycleUseWhenPropertyValue(string current)
    {
        if (!TryParseUseCondition(current, out var condition))
        {
            condition = ForwardSpawnUseCondition.ObjectiveOwnedByTeam;
        }

        var next = (ForwardSpawnUseCondition)(((int)condition + 1) % 4);
        return ToPropertyValue(next);
    }

    public static int ParsePriority(IReadOnlyDictionary<string, string> properties)
    {
        if (properties.TryGetValue(ForwardSpawnPriorityMetadata.PropertyKey, out var priorityRaw)
            && ForwardSpawnPriorityMetadata.TryParse(priorityRaw, out var priority))
        {
            return priority;
        }

        if (properties.TryGetValue("objectiveIndex", out var objectiveIndexRaw)
            && int.TryParse(objectiveIndexRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var legacyPriority)
            && legacyPriority > 0)
        {
            return ForwardSpawnPriorityMetadata.ClampPriority(legacyPriority);
        }

        return ForwardSpawnPriorityMetadata.MinPriority;
    }

    public static int ParseLinkedControlPointIndex(IReadOnlyDictionary<string, string> properties)
    {
        if (properties.TryGetValue("objectiveIndex", out var objectiveIndexRaw)
            && int.TryParse(objectiveIndexRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var objectiveIndex)
            && objectiveIndex > 0)
        {
            return objectiveIndex;
        }

        if (properties.TryGetValue("linkObjective", out var linkObjective))
        {
            return ParseControlPointIndexFromLink(linkObjective);
        }

        return 0;
    }

    public static int ParseControlPointIndexFromLink(string? linkObjective)
    {
        if (string.IsNullOrWhiteSpace(linkObjective))
        {
            return 0;
        }

        var trimmed = linkObjective.Trim();
        if (trimmed.StartsWith("controlPoint", StringComparison.OrdinalIgnoreCase)
            && trimmed.Length > "controlPoint".Length
            && int.TryParse(trimmed["controlPoint".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
            && index > 0)
        {
            return index;
        }

        if (trimmed.Equals("KothControlPoint", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("KothRedControlPoint", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("KothBlueControlPoint", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("ArenaControlPoint", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 0;
    }

    public static bool EvaluateUseCondition(ForwardSpawnUseCondition condition, PlayerTeam team, PlayerTeam? objectiveOwner)
    {
        return condition switch
        {
            ForwardSpawnUseCondition.ObjectiveOwnedByTeam => objectiveOwner == team,
            ForwardSpawnUseCondition.ObjectiveNotOwnedByTeam => objectiveOwner != team,
            ForwardSpawnUseCondition.ObjectiveNeutral => objectiveOwner is null,
            ForwardSpawnUseCondition.ObjectiveOwnedByEnemy => objectiveOwner.HasValue && objectiveOwner.Value != team,
            _ => false,
        };
    }

    /// <summary>
    /// Maps a forward-spawn slot (1 = closest to that team's attack direction) to a numbered control point.
    /// Red uses slots in ascending CP order; blue uses descending CP order because points are numbered red-to-blue.
    /// </summary>
    public static int ResolveForwardSpawnControlPointIndex(PlayerTeam team, int spawnSlotIndex, int totalControlPoints)
    {
        if (spawnSlotIndex <= 0)
        {
            return 0;
        }

        if (totalControlPoints <= 0)
        {
            return spawnSlotIndex;
        }

        var clampedSlot = Math.Clamp(spawnSlotIndex, 1, totalControlPoints);
        return team == PlayerTeam.Blue
            ? totalControlPoints - clampedSlot + 1
            : clampedSlot;
    }

    public static int CountMapControlPoints(IReadOnlyList<RoomObjectMarker> roomObjects)
    {
        var maxIndex = 0;
        var count = 0;
        for (var index = 0; index < roomObjects.Count; index += 1)
        {
            var marker = roomObjects[index];
            if (marker.Type is not RoomObjectType.ControlPoint and not RoomObjectType.ArenaControlPoint)
            {
                continue;
            }

            count += 1;
            if (ControlPointMarkerIndex.TryGetIndex(marker, out var markerIndex))
            {
                maxIndex = Math.Max(maxIndex, markerIndex);
            }
            else
            {
                maxIndex = Math.Max(maxIndex, 1);
            }
        }

        return Math.Max(maxIndex, count);
    }

    public static int CountMapControlPointsFromEditorEntities(IReadOnlyList<CustomMapBuilderEntity> entities)
    {
        var maxIndex = 0;
        var count = 0;
        for (var index = 0; index < entities.Count; index += 1)
        {
            var entity = entities[index];
            if (!ControlPointOwnershipResolver.IsControlPointEntity(entity.Type))
            {
                continue;
            }

            count += 1;
            maxIndex = Math.Max(maxIndex, ControlPointOwnershipResolver.ResolveControlPointIndex(entity));
        }

        return Math.Max(maxIndex, count);
    }

    public static void ApplyForwardSpawnControlPointLinks(
        IList<SpawnPoint> redSpawns,
        IList<SpawnPoint> blueSpawns,
        int totalControlPoints)
    {
        for (var index = 0; index < redSpawns.Count; index += 1)
        {
            redSpawns[index] = RemapForwardSpawnControlPointLink(redSpawns[index], PlayerTeam.Red, totalControlPoints);
        }

        for (var index = 0; index < blueSpawns.Count; index += 1)
        {
            blueSpawns[index] = RemapForwardSpawnControlPointLink(blueSpawns[index], PlayerTeam.Blue, totalControlPoints);
        }
    }

    private static SpawnPoint RemapForwardSpawnControlPointLink(
        SpawnPoint spawn,
        PlayerTeam team,
        int totalControlPoints)
    {
        if (!spawn.IsForwardSpawn)
        {
            return spawn;
        }

        if (spawn.LinkedControlPointIndex > 0 && spawn.Priority < ForwardSpawnPriorityMetadata.MinPriority)
        {
            return spawn;
        }

        var spawnSlot = spawn.Priority >= ForwardSpawnPriorityMetadata.MinPriority
            ? spawn.Priority
            : spawn.LinkedControlPointIndex;
        if (spawnSlot <= 0)
        {
            return spawn;
        }

        var linkedIndex = ResolveForwardSpawnControlPointIndex(team, spawnSlot, totalControlPoints);
        return spawn with { LinkedControlPointIndex = linkedIndex };
    }
}
