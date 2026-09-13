using System.Threading.Channels;
using OpenGarrison.Core;

namespace OpenGarrison.Server;

/// <summary>Decode and encode off the authoritative game loop; backpressure bounds read-ahead to 160 ms.</summary>
internal sealed class JukeboxTrackReader : IDisposable
{
    private readonly CancellationTokenSource _cancel = new();
    private readonly Channel<byte[]> _frames = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(8)
    {
        SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait,
    });
    private readonly Task? _worker;
    private IEnumerator<byte[]>? _browserFrames;
    private bool _browserFinished;
    private int _disposed;
    private string? _error;
    public string? Error => Volatile.Read(ref _error);
    public bool Finished => OperatingSystem.IsBrowser() ? _browserFinished : _worker!.IsCompleted && !_frames.Reader.TryPeek(out _);
    public bool TryRead(out byte[] frame)
    {
        if (!OperatingSystem.IsBrowser()) return _frames.Reader.TryRead(out frame!);
        frame = [];
        if (_browserFinished || _disposed != 0) return false;
        try
        {
            if (_browserFrames!.MoveNext()) { frame = _browserFrames.Current; return true; }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { _error = ex.Message; }
        _browserFinished = true;
        _browserFrames?.Dispose(); _browserFrames = null;
        return false;
    }

    public JukeboxTrackReader(string path)
    {
        if (OperatingSystem.IsBrowser()) { _browserFrames = DecodeFrames(path).GetEnumerator(); return; }
        var token = _cancel.Token;
        _worker = Task.Run(() =>
        {
            try
            {
                foreach (var frame in DecodeFrames(path))
                {
                    token.ThrowIfCancellationRequested();
                    _frames.Writer.WriteAsync(frame, token).AsTask().GetAwaiter().GetResult();
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex) when (ex is not OutOfMemoryException) { Volatile.Write(ref _error, ex.Message); }
            finally { _frames.Writer.TryComplete(); }
        });
    }

    // Browsers advance this iterator a frame at a time; no blocking waits or worker threads are needed.
    private static IEnumerable<byte[]> DecodeFrames(string path)
    {
        using var encoder = StreamingOpus.CreateEncoder(music: true);
        using var source = OpenSource(path);
        if (source.Channels is < 1 or > 2 || source.SampleRate is < 8000 or > 192000)
            throw new InvalidDataException("Use mono or stereo audio at 8-192 kHz.");
        var pending = new Queue<byte[]>();
        var resampler = new StreamingPcmResampler(source.SampleRate, 2,
            samples => pending.Enqueue(StreamingOpus.Encode(encoder, samples)));
        var input = new float[1024 * source.Channels];
        var stereo = new float[2048];
        while (true)
        {
            var count = source.Read(input);
            if (count == 0) break;
            if (count % source.Channels != 0) throw new InvalidDataException("Incomplete audio sample.");
            if (source.Channels == 1)
            {
                for (var i = 0; i < count; i++) stereo[i * 2] = stereo[i * 2 + 1] = input[i];
                resampler.Add(stereo.AsSpan(0, count * 2));
            }
            else resampler.Add(input.AsSpan(0, count));
            while (pending.TryDequeue(out var frame)) yield return frame;
        }
        resampler.Finish();
        while (pending.TryDequeue(out var frame)) yield return frame;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _cancel.Cancel();
        if (_worker is not null) _ = _worker.ContinueWith(_ => _cancel.Dispose(), TaskScheduler.Default);
        else { _browserFrames?.Dispose(); _browserFrames = null; _browserFinished = true; _cancel.Dispose(); }
    }

    private static IPcmSource OpenSource(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".wav" => new WaveSource(path),
        ".ogg" => new VorbisSource(path),
        ".mp3" => new Mp3Source(path),
        _ => throw new InvalidDataException("Supported music formats: WAV, OGG Vorbis and MP3."),
    };

    private interface IPcmSource : IDisposable
    {
        int SampleRate { get; }
        int Channels { get; }
        int Read(float[] buffer);
    }

    private sealed class VorbisSource(string path) : IPcmSource
    {
        private readonly NVorbis.VorbisReader _reader = new(path);
        public int SampleRate => _reader.SampleRate;
        public int Channels => _reader.Channels;
        public int Read(float[] buffer)
        {
            if (Channels is < 1 or > 2) throw new InvalidDataException("Music must be mono or stereo.");
            return _reader.ReadSamples(buffer, 0, buffer.Length);
        }
        public void Dispose() => _reader.Dispose();
    }

    private sealed class Mp3Source(string path) : IPcmSource
    {
        private readonly NLayer.MpegFile _reader = new(path);
        public int SampleRate => _reader.SampleRate;
        public int Channels => _reader.Channels;
        public int Read(float[] buffer) => _reader.ReadSamples(buffer, 0, buffer.Length);
        public void Dispose() => _reader.Dispose();
    }

    private sealed class WaveSource : IPcmSource
    {
        private readonly BinaryReader _reader;
        private readonly int _format;
        private readonly int _bits;
        private long _remaining;
        public int SampleRate { get; }
        public int Channels { get; }

        public WaveSource(string path)
        {
            _reader = new BinaryReader(File.OpenRead(path));
            try
            {
                if (_reader.ReadUInt32() != 0x46464952) throw new InvalidDataException("Expected RIFF WAV.");
                _reader.ReadUInt32();
                if (_reader.ReadUInt32() != 0x45564157) throw new InvalidDataException("Expected WAVE header.");
                while (_reader.BaseStream.Position + 8 <= _reader.BaseStream.Length)
                {
                    var id = _reader.ReadUInt32();
                    var size = _reader.ReadUInt32();
                    var end = checked(_reader.BaseStream.Position + size);
                    if (end > _reader.BaseStream.Length) throw new InvalidDataException("Truncated WAV chunk.");
                    if (id == 0x20746d66)
                    {
                        if (size < 16) throw new InvalidDataException("Invalid WAV format chunk.");
                        _format = _reader.ReadUInt16();
                        Channels = _reader.ReadUInt16();
                        SampleRate = _reader.ReadInt32();
                        _reader.ReadUInt32();
                        var blockAlign = _reader.ReadUInt16();
                        _bits = _reader.ReadUInt16();
                        if (Channels is < 1 or > 2 || SampleRate is < 8000 or > 192000
                            || !(_format == 1 && _bits is 8 or 16 or 24 or 32 || _format == 3 && _bits == 32)
                            || blockAlign != Channels * (_bits / 8))
                            throw new InvalidDataException("Use mono/stereo PCM (8/16/24/32-bit) or float32 WAV.");
                    }
                    else if (id == 0x61746164)
                    {
                        if (Channels == 0 || size % (Channels * (_bits / 8)) != 0) throw new InvalidDataException("Invalid WAV data.");
                        _remaining = size;
                        return;
                    }
                    _reader.BaseStream.Position = end + (size & 1);
                }
                throw new InvalidDataException("WAV has no audio data.");
            }
            catch { _reader.Dispose(); throw; }
        }

        public int Read(float[] buffer)
        {
            var count = (int)Math.Min(buffer.Length, _remaining / (_bits / 8));
            for (var i = 0; i < count; i++)
            {
                buffer[i] = _format == 3 ? _reader.ReadSingle() : _bits switch
                {
                    8 => (_reader.ReadByte() - 128) / 128f,
                    16 => _reader.ReadInt16() / 32768f,
                    24 => ReadInt24() / 8388608f,
                    32 => _reader.ReadInt32() / 2147483648f,
                    _ => 0,
                };
            }
            _remaining -= count * (_bits / 8);
            return count;
        }

        private int ReadInt24()
        {
            var value = _reader.ReadByte() | (_reader.ReadByte() << 8) | (_reader.ReadByte() << 16);
            return (value << 8) >> 8;
        }
        public void Dispose() => _reader.Dispose();
    }
}
