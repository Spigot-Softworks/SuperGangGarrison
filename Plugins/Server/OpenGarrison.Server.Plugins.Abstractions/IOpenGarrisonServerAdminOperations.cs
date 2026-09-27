using System;
using System.Collections.Generic;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Server.Plugins;

/// <summary>
/// A bot occupying a server slot.
/// </summary>
/// <param name="Slot">The bot's slot.</param>
/// <param name="Team">The bot's team.</param>
/// <param name="PlayerClass">The bot's class.</param>
/// <param name="DisplayName">The bot's display name.</param>
public readonly record struct OpenGarrisonServerBotSlotInfo(
    byte Slot,
    PlayerTeam Team,
    PlayerClass PlayerClass,
    string DisplayName);

/// <summary>
/// The result of a demo recording action.
/// </summary>
/// <param name="Success">Whether the action succeeded.</param>
/// <param name="Status">The recording status.</param>
/// <param name="Error">The error message when the action failed.</param>
public readonly record struct OpenGarrisonServerDemoRecordingResult(
    bool Success,
    string Status,
    string Error);

/// <summary>
/// Admin operations plugins can invoke on players, bots, matches, and recordings.
/// </summary>
public interface IOpenGarrisonServerAdminOperations
{
    /// <summary>
    /// Broadcasts a system message to all clients.
    /// </summary>
    /// <param name="text">The message text.</param>
    void BroadcastSystemMessage(string text);

    /// <summary>
    /// Sends a system message to the client on a slot.
    /// </summary>
    /// <param name="slot">The client slot.</param>
    /// <param name="text">The message text.</param>
    void SendSystemMessage(byte slot, string text);

    /// <summary>
    /// Tries to rename the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="newName">The new name.</param>
    /// <returns>True when the player was renamed.</returns>
    bool TryRenamePlayer(byte slot, string newName);

    /// <summary>
    /// Tries to disconnect the client on a slot.
    /// </summary>
    /// <param name="slot">The client slot.</param>
    /// <param name="reason">The disconnect reason.</param>
    /// <returns>True when the client was disconnected.</returns>
    bool TryDisconnect(byte slot, string reason);

    /// <summary>
    /// Tries to ban the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="duration">The ban duration, or null for permanent.</param>
    /// <param name="reason">The ban reason.</param>
    /// <returns>The ban result.</returns>
    OpenGarrisonServerBanActionResult TryBanPlayer(byte slot, TimeSpan? duration, string reason);

    /// <summary>
    /// Tries to ban an IP address.
    /// </summary>
    /// <param name="ipAddress">The IP address.</param>
    /// <param name="duration">The ban duration, or null for permanent.</param>
    /// <param name="reason">The ban reason.</param>
    /// <returns>The ban result.</returns>
    OpenGarrisonServerBanActionResult TryBanIpAddress(string ipAddress, TimeSpan? duration, string reason);

    /// <summary>
    /// Tries to unban an IP address.
    /// </summary>
    /// <param name="ipAddress">The IP address.</param>
    /// <returns>The unban result.</returns>
    OpenGarrisonServerAddressActionResult TryUnbanIpAddress(string ipAddress);

    /// <summary>
    /// Tries to gag or ungag the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="isGagged">Whether the player should be gagged.</param>
    /// <returns>True when the gag state was changed.</returns>
    bool TrySetPlayerGagged(byte slot, bool isGagged);

    /// <summary>
    /// Tries to move the player on a slot to spectator.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <returns>True when the player was moved.</returns>
    bool TryMoveToSpectator(byte slot);

    /// <summary>
    /// Tries to set the team of the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="team">The new team.</param>
    /// <returns>True when the team was set.</returns>
    bool TrySetTeam(byte slot, PlayerTeam team);

    /// <summary>
    /// Tries to set the class of the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="playerClass">The new class.</param>
    /// <returns>True when the class was set.</returns>
    bool TrySetClass(byte slot, PlayerClass playerClass);

    /// <summary>
    /// Tries to set the gameplay loadout of the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="loadoutId">The loadout id.</param>
    /// <returns>True when the loadout was set.</returns>
    bool TrySetGameplayLoadout(byte slot, string loadoutId);

    /// <summary>
    /// Tries to set the gameplay secondary item of the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="itemId">The item id, or null to clear.</param>
    /// <returns>True when the item was set.</returns>
    bool TrySetGameplaySecondaryItem(byte slot, string? itemId);

    /// <summary>
    /// Tries to set the gameplay acquired item of the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="itemId">The item id, or null to clear.</param>
    /// <returns>True when the item was set.</returns>
    bool TrySetGameplayAcquiredItem(byte slot, string? itemId);

    /// <summary>
    /// Tries to grant a gameplay item to the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="itemId">The item id.</param>
    /// <returns>True when the item was granted.</returns>
    bool TryGrantGameplayItem(byte slot, string itemId);

    /// <summary>
    /// Tries to revoke a gameplay item from the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="itemId">The item id.</param>
    /// <returns>True when the item was revoked.</returns>
    bool TryRevokeGameplayItem(byte slot, string itemId);

    /// <summary>
    /// Tries to set the equipped gameplay slot of the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="equippedSlot">The equipment slot.</param>
    /// <returns>True when the equipped slot was set.</returns>
    bool TrySetGameplayEquippedSlot(byte slot, GameplayEquipmentSlot equippedSlot);

    /// <summary>
    /// Tries to force-kill the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <returns>True when the player was killed.</returns>
    bool TryForceKill(byte slot);

    /// <summary>
    /// Tries to explode the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <returns>True when the player exploded.</returns>
    bool TryExplodePlayer(byte slot) => false;

    /// <summary>
    /// Tries to build a jump pad for the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <returns>True when the jump pad was built.</returns>
    bool TryBuildJumpPad(byte slot) => false;

    /// <summary>
    /// Tries to set noclip for the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="enabled">Whether noclip is enabled.</param>
    /// <returns>True when noclip was set.</returns>
    bool TrySetPlayerNoclip(byte slot, bool enabled) => false;

    /// <summary>
    /// Tries to toggle noclip for the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="enabled">The resulting noclip state.</param>
    /// <returns>True when noclip was toggled.</returns>
    bool TryTogglePlayerNoclip(byte slot, out bool enabled)
    {
        enabled = false;
        return false;
    }

    /// <summary>
    /// Tries to freeze or unfreeze the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="frozen">Whether the player should be frozen.</param>
    /// <returns>True when the frozen state was set.</returns>
    bool TrySetPlayerFrozen(byte slot, bool frozen) => false;

    /// <summary>
    /// Tries to toggle the frozen state of the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="frozen">The resulting frozen state.</param>
    /// <returns>True when the frozen state was toggled.</returns>
    bool TryTogglePlayerFrozen(byte slot, out bool frozen)
    {
        frozen = false;
        return false;
    }

    /// <summary>
    /// Tries to stun the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="durationSeconds">The stun duration in seconds.</param>
    /// <returns>True when the player was stunned.</returns>
    bool TryStunPlayer(byte slot, float durationSeconds) => false;

    /// <summary>
    /// Tries to teleport the player on a source slot to the player on a target slot.
    /// </summary>
    /// <param name="sourceSlot">The source player slot.</param>
    /// <param name="targetSlot">The target player slot.</param>
    /// <returns>True when the player was teleported.</returns>
    bool TryTeleportPlayerToPlayer(byte sourceSlot, byte targetSlot) => false;

    /// <summary>
    /// Tries to set the respawn position of the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="x">The world X.</param>
    /// <param name="y">The world Y.</param>
    /// <returns>True when the respawn position was set.</returns>
    bool TrySetPlayerRespawnPosition(byte slot, float x, float y) => false;

    /// <summary>
    /// Tries to ignite the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="durationSeconds">The burn duration in seconds.</param>
    /// <returns>True when the player was ignited.</returns>
    bool TryIgnitePlayer(byte slot, float durationSeconds);

    /// <summary>
    /// Tries to set the player scale of the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="scale">The scale.</param>
    /// <returns>True when the scale was set.</returns>
    bool TrySetPlayerScale(byte slot, float scale);

    /// <summary>
    /// Tries to set the movement speed scale of the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="scale">The movement speed scale.</param>
    /// <returns>True when the scale was set.</returns>
    bool TrySetPlayerMovementSpeedScale(byte slot, float scale);

    /// <summary>
    /// Tries to clear the movement speed scale override of the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <returns>True when the override was cleared.</returns>
    bool TryClearPlayerMovementSpeedScale(byte slot);

    /// <summary>
    /// Tries to set the gravity scale of the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="scale">The gravity scale.</param>
    /// <returns>True when the scale was set.</returns>
    bool TrySetPlayerGravityScale(byte slot, float scale);

    /// <summary>
    /// Tries to clear the gravity scale override of the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <returns>True when the override was cleared.</returns>
    bool TryClearPlayerGravityScale(byte slot);

    /// <summary>
    /// Tries to set the match time limit.
    /// </summary>
    /// <param name="timeLimitMinutes">The time limit in minutes.</param>
    /// <returns>True when the time limit was set.</returns>
    bool TrySetTimeLimit(int timeLimitMinutes);

    /// <summary>
    /// Tries to set the cap limit.
    /// </summary>
    /// <param name="capLimit">The cap limit.</param>
    /// <returns>True when the cap limit was set.</returns>
    bool TrySetCapLimit(int capLimit);

    /// <summary>
    /// Tries to set the respawn time.
    /// </summary>
    /// <param name="respawnSeconds">The respawn time in seconds.</param>
    /// <returns>True when the respawn time was set.</returns>
    bool TrySetRespawnSeconds(int respawnSeconds);

    /// <summary>
    /// Tries to change the map.
    /// </summary>
    /// <param name="levelName">The level name.</param>
    /// <param name="mapAreaIndex">The map area index.</param>
    /// <param name="preservePlayerStats">Whether player stats are preserved.</param>
    /// <returns>True when the map change started.</returns>
    bool TryChangeMap(string levelName, int mapAreaIndex = 1, bool preservePlayerStats = false);

    /// <summary>
    /// Tries to set the map for the next round.
    /// </summary>
    /// <param name="levelName">The level name.</param>
    /// <param name="mapAreaIndex">The map area index.</param>
    /// <returns>True when the next map was set.</returns>
    bool TrySetNextRoundMap(string levelName, int mapAreaIndex = 1);

    /// <summary>
    /// Tries to add a bot on a slot.
    /// </summary>
    /// <param name="slot">The slot for the bot.</param>
    /// <param name="team">The bot's team.</param>
    /// <param name="playerClass">The bot's class.</param>
    /// <param name="displayName">The bot's display name.</param>
    /// <returns>True when the bot was added.</returns>
    bool TryAddBot(byte slot, PlayerTeam team, PlayerClass playerClass, string displayName);

    /// <summary>
    /// Tries to add a bot that mimics the player on a source slot.
    /// </summary>
    /// <param name="sourceSlot">The slot to mimic.</param>
    /// <param name="team">The bot's team.</param>
    /// <param name="playerClass">The bot's class.</param>
    /// <param name="displayName">The bot's display name.</param>
    /// <param name="botSlot">The bot's slot.</param>
    /// <returns>True when the bot was added.</returns>
    bool TryAddMimicBot(byte sourceSlot, PlayerTeam team, PlayerClass playerClass, string displayName, out byte botSlot)
    {
        botSlot = 0;
        return false;
    }

    /// <summary>
    /// Tries to add a healer bot that follows the player on a target slot.
    /// </summary>
    /// <param name="targetSlot">The slot to follow.</param>
    /// <param name="displayName">The bot's display name.</param>
    /// <param name="botSlot">The bot's slot.</param>
    /// <returns>True when the bot was added.</returns>
    bool TryAddFollowHealerBot(byte targetSlot, string displayName, out byte botSlot)
    {
        botSlot = 0;
        return false;
    }

    /// <summary>
    /// Tries to remove the bot on a slot.
    /// </summary>
    /// <param name="slot">The bot's slot.</param>
    /// <returns>True when the bot was removed.</returns>
    bool TryRemoveBot(byte slot);

    /// <summary>
    /// Tries to set the team of the bot on a slot.
    /// </summary>
    /// <param name="slot">The bot's slot.</param>
    /// <param name="team">The new team.</param>
    /// <returns>True when the team was set.</returns>
    bool TrySetBotTeam(byte slot, PlayerTeam team);

    /// <summary>
    /// Tries to set the class of the bot on a slot.
    /// </summary>
    /// <param name="slot">The bot's slot.</param>
    /// <param name="playerClass">The new class.</param>
    /// <returns>True when the class was set.</returns>
    bool TrySetBotClass(byte slot, PlayerClass playerClass);

    /// <summary>
    /// Tries to fill both teams with bots up to a per-team target.
    /// </summary>
    /// <param name="targetPerTeam">The target bot count per team.</param>
    /// <param name="requestedClass">The requested class, if any.</param>
    /// <returns>The number of bots added.</returns>
    int TryFillBots(int targetPerTeam, PlayerClass? requestedClass);

    /// <summary>
    /// Tries to fill a team with bots up to a target count.
    /// </summary>
    /// <param name="team">The team.</param>
    /// <param name="targetCount">The target bot count.</param>
    /// <param name="requestedClass">The requested class, if any.</param>
    /// <returns>The number of bots added.</returns>
    int TryFillBotTeam(PlayerTeam team, int targetCount, PlayerClass? requestedClass);

    /// <summary>
    /// Gets the current bot slots.
    /// </summary>
    /// <returns>The bot slot snapshots.</returns>
    IReadOnlyList<OpenGarrisonServerBotSlotInfo> GetBotSlots();

    /// <summary>
    /// Tries to remove all bots.
    /// </summary>
    /// <returns>The number of bots removed.</returns>
    int TryClearAllBots();

    /// <summary>
    /// Gets the current demo recording status.
    /// </summary>
    /// <returns>The status text.</returns>
    string GetDemoRecordingStatus();

    /// <summary>
    /// Tries to start a demo recording.
    /// </summary>
    /// <param name="requestedPath">The requested output path, or null for the default.</param>
    /// <returns>The recording result.</returns>
    OpenGarrisonServerDemoRecordingResult TryStartDemoRecording(string? requestedPath);

    /// <summary>
    /// Tries to stop the current demo recording.
    /// </summary>
    /// <returns>The recording result.</returns>
    OpenGarrisonServerDemoRecordingResult TryStopDemoRecording();
}
