#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int DamageVignetteIntensityBuckets = 32;
    private const int DamageVignetteMaskMaxDimension = 512;

    private bool TryEnsureDamageVignetteTexture(float intensity, out Texture2D texture)
    {
        GetDamageVignetteMaskDimensions(out var width, out var height);
        if (_damageVignetteTextureWidth != width || _damageVignetteTextureHeight != height)
        {
            DisposeDamageVignetteTextures();
            _damageVignetteTextureWidth = width;
            _damageVignetteTextureHeight = height;
        }

        var normalizedIntensity = Math.Clamp(intensity, 0f, 1f);
        var intensityBucket = Math.Clamp(
            (int)MathF.Round(normalizedIntensity * DamageVignetteIntensityBuckets),
            0,
            DamageVignetteIntensityBuckets);
        if (_damageVignetteTexturesByBucket.TryGetValue(intensityBucket, out var cachedTexture)
            && !cachedTexture.IsDisposed)
        {
            texture = cachedTexture;
            return true;
        }

        var bucketIntensity = intensityBucket / (float)DamageVignetteIntensityBuckets;
        texture = CreateDamageVignetteMaskTexture(width, height, bucketIntensity);
        _damageVignetteTexturesByBucket[intensityBucket] = texture;
        return true;
    }

    private void DisposeDamageVignetteTextures()
    {
        foreach (var texture in _damageVignetteTexturesByBucket.Values)
        {
            texture.Dispose();
        }

        _damageVignetteTexturesByBucket.Clear();
        _damageVignetteTextureWidth = 0;
        _damageVignetteTextureHeight = 0;
    }

    private void GetDamageVignetteMaskDimensions(out int width, out int height)
    {
        var viewportWidth = Math.Max(1, ViewportWidth);
        var viewportHeight = Math.Max(1, ViewportHeight);
        var longestSide = Math.Max(viewportWidth, viewportHeight);
        var scale = Math.Min(1f, DamageVignetteMaskMaxDimension / (float)longestSide);
        width = Math.Max(1, (int)MathF.Ceiling(viewportWidth * scale));
        height = Math.Max(1, (int)MathF.Ceiling(viewportHeight * scale));
    }

    private Texture2D CreateDamageVignetteMaskTexture(int width, int height, float intensity)
    {
        var texture = new Texture2D(GraphicsDevice, width, height, false, SurfaceFormat.Color);
        var pixels = new Color[width * height];
        var centerX = (width - 1) * 0.5f;
        var centerY = (height - 1) * 0.5f;
        var invCenterX = centerX <= 0f ? 0f : 1f / centerX;
        var invCenterY = centerY <= 0f ? 0f : 1f / centerY;
        var creep = SmoothStep(0f, 1f, intensity);
        var innerRadius = MathHelper.Lerp(0.94f, 0.48f, creep);
        var outerRadius = MathHelper.Lerp(1.38f, 1.04f, creep);
        var edgeStart = MathHelper.Lerp(0.82f, 0.64f, creep);
        var maxAlpha = MathHelper.Lerp(0.16f, 0.74f, creep);
        var edgeBoost = MathHelper.Lerp(0.24f, 0.46f, creep);
        var curve = MathHelper.Lerp(1.35f, 0.85f, creep);

        for (var y = 0; y < height; y += 1)
        {
            var normalizedY = MathF.Abs(y - centerY) * invCenterY;
            for (var x = 0; x < width; x += 1)
            {
                var normalizedX = MathF.Abs(x - centerX) * invCenterX;
                var radial = MathF.Sqrt((normalizedX * normalizedX) + (normalizedY * normalizedY));
                var edgeProximity = MathF.Max(normalizedX, normalizedY);
                var radialMask = SmoothStep(innerRadius, outerRadius, radial);
                var edgeMask = SmoothStep(edgeStart, 1f, edgeProximity) * edgeBoost;
                var vignette = MathF.Pow(Math.Clamp(MathF.Max(radialMask, edgeMask), 0f, 1f), curve);
                var alpha = Math.Clamp(vignette * maxAlpha, 0f, 0.8f);
                var alphaByte = (byte)MathF.Round(alpha * 255f);
                var red = (byte)MathF.Round(MathHelper.Lerp(72f, 142f, creep) * alpha);
                var green = (byte)MathF.Round(MathHelper.Lerp(0f, 7f, creep) * alpha);
                pixels[(y * width) + x] = new Color(red, green, (byte)0, alphaByte);
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
