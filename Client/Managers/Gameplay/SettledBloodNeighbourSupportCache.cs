using System.Collections.Generic;

namespace OpenGarrison.Client;

internal readonly record struct SettledBloodNeighbourSupport(float Amount, int Count);

/// <summary>
/// Memoizes the unchanged eight-neighbour support for missing cells during one
/// smoothing pass. The support sum retains the original y-major/x-major order.
/// </summary>
internal sealed class SettledBloodNeighbourSupportCache
{
    private readonly Dictionary<(int X, int Y), SettledBloodNeighbourSupport> _supportByCell = new();

    internal int LookupCount { get; private set; }

    internal int ComputedSupportCount { get; private set; }

    internal void Clear()
    {
        _supportByCell.Clear();
        LookupCount = 0;
        ComputedSupportCount = 0;
    }

    internal SettledBloodNeighbourSupport Get(
        Dictionary<(int X, int Y), float> cells,
        (int X, int Y) key)
    {
        LookupCount += 1;
        if (_supportByCell.TryGetValue(key, out var cachedSupport))
        {
            return cachedSupport;
        }

        var support = 0f;
        var count = 0;
        for (var ny = -1; ny <= 1; ny += 1)
        {
            for (var nx = -1; nx <= 1; nx += 1)
            {
                if (nx == 0 && ny == 0)
                {
                    continue;
                }

                if (!cells.TryGetValue((key.X + nx, key.Y + ny), out var neighbourAmount)
                    || neighbourAmount < 0.2f)
                {
                    continue;
                }

                support += neighbourAmount;
                count += 1;
            }
        }

        cachedSupport = new SettledBloodNeighbourSupport(support, count);
        _supportByCell.Add(key, cachedSupport);
        ComputedSupportCount += 1;
        return cachedSupport;
    }
}

internal static class SettledBloodMorphologicalClose
{
    internal static void BuildBridges(
        Dictionary<(int X, int Y), float> cells,
        Dictionary<(int X, int Y), float> bridgeScratch,
        SettledBloodNeighbourSupportCache supportCache,
        float maxAmount)
    {
        bridgeScratch.Clear();
        supportCache.Clear();

        // Aggressive morphological close: unify nearby pools into one sheet.
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

                    var neighbourSupport = supportCache.Get(cells, key);
                    var neighbourCount = neighbourSupport.Count;
                    if (neighbourCount < 2)
                    {
                        continue;
                    }

                    var fillAmount = MathF.Min(maxAmount, neighbourSupport.Amount / Math.Max(2, neighbourCount));
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
    }
}
