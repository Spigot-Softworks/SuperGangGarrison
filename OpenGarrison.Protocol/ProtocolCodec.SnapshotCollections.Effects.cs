using System.Collections.Generic;
using System.IO;

namespace OpenGarrison.Protocol;

public static partial class ProtocolCodec
{
    private static void WriteDeadBodyStates(BinaryWriter writer, IReadOnlyList<SnapshotDeadBodyState> deadBodies)
    {
        writer.Write((ushort)deadBodies.Count);
        for (var index = 0; index < deadBodies.Count; index += 1)
        {
            var deadBody = deadBodies[index];
            writer.Write(deadBody.Id);
            writer.Write(deadBody.Team);
            writer.Write(deadBody.ClassId);
            writer.Write(deadBody.X);
            writer.Write(deadBody.Y);
            writer.Write(deadBody.Width);
            writer.Write(deadBody.Height);
            writer.Write(deadBody.HorizontalSpeed);
            writer.Write(deadBody.VerticalSpeed);
            writer.Write(deadBody.FacingLeft);
            writer.Write(deadBody.TicksRemaining);
        }
    }

    private static List<SnapshotDeadBodyState> ReadDeadBodyStates(BinaryReader reader)
    {
        var count = reader.ReadUInt16();
        var deadBodies = new List<SnapshotDeadBodyState>(count);
        for (var index = 0; index < count; index += 1)
        {
            deadBodies.Add(new SnapshotDeadBodyState(
                reader.ReadInt32(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadBoolean(),
                reader.ReadInt32()));
        }

        return deadBodies;
    }

    private static void WritePlayerGibStates(BinaryWriter writer, IReadOnlyList<SnapshotPlayerGibState> playerGibs)
    {
        writer.Write((ushort)playerGibs.Count);
        for (var index = 0; index < playerGibs.Count; index += 1)
        {
            var gib = playerGibs[index];
            writer.Write(gib.Id);
            WriteString(writer, gib.SpriteName, MaxAssetNameBytes, nameof(gib.SpriteName));
            writer.Write(gib.FrameIndex);
            writer.Write(gib.X);
            writer.Write(gib.Y);
            writer.Write(gib.VelocityX);
            writer.Write(gib.VelocityY);
            writer.Write(gib.RotationDegrees);
            writer.Write(gib.RotationSpeedDegrees);
            writer.Write(gib.TicksRemaining);
            writer.Write(gib.BloodChance);
        }
    }

    private static List<SnapshotPlayerGibState> ReadPlayerGibStates(BinaryReader reader)
    {
        var count = reader.ReadUInt16();
        var playerGibs = new List<SnapshotPlayerGibState>(count);
        for (var index = 0; index < count; index += 1)
        {
            playerGibs.Add(new SnapshotPlayerGibState(
                reader.ReadInt32(),
                ReadString(reader, MaxAssetNameBytes),
                reader.ReadInt32(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadInt32(),
                reader.ReadSingle()));
        }

        return playerGibs;
    }

    private static void WriteBloodDropStates(BinaryWriter writer, IReadOnlyList<SnapshotBloodDropState> bloodDrops)
    {
        writer.Write((ushort)bloodDrops.Count);
        for (var index = 0; index < bloodDrops.Count; index += 1)
        {
            var bloodDrop = bloodDrops[index];
            writer.Write(bloodDrop.Id);
            writer.Write(bloodDrop.X);
            writer.Write(bloodDrop.Y);
            writer.Write(bloodDrop.VelocityX);
            writer.Write(bloodDrop.VelocityY);
            writer.Write(bloodDrop.IsStuck);
            writer.Write(bloodDrop.TicksRemaining);
            writer.Write(bloodDrop.Scale);
        }
    }

    private static List<SnapshotBloodDropState> ReadBloodDropStates(BinaryReader reader)
    {
        var count = reader.ReadUInt16();
        var bloodDrops = new List<SnapshotBloodDropState>(count);
        for (var index = 0; index < count; index += 1)
        {
            bloodDrops.Add(new SnapshotBloodDropState(
                reader.ReadInt32(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadBoolean(),
                reader.ReadInt32(),
                reader.ReadSingle()));
        }

        return bloodDrops;
    }

    private static void WriteSoundEvents(BinaryWriter writer, IReadOnlyList<SnapshotSoundEvent> soundEvents)
    {
        writer.Write((ushort)soundEvents.Count);
        for (var index = 0; index < soundEvents.Count; index += 1)
        {
            var soundEvent = soundEvents[index];
            WriteString(writer, soundEvent.SoundName, MaxAssetNameBytes, nameof(soundEvent.SoundName));
            writer.Write(soundEvent.X);
            writer.Write(soundEvent.Y);
            writer.Write(soundEvent.EventId);
            writer.Write(soundEvent.SourceFrame);
        }
    }

    private static List<SnapshotSoundEvent> ReadSoundEvents(BinaryReader reader)
    {
        var count = reader.ReadUInt16();
        var soundEvents = new List<SnapshotSoundEvent>(count);
        for (var index = 0; index < count; index += 1)
        {
            soundEvents.Add(new SnapshotSoundEvent(
                ReadString(reader, MaxAssetNameBytes),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadUInt64(),
                reader.ReadUInt64()));
        }

        return soundEvents;
    }

    private static void WriteVisualEvents(BinaryWriter writer, IReadOnlyList<SnapshotVisualEvent> visualEvents)
    {
        writer.Write((ushort)visualEvents.Count);
        for (var index = 0; index < visualEvents.Count; index += 1)
        {
            var visualEvent = visualEvents[index];
            WriteString(writer, visualEvent.EffectName, MaxAssetNameBytes, nameof(visualEvent.EffectName));
            writer.Write(visualEvent.X);
            writer.Write(visualEvent.Y);
            writer.Write(visualEvent.DirectionDegrees);
            writer.Write(visualEvent.Count);
            writer.Write(visualEvent.EventId);
        }
    }

    private static List<SnapshotVisualEvent> ReadVisualEvents(BinaryReader reader)
    {
        var count = reader.ReadUInt16();
        var visualEvents = new List<SnapshotVisualEvent>(count);
        for (var index = 0; index < count; index += 1)
        {
            visualEvents.Add(new SnapshotVisualEvent(
                ReadString(reader, MaxAssetNameBytes),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadInt32(),
                reader.ReadUInt64()));
        }

        return visualEvents;
    }
}
