namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : ISimulationTickHost
{
    long ISimulationTickHost.Frame => Frame;

    bool ISimulationTickHost.ClientPredictionMode => ClientPredictionMode;

    bool ISimulationTickHost.AdvancePendingMapChange() => AdvancePendingMapChange();

    void ISimulationTickHost.AdvanceAuthoritativeMapLogicRuntime() => MapLogic.AdvanceAuthoritativeMapLogicRuntime();

    void ISimulationTickHost.AdvanceMovingPlatforms() => AdvanceMovingPlatforms();

    void ISimulationTickHost.BeginLastToDieStatusEffectsTick() => LastToDieRules.BeginLastToDieStatusEffectsTick();

    void ISimulationTickHost.RefreshLastToDieMedicLinkProjections() => LastToDieRules.RefreshLastToDieMedicLinkProjections();

    void ISimulationTickHost.AdvanceCivilDefenseTurrets() => Structures.AdvanceCivilDefenseTurrets();

    void ISimulationTickHost.EndLastToDieStatusEffectsTick() => LastToDieRules.EndLastToDieStatusEffectsTick();

    void ISimulationTickHost.TickMapLogicTimersOncePerFrame() => MapLogic.TickMapLogicTimersOncePerFrame();

    void ISimulationTickHost.CommitLocalInputForTick() => LocalState.PreviousInput = LocalState.Input;

    void ISimulationTickHost.AdvanceFrameCounter() => Frame += 1;
}
