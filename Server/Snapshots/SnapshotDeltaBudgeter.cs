using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenGarrison.Protocol;

namespace OpenGarrison.Server;

internal static class SnapshotDeltaBudgeter
{
    private readonly record struct SerializedSnapshot(SnapshotMessage Message, byte[] Payload, int UncompressedBytes);

    // Stay below a typical 1500-byte Ethernet MTU after IPv4/UDP headers while
    // giving internet UDP clients more room than the prior ultra-conservative cap.
    public const int TargetSnapshotPayloadBytes = 1400;
    public const int LoopbackTargetSnapshotPayloadBytes = 4 * 1024;
    public const int ReliableStreamTargetSnapshotPayloadBytes = 12 * 1024;

    internal enum ContributionKind
    {
        Optional,
        LocalPlayerUpdate,
        LocalPlayerStatusUpdate,
        PlayerMovementUpdate,
        PlayerExtendedStatusUpdate,
        PlayerChatBubbleUpdate,
        PlayerFirstAppearance,
        PlayerRosterUpdate,
        PlayerRemoval,
        EntityRemoval,
        ProjectileSpawn,
    }

    internal sealed record Contribution(
        int Priority,
        float DistanceSquared,
        int EstimatedBytes,
        Action<Builder> Apply,
        ContributionKind Kind = ContributionKind.Optional);

    public static (SnapshotMessage Message, byte[] Payload) BuildBudgetedSnapshot(
        SnapshotMessage fullSnapshot,
        ISnapshotBaselineState? baseline,
        IReadOnlyList<Contribution> contributions,
        int targetPayloadBytes = TargetSnapshotPayloadBytes)
    {
        var result = BuildBudgetedSnapshotWithMetrics(fullSnapshot, baseline, contributions, targetPayloadBytes);
        return (result.Message, result.Payload);
    }

    internal static SnapshotBudgetBuildResult BuildBudgetedSnapshotWithMetrics(
        SnapshotMessage fullSnapshot,
        ISnapshotBaselineState? baseline,
        IReadOnlyList<Contribution> contributions,
        int targetPayloadBytes = TargetSnapshotPayloadBytes)
    {
        var builder = new Builder(fullSnapshot, baseline?.Frame ?? 0, seedFromTemplateCollections: false);
        var snapshot = builder.Build();
        var serializePassCount = 0;
        byte[]? payload = null;
        var payloadSize = Measure(snapshot);

        if (payloadSize > targetPayloadBytes && TrimAuxiliaryCollections(builder))
        {
            snapshot = builder.Build();
            payloadSize = Measure(snapshot);
        }

        var orderedContributions = contributions
            .OrderByDescending(static entry => entry.Priority)
            .ThenBy(static entry => entry.DistanceSquared)
            .ToArray();
        var appliedContributions = new bool[orderedContributions.Length];
        var remainingBudget = targetPayloadBytes - payloadSize;

        ApplyRequiredRosterContribution(
            builder,
            orderedContributions,
            appliedContributions,
            SnapshotDeltaBudgeter.ContributionKind.PlayerFirstAppearance,
            ref remainingBudget);
        ApplyRequiredRosterContribution(
            builder,
            orderedContributions,
            appliedContributions,
            SnapshotDeltaBudgeter.ContributionKind.PlayerRosterUpdate,
            ref remainingBudget);
        ApplyRequiredRosterContribution(
            builder,
            orderedContributions,
            appliedContributions,
            SnapshotDeltaBudgeter.ContributionKind.PlayerRemoval,
            ref remainingBudget);
        ApplyRequiredContributions(
            builder,
            orderedContributions,
            appliedContributions,
            SnapshotDeltaBudgeter.ContributionKind.EntityRemoval,
            ref remainingBudget);
        ApplyRequiredContributions(
            builder,
            orderedContributions,
            appliedContributions,
            SnapshotDeltaBudgeter.ContributionKind.LocalPlayerUpdate,
            ref remainingBudget);
        ApplyRequiredRosterContribution(
            builder,
            orderedContributions,
            appliedContributions,
            SnapshotDeltaBudgeter.ContributionKind.LocalPlayerStatusUpdate,
            ref remainingBudget);

        for (var index = 0; index < orderedContributions.Length; index += 1)
        {
            if (appliedContributions[index])
            {
                continue;
            }

            var contribution = orderedContributions[index];
            if (contribution.EstimatedBytes > remainingBudget)
            {
                continue;
            }

            contribution.Apply(builder);
            appliedContributions[index] = true;
            remainingBudget -= contribution.EstimatedBytes;
        }

        snapshot = builder.Build();
        payloadSize = Measure(snapshot);
        payload = Serialize(snapshot, payloadSize, ref serializePassCount);

        if (payload.Length > targetPayloadBytes)
        {
            if (TryReduceToBudget(builder, targetPayloadBytes, ref serializePassCount) is { } reducedBuilderSnapshot)
            {
                snapshot = reducedBuilderSnapshot.Message;
                payload = reducedBuilderSnapshot.Payload;
                payloadSize = reducedBuilderSnapshot.UncompressedBytes;
            }
            else if (TryReduceSnapshotForBudget(snapshot, targetPayloadBytes, ref serializePassCount) is { } reducedSnapshot)
            {
                snapshot = reducedSnapshot.Message;
                payload = reducedSnapshot.Payload;
                payloadSize = reducedSnapshot.UncompressedBytes;
            }
            else
            {
                snapshot = ReduceSnapshotToAbsoluteMinimum(snapshot);
                payloadSize = Measure(snapshot);
                payload = Serialize(snapshot, payloadSize, ref serializePassCount);
            }
        }

        var composition = BuildCompositionMetrics(snapshot, targetPayloadBytes, payloadSize, payload!.Length);
        return new SnapshotBudgetBuildResult(snapshot, payload, serializePassCount, composition);
    }

    private static void ApplyRequiredRosterContribution(
        Builder builder,
        Contribution[] orderedContributions,
        bool[] appliedContributions,
        ContributionKind kind,
        ref int remainingBudget)
    {
        for (var index = 0; index < orderedContributions.Length; index += 1)
        {
            if (appliedContributions[index] || orderedContributions[index].Kind != kind)
            {
                continue;
            }

            var contribution = orderedContributions[index];
            contribution.Apply(builder);
            appliedContributions[index] = true;
            remainingBudget -= contribution.EstimatedBytes;
            return;
        }
    }

    private static void ApplyRequiredContributions(
        Builder builder,
        Contribution[] orderedContributions,
        bool[] appliedContributions,
        ContributionKind kind,
        ref int remainingBudget)
    {
        for (var index = 0; index < orderedContributions.Length; index += 1)
        {
            if (appliedContributions[index] || orderedContributions[index].Kind != kind)
            {
                continue;
            }

            var contribution = orderedContributions[index];
            contribution.Apply(builder);
            appliedContributions[index] = true;
            remainingBudget -= contribution.EstimatedBytes;
        }
    }

    private static bool TrimAuxiliaryCollections(Builder builder)
    {
        var changed = false;
        changed |= ClearIfAny(builder.KillFeed);
        changed |= ClearIfAny(builder.CombatTraces);
        // Keep transient events here; let the contribution and reduction passes decide.
        // Keep DamageEvents - needed for client-side blood and hit feedback
        return changed;
    }

    private static SerializedSnapshot? TryReduceToBudget(Builder builder, int targetPayloadBytes, ref int serializePassCount)
    {
        var madeProgress = true;
        while (madeProgress)
        {
            madeProgress = false;
            foreach (var dropStep in BudgetDropSteps)
            {
                if (!dropStep(builder))
                {
                    continue;
                }

                madeProgress = true;
                var snapshot = builder.Build();
                var payloadSize = Measure(snapshot);
                var payload = Serialize(snapshot, payloadSize, ref serializePassCount);
                if (payload.Length <= targetPayloadBytes)
                {
                    return new SerializedSnapshot(snapshot, payload, payloadSize);
                }
            }
        }

        return null;
    }

    private static SerializedSnapshot? TryReduceSnapshotForBudget(SnapshotMessage snapshot, int targetPayloadBytes, ref int serializePassCount)
    {
        snapshot = snapshot with
        {
            Players = snapshot.Players
                .Select(ReducePlayerStateForBudget)
                .ToArray(),
            LocalDeathCam = null,
        };

        var payloadSize = Measure(snapshot);
        var payload = Serialize(snapshot, payloadSize, ref serializePassCount);
        if (payload.Length <= targetPayloadBytes)
        {
            return new SerializedSnapshot(snapshot, payload, payloadSize);
        }

        snapshot = snapshot with
        {
            Players = snapshot.Players
                .Select(ReducePlayerStateAggressivelyForBudget)
                .ToArray(),
        };

        payloadSize = Measure(snapshot);
        payload = Serialize(snapshot, payloadSize, ref serializePassCount);
        if (payload.Length <= targetPayloadBytes)
        {
            return new SerializedSnapshot(snapshot, payload, payloadSize);
        }

        snapshot = snapshot with
        {
            Players = Array.Empty<SnapshotPlayerState>(),
            PlayerMovementStates = Array.Empty<SnapshotPlayerMovementState>(),
            PlayerExtendedStatusStates = Array.Empty<SnapshotPlayerExtendedStatusState>(),
        };

        payloadSize = Measure(snapshot);
        payload = Serialize(snapshot, payloadSize, ref serializePassCount);
        return payload.Length <= targetPayloadBytes
            ? new SerializedSnapshot(snapshot, payload, payloadSize)
            : null;
    }

    private static int Measure(SnapshotMessage snapshot)
    {
        return ProtocolCodec.MeasureSerializedSize(snapshot);
    }

    private static byte[] Serialize(SnapshotMessage snapshot, int measuredSize, ref int serializePassCount)
    {
        serializePassCount += 1;
        return ProtocolCodec.Serialize(snapshot, measuredSize, ServerProtocolCompression.Settings);
    }

    internal static SnapshotCompositionMetrics BuildCompositionMetrics(
        SnapshotMessage snapshot,
        int targetPayloadBytes,
        int finalUncompressedBytes,
        int finalPayloadBytes)
    {
        var fullPlayerBytes = EstimateByteCountCollection(snapshot.Players, EstimatePlayerBytes);
        var movementBytes = EstimateByteCountCollection(snapshot.PlayerMovementStates, EstimatePlayerMovementBytes);
        var statusBytes = EstimateByteCountCollection(snapshot.PlayerStatusStates, static _ => 14);
        var extendedStatusBytes = EstimateByteCountCollection(snapshot.PlayerExtendedStatusStates, static _ => 30);
        var chatBubbleBytes = EstimateByteCountCollection(snapshot.PlayerChatBubbleStates, static _ => 6);
        var projectileBytes =
            EstimateShotCollectionBytes(snapshot.Shots)
            + EstimateShotCollectionBytes(snapshot.Bubbles)
            + EstimateShotCollectionBytes(snapshot.Blades)
            + EstimateShotCollectionBytes(snapshot.Needles)
            + EstimateShotCollectionBytes(snapshot.RevolverShots)
            + EstimateShotCollectionBytes(snapshot.Flares)
            + EstimateUShortCountCollection(snapshot.Rockets, EstimateRocketBytes)
            + EstimateUShortCountCollection(snapshot.RocketSpawnEvents, static _ => 76)
            + EstimateUShortCountCollection(snapshot.Flames, static _ => 50)
            + EstimateUShortCountCollection(snapshot.Mines, static _ => 32);
        var sentryBytes =
            EstimateUShortCountCollection(snapshot.Sentries, static _ => 44)
            + EstimateUShortCountCollection(snapshot.SentryUpdateStates, static _ => 37)
            + EstimateUShortCountCollection(snapshot.JumpPads, static _ => 22);
        var eventBytes =
            EstimateUShortCountCollection(snapshot.CombatTraces, static _ => 24)
            + EstimateByteCountCollection(snapshot.KillFeed, EstimateKillFeedBytes)
            + EstimateUShortCountCollection(snapshot.VisualEvents, EstimateVisualEventBytes)
            + EstimateUShortCountCollection(snapshot.DamageEvents, static _ => 42)
            + EstimateUShortCountCollection(snapshot.SoundEvents, EstimateSoundEventBytes)
            + EstimateUShortCountCollection(snapshot.GibSpawnEvents, EstimateGibSpawnEventBytes)
            + EstimateUShortCountCollection(snapshot.DeadBodies, static _ => 40)
            + EstimateUShortCountCollection(snapshot.SentryGibs, static _ => 17);
        var removalBytes =
            EstimateEntityIdListBytes(snapshot.RemovedPlayerIds)
            + EstimateEntityIdListBytes(snapshot.RemovedSentryIds)
            + EstimateEntityIdListBytes(snapshot.RemovedShotIds)
            + EstimateEntityIdListBytes(snapshot.RemovedBubbleIds)
            + EstimateEntityIdListBytes(snapshot.RemovedBladeIds)
            + EstimateEntityIdListBytes(snapshot.RemovedNeedleIds)
            + EstimateEntityIdListBytes(snapshot.RemovedRevolverShotIds)
            + EstimateEntityIdListBytes(snapshot.RemovedRocketIds)
            + EstimateEntityIdListBytes(snapshot.RemovedFlameIds)
            + EstimateEntityIdListBytes(snapshot.RemovedFlareIds)
            + EstimateEntityIdListBytes(snapshot.RemovedMineIds)
            + EstimateEntityIdListBytes(snapshot.RemovedDeadBodyIds)
            + EstimateEntityIdListBytes(snapshot.RemovedSentryGibIds)
            + EstimateEntityIdListBytes(snapshot.RemovedJumpPadIds);
        var worldBytes =
            EstimateByteCountCollection(snapshot.ControlPoints, static _ => 8)
            + EstimateByteCountCollection(snapshot.Generators, static _ => 5)
            + EstimateDeathCamBytes(snapshot.LocalDeathCam);
        var knownBytes =
            fullPlayerBytes
            + movementBytes
            + statusBytes
            + extendedStatusBytes
            + chatBubbleBytes
            + projectileBytes
            + sentryBytes
            + eventBytes
            + removalBytes
            + worldBytes;
        var envelopeBytes = Math.Max(0, finalUncompressedBytes - knownBytes);

        return new SnapshotCompositionMetrics(
            targetPayloadBytes,
            finalUncompressedBytes,
            finalPayloadBytes,
            finalPayloadBytes > targetPayloadBytes,
            fullPlayerBytes,
            movementBytes,
            statusBytes,
            extendedStatusBytes,
            chatBubbleBytes,
            projectileBytes,
            sentryBytes,
            eventBytes,
            removalBytes,
            worldBytes,
            envelopeBytes);
    }

    private static int EstimateByteCountCollection<T>(IReadOnlyList<T> entries, Func<T, int> estimateEntryBytes)
    {
        var bytes = 1;
        for (var index = 0; index < entries.Count; index += 1)
        {
            bytes += estimateEntryBytes(entries[index]);
        }

        return bytes;
    }

    private static int EstimateUShortCountCollection<T>(IReadOnlyList<T> entries, Func<T, int> estimateEntryBytes)
    {
        var bytes = 2;
        for (var index = 0; index < entries.Count; index += 1)
        {
            bytes += estimateEntryBytes(entries[index]);
        }

        return bytes;
    }

    private static int EstimateEntityIdListBytes(IReadOnlyList<int> ids) => 1 + (ids.Count * 4);

    private static int EstimateShotCollectionBytes(IReadOnlyList<SnapshotShotState> shots) => 2 + (shots.Count * 30);

    private static int EstimateRocketBytes(SnapshotRocketState rocket)
    {
        return 69 + ((rocket.PassedFriendlyPlayerIds?.Count ?? 0) * 4);
    }

    private static int EstimatePlayerMovementBytes(SnapshotPlayerMovementState state)
    {
        return 1 // Slot
            + 8 // Quantized position and velocity shorts
            + 1 // Flags
            + EstimateVariableInt32Bytes(state.RemainingAirJumps)
            + 2 // Quantized aim angle
            + 1 // MovementState
            + 2 // Quantized taunt frame
            + 2 // Quantized burn intensity
            + 1 // GameplayEquippedSlot
            + EstimateVariableInt32Bytes(state.PrimaryCooldownTicks)
            + EstimateVariableInt32Bytes(state.ReloadTicksUntilNextShell)
            + EstimateVariableInt32Bytes(state.OffhandCooldownTicks)
            + EstimateVariableInt32Bytes(state.OffhandReloadTicks)
            + EstimateVariableInt32Bytes(state.MedicHealTargetId);
    }

    private static int EstimatePlayerBytes(SnapshotPlayerState player)
    {
        var bytes = 206
            + EstimateStringBytes(player.Name)
            + EstimateCachedStringBytes(player.GameplayModPackCacheId, player.GameplayModPackId)
            + EstimateCachedStringBytes(player.GameplayLoadoutCacheId, player.GameplayLoadoutId)
            + EstimateCachedStringBytes(player.GameplayPrimaryItemCacheId, player.GameplayPrimaryItemId)
            + EstimateCachedStringBytes(player.GameplaySecondaryItemCacheId, player.GameplaySecondaryItemId)
            + EstimateCachedStringBytes(player.GameplayUtilityItemCacheId, player.GameplayUtilityItemId)
            + EstimateCachedStringBytes(player.GameplayEquippedItemCacheId, player.GameplayEquippedItemId)
            + EstimateCachedStringBytes(player.GameplayAcquiredItemCacheId, player.GameplayAcquiredItemId)
            + EstimateGameplayIdListBytes(player.OwnedGameplayItemIds)
            + EstimateReplicatedStateEntriesBytes(player.ReplicatedStates);
        return bytes;
    }

    private static int EstimateCachedStringBytes(ushort cacheId, string value)
    {
        return 2 + (cacheId == 0 ? EstimateStringBytes(value) : 0);
    }

    private static int EstimateGameplayIdListBytes(IReadOnlyList<string>? ids)
    {
        var count = ids?.Count ?? 0;
        var bytes = 1;
        for (var index = 0; index < count; index += 1)
        {
            bytes += EstimateStringBytes(ids![index]);
        }

        return bytes;
    }

    private static int EstimateReplicatedStateEntriesBytes(IReadOnlyList<SnapshotReplicatedStateEntry>? entries)
    {
        var count = entries?.Count ?? 0;
        var bytes = 1;
        for (var index = 0; index < count; index += 1)
        {
            var entry = entries![index];
            bytes += EstimateStringBytes(entry.OwnerId)
                + EstimateStringBytes(entry.Key)
                + 10;
        }

        return bytes;
    }

    private static int EstimateKillFeedBytes(SnapshotKillFeedEntry entry)
    {
        return EstimateStringBytes(entry.KillerName)
            + EstimateStringBytes(entry.WeaponSpriteName)
            + EstimateStringBytes(entry.VictimName)
            + EstimateStringBytes(entry.MessageText)
            + 27;
    }

    private static int EstimateSoundEventBytes(SnapshotSoundEvent soundEvent)
    {
        return EstimateStringBytes(soundEvent.SoundName) + 24;
    }

    private static int EstimateVisualEventBytes(SnapshotVisualEvent visualEvent)
    {
        return EstimateStringBytes(visualEvent.EffectName) + 24;
    }

    private static int EstimateGibSpawnEventBytes(SnapshotGibSpawnEvent gibEvent)
    {
        return EstimateStringBytes(gibEvent.SpriteName) + 48;
    }

    private static int EstimateDeathCamBytes(SnapshotDeathCamState? deathCam)
    {
        if (deathCam is null)
        {
            return 1;
        }

        return 1
            + 8
            + EstimateStringBytes(deathCam.KillMessage)
            + EstimateStringBytes(deathCam.KillerName)
            + 17;
    }

    private static int EstimateStringBytes(string value) => 2 + Encoding.UTF8.GetByteCount(value);

    private static int EstimateVariableInt32Bytes(int value) => EstimateVariableUInt32Bytes(EncodeZigZagInt32(value));

    private static int EstimateVariableUInt32Bytes(uint value)
    {
        var bytes = 1;
        while (value >= 0x80)
        {
            value >>= 7;
            bytes += 1;
        }

        return bytes;
    }

    private static uint EncodeZigZagInt32(int value)
    {
        return unchecked((uint)((value << 1) ^ (value >> 31)));
    }

    private static SnapshotPlayerState ReducePlayerStateForBudget(SnapshotPlayerState player)
    {
        // Keep Toggle (bool) ReplicatedStates entries — these carry low-frequency state like
        // soldier_shotgun_equipped that clients need even under budget pressure. Drop Whole/Scalar
        // int entries since the animation-critical cooldown/reload values now arrive via the movement
        // delta (OffhandCooldownTicks / OffhandReloadTicks fields).
        var reducedReplicatedStates = player.ReplicatedStates is { Count: > 0 }
            ? player.ReplicatedStates
                .Where(static e => e.Kind == SnapshotReplicatedStateValueKind.Toggle)
                .ToArray()
            : Array.Empty<SnapshotReplicatedStateEntry>();

        return player with
        {
            BadgeMask = 0,
            OwnedGameplayItemIds = Array.Empty<string>(),
            ReplicatedStates = reducedReplicatedStates,
            IsChatBubbleVisible = false,
            ChatBubbleFrameIndex = 0,
            ChatBubbleAlpha = 0f,
            // Weapon state trimming - clear if at default values
            MedicNeedleCooldownTicks = 0,
            MedicNeedleRefillTicks = 0,
            PyroAirblastCooldownTicks = 0,
            PyroFlareCooldownTicks = 0,
            PyroFlameLoopTicksRemaining = 0,
            HeavyEatCooldownTicksRemaining = 0,
            // Status effect trimming - clear burn if not burning
            BurnIntensity = 0f,
            BurnDurationSourceTicks = 0f,
            BurnDecayDelaySourceTicksRemaining = 0f,
            BurnIntensityDecayPerSourceTick = 0f,
            BurnedByPlayerId = -1,
            // Medic beam trimming - clear if not healing
            IsMedicHealing = player.IsMedicHealing && player.MedicHealTargetId >= 0 ? player.IsMedicHealing : false,
            MedicHealTargetId = player.IsMedicHealing && player.MedicHealTargetId >= 0 ? player.MedicHealTargetId : -1,
        };
    }

    private static SnapshotPlayerState ReducePlayerStateAggressivelyForBudget(SnapshotPlayerState player)
    {
        return player with
        {
            Name = player.Name.Length > 12 ? player.Name[..12] : player.Name,
            OwnedGameplayItemIds = Array.Empty<string>(),
            ReplicatedStates = Array.Empty<SnapshotReplicatedStateEntry>(),
            IsChatBubbleVisible = false,
            ChatBubbleFrameIndex = 0,
            ChatBubbleAlpha = 0f,
            Points = 0f,
            HealPoints = 0,
            ActiveDominationCount = 0,
            IsDominatingLocalViewer = false,
            IsDominatedByLocalViewer = false,
            Metal = 0f,
            IsGrounded = false,
            RemainingAirJumps = 0,
            IsCarryingIntel = false,
            IntelRechargeTicks = 0f,
            IsUbered = false,
            IsHeavyEating = false,
            HeavyEatTicksRemaining = 0,
            IsSniperScoped = false,
            SniperChargeTicks = 0,
            BurnIntensity = 0f,
            BurnDurationSourceTicks = 0f,
            BurnDecayDelaySourceTicksRemaining = 0f,
            BurnIntensityDecayPerSourceTick = 0f,
            BurnedByPlayerId = -1,
            MovementState = 0,
            PrimaryCooldownTicks = 0,
            ReloadTicksUntilNextShell = 0,
            MedicNeedleCooldownTicks = 0,
            MedicNeedleRefillTicks = 0,
            PyroAirblastCooldownTicks = 0,
            PyroFlareCooldownTicks = 0,
            PyroPrimaryFuelScaled = 0,
            IsPyroPrimaryRefilling = false,
            PyroFlameLoopTicksRemaining = 0,
            PyroPrimaryRequiresReleaseAfterEmpty = false,
            HeavyEatCooldownTicksRemaining = 0,
            // Medic beam trimming
            IsMedicHealing = false,
            MedicHealTargetId = -1,
        };
    }

    private static SnapshotMessage ReduceSnapshotToAbsoluteMinimum(SnapshotMessage snapshot)
    {
        return snapshot with
        {
            LevelName = string.Empty,
            MapDownloadUrl = string.Empty,
            MapContentHash = string.Empty,
            Players = Array.Empty<SnapshotPlayerState>(),
            PlayerMovementStates = Array.Empty<SnapshotPlayerMovementState>(),
            PlayerExtendedStatusStates = Array.Empty<SnapshotPlayerExtendedStatusState>(),
            CombatTraces = Array.Empty<SnapshotCombatTraceState>(),
            Sentries = Array.Empty<SnapshotSentryState>(),
            Shots = Array.Empty<SnapshotShotState>(),
            Bubbles = Array.Empty<SnapshotShotState>(),
            Blades = Array.Empty<SnapshotShotState>(),
            Needles = Array.Empty<SnapshotShotState>(),
            RevolverShots = Array.Empty<SnapshotShotState>(),
            Rockets = Array.Empty<SnapshotRocketState>(),
            Flames = Array.Empty<SnapshotFlameState>(),
            Flares = Array.Empty<SnapshotShotState>(),
            Mines = Array.Empty<SnapshotMineState>(),
            PlayerGibs = Array.Empty<SnapshotPlayerGibState>(),
            DeadBodies = Array.Empty<SnapshotDeadBodyState>(),
            ControlPoints = Array.Empty<SnapshotControlPointState>(),
            Generators = Array.Empty<SnapshotGeneratorState>(),
            LocalDeathCam = null,
            KillFeed = Array.Empty<SnapshotKillFeedEntry>(),
            VisualEvents = Array.Empty<SnapshotVisualEvent>(),
            DamageEvents = Array.Empty<SnapshotDamageEvent>(),
            SoundEvents = Array.Empty<SnapshotSoundEvent>(),
            RocketSpawnEvents = Array.Empty<SnapshotRocketSpawnEvent>(),
            SentryGibs = Array.Empty<SnapshotSentryGibState>(),
            JumpPads = Array.Empty<SnapshotJumpPadState>(),
            RemovedPlayerIds = Array.Empty<int>(),
            RemovedSentryIds = Array.Empty<int>(),
            RemovedShotIds = Array.Empty<int>(),
            RemovedBubbleIds = Array.Empty<int>(),
            RemovedBladeIds = Array.Empty<int>(),
            RemovedNeedleIds = Array.Empty<int>(),
            RemovedRevolverShotIds = Array.Empty<int>(),
            RemovedRocketIds = Array.Empty<int>(),
            RemovedFlameIds = Array.Empty<int>(),
            RemovedFlareIds = Array.Empty<int>(),
            RemovedMineIds = Array.Empty<int>(),
            RemovedPlayerGibIds = Array.Empty<int>(),
            RemovedDeadBodyIds = Array.Empty<int>(),
            RemovedSentryGibIds = Array.Empty<int>(),
            RemovedJumpPadIds = Array.Empty<int>(),
        };
    }

    private static readonly Func<Builder, bool>[] BudgetDropSteps =
    [
        static builder =>
        {
            var changed = false;
            changed |= ClearIfAny(builder.VisualEvents);
            changed |= ClearIfAny(builder.KillFeed);
            return changed;
        },
        static builder => ClearIfAny(builder.CombatTraces),
        static builder =>
        {
            var changed = false;
            changed |= ClearIfAny(builder.GibSpawnEvents);
            changed |= ClearIfAny(builder.SentryGibs);
            changed |= ClearIfAny(builder.DeadBodies);
            return changed;
        },
        static builder =>
        {
            var changed = false;
            changed |= ClearIfAny(builder.Flares);
            changed |= ClearIfAny(builder.Blades);
            changed |= ClearIfAny(builder.Bubbles);
            changed |= ClearIfAny(builder.Needles);
            changed |= ClearIfAny(builder.RevolverShots);
            changed |= ClearIfAny(builder.Shots);
            return changed;
        },
        static builder =>
        {
            var changed = false;
            changed |= ClearIfAny(builder.Mines);
            changed |= ClearIfAny(builder.Flames);
            changed |= ClearIfAny(builder.Rockets);
            changed |= ClearIfAny(builder.Sentries);
            changed |= ClearIfAny(builder.JumpPads);
            return changed;
        },
        static builder => RemoveLastIfAboveMinimum(builder.RocketSpawnEvents, minimumCount: 0),
        static builder => ClearIfAny(builder.SoundEvents),
        static builder =>
        {
            var changed = false;
            changed |= ClearIfAny(builder.RemovedPlayerIds);
            changed |= ClearIfAny(builder.RemovedSentryGibIds);
            changed |= ClearIfAny(builder.RemovedJumpPadIds);
            changed |= ClearIfAny(builder.RemovedDeadBodyIds);
            return changed;
        },
        static builder =>
        {
            var changed = false;
            changed |= ClearIfAny(builder.RemovedFlareIds);
            changed |= ClearIfAny(builder.RemovedBladeIds);
            changed |= ClearIfAny(builder.RemovedBubbleIds);
            changed |= ClearIfAny(builder.RemovedNeedleIds);
            changed |= ClearIfAny(builder.RemovedRevolverShotIds);
            changed |= ClearIfAny(builder.RemovedShotIds);
            return changed;
        },
        static builder =>
        {
            var changed = false;
            changed |= ClearIfAny(builder.RemovedMineIds);
            changed |= ClearIfAny(builder.RemovedFlameIds);
            changed |= ClearIfAny(builder.RemovedRocketIds);
            changed |= ClearIfAny(builder.RemovedSentryIds);
            return changed;
        },
        static builder => RemoveLastIfAboveMinimum(builder.PlayerChatBubbleStates, minimumCount: 0),
        static builder => RemoveLastIfAboveMinimum(builder.PlayerExtendedStatusStates, minimumCount: 0),
        static builder => RemoveLastIfAboveMinimum(builder.PlayerMovementStates, minimumCount: 1),
    ];

    private static bool ClearIfAny<T>(TrackingList<T> list)
    {
        if (list.Count == 0)
        {
            return false;
        }

        list.Clear();
        return true;
    }

    private static bool RemoveLastIfAboveMinimum<T>(TrackingList<T> list, int minimumCount)
    {
        if (list.Count <= minimumCount)
        {
            return false;
        }

        list.RemoveLast();
        return true;
    }

    internal sealed class Builder
    {
        private readonly SnapshotMessage _template;

        public Builder(SnapshotMessage template, ulong baselineFrame, bool seedFromTemplateCollections)
        {
            _template = template;
            BaselineFrame = baselineFrame;
            CombatTraces = seedFromTemplateCollections ? new TrackingList<SnapshotCombatTraceState>(template.CombatTraces) : [];
            KillFeed = seedFromTemplateCollections ? new TrackingList<SnapshotKillFeedEntry>(template.KillFeed) : [];
            VisualEvents = seedFromTemplateCollections ? new TrackingList<SnapshotVisualEvent>(template.VisualEvents) : [];
            DamageEvents = seedFromTemplateCollections ? new TrackingList<SnapshotDamageEvent>(template.DamageEvents) : [];
            SoundEvents = seedFromTemplateCollections ? new TrackingList<SnapshotSoundEvent>(template.SoundEvents) : [];
            GibSpawnEvents = seedFromTemplateCollections ? new TrackingList<SnapshotGibSpawnEvent>(template.GibSpawnEvents) : [];
            RocketSpawnEvents = seedFromTemplateCollections ? new TrackingList<SnapshotRocketSpawnEvent>(template.RocketSpawnEvents) : [];
            Players = seedFromTemplateCollections ? new TrackingList<SnapshotPlayerState>(template.Players) : [];
            PlayerMovementStates = seedFromTemplateCollections ? new TrackingList<SnapshotPlayerMovementState>(template.PlayerMovementStates) : [];
            PlayerStatusStates = seedFromTemplateCollections ? new TrackingList<SnapshotPlayerStatusState>(template.PlayerStatusStates) : [];
            PlayerExtendedStatusStates = seedFromTemplateCollections ? new TrackingList<SnapshotPlayerExtendedStatusState>(template.PlayerExtendedStatusStates) : [];
            PlayerChatBubbleStates = seedFromTemplateCollections ? new TrackingList<SnapshotPlayerChatBubbleState>(template.PlayerChatBubbleStates) : [];
            Sentries = seedFromTemplateCollections ? new TrackingList<SnapshotSentryState>(template.Sentries) : [];
            SentryUpdateStates = seedFromTemplateCollections ? new TrackingList<SnapshotSentryUpdateState>(template.SentryUpdateStates) : [];
            Shots = seedFromTemplateCollections ? new TrackingList<SnapshotShotState>(template.Shots) : [];
            Bubbles = seedFromTemplateCollections ? new TrackingList<SnapshotShotState>(template.Bubbles) : [];
            Blades = seedFromTemplateCollections ? new TrackingList<SnapshotShotState>(template.Blades) : [];
            Needles = seedFromTemplateCollections ? new TrackingList<SnapshotShotState>(template.Needles) : [];
            RevolverShots = seedFromTemplateCollections ? new TrackingList<SnapshotShotState>(template.RevolverShots) : [];
            Rockets = seedFromTemplateCollections ? new TrackingList<SnapshotRocketState>(template.Rockets) : [];
            Flames = seedFromTemplateCollections ? new TrackingList<SnapshotFlameState>(template.Flames) : [];
            Flares = seedFromTemplateCollections ? new TrackingList<SnapshotShotState>(template.Flares) : [];
            Mines = seedFromTemplateCollections ? new TrackingList<SnapshotMineState>(template.Mines) : [];
            SentryGibs = seedFromTemplateCollections ? new TrackingList<SnapshotSentryGibState>(template.SentryGibs) : [];
            JumpPads = seedFromTemplateCollections ? new TrackingList<SnapshotJumpPadState>(template.JumpPads) : [];
            PlayerGibs = seedFromTemplateCollections ? new TrackingList<SnapshotPlayerGibState>(template.PlayerGibs) : [];
            DeadBodies = seedFromTemplateCollections ? new TrackingList<SnapshotDeadBodyState>(template.DeadBodies) : [];
            RemovedPlayerIds = new TrackingList<int>(template.RemovedPlayerIds);
            RemovedSentryIds = new TrackingList<int>(template.RemovedSentryIds);
            RemovedShotIds = new TrackingList<int>(template.RemovedShotIds);
            RemovedBubbleIds = new TrackingList<int>(template.RemovedBubbleIds);
            RemovedBladeIds = new TrackingList<int>(template.RemovedBladeIds);
            RemovedNeedleIds = new TrackingList<int>(template.RemovedNeedleIds);
            RemovedRevolverShotIds = new TrackingList<int>(template.RemovedRevolverShotIds);
            RemovedRocketIds = new TrackingList<int>(template.RemovedRocketIds);
            RemovedFlameIds = new TrackingList<int>(template.RemovedFlameIds);
            RemovedFlareIds = new TrackingList<int>(template.RemovedFlareIds);
            RemovedMineIds = new TrackingList<int>(template.RemovedMineIds);
            RemovedSentryGibIds = new TrackingList<int>(template.RemovedSentryGibIds);
            RemovedJumpPadIds = new TrackingList<int>(template.RemovedJumpPadIds);
            RemovedPlayerGibIds = new TrackingList<int>(template.RemovedPlayerGibIds);
            RemovedDeadBodyIds = new TrackingList<int>(template.RemovedDeadBodyIds);
        }

        private Builder(Builder other)
        {
            _template = other._template;
            BaselineFrame = other.BaselineFrame;
            CombatTraces = new TrackingList<SnapshotCombatTraceState>(other.CombatTraces);
            KillFeed = new TrackingList<SnapshotKillFeedEntry>(other.KillFeed);
            VisualEvents = new TrackingList<SnapshotVisualEvent>(other.VisualEvents);
            DamageEvents = new TrackingList<SnapshotDamageEvent>(other.DamageEvents);
            SoundEvents = new TrackingList<SnapshotSoundEvent>(other.SoundEvents);
            GibSpawnEvents = new TrackingList<SnapshotGibSpawnEvent>(other.GibSpawnEvents);
            RocketSpawnEvents = new TrackingList<SnapshotRocketSpawnEvent>(other.RocketSpawnEvents);
            Players = new TrackingList<SnapshotPlayerState>(other.Players);
            PlayerMovementStates = new TrackingList<SnapshotPlayerMovementState>(other.PlayerMovementStates);
            PlayerStatusStates = new TrackingList<SnapshotPlayerStatusState>(other.PlayerStatusStates);
            PlayerExtendedStatusStates = new TrackingList<SnapshotPlayerExtendedStatusState>(other.PlayerExtendedStatusStates);
            PlayerChatBubbleStates = new TrackingList<SnapshotPlayerChatBubbleState>(other.PlayerChatBubbleStates);
            Sentries = new TrackingList<SnapshotSentryState>(other.Sentries);
            SentryUpdateStates = new TrackingList<SnapshotSentryUpdateState>(other.SentryUpdateStates);
            Shots = new TrackingList<SnapshotShotState>(other.Shots);
            Bubbles = new TrackingList<SnapshotShotState>(other.Bubbles);
            Blades = new TrackingList<SnapshotShotState>(other.Blades);
            Needles = new TrackingList<SnapshotShotState>(other.Needles);
            RevolverShots = new TrackingList<SnapshotShotState>(other.RevolverShots);
            Rockets = new TrackingList<SnapshotRocketState>(other.Rockets);
            Flames = new TrackingList<SnapshotFlameState>(other.Flames);
            Flares = new TrackingList<SnapshotShotState>(other.Flares);
            Mines = new TrackingList<SnapshotMineState>(other.Mines);
            SentryGibs = new TrackingList<SnapshotSentryGibState>(other.SentryGibs);
            PlayerGibs = new TrackingList<SnapshotPlayerGibState>(other.PlayerGibs);
            DeadBodies = new TrackingList<SnapshotDeadBodyState>(other.DeadBodies);
            RemovedPlayerIds = new TrackingList<int>(other.RemovedPlayerIds);
            RemovedSentryIds = new TrackingList<int>(other.RemovedSentryIds);
            RemovedShotIds = new TrackingList<int>(other.RemovedShotIds);
            RemovedBubbleIds = new TrackingList<int>(other.RemovedBubbleIds);
            RemovedBladeIds = new TrackingList<int>(other.RemovedBladeIds);
            RemovedNeedleIds = new TrackingList<int>(other.RemovedNeedleIds);
            RemovedRevolverShotIds = new TrackingList<int>(other.RemovedRevolverShotIds);
            RemovedRocketIds = new TrackingList<int>(other.RemovedRocketIds);
            RemovedFlameIds = new TrackingList<int>(other.RemovedFlameIds);
            RemovedFlareIds = new TrackingList<int>(other.RemovedFlareIds);
            RemovedMineIds = new TrackingList<int>(other.RemovedMineIds);
            RemovedSentryGibIds = new TrackingList<int>(other.RemovedSentryGibIds);
            JumpPads = new TrackingList<SnapshotJumpPadState>(other.JumpPads);
            RemovedJumpPadIds = new TrackingList<int>(other.RemovedJumpPadIds);
            RemovedPlayerGibIds = new TrackingList<int>(other.RemovedPlayerGibIds);
            RemovedDeadBodyIds = new TrackingList<int>(other.RemovedDeadBodyIds);
        }

        public ulong BaselineFrame { get; }
        public TrackingList<SnapshotCombatTraceState> CombatTraces { get; }
        public TrackingList<SnapshotKillFeedEntry> KillFeed { get; }
        public TrackingList<SnapshotVisualEvent> VisualEvents { get; }
        public TrackingList<SnapshotDamageEvent> DamageEvents { get; }
        public TrackingList<SnapshotSoundEvent> SoundEvents { get; }
        public TrackingList<SnapshotGibSpawnEvent> GibSpawnEvents { get; }
        public TrackingList<SnapshotRocketSpawnEvent> RocketSpawnEvents { get; }
        public TrackingList<SnapshotPlayerState> Players { get; }
        public TrackingList<SnapshotPlayerMovementState> PlayerMovementStates { get; }
        public TrackingList<SnapshotPlayerStatusState> PlayerStatusStates { get; }
        public TrackingList<SnapshotPlayerExtendedStatusState> PlayerExtendedStatusStates { get; }
        public TrackingList<SnapshotPlayerChatBubbleState> PlayerChatBubbleStates { get; }
        public TrackingList<SnapshotSentryState> Sentries { get; } = [];
        public TrackingList<SnapshotSentryUpdateState> SentryUpdateStates { get; } = [];
        public TrackingList<SnapshotShotState> Shots { get; } = [];
        public TrackingList<SnapshotShotState> Bubbles { get; } = [];
        public TrackingList<SnapshotShotState> Blades { get; } = [];
        public TrackingList<SnapshotShotState> Needles { get; } = [];
        public TrackingList<SnapshotShotState> RevolverShots { get; } = [];
        public TrackingList<SnapshotRocketState> Rockets { get; } = [];
        public TrackingList<SnapshotFlameState> Flames { get; } = [];
        public TrackingList<SnapshotShotState> Flares { get; } = [];
        public TrackingList<SnapshotMineState> Mines { get; } = [];
        public TrackingList<SnapshotSentryGibState> SentryGibs { get; } = [];
        public TrackingList<SnapshotJumpPadState> JumpPads { get; } = [];
        public TrackingList<SnapshotPlayerGibState> PlayerGibs { get; } = [];
        public TrackingList<SnapshotDeadBodyState> DeadBodies { get; } = [];
        public TrackingList<int> RemovedPlayerIds { get; } = [];
        public TrackingList<int> RemovedSentryIds { get; } = [];
        public TrackingList<int> RemovedShotIds { get; } = [];
        public TrackingList<int> RemovedBubbleIds { get; } = [];
        public TrackingList<int> RemovedBladeIds { get; } = [];
        public TrackingList<int> RemovedNeedleIds { get; } = [];
        public TrackingList<int> RemovedRevolverShotIds { get; } = [];
        public TrackingList<int> RemovedRocketIds { get; } = [];
        public TrackingList<int> RemovedFlameIds { get; } = [];
        public TrackingList<int> RemovedFlareIds { get; } = [];
        public TrackingList<int> RemovedMineIds { get; } = [];
        public TrackingList<int> RemovedSentryGibIds { get; } = [];
        public TrackingList<int> RemovedJumpPadIds { get; } = [];
        public TrackingList<int> RemovedPlayerGibIds { get; } = [];
        public TrackingList<int> RemovedDeadBodyIds { get; } = [];

        public Builder Clone()
        {
            return new Builder(this);
        }

        public SnapshotMessage Build()
        {
            return _template with
            {
                BaselineFrame = BaselineFrame,
                IsDelta = true,
                Players = Players.ToArrayCached(),
                PlayerMovementStates = PlayerMovementStates.ToArrayCached(),
                PlayerStatusStates = PlayerStatusStates.ToArrayCached(),
                PlayerExtendedStatusStates = PlayerExtendedStatusStates.ToArrayCached(),
                PlayerChatBubbleStates = PlayerChatBubbleStates.ToArrayCached(),
                CombatTraces = CombatTraces.ToArrayCached(),
                Sentries = Sentries.ToArrayCached(),
                SentryUpdateStates = SentryUpdateStates.ToArrayCached(),
                Shots = Shots.ToArrayCached(),
                Bubbles = Bubbles.ToArrayCached(),
                Blades = Blades.ToArrayCached(),
                Needles = Needles.ToArrayCached(),
                RevolverShots = RevolverShots.ToArrayCached(),
                Rockets = Rockets.ToArrayCached(),
                Flames = Flames.ToArrayCached(),
                Flares = Flares.ToArrayCached(),
                Mines = Mines.ToArrayCached(),
                SentryGibs = SentryGibs.ToArrayCached(),
                JumpPads = JumpPads.ToArrayCached(),
                PlayerGibs = PlayerGibs.ToArrayCached(),
                DeadBodies = DeadBodies.ToArrayCached(),
                KillFeed = KillFeed.ToArrayCached(),
                VisualEvents = VisualEvents.ToArrayCached(),
                DamageEvents = DamageEvents.ToArrayCached(),
                SoundEvents = SoundEvents.ToArrayCached(),
                GibSpawnEvents = GibSpawnEvents.ToArrayCached(),
                RocketSpawnEvents = RocketSpawnEvents.ToArrayCached(),
                RemovedPlayerIds = RemovedPlayerIds.ToArrayCached(),
                RemovedSentryIds = RemovedSentryIds.ToArrayCached(),
                RemovedShotIds = RemovedShotIds.ToArrayCached(),
                RemovedBubbleIds = RemovedBubbleIds.ToArrayCached(),
                RemovedBladeIds = RemovedBladeIds.ToArrayCached(),
                RemovedNeedleIds = RemovedNeedleIds.ToArrayCached(),
                RemovedRevolverShotIds = RemovedRevolverShotIds.ToArrayCached(),
                RemovedRocketIds = RemovedRocketIds.ToArrayCached(),
                RemovedFlameIds = RemovedFlameIds.ToArrayCached(),
                RemovedFlareIds = RemovedFlareIds.ToArrayCached(),
                RemovedMineIds = RemovedMineIds.ToArrayCached(),
                RemovedSentryGibIds = RemovedSentryGibIds.ToArrayCached(),
                RemovedJumpPadIds = RemovedJumpPadIds.ToArrayCached(),
                RemovedPlayerGibIds = RemovedPlayerGibIds.ToArrayCached(),
                RemovedDeadBodyIds = RemovedDeadBodyIds.ToArrayCached(),
            };
        }
    }

    internal sealed class TrackingList<T> : IReadOnlyList<T>
    {
        private readonly List<T> _items;
        private T[]? _cachedArray;

        public TrackingList()
        {
            _items = [];
        }

        public TrackingList(IEnumerable<T> items)
        {
            _items = new List<T>(items);
        }

        public int Count => _items.Count;

        public T this[int index] => _items[index];

        public void Add(T item)
        {
            _items.Add(item);
            _cachedArray = null;
        }

        public void Clear()
        {
            if (_items.Count == 0)
            {
                return;
            }

            _items.Clear();
            _cachedArray = null;
        }

        public void RemoveLast()
        {
            if (_items.Count == 0)
            {
                return;
            }

            _items.RemoveAt(_items.Count - 1);
            _cachedArray = null;
        }

        public T[] ToArrayCached()
        {
            return _cachedArray ??= _items.Count == 0 ? Array.Empty<T>() : _items.ToArray();
        }

        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
