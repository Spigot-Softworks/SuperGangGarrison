using System;
using System.Collections.Generic;
using System.IO;

namespace OpenGarrison.Protocol;

public static partial class ProtocolCodec
{
    private const float QuantizedAimDegreesMax = 360f;
    private const float QuantizedMetalScale = 10f;
    private const float QuantizedIntelRechargeScale = 4f;
    private const float QuantizedChatBubbleAlphaScale = 255f;

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
        writer.Write(snapshot.MapScale);
        writer.Write(snapshot.GameMode);
        writer.Write(snapshot.MatchPhase);
        writer.Write(snapshot.WinnerTeam);
        writer.Write(snapshot.TimeRemainingTicks);
        writer.Write(snapshot.TimeLimitTicks);
        writer.Write(snapshot.RedCaps);
        writer.Write(snapshot.BlueCaps);
        writer.Write(snapshot.SpectatorCount);
        writer.Write(snapshot.LastProcessedInputSequence);
        WriteIntelState(writer, snapshot.RedIntel);
        WriteIntelState(writer, snapshot.BlueIntel);
        WriteSnapshotPlayers(writer, snapshot.Players);
        WriteSnapshotPlayerMovementStates(writer, snapshot.PlayerMovementStates);
        WriteSnapshotPlayerStatusStates(writer, snapshot.PlayerStatusStates);
        WriteSnapshotPlayerChatBubbleStates(writer, snapshot.PlayerChatBubbleStates);
        WriteEntityIdList(writer, snapshot.RemovedPlayerIds);
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
        WriteJumpPadStates(writer, snapshot.JumpPads);
        WriteEntityIdList(writer, snapshot.RemovedJumpPadIds);
        writer.Write(snapshot.ControlPointSetupTicksRemaining);
        writer.Write(snapshot.KothUnlockTicksRemaining);
        writer.Write(snapshot.KothRedTimerTicksRemaining);
        writer.Write(snapshot.KothBlueTimerTicksRemaining);
        writer.Write(snapshot.ArenaUnlockTicksRemaining);
        writer.Write(snapshot.ArenaPointTeam);
        writer.Write(snapshot.ArenaCappingTeam);
        writer.Write(snapshot.ArenaCappingTicks);
        writer.Write(snapshot.ArenaCappers);
        writer.Write(snapshot.ArenaRedConsecutiveWins);
        writer.Write(snapshot.ArenaBlueConsecutiveWins);
        WriteControlPointStates(writer, snapshot.ControlPoints);
        WriteGeneratorStates(writer, snapshot.Generators);
        WriteDeathCamState(writer, snapshot.LocalDeathCam);
        WriteKillFeedEntries(writer, snapshot.KillFeed);
        WriteVisualEvents(writer, snapshot.VisualEvents);
        WriteDamageEvents(writer, snapshot.DamageEvents);
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
        var mapScale = reader.ReadSingle();
        var gameMode = reader.ReadByte();
        var matchPhase = reader.ReadByte();
        var winnerTeam = reader.ReadByte();
        var timeRemainingTicks = reader.ReadInt32();
        var timeLimitTicks = reader.ReadInt32();
        var redCaps = reader.ReadInt32();
        var blueCaps = reader.ReadInt32();
        var spectatorCount = reader.ReadInt32();
        var lastProcessedInputSequence = reader.ReadUInt32();
        var redIntel = ReadIntelState(reader);
        var blueIntel = ReadIntelState(reader);
        var players = ReadSnapshotPlayers(reader);
        var playerMovementStates = ReadSnapshotPlayerMovementStates(reader);
        var playerStatusStates = ReadSnapshotPlayerStatusStates(reader);
        var playerChatBubbleStates = ReadSnapshotPlayerChatBubbleStates(reader);
        var removedPlayerIds = ReadEntityIdList(reader);
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
        var jumpPads = ReadJumpPadStates(reader);
        var removedJumpPadIds = ReadEntityIdList(reader);
        var controlPointSetupTicksRemaining = reader.ReadInt32();
        var kothUnlockTicksRemaining = reader.ReadInt32();
        var kothRedTimerTicksRemaining = reader.ReadInt32();
        var kothBlueTimerTicksRemaining = reader.ReadInt32();
        var arenaUnlockTicksRemaining = reader.ReadInt32();
        var arenaPointTeam = reader.ReadByte();
        var arenaCappingTeam = reader.ReadByte();
        var arenaCappingTicks = reader.ReadSingle();
        var arenaCappers = reader.ReadInt32();
        var arenaRedConsecutiveWins = reader.ReadInt32();
        var arenaBlueConsecutiveWins = reader.ReadInt32();
        var controlPoints = ReadControlPointStates(reader);
        var generators = ReadGeneratorStates(reader);
        var deathCam = ReadDeathCamState(reader);
        var killFeed = ReadKillFeedEntries(reader);
        var visualEvents = ReadVisualEvents(reader);
        var damageEvents = ReadDamageEvents(reader);
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
            kothUnlockTicksRemaining,
            kothRedTimerTicksRemaining,
            kothBlueTimerTicksRemaining,
            controlPoints,
            generators,
            deathCam,
            killFeed,
            visualEvents,
            damageEvents,
            soundEvents,
            isCustomMap,
            mapDownloadUrl,
            mapContentHash,
            mapScale)
        {
            TimeLimitTicks = timeLimitTicks,
            ArenaUnlockTicksRemaining = arenaUnlockTicksRemaining,
            ArenaPointTeam = arenaPointTeam,
            ArenaCappingTeam = arenaCappingTeam,
            ArenaCappingTicks = arenaCappingTicks,
            ArenaCappers = arenaCappers,
            ArenaRedConsecutiveWins = arenaRedConsecutiveWins,
            ArenaBlueConsecutiveWins = arenaBlueConsecutiveWins,
            BaselineFrame = baselineFrame,
            IsDelta = isDelta,
            PlayerMovementStates = playerMovementStates,
            PlayerStatusStates = playerStatusStates,
            PlayerChatBubbleStates = playerChatBubbleStates,
            RemovedPlayerIds = removedPlayerIds,
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
            JumpPads = jumpPads,
            RemovedJumpPadIds = removedJumpPadIds,
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
            writer.Write(player.Points);
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
            writer.Write(player.Assists);
            writer.Write(player.BadgeMask);
            WriteString(writer, player.GameplayModPackId, MaxGameplayIdBytes, nameof(player.GameplayModPackId));
            WriteString(writer, player.GameplayLoadoutId, MaxGameplayIdBytes, nameof(player.GameplayLoadoutId));
            WriteString(writer, player.GameplayPrimaryItemId, MaxGameplayIdBytes, nameof(player.GameplayPrimaryItemId));
            WriteString(writer, player.GameplaySecondaryItemId, MaxGameplayIdBytes, nameof(player.GameplaySecondaryItemId));
            WriteString(writer, player.GameplayUtilityItemId, MaxGameplayIdBytes, nameof(player.GameplayUtilityItemId));
            writer.Write(player.GameplayEquippedSlot);
            WriteString(writer, player.GameplayEquippedItemId, MaxGameplayIdBytes, nameof(player.GameplayEquippedItemId));
            WriteString(writer, player.GameplayAcquiredItemId, MaxGameplayIdBytes, nameof(player.GameplayAcquiredItemId));
            WriteGameplayIdList(writer, player.OwnedGameplayItemIds);
            WriteReplicatedStateEntries(writer, player.ReplicatedStates);
            writer.Write(player.PlayerScale);
            writer.Write(player.AimWorldX);
            writer.Write(player.AimWorldY);
            writer.Write(player.MedicHealTargetPlayerId);
            writer.Write(player.IsMedicHealing);
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
                reader.ReadSingle(),
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
                reader.ReadInt32(),
                reader.ReadInt16(),
                reader.ReadUInt64(),
                ReadString(reader, MaxGameplayIdBytes),
                ReadString(reader, MaxGameplayIdBytes),
                ReadString(reader, MaxGameplayIdBytes),
                ReadString(reader, MaxGameplayIdBytes),
                ReadString(reader, MaxGameplayIdBytes),
                reader.ReadByte(),
                ReadString(reader, MaxGameplayIdBytes),
                ReadString(reader, MaxGameplayIdBytes),
                ReadGameplayIdList(reader),
                ReadReplicatedStateEntries(reader),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadInt32(),
                reader.ReadBoolean()));
        }

        return players;
    }

    private static void WriteSnapshotPlayerMovementStates(
        BinaryWriter writer,
        IReadOnlyList<SnapshotPlayerMovementState> states)
    {
        writer.Write((byte)states.Count);
        for (var index = 0; index < states.Count; index += 1)
        {
            var state = states[index];
            writer.Write(state.Slot);
            writer.Write(state.X);
            writer.Write(state.Y);
            writer.Write(state.HorizontalSpeed);
            writer.Write(state.VerticalSpeed);
            writer.Write(GetMovementFlags(state.IsGrounded, state.FacingDirectionX < 0f, state.IsMedicHealing));
            writer.Write((byte)Math.Clamp(state.RemainingAirJumps, 0, byte.MaxValue));
            writer.Write(QuantizeAngleDegrees(state.AimDirectionDegrees));
            writer.Write(state.MovementState);
            writer.Write(state.MedicHealTargetPlayerId);
        }
    }

    private static List<SnapshotPlayerMovementState> ReadSnapshotPlayerMovementStates(BinaryReader reader)
    {
        var stateCount = reader.ReadByte();
        var states = new List<SnapshotPlayerMovementState>(stateCount);
        for (var index = 0; index < stateCount; index += 1)
        {
            var slot = reader.ReadByte();
            var x = reader.ReadSingle();
            var y = reader.ReadSingle();
            var horizontalSpeed = reader.ReadSingle();
            var verticalSpeed = reader.ReadSingle();
            var flags = reader.ReadByte();
            states.Add(new SnapshotPlayerMovementState(
                slot,
                x,
                y,
                horizontalSpeed,
                verticalSpeed,
                IsGroundedFromFlags(flags),
                reader.ReadByte(),
                FacingDirectionXFromFlags(flags),
                ReadQuantizedAngleDegrees(reader),
                reader.ReadByte(),
                reader.ReadInt32(),
                IsMedicHealingFromFlags(flags)));
        }

        return states;
    }

    private static void WriteSnapshotPlayerStatusStates(
        BinaryWriter writer,
        IReadOnlyList<SnapshotPlayerStatusState> states)
    {
        writer.Write((byte)states.Count);
        for (var index = 0; index < states.Count; index += 1)
        {
            var state = states[index];
            writer.Write(state.Slot);
            writer.Write(state.Health);
            writer.Write(state.MaxHealth);
            writer.Write(state.Ammo);
            writer.Write(state.MaxAmmo);
            writer.Write(QuantizeScaledUInt16(state.Metal, QuantizedMetalScale));
            writer.Write(GetStatusFlags(state.IsCarryingIntel));
            writer.Write(QuantizeScaledUInt16(state.IntelRechargeTicks, QuantizedIntelRechargeScale));
        }
    }

    private static List<SnapshotPlayerStatusState> ReadSnapshotPlayerStatusStates(BinaryReader reader)
    {
        var stateCount = reader.ReadByte();
        var states = new List<SnapshotPlayerStatusState>(stateCount);
        for (var index = 0; index < stateCount; index += 1)
        {
            states.Add(new SnapshotPlayerStatusState(
                reader.ReadByte(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                ReadScaledUInt16(reader, QuantizedMetalScale),
                IsCarryingIntelFromFlags(reader.ReadByte()),
                ReadScaledUInt16(reader, QuantizedIntelRechargeScale)));
        }

        return states;
    }

    private static void WriteSnapshotPlayerChatBubbleStates(
        BinaryWriter writer,
        IReadOnlyList<SnapshotPlayerChatBubbleState> states)
    {
        writer.Write((byte)states.Count);
        for (var index = 0; index < states.Count; index += 1)
        {
            var state = states[index];
            writer.Write(state.Slot);
            writer.Write(GetChatBubbleFlags(state.IsChatBubbleVisible));
            writer.Write((ushort)Math.Clamp(state.ChatBubbleFrameIndex, 0, ushort.MaxValue));
            writer.Write((byte)Math.Clamp((int)MathF.Round(Math.Clamp(state.ChatBubbleAlpha, 0f, 1f) * QuantizedChatBubbleAlphaScale), 0, byte.MaxValue));
        }
    }

    private static List<SnapshotPlayerChatBubbleState> ReadSnapshotPlayerChatBubbleStates(BinaryReader reader)
    {
        var stateCount = reader.ReadByte();
        var states = new List<SnapshotPlayerChatBubbleState>(stateCount);
        for (var index = 0; index < stateCount; index += 1)
        {
            states.Add(new SnapshotPlayerChatBubbleState(
                reader.ReadByte(),
                IsChatBubbleVisibleFromFlags(reader.ReadByte()),
                reader.ReadUInt16(),
                reader.ReadByte() / QuantizedChatBubbleAlphaScale));
        }

        return states;
    }

    private static byte GetMovementFlags(bool isGrounded, bool isFacingLeft, bool isMedicHealing)
    {
        byte flags = 0;
        if (isGrounded)
        {
            flags |= 0x01;
        }

        if (isFacingLeft)
        {
            flags |= 0x02;
        }

        if (isMedicHealing)
        {
            flags |= 0x04;
        }

        return flags;
    }

    private static bool IsGroundedFromFlags(byte flags) => (flags & 0x01) != 0;

    private static float FacingDirectionXFromFlags(byte flags) => (flags & 0x02) != 0 ? -1f : 1f;

    private static bool IsMedicHealingFromFlags(byte flags) => (flags & 0x04) != 0;

    private static byte GetStatusFlags(bool isCarryingIntel) => isCarryingIntel ? (byte)0x01 : (byte)0x00;

    private static bool IsCarryingIntelFromFlags(byte flags) => (flags & 0x01) != 0;

    private static byte GetChatBubbleFlags(bool isVisible) => isVisible ? (byte)0x01 : (byte)0x00;

    private static bool IsChatBubbleVisibleFromFlags(byte flags) => (flags & 0x01) != 0;

    private static ushort QuantizeAngleDegrees(float degrees)
    {
        var normalized = degrees % QuantizedAimDegreesMax;
        if (normalized < 0f)
        {
            normalized += QuantizedAimDegreesMax;
        }

        return (ushort)Math.Clamp(
            (int)MathF.Round((normalized / QuantizedAimDegreesMax) * ushort.MaxValue),
            0,
            ushort.MaxValue);
    }

    private static float ReadQuantizedAngleDegrees(BinaryReader reader)
    {
        var quantized = reader.ReadUInt16();
        return (quantized / (float)ushort.MaxValue) * QuantizedAimDegreesMax;
    }

    private static ushort QuantizeScaledUInt16(float value, float scale)
    {
        return (ushort)Math.Clamp(
            (int)MathF.Round(Math.Max(0f, value) * scale),
            0,
            ushort.MaxValue);
    }

    private static float ReadScaledUInt16(BinaryReader reader, float scale)
    {
        return reader.ReadUInt16() / scale;
    }

    private static void WriteReplicatedStateEntries(BinaryWriter writer, IReadOnlyList<SnapshotReplicatedStateEntry>? entries)
    {
        var count = entries?.Count ?? 0;
        writer.Write((byte)count);
        for (var index = 0; index < count; index += 1)
        {
            var entry = entries![index];
            WriteString(writer, entry.OwnerId, MaxPluginIdBytes, nameof(entry.OwnerId));
            WriteString(writer, entry.Key, MaxGameplayIdBytes, nameof(entry.Key));
            writer.Write((byte)entry.Kind);
            writer.Write(entry.IntValue);
            writer.Write(entry.FloatValue);
            writer.Write(entry.BoolValue);
        }
    }

    private static void WriteGameplayIdList(BinaryWriter writer, IReadOnlyList<string>? ids)
    {
        var count = ids?.Count ?? 0;
        writer.Write((byte)count);
        for (var index = 0; index < count; index += 1)
        {
            WriteString(writer, ids![index], MaxGameplayIdBytes, nameof(ids));
        }
    }

    private static List<string> ReadGameplayIdList(BinaryReader reader)
    {
        var count = reader.ReadByte();
        var ids = new List<string>(count);
        for (var index = 0; index < count; index += 1)
        {
            ids.Add(ReadString(reader, MaxGameplayIdBytes));
        }

        return ids;
    }

    private static List<SnapshotReplicatedStateEntry> ReadReplicatedStateEntries(BinaryReader reader)
    {
        var count = reader.ReadByte();
        var entries = new List<SnapshotReplicatedStateEntry>(count);
        for (var index = 0; index < count; index += 1)
        {
            entries.Add(new SnapshotReplicatedStateEntry(
                ReadString(reader, MaxPluginIdBytes),
                ReadString(reader, MaxGameplayIdBytes),
                (SnapshotReplicatedStateValueKind)reader.ReadByte(),
                reader.ReadInt32(),
                reader.ReadSingle(),
                reader.ReadBoolean()));
        }

        return entries;
    }
}
