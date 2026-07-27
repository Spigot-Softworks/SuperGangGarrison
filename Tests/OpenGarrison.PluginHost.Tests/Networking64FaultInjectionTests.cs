using System.Buffers.Binary;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class Networking64FaultInjectionTests
{
    [Fact]
    public void TruncatedFrameIsNotDeliveredUntilTheMissingBytesArrive()
    {
        var expectedFrame = CreateFrame(7, 1, Networking64DeliveryMode.ReliableOrdered, "complete");
        var encoded = Networking64FrameCodec.Encode(expectedFrame);
        var reader = new Networking64FrameReader();

        reader.Append(encoded.AsSpan(0, encoded.Length - 1));

        Assert.False(reader.TryRead(out var partialFrame, out var partialFailure));
        Assert.Null(partialFrame);
        Assert.Equal(Networking64DecodeFailureKind.NeedMoreData, partialFailure);
        Assert.True(reader.BufferedByteCount > 0);

        reader.Append(encoded.AsSpan(encoded.Length - 1, 1));

        Assert.True(reader.TryRead(out var decodedFrame, out var decodedFailure));
        Assert.Equal(Networking64DecodeFailureKind.None, decodedFailure);
        Assert.NotNull(decodedFrame);
        Assert.Equal(expectedFrame.StreamId, decodedFrame!.StreamId);
        Assert.Equal(expectedFrame.Sequence, decodedFrame.Sequence);
        Assert.Equal(expectedFrame.Payload, decodedFrame.Payload);
        Assert.Equal(0, reader.BufferedByteCount);
    }

    [Fact]
    public void LossIsExplicitAndDoesNotInventFrames()
    {
        var faults = new Networking64FaultScript();
        faults.DropSequences.Add(2);
        var transport = new Networking64InMemoryTransport(maxPendingPackets: 8, faultScript: faults);

        Assert.True(transport.TrySend(CreateFrame(1, 1)));
        Assert.True(transport.TrySend(CreateFrame(1, 2)));
        Assert.True(transport.TrySend(CreateFrame(1, 3)));

        var receivedSequences = ReceiveSequences(transport);

        Assert.Equal([1UL, 3UL], receivedSequences);
        Assert.Contains(transport.Events, transportEvent =>
            transportEvent.Kind == Networking64TransportEventKind.Dropped && transportEvent.Sequence == 2);
    }

    [Fact]
    public void DuplicationIsVisibleAtTheTransportAndSuppressedByReliableUnorderedDelivery()
    {
        var faults = new Networking64FaultScript();
        faults.DuplicateSequences.Add(4);
        var transport = new Networking64InMemoryTransport(maxPendingPackets: 8, faultScript: faults);
        var receiver = new Networking64ReliableUnorderedReceiver();

        Assert.True(transport.TrySend(CreateFrame(2, 4, Networking64DeliveryMode.ReliableUnordered)));

        var delivered = new List<Networking64Frame>();
        while (transport.TryReceive(out var packet))
        {
            Assert.True(Networking64FrameCodec.TryDecode(packet.Bytes, out var frame, out var failure));
            Assert.Equal(Networking64DecodeFailureKind.None, failure);
            delivered.AddRange(receiver.Accept(frame!));
        }

        Assert.Equal(2, transport.Events.Single(eventRecord =>
            eventRecord.Kind == Networking64TransportEventKind.Duplicated).AffectedPacketCount);
        Assert.Single(delivered);
        Assert.Equal(4UL, delivered[0].Sequence);
        Assert.Equal(1, receiver.DuplicateCount);
    }

    [Fact]
    public void ReorderingDoesNotChangeReliableOrderedDelivery()
    {
        var transport = new Networking64InMemoryTransport(maxPendingPackets: 8);
        var receiver = new Networking64ReliableOrderedReceiver();

        Assert.True(transport.TrySend(CreateFrame(3, 1)));
        Assert.True(transport.TrySend(CreateFrame(3, 2)));
        Assert.True(transport.TrySend(CreateFrame(3, 3)));
        transport.ReorderPendingPackets();

        var deliveredSequences = new List<ulong>();
        while (transport.TryReceive(out var packet))
        {
            Assert.True(Networking64FrameCodec.TryDecode(packet.Bytes, out var frame, out _));
            deliveredSequences.AddRange(receiver.Accept(frame!).Select(delivered => delivered.Sequence));
        }

        Assert.Equal([1UL, 2UL, 3UL], deliveredSequences);
        Assert.Equal(2, receiver.BufferedOutOfOrderCount);
        Assert.Empty(receiver.MissingSequences);
    }

    [Fact]
    public void ReliableOrderedReceiverReportsGapsAndReleasesBufferedFramesAfterRepair()
    {
        var receiver = new Networking64ReliableOrderedReceiver();

        Assert.Single(receiver.Accept(CreateFrame(4, 1)));
        Assert.Empty(receiver.Accept(CreateFrame(4, 3)));
        Assert.Equal([2UL], receiver.MissingSequences);

        var repaired = receiver.Accept(CreateFrame(4, 2));

        Assert.Equal([2UL, 3UL], repaired.Select(frame => frame.Sequence));
        Assert.Empty(receiver.MissingSequences);
        Assert.Equal(4UL, receiver.NextSequence);
    }

    [Fact]
    public void StreamResetRemovesOnlyTheFaultedStreamAndLeavesOtherStreamsIntact()
    {
        var transport = new Networking64InMemoryTransport(maxPendingPackets: 8);
        Assert.True(transport.TrySend(CreateFrame(10, 1)));
        Assert.True(transport.TrySend(CreateFrame(11, 1)));
        Assert.True(transport.TrySend(CreateFrame(10, 2)));

        var removed = transport.ResetStream(10);

        Assert.Equal(2, removed);
        Assert.True(transport.TryReceive(out var survivingPacket));
        Assert.Equal(11, survivingPacket.StreamId);
        Assert.False(transport.TryReceive(out _));
        Assert.Contains(transport.Events, transportEvent =>
            transportEvent.Kind == Networking64TransportEventKind.StreamReset
            && transportEvent.StreamId == 10
            && transportEvent.AffectedPacketCount == 2);
    }

    [Fact]
    public void BackpressureRejectsAFrameWithoutSilentlyDroppingQueuedFrames()
    {
        var transport = new Networking64InMemoryTransport(maxPendingPackets: 1);

        Assert.True(transport.TrySend(CreateFrame(12, 1)));
        Assert.False(transport.TrySend(CreateFrame(12, 2)));
        Assert.Equal(1, transport.PendingPacketCount);

        Assert.True(transport.TryReceive(out var queuedPacket));
        Assert.Equal(1UL, queuedPacket.Sequence);
        Assert.False(transport.TryReceive(out _));
        Assert.Contains(transport.Events, transportEvent =>
            transportEvent.Kind == Networking64TransportEventKind.Backpressured
            && transportEvent.Sequence == 2);
    }

    [Fact]
    public void LastWinsMailboxReplacesNewerStateAndRejectsLateState()
    {
        var mailbox = new Networking64LastWinsMailbox<string>();

        Assert.True(mailbox.TryReplace(10, "old"));
        Assert.True(mailbox.TryReplace(12, "new"));
        Assert.False(mailbox.TryReplace(11, "late"));
        Assert.True(mailbox.HasValue);
        Assert.Equal(12UL, mailbox.LatestSequence);

        Assert.True(mailbox.TryTake(out var value));
        Assert.Equal("new", value);
        Assert.False(mailbox.HasValue);
    }

    [Fact]
    public void OversizedDeclaredPayloadIsRejectedBeforeFrameDelivery()
    {
        var oversizedHeader = new byte[Networking64FrameCodec.HeaderLength];
        BinaryPrimitives.WriteUInt16LittleEndian(oversizedHeader.AsSpan(0, 2), 0x6434);
        oversizedHeader[2] = Networking64FrameCodec.ProtocolVersion;
        oversizedHeader[3] = (byte)Networking64DeliveryMode.ReliableOrdered;
        BinaryPrimitives.WriteInt32LittleEndian(oversizedHeader.AsSpan(16, 4), Networking64FrameCodec.MaxPayloadLength + 1);

        var reader = new Networking64FrameReader();
        reader.Append(oversizedHeader);

        Assert.False(reader.TryRead(out var frame, out var failure));
        Assert.Null(frame);
        Assert.Equal(Networking64DecodeFailureKind.FrameTooLarge, failure);
    }

    private static Networking64Frame CreateFrame(
        int streamId,
        ulong sequence,
        Networking64DeliveryMode delivery = Networking64DeliveryMode.ReliableOrdered,
        string payload = "payload")
    {
        return new Networking64Frame(streamId, sequence, delivery, System.Text.Encoding.UTF8.GetBytes(payload));
    }

    private static IReadOnlyList<ulong> ReceiveSequences(Networking64InMemoryTransport transport)
    {
        var sequences = new List<ulong>();
        while (transport.TryReceive(out var packet))
        {
            sequences.Add(packet.Sequence);
        }

        return sequences;
    }
}
