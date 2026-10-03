namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IMatchPhaseHost
{
    void IMatchPhaseHost.AdvanceExperimentalRageState() => ExperimentalRules.AdvanceExperimentalRageState();
    void IMatchPhaseHost.AdvanceKillFeed() => KillFeed.AdvanceKillFeed();
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
    bool IMatchPhaseHost.TryGetNetworkPlayer(byte slot, out PlayerEntity player) => NetworkPlayers.TryGetNetworkPlayer(slot, out player);
    void IMatchPhaseHost.UpdateAuxiliaryControlPointStateIfNeeded()
    {
        if (!Level.ShowControlPoints
            || Objectives.ControlPoints.Points.Count == 0
            || MatchRules.Mode is GameModeKind.ControlPoint
                or GameModeKind.Scr
                or GameModeKind.KingOfTheHill
                or GameModeKind.DoubleKingOfTheHill)
        {
            return;
        }

        ObjectiveRules.UpdateControlPointState();
    }
}
