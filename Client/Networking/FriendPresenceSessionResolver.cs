#nullable enable

using OpenGarrison.ClientShared;

namespace OpenGarrison.Client;

internal static class FriendPresenceSessionResolver
{
    public static void ApplyRelayEndpoint(
        PresenceHeartbeatRequest request,
        string guestWebSocketUrl,
        bool joinable)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Host = TryGetRelayHost(guestWebSocketUrl, out var host) ? host : string.Empty;
        request.UdpPort = 0;
        request.WebSocketPort = 0;
        request.WebSocketUrl = guestWebSocketUrl?.Trim() ?? string.Empty;
        request.Joinable = joinable
            && !string.IsNullOrWhiteSpace(request.Host)
            && WebSocketNetworkClientMessageTransport.IsWebSocketEndpoint(request.WebSocketUrl);
    }

    public static void ApplyObservedUdpEndpoint(
        PresenceHeartbeatRequest request,
        int udpPort,
        bool joinable)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Host = string.Empty;
        request.UdpPort = Math.Clamp(udpPort, 0, 65535);
        request.WebSocketPort = 0;
        request.WebSocketUrl = string.Empty;
        request.Joinable = joinable && request.UdpPort > 0;
    }

    public static bool TryCreateJoinEndpoint(
        FriendPresenceEntry? presence,
        out NetworkEndpoint endpoint)
    {
        endpoint = default;
        if (presence is not { Online: true, Joinable: true }
            || string.IsNullOrWhiteSpace(presence.Host)
            || (presence.UdpPort <= 0
                && presence.WebSocketPort <= 0
                && string.IsNullOrWhiteSpace(presence.WebSocketUrl)))
        {
            return false;
        }

        endpoint = new NetworkEndpoint(
            presence.Host.Trim(),
            presence.UdpPort,
            presence.WebSocketPort,
            presence.WebSocketUrl);
        return true;
    }

    public static bool IsLastToDieRoom(FriendPresenceEntry? presence)
    {
        return presence is not null
            && (string.Equals(presence.Status, "last_to_die", StringComparison.OrdinalIgnoreCase)
                || string.Equals(presence.Mode, "Last to Die", StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryGetRelayHost(string? value, out string host)
    {
        host = string.Empty;
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("ws64" or "wss64")
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        host = uri.Host;
        return true;
    }
}
