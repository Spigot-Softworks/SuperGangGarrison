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

    private static readonly HttpClient RunUploadHttpClient = new() { Timeout = TimeSpan.FromMinutes(2) };

    private readonly Uri _baseUri;

    public OpenGarrisonPresenceClient(string? baseUrl = null)
    {
        _baseUri = new Uri(string.IsNullOrWhiteSpace(baseUrl) ? DefaultApiBaseUrl : baseUrl.Trim(), UriKind.Absolute);
    }

    public async Task<RunUploadStatus> ClaimVerifiedRunAsync(string token, string attemptId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri("/api/last-to-die/recordings/claims/" + Uri.EscapeDataString(attemptId)));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var response = await (GetHttpClient() ?? throw new HttpRequestException("HTTP unavailable")).SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(RunUploadJsonContext.Default.RunUploadStatus) ?? throw new HttpRequestException("Empty run response");
    }

    public async Task<RunUploadStatus> UploadRunAsync(string token, string ruleset, byte[] recording)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri("/api/last-to-die/recordings"));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-Run-Ruleset", ruleset);
        request.Content = new ByteArrayContent(recording);
        request.Content.Headers.ContentType = new("application/gzip");
        using var response = await (OperatingSystem.IsBrowser() ? GetHttpClient() ?? throw new HttpRequestException("HTTP unavailable") : RunUploadHttpClient).SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(RunUploadJsonContext.Default.RunUploadStatus) ?? throw new HttpRequestException("Empty run response");
    }

    public async Task<RunUploadStatus> GetRunUploadStatusAsync(string token, string id)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri("/api/last-to-die/recordings/" + Uri.EscapeDataString(id)));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var response = await (GetHttpClient() ?? throw new HttpRequestException("HTTP unavailable")).SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(RunUploadJsonContext.Default.RunUploadStatus) ?? throw new HttpRequestException("Empty run response");
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

    public async Task<RelaySessionCreateResponse> CreateRelaySessionAsync(ClientIdentityDocument identity)
    {
        var httpClient = GetHttpClient() ?? throw new InvalidOperationException("HTTP client is unavailable.");
        var request = new RelaySessionCreateRequest
        {
            ClientId = identity.ClientId,
            ClientSecret = identity.ClientSecret,
            FriendCode = identity.FriendCode,
            DisplayName = identity.DisplayName,
        };
        using var response = await httpClient.PostAsJsonAsync(BuildUri("/api/relay/session"), request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RelaySessionCreateResponse>().ConfigureAwait(false)
            ?? throw new InvalidOperationException("Relay session response was empty.");
    }

    public async Task<AccountProfileResponse> GetAccountProfileAsync(ClientIdentityDocument identity)
    {
        return await PostAccountRequestAsync(
            "/api/account/profile",
            AccountAuthenticatedRequest.FromIdentity(identity),
            "Account profile response was empty.").ConfigureAwait(false);
    }

    public async Task<AccountProfileResponse> ProtectAccountAsync(ClientIdentityDocument identity)
    {
        return await PostAccountRequestAsync(
            "/api/account/protect",
            AccountAuthenticatedRequest.FromIdentity(identity),
            "Account protection response was empty.").ConfigureAwait(false);
    }

    public async Task<AccountProfileResponse> ShortenFriendCodeAsync(ClientIdentityDocument identity)
    {
        return await PostAccountRequestAsync(
            "/api/account/friend-code/shorten",
            AccountAuthenticatedRequest.FromIdentity(identity),
            "Friend-code response was empty.").ConfigureAwait(false);
    }

    public async Task<AccountProfileResponse> LoginAccountAsync(
        ClientIdentityDocument identity,
        string friendCode,
        string recoveryKey)
    {
        return await PostAccountRequestAsync(
            "/api/account/login",
            new AccountLoginRequest
            {
                ClientId = identity.ClientId,
                ClientSecret = identity.ClientSecret,
                FriendCode = friendCode,
                RecoveryKey = recoveryKey,
            },
            "Account login response was empty.").ConfigureAwait(false);
    }

    public async Task<GameplaySessionCreateResponse> CreateGameplaySessionAsync(ClientIdentityDocument identity)
    {
        var httpClient = GetHttpClient() ?? throw new InvalidOperationException("HTTP client is unavailable.");
        using var response = await httpClient.PostAsJsonAsync(
            BuildUri("/api/game-session/create"),
            AccountAuthenticatedRequest.FromIdentity(identity)).ConfigureAwait(false);
        await EnsureAccountSuccessAsync(response).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<GameplaySessionCreateResponse>().ConfigureAwait(false)
            ?? throw new InvalidOperationException("Gameplay-session response was empty.");
    }

    public async Task<LastToDieRankingsResponse> GetLastToDieRankingsAsync(
        ClientIdentityDocument identity,
        int limit = 3)
    {
        var httpClient = GetHttpClient() ?? throw new InvalidOperationException("HTTP client is unavailable.");
        var request = new LastToDieRankingsRequest
        {
            ClientId = identity.ClientId,
            ClientSecret = identity.ClientSecret,
            FriendCode = identity.FriendCode,
            Limit = Math.Clamp(limit, 1, 10),
        };
        using var response = await httpClient.PostAsJsonAsync(
            BuildUri("/api/last-to-die/rankings"),
            request).ConfigureAwait(false);
        await EnsureAccountSuccessAsync(response).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<LastToDieRankingsResponse>().ConfigureAwait(false)
            ?? throw new InvalidOperationException("Last to Die rankings response was empty.");
    }

    public async Task<LastToDieLeaderboardResponse> GetLastToDieLeaderboardAsync(
        string sort,
        string survivorId = "",
        int limit = 50,
        int offset = 0)
    {
        var httpClient = GetHttpClient() ?? throw new InvalidOperationException("HTTP client is unavailable.");
        var normalizedSort = string.Equals(sort, "round", StringComparison.OrdinalIgnoreCase)
            ? "round"
            : "score";
        var path = $"/api/last-to-die/leaderboard?sort={normalizedSort}" +
            $"&limit={Math.Clamp(limit, 1, 50)}&offset={Math.Max(0, offset)}";
        if (!string.IsNullOrWhiteSpace(survivorId))
        {
            path += $"&survivor={Uri.EscapeDataString(survivorId.Trim().ToLowerInvariant())}";
        }
        using var response = await httpClient.GetAsync(BuildUri(path)).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LastToDieLeaderboardResponse>().ConfigureAwait(false)
            ?? throw new InvalidOperationException("Last to Die leaderboard response was empty.");
    }

    private async Task<AccountProfileResponse> PostAccountRequestAsync<TRequest>(
        string relativePath,
        TRequest request,
        string emptyResponseMessage)
    {
        var httpClient = GetHttpClient() ?? throw new InvalidOperationException("HTTP client is unavailable.");
        using var response = await httpClient.PostAsJsonAsync(BuildUri(relativePath), request).ConfigureAwait(false);
        await EnsureAccountSuccessAsync(response).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<AccountProfileResponse>().ConfigureAwait(false)
            ?? throw new InvalidOperationException(emptyResponseMessage);
    }

    private static async Task EnsureAccountSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>().ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(error?.Detail))
            {
                throw new InvalidOperationException(error.Detail.Trim());
            }
        }
        catch (System.Text.Json.JsonException)
        {
        }

        throw new InvalidOperationException($"Account service returned HTTP {(int)response.StatusCode}.");
    }

    public async Task<RelayRoomResolveResponse?> ResolveRelayRoomAsync(string roomCode)
    {
        if (!RelayRoomCode.TryNormalize(roomCode, out var normalizedRoomCode))
        {
            return null;
        }

        return await ResolveRelayRoomEndpointAsync(
            $"/api/relay/room/{Uri.EscapeDataString(normalizedRoomCode)}").ConfigureAwait(false);
    }

    public async Task<RelayRoomResolveResponse?> ResolveRelayRoomByFriendCodeAsync(string friendCode)
    {
        if (!ClientIdentityDocument.TryNormalizeFriendCode(friendCode, out var normalizedFriendCode))
        {
            return null;
        }

        return await ResolveRelayRoomEndpointAsync(
            $"/api/relay/friend/{Uri.EscapeDataString(normalizedFriendCode)}").ConfigureAwait(false);
    }

    private async Task<RelayRoomResolveResponse?> ResolveRelayRoomEndpointAsync(string relativePath)
    {
        var httpClient = GetHttpClient() ?? throw new InvalidOperationException("HTTP client is unavailable.");
        const int maximumAttempts = 9;
        for (var attempt = 0; attempt < maximumAttempts; attempt += 1)
        {
            using var response = await httpClient.GetAsync(BuildUri(relativePath)).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>().ConfigureAwait(false);
                if (string.Equals(error?.Detail, "Not Found", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("The online co-op relay service is unavailable.");
                }

                return null;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                if (attempt + 1 < maximumAttempts)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(750)).ConfigureAwait(false);
                    continue;
                }

                throw new InvalidOperationException("The relay room did not finish starting.");
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<RelayRoomResolveResponse>().ConfigureAwait(false)
                ?? throw new InvalidOperationException("Relay room response was empty.");
        }
        return null;
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

public sealed class RelaySessionCreateRequest
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

public sealed class RelaySessionCreateResponse
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("roomCode")]
    public string RoomCode { get; set; } = string.Empty;

    [JsonPropertyName("hostWebSocketUrl")]
    public string HostWebSocketUrl { get; set; } = string.Empty;

    [JsonPropertyName("guestWebSocketUrl")]
    public string GuestWebSocketUrl { get; set; } = string.Empty;

    [JsonPropertyName("expiresAtIso")]
    public string ExpiresAtIso { get; set; } = string.Empty;
}

public sealed class RelayRoomResolveResponse
{
    [JsonPropertyName("roomCode")]
    public string RoomCode { get; set; } = string.Empty;

    [JsonPropertyName("friendCode")]
    public string FriendCode { get; set; } = string.Empty;

    [JsonPropertyName("guestWebSocketUrl")]
    public string GuestWebSocketUrl { get; set; } = string.Empty;

    [JsonPropertyName("expiresAtIso")]
    public string ExpiresAtIso { get; set; } = string.Empty;
}

public sealed class AccountAuthenticatedRequest
{
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("clientSecret")]
    public string ClientSecret { get; set; } = string.Empty;

    [JsonPropertyName("friendCode")]
    public string FriendCode { get; set; } = string.Empty;

    public static AccountAuthenticatedRequest FromIdentity(ClientIdentityDocument identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return new AccountAuthenticatedRequest
        {
            ClientId = identity.ClientId,
            ClientSecret = identity.ClientSecret,
            FriendCode = identity.FriendCode,
        };
    }
}

public sealed class AccountLoginRequest
{
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("clientSecret")]
    public string ClientSecret { get; set; } = string.Empty;

    [JsonPropertyName("friendCode")]
    public string FriendCode { get; set; } = string.Empty;

    [JsonPropertyName("recoveryKey")]
    public string RecoveryKey { get; set; } = string.Empty;
}

public sealed class AccountProfileResponse
{
    [JsonPropertyName("accountId")]
    public string AccountId { get; set; } = string.Empty;

    [JsonPropertyName("friendCode")]
    public string FriendCode { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("playerCard")]
    public string PlayerCardJson { get; set; } = string.Empty;

    [JsonPropertyName("lifetimePoints")]
    public long LifetimePoints { get; set; }

    [JsonPropertyName("walletBalance")]
    public long WalletBalance { get; set; }

    [JsonPropertyName("profileRevision")]
    public long ProfileRevision { get; set; }

    [JsonPropertyName("isProtected")]
    public bool IsProtected { get; set; }

    [JsonPropertyName("recoveryKey")]
    public string RecoveryKey { get; set; } = string.Empty;

    [JsonPropertyName("createdAtIso")]
    public string CreatedAtIso { get; set; } = string.Empty;

    [JsonPropertyName("updatedAtIso")]
    public string UpdatedAtIso { get; set; } = string.Empty;
}

public sealed class GameplaySessionCreateResponse
{
    [JsonPropertyName("gameplayToken")]
    public string GameplayToken { get; set; } = string.Empty;

    [JsonPropertyName("expiresAtIso")]
    public string ExpiresAtIso { get; set; } = string.Empty;

    [JsonPropertyName("profile")]
    public AccountProfileResponse Profile { get; set; } = new();
}

public sealed class LastToDieRankingsRequest
{
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("clientSecret")]
    public string ClientSecret { get; set; } = string.Empty;

    [JsonPropertyName("friendCode")]
    public string FriendCode { get; set; } = string.Empty;

    [JsonPropertyName("limit")]
    public int Limit { get; set; } = 3;
}

public sealed class LastToDiePlayerRanking
{
    [JsonPropertyName("friendCode")]
    public string FriendCode { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("runsPlayed")]
    public int RunsPlayed { get; set; }

    [JsonPropertyName("bestScoreUnits")]
    public int BestScoreUnits { get; set; }

    [JsonPropertyName("highestRound")]
    public int HighestRound { get; set; }

    [JsonPropertyName("scoreRank")]
    public int ScoreRank { get; set; }

    [JsonPropertyName("roundRank")]
    public int RoundRank { get; set; }
}

public sealed class LastToDieLeaderboardEntry
{
    [JsonPropertyName("rank")]
    public int Rank { get; set; }

    [JsonPropertyName("friendCode")]
    public string FriendCode { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("survivorId")]
    public string SurvivorId { get; set; } = string.Empty;

    [JsonPropertyName("runsPlayed")]
    public int RunsPlayed { get; set; }

    [JsonPropertyName("bestScoreUnits")]
    public int BestScoreUnits { get; set; }

    [JsonPropertyName("highestRound")]
    public int HighestRound { get; set; }
}

public sealed class LastToDieRankingsResponse
{
    [JsonPropertyName("player")]
    public LastToDiePlayerRanking Player { get; set; } = new();

    [JsonPropertyName("scoreRecords")]
    public List<LastToDieLeaderboardEntry> ScoreRecords { get; set; } = [];

    [JsonPropertyName("roundRecords")]
    public List<LastToDieLeaderboardEntry> RoundRecords { get; set; } = [];
}

public sealed class LastToDieLeaderboardResponse
{
    [JsonPropertyName("sort")]
    public string Sort { get; set; } = "score";

    [JsonPropertyName("survivorId")]
    public string SurvivorId { get; set; } = string.Empty;

    [JsonPropertyName("entries")]
    public List<LastToDieLeaderboardEntry> Entries { get; set; } = [];

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

internal sealed class ApiErrorResponse
{
    [JsonPropertyName("detail")]
    public string Detail { get; set; } = string.Empty;
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
