using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class WebSocketEndpointAdvertisementTests
{
    [Fact]
    public void NetworkEndpointTreatsAdvertisedWebSocketUrlAsBrowserEndpoint()
    {
        var endpoint = new NetworkEndpoint(
            "game.example.com",
            UdpPort: 8190,
            WebSocketPort: 8191,
            WebSocketUrl: "wss://game.example.com/opengarrison/ws");

        Assert.True(endpoint.HasUdpEndpoint);
        Assert.True(endpoint.HasWebSocketEndpoint);
        Assert.Contains("wss://game.example.com/opengarrison/ws", endpoint.AddressLabel);
    }

    [Fact]
    public void ServerLaunchOptionsAcceptsPublicWebSocketUrl()
    {
        var options = ServerLaunchOptions.Load(
            [
                "--websocket-port",
                "8191",
                "--public-websocket-url",
                "wss://game.example.com/opengarrison/ws",
            ],
            _ => new ServerSettings());

        Assert.Equal(8191, options.WebSocketPort);
        Assert.Equal("wss://game.example.com/opengarrison/ws", options.PublicWebSocketUrl);
    }

    [Fact]
    public void ServerLaunchOptionsDefaultsWebSocketPortToServerPort()
    {
        var options = ServerLaunchOptions.Load(
            [
                "--port",
                "9000",
            ],
            _ => new ServerSettings());

        Assert.Equal(9000, options.Port);
        Assert.Equal(9000, options.WebSocketPort);
    }

    [Fact]
    public void ServerLaunchOptionsCanDisableWebSocketListener()
    {
        var options = ServerLaunchOptions.Load(
            [
                "--port",
                "9000",
                "--no-websocket",
            ],
            _ => new ServerSettings());

        Assert.Equal(0, options.WebSocketPort);
    }

    [Fact]
    public void ServerLaunchOptionsRejectsNonWebSocketPublicUrl()
    {
        var options = ServerLaunchOptions.Load(
            [
                "--public-websocket-url",
                "https://game.example.com/opengarrison/ws",
            ],
            _ => new ServerSettings());

        Assert.Null(options.PublicWebSocketUrl);
    }
}
