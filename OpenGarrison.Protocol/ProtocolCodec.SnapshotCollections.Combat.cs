using System.Collections.Generic;
using System.IO;

namespace OpenGarrison.Protocol;

public static partial class ProtocolCodec
{
    private static void WriteSentryStates(BinaryWriter writer, IReadOnlyList<SnapshotSentryState> sentries)
    {
        writer.Write((ushort)sentries.Count);
        for (var index = 0; index < sentries.Count; index += 1)
        {
            var sentry = sentries[index];
            writer.Write(sentry.Id);
            writer.Write(sentry.OwnerPlayerId);
            writer.Write(sentry.Team);
            writer.Write(sentry.X);
            writer.Write(sentry.Y);
            writer.Write(sentry.Health);
            writer.Write(sentry.IsBuilt);
            writer.Write(sentry.FacingDirectionX);
            writer.Write(sentry.DesiredFacingDirectionX);
            writer.Write(sentry.AimDirectionDegrees);
            writer.Write(sentry.ReloadTicksRemaining);
            writer.Write(sentry.AlertTicksRemaining);
            writer.Write(sentry.ShotTraceTicksRemaining);
            writer.Write(sentry.HasLanded);
            writer.Write(sentry.HasActiveTarget);
            writer.Write(sentry.CurrentTargetPlayerId);
            writer.Write(sentry.LastShotTargetX);
            writer.Write(sentry.LastShotTargetY);
        }
    }

    private static List<SnapshotSentryState> ReadSentryStates(BinaryReader reader)
    {
        var count = reader.ReadUInt16();
        var sentries = new List<SnapshotSentryState>(count);
        for (var index = 0; index < count; index += 1)
        {
            sentries.Add(new SnapshotSentryState(
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadByte(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadInt32(),
                reader.ReadBoolean(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadBoolean(),
                reader.ReadBoolean(),
                reader.ReadInt32(),
                reader.ReadSingle(),
                reader.ReadSingle()));
        }

        return sentries;
    }

    private static void WriteShotStates(BinaryWriter writer, IReadOnlyList<SnapshotShotState> shots)
    {
        writer.Write((ushort)shots.Count);
        for (var index = 0; index < shots.Count; index += 1)
        {
            var shot = shots[index];
            writer.Write(shot.Id);
            writer.Write(shot.Team);
            writer.Write(shot.OwnerId);
            writer.Write(shot.X);
            writer.Write(shot.Y);
            writer.Write(shot.VelocityX);
            writer.Write(shot.VelocityY);
            writer.Write(shot.TicksRemaining);
        }
    }

    private static List<SnapshotShotState> ReadShotStates(BinaryReader reader)
    {
        var count = reader.ReadUInt16();
        var shots = new List<SnapshotShotState>(count);
        for (var index = 0; index < count; index += 1)
        {
            shots.Add(new SnapshotShotState(
                reader.ReadInt32(),
                reader.ReadByte(),
                reader.ReadInt32(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadInt32()));
        }

        return shots;
    }

    private static void WriteRocketStates(BinaryWriter writer, IReadOnlyList<SnapshotRocketState> rockets)
    {
        writer.Write((ushort)rockets.Count);
        for (var index = 0; index < rockets.Count; index += 1)
        {
            var rocket = rockets[index];
            writer.Write(rocket.Id);
            writer.Write(rocket.Team);
            writer.Write(rocket.OwnerId);
            writer.Write(rocket.X);
            writer.Write(rocket.Y);
            writer.Write(rocket.PreviousX);
            writer.Write(rocket.PreviousY);
            writer.Write(rocket.DirectionRadians);
            writer.Write(rocket.Speed);
            writer.Write(rocket.TicksRemaining);
            writer.Write(rocket.ReducedKnockbackSourceTicksRemaining);
            writer.Write(rocket.ZeroKnockbackSourceTicksRemaining);
            writer.Write(rocket.RangeAnchorOwnerId);
            writer.Write(rocket.LastKnownRangeOriginX);
            writer.Write(rocket.LastKnownRangeOriginY);
            writer.Write(rocket.DistanceToTravel);
            writer.Write(rocket.IsFading);
            writer.Write(rocket.FadeSourceTicksRemaining);
            var passedFriendlyPlayerIds = rocket.PassedFriendlyPlayerIds;
            writer.Write((ushort)(passedFriendlyPlayerIds?.Count ?? 0));
            if (passedFriendlyPlayerIds is not null)
            {
                for (var passedIndex = 0; passedIndex < passedFriendlyPlayerIds.Count; passedIndex += 1)
                {
                    writer.Write(passedFriendlyPlayerIds[passedIndex]);
                }
            }
        }
    }

    private static List<SnapshotRocketState> ReadRocketStates(BinaryReader reader)
    {
        var count = reader.ReadUInt16();
        var rockets = new List<SnapshotRocketState>(count);
        for (var index = 0; index < count; index += 1)
        {
            var id = reader.ReadInt32();
            var team = reader.ReadByte();
            var ownerId = reader.ReadInt32();
            var x = reader.ReadSingle();
            var y = reader.ReadSingle();
            var previousX = reader.ReadSingle();
            var previousY = reader.ReadSingle();
            var directionRadians = reader.ReadSingle();
            var speed = reader.ReadSingle();
            var ticksRemaining = reader.ReadInt32();
            var reducedKnockbackSourceTicksRemaining = reader.ReadSingle();
            var zeroKnockbackSourceTicksRemaining = reader.ReadSingle();
            var rangeAnchorOwnerId = reader.ReadInt32();
            var lastKnownRangeOriginX = reader.ReadSingle();
            var lastKnownRangeOriginY = reader.ReadSingle();
            var distanceToTravel = reader.ReadSingle();
            var isFading = reader.ReadBoolean();
            var fadeSourceTicksRemaining = reader.ReadSingle();
            var passedFriendlyCount = reader.ReadUInt16();
            var passedFriendlyPlayerIds = new int[passedFriendlyCount];
            for (var passedIndex = 0; passedIndex < passedFriendlyCount; passedIndex += 1)
            {
                passedFriendlyPlayerIds[passedIndex] = reader.ReadInt32();
            }

            rockets.Add(new SnapshotRocketState(
                id,
                team,
                ownerId,
                x,
                y,
                previousX,
                previousY,
                directionRadians,
                speed,
                ticksRemaining,
                reducedKnockbackSourceTicksRemaining,
                zeroKnockbackSourceTicksRemaining,
                rangeAnchorOwnerId,
                lastKnownRangeOriginX,
                lastKnownRangeOriginY,
                distanceToTravel,
                isFading,
                fadeSourceTicksRemaining,
                passedFriendlyPlayerIds));
        }

        return rockets;
    }

    private static void WriteFlameStates(BinaryWriter writer, IReadOnlyList<SnapshotFlameState> flames)
    {
        writer.Write((ushort)flames.Count);
        for (var index = 0; index < flames.Count; index += 1)
        {
            var flame = flames[index];
            writer.Write(flame.Id);
            writer.Write(flame.Team);
            writer.Write(flame.OwnerId);
            writer.Write(flame.X);
            writer.Write(flame.Y);
            writer.Write(flame.PreviousX);
            writer.Write(flame.PreviousY);
            writer.Write(flame.VelocityX);
            writer.Write(flame.VelocityY);
            writer.Write(flame.TicksRemaining);
            writer.Write(flame.AttachedPlayerId);
            writer.Write(flame.AttachedOffsetX);
            writer.Write(flame.AttachedOffsetY);
        }
    }

    private static List<SnapshotFlameState> ReadFlameStates(BinaryReader reader)
    {
        var count = reader.ReadUInt16();
        var flames = new List<SnapshotFlameState>(count);
        for (var index = 0; index < count; index += 1)
        {
            flames.Add(new SnapshotFlameState(
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
                reader.ReadInt32(),
                reader.ReadSingle(),
                reader.ReadSingle()));
        }

        return flames;
    }

    private static void WriteMineStates(BinaryWriter writer, IReadOnlyList<SnapshotMineState> mines)
    {
        writer.Write((ushort)mines.Count);
        for (var index = 0; index < mines.Count; index += 1)
        {
            var mine = mines[index];
            writer.Write(mine.Id);
            writer.Write(mine.Team);
            writer.Write(mine.OwnerId);
            writer.Write(mine.X);
            writer.Write(mine.Y);
            writer.Write(mine.VelocityX);
            writer.Write(mine.VelocityY);
            writer.Write(mine.IsStickied);
            writer.Write(mine.IsDestroyed);
            writer.Write(mine.ExplosionDamage);
        }
    }

    private static List<SnapshotMineState> ReadMineStates(BinaryReader reader)
    {
        var count = reader.ReadUInt16();
        var mines = new List<SnapshotMineState>(count);
        for (var index = 0; index < count; index += 1)
        {
            mines.Add(new SnapshotMineState(
                reader.ReadInt32(),
                reader.ReadByte(),
                reader.ReadInt32(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadBoolean(),
                reader.ReadBoolean(),
                reader.ReadSingle()));
        }

        return mines;
    }

    private static void WriteCombatTraces(BinaryWriter writer, IReadOnlyList<SnapshotCombatTraceState> combatTraces)
    {
        writer.Write((ushort)combatTraces.Count);
        for (var index = 0; index < combatTraces.Count; index += 1)
        {
            var trace = combatTraces[index];
            writer.Write(trace.StartX);
            writer.Write(trace.StartY);
            writer.Write(trace.EndX);
            writer.Write(trace.EndY);
            writer.Write(trace.TicksRemaining);
            writer.Write(trace.HitCharacter);
            writer.Write(trace.Team);
            writer.Write(trace.IsSniperTracer);
        }
    }

    private static List<SnapshotCombatTraceState> ReadCombatTraces(BinaryReader reader)
    {
        var count = reader.ReadUInt16();
        var combatTraces = new List<SnapshotCombatTraceState>(count);
        for (var index = 0; index < count; index += 1)
        {
            combatTraces.Add(new SnapshotCombatTraceState(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadInt32(),
                reader.ReadBoolean(),
                reader.ReadByte(),
                reader.ReadBoolean()));
        }

        return combatTraces;
    }
}
