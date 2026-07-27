using System;
using System.IO;

namespace OpenGarrison.Protocol;

public enum Protocol64TransportRepairReason : byte
{
    MissingReliableFrame = 1,
    MalformedCompleteFrame = 2,
    StreamReset = 3,
    StreamClosed = 4,
}

/// <summary>
/// Backend-neutral control event used to request a replacement for a frame or
/// stream range. The malformed bytes are never echoed into the control stream.
/// </summary>
public sealed record Protocol64RetransmitRequest(
    Guid RequestId,
    ulong ConnectionEpoch,
    Protocol64TransportRepairReason Reason,
    int StreamId,
    ChannelType Channel,
    Protocol64DeliveryKind Delivery,
    ulong? MissingSequenceFrom,
    ulong? MissingSequenceTo,
    ulong? OffendingFrameId,
    bool ExcludeOffendingFrame,
    int Lane = 0);

/// <summary>
/// Transport metadata sent as the first frame on a dedicated retransmit
/// stream. QUIC streams are independent, so the receiver needs the original
/// logical lane and sequence range before it can feed the replacement into its
/// backend-neutral ordering scheduler. It is consumed by the backend and is
/// never exposed as gameplay.
/// </summary>
public sealed record Protocol64RetransmitResponse(
    Guid RequestId,
    ulong ConnectionEpoch,
    bool Available,
    int RecoveryStreamId,
    ChannelType Channel,
    Protocol64DeliveryKind Delivery,
    int Lane,
    ulong SequenceFrom,
    ulong SequenceTo);

[ReliableOrdered(ChannelType.Control)]
public sealed class Protocol64RetransmitRequestSchema
    : Protocol64EventSchema<Protocol64RetransmitRequest>
{
    public const int MaxBodyBytes = 128;

    public Protocol64RetransmitRequestSchema()
        : base((ushort)Protocol64EventId.RetransmitRequest, 1, Protocol64Direction.Bidirectional, MaxBodyBytes)
    {
    }

    public override void WriteBody(Protocol64RetransmitRequest value, BinaryWriter writer)
    {
        Validate(value);
        writer.Write(value.RequestId.ToByteArray());
        writer.Write(value.ConnectionEpoch);
        writer.Write((byte)value.Reason);
        writer.Write(value.StreamId);
        writer.Write((byte)value.Channel);
        writer.Write((byte)value.Delivery);
        WriteOptional(writer, value.MissingSequenceFrom);
        WriteOptional(writer, value.MissingSequenceTo);
        WriteOptional(writer, value.OffendingFrameId);
        writer.Write(value.ExcludeOffendingFrame);
        writer.Write(value.Lane);
    }

    public override Protocol64RetransmitRequest ReadBody(BinaryReader reader)
        => new(
            new Guid(ReadGuid(reader)),
            reader.ReadUInt64(),
            (Protocol64TransportRepairReason)reader.ReadByte(),
            reader.ReadInt32(),
            (ChannelType)reader.ReadByte(),
            (Protocol64DeliveryKind)reader.ReadByte(),
            ReadOptional(reader),
            ReadOptional(reader),
            ReadOptional(reader),
            reader.ReadBoolean(),
            reader.ReadInt32());

    public override void Validate(Protocol64RetransmitRequest value)
    {
        if (value is null || value.RequestId == Guid.Empty || value.ConnectionEpoch == 0 || value.StreamId < 0 || value.Lane < 0 ||
            !Enum.IsDefined(value.Reason) || !Enum.IsDefined(value.Channel) || !Enum.IsDefined(value.Delivery))
        {
            throw new Protocol64SchemaValidationException("Protocol-64 retransmit request identity or delivery is invalid.");
        }

        if (value.MissingSequenceFrom is 0 || value.MissingSequenceTo is 0)
        {
            throw new Protocol64SchemaValidationException("Protocol-64 retransmit sequence bounds must be non-zero when present.");
        }

        if (value.MissingSequenceFrom.HasValue && value.MissingSequenceTo.HasValue &&
            value.MissingSequenceFrom.Value > value.MissingSequenceTo.Value)
        {
            throw new Protocol64SchemaValidationException("Protocol-64 retransmit sequence bounds are reversed.");
        }
    }

    private static byte[] ReadGuid(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(16);
        if (bytes.Length != 16)
        {
            throw new EndOfStreamException("Protocol-64 retransmit request ended inside its request ID.");
        }

        return bytes;
    }

    private static void WriteOptional(BinaryWriter writer, ulong? value)
    {
        writer.Write(value.HasValue);
        if (value.HasValue)
        {
            writer.Write(value.Value);
        }
    }

    private static ulong? ReadOptional(BinaryReader reader)
        => reader.ReadBoolean() ? reader.ReadUInt64() : null;
}

[ReliableOrdered(ChannelType.Control)]
public sealed class Protocol64RetransmitResponseSchema
    : Protocol64EventSchema<Protocol64RetransmitResponse>
{
    public const int MaxBodyBytes = 128;

    public Protocol64RetransmitResponseSchema()
        : base((ushort)Protocol64EventId.RetransmitResponse, 1, Protocol64Direction.Bidirectional, MaxBodyBytes)
    {
    }

    public override void WriteBody(Protocol64RetransmitResponse value, BinaryWriter writer)
    {
        Validate(value);
        writer.Write(value.RequestId.ToByteArray());
        writer.Write(value.ConnectionEpoch);
        writer.Write(value.Available);
        writer.Write(value.RecoveryStreamId);
        writer.Write((byte)value.Channel);
        writer.Write((byte)value.Delivery);
        writer.Write(value.Lane);
        writer.Write(value.SequenceFrom);
        writer.Write(value.SequenceTo);
    }

    public override Protocol64RetransmitResponse ReadBody(BinaryReader reader)
        => new(
            new Guid(ReadGuid(reader)),
            reader.ReadUInt64(),
            reader.ReadBoolean(),
            reader.ReadInt32(),
            (ChannelType)reader.ReadByte(),
            (Protocol64DeliveryKind)reader.ReadByte(),
            reader.ReadInt32(),
            reader.ReadUInt64(),
            reader.ReadUInt64());

    public override void Validate(Protocol64RetransmitResponse value)
    {
        if (value is null || value.RequestId == Guid.Empty || value.ConnectionEpoch == 0 ||
            value.RecoveryStreamId < 0 || value.Lane < 0 || value.SequenceFrom == 0 ||
            value.SequenceTo < value.SequenceFrom || !Enum.IsDefined(value.Channel) ||
            !Enum.IsDefined(value.Delivery))
        {
            throw new Protocol64SchemaValidationException("Protocol-64 retransmit response metadata is invalid.");
        }
    }

    private static byte[] ReadGuid(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(16);
        if (bytes.Length != 16)
        {
            throw new EndOfStreamException("Protocol-64 retransmit response ended inside its request ID.");
        }

        return bytes;
    }
}
