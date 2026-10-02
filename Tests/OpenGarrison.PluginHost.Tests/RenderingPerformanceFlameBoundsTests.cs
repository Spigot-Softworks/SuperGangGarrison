using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class RenderingPerformanceFlameBoundsTests
{
    [Fact]
    public void FlameCellBoundsCoverPartialVisibleCellsAndOutlineNeighbors()
    {
        Assert.True(FlameCellBounds.TryCreateForWorldRectangle(
            left: 4f,
            top: 8f,
            right: 14f,
            bottom: 18f,
            haloCells: 1,
            out var bounds));

        Assert.Equal(new FlameCellBounds(1, 3, 7, 9), bounds);
        Assert.True(bounds.Contains(1, 3));
        Assert.True(bounds.Contains(7, 9));
        Assert.False(bounds.Contains(0, 3));
        Assert.False(bounds.Contains(8, 9));
    }

    [Fact]
    public void ClippedFlameAccumulationMatchesFullAccumulationInVisibleCellsAndTheirOutlines()
    {
        var game = CreateRasterizerGame();
        var fullCells = new Dictionary<(int, int), float>();
        var clippedCells = new Dictionary<(int, int), float>();
        Assert.True(FlameCellBounds.TryCreateForWorldRectangle(
            left: -24.7f,
            top: -12.3f,
            right: 120.5f,
            bottom: 78.25f,
            haloCells: 0,
            out var visibleCells));
        Assert.True(FlameCellBounds.TryCreateForWorldRectangle(
            left: -24.7f,
            top: -12.3f,
            right: 120.5f,
            bottom: 78.25f,
            haloCells: 1,
            out var clippedBounds));

        var particles = new[]
        {
            new Particle(7, 28f, 24f, 1.0f, 1.0f, 4f, -3f, 1.5f, false),
            new Particle(11, 33f, 27f, 1.1f, 0.9f, -2f, 1f, 1.8f, false),
            new Particle(17, 51f, 42f, 0.95f, 1f, 1f, 2f, 1.2f, false),
            new Particle(23, 116f, 50f, 1.05f, 1f, -5f, -1f, 1.5f, false),
            new Particle(29, 81f, 69f, 0.9f, 0.95f, 0f, -4f, 2.1f, false),
            new Particle(31, 48f, 40f, 1.0f, 1f, -1f, -2f, 1.7f, false),
            new Particle(37, -23.8f, -5.25f, 1.6f, 1f, -9.5f, 6.25f, 2.25f, true),
            new Particle(41, 119.8f, 76.25f, 1.35f, 0.9f, 8f, -4.75f, 2.25f, true),
        };

        foreach (var particle in particles)
        {
            game.AccumulateProceduralFlameParticle(
                fullCells,
                particle.Seed,
                particle.X,
                particle.Y,
                particle.Scale,
                particle.AlphaScale,
                particle.MotionX,
                particle.MotionY,
                particle.Stretch,
                particle.IncludeHornAccent);
            game.AccumulateProceduralFlameParticleWithinBounds(
                clippedCells,
                particle.Seed,
                particle.X,
                particle.Y,
                particle.Scale,
                particle.AlphaScale,
                particle.MotionX,
                particle.MotionY,
                particle.Stretch,
                particle.IncludeHornAccent,
                clippedBounds);
        }

        foreach (var (cell, alpha) in fullCells)
        {
            if (!clippedBounds.Contains(cell.Item1, cell.Item2))
            {
                continue;
            }

            Assert.True(clippedCells.TryGetValue(cell, out var clippedAlpha), $"Missing clipped flame cell {cell}.");
            Assert.Equal(alpha, clippedAlpha);
        }

        foreach (var (cell, alpha) in clippedCells)
        {
            Assert.True(clippedBounds.Contains(cell.Item1, cell.Item2));
            Assert.True(fullCells.TryGetValue(cell, out var fullAlpha), $"Clipping added flame cell {cell}.");
            Assert.Equal(fullAlpha, alpha);
        }

        var visibleCellCount = 0;
        var overlappingCellCount = 0;
        for (var gridY = visibleCells.MinGridY; gridY <= visibleCells.MaxGridY; gridY += 1)
        {
            for (var gridX = visibleCells.MinGridX; gridX <= visibleCells.MaxGridX; gridX += 1)
            {
                var key = (gridX, gridY);
                var existsInFull = fullCells.TryGetValue(key, out var fullAlpha);
                var existsInClipped = clippedCells.TryGetValue(key, out var clippedAlpha);
                Assert.Equal(existsInFull, existsInClipped);
                if (!existsInFull)
                {
                    continue;
                }

                visibleCellCount += 1;
                Assert.Equal(fullAlpha, clippedAlpha);
                Assert.Equal(IsOutlineCell(fullCells, gridX, gridY), IsOutlineCell(clippedCells, gridX, gridY));
                if (fullAlpha >= 1.3f)
                {
                    overlappingCellCount += 1;
                }
            }
        }

        Assert.True(visibleCellCount > 0);
        Assert.True(overlappingCellCount > 0);
    }

    private static bool IsOutlineCell(Dictionary<(int, int), float> cells, int gridX, int gridY)
    {
        for (var offsetY = -1; offsetY <= 1; offsetY += 1)
        {
            for (var offsetX = -1; offsetX <= 1; offsetX += 1)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }

                if (!cells.ContainsKey((gridX + offsetX, gridY + offsetY)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static Game1 CreateRasterizerGame()
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        game._world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        return game;
    }

    private readonly record struct Particle(
        int Seed,
        float X,
        float Y,
        float Scale,
        float AlphaScale,
        float MotionX,
        float MotionY,
        float Stretch,
        bool IncludeHornAccent);
}
