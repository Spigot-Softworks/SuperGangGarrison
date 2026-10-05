#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace OpenGarrison.Client;

internal sealed class ClientFrameCaptureStageAccumulator
{
    private readonly double[] _milliseconds = new double[Enum.GetValues<Game1.ClientPerformanceMetric>().Length];

    public void Record(Game1.ClientPerformanceMetric metric, double milliseconds)
    {
        _milliseconds[(int)metric] += milliseconds;
    }

    public double GetMilliseconds(Game1.ClientPerformanceMetric metric)
    {
        return _milliseconds[(int)metric];
    }

    public void Reset()
    {
        Array.Clear(_milliseconds);
    }
}

internal readonly record struct ClientFrameCaptureSample(
    long FrameIndex,
    long CompletedUtcTicks,
    long CompletedStopwatchTicks,
    double DrawIntervalMilliseconds,
    double DrawCpuMilliseconds,
    double IntervalGapMilliseconds,
    string SessionKind,
    string Map,
    bool GameplayActive,
    bool Loading,
    bool WindowActive,
    bool LocalPlayerAwaitingJoin,
    int PracticeBotCount,
    int EntityCount,
    int ViewportWidth,
    int ViewportHeight,
    bool VSync,
    int FrameRateLimit,
    long AllocatedBytesDelta,
    int Gc0Delta,
    int Gc1Delta,
    int Gc2Delta,
    double StageUpdateMilliseconds,
    double StageSimulationMilliseconds,
    double StagePresentationMilliseconds,
    double StageWorldDrawMilliseconds,
    double StageHudDrawMilliseconds,
    double StageModalDrawMilliseconds,
    double StagePluginFrameMilliseconds,
    double StagePluginEventsMilliseconds,
    double StageInterpolationMilliseconds,
    double StageRenderStatesMilliseconds,
    double StageMusicMilliseconds,
    double StageBotBuildMilliseconds,
    double StageBotApplyMilliseconds,
    double StageNetworkReceiveMilliseconds,
    double StageNetworkResolveMilliseconds,
    double StageNetworkApplyMilliseconds,
    long DroppedFrames,
    string CaptureRegion = "",
    double PreviousFrameworkEndDrawMilliseconds = 0d,
    int TextureUploads = 0,
    double GlFinishMilliseconds = 0d,
    int GpuReadbacks = 0,
    double GpuReadbackMilliseconds = 0d);

/// <summary>
/// Bounded in-memory capture for completed CPU draw frames. It performs no disk I/O while
/// recording; a caller writes the retained chronological window when the client exits.
/// </summary>
internal sealed class ClientFrameCaptureRecorder
{
    public const int DefaultCapacity = 12_000;
    public const int DefaultHitchArchiveCapacity = 2_000;
    public const double HitchArchiveThresholdMilliseconds = 25d;

    private readonly ClientFrameCaptureSample[] _samples;
    private readonly ClientFrameCaptureSample[] _hitchSamples;
    private readonly ClientFrameTimingAccumulator _allFrameTiming = new();
    private readonly ClientFrameTimingAccumulator _activeGameplayFrameTiming = new();
    private int _nextWriteIndex;
    private int _hitchCount;
    private bool _hasPreviousFrame;
    private bool _previousFrameWasActiveGameplay;
    private long _previousFrameTimestamp;
    private long _firstFrameIndex;
    private long _lastFrameIndex;
    private long _captureStartUtcTicks;
    private long _captureEndUtcTicks;
    private long _totalFrameCount;
    private long _activeGameplayFrameCount;
    private long _framesAtLeast25Milliseconds;
    private long _framesAtLeast33Point34Milliseconds;
    private long _framesAtLeast50Milliseconds;
    private long _activeFramesAtLeast25Milliseconds;
    private long _activeFramesAtLeast33Point34Milliseconds;
    private long _activeFramesAtLeast50Milliseconds;
    private double _totalElapsedMilliseconds;
    private double _activeGameplayElapsedMilliseconds;

    public ClientFrameCaptureRecorder(
        int capacity = DefaultCapacity,
        int hitchArchiveCapacity = DefaultHitchArchiveCapacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        if (hitchArchiveCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hitchArchiveCapacity));
        }

        _samples = new ClientFrameCaptureSample[capacity];
        _hitchSamples = new ClientFrameCaptureSample[hitchArchiveCapacity];
        PrimeHistogram(_allFrameTiming);
        PrimeHistogram(_activeGameplayFrameTiming);
    }

    public int Capacity => _samples.Length;

    public int HitchArchiveCapacity => _hitchSamples.Length;

    public int Count { get; private set; }

    public long DroppedFrames { get; private set; }

    public int HitchArchiveCount => _hitchCount;

    public long HitchArchiveOmittedCount { get; private set; }

    public long TotalFrameCount => _totalFrameCount;

    public void Add(in ClientFrameCaptureSample sample)
    {
        if (Count == Capacity)
        {
            DroppedFrames += 1;
        }
        else
        {
            Count += 1;
        }

        var recentSample = sample with
        {
            DroppedFrames = DroppedFrames,
            CaptureRegion = "recent",
        };

        if (sample.DrawIntervalMilliseconds >= HitchArchiveThresholdMilliseconds)
        {
            if (_hitchCount < HitchArchiveCapacity)
            {
                _hitchSamples[_hitchCount] = recentSample with { CaptureRegion = "hitch_archive" };
                _hitchCount += 1;
                recentSample = recentSample with { CaptureRegion = "recent+hitch_archive" };
            }
            else
            {
                HitchArchiveOmittedCount += 1;
            }
        }

        _samples[_nextWriteIndex] = recentSample;
        _nextWriteIndex = (_nextWriteIndex + 1) % Capacity;

        RecordSessionStatistics(sample);
    }

    public void WriteCsv(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var mergedSamples = CreateMergedSamples(out var mergedCount, out var duplicateCount);
        using var writer = new StreamWriter(fullPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.WriteLine(

            "frame_index,completed_utc_ticks,completed_stopwatch_ticks,draw_interval_ms,draw_cpu_ms,interval_gap_ms,session_kind,map,gameplay_active,loading,window_active,local_player_awaiting_join,practice_bot_count,entity_count,viewport_width,viewport_height,vsync,frame_rate_limit,allocated_bytes_delta,gc0_delta,gc1_delta,gc2_delta,stage_update_ms,stage_simulation_ms,stage_presentation_ms,stage_world_draw_ms,stage_hud_draw_ms,stage_modal_draw_ms,stage_plugin_frame_ms,stage_plugin_events_ms,stage_interpolation_ms,stage_render_states_ms,stage_music_ms,stage_bot_build_ms,stage_bot_apply_ms,stage_network_receive_ms,stage_network_resolve_ms,stage_network_apply_ms,dropped_frames,capture_region,previous_framework_end_draw_ms,texture_uploads,gl_finish_ms,gpu_readbacks,gpu_readback_ms");

        for (var sampleIndex = 0; sampleIndex < mergedCount; sampleIndex += 1)
        {
            WriteSample(writer, mergedSamples[sampleIndex]);
        }

        writer.Flush();
        WriteSummaryJson(fullPath, mergedCount, duplicateCount);
    }

    private static void PrimeHistogram(ClientFrameTimingAccumulator timing)
    {
        timing.RecordInterval(0d);
        timing.Reset();
    }

    private void RecordSessionStatistics(in ClientFrameCaptureSample sample)
    {
        _totalFrameCount += 1;
        if (_totalFrameCount == 1)
        {
            _firstFrameIndex = sample.FrameIndex;
            _captureStartUtcTicks = sample.CompletedUtcTicks;
        }

        _lastFrameIndex = sample.FrameIndex;
        _captureEndUtcTicks = sample.CompletedUtcTicks;
        var currentFrameIsActiveGameplay = IsQualifiedActiveGameplayFrame(sample);
        if (_hasPreviousFrame && sample.CompletedStopwatchTicks > _previousFrameTimestamp)
        {
            var intervalMilliseconds = sample.DrawIntervalMilliseconds;
            _allFrameTiming.RecordInterval(intervalMilliseconds);
            _totalElapsedMilliseconds += intervalMilliseconds;
            RecordThresholdCounts(
                intervalMilliseconds,
                ref _framesAtLeast25Milliseconds,
                ref _framesAtLeast33Point34Milliseconds,
                ref _framesAtLeast50Milliseconds);

            if (_previousFrameWasActiveGameplay && currentFrameIsActiveGameplay)
            {
                _activeGameplayFrameTiming.RecordInterval(intervalMilliseconds);
                _activeGameplayElapsedMilliseconds += intervalMilliseconds;
                RecordThresholdCounts(
                    intervalMilliseconds,
                    ref _activeFramesAtLeast25Milliseconds,
                    ref _activeFramesAtLeast33Point34Milliseconds,
                    ref _activeFramesAtLeast50Milliseconds);
            }
        }

        if (currentFrameIsActiveGameplay)
        {
            _activeGameplayFrameCount += 1;
        }

        _hasPreviousFrame = true;
        _previousFrameWasActiveGameplay = currentFrameIsActiveGameplay;
        _previousFrameTimestamp = sample.CompletedStopwatchTicks;
    }

    private static bool IsQualifiedActiveGameplayFrame(in ClientFrameCaptureSample sample)
    {
        return sample.GameplayActive
            && !sample.Loading
            && sample.WindowActive
            && !sample.LocalPlayerAwaitingJoin;
    }

    private static void RecordThresholdCounts(
        double milliseconds,
        ref long framesAtLeast25Milliseconds,
        ref long framesAtLeast33Point34Milliseconds,
        ref long framesAtLeast50Milliseconds)
    {
        if (milliseconds >= 25d)
        {
            framesAtLeast25Milliseconds += 1;
        }

        if (milliseconds >= 33.34d)
        {
            framesAtLeast33Point34Milliseconds += 1;
        }

        if (milliseconds >= 50d)
        {
            framesAtLeast50Milliseconds += 1;
        }
    }

    private ClientFrameCaptureSample[] CreateMergedSamples(out int mergedCount, out int duplicateCount)
    {
        var oldestIndex = Count == Capacity ? _nextWriteIndex : 0;
        var merged = new ClientFrameCaptureSample[Count + _hitchCount];
        var destinationIndex = 0;
        for (var sampleIndex = 0; sampleIndex < Count; sampleIndex += 1)
        {
            merged[destinationIndex++] = _samples[(oldestIndex + sampleIndex) % Capacity];
        }

        for (var sampleIndex = 0; sampleIndex < _hitchCount; sampleIndex += 1)
        {
            merged[destinationIndex++] = _hitchSamples[sampleIndex];
        }

        Array.Sort(merged, ClientFrameCaptureSampleComparer.Instance);
        mergedCount = 0;
        duplicateCount = 0;
        for (var sampleIndex = 0; sampleIndex < merged.Length; sampleIndex += 1)
        {
            var sample = merged[sampleIndex];
            if (mergedCount > 0 && merged[mergedCount - 1].FrameIndex == sample.FrameIndex)
            {
                duplicateCount += 1;
                merged[mergedCount - 1] = merged[mergedCount - 1] with { CaptureRegion = "recent+hitch_archive" };
                continue;
            }

            merged[mergedCount++] = sample;
        }

        return merged;
    }

    private void WriteSummaryJson(string csvPath, int mergedCount, int duplicateCount)
    {
        var allFrames = _allFrameTiming.GetSummary();
        var activeFrames = _activeGameplayFrameTiming.GetSummary();
        var summary = new
        {
            schema_version = 1,
            capture_start_utc_ticks = _captureStartUtcTicks,
            capture_end_utc_ticks = _captureEndUtcTicks,
            first_frame_index = _firstFrameIndex,
            last_frame_index = _lastFrameIndex,
            total_frame_count = _totalFrameCount,
            active_gameplay_frame_count = _activeGameplayFrameCount,
            total_elapsed_ms = _totalElapsedMilliseconds,
            retained_recent_count = Count,
            recent_overwritten_count = DroppedFrames,
            retained_hitch_count = _hitchCount,
            hitch_archive_capacity = HitchArchiveCapacity,
            hitch_archive_threshold_ms = HitchArchiveThresholdMilliseconds,
            hitch_archive_omitted_count = HitchArchiveOmittedCount,
            merged_csv_row_count = mergedCount,
            merged_duplicate_count = duplicateCount,
            all_frames = CreateIntervalSummary(
                allFrames,
                _totalElapsedMilliseconds,
                _framesAtLeast25Milliseconds,
                _framesAtLeast33Point34Milliseconds,
                _framesAtLeast50Milliseconds),
            active_gameplay = CreateIntervalSummary(
                activeFrames,
                _activeGameplayElapsedMilliseconds,
                _activeFramesAtLeast25Milliseconds,
                _activeFramesAtLeast33Point34Milliseconds,
                _activeFramesAtLeast50Milliseconds),
        };

        var summaryPath = Path.Combine(
            Path.GetDirectoryName(csvPath) ?? string.Empty,
            $"{Path.GetFileNameWithoutExtension(csvPath)}.summary.json");
        var options = new JsonSerializerOptions { WriteIndented = true };
        using var stream = File.Create(summaryPath);
        JsonSerializer.Serialize(stream, summary, options);
    }

    private static object CreateIntervalSummary(
        ClientFrameTimingSummary summary,
        double totalElapsedMilliseconds,
        long framesAtLeast25Milliseconds,
        long framesAtLeast33Point34Milliseconds,
        long framesAtLeast50Milliseconds)
    {
        return new
        {
            sample_count = summary.SampleCount,
            total_elapsed_ms = totalElapsedMilliseconds,
            average_fps = summary.AverageFps,
            p50_ms = summary.P50Milliseconds,
            p95_ms = summary.P95Milliseconds,
            p99_ms = summary.P99Milliseconds,
            max_ms = summary.MaxMilliseconds,
            frames_25ms_or_more = framesAtLeast25Milliseconds,
            frames_33_34ms_or_more = framesAtLeast33Point34Milliseconds,
            frames_50ms_or_more = framesAtLeast50Milliseconds,
        };
    }

    private sealed class ClientFrameCaptureSampleComparer : IComparer<ClientFrameCaptureSample>
    {
        public static readonly ClientFrameCaptureSampleComparer Instance = new();

        public int Compare(ClientFrameCaptureSample left, ClientFrameCaptureSample right)
        {
            return left.FrameIndex.CompareTo(right.FrameIndex);
        }
    }

    private static void WriteSample(TextWriter writer, in ClientFrameCaptureSample sample)
    {
        WriteValue(writer, sample.FrameIndex);
        WriteValue(writer, sample.CompletedUtcTicks);
        WriteValue(writer, sample.CompletedStopwatchTicks);
        WriteValue(writer, sample.DrawIntervalMilliseconds);
        WriteValue(writer, sample.DrawCpuMilliseconds);
        WriteValue(writer, sample.IntervalGapMilliseconds);
        WriteValue(writer, sample.SessionKind);
        WriteValue(writer, sample.Map);
        WriteValue(writer, sample.GameplayActive);
        WriteValue(writer, sample.Loading);
        WriteValue(writer, sample.WindowActive);
        WriteValue(writer, sample.LocalPlayerAwaitingJoin);
        WriteValue(writer, sample.PracticeBotCount);
        WriteValue(writer, sample.EntityCount);
        WriteValue(writer, sample.ViewportWidth);
        WriteValue(writer, sample.ViewportHeight);
        WriteValue(writer, sample.VSync);
        WriteValue(writer, sample.FrameRateLimit);
        WriteValue(writer, sample.AllocatedBytesDelta);
        WriteValue(writer, sample.Gc0Delta);
        WriteValue(writer, sample.Gc1Delta);
        WriteValue(writer, sample.Gc2Delta);
        WriteValue(writer, sample.StageUpdateMilliseconds);
        WriteValue(writer, sample.StageSimulationMilliseconds);
        WriteValue(writer, sample.StagePresentationMilliseconds);
        WriteValue(writer, sample.StageWorldDrawMilliseconds);
        WriteValue(writer, sample.StageHudDrawMilliseconds);
        WriteValue(writer, sample.StageModalDrawMilliseconds);
        WriteValue(writer, sample.StagePluginFrameMilliseconds);
        WriteValue(writer, sample.StagePluginEventsMilliseconds);
        WriteValue(writer, sample.StageInterpolationMilliseconds);
        WriteValue(writer, sample.StageRenderStatesMilliseconds);
        WriteValue(writer, sample.StageMusicMilliseconds);
        WriteValue(writer, sample.StageBotBuildMilliseconds);
        WriteValue(writer, sample.StageBotApplyMilliseconds);
        WriteValue(writer, sample.StageNetworkReceiveMilliseconds);
        WriteValue(writer, sample.StageNetworkResolveMilliseconds);
        WriteValue(writer, sample.StageNetworkApplyMilliseconds);
        writer.Write(sample.DroppedFrames.ToString(CultureInfo.InvariantCulture));
        writer.Write(',');
        WriteCsvString(writer, sample.CaptureRegion);
        writer.Write(',');
        writer.Write(sample.PreviousFrameworkEndDrawMilliseconds.ToString("F3", CultureInfo.InvariantCulture));
        writer.Write(',');
        writer.Write(sample.TextureUploads.ToString(CultureInfo.InvariantCulture));
        writer.Write(',');
        writer.Write(sample.GlFinishMilliseconds.ToString("F3", CultureInfo.InvariantCulture));
        writer.Write(',');
        writer.Write(sample.GpuReadbacks.ToString(CultureInfo.InvariantCulture));
        writer.Write(',');
        writer.Write(sample.GpuReadbackMilliseconds.ToString("F3", CultureInfo.InvariantCulture));
        writer.WriteLine();
    }

    private static void WriteValue(TextWriter writer, long value)
    {
        writer.Write(value.ToString(CultureInfo.InvariantCulture));
        writer.Write(',');
    }

    private static void WriteValue(TextWriter writer, int value)
    {
        writer.Write(value.ToString(CultureInfo.InvariantCulture));
        writer.Write(',');
    }

    private static void WriteValue(TextWriter writer, double value)
    {
        writer.Write(value.ToString("F3", CultureInfo.InvariantCulture));
        writer.Write(',');
    }

    private static void WriteValue(TextWriter writer, bool value)
    {
        writer.Write(value ? "true," : "false,");
    }

    private static void WriteValue(TextWriter writer, string? value)
    {
        WriteCsvString(writer, value);
        writer.Write(',');
    }

    private static void WriteCsvString(TextWriter writer, string? value)
    {
        value ??= string.Empty;
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            writer.Write(value);
            return;
        }

        writer.Write('"');
        for (var index = 0; index < value.Length; index += 1)
        {
            var character = value[index];
            if (character == '"')
            {
                writer.Write('"');
            }

            writer.Write(character);
        }

        writer.Write('"');
    }
}
