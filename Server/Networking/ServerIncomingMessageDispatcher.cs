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
    Func<int> allocateUserId,
    Func<IPAddress, string?> getHelloRateLimitReason,
    Action<IPAddress> resetConnectionAttemptLimits,
    Func<(bool IsCustomMap, string MapDownloadUrl, string MapContentHash)> getCurrentMapMetadata,
    Action<ServerTransportPeer, IProtocolMessage> sendMessage,
    Action<ServerTransportPeer> sendServerStatus,
    Action<ServerTransportPeer> sendServerDetails,
    Action<ClientSession, string, bool> broadcastChat,
    Action<string, (string Key, object? Value)[]> logServerEvent,
    Action<string> log,
    Func<byte, bool>? isPlayableSlotAvailable = null,
    ServerBanService? banService = null,
    Action<ClientSession, CustomBubbleUploadMessage>? receiveCustomBubbleUpload = null,
    Action<ClientSession>? receiveCustomBubbleClear = null,
    Action<ServerTransportPeer>? sendCustomBubbleStates = null)
{
    public void Dispatch(IProtocolMessage message, ServerTransportPeer remotePeer)
    {
        switch (message)
        {
            case ServerStatusRequestMessage:
                sendServerStatus(remotePeer);
                break;
            case ServerDetailsRequestMessage:
                sendServerDetails(remotePeer);
                break;
            case HelloMessage hello:
                HandleHello(hello, remotePeer);
                break;
            case PasswordSubmitMessage passwordSubmit:
                if (TryGetClient(remotePeer, out var passwordClient))
                {
                    passwordClient.LastSeen = elapsedGetter();
                    sessionManager.HandlePasswordSubmit(passwordClient, passwordSubmit);
                }
                break;
            case ChatSubmitMessage chatSubmit:
                if (TryGetAuthorizedClient(remotePeer, out var chatClient))
                {
                    chatClient.LastSeen = elapsedGetter();
                    if (chatClient.IsGagged)
                    {
                        sendMessage(remotePeer, new ChatRelayMessage(0, "[server]", "You are gagged and cannot send chat.", false));
                        log($"[server] gagged chat blocked slot={chatClient.Slot} peer={chatClient.RemoteDescription}");
                        break;
                    }

                    broadcastChat(chatClient, chatSubmit.Text, chatSubmit.TeamOnly);
                }
                break;
            case SnapshotAckMessage snapshotAck:
                if (TryGetClient(remotePeer, out var ackClient))
                {
                    ackClient.LastSeen = elapsedGetter();
                    ackClient.AcknowledgeSnapshot(snapshotAck.Frame);
                }
                break;
            case PingRequestMessage pingRequest:
                if (TryGetClient(remotePeer, out var pingClient))
                {
                    pingClient.LastSeen = elapsedGetter();
                }

                sendMessage(remotePeer, new PingResponseMessage(pingRequest.Sequence));
                break;
            case PlayerProfileUpdateMessage profileUpdate:
                if (TryGetClient(remotePeer, out var profileClient))
                {
                    profileClient.LastSeen = elapsedGetter();
                    sessionManager.ApplyClientProfile(
                        profileClient.Slot,
                        profileUpdate.Name,
                        profileUpdate.BadgeMask,
                        profileUpdate.FriendCode,
                        profileUpdate.PlayerCardJson);
                }
                break;
            case CustomBubbleUploadMessage customBubbleUpload:
                if (TryGetAuthorizedClient(remotePeer, out var customBubbleClient))
                {
                    customBubbleClient.LastSeen = elapsedGetter();
                    receiveCustomBubbleUpload?.Invoke(customBubbleClient, customBubbleUpload);
                }
                break;
            case CustomBubbleClearMessage:
                if (TryGetAuthorizedClient(remotePeer, out var customBubbleClearClient))
                {
                    customBubbleClearClient.LastSeen = elapsedGetter();
                    receiveCustomBubbleClear?.Invoke(customBubbleClearClient);
                }
                break;
            case InputStateMessage input:
                if (TryGetAuthorizedClient(remotePeer, out var inputClient))
                {
                    inputClient.LastSeen = elapsedGetter();
                    inputClient.TrySetLatestInput(input.Sequence, ToCoreInput(input));
                    inputClient.PingMilliseconds = input.PingMilliseconds;
                    if (input.ChatBubbleFrameIndex >= 0)
                    {
                        world.TryTriggerNetworkPlayerChatBubble(inputClient.Slot, input.ChatBubbleFrameIndex);
                    }

                    world.SetNetworkPlayerIsTypingChatMessage(inputClient.Slot, input.Buttons.HasFlag(InputButtons.IsTypingChatMessage));
                }
                break;
            case ControlCommandMessage command:
                if (TryGetAuthorizedClient(remotePeer, out var controlClient))
                {
                    controlClient.LastSeen = elapsedGetter();
                    sessionManager.HandleControlCommand(controlClient, command);
                }
                break;
            case ClientPluginMessage pluginMessage:
                if (TryGetAuthorizedClient(remotePeer, out var pluginClient))
                {
                    pluginClient.LastSeen = elapsedGetter();
                    pluginHostGetter()?.NotifyClientPluginMessage(new OpenGarrisonServerPluginMessageEnvelope(
                        pluginClient.Slot,
                        pluginClient.Name,
                        pluginMessage.SourcePluginId,
                        pluginMessage.TargetPluginId,
                        pluginMessage.MessageTypeName,
                        pluginMessage.Payload,
                        pluginMessage.PayloadFormat,
                        pluginMessage.SchemaVersion));
                }
                break;
        }
    }

    public void Dispatch(IProtocolMessage message, IPEndPoint remoteEndPoint)
    {
        Dispatch(message, ServerTransportPeer.FromUdpEndPoint(remoteEndPoint));
    }

    private void HandleHello(HelloMessage hello, ServerTransportPeer remotePeer)
    {
        var remoteDescription = remotePeer.ToString();
        var clientName = PlayerEntity.NormalizeDisplayName(hello.Name);
        pluginHostGetter()?.NotifyHelloReceived(new HelloReceivedEvent(clientName, remoteDescription, hello.Version));
        if (hello.Version != ProtocolVersion.Current)
        {
            log($"[server] rejected client {remoteDescription} due to protocol mismatch client={hello.Version} server={ProtocolVersion.Current}");
            sendMessage(remotePeer, new ConnectionDeniedMessage("Protocol mismatch."));
            return;
        }

        var existingClient = FindClient(clientsBySlot, remotePeer);
        if (existingClient is not null)
        {
            if (hello.Intent == ConnectionIntent.Watch && !IsSpectatorSlot(existingClient.Slot))
            {
                log($"[server] rejected watch refresh {remoteDescription}; existing slot is playable slot={existingClient.Slot}");
                sendMessage(remotePeer, new ConnectionDeniedMessage("Existing session is not a spectator."));
                return;
            }

            existingClient.Name = clientName;
            existingClient.BadgeMask = hello.BadgeMask;
            existingClient.IsWatchOnly = existingClient.IsWatchOnly || hello.Intent == ConnectionIntent.Watch;
            existingClient.LastSeen = elapsedGetter();
            sessionManager.ApplyClientProfile(
                existingClient.Slot,
                clientName,
                hello.BadgeMask,
                hello.FriendCode,
                hello.PlayerCardJson);
            var existingMapMetadata = getCurrentMapMetadata();
            sendMessage(remotePeer, new WelcomeMessage(
                serverName,
                ProtocolVersion.Current,
                config.TicksPerSecond,
                world.Level.Name,
                existingClient.Slot,
                maxPlayableClients,
                existingMapMetadata.IsCustomMap,
                existingMapMetadata.MapDownloadUrl,
                existingMapMetadata.MapContentHash,
                world.Level.MapScale));
            if (passwordRequired && !existingClient.IsAuthorized)
            {
                sendMessage(remotePeer, new PasswordRequestMessage());
                existingClient.LastPasswordRequestSentAt = elapsedGetter();
            }
            else
            {
                sendCustomBubbleStates?.Invoke(remotePeer);
            }

            log($"[server] client refreshed {remoteDescription} slot={existingClient.Slot} name=\"{clientName}\" version={hello.Version}");
            return;
        }

        var remoteAddress = remotePeer.RemoteAddress;
        if (remoteAddress is not null && getHelloRateLimitReason(remoteAddress) is { } rateLimitReason)
        {
            log($"[server] rejected client {remoteDescription}; {rateLimitReason}");
            sendMessage(remotePeer, new ConnectionDeniedMessage(rateLimitReason));
            return;
        }

        if (remoteAddress is not null && banService?.GetConnectionDeniedReason(remoteAddress) is { } banReason)
        {
            log($"[server] rejected client {remoteDescription}; banned");
            sendMessage(remotePeer, new ConnectionDeniedMessage(banReason));
            logServerEvent(
                "client_rejected_banned",
                [
                    ("endpoint", remoteDescription),
                    ("reason", banReason)
                ]);
            return;
        }

        var watchOnly = hello.Intent == ConnectionIntent.Watch;
        var assignedSlot = watchOnly
            ? FindAvailableSpectatorSlot(clientsBySlot, maxTotalClients, maxSpectatorClients)
            : FindAvailableSlot(
                clientsBySlot,
                maxTotalClients,
                maxSpectatorClients,
                maxPlayableClients,
                isPlayableSlotAvailable);
        if (assignedSlot == 0)
        {
            var reason = watchOnly ? "Spectator slots are full." : "Server is full.";
            log($"[server] rejected client {remoteDescription}; {reason}");
            sendMessage(remotePeer, new ConnectionDeniedMessage(reason));
            return;
        }

        var now = elapsedGetter();
        var client = new ClientSession(assignedSlot, allocateUserId(), remotePeer, clientName, now)
        {
            IsAuthorized = !passwordRequired,
            BadgeMask = hello.BadgeMask,
            FriendCode = hello.FriendCode.Trim(),
            PlayerCardJson = hello.PlayerCardJson.Trim(),
            IsWatchOnly = watchOnly,
        };
        clientsBySlot[assignedSlot] = client;
        if (SimulationWorld.IsPlayableNetworkPlayerSlot(assignedSlot))
        {
            world.TryPrepareNetworkPlayerJoin(assignedSlot);
        }
        sessionManager.ApplyClientProfile(assignedSlot, clientName, hello.BadgeMask, hello.FriendCode, hello.PlayerCardJson);

        var mapMetadata = getCurrentMapMetadata();
        sendMessage(remotePeer, new WelcomeMessage(
            serverName,
            ProtocolVersion.Current,
            config.TicksPerSecond,
            world.Level.Name,
            assignedSlot,
            maxPlayableClients,
            mapMetadata.IsCustomMap,
            mapMetadata.MapDownloadUrl,
            mapMetadata.MapContentHash,
            world.Level.MapScale));
        if (passwordRequired && !client.IsAuthorized)
        {
            sendMessage(remotePeer, new PasswordRequestMessage());
            client.LastPasswordRequestSentAt = elapsedGetter();
        }
        else
        {
            sendCustomBubbleStates?.Invoke(remotePeer);
        }

        if (remoteAddress is not null)
        {
            resetConnectionAttemptLimits(remoteAddress);
        }

        log($"[server] client connected {remoteDescription} slot={assignedSlot} name=\"{clientName}\" version={hello.Version}");
        logServerEvent(
            "client_connected",
            [
                ("slot", assignedSlot),
                ("player_name", clientName),
                ("endpoint", remoteDescription),
                ("is_authorized", client.IsAuthorized),
                ("is_spectator", IsSpectatorSlot(assignedSlot)),
                ("version", hello.Version)
            ]);
        pluginHostGetter()?.NotifyClientConnected(new ClientConnectedEvent(
            assignedSlot,
            clientName,
            remoteDescription,
            client.IsAuthorized,
            IsSpectatorSlot(assignedSlot)));
    }

    private bool TryGetClient(ServerTransportPeer remotePeer, out ClientSession client)
    {
        client = FindClient(clientsBySlot, remotePeer)!;
        return client is not null;
    }

    private bool TryGetAuthorizedClient(ServerTransportPeer remotePeer, out ClientSession client)
    {
        if (!TryGetClient(remotePeer, out client))
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
