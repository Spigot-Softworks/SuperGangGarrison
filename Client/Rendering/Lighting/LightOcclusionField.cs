#nullable enable

using System;
using System.Collections.Generic;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>
/// The map's walkmask as a coarse grid of solid cells, for casting light rays. Built once
/// per level from the level's solids (which are the walkmask), then queried by
/// <see cref="ComputeVisibility"/> to find how far each ray from a light travels before
/// a wall stops it.
/// </summary>
internal sealed class LightOcclusionField
{
    /// <summary>Most cells the grid may hold; big maps get coarser cells instead.</summary>
    private const int MaxCells = 4_000_000;

    /// <summary>How far light reaches into a wall it hits, so the wall's face lights up.</summary>
    public const float WallLightDepth = 6f;

    /// <summary>A lamp sunk into a wall shines out once it clears this much solid.</summary>
    private const float EmbeddedLampAllowance = 12f;

    private readonly bool[] _solid;
    private readonly int _columns;
    private readonly int _rows;
    private readonly float _cellSize;

    private LightOcclusionField(bool[] solid, int columns, int rows, float cellSize)
    {
        _solid = solid;
        _columns = columns;
        _rows = rows;
        _cellSize = cellSize;
    }

    public static LightOcclusionField Build(IReadOnlyList<LevelSolid> solids, WorldBounds bounds)
    {
        var width = MathF.Max(1f, bounds.Width);
        var height = MathF.Max(1f, bounds.Height);
        var cellSize = 2f;
        while ((width / cellSize) * (height / cellSize) > MaxCells)
        {
            cellSize *= 2f;
        }

        var columns = Math.Max(1, (int)MathF.Ceiling(width / cellSize));
        var rows = Math.Max(1, (int)MathF.Ceiling(height / cellSize));
        var solid = new bool[columns * rows];
        foreach (var rect in solids)
        {
            // A cell is solid when its centre lies inside the rectangle.
            var firstColumn = Math.Max(0, (int)MathF.Ceiling((rect.Left / cellSize) - 0.5f));
            var lastColumn = Math.Min(columns - 1, (int)MathF.Floor((rect.Right / cellSize) - 0.5f));
            var firstRow = Math.Max(0, (int)MathF.Ceiling((rect.Top / cellSize) - 0.5f));
            var lastRow = Math.Min(rows - 1, (int)MathF.Floor((rect.Bottom / cellSize) - 0.5f));
            for (var row = firstRow; row <= lastRow; row += 1)
            {
                var rowStart = row * columns;
                for (var column = firstColumn; column <= lastColumn; column += 1)
                {
                    solid[rowStart + column] = true;
                }
            }
        }

        return new LightOcclusionField(solid, columns, rows, cellSize);
    }

    /// <summary>Ray count for a light: about one ray per 8 px of circumference.</summary>
    public static int GetRayCount(float radius) => Math.Clamp((int)MathF.Round(radius * MathF.Tau / 8f), 64, 512);

    /// <summary>
    /// Reach of evenly spaced rays (ray i points at angle i * 2pi / count, screen space,
    /// y down), each stopped by the first wall plus <see cref="WallLightDepth"/>.
    /// </summary>
    public float[] ComputeVisibility(float x, float y, float radius)
    {
        var count = GetRayCount(radius);
        var distances = new float[count];
        for (var index = 0; index < count; index += 1)
        {
            var angle = index * MathF.Tau / count;
            var hit = CastRay(x, y, MathF.Cos(angle), MathF.Sin(angle), radius);
            distances[index] = MathF.Min(radius, hit + WallLightDepth);
        }

        return distances;
    }

    /// <summary>Grid traversal (Amanatides-Woo): distance to the first solid cell, or maxDistance.</summary>
    public float CastRay(float originX, float originY, float directionX, float directionY, float maxDistance)
    {
        var column = (int)MathF.Floor(originX / _cellSize);
        var row = (int)MathF.Floor(originY / _cellSize);
        var stepColumn = directionX > 0f ? 1 : -1;
        var stepRow = directionY > 0f ? 1 : -1;
        var tDeltaX = MathF.Abs(directionX) > 1e-6f ? _cellSize / MathF.Abs(directionX) : float.PositiveInfinity;
        var tDeltaY = MathF.Abs(directionY) > 1e-6f ? _cellSize / MathF.Abs(directionY) : float.PositiveInfinity;
        var tMaxX = MathF.Abs(directionX) > 1e-6f
            ? (((column + (directionX > 0f ? 1 : 0)) * _cellSize) - originX) / directionX
            : float.PositiveInfinity;
        var tMaxY = MathF.Abs(directionY) > 1e-6f
            ? (((row + (directionY > 0f ? 1 : 0)) * _cellSize) - originY) / directionY
            : float.PositiveInfinity;

        var t = 0f;
        var reachedOpenAir = !IsSolidCell(column, row);
        while (t <= maxDistance)
        {
            if (column < 0 || row < 0 || column >= _columns || row >= _rows)
            {
                // Outside the map nothing blocks light.
                return maxDistance;
            }

            if (_solid[(row * _columns) + column])
            {
                if (reachedOpenAir)
                {
                    return t;
                }

                if (t > EmbeddedLampAllowance)
                {
                    // Buried too deep in solid ground to shine this way.
                    return 0f;
                }
            }
            else
            {
                reachedOpenAir = true;
            }

            if (tMaxX < tMaxY)
            {
                t = tMaxX;
                tMaxX += tDeltaX;
                column += stepColumn;
            }
            else
            {
                t = tMaxY;
                tMaxY += tDeltaY;
                row += stepRow;
            }
        }

        return maxDistance;
    }

    /// <summary>
    /// True when a ray set built by <see cref="ComputeVisibility"/> reaches the point
    /// (used to decide whether a light can rim-light a character).
    /// </summary>
    public static bool Reaches(float[] distances, float lightX, float lightY, float pointX, float pointY)
    {
        var deltaX = pointX - lightX;
        var deltaY = pointY - lightY;
        var angle = MathF.Atan2(deltaY, deltaX);
        if (angle < 0f)
        {
            angle += MathF.Tau;
        }

        var index = (int)MathF.Round(angle / MathF.Tau * distances.Length) % distances.Length;
        return MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY)) <= distances[index] + 4f;
    }

    private bool IsSolidCell(int column, int row) =>
        column >= 0 && row >= 0 && column < _columns && row < _rows && _solid[(row * _columns) + column];
}
