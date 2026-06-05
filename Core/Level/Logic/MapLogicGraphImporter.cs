using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

public static class MapLogicGraphImporter
{
    public static MapLogicGraph BuildFromEntities(
        IReadOnlyList<MapImportedEntity> entities,
        IReadOnlyList<RoomObjectMarker>? roomObjects = null)
    {
        var definitions = new List<MapLogicNodeDefinition>();
        foreach (var entity in entities)
        {
            if (!MapLogicMetadata.IsLogicEntityType(entity.Type)
                || AreaExtensionMetadata.IsAreaEntityType(entity.Type)
                || DamageableMetadata.IsDamageableEntityType(entity.Type))
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
                    SignalMode = MapLogicSignalMetadata.ParseSignalMode(properties),
                    CpCaptureDetectMode = MapLogicSignalMetadata.ParseCpCaptureDetectMode(properties),
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

            if (entity.Type.Equals(MapLogicMetadata.RisingEdgeEntityType, StringComparison.OrdinalIgnoreCase))
            {
                definitions.Add(new MapLogicNodeDefinition
                {
                    LogicKey = logicKey,
                    Kind = MapLogicNodeKind.RisingEdge,
                    InputRef = ReadProperty(properties, MapLogicMetadata.LogicInputPropertyKey),
                    NodePriority = MapLogicMetadata.ParseNodePriority(properties),
                });
                continue;
            }

            if (entity.Type.Equals(MapLogicMetadata.LatchEntityType, StringComparison.OrdinalIgnoreCase))
            {
                definitions.Add(new MapLogicNodeDefinition
                {
                    LogicKey = logicKey,
                    Kind = MapLogicNodeKind.Latch,
                    InputRef = ReadProperty(properties, MapLogicMetadata.LogicInputPropertyKey),
                    ResetRef = ReadProperty(properties, MapLogicMetadata.LogicResetPropertyKey),
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
                    DelayedTrue = MapLogicMetadata.ParseDelayedTrue(properties),
                    DelayedFalse = MapLogicMetadata.ParseDelayedFalse(properties),
                    NodePriority = MapLogicMetadata.ParseNodePriority(properties),
                    SignalMode = MapLogicSignalMetadata.ParseSignalMode(properties),
                });
                continue;
            }

            if (entity.Type.Equals(MapLogicMetadata.OscillatorEntityType, StringComparison.OrdinalIgnoreCase))
            {
                definitions.Add(new MapLogicNodeDefinition
                {
                    LogicKey = logicKey,
                    Kind = MapLogicNodeKind.Oscillator,
                    TrueTimeSeconds = MapLogicMetadata.ParseTrueTimeSeconds(properties),
                    FalseTimeSeconds = MapLogicMetadata.ParseFalseTimeSeconds(properties),
                    InitialValue = MapLogicMetadata.ParseInitialValue(properties),
                    Autostart = MapLogicMetadata.ParseAutostart(properties),
                    StartWhenRef = ReadProperty(properties, MapLogicMetadata.StartWhenPropertyKey),
                    EndWhenRef = ReadProperty(properties, MapLogicMetadata.EndWhenPropertyKey),
                    NodePriority = MapLogicMetadata.ParseNodePriority(properties),
                    SignalMode = MapLogicSignalMetadata.ParseSignalMode(properties),
                    SignalPeriodSeconds = MapLogicSignalMetadata.ParsePeriodSeconds(properties),
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
                var zoneIndices = roomObjects is null
                    ? []
                    : AreaExtensionMetadata.CollectPlayerTriggerZoneIndices(
                        roomObjects,
                        logicKey,
                        roomObjectIndex);
                definitions.Add(new MapLogicNodeDefinition
                {
                    LogicKey = logicKey,
                    Kind = MapLogicNodeKind.PlayerTrigger,
                    PlayerTriggerRoomObjectIndex = roomObjectIndex,
                    PlayerTriggerZoneRoomObjectIndices = zoneIndices,
                    PlayerTriggerTeamFilter = teamFilter,
                    NodePriority = MapLogicMetadata.ParseNodePriority(properties),
                    SignalMode = MapLogicSignalMetadata.ParseSignalMode(properties),
                    PlayerDetectMode = MapLogicSignalMetadata.ParsePlayerDetectMode(properties),
                });
                continue;
            }

            if (DamageTriggerMetadata.IsDamageTriggerEntityType(entity.Type))
            {
                var damageableRoomObjectIndex = roomObjects is null
                    ? -1
                    : DamageableMetadata.TryResolveRoomObjectIndex(
                        roomObjects,
                        ReadProperty(properties, DamageTriggerMetadata.DamageableEntityPropertyKey),
                        out var resolvedIndex)
                        ? resolvedIndex
                        : -1;
                definitions.Add(new MapLogicNodeDefinition
                {
                    LogicKey = logicKey,
                    Kind = MapLogicNodeKind.DamageTrigger,
                    DamageableRoomObjectIndex = damageableRoomObjectIndex,
                    TriggerBelowPercent = DamageTriggerMetadata.ParseTriggerBelowPercent(properties),
                    TriggerBelowThreshold = DamageTriggerMetadata.ParseTriggerBelowThreshold(properties),
                    TriggerOnAnyDamage = DamageTriggerMetadata.ParseTriggerOnAnyDamage(properties),
                    TriggerOnHeal = DamageTriggerMetadata.ParseTriggerOnHeal(properties),
                    TriggerWhenDestroyed = DamageTriggerMetadata.ParseTriggerWhenDestroyed(properties),
                    TrueTimeSeconds = DamageTriggerMetadata.ParseAnyDamageTrueTimeSeconds(properties),
                    NodePriority = MapLogicMetadata.ParseNodePriority(properties),
                    SignalMode = MapLogicSignalMetadata.ParseSignalMode(properties),
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
