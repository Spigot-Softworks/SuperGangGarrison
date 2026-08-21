using System.Text;

namespace OpenGarrison.Protocol;

public enum LastToDieCommandKind : byte
{
    RequestStart = 1,
    ChooseSurvivor = 2,
    SelectReward = 3,
    Ready = 4,
    Unready = 5,
    StageContentReady = 6,
    Leave = 7,
    Retry = 8,
    ReturnToLobby = 9,
}

public enum LastToDieCommandResultKind : byte
{
    Accepted = 1,
    Rejected = 2,
    Duplicate = 3,
}

public enum LastToDieWireDifficulty : byte
{
    Standard = 0,
    Hardcore = 1,
}

public enum LastToDieWirePhase : byte
{
    Lobby = 0,
    SurvivorChoice = 1,
    RewardChoice = 2,
    LoadingStage = 3,
    Playing = 4,
    Won = 5,
    Lost = 6,
}

public sealed record LastToDieCommandMessage(
    ulong CommandId,
    Guid RunId,
    ulong ExpectedStructuralRevision,
    LastToDieCommandKind Kind,
    ulong StageInstanceId = 0,
    ulong OfferId = 0,
    string SelectedId = "") : IProtocolMessage
{
    public MessageType Type => MessageType.LastToDieCommand;
}

public sealed record LastToDieCommandResultMessage(
    ulong CommandId,
    LastToDieCommandResultKind Result,
    ulong AuthoritativeStructuralRevision,
    string Reason = "") : IProtocolMessage
{
    public MessageType Type => MessageType.LastToDieCommandResult;
}

public sealed record LastToDiePlayerSnapshotMessage(
    byte Slot,
    Guid PlayerId,
    bool IsConnected,
    string SurvivorId,
    IReadOnlyList<string> OwnedPerkIds,
    ulong ActiveOfferId,
    int ActiveOfferOrdinal,
    IReadOnlyList<string> ActiveOfferChoices,
    bool IsReady,
    bool IsAlive,
    int Kills,
    bool IsHost = false,
    int ConquistadorStacks = 0,
    long ReconnectGraceEndServerTick = 0);

public sealed record LastToDieRunSnapshotMessage(
    Guid RunId,
    ulong StructuralRevision,
    ulong Seed,
    int RulesetVersion,
    LastToDieWireDifficulty Difficulty,
    LastToDieWirePhase Phase,
    long ServerTick,
    int StageNumber,
    ulong StageInstanceId,
    string CurrentMap,
    int EnemyCount,
    long StageEndServerTick,
    long RunEndServerTick,
    IReadOnlyList<LastToDiePlayerSnapshotMessage> Players,
    string TerminalReason = "",
    ulong BaselineStartFrame = 0,
    byte MaximumPlayers = 2) : IProtocolMessage
{
    public MessageType Type => MessageType.LastToDieRunSnapshot;
}

public sealed record LastToDieRunSnapshotAckMessage(
    Guid RunId,
    ulong StructuralRevision) : IProtocolMessage
{
    public MessageType Type => MessageType.LastToDieRunSnapshotAck;
}

public static class LastToDieProtocolSchemaIds
{
    public const ushort Command = 40;
    public const ushort CommandResult = 41;
    public const ushort RunSnapshot = 42;
    public const ushort RunSnapshotAck = 43;
}

[ReliableOrdered(ChannelType.Control)]
public sealed class LastToDieCommandSchema : Protocol64EventSchema<LastToDieCommandMessage>
{
    public LastToDieCommandSchema()
        : base(LastToDieProtocolSchemaIds.Command, 1, Protocol64Direction.ClientToServer, 512)
    {
    }

    public override void WriteBody(LastToDieCommandMessage eventValue, BinaryWriter writer)
        => LastToDieProtocolBinary.WriteCommand(writer, eventValue);

    public override LastToDieCommandMessage ReadBody(BinaryReader reader)
        => LastToDieProtocolBinary.ReadCommand(reader);

    public override void Validate(LastToDieCommandMessage eventValue)
        => LastToDieProtocolValidation.Validate(eventValue);
}

[ReliableOrdered(ChannelType.Control)]
public sealed class LastToDieCommandResultSchema : Protocol64EventSchema<LastToDieCommandResultMessage>
{
    public LastToDieCommandResultSchema()
        : base(LastToDieProtocolSchemaIds.CommandResult, 1, Protocol64Direction.ServerToClient, 512)
    {
    }

    public override void WriteBody(LastToDieCommandResultMessage eventValue, BinaryWriter writer)
        => LastToDieProtocolBinary.WriteCommandResult(writer, eventValue);

    public override LastToDieCommandResultMessage ReadBody(BinaryReader reader)
        => LastToDieProtocolBinary.ReadCommandResult(reader);

    public override void Validate(LastToDieCommandResultMessage eventValue)
        => LastToDieProtocolValidation.Validate(eventValue);
}

[ReliableOrdered(ChannelType.GameplayEvents)]
public sealed class LastToDieRunSnapshotSchema : Protocol64EventSchema<LastToDieRunSnapshotMessage>
{
    public const int MaxBodyBytes = 32 * 1024;

    public LastToDieRunSnapshotSchema()
        : base(LastToDieProtocolSchemaIds.RunSnapshot, 4, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }

    public override void WriteBody(LastToDieRunSnapshotMessage eventValue, BinaryWriter writer)
        => LastToDieProtocolBinary.WriteRunSnapshot(writer, eventValue);

    public override LastToDieRunSnapshotMessage ReadBody(BinaryReader reader)
        => LastToDieProtocolBinary.ReadRunSnapshot(reader);

    public override void Validate(LastToDieRunSnapshotMessage eventValue)
        => LastToDieProtocolValidation.Validate(eventValue);
}

[ReliableOrdered(ChannelType.Control)]
public sealed class LastToDieRunSnapshotAckSchema : Protocol64EventSchema<LastToDieRunSnapshotAckMessage>
{
    public LastToDieRunSnapshotAckSchema()
        : base(LastToDieProtocolSchemaIds.RunSnapshotAck, 1, Protocol64Direction.ClientToServer, 32)
    {
    }

    public override void WriteBody(LastToDieRunSnapshotAckMessage eventValue, BinaryWriter writer)
        => LastToDieProtocolBinary.WriteRunSnapshotAck(writer, eventValue);

    public override LastToDieRunSnapshotAckMessage ReadBody(BinaryReader reader)
        => LastToDieProtocolBinary.ReadRunSnapshotAck(reader);

    public override void Validate(LastToDieRunSnapshotAckMessage eventValue)
        => LastToDieProtocolValidation.Validate(eventValue);
}

internal static class LastToDieProtocolValidation
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public const int MaximumPlayers = 2;
    public const int MaximumOwnedPerks = 128;
    public const int MaximumOfferChoices = 3;
    public const int MaximumStableIdBytes = 96;
    public const int MaximumMapNameBytes = 64;
    public const int MaximumReasonBytes = 128;

    public static void Validate(LastToDieCommandMessage value)
    {
        if (value.CommandId == 0 || value.RunId == Guid.Empty || value.ExpectedStructuralRevision == 0)
        {
            throw new Protocol64SchemaValidationException(
                "Last to Die command identity, run, and expected revision must be non-zero.");
        }

        if (!Enum.IsDefined(value.Kind))
        {
            throw new Protocol64SchemaValidationException(
                $"Unknown Last to Die command kind {(byte)value.Kind}.");
        }

        ValidateString(value.SelectedId, MaximumStableIdBytes, "selected ID");
    }

    public static void Validate(LastToDieCommandResultMessage value)
    {
        if (value.CommandId == 0 || value.AuthoritativeStructuralRevision == 0)
        {
            throw new Protocol64SchemaValidationException(
                "Last to Die command result identity and revision must be non-zero.");
        }

        if (!Enum.IsDefined(value.Result))
        {
            throw new Protocol64SchemaValidationException(
                $"Unknown Last to Die command result kind {(byte)value.Result}.");
        }

        ValidateString(value.Reason, MaximumReasonBytes, "command result reason");
    }

    public static void Validate(LastToDieRunSnapshotMessage value)
    {
        if (value.RunId == Guid.Empty || value.StructuralRevision == 0 || value.RulesetVersion <= 0)
        {
            throw new Protocol64SchemaValidationException(
                "Last to Die run identity, revision, and ruleset version must be valid.");
        }

        if (!Enum.IsDefined(value.Difficulty) || !Enum.IsDefined(value.Phase))
        {
            throw new Protocol64SchemaValidationException("Last to Die run enum value is invalid.");
        }

        if (value.ServerTick < 0
            || value.StageNumber < 0
            || value.EnemyCount < 0
            || value.StageEndServerTick < 0
            || value.RunEndServerTick < 0)
        {
            throw new Protocol64SchemaValidationException("Last to Die run counters cannot be negative.");
        }

        if (value.MaximumPlayers is < 1 or > MaximumPlayers)
        {
            throw new Protocol64SchemaValidationException("Last to Die maximum player count is invalid.");
        }

        ValidateString(value.CurrentMap, MaximumMapNameBytes, "map name");
        ValidateString(value.TerminalReason, MaximumReasonBytes, "terminal reason");
        if (value.Players is null || value.Players.Count > MaximumPlayers)
        {
            throw new Protocol64SchemaValidationException("Last to Die player collection is invalid.");
        }

        var slots = new HashSet<byte>();
        var playerIds = new HashSet<Guid>();
        foreach (var player in value.Players)
        {
            if (player is null
                || player.Slot == 0
                || player.PlayerId == Guid.Empty
                || !slots.Add(player.Slot)
                || !playerIds.Add(player.PlayerId)
                || player.Kills < 0
                || player.ConquistadorStacks < 0
                || player.ConquistadorStacks > 100
                || player.ReconnectGraceEndServerTick < 0)
            {
                throw new Protocol64SchemaValidationException(
                    "Last to Die player identity or counters are invalid.");
            }

            ValidateString(player.SurvivorId, MaximumStableIdBytes, "survivor ID");
            ValidateStableIdCollection(player.OwnedPerkIds, MaximumOwnedPerks, "owned perks");
            ValidateStableIdCollection(player.ActiveOfferChoices, MaximumOfferChoices, "offer choices");
            if ((player.ActiveOfferId == 0 && player.ActiveOfferChoices.Count != 0)
                || (player.ActiveOfferId != 0 && player.ActiveOfferChoices.Count == 0)
                || player.ActiveOfferOrdinal < 0)
            {
                throw new Protocol64SchemaValidationException("Last to Die reward offer is invalid.");
            }
        }
    }

    public static void Validate(LastToDieRunSnapshotAckMessage value)
    {
        if (value.RunId == Guid.Empty || value.StructuralRevision == 0)
        {
            throw new Protocol64SchemaValidationException(
                "Last to Die snapshot acknowledgement identity must be non-zero.");
        }
    }

    private static void ValidateStableIdCollection(
        IReadOnlyList<string>? values,
        int maximumCount,
        string fieldName)
    {
        if (values is null || values.Count > maximumCount)
        {
            throw new Protocol64SchemaValidationException(
                $"Last to Die {fieldName} collection is invalid.");
        }

        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            ValidateString(value, MaximumStableIdBytes, fieldName);
            if (string.IsNullOrWhiteSpace(value) || !unique.Add(value))
            {
                throw new Protocol64SchemaValidationException(
                    $"Last to Die {fieldName} must contain unique non-empty IDs.");
            }
        }
    }

    private static void ValidateString(string? value, int maximumBytes, string fieldName)
    {
        if (value is null || StrictUtf8.GetByteCount(value) > maximumBytes)
        {
            throw new Protocol64SchemaValidationException(
                $"Last to Die {fieldName} exceeds its protocol limit.");
        }
    }
}

internal static class LastToDieProtocolBinary
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static void WriteCommand(BinaryWriter writer, LastToDieCommandMessage value)
    {
        writer.Write(value.CommandId);
        writer.Write(value.RunId.ToByteArray());
        writer.Write(value.ExpectedStructuralRevision);
        writer.Write((byte)value.Kind);
        writer.Write(value.StageInstanceId);
        writer.Write(value.OfferId);
        WriteString(writer, value.SelectedId, LastToDieProtocolValidation.MaximumStableIdBytes);
    }

    public static LastToDieCommandMessage ReadCommand(BinaryReader reader)
        => new(
            reader.ReadUInt64(),
            ReadGuid(reader),
            reader.ReadUInt64(),
            (LastToDieCommandKind)reader.ReadByte(),
            reader.ReadUInt64(),
            reader.ReadUInt64(),
            ReadString(reader, LastToDieProtocolValidation.MaximumStableIdBytes));

    public static void WriteCommandResult(BinaryWriter writer, LastToDieCommandResultMessage value)
    {
        writer.Write(value.CommandId);
        writer.Write((byte)value.Result);
        writer.Write(value.AuthoritativeStructuralRevision);
        WriteString(writer, value.Reason, LastToDieProtocolValidation.MaximumReasonBytes);
    }

    public static LastToDieCommandResultMessage ReadCommandResult(BinaryReader reader)
        => new(
            reader.ReadUInt64(),
            (LastToDieCommandResultKind)reader.ReadByte(),
            reader.ReadUInt64(),
            ReadString(reader, LastToDieProtocolValidation.MaximumReasonBytes));

    public static void WriteRunSnapshot(BinaryWriter writer, LastToDieRunSnapshotMessage value)
    {
        writer.Write(value.RunId.ToByteArray());
        writer.Write(value.StructuralRevision);
        writer.Write(value.Seed);
        writer.Write(value.RulesetVersion);
        writer.Write((byte)value.Difficulty);
        writer.Write((byte)value.Phase);
        writer.Write(value.ServerTick);
        writer.Write(value.StageNumber);
        writer.Write(value.StageInstanceId);
        WriteString(writer, value.CurrentMap, LastToDieProtocolValidation.MaximumMapNameBytes);
        writer.Write(value.EnemyCount);
        writer.Write(value.StageEndServerTick);
        writer.Write(value.RunEndServerTick);
        writer.Write((byte)value.Players.Count);
        foreach (var player in value.Players)
        {
            writer.Write(player.Slot);
            writer.Write(player.PlayerId.ToByteArray());
            writer.Write(player.IsConnected);
            WriteString(writer, player.SurvivorId, LastToDieProtocolValidation.MaximumStableIdBytes);
            WriteStrings(writer, player.OwnedPerkIds, LastToDieProtocolValidation.MaximumOwnedPerks);
            writer.Write(player.ActiveOfferId);
            writer.Write(player.ActiveOfferOrdinal);
            WriteStrings(writer, player.ActiveOfferChoices, LastToDieProtocolValidation.MaximumOfferChoices);
            writer.Write(player.IsReady);
            writer.Write(player.IsAlive);
            writer.Write(player.Kills);
            writer.Write(player.IsHost);
            writer.Write((byte)player.ConquistadorStacks);
            writer.Write(player.ReconnectGraceEndServerTick);
        }

        WriteString(writer, value.TerminalReason, LastToDieProtocolValidation.MaximumReasonBytes);
        writer.Write(value.BaselineStartFrame);
        writer.Write(value.MaximumPlayers);
    }

    public static LastToDieRunSnapshotMessage ReadRunSnapshot(BinaryReader reader)
    {
        var runId = ReadGuid(reader);
        var structuralRevision = reader.ReadUInt64();
        var seed = reader.ReadUInt64();
        var rulesetVersion = reader.ReadInt32();
        var difficulty = (LastToDieWireDifficulty)reader.ReadByte();
        var phase = (LastToDieWirePhase)reader.ReadByte();
        var serverTick = reader.ReadInt64();
        var stageNumber = reader.ReadInt32();
        var stageInstanceId = reader.ReadUInt64();
        var currentMap = ReadString(reader, LastToDieProtocolValidation.MaximumMapNameBytes);
        var enemyCount = reader.ReadInt32();
        var stageEndServerTick = reader.ReadInt64();
        var runEndServerTick = reader.ReadInt64();
        var playerCount = reader.ReadByte();
        if (playerCount > LastToDieProtocolValidation.MaximumPlayers)
        {
            throw new IOException("Last to Die player collection exceeds its protocol limit.");
        }

        var players = new LastToDiePlayerSnapshotMessage[playerCount];
        for (var index = 0; index < players.Length; index += 1)
        {
            players[index] = new LastToDiePlayerSnapshotMessage(
                reader.ReadByte(),
                ReadGuid(reader),
                reader.ReadBoolean(),
                ReadString(reader, LastToDieProtocolValidation.MaximumStableIdBytes),
                ReadStrings(reader, LastToDieProtocolValidation.MaximumOwnedPerks),
                reader.ReadUInt64(),
                reader.ReadInt32(),
                ReadStrings(reader, LastToDieProtocolValidation.MaximumOfferChoices),
                reader.ReadBoolean(),
                reader.ReadBoolean(),
                reader.ReadInt32(),
                reader.ReadBoolean(),
                reader.ReadByte(),
                reader.ReadInt64());
        }

        var terminalReason = ReadString(reader, LastToDieProtocolValidation.MaximumReasonBytes);
        var baselineStartFrame = reader.ReadUInt64();
        var maximumPlayers = reader.ReadByte();
        return new LastToDieRunSnapshotMessage(
            runId,
            structuralRevision,
            seed,
            rulesetVersion,
            difficulty,
            phase,
            serverTick,
            stageNumber,
            stageInstanceId,
            currentMap,
            enemyCount,
            stageEndServerTick,
            runEndServerTick,
            players,
            terminalReason,
            baselineStartFrame,
            maximumPlayers);
    }

    public static void WriteRunSnapshotAck(BinaryWriter writer, LastToDieRunSnapshotAckMessage value)
    {
        writer.Write(value.RunId.ToByteArray());
        writer.Write(value.StructuralRevision);
    }

    public static LastToDieRunSnapshotAckMessage ReadRunSnapshotAck(BinaryReader reader)
        => new(ReadGuid(reader), reader.ReadUInt64());

    private static void WriteStrings(BinaryWriter writer, IReadOnlyList<string> values, int maximumCount)
    {
        if (values.Count > maximumCount)
        {
            throw new Protocol64SchemaValidationException(
                "Last to Die string collection exceeds its protocol limit.");
        }

        writer.Write((byte)values.Count);
        foreach (var value in values)
        {
            WriteString(writer, value, LastToDieProtocolValidation.MaximumStableIdBytes);
        }
    }

    private static IReadOnlyList<string> ReadStrings(BinaryReader reader, int maximumCount)
    {
        var count = reader.ReadByte();
        if (count > maximumCount)
        {
            throw new IOException("Last to Die string collection exceeds its protocol limit.");
        }

        var values = new string[count];
        for (var index = 0; index < values.Length; index += 1)
        {
            values[index] = ReadString(reader, LastToDieProtocolValidation.MaximumStableIdBytes);
        }

        return values;
    }

    private static void WriteString(BinaryWriter writer, string value, int maximumBytes)
    {
        var bytes = StrictUtf8.GetBytes(value ?? string.Empty);
        if (bytes.Length > maximumBytes)
        {
            throw new Protocol64SchemaValidationException(
                "Last to Die string exceeds its protocol limit.");
        }

        writer.Write((ushort)bytes.Length);
        writer.Write(bytes);
    }

    private static string ReadString(BinaryReader reader, int maximumBytes)
    {
        var length = reader.ReadUInt16();
        if (length > maximumBytes)
        {
            throw new IOException("Last to Die string exceeds its protocol limit.");
        }

        var bytes = reader.ReadBytes(length);
        if (bytes.Length != length)
        {
            throw new EndOfStreamException();
        }

        return StrictUtf8.GetString(bytes);
    }

    private static Guid ReadGuid(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(16);
        if (bytes.Length != 16)
        {
            throw new EndOfStreamException();
        }

        return new Guid(bytes);
    }
}
