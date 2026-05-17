#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const float SmoothCameraResetDeltaPixels = 48f;
    private const float SmoothCameraPixelHysteresisPixels = 0.75f;
    private const float SmoothCameraMinFollowPerFrame = 0.08f;
    private const float SmoothCameraMaxFollowPerFrame = 0.5f;

    private Vector2 GetCameraTopLeft(int viewportWidth, int viewportHeight, int mouseX, int mouseY)
    {
        var cameraTopLeft = CalculateBaseCameraTopLeft(viewportWidth, viewportHeight, mouseX, mouseY, trackLiveCamera: true);
        cameraTopLeft = ApplySmoothCameraY(cameraTopLeft);
        return RoundToSourcePixels(cameraTopLeft + GetClientPluginCameraOffset() + GetLastToDieCameraShakeOffset());
    }

    private Vector2 CalculateBaseCameraTopLeft(int viewportWidth, int viewportHeight, int mouseX, int mouseY, bool trackLiveCamera)
    {
        Vector2 cameraTopLeft;
        if (IsDeathCamPresentationActive())
        {
            cameraTopLeft = GetDeathCamCameraTopLeft(viewportWidth, viewportHeight);
            if (trackLiveCamera)
            {
                TrackLiveCamera(cameraTopLeft);
            }

            return cameraTopLeft;
        }

        if (_networkClient.IsSpectator && GetSpectatorFocusPlayer() is PlayerEntity trackedPlayer)
        {
            if (GetPlayerIsUsingBinoculars(trackedPlayer))
            {
                cameraTopLeft = GetBinocularsCameraTopLeft(trackedPlayer, viewportWidth, viewportHeight);
                if (trackLiveCamera)
                {
                    TrackLiveCamera(cameraTopLeft);
                }
                return cameraTopLeft;
            }
            else if (GetPlayerIsSniperScoped(trackedPlayer))
            {
                cameraTopLeft = GetSpectatorScopedSniperCameraTopLeft(trackedPlayer, viewportWidth, viewportHeight);
                if (trackLiveCamera)
                {
                    TrackLiveCamera(cameraTopLeft);
                }
                return cameraTopLeft;
            }
        }

        var localViewPosition = GetLocalViewPosition();
        if (_world.LocalPlayer.IsAlive && GetPlayerIsUsingBinoculars(_world.LocalPlayer))
        {
            cameraTopLeft = GetBinocularsCameraTopLeft(_world.LocalPlayer, viewportWidth, viewportHeight);
        }
        else if (_world.LocalPlayer.IsAlive && GetPlayerIsSniperScoped(_world.LocalPlayer))
        {
            cameraTopLeft = new Vector2(
                localViewPosition.X + mouseX - viewportWidth,
                localViewPosition.Y + mouseY - viewportHeight);
        }
        else
        {
            var halfViewportWidth = viewportWidth / 2f;
            var halfViewportHeight = viewportHeight / 2f;

            cameraTopLeft = new Vector2(
                localViewPosition.X - halfViewportWidth,
                localViewPosition.Y - halfViewportHeight);
        }

        if (trackLiveCamera)
        {
            TrackLiveCamera(cameraTopLeft);
        }

        return cameraTopLeft;
    }

    private Vector2 GetLocalViewPosition()
    {
        if (_networkClient.IsSpectator)
        {
            if (_respawnCameraDetached)
            {
                return _respawnCameraCenter;
            }

            var spectatorFocus = GetSpectatorFocusPlayer();
            if (spectatorFocus is not null)
            {
                return GetRenderPosition(spectatorFocus);
            }

            return _respawnCameraCenter;
        }

        if (IsRespawnFreeCameraActive())
        {
            return _respawnCameraCenter;
        }

        if (_networkClient.IsConnected && _world.LocalPlayer.IsAlive)
        {
            if (_hasSmoothedLocalPlayerRenderPosition)
            {
                return _smoothedLocalPlayerRenderPosition;
            }

            if (_hasPredictedLocalPlayerPosition)
            {
                return _predictedLocalPlayerPosition;
            }

            return GetRenderPosition(_world.LocalPlayer, allowInterpolation: true);
        }

        return new Vector2(_world.LocalPlayer.X, _world.LocalPlayer.Y);
    }

    private Vector2 GetDefaultFreeCameraCenter()
    {
        if (_networkClient.IsSpectator)
        {
            var spectatorFocus = GetSpectatorFocusPlayer();
            if (spectatorFocus is not null)
            {
                return GetRenderPosition(spectatorFocus);
            }

            return _respawnCameraCenter;
        }

        return new Vector2(_world.LocalPlayer.X, _world.LocalPlayer.Y);
    }

    private Vector2 GetSpectatorScopedSniperCameraTopLeft(PlayerEntity trackedPlayer, int viewportWidth, int viewportHeight)
    {
        var trackedPlayerPosition = GetRenderPosition(trackedPlayer);
        var aimWorldPosition = GetRenderAimWorldPosition(trackedPlayer);
        var scopedCameraCenter = (trackedPlayerPosition + aimWorldPosition) / 2f;
        return new Vector2(
            scopedCameraCenter.X - (viewportWidth / 2f),
            scopedCameraCenter.Y - (viewportHeight / 2f));
    }

    private Vector2 GetBinocularsCameraTopLeft(PlayerEntity player, int viewportWidth, int viewportHeight)
    {
        var playerPosition = GetRenderPosition(player);
        var binocularsFocusX = player.BinocularsFocusX;
        var binocularsFocusY = player.BinocularsFocusY;
        
        // Clamp the view distance to max binoculars range
        var deltaX = binocularsFocusX - playerPosition.X;
        var deltaY = binocularsFocusY - playerPosition.Y;
        var distance = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
        
        Vector2 focusPosition;
        if (distance > PlayerEntity.BinocularsMaxViewDistance)
        {
            var scale = PlayerEntity.BinocularsMaxViewDistance / distance;
            focusPosition = new Vector2(
                playerPosition.X + deltaX * scale,
                playerPosition.Y + deltaY * scale);
        }
        else
        {
            focusPosition = new Vector2(binocularsFocusX, binocularsFocusY);
        }
        
        // Center camera on the focus position
        return new Vector2(
            focusPosition.X - (viewportWidth / 2f),
            focusPosition.Y - (viewportHeight / 2f));
    }

    private Vector2 ApplySmoothCameraY(Vector2 cameraTopLeft)
    {
        var multiplier = NormalizeSmoothCameraMultiplier(_smoothCameraMultiplier);
        if (multiplier <= 0f || !ShouldSmoothCameraY())
        {
            _hasSmoothCameraY = false;
            return cameraTopLeft;
        }

        if (!_hasSmoothCameraY || MathF.Abs(cameraTopLeft.Y - _smoothCameraY) > SmoothCameraResetDeltaPixels)
        {
            _smoothCameraY = cameraTopLeft.Y;
            _smoothCameraPixelY = RoundToSourcePixel(cameraTopLeft.Y);
            _hasSmoothCameraY = true;
            return new Vector2(cameraTopLeft.X, _smoothCameraPixelY);
        }

        var elapsedFrames = MathF.Max(1f, MathF.Min(4f, _clientUpdateElapsedSeconds * 60f));
        var followPerFrame = MathHelper.Lerp(SmoothCameraMaxFollowPerFrame, SmoothCameraMinFollowPerFrame, multiplier);
        var alpha = 1f - MathF.Pow(1f - followPerFrame, elapsedFrames);
        _smoothCameraY = MathHelper.Lerp(_smoothCameraY, cameraTopLeft.Y, MathHelper.Clamp(alpha, 0f, 1f));
        _smoothCameraPixelY = ApplySmoothCameraPixelHysteresis(_smoothCameraPixelY, _smoothCameraY);
        return new Vector2(cameraTopLeft.X, _smoothCameraPixelY);
    }

    private static float ApplySmoothCameraPixelHysteresis(float currentPixelY, float smoothedY)
    {
        return MathF.Abs(smoothedY - currentPixelY) < SmoothCameraPixelHysteresisPixels
            ? currentPixelY
            : RoundToSourcePixel(smoothedY);
    }

    private bool ShouldSmoothCameraY()
    {
        if (IsDeathCamPresentationActive() || IsRespawnFreeCameraActive())
        {
            return false;
        }

        if (_networkClient.IsSpectator)
        {
            var spectatorFocus = GetSpectatorFocusPlayer();
            return spectatorFocus is null
                || (!GetPlayerIsUsingBinoculars(spectatorFocus) && !GetPlayerIsSniperScoped(spectatorFocus));
        }

        return !_world.LocalPlayer.IsAlive
            || (!GetPlayerIsUsingBinoculars(_world.LocalPlayer) && !GetPlayerIsSniperScoped(_world.LocalPlayer));
    }
}
