using System;
using System.IO;

namespace OpenGarrison.Protocol;

public enum Protocol64InputCommandKind : byte
{
    Jump = 1,
    BuildSentry = 2,
    DestroySentry = 3,
    Taunt = 4,
    FirePrimary = 5,
    FireSecondary = 6,
    DebugKill = 7,
    DropIntel = 8,
    UseAbility = 9,
    InteractWeapon = 10,
    SwapWeapon = 11,
    ReadyUp = 12,
    BuildDispenser = 13,
    DestroyDispenser = 14,
}

public enum Protocol64InputCommandResultKind : byte
{
    Consumed = 1,
    Rejected = 2,
    Duplicate = 3,
}

/// <summary>
/// A one-shot input transition. Held buttons remain in InputState; transitions
/// use this event so a short press has a durable identity and cannot disappear
/// between two latest-state samples.
/// </summary>
public sealed record Protocol64InputCommand(
    ulong CommandId,
    uint InputSequence,
    Protocol64InputCommandKind Kind,
    InputButtons HeldButtons,
    float AimRelX,
    float AimRelY,
    uint ClientTick = 0,
    // Command ordering is a separate stream from the latest-state input
    // sequence. Multiple one-shot commands can belong to one input frame.
    uint CommandSequence = 0);

public sealed record Protocol64InputCommandResult(
    ulong CommandId,
    uint InputSequence,
    Protocol64InputCommandResultKind Result,
    uint ServerTick,
    string Reason = "",
    uint CommandSequence = 0);

public sealed record Protocol64InputCommandResultAck(ulong CommandId);

public static class Protocol64InputSchemaIds
{
    public const ushort InputCommand = 29;
    public const ushort InputCommandResult = 30;
    public const ushort InputCommandResultAck = 31;
}

[ReliableOrdered(ChannelType.Input)]
public sealed class Protocol64InputCommandSchema
    : Protocol64EventSchema<Protocol64InputCommand>
{
    public Protocol64InputCommandSchema()
        : base(
            Protocol64InputSchemaIds.InputCommand,
            revision: 3,
            Protocol64Direction.ClientToServer,
            maxBodyBytes: 64)
    {
    }

    public override void WriteBody(Protocol64InputCommand value, BinaryWriter writer)
    {
        writer.Write(value.CommandId);
        writer.Write(value.InputSequence);
        writer.Write((byte)value.Kind);
        writer.Write((uint)value.HeldButtons);
        writer.Write(value.AimRelX);
        writer.Write(value.AimRelY);
        writer.Write(value.ClientTick);
        writer.Write(value.CommandSequence);
    }

    public override Protocol64InputCommand ReadBody(BinaryReader reader)
    {
        return new Protocol64InputCommand(
            reader.ReadUInt64(),
            reader.ReadUInt32(),
            (Protocol64InputCommandKind)reader.ReadByte(),
            (InputButtons)reader.ReadUInt32(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadUInt32(),
            reader.ReadUInt32());
    }

    public override void Validate(Protocol64InputCommand value)
    {
        if (value.CommandId == 0)
        {
            throw new Protocol64SchemaValidationException("Input command ID must be non-zero.");
        }

        if (!Enum.IsDefined(value.Kind))
        {
            throw new Protocol64SchemaValidationException($"Unknown input command kind {(byte)value.Kind}.");
        }

        if (!float.IsFinite(value.AimRelX) || !float.IsFinite(value.AimRelY))
        {
            throw new Protocol64SchemaValidationException("Input command aim must be finite.");
        }
    }
}

[ReliableOrdered(ChannelType.Input)]
public sealed class Protocol64InputCommandResultSchema
    : Protocol64EventSchema<Protocol64InputCommandResult>
{
    public Protocol64InputCommandResultSchema()
        : base(
            Protocol64InputSchemaIds.InputCommandResult,
            revision: 2,
            Protocol64Direction.ServerToClient,
            maxBodyBytes: 256)
    {
    }

    public override void WriteBody(Protocol64InputCommandResult value, BinaryWriter writer)
    {
        writer.Write(value.CommandId);
        writer.Write(value.InputSequence);
        writer.Write((byte)value.Result);
        writer.Write(value.ServerTick);
        writer.Write(value.Reason ?? string.Empty);
        writer.Write(value.CommandSequence);
    }

    public override Protocol64InputCommandResult ReadBody(BinaryReader reader)
    {
        return new Protocol64InputCommandResult(
            reader.ReadUInt64(),
            reader.ReadUInt32(),
            (Protocol64InputCommandResultKind)reader.ReadByte(),
            reader.ReadUInt32(),
            reader.ReadString(),
            reader.ReadUInt32());
    }

    public override void Validate(Protocol64InputCommandResult value)
    {
        if (value.CommandId == 0)
        {
            throw new Protocol64SchemaValidationException("Input command result ID must be non-zero.");
        }

        if (!Enum.IsDefined(value.Result))
        {
            throw new Protocol64SchemaValidationException($"Unknown input command result {(byte)value.Result}.");
        }

        if (value.Reason is null || value.Reason.Length > 128)
        {
            throw new Protocol64SchemaValidationException("Input command result reason is too long.");
        }
    }
}

[ReliableOrdered(ChannelType.Input)]
public sealed class Protocol64InputCommandResultAckSchema
    : Protocol64EventSchema<Protocol64InputCommandResultAck>
{
    public Protocol64InputCommandResultAckSchema()
        : base(
            Protocol64InputSchemaIds.InputCommandResultAck,
            revision: 1,
            Protocol64Direction.ClientToServer,
            maxBodyBytes: 8)
    {
    }

    public override void WriteBody(Protocol64InputCommandResultAck value, BinaryWriter writer)
        => writer.Write(value.CommandId);

    public override Protocol64InputCommandResultAck ReadBody(BinaryReader reader)
        => new(reader.ReadUInt64());

    public override void Validate(Protocol64InputCommandResultAck value)
    {
        if (value.CommandId == 0)
        {
            throw new Protocol64SchemaValidationException("Input command result acknowledgement ID must be non-zero.");
        }
    }
}
