using Concentus;
using Concentus.Enums;
using Concentus.Structs;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

public static class StreamingOpus
{
    static StreamingOpus()
    {
        // The managed implementation ships on desktop, dedicated servers and browser AOT.
        OpusCodecFactory.AttemptToUseNativeLibrary = false;
    }

    public static IOpusEncoder CreateEncoder(bool music)
    {
        var encoder = OpusCodecFactory.CreateEncoder(AudioWireFormat.SampleRate, music ? 2 : 1,
            music ? OpusApplication.OPUS_APPLICATION_AUDIO : OpusApplication.OPUS_APPLICATION_VOIP);
        encoder.Bitrate = music ? 96000 : 24000;
        encoder.Complexity = 5;
        encoder.UseVBR = true;
        encoder.UseConstrainedVBR = true;
        encoder.SignalType = music ? OpusSignal.OPUS_SIGNAL_MUSIC : OpusSignal.OPUS_SIGNAL_VOICE;
        return encoder;
    }

    public static IOpusDecoder CreateDecoder(bool music) => OpusCodecFactory.CreateDecoder(AudioWireFormat.SampleRate, music ? 2 : 1);

    public static bool IsValid(AudioPacket packet) => AudioWireFormat.IsValid(packet)
        && packet.Frames.All(frame => OpusPacketInfo.GetNumSamples(frame, AudioWireFormat.SampleRate) == AudioWireFormat.SamplesPerFrame);

    public static byte[] Encode(IOpusEncoder encoder, ReadOnlySpan<short> samples)
    {
        var output = new byte[AudioWireFormat.MaxFrameBytes];
        var length = encoder.Encode(samples, AudioWireFormat.SamplesPerFrame, output, output.Length);
        if (length <= 0) throw new InvalidDataException("Opus encoder returned an empty frame.");
        return output[..length];
    }
}

/// <summary>Packet redundancy lets a latest-value transport coalesce frames without losing normal audio.</summary>
public sealed class AudioPacketHistory
{
    private readonly Queue<byte[]> _frames = new();
    private uint _sequence;

    public AudioPacket Add(byte[] frame)
    {
        if (_frames.Count == AudioWireFormat.HistoryFrames) _frames.Dequeue();
        _frames.Enqueue(frame);
        return new AudioPacket(++_sequence, _frames.ToArray());
    }

    public void Clear() => _frames.Clear(); // Keep sequence monotonic across push-to-talk releases.
}

/// <summary>Bounded, ordered playout with redundancy recovery, loss concealment and latency recovery.</summary>
public sealed class AudioPlaybackBuffer : IDisposable
{
    public const int MaxPendingFrames = 12;
    private readonly IOpusDecoder _decoder;
    private readonly Dictionary<uint, byte[]> _pending = new();
    private uint _nextSequence;
    private uint _latestSequence;
    private bool _initialized;
    private double _nextAt;
    private double _lastReceivedAt = double.NegativeInfinity;

    public AudioPlaybackBuffer(bool music) => _decoder = StreamingOpus.CreateDecoder(music);
    public int PendingFrames => _pending.Count;
    public double LastReceivedAt => _lastReceivedAt;

    public bool Add(AudioPacket packet, double now)
    {
        if (!StreamingOpus.IsValid(packet)) return false;
        var first = unchecked(packet.Sequence - (uint)(packet.Frames.Length - 1));
        if (!_initialized || now - _lastReceivedAt > 0.5)
        {
            _pending.Clear();
            _decoder.ResetState();
            _nextSequence = first;
            _latestSequence = packet.Sequence;
            _nextAt = now + 0.06;
            _initialized = true;
        }
        else if (!AudioWireFormat.IsNewer(packet.Sequence, _latestSequence))
        {
            return false;
        }
        _latestSequence = packet.Sequence;
        _lastReceivedAt = now;
        if (unchecked((int)(packet.Sequence - _nextSequence)) >= MaxPendingFrames)
        {
            _pending.Clear();
            _decoder.ResetState();
            _nextSequence = first;
            _nextAt = now + 0.04;
        }
        for (var i = 0; i < packet.Frames.Length; i++)
        {
            var sequence = unchecked(first + (uint)i);
            if (unchecked((int)(sequence - _nextSequence)) >= 0) _pending.TryAdd(sequence, packet.Frames[i]);
        }
        return true;
    }

    public bool TryRead(double now, out short[] samples)
    {
        samples = [];
        if (!_initialized || now < _nextAt || AudioWireFormat.IsNewer(_nextSequence, _latestSequence)) return false;
        // A stalled render loop must resume near live time, not play seconds of old speech.
        if (now - _nextAt > 0.2)
        {
            var keepFrom = unchecked(_latestSequence - 2);
            if (AudioWireFormat.IsNewer(keepFrom, _nextSequence)) _nextSequence = keepFrom;
            foreach (var key in _pending.Keys.Where(key => AudioWireFormat.IsNewer(_nextSequence, key)).ToArray()) _pending.Remove(key);
            _decoder.ResetState();
            _nextAt = now;
        }
        _pending.Remove(_nextSequence++, out var frame);
        _nextAt += AudioWireFormat.FrameMilliseconds / 1000d;
        samples = new short[AudioWireFormat.SamplesPerFrame * _decoder.NumChannels];
        try
        {
            _decoder.Decode(frame ?? [], samples, AudioWireFormat.SamplesPerFrame);
        }
        catch (Exception ex) when (ex is Concentus.OpusException or ArgumentException)
        {
            samples.AsSpan().Clear();
            _decoder.ResetState();
        }
        return true;
    }

    public void Dispose() => _decoder.Dispose();
}
