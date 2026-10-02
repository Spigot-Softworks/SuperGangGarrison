namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="SpawnSystem"/> needs from the world coordinator.
/// </summary>
internal interface ISpawnHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    PracticeDummyState DummyState { get; }
    PlayerEntity EnemyPlayer { get; }
    bool EnemyPlayerEnabled { get; }
    PlayerEntity FriendlyDummy { get; }
    bool FriendlyDummyEnabled { get; }
    MatchLifecycleState Lifecycle { get; }
    LocalSimulationState LocalState { get; }
    MatchRules MatchRules { get; }
    ObjectiveStateStore Objectives { get; }
    NetworkPlayerRegistry PlayerRegistry { get; }

    void ClearLastToDieStatusEffectsForTarget(int targetPlayerId);
    (float X, float Y) FindFriendlyDummySpawnNearLocalPlayer();
    ControlPointState? GetDualKothPoint(PlayerTeam homeTeam);
    CharacterClassDefinition GetNetworkPlayerClassDefinition(byte slot);
    PlayerTeam GetNetworkPlayerConfiguredTeam(byte slot);
    ControlPointState? GetSingleKothPoint();
    bool IsNetworkPlayerAwaitingJoin(byte slot);
    void RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId = -1);
    bool ShouldCancelSpawn(PlayerEntity player, PlayerTeam team, float x, float y);
    bool SpawnPracticeCombatDummyResolved(bool playRespawnSound);
    void SyncExperimentalGameplayLoadout(byte slot, PlayerEntity player);
    bool TryClearNetworkPlayerSpawnOverride(byte slot);
    bool TryGetNetworkPlayer(byte slot, out PlayerEntity player);
    bool TryResolveMapManualSpawn(PlayerEntity player, PlayerTeam team, byte slot, out SpawnPoint spawn);
    bool TrySetNetworkPlayerSpawnOverride(byte slot, float x, float y);
    void UpdateSpawnRoomState(PlayerEntity player);
}
