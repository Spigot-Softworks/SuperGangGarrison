using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class QuicClientTransportTests
{
    [Theory]
    [InlineData("quic64://example.com", true)]
    [InlineData("QUIC64://127.0.0.1:443", true)]
    [InlineData("https://example.com", false)]
    [InlineData("example.com", false)]
    public void QuicEndpointDetectionIsSchemeSpecific(string host, bool expected)
    {
        Assert.Equal(expected, QuicNetworkClientMessageTransport.IsQuicEndpoint(host));
    }

    [Fact]
    public void QuicEndpointRejectsPathsBeforeOpeningSocket()
    {
        var connected = QuicNetworkClientMessageTransport.TryConnect(
            "quic64://example.com/protocol",
            443,
            out var transport,
            out var error);

        Assert.False(connected);
        Assert.Null(transport);
        Assert.Contains("cannot include a path", error);
    }

    [Theory]
    [InlineData("ws64://example.com", true)]
    [InlineData("wss64://example.com:443", true)]
    [InlineData("ws://example.com", false)]
    public void DesktopWebSocketEndpointDetectionIsExplicit(string host, bool expected)
    {
        Assert.Equal(expected, WebSocketNetworkClientMessageTransport.IsWebSocketEndpoint(host));
    }

    [Fact]
    public void DesktopWebSocketEndpointPreservesRelayPathAndToken()
    {
        Assert.True(WebSocketNetworkClientMessageTransport.TryCreateEndpoint(
            "wss64://example.com/api/relay/ws/session/guest?token=secret",
            0,
            out var endpoint,
            out var error), error);

        Assert.Equal("wss", endpoint.Scheme);
        Assert.Equal("example.com", endpoint.Host);
        Assert.Equal("/api/relay/ws/session/guest", endpoint.AbsolutePath);
        Assert.Equal("?token=secret", endpoint.Query);
    }
}
