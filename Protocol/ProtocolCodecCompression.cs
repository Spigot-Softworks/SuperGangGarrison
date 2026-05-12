using System;
using System.IO;
using K4os.Compression.LZ4;

namespace OpenGarrison.Protocol;

/// <summary>
/// Handles compression/decompression of protocol messages using LZ4.
/// </summary>
internal static class ProtocolCodecCompression
{
    /// <summary>
    /// Compress data using LZ4.
    /// Returns null if compression failed or didn't save space.
    /// </summary>
    public static byte[]? TryCompress(byte[] data, ProtocolCompressionSettings settings)
    {
        if (!settings.EnableCompression || data.Length < settings.MinimumBytesForCompression)
        {
            return null;
        }

        try
        {
            // LZ4 Level 0 = fastest compression, good for real-time data
            var compressed = LZ4Pickler.Pickle(data, LZ4Level.L00_FAST);

            // Only use compressed version if actually smaller
            if (settings.UseCompressionOnlyIfSmaller && compressed.Length >= data.Length)
            {
                return null;
            }

            return compressed;
        }
        catch
        {
            // Compression failed - caller should fall back to uncompressed
            return null;
        }
    }

    /// <summary>
    /// Decompress LZ4-compressed data.
    /// </summary>
    public static byte[] Decompress(byte[] compressedData)
    {
        try
        {
            return LZ4Pickler.Unpickle(compressedData);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException("Failed to decompress LZ4 data", ex);
        }
    }

    /// <summary>
    /// Prepend encoding byte to payload.
    /// Format: [encoding:1 byte][payload:N bytes]
    /// </summary>
    public static byte[] PrependEncoding(byte[] payload, MessageEncoding encoding)
    {
        var result = new byte[payload.Length + 1];
        result[0] = (byte)encoding;
        Array.Copy(payload, 0, result, 1, payload.Length);
        return result;
    }

    /// <summary>
    /// Read encoding byte and extract payload.
    /// Returns the encoding type and the payload without the encoding byte.
    /// </summary>
    public static (MessageEncoding Encoding, byte[] Payload) ReadEncoding(byte[] data)
    {
        if (data.Length < 2) // Need at least encoding byte + 1 byte of data
        {
            throw new InvalidDataException("Payload too short");
        }

        var encoding = (MessageEncoding)data[0];
        var payload = new byte[data.Length - 1];
        Array.Copy(data, 1, payload, 0, payload.Length);
        return (encoding, payload);
    }
}
