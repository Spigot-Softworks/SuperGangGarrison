using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace OpenGarrison.Tools.BrowserAssetBuilder.Atlas;

internal static class AtlasImageWriter
{
    public static void WritePng(string outputPath, AtlasPageBuilder page)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var image = new Image<Rgba32>(page.OutputWidth, page.OutputHeight);
        foreach (var frame in page.Frames)
        {
            DrawFrame(image, frame, page.Group.Padding);
        }

        image.SaveAsPng(outputPath);
    }

    private static void DrawFrame(Image<Rgba32> atlasImage, AtlasPlacedFrame placedFrame, int padding)
    {
        var width = placedFrame.Width;
        var height = placedFrame.Height;
        var sourcePixels = placedFrame.Source.PixelBytes;
        for (var row = 0; row < height; row += 1)
        {
            var sourceRowOffset = row * width * 4;
            for (var column = 0; column < width; column += 1)
            {
                var sourceIndex = sourceRowOffset + (column * 4);
                atlasImage[placedFrame.X + column, placedFrame.Y + row] = new Rgba32(
                    sourcePixels[sourceIndex],
                    sourcePixels[sourceIndex + 1],
                    sourcePixels[sourceIndex + 2],
                    sourcePixels[sourceIndex + 3]);
            }
        }

        for (var offset = 1; offset <= padding; offset += 1)
        {
            for (var column = 0; column < width; column += 1)
            {
                atlasImage[placedFrame.X + column, placedFrame.Y - offset] = atlasImage[placedFrame.X + column, placedFrame.Y];
                atlasImage[placedFrame.X + column, placedFrame.Y + height - 1 + offset] = atlasImage[placedFrame.X + column, placedFrame.Y + height - 1];
            }

            for (var row = 0; row < height; row += 1)
            {
                atlasImage[placedFrame.X - offset, placedFrame.Y + row] = atlasImage[placedFrame.X, placedFrame.Y + row];
                atlasImage[placedFrame.X + width - 1 + offset, placedFrame.Y + row] = atlasImage[placedFrame.X + width - 1, placedFrame.Y + row];
            }

            atlasImage[placedFrame.X - offset, placedFrame.Y - offset] = atlasImage[placedFrame.X, placedFrame.Y];
            atlasImage[placedFrame.X + width - 1 + offset, placedFrame.Y - offset] = atlasImage[placedFrame.X + width - 1, placedFrame.Y];
            atlasImage[placedFrame.X - offset, placedFrame.Y + height - 1 + offset] = atlasImage[placedFrame.X, placedFrame.Y + height - 1];
            atlasImage[placedFrame.X + width - 1 + offset, placedFrame.Y + height - 1 + offset] = atlasImage[placedFrame.X + width - 1, placedFrame.Y + height - 1];
        }
    }
}
