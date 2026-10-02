using OpenGarrison.Core.LastToDie;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

// Thin forwarders so remaining world code keeps its call shape; callers should migrate to the system directly over time.
public sealed partial class SimulationWorld
{
    public void ClearEnemyInputOverride() => NetworkPlayerRules.ClearEnemyInputOverride();
    public void CompleteLocalPlayerJoin(PlayerClass playerClass) => NetworkPlayerRules.CompleteLocalPlayerJoin(playerClass);
    public void CompleteLocalPlayerJoin(string gameplayClassId) => NetworkPlayerRules.CompleteLocalPlayerJoin(gameplayClassId);
    public IEnumerable<(byte Slot, PlayerEntity Player)> EnumerateActiveNetworkPlayers() => NetworkPlayerRules.EnumerateActiveNetworkPlayers();
    public IEnumerable<(byte Slot, PlayerEntity Player)> EnumerateReplicatedNetworkPlayers() => NetworkPlayerRules.EnumerateReplicatedNetworkPlayers();
    public void ForceKillLocalPlayer() => NetworkPlayerRules.ForceKillLocalPlayer();
    public bool ForceKillNetworkPlayer(byte slot) => NetworkPlayerRules.ForceKillNetworkPlayer(slot);
    public void ForceRespawnLocalPlayer() => NetworkPlayerRules.ForceRespawnLocalPlayer();
    public CharacterClassDefinition GetNetworkPlayerClassDefinition(byte slot) => NetworkPlayerRules.GetNetworkPlayerClassDefinition(slot);
    public PlayerTeam GetNetworkPlayerConfiguredTeam(byte slot) => NetworkPlayerRules.GetNetworkPlayerConfiguredTeam(slot);
    public static string GetNetworkPlayerDefaultName(byte slot) => NetworkPlayerSystem.GetNetworkPlayerDefaultName(slot);
    public int GetNetworkPlayerPingMilliseconds(byte slot) => NetworkPlayerRules.GetNetworkPlayerPingMilliseconds(slot);
    public int GetNetworkPlayerRespawnTicks(byte slot) => NetworkPlayerRules.GetNetworkPlayerRespawnTicks(slot);
    public bool IsNetworkPlayerAutomaticRespawnSuppressed(PlayerEntity player) => NetworkPlayerRules.IsNetworkPlayerAutomaticRespawnSuppressed(player);
    public bool IsNetworkPlayerAwaitingJoin(byte slot) => NetworkPlayerRules.IsNetworkPlayerAwaitingJoin(slot);
    public bool IsNetworkPlayerBot(byte slot) => NetworkPlayerRules.IsNetworkPlayerBot(slot);
    public static bool IsPlayableNetworkPlayerSlot(byte slot) => NetworkPlayerSystem.IsPlayableNetworkPlayerSlot(slot);
    public void PrepareLocalPlayerJoin() => NetworkPlayerRules.PrepareLocalPlayerJoin();
    public void SetEnemyInput(PlayerInputSnapshot input) => NetworkPlayerRules.SetEnemyInput(input);
    public void SetLocalInput(PlayerInputSnapshot input) => NetworkPlayerRules.SetLocalInput(input);
    public void SetLocalPlayerBadgeMask(ulong badgeMask) => NetworkPlayerRules.SetLocalPlayerBadgeMask(badgeMask);
    public void SetLocalPlayerChatBubble(int frameIndex) => NetworkPlayerRules.SetLocalPlayerChatBubble(frameIndex);
    public void SetLocalPlayerName(string displayName) => NetworkPlayerRules.SetLocalPlayerName(displayName);
    public void SetLocalPlayerTeam(PlayerTeam team) => NetworkPlayerRules.SetLocalPlayerTeam(team);
    public void SetLocalPreviousInput(PlayerInputSnapshot input) => NetworkPlayerRules.SetLocalPreviousInput(input);
    public void SetNetworkPlayerIsTypingChatMessage(byte slot, bool isTyping) => NetworkPlayerRules.SetNetworkPlayerIsTypingChatMessage(slot, isTyping);
    public void SetPendingLocalPlayerClass(PlayerClass playerClass) => NetworkPlayerRules.SetPendingLocalPlayerClass(playerClass);
    public void SetPendingLocalPlayerClass(string gameplayClassId) => NetworkPlayerRules.SetPendingLocalPlayerClass(gameplayClassId);
    public bool TryApplyNetworkPlayerClassSelection(byte slot, PlayerClass playerClass) => NetworkPlayerRules.TryApplyNetworkPlayerClassSelection(slot, playerClass);
    public bool TryApplyNetworkPlayerClassSelection(byte slot, string gameplayClassId) => NetworkPlayerRules.TryApplyNetworkPlayerClassSelection(slot, gameplayClassId);
    public bool TryClearNetworkPlayerInputOverride(byte slot) => NetworkPlayerRules.TryClearNetworkPlayerInputOverride(slot);
    public bool TryClearNetworkPlayerSpawnOverride(byte slot) => NetworkPlayerRules.TryClearNetworkPlayerSpawnOverride(slot);
    public bool TryForceNetworkPlayerClassSelectionAndRespawn(byte slot, PlayerClass playerClass) => NetworkPlayerRules.TryForceNetworkPlayerClassSelectionAndRespawn(slot, playerClass);
    public bool TryForceNetworkPlayerClassSelectionAndRespawn(byte slot, string gameplayClassId) => NetworkPlayerRules.TryForceNetworkPlayerClassSelectionAndRespawn(slot, gameplayClassId);
    public bool TryGetNetworkPlayer(byte slot, out PlayerEntity player) => NetworkPlayerRules.TryGetNetworkPlayer(slot, out player);
    public bool TryGetPlayerNetworkSlot(PlayerEntity player, out byte slot) => NetworkPlayerRules.TryGetPlayerNetworkSlot(player, out slot);
    public bool TryGrantNetworkPlayerGameplayItem(byte slot, string itemId) => NetworkPlayerRules.TryGrantNetworkPlayerGameplayItem(slot, itemId);
    public bool TryPrepareNetworkPlayerJoin(byte slot) => NetworkPlayerRules.TryPrepareNetworkPlayerJoin(slot);
    public bool TryReleaseNetworkPlayerSlot(byte slot) => NetworkPlayerRules.TryReleaseNetworkPlayerSlot(slot);
    public bool TryRequestNetworkPlayerTeamSelection(byte slot, PlayerTeam team) => NetworkPlayerRules.TryRequestNetworkPlayerTeamSelection(slot, team);
    public bool TryRevokeNetworkPlayerGameplayItem(byte slot, string itemId) => NetworkPlayerRules.TryRevokeNetworkPlayerGameplayItem(slot, itemId);
    public bool TrySetNetworkPlayerAutomaticRespawnSuppressed(byte slot, bool suppressed) => NetworkPlayerRules.TrySetNetworkPlayerAutomaticRespawnSuppressed(slot, suppressed);
    public bool TrySetNetworkPlayerAwaitingJoin(byte slot, bool awaitingJoin) => NetworkPlayerRules.TrySetNetworkPlayerAwaitingJoin(slot, awaitingJoin);
    public bool TrySetNetworkPlayerBadgeMask(byte slot, ulong badgeMask) => NetworkPlayerRules.TrySetNetworkPlayerBadgeMask(slot, badgeMask);
    public bool TrySetNetworkPlayerClassDefinition(byte slot, CharacterClassDefinition definition) => NetworkPlayerRules.TrySetNetworkPlayerClassDefinition(slot, definition);
    public bool TrySetNetworkPlayerConfiguredTeam(byte slot, PlayerTeam team) => NetworkPlayerRules.TrySetNetworkPlayerConfiguredTeam(slot, team);
    public bool TrySetNetworkPlayerGameplayAcquiredItem(byte slot, string? itemId) => NetworkPlayerRules.TrySetNetworkPlayerGameplayAcquiredItem(slot, itemId);
    public bool TrySetNetworkPlayerGameplayEquippedSlot(byte slot, GameplayEquipmentSlot equippedSlot) => NetworkPlayerRules.TrySetNetworkPlayerGameplayEquippedSlot(slot, equippedSlot);
    public bool TrySetNetworkPlayerGameplayLoadout(byte slot, string loadoutId) => NetworkPlayerRules.TrySetNetworkPlayerGameplayLoadout(slot, loadoutId);
    public bool TrySetNetworkPlayerGameplayPrimaryItem(byte slot, string itemId, bool refillAmmo = true) => NetworkPlayerRules.TrySetNetworkPlayerGameplayPrimaryItem(slot, itemId, refillAmmo);
    public bool TrySetNetworkPlayerGameplaySecondaryItem(byte slot, string? itemId) => NetworkPlayerRules.TrySetNetworkPlayerGameplaySecondaryItem(slot, itemId);
    public bool TrySetNetworkPlayerInput(byte slot, PlayerInputSnapshot input) => NetworkPlayerRules.TrySetNetworkPlayerInput(slot, input);
    public bool TrySetNetworkPlayerInput(byte slot, PlayerInputSnapshot input, InputButtons forcedPressedButtons, bool requireExplicitPresses = false) => NetworkPlayerRules.TrySetNetworkPlayerInput(slot, input, forcedPressedButtons, requireExplicitPresses);
    public bool TrySetNetworkPlayerName(byte slot, string displayName) => NetworkPlayerRules.TrySetNetworkPlayerName(slot, displayName);
    public bool TrySetNetworkPlayerRespawnTicks(byte slot, int ticks) => NetworkPlayerRules.TrySetNetworkPlayerRespawnTicks(slot, ticks);
    public bool TrySetNetworkPlayerSpawnOverride(byte slot, float x, float y) => NetworkPlayerRules.TrySetNetworkPlayerSpawnOverride(slot, x, y);
    public bool TrySetNetworkPlayerTeam(byte slot, PlayerTeam team, bool respawnLivePlayerImmediately = false) => NetworkPlayerRules.TrySetNetworkPlayerTeam(slot, team, respawnLivePlayerImmediately);
    public bool TryTriggerNetworkPlayerChatBubble(byte slot, int frameIndex) => NetworkPlayerRules.TryTriggerNetworkPlayerChatBubble(slot, frameIndex);
}
