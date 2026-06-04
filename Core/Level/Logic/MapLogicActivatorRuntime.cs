using System;

namespace OpenGarrison.Core;

public static class MapLogicActivatorRuntime
{
    private readonly struct ActivatorDecision
    {
        public ActivatorDecision(int priority, bool activeValue)
        {
            Priority = priority;
            ActiveValue = activeValue;
        }

        public int Priority { get; }

        public bool ActiveValue { get; }
    }

    public static void Apply(
        MapLogicGraph graph,
        MapLogicActivatorSet activators,
        bool[] roomObjectActiveMask,
        bool[] activatorStartApplied)
    {
        if (activators.Activators.Count == 0 || roomObjectActiveMask.Length == 0)
        {
            return;
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

            if (!ShouldApplyActivator(graph, activator, activatorIndex, activatorStartApplied))
            {
                continue;
            }

            var decision = new ActivatorDecision(
                activator.NodePriority,
                activator.Behavior == MapLogicActivatorBehavior.Enable);
            ref var winner = ref winners[activator.TargetRoomObjectIndex];
            if (winner is null || IsBetterDecision(decision, winner.Value))
            {
                winner = decision;
            }
        }

        for (var index = 0; index < roomObjectActiveMask.Length; index += 1)
        {
            roomObjectActiveMask[index] = winners[index]?.ActiveValue ?? true;
        }
    }

    private static bool IsBetterDecision(ActivatorDecision candidate, ActivatorDecision current)
    {
        if (candidate.Priority > current.Priority)
        {
            return true;
        }

        if (candidate.Priority < current.Priority)
        {
            return false;
        }

        return candidate.ActiveValue && !current.ActiveValue;
    }

    private static bool ShouldApplyActivator(
        MapLogicGraph graph,
        MapLogicActivator activator,
        int activatorIndex,
        bool[] activatorStartApplied)
    {
        if (activator.ActivateOnStart)
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
        }

        return activator.InputNodeIndex >= 0 && graph.GetOutput(activator.InputNodeIndex);
    }
}
