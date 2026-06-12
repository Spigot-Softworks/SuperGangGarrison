using System;
using System.Collections.Generic;
using System.Net;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SnapshotDeltaBudgeterRocketRemovalTests
{
    [Fact]
    public void BuildBudgetedSnapshotKeepsExplosionSoundBeforeRocketRemovalWhenReducingOverage()
    {
        var rocket = CreateRocketState(42);
        var baseline = CreateSnapshot(900) with
        {
            Rockets = [rocket],
        };
        var current = CreateSnapshot(901);
        var soundEvent = new SnapshotSoundEvent(
            "ExplosionSnd-91C5D7A842F9466F9A0C63F83E25452B",
            128f,
            96f,
            EventId: 1,
            SourceFrame: 901);
        var removalOnlyDelta = current with
        {
            IsDelta = true,
            BaselineFrame = baseline.Frame,
            RemovedRocketIds = [rocket.Id],
        };
        var soundOnlyDelta = current with
        {
            IsDelta = true,
            BaselineFrame = baseline.Frame,
            SoundEvents = [soundEvent],
        };
        var bothDelta = current with
        {
            IsDelta = true,
            BaselineFrame = baseline.Frame,
            RemovedRocketIds = [rocket.Id],
            SoundEvents = [soundEvent],
        };
        var targetPayloadBytes = Math.Max(MeasurePayloadLength(removalOnlyDelta), MeasurePayloadLength(soundOnlyDelta));
        Assert.True(MeasurePayloadLength(bothDelta) > targetPayloadBytes);

        var contributions = new List<SnapshotDeltaBudgeter.Contribution>
        {
            new(
                Priority: 1200,
                DistanceSquared: 0f,
                EstimatedBytes: 1,
                Apply: builder => builder.RemovedRocketIds.Add(rocket.Id),
                Kind: SnapshotDeltaBudgeter.ContributionKind.EntityRemoval),
            new(
                Priority: 850,
                DistanceSquared: 0f,
                EstimatedBytes: 1,
                Apply: builder => builder.SoundEvents.Add(soundEvent),
                Kind: SnapshotDeltaBudgeter.ContributionKind.TransientSoundEvent),
        };

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, contributions, targetPayloadBytes);
        var merged = SnapshotDelta.ToFullSnapshot(result.Message, baseline);

        Assert.True(result.Payload.Length <= targetPayloadBytes);
        Assert.Empty(result.Message.RemovedRocketIds);
        Assert.Single(result.Message.SoundEvents);
        Assert.Single(merged.Rockets);
    }

    [Fact]
    public void BuildUntrimmedSnapshotKeepsRocketRemovalAndExplosionSoundWhenOverTarget()
    {
        var rocket = CreateRocketState(42);
        var baseline = CreateSnapshot(900) with
        {
            Rockets = [rocket],
        };
        var current = CreateSnapshot(901);
        var soundEvent = new SnapshotSoundEvent(
            "ExplosionSnd-91C5D7A842F9466F9A0C63F83E25452B",
            128f,
            96f,
            EventId: 1,
            SourceFrame: 901);
        var removalOnlyDelta = current with
        {
            IsDelta = true,
            BaselineFrame = baseline.Frame,
            RemovedRocketIds = [rocket.Id],
        };
        var soundOnlyDelta = current with
        {
            IsDelta = true,
            BaselineFrame = baseline.Frame,
            SoundEvents = [soundEvent],
        };
        var bothDelta = current with
        {
            IsDelta = true,
            BaselineFrame = baseline.Frame,
            RemovedRocketIds = [rocket.Id],
            SoundEvents = [soundEvent],
        };
        var targetPayloadBytes = Math.Max(MeasurePayloadLength(removalOnlyDelta), MeasurePayloadLength(soundOnlyDelta));
        Assert.True(MeasurePayloadLength(bothDelta) > targetPayloadBytes);

        var contributions = new List<SnapshotDeltaBudgeter.Contribution>
        {
            new(
                Priority: 1200,
                DistanceSquared: 0f,
                EstimatedBytes: 1,
                Apply: builder => builder.RemovedRocketIds.Add(rocket.Id),
                Kind: SnapshotDeltaBudgeter.ContributionKind.EntityRemoval),
            new(
                Priority: 850,
                DistanceSquared: 0f,
                EstimatedBytes: 1,
                Apply: builder => builder.SoundEvents.Add(soundEvent),
                Kind: SnapshotDeltaBudgeter.ContributionKind.TransientSoundEvent),
        };

        var result = SnapshotDeltaBudgeter.BuildUntrimmedSnapshotWithEmergencyReduction(
            current,
            baseline,
            contributions,
            targetPayloadBytes);
        var merged = SnapshotDelta.ToFullSnapshot(result.Message, baseline);

        Assert.True(result.ReductionApplied);
        Assert.True(result.Payload.Length > targetPayloadBytes);
        Assert.True(result.Composition.PayloadOverTarget);
        Assert.Equal(rocket.Id, Assert.Single(result.Message.RemovedRocketIds));
        Assert.Single(result.Message.SoundEvents);
        Assert.Empty(merged.Rockets);
    }

    [Fact]
    public void BuildBudgetedSnapshotIncludesOnlyNetworkVisibleCombatTraces()
    {
        var baseline = CreateSnapshot(910);
        var ordinaryTrace = new SnapshotCombatTraceState(
            StartX: 64f,
            StartY: 96f,
            EndX: 96f,
            EndY: 96f,
            TicksRemaining: 3,
            HitCharacter: false,
            Team: 1,
            IsSniperTracer: false);
        var sniperTrace = new SnapshotCombatTraceState(
            StartX: 128f,
            StartY: 96f,
            EndX: 512f,
            EndY: 96f,
            TicksRemaining: 3,
            HitCharacter: true,
            Team: 1,
            IsSniperTracer: true);
        var current = CreateSnapshot(911) with
        {
            CombatTraces = [ordinaryTrace, sniperTrace],
        };
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Tester",
            TimeSpan.Zero);
        var contributions = SnapshotContributionPlanner.BuildContributions(
            client,
            current,
            baseline,
            new SimulationWorld());

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(
            current,
            baseline,
            contributions,
            targetPayloadBytes: 64 * 1024);

        var trace = Assert.Single(result.Message.CombatTraces);
        Assert.True(trace.IsSniperTracer);
        Assert.Equal(sniperTrace.EndX, trace.EndX);
    }

    [Fact]
    public void SnapshotBroadcasterCapturesOnlyNetworkVisibleCombatTraces()
    {
        var ordinaryTrace = new CombatTrace(
            StartX: 64f,
            StartY: 96f,
            EndX: 96f,
            EndY: 96f,
            TicksRemaining: 3,
            HitCharacter: false,
            Team: PlayerTeam.Red,
            IsSniperTracer: false);
        var sniperTrace = new CombatTrace(
            StartX: 128f,
            StartY: 96f,
            EndX: 512f,
            EndY: 96f,
            TicksRemaining: 3,
            HitCharacter: true,
            Team: PlayerTeam.Blue,
            IsSniperTracer: true);

        var captured = SnapshotBroadcaster.ConvertNetworkCombatTracesToArray([ordinaryTrace, sniperTrace]);

        var trace = Assert.Single(captured);
        Assert.True(trace.IsSniperTracer);
        Assert.Equal((byte)PlayerTeam.Blue, trace.Team);
        Assert.Equal(sniperTrace.EndX, trace.EndX);
    }

    private static int MeasurePayloadLength(SnapshotMessage snapshot)
    {
        return ProtocolCodec.Serialize(
            snapshot,
            ProtocolCodec.MeasureSerializedSize(snapshot),
            ServerProtocolCompression.Settings).Length;
    }

    private static SnapshotRocketState CreateRocketState(int id)
    {
        return new SnapshotRocketState(
            id,
            Team: 1,
            OwnerId: 7,
            X: 128f,
            Y: 64f,
            PreviousX: 120f,
            PreviousY: 60f,
            DirectionRadians: 0.5f,
            Speed: 240f,
            TicksRemaining: 40);
    }

    private static SnapshotMessage CreateSnapshot(ulong frame)
    {
        return new SnapshotMessage(
            frame,
            TickRate: 60,
            LevelName: "ctf_test",
            MapAreaIndex: 1,
            MapAreaCount: 1,
            GameMode: 1,
            MatchPhase: 1,
            WinnerTeam: 0,
            TimeRemainingTicks: 0,
            RedCaps: 0,
            BlueCaps: 0,
            SpectatorCount: 0,
            LastProcessedInputSequence: 0,
            RedIntel: new SnapshotIntelState(0, 0f, 0f, true, false, 0),
            BlueIntel: new SnapshotIntelState(0, 0f, 0f, true, false, 0),
            Players: Array.Empty<SnapshotPlayerState>(),
            CombatTraces: Array.Empty<SnapshotCombatTraceState>(),
            SniperAimIndicators: Array.Empty<SnapshotSniperAimIndicatorState>(),
            Sentries: Array.Empty<SnapshotSentryState>(),
            Shots: Array.Empty<SnapshotShotState>(),
            Bubbles: Array.Empty<SnapshotShotState>(),
            Blades: Array.Empty<SnapshotShotState>(),
            Needles: Array.Empty<SnapshotShotState>(),
            RevolverShots: Array.Empty<SnapshotShotState>(),
            Rockets: Array.Empty<SnapshotRocketState>(),
            Flames: Array.Empty<SnapshotFlameState>(),
            Flares: Array.Empty<SnapshotShotState>(),
            Mines: Array.Empty<SnapshotMineState>(),
            DeadBodies: Array.Empty<SnapshotDeadBodyState>(),
            ControlPointSetupTicksRemaining: 0,
            KothUnlockTicksRemaining: 0,
            KothRedTimerTicksRemaining: 0,
            KothBlueTimerTicksRemaining: 0,
            ControlPoints: Array.Empty<SnapshotControlPointState>(),
            Generators: Array.Empty<SnapshotGeneratorState>(),
            LocalDeathCam: null,
            KillFeed: Array.Empty<SnapshotKillFeedEntry>(),
            VisualEvents: Array.Empty<SnapshotVisualEvent>(),
            DamageEvents: Array.Empty<SnapshotDamageEvent>(),
            SoundEvents: Array.Empty<SnapshotSoundEvent>(),
            IsCustomMap: false,
            MapDownloadUrl: string.Empty,
            MapContentHash: string.Empty);
    }
}
