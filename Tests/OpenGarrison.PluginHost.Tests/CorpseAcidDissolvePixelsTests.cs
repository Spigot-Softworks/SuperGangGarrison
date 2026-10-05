using System.Diagnostics;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Client;
using Xunit;
using Xunit.Abstractions;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CorpseAcidDissolvePixelsTests
{
    private const int Width = 53;
    private const int Height = 47;
    private readonly ITestOutputHelper _output;

    public CorpseAcidDissolvePixelsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ForwardProgressMatchesFullImageReferenceAndReportsContiguousUploadRows()
    {
        var state = CreateState();
        var simulatedTexture = (Color[])state.SourcePixels.Clone();
        var uploadRegions = new List<Rectangle>();

        foreach (var progress in new[] { 0.18f, 0.183f, 0.29f, 0.72f, 0.78f, 1f })
        {
            if (ApplyAndAssertMatchesFullImage(state, simulatedTexture, progress, out var region))
            {
                uploadRegions.Add(region);
            }
        }

        Assert.Contains(uploadRegions, region => region.Height < Height);
    }

    [Fact]
    public void FrontAndSoftBandBoundaryProgressMatchesFullImageReference()
    {
        var state = CreateState();
        var simulatedTexture = (Color[])state.SourcePixels.Clone();
        const int x = Width / 2;
        const int y = Height / 3;
        var frontOffset = state.ColumnOffsets[x]
            + (MathF.Sin((x * state.WaveFrequency) + state.WavePhase) * state.WaveAmplitude);
        var divisor = state.Height + state.EdgePad;
        var atFrontBoundary = (y - frontOffset) / divisor;
        var atSoftBandBoundary = (y + state.SoftBand - frontOffset) / divisor;

        foreach (var progress in new[] { atFrontBoundary, atSoftBandBoundary })
        {
            ApplyAndAssertMatchesFullImage(state, simulatedTexture, progress, out _);
        }
    }

    [Fact]
    public void RewindingRebuildsPixelsAndRestoresPreviouslyDissolvedParts()
    {
        var state = CreateState();
        var simulatedTexture = (Color[])state.SourcePixels.Clone();
        Assert.True(ApplyAndAssertMatchesFullImage(state, simulatedTexture, 0.82f, out _));
        var beforeRewind = (Color[])state.WorkingPixels.Clone();

        Assert.True(ApplyAndAssertMatchesFullImage(state, simulatedTexture, 0.21f, out var rewindRegion));

        Assert.False(beforeRewind.SequenceEqual(state.WorkingPixels));
        Assert.Equal(new Rectangle(0, 0, Width, Height), rewindRegion);
        Assert.True(ApplyAndAssertMatchesFullImage(state, simulatedTexture, 0.58f, out var forwardRegion));
        Assert.True(forwardRegion.Height < Height);
    }

    [Fact]
    public void UnchangedPixelsDoNotRequestAnUploadOrAdvanceTheEpsilonGuard()
    {
        var state = CreateState();
        var simulatedTexture = (Color[])state.SourcePixels.Clone();
        Assert.True(ApplyAndAssertMatchesFullImage(state, simulatedTexture, 0.5f, out _));

        var lastApplied = state.LastAppliedProgress;
        var before = (Color[])state.WorkingPixels.Clone();
        Assert.False(ApplyAndAssertMatchesFullImage(state, simulatedTexture, 0.5002f, out var epsilonRegion));
        Assert.Equal(Rectangle.Empty, epsilonRegion);
        Assert.Equal(lastApplied, state.LastAppliedProgress);
        Assert.Equal(before, state.WorkingPixels);

        Assert.False(ApplyAndAssertMatchesFullImage(state, simulatedTexture, 0.5f, out var sameRegion));
        Assert.Equal(Rectangle.Empty, sameRegion);
        Assert.Equal(before, state.WorkingPixels);
    }

    [Fact]
    public void ProgressThatDoesNotReachOpaquePixelsSkipsUploadsBeyondEpsilon()
    {
        var state = CreateState(sparseLowerBody: true);
        var simulatedTexture = (Color[])state.SourcePixels.Clone();
        var uploadCalls = 0;
        foreach (var progress in new[] { 0.02f, 0.05f, 0.08f, 0.11f })
        {
            if (ApplyAndAssertMatchesFullImage(state, simulatedTexture, progress, out _))
            {
                uploadCalls += 1;
            }
        }

        Assert.Equal(0, uploadCalls);
        Assert.Equal(0.11f, state.LastAppliedProgress);
        Assert.Equal(state.SourcePixels, simulatedTexture);
    }

    [Fact]
    public void RandomizedDimensionsSeedsJumpsAndRewindsMatchFullImageAndSimulatedUploads()
    {
        foreach (var (width, height, seed) in new[]
        {
            (1, 1, 13),
            (9, 8, 71),
            (31, 19, 109),
            (53, 47, 811),
            (86, 123, 1207),
        })
        {
            var state = CreateState(width, height, seed, randomized: true);
            var simulatedTexture = (Color[])state.SourcePixels.Clone();
            var sequenceRandom = new Random(seed ^ 0x5A17);
            var progress = 0f;
            var progressSequence = new List<float> { 0.015f, 0.19f, 0.61f, 0.93f, 0.27f, 0.29f, 0.79f, 1f, 0.43f };
            for (var index = 0; index < 36; index += 1)
            {
                if (index % 6 == 4)
                {
                    progress = MathF.Max(0f, progress - 0.04f - (sequenceRandom.NextSingle() * 0.65f));
                }
                else
                {
                    progress = MathF.Min(1f, progress + 0.01f + (sequenceRandom.NextSingle() * 0.15f));
                }

                progressSequence.Add(progress);
            }

            foreach (var sample in progressSequence)
            {
                ApplyAndAssertMatchesFullImage(state, simulatedTexture, sample, out _);
            }
        }
    }

    [Fact]
    public void BurnCharredSourceAndWorkingResetStartsANewFullDissolve()
    {
        var state = CreateState();
        var simulatedTexture = (Color[])state.SourcePixels.Clone();
        Assert.True(ApplyAndAssertMatchesFullImage(state, simulatedTexture, 0.79f, out _));

        for (var index = 0; index < state.SourcePixels.Length; index += 1)
        {
            var source = state.SourcePixels[index];
            if (source.A == 0)
            {
                continue;
            }

            state.SourcePixels[index] = new Color((byte)(source.R / 2), (byte)(source.G / 3), (byte)(source.B / 4), source.A);
        }

        // Mirrors ConvertDissolveStateToBurnCharred: soot becomes the new source image.
        Array.Copy(state.SourcePixels, state.WorkingPixels, state.SourcePixels.Length);
        state.LastAppliedProgress = -1f;
        Array.Copy(state.SourcePixels, simulatedTexture, state.SourcePixels.Length);

        Assert.True(ApplyAndAssertMatchesFullImage(state, simulatedTexture, 0.41f, out var region));
        Assert.Equal(new Rectangle(0, 0, Width, Height), region);
    }

    [Fact]
    [Trait("Category", "PerformanceBenchmark")]
    public void DeterministicBenchmarkComparesFullAndIncrementalPixelWork()
    {
        const int width = 192;
        const int height = 144;
        var state = CreateState(width, height, seed: 90210, randomized: true);
        var progressSequence = new List<float>(209);
        for (var index = 1; index <= 200; index += 1)
        {
            progressSequence.Add(index / 200f);
        }

        progressSequence.AddRange(new[] { 0.64f, 0.31f, 0.88f, 0.17f, 1f });
        var noChangeProgressSequence = Enumerable.Range(1, 60).Select(index => index * 0.005f).ToArray();

        // First validate each real helper result and the exact partial-row copy semantics.
        var simulatedTexture = (Color[])state.SourcePixels.Clone();
        foreach (var progress in progressSequence)
        {
            ApplyAndAssertMatchesFullImage(state, simulatedTexture, progress, out _);
        }

        var fullPixels = new Color[state.SourcePixels.Length];
        var fullState = CreateState(width, height, seed: 90210, randomized: true);
        var fullTimer = Stopwatch.StartNew();
        foreach (var progress in progressSequence)
        {
            RebuildFullImageInto(fullState, fullPixels, progress);
        }

        fullTimer.Stop();

        var incrementalState = CreateState(width, height, seed: 90210, randomized: true);
        var incrementalTexture = (Color[])incrementalState.SourcePixels.Clone();
        var uploadCalls = 0;
        var uploadedPixels = 0L;
        var incrementalTimer = Stopwatch.StartNew();
        foreach (var progress in progressSequence)
        {
            if (!Game1.TryApplyCorpseAcidDissolvePixels(incrementalState, progress, out var region))
            {
                continue;
            }

            uploadCalls += 1;
            var startIndex = region.Y * width;
            var elementCount = region.Height * width;
            uploadedPixels += elementCount;
            Array.Copy(incrementalState.WorkingPixels, startIndex, incrementalTexture, startIndex, elementCount);
        }

        incrementalTimer.Stop();
        RebuildFullImageInto(fullState, fullPixels, progressSequence[^1]);
        Assert.Equal(fullPixels, incrementalState.WorkingPixels);
        Assert.Equal(fullPixels, incrementalTexture);

        // A long transparent upper section gives a controlled case where progress changes but
        // the front has not touched visible pixels; legacy code still issued a full SetData.
        var noChangeState = CreateState(width, height, seed: 90210, randomized: true, sparseLowerBody: true);
        var noChangeTexture = (Color[])noChangeState.SourcePixels.Clone();
        var noChangeFullPixels = new Color[noChangeState.SourcePixels.Length];
        var noChangeLegacyTimer = Stopwatch.StartNew();
        foreach (var progress in noChangeProgressSequence)
        {
            RebuildFullImageInto(noChangeState, noChangeFullPixels, progress);
        }

        noChangeLegacyTimer.Stop();
        var noChangeIncrementalTimer = Stopwatch.StartNew();
        var noChangeUploadCalls = 0;
        foreach (var progress in noChangeProgressSequence)
        {
            if (Game1.TryApplyCorpseAcidDissolvePixels(noChangeState, progress, out var region))
            {
                noChangeUploadCalls += 1;
                var startIndex = region.Y * width;
                Array.Copy(noChangeState.WorkingPixels, startIndex, noChangeTexture, startIndex, region.Height * width);
            }
        }

        noChangeIncrementalTimer.Stop();
        Assert.Equal(noChangeFullPixels, noChangeState.WorkingPixels);
        Assert.Equal(noChangeFullPixels, noChangeTexture);
        Assert.Equal(0, noChangeUploadCalls);

        var legacyUploadCalls = progressSequence.Count;
        var legacyUploadedPixels = (long)legacyUploadCalls * width * height;
        var noChangeLegacyUploadPixels = (long)noChangeProgressSequence.Length * width * height;
        var report = new
        {
            width,
            height,
            frameCount = progressSequence.Count,
            legacyUploadCalls,
            incrementalUploadCalls = uploadCalls,
            uploadCallsAvoided = legacyUploadCalls - uploadCalls,
            legacyUploadedPixels,
            incrementalUploadedPixels = uploadedPixels,
            uploadedPixelsAvoided = legacyUploadedPixels - uploadedPixels,
            fullRebuildMilliseconds = fullTimer.Elapsed.TotalMilliseconds,
            incrementalMilliseconds = incrementalTimer.Elapsed.TotalMilliseconds,
            unchangedOutputCase = new
            {
                frameCount = noChangeProgressSequence.Length,
                legacyUploadCalls = noChangeProgressSequence.Length,
                incrementalUploadCalls = noChangeUploadCalls,
                uploadCallsAvoided = noChangeProgressSequence.Length - noChangeUploadCalls,
                legacyUploadedPixels = noChangeLegacyUploadPixels,
                incrementalUploadedPixels = 0,
                uploadedPixelsAvoided = noChangeLegacyUploadPixels,
                fullRebuildMilliseconds = noChangeLegacyTimer.Elapsed.TotalMilliseconds,
                incrementalMilliseconds = noChangeIncrementalTimer.Elapsed.TotalMilliseconds,
                pixelEquality = true,
            },
            pixelEquality = true,
            note = "CPU pixel-processing benchmark; Texture2D/OpenGL transfer and GL synchronization are not included."
        };

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        _output.WriteLine(json);
        var reportPath = Environment.GetEnvironmentVariable("OPENGARRISON_CORPSE_ACID_BENCHMARK_PATH");
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            var fullReportPath = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
            File.WriteAllText(fullReportPath, json);
        }
    }

    private static Game1.CorpseAcidDissolveState CreateState(
        int width = Width,
        int height = Height,
        int seed = 8127,
        bool randomized = false,
        bool sparseLowerBody = false)
    {
        var random = new Random(seed);
        var source = new Color[width * height];
        for (var y = 0; y < height; y += 1)
        {
            for (var x = 0; x < width; x += 1)
            {
                var index = (y * width) + x;
                if ((sparseLowerBody && y < height * 0.65f) || (x * 11 + y * 7 + seed) % (randomized ? 9 : 17) == 0)
                {
                    source[index] = randomized
                        ? new Color((byte)random.Next(1, 256), (byte)random.Next(1, 256), (byte)random.Next(1, 256), (byte)0)
                        : new Color((byte)19, (byte)27, (byte)41, (byte)0);
                    continue;
                }

                source[index] = randomized
                    ? new Color(
                        (byte)random.Next(0, 256),
                        (byte)random.Next(0, 256),
                        (byte)random.Next(0, 256),
                        (byte)random.Next(1, 256))
                    : new Color(
                        (byte)((x * 31 + y * 3) % 256),
                        (byte)((x * 5 + y * 29) % 256),
                        (byte)((x * 17 + y * 13) % 256),
                        (byte)(64 + ((x * 7 + y * 11) % 192)));
            }
        }

        var offsets = new float[width];
        for (var x = 0; x < width; x += 1)
        {
            offsets[x] = randomized ? (random.NextSingle() * 6.5f) - 3.25f : MathF.Sin(x * 0.43f) * 2.9f;
        }

        var softBand = Math.Clamp(height * 0.08f, 2.5f, 5.5f);
        var waveAmplitude = Math.Clamp(height * 0.035f, 1.25f, 3.25f);
        return new Game1.CorpseAcidDissolveState
        {
            Texture = null!,
            SourcePixels = source,
            WorkingPixels = (Color[])source.Clone(),
            ColumnOffsets = offsets,
            Width = width,
            Height = height,
            Origin = Vector2.Zero,
            Scale = 1f,
            OwnsTexture = true,
            WavePhase = randomized ? random.NextSingle() * MathF.Tau : 0.73f,
            WaveAmplitude = waveAmplitude,
            WaveFrequency = MathF.Tau / MathF.Max(10f, width * 0.55f),
            SoftBand = softBand,
            EdgePad = softBand + 3.25f + waveAmplitude + 1f,
            NoiseSeed = seed,
        };
    }

    private static bool ApplyAndAssertMatchesFullImage(
        Game1.CorpseAcidDissolveState state,
        Color[] simulatedTexture,
        float progress,
        out Rectangle region)
    {
        var appliedProgress = Math.Clamp(progress, 0f, 1f);
        if (MathF.Abs(appliedProgress - state.LastAppliedProgress) < 0.0005f)
        {
            appliedProgress = state.LastAppliedProgress;
        }

        var before = (Color[])state.WorkingPixels.Clone();
        var expected = RebuildFullImage(state, appliedProgress);
        var expectedChanged = !before.SequenceEqual(expected);
        var needsUpload = Game1.TryApplyCorpseAcidDissolvePixels(state, progress, out region);

        Assert.Equal(expected, state.WorkingPixels);
        Assert.Equal(expectedChanged, needsUpload);
        if (!needsUpload)
        {
            Assert.Equal(Rectangle.Empty, region);
            Assert.Equal(expected, simulatedTexture);
            return false;
        }

        Assert.Equal(0, region.X);
        Assert.Equal(state.Width, region.Width);
        Assert.True(region.Y >= 0 && region.Bottom <= state.Height);
        var startIndex = region.Y * state.Width;
        var elementCount = region.Height * state.Width;
        Assert.InRange(startIndex, 0, expected.Length - 1);
        Assert.InRange(elementCount, 1, expected.Length - startIndex);

        for (var index = 0; index < expected.Length; index += 1)
        {
            if (before[index] != expected[index])
            {
                Assert.InRange(index / state.Width, region.Y, region.Bottom - 1);
            }
        }

        Array.Copy(state.WorkingPixels, startIndex, simulatedTexture, startIndex, elementCount);
        Assert.Equal(expected, simulatedTexture);
        return true;
    }

    private static Color[] RebuildFullImage(Game1.CorpseAcidDissolveState state, float progress)
    {
        var pixels = new Color[state.SourcePixels.Length];
        RebuildFullImageInto(state, pixels, progress);
        return pixels;
    }

    private static void RebuildFullImageInto(Game1.CorpseAcidDissolveState state, Color[] pixels, float progress)
    {
        Array.Copy(state.SourcePixels, pixels, state.SourcePixels.Length);
        var baseFront = progress * (state.Height + state.EdgePad);
        for (var x = 0; x < state.Width; x += 1)
        {
            var front = baseFront
                + state.ColumnOffsets[x]
                + (MathF.Sin((x * state.WaveFrequency) + state.WavePhase) * state.WaveAmplitude);
            for (var y = 0; y < state.Height; y += 1)
            {
                var index = (y * state.Width) + x;
                if (state.SourcePixels[index].A == 0)
                {
                    continue;
                }

                var intoAcid = front - y;
                if (intoAcid <= 0f)
                {
                    continue;
                }

                if (intoAcid >= state.SoftBand)
                {
                    pixels[index] = Color.Transparent;
                    continue;
                }

                var bandT = intoAcid / state.SoftBand;
                var nibble = Hash01(x, y, state.NoiseSeed);
                var threshold = 0.12f + (bandT * bandT * 0.88f);
                if (nibble < threshold)
                {
                    pixels[index] = Color.Transparent;
                }
            }
        }
    }

    private static float Hash01(int x, int y, int seed)
    {
        unchecked
        {
            var h = (uint)(x * 374761393 + y * 668265263 + seed * 362437);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h & 0xFFFFu) / 65535f;
        }
    }
}
