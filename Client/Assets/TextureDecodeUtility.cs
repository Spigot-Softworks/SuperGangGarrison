using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;

namespace OpenGarrison.Client;

internal static class TextureDecodeUtility
{
    public static Texture2D LoadTexture(GraphicsDevice graphicsDevice, byte[] bytes, bool applyLegacyChromaKey)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(bytes);

        using var image = Image.Load<Rgba32>(bytes);
        var pixelData = new Rgba32[image.Width * image.Height];
        image.CopyPixelDataTo(pixelData);
        if (applyLegacyChromaKey)
        {
            ApplyLegacyChromaKeyTransparency(pixelData, image.Width, image.Height);
        }

        var textureData = new XnaColor[pixelData.Length];
        for (var index = 0; index < pixelData.Length; index += 1)
        {
            var pixel = pixelData[index];
            if (pixel.A == 0)
            {
                textureData[index] = XnaColor.Transparent;
                continue;
            }

            var premultipliedRed = (pixel.R * pixel.A + 127) / 255;
            var premultipliedGreen = (pixel.G * pixel.A + 127) / 255;
            var premultipliedBlue = (pixel.B * pixel.A + 127) / 255;
            textureData[index] = new XnaColor(
                (byte)premultipliedRed,
                (byte)premultipliedGreen,
                (byte)premultipliedBlue,
                pixel.A);
        }

        var texture = new Texture2D(graphicsDevice, image.Width, image.Height);
        texture.SetData(textureData);
        return texture;
    }

    public static LoadedSpriteFrame[] LoadFrameTextures(
        GraphicsDevice graphicsDevice,
        byte[] bytes,
        int? frameWidth,
        int? frameHeight)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(bytes);

        using var image = Image.Load<Rgba32>(bytes);
        var width = image.Width;
        var height = image.Height;
        var resolvedFrameWidth = frameWidth ?? width;
        var resolvedFrameHeight = frameHeight ?? height;
        if (resolvedFrameWidth <= 0
            || resolvedFrameHeight <= 0
            || width % resolvedFrameWidth != 0
            || height % resolvedFrameHeight != 0)
        {
            throw new InvalidOperationException("Texture frame dimensions do not evenly divide the source image.");
        }

        var sourcePixels = new Rgba32[width * height];
        image.CopyPixelDataTo(sourcePixels);

        var columns = Math.Max(1, width / resolvedFrameWidth);
        var rows = Math.Max(1, height / resolvedFrameHeight);
        var frames = new LoadedSpriteFrame[columns * rows];
        var frameIndex = 0;
        for (var row = 0; row < rows; row += 1)
        {
            for (var column = 0; column < columns; column += 1)
            {
                var framePixels = new XnaColor[resolvedFrameWidth * resolvedFrameHeight];
                var minX = resolvedFrameWidth;
                var minY = resolvedFrameHeight;
                var maxX = -1;
                var maxY = -1;
                for (var y = 0; y < resolvedFrameHeight; y += 1)
                {
                    var sourceRowIndex = ((row * resolvedFrameHeight) + y) * width + (column * resolvedFrameWidth);
                    for (var x = 0; x < resolvedFrameWidth; x += 1)
                    {
                        var sourcePixel = sourcePixels[sourceRowIndex + x];
                        var premultipliedPixel = ToPremultipliedColor(sourcePixel);
                        framePixels[(y * resolvedFrameWidth) + x] = premultipliedPixel;
                        if (premultipliedPixel.A == 0)
                        {
                            continue;
                        }

                        minX = Math.Min(minX, x);
                        minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x);
                        maxY = Math.Max(maxY, y);
                    }
                }

                var frameTexture = new Texture2D(graphicsDevice, resolvedFrameWidth, resolvedFrameHeight);
                frameTexture.SetData(framePixels);
                var opaqueBounds = maxX >= minX && maxY >= minY
                    ? new XnaRectangle(minX, minY, (maxX - minX) + 1, (maxY - minY) + 1)
                    : new XnaRectangle(0, 0, resolvedFrameWidth, resolvedFrameHeight);
                frames[frameIndex] = new LoadedSpriteFrame(frameTexture, OpaqueBounds: opaqueBounds);
                frameIndex += 1;
            }
        }

        return frames;
    }

    private static void ApplyLegacyChromaKeyTransparency(Rgba32[] pixelData, int width, int height)
    {
        if (pixelData.Length == 0 || width <= 0 || height <= 0)
        {
            return;
        }

        var visited = new bool[pixelData.Length];
        var pending = new Queue<int>();
        for (var x = 0; x < width; x += 1)
        {
            TryQueueChromaKeyIndex(x, 0, width, height, pixelData, visited, pending);
            TryQueueChromaKeyIndex(x, height - 1, width, height, pixelData, visited, pending);
        }

        for (var y = 0; y < height; y += 1)
        {
            TryQueueChromaKeyIndex(0, y, width, height, pixelData, visited, pending);
            TryQueueChromaKeyIndex(width - 1, y, width, height, pixelData, visited, pending);
        }

        while (pending.Count > 0)
        {
            var index = pending.Dequeue();
            pixelData[index] = new Rgba32(0, 0, 0, 0);
            var x = index % width;
            var y = index / width;
            TryQueueChromaKeyIndex(x + 1, y, width, height, pixelData, visited, pending);
            TryQueueChromaKeyIndex(x - 1, y, width, height, pixelData, visited, pending);
            TryQueueChromaKeyIndex(x, y + 1, width, height, pixelData, visited, pending);
            TryQueueChromaKeyIndex(x, y - 1, width, height, pixelData, visited, pending);
        }
    }

    private static void TryQueueChromaKeyIndex(
        int x,
        int y,
        int width,
        int height,
        Rgba32[] pixelData,
        bool[] visited,
        Queue<int> pending)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return;
        }

        var index = (y * width) + x;
        if (visited[index])
        {
            return;
        }

        visited[index] = true;
        if (!IsLegacyChromaKeyGreen(pixelData[index]))
        {
            return;
        }

        pending.Enqueue(index);
    }

    private static bool IsLegacyChromaKeyGreen(Rgba32 pixel)
    {
        return pixel.A >= 128
            && pixel.G >= 90
            && pixel.R <= 48
            && pixel.G - pixel.R >= 56
            && pixel.G - pixel.B >= 32;
    }

    private static XnaColor ToPremultipliedColor(Rgba32 pixel)
    {
        if (pixel.A == 0)
        {
            return XnaColor.Transparent;
        }

        var premultipliedRed = (pixel.R * pixel.A + 127) / 255;
        var premultipliedGreen = (pixel.G * pixel.A + 127) / 255;
        var premultipliedBlue = (pixel.B * pixel.A + 127) / 255;
        return new XnaColor(
            (byte)premultipliedRed,
            (byte)premultipliedGreen,
            (byte)premultipliedBlue,
            pixel.A);
    }
}
