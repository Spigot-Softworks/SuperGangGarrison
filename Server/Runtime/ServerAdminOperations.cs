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
    Action<ServerTransportPeer, IProtocolMessage> sendMessage,
    Func<IReadOnlyDictionary<byte, ClientSession>> clientsGetter,
    Func<ServerSessionManager> sessionManagerGetter,
    Func<SimulationWorld> worldGetter,
    Func<GameplayOwnershipService?> gameplayOwnershipServiceGetter,
    Func<MapRotationManager> mapRotationManagerGetter,
    Func<SnapshotBroadcaster> snapshotBroadcasterGetter,
    Func<ServerBotManager> botManagerGetter,
    Action<ChatRelayMessage>? broadcastSystemChatMessage = null,
    Action<MapChangeTransition>? applyMapTransition = null,
    ServerBanService? banService = null,
    Func<string>? demoRecordingStatusGetter = null,
    Func<string?, OpenGarrisonServerDemoRecordingResult>? demoRecordingStarter = null,
    Func<OpenGarrisonServerDemoRecordingResult>? demoRecordingStopper = null,
    Func<byte, PlayerTeam, OpenGarrisonServerDecisionResult>? beforeTeamChange = null,
    Func<byte, PlayerClass, OpenGarrisonServerDecisionResult>? beforeClassChange = null,
    Func<byte, string, OpenGarrisonServerDecisionResult>? beforeLoadoutChange = null,
    Func<string, int, bool, OpenGarrisonServerDecisionResult>? beforeMapChange = null) : IOpenGarrisonServerAdminOperations
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
        for (var index = 0; index < messageSegments.Count; index += 1)
        {
            var relay = new ChatRelayMessage(0, "[server]", messageSegments[index]);
            foreach (var client in clientsGetter().Values)
            {
                TrySend(client.Peer, relay);
            }

            broadcastSystemChatMessage?.Invoke(relay);
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
            TrySend(client.Peer, new ChatRelayMessage(0, "[server]", messageSegments[index]));
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
        TrySend(client.Peer, new ConnectionDeniedMessage(finalReason));
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

        if (client.UdpEndPoint is not { } clientEndPoint)
        {
            return new OpenGarrisonServerBanActionResult(false, string.Empty, "That client transport does not expose an IP address.", false, 0);
        }

        var result = banService.BanIpAddress(clientEndPoint.Address, duration, reason, "admin", client.Name);
        if (!result.Success)
        {
            return result;
        }

        var disconnectReason = banService.GetConnectionDeniedReason(clientEndPoint) ?? "You are banned from this server.";
        TrySend(client.Peer, new ConnectionDeniedMessage(disconnectReason));
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
                client.UdpEndPoint is { } endPoint
                    ? endPoint.Address.IsIPv4MappedToIPv6 ? endPoint.Address.MapToIPv4().ToString() : endPoint.Address.ToString()
                    : string.Empty,
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
                var disconnectReason = client.UdpEndPoint is { } endPoint
                    ? banService.GetConnectionDeniedReason(endPoint) ?? "You are banned from this server."
                    : "You are banned from this server.";
                TrySend(client.Peer, new ConnectionDeniedMessage(disconnectReason));
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

    public bool TrySetTeam(byte slot, PlayerTeam team)
    {
        if (IsCancelled(beforeTeamChange?.Invoke(slot, team), $"team change for slot {slot} to {team}"))
        {
            return false;
        }

        return sessionManagerGetter().TrySetClientTeam(slot, team);
    }

    public bool TrySetClass(byte slot, PlayerClass playerClass)
    {
        if (IsCancelled(beforeClassChange?.Invoke(slot, playerClass), $"class change for slot {slot} to {playerClass}"))
        {
            return false;
        }

        return sessionManagerGetter().TrySetClientClass(slot, playerClass);
    }

    public bool TrySetGameplayLoadout(byte slot, string loadoutId)
    {
        if (!SimulationWorld.IsPlayableNetworkPlayerSlot(slot)
            || string.IsNullOrWhiteSpace(loadoutId))
        {
            return false;
        }

        if (IsCancelled(beforeLoadoutChange?.Invoke(slot, loadoutId), $"loadout change for slot {slot} to {loadoutId}"))
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

    public bool TrySetPlayerMovementSpeedScale(byte slot, float scale)
    {
        if (!SimulationWorld.IsPlayableNetworkPlayerSlot(slot))
        {
            return false;
        }

        var world = worldGetter();
        if (!world.TrySetNetworkPlayerMovementSpeedScale(slot, scale))
        {
            return false;
        }

        log($"[server] movement speed scale for slot {slot} set to {world.GetNetworkPlayerMovementSpeedScale(slot):G9}");
        return true;
    }

    public bool TryClearPlayerMovementSpeedScale(byte slot)
    {
        if (!SimulationWorld.IsPlayableNetworkPlayerSlot(slot))
        {
            return false;
        }

        var world = worldGetter();
        if (!world.TryClearNetworkPlayerMovementSpeedScale(slot))
        {
            return false;
        }

        log($"[server] movement speed scale override for slot {slot} cleared");
        return true;
    }

    public bool TrySetPlayerGravityScale(byte slot, float scale)
    {
        if (!SimulationWorld.IsPlayableNetworkPlayerSlot(slot))
        {
            return false;
        }

        var world = worldGetter();
        if (!world.TrySetNetworkPlayerGravityScale(slot, scale))
        {
            return false;
        }

        log($"[server] gravity scale for slot {slot} set to {world.GetNetworkPlayerGravityScale(slot):G9}");
        return true;
    }

    public bool TryClearPlayerGravityScale(byte slot)
    {
        if (!SimulationWorld.IsPlayableNetworkPlayerSlot(slot))
        {
            return false;
        }

        var world = worldGetter();
        if (!world.TryClearNetworkPlayerGravityScale(slot))
        {
            return false;
        }

        log($"[server] gravity scale override for slot {slot} cleared");
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
        if (IsCancelled(beforeMapChange?.Invoke(levelName, mapAreaIndex, preservePlayerStats), $"map change to {levelName} area {mapAreaIndex}"))
        {
            return false;
        }

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

    private bool IsCancelled(OpenGarrisonServerDecisionResult? decision, string action)
    {
        if (decision is not { IsCancelled: true } cancelledDecision)
        {
            return false;
        }

        log(string.IsNullOrWhiteSpace(cancelledDecision.Reason)
            ? $"[plugin] cancelled {action}."
            : $"[plugin] cancelled {action}: {cancelledDecision.Reason}");
        return true;
    }

    public bool TrySetNextRoundMap(string levelName, int mapAreaIndex = 1)
    {
        return mapRotationManagerGetter().TrySetNextRoundMap(levelName, mapAreaIndex);
    }

    // Bot management
    public bool TryAddBot(byte slot, PlayerTeam team, PlayerClass playerClass, string displayName)
    {
        var botManager = botManagerGetter();
        var result = botManager.TryAddBot(slot, team, playerClass, displayName);
        if (result)
        {
            var resolvedDisplayName = botManager.BotSlots.TryGetValue(slot, out var state)
                ? state.DisplayName
                : displayName;
            log($"[server] added bot slot={slot} team={team} class={playerClass} name=\"{resolvedDisplayName}\"");
        }
        else
        {
            log($"[server] failed to add bot slot={slot}: slot unavailable or invalid");
        }
        return result;
    }

    public bool TryRemoveBot(byte slot)
    {
        var result = botManagerGetter().TryRemoveBot(slot);
        if (result)
        {
            log($"[server] removed bot slot={slot}");
        }
        else
        {
            log($"[server] failed to remove bot slot={slot}: no bot in that slot");
        }
        return result;
    }

    public bool TrySetBotTeam(byte slot, PlayerTeam team)
    {
        var result = botManagerGetter().TrySetBotTeam(slot, team);
        if (result)
        {
            log($"[server] set bot slot={slot} team={team}");
        }
        return result;
    }

    public bool TrySetBotClass(byte slot, PlayerClass playerClass)
    {
        var result = botManagerGetter().TrySetBotClass(slot, playerClass);
        if (result)
        {
            log($"[server] set bot slot={slot} class={playerClass}");
        }
        return result;
    }

    public int TryFillBots(int targetPerTeam, PlayerClass? requestedClass)
    {
        var addedCount = botManagerGetter().FillBots(targetPerTeam, requestedClass);
        log($"[server] fillbots target={targetPerTeam} per team, added {addedCount} bots");
        return addedCount;
    }

    public int TryFillBotTeam(PlayerTeam team, int targetCount, PlayerClass? requestedClass)
    {
        var addedCount = botManagerGetter().TryFillTeam(team, targetCount, requestedClass);
        log($"[server] fillbots target={targetCount} team={team}, added {addedCount} bots");
        return addedCount;
    }

    public IReadOnlyList<OpenGarrisonServerBotSlotInfo> GetBotSlots()
    {
        return botManagerGetter()
            .BotSlots
            .Values
            .OrderBy(state => state.Slot)
            .Select(state => new OpenGarrisonServerBotSlotInfo(state.Slot, state.Team, state.ClassId, state.DisplayName))
            .ToArray();
    }

    public int TryClearAllBots()
    {
        var count = botManagerGetter().BotSlots.Count;
        botManagerGetter().ClearAllBots();
        log($"[server] clearbots removed {count} bots");
        return count;
    }

    public string GetDemoRecordingStatus()
    {
        return demoRecordingStatusGetter?.Invoke() ?? "[server] demo | status=unavailable";
    }

    public OpenGarrisonServerDemoRecordingResult TryStartDemoRecording(string? requestedPath)
    {
        return demoRecordingStarter?.Invoke(requestedPath)
            ?? new OpenGarrisonServerDemoRecordingResult(false, string.Empty, "Demo recording is not configured.");
    }

    public OpenGarrisonServerDemoRecordingResult TryStopDemoRecording()
    {
        return demoRecordingStopper?.Invoke()
            ?? new OpenGarrisonServerDemoRecordingResult(false, string.Empty, "Demo recording is not configured.");
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

    private void TrySend(ServerTransportPeer peer, IProtocolMessage message)
    {
        try
        {
            sendMessage(peer, message);
        }
        catch (Exception ex)
        {
            log($"[server] failed to send admin message: {ex.Message}");
        }
    }

    private static List<string> SplitSystemMessageSegments(string text)
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
