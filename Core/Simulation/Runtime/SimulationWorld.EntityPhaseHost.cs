namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IEntityPhaseHost
{
    byte IEntityPhaseHost.LocalPlayerSlot => LocalPlayerSlot;
    SimulationConfig IEntityPhaseHost.Config => Config;
    long IEntityPhaseHost.Frame => Frame;
    SimpleLevel IEntityPhaseHost.Level => Level;
    ProjectileSystem IEntityPhaseHost.Projectiles => Projectiles;
    PresentationEventLog IEntityPhaseHost.PresentationEvents => PresentationEvents;
    WorldObjectStore IEntityPhaseHost.WorldObjects => WorldObjects;
    NetworkPlayerRegistry IEntityPhaseHost.PlayerRegistry => PlayerRegistry;
    LastToDieState IEntityPhaseHost.LastToDieState => LastToDieState;
    PlayerEntity IEntityPhaseHost.LocalPlayer => LocalPlayer;
    PlayerEntity IEntityPhaseHost.EnemyPlayer => EnemyPlayer;
    bool IEntityPhaseHost.EnemyPlayerEnabled => EnemyPlayerEnabled;
    PlayerEntity IEntityPhaseHost.FriendlyDummy => FriendlyDummy;
    bool IEntityPhaseHost.FriendlyDummyEnabled => FriendlyDummyEnabled;
    bool IEntityPhaseHost.SniperAimIndicatorEnabled => SniperAimIndicatorEnabled;

    void IEntityPhaseHost.AdvanceAfterburnAlertBubbles() => AdvanceAfterburnAlertBubbles();
    void IEntityPhaseHost.AdvanceBloodDrops() => AdvanceBloodDrops();
    void IEntityPhaseHost.AdvanceCivvieMoneyPickups() => AdvanceCivvieMoneyPickups();
    void IEntityPhaseHost.AdvanceCombatTraces() => AdvanceCombatTraces();
    void IEntityPhaseHost.AdvanceDeadBodies() => AdvanceDeadBodies();
    void IEntityPhaseHost.AdvanceDroppedWeapons() => AdvanceDroppedWeapons();
    void IEntityPhaseHost.AdvanceEnemyDummy() => AdvanceEnemyDummy();
    void IEntityPhaseHost.AdvanceHealthPacks() => AdvanceHealthPacks();
    void IEntityPhaseHost.AdvanceJumpPadGibs() => AdvanceJumpPadGibs();
    void IEntityPhaseHost.AdvanceJumpPads() => AdvanceJumpPads();
    void IEntityPhaseHost.AdvanceMovingPlatforms() => AdvanceMovingPlatforms();
    void IEntityPhaseHost.AdvancePlayableNetworkPlayer(byte slot) => AdvancePlayableNetworkPlayer(slot);
    void IEntityPhaseHost.AdvancePlayerGibs() => AdvancePlayerGibs();
    void IEntityPhaseHost.AdvanceRemoteSnapshotPlayerTauntStates() => AdvanceRemoteSnapshotPlayerTauntStates();
    void IEntityPhaseHost.AdvanceSentries() => AdvanceSentries();
    void IEntityPhaseHost.AdvanceSentryGibs() => AdvanceSentryGibs();
    void IEntityPhaseHost.ApplyBuffBannerRegeneration() => ApplyBuffBannerRegeneration();
    void IEntityPhaseHost.ApplyHealingCabinets(PlayerEntity player) => ApplyHealingCabinets(player);
    bool IEntityPhaseHost.ApplyRoomForces(PlayerEntity player, bool jumpPressed) => ApplyRoomForces(player, jumpPressed);
    void IEntityPhaseHost.ApplyRoomHazards(PlayerEntity player) => ApplyRoomHazards(player);
    void IEntityPhaseHost.ComputeSniperAimIndicators() => ComputeSniperAimIndicators();
    bool IEntityPhaseHost.IsNetworkPlayerActive(byte slot) => IsNetworkPlayerActive(slot);
    void IEntityPhaseHost.TryActivatePendingSpyBackstab(PlayerEntity player) => TryActivatePendingSpyBackstab(player);
    bool IEntityPhaseHost.TryGetNetworkPlayer(byte slot, out PlayerEntity player) => TryGetNetworkPlayer(slot, out player);
    void IEntityPhaseHost.UpdateBuffBannerAuras() => UpdateBuffBannerAuras();
    void IEntityPhaseHost.UpdateDispenserAuras() => UpdateDispenserAuras();
    void IEntityPhaseHost.UpdateSpawnRoomState(PlayerEntity player) => UpdateSpawnRoomState(player);
}
