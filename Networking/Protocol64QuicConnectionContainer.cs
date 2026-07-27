using System.Net.Quic;
using OpenGarrison.Protocol;

namespace OpenGarrison.Networking;

/// <summary>
/// Optional native socket seam for the logical QUIC container. The container
/// does not open a socket by itself, which keeps its scheduling and recovery
/// behavior deterministic on hosts without native QUIC support.
/// </summary>
public interface IProtocol64QuicSocketBackend
{
    ValueTask<QuicConnection?> OpenConnectionAsync(
        ulong connectionEpoch,
        CancellationToken cancellationToken = default);

    ValueTask CloseConnectionAsync(
        QuicConnection connection,
        CancellationToken cancellationToken = default);
}

public sealed record Protocol64QuicConnectionOptions
{
    public Protocol64ChannelSchedulerOptions SchedulerOptions { get; init; } = new();

    public int FirstLocallyInitiatedBidirectionalStreamId { get; init; }

    public bool PreferDatagramsForLastWins { get; init; } = true;

    public bool DatagramsAvailable { get; init; }

    public IProtocol64QuicSocketBackend? SocketBackend { get; init; }
}

public enum Protocol64QuicStreamRole : byte
{
    Control = 1,
    Input = 2,
    ReliableUnordered = 3,
    LastWinsDatagram = 4,
    LastWinsFallback = 5,
    RecoveryRetransmit = 6,
    ReliableOrdered = 7,
}

/// <summary>
/// A deterministic logical-to-QUIC stream binding. StreamId is an in-memory
/// stand-in for the native QUIC stream ID until a socket backend binds it.
/// </summary>
public sealed record Protocol64QuicStreamSelection(
    int StreamId,
    Protocol64StreamKey Stream,
    Protocol64QuicStreamRole Role,
    int Generation,
    bool IsDatagram,
    bool IsFallback,
    QuicStreamType StreamType)
{
    public ChannelType Channel => Stream.Channel;

    public Protocol64DeliveryKind Delivery => Stream.Delivery;

    public int Lane => Stream.Lane;

    public bool IsDedicatedRecoveryStream
        => Role == Protocol64QuicStreamRole.RecoveryRetransmit;
}

public sealed record Protocol64QuicRetransmitPlan(
    Guid PlanId,
    Protocol64RepairRequest RepairRequest,
    int OriginalStreamId,
    Protocol64QuicStreamSelection ReopenedStream,
    Protocol64QuicStreamSelection DedicatedStream,
    Protocol64QuicStreamSelection ControlStream)
{
    public ulong? SequenceFrom => RepairRequest.MissingSequenceFrom;

    public ulong? SequenceTo => RepairRequest.MissingSequenceTo;
}

/// <summary>
/// Protocol-64's backend-specific QUIC slice. It composes the existing
/// scheduler and recovery state machines without changing either one.
/// </summary>
public sealed class Protocol64QuicConnectionContainer : IConnectionContainer
{
    private readonly Protocol64ConnectionContainer _logicalContainer;
    private readonly Protocol64QuicConnectionOptions _options;
    private readonly Dictionary<Protocol64StreamKey, Protocol64QuicStreamSelection> _currentStreams = [];
    private readonly Dictionary<int, Protocol64QuicStreamSelection> _streamIds = [];
    private readonly HashSet<Protocol64QuicStreamIdentity> _recoveredStreams = [];
    private readonly Queue<Protocol64RepairRequest> _pendingControlRepairRequests = [];
    private readonly Dictionary<Guid, Protocol64QuicRetransmitPlan> _retransmitPlans = [];
    private int _nextStreamId;
    private bool _disposed;

    public Protocol64QuicConnectionContainer(
        ulong connectionEpoch,
        Protocol64QuicConnectionOptions? options = null)
    {
        ArgumentOutOfRangeException.ThrowIfZero(connectionEpoch);

        _options = options ?? new Protocol64QuicConnectionOptions();
        if (_options.FirstLocallyInitiatedBidirectionalStreamId < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The first logical QUIC stream ID cannot be negative.");
        }

        _logicalContainer = new Protocol64ConnectionContainer(
            connectionEpoch,
            _options.SchedulerOptions);
        _nextStreamId = _options.FirstLocallyInitiatedBidirectionalStreamId;
    }

    public ulong ConnectionEpoch => _logicalContainer.ConnectionEpoch;

    public Protocol64NetworkTelemetry Telemetry => _logicalContainer.Telemetry;

    public Protocol64ConnectionState State => _logicalContainer.State;

    public IReadOnlyList<Protocol64StreamRecoverySnapshot> Streams
        => _logicalContainer.Streams;

    public IProtocol64QuicSocketBackend? SocketBackend => _options.SocketBackend;

    public Protocol64QuicStreamSelection ControlStream
        => SelectStream(new Protocol64StreamKey(
            ChannelType.Control,
            Protocol64DeliveryKind.ReliableOrdered,
            0));

    public Protocol64QuicStreamSelection InputStream
        => SelectStream(new Protocol64StreamKey(
            ChannelType.Input,
            Protocol64DeliveryKind.ReliableOrdered,
            0));

    public IReadOnlyList<Protocol64QuicStreamSelection> LogicalStreams
        => _currentStreams.Values
            .OrderBy(stream => stream.StreamId)
            .ToArray();

    public IReadOnlyList<Protocol64RepairRequest> PendingControlRepairRequests
        => _pendingControlRepairRequests.ToArray();

    public IReadOnlyList<Protocol64QuicRetransmitPlan> PendingRetransmitPlans
        => _retransmitPlans.Values
            .OrderBy(plan => plan.PlanId)
            .ToArray();

    public ConnectionSendResult EnqueueSend(Protocol64OutboundFrame frame)
    {
        EnsureUsable();
        return _logicalContainer.EnqueueSend(frame);
    }

    public bool TryDequeueSend(out Protocol64ScheduledFrame frame)
    {
        EnsureUsable();
        return _logicalContainer.TryDequeueSend(out frame);
    }

    public bool TryDequeueSend(
        out Protocol64ScheduledFrame frame,
        out Protocol64QuicStreamSelection stream)
    {
        EnsureUsable();
        if (!_logicalContainer.TryDequeueSend(out frame))
        {
            stream = null!;
            return false;
        }

        stream = SelectStream(frame.Stream);
        return true;
    }

    public ConnectionReceiveResult AcceptReceived(Protocol64ReceivedFrame frame)
    {
        EnsureUsable();
        return _logicalContainer.AcceptReceived(frame);
    }

    public bool TryDequeueReceived(out Protocol64ReceivedFrame frame)
    {
        EnsureUsable();
        return _logicalContainer.TryDequeueReceived(out frame);
    }

    /// <summary>
    /// Returns the stable logical stream for a scheduled delivery. Control and
    /// Input each get one ordered stream; unordered deliveries get the lane
    /// selected by the existing scheduler; LastWins gets a datagram lane when
    /// explicitly available, otherwise a replaceable fallback stream.
    /// </summary>
    public Protocol64QuicStreamSelection SelectStream(Protocol64StreamKey stream)
    {
        EnsureUsable();
        ValidateStreamKey(stream);

        if (_currentStreams.TryGetValue(stream, out var existing))
        {
            return existing;
        }

        var role = GetRole(stream);
        var isDatagram = role == Protocol64QuicStreamRole.LastWinsDatagram;
        var selection = AllocateStream(
            stream,
            role,
            isDatagram,
            isFallback: role == Protocol64QuicStreamRole.LastWinsFallback,
            generation: 0);
        _currentStreams[stream] = selection;
        return selection;
    }

    public Protocol64QuicStreamSelection SelectStream(
        Protocol64DeliveryDescriptor delivery,
        int lane = 0)
    {
        var channel = delivery.Channel ?? ChannelType.State;
        return SelectStream(new Protocol64StreamKey(channel, delivery.Kind, lane));
    }

    public bool TryGetStreamSelection(
        int streamId,
        out Protocol64QuicStreamSelection selection)
        => _streamIds.TryGetValue(streamId, out selection!);

    public bool TryDequeueControlRepairRequest(out Protocol64RepairRequest request)
    {
        EnsureUsable();
        if (_pendingControlRepairRequests.Count == 0)
        {
            request = null!;
            return false;
        }

        request = _pendingControlRepairRequests.Dequeue();
        return true;
    }

    public bool TryGetRetransmitPlan(
        Guid planId,
        out Protocol64QuicRetransmitPlan plan)
        => _retransmitPlans.TryGetValue(planId, out plan!);

    /// <summary>
    /// Allocates a physical bidirectional stream reserved for a peer's
    /// retransmit request. The logical key remains the original channel/lane;
    /// the runtime sends a response header first so the receiver can associate
    /// this physical stream with that logical lane.
    /// </summary>
    public Protocol64QuicStreamSelection AllocatePeerRetransmitStream(
        ChannelType channel,
        Protocol64DeliveryKind delivery,
        int lane)
    {
        EnsureUsable();
        var key = new Protocol64StreamKey(channel, delivery, lane);
        ValidateStreamKey(key);
        return AllocateDedicatedRetransmitStream(key);
    }

    public Protocol64RecoveryResult ReportTransportFault(Protocol64TransportFault fault)
    {
        EnsureUsable();
        ArgumentNullException.ThrowIfNull(fault);

        var identity = ResolveFaultIdentity(fault);
        var isPostRecoveryFault = identity is not null &&
            _recoveredStreams.Contains(identity.Value);
        var result = _logicalContainer.ReportTransportFault(fault);

        // Protocol64ConnectionRecovery intentionally allows a fresh repair
        // after a completed repair. QUIC's backend policy is stricter: a
        // second fault after recovery is a protocol error. Reporting the
        // same fault twice drives the existing state machine's documented
        // repeated-fault transition without changing Recovery.cs.
        if (isPostRecoveryFault && result.RepairRequest is not null)
        {
            return _logicalContainer.ReportTransportFault(fault);
        }

        if (result.RepairRequest is null || fault.StreamId is null)
        {
            return result;
        }

        var originalStream = ResolveFaultStream(fault, identity);
        result = result with
        {
            RepairRequest = result.RepairRequest with { Lane = originalStream.Lane },
            Transition = result.Transition with
            {
                RepairRequest = result.RepairRequest with { Lane = originalStream.Lane },
            },
        };
        var reopenedStream = ReopenStream(originalStream);
        var reopened = _logicalContainer.MarkStreamReopened(fault.StreamId.Value);
        if (!reopened.Accepted)
        {
            return reopened;
        }

        var controlStream = ControlStream;
        var dedicatedStream = AllocateDedicatedRetransmitStream(originalStream);
        var repair = result.RepairRequest;
        var plan = new Protocol64QuicRetransmitPlan(
            repair.RequestId,
            repair,
            fault.StreamId.Value,
            reopenedStream,
            dedicatedStream,
            controlStream);

        _pendingControlRepairRequests.Enqueue(repair);
        _retransmitPlans.Add(plan.PlanId, plan);

        var transition = reopened.Transition with
        {
            Fault = fault,
            RepairRequest = repair,
            Reason = "Stream reopened; repair request queued on Control and retransmit reserved on a dedicated stream.",
        };
        return reopened with
        {
            Transition = transition,
            RepairRequest = repair,
        };
    }

    public Protocol64RecoveryResult MarkStreamReopened(int streamId)
    {
        EnsureUsable();
        return _logicalContainer.MarkStreamReopened(streamId);
    }

    public Protocol64RecoveryResult MarkRepairCompleted(Guid requestId, int streamId)
    {
        EnsureUsable();

        var recoveryStreamId = streamId;
        if (_retransmitPlans.TryGetValue(requestId, out var plan))
        {
            // Recovery.cs keys its state by the failed stream ID, while the
            // backend may acknowledge using the newly reopened stream ID.
            recoveryStreamId = plan.OriginalStreamId;
        }

        var result = _logicalContainer.MarkRepairCompleted(requestId, recoveryStreamId);
        if (!result.Accepted)
        {
            return result;
        }

        if (plan is not null)
        {
            _retransmitPlans.Remove(requestId);
            RemovePendingControlRepair(requestId);
            _recoveredStreams.Add(GetIdentity(plan.ReopenedStream.Stream));
            _recoveredStreams.Add(GetIdentity(plan.DedicatedStream.Stream));
        }

        return result;
    }

    /// <summary>
    /// Invokes the optional native hook only when the caller explicitly asks
    /// for it. No socket is required by the logical container or its tests.
    /// </summary>
    public ValueTask<QuicConnection?> OpenNativeConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureUsable();
        return SocketBackend is null
            ? ValueTask.FromResult<QuicConnection?>(null)
            : SocketBackend.OpenConnectionAsync(ConnectionEpoch, cancellationToken);
    }

    public ValueTask CloseNativeConnectionAsync(
        QuicConnection connection,
        CancellationToken cancellationToken = default)
    {
        EnsureUsable();
        ArgumentNullException.ThrowIfNull(connection);
        return SocketBackend is null
            ? ValueTask.CompletedTask
            : SocketBackend.CloseConnectionAsync(connection, cancellationToken);
    }

    public void Close()
    {
        if (_disposed)
        {
            return;
        }

        _logicalContainer.Close();
        _disposed = true;
    }

    public void Dispose() => Close();

    private Protocol64QuicStreamSelection ReopenStream(
        Protocol64StreamKey originalStream)
    {
        var previous = _currentStreams.TryGetValue(originalStream, out var current)
            ? current
            : SelectStream(originalStream);
        var replacement = AllocateStream(
            originalStream,
            previous.Role,
            previous.IsDatagram,
            previous.IsFallback,
            previous.Generation + 1);
        _currentStreams[originalStream] = replacement;
        return replacement;
    }

    private Protocol64QuicStreamSelection AllocateDedicatedRetransmitStream(
        Protocol64StreamKey originalStream)
    {
        var selection = new Protocol64QuicStreamSelection(
            NextStreamId(),
            originalStream,
            Protocol64QuicStreamRole.RecoveryRetransmit,
            0,
            IsDatagram: false,
            IsFallback: false,
            QuicStreamType.Bidirectional);
        _streamIds[selection.StreamId] = selection;
        return selection;
    }

    private Protocol64QuicStreamSelection AllocateStream(
        Protocol64StreamKey stream,
        Protocol64QuicStreamRole role,
        bool isDatagram,
        bool isFallback,
        int generation)
    {
        var selection = new Protocol64QuicStreamSelection(
            isDatagram ? -1 : NextStreamId(),
            stream,
            role,
            generation,
            isDatagram,
            isFallback,
            QuicStreamType.Bidirectional);
        if (!isDatagram)
        {
            _streamIds[selection.StreamId] = selection;
        }

        return selection;
    }

    private Protocol64QuicStreamIdentity? ResolveFaultIdentity(
        Protocol64TransportFault fault)
    {
        if (fault.StreamId is int streamId && _streamIds.TryGetValue(streamId, out var selection))
        {
            return GetIdentity(selection.Stream);
        }

        if (fault.Channel is not ChannelType channel)
        {
            return null;
        }

        var delivery = fault.Delivery ?? Protocol64DeliveryKind.ReliableOrdered;
        var lane = _currentStreams.Values
            .Where(candidate => candidate.Channel == channel && candidate.Delivery == delivery)
            .Select(candidate => candidate.Lane)
            .DefaultIfEmpty(0)
            .First();
        var streamKey = new Protocol64StreamKey(channel, delivery, lane);
        var current = SelectStream(streamKey);
        if (fault.StreamId is int unknownStreamId)
        {
            _streamIds[unknownStreamId] = current;
        }

        return GetIdentity(streamKey);
    }

    private Protocol64StreamKey ResolveFaultStream(
        Protocol64TransportFault fault,
        Protocol64QuicStreamIdentity? identity)
    {
        if (fault.StreamId is int streamId && _streamIds.TryGetValue(streamId, out var selection))
        {
            return selection.Stream;
        }

        return identity is Protocol64QuicStreamIdentity known
            ? new Protocol64StreamKey(known.Channel, known.Delivery, known.Lane)
            : new Protocol64StreamKey(
                fault.Channel ?? ChannelType.State,
                fault.Delivery ?? Protocol64DeliveryKind.ReliableOrdered,
                0);
    }

    private Protocol64QuicStreamRole GetRole(Protocol64StreamKey stream)
        => stream.Delivery switch
        {
            Protocol64DeliveryKind.ReliableOrdered when stream.Channel == ChannelType.Control
                => Protocol64QuicStreamRole.Control,
            Protocol64DeliveryKind.ReliableOrdered when stream.Channel == ChannelType.Input
                => Protocol64QuicStreamRole.Input,
            Protocol64DeliveryKind.ReliableOrdered
                => Protocol64QuicStreamRole.ReliableOrdered,
            Protocol64DeliveryKind.ReliableUnordered
                => Protocol64QuicStreamRole.ReliableUnordered,
            Protocol64DeliveryKind.LastWins when
                _options.PreferDatagramsForLastWins && _options.DatagramsAvailable
                => Protocol64QuicStreamRole.LastWinsDatagram,
            Protocol64DeliveryKind.LastWins
                => Protocol64QuicStreamRole.LastWinsFallback,
            _ => throw new ArgumentOutOfRangeException(nameof(stream)),
        };

    private int NextStreamId()
    {
        var streamId = _nextStreamId;
        _nextStreamId = checked(_nextStreamId + 4);
        return streamId;
    }

    private static Protocol64QuicStreamIdentity GetIdentity(Protocol64StreamKey stream)
        => new(stream.Channel, stream.Delivery, stream.Lane);

    private void ValidateStreamKey(Protocol64StreamKey stream)
    {
        if (stream.Lane < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stream), "A stream lane cannot be negative.");
        }

        if (stream.Delivery == Protocol64DeliveryKind.LastWins && stream.Lane != 0)
        {
            throw new ArgumentException("LastWins has exactly one logical lane.", nameof(stream));
        }

        if (stream.Delivery == Protocol64DeliveryKind.ReliableUnordered &&
            stream.Lane >= _options.SchedulerOptions.ReliableUnorderedLaneCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stream),
                "The selected unordered lane is outside the configured lane pool.");
        }
    }

    private void RemovePendingControlRepair(Guid requestId)
    {
        if (_pendingControlRepairRequests.Count == 0)
        {
            return;
        }

        var remaining = _pendingControlRepairRequests
            .Where(request => request.RequestId != requestId)
            .ToArray();
        _pendingControlRepairRequests.Clear();
        foreach (var request in remaining)
        {
            _pendingControlRepairRequests.Enqueue(request);
        }
    }

    private void EnsureUsable()
        => ObjectDisposedException.ThrowIf(_disposed, this);

    private readonly record struct Protocol64QuicStreamIdentity(
        ChannelType Channel,
        Protocol64DeliveryKind Delivery,
        int Lane);
}
