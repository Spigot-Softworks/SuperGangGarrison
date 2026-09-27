using System;

namespace OpenGarrison.ClientShared;

/// <summary>The edition is chosen by the published assembly, never by saved preferences.</summary>
public static class ClientDistribution
{
    public const string RestrictedEdition = "PracticeAndLastToDie";
    public const string Gg2OnlyEdition = "GG2Only";
    private static bool _initialized;
    private static Uri? _roomEndpoint;
    private static DateTimeOffset _roomGrantExpiry;

    public static string Edition { get; private set; } = "Full";
    public static bool IsRestricted => Edition == RestrictedEdition;
    public static bool IsGg2Only => Edition == Gg2OnlyEdition;
    public static string ContentId { get; private set; } = "dev";
    public static string BuildVersion { get; private set; } = "dev";
    public static Uri RoomServiceOrigin { get; private set; } = new(PrivateRoomClient.DefaultServiceOrigin);

    public static void Initialize(string? edition, string? contentId = null, string? buildVersion = null, string? roomServiceOrigin = null)
    {
        if (_initialized) throw new InvalidOperationException("Client edition is already initialized.");
        Edition = edition is null or "" or "Full" ? "Full"
            : edition == RestrictedEdition ? RestrictedEdition
            : edition == Gg2OnlyEdition ? Gg2OnlyEdition
            : throw new ArgumentException("Unknown browser edition.", nameof(edition));
        _initialized = true;
        ContentId = string.IsNullOrWhiteSpace(contentId) ? "dev" : contentId;
        BuildVersion = string.IsNullOrWhiteSpace(buildVersion) ? "dev" : buildVersion;
        if (!string.IsNullOrWhiteSpace(roomServiceOrigin)) RoomServiceOrigin = new(roomServiceOrigin, UriKind.Absolute);
    }

    public static void AuthorizeRoomEndpoint(Uri endpoint, DateTimeOffset expiry)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (endpoint.Scheme is not ("wss64" or "ws64") || expiry <= DateTimeOffset.UtcNow)
            throw new ArgumentException("Room endpoint or expiry is invalid.");
        _roomEndpoint = endpoint;
        _roomGrantExpiry = expiry;
    }

    public static void ClearRoomAccess()
    {
        _roomEndpoint = null;
        _roomGrantExpiry = default;
    }

    public static bool AllowsEndpoint(string? endpoint) => !IsRestricted
        || (_roomEndpoint is not null && _roomGrantExpiry > DateTimeOffset.UtcNow
            && Uri.TryCreate(endpoint, UriKind.Absolute, out var candidate)
            && string.Equals(candidate.AbsoluteUri, _roomEndpoint.AbsoluteUri, StringComparison.Ordinal))
        || IsGg2GatewayEndpoint(endpoint);

    public static Uri CreateGg2GatewayEndpoint(string host, int port)
    {
        if (string.IsNullOrWhiteSpace(host) || port is < 1 or > 65535)
            throw new ArgumentException("A GG2 server address and port are required.");
        var origin = RoomServiceOrigin;
        var builder = new UriBuilder(origin)
        {
            Scheme = origin.Scheme == Uri.UriSchemeHttps ? "wss" : "ws",
            Path = $"/api/gg2/ws/{Uri.EscapeDataString(host.Trim())}/{port}",
            Query = string.Empty,
        };
        return builder.Uri;
    }

    public static bool IsGg2GatewayEndpoint(string? endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var candidate)) return false;
        var origin = RoomServiceOrigin;
        var expectedScheme = origin.Scheme == Uri.UriSchemeHttps ? "wss" : "ws";
        return candidate.Scheme == expectedScheme
            && string.Equals(candidate.Host, origin.Host, StringComparison.OrdinalIgnoreCase)
            && candidate.Port == origin.Port
            && candidate.AbsolutePath.StartsWith("/api/gg2/ws/", StringComparison.Ordinal)
            && string.IsNullOrEmpty(candidate.Query)
            && string.IsNullOrEmpty(candidate.Fragment);
    }
}
