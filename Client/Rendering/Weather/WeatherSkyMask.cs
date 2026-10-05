#nullable enable

using System;
using System.Collections.Generic;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>
/// Per-column height where falling weather first meets the level, built once per
/// level from its collision rectangles. A lookup is one array read, so hundreds of
/// drops can be occluded each frame without touching the collision index.
/// </summary>
/// <remarks>
/// Each column's ground is the top of the first solid below open sky. A solid that
/// starts at the very top of the map is treated as a ceiling border: the sky
/// begins under it, so maps framed by a solid top edge still get weather. Moving
/// platforms and one-way platforms are not solids and let weather through.
/// Author-placed indoor regions act as invisible roofs: weather stops at their top.
/// </remarks>
internal sealed class WeatherSkyMask
{
    public const float ColumnWidth = 2f;
    private const float TopBorderTolerance = 1f;

    private readonly float[] _groundY;
    private readonly float[] _skyStartY;
    private readonly float[] _indoorTopY;
    private readonly float[] _hazeBottomY;

    /// <summary>Solid runs thinner than this (one-row ledges, platforms) let haze through.</summary>
    private const float ThinLedgeThickness = 18f;

    private WeatherSkyMask(float[] groundY, float[] skyStartY, float[] indoorTopY, float[] hazeBottomY, float width, float height)
    {
        _groundY = groundY;
        _skyStartY = skyStartY;
        _indoorTopY = indoorTopY;
        _hazeBottomY = hazeBottomY;
        Width = width;
        Height = height;
    }

    public float Width { get; }

    public float Height { get; }

    public int ColumnCount => _groundY.Length;

    public static WeatherSkyMask Build(
        IReadOnlyList<LevelSolid> solids,
        WorldBounds bounds,
        IReadOnlyList<IndoorRegionMarker>? indoorRegions = null)
    {
        var width = MathF.Max(1f, bounds.Width);
        var height = MathF.Max(1f, bounds.Height);
        var columnCount = Math.Max(1, (int)MathF.Ceiling(width / ColumnWidth));
        var skyStart = new float[columnCount];
        var ground = new float[columnCount];
        Array.Fill(ground, height);

        // Pass 1: a ceiling attached to the top edge pushes the sky start down.
        // Walkmask imports split solids into thin row strips, so walk them from the
        // top and keep extending while each strip touches the run above it.
        var byTop = new int[solids.Count];
        for (var index = 0; index < byTop.Length; index += 1)
        {
            byTop[index] = index;
        }

        Array.Sort(byTop, (a, b) => solids[a].Top.CompareTo(solids[b].Top));
        for (var order = 0; order < byTop.Length; order += 1)
        {
            var solid = solids[byTop[order]];
            if (solid.Width <= 0f || solid.Height <= 0f)
            {
                continue;
            }

            GetColumnRange(solid.Left, solid.Right, columnCount, out var first, out var last);
            for (var column = first; column <= last; column += 1)
            {
                if (solid.Top <= skyStart[column] + TopBorderTolerance)
                {
                    skyStart[column] = MathF.Max(skyStart[column], solid.Bottom);
                }
            }
        }

        // Pass 2: the highest surface at or below the sky start is the ground.
        for (var index = 0; index < solids.Count; index += 1)
        {
            var solid = solids[index];
            if (solid.Width <= 0f || solid.Height <= 0f)
            {
                continue;
            }

            GetColumnRange(solid.Left, solid.Right, columnCount, out var first, out var last);
            for (var column = first; column <= last; column += 1)
            {
                var start = skyStart[column];
                if (solid.Bottom <= start)
                {
                    continue;
                }

                var surface = MathF.Max(solid.Top, start);
                if (surface < ground[column])
                {
                    ground[column] = surface;
                }
            }
        }

        // Haze (snowstorm sheets) is air, not falling snow: it passes under a thin ledge
        // and stops at the next real surface, so sheltered gaps don't read as dark boxes.
        var runEnd = (float[])ground.Clone();
        for (var order = 0; order < byTop.Length; order += 1)
        {
            var solid = solids[byTop[order]];
            if (solid.Width <= 0f || solid.Height <= 0f)
            {
                continue;
            }

            GetColumnRange(solid.Left, solid.Right, columnCount, out var first, out var last);
            for (var column = first; column <= last; column += 1)
            {
                if (solid.Top >= ground[column] - 0.5f
                    && solid.Top <= runEnd[column] + TopBorderTolerance
                    && solid.Bottom > runEnd[column])
                {
                    runEnd[column] = solid.Bottom;
                }
            }
        }

        var hazeBottom = (float[])ground.Clone();
        for (var column = 0; column < columnCount; column += 1)
        {
            if (ground[column] < height
                && runEnd[column] < height - TopBorderTolerance
                && runEnd[column] - ground[column] < ThinLedgeThickness)
            {
                hazeBottom[column] = height;
            }
        }

        for (var index = 0; index < solids.Count; index += 1)
        {
            var solid = solids[index];
            if (solid.Width <= 0f || solid.Height <= 0f)
            {
                continue;
            }

            GetColumnRange(solid.Left, solid.Right, columnCount, out var first, out var last);
            for (var column = first; column <= last; column += 1)
            {
                if (hazeBottom[column] > ground[column]
                    && solid.Top >= runEnd[column] - 0.5f
                    && solid.Top < hazeBottom[column])
                {
                    hazeBottom[column] = solid.Top;
                }
            }
        }

        // Pass 3: indoor regions roof over whatever lies beneath them.
        var indoorTop = new float[columnCount];
        Array.Fill(indoorTop, height);
        if (indoorRegions is not null)
        {
            for (var index = 0; index < indoorRegions.Count; index += 1)
            {
                var region = indoorRegions[index];
                if (region.Width <= 0f || region.Height <= 0f)
                {
                    continue;
                }

                GetColumnRange(region.Left, region.Right, columnCount, out var first, out var last);
                for (var column = first; column <= last; column += 1)
                {
                    if (region.Bottom <= skyStart[column])
                    {
                        continue;
                    }

                    indoorTop[column] = MathF.Min(indoorTop[column], MathF.Max(region.Top, skyStart[column]));
                    hazeBottom[column] = MathF.Min(hazeBottom[column], indoorTop[column]);

                    var surface = MathF.Max(region.Top, skyStart[column]);
                    if (surface < ground[column])
                    {
                        ground[column] = surface;
                    }
                }
            }
        }

        return new WeatherSkyMask(ground, skyStart, indoorTop, hazeBottom, width, height);
    }

    /// <summary>World Y where weather falling at <paramref name="x"/> stops.</summary>
    public float GroundAt(float x)
    {
        var column = (int)(x / ColumnWidth);
        if ((uint)column >= (uint)_groundY.Length)
        {
            return column < 0 ? _groundY[0] : _groundY[^1];
        }

        return _groundY[column];
    }

    /// <summary>World Y where open sky begins at <paramref name="x"/> (below any top-edge ceiling).</summary>
    public float SkyStartAt(float x)
    {
        var column = (int)(x / ColumnWidth);
        if ((uint)column >= (uint)_skyStartY.Length)
        {
            return column < 0 ? _skyStartY[0] : _skyStartY[^1];
        }

        return _skyStartY[column];
    }

    /// <summary>
    /// Top of the highest author-marked indoor region in the column at <paramref name="x"/>,
    /// or the map height when there is none. Airborne haze (the storm whiteout) stops here but,
    /// unlike falling weather, still fills the air under ordinary ledges.
    /// </summary>
    public float IndoorTopAt(float x)
    {
        var column = (int)(x / ColumnWidth);
        if ((uint)column >= (uint)_indoorTopY.Length)
        {
            return column < 0 ? _indoorTopY[0] : _indoorTopY[^1];
        }

        return _indoorTopY[column];
    }

    /// <summary>Where airborne haze stops: the first surface thicker than a ledge, or an indoor region.</summary>
    public float HazeBottomAt(float x)
    {
        var column = (int)(x / ColumnWidth);
        if ((uint)column >= (uint)_hazeBottomY.Length)
        {
            return column < 0 ? _hazeBottomY[0] : _hazeBottomY[^1];
        }

        return _hazeBottomY[column];
    }

    private static void GetColumnRange(float left, float right, int columnCount, out int first, out int last)
    {
        // A column belongs to a rectangle when the column's centre lies inside it.
        first = Math.Max(0, (int)MathF.Ceiling((left / ColumnWidth) - 0.5f));
        last = Math.Min(columnCount - 1, (int)MathF.Ceiling((right / ColumnWidth) - 0.5f) - 1);
    }
}
