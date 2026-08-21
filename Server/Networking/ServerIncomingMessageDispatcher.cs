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
    Action<ServerTransportPeer>? sendCustomBubbleStates = null,
    Func<bool>? localPredictionEnabledGetter = null,
    Action<ClientSession, Protocol64StateResyncRequest>? sendProtocol64StateResync = null,
    Action<ClientSession, LastToDieCommandMessage>? receiveLastToDieCommand = null,
    Action<ClientSession, LastToDieRunSnapshotAckMessage>? receiveLastToDieSnapshotAck = null,
    Func<ClientSession, ControlCommandMessage, bool>? allowControlCommand = null,
    Func<Guid, byte>? resolveLastToDieReconnectSlot = null)
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
                if (remotePeer.IsProtocol64 && TryGetClient(remotePeer, out var protocol64HelloClient))
                {
                    protocol64HelloClient.Protocol64Enabled = true;
                }
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
                    if (allowControlCommand?.Invoke(controlClient, command) != false)
                    {
                        sessionManager.HandleControlCommand(controlClient, command);
                    }
                    else
                    {
                        sendMessage(
                            controlClient.Peer,
                            new ControlAckMessage(command.Sequence, command.Kind, Accepted: false));
                    }
                }
                break;
            case LastToDieCommandMessage lastToDieCommand:
                if (TryGetAuthorizedClient(remotePeer, out var lastToDieClient))
                {
                    lastToDieClient.LastSeen = elapsedGetter();
                    receiveLastToDieCommand?.Invoke(lastToDieClient, lastToDieCommand);
                }
                break;
            case LastToDieRunSnapshotAckMessage lastToDieSnapshotAck:
                if (TryGetAuthorizedClient(remotePeer, out var lastToDieAckClient))
                {
                    lastToDieAckClient.LastSeen = elapsedGetter();
                    receiveLastToDieSnapshotAck?.Invoke(lastToDieAckClient, lastToDieSnapshotAck);
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

    public void DispatchProtocol64(object eventValue, ServerTransportPeer remotePeer)
    {
        ArgumentNullException.ThrowIfNull(eventValue);

        if (remotePeer.Kind == ServerTransportKind.Udp)
        {
            log($"[network] protocol-64 event rejected from legacy UDP peer {remotePeer}.");
            return;
        }

        switch (eventValue)
        {
            case Protocol64InputCommand command:
                if (TryGetAuthorizedClient(remotePeer, out var inputClient))
                {
                    inputClient.Protocol64Enabled = true;
                    inputClient.LastSeen = elapsedGetter();
                    sessionManager.HandleProtocol64InputCommand(inputClient, command);
                }
                break;
            case LastToDieCommandMessage lastToDieCommand:
                if (TryGetAuthorizedClient(remotePeer, out var lastToDieClient))
                {
                    lastToDieClient.LastSeen = elapsedGetter();
                    receiveLastToDieCommand?.Invoke(lastToDieClient, lastToDieCommand);
                }
                break;
            case LastToDieRunSnapshotAckMessage lastToDieSnapshotAck:
                if (TryGetAuthorizedClient(remotePeer, out var lastToDieAckClient))
                {
                    lastToDieAckClient.LastSeen = elapsedGetter();
                    receiveLastToDieSnapshotAck?.Invoke(lastToDieAckClient, lastToDieSnapshotAck);
                }
                break;
            case Protocol64InputCommandResultAck acknowledgement:
                if (TryGetAuthorizedClient(remotePeer, out var acknowledgementClient))
                {
                    acknowledgementClient.Protocol64Enabled = true;
                    acknowledgementClient.LastSeen = elapsedGetter();
                    sessionManager.HandleProtocol64InputResultAck(acknowledgementClient, acknowledgement);
                }
                break;
            case Protocol64StateResyncRequest request:
                if (TryGetAuthorizedClient(remotePeer, out var stateClient))
                {
                    stateClient.Protocol64Enabled = true;
                    stateClient.LastSeen = elapsedGetter();
                    sendProtocol64StateResync?.Invoke(stateClient, request);
                }
                break;
            default:
                if (eventValue is IProtocolMessage legacyMessage)
                {
                    Dispatch(legacyMessage, remotePeer);
                    if (legacyMessage is HelloMessage && TryGetClient(remotePeer, out var protocol64Client))
                    {
                        protocol64Client.Protocol64Enabled = true;
                    }
                    break;
                }

                log($"[network] protocol-64 event {eventValue.GetType().Name} is not accepted from clients.");
                break;
        }
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
            if (existingClient.ClientInstanceId != Guid.Empty
                && hello.ClientInstanceId != Guid.Empty
                && existingClient.ClientInstanceId != hello.ClientInstanceId)
            {
                log($"[server] rejected client-instance change for existing peer {remoteDescription}.");
                sendMessage(remotePeer, new ConnectionDeniedMessage("Client session identity changed."));
                return;
            }

            if (hello.Intent == ConnectionIntent.Watch && !IsSpectatorSlot(existingClient.Slot))
            {
                log($"[server] rejected watch refresh {remoteDescription}; existing slot is playable slot={existingClient.Slot}");
                sendMessage(remotePeer, new ConnectionDeniedMessage("Existing session is not a spectator."));
                return;
            }

            existingClient.Name = clientName;
            existingClient.BadgeMask = hello.BadgeMask;
            existingClient.IsWatchOnly = existingClient.IsWatchOnly || hello.Intent == ConnectionIntent.Watch;
            existingClient.Protocol64Enabled |= remotePeer.IsProtocol64;
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
                world.Level.MapScale,
                localPredictionEnabledGetter?.Invoke() == true));
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
        var reconnectSlot = !watchOnly && hello.ClientInstanceId != Guid.Empty
            ? resolveLastToDieReconnectSlot?.Invoke(hello.ClientInstanceId) ?? (byte)0
            : (byte)0;
        if (reconnectSlot != 0
            && (!SimulationWorld.IsPlayableNetworkPlayerSlot(reconnectSlot)
                || reconnectSlot > maxPlayableClients
                || reconnectSlot > maxTotalClients
                || isPlayableSlotAvailable?.Invoke(reconnectSlot) == false))
        {
            log($"[server] rejected invalid Last to Die reconnect slot={reconnectSlot} peer={remoteDescription}.");
            sendMessage(remotePeer, new ConnectionDeniedMessage("Reserved Last to Die slot is unavailable."));
            return;
        }

        if (reconnectSlot != 0
            && clientsBySlot.TryGetValue(reconnectSlot, out var supersededClient))
        {
            if (supersededClient.ClientInstanceId != hello.ClientInstanceId)
            {
                sendMessage(remotePeer, new ConnectionDeniedMessage("Reserved Last to Die slot identity did not match."));
                return;
            }

            sessionManager.RemoveClient(reconnectSlot, "superseded by Last to Die reconnect");
        }

        var assignedSlot = reconnectSlot != 0
            ? reconnectSlot
            : watchOnly
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
        var client = new ClientSession(
            assignedSlot,
            allocateUserId(),
            remotePeer,
            clientName,
            now,
            hello.ClientInstanceId)
        {
            IsAuthorized = !passwordRequired,
            BadgeMask = hello.BadgeMask,
            FriendCode = hello.FriendCode.Trim(),
            PlayerCardJson = hello.PlayerCardJson.Trim(),
            IsWatchOnly = watchOnly,
            Protocol64Enabled = remotePeer.IsProtocol64,
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
            world.Level.MapScale,
            localPredictionEnabledGetter?.Invoke() == true));
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
