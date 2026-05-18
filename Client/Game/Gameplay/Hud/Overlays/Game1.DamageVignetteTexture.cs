#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool TryEnsureDamageVignetteTexture(out Texture2D texture)
    {
        var width = Math.Max(1, ViewportWidth);
        var height = Math.Max(1, ViewportHeight);
        if (_damageVignetteTexture is not null
            && _damageVignetteTextureWidth == width
            && _damageVignetteTextureHeight == height
            && !_damageVignetteTexture.IsDisposed)
        {
            texture = _damageVignetteTexture;
            return true;
        }

        _damageVignetteTexture?.Dispose();
        _damageVignetteTexture = CreateDamageVignetteTexture(width, height);
        _damageVignetteTextureWidth = width;
        _damageVignetteTextureHeight = height;
        texture = _damageVignetteTexture;
        return true;
    }

    private Texture2D CreateDamageVignetteTexture(int width, int height)
    {
        var texture = new Texture2D(GraphicsDevice, width, height, false, SurfaceFormat.Color);
        var pixels = new Color[width * height];
        var centerX = (width - 1) * 0.5f;
        var centerY = (height - 1) * 0.5f;
        var invCenterX = centerX <= 0f ? 0f : 1f / centerX;
        var invCenterY = centerY <= 0f ? 0f : 1f / centerY;

        for (var y = 0; y < height; y += 1)
        {
            var normalizedY = MathF.Abs(y - centerY) * invCenterY;
            for (var x = 0; x < width; x += 1)
            {
                var normalizedX = MathF.Abs(x - centerX) * invCenterX;
                var radial = MathF.Sqrt((normalizedX * normalizedX) + (normalizedY * normalizedY));
                var edgeProximity = MathF.Max(normalizedX, normalizedY);
                var vignette = SmoothStep(0.92f, 1.34f, radial);
                vignette = MathF.Max(vignette, SmoothStep(0.94f, 1f, edgeProximity) * 0.22f);
                vignette = MathF.Pow(Math.Clamp(vignette, 0f, 1f), 1.45f);
                pixels[(y * width) + x] = new Color((byte)255, (byte)255, (byte)255, (byte)MathF.Round(vignette * 255f));
            }
        }

        texture.SetData(pixels);
        return texture;
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        if (edge0 >= edge1)
        {
            return value >= edge1 ? 1f : 0f;
        }

        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - (2f * t));
    }
}
