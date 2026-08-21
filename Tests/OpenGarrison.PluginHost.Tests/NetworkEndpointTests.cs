using System.Linq;
using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class NetworkEndpointTests
{
    [Fact]
    public void NativeSinglePortConnectionsUseOnlyUdp()
    {
        var endpoint = NetworkEndpoint.ForCurrentRuntimeSinglePort("127.0.0.1", 8190);
        var candidates = endpoint.GetConnectionCandidates();

        var candidate = Assert.Single(candidates);
        Assert.Equal(NetworkEndpointTransport.Udp, candidate.Transport);
        Assert.Equal(8190, candidate.Port);
    }

    [Fact]
    public void NativeAdvertisedEndpointsTryUdpThenWebSocketAndSkipQuic()
    {
        var endpoint = new NetworkEndpoint("127.0.0.1", 8190, 8191, QuicPort: 8192);
        var candidates = endpoint.GetConnectionCandidates();

        Assert.Equal(
            [
                NetworkEndpointTransport.Udp,
                NetworkEndpointTransport.WebSocket,
            ],
            candidates.Select(candidate => candidate.Transport));
        Assert.DoesNotContain(candidates, candidate => candidate.Transport == NetworkEndpointTransport.Quic);
    }
}
