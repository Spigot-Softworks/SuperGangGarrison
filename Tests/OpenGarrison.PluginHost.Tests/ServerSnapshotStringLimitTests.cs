using System.Text;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ServerSnapshotStringLimitTests
{
    [Fact]
    public void ToSnapshotKillFeedEntryClampsObjectiveNameListToProtocolLimit()
    {
        var capperNames = string.Join(", ", Enumerable.Repeat("LongObjectivePlayer", 6));
        Assert.True(Encoding.UTF8.GetByteCount(capperNames) > ProtocolCodec.MaxPlayerNameBytes);

        var entry = new KillFeedEntry(
            capperNames,
            PlayerTeam.Blue,
            "BlueCaptureS",
            string.Empty,
            PlayerTeam.Blue,
            "captured the point!",
            EventId: 42);

        var snapshotEntry = global::ServerHelpers.ToSnapshotKillFeedEntry(entry);

        Assert.True(Encoding.UTF8.GetByteCount(snapshotEntry.KillerName) <= ProtocolCodec.MaxPlayerNameBytes);
        var payload = ProtocolCodec.Serialize(
            CreateSnapshot(LocalDeathCam: null, KillFeed: [snapshotEntry]),
            ProtocolCompressionSettings.Disabled);

        Assert.True(ProtocolCodec.TryDeserialize(payload, out var roundTripped));
        var snapshot = Assert.IsType<SnapshotMessage>(roundTripped);
        Assert.Equal(snapshotEntry.KillerName, Assert.Single(snapshot.KillFeed).KillerName);
    }

    [Fact]
    public void ToSnapshotDeathCamStateClampsNamesAndMessagesToProtocolLimits()
    {
        var longMessage = string.Concat(Enumerable.Repeat("fatal objective combat message ", 12));
        var longName = string.Concat(Enumerable.Repeat("KillerName", 12));

        var deathCam = new LocalDeathCamState(
            FocusX: 1f,
            FocusY: 2f,
            KillMessage: longMessage,
            KillerName: longName,
            KillerTeam: PlayerTeam.Red,
            Health: 50,
            MaxHealth: 100,
            RemainingTicks: 30);

        var snapshotDeathCam = global::ServerHelpers.ToSnapshotDeathCamState(deathCam);
        Assert.NotNull(snapshotDeathCam);
        Assert.True(Encoding.UTF8.GetByteCount(snapshotDeathCam.KillMessage) <= ProtocolCodec.MaxKillMessageBytes);
        Assert.True(Encoding.UTF8.GetByteCount(snapshotDeathCam.KillerName) <= ProtocolCodec.MaxPlayerNameBytes);

        var payload = ProtocolCodec.Serialize(
            CreateSnapshot(snapshotDeathCam, KillFeed: []),
            ProtocolCompressionSettings.Disabled);

        Assert.True(ProtocolCodec.TryDeserialize(payload, out var roundTripped));
        var snapshot = Assert.IsType<SnapshotMessage>(roundTripped);
        Assert.Equal(snapshotDeathCam.KillerName, snapshot.LocalDeathCam?.KillerName);
    }

    private static SnapshotMessage CreateSnapshot(
        SnapshotDeathCamState? LocalDeathCam,
        IReadOnlyList<SnapshotKillFeedEntry> KillFeed)
    {
        return new SnapshotMessage(
            Frame: 1,
            TickRate: 60,
            LevelName: "ctf_test",
            MapAreaIndex: 0,
            MapAreaCount: 1,
            GameMode: 1,
            MatchPhase: 1,
            WinnerTeam: 0,
            TimeRemainingTicks: 600,
            RedCaps: 0,
            BlueCaps: 0,
            SpectatorCount: 0,
            LastProcessedInputSequence: 0,
            RedIntel: new SnapshotIntelState(1, 0f, 0f, true, false, 0),
            BlueIntel: new SnapshotIntelState(2, 0f, 0f, true, false, 0),
            Players: [],
            CombatTraces: [],
            SniperAimIndicators: [],
            Sentries: [],
            Shots: [],
            Bubbles: [],
            Blades: [],
            Needles: [],
            RevolverShots: [],
            Rockets: [],
            Flames: [],
            Flares: [],
            Mines: [],
            DeadBodies: [],
            ControlPointSetupTicksRemaining: 0,
            KothUnlockTicksRemaining: 0,
            KothRedTimerTicksRemaining: 0,
            KothBlueTimerTicksRemaining: 0,
            ControlPoints: [],
            Generators: [],
            LocalDeathCam: LocalDeathCam,
            KillFeed: KillFeed,
            VisualEvents: [],
            DamageEvents: [],
            SoundEvents: []);
    }
}
