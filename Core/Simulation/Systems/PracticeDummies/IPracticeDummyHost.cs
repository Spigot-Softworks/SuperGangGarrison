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
    PlayerEntity LocalPlayer { get; }
    PlayerTeam LocalPlayerTeam { get; }
    LocalSimulationState LocalState { get; }
    SimulationRandomStreams Randoms { get; }

    void AdvanceAlivePlayerWithInput(
        PlayerEntity player,
        PlayerInputSnapshot input,
        PlayerInputSnapshot previousInput,
        PlayerTeam team,
        bool allowDebugKill);
    void AdvanceEnemyDummyRespawnTimer();
    void ClearEnemyInputOverride();
    bool HasLineOfSight(PlayerEntity attacker, PlayerEntity target);
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
    SpawnPoint ReserveSpawn(PlayerEntity player, PlayerTeam team);
    SpawnPoint ReserveSpawn(PlayerEntity player, PlayerTeam team, byte slot);
    bool SpawnPlayerResolved(
        PlayerEntity player,
        PlayerTeam team,
        float x,
        float y,
        bool clearMedicHealingTarget = true,
        bool playRespawnSound = false);
    bool SpawnPlayerResolved(
        PlayerEntity player,
        PlayerTeam team,
        SpawnPoint spawn,
        bool clearMedicHealingTarget = true,
        bool playRespawnSound = false);
    bool WouldRunIntoWall(PlayerEntity player, float moveDirection);
}
