using System;

namespace OpenGarrison.Core;

public static class MapLogicActivatorRuntime
{
    private readonly struct ActivatorDecision
    {
        public ActivatorDecision(long risingEdgeOrder, int nodePriority, bool activeValue)
        {
            RisingEdgeOrder = risingEdgeOrder;
            NodePriority = nodePriority;
            ActiveValue = activeValue;
        }

        public long RisingEdgeOrder { get; }

        public int NodePriority { get; }

        public bool ActiveValue { get; }
    }

    public static void Apply(
        MapLogicGraph graph,
        MapLogicActivatorSet activators,
        bool[] roomObjectActiveMask,
        bool[] activatorStartApplied,
        MapLogicActivatorRuntimeState? runtimeState = null,
        IReadOnlyList<RoomObjectMarker>? roomObjects = null)
    {
        if (activators.Activators.Count == 0 || roomObjectActiveMask.Length == 0)
        {
            return;
        }

        runtimeState ??= MapLogicActivatorRuntimeState.CreateForActivatorCount(activators.Activators.Count);
        runtimeState.EnsureActivatorCount(activators.Activators.Count);
        runtimeState.EnsureToggleTargetStateCount(roomObjectActiveMask.Length);
        runtimeState.BeginEvaluationTick();
        ApplyToggleActivateOnStart(activators, roomObjectActiveMask, activatorStartApplied, runtimeState);

        Span<bool> currentSignals = activators.Activators.Count <= 256
            ? stackalloc bool[activators.Activators.Count]
            : new bool[activators.Activators.Count];

        for (var activatorIndex = 0; activatorIndex < activators.Activators.Count; activatorIndex += 1)
        {
            var activator = activators.Activators[activatorIndex];
            currentSignals[activatorIndex] = IsActivatorSignalActive(
                graph,
                activator,
                activatorIndex,
                activatorStartApplied,
                runtimeState);
            if (activator.InputNodeIndex >= 0 && graph.GetOutput(activator.InputNodeIndex))
            {
                runtimeState.MarkLogicInputActivated(activatorIndex);
            }

            runtimeState.RecordSignalTransition(activatorIndex, currentSignals[activatorIndex]);
        }

        Span<ActivatorDecision?> winners = roomObjectActiveMask.Length <= 256
            ? stackalloc ActivatorDecision?[roomObjectActiveMask.Length]
            : new ActivatorDecision?[roomObjectActiveMask.Length];

        for (var index = 0; index < roomObjectActiveMask.Length; index += 1)
        {
            winners[index] = null;
        }

        for (var activatorIndex = 0; activatorIndex < activators.Activators.Count; activatorIndex += 1)
        {
            var activator = activators.Activators[activatorIndex];
            if (activator.TargetRoomObjectIndex < 0
                || activator.TargetRoomObjectIndex >= roomObjectActiveMask.Length)
            {
                continue;
            }

            if (!currentSignals[activatorIndex]
                || !runtimeState.TryGetLastRisingEdgeOrder(activatorIndex, out var risingEdgeOrder))
            {
                continue;
            }

            var activeValue = ResolveActivatorActiveValue(activator, activatorIndex, runtimeState);
            runtimeState.SetToggleTargetActive(activator.TargetRoomObjectIndex, activeValue);

            var decision = new ActivatorDecision(
                risingEdgeOrder,
                activator.NodePriority,
                activeValue);
            ref var winner = ref winners[activator.TargetRoomObjectIndex];
            if (winner is null || IsBetterDecision(decision, winner.Value))
            {
                winner = decision;
            }
        }

        for (var index = 0; index < roomObjectActiveMask.Length; index += 1)
        {
            if (winners[index] is ActivatorDecision winner)
            {
                roomObjectActiveMask[index] = winner.ActiveValue;
                if (winner.ActiveValue
                    && roomObjects is not null
                    && AreaExtensionMetadata.IsPrimaryExtendableZone(roomObjects, index))
                {
                    AreaExtensionMetadata.EnableAllExtensionMasks(
                        roomObjects,
                        index,
                        roomObjectActiveMask);
                }

                continue;
            }

            if (runtimeState.TryGetToggleTargetActive(index, out var toggleActive))
            {
                roomObjectActiveMask[index] = toggleActive;
                continue;
            }

            roomObjectActiveMask[index] = true;
        }

        ApplyToggleActivateOnStartHold(activators, roomObjectActiveMask, runtimeState);
    }

    private static void ApplyToggleActivateOnStart(
        MapLogicActivatorSet activators,
        bool[] roomObjectActiveMask,
        bool[] activatorStartApplied,
        MapLogicActivatorRuntimeState runtimeState)
    {
        for (var activatorIndex = 0; activatorIndex < activators.Activators.Count; activatorIndex += 1)
        {
            var activator = activators.Activators[activatorIndex];
            if (activator.Behavior != MapLogicActivatorBehavior.Toggle || !activator.ActivateOnStart)
            {
                continue;
            }

            if (activatorIndex >= activatorStartApplied.Length)
            {
                Array.Resize(ref activatorStartApplied, activatorIndex + 1);
            }

            if (activatorStartApplied[activatorIndex])
            {
                continue;
            }

            activatorStartApplied[activatorIndex] = true;
            runtimeState.BeginToggleActivateOnStart(activatorIndex);
            if (activator.TargetRoomObjectIndex >= 0
                && activator.TargetRoomObjectIndex < roomObjectActiveMask.Length)
            {
                runtimeState.BeginToggleActivateOnStartTarget(activator.TargetRoomObjectIndex);
            }
        }
    }

    private static void ApplyToggleActivateOnStartHold(
        MapLogicActivatorSet activators,
        bool[] roomObjectActiveMask,
        MapLogicActivatorRuntimeState runtimeState)
    {
        for (var activatorIndex = 0; activatorIndex < activators.Activators.Count; activatorIndex += 1)
        {
            var activator = activators.Activators[activatorIndex];
            if (activator.Behavior != MapLogicActivatorBehavior.Toggle
                || activator.TargetRoomObjectIndex < 0
                || activator.TargetRoomObjectIndex >= roomObjectActiveMask.Length
                || !runtimeState.IsToggleActivateOnStartPending(activatorIndex))
            {
                continue;
            }

            runtimeState.SetToggleTargetActive(activator.TargetRoomObjectIndex, activeValue: false);
            roomObjectActiveMask[activator.TargetRoomObjectIndex] = false;
        }
    }

    private static bool ResolveActivatorActiveValue(
        MapLogicActivator activator,
        int activatorIndex,
        MapLogicActivatorRuntimeState runtimeState)
    {
        return activator.Behavior switch
        {
            MapLogicActivatorBehavior.Toggle => runtimeState.ResolveToggleRisingEdgeActiveValue(activatorIndex),
            MapLogicActivatorBehavior.Enable => true,
            _ => false,
        };
    }

    private static bool IsBetterDecision(ActivatorDecision candidate, ActivatorDecision current)
    {
        if (candidate.RisingEdgeOrder > current.RisingEdgeOrder)
        {
            return true;
        }

        if (candidate.RisingEdgeOrder < current.RisingEdgeOrder)
        {
            return false;
        }

        if (candidate.NodePriority > current.NodePriority)
        {
            return true;
        }

        if (candidate.NodePriority < current.NodePriority)
        {
            return false;
        }

        return candidate.ActiveValue && !current.ActiveValue;
    }

    private static bool IsActivatorSignalActive(
        MapLogicGraph graph,
        MapLogicActivator activator,
        int activatorIndex,
        bool[] activatorStartApplied,
        MapLogicActivatorRuntimeState runtimeState)
    {
        if (activator.ActivateOnStart && activator.Behavior != MapLogicActivatorBehavior.Toggle)
        {
            if (activatorIndex >= activatorStartApplied.Length)
            {
                Array.Resize(ref activatorStartApplied, activatorIndex + 1);
            }

            if (!activatorStartApplied[activatorIndex])
            {
                activatorStartApplied[activatorIndex] = true;
                return true;
            }

            if (activator.InputNodeIndex >= 0
                && !runtimeState.HasLogicInputActivated(activatorIndex))
            {
                return true;
            }
        }

        return activator.InputNodeIndex >= 0 && graph.GetOutput(activator.InputNodeIndex);
    }
}
