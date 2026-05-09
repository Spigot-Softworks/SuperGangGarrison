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
        WriteStringCacheUpdates(writer, snapshot.StringCacheUpdates);
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
        WriteGibSpawnEvents(writer, snapshot.GibSpawnEvents);
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
        var stringCacheUpdates = ReadStringCacheUpdates(reader);
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
        var gibSpawnEvents = ReadGibSpawnEvents(reader);
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
            stringCacheUpdates,
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
            GibSpawnEvents = gibSpawnEvents,
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

    private static void WriteStringCacheUpdates(BinaryWriter writer, IReadOnlyDictionary<ushort, string>? updates)
    {
        if (updates == null || updates.Count == 0)
        {
            writer.Write((ushort)0);
            return;
        }

        writer.Write((ushort)updates.Count);
        foreach (var (id, value) in updates)
        {
            writer.Write(id);
            WriteString(writer, value, MaxGameplayIdBytes, nameof(updates));
        }
    }

    private static Dictionary<ushort, string>? ReadStringCacheUpdates(BinaryReader reader)
    {
        var count = reader.ReadUInt16();
        if (count == 0)
        {
            return null;
        }

        var updates = new Dictionary<ushort, string>(count);
        for (var i = 0; i < count; i++)
        {
            var id = reader.ReadUInt16();
            var value = ReadString(reader, MaxGameplayIdBytes);
            updates[id] = value;
        }

        return updates;
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
            writer.Write(QuantizePosition(player.X)); // Quantized to int16
            writer.Write(QuantizePosition(player.Y)); // Quantized to int16
            writer.Write(QuantizePosition(player.HorizontalSpeed)); // Quantized to int16
            writer.Write(QuantizePosition(player.VerticalSpeed)); // Quantized to int16
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
            writer.Write(player.IsSpySuperjumping);
            writer.Write(player.SpySuperjumpHorizontalVelocity);
            writer.Write(player.SpySuperjumpCooldownTicksRemaining);
            writer.Write(player.SpyBackstabVisualTicksRemaining);
            writer.Write(player.IsUbered);
            writer.Write(player.IsKritzCritBoosted);
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
            writer.Write(player.IsMedicHealing);
            writer.Write(player.MedicHealTargetId);
            writer.Write(player.MedicUberCharge);
            writer.Write(player.IsMedicUberReady);
            // String caching: write cache ID (ushort) if available, otherwise write full string
            writer.Write(player.GameplayModPackCacheId);
            if (player.GameplayModPackCacheId == 0)
                WriteString(writer, player.GameplayModPackId, MaxGameplayIdBytes, nameof(player.GameplayModPackId));
            writer.Write(player.GameplayLoadoutCacheId);
            if (player.GameplayLoadoutCacheId == 0)
                WriteString(writer, player.GameplayLoadoutId, MaxGameplayIdBytes, nameof(player.GameplayLoadoutId));
            writer.Write(player.GameplayPrimaryItemCacheId);
            if (player.GameplayPrimaryItemCacheId == 0)
                WriteString(writer, player.GameplayPrimaryItemId, MaxGameplayIdBytes, nameof(player.GameplayPrimaryItemId));
            writer.Write(player.GameplaySecondaryItemCacheId);
            if (player.GameplaySecondaryItemCacheId == 0)
                WriteString(writer, player.GameplaySecondaryItemId, MaxGameplayIdBytes, nameof(player.GameplaySecondaryItemId));
            writer.Write(player.GameplayUtilityItemCacheId);
            if (player.GameplayUtilityItemCacheId == 0)
                WriteString(writer, player.GameplayUtilityItemId, MaxGameplayIdBytes, nameof(player.GameplayUtilityItemId));
            writer.Write(player.GameplayEquippedSlot);
            writer.Write(player.GameplayEquippedItemCacheId);
            if (player.GameplayEquippedItemCacheId == 0)
                WriteString(writer, player.GameplayEquippedItemId, MaxGameplayIdBytes, nameof(player.GameplayEquippedItemId));
            writer.Write(player.GameplayAcquiredItemCacheId);
            if (player.GameplayAcquiredItemCacheId == 0)
                WriteString(writer, player.GameplayAcquiredItemId, MaxGameplayIdBytes, nameof(player.GameplayAcquiredItemId));
            WriteGameplayIdList(writer, player.OwnedGameplayItemIds);
            WriteReplicatedStateEntries(writer, player.ReplicatedStates);
            writer.Write(player.PlayerScale);
            writer.Write(player.AimWorldX);
            writer.Write(player.AimWorldY);
            writer.Write(player.OffhandCooldownTicks);
            writer.Write(player.OffhandReloadTicks);
            writer.Write(player.GibDeaths);
        }
    }

    private static List<SnapshotPlayerState> ReadSnapshotPlayers(BinaryReader reader)
    {
        var playerCount = reader.ReadByte();
        var players = new List<SnapshotPlayerState>(playerCount);
        for (var index = 0; index < playerCount; index += 1)
        {
            var slot = reader.ReadByte();
            var playerId = reader.ReadInt32();
            var name = ReadString(reader, MaxPlayerNameBytes);
            var team = reader.ReadByte();
            var classId = reader.ReadByte();
            var isAlive = reader.ReadBoolean();
            var isAwaitingJoin = reader.ReadBoolean();
            var isSpectator = reader.ReadBoolean();
            var respawnTicks = reader.ReadInt32();
            var x = DequantizePosition(reader.ReadInt16());
            var y = DequantizePosition(reader.ReadInt16());
            var horizontalSpeed = DequantizePosition(reader.ReadInt16());
            var verticalSpeed = DequantizePosition(reader.ReadInt16());
            var health = reader.ReadInt16();
            var maxHealth = reader.ReadInt16();
            var ammo = reader.ReadInt16();
            var maxAmmo = reader.ReadInt16();
            var kills = reader.ReadInt16();
            var deaths = reader.ReadInt16();
            var caps = reader.ReadInt16();
            var points = reader.ReadSingle();
            var healPoints = reader.ReadInt16();
            var activeDominationCount = reader.ReadInt16();
            var isDominatingLocalViewer = reader.ReadBoolean();
            var isDominatedByLocalViewer = reader.ReadBoolean();
            var metal = reader.ReadSingle();
            var isGrounded = reader.ReadBoolean();
            var remainingAirJumps = reader.ReadInt32();
            var isCarryingIntel = reader.ReadBoolean();
            var intelRechargeTicks = reader.ReadSingle();
            var isSpyCloaked = reader.ReadBoolean();
            var spyCloakAlpha = reader.ReadSingle();
            var isSpySuperjumping = reader.ReadBoolean();
            var spySuperjumpHorizontalVelocity = reader.ReadSingle();
            var spySuperjumpCooldownTicksRemaining = reader.ReadInt32();
            var spyBackstabVisualTicksRemaining = reader.ReadInt32();
            var isUbered = reader.ReadBoolean();
            var isKritzCritBoosted = reader.ReadBoolean();
            var isHeavyEating = reader.ReadBoolean();
            var heavyEatTicksRemaining = reader.ReadInt32();
            var isSniperScoped = reader.ReadBoolean();
            var sniperChargeTicks = reader.ReadInt32();
            var facingDirectionX = reader.ReadSingle();
            var aimDirectionDegrees = reader.ReadSingle();
            var isTaunting = reader.ReadBoolean();
            var tauntFrameIndex = reader.ReadSingle();
            var isChatBubbleVisible = reader.ReadBoolean();
            var chatBubbleFrameIndex = reader.ReadInt32();
            var chatBubbleAlpha = reader.ReadSingle();
            var burnIntensity = reader.ReadSingle();
            var burnDurationSourceTicks = reader.ReadSingle();
            var burnDecayDelaySourceTicksRemaining = reader.ReadSingle();
            var burnIntensityDecayPerSourceTick = reader.ReadSingle();
            var burnedByPlayerId = reader.ReadInt32();
            var movementState = reader.ReadByte();
            var primaryCooldownTicks = reader.ReadInt32();
            var reloadTicksUntilNextShell = reader.ReadInt32();
            var medicNeedleCooldownTicks = reader.ReadInt32();
            var medicNeedleRefillTicks = reader.ReadInt32();
            var pyroAirblastCooldownTicks = reader.ReadInt32();
            var pyroFlareCooldownTicks = reader.ReadInt32();
            var pyroPrimaryFuelScaled = reader.ReadInt32();
            var isPyroPrimaryRefilling = reader.ReadBoolean();
            var pyroFlameLoopTicksRemaining = reader.ReadInt32();
            var pyroPrimaryRequiresReleaseAfterEmpty = reader.ReadBoolean();
            var heavyEatCooldownTicksRemaining = reader.ReadInt32();
            var assists = reader.ReadInt16();
            var badgeMask = reader.ReadUInt64();
            var isMedicHealing = reader.ReadBoolean();
            var medicHealTargetId = reader.ReadInt32();
            var medicUberCharge = reader.ReadSingle();
            var isMedicUberReady = reader.ReadBoolean();

            // String caching: read cache ID, then string if cache ID is 0
            var gameplayModPackCacheId = reader.ReadUInt16();
            var gameplayModPackId = gameplayModPackCacheId == 0 ? ReadString(reader, MaxGameplayIdBytes) : string.Empty;
            var gameplayLoadoutCacheId = reader.ReadUInt16();
            var gameplayLoadoutId = gameplayLoadoutCacheId == 0 ? ReadString(reader, MaxGameplayIdBytes) : string.Empty;
            var gameplayPrimaryItemCacheId = reader.ReadUInt16();
            var gameplayPrimaryItemId = gameplayPrimaryItemCacheId == 0 ? ReadString(reader, MaxGameplayIdBytes) : string.Empty;
            var gameplaySecondaryItemCacheId = reader.ReadUInt16();
            var gameplaySecondaryItemId = gameplaySecondaryItemCacheId == 0 ? ReadString(reader, MaxGameplayIdBytes) : string.Empty;
            var gameplayUtilityItemCacheId = reader.ReadUInt16();
            var gameplayUtilityItemId = gameplayUtilityItemCacheId == 0 ? ReadString(reader, MaxGameplayIdBytes) : string.Empty;
            var gameplayEquippedSlot = reader.ReadByte();
            var gameplayEquippedItemCacheId = reader.ReadUInt16();
            var gameplayEquippedItemId = gameplayEquippedItemCacheId == 0 ? ReadString(reader, MaxGameplayIdBytes) : string.Empty;
            var gameplayAcquiredItemCacheId = reader.ReadUInt16();
            var gameplayAcquiredItemId = gameplayAcquiredItemCacheId == 0 ? ReadString(reader, MaxGameplayIdBytes) : string.Empty;
            var ownedGameplayItemIds = ReadGameplayIdList(reader);
            var replicatedStates = ReadReplicatedStateEntries(reader);
            var playerScale = reader.ReadSingle();
            var aimWorldX = reader.ReadSingle();
            var aimWorldY = reader.ReadSingle();
            var offhandCooldownTicks = reader.ReadInt32();
            var offhandReloadTicks = reader.ReadInt32();
            var gibDeaths = reader.ReadInt16();

            players.Add(new SnapshotPlayerState(
                slot, playerId, name, team, classId, isAlive, isAwaitingJoin, isSpectator,
                respawnTicks, x, y, horizontalSpeed, verticalSpeed,
                health, maxHealth, ammo, maxAmmo, kills, deaths, caps, points, healPoints,
                activeDominationCount, isDominatingLocalViewer, isDominatedByLocalViewer,
                metal, isGrounded, remainingAirJumps, isCarryingIntel, intelRechargeTicks,
                isSpyCloaked, spyCloakAlpha, isSpySuperjumping, spySuperjumpHorizontalVelocity,
                spySuperjumpCooldownTicksRemaining, spyBackstabVisualTicksRemaining,
                isUbered, isKritzCritBoosted, isHeavyEating, heavyEatTicksRemaining,
                isSniperScoped, sniperChargeTicks, facingDirectionX, aimDirectionDegrees,
                isTaunting, tauntFrameIndex, isChatBubbleVisible, chatBubbleFrameIndex, chatBubbleAlpha,
                burnIntensity, burnDurationSourceTicks, burnDecayDelaySourceTicksRemaining,
                burnIntensityDecayPerSourceTick, burnedByPlayerId, movementState,
                primaryCooldownTicks, reloadTicksUntilNextShell, medicNeedleCooldownTicks,
                medicNeedleRefillTicks, pyroAirblastCooldownTicks, pyroFlareCooldownTicks,
                pyroPrimaryFuelScaled, isPyroPrimaryRefilling, pyroFlameLoopTicksRemaining,
                pyroPrimaryRequiresReleaseAfterEmpty, heavyEatCooldownTicksRemaining,
                assists, badgeMask, isMedicHealing, medicHealTargetId, medicUberCharge, isMedicUberReady,
                gameplayModPackId, gameplayLoadoutId, gameplayPrimaryItemId,
                gameplaySecondaryItemId, gameplayUtilityItemId, gameplayEquippedSlot,
                gameplayEquippedItemId, gameplayAcquiredItemId,
                gameplayModPackCacheId, gameplayLoadoutCacheId, gameplayPrimaryItemCacheId,
                gameplaySecondaryItemCacheId, gameplayUtilityItemCacheId,
                gameplayEquippedItemCacheId, gameplayAcquiredItemCacheId,
                ownedGameplayItemIds, replicatedStates, playerScale, aimWorldX, aimWorldY,
                offhandCooldownTicks, offhandReloadTicks, gibDeaths));
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
            writer.Write(QuantizePosition(state.X));
            writer.Write(QuantizePosition(state.Y));
            writer.Write(QuantizePosition(state.HorizontalSpeed));
            writer.Write(QuantizePosition(state.VerticalSpeed));
            writer.Write(state.IsGrounded);
            writer.Write(state.RemainingAirJumps);
            writer.Write(state.FacingDirectionX);
            writer.Write(state.AimDirectionDegrees);
            writer.Write(state.MovementState);
            writer.Write(state.IsTaunting);
            writer.Write(state.TauntFrameIndex);
            writer.Write(state.BurnIntensity);
            writer.Write(state.GameplayEquippedSlot);
            writer.Write(state.PrimaryCooldownTicks);
            writer.Write(state.ReloadTicksUntilNextShell);
            writer.Write(state.OffhandCooldownTicks);
            writer.Write(state.OffhandReloadTicks);
            writer.Write(state.MedicHealTargetId);
            writer.Write(state.IsMedicHealing);
        }
    }

    private static List<SnapshotPlayerMovementState> ReadSnapshotPlayerMovementStates(BinaryReader reader)
    {
        var stateCount = reader.ReadByte();
        var states = new List<SnapshotPlayerMovementState>(stateCount);
        for (var index = 0; index < stateCount; index += 1)
        {
            states.Add(new SnapshotPlayerMovementState(
                reader.ReadByte(),
                DequantizePosition(reader.ReadInt16()),
                DequantizePosition(reader.ReadInt16()),
                DequantizePosition(reader.ReadInt16()),
                DequantizePosition(reader.ReadInt16()),
                reader.ReadBoolean(),
                reader.ReadInt32(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadByte(),
                reader.ReadBoolean(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadByte(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadBoolean()));
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
