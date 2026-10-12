#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace OpenGarrison.Client;

/// <summary>
/// Captures the layers above emissive map artwork so they can be lit separately.
/// Multiply lighting preserves their alpha; compositing then hides the artwork under
/// opaque sprites without dimming the artwork or stamping its shape onto those sprites.
/// </summary>
internal sealed class EmissiveWorldLayer : IDisposable
{
    private RenderTarget2D? _foreground;
    private RenderTargetBinding[]? _destination;
    private Viewport _viewport;
    private Rectangle _scissor;

    public bool IsActive => _destination is not null;

    // Call with no active SpriteBatch, and with a destination that preserves contents.
    public void BeginForeground(GraphicsDevice device)
    {
        if (IsActive) throw new InvalidOperationException("An emissive world layer is already open.");
        var targets = device.GetRenderTargets();
        if (targets.Length != 1 || targets[0].RenderTarget is not RenderTarget2D destination
            || destination.RenderTargetUsage != RenderTargetUsage.PreserveContents)
            throw new InvalidOperationException("Emissive world layers require a preserved world target.");

        if (_foreground is null || _foreground.IsDisposed
            || _foreground.Width != destination.Width || _foreground.Height != destination.Height)
        {
            _foreground?.Dispose();
            _foreground = new RenderTarget2D(device, destination.Width, destination.Height, false,
                SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        }

        _destination = targets;
        _viewport = device.Viewport;
        _scissor = device.ScissorRectangle;
        device.SetRenderTarget(_foreground);
        device.Clear(Color.Transparent);
        device.Viewport = _viewport;
        device.ScissorRectangle = _scissor;
    }

    // Call after multiplying the foreground's colours, before air glow/beam overlays.
    public void CompositeForeground(GraphicsDevice device, SpriteBatch batch, RasterizerState rasterizer)
    {
        if (_destination is null) return;
        device.SetRenderTargets(_destination);
        device.Viewport = _viewport;
        device.ScissorRectangle = _scissor;
        _destination = null;
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, rasterizer);
        // SpriteBatch coordinates are viewport-relative; the texture is target-relative.
        batch.Draw(_foreground!, new Vector2(-_viewport.X, -_viewport.Y), Color.White);
        batch.End();
    }

    public void Dispose() => _foreground?.Dispose();
}
