namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="PlayerDeathSystem"/> needs from the world coordinator.
/// </summary>
internal interface IPlayerDeathHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    CombatSystem Combat { get; }
    CombatFeedbackSystem CombatFeedback { get; }
    DecisionGate DecisionGate { get; }
    PracticeDummyState DummyState { get; }
    PlayerEntity EnemyPlayer { get; }
    bool EnemyPlayerEnabled { get; }
    EntityStore EntityStore { get; }
    ExperimentalRulesSystem ExperimentalRules { get; }
    ExplosionRulesSystem ExplosionRules { get; }
    KillFeedSystem KillFeed { get; }
    LastToDieRulesSystem LastToDieRules { get; }
    LocalDeathCamState? LocalDeathCam { get; set; }
    MatchRules MatchRules { get; }
    MatchSettingsState MatchSettings { get; }
    NetworkPlayerSystem NetworkPlayers { get; }
    ObjectiveRulesSystem ObjectiveRules { get; }
    PickupSystem Pickups { get; }
    NetworkPlayerRegistry PlayerRegistry { get; }
    PlayerRemainsSystem PlayerRemains { get; }
    PracticeDummySystem PracticeDummies { get; }
    ProjectileSystem Projectiles { get; }
    SimulationRandomStreams Randoms { get; }
    ScorekeepingSystem Scorekeeping { get; }
    SpawnSystem Spawns { get; }
    VipRulesSystem VipRules { get; }
    WorldEffectsSystem WorldEffects { get; }
    WorldObjectStore WorldObjects { get; }

    int AllocateEntityId();
    bool TryBeginPlayerDeath(PlayerEntity player, bool gibbed, PlayerEntity? killer, string? weaponSpriteName);
}
