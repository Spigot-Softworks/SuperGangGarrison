#nullable enable

using System;
using System.Globalization;

namespace OpenGarrison.Client;

internal enum NetworkEndpointTransport
{
    Udp,
    WebSocket,
}

internal readonly record struct NetworkEndpoint(string Host, int UdpPort, int WebSocketPort, string WebSocketUrl = "")
{
    public bool HasUdpEndpoint => UdpPort is > 0 and <= 65535;
    public bool HasWebSocketEndpoint => !string.IsNullOrWhiteSpace(WebSocketUrl) || WebSocketPort is > 0 and <= 65535;

    public bool TryResolveForCurrentRuntime(out string host, out int port, out NetworkEndpointTransport transport)
    {
        host = Host.Trim();
        port = 0;
        transport = NetworkEndpointTransport.Udp;
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        if (OperatingSystem.IsBrowser())
        {
            var webSocketUrl = WebSocketUrl.Trim();
            if (!string.IsNullOrWhiteSpace(webSocketUrl))
            {
                host = webSocketUrl;
                transport = NetworkEndpointTransport.WebSocket;
                return true;
            }

            if (WebSocketPort is <= 0 or > 65535)
            {
                return false;
            }

            port = WebSocketPort;
            transport = NetworkEndpointTransport.WebSocket;
            return true;
        }

        if (!HasUdpEndpoint)
        {
            return false;
        }

        port = UdpPort;
        return true;
    }

    public string AddressLabel
    {
        get
        {
            var host = Host.Trim();
            if (HasUdpEndpoint && HasWebSocketEndpoint && UdpPort != WebSocketPort)
            {
                var webSocketLabel = string.IsNullOrWhiteSpace(WebSocketUrl) ? WebSocketPort.ToString(CultureInfo.InvariantCulture) : WebSocketUrl.Trim();
                return $"{host}:udp {UdpPort} / ws {webSocketLabel}";
            }

            if (HasWebSocketEndpoint && !HasUdpEndpoint)
            {
                return string.IsNullOrWhiteSpace(WebSocketUrl)
                    ? $"{host}:ws {WebSocketPort}"
                    : WebSocketUrl.Trim();
            }

            if (HasUdpEndpoint)
            {
                return $"{host}:{UdpPort}";
            }

            return host;
        }
    }

    public int QueryPort => HasUdpEndpoint ? UdpPort : 0;

    public static NetworkEndpoint ForUdp(string host, int port)
    {
        return new NetworkEndpoint(host, NormalizePort(port), 0);
    }

    public static NetworkEndpoint ForCurrentRuntimeSinglePort(string host, int port)
    {
        var normalizedPort = NormalizePort(port);
        return OperatingSystem.IsBrowser()
            ? new NetworkEndpoint(host, 0, normalizedPort)
            : new NetworkEndpoint(host, normalizedPort, 0);
    }

    private static int NormalizePort(int port)
    {
        return port is > 0 and <= 65535 ? port : 0;
    }
}
