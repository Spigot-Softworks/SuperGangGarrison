#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const float SmoothCameraSnapDeltaPixels = 160f;
    private const float SmoothCameraMinSmoothTimeSeconds = 0.045f;
    private const float SmoothCameraMaxSmoothTimeSeconds = 0.135f;
    private const float SmoothCameraFastMaxSpeedPixelsPerSecond = 16000f;
    private const float SmoothCameraSlowMaxSpeedPixelsPerSecond = 7200f;
    private const float SmoothCameraMinVerticalWindowPixels = 0.15f;
    private const float SmoothCameraMaxVerticalWindowPixels = 2.25f;
    private Vector2 _smoothCameraVelocity;

    private Vector2 GetCameraTopLeft(int viewportWidth, int viewportHeight, int mouseX, int mouseY)
    {
        var cameraTopLeft = CalculateBaseCameraTopLeft(viewportWidth, viewportHeight, mouseX, mouseY, trackLiveCamera: false);
        cameraTopLeft = ApplySmoothCamera(cameraTopLeft);
        cameraTopLeft = RoundToSourcePixels(cameraTopLeft + GetClientPluginCameraOffset() + GetLastToDieCameraShakeOffset());
        TrackLiveCamera(cameraTopLeft);
        _gameplayCameraTopLeft = cameraTopLeft;
        _hasGameplayCameraTopLeft = true;
        return cameraTopLeft;
    }

    private Vector2 GetGameplayInputCameraTopLeft(int viewportWidth, int viewportHeight, int mouseX, int mouseY)
    {
        if (_hasGameplayCameraTopLeft)
        {
            return _gameplayCameraTopLeft;
        }

        return GetUntrackedCameraTopLeft(viewportWidth, viewportHeight, mouseX, mouseY);
    }

    private Vector2 GetUntrackedCameraTopLeft(int viewportWidth, int viewportHeight, int mouseX, int mouseY)
    {
        var cameraTopLeft = CalculateBaseCameraTopLeft(viewportWidth, viewportHeight, mouseX, mouseY, trackLiveCamera: false);
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

        var localViewPosition = GetLocalViewPosition();
        if (_world.LocalPlayer.IsAlive && GetPlayerIsUsingBinoculars(_world.LocalPlayer))
        {
            cameraTopLeft = GetBinocularsCameraTopLeft(_world.LocalPlayer, viewportWidth, viewportHeight);
        }
        else if (_world.LocalPlayer.IsAlive && GetPlayerIsSniperScoped(_world.LocalPlayer))
        {
            if (IsControllerGameplayInputActive() && _hasLatestLocalAimWorldPosition)
            {
                var aimWorldPosition = new Vector2(_latestLocalAimWorldX, _latestLocalAimWorldY);
                var scopedCameraCenter = (localViewPosition + aimWorldPosition) / 2f;
                cameraTopLeft = new Vector2(
                    scopedCameraCenter.X - (viewportWidth / 2f),
                    scopedCameraCenter.Y - (viewportHeight / 2f));
            }
            else
            {
                cameraTopLeft = new Vector2(
                    localViewPosition.X + mouseX - viewportWidth,
                    localViewPosition.Y + mouseY - viewportHeight);
            }
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
        if (IsLocalSpectatorPresentationActive())
        {
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
        if (IsLocalSpectatorPresentationActive())
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
        // For the local player, always use the locally-maintained focus position — it is updated
        // every frame and is independent of the prediction system. Remote/spectated players use
        // the server-synced entity state.
        float binocularsFocusX, binocularsFocusY;
        if (ReferenceEquals(player, _world.LocalPlayer))
        {
            binocularsFocusX = _binocularsFocusX;
            binocularsFocusY = _binocularsFocusY;
        }
        else
        {
            binocularsFocusX = player.BinocularsFocusX;
            binocularsFocusY = player.BinocularsFocusY;
        }
        
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

    private Vector2 ApplySmoothCamera(Vector2 cameraTopLeft)
    {
        var multiplier = NormalizeSmoothCameraMultiplier(_smoothCameraMultiplier);
        if (multiplier <= 0f || !ShouldSmoothCamera())
        {
            ResetSmoothCameraState();
            return cameraTopLeft;
        }

        if (!_hasSmoothCamera
            || !IsFinite(_smoothCamera)
            || Vector2.Distance(cameraTopLeft, _smoothCamera) > SmoothCameraSnapDeltaPixels)
        {
            ResetSmoothCameraState(cameraTopLeft);
            return _smoothCameraPixel;
        }

        var deltaSeconds = GetSmoothCameraDeltaSeconds();
        var target = GetSmoothCameraTarget(cameraTopLeft, multiplier);
        var smoothTimeSeconds = MathHelper.Lerp(
            SmoothCameraMinSmoothTimeSeconds,
            SmoothCameraMaxSmoothTimeSeconds,
            multiplier);
        var maxSpeedPixelsPerSecond = MathHelper.Lerp(
            SmoothCameraFastMaxSpeedPixelsPerSecond,
            SmoothCameraSlowMaxSpeedPixelsPerSecond,
            multiplier);

        var velocityX = _smoothCameraVelocity.X;
        var velocityY = _smoothCameraVelocity.Y;
        var smoothX = SmoothDamp(
            _smoothCamera.X,
            target.X,
            ref velocityX,
            smoothTimeSeconds,
            maxSpeedPixelsPerSecond,
            deltaSeconds);
        var smoothY = SmoothDamp(
            _smoothCamera.Y,
            target.Y,
            ref velocityY,
            smoothTimeSeconds,
            maxSpeedPixelsPerSecond,
            deltaSeconds);
        _smoothCamera = new Vector2(smoothX, smoothY);
        _smoothCameraVelocity = new Vector2(velocityX, velocityY);

        if (!IsFinite(_smoothCamera) || !IsFinite(_smoothCameraVelocity))
        {
            ResetSmoothCameraState(cameraTopLeft);
        }
        else
        {
            _smoothCameraPixel = RoundToSourcePixels(_smoothCamera);
        }

        return _smoothCameraPixel;
    }

    private void ResetSmoothCameraState()
    {
        _hasSmoothCamera = false;
        _smoothCameraVelocity = Vector2.Zero;
        _hasGameplayCameraTopLeft = false;
    }

    private void ResetSmoothCameraState(Vector2 position)
    {
        _smoothCamera = position;
        _smoothCameraPixel = RoundToSourcePixels(position);
        _smoothCameraVelocity = Vector2.Zero;
        _hasSmoothCamera = true;
    }

    private float GetSmoothCameraDeltaSeconds()
    {
        return Math.Clamp(_clientUpdateElapsedSeconds, 1f / 240f, 1f / 20f);
    }

    private Vector2 GetSmoothCameraTarget(Vector2 rawTarget, float multiplier)
    {
        var windowPixels = MathHelper.Lerp(
            SmoothCameraMinVerticalWindowPixels,
            SmoothCameraMaxVerticalWindowPixels,
            multiplier);

        return new Vector2(
            GetWindowedSmoothCameraAxisTarget(rawTarget.X, _smoothCamera.X, windowPixels),
            GetWindowedSmoothCameraAxisTarget(rawTarget.Y, _smoothCamera.Y, windowPixels));
    }

    private static float GetWindowedSmoothCameraAxisTarget(float rawTarget, float current, float windowPixels)
    {
        var displacement = rawTarget - current;
        return MathF.Abs(displacement) <= windowPixels
            ? current
            : rawTarget - (MathF.Sign(displacement) * windowPixels);
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
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

    private bool ShouldSmoothCamera()
    {
        if (IsDeathCamPresentationActive() || IsRespawnFreeCameraActive())
        {
            return false;
        }

        if (IsLocalSpectatorPresentationActive())
        {
            var spectatorFocus = GetSpectatorFocusPlayer();
            return spectatorFocus is null
                || (!GetPlayerIsUsingBinoculars(spectatorFocus) && !GetPlayerIsSniperScoped(spectatorFocus));
        }

        return !_world.LocalPlayer.IsAlive
            || (!GetPlayerIsUsingBinoculars(_world.LocalPlayer) && !GetPlayerIsSniperScoped(_world.LocalPlayer));
    }
}
