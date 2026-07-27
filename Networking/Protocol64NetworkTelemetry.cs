using System.Collections.Concurrent;
using OpenGarrison.Protocol;

namespace OpenGarrison.Networking;

public readonly record struct Protocol64TelemetryKey(
    ushort SchemaId,
    ChannelType Channel,
    Protocol64DeliveryKind Delivery);

public readonly record struct Protocol64TelemetryFaultKey(
    Protocol64TransportFaultKind? TransportFault,
    Protocol64FaultKind? ProtocolFault);

public sealed record Protocol64NetworkTelemetrySnapshot(
    long FramesSent,
    long FramesReceived,
    long BytesSent,
    long BytesReceived,
    long ReliableBackpressure,
    long LastWinsReplacements,
    long LastWinsStaleDiscards,
    long DecodeFaults,
    long StreamResets,
    long ReconnectAttempts,
    long RepairRequests,
    long RepairCompletions,
    long ProtocolErrorDisconnects,
    long StateRepairRequests,
    long InputCommandsReceived,
    long InputCommandsApplied,
    long InputCommandsRejected,
    long InputCommandsDuplicated,
    IReadOnlyDictionary<Protocol64TelemetryKey, long> FramesByEvent,
    IReadOnlyDictionary<Protocol64TelemetryFaultKey, long> FaultsByKind);

/// <summary>
/// Thread-safe, privacy-safe protocol-64 counters. It stores no player names,
/// payloads, or endpoint addresses and can therefore be exported with normal
/// connection diagnostics.
/// </summary>
public sealed class Protocol64NetworkTelemetry
{
    private readonly ConcurrentDictionary<Protocol64TelemetryKey, long> _framesByEvent = new();
    private readonly ConcurrentDictionary<Protocol64TelemetryFaultKey, long> _faultsByKind = new();
    private long _framesSent;
    private long _framesReceived;
    private long _bytesSent;
    private long _bytesReceived;
    private long _reliableBackpressure;
    private long _lastWinsReplacements;
    private long _lastWinsStaleDiscards;
    private long _decodeFaults;
    private long _streamResets;
    private long _reconnectAttempts;
    private long _repairRequests;
    private long _repairCompletions;
    private long _protocolErrorDisconnects;
    private long _stateRepairRequests;
    private long _inputCommandsReceived;
    private long _inputCommandsApplied;
    private long _inputCommandsRejected;
    private long _inputCommandsDuplicated;

    public void RecordFrameSent(Protocol64FrameHeader header, Protocol64DeliveryDescriptor delivery, int bytes)
    {
        Interlocked.Increment(ref _framesSent);
        Interlocked.Add(ref _bytesSent, Math.Max(0, bytes));
        Increment(_framesByEvent, Key(header, delivery));
    }

    public void RecordFrameReceived(Protocol64FrameHeader header, Protocol64DeliveryDescriptor delivery, int bytes)
    {
        Interlocked.Increment(ref _framesReceived);
        Interlocked.Add(ref _bytesReceived, Math.Max(0, bytes));
        Increment(_framesByEvent, Key(header, delivery));
    }

    public void RecordReliableBackpressure() => Interlocked.Increment(ref _reliableBackpressure);

    public void RecordLastWinsReplacement() => Interlocked.Increment(ref _lastWinsReplacements);

    public void RecordLastWinsStaleDiscard() => Interlocked.Increment(ref _lastWinsStaleDiscards);

    public void RecordDecodeFault(Protocol64Fault fault)
    {
        ArgumentNullException.ThrowIfNull(fault);
        Interlocked.Increment(ref _decodeFaults);
        Increment(_faultsByKind, new Protocol64TelemetryFaultKey(null, fault.Kind));
    }

    public void RecordTransportFault(Protocol64TransportFault fault)
    {
        ArgumentNullException.ThrowIfNull(fault);
        Increment(_faultsByKind, new Protocol64TelemetryFaultKey(fault.Kind, fault.ProtocolFault?.Kind));
        if (fault.Kind == Protocol64TransportFaultKind.StreamReset)
        {
            Interlocked.Increment(ref _streamResets);
        }
    }

    public void RecordReconnectAttempt() => Interlocked.Increment(ref _reconnectAttempts);

    public void RecordRepairRequested(bool stateRepair = false)
    {
        Interlocked.Increment(ref _repairRequests);
        if (stateRepair)
        {
            Interlocked.Increment(ref _stateRepairRequests);
        }
    }

    public void RecordRepairCompleted() => Interlocked.Increment(ref _repairCompletions);

    public void RecordProtocolErrorDisconnect() => Interlocked.Increment(ref _protocolErrorDisconnects);

    public void RecordInputCommand(Protocol64InputCommandResultKind result)
    {
        switch (result)
        {
            case Protocol64InputCommandResultKind.Consumed:
                Interlocked.Increment(ref _inputCommandsApplied);
                break;
            case Protocol64InputCommandResultKind.Rejected:
                Interlocked.Increment(ref _inputCommandsRejected);
                break;
            case Protocol64InputCommandResultKind.Duplicate:
                Interlocked.Increment(ref _inputCommandsDuplicated);
                break;
        }
    }

    public void RecordInputCommandReceived() => Interlocked.Increment(ref _inputCommandsReceived);

    public Protocol64NetworkTelemetrySnapshot Snapshot()
        => new(
            Interlocked.Read(ref _framesSent),
            Interlocked.Read(ref _framesReceived),
            Interlocked.Read(ref _bytesSent),
            Interlocked.Read(ref _bytesReceived),
            Interlocked.Read(ref _reliableBackpressure),
            Interlocked.Read(ref _lastWinsReplacements),
            Interlocked.Read(ref _lastWinsStaleDiscards),
            Interlocked.Read(ref _decodeFaults),
            Interlocked.Read(ref _streamResets),
            Interlocked.Read(ref _reconnectAttempts),
            Interlocked.Read(ref _repairRequests),
            Interlocked.Read(ref _repairCompletions),
            Interlocked.Read(ref _protocolErrorDisconnects),
            Interlocked.Read(ref _stateRepairRequests),
            Interlocked.Read(ref _inputCommandsReceived),
            Interlocked.Read(ref _inputCommandsApplied),
            Interlocked.Read(ref _inputCommandsRejected),
            Interlocked.Read(ref _inputCommandsDuplicated),
            new Dictionary<Protocol64TelemetryKey, long>(_framesByEvent),
            new Dictionary<Protocol64TelemetryFaultKey, long>(_faultsByKind));

    private static Protocol64TelemetryKey Key(
        Protocol64FrameHeader header,
        Protocol64DeliveryDescriptor delivery)
        => new(header.SchemaId, delivery.Channel ?? ChannelType.State, delivery.Kind);

    private static void Increment<TKey>(ConcurrentDictionary<TKey, long> counters, TKey key)
        where TKey : notnull
        => counters.AddOrUpdate(key, 1, static (_, current) => checked(current + 1));
}
