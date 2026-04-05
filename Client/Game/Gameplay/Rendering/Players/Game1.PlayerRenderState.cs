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
        var isHumiliated = _world.IsPlayerHumiliated(player);
        var animationImage = renderState.BodyAnimationImage;

        var appearsAirborne = !GetPlayerRenderIsGrounded(player);
        if (appearsAirborne && isHumiliated)
        {
            appearsAirborne = verticalSourceStepSpeed > 0.35f;
        }

        if (!isHumiliated && isRemoteNetworkPlayer && !player.IsGrounded)
        {
            appearsAirborne = verticalSourceStepSpeed > 0.35f;
        }

        // Small stair snaps can briefly clear grounded state without being a real jump/fall.
        if (appearsAirborne
            && horizontalSourceStepSpeed >= 0.2f
            && verticalSourceStepSpeed <= 0.35f)
        {
            appearsAirborne = false;
        }

        if (!appearsAirborne && horizontalSourceStepSpeed < 0.2f)
        {
            animationImage = 0f;
        }
        else if (appearsAirborne)
        {
            animationImage = isHumiliated ? 2f : 1f;
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

        var weaponRenderDefinition = GetWeaponRenderDefinition(player);
        var weaponStats = GetRenderWeaponStats(player);
        var maxAmmoCount = GetRenderWeaponMaxShells(player);
        var currentAmmoCount = GetRenderWeaponAmmoCount(player);
        var currentCooldownTicks = GetRenderWeaponCooldownTicks(player);
        var currentReloadTicks = GetRenderWeaponReloadTicks(player);
        var reloadRestarted = currentReloadTicks > renderState.PreviousReloadTicks;
        var cooldownRestarted = currentCooldownTicks > renderState.PreviousCooldownTicks;
        var shotStarted = currentCooldownTicks > 0
            && (currentAmmoCount < renderState.PreviousAmmoCount
                || renderState.PreviousCooldownTicks <= 0
                || cooldownRestarted);
        var ammoIncreased = currentAmmoCount > renderState.PreviousAmmoCount;
        var shellReloaded = ammoIncreased && currentAmmoCount < maxAmmoCount;
        var preserveRecoilLoop = weaponRenderDefinition.LoopRecoilWhileActive
            && renderState.WeaponAnimationMode == WeaponAnimationMode.Recoil;
        var useScopedRecoilSprite = player.ClassId == PlayerClass.Sniper
            && !ShouldPresentExperimentalSoldierShotgun(player)
            && weaponRenderDefinition.ReloadSpriteName is not null
            && player.IsSniperScoped;

        if (player.ClassId == PlayerClass.Sniper)
        {
            UpdateSniperWeaponAnimationState(player, renderState, weaponRenderDefinition, shotStarted);
            QueueWeaponShellVisuals(player, shotStarted, ammoIncreased);
            renderState.PreviousAmmoCount = currentAmmoCount;
            renderState.PreviousCooldownTicks = currentCooldownTicks;
            renderState.PreviousReloadTicks = currentReloadTicks;
            return;
        }

        if (shotStarted)
        {
            if (useScopedRecoilSprite)
            {
                StartWeaponAnimation(renderState, WeaponAnimationMode.ScopedRecoil, weaponRenderDefinition.ScopedRecoilDurationSeconds);
            }
            else if (weaponRenderDefinition.RecoilSpriteName is not null)
            {
                StartWeaponAnimation(renderState, WeaponAnimationMode.Recoil, weaponRenderDefinition.RecoilDurationSeconds, preserveElapsed: preserveRecoilLoop);
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
                    if (ShouldShowReloadAnimation(player, weaponStats, weaponRenderDefinition, currentAmmoCount, maxAmmoCount, currentReloadTicks))
                    {
                        StartWeaponAnimation(renderState, WeaponAnimationMode.Reload, weaponRenderDefinition.ReloadDurationSeconds);
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
                    if (!ShouldShowReloadAnimation(player, weaponStats, weaponRenderDefinition, currentAmmoCount, maxAmmoCount, currentReloadTicks))
                    {
                        StopWeaponAnimation(renderState);
                    }
                    else if (shellReloaded || reloadRestarted)
                    {
                        StartWeaponAnimation(renderState, WeaponAnimationMode.Reload, weaponRenderDefinition.ReloadDurationSeconds);
                    }
                    break;
                case WeaponAnimationMode.Idle:
                    if (ShouldShowReloadAnimation(player, weaponStats, weaponRenderDefinition, currentAmmoCount, maxAmmoCount, currentReloadTicks))
                    {
                        StartWeaponAnimation(renderState, WeaponAnimationMode.Reload, weaponRenderDefinition.ReloadDurationSeconds);
                    }
                    break;
            }
        }

        QueueWeaponShellVisuals(player, shotStarted, ammoIncreased);
        renderState.PreviousAmmoCount = currentAmmoCount;
        renderState.PreviousCooldownTicks = currentCooldownTicks;
        renderState.PreviousReloadTicks = currentReloadTicks;
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
            PreviousAmmoCount = GetRenderWeaponAmmoCount(player),
            PreviousCooldownTicks = GetRenderWeaponCooldownTicks(player),
            PreviousReloadTicks = GetRenderWeaponReloadTicks(player),
        };
        _playerRenderStates[playerStateKey] = renderState;
        return renderState;
    }

    private int GetRenderWeaponAmmoCount(PlayerEntity player)
    {
        if (player.IsExperimentalDemoknightEnabled)
        {
            return 1;
        }

        if (ShouldPresentAcquiredWeapon(player))
        {
            return player.AcquiredWeaponCurrentShells;
        }

        if (ShouldPresentExperimentalSoldierShotgun(player))
        {
            return player.ExperimentalOffhandCurrentShells;
        }

        return _networkClient.IsConnected
            && ReferenceEquals(player, _world.LocalPlayer)
            && _hasPredictedLocalActionState
                ? _predictedLocalActionState.CurrentShells
                : player.CurrentShells;
    }

    private int GetRenderWeaponCooldownTicks(PlayerEntity player)
    {
        if (ShouldPresentAcquiredWeapon(player))
        {
            return player.AcquiredWeaponCooldownTicks;
        }

        if (ShouldPresentExperimentalSoldierShotgun(player))
        {
            return player.ExperimentalOffhandCooldownTicks;
        }

        if (_networkClient.IsConnected
            && ReferenceEquals(player, _world.LocalPlayer)
            && _hasPredictedLocalActionState)
        {
            return player.ClassId == PlayerClass.Medic
                ? _predictedLocalActionState.MedicNeedleCooldownTicks
                : _predictedLocalActionState.PrimaryCooldownTicks;
        }

        return player.ClassId == PlayerClass.Medic
            ? player.MedicNeedleCooldownTicks
            : player.PrimaryCooldownTicks;
    }

    private int GetRenderWeaponReloadTicks(PlayerEntity player)
    {
        if (player.IsExperimentalDemoknightEnabled)
        {
            return 0;
        }

        if (ShouldPresentAcquiredWeapon(player))
        {
            return player.AcquiredWeaponReloadTicksUntilNextShell;
        }

        if (ShouldPresentExperimentalSoldierShotgun(player))
        {
            return player.ExperimentalOffhandReloadTicksUntilNextShell;
        }

        if (_networkClient.IsConnected
            && ReferenceEquals(player, _world.LocalPlayer)
            && _hasPredictedLocalActionState)
        {
            return player.ClassId == PlayerClass.Medic
                ? _predictedLocalActionState.MedicNeedleRefillTicks
                : _predictedLocalActionState.ReloadTicksUntilNextShell;
        }

        return player.ClassId == PlayerClass.Medic
            ? player.MedicNeedleRefillTicks
            : player.ReloadTicksUntilNextShell;
    }

    private static int GetRenderWeaponMaxShells(PlayerEntity player)
    {
        if (player.IsExperimentalDemoknightEnabled)
        {
            return 1;
        }

        if (ShouldPresentAcquiredWeapon(player))
        {
            return player.AcquiredWeaponMaxShells;
        }

        return ShouldPresentExperimentalSoldierShotgun(player)
            ? player.ExperimentalOffhandMaxShells
            : player.MaxShells;
    }

    private static PrimaryWeaponDefinition GetRenderWeaponStats(PlayerEntity player)
    {
        if (player.IsExperimentalDemoknightEnabled)
        {
            return CharacterClassCatalog.ExperimentalDemoknightEyelander;
        }

        if (ShouldPresentAcquiredWeapon(player))
        {
            return player.AcquiredWeapon ?? player.PrimaryWeapon;
        }

        return ShouldPresentExperimentalSoldierShotgun(player)
            ? player.ExperimentalOffhandWeapon ?? CharacterClassCatalog.Shotgun
            : player.PrimaryWeapon;
    }

    private static PlayerClass GetRenderWeaponPresentationClassId(PlayerEntity player)
    {
        if (ShouldPresentAcquiredWeapon(player))
        {
            return player.AcquiredWeaponClassId ?? player.ClassId;
        }

        return ShouldPresentExperimentalSoldierShotgun(player)
            ? PlayerClass.Engineer
            : player.ClassId;
    }

    private static bool ShouldPresentAcquiredWeapon(PlayerEntity player)
    {
        return player.IsAcquiredWeaponPresented;
    }

    private static bool ShouldPresentExperimentalSoldierShotgun(PlayerEntity player)
    {
        return player.ClassId == PlayerClass.Soldier
            && player.IsExperimentalOffhandPresented;
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

    private float GetPlayerBodyAnimationLength(PlayerEntity player)
    {
        return player.ClassId == PlayerClass.Quote
            || (player.ClassId == PlayerClass.Sniper && player.IsSniperScoped)
            || _world.IsPlayerHumiliated(player)
                ? 2f
                : 4f;
    }

    private void QueueWeaponShellVisuals(PlayerEntity player, bool shotStarted, bool shellInserted)
    {
        if (_particleMode != 0)
        {
            return;
        }

        var shellClassId = GetRenderWeaponPresentationClassId(player);
        switch (shellClassId)
        {
            case PlayerClass.Heavy when shotStarted:
                QueueWeaponShellVisual(player, delaySeconds: 0f, count: 1, shellClassId);
                break;
            case PlayerClass.Engineer when shotStarted:
                QueueWeaponShellVisual(player, delaySeconds: GetSourceTicksAsSeconds(10f), count: 1, shellClassId);
                break;
            case PlayerClass.Scout when shellInserted:
                QueueWeaponShellVisual(player, delaySeconds: 0f, count: 1, shellClassId);
                break;
            case PlayerClass.Sniper when shotStarted:
                QueueWeaponShellVisual(player, delaySeconds: GetSourceTicksAsSeconds(player.IsSniperScoped ? 20f : 10f), count: 1, shellClassId);
                break;
            case PlayerClass.Medic when shotStarted:
                QueueResettingWeaponShellVisual(player, delaySeconds: GetSourceTicksAsSeconds(55f / 4f), count: 1);
                break;
            case PlayerClass.Spy when shotStarted:
                QueueWeaponShellVisual(player, delaySeconds: GetSourceTicksAsSeconds((18f + 45f) * 3f / 5f), count: 1, shellClassId);
                break;
        }
    }

    private void QueueResettingWeaponShellVisual(PlayerEntity player, float delaySeconds, int count)
    {
        if (_particleMode != 0 || count <= 0)
        {
            return;
        }

        var playerStateKey = GetPlayerStateKey(player);
        for (var pendingIndex = _pendingWeaponShellVisuals.Count - 1; pendingIndex >= 0; pendingIndex -= 1)
        {
            var pendingShell = _pendingWeaponShellVisuals[pendingIndex];
            if (pendingShell.PlayerId == playerStateKey
                && pendingShell.ClassId == player.ClassId)
            {
                _pendingWeaponShellVisuals.RemoveAt(pendingIndex);
            }
        }

        QueueWeaponShellVisual(player, delaySeconds, count);
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

    private static bool ShouldShowReloadAnimation(
        PlayerEntity player,
        PrimaryWeaponDefinition weaponStats,
        WeaponRenderDefinition weaponDefinition,
        int currentAmmoCount,
        int maxAmmoCount,
        int currentReloadTicks)
    {
        if (weaponDefinition.ReloadSpriteName is null)
        {
            return false;
        }

        if (!weaponStats.AutoReloads && !weaponStats.RefillsAllAtOnce)
        {
            return player.ClassId == PlayerClass.Medic
                && weaponStats.Kind == PrimaryWeaponKind.Medigun
                && currentAmmoCount < maxAmmoCount
                && currentReloadTicks > 0;
        }

        return currentAmmoCount < maxAmmoCount
            && currentReloadTicks > 0;
    }
}
