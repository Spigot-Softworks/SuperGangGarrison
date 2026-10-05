using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

internal readonly record struct SettledBloodRenderCell(int X, int Y, Color Color);

/// <summary>
/// Holds the ordered, camera-independent output for settled blood rendering.
/// Age and seep progress are omitted from the snapshot because neither affects
/// the image until it changes Amount or the cell's visible membership/tint.
/// </summary>
internal sealed class SettledBloodPoolDrawCache
{
    private readonly List<SourceCellSnapshot> _sourceCells = new();
    private readonly List<SettledBloodRenderCell> _normalCells = new();
    private readonly List<SettledBloodRenderCell> _cryoCells = new();
    private bool _hasSnapshot;

    internal List<SettledBloodRenderCell> NormalCells => _normalCells;

    internal List<SettledBloodRenderCell> CryoCells => _cryoCells;

    internal int BuildCount { get; private set; }

    internal int CacheHitCount { get; private set; }

    internal long ComparedSourceCellCount { get; private set; }

    internal void Clear()
    {
        _sourceCells.Clear();
        _normalCells.Clear();
        _cryoCells.Clear();
        _hasSnapshot = false;
        BuildCount = 0;
        CacheHitCount = 0;
        ComparedSourceCellCount = 0;
    }

    internal bool TryUse(Dictionary<(int X, int Y), Game1.SettledBloodCell> source)
    {
        if (!_hasSnapshot)
        {
            return false;
        }

        var snapshotIndex = 0;
        foreach (var entry in source)
        {
            if (entry.Value.Amount < 0.08f)
            {
                continue;
            }

            ComparedSourceCellCount += 1;
            if (snapshotIndex >= _sourceCells.Count)
            {
                return false;
            }

            var expected = _sourceCells[snapshotIndex];
            if (expected.X != entry.Key.X
                || expected.Y != entry.Key.Y
                || expected.AmountBits != BitConverter.SingleToInt32Bits(entry.Value.Amount)
                || expected.Cryo != entry.Value.ExperimentalCryoTinted)
            {
                return false;
            }

            snapshotIndex += 1;
        }

        if (snapshotIndex != _sourceCells.Count)
        {
            return false;
        }

        CacheHitCount += 1;
        return true;
    }

    internal void BeginBuild()
    {
        _sourceCells.Clear();
        _normalCells.Clear();
        _cryoCells.Clear();
        _hasSnapshot = false;
    }

    internal void CaptureSourceCell(
        (int X, int Y) key,
        float amount,
        bool cryo)
    {
        _sourceCells.Add(new SourceCellSnapshot(
            key.X,
            key.Y,
            BitConverter.SingleToInt32Bits(amount),
            cryo));
    }

    internal void CompleteBuild()
    {
        _hasSnapshot = true;
        BuildCount += 1;
    }

    private readonly record struct SourceCellSnapshot(int X, int Y, int AmountBits, bool Cryo);
}
