using System;
using System.Collections.Generic;
using System.Linq;
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
    Action<MapChangeTransition>? applyMapTransition = null,
    ServerBanService? banService = null) : IOpenGarrisonServerAdminOperations
{
    private const int MaxPlayerNameLength = 20;
    private const string DefaultPlayerName = "Player";

    public void BroadcastSystemMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var messageSegments = SplitSystemMessageSegments(text);
        foreach (var client in clientsGetter().Values)
        {
            for (var index = 0; index < messageSegments.Count; index += 1)
            {
                TrySend(client.EndPoint, new ChatRelayMessage(0, "[server]", messageSegments[index]));
            }
        }

        log($"[server] system message: {text.Trim()}");
    }

    public void SendSystemMessage(byte slot, string text)
    {
        if (string.IsNullOrWhiteSpace(text) || !clientsGetter().TryGetValue(slot, out var client))
        {
            return;
        }

        var messageSegments = SplitSystemMessageSegments(text);
        for (var index = 0; index < messageSegments.Count; index += 1)
        {
            TrySend(client.EndPoint, new ChatRelayMessage(0, "[server]", messageSegments[index]));
        }

        log($"[server] system message to slot {slot}: {text.Trim()}");
    }

    public bool TryRenamePlayer(byte slot, string newName)
    {
        if (!clientsGetter().TryGetValue(slot, out var client))
        {
            return false;
        }

        var sanitizedName = SanitizePlayerName(newName);
        client.Name = sanitizedName;
        sessionManagerGetter().ApplyClientProfile(slot, sanitizedName, client.BadgeMask);
        log($"[server] renamed slot {slot} to \"{sanitizedName}\"");
        return true;
    }

    public bool TryDisconnect(byte slot, string reason)
    {
        if (!clientsGetter().TryGetValue(slot, out var client))
        {
            return false;
        }

        var finalReason = ProtocolCodec.TruncateUtf8(string.IsNullOrWhiteSpace(reason) ? "Disconnected." : reason.Trim(), ProtocolCodec.MaxReasonBytes);
        TrySend(client.EndPoint, new ConnectionDeniedMessage(finalReason));
        sessionManagerGetter().RemoveClient(slot, finalReason);
        return true;
    }

    public OpenGarrisonServerBanActionResult TryBanPlayer(byte slot, TimeSpan? duration, string reason)
    {
        if (banService is null)
        {
            return new OpenGarrisonServerBanActionResult(false, string.Empty, "Ban service is not configured.", false, 0);
        }

        if (!clientsGetter().TryGetValue(slot, out var client))
        {
            return new OpenGarrisonServerBanActionResult(false, string.Empty, "No connected client for that slot.", false, 0);
        }

        var result = banService.BanIpAddress(client.EndPoint.Address, duration, reason, "admin", client.Name);
        if (!result.Success)
        {
            return result;
        }

        var disconnectReason = banService.GetConnectionDeniedReason(client.EndPoint) ?? "You are banned from this server.";
        TrySend(client.EndPoint, new ConnectionDeniedMessage(disconnectReason));
        sessionManagerGetter().RemoveClient(slot, disconnectReason);
        return result;
    }

    public OpenGarrisonServerBanActionResult TryBanIpAddress(string ipAddress, TimeSpan? duration, string reason)
    {
        if (banService is null)
        {
            return new OpenGarrisonServerBanActionResult(false, string.Empty, "Ban service is not configured.", false, 0);
        }

        var result = banService.BanIpAddress(ipAddress, duration, reason, "admin");
        if (!result.Success)
        {
            return result;
        }

        var disconnectedSlots = clientsGetter().Values
            .Where(client => string.Equals(
                client.EndPoint.Address.IsIPv4MappedToIPv6 ? client.EndPoint.Address.MapToIPv4().ToString() : client.EndPoint.Address.ToString(),
                result.Address,
                StringComparison.Ordinal))
            .Select(client => client.Slot)
            .Distinct()
            .ToArray();
        for (var index = 0; index < disconnectedSlots.Length; index += 1)
        {
            var slot = disconnectedSlots[index];
            if (clientsGetter().TryGetValue(slot, out var client))
            {
                var disconnectReason = banService.GetConnectionDeniedReason(client.EndPoint) ?? "You are banned from this server.";
                TrySend(client.EndPoint, new ConnectionDeniedMessage(disconnectReason));
                sessionManagerGetter().RemoveClient(slot, disconnectReason);
            }
        }

        return result;
    }

    public OpenGarrisonServerAddressActionResult TryUnbanIpAddress(string ipAddress)
    {
        return banService is null
            ? new OpenGarrisonServerAddressActionResult(false, string.Empty, "Ban service is not configured.")
            : banService.UnbanIpAddress(ipAddress);
    }

    public bool TrySetPlayerGagged(byte slot, bool isGagged)
    {
        if (!clientsGetter().TryGetValue(slot, out var client))
        {
            return false;
        }

        client.IsGagged = isGagged;
        log(isGagged
            ? $"[server] gagged slot {slot} ({client.Name})"
            : $"[server] ungagged slot {slot} ({client.Name})");
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

    public bool TryIgnitePlayer(byte slot, float durationSeconds)
    {
        if (!SimulationWorld.IsPlayableNetworkPlayerSlot(slot))
        {
            return false;
        }

        var world = worldGetter();
        if (!world.TryGetNetworkPlayer(slot, out var player)
            || !player.IsAlive
            || player.IsUbered)
        {
            return false;
        }

        var clampedDurationSeconds = float.Clamp(durationSeconds, 0.1f, 60f);
        player.IgniteAfterburn(
            ownerPlayerId: 0,
            durationIncreaseSourceTicks: clampedDurationSeconds * LegacyMovementModel.SourceTicksPerSecond,
            intensityIncrease: PlayerEntity.BurnMaxIntensity,
            afterburnFalloff: false,
            burnFalloffAmount: 0f);
        if (!player.IsBurning)
        {
            return false;
        }

        log($"[server] ignited slot {slot} ({player.DisplayName}) for {clampedDurationSeconds:G9}s");
        return true;
    }

    public bool TrySetPlayerScale(byte slot, float scale)
    {
        if (!SimulationWorld.IsPlayableNetworkPlayerSlot(slot))
        {
            return false;
        }

        var world = worldGetter();
        if (!world.TrySetNetworkPlayerScale(slot, scale))
        {
            return false;
        }

        log($"[server] player scale for slot {slot} set to {scale:G9}");
        return true;
    }

    public bool TrySetTimeLimit(int timeLimitMinutes)
    {
        if (timeLimitMinutes is < 1 or > 255)
        {
            return false;
        }

        var world = worldGetter();
        world.SetTimeLimitMinutes(timeLimitMinutes);
        log($"[server] time limit set to {world.MatchRules.TimeLimitMinutes} minutes");
        return true;
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

    public bool TrySetRespawnSeconds(int respawnSeconds)
    {
        if (respawnSeconds is < 0 or > 255)
        {
            return false;
        }

        var world = worldGetter();
        world.SetRespawnSeconds(respawnSeconds);
        log($"[server] respawn set to {world.ConfiguredRespawnSeconds} seconds");
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

    private static string SanitizePlayerName(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return DefaultPlayerName;
        }

        var sanitized = value.Replace("#", string.Empty).Trim();
        if (sanitized.Length == 0)
        {
            return DefaultPlayerName;
        }

        sanitized = ProtocolCodec.TruncateUtf8(sanitized, maxBytes: 80);
        return sanitized.Length > MaxPlayerNameLength
            ? sanitized[..MaxPlayerNameLength]
            : sanitized;
    }

    private void TrySend(IPEndPoint endPoint, IProtocolMessage message)
    {
        try
        {
            sendMessage(endPoint, message);
        }
        catch (Exception ex)
        {
            log($"[server] failed to send admin message: {ex.Message}");
        }
    }

    private static IReadOnlyList<string> SplitSystemMessageSegments(string text)
    {
        var normalized = text.Trim();
        if (normalized.Length == 0)
        {
            return [];
        }

        var segments = new List<string>();
        var remaining = normalized;
        while (remaining.Length > 0)
        {
            var next = ProtocolCodec.TruncateUtf8(remaining, ProtocolCodec.MaxChatBytes);
            if (next.Length == 0)
            {
                break;
            }

            if (next.Length < remaining.Length)
            {
                var preferredSplit = next.LastIndexOf(' ');
                if (preferredSplit > 0)
                {
                    var candidate = next[..preferredSplit].TrimEnd();
                    if (candidate.Length > 0)
                    {
                        next = candidate;
                    }
                }
            }

            segments.Add(next);
            remaining = remaining[next.Length..].TrimStart();
        }

        return segments.Count == 0 ? [ProtocolCodec.TruncateUtf8(normalized, ProtocolCodec.MaxChatBytes)] : segments;
    }
}
