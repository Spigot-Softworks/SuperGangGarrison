using System.Buffers.Binary;
using OpenGarrison.Protocol;

namespace OpenGarrison.Networking;

public interface IConnectionContainer : IDisposable
{
    ulong ConnectionEpoch { get; }

    Protocol64NetworkTelemetry Telemetry { get; }

    Protocol64ConnectionState State { get; }

    IReadOnlyList<Protocol64StreamRecoverySnapshot> Streams { get; }

    ConnectionSendResult EnqueueSend(Protocol64OutboundFrame frame);

    bool TryDequeueSend(out Protocol64ScheduledFrame frame);

    ConnectionReceiveResult AcceptReceived(Protocol64ReceivedFrame frame);

    bool TryDequeueReceived(out Protocol64ReceivedFrame frame);

    Protocol64RecoveryResult ReportTransportFault(Protocol64TransportFault fault);

    Protocol64RecoveryResult MarkStreamReopened(int streamId);

    Protocol64RecoveryResult MarkRepairCompleted(Guid requestId, int streamId);

    void Close();
}

/// <summary>
/// Default backend-neutral protocol-64 connection container. It coordinates
/// outbound delivery scheduling, inbound semantic ordering, and stream recovery;
/// a concrete backend only needs to move complete frames and honor the selected
/// stream/lane plus recovery requests.
/// </summary>
public sealed class Protocol64ConnectionContainer : IConnectionContainer
{
    private readonly Protocol64ChannelScheduler _sendScheduler;
    private readonly Protocol64ReceiveScheduler _receiveScheduler;
    private readonly Protocol64ConnectionRecovery _recovery;
    private bool _disposed;

    public Protocol64NetworkTelemetry Telemetry { get; }

    public Protocol64ConnectionContainer(
        ulong connectionEpoch,
        Protocol64ChannelSchedulerOptions? schedulerOptions = null)
    {
        ArgumentOutOfRangeException.ThrowIfZero(connectionEpoch);

        ConnectionEpoch = connectionEpoch;
        Telemetry = new Protocol64NetworkTelemetry();
        _sendScheduler = new Protocol64ChannelScheduler(schedulerOptions);
        _receiveScheduler = new Protocol64ReceiveScheduler(
            schedulerOptions?.MaxPendingReliableFrames ?? 4096);
        _recovery = new Protocol64ConnectionRecovery(connectionEpoch);
    }

    public ulong ConnectionEpoch { get; }

    public Protocol64ConnectionState State => _recovery.State;

    public IReadOnlyList<Protocol64StreamRecoverySnapshot> Streams => _recovery.Streams;

    public ConnectionSendResult EnqueueSend(Protocol64OutboundFrame frame)
    {
        EnsureUsable();
        ArgumentNullException.ThrowIfNull(frame);
        if (State is Protocol64ConnectionState.ProtocolError or Protocol64ConnectionState.Closed)
        {
            return ConnectionSendResult.Rejected(Protocol64ConnectionFrameValidation.Fault(
                Protocol64FaultKind.ValidationFailed,
                "Cannot enqueue a frame on a failed or closed connection."));
        }

        if (frame.Header.ConnectionEpoch != ConnectionEpoch)
        {
            return ConnectionSendResult.Rejected(Protocol64ConnectionFrameValidation.Fault(
                Protocol64FaultKind.ValidationFailed,
                "Frame connection epoch does not match the container."));
        }

        var result = _sendScheduler.Enqueue(frame);
        switch (result.Status)
        {
            case ConnectionSendStatus.Backpressured:
                Telemetry.RecordReliableBackpressure();
                break;
            case ConnectionSendStatus.Replaced:
                Telemetry.RecordLastWinsReplacement();
                break;
            case ConnectionSendStatus.IgnoredStale:
                Telemetry.RecordLastWinsStaleDiscard();
                break;
        }

        return result;
    }

    public bool TryDequeueSend(out Protocol64ScheduledFrame frame)
    {
        EnsureUsable();
        return _sendScheduler.TryDequeue(out frame);
    }

    public ConnectionReceiveResult AcceptReceived(Protocol64ReceivedFrame frame)
    {
        EnsureUsable();
        ArgumentNullException.ThrowIfNull(frame);
        if (State is Protocol64ConnectionState.ProtocolError or Protocol64ConnectionState.Closed)
        {
            return new ConnectionReceiveResult(
                ConnectionReceiveStatus.Rejected,
                [],
                Fault: new Protocol64TransportFault(
                    Protocol64TransportFaultKind.ProtocolViolation,
                    Protocol64TransportFaultScope.Connection,
                    "Cannot accept a frame on a failed or closed connection.",
                    ConnectionEpoch,
                    null,
                    null,
                    null,
                    null,
                    null,
                    CompleteFrameDelivered: false));
        }

        if (frame.Header.ConnectionEpoch != ConnectionEpoch)
        {
            return new ConnectionReceiveResult(
                ConnectionReceiveStatus.Rejected,
                [],
                Fault: new Protocol64TransportFault(
                    Protocol64TransportFaultKind.ProtocolViolation,
                    Protocol64TransportFaultScope.Connection,
                    "Received frame belongs to another connection epoch.",
                    ConnectionEpoch,
                    frame.StreamId,
                    frame.EffectiveChannel,
                    frame.Delivery.Kind,
                    frame.StreamSequence,
                    frame.Header.FrameId,
                    CompleteFrameDelivered: true));
        }

        var result = _receiveScheduler.Accept(frame);
        if (result.Status is ConnectionReceiveStatus.Delivered or ConnectionReceiveStatus.Replaced)
        {
            Telemetry.RecordFrameReceived(frame.Header, frame.Delivery, frame.EncodedLength);
        }

        if (result.RepairRequest is not null)
        {
            Telemetry.RecordRepairRequested();
        }

        if (result.Fault?.ProtocolFault is { } protocolFault)
        {
            Telemetry.RecordDecodeFault(protocolFault);
        }

        return result;
    }

    public bool TryDequeueReceived(out Protocol64ReceivedFrame frame)
    {
        EnsureUsable();
        return _receiveScheduler.TryDequeue(out frame);
    }

    public Protocol64RecoveryResult ReportTransportFault(Protocol64TransportFault fault)
    {
        EnsureUsable();
        Telemetry.RecordTransportFault(fault);
        var result = _recovery.ReportFault(fault);
        if (result.RepairRequest is not null)
        {
            Telemetry.RecordRepairRequested();
        }

        if (result.RequiresDisconnect)
        {
            Telemetry.RecordProtocolErrorDisconnect();
        }

        return result;
    }

    public Protocol64RecoveryResult MarkStreamReopened(int streamId)
    {
        EnsureUsable();
        return _recovery.MarkStreamReopened(streamId);
    }

    public Protocol64RecoveryResult MarkRepairCompleted(Guid requestId, int streamId)
    {
        EnsureUsable();
        var result = _recovery.MarkRepairCompleted(requestId, streamId);
        if (result.Accepted)
        {
            Telemetry.RecordRepairCompleted();
        }

        return result;
    }

    public void Close()
    {
        if (_disposed)
        {
            return;
        }

        _recovery.Close();
        _disposed = true;
    }

    public void Dispose() => Close();

    private void EnsureUsable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

internal static class Protocol64ConnectionFrameValidation
{
    public static Protocol64Fault? ValidateOutbound(Protocol64OutboundFrame frame)
    {
        var fault = ValidateEnvelope(frame.EncodedPayload, frame.Header);
        if (fault is not null)
        {
            return fault;
        }

        if (frame.Delivery.Kind == Protocol64DeliveryKind.LastWins)
        {
            return null;
        }

        return frame.Delivery.Channel.HasValue
            ? null
            : Fault(
                Protocol64FaultKind.ValidationFailed,
                "Reliable delivery requires a channel.");
    }

    public static Protocol64Fault? ValidateReceived(Protocol64ReceivedFrame frame)
    {
        if (frame.StreamId < 0 || frame.Lane < 0 || frame.StreamSequence == 0)
        {
            return Fault(
                Protocol64FaultKind.ValidationFailed,
                "Received stream identity and sequence must be non-negative and non-zero.");
        }

        var envelopeFault = ValidateEnvelope(frame.EncodedPayload, frame.Header);
        if (envelopeFault is not null)
        {
            return envelopeFault;
        }

        if (frame.Delivery.Kind != Protocol64DeliveryKind.LastWins && !frame.Delivery.Channel.HasValue)
        {
            return Fault(
                Protocol64FaultKind.ValidationFailed,
                "Reliable delivery requires a channel.");
        }

        return null;
    }

    public static Protocol64Fault Fault(Protocol64FaultKind kind, string message)
        => new(kind, message, Protocol64FaultMetadata.Empty);

    private static Protocol64Fault? ValidateEnvelope(
        ReadOnlyMemory<byte> encodedPayload,
        Protocol64FrameHeader header)
    {
        if (header.ProtocolVersion != Protocol64.Version)
        {
            return Fault(
                Protocol64FaultKind.UnsupportedVersion,
                $"Expected protocol {Protocol64.Version}, received {header.ProtocolVersion}.");
        }

        var payloadSpan = encodedPayload.Span;
        if (payloadSpan.Length < Protocol64FrameHeader.EncodedSize)
        {
            return Fault(
                Protocol64FaultKind.TruncatedFrame,
                $"Protocol-64 frame header is truncated at {payloadSpan.Length} bytes.");
        }

        if (BinaryPrimitives.ReadUInt32LittleEndian(payloadSpan) != Protocol64FrameHeader.Magic)
        {
            return Fault(
                Protocol64FaultKind.InvalidEnvelope,
                "Protocol-64 frame magic does not match the declared complete frame.");
        }

        if (header.EncodedBodyLength > int.MaxValue - Protocol64FrameHeader.EncodedSize)
        {
            return Fault(
                Protocol64FaultKind.OversizedLength,
                "Protocol-64 encoded body length overflows the envelope size.");
        }

        var expectedLength = Protocol64FrameHeader.EncodedSize + checked((int)header.EncodedBodyLength);
        if (encodedPayload.Length < expectedLength)
        {
            return Fault(
                Protocol64FaultKind.TruncatedFrame,
                "The backend supplied fewer bytes than the complete protocol-64 frame declares.");
        }

        if (encodedPayload.Length > expectedLength)
        {
            return Fault(
                Protocol64FaultKind.TrailingBytes,
                "The backend supplied bytes beyond the complete protocol-64 frame.");
        }

        var encodedHeader = new Protocol64FrameHeader(
            BinaryPrimitives.ReadUInt16LittleEndian(payloadSpan[4..]),
            (Protocol64FrameFlags)BinaryPrimitives.ReadUInt16LittleEndian(payloadSpan[6..]),
            BinaryPrimitives.ReadUInt16LittleEndian(payloadSpan[8..]),
            BinaryPrimitives.ReadUInt16LittleEndian(payloadSpan[10..]),
            BinaryPrimitives.ReadUInt64LittleEndian(payloadSpan[12..]),
            BinaryPrimitives.ReadUInt64LittleEndian(payloadSpan[20..]),
            BinaryPrimitives.ReadUInt32LittleEndian(payloadSpan[28..]),
            BinaryPrimitives.ReadUInt32LittleEndian(payloadSpan[32..]),
            BinaryPrimitives.ReadUInt32LittleEndian(payloadSpan[36..]));
        if (encodedHeader != header)
        {
            return Fault(
                Protocol64FaultKind.InvalidEnvelope,
                "Protocol-64 frame metadata does not match the encoded header.");
        }

        var integrityFault = Protocol64FrameCodec.ValidateIntegrity(encodedPayload, header);
        if (integrityFault is not null)
        {
            return integrityFault;
        }

        return null;
    }
}
