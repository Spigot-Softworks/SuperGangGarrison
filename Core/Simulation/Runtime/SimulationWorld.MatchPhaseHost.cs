namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IMatchPhaseHost
{
    void IMatchPhaseHost.AdvanceExperimentalRageState() => ExperimentalRules.AdvanceExperimentalRageState();
    void IMatchPhaseHost.AdvanceKillFeed() => KillFeedRules.AdvanceKillFeed();
    void IMatchPhaseHost.AdvanceLocalDeathCam() => PlayerDeaths.AdvanceLocalDeathCam();
    void IMatchPhaseHost.AdvanceMedicUberEffects() => SupportRules.AdvanceMedicUberEffects();
    void IMatchPhaseHost.AdvanceVipState() => VipRules.AdvanceVipState();
    void IMatchPhaseHost.ApplyExperimentalRageEffects() => ExperimentalRules.ApplyExperimentalRageEffects();
    void IMatchPhaseHost.EmitPendingMedicUberReadyPresentation() => SupportRules.EmitPendingMedicUberReadyPresentation();
    SortedSet<byte> IMatchPhaseHost.EnabledAdditionalPlayerSlots => PlayerRegistry.EnabledAdditionalSlots;
    PlayerEntity IMatchPhaseHost.EnemyPlayer => EnemyPlayer;
    bool IMatchPhaseHost.EnemyPlayerEnabled => EnemyPlayerEnabled;
    PlayerEntity IMatchPhaseHost.FriendlyDummy => FriendlyDummy;
    byte IMatchPhaseHost.LocalPlayerSlot => LocalPlayerSlot;
    void IMatchPhaseHost.TickForegroundSpriteJungle() => MapLogic.TickForegroundSpriteJungle();
    void IMatchPhaseHost.TickSpritesheetPlayback() => MapLogic.TickSpritesheetPlayback();
    bool IMatchPhaseHost.TryGetNetworkPlayer(byte slot, out PlayerEntity player) => NetworkPlayerRules.TryGetNetworkPlayer(slot, out player);
    void IMatchPhaseHost.UpdateAuxiliaryControlPointStateIfNeeded() => UpdateAuxiliaryControlPointStateIfNeeded();
}
