using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

public static class MapLogicGraphImporter
{
    public static MapLogicGraph BuildFromEntities(
        IReadOnlyList<MapImportedEntity> entities,
        IList<RoomObjectMarker>? roomObjects = null)
    {
        var definitions = new List<MapLogicNodeDefinition>();
        foreach (var entity in entities)
        {
            if (!MapLogicMetadata.IsLogicEntityType(entity.Type))
            {
                continue;
            }

            var properties = entity.Properties ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var logicKey = MapLogicMetadata.EnsureLogicKey(properties);
            if (entity.Type.Equals(MapLogicMetadata.CpTriggerEntityType, StringComparison.OrdinalIgnoreCase))
            {
                MapLogicMetadata.TryParseCpTriggerOwnerRequirement(
                    ReadProperty(properties, MapLogicMetadata.RequiredOwnerPropertyKey),
                    out var ownerRequirement);
                definitions.Add(new MapLogicNodeDefinition
                {
                    LogicKey = logicKey,
                    Kind = MapLogicNodeKind.CpTrigger,
                    LinkedControlPointIndex = MapLogicMetadata.ParseLinkedControlPointIndex(properties),
                    OwnerRequirement = ownerRequirement,
                    NodePriority = MapLogicMetadata.ParseNodePriority(properties),
                });
                continue;
            }

            if (entity.Type.Equals(MapLogicMetadata.GateEntityType, StringComparison.OrdinalIgnoreCase))
            {
                MapLogicMetadata.TryParseGateType(
                    ReadProperty(properties, MapLogicMetadata.GateTypePropertyKey),
                    out var gateType);
                definitions.Add(new MapLogicNodeDefinition
                {
                    LogicKey = logicKey,
                    Kind = MapLogicNodeKind.Gate,
                    GateType = gateType,
                    InputRef1 = ReadProperty(properties, MapLogicMetadata.LogicInput1PropertyKey),
                    InputRef2 = ReadProperty(properties, MapLogicMetadata.LogicInput2PropertyKey),
                    NodePriority = MapLogicMetadata.ParseNodePriority(properties),
                });
                continue;
            }

            if (entity.Type.Equals(MapLogicMetadata.NotEntityType, StringComparison.OrdinalIgnoreCase))
            {
                definitions.Add(new MapLogicNodeDefinition
                {
                    LogicKey = logicKey,
                    Kind = MapLogicNodeKind.Not,
                    InputRef = ReadProperty(properties, MapLogicMetadata.LogicInputPropertyKey),
                    NodePriority = MapLogicMetadata.ParseNodePriority(properties),
                });
                continue;
            }

            if (entity.Type.Equals(MapLogicMetadata.TimerEntityType, StringComparison.OrdinalIgnoreCase))
            {
                definitions.Add(new MapLogicNodeDefinition
                {
                    LogicKey = logicKey,
                    Kind = MapLogicNodeKind.Timer,
                    InputRef = ReadProperty(properties, MapLogicMetadata.LogicInputPropertyKey),
                    CountdownSeconds = MapLogicMetadata.ParseCountdownSeconds(properties),
                    TriggerOnStart = MapLogicMetadata.ParseTriggerOnStart(properties),
                    NodePriority = MapLogicMetadata.ParseNodePriority(properties),
                });
                continue;
            }

            if (entity.Type.Equals(MapLogicMetadata.PlayerTriggerEntityType, StringComparison.OrdinalIgnoreCase))
            {
                PlayerTriggerMetadata.TryParseTeamFilter(
                    ReadProperty(properties, PlayerTriggerMetadata.TeamPropertyKey),
                    out var teamFilter);
                var roomObjectIndex = roomObjects is null
                    ? -1
                    : PlayerTriggerMetadata.TryResolveRoomObjectIndex(
                        roomObjects,
                        logicKey,
                        entity.X,
                        entity.Y);
                definitions.Add(new MapLogicNodeDefinition
                {
                    LogicKey = logicKey,
                    Kind = MapLogicNodeKind.PlayerTrigger,
                    PlayerTriggerRoomObjectIndex = roomObjectIndex,
                    PlayerTriggerTeamFilter = teamFilter,
                    NodePriority = MapLogicMetadata.ParseNodePriority(properties),
                });
            }
        }

        return MapLogicGraphBuilder.Build(definitions);
    }

    public static int ResolveLogicSignalNodeIndex(MapLogicGraph graph, string? logicSignalRef)
    {
        if (!MapLogicMetadata.TryParseLogicRef(logicSignalRef, out var logicKey)
            || !graph.TryGetNodeIndex(logicKey, out var nodeIndex))
        {
            return -1;
        }

        return nodeIndex;
    }

    public static ControlPointLockRules ApplyLogicToLockRules(
        ControlPointLockRules rules,
        MapLogicGraph graph,
        IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null || !graph.HasNodes)
        {
            return rules;
        }

        var lockedWhenLogic = ResolveLogicSignalNodeIndex(
            graph,
            ReadProperty(properties, MapLogicMetadata.LockedWhenLogicPropertyKey));
        var unlockedWhenLogic = ResolveLogicSignalNodeIndex(
            graph,
            ReadProperty(properties, MapLogicMetadata.UnlockedWhenLogicPropertyKey));
        if (lockedWhenLogic < 0 && unlockedWhenLogic < 0)
        {
            return rules;
        }

        return rules with
        {
            LockedWhenLogicNodeIndex = lockedWhenLogic,
            UnlockedWhenLogicNodeIndex = unlockedWhenLogic,
        };
    }

    public static SpawnPoint ApplyLogicSignal(SpawnPoint spawn, MapLogicGraph graph, IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null || !graph.HasNodes)
        {
            return spawn;
        }

        var logicNodeIndex = ResolveLogicSignalNodeIndex(
            graph,
            ReadProperty(properties, MapLogicMetadata.LogicSignalPropertyKey));
        if (logicNodeIndex < 0)
        {
            return spawn;
        }

        return spawn with { LogicSignalNodeIndex = logicNodeIndex };
    }

    private static string ReadProperty(IReadOnlyDictionary<string, string> properties, string key)
    {
        return properties.TryGetValue(key, out var value) ? value : string.Empty;
    }
}
