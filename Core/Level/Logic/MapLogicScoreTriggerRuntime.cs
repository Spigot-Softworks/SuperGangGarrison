namespace OpenGarrison.Core;

internal static class MapLogicScoreTriggerRuntime
{
    public static void Apply(
        SimulationWorld world,
        MapLogicGraph graph,
        MapLogicScoreTriggerSet scoreTriggers,
        MapLogicActivatorRuntimeState runtimeState)
    {
        if (world.ClientPredictionMode
            || world.MatchState.IsEnded
            || world.MatchRules.Mode != GameModeKind.Scr
            || !scoreTriggers.HasTriggers)
        {
            return;
        }

        runtimeState.EnsureActivatorCount(scoreTriggers.Triggers.Count);
        runtimeState.BeginEvaluationTick();
        for (var index = 0; index < scoreTriggers.Triggers.Count; index += 1)
        {
            var trigger = scoreTriggers.Triggers[index];
            if (trigger.InputNodeIndex < 0)
            {
                continue;
            }

            var currentSignal = graph.GetOutput(trigger.InputNodeIndex);
            if (!runtimeState.RecordSignalTransition(index, currentSignal))
            {
                continue;
            }

            ApplyTrigger(world, trigger);
        }
    }

    private static void ApplyTrigger(SimulationWorld world, MapLogicScoreTrigger trigger)
    {
        var delta = trigger.SignedDelta;
        if (delta == 0)
        {
            return;
        }

        switch (trigger.TeamTarget)
        {
            case MapLogicScoreTeamTarget.Red:
                world.TryModifyTeamScore(PlayerTeam.Red, delta, "logic_score");
                break;
            case MapLogicScoreTeamTarget.Blue:
                world.TryModifyTeamScore(PlayerTeam.Blue, delta, "logic_score");
                break;
            default:
                world.TryModifyTeamScore(PlayerTeam.Red, delta, "logic_score");
                world.TryModifyTeamScore(PlayerTeam.Blue, delta, "logic_score");
                break;
        }
    }
}
