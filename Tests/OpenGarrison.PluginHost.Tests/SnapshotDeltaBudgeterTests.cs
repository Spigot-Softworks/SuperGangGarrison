using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SnapshotDeltaBudgeterTests
{
    [Fact]
    public void BuildBudgetedSnapshotKeepsPayloadUnderTargetAndPreservesHighPriorityContributions()
    {
        var baseline = CreateSnapshot(100);
        var current = CreateSnapshot(101);
        var contributions = new List<SnapshotDeltaBudgeter.Contribution>
        {
            new(
                Priority: 2000,
                DistanceSquared: 0f,
                EstimatedBytes: 88,
                Apply: static builder => builder.Rockets.Add(new SnapshotRocketState(
                    Id: 1,
                    Team: 1,
                    OwnerId: 7,
                    X: 128f,
                    Y: 64f,
                    PreviousX: 120f,
                    PreviousY: 60f,
                    DirectionRadians: 0.5f,
                    Speed: 240f,
                    TicksRemaining: 40))),
        };

        for (var index = 0; index < 120; index += 1)
        {
            var soundIndex = index;
            contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                Priority: 1000 - index,
                DistanceSquared: index,
                EstimatedBytes: 48,
                Apply: builder => builder.SoundEvents.Add(new SnapshotSoundEvent(
                    $"low-priority-sound-event-{soundIndex:D3}-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    X: soundIndex,
                    Y: soundIndex,
                    EventId: (ulong)soundIndex,
                    SourceFrame: 101))));
        }

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, contributions);
        var merged = SnapshotDelta.ToFullSnapshot(result.Message, baseline);

        Assert.True(result.Payload.Length <= SnapshotDeltaBudgeter.TargetSnapshotPayloadBytes);
        Assert.True(result.Message.IsDelta);
        Assert.Equal(baseline.Frame, result.Message.BaselineFrame);
        Assert.Single(result.Message.Rockets);
        Assert.True(result.Message.SoundEvents.Count < 120);
        Assert.Single(merged.Rockets);
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
            PlayerGibs: Array.Empty<SnapshotPlayerGibState>(),
            BloodDrops: Array.Empty<SnapshotBloodDropState>(),
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
            MapContentHash: string.Empty)
        {
            SentryGibs = Array.Empty<SnapshotSentryGibState>(),
        };
    }
}
