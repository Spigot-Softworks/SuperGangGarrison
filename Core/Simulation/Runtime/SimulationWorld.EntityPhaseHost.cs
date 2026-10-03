namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IEntityPhaseHost
{
    void IEntityPhaseHost.AdvanceBloodDrops() => PlayerRemains.AdvanceBloodDrops();
    void IEntityPhaseHost.AdvanceCivvieMoneyPickups() => AdvanceCivvieMoneyPickups();
    void IEntityPhaseHost.AdvanceCombatTraces() => WorldEffects.AdvanceCombatTraces();
    void IEntityPhaseHost.AdvanceDeadBodies() => PlayerRemains.AdvanceDeadBodies();
    void IEntityPhaseHost.AdvanceJumpPadGibs() => Structures.AdvanceJumpPadGibs();
    void IEntityPhaseHost.AdvancePlayerGibs() => PlayerRemains.AdvancePlayerGibs();
    void IEntityPhaseHost.AdvanceSentryGibs() => Structures.AdvanceSentryGibs();
    CombatFeedbackSystem IEntityPhaseHost.CombatFeedback => CombatFeedback;
    void IEntityPhaseHost.ComputeSniperAimIndicators() => WorldEffects.ComputeSniperAimIndicators();
    SimulationConfig IEntityPhaseHost.Config => Config;
    PlayerEntity IEntityPhaseHost.EnemyPlayer => EnemyPlayer;
    bool IEntityPhaseHost.EnemyPlayerEnabled => EnemyPlayerEnabled;
    long IEntityPhaseHost.Frame => Frame;
    PlayerEntity IEntityPhaseHost.FriendlyDummy => FriendlyDummy;
    bool IEntityPhaseHost.FriendlyDummyEnabled => FriendlyDummyEnabled;
    bool IEntityPhaseHost.IsNetworkPlayerActive(byte slot) => IsNetworkPlayerActive(slot);
    LastToDieState IEntityPhaseHost.LastToDieState => LastToDieState;
    SimpleLevel IEntityPhaseHost.Level => Level;
    PlayerEntity IEntityPhaseHost.LocalPlayer => LocalPlayer;
    byte IEntityPhaseHost.LocalPlayerSlot => LocalPlayerSlot;
    MovementSystem IEntityPhaseHost.Movement => Movement;
    NetworkPlayerSystem IEntityPhaseHost.NetworkPlayerRules => NetworkPlayerRules;
    PickupSystem IEntityPhaseHost.Pickups => Pickups;
    PlayerInputSystem IEntityPhaseHost.PlayerInput => PlayerInput;
    NetworkPlayerRegistry IEntityPhaseHost.PlayerRegistry => PlayerRegistry;
    PracticeDummySystem IEntityPhaseHost.PracticeDummies => PracticeDummies;
    PresentationEventLog IEntityPhaseHost.PresentationEvents => PresentationEvents;
    ProjectileSystem IEntityPhaseHost.Projectiles => Projectiles;
    RoomEffectsSystem IEntityPhaseHost.RoomEffects => RoomEffects;
    SnapshotApplySystem IEntityPhaseHost.SnapshotApply => SnapshotApply;
    bool IEntityPhaseHost.SniperAimIndicatorEnabled => SniperAimIndicatorEnabled;
    StructureSystem IEntityPhaseHost.Structures => Structures;
    SupportRulesSystem IEntityPhaseHost.SupportRules => SupportRules;
    WorldObjectStore IEntityPhaseHost.WorldObjects => WorldObjects;
}
