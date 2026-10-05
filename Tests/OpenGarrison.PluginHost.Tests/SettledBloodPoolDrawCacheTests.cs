using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using OpenGarrison.Client;
using Microsoft.Xna.Framework;
using Xunit;
using Xunit.Abstractions;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SettledBloodPoolDrawCacheTests
{
    private readonly ITestOutputHelper _output;

    public SettledBloodPoolDrawCacheTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void SnapshotCacheHitsIgnoreAgeButDetectVisibleAmountTintAndTopologyChanges()
    {
        var source = new Dictionary<(int X, int Y), Game1.SettledBloodCell>
        {
            [(0, 0)] = new() { Amount = 0.75f, Age = 3, DripProgress = 0.2f },
            [(1, 0)] = new() { Amount = 0.079f, Age = 7 },
        };
        var cache = new SettledBloodPoolDrawCache();
        BuildSnapshot(cache, source);

        Assert.True(cache.TryUse(source));
        source[(0, 0)].Age += 1;
        source[(0, 0)].DripProgress += 0.1f;
        Assert.True(cache.TryUse(source));

        source[(1, 0)].Amount = 0.08f;
        Assert.False(cache.TryUse(source));
        BuildSnapshot(cache, source);

        source[(0, 0)].Amount = MathF.BitIncrement(source[(0, 0)].Amount);
        Assert.False(cache.TryUse(source));
        BuildSnapshot(cache, source);

        source[(0, 0)].ExperimentalCryoTinted = true;
        Assert.False(cache.TryUse(source));
        BuildSnapshot(cache, source);

        source[(2, 0)] = new Game1.SettledBloodCell { Amount = 0.4f };
        Assert.False(cache.TryUse(source));
        BuildSnapshot(cache, source);

        source.Remove((2, 0));
        Assert.False(cache.TryUse(source));
        Assert.Equal(5, cache.BuildCount);
        Assert.Equal(2, cache.CacheHitCount);

        cache.Clear();
        Assert.False(cache.TryUse(source));
        Assert.Empty(cache.NormalCells);
        Assert.Empty(cache.CryoCells);
        Assert.Equal(0, cache.BuildCount);
        Assert.Equal(0, cache.CacheHitCount);
    }

    [Fact]
    public void CachedPoolColorsPreserveLegacyPixelOrderAndCameraRounding()
    {
        var normal = new Dictionary<(int X, int Y), float>
        {
            [(-3, -2)] = 0.4f,
            [(-2, -2)] = 0.9f,
            [(-1, -2)] = 1.2f,
            [(-3, -1)] = 0.75f,
            [(-2, -1)] = 1.8f,
            [(-1, -1)] = 0.6f,
        };
        var cryo = new Dictionary<(int X, int Y), float>
        {
            [(3, 1)] = 0.8f,
            [(4, 1)] = 1.3f,
            [(3, 2)] = 1.7f,
            [(4, 2)] = 2.1f,
        };
        var source = new Dictionary<(int X, int Y), Game1.SettledBloodCell>();
        CopySource(normal, source, cryo: false);
        CopySource(cryo, source, cryo: true);

        var cache = new SettledBloodPoolDrawCache();
        BuildSnapshot(cache, source);
        GameplayGoreEffectsController.AppendSettledBloodRenderCells(normal, cache.NormalCells, useCryoColors: false);
        GameplayGoreEffectsController.AppendSettledBloodRenderCells(cryo, cache.CryoCells, useCryoColors: true);

        var expectedNormal = BuildLegacyRenderCells(normal, cryo: false);
        var expectedCryo = BuildLegacyRenderCells(cryo, cryo: true);
        AssertRenderCellsEqual(expectedNormal, cache.NormalCells);
        AssertRenderCellsEqual(expectedCryo, cache.CryoCells);

        Vector2[] cameras =
        [
            new Vector2(0.49f, 0.51f),
            new Vector2(0.5f, -0.5f),
            new Vector2(6.999f, -2.501f),
            new Vector2(-1.501f, 3.499f),
        ];

        foreach (var camera in cameras)
        {
            var expectedPixels = BuildLegacyPixels(expectedNormal, expectedCryo, camera);
            var actualPixels = BuildCachedPixels(cache.NormalCells, cache.CryoCells, camera);
            Assert.Equal(expectedPixels.Count, actualPixels.Count);
            for (var index = 0; index < expectedPixels.Count; index += 1)
            {
                Assert.Equal(expectedPixels[index], actualPixels[index]);
            }
        }
    }

    [Fact]
    public void ProductionCacheRebuildsAfterVisibleMutationsAndMatchesForcedRebuildOutput()
    {
        var seededSource = new Dictionary<(int X, int Y), Game1.SettledBloodCell>
        {
            [(0, 0)] = new() { Amount = 0.72f },
            [(1, 0)] = new() { Amount = 0.66f },
            [(2, 0)] = new() { Amount = 0.51f },
            [(0, 1)] = new() { Amount = 0.93f },
            [(8, -2)] = new() { Amount = 0.8f, ExperimentalCryoTinted = true },
            [(9, -2)] = new() { Amount = 1.1f, ExperimentalCryoTinted = true },
            [(15, 4)] = new() { Amount = 0.079f },
        };
        var controller = CreateController(seededSource);
        var source = GetSourceCells(controller);
        var cache = GetDrawCache(controller);

        Assert.False(controller.EnsureSettledBloodPoolDrawCache());
        Assert.Equal(1, cache.BuildCount);
        var initialPixels = SnapshotPixels(cache, new Vector2(0.49f, -0.5f));

        Assert.True(controller.EnsureSettledBloodPoolDrawCache());
        source[(0, 0)].Age += 1;
        source[(0, 0)].DripProgress += 0.04f;
        Assert.True(controller.EnsureSettledBloodPoolDrawCache());
        AssertPixelsEqual(initialPixels, SnapshotPixels(cache, new Vector2(0.49f, -0.5f)));
        AssertForcedRebuildMatches(controller, cache);

        source[(1, 0)].Amount = MathF.BitIncrement(source[(1, 0)].Amount);
        Assert.False(controller.EnsureSettledBloodPoolDrawCache());
        AssertForcedRebuildMatches(controller, cache);

        source[(1, 0)].ExperimentalCryoTinted = true;
        Assert.False(controller.EnsureSettledBloodPoolDrawCache());
        AssertForcedRebuildMatches(controller, cache);

        source[(15, 4)].Amount = 0.08f;
        Assert.False(controller.EnsureSettledBloodPoolDrawCache());
        AssertForcedRebuildMatches(controller, cache);

        source[(17, 4)] = new Game1.SettledBloodCell { Amount = 0.44f };
        Assert.False(controller.EnsureSettledBloodPoolDrawCache());
        AssertForcedRebuildMatches(controller, cache);

        source.Remove((17, 4));
        Assert.False(controller.EnsureSettledBloodPoolDrawCache());
        AssertForcedRebuildMatches(controller, cache);

        source[(15, 4)].Amount = 0.079f;
        Assert.False(controller.EnsureSettledBloodPoolDrawCache());
        AssertForcedRebuildMatches(controller, cache);
        var hiddenPixels = SnapshotPixels(cache, new Vector2(-1.501f, 3.499f));
        source[(15, 4)].Amount = 0.02f;
        Assert.True(controller.EnsureSettledBloodPoolDrawCache());
        Assert.Equal(0.02f, source[(15, 4)].Amount);
        AssertPixelsEqual(hiddenPixels, SnapshotPixels(cache, new Vector2(-1.501f, 3.499f)));
    }

    [Fact]
    public void CacheHitPathAllocatesNothingAndReportsInformationalCpuComparison()
    {
        const int iterations = 12;
        var seededSource = CreateBenchmarkPool(width: 37, height: 29, densityPercent: 69);
        var controller = CreateController(seededSource);
        var cache = GetDrawCache(controller);
        Assert.False(controller.EnsureSettledBloodPoolDrawCache());
        Assert.Equal(1, cache.BuildCount);

        var hitChecksum = 0u;
        for (var index = 0; index < 5; index += 1)
        {
            Assert.True(controller.EnsureSettledBloodPoolDrawCache());
        }

        var hitTimer = new Stopwatch();
        hitTimer.Start();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < iterations; iteration += 1)
        {
            if (!controller.EnsureSettledBloodPoolDrawCache())
            {
                throw new InvalidOperationException("Unchanged benchmark source missed the settled-blood render cache.");
            }

            AccumulateColors(cache.NormalCells, ref hitChecksum);
            AccumulateColors(cache.CryoCells, ref hitChecksum);
        }

        hitTimer.Stop();
        var hitAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        var legacyChecksum = 0u;
        for (var warmup = 0; warmup < 2; warmup += 1)
        {
            controller.RebuildSettledBloodPoolDrawCache();
        }

        var legacyTimer = Stopwatch.StartNew();
        for (var iteration = 0; iteration < iterations; iteration += 1)
        {
            controller.RebuildSettledBloodPoolDrawCache();
            AccumulateColors(cache.NormalCells, ref legacyChecksum);
            AccumulateColors(cache.CryoCells, ref legacyChecksum);
        }

        legacyTimer.Stop();
        Assert.NotEqual(0u, hitChecksum);
        Assert.Equal(hitChecksum, legacyChecksum);
        Assert.Equal(1 + 2 + iterations, cache.BuildCount);
        Assert.Equal(5 + iterations, cache.CacheHitCount);
        Assert.True(cache.ComparedSourceCellCount >= (long)(5 + iterations) * seededSource.Count);
        Assert.Equal(0, hitAllocatedBytes);
        var reportPath = Environment.GetEnvironmentVariable("OPENGARRISON_BLOOD_DRAW_BENCHMARK_PATH");
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            var fullPath = Path.GetFullPath(reportPath);
            var parentDirectory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            File.WriteAllText(
                fullPath,
                JsonSerializer.Serialize(new
                {
                    schema = "settled-blood-draw-cache-benchmark/1",
                    iterations,
                    sourceCells = seededSource.Count,
                    renderedCells = cache.NormalCells.Count + cache.CryoCells.Count,
                    cacheBuilds = cache.BuildCount,
                    cacheHits = cache.CacheHitCount,
                    comparedSourceCells = cache.ComparedSourceCellCount,
                    hitPathAllocatedBytes = hitAllocatedBytes,
                    cachedPathMilliseconds = hitTimer.Elapsed.TotalMilliseconds,
                    fullRebuildPathMilliseconds = legacyTimer.Elapsed.TotalMilliseconds,
                    checksum = hitChecksum,
                }, new JsonSerializerOptions { WriteIndented = true }));
        }

        _output.WriteLine(
            $"Settled blood render CPU sample (informational, {iterations} iterations, " +
            $"source={seededSource.Count}, renderCells={cache.NormalCells.Count + cache.CryoCells.Count}): " +
            $"ordered-snapshot/cache-list path={hitTimer.Elapsed.TotalMilliseconds:F3}ms, " +
            $"full four-phase production rebuild path={legacyTimer.Elapsed.TotalMilliseconds:F3}ms, " +
            $"cacheBuilds={cache.BuildCount}, cacheHits={cache.CacheHitCount}, " +
            $"comparedSourceCells={cache.ComparedSourceCellCount}, hitPathAllocatedBytes={hitAllocatedBytes}.");
    }

    private static void BuildSnapshot(
        SettledBloodPoolDrawCache cache,
        Dictionary<(int X, int Y), Game1.SettledBloodCell> source)
    {
        cache.BeginBuild();
        foreach (var entry in source)
        {
            if (entry.Value.Amount < 0.08f)
            {
                continue;
            }

            cache.CaptureSourceCell(entry.Key, entry.Value.Amount, entry.Value.ExperimentalCryoTinted);
        }

        cache.CompleteBuild();
    }

    private static void CopySource(
        Dictionary<(int X, int Y), float> cells,
        Dictionary<(int X, int Y), Game1.SettledBloodCell> source,
        bool cryo)
    {
        foreach (var entry in cells)
        {
            source.Add(entry.Key, new Game1.SettledBloodCell
            {
                Amount = entry.Value,
                ExperimentalCryoTinted = cryo,
            });
        }
    }

    private static List<SettledBloodRenderCell> BuildLegacyRenderCells(
        Dictionary<(int X, int Y), float> cells,
        bool cryo)
    {
        var rendered = new List<SettledBloodRenderCell>();
        foreach (var ((gx, gy), _) in cells)
        {
            var isOutline = !cells.ContainsKey((gx - 1, gy))
                || !cells.ContainsKey((gx + 1, gy))
                || !cells.ContainsKey((gx, gy - 1))
                || !cells.ContainsKey((gx, gy + 1));
            var color = cryo
                ? isOutline
                    ? new Color(140, 195, 220)
                    : GameplayGoreEffectsController.ResolveSettledBloodFillColor(cells, gx, gy, cryo: true, flight: false)
                : isOutline
                    ? new Color(145, 8, 14)
                    : GameplayGoreEffectsController.ResolveSettledBloodFillColor(cells, gx, gy, cryo: false, flight: false);
            rendered.Add(new SettledBloodRenderCell(gx, gy, color));
        }

        return rendered;
    }

    private static void AssertRenderCellsEqual(
        IReadOnlyList<SettledBloodRenderCell> expected,
        IReadOnlyList<SettledBloodRenderCell> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index += 1)
        {
            Assert.Equal(expected[index].X, actual[index].X);
            Assert.Equal(expected[index].Y, actual[index].Y);
            Assert.Equal(expected[index].Color.R, actual[index].Color.R);
            Assert.Equal(expected[index].Color.G, actual[index].Color.G);
            Assert.Equal(expected[index].Color.B, actual[index].Color.B);
            Assert.Equal(expected[index].Color.A, actual[index].Color.A);
        }
    }

    private static List<Pixel> BuildLegacyPixels(
        IReadOnlyList<SettledBloodRenderCell> normal,
        IReadOnlyList<SettledBloodRenderCell> cryo,
        Vector2 camera)
    {
        var result = new List<Pixel>(normal.Count + cryo.Count);
        AddLegacyPixels(result, normal, camera);
        AddLegacyPixels(result, cryo, camera);
        return result;
    }

    private static List<Pixel> BuildCachedPixels(
        IReadOnlyList<SettledBloodRenderCell> normal,
        IReadOnlyList<SettledBloodRenderCell> cryo,
        Vector2 camera)
    {
        var result = new List<Pixel>(normal.Count + cryo.Count);
        AddCachedPixels(result, normal, camera);
        AddCachedPixels(result, cryo, camera);
        return result;
    }

    private static void AddLegacyPixels(List<Pixel> pixels, IReadOnlyList<SettledBloodRenderCell> cells, Vector2 camera)
    {
        const int cellSize = 2;
        foreach (var cell in cells)
        {
            var color = cell.Color;
            pixels.Add(new Pixel(
                (int)MathF.Round((cell.X * cellSize) - camera.X),
                (int)MathF.Round((cell.Y * cellSize) - camera.Y),
                cellSize,
                cellSize,
                color.R,
                color.G,
                color.B,
                color.A));
        }
    }

    private static void AddCachedPixels(List<Pixel> pixels, IReadOnlyList<SettledBloodRenderCell> cells, Vector2 camera)
    {
        const int cellSize = 2;
        foreach (var cell in cells)
        {
            var rect = GameplayGoreEffectsController.GetSettledBloodRenderRectangle(cell, camera, cellSize);
            var color = cell.Color;
            pixels.Add(new Pixel(rect.X, rect.Y, rect.Width, rect.Height, color.R, color.G, color.B, color.A));
        }
    }

    private static Dictionary<(int X, int Y), Game1.SettledBloodCell> CreateBenchmarkPool(
        int width,
        int height,
        int densityPercent)
    {
        var result = new Dictionary<(int X, int Y), Game1.SettledBloodCell>();
        var state = 197903u;
        for (var y = -height / 2; y < (height + 1) / 2; y += 1)
        {
            for (var x = -width / 2; x < (width + 1) / 2; x += 1)
            {
                state = (state * 1664525u) + 1013904223u;
                if ((state % 100u) >= (uint)densityPercent)
                {
                    continue;
                }

                state = (state * 1664525u) + 1013904223u;
                result.Add((x, y), new Game1.SettledBloodCell
                {
                    Amount = 0.2f + ((state % 18u) * 0.11f),
                    ExperimentalCryoTinted = (state & 7u) == 0u,
                });
            }
        }

        return result;
    }

    private static GameplayGoreEffectsController CreateController(
        Dictionary<(int X, int Y), Game1.SettledBloodCell> source)
    {
        var controller = new GameplayGoreEffectsController(null!);
        var bloodField = typeof(GameplayGoreEffectsController).GetField(
            "_settledBloodCells",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var cells = Assert.IsType<Dictionary<(int X, int Y), Game1.SettledBloodCell>>(
            bloodField!.GetValue(controller));
        foreach (var entry in source)
        {
            cells.Add(entry.Key, new Game1.SettledBloodCell
            {
                Amount = entry.Value.Amount,
                Age = entry.Value.Age,
                DripProgress = entry.Value.DripProgress,
                ExperimentalCryoTinted = entry.Value.ExperimentalCryoTinted,
            });
        }

        return controller;
    }

    private static SettledBloodPoolDrawCache GetDrawCache(GameplayGoreEffectsController controller)
    {
        var cacheField = typeof(GameplayGoreEffectsController).GetField(
            "_settledBloodPoolDrawCache",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return Assert.IsType<SettledBloodPoolDrawCache>(cacheField!.GetValue(controller));
    }

    private static Dictionary<(int X, int Y), Game1.SettledBloodCell> GetSourceCells(
        GameplayGoreEffectsController controller)
    {
        var sourceField = typeof(GameplayGoreEffectsController).GetField(
            "_settledBloodCells",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return Assert.IsType<Dictionary<(int X, int Y), Game1.SettledBloodCell>>(sourceField!.GetValue(controller));
    }

    private static List<Pixel> SnapshotPixels(SettledBloodPoolDrawCache cache, Vector2 camera)
    {
        return BuildCachedPixels(cache.NormalCells, cache.CryoCells, camera);
    }

    private static void AssertPixelsEqual(IReadOnlyList<Pixel> expected, IReadOnlyList<Pixel> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index += 1)
        {
            Assert.Equal(expected[index], actual[index]);
        }
    }

    private static void AssertForcedRebuildMatches(
        GameplayGoreEffectsController controller,
        SettledBloodPoolDrawCache cache)
    {
        var normalBefore = new List<SettledBloodRenderCell>(cache.NormalCells);
        var cryoBefore = new List<SettledBloodRenderCell>(cache.CryoCells);
        Vector2[] cameras =
        [
            new Vector2(0.49f, -0.5f),
            new Vector2(-1.501f, 3.499f),
        ];
        var beforePixels = new List<List<Pixel>>(cameras.Length);
        foreach (var camera in cameras)
        {
            beforePixels.Add(BuildCachedPixels(normalBefore, cryoBefore, camera));
        }

        controller.RebuildSettledBloodPoolDrawCache();
        AssertRenderCellsEqual(normalBefore, cache.NormalCells);
        AssertRenderCellsEqual(cryoBefore, cache.CryoCells);
        for (var index = 0; index < cameras.Length; index += 1)
        {
            AssertPixelsEqual(beforePixels[index], BuildCachedPixels(cache.NormalCells, cache.CryoCells, cameras[index]));
        }
    }

    private static void AccumulateColors(IReadOnlyList<SettledBloodRenderCell> cells, ref uint checksum)
    {
        for (var index = 0; index < cells.Count; index += 1)
        {
            checksum = unchecked((checksum * 16777619u) ^ cells[index].Color.PackedValue ^ (uint)cells[index].X ^ (uint)cells[index].Y);
        }
    }

    private readonly record struct Pixel(int X, int Y, int Width, int Height, byte R, byte G, byte B, byte A);
}
