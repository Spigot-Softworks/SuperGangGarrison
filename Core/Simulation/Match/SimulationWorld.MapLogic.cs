using System;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{


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

        if (Level.LogicGraph.HasNodes || Level.LogicActivators.HasActivators)
        {
            RefreshMapLogicRuntimeIfControlPointInputsChanged();
            EvaluateMapLogicPlayerTriggersIfNeeded();
            EvaluateMapLogicIntelTriggersIfNeeded();
            ApplyDamageableZoneHealWhenSignals();
            EvaluateMapLogicDamageTriggersIfNeeded();
            TickMapLogicTimers();
        }

        TickSpritesheetPlayback();
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
        EvaluateMapLogicIntelTriggersIfNeeded();
        ApplyDamageableZoneHealWhenSignals();
        EvaluateMapLogicDamageTriggersIfNeeded();
        EvaluateMapLogicScoreTriggersIfNeeded();

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

    public void TickMapLogicTimersOncePerFrame()
    {
        if (MapRuntime.LogicTimersLastFrame == Frame)
        {
            return;
        }

        MapRuntime.LogicTimersLastFrame = Frame;
        TickMapLogicTimers();
    }

    public bool PulseMapLogicNode(int nodeIndex)
    {
        if (!Level.LogicGraph.PulseExternalOutput(nodeIndex))
        {
            return false;
        }

        ApplyMapLogicActivators();
        return true;
    }

    private void EvaluateMapLogicPlayerTriggersIfNeeded()
    {
        if (!Level.LogicGraph.HasPlayerTriggers)
        {
            return;
        }

        Level.LogicGraph.EvaluateCombinatorial(Objectives.ControlPoints.Points, CreatePlayerTriggerEvaluationContext());
        ApplyMapLogicActivators();
    }

    private void EvaluateMapLogicIntelTriggersIfNeeded()
    {
        if (!Level.LogicGraph.HasIntelTriggers)
        {
            return;
        }

        Level.LogicGraph.EvaluateIntelTriggers(CreateIntelTriggerEvaluationContext());
        ApplyMapLogicActivators();
    }

    private void EvaluateMapLogicScoreTriggersIfNeeded()
    {
        if (!Level.LogicScoreTriggers.HasTriggers)
        {
            return;
        }

        MapRuntime.LogicScoreTriggerRuntimeState.EnsureActivatorCount(Level.LogicScoreTriggers.Triggers.Count);
        MapLogicScoreTriggerRuntime.Apply(
            this,
            Level.LogicGraph,
            Level.LogicScoreTriggers,
            MapRuntime.LogicScoreTriggerRuntimeState);
    }

    private PlayerTriggerEvaluationContext CreatePlayerTriggerEvaluationContext()
    {
        return new PlayerTriggerEvaluationContext(
            EnumerateSimulatedPlayers(),
            Level.RoomObjects,
            Level.IsRoomObjectActive);
    }

    private IntelTriggerEvaluationContext CreateIntelTriggerEvaluationContext()
    {
        return new IntelTriggerEvaluationContext(RedIntel, BlueIntel);
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
            && !graph.HasIntelTriggers
            && !graph.HasDamageTriggers
            && signature == MapRuntime.LogicControlPointInputSignature)
        {
            return;
        }

        MapRuntime.LogicControlPointInputSignature = signature;

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
                graph.ResetCpTriggerStates(Objectives.ControlPoints.Points);
                graph.ResetPlayerTriggerStates(CreatePlayerTriggerEvaluationContext());
                graph.ResetIntelTriggerStates(CreateIntelTriggerEvaluationContext());
                graph.ResetTimerStates();
                graph.ResetOscillatorStates();
                graph.ResetDamageTriggerStates(CreateDamageTriggerEvaluationContext());
                graph.ResetRisingEdgeStates();
                graph.ResetLatchStates();
            }

            graph.EvaluateCombinatorial(Objectives.ControlPoints.Points, CreatePlayerTriggerEvaluationContext());
            graph.EvaluateIntelTriggers(CreateIntelTriggerEvaluationContext());
            ApplyDamageableZoneHealWhenSignals();
            graph.EvaluateDamageTriggers(CreateDamageTriggerEvaluationContext());
            EvaluateMapLogicScoreTriggersIfNeeded();
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
        if (!Level.ControlPointSettings.OverrideInitialOwnership || Objectives.ControlPoints.Points.Count == 0)
        {
            return;
        }

        for (var index = 0; index < Objectives.ControlPoints.Points.Count; index += 1)
        {
            var point = Objectives.ControlPoints.Points[index];
            var isLocked = point.IsLocked;
            ControlPointLockDependencyMetadata.ApplyMapLockTriggers(
                point.Marker.LockRules,
                Objectives.ControlPoints.Points,
                Level.LogicGraph,
                ref isLocked);
            point.IsLocked = isLocked;
        }
    }

    private void ResetMapLogicActivatorRuntime()
    {
        if (MapRuntime.LogicActivatorStartApplied.Length > 0)
        {
            Array.Clear(MapRuntime.LogicActivatorStartApplied, 0, MapRuntime.LogicActivatorStartApplied.Length);
        }

        MapRuntime.LogicActivatorRuntimeState.Reset();
        MapRuntime.LogicScoreTriggerRuntimeState.Reset();
        ResetSpritesheetPlaybackRuntime();
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

        if (Objectives.ControlPoints.Points.Count == 0)

        {

            return 0;

        }



        var hash = 17ul;

        for (var index = 0; index < Objectives.ControlPoints.Points.Count; index += 1)

        {

            var point = Objectives.ControlPoints.Points[index];

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



        if (MapRuntime.LogicActivatorStartApplied.Length != Level.LogicActivators.Activators.Count)

        {

            MapRuntime.LogicActivatorStartApplied = new bool[Level.LogicActivators.Activators.Count];

        }



        MapRuntime.LogicActivatorRuntimeState.EnsureActivatorCount(Level.LogicActivators.Activators.Count);
        MapLogicActivatorRuntime.Apply(
            Level.LogicGraph,
            Level.LogicActivators,
            Level.RoomObjectLogicActiveMask,
            MapRuntime.LogicActivatorStartApplied,
            MapRuntime.LogicActivatorRuntimeState,
            Level.RoomObjects);
    }

}


