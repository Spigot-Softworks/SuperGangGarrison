using OpenGarrison.Networking;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.Networking.Tests;

public sealed class TelemetryTests
{
    [Fact]
    public void SnapshotContainsDeliveryAndFaultCounters()
    {
        var telemetry = new Protocol64NetworkTelemetry();
        var header = new Protocol64FrameHeader(
            Protocol64.Version,
            Protocol64FrameFlags.None,
            (ushort)Protocol64EventId.ChatRelay,
            1,
            1,
            1,
            0,
            0);
        var delivery = new Protocol64DeliveryDescriptor(
            Protocol64DeliveryKind.ReliableOrdered,
            ChannelType.Chat);

        telemetry.RecordFrameSent(header, delivery, 36);
        telemetry.RecordFrameReceived(header, delivery, 36);
        telemetry.RecordReliableBackpressure();
        telemetry.RecordDecodeFault(new Protocol64Fault(
            Protocol64FaultKind.TruncatedFrame,
            "truncated",
            Protocol64FaultMetadata.Empty));
        telemetry.RecordRepairRequested(stateRepair: true);
        telemetry.RecordInputCommandReceived();
        telemetry.RecordInputCommand(Protocol64InputCommandResultKind.Consumed);

        var snapshot = telemetry.Snapshot();

        Assert.Equal(1, snapshot.FramesSent);
        Assert.Equal(1, snapshot.FramesReceived);
        Assert.Equal(36, snapshot.BytesSent);
        Assert.Equal(36, snapshot.BytesReceived);
        Assert.Equal(1, snapshot.ReliableBackpressure);
        Assert.Equal(1, snapshot.DecodeFaults);
        Assert.Equal(1, snapshot.RepairRequests);
        Assert.Equal(1, snapshot.StateRepairRequests);
        Assert.Equal(1, snapshot.InputCommandsReceived);
        Assert.Equal(1, snapshot.InputCommandsApplied);
        Assert.Equal(2, snapshot.FramesByEvent[new Protocol64TelemetryKey(
            (ushort)Protocol64EventId.ChatRelay,
            ChannelType.Chat,
            Protocol64DeliveryKind.ReliableOrdered)]);
    }
}
