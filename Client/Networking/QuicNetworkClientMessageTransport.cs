#nullable enable

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using OpenGarrison.Networking;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

/// <summary>
/// Native protocol-64 client transport over System.Net.Quic.
///
/// The public client seam remains synchronous, while the QUIC runtime owns all
/// asynchronous stream I/O. Inbound values are complete encoded protocol-64
/// frames; outbound values are decoded only to hand their events to the
/// runtime, which applies the protocol-64 delivery scheduler before writing.
/// </summary>
internal sealed class QuicNetworkClientMessageTransport : INetworkClientMessageTransport
{
    private const string Alpn = "opengarrison-64";
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    private readonly Protocol64QuicConnectionRuntime _runtime;
    private readonly Protocol64SchemaRegistry _registry;
    private readonly CancellationTokenSource _lifetimeCts;
    private readonly ConcurrentQueue<byte[]> _inboundFrames = new();
    private Task? _runtimeMonitorTask;
    private int _disposed;
    private string? _disconnectReason;

    private QuicNetworkClientMessageTransport(
        IPEndPoint remoteEndPoint,
        string tlsHost,
        Protocol64QuicConnectionRuntime runtime,
        Protocol64SchemaRegistry registry,
        CancellationTokenSource lifetimeCts)
    {
        _runtime = runtime;
        _registry = registry;
        _lifetimeCts = lifetimeCts;
        RemoteEndPoint = remoteEndPoint;
        TlsHost = tlsHost;
    }

    public IPEndPoint RemoteEndPoint { get; }

    public string TlsHost { get; }

    public bool HasPendingMessages => !_inboundFrames.IsEmpty;

    public bool IsLoopbackConnection => IPAddress.IsLoopback(RemoteEndPoint.Address);

    public string RemoteDescription
        => $"quic64://{FormatHost(RemoteEndPoint.Address)}:{RemoteEndPoint.Port}";

    public static bool IsQuicEndpoint(string? host)
        => Uri.TryCreate(host?.Trim(), UriKind.Absolute, out var endpoint)
            && string.Equals(endpoint.Scheme, "quic64", StringComparison.OrdinalIgnoreCase);

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
            if (!TryResolveEndpoint(host, port, out var tlsHost, out var remoteEndPoint, out error))
            {
                return false;
            }

            using var connectCts = new CancellationTokenSource(ConnectTimeout);
            var connection = QuicConnection.ConnectAsync(
                new QuicClientConnectionOptions
                {
                    RemoteEndPoint = remoteEndPoint,
                    DefaultStreamErrorCode = 0x100,
                    DefaultCloseErrorCode = 0x101,
                    ClientAuthenticationOptions = new SslClientAuthenticationOptions
                    {
                        TargetHost = tlsHost,
                        ApplicationProtocols = [new SslApplicationProtocol(Alpn)],
                        EnabledSslProtocols = SslProtocols.Tls13,
                    },
                },
                connectCts.Token).AsTask().GetAwaiter().GetResult();

            var lifetimeCts = new CancellationTokenSource();
            var registry = Protocol64SchemaRegistryFactory.CreateDefault();
            var container = new Protocol64QuicConnectionContainer(connectionEpoch: 1);
            QuicNetworkClientMessageTransport? created = null;
            var runtime = new Protocol64QuicConnectionRuntime(
                connection,
                container,
                registry,
                new Protocol64QuicRuntimeOptions
                {
                    ExpectedInboundDirection = Protocol64Direction.ServerToClient,
                    FrameReceived = frame => created?._inboundFrames.Enqueue(frame.EncodedPayload.ToArray()),
                });

            created = new QuicNetworkClientMessageTransport(
                remoteEndPoint,
                tlsHost,
                runtime,
                registry,
                lifetimeCts);
            created.Start();
            transport = created;
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            error = exception.Message;
            return false;
        }
    }

    private void Start()
    {
        var runTask = _runtime.RunAsync(_lifetimeCts.Token);
        _runtimeMonitorTask = ObserveRuntimeAsync(runTask);
    }

    public bool TryReceive(out byte[] payload)
        => _inboundFrames.TryDequeue(out payload!);

    public void Send(byte[] payload)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(payload);

        var decoded = Protocol64FrameCodec.Decode(
            payload,
            _registry,
            new Protocol64FrameDecodeOptions
            {
                Backend = "quic",
                ExpectedDirection = Protocol64Direction.ClientToServer,
            });

        if (decoded.Succeeded && decoded.Event is not null)
        {
            EnqueueEvent(decoded.Event);
            return;
        }

        // Details/status callers still use the legacy codec at this seam.
        // Convert those messages to their protocol-64 schema before handing
        // them to the same runtime scheduler.
        if (ProtocolCodec.TryDeserialize(payload, out var legacyMessage) && legacyMessage is not null)
        {
            EnqueueEvent(legacyMessage);
            return;
        }

        SetDisconnectReason("Protocol-64 QUIC rejected an outbound frame.");
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
            _runtime.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _runtimeMonitorTask?.GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            SetDisconnectReason(exception.Message);
        }
        finally
        {
            _lifetimeCts.Dispose();
        }
    }

    private async Task ObserveRuntimeAsync(Task runTask)
    {
        try
        {
            await runTask.ConfigureAwait(false);
            if (Volatile.Read(ref _disposed) == 0)
            {
                SetDisconnectReason("QUIC connection closed.");
            }
        }
        catch (OperationCanceledException) when (_lifetimeCts.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            SetDisconnectReason($"QUIC connection failed: {exception.Message}");
        }
    }

    private void EnqueueEvent(object eventValue)
    {
        var result = _runtime.EnqueueEvent(eventValue);
        if (!result.Accepted)
        {
            SetDisconnectReason(result.Fault?.Message ?? "Protocol-64 QUIC rejected an outbound event.");
        }
    }

    private void SetDisconnectReason(string reason)
        => Interlocked.CompareExchange(ref _disconnectReason, reason, null);

    private static bool TryResolveEndpoint(
        string host,
        int port,
        out string tlsHost,
        out IPEndPoint remoteEndPoint,
        out string error)
    {
        tlsHost = string.Empty;
        remoteEndPoint = null!;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(host))
        {
            error = "QUIC host is empty.";
            return false;
        }

        var trimmedHost = host.Trim();
        if (Uri.TryCreate(trimmedHost, UriKind.Absolute, out var endpoint)
            && string.Equals(endpoint.Scheme, "quic64", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrEmpty(endpoint.AbsolutePath) && endpoint.AbsolutePath != "/")
            {
                error = "A quic64 endpoint cannot include a path.";
                return false;
            }

            tlsHost = endpoint.Host;
            if (port <= 0)
            {
                port = endpoint.Port;
            }
        }
        else
        {
            tlsHost = trimmedHost;
        }

        if (string.IsNullOrWhiteSpace(tlsHost))
        {
            error = "QUIC host is empty.";
            return false;
        }

        if (port is <= 0 or > 65535)
        {
            error = "QUIC port must be between 1 and 65535.";
            return false;
        }

        var addresses = Dns.GetHostAddresses(tlsHost);
        var address = addresses.FirstOrDefault(candidate => candidate.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            ?? addresses.FirstOrDefault();
        if (address is null)
        {
            error = $"could not resolve host {tlsHost}";
            return false;
        }

        remoteEndPoint = new IPEndPoint(address, port);
        return true;
    }

    private static string FormatHost(IPAddress address)
        => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? $"[{address}]"
            : address.ToString();
}
