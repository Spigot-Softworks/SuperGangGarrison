#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Client.Rendering.Crt;
using OpenGarrison.Core;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    public CrtPresentationRenderer? _crtPresentationRenderer;
    public string? _crtPresentationFailureReason;
    private bool _crtInputMappingReady;
    private bool _crtDeviceResetSubscribed;
    private int _crtMappedSourceWidth;
    private int _crtMappedSourceHeight;
    private Rectangle _crtMappedDestination;
    private bool _crtInputScrollInitialized;
    private int _crtLastRawScrollWheelValue;
    private long _crtSuppressedScrollWheelOffset;
    private bool _crtSuppressLeftButtonUntilRelease;
    private bool _crtSuppressMiddleButtonUntilRelease;
    private bool _crtSuppressRightButtonUntilRelease;
    private bool _crtSuppressXButton1UntilRelease;
    private bool _crtSuppressXButton2UntilRelease;

    private bool IsCrtEnabledBySettings => !OperatingSystem.IsBrowser()
        && !_crtStartupForcedOff
        && _clientSettings.CrtPreset != CrtPresetKind.Off;

    private bool ShouldUseCrtPresentation => IsCrtEnabledBySettings
        && string.IsNullOrWhiteSpace(_crtPresentationFailureReason);

    private void OnCrtSettingsChanged()
    {
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        _crtPresentationFailureReason = null;
        InvalidateCrtPresentationMapping(releaseResources: false);
        if (!IsCrtEnabledBySettings)
        {
            DisposeCrtPresentationRenderer();
            return;
        }

        if (_spriteBatch is not null && Content is not null)
        {
            EnsureCrtDeviceResetSubscription();
        }
    }

    public string GetCrtQualityStatusLabel()
    {
        if (OperatingSystem.IsBrowser())
        {
            return "Unavailable";
        }

        if (_clientSettings.CrtPreset == CrtPresetKind.Off)
        {
            return "Off";
        }

        if (_crtStartupForcedOff)
        {
            return "Off (startup)";
        }

        var requestedQuality = GetCrtQualityName(_clientSettings.CrtQuality);
        if (!string.IsNullOrWhiteSpace(_crtPresentationFailureReason))
        {
            return $"{requestedQuality} (Off fallback)";
        }

        var renderer = _crtPresentationRenderer;
        if (renderer is null || !renderer.IsActive)
        {
            return $"{requestedQuality} (pending)";
        }

        var effectiveQuality = GetCrtQualityName(renderer.EffectiveQuality);
        var isFallback = !string.IsNullOrWhiteSpace(renderer.FallbackReason);
        if (isFallback)
        {
            return $"{requestedQuality} ({effectiveQuality} fallback)";
        }

        return string.Equals(requestedQuality, effectiveQuality, StringComparison.Ordinal)
            ? effectiveQuality
            : $"{requestedQuality} ({effectiveQuality})";
    }

    private static string GetCrtQualityName(CrtQualityKind quality)
    {
        return quality switch
        {
            CrtQualityKind.Balanced => "Balanced",
            CrtQualityKind.High => "High",
            _ => "Auto",
        };
    }

    private void EnsureCrtDeviceResetSubscription()
    {
        if (_crtDeviceResetSubscribed || OperatingSystem.IsBrowser())
        {
            return;
        }

        GraphicsDevice.DeviceReset += OnCrtGraphicsDeviceReset;
        _crtDeviceResetSubscribed = true;
    }

    private void OnCrtGraphicsDeviceReset(object? sender, EventArgs eventArgs)
    {
        _crtPresentationFailureReason = null;
        InvalidateCrtPresentationMapping();
    }

    private CrtPresentationRenderer? EnsureCrtPresentationRenderer()
    {
        if (_crtPresentationRenderer is not null)
        {
            return _crtPresentationRenderer;
        }

        if (!ShouldUseCrtPresentation || OperatingSystem.IsBrowser() || _spriteBatch is null)
        {
            return null;
        }

        EnsureCrtDeviceResetSubscription();
        try
        {
            _crtPresentationRenderer = new CrtPresentationRenderer(
                GraphicsDevice,
                Content,
                message => Console.WriteLine($"CRT: {message}"));
            return _crtPresentationRenderer;
        }
        catch (Exception ex) when (IsRecoverableCrtResourceFailure(ex))
        {
            DisableCrtPresentationForSession($"presentation effect unavailable: {ex.Message}");
            return null;
        }
    }

    private bool TryPresentCrtFrame(Texture2D source, Rectangle destination)
    {
        var viewport = GraphicsDevice.Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0 || destination.Width <= 0 || destination.Height <= 0)
        {
            _crtInputMappingReady = false;
            return false;
        }

        var renderer = EnsureCrtPresentationRenderer();
        if (renderer is null)
        {
            return false;
        }

        if (_crtInputMappingReady
            && (_crtMappedSourceWidth != source.Width
                || _crtMappedSourceHeight != source.Height
                || _crtMappedDestination != destination))
        {
            InvalidateCrtPresentationMapping();
        }

        bool prepared;
        try
        {
            prepared = renderer.Prepare(
                _clientSettings.CrtPreset,
                _clientSettings.CrtQuality,
                _clientSettings.CrtSignalMode,
                _clientSettings.CrtCurvatureEnabled,
                _clientSettings.CrtBrightnessPercent,
                source.Width,
                source.Height,
                destination);
        }
        catch (Exception ex) when (IsRecoverableCrtResourceFailure(ex))
        {
            DisableCrtPresentationForSession($"presentation resources unavailable: {ex.Message}");
            return false;
        }

        if (!prepared || !renderer.IsActive)
        {
            var reason = string.IsNullOrWhiteSpace(renderer.FallbackReason)
                ? "renderer reported CRT unavailable"
                : renderer.FallbackReason;
            DisableCrtPresentationForSession(reason);
            return false;
        }

        // The renderer owns its isolated SpriteBatch and restores graphics state
        // after each presentation attempt. A false result leaves no partial frame,
        // so the caller can use the ordinary blit.
        bool presented;
        try
        {
            presented = renderer.Present(source, destination);
        }
        catch (Exception ex) when (IsRecoverableCrtResourceFailure(ex))
        {
            DisableCrtPresentationForSession($"presentation failed: {ex.Message}");
            return false;
        }

        if (!presented)
        {
            var reason = string.IsNullOrWhiteSpace(renderer.FallbackReason)
                ? "presentation pass unavailable"
                : renderer.FallbackReason;
            DisableCrtPresentationForSession(reason);
            return false;
        }

        _crtMappedSourceWidth = source.Width;
        _crtMappedSourceHeight = source.Height;
        _crtMappedDestination = destination;
        _crtInputMappingReady = renderer.CurvatureActive;
        return true;
    }

    private void DisableCrtPresentationForSession(string reason)
    {
        if (!string.IsNullOrWhiteSpace(_crtPresentationFailureReason))
        {
            return;
        }

        _crtPresentationFailureReason = string.IsNullOrWhiteSpace(reason)
            ? "CRT presentation unavailable"
            : reason;
        _crtInputMappingReady = false;
        Console.WriteLine($"CRT fallback: {_crtPresentationFailureReason}");
    }

    private void InvalidateCrtPresentationMapping(bool releaseResources = true)
    {
        _crtInputMappingReady = false;
        _crtMappedSourceWidth = 0;
        _crtMappedSourceHeight = 0;
        _crtMappedDestination = Rectangle.Empty;
        _crtPresentationRenderer?.Invalidate(releaseResources);
    }

    private void DisposeCrtPresentationRenderer()
    {
        _crtPresentationRenderer?.Dispose();
        _crtPresentationRenderer = null;
        _crtInputMappingReady = false;
    }

    public void UnloadCrtPresentation()
    {
        if (_crtDeviceResetSubscribed && !OperatingSystem.IsBrowser())
        {
            GraphicsDevice.DeviceReset -= OnCrtGraphicsDeviceReset;
            _crtDeviceResetSubscribed = false;
        }

        DisposeCrtPresentationRenderer();
        _crtPresentationFailureReason = null;
    }

    private static bool IsRecoverableCrtResourceFailure(Exception exception)
    {
        return exception is ContentLoadException
            or OutOfMemoryException
            or InvalidOperationException
            or NotSupportedException;
    }

    private bool TryMapCurvedCrtPointer(
        MouseState rawMouse,
        Rectangle inputDestination,
        out MouseState mappedMouse)
    {
        mappedMouse = default;
        if (!ShouldUseCrtPresentation || !_crtInputMappingReady || _crtPresentationRenderer is null)
        {
            return false;
        }

        if (_crtMappedSourceWidth != ViewportWidth
            || _crtMappedSourceHeight != ViewportHeight
            || _crtMappedDestination != GetPresentationDestinationRectangle())
        {
            InvalidateCrtPresentationMapping();
            return false;
        }

        if (!_crtPresentationRenderer.CurvatureActive)
        {
            return false;
        }

        var outputUv = new Vector2(
            (rawMouse.X - inputDestination.X + 0.5f) / inputDestination.Width,
            (rawMouse.Y - inputDestination.Y + 0.5f) / inputDestination.Height);
        var sourceUv = _crtPresentationRenderer.MapOutputToSource(outputUv);
        var logicalX = MapCrtUvToLogicalCoordinate(sourceUv.X, ViewportWidth);
        var logicalY = MapCrtUvToLogicalCoordinate(sourceUv.Y, ViewportHeight);
        if (outputUv.X < 0f)
        {
            logicalX = -1;
        }
        else if (outputUv.X > 1f)
        {
            logicalX = ViewportWidth;
        }

        if (outputUv.Y < 0f)
        {
            logicalY = -1;
        }
        else if (outputUv.Y > 1f)
        {
            logicalY = ViewportHeight;
        }

        var visible = outputUv.X >= 0f
            && outputUv.X <= 1f
            && outputUv.Y >= 0f
            && outputUv.Y <= 1f
            && float.IsFinite(sourceUv.X)
            && float.IsFinite(sourceUv.Y)
            && sourceUv.X >= 0f
            && sourceUv.X <= 1f
            && sourceUv.Y >= 0f
            && sourceUv.Y <= 1f;
        var scrollWheelValue = GetCrtAwareScrollWheelValue(rawMouse.ScrollWheelValue, visible);
        var leftButton = GetCrtAwareButtonState(rawMouse.LeftButton, ref _crtSuppressLeftButtonUntilRelease, visible);
        var middleButton = GetCrtAwareButtonState(rawMouse.MiddleButton, ref _crtSuppressMiddleButtonUntilRelease, visible);
        var rightButton = GetCrtAwareButtonState(rawMouse.RightButton, ref _crtSuppressRightButtonUntilRelease, visible);
        var xButton1 = GetCrtAwareButtonState(rawMouse.XButton1, ref _crtSuppressXButton1UntilRelease, visible);
        var xButton2 = GetCrtAwareButtonState(rawMouse.XButton2, ref _crtSuppressXButton2UntilRelease, visible);
        mappedMouse = new MouseState(
            logicalX,
            logicalY,
            scrollWheelValue,
            leftButton,
            middleButton,
            rightButton,
            xButton1,
            xButton2);
        return true;
    }

    private static int MapCrtUvToLogicalCoordinate(float sourceUv, int logicalSize)
    {
        if (!float.IsFinite(sourceUv) || sourceUv < 0f)
        {
            return -1;
        }

        if (sourceUv > 1f)
        {
            return logicalSize;
        }

        return Math.Clamp((int)MathF.Floor(sourceUv * logicalSize), 0, logicalSize - 1);
    }

    private int GetCrtAwareScrollWheelValue(int rawValue, bool pointerVisible)
    {
        if (!_crtInputScrollInitialized)
        {
            _crtInputScrollInitialized = true;
            _crtLastRawScrollWheelValue = rawValue;
            _crtSuppressedScrollWheelOffset = 0L;
            return rawValue;
        }

        var delta = (long)rawValue - _crtLastRawScrollWheelValue;
        if (!pointerVisible)
        {
            _crtSuppressedScrollWheelOffset += delta;
        }

        _crtLastRawScrollWheelValue = rawValue;
        var logicalValue = (long)rawValue - _crtSuppressedScrollWheelOffset;
        return (int)Math.Clamp(logicalValue, int.MinValue, int.MaxValue);
    }

    private static ButtonState GetCrtAwareButtonState(ButtonState rawState, ref bool suppressUntilRelease, bool pointerVisible)
    {
        if (!pointerVisible && rawState == ButtonState.Pressed)
        {
            suppressUntilRelease = true;
        }

        if (rawState == ButtonState.Released)
        {
            suppressUntilRelease = false;
            return ButtonState.Released;
        }

        return suppressUntilRelease ? ButtonState.Released : rawState;
    }

    private MouseState GetCrtInputMouseStateWhenUnwarped(MouseState rawMouse)
    {
        var scrollWheelValue = rawMouse.ScrollWheelValue;
        if (_crtInputScrollInitialized)
        {
            _crtLastRawScrollWheelValue = rawMouse.ScrollWheelValue;
            var logicalValue = (long)rawMouse.ScrollWheelValue - _crtSuppressedScrollWheelOffset;
            scrollWheelValue = (int)Math.Clamp(logicalValue, int.MinValue, int.MaxValue);
        }

        return new MouseState(
            rawMouse.X,
            rawMouse.Y,
            scrollWheelValue,
            GetCrtAwareButtonState(rawMouse.LeftButton, ref _crtSuppressLeftButtonUntilRelease, pointerVisible: true),
            GetCrtAwareButtonState(rawMouse.MiddleButton, ref _crtSuppressMiddleButtonUntilRelease, pointerVisible: true),
            GetCrtAwareButtonState(rawMouse.RightButton, ref _crtSuppressRightButtonUntilRelease, pointerVisible: true),
            GetCrtAwareButtonState(rawMouse.XButton1, ref _crtSuppressXButton1UntilRelease, pointerVisible: true),
            GetCrtAwareButtonState(rawMouse.XButton2, ref _crtSuppressXButton2UntilRelease, pointerVisible: true));
    }
}
