using System.Collections.Generic;
using System.IO;

namespace OpenGarrison.Protocol;

public static partial class ProtocolCodec
{
    private static void WriteSnapshot(BinaryWriter writer, SnapshotMessage snapshot)
    {
        writer.Write(snapshot.Frame);
        writer.Write(snapshot.BaselineFrame);
        writer.Write(snapshot.IsDelta);
        writer.Write(snapshot.TickRate);
        WriteString(writer, snapshot.LevelName, MaxLevelNameBytes, nameof(snapshot.LevelName));
        writer.Write(snapshot.MapAreaIndex);
        writer.Write(snapshot.MapAreaCount);
        writer.Write(snapshot.IsCustomMap);
        WriteString(writer, snapshot.MapDownloadUrl, MaxMapUrlBytes, nameof(snapshot.MapDownloadUrl));
        WriteString(writer, snapshot.MapContentHash, MaxMapHashBytes, nameof(snapshot.MapContentHash));
        writer.Write(snapshot.GameMode);
        writer.Write(snapshot.MatchPhase);
        writer.Write(snapshot.WinnerTeam);
        writer.Write(snapshot.TimeRemainingTicks);
        writer.Write(snapshot.RedCaps);
        writer.Write(snapshot.BlueCaps);
        writer.Write(snapshot.SpectatorCount);
        writer.Write(snapshot.LastProcessedInputSequence);
        WriteIntelState(writer, snapshot.RedIntel);
        WriteIntelState(writer, snapshot.BlueIntel);
        WriteSnapshotPlayers(writer, snapshot.Players);
        WriteCombatTraces(writer, snapshot.CombatTraces);
        WriteSentryStates(writer, snapshot.Sentries);
        WriteEntityIdList(writer, snapshot.RemovedSentryIds);
        WriteShotStates(writer, snapshot.Shots);
        WriteEntityIdList(writer, snapshot.RemovedShotIds);
        WriteShotStates(writer, snapshot.Bubbles);
        WriteEntityIdList(writer, snapshot.RemovedBubbleIds);
        WriteShotStates(writer, snapshot.Blades);
        WriteEntityIdList(writer, snapshot.RemovedBladeIds);
        WriteShotStates(writer, snapshot.Needles);
        WriteEntityIdList(writer, snapshot.RemovedNeedleIds);
        WriteShotStates(writer, snapshot.RevolverShots);
        WriteEntityIdList(writer, snapshot.RemovedRevolverShotIds);
        WriteRocketStates(writer, snapshot.Rockets);
        WriteEntityIdList(writer, snapshot.RemovedRocketIds);
        WriteFlameStates(writer, snapshot.Flames);
        WriteEntityIdList(writer, snapshot.RemovedFlameIds);
        WriteShotStates(writer, snapshot.Flares);
        WriteEntityIdList(writer, snapshot.RemovedFlareIds);
        WriteMineStates(writer, snapshot.Mines);
        WriteEntityIdList(writer, snapshot.RemovedMineIds);
        WritePlayerGibStates(writer, snapshot.PlayerGibs);
        WriteEntityIdList(writer, snapshot.RemovedPlayerGibIds);
        WriteBloodDropStates(writer, snapshot.BloodDrops);
        WriteEntityIdList(writer, snapshot.RemovedBloodDropIds);
        WriteDeadBodyStates(writer, snapshot.DeadBodies);
        WriteEntityIdList(writer, snapshot.RemovedDeadBodyIds);
        WriteSentryGibStates(writer, snapshot.SentryGibs);
        WriteEntityIdList(writer, snapshot.RemovedSentryGibIds);
        writer.Write(snapshot.ControlPointSetupTicksRemaining);
        WriteControlPointStates(writer, snapshot.ControlPoints);
        WriteGeneratorStates(writer, snapshot.Generators);
        WriteDeathCamState(writer, snapshot.LocalDeathCam);
        WriteKillFeedEntries(writer, snapshot.KillFeed);
        WriteVisualEvents(writer, snapshot.VisualEvents);
        WriteSoundEvents(writer, snapshot.SoundEvents);
    }

    private static SnapshotMessage ReadSnapshot(BinaryReader reader)
    {
        var frame = reader.ReadUInt64();
        var baselineFrame = reader.ReadUInt64();
        var isDelta = reader.ReadBoolean();
        var tickRate = reader.ReadInt32();
        var levelName = ReadString(reader, MaxLevelNameBytes);
        var mapAreaIndex = reader.ReadByte();
        var mapAreaCount = reader.ReadByte();
        var isCustomMap = reader.ReadBoolean();
        var mapDownloadUrl = ReadString(reader, MaxMapUrlBytes);
        var mapContentHash = ReadString(reader, MaxMapHashBytes);
        var gameMode = reader.ReadByte();
        var matchPhase = reader.ReadByte();
        var winnerTeam = reader.ReadByte();
        var timeRemainingTicks = reader.ReadInt32();
        var redCaps = reader.ReadInt32();
        var blueCaps = reader.ReadInt32();
        var spectatorCount = reader.ReadInt32();
        var lastProcessedInputSequence = reader.ReadUInt32();
        var redIntel = ReadIntelState(reader);
        var blueIntel = ReadIntelState(reader);
        var players = ReadSnapshotPlayers(reader);
        var combatTraces = ReadCombatTraces(reader);
        var sentries = ReadSentryStates(reader);
        var removedSentryIds = ReadEntityIdList(reader);
        var shots = ReadShotStates(reader);
        var removedShotIds = ReadEntityIdList(reader);
        var bubbles = ReadShotStates(reader);
        var removedBubbleIds = ReadEntityIdList(reader);
        var blades = ReadShotStates(reader);
        var removedBladeIds = ReadEntityIdList(reader);
        var needles = ReadShotStates(reader);
        var removedNeedleIds = ReadEntityIdList(reader);
        var revolverShots = ReadShotStates(reader);
        var removedRevolverShotIds = ReadEntityIdList(reader);
        var rockets = ReadRocketStates(reader);
        var removedRocketIds = ReadEntityIdList(reader);
        var flames = ReadFlameStates(reader);
        var removedFlameIds = ReadEntityIdList(reader);
        var flares = ReadShotStates(reader);
        var removedFlareIds = ReadEntityIdList(reader);
        var mines = ReadMineStates(reader);
        var removedMineIds = ReadEntityIdList(reader);
        var playerGibs = ReadPlayerGibStates(reader);
        var removedPlayerGibIds = ReadEntityIdList(reader);
        var bloodDrops = ReadBloodDropStates(reader);
        var removedBloodDropIds = ReadEntityIdList(reader);
        var deadBodies = ReadDeadBodyStates(reader);
        var removedDeadBodyIds = ReadEntityIdList(reader);
        var sentryGibs = ReadSentryGibStates(reader);
        var removedSentryGibIds = ReadEntityIdList(reader);
        var controlPointSetupTicksRemaining = reader.ReadInt32();
        var controlPoints = ReadControlPointStates(reader);
        var generators = ReadGeneratorStates(reader);
        var deathCam = ReadDeathCamState(reader);
        var killFeed = ReadKillFeedEntries(reader);
        var visualEvents = ReadVisualEvents(reader);
        var soundEvents = ReadSoundEvents(reader);

        return new SnapshotMessage(
            frame,
            tickRate,
            levelName,
            mapAreaIndex,
            mapAreaCount,
            gameMode,
            matchPhase,
            winnerTeam,
            timeRemainingTicks,
            redCaps,
            blueCaps,
            spectatorCount,
            lastProcessedInputSequence,
            redIntel,
            blueIntel,
            players,
            combatTraces,
            sentries,
            shots,
            bubbles,
            blades,
            needles,
            revolverShots,
            rockets,
            flames,
            flares,
            mines,
            playerGibs,
            bloodDrops,
            deadBodies,
            controlPointSetupTicksRemaining,
            controlPoints,
            generators,
            deathCam,
            killFeed,
            visualEvents,
            soundEvents,
            isCustomMap,
            mapDownloadUrl,
            mapContentHash)
        {
            BaselineFrame = baselineFrame,
            IsDelta = isDelta,
            RemovedSentryIds = removedSentryIds,
            RemovedShotIds = removedShotIds,
            RemovedBubbleIds = removedBubbleIds,
            RemovedBladeIds = removedBladeIds,
            RemovedNeedleIds = removedNeedleIds,
            RemovedRevolverShotIds = removedRevolverShotIds,
            RemovedRocketIds = removedRocketIds,
            RemovedFlameIds = removedFlameIds,
            RemovedFlareIds = removedFlareIds,
            RemovedMineIds = removedMineIds,
            RemovedPlayerGibIds = removedPlayerGibIds,
            RemovedBloodDropIds = removedBloodDropIds,
            RemovedDeadBodyIds = removedDeadBodyIds,
            SentryGibs = sentryGibs,
            RemovedSentryGibIds = removedSentryGibIds,
        };
    }

    private static void WriteEntityIdList(BinaryWriter writer, IReadOnlyList<int> ids)
    {
        writer.Write((ushort)ids.Count);
        for (var index = 0; index < ids.Count; index += 1)
        {
            writer.Write(ids[index]);
        }
    }

    private static List<int> ReadEntityIdList(BinaryReader reader)
    {
        var count = reader.ReadUInt16();
        var ids = new List<int>(count);
        for (var index = 0; index < count; index += 1)
        {
            ids.Add(reader.ReadInt32());
        }

        return ids;
    }

    private static void WriteSnapshotPlayers(BinaryWriter writer, IReadOnlyList<SnapshotPlayerState> players)
    {
        writer.Write((byte)players.Count);
        for (var index = 0; index < players.Count; index += 1)
        {
            var player = players[index];
            writer.Write(player.Slot);
            writer.Write(player.PlayerId);
            WriteString(writer, player.Name, MaxPlayerNameBytes, nameof(player.Name));
            writer.Write(player.Team);
            writer.Write(player.ClassId);
            writer.Write(player.IsAlive);
            writer.Write(player.IsAwaitingJoin);
            writer.Write(player.IsSpectator);
            writer.Write(player.RespawnTicks);
            writer.Write(player.X);
            writer.Write(player.Y);
            writer.Write(player.HorizontalSpeed);
            writer.Write(player.VerticalSpeed);
            writer.Write(player.Health);
            writer.Write(player.MaxHealth);
            writer.Write(player.Ammo);
            writer.Write(player.MaxAmmo);
            writer.Write(player.Kills);
            writer.Write(player.Deaths);
            writer.Write(player.Caps);
            writer.Write(player.HealPoints);
            writer.Write(player.ActiveDominationCount);
            writer.Write(player.IsDominatingLocalViewer);
            writer.Write(player.IsDominatedByLocalViewer);
            writer.Write(player.Metal);
            writer.Write(player.IsGrounded);
            writer.Write(player.RemainingAirJumps);
            writer.Write(player.IsCarryingIntel);
            writer.Write(player.IntelRechargeTicks);
            writer.Write(player.IsSpyCloaked);
            writer.Write(player.SpyCloakAlpha);
            writer.Write(player.IsUbered);
            writer.Write(player.IsHeavyEating);
            writer.Write(player.HeavyEatTicksRemaining);
            writer.Write(player.IsSniperScoped);
            writer.Write(player.SniperChargeTicks);
            writer.Write(player.FacingDirectionX);
            writer.Write(player.AimDirectionDegrees);
            writer.Write(player.IsTaunting);
            writer.Write(player.TauntFrameIndex);
            writer.Write(player.IsChatBubbleVisible);
            writer.Write(player.ChatBubbleFrameIndex);
            writer.Write(player.ChatBubbleAlpha);
            writer.Write(player.BurnIntensity);
            writer.Write(player.BurnDurationSourceTicks);
            writer.Write(player.BurnDecayDelaySourceTicksRemaining);
            writer.Write(player.BurnIntensityDecayPerSourceTick);
            writer.Write(player.BurnedByPlayerId);
            writer.Write(player.MovementState);
            writer.Write(player.PrimaryCooldownTicks);
            writer.Write(player.ReloadTicksUntilNextShell);
            writer.Write(player.MedicNeedleCooldownTicks);
            writer.Write(player.MedicNeedleRefillTicks);
            writer.Write(player.PyroAirblastCooldownTicks);
            writer.Write(player.PyroFlareCooldownTicks);
            writer.Write(player.PyroPrimaryFuelScaled);
            writer.Write(player.IsPyroPrimaryRefilling);
            writer.Write(player.PyroFlameLoopTicksRemaining);
            writer.Write(player.PyroPrimaryRequiresReleaseAfterEmpty);
            writer.Write(player.HeavyEatCooldownTicksRemaining);
        }
    }

    private static List<SnapshotPlayerState> ReadSnapshotPlayers(BinaryReader reader)
    {
        var playerCount = reader.ReadByte();
        var players = new List<SnapshotPlayerState>(playerCount);
        for (var index = 0; index < playerCount; index += 1)
        {
            players.Add(new SnapshotPlayerState(
                reader.ReadByte(),
                reader.ReadInt32(),
                ReadString(reader, MaxPlayerNameBytes),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadBoolean(),
                reader.ReadBoolean(),
                reader.ReadBoolean(),
                reader.ReadInt32(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadBoolean(),
                reader.ReadBoolean(),
                reader.ReadSingle(),
                reader.ReadBoolean(),
                reader.ReadInt32(),
                reader.ReadBoolean(),
                reader.ReadSingle(),
                reader.ReadBoolean(),
                reader.ReadSingle(),
                reader.ReadBoolean(),
                reader.ReadBoolean(),
                reader.ReadInt32(),
                reader.ReadBoolean(),
                reader.ReadInt32(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadBoolean(),
                reader.ReadSingle(),
                reader.ReadBoolean(),
                reader.ReadInt32(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadInt32(),
                reader.ReadByte(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadBoolean(),
                reader.ReadInt32(),
                reader.ReadBoolean(),
                reader.ReadInt32()));
        }

        return players;
    }
}
