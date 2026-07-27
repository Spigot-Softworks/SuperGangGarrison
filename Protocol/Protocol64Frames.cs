using System;
using System.Buffers.Binary;
using System.IO;
using K4os.Compression.LZ4;

namespace OpenGarrison.Protocol;

public enum Protocol64Compression : byte
{
    None = 0,
    Lz4 = 1,
}

[Flags]
public enum Protocol64FrameFlags : ushort
{
    None = 0,
    Lz4 = 1 << 0,
}

public sealed record Protocol64FrameLimits
{
    public const int DefaultMaxEnvelopeBytes = 1 * 1024 * 1024;
    public const int DefaultMaxEncodedBodyBytes = DefaultMaxEnvelopeBytes - Protocol64FrameHeader.EncodedSize;
    public const int DefaultMaxDecodedBodyBytes = 4 * 1024 * 1024;

    public int MaxEnvelopeBytes { get; init; } = DefaultMaxEnvelopeBytes;

    public int MaxEncodedBodyBytes { get; init; } = DefaultMaxEncodedBodyBytes;

    public int MaxDecodedBodyBytes { get; init; } = DefaultMaxDecodedBodyBytes;

    public static Protocol64FrameLimits Default { get; } = new();

    internal Protocol64Fault? Validate()
    {
        if (MaxEnvelopeBytes < Protocol64FrameHeader.EncodedSize ||
            MaxEncodedBodyBytes < 0 ||
            MaxDecodedBodyBytes < 0 ||
            MaxEnvelopeBytes - Protocol64FrameHeader.EncodedSize < MaxEncodedBodyBytes)
        {
            return new Protocol64Fault(
                Protocol64FaultKind.InvalidArgument,
                "Protocol-64 frame limits are inconsistent.",
                Protocol64FaultMetadata.Empty);
        }

        return null;
    }
}

public sealed record Protocol64FrameEncodeOptions
{
    public Protocol64Compression Compression { get; init; } = Protocol64Compression.Lz4;

    public int CompressionThresholdBytes { get; init; } = 256;

    public Protocol64FrameLimits Limits { get; init; } = Protocol64FrameLimits.Default;

    public string? Backend { get; init; }
}

public sealed record Protocol64FrameDecodeOptions
{
    public Protocol64FrameLimits Limits { get; init; } = Protocol64FrameLimits.Default;

    public string? Backend { get; init; }

    public int? StreamId { get; init; }

    public IProtocol64FaultSink? FaultSink { get; init; }
}

public sealed record Protocol64FrameHeader(
    ushort ProtocolVersion,
    Protocol64FrameFlags Flags,
    ushort SchemaId,
    ushort SchemaRevision,
    ulong ConnectionEpoch,
    ulong FrameId,
    uint EncodedBodyLength,
    uint DecodedBodyLength)
{
    public const uint Magic = 0x3432474F; // "OG24" in little-endian byte order.
    public const int EncodedSize = 36;

    public Protocol64Compression Compression =>
        (Flags & Protocol64FrameFlags.Lz4) != 0
            ? Protocol64Compression.Lz4
            : Protocol64Compression.None;

    public Protocol64FrameMetadata ToMetadata(
        string? backend = null,
        int? streamId = null,
        bool completeFrameDelivered = true)
        => new(
            ProtocolVersion,
            Flags,
            SchemaId,
            SchemaRevision,
            ConnectionEpoch,
            FrameId,
            EncodedBodyLength,
            DecodedBodyLength,
            backend,
            streamId,
            completeFrameDelivered);
}

public sealed record Protocol64FrameMetadata(
    ushort ProtocolVersion,
    Protocol64FrameFlags Flags,
    ushort SchemaId,
    ushort SchemaRevision,
    ulong ConnectionEpoch,
    ulong FrameId,
    uint EncodedBodyLength,
    uint DecodedBodyLength,
    string? Backend,
    int? StreamId,
    bool CompleteFrameDelivered)
{
    public Protocol64Compression Compression =>
        (Flags & Protocol64FrameFlags.Lz4) != 0
            ? Protocol64Compression.Lz4
            : Protocol64Compression.None;
}

public sealed record Protocol64FrameEncodeResult(
    bool Succeeded,
    byte[]? Payload,
    Protocol64FrameHeader? Header,
    Protocol64Fault? Fault)
{
    public static Protocol64FrameEncodeResult Success(byte[] payload, Protocol64FrameHeader header)
        => new(true, payload, header, null);

    public static Protocol64FrameEncodeResult Failure(Protocol64Fault fault)
        => new(false, null, null, fault);
}

public sealed record Protocol64FrameDecodeResult(
    bool Succeeded,
    object? Event,
    IProtocol64EventSchema? Schema,
    Protocol64FrameHeader? Header,
    Protocol64Fault? Fault)
{
    public static Protocol64FrameDecodeResult Success(
        object value,
        IProtocol64EventSchema schema,
        Protocol64FrameHeader header)
        => new(true, value, schema, header, null);

    public static Protocol64FrameDecodeResult Failure(
        Protocol64Fault fault,
        Protocol64FrameHeader? header = null,
        IProtocol64EventSchema? schema = null)
        => new(false, null, schema, header, fault);

    public MalformedS2CException? GetMalformedS2CException()
        => Fault?.IsServerToClient == true ? Fault.ToMalformedS2CException() : null;
}

public sealed record Protocol64FrameDecodeResult<TEvent>(
    bool Succeeded,
    TEvent? Event,
    Protocol64FrameHeader? Header,
    Protocol64Fault? Fault)
{
    public static Protocol64FrameDecodeResult<TEvent> Success(
        TEvent value,
        Protocol64FrameHeader header)
        => new(true, value, header, null);

    public static Protocol64FrameDecodeResult<TEvent> Failure(
        Protocol64Fault fault,
        Protocol64FrameHeader? header = null)
        => new(false, default, header, fault);

    public MalformedS2CException? GetMalformedS2CException()
        => Fault?.IsServerToClient == true ? Fault.ToMalformedS2CException() : null;
}

public static class Protocol64FrameCodec
{
    public static Protocol64FrameEncodeResult Encode<TEvent>(
        Protocol64SchemaRegistry registry,
        TEvent eventValue,
        ulong connectionEpoch,
        ulong frameId,
        Protocol64FrameEncodeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        options ??= new Protocol64FrameEncodeOptions();

        var limits = options.Limits ?? Protocol64FrameLimits.Default;
        var limitFault = limits.Validate();
        if (limitFault is not null)
        {
            return Protocol64FrameEncodeResult.Failure(limitFault);
        }

        if (options.CompressionThresholdBytes < 0)
        {
            return Protocol64FrameEncodeResult.Failure(CreateFault(
                Protocol64FaultKind.InvalidArgument,
                "Compression threshold cannot be negative.",
                options.Backend));
        }

        IProtocol64EventSchema schema;
        try
        {
            schema = registry.Get<TEvent>();
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            return Protocol64FrameEncodeResult.Failure(CreateFault(
                Protocol64FaultKind.UnknownSchema,
                $"No protocol-64 schema is registered for event type {typeof(TEvent).FullName}.",
                options.Backend,
                ex));
        }

        var bodyResult = schema.EncodeObject(eventValue!);
        if (!bodyResult.Succeeded)
        {
            return Protocol64FrameEncodeResult.Failure(bodyResult.Fault!);
        }

        var decodedBody = bodyResult.Body!;
        if (decodedBody.Length > limits.MaxDecodedBodyBytes ||
            decodedBody.Length > schema.Descriptor.MaxBodyBytes)
        {
            return Protocol64FrameEncodeResult.Failure(CreateFault(
                Protocol64FaultKind.BodyTooLarge,
                $"Decoded body length {decodedBody.Length} exceeds the configured protocol-64 limit.",
                options.Backend,
                direction: schema.Descriptor.Direction,
                schema: schema,
                decodedBodyBytes: decodedBody.Length));
        }

        var encodedBody = decodedBody;
        var flags = Protocol64FrameFlags.None;
        if (options.Compression == Protocol64Compression.Lz4 &&
            decodedBody.Length >= options.CompressionThresholdBytes)
        {
            try
            {
                var compressed = LZ4Pickler.Pickle(decodedBody, LZ4Level.L00_FAST);
                if (compressed.Length < decodedBody.Length)
                {
                    encodedBody = compressed;
                    flags |= Protocol64FrameFlags.Lz4;
                }
            }
            catch (Exception ex)
            {
                return Protocol64FrameEncodeResult.Failure(CreateFault(
                    Protocol64FaultKind.CompressionFailed,
                    "Protocol-64 LZ4 compression failed.",
                    options.Backend,
                    direction: schema.Descriptor.Direction,
                    schema: schema,
                    exception: ex,
                    decodedBodyBytes: decodedBody.Length));
            }
        }

        if (encodedBody.Length > limits.MaxEncodedBodyBytes ||
            Protocol64FrameHeader.EncodedSize > limits.MaxEnvelopeBytes - encodedBody.Length)
        {
            return Protocol64FrameEncodeResult.Failure(CreateFault(
                Protocol64FaultKind.OversizedLength,
                $"Encoded frame length {encodedBody.Length + Protocol64FrameHeader.EncodedSize} exceeds the configured limit.",
                options.Backend,
                direction: schema.Descriptor.Direction,
                schema: schema,
                encodedBodyBytes: encodedBody.Length,
                decodedBodyBytes: decodedBody.Length));
        }

        var header = new Protocol64FrameHeader(
            ProtocolVersion: Protocol64.Version,
            Flags: flags,
            SchemaId: schema.Descriptor.Key.SchemaId,
            SchemaRevision: schema.Descriptor.Key.Revision,
            ConnectionEpoch: connectionEpoch,
            FrameId: frameId,
            EncodedBodyLength: checked((uint)encodedBody.Length),
            DecodedBodyLength: checked((uint)decodedBody.Length));

        var payload = new byte[Protocol64FrameHeader.EncodedSize + encodedBody.Length];
        WriteHeader(payload, header);
        encodedBody.CopyTo(payload, Protocol64FrameHeader.EncodedSize);
        return Protocol64FrameEncodeResult.Success(payload, header);
    }

    public static Protocol64FrameDecodeResult Decode(
        ReadOnlyMemory<byte> payload,
        Protocol64SchemaRegistry registry,
        Protocol64FrameDecodeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        options ??= new Protocol64FrameDecodeOptions();

        var limits = options.Limits ?? Protocol64FrameLimits.Default;
        var limitFault = limits.Validate();
        if (limitFault is not null)
        {
            return Report(Protocol64FrameDecodeResult.Failure(limitFault), options.FaultSink);
        }

        if (payload.Length < Protocol64FrameHeader.EncodedSize)
        {
            return Report(Protocol64FrameDecodeResult.Failure(CreateFault(
                Protocol64FaultKind.TruncatedFrame,
                $"Protocol-64 frame contains {payload.Length} bytes; header requires {Protocol64FrameHeader.EncodedSize}.",
                options.Backend,
                streamId: options.StreamId,
                encodedBodyBytes: Math.Max(0, payload.Length - Protocol64FrameHeader.EncodedSize))), options.FaultSink);
        }

        Protocol64FrameHeader header;
        try
        {
            header = ReadHeader(payload.Span);
        }
        catch (Protocol64FrameParseException ex)
        {
            return Report(Protocol64FrameDecodeResult.Failure(CreateFault(
                ex.Kind,
                ex.Message,
                options.Backend,
                streamId: options.StreamId,
                exception: ex)), options.FaultSink);
        }

        if (header.ProtocolVersion != Protocol64.Version)
        {
            return Report(Protocol64FrameDecodeResult.Failure(CreateFault(
                Protocol64FaultKind.UnsupportedVersion,
                $"Protocol-64 frame declares version {header.ProtocolVersion}; expected {Protocol64.Version}.",
                options.Backend,
                streamId: options.StreamId,
                header: header,
                completeFrameDelivered: false)), options.FaultSink);
        }

        if ((header.Flags & ~Protocol64FrameFlags.Lz4) != Protocol64FrameFlags.None)
        {
            return Report(Protocol64FrameDecodeResult.Failure(CreateFault(
                Protocol64FaultKind.InvalidFlags,
                $"Protocol-64 frame declares unsupported flags 0x{(ushort)header.Flags:X4}.",
                options.Backend,
                streamId: options.StreamId,
                header: header)), options.FaultSink);
        }

        if (header.EncodedBodyLength > limits.MaxEncodedBodyBytes ||
            header.DecodedBodyLength > limits.MaxDecodedBodyBytes)
        {
            return Report(Protocol64FrameDecodeResult.Failure(CreateFault(
                Protocol64FaultKind.OversizedLength,
                "Protocol-64 frame body length exceeds configured limits.",
                options.Backend,
                streamId: options.StreamId,
                header: header)), options.FaultSink);
        }

        var expectedLength = (long)Protocol64FrameHeader.EncodedSize + header.EncodedBodyLength;
        if (expectedLength > limits.MaxEnvelopeBytes)
        {
            return Report(Protocol64FrameDecodeResult.Failure(CreateFault(
                Protocol64FaultKind.OversizedLength,
                $"Protocol-64 envelope length {expectedLength} exceeds limit {limits.MaxEnvelopeBytes}.",
                options.Backend,
                streamId: options.StreamId,
                header: header)), options.FaultSink);
        }

        if (payload.Length < expectedLength)
        {
            return Report(Protocol64FrameDecodeResult.Failure(CreateFault(
                Protocol64FaultKind.TruncatedFrame,
                $"Protocol-64 frame declares {header.EncodedBodyLength} body bytes but only {payload.Length - Protocol64FrameHeader.EncodedSize} arrived.",
                options.Backend,
                streamId: options.StreamId,
                header: header,
                completeFrameDelivered: false)), options.FaultSink);
        }

        if (payload.Length > expectedLength)
        {
            return Report(Protocol64FrameDecodeResult.Failure(CreateFault(
                Protocol64FaultKind.TrailingBytes,
                $"Protocol-64 frame has {payload.Length - expectedLength} trailing bytes after its declared body.",
                options.Backend,
                streamId: options.StreamId,
                header: header)), options.FaultSink);
        }

        if (!registry.TryGet(header.SchemaId, header.SchemaRevision, out var schema) || schema is null)
        {
            return Report(Protocol64FrameDecodeResult.Failure(CreateFault(
                Protocol64FaultKind.UnknownSchema,
                $"Protocol-64 schema ({header.SchemaId}, {header.SchemaRevision}) is not registered.",
                options.Backend,
                streamId: options.StreamId,
                header: header)), options.FaultSink);
        }

        var encodedBody = payload.Slice(Protocol64FrameHeader.EncodedSize, checked((int)header.EncodedBodyLength));
        byte[] decodedBody;
        try
        {
            decodedBody = header.Compression switch
            {
                Protocol64Compression.None => encodedBody.ToArray(),
                Protocol64Compression.Lz4 => LZ4Pickler.Unpickle(encodedBody.ToArray()),
                _ => throw new InvalidDataException("Unknown protocol-64 compression encoding."),
            };
        }
        catch (Exception ex)
        {
            return Report(Protocol64FrameDecodeResult.Failure(CreateFault(
                Protocol64FaultKind.CompressionFailed,
                "Protocol-64 body decompression failed.",
                options.Backend,
                streamId: options.StreamId,
                header: header,
                schema: schema,
                exception: ex)), options.FaultSink);
        }

        if (decodedBody.Length != header.DecodedBodyLength)
        {
            return Report(Protocol64FrameDecodeResult.Failure(CreateFault(
                Protocol64FaultKind.DecodedLengthMismatch,
                $"Protocol-64 body decoded to {decodedBody.Length} bytes; header declares {header.DecodedBodyLength}.",
                options.Backend,
                streamId: options.StreamId,
                header: header,
                schema: schema,
                decodedBodyBytes: decodedBody.Length)), options.FaultSink);
        }

        var metadata = header.ToMetadata(options.Backend, options.StreamId, completeFrameDelivered: true);
        var schemaResult = schema.Decode(decodedBody, metadata);
        if (!schemaResult.Succeeded)
        {
            return Report(Protocol64FrameDecodeResult.Failure(
                schemaResult.Fault!,
                header,
                schema), options.FaultSink);
        }

        return Protocol64FrameDecodeResult.Success(schemaResult.Event!, schema, header);
    }

    public static Protocol64FrameDecodeResult<TEvent> Decode<TEvent>(
        ReadOnlyMemory<byte> payload,
        Protocol64SchemaRegistry registry,
        Protocol64FrameDecodeOptions? options = null)
    {
        var untyped = Decode(payload, registry, options);
        if (!untyped.Succeeded)
        {
            return Protocol64FrameDecodeResult<TEvent>.Failure(untyped.Fault!, untyped.Header);
        }

        if (untyped.Event is not TEvent typedEvent)
        {
            var fault = CreateFault(
                Protocol64FaultKind.InvalidBody,
                $"Protocol-64 schema returned {untyped.Event?.GetType().FullName}, not {typeof(TEvent).FullName}.",
                options?.Backend,
                header: untyped.Header,
                schema: untyped.Schema);
            Report(fault, options?.FaultSink);
            return Protocol64FrameDecodeResult<TEvent>.Failure(fault, untyped.Header);
        }

        return Protocol64FrameDecodeResult<TEvent>.Success(typedEvent, untyped.Header!);
    }

    private static Protocol64FrameDecodeResult Report(
        Protocol64FrameDecodeResult result,
        IProtocol64FaultSink? sink)
    {
        if (result.Fault is not null)
        {
            sink?.Report(result.Fault);
        }

        return result;
    }

    private static void Report(Protocol64Fault fault, IProtocol64FaultSink? sink)
        => sink?.Report(fault);

    private static Protocol64FrameHeader ReadHeader(ReadOnlySpan<byte> payload)
    {
        if (BinaryPrimitives.ReadUInt32LittleEndian(payload) != Protocol64FrameHeader.Magic)
        {
            throw new Protocol64FrameParseException(
                Protocol64FaultKind.InvalidEnvelope,
                "Protocol-64 frame magic is invalid.");
        }

        return new Protocol64FrameHeader(
            BinaryPrimitives.ReadUInt16LittleEndian(payload[4..]),
            (Protocol64FrameFlags)BinaryPrimitives.ReadUInt16LittleEndian(payload[6..]),
            BinaryPrimitives.ReadUInt16LittleEndian(payload[8..]),
            BinaryPrimitives.ReadUInt16LittleEndian(payload[10..]),
            BinaryPrimitives.ReadUInt64LittleEndian(payload[12..]),
            BinaryPrimitives.ReadUInt64LittleEndian(payload[20..]),
            BinaryPrimitives.ReadUInt32LittleEndian(payload[28..]),
            BinaryPrimitives.ReadUInt32LittleEndian(payload[32..]));
    }

    private static void WriteHeader(Span<byte> payload, Protocol64FrameHeader header)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(payload, Protocol64FrameHeader.Magic);
        BinaryPrimitives.WriteUInt16LittleEndian(payload[4..], header.ProtocolVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(payload[6..], (ushort)header.Flags);
        BinaryPrimitives.WriteUInt16LittleEndian(payload[8..], header.SchemaId);
        BinaryPrimitives.WriteUInt16LittleEndian(payload[10..], header.SchemaRevision);
        BinaryPrimitives.WriteUInt64LittleEndian(payload[12..], header.ConnectionEpoch);
        BinaryPrimitives.WriteUInt64LittleEndian(payload[20..], header.FrameId);
        BinaryPrimitives.WriteUInt32LittleEndian(payload[28..], header.EncodedBodyLength);
        BinaryPrimitives.WriteUInt32LittleEndian(payload[32..], header.DecodedBodyLength);
    }

    private static Protocol64Fault CreateFault(
        Protocol64FaultKind kind,
        string message,
        string? backend,
        Exception? exception = null,
        int? streamId = null,
        Protocol64FrameHeader? header = null,
        IProtocol64EventSchema? schema = null,
        Protocol64Direction? direction = null,
        int encodedBodyBytes = 0,
        int decodedBodyBytes = 0,
        bool? completeFrameDelivered = null)
    {
        return new Protocol64Fault(
            kind,
            message,
            new Protocol64FaultMetadata(
                direction ?? schema?.Descriptor.Direction,
                header?.SchemaId ?? schema?.Descriptor.Key.SchemaId,
                header?.SchemaRevision ?? schema?.Descriptor.Key.Revision,
                header?.ConnectionEpoch,
                header?.FrameId,
                backend,
                streamId,
                completeFrameDelivered ?? header is not null,
                encodedBodyBytes != 0
                    ? encodedBodyBytes
                    : header is null ? 0 : ClampLength(header.EncodedBodyLength),
                decodedBodyBytes != 0
                    ? decodedBodyBytes
                    : header is null ? 0 : ClampLength(header.DecodedBodyLength)),
            exception);
    }

    private static int ClampLength(uint length)
        => length > int.MaxValue ? int.MaxValue : (int)length;
}

internal sealed class Protocol64FrameParseException : Exception
{
    public Protocol64FrameParseException(Protocol64FaultKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }

    public Protocol64FaultKind Kind { get; }
}
