namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IMatchPhaseHost
{
    byte IMatchPhaseHost.LocalPlayerSlot => LocalPlayerSlot;

    SortedSet<byte> IMatchPhaseHost.EnabledAdditionalPlayerSlots => PlayerRegistry.EnabledAdditionalSlots;

    bool IMatchPhaseHost.EnemyPlayerEnabled => EnemyPlayerEnabled;

    PlayerEntity IMatchPhaseHost.EnemyPlayer => EnemyPlayer;

    PlayerEntity IMatchPhaseHost.FriendlyDummy => FriendlyDummy;

    bool IMatchPhaseHost.TryGetNetworkPlayer(byte slot, out PlayerEntity player) => TryGetNetworkPlayer(slot, out player);

    void IMatchPhaseHost.ApplyExperimentalRageEffects() => ApplyExperimentalRageEffects();

    void IMatchPhaseHost.AdvanceMedicUberEffects() => AdvanceMedicUberEffects();

    void IMatchPhaseHost.AdvanceVipState() => AdvanceVipState();

    void IMatchPhaseHost.AdvanceKillFeed() => AdvanceKillFeed();

    void IMatchPhaseHost.AdvanceLocalDeathCam() => AdvanceLocalDeathCam();

    void IMatchPhaseHost.EmitPendingMedicUberReadyPresentation() => EmitPendingMedicUberReadyPresentation();

    void IMatchPhaseHost.AdvanceExperimentalRageState() => AdvanceExperimentalRageState();

    void IMatchPhaseHost.UpdateAuxiliaryControlPointStateIfNeeded() => UpdateAuxiliaryControlPointStateIfNeeded();

    void IMatchPhaseHost.TickForegroundSpriteJungle() => TickForegroundSpriteJungle();

    void IMatchPhaseHost.TickSpritesheetPlayback() => TickSpritesheetPlayback();
}
