#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using System;
using System.Runtime.InteropServices;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool _logicalFrameRendersDirectlyToBackBuffer;

    private int ViewportWidth => GetViewportDimensions(_ingameResolution).X;

    private int ViewportHeight => GetViewportDimensions(_ingameResolution).Y;

    private bool ShouldUseNavEditorWindowGutter()
    {
        return _navEditorEnabled && !IsScreenFillingDisplayMode(_displayMode);
    }

    private void ApplyGraphicsSettings()
    {
        ApplyGraphicsSettings(persist: true);
    }

    private void ApplyGraphicsSettings(bool persist)
    {
        var previousDisplayMode = _displayMode;
        RememberWindowedPosition(previousDisplayMode);

        ApplyDisplayMode(_clientSettings.DisplayMode);
        ApplyIngameResolution(_clientSettings.IngameResolution);
        ApplyWindowSize(_clientSettings.WindowSize);
        ApplyDisplayScaleMode(_clientSettings.DisplayScaleMode);

        if (OperatingSystem.IsBrowser())
        {
            // Browser rendering stays on the host canvas size; only the logical viewport changes.
            ApplyDisplayMode(DisplayModeKind.Windowed);
            _graphics.IsFullScreen = false;
            SetWindowBorderless(borderless: false);
            _graphics.SynchronizeWithVerticalRetrace = false;
            _clientSettings.DisplayMode = DisplayModeKind.Windowed;
            if (persist)
            {
                PersistClientSettings();
            }
            return;
        }

        var displayMode = OpenGarrisonPreferencesDocument.NormalizeDisplayMode(_displayMode);
        var isFullscreen = displayMode == DisplayModeKind.Fullscreen;
        var isBorderless = IsBorderlessDisplayMode(displayMode);
        if (!isFullscreen)
        {
            _graphics.IsFullScreen = false;
        }

        if (displayMode == DisplayModeKind.Windowed)
        {
            Window.AllowUserResizing = true;
            RestoreNativeWindowedState(updateSize: false);
        }

        SetWindowBorderless(isBorderless);
        _graphics.IsFullScreen = isFullscreen;
        _graphics.SynchronizeWithVerticalRetrace = _clientSettings.VSync;
        ApplyPreferredBackBufferSize(displayMode, _ingameResolution, _windowSize);
        _graphics.ApplyChanges();
        SetWindowBorderless(isBorderless);
        if (displayMode == DisplayModeKind.Windowed)
        {
            Window.AllowUserResizing = true;
            RestoreNativeWindowedState(updateSize: true);
            PositionWindowedWindowAfterModeChange(previousDisplayMode);
        }
        else if (displayMode == DisplayModeKind.Borderless)
        {
            PositionBorderlessWindow();
        }
        else if (displayMode == DisplayModeKind.BorderlessWindow)
        {
            PositionBorderlessWindowCentered();
        }

        Window.AllowUserResizing = IsUserResizableDisplayMode(displayMode);
        if (persist)
        {
            PersistClientSettings();
        }
    }

    private void ApplyDisplayMode(DisplayModeKind displayMode)
    {
        _displayMode = OperatingSystem.IsBrowser()
            ? DisplayModeKind.Windowed
            : OpenGarrisonPreferencesDocument.NormalizeDisplayMode(displayMode);
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

    private void ApplyDisplayScaleMode(DisplayScaleModeKind displayScaleMode)
    {
        _displayScaleMode = OpenGarrisonPreferencesDocument.NormalizeDisplayScaleMode(displayScaleMode);
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
        var presentationDestination = GetPresentationDestinationRectangle();
        WriteGameplayRenderTrace("frame endlogical spritebatchbegin-2");
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
        WriteGameplayRenderTrace("frame endlogical draw-rendertarget");
        _spriteBatch.Draw(_gameRenderTarget, presentationDestination, Color.White);
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
            var fallback = GetPreferredBackBufferDimensions(DisplayModeKind.Windowed, _ingameResolution, _windowSize);
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
            var fallback = GetPreferredBackBufferDimensions(DisplayModeKind.Windowed, _ingameResolution, _windowSize);
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
        if (ShouldUsePixelPerfectDisplayScale())
        {
            var integerScale = MathF.Floor(scale);
            if (integerScale >= 1f)
            {
                scale = integerScale;
            }
        }

        var destinationWidth = Math.Max(1, (int)MathF.Floor(ViewportWidth * scale));
        var destinationHeight = Math.Max(1, (int)MathF.Floor(ViewportHeight * scale));
        return new Rectangle(
            (availableWidth - destinationWidth) / 2,
            (surfaceHeight - destinationHeight) / 2,
            destinationWidth,
            destinationHeight);
    }

    private bool ShouldUsePixelPerfectDisplayScale()
    {
        // The pixel-perfect screen-scale option has been retired from the UI; Fill is the only
        // supported behavior. Force Fill here so any preferences file that still has a stored
        // pixel-perfect value renders correctly instead of leaving the player stuck on it.
        return false;
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
        if (!IsScreenFillingDisplayMode(_displayMode) || !IsActive)
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

    private void ApplyPreferredBackBufferSize(DisplayModeKind displayMode, IngameResolutionKind ingameResolution, WindowSizeKind windowSize)
    {
        var preferredDimensions = GetWindowDimensions(displayMode, ingameResolution, windowSize);
        _graphics.PreferredBackBufferWidth = preferredDimensions.X;
        _graphics.PreferredBackBufferHeight = preferredDimensions.Y;
    }

    private Point GetWindowDimensions(DisplayModeKind displayMode, IngameResolutionKind ingameResolution, WindowSizeKind windowSize)
    {
        var gameplayDimensions = GetPreferredBackBufferDimensions(displayMode, ingameResolution, windowSize);
        if (IsScreenFillingDisplayMode(displayMode) || !_navEditorEnabled)
        {
            return gameplayDimensions;
        }

        return new Point(
            gameplayDimensions.X + GetNavEditorWindowGutterWidth(),
            Math.Max(gameplayDimensions.Y, GetNavEditorExpandedWindowHeight()));
    }

    private void RefreshNavEditorWindowGutter()
    {
        var preferredDimensions = GetWindowDimensions(_displayMode, _ingameResolution, _windowSize);
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

    private static Point GetPreferredBackBufferDimensions(DisplayModeKind displayMode, IngameResolutionKind ingameResolution, WindowSizeKind windowSize)
    {
        if (IsScreenFillingDisplayMode(displayMode) && !OperatingSystem.IsBrowser())
        {
            var adapterDisplayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            return new Point(adapterDisplayMode.Width, adapterDisplayMode.Height);
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
            IngameResolutionKind.Aspect16x9 => new Point(960, 540),
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

    private static string GetDisplayScaleModeLabel(DisplayScaleModeKind displayScaleMode)
    {
        return OpenGarrisonPreferencesDocument.NormalizeDisplayScaleMode(displayScaleMode) switch
        {
            DisplayScaleModeKind.PixelPerfect => "Pixel-Perfect",
            _ => "Fill",
        };
    }

    private static string GetDisplayModeLabel(DisplayModeKind displayMode)
    {
        return OpenGarrisonPreferencesDocument.NormalizeDisplayMode(displayMode) switch
        {
            DisplayModeKind.BorderlessWindow => "Borderless Windowed",
            DisplayModeKind.Borderless => "Borderless Fullscreen",
            DisplayModeKind.Fullscreen => "Fullscreen",
            _ => "Windowed",
        };
    }

    private static DisplayModeKind GetNextDisplayMode(DisplayModeKind displayMode)
    {
        return OpenGarrisonPreferencesDocument.NormalizeDisplayMode(displayMode) switch
        {
            DisplayModeKind.Windowed => DisplayModeKind.BorderlessWindow,
            DisplayModeKind.BorderlessWindow => DisplayModeKind.Borderless,
            DisplayModeKind.Borderless => DisplayModeKind.Fullscreen,
            _ => DisplayModeKind.Windowed,
        };
    }

    private static DisplayScaleModeKind GetNextDisplayScaleMode(DisplayScaleModeKind displayScaleMode)
    {
        return OpenGarrisonPreferencesDocument.NormalizeDisplayScaleMode(displayScaleMode) switch
        {
            DisplayScaleModeKind.Fill => DisplayScaleModeKind.PixelPerfect,
            _ => DisplayScaleModeKind.Fill,
        };
    }

    private static bool IsScreenFillingDisplayMode(DisplayModeKind displayMode)
    {
        return OpenGarrisonPreferencesDocument.NormalizeDisplayMode(displayMode) is DisplayModeKind.Borderless or DisplayModeKind.Fullscreen;
    }

    private static bool IsBorderlessDisplayMode(DisplayModeKind displayMode)
    {
        return OpenGarrisonPreferencesDocument.NormalizeDisplayMode(displayMode) is DisplayModeKind.Borderless or DisplayModeKind.BorderlessWindow;
    }

    private static bool IsUserResizableDisplayMode(DisplayModeKind displayMode)
    {
        return OpenGarrisonPreferencesDocument.NormalizeDisplayMode(displayMode) == DisplayModeKind.Windowed;
    }

    private void SetWindowBorderless(bool borderless)
    {
        try
        {
            Window.IsBorderless = borderless;
        }
        catch (NotImplementedException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }

    private void RememberWindowedPosition(DisplayModeKind displayMode)
    {
        if (OpenGarrisonPreferencesDocument.NormalizeDisplayMode(displayMode) != DisplayModeKind.Windowed)
        {
            return;
        }

        if (TryGetWindowPosition(out var position))
        {
            _lastWindowedPosition = position;
        }
    }

    private void PositionWindowedWindowAfterModeChange(DisplayModeKind previousDisplayMode)
    {
        if (OpenGarrisonPreferencesDocument.NormalizeDisplayMode(previousDisplayMode) == DisplayModeKind.Windowed)
        {
            return;
        }

        var preferredDimensions = GetWindowDimensions(DisplayModeKind.Windowed, _ingameResolution, _windowSize);
        var targetPosition = _lastWindowedPosition ?? GetCenteredWindowPosition(preferredDimensions);
        TrySetWindowPosition(targetPosition);
        SetNativeWindowPosition(targetPosition);
    }

    private static Point GetCenteredWindowPosition(Point dimensions)
    {
        var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        return new Point(
            Math.Max(0, (displayMode.Width - dimensions.X) / 2),
            Math.Max(0, (displayMode.Height - dimensions.Y) / 2));
    }

#if BROWSER_KNI
    private bool TryGetWindowPosition(out Point position)
    {
        position = default;
        return false;
    }

    private void TrySetWindowPosition(Point position)
    {
    }

    private void RestoreNativeWindowedState(bool updateSize)
    {
    }

    private void SetNativeWindowPosition(Point position)
    {
    }

    private void PositionBorderlessWindow()
    {
    }

    private void PositionBorderlessWindowCentered()
    {
    }
#else
    private bool TryGetWindowPosition(out Point position)
    {
        try
        {
            position = Window.Position;
            return true;
        }
        catch (NotImplementedException)
        {
        }
        catch (NotSupportedException)
        {
        }

        position = default;
        return false;
    }

    private void TrySetWindowPosition(Point position)
    {
        try
        {
            Window.Position = position;
        }
        catch (NotImplementedException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }

    private void RestoreNativeWindowedState(bool updateSize)
    {
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        try
        {
            var handle = Window.Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            _ = SDL_SetWindowFullscreen(handle, 0);
            SDL_SetWindowBordered(handle, 1);
            SDL_SetWindowResizable(handle, 1);
            if (updateSize)
            {
                SDL_SetWindowSize(
                    handle,
                    Math.Max(1, _graphics.PreferredBackBufferWidth),
                    Math.Max(1, _graphics.PreferredBackBufferHeight));
            }
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
        catch (BadImageFormatException)
        {
        }
        catch (NotImplementedException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }

    private void SetNativeWindowPosition(Point position)
    {
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        try
        {
            var handle = Window.Handle;
            if (handle != IntPtr.Zero)
            {
                SDL_SetWindowPosition(handle, position.X, position.Y);
            }
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
        catch (BadImageFormatException)
        {
        }
        catch (NotImplementedException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }

    private void PositionBorderlessWindow()
    {
        try
        {
            Window.Position = Point.Zero;
        }
        catch (NotImplementedException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }

    private void PositionBorderlessWindowCentered()
    {
        try
        {
            var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            var x = Math.Max(0, (displayMode.Width - _graphics.PreferredBackBufferWidth) / 2);
            var y = Math.Max(0, (displayMode.Height - _graphics.PreferredBackBufferHeight) / 2);
            Window.Position = new Point(x, y);
        }
        catch (NotImplementedException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SDL_SetWindowFullscreen(IntPtr window, uint flags);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_SetWindowBordered(IntPtr window, int bordered);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_SetWindowResizable(IntPtr window, int resizable);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_SetWindowSize(IntPtr window, int width, int height);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_SetWindowPosition(IntPtr window, int x, int y);
#endif

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
