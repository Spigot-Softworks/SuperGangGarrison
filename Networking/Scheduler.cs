using OpenGarrison.Protocol;

namespace OpenGarrison.Networking;

public sealed record Protocol64ChannelSchedulerOptions
{
    public int ReliableUnorderedLaneCount { get; init; } = 4;

    public int MaxPendingReliableFrames { get; init; } = 4096;

    public long MaxPendingReliableBytes { get; init; } = 16 * 1024 * 1024;

    public int MaxPendingLastWinsFrames { get; init; } = 256;

    public long MaxPendingLastWinsBytes { get; init; } = 16 * 1024 * 1024;
}

/// <summary>
/// Backend-neutral outbound scheduling. Reliable frames are never silently
/// discarded: capacity returns Backpressured and leaves every accepted frame in
/// its lane. LastWins frames use a keyed mailbox, so an older pending state can
/// be replaced by a newer one without growing the queue.
/// </summary>
public sealed class Protocol64ChannelScheduler
{
    // QUIC has a simulation-thread producer and an I/O-thread consumer. The
    // original scheduler was written as a single-threaded primitive, but the
    // native QUIC backend shares it across those two lifetimes. Keep the
    // scheduler as the ownership boundary so callers cannot concurrently
    // mutate one of the dictionaries while TryDequeue/GetActiveStreams is
    // enumerating it.
    private readonly object _gate = new();
    private readonly Protocol64ChannelSchedulerOptions _options;
    private readonly Dictionary<ChannelType, Queue<PendingFrame>> _ordered = [];
    private readonly Dictionary<(ChannelType Channel, int Lane), Queue<PendingFrame>> _unordered = [];
    private readonly Dictionary<(ChannelType Channel, string Key), PendingFrame> _lastWins = [];
    private readonly Dictionary<Protocol64StreamKey, ulong> _nextStreamSequences = [];
    private ulong _nextSchedulerSequence = 1;
    private int _nextUnorderedLane;
    private int _pendingReliableFrames;
    private long _pendingReliableBytes;
    private int _pendingLastWinsFrames;
    private long _pendingLastWinsBytes;

    public Protocol64ChannelScheduler(Protocol64ChannelSchedulerOptions? options = null)
    {
        _options = options ?? new Protocol64ChannelSchedulerOptions();
        ValidateOptions(_options);
    }

    public int PendingReliableFrames
    {
        get
        {
            lock (_gate)
            {
                return _pendingReliableFrames;
            }
        }
    }

    public long PendingReliableBytes
    {
        get
        {
            lock (_gate)
            {
                return _pendingReliableBytes;
            }
        }
    }

    public int PendingLastWinsFrames
    {
        get
        {
            lock (_gate)
            {
                return _pendingLastWinsFrames;
            }
        }
    }

    public long PendingLastWinsBytes
    {
        get
        {
            lock (_gate)
            {
                return _pendingLastWinsBytes;
            }
        }
    }

    public int PendingFrames
    {
        get
        {
            lock (_gate)
            {
                return _pendingReliableFrames + _pendingLastWinsFrames;
            }
        }
    }

    public ConnectionSendResult Enqueue(Protocol64OutboundFrame frame)
    {
        lock (_gate)
        {
            ArgumentNullException.ThrowIfNull(frame);

            var validationFault = Protocol64ConnectionFrameValidation.ValidateOutbound(frame);
            if (validationFault is not null)
            {
                return ConnectionSendResult.Rejected(validationFault);
            }

            var effectiveChannel = frame.EffectiveChannel;
            var descriptor = frame.Delivery;
            if (descriptor.IsLastWins)
            {
                return EnqueueLastWins(frame, effectiveChannel);
            }

            if (!descriptor.Channel.HasValue)
            {
                return ConnectionSendResult.Rejected(Protocol64ConnectionFrameValidation.Fault(
                    Protocol64FaultKind.ValidationFailed,
                    "Reliable protocol-64 delivery must declare a channel."));
            }

            if (_pendingReliableFrames >= _options.MaxPendingReliableFrames ||
                _pendingReliableBytes > _options.MaxPendingReliableBytes - frame.EncodedLength)
            {
                return new ConnectionSendResult(
                    ConnectionSendStatus.Backpressured,
                    null,
                    PendingReliableFrames: _pendingReliableFrames,
                    PendingReliableBytes: _pendingReliableBytes);
            }

            var stream = descriptor.Kind == Protocol64DeliveryKind.ReliableOrdered
                ? new Protocol64StreamKey(effectiveChannel, descriptor.Kind, 0)
                : SelectUnorderedStream(effectiveChannel, descriptor.Kind);
            var pending = CreatePending(frame, stream);

            if (descriptor.Kind == Protocol64DeliveryKind.ReliableOrdered)
            {
                if (!_ordered.TryGetValue(effectiveChannel, out var queue))
                {
                    queue = new Queue<PendingFrame>();
                    _ordered.Add(effectiveChannel, queue);
                }

                queue.Enqueue(pending);
            }
            else
            {
                var key = (effectiveChannel, stream.Lane);
                if (!_unordered.TryGetValue(key, out var queue))
                {
                    queue = new Queue<PendingFrame>();
                    _unordered.Add(key, queue);
                }

                queue.Enqueue(pending);
            }

            _pendingReliableFrames++;
            _pendingReliableBytes += frame.EncodedLength;
            return Queued(ConnectionSendStatus.Queued, stream);
        }
    }

    public bool TryDequeue(out Protocol64ScheduledFrame frame)
    {
        lock (_gate)
        {
            PendingFrame? selected = null;
            Action? remove = null;

            foreach (var pair in _ordered)
            {
                if (pair.Value.Count == 0)
                {
                    continue;
                }

                Consider(pair.Value.Peek(), ref selected, ref remove, () => pair.Value.Dequeue());
            }

            foreach (var pair in _unordered)
            {
                if (pair.Value.Count == 0)
                {
                    continue;
                }

                Consider(pair.Value.Peek(), ref selected, ref remove, () => pair.Value.Dequeue());
            }

            foreach (var pair in _lastWins)
            {
                Consider(pair.Value, ref selected, ref remove, () => _lastWins.Remove(pair.Key));
            }

            if (selected is null || remove is null)
            {
                frame = null!;
                return false;
            }

            remove();
            if (selected.Frame.Delivery.IsReliable)
            {
                _pendingReliableFrames--;
                _pendingReliableBytes -= selected.Frame.EncodedLength;
            }
            else
            {
                _pendingLastWinsFrames--;
                _pendingLastWinsBytes -= selected.Frame.EncodedLength;
            }

            frame = new Protocol64ScheduledFrame(
                selected.Frame,
                selected.Stream,
                selected.StreamSequence,
                selected.SchedulerSequence);
            return true;
        }
    }

    public IReadOnlyList<Protocol64StreamKey> GetActiveStreams()
    {
        lock (_gate)
        {
            var streams = new HashSet<Protocol64StreamKey>();
            foreach (var pair in _ordered)
            {
                if (pair.Value.Count > 0)
                {
                    streams.Add(new Protocol64StreamKey(pair.Key, Protocol64DeliveryKind.ReliableOrdered, 0));
                }
            }

            foreach (var pair in _unordered)
            {
                if (pair.Value.Count > 0)
                {
                    streams.Add(new Protocol64StreamKey(pair.Key.Channel, Protocol64DeliveryKind.ReliableUnordered, pair.Key.Lane));
                }
            }

            foreach (var pair in _lastWins)
            {
                if (pair.Value is not null)
                {
                    streams.Add(pair.Value.Stream);
                }
            }

            return streams.OrderBy(stream => stream.Channel).ThenBy(stream => stream.Delivery).ThenBy(stream => stream.Lane).ToArray();
        }
    }

    private ConnectionSendResult EnqueueLastWins(
        Protocol64OutboundFrame frame,
        ChannelType effectiveChannel)
    {
        var key = (effectiveChannel, frame.ReplacementKey ?? string.Empty);
        if (_lastWins.TryGetValue(key, out var existing))
        {
            var incomingOrder = GetStateOrder(frame, _nextSchedulerSequence);
            var existingOrder = GetStateOrder(existing.Frame, existing.SchedulerSequence);
            if (incomingOrder <= existingOrder)
            {
                return new ConnectionSendResult(
                    ConnectionSendStatus.IgnoredStale,
                    existing.Stream,
                    PendingReliableFrames: _pendingReliableFrames,
                    PendingReliableBytes: _pendingReliableBytes);
            }

            var replacementDelta = frame.EncodedLength - existing.Frame.EncodedLength;
            if (_pendingLastWinsBytes > _options.MaxPendingLastWinsBytes - replacementDelta)
            {
                return new ConnectionSendResult(
                    ConnectionSendStatus.Backpressured,
                    existing.Stream,
                    PendingReliableFrames: _pendingReliableFrames,
                    PendingReliableBytes: _pendingReliableBytes);
            }

            var replacement = CreatePending(
                frame,
                existing.Stream,
                GetStateOrder(frame, _nextSchedulerSequence));
            _lastWins[key] = replacement;
            _pendingLastWinsBytes += replacementDelta;
            return Queued(ConnectionSendStatus.Replaced, replacement.Stream);
        }

        if (_pendingLastWinsFrames >= _options.MaxPendingLastWinsFrames ||
            _pendingLastWinsBytes > _options.MaxPendingLastWinsBytes - frame.EncodedLength)
        {
            return new ConnectionSendResult(
                ConnectionSendStatus.Backpressured,
                null,
                PendingReliableFrames: _pendingReliableFrames,
                PendingReliableBytes: _pendingReliableBytes);
        }

        var stream = new Protocol64StreamKey(effectiveChannel, Protocol64DeliveryKind.LastWins, 0);
        var pending = CreatePending(frame, stream);
        _lastWins.Add(key, pending);
        _pendingLastWinsFrames++;
        _pendingLastWinsBytes += frame.EncodedLength;
        return Queued(ConnectionSendStatus.Queued, stream);
    }

    private Protocol64StreamKey SelectUnorderedStream(
        ChannelType channel,
        Protocol64DeliveryKind delivery)
    {
        var selectedLane = 0;
        var selectedCount = int.MaxValue;
        for (var offset = 0; offset < _options.ReliableUnorderedLaneCount; offset++)
        {
            var lane = (_nextUnorderedLane + offset) % _options.ReliableUnorderedLaneCount;
            var count = _unordered.TryGetValue((channel, lane), out var queue)
                ? queue.Count
                : 0;
            if (count < selectedCount)
            {
                selectedLane = lane;
                selectedCount = count;
            }
        }

        _nextUnorderedLane = (selectedLane + 1) % _options.ReliableUnorderedLaneCount;
        return new Protocol64StreamKey(channel, delivery, selectedLane);
    }

    private PendingFrame CreatePending(
        Protocol64OutboundFrame frame,
        Protocol64StreamKey stream,
        ulong? stateOrder = null)
    {
        var schedulerSequence = _nextSchedulerSequence++;
        var streamSequence = NextStreamSequence(stream);
        return new PendingFrame(
            frame,
            stream,
            streamSequence,
            schedulerSequence,
            stateOrder ?? GetStateOrder(frame, schedulerSequence));
    }

    private ulong NextStreamSequence(Protocol64StreamKey stream)
    {
        if (!_nextStreamSequences.TryGetValue(stream, out var next))
        {
            next = 1;
        }

        _nextStreamSequences[stream] = checked(next + 1);
        return next;
    }

    private ConnectionSendResult Queued(
        ConnectionSendStatus status,
        Protocol64StreamKey stream)
        => new(
            status,
            stream,
            PendingReliableFrames: _pendingReliableFrames,
            PendingReliableBytes: _pendingReliableBytes);

    private static ulong GetStateOrder(Protocol64OutboundFrame frame, ulong fallback)
        => frame.Header.FrameId == 0 ? fallback : frame.Header.FrameId;

    private static void Consider(
        PendingFrame candidate,
        ref PendingFrame? selected,
        ref Action? remove,
        Action candidateRemoval)
    {
        if (selected is null || candidate.SchedulerSequence < selected.SchedulerSequence)
        {
            selected = candidate;
            remove = candidateRemoval;
        }
    }

    private static void ValidateOptions(Protocol64ChannelSchedulerOptions options)
    {
        if (options.ReliableUnorderedLaneCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "At least one unordered lane is required.");
        }

        if (options.MaxPendingReliableFrames <= 0 || options.MaxPendingReliableBytes <= 0 ||
            options.MaxPendingLastWinsFrames <= 0 || options.MaxPendingLastWinsBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Scheduler limits must be positive.");
        }
    }

    private sealed record PendingFrame(
        Protocol64OutboundFrame Frame,
        Protocol64StreamKey Stream,
        ulong StreamSequence,
        ulong SchedulerSequence,
        ulong StateOrder);
}
