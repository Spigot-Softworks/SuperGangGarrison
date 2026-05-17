using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace OpenGarrison.Client;

public sealed record LoadedSpriteFramePixelSource(Color[] Pixels, int Width, int Height);

public sealed record LoadedSpriteFrame(
    Texture2D Texture,
    Rectangle? SourceRectangle = null,
    bool OwnsTexture = true,
    Rectangle? OpaqueBounds = null,
    LoadedSpriteFramePixelSource? PixelSource = null) : IDisposable
{
    public int Width => SourceRectangle?.Width ?? Texture.Width;

    public int Height => SourceRectangle?.Height ?? Texture.Height;

    public bool TryCopyPixelData(Color[] destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (PixelSource is null)
        {
            return false;
        }

        var sourceRectangle = SourceRectangle ?? new Rectangle(0, 0, PixelSource.Width, PixelSource.Height);
        if (sourceRectangle.Width < 0
            || sourceRectangle.Height < 0
            || sourceRectangle.X < 0
            || sourceRectangle.Y < 0
            || sourceRectangle.Right > PixelSource.Width
            || sourceRectangle.Bottom > PixelSource.Height
            || PixelSource.Pixels.Length < PixelSource.Width * PixelSource.Height)
        {
            return false;
        }

        var expectedLength = sourceRectangle.Width * sourceRectangle.Height;
        if (destination.Length < expectedLength)
        {
            throw new ArgumentException("Destination buffer is smaller than the sprite frame.", nameof(destination));
        }

        for (var row = 0; row < sourceRectangle.Height; row += 1)
        {
            var sourceIndex = ((sourceRectangle.Y + row) * PixelSource.Width) + sourceRectangle.X;
            Array.Copy(PixelSource.Pixels, sourceIndex, destination, row * sourceRectangle.Width, sourceRectangle.Width);
        }

        return true;
    }

    public void Dispose()
    {
        if (OwnsTexture)
        {
            Texture.Dispose();
        }
    }
}
