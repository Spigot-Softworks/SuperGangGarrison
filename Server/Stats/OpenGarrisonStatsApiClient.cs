#nullable enable

using System.Net.Http.Json;
using System.Text.Json.Serialization;
using OpenGarrison.Core;

namespace OpenGarrison.Server;

internal interface IOpenGarrisonStatsApiClient
{
    Task<GameplaySessionValidationResponse> ValidateGameplaySessionAsync(string token, CancellationToken cancellationToken);

    Task<StatAwardResponse> AwardAsync(StatAwardRequest request, CancellationToken cancellationToken);

    Task<PlayerPointsResponse> GetPlayerPointsAsync(string token, CancellationToken cancellationToken);

    Task<PointsLeaderboardResponse> GetLeaderboardAsync(int limit, int offset, CancellationToken cancellationToken);

    Task<LastToDieRunResponse> RecordLastToDieRunAsync(
        LastToDieRunRequest request,
        CancellationToken cancellationToken);
}

internal sealed class OpenGarrisonStatsApiClient : IOpenGarrisonStatsApiClient
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(5),
    };

    private readonly Uri _baseUri;

    public OpenGarrisonStatsApiClient(string? baseUrl = null)
    {
        var configured = string.IsNullOrWhiteSpace(baseUrl)
            ? OpenGarrisonPreferencesDocument.DefaultApiBaseUrl
            : baseUrl.Trim();
        _baseUri = new Uri(configured.EndsWith('/') ? configured : configured + '/', UriKind.Absolute);
    }

    public Task<GameplaySessionValidationResponse> ValidateGameplaySessionAsync(
        string token,
        CancellationToken cancellationToken)
        => PostAsync<GameplaySessionValidationResponse>(
            "api/game-session/validate",
            new GameplayTokenRequest { GameplayToken = token },
            cancellationToken);

    public Task<StatAwardResponse> AwardAsync(StatAwardRequest request, CancellationToken cancellationToken)
        => PostAsync<StatAwardResponse>("api/stats/award", request, cancellationToken);

    public Task<PlayerPointsResponse> GetPlayerPointsAsync(string token, CancellationToken cancellationToken)
        => PostAsync<PlayerPointsResponse>(
            "api/stats/points",
            new GameplayTokenRequest { GameplayToken = token },
            cancellationToken);

    public async Task<PointsLeaderboardResponse> GetLeaderboardAsync(
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        var uri = new Uri(_baseUri, $"api/stats/leaderboard?limit={Math.Clamp(limit, 1, 50)}&offset={Math.Max(0, offset)}");
        using var response = await Http.GetAsync(uri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PointsLeaderboardResponse>(cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Leaderboard response was empty.");
    }

    public Task<LastToDieRunResponse> RecordLastToDieRunAsync(
        LastToDieRunRequest request,
        CancellationToken cancellationToken)
        => PostAsync<LastToDieRunResponse>("api/last-to-die/run", request, cancellationToken);

    private async Task<TResponse> PostAsync<TResponse>(
        string relativePath,
        object request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseUri, relativePath))
        { Content = JsonContent.Create(request) };
        if (relativePath is "api/stats/award" or "api/last-to-die/run")
        {
            var authorityKey = Environment.GetEnvironmentVariable("OPENGARRISON_REWARD_AUTHORITY_KEY");
            if (string.IsNullOrWhiteSpace(authorityKey))
                throw new InvalidOperationException("Persistent awards require a trusted reward authority.");
            message.Headers.Add("X-OpenGarrison-Reward-Key", authorityKey);
        }
        using var response = await Http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"{typeof(TResponse).Name} response was empty.");
    }
}

internal sealed class GameplayTokenRequest
{
    [JsonPropertyName("gameplayToken")]
    public string GameplayToken { get; set; } = string.Empty;
}

internal sealed class StatsAccountProfile
{
    [JsonPropertyName("accountId")]
    public string AccountId { get; set; } = string.Empty;

    [JsonPropertyName("friendCode")]
    public string FriendCode { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("lifetimePoints")]
    public long LifetimePoints { get; set; }

    [JsonPropertyName("walletBalance")]
    public long WalletBalance { get; set; }

    [JsonPropertyName("profileRevision")]
    public long ProfileRevision { get; set; }
}

internal sealed class GameplaySessionValidationResponse
{
    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("expiresAtIso")]
    public string ExpiresAtIso { get; set; } = string.Empty;

    [JsonPropertyName("profile")]
    public StatsAccountProfile Profile { get; set; } = new();
}

internal sealed class StatAwardRequest
{
    [JsonPropertyName("gameplayToken")]
    public string GameplayToken { get; set; } = string.Empty;

    [JsonPropertyName("eventId")]
    public string EventId { get; set; } = string.Empty;

    [JsonPropertyName("matchId")]
    public string MatchId { get; set; } = string.Empty;

    [JsonPropertyName("sourceFrame")]
    public long SourceFrame { get; set; }

    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("rawValue")]
    public int RawValue { get; set; }

    [JsonPropertyName("pointsDelta")]
    public int PointsDelta { get; set; }

    [JsonPropertyName("creditsDelta")]
    public int CreditsDelta { get; set; }

    [JsonPropertyName("policyVersion")]
    public int PolicyVersion { get; set; } = 1;
}

internal sealed class StatAwardResponse
{
    [JsonPropertyName("applied")]
    public bool Applied { get; set; }

    [JsonPropertyName("eventId")]
    public string EventId { get; set; } = string.Empty;

    [JsonPropertyName("profile")]
    public StatsAccountProfile Profile { get; set; } = new();
}

internal sealed class PlayerPointsResponse
{
    [JsonPropertyName("profile")]
    public StatsAccountProfile Profile { get; set; } = new();

    [JsonPropertyName("globalRank")]
    public int GlobalRank { get; set; }

    [JsonPropertyName("stats")]
    public Dictionary<string, int> Stats { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class PointsLeaderboardEntry
{
    [JsonPropertyName("rank")]
    public int Rank { get; set; }

    [JsonPropertyName("friendCode")]
    public string FriendCode { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("lifetimePoints")]
    public long LifetimePoints { get; set; }
}

internal sealed class PointsLeaderboardResponse
{
    [JsonPropertyName("entries")]
    public List<PointsLeaderboardEntry> Entries { get; set; } = [];

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

internal sealed class LastToDieRunRequest
{
    [JsonPropertyName("gameplayToken")]
    public string GameplayToken { get; set; } = string.Empty;

    [JsonPropertyName("submissionId")]
    public string SubmissionId { get; set; } = string.Empty;

    [JsonPropertyName("runId")]
    public string RunId { get; set; } = string.Empty;

    [JsonPropertyName("scoreUnits")]
    public int ScoreUnits { get; set; }

    [JsonPropertyName("roundNumber")]
    public int RoundNumber { get; set; }

    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = "standard";

    [JsonPropertyName("survivorId")]
    public string SurvivorId { get; set; } = string.Empty;

    [JsonPropertyName("policyVersion")]
    public int PolicyVersion { get; set; } = 1;
}

internal sealed class LastToDiePlayerStats
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

internal sealed class LastToDieRunResponse
{
    [JsonPropertyName("applied")]
    public bool Applied { get; set; }

    [JsonPropertyName("submissionId")]
    public string SubmissionId { get; set; } = string.Empty;

    [JsonPropertyName("player")]
    public LastToDiePlayerStats Player { get; set; } = new();
}
