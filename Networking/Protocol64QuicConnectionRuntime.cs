using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Quic;
using OpenGarrison.Protocol;

namespace OpenGarrison.Networking;

/// <summary>
/// Native QUIC I/O loop for protocol 64. The logical container remains the
/// owner of delivery semantics; this type owns QUIC stream lifetime, complete
/// frame reads/writes, and the stream-fault bridge back into that container.
/// </summary>
public sealed class Protocol64QuicConnectionRuntime : IAsyncDisposable
{
    private readonly QuicConnection _connection;
    private readonly Protocol64QuicConnectionContainer _container;
    private readonly Protocol64SchemaRegistry _registry;
    private readonly Protocol64QuicRuntimeOptions _options;
    private readonly ConcurrentDictionary<(Protocol64StreamKey Stream, int Generation), QuicStreamHandle> _outboundStreams = new();
    private readonly ConcurrentDictionary<int, ulong> _inboundSequences = new();
    private readonly ConcurrentDictionary<int, InboundRecoveryBinding> _inboundRecoveryBindings = new();
    private readonly Dictionary<(int StreamId, ulong Sequence), ReplayFrame> _replayFrames = [];
    private readonly Queue<(int StreamId, ulong Sequence)> _replayFrameOrder = new();
    private readonly object _replayGate = new();
    private readonly SemaphoreSlim _outboundSignal = new(0);
    private readonly object _lifecycleGate = new();
    private CancellationTokenSource? _runCts;
    private Task? _runTask;
    private long _nextFrameId;
    private int _disposed;

    public Protocol64QuicConnectionRuntime(
        QuicConnection connection,
        Protocol64QuicConnectionContainer container,
        Protocol64SchemaRegistry registry,
        Protocol64QuicRuntimeOptions? options = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _options = options ?? new Protocol64QuicRuntimeOptions();
        if (_options.FrameLimits is null)
        {
            throw new ArgumentException("FrameLimits cannot be null.", nameof(options));
        }

        if (_options.ReceiveBufferBytes < Protocol64FrameHeader.EncodedSize)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "ReceiveBufferBytes must fit a protocol-64 header.");
        }
    }

    public ulong ConnectionEpoch => _container.ConnectionEpoch;

    public Protocol64QuicConnectionContainer Container => _container;

    public Protocol64NetworkTelemetry Telemetry => _container.Telemetry;

    public bool IsRunning
    {
        get
        {
            lock (_lifecycleGate)
            {
                return _runTask is { IsCompleted: false };
            }
        }
    }

    public ConnectionSendResult EnqueueEvent(object eventValue, string? replacementKey = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(eventValue);

        var frameId = unchecked((ulong)Interlocked.Increment(ref _nextFrameId));
        var encoded = Protocol64FrameCodec.EncodeObject(
            _registry,
            eventValue,
            ConnectionEpoch,
            frameId,
            new Protocol64FrameEncodeOptions
            {
                Backend = "quic",
                Limits = _options.FrameLimits,
            });
        if (!encoded.Succeeded || encoded.Payload is null || encoded.Header is null)
        {
            return ConnectionSendResult.Rejected(
                encoded.Fault ?? new Protocol64Fault(
                    Protocol64FaultKind.InvalidBody,
                    "Protocol-64 QUIC event encoding failed without a typed fault.",
                    Protocol64FaultMetadata.Empty));
        }

        var schema = _registry.Get(encoded.Header.SchemaId, encoded.Header.SchemaRevision);
        var frame = new Protocol64OutboundFrame(
            encoded.Payload,
            encoded.Header,
            schema.Descriptor.Delivery,
            replacementKey);
        var result = _container.EnqueueSend(frame);
        if (result.Accepted)
        {
            SignalOutbound();
        }

        return result;
    }

    /// <summary>
    /// Enqueues an already encoded, complete protocol-64 frame for the native
    /// QUIC connection. This is the backend handoff used by the server's
    /// protocol-aware outbound dispatcher; the frame is revalidated here so a
    /// caller cannot bypass schema delivery metadata or envelope limits.
    /// </summary>
    public ConnectionSendResult EnqueueFrame(ReadOnlyMemory<byte> encodedPayload, string? replacementKey = null)
    {
        ThrowIfDisposed();
        var decoded = Protocol64FrameCodec.Decode(
            encodedPayload,
            _registry,
            new Protocol64FrameDecodeOptions
            {
                Backend = "quic",
                Limits = _options.FrameLimits,
                FaultSink = null,
            });
        if (!decoded.Succeeded || decoded.Header is null || decoded.Schema is null)
        {
            return ConnectionSendResult.Rejected(
                decoded.Fault ?? new Protocol64Fault(
                    Protocol64FaultKind.InvalidBody,
                    "Protocol-64 QUIC outbound frame validation failed without a typed fault.",
                    Protocol64FaultMetadata.Empty));
        }

        var result = _container.EnqueueSend(new Protocol64OutboundFrame(
            encodedPayload.ToArray(),
            decoded.Header,
            decoded.Schema.Descriptor.Delivery,
            replacementKey));
        if (result.Accepted)
        {
            SignalOutbound();
        }

        return result;
    }

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        lock (_lifecycleGate)
        {
            if (_runTask is { IsCompleted: false })
            {
                return _runTask;
            }

            _runCts?.Dispose();
            _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _runTask = RunCoreAsync(_runCts.Token);
            return _runTask;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        CancellationTokenSource? runCts;
        Task? runTask;
        lock (_lifecycleGate)
        {
            runCts = _runCts;
            runTask = _runTask;
            _runCts = null;
        }

        runCts?.Cancel();
        if (runTask is not null)
        {
            try
            {
                await runTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        foreach (var stream in _outboundStreams.Values)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }

        _container.Close();
        _outboundSignal.Dispose();
        runCts?.Dispose();
        await _connection.DisposeAsync().ConfigureAwait(false);
    }

    private async Task RunCoreAsync(CancellationToken cancellationToken)
    {
        using var loopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var loopToken = loopCts.Token;
        var outbound = RunOutboundLoopAsync(loopToken);
        var inbound = RunInboundAcceptLoopAsync(loopToken);
        try
        {
            await Task.WhenAny(outbound, inbound).ConfigureAwait(false);
            loopCts.Cancel();
            if (!cancellationToken.IsCancellationRequested)
            {
                _options.WarningLogger?.Invoke("Protocol-64 QUIC connection loop ended; cancelling its peer loop.");
            }
        }
        finally
        {
            try
            {
                await Task.WhenAll(outbound, inbound).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (loopToken.IsCancellationRequested)
            {
            }
        }
    }

    private async Task RunOutboundLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_container.TryDequeueSend(out var scheduled, out var selection))
            {
                await _outboundSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (selection.IsDatagram)
            {
                _options.WarningLogger?.Invoke("Protocol-64 QUIC datagram lane is using its reliable stream fallback.");
                selection = selection with
                {
                    Role = Protocol64QuicStreamRole.LastWinsFallback,
                    IsDatagram = false,
                    IsFallback = true,
                    StreamId = 0,
                };
            }

            var handle = await GetOrOpenStreamAsync(selection, cancellationToken).ConfigureAwait(false);
            QuicStreamHandle? failedHandle = null;
            await handle.WriteGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await handle.Stream.WriteAsync(scheduled.EncodedPayload, cancellationToken).ConfigureAwait(false);
                Telemetry.RecordFrameSent(
                    scheduled.Header,
                    scheduled.Frame.Delivery,
                    scheduled.EncodedPayload.Length);
                RememberReplayFrame(scheduled, selection);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                if (_outboundStreams.TryRemove((selection.Stream, selection.Generation), out var removedHandle))
                {
                    failedHandle = removedHandle;
                }

                await HandleTransportFaultAsync(
                    new Protocol64TransportFault(
                        Protocol64TransportFaultKind.WriteFailed,
                        Protocol64TransportFaultScope.Stream,
                        "QUIC stream write failed.",
                        ConnectionEpoch,
                        selection.StreamId,
                        selection.Channel,
                        selection.Delivery,
                        scheduled.StreamSequence,
                        scheduled.Header.FrameId,
                        CompleteFrameDelivered: false,
                        Exception: exception),
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                handle.WriteGate.Release();
            }

            if (failedHandle is not null)
            {
                await failedHandle.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task RunInboundAcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            QuicStream stream;
            try
            {
                stream = await _connection.AcceptInboundStreamAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await HandleTransportFaultAsync(
                    new Protocol64TransportFault(
                        Protocol64TransportFaultKind.ReadFailed,
                        Protocol64TransportFaultScope.Connection,
                        "QUIC inbound stream acceptance failed.",
                        ConnectionEpoch,
                        null,
                        null,
                        null,
                        null,
                        null,
                        CompleteFrameDelivered: false,
                        Exception: exception),
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            _ = RunInboundStreamAsync(stream, cancellationToken);
        }
    }

    private async Task RunInboundStreamAsync(QuicStream stream, CancellationToken cancellationToken)
    {
        var streamId = checked((int)stream.Id);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var payload = await ReadFrameAsync(stream, cancellationToken).ConfigureAwait(false);
                if (payload is null)
                {
                    await HandleTransportFaultAsync(
                        CreateStreamFault(
                            Protocol64TransportFaultKind.StreamClosed,
                            streamId,
                            "QUIC stream closed between complete protocol-64 frames."),
                        cancellationToken).ConfigureAwait(false);
                    return;
                }

                var decoded = Protocol64FrameCodec.Decode(
                    payload,
                    _registry,
                    new Protocol64FrameDecodeOptions
                    {
                        Backend = "quic",
                        StreamId = streamId,
                        Limits = _options.FrameLimits,
                        ExpectedDirection = _options.ExpectedInboundDirection,
                        FaultSink = new DelegateProtocol64FaultSink(fault =>
                            _options.FaultSink?.Report(fault)),
                    });
                if (!decoded.Succeeded || decoded.Event is null || decoded.Header is null || decoded.Schema is null)
                {
                    var fault = decoded.Fault ?? new Protocol64Fault(
                        Protocol64FaultKind.InvalidBody,
                        "Protocol-64 QUIC frame decoding failed without a typed fault.",
                        Protocol64FaultMetadata.Empty);
                    await HandleTransportFaultAsync(
                        new Protocol64TransportFault(
                            Protocol64TransportFaultKind.InvalidFrame,
                            Protocol64TransportFaultScope.Stream,
                            "QUIC delivered a complete frame that protocol 64 rejected.",
                            ConnectionEpoch,
                            streamId,
                            decoded.Schema?.Descriptor.Delivery.Channel,
                            decoded.Schema?.Descriptor.Delivery.Kind,
                            NextInboundSequence(streamId),
                            decoded.Header?.FrameId,
                            CompleteFrameDelivered: true,
                            ProtocolFault: fault),
                        cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (decoded.Event is Protocol64RetransmitResponse retransmitResponse)
                {
                    RegisterInboundRecoveryBinding(retransmitResponse);
                    continue;
                }

                if (decoded.Event is Protocol64RetransmitRequest retransmitRequest)
                {
                    await ReplayRequestedFramesAsync(retransmitRequest, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var hasRecoveryBinding = _inboundRecoveryBindings.TryGetValue(streamId, out var recoveryBinding);
                var sequence = hasRecoveryBinding
                    ? recoveryBinding!.TakeSequence()
                    : NextInboundSequence(streamId);
                var lane = hasRecoveryBinding ? recoveryBinding!.Lane : 0;
                var received = new Protocol64ReceivedFrame(
                    payload,
                    decoded.Header,
                    decoded.Schema.Descriptor.Delivery,
                    streamId,
                    lane,
                    sequence);
                var accepted = _container.AcceptReceived(received);
                foreach (var released in accepted.ReleasedFrames)
                {
                    _options.FrameReceived?.Invoke(released);
                }

                // LastWins frames are held by the semantic receive scheduler
                // until the newest value for a replacement key is selected.
                // They therefore do not appear in ReleasedFrames from
                // AcceptReceived. Drain that mailbox here or QUIC state frames
                // (including player snapshots) can be accepted forever without
                // ever reaching the game client.
                while (_container.TryDequeueReceived(out var pending))
                {
                    _options.FrameReceived?.Invoke(pending);
                }

                if (accepted.Fault is not null)
                {
                    await HandleTransportFaultAsync(accepted.Fault, cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (hasRecoveryBinding)
                {
                    if (recoveryBinding!.IsComplete)
                    {
                        _container.MarkRepairCompleted(recoveryBinding.RequestId, streamId);
                        _inboundRecoveryBindings.TryRemove(streamId, out _);
                    }
                }
                else
                {
                    CompleteMatchingRepair(streamId, isRecoveryControlFrame: false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Protocol64QuicFrameException exception)
        {
            await HandleTransportFaultAsync(
                new Protocol64TransportFault(
                    Protocol64TransportFaultKind.InvalidFrame,
                    Protocol64TransportFaultScope.Stream,
                    exception.Message,
                    ConnectionEpoch,
                    streamId,
                    ChannelType.State,
                    Protocol64DeliveryKind.ReliableUnordered,
                    _inboundSequences.TryGetValue(streamId, out var sequence) ? sequence : null,
                    null,
                    CompleteFrameDelivered: false,
                    ProtocolFault: new Protocol64Fault(
                        exception.Kind,
                        exception.Message,
                        new Protocol64FaultMetadata(
                            _options.ExpectedInboundDirection,
                            null,
                            null,
                            ConnectionEpoch,
                            null,
                            "quic",
                            streamId,
                            false,
                            0,
                            0),
                        exception)),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await HandleTransportFaultAsync(
                CreateStreamFault(
                    Protocol64TransportFaultKind.ReadFailed,
                    streamId,
                    "QUIC stream read failed.",
                    exception),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async ValueTask<QuicStreamHandle> GetOrOpenStreamAsync(
        Protocol64QuicStreamSelection selection,
        CancellationToken cancellationToken)
    {
        var streamKey = (selection.Stream, selection.Generation);
        if (_outboundStreams.TryGetValue(streamKey, out var existing))
        {
            return existing;
        }

        var stream = await _connection.OpenOutboundStreamAsync(
            selection.StreamType,
            cancellationToken).ConfigureAwait(false);
        var created = new QuicStreamHandle(stream);
        if (_outboundStreams.TryAdd(streamKey, created))
        {
            return created;
        }

        await created.DisposeAsync().ConfigureAwait(false);
        return _outboundStreams[streamKey];
    }

    private async ValueTask HandleTransportFaultAsync(
        Protocol64TransportFault fault,
        CancellationToken cancellationToken)
    {
        var protocolFault = fault.ProtocolFault ?? new Protocol64Fault(
            Protocol64FaultKind.InvalidEnvelope,
            fault.Message,
            new Protocol64FaultMetadata(
                null,
                null,
                null,
                fault.ConnectionEpoch,
                fault.FrameId,
                "quic",
                fault.StreamId,
                fault.CompleteFrameDelivered,
            0,
            0),
            fault.Exception);
        try
        {
            _options.FaultCallbackByKind?.Invoke(protocolFault.Kind, protocolFault);
        }
        catch (Exception exception)
        {
            _options.WarningLogger?.Invoke($"Protocol-64 QUIC fault callback threw for {protocolFault.Kind}: {exception.Message}");
        }

        try
        {
            _options.FaultSink?.Report(protocolFault);
        }
        catch (Exception exception)
        {
            _options.WarningLogger?.Invoke($"Protocol-64 QUIC fault sink threw for {protocolFault.Kind}: {exception.Message}");
        }

        var recovery = _container.ReportTransportFault(fault);
        if (recovery.RequiresDisconnect)
        {
            _options.WarningLogger?.Invoke($"Protocol-64 QUIC connection entered protocol error: {fault.Message}");
            return;
        }

        if (recovery.RepairRequest is not { } repair)
        {
            return;
        }

        _options.RepairRequested?.Invoke(repair);
        var result = EnqueueEvent(repair.ToProtocolEvent());
        if (!result.Accepted)
        {
            _options.WarningLogger?.Invoke("Protocol-64 QUIC could not enqueue its Control-stream repair request.");
            return;
        }

        foreach (var plan in _container.PendingRetransmitPlans.Where(plan => plan.PlanId == repair.RequestId))
        {
            await GetOrOpenStreamAsync(plan.ReopenedStream, cancellationToken).ConfigureAwait(false);
            await GetOrOpenStreamAsync(plan.DedicatedStream, cancellationToken).ConfigureAwait(false);
        }
    }

    private void CompleteMatchingRepair(int streamId)
    {
        foreach (var plan in _container.PendingRetransmitPlans)
        {
            if (plan.ReopenedStream.StreamId == streamId || plan.DedicatedStream.StreamId == streamId)
            {
                _container.MarkRepairCompleted(plan.PlanId, streamId);
            }
        }
    }

    private void CompleteMatchingRepair(int streamId, bool isRecoveryControlFrame)
    {
        if (isRecoveryControlFrame)
        {
            return;
        }

        CompleteMatchingRepair(streamId);
    }

    private void RegisterInboundRecoveryBinding(Protocol64RetransmitResponse response)
    {
        if (response.ConnectionEpoch != ConnectionEpoch)
        {
            _options.WarningLogger?.Invoke(
                $"Protocol-64 QUIC retransmit response epoch {response.ConnectionEpoch} does not match {ConnectionEpoch}.");
            return;
        }

        if (!response.Available)
        {
            _options.WarningLogger?.Invoke(
                $"Protocol-64 QUIC peer could not satisfy retransmit request {response.RequestId}.");
            return;
        }

        _inboundRecoveryBindings[response.RecoveryStreamId] = new InboundRecoveryBinding(
            response.RequestId,
            response.Channel,
            response.Delivery,
            response.Lane,
            response.SequenceFrom,
            response.SequenceTo);
    }

    private async ValueTask ReplayRequestedFramesAsync(
        Protocol64RetransmitRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ConnectionEpoch != ConnectionEpoch)
        {
            _options.WarningLogger?.Invoke(
                $"Protocol-64 QUIC retransmit request epoch {request.ConnectionEpoch} does not match {ConnectionEpoch}.");
            return;
        }

        var frames = FindReplayFrames(request);
        if (frames.Count == 0)
        {
            _options.WarningLogger?.Invoke(
                $"Protocol-64 QUIC has no cached frame for retransmit request {request.RequestId}; state repair may be required.");
            return;
        }

        var first = frames[0];
        var last = frames[^1];
        var recoveryStream = _container.AllocatePeerRetransmitStream(
            first.Stream.Channel,
            first.Stream.Delivery,
            first.Stream.Lane);
        var response = new Protocol64RetransmitResponse(
            request.RequestId,
            ConnectionEpoch,
            Available: true,
            recoveryStream.StreamId,
            first.Stream.Channel,
            first.Stream.Delivery,
            first.Stream.Lane,
            first.StreamSequence,
            last.StreamSequence);
        var encodedResponse = Protocol64FrameCodec.EncodeObject(
            _registry,
            response,
            ConnectionEpoch,
            unchecked((ulong)Interlocked.Increment(ref _nextFrameId)),
            new Protocol64FrameEncodeOptions
            {
                Backend = "quic",
                Limits = _options.FrameLimits,
            });
        if (!encodedResponse.Succeeded || encodedResponse.Payload is null || encodedResponse.Header is null)
        {
            _options.WarningLogger?.Invoke(
                $"Protocol-64 QUIC could not encode retransmit response {request.RequestId}: {encodedResponse.Fault?.Message}");
            return;
        }

        // The response is the first complete frame on the dedicated stream.
        // This avoids a race where the replacement stream arrives before a
        // control-stream metadata frame can teach the receiver its mapping.
        await SendRawFrameAsync(
            recoveryStream,
            encodedResponse.Payload,
            encodedResponse.Header,
            _registry.Get(encodedResponse.Header.SchemaId, encodedResponse.Header.SchemaRevision).Descriptor.Delivery,
            cancellationToken).ConfigureAwait(false);
        foreach (var frame in frames)
        {
            await SendRawFrameAsync(
                recoveryStream,
                frame.Payload,
                frame.Header,
                frame.Delivery,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private IReadOnlyList<ReplayFrame> FindReplayFrames(Protocol64RetransmitRequest request)
    {
        lock (_replayGate)
        {
            var from = request.MissingSequenceFrom ?? 0;
            var to = request.MissingSequenceTo ?? ulong.MaxValue;
            return _replayFrames.Values
                .Where(frame => frame.StreamId == request.StreamId
                    && frame.Stream.Channel == request.Channel
                    && frame.Stream.Delivery == request.Delivery
                    && frame.Stream.Lane == request.Lane
                    && frame.StreamSequence >= from
                    && frame.StreamSequence <= to
                    && (!request.ExcludeOffendingFrame
                        || request.OffendingFrameId is not ulong offending
                        || frame.Header.FrameId != offending))
                .OrderBy(frame => frame.StreamSequence)
                .ToArray();
        }
    }

    private void RememberReplayFrame(
        Protocol64ScheduledFrame scheduled,
        Protocol64QuicStreamSelection selection)
    {
        if (!scheduled.Frame.Delivery.IsReliable || selection.IsDatagram || selection.StreamId < 0)
        {
            return;
        }

        var replay = new ReplayFrame(
            scheduled.EncodedPayload.ToArray(),
            scheduled.Header,
            scheduled.Frame.Delivery,
            selection.Stream,
            selection.StreamId,
            scheduled.StreamSequence);
        lock (_replayGate)
        {
            var key = (selection.StreamId, scheduled.StreamSequence);
            if (_replayFrames.ContainsKey(key))
            {
                return;
            }

            _replayFrames[key] = replay;
            _replayFrameOrder.Enqueue(key);
            var maxFrames = Math.Max(1, _options.MaxReplayCacheFrames);
            var maxBytes = Math.Max(1L, _options.MaxReplayCacheBytes);
            while (_replayFrames.Count > maxFrames
                || _replayFrames.Values.Sum(frame => (long)frame.Payload.Length) > maxBytes)
            {
                if (!_replayFrameOrder.TryDequeue(out var oldest))
                {
                    break;
                }

                _replayFrames.Remove(oldest);
            }
        }
    }

    private async ValueTask SendRawFrameAsync(
        Protocol64QuicStreamSelection selection,
        ReadOnlyMemory<byte> payload,
        Protocol64FrameHeader header,
        Protocol64DeliveryDescriptor delivery,
        CancellationToken cancellationToken)
    {
        var handle = await GetOrOpenStreamAsync(selection, cancellationToken).ConfigureAwait(false);
        await handle.WriteGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await handle.Stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            Telemetry.RecordFrameSent(header, delivery, payload.Length);
        }
        finally
        {
            handle.WriteGate.Release();
        }
    }

    private async ValueTask<byte[]?> ReadFrameAsync(
        QuicStream stream,
        CancellationToken cancellationToken)
    {
        var header = new byte[Protocol64FrameHeader.EncodedSize];
        var headerBytes = await ReadExactAsync(stream, header, cancellationToken).ConfigureAwait(false);
        if (headerBytes == 0)
        {
            return null;
        }

        if (headerBytes != header.Length || BinaryPrimitives.ReadUInt32LittleEndian(header) != Protocol64FrameHeader.Magic)
        {
            throw new Protocol64QuicFrameException(
                Protocol64FaultKind.TruncatedFrame,
                "QUIC delivered a partial or invalid protocol-64 frame header.");
        }

        var encodedBodyLength = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(28));
        if (encodedBodyLength > _options.FrameLimits.MaxEncodedBodyBytes)
        {
            throw new Protocol64QuicFrameException(
                Protocol64FaultKind.OversizedLength,
                "QUIC protocol-64 frame body exceeds the configured limit.");
        }

        var payload = new byte[checked(Protocol64FrameHeader.EncodedSize + (int)encodedBodyLength)];
        header.CopyTo(payload, 0);
        var bodyBytes = await ReadExactAsync(
            stream,
            payload.AsMemory(Protocol64FrameHeader.EncodedSize),
            cancellationToken).ConfigureAwait(false);
        if (bodyBytes != encodedBodyLength)
        {
            throw new Protocol64QuicFrameException(
                Protocol64FaultKind.TruncatedFrame,
                "QUIC stream ended before the complete protocol-64 frame body arrived.");
        }

        return payload;
    }

    private static async ValueTask<int> ReadExactAsync(
        QuicStream stream,
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < destination.Length)
        {
            var read = await stream.ReadAsync(destination[total..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return total;
            }

            total += read;
        }

        return total;
    }

    private ulong NextInboundSequence(int streamId)
        => _inboundSequences.AddOrUpdate(streamId, 1, static (_, current) => checked(current + 1));

    private Protocol64TransportFault CreateStreamFault(
        Protocol64TransportFaultKind kind,
        int streamId,
        string message,
        Exception? exception = null)
        => new(
            kind,
            Protocol64TransportFaultScope.Stream,
            message,
            ConnectionEpoch,
            streamId,
            ChannelType.State,
            Protocol64DeliveryKind.ReliableUnordered,
            _inboundSequences.TryGetValue(streamId, out var sequence) ? sequence : null,
            null,
            CompleteFrameDelivered: false,
            Exception: exception);

    private void SignalOutbound()
    {
        try
        {
            _outboundSignal.Release();
        }
        catch (SemaphoreFullException)
        {
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(Protocol64QuicConnectionRuntime));
        }
    }

    private sealed class QuicStreamHandle(QuicStream stream) : IAsyncDisposable
    {
        public QuicStream Stream { get; } = stream;

        public SemaphoreSlim WriteGate { get; } = new(1, 1);

        public async ValueTask DisposeAsync()
        {
            WriteGate.Dispose();
            await Stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class InboundRecoveryBinding(
        Guid requestId,
        ChannelType channel,
        Protocol64DeliveryKind delivery,
        int lane,
        ulong sequenceFrom,
        ulong sequenceTo)
    {
        private readonly object _gate = new();
        private ulong _nextSequence = sequenceFrom;

        public Guid RequestId { get; } = requestId;

        public ChannelType Channel { get; } = channel;

        public Protocol64DeliveryKind Delivery { get; } = delivery;

        public int Lane { get; } = lane;

        public bool IsComplete
        {
            get
            {
                lock (_gate)
                {
                    return _nextSequence > sequenceTo;
                }
            }
        }

        public ulong TakeSequence()
        {
            lock (_gate)
            {
                if (_nextSequence > sequenceTo)
                {
                    throw new Protocol64QuicFrameException(
                        Protocol64FaultKind.ValidationFailed,
                        "QUIC retransmit stream delivered more frames than its declared sequence range.");
                }

                return _nextSequence++;
            }
        }
    }

    private sealed record ReplayFrame(
        byte[] Payload,
        Protocol64FrameHeader Header,
        Protocol64DeliveryDescriptor Delivery,
        Protocol64StreamKey Stream,
        int StreamId,
        ulong StreamSequence);
}

public sealed record Protocol64QuicRuntimeOptions
{
    public Protocol64FrameLimits FrameLimits { get; init; } = Protocol64FrameLimits.Default;

    public int ReceiveBufferBytes { get; init; } = 8 * 1024;

    public Protocol64Direction? ExpectedInboundDirection { get; init; }

    public IProtocol64FaultSink? FaultSink { get; init; }

    public Action<Protocol64FaultKind, Protocol64Fault>? FaultCallbackByKind { get; init; }

    public Action<Protocol64ReceivedFrame>? FrameReceived { get; init; }

    public Action<Protocol64RepairRequest>? RepairRequested { get; init; }

    public int MaxReplayCacheFrames { get; init; } = 2048;

    public long MaxReplayCacheBytes { get; init; } = 16 * 1024 * 1024;

    public Action<string>? WarningLogger { get; init; } =
        static message => Console.Error.WriteLine($"[network] warning: {message}");
}

public sealed class Protocol64QuicFrameException : Exception
{
    public Protocol64QuicFrameException(Protocol64FaultKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }

    public Protocol64FaultKind Kind { get; }
}
