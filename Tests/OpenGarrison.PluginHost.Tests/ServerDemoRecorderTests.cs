using System;
using System.IO;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ServerDemoRecorderTests
{
    [Fact]
    public void ServerDemoRecorderWritesPlayableSegment()
    {
        var root = CreateTempRoot();
        var requestedPath = Path.Combine(root, "session.ogdemo");
        const string levelName = "ClassicWell";
        var currentFrame = 1UL;
        var recorder = new ServerDemoRecorder(
            () => BuildWelcomeMessage(levelName),
            () => BuildSnapshot(levelName, currentFrame),
            () => "Test Server",
            () => levelName,
            static _ => { });

        Assert.True(recorder.TryStart(requestedPath, out var startStatus, out var startError), startError);
        Assert.Contains("demo recording started:", startStatus, StringComparison.Ordinal);

        recorder.RecordBroadcastMessage(new ChatRelayMessage(0, "[server]", "hello"));
        currentFrame = 2;
        recorder.RecordSnapshot(BuildSnapshot(levelName, currentFrame));

        Assert.True(recorder.TryStop(out var stopStatus, out var stopError), stopError);
        Assert.Contains("demo recording stopped:", stopStatus, StringComparison.Ordinal);

        var segmentPath = Path.Combine(root, "session [01] ClassicWell.ogdemo");
        Assert.True(File.Exists(segmentPath));

        var demo = OpenGarrisonDemoFile.Read(segmentPath);
        Assert.Equal(4, demo.Messages.Count);
        Assert.Equal(0, demo.Messages[0].DueMilliseconds);
        Assert.Equal(0, demo.Messages[1].DueMilliseconds);
        Assert.Equal(0, demo.Messages[2].DueMilliseconds);
        Assert.True(demo.Messages[3].DueMilliseconds >= 33);

        Assert.True(OpenGarrisonDemoTransport.TryVerifyPlaybackSummary(segmentPath, out var summary, out var error), error);
        Assert.Contains("verifiedMessages=4", summary, StringComparison.Ordinal);
        Assert.Contains("appliedSnapshots=2", summary, StringComparison.Ordinal);
        Assert.Contains("level=ClassicWell", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ServerDemoRecorderRollsOverAcrossMapChanges()
    {
        var root = CreateTempRoot();
        var requestedPath = Path.Combine(root, "session.ogdemo");
        var currentLevelName = "ClassicWell";
        var currentFrame = 1UL;
        var recorder = new ServerDemoRecorder(
            () => BuildWelcomeMessage(currentLevelName),
            () => BuildSnapshot(currentLevelName, currentFrame),
            () => "Test Server",
            () => currentLevelName,
            static _ => { });

        Assert.True(recorder.TryStart(requestedPath, out _, out var startError), startError);

        currentFrame = 2;
        recorder.RecordSnapshot(BuildSnapshot(currentLevelName, currentFrame));

        currentLevelName = "Truefort";
        currentFrame = 10;
        recorder.HandleMapTransition(new MapChangeTransition("ClassicWell", 1, 1, "Truefort", 1, false, null));

        currentFrame = 11;
        recorder.RecordSnapshot(BuildSnapshot(currentLevelName, currentFrame));

        Assert.True(recorder.TryStop(out _, out var stopError), stopError);

        var firstSegmentPath = Path.Combine(root, "session [01] ClassicWell.ogdemo");
        var secondSegmentPath = Path.Combine(root, "session [02] Truefort.ogdemo");
        Assert.True(File.Exists(firstSegmentPath));
        Assert.True(File.Exists(secondSegmentPath));

        Assert.True(OpenGarrisonDemoTransport.TryVerifyPlaybackSummary(firstSegmentPath, out var firstSummary, out var firstError), firstError);
        Assert.Contains("level=ClassicWell", firstSummary, StringComparison.Ordinal);
        Assert.Contains("appliedSnapshots=2", firstSummary, StringComparison.Ordinal);

        Assert.True(OpenGarrisonDemoTransport.TryVerifyPlaybackSummary(secondSegmentPath, out var secondSummary, out var secondError), secondError);
        Assert.Contains("level=Truefort", secondSummary, StringComparison.Ordinal);
        Assert.Contains("appliedSnapshots=2", secondSummary, StringComparison.Ordinal);
    }

    private static WelcomeMessage BuildWelcomeMessage(string levelName)
    {
        return new WelcomeMessage("Demo Server", ProtocolVersion.Current, 30, levelName, 128, 16);
    }

    private static SnapshotMessage BuildSnapshot(string levelName, ulong frame)
    {
        return new SnapshotMessage(
            Frame: frame,
            TickRate: 30,
            LevelName: levelName,
            MapAreaIndex: 1,
            MapAreaCount: 1,
            GameMode: (byte)GameModeKind.CaptureTheFlag,
            MatchPhase: (byte)MatchPhase.Running,
            WinnerTeam: 0,
            TimeRemainingTicks: 18000,
            RedCaps: 0,
            BlueCaps: 0,
            SpectatorCount: 0,
            LastProcessedInputSequence: 0,
            RedIntel: new SnapshotIntelState((byte)PlayerTeam.Red, 0f, 0f, true, false, 0),
            BlueIntel: new SnapshotIntelState((byte)PlayerTeam.Blue, 0f, 0f, true, false, 0),
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
            SoundEvents: Array.Empty<SnapshotSoundEvent>())
        {
            TimeLimitTicks = 18000,
            PlayerGibs = Array.Empty<SnapshotPlayerGibState>(),
        };
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "OpenGarrison.PluginHost.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
