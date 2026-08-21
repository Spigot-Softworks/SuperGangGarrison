using System.Linq;
using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class NetworkEndpointTests
{
    [Fact]
    public void NativeSinglePortConnectionsPreferUdpAndRetainQuicFallback()
    {
        var endpoint = NetworkEndpoint.ForCurrentRuntimeSinglePort("127.0.0.1", 8190);
        var candidates = endpoint.GetConnectionCandidates();

        Assert.NotEmpty(candidates);
        Assert.Equal(NetworkEndpointTransport.Udp, candidates[0].Transport);
        Assert.Equal(8190, candidates[0].Port);
        Assert.Contains(candidates, candidate => candidate.Transport == NetworkEndpointTransport.Quic);
    }

    [Fact]
    public void NativeAdvertisedEndpointsTryUdpBeforeOtherFallbacks()
    {
        var endpoint = new NetworkEndpoint("127.0.0.1", 8190, 8191, QuicPort: 8192);
        var candidates = endpoint.GetConnectionCandidates();

        Assert.Equal(
            [
                NetworkEndpointTransport.Udp,
                NetworkEndpointTransport.WebSocket,
                NetworkEndpointTransport.Quic,
            ],
            candidates.Select(candidate => candidate.Transport));

        var quicCandidate = candidates.Single(candidate => candidate.Transport == NetworkEndpointTransport.Quic);
        Assert.Equal("quic64://127.0.0.1", quicCandidate.Host);
        Assert.Equal(8192, quicCandidate.Port);
    }
}
