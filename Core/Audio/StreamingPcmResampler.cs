using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

/// <summary>Incremental interleaved PCM resampling and fixed frame assembly; retains fractional phase across callbacks.</summary>
public sealed class StreamingPcmResampler
{
    private readonly int _channels;
    private readonly double _step;
    private readonly Action<short[]> _onFrame;
    private readonly float[] _previous;
    private short[] _frame;
    private int _written;
    private long _inputPosition;
    private double _nextOutputPosition;
    private bool _hasPrevious;

    public StreamingPcmResampler(int sampleRate, int channels, Action<short[]> onFrame)
    {
        if (sampleRate is < 8000 or > 192000 || channels is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        _channels = channels;
        _step = sampleRate / (double)AudioWireFormat.SampleRate;
        _onFrame = onFrame;
        _previous = new float[channels];
        _frame = new short[AudioWireFormat.SamplesPerFrame * channels];
    }

    public void Add(ReadOnlySpan<float> samples, float gain = 1f)
    {
        for (var i = 0; i + _channels <= samples.Length; i += _channels)
        {
            if (!_hasPrevious)
            {
                samples.Slice(i, _channels).CopyTo(_previous);
                _hasPrevious = true;
            }
            while (_nextOutputPosition <= _inputPosition)
            {
                var blend = Math.Clamp(_nextOutputPosition - (_inputPosition - 1), 0, 1);
                for (var channel = 0; channel < _channels; channel++)
                {
                    var value = (_previous[channel] + ((samples[i + channel] - _previous[channel]) * blend)) * gain;
                    _frame[_written++] = double.IsFinite(value) ? (short)Math.Clamp(value * 32767, -32768, 32767) : (short)0;
                }
                if (_written == _frame.Length)
                {
                    var completed = _frame;
                    _frame = new short[completed.Length];
                    _written = 0;
                    _onFrame(completed);
                }
                _nextOutputPosition += _step;
            }
            samples.Slice(i, _channels).CopyTo(_previous);
            _inputPosition++;
        }
    }

    public void Finish()
    {
        if (_written == 0) return;
        _onFrame(_frame);
        _frame = new short[_frame.Length];
        _written = 0;
    }
}
