using System;

namespace OpenGarrison.Protocol;

public enum Protocol64FaultKind : byte
{
    InvalidArgument = 1,
    InvalidEnvelope = 2,
    UnsupportedVersion = 3,
    InvalidFlags = 4,
    TruncatedFrame = 5,
    OversizedLength = 6,
    TrailingBytes = 7,
    UnknownSchema = 8,
    CompressionFailed = 9,
    DecodedLengthMismatch = 10,
    TruncatedBody = 11,
    InvalidBody = 12,
    TrailingBodyBytes = 13,
    BodyTooLarge = 14,
    ValidationFailed = 15,
    IntegrityMismatch = 16,
}

public sealed record Protocol64FaultMetadata(
    Protocol64Direction? Direction,
    ushort? SchemaId,
    ushort? SchemaRevision,
    ulong? ConnectionEpoch,
    ulong? FrameId,
    string? Backend,
    int? StreamId,
    bool CompleteFrameDelivered,
    int EncodedBodyBytes,
    int DecodedBodyBytes)
{
    public static Protocol64FaultMetadata Empty { get; } = new(
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        false,
        0,
        0);
}

public sealed record Protocol64Fault(
    Protocol64FaultKind Kind,
    string Message,
    Protocol64FaultMetadata Metadata,
    Exception? Exception = null)
{
    public bool IsServerToClient => Metadata.Direction == Protocol64Direction.ServerToClient;

    public MalformedS2CException ToMalformedS2CException()
        => new(this);
}

/// <summary>
/// Non-fatal by default: a complete server-to-client frame was delivered but could
/// not be accepted by the protocol/schema layer.
/// </summary>
public sealed class MalformedS2CException : Exception
{
    public MalformedS2CException(Protocol64Fault fault)
        : base(fault?.Message, fault?.Exception)
    {
        Fault = fault ?? throw new ArgumentNullException(nameof(fault));
    }

    public Protocol64Fault Fault { get; }
}

public interface IProtocol64FaultSink
{
    void Report(Protocol64Fault fault);
}

public sealed class DelegateProtocol64FaultSink : IProtocol64FaultSink
{
    private readonly Action<Protocol64Fault> _report;

    public DelegateProtocol64FaultSink(Action<Protocol64Fault> report)
    {
        _report = report ?? throw new ArgumentNullException(nameof(report));
    }

    public void Report(Protocol64Fault fault)
        => _report(fault);
}
