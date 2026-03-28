using System;
using System.Collections.Generic;
using System.Linq;
using OpenGarrison.Protocol;

namespace OpenGarrison.Server;

internal static class SnapshotDeltaBudgeter
{
    public const int TargetSnapshotPayloadBytes = 1200;

    internal sealed record Contribution(int Priority, float DistanceSquared, Action<Builder> Apply);

    public static (SnapshotMessage Message, byte[] Payload) BuildBudgetedSnapshot(
        SnapshotMessage fullSnapshot,
        SnapshotMessage? baseline,
        IReadOnlyList<Contribution> contributions)
    {
        var builder = new Builder(fullSnapshot, baseline?.Frame ?? 0);
        var snapshot = builder.Build();
        var payload = ProtocolCodec.Serialize(snapshot);
        var payloadSize = payload.Length;

        if (payloadSize > TargetSnapshotPayloadBytes)
        {
            TrimAuxiliaryCollections(builder);
            snapshot = builder.Build();
            payload = ProtocolCodec.Serialize(snapshot);
            payloadSize = payload.Length;
            if (payloadSize > TargetSnapshotPayloadBytes)
            {
                return (snapshot, payload);
            }
        }

        foreach (var contribution in contributions.OrderByDescending(static entry => entry.Priority).ThenBy(static entry => entry.DistanceSquared))
        {
            var trialBuilder = builder.Clone();
            contribution.Apply(trialBuilder);
            var trialSnapshot = trialBuilder.Build();
            var trialPayload = ProtocolCodec.Serialize(trialSnapshot);
            if (trialPayload.Length > TargetSnapshotPayloadBytes)
            {
                continue;
            }

            builder = trialBuilder;
            snapshot = trialSnapshot;
            payload = trialPayload;
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

    internal sealed class Builder
    {
        private readonly SnapshotMessage _template;

        public Builder(SnapshotMessage template, ulong baselineFrame)
        {
            _template = template;
            BaselineFrame = baselineFrame;
            CombatTraces = new List<SnapshotCombatTraceState>(template.CombatTraces);
            KillFeed = new List<SnapshotKillFeedEntry>(template.KillFeed);
            VisualEvents = new List<SnapshotVisualEvent>(template.VisualEvents);
            DamageEvents = new List<SnapshotDamageEvent>(template.DamageEvents);
            SoundEvents = new List<SnapshotSoundEvent>(template.SoundEvents);
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
