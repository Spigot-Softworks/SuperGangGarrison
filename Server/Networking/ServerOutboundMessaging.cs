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

        if (TryHandleVipVoteChatCommand(client, sanitized))
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

    private VipVoteState? _activeVipVote;

    private bool TryHandleVipVoteChatCommand(ClientSession client, string text)
    {
        ExpireVipVoteIfNeeded();

        if (text.StartsWith("!votevip", StringComparison.OrdinalIgnoreCase))
        {
            return TryStartVipVote(client, text["!votevip".Length..].Trim());
        }

        if (text.StartsWith("!vipvote", StringComparison.OrdinalIgnoreCase))
        {
            return TryStartVipVote(client, text["!vipvote".Length..].Trim());
        }

        if (text.Equals("!vipstatus", StringComparison.OrdinalIgnoreCase))
        {
            SendVipVoteStatus(client.Slot);
            return true;
        }

        if (text.Equals("!vipcancel", StringComparison.OrdinalIgnoreCase))
        {
            return TryCancelVipVote(client);
        }

        if (_activeVipVote is not null && text.StartsWith("!vote ", StringComparison.OrdinalIgnoreCase))
        {
            return TryRegisterVipVote(client, text["!vote ".Length..].Trim());
        }

        return false;
    }

    private bool TryStartVipVote(ClientSession client, string arguments)
    {
        if (!world.IsVipModeActive)
        {
            SendSystemMessage(client.Slot, "VIP votes are only available on vip_ maps.");
            return true;
        }

        if (_activeVipVote is not null)
        {
            SendSystemMessage(client.Slot, $"A VIP vote is already active: {BuildVipVoteSummary(_activeVipVote)}");
            return true;
        }

        if (!TryResolveVipVoteTarget(arguments, out var team, out var targetSlot, out var targetName, out var error))
        {
            SendSystemMessage(client.Slot, error);
            return true;
        }

        _activeVipVote = new VipVoteState(
            client.Slot,
            targetSlot,
            team,
            targetName,
            (long)world.Frame);
        _activeVipVote.YesVotes.Add(client.Slot);
        BroadcastSystemMessage(
            $"VIP vote started: make {targetName} the {team} VIP. Type !vote yes or !vote no.");
        ResolveVipVoteIfPossible();
        return true;
    }

    private bool TryRegisterVipVote(ClientSession client, string arguments)
    {
        if (_activeVipVote is null)
        {
            return false;
        }

        if (!IsEligibleVipVoter(client))
        {
            SendSystemMessage(client.Slot, "Spectators cannot vote for VIP.");
            return true;
        }

        var normalized = arguments.Trim();
        bool isYesVote;
        if (normalized.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("y", StringComparison.OrdinalIgnoreCase))
        {
            isYesVote = true;
        }
        else if (normalized.Equals("no", StringComparison.OrdinalIgnoreCase)
                 || normalized.Equals("n", StringComparison.OrdinalIgnoreCase))
        {
            isYesVote = false;
        }
        else
        {
            SendSystemMessage(client.Slot, "Usage: !vote <yes|no>");
            return true;
        }

        _activeVipVote.YesVotes.Remove(client.Slot);
        _activeVipVote.NoVotes.Remove(client.Slot);
        if (isYesVote)
        {
            _activeVipVote.YesVotes.Add(client.Slot);
        }
        else
        {
            _activeVipVote.NoVotes.Add(client.Slot);
        }

        BroadcastSystemMessage(
            $"{client.Name} voted {(isYesVote ? "yes" : "no")} on VIP: {BuildVipVoteSummary(_activeVipVote)}");
        ResolveVipVoteIfPossible();
        return true;
    }

    private bool TryCancelVipVote(ClientSession client)
    {
        if (_activeVipVote is null)
        {
            SendSystemMessage(client.Slot, "There is no active VIP vote.");
            return true;
        }

        if (_activeVipVote.InitiatorSlot != client.Slot)
        {
            SendSystemMessage(client.Slot, "Only the player who started the VIP vote can cancel it.");
            return true;
        }

        BroadcastSystemMessage("VIP vote canceled.");
        _activeVipVote = null;
        return true;
    }

    private void SendVipVoteStatus(byte slot)
    {
        SendSystemMessage(
            slot,
            _activeVipVote is null
                ? "There is no active VIP vote."
                : BuildVipVoteSummary(_activeVipVote));
    }

    private void ResolveVipVoteIfPossible()
    {
        var vote = _activeVipVote;
        if (vote is null)
        {
            return;
        }

        PruneVipVote(vote);
        var eligibleCount = CountEligibleVipVoters();
        if (eligibleCount <= 0)
        {
            BroadcastSystemMessage("VIP vote canceled: no eligible voters remain.");
            _activeVipVote = null;
            return;
        }

        var required = (eligibleCount / 2) + 1;
        if (vote.YesVotes.Count >= required)
        {
            if (world.TrySetPreferredVipSlot(vote.Team, vote.TargetSlot))
            {
                BroadcastSystemMessage($"VIP vote passed: {vote.TargetName} will be the {vote.Team} VIP.");
            }
            else
            {
                BroadcastSystemMessage("VIP vote passed, but the selected player is no longer eligible.");
            }

            _activeVipVote = null;
            return;
        }

        if (vote.NoVotes.Count >= required)
        {
            BroadcastSystemMessage("VIP vote failed.");
            _activeVipVote = null;
        }
    }

    private void ExpireVipVoteIfNeeded()
    {
        if (_activeVipVote is null)
        {
            return;
        }

        var lifetimeTicks = Math.Max(1, world.Config.TicksPerSecond * 30);
        if ((long)world.Frame - _activeVipVote.StartFrame <= lifetimeTicks)
        {
            return;
        }

        BroadcastSystemMessage("VIP vote expired.");
        _activeVipVote = null;
    }

    private void PruneVipVote(VipVoteState vote)
    {
        vote.YesVotes.RemoveWhere(slot => !IsEligibleVipVoter(slot));
        vote.NoVotes.RemoveWhere(slot => !IsEligibleVipVoter(slot));
    }

    private bool TryResolveVipVoteTarget(
        string arguments,
        out PlayerTeam team,
        out byte targetSlot,
        out string targetName,
        out string error)
    {
        team = PlayerTeam.Red;
        targetSlot = 0;
        targetName = string.Empty;
        error = "Usage: !votevip <player>";

        var tokens = SplitVipVoteArguments(arguments);
        if (tokens.Count == 0)
        {
            error = world.VipRequiresDualVip
                ? "Usage: !votevip <red|blue> <player>"
                : "Usage: !votevip <player>";
            return false;
        }

        var targetTokens = tokens;
        if (TryParseTeamToken(tokens[0], out var explicitTeam))
        {
            team = explicitTeam;
            targetTokens = tokens.Skip(1).ToList();
        }
        else if (world.VipRequiresDualVip)
        {
            team = PlayerTeam.Red;
        }

        if (targetTokens.Count == 0)
        {
            error = world.VipRequiresDualVip
                ? "Usage: !votevip <red|blue> <player>"
                : "Usage: !votevip <player>";
            return false;
        }

        var targetText = string.Join(' ', targetTokens);
        if (!TryResolveClientBySlotOrName(targetText, out var targetClient, out error))
        {
            return false;
        }

        if (!IsEligibleVipVoter(targetClient))
        {
            error = "Selected VIP must be an active player.";
            return false;
        }

        if (world.VipRequiresDualVip && !TryParseTeamToken(tokens[0], out _))
        {
            team = world.GetNetworkPlayerConfiguredTeam(targetClient.Slot);
        }

        if (!world.VipRequiresDualVip)
        {
            team = PlayerTeam.Red;
        }

        targetSlot = targetClient.Slot;
        targetName = targetClient.Name;
        return true;
    }

    private bool TryResolveClientBySlotOrName(string text, out ClientSession client, out string error)
    {
        client = null!;
        error = "Player not found.";
        var trimmed = text.Trim();
        if (byte.TryParse(trimmed, out var slot)
            && clientsBySlot.TryGetValue(slot, out client!))
        {
            return true;
        }

        var matches = clientsBySlot.Values
            .Where(candidate => !ServerHelpers.IsSpectatorSlot(candidate.Slot))
            .Where(candidate => candidate.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();
        if (matches.Length == 1)
        {
            client = matches[0];
            return true;
        }

        error = matches.Length == 0
            ? "Player not found."
            : "Player name is ambiguous; use the slot number.";
        return false;
    }

    private static List<string> SplitVipVoteArguments(string arguments)
    {
        return arguments
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static bool TryParseTeamToken(string token, out PlayerTeam team)
    {
        if (token.Equals("red", StringComparison.OrdinalIgnoreCase)
            || token.Equals("r", StringComparison.OrdinalIgnoreCase))
        {
            team = PlayerTeam.Red;
            return true;
        }

        if (token.Equals("blue", StringComparison.OrdinalIgnoreCase)
            || token.Equals("blu", StringComparison.OrdinalIgnoreCase)
            || token.Equals("b", StringComparison.OrdinalIgnoreCase))
        {
            team = PlayerTeam.Blue;
            return true;
        }

        team = default;
        return false;
    }

    private int CountEligibleVipVoters()
    {
        return clientsBySlot.Values.Count(IsEligibleVipVoter);
    }

    private bool IsEligibleVipVoter(byte slot)
    {
        return clientsBySlot.TryGetValue(slot, out var client) && IsEligibleVipVoter(client);
    }

    private bool IsEligibleVipVoter(ClientSession client)
    {
        return client.IsAuthorized
            && !ServerHelpers.IsSpectatorSlot(client.Slot)
            && world.TryGetNetworkPlayer(client.Slot, out _)
            && !world.IsNetworkPlayerAwaitingJoin(client.Slot);
    }

    private string BuildVipVoteSummary(VipVoteState vote)
    {
        var eligibleCount = CountEligibleVipVoters();
        var required = eligibleCount <= 0 ? 1 : (eligibleCount / 2) + 1;
        return $"{vote.TargetName} as {vote.Team} VIP: yes {vote.YesVotes.Count}/{required}, no {vote.NoVotes.Count}/{required}";
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

    private sealed class VipVoteState(
        byte initiatorSlot,
        byte targetSlot,
        PlayerTeam team,
        string targetName,
        long startFrame)
    {
        public byte InitiatorSlot { get; } = initiatorSlot;

        public byte TargetSlot { get; } = targetSlot;

        public PlayerTeam Team { get; } = team;

        public string TargetName { get; } = targetName;

        public long StartFrame { get; } = startFrame;

        public HashSet<byte> YesVotes { get; } = new();

        public HashSet<byte> NoVotes { get; } = new();
    }
}
