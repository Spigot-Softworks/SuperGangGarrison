using System.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Collections.Concurrent;
using System.Threading;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server;

internal sealed partial class ServerOutboundMessaging(
    IServerMessageTransport transport,
    string serverName,
    SimulationWorld world,
    Dictionary<byte, ClientSession> clientsBySlot,
    int maxPlayableClients,
    Func<ServerAdminChatRouter?> adminChatRouterGetter,
    Func<PluginHost?> pluginHostGetter,
    Action<string, (string Key, object? Value)[]> writeEvent,
    Action<string> log,
    Action<IProtocolMessage>? recordBroadcastMessage = null,
    Func<byte, bool>? isBotSlotProvider = null)
{
    private readonly Dictionary<byte, ServerCustomBubbleState> _customBubblesBySlot = new();
    private readonly Protocol64SchemaRegistry _protocol64Registry = Protocol64SchemaRegistryFactory.CreateDefault();
    private readonly Protocol64StatePublisher _protocol64StatePublisher = new(
        world,
        slot => clientsBySlot.TryGetValue(slot, out var client)
            ? client.LastProcessedInputSequence
            : 0u,
        isBotSlotProvider);
    private readonly ConcurrentDictionary<ulong, ulong> _protocol64ConnectionEpochs = new();
    private long _nextProtocol64FrameId;

    public void SendMessage(ServerTransportPeer remotePeer, IProtocolMessage message)
    {
        if (clientsBySlot.Values.Any(client => client.Protocol64Enabled && client.Peer == remotePeer))
        {
            SendProtocol64Event(remotePeer, message);
            return;
        }

        var payload = ProtocolCodec.Serialize(message, ServerProtocolCompression.GetSettingsFor(message));
        SendPayload(
            remotePeer,
            payload,
            message is SnapshotMessage ? MessageType.Snapshot : null);
    }

    /// <summary>
    /// Sends a protocol-64 event through the selected connection container's
    /// complete-frame boundary. This is intentionally separate from the legacy
    /// MessageType serializer so a caller cannot choose reliability ad hoc.
    /// </summary>
    public void SendProtocol64Event(ServerTransportPeer remotePeer, object eventValue)
    {
        ArgumentNullException.ThrowIfNull(eventValue);

        var encoded = Protocol64FrameCodec.EncodeObject(
            _protocol64Registry,
            eventValue,
            GetProtocol64ConnectionEpoch(remotePeer),
            NextProtocol64FrameId(),
            new Protocol64FrameEncodeOptions { Backend = remotePeer.Kind.ToString() });

        if (!encoded.Succeeded || encoded.Payload is null)
        {
            log($"[network] protocol-64 event {eventValue.GetType().Name} rejected: {encoded.Fault?.Message}");
            return;
        }

        var schema = _protocol64Registry.Get(encoded.Header!.SchemaId, encoded.Header.SchemaRevision);
        if (schema.Descriptor.Direction is not (Protocol64Direction.ServerToClient or Protocol64Direction.Bidirectional))
        {
            log($"[network] protocol-64 event {eventValue.GetType().Name} is not a server-to-client schema.");
            return;
        }

        transport.SendProtocol64(
            remotePeer,
            encoded.Payload,
            schema.Descriptor.Delivery,
            GetProtocol64ReplacementKey(eventValue));
    }

    public void SendSnapshotPayload(ServerTransportPeer remotePeer, SnapshotMessage snapshot, byte[] payload)
    {
        // Protocol-64 WebSocket and QUIC peers do not have a legacy payload lane. The
        // snapshot schema is the canonical handoff for the client's initial
        // world warmup, so route the snapshot through the same protocol-64
        // event path as the other server-authoritative state.
        if (remotePeer.IsProtocol64 || remotePeer.Kind == ServerTransportKind.Quic)
        {
            SendMessage(remotePeer, snapshot);
            return;
        }

        SendPayload(remotePeer, payload, MessageType.Snapshot);
    }

    public void BroadcastProtocol64State(uint stateTick)
    {
        var roster = _protocol64StatePublisher.BuildRosterState(stateTick);
        var projectiles = _protocol64StatePublisher.BuildProjectileStates(stateTick);
        var projectileLifecycles = _protocol64StatePublisher.BuildProjectileLifecycleEvents();
        foreach (var client in clientsBySlot.Values)
        {
            if (client.IsAuthorized && client.Protocol64Enabled)
            {
                SendProtocol64Event(
                    client.Peer,
                    _protocol64StatePublisher.BuildPlayerStateBatch(stateTick, client.Slot));
                SendProtocol64Event(client.Peer, roster);
                foreach (var projectile in projectiles)
                {
                    SendProtocol64Event(client.Peer, projectile);
                }
                foreach (var lifecycle in projectileLifecycles)
                {
                    SendProtocol64Event(client.Peer, lifecycle);
                }
            }
        }
    }

    public void SendProtocol64StateResync(
        ClientSession client,
        Protocol64StateResyncRequest request,
        uint stateTick)
    {
        ArgumentNullException.ThrowIfNull(client);
        SendProtocol64Event(
            client.Peer,
            _protocol64StatePublisher.BuildResyncResponse(request, stateTick, client.Slot));
    }

    private ulong GetProtocol64ConnectionEpoch(ServerTransportPeer remotePeer)
    {
        // Native QUIC currently creates the client runtime before it has
        // received a server frame, so there is no negotiation round-trip in
        // which the server-assigned peer id can be learned.  The QUIC runtime
        // therefore uses one epoch per QUIC connection (the QUIC connection
        // itself supplies the isolation); using the server's monotonically
        // increasing peer id here made every reconnect after session #1 reject
        // both directions as belonging to another epoch.
        if (remotePeer.Kind == ServerTransportKind.Quic)
        {
            return 1UL;
        }

        return _protocol64ConnectionEpochs.GetOrAdd(
            remotePeer.Id,
            static peerId => peerId == 0 ? 1UL : peerId);
    }

    private ulong NextProtocol64FrameId()
        => unchecked((ulong)Interlocked.Increment(ref _nextProtocol64FrameId));

    private static string? GetProtocol64ReplacementKey(object eventValue)
        => eventValue switch
        {
            AudioRelayMessage audio => AudioReplacementKey.For(audio),
            ServerAudioStateMessage state => AudioReplacementKey.For(state),
            Protocol64ProjectileState projectile => $"projectile:{projectile.EntityId}",
            Protocol64PlayerStateBatch => "players",
            Protocol64RosterState => "roster",
            Protocol64StateResyncResponse response => $"resync:{response.RequestId}",
            _ => null,
        };

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

        if (TryHandlePointsChatCommand(client, sanitized))
        {
            return;
        }

        if (TryHandleVoteChatCommand(client, sanitized))
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

        BroadcastPlayerSocialProfileUpdate(new PlayerSocialProfileUpdateMessage(
            profiles,
            Array.Empty<byte>(),
            BuildPlayerServerTitles()));
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

    private List<PlayerServerTitleState> BuildPlayerServerTitles()
    {
        var titles = new List<PlayerServerTitleState>();
        foreach (var client in clientsBySlot.Values)
        {
            if (!client.HasAttachedGameplayAccount || string.IsNullOrWhiteSpace(client.ServerTitleText)) continue;
            titles.Add(new PlayerServerTitleState(
                client.Slot,
                client.ServerTitleText,
                client.ServerTitleColorRgb,
                client.ServerTitleRainbow));
        }

        return titles;
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

    private void BroadcastSystemMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var relay = new ChatRelayMessage(0, "[server]", text.Trim());
        foreach (var session in clientsBySlot.Values)
        {
            TrySendMessage(session.Peer, relay, "system chat relay");
        }

        recordBroadcastMessage?.Invoke(relay);
        log($"[server] system message: {text.Trim()}");
    }

    private void SendSystemMessage(byte slot, string text)
    {
        if (string.IsNullOrWhiteSpace(text)
            || !clientsBySlot.TryGetValue(slot, out var client))
        {
            return;
        }

        TrySendMessage(client.Peer, new ChatRelayMessage(0, "[server]", text.Trim()), "system chat relay");
        log($"[server] system message to slot {slot}: {text.Trim()}");
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
