#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const float SmoothCameraSnapDeltaPixels = 160f;
    private const float SmoothCameraFastCatchUpRate = 28f;
    private const float SmoothCameraSlowCatchUpRate = 10f;
    private const float SmoothCameraFastMaxStepPixelsPerSecond = 16000f;
    private const float SmoothCameraSlowMaxStepPixelsPerSecond = 7200f;
    private const float SmoothCameraMinLookaheadSeconds = 0.035f;
    private const float SmoothCameraMaxLookaheadSeconds = 0.12f;
    private const float SmoothCameraMaxHorizontalLookaheadPixels = 36f;
    private const float SmoothCameraMaxVerticalLookaheadPixels = 18f;
    private const float SmoothCameraMinVerticalWindowPixels = 0.15f;
    private const float SmoothCameraMaxVerticalWindowPixels = 2.25f;
    private bool _smoothCameraRenderingActive;
    private double _lastSmoothCameraUpdateClockSeconds = -1d;
    private Vector2 _lastSmoothCameraRawTarget;
    private bool _hasLastSmoothCameraRawTarget;
    private Vector2 _smoothCameraLookaheadOffset;

    private Vector2 GetCameraTopLeft(int viewportWidth, int viewportHeight, int mouseX, int mouseY)
    {
        var cameraTopLeft = CalculateBaseCameraTopLeft(viewportWidth, viewportHeight, mouseX, mouseY, trackLiveCamera: false);
        cameraTopLeft = ApplySmoothCamera(cameraTopLeft);
        cameraTopLeft += GetClientPluginCameraOffset() + GetLastToDieCameraShakeOffset();
        if (!_smoothCameraRenderingActive)
        {
            cameraTopLeft = RoundToSourcePixels(cameraTopLeft);
        }

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
            if (TryGetPredictedLocalPlayerCameraPosition(out var predictedCameraPosition))
            {
                return predictedCameraPosition;
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
            _smoothCameraRenderingActive = true;
            return _smoothCamera;
        }

        var deltaSeconds = GetSmoothCameraDeltaSeconds();
        var rawTargetDelta = _hasLastSmoothCameraRawTarget
            ? cameraTopLeft - _lastSmoothCameraRawTarget
            : Vector2.Zero;
        if (_hasLastSmoothCameraRawTarget)
        {
            _smoothCamera += rawTargetDelta;
        }

        _lastSmoothCameraRawTarget = cameraTopLeft;
        _hasLastSmoothCameraRawTarget = true;

        if (deltaSeconds > 0f)
        {
            var rawTargetVelocity = rawTargetDelta / deltaSeconds;
            var lookaheadTarget = GetSmoothCameraLookaheadOffset(rawTargetVelocity, multiplier);
            var catchUpRate = MathHelper.Lerp(
                SmoothCameraFastCatchUpRate,
                SmoothCameraSlowCatchUpRate,
                multiplier);
            var maxStepPixelsPerSecond = MathHelper.Lerp(
                SmoothCameraFastMaxStepPixelsPerSecond,
                SmoothCameraSlowMaxStepPixelsPerSecond,
                multiplier);

            _smoothCameraLookaheadOffset = AdvanceSmoothCameraNoOvershoot(
                _smoothCameraLookaheadOffset,
                lookaheadTarget,
                catchUpRate,
                maxStepPixelsPerSecond,
                deltaSeconds);

            var target = GetSmoothCameraTarget(cameraTopLeft + _smoothCameraLookaheadOffset, multiplier);
            _smoothCamera = AdvanceSmoothCameraNoOvershoot(
                _smoothCamera,
                target,
                catchUpRate,
                maxStepPixelsPerSecond,
                deltaSeconds);
        }

        if (!IsFinite(_smoothCamera))
        {
            ResetSmoothCameraState(cameraTopLeft);
        }
        else
        {
            _smoothCameraPixel = RoundToSourcePixels(_smoothCamera);
        }

        _smoothCameraRenderingActive = true;
        return _smoothCamera;
    }

    private void ResetSmoothCameraState()
    {
        _hasSmoothCamera = false;
        _lastSmoothCameraUpdateClockSeconds = -1d;
        _hasLastSmoothCameraRawTarget = false;
        _smoothCameraLookaheadOffset = Vector2.Zero;
        _hasGameplayCameraTopLeft = false;
        _smoothCameraRenderingActive = false;
    }

    private void ResetSmoothCameraState(Vector2 position)
    {
        _smoothCamera = position;
        _smoothCameraPixel = RoundToSourcePixels(position);
        _lastSmoothCameraUpdateClockSeconds = -1d;
        _lastSmoothCameraRawTarget = position;
        _hasLastSmoothCameraRawTarget = true;
        _smoothCameraLookaheadOffset = Vector2.Zero;
        _hasSmoothCamera = true;
    }

    private float GetSmoothCameraDeltaSeconds()
    {
        if (_lastSmoothCameraUpdateClockSeconds == _networkInterpolationClockSeconds)
        {
            return 0f;
        }

        _lastSmoothCameraUpdateClockSeconds = _networkInterpolationClockSeconds;
        return Math.Clamp(_gameplayPresentationDeltaSeconds, 0f, 1f / 20f);
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

    private static Vector2 GetSmoothCameraLookaheadOffset(Vector2 rawTargetVelocity, float multiplier)
    {
        var lookaheadSeconds = MathHelper.Lerp(
            SmoothCameraMinLookaheadSeconds,
            SmoothCameraMaxLookaheadSeconds,
            multiplier);
        return new Vector2(
            Math.Clamp(
                rawTargetVelocity.X * lookaheadSeconds,
                -SmoothCameraMaxHorizontalLookaheadPixels,
                SmoothCameraMaxHorizontalLookaheadPixels),
            Math.Clamp(
                rawTargetVelocity.Y * lookaheadSeconds,
                -SmoothCameraMaxVerticalLookaheadPixels,
                SmoothCameraMaxVerticalLookaheadPixels));
    }

    private static Vector2 AdvanceSmoothCameraNoOvershoot(
        Vector2 current,
        Vector2 target,
        float catchUpRate,
        float maxStepPixelsPerSecond,
        float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return current;
        }

        return new Vector2(
            AdvanceSmoothCameraAxisNoOvershoot(current.X, target.X, catchUpRate, maxStepPixelsPerSecond, deltaSeconds),
            AdvanceSmoothCameraAxisNoOvershoot(current.Y, target.Y, catchUpRate, maxStepPixelsPerSecond, deltaSeconds));
    }

    private static float AdvanceSmoothCameraAxisNoOvershoot(
        float current,
        float target,
        float catchUpRate,
        float maxStepPixelsPerSecond,
        float deltaSeconds)
    {
        var remaining = target - current;
        if (MathF.Abs(remaining) <= 0.001f)
        {
            return target;
        }

        var catchUpFactor = 1f - MathF.Exp(-MathF.Max(0f, catchUpRate) * deltaSeconds);
        var step = remaining * catchUpFactor;
        var maxStep = MathF.Max(0f, maxStepPixelsPerSecond) * deltaSeconds;
        if (maxStep > 0f && MathF.Abs(step) > maxStep)
        {
            step = MathF.Sign(step) * maxStep;
        }

        if (MathF.Abs(step) >= MathF.Abs(remaining))
        {
            return target;
        }

        return current + step;
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
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
