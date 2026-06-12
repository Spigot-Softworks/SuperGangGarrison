using System.Net;
using System.Net.Sockets;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
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
        JoinNetworkPlayer(world, 2);
        JoinNetworkPlayer(world, 3);

        var clients = CreateAuthorizedClients(3);
        var transport = new ThrowingServerMessageTransport();
        var logs = new List<string>();
        var outbound = CreateOutboundMessaging(transport, clients, logs, world);

        outbound.BroadcastChat(clients[1], "!votevip 2", teamOnly: false);

        Assert.Contains(logs, log => log.Contains("VIP vote started", StringComparison.Ordinal));
        Assert.DoesNotContain(logs, log => log.Contains("VIP vote passed", StringComparison.Ordinal));

        outbound.BroadcastChat(clients[2], "!vote yes", teamOnly: false);

        Assert.Contains(logs, log => log.Contains("VIP vote passed: Two will be the Blue VIP.", StringComparison.Ordinal));
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

    private static void JoinNetworkPlayer(SimulationWorld world, byte slot)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, PlayerClass.Scout));
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
    }
}
