using System;

namespace OpenGarrison.Client;

/// <summary>
/// Inclusive bounds for the world-aligned cells used by procedural flames.
/// </summary>
internal readonly record struct FlameCellBounds(int MinGridX, int MinGridY, int MaxGridX, int MaxGridY)
{
    public bool IsEmpty => MinGridX > MaxGridX || MinGridY > MaxGridY;

    public bool Contains(int gridX, int gridY)
        => gridX >= MinGridX && gridX <= MaxGridX && gridY >= MinGridY && gridY <= MaxGridY;

    /// <summary>
    /// Builds cell bounds for a half-open world rectangle. The optional halo
    /// keeps neighboring cells available to procedural flame outline tests.
    /// </summary>
    public static bool TryCreateForWorldRectangle(
        float left,
        float top,
        float right,
        float bottom,
        int haloCells,
        out FlameCellBounds bounds,
        float cellSize = 2f)
    {
        bounds = default;
        if (!float.IsFinite(left)
            || !float.IsFinite(top)
            || !float.IsFinite(right)
            || !float.IsFinite(bottom)
            || !float.IsFinite(cellSize)
            || cellSize <= 0f
            || haloCells < 0
            || right <= left
            || bottom <= top)
        {
            return false;
        }

        var minGridX = MathF.Floor(left / cellSize) - haloCells;
        var minGridY = MathF.Floor(top / cellSize) - haloCells;
        var maxGridX = MathF.Ceiling(right / cellSize) - 1f + haloCells;
        var maxGridY = MathF.Ceiling(bottom / cellSize) - 1f + haloCells;
        if (minGridX < int.MinValue || minGridY < int.MinValue
            || maxGridX > int.MaxValue || maxGridY > int.MaxValue)
        {
            return false;
        }

        bounds = new FlameCellBounds(
            (int)minGridX,
            (int)minGridY,
            (int)maxGridX,
            (int)maxGridY);
        return !bounds.IsEmpty;
    }
}
