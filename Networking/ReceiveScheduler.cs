using OpenGarrison.Protocol;

namespace OpenGarrison.Networking;

/// <summary>
/// Applies delivery semantics after a backend has delivered a complete frame.
/// It owns only semantic ordering/deduplication; it never parses a transport.
/// </summary>
public sealed class Protocol64ReceiveScheduler
{
    private readonly int _maxPendingReliableFrames;
    private readonly Dictionary<Protocol64StreamKey, OrderedLane> _ordered = [];
    private readonly Dictionary<Protocol64StreamKey, HashSet<ulong>> _seenUnordered = [];
    private readonly Dictionary<(ChannelType Channel, string Key), PendingLastWins> _lastWins = [];
    private ulong _nextArrivalSequence = 1;
    private int _pendingReliableFrames;

    public Protocol64ReceiveScheduler(int maxPendingReliableFrames = 4096)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPendingReliableFrames);

        _maxPendingReliableFrames = maxPendingReliableFrames;
    }

    public int PendingReliableFrames => _pendingReliableFrames;

    public int PendingLastWinsFrames => _lastWins.Count;

    public ConnectionReceiveResult Accept(Protocol64ReceivedFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var validationFault = Protocol64ConnectionFrameValidation.ValidateReceived(frame);
        if (validationFault is not null)
        {
            return new ConnectionReceiveResult(
                ConnectionReceiveStatus.Rejected,
                [],
                Fault: new Protocol64TransportFault(
                    Protocol64TransportFaultKind.InvalidFrame,
                    Protocol64TransportFaultScope.Stream,
                    "The received protocol-64 frame failed container validation.",
                    frame.Header.ConnectionEpoch,
                    frame.StreamId,
                    frame.EffectiveChannel,
                    frame.Delivery.Kind,
                    frame.StreamSequence,
                    frame.Header.FrameId,
                    CompleteFrameDelivered: true,
                    validationFault));
        }

        return frame.Delivery.Kind switch
        {
            Protocol64DeliveryKind.ReliableOrdered => AcceptReliableOrdered(frame),
            Protocol64DeliveryKind.ReliableUnordered => AcceptReliableUnordered(frame),
            Protocol64DeliveryKind.LastWins => AcceptLastWins(frame),
            _ => throw new InvalidOperationException($"Unsupported delivery kind {frame.Delivery.Kind}."),
        };
    }

    public bool TryDequeue(out Protocol64ReceivedFrame frame)
    {
        foreach (var pair in _lastWins.OrderBy(pair => pair.Value.ArrivalSequence).ToArray())
        {
            _lastWins.Remove(pair.Key);
            frame = pair.Value.Frame;
            return true;
        }

        frame = null!;
        return false;
    }

    private ConnectionReceiveResult AcceptReliableOrdered(Protocol64ReceivedFrame frame)
    {
        var stream = GetStreamKey(frame);
        if (!_ordered.TryGetValue(stream, out var lane))
        {
            lane = new OrderedLane(frame.StreamSequence);
            _ordered.Add(stream, lane);
        }

        if (frame.StreamSequence < lane.NextSequence || lane.Pending.ContainsKey(frame.StreamSequence))
        {
            return new ConnectionReceiveResult(ConnectionReceiveStatus.Duplicate, []);
        }

        if (frame.StreamSequence > lane.NextSequence)
        {
            if (_pendingReliableFrames >= _maxPendingReliableFrames)
            {
                var fault = new Protocol64TransportFault(
                    Protocol64TransportFaultKind.ReceiveBackpressure,
                    Protocol64TransportFaultScope.Stream,
                    "Reliable receive ordering buffer is full; the frame was not accepted.",
                    frame.Header.ConnectionEpoch,
                    frame.StreamId,
                    frame.EffectiveChannel,
                    frame.Delivery.Kind,
                    frame.StreamSequence,
                    frame.Header.FrameId,
                    CompleteFrameDelivered: true);
                return new ConnectionReceiveResult(ConnectionReceiveStatus.Rejected, [], Fault: fault);
            }

            lane.Pending.Add(frame.StreamSequence, frame);
            _pendingReliableFrames++;
            var repair = Protocol64RepairRequest.MissingReliableFrame(
                frame.Header.ConnectionEpoch,
                frame.StreamId,
                frame.EffectiveChannel,
                frame.Delivery.Kind,
                lane.NextSequence,
                frame.StreamSequence - 1,
                frame.Header.FrameId);
            return new ConnectionReceiveResult(ConnectionReceiveStatus.RepairRequested, [], repair);
        }

        var released = ReleaseOrdered(lane, frame);
        _pendingReliableFrames -= Math.Max(0, released.Count - 1);
        return new ConnectionReceiveResult(ConnectionReceiveStatus.Delivered, released);
    }

    private ConnectionReceiveResult AcceptReliableUnordered(Protocol64ReceivedFrame frame)
    {
        var stream = GetStreamKey(frame);
        if (!_seenUnordered.TryGetValue(stream, out var seen))
        {
            seen = [];
            _seenUnordered.Add(stream, seen);
        }

        if (!seen.Add(frame.StreamSequence))
        {
            return new ConnectionReceiveResult(ConnectionReceiveStatus.Duplicate, []);
        }

        return new ConnectionReceiveResult(ConnectionReceiveStatus.Delivered, [frame]);
    }

    private ConnectionReceiveResult AcceptLastWins(Protocol64ReceivedFrame frame)
    {
        var key = (frame.EffectiveChannel, frame.ReplacementKey ?? string.Empty);
        var order = frame.Header.FrameId == 0 ? _nextArrivalSequence : frame.Header.FrameId;
        var arrival = _nextArrivalSequence++;
        if (_lastWins.TryGetValue(key, out var existing) && order <= existing.Order)
        {
            return new ConnectionReceiveResult(ConnectionReceiveStatus.Stale, []);
        }

        var wasReplacement = _lastWins.ContainsKey(key);
        _lastWins[key] = new PendingLastWins(frame, order, arrival);
        return new ConnectionReceiveResult(
            wasReplacement ? ConnectionReceiveStatus.Replaced : ConnectionReceiveStatus.Delivered,
            []);
    }

    private static List<Protocol64ReceivedFrame> ReleaseOrdered(
        OrderedLane lane,
        Protocol64ReceivedFrame frame)
    {
        var released = new List<Protocol64ReceivedFrame> { frame };
        lane.NextSequence = checked(lane.NextSequence + 1);
        while (lane.Pending.Remove(lane.NextSequence, out var pending))
        {
            released.Add(pending);
            lane.NextSequence = checked(lane.NextSequence + 1);
        }

        return released;
    }

    private static Protocol64StreamKey GetStreamKey(Protocol64ReceivedFrame frame)
        => new(frame.EffectiveChannel, frame.Delivery.Kind, frame.Lane);

    private sealed class OrderedLane(ulong nextSequence)
    {
        public ulong NextSequence { get; set; } = nextSequence;

        public SortedDictionary<ulong, Protocol64ReceivedFrame> Pending { get; } = [];
    }

    private sealed record PendingLastWins(
        Protocol64ReceivedFrame Frame,
        ulong Order,
        ulong ArrivalSequence);
}
