#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private enum WeaponAnimationMode
    {
        Idle,
        Recoil,
        ScopedRecoil,
        Reload,
    }

    private sealed class PlayerRenderState
    {
        public float BodyAnimationImage { get; set; }

        public float RenderHorizontalSpeed { get; set; }

        public bool AppearsAirborne { get; set; }

        public WeaponAnimationMode WeaponAnimationMode { get; set; }

        public float WeaponAnimationDurationSeconds { get; set; }

        public float WeaponAnimationTimeRemainingSeconds { get; set; }

        public float WeaponAnimationElapsedSeconds { get; set; }

        public int PreviousAmmoCount { get; set; }

        public int PreviousCooldownTicks { get; set; }

        public int PreviousReloadTicks { get; set; }
    }

    private readonly HashSet<int> _activePlayerRenderStateIds = new();
    private readonly List<int> _stalePlayerRenderStateIds = new();

    private void UpdatePlayerRenderState(PlayerEntity player)
    {
        var playerStateKey = GetPlayerStateKey(player);
        if (!player.IsAlive)
        {
            _playerRenderStates.Remove(playerStateKey);
            _playerPreviousRenderPositions.Remove(playerStateKey);
            _playerPreviousRenderSampleTimes.Remove(playerStateKey);
            return;
        }

        var renderState = GetOrCreatePlayerRenderState(playerStateKey, player);
        var observedRenderVelocity = SampleObservedRenderVelocity(player);
        var renderHorizontalSpeed = GetPlayerRenderHorizontalSpeed(player, observedRenderVelocity);
        var renderVerticalSpeed = GetPlayerRenderVerticalSpeed(player, observedRenderVelocity);
        var horizontalSourceStepSpeed = GetPlayerAnimationSourceStepSpeed(renderHorizontalSpeed);
        var verticalSourceStepSpeed = GetPlayerAnimationSourceStepSpeed(renderVerticalSpeed);
        var animationElapsedSeconds = GetPlayerAnimationElapsedSeconds();
        var isRemoteNetworkPlayer = _networkClient.IsConnected && !ReferenceEquals(player, _world.LocalPlayer);
        var animationImage = renderState.BodyAnimationImage;

        var appearsAirborne = !GetPlayerRenderIsGrounded(player);

        if (isRemoteNetworkPlayer && !player.IsGrounded)
        {
            appearsAirborne = verticalSourceStepSpeed > 0.35f;
        }

        if (!appearsAirborne && horizontalSourceStepSpeed < 0.2f)
        {
            animationImage = 0f;
        }
        else if (appearsAirborne)
        {
            animationImage = 1f;
        }
        else
        {
            animationImage = WrapAnimationImage(
                animationImage + GetPlayerAnimationAdvance(renderHorizontalSpeed, animationElapsedSeconds, GetPlayerFacingScale(player)),
                GetPlayerBodyAnimationLength(player));
        }

        renderState.BodyAnimationImage = animationImage;
        renderState.RenderHorizontalSpeed = renderHorizontalSpeed;
        renderState.AppearsAirborne = appearsAirborne;
        UpdatePlayerWeaponAnimationState(player, renderState, animationElapsedSeconds);
    }

    private void UpdatePlayerWeaponAnimationState(PlayerEntity player, PlayerRenderState renderState, float elapsedSeconds)
    {
        if (renderState.WeaponAnimationMode != WeaponAnimationMode.Idle && elapsedSeconds > 0f)
        {
            renderState.WeaponAnimationElapsedSeconds += elapsedSeconds;
        }

        if (renderState.WeaponAnimationTimeRemainingSeconds > 0f)
        {
            renderState.WeaponAnimationTimeRemainingSeconds = MathF.Max(0f, renderState.WeaponAnimationTimeRemainingSeconds - elapsedSeconds);
        }

        var weaponDefinition = GetWeaponRenderDefinition(player);
        var cooldownRestarted = player.PrimaryCooldownTicks > renderState.PreviousCooldownTicks;
        var reloadRestarted = player.ReloadTicksUntilNextShell > renderState.PreviousReloadTicks;
        var shotStarted = player.PrimaryCooldownTicks > 0
            && (player.CurrentShells < renderState.PreviousAmmoCount
                || renderState.PreviousCooldownTicks <= 0
                || cooldownRestarted);
        var ammoIncreased = player.CurrentShells > renderState.PreviousAmmoCount;
        var shellReloaded = ammoIncreased && player.CurrentShells < player.MaxShells;
        var preserveRecoilLoop = weaponDefinition.LoopRecoilWhileActive
            && renderState.WeaponAnimationMode == WeaponAnimationMode.Recoil;
        var useScopedRecoilSprite = player.ClassId == PlayerClass.Sniper
            && weaponDefinition.ReloadSpriteName is not null
            && player.IsSniperScoped;

        if (player.ClassId == PlayerClass.Sniper)
        {
            UpdateSniperWeaponAnimationState(player, renderState, weaponDefinition, shotStarted);
            QueueWeaponShellVisuals(player, shotStarted, ammoIncreased);
            renderState.PreviousAmmoCount = player.CurrentShells;
            renderState.PreviousCooldownTicks = player.PrimaryCooldownTicks;
            renderState.PreviousReloadTicks = player.ReloadTicksUntilNextShell;
            return;
        }

        if (player.ClassId == PlayerClass.Medic && player.IsMedicHealing && weaponDefinition.RecoilSpriteName is not null)
        {
            StartWeaponAnimation(renderState, WeaponAnimationMode.Recoil, weaponDefinition.RecoilDurationSeconds, preserveElapsed: preserveRecoilLoop);
        }
        else if (shotStarted)
        {
            if (useScopedRecoilSprite)
            {
                StartWeaponAnimation(renderState, WeaponAnimationMode.ScopedRecoil, weaponDefinition.ScopedRecoilDurationSeconds);
            }
            else if (weaponDefinition.RecoilSpriteName is not null)
            {
                StartWeaponAnimation(renderState, WeaponAnimationMode.Recoil, weaponDefinition.RecoilDurationSeconds, preserveElapsed: preserveRecoilLoop);
            }
            else
            {
                StopWeaponAnimation(renderState);
            }
        }
        else
        {
            switch (renderState.WeaponAnimationMode)
            {
                case WeaponAnimationMode.Recoil when renderState.WeaponAnimationTimeRemainingSeconds <= 0f:
                    if (ShouldShowReloadAnimation(player, weaponDefinition))
                    {
                        StartWeaponAnimation(renderState, WeaponAnimationMode.Reload, weaponDefinition.ReloadDurationSeconds);
                    }
                    else
                    {
                        StopWeaponAnimation(renderState);
                    }
                    break;
                case WeaponAnimationMode.ScopedRecoil when renderState.WeaponAnimationTimeRemainingSeconds <= 0f:
                    StopWeaponAnimation(renderState);
                    break;
                case WeaponAnimationMode.Reload:
                    if (!ShouldShowReloadAnimation(player, weaponDefinition))
                    {
                        StopWeaponAnimation(renderState);
                    }
                    else if (shellReloaded || reloadRestarted || renderState.WeaponAnimationTimeRemainingSeconds <= 0f)
                    {
                        StartWeaponAnimation(renderState, WeaponAnimationMode.Reload, weaponDefinition.ReloadDurationSeconds);
                    }
                    break;
                case WeaponAnimationMode.Idle:
                    if (player.PrimaryCooldownTicks > 0)
                    {
                        if (useScopedRecoilSprite)
                        {
                            StartWeaponAnimation(renderState, WeaponAnimationMode.ScopedRecoil, weaponDefinition.ScopedRecoilDurationSeconds);
                        }
                        else if (weaponDefinition.RecoilSpriteName is not null)
                        {
                            StartWeaponAnimation(renderState, WeaponAnimationMode.Recoil, weaponDefinition.RecoilDurationSeconds);
                        }
                    }
                    else if (ShouldShowReloadAnimation(player, weaponDefinition))
                    {
                        StartWeaponAnimation(renderState, WeaponAnimationMode.Reload, weaponDefinition.ReloadDurationSeconds);
                    }
                    break;
            }
        }

        QueueWeaponShellVisuals(player, shotStarted, ammoIncreased);
        renderState.PreviousAmmoCount = player.CurrentShells;
        renderState.PreviousCooldownTicks = player.PrimaryCooldownTicks;
        renderState.PreviousReloadTicks = player.ReloadTicksUntilNextShell;
    }

    private static void UpdateSniperWeaponAnimationState(
        PlayerEntity player,
        PlayerRenderState renderState,
        WeaponRenderDefinition weaponDefinition,
        bool shotStarted)
    {
        if (shotStarted)
        {
            if (player.IsSniperScoped && weaponDefinition.ReloadSpriteName is not null)
            {
                StartWeaponAnimation(renderState, WeaponAnimationMode.ScopedRecoil, weaponDefinition.ScopedRecoilDurationSeconds);
            }
            else if (weaponDefinition.RecoilSpriteName is not null)
            {
                StartWeaponAnimation(renderState, WeaponAnimationMode.Recoil, weaponDefinition.RecoilDurationSeconds);
            }
            else
            {
                StopWeaponAnimation(renderState);
            }

            return;
        }

        if (renderState.WeaponAnimationMode == WeaponAnimationMode.Recoil
            && renderState.WeaponAnimationTimeRemainingSeconds <= 0f)
        {
            StopWeaponAnimation(renderState);
            return;
        }

        if (renderState.WeaponAnimationMode == WeaponAnimationMode.ScopedRecoil
            && renderState.WeaponAnimationTimeRemainingSeconds <= 0f)
        {
            StopWeaponAnimation(renderState);
            return;
        }

        if (renderState.WeaponAnimationMode == WeaponAnimationMode.Reload)
        {
            StopWeaponAnimation(renderState);
        }
    }

    private void RemoveStalePlayerRenderState()
    {
        _activePlayerRenderStateIds.Clear();
        if (_world.LocalPlayer.IsAlive)
        {
            _activePlayerRenderStateIds.Add(GetPlayerStateKey(_world.LocalPlayer));
        }

        foreach (var player in EnumerateRemotePlayersForView())
        {
            if (player.IsAlive)
            {
                _activePlayerRenderStateIds.Add(GetPlayerStateKey(player));
            }
        }

        _stalePlayerRenderStateIds.Clear();
        foreach (var playerId in _playerRenderStates.Keys)
        {
            if (!_activePlayerRenderStateIds.Contains(playerId))
            {
                _stalePlayerRenderStateIds.Add(playerId);
            }
        }

        foreach (var playerId in _stalePlayerRenderStateIds)
        {
            _playerRenderStates.Remove(playerId);
            _playerPreviousRenderPositions.Remove(playerId);
            _playerPreviousRenderSampleTimes.Remove(playerId);
        }
    }

    private float GetPlayerAnimationElapsedSeconds()
    {
        return MathF.Max(0f, _clientUpdateElapsedSeconds);
    }

    private static float GetPlayerAnimationSourceStepSpeed(float speedPerSecond)
    {
        return MathF.Abs(speedPerSecond) / LegacyMovementModel.SourceTicksPerSecond;
    }

    private static float GetPlayerAnimationAdvance(float speedPerSecond, float elapsedSeconds, float facingScale)
    {
        if (elapsedSeconds <= 0f)
        {
            return 0f;
        }

        var clampedSpeedPerSecond = MathF.Min(MathF.Abs(speedPerSecond), 8f * LegacyMovementModel.SourceTicksPerSecond);
        return clampedSpeedPerSecond * elapsedSeconds / 20f * MathF.Sign(speedPerSecond) * facingScale;
    }

    private Vector2 SampleObservedRenderVelocity(PlayerEntity player)
    {
        var playerStateKey = GetPlayerStateKey(player);
        var currentPosition = GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _world.LocalPlayer));
        var currentTimeSeconds = _networkInterpolationClockSeconds;
        if (!_playerPreviousRenderPositions.TryGetValue(playerStateKey, out var previousPosition)
            || !_playerPreviousRenderSampleTimes.TryGetValue(playerStateKey, out var previousTimeSeconds))
        {
            _playerPreviousRenderPositions[playerStateKey] = currentPosition;
            _playerPreviousRenderSampleTimes[playerStateKey] = currentTimeSeconds;
            return Vector2.Zero;
        }

        _playerPreviousRenderPositions[playerStateKey] = currentPosition;
        _playerPreviousRenderSampleTimes[playerStateKey] = currentTimeSeconds;

        var elapsedSeconds = currentTimeSeconds - previousTimeSeconds;
        if (elapsedSeconds <= 0.0001d)
        {
            return Vector2.Zero;
        }

        return (currentPosition - previousPosition) / (float)elapsedSeconds;
    }

    private float GetPlayerRenderHorizontalSpeed(PlayerEntity player, Vector2 observedRenderVelocity)
    {
        if (_networkClient.IsConnected && ReferenceEquals(player, _world.LocalPlayer))
        {
            if (_hasPredictedLocalPlayerPosition)
            {
                return MathF.Abs(observedRenderVelocity.X) > MathF.Abs(_predictedLocalPlayerVelocity.X)
                    ? observedRenderVelocity.X
                    : _predictedLocalPlayerVelocity.X;
            }

            return observedRenderVelocity.X;
        }

        return player.HorizontalSpeed;
    }

    private float GetPlayerRenderVerticalSpeed(PlayerEntity player, Vector2 observedRenderVelocity)
    {
        if (_networkClient.IsConnected && ReferenceEquals(player, _world.LocalPlayer))
        {
            if (_hasPredictedLocalPlayerPosition)
            {
                return MathF.Abs(observedRenderVelocity.Y) > MathF.Abs(_predictedLocalPlayerVelocity.Y)
                    ? observedRenderVelocity.Y
                    : _predictedLocalPlayerVelocity.Y;
            }

            return observedRenderVelocity.Y;
        }

        return player.VerticalSpeed;
    }

    private bool GetPlayerRenderIsGrounded(PlayerEntity player)
    {
        return _networkClient.IsConnected
            && ReferenceEquals(player, _world.LocalPlayer)
            && _hasPredictedLocalPlayerPosition
                ? _predictedLocalPlayerGrounded
                : player.IsGrounded;
    }

    private PlayerRenderState GetOrCreatePlayerRenderState(int playerStateKey, PlayerEntity player)
    {
        if (_playerRenderStates.TryGetValue(playerStateKey, out var renderState))
        {
            return renderState;
        }

        renderState = new PlayerRenderState
        {
            PreviousAmmoCount = player.CurrentShells,
            PreviousCooldownTicks = player.PrimaryCooldownTicks,
            PreviousReloadTicks = player.ReloadTicksUntilNextShell,
        };
        _playerRenderStates[playerStateKey] = renderState;
        return renderState;
    }

    private static float WrapAnimationImage(float animationImage, float length)
    {
        if (length <= 0f)
        {
            return 0f;
        }

        animationImage %= length;
        if (animationImage < 0f)
        {
            animationImage += length;
        }

        return animationImage;
    }

    private static float GetPlayerBodyAnimationLength(PlayerEntity player)
    {
        return player.ClassId == PlayerClass.Quote || player.IsSniperScoped ? 2f : 4f;
    }

    private void QueueWeaponShellVisuals(PlayerEntity player, bool shotStarted, bool shellInserted)
    {
        if (_particleMode != 0)
        {
            return;
        }

        switch (player.ClassId)
        {
            case PlayerClass.Heavy when shotStarted:
                QueueWeaponShellVisual(player, delaySeconds: 0f, count: 1);
                break;
            case PlayerClass.Engineer when shotStarted:
                QueueWeaponShellVisual(player, delaySeconds: GetSourceTicksAsSeconds(10f), count: 1);
                break;
            case PlayerClass.Scout when shellInserted:
                QueueWeaponShellVisual(player, delaySeconds: 0f, count: 1);
                break;
            case PlayerClass.Sniper when shotStarted:
                QueueWeaponShellVisual(player, delaySeconds: GetSourceTicksAsSeconds(player.IsSniperScoped ? 20f : 10f), count: 1);
                break;
            case PlayerClass.Medic when shotStarted:
                QueueWeaponShellVisual(player, delaySeconds: GetSourceTicksAsSeconds(55f / 4f), count: 1);
                break;
            case PlayerClass.Spy when shotStarted:
                QueueWeaponShellVisual(player, delaySeconds: GetSourceTicksAsSeconds((18f + 45f) * 3f / 5f), count: 1);
                break;
        }
    }

    private static void StartWeaponAnimation(PlayerRenderState renderState, WeaponAnimationMode mode, float durationSeconds, bool preserveElapsed = false)
    {
        var resetElapsed = !preserveElapsed || renderState.WeaponAnimationMode != mode;
        renderState.WeaponAnimationMode = mode;
        renderState.WeaponAnimationDurationSeconds = MathF.Max(durationSeconds, 0f);
        renderState.WeaponAnimationTimeRemainingSeconds = MathF.Max(durationSeconds, 0f);
        if (resetElapsed)
        {
            renderState.WeaponAnimationElapsedSeconds = 0f;
        }
    }

    private static void StopWeaponAnimation(PlayerRenderState renderState)
    {
        renderState.WeaponAnimationMode = WeaponAnimationMode.Idle;
        renderState.WeaponAnimationDurationSeconds = 0f;
        renderState.WeaponAnimationTimeRemainingSeconds = 0f;
        renderState.WeaponAnimationElapsedSeconds = 0f;
    }

    private static bool ShouldShowReloadAnimation(PlayerEntity player, WeaponRenderDefinition weaponDefinition)
    {
        if (weaponDefinition.ReloadSpriteName is null)
        {
            return false;
        }

        if (!player.PrimaryWeapon.AutoReloads && !player.PrimaryWeapon.RefillsAllAtOnce)
        {
            return false;
        }

        return player.CurrentShells < player.MaxShells
            && player.ReloadTicksUntilNextShell > 0;
    }
}
