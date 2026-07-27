using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using OpenGarrison.Networking;
using OpenGarrison.Protocol;

namespace OpenGarrison.Server;

public enum Protocol64WebSocketRecoveryState : byte
{
    Open = 1,
    Recovering = 2,
    ProtocolError = 3,
    Closed = 4,
}

public enum Protocol64WebSocketReceiveStatus : byte
{
    Frame = 1,
    IgnoredMalformedFrame = 2,
    Closed = 3,
    ProtocolError = 4,
}

public sealed record Protocol64WebSocketOptions
{
    public const int DefaultMaxWsRetries = 2;

    public static Protocol64WebSocketOptions Default { get; } = new();

    public int MaxInboundFrameBytes { get; init; } = Protocol64FrameLimits.DefaultMaxEnvelopeBytes;

    public int ReceiveBufferBytes { get; init; } = 8 * 1024;

    public int MaxWsRetries { get; init; } = DefaultMaxWsRetries;

    /// <summary>
    /// Upper bound for accepted reliable frames waiting for the WebSocket
    /// writer. Reliable frames are never discarded to enforce this bound;
    /// enqueueing beyond it fails explicitly with backpressure.
    /// </summary>
    public int MaxReliableQueueFrames { get; init; } = 4096;

    /// <summary>
    /// Upper bound for encoded bytes owned by the reliable queue.
    /// </summary>
    public long MaxReliableQueueBytes { get; init; } = 4 * 1024 * 1024;

    public Protocol64FrameLimits FrameLimits { get; init; } = Protocol64FrameLimits.Default;

    public IProtocol64FaultSink? FaultSink { get; init; }

    /// <summary>
    /// Optional typed callback for a complete WebSocket message that cannot be
    /// deserialized. The default path still logs and ignores the first bad
    /// message according to the protocol-64 recovery state machine.
    /// </summary>
    public Action<Protocol64FaultKind, Protocol64Fault>? FaultCallbackByKind { get; init; }

    /// <summary>
    /// Receives a warning for a complete WebSocket message that is not a valid
    /// protocol-64 frame. The default deliberately writes a concise warning to
    /// stderr; server integration can replace it with its logger.
    /// </summary>
    public Action<string>? WarningLogger { get; init; } =
        static message => Console.Error.WriteLine($"[network] warning: {message}");

    /// <summary>
    /// Optional reconnect hook. The connection remains in Recovering state when
    /// this is absent, allowing the next complete WebSocket message to prove that
    /// the stream is usable. A replacement is attempted at most MaxWsRetries times.
    /// </summary>
    public Func<CancellationToken, ValueTask<WebSocket?>>? ReopenAsync { get; init; }

    public string BackendName { get; init; } = "websocket";
}

public sealed record Protocol64WebSocketReceiveResult(
    Protocol64WebSocketReceiveStatus Status,
    Protocol64FrameDecodeResult? Decoded = null,
    Protocol64Fault? Fault = null,
    byte[]? EncodedPayload = null)
{
    public bool HasFrame => Status == Protocol64WebSocketReceiveStatus.Frame && Decoded?.Succeeded == true;
}

/// <summary>
/// Protocol-64 WebSocket connection container.
///
/// This type intentionally does not participate in the legacy server transport
/// interfaces. It owns complete WebSocket messages, protocol-64 framing, outbound
/// delivery mailboxes, and the malformed-frame recovery state machine so that the
/// canonical backend can be integrated independently of the old UDP/WS adapter.
/// </summary>
public sealed class Protocol64WebSocketConnection : IDisposable, IAsyncDisposable
{
    private readonly Protocol64SchemaRegistry _registry;
    private readonly Protocol64WebSocketOptions _options;
    private readonly Channel<OutboundFrame> _reliableQueue =
        Channel.CreateUnbounded<OutboundFrame>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });
    private readonly Channel<string> _outboundSignals =
        Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });
    private readonly ConcurrentDictionary<string, OutboundFrame> _lastWinsMailbox = new(StringComparer.Ordinal);
    private readonly object _outboundGate = new();
    private readonly object _socketGate = new();
    private readonly SemaphoreSlim _receiveGate = new(1, 1);
    private WebSocket _webSocket;
    private long _nextFrameId;
    private int _reliableQueueCount;
    private long _reliableQueueBytes;
    private long _lastWinsReplacementCount;
    private int _consecutiveInvalidFrames;
    private int _wsRetryCount;
    private int _recoveryState = (int)Protocol64WebSocketRecoveryState.Open;
    private int _disposed;

    public Protocol64WebSocketConnection(
        WebSocket webSocket,
        Protocol64SchemaRegistry registry,
        ulong connectionEpoch,
        Protocol64WebSocketOptions? options = null)
    {
        _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _options = options ?? Protocol64WebSocketOptions.Default;

        if (connectionEpoch == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(connectionEpoch), "Connection epochs must be non-zero.");
        }

        if (_options.MaxInboundFrameBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxInboundFrameBytes must be positive.");
        }

        if (_options.ReceiveBufferBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "ReceiveBufferBytes must be positive.");
        }

        if (_options.MaxWsRetries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxWsRetries cannot be negative.");
        }

        if (_options.MaxReliableQueueFrames <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxReliableQueueFrames must be positive.");
        }

        if (_options.MaxReliableQueueBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxReliableQueueBytes must be positive.");
        }

        if (_options.FrameLimits is null)
        {
            throw new ArgumentException("FrameLimits cannot be null.", nameof(options));
        }

        ConnectionEpoch = connectionEpoch;
    }

    public ulong ConnectionEpoch { get; }

    public Protocol64NetworkTelemetry Telemetry { get; } = new();

    public ulong LastAllocatedFrameId
        => unchecked((ulong)Volatile.Read(ref _nextFrameId));

    public Protocol64WebSocketRecoveryState RecoveryState
        => (Protocol64WebSocketRecoveryState)Volatile.Read(ref _recoveryState);

    public int ConsecutiveInvalidFrameCount
        => Volatile.Read(ref _consecutiveInvalidFrames);

    public int WsRetryCount
        => Volatile.Read(ref _wsRetryCount);

    public int ReliableQueueCount
        => Math.Max(0, Volatile.Read(ref _reliableQueueCount));

    public long ReliableQueueBytes
        => Math.Max(0L, Interlocked.Read(ref _reliableQueueBytes));

    public int PendingLastWinsCount
        => _lastWinsMailbox.Count;

    public long LastWinsReplacementCount
        => Interlocked.Read(ref _lastWinsReplacementCount);

    public bool IsDisposed
        => Volatile.Read(ref _disposed) != 0;

    public Protocol64FrameHeader QueueReliable<TEvent>(
        TEvent eventValue,
        Protocol64FrameEncodeOptions? encodeOptions = null)
    {
        var frame = EncodeEvent(eventValue, encodeOptions, lastWinsKey: null);
        EnqueueReliable(frame);
        return frame.Header;
    }

    public Protocol64FrameHeader PublishLastWins<TEvent>(
        string key,
        TEvent eventValue,
        Protocol64FrameEncodeOptions? encodeOptions = null)
    {
        ValidateMailboxKey(key);
        var frame = EncodeEvent(eventValue, encodeOptions, key);
        PublishLastWinsFrame(key, frame);
        return frame.Header;
    }

    /// <summary>
    /// Queues a validated, copied protocol-64 frame. Copying at the ownership
    /// boundary prevents a caller from changing bytes after reliable acceptance.
    /// </summary>
    public Protocol64FrameHeader QueueReliableFrame(ReadOnlyMemory<byte> encodedFrame)
    {
        var frame = MaterializeEncodedFrame(encodedFrame, lastWinsKey: null, expectedDelivery: null);
        EnqueueReliable(frame);
        return frame.Header;
    }

    public Protocol64FrameHeader QueueReliableFrame(
        ReadOnlyMemory<byte> encodedFrame,
        Protocol64DeliveryDescriptor expectedDelivery)
    {
        var frame = MaterializeEncodedFrame(encodedFrame, lastWinsKey: null, expectedDelivery);
        EnqueueReliable(frame);
        return frame.Header;
    }

    /// <summary>
    /// Publishes a validated, copied protocol-64 frame into a per-key mailbox.
    /// Replacing an existing key is intentional LastWins behavior, not a reliable
    /// queue drop, and is counted by LastWinsReplacementCount.
    /// </summary>
    public Protocol64FrameHeader PublishLastWinsFrame(
        string key,
        ReadOnlyMemory<byte> encodedFrame)
    {
        ValidateMailboxKey(key);
        var frame = MaterializeEncodedFrame(encodedFrame, key, expectedDelivery: null);
        PublishLastWinsFrame(key, frame);
        return frame.Header;
    }

    public Protocol64FrameHeader PublishLastWinsFrame(
        string key,
        ReadOnlyMemory<byte> encodedFrame,
        Protocol64DeliveryDescriptor expectedDelivery)
    {
        ValidateMailboxKey(key);
        var frame = MaterializeEncodedFrame(encodedFrame, key, expectedDelivery);
        PublishLastWinsFrame(key, frame);
        return frame.Header;
    }

    public async ValueTask<bool> SendNextAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_reliableQueue.Reader.TryRead(out var reliableFrame))
        {
            Interlocked.Decrement(ref _reliableQueueCount);
            Interlocked.Add(ref _reliableQueueBytes, -reliableFrame.Payload.Length);
            await SendFrameAsync(reliableFrame, cancellationToken).ConfigureAwait(false);
            return true;
        }

        while (_outboundSignals.Reader.TryRead(out var signal))
        {
            if (signal.Length == 0)
            {
                continue;
            }

            OutboundFrame? lastWinsFrame = null;
            lock (_outboundGate)
            {
                if (_lastWinsMailbox.TryRemove(signal, out var pendingFrame))
                {
                    lastWinsFrame = pendingFrame;
                }
            }

            if (lastWinsFrame is not null)
            {
                await SendFrameAsync(lastWinsFrame, cancellationToken).ConfigureAwait(false);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Runs the single WebSocket writer. Reliable frames are sent before pending
    /// LastWins frames. An accepted reliable frame remains owned by this
    /// container until SendAsync succeeds or throws.
    /// </summary>
    public async Task RunSendLoopAsync(CancellationToken cancellationToken = default)
    {
        while (await _outboundSignals.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await SendNextAsync(cancellationToken).ConfigureAwait(false))
            {
            }
        }
    }

    /// <summary>
    /// Reads exactly one complete binary WebSocket message and decodes exactly one
    /// protocol-64 frame from it. A malformed message is reported and ignored once;
    /// a second consecutive invalid message transitions the connection to
    /// ProtocolError and sends a WebSocket protocol-error close.
    /// </summary>
    public async ValueTask<Protocol64WebSocketReceiveResult> ReceiveAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var state = RecoveryState;
        if (state == Protocol64WebSocketRecoveryState.ProtocolError)
        {
            return new(Protocol64WebSocketReceiveStatus.ProtocolError);
        }

        if (state == Protocol64WebSocketRecoveryState.Closed)
        {
            return new(Protocol64WebSocketReceiveStatus.Closed);
        }

        await _receiveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var message = await ReadCompleteMessageAsync(cancellationToken).ConfigureAwait(false);
            if (message.Closed)
            {
                SetRecoveryState(Protocol64WebSocketRecoveryState.Closed);
                return new(Protocol64WebSocketReceiveStatus.Closed);
            }

            if (message.Fault is not null)
            {
                return await HandleMalformedFrameAsync(message.Fault, cancellationToken).ConfigureAwait(false);
            }

            Protocol64FrameDecodeResult decoded;
            try
            {
                decoded = Protocol64FrameCodec.Decode(
                    message.Payload!,
                    _registry,
                    new Protocol64FrameDecodeOptions
                    {
                        Limits = _options.FrameLimits,
                        Backend = _options.BackendName,
                        FaultSink = null,
                    });
            }
            catch (Exception exception)
            {
                var fault = CreateWebSocketFault(
                    Protocol64FaultKind.InvalidBody,
                    "Protocol-64 frame decoding threw unexpectedly.",
                    exception,
                    completeFrameDelivered: true,
                    encodedBodyBytes: message.Payload?.Length ?? 0);
                return await HandleMalformedFrameAsync(fault, cancellationToken).ConfigureAwait(false);
            }

            if (!decoded.Succeeded || decoded.Event is null || decoded.Header is null)
            {
                return await HandleMalformedFrameAsync(
                    decoded.Fault ?? CreateWebSocketFault(
                        Protocol64FaultKind.InvalidBody,
                        "Protocol-64 frame decoding failed without a fault.",
                        completeFrameDelivered: true,
                        encodedBodyBytes: message.Payload?.Length ?? 0),
                    cancellationToken).ConfigureAwait(false);
            }

            Interlocked.Exchange(ref _consecutiveInvalidFrames, 0);
            Interlocked.Exchange(ref _wsRetryCount, 0);
            SetRecoveryState(Protocol64WebSocketRecoveryState.Open);
            Telemetry.RecordFrameReceived(
                decoded.Header!,
                decoded.Schema!.Descriptor.Delivery,
                message.Payload!.Length);
            return new(Protocol64WebSocketReceiveStatus.Frame, decoded, EncodedPayload: message.Payload);
        }
        finally
        {
            _receiveGate.Release();
        }
    }

    public async ValueTask CloseAsync(
        WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure,
        string? statusDescription = null,
        CancellationToken cancellationToken = default)
    {
        if (IsDisposed)
        {
            return;
        }

        SetRecoveryState(Protocol64WebSocketRecoveryState.Closed);
        try
        {
            if (_webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await _webSocket.CloseAsync(closeStatus, statusDescription, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            Dispose();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        SetRecoveryState(Protocol64WebSocketRecoveryState.Closed);
        lock (_outboundGate)
        {
            _reliableQueue.Writer.TryComplete();
            _outboundSignals.Writer.TryComplete();
            _lastWinsMailbox.Clear();
        }

        lock (_socketGate)
        {
            _webSocket.Dispose();
        }

        _receiveGate.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private OutboundFrame EncodeEvent<TEvent>(
        TEvent eventValue,
        Protocol64FrameEncodeOptions? encodeOptions,
        string? lastWinsKey)
    {
        var frameId = unchecked((ulong)Interlocked.Increment(ref _nextFrameId));
        var effectiveOptions = encodeOptions is null
            ? new Protocol64FrameEncodeOptions { Backend = _options.BackendName }
            : encodeOptions with { Backend = _options.BackendName };
        var encoded = Protocol64FrameCodec.Encode(
            _registry,
            eventValue,
            ConnectionEpoch,
            frameId,
            effectiveOptions);

        if (!encoded.Succeeded || encoded.Payload is null || encoded.Header is null)
        {
            throw new Protocol64WebSocketException(
                "Protocol-64 event could not be encoded for WebSocket delivery.",
                fault: encoded.Fault);
        }

        return new(encoded.Payload, encoded.Header, lastWinsKey);
    }

    private OutboundFrame MaterializeEncodedFrame(
        ReadOnlyMemory<byte> encodedFrame,
        string? lastWinsKey,
        Protocol64DeliveryDescriptor? expectedDelivery)
    {
        var payload = encodedFrame.ToArray();
        var decoded = Protocol64FrameCodec.Decode(
            payload,
            _registry,
            new Protocol64FrameDecodeOptions
            {
                Limits = _options.FrameLimits,
                Backend = _options.BackendName,
                FaultSink = null,
            });

        if (!decoded.Succeeded || decoded.Header is null)
        {
            throw new Protocol64WebSocketException(
                "The supplied bytes are not a valid protocol-64 frame.",
                fault: decoded.Fault);
        }

        if (expectedDelivery is { } expected
            && decoded.Schema is not null
            && decoded.Schema.Descriptor.Delivery != expected)
        {
            throw new Protocol64WebSocketException(
                $"The supplied protocol-64 frame delivery {decoded.Schema.Descriptor} does not match the backend operation {expected}.");
        }

        return new(payload, decoded.Header, lastWinsKey);
    }

    private void EnqueueReliable(OutboundFrame frame)
    {
        lock (_outboundGate)
        {
            ThrowIfDisposed();

            var nextCount = Volatile.Read(ref _reliableQueueCount) + 1;
            var nextBytes = Interlocked.Read(ref _reliableQueueBytes) + frame.Payload.Length;
            if (nextCount > _options.MaxReliableQueueFrames ||
                nextBytes > _options.MaxReliableQueueBytes)
            {
                Telemetry.RecordReliableBackpressure();
                var message = $"Protocol-64 reliable WebSocket queue is backpressured " +
                    $"(frames={ReliableQueueCount}/{_options.MaxReliableQueueFrames}, " +
                    $"bytes={ReliableQueueBytes}/{_options.MaxReliableQueueBytes}).";
                ReportWarning(message);
                throw new Protocol64WebSocketBackpressureException(
                    message,
                    ReliableQueueCount,
                    ReliableQueueBytes);
            }

            Interlocked.Increment(ref _reliableQueueCount);
            Interlocked.Add(ref _reliableQueueBytes, frame.Payload.Length);
            if (!_reliableQueue.Writer.TryWrite(frame))
            {
                Interlocked.Decrement(ref _reliableQueueCount);
                Interlocked.Add(ref _reliableQueueBytes, -frame.Payload.Length);
                throw new InvalidOperationException("The protocol-64 reliable WebSocket queue is closed.");
            }

            SignalOutbound(string.Empty);
        }
    }

    private void PublishLastWinsFrame(string key, OutboundFrame frame)
    {
        lock (_outboundGate)
        {
            ThrowIfDisposed();
            if (_lastWinsMailbox.ContainsKey(key))
            {
                Interlocked.Increment(ref _lastWinsReplacementCount);
            }

            _lastWinsMailbox[key] = frame;
            SignalOutbound(key);
        }
    }

    private void SignalOutbound(string signal)
    {
        if (!_outboundSignals.Writer.TryWrite(signal))
        {
            throw new InvalidOperationException("The protocol-64 WebSocket outbound scheduler is closed.");
        }
    }

    private async ValueTask SendFrameAsync(
        OutboundFrame frame,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        WebSocket socket;
        lock (_socketGate)
        {
            socket = _webSocket;
        }

        if (socket.State != WebSocketState.Open)
        {
            throw new WebSocketException($"Cannot send protocol-64 frame while WebSocket state is {socket.State}.");
        }

        try
        {
            await socket.SendAsync(
                new ArraySegment<byte>(frame.Payload),
                WebSocketMessageType.Binary,
                endOfMessage: true,
                cancellationToken).ConfigureAwait(false);
            var delivery = _registry.Get(frame.Header.SchemaId, frame.Header.SchemaRevision).Descriptor.Delivery;
            Telemetry.RecordFrameSent(frame.Header, delivery, frame.Payload.Length);
        }
        catch
        {
            SetRecoveryState(Protocol64WebSocketRecoveryState.Closed);
            throw;
        }
    }

    private async ValueTask<CompleteMessageReadResult> ReadCompleteMessageAsync(
        CancellationToken cancellationToken)
    {
        var buffer = new byte[_options.ReceiveBufferBytes];
        using var message = new MemoryStream(Math.Min(_options.MaxInboundFrameBytes, _options.ReceiveBufferBytes));
        WebSocketMessageType? messageType = null;

        while (true)
        {
            WebSocketReceiveResult result;
            try
            {
                WebSocket socket;
                lock (_socketGate)
                {
                    socket = _webSocket;
                }

                result = await socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (WebSocketException exception)
            {
                SetRecoveryState(Protocol64WebSocketRecoveryState.Closed);
                throw new Protocol64WebSocketException("WebSocket receive failed.", exception);
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return CompleteMessageReadResult.ClosedResult;
            }

            if (messageType is null)
            {
                messageType = result.MessageType;
            }

            if (result.MessageType != WebSocketMessageType.Binary || messageType != WebSocketMessageType.Binary)
            {
                return CompleteMessageReadResult.Faulted(CreateWebSocketFault(
                    Protocol64FaultKind.InvalidEnvelope,
                    "Protocol-64 WebSocket messages must be binary.",
                    completeFrameDelivered: false));
            }

            if (message.Length + result.Count > _options.MaxInboundFrameBytes)
            {
                return CompleteMessageReadResult.Faulted(CreateWebSocketFault(
                    Protocol64FaultKind.OversizedLength,
                    $"Inbound WebSocket message exceeded {_options.MaxInboundFrameBytes} bytes.",
                    completeFrameDelivered: false,
                    encodedBodyBytes: checked((int)Math.Min(int.MaxValue, message.Length + result.Count))));
            }

            message.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                return CompleteMessageReadResult.PayloadResult(message.ToArray());
            }
        }
    }

    private async ValueTask<Protocol64WebSocketReceiveResult> HandleMalformedFrameAsync(
        Protocol64Fault fault,
        CancellationToken cancellationToken)
    {
        ReportFault(fault);

        var invalidCount = Interlocked.Increment(ref _consecutiveInvalidFrames);
        if (invalidCount >= 2)
        {
            SetRecoveryState(Protocol64WebSocketRecoveryState.ProtocolError);
            Telemetry.RecordProtocolErrorDisconnect();
            await TryCloseProtocolErrorAsync(cancellationToken).ConfigureAwait(false);
            return new(Protocol64WebSocketReceiveStatus.ProtocolError, Fault: fault);
        }

        SetRecoveryState(Protocol64WebSocketRecoveryState.Recovering);
        await TryReopenAsync(cancellationToken).ConfigureAwait(false);
        return new(Protocol64WebSocketReceiveStatus.IgnoredMalformedFrame, Fault: fault);
    }

    private async ValueTask TryReopenAsync(CancellationToken cancellationToken)
    {
        if (_options.ReopenAsync is null || Volatile.Read(ref _wsRetryCount) >= _options.MaxWsRetries)
        {
            return;
        }

        Interlocked.Increment(ref _wsRetryCount);
        Telemetry.RecordReconnectAttempt();
        try
        {
            var replacement = await _options.ReopenAsync(cancellationToken).ConfigureAwait(false);
            if (replacement is null)
            {
                return;
            }

            WebSocket previous;
            lock (_socketGate)
            {
                ThrowIfDisposed();
                previous = _webSocket;
                _webSocket = replacement;
            }

            if (!ReferenceEquals(previous, replacement))
            {
                previous.Dispose();
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ReportWarning($"Protocol-64 WebSocket recovery attempt {WsRetryCount} failed: {exception.Message}");
        }
    }

    private async ValueTask TryCloseProtocolErrorAsync(CancellationToken cancellationToken)
    {
        try
        {
            WebSocket socket;
            lock (_socketGate)
            {
                socket = _webSocket;
            }

            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(
                    WebSocketCloseStatus.ProtocolError,
                    "Consecutive invalid protocol-64 frames.",
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ReportWarning($"Protocol-64 WebSocket protocol-error close failed: {exception.Message}");
        }
    }

    private void ReportFault(Protocol64Fault fault)
    {
        Telemetry.RecordDecodeFault(fault);
        try
        {
            _options.FaultCallbackByKind?.Invoke(fault.Kind, fault);
        }
        catch (Exception exception)
        {
            ReportWarning($"Protocol-64 WebSocket fault callback threw for {fault.Kind}: {exception.Message}");
        }

        try
        {
            _options.FaultSink?.Report(fault);
        }
        catch (Exception exception)
        {
            ReportWarning($"Protocol-64 fault sink threw while reporting {fault.Kind}: {exception.Message}");
        }

        ReportWarning($"Protocol-64 WebSocket frame rejected ({fault.Kind}): {fault.Message}");
    }

    private void ReportWarning(string message)
    {
        try
        {
            _options.WarningLogger?.Invoke(message);
        }
        catch
        {
            // A logger must never destabilize the connection state machine.
        }
    }

    private Protocol64Fault CreateWebSocketFault(
        Protocol64FaultKind kind,
        string message,
        Exception? exception = null,
        bool completeFrameDelivered = false,
        int encodedBodyBytes = 0)
        => new(
            kind,
            message,
            new Protocol64FaultMetadata(
                Direction: null,
                SchemaId: null,
                SchemaRevision: null,
                ConnectionEpoch: ConnectionEpoch,
                FrameId: null,
                Backend: _options.BackendName,
                StreamId: null,
                CompleteFrameDelivered: completeFrameDelivered,
                EncodedBodyBytes: encodedBodyBytes,
                DecodedBodyBytes: 0),
            exception);

    private static void ValidateMailboxKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("A LastWins mailbox key is required.", nameof(key));
        }
    }

    private void SetRecoveryState(Protocol64WebSocketRecoveryState state)
        => Volatile.Write(ref _recoveryState, (int)state);

    private void ThrowIfDisposed()
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(Protocol64WebSocketConnection));
        }
    }

    private sealed record OutboundFrame(
        byte[] Payload,
        Protocol64FrameHeader Header,
        string? LastWinsKey);

    private sealed record CompleteMessageReadResult(
        byte[]? Payload,
        Protocol64Fault? Fault,
        bool Closed)
    {
        public static CompleteMessageReadResult ClosedResult { get; } = new(null, null, true);

        public static CompleteMessageReadResult PayloadResult(byte[] payload)
            => new(payload, null, false);

        public static CompleteMessageReadResult Faulted(Protocol64Fault fault)
            => new(null, fault, false);
    }
}

public class Protocol64WebSocketException : InvalidOperationException
{
    public Protocol64WebSocketException(string message, Exception? innerException = null, Protocol64Fault? fault = null)
        : base(message, innerException)
    {
        Fault = fault;
    }

    public Protocol64Fault? Fault { get; }
}

public sealed class Protocol64WebSocketBackpressureException : Protocol64WebSocketException
{
    public Protocol64WebSocketBackpressureException(
        string message,
        int pendingFrames,
        long pendingBytes)
        : base(message)
    {
        PendingFrames = pendingFrames;
        PendingBytes = pendingBytes;
    }

    public int PendingFrames { get; }

    public long PendingBytes { get; }
}
