namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="SnapshotApplySystem"/> needs from the world coordinator.
/// </summary>
internal interface ISnapshotApplyHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    GameplayAbilitySystem Abilities { get; }
    IReadOnlyList<BladeProjectileEntity> Blades { get; }
    int BlueCaps { get; set; }
    TeamIntelligenceState BlueIntel { get; }
    IReadOnlyList<BubbleProjectileEntity> Bubbles { get; }
    bool ClientPredictionMode { get; }
    ClientSnapshotState ClientSnapshots { get; }
    PracticeDummyState DummyState { get; }
    PlayerEntity EnemyPlayer { get; }
    bool EnemyPlayerEnabled { get; set; }
    EntityStore EntityStore { get; }
    IReadOnlyList<FlameProjectileEntity> Flames { get; }
    IReadOnlyList<FlareProjectileEntity> Flares { get; }
    PlayerEntity FriendlyDummy { get; }
    bool FriendlyDummyEnabled { get; set; }
    IReadOnlyList<GrenadeProjectileEntity> Grenades { get; }
    KillFeedSystem KillFeed { get; }
    LocalDeathCamState? LocalDeathCam { get; set; }
    PlayerEntity LocalPlayer { get; }
    MatchRules MatchRules { get; set; }
    MatchState MatchState { get; set; }
    IReadOnlyList<MineProjectileEntity> Mines { get; }
    IReadOnlyList<NeedleProjectileEntity> Needles { get; }
    NetworkPlayerSystem NetworkPlayers { get; }
    ObjectiveRulesSystem ObjectiveRules { get; }
    ObjectiveStateStore Objectives { get; }
    NetworkPlayerRegistry PlayerRegistry { get; }
    PlayerRemainsSystem PlayerRemains { get; }
    PresentationEventLog PresentationEvents { get; }
    ProjectileSystem Projectiles { get; }
    ReadyUpSystem ReadyUp { get; }
    int RedCaps { get; set; }
    TeamIntelligenceState RedIntel { get; }
    RemoteSnapshotPlayerRegistry RemoteSnapshots { get; }
    IReadOnlyList<RevolverProjectileEntity> RevolverShots { get; }
    IReadOnlyList<RocketProjectileEntity> Rockets { get; }
    IReadOnlyList<ShotProjectileEntity> Shots { get; }
    int SpectatorCount { get; set; }
    WorldEffectsSystem WorldEffects { get; }
    WorldObjectStore WorldObjects { get; }

    bool TryLoadLevel(string levelName);
    bool TryLoadLevel(string levelName, int mapAreaIndex, bool preservePlayerStats, float? mapScale = null);
}
