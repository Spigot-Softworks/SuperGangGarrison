using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace OpenGarrison.Core;

public static class GameMakerCollisionMaskImporter
{
    public static IReadOnlyList<LevelSolid> Import(string collisionMaskPath, WorldBounds bounds)
    {
        if (!File.Exists(collisionMaskPath))
        {
            return [];
        }

        using var image = Image.Load<Rgba32>(collisionMaskPath);
        if (image.Width <= 0 || image.Height <= 0)
        {
            return [];
        }

        var pixelWidth = bounds.Width / image.Width;
        var pixelHeight = bounds.Height / image.Height;
        var activeRects = new Dictionary<(int X, int Width), RectanglePixels>();
        var mergedRects = new List<RectanglePixels>();

        for (var y = 0; y < image.Height; y++)
        {
            var rowRuns = ReadOpaqueRuns(image, y);
            var nextActiveRects = new Dictionary<(int X, int Width), RectanglePixels>();

            foreach (var run in rowRuns)
            {
                var key = (run.StartX, run.Width);
                if (activeRects.Remove(key, out var existing))
                {
                    existing.Height += 1;
                    nextActiveRects[key] = existing;
                }
                else
                {
                    nextActiveRects[key] = new RectanglePixels(run.StartX, y, run.Width, 1);
                }
            }

            mergedRects.AddRange(activeRects.Values);
            activeRects = nextActiveRects;
        }

        mergedRects.AddRange(activeRects.Values);

        var solids = new List<LevelSolid>(mergedRects.Count);
        foreach (var rect in mergedRects)
        {
            solids.Add(new LevelSolid(
                rect.X * pixelWidth,
                rect.Y * pixelHeight,
                rect.Width * pixelWidth,
                rect.Height * pixelHeight));
        }

        return solids;
    }

    private static List<RowRun> ReadOpaqueRuns(Image<Rgba32> image, int y)
    {
        var runs = new List<RowRun>();
        var currentRunStart = -1;

        for (var x = 0; x < image.Width; x++)
        {
            var isOpaque = image[x, y].A > 0;
            if (isOpaque)
            {
                if (currentRunStart < 0)
                {
                    currentRunStart = x;
                }

                continue;
            }

            if (currentRunStart >= 0)
            {
                runs.Add(new RowRun(currentRunStart, x - currentRunStart));
                currentRunStart = -1;
            }
        }

        if (currentRunStart >= 0)
        {
            runs.Add(new RowRun(currentRunStart, image.Width - currentRunStart));
        }

        return runs;
    }

    private sealed class RectanglePixels
    {
        public RectanglePixels(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int X { get; }

        public int Y { get; }

        public int Width { get; }

        public int Height { get; set; }
    }

    private readonly record struct RowRun(int StartX, int Width);
}
