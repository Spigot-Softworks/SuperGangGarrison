using System.Net;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;
using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server;

internal sealed class ServerAdminOperations(
    Action<string> log,
    Action<IPEndPoint, IProtocolMessage> sendMessage,
    Func<IReadOnlyDictionary<byte, ClientSession>> clientsGetter,
    Func<ServerSessionManager> sessionManagerGetter,
    Func<SimulationWorld> worldGetter,
    Func<GameplayOwnershipService?> gameplayOwnershipServiceGetter,
    Func<MapRotationManager> mapRotationManagerGetter,
    Func<SnapshotBroadcaster> snapshotBroadcasterGetter,
    Action<MapChangeTransition>? applyMapTransition = null) : IOpenGarrisonServerAdminOperations
{
    public void BroadcastSystemMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var relay = new ChatRelayMessage(0, "[server]", text.Trim());
        foreach (var client in clientsGetter().Values)
        {
            sendMessage(client.EndPoint, relay);
        }

        log($"[server] system message: {text.Trim()}");
    }

    public void SendSystemMessage(byte slot, string text)
    {
        if (string.IsNullOrWhiteSpace(text) || !clientsGetter().TryGetValue(slot, out var client))
        {
            return;
        }

        sendMessage(client.EndPoint, new ChatRelayMessage(0, "[server]", text.Trim()));
        log($"[server] system message to slot {slot}: {text.Trim()}");
    }

    public bool TryDisconnect(byte slot, string reason)
    {
        if (!clientsGetter().TryGetValue(slot, out var client))
        {
            return false;
        }

        sendMessage(client.EndPoint, new ConnectionDeniedMessage(reason));
        sessionManagerGetter().RemoveClient(slot, reason);
        return true;
    }

    public bool TryMoveToSpectator(byte slot) => sessionManagerGetter().TryMoveClientToSpectator(slot);

    public bool TrySetTeam(byte slot, PlayerTeam team) => sessionManagerGetter().TrySetClientTeam(slot, team);

    public bool TrySetClass(byte slot, PlayerClass playerClass) => sessionManagerGetter().TrySetClientClass(slot, playerClass);

    public bool TrySetGameplayLoadout(byte slot, string loadoutId)
    {
        if (!SimulationWorld.IsPlayableNetworkPlayerSlot(slot)
            || string.IsNullOrWhiteSpace(loadoutId))
        {
            return false;
        }

        var world = worldGetter();
        if (!world.TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        if (!TryResolveGameplayLoadoutSelection(player.ClassId, loadoutId, out var resolvedLoadoutId))
        {
            return false;
        }

        return world.TrySetNetworkPlayerGameplayLoadout(slot, resolvedLoadoutId);
    }

    public bool TrySetGameplaySecondaryItem(byte slot, string? itemId)
    {
        return SimulationWorld.IsPlayableNetworkPlayerSlot(slot)
            && worldGetter().TrySetNetworkPlayerGameplaySecondaryItem(slot, itemId);
    }

    public bool TrySetGameplayAcquiredItem(byte slot, string? itemId)
    {
        return SimulationWorld.IsPlayableNetworkPlayerSlot(slot)
            && worldGetter().TrySetNetworkPlayerGameplayAcquiredItem(slot, itemId);
    }

    public bool TryGrantGameplayItem(byte slot, string itemId)
    {
        return SimulationWorld.IsPlayableNetworkPlayerSlot(slot)
            && !string.IsNullOrWhiteSpace(itemId)
            && (gameplayOwnershipServiceGetter()?.TryGrantItem(slot, itemId) ?? worldGetter().TryGrantNetworkPlayerGameplayItem(slot, itemId));
    }

    public bool TryRevokeGameplayItem(byte slot, string itemId)
    {
        return SimulationWorld.IsPlayableNetworkPlayerSlot(slot)
            && !string.IsNullOrWhiteSpace(itemId)
            && (gameplayOwnershipServiceGetter()?.TryRevokeItem(slot, itemId) ?? worldGetter().TryRevokeNetworkPlayerGameplayItem(slot, itemId));
    }

    public bool TrySetGameplayEquippedSlot(byte slot, GameplayEquipmentSlot equippedSlot)
    {
        return SimulationWorld.IsPlayableNetworkPlayerSlot(slot)
            && worldGetter().TrySetNetworkPlayerGameplayEquippedSlot(slot, equippedSlot);
    }

    public bool TryForceKill(byte slot)
    {
        if (!SimulationWorld.IsPlayableNetworkPlayerSlot(slot))
        {
            return false;
        }

        return worldGetter().ForceKillNetworkPlayer(slot);
    }

    public bool TrySetCapLimit(int capLimit)
    {
        if (capLimit is < 1 or > 255)
        {
            return false;
        }

        var world = worldGetter();
        world.SetCapLimit(capLimit);
        log($"[server] cap limit set to {world.MatchRules.CapLimit}");
        return true;
    }

    public bool TryChangeMap(string levelName, int mapAreaIndex = 1, bool preservePlayerStats = false)
    {
        var world = worldGetter();
        var transition = new MapChangeTransition(
            world.Level.Name,
            world.Level.MapAreaIndex,
            world.Level.MapAreaCount,
            levelName,
            mapAreaIndex,
            preservePlayerStats,
            world.MatchState.WinnerTeam);
        if (!world.TryLoadLevel(levelName, mapAreaIndex, preservePlayerStats))
        {
            return false;
        }

        if (!preservePlayerStats)
        {
            world.ResetPlayersToAwaitingJoinForFreshMap();
        }

        applyMapTransition?.Invoke(transition);
        mapRotationManagerGetter().ClearQueuedNextRoundMap();
        mapRotationManagerGetter().AlignCurrentMap(levelName);
        snapshotBroadcasterGetter().ResetTransientEvents();
        log($"[server] admin changed map to {world.Level.Name} area {world.Level.MapAreaIndex}/{world.Level.MapAreaCount}");
        return true;
    }

    public bool TrySetNextRoundMap(string levelName, int mapAreaIndex = 1)
    {
        return mapRotationManagerGetter().TrySetNextRoundMap(levelName, mapAreaIndex);
    }

    private static bool TryResolveGameplayLoadoutSelection(PlayerClass playerClass, string selection, out string loadoutId)
    {
        return GameplayLoadoutSelectionResolver.TryResolveLoadoutId(playerClass, selection, out loadoutId);
    }
}
