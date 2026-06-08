using System.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server;

internal sealed class ServerOutboundMessaging(
    IServerMessageTransport transport,
    string serverName,
    SimulationWorld world,
    Dictionary<byte, ClientSession> clientsBySlot,
    int maxPlayableClients,
    Func<ServerAdminChatRouter?> adminChatRouterGetter,
    Func<PluginHost?> pluginHostGetter,
    Action<string, (string Key, object? Value)[]> writeEvent,
    Action<string> log,
    Action<IProtocolMessage>? recordBroadcastMessage = null)
{
    private readonly Dictionary<byte, ServerCustomBubbleState> _customBubblesBySlot = new();

    public void SendMessage(ServerTransportPeer remotePeer, IProtocolMessage message)
    {
        var payload = ProtocolCodec.Serialize(message, ServerProtocolCompression.Settings);
        SendPayload(
            remotePeer,
            payload,
            message is SnapshotMessage ? MessageType.Snapshot : null);
    }

    public void SendSnapshotPayload(ServerTransportPeer remotePeer, SnapshotMessage _, byte[] payload)
    {
        SendPayload(remotePeer, payload, MessageType.Snapshot);
    }

    public void SendServerStatus(ServerTransportPeer remotePeer)
    {
        var playerCount = clientsBySlot.Count;
        var spectatorCount = clientsBySlot.Keys.Count(ServerHelpers.IsSpectatorSlot);
        SendMessage(
            remotePeer,
            new ServerStatusResponseMessage(
                serverName,
                world.Level.Name,
                (byte)world.MatchRules.Mode,
                playerCount - spectatorCount,
                maxPlayableClients,
                spectatorCount));
    }

    public void SendServerDetails(ServerTransportPeer remotePeer)
    {
        var spectatorCount = clientsBySlot.Keys.Count(ServerHelpers.IsSpectatorSlot);
        SendMessage(
            remotePeer,
            new ServerDetailsResponseMessage(
                serverName,
                world.Level.Name,
                (byte)world.MatchRules.Mode,
                clientsBySlot.Count - spectatorCount,
                maxPlayableClients,
                spectatorCount,
                world.RedCaps,
                world.BlueCaps,
                world.MatchState.TimeRemainingTicks,
                world.MatchRules.TimeLimitTicks,
                world.Config.TicksPerSecond,
                BuildServerDetailsRoster()));
    }

    public void BroadcastChat(ClientSession client, string text, bool teamOnly)
    {
        var sanitized = text.Trim();
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return;
        }

        if (sanitized.Length > 120)
        {
            sanitized = sanitized[..120];
        }

        if (adminChatRouterGetter()?.TryHandlePrivateChatCommand(client, sanitized, teamOnly) == true)
        {
            return;
        }

        var team = TryGetClientChatTeam(client) is { } resolvedTeam
            ? (byte)resolvedTeam
            : (byte)0;
        var chatEvent = new ChatReceivedEvent(
            client.Slot,
            client.Name,
            sanitized,
            team == 0 ? null : (PlayerTeam)team,
            teamOnly);
        if (pluginHostGetter()?.TryHandleChatMessage(chatEvent) == true)
        {
            return;
        }

        writeEvent(
            "chat_received",
            [
                ("slot", client.Slot),
                ("player_name", client.Name),
                ("team", team == 0 ? null : ((PlayerTeam)team).ToString()),
                ("team_only", teamOnly),
                ("text", sanitized)
            ]);
        pluginHostGetter()?.NotifyChatReceived(chatEvent);
        var relay = new ChatRelayMessage(team, client.Name, sanitized, teamOnly, client.Slot);
        foreach (var session in clientsBySlot.Values)
        {
            if (teamOnly)
            {
                var sessionTeam = TryGetClientChatTeam(session);
                if (team == 0)
                {
                    if (session.Slot != client.Slot)
                    {
                        continue;
                    }
                }
                else if (sessionTeam != (PlayerTeam)team)
                {
                    continue;
                }
            }

            TrySendMessage(session.Peer, relay, "chat relay");
        }

        log(teamOnly
            ? $"[team chat] {client.Name}: {sanitized}"
            : $"[chat] {client.Name}: {sanitized}");
        recordBroadcastMessage?.Invoke(relay);
    }

    public void SendPluginMessage(
        byte slot,
        string sourcePluginId,
        string targetPluginId,
        string messageType,
        string payload,
        PluginMessagePayloadFormat payloadFormat,
        ushort schemaVersion)
    {
        if (!clientsBySlot.TryGetValue(slot, out var client) || !client.IsAuthorized)
        {
            return;
        }

        TrySendMessage(
            client.Peer,
            new ServerPluginMessage(sourcePluginId, targetPluginId, messageType, payload, payloadFormat, schemaVersion),
            "plugin message");
    }

    public void BroadcastPlayerSocialProfiles()
    {
        var profiles = BuildPlayerSocialProfiles();
        if (profiles.Count == 0)
        {
            return;
        }

        BroadcastPlayerSocialProfileUpdate(new PlayerSocialProfileUpdateMessage(profiles, Array.Empty<byte>()));
    }

    public void BroadcastPlayerSocialProfileRemoval(byte slot)
    {
        BroadcastPlayerSocialProfileUpdate(new PlayerSocialProfileUpdateMessage(
            Array.Empty<PlayerSocialProfileState>(),
            [slot]));
    }

    public void ReceiveCustomBubbleUpload(ClientSession client, CustomBubbleUploadMessage upload)
    {
        if (!client.IsAuthorized
            || upload.Slot >= ChatBubbleFrameCatalog.CustomBubbleSlotCount
            || upload.Rgba64Pixels.Length != ProtocolCodec.CustomBubbleRgba64PayloadBytes)
        {
            return;
        }

        var pixels = (byte[])upload.Rgba64Pixels.Clone();
        _customBubblesBySlot[client.Slot] = new ServerCustomBubbleState(upload.Slot, upload.Revision, pixels);
        BroadcastCustomBubbleState(client.Slot, upload.Slot, upload.Revision, pixels);
    }

    public void ReceiveCustomBubbleClear(ClientSession client)
    {
        if (!client.IsAuthorized)
        {
            return;
        }

        BroadcastCustomBubbleClear(client.Slot);
    }

    public void BroadcastCustomBubbleClear(byte slot)
    {
        _customBubblesBySlot.Remove(slot);
        var message = new CustomBubbleClearMessage(slot);
        foreach (var client in clientsBySlot.Values)
        {
            if (client.IsAuthorized)
            {
                TrySendMessage(client.Peer, message, "custom bubble clear");
            }
        }

        recordBroadcastMessage?.Invoke(message);
    }

    public void SendCustomBubbleStatesToClient(ServerTransportPeer remotePeer)
    {
        foreach (var (slot, state) in _customBubblesBySlot)
        {
            TrySendMessage(
                remotePeer,
                new CustomBubbleStateMessage(slot, state.Slot, state.Revision, state.Rgba64Pixels),
                "custom bubble state");
        }
    }

    private List<PlayerSocialProfileState> BuildPlayerSocialProfiles()
    {
        var profiles = new List<PlayerSocialProfileState>(clientsBySlot.Count);
        foreach (var client in clientsBySlot.Values)
        {
            profiles.Add(new PlayerSocialProfileState(
                client.Slot,
                client.Name,
                client.FriendCode,
                client.PlayerCardJson));
        }

        return profiles;
    }

    private List<ServerDetailsRosterEntry> BuildServerDetailsRoster()
    {
        var entries = new List<ServerDetailsRosterEntry>(clientsBySlot.Count);
        foreach (var (_, client) in clientsBySlot.OrderBy(static pair => pair.Key))
        {
            var isSpectator = ServerHelpers.IsSpectatorSlot(client.Slot);
            if (!isSpectator && world.TryGetNetworkPlayer(client.Slot, out var player))
            {
                entries.Add(new ServerDetailsRosterEntry(
                    client.Slot,
                    client.Name,
                    (byte)player.Team,
                    (byte)player.ClassId,
                    IsSpectator: false,
                    player.IsAlive,
                    IsAwaitingJoin: false,
                    ClampToShort(player.Health),
                    ClampToShort(player.MaxHealth),
                    ClampToShort(player.Kills),
                    ClampToShort(player.Deaths),
                    ClampToShort(player.Assists),
                    ClampToShort(player.Caps),
                    player.Points));
                continue;
            }

            entries.Add(new ServerDetailsRosterEntry(
                client.Slot,
                client.Name,
                Team: 0,
                ClassId: 0,
                IsSpectator: true,
                IsAlive: false,
                IsAwaitingJoin: true,
                Health: 0,
                MaxHealth: 0,
                Kills: 0,
                Deaths: 0,
                Assists: 0,
                Caps: 0,
                Points: 0f));
        }

        return entries;
    }

    private static short ClampToShort(int value)
    {
        return (short)Math.Clamp(value, short.MinValue, short.MaxValue);
    }

    private void BroadcastPlayerSocialProfileUpdate(PlayerSocialProfileUpdateMessage message)
    {
        foreach (var client in clientsBySlot.Values)
        {
            TrySendMessage(client.Peer, message, "player social profile update");
        }

        recordBroadcastMessage?.Invoke(message);
    }

    private void BroadcastCustomBubbleState(byte playerSlot, byte slot, uint revision, byte[] pixels)
    {
        var message = new CustomBubbleStateMessage(playerSlot, slot, revision, pixels);
        foreach (var client in clientsBySlot.Values)
        {
            if (client.IsAuthorized)
            {
                TrySendMessage(client.Peer, message, "custom bubble state");
            }
        }

        recordBroadcastMessage?.Invoke(message);
    }

    public void BroadcastPluginMessage(
        string sourcePluginId,
        string targetPluginId,
        string messageType,
        string payload,
        PluginMessagePayloadFormat payloadFormat,
        ushort schemaVersion)
    {
        var message = new ServerPluginMessage(sourcePluginId, targetPluginId, messageType, payload, payloadFormat, schemaVersion);
        foreach (var client in clientsBySlot.Values)
        {
            if (!client.IsAuthorized)
            {
                continue;
            }

            TrySendMessage(client.Peer, message, "plugin message broadcast");
        }

        recordBroadcastMessage?.Invoke(message);
    }

    public void NotifyClientsOfShutdown()
    {
        if (clientsBySlot.Count == 0)
        {
            return;
        }

        foreach (var client in clientsBySlot.Values)
        {
            try
            {
                SendMessage(client.Peer, new ConnectionDeniedMessage("Server shutting down."));
            }
            catch
            {
            }
        }
    }

    private PlayerTeam? TryGetClientChatTeam(ClientSession client)
    {
        return SimulationWorld.IsPlayableNetworkPlayerSlot(client.Slot)
            && world.TryGetNetworkPlayer(client.Slot, out var player)
            ? player.Team
            : null;
    }

    private void SendPayload(ServerTransportPeer remotePeer, byte[] payload, MessageType? messageType = null)
    {
        transport.Send(remotePeer, payload, messageType);
    }

    private bool TrySendMessage(ServerTransportPeer remotePeer, IProtocolMessage message, string description)
    {
        try
        {
            SendMessage(remotePeer, message);
            return true;
        }
        catch (Exception ex)
        {
            log($"[server] failed to send {description} to {remotePeer}: {ex.Message}");
            return false;
        }
    }

    private sealed record ServerCustomBubbleState(byte Slot, uint Revision, byte[] Rgba64Pixels);
}
