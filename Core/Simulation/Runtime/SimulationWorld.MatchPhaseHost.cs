namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IMatchPhaseHost
{
    byte IMatchPhaseHost.LocalPlayerSlot => LocalPlayerSlot;

    SortedSet<byte> IMatchPhaseHost.EnabledAdditionalPlayerSlots => PlayerRegistry.EnabledAdditionalSlots;

    bool IMatchPhaseHost.EnemyPlayerEnabled => EnemyPlayerEnabled;

    PlayerEntity IMatchPhaseHost.EnemyPlayer => EnemyPlayer;

    PlayerEntity IMatchPhaseHost.FriendlyDummy => FriendlyDummy;

    bool IMatchPhaseHost.TryGetNetworkPlayer(byte slot, out PlayerEntity player) => NetworkPlayerRules.TryGetNetworkPlayer(slot, out player);

    void IMatchPhaseHost.ApplyExperimentalRageEffects() => ExperimentalRules.ApplyExperimentalRageEffects();

    void IMatchPhaseHost.AdvanceMedicUberEffects() => SupportRules.AdvanceMedicUberEffects();

    void IMatchPhaseHost.AdvanceVipState() => VipRules.AdvanceVipState();

    void IMatchPhaseHost.AdvanceKillFeed() => KillFeedRules.AdvanceKillFeed();

    void IMatchPhaseHost.AdvanceLocalDeathCam() => PlayerDeaths.AdvanceLocalDeathCam();

    void IMatchPhaseHost.EmitPendingMedicUberReadyPresentation() => SupportRules.EmitPendingMedicUberReadyPresentation();

    void IMatchPhaseHost.AdvanceExperimentalRageState() => ExperimentalRules.AdvanceExperimentalRageState();

    void IMatchPhaseHost.UpdateAuxiliaryControlPointStateIfNeeded() => UpdateAuxiliaryControlPointStateIfNeeded();

    void IMatchPhaseHost.TickForegroundSpriteJungle() => MapLogic.TickForegroundSpriteJungle();

    void IMatchPhaseHost.TickSpritesheetPlayback() => MapLogic.TickSpritesheetPlayback();
}
