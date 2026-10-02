namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IPracticeDummyHost
{
    void IPracticeDummyHost.AdvanceAlivePlayerWithInput(PlayerEntity player, PlayerInputSnapshot input, PlayerInputSnapshot previousInput, PlayerTeam team, bool allowDebugKill)
        => PlayerInput.AdvanceAlivePlayerWithInput(player, input, previousInput, team, allowDebugKill);
    void IPracticeDummyHost.AdvanceEnemyDummyRespawnTimer()
        => PlayerDeaths.AdvanceEnemyDummyRespawnTimer();
    void IPracticeDummyHost.ClearEnemyInputOverride()
        => NetworkPlayerRules.ClearEnemyInputOverride();
    PracticeDummyState IPracticeDummyHost.DummyState => DummyState;
    PlayerEntity IPracticeDummyHost.EnemyPlayer => EnemyPlayer;
    bool IPracticeDummyHost.EnemyPlayerEnabled { get => EnemyPlayerEnabled; set => EnemyPlayerEnabled = value; }
    PlayerEntity IPracticeDummyHost.FriendlyDummy => FriendlyDummy;
    bool IPracticeDummyHost.FriendlyDummyEnabled { get => FriendlyDummyEnabled; set => FriendlyDummyEnabled = value; }
    bool IPracticeDummyHost.HasLineOfSight(PlayerEntity attacker, PlayerEntity target)
        => HasLineOfSight(attacker, target);
    PlayerEntity IPracticeDummyHost.LocalPlayer => LocalPlayer;
    PlayerTeam IPracticeDummyHost.LocalPlayerTeam => LocalPlayerTeam;
    LocalSimulationState IPracticeDummyHost.LocalState => LocalState;
    SimulationRandomStreams IPracticeDummyHost.Randoms => Randoms;
    void IPracticeDummyHost.RegisterDamageEvent(PlayerEntity? attacker, DamageTargetKind targetKind, int targetEntityId, float x, float y, int amount, bool wasFatal, PlayerEntity? playerTarget, DamageEventFlags flags, int assistPlayerIdOverride, int attackerPlayerIdOverride)
        => RegisterDamageEvent(attacker, targetKind, targetEntityId, x, y, amount, wasFatal, playerTarget, flags, assistPlayerIdOverride, attackerPlayerIdOverride);
    SpawnPoint IPracticeDummyHost.ReserveSpawn(PlayerEntity player, PlayerTeam team)
        => Spawns.ReserveSpawn(player, team);
    SpawnPoint IPracticeDummyHost.ReserveSpawn(PlayerEntity player, PlayerTeam team, byte slot)
        => Spawns.ReserveSpawn(player, team, slot);
    bool IPracticeDummyHost.SpawnPlayerResolved(PlayerEntity player, PlayerTeam team, float x, float y, bool clearMedicHealingTarget, bool playRespawnSound)
        => Spawns.SpawnPlayerResolved(player, team, x, y, clearMedicHealingTarget, playRespawnSound);
    bool IPracticeDummyHost.SpawnPlayerResolved(PlayerEntity player, PlayerTeam team, SpawnPoint spawn, bool clearMedicHealingTarget, bool playRespawnSound)
        => Spawns.SpawnPlayerResolved(player, team, spawn, clearMedicHealingTarget, playRespawnSound);
    bool IPracticeDummyHost.WouldRunIntoWall(PlayerEntity player, float moveDirection)
        => WouldRunIntoWall(player, moveDirection);
}
