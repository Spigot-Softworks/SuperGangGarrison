using OpenGarrison.Client;
using OpenGarrison.ClientShared;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class FriendPresenceSessionResolverTests
{
    [Fact]
    public void HostedHeartbeatRequestsObservedPublicAddress()
    {
        var heartbeat = new PresenceHeartbeatRequest
        {
            Host = "127.0.0.1",
            WebSocketPort = 8191,
            WebSocketUrl = "ws://127.0.0.1:8191/",
        };

        FriendPresenceSessionResolver.ApplyObservedUdpEndpoint(heartbeat, 8190, joinable: true);

        Assert.Equal(string.Empty, heartbeat.Host);
        Assert.Equal(8190, heartbeat.UdpPort);
        Assert.Equal(0, heartbeat.WebSocketPort);
        Assert.Equal(string.Empty, heartbeat.WebSocketUrl);
        Assert.True(heartbeat.Joinable);
    }

    [Fact]
    public void JoinablePresenceCreatesAdvertisedEndpoint()
    {
        var presence = new FriendPresenceEntry
        {
            FriendCode = "OG2-ABCD-EFGH",
            Online = true,
            Joinable = true,
            Host = "203.0.113.42",
            UdpPort = 8190,
        };

        Assert.True(FriendPresenceSessionResolver.TryCreateJoinEndpoint(presence, out var endpoint));
        Assert.Equal("203.0.113.42", endpoint.Host);
        Assert.Equal(8190, endpoint.UdpPort);
    }

    [Fact]
    public void RelayHeartbeatAdvertisesProtocol64WithoutUdpFallback()
    {
        var heartbeat = new PresenceHeartbeatRequest();

        FriendPresenceSessionResolver.ApplyRelayEndpoint(
            heartbeat,
            "wss64://relay.example.com/api/relay/ws/run/guest?token=join-secret",
            joinable: true);

        Assert.Equal("relay.example.com", heartbeat.Host);
        Assert.Equal(0, heartbeat.UdpPort);
        Assert.Equal(0, heartbeat.WebSocketPort);
        Assert.Equal("wss64://relay.example.com/api/relay/ws/run/guest?token=join-secret", heartbeat.WebSocketUrl);
        Assert.True(heartbeat.Joinable);

        var presence = new FriendPresenceEntry
        {
            Online = true,
            Joinable = heartbeat.Joinable,
            Host = heartbeat.Host,
            WebSocketUrl = heartbeat.WebSocketUrl,
        };
        Assert.True(FriendPresenceSessionResolver.TryCreateJoinEndpoint(presence, out var endpoint));
        var candidate = Assert.Single(endpoint.GetConnectionCandidates());
        Assert.Equal(NetworkEndpointTransport.WebSocket, candidate.Transport);
        Assert.Equal(heartbeat.WebSocketUrl, candidate.Host);
    }

    [Theory]
    [InlineData("last_to_die", "", true)]
    [InlineData("server", "Last to Die", true)]
    [InlineData("server", "KingOfTheHill", false)]
    public void LastToDieRoomDetectionUsesPresenceMode(string status, string mode, bool expected)
    {
        var presence = new FriendPresenceEntry
        {
            Status = status,
            Mode = mode,
        };

        Assert.Equal(expected, FriendPresenceSessionResolver.IsLastToDieRoom(presence));
    }

    [Theory]
    [InlineData(false, true, "203.0.113.42", 8190)]
    [InlineData(true, false, "203.0.113.42", 8190)]
    [InlineData(true, true, "", 8190)]
    [InlineData(true, true, "203.0.113.42", 0)]
    public void NonJoinablePresenceIsRejected(bool online, bool joinable, string host, int udpPort)
    {
        var presence = new FriendPresenceEntry
        {
            Online = online,
            Joinable = joinable,
            Host = host,
            UdpPort = udpPort,
        };

        Assert.False(FriendPresenceSessionResolver.TryCreateJoinEndpoint(presence, out _));
    }
}
