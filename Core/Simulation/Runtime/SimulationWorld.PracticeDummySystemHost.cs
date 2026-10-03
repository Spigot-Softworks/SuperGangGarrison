namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IPracticeDummyHost
{
    PracticeDummyState IPracticeDummyHost.DummyState => DummyState;
    PlayerEntity IPracticeDummyHost.EnemyPlayer => EnemyPlayer;
    bool IPracticeDummyHost.EnemyPlayerEnabled { get => EnemyPlayerEnabled; set => EnemyPlayerEnabled = value; }
    PlayerEntity IPracticeDummyHost.FriendlyDummy => FriendlyDummy;
    bool IPracticeDummyHost.FriendlyDummyEnabled { get => FriendlyDummyEnabled; set => FriendlyDummyEnabled = value; }
    CombatResolver IPracticeDummyHost.GeometryResolver => GeometryResolver;
    PlayerEntity IPracticeDummyHost.LocalPlayer => LocalPlayer;
    PlayerTeam IPracticeDummyHost.LocalPlayerTeam => LocalPlayerTeam;
    LocalSimulationState IPracticeDummyHost.LocalState => LocalState;
    MovementSystem IPracticeDummyHost.Movement => Movement;
    NetworkPlayerSystem IPracticeDummyHost.NetworkPlayers => NetworkPlayers;
    PlayerDeathSystem IPracticeDummyHost.PlayerDeaths => PlayerDeaths;
    PlayerInputSystem IPracticeDummyHost.PlayerInput => PlayerInput;
    SimulationRandomStreams IPracticeDummyHost.Randoms => Randoms;
    void IPracticeDummyHost.RegisterDamageEvent(PlayerEntity? attacker, DamageTargetKind targetKind, int targetEntityId, float x, float y, int amount, bool wasFatal, PlayerEntity? playerTarget, DamageEventFlags flags, int assistPlayerIdOverride, int attackerPlayerIdOverride)
        => RegisterDamageEvent(attacker, targetKind, targetEntityId, x, y, amount, wasFatal, playerTarget, flags, assistPlayerIdOverride, attackerPlayerIdOverride);
    SpawnSystem IPracticeDummyHost.Spawns => Spawns;
}
