using System.Collections.Generic;

namespace OpenGarrison.Core;

public enum MapLogicActivatorBehavior
{
    Disable,
    Enable,
    Toggle,
}

public sealed class MapLogicActivator
{
    public MapLogicActivator(
        int inputNodeIndex,
        int targetRoomObjectIndex,
        MapLogicActivatorBehavior behavior,
        bool activateOnStart,
        int nodePriority = 0)
        : this(inputNodeIndex, targetRoomObjectIndex, behavior, activateOnStart, nodePriority, -1)
    {
    }

    public MapLogicActivator(
        int inputNodeIndex,
        int targetRoomObjectIndex,
        MapLogicActivatorBehavior behavior,
        bool activateOnStart,
        int nodePriority,
        int externalPulseNodeIndex)
    {
        InputNodeIndex = inputNodeIndex;
        TargetRoomObjectIndex = targetRoomObjectIndex;
        Behavior = behavior;
        ActivateOnStart = activateOnStart;
        NodePriority = nodePriority;
        ExternalPulseNodeIndex = externalPulseNodeIndex;
    }

    public int InputNodeIndex { get; }

    public int TargetRoomObjectIndex { get; }

    public MapLogicActivatorBehavior Behavior { get; }

    public bool ActivateOnStart { get; }

    public int NodePriority { get; }

    public int ExternalPulseNodeIndex { get; }
}

public sealed class MapLogicActivatorSet
{
    public static MapLogicActivatorSet Empty { get; } = new(Array.Empty<MapLogicActivator>());

    public MapLogicActivatorSet(IReadOnlyList<MapLogicActivator> activators)
    {
        Activators = activators;
    }

    public IReadOnlyList<MapLogicActivator> Activators { get; }

    public bool HasActivators => Activators.Count > 0;
}
