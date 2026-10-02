using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : ISnapshotApplyHost
{
    GameplayAbilitySystem ISnapshotApplyHost.Abilities => Abilities;
    IReadOnlyList<BladeProjectileEntity> ISnapshotApplyHost.Blades => Blades;
    int ISnapshotApplyHost.BlueCaps { get => BlueCaps; set => BlueCaps = value; }
    TeamIntelligenceState ISnapshotApplyHost.BlueIntel => BlueIntel;
    IReadOnlyList<BubbleProjectileEntity> ISnapshotApplyHost.Bubbles => Bubbles;
    bool ISnapshotApplyHost.ClientPredictionMode => ClientPredictionMode;
    ClientSnapshotState ISnapshotApplyHost.ClientSnapshots => ClientSnapshots;
    PracticeDummyState ISnapshotApplyHost.DummyState => DummyState;
    PlayerEntity ISnapshotApplyHost.EnemyPlayer => EnemyPlayer;
    bool ISnapshotApplyHost.EnemyPlayerEnabled { get => EnemyPlayerEnabled; set => EnemyPlayerEnabled = value; }
    EntityStore ISnapshotApplyHost.EntityStore => EntityStore;
    IReadOnlyList<FlameProjectileEntity> ISnapshotApplyHost.Flames => Flames;
    IReadOnlyList<FlareProjectileEntity> ISnapshotApplyHost.Flares => Flares;
    PlayerEntity ISnapshotApplyHost.FriendlyDummy => FriendlyDummy;
    bool ISnapshotApplyHost.FriendlyDummyEnabled { get => FriendlyDummyEnabled; set => FriendlyDummyEnabled = value; }
    IReadOnlyList<GrenadeProjectileEntity> ISnapshotApplyHost.Grenades => Grenades;
    KillFeedSystem ISnapshotApplyHost.KillFeedRules => KillFeedRules;
    LocalDeathCamState? ISnapshotApplyHost.LocalDeathCam { get => LocalDeathCam; set => LocalDeathCam = value; }
    PlayerEntity ISnapshotApplyHost.LocalPlayer => LocalPlayer;
    MatchRules ISnapshotApplyHost.MatchRules { get => MatchRules; set => MatchRules = value; }
    MatchState ISnapshotApplyHost.MatchState { get => MatchState; set => MatchState = value; }
    IReadOnlyList<MineProjectileEntity> ISnapshotApplyHost.Mines => Mines;
    IReadOnlyList<NeedleProjectileEntity> ISnapshotApplyHost.Needles => Needles;
    NetworkPlayerSystem ISnapshotApplyHost.NetworkPlayerRules => NetworkPlayerRules;
    ObjectiveRulesSystem ISnapshotApplyHost.ObjectiveRules => ObjectiveRules;
    ObjectiveStateStore ISnapshotApplyHost.Objectives => Objectives;
    NetworkPlayerRegistry ISnapshotApplyHost.PlayerRegistry => PlayerRegistry;
    PlayerRemainsSystem ISnapshotApplyHost.PlayerRemains => PlayerRemains;
    PresentationEventLog ISnapshotApplyHost.PresentationEvents => PresentationEvents;
    ProjectileSystem ISnapshotApplyHost.Projectiles => Projectiles;
    ReadyUpSystem ISnapshotApplyHost.ReadyUp => ReadyUp;
    int ISnapshotApplyHost.RedCaps { get => RedCaps; set => RedCaps = value; }
    TeamIntelligenceState ISnapshotApplyHost.RedIntel => RedIntel;
    RemoteSnapshotPlayerRegistry ISnapshotApplyHost.RemoteSnapshots => RemoteSnapshots;
    IReadOnlyList<RevolverProjectileEntity> ISnapshotApplyHost.RevolverShots => RevolverShots;
    IReadOnlyList<RocketProjectileEntity> ISnapshotApplyHost.Rockets => Rockets;
    IReadOnlyList<ShotProjectileEntity> ISnapshotApplyHost.Shots => Shots;
    int ISnapshotApplyHost.SpectatorCount { get => SpectatorCount; set => SpectatorCount = value; }
    bool ISnapshotApplyHost.TryLoadLevel(string levelName)
        => TryLoadLevel(levelName);
    bool ISnapshotApplyHost.TryLoadLevel(string levelName, int mapAreaIndex, bool preservePlayerStats, float? mapScale)
        => TryLoadLevel(levelName, mapAreaIndex, preservePlayerStats, mapScale);
    WorldEffectsSystem ISnapshotApplyHost.WorldEffects => WorldEffects;
    WorldObjectStore ISnapshotApplyHost.WorldObjects => WorldObjects;
}
