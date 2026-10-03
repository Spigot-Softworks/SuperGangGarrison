namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="PracticeDummySystem"/> needs from the world coordinator.
/// </summary>
internal interface IPracticeDummyHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    PracticeDummyState DummyState { get; }
    PlayerEntity EnemyPlayer { get; }
    bool EnemyPlayerEnabled { get; set; }
    PlayerEntity FriendlyDummy { get; }
    bool FriendlyDummyEnabled { get; set; }
    CombatResolver GeometryResolver { get; }
    PlayerEntity LocalPlayer { get; }
    PlayerTeam LocalPlayerTeam { get; }
    LocalSimulationState LocalState { get; }
    MovementSystem Movement { get; }
    NetworkPlayerSystem NetworkPlayers { get; }
    PlayerDeathSystem PlayerDeaths { get; }
    PlayerInputSystem PlayerInput { get; }
    SimulationRandomStreams Randoms { get; }
    SpawnSystem Spawns { get; }

    void RegisterDamageEvent(
        PlayerEntity? attacker,
        DamageTargetKind targetKind,
        int targetEntityId,
        float x,
        float y,
        int amount,
        bool wasFatal,
        PlayerEntity? playerTarget = null,
        DamageEventFlags flags = DamageEventFlags.None,
        int assistPlayerIdOverride = -1,
        int attackerPlayerIdOverride = -1);
}
