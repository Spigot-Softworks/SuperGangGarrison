using Microsoft.Xna.Framework.Audio;
using NVorbis;
using System;
using System.Buffers.Binary;
using System.IO;

namespace OpenGarrison.Client;

internal static class SoundDecodeUtility
{
    internal readonly record struct DecodedSoundData(byte[] PcmBytes, int SampleRate, AudioChannels Channels);

    public static SoundEffect LoadSoundEffect(byte[] bytes, string assetName)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetName);

        if (string.Equals(Path.GetExtension(assetName), ".ogg", StringComparison.OrdinalIgnoreCase))
        {
            return LoadOggSoundEffect(bytes, assetName);
        }

        if (string.Equals(Path.GetExtension(assetName), ".wav", StringComparison.OrdinalIgnoreCase))
        {
            if (TryDecodeWavePcm16(bytes, out var decodedWave))
            {
                return new SoundEffect(decodedWave.PcmBytes, decodedWave.SampleRate, decodedWave.Channels);
            }

            if (OperatingSystem.IsBrowser())
            {
                throw new NotSupportedException($"Unsupported WAV encoding for {assetName}.");
            }
        }

        using var stream = new MemoryStream(bytes, writable: false);
        return SoundEffect.FromStream(stream);
    }

    internal static bool TryDecodeWavePcm16(byte[] bytes, out DecodedSoundData decodedSound)
    {
        decodedSound = default;
        if (bytes.Length < 44
            || !HasAscii(bytes, 0, "RIFF")
            || !HasAscii(bytes, 8, "WAVE"))
        {
            return false;
        }

        var offset = 12;
        WaveFormatInfo? format = null;
        ReadOnlySpan<byte> data = default;
        while (offset + 8 <= bytes.Length)
        {
            var chunkId = bytes.AsSpan(offset, 4);
            var chunkLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4, 4));
            offset += 8;
            if (chunkLength > int.MaxValue || offset + (int)chunkLength > bytes.Length)
            {
                return false;
            }

            var chunk = bytes.AsSpan(offset, (int)chunkLength);
            if (chunkId.SequenceEqual("fmt "u8))
            {
                if (!TryReadWaveFormat(chunk, out var parsedFormat))
                {
                    return false;
                }

                format = parsedFormat;
            }
            else if (chunkId.SequenceEqual("data"u8))
            {
                data = chunk;
            }

            offset += (int)chunkLength;
            if ((chunkLength & 1u) != 0u)
            {
                offset += 1;
            }
        }

        if (format is not { } waveFormat || data.Length == 0)
        {
            return false;
        }

        var channels = waveFormat.Channels == 1 ? AudioChannels.Mono : AudioChannels.Stereo;
        var pcmBytes = waveFormat.Encoding switch
        {
            WaveEncoding.Pcm when waveFormat.BitsPerSample == 16 => CopyAlignedPcm16(data),
            WaveEncoding.Pcm when waveFormat.BitsPerSample == 8 => ConvertPcm8ToPcm16(data),
            WaveEncoding.Pcm when waveFormat.BitsPerSample == 24 => ConvertPcm24ToPcm16(data),
            WaveEncoding.Pcm when waveFormat.BitsPerSample == 32 => ConvertPcm32ToPcm16(data),
            WaveEncoding.IeeeFloat when waveFormat.BitsPerSample == 32 => ConvertFloat32ToPcm16(data),
            _ => null,
        };

        if (pcmBytes is null || pcmBytes.Length == 0)
        {
            return false;
        }

        decodedSound = new DecodedSoundData(pcmBytes, waveFormat.SampleRate, channels);
        return true;
    }

    private static bool TryReadWaveFormat(ReadOnlySpan<byte> chunk, out WaveFormatInfo format)
    {
        format = default;
        if (chunk.Length < 16)
        {
            return false;
        }

        var formatTag = BinaryPrimitives.ReadUInt16LittleEndian(chunk[..2]);
        var channels = BinaryPrimitives.ReadUInt16LittleEndian(chunk.Slice(2, 2));
        var sampleRate = BinaryPrimitives.ReadUInt32LittleEndian(chunk.Slice(4, 4));
        var blockAlign = BinaryPrimitives.ReadUInt16LittleEndian(chunk.Slice(12, 2));
        var bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(chunk.Slice(14, 2));
        if (channels is < 1 or > 2 || sampleRate == 0 || sampleRate > int.MaxValue || blockAlign == 0)
        {
            return false;
        }

        var encoding = formatTag switch
        {
            1 => WaveEncoding.Pcm,
            3 => WaveEncoding.IeeeFloat,
            0xFFFE when TryReadWaveExtensibleEncoding(chunk, out var extensibleEncoding) => extensibleEncoding,
            _ => WaveEncoding.Unsupported,
        };

        if (encoding == WaveEncoding.Unsupported)
        {
            return false;
        }

        format = new WaveFormatInfo(encoding, channels, (int)sampleRate, blockAlign, bitsPerSample);
        return true;
    }

    private static bool TryReadWaveExtensibleEncoding(ReadOnlySpan<byte> chunk, out WaveEncoding encoding)
    {
        encoding = WaveEncoding.Unsupported;
        if (chunk.Length < 40)
        {
            return false;
        }

        var subFormat = chunk.Slice(24, 16);
        if (subFormat.SequenceEqual(PcmSubFormatGuidBytes))
        {
            encoding = WaveEncoding.Pcm;
            return true;
        }

        if (subFormat.SequenceEqual(IeeeFloatSubFormatGuidBytes))
        {
            encoding = WaveEncoding.IeeeFloat;
            return true;
        }

        return false;
    }

    private static byte[]? CopyAlignedPcm16(ReadOnlySpan<byte> data)
    {
        var length = data.Length - (data.Length % sizeof(short));
        if (length <= 0)
        {
            return null;
        }

        return data[..length].ToArray();
    }

    private static byte[]? ConvertPcm8ToPcm16(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
        {
            return null;
        }

        var pcmBytes = new byte[data.Length * sizeof(short)];
        for (var index = 0; index < data.Length; index += 1)
        {
            var sample = (short)((data[index] - 128) << 8);
            BinaryPrimitives.WriteInt16LittleEndian(pcmBytes.AsSpan(index * sizeof(short), sizeof(short)), sample);
        }

        return pcmBytes;
    }

    private static byte[]? ConvertPcm24ToPcm16(ReadOnlySpan<byte> data)
    {
        var sampleCount = data.Length / 3;
        if (sampleCount <= 0)
        {
            return null;
        }

        var pcmBytes = new byte[sampleCount * sizeof(short)];
        for (var index = 0; index < sampleCount; index += 1)
        {
            var sourceOffset = index * 3;
            var value = data[sourceOffset]
                | (data[sourceOffset + 1] << 8)
                | (data[sourceOffset + 2] << 16);
            if ((value & 0x800000) != 0)
            {
                value |= unchecked((int)0xFF000000);
            }

            var sample = (short)(value >> 8);
            BinaryPrimitives.WriteInt16LittleEndian(pcmBytes.AsSpan(index * sizeof(short), sizeof(short)), sample);
        }

        return pcmBytes;
    }

    private static byte[]? ConvertPcm32ToPcm16(ReadOnlySpan<byte> data)
    {
        var sampleCount = data.Length / sizeof(int);
        if (sampleCount <= 0)
        {
            return null;
        }

        var pcmBytes = new byte[sampleCount * sizeof(short)];
        for (var index = 0; index < sampleCount; index += 1)
        {
            var value = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(index * sizeof(int), sizeof(int)));
            var sample = (short)(value >> 16);
            BinaryPrimitives.WriteInt16LittleEndian(pcmBytes.AsSpan(index * sizeof(short), sizeof(short)), sample);
        }

        return pcmBytes;
    }

    private static byte[]? ConvertFloat32ToPcm16(ReadOnlySpan<byte> data)
    {
        var sampleCount = data.Length / sizeof(float);
        if (sampleCount <= 0)
        {
            return null;
        }

        var pcmBytes = new byte[sampleCount * sizeof(short)];
        for (var index = 0; index < sampleCount; index += 1)
        {
            var value = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data.Slice(index * sizeof(float), sizeof(float))));
            var clamped = Math.Clamp(value, -1f, 1f);
            var sample = clamped >= 0f
                ? (short)Math.Round(clamped * short.MaxValue)
                : (short)Math.Round(clamped * -short.MinValue);
            BinaryPrimitives.WriteInt16LittleEndian(pcmBytes.AsSpan(index * sizeof(short), sizeof(short)), sample);
        }

        return pcmBytes;
    }

    private static bool HasAscii(byte[] bytes, int offset, string text)
    {
        return offset >= 0
            && offset + text.Length <= bytes.Length
            && bytes.AsSpan(offset, text.Length).SequenceEqual(System.Text.Encoding.ASCII.GetBytes(text));
    }

    private static SoundEffect LoadOggSoundEffect(byte[] bytes, string assetName)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var vorbis = new VorbisReader(stream, false);
        if (vorbis.Channels is < 1 or > 2)
        {
            throw new NotSupportedException($"Unsupported Ogg channel count {vorbis.Channels} for {assetName}.");
        }

        var channels = vorbis.Channels == 1 ? AudioChannels.Mono : AudioChannels.Stereo;
        var sampleRate = vorbis.SampleRate;
        var sampleBuffer = new float[4096];
        byte[] pcmBytes;

        if (vorbis.TotalSamples > 0)
        {
            var estimatedBytes = checked((int)Math.Min(vorbis.TotalSamples * vorbis.Channels * sizeof(short), int.MaxValue));
            pcmBytes = new byte[estimatedBytes];
            var offset = 0;
            while (true)
            {
                var samplesRead = vorbis.ReadSamples(sampleBuffer, 0, sampleBuffer.Length);
                if (samplesRead <= 0)
                {
                    break;
                }

                EnsureCapacity(ref pcmBytes, offset, samplesRead * sizeof(short));
                WritePcm16(sampleBuffer.AsSpan(0, samplesRead), pcmBytes.AsSpan(offset));
                offset += samplesRead * sizeof(short);
            }

            if (offset != pcmBytes.Length)
            {
                Array.Resize(ref pcmBytes, offset);
            }
        }
        else
        {
            pcmBytes = Array.Empty<byte>();
            var offset = 0;
            while (true)
            {
                var samplesRead = vorbis.ReadSamples(sampleBuffer, 0, sampleBuffer.Length);
                if (samplesRead <= 0)
                {
                    break;
                }

                EnsureCapacity(ref pcmBytes, offset, samplesRead * sizeof(short));
                WritePcm16(sampleBuffer.AsSpan(0, samplesRead), pcmBytes.AsSpan(offset));
                offset += samplesRead * sizeof(short);
            }

            Array.Resize(ref pcmBytes, offset);
        }

        return new SoundEffect(pcmBytes, sampleRate, channels);
    }

    private static void EnsureCapacity(ref byte[] buffer, int offset, int additionalBytes)
    {
        var requiredLength = checked(offset + additionalBytes);
        if (requiredLength <= buffer.Length)
        {
            return;
        }

        var nextLength = buffer.Length == 0 ? 8192 : buffer.Length;
        while (nextLength < requiredLength)
        {
            nextLength = checked(nextLength * 2);
        }

        Array.Resize(ref buffer, nextLength);
    }

    private static void WritePcm16(ReadOnlySpan<float> samples, Span<byte> destination)
    {
        for (var index = 0; index < samples.Length; index += 1)
        {
            var clamped = Math.Clamp(samples[index], -1f, 1f);
            var value = clamped >= 0f
                ? (short)Math.Round(clamped * short.MaxValue)
                : (short)Math.Round(clamped * -short.MinValue);
            BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(index * sizeof(short), sizeof(short)), value);
        }
    }

    private static readonly byte[] PcmSubFormatGuidBytes =
    [
        0x01, 0x00, 0x00, 0x00,
        0x00, 0x00,
        0x10, 0x00,
        0x80, 0x00,
        0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71,
    ];

    private static readonly byte[] IeeeFloatSubFormatGuidBytes =
    [
        0x03, 0x00, 0x00, 0x00,
        0x00, 0x00,
        0x10, 0x00,
        0x80, 0x00,
        0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71,
    ];

    private readonly record struct WaveFormatInfo(
        WaveEncoding Encoding,
        int Channels,
        int SampleRate,
        int BlockAlign,
        int BitsPerSample);

    private enum WaveEncoding
    {
        Unsupported,
        Pcm,
        IeeeFloat,
    }
}
