#nullable enable

using System;
using System.Diagnostics;

namespace OpenGarrison.Client;

internal readonly record struct ClientFrameTimingSummary(
    long SampleCount,
    double AverageFps,
    double P50Milliseconds,
    double P95Milliseconds,
    double P99Milliseconds,
    double MaxMilliseconds,
    long Over16Point67Milliseconds,
    long Over33Point34Milliseconds,
    long Over50Milliseconds);

/// <summary>Bounded, allocation-free-on-record accumulator for completed-frame intervals.</summary>
internal sealed class ClientFrameTimingAccumulator
{
    private const double HistogramResolutionMilliseconds = 0.1d;
    private const double HistogramMaximumMilliseconds = 2000d;
    private const int HistogramOverflowIndex = (int)(HistogramMaximumMilliseconds / HistogramResolutionMilliseconds);
    private const double FirstHitchThresholdMilliseconds = 16.67d;
    private const double SecondHitchThresholdMilliseconds = 33.34d;
    private const double ThirdHitchThresholdMilliseconds = 50d;

    private long[]? _intervalHistogram;
    private long _lastCompletedFrameTimestamp;
    private long _sampleCount;
    private double _totalIntervalMilliseconds;
    private double _maxIntervalMilliseconds;
    private long _over16Point67Milliseconds;
    private long _over33Point34Milliseconds;
    private long _over50Milliseconds;

    public void RecordCompletedFrame(long timestamp)
    {
        if (_lastCompletedFrameTimestamp > 0L && timestamp > _lastCompletedFrameTimestamp)
        {
            var intervalMilliseconds =
                (timestamp - _lastCompletedFrameTimestamp) * 1000d / Stopwatch.Frequency;
            RecordInterval(intervalMilliseconds);
        }

        _lastCompletedFrameTimestamp = timestamp;
    }

    public void RecordInterval(double intervalMilliseconds)
    {
        if (!double.IsFinite(intervalMilliseconds) || intervalMilliseconds < 0d)
        {
            return;
        }

        _intervalHistogram ??= new long[HistogramOverflowIndex + 1];
        var histogramIndex = intervalMilliseconds >= HistogramMaximumMilliseconds
            ? HistogramOverflowIndex
            : Math.Max(
                0,
                (int)Math.Ceiling(
                    intervalMilliseconds / HistogramResolutionMilliseconds - 1e-9d) - 1);
        _intervalHistogram[histogramIndex] += 1;
        _sampleCount += 1;
        _totalIntervalMilliseconds += intervalMilliseconds;
        _maxIntervalMilliseconds = Math.Max(_maxIntervalMilliseconds, intervalMilliseconds);
        if (intervalMilliseconds > FirstHitchThresholdMilliseconds)
        {
            _over16Point67Milliseconds += 1;
        }

        if (intervalMilliseconds > SecondHitchThresholdMilliseconds)
        {
            _over33Point34Milliseconds += 1;
        }

        if (intervalMilliseconds > ThirdHitchThresholdMilliseconds)
        {
            _over50Milliseconds += 1;
        }
    }

    public ClientFrameTimingSummary GetSummary()
    {
        if (_sampleCount == 0 || _intervalHistogram is null)
        {
            return default;
        }

        return new ClientFrameTimingSummary(
            _sampleCount,
            _totalIntervalMilliseconds > 0d
                ? _sampleCount * 1000d / _totalIntervalMilliseconds
                : 0d,
            GetPercentileUpperBound(0.50d),
            GetPercentileUpperBound(0.95d),
            GetPercentileUpperBound(0.99d),
            _maxIntervalMilliseconds,
            _over16Point67Milliseconds,
            _over33Point34Milliseconds,
            _over50Milliseconds);
    }

    public void Reset()
    {
        if (_intervalHistogram is not null)
        {
            Array.Clear(_intervalHistogram, 0, _intervalHistogram.Length);
        }

        _lastCompletedFrameTimestamp = 0L;
        _sampleCount = 0L;
        _totalIntervalMilliseconds = 0d;
        _maxIntervalMilliseconds = 0d;
        _over16Point67Milliseconds = 0L;
        _over33Point34Milliseconds = 0L;
        _over50Milliseconds = 0L;
    }

    private double GetPercentileUpperBound(double percentile)
    {
        var targetRank = (long)Math.Ceiling(percentile * _sampleCount);
        long cumulativeCount = 0L;
        for (var index = 0; index < _intervalHistogram!.Length; index += 1)
        {
            cumulativeCount += _intervalHistogram[index];
            if (cumulativeCount >= targetRank)
            {
                // Bins use inclusive upper edges (20.0 ms stays in the 20.0 ms
                // bin), so percentile gates do not reject exact threshold values.
                return index == HistogramOverflowIndex
                    ? _maxIntervalMilliseconds
                    : (index + 1) * HistogramResolutionMilliseconds;
            }
        }

        return _maxIntervalMilliseconds;
    }
}
