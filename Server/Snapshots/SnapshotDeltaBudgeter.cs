using System;
using System.Collections.Generic;
using System.Linq;
using OpenGarrison.Protocol;

namespace OpenGarrison.Server;

internal static class SnapshotDeltaBudgeter
{
    private readonly record struct SerializedSnapshot(SnapshotMessage Message, byte[] Payload);

    public const int TargetSnapshotPayloadBytes = 1400;
    public const int ReliableStreamTargetSnapshotPayloadBytes = 8 * 1024;

    internal enum ContributionKind
    {
        Optional,
        LocalPlayerUpdate,
        PlayerMovementUpdate,
        PlayerFirstAppearance,
        PlayerRosterUpdate,
        PlayerRemoval,
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

        if (payloadSize > targetPayloadBytes)
        {
            TrimAuxiliaryCollections(builder);
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
            SnapshotDeltaBudgeter.ContributionKind.LocalPlayerUpdate,
            ref remainingBudget);
        ApplyRequiredContributions(
            builder,
            orderedContributions,
            appliedContributions,
            SnapshotDeltaBudgeter.ContributionKind.PlayerMovementUpdate,
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

    private static void TrimAuxiliaryCollections(Builder builder)
    {
        builder.KillFeed.Clear();
        builder.CombatTraces.Clear();
        // Keep VisualEvents (rocket explosions) - let priority system decide
        // Keep DamageEvents - needed for client-side blood and hit feedback
        builder.SoundEvents.Clear();
    }

    private static SerializedSnapshot? TryReduceToBudget(Builder builder, int targetPayloadBytes, ref int serializePassCount)
    {
        foreach (var dropStep in BudgetDropSteps)
        {
            dropStep(builder);

            var snapshot = builder.Build();
            if (Measure(snapshot) <= targetPayloadBytes)
            {
                return new SerializedSnapshot(snapshot, Serialize(snapshot, ref serializePassCount));
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

        if (Measure(snapshot) <= targetPayloadBytes)
        {
            return new SerializedSnapshot(snapshot, Serialize(snapshot, ref serializePassCount));
        }

        snapshot = snapshot with
        {
            Players = snapshot.Players
                .Select(ReducePlayerStateAggressivelyForBudget)
                .ToArray(),
        };

        if (Measure(snapshot) <= targetPayloadBytes)
        {
            return new SerializedSnapshot(snapshot, Serialize(snapshot, ref serializePassCount));
        }

        snapshot = snapshot with
        {
            Players = Array.Empty<SnapshotPlayerState>(),
            PlayerMovementStates = Array.Empty<SnapshotPlayerMovementState>(),
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
        return ProtocolCodec.Serialize(snapshot);
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
            GameplayModPackId = string.Empty,
            GameplayLoadoutId = string.Empty,
            GameplayPrimaryItemId = string.Empty,
            GameplaySecondaryItemId = string.Empty,
            GameplayUtilityItemId = string.Empty,
            GameplayEquippedItemId = string.Empty,
            GameplayAcquiredItemId = string.Empty,
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
            BadgeMask = 0,
            GameplayModPackId = string.Empty,
            GameplayLoadoutId = string.Empty,
            GameplayPrimaryItemId = string.Empty,
            GameplaySecondaryItemId = string.Empty,
            GameplayUtilityItemId = string.Empty,
            GameplayEquippedItemId = string.Empty,
            GameplayAcquiredItemId = string.Empty,
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

    private static readonly Action<Builder>[] BudgetDropSteps =
    [
        static builder =>
        {
            builder.SoundEvents.Clear();
            builder.VisualEvents.Clear();
            builder.KillFeed.Clear();
        },
        static builder => builder.CombatTraces.Clear(),
        static builder =>
        {
            builder.PlayerGibs.Clear();
            builder.GibSpawnEvents.Clear();
            builder.SentryGibs.Clear();
            builder.DeadBodies.Clear();
        },
        static builder =>
        {
            builder.Flares.Clear();
            builder.Blades.Clear();
            builder.Bubbles.Clear();
            builder.Needles.Clear();
            builder.RevolverShots.Clear();
            builder.Shots.Clear();
            // Drop damage events only after cosmetic projectiles are gone
            builder.DamageEvents.Clear();
        },
        static builder =>
        {
            builder.Mines.Clear();
            builder.Flames.Clear();
            builder.Rockets.Clear();
            builder.Sentries.Clear();
            builder.JumpPads.Clear();
        },
        static builder =>
        {
            builder.RemovedPlayerIds.Clear();
            builder.RemovedPlayerGibIds.Clear();
            builder.RemovedSentryGibIds.Clear();
            builder.RemovedJumpPadIds.Clear();
            builder.RemovedDeadBodyIds.Clear();
        },
        static builder =>
        {
            builder.RemovedFlareIds.Clear();
            builder.RemovedBladeIds.Clear();
            builder.RemovedBubbleIds.Clear();
            builder.RemovedNeedleIds.Clear();
            builder.RemovedRevolverShotIds.Clear();
            builder.RemovedShotIds.Clear();
        },
        static builder =>
        {
            builder.RemovedMineIds.Clear();
            builder.RemovedFlameIds.Clear();
            builder.RemovedRocketIds.Clear();
            builder.RemovedSentryIds.Clear();
        },
    ];

    internal sealed class Builder
    {
        private readonly SnapshotMessage _template;

        public Builder(SnapshotMessage template, ulong baselineFrame, bool seedFromTemplateCollections)
        {
            _template = template;
            BaselineFrame = baselineFrame;
            CombatTraces = seedFromTemplateCollections ? new List<SnapshotCombatTraceState>(template.CombatTraces) : [];
            KillFeed = seedFromTemplateCollections ? new List<SnapshotKillFeedEntry>(template.KillFeed) : [];
            VisualEvents = seedFromTemplateCollections ? new List<SnapshotVisualEvent>(template.VisualEvents) : [];
            DamageEvents = seedFromTemplateCollections ? new List<SnapshotDamageEvent>(template.DamageEvents) : [];
            SoundEvents = seedFromTemplateCollections ? new List<SnapshotSoundEvent>(template.SoundEvents) : [];
            GibSpawnEvents = seedFromTemplateCollections ? new List<SnapshotGibSpawnEvent>(template.GibSpawnEvents) : [];
            Players = seedFromTemplateCollections ? new List<SnapshotPlayerState>(template.Players) : [];
            PlayerMovementStates = seedFromTemplateCollections ? new List<SnapshotPlayerMovementState>(template.PlayerMovementStates) : [];
            Sentries = seedFromTemplateCollections ? new List<SnapshotSentryState>(template.Sentries) : [];
            Shots = seedFromTemplateCollections ? new List<SnapshotShotState>(template.Shots) : [];
            Bubbles = seedFromTemplateCollections ? new List<SnapshotShotState>(template.Bubbles) : [];
            Blades = seedFromTemplateCollections ? new List<SnapshotShotState>(template.Blades) : [];
            Needles = seedFromTemplateCollections ? new List<SnapshotShotState>(template.Needles) : [];
            RevolverShots = seedFromTemplateCollections ? new List<SnapshotShotState>(template.RevolverShots) : [];
            Rockets = seedFromTemplateCollections ? new List<SnapshotRocketState>(template.Rockets) : [];
            Flames = seedFromTemplateCollections ? new List<SnapshotFlameState>(template.Flames) : [];
            Flares = seedFromTemplateCollections ? new List<SnapshotShotState>(template.Flares) : [];
            Mines = seedFromTemplateCollections ? new List<SnapshotMineState>(template.Mines) : [];
            SentryGibs = seedFromTemplateCollections ? new List<SnapshotSentryGibState>(template.SentryGibs) : [];
            JumpPads = seedFromTemplateCollections ? new List<SnapshotJumpPadState>(template.JumpPads) : [];
            PlayerGibs = seedFromTemplateCollections ? new List<SnapshotPlayerGibState>(template.PlayerGibs) : [];
            DeadBodies = seedFromTemplateCollections ? new List<SnapshotDeadBodyState>(template.DeadBodies) : [];
            RemovedPlayerIds = new List<int>(template.RemovedPlayerIds);
            RemovedSentryIds = new List<int>(template.RemovedSentryIds);
            RemovedShotIds = new List<int>(template.RemovedShotIds);
            RemovedBubbleIds = new List<int>(template.RemovedBubbleIds);
            RemovedBladeIds = new List<int>(template.RemovedBladeIds);
            RemovedNeedleIds = new List<int>(template.RemovedNeedleIds);
            RemovedRevolverShotIds = new List<int>(template.RemovedRevolverShotIds);
            RemovedRocketIds = new List<int>(template.RemovedRocketIds);
            RemovedFlameIds = new List<int>(template.RemovedFlameIds);
            RemovedFlareIds = new List<int>(template.RemovedFlareIds);
            RemovedMineIds = new List<int>(template.RemovedMineIds);
            RemovedSentryGibIds = new List<int>(template.RemovedSentryGibIds);
            RemovedJumpPadIds = new List<int>(template.RemovedJumpPadIds);
            RemovedPlayerGibIds = new List<int>(template.RemovedPlayerGibIds);
            RemovedDeadBodyIds = new List<int>(template.RemovedDeadBodyIds);
        }

        private Builder(Builder other)
        {
            _template = other._template;
            BaselineFrame = other.BaselineFrame;
            CombatTraces = new List<SnapshotCombatTraceState>(other.CombatTraces);
            KillFeed = new List<SnapshotKillFeedEntry>(other.KillFeed);
            VisualEvents = new List<SnapshotVisualEvent>(other.VisualEvents);
            DamageEvents = new List<SnapshotDamageEvent>(other.DamageEvents);
            SoundEvents = new List<SnapshotSoundEvent>(other.SoundEvents);
            GibSpawnEvents = new List<SnapshotGibSpawnEvent>(other.GibSpawnEvents);
            Players = new List<SnapshotPlayerState>(other.Players);
            PlayerMovementStates = new List<SnapshotPlayerMovementState>(other.PlayerMovementStates);
            Sentries = new List<SnapshotSentryState>(other.Sentries);
            Shots = new List<SnapshotShotState>(other.Shots);
            Bubbles = new List<SnapshotShotState>(other.Bubbles);
            Blades = new List<SnapshotShotState>(other.Blades);
            Needles = new List<SnapshotShotState>(other.Needles);
            RevolverShots = new List<SnapshotShotState>(other.RevolverShots);
            Rockets = new List<SnapshotRocketState>(other.Rockets);
            Flames = new List<SnapshotFlameState>(other.Flames);
            Flares = new List<SnapshotShotState>(other.Flares);
            Mines = new List<SnapshotMineState>(other.Mines);
            SentryGibs = new List<SnapshotSentryGibState>(other.SentryGibs);
            PlayerGibs = new List<SnapshotPlayerGibState>(other.PlayerGibs);
            DeadBodies = new List<SnapshotDeadBodyState>(other.DeadBodies);
            RemovedPlayerIds = new List<int>(other.RemovedPlayerIds);
            RemovedSentryIds = new List<int>(other.RemovedSentryIds);
            RemovedShotIds = new List<int>(other.RemovedShotIds);
            RemovedBubbleIds = new List<int>(other.RemovedBubbleIds);
            RemovedBladeIds = new List<int>(other.RemovedBladeIds);
            RemovedNeedleIds = new List<int>(other.RemovedNeedleIds);
            RemovedRevolverShotIds = new List<int>(other.RemovedRevolverShotIds);
            RemovedRocketIds = new List<int>(other.RemovedRocketIds);
            RemovedFlameIds = new List<int>(other.RemovedFlameIds);
            RemovedFlareIds = new List<int>(other.RemovedFlareIds);
            RemovedMineIds = new List<int>(other.RemovedMineIds);
            RemovedSentryGibIds = new List<int>(other.RemovedSentryGibIds);
            JumpPads = new List<SnapshotJumpPadState>(other.JumpPads);
            RemovedJumpPadIds = new List<int>(other.RemovedJumpPadIds);
            RemovedPlayerGibIds = new List<int>(other.RemovedPlayerGibIds);
            RemovedDeadBodyIds = new List<int>(other.RemovedDeadBodyIds);
        }

        public ulong BaselineFrame { get; }
        public List<SnapshotCombatTraceState> CombatTraces { get; }
        public List<SnapshotKillFeedEntry> KillFeed { get; }
        public List<SnapshotVisualEvent> VisualEvents { get; }
        public List<SnapshotDamageEvent> DamageEvents { get; }
        public List<SnapshotSoundEvent> SoundEvents { get; }
        public List<SnapshotGibSpawnEvent> GibSpawnEvents { get; }
        public List<SnapshotPlayerState> Players { get; }
        public List<SnapshotPlayerMovementState> PlayerMovementStates { get; }
        public List<SnapshotSentryState> Sentries { get; } = new();
        public List<SnapshotShotState> Shots { get; } = new();
        public List<SnapshotShotState> Bubbles { get; } = new();
        public List<SnapshotShotState> Blades { get; } = new();
        public List<SnapshotShotState> Needles { get; } = new();
        public List<SnapshotShotState> RevolverShots { get; } = new();
        public List<SnapshotRocketState> Rockets { get; } = new();
        public List<SnapshotFlameState> Flames { get; } = new();
        public List<SnapshotShotState> Flares { get; } = new();
        public List<SnapshotMineState> Mines { get; } = new();
        public List<SnapshotSentryGibState> SentryGibs { get; } = new();
        public List<SnapshotJumpPadState> JumpPads { get; } = new();
        public List<SnapshotPlayerGibState> PlayerGibs { get; } = new();
        public List<SnapshotDeadBodyState> DeadBodies { get; } = new();
        public List<int> RemovedPlayerIds { get; } = new();
        public List<int> RemovedSentryIds { get; } = new();
        public List<int> RemovedShotIds { get; } = new();
        public List<int> RemovedBubbleIds { get; } = new();
        public List<int> RemovedBladeIds { get; } = new();
        public List<int> RemovedNeedleIds { get; } = new();
        public List<int> RemovedRevolverShotIds { get; } = new();
        public List<int> RemovedRocketIds { get; } = new();
        public List<int> RemovedFlameIds { get; } = new();
        public List<int> RemovedFlareIds { get; } = new();
        public List<int> RemovedMineIds { get; } = new();
        public List<int> RemovedSentryGibIds { get; } = new();
        public List<int> RemovedJumpPadIds { get; } = new();
        public List<int> RemovedPlayerGibIds { get; } = new();
        public List<int> RemovedDeadBodyIds { get; } = new();

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
                Players = Players.ToArray(),
                PlayerMovementStates = PlayerMovementStates.ToArray(),
                CombatTraces = CombatTraces.ToArray(),
                Sentries = Sentries.ToArray(),
                Shots = Shots.ToArray(),
                Bubbles = Bubbles.ToArray(),
                Blades = Blades.ToArray(),
                Needles = Needles.ToArray(),
                RevolverShots = RevolverShots.ToArray(),
                Rockets = Rockets.ToArray(),
                Flames = Flames.ToArray(),
                Flares = Flares.ToArray(),
                Mines = Mines.ToArray(),
                SentryGibs = SentryGibs.ToArray(),
                JumpPads = JumpPads.ToArray(),
                PlayerGibs = PlayerGibs.ToArray(),
                DeadBodies = DeadBodies.ToArray(),
                KillFeed = KillFeed.ToArray(),
                VisualEvents = VisualEvents.ToArray(),
                DamageEvents = DamageEvents.ToArray(),
                SoundEvents = SoundEvents.ToArray(),
                GibSpawnEvents = GibSpawnEvents.ToArray(),
                RemovedPlayerIds = RemovedPlayerIds.ToArray(),
                RemovedSentryIds = RemovedSentryIds.ToArray(),
                RemovedShotIds = RemovedShotIds.ToArray(),
                RemovedBubbleIds = RemovedBubbleIds.ToArray(),
                RemovedBladeIds = RemovedBladeIds.ToArray(),
                RemovedNeedleIds = RemovedNeedleIds.ToArray(),
                RemovedRevolverShotIds = RemovedRevolverShotIds.ToArray(),
                RemovedRocketIds = RemovedRocketIds.ToArray(),
                RemovedFlameIds = RemovedFlameIds.ToArray(),
                RemovedFlareIds = RemovedFlareIds.ToArray(),
                RemovedMineIds = RemovedMineIds.ToArray(),
                RemovedSentryGibIds = RemovedSentryGibIds.ToArray(),
                RemovedJumpPadIds = RemovedJumpPadIds.ToArray(),
                RemovedPlayerGibIds = RemovedPlayerGibIds.ToArray(),
                RemovedDeadBodyIds = RemovedDeadBodyIds.ToArray(),
            };
        }
    }
}
