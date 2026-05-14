using System;
using System.Collections.Generic;
using System.Linq;
using OpenGarrison.Protocol;

namespace OpenGarrison.Server;

internal static class SnapshotDeltaBudgeter
{
    private readonly record struct SerializedSnapshot(SnapshotMessage Message, byte[] Payload);

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
            SnapshotDeltaBudgeter.ContributionKind.ProjectileSpawn,
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
        ApplyRequiredContributions(
            builder,
            orderedContributions,
            appliedContributions,
            SnapshotDeltaBudgeter.ContributionKind.PlayerMovementUpdate,
            ref remainingBudget);
        ApplyRequiredContributions(
            builder,
            orderedContributions,
            appliedContributions,
            SnapshotDeltaBudgeter.ContributionKind.PlayerExtendedStatusUpdate,
            ref remainingBudget);
        ApplyRequiredContributions(
            builder,
            orderedContributions,
            appliedContributions,
            SnapshotDeltaBudgeter.ContributionKind.PlayerChatBubbleUpdate,
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

        if (payloadSize > targetPayloadBytes)
        {
            if (TryReduceToBudget(builder, targetPayloadBytes, ref serializePassCount) is { } reducedBuilderSnapshot)
            {
                snapshot = reducedBuilderSnapshot.Message;
                payload = reducedBuilderSnapshot.Payload;
            }
            else
            {
                if (TryReduceSnapshotForBudget(snapshot, targetPayloadBytes, ref serializePassCount) is { } reducedSnapshot)
                {
                    snapshot = reducedSnapshot.Message;
                    payload = reducedSnapshot.Payload;
                }
                else
                {
                    snapshot = ReduceSnapshotToAbsoluteMinimum(snapshot);
                    payload = Serialize(snapshot, ref serializePassCount);
                }
            }
        }
        else
        {
            payload = Serialize(snapshot, ref serializePassCount);
        }

        return new SnapshotBudgetBuildResult(snapshot, payload!, serializePassCount);
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
        // Keep VisualEvents (rocket explosions) - let priority system decide
        // Keep DamageEvents - needed for client-side blood and hit feedback
        changed |= ClearIfAny(builder.SoundEvents);
        return changed;
    }

    private static SerializedSnapshot? TryReduceToBudget(Builder builder, int targetPayloadBytes, ref int serializePassCount)
    {
        foreach (var dropStep in BudgetDropSteps)
        {
            if (!dropStep(builder))
            {
                continue;
            }

            var snapshot = builder.Build();
            var payloadSize = Measure(snapshot);
            if (payloadSize <= targetPayloadBytes)
            {
                return new SerializedSnapshot(snapshot, Serialize(snapshot, payloadSize, ref serializePassCount));
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
        if (payloadSize <= targetPayloadBytes)
        {
            return new SerializedSnapshot(snapshot, Serialize(snapshot, payloadSize, ref serializePassCount));
        }

        snapshot = snapshot with
        {
            Players = snapshot.Players
                .Select(ReducePlayerStateAggressivelyForBudget)
                .ToArray(),
        };

        payloadSize = Measure(snapshot);
        if (payloadSize <= targetPayloadBytes)
        {
            return new SerializedSnapshot(snapshot, Serialize(snapshot, payloadSize, ref serializePassCount));
        }

        snapshot = snapshot with
        {
            Players = Array.Empty<SnapshotPlayerState>(),
            PlayerMovementStates = Array.Empty<SnapshotPlayerMovementState>(),
            PlayerExtendedStatusStates = Array.Empty<SnapshotPlayerExtendedStatusState>(),
        };

        var payload = Serialize(snapshot, ref serializePassCount);
        return payload.Length <= targetPayloadBytes
            ? new SerializedSnapshot(snapshot, payload)
            : null;
    }

    private static int Measure(SnapshotMessage snapshot)
    {
        return ProtocolCodec.MeasureSerializedSize(snapshot);
    }

    private static byte[] Serialize(SnapshotMessage snapshot, ref int serializePassCount)
    {
        serializePassCount += 1;
        return ProtocolCodec.Serialize(snapshot, ServerProtocolCompression.Settings);
    }

    private static byte[] Serialize(SnapshotMessage snapshot, int measuredSize, ref int serializePassCount)
    {
        serializePassCount += 1;
        return ProtocolCodec.Serialize(snapshot, measuredSize, ServerProtocolCompression.Settings);
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
            changed |= ClearIfAny(builder.SoundEvents);
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

        public T[] ToArrayCached()
        {
            return _cachedArray ??= _items.Count == 0 ? Array.Empty<T>() : _items.ToArray();
        }

        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
