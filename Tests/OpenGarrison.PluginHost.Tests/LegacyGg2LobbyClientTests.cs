using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LegacyGg2LobbyClientTests
{
    [Fact]
    public async Task ReadsAdvertisedGg2ServerFromNativeLobbyStream()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var serverTask = Task.Run(async () =>
            {
                using var peer = await listener.AcceptTcpClientAsync();
                using var stream = peer.GetStream();
                var request = new byte[32];
                await stream.ReadExactlyAsync(request);
                Assert.Equal(Convert.FromHexString(
                    "297d0df4430cbf61640a640897eaef57" +
                    "1ccf16b1436d856f504dcc1af306aaa7"), request);

                var block = CreateBlock(Convert.FromHexString(
                    LegacyGg2Wire.ProtocolUuid.Replace("-", string.Empty)));
                await stream.WriteAsync(BigEndian32(1));
                await stream.WriteAsync(BigEndian32((uint)block.Length));
                await stream.WriteAsync(block);
            });

            var servers = await LegacyGg2LobbyClient.FetchAsync("127.0.0.1", port);
            await serverTask;
            var server = Assert.Single(servers);
            Assert.Equal("Vindicator's - US West", server.Name);
            Assert.Equal("45.59.102.99", server.Host);
            Assert.Equal(8190, server.Port);
            Assert.Equal("koth_corinth", server.Map);
            Assert.Equal("2.9.2", server.Version);
            Assert.Equal(7, server.Bots);
            Assert.Equal(10, server.Slots);
            Assert.True(server.IsCompatible);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public void RejectsOtherProtocolAndTruncatedBlocks()
    {
        var otherProtocol = CreateBlock(new byte[16]);
        Assert.True(LegacyGg2LobbyClient.TryParseServerBlock(otherProtocol, out var server));
        Assert.False(server.IsCompatible);
        Assert.False(LegacyGg2LobbyClient.TryParseServerBlock(otherProtocol.AsSpan(0, 15), out _));
    }

    [Fact]
    public void ReadsBrowserLobbyResponseWithoutAllowingMalformedRowsToHideValidServers()
    {
        using var json = JsonDocument.Parse("""
            {"servers":[{"host":"45.59.102.99","port":8190,"name":"Vindicator's","map":"koth_corinth","game":"gg2","version":"2.9.2","players":3,"bots":1,"slots":10,"isPrivate":false,"isCompatible":true},{"host":"bad"}]}
            """);
        var server = Assert.Single(LegacyGg2LobbyClient.ParseBrowserResponse(json.RootElement));
        Assert.Equal("Vindicator's", server.Name);
        Assert.Equal("koth_corinth", server.Map);
        Assert.True(server.IsCompatible);
    }

    private static byte[] CreateBlock(byte[] protocol)
    {
        using var stream = new MemoryStream();
        stream.WriteByte(0); // TCP
        stream.Write(BigEndian16(8190));
        stream.Write([45, 59, 102, 99]);
        stream.Write(new byte[18]); // lobby server ID
        stream.Write(BigEndian16(10));
        stream.Write(BigEndian16(0));
        stream.Write(BigEndian16(7));
        stream.Write(BigEndian16(0));
        stream.Write(BigEndian16(4));
        WritePair(stream, "name", Encoding.UTF8.GetBytes("Vindicator's - US West"));
        WritePair(stream, "map", Encoding.UTF8.GetBytes("koth_corinth"));
        WritePair(stream, "game_ver", Encoding.UTF8.GetBytes("2.9.2"));
        WritePair(stream, "protocol_id", protocol);
        return stream.ToArray();
    }

    private static void WritePair(Stream stream, string key, byte[] value)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        stream.WriteByte(checked((byte)keyBytes.Length));
        stream.Write(keyBytes);
        stream.Write(BigEndian16(checked((ushort)value.Length)));
        stream.Write(value);
    }

    private static byte[] BigEndian16(ushort value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] BigEndian32(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }
}
