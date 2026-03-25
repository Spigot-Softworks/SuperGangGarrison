using System.Collections.Generic;
using System.IO;

namespace OpenGarrison.Protocol;

public static partial class ProtocolCodec
{
    private static void WriteKillFeedEntries(BinaryWriter writer, IReadOnlyList<SnapshotKillFeedEntry> killFeed)
    {
        writer.Write((byte)killFeed.Count);
        for (var index = 0; index < killFeed.Count; index += 1)
        {
            var entry = killFeed[index];
            WriteString(writer, entry.KillerName, MaxPlayerNameBytes, nameof(entry.KillerName));
            writer.Write(entry.KillerTeam);
            WriteString(writer, entry.WeaponSpriteName, MaxAssetNameBytes, nameof(entry.WeaponSpriteName));
            WriteString(writer, entry.VictimName, MaxPlayerNameBytes, nameof(entry.VictimName));
            writer.Write(entry.VictimTeam);
            WriteString(writer, entry.MessageText, MaxKillMessageBytes, nameof(entry.MessageText));
            writer.Write(entry.KillerPlayerId);
            writer.Write(entry.VictimPlayerId);
            writer.Write((byte)entry.SpecialType);
            writer.Write(entry.EventId);
        }
    }

    private static List<SnapshotKillFeedEntry> ReadKillFeedEntries(BinaryReader reader)
    {
        var count = reader.ReadByte();
        var killFeed = new List<SnapshotKillFeedEntry>(count);
        for (var index = 0; index < count; index += 1)
        {
            killFeed.Add(new SnapshotKillFeedEntry(
                ReadString(reader, MaxPlayerNameBytes),
                reader.ReadByte(),
                ReadString(reader, MaxAssetNameBytes),
                ReadString(reader, MaxPlayerNameBytes),
                reader.ReadByte(),
                ReadString(reader, MaxKillMessageBytes),
                reader.ReadInt32(),
                reader.ReadInt32(),
                (KillFeedSpecialType)reader.ReadByte(),
                reader.ReadUInt64()));
        }

        return killFeed;
    }

    private static void WriteIntelState(BinaryWriter writer, SnapshotIntelState intel)
    {
        writer.Write(intel.Team);
        writer.Write(intel.X);
        writer.Write(intel.Y);
        writer.Write(intel.IsAtBase);
        writer.Write(intel.IsDropped);
        writer.Write(intel.ReturnTicksRemaining);
    }

    private static SnapshotIntelState ReadIntelState(BinaryReader reader)
    {
        return new SnapshotIntelState(
            reader.ReadByte(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadBoolean(),
            reader.ReadBoolean(),
            reader.ReadInt32());
    }

    private static void WriteControlPointStates(BinaryWriter writer, IReadOnlyList<SnapshotControlPointState> controlPoints)
    {
        writer.Write((byte)controlPoints.Count);
        for (var index = 0; index < controlPoints.Count; index += 1)
        {
            var point = controlPoints[index];
            writer.Write(point.Index);
            writer.Write(point.Team);
            writer.Write(point.CappingTeam);
            writer.Write(point.CappingTicks);
            writer.Write(point.CapTimeTicks);
            writer.Write(point.Cappers);
            writer.Write(point.IsLocked);
        }
    }

    private static List<SnapshotControlPointState> ReadControlPointStates(BinaryReader reader)
    {
        var count = reader.ReadByte();
        var points = new List<SnapshotControlPointState>(count);
        for (var index = 0; index < count; index += 1)
        {
            points.Add(new SnapshotControlPointState(
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadByte(),
                reader.ReadBoolean()));
        }

        return points;
    }

    private static void WriteGeneratorStates(BinaryWriter writer, IReadOnlyList<SnapshotGeneratorState> generators)
    {
        writer.Write((byte)generators.Count);
        for (var index = 0; index < generators.Count; index += 1)
        {
            var generator = generators[index];
            writer.Write(generator.Team);
            writer.Write(generator.Health);
            writer.Write(generator.MaxHealth);
        }
    }

    private static List<SnapshotGeneratorState> ReadGeneratorStates(BinaryReader reader)
    {
        var count = reader.ReadByte();
        var generators = new List<SnapshotGeneratorState>(count);
        for (var index = 0; index < count; index += 1)
        {
            generators.Add(new SnapshotGeneratorState(
                reader.ReadByte(),
                reader.ReadInt16(),
                reader.ReadInt16()));
        }

        return generators;
    }

    private static void WriteDeathCamState(BinaryWriter writer, SnapshotDeathCamState? deathCam)
    {
        writer.Write(deathCam is not null);
        if (deathCam is null)
        {
            return;
        }

        writer.Write(deathCam.FocusX);
        writer.Write(deathCam.FocusY);
        WriteString(writer, deathCam.KillMessage, MaxKillMessageBytes, nameof(deathCam.KillMessage));
        WriteString(writer, deathCam.KillerName, MaxPlayerNameBytes, nameof(deathCam.KillerName));
        writer.Write(deathCam.KillerTeam);
        writer.Write(deathCam.Health);
        writer.Write(deathCam.MaxHealth);
        writer.Write(deathCam.RemainingTicks);
        writer.Write(deathCam.InitialTicks);
    }

    private static SnapshotDeathCamState? ReadDeathCamState(BinaryReader reader)
    {
        if (!reader.ReadBoolean())
        {
            return null;
        }

        return new SnapshotDeathCamState(
            reader.ReadSingle(),
            reader.ReadSingle(),
            ReadString(reader, MaxKillMessageBytes),
            ReadString(reader, MaxPlayerNameBytes),
            reader.ReadByte(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32());
    }
}
