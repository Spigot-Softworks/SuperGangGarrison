using System;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Server.Plugins;

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

    bool TryIgnitePlayer(byte slot, float durationSeconds);

    bool TrySetPlayerScale(byte slot, float scale);

    bool TrySetTimeLimit(int timeLimitMinutes);

    bool TrySetCapLimit(int capLimit);

    bool TrySetRespawnSeconds(int respawnSeconds);

    bool TryChangeMap(string levelName, int mapAreaIndex = 1, bool preservePlayerStats = false);

    bool TrySetNextRoundMap(string levelName, int mapAreaIndex = 1);
}
