namespace OpenGarrison.Core;

/// <summary>
/// Narrow view of the world used by EntityTickPhase to advance projectiles, transient entities, players, and world objects.
/// </summary>
internal interface IEntityPhaseHost
{
    byte LocalPlayerSlot { get; }
    SimulationConfig Config { get; }
    long Frame { get; }
    SimpleLevel Level { get; }
    ProjectileSystem Projectiles { get; }
    PresentationEventLog PresentationEvents { get; }
    WorldObjectStore WorldObjects { get; }
    NetworkPlayerRegistry PlayerRegistry { get; }
    LastToDieState LastToDieState { get; }
    PlayerEntity LocalPlayer { get; }
    PlayerEntity EnemyPlayer { get; }
    bool EnemyPlayerEnabled { get; }
    PlayerEntity FriendlyDummy { get; }
    bool FriendlyDummyEnabled { get; }
    bool SniperAimIndicatorEnabled { get; }

    void AdvanceAfterburnAlertBubbles();
    void AdvanceBloodDrops();
    void AdvanceCivvieMoneyPickups();
    void AdvanceCombatTraces();
    void AdvanceDeadBodies();
    void AdvanceDroppedWeapons();
    void AdvanceEnemyDummy();
    void AdvanceHealthPacks();
    void AdvanceJumpPadGibs();
    void AdvanceJumpPads();
    void AdvanceMovingPlatforms();
    void AdvancePlayableNetworkPlayer(byte slot);
    void AdvancePlayerGibs();
    void AdvanceRemoteSnapshotPlayerTauntStates();
    void AdvanceSentries();
    void AdvanceSentryGibs();
    void ApplyBuffBannerRegeneration();
    void ApplyHealingCabinets(PlayerEntity player);
    bool ApplyRoomForces(PlayerEntity player, bool jumpPressed = false);
    void ApplyRoomHazards(PlayerEntity player);
    void ComputeSniperAimIndicators();
    bool IsNetworkPlayerActive(byte slot);
    void TryActivatePendingSpyBackstab(PlayerEntity player);
    bool TryGetNetworkPlayer(byte slot, out PlayerEntity player);
    void UpdateBuffBannerAuras();
    void UpdateDispenserAuras();
    void UpdateSpawnRoomState(PlayerEntity player);
}
