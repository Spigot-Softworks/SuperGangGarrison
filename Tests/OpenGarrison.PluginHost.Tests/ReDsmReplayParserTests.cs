using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ReDsmReplayParserTests
{
    [Fact]
    public void ParseReadsHeaderFramesAndEndMarker()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.Latin1, leaveOpen: true))
        {
            WriteRecord(
                writer,
                254,
                1,
                0x10,
                0x72,
                0,
                0x00,
                0x07,
                (byte)'S',
                (byte)'e',
                (byte)'r',
                (byte)'v',
                (byte)'e',
                (byte)'r',
                (byte)'1',
                0x03,
                (byte)'c',
                (byte)'t',
                (byte)'f',
                0x04,
                (byte)'a',
                (byte)'b',
                (byte)'c',
                (byte)'d',
                0x01,
                0x08,
                0x00,
                (byte)'p',
                (byte)'l',
                (byte)'u',
                (byte)'g',
                (byte)'i',
                (byte)'n',
                (byte)'s',
                (byte)'1',
                0x01,
                0x02,
                0x03);
            WriteRecord(writer, 0x1C, 0x04, 0x00, 0x00, 0x00, 0x00);
            WriteRecord(writer, 0x06, 0x04, 0x00, 0x00, 0x00, 0x00);
            WriteRecord(writer, 255);
        }

        stream.Position = 0;
        var replay = ReDsmReplayParser.Parse(stream);

        Assert.Equal((byte)1, replay.Header.ReplayVersion);
        Assert.Equal((ushort)0x7210, replay.Header.GameVersion);
        Assert.Equal((byte)0, replay.Header.FrameRateKind);
        Assert.Equal(30, replay.Header.FramesPerSecond);
        Assert.Equal("Server1", replay.Header.ServerName);
        Assert.Equal("ctf", replay.Header.MapName);
        Assert.Equal("abcd", replay.Header.MapContentHash);
        Assert.True(replay.Header.PluginsRequired);
        Assert.Equal("plugins1", replay.Header.PluginList);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, replay.Header.JoinStatePayload);
        Assert.True(replay.HasEndMarker);
        Assert.Equal(2, replay.Frames.Count);
        Assert.Equal(0, replay.Frames[0].Index);
        Assert.Equal(new byte[] { 0x1C, 0x04, 0x00, 0x00, 0x00, 0x00 }, replay.Frames[0].Payload);
        Assert.Equal((byte)0x06, replay.Frames[1].FirstByte);
    }

    [Fact]
    public void ParseThrowsOnTruncatedRecordPayload()
    {
        using var stream = new MemoryStream(new byte[] { 0x04, 0x00, 0xFE, 0x01 });

        Assert.Throws<InvalidDataException>(() => ReDsmReplayParser.Parse(stream));
    }

    [Fact]
    public void VerifyPlaybackSummaryAcceptsMinimalSupportedStockReplay()
    {
        var replayPath = Path.Combine(Path.GetTempPath(), $"redsm-minimal-{Guid.NewGuid():N}.rply");
        try
        {
            using (var stream = File.Create(replayPath))
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.Latin1, leaveOpen: false))
            {
                var headerPayload = new byte[]
                {
                    254,
                    1,
                    0x10,
                    0x72,
                    0,
                    0x00,
                    0x07,
                    (byte)'S',
                    (byte)'e',
                    (byte)'r',
                    (byte)'v',
                    (byte)'e',
                    (byte)'r',
                    (byte)'1',
                    0x0F,
                    (byte)'c',
                    (byte)'t',
                    (byte)'f',
                    (byte)'_',
                    (byte)'c',
                    (byte)'l',
                    (byte)'a',
                    (byte)'s',
                    (byte)'s',
                    (byte)'i',
                    (byte)'c',
                    (byte)'w',
                    (byte)'e',
                    (byte)'l',
                    (byte)'l',
                    0x00,
                    0x00,
                    0x00,
                    0x00,
                };
                WriteRecord(writer, ConcatBytes(headerPayload, BuildMinimalSupportedJoinStatePayload()));
                WriteRecord(writer, 0x06, 0x00);
                WriteRecord(writer, 255);
            }

            Assert.True(ReDsmReplayTransport.TryVerifyPlaybackSummary(replayPath, out var summary, out var error), error);
            Assert.Contains("verifiedMessages=3", summary, StringComparison.Ordinal);
            Assert.Contains("appliedSnapshots=2", summary, StringComparison.Ordinal);
            Assert.Contains("level=", summary, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(replayPath))
            {
                File.Delete(replayPath);
            }
        }
    }

    [Fact]
    public void VerifyPlaybackSummaryAcceptsMinimalNativeDemo()
    {
        var demoPath = Path.Combine(Path.GetTempPath(), $"og-demo-{Guid.NewGuid():N}.ogdemo");
        try
        {
            var welcome = BuildMinimalWelcomeMessage();
            var snapshot = BuildMinimalSnapshotMessage();

            OpenGarrisonDemoFile.Write(
                demoPath,
                new OpenGarrisonDemoFile(
                    "demo:test",
                    "Demo ended.",
                    new[]
                    {
                        new OpenGarrisonDemoMessage(0, ProtocolCodec.Serialize(welcome)),
                        new OpenGarrisonDemoMessage(0, ProtocolCodec.Serialize(snapshot)),
                    }));

            Assert.True(OpenGarrisonDemoTransport.TryVerifyPlaybackSummary(demoPath, out var summary, out var error), error);
            Assert.Contains("verifiedMessages=2", summary, StringComparison.Ordinal);
            Assert.Contains("appliedSnapshots=1", summary, StringComparison.Ordinal);
            Assert.Contains("level=ClassicWell", summary, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(demoPath))
            {
                File.Delete(demoPath);
            }
        }
    }

    [Fact]
    public void RecordingWriterStreamsPlayableNativeDemo()
    {
        var demoPath = Path.Combine(Path.GetTempPath(), $"og-demo-stream-{Guid.NewGuid():N}.ogdemo");
        try
        {
            using (var writer = new OpenGarrisonDemoRecordingWriter(demoPath, "demo:test"))
            {
                writer.AppendMessage(0, ProtocolCodec.Serialize(BuildMinimalWelcomeMessage()));
                writer.AppendMessage(0, ProtocolCodec.Serialize(BuildMinimalSnapshotMessage()));
                writer.Complete();
            }

            Assert.True(OpenGarrisonDemoTransport.TryVerifyPlaybackSummary(demoPath, out var summary, out var error), error);
            Assert.Contains("verifiedMessages=2", summary, StringComparison.Ordinal);
            Assert.Contains("appliedSnapshots=1", summary, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(demoPath))
            {
                File.Delete(demoPath);
            }
        }
    }

    [Fact]
    public void NetworkClientCanArmRecordingBeforeConnectAndAutoSaveOnDisconnect()
    {
        var demoPath = Path.Combine(Path.GetTempPath(), $"og-demo-client-{Guid.NewGuid():N}.ogdemo");
        try
        {
            var client = new NetworkGameClient();
            try
            {
                Assert.True(client.TryStartDemoRecording(demoPath, "demo:test", initialWelcomePayload: null, out _, out var armError), armError);

                var transport = new FakeInboundTransport("127.0.0.1:8190");
                transport.EnqueueInbound(ProtocolCodec.Serialize(BuildMinimalWelcomeMessage()));
                transport.EnqueueInbound(ProtocolCodec.Serialize(BuildMinimalSnapshotMessage()));

                Assert.True(client.Connect(transport, "Recorder", 0UL, out var connectError), connectError);
                var messages = client.ReceiveMessages().ToArray();
                Assert.Equal(2, messages.Length);

                client.Disconnect();
                Assert.True(client.TryConsumeCompletedDemoRecordingNotice(out var notice));
                Assert.Contains("demo recording saved:", notice, StringComparison.Ordinal);
            }
            finally
            {
                client.Dispose();
            }

            Assert.True(OpenGarrisonDemoTransport.TryVerifyPlaybackSummary(demoPath, out var summary, out var error), error);
            Assert.Contains("verifiedMessages=2", summary, StringComparison.Ordinal);
            Assert.Contains("appliedSnapshots=1", summary, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(demoPath))
            {
                File.Delete(demoPath);
            }
        }
    }

    [Theory]
    [InlineData("arena_lumberyard")]
    [InlineData("cp_dirtbowl")]
    [InlineData("koth_harvest")]
    [InlineData("dkoth_atalia")]
    [InlineData("tdm_mantic")]
    public void VerifyPlaybackSummaryAcceptsMinimalSupportedReplayByMode(string legacyMapName)
    {
        var replayPath = Path.Combine(Path.GetTempPath(), $"redsm-mode-{legacyMapName}-{Guid.NewGuid():N}.rply");
        try
        {
            using (var stream = File.Create(replayPath))
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.Latin1, leaveOpen: false))
            {
                var headerPayload = BuildReplayHeaderPayload(legacyMapName);
                WriteRecord(writer, ConcatBytes(headerPayload, BuildMinimalModeJoinStatePayload(legacyMapName)));
                WriteRecord(writer, BuildModeExerciseFramePayload(legacyMapName));
                WriteRecord(writer, 255);
            }

            Assert.True(ReDsmReplayTransport.TryVerifyPlaybackSummary(replayPath, out var summary, out var error), error);
            Assert.Contains("verifiedMessages=3", summary, StringComparison.Ordinal);
            Assert.Contains("appliedSnapshots=2", summary, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(replayPath))
            {
                File.Delete(replayPath);
            }
        }
    }

    private static void WriteRecord(BinaryWriter writer, params byte[] payload)
    {
        writer.Write((ushort)payload.Length);
        writer.Write(payload);
    }

    private static byte[] BuildMinimalSupportedJoinStatePayload()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.Latin1, leaveOpen: true);

        writer.Write((byte)44); // JOIN_UPDATE
        writer.Write((byte)0);  // playerCount
        writer.Write((byte)1);  // mapAreaIndex

        writer.Write((byte)7);  // CHANGE_MAP
        writer.Write((byte)15); // map name length
        writer.Write(System.Text.Encoding.Latin1.GetBytes("ctf_classicwell"));
        writer.Write((byte)0);  // empty map hash

        writer.Write((byte)8);  // FULL_UPDATE
        writer.Write((ushort)0); // tdmInvulnerabilityTicks
        writer.Write((byte)0);   // playerCount
        writer.Write((ushort)0); // red intel instance count
        writer.Write((ushort)0); // blue intel instance count
        writer.Write((byte)3);   // caplimit
        writer.Write((byte)0);   // red caps
        writer.Write((byte)0);   // blue caps
        writer.Write((byte)10);  // respawn seconds
        writer.Write((byte)12);  // time limit minutes
        writer.Write((uint)21600); // timer ticks
        for (var index = 0; index < 10; index += 1)
        {
            writer.Write((byte)0);
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] BuildReplayHeaderPayload(string mapName)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.Latin1, leaveOpen: true);
        writer.Write((byte)254);
        writer.Write((byte)1);
        writer.Write((byte)0x10);
        writer.Write((byte)0x72);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)7);
        writer.Write(System.Text.Encoding.Latin1.GetBytes("Server1"));
        writer.Write((byte)mapName.Length);
        writer.Write(System.Text.Encoding.Latin1.GetBytes(mapName));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)0);
        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] BuildMinimalModeJoinStatePayload(string mapName)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.Latin1, leaveOpen: true);

        writer.Write((byte)44);
        writer.Write((byte)0);
        writer.Write((byte)1);

        writer.Write((byte)7);
        writer.Write((byte)mapName.Length);
        writer.Write(System.Text.Encoding.Latin1.GetBytes(mapName));
        writer.Write((byte)0);

        writer.Write((byte)8);
        writer.Write((ushort)0);
        writer.Write((byte)0);
        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] BuildModeExerciseFramePayload(string legacyMapName)
    {
        Assert.True(OpenGarrisonStockMapCatalog.TryGetDefinition(legacyMapName, out var definition));
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.Latin1, leaveOpen: true);
        switch (definition.Mode)
        {
            case GameModeKind.Arena:
                writer.Write((byte)40);
                writer.Write((byte)28);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((byte)10);
                writer.Write((byte)12);
                writer.Write((uint)21600);
                writer.Write((ushort)1800);
                writer.Write((byte)0);
                writer.Write((byte)255);
                writer.Write((byte)255);
                writer.Write((ushort)0);
                break;
            case GameModeKind.ControlPoint:
                writer.Write((byte)28);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((byte)10);
                writer.Write((byte)12);
                writer.Write((uint)21600);
                writer.Write((ushort)0);
                WriteNeutralControlPoints(writer, definition.LevelName);
                break;
            case GameModeKind.KingOfTheHill:
            case GameModeKind.DoubleKingOfTheHill:
                writer.Write((byte)28);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((byte)10);
                writer.Write((ushort)0);
                writer.Write((ushort)5400);
                writer.Write((ushort)5400);
                WriteNeutralControlPoints(writer, definition.LevelName);
                break;
            case GameModeKind.TeamDeathmatch:
                writer.Write((byte)28);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((byte)10);
                writer.Write((byte)12);
                writer.Write((uint)21600);
                writer.Write((ushort)30);
                break;
            default:
                throw new InvalidOperationException($"Unsupported mode exercise for {legacyMapName}.");
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteNeutralControlPoints(BinaryWriter writer, string levelName)
    {
        var level = SimpleLevelFactory.CreateImportedLevel(levelName)
            ?? throw new InvalidOperationException($"Test level '{levelName}' could not be imported.");
        var pointCount = level.GetRoomObjects(RoomObjectType.ControlPoint).Count;
        for (var index = 0; index < pointCount; index += 1)
        {
            writer.Write((byte)255);
            writer.Write((byte)255);
            writer.Write((ushort)0);
        }
    }

    private static byte[] ConcatBytes(byte[] first, byte[] second)
    {
        var combined = new byte[first.Length + second.Length];
        Buffer.BlockCopy(first, 0, combined, 0, first.Length);
        Buffer.BlockCopy(second, 0, combined, first.Length, second.Length);
        return combined;
    }

    private static WelcomeMessage BuildMinimalWelcomeMessage()
    {
        return new WelcomeMessage("Demo Server", ProtocolVersion.Current, 30, "ClassicWell", 128, 16);
    }

    private static SnapshotMessage BuildMinimalSnapshotMessage()
    {
        return new SnapshotMessage(
            Frame: 1,
            TickRate: 30,
            LevelName: "ClassicWell",
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

    private sealed class FakeInboundTransport(string remoteDescription) : INetworkClientMessageTransport
    {
        private readonly Queue<byte[]> _inboundPayloads = new();

        public bool HasPendingMessages => _inboundPayloads.Count > 0;
        public bool IsLoopbackConnection => true;
        public string RemoteDescription => remoteDescription;

        public void EnqueueInbound(byte[] payload)
        {
            _inboundPayloads.Enqueue(payload);
        }

        public bool TryReceive(out byte[] payload)
        {
            if (_inboundPayloads.Count == 0)
            {
                payload = Array.Empty<byte>();
                return false;
            }

            payload = _inboundPayloads.Dequeue();
            return true;
        }

        public bool TryConsumeDisconnectReason(out string reason)
        {
            reason = string.Empty;
            return false;
        }

        public void Send(byte[] payload)
        {
        }

        public void Dispose()
        {
        }
    }
}
