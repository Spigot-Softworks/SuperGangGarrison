using System;
using System.Collections.Generic;
using System.Linq;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;
using Xunit.Abstractions;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SettledBloodHotPathTests
{
    private readonly ITestOutputHelper _output;

    public SettledBloodHotPathTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void HostSolidCacheMatchesOrderedLegacySearchAndReusesLiveCellResults()
    {
        var solids = new[]
        {
            new LevelSolid(0f, 0f, 20f, 20f),
            new LevelSolid(4f, 4f, 20f, 20f),
            new LevelSolid(40f, 22f, 20f, 20f),
            new LevelSolid(40f, 22f, 28f, 24f),
        };
        var level = CreateLevel(solids);
        var cache = new SettledBloodHostSolidCache();
        cache.PrepareForLevel(level);
        (int X, int Y)[] cells = [(2, 2), (10, 2), (20, 10), (50, 50)];

        foreach (var cell in cells)
        {
            AssertCacheMatchesLegacy(cache, level, solids, cell);
        }

        for (var tick = 0; tick < 120; tick += 1)
        {
            foreach (var cell in cells)
            {
                AssertCacheMatchesLegacy(cache, level, solids, cell);
            }
        }

        Assert.Equal(cells.Length, cache.GeometryQueryCount);
        Assert.Equal(cells.Length, cache.CachedCellCount);

        var queriesBeforeForget = cache.GeometryQueryCount;
        cache.ForgetCell(cells[^1]);
        Assert.Equal(cells.Length - 1, cache.CachedCellCount);
        AssertCacheMatchesLegacy(cache, level, solids, cells[^1]);
        Assert.Equal(queriesBeforeForget + 1, cache.GeometryQueryCount);
    }

    [Fact]
    public void HostSolidCacheInvalidatesForInPlaceGeometryChangesAndLevelChanges()
    {
        var solids = new[]
        {
            new LevelSolid(0f, 0f, 20f, 20f),
            new LevelSolid(4f, 4f, 20f, 20f),
        };
        var level = CreateLevel(solids);
        var cache = new SettledBloodHostSolidCache();

        Assert.True(cache.TryFindHostSolid(level, 2, 2, SettledBloodConstants.CellSize, out var firstHost));
        Assert.Equal(solids[0], firstHost);

        solids[0] = new LevelSolid(200f, 200f, 20f, 20f);
        cache.PrepareForLevel(level);
        Assert.True(cache.TryFindHostSolid(level, 2, 2, SettledBloodConstants.CellSize, out var updatedHost));
        Assert.Equal(solids[1], updatedHost);
        Assert.Equal(1, cache.GeometryQueryCount);

        var replacementLevel = CreateLevel([new LevelSolid(0f, 0f, 12f, 12f)]);
        Assert.True(cache.TryFindHostSolid(
            replacementLevel,
            2,
            2,
            SettledBloodConstants.CellSize,
            out var replacementHost));
        Assert.Equal(new LevelSolid(0f, 0f, 12f, 12f), replacementHost);
        Assert.Equal(1, cache.GeometryQueryCount);
        Assert.Equal(1, cache.CachedCellCount);

        cache.Clear();
        Assert.Equal(0, cache.CachedCellCount);
        Assert.Equal(0, cache.GeometryQueryCount);
        Assert.True(cache.TryFindHostSolid(
            replacementLevel,
            2,
            2,
            SettledBloodConstants.CellSize,
            out replacementHost));
        Assert.Equal(1, cache.GeometryQueryCount);
    }

    [Fact]
    public void MorphologicalCloseMemoizationIsBitExactAcrossDeterministicPoolShapes()
    {
        var poolCases = new (string Name, Dictionary<(int X, int Y), float> Cells)[]
        {
            ("empty", new Dictionary<(int X, int Y), float>()),
            ("sparse-and-threshold", CreateSparseThresholdBloodPool()),
            ("dense-pattern", CreateDeterministicBloodPool()),
            ("random-seed-7", CreateRandomBloodPool(7, width: 23, height: 17, densityPercent: 41)),
            ("random-seed-91", CreateRandomBloodPool(91, width: 31, height: 19, densityPercent: 28)),
            ("random-seed-2047", CreateRandomBloodPool(2047, width: 17, height: 29, densityPercent: 56)),
        };

        foreach (var (name, cells) in poolCases)
        {
            var expected = BuildLegacyMorphologicalCloseBridges(cells);
            var actual = new Dictionary<(int X, int Y), float>();
            var supportCache = new SettledBloodNeighbourSupportCache();

            SettledBloodMorphologicalClose.BuildBridges(
                cells,
                actual,
                supportCache,
                SettledBloodConstants.MaxAmount);

            AssertDictionariesBitExactAndOrdered(expected, actual);
            Assert.True(supportCache.LookupCount >= supportCache.ComputedSupportCount);
            if (name is "dense-pattern" or "random-seed-7" or "random-seed-91" or "random-seed-2047")
            {
                Assert.True(supportCache.LookupCount > supportCache.ComputedSupportCount);
            }

            _output.WriteLine(
                $"Deterministic blood smoothing work ({name}): sourceCells={cells.Count}, " +
                $"bridgeCells={actual.Count}, candidateLookups={supportCache.LookupCount}, " +
                $"uniqueNeighbourScans={supportCache.ComputedSupportCount}, " +
                $"legacyNeighbourDictionaryReads={supportCache.LookupCount * 8}, " +
                $"memoizedNeighbourDictionaryReads={supportCache.ComputedSupportCount * 8}.");
        }
    }

    private static SimpleLevel CreateLevel(IReadOnlyList<LevelSolid> solids)
    {
        return new SimpleLevel(
            name: "settled_blood_lookup_test",
            mode: GameModeKind.CaptureTheFlag,
            bounds: new WorldBounds(512f, 512f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 0,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(32f, 32f),
            redSpawns: Array.Empty<SpawnPoint>(),
            blueSpawns: Array.Empty<SpawnPoint>(),
            intelBases: Array.Empty<IntelBaseMarker>(),
            roomObjects: Array.Empty<RoomObjectMarker>(),
            floorY: 0f,
            solids: solids,
            importedFromSource: false);
    }

    private static void AssertCacheMatchesLegacy(
        SettledBloodHostSolidCache cache,
        SimpleLevel level,
        IReadOnlyList<LevelSolid> solids,
        (int X, int Y) cell)
    {
        var worldX = (cell.X * 2f) + 1f;
        var worldY = (cell.Y * 2f) + 1f;
        var legacyFound = TryFindLegacyHostSolid(solids, worldX, worldY, out var legacySolid);
        var cachedFound = cache.TryFindHostSolid(
            level,
            cell.X,
            cell.Y,
            SettledBloodConstants.CellSize,
            out var cachedSolid);

        Assert.Equal(legacyFound, cachedFound);
        if (legacyFound)
        {
            Assert.Equal(legacySolid, cachedSolid);
        }
    }

    private static bool TryFindLegacyHostSolid(
        IReadOnlyList<LevelSolid> solids,
        float worldX,
        float worldY,
        out LevelSolid hostSolid)
    {
        for (var index = 0; index < solids.Count; index += 1)
        {
            var candidate = solids[index];
            if (worldX >= candidate.Left
                && worldX < candidate.Right
                && worldY >= candidate.Top
                && worldY < candidate.Bottom)
            {
                hostSolid = candidate;
                return true;
            }
        }

        var probeY = worldY + 2f;
        for (var index = 0; index < solids.Count; index += 1)
        {
            var candidate = solids[index];
            if (worldX < candidate.Left || worldX >= candidate.Right)
            {
                continue;
            }

            if (worldY <= candidate.Top + 0.01f
                && probeY >= candidate.Top
                && worldY >= candidate.Top - 2f)
            {
                hostSolid = candidate;
                return true;
            }
        }

        hostSolid = default;
        return false;
    }

    private static Dictionary<(int X, int Y), float> CreateDeterministicBloodPool()
    {
        var cells = new Dictionary<(int X, int Y), float>();
        for (var gy = -6; gy < 22; gy += 1)
        {
            for (var gx = -12; gx < 48; gx += 1)
            {
                var pattern = ((gx * 31) + (gy * 17)) & 7;
                if (pattern is 0 or 3)
                {
                    continue;
                }

                cells[(gx, gy)] = 0.21f + (pattern * 0.13f);
            }
        }

        return cells;
    }

    private static Dictionary<(int X, int Y), float> CreateSparseThresholdBloodPool()
    {
        return new Dictionary<(int X, int Y), float>
        {
            [(-8, -2)] = 0.1999f,
            [(-6, -2)] = 0.2f,
            [(0, 0)] = 0.2f,
            [(2, 0)] = 0.2f,
            [(12, 4)] = 0.85f,
            [(13, 4)] = 0.21f,
            [(14, 5)] = 0.37f,
        };
    }

    private static Dictionary<(int X, int Y), float> CreateRandomBloodPool(
        int seed,
        int width,
        int height,
        int densityPercent)
    {
        var cells = new Dictionary<(int X, int Y), float>();
        var state = unchecked((uint)seed);
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
                var amountSteps = (int)(state % 18u);
                var amount = amountSteps == 0
                    ? 0.1999f
                    : amountSteps == 1
                        ? 0.2f
                        : 0.2f + (amountSteps * 0.11f);
                cells[(x, y)] = amount;
            }
        }

        return cells;
    }

    private static Dictionary<(int X, int Y), float> BuildLegacyMorphologicalCloseBridges(
        Dictionary<(int X, int Y), float> cells)
    {
        var bridgeScratch = new Dictionary<(int X, int Y), float>();
        foreach (var ((gx, gy), amount) in cells)
        {
            if (amount < 0.2f)
            {
                continue;
            }

            for (var offsetY = -1; offsetY <= 2; offsetY += 1)
            {
                for (var offsetX = -3; offsetX <= 3; offsetX += 1)
                {
                    if (offsetX == 0 && offsetY == 0)
                    {
                        continue;
                    }

                    var key = (gx + offsetX, gy + offsetY);
                    if (cells.ContainsKey(key))
                    {
                        continue;
                    }

                    var neighbourSupport = 0f;
                    var neighbourCount = 0;
                    for (var ny = -1; ny <= 1; ny += 1)
                    {
                        for (var nx = -1; nx <= 1; nx += 1)
                        {
                            if (nx == 0 && ny == 0)
                            {
                                continue;
                            }

                            if (!cells.TryGetValue((key.Item1 + nx, key.Item2 + ny), out var neighbourAmount)
                                || neighbourAmount < 0.2f)
                            {
                                continue;
                            }

                            neighbourSupport += neighbourAmount;
                            neighbourCount += 1;
                        }
                    }

                    if (neighbourCount < 2)
                    {
                        continue;
                    }

                    var fillAmount = MathF.Min(2.4f, neighbourSupport / Math.Max(2, neighbourCount));
                    if (offsetY > 0)
                    {
                        fillAmount *= 0.65f;
                    }

                    if (offsetY == 0)
                    {
                        fillAmount = MathF.Max(fillAmount, 0.85f);
                    }

                    if (fillAmount < 0.2f)
                    {
                        continue;
                    }

                    if (!bridgeScratch.TryGetValue(key, out var existing) || fillAmount > existing)
                    {
                        bridgeScratch[key] = fillAmount;
                    }
                }
            }
        }

        return bridgeScratch;
    }

    private static void AssertDictionariesBitExactAndOrdered(
        Dictionary<(int X, int Y), float> expected,
        Dictionary<(int X, int Y), float> actual)
    {
        var expectedEntries = expected.ToArray();
        var actualEntries = actual.ToArray();
        Assert.Equal(expectedEntries.Length, actualEntries.Length);
        for (var index = 0; index < expectedEntries.Length; index += 1)
        {
            Assert.Equal(expectedEntries[index].Key, actualEntries[index].Key);
            Assert.Equal(
                BitConverter.SingleToInt32Bits(expectedEntries[index].Value),
                BitConverter.SingleToInt32Bits(actualEntries[index].Value));
        }
    }
}
