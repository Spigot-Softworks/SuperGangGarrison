using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenGarrison.Core;

public enum MapLogicNodeKind
{
    CpTrigger,
    Gate,
    Not,
    Timer,
    PlayerTrigger,
}

public readonly struct PlayerTriggerEvaluationContext(
    IEnumerable<PlayerEntity> players,
    IReadOnlyList<RoomObjectMarker> roomObjects,
    Func<int, bool> isRoomObjectActive)
{
    public IEnumerable<PlayerEntity> Players { get; } = players;

    public IReadOnlyList<RoomObjectMarker> RoomObjects { get; } = roomObjects;

    public Func<int, bool> IsRoomObjectActive { get; } = isRoomObjectActive;
}

public sealed class MapLogicNode
{
    public MapLogicNode(
        int nodeIndex,
        string logicKey,
        MapLogicNodeKind kind,
        int linkedControlPointIndex = -1,
        MapLogicCpTriggerOwnerRequirement ownerRequirement = MapLogicCpTriggerOwnerRequirement.Red,
        MapLogicGateType gateType = MapLogicGateType.And,
        int inputNodeIndex1 = -1,
        int inputNodeIndex2 = -1,
        int inputNodeIndex = -1,
        int nodePriority = 0,
        float countdownSeconds = 1f,
        bool triggerOnStart = false,
        int playerTriggerRoomObjectIndex = -1,
        PlayerTriggerTeamFilter playerTriggerTeamFilter = PlayerTriggerTeamFilter.Any)
    {
        NodeIndex = nodeIndex;
        LogicKey = logicKey;
        Kind = kind;
        LinkedControlPointIndex = linkedControlPointIndex;
        OwnerRequirement = ownerRequirement;
        GateType = gateType;
        InputNodeIndex1 = inputNodeIndex1;
        InputNodeIndex2 = inputNodeIndex2;
        InputNodeIndex = inputNodeIndex;
        NodePriority = nodePriority;
        CountdownSeconds = countdownSeconds;
        TriggerOnStart = triggerOnStart;
        PlayerTriggerRoomObjectIndex = playerTriggerRoomObjectIndex;
        PlayerTriggerTeamFilter = playerTriggerTeamFilter;
    }

    public int NodeIndex { get; }

    public string LogicKey { get; }

    public MapLogicNodeKind Kind { get; }

    public int LinkedControlPointIndex { get; }

    public MapLogicCpTriggerOwnerRequirement OwnerRequirement { get; }

    public MapLogicGateType GateType { get; }

    public int InputNodeIndex1 { get; }

    public int InputNodeIndex2 { get; }

    public int InputNodeIndex { get; }

    public int NodePriority { get; }

    public float CountdownSeconds { get; }

    public bool TriggerOnStart { get; }

    public int PlayerTriggerRoomObjectIndex { get; }

    public PlayerTriggerTeamFilter PlayerTriggerTeamFilter { get; }
}

internal sealed class MapLogicTimerNodeState
{
    public float RemainingSeconds;

    public bool IsRunning;

    public bool HasFired;

    public bool PreviousTriggerInput;
}

public sealed class MapLogicGraph
{
    private readonly MapLogicNode[] _nodes;
    private readonly bool[] _outputs;
    private readonly int[] _evaluationOrder;
    private readonly MapLogicTimerNodeState[]? _timerStates;

    public MapLogicGraph(IReadOnlyList<MapLogicNode> nodes, int[] evaluationOrder)
    {
        _nodes = nodes.ToArray();
        _outputs = new bool[_nodes.Length];
        _evaluationOrder = evaluationOrder;
        NodeIndexByKey = BuildKeyIndex(_nodes);
        HasTimers = _nodes.Any(node => node.Kind == MapLogicNodeKind.Timer);
        HasPlayerTriggers = _nodes.Any(node => node.Kind == MapLogicNodeKind.PlayerTrigger);
        _timerStates = HasTimers ? new MapLogicTimerNodeState[_nodes.Length] : null;
        if (_timerStates is not null)
        {
            for (var index = 0; index < _timerStates.Length; index += 1)
            {
                _timerStates[index] = new MapLogicTimerNodeState();
            }
        }
    }

    public IReadOnlyList<MapLogicNode> Nodes => _nodes;

    public IReadOnlyDictionary<string, int> NodeIndexByKey { get; }

    public static MapLogicGraph Empty { get; } = new(Array.Empty<MapLogicNode>(), Array.Empty<int>());

    public bool HasNodes => _nodes.Length > 0;

    public bool HasTimers { get; }

    public bool HasPlayerTriggers { get; }

    public bool TryGetNodeIndex(string logicKey, out int nodeIndex)
    {
        return NodeIndexByKey.TryGetValue(logicKey, out nodeIndex);
    }

    public bool GetOutput(int nodeIndex)
    {
        if (nodeIndex < 0 || nodeIndex >= _outputs.Length)
        {
            return false;
        }

        return _outputs[nodeIndex];
    }

    public int GetNodePriority(int nodeIndex)
    {
        if (nodeIndex < 0 || nodeIndex >= _nodes.Length)
        {
            return 0;
        }

        return _nodes[nodeIndex].NodePriority;
    }

    public void Evaluate(IReadOnlyList<ControlPointState> controlPoints)
    {
        EvaluateCombinatorial(controlPoints);
        AdvanceTimers(0f);
    }

    public void EvaluateCombinatorial(
        IReadOnlyList<ControlPointState> controlPoints,
        PlayerTriggerEvaluationContext? playerTriggers = null)
    {
        if (_nodes.Length == 0)
        {
            return;
        }

        Array.Clear(_outputs, 0, _outputs.Length);
        for (var orderIndex = 0; orderIndex < _evaluationOrder.Length; orderIndex += 1)
        {
            var nodeIndex = _evaluationOrder[orderIndex];
            var node = _nodes[nodeIndex];
            if (node.Kind == MapLogicNodeKind.Timer)
            {
                continue;
            }

            _outputs[nodeIndex] = node.Kind switch
            {
                MapLogicNodeKind.CpTrigger => MapLogicMetadata.EvaluateCpTrigger(
                    node.LinkedControlPointIndex,
                    node.OwnerRequirement,
                    controlPoints),
                MapLogicNodeKind.PlayerTrigger => EvaluatePlayerTrigger(node, playerTriggers),
                MapLogicNodeKind.Gate => MapLogicMetadata.EvaluateGate(
                    node.GateType,
                    ReadInput(node.InputNodeIndex1),
                    ReadInput(node.InputNodeIndex2)),
                MapLogicNodeKind.Not => !ReadInput(node.InputNodeIndex),
                _ => false,
            };
        }
    }

    private static bool EvaluatePlayerTrigger(MapLogicNode node, PlayerTriggerEvaluationContext? context)
    {
        if (context is not PlayerTriggerEvaluationContext activeContext
            || node.PlayerTriggerRoomObjectIndex < 0)
        {
            return false;
        }

        var roomObjectIndex = node.PlayerTriggerRoomObjectIndex;
        if (roomObjectIndex >= activeContext.RoomObjects.Count
            || !activeContext.IsRoomObjectActive(roomObjectIndex))
        {
            return false;
        }

        var marker = activeContext.RoomObjects[roomObjectIndex];
        return PlayerTriggerMetadata.AnyMatchingPlayerInside(
            marker,
            node.PlayerTriggerTeamFilter,
            activeContext.Players);
    }

    public void ResetTimerStates()
    {
        if (_timerStates is null)
        {
            return;
        }

        for (var index = 0; index < _nodes.Length; index += 1)
        {
            var node = _nodes[index];
            if (node.Kind != MapLogicNodeKind.Timer)
            {
                continue;
            }

            var state = _timerStates[index];
            state.RemainingSeconds = 0f;
            state.IsRunning = false;
            state.HasFired = false;
            state.PreviousTriggerInput = false;
            if (node.TriggerOnStart)
            {
                state.RemainingSeconds = node.CountdownSeconds;
                state.IsRunning = true;
            }
        }
    }

    public void AdvanceTimers(float deltaSeconds)
    {
        if (_timerStates is null)
        {
            return;
        }

        for (var orderIndex = 0; orderIndex < _evaluationOrder.Length; orderIndex += 1)
        {
            var nodeIndex = _evaluationOrder[orderIndex];
            var node = _nodes[nodeIndex];
            if (node.Kind != MapLogicNodeKind.Timer)
            {
                continue;
            }

            var state = _timerStates[nodeIndex];
            if (!node.TriggerOnStart)
            {
                var triggerInput = ReadInput(node.InputNodeIndex);
                if (triggerInput && !state.PreviousTriggerInput)
                {
                    state.RemainingSeconds = node.CountdownSeconds;
                    state.IsRunning = true;
                    state.HasFired = false;
                }

                state.PreviousTriggerInput = triggerInput;
            }

            if (state.IsRunning && !state.HasFired && deltaSeconds > 0f)
            {
                state.RemainingSeconds -= deltaSeconds;
                if (state.RemainingSeconds <= 0f)
                {
                    state.RemainingSeconds = 0f;
                    state.IsRunning = false;
                    state.HasFired = true;
                }
            }

            _outputs[nodeIndex] = state.HasFired;
        }
    }

    private bool ReadInput(int inputNodeIndex)
    {
        return inputNodeIndex >= 0
            && inputNodeIndex < _outputs.Length
            && _outputs[inputNodeIndex];
    }

    private static Dictionary<string, int> BuildKeyIndex(MapLogicNode[] nodes)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < nodes.Length; index += 1)
        {
            if (!string.IsNullOrWhiteSpace(nodes[index].LogicKey))
            {
                map[nodes[index].LogicKey] = index;
            }
        }

        return map;
    }
}

public static class MapLogicGraphBuilder
{
    public static MapLogicGraph Build(IReadOnlyList<MapLogicNodeDefinition> definitions)
    {
        if (definitions.Count == 0)
        {
            return MapLogicGraph.Empty;
        }

        var keyToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var nodes = new List<MapLogicNode>(definitions.Count);
        for (var index = 0; index < definitions.Count; index += 1)
        {
            var definition = definitions[index];
            keyToIndex[definition.LogicKey] = index;
            nodes.Add(new MapLogicNode(
                index,
                definition.LogicKey,
                definition.Kind,
                definition.LinkedControlPointIndex,
                definition.OwnerRequirement,
                definition.GateType,
                -1,
                -1,
                -1,
                definition.NodePriority,
                definition.CountdownSeconds,
                definition.TriggerOnStart,
                definition.PlayerTriggerRoomObjectIndex,
                definition.PlayerTriggerTeamFilter));
        }

        var resolvedNodes = new MapLogicNode[nodes.Count];
        for (var index = 0; index < definitions.Count; index += 1)
        {
            var definition = definitions[index];
            var baseNode = nodes[index];
            resolvedNodes[index] = new MapLogicNode(
                baseNode.NodeIndex,
                baseNode.LogicKey,
                baseNode.Kind,
                baseNode.LinkedControlPointIndex,
                baseNode.OwnerRequirement,
                baseNode.GateType,
                ResolveInput(definition.InputRef1, keyToIndex),
                ResolveInput(definition.InputRef2, keyToIndex),
                ResolveInput(definition.InputRef, keyToIndex),
                baseNode.NodePriority,
                baseNode.CountdownSeconds,
                baseNode.TriggerOnStart,
                baseNode.PlayerTriggerRoomObjectIndex,
                baseNode.PlayerTriggerTeamFilter);
        }

        var evaluationOrder = BuildEvaluationOrder(resolvedNodes);
        return new MapLogicGraph(resolvedNodes, evaluationOrder);
    }

    private static int ResolveInput(string? inputRef, IReadOnlyDictionary<string, int> keyToIndex)
    {
        if (!MapLogicMetadata.TryParseLogicRef(inputRef, out var logicKey)
            || !keyToIndex.TryGetValue(logicKey, out var nodeIndex))
        {
            return -1;
        }

        return nodeIndex;
    }

    private static int[] BuildEvaluationOrder(IReadOnlyList<MapLogicNode> nodes)
    {
        var order = new List<int>(nodes.Count);
        var visited = new bool[nodes.Count];
        var visiting = new bool[nodes.Count];

        for (var index = 0; index < nodes.Count; index += 1)
        {
            Visit(index, nodes, visited, visiting, order);
        }

        return order.ToArray();
    }

    private static void Visit(
        int nodeIndex,
        IReadOnlyList<MapLogicNode> nodes,
        bool[] visited,
        bool[] visiting,
        IList<int> order)
    {
        if (visited[nodeIndex])
        {
            return;
        }

        if (visiting[nodeIndex])
        {
            return;
        }

        visiting[nodeIndex] = true;
        var node = nodes[nodeIndex];
        if (node.InputNodeIndex1 >= 0)
        {
            Visit(node.InputNodeIndex1, nodes, visited, visiting, order);
        }

        if (node.InputNodeIndex2 >= 0)
        {
            Visit(node.InputNodeIndex2, nodes, visited, visiting, order);
        }

        if (node.InputNodeIndex >= 0)
        {
            Visit(node.InputNodeIndex, nodes, visited, visiting, order);
        }

        visiting[nodeIndex] = false;
        visited[nodeIndex] = true;
        order.Add(nodeIndex);
    }
}

public sealed class MapLogicNodeDefinition
{
    public required string LogicKey { get; init; }

    public required MapLogicNodeKind Kind { get; init; }

    public int LinkedControlPointIndex { get; init; } = -1;

    public MapLogicCpTriggerOwnerRequirement OwnerRequirement { get; init; } = MapLogicCpTriggerOwnerRequirement.Red;

    public MapLogicGateType GateType { get; init; } = MapLogicGateType.And;

    public string? InputRef { get; init; }

    public string? InputRef1 { get; init; }

    public string? InputRef2 { get; init; }

    public int NodePriority { get; init; }

    public float CountdownSeconds { get; init; } = 1f;

    public bool TriggerOnStart { get; init; }

    public int PlayerTriggerRoomObjectIndex { get; init; } = -1;

    public PlayerTriggerTeamFilter PlayerTriggerTeamFilter { get; init; } = PlayerTriggerTeamFilter.Any;
}
