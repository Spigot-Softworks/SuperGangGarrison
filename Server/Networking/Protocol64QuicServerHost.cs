using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using OpenGarrison.Networking;
using OpenGarrison.Protocol;

namespace OpenGarrison.Server;

/// <summary>
/// Native QUIC listener for the protocol-64 backend. It deliberately shares
/// the server message queue with UDP and WebSocket: the packet pump remains
/// backend-neutral while the QUIC runtime owns stream framing and recovery.
/// </summary>
internal sealed class Protocol64QuicServerHost : IAsyncDisposable
{
    private static long _nextSessionId;
    private readonly QuicListener _listener;
    private readonly CompositeServerMessageTransport _transport;
    private readonly Protocol64SchemaRegistry _registry;
    private readonly Action<string> _log;
    private readonly CancellationTokenSource _stopCts = new();
    private readonly X509Certificate2 _certificate;
    private readonly Task _acceptTask;
    private int _disposed;

    private Protocol64QuicServerHost(
        QuicListener listener,
        CompositeServerMessageTransport transport,
        Protocol64SchemaRegistry registry,
        X509Certificate2 certificate,
        Action<string> log)
    {
        _listener = listener;
        _transport = transport;
        _registry = registry;
        _certificate = certificate;
        _log = log;
        _acceptTask = AcceptLoopAsync(_stopCts.Token);
    }

    public static Protocol64QuicServerHost Start(
        int port,
        string certificatePath,
        string? certificatePassword,
        CompositeServerMessageTransport transport,
        Action<string> log)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(port, 0);
        if (string.IsNullOrWhiteSpace(certificatePath))
        {
            throw new InvalidOperationException("A PKCS#12 certificate is required for the QUIC endpoint.");
        }

        var certificate = X509CertificateLoader.LoadPkcs12FromFile(certificatePath, certificatePassword);
        var applicationProtocol = new SslApplicationProtocol("opengarrison-64");
        try
        {
            var listener = QuicListener.ListenAsync(new QuicListenerOptions
            {
                ListenEndPoint = new IPEndPoint(IPAddress.Any, port),
                ApplicationProtocols = [applicationProtocol],
                ConnectionOptionsCallback = (_, _, _) =>
                    ValueTask.FromResult(new QuicServerConnectionOptions
                    {
                        DefaultStreamErrorCode = 0x100,
                        DefaultCloseErrorCode = 0x101,
                        MaxInboundBidirectionalStreams = 64,
                        MaxInboundUnidirectionalStreams = 64,
                        ServerAuthenticationOptions = new SslServerAuthenticationOptions
                        {
                            ApplicationProtocols = [applicationProtocol],
                            ServerCertificate = certificate,
                            EnabledSslProtocols = SslProtocols.Tls13,
                        },
                    }),
            }).AsTask().GetAwaiter().GetResult();

            return new Protocol64QuicServerHost(
                listener,
                transport,
                Protocol64SchemaRegistryFactory.CreateDefault(),
                certificate,
                log);
        }
        catch
        {
            certificate.Dispose();
            throw;
        }
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            QuicConnection connection;
            try
            {
                connection = await _listener.AcceptConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _log($"[server] protocol-64 QUIC accept failed: {exception.Message}");
                return;
            }

            _ = RunPeerAsync(connection, cancellationToken);
        }
    }

    private async Task RunPeerAsync(QuicConnection connection, CancellationToken cancellationToken)
    {
        var sessionId = Interlocked.Increment(ref _nextSessionId);
        var remoteEndPoint = connection.RemoteEndPoint as IPEndPoint;
        var peer = ServerTransportPeer.FromQuicSession(sessionId, remoteEndPoint);
        var container = new Protocol64QuicConnectionContainer(
            // The client QUIC runtime starts with epoch 1 before it can
            // receive any server frame. QUIC already isolates each connection,
            // so use the same fixed per-connection epoch on both sides until
            // an explicit epoch-negotiation frame exists.
            connectionEpoch: 1,
            new Protocol64QuicConnectionOptions
            {
                FirstLocallyInitiatedBidirectionalStreamId = 0,
                PreferDatagramsForLastWins = true,
                DatagramsAvailable = false,
            });
        var runtime = new Protocol64QuicConnectionRuntime(
            connection,
            container,
            _registry,
            new Protocol64QuicRuntimeOptions
            {
                ExpectedInboundDirection = Protocol64Direction.ClientToServer,
                WarningLogger = _log,
                FaultSink = new DelegateProtocol64FaultSink(fault =>
                    _log(fault.Exception is null
                        ? $"[server] protocol-64 QUIC fault peer={peer}: {fault.Kind} {fault.Message}"
                        : $"[server] protocol-64 QUIC fault peer={peer}: {fault.Kind} {fault.Message} exception={fault.Exception.GetType().Name}: {fault.Exception.Message}")),
                FrameReceived = frame => _transport.EnqueueInboundProtocol64Frame(peer, frame),
            });

        _transport.RegisterProtocol64QuicConnection(peer, runtime);
        try
        {
            await runtime.RunAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _log($"[server] protocol-64 QUIC peer {peer} closed with error: {exception.Message}");
        }
        finally
        {
            _transport.UnregisterProtocol64QuicConnection(peer);
            await runtime.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _stopCts.Cancel();
        try
        {
            await _listener.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
        }

        try
        {
            await _acceptTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        _certificate.Dispose();
        _stopCts.Dispose();
    }
}
