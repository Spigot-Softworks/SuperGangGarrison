using System.Collections.Generic;
using System.IO;

namespace OpenGarrison.Protocol;

public static partial class ProtocolCodec
{
    private static void WriteSentryGibStates(BinaryWriter writer, IReadOnlyList<SnapshotSentryGibState> sentryGibs)
    {
        writer.Write((ushort)sentryGibs.Count);
        for (var index = 0; index < sentryGibs.Count; index += 1)
        {
            var sentryGib = sentryGibs[index];
            writer.Write(sentryGib.Id);
            writer.Write(sentryGib.Team);
            writer.Write(sentryGib.X);
            writer.Write(sentryGib.Y);
            writer.Write(sentryGib.TicksRemaining);
        }
    }

    private static List<SnapshotSentryGibState> ReadSentryGibStates(BinaryReader reader)
    {
        var count = reader.ReadUInt16();
        var sentryGibs = new List<SnapshotSentryGibState>(count);
        for (var index = 0; index < count; index += 1)
        {
            sentryGibs.Add(new SnapshotSentryGibState(
                reader.ReadInt32(),
                reader.ReadByte(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadInt32()));
        }

        return sentryGibs;
    }

    private static void WriteDeadBodyStates(BinaryWriter writer, IReadOnlyList<SnapshotDeadBodyState> deadBodies)
    {
        writer.Write((ushort)deadBodies.Count);
        for (var index = 0; index < deadBodies.Count; index += 1)
        {
            var deadBody = deadBodies[index];
            writer.Write(deadBody.Id);
            writer.Write(deadBody.SourcePlayerId);
            writer.Write(deadBody.Team);
            writer.Write(deadBody.ClassId);
            writer.Write(deadBody.AnimationKind);
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
                reader.ReadInt32(),
                reader.ReadByte(),
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

    private static void WriteGibSpawnEvents(BinaryWriter writer, IReadOnlyList<SnapshotGibSpawnEvent> gibSpawnEvents)
    {
        writer.Write((ushort)gibSpawnEvents.Count);
        for (var index = 0; index < gibSpawnEvents.Count; index += 1)
        {
            var e = gibSpawnEvents[index];
            WriteString(writer, e.SpriteName, MaxAssetNameBytes, nameof(e.SpriteName));
            writer.Write(e.FrameIndex);
            writer.Write(e.X);
            writer.Write(e.Y);
            writer.Write(e.VelocityX);
            writer.Write(e.VelocityY);
            writer.Write(e.RotationSpeedDegrees);
            writer.Write(e.HorizontalFriction);
            writer.Write(e.RotationFriction);
            writer.Write(e.LifetimeTicks);
            writer.Write(e.BloodChance);
            writer.Write(e.EventId);
        }
    }

    private static List<SnapshotGibSpawnEvent> ReadGibSpawnEvents(BinaryReader reader)
    {
        var count = reader.ReadUInt16();
        var events = new List<SnapshotGibSpawnEvent>(count);
        for (var index = 0; index < count; index += 1)
        {
            events.Add(new SnapshotGibSpawnEvent(
                ReadString(reader, MaxAssetNameBytes),
                reader.ReadInt32(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadInt32(),
                reader.ReadSingle(),
                reader.ReadUInt64()));
        }
        return events;
    }

    private static void WriteRocketSpawnEvents(BinaryWriter writer, IReadOnlyList<SnapshotRocketSpawnEvent> rocketSpawnEvents)
    {
        writer.Write((ushort)rocketSpawnEvents.Count);
        for (var index = 0; index < rocketSpawnEvents.Count; index += 1)
        {
            var e = rocketSpawnEvents[index];
            writer.Write(e.Id);
            writer.Write(e.Team);
            writer.Write(e.OwnerId);
            writer.Write(e.X);
            writer.Write(e.Y);
            writer.Write(e.PreviousX);
            writer.Write(e.PreviousY);
            writer.Write(e.DirectionRadians);
            writer.Write(e.Speed);
            writer.Write(e.TicksRemaining);
            writer.Write(e.ReducedKnockbackSourceTicksRemaining);
            writer.Write(e.ZeroKnockbackSourceTicksRemaining);
            writer.Write(e.RangeAnchorOwnerId);
            writer.Write(e.LastKnownRangeOriginX);
            writer.Write(e.LastKnownRangeOriginY);
            writer.Write(e.DistanceToTravel);
            writer.Write(e.IsFading);
            writer.Write(e.FadeSourceTicksRemaining);
            writer.Write(e.ExplodeImmediately);
            writer.Write(e.IsCritical);
            writer.Write(e.EventId);
            WriteEntityIdList(writer, e.PassedFriendlyPlayerIds ?? []);
        }
    }

    private static List<SnapshotRocketSpawnEvent> ReadRocketSpawnEvents(BinaryReader reader)
    {
        var count = reader.ReadUInt16();
        var events = new List<SnapshotRocketSpawnEvent>(count);
        for (var index = 0; index < count; index += 1)
        {
            events.Add(new SnapshotRocketSpawnEvent(
                reader.ReadInt32(),
                reader.ReadByte(),
                reader.ReadInt32(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadInt32(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadInt32(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadBoolean(),
                reader.ReadSingle(),
                reader.ReadBoolean(),
                reader.ReadBoolean(),
                reader.ReadUInt64(),
                ReadEntityIdList(reader)));
        }

        return events;
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

    private static void WriteDamageEvents(BinaryWriter writer, IReadOnlyList<SnapshotDamageEvent> damageEvents)
    {
        writer.Write((ushort)damageEvents.Count);
        for (var index = 0; index < damageEvents.Count; index += 1)
        {
            var damageEvent = damageEvents[index];
            writer.Write(damageEvent.Amount);
            writer.Write(damageEvent.AttackerPlayerId);
            writer.Write(damageEvent.AssistedByPlayerId);
            writer.Write(damageEvent.TargetKind);
            writer.Write(damageEvent.TargetEntityId);
            writer.Write(damageEvent.X);
            writer.Write(damageEvent.Y);
            writer.Write(damageEvent.WasFatal);
            writer.Write(damageEvent.EventId);
            writer.Write(damageEvent.SourceFrame);
            writer.Write(damageEvent.Flags);
        }
    }

    private static List<SnapshotDamageEvent> ReadDamageEvents(BinaryReader reader)
    {
        var count = reader.ReadUInt16();
        var damageEvents = new List<SnapshotDamageEvent>(count);
        for (var index = 0; index < count; index += 1)
        {
            damageEvents.Add(new SnapshotDamageEvent(
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadByte(),
                reader.ReadInt32(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadBoolean(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadByte()));
        }

        return damageEvents;
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

    private static void WriteJumpPadStates(BinaryWriter writer, IReadOnlyList<SnapshotJumpPadState> jumpPads)
    {
        writer.Write((ushort)jumpPads.Count);
        for (var index = 0; index < jumpPads.Count; index += 1)
        {
            var pad = jumpPads[index];
            writer.Write(pad.Id);
            writer.Write(pad.OwnerPlayerId);
            writer.Write(pad.Team);
            writer.Write(pad.X);
            writer.Write(pad.Y);
            writer.Write(pad.Health);
            writer.Write(pad.HasLanded);
            writer.Write(pad.IsBuilt);
        }
    }

    private static List<SnapshotJumpPadState> ReadJumpPadStates(BinaryReader reader)
    {
        var count = reader.ReadUInt16();
        var jumpPads = new List<SnapshotJumpPadState>(count);
        for (var index = 0; index < count; index += 1)
        {
            jumpPads.Add(new SnapshotJumpPadState(
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadByte(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadInt32(),
                reader.ReadBoolean(),
                reader.ReadBoolean()));
        }

        return jumpPads;
    }

    private static void WriteJumpPadGibStates(BinaryWriter writer, IReadOnlyList<SnapshotJumpPadGibState> jumpPadGibs)
    {
        writer.Write((ushort)jumpPadGibs.Count);
        for (var index = 0; index < jumpPadGibs.Count; index += 1)
        {
            var gib = jumpPadGibs[index];
            writer.Write(gib.Id);
            writer.Write(gib.Team);
            writer.Write(gib.X);
            writer.Write(gib.Y);
            writer.Write(gib.TicksRemaining);
        }
    }

    private static List<SnapshotJumpPadGibState> ReadJumpPadGibStates(BinaryReader reader)
    {
        var count = reader.ReadUInt16();
        var jumpPadGibs = new List<SnapshotJumpPadGibState>(count);
        for (var index = 0; index < count; index += 1)
        {
            jumpPadGibs.Add(new SnapshotJumpPadGibState(
                reader.ReadInt32(),
                reader.ReadByte(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadInt32()));
        }

        return jumpPadGibs;
    }
}
