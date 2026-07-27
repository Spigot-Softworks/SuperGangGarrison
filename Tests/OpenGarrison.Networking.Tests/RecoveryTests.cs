using OpenGarrison.Networking;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.Networking.Tests;

public sealed class RecoveryTests
{
    [Fact]
    public void StreamRepairMovesThroughReopenAndRetransmitStates()
    {
        using var container = new Protocol64ConnectionContainer(connectionEpoch: 77);
        var fault = new Protocol64TransportFault(
            Protocol64TransportFaultKind.StreamReset,
            Protocol64TransportFaultScope.Stream,
            "stream reset",
            77,
            4,
            ChannelType.State,
            Protocol64DeliveryKind.ReliableUnordered,
            8,
            100,
            CompleteFrameDelivered: false);

        var requested = container.ReportTransportFault(fault);

        Assert.True(requested.Accepted);
        Assert.Equal(Protocol64ConnectionState.Recovering, container.State);
        Assert.NotNull(requested.RepairRequest);
        Assert.Equal(Protocol64StreamState.RepairRequested, requested.Transition.CurrentStreamState);

        var reopened = container.MarkStreamReopened(4);
        Assert.True(reopened.Accepted);
        Assert.Equal(Protocol64StreamState.AwaitingRetransmit, reopened.Transition.CurrentStreamState);

        var completed = container.MarkRepairCompleted(requested.RepairRequest!.RequestId, 4);

        Assert.True(completed.Accepted);
        Assert.Equal(Protocol64ConnectionState.Healthy, container.State);
        Assert.Equal(Protocol64StreamState.Healthy, completed.Transition.CurrentStreamState);
    }

    [Fact]
    public void ASecondFaultBeforeRepairEscalatesToProtocolError()
    {
        using var container = new Protocol64ConnectionContainer(connectionEpoch: 78);
        var fault = CreateFault(78, 9, ChannelType.GameplayEvents);

        Assert.True(container.ReportTransportFault(fault).Accepted);
        var escalation = container.ReportTransportFault(fault with
        {
            Kind = Protocol64TransportFaultKind.StreamClosed,
            Message = "stream failed again",
        });

        Assert.True(escalation.Accepted);
        Assert.True(escalation.RequiresDisconnect);
        Assert.Equal(Protocol64ConnectionState.ProtocolError, container.State);
        Assert.Equal(Protocol64StreamState.ProtocolError, escalation.Transition.CurrentStreamState);
    }

    [Fact]
    public void ControlStreamFaultRequiresConnectionProtocolError()
    {
        using var container = new Protocol64ConnectionContainer(connectionEpoch: 79);
        var result = container.ReportTransportFault(CreateFault(79, 1, ChannelType.Control));

        Assert.True(result.Accepted);
        Assert.True(result.RequiresDisconnect);
        Assert.Equal(Protocol64ConnectionState.ProtocolError, container.State);
        Assert.Null(result.RepairRequest);
    }

    private static Protocol64TransportFault CreateFault(
        ulong epoch,
        int streamId,
        ChannelType channel)
        => new(
            Protocol64TransportFaultKind.StreamReset,
            Protocol64TransportFaultScope.Stream,
            "stream reset",
            epoch,
            streamId,
            channel,
            Protocol64DeliveryKind.ReliableOrdered,
            3,
            20,
            CompleteFrameDelivered: false);
}
