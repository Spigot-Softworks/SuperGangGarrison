using System;
using System.Collections.Generic;
using System.Linq;
using OpenGarrison.Protocol;

namespace OpenGarrison.Server;

internal static class SnapshotDeltaBudgeter
{
    public const int TargetSnapshotPayloadBytes = 1200;

    internal sealed record Contribution(int Priority, float DistanceSquared, int EstimatedBytes, Action<Builder> Apply);

    public static (SnapshotMessage Message, byte[] Payload) BuildBudgetedSnapshot(
        SnapshotMessage fullSnapshot,
        SnapshotMessage? baseline,
        IReadOnlyList<Contribution> contributions)
    {
        var builder = new Builder(fullSnapshot, baseline?.Frame ?? 0, seedFromTemplateCollections: baseline is null);
        var snapshot = builder.Build();
        var payload = ProtocolCodec.Serialize(snapshot);
        var payloadSize = payload.Length;

        if (payloadSize > TargetSnapshotPayloadBytes)
        {
            TrimAuxiliaryCollections(builder);
            snapshot = builder.Build();
            payload = ProtocolCodec.Serialize(snapshot);
            payloadSize = payload.Length;
        }

        var remainingBudget = TargetSnapshotPayloadBytes - payloadSize;
        foreach (var contribution in contributions.OrderByDescending(static entry => entry.Priority).ThenBy(static entry => entry.DistanceSquared))
        {
            if (contribution.EstimatedBytes > remainingBudget)
            {
                continue;
            }

            contribution.Apply(builder);
            remainingBudget -= contribution.EstimatedBytes;
        }

        snapshot = builder.Build();
        payload = ProtocolCodec.Serialize(snapshot);

        if (payload.Length > TargetSnapshotPayloadBytes)
        {
            ReduceToBudget(builder);
            snapshot = builder.Build();
            payload = ProtocolCodec.Serialize(snapshot);
            if (payload.Length > TargetSnapshotPayloadBytes)
            {
                snapshot = ReduceSnapshotForBudget(snapshot);
                payload = ProtocolCodec.Serialize(snapshot);
            }
            if (payload.Length > TargetSnapshotPayloadBytes)
            {
                snapshot = ReduceSnapshotToAbsoluteMinimum(snapshot);
                payload = ProtocolCodec.Serialize(snapshot);
            }
        }

        return (snapshot, payload);
    }

    private static void TrimAuxiliaryCollections(Builder builder)
    {
        builder.KillFeed.Clear();
        builder.CombatTraces.Clear();
        builder.VisualEvents.Clear();
        builder.DamageEvents.Clear();
        builder.SoundEvents.Clear();
    }

    private static void ReduceToBudget(Builder builder)
    {
        foreach (var dropStep in BudgetDropSteps)
        {
            dropStep(builder);

            var payload = ProtocolCodec.Serialize(builder.Build());
            if (payload.Length <= TargetSnapshotPayloadBytes)
            {
                return;
            }
        }
    }

    private static SnapshotMessage ReduceSnapshotForBudget(SnapshotMessage snapshot)
    {
        snapshot = snapshot with
        {
            Players = snapshot.Players
                .Select(ReducePlayerStateForBudget)
                .ToArray(),
            LocalDeathCam = null,
        };

        var payload = ProtocolCodec.Serialize(snapshot);
        if (payload.Length <= TargetSnapshotPayloadBytes)
        {
            return snapshot;
        }

        snapshot = snapshot with
        {
            Players = snapshot.Players
                .Select(ReducePlayerStateAggressivelyForBudget)
                .ToArray(),
        };

        payload = ProtocolCodec.Serialize(snapshot);
        if (payload.Length <= TargetSnapshotPayloadBytes)
        {
            return snapshot;
        }

        return snapshot with
        {
            Players = Array.Empty<SnapshotPlayerState>(),
        };
    }

    private static SnapshotPlayerState ReducePlayerStateForBudget(SnapshotPlayerState player)
    {
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
            ReplicatedStates = Array.Empty<SnapshotReplicatedStateEntry>(),
            IsChatBubbleVisible = false,
            ChatBubbleFrameIndex = 0,
            ChatBubbleAlpha = 0f,
            IsTaunting = false,
            TauntFrameIndex = 0f,
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
            IsTaunting = false,
            TauntFrameIndex = 0f,
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
            IsSpyCloaked = false,
            SpyCloakAlpha = 1f,
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
            BloodDrops = Array.Empty<SnapshotBloodDropState>(),
            DeadBodies = Array.Empty<SnapshotDeadBodyState>(),
            ControlPoints = Array.Empty<SnapshotControlPointState>(),
            Generators = Array.Empty<SnapshotGeneratorState>(),
            LocalDeathCam = null,
            KillFeed = Array.Empty<SnapshotKillFeedEntry>(),
            VisualEvents = Array.Empty<SnapshotVisualEvent>(),
            DamageEvents = Array.Empty<SnapshotDamageEvent>(),
            SoundEvents = Array.Empty<SnapshotSoundEvent>(),
            SentryGibs = Array.Empty<SnapshotSentryGibState>(),
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
            RemovedBloodDropIds = Array.Empty<int>(),
            RemovedDeadBodyIds = Array.Empty<int>(),
            RemovedSentryGibIds = Array.Empty<int>(),
        };
    }

    private static readonly Action<Builder>[] BudgetDropSteps =
    [
        static builder =>
        {
            builder.SoundEvents.Clear();
            builder.DamageEvents.Clear();
            builder.VisualEvents.Clear();
            builder.KillFeed.Clear();
        },
        static builder => builder.CombatTraces.Clear(),
        static builder =>
        {
            builder.BloodDrops.Clear();
            builder.PlayerGibs.Clear();
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
        },
        static builder =>
        {
            builder.Mines.Clear();
            builder.Flames.Clear();
            builder.Rockets.Clear();
            builder.Sentries.Clear();
        },
        static builder =>
        {
            builder.RemovedBloodDropIds.Clear();
            builder.RemovedPlayerGibIds.Clear();
            builder.RemovedSentryGibIds.Clear();
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
            PlayerGibs = seedFromTemplateCollections ? new List<SnapshotPlayerGibState>(template.PlayerGibs) : [];
            BloodDrops = seedFromTemplateCollections ? new List<SnapshotBloodDropState>(template.BloodDrops) : [];
            DeadBodies = seedFromTemplateCollections ? new List<SnapshotDeadBodyState>(template.DeadBodies) : [];
            RemovedSentryIds = seedFromTemplateCollections ? new List<int>(template.RemovedSentryIds) : [];
            RemovedShotIds = seedFromTemplateCollections ? new List<int>(template.RemovedShotIds) : [];
            RemovedBubbleIds = seedFromTemplateCollections ? new List<int>(template.RemovedBubbleIds) : [];
            RemovedBladeIds = seedFromTemplateCollections ? new List<int>(template.RemovedBladeIds) : [];
            RemovedNeedleIds = seedFromTemplateCollections ? new List<int>(template.RemovedNeedleIds) : [];
            RemovedRevolverShotIds = seedFromTemplateCollections ? new List<int>(template.RemovedRevolverShotIds) : [];
            RemovedRocketIds = seedFromTemplateCollections ? new List<int>(template.RemovedRocketIds) : [];
            RemovedFlameIds = seedFromTemplateCollections ? new List<int>(template.RemovedFlameIds) : [];
            RemovedFlareIds = seedFromTemplateCollections ? new List<int>(template.RemovedFlareIds) : [];
            RemovedMineIds = seedFromTemplateCollections ? new List<int>(template.RemovedMineIds) : [];
            RemovedSentryGibIds = seedFromTemplateCollections ? new List<int>(template.RemovedSentryGibIds) : [];
            RemovedPlayerGibIds = seedFromTemplateCollections ? new List<int>(template.RemovedPlayerGibIds) : [];
            RemovedBloodDropIds = seedFromTemplateCollections ? new List<int>(template.RemovedBloodDropIds) : [];
            RemovedDeadBodyIds = seedFromTemplateCollections ? new List<int>(template.RemovedDeadBodyIds) : [];
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
            BloodDrops = new List<SnapshotBloodDropState>(other.BloodDrops);
            DeadBodies = new List<SnapshotDeadBodyState>(other.DeadBodies);
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
            RemovedPlayerGibIds = new List<int>(other.RemovedPlayerGibIds);
            RemovedBloodDropIds = new List<int>(other.RemovedBloodDropIds);
            RemovedDeadBodyIds = new List<int>(other.RemovedDeadBodyIds);
        }

        public ulong BaselineFrame { get; }
        public List<SnapshotCombatTraceState> CombatTraces { get; }
        public List<SnapshotKillFeedEntry> KillFeed { get; }
        public List<SnapshotVisualEvent> VisualEvents { get; }
        public List<SnapshotDamageEvent> DamageEvents { get; }
        public List<SnapshotSoundEvent> SoundEvents { get; }
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
        public List<SnapshotPlayerGibState> PlayerGibs { get; } = new();
        public List<SnapshotBloodDropState> BloodDrops { get; } = new();
        public List<SnapshotDeadBodyState> DeadBodies { get; } = new();
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
        public List<int> RemovedPlayerGibIds { get; } = new();
        public List<int> RemovedBloodDropIds { get; } = new();
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
                PlayerGibs = PlayerGibs.ToArray(),
                BloodDrops = BloodDrops.ToArray(),
                DeadBodies = DeadBodies.ToArray(),
                KillFeed = KillFeed.ToArray(),
                VisualEvents = VisualEvents.ToArray(),
                DamageEvents = DamageEvents.ToArray(),
                SoundEvents = SoundEvents.ToArray(),
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
                RemovedPlayerGibIds = RemovedPlayerGibIds.ToArray(),
                RemovedBloodDropIds = RemovedBloodDropIds.ToArray(),
                RemovedDeadBodyIds = RemovedDeadBodyIds.ToArray(),
            };
        }
    }
}
