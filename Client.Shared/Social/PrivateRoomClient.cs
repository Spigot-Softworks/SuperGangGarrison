using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenGarrison.Core;

namespace OpenGarrison.ClientShared;

public sealed record PrivateRoomRequest(
    string ClientId, string ClientSecret, string FriendCode, string DisplayName,
    string RequestId = "", string RoomId = "", string Code = "", int MaximumPlayers = 2,
    string Difficulty = "standard", int ProtocolVersion = 0, string BuildVersion = "", string ContentId = "");

public sealed record PrivateRoomResponse(
    string RoomId, string RoomCode, string Kind, string Status, int ProtocolVersion,
    string BuildVersion, string ContentId, int MaximumPlayers, bool IsOwner,
    string Endpoint = "", string ExpiresAtIso = "", string Message = "");

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PrivateRoomRequest))]
[JsonSerializable(typeof(PrivateRoomResponse))]
[JsonSerializable(typeof(PrivateRoomErrorResponse))]
internal partial class PrivateRoomJsonContext : JsonSerializerContext;

internal sealed record PrivateRoomErrorResponse(JsonElement Detail);

public sealed class PrivateRoomException(string message, System.Net.HttpStatusCode status, string code, string roomId)
    : HttpRequestException(message, null, status)
{
    public string Code { get; } = code;
    public string RoomId { get; } = roomId;
}

/// <summary>Private-room control plane; it never accepts a user-supplied endpoint.</summary>
public sealed class PrivateRoomClient(HttpClient httpClient, Uri serviceOrigin)
{
    public const string DefaultServiceOrigin = OpenGarrisonPresenceClient.DefaultApiBaseUrl;

    public async Task<PrivateRoomResponse> CreateAndJoinAsync(PrivateRoomRequest request, CancellationToken cancellationToken,
        IProgress<string>? progress = null)
    {
        PrivateRoomResponse room;
        // A retry uses the original idempotency key. Only the owner's closing
        // room is automatically retried; global capacity never causes a loop.
        using (var cleanupTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            var waitingForPreviousRoom = false;
            cleanupTimeout.CancelAfter(TimeSpan.FromSeconds(30));
            try
            {
                while (true)
                {
                    try { room = await SendAsync("create", request, cleanupTimeout.Token).ConfigureAwait(false); break; }
                    catch (PrivateRoomException error) when (error.Code == "owned_room_closing"
                        && Guid.TryParseExact(error.RoomId, "N", out _))
                    {
                        waitingForPreviousRoom = true;
                        progress?.Report("Closing your previous run...");
                        await WaitForClosureAsync(request with { RoomId = error.RoomId }, cleanupTimeout.Token).ConfigureAwait(false);
                        waitingForPreviousRoom = false;
                        progress?.Report(request.MaximumPlayers == 1 ? "Starting Last to Die solo..." : "Creating Last to Die room...");
                    }
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(waitingForPreviousRoom
                    ? "Your previous run is still closing. Try again shortly."
                    : "The room service took too long to respond. Try again.");
            }
        }
        progress?.Report(request.MaximumPlayers == 1 ? "Starting Last to Die solo..." : "Creating Last to Die room...");
        for (var attempt = 0; attempt < 150; attempt++)
        {
            if (room.Status == "ready")
                return await SendAsync("join", request with { RoomId = room.RoomId }, cancellationToken).ConfigureAwait(false);
            if (room.Status is "failed" or "closed" or "cancelled")
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(room.Message) ? "The room could not start." : room.Message);
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            room = await SendAsync("status", request with { RoomId = room.RoomId }, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("The Last to Die server took too long to start.");
    }

    public Task<PrivateRoomResponse> JoinAsync(PrivateRoomRequest request, CancellationToken cancellationToken)
        => SendAsync("join", request, cancellationToken);

    public Task LeaveAsync(PrivateRoomRequest request) => CleanupAsync("leave", request);

    public Task CancelAsync(PrivateRoomRequest request) => CleanupAsync("cancel", request);

    private async Task CleanupAsync(string operation, PrivateRoomRequest request)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var room = await SendCleanupRequestAsync(operation, request, timeout.Token).ConfigureAwait(false);
        if (room is { IsOwner: true } && room.Status is not ("closed" or "cancelled" or "failed"))
            await WaitForClosureAsync(request with { RoomId = room.RoomId }, timeout.Token).ConfigureAwait(false);
    }

    private async Task<PrivateRoomResponse?> SendCleanupRequestAsync(string operation, PrivateRoomRequest request, CancellationToken token)
    {
        var retryDelay = TimeSpan.FromMilliseconds(500);
        while (true)
        {
            token.ThrowIfCancellationRequested();
            try { return await SendAsync(operation, request, token).ConfigureAwait(false); }
            catch (HttpRequestException error) when (error.StatusCode == System.Net.HttpStatusCode.NotFound) { return null; }
            catch (HttpRequestException error) when ((int?)error.StatusCode is null or 408 or 500 or 502 or 503 or 504)
            {
                // Leave/cancel/status are idempotent. Keep the same room and
                // request identity when a connection drops, within the caller's
                // existing cleanup deadline; never retry an authorization error.
                await Task.Delay(retryDelay, token).ConfigureAwait(false);
                retryDelay = TimeSpan.FromMilliseconds(Math.Min(2000, retryDelay.TotalMilliseconds * 2));
            }
        }
    }

    private async Task WaitForClosureAsync(PrivateRoomRequest request, CancellationToken token)
    {
        while (true)
        {
            var room = await SendCleanupRequestAsync("status", request, token).ConfigureAwait(false);
            if (room is null || room.Status is "closed" or "cancelled" or "failed") return;
            await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
        }
    }

    public bool TryValidateGrant(PrivateRoomResponse room, int protocolVersion, string contentId, out Uri? endpoint)
    {
        endpoint = null;
        if (!Guid.TryParseExact(room.RoomId, "N", out _) || room.Kind != "LastToDie" || room.Status != "ready" || room.ProtocolVersion != protocolVersion
            || room.ContentId != contentId || room.MaximumPlayers is < 1 or > 2
            || !DateTimeOffset.TryParse(room.ExpiresAtIso, out var expires) || expires <= DateTimeOffset.UtcNow
            || !Uri.TryCreate(room.Endpoint, UriKind.Absolute, out var candidate)) return false;
        var expectedScheme = serviceOrigin.Scheme == "https" ? "wss64" : "ws64";
        if (candidate.Scheme != expectedScheme || candidate.Host != serviceOrigin.Host
            || (candidate.Port < 0 ? (expectedScheme == "wss64" ? 443 : 80) : candidate.Port) != serviceOrigin.Port
            || (candidate.AbsolutePath != "/api/private-rooms/ws/" + room.RoomId + "/1"
                && candidate.AbsolutePath != "/api/private-rooms/ws/" + room.RoomId + "/2")
            || !candidate.Query.StartsWith("?token=", StringComparison.Ordinal)
            || candidate.UserInfo.Length != 0 || candidate.Fragment.Length != 0) return false;
        endpoint = candidate;
        return true;
    }

    private async Task<PrivateRoomResponse> SendAsync(string operation, PrivateRoomRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(new Uri(serviceOrigin, "/api/private-rooms/" + operation),
            request, PrivateRoomJsonContext.Default.PrivateRoomRequest, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var code = string.Empty;
            var roomId = string.Empty;
            try
            {
                var error = await response.Content.ReadFromJsonAsync(PrivateRoomJsonContext.Default.PrivateRoomErrorResponse, cancellationToken).ConfigureAwait(false);
                if (error?.Detail.ValueKind == JsonValueKind.Object)
                {
                    if (error.Detail.TryGetProperty("code", out var value) && value.ValueKind == JsonValueKind.String) code = value.GetString() ?? "";
                    if (error.Detail.TryGetProperty("roomId", out value) && value.ValueKind == JsonValueKind.String) roomId = value.GetString() ?? "";
                }
            }
            catch (JsonException) { } // Older services and proxies can return plain text/HTML.
            var reason = (int)response.StatusCode switch
            {
                404 => "Room not found or expired.",
                409 => "Room is full, closed, or still starting.",
                426 => "This room needs a different game build. Refresh the game and try again.",
                429 => "Too many room requests. Try again shortly.",
                503 => "Last to Die hosting is currently unavailable or full.",
                _ => "The room service could not complete the request.",
            };
            reason = code switch
            {
                "capacity_full" => "All Last to Die rooms are busy. Try again shortly.",
                "owned_room_closing" => "Your previous run is still closing. Try again shortly.",
                "owned_room_active" => "You already have an active room. Leave it before starting another.",
                "hosting_unavailable" => "Last to Die hosting is temporarily unavailable. Try again later.",
                _ => reason,
            };
            throw new PrivateRoomException(reason, response.StatusCode, code, roomId);
        }
        return await response.Content.ReadFromJsonAsync(PrivateRoomJsonContext.Default.PrivateRoomResponse, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The room service returned an empty response.");
    }
}
