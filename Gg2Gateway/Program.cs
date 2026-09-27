using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("OPENGARRISON_GG2_GATEWAY_URLS")
    ?? "http://127.0.0.1:8768");
var origins = (Environment.GetEnvironmentVariable("OPENGARRISON_GG2_ALLOWED_ORIGINS")
    ?? "https://superganggarrison.com,https://www.superganggarrison.com,https://play.superganggarrison.com,http://localhost:5014,http://127.0.0.1:5014")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(origins).WithMethods("GET").AllowAnyHeader()));
var app = builder.Build();
ContentRoot.Initialize(Path.Combine(AppContext.BaseDirectory, "Content"));
var mapDirectory = Path.GetFullPath(Environment.GetEnvironmentVariable("OPENGARRISON_GG2_MAP_CACHE")
    ?? Path.Combine(AppContext.BaseDirectory, "gg2-maps"));
var publicOrigin = new Uri(Environment.GetEnvironmentVariable("OPENGARRISON_GG2_PUBLIC_ORIGIN")
    ?? "https://api.superganggarrison.com", UriKind.Absolute);
var allowLoopback = string.Equals(Environment.GetEnvironmentVariable("OPENGARRISON_GG2_ALLOW_LOOPBACK"), "true", StringComparison.OrdinalIgnoreCase);
var lobby = new Gg2GatewayLobby();

app.UseCors();
app.UseWebSockets();
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/gg2/servers", async (CancellationToken cancellationToken) =>
{
    try { return Results.Ok(new { servers = await lobby.GetServersAsync(cancellationToken) }); }
    catch (Exception exception) when (exception is IOException or System.Net.Sockets.SocketException or OperationCanceledException)
    { return Results.Problem("GG2 lobby is temporarily unavailable.", statusCode: 503); }
});
app.MapGet("/api/gg2/maps/{hash}.png", (string hash) =>
{
    if (hash.Length != 32 || !hash.All(Uri.IsHexDigit)) return Results.NotFound();
    var path = Path.Combine(mapDirectory, $"gg2_{hash.ToLowerInvariant()}.png");
    if (!File.Exists(path)) return Results.NotFound();
    return Results.File(path, "image/png", enableRangeProcessing: false);
});
app.Map("/api/gg2/ws/{host}/{port:int}", async (HttpContext context, string host, int port) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }
    if (context.Request.Headers.Origin is { Count: > 0 } requestOrigin
        && !origins.Contains(requestOrigin.ToString(), StringComparer.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    if (!IPAddress.TryParse(host, out var address) || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork
        || port is < 1 or > 65535)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }
    var isLoopback = IPAddress.IsLoopback(address);
    if (!(allowLoopback && isLoopback))
    {
        if (isLoopback || IsPrivateAddress(address))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        IReadOnlyList<LegacyGg2LobbyServer> servers;
        try { servers = await lobby.GetServersAsync(context.RequestAborted); }
        catch (Exception exception) when (exception is IOException or System.Net.Sockets.SocketException or OperationCanceledException)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }
        if (!servers.Any(server => server.Host == host && server.Port == port
                && server.IsCompatible && !server.IsPrivate))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
    }

    if (!LegacyGg2NetworkClientTransport.TryConnect(host, port, mapDirectory, out var rawTransport, out _)
        || rawTransport is not LegacyGg2NetworkClientTransport transport)
    {
        context.Response.StatusCode = StatusCodes.Status502BadGateway;
        return;
    }

    using (transport)
    using (var socket = await context.WebSockets.AcceptWebSocketAsync())
    using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted))
    {
        var receiveTask = ReceiveBrowserMessagesAsync(socket, transport, cancellation.Token);
        var sendTask = SendGg2MessagesAsync(socket, transport, publicOrigin, cancellation.Token);
        await Task.WhenAny(receiveTask, sendTask);
        cancellation.Cancel();
        try { await Task.WhenAll(receiveTask, sendTask); }
        catch (Exception exception) when (exception is IOException or OperationCanceledException or WebSocketException)
        { app.Logger.LogDebug(exception, "GG2 gateway session closed"); }
    }
});
app.Run();

static bool IsPrivateAddress(IPAddress address)
{
    var bytes = address.GetAddressBytes();
    return bytes[0] is 0 or 10 or 127
        || bytes[0] == 169 && bytes[1] == 254
        || bytes[0] == 172 && bytes[1] is >= 16 and <= 31
        || bytes[0] == 192 && bytes[1] == 168
        || bytes[0] >= 224;
}

static async Task ReceiveBrowserMessagesAsync(
    WebSocket socket, LegacyGg2NetworkClientTransport transport, CancellationToken cancellationToken)
{
    var buffer = new byte[16 * 1024];
    using var message = new MemoryStream();
    while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
    {
        var part = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken);
        if (part.MessageType == WebSocketMessageType.Close) return;
        if (part.MessageType != WebSocketMessageType.Binary) throw new IOException("Expected a binary game message.");
        if (message.Length + part.Count > 64 * 1024) throw new IOException("Browser game message is too large.");
        message.Write(buffer, 0, part.Count);
        if (!part.EndOfMessage) continue;
        transport.Send(message.ToArray());
        message.SetLength(0);
    }
}

static async Task SendGg2MessagesAsync(
    WebSocket socket, LegacyGg2NetworkClientTransport transport, Uri publicOrigin, CancellationToken cancellationToken)
{
    while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
    {
        if (transport.TryConsumeDisconnectReason(out var reason))
            throw new IOException($"GG2 server disconnected: {reason}");
        if (!transport.TryReceive(out var payload))
        {
            await Task.Delay(5, cancellationToken);
            continue;
        }
        payload = AddBrowserMapMetadata(payload, transport, publicOrigin);
        await socket.SendAsync(payload.AsMemory(), WebSocketMessageType.Binary, true, cancellationToken);
    }
}

static byte[] AddBrowserMapMetadata(byte[] payload, LegacyGg2NetworkClientTransport transport, Uri publicOrigin)
{
    if (!ProtocolCodec.TryDeserialize(payload, out var message) || message is null) return payload;
    string levelName;
    switch (message)
    {
        case WelcomeMessage welcome: levelName = welcome.LevelName; break;
        case SnapshotMessage snapshot: levelName = snapshot.LevelName; break;
        default: return payload;
    }
    if (!levelName.StartsWith("gg2_stock_", StringComparison.OrdinalIgnoreCase)
        && !(levelName.StartsWith("gg2_", StringComparison.OrdinalIgnoreCase) && levelName.Length == 36))
        return payload;

    var stock = levelName.StartsWith("gg2_stock_", StringComparison.OrdinalIgnoreCase);
    var hash = stock ? string.Empty : levelName[4..].ToLowerInvariant();
    var mapName = transport.CurrentMapName;
    var mapUrl = stock ? string.Empty
        : new Uri(publicOrigin, $"/api/gg2/maps/{hash}.png?name={Uri.EscapeDataString(mapName)}").AbsoluteUri;
    var translated = message switch
    {
        WelcomeMessage welcome => welcome with
        {
            IsCustomMap = true, MapDownloadUrl = mapUrl, MapContentHash = hash,
        },
        SnapshotMessage snapshot => snapshot with
        {
            IsCustomMap = true, MapDownloadUrl = mapUrl, MapContentHash = hash,
        },
        _ => message,
    };
    return ProtocolCodec.Serialize(translated);
}

internal sealed class Gg2GatewayLobby
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<LegacyGg2LobbyServer>? _cached;
    private DateTimeOffset _expiresAt;

    public async Task<IReadOnlyList<LegacyGg2LobbyServer>> GetServersAsync(CancellationToken cancellationToken)
    {
        if (_cached is { } cached && DateTimeOffset.UtcNow < _expiresAt) return cached;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_cached is { } ready && DateTimeOffset.UtcNow < _expiresAt) return ready;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(6));
            var servers = await LegacyGg2LobbyClient.FetchAsync(cancellationToken: timeout.Token);
            _cached = servers;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(15);
            return servers;
        }
        finally { _gate.Release(); }
    }
}
