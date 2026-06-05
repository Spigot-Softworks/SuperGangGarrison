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
    Oscillator,
    PlayerTrigger,
    IntelTrigger,
    DamageTrigger,
    RisingEdge,
    Latch,
}

public readonly struct DamageTriggerEvaluationContext(Func<int, float> getHealthRatio)
{
    public float GetHealthRatio(int roomObjectIndex)
    {
        return roomObjectIndex >= 0 ? getHealthRatio(roomObjectIndex) : 1f;
    }
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

public readonly struct IntelTriggerEvaluationContext(
    TeamIntelligenceState redIntel,
    TeamIntelligenceState blueIntel)
{
    public TeamIntelligenceState RedIntel { get; } = redIntel;

    public TeamIntelligenceState BlueIntel { get; } = blueIntel;
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
        bool delayedTrue = true,
        bool delayedFalse = true,
        float trueTimeSeconds = 1f,
        float falseTimeSeconds = 1f,
        bool initialValue = true,
        bool autostart = false,
        int startWhenNodeIndex = -1,
        int endWhenNodeIndex = -1,
        int resetNodeIndex = -1,
        int playerTriggerRoomObjectIndex = -1,
        int[]? playerTriggerZoneRoomObjectIndices = null,
        PlayerTriggerTeamFilter playerTriggerTeamFilter = PlayerTriggerTeamFilter.Any,
        bool playerTriggerIntelCarriersOnly = false,
        int damageableRoomObjectIndex = -1,
        int triggerBelowPercent = DamageTriggerMetadata.DefaultTriggerBelowPercent,
        bool triggerBelowThreshold = false,
        bool triggerOnAnyDamage = false,
        bool triggerOnHeal = false,
        bool triggerWhenDestroyed = false,
        MapLogicSignalMode signalMode = MapLogicSignalMode.Latch,
        MapLogicCpCaptureDetectMode cpCaptureDetectMode = MapLogicCpCaptureDetectMode.AnyCapture,
        MapLogicPlayerDetectMode playerDetectMode = MapLogicPlayerDetectMode.PlayerEnter,
        float signalPeriodSeconds = MapLogicSignalMetadata.DefaultPeriodSeconds,
        IntelTriggerIntelFilter intelTriggerIntelFilter = IntelTriggerIntelFilter.Any,
        IntelTriggerLatchState intelTriggerLatchState = IntelTriggerLatchState.AtBase,
        bool intelTriggerOnPickup = false,
        bool intelTriggerOnDrop = false,
        bool intelTriggerOnCapture = false,
        bool intelTriggerOnReset = false)
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
        DelayedTrue = delayedTrue;
        DelayedFalse = delayedFalse;
        TrueTimeSeconds = trueTimeSeconds;
        FalseTimeSeconds = falseTimeSeconds;
        InitialValue = initialValue;
        Autostart = autostart;
        StartWhenNodeIndex = startWhenNodeIndex;
        EndWhenNodeIndex = endWhenNodeIndex;
        ResetNodeIndex = resetNodeIndex;
        PlayerTriggerRoomObjectIndex = playerTriggerRoomObjectIndex;
        PlayerTriggerZoneRoomObjectIndices = playerTriggerZoneRoomObjectIndices ?? [];
        PlayerTriggerTeamFilter = playerTriggerTeamFilter;
        PlayerTriggerIntelCarriersOnly = playerTriggerIntelCarriersOnly;
        DamageableRoomObjectIndex = damageableRoomObjectIndex;
        TriggerBelowPercent = triggerBelowPercent;
        TriggerBelowThreshold = triggerBelowThreshold;
        TriggerOnAnyDamage = triggerOnAnyDamage;
        TriggerOnHeal = triggerOnHeal;
        TriggerWhenDestroyed = triggerWhenDestroyed;
        SignalMode = signalMode;
        CpCaptureDetectMode = cpCaptureDetectMode;
        PlayerDetectMode = playerDetectMode;
        SignalPeriodSeconds = signalPeriodSeconds;
        IntelTriggerIntelFilter = intelTriggerIntelFilter;
        IntelTriggerLatchState = intelTriggerLatchState;
        IntelTriggerOnPickup = intelTriggerOnPickup;
        IntelTriggerOnDrop = intelTriggerOnDrop;
        IntelTriggerOnCapture = intelTriggerOnCapture;
        IntelTriggerOnReset = intelTriggerOnReset;
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

    public bool DelayedTrue { get; }

    public bool DelayedFalse { get; }

    public float TrueTimeSeconds { get; }

    public float FalseTimeSeconds { get; }

    public bool InitialValue { get; }

    public bool Autostart { get; }

    public int StartWhenNodeIndex { get; }

    public int EndWhenNodeIndex { get; }

    public int ResetNodeIndex { get; }

    public int PlayerTriggerRoomObjectIndex { get; }

    public int[] PlayerTriggerZoneRoomObjectIndices { get; }

    public PlayerTriggerTeamFilter PlayerTriggerTeamFilter { get; }

    public bool PlayerTriggerIntelCarriersOnly { get; }

    public int DamageableRoomObjectIndex { get; }

    public int TriggerBelowPercent { get; }

    public bool TriggerBelowThreshold { get; }

    public bool TriggerOnAnyDamage { get; }

    public bool TriggerOnHeal { get; }

    public bool TriggerWhenDestroyed { get; }

    public MapLogicSignalMode SignalMode { get; }

    public MapLogicCpCaptureDetectMode CpCaptureDetectMode { get; }

    public MapLogicPlayerDetectMode PlayerDetectMode { get; }

    public float SignalPeriodSeconds { get; }

    public IntelTriggerIntelFilter IntelTriggerIntelFilter { get; }

    public IntelTriggerLatchState IntelTriggerLatchState { get; }

    public bool IntelTriggerOnPickup { get; }

    public bool IntelTriggerOnDrop { get; }

    public bool IntelTriggerOnCapture { get; }

    public bool IntelTriggerOnReset { get; }
}

internal sealed class MapLogicIntelTriggerNodeState
{
    public IntelTriggerPhase PreviousRedPhase;

    public IntelTriggerPhase PreviousBluePhase;
}

internal sealed class MapLogicCpTriggerNodeState
{
    public PlayerTeam? PreviousOwner;
}

internal sealed class MapLogicPlayerTriggerNodeState
{
    public bool WasOccupied;
}

internal sealed class MapLogicDamageTriggerNodeState
{
    public float PreviousHealthRatio = 1f;

    public float AnyDamagePulseRemainingSeconds;
}

internal sealed class MapLogicRisingEdgeNodeState
{
    public bool PreviousInput;
}

internal sealed class MapLogicLatchNodeState
{
    public bool Latched;

    public bool PreviousReset;
}

internal sealed class MapLogicTimerNodeState
{
    public bool Output;

    public bool PendingOutput;

    public float RemainingSeconds;

    public bool IsDelayActive;

    public bool PreviousInput;

    public bool ClearImpulseOnNextAdvance;
}

internal sealed class MapLogicOscillatorNodeState
{
    public bool Output;

    public bool IsRunning;

    public bool PhaseIsTrue;

    public float PhaseRemainingSeconds;

    public bool PreviousStartWhen;

    public bool AwaitingStartWhenBaseline;

    public bool ClearImpulseOnNextAdvance;
}

public sealed class MapLogicGraph
{
    private readonly MapLogicNode[] _nodes;
    private readonly bool[] _outputs;
    private readonly int[] _evaluationOrder;
    private readonly MapLogicTimerNodeState[]? _timerStates;
    private readonly MapLogicOscillatorNodeState[]? _oscillatorStates;
    private readonly MapLogicDamageTriggerNodeState[]? _damageTriggerStates;
    private readonly MapLogicRisingEdgeNodeState[]? _risingEdgeStates;
    private readonly MapLogicLatchNodeState[]? _latchStates;
    private readonly MapLogicCpTriggerNodeState[]? _cpTriggerStates;
    private readonly MapLogicPlayerTriggerNodeState[]? _playerTriggerStates;
    private readonly MapLogicIntelTriggerNodeState[]? _intelTriggerStates;

    public MapLogicGraph(IReadOnlyList<MapLogicNode> nodes, int[] evaluationOrder)
    {
        _nodes = nodes.ToArray();
        _outputs = new bool[_nodes.Length];
        _evaluationOrder = evaluationOrder;
        NodeIndexByKey = BuildKeyIndex(_nodes);
        HasTimers = _nodes.Any(node => node.Kind == MapLogicNodeKind.Timer);
        HasOscillators = _nodes.Any(node => node.Kind == MapLogicNodeKind.Oscillator);
        HasPlayerTriggers = _nodes.Any(node => node.Kind == MapLogicNodeKind.PlayerTrigger);
        HasDamageTriggers = _nodes.Any(node => node.Kind == MapLogicNodeKind.DamageTrigger);
        HasRisingEdges = _nodes.Any(node => node.Kind == MapLogicNodeKind.RisingEdge);
        HasLatches = _nodes.Any(node => node.Kind == MapLogicNodeKind.Latch);
        HasCpTriggers = _nodes.Any(node => node.Kind == MapLogicNodeKind.CpTrigger);
        HasIntelTriggers = _nodes.Any(node => node.Kind == MapLogicNodeKind.IntelTrigger);
        _timerStates = HasTimers ? new MapLogicTimerNodeState[_nodes.Length] : null;
        if (_timerStates is not null)
        {
            for (var index = 0; index < _timerStates.Length; index += 1)
            {
                _timerStates[index] = new MapLogicTimerNodeState();
            }
        }

        _oscillatorStates = HasOscillators ? new MapLogicOscillatorNodeState[_nodes.Length] : null;
        if (_oscillatorStates is not null)
        {
            for (var index = 0; index < _oscillatorStates.Length; index += 1)
            {
                _oscillatorStates[index] = new MapLogicOscillatorNodeState();
            }
        }

        _damageTriggerStates = HasDamageTriggers ? new MapLogicDamageTriggerNodeState[_nodes.Length] : null;
        if (_damageTriggerStates is not null)
        {
            for (var index = 0; index < _damageTriggerStates.Length; index += 1)
            {
                _damageTriggerStates[index] = new MapLogicDamageTriggerNodeState();
            }
        }

        _risingEdgeStates = HasRisingEdges ? new MapLogicRisingEdgeNodeState[_nodes.Length] : null;
        if (_risingEdgeStates is not null)
        {
            for (var index = 0; index < _risingEdgeStates.Length; index += 1)
            {
                _risingEdgeStates[index] = new MapLogicRisingEdgeNodeState();
            }
        }

        _latchStates = HasLatches ? new MapLogicLatchNodeState[_nodes.Length] : null;
        if (_latchStates is not null)
        {
            for (var index = 0; index < _latchStates.Length; index += 1)
            {
                _latchStates[index] = new MapLogicLatchNodeState();
            }
        }

        _cpTriggerStates = HasCpTriggers ? new MapLogicCpTriggerNodeState[_nodes.Length] : null;
        if (_cpTriggerStates is not null)
        {
            for (var index = 0; index < _cpTriggerStates.Length; index += 1)
            {
                _cpTriggerStates[index] = new MapLogicCpTriggerNodeState();
            }
        }

        _playerTriggerStates = HasPlayerTriggers ? new MapLogicPlayerTriggerNodeState[_nodes.Length] : null;
        if (_playerTriggerStates is not null)
        {
            for (var index = 0; index < _playerTriggerStates.Length; index += 1)
            {
                _playerTriggerStates[index] = new MapLogicPlayerTriggerNodeState();
            }
        }

        _intelTriggerStates = HasIntelTriggers ? new MapLogicIntelTriggerNodeState[_nodes.Length] : null;
        if (_intelTriggerStates is not null)
        {
            for (var index = 0; index < _intelTriggerStates.Length; index += 1)
            {
                _intelTriggerStates[index] = new MapLogicIntelTriggerNodeState();
            }
        }
    }

    public IReadOnlyList<MapLogicNode> Nodes => _nodes;

    public IReadOnlyDictionary<string, int> NodeIndexByKey { get; }

    public static MapLogicGraph Empty { get; } = new(Array.Empty<MapLogicNode>(), Array.Empty<int>());

    public bool HasNodes => _nodes.Length > 0;

    public bool HasTimers { get; }

    public bool HasOscillators { get; }

    public bool HasPlayerTriggers { get; }

    public bool HasDamageTriggers { get; }

    public bool HasRisingEdges { get; }

    public bool HasLatches { get; }

    public bool HasCpTriggers { get; }

    public bool HasIntelTriggers { get; }

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
        AdvanceOscillators(0f);
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
            if (node.Kind is MapLogicNodeKind.Timer or MapLogicNodeKind.Oscillator or MapLogicNodeKind.DamageTrigger or MapLogicNodeKind.IntelTrigger)
            {
                continue;
            }

            _outputs[nodeIndex] = node.Kind switch
            {
                MapLogicNodeKind.CpTrigger => EvaluateCpTrigger(nodeIndex, node, controlPoints),
                MapLogicNodeKind.PlayerTrigger => EvaluatePlayerTrigger(nodeIndex, node, playerTriggers),
                MapLogicNodeKind.Gate => MapLogicMetadata.EvaluateGate(
                    node.GateType,
                    ReadInput(node.InputNodeIndex1),
                    ReadInput(node.InputNodeIndex2)),
                MapLogicNodeKind.Not => !ReadInput(node.InputNodeIndex),
                MapLogicNodeKind.RisingEdge => EvaluateRisingEdge(nodeIndex, node),
                MapLogicNodeKind.Latch => EvaluateLatch(nodeIndex, node),
                _ => false,
            };
        }
    }

    private bool EvaluateRisingEdge(int nodeIndex, MapLogicNode node)
    {
        if (_risingEdgeStates is null)
        {
            return false;
        }

        var input = ReadInput(node.InputNodeIndex);
        var state = _risingEdgeStates[nodeIndex];
        var output = input && !state.PreviousInput;
        state.PreviousInput = input;
        return output;
    }

    private bool EvaluateLatch(int nodeIndex, MapLogicNode node)
    {
        if (_latchStates is null)
        {
            return false;
        }

        var input = ReadInput(node.InputNodeIndex);
        var reset = ReadInput(node.ResetNodeIndex);
        var state = _latchStates[nodeIndex];
        if (input)
        {
            state.Latched = true;
        }

        if (reset && !state.PreviousReset)
        {
            state.Latched = false;
        }

        state.PreviousReset = reset;
        return state.Latched;
    }

    public void ResetRisingEdgeStates()
    {
        if (_risingEdgeStates is null)
        {
            return;
        }

        for (var index = 0; index < _risingEdgeStates.Length; index += 1)
        {
            _risingEdgeStates[index].PreviousInput = false;
        }
    }

    public void ResetLatchStates()
    {
        if (_latchStates is null)
        {
            return;
        }

        for (var index = 0; index < _latchStates.Length; index += 1)
        {
            _latchStates[index].Latched = false;
            _latchStates[index].PreviousReset = false;
        }
    }

    private bool EvaluateCpTrigger(
        int nodeIndex,
        MapLogicNode node,
        IReadOnlyList<ControlPointState> controlPoints)
    {
        if (!MapLogicMetadata.TryGetControlPointOwner(controlPoints, node.LinkedControlPointIndex, out var currentOwner))
        {
            if (node.SignalMode == MapLogicSignalMode.Impulse && _cpTriggerStates is not null)
            {
                _cpTriggerStates[nodeIndex].PreviousOwner = null;
            }

            return false;
        }

        if (node.SignalMode == MapLogicSignalMode.Latch)
        {
            if (_cpTriggerStates is not null)
            {
                _cpTriggerStates[nodeIndex].PreviousOwner = currentOwner;
            }

            return MapLogicMetadata.EvaluateCpTrigger(
                node.LinkedControlPointIndex,
                node.OwnerRequirement,
                controlPoints);
        }

        if (_cpTriggerStates is null)
        {
            return false;
        }

        var state = _cpTriggerStates[nodeIndex];
        var captured = DetectCpCapture(state.PreviousOwner, currentOwner, node.CpCaptureDetectMode);
        state.PreviousOwner = currentOwner;
        return captured;
    }

    private static bool DetectCpCapture(
        PlayerTeam? previousOwner,
        PlayerTeam? currentOwner,
        MapLogicCpCaptureDetectMode detectMode)
    {
        if (!currentOwner.HasValue)
        {
            return false;
        }

        return detectMode switch
        {
            MapLogicCpCaptureDetectMode.RedCapture => currentOwner == PlayerTeam.Red && previousOwner != PlayerTeam.Red,
            MapLogicCpCaptureDetectMode.BlueCapture => currentOwner == PlayerTeam.Blue && previousOwner != PlayerTeam.Blue,
            _ => previousOwner != currentOwner,
        };
    }

    private bool EvaluatePlayerTrigger(
        int nodeIndex,
        MapLogicNode node,
        PlayerTriggerEvaluationContext? context)
    {
        var occupied = IsPlayerTriggerOccupied(node, context);
        if (node.SignalMode == MapLogicSignalMode.Latch)
        {
            if (_playerTriggerStates is not null)
            {
                _playerTriggerStates[nodeIndex].WasOccupied = occupied;
            }

            return occupied;
        }

        if (_playerTriggerStates is null)
        {
            return false;
        }

        var state = _playerTriggerStates[nodeIndex];
        var impulse = node.PlayerDetectMode switch
        {
            MapLogicPlayerDetectMode.PlayerExit => state.WasOccupied && !occupied,
            _ => !state.WasOccupied && occupied,
        };
        state.WasOccupied = occupied;
        return impulse;
    }

    private static bool IsPlayerTriggerOccupied(
        MapLogicNode node,
        PlayerTriggerEvaluationContext? context)
    {
        if (context is not PlayerTriggerEvaluationContext activeContext)
        {
            return false;
        }

        var zoneIndices = node.PlayerTriggerZoneRoomObjectIndices;
        if (zoneIndices.Length == 0)
        {
            if (node.PlayerTriggerRoomObjectIndex < 0)
            {
                return false;
            }

            zoneIndices = [node.PlayerTriggerRoomObjectIndex];
        }

        for (var zoneIndex = 0; zoneIndex < zoneIndices.Length; zoneIndex += 1)
        {
            var roomObjectIndex = zoneIndices[zoneIndex];
            if (roomObjectIndex < 0
                || roomObjectIndex >= activeContext.RoomObjects.Count
                || !activeContext.IsRoomObjectActive(roomObjectIndex))
            {
                continue;
            }

            var marker = activeContext.RoomObjects[roomObjectIndex];
            if (PlayerTriggerMetadata.AnyMatchingPlayerInside(
                    marker,
                    node.PlayerTriggerTeamFilter,
                    activeContext.Players,
                    node.PlayerTriggerIntelCarriersOnly))
            {
                return true;
            }
        }

        return false;
    }

    public void ResetCpTriggerStates(IReadOnlyList<ControlPointState> controlPoints)
    {
        if (_cpTriggerStates is null)
        {
            return;
        }

        for (var index = 0; index < _nodes.Length; index += 1)
        {
            var node = _nodes[index];
            if (node.Kind != MapLogicNodeKind.CpTrigger)
            {
                continue;
            }

            var state = _cpTriggerStates[index];
            state.PreviousOwner = MapLogicMetadata.TryGetControlPointOwner(
                controlPoints,
                node.LinkedControlPointIndex,
                out var owner)
                ? owner
                : null;
        }
    }

    public void ResetPlayerTriggerStates(PlayerTriggerEvaluationContext? context = null)
    {
        if (_playerTriggerStates is null)
        {
            return;
        }

        for (var index = 0; index < _nodes.Length; index += 1)
        {
            var node = _nodes[index];
            if (node.Kind != MapLogicNodeKind.PlayerTrigger)
            {
                continue;
            }

            _playerTriggerStates[index].WasOccupied = IsPlayerTriggerOccupied(node, context);
        }
    }

    public void ResetIntelTriggerStates(IntelTriggerEvaluationContext context)
    {
        if (_intelTriggerStates is null)
        {
            return;
        }

        for (var index = 0; index < _nodes.Length; index += 1)
        {
            var node = _nodes[index];
            if (node.Kind != MapLogicNodeKind.IntelTrigger)
            {
                continue;
            }

            var state = _intelTriggerStates[index];
            state.PreviousRedPhase = IntelTriggerMetadata.GetPhase(context.RedIntel);
            state.PreviousBluePhase = IntelTriggerMetadata.GetPhase(context.BlueIntel);
        }
    }

    public void EvaluateIntelTriggers(IntelTriggerEvaluationContext context)
    {
        if (_intelTriggerStates is null)
        {
            return;
        }

        for (var orderIndex = 0; orderIndex < _evaluationOrder.Length; orderIndex += 1)
        {
            var nodeIndex = _evaluationOrder[orderIndex];
            var node = _nodes[nodeIndex];
            if (node.Kind != MapLogicNodeKind.IntelTrigger)
            {
                continue;
            }

            _outputs[nodeIndex] = node.SignalMode == MapLogicSignalMode.Latch
                ? EvaluateIntelLatch(node, context)
                : EvaluateIntelImpulse(nodeIndex, node, context);
        }

        EvaluateCombinatorialPropagators();
    }

    private static bool EvaluateIntelLatch(MapLogicNode node, IntelTriggerEvaluationContext context)
    {
        bool Matches(TeamIntelligenceState intel)
        {
            return IntelTriggerMetadata.MatchesLatchState(intel, node.IntelTriggerLatchState);
        }

        return node.IntelTriggerIntelFilter switch
        {
            IntelTriggerIntelFilter.Red => Matches(context.RedIntel),
            IntelTriggerIntelFilter.Blue => Matches(context.BlueIntel),
            _ => Matches(context.RedIntel) || Matches(context.BlueIntel),
        };
    }

    private bool EvaluateIntelImpulse(int nodeIndex, MapLogicNode node, IntelTriggerEvaluationContext context)
    {
        var state = _intelTriggerStates![nodeIndex];
        var impulse = false;

        if (node.IntelTriggerIntelFilter is IntelTriggerIntelFilter.Any or IntelTriggerIntelFilter.Red)
        {
            var current = IntelTriggerMetadata.GetPhase(context.RedIntel);
            if (IntelTriggerMetadata.DetectImpulseEdge(
                    state.PreviousRedPhase,
                    current,
                    node.IntelTriggerOnPickup,
                    node.IntelTriggerOnDrop,
                    node.IntelTriggerOnCapture,
                    node.IntelTriggerOnReset))
            {
                impulse = true;
            }

            state.PreviousRedPhase = current;
        }

        if (node.IntelTriggerIntelFilter is IntelTriggerIntelFilter.Any or IntelTriggerIntelFilter.Blue)
        {
            var current = IntelTriggerMetadata.GetPhase(context.BlueIntel);
            if (IntelTriggerMetadata.DetectImpulseEdge(
                    state.PreviousBluePhase,
                    current,
                    node.IntelTriggerOnPickup,
                    node.IntelTriggerOnDrop,
                    node.IntelTriggerOnCapture,
                    node.IntelTriggerOnReset))
            {
                impulse = true;
            }

            state.PreviousBluePhase = current;
        }

        return impulse;
    }

    public void ResetDamageTriggerStates(DamageTriggerEvaluationContext? context = null)
    {
        if (_damageTriggerStates is null)
        {
            return;
        }

        for (var index = 0; index < _nodes.Length; index += 1)
        {
            var node = _nodes[index];
            if (node.Kind != MapLogicNodeKind.DamageTrigger)
            {
                continue;
            }

            var state = _damageTriggerStates[index];
            state.PreviousHealthRatio = context?.GetHealthRatio(node.DamageableRoomObjectIndex) ?? 1f;
            state.AnyDamagePulseRemainingSeconds = 0f;
        }
    }

    public void EvaluateDamageTriggers(DamageTriggerEvaluationContext context)
    {
        if (_damageTriggerStates is null)
        {
            return;
        }

        for (var orderIndex = 0; orderIndex < _evaluationOrder.Length; orderIndex += 1)
        {
            var nodeIndex = _evaluationOrder[orderIndex];
            var node = _nodes[nodeIndex];
            if (node.Kind != MapLogicNodeKind.DamageTrigger)
            {
                continue;
            }

            var state = _damageTriggerStates[nodeIndex];
            var currentRatio = context.GetHealthRatio(node.DamageableRoomObjectIndex);
            var previousRatio = state.PreviousHealthRatio;
            var impulse = node.SignalMode == MapLogicSignalMode.Impulse;
            if (node.TriggerOnHeal)
            {
                _outputs[nodeIndex] = impulse
                    && previousRatio < 0.9999f
                    && currentRatio >= 0.9999f;
            }
            else if (node.TriggerWhenDestroyed)
            {
                _outputs[nodeIndex] = impulse
                    ? previousRatio > 0.0001f && currentRatio <= 0.0001f
                    : currentRatio <= 0.0001f;
            }
            else if (node.TriggerBelowThreshold)
            {
                var threshold = node.TriggerBelowPercent / 100f;
                _outputs[nodeIndex] = impulse
                    ? previousRatio > threshold && currentRatio <= threshold
                    : currentRatio <= threshold;
            }
            else if (node.TriggerOnAnyDamage)
            {
                if (impulse)
                {
                    _outputs[nodeIndex] = currentRatio < previousRatio - 0.0001f;
                }
                else if (currentRatio < previousRatio - 0.0001f)
                {
                    state.AnyDamagePulseRemainingSeconds = Math.Max(0f, node.TrueTimeSeconds);
                    _outputs[nodeIndex] = true;
                }
                else
                {
                    _outputs[nodeIndex] = state.AnyDamagePulseRemainingSeconds > 0f;
                }
            }
            else
            {
                _outputs[nodeIndex] = false;
            }

            state.PreviousHealthRatio = currentRatio;
        }

        EvaluateCombinatorialPropagators();
    }

    public void EvaluateCombinatorialPropagators()
    {
        if (_nodes.Length == 0)
        {
            return;
        }

        for (var orderIndex = 0; orderIndex < _evaluationOrder.Length; orderIndex += 1)
        {
            var nodeIndex = _evaluationOrder[orderIndex];
            var node = _nodes[nodeIndex];
            _outputs[nodeIndex] = node.Kind switch
            {
                MapLogicNodeKind.Gate => MapLogicMetadata.EvaluateGate(
                    node.GateType,
                    ReadInput(node.InputNodeIndex1),
                    ReadInput(node.InputNodeIndex2)),
                MapLogicNodeKind.Not => !ReadInput(node.InputNodeIndex),
                MapLogicNodeKind.RisingEdge => EvaluateRisingEdge(nodeIndex, node),
                MapLogicNodeKind.Latch => EvaluateLatch(nodeIndex, node),
                _ => _outputs[nodeIndex],
            };
        }
    }

    public void AdvanceDamageTriggers(float deltaSeconds)
    {
        if (_damageTriggerStates is null || deltaSeconds <= 0f)
        {
            return;
        }

        for (var orderIndex = 0; orderIndex < _evaluationOrder.Length; orderIndex += 1)
        {
            var nodeIndex = _evaluationOrder[orderIndex];
            var node = _nodes[nodeIndex];
            if (node.Kind != MapLogicNodeKind.DamageTrigger
                || !node.TriggerOnAnyDamage
                || node.SignalMode == MapLogicSignalMode.Impulse)
            {
                continue;
            }

            var state = _damageTriggerStates[nodeIndex];
            if (state.AnyDamagePulseRemainingSeconds <= 0f)
            {
                continue;
            }

            state.AnyDamagePulseRemainingSeconds = Math.Max(
                0f,
                state.AnyDamagePulseRemainingSeconds - deltaSeconds);
            if (state.AnyDamagePulseRemainingSeconds <= 0f)
            {
                _outputs[nodeIndex] = false;
                EvaluateCombinatorialPropagators();
            }
        }
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
            state.Output = false;
            state.PendingOutput = false;
            state.RemainingSeconds = 0f;
            state.IsDelayActive = false;
            state.PreviousInput = false;
            state.ClearImpulseOnNextAdvance = false;
            if (node.TriggerOnStart)
            {
                ScheduleTimerTransition(state, targetOutput: true, node.CountdownSeconds, node.DelayedTrue);
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
            if (node.SignalMode == MapLogicSignalMode.Impulse && state.ClearImpulseOnNextAdvance)
            {
                state.Output = false;
                state.ClearImpulseOnNextAdvance = false;
            }

            var input = ReadInput(node.InputNodeIndex);
            if (input != state.PreviousInput)
            {
                if (node.SignalMode == MapLogicSignalMode.Impulse)
                {
                    if (input)
                    {
                        ScheduleTimerTransition(state, true, node.CountdownSeconds, node.DelayedTrue);
                    }
                }
                else
                {
                    var useDelay = input ? node.DelayedTrue : node.DelayedFalse;
                    ScheduleTimerTransition(state, input, node.CountdownSeconds, useDelay);
                }

                state.PreviousInput = input;
            }

            if (state.IsDelayActive)
            {
                if (deltaSeconds > 0f)
                {
                    state.RemainingSeconds -= deltaSeconds;
                }

                if (state.RemainingSeconds <= 0f)
                {
                    state.RemainingSeconds = 0f;
                    state.IsDelayActive = false;
                    if (node.SignalMode == MapLogicSignalMode.Impulse && state.PendingOutput)
                    {
                        state.Output = true;
                    }
                    else
                    {
                        state.Output = state.PendingOutput;
                    }
                }
            }

            if (node.SignalMode == MapLogicSignalMode.Impulse && state.Output)
            {
                state.ClearImpulseOnNextAdvance = true;
            }

            _outputs[nodeIndex] = state.Output;
        }
    }

    public void ResetOscillatorStates()
    {
        if (_oscillatorStates is null)
        {
            return;
        }

        for (var index = 0; index < _nodes.Length; index += 1)
        {
            var node = _nodes[index];
            if (node.Kind != MapLogicNodeKind.Oscillator)
            {
                continue;
            }

            var state = _oscillatorStates[index];
            state.Output = false;
            state.IsRunning = false;
            state.PhaseIsTrue = false;
            state.PhaseRemainingSeconds = 0f;
            state.PreviousStartWhen = false;
            state.AwaitingStartWhenBaseline = !node.Autostart;
            state.ClearImpulseOnNextAdvance = false;
            if (node.Autostart)
            {
                if (node.SignalMode == MapLogicSignalMode.Impulse)
                {
                    StartImpulseOscillator(node, state);
                }
                else
                {
                    StartOscillator(node, state);
                }
            }
        }
    }

    public void AdvanceOscillators(float deltaSeconds)
    {
        if (_oscillatorStates is null)
        {
            return;
        }

        for (var orderIndex = 0; orderIndex < _evaluationOrder.Length; orderIndex += 1)
        {
            var nodeIndex = _evaluationOrder[orderIndex];
            var node = _nodes[nodeIndex];
            if (node.Kind != MapLogicNodeKind.Oscillator)
            {
                continue;
            }

            var state = _oscillatorStates[nodeIndex];
            var startWhen = ReadInput(node.StartWhenNodeIndex);
            var endWhen = ReadInput(node.EndWhenNodeIndex);

            if (node.SignalMode == MapLogicSignalMode.Impulse)
            {
                AdvanceImpulseOscillator(node, state, deltaSeconds, startWhen, endWhen);
                _outputs[nodeIndex] = state.Output;
                continue;
            }

            if (state.IsRunning && endWhen)
            {
                StopOscillator(state);
            }
            else if (!state.IsRunning && !node.Autostart)
            {
                if (state.AwaitingStartWhenBaseline)
                {
                    state.PreviousStartWhen = startWhen;
                    state.AwaitingStartWhenBaseline = false;
                }
                else if (startWhen && !state.PreviousStartWhen)
                {
                    StartOscillator(node, state);
                }
            }

            state.PreviousStartWhen = startWhen;

            if (state.IsRunning && deltaSeconds > 0f)
            {
                state.PhaseRemainingSeconds -= deltaSeconds;
                var phaseAdvances = 0;
                while (state.IsRunning
                    && state.PhaseRemainingSeconds <= 0f
                    && phaseAdvances < 32)
                {
                    phaseAdvances += 1;
                    var overshoot = state.PhaseRemainingSeconds;
                    AdvanceOscillatorPhase(node, state, overshoot);
                }
            }

            if (!state.IsRunning)
            {
                state.Output = false;
            }

            _outputs[nodeIndex] = state.Output;
        }
    }

    private static void StartOscillator(MapLogicNode node, MapLogicOscillatorNodeState state)
    {
        state.IsRunning = true;
        state.AwaitingStartWhenBaseline = false;
        state.PhaseIsTrue = node.InitialValue;
        state.Output = state.PhaseIsTrue;
        state.PhaseRemainingSeconds = GetOscillatorPhaseDuration(node, state.PhaseIsTrue);
    }

    private static void StartImpulseOscillator(MapLogicNode node, MapLogicOscillatorNodeState state)
    {
        state.IsRunning = true;
        state.AwaitingStartWhenBaseline = false;
        state.PhaseIsTrue = false;
        state.Output = true;
        state.PhaseRemainingSeconds = Math.Max(0f, node.SignalPeriodSeconds);
    }

    private static void AdvanceImpulseOscillator(
        MapLogicNode node,
        MapLogicOscillatorNodeState state,
        float deltaSeconds,
        bool startWhen,
        bool endWhen)
    {
        if (state.ClearImpulseOnNextAdvance)
        {
            state.Output = false;
            state.ClearImpulseOnNextAdvance = false;
        }

        if (state.IsRunning && endWhen)
        {
            StopOscillator(state);
            state.ClearImpulseOnNextAdvance = false;
            return;
        }

        if (!state.IsRunning && !node.Autostart)
        {
            if (state.AwaitingStartWhenBaseline)
            {
                state.PreviousStartWhen = startWhen;
                state.AwaitingStartWhenBaseline = false;
            }
            else if (startWhen && !state.PreviousStartWhen)
            {
                StartImpulseOscillator(node, state);
            }
        }

        state.PreviousStartWhen = startWhen;

        if (state.IsRunning && deltaSeconds > 0f)
        {
            state.PhaseRemainingSeconds -= deltaSeconds;
            if (state.PhaseRemainingSeconds <= 0f)
            {
                state.Output = true;
                state.PhaseRemainingSeconds = Math.Max(0f, node.SignalPeriodSeconds);
            }
        }

        if (!state.IsRunning)
        {
            state.Output = false;
        }
        else if (state.Output)
        {
            state.ClearImpulseOnNextAdvance = true;
        }
    }

    private static void StopOscillator(MapLogicOscillatorNodeState state)
    {
        state.IsRunning = false;
        state.Output = false;
        state.PhaseIsTrue = false;
        state.PhaseRemainingSeconds = 0f;
        state.AwaitingStartWhenBaseline = true;
    }

    private static void AdvanceOscillatorPhase(
        MapLogicNode node,
        MapLogicOscillatorNodeState state,
        float overshootSeconds)
    {
        state.PhaseIsTrue = !state.PhaseIsTrue;
        state.Output = state.PhaseIsTrue;
        var duration = GetOscillatorPhaseDuration(node, state.PhaseIsTrue);
        state.PhaseRemainingSeconds = duration <= 0f ? 0f : duration + overshootSeconds;
    }

    private static float GetOscillatorPhaseDuration(MapLogicNode node, bool phaseIsTrue)
    {
        return phaseIsTrue ? node.TrueTimeSeconds : node.FalseTimeSeconds;
    }

    private static void ScheduleTimerTransition(
        MapLogicTimerNodeState state,
        bool targetOutput,
        float delaySeconds,
        bool useDelay)
    {
        state.PendingOutput = targetOutput;
        if (!useDelay || delaySeconds <= 0f)
        {
            state.Output = targetOutput;
            state.RemainingSeconds = 0f;
            state.IsDelayActive = false;
            return;
        }

        state.RemainingSeconds = delaySeconds;
        state.IsDelayActive = true;
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
                definition.DelayedTrue,
                definition.DelayedFalse,
                definition.TrueTimeSeconds,
                definition.FalseTimeSeconds,
                definition.InitialValue,
                definition.Autostart,
                -1,
                -1,
                -1,
                definition.PlayerTriggerRoomObjectIndex,
                definition.PlayerTriggerZoneRoomObjectIndices,
                definition.PlayerTriggerTeamFilter,
                definition.PlayerTriggerIntelCarriersOnly,
                definition.DamageableRoomObjectIndex,
                definition.TriggerBelowPercent,
                definition.TriggerBelowThreshold,
                definition.TriggerOnAnyDamage,
                definition.TriggerOnHeal,
                definition.TriggerWhenDestroyed,
                definition.SignalMode,
                definition.CpCaptureDetectMode,
                definition.PlayerDetectMode,
                definition.SignalPeriodSeconds,
                definition.IntelTriggerIntelFilter,
                definition.IntelTriggerLatchState,
                definition.IntelTriggerOnPickup,
                definition.IntelTriggerOnDrop,
                definition.IntelTriggerOnCapture,
                definition.IntelTriggerOnReset));
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
                baseNode.DelayedTrue,
                baseNode.DelayedFalse,
                baseNode.TrueTimeSeconds,
                baseNode.FalseTimeSeconds,
                baseNode.InitialValue,
                baseNode.Autostart,
                ResolveInput(definition.StartWhenRef, keyToIndex),
                ResolveInput(definition.EndWhenRef, keyToIndex),
                ResolveInput(definition.ResetRef, keyToIndex),
                baseNode.PlayerTriggerRoomObjectIndex,
                baseNode.PlayerTriggerZoneRoomObjectIndices,
                baseNode.PlayerTriggerTeamFilter,
                baseNode.PlayerTriggerIntelCarriersOnly,
                baseNode.DamageableRoomObjectIndex,
                baseNode.TriggerBelowPercent,
                baseNode.TriggerBelowThreshold,
                baseNode.TriggerOnAnyDamage,
                baseNode.TriggerOnHeal,
                baseNode.TriggerWhenDestroyed,
                baseNode.SignalMode,
                baseNode.CpCaptureDetectMode,
                baseNode.PlayerDetectMode,
                baseNode.SignalPeriodSeconds,
                baseNode.IntelTriggerIntelFilter,
                baseNode.IntelTriggerLatchState,
                baseNode.IntelTriggerOnPickup,
                baseNode.IntelTriggerOnDrop,
                baseNode.IntelTriggerOnCapture,
                baseNode.IntelTriggerOnReset);
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

        if (node.StartWhenNodeIndex >= 0)
        {
            Visit(node.StartWhenNodeIndex, nodes, visited, visiting, order);
        }

        if (node.EndWhenNodeIndex >= 0)
        {
            Visit(node.EndWhenNodeIndex, nodes, visited, visiting, order);
        }

        if (node.ResetNodeIndex >= 0)
        {
            Visit(node.ResetNodeIndex, nodes, visited, visiting, order);
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

    public bool DelayedTrue { get; init; } = true;

    public bool DelayedFalse { get; init; } = true;

    public float TrueTimeSeconds { get; init; } = 1f;

    public float FalseTimeSeconds { get; init; } = 1f;

    public bool InitialValue { get; init; } = true;

    public bool Autostart { get; init; }

    public string? StartWhenRef { get; init; }

    public string? EndWhenRef { get; init; }

    public string? ResetRef { get; init; }

    public int PlayerTriggerRoomObjectIndex { get; init; } = -1;

    public int[] PlayerTriggerZoneRoomObjectIndices { get; init; } = [];

    public PlayerTriggerTeamFilter PlayerTriggerTeamFilter { get; init; } = PlayerTriggerTeamFilter.Any;

    public bool PlayerTriggerIntelCarriersOnly { get; init; }

    public int DamageableRoomObjectIndex { get; init; } = -1;

    public int TriggerBelowPercent { get; init; } = DamageTriggerMetadata.DefaultTriggerBelowPercent;

    public bool TriggerBelowThreshold { get; init; }

    public bool TriggerOnAnyDamage { get; init; }

    public bool TriggerOnHeal { get; init; }

    public bool TriggerWhenDestroyed { get; init; }

    public MapLogicSignalMode SignalMode { get; init; } = MapLogicSignalMode.Latch;

    public MapLogicCpCaptureDetectMode CpCaptureDetectMode { get; init; } = MapLogicCpCaptureDetectMode.AnyCapture;

    public MapLogicPlayerDetectMode PlayerDetectMode { get; init; } = MapLogicPlayerDetectMode.PlayerEnter;

    public float SignalPeriodSeconds { get; init; } = MapLogicSignalMetadata.DefaultPeriodSeconds;

    public IntelTriggerIntelFilter IntelTriggerIntelFilter { get; init; } = IntelTriggerIntelFilter.Any;

    public IntelTriggerLatchState IntelTriggerLatchState { get; init; } = IntelTriggerLatchState.AtBase;

    public bool IntelTriggerOnPickup { get; init; }

    public bool IntelTriggerOnDrop { get; init; }

    public bool IntelTriggerOnCapture { get; init; }

    public bool IntelTriggerOnReset { get; init; }
}
