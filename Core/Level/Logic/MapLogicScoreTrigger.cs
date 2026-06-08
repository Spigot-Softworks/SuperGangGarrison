using System.Collections.Generic;

namespace OpenGarrison.Core;

public sealed class MapLogicScoreTrigger
{
    public MapLogicScoreTrigger(
        int inputNodeIndex,
        MapLogicScoreTeamTarget teamTarget,
        MapLogicScoreChangeMode changeMode,
        int value,
        int nodePriority = 0)
    {
        InputNodeIndex = inputNodeIndex;
        TeamTarget = teamTarget;
        ChangeMode = changeMode;
        Value = value;
        NodePriority = nodePriority;
    }

    public int InputNodeIndex { get; }

    public MapLogicScoreTeamTarget TeamTarget { get; }

    public MapLogicScoreChangeMode ChangeMode { get; }

    public int Value { get; }

    public int NodePriority { get; }

    public int SignedDelta => ChangeMode == MapLogicScoreChangeMode.Subtract ? -Value : Value;
}

public sealed class MapLogicScoreTriggerSet
{
    public static MapLogicScoreTriggerSet Empty { get; } = new(Array.Empty<MapLogicScoreTrigger>());

    public MapLogicScoreTriggerSet(IReadOnlyList<MapLogicScoreTrigger> triggers)
    {
        Triggers = triggers;
    }

    public IReadOnlyList<MapLogicScoreTrigger> Triggers { get; }

    public bool HasTriggers => Triggers.Count > 0;
}
