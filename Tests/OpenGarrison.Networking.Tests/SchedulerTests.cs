using System.Buffers.Binary;
using System.Text;
using OpenGarrison.Networking;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.Networking.Tests;

public sealed class SchedulerTests
{
    [Fact]
    public void ReliableOrderedFramesRemainOrderedOnTheirChannel()
    {
        var scheduler = new Protocol64ChannelScheduler();

        Assert.True(scheduler.Enqueue(CreateOutbound(Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control, 1)).Accepted);
        Assert.True(scheduler.Enqueue(CreateOutbound(Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control, 2)).Accepted);
        Assert.True(scheduler.Enqueue(CreateOutbound(Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control, 3)).Accepted);

        Assert.Equal([1UL, 2UL, 3UL], DequeueFrameIds(scheduler));
    }

    [Fact]
    public void ReliableUnorderedFramesUseConfiguredNumberOfLanesWithoutDroppingAcceptedFrames()
    {
        var scheduler = new Protocol64ChannelScheduler(new Protocol64ChannelSchedulerOptions
        {
            ReliableUnorderedLaneCount = 3,
        });

        for (ulong frameId = 1; frameId <= 12; frameId++)
        {
            Assert.True(scheduler.Enqueue(CreateOutbound(
                Protocol64DeliveryKind.ReliableUnordered,
                ChannelType.GameplayEvents,
                frameId)).Accepted);
        }

        var frames = DequeueFrames(scheduler);

        Assert.Equal(12, frames.Count);
        Assert.Equal(Enumerable.Range(1, 12).Select(value => (ulong)value), frames.Select(frame => frame.Header.FrameId));
        Assert.All(frames, frame => Assert.InRange(frame.Stream.Lane, 0, 2));
        Assert.Equal(3, frames.Select(frame => frame.Stream.Lane).Distinct().Count());
    }

    [Fact]
    public void ReliableBackpressureDoesNotDropQueuedFrames()
    {
        var scheduler = new Protocol64ChannelScheduler(new Protocol64ChannelSchedulerOptions
        {
            MaxPendingReliableFrames = 1,
            MaxPendingReliableBytes = 1024,
        });

        var first = CreateOutbound(Protocol64DeliveryKind.ReliableOrdered, ChannelType.Input, 1);
        var second = CreateOutbound(Protocol64DeliveryKind.ReliableOrdered, ChannelType.Input, 2);

        Assert.Equal(ConnectionSendStatus.Queued, scheduler.Enqueue(first).Status);
        Assert.Equal(ConnectionSendStatus.Backpressured, scheduler.Enqueue(second).Status);
        Assert.Equal(1, scheduler.PendingReliableFrames);

        Assert.True(scheduler.TryDequeue(out var dequeuedFirst));
        Assert.Equal(1UL, dequeuedFirst.Header.FrameId);

        Assert.Equal(ConnectionSendStatus.Queued, scheduler.Enqueue(second).Status);
        Assert.True(scheduler.TryDequeue(out var dequeuedSecond));
        Assert.Equal(2UL, dequeuedSecond.Header.FrameId);
        Assert.False(scheduler.TryDequeue(out _));
    }

    [Fact]
    public void LastWinsReplacesPendingStateAndRejectsAnOlderReplacement()
    {
        var scheduler = new Protocol64ChannelScheduler();

        Assert.Equal(
            ConnectionSendStatus.Queued,
            scheduler.Enqueue(CreateOutbound(Protocol64DeliveryKind.LastWins, ChannelType.State, 10, "old", "player-1")).Status);
        Assert.Equal(
            ConnectionSendStatus.Replaced,
            scheduler.Enqueue(CreateOutbound(Protocol64DeliveryKind.LastWins, ChannelType.State, 12, "new", "player-1")).Status);
        Assert.Equal(
            ConnectionSendStatus.IgnoredStale,
            scheduler.Enqueue(CreateOutbound(Protocol64DeliveryKind.LastWins, ChannelType.State, 11, "late", "player-1")).Status);

        Assert.True(scheduler.TryDequeue(out var dequeued));
        Assert.Equal(12UL, dequeued.Header.FrameId);
        Assert.Equal("new", Encoding.UTF8.GetString(dequeued.EncodedPayload.Span[Protocol64FrameHeader.EncodedSize..]));
        Assert.False(scheduler.TryDequeue(out _));
    }

    [Fact]
    public void ReceiveSchedulerRequestsRepairForAnOrderedGapAndReleasesBufferedFrames()
    {
        var scheduler = new Protocol64ReceiveScheduler();

        var first = CreateReceived(1, 1, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control);
        var third = CreateReceived(1, 3, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control);
        var second = CreateReceived(1, 2, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control);

        Assert.Equal(ConnectionReceiveStatus.Delivered, scheduler.Accept(first).Status);
        var gap = scheduler.Accept(third);
        Assert.Equal(ConnectionReceiveStatus.RepairRequested, gap.Status);
        Assert.NotNull(gap.RepairRequest);
        Assert.Equal(2UL, gap.RepairRequest!.MissingSequenceFrom);
        Assert.Equal(2UL, gap.RepairRequest.MissingSequenceTo);

        var released = scheduler.Accept(second);
        Assert.Equal(ConnectionReceiveStatus.Delivered, released.Status);
        Assert.Equal([2UL, 3UL], released.ReleasedFrames.Select(frame => frame.StreamSequence));
    }

    private static ulong[] DequeueFrameIds(Protocol64ChannelScheduler scheduler)
        => DequeueFrames(scheduler).Select(frame => frame.Header.FrameId).ToArray();

    private static List<Protocol64ScheduledFrame> DequeueFrames(Protocol64ChannelScheduler scheduler)
    {
        var frames = new List<Protocol64ScheduledFrame>();
        while (scheduler.TryDequeue(out var frame))
        {
            frames.Add(frame);
        }

        return frames;
    }

    private static Protocol64OutboundFrame CreateOutbound(
        Protocol64DeliveryKind kind,
        ChannelType channel,
        ulong frameId,
        string body = "payload",
        string? replacementKey = null)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var header = new Protocol64FrameHeader(
            Protocol64.Version,
            Protocol64FrameFlags.None,
            (ushort)Protocol64EventId.ChatRelay,
            1,
            1,
            frameId,
            checked((uint)bytes.Length),
            checked((uint)bytes.Length));
        return new Protocol64OutboundFrame(
            CreateEncodedPayload(bytes, header),
            header,
            new Protocol64DeliveryDescriptor(kind, channel),
            replacementKey);
    }

    private static Protocol64ReceivedFrame CreateReceived(
        int streamId,
        ulong streamSequence,
        Protocol64DeliveryKind kind,
        ChannelType channel)
    {
        var bytes = Encoding.UTF8.GetBytes($"frame-{streamSequence}");
        var header = new Protocol64FrameHeader(
            Protocol64.Version,
            Protocol64FrameFlags.None,
            (ushort)Protocol64EventId.ChatRelay,
            1,
            1,
            streamSequence,
            checked((uint)bytes.Length),
            checked((uint)bytes.Length));
        return new Protocol64ReceivedFrame(
            CreateEncodedPayload(bytes, header),
            header,
            new Protocol64DeliveryDescriptor(kind, channel),
            streamId,
            0,
            streamSequence);
    }

    private static byte[] CreateEncodedPayload(byte[] body, Protocol64FrameHeader header)
    {
        var payload = new byte[Protocol64FrameHeader.EncodedSize + body.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, Protocol64FrameHeader.Magic);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4), header.ProtocolVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(6), (ushort)header.Flags);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(8), header.SchemaId);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(10), header.SchemaRevision);
        BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(12), header.ConnectionEpoch);
        BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(20), header.FrameId);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(28), header.EncodedBodyLength);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(32), header.DecodedBodyLength);
        body.CopyTo(payload, Protocol64FrameHeader.EncodedSize);
        return payload;
    }
}
