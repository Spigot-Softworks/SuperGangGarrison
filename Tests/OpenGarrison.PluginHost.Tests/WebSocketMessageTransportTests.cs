using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Linq;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class WebSocketMessageTransportTests
{
    [Fact]
    public async Task WebSocketHostReceivesAndSendsBinaryProtocolPayloads()
    {
        var port = GetFreeTcpPort();
        using var udp = new UdpClient(0);
        var transport = new CompositeServerMessageTransport(udp);
        using var host = new WebSocketServerHost(port, certificatePath: null, certificatePassword: null, transport, _ => { });
        host.Start();

        using var client = new ClientWebSocket();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await client.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/opengarrison/ws"), timeout.Token);

        var outbound = new byte[] { 1, 2, 3, 4 };
        await client.SendAsync(outbound, WebSocketMessageType.Binary, endOfMessage: true, timeout.Token);

        var packet = await WaitForServerPacketAsync(transport, timeout.Token);
        Assert.Equal(outbound, packet.Payload);
        Assert.Equal(ServerTransportKind.WebSocket, packet.RemotePeer.Kind);
        Assert.Equal(IPAddress.Loopback, packet.RemotePeer.RemoteAddress);

        var response = new byte[] { 9, 8, 7 };
        transport.Send(packet.RemotePeer, response);

        var inbound = new byte[16];
        var receive = await client.ReceiveAsync(inbound, timeout.Token);
        Assert.Equal(WebSocketMessageType.Binary, receive.MessageType);
        Assert.True(receive.EndOfMessage);
        Assert.Equal(response, inbound.AsSpan(0, receive.Count).ToArray());
    }

    [Fact]
    public async Task WebSocketSnapshotQueueUsesExplicitMessageTypeInsteadOfPayloadPrefix()
    {
        var port = GetFreeTcpPort();
        using var udp = new UdpClient(0);
        var transport = new CompositeServerMessageTransport(udp);
        using var host = new WebSocketServerHost(port, certificatePath: null, certificatePassword: null, transport, _ => { });
        host.Start();

        using var client = new ClientWebSocket();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await client.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/opengarrison/ws"), timeout.Token);

        await client.SendAsync(new byte[] { 1 }, WebSocketMessageType.Binary, endOfMessage: true, timeout.Token);
        var packet = await WaitForServerPacketAsync(transport, timeout.Token);

        for (var index = 0; index < 32; index += 1)
        {
            transport.Send(packet.RemotePeer, new byte[] { 0x41, (byte)index });
        }

        var staleSnapshotPayload = new byte[] { 0x00, 0xA1, 0x01 };
        var latestSnapshotPayload = new byte[] { 0x00, 0xA2, 0x02 };
        transport.Send(packet.RemotePeer, staleSnapshotPayload, MessageType.Snapshot);
        transport.Send(packet.RemotePeer, latestSnapshotPayload, MessageType.Snapshot);

        var received = await ReceiveUntilPayloadAsync(client, latestSnapshotPayload, timeout.Token);

        Assert.DoesNotContain(received, payload => payload.SequenceEqual(staleSnapshotPayload));
    }

    [Fact]
    public async Task WebSocketSnapshotPayloadIsSentBeforeQueuedReliablePayloads()
    {
        var port = GetFreeTcpPort();
        using var udp = new UdpClient(0);
        var transport = new CompositeServerMessageTransport(udp);
        using var host = new WebSocketServerHost(port, certificatePath: null, certificatePassword: null, transport, _ => { });
        host.Start();

        using var client = new ClientWebSocket();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await client.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/opengarrison/ws"), timeout.Token);

        await client.SendAsync(new byte[] { 1 }, WebSocketMessageType.Binary, endOfMessage: true, timeout.Token);
        var packet = await WaitForServerPacketAsync(transport, timeout.Token);

        var snapshotPayload = new byte[] { 0x00, 0xB1, 0x01 };
        transport.Send(packet.RemotePeer, snapshotPayload, MessageType.Snapshot);
        for (var index = 0; index < 8; index += 1)
        {
            transport.Send(packet.RemotePeer, new byte[] { 0x41, (byte)index });
        }

        var firstPayload = await ReceivePayloadAsync(client, timeout.Token);

        Assert.Equal(snapshotPayload, firstPayload);
    }

    [Fact]
    public void TransportPeerEqualityIncludesTransportKind()
    {
        var udpPeer = ServerTransportPeer.FromUdpEndPoint(new IPEndPoint(IPAddress.Loopback, 8190));
        var websocketPeer = new ServerTransportPeer(
            ServerTransportKind.WebSocket,
            udpPeer.Id,
            "forced collision",
            udpEndPoint: null,
            IPAddress.Loopback,
            remotePort: 8191);

        Assert.NotEqual(udpPeer, websocketPeer);
    }

    private static async Task<ServerMessagePacket> WaitForServerPacketAsync(
        CompositeServerMessageTransport transport,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (transport.HasPendingMessages)
            {
                return transport.Receive();
            }

            await Task.Delay(10, cancellationToken);
        }

        throw new TimeoutException("Timed out waiting for WebSocket transport packet.");
    }

    private static async Task<IReadOnlyList<byte[]>> ReceiveUntilPayloadAsync(
        ClientWebSocket client,
        byte[] expectedPayload,
        CancellationToken cancellationToken)
    {
        var received = new List<byte[]>();
        while (!cancellationToken.IsCancellationRequested)
        {
            var payload = await ReceivePayloadAsync(client, cancellationToken);
            received.Add(payload);
            if (payload.SequenceEqual(expectedPayload))
            {
                return received;
            }
        }

        throw new TimeoutException("Timed out waiting for expected WebSocket payload.");
    }

    private static async Task<byte[]> ReceivePayloadAsync(ClientWebSocket client, CancellationToken cancellationToken)
    {
        var buffer = new byte[64];
        var receive = await client.ReceiveAsync(buffer, cancellationToken);
        Assert.Equal(WebSocketMessageType.Binary, receive.MessageType);
        Assert.True(receive.EndOfMessage);
        return buffer.AsSpan(0, receive.Count).ToArray();
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
