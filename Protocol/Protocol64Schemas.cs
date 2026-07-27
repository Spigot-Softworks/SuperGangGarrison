using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenGarrison.Protocol;

public enum Protocol64Direction : byte
{
    ClientToServer = 1,
    ServerToClient = 2,
    Bidirectional = 3,
}

/// <summary>
/// Stable IDs for the current protocol message families. Concrete protocol-64
/// schemas may use these IDs or allocate new IDs without depending on MessageType.
/// </summary>
public enum Protocol64EventId : ushort
{
    Hello = 1,
    Welcome = 2,
    InputState = 3,
    Snapshot = 4,
    ControlCommand = 5,
    ControlAck = 6,
    ConnectionDenied = 7,
    SessionSlotChanged = 8,
    ServerStatusRequest = 9,
    ServerStatusResponse = 10,
    PasswordRequest = 11,
    PasswordSubmit = 12,
    PasswordResult = 13,
    AutoBalanceNotice = 14,
    ChatSubmit = 15,
    ChatRelay = 16,
    SnapshotAck = 17,
    PlayerProfileUpdate = 18,
    ClientPluginMessage = 19,
    ServerPluginMessage = 20,
    PlayerSocialProfileUpdate = 21,
    ServerDetailsRequest = 22,
    ServerDetailsResponse = 23,
    CustomBubbleUpload = 24,
    CustomBubbleState = 25,
    CustomBubbleClear = 26,
    PingRequest = 27,
    PingResponse = 28,
}

public readonly record struct Protocol64SchemaKey(ushort SchemaId, ushort Revision);

public sealed record Protocol64SchemaDescriptor(
    Protocol64SchemaKey Key,
    Protocol64Direction Direction,
    Type EventType,
    Protocol64DeliveryDescriptor Delivery,
    int MaxBodyBytes);

public sealed record Protocol64SchemaEncodeResult(
    bool Succeeded,
    byte[]? Body,
    Protocol64Fault? Fault)
{
    public static Protocol64SchemaEncodeResult Success(byte[] body)
        => new(true, body, null);

    public static Protocol64SchemaEncodeResult Failure(Protocol64Fault fault)
        => new(false, null, fault);
}

public sealed record Protocol64SchemaDecodeResult(
    bool Succeeded,
    object? Event,
    Protocol64Fault? Fault)
{
    public static Protocol64SchemaDecodeResult Success(object value)
        => new(true, value, null);

    public static Protocol64SchemaDecodeResult Failure(Protocol64Fault fault)
        => new(false, null, fault);
}

public interface IProtocol64EventSchema
{
    Protocol64SchemaDescriptor Descriptor { get; }

    Type EventType { get; }

    Protocol64SchemaEncodeResult EncodeObject(object eventValue);

    Protocol64SchemaDecodeResult Decode(ReadOnlyMemory<byte> body, Protocol64FrameMetadata metadata);
}

public interface IProtocol64EventSchema<TEvent> : IProtocol64EventSchema
{
    Protocol64SchemaEncodeResult Encode(TEvent eventValue);

    Protocol64SchemaDecodeResult<TEvent> DecodeTyped(
        ReadOnlyMemory<byte> body,
        Protocol64FrameMetadata metadata);
}

public sealed record Protocol64SchemaDecodeResult<TEvent>(
    bool Succeeded,
    TEvent? Event,
    Protocol64Fault? Fault)
{
    public static Protocol64SchemaDecodeResult<TEvent> Success(TEvent value)
        => new(true, value, null);

    public static Protocol64SchemaDecodeResult<TEvent> Failure(Protocol64Fault fault)
        => new(false, default, fault);
}

/// <summary>
/// Base class for a dedicated protocol-64 schema. The BinaryReader position check
/// makes trailing body bytes a schema fault instead of silently accepting a prefix.
/// </summary>
public abstract class Protocol64EventSchema<TEvent> : IProtocol64EventSchema<TEvent>
{
    protected Protocol64EventSchema(
        ushort schemaId,
        ushort revision,
        Protocol64Direction direction,
        int maxBodyBytes)
    {
        if (schemaId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaId), "Schema ID zero is reserved.");
        }

        if (revision == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revision), "Schema revision zero is reserved.");
        }

        if (maxBodyBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBodyBytes));
        }

        Descriptor = new Protocol64SchemaDescriptor(
            new Protocol64SchemaKey(schemaId, revision),
            direction,
            typeof(TEvent),
            Protocol64DeliveryMetadata.GetDescriptor(GetType()),
            maxBodyBytes);
    }

    public Protocol64SchemaDescriptor Descriptor { get; }

    public Type EventType => typeof(TEvent);

    public abstract void WriteBody(TEvent eventValue, BinaryWriter writer);

    public abstract TEvent ReadBody(BinaryReader reader);

    /// <summary>
    /// Schema-specific validation runs after the body has been consumed exactly.
    /// </summary>
    public virtual void Validate(TEvent eventValue)
    {
    }

    public Protocol64SchemaEncodeResult Encode(TEvent eventValue)
    {
        if (eventValue is null)
        {
            return Protocol64SchemaEncodeResult.Failure(CreateFault(
                Protocol64FaultKind.InvalidArgument,
                "A protocol-64 event cannot be null.",
                null,
                completeFrameDelivered: false));
        }

        try
        {
            using var backing = new MemoryStream();
            using (var bounded = new Protocol64BoundedWriteStream(backing, Descriptor.MaxBodyBytes))
            using (var writer = new BinaryWriter(bounded, Encoding.UTF8, leaveOpen: true))
            {
                WriteBody(eventValue, writer);
                writer.Flush();
            }

            Validate(eventValue);
            return Protocol64SchemaEncodeResult.Success(backing.ToArray());
        }
        catch (Protocol64LimitExceededException ex)
        {
            return Protocol64SchemaEncodeResult.Failure(CreateFault(
                Protocol64FaultKind.BodyTooLarge,
                ex.Message,
                ex,
                completeFrameDelivered: false));
        }
        catch (Protocol64SchemaValidationException ex)
        {
            return Protocol64SchemaEncodeResult.Failure(CreateFault(
                Protocol64FaultKind.ValidationFailed,
                ex.Message,
                ex,
                completeFrameDelivered: false));
        }
        catch (Exception ex)
        {
            return Protocol64SchemaEncodeResult.Failure(CreateFault(
                Protocol64FaultKind.InvalidBody,
                "The protocol-64 schema could not encode its body.",
                ex,
                completeFrameDelivered: false));
        }
    }

    public Protocol64SchemaEncodeResult EncodeObject(object eventValue)
    {
        if (eventValue is not TEvent typedEvent)
        {
            return Protocol64SchemaEncodeResult.Failure(CreateFault(
                Protocol64FaultKind.InvalidArgument,
                $"Schema {Descriptor.Key} expects event type {typeof(TEvent).FullName}.",
                null,
                completeFrameDelivered: false));
        }

        return Encode(typedEvent);
    }

    public Protocol64SchemaDecodeResult<TEvent> DecodeTyped(
        ReadOnlyMemory<byte> body,
        Protocol64FrameMetadata metadata)
    {
        if (body.Length > Descriptor.MaxBodyBytes)
        {
            return Protocol64SchemaDecodeResult<TEvent>.Failure(CreateFault(
                Protocol64FaultKind.BodyTooLarge,
                $"Decoded body length {body.Length} exceeds schema limit {Descriptor.MaxBodyBytes}.",
                null,
                metadata));
        }

        try
        {
            using var stream = new MemoryStream(body.ToArray(), writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var eventValue = ReadBody(reader);

            if (stream.Position != stream.Length)
            {
                return Protocol64SchemaDecodeResult<TEvent>.Failure(CreateFault(
                    Protocol64FaultKind.TrailingBodyBytes,
                    $"Schema {Descriptor.Key} consumed {stream.Position} of {stream.Length} body bytes.",
                    null,
                    metadata));
            }

            Validate(eventValue);
            return Protocol64SchemaDecodeResult<TEvent>.Success(eventValue);
        }
        catch (Protocol64SchemaValidationException ex)
        {
            return Protocol64SchemaDecodeResult<TEvent>.Failure(CreateFault(
                Protocol64FaultKind.ValidationFailed,
                ex.Message,
                ex,
                metadata));
        }
        catch (EndOfStreamException ex)
        {
            return Protocol64SchemaDecodeResult<TEvent>.Failure(CreateFault(
                Protocol64FaultKind.TruncatedBody,
                "The schema body ended before the event was complete.",
                ex,
                metadata));
        }
        catch (Exception ex)
        {
            return Protocol64SchemaDecodeResult<TEvent>.Failure(CreateFault(
                Protocol64FaultKind.InvalidBody,
                "The schema rejected the event body.",
                ex,
                metadata));
        }
    }

    Protocol64SchemaDecodeResult IProtocol64EventSchema.Decode(
        ReadOnlyMemory<byte> body,
        Protocol64FrameMetadata metadata)
    {
        var result = DecodeTyped(body, metadata);
        return result.Succeeded
            ? Protocol64SchemaDecodeResult.Success(result.Event!)
            : Protocol64SchemaDecodeResult.Failure(result.Fault!);
    }

    private Protocol64Fault CreateFault(
        Protocol64FaultKind kind,
        string message,
        Exception? exception,
        Protocol64FrameMetadata? metadata = null,
        bool completeFrameDelivered = true)
    {
        var faultMetadata = metadata is null
            ? new Protocol64FaultMetadata(
                Descriptor.Direction,
                Descriptor.Key.SchemaId,
                Descriptor.Key.Revision,
                null,
                null,
                null,
                null,
                completeFrameDelivered,
                0,
                0)
            : new Protocol64FaultMetadata(
                Descriptor.Direction,
                Descriptor.Key.SchemaId,
                Descriptor.Key.Revision,
                metadata.ConnectionEpoch,
                metadata.FrameId,
                metadata.Backend,
                metadata.StreamId,
                metadata.CompleteFrameDelivered,
                checked((int)metadata.EncodedBodyLength),
                checked((int)metadata.DecodedBodyLength));

        return new Protocol64Fault(kind, message, faultMetadata, exception);
    }
}

public sealed class Protocol64SchemaValidationException : Exception
{
    public Protocol64SchemaValidationException(string message)
        : base(message)
    {
    }
}

public sealed class Protocol64SchemaRegistry
{
    private readonly Dictionary<Protocol64SchemaKey, IProtocol64EventSchema> _schemas = new();
    private readonly Dictionary<Type, IProtocol64EventSchema> _schemasByType = new();

    public int Count => _schemas.Count;

    public void Register(IProtocol64EventSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        var descriptor = schema.Descriptor;
        if (descriptor.Key.SchemaId == 0 || descriptor.Key.Revision == 0)
        {
            throw new ArgumentException("Protocol-64 schema IDs and revisions must be non-zero.", nameof(schema));
        }

        if (!_schemas.TryAdd(descriptor.Key, schema))
        {
            throw new InvalidOperationException(
                $"Protocol-64 schema {descriptor.Key} is already registered.");
        }

        if (!_schemasByType.TryAdd(schema.EventType, schema))
        {
            _schemas.Remove(descriptor.Key);
            throw new InvalidOperationException(
                $"Event type {schema.EventType.FullName} is already registered in protocol 64.");
        }
    }

    public bool TryGet(
        ushort schemaId,
        ushort revision,
        out IProtocol64EventSchema? schema)
        => _schemas.TryGetValue(new Protocol64SchemaKey(schemaId, revision), out schema);

    public bool TryGet<TEvent>(out IProtocol64EventSchema<TEvent>? schema)
    {
        if (_schemasByType.TryGetValue(typeof(TEvent), out var untyped))
        {
            schema = untyped as IProtocol64EventSchema<TEvent>;
            return schema is not null;
        }

        schema = null;
        return false;
    }

    public IProtocol64EventSchema Get(ushort schemaId, ushort revision)
    {
        if (!TryGet(schemaId, revision, out var schema))
        {
            throw new KeyNotFoundException($"Protocol-64 schema ({schemaId}, {revision}) is not registered.");
        }

        return schema!;
    }

    public IProtocol64EventSchema<TEvent> Get<TEvent>()
    {
        if (!TryGet<TEvent>(out var schema))
        {
            throw new KeyNotFoundException($"Protocol-64 event type {typeof(TEvent).FullName} is not registered.");
        }

        return schema!;
    }
}

internal sealed class Protocol64LimitExceededException : IOException
{
    public Protocol64LimitExceededException(string message)
        : base(message)
    {
    }
}

internal sealed class Protocol64BoundedWriteStream : Stream
{
    private readonly Stream _inner;
    private readonly long _limit;
    private long _written;

    public Protocol64BoundedWriteStream(Stream inner, long limit)
    {
        _inner = inner;
        _limit = limit;
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => _written;
    public override long Position
    {
        get => _written;
        set => throw new NotSupportedException();
    }

    public override void Flush() => _inner.Flush();

    public override void Write(byte[] buffer, int offset, int count)
    {
        EnsureCapacity(count);
        _inner.Write(buffer, offset, count);
        _written += count;
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        EnsureCapacity(buffer.Length);
        _inner.Write(buffer);
        _written += buffer.Length;
    }

    public override void WriteByte(byte value)
    {
        EnsureCapacity(1);
        _inner.WriteByte(value);
        _written++;
    }

    private void EnsureCapacity(int count)
    {
        if (count < 0 || _written > _limit - count)
        {
            throw new Protocol64LimitExceededException(
                $"Schema body exceeds the configured limit of {_limit} bytes.");
        }
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
