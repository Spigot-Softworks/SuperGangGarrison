using OpenGarrison.Core.LastToDie;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

/// <summary>
/// Narrow view of the world used by <see cref="NetworkPlayerSystem"/>.
/// </summary>
internal partial interface INetworkPlayerHost
{
    PracticeDummyState DummyState { get; }
    EntityStore EntityStore { get; }
    PlayerEntity FriendlyDummy { get; }
    bool FriendlyDummyEnabled { get; }
    LastToDieState LastToDieState { get; }
    PlayerEntity LocalPlayer { get; }
    int LocalPlayerRespawnTicks { get; set; }
    PlayerTeam LocalPlayerTeam { get; set; }
    LocalSimulationState LocalState { get; }
    MatchRules MatchRules { get; }
    MatchSettingsState MatchSettings { get; }
    NetworkPlayerRegistry PlayerRegistry { get; }
    RemoteSnapshotPlayerRegistry RemoteSnapshots { get; }

    void AdvanceAlivePlayerWithInput(PlayerEntity player, PlayerInputSnapshot input, PlayerInputSnapshot previousInput, PlayerTeam team, bool allowDebugKill);
    void AdvanceLastToDiePassivePerks(byte slot, PlayerEntity player);
    void AdvanceNetworkRespawnTimer(byte slot);
    int AllocateEntityId();
    void ApplyServerGameplayTuning(byte slot, PlayerEntity player);
    bool CanApplyNetworkPlayerClassLimit(byte slot, CharacterClassDefinition definition);
    void ClearDominationsForPlayer(PlayerEntity player);
    void ClearJumpInputBuffer(PlayerEntity player);
    void ClearLastToDieSniperMarksTargeting(byte targetSlot);
    void ClearLastToDieStatusEffectsForReleasedPlayer(int playerId);
    void ClearLastToDieStatusEffectsForTarget(int targetPlayerId);
    IEnumerable<PlayerEntity> EnumerateSimulatedPlayers();
    (float X, float Y) FindFriendlyDummySpawnNearLocalPlayer();
    PlayerTeam GetNetworkPlayerTeam(byte slot);
    bool IsNetworkPlayerActive(byte slot);
    void KillPlayer(PlayerEntity player, bool gibbed = false, PlayerEntity? killer = null, string? weaponSpriteName = null, DeadBodyAnimationKind deadBodyAnimationKind = DeadBodyAnimationKind.Default, string? deathCamMessage = null, SentryEntity? deathCamSentry = null, string? killFeedMessage = null, bool createDeathCam = true, bool spawnRemains = true, bool forceCorpseRemains = false, bool recordKillFeed = true, int assistingPlayerIdOverride = -1, bool completingLastToDieSpyAfterlifeDeath = false);
    void RemoveOwnedMines(int ownerId);
    void RemoveOwnedProjectiles(int ownerId);
    void RemoveOwnedSentries(int ownerId);
    void RemoveOwnedSpyArtifacts(int ownerId);
    SpawnPoint ReserveSpawn(PlayerEntity player, PlayerTeam team);
    SpawnPoint ReserveSpawn(PlayerEntity player, PlayerTeam team, byte slot);
    CharacterClassDefinition ResolveMapForcedClassDefinition(byte slot, CharacterClassDefinition requested);
    void SetNetworkPlayerDeathCam(byte slot, LocalDeathCamState? deathCam);
    bool SpawnPlayerResolved(PlayerEntity player, PlayerTeam team, float x, float y, bool clearMedicHealingTarget = true, bool playRespawnSound = false);
    bool SpawnPlayerResolved(PlayerEntity player, PlayerTeam team, SpawnPoint spawn, bool clearMedicHealingTarget = true, bool playRespawnSound = false);
    void SyncExperimentalGameplayLoadout(byte slot, PlayerEntity player);
    void TryDropCarriedIntel();
    void TryDropCarriedIntel(PlayerEntity player);
    bool TryFailLastToDieSpyAfterlifeOnDisconnect(byte slot, PlayerEntity player);
    bool TrySetLocalClass(PlayerClass playerClass);
    bool TrySetLocalClass(string gameplayClassId);
    bool TrySetNetworkPlayerReady(byte slot, bool ready);
}
