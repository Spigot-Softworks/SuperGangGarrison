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

    private static ServerOutboundMessaging CreateOutboundMessaging(
        ThrowingServerMessageTransport transport,
        Dictionary<byte, ClientSession> clients,
        List<string> logs)
    {
        return new ServerOutboundMessaging(
            transport,
            "Test Server",
            new SimulationWorld(),
            clients,
            maxPlayableClients: 24,
            adminChatRouterGetter: static () => null,
            pluginHostGetter: static () => null,
            writeEvent: static (_, _) => { },
            logs.Add);
    }

    private static Dictionary<byte, ClientSession> CreateAuthorizedClients()
    {
        return new Dictionary<byte, ClientSession>
        {
            [1] = new ClientSession(1, 101, new IPEndPoint(IPAddress.Loopback, 8190), "One", TimeSpan.Zero)
            {
                IsAuthorized = true,
            },
            [2] = new ClientSession(2, 202, new IPEndPoint(IPAddress.Loopback, 8191), "Two", TimeSpan.Zero)
            {
                IsAuthorized = true,
            },
        };
    }

    private sealed class ThrowingServerMessageTransport : IServerMessageTransport
    {
        public int SendAttempts { get; private set; }

        public bool HasPendingMessages => false;

        public ServerMessagePacket Receive() => throw new InvalidOperationException("No test packets.");

        public void Send(ServerTransportPeer remotePeer, byte[] payload)
        {
            SendAttempts += 1;
            throw new SocketException((int)SocketError.WouldBlock);
        }
    }
}
