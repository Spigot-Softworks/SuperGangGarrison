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

    void IEntityPhaseHost.AdvanceAfterburnAlertBubbles() => CombatFeedback.AdvanceAfterburnAlertBubbles();
    void IEntityPhaseHost.AdvanceBloodDrops() => PlayerRemains.AdvanceBloodDrops();
    void IEntityPhaseHost.AdvanceCivvieMoneyPickups() => AdvanceCivvieMoneyPickups();
    void IEntityPhaseHost.AdvanceCombatTraces() => WorldEffects.AdvanceCombatTraces();
    void IEntityPhaseHost.AdvanceDeadBodies() => PlayerRemains.AdvanceDeadBodies();
    void IEntityPhaseHost.AdvanceDroppedWeapons() => Pickups.AdvanceDroppedWeapons();
    void IEntityPhaseHost.AdvanceEnemyDummy() => PracticeDummies.AdvanceEnemyDummy();
    void IEntityPhaseHost.AdvanceHealthPacks() => Pickups.AdvanceHealthPacks();
    void IEntityPhaseHost.AdvanceJumpPadGibs() => Structures.AdvanceJumpPadGibs();
    void IEntityPhaseHost.AdvanceJumpPads() => AdvanceJumpPads();
    void IEntityPhaseHost.AdvanceMovingPlatforms() => AdvanceMovingPlatforms();
    void IEntityPhaseHost.AdvancePlayableNetworkPlayer(byte slot) => NetworkPlayerRules.AdvancePlayableNetworkPlayer(slot);
    void IEntityPhaseHost.AdvancePlayerGibs() => PlayerRemains.AdvancePlayerGibs();
    void IEntityPhaseHost.AdvanceRemoteSnapshotPlayerTauntStates() => SnapshotApply.AdvanceRemoteSnapshotPlayerTauntStates();
    void IEntityPhaseHost.AdvanceSentries() => Structures.AdvanceSentries();
    void IEntityPhaseHost.AdvanceSentryGibs() => Structures.AdvanceSentryGibs();
    void IEntityPhaseHost.ApplyBuffBannerRegeneration() => SupportRules.ApplyBuffBannerRegeneration();
    void IEntityPhaseHost.ApplyHealingCabinets(PlayerEntity player) => RoomEffects.ApplyHealingCabinets(player);
    bool IEntityPhaseHost.ApplyRoomForces(PlayerEntity player, bool jumpPressed) => ApplyRoomForces(player, jumpPressed);
    void IEntityPhaseHost.ApplyRoomHazards(PlayerEntity player) => RoomEffects.ApplyRoomHazards(player);
    void IEntityPhaseHost.ComputeSniperAimIndicators() => WorldEffects.ComputeSniperAimIndicators();
    bool IEntityPhaseHost.IsNetworkPlayerActive(byte slot) => IsNetworkPlayerActive(slot);
    void IEntityPhaseHost.TryActivatePendingSpyBackstab(PlayerEntity player) => PlayerInput.TryActivatePendingSpyBackstab(player);
    bool IEntityPhaseHost.TryGetNetworkPlayer(byte slot, out PlayerEntity player) => NetworkPlayerRules.TryGetNetworkPlayer(slot, out player);
    void IEntityPhaseHost.UpdateBuffBannerAuras() => SupportRules.UpdateBuffBannerAuras();
    void IEntityPhaseHost.UpdateDispenserAuras() => Structures.UpdateDispenserAuras();
    void IEntityPhaseHost.UpdateSpawnRoomState(PlayerEntity player) => RoomEffects.UpdateSpawnRoomState(player);
}
