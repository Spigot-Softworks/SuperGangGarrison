using System.Buffers.Binary;
using Microsoft.Xna.Framework.Audio;
using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SoundDecodeUtilityTests
{
    [Fact]
    public void WaveDecoderReadsPackagedDamageIndicatorDing()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "Plugins",
            "Packaged",
            "Client",
            "Lua.DamageIndicator",
            "dingaling.wav");

        var bytes = File.ReadAllBytes(path);

        Assert.True(SoundDecodeUtility.TryDecodeWavePcm16(bytes, out var decoded));
        Assert.Equal(44100, decoded.SampleRate);
        Assert.Equal(AudioChannels.Mono, decoded.Channels);
        Assert.True(decoded.PcmBytes.Length > 0);
        Assert.Equal(0, decoded.PcmBytes.Length % sizeof(short));
    }

    [Fact]
    public void WaveDecoderConvertsUnsignedEightBitPcmToSignedSixteenBitPcm()
    {
        var bytes = BuildWave(formatTag: 1, channels: 1, sampleRate: 8000, bitsPerSample: 8, [0, 128, 255]);

        Assert.True(SoundDecodeUtility.TryDecodeWavePcm16(bytes, out var decoded));

        Assert.Equal(8000, decoded.SampleRate);
        Assert.Equal(AudioChannels.Mono, decoded.Channels);
        Assert.Equal(-32768, BinaryPrimitives.ReadInt16LittleEndian(decoded.PcmBytes.AsSpan(0, 2)));
        Assert.Equal(0, BinaryPrimitives.ReadInt16LittleEndian(decoded.PcmBytes.AsSpan(2, 2)));
        Assert.Equal(32512, BinaryPrimitives.ReadInt16LittleEndian(decoded.PcmBytes.AsSpan(4, 2)));
    }

    [Fact]
    public void WaveDecoderRejectsUnsupportedChannelCounts()
    {
        var bytes = BuildWave(formatTag: 1, channels: 3, sampleRate: 44100, bitsPerSample: 16, [0, 0, 1, 0, 2, 0]);

        Assert.False(SoundDecodeUtility.TryDecodeWavePcm16(bytes, out _));
    }

    private static byte[] BuildWave(
        ushort formatTag,
        ushort channels,
        uint sampleRate,
        ushort bitsPerSample,
        byte[] data)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        var bytesPerSample = Math.Max(1, bitsPerSample / 8);
        var blockAlign = (ushort)(channels * bytesPerSample);
        var averageBytesPerSecond = sampleRate * blockAlign;
        var riffSize = 4 + 8 + 16 + 8 + data.Length;

        writer.Write("RIFF"u8);
        writer.Write(riffSize);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write(formatTag);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(averageBytesPerSecond);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write("data"u8);
        writer.Write(data.Length);
        writer.Write(data);
        return stream.ToArray();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OpenGarrison.sln"))
                && Directory.Exists(Path.Combine(directory.FullName, "Plugins")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
