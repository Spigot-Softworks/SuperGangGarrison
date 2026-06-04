using System;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private bool[] _logicActivatorStartApplied = [];

    private ulong _mapLogicControlPointInputSignature;

    public void EvaluateMapLogicGraph()
    {
        RefreshMapLogicRuntime(force: true);
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

        if (!Level.LogicGraph.HasTimers)
        {
            return;
        }

        var deltaSeconds = (float)Config.FixedDeltaSeconds;
        Level.LogicGraph.AdvanceTimers(deltaSeconds);

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



    private void RefreshMapLogicRuntime(bool force)

    {

        if (!Level.LogicGraph.HasNodes && !Level.LogicActivators.HasActivators)

        {

            return;

        }



        var graph = Level.LogicGraph;
        var signature = ComputeMapLogicControlPointInputSignature();

        if (!force && !graph.HasPlayerTriggers && signature == _mapLogicControlPointInputSignature)
        {
            return;
        }

        _mapLogicControlPointInputSignature = signature;

        if (force)
        {
            ResetMapLogicActivatorRuntime();
            ResetRoomObjectLogicActiveMask();
        }

        if (graph.HasNodes)
        {
            if (force)
            {
                graph.ResetTimerStates();
            }

            graph.EvaluateCombinatorial(_controlPoints, CreatePlayerTriggerEvaluationContext());

            if (force)
            {
                graph.AdvanceTimers(0f);
            }
        }

        ApplyMapLogicActivators();
    }

    private void ResetMapLogicActivatorRuntime()
    {
        if (_logicActivatorStartApplied.Length > 0)
        {
            Array.Clear(_logicActivatorStartApplied, 0, _logicActivatorStartApplied.Length);
        }
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



        MapLogicActivatorRuntime.Apply(

            Level.LogicGraph,

            Level.LogicActivators,

            Level.RoomObjectLogicActiveMask,

            _logicActivatorStartApplied);

    }

}


