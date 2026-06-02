#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool IsRespawnFreeCameraActive()
    {
        return ShouldBlockGameplayForNavEditor()
            || IsLocalSpectatorPresentationActive()
            || (!_world.LocalPlayerAwaitingJoin
                && !_world.LocalPlayer.IsAlive
                && _world.LocalDeathCam is null);
    }

    private void UpdateRespawnCameraState(float deltaSeconds, KeyboardState keyboard, MouseState mouse)
    {
        if (!IsRespawnFreeCameraActive())
        {
            _respawnCameraDetached = false;
            _respawnCameraCenter = GetDefaultFreeCameraCenter();
            return;
        }

        if (IsLocalSpectatorPresentationActive())
        {
            UpdateSpectatorCameraState(deltaSeconds, keyboard, mouse);
            return;
        }

        if (!_respawnCameraDetached)
        {
            _respawnCameraCenter = GetDefaultFreeCameraCenter();
        }

        if (ShouldBlockGameplayForNavEditor() || !IsGameplayInputBlocked())
        {
            var moveAmount = 600f * deltaSeconds;
            var moved = false;

            if (IsBindingDown(keyboard, mouse, _inputBindings.MoveLeft))
            {
                _respawnCameraCenter.X -= moveAmount;
                moved = true;
            }
            else if (IsBindingDown(keyboard, mouse, _inputBindings.MoveRight))
            {
                _respawnCameraCenter.X += moveAmount;
                moved = true;
            }

            if (IsBindingDown(keyboard, mouse, _inputBindings.MoveUp))
            {
                _respawnCameraCenter.Y -= moveAmount;
                moved = true;
            }
            else if (IsBindingDown(keyboard, mouse, _inputBindings.MoveDown))
            {
                _respawnCameraCenter.Y += moveAmount;
                moved = true;
            }

            if (moved)
            {
                if (IsLocalSpectatorPresentationActive() && _spectatorTrackingEnabled)
                {
                    _spectatorTrackingEnabled = false;
                    _spectatorTrackedPlayerId = null;
                    ShowNotice(NoticeKind.PlayerTrackDisable);
                }

                _respawnCameraDetached = true;
            }
        }

        _respawnCameraCenter = ClampRespawnCameraCenter(_respawnCameraCenter);
    }

    private void UpdateSpectatorCameraState(float deltaSeconds, KeyboardState keyboard, MouseState mouse)
    {
        if (!_respawnCameraDetached && _respawnCameraCenter == Vector2.Zero)
        {
            _respawnCameraCenter = GetDefaultFreeCameraCenter();
        }

        if (TryApplySpectatorManualCameraMovement(deltaSeconds, keyboard, mouse))
        {
            _respawnCameraCenter = ClampRespawnCameraCenter(_respawnCameraCenter);
            return;
        }

        if (_spectatorCameraMode == SpectatorCameraMode.Normal
            && _spectatorTrackingEnabled
            && GetSpectatorFocusPlayer() is { } trackedPlayer
            && trackedPlayer.IsAlive)
        {
            if (IsSpectatorHiddenSpy(trackedPlayer))
            {
                _spectatorTrackingEnabled = false;
                _spectatorTrackedPlayerId = null;
            }
            else
            {
                _respawnCameraCenter = SmoothSpectatorCameraToward(
                    _respawnCameraCenter,
                    GetSpectatorTrackedPlayerTarget(trackedPlayer),
                    0.1f,
                    deltaSeconds);
                _respawnCameraCenter = ClampSpectatorTrackingCameraCenter(_respawnCameraCenter);
                return;
            }
        }

        if (_spectatorCameraMode == SpectatorCameraMode.RedIntel || _spectatorCameraMode == SpectatorCameraMode.BlueIntel)
        {
            UpdateSpectatorIntelCamera(deltaSeconds);
        }
        else if (_spectatorCameraMode == SpectatorCameraMode.Auto)
        {
            UpdateSpectatorAutoCamera(deltaSeconds);
        }
        else if (!_respawnCameraDetached)
        {
            _respawnCameraCenter = SmoothSpectatorCameraToward(
                _respawnCameraCenter,
                GetDefaultFreeCameraCenter(),
                0.05f,
                deltaSeconds);
        }

        _respawnCameraCenter = ClampRespawnCameraCenter(_respawnCameraCenter);
    }

    private bool TryApplySpectatorManualCameraMovement(float deltaSeconds, KeyboardState keyboard, MouseState mouse)
    {
        if (ShouldBlockGameplayForNavEditor() || !IsGameplayInputBlocked())
        {
            var moveAmount = 600f * deltaSeconds;
            var moved = false;

            if (IsBindingDown(keyboard, mouse, _inputBindings.MoveLeft))
            {
                _respawnCameraCenter.X -= moveAmount;
                moved = true;
            }
            else if (IsBindingDown(keyboard, mouse, _inputBindings.MoveRight))
            {
                _respawnCameraCenter.X += moveAmount;
                moved = true;
            }

            if (IsBindingDown(keyboard, mouse, _inputBindings.MoveUp))
            {
                _respawnCameraCenter.Y -= moveAmount;
                moved = true;
            }
            else if (IsBindingDown(keyboard, mouse, _inputBindings.MoveDown))
            {
                _respawnCameraCenter.Y += moveAmount;
                moved = true;
            }

            if (!moved)
            {
                return false;
            }

            if (_spectatorTrackingEnabled)
            {
                _spectatorTrackingEnabled = false;
                _spectatorTrackedPlayerId = null;
                ShowNotice(NoticeKind.PlayerTrackDisable);
            }

            _spectatorCameraMode = SpectatorCameraMode.Normal;
            _respawnCameraDetached = true;
            return true;
        }

        return false;
    }

    private void UpdateSpectatorIntelCamera(float deltaSeconds)
    {
        var targetIntel = _spectatorCameraMode == SpectatorCameraMode.RedIntel
            ? _world.RedIntel
            : _world.BlueIntel;
        var carrierTeam = targetIntel.Team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
        foreach (var player in EnumerateRemotePlayersForView())
        {
            if (player.IsAlive
                && player.Team == carrierTeam
                && player.IsCarryingIntel
                && !IsSpectatorHiddenSpy(player))
            {
                _spectatorTrackedPlayerId = player.Id;
                _spectatorTrackingEnabled = true;
                _respawnCameraCenter = SmoothSpectatorCameraToward(
                    _respawnCameraCenter,
                    GetSpectatorTrackedPlayerTarget(player),
                    0.1f,
                    deltaSeconds);
                return;
            }
        }

        _spectatorTrackingEnabled = false;
        _spectatorTrackedPlayerId = null;
        _respawnCameraCenter = SmoothSpectatorCameraToward(
            _respawnCameraCenter,
            GetRenderIntelPosition(targetIntel),
            0.05f,
            deltaSeconds);
    }

    private void UpdateSpectatorAutoCamera(float deltaSeconds)
    {
        var expandedLeft = _respawnCameraCenter.X - (ViewportWidth / 2f) - 120f;
        var expandedRight = _respawnCameraCenter.X + (ViewportWidth / 2f) + 120f;
        var gameplayViewportHeight = GetGameplayCameraViewportHeight(ViewportHeight);
        var expandedTop = _respawnCameraCenter.Y - (gameplayViewportHeight / 2f) - 120f;
        var expandedBottom = _respawnCameraCenter.Y + (gameplayViewportHeight / 2f) + 120f;
        var weightedTarget = Vector2.Zero;
        var weightTotal = 0f;

        foreach (var player in EnumerateRemotePlayersForView())
        {
            if (!player.IsAlive
                || IsSpectatorHiddenSpy(player)
                || player.X < expandedLeft
                || player.X > expandedRight
                || player.Y < expandedTop
                || player.Y > expandedBottom)
            {
                continue;
            }

            var dangerWeight = GetSpectatorAutoDanger(player) * 5f;
            var weight = MathF.Ceiling(1f
                + (player.IsCarryingIntel ? 8f : 0f)
                + (MathF.Abs(GetSpectatorSourceTickVelocity(player).X) * 0.5f)
                + dangerWeight);
            AddSpectatorAutoTarget(ref weightedTarget, ref weightTotal, GetSpectatorAutoPlayerTarget(player), weight);
        }

        foreach (var sentry in _world.Sentries)
        {
            if (!sentry.IsBuilt || !IsInSpectatorAutoRange(sentry.X, sentry.Y, 120f))
            {
                continue;
            }

            AddSpectatorAutoTarget(ref weightedTarget, ref weightTotal, new Vector2(sentry.X, sentry.Y), 0.5f);
        }

        foreach (var rocket in _world.Rockets)
        {
            if (!IsInSpectatorAutoRange(rocket.X, rocket.Y, 120f))
            {
                continue;
            }

            AddSpectatorAutoTarget(ref weightedTarget, ref weightTotal, new Vector2(rocket.X, rocket.Y), 0.5f);
        }

        foreach (var flare in _world.Flares)
        {
            if (!IsInSpectatorAutoRange(flare.X, flare.Y, 120f))
            {
                continue;
            }

            AddSpectatorAutoTarget(ref weightedTarget, ref weightTotal, new Vector2(flare.X, flare.Y), 0.5f);
        }

        foreach (var shot in _world.Shots)
        {
            if (!IsInSpectatorAutoRange(shot.X, shot.Y, 120f))
            {
                continue;
            }

            AddSpectatorAutoTarget(ref weightedTarget, ref weightTotal, new Vector2(shot.X, shot.Y), 0.1f);
        }

        if (weightTotal > 0f)
        {
            var target = weightedTarget / weightTotal;
            var delta = target - _respawnCameraCenter;
            var tickScale = GetSpectatorTickScale(deltaSeconds);
            if (MathF.Abs(delta.X) > 32f)
            {
                _respawnCameraCenter.X += Math.Clamp(delta.X * 0.02f, -8f, 8f) * tickScale;
            }

            if (MathF.Abs(delta.Y) > 32f)
            {
                _respawnCameraCenter.Y += Math.Clamp(delta.Y * 0.01f, -8f, 8f) * tickScale;
            }

            return;
        }

        if (TryFindNearestSpectatorPlayer(_respawnCameraCenter, out var nearestPlayer))
        {
            var target = GetRenderPosition(nearestPlayer);
            var tickScale = GetSpectatorTickScale(deltaSeconds);
            _respawnCameraCenter.X += Math.Clamp((target.X - _respawnCameraCenter.X) * 0.05f, -32f, 32f) * tickScale;
            _respawnCameraCenter.Y += Math.Clamp((target.Y - _respawnCameraCenter.Y) * 0.02f, -8f, 8f) * tickScale;
        }
    }

    private Vector2 GetSpectatorTrackedPlayerTarget(PlayerEntity player)
    {
        var position = GetRenderPosition(player);
        var target = position + new Vector2(player.HorizontalSpeed, player.VerticalSpeed) * 0.066f;
        if (GetPlayerIsSniperScoped(player) || GetPlayerIsUsingBinoculars(player))
        {
            var aim = GetRenderAimWorldPosition(player);
            target += (aim - position) * 0.5f;
        }

        return target;
    }

    private Vector2 GetSpectatorAutoPlayerTarget(PlayerEntity player)
    {
        var position = GetRenderPosition(player);
        var sourceVelocity = GetSpectatorSourceTickVelocity(player);
        var aimDirection = GetSpectatorAimDirection(player);
        return position + (sourceVelocity * 12f) + (aimDirection * 20f);
    }

    private static Vector2 GetSpectatorSourceTickVelocity(PlayerEntity player)
    {
        return new Vector2(player.HorizontalSpeed, player.VerticalSpeed) / LegacyMovementModel.SourceTicksPerSecond;
    }

    private Vector2 GetSpectatorAimDirection(PlayerEntity player)
    {
        var position = GetRenderPosition(player);
        var aim = GetRenderAimWorldPosition(player) - position;
        if (aim.LengthSquared() <= 0.0001f)
        {
            var radians = MathF.PI * player.AimDirectionDegrees / 180f;
            return new Vector2(MathF.Cos(radians), MathF.Sin(radians));
        }

        aim.Normalize();
        return aim;
    }

    private float GetSpectatorAutoDanger(PlayerEntity player)
    {
        var danger = 0f;
        foreach (var candidate in EnumerateRemotePlayersForView())
        {
            if (!candidate.IsAlive
                || candidate.Team == player.Team
                || IsSpectatorHiddenSpy(candidate))
            {
                continue;
            }

            var dx = candidate.X - player.X;
            var dy = candidate.Y - player.Y;
            var distance = MathF.Sqrt((dx * dx) + (dy * dy));
            var dangerAdd = 500f - distance;
            if (dangerAdd > 0f)
            {
                danger += dangerAdd / 500f;
            }
        }

        return danger;
    }

    private bool IsInSpectatorAutoRange(float x, float y, float range)
    {
        var gameplayViewportHeight = GetGameplayCameraViewportHeight(ViewportHeight);
        var viewX = _respawnCameraCenter.X - (ViewportWidth / 2f);
        var viewY = _respawnCameraCenter.Y - (gameplayViewportHeight / 2f);
        return x > viewX - range
            && y > viewY - range
            && x < viewX + ViewportWidth + range
            && y < viewY + gameplayViewportHeight + range;
    }

    private bool TryFindNearestSpectatorPlayer(Vector2 center, out PlayerEntity player)
    {
        player = null!;
        var nearestDistanceSquared = float.MaxValue;
        foreach (var candidate in EnumerateRemotePlayersForView())
        {
            if (!candidate.IsAlive || IsSpectatorHiddenSpy(candidate))
            {
                continue;
            }

            var position = GetRenderPosition(candidate);
            var distanceSquared = Vector2.DistanceSquared(position, center);
            if (distanceSquared >= nearestDistanceSquared)
            {
                continue;
            }

            nearestDistanceSquared = distanceSquared;
            player = candidate;
        }

        return nearestDistanceSquared < float.MaxValue;
    }

    private static void AddSpectatorAutoTarget(ref Vector2 weightedTarget, ref float weightTotal, Vector2 target, float weight)
    {
        if (weight <= 0f)
        {
            return;
        }

        weightedTarget += target * weight;
        weightTotal += weight;
    }

    private static bool IsSpectatorHiddenSpy(PlayerEntity player)
    {
        return player.ClassId == PlayerClass.Spy
            && player.IsSpyCloaked
            && !player.IsSpyBackstabAnimating;
    }

    private static Vector2 SmoothSpectatorCameraToward(Vector2 current, Vector2 target, float perSourceTickFactor, float deltaSeconds)
    {
        return Vector2.Lerp(current, target, GetSpectatorLerpFactor(perSourceTickFactor, deltaSeconds));
    }

    private Vector2 ClampSpectatorTrackingCameraCenter(Vector2 position)
    {
        return new Vector2(
            System.Math.Clamp(position.X, 0f, System.Math.Max(0f, _world.Bounds.Width)),
            System.Math.Clamp(position.Y, 0f, System.Math.Max(0f, _world.Bounds.Height)));
    }

    private Vector2 ClampRespawnCameraCenter(Vector2 position)
    {
        var halfViewportWidth = ViewportWidth / 2f;
        var halfViewportHeight = GetGameplayCameraViewportHeight(ViewportHeight) / 2f;
        var maxX = System.Math.Max(halfViewportWidth, _world.Bounds.Width - halfViewportWidth);
        var maxY = System.Math.Max(halfViewportHeight, _world.Bounds.Height - halfViewportHeight);
        return new Vector2(
            System.Math.Clamp(position.X, halfViewportWidth, maxX),
            System.Math.Clamp(position.Y, halfViewportHeight, maxY));
    }
}
