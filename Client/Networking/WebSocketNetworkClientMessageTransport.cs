#nullable enable

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace OpenGarrison.Client;

/// <summary>
/// Desktop protocol-64 WebSocket transport. Each receive operation assembles
/// one complete binary WebSocket message before exposing it to the client
/// protocol decoder; send failures become an explicit disconnect instead of a
/// silently lost reliable command.
/// </summary>
internal sealed class WebSocketNetworkClientMessageTransport : INetworkClientMessageTransport
{
    private const int ReceiveBufferBytes = 16 * 1024;
    private const int MaxMessageBytes = 4 * 1024 * 1024;
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    private readonly ClientWebSocket _webSocket;
    private readonly ConcurrentQueue<byte[]> _inboundMessages = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCts;
    private readonly Task _receiveTask;
    private string? _disconnectReason;
    private int _disposed;

    private WebSocketNetworkClientMessageTransport(
        ClientWebSocket webSocket,
        Uri endpoint,
        CancellationTokenSource lifetimeCts)
    {
        _webSocket = webSocket;
        _lifetimeCts = lifetimeCts;
        RemoteDescription = RedactEndpoint(endpoint);
        _receiveTask = ReceiveLoopAsync(lifetimeCts.Token);
    }

    public bool HasPendingMessages => !_inboundMessages.IsEmpty;

    public bool IsLoopbackConnection
    {
        get
        {
            if (!Uri.TryCreate(RemoteDescription, UriKind.Absolute, out var endpoint))
            {
                return false;
            }

            if (string.Equals(endpoint.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return IPAddress.TryParse(endpoint.Host, out var address)
                && IPAddress.IsLoopback(address);
        }
    }

    public string RemoteDescription { get; }

    public static bool IsWebSocketEndpoint(string? host)
        => Uri.TryCreate(host?.Trim(), UriKind.Absolute, out var endpoint)
            && endpoint.Scheme is "ws64" or "wss64";

    public static bool TryConnect(
        string host,
        int port,
        out INetworkClientMessageTransport? transport,
        out string error)
    {
        transport = null;
        error = string.Empty;
        try
        {
            if (!TryCreateEndpoint(host, port, out var endpoint, out error))
            {
                return false;
            }

            var socket = new ClientWebSocket();
            using var connectCts = new CancellationTokenSource(ConnectTimeout);
            socket.ConnectAsync(endpoint, connectCts.Token).GetAwaiter().GetResult();
            var lifetimeCts = new CancellationTokenSource();
            transport = new WebSocketNetworkClientMessageTransport(socket, endpoint, lifetimeCts);
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            error = exception.Message;
            return false;
        }
    }

    public bool TryReceive(out byte[] payload)
        => _inboundMessages.TryDequeue(out payload!);

    public void Send(byte[] payload)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (payload.Length == 0)
        {
            return;
        }

        _sendGate.Wait(_lifetimeCts.Token);
        try
        {
            if (_webSocket.State != WebSocketState.Open)
            {
                SetDisconnectReason("WebSocket is not open.");
                return;
            }

            _webSocket.SendAsync(
                payload,
                WebSocketMessageType.Binary,
                endOfMessage: true,
                _lifetimeCts.Token).GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            SetDisconnectReason($"WebSocket send failed: {exception.Message}");
        }
        finally
        {
            _sendGate.Release();
        }
    }

    public bool TryConsumeDisconnectReason(out string reason)
    {
        reason = Interlocked.Exchange(ref _disconnectReason, null) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(reason);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _lifetimeCts.Cancel();
        try
        {
            _receiveTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
        }

        try
        {
            _webSocket.Dispose();
        }
        finally
        {
            _sendGate.Dispose();
            _lifetimeCts.Dispose();
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[ReceiveBufferBytes];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var message = new MemoryStream();
                WebSocketMessageType? messageType = null;
                while (true)
                {
                    var result = await _webSocket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        SetDisconnectReason("WebSocket connection closed by the server.");
                        return;
                    }

                    messageType ??= result.MessageType;
                    if (messageType != WebSocketMessageType.Binary || result.MessageType != WebSocketMessageType.Binary)
                    {
                        SetDisconnectReason("WebSocket delivered a non-binary protocol-64 message.");
                        return;
                    }

                    if (message.Length + result.Count > MaxMessageBytes)
                    {
                        SetDisconnectReason("WebSocket protocol-64 message exceeded the size limit.");
                        return;
                    }

                    message.Write(buffer, 0, result.Count);
                    if (result.EndOfMessage)
                    {
                        _inboundMessages.Enqueue(message.ToArray());
                        break;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            SetDisconnectReason($"WebSocket receive failed: {exception.Message}");
        }
    }

    internal static bool TryCreateEndpoint(string host, int port, out Uri endpoint, out string error)
    {
        endpoint = null!;
        error = string.Empty;
        if (!Uri.TryCreate(host?.Trim(), UriKind.Absolute, out var source)
            || source.Scheme is not ("ws64" or "wss64"))
        {
            error = "A ws64 or wss64 endpoint is required.";
            return false;
        }

        var resolvedPort = port > 0
            ? port
            : source.Port > 0
                ? source.Port
                : source.Scheme == "wss64" ? 443 : 80;
        if (resolvedPort is <= 0 or > 65535)
        {
            error = "WebSocket port must be between 1 and 65535.";
            return false;
        }

        var scheme = source.Scheme == "wss64" ? "wss" : "ws";
        var path = string.IsNullOrEmpty(source.AbsolutePath) || source.AbsolutePath == "/"
            ? "/opengarrison/ws64"
            : source.AbsolutePath;
        endpoint = new UriBuilder(scheme, source.Host, resolvedPort, path)
        {
            Query = source.Query.TrimStart('?'),
            Fragment = string.Empty,
        }.Uri;
        return true;
    }

    private void SetDisconnectReason(string reason)
        => Interlocked.CompareExchange(ref _disconnectReason, reason, null);

    private static string RedactEndpoint(Uri endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint.Query))
        {
            return endpoint.ToString();
        }

        return new UriBuilder(endpoint)
        {
            Query = "token=REDACTED",
            Fragment = string.Empty,
        }.Uri.ToString();
    }
}
