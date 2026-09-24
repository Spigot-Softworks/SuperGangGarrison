#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace OpenGarrison.Client;

public partial class Game1
{
    private static bool TryGetSpriteFramePixels(
        LoadedSpriteFrame frame,
        out Color[] pixels,
        out int width,
        out int height)
    {
        width = frame.Width;
        height = frame.Height;
        pixels = new Color[width * height];
        if (frame.TryCopyPixelData(pixels))
        {
            return true;
        }

        try
        {
            if (frame.SourceRectangle is { } sourceRectangle)
            {
                frame.Texture.GetData(0, sourceRectangle, pixels, 0, pixels.Length);
            }
            else
            {
                frame.Texture.GetData(pixels);
            }

            return true;
        }
        catch
        {
            pixels = Array.Empty<Color>();
            width = 0;
            height = 0;
            return false;
        }
    }
}
