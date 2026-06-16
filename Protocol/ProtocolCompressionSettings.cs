namespace OpenGarrison.Protocol;

/// <summary>
/// Configuration for protocol message compression.
/// </summary>
public sealed class ProtocolCompressionSettings
{
    /// <summary>
    /// Shared instance with compression enabled (default).
    /// </summary>
    public static readonly ProtocolCompressionSettings Default = new()
    {
        EnableCompression = true,
        MinimumBytesForCompression = 200,
        CompressOnlySnapshots = true,
    };

    public static readonly ProtocolCompressionSettings AllMessages = new()
    {
        EnableCompression = true,
        MinimumBytesForCompression = 200,
        CompressOnlySnapshots = false,
    };

    /// <summary>
    /// Shared instance with compression disabled.
    /// Useful for debugging or compatibility fallback.
    /// </summary>
    public static readonly ProtocolCompressionSettings Disabled = new()
    {
        EnableCompression = false,
    };

    /// <summary>
    /// Whether to enable LZ4 compression for messages.
    /// Default: true
    /// </summary>
    public bool EnableCompression { get; init; } = true;

    /// <summary>
    /// Minimum message size (in bytes) before compression is attempted.
    /// Messages smaller than this are sent uncompressed.
    /// Default: 200 bytes
    /// </summary>
    public int MinimumBytesForCompression { get; init; } = 200;

    /// <summary>
    /// If true, only compress snapshot messages (most bandwidth-heavy).
    /// If false, compress all message types.
    /// Default: true (snapshots only)
    /// </summary>
    public bool CompressOnlySnapshots { get; init; } = true;

    /// <summary>
    /// If true, only use compressed payload if it's actually smaller than uncompressed.
    /// Default: true
    /// </summary>
    public bool UseCompressionOnlyIfSmaller { get; init; } = true;
}

/// <summary>
/// Indicates the encoding format of a protocol message payload.
/// </summary>
internal enum MessageEncoding : byte
{
    /// <summary>No compression - raw serialized data</summary>
    None = 0,

    /// <summary>LZ4 compressed data</summary>
    LZ4 = 1,
}
