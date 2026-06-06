#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool _logicalFrameRendersDirectlyToBackBuffer;

    private int ViewportWidth => GetViewportDimensions(_ingameResolution).X;

    private int ViewportHeight => GetViewportDimensions(_ingameResolution).Y;

    private bool ShouldUseNavEditorWindowGutter()
    {
        return _navEditorEnabled && !_graphics.IsFullScreen;
    }

    private void ApplyGraphicsSettings()
    {
        ApplyIngameResolution(_clientSettings.IngameResolution);
        ApplyWindowSize(_clientSettings.WindowSize);

        if (OperatingSystem.IsBrowser())
        {
            // Browser rendering stays on the host canvas size; only the logical viewport changes.
            _graphics.IsFullScreen = false;
            _graphics.SynchronizeWithVerticalRetrace = false;
            _clientSettings.Fullscreen = false;
            PersistClientSettings();
            return;
        }

        _graphics.IsFullScreen = _clientSettings.Fullscreen;
        _graphics.SynchronizeWithVerticalRetrace = _clientSettings.VSync;
        ApplyPreferredBackBufferSize(_graphics.IsFullScreen, _ingameResolution, _windowSize);
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

    private void ApplyWindowSize(WindowSizeKind windowSize)
    {
        _windowSize = OpenGarrisonPreferencesDocument.NormalizeWindowSize(windowSize);
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
        _logicalFrameRendersDirectlyToBackBuffer = ShouldRenderDirectlyToBackBuffer();
        if (_logicalFrameRendersDirectlyToBackBuffer)
        {
            WriteGameplayRenderTrace("frame beginlogical clear-backbuffer-direct");
            GraphicsDevice.Clear(clearColor);
            WriteGameplayRenderTrace("frame beginlogical spritebatchbegin-direct");
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
            WriteGameplayRenderTrace("frame beginlogical done-direct");
            return;
        }

        EnsureGameRenderTarget();
        WriteGameplayRenderTrace("frame beginlogical setrendertarget");
        GraphicsDevice.SetRenderTarget(_gameRenderTarget);
        WriteGameplayRenderTrace("frame beginlogical clear");
        GraphicsDevice.Clear(clearColor);
        WriteGameplayRenderTrace("frame beginlogical spritebatchbegin");
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
        WriteGameplayRenderTrace("frame beginlogical done");
    }

    private void EndLogicalFrame()
    {
        if (_logicalFrameRendersDirectlyToBackBuffer)
        {
            WriteGameplayRenderTrace("frame endlogical spritebatchend-direct");
            _spriteBatch.End();
            WriteGameplayRenderTrace("frame endlogical done-direct");
            _logicalFrameRendersDirectlyToBackBuffer = false;
            return;
        }

        WriteGameplayRenderTrace("frame endlogical spritebatchend-1");
        _spriteBatch.End();
        WriteGameplayRenderTrace("frame endlogical setrendertarget-null");
        GraphicsDevice.SetRenderTarget(null);
        WriteGameplayRenderTrace("frame endlogical clear-backbuffer");
        GraphicsDevice.Clear(Color.Black);
        WriteGameplayRenderTrace("frame endlogical spritebatchbegin-2");
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
        WriteGameplayRenderTrace("frame endlogical draw-rendertarget");
        _spriteBatch.Draw(_gameRenderTarget, GetPresentationDestinationRectangle(), Color.White);
        WriteGameplayRenderTrace("frame endlogical spritebatchend-2");
        _spriteBatch.End();
        _logicalFrameRendersDirectlyToBackBuffer = false;
        WriteGameplayRenderTrace("frame endlogical done");
    }

    private Rectangle GetPresentationDestinationRectangle()
    {
        var actualWidth = GraphicsDevice.Viewport.Width;
        var actualHeight = GraphicsDevice.Viewport.Height;
        if (actualWidth <= 0 || actualHeight <= 0)
        {
            var fallback = GetPreferredBackBufferDimensions(fullscreen: false, _ingameResolution, _windowSize);
            return new Rectangle(0, 0, fallback.X, fallback.Y);
        }

        return GetGameplayDestinationRectangle(actualWidth, actualHeight);
    }

    private bool ShouldRenderDirectlyToBackBuffer()
    {
        if (ShouldUseNavEditorWindowGutter())
        {
            return false;
        }

        var viewport = GraphicsDevice.Viewport;
        return viewport.Width == ViewportWidth
            && viewport.Height == ViewportHeight;
    }

    private Rectangle GetInputDestinationRectangle()
    {
        if (OperatingSystem.IsBrowser())
        {
            var viewportWidth = GraphicsDevice.Viewport.Width;
            var viewportHeight = GraphicsDevice.Viewport.Height;
            if (viewportWidth > 0 && viewportHeight > 0)
            {
                return GetGameplayDestinationRectangle(viewportWidth, viewportHeight);
            }
        }

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
            var fallback = GetPreferredBackBufferDimensions(fullscreen: false, _ingameResolution, _windowSize);
            return new Rectangle(0, 0, fallback.X, fallback.Y);
        }

        return GetGameplayDestinationRectangle(inputWidth, inputHeight);
    }

    private Rectangle GetGameplayDestinationRectangle(int surfaceWidth, int surfaceHeight)
    {
        var availableWidth = ShouldUseNavEditorWindowGutter()
            ? Math.Max(1, surfaceWidth - GetNavEditorWindowGutterWidth())
            : surfaceWidth;
        var scale = MathF.Min(availableWidth / (float)ViewportWidth, surfaceHeight / (float)ViewportHeight);
        var destinationWidth = Math.Max(1, (int)MathF.Floor(ViewportWidth * scale));
        var destinationHeight = Math.Max(1, (int)MathF.Floor(ViewportHeight * scale));
        return new Rectangle(
            (availableWidth - destinationWidth) / 2,
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

    private void ApplyPreferredBackBufferSize(bool fullscreen, IngameResolutionKind ingameResolution, WindowSizeKind windowSize)
    {
        var preferredDimensions = GetWindowDimensions(fullscreen, ingameResolution, windowSize);
        _graphics.PreferredBackBufferWidth = preferredDimensions.X;
        _graphics.PreferredBackBufferHeight = preferredDimensions.Y;
    }

    private Point GetWindowDimensions(bool fullscreen, IngameResolutionKind ingameResolution, WindowSizeKind windowSize)
    {
        var gameplayDimensions = GetPreferredBackBufferDimensions(fullscreen, ingameResolution, windowSize);
        if (fullscreen || !_navEditorEnabled)
        {
            return gameplayDimensions;
        }

        return new Point(
            gameplayDimensions.X + GetNavEditorWindowGutterWidth(),
            Math.Max(gameplayDimensions.Y, GetNavEditorExpandedWindowHeight()));
    }

    private void RefreshNavEditorWindowGutter()
    {
        var preferredDimensions = GetWindowDimensions(_graphics.IsFullScreen, _ingameResolution, _windowSize);
        if (_graphics.PreferredBackBufferWidth == preferredDimensions.X
            && _graphics.PreferredBackBufferHeight == preferredDimensions.Y)
        {
            return;
        }

        _graphics.PreferredBackBufferWidth = preferredDimensions.X;
        _graphics.PreferredBackBufferHeight = preferredDimensions.Y;
        _graphics.ApplyChanges();
    }

    private static int GetNavEditorWindowGutterWidth()
    {
        return NavEditorPanelWidth + (NavEditorPanelMargin * 2);
    }

    private static int GetNavEditorExpandedWindowHeight()
    {
        return NavEditorPanelExpandedHeight + (NavEditorPanelMargin * 2);
    }

    private static Point GetPreferredBackBufferDimensions(bool fullscreen, IngameResolutionKind ingameResolution, WindowSizeKind windowSize)
    {
        if (fullscreen && !OperatingSystem.IsBrowser())
        {
            var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            return new Point(displayMode.Width, displayMode.Height);
        }

        var viewportDimensions = GetViewportDimensions(ingameResolution);
        var scale = GetWindowSizeScale(windowSize);
        return new Point(
            Math.Max(1, (int)MathF.Round(viewportDimensions.X * scale)),
            Math.Max(1, (int)MathF.Round(viewportDimensions.Y * scale)));
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
            _ => OpenGarrisonPreferencesDocument.DefaultIngameResolution,
        };
    }

    private static float GetWindowSizeScale(WindowSizeKind windowSize)
    {
        return OpenGarrisonPreferencesDocument.NormalizeWindowSize(windowSize) switch
        {
            WindowSizeKind.Scale150 => 1.5f,
            WindowSizeKind.Scale200 => 2f,
            _ => 1f,
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

    private static string GetWindowSizeLabel(WindowSizeKind windowSize)
    {
        return OpenGarrisonPreferencesDocument.NormalizeWindowSize(windowSize) switch
        {
            WindowSizeKind.Scale150 => "150%",
            WindowSizeKind.Scale200 => "200%",
            _ => "100%",
        };
    }

    private static WindowSizeKind GetNextWindowSize(WindowSizeKind windowSize)
    {
        return OpenGarrisonPreferencesDocument.NormalizeWindowSize(windowSize) switch
        {
            WindowSizeKind.Scale100 => WindowSizeKind.Scale150,
            WindowSizeKind.Scale150 => WindowSizeKind.Scale200,
            _ => WindowSizeKind.Scale100,
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
