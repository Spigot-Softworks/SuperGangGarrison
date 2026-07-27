using OpenGarrison.Protocol;

namespace OpenGarrison.Networking;

/// <summary>
/// A complete protocol-64 frame ready to be handed to a backend. The backend is
/// responsible for putting the frame on the stream identified by the scheduler.
/// </summary>
public sealed record Protocol64OutboundFrame(
    ReadOnlyMemory<byte> EncodedPayload,
    Protocol64FrameHeader Header,
    Protocol64DeliveryDescriptor Delivery,
    string? ReplacementKey = null)
{
    public int EncodedLength => EncodedPayload.Length;

    public ChannelType EffectiveChannel
        => Delivery.Channel ?? ChannelType.State;
}

/// <summary>
/// A complete protocol-64 frame delivered by a backend. StreamSequence is a
/// backend/container sequence, deliberately separate from Protocol64 FrameId:
/// FrameId is global protocol identity while ordering is scoped to a stream.
/// </summary>
public sealed record Protocol64ReceivedFrame(
    ReadOnlyMemory<byte> EncodedPayload,
    Protocol64FrameHeader Header,
    Protocol64DeliveryDescriptor Delivery,
    int StreamId,
    int Lane,
    ulong StreamSequence,
    string? ReplacementKey = null)
{
    public int EncodedLength => EncodedPayload.Length;

    public ChannelType EffectiveChannel
        => Delivery.Channel ?? ChannelType.State;
}

public readonly record struct Protocol64StreamKey(
    ChannelType Channel,
    Protocol64DeliveryKind Delivery,
    int Lane)
{
    public override string ToString()
        => $"{Channel}/{Delivery}/lane-{Lane}";
}

public sealed record Protocol64ScheduledFrame(
    Protocol64OutboundFrame Frame,
    Protocol64StreamKey Stream,
    ulong StreamSequence,
    ulong SchedulerSequence)
{
    public ReadOnlyMemory<byte> EncodedPayload => Frame.EncodedPayload;

    public Protocol64FrameHeader Header => Frame.Header;
}

public enum ConnectionSendStatus : byte
{
    Queued = 1,
    Replaced = 2,
    IgnoredStale = 3,
    Backpressured = 4,
    Rejected = 5,
}

public sealed record ConnectionSendResult(
    ConnectionSendStatus Status,
    Protocol64StreamKey? Stream,
    Protocol64Fault? Fault = null,
    int PendingReliableFrames = 0,
    long PendingReliableBytes = 0)
{
    public bool Accepted => Status is ConnectionSendStatus.Queued or ConnectionSendStatus.Replaced;

    public bool RequiresRetry => Status == ConnectionSendStatus.Backpressured;

    public bool IsReliable => Stream?.Delivery is
        Protocol64DeliveryKind.ReliableOrdered or Protocol64DeliveryKind.ReliableUnordered;

    public static ConnectionSendResult Rejected(Protocol64Fault fault)
        => new(ConnectionSendStatus.Rejected, null, fault);
}

public enum ConnectionReceiveStatus : byte
{
    Delivered = 1,
    BufferedForOrdering = 2,
    Replaced = 3,
    Duplicate = 4,
    Stale = 5,
    RepairRequested = 6,
    Rejected = 7,
}

public sealed record ConnectionReceiveResult(
    ConnectionReceiveStatus Status,
    IReadOnlyList<Protocol64ReceivedFrame> ReleasedFrames,
    Protocol64RepairRequest? RepairRequest = null,
    Protocol64TransportFault? Fault = null)
{
    public bool Accepted => Status is
        ConnectionReceiveStatus.Delivered or
        ConnectionReceiveStatus.BufferedForOrdering or
        ConnectionReceiveStatus.Replaced or
        ConnectionReceiveStatus.Duplicate or
        ConnectionReceiveStatus.Stale or
        ConnectionReceiveStatus.RepairRequested;
}

public enum Protocol64ConnectionState : byte
{
    Healthy = 1,
    Recovering = 2,
    ProtocolError = 3,
    Closed = 4,
}

public enum Protocol64StreamState : byte
{
    Healthy = 1,
    RepairRequested = 2,
    Reopening = 3,
    AwaitingRetransmit = 4,
    ProtocolError = 5,
    Closed = 6,
}
