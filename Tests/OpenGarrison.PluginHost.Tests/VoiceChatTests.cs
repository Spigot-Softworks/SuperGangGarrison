using System.Net;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class VoiceChatTests
{
    private static short[] Tone(int channels = 1) => Enumerable.Range(0, 960 * channels)
        .Select(i => (short)(Math.Sin((i / channels) * 2 * Math.PI * 440 / 48000) * 9000)).ToArray();

    private static AudioPacket Packet(uint sequence = 1)
    {
        using var encoder = StreamingOpus.CreateEncoder(false);
        return new(sequence, [StreamingOpus.Encode(encoder, Tone())]);
    }








    private static int MaxAbs(short[] samples) => samples.Max(sample => Math.Abs((int)sample));



    [Theory]
    [InlineData(VoiceTransmitMode.PushToTalk, true, false, true, false)]
    [InlineData(VoiceTransmitMode.PushToTalk, true, true, true, true)]
    [InlineData(VoiceTransmitMode.PushToTalk, false, true, true, false)]
    [InlineData(VoiceTransmitMode.OpenMicrophone, true, false, true, true)]
    [InlineData(VoiceTransmitMode.OpenMicrophone, false, false, true, false)]
    [InlineData(VoiceTransmitMode.OpenMicrophone, true, true, false, false)]
    [InlineData(VoiceTransmitMode.Disabled, true, true, true, false)]
    public void CaptureRequiresSelectedModeFocusAndServerPermission(VoiceTransmitMode mode, bool allowed, bool held, bool server, bool expected)
        => Assert.Equal(expected, VoiceChatClient.ShouldCapture(mode, allowed, held, server));

    [Fact]
    public void AudioRoundTripsBothWireContainersAndFitsUdpMtu()
    {
        using var encoder = StreamingOpus.CreateEncoder(true);
        var history = new AudioPacketHistory();
        AudioPacket packet = null!;
        for (var i = 0; i < 3; i++) packet = history.Add(StreamingOpus.Encode(encoder, Tone(2)));
        var relay = new AudioRelayMessage(0, 17, "spoofed music name", 0, packet);
        var payload = ProtocolCodec.Serialize(relay);
        Assert.True(payload.Length < 1200);
        Assert.True(ProtocolCodec.TryDeserialize(payload, out var decoded));
        var audio = Assert.IsType<AudioRelayMessage>(decoded);
        Assert.Equal("Jukebox", audio.SpeakerName);
        Assert.Equal(packet.Sequence, audio.Packet.Sequence);
        Assert.Equal(packet.Frames[2], audio.Packet.Frames[2]);
        var registry = Protocol64SchemaRegistryFactory.CreateDefault();
        var framed = Protocol64FrameCodec.EncodeObject(registry, relay, 1, 1);
        Assert.True(framed.Succeeded, framed.Fault?.Message);
        Assert.True(framed.Payload!.Length < 1200);
        var unframed = Protocol64FrameCodec.Decode(framed.Payload, registry);
        Assert.True(unframed.Succeeded, unframed.Fault?.Message);
        Assert.Equal(Protocol64DeliveryKind.LastWins, unframed.Schema!.Descriptor.Delivery.Kind);
        Assert.NotEqual(AudioReplacementKey.For(relay), AudioReplacementKey.For(relay with { SpeakerSlot = 1 }));
    }

    [Fact]
    public void MalformedAudioIsRejectedWithoutThrowingOrLargeAllocations()
    {
        var valid = ProtocolCodec.Serialize(new VoiceSubmitMessage(false, Packet()));
        for (var size = 0; size < valid.Length; size++) Assert.False(ProtocolCodec.TryDeserialize(valid[..size], out _));
        var invalidCount = valid.ToArray();
        // Encoding byte, type, teamOnly, uint sequence, byte frame count.
        invalidCount[7] = 255;
        Assert.False(ProtocolCodec.TryDeserialize(invalidCount, out _));
        var invalidLength = valid.ToArray();
        invalidLength[8] = 255; invalidLength[9] = 255;
        Assert.False(ProtocolCodec.TryDeserialize(invalidLength, out _));
        Assert.False(StreamingOpus.IsValid(new AudioPacket(1, [new byte[] { 0x83, 0xff }])));
    }

    [Fact]
    public void RedundancyRecoversDroppedPacketsAndPlaybackRejectsDuplicatesAndStaleFrames()
    {
        using var encoder = StreamingOpus.CreateEncoder(false);
        using var playback = new AudioPlaybackBuffer(false);
        var history = new AudioPacketHistory();
        var packets = Enumerable.Range(0, 6).Select(_ => history.Add(StreamingOpus.Encode(encoder, Tone()))).ToArray();
        Assert.True(playback.Add(packets[2], 0));
        Assert.True(playback.Add(packets[5], 0.02));
        Assert.False(playback.Add(packets[5], 0.03));
        Assert.False(playback.Add(packets[1], 0.03));
        Assert.Equal(6, playback.PendingFrames);
        var output = new List<short>();
        for (var i = 0; i < 6; i++)
        {
            Assert.True(playback.TryRead(0.061 + i * 0.02, out var pcm));
            output.AddRange(pcm);
        }
        Assert.Equal(5760, output.Count);
        Assert.Contains(output, sample => sample > 1000);
        Assert.False(playback.TryRead(0.3, out _));
        Assert.Equal(0, playback.PendingFrames);
    }

    [Fact]
    public void LongStallsAndSequenceWrapRemainBounded()
    {
        using var playback = new AudioPlaybackBuffer(false);
        var packet = Packet(uint.MaxValue);
        Assert.True(playback.Add(packet, 0));
        Assert.True(playback.Add(packet with { Sequence = 0 }, 0.02));
        for (uint i = 1; i < 500; i++) playback.Add(packet with { Sequence = i }, 0.03 + i * 0.001);
        Assert.InRange(playback.PendingFrames, 1, AudioPlaybackBuffer.MaxPendingFrames);
        Assert.True(playback.TryRead(5, out var pcm));
        Assert.Equal(960, pcm.Length);
        Assert.InRange(playback.PendingFrames, 0, 2);
    }


    [Fact]
    public void AuthenticatedVoiceRoutesByServerIdentityAndHonorsTeamGagReplayAndRateLimits()
    {
        var clients = new Dictionary<byte, ClientSession>
        {
            [1] = new(1, 11, new IPEndPoint(IPAddress.Loopback, 30001), "Alice", TimeSpan.Zero),
            [2] = new(2, 12, new IPEndPoint(IPAddress.Loopback, 30002), "Bob", TimeSpan.Zero),
            [3] = new(3, 13, new IPEndPoint(IPAddress.Loopback, 30003), "Unauthenticated", TimeSpan.Zero) { IsAuthorized = false },
        };
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.TryPrepareNetworkPlayerJoin(1); world.TryPrepareNetworkPlayerJoin(2);
        world.TrySetNetworkPlayerTeam(1, PlayerTeam.Red); world.TrySetNetworkPlayerTeam(2, PlayerTeam.Blue);
        var sent = new List<(ServerTransportPeer Peer, IProtocolMessage Message)>();
        var now = 0d;
        using var server = new ServerAudioService(new(), clients, world, (peer, message) => sent.Add((peer, message)), () => now, _ => { });
        var voice = new VoiceSubmitMessage(false, Packet());
        server.ReceiveVoice(clients[3], voice);
        Assert.Empty(sent);
        server.ReceiveVoice(clients[1], voice);
        var relayed = Assert.IsType<AudioRelayMessage>(Assert.Single(sent).Message);
        Assert.Equal((byte)1, relayed.SpeakerSlot);
        Assert.Equal("Alice", relayed.SpeakerName);
        Assert.Equal(clients[2].Peer, sent[0].Peer);
        sent.Clear();
        server.ReceiveVoice(clients[1], voice); // Replay
        Assert.Empty(sent);
        server.ReceiveVoice(clients[1], voice with { TeamOnly = true, Packet = voice.Packet with { Sequence = 2 } });
        Assert.Empty(sent);
        clients[1].IsGagged = true;
        server.ReceiveVoice(clients[1], voice with { Packet = voice.Packet with { Sequence = 3 } });
        Assert.Empty(sent);
        server.SendState(clients[1]);
        Assert.False(Assert.IsType<ServerAudioStateMessage>(Assert.Single(sent).Message).VoiceEnabled);
        clients[1].IsGagged = false; sent.Clear();
        for (uint i = 3; i < 100; i++) server.ReceiveVoice(clients[1], voice with { Packet = voice.Packet with { Sequence = i } });
        Assert.InRange(sent.Count, 1, 10);
        now += 1;
        server.ReceiveVoice(clients[1], voice with { Packet = voice.Packet with { Sequence = 101 } });
        Assert.True(sent.Count > 1);
    }

    [Fact]
    public void CaptureToWireToPlaybackAndMuteAndDisconnectUseRealOpus()
    {
        var senderDevice = new FakeVoiceDevice();
        var receiverDevice = new FakeVoiceDevice();
        var now = 0d;
        using var receiver = new VoiceChatClient(receiverDevice, new(), (_, _) => { });
        var packets = 0;
        using var sender = new VoiceChatClient(senderDevice, new(), (_, packet) =>
        {
            var wire = ProtocolCodec.Serialize(new AudioRelayMessage(1, 1, "Alice", 1, packet));
            Assert.True(ProtocolCodec.TryDeserialize(wire, out var decoded));
            receiver.Receive(Assert.IsType<AudioRelayMessage>(decoded), now);
            packets++;
        });
        sender.ApplyState(new(1, true, false, 0, false, false, ""));
        receiver.ApplyState(new(1, true, false, 0, false, false, ""));
        senderDevice.Captured.Enqueue(Tone().Select(sample => sample / 32768f).ToArray());
        sender.Update(now, true, false, 1, _ => false);
        Assert.Equal(0, packets);
        sender.Update(now, true, true, 1, _ => false);
        Assert.Equal(1, packets);
        now = 0.08;
        receiver.Update(now, true, false, 1, _ => false);
        Assert.Single(receiverDevice.Played);
        Assert.Contains(receiverDevice.Played[0], sample => sample != 0);
        Assert.True(sender.IsTransmitting(now));
        sender.Update(now, false, true, 1, _ => false);
        Assert.False(senderDevice.CaptureActive);
        receiver.Update(now, true, false, 1, _ => true);
        Assert.Contains((byte)1, receiverDevice.Stopped);
        receiver.Reset();
        Assert.Empty(receiver.Speakers);
        Assert.Null(receiver.ServerState);
        Assert.False(receiverDevice.CaptureActive);
    }

    [Fact]
    public void TransmitPermissionDoesNotInterruptIncomingVoiceForAGaggedListener()
    {
        var device = new FakeVoiceDevice();
        using var client = new VoiceChatClient(device, new(), (_, _) => { });
        client.ApplyState(new(1, false, false, 0, false, false, ""));
        client.Receive(new(2, 1, "Alice", 1, Packet()), 0);
        client.ApplyState(new(1, false, false, 0, false, false, ""));
        client.Update(0.08, true, true, 1, _ => false);
        Assert.False(device.CaptureActive);
        Assert.Single(device.Played);
    }


    [Fact]
    public void ChannelMembershipAndPersonalizedAcknowledgmentsRoundTripBothProtocols()
    {
        IProtocolMessage[] messages = [new VoiceChannelMembershipMessage(42, true),
            new ServerAudioStateMessage(9, true, false, 2, true, false, "Song", true, true, 42)];
        var registry = Protocol64SchemaRegistryFactory.CreateDefault();
        foreach (var message in messages)
        {
            var bytes = ProtocolCodec.Serialize(message);
            Assert.True(ProtocolCodec.TryDeserialize(bytes, out var decoded));
            Assert.Equal(message, decoded);
            for (var size = 0; size < bytes.Length; size++) Assert.False(ProtocolCodec.TryDeserialize(bytes[..size], out _));
            var frame = Protocol64FrameCodec.EncodeObject(registry, message, 1, 1);
            Assert.True(frame.Succeeded, frame.Fault?.Message);
            var result = Protocol64FrameCodec.Decode(frame.Payload!, registry);
            Assert.True(result.Succeeded, result.Fault?.Message);
            Assert.Equal(message, result.Event);
        }
        Assert.NotEqual(AudioReplacementKey.For(messages[0]), AudioReplacementKey.For(new VoiceSubmitMessage(false, Packet())));
    }

    [Fact]
    public void LastToDieServerRequiresBothParticipantsToJoinRejectsStaleChangesAndResetsReconnectMembership()
    {
        var clients = new Dictionary<byte, ClientSession>
        {
            [1] = new(1, 1, new IPEndPoint(IPAddress.Loopback, 30001), "Alice", TimeSpan.Zero),
            [2] = new(2, 2, new IPEndPoint(IPAddress.Loopback, 30002), "Bob", TimeSpan.Zero),
            [3] = new(3, 3, new IPEndPoint(IPAddress.Loopback, 30003), "Unauthenticated", TimeSpan.Zero) { IsAuthorized = false },
        };
        var sent = new List<(ServerTransportPeer Peer, IProtocolMessage Message)>();
        using var server = new ServerAudioService(new(), clients, new SimulationWorld(new SimulationConfig { EnableLocalDummies = false }),
            (peer, message) => sent.Add((peer, message)), () => 0, _ => { }, requireVoiceChannelJoin: true);
        var voice = new VoiceSubmitMessage(false, Packet());
        server.SendState(clients[1]);
        var initial = Assert.IsType<ServerAudioStateMessage>(Assert.Single(sent).Message);
        Assert.True(initial.VoiceChannelRequiresJoin);
        Assert.False(initial.VoiceChannelJoined);
        sent.Clear();
        server.ReceiveMembership(clients[3], new(1, true));
        server.ReceiveVoice(clients[1], voice);
        Assert.Empty(sent);
        server.ReceiveMembership(clients[1], new(1, true));
        sent.Clear();
        server.ReceiveVoice(clients[1], voice);
        Assert.Empty(sent); // The recipient has not joined.
        server.ReceiveMembership(clients[2], new(1, true));
        sent.Clear();
        server.ReceiveVoice(clients[1], voice with { Packet = voice.Packet with { Sequence = 2 } });
        Assert.Equal(clients[2].Peer, Assert.Single(sent).Peer);
        Assert.IsType<AudioRelayMessage>(sent[0].Message);

        server.ReceiveMembership(clients[2], new(3, false));
        server.ReceiveMembership(clients[2], new(2, true)); // Delayed join cannot reverse leave.
        server.ReceiveMembership(clients[2], new(3, true)); // Same revision cannot change intent.
        server.SendState(clients[2]);
        var left = Assert.IsType<ServerAudioStateMessage>(sent.Last().Message);
        Assert.False(left.VoiceChannelJoined);
        Assert.Equal(3u, left.VoiceChannelRevision);
        sent.Clear();
        server.ReceiveVoice(clients[1], voice with { Packet = voice.Packet with { Sequence = 3 } });
        server.ReceiveVoice(clients[2], voice);
        Assert.Empty(sent);

        server.ReceiveMembership(clients[2], new(4, true));
        var oldClient = clients[2];
        clients[2] = new(2, 4, new IPEndPoint(IPAddress.Loopback, 30004), "Reconnected Bob", TimeSpan.Zero);
        server.ReceiveMembership(oldClient, new(5, true));
        server.SendState(clients[2]);
        Assert.False(Assert.IsType<ServerAudioStateMessage>(sent.Last().Message).VoiceChannelJoined);
        sent.Clear();
        server.ReceiveVoice(clients[1], voice with { Packet = voice.Packet with { Sequence = 4 } });
        Assert.Empty(sent);
    }

    [Fact]
    public void ChannelOptInRetriesLossWaitsForAckAndLeaveImmediatelyStopsCaptureAndListeningButKeepsJukebox()
    {
        var device = new FakeVoiceDevice();
        var membershipRequests = new List<VoiceChannelMembershipMessage>();
        var captured = new List<AudioPacket>();
        using var client = new VoiceChatClient(device, new(), (_, packet) => captured.Add(packet), membershipRequests.Add);
        var initial = new ServerAudioStateMessage(1, true, false, 7, true, false, "Song", true, false, 0);
        client.ApplyState(initial);
        client.Receive(new(2, 1, "Alice", 1, Packet()), 0);
        client.Update(0, true, true, 1, _ => false);
        Assert.False(device.CaptureActive);
        Assert.Empty(client.Speakers);
        Assert.Empty(membershipRequests);

        client.SetVoiceChannelJoined(true);
        Assert.True(client.IsVoiceChannelJoined);
        Assert.False(client.CanUseVoiceChannel);
        var join = Assert.Single(membershipRequests);
        Assert.True(join.Joined);
        client.Update(0.3, true, true, 1, _ => false); // Initial request or ack was lost.
        Assert.Equal(join, membershipRequests.Last());
        Assert.Equal(2, membershipRequests.Count);
        Assert.False(device.CaptureActive);
        client.ApplyState(initial with { Revision = 2, VoiceChannelJoined = true, VoiceChannelRevision = join.Revision });
        device.Captured.Enqueue(Tone().Select(sample => sample / 32768f).ToArray());
        client.Update(0.31, true, true, 1, _ => false);
        Assert.True(device.CaptureActive);
        Assert.Single(captured);
        client.Receive(new(2, 1, "Alice", 1, Packet()), 0.31);
        client.Receive(new(0, 7, "Jukebox", 0, Packet()), 0.31);
        client.Update(0.4, true, false, 1, _ => false);
        Assert.Equal(2, device.Played.Count);

        client.SetVoiceChannelJoined(false);
        var leave = membershipRequests.Last();
        Assert.False(leave.Joined);
        Assert.False(device.CaptureActive);
        Assert.Contains((byte)2, device.Stopped);
        Assert.Equal((byte)0, Assert.Single(client.Speakers).Slot);
        client.ApplyState(initial with { Revision = 2, VoiceChannelJoined = true, VoiceChannelRevision = join.Revision });
        client.Receive(new(2, 1, "Alice", 1, Packet(2)), 0.41);
        Assert.False(client.CanUseVoiceChannel);
        Assert.Equal((byte)0, Assert.Single(client.Speakers).Slot);
        client.Update(0.7, true, true, 1, _ => false);
        Assert.Equal(leave, membershipRequests.Last());
        client.ApplyState(initial with { Revision = 3, VoiceChannelJoined = false, VoiceChannelRevision = leave.Revision });
        var count = membershipRequests.Count;
        client.Update(1, true, true, 1, _ => false);
        Assert.Equal(count, membershipRequests.Count);

        client.SetVoiceChannelJoined(true);
        var rejoin = membershipRequests.Last();
        client.ApplyState(initial with { Revision = 4, VoiceChannelJoined = true, VoiceChannelRevision = rejoin.Revision });
        client.SuspendCapture(); // Map/stage presentation reset leaves channel membership intact.
        Assert.True(client.CanUseVoiceChannel);
        client.Reset(); // Actual disconnect/reconnect starts outside the channel.
        client.ApplyState(initial);
        Assert.False(client.IsVoiceChannelJoined);
        Assert.False(client.CanUseVoiceChannel);
    }

    private sealed class FakeVoiceDevice : IVoiceAudioDevice
    {
        public bool UsesGameMasterVolume => false;
        public bool CaptureActive { get; private set; }
        public string Status => "";
        public IReadOnlyList<string> MicrophoneNames => ["Test microphone"];
        public Queue<float[]> Captured { get; } = new();
        public List<short[]> Played { get; } = new();
        public List<(byte Slot, float Volume, float Pan)> Playback { get; } = new();
        public List<(byte Slot, float Volume)> Volumes { get; } = new();
        public List<byte> Stopped { get; } = new();
        public void SetCapture(bool active, string name) => CaptureActive = active;
        public bool TryReadCapture(out float[] samples, out int rate) { rate = 48000; return Captured.TryDequeue(out samples!); }
        public void Play(byte slot, short[] samples, int channels, float volume, float pan)
        {
            Played.Add(samples);
            Playback.Add((slot, volume, pan));
        }
        public void SetVolume(byte slot, float volume) => Volumes.Add((slot, volume));
        public void StopPlayback(byte slot) => Stopped.Add(slot);
        public void Dispose() => CaptureActive = false;
    }
}
