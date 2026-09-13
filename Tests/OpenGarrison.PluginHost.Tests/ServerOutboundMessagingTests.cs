using System.Net;
using System.Net.Sockets;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using OpenGarrison.Server.Plugins;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ServerOutboundMessagingTests
{
    [Fact]
    public void PluginBroadcastSuppressesTransportSendFailures()
    {
        var clients = CreateAuthorizedClients();
        var transport = new ThrowingServerMessageTransport();
        var logs = new List<string>();
        var outbound = CreateOutboundMessaging(transport, clients, logs);

        outbound.BroadcastPluginMessage(
            "chat.voting",
            "chat.vote.presentation",
            "vote.event",
            "{}",
            PluginMessagePayloadFormat.Json,
            schemaVersion: 1);

        Assert.Equal(clients.Count, transport.SendAttempts);
        Assert.Contains(logs, log => log.Contains("failed to send plugin message broadcast", StringComparison.Ordinal));
    }

    [Fact]
    public void CustomBubbleBroadcastSuppressesTransportSendFailures()
    {
        var clients = CreateAuthorizedClients();
        var transport = new ThrowingServerMessageTransport();
        var logs = new List<string>();
        var outbound = CreateOutboundMessaging(transport, clients, logs);

        outbound.ReceiveCustomBubbleUpload(
            clients[1],
            new CustomBubbleUploadMessage(
                Slot: 0,
                Revision: 1,
                new byte[ProtocolCodec.CustomBubbleRgba64PayloadBytes]));

        Assert.Equal(clients.Count, transport.SendAttempts);
        Assert.Contains(logs, log => log.Contains("failed to send custom bubble state", StringComparison.Ordinal));
    }

    [Fact]
    public void VipVotePassesWithStrictMajorityWithoutFullTurnout()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(world.TryLoadLevel("vip_egypt"));
        world.CompleteLocalPlayerJoin(PlayerClass.Scout);
        JoinNetworkPlayer(world, 2);
        JoinNetworkPlayer(world, 3);

        var clients = CreateAuthorizedClients(3);
        var transport = new ThrowingServerMessageTransport();
        var logs = new List<string>();
        var outbound = CreateOutboundMessaging(transport, clients, logs, world);
        outbound.ConfigureVoting(
            static (_, _) => true,
            static (_, _) => true,
            static () => Array.Empty<string>());

        outbound.BroadcastChat(clients[1], "!votevip 2", teamOnly: false);

        Assert.Contains(logs, log => log.Contains("One started a vote for Two as Blue VIP", StringComparison.Ordinal));
        Assert.DoesNotContain(logs, log => log.Contains("Vote passed", StringComparison.Ordinal));

        outbound.BroadcastChat(clients[2], "!vote yes", teamOnly: false);

        Assert.Contains(logs, log => log.Contains("Vote passed for Two as Blue VIP.", StringComparison.Ordinal));
    }

    [Fact]
    public void KickAndMuteVotesUseStableAuthoritativeTargets()
    {
        var world = CreateJoinedWorld(3);
        var clients = CreateAuthorizedClients(3);
        var outbound = CreateOutboundMessaging(new ThrowingServerMessageTransport(), clients, [], world);
        byte disconnectedSlot = 0;
        string disconnectReason = string.Empty;
        outbound.ConfigureVoting(
            static (_, _) => true,
            static (_, _) => true,
            static () => Array.Empty<string>(),
            (slot, reason) =>
            {
                disconnectedSlot = slot;
                disconnectReason = reason;
                return true;
            },
            (slot, muted) =>
            {
                clients[slot].IsGagged = muted;
                return true;
            });

        outbound.BroadcastChat(clients[1], "!votekick 3", teamOnly: false);
        outbound.BroadcastChat(clients[2], "!yes", teamOnly: false);
        Assert.Equal((byte)3, disconnectedSlot);
        Assert.Contains("player vote", disconnectReason, StringComparison.OrdinalIgnoreCase);

        AdvanceWorld(world, world.Config.TicksPerSecond * 30);
        outbound.BroadcastChat(clients[1], "!votemute 3", teamOnly: false);
        outbound.BroadcastChat(clients[2], "!yes", teamOnly: false);
        Assert.True(clients[3].IsGagged);
    }

    [Fact]
    public void ScrambleVoteRunsOnlyAfterStrictMajority()
    {
        var world = CreateJoinedWorld(3);
        var clients = CreateAuthorizedClients(3);
        var outbound = CreateOutboundMessaging(new ThrowingServerMessageTransport(), clients, [], world);
        var scrambleCount = 0;
        outbound.ConfigureVoting(
            static (_, _) => true,
            static (_, _) => true,
            static () => Array.Empty<string>(),
            scrambleTeams: () =>
            {
                scrambleCount += 1;
                return true;
            });

        outbound.BroadcastChat(clients[1], "!votescramble", teamOnly: false);
        Assert.Equal(0, scrambleCount);
        outbound.BroadcastChat(clients[2], "!vote yes", teamOnly: false);
        Assert.Equal(1, scrambleCount);
    }

    [Fact]
    public void KickVoteCannotFollowAReusedPlayerSlot()
    {
        var world = CreateJoinedWorld(3);
        var clients = CreateAuthorizedClients(3);
        var logs = new List<string>();
        var outbound = CreateOutboundMessaging(new ThrowingServerMessageTransport(), clients, logs, world);
        byte disconnectedSlot = 0;
        outbound.ConfigureVoting(
            static (_, _) => true,
            static (_, _) => true,
            static () => Array.Empty<string>(),
            (slot, _) =>
            {
                disconnectedSlot = slot;
                return true;
            });

        outbound.BroadcastChat(clients[1], "!votekick 3", teamOnly: false);
        clients[3] = new ClientSession(
            3,
            9999,
            new IPEndPoint(IPAddress.Loopback, 9999),
            "Replacement",
            TimeSpan.Zero)
        {
            IsAuthorized = true,
        };
        outbound.BroadcastChat(clients[2], "!yes", teamOnly: false);

        Assert.Equal((byte)0, disconnectedSlot);
        Assert.Contains(logs, line => line.Contains("action could not be applied", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ActiveVoteRejectsPluginRequestBeforeCallingValidator()
    {
        var world = CreateJoinedWorld(3);
        var clients = CreateAuthorizedClients(3);
        var outbound = CreateOutboundMessaging(new ThrowingServerMessageTransport(), clients, [], world);
        outbound.ConfigureVoting(
            static (_, _) => true,
            static (_, _) => true,
            static () => Array.Empty<string>());
        var validationCount = 0;
        Assert.True(outbound.TryRegisterPluginVoteKind(
            "example.votes",
            new OpenGarrisonServerVoteRegistration(
                "safe",
                "Safe Vote",
                string.Empty,
                OpenGarrisonServerVoteTargetKind.None,
                static _ => true,
                _ =>
                {
                    validationCount += 1;
                    return OpenGarrisonServerVoteValidationResult.Accept("safe vote");
                }),
            out var registrationError), registrationError);

        Assert.True(outbound.TryStartPluginVote("example.votes", "safe", 1, string.Empty, out var firstError), firstError);
        Assert.False(outbound.TryStartPluginVote("example.votes", "safe", 2, string.Empty, out var secondError));

        Assert.Equal(1, validationCount);
        Assert.Contains("already active", secondError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PluginVoteUsesNativeQuorumAndCannotSurviveOwnerUnload()
    {
        var world = CreateJoinedWorld(3);
        var clients = CreateAuthorizedClients(3);
        var logs = new List<string>();
        var outbound = CreateOutboundMessaging(new ThrowingServerMessageTransport(), clients, logs, world);
        outbound.ConfigureVoting(
            static (_, _) => true,
            static (_, _) => true,
            static () => Array.Empty<string>());
        OpenGarrisonServerVoteRequest? appliedRequest = null;
        Assert.True(outbound.TryRegisterPluginVoteKind(
            "example.votes",
            new OpenGarrisonServerVoteRegistration(
                "move-player",
                "Move Player",
                "Moves a selected player.",
                OpenGarrisonServerVoteTargetKind.Player,
                request =>
                {
                    appliedRequest = request;
                    return true;
                },
                request => OpenGarrisonServerVoteValidationResult.Accept($"move {request.TargetName}")),
            out var registrationError), registrationError);

        outbound.BroadcastChat(clients[1], "!votecustom move-player 3", teamOnly: false);
        outbound.BroadcastChat(clients[2], "!yes", teamOnly: false);
        Assert.NotNull(appliedRequest);
        Assert.Equal((byte)3, appliedRequest!.TargetSlot);
        Assert.Equal("Three", appliedRequest.TargetName);

        AdvanceWorld(world, world.Config.TicksPerSecond * 30);
        appliedRequest = null;
        outbound.BroadcastChat(clients[1], "!votecustom example.votes:move-player 3", teamOnly: false);
        outbound.UnregisterPluginVoteKinds("example.votes");
        outbound.BroadcastChat(clients[2], "!yes", teamOnly: false);
        Assert.Null(appliedRequest);
        Assert.Contains(logs, line => line.Contains("unloaded", StringComparison.OrdinalIgnoreCase));
    }

    private static ServerOutboundMessaging CreateOutboundMessaging(
        ThrowingServerMessageTransport transport,
        Dictionary<byte, ClientSession> clients,
        List<string> logs,
        SimulationWorld? world = null)
    {
        return new ServerOutboundMessaging(
            transport,
            "Test Server",
            world ?? new SimulationWorld(),
            clients,
            maxPlayableClients: 24,
            adminChatRouterGetter: static () => null,
            pluginHostGetter: static () => null,
            writeEvent: static (_, _) => { },
            logs.Add);
    }

    private static Dictionary<byte, ClientSession> CreateAuthorizedClients(int count = 2)
    {
        var clients = new Dictionary<byte, ClientSession>();
        for (byte slot = 1; slot <= count; slot += 1)
        {
            clients[slot] = new ClientSession(
                slot,
                slot * 101,
                new IPEndPoint(IPAddress.Loopback, 8189 + slot),
                slot switch
                {
                    1 => "One",
                    2 => "Two",
                    3 => "Three",
                    _ => $"Player {slot}",
                },
                TimeSpan.Zero)
            {
                IsAuthorized = true,
            };
        }

        return clients;
    }

    private static SimulationWorld CreateJoinedWorld(int playerCount)
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.CompleteLocalPlayerJoin(PlayerClass.Scout);
        for (byte slot = 2; slot <= playerCount; slot += 1)
        {
            JoinNetworkPlayer(world, slot);
        }

        return world;
    }

    private static void AdvanceWorld(SimulationWorld world, int ticks)
    {
        for (var tick = 0; tick < ticks; tick += 1)
        {
            world.AdvanceOneTick();
        }
    }

    private static void JoinNetworkPlayer(SimulationWorld world, byte slot)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, slot == 2 ? PlayerTeam.Blue : PlayerTeam.Red));
        var playerClass = slot == 2 ? PlayerClass.Scout : PlayerClass.Soldier;
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, playerClass));
    }

    private sealed class ThrowingServerMessageTransport : IServerMessageTransport
    {
        public int SendAttempts { get; private set; }

        public bool HasPendingMessages => false;

        public ServerMessagePacket Receive() => throw new InvalidOperationException("No test packets.");

        public void Send(ServerTransportPeer remotePeer, byte[] payload, MessageType? messageType = null)
        {
            _ = messageType;
            SendAttempts += 1;
            throw new SocketException((int)SocketError.WouldBlock);
        }

        public void SendProtocol64(
            ServerTransportPeer remotePeer,
            byte[] payload,
            Protocol64DeliveryDescriptor delivery,
            string? replacementKey = null)
        {
            _ = remotePeer;
            _ = payload;
            _ = delivery;
            _ = replacementKey;
            SendAttempts += 1;
            throw new SocketException((int)SocketError.WouldBlock);
        }
    }
}
