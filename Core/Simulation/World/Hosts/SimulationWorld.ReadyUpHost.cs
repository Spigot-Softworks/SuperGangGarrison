namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IReadyUpHost
{
    ObjectiveRulesSystem IReadyUpHost.ObjectiveRules => ObjectiveRules;
    ObjectiveStateStore IReadyUpHost.Objectives => Objectives;
    CompetitiveReadyUpState IReadyUpHost.ReadyUpState => ReadyUpState;
    MapLifecycleSystem IReadyUpHost.MapLifecycle => MapLifecycle;
}
