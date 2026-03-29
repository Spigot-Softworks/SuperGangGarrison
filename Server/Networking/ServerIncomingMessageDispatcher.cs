using System.Net;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server.Plugins;
using static ServerHelpers;

namespace OpenGarrison.Server;

internal sealed class ServerIncomingMessageDispatcher(
    SimulationConfig config,
    string serverName,
    bool passwordRequired,
    int maxPlayableClients,
    int maxTotalClients,
    int maxSpectatorClients,
    Dictionary<byte, ClientSession> clientsBySlot,
    ServerSessionManager sessionManager,
    SimulationWorld world,
    Func<TimeSpan> elapsedGetter,
    Func<PluginHost?> pluginHostGetter,
    Func<IPEndPoint, string?> getHelloRateLimitReason,
    Action<IPEndPoint> resetConnectionAttemptLimits,
    Func<(bool IsCustomMap, string MapDownloadUrl, string MapContentHash)> getCurrentMapMetadata,
    Action<IPEndPoint, IProtocolMessage> sendMessage,
    Action<IPEndPoint> sendServerStatus,
    Action<ClientSession, string, bool> broadcastChat,
    Action<string, (string Key, object? Value)[]> logServerEvent,
    Action<string> log)
{
    public void Dispatch(IProtocolMessage message, IPEndPoint remoteEndPoint)
    {
        switch (message)
        {
            case ServerStatusRequestMessage:
                sendServerStatus(remoteEndPoint);
                break;
            case HelloMessage hello:
                HandleHello(hello, remoteEndPoint);
                break;
            case PasswordSubmitMessage passwordSubmit:
                if (TryGetClient(remoteEndPoint, out var passwordClient))
                {
                    passwordClient.LastSeen = elapsedGetter();
                    sessionManager.HandlePasswordSubmit(passwordClient, passwordSubmit);
                }
                break;
            case ChatSubmitMessage chatSubmit:
                if (TryGetAuthorizedClient(remoteEndPoint, out var chatClient))
                {
                    chatClient.LastSeen = elapsedGetter();
                    broadcastChat(chatClient, chatSubmit.Text, chatSubmit.TeamOnly);
                }
                break;
            case SnapshotAckMessage snapshotAck:
                if (TryGetClient(remoteEndPoint, out var ackClient))
                {
                    ackClient.LastSeen = elapsedGetter();
                    ackClient.AcknowledgeSnapshot(snapshotAck.Frame);
                }
                break;
            case InputStateMessage input:
                if (TryGetAuthorizedClient(remoteEndPoint, out var inputClient))
                {
                    inputClient.LastSeen = elapsedGetter();
                    inputClient.TrySetLatestInput(input.Sequence, ToCoreInput(input));
                    if (input.ChatBubbleFrameIndex >= 0)
                    {
                        world.TryTriggerNetworkPlayerChatBubble(inputClient.Slot, input.ChatBubbleFrameIndex);
                    }
                }
                break;
            case ControlCommandMessage command:
                if (TryGetAuthorizedClient(remoteEndPoint, out var controlClient))
                {
                    controlClient.LastSeen = elapsedGetter();
                    sessionManager.HandleControlCommand(controlClient, command);
                }
                break;
        }
    }

    private void HandleHello(HelloMessage hello, IPEndPoint remoteEndPoint)
    {
        pluginHostGetter()?.NotifyHelloReceived(new HelloReceivedEvent(hello.Name, remoteEndPoint.ToString(), hello.Version));
        if (hello.Version != ProtocolVersion.Current)
        {
            log($"[server] rejected client {remoteEndPoint} due to protocol mismatch client={hello.Version} server={ProtocolVersion.Current}");
            sendMessage(remoteEndPoint, new ConnectionDeniedMessage("Protocol mismatch."));
            return;
        }

        var existingClient = FindClient(clientsBySlot, remoteEndPoint);
        if (existingClient is not null)
        {
            existingClient.Name = hello.Name;
            existingClient.LastSeen = elapsedGetter();
            sessionManager.ApplyClientName(existingClient.Slot, hello.Name);
            var existingMapMetadata = getCurrentMapMetadata();
            sendMessage(remoteEndPoint, new WelcomeMessage(
                serverName,
                ProtocolVersion.Current,
                config.TicksPerSecond,
                world.Level.Name,
                existingClient.Slot,
                existingMapMetadata.IsCustomMap,
                existingMapMetadata.MapDownloadUrl,
                existingMapMetadata.MapContentHash));
            if (passwordRequired && !existingClient.IsAuthorized)
            {
                sendMessage(remoteEndPoint, new PasswordRequestMessage());
                existingClient.LastPasswordRequestSentAt = elapsedGetter();
            }

            log($"[server] client refreshed {remoteEndPoint} slot={existingClient.Slot} name=\"{hello.Name}\" version={hello.Version}");
            return;
        }

        if (getHelloRateLimitReason(remoteEndPoint) is { } rateLimitReason)
        {
            log($"[server] rejected client {remoteEndPoint}; {rateLimitReason}");
            sendMessage(remoteEndPoint, new ConnectionDeniedMessage(rateLimitReason));
            return;
        }

        var assignedSlot = FindAvailableSlot(clientsBySlot, maxTotalClients, maxSpectatorClients, maxPlayableClients);
        if (assignedSlot == 0)
        {
            log($"[server] rejected client {remoteEndPoint}; server is full");
            sendMessage(remoteEndPoint, new ConnectionDeniedMessage("Server is full."));
            return;
        }

        var now = elapsedGetter();
        var client = new ClientSession(assignedSlot, remoteEndPoint, hello.Name, now)
        {
            IsAuthorized = !passwordRequired,
        };
        clientsBySlot[assignedSlot] = client;
        sessionManager.ApplyClientName(assignedSlot, hello.Name);
        if (SimulationWorld.IsPlayableNetworkPlayerSlot(assignedSlot))
        {
            world.TryPrepareNetworkPlayerJoin(assignedSlot);
        }

        var mapMetadata = getCurrentMapMetadata();
        sendMessage(remoteEndPoint, new WelcomeMessage(
            serverName,
            ProtocolVersion.Current,
            config.TicksPerSecond,
            world.Level.Name,
            assignedSlot,
            mapMetadata.IsCustomMap,
            mapMetadata.MapDownloadUrl,
            mapMetadata.MapContentHash));
        if (passwordRequired && !client.IsAuthorized)
        {
            sendMessage(remoteEndPoint, new PasswordRequestMessage());
            client.LastPasswordRequestSentAt = elapsedGetter();
        }

        resetConnectionAttemptLimits(remoteEndPoint);
        log($"[server] client connected {remoteEndPoint} slot={assignedSlot} name=\"{hello.Name}\" version={hello.Version}");
        logServerEvent(
            "client_connected",
            [
                ("slot", assignedSlot),
                ("player_name", hello.Name),
                ("endpoint", remoteEndPoint.ToString()),
                ("is_authorized", client.IsAuthorized),
                ("is_spectator", IsSpectatorSlot(assignedSlot)),
                ("version", hello.Version)
            ]);
        pluginHostGetter()?.NotifyClientConnected(new ClientConnectedEvent(
            assignedSlot,
            hello.Name,
            remoteEndPoint.ToString(),
            client.IsAuthorized,
            IsSpectatorSlot(assignedSlot)));
    }

    private bool TryGetClient(IPEndPoint remoteEndPoint, out ClientSession client)
    {
        client = FindClient(clientsBySlot, remoteEndPoint)!;
        return client is not null;
    }

    private bool TryGetAuthorizedClient(IPEndPoint remoteEndPoint, out ClientSession client)
    {
        if (!TryGetClient(remoteEndPoint, out client))
        {
            return false;
        }

        if (!client.IsAuthorized && passwordRequired)
        {
            return false;
        }

        return true;
    }
}
