using Microsoft.Xna.Framework.Audio;
using NVorbis;
using System;
using System.Buffers.Binary;
using System.IO;

namespace OpenGarrison.Client;

internal static class SoundDecodeUtility
{
    public static SoundEffect LoadSoundEffect(byte[] bytes, string assetName)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetName);

        if (string.Equals(Path.GetExtension(assetName), ".ogg", StringComparison.OrdinalIgnoreCase))
        {
            return LoadOggSoundEffect(bytes, assetName);
        }

        using var stream = new MemoryStream(bytes, writable: false);
        return SoundEffect.FromStream(stream);
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
}
