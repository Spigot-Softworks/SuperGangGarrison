namespace OpenGarrison.Core;

/// <summary>
/// Narrow view of the world used by EntityTickPhase to advance projectiles, transient entities, players, and world objects.
/// </summary>
internal interface IEntityPhaseHost
{
    CombatFeedbackSystem CombatFeedback { get; }
    SimulationConfig Config { get; }
    PlayerEntity EnemyPlayer { get; }
    bool EnemyPlayerEnabled { get; }
    long Frame { get; }
    PlayerEntity FriendlyDummy { get; }
    bool FriendlyDummyEnabled { get; }
    LastToDieState LastToDieState { get; }
    SimpleLevel Level { get; }
    PlayerEntity LocalPlayer { get; }
    byte LocalPlayerSlot { get; }
    MovementSystem Movement { get; }
    NetworkPlayerSystem NetworkPlayerRules { get; }
    PickupSystem Pickups { get; }
    PlayerInputSystem PlayerInput { get; }
    NetworkPlayerRegistry PlayerRegistry { get; }
    PracticeDummySystem PracticeDummies { get; }
    PresentationEventLog PresentationEvents { get; }
    ProjectileSystem Projectiles { get; }
    RoomEffectsSystem RoomEffects { get; }
    SnapshotApplySystem SnapshotApply { get; }
    bool SniperAimIndicatorEnabled { get; }
    StructureSystem Structures { get; }
    SupportRulesSystem SupportRules { get; }
    WorldObjectStore WorldObjects { get; }

    void AdvanceBloodDrops();
    void AdvanceCivvieMoneyPickups();
    void AdvanceCombatTraces();
    void AdvanceDeadBodies();
    void AdvanceJumpPadGibs();
    void AdvancePlayerGibs();
    void AdvanceSentryGibs();
    void ComputeSniperAimIndicators();
    bool IsNetworkPlayerActive(byte slot);
}
