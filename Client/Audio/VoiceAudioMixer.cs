using System;
using System.Collections.Generic;

namespace OpenGarrison.Client;

/// <summary>
/// Small lock-protected PCM mixer used by the desktop native audio callback.
/// The callback consumes queued chunks continuously, independently of the game
/// update rate, and applies the current per-speaker gain and pan to every frame.
/// </summary>
internal sealed class VoiceAudioMixer
{
    private const int MaxQueuedChunksPerSpeaker = 8;
    private readonly object _gate = new();
    private readonly Dictionary<byte, Stream> _streams = new();

    public void Enqueue(byte slot, short[] samples, int channels, float volume, float pan)
    {
        if (samples.Length == 0) return;
        channels = channels == 2 ? 2 : 1;
        if (channels == 2 && (samples.Length & 1) != 0) return;
        lock (_gate)
        {
            if (!_streams.TryGetValue(slot, out var stream) || stream.Channels != channels)
            {
                _streams[slot] = stream = new Stream(channels);
            }

            stream.Volume = Math.Clamp(volume, 0f, 1f);
            stream.Pan = Math.Clamp(pan, -1f, 1f);
            while (stream.Chunks.Count >= MaxQueuedChunksPerSpeaker) stream.Chunks.Dequeue();
            stream.Chunks.Enqueue(new Chunk(samples));
        }
    }

    public void SetVolume(byte slot, float volume)
    {
        lock (_gate)
        {
            if (_streams.TryGetValue(slot, out var stream)) stream.Volume = Math.Clamp(volume, 0f, 1f);
        }
    }

    public void Stop(byte slot)
    {
        lock (_gate) _streams.Remove(slot);
    }

    public void StopAll()
    {
        lock (_gate) _streams.Clear();
    }

    public void Render(Span<short> stereoOutput)
    {
        if ((stereoOutput.Length & 1) != 0) throw new ArgumentException("Stereo output must contain an even number of samples.", nameof(stereoOutput));
        Span<float> mix = stackalloc float[stereoOutput.Length];
        mix.Clear();
        lock (_gate)
        {
            foreach (var stream in _streams.Values)
            {
                for (var frame = 0; frame < stereoOutput.Length / 2; frame++)
                {
                    if (!stream.TryReadFrame(out var left, out var right)) break;
                    var leftMix = left * stream.Volume;
                    var rightMix = right * stream.Volume;
                    if (stream.Channels == 1)
                    {
                        var mono = leftMix;
                        leftMix = mono * (stream.Pan <= 0f ? 1f : 1f - stream.Pan);
                        rightMix = mono * (stream.Pan >= 0f ? 1f : 1f + stream.Pan);
                    }

                    var outputIndex = frame * 2;
                    mix[outputIndex] += leftMix;
                    mix[outputIndex + 1] += rightMix;
                }
            }
        }

        for (var index = 0; index < stereoOutput.Length; index += 1)
        {
            stereoOutput[index] = (short)Math.Clamp(
                (int)MathF.Round(mix[index]), short.MinValue, short.MaxValue);
        }
    }

    private sealed class Stream
    {
        public Stream(int channels) => Channels = channels;
        public int Channels { get; }
        public float Volume { get; set; }
        public float Pan { get; set; }
        public Queue<Chunk> Chunks { get; } = new();

        public bool TryReadFrame(out short left, out short right)
        {
            while (Chunks.Count > 0)
            {
                var chunk = Chunks.Peek();
                if (chunk.Offset >= chunk.Samples.Length)
                {
                    Chunks.Dequeue();
                    continue;
                }

                left = chunk.Samples[chunk.Offset++];
                right = Channels == 2 ? chunk.Samples[chunk.Offset++] : left;
                return true;
            }

            left = right = 0;
            return false;
        }
    }

    private sealed class Chunk(short[] samples)
    {
        public short[] Samples { get; } = samples;
        public int Offset { get; set; }
    }
}
