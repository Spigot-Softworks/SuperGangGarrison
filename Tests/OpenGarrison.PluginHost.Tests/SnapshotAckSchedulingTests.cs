using System.Reflection;
using System.Linq;
using System.Threading;
using OpenGarrison.Client;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SnapshotAckSchedulingTests
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData(30, 33)]
    [InlineData(60, 17)]
    public void NewerAckFrameIsEligibleOnNextInputAfterPriorFrameWasSent(int tickRate, int inputIntervalMilliseconds)
    {
        using var client = new NetworkGameClient();
        SetField(client, "_serverTickRate", tickRate);
        SetField(client, "_pendingSnapshotAckFrame", 902UL);
        SetField(client, "_snapshotAckLastSentFrame", 901UL);
        SetField(client, "_snapshotAckLastSentAtMilliseconds", 1000L);
        SetEstimatedPingMilliseconds(client, 300);

        var dueFrame = InvokeDueInputAck(client, 1000L + inputIntervalMilliseconds);

        Assert.Equal(902UL, dueFrame);
    }

    [Fact]
    public void RetryOfSameAckFrameWaitsForTwoRtt()
    {
        using var client = new NetworkGameClient();
        SetField(client, "_pendingSnapshotAckFrame", 902UL);
        SetField(client, "_snapshotAckLastSentFrame", 902UL);
        SetField(client, "_snapshotAckLastSentAtMilliseconds", 1000L);
        SetEstimatedPingMilliseconds(client, 300);

        Assert.Null(InvokeDueInputAck(client, 1599L));
        Assert.Equal(902UL, InvokeDueInputAck(client, 1600L));
    }

    [Fact]
    public void ContinuousUnsentFramesKeepFallbackDeadlineAnchoredToOldestArrival()
    {
        using var client = ConnectClient(out _);
        SetField(client, "_serverTickRate", 60);
        SetField(client, "_snapshotAckPendingSinceMilliseconds", 100L);
        SetField(client, "_snapshotAckLastSentAtMilliseconds", 0L);
        SetField(client, "_snapshotAckLastSentFrame", 0UL);
        SetEstimatedPingMilliseconds(client, 300);
        SetField(client, "_pendingSnapshotAckFrame", 1UL);

        for (ulong frame = 2; frame <= 5; frame += 1)
        {
            client.AcknowledgeSnapshot(frame);
        }

        Assert.Equal(100L, (long)typeof(NetworkGameClient).GetField("_snapshotAckPendingSinceMilliseconds", InstanceMembers)!.GetValue(client)!);
        Assert.Equal(5UL, InvokeDueFallbackAck(client, 117L));
    }

    [Fact]
    public void FallbackAgeRestartsWhenANewFrameArrivesAfterPriorAckWasSent()
    {
        using var client = new NetworkGameClient();
        SetField(client, "_serverTickRate", 60);
        SetField(client, "_pendingSnapshotAckFrame", 6UL);
        SetField(client, "_snapshotAckLastSentFrame", 5UL);
        SetField(client, "_snapshotAckLastSentAtMilliseconds", 117L);
        SetField(client, "_snapshotAckPendingSinceMilliseconds", 118L);

        Assert.Null(InvokeDueFallbackAck(client, 134L));
        Assert.Equal(6UL, InvokeDueFallbackAck(client, 135L));
    }

    [Fact]
    public void InputPiggybacksTheNewestUnsentSnapshotAckImmediately()
    {
        using var client = ConnectClient(out var transport);
        SetField(client, "_pendingSnapshotAckFrame", 902UL);
        SetField(client, "_snapshotAckLastSentFrame", 901UL);
        SetField(client, "_snapshotAckLastSentAtMilliseconds", 0L);
        SetEstimatedPingMilliseconds(client, 300);

        client.SendInput(default, 0f, 0f);

        var input = Assert.IsType<InputStateMessage>(GetLastMessage(transport));
        Assert.Equal(902UL, input.SnapshotAckFrame);
    }

    [Fact]
    public void IdleReceivePumpSendsFallbackSnapshotAck()
    {
        using var client = ConnectClient(out var transport);
        SetField(client, "_serverTickRate", 60);
        SetField(client, "_pendingSnapshotAckFrame", 777UL);
        SetField(client, "_snapshotAckLastSentFrame", 0UL);
        SetField(client, "_snapshotAckPendingSinceMilliseconds", 0L);
        Thread.Sleep(25);

        client.ReceiveMessages();

        Assert.Contains(transport.SentPayloads.Select(Deserialize), message => message is SnapshotAckMessage { Frame: 777UL });
    }

    [Fact]
    public void DelayedInputAttachesSnapshotAckOnlyWhenTheInputIsReleased()
    {
        using var client = ConnectClient(out var transport);
        client.NetworkInputDelayTicks = 2;
        SetField(client, "_pendingSnapshotAckFrame", 778UL);
        SetField(client, "_snapshotAckLastSentFrame", 0UL);
        SetField(client, "_snapshotAckPendingSinceMilliseconds", 0L);

        client.SendInput(default, 0f, 0f);
        Assert.DoesNotContain(transport.SentPayloads.Select(Deserialize), message => message is InputStateMessage);

        client.AdvanceNetworkInputTick();
        Assert.DoesNotContain(transport.SentPayloads.Select(Deserialize), message => message is InputStateMessage);
        client.AdvanceNetworkInputTick();

        var input = Assert.IsType<InputStateMessage>(GetLastMessage(transport));
        Assert.Equal(778UL, input.SnapshotAckFrame);
    }

    private static ulong? InvokeDueInputAck(NetworkGameClient client, long nowMilliseconds)
        => (ulong?)typeof(NetworkGameClient)
            .GetMethod("GetDueSnapshotAck", InstanceMembers)!
            .Invoke(client, [nowMilliseconds]);

    private static ulong? InvokeDueFallbackAck(NetworkGameClient client, long nowMilliseconds)
        => (ulong?)typeof(NetworkGameClient)
            .GetMethod("GetDueSnapshotAckFallback", InstanceMembers)!
            .Invoke(client, [nowMilliseconds]);

    private static void SetField(NetworkGameClient client, string name, object value)
        => typeof(NetworkGameClient).GetField(name, InstanceMembers)!.SetValue(client, value);

    private static void SetEstimatedPingMilliseconds(NetworkGameClient client, int milliseconds)
        => typeof(NetworkGameClient)
            .GetProperty(nameof(NetworkGameClient.EstimatedPingMilliseconds))!
            .SetValue(client, milliseconds);

    private static NetworkGameClient ConnectClient(out RecordingTransport transport)
    {
        var client = new NetworkGameClient();
        transport = new RecordingTransport();
        Assert.True(client.Connect(transport, "Tester", 0, out var error), error);
        return client;
    }

    private static IProtocolMessage GetLastMessage(RecordingTransport transport)
    {
        Assert.NotEmpty(transport.SentPayloads);
        return Deserialize(transport.SentPayloads.Last());
    }

    private static IProtocolMessage Deserialize(byte[] payload)
    {
        Assert.True(ProtocolCodec.TryDeserialize(payload, out var message));
        return message!;
    }

    private sealed class RecordingTransport : INetworkClientMessageTransport
    {
        public List<byte[]> SentPayloads { get; } = [];
        public bool IsLoopbackConnection => false;
        public string RemoteDescription => "server.example:8190";
        public bool HasPendingMessages => false;
        public void Send(byte[] payload) => SentPayloads.Add(payload);
        public bool TryConsumeDisconnectReason(out string reason)
        {
            reason = string.Empty;
            return false;
        }
        public bool TryReceive(out byte[] payload)
        {
            payload = [];
            return false;
        }
        public void Dispose() { }
    }
}
