using System;
using System.Collections.Generic;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>
/// Listens to the jukebox's decoded PCM and turns it into light-friendly features: overall
/// level, bass/mid/high bands, and beats. Fed only while the jukebox is audible to this
/// player (a muted jukebox feeds nothing, so lights hold their floor).
/// </summary>
/// <remarks>
/// <para>Bands are one-pole filter splits (cheap, no FFT): bass below ~150 Hz, mids up to
/// ~2 kHz, highs above. Each is normalised by its own slowly decaying peak (auto-gain) so
/// quiet and loud tracks both drive lights across the full range.</para>
/// <para>Beats are onsets in a bass-weighted energy flux, above an adaptive threshold (local
/// mean times a factor) with a refractory period.</para>
/// <para>Features are timestamped with the playback queue's delay, so a flash lands when
/// the beat is heard, not when its packet arrives.</para>
/// </remarks>
public sealed class JukeboxAnalyzer
{
    private const float SampleRate = 48000f;

    /// <summary>Roughly how long decoded audio waits in the output queue before it is heard.</summary>
    public const double PlaybackLatencySeconds = 0.12;

    /// <summary>Features older than this mean the music stopped (or was muted).</summary>
    private const double SilenceTimeoutSeconds = 0.5;

    private const int FluxHistoryLength = 43; // ~0.86 s of 20 ms chunks
    private const float BeatThreshold = 1.45f;
    private const double BeatRefractorySeconds = 0.11;
    private const double BeatDecaySeconds = 0.14;

    // Quiet-section ("down time") detection: the recent energy against the song's longer
    // average, with hold times either way so a light does not flap on short gaps.
    private const float QuietEnterRatio = 0.5f;
    private const float QuietExitRatio = 0.65f;
    private const float QuietEnterSeconds = 1.0f;
    private const float QuietExitSeconds = 0.25f;
    private const float ShortEnergySeconds = 0.4f;
    private const float LongEnergySeconds = 15f;
    private const float QuietWarmupSeconds = 4f;
    private const float QuietBlendSeconds = 0.35f;

    private static readonly float BassCoefficient = OnePole(150f);
    private static readonly float HighSplitCoefficient = OnePole(2000f);

    private readonly Queue<(double At, Features Value)> _pending = new();
    private readonly float[] _fluxHistory = new float[FluxHistoryLength];
    private int _fluxIndex;
    private int _fluxCount;

    private float _bassState;
    private float _midState;
    private float _peakLevel = 0.02f;
    private float _peakBass = 0.02f;
    private float _peakMids = 0.02f;
    private float _peakHighs = 0.02f;
    private float _previousOnsetEnergy;
    private double _lastFedAt = double.NegativeInfinity;

    private float _shortEnergy;
    private float _longEnergy;
    private float _fedSeconds;
    private float _quietTimer;
    private bool _quiet;
    private float _quietBlend;

    // Consumer side (smoothed, at playback time).
    private Features _target;
    private float _level;
    private float _bass;
    private float _mids;
    private float _highs;
    private double _lastBeatAt = double.NegativeInfinity;
    private double _lastSampledAt = double.NegativeInfinity;
    private double _latestReadyAt = double.NegativeInfinity;
    private int _beatCount;

    private readonly record struct Features(float Level, float Bass, float Mids, float Highs, bool Beat, bool Quiet);

    /// <summary>Analyses one chunk of interleaved 16-bit PCM as it is queued for playback.</summary>
    public void Feed(short[] samples, int channels, double now)
    {
        if (samples.Length == 0)
        {
            return;
        }

        channels = channels == 2 ? 2 : 1;
        var frames = samples.Length / channels;
        if (frames == 0)
        {
            return;
        }

        double sumAll = 0d;
        double sumBass = 0d;
        double sumMids = 0d;
        double sumHighs = 0d;
        for (var frame = 0; frame < frames; frame += 1)
        {
            var mono = channels == 2
                ? (samples[frame * 2] + samples[(frame * 2) + 1]) * (0.5f / 32768f)
                : samples[frame] / 32768f;
            _bassState += BassCoefficient * (mono - _bassState);
            _midState += HighSplitCoefficient * (mono - _midState);
            var bass = _bassState;
            var mids = _midState - _bassState;
            var highs = mono - _midState;
            sumAll += mono * mono;
            sumBass += bass * bass;
            sumMids += mids * mids;
            sumHighs += highs * highs;
        }

        var level = MathF.Sqrt((float)(sumAll / frames));
        var bassLevel = MathF.Sqrt((float)(sumBass / frames));
        var midLevel = MathF.Sqrt((float)(sumMids / frames));
        var highLevel = MathF.Sqrt((float)(sumHighs / frames));

        // Auto-gain: each band against its own peak, which decays over a few seconds.
        var chunkSeconds = frames / SampleRate;
        UpdateQuiet(level, chunkSeconds);
        var decay = MathF.Exp(-chunkSeconds / 4f);
        var features = new Features(
            Normalize(level, ref _peakLevel, decay),
            Normalize(bassLevel, ref _peakBass, decay),
            Normalize(midLevel, ref _peakMids, decay),
            Normalize(highLevel, ref _peakHighs, decay),
            DetectBeat((bassLevel * 2f) + midLevel),
            _quiet);

        _lastFedAt = now;
        _pending.Enqueue((now + PlaybackLatencySeconds, features));
        while (_pending.Count > 64)
        {
            _pending.Dequeue();
        }
    }

    /// <summary>Clears everything (stream changed or stopped).</summary>
    public void Reset()
    {
        _pending.Clear();
        Array.Clear(_fluxHistory);
        _fluxIndex = 0;
        _fluxCount = 0;
        _bassState = _midState = 0f;
        _peakLevel = _peakBass = _peakMids = _peakHighs = 0.02f;
        _previousOnsetEnergy = 0f;
        _target = default;
        _level = _bass = _mids = _highs = 0f;
        _shortEnergy = _longEnergy = 0f;
        _fedSeconds = 0f;
        _quietTimer = 0f;
        _quiet = false;
        _quietBlend = 0f;
        _lastFedAt = double.NegativeInfinity;
        _latestReadyAt = double.NegativeInfinity;
    }

    /// <summary>Music features as heard at <paramref name="now"/> (same clock as <see cref="Feed"/>).</summary>
    public MapLightMusic Sample(double now)
    {
        while (_pending.Count > 0 && _pending.Peek().At <= now)
        {
            var (at, value) = _pending.Dequeue();
            _target = value;
            _latestReadyAt = at;
            if (value.Beat && at - _lastBeatAt >= BeatRefractorySeconds)
            {
                _lastBeatAt = at;
                _beatCount += 1;
            }
        }

        var active = now - _lastFedAt <= SilenceTimeoutSeconds + PlaybackLatencySeconds
            && now - _latestReadyAt <= SilenceTimeoutSeconds;
        var elapsed = _lastSampledAt > double.NegativeInfinity ? Math.Clamp(now - _lastSampledAt, 0d, 0.25d) : 0d;
        _lastSampledAt = now;
        if (!active)
        {
            _level = _bass = _mids = _highs = 0f;
            _quietBlend = 0f;
            return MapLightMusic.Silent;
        }

        var quietTarget = _target.Quiet ? 1f : 0f;
        _quietBlend += (quietTarget - _quietBlend) * (1f - MathF.Exp(-(float)elapsed / QuietBlendSeconds));

        // Fast attack, slower release: punchy without jitter.
        _level = Follow(_level, _target.Level, elapsed);
        _bass = Follow(_bass, _target.Bass, elapsed);
        _mids = Follow(_mids, _target.Mids, elapsed);
        _highs = Follow(_highs, _target.Highs, elapsed);
        var beat = (float)Math.Exp(-Math.Max(0d, now - _lastBeatAt) / BeatDecaySeconds);
        return new MapLightMusic(true, _level, _bass, _mids, _highs, beat, _beatCount)
        {
            Quiet = _target.Quiet,
            QuietBlend = Math.Clamp(_quietBlend, 0f, 1f),
        };
    }

    /// <summary>
    /// Tracks quiet sections: the energy of the last fraction of a second against the
    /// song's average over the last ~15 seconds. The long average holds still while quiet,
    /// so a long breakdown stays quiet until the music really comes back.
    /// </summary>
    private void UpdateQuiet(float level, float chunkSeconds)
    {
        _fedSeconds += chunkSeconds;
        _shortEnergy += (level - _shortEnergy) * (1f - MathF.Exp(-chunkSeconds / ShortEnergySeconds));
        if (_longEnergy <= 0f)
        {
            _longEnergy = level;
        }
        else if (!_quiet)
        {
            _longEnergy += (level - _longEnergy) * (1f - MathF.Exp(-chunkSeconds / LongEnergySeconds));
        }

        if (_fedSeconds < QuietWarmupSeconds || _longEnergy <= 0.0005f)
        {
            _quietTimer = 0f;
            _quiet = false;
            return;
        }

        var ratio = _shortEnergy / _longEnergy;
        var crossing = _quiet ? ratio > QuietExitRatio : ratio < QuietEnterRatio;
        _quietTimer = crossing ? _quietTimer + chunkSeconds : 0f;
        if (_quietTimer >= (_quiet ? QuietExitSeconds : QuietEnterSeconds))
        {
            _quiet = !_quiet;
            _quietTimer = 0f;
        }
    }

    private bool DetectBeat(float energy)
    {
        var flux = MathF.Max(0f, energy - _previousOnsetEnergy);
        _previousOnsetEnergy = energy;
        var mean = 0f;
        for (var index = 0; index < _fluxCount; index += 1)
        {
            mean += _fluxHistory[index];
        }

        mean = _fluxCount > 0 ? mean / _fluxCount : 0f;
        _fluxHistory[_fluxIndex] = flux;
        _fluxIndex = (_fluxIndex + 1) % FluxHistoryLength;
        _fluxCount = Math.Min(_fluxCount + 1, FluxHistoryLength);
        return _fluxCount >= 8 && flux > (mean * BeatThreshold) + 0.002f;
    }

    private static float Normalize(float value, ref float peak, float decay)
    {
        peak = MathF.Max(value, MathF.Max(0.01f, peak * decay));
        return Math.Clamp(value / peak, 0f, 1f);
    }

    private static float Follow(float current, float target, double elapsed)
    {
        var seconds = (float)elapsed;
        var rate = target > current ? 0.025f : 0.18f; // attack / release time constants
        var blend = 1f - MathF.Exp(-seconds / rate);
        return current + ((target - current) * blend);
    }

    /// <summary>One-pole low-pass coefficient for a cutoff at 48 kHz.</summary>
    private static float OnePole(float cutoffHz) => 1f - MathF.Exp(-MathF.Tau * cutoffHz / SampleRate);
}
