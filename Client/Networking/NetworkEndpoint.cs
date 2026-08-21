#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Client;

internal enum NetworkEndpointTransport
{
    Udp,
    WebSocket,
    Quic,
}

internal readonly record struct NetworkEndpointCandidate(string Host, int Port, NetworkEndpointTransport Transport);

internal readonly record struct NetworkEndpoint(string Host, int UdpPort, int WebSocketPort, string WebSocketUrl = "", int QuicPort = 0, string QuicUrl = "")
{
    public bool HasUdpEndpoint => UdpPort is > 0 and <= 65535;
    public bool HasWebSocketEndpoint => !string.IsNullOrWhiteSpace(WebSocketUrl) || WebSocketPort is > 0 and <= 65535;
    public bool HasQuicEndpoint => !string.IsNullOrWhiteSpace(QuicUrl) || QuicPort is > 0 and <= 65535;

    public bool TryResolveForCurrentRuntime(out string host, out int port, out NetworkEndpointTransport transport)
    {
        foreach (var candidate in EnumerateConnectionCandidates())
        {
            host = candidate.Host;
            port = candidate.Port;
            transport = candidate.Transport;
            return true;
        }

        host = string.Empty;
        port = 0;
        transport = NetworkEndpointTransport.Udp;
        return false;
    }

    public IReadOnlyList<NetworkEndpointCandidate> GetConnectionCandidates()
        => [.. EnumerateConnectionCandidates()];

    private IEnumerable<NetworkEndpointCandidate> EnumerateConnectionCandidates()
    {
        var host = Host.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            yield break;
        }

        if (OperatingSystem.IsBrowser())
        {
            var webSocketUrl = WebSocketUrl.Trim();
            if (!string.IsNullOrWhiteSpace(webSocketUrl))
            {
                yield return new NetworkEndpointCandidate(webSocketUrl, 0, NetworkEndpointTransport.WebSocket);
                yield break;
            }

            if (WebSocketPort is > 0 and <= 65535)
            {
                yield return new NetworkEndpointCandidate(host, WebSocketPort, NetworkEndpointTransport.WebSocket);
            }

            yield break;
        }

        // Native clients intentionally use the shipped UDP transport. QUIC is
        // retained in the codebase for later work, but is not a selectable or
        // advertised native connection fallback in this release.
        if (HasUdpEndpoint)
        {
            yield return new NetworkEndpointCandidate(host, UdpPort, NetworkEndpointTransport.Udp);
        }

        var nativeWebSocketUrl = WebSocketUrl.Trim();
        if (!string.IsNullOrWhiteSpace(nativeWebSocketUrl))
        {
            if (TryCreateProtocol64WebSocketEndpoint(nativeWebSocketUrl, out var protocol64WebSocketUrl))
            {
                yield return new NetworkEndpointCandidate(protocol64WebSocketUrl, 0, NetworkEndpointTransport.WebSocket);
            }
        }
        else if (WebSocketPort is > 0 and <= 65535)
        {
            yield return new NetworkEndpointCandidate($"ws64://{host}", WebSocketPort, NetworkEndpointTransport.WebSocket);
        }

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
                return $"{host}:udp {UdpPort}";
            }

            if (HasQuicEndpoint)
            {
                var quicLabel = string.IsNullOrWhiteSpace(QuicUrl) ? QuicPort.ToString(CultureInfo.InvariantCulture) : QuicUrl.Trim();
                return $"{host}:quic {quicLabel}";
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

    private static bool TryCreateProtocol64WebSocketEndpoint(string value, out string endpoint)
    {
        endpoint = string.Empty;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("ws" or "wss" or "ws64" or "wss64")
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        var scheme = uri.Scheme is "wss" or "wss64" ? "wss64" : "ws64";
        var builder = new UriBuilder(scheme, uri.Host)
        {
            Port = uri.IsDefaultPort ? -1 : uri.Port,
            Path = string.IsNullOrEmpty(uri.AbsolutePath) ? string.Empty : uri.AbsolutePath,
            Query = uri.Query.TrimStart('?'),
            Fragment = string.Empty,
        };
        endpoint = builder.Uri.ToString();
        return true;
    }
}
