namespace OpenGarrison.Protocol;

public static partial class ProtocolCodec
{
    private static void WriteLastToDieCommand(BinaryWriter writer, LastToDieCommandMessage value)
    {
        LastToDieProtocolValidation.Validate(value);
        LastToDieProtocolBinary.WriteCommand(writer, value);
    }

    private static LastToDieCommandMessage ReadLastToDieCommand(BinaryReader reader)
        => ValidateDecoded(LastToDieProtocolBinary.ReadCommand(reader), LastToDieProtocolValidation.Validate);

    private static void WriteLastToDieCommandResult(BinaryWriter writer, LastToDieCommandResultMessage value)
    {
        LastToDieProtocolValidation.Validate(value);
        LastToDieProtocolBinary.WriteCommandResult(writer, value);
    }

    private static LastToDieCommandResultMessage ReadLastToDieCommandResult(BinaryReader reader)
        => ValidateDecoded(
            LastToDieProtocolBinary.ReadCommandResult(reader),
            LastToDieProtocolValidation.Validate);

    private static void WriteLastToDieRunSnapshot(BinaryWriter writer, LastToDieRunSnapshotMessage value)
    {
        LastToDieProtocolValidation.Validate(value);
        LastToDieProtocolBinary.WriteRunSnapshot(writer, value);
    }

    private static LastToDieRunSnapshotMessage ReadLastToDieRunSnapshot(BinaryReader reader)
        => ValidateDecoded(
            LastToDieProtocolBinary.ReadRunSnapshot(reader),
            LastToDieProtocolValidation.Validate);

    private static void WriteLastToDieRunSnapshotAck(BinaryWriter writer, LastToDieRunSnapshotAckMessage value)
    {
        LastToDieProtocolValidation.Validate(value);
        LastToDieProtocolBinary.WriteRunSnapshotAck(writer, value);
    }

    private static LastToDieRunSnapshotAckMessage ReadLastToDieRunSnapshotAck(BinaryReader reader)
        => ValidateDecoded(
            LastToDieProtocolBinary.ReadRunSnapshotAck(reader),
            LastToDieProtocolValidation.Validate);

    private static T ValidateDecoded<T>(T value, Action<T> validate)
    {
        try
        {
            validate(value);
            return value;
        }
        catch (Protocol64SchemaValidationException exception)
        {
            throw new IOException(exception.Message, exception);
        }
    }
}
