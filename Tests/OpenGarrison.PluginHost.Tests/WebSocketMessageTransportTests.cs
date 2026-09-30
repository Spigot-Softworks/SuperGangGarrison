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

    [Fact]
    public void RelayDiagnosticsRedactBearerToken()
    {
        var endpoint = new Uri("wss://relay.example.com/api/relay/ws/run/host?token=host-secret");

        var redacted = CompositeServerMessageTransport.RedactRelayEndpoint(endpoint);

        Assert.Contains("/api/relay/ws/run/host", redacted, StringComparison.Ordinal);
        Assert.Contains("token=REDACTED", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("host-secret", redacted, StringComparison.Ordinal);
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
