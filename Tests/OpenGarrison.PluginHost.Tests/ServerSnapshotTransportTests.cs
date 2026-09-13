using System.Net;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ServerSnapshotTransportTests
{
    [Fact]
    public void RepeatedClientBubbleClearsRemainCanonicalFrames()
    {
        using var client = new NetworkGameClient();
        var transport = new RecordingClientTransport();
        Assert.True(client.Connect(transport, "Tester", 0, out var error), error);
        client.SetLocalPlayerSlot(1);
        transport.Payloads.Clear();

        client.SendCustomBubbleClear();
        client.SendCustomBubbleClear();

        Assert.Equal(2, transport.Payloads.Count);
        foreach (var payload in transport.Payloads)
        {
            var decoded = Protocol64FrameCodec.Decode<CustomBubbleClearMessage>(payload,
                Protocol64SchemaRegistryFactory.CreateDefault(),
                new Protocol64FrameDecodeOptions { ExpectedDirection = Protocol64Direction.ClientToServer });
            Assert.True(decoded.Succeeded, decoded.Fault?.Message);
            Assert.Equal((byte)0, decoded.Event!.PlayerSlot);
        }
    }

    [Fact]
    public void ServerBubbleClearRemainsCanonicalAfterBidirectionalAdmission()
    {
        var peer = ServerTransportPeer.FromWebSocketSession(7, IPAddress.Loopback, 8190, protocol64: true);
        var client = new ClientSession(1, 101, peer, "Tester", TimeSpan.Zero) { IsAuthorized = true, Protocol64Enabled = true };
        var transport = new RecordingTransport();
        var logs = new List<string>();
        var outbound = new ServerOutboundMessaging(transport, "Test", new SimulationWorld(),
            new Dictionary<byte, ClientSession> { [1] = client }, 2, () => null, () => null, (_, _) => { }, logs.Add);

        outbound.SendMessage(peer, new CustomBubbleClearMessage(1));

        Assert.Empty(logs);
        Assert.True(transport.Protocol64);
        var decoded = Protocol64FrameCodec.Decode<CustomBubbleClearMessage>(transport.Payload!,
            Protocol64SchemaRegistryFactory.CreateDefault(),
            new Protocol64FrameDecodeOptions { ExpectedDirection = Protocol64Direction.ServerToClient });
        Assert.True(decoded.Succeeded, decoded.Fault?.Message);
        Assert.Equal((byte)1, decoded.Event!.PlayerSlot);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void InitialWorldSnapshotUsesThePeersNegotiatedTransport(bool protocol64, bool quic)
    {
        var peer = quic
            ? ServerTransportPeer.FromQuicSession(7, new IPEndPoint(IPAddress.Loopback, 8190))
            : ServerTransportPeer.FromWebSocketSession(7, IPAddress.Loopback, 8190, protocol64);
        var client = new ClientSession(1, 101, peer, "Tester", TimeSpan.Zero)
        {
            IsAuthorized = true,
            Protocol64Enabled = protocol64,
        };
        var transport = new RecordingTransport();
        var logs = new List<string>();
        var outbound = new ServerOutboundMessaging(transport, "Test", new SimulationWorld(),
            new Dictionary<byte, ClientSession> { [1] = client }, 2, () => null, () => null, (_, _) => { }, logs.Add);
        var snapshot = new SnapshotMessage(
            Frame: 42, TickRate: 60, LevelName: "Harvest", MapAreaIndex: 0, MapAreaCount: 1,
            GameMode: 1, MatchPhase: 1, WinnerTeam: 0, TimeRemainingTicks: 600,
            RedCaps: 0, BlueCaps: 0, SpectatorCount: 0, LastProcessedInputSequence: 17,
            RedIntel: new SnapshotIntelState(1, 0f, 0f, true, false, 0),
            BlueIntel: new SnapshotIntelState(2, 0f, 0f, true, false, 0),
            Players: [], CombatTraces: [], SniperAimIndicators: [], Sentries: [], Shots: [],
            Bubbles: [], Blades: [], Needles: [], RevolverShots: [], Rockets: [], Flames: [], Flares: [],
            Mines: [], DeadBodies: [], ControlPointSetupTicksRemaining: 0, KothUnlockTicksRemaining: 0,
            KothRedTimerTicksRemaining: 0, KothBlueTimerTicksRemaining: 0, ControlPoints: [], Generators: [],
            LocalDeathCam: null, KillFeed: [], VisualEvents: [], DamageEvents: [], SoundEvents: []);
        var legacyPayload = ProtocolCodec.Serialize(snapshot);

        outbound.SendSnapshotPayload(peer, snapshot, legacyPayload);

        Assert.Empty(logs);
        Assert.Equal(protocol64, transport.Protocol64);
        Assert.NotNull(transport.Payload);
        if (protocol64)
        {
            var decoded = Protocol64FrameCodec.Decode<SnapshotMessage>(transport.Payload!, Protocol64SchemaRegistryFactory.CreateDefault());
            Assert.True(decoded.Succeeded, decoded.Fault?.Message);
            Assert.Equal(42UL, decoded.Event!.Frame);
            Assert.Equal("Harvest", decoded.Event.LevelName);
        }
        else Assert.Same(legacyPayload, transport.Payload);
    }

    private sealed class RecordingClientTransport : INetworkClientMessageTransport
    {
        public List<byte[]> Payloads { get; } = [];
        public bool HasPendingMessages => false;
        public bool IsLoopbackConnection => false;
        public string RemoteDescription => "ws64://127.0.0.1:8190";
        public bool TryReceive(out byte[] payload) { payload = []; return false; }
        public bool TryConsumeDisconnectReason(out string reason) { reason = string.Empty; return false; }
        public void Send(byte[] payload) => Payloads.Add(payload);
        public void Dispose() { }
    }

    private sealed class RecordingTransport : IServerMessageTransport
    {
        public bool HasPendingMessages => false;
        public byte[]? Payload { get; private set; }
        public bool Protocol64 { get; private set; }
        public ServerMessagePacket Receive() => throw new InvalidOperationException();
        public void Send(ServerTransportPeer peer, byte[] payload, MessageType? messageType = null) => Payload = payload;
        public void SendProtocol64(ServerTransportPeer peer, byte[] payload, Protocol64DeliveryDescriptor delivery, string? replacementKey = null)
        {
            Payload = payload;
            Protocol64 = true;
        }
    }
}
