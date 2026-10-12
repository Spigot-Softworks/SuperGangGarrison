#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace OpenGarrison.Client;

/// <summary>Blurs a copy of a mask without resizing its authored pixels or reading them back from the GPU.</summary>
internal sealed class MaskGlowBuffer : IDisposable
{
    private RenderTarget2D? _source;
    private RenderTarget2D? _horizontal;
    private RenderTarget2D? _blurred;
    private Rectangle _region;

    internal readonly record struct Bounds(Vector2 Origin, int Width, int Height, float WorldPerTexel);

    internal static Bounds? GetBounds(Vector2 center, Vector2 maskSize, float rotation, float radius,
        Vector2 viewOrigin, Vector2 viewSize, float worldPerTexel)
    {
        var cos = MathF.Abs(MathF.Cos(rotation));
        var sin = MathF.Abs(MathF.Sin(rotation));
        var half = new Vector2(maskSize.X * cos + maskSize.Y * sin, maskSize.X * sin + maskSize.Y * cos) * 0.5f;
        // Include offscreen source pixels which can still spill light into the view.
        var padding = new Vector2(radius + worldPerTexel * 2f);
        var left = Vector2.Max(center - half - padding, viewOrigin - padding);
        var right = Vector2.Min(center + half + padding, viewOrigin + viewSize + padding);
        if (right.X <= left.X || right.Y <= left.Y) return null;
        var origin = new Vector2(MathF.Floor(left.X / worldPerTexel), MathF.Floor(left.Y / worldPerTexel)) * worldPerTexel;
        return new Bounds(origin, Math.Max(1, (int)MathF.Ceiling((right.X - origin.X) / worldPerTexel)),
            Math.Max(1, (int)MathF.Ceiling((right.Y - origin.Y) / worldPerTexel)), worldPerTexel);
    }

    public void BeginSource(GraphicsDevice device, Bounds bounds)
    {
        _region = new Rectangle(0, 0, bounds.Width, bounds.Height);
        EnsureTarget(device, ref _source);
        EnsureTarget(device, ref _horizontal);
        EnsureTarget(device, ref _blurred);
        device.SetRenderTarget(_source);
        device.Clear(Color.Transparent);
    }

    private void EnsureTarget(GraphicsDevice device, ref RenderTarget2D? target)
    {
        if (target is { IsDisposed: false } && target.Width >= _region.Width && target.Height >= _region.Height
            && target.Width <= _region.Width + 256 && target.Height <= _region.Height + 256) return;
        target?.Dispose();
        target = new RenderTarget2D(device, _region.Width + 16, _region.Height + 16, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
    }

    // Each level filters neighbouring samples before widening the filter. Unlike a few
    // widely spaced copies, this covers every pixel, even around a single painted dot.
    internal static IEnumerable<float> GetBlurSteps(float radius)
    {
        if (!float.IsFinite(radius) || radius <= 0f) yield break;
        var remaining = radius;
        for (var step = 1f; remaining > 0f; step *= 2f)
        {
            var distance = MathF.Min(step, remaining);
            yield return distance;
            remaining -= distance;
        }
    }

    public Texture2D Blur(GraphicsDevice device, SpriteBatch batch, float radiusInTexels)
    {
        Texture2D input = _source!;
        foreach (var step in GetBlurSteps(radiusInTexels))
        {
            Filter(device, batch, input, _horizontal!, new Vector2(step, 0f));
            Filter(device, batch, _horizontal!, _blurred!, new Vector2(0f, step));
            input = _blurred!;
        }
        return input;
    }

    private void Filter(GraphicsDevice device, SpriteBatch batch, Texture2D input, RenderTarget2D output, Vector2 step)
    {
        device.SetRenderTarget(output);
        device.Clear(Color.Transparent);
        batch.Begin(SpriteSortMode.Deferred, LightmapRenderer.AddOne, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone);
        batch.Draw(input, -step, _region, Color.White * 0.25f);
        batch.Draw(input, Vector2.Zero, _region, Color.White * 0.5f);
        batch.Draw(input, step, _region, Color.White * 0.25f);
        batch.End();
    }

    public void Composite(GraphicsDevice device, SpriteBatch batch, RenderTarget2D target, Texture2D blurred,
        Bounds bounds, Vector2 targetOrigin, float targetWorldPerTexel, Color color)
    {
        device.SetRenderTarget(target);
        batch.Begin(SpriteSortMode.Deferred, LightmapRenderer.AddOne, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone);
        batch.Draw(blurred, (bounds.Origin - targetOrigin) / targetWorldPerTexel, _region, color, 0f,
            Vector2.Zero, bounds.WorldPerTexel / targetWorldPerTexel, SpriteEffects.None, 0f);
        batch.End();
    }

    public void Dispose()
    {
        _source?.Dispose();
        _horizontal?.Dispose();
        _blurred?.Dispose();
    }
}
