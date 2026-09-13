using System.Net;
using System.Text.Json;
using OpenGarrison.ClientShared;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PrivateRoomClientTests
{
    private const string RoomId = "11111111111111111111111111111111";
    private static PrivateRoomResponse Grant(string endpoint) => new(RoomId, "ABCD", "LastToDie", "ready", 94,
        "test", "content", 2, true, endpoint, DateTimeOffset.UtcNow.AddMinutes(1).ToString("O"));

    [Theory]
    [InlineData("wss64://api.example.test/api/private-rooms/ws/11111111111111111111111111111111/1?token=abc", true)]
    [InlineData("wss64://api.example.test/api/private-rooms/ws/11111111111111111111111111111111/2?token=abc", true)]
    [InlineData("ws64://api.example.test/api/private-rooms/ws/11111111111111111111111111111111/1?token=abc", false)]
    [InlineData("wss64://other.example.test/api/private-rooms/ws/11111111111111111111111111111111/1?token=abc", false)]
    [InlineData("wss64://api.example.test:8190/api/private-rooms/ws/11111111111111111111111111111111/1?token=abc", false)]
    [InlineData("wss64://api.example.test/opengarrison/ws64", false)]
    [InlineData("wss64://api.example.test/api/private-rooms/ws/11111111111111111111111111111111/3?token=abc", false)]
    [InlineData("wss64://api.example.test/api/private-rooms/ws/11111111111111111111111111111111/1", false)]
    public void GrantsRequireExactServiceAndTypedPath(string endpoint, bool accepted)
    {
        using var http = new HttpClient();
        var client = new PrivateRoomClient(http, new("https://api.example.test"));
        Assert.Equal(accepted, client.TryValidateGrant(Grant(endpoint), 94, "content", out _));
    }

    [Fact]
    public void GrantRejectsExpiredOrIncompatibleRoom()
    {
        using var http = new HttpClient();
        var client = new PrivateRoomClient(http, new("https://api.example.test"));
        var grant = Grant($"wss64://api.example.test/api/private-rooms/ws/{RoomId}/1?token=abc");
        Assert.False(client.TryValidateGrant(grant with { ExpiresAtIso = DateTimeOffset.UtcNow.AddSeconds(-1).ToString("O") }, 94, "content", out _));
        Assert.False(client.TryValidateGrant(grant with { Kind = "Practice" }, 94, "content", out _));
        Assert.False(client.TryValidateGrant(grant, 93, "content", out _));
        Assert.False(client.TryValidateGrant(grant, 94, "different", out _));
        Assert.False(client.TryValidateGrant(grant with { Status = "starting" }, 94, "content", out _));
    }

    [Fact]
    public async Task ServiceErrorsDoNotExposeInternalResponseOrCredentials()
    {
        using var http = new HttpClient(new ErrorHandler());
        var client = new PrivateRoomClient(http, new("https://api.example.test"));
        var error = await Assert.ThrowsAsync<PrivateRoomException>(() => client.JoinAsync(new("id", "secret", "friend", "name"), default));
        Assert.Contains("full", error.Message);
        Assert.DoesNotContain("private", error.Message);
    }

    [Theory]
    [InlineData("capacity_full", "busy")]
    [InlineData("owned_room_active", "active room")]
    [InlineData("hosting_unavailable", "temporarily unavailable")]
    public async Task NonCleanupFailuresAreActionableAndNeverRetried(string code, string message)
    {
        var handler = new SequenceHandler((HttpStatusCode.ServiceUnavailable, ErrorJson(code)));
        using var http = new HttpClient(handler);
        var client = new PrivateRoomClient(http, new("https://api.example.test"));
        var error = await Assert.ThrowsAsync<PrivateRoomException>(() => client.CreateAndJoinAsync(Request(), default));
        Assert.Equal(code, error.Code);
        Assert.Contains(message, error.Message);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ClosingOwnerRoomIsPolledThenCreateResumesWithSameIdempotencyKey()
    {
        var handler = new SequenceHandler(
            (HttpStatusCode.ServiceUnavailable, ErrorJson("owned_room_closing", RoomId)),
            (HttpStatusCode.OK, RoomJson("closed")),
            (HttpStatusCode.OK, RoomJson("ready")),
            (HttpStatusCode.OK, RoomJson("ready")));
        using var http = new HttpClient(handler);
        var client = new PrivateRoomClient(http, new("https://api.example.test"));
        var result = await client.CreateAndJoinAsync(Request(), default);
        Assert.Equal("ready", result.Status);
        Assert.Equal(new[] { "create", "status", "create", "join" }, handler.Requests.Select(r => r.Path));
        Assert.All(handler.Requests, r => Assert.Equal("same-request", r.RequestId));
    }

    [Fact]
    public async Task CancelDuringCleanupNeverStartsAnotherAllocation()
    {
        using var cancel = new CancellationTokenSource();
        var handler = new SequenceHandler(
            (HttpStatusCode.ServiceUnavailable, ErrorJson("owned_room_closing", RoomId)),
            (HttpStatusCode.OK, RoomJson("closing")))
        {
            // Cancel when the cleanup request actually starts. A wall-clock
            // timer could fire during the initial create on a busy build host.
            RequestObserved = path => { if (path == "status") cancel.Cancel(); },
        };
        using var http = new HttpClient(handler);
        var client = new PrivateRoomClient(http, new("https://api.example.test"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.CreateAndJoinAsync(Request(), cancel.Token));
        Assert.Equal(new[] { "create", "status" }, handler.Requests.Select(r => r.Path));
    }

    [Fact]
    public async Task OwnerLeaveWaitsForWorkerEvenWithOldReadyLeaveResponse()
    {
        var handler = new SequenceHandler((HttpStatusCode.OK, RoomJson("ready")),
            (HttpStatusCode.OK, RoomJson("closing")), (HttpStatusCode.OK, RoomJson("closed")));
        using var http = new HttpClient(handler);
        await new PrivateRoomClient(http, new("https://api.example.test")).LeaveAsync(Request() with { RoomId = RoomId });
        Assert.Equal(new[] { "leave", "status", "status" }, handler.Requests.Select(r => r.Path));
    }

    [Fact]
    public async Task GuestLeaveDoesNotPollOwnerOnlyEndpoint()
    {
        var handler = new SequenceHandler((HttpStatusCode.OK, RoomJson("ready", false)));
        using var http = new HttpClient(handler);
        await new PrivateRoomClient(http, new("https://api.example.test")).LeaveAsync(Request() with { RoomId = RoomId });
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 503)]
    [InlineData(true, 503)]
    public async Task CleanupRetriesLostRequestsAndPollingWithTheSameIdentity(bool cancel, int status)
    {
        var handler = new SequenceHandler(((HttpStatusCode)status, "{}"),
            (HttpStatusCode.OK, RoomJson("closing")), (HttpStatusCode.BadGateway, "{}"),
            (HttpStatusCode.OK, RoomJson("closed")));
        using var http = new HttpClient(handler);
        var client = new PrivateRoomClient(http, new("https://api.example.test"));
        var request = Request() with { RoomId = RoomId };
        await (cancel ? client.CancelAsync(request) : client.LeaveAsync(request));
        var operation = cancel ? "cancel" : "leave";
        Assert.Equal(new[] { operation, operation, "status", "status" }, handler.Requests.Select(r => r.Path));
        Assert.All(handler.Requests, r => { Assert.Equal(RoomId, r.RoomId); Assert.Equal("same-request", r.RequestId); });
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task CleanupDoesNotRetryAuthorizationFailures(HttpStatusCode status)
    {
        var handler = new SequenceHandler((status, "{}"));
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<PrivateRoomException>(() => new PrivateRoomClient(http, new("https://api.example.test")).LeaveAsync(Request()));
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CleanupOfAnAlreadyRemovedRoomCompletes(bool cancel)
    {
        var handler = new SequenceHandler((HttpStatusCode.NotFound, "{}"));
        using var http = new HttpClient(handler);
        var client = new PrivateRoomClient(http, new("https://api.example.test"));
        await (cancel ? client.CancelAsync(Request()) : client.LeaveAsync(Request()));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task CancellationDuringTransientCleanupFailureStopsRetries()
    {
        using var cancel = new CancellationTokenSource();
        var handler = new SequenceHandler((HttpStatusCode.ServiceUnavailable, ErrorJson("owned_room_closing", RoomId)),
            (0, "{}")) { RequestObserved = path => { if (path == "status") cancel.Cancel(); } };
        using var http = new HttpClient(handler);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PrivateRoomClient(http, new("https://api.example.test"))
            .CreateAndJoinAsync(Request(), cancel.Token));
        Assert.Equal(new[] { "create", "status" }, handler.Requests.Select(r => r.Path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ServiceTimeoutDoesNotClaimAnUnrelatedRoomIsClosing(bool afterCleanup)
    {
        using var http = new HttpClient(new TimeoutHandler(afterCleanup));
        var client = new PrivateRoomClient(http, new("https://api.example.test"));
        var error = await Assert.ThrowsAsync<TimeoutException>(() => client.CreateAndJoinAsync(Request(), default));
        Assert.Contains("room service", error.Message);
        Assert.DoesNotContain("previous run", error.Message);
    }

    private sealed class TimeoutHandler(bool afterCleanup) : HttpMessageHandler
    {
        private int _requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            _requests++;
            if (afterCleanup && _requests <= 2)
                return Task.FromResult(new HttpResponseMessage(_requests == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
                { Content = new StringContent(_requests == 1 ? ErrorJson("owned_room_closing", RoomId) : RoomJson("closed")) });
            return Task.FromException<HttpResponseMessage>(new TaskCanceledException("HTTP timeout"));
        }
    }

    private static PrivateRoomRequest Request() => new("id", "secret", "friend", "name", RequestId: "same-request");
    private static string ErrorJson(string code, string roomId = "") => JsonSerializer.Serialize(new { detail = new { code, roomId } });
    private static string RoomJson(string status, bool owner = true) => JsonSerializer.Serialize(
        Grant($"wss64://api.example.test/api/private-rooms/ws/{RoomId}/1?token=abc") with { Status = status, IsOwner = owner },
        JsonSerializerOptions.Web);

    private sealed class SequenceHandler(params (HttpStatusCode Status, string Json)[] responses) : HttpMessageHandler
    {
        public List<(string Path, string RequestId, string RoomId)> Requests { get; } = [];
        public Action<string>? RequestObserved { get; init; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            var response = responses[Requests.Count];
            Requests.Add((request.RequestUri!.Segments[^1], body.RootElement.GetProperty("requestId").GetString()!,
                body.RootElement.GetProperty("roomId").GetString()!));
            RequestObserved?.Invoke(Requests[^1].Path);
            if ((int)response.Status == 0) throw new HttpRequestException("Simulated connection reset.");
            return new HttpResponseMessage(response.Status) { Content = new StringContent(response.Json) };
        }
    }

    private sealed class ErrorHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict) { Content = new StringContent("private backend details") });
    }
}
