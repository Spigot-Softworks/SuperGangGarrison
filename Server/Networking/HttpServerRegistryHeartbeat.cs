using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using OpenGarrison.Protocol;

sealed class HttpServerRegistryHeartbeat : IDisposable
{
    private static readonly TimeSpan DefaultHeartbeatInterval = TimeSpan.FromSeconds(30);

    private readonly HttpClient _httpClient = new();
    private readonly Uri _registryUri;
    private readonly string? _token;
    private readonly Func<ServerRegistrySnapshot> _snapshotProvider;
    private readonly Action<string> _log;
    private readonly TimeSpan _heartbeatInterval;
    private string _serverId;
    private TimeSpan _lastSent;
    private bool _sendInProgress;
    private bool _disposed;

    public HttpServerRegistryHeartbeat(
        Uri registryUri,
        string? token,
        Func<ServerRegistrySnapshot> snapshotProvider,
        Action<string> log,
        TimeSpan? heartbeatInterval = null)
    {
        _registryUri = registryUri;
        _token = token;
        _snapshotProvider = snapshotProvider;
        _log = log;
        _heartbeatInterval = heartbeatInterval ?? DefaultHeartbeatInterval;
        _lastSent = -_heartbeatInterval;
        _serverId = CreateServerId(snapshotProvider());
    }

    public void Tick(TimeSpan now)
    {
        if (_disposed || _sendInProgress || now - _lastSent < _heartbeatInterval)
        {
            return;
        }

        _lastSent = now;
        _sendInProgress = true;
        _ = SendHeartbeatAsync();
    }

    public void Remove()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            SendRemoveAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log($"[server] registry remove failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _httpClient.Dispose();
    }

    private async Task SendHeartbeatAsync()
    {
        try
        {
            var snapshot = _snapshotProvider();
            var payload = new ServerRegistryHeartbeatRequest
            {
                Action = "heartbeat",
                Token = _token ?? string.Empty,
                ServerId = _serverId,
                Name = snapshot.Name,
                Host = snapshot.PublicHost ?? string.Empty,
                UdpPort = snapshot.UdpPort,
                WebSocketPort = snapshot.WebSocketPort,
                WebSocketUrl = snapshot.WebSocketUrl ?? string.Empty,
                IsPrivate = snapshot.IsPrivate,
                BuildVersion = snapshot.BuildVersion,
                ReleaseChannel = snapshot.ReleaseChannel,
                CompatibilityKey = snapshot.CompatibilityKey,
                Map = snapshot.Map,
                Mode = snapshot.Mode,
                Players = snapshot.Players,
                MaxPlayers = snapshot.MaxPlayers,
                Spectators = snapshot.Spectators,
                ProtocolVersion = ProtocolVersion.Current,
            };

            using var response = await _httpClient.PostAsJsonAsync(_registryUri, payload).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _log($"[server] registry heartbeat failed: HTTP {(int)response.StatusCode}");
                return;
            }

            var registryResponse = await response.Content
                .ReadFromJsonAsync<ServerRegistryHeartbeatResponse>()
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(registryResponse?.ServerId))
            {
                _serverId = registryResponse.ServerId;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log($"[server] registry heartbeat failed: {ex.Message}");
        }
        finally
        {
            _sendInProgress = false;
        }
    }

    private async Task SendRemoveAsync()
    {
        var payload = new ServerRegistryRemoveRequest
        {
            Action = "remove",
            Token = _token ?? string.Empty,
            ServerId = _serverId,
        };
        if (string.IsNullOrWhiteSpace(payload.ServerId) || string.IsNullOrWhiteSpace(payload.Token))
        {
            return;
        }

        using var response = await _httpClient.PostAsJsonAsync(_registryUri, payload).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _log($"[server] registry remove failed: HTTP {(int)response.StatusCode}");
        }
    }

    private static string CreateServerId(ServerRegistrySnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.PublicHost))
        {
            return string.Empty;
        }

        var host = snapshot.PublicHost.Trim().ToLowerInvariant();
        return $"og2:{host}:{snapshot.UdpPort}:{snapshot.WebSocketPort}:{snapshot.WebSocketUrl}";
    }

    private sealed class ServerRegistryHeartbeatResponse
    {
        [JsonPropertyName("serverId")]
        public string ServerId { get; set; } = string.Empty;
    }

    private sealed class ServerRegistryHeartbeatRequest
    {
        [JsonPropertyName("action")]
        public string Action { get; set; } = "heartbeat";

        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("serverId")]
        public string ServerId { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("host")]
        public string Host { get; set; } = string.Empty;

        [JsonPropertyName("udpPort")]
        public int UdpPort { get; set; }

        [JsonPropertyName("webSocketPort")]
        public int WebSocketPort { get; set; }

        [JsonPropertyName("webSocketUrl")]
        public string WebSocketUrl { get; set; } = string.Empty;

        [JsonPropertyName("private")]
        public bool IsPrivate { get; set; }

        [JsonPropertyName("buildVersion")]
        public string BuildVersion { get; set; } = string.Empty;

        [JsonPropertyName("releaseChannel")]
        public string ReleaseChannel { get; set; } = string.Empty;

        [JsonPropertyName("compatibilityKey")]
        public string CompatibilityKey { get; set; } = string.Empty;

        [JsonPropertyName("map")]
        public string Map { get; set; } = string.Empty;

        [JsonPropertyName("mode")]
        public string Mode { get; set; } = string.Empty;

        [JsonPropertyName("players")]
        public int Players { get; set; }

        [JsonPropertyName("maxPlayers")]
        public int MaxPlayers { get; set; }

        [JsonPropertyName("spectators")]
        public int Spectators { get; set; }

        [JsonPropertyName("protocolVersion")]
        public int ProtocolVersion { get; set; }
    }

    private sealed class ServerRegistryRemoveRequest
    {
        [JsonPropertyName("action")]
        public string Action { get; set; } = "remove";

        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("serverId")]
        public string ServerId { get; set; } = string.Empty;
    }
}

readonly record struct ServerRegistrySnapshot(
    string Name,
    string? PublicHost,
    int UdpPort,
    int WebSocketPort,
    string? WebSocketUrl,
    bool IsPrivate,
    string BuildVersion,
    string ReleaseChannel,
    string CompatibilityKey,
    string Map,
    string Mode,
    int Players,
    int MaxPlayers,
    int Spectators);
