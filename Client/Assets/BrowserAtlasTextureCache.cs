using Microsoft.Xna.Framework.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;

namespace OpenGarrison.Client;

internal sealed class BrowserAtlasTextureCache(GraphicsDevice graphicsDevice) : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice = graphicsDevice;
    private readonly Dictionary<string, BrowserLoadedAtlasPage> _pages = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task<BrowserLoadedAtlasPage?>> _pendingPages = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public Task<BrowserLoadedAtlasPage?> GetPageAsync(string relativePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_pages.TryGetValue(relativePath, out var cachedPage))
        {
            return Task.FromResult<BrowserLoadedAtlasPage?>(cachedPage);
        }

        if (_pendingPages.TryGetValue(relativePath, out var pendingPage))
        {
            return pendingPage;
        }

        pendingPage = LoadPageAsync(relativePath);
        _pendingPages[relativePath] = pendingPage;
        return pendingPage;
    }

    public Texture2D CreateFrameTexture(BrowserLoadedAtlasPage page, XnaRectangle sourceRect)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var clampedRect = XnaRectangle.Intersect(new XnaRectangle(0, 0, page.Width, page.Height), sourceRect);
        if (clampedRect.Width <= 0 || clampedRect.Height <= 0)
        {
            throw new InvalidOperationException("Atlas frame source rectangle was empty after clamping.");
        }

        var framePixels = new XnaColor[clampedRect.Width * clampedRect.Height];
        for (var row = 0; row < clampedRect.Height; row += 1)
        {
            var sourceIndex = ((clampedRect.Y + row) * page.Width) + clampedRect.X;
            Array.Copy(page.PixelData, sourceIndex, framePixels, row * clampedRect.Width, clampedRect.Width);
        }

        var texture = new Texture2D(_graphicsDevice, clampedRect.Width, clampedRect.Height);
        texture.SetData(framePixels);
        return texture;
    }

    public LoadedSpriteFrame CreateFrame(BrowserLoadedAtlasPage page, XnaRectangle sourceRect)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var clampedRect = XnaRectangle.Intersect(new XnaRectangle(0, 0, page.Width, page.Height), sourceRect);
        if (clampedRect.Width <= 0 || clampedRect.Height <= 0)
        {
            throw new InvalidOperationException("Atlas frame source rectangle was empty after clamping.");
        }

        return new LoadedSpriteFrame(
            page.Texture,
            clampedRect,
            OwnsTexture: false,
            OpaqueBounds: GetOpaqueBounds(page, clampedRect));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var page in _pages.Values)
        {
            page.Texture.Dispose();
        }

        _pages.Clear();
        _pendingPages.Clear();
    }

    private async Task<BrowserLoadedAtlasPage?> LoadPageAsync(string relativePath)
    {
        var bytes = await BrowserAssetFetchUtility.TryGetBytesAsync(relativePath).ConfigureAwait(false);
        if (bytes is null || bytes.Length == 0)
        {
            _pendingPages.Remove(relativePath);
            return null;
        }

        using var image = Image.Load<Rgba32>(bytes);
        var pixelData = new Rgba32[image.Width * image.Height];
        image.CopyPixelDataTo(pixelData);

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

        var texture = new Texture2D(_graphicsDevice, image.Width, image.Height);
        texture.SetData(textureData);
        var page = new BrowserLoadedAtlasPage(relativePath, texture, textureData, image.Width, image.Height);
        _pages[relativePath] = page;
        _pendingPages.Remove(relativePath);
        return page;
    }

    private static XnaRectangle GetOpaqueBounds(BrowserLoadedAtlasPage page, XnaRectangle sourceRect)
    {
        var minX = sourceRect.Width;
        var minY = sourceRect.Height;
        var maxX = -1;
        var maxY = -1;
        for (var y = 0; y < sourceRect.Height; y += 1)
        {
            var sourceIndex = ((sourceRect.Y + y) * page.Width) + sourceRect.X;
            for (var x = 0; x < sourceRect.Width; x += 1)
            {
                if (page.PixelData[sourceIndex + x].A == 0)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        return maxX >= minX && maxY >= minY
            ? new XnaRectangle(minX, minY, (maxX - minX) + 1, (maxY - minY) + 1)
            : new XnaRectangle(0, 0, sourceRect.Width, sourceRect.Height);
    }
}

internal sealed record BrowserLoadedAtlasPage(
    string RelativePath,
    Texture2D Texture,
    XnaColor[] PixelData,
    int Width,
    int Height);
