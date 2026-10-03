namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : ISimulationTickHost
{
    void ISimulationTickHost.AdvanceAuthoritativeMapLogicRuntime() => MapLogic.AdvanceAuthoritativeMapLogicRuntime();
    void ISimulationTickHost.AdvanceCivilDefenseTurrets() => Structures.AdvanceCivilDefenseTurrets();
    void ISimulationTickHost.AdvanceFrameCounter() => Frame += 1;
    void ISimulationTickHost.AdvanceMovingPlatforms() => Movement.AdvanceMovingPlatforms();
    bool ISimulationTickHost.AdvancePendingMapChange() => MapLifecycle.AdvancePendingMapChange();
    void ISimulationTickHost.BeginLastToDieStatusEffectsTick() => LastToDieRules.BeginLastToDieStatusEffectsTick();
    bool ISimulationTickHost.ClientPredictionMode => ClientPredictionMode;
    void ISimulationTickHost.CommitLocalInputForTick() => LocalState.PreviousInput = LocalState.Input;
    void ISimulationTickHost.EndLastToDieStatusEffectsTick() => LastToDieRules.EndLastToDieStatusEffectsTick();
    long ISimulationTickHost.Frame => Frame;
    void ISimulationTickHost.RefreshLastToDieMedicLinkProjections() => LastToDieRules.RefreshLastToDieMedicLinkProjections();
    void ISimulationTickHost.TickMapLogicTimersOncePerFrame() => MapLogic.TickMapLogicTimersOncePerFrame();
}
