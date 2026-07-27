using System.IO;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class Protocol64FoundationTests
{
    [Fact]
    public void AnnotatedSchemaDeliveryIsExposedByRegistry()
    {
        var registry = CreateRegistry();
        var schema = registry.Get<Protocol64TestEvent>();

        Assert.Equal(new Protocol64SchemaKey(100, 1), schema.Descriptor.Key);
        Assert.Equal(Protocol64Direction.ServerToClient, schema.Descriptor.Direction);
        Assert.Equal(Protocol64DeliveryKind.ReliableUnordered, schema.Descriptor.Delivery.Kind);
        Assert.Equal(ChannelType.State, schema.Descriptor.Delivery.Channel);

        var lastWins = Protocol64DeliveryMetadata.GetDescriptor<Protocol64LastWinsSchema>();
        Assert.Equal(Protocol64DeliveryKind.LastWins, lastWins.Kind);
        Assert.Null(lastWins.Channel);

        var ordered = Protocol64DeliveryMetadata.GetDescriptor<Protocol64ReliableOrderedSchema>();
        Assert.Equal(Protocol64DeliveryKind.ReliableOrdered, ordered.Kind);
        Assert.Equal(ChannelType.Control, ordered.Channel);
    }

    [Fact]
    public void CompleteFrameRoundTripsWithLz4AndTypedResult()
    {
        var registry = CreateRegistry();
        var value = new Protocol64TestEvent(new string('A', 1_000));

        var encoded = Protocol64FrameCodec.Encode(
            registry,
            value,
            connectionEpoch: 7,
            frameId: 42,
            options: new Protocol64FrameEncodeOptions
            {
                Compression = Protocol64Compression.Lz4,
                CompressionThresholdBytes = 1,
            });

        Assert.True(encoded.Succeeded, encoded.Fault?.Message);
        Assert.Equal(Protocol64.Version, encoded.Header!.ProtocolVersion);
        Assert.Equal(7UL, encoded.Header.ConnectionEpoch);
        Assert.Equal(42UL, encoded.Header.FrameId);

        var decoded = Protocol64FrameCodec.Decode<Protocol64TestEvent>(
            encoded.Payload!,
            registry);

        Assert.True(decoded.Succeeded, decoded.Fault?.Message);
        Assert.Equal(value, decoded.Event);
    }

    [Fact]
    public void TruncatedFrameReturnsTypedFaultAndMetadata()
    {
        var registry = CreateRegistry();
        var encoded = Encode(new Protocol64TestEvent("hello"), registry);
        var truncated = encoded[..^1];

        var result = Protocol64FrameCodec.Decode(truncated, registry);

        Assert.False(result.Succeeded);
        Assert.Equal(Protocol64FaultKind.TruncatedFrame, result.Fault!.Kind);
        Assert.False(result.Fault.Metadata.CompleteFrameDelivered);
    }

    [Fact]
    public void OversizedDeclaredLengthIsRejectedBeforeBodyRead()
    {
        var registry = CreateRegistry();
        var encoded = Encode(new Protocol64TestEvent("hello"), registry);
        var header = encoded.ToArray();
        BitConverter.GetBytes(uint.MaxValue).CopyTo(header, 28);

        var result = Protocol64FrameCodec.Decode(
            header,
            registry,
            new Protocol64FrameDecodeOptions
            {
                Limits = new Protocol64FrameLimits
                {
                    MaxEnvelopeBytes = 256,
                    MaxEncodedBodyBytes = 128,
                    MaxDecodedBodyBytes = 128,
                },
            });

        Assert.False(result.Succeeded);
        Assert.Equal(Protocol64FaultKind.OversizedLength, result.Fault!.Kind);
    }

    [Fact]
    public void TrailingEnvelopeBytesAreRejected()
    {
        var registry = CreateRegistry();
        var encoded = Encode(new Protocol64TestEvent("hello"), registry);
        var withTrailingBytes = encoded.Concat(new byte[] { 0xA5, 0x5A }).ToArray();

        var result = Protocol64FrameCodec.Decode(withTrailingBytes, registry);

        Assert.False(result.Succeeded);
        Assert.Equal(Protocol64FaultKind.TrailingBytes, result.Fault!.Kind);
    }

    [Fact]
    public void TrailingSchemaBodyBytesAreRejected()
    {
        var registry = CreateRegistry();
        var encoded = Encode(new Protocol64TestEvent("hello"), registry);
        var bodyLength = BitConverter.ToUInt32(encoded, 28);
        var expanded = new byte[encoded.Length + 1];
        encoded.CopyTo(expanded, 0);
        expanded[^1] = 0xFF;
        BitConverter.GetBytes(bodyLength + 1).CopyTo(expanded, 28);
        BitConverter.GetBytes(7U).CopyTo(expanded, 32);

        var result = Protocol64FrameCodec.Decode(expanded, registry);

        Assert.False(result.Succeeded);
        Assert.Equal(Protocol64FaultKind.TrailingBodyBytes, result.Fault!.Kind);
        Assert.Equal(Protocol64Direction.ServerToClient, result.Fault.Metadata.Direction);
        Assert.IsType<MalformedS2CException>(result.GetMalformedS2CException());
    }

    [Fact]
    public void UnknownSchemaIsReportedWithoutInvokingAHandler()
    {
        var registry = CreateRegistry();
        var encoded = Encode(new Protocol64TestEvent("hello"), registry);
        BitConverter.GetBytes((ushort)999).CopyTo(encoded, 8);

        var result = Protocol64FrameCodec.Decode(encoded, registry);

        Assert.False(result.Succeeded);
        Assert.Equal(Protocol64FaultKind.UnknownSchema, result.Fault!.Kind);
        Assert.Equal((ushort)999, result.Fault.Metadata.SchemaId);
    }

    [Fact]
    public void SchemaBodyTruncationIsDistinctFromEnvelopeTruncation()
    {
        var registry = CreateRegistry();
        var encoded = Encode(new Protocol64TestEvent("hello"), registry);
        var bodyLength = BitConverter.ToUInt32(encoded, 28);
        var bodyTruncated = encoded[..^1];
        BitConverter.GetBytes(bodyLength - 1).CopyTo(bodyTruncated, 28);

        var result = Protocol64FrameCodec.Decode(bodyTruncated, registry);

        Assert.False(result.Succeeded);
        Assert.Equal(Protocol64FaultKind.TruncatedBody, result.Fault!.Kind);
    }

    private static Protocol64SchemaRegistry CreateRegistry()
    {
        var registry = new Protocol64SchemaRegistry();
        registry.Register(new Protocol64TestSchema());
        registry.Register(new Protocol64LastWinsSchema());
        registry.Register(new Protocol64ReliableOrderedSchema());
        return registry;
    }

    private static byte[] Encode(Protocol64TestEvent value, Protocol64SchemaRegistry registry)
        => Protocol64FrameCodec.Encode(
            registry,
            value,
            connectionEpoch: 1,
            frameId: 1,
            options: new Protocol64FrameEncodeOptions
            {
                Compression = Protocol64Compression.None,
            }).Payload!;

    private sealed record Protocol64TestEvent(string Value);

    [ReliableUnordered(ChannelType.State)]
    private sealed class Protocol64TestSchema : Protocol64EventSchema<Protocol64TestEvent>
    {
        public Protocol64TestSchema()
            : base(100, 1, Protocol64Direction.ServerToClient, maxBodyBytes: 2_048)
        {
        }

        public override void WriteBody(Protocol64TestEvent eventValue, BinaryWriter writer)
            => writer.Write(eventValue.Value);

        public override Protocol64TestEvent ReadBody(BinaryReader reader)
            => new(reader.ReadString());
    }

    private sealed record Protocol64LastWinsEvent(int Value);

    [LastWins]
    private sealed class Protocol64LastWinsSchema : Protocol64EventSchema<Protocol64LastWinsEvent>
    {
        public Protocol64LastWinsSchema()
            : base(101, 1, Protocol64Direction.ServerToClient, maxBodyBytes: 32)
        {
        }

        public override void WriteBody(Protocol64LastWinsEvent eventValue, BinaryWriter writer)
            => writer.Write(eventValue.Value);

        public override Protocol64LastWinsEvent ReadBody(BinaryReader reader)
            => new(reader.ReadInt32());
    }

    private sealed record Protocol64ReliableOrderedEvent(byte Value);

    [ReliableOrdered(ChannelType.Control)]
    private sealed class Protocol64ReliableOrderedSchema
        : Protocol64EventSchema<Protocol64ReliableOrderedEvent>
    {
        public Protocol64ReliableOrderedSchema()
            : base(102, 1, Protocol64Direction.ClientToServer, maxBodyBytes: 1)
        {
        }

        public override void WriteBody(Protocol64ReliableOrderedEvent eventValue, BinaryWriter writer)
            => writer.Write(eventValue.Value);

        public override Protocol64ReliableOrderedEvent ReadBody(BinaryReader reader)
            => new(reader.ReadByte());
    }
}
