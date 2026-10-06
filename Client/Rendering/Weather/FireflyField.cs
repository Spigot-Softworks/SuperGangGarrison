#nullable enable

using System;
using System.Collections.Generic;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>One visible firefly this frame: world position and 0..1 brightness.</summary>
internal readonly record struct FireflyInstance(float X, float Y, float Brightness);

/// <summary>
/// Fireflies: slow, blinking motes hovering in the open air near the ground, or, in the
/// <see cref="MapFireflyStyle.Subtle"/> style, steady glowing motes drifting gently on the
/// breeze through all the open air.
/// </summary>
/// <remarks>
/// Like the rest of the weather this is stateless: every firefly's position and blink are
/// pure functions of time and a per-firefly hash, laid out in tiles across the map, so
/// nothing is simulated or stored and any camera can evaluate just what it sees. The same
/// positions feed both the drawn motes and (with map lighting on) the little lights they
/// cast, so the two always agree.
/// </remarks>
internal static class FireflyField
{
    /// <summary>Fireflies repeat in tiles this wide (and this tall on top-down maps).</summary>
    public const float TileSize = 480f;

    /// <summary>Fireflies per tile at density 100.</summary>
    public const int MaxPerTile = 36;

    /// <summary>Highest a firefly hovers above the ground.</summary>
    private const float MaxHoverHeight = 150f;

    /// <summary>Furthest a firefly wanders from its anchor (so tiles just off-screen are included).</summary>
    private const float WanderReach = 48f;

    /// <summary>Subtle motes ease out over this distance from a wall, floor or ceiling instead of popping.</summary>
    private const float SubtleEdgeFade = 14f;

    /// <summary>
    /// Fills <paramref name="output"/> with the fireflies inside (or just around) the view.
    /// </summary>
    /// <param name="skyMask">Open-air surface; null on top-down maps (fireflies fill the plane).</param>
    /// <param name="density">Author density 0-100.</param>
    /// <param name="densityScale">0..1 quality scale (particle settings).</param>
    public static void Collect(
        List<FireflyInstance> output,
        WeatherSkyMask? skyMask,
        float viewLeft,
        float viewTop,
        float viewWidth,
        float viewHeight,
        float worldWidth,
        float worldHeight,
        double time,
        MapWeatherWind wind,
        int density,
        float densityScale,
        IReadOnlyList<IndoorRegionMarker>? hiddenRegions,
        MapFireflyStyle style = MapFireflyStyle.Fireflies)
    {
        var subtle = style == MapFireflyStyle.Subtle;
        output.Clear();
        var count = (int)MathF.Round(MaxPerTile * (Math.Clamp(density, 0, 100) / 100f) * Math.Clamp(densityScale, 0f, 1f));
        if (count <= 0)
        {
            return;
        }

        var left = MathF.Max(0f, viewLeft - WanderReach);
        var right = MathF.Min(worldWidth, viewLeft + viewWidth + WanderReach);
        var top = MathF.Max(0f, viewTop - WanderReach);
        var bottom = MathF.Min(worldHeight, viewTop + viewHeight + WanderReach);
        if (right <= left || bottom <= top)
        {
            return;
        }

        // Wind slowly carries the whole swarm. Tiles are laid out in the drifting frame,
        // so fireflies never wrap or pop at tile edges.
        // Subtle motes always ride a light breeze (calm still drifts a little).
        var drift = (float)((wind switch
        {
            MapWeatherWind.Left => subtle ? -11d : -7d,
            MapWeatherWind.Right => subtle ? 11d : 7d,
            _ => subtle ? 4d : 0d,
        } * time) % (TileSize * 4096d));
        var firstColumn = (int)MathF.Floor((left - drift) / TileSize) - 1;
        var lastColumn = (int)MathF.Floor((right - drift) / TileSize) + 1;
        var firstRow = 0;
        var lastRow = 0;
        if (skyMask is null || subtle)
        {
            // Top-down maps and subtle motes fill the plane in 2D tiles.
            firstRow = (int)MathF.Floor(top / TileSize) - 1;
            lastRow = (int)MathF.Floor(bottom / TileSize) + 1;
        }

        for (var column = firstColumn; column <= lastColumn; column += 1)
        {
            for (var row = firstRow; row <= lastRow; row += 1)
            {
                for (var index = 0; index < count; index += 1)
                {
                    var seed = Hash(unchecked((uint)index * 0x9E3779B1u) ^ Hash(unchecked((uint)column * 0x85EBCA77u) ^ ((uint)row * 0xC2B2AE3Du)));
                    FireflyInstance firefly;
                    var placed = subtle
                        ? TryPlaceSubtle(seed, column, row, drift, skyMask, time, out firefly)
                        : TryPlace(seed, column, row, drift, skyMask, time, out firefly);
                    if (placed
                        && firefly.X >= left && firefly.X <= right
                        && firefly.Y >= top && firefly.Y <= bottom
                        && !IsHidden(hiddenRegions, firefly.X, firefly.Y))
                    {
                        output.Add(firefly);
                    }
                }
            }
        }
    }

    private static bool TryPlace(uint seed, int column, int row, float drift, WeatherSkyMask? skyMask, double time, out FireflyInstance firefly)
    {
        firefly = default;
        var anchorX = (column * TileSize) + (Unit(seed) * TileSize) + drift;

        // Lazy two-frequency wander, different for every firefly.
        var wanderX = (26f * Wave(time, 0.15f + (0.25f * Unit(Hash(seed ^ 0x11u))), Unit(Hash(seed ^ 0x12u))))
            + (11f * Wave(time, 0.5f + (0.6f * Unit(Hash(seed ^ 0x13u))), Unit(Hash(seed ^ 0x14u))));
        var bob = (10f * Wave(time, 0.2f + (0.3f * Unit(Hash(seed ^ 0x15u))), Unit(Hash(seed ^ 0x16u))))
            + (4f * Wave(time, 0.7f + (0.8f * Unit(Hash(seed ^ 0x17u))), Unit(Hash(seed ^ 0x18u))));
        var x = anchorX + wanderX;
        float y;
        if (skyMask is not null)
        {
            // Hover above the ground at the anchor; most stay low, a few drift up high.
            var ground = skyMask.GroundAt(anchorX);
            if (ground >= skyMask.Height - 0.5f)
            {
                return false; // bottomless column: nothing to hover over
            }

            var height = 6f + (MathF.Pow(Unit(Hash(seed ^ 0x19u)), 1.7f) * MaxHoverHeight);
            y = ground - height + bob;
            if (y > skyMask.GroundAt(x) - 2f || y < skyMask.SkyStartAt(x) + 2f)
            {
                return false; // wandered into a wall, ledge or ceiling
            }
        }
        else
        {
            y = (row * TileSize) + (Unit(Hash(seed ^ 0x1Au)) * TileSize) + (bob * 2f);
        }

        // Blink: mostly off, glowing on and off at its own slow rhythm, with a faint shimmer.
        var blink = 0.5f + (0.5f * Wave(time, 0.12f + (0.18f * Unit(Hash(seed ^ 0x1Bu))), Unit(Hash(seed ^ 0x1Cu))));
        var brightness = SmoothStep(0.3f, 0.85f, blink)
            * (0.88f + (0.12f * Wave(time, 1.1f, Unit(Hash(seed ^ 0x1Du)))))
            * (0.55f + (0.45f * Unit(Hash(seed ^ 0x1Eu))));
        if (brightness < 0.03f)
        {
            return false;
        }

        firefly = new FireflyInstance(x, y, brightness);
        return true;
    }

    /// <summary>
    /// A subtle mote: carried by the breeze with a slow, small sway and a gentle rise and
    /// fall, glowing steadily at its own brightness with only a faint slow shimmer. It
    /// never blinks; near walls, floors and ceilings it eases out rather than vanishing.
    /// </summary>
    private static bool TryPlaceSubtle(uint seed, int column, int row, float drift, WeatherSkyMask? skyMask, double time, out FireflyInstance firefly)
    {
        firefly = default;
        var x = (column * TileSize) + (Unit(seed) * TileSize) + drift
            + (9f * Wave(time, 0.04f + (0.05f * Unit(Hash(seed ^ 0x21u))), Unit(Hash(seed ^ 0x22u))));
        var y = (row * TileSize) + (Unit(Hash(seed ^ 0x23u)) * TileSize)
            + (7f * Wave(time, 0.05f + (0.06f * Unit(Hash(seed ^ 0x24u))), Unit(Hash(seed ^ 0x25u))));

        var edge = 1f;
        if (skyMask is not null)
        {
            var clearanceBelow = skyMask.GroundAt(x) - y;
            var clearanceAbove = y - skyMask.SkyStartAt(x);
            if (clearanceBelow <= 0f || clearanceAbove <= 0f)
            {
                return false;
            }

            edge = SmoothStep(0f, SubtleEdgeFade, clearanceBelow) * SmoothStep(0f, SubtleEdgeFade, clearanceAbove);
        }

        // Steady: a fixed per-mote brightness with a faint, slow shimmer (never off).
        var brightness = (0.45f + (0.45f * Unit(Hash(seed ^ 0x26u))))
            * (0.9f + (0.1f * Wave(time, 0.08f + (0.07f * Unit(Hash(seed ^ 0x27u))), Unit(Hash(seed ^ 0x28u)))))
            * edge;
        if (brightness < 0.03f)
        {
            return false;
        }

        firefly = new FireflyInstance(x, y, brightness);
        return true;
    }

    /// <summary>sin(2pi (time * frequency + phase)), with time kept in double until the end.</summary>
    private static float Wave(double time, float frequency, float phase) =>
        (float)Math.Sin(Math.Tau * ((time * frequency) + phase));

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - (2f * t));
    }

    private static bool IsHidden(IReadOnlyList<IndoorRegionMarker>? regions, float x, float y)
    {
        if (regions is null)
        {
            return false;
        }

        for (var index = 0; index < regions.Count; index += 1)
        {
            var region = regions[index];
            if (x >= region.Left && x < region.Right && y >= region.Top && y < region.Bottom)
            {
                return true;
            }
        }

        return false;
    }

    private static float Unit(uint hash) => (hash >> 8) * (1f / 16777216f);

    private static uint Hash(uint value)
    {
        unchecked
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }
    }
}
