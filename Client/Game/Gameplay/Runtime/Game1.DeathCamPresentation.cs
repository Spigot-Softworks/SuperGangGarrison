using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    public const int DeathCamFocusDelayTicks = 60;
    public const float DeathCamZoomStart = 1f;
    public const float DeathCamZoomEnd = 2f;
    public const float DeathCamZoomTicks = 10f;

    private Vector2 _lastLiveCameraTopLeft;
    private Vector2 _deathCamEntryCameraTopLeft;
    private bool _hasDeathCamEntryCameraTopLeft;
    private int _deathCamTrackedInitialTicks;
    private int _deathCamTrackedRemainingTicks = -1;

    private bool IsDeathCamPresentationActive()
    {
        return !IsHostedLastToDieActive()
            && _gameplayManager.RuntimeSettings.KillCamEnabled
            && !_world.LocalPlayer.IsAlive
            && _world.LocalDeathCam is not null;
    }

    private static int GetDeathCamInitialTicks(LocalDeathCamState deathCam)
    {
        return deathCam.InitialTicks > 0 ? deathCam.InitialTicks : deathCam.RemainingTicks;
    }

    public static int GetDeathCamElapsedTicks(LocalDeathCamState deathCam)
    {
        return Math.Max(0, GetDeathCamInitialTicks(deathCam) - deathCam.RemainingTicks);
    }

    private static bool IsDeathCamZoomPhase(LocalDeathCamState deathCam)
    {
        return GetDeathCamElapsedTicks(deathCam) >= DeathCamFocusDelayTicks;
    }

    private static float GetDeathCamZoom(LocalDeathCamState deathCam)
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
        _renderTargetResources.DeathCamCaptureValid = false;
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
            _renderTargetResources.DeathCamCaptureValid = false;
            _deathCamTrackedInitialTicks = initialTicks;
        }

        _deathCamTrackedRemainingTicks = deathCam.RemainingTicks;
    }

    private Vector2 GetDeathCamFocusCameraTopLeft(int viewportWidth, int viewportHeight)
    {
        var deathCam = _world.LocalDeathCam!;
        var halfViewportWidth = viewportWidth / 2f;
        var halfViewportHeight = viewportHeight / 2f;
        return CameraPanningState.ClampToMap(
            new Vector2(
                deathCam.FocusX - halfViewportWidth,
                deathCam.FocusY - halfViewportHeight),
            viewportWidth,
            viewportHeight,
            _world.Level.Bounds);
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
        if (_renderTargetResources.DeathCamCaptureTarget is not null
            && _renderTargetResources.DeathCamCaptureTarget.Width == viewportWidth
            && _renderTargetResources.DeathCamCaptureTarget.Height == viewportHeight)
        {
            return;
        }

        _renderTargetResources.DeathCamCaptureTarget?.Dispose();
        _renderTargetResources.DeathCamCaptureTarget = new RenderTarget2D(
            GraphicsDevice,
            viewportWidth,
            viewportHeight,
            false,
            SurfaceFormat.Color,
            DepthFormat.None,
            0,
            RenderTargetUsage.PreserveContents);
        _renderTargetResources.DeathCamCaptureValid = false;
    }

    public void PrepareDeathCamCaptureIfNeeded(int viewportWidth, int viewportHeight)
    {
        if (!IsDeathCamPresentationActive())
        {
            return;
        }

        SyncDeathCamPresentationState();
        var deathCam = _world.LocalDeathCam!;
        if (!IsDeathCamZoomPhase(deathCam) || _renderTargetResources.DeathCamCaptureValid)
        {
            return;
        }

        EnsureDeathCamCaptureTarget(viewportWidth, viewportHeight);
        var focusCameraPosition = GetDeathCamFocusCameraTopLeft(viewportWidth, viewportHeight);
        WriteGameplayRenderTrace("deathcam prepare setrendertarget");
        GraphicsDevice.SetRenderTarget(_renderTargetResources.DeathCamCaptureTarget);
        GraphicsDevice.Clear(new Color(24, 32, 48));
        WriteGameplayRenderTrace("deathcam prepare spritebatchbegin");
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
        WriteGameplayRenderTrace("deathcam prepare drawworld");
        DrawGameplayWorldForCamera(focusCameraPosition, viewportWidth, viewportHeight);
        WriteGameplayRenderTrace("deathcam prepare spritebatchend");
        _spriteBatch.End();
        WriteGameplayRenderTrace("deathcam prepare setrendertarget-null");
        GraphicsDevice.SetRenderTarget(null);
        _renderTargetResources.DeathCamCaptureValid = true;
    }

    public bool DrawDeathCamCaptureOverlay(int viewportWidth, int viewportHeight)
    {
        if (!IsDeathCamPresentationActive() || !_renderTargetResources.DeathCamCaptureValid || _renderTargetResources.DeathCamCaptureTarget is null)
        {
            return false;
        }

        var deathCam = _world.LocalDeathCam!;
        if (!IsDeathCamZoomPhase(deathCam))
        {
            return false;
        }

        var scale = GetDeathCamZoom(deathCam);
        var focusCameraPosition = GetDeathCamFocusCameraTopLeft(viewportWidth, viewportHeight);
        var focusScreenPosition = new Vector2(
            Math.Clamp(deathCam.FocusX - focusCameraPosition.X, 0f, viewportWidth),
            Math.Clamp(deathCam.FocusY - focusCameraPosition.Y, 0f, viewportHeight));
        _spriteBatch.Draw(_renderTargetResources.DeathCamCaptureTarget, focusScreenPosition, null, Color.White, 0f, focusScreenPosition, scale, SpriteEffects.None, 0f);
        return true;
    }
}
