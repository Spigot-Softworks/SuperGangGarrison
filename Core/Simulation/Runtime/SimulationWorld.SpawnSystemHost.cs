namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : ISpawnHost
{
    void ISpawnHost.ClearLastToDieStatusEffectsForTarget(int targetPlayerId)
        => LastToDieRules.ClearLastToDieStatusEffectsForTarget(targetPlayerId);
    PracticeDummyState ISpawnHost.DummyState => DummyState;
    PlayerEntity ISpawnHost.EnemyPlayer => EnemyPlayer;
    bool ISpawnHost.EnemyPlayerEnabled => EnemyPlayerEnabled;
    (float X, float Y) ISpawnHost.FindFriendlyDummySpawnNearLocalPlayer()
        => PracticeDummies.FindFriendlyDummySpawnNearLocalPlayer();
    PlayerEntity ISpawnHost.FriendlyDummy => FriendlyDummy;
    bool ISpawnHost.FriendlyDummyEnabled => FriendlyDummyEnabled;
    ControlPointState? ISpawnHost.GetDualKothPoint(PlayerTeam homeTeam)
        => ObjectiveRules.GetDualKothPoint(homeTeam);
    CharacterClassDefinition ISpawnHost.GetNetworkPlayerClassDefinition(byte slot)
        => NetworkPlayerRules.GetNetworkPlayerClassDefinition(slot);
    PlayerTeam ISpawnHost.GetNetworkPlayerConfiguredTeam(byte slot)
        => NetworkPlayerRules.GetNetworkPlayerConfiguredTeam(slot);
    ControlPointState? ISpawnHost.GetSingleKothPoint()
        => ObjectiveRules.GetSingleKothPoint();
    bool ISpawnHost.IsNetworkPlayerAwaitingJoin(byte slot)
        => NetworkPlayerRules.IsNetworkPlayerAwaitingJoin(slot);
    MatchLifecycleState ISpawnHost.Lifecycle => Lifecycle;
    LocalSimulationState ISpawnHost.LocalState => LocalState;
    MatchRules ISpawnHost.MatchRules => MatchRules;
    ObjectiveStateStore ISpawnHost.Objectives => Objectives;
    NetworkPlayerRegistry ISpawnHost.PlayerRegistry => PlayerRegistry;
    void ISpawnHost.RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId)
        => WorldEffects.RegisterWorldSoundEvent(soundName, x, y, sourcePlayerId);
    bool ISpawnHost.ShouldCancelSpawn(PlayerEntity player, PlayerTeam team, float x, float y)
        => ShouldCancelSpawn(player, team, x, y);
    bool ISpawnHost.SpawnPracticeCombatDummyResolved(bool playRespawnSound)
        => PracticeDummies.SpawnPracticeCombatDummyResolved(playRespawnSound);
    void ISpawnHost.SyncExperimentalGameplayLoadout(byte slot, PlayerEntity player)
        => SyncExperimentalGameplayLoadout(slot, player);
    bool ISpawnHost.TryClearNetworkPlayerSpawnOverride(byte slot)
        => NetworkPlayerRules.TryClearNetworkPlayerSpawnOverride(slot);
    bool ISpawnHost.TryGetNetworkPlayer(byte slot, out PlayerEntity player)
        => NetworkPlayerRules.TryGetNetworkPlayer(slot, out player);
    bool ISpawnHost.TryResolveMapManualSpawn(PlayerEntity player, PlayerTeam team, byte slot, out SpawnPoint spawn)
        => ClassRules.TryResolveMapManualSpawn(player, team, slot, out spawn);
    bool ISpawnHost.TrySetNetworkPlayerSpawnOverride(byte slot, float x, float y)
        => NetworkPlayerRules.TrySetNetworkPlayerSpawnOverride(slot, x, y);
    void ISpawnHost.UpdateSpawnRoomState(PlayerEntity player)
        => RoomEffects.UpdateSpawnRoomState(player);
}
