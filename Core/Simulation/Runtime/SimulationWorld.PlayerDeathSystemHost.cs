namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IPlayerDeathHost
{
    int IPlayerDeathHost.AllocateEntityId()
        => AllocateEntityId();
    CombatSystem IPlayerDeathHost.Combat => Combat;
    CombatFeedbackSystem IPlayerDeathHost.CombatFeedback => CombatFeedback;
    DecisionGate IPlayerDeathHost.DecisionGate => DecisionGate;
    PracticeDummyState IPlayerDeathHost.DummyState => DummyState;
    PlayerEntity IPlayerDeathHost.EnemyPlayer => EnemyPlayer;
    bool IPlayerDeathHost.EnemyPlayerEnabled => EnemyPlayerEnabled;
    EntityStore IPlayerDeathHost.EntityStore => EntityStore;
    ExperimentalRulesSystem IPlayerDeathHost.ExperimentalRules => ExperimentalRules;
    ExplosionRulesSystem IPlayerDeathHost.ExplosionRules => ExplosionRules;
    KillFeedSystem IPlayerDeathHost.KillFeed => KillFeed;
    LastToDieRulesSystem IPlayerDeathHost.LastToDieRules => LastToDieRules;
    LocalDeathCamState? IPlayerDeathHost.LocalDeathCam { get => LocalDeathCam; set => LocalDeathCam = value; }
    MatchRules IPlayerDeathHost.MatchRules => MatchRules;
    MatchSettingsState IPlayerDeathHost.MatchSettings => MatchSettings;
    NetworkPlayerSystem IPlayerDeathHost.NetworkPlayers => NetworkPlayers;
    ObjectiveRulesSystem IPlayerDeathHost.ObjectiveRules => ObjectiveRules;
    PickupSystem IPlayerDeathHost.Pickups => Pickups;
    NetworkPlayerRegistry IPlayerDeathHost.PlayerRegistry => PlayerRegistry;
    PlayerRemainsSystem IPlayerDeathHost.PlayerRemains => PlayerRemains;
    PracticeDummySystem IPlayerDeathHost.PracticeDummies => PracticeDummies;
    ProjectileSystem IPlayerDeathHost.Projectiles => Projectiles;
    SimulationRandomStreams IPlayerDeathHost.Randoms => Randoms;
    ScorekeepingSystem IPlayerDeathHost.Scorekeeping => Scorekeeping;
    SpawnSystem IPlayerDeathHost.Spawns => Spawns;
    bool IPlayerDeathHost.TryBeginPlayerDeath(PlayerEntity player, bool gibbed, PlayerEntity? killer, string? weaponSpriteName)
        => TryBeginPlayerDeath(player, gibbed, killer, weaponSpriteName);
    VipRulesSystem IPlayerDeathHost.VipRules => VipRules;
    WorldEffectsSystem IPlayerDeathHost.WorldEffects => WorldEffects;
    WorldObjectStore IPlayerDeathHost.WorldObjects => WorldObjects;
}
