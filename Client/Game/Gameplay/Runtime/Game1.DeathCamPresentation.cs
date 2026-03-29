using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int DeathCamFocusDelayTicks = 60;
    private const float DeathCamZoomStart = 1f;
    private const float DeathCamZoomEnd = 2f;
    private const float DeathCamZoomTicks = 10f;

    private RenderTarget2D? _deathCamCaptureTarget;
    private bool _deathCamCaptureValid;
    private Vector2 _lastLiveCameraTopLeft;
    private Vector2 _deathCamEntryCameraTopLeft;
    private bool _hasDeathCamEntryCameraTopLeft;
    private int _deathCamTrackedInitialTicks;
    private int _deathCamTrackedRemainingTicks = -1;

    private bool IsDeathCamPresentationActive()
    {
        return _killCamEnabled && !_world.LocalPlayer.IsAlive && _world.LocalDeathCam is not null;
    }

    private static int GetDeathCamInitialTicks(LocalDeathCamState deathCam)
    {
        return deathCam.InitialTicks > 0 ? deathCam.InitialTicks : deathCam.RemainingTicks;
    }

    private int GetDeathCamElapsedTicks(LocalDeathCamState deathCam)
    {
        return Math.Max(0, GetDeathCamInitialTicks(deathCam) - deathCam.RemainingTicks);
    }

    private bool IsDeathCamZoomPhase(LocalDeathCamState deathCam)
    {
        return GetDeathCamElapsedTicks(deathCam) >= DeathCamFocusDelayTicks;
    }

    private float GetDeathCamZoom(LocalDeathCamState deathCam)
    {
        var zoomTicks = Math.Max(0f, GetDeathCamElapsedTicks(deathCam) - DeathCamFocusDelayTicks);
        return Math.Clamp(
            DeathCamZoomStart + (zoomTicks / DeathCamZoomTicks) * (DeathCamZoomEnd - DeathCamZoomStart),
            DeathCamZoomStart,
            DeathCamZoomEnd);
    }

    private void TrackLiveCamera(Vector2 cameraTopLeft)
    {
        _lastLiveCameraTopLeft = cameraTopLeft;
    }

    private void ClearDeathCamPresentation()
    {
        _deathCamCaptureValid = false;
        _hasDeathCamEntryCameraTopLeft = false;
        _deathCamTrackedInitialTicks = 0;
        _deathCamTrackedRemainingTicks = -1;
    }

    private void SyncDeathCamPresentationState()
    {
        if (!IsDeathCamPresentationActive())
        {
            ClearDeathCamPresentation();
            return;
        }

        var deathCam = _world.LocalDeathCam!;
        var initialTicks = GetDeathCamInitialTicks(deathCam);
        var isNewDeathCam = !_hasDeathCamEntryCameraTopLeft
            || _deathCamTrackedRemainingTicks < 0
            || initialTicks != _deathCamTrackedInitialTicks
            || deathCam.RemainingTicks > _deathCamTrackedRemainingTicks;
        if (isNewDeathCam)
        {
            _deathCamEntryCameraTopLeft = _lastLiveCameraTopLeft;
            _hasDeathCamEntryCameraTopLeft = true;
            _deathCamCaptureValid = false;
            _deathCamTrackedInitialTicks = initialTicks;
        }

        _deathCamTrackedRemainingTicks = deathCam.RemainingTicks;
    }

    private Vector2 GetDeathCamFocusCameraTopLeft(int viewportWidth, int viewportHeight)
    {
        var deathCam = _world.LocalDeathCam!;
        var halfViewportWidth = viewportWidth / 2f;
        var halfViewportHeight = viewportHeight / 2f;
        var x = Math.Clamp(
            deathCam.FocusX - halfViewportWidth,
            0f,
            Math.Max(0f, _world.Bounds.Width - viewportWidth));
        var y = Math.Clamp(
            deathCam.FocusY - halfViewportHeight,
            0f,
            Math.Max(0f, _world.Bounds.Height - viewportHeight));
        return new Vector2(x, y);
    }

    private Vector2 GetDeathCamCameraTopLeft(int viewportWidth, int viewportHeight)
    {
        SyncDeathCamPresentationState();
        var deathCam = _world.LocalDeathCam!;
        if (!IsDeathCamZoomPhase(deathCam))
        {
            return _deathCamEntryCameraTopLeft;
        }

        return GetDeathCamFocusCameraTopLeft(viewportWidth, viewportHeight);
    }

    private void EnsureDeathCamCaptureTarget(int viewportWidth, int viewportHeight)
    {
        if (_deathCamCaptureTarget is not null
            && _deathCamCaptureTarget.Width == viewportWidth
            && _deathCamCaptureTarget.Height == viewportHeight)
        {
            return;
        }

        _deathCamCaptureTarget?.Dispose();
        _deathCamCaptureTarget = new RenderTarget2D(
            GraphicsDevice,
            viewportWidth,
            viewportHeight,
            false,
            SurfaceFormat.Color,
            DepthFormat.None,
            0,
            RenderTargetUsage.PreserveContents);
        _deathCamCaptureValid = false;
    }

    private void PrepareDeathCamCaptureIfNeeded(int viewportWidth, int viewportHeight)
    {
        if (!IsDeathCamPresentationActive())
        {
            return;
        }

        SyncDeathCamPresentationState();
        var deathCam = _world.LocalDeathCam!;
        if (!IsDeathCamZoomPhase(deathCam) || _deathCamCaptureValid)
        {
            return;
        }

        EnsureDeathCamCaptureTarget(viewportWidth, viewportHeight);
        var focusCameraPosition = GetDeathCamFocusCameraTopLeft(viewportWidth, viewportHeight);
        GraphicsDevice.SetRenderTarget(_deathCamCaptureTarget);
        GraphicsDevice.Clear(new Color(24, 32, 48));
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
        DrawGameplayWorldForCamera(focusCameraPosition, viewportWidth, viewportHeight);
        _spriteBatch.End();
        GraphicsDevice.SetRenderTarget(null);
        _deathCamCaptureValid = true;
    }

    private bool DrawDeathCamCaptureOverlay(int viewportWidth, int viewportHeight)
    {
        if (!IsDeathCamPresentationActive() || !_deathCamCaptureValid || _deathCamCaptureTarget is null)
        {
            return false;
        }

        var deathCam = _world.LocalDeathCam!;
        if (!IsDeathCamZoomPhase(deathCam))
        {
            return false;
        }

        var scale = GetDeathCamZoom(deathCam);
        var center = new Vector2(viewportWidth / 2f, viewportHeight / 2f);
        var origin = new Vector2(_deathCamCaptureTarget.Width / 2f, _deathCamCaptureTarget.Height / 2f);
        _spriteBatch.Draw(_deathCamCaptureTarget, center, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
        return true;
    }
}
