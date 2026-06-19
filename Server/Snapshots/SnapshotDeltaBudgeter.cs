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
        LocalPlayerExtendedStatusUpdate,
        PlayerChatBubbleUpdate,
        PlayerFirstAppearance,
        PlayerRosterUpdate,
        PlayerRemoval,
        EntityRemoval,
        EntityStateUpdate,
        EntityFirstAppearance,
        TransientSoundEvent,
        TransientDamageEvent,
        ProjectileSpawn,
        ProjectileMotionUpdate,
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

        ApplyRequiredContributions(
            builder,
            orderedContributions,
            appliedContributions,
            SnapshotDeltaBudgeter.ContributionKind.PlayerFirstAppearance,
            ref remainingBudget);
        ApplyRequiredContributions(
            builder,
            orderedContributions,
            appliedContributions,
            SnapshotDeltaBudgeter.ContributionKind.PlayerRosterUpdate,
            ref remainingBudget);
        ApplyRequiredContributions(
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
            SnapshotDeltaBudgeter.ContributionKind.EntityFirstAppearance,
            ref remainingBudget);
        ApplyRequiredContributions(
            builder,
            orderedContributions,
            appliedContributions,
            SnapshotDeltaBudgeter.ContributionKind.EntityStateUpdate,
            ref remainingBudget);
        ApplyRequiredContributions(
            builder,
            orderedContributions,
            appliedContributions,
            SnapshotDeltaBudgeter.ContributionKind.LocalPlayerUpdate,
            ref remainingBudget);
        ApplyRequiredContributions(
            builder,
            orderedContributions,
            appliedContributions,
            SnapshotDeltaBudgeter.ContributionKind.PlayerMovementUpdate,
            ref remainingBudget);
        ApplyRequiredRosterContribution(
            builder,
            orderedContributions,
            appliedContributions,
            SnapshotDeltaBudgeter.ContributionKind.LocalPlayerStatusUpdate,
            ref remainingBudget);
        ApplyRequiredContributions(
            builder,
            orderedContributions,
            appliedContributions,
            SnapshotDeltaBudgeter.ContributionKind.LocalPlayerExtendedStatusUpdate,
            ref remainingBudget);
        ApplyRequiredContributions(
            builder,
            orderedContributions,
            appliedContributions,
            SnapshotDeltaBudgeter.ContributionKind.TransientSoundEvent,
            ref remainingBudget);
        ApplyRequiredContributions(
            builder,
            orderedContributions,
            appliedContributions,
            SnapshotDeltaBudgeter.ContributionKind.TransientDamageEvent,
            ref remainingBudget);
        // Projectile spawns (first appearance of any bullet/rocket/etc.) are treated as
        // required so that new projectiles are never silently dropped under budget pressure.
        // Skipped motion-only backfill is budget-admitted immediately afterward so spare
        // packet room goes to smoothing projectile flight before ordinary optional data.
        ApplyRequiredContributions(
            builder,
            orderedContributions,
            appliedContributions,
            SnapshotDeltaBudgeter.ContributionKind.ProjectileSpawn,
            ref remainingBudget);
        ApplyBudgetedContributions(
            builder,
            orderedContributions,
            appliedContributions,
            SnapshotDeltaBudgeter.ContributionKind.ProjectileMotionUpdate,
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
        var candidateComposition = BuildCompositionMetrics(snapshot, targetPayloadBytes, payloadSize, payload.Length);

        if (payload.Length > targetPayloadBytes)
        {
            if (TryReduceToBudget(builder, targetPayloadBytes, ref serializePassCount) is { } reducedBuilderSnapshot)
            {
                snapshot = reducedBuilderSnapshot.Message;
                payload = reducedBuilderSnapshot.Payload;
                payloadSize = reducedBuilderSnapshot.UncompressedBytes;
            }
            else if (TryReduceSnapshotForBudget(builder.Build(), targetPayloadBytes, ref serializePassCount) is { } reducedSnapshot)
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
        return new SnapshotBudgetBuildResult(snapshot, payload, serializePassCount, composition, candidateComposition, ReductionApplied: true);
    }

    internal static SnapshotBudgetBuildResult BuildUntrimmedSnapshotWithEmergencyReduction(
        SnapshotMessage fullSnapshot,
        ISnapshotBaselineState? baseline,
        IReadOnlyList<Contribution> contributions,
        int targetPayloadBytes = TargetSnapshotPayloadBytes)
    {
        var builder = new Builder(fullSnapshot, baseline?.Frame ?? 0, seedFromTemplateCollections: false);
        for (var index = 0; index < contributions.Count; index += 1)
        {
            contributions[index].Apply(builder);
        }

        var snapshot = builder.Build();
        var serializePassCount = 0;
        var payloadSize = Measure(snapshot);
        var payload = Serialize(snapshot, payloadSize, ref serializePassCount);
        var candidateComposition = BuildCompositionMetrics(snapshot, targetPayloadBytes, payloadSize, payload.Length);
        var reductionApplied = false;

        if (payload.Length > targetPayloadBytes)
        {
            reductionApplied = true;
            var reducedSnapshot = ReduceGameplayCriticalSnapshot(
                builder,
                targetPayloadBytes,
                new SerializedSnapshot(snapshot, payload, payloadSize),
                ref serializePassCount);
            snapshot = reducedSnapshot.Message;
            payload = reducedSnapshot.Payload;
            payloadSize = reducedSnapshot.UncompressedBytes;
        }

        var composition = BuildCompositionMetrics(snapshot, targetPayloadBytes, payloadSize, payload.Length);
        return new SnapshotBudgetBuildResult(snapshot, payload, serializePassCount, composition, candidateComposition, reductionApplied);
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

    private static void ApplyBudgetedContributions(
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
            if (contribution.EstimatedBytes > remainingBudget)
            {
                continue;
            }

            contribution.Apply(builder);
            appliedContributions[index] = true;
            remainingBudget -= contribution.EstimatedBytes;
        }
    }

    private static bool TrimAuxiliaryCollections(Builder builder)
    {
        var changed = false;
        changed |= ClearIfAny(builder.KillFeed);
        // Network-visible combat traces are sniper tracers. They are dropped
        // one-at-a-time (farthest-first) in BudgetDropSteps after gibs and small
        // projectiles, so the packet is filled to the maximum before any tracer
        // is removed. Entries that don't fit can reappear while they persist.
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

    private static SerializedSnapshot ReduceGameplayCriticalSnapshot(
        Builder builder,
        int targetPayloadBytes,
        SerializedSnapshot latest,
        ref int serializePassCount)
    {
        if (latest.Payload.Length <= targetPayloadBytes)
        {
            return latest;
        }

        var madeProgress = true;
        while (madeProgress)
        {
            madeProgress = false;
            foreach (var dropStep in GameplayCriticalDropSteps)
            {
                if (!dropStep(builder))
                {
                    continue;
                }

                madeProgress = true;
                latest = SerializeBuilderSnapshot(builder, ref serializePassCount);
                if (latest.Payload.Length <= targetPayloadBytes)
                {
                    return latest;
                }
            }
        }

        return latest;
    }

    private static SerializedSnapshot SerializeBuilderSnapshot(Builder builder, ref int serializePassCount)
    {
        var snapshot = builder.Build();
        var payloadSize = Measure(snapshot);
        var payload = Serialize(snapshot, payloadSize, ref serializePassCount);
        return new SerializedSnapshot(snapshot, payload, payloadSize);
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
            + EstimateUShortCountCollection(snapshot.RocketSpawnEvents, EstimateRocketSpawnEventBytes)
            + EstimateUShortCountCollection(snapshot.Flames, static _ => 50)
            + EstimateUShortCountCollection(snapshot.Mines, static _ => 32)
            + EstimateUShortCountCollection(snapshot.Grenades, static _ => 48);
        var sentryBytes =
            EstimateUShortCountCollection(snapshot.Sentries, static _ => 44)
            + EstimateUShortCountCollection(snapshot.SentryUpdateStates, static _ => 37)
            + EstimateUShortCountCollection(snapshot.JumpPads, static _ => 22);
        var eventBytes =
            EstimateUShortCountCollection(snapshot.CombatTraces, static _ => 24)
            + EstimateUShortCountCollection(snapshot.SniperAimIndicators, static _ => 18)
            + EstimateByteCountCollection(snapshot.KillFeed, EstimateKillFeedBytes)
            + EstimateUShortCountCollection(snapshot.VisualEvents, EstimateVisualEventBytes)
            + EstimateUShortCountCollection(snapshot.DamageEvents, static _ => 42)
            + EstimateUShortCountCollection(snapshot.SoundEvents, EstimateSoundEventBytes)
            + EstimateUShortCountCollection(snapshot.GibSpawnEvents, EstimateGibSpawnEventBytes)
            + EstimateUShortCountCollection(snapshot.DeadBodies, static _ => 40)
            + EstimateUShortCountCollection(snapshot.SentryGibs, static _ => 17)
            + EstimateUShortCountCollection(snapshot.JumpPadGibs, static _ => 17);
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
            + EstimateEntityIdListBytes(snapshot.RemovedGrenadeIds)
            + EstimateEntityIdListBytes(snapshot.RemovedDeadBodyIds)
            + EstimateEntityIdListBytes(snapshot.RemovedSentryGibIds)
            + EstimateEntityIdListBytes(snapshot.RemovedJumpPadIds)
            + EstimateEntityIdListBytes(snapshot.RemovedJumpPadGibIds);
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
        var bytes = 211
            + EstimateStringBytes(player.Name)
            + EstimateCachedStringBytes(player.GameplayModPackCacheId, player.GameplayModPackId)
            + EstimateCachedStringBytes(player.GameplayLoadoutCacheId, player.GameplayLoadoutId)
            + EstimateCachedStringBytes(player.GameplayPrimaryItemCacheId, player.GameplayPrimaryItemId)
            + EstimateCachedStringBytes(player.GameplaySecondaryItemCacheId, player.GameplaySecondaryItemId)
            + EstimateCachedStringBytes(player.GameplayUtilityItemCacheId, player.GameplayUtilityItemId)
            + EstimateCachedStringBytes(player.GameplayEquippedItemCacheId, player.GameplayEquippedItemId)
            + EstimateCachedStringBytes(player.GameplayAcquiredItemCacheId, player.GameplayAcquiredItemId)
            + EstimateCachedStringBytes(player.GameplayClassCacheId, player.GameplayClassId)
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
            + 28
            + (entry.InvolvedPlayerIds.Count * sizeof(int));
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

    private static int EstimateRocketSpawnEventBytes(SnapshotRocketSpawnEvent rocketEvent)
    {
        return 82 + 2 + ((rocketEvent.PassedFriendlyPlayerIds?.Count ?? 0) * 4);
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
        // soldier_shotgun_equipped that clients need even under budget pressure. Most Whole/Scalar
        // int entries can be dropped since the animation-critical cooldown/reload values now arrive
        // via the movement delta (OffhandCooldownTicks / OffhandReloadTicks fields).
        // Exception: ammo count entries (keys containing "_ammo" under core.player) are preserved here
        // as a safety net; they also ship via the high-priority status path but keeping them here
        // ensures correctness if both arrive in the same snapshot.
        // Plugin ability cooldowns are also kept because the HUD reads them from replicated state.
        var reducedReplicatedStates = player.ReplicatedStates is { Count: > 0 }
            ? player.ReplicatedStates
                .Where(static e => e.Kind == SnapshotReplicatedStateValueKind.Toggle
                    || IsBudgetCriticalReplicatedState(e))
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
            IsTypingChatMessage = false,
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

    private static bool IsBudgetCriticalReplicatedState(SnapshotReplicatedStateEntry entry)
    {
        return (string.Equals(entry.OwnerId, "core.player", StringComparison.Ordinal)
                && entry.Key.IndexOf("_ammo", StringComparison.Ordinal) >= 0)
            || (!string.Equals(entry.OwnerId, "core.player", StringComparison.Ordinal)
                && entry.Kind == SnapshotReplicatedStateValueKind.Whole
                && entry.Key.IndexOf("cooldown", StringComparison.OrdinalIgnoreCase) >= 0);
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
            IsTypingChatMessage = false,
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
            PingMilliseconds = -1,
        };
    }

    private static SnapshotMessage ReduceSnapshotToAbsoluteMinimum(SnapshotMessage snapshot)
    {
        return snapshot with
        {
            EntityCollectionCompletenessFlags = SnapshotEntityCollectionCompletenessFlags.None,
            LevelName = string.Empty,
            MapDownloadUrl = string.Empty,
            MapContentHash = string.Empty,
            Players = Array.Empty<SnapshotPlayerState>(),
            PlayerExtendedStatusStates = Array.Empty<SnapshotPlayerExtendedStatusState>(),
            CombatTraces = Array.Empty<SnapshotCombatTraceState>(),
            SniperAimIndicators = Array.Empty<SnapshotSniperAimIndicatorState>(),
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
            Grenades = Array.Empty<SnapshotGrenadeState>(),
            PlayerGibs = Array.Empty<SnapshotPlayerGibState>(),
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
            JumpPadGibs = Array.Empty<SnapshotJumpPadGibState>(),
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
            RemovedGrenadeIds = Array.Empty<int>(),
            RemovedPlayerGibIds = Array.Empty<int>(),
            RemovedSentryGibIds = Array.Empty<int>(),
            RemovedJumpPadIds = Array.Empty<int>(),
            RemovedJumpPadGibIds = Array.Empty<int>(),
        };
    }

    private static readonly Func<Builder, bool>[] BudgetDropSteps =
    [
        static builder => ClearIfAny(builder.KillFeed),
        static builder =>
        {
            var changed = false;
            changed |= ClearIfAny(builder.GibSpawnEvents);
            changed |= ClearIfAny(builder.SentryGibs);
            changed |= ClearIfAny(builder.JumpPadGibs);
            return changed;
        },
        static builder => RemoveLastIfAboveMinimum(builder.PlayerChatBubbleStates, minimumCount: 0),
        static builder => RemoveLastIfAboveMinimum(builder.PlayerExtendedStatusStates, minimumCount: 1),
        // Drop SniperAimIndicators and network-visible sniper CombatTraces one entry at a time
        // (farthest-first, since the contribution planner adds nearest entries first). This ensures
        // the packet is filled to the maximum before any tracer is dropped, and entries at the edge
        // of the viewport go first.
        static builder => RemoveLastIfAboveMinimum(builder.SniperAimIndicators, minimumCount: 0),
        static builder => RemoveLastIfAboveMinimum(builder.CombatTraces, minimumCount: 0),
        static builder => RemoveLastIfAboveMinimum(builder.SoundEvents, minimumCount: 1),
        static builder => RemoveLastIfAboveMinimum(builder.RocketSpawnEvents, minimumCount: 0),
        // Projectile motion is visual-critical: keep it until cheaper cosmetics, traces, and
        // repeated transient events have already been exhausted.
        static builder => ClearProjectileCollectionIfAny(builder, builder.Flares, SnapshotEntityCollectionCompletenessFlags.Flares),
        static builder => ClearProjectileCollectionIfAny(builder, builder.Blades, SnapshotEntityCollectionCompletenessFlags.Blades),
        static builder => ClearProjectileCollectionIfAny(builder, builder.Bubbles, SnapshotEntityCollectionCompletenessFlags.Bubbles),
        static builder => ClearProjectileCollectionIfAny(builder, builder.Needles, SnapshotEntityCollectionCompletenessFlags.Needles),
        static builder => ClearProjectileCollectionIfAny(builder, builder.RevolverShots, SnapshotEntityCollectionCompletenessFlags.RevolverShots),
        static builder => ClearProjectileCollectionIfAny(builder, builder.Shots, SnapshotEntityCollectionCompletenessFlags.Shots),
        static builder =>
        {
            // VisualEvents (explosions, impacts, etc.) are dropped together with the
            // projectile collections that produce them. This prevents a grenade/mine/rocket
            // from disappearing in one snapshot while its explosion visual is withheld until
            // a later snapshot due to budget pressure.
            var changed = false;
            changed |= ClearIfAny(builder.VisualEvents);
            changed |= ClearProjectileCollectionIfAny(builder, builder.Mines, SnapshotEntityCollectionCompletenessFlags.Mines);
            changed |= ClearProjectileCollectionIfAny(builder, builder.Grenades, SnapshotEntityCollectionCompletenessFlags.Grenades);
            changed |= ClearProjectileCollectionIfAny(builder, builder.Flames, SnapshotEntityCollectionCompletenessFlags.Flames);
            changed |= ClearProjectileCollectionIfAny(builder, builder.Rockets, SnapshotEntityCollectionCompletenessFlags.Rockets);
            changed |= ClearIfAny(builder.Sentries);
            changed |= ClearIfAny(builder.JumpPads);
            return changed;
        },
        static builder =>
        {
            var changed = false;
            changed |= ClearIfAny(builder.RemovedPlayerIds);
            changed |= ClearIfAny(builder.RemovedSentryGibIds);
            changed |= ClearIfAny(builder.RemovedJumpPadGibIds);
            changed |= ClearIfAny(builder.RemovedJumpPadIds);
            return changed;
        },
        static builder =>
        {
            var changed = false;
            changed |= ClearProjectileRemovalIfAny(builder, builder.RemovedFlareIds, SnapshotEntityCollectionCompletenessFlags.Flares);
            changed |= ClearProjectileRemovalIfAny(builder, builder.RemovedBladeIds, SnapshotEntityCollectionCompletenessFlags.Blades);
            changed |= ClearProjectileRemovalIfAny(builder, builder.RemovedBubbleIds, SnapshotEntityCollectionCompletenessFlags.Bubbles);
            changed |= ClearProjectileRemovalIfAny(builder, builder.RemovedNeedleIds, SnapshotEntityCollectionCompletenessFlags.Needles);
            changed |= ClearProjectileRemovalIfAny(builder, builder.RemovedRevolverShotIds, SnapshotEntityCollectionCompletenessFlags.RevolverShots);
            changed |= ClearProjectileRemovalIfAny(builder, builder.RemovedShotIds, SnapshotEntityCollectionCompletenessFlags.Shots);
            return changed;
        },
        static builder =>
        {
            var changed = false;
            changed |= ClearProjectileRemovalIfAny(builder, builder.RemovedMineIds, SnapshotEntityCollectionCompletenessFlags.Mines);
            changed |= ClearProjectileRemovalIfAny(builder, builder.RemovedGrenadeIds, SnapshotEntityCollectionCompletenessFlags.Grenades);
            changed |= ClearProjectileRemovalIfAny(builder, builder.RemovedFlameIds, SnapshotEntityCollectionCompletenessFlags.Flames);
            changed |= ClearProjectileRemovalIfAny(builder, builder.RemovedRocketIds, SnapshotEntityCollectionCompletenessFlags.Rockets);
            changed |= ClearIfAny(builder.RemovedSentryIds);
            return changed;
        },
    ];

    private static readonly Func<Builder, bool>[] GameplayCriticalDropSteps =
    [
        static builder => ClearIfAny(builder.KillFeed),
        static builder =>
        {
            var changed = false;
            changed |= ClearIfAny(builder.GibSpawnEvents);
            changed |= ClearIfAny(builder.PlayerGibs);
            changed |= ClearIfAny(builder.SentryGibs);
            changed |= ClearIfAny(builder.JumpPadGibs);
            changed |= ClearIfAny(builder.DeadBodies);
            return changed;
        },
        static builder => RemoveLastIfAboveMinimum(builder.SniperAimIndicators, minimumCount: 0),
        static builder => RemoveLastIfAboveMinimum(builder.CombatTraces, minimumCount: 0),
        static builder => ReducePlayersForGameplayCriticalBudget(builder),
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

    private static bool ClearProjectileCollectionIfAny<T>(
        Builder builder,
        TrackingList<T> list,
        SnapshotEntityCollectionCompletenessFlags flag)
    {
        if (!ClearIfAny(list))
        {
            return false;
        }

        builder.MarkEntityCollectionIncomplete(flag);
        return true;
    }

    private static bool ClearProjectileRemovalIfAny(
        Builder builder,
        TrackingList<int> list,
        SnapshotEntityCollectionCompletenessFlags flag)
    {
        if (!ClearIfAny(list))
        {
            return false;
        }

        builder.MarkEntityCollectionIncomplete(flag);
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

    private static bool ReducePlayersForGameplayCriticalBudget(Builder builder)
    {
        if (builder.Players.Count == 0)
        {
            return false;
        }

        var reducedPlayers = new SnapshotPlayerState[builder.Players.Count];
        var changed = false;
        for (var index = 0; index < builder.Players.Count; index += 1)
        {
            var player = builder.Players[index];
            var reducedPlayer = ReducePlayerStateForGameplayCriticalBudget(player);
            reducedPlayers[index] = reducedPlayer;
            changed |= !EqualityComparer<SnapshotPlayerState>.Default.Equals(player, reducedPlayer);
        }

        if (!changed)
        {
            return false;
        }

        builder.Players.ReplaceWith(reducedPlayers);
        return true;
    }

    private static SnapshotPlayerState ReducePlayerStateForGameplayCriticalBudget(SnapshotPlayerState player)
    {
        return player with
        {
            BadgeMask = 0,
            OwnedGameplayItemIds = Array.Empty<string>(),
            IsChatBubbleVisible = false,
            ChatBubbleFrameIndex = 0,
            ChatBubbleAlpha = 0f,
            IsTypingChatMessage = false,
            Kills = 0,
            Deaths = 0,
            Caps = 0,
            Points = 0f,
            HealPoints = 0,
            ActiveDominationCount = 0,
            IsDominatingLocalViewer = false,
            IsDominatedByLocalViewer = false,
            Assists = 0,
            GibDeaths = 0,
        };
    }

    internal sealed class Builder
    {
        private readonly SnapshotMessage _template;

        public Builder(SnapshotMessage template, ulong baselineFrame, bool seedFromTemplateCollections)
        {
            _template = template;
            BaselineFrame = baselineFrame;
            EntityCollectionCompletenessFlags = template.EntityCollectionCompletenessFlags;
            CombatTraces = seedFromTemplateCollections ? new TrackingList<SnapshotCombatTraceState>(template.CombatTraces) : [];
            SniperAimIndicators = seedFromTemplateCollections ? new TrackingList<SnapshotSniperAimIndicatorState>(template.SniperAimIndicators) : [];
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
            Grenades = seedFromTemplateCollections ? new TrackingList<SnapshotGrenadeState>(template.Grenades) : [];
            SentryGibs = seedFromTemplateCollections ? new TrackingList<SnapshotSentryGibState>(template.SentryGibs) : [];
            JumpPads = seedFromTemplateCollections ? new TrackingList<SnapshotJumpPadState>(template.JumpPads) : [];
            JumpPadGibs = seedFromTemplateCollections ? new TrackingList<SnapshotJumpPadGibState>(template.JumpPadGibs) : [];
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
            RemovedGrenadeIds = new TrackingList<int>(template.RemovedGrenadeIds);
            RemovedSentryGibIds = new TrackingList<int>(template.RemovedSentryGibIds);
            RemovedJumpPadIds = new TrackingList<int>(template.RemovedJumpPadIds);
            RemovedJumpPadGibIds = new TrackingList<int>(template.RemovedJumpPadGibIds);
            RemovedPlayerGibIds = new TrackingList<int>(template.RemovedPlayerGibIds);
            RemovedDeadBodyIds = new TrackingList<int>(template.RemovedDeadBodyIds);
        }

        private Builder(Builder other)
        {
            _template = other._template;
            BaselineFrame = other.BaselineFrame;
            EntityCollectionCompletenessFlags = other.EntityCollectionCompletenessFlags;
            CombatTraces = new TrackingList<SnapshotCombatTraceState>(other.CombatTraces);
            SniperAimIndicators = new TrackingList<SnapshotSniperAimIndicatorState>(other.SniperAimIndicators);
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
            Grenades = new TrackingList<SnapshotGrenadeState>(other.Grenades);
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
            RemovedGrenadeIds = new TrackingList<int>(other.RemovedGrenadeIds);
            RemovedSentryGibIds = new TrackingList<int>(other.RemovedSentryGibIds);
            JumpPads = new TrackingList<SnapshotJumpPadState>(other.JumpPads);
            JumpPadGibs = new TrackingList<SnapshotJumpPadGibState>(other.JumpPadGibs);
            RemovedJumpPadIds = new TrackingList<int>(other.RemovedJumpPadIds);
            RemovedJumpPadGibIds = new TrackingList<int>(other.RemovedJumpPadGibIds);
            RemovedPlayerGibIds = new TrackingList<int>(other.RemovedPlayerGibIds);
            RemovedDeadBodyIds = new TrackingList<int>(other.RemovedDeadBodyIds);
        }

        public ulong BaselineFrame { get; }
        public SnapshotEntityCollectionCompletenessFlags EntityCollectionCompletenessFlags { get; private set; }
        public TrackingList<SnapshotCombatTraceState> CombatTraces { get; }
        public TrackingList<SnapshotSniperAimIndicatorState> SniperAimIndicators { get; }
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
        public TrackingList<SnapshotGrenadeState> Grenades { get; } = [];
        public TrackingList<SnapshotSentryGibState> SentryGibs { get; } = [];
        public TrackingList<SnapshotJumpPadState> JumpPads { get; } = [];
        public TrackingList<SnapshotJumpPadGibState> JumpPadGibs { get; } = [];
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
        public TrackingList<int> RemovedGrenadeIds { get; } = [];
        public TrackingList<int> RemovedSentryGibIds { get; } = [];
        public TrackingList<int> RemovedJumpPadIds { get; } = [];
        public TrackingList<int> RemovedJumpPadGibIds { get; } = [];
        public TrackingList<int> RemovedPlayerGibIds { get; } = [];
        public TrackingList<int> RemovedDeadBodyIds { get; } = [];

        public Builder Clone()
        {
            return new Builder(this);
        }

        public void MarkEntityCollectionIncomplete(SnapshotEntityCollectionCompletenessFlags flag)
        {
            EntityCollectionCompletenessFlags &= ~flag;
        }

        public SnapshotMessage Build()
        {
            return _template with
            {
                BaselineFrame = BaselineFrame,
                IsDelta = true,
                EntityCollectionCompletenessFlags = EntityCollectionCompletenessFlags,
                Players = Players.ToArrayCached(),
                PlayerMovementStates = PlayerMovementStates.ToArrayCached(),
                PlayerStatusStates = PlayerStatusStates.ToArrayCached(),
                PlayerExtendedStatusStates = PlayerExtendedStatusStates.ToArrayCached(),
                PlayerChatBubbleStates = PlayerChatBubbleStates.ToArrayCached(),
                CombatTraces = CombatTraces.ToArrayCached(),
                SniperAimIndicators = SniperAimIndicators.ToArrayCached(),
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
                Grenades = Grenades.ToArrayCached(),
                SentryGibs = SentryGibs.ToArrayCached(),
                JumpPads = JumpPads.ToArrayCached(),
                JumpPadGibs = JumpPadGibs.ToArrayCached(),
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
                RemovedGrenadeIds = RemovedGrenadeIds.ToArrayCached(),
                RemovedSentryGibIds = RemovedSentryGibIds.ToArrayCached(),
                RemovedJumpPadIds = RemovedJumpPadIds.ToArrayCached(),
                RemovedJumpPadGibIds = RemovedJumpPadGibIds.ToArrayCached(),
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

        public void ReplaceWith(IEnumerable<T> items)
        {
            _items.Clear();
            _items.AddRange(items);
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
