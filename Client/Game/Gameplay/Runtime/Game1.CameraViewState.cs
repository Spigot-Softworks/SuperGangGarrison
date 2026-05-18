#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const float SmoothCameraSnapDeltaPixels = 256f;
    private const float SmoothCameraMinSmoothTimeSeconds = 0.022f;
    private const float SmoothCameraMaxSmoothTimeSeconds = 0.09f;
    private const float SmoothCameraFastMaxSpeedPixelsPerSecond = 9000f;
    private const float SmoothCameraSlowMaxSpeedPixelsPerSecond = 3600f;
    private const float SmoothCameraMinVerticalWindowPixels = 0.75f;
    private const float SmoothCameraMaxVerticalWindowPixels = 6f;
    private const float SmoothCameraMinPixelHysteresisPixels = 0.2f;
    private const float SmoothCameraMaxPixelHysteresisPixels = 0.7f;
    private const float SmoothCameraPixelHysteresisCatchUpPixels = 18f;
    private const float SmoothCameraTargetVelocityBlendTimeSeconds = 0.05f;
    private const float SmoothCameraVelocityDeadZonePixelsPerSecond = 18f;
    private const float SmoothCameraMinLookAheadSeconds = 0.008f;
    private const float SmoothCameraMaxLookAheadSeconds = 0.02f;
    private const float SmoothCameraMaxLookAheadPixels = 12f;
    private float _smoothCameraVelocityY;
    private bool _hasSmoothCameraPreviousTargetY;
    private float _smoothCameraPreviousTargetY;
    private float _smoothCameraTargetVelocityY;

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

        return _world.LocalPlayer.IsAlive
            ? GetRenderPosition(_world.LocalPlayer, allowInterpolation: true)
            : new Vector2(_world.LocalPlayer.X, _world.LocalPlayer.Y);
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

        return _world.LocalPlayer.IsAlive
            ? GetRenderPosition(_world.LocalPlayer, allowInterpolation: true)
            : new Vector2(_world.LocalPlayer.X, _world.LocalPlayer.Y);
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
            ResetSmoothCameraState();
            return cameraTopLeft;
        }

        var rawTargetY = cameraTopLeft.Y;
        if (!_hasSmoothCameraY
            || !float.IsFinite(_smoothCameraY)
            || MathF.Abs(rawTargetY - _smoothCameraY) > SmoothCameraSnapDeltaPixels)
        {
            ResetSmoothCameraState(rawTargetY);
            return new Vector2(cameraTopLeft.X, _smoothCameraPixelY);
        }

        var deltaSeconds = GetSmoothCameraDeltaSeconds();
        UpdateSmoothCameraTargetVelocity(rawTargetY, deltaSeconds);
        var targetY = GetSmoothCameraTargetY(rawTargetY, multiplier);
        var smoothTimeSeconds = MathHelper.Lerp(
            SmoothCameraMinSmoothTimeSeconds,
            SmoothCameraMaxSmoothTimeSeconds,
            multiplier);
        var maxSpeedPixelsPerSecond = MathHelper.Lerp(
            SmoothCameraFastMaxSpeedPixelsPerSecond,
            SmoothCameraSlowMaxSpeedPixelsPerSecond,
            multiplier);
        _smoothCameraY = SmoothDamp(
            _smoothCameraY,
            targetY,
            ref _smoothCameraVelocityY,
            smoothTimeSeconds,
            maxSpeedPixelsPerSecond,
            deltaSeconds);
        if (!float.IsFinite(_smoothCameraY) || !float.IsFinite(_smoothCameraVelocityY))
        {
            ResetSmoothCameraState(rawTargetY);
        }
        else
        {
            _smoothCameraPixelY = QuantizeSmoothCameraPixelY(_smoothCameraY, rawTargetY, multiplier);
        }

        return new Vector2(cameraTopLeft.X, _smoothCameraPixelY);
    }

    private void ResetSmoothCameraState()
    {
        _hasSmoothCameraY = false;
        _smoothCameraVelocityY = 0f;
        _hasSmoothCameraPreviousTargetY = false;
        _smoothCameraTargetVelocityY = 0f;
    }

    private void ResetSmoothCameraState(float y)
    {
        _smoothCameraY = y;
        _smoothCameraPixelY = RoundToSourcePixel(y);
        _smoothCameraVelocityY = 0f;
        _smoothCameraPreviousTargetY = y;
        _smoothCameraTargetVelocityY = 0f;
        _hasSmoothCameraPreviousTargetY = true;
        _hasSmoothCameraY = true;
    }

    private float GetSmoothCameraDeltaSeconds()
    {
        return Math.Clamp(_clientUpdateElapsedSeconds, 1f / 240f, 1f / 20f);
    }

    private void UpdateSmoothCameraTargetVelocity(float targetY, float deltaSeconds)
    {
        if (!_hasSmoothCameraPreviousTargetY || deltaSeconds <= 0f)
        {
            _smoothCameraPreviousTargetY = targetY;
            _smoothCameraTargetVelocityY = 0f;
            _hasSmoothCameraPreviousTargetY = true;
            return;
        }

        var measuredVelocityY = (targetY - _smoothCameraPreviousTargetY) / deltaSeconds;
        _smoothCameraPreviousTargetY = targetY;
        if (!float.IsFinite(measuredVelocityY))
        {
            measuredVelocityY = 0f;
        }

        var velocityBlend = 1f - MathF.Exp(-deltaSeconds / SmoothCameraTargetVelocityBlendTimeSeconds);
        _smoothCameraTargetVelocityY = MathHelper.Lerp(
            _smoothCameraTargetVelocityY,
            measuredVelocityY,
            Math.Clamp(velocityBlend, 0f, 1f));
    }

    private float GetSmoothCameraTargetY(float rawTargetY, float multiplier)
    {
        var verticalWindowPixels = MathHelper.Lerp(
            SmoothCameraMinVerticalWindowPixels,
            SmoothCameraMaxVerticalWindowPixels,
            multiplier);
        var displacementY = rawTargetY - _smoothCameraY;
        var windowedTargetY = MathF.Abs(displacementY) <= verticalWindowPixels
            ? _smoothCameraY
            : rawTargetY - (MathF.Sign(displacementY) * verticalWindowPixels);

        var targetVelocityY = MathF.Abs(_smoothCameraTargetVelocityY) < SmoothCameraVelocityDeadZonePixelsPerSecond
            ? 0f
            : _smoothCameraTargetVelocityY;
        var lookAheadSeconds = MathHelper.Lerp(
            SmoothCameraMinLookAheadSeconds,
            SmoothCameraMaxLookAheadSeconds,
            multiplier);
        var lookAheadY = Math.Clamp(
            targetVelocityY * lookAheadSeconds,
            -SmoothCameraMaxLookAheadPixels,
            SmoothCameraMaxLookAheadPixels);

        return windowedTargetY + lookAheadY;
    }

    private float QuantizeSmoothCameraPixelY(float smoothY, float rawTargetY, float multiplier)
    {
        var desiredPixelY = RoundToSourcePixel(smoothY);
        if (!float.IsFinite(_smoothCameraPixelY)
            || MathF.Abs(rawTargetY - _smoothCameraPixelY) >= SmoothCameraPixelHysteresisCatchUpPixels)
        {
            return desiredPixelY;
        }

        var deltaFromCurrentPixel = smoothY - _smoothCameraPixelY;
        var hysteresisPixels = MathHelper.Lerp(
            SmoothCameraMinPixelHysteresisPixels,
            SmoothCameraMaxPixelHysteresisPixels,
            multiplier);
        if (MathF.Abs(deltaFromCurrentPixel) < 0.5f + hysteresisPixels)
        {
            return _smoothCameraPixelY;
        }

        return desiredPixelY;
    }

    private static float SmoothDamp(
        float current,
        float target,
        ref float currentVelocity,
        float smoothTime,
        float maxSpeed,
        float deltaTime)
    {
        smoothTime = MathF.Max(0.0001f, smoothTime);
        deltaTime = MathF.Max(0f, deltaTime);
        var omega = 2f / smoothTime;
        var x = omega * deltaTime;
        var exponential = 1f / (1f + x + (0.48f * x * x) + (0.235f * x * x * x));
        var change = current - target;
        var originalTarget = target;
        var maxChange = MathF.Max(0f, maxSpeed) * smoothTime;
        change = Math.Clamp(change, -maxChange, maxChange);
        target = current - change;
        var temp = (currentVelocity + (omega * change)) * deltaTime;
        currentVelocity = (currentVelocity - (omega * temp)) * exponential;
        var output = target + ((change + temp) * exponential);

        if ((originalTarget - current > 0f) == (output > originalTarget))
        {
            output = originalTarget;
            currentVelocity = deltaTime > 0f
                ? (output - originalTarget) / deltaTime
                : 0f;
        }

        return output;
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
