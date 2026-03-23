using OpenGarrison.Core;

namespace OpenGarrison.Server.Plugins;

public interface IOpenGarrisonServerAdminOperations
{
    void BroadcastSystemMessage(string text);

    void SendSystemMessage(byte slot, string text);

    bool TryDisconnect(byte slot, string reason);

    bool TryMoveToSpectator(byte slot);

    bool TrySetTeam(byte slot, PlayerTeam team);

    bool TrySetClass(byte slot, PlayerClass playerClass);

    bool TryForceKill(byte slot);

    bool TrySetCapLimit(int capLimit);

    bool TryChangeMap(string levelName, int mapAreaIndex = 1, bool preservePlayerStats = false);

    bool TrySetNextRoundMap(string levelName, int mapAreaIndex = 1);
}
