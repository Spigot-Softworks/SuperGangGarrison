using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace OpenGarrison.Client;

public sealed record LoadedSpriteFrame(
    Texture2D Texture,
    Rectangle? SourceRectangle = null,
    bool OwnsTexture = true,
    Rectangle? OpaqueBounds = null) : IDisposable
{
    public int Width => SourceRectangle?.Width ?? Texture.Width;

    public int Height => SourceRectangle?.Height ?? Texture.Height;

    public void Dispose()
    {
        if (OwnsTexture)
        {
            Texture.Dispose();
        }
    }
}
