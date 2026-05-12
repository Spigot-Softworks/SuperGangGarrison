using System.Linq;
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
    public void SendMessage(ServerTransportPeer remotePeer, IProtocolMessage message)
    {
        var payload = ProtocolCodec.Serialize(message, ServerProtocolCompression.Settings);
        SendPayload(remotePeer, payload);
    }

    public void SendSnapshotPayload(ServerTransportPeer remotePeer, SnapshotMessage _, byte[] payload)
    {
        SendPayload(remotePeer, payload);
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
        var relay = new ChatRelayMessage(team, client.Name, sanitized, teamOnly);
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

            SendMessage(session.Peer, relay);
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

        SendMessage(client.Peer, new ServerPluginMessage(sourcePluginId, targetPluginId, messageType, payload, payloadFormat, schemaVersion));
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

            SendMessage(client.Peer, message);
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

    private void SendPayload(ServerTransportPeer remotePeer, byte[] payload)
    {
        transport.Send(remotePeer, payload);
    }
}
