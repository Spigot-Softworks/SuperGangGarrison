#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private int ViewportWidth => GetViewportDimensions(_ingameResolution).X;

    private int ViewportHeight => GetViewportDimensions(_ingameResolution).Y;

    private void ApplyGraphicsSettings()
    {
        _graphics.IsFullScreen = _clientSettings.Fullscreen;
        _graphics.SynchronizeWithVerticalRetrace = _clientSettings.VSync;
        ApplyIngameResolution(_clientSettings.IngameResolution);
        ApplyPreferredBackBufferSize(_graphics.IsFullScreen, _ingameResolution);
        _graphics.ApplyChanges();
        PersistClientSettings();
    }

    private void ApplyIngameResolution(IngameResolutionKind ingameResolution)
    {
        _ingameResolution = NormalizeIngameResolution(ingameResolution);
        if (_gameRenderTarget is not null
            && (_gameRenderTarget.Width != ViewportWidth || _gameRenderTarget.Height != ViewportHeight))
        {
            _gameRenderTarget.Dispose();
            _gameRenderTarget = null;
        }
    }

    private void EnsureGameRenderTarget()
    {
        if (_gameRenderTarget is not null
            && _gameRenderTarget.Width == ViewportWidth
            && _gameRenderTarget.Height == ViewportHeight)
        {
            return;
        }

        _gameRenderTarget?.Dispose();
        _gameRenderTarget = new RenderTarget2D(
            GraphicsDevice,
            ViewportWidth,
            ViewportHeight,
            mipMap: false,
            SurfaceFormat.Color,
            DepthFormat.None,
            preferredMultiSampleCount: 0,
            RenderTargetUsage.DiscardContents);
    }

    private void BeginLogicalFrame(Color clearColor)
    {
        EnsureGameRenderTarget();
        GraphicsDevice.SetRenderTarget(_gameRenderTarget);
        GraphicsDevice.Clear(clearColor);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
    }

    private void EndLogicalFrame()
    {
        _spriteBatch.End();
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
        _spriteBatch.Draw(_gameRenderTarget, GetPresentationDestinationRectangle(), Color.White);
        _spriteBatch.End();
    }

    private Rectangle GetPresentationDestinationRectangle()
    {
        var actualWidth = GraphicsDevice.Viewport.Width;
        var actualHeight = GraphicsDevice.Viewport.Height;
        if (actualWidth <= 0 || actualHeight <= 0)
        {
            var fallback = GetPreferredBackBufferDimensions(fullscreen: false, _ingameResolution);
            return new Rectangle(0, 0, fallback.X, fallback.Y);
        }

        return GetLetterboxedDestinationRectangle(actualWidth, actualHeight);
    }

    private Rectangle GetInputDestinationRectangle()
    {
        var clientBounds = Window.ClientBounds;
        var inputWidth = clientBounds.Width;
        var inputHeight = clientBounds.Height;
        if (inputWidth <= 0 || inputHeight <= 0)
        {
            inputWidth = GraphicsDevice.Viewport.Width;
            inputHeight = GraphicsDevice.Viewport.Height;
        }

        if (inputWidth <= 0 || inputHeight <= 0)
        {
            var fallback = GetPreferredBackBufferDimensions(fullscreen: false, _ingameResolution);
            return new Rectangle(0, 0, fallback.X, fallback.Y);
        }

        return GetLetterboxedDestinationRectangle(inputWidth, inputHeight);
    }

    private Rectangle GetLetterboxedDestinationRectangle(int surfaceWidth, int surfaceHeight)
    {
        var scale = MathF.Min(surfaceWidth / (float)ViewportWidth, surfaceHeight / (float)ViewportHeight);
        var destinationWidth = Math.Max(1, (int)MathF.Floor(ViewportWidth * scale));
        var destinationHeight = Math.Max(1, (int)MathF.Floor(ViewportHeight * scale));
        return new Rectangle(
            (surfaceWidth - destinationWidth) / 2,
            (surfaceHeight - destinationHeight) / 2,
            destinationWidth,
            destinationHeight);
    }

    private MouseState GetScaledMouseState(MouseState rawMouse)
    {
        var destination = GetInputDestinationRectangle();
        if (destination.Width <= 0 || destination.Height <= 0)
        {
            return rawMouse;
        }

        var logicalX = ((rawMouse.X - destination.X) * ViewportWidth) / (float)destination.Width;
        var logicalY = ((rawMouse.Y - destination.Y) * ViewportHeight) / (float)destination.Height;
        return new MouseState(
            Math.Clamp((int)MathF.Round(logicalX), 0, Math.Max(0, ViewportWidth - 1)),
            Math.Clamp((int)MathF.Round(logicalY), 0, Math.Max(0, ViewportHeight - 1)),
            rawMouse.ScrollWheelValue,
            rawMouse.LeftButton,
            rawMouse.MiddleButton,
            rawMouse.RightButton,
            rawMouse.XButton1,
            rawMouse.XButton2);
    }

    private MouseState GetConstrainedMouseState(MouseState rawMouse)
    {
        if (!_graphics.IsFullScreen || !IsActive)
        {
            return rawMouse;
        }

        var destination = GetInputDestinationRectangle();
        if (destination.Width <= 0 || destination.Height <= 0)
        {
            return rawMouse;
        }

        var clampedX = Math.Clamp(rawMouse.X, destination.Left, destination.Right - 1);
        var clampedY = Math.Clamp(rawMouse.Y, destination.Top, destination.Bottom - 1);
        if (clampedX != rawMouse.X || clampedY != rawMouse.Y)
        {
            Mouse.SetPosition(clampedX, clampedY);
        }

        return new MouseState(
            clampedX,
            clampedY,
            rawMouse.ScrollWheelValue,
            rawMouse.LeftButton,
            rawMouse.MiddleButton,
            rawMouse.RightButton,
            rawMouse.XButton1,
            rawMouse.XButton2);
    }

    private void ApplyPreferredBackBufferSize(bool fullscreen, IngameResolutionKind ingameResolution)
    {
        var preferredDimensions = GetPreferredBackBufferDimensions(fullscreen, ingameResolution);
        _graphics.PreferredBackBufferWidth = preferredDimensions.X;
        _graphics.PreferredBackBufferHeight = preferredDimensions.Y;
    }

    private static Point GetPreferredBackBufferDimensions(bool fullscreen, IngameResolutionKind ingameResolution)
    {
        if (fullscreen)
        {
            var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            return new Point(displayMode.Width, displayMode.Height);
        }

        return GetViewportDimensions(ingameResolution);
    }

    private static IngameResolutionKind NormalizeIngameResolution(IngameResolutionKind ingameResolution)
    {
        return ingameResolution switch
        {
            IngameResolutionKind.Aspect5x4 => IngameResolutionKind.Aspect5x4,
            IngameResolutionKind.Aspect4x3 => IngameResolutionKind.Aspect4x3,
            IngameResolutionKind.Aspect16x9 => IngameResolutionKind.Aspect16x9,
            IngameResolutionKind.Aspect16x10 => IngameResolutionKind.Aspect16x9,
            IngameResolutionKind.Aspect2x1 => IngameResolutionKind.Aspect16x9,
            _ => IngameResolutionKind.Aspect4x3,
        };
    }

    private static Point GetViewportDimensions(IngameResolutionKind ingameResolution)
    {
        return NormalizeIngameResolution(ingameResolution) switch
        {
            IngameResolutionKind.Aspect5x4 => new Point(780, 624),
            IngameResolutionKind.Aspect16x9 => new Point(864, 486),
            _ => new Point(800, 600),
        };
    }

    private static string GetIngameResolutionLabel(IngameResolutionKind ingameResolution)
    {
        return NormalizeIngameResolution(ingameResolution) switch
        {
            IngameResolutionKind.Aspect5x4 => "5:4",
            IngameResolutionKind.Aspect16x9 => "16:9",
            _ => "4:3",
        };
    }

    private static IngameResolutionKind GetNextIngameResolution(IngameResolutionKind ingameResolution)
    {
        return NormalizeIngameResolution(ingameResolution) switch
        {
            IngameResolutionKind.Aspect5x4 => IngameResolutionKind.Aspect4x3,
            IngameResolutionKind.Aspect4x3 => IngameResolutionKind.Aspect16x9,
            _ => IngameResolutionKind.Aspect5x4,
        };
    }
}
