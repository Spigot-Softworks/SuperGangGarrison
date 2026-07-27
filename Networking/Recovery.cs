using OpenGarrison.Protocol;

namespace OpenGarrison.Networking;

public enum Protocol64TransportFaultKind : byte
{
    StreamReset = 1,
    StreamClosed = 2,
    ReadFailed = 3,
    WriteFailed = 4,
    InvalidFrame = 5,
    ReceiveBackpressure = 6,
    Timeout = 7,
    ConnectionClosed = 8,
    ProtocolViolation = 9,
}

public enum Protocol64TransportFaultScope : byte
{
    Stream = 1,
    Connection = 2,
}

public sealed record Protocol64TransportFault(
    Protocol64TransportFaultKind Kind,
    Protocol64TransportFaultScope Scope,
    string Message,
    ulong ConnectionEpoch,
    int? StreamId,
    ChannelType? Channel,
    Protocol64DeliveryKind? Delivery,
    ulong? StreamSequence,
    ulong? FrameId,
    bool CompleteFrameDelivered,
    Protocol64Fault? ProtocolFault = null,
    Exception? Exception = null);

public enum Protocol64RepairReason : byte
{
    MissingReliableFrame = 1,
    MalformedCompleteFrame = 2,
    StreamReset = 3,
    StreamClosed = 4,
}

public sealed record Protocol64RepairRequest(
    Guid RequestId,
    ulong ConnectionEpoch,
    Protocol64RepairReason Reason,
    int StreamId,
    ChannelType Channel,
    Protocol64DeliveryKind Delivery,
    ulong? MissingSequenceFrom,
    ulong? MissingSequenceTo,
    ulong? OffendingFrameId,
    bool ExcludeOffendingFrame,
    int Lane = 0)
{
    public static ChannelType RequestChannel => ChannelType.Control;

    public Protocol64RetransmitRequest ToProtocolEvent()
        => new(
            RequestId,
            ConnectionEpoch,
            (Protocol64TransportRepairReason)Reason,
            StreamId,
            Channel,
            Delivery,
            MissingSequenceFrom,
            MissingSequenceTo,
            OffendingFrameId,
            ExcludeOffendingFrame,
            Lane);

    public static Protocol64RepairRequest MissingReliableFrame(
        ulong connectionEpoch,
        int streamId,
        ChannelType channel,
        Protocol64DeliveryKind delivery,
        ulong sequenceFrom,
        ulong sequenceTo,
        ulong? offendingFrameId)
        => new(
            Guid.NewGuid(),
            connectionEpoch,
            Protocol64RepairReason.MissingReliableFrame,
            streamId,
            channel,
            delivery,
            sequenceFrom,
            sequenceTo,
            offendingFrameId,
            ExcludeOffendingFrame: false);
}

public sealed record Protocol64StreamRecoverySnapshot(
    int StreamId,
    Protocol64StreamState State,
    ChannelType? Channel,
    Protocol64DeliveryKind? Delivery,
    Guid? PendingRepairId,
    int FaultCount);

public sealed record Protocol64RecoveryTransition(
    Protocol64ConnectionState PreviousConnectionState,
    Protocol64ConnectionState CurrentConnectionState,
    Protocol64StreamState? PreviousStreamState,
    Protocol64StreamState? CurrentStreamState,
    Protocol64TransportFault? Fault,
    Protocol64RepairRequest? RepairRequest,
    string Reason);

public sealed record Protocol64RecoveryResult(
    bool Accepted,
    Protocol64RecoveryTransition Transition,
    Protocol64RepairRequest? RepairRequest = null)
{
    public bool RequiresDisconnect
        => Transition.CurrentConnectionState == Protocol64ConnectionState.ProtocolError;
}

/// <summary>
/// The transport-independent part of the recovery state machine. A stream fault
/// requests a stream repair; a repeated fault before that repair completes, or a
/// control/connection fault, escalates to a protocol error.
/// </summary>
public sealed class Protocol64ConnectionRecovery
{
    private readonly Dictionary<int, MutableStreamState> _streams = [];

    public Protocol64ConnectionRecovery(ulong connectionEpoch)
    {
        ConnectionEpoch = connectionEpoch;
    }

    public ulong ConnectionEpoch { get; }

    public Protocol64ConnectionState State { get; private set; } = Protocol64ConnectionState.Healthy;

    public IReadOnlyList<Protocol64StreamRecoverySnapshot> Streams
        => _streams.Values
            .OrderBy(stream => stream.StreamId)
            .Select(stream => stream.Snapshot())
            .ToArray();

    public Protocol64RecoveryResult ReportFault(Protocol64TransportFault fault)
    {
        ArgumentNullException.ThrowIfNull(fault);
        if (State == Protocol64ConnectionState.Closed)
        {
            return Rejected("The connection is already closed.", fault);
        }

        if (fault.ConnectionEpoch != ConnectionEpoch)
        {
            return Rejected("The transport fault belongs to another connection epoch.", fault);
        }

        if (fault.Scope == Protocol64TransportFaultScope.Connection ||
            fault.StreamId is null ||
            fault.Channel == ChannelType.Control)
        {
            var previous = State;
            State = Protocol64ConnectionState.ProtocolError;
            return new Protocol64RecoveryResult(
                true,
                new Protocol64RecoveryTransition(
                    previous,
                    State,
                    null,
                    Protocol64StreamState.ProtocolError,
                    fault,
                    null,
                    "Connection or control stream fault requires protocol-error disconnect."));
        }

        var stream = GetOrCreate(fault);
        var previousConnectionState = State;
        if (stream.State is Protocol64StreamState.RepairRequested or
            Protocol64StreamState.Reopening or
            Protocol64StreamState.AwaitingRetransmit)
        {
            var previousStreamState = stream.State;
            stream.State = Protocol64StreamState.ProtocolError;
            State = Protocol64ConnectionState.ProtocolError;
            return new Protocol64RecoveryResult(
                true,
                new Protocol64RecoveryTransition(
                    previousConnectionState,
                    State,
                    previousStreamState,
                    stream.State,
                    fault,
                    null,
                    "A stream fault recurred before repair completed."));
        }

        var repair = new Protocol64RepairRequest(
            Guid.NewGuid(),
            ConnectionEpoch,
            fault.Kind == Protocol64TransportFaultKind.StreamReset
                ? Protocol64RepairReason.StreamReset
                : fault.Kind == Protocol64TransportFaultKind.StreamClosed
                    ? Protocol64RepairReason.StreamClosed
                    : Protocol64RepairReason.MalformedCompleteFrame,
            fault.StreamId ?? throw new InvalidOperationException("A stream repair requires a stream ID."),
            fault.Channel ?? throw new InvalidOperationException("A stream repair requires a channel."),
            fault.Delivery ?? Protocol64DeliveryKind.ReliableOrdered,
            fault.StreamSequence,
            fault.StreamSequence,
            fault.FrameId,
            ExcludeOffendingFrame: true);
        var oldState = stream.State;
        stream.State = Protocol64StreamState.RepairRequested;
        stream.PendingRepairId = repair.RequestId;
        stream.FaultCount++;
        State = Protocol64ConnectionState.Recovering;
        return new Protocol64RecoveryResult(
            true,
            new Protocol64RecoveryTransition(
                previousConnectionState,
                State,
                oldState,
                stream.State,
                fault,
                repair,
                "Stream repair requested."),
            repair);
    }

    public Protocol64RecoveryResult MarkStreamReopened(int streamId)
    {
        if (!_streams.TryGetValue(streamId, out var stream) ||
            stream.State != Protocol64StreamState.RepairRequested)
        {
            return Rejected("The stream cannot be reopened in its current state.", null);
        }

        var previous = stream.State;
        stream.State = Protocol64StreamState.AwaitingRetransmit;
        return new Protocol64RecoveryResult(
            true,
            new Protocol64RecoveryTransition(
                State,
                State,
                previous,
                stream.State,
                null,
                null,
                "Stream reopened; waiting for retransmission."));
    }

    public Protocol64RecoveryResult MarkRepairCompleted(Guid requestId, int streamId)
    {
        if (!_streams.TryGetValue(streamId, out var stream) ||
            stream.PendingRepairId != requestId ||
            stream.State != Protocol64StreamState.AwaitingRetransmit)
        {
            return Rejected("The repair acknowledgement does not match an awaiting stream repair.", null);
        }

        var previousConnection = State;
        var previous = stream.State;
        stream.State = Protocol64StreamState.Healthy;
        stream.PendingRepairId = null;
        if (_streams.Values.All(candidate => candidate.State == Protocol64StreamState.Healthy))
        {
            State = Protocol64ConnectionState.Healthy;
        }

        return new Protocol64RecoveryResult(
            true,
            new Protocol64RecoveryTransition(
                previousConnection,
                State,
                previous,
                stream.State,
                null,
                null,
                "Stream retransmission completed."));
    }

    public void Close()
    {
        State = Protocol64ConnectionState.Closed;
        foreach (var stream in _streams.Values)
        {
            stream.State = Protocol64StreamState.Closed;
        }
    }

    private MutableStreamState GetOrCreate(Protocol64TransportFault fault)
    {
        var streamId = fault.StreamId!.Value;
        if (!_streams.TryGetValue(streamId, out var stream))
        {
            stream = new MutableStreamState(streamId, fault.Channel, fault.Delivery);
            _streams.Add(streamId, stream);
        }

        return stream;
    }

    private Protocol64RecoveryResult Rejected(string reason, Protocol64TransportFault? fault)
        => new(
            false,
            new Protocol64RecoveryTransition(
                State,
                State,
                null,
                null,
                fault,
                null,
                reason));

    private sealed class MutableStreamState(
        int streamId,
        ChannelType? channel,
        Protocol64DeliveryKind? delivery)
    {
        public int StreamId { get; } = streamId;

        public ChannelType? Channel { get; } = channel;

        public Protocol64DeliveryKind? Delivery { get; } = delivery;

        public Protocol64StreamState State { get; set; } = Protocol64StreamState.Healthy;

        public Guid? PendingRepairId { get; set; }

        public int FaultCount { get; set; }

        public Protocol64StreamRecoverySnapshot Snapshot()
            => new(StreamId, State, Channel, Delivery, PendingRepairId, FaultCount);
    }
}
