namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : ISimulationTickHost
{
    long ISimulationTickHost.Frame => Frame;

    bool ISimulationTickHost.ClientPredictionMode => ClientPredictionMode;

    bool ISimulationTickHost.AdvancePendingMapChange() => AdvancePendingMapChange();

    void ISimulationTickHost.AdvanceAuthoritativeMapLogicRuntime() => AdvanceAuthoritativeMapLogicRuntime();

    void ISimulationTickHost.AdvanceMovingPlatforms() => AdvanceMovingPlatforms();

    void ISimulationTickHost.BeginLastToDieStatusEffectsTick() => BeginLastToDieStatusEffectsTick();

    void ISimulationTickHost.RefreshLastToDieMedicLinkProjections() => RefreshLastToDieMedicLinkProjections();

    void ISimulationTickHost.AdvanceCivilDefenseTurrets() => AdvanceCivilDefenseTurrets();

    void ISimulationTickHost.EndLastToDieStatusEffectsTick() => EndLastToDieStatusEffectsTick();

    void ISimulationTickHost.TickMapLogicTimersOncePerFrame() => TickMapLogicTimersOncePerFrame();

    void ISimulationTickHost.CommitLocalInputForTick() => LocalState.PreviousInput = LocalState.Input;

    void ISimulationTickHost.AdvanceFrameCounter() => Frame += 1;
}
