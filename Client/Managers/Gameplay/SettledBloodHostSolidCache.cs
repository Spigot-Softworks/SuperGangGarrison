using System.Collections.Generic;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

internal static class SettledBloodConstants
{
    internal const float CellSize = 2f;
    internal const float MaxAmount = 2.4f;
}

/// <summary>
/// Reuses the settled-blood host-solid lookup while its grid cell and level
/// geometry stay unchanged. Search order intentionally matches the original
/// ordered scan of <see cref="SimpleLevel.Solids"/>.
/// </summary>
internal sealed class SettledBloodHostSolidCache
{
    private readonly Dictionary<(int X, int Y), LevelSolid?> _hostSolidsByCell = new();
    private SimpleLevel? _level;
    private LevelSolid[]? _solidSnapshot;

    internal int GeometryQueryCount { get; private set; }

    internal int CachedCellCount => _hostSolidsByCell.Count;

    internal void PrepareForLevel(SimpleLevel level)
    {
        var solids = level.Solids;
        var geometryChanged = !ReferenceEquals(_level, level)
            || _solidSnapshot is null
            || _solidSnapshot.Length != solids.Count;

        if (!geometryChanged)
        {
            for (var index = 0; index < solids.Count; index += 1)
            {
                if (_solidSnapshot![index] != solids[index])
                {
                    geometryChanged = true;
                    break;
                }
            }
        }

        if (!geometryChanged)
        {
            return;
        }

        _level = level;
        _solidSnapshot = new LevelSolid[solids.Count];
        for (var index = 0; index < solids.Count; index += 1)
        {
            _solidSnapshot[index] = solids[index];
        }

        _hostSolidsByCell.Clear();
        GeometryQueryCount = 0;
    }

    internal bool TryFindHostSolid(
        SimpleLevel level,
        int gridX,
        int gridY,
        float cellSize,
        out LevelSolid hostSolid)
    {
        if (!ReferenceEquals(_level, level) || _solidSnapshot is null)
        {
            PrepareForLevel(level);
        }

        var key = (gridX, gridY);
        if (_hostSolidsByCell.TryGetValue(key, out var cachedSolid))
        {
            hostSolid = cachedSolid.GetValueOrDefault();
            return cachedSolid.HasValue;
        }

        GeometryQueryCount += 1;
        var worldX = (gridX * cellSize) + (cellSize * 0.5f);
        var worldY = (gridY * cellSize) + (cellSize * 0.5f);
        var solids = level.Solids;
        LevelSolid? resolvedSolid = null;
        if (TryFindSolidContainingPoint(solids, worldX, worldY, out var containingSolid)
            || TryFindSolidBelowCell(solids, worldX, worldY, cellSize, out containingSolid))
        {
            resolvedSolid = containingSolid;
        }

        _hostSolidsByCell.Add(key, resolvedSolid);
        hostSolid = resolvedSolid.GetValueOrDefault();
        return resolvedSolid.HasValue;
    }

    internal void ForgetCell((int X, int Y) key)
    {
        _hostSolidsByCell.Remove(key);
    }

    internal void Clear()
    {
        _level = null;
        _solidSnapshot = null;
        _hostSolidsByCell.Clear();
        GeometryQueryCount = 0;
    }

    private static bool TryFindSolidContainingPoint(
        IReadOnlyList<LevelSolid> solids,
        float worldX,
        float worldY,
        out LevelSolid solid)
    {
        for (var index = 0; index < solids.Count; index += 1)
        {
            var candidate = solids[index];
            if (worldX >= candidate.Left
                && worldX < candidate.Right
                && worldY >= candidate.Top
                && worldY < candidate.Bottom)
            {
                solid = candidate;
                return true;
            }
        }

        solid = default;
        return false;
    }

    private static bool TryFindSolidBelowCell(
        IReadOnlyList<LevelSolid> solids,
        float worldX,
        float worldY,
        float cellSize,
        out LevelSolid solid)
    {
        var probeY = worldY + cellSize;
        for (var index = 0; index < solids.Count; index += 1)
        {
            var candidate = solids[index];
            if (worldX < candidate.Left || worldX >= candidate.Right)
            {
                continue;
            }

            if (worldY <= candidate.Top + 0.01f
                && probeY >= candidate.Top
                && worldY >= candidate.Top - cellSize)
            {
                solid = candidate;
                return true;
            }
        }

        solid = default;
        return false;
    }
}
