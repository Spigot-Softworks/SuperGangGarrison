using System.Linq;
using System.Net;
using System.Net.Sockets;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server;

internal sealed class ServerOutboundMessaging(
    UdpClient udp,
    string serverName,
    SimulationWorld world,
    Dictionary<byte, ClientSession> clientsBySlot,
    int maxPlayableClients,
    Func<PluginHost?> pluginHostGetter,
    Action<string, (string Key, object? Value)[]> writeEvent,
    Action<string> log)
{
    public void SendMessage(IPEndPoint remoteEndPoint, IProtocolMessage message)
    {
        var payload = ProtocolCodec.Serialize(message);
        SendPayload(remoteEndPoint, payload);
    }

    public void SendSnapshotPayload(IPEndPoint remoteEndPoint, SnapshotMessage _, byte[] payload)
    {
        SendPayload(remoteEndPoint, payload);
    }

    public void SendServerStatus(IPEndPoint remoteEndPoint)
    {
        var playerCount = clientsBySlot.Count;
        var spectatorCount = clientsBySlot.Keys.Count(ServerHelpers.IsSpectatorSlot);
        SendMessage(
            remoteEndPoint,
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

            SendMessage(session.EndPoint, relay);
        }

        log(teamOnly
            ? $"[team chat] {client.Name}: {sanitized}"
            : $"[chat] {client.Name}: {sanitized}");
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
                SendMessage(client.EndPoint, new ConnectionDeniedMessage("Server shutting down."));
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

    private void SendPayload(IPEndPoint remoteEndPoint, byte[] payload)
    {
        udp.Send(payload, payload.Length, remoteEndPoint);
    }
}
