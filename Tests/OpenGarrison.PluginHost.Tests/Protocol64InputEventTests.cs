using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class Protocol64InputEventTests
{
    [Fact]
    public void InputCommandRoundTripsAsReliableOrderedInput()
    {
        var registry = new Protocol64SchemaRegistry();
        registry.Register(new Protocol64InputCommandSchema());
        var value = new Protocol64InputCommand(
            CommandId: 9,
            InputSequence: 44,
            Kind: Protocol64InputCommandKind.Jump,
            HeldButtons: InputButtons.Right,
            AimRelX: 12.5f,
            AimRelY: -3.25f,
            ClientTick: 101,
            CommandSequence: 8);

        var schema = registry.Get<Protocol64InputCommand>();
        Assert.Equal(Protocol64DeliveryKind.ReliableOrdered, schema.Descriptor.Delivery.Kind);
        Assert.Equal(ChannelType.Input, schema.Descriptor.Delivery.Channel);

        var encoded = Protocol64FrameCodec.Encode(registry, value, 1, 1).Payload!;
        var decoded = Protocol64FrameCodec.Decode<Protocol64InputCommand>(encoded, registry);

        Assert.True(decoded.Succeeded, decoded.Fault?.Message);
        Assert.Equal(value, decoded.Event);
    }

    [Fact]
    public void InvalidInputCommandIsRejectedBySchemaValidation()
    {
        var registry = new Protocol64SchemaRegistry();
        registry.Register(new Protocol64InputCommandSchema());
        var value = new Protocol64InputCommand(
            CommandId: 0,
            InputSequence: 1,
            Kind: Protocol64InputCommandKind.Jump,
            HeldButtons: InputButtons.None,
            AimRelX: 0,
            AimRelY: 0);

        var encoded = Protocol64FrameCodec.Encode(registry, value, 1, 1);

        Assert.False(encoded.Succeeded);
        Assert.Equal(Protocol64FaultKind.ValidationFailed, encoded.Fault!.Kind);
    }

    [Fact]
    public void InputResultRoundTripsAndRejectsUnknownResult()
    {
        var registry = new Protocol64SchemaRegistry();
        registry.Register(new Protocol64InputCommandResultSchema());
        var value = new Protocol64InputCommandResult(
            CommandId: 9,
            InputSequence: 44,
            Result: Protocol64InputCommandResultKind.Consumed,
            ServerTick: 555,
            CommandSequence: 8);

        var encoded = Protocol64FrameCodec.Encode(registry, value, 2, 3).Payload!;
        var decoded = Protocol64FrameCodec.Decode<Protocol64InputCommandResult>(encoded, registry);

        Assert.True(decoded.Succeeded, decoded.Fault?.Message);
        Assert.Equal(value, decoded.Event);
    }

    [Fact]
    public void InputResultAcknowledgementIsReliableOrderedOnInput()
    {
        var registry = new Protocol64SchemaRegistry();
        registry.Register(new Protocol64InputCommandResultAckSchema());
        var schema = registry.Get<Protocol64InputCommandResultAck>();

        Assert.Equal(Protocol64DeliveryKind.ReliableOrdered, schema.Descriptor.Delivery.Kind);
        Assert.Equal(ChannelType.Input, schema.Descriptor.Delivery.Channel);

        var encoded = Protocol64FrameCodec.Encode(
            registry,
            new Protocol64InputCommandResultAck(91),
            connectionEpoch: 1,
            frameId: 1).Payload!;
        var decoded = Protocol64FrameCodec.Decode<Protocol64InputCommandResultAck>(encoded, registry);

        Assert.True(decoded.Succeeded, decoded.Fault?.Message);
        Assert.Equal(91UL, decoded.Event!.CommandId);
    }
}
