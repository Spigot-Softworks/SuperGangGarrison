using System;
using System.Collections.Generic;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Server.Plugins;

public readonly record struct OpenGarrisonServerBotSlotInfo(
    byte Slot,
    PlayerTeam Team,
    PlayerClass PlayerClass,
    string DisplayName);

public readonly record struct OpenGarrisonServerDemoRecordingResult(
    bool Success,
    string Status,
    string Error);

public interface IOpenGarrisonServerAdminOperations
{
    void BroadcastSystemMessage(string text);

    void SendSystemMessage(byte slot, string text);

    bool TryRenamePlayer(byte slot, string newName);

    bool TryDisconnect(byte slot, string reason);

    OpenGarrisonServerBanActionResult TryBanPlayer(byte slot, TimeSpan? duration, string reason);

    OpenGarrisonServerBanActionResult TryBanIpAddress(string ipAddress, TimeSpan? duration, string reason);

    OpenGarrisonServerAddressActionResult TryUnbanIpAddress(string ipAddress);

    bool TrySetPlayerGagged(byte slot, bool isGagged);

    bool TryMoveToSpectator(byte slot);

    bool TrySetTeam(byte slot, PlayerTeam team);

    bool TrySetClass(byte slot, PlayerClass playerClass);

    bool TrySetGameplayLoadout(byte slot, string loadoutId);

    bool TrySetGameplaySecondaryItem(byte slot, string? itemId);

    bool TrySetGameplayAcquiredItem(byte slot, string? itemId);

    bool TryGrantGameplayItem(byte slot, string itemId);

    bool TryRevokeGameplayItem(byte slot, string itemId);

    bool TrySetGameplayEquippedSlot(byte slot, GameplayEquipmentSlot equippedSlot);

    bool TryForceKill(byte slot);

    bool TryExplodePlayer(byte slot) => false;

    bool TryBuildJumpPad(byte slot) => false;

    bool TrySetPlayerNoclip(byte slot, bool enabled) => false;

    bool TryTogglePlayerNoclip(byte slot, out bool enabled)
    {
        enabled = false;
        return false;
    }

    bool TrySetPlayerFrozen(byte slot, bool frozen) => false;

    bool TryTogglePlayerFrozen(byte slot, out bool frozen)
    {
        frozen = false;
        return false;
    }

    bool TryStunPlayer(byte slot, float durationSeconds) => false;

    bool TryTeleportPlayerToPlayer(byte sourceSlot, byte targetSlot) => false;

    bool TrySetPlayerRespawnPosition(byte slot, float x, float y) => false;

    bool TryIgnitePlayer(byte slot, float durationSeconds);

    bool TrySetPlayerScale(byte slot, float scale);

    bool TrySetPlayerMovementSpeedScale(byte slot, float scale);

    bool TryClearPlayerMovementSpeedScale(byte slot);

    bool TrySetPlayerGravityScale(byte slot, float scale);

    bool TryClearPlayerGravityScale(byte slot);

    bool TrySetTimeLimit(int timeLimitMinutes);

    bool TrySetCapLimit(int capLimit);

    bool TrySetRespawnSeconds(int respawnSeconds);

    bool TryChangeMap(string levelName, int mapAreaIndex = 1, bool preservePlayerStats = false);

    bool TrySetNextRoundMap(string levelName, int mapAreaIndex = 1);

    // Bot management
    bool TryAddBot(byte slot, PlayerTeam team, PlayerClass playerClass, string displayName);

    bool TryAddMimicBot(byte sourceSlot, PlayerTeam team, PlayerClass playerClass, string displayName, out byte botSlot)
    {
        botSlot = 0;
        return false;
    }

    bool TryAddFollowHealerBot(byte targetSlot, string displayName, out byte botSlot)
    {
        botSlot = 0;
        return false;
    }

    bool TryRemoveBot(byte slot);

    bool TrySetBotTeam(byte slot, PlayerTeam team);

    bool TrySetBotClass(byte slot, PlayerClass playerClass);

    int TryFillBots(int targetPerTeam, PlayerClass? requestedClass);

    int TryFillBotTeam(PlayerTeam team, int targetCount, PlayerClass? requestedClass);

    IReadOnlyList<OpenGarrisonServerBotSlotInfo> GetBotSlots();

    int TryClearAllBots();

    string GetDemoRecordingStatus();

    OpenGarrisonServerDemoRecordingResult TryStartDemoRecording(string? requestedPath);

    OpenGarrisonServerDemoRecordingResult TryStopDemoRecording();
}
