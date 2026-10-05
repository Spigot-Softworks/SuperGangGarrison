using System.Globalization;
using System.Text;
using System.Text.Json;
using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ClientFrameCaptureRecorderTests
{
    [Fact]
    public void StageAccumulatorSumsRepeatedMetricSamplesAndResetsAtDrawBoundary()
    {
        var stages = new ClientFrameCaptureStageAccumulator();
        stages.Record(Game1.ClientPerformanceMetric.Update, 2.5d);
        stages.Record(Game1.ClientPerformanceMetric.Update, 1.25d);
        stages.Record(Game1.ClientPerformanceMetric.Simulation, 0.75d);

        Assert.Equal(3.75d, stages.GetMilliseconds(Game1.ClientPerformanceMetric.Update));
        Assert.Equal(0.75d, stages.GetMilliseconds(Game1.ClientPerformanceMetric.Simulation));

        stages.Reset();
        stages.Record(Game1.ClientPerformanceMetric.Update, 4d);

        Assert.Equal(4d, stages.GetMilliseconds(Game1.ClientPerformanceMetric.Update));
        Assert.Equal(0d, stages.GetMilliseconds(Game1.ClientPerformanceMetric.Simulation));
    }

    [Fact]
    public void CsvKeepsNewestRowsInOrderAndWritesInvariantEscapedFields()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var path = Path.Combine(Path.GetTempPath(), $"og-frame-capture-{Guid.NewGuid():N}.csv");
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var recorder = new ClientFrameCaptureRecorder(capacity: 2);
            recorder.Add(CreateSample(1, "Old map"));
            recorder.Add(CreateSample(2, "Map, \"One\"\ncontinued"));
            recorder.Add(CreateSample(3, "Current map"));

            recorder.WriteCsv(path);
            var records = ParseCsv(File.ReadAllText(path));

            Assert.Equal(1, recorder.DroppedFrames);
            Assert.Equal(3, records.Count);
            var expectedColumnCount = records[0].Count;
            Assert.Equal(45, expectedColumnCount);
            Assert.Equal("previous_framework_end_draw_ms", records[0][^5]);
            Assert.Equal("gpu_readback_ms", records[0][^1]);
            Assert.All(records, record => Assert.Equal(expectedColumnCount, record.Count));
            Assert.Equal("2", records[1][0]);
            Assert.Equal("Map, \"One\"\ncontinued", records[1][7]);
            Assert.Equal("3", records[2][0]);
            Assert.Equal("1.250", records[1][4]);
            Assert.Equal("0", records[1][^7]);
            Assert.Equal("1", records[2][^7]);
            Assert.Equal("recent", records[1][^6]);
            Assert.Equal("recent", records[2][^6]);
            Assert.Equal("0.000", records[1][^5]);
            Assert.Equal("0.000", records[2][^5]);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void CsvSerializesPreviousFrameworkEndDrawAsInvariantColumn()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var path = Path.Combine(Path.GetTempPath(), $"og-frame-capture-enddraw-{Guid.NewGuid():N}.csv");
        var summaryPath = Path.ChangeExtension(path, ".summary.json");
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var recorder = new ClientFrameCaptureRecorder(capacity: 2);
            var sample = CreateSample(1, "Map") with { PreviousFrameworkEndDrawMilliseconds = 2.375d };
            recorder.Add(sample);

            recorder.WriteCsv(path);
            var records = ParseCsv(File.ReadAllText(path));
            var expectedColumnCount = records[0].Count;

            Assert.Equal("previous_framework_end_draw_ms", records[0][^5]);
            Assert.Equal(45, expectedColumnCount);
            Assert.All(records, record => Assert.Equal(expectedColumnCount, record.Count));
            Assert.Equal("2.375", records[1][^5]);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(summaryPath)) File.Delete(summaryPath);
        }
    }

    [Fact]
    public void CsvSerializesGlSyncCountersAsInvariantFinalColumns()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var path = Path.Combine(Path.GetTempPath(), $"og-frame-capture-glsync-{Guid.NewGuid():N}.csv");
        var summaryPath = Path.ChangeExtension(path, ".summary.json");
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var recorder = new ClientFrameCaptureRecorder(capacity: 2);
            var sample = CreateSample(1, "Map") with
            {
                TextureUploads = 3,
                GlFinishMilliseconds = 12.5d,
                GpuReadbacks = 1,
                GpuReadbackMilliseconds = 31.25d,
            };
            recorder.Add(sample);

            recorder.WriteCsv(path);
            var records = ParseCsv(File.ReadAllText(path));

            Assert.Equal(
                new[] { "texture_uploads", "gl_finish_ms", "gpu_readbacks", "gpu_readback_ms" },
                records[0].Skip(records[0].Count - 4).ToArray());
            Assert.Equal(
                new[] { "3", "12.500", "1", "31.250" },
                records[1].Skip(records[1].Count - 4).ToArray());
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(summaryPath)) File.Delete(summaryPath);
        }
    }

    [Fact]
    public void HitchArchiveAndSidecarPreserveWholeCaptureStatisticsBeyondRecentWindow()
    {
        var path = Path.Combine(Path.GetTempPath(), $"og-frame-capture-{Guid.NewGuid():N}.csv");
        var summaryPath = Path.ChangeExtension(path, ".summary.json");
        try
        {
            var recorder = new ClientFrameCaptureRecorder(capacity: 2, hitchArchiveCapacity: 2);
            recorder.Add(CreateSample(1, "Map", drawIntervalMilliseconds: 0d));
            recorder.Add(CreateSample(2, "Map", drawIntervalMilliseconds: 25d));
            recorder.Add(CreateSample(3, "Map", drawIntervalMilliseconds: 16d));
            recorder.Add(CreateSample(4, "Map", drawIntervalMilliseconds: 34d));
            recorder.Add(CreateSample(5, "Map", drawIntervalMilliseconds: 50d));

            recorder.WriteCsv(path);
            var rows = ParseCsv(File.ReadAllText(path));
            using var summary = JsonDocument.Parse(File.ReadAllText(summaryPath));
            var root = summary.RootElement;

            Assert.Equal(new[] { "frame_index", "2", "4", "5" }, rows.Select(row => row[0]).ToArray());
            var captureRegion = rows[0].IndexOf("capture_region");
            Assert.True(captureRegion >= 0);
            Assert.Equal("hitch_archive", rows[1][captureRegion]);
            Assert.Equal("recent+hitch_archive", rows[2][captureRegion]);
            Assert.Equal("recent", rows[3][captureRegion]);
            Assert.Equal(3, root.GetProperty("merged_csv_row_count").GetInt32());
            Assert.Equal(1, root.GetProperty("merged_duplicate_count").GetInt32());
            Assert.Equal(3, root.GetProperty("recent_overwritten_count").GetInt64());
            Assert.Equal(2, root.GetProperty("retained_hitch_count").GetInt32());
            Assert.Equal(1, root.GetProperty("hitch_archive_omitted_count").GetInt64());

            var allFrames = root.GetProperty("all_frames");
            Assert.Equal(4, allFrames.GetProperty("sample_count").GetInt64());
            Assert.Equal(125d, allFrames.GetProperty("total_elapsed_ms").GetDouble());
            Assert.Equal(3, allFrames.GetProperty("frames_25ms_or_more").GetInt64());
            Assert.Equal(2, allFrames.GetProperty("frames_33_34ms_or_more").GetInt64());
            Assert.Equal(1, allFrames.GetProperty("frames_50ms_or_more").GetInt64());

            var activeGameplay = root.GetProperty("active_gameplay");
            Assert.Equal(4, activeGameplay.GetProperty("sample_count").GetInt64());
            Assert.Equal(125d, activeGameplay.GetProperty("total_elapsed_ms").GetDouble());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(summaryPath)) File.Delete(summaryPath);
        }
    }

    [Fact]
    public void ActiveGameplayHistogramRequiresBothIntervalEndpointsToBeActive()
    {
        var path = Path.Combine(Path.GetTempPath(), $"og-frame-capture-{Guid.NewGuid():N}.csv");
        var summaryPath = Path.ChangeExtension(path, ".summary.json");
        try
        {
            var recorder = new ClientFrameCaptureRecorder(capacity: 8, hitchArchiveCapacity: 8);
            recorder.Add(CreateSample(1, "Map", drawIntervalMilliseconds: 0d));
            recorder.Add(CreateSample(2, "Map", drawIntervalMilliseconds: 60d, loading: true));
            recorder.Add(CreateSample(3, "Map", drawIntervalMilliseconds: 50d));
            recorder.Add(CreateSample(4, "Map", drawIntervalMilliseconds: 16d));
            recorder.Add(CreateSample(5, "Map", drawIntervalMilliseconds: 100d, windowActive: false));
            recorder.Add(CreateSample(6, "Map", drawIntervalMilliseconds: 50d));
            recorder.Add(CreateSample(7, "Map", drawIntervalMilliseconds: 16d));

            recorder.WriteCsv(path);
            using var summary = JsonDocument.Parse(File.ReadAllText(summaryPath));
            var root = summary.RootElement;

            var allFrames = root.GetProperty("all_frames");
            Assert.Equal(6, allFrames.GetProperty("sample_count").GetInt64());
            Assert.Equal(292d, allFrames.GetProperty("total_elapsed_ms").GetDouble());

            var activeGameplay = root.GetProperty("active_gameplay");
            Assert.Equal(2, activeGameplay.GetProperty("sample_count").GetInt64());
            Assert.Equal(32d, activeGameplay.GetProperty("total_elapsed_ms").GetDouble());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(summaryPath)) File.Delete(summaryPath);
        }
    }

    private static ClientFrameCaptureSample CreateSample(
        long frameIndex,
        string map,
        double drawIntervalMilliseconds = 16.667d,
        bool loading = false,
        bool windowActive = true)
    {
        return default(ClientFrameCaptureSample) with
        {
            FrameIndex = frameIndex,
            CompletedUtcTicks = frameIndex * 10,
            CompletedStopwatchTicks = frameIndex * 20,
            DrawIntervalMilliseconds = drawIntervalMilliseconds,
            DrawCpuMilliseconds = 1.25d,
            IntervalGapMilliseconds = 4.5d,
            SessionKind = "Practice",
            Map = map,
            GameplayActive = true,
            Loading = loading,
            WindowActive = windowActive,
            LocalPlayerAwaitingJoin = false,
            PracticeBotCount = 2,
            EntityCount = 24,
            ViewportWidth = 1280,
            ViewportHeight = 720,
            VSync = true,
            FrameRateLimit = 144,
            AllocatedBytesDelta = 256,
            Gc0Delta = 0,
            Gc1Delta = 0,
            Gc2Delta = 0,
            StageUpdateMilliseconds = 3.25d,
            StageSimulationMilliseconds = 1.5d,
            StagePresentationMilliseconds = 0.5d,
            StageWorldDrawMilliseconds = 0.75d,
            StageHudDrawMilliseconds = 0.25d,
            StageModalDrawMilliseconds = 0d,
            StagePluginFrameMilliseconds = 0d,
            StagePluginEventsMilliseconds = 0d,
            StageInterpolationMilliseconds = 0d,
            StageRenderStatesMilliseconds = 0d,
            StageMusicMilliseconds = 0d,
            StageBotBuildMilliseconds = 0d,
            StageBotApplyMilliseconds = 0d,
            StageNetworkReceiveMilliseconds = 0d,
            StageNetworkResolveMilliseconds = 0d,
            StageNetworkApplyMilliseconds = 0d,
            DroppedFrames = 0,
        };
    }

    private static List<List<string>> ParseCsv(string content)
    {
        var rows = new List<List<string>>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < content.Length; index += 1)
        {
            var character = content[index];
            if (inQuotes)
            {
                if (character == '"' && index + 1 < content.Length && content[index + 1] == '"')
                {
                    field.Append('"');
                    index += 1;
                }
                else if (character == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    field.Append(character);
                }

                continue;
            }

            if (character == '"' && field.Length == 0)
            {
                inQuotes = true;
            }
            else if (character == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else if (character == '\r' || character == '\n')
            {
                if (character == '\r' && index + 1 < content.Length && content[index + 1] == '\n')
                {
                    index += 1;
                }

                fields.Add(field.ToString());
                field.Clear();
                rows.Add(fields);
                fields = new List<string>();
            }
            else
            {
                field.Append(character);
            }
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            rows.Add(fields);
        }

        return rows;
    }
}
