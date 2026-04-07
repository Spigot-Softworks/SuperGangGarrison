using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Server.Plugins;

public interface IOpenGarrisonServerAdminOperations
{
    void BroadcastSystemMessage(string text);

    void SendSystemMessage(byte slot, string text);

    bool TryDisconnect(byte slot, string reason);

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

    bool TrySetCapLimit(int capLimit);

    bool TryChangeMap(string levelName, int mapAreaIndex = 1, bool preservePlayerStats = false);

    bool TrySetNextRoundMap(string levelName, int mapAreaIndex = 1);
}
