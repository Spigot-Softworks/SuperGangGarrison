using OpenGarrison.Networking;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.Networking.Tests;

public sealed class QuicConnectionTests
{
    [Fact]
    public void SelectStreamBuildsCanonicalControlInputLanesAndLastWinsFallback()
    {
        using var container = new Protocol64QuicConnectionContainer(
            401,
            new Protocol64QuicConnectionOptions
            {
                SchedulerOptions = new Protocol64ChannelSchedulerOptions
                {
                    ReliableUnorderedLaneCount = 3,
                },
                DatagramsAvailable = false,
            });

        var control = container.ControlStream;
        var input = container.InputStream;
        var lane0 = container.SelectStream(new(
            ChannelType.GameplayEvents,
            Protocol64DeliveryKind.ReliableUnordered,
            0));
        var lane1 = container.SelectStream(new(
            ChannelType.GameplayEvents,
            Protocol64DeliveryKind.ReliableUnordered,
            1));
        var lane2 = container.SelectStream(new(
            ChannelType.GameplayEvents,
            Protocol64DeliveryKind.ReliableUnordered,
            2));
        var lastWins = container.SelectStream(new(
            ChannelType.State,
            Protocol64DeliveryKind.LastWins,
            0));

        Assert.Equal(Protocol64QuicStreamRole.Control, control.Role);
        Assert.Equal(Protocol64QuicStreamRole.Input, input.Role);
        Assert.Equal(Protocol64QuicStreamRole.ReliableUnordered, lane0.Role);
        Assert.Equal(Protocol64QuicStreamRole.ReliableUnordered, lane1.Role);
        Assert.Equal(Protocol64QuicStreamRole.ReliableUnordered, lane2.Role);
        Assert.Equal(3, new[] { lane0, lane1, lane2 }.Select(stream => stream.StreamId).Distinct().Count());
        Assert.Equal(Protocol64QuicStreamRole.LastWinsFallback, lastWins.Role);
        Assert.True(lastWins.IsFallback);
        Assert.False(lastWins.IsDatagram);
        Assert.Same(lastWins, container.SelectStream(lastWins.Stream));
    }

    [Fact]
    public void DatagramAvailabilitySelectsLastWinsDatagramLane()
    {
        using var container = new Protocol64QuicConnectionContainer(
            402,
            new Protocol64QuicConnectionOptions
            {
                DatagramsAvailable = true,
                PreferDatagramsForLastWins = true,
            });

        var selection = container.SelectStream(new(
            ChannelType.State,
            Protocol64DeliveryKind.LastWins,
            0));

        Assert.Equal(Protocol64QuicStreamRole.LastWinsDatagram, selection.Role);
        Assert.True(selection.IsDatagram);
        Assert.Equal(-1, selection.StreamId);
    }

    [Fact]
    public void DequeuedFrameExposesItsSelectedQuicStream()
    {
        using var container = new Protocol64QuicConnectionContainer(403);
        var frame = CreateFrame(
            403,
            1,
            new Protocol64DeliveryDescriptor(
                Protocol64DeliveryKind.ReliableOrdered,
                ChannelType.Input));

        var accepted = container.EnqueueSend(frame);

        Assert.True(accepted.Accepted);
        Assert.True(container.TryDequeueSend(out var scheduled, out var selection));
        Assert.Equal(accepted.Stream!.Value, scheduled.Stream);
        Assert.Equal(Protocol64QuicStreamRole.Input, selection.Role);
        Assert.Equal(selection.StreamId, container.InputStream.StreamId);
    }

    [Fact]
    public void LastWinsFallbackUsesTheExistingReplaceableMailbox()
    {
        using var container = new Protocol64QuicConnectionContainer(406);
        var delivery = new Protocol64DeliveryDescriptor(
            Protocol64DeliveryKind.LastWins,
            ChannelType.State);

        Assert.True(container.EnqueueSend(CreateFrame(406, 1, delivery, "player-7")).Accepted);
        var replacement = container.EnqueueSend(CreateFrame(406, 2, delivery, "player-7"));

        Assert.Equal(ConnectionSendStatus.Replaced, replacement.Status);
        Assert.True(container.TryDequeueSend(out var scheduled, out var stream));
        Assert.Equal((ulong)2, scheduled.Header.FrameId);
        Assert.Equal(Protocol64QuicStreamRole.LastWinsFallback, stream.Role);
    }

    [Fact]
    public void StreamFaultReopensQueuesControlRepairAndReservesDedicatedRetransmit()
    {
        using var container = new Protocol64QuicConnectionContainer(404);
        var original = container.SelectStream(new(
            ChannelType.GameplayEvents,
            Protocol64DeliveryKind.ReliableUnordered,
            0));
        var fault = CreateFault(404, original.StreamId, ChannelType.GameplayEvents);

        var result = container.ReportTransportFault(fault);

        Assert.True(result.Accepted);
        Assert.Equal(Protocol64ConnectionState.Recovering, container.State);
        Assert.Equal(Protocol64StreamState.AwaitingRetransmit, result.Transition.CurrentStreamState);
        Assert.NotNull(result.RepairRequest);
        Assert.Single(container.PendingControlRepairRequests);
        Assert.Single(container.PendingRetransmitPlans);

        var plan = container.PendingRetransmitPlans.Single();
        Assert.Equal(container.ControlStream.StreamId, plan.ControlStream.StreamId);
        Assert.Equal(Protocol64QuicStreamRole.RecoveryRetransmit, plan.DedicatedStream.Role);
        Assert.NotEqual(original.StreamId, plan.ReopenedStream.StreamId);
        Assert.Equal(1, plan.ReopenedStream.Generation);

        Assert.True(container.TryDequeueControlRepairRequest(out var controlRepair));
        Assert.Equal(result.RepairRequest, controlRepair);

        var completed = container.MarkRepairCompleted(
            result.RepairRequest!.RequestId,
            plan.ReopenedStream.StreamId);

        Assert.True(completed.Accepted);
        Assert.Equal(Protocol64ConnectionState.Healthy, container.State);
        Assert.Empty(container.PendingRetransmitPlans);
    }

    [Fact]
    public void SecondFaultAfterCompletedRepairEscalatesToProtocolError()
    {
        using var container = new Protocol64QuicConnectionContainer(405);
        var original = container.SelectStream(new(
            ChannelType.State,
            Protocol64DeliveryKind.ReliableUnordered,
            0));
        var fault = CreateFault(405, original.StreamId, ChannelType.State);
        var first = container.ReportTransportFault(fault);
        var plan = container.PendingRetransmitPlans.Single();

        Assert.True(container.MarkRepairCompleted(
            first.RepairRequest!.RequestId,
            plan.ReopenedStream.StreamId).Accepted);

        var second = container.ReportTransportFault(fault with
        {
            StreamId = plan.ReopenedStream.StreamId,
            Message = "the reopened stream failed again",
        });

        Assert.True(second.Accepted);
        Assert.True(second.RequiresDisconnect);
        Assert.Equal(Protocol64ConnectionState.ProtocolError, container.State);
        Assert.Equal(Protocol64StreamState.ProtocolError, second.Transition.CurrentStreamState);
        Assert.Empty(container.PendingRetransmitPlans);
    }

    [Fact]
    public void PeerRetransmitStreamPreservesTheOriginalLogicalLane()
    {
        using var container = new Protocol64QuicConnectionContainer(407);

        var recovery = container.AllocatePeerRetransmitStream(
            ChannelType.GameplayEvents,
            Protocol64DeliveryKind.ReliableUnordered,
            lane: 2);

        Assert.Equal(Protocol64QuicStreamRole.RecoveryRetransmit, recovery.Role);
        Assert.Equal(ChannelType.GameplayEvents, recovery.Channel);
        Assert.Equal(Protocol64DeliveryKind.ReliableUnordered, recovery.Delivery);
        Assert.Equal(2, recovery.Lane);
        Assert.True(recovery.StreamId >= 0);
    }

    private static Protocol64OutboundFrame CreateFrame(
        ulong connectionEpoch,
        ulong frameId,
        Protocol64DeliveryDescriptor delivery,
        string? replacementKey = null)
    {
        var header = new Protocol64FrameHeader(
            Protocol64.Version,
            Protocol64FrameFlags.None,
            SchemaId: 1,
            SchemaRevision: 1,
            ConnectionEpoch: connectionEpoch,
            FrameId: frameId,
            EncodedBodyLength: 0,
            DecodedBodyLength: 0);
        var payload = new byte[Protocol64FrameHeader.EncodedSize];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(
            payload,
            Protocol64FrameHeader.Magic);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4), header.ProtocolVersion);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(6), (ushort)header.Flags);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(8), header.SchemaId);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(10), header.SchemaRevision);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(12), header.ConnectionEpoch);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(20), header.FrameId);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(28), header.EncodedBodyLength);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(32), header.DecodedBodyLength);
        header = header with { Integrity = Protocol64FrameCodec.ComputeIntegrity(payload) };
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(36), header.Integrity);
        return new(payload, header, delivery, replacementKey);
    }

    private static Protocol64TransportFault CreateFault(
        ulong connectionEpoch,
        int streamId,
        ChannelType channel)
        => new(
            Protocol64TransportFaultKind.StreamReset,
            Protocol64TransportFaultScope.Stream,
            "stream reset",
            connectionEpoch,
            streamId,
            channel,
            Protocol64DeliveryKind.ReliableUnordered,
            StreamSequence: 4,
            FrameId: 22,
            CompleteFrameDelivered: false);
}
