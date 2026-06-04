using System.Collections.Generic;

namespace OpenGarrison.Core;

public enum MapLogicActivatorBehavior
{
    Disable,
    Enable,
}

public sealed class MapLogicActivator
{
    public MapLogicActivator(
        int inputNodeIndex,
        int targetRoomObjectIndex,
        MapLogicActivatorBehavior behavior,
        bool activateOnStart,
        int nodePriority = 0)
    {
        InputNodeIndex = inputNodeIndex;
        TargetRoomObjectIndex = targetRoomObjectIndex;
        Behavior = behavior;
        ActivateOnStart = activateOnStart;
        NodePriority = nodePriority;
    }

    public int InputNodeIndex { get; }

    public int TargetRoomObjectIndex { get; }

    public MapLogicActivatorBehavior Behavior { get; }

    public bool ActivateOnStart { get; }

    public int NodePriority { get; }
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
