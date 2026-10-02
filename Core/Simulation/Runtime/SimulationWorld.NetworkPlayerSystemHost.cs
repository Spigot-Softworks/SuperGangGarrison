using OpenGarrison.Core.LastToDie;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : INetworkPlayerHost
{
    PracticeDummyState INetworkPlayerHost.DummyState => DummyState;
    EntityStore INetworkPlayerHost.EntityStore => EntityStore;
    PlayerEntity INetworkPlayerHost.FriendlyDummy => FriendlyDummy;
    bool INetworkPlayerHost.FriendlyDummyEnabled => FriendlyDummyEnabled;
    LastToDieState INetworkPlayerHost.LastToDieState => LastToDieState;
    PlayerEntity INetworkPlayerHost.LocalPlayer => LocalPlayer;
    int INetworkPlayerHost.LocalPlayerRespawnTicks { get => LocalPlayerRespawnTicks; set => LocalPlayerRespawnTicks = value; }
    PlayerTeam INetworkPlayerHost.LocalPlayerTeam { get => LocalPlayerTeam; set => LocalPlayerTeam = value; }
    LocalSimulationState INetworkPlayerHost.LocalState => LocalState;
    MatchRules INetworkPlayerHost.MatchRules => MatchRules;
    MatchSettingsState INetworkPlayerHost.MatchSettings => MatchSettings;
    NetworkPlayerRegistry INetworkPlayerHost.PlayerRegistry => PlayerRegistry;
    RemoteSnapshotPlayerRegistry INetworkPlayerHost.RemoteSnapshots => RemoteSnapshots;

    void INetworkPlayerHost.AdvanceAlivePlayerWithInput(PlayerEntity player, PlayerInputSnapshot input, PlayerInputSnapshot previousInput, PlayerTeam team, bool allowDebugKill) => AdvanceAlivePlayerWithInput(player, input, previousInput, team, allowDebugKill);
    void INetworkPlayerHost.AdvanceLastToDiePassivePerks(byte slot, PlayerEntity player) => AdvanceLastToDiePassivePerks(slot, player);
    void INetworkPlayerHost.AdvanceNetworkRespawnTimer(byte slot) => AdvanceNetworkRespawnTimer(slot);
    int INetworkPlayerHost.AllocateEntityId() => AllocateEntityId();
    void INetworkPlayerHost.ApplyServerGameplayTuning(byte slot, PlayerEntity player) => ApplyServerGameplayTuning(slot, player);
    bool INetworkPlayerHost.CanApplyNetworkPlayerClassLimit(byte slot, CharacterClassDefinition definition) => CanApplyNetworkPlayerClassLimit(slot, definition);
    void INetworkPlayerHost.ClearDominationsForPlayer(PlayerEntity player) => ClearDominationsForPlayer(player);
    void INetworkPlayerHost.ClearJumpInputBuffer(PlayerEntity player) => ClearJumpInputBuffer(player);
    void INetworkPlayerHost.ClearLastToDieSniperMarksTargeting(byte targetSlot) => ClearLastToDieSniperMarksTargeting(targetSlot);
    void INetworkPlayerHost.ClearLastToDieStatusEffectsForReleasedPlayer(int playerId) => ClearLastToDieStatusEffectsForReleasedPlayer(playerId);
    void INetworkPlayerHost.ClearLastToDieStatusEffectsForTarget(int targetPlayerId) => ClearLastToDieStatusEffectsForTarget(targetPlayerId);
    IEnumerable<PlayerEntity> INetworkPlayerHost.EnumerateSimulatedPlayers() => EnumerateSimulatedPlayers();
    (float X, float Y) INetworkPlayerHost.FindFriendlyDummySpawnNearLocalPlayer() => FindFriendlyDummySpawnNearLocalPlayer();
    PlayerTeam INetworkPlayerHost.GetNetworkPlayerTeam(byte slot) => GetNetworkPlayerTeam(slot);
    bool INetworkPlayerHost.IsNetworkPlayerActive(byte slot) => IsNetworkPlayerActive(slot);
    void INetworkPlayerHost.KillPlayer(PlayerEntity player, bool gibbed, PlayerEntity? killer, string? weaponSpriteName, DeadBodyAnimationKind deadBodyAnimationKind, string? deathCamMessage, SentryEntity? deathCamSentry, string? killFeedMessage, bool createDeathCam, bool spawnRemains, bool forceCorpseRemains, bool recordKillFeed, int assistingPlayerIdOverride, bool completingLastToDieSpyAfterlifeDeath) => KillPlayer(player, gibbed, killer, weaponSpriteName, deadBodyAnimationKind, deathCamMessage, deathCamSentry, killFeedMessage, createDeathCam, spawnRemains, forceCorpseRemains, recordKillFeed, assistingPlayerIdOverride, completingLastToDieSpyAfterlifeDeath);
    void INetworkPlayerHost.RemoveOwnedMines(int ownerId) => RemoveOwnedMines(ownerId);
    void INetworkPlayerHost.RemoveOwnedProjectiles(int ownerId) => RemoveOwnedProjectiles(ownerId);
    void INetworkPlayerHost.RemoveOwnedSentries(int ownerId) => RemoveOwnedSentries(ownerId);
    void INetworkPlayerHost.RemoveOwnedSpyArtifacts(int ownerId) => RemoveOwnedSpyArtifacts(ownerId);
    SpawnPoint INetworkPlayerHost.ReserveSpawn(PlayerEntity player, PlayerTeam team) => ReserveSpawn(player, team);
    SpawnPoint INetworkPlayerHost.ReserveSpawn(PlayerEntity player, PlayerTeam team, byte slot) => ReserveSpawn(player, team, slot);
    CharacterClassDefinition INetworkPlayerHost.ResolveMapForcedClassDefinition(byte slot, CharacterClassDefinition requested) => ResolveMapForcedClassDefinition(slot, requested);
    void INetworkPlayerHost.SetNetworkPlayerDeathCam(byte slot, LocalDeathCamState? deathCam) => SetNetworkPlayerDeathCam(slot, deathCam);
    bool INetworkPlayerHost.SpawnPlayerResolved(PlayerEntity player, PlayerTeam team, float x, float y, bool clearMedicHealingTarget, bool playRespawnSound) => SpawnPlayerResolved(player, team, x, y, clearMedicHealingTarget, playRespawnSound);
    bool INetworkPlayerHost.SpawnPlayerResolved(PlayerEntity player, PlayerTeam team, SpawnPoint spawn, bool clearMedicHealingTarget, bool playRespawnSound) => SpawnPlayerResolved(player, team, spawn, clearMedicHealingTarget, playRespawnSound);
    void INetworkPlayerHost.SyncExperimentalGameplayLoadout(byte slot, PlayerEntity player) => SyncExperimentalGameplayLoadout(slot, player);
    void INetworkPlayerHost.TryDropCarriedIntel() => TryDropCarriedIntel();
    void INetworkPlayerHost.TryDropCarriedIntel(PlayerEntity player) => TryDropCarriedIntel(player);
    bool INetworkPlayerHost.TryFailLastToDieSpyAfterlifeOnDisconnect(byte slot, PlayerEntity player) => TryFailLastToDieSpyAfterlifeOnDisconnect(slot, player);
    bool INetworkPlayerHost.TrySetLocalClass(PlayerClass playerClass) => TrySetLocalClass(playerClass);
    bool INetworkPlayerHost.TrySetLocalClass(string gameplayClassId) => TrySetLocalClass(gameplayClassId);
    bool INetworkPlayerHost.TrySetNetworkPlayerReady(byte slot, bool ready) => TrySetNetworkPlayerReady(slot, ready);
}
