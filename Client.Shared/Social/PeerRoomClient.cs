using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace OpenGarrison.ClientShared;

public sealed record PeerPracticeSettings(string Map = "Harvest", int MapArea = 1, int TickRate = 30,
    int TimeLimitMinutes = 15, int CaptureLimit = 5, int RespawnSeconds = 5, int RedBots = 0, int BlueBots = 1,
    bool SpecialAbilities = true, string Difficulty = "standard");
public sealed record PeerRoomRequest(string ClientId, string ClientSecret, string FriendCode, string DisplayName,
    string RequestId, int ProtocolVersion, string ContentId, string Kind = "LastToDie", string Code = "",
    PeerPracticeSettings? Settings = null);
public sealed record PeerRoomGrant(string Code, byte Slot, string Kind, int ProtocolVersion, string ContentId,
    string Endpoint, JsonElement IceServers);
public sealed record PeerRoomPlayer(byte Slot, string ClientId, string Name, bool Connected, bool Ready, string Team);
public sealed record PeerRoomMessage(string Type, string Code = "", string Kind = "", string Phase = "",
    int Revision = 0, int Generation = 0, int MaximumPlayers = 4, PeerPracticeSettings? Settings = null,
    PeerRoomPlayer[]? Players = null, byte Source = 0, string? Data = null, string Reason = "");
public sealed record PeerRoomCommand(string Type, byte Target = 0, string? Data = null, bool Ready = false,
    string Team = "Red", int Revision = 0, PeerPracticeSettings? Settings = null);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PeerRoomRequest))]
[JsonSerializable(typeof(PeerRoomGrant))]
[JsonSerializable(typeof(PeerRoomMessage))]
[JsonSerializable(typeof(PeerRoomCommand))]
public partial class PeerRoomJsonContext : JsonSerializerContext;

/// <summary>Lobby/signaling connection. A binary message is a fallback packet with its separately admitted peer slot.</summary>
public sealed class PeerRoomConnection : IDisposable
{
    private readonly ClientWebSocket _socket = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Channel<(byte[] Bytes, WebSocketMessageType Type)> _outgoing = Channel.CreateBounded<(byte[], WebSocketMessageType)>(256);
    private readonly ConcurrentQueue<PeerRoomMessage> _messages = new();
    private readonly ConcurrentQueue<(byte Slot, byte[] Payload)> _packets = new();
    private Task? _receiver, _sender;
    private int _outgoingBytes, _incomingBytes;
    private bool _disposed;
    private readonly HttpClient _http;
    private readonly Uri _origin;
    private readonly PeerRoomRequest _admission;
    public PeerRoomGrant Grant { get; }
    public string Error { get; private set; } = "";
    public bool IsConnected => _socket.State == WebSocketState.Open;
    private PeerRoomConnection(PeerRoomGrant grant, HttpClient http, Uri origin, PeerRoomRequest admission)
    {
        Grant = grant; _http = http; _origin = origin;
        _admission = admission with { Code = grant.Code };
    }

    public static async Task<PeerRoomConnection> OpenAsync(HttpClient http, Uri origin, PeerRoomRequest request,
        bool create, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(new Uri(origin, "/api/peer-rooms/" + (create ? "create" : "join")),
            request, PeerRoomJsonContext.Default.PeerRoomRequest, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var reason = "The room service could not complete the request.";
            try
            {
                using var json = JsonDocument.Parse(body);
                if (json.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                    reason = detail.GetString() ?? reason;
            }
            catch (JsonException) { }
            throw new HttpRequestException(reason, null, response.StatusCode);
        }
        var grant = await response.Content.ReadFromJsonAsync(PeerRoomJsonContext.Default.PeerRoomGrant, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Missing room admission.");
        if (grant.Kind != request.Kind || grant.ProtocolVersion != request.ProtocolVersion || grant.ContentId != request.ContentId
            || grant.Slot is < 1 or > 4 || !RelayRoomCode.TryNormalize(grant.Code, out _)
            || !Uri.TryCreate(grant.Endpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Host != origin.Host || endpoint.Port != origin.Port
            || endpoint.Scheme != (origin.Scheme == "https" ? "wss" : "ws")
            || endpoint.AbsolutePath != $"/api/peer-rooms/ws/{grant.Code}/{grant.Slot}")
            throw new InvalidDataException("The room returned an incompatible admission.");
        var connection = new PeerRoomConnection(grant, http, origin, request);
        try
        {
            await connection._socket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
            connection._sender = connection.SendLoop();
            connection._receiver = connection.ReceiveLoop();
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }

    public bool TryReadMessage(out PeerRoomMessage message) => _messages.TryDequeue(out message!);
    public bool TryReadPacket(out byte slot, out byte[] payload)
    {
        if (_packets.TryDequeue(out var item))
        { slot = item.Slot; payload = item.Payload; Interlocked.Add(ref _incomingBytes, -payload.Length); return true; }
        slot = 0; payload = []; return false;
    }
    public void Send(PeerRoomCommand command)
        => Queue(JsonSerializer.SerializeToUtf8Bytes(command, PeerRoomJsonContext.Default.PeerRoomCommand), WebSocketMessageType.Text);
    public void SendPacket(byte target, byte[] payload)
    {
        if (target is < 1 or > 4 || payload.Length > 4 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(target));
        var bytes = new byte[payload.Length + 1]; bytes[0] = target; payload.CopyTo(bytes, 1);
        Queue(bytes, WebSocketMessageType.Binary);
    }
    private void Queue(byte[] bytes, WebSocketMessageType type)
    {
        if (_disposed) return;
        if (Interlocked.Add(ref _outgoingBytes, bytes.Length) > 8 * 1024 * 1024 || !_outgoing.Writer.TryWrite((bytes, type)))
        { Interlocked.Add(ref _outgoingBytes, -bytes.Length); Fail("The room connection cannot keep up."); }
    }
    private async Task SendLoop()
    {
        try
        {
            await foreach (var item in _outgoing.Reader.ReadAllAsync(_stop.Token).ConfigureAwait(false))
            {
                await _socket.SendAsync(item.Bytes.AsMemory(), item.Type, true, _stop.Token).ConfigureAwait(false);
                Interlocked.Add(ref _outgoingBytes, -item.Bytes.Length);
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException)
        { if (!_disposed) Fail("The room connection ended."); }
    }
    private async Task ReceiveLoop()
    {
        var buffer = new byte[16384];
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                using var message = new MemoryStream();
                ValueWebSocketReceiveResult result;
                do
                {
                    result = await _socket.ReceiveAsync(buffer.AsMemory(), _stop.Token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close) { Fail("The room connection ended."); return; }
                    message.Write(buffer, 0, result.Count);
                    if (message.Length > 4 * 1024 * 1024 + 1) throw new InvalidDataException("Room message is too large.");
                } while (!result.EndOfMessage);
                var bytes = message.ToArray();
                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    if (bytes.Length < 2 || bytes[0] is < 1 or > 4) throw new InvalidDataException("Invalid room packet.");
                    var payload = bytes[1..];
                    if (Interlocked.Add(ref _incomingBytes, payload.Length) > 8 * 1024 * 1024) throw new InvalidDataException("Room packet queue is full.");
                    _packets.Enqueue((bytes[0], payload));
                }
                else
                {
                    if (_messages.Count >= 256) throw new InvalidDataException("Room command queue is full.");
                    var value = JsonSerializer.Deserialize(bytes, PeerRoomJsonContext.Default.PeerRoomMessage);
                    if (value is not null) _messages.Enqueue(value);
                }
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException or JsonException or InvalidDataException)
        { if (!_disposed) Fail("The room connection ended: " + ex.Message); }
    }
    private void Fail(string reason) { Error = reason; Dispose(); }
    private Task? _leaveTask;
    public Task LeaveAsync() => _leaveTask ??= LeaveCoreAsync();
    private async Task LeaveCoreAsync()
    {
        try
        {
            Send(new("leave"));
            // A flushed browser socket does not confirm that the service released the seat.
            // The authenticated response is the barrier before another admission request.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            using var response = await _http.PostAsJsonAsync(new Uri(_origin, "/api/peer-rooms/leave"),
                _admission, PeerRoomJsonContext.Default.PeerRoomRequest, timeout.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
        finally { Dispose(); }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _outgoing.Writer.TryComplete();
        _stop.Cancel();
        _socket.Dispose();
    }
}
