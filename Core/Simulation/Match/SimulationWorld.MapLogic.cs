using System;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private bool[] _logicActivatorStartApplied = [];
    private MapLogicActivatorRuntimeState _logicActivatorRuntimeState = new();

    private ulong _mapLogicControlPointInputSignature;

    public void EvaluateMapLogicGraph(bool resetStatefulNodes = true)
    {
        RefreshMapLogicRuntime(force: true, resetStatefulNodes);
    }

    public void RefreshMapLogicRuntimeIfControlPointInputsChanged()
    {
        RefreshMapLogicRuntime(force: false);
    }

    /// <summary>
    /// Keeps combinatorial logic and timers aligned with authoritative control point state
    /// while connected (client prediction mode). Gameplay outcomes remain server-authoritative.
    /// </summary>
    public void AdvanceAuthoritativeMapLogicRuntime()
    {
        if (!ClientPredictionMode)
        {
            return;
        }

        if (!Level.LogicGraph.HasNodes && !Level.LogicActivators.HasActivators)
        {
            return;
        }

        RefreshMapLogicRuntimeIfControlPointInputsChanged();
        EvaluateMapLogicPlayerTriggersIfNeeded();
        EvaluateMapLogicDamageTriggersIfNeeded();
        TickMapLogicTimers();
    }

    public void SyncMapLogicRuntimeFromAuthoritativeControlPoints(bool newRound)
    {
        if (newRound)
        {
            EvaluateMapLogicGraph();
            return;
        }

        RefreshMapLogicRuntimeIfControlPointInputsChanged();
    }

    public void TickMapLogicTimers()
    {
        EvaluateMapLogicPlayerTriggersIfNeeded();
        ApplyDamageableZoneHealWhenSignals();
        EvaluateMapLogicDamageTriggersIfNeeded();

        var deltaSeconds = (float)Config.FixedDeltaSeconds;
        if (Level.LogicGraph.HasDamageTriggers)
        {
            Level.LogicGraph.AdvanceDamageTriggers(deltaSeconds);
            if (Level.LogicActivators.HasActivators)
            {
                ApplyMapLogicActivators();
            }
        }

        if (!Level.LogicGraph.HasTimers && !Level.LogicGraph.HasOscillators)
        {
            return;
        }

        if (Level.LogicGraph.HasTimers)
        {
            Level.LogicGraph.AdvanceTimers(deltaSeconds);
        }

        if (Level.LogicGraph.HasOscillators)
        {
            Level.LogicGraph.AdvanceOscillators(deltaSeconds);
        }

        if (Level.LogicActivators.HasActivators)
        {
            ApplyMapLogicActivators();
        }
    }

    private void EvaluateMapLogicPlayerTriggersIfNeeded()
    {
        if (!Level.LogicGraph.HasPlayerTriggers)
        {
            return;
        }

        Level.LogicGraph.EvaluateCombinatorial(_controlPoints, CreatePlayerTriggerEvaluationContext());
        ApplyMapLogicActivators();
    }

    private PlayerTriggerEvaluationContext CreatePlayerTriggerEvaluationContext()
    {
        return new PlayerTriggerEvaluationContext(
            EnumerateSimulatedPlayers(),
            Level.RoomObjects,
            Level.IsRoomObjectActive);
    }



    private void RefreshMapLogicRuntime(bool force, bool resetStatefulNodes = true)

    {

        if (!Level.LogicGraph.HasNodes && !Level.LogicActivators.HasActivators)

        {

            return;

        }



        var graph = Level.LogicGraph;
        var signature = ComputeMapLogicControlPointInputSignature();

        if (!force
            && !graph.HasPlayerTriggers
            && !graph.HasDamageTriggers
            && signature == _mapLogicControlPointInputSignature)
        {
            return;
        }

        _mapLogicControlPointInputSignature = signature;

        if (force && resetStatefulNodes)
        {
            ResetMapLogicActivatorRuntime();
            ResetRoomObjectLogicActiveMask();
            ResetDamageableZoneHealth();
        }

        if (graph.HasNodes)
        {
            if (force && resetStatefulNodes)
            {
                graph.ResetCpTriggerStates(_controlPoints);
                graph.ResetPlayerTriggerStates(CreatePlayerTriggerEvaluationContext());
                graph.ResetTimerStates();
                graph.ResetOscillatorStates();
                graph.ResetDamageTriggerStates(CreateDamageTriggerEvaluationContext());
                graph.ResetRisingEdgeStates();
                graph.ResetLatchStates();
            }

            graph.EvaluateCombinatorial(_controlPoints, CreatePlayerTriggerEvaluationContext());
            ApplyDamageableZoneHealWhenSignals();
            graph.EvaluateDamageTriggers(CreateDamageTriggerEvaluationContext());
            ApplyControlPointLogicLockTriggers();

            if (force)
            {
                graph.AdvanceTimers(0f);
                graph.AdvanceOscillators(0f);
            }
        }

        ApplyMapLogicActivators();
    }

    private void ApplyControlPointLogicLockTriggers()
    {
        if (!Level.ControlPointSettings.OverrideInitialOwnership || _controlPoints.Count == 0)
        {
            return;
        }

        for (var index = 0; index < _controlPoints.Count; index += 1)
        {
            var point = _controlPoints[index];
            var isLocked = point.IsLocked;
            ControlPointLockDependencyMetadata.ApplyMapLockTriggers(
                point.Marker.LockRules,
                _controlPoints,
                Level.LogicGraph,
                ref isLocked);
            point.IsLocked = isLocked;
        }
    }

    private void ResetMapLogicActivatorRuntime()
    {
        if (_logicActivatorStartApplied.Length > 0)
        {
            Array.Clear(_logicActivatorStartApplied, 0, _logicActivatorStartApplied.Length);
        }

        _logicActivatorRuntimeState.Reset();
    }

    private void ResetRoomObjectLogicActiveMask()
    {
        if (Level.RoomObjectLogicActiveMask.Length > 0)
        {
            Array.Fill(Level.RoomObjectLogicActiveMask, true);
        }
    }



    private ulong ComputeMapLogicControlPointInputSignature()

    {

        if (_controlPoints.Count == 0)

        {

            return 0;

        }



        var hash = 17ul;

        for (var index = 0; index < _controlPoints.Count; index += 1)

        {

            var point = _controlPoints[index];

            var logicalIndex = 0;

            if (ControlPointMarkerIndex.TryGetIndex(point.Marker, out var parsedIndex))

            {

                logicalIndex = parsedIndex;

            }



            hash = unchecked((hash * 397) + (ulong)(uint)logicalIndex);

            hash = unchecked((hash * 397) + (ulong)(uint)(point.Team.HasValue ? (int)point.Team.Value + 1 : 0));

        }



        return hash;

    }



    private void ApplyMapLogicActivators()

    {

        if (!Level.LogicActivators.HasActivators)

        {

            return;

        }



        if (_logicActivatorStartApplied.Length != Level.LogicActivators.Activators.Count)

        {

            _logicActivatorStartApplied = new bool[Level.LogicActivators.Activators.Count];

        }



        _logicActivatorRuntimeState.EnsureActivatorCount(Level.LogicActivators.Activators.Count);
        MapLogicActivatorRuntime.Apply(
            Level.LogicGraph,
            Level.LogicActivators,
            Level.RoomObjectLogicActiveMask,
            _logicActivatorStartApplied,
            _logicActivatorRuntimeState,
            Level.RoomObjects);
    }

}


