using System;

namespace OpenGarrison.Core;

internal sealed partial class MapLogicSystem
{


    internal void EvaluateMapLogicGraph(bool resetStatefulNodes = true)
    {
        RefreshMapLogicRuntime(force: true, resetStatefulNodes);
    }

    internal void RefreshMapLogicRuntimeIfControlPointInputsChanged()
    {
        RefreshMapLogicRuntime(force: false);
    }

    /// <summary>
    /// Keeps combinatorial logic and timers aligned with authoritative control point state
    /// while connected (client prediction mode). Gameplay outcomes remain server-authoritative.
    /// </summary>
    internal void AdvanceAuthoritativeMapLogicRuntime()
    {
        if (!_host.ClientPredictionMode)
        {
            return;
        }

        if (_host.Level.LogicGraph.HasNodes || _host.Level.LogicActivators.HasActivators)
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

    internal void SyncMapLogicRuntimeFromAuthoritativeControlPoints(bool newRound)
    {
        if (newRound)
        {
            EvaluateMapLogicGraph();
            return;
        }

        RefreshMapLogicRuntimeIfControlPointInputsChanged();
    }

    internal void TickMapLogicTimers()
    {
        EvaluateMapLogicPlayerTriggersIfNeeded();
        EvaluateMapLogicIntelTriggersIfNeeded();
        ApplyDamageableZoneHealWhenSignals();
        EvaluateMapLogicDamageTriggersIfNeeded();
        EvaluateMapLogicScoreTriggersIfNeeded();

        var deltaSeconds = (float)_host.Config.FixedDeltaSeconds;
        if (_host.Level.LogicGraph.HasDamageTriggers)
        {
            _host.Level.LogicGraph.AdvanceDamageTriggers(deltaSeconds);
            if (_host.Level.LogicActivators.HasActivators)
            {
                ApplyMapLogicActivators();
            }
        }

        if (!_host.Level.LogicGraph.HasTimers && !_host.Level.LogicGraph.HasOscillators)
        {
            return;
        }

        if (_host.Level.LogicGraph.HasTimers)
        {
            _host.Level.LogicGraph.AdvanceTimers(deltaSeconds);
        }

        if (_host.Level.LogicGraph.HasOscillators)
        {
            _host.Level.LogicGraph.AdvanceOscillators(deltaSeconds);
        }

        if (_host.Level.LogicActivators.HasActivators)
        {
            ApplyMapLogicActivators();
        }
    }

    internal void TickMapLogicTimersOncePerFrame()
    {
        if (_host.MapRuntime.LogicTimersLastFrame == _host.Frame)
        {
            return;
        }

        _host.MapRuntime.LogicTimersLastFrame = _host.Frame;
        TickMapLogicTimers();
    }

    internal bool PulseMapLogicNode(int nodeIndex)
    {
        if (!_host.Level.LogicGraph.PulseExternalOutput(nodeIndex))
        {
            return false;
        }

        ApplyMapLogicActivators();
        return true;
    }

    private void EvaluateMapLogicPlayerTriggersIfNeeded()
    {
        if (!_host.Level.LogicGraph.HasPlayerTriggers)
        {
            return;
        }

        _host.Level.LogicGraph.EvaluateCombinatorial(_host.Objectives.ControlPoints.Points, CreatePlayerTriggerEvaluationContext());
        ApplyMapLogicActivators();
    }

    internal void EvaluateMapLogicIntelTriggersIfNeeded()
    {
        if (!_host.Level.LogicGraph.HasIntelTriggers)
        {
            return;
        }

        _host.Level.LogicGraph.EvaluateIntelTriggers(CreateIntelTriggerEvaluationContext());
        ApplyMapLogicActivators();
    }

    private void EvaluateMapLogicScoreTriggersIfNeeded()
    {
        if (!_host.Level.LogicScoreTriggers.HasTriggers)
        {
            return;
        }

        _host.MapRuntime.LogicScoreTriggerRuntimeState.EnsureActivatorCount(_host.Level.LogicScoreTriggers.Triggers.Count);
        MapLogicScoreTriggerRuntime.Apply(
            _host,
            _host.Level.LogicGraph,
            _host.Level.LogicScoreTriggers,
            _host.MapRuntime.LogicScoreTriggerRuntimeState);
    }

    private PlayerTriggerEvaluationContext CreatePlayerTriggerEvaluationContext()
    {
        return new PlayerTriggerEvaluationContext(
            _host.EnumerateSimulatedPlayers(),
            _host.Level.RoomObjects,
            _host.Level.IsRoomObjectActive);
    }

    private IntelTriggerEvaluationContext CreateIntelTriggerEvaluationContext()
    {
        return new IntelTriggerEvaluationContext(_host.RedIntel, _host.BlueIntel);
    }



    private void RefreshMapLogicRuntime(bool force, bool resetStatefulNodes = true)

    {

        if (!_host.Level.LogicGraph.HasNodes && !_host.Level.LogicActivators.HasActivators)

        {

            return;

        }



        var graph = _host.Level.LogicGraph;
        var signature = ComputeMapLogicControlPointInputSignature();

        if (!force
            && !graph.HasPlayerTriggers
            && !graph.HasIntelTriggers
            && !graph.HasDamageTriggers
            && signature == _host.MapRuntime.LogicControlPointInputSignature)
        {
            return;
        }

        _host.MapRuntime.LogicControlPointInputSignature = signature;

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
                graph.ResetCpTriggerStates(_host.Objectives.ControlPoints.Points);
                graph.ResetPlayerTriggerStates(CreatePlayerTriggerEvaluationContext());
                graph.ResetIntelTriggerStates(CreateIntelTriggerEvaluationContext());
                graph.ResetTimerStates();
                graph.ResetOscillatorStates();
                graph.ResetDamageTriggerStates(CreateDamageTriggerEvaluationContext());
                graph.ResetRisingEdgeStates();
                graph.ResetLatchStates();
            }

            graph.EvaluateCombinatorial(_host.Objectives.ControlPoints.Points, CreatePlayerTriggerEvaluationContext());
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
        if (!_host.Level.ControlPointSettings.OverrideInitialOwnership || _host.Objectives.ControlPoints.Points.Count == 0)
        {
            return;
        }

        for (var index = 0; index < _host.Objectives.ControlPoints.Points.Count; index += 1)
        {
            var point = _host.Objectives.ControlPoints.Points[index];
            var isLocked = point.IsLocked;
            ControlPointLockDependencyMetadata.ApplyMapLockTriggers(
                point.Marker.LockRules,
                _host.Objectives.ControlPoints.Points,
                _host.Level.LogicGraph,
                ref isLocked);
            point.IsLocked = isLocked;
        }
    }

    private void ResetMapLogicActivatorRuntime()
    {
        if (_host.MapRuntime.LogicActivatorStartApplied.Length > 0)
        {
            Array.Clear(_host.MapRuntime.LogicActivatorStartApplied, 0, _host.MapRuntime.LogicActivatorStartApplied.Length);
        }

        _host.MapRuntime.LogicActivatorRuntimeState.Reset();
        _host.MapRuntime.LogicScoreTriggerRuntimeState.Reset();
        ResetSpritesheetPlaybackRuntime();
    }

    private void ResetRoomObjectLogicActiveMask()
    {
        if (_host.Level.RoomObjectLogicActiveMask.Length > 0)
        {
            Array.Fill(_host.Level.RoomObjectLogicActiveMask, true);
        }
    }



    private ulong ComputeMapLogicControlPointInputSignature()

    {

        if (_host.Objectives.ControlPoints.Points.Count == 0)

        {

            return 0;

        }



        var hash = 17ul;

        for (var index = 0; index < _host.Objectives.ControlPoints.Points.Count; index += 1)

        {

            var point = _host.Objectives.ControlPoints.Points[index];

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

        if (!_host.Level.LogicActivators.HasActivators)

        {

            return;

        }



        if (_host.MapRuntime.LogicActivatorStartApplied.Length != _host.Level.LogicActivators.Activators.Count)

        {

            _host.MapRuntime.LogicActivatorStartApplied = new bool[_host.Level.LogicActivators.Activators.Count];

        }



        _host.MapRuntime.LogicActivatorRuntimeState.EnsureActivatorCount(_host.Level.LogicActivators.Activators.Count);
        MapLogicActivatorRuntime.Apply(
            _host.Level.LogicGraph,
            _host.Level.LogicActivators,
            _host.Level.RoomObjectLogicActiveMask,
            _host.MapRuntime.LogicActivatorStartApplied,
            _host.MapRuntime.LogicActivatorRuntimeState,
            _host.Level.RoomObjects);
    }

}
