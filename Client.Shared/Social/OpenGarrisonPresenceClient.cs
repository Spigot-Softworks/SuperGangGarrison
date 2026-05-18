#nullable enable

using System.Net.Http.Json;
using System.Text.Json.Serialization;
using OpenGarrison.Core;

namespace OpenGarrison.ClientShared;

public sealed class OpenGarrisonPresenceClient
{
    public const string DefaultApiBaseUrl = OpenGarrisonPreferencesDocument.DefaultApiBaseUrl;

    private static readonly HttpClient DesktopHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8),
    };

    private readonly Uri _baseUri;

    public OpenGarrisonPresenceClient(string? baseUrl = null)
    {
        _baseUri = new Uri(string.IsNullOrWhiteSpace(baseUrl) ? DefaultApiBaseUrl : baseUrl.Trim(), UriKind.Absolute);
    }

    public async Task SendHeartbeatAsync(PresenceHeartbeatRequest request)
    {
        var httpClient = GetHttpClient();
        if (httpClient is null)
        {
            return;
        }

        using var response = await httpClient.PostAsJsonAsync(BuildUri("/api/presence/heartbeat"), request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task SendOfflineAsync(ClientIdentityDocument identity)
    {
        var httpClient = GetHttpClient();
        if (httpClient is null)
        {
            return;
        }

        var request = new PresenceOfflineRequest
        {
            ClientId = identity.ClientId,
            ClientSecret = identity.ClientSecret,
        };
        using var response = await httpClient.PostAsJsonAsync(BuildUri("/api/presence/offline"), request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<FriendPresenceEntry>> GetFriendPresenceAsync(IEnumerable<string> friendCodes)
    {
        var httpClient = GetHttpClient();
        if (httpClient is null)
        {
            return [];
        }

        var normalizedCodes = friendCodes
            .Where(code => ClientIdentityDocument.TryNormalizeFriendCode(code, out _))
            .Select(code => ClientIdentityDocument.TryNormalizeFriendCode(code, out var normalized) ? normalized : string.Empty)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedCodes.Length == 0)
        {
            return [];
        }

        var uri = BuildUri($"/api/presence?codes={Uri.EscapeDataString(string.Join(',', normalizedCodes))}");
        var response = await httpClient.GetFromJsonAsync<FriendPresenceResponse>(uri).ConfigureAwait(false);
        return response?.Friends ?? [];
    }

    public async Task<FriendRequestEntry> SendFriendRequestAsync(ClientIdentityDocument identity, string targetFriendCode)
    {
        var httpClient = GetHttpClient() ?? throw new InvalidOperationException("HTTP client is unavailable.");
        var request = new FriendRequestCreateRequest
        {
            ClientId = identity.ClientId,
            ClientSecret = identity.ClientSecret,
            FriendCode = identity.FriendCode,
            DisplayName = identity.DisplayName,
            TargetFriendCode = targetFriendCode,
        };
        using var response = await httpClient.PostAsJsonAsync(BuildUri("/api/friends/request"), request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FriendRequestEntry>().ConfigureAwait(false)
            ?? throw new InvalidOperationException("Friend request response was empty.");
    }

    public async Task<IReadOnlyList<FriendRequestEntry>> GetFriendRequestsAsync(ClientIdentityDocument identity)
    {
        var httpClient = GetHttpClient();
        if (httpClient is null)
        {
            return [];
        }

        var request = new FriendRequestsListRequest
        {
            ClientId = identity.ClientId,
            ClientSecret = identity.ClientSecret,
            FriendCode = identity.FriendCode,
            DisplayName = identity.DisplayName,
        };
        using var response = await httpClient.PostAsJsonAsync(BuildUri("/api/friends/requests"), request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<FriendRequestsResponse>().ConfigureAwait(false);
        return payload?.Requests ?? [];
    }

    public async Task<FriendRequestEntry> RespondToFriendRequestAsync(ClientIdentityDocument identity, int requestId, bool accept)
    {
        var httpClient = GetHttpClient() ?? throw new InvalidOperationException("HTTP client is unavailable.");
        var request = new FriendRequestRespondRequest
        {
            ClientId = identity.ClientId,
            ClientSecret = identity.ClientSecret,
            FriendCode = identity.FriendCode,
            DisplayName = identity.DisplayName,
            RequestId = requestId,
            Accept = accept,
        };
        using var response = await httpClient.PostAsJsonAsync(BuildUri("/api/friends/respond"), request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FriendRequestEntry>().ConfigureAwait(false)
            ?? throw new InvalidOperationException("Friend request response was empty.");
    }

    public async Task<FriendDirectMessageEntry> SendDirectMessageAsync(ClientIdentityDocument identity, string targetFriendCode, string text)
    {
        var httpClient = GetHttpClient() ?? throw new InvalidOperationException("HTTP client is unavailable.");
        var request = new DirectMessageSendRequest
        {
            ClientId = identity.ClientId,
            ClientSecret = identity.ClientSecret,
            FriendCode = identity.FriendCode,
            DisplayName = identity.DisplayName,
            TargetFriendCode = targetFriendCode,
            Text = text,
        };
        using var response = await httpClient.PostAsJsonAsync(BuildUri("/api/messages/send"), request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FriendDirectMessageEntry>().ConfigureAwait(false)
            ?? throw new InvalidOperationException("Message response was empty.");
    }

    public async Task<IReadOnlyList<FriendDirectMessageEntry>> PollDirectMessagesAsync(ClientIdentityDocument identity, long afterId)
    {
        var httpClient = GetHttpClient();
        if (httpClient is null)
        {
            return [];
        }

        var request = new DirectMessagesPollRequest
        {
            ClientId = identity.ClientId,
            ClientSecret = identity.ClientSecret,
            FriendCode = identity.FriendCode,
            DisplayName = identity.DisplayName,
            AfterId = afterId,
        };
        using var response = await httpClient.PostAsJsonAsync(BuildUri("/api/messages/poll"), request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<DirectMessagesPollResponse>().ConfigureAwait(false);
        return payload?.Messages ?? [];
    }

    private static HttpClient? GetHttpClient()
    {
        return OperatingSystem.IsBrowser()
            ? ClientRuntimeBootstrap.GetBrowserHttpClient()
            : DesktopHttpClient;
    }

    private Uri BuildUri(string relativePath)
    {
        return new Uri(_baseUri, relativePath);
    }
}

public sealed class PresenceHeartbeatRequest
{
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("clientSecret")]
    public string ClientSecret { get; set; } = string.Empty;

    [JsonPropertyName("friendCode")]
    public string FriendCode { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "menu";

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = string.Empty;

    [JsonPropertyName("map")]
    public string Map { get; set; } = string.Empty;

    [JsonPropertyName("serverName")]
    public string ServerName { get; set; } = string.Empty;

    [JsonPropertyName("host")]
    public string Host { get; set; } = string.Empty;

    [JsonPropertyName("udpPort")]
    public int UdpPort { get; set; }

    [JsonPropertyName("webSocketPort")]
    public int WebSocketPort { get; set; }

    [JsonPropertyName("webSocketUrl")]
    public string WebSocketUrl { get; set; } = string.Empty;

    [JsonPropertyName("joinable")]
    public bool Joinable { get; set; }

    [JsonPropertyName("playerCard")]
    public string PlayerCardJson { get; set; } = string.Empty;
}

public sealed class PresenceOfflineRequest
{
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("clientSecret")]
    public string ClientSecret { get; set; } = string.Empty;
}

public sealed class FriendPresenceResponse
{
    [JsonPropertyName("friends")]
    public List<FriendPresenceEntry> Friends { get; set; } = [];
}

public sealed class FriendPresenceEntry
{
    [JsonPropertyName("friendCode")]
    public string FriendCode { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("online")]
    public bool Online { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "offline";

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = string.Empty;

    [JsonPropertyName("map")]
    public string Map { get; set; } = string.Empty;

    [JsonPropertyName("serverName")]
    public string ServerName { get; set; } = string.Empty;

    [JsonPropertyName("host")]
    public string Host { get; set; } = string.Empty;

    [JsonPropertyName("udpPort")]
    public int UdpPort { get; set; }

    [JsonPropertyName("webSocketPort")]
    public int WebSocketPort { get; set; }

    [JsonPropertyName("webSocketUrl")]
    public string WebSocketUrl { get; set; } = string.Empty;

    [JsonPropertyName("joinable")]
    public bool Joinable { get; set; }

    [JsonPropertyName("lastSeenIso")]
    public string LastSeenIso { get; set; } = string.Empty;

    [JsonPropertyName("playerCard")]
    public string PlayerCardJson { get; set; } = string.Empty;
}

public sealed class FriendRequestCreateRequest
{
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("clientSecret")]
    public string ClientSecret { get; set; } = string.Empty;

    [JsonPropertyName("friendCode")]
    public string FriendCode { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("targetFriendCode")]
    public string TargetFriendCode { get; set; } = string.Empty;
}

public sealed class FriendRequestsListRequest
{
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("clientSecret")]
    public string ClientSecret { get; set; } = string.Empty;

    [JsonPropertyName("friendCode")]
    public string FriendCode { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class FriendRequestRespondRequest
{
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("clientSecret")]
    public string ClientSecret { get; set; } = string.Empty;

    [JsonPropertyName("friendCode")]
    public string FriendCode { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("requestId")]
    public int RequestId { get; set; }

    [JsonPropertyName("accept")]
    public bool Accept { get; set; }
}

public sealed class FriendRequestsResponse
{
    [JsonPropertyName("requests")]
    public List<FriendRequestEntry> Requests { get; set; } = [];
}

public sealed class FriendRequestEntry
{
    [JsonPropertyName("requestId")]
    public int RequestId { get; set; }

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("friendCode")]
    public string FriendCode { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("createdAtIso")]
    public string CreatedAtIso { get; set; } = string.Empty;

    [JsonPropertyName("updatedAtIso")]
    public string UpdatedAtIso { get; set; } = string.Empty;
}

public sealed class DirectMessageSendRequest
{
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("clientSecret")]
    public string ClientSecret { get; set; } = string.Empty;

    [JsonPropertyName("friendCode")]
    public string FriendCode { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("targetFriendCode")]
    public string TargetFriendCode { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public sealed class DirectMessagesPollRequest
{
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("clientSecret")]
    public string ClientSecret { get; set; } = string.Empty;

    [JsonPropertyName("friendCode")]
    public string FriendCode { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("afterId")]
    public long AfterId { get; set; }
}

public sealed class DirectMessagesPollResponse
{
    [JsonPropertyName("messages")]
    public List<FriendDirectMessageEntry> Messages { get; set; } = [];
}

public sealed class FriendDirectMessageEntry
{
    [JsonPropertyName("messageId")]
    public long MessageId { get; set; }

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = string.Empty;

    [JsonPropertyName("friendCode")]
    public string FriendCode { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("createdAtIso")]
    public string CreatedAtIso { get; set; } = string.Empty;
}
