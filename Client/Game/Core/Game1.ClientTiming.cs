#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int ClientUpdateTicksPerSecond = 60;
    private const double ClientUpdateStepSeconds = 1d / ClientUpdateTicksPerSecond;
    private double _clientTickAccumulatorSeconds;
    private double _networkInputAccumulatorSeconds;
    private float _clientUpdateElapsedSeconds;
    private float _gameplayPresentationDeltaSeconds;
    private double _lastGameplayPresentationClockSeconds = -1d;
    private int _lastGameplayPresentationClientTicks;
    private float _goreSourceTickAccumulator;
    private bool _pendingPredictedJumpPress;
    private bool _pendingPredictedPrimaryPress;
    private bool _pendingPredictedSecondaryAbilityPress;
    private bool _pendingPredictedAbilityPress;
    private bool _pendingPredictedSwapWeaponPress;
    private uint _latchedJumpPressSequence;

    private bool _hasLatestLocalAimWorldPosition;
    private float _latestLocalAimWorldX;
    private float _latestLocalAimWorldY;
    private bool _hasLatestNetworkInputAimOrigin;
    private float _latestNetworkInputAimOriginX;
    private float _latestNetworkInputAimOriginY;
    private int _writeBubbleTick;

    private int ConsumeClientTickCount(GameTime gameTime)
    {
        _clientUpdateElapsedSeconds = (float)Math.Clamp(gameTime.ElapsedGameTime.TotalSeconds, 0d, 0.1d);
        _clientTickAccumulatorSeconds += _clientUpdateElapsedSeconds;

        var ticks = 0;
        var maxCatchUpTicks = OperatingSystem.IsBrowser() ? 2 : 8;
        while (_clientTickAccumulatorSeconds >= ClientUpdateStepSeconds && ticks < maxCatchUpTicks)
        {
            _clientTickAccumulatorSeconds -= ClientUpdateStepSeconds;
            ticks += 1;
        }

        return ticks;
    }

    private void ResetClientTimingState()
    {
        _clientTickAccumulatorSeconds = 0d;
        _networkInputAccumulatorSeconds = 0d;
        _gameplayPresentationDeltaSeconds = 0f;
        _lastGameplayPresentationClockSeconds = -1d;
        _lastGameplayPresentationClientTicks = 0;
        _goreSourceTickAccumulator = 0f;
        ClearPendingPredictedInputEdges();
        _latchedJumpPressSequence = 0;
        _hasLatestLocalAimWorldPosition = false;
        _latestLocalAimWorldX = 0f;
        _latestLocalAimWorldY = 0f;
        _hasLatestNetworkInputAimOrigin = false;
        _latestNetworkInputAimOriginX = 0f;
        _latestNetworkInputAimOriginY = 0f;
        _writeBubbleTick = 0;
    }

    private void ClearPendingPredictedInputEdges()
    {
        _pendingPredictedJumpPress = false;
        _pendingPredictedPrimaryPress = false;
        _pendingPredictedSecondaryAbilityPress = false;
        _pendingPredictedAbilityPress = false;
        _pendingPredictedSwapWeaponPress = false;
    }

    private void CapturePendingPredictedInputEdges(KeyboardState keyboard, MouseState mouse, PlayerInputSnapshot networkInput)
    {
        _previousPredictedLocalInput = _latestPredictedLocalInput;
        _latestPredictedLocalInput = networkInput;
        var previousPredictedInput = _previousPredictedLocalInput;

        if (!_networkClient.IsConnected)
        {
            ClearPendingPredictedInputEdges();
            return;
        }

        if (!networkInput.Up
            && !networkInput.FirePrimary
            && !networkInput.FireSecondary
            && !networkInput.UseAbility
            && !networkInput.SwapWeapon)
        {
            return;
        }

        var jumpPressed = networkInput.Up
            && ((!previousPredictedInput.Up)
                || InputBindingInput.IsPressed(_inputBindings.MoveUp, keyboard, _previousKeyboard, mouse, _previousMouse)
                || (keyboard.IsKeyDown(Keys.Up) && !_previousKeyboard.IsKeyDown(Keys.Up)));
        if (jumpPressed)
        {
            _pendingPredictedJumpPress = true;
        }

        var primaryPressed = networkInput.FirePrimary
            && (mouse.LeftButton == ButtonState.Pressed
                && _previousMouse.LeftButton != ButtonState.Pressed
                || !previousPredictedInput.FirePrimary);
        if (primaryPressed)
        {
            _pendingPredictedPrimaryPress = true;
        }

        var secondaryAbilityPressed = networkInput.FireSecondary
            && (mouse.RightButton == ButtonState.Pressed
                && _previousMouse.RightButton != ButtonState.Pressed
                || _latestAimUsesController
                && !previousPredictedInput.FireSecondary);
        if (secondaryAbilityPressed)
        {
            _pendingPredictedSecondaryAbilityPress = true;
        }

        var abilityPressed = networkInput.UseAbility
            && (InputBindingInput.IsPressed(_inputBindings.UseAbility, keyboard, _previousKeyboard, mouse, _previousMouse)
                || _latestAimUsesController
                && !previousPredictedInput.UseAbility);
        if (abilityPressed)
        {
            _pendingPredictedAbilityPress = true;
        }

        var swapWeaponPressed = networkInput.SwapWeapon && !previousPredictedInput.SwapWeapon;
        if (swapWeaponPressed)
        {
            _pendingPredictedSwapWeaponPress = true;
        }
    }

    private PlayerInputSnapshot ApplyPendingInputEdges(PlayerInputSnapshot input)
    {
        if (!_networkClient.IsConnected)
        {
            return input;
        }

        if (_pendingPredictedJumpPress && !input.Up)
        {
            input = input with { Up = true };
        }

        if (_pendingPredictedSecondaryAbilityPress && !input.FireSecondary)
        {
            input = input with { FireSecondary = true };
        }

        if (_pendingPredictedPrimaryPress && !input.FirePrimary)
        {
            input = input with { FirePrimary = true };
        }

        if (_pendingPredictedSwapWeaponPress && !input.SwapWeapon)
        {
            input = input with { SwapWeapon = true };
        }

        if (_pendingPredictedAbilityPress && !input.UseAbility)
        {
            input = input with { UseAbility = true };
        }

        return input;
    }

    private void ClearPendingSecondaryAbilityPress()
    {
        _pendingPredictedSecondaryAbilityPress = false;
    }

    private void AdvanceNetworkInputLane(PlayerInputSnapshot networkInput)
    {
        _networkInputAccumulatorSeconds += _clientUpdateElapsedSeconds;
        while (_networkInputAccumulatorSeconds >= _config.FixedDeltaSeconds)
        {
            _networkClient.AdvanceNetworkInputTick();
            _networkInputAccumulatorSeconds -= _config.FixedDeltaSeconds;
            var outboundNetworkInput = ApplyPendingInputEdges(networkInput);
            if (_latchedJumpPressSequence != 0 && !outboundNetworkInput.Up)
            {
                // Keep jump held in the outbound stream until authority confirms it
                // processed one matching input, so brief tap timing can't lose the edge.
                outboundNetworkInput = outboundNetworkInput with { Up = true };
            }

            var aimOrigin = _hasLatestNetworkInputAimOrigin
                ? new Vector2(_latestNetworkInputAimOriginX, _latestNetworkInputAimOriginY)
                : GetLocalViewPosition();
            var sentInputSequence = _networkClient.SendInput(outboundNetworkInput, aimOrigin.X, aimOrigin.Y);
            if (_pendingPredictedJumpPress && sentInputSequence != 0)
            {
                _latchedJumpPressSequence = sentInputSequence;
            }

            RecordPredictedInput(
                sentInputSequence,
                outboundNetworkInput,
                _pendingPredictedJumpPress,
                _pendingPredictedPrimaryPress,
                _pendingPredictedSecondaryAbilityPress,
                _pendingPredictedAbilityPress,
                _pendingPredictedSwapWeaponPress);
            ClearPendingPredictedInputEdges();
        }
    }

    private void AcknowledgeLatchedPredictedInputs(uint lastProcessedInputSequence)
    {
        if (_latchedJumpPressSequence != 0
            && (lastProcessedInputSequence == _latchedJumpPressSequence
                || unchecked((int)(lastProcessedInputSequence - _latchedJumpPressSequence)) > 0))
        {
            _latchedJumpPressSequence = 0;
        }
    }

    private void AdvanceStartupSplashTicks(int ticks, KeyboardState keyboard, MouseState mouse)
    {
        for (var tick = 0; tick < ticks && _startupSplashOpen; tick += 1)
        {
            UpdateStartupSplash(keyboard, mouse);
        }
    }

    private void AdvanceMenuClientTicks(int ticks)
    {
        UpdateDevMessageState();
        for (var tick = 0; tick < ticks; tick += 1)
        {
            UpdatePendingHostedConnect();
            UpdateServerLauncherState();
        }
    }

    private void AdvanceGameplayClientTicks(int ticks)
    {
        for (var tick = 0; tick < ticks; tick += 1)
        {
            AdvancePredictedAfterburnVisuals();
            AdvanceClientSideSuperjumpCharging();
            AdvanceSpySuperjumpTrajectoryAnimation();
            AdvanceSpySuperjumpCloakReveal();
            EnsureRemoteSpyBackstabVisuals();
            AdvanceChatHud();
            AdvanceWriteBubbleTick();
            UpdateNoticeState();
            AdvanceExplosionVisuals();
            AdvanceImpactVisuals();
            AdvanceGoreSourceTicks();
            AdvanceExperimentalHealingHudIndicators();
            AdvanceShellVisuals();
            AdvanceRocketSmokeVisuals();
            AdvanceMineTrailVisuals();
            AdvanceFlameSmokeVisuals();
            AdvanceLooseSheetVisuals();
            AdvanceCivvieUmbrellaShieldBlockVisuals();
            AdvanceFrozenSpyVisuals();
            AdvanceHeavyDashTrailVisuals();
            AdvanceImmediateNetworkDeadBodies();
            AdvanceMedigunBeamHelixPhase();

            if (_autoBalanceNoticeTicks > 0)
            {
                _autoBalanceNoticeTicks = Math.Max(0, _autoBalanceNoticeTicks - 1);
                if (_autoBalanceNoticeTicks == 0)
                {
                    _autoBalanceNoticeText = string.Empty;
                }
            }

            AdvanceBackstabVisuals();
        }
    }

    private void AdvanceWriteBubbleTick()
    {
        _writeBubbleTick += 1;
    }

    private void AdvancePredictedAfterburnVisuals()
    {
        if (!_networkClient.IsConnected)
        {
            return;
        }

        AdvancePredictedAfterburnVisual(_world.LocalPlayer);
        for (var index = 0; index < _world.RemoteSnapshotPlayers.Count; index += 1)
        {
            AdvancePredictedAfterburnVisual(_world.RemoteSnapshotPlayers[index]);
        }
    }

    private static void AdvancePredictedAfterburnVisual(PlayerEntity player)
    {
        if (!player.IsAlive || !player.IsBurning)
        {
            return;
        }

        player.AdvanceAfterburnVisual((float)ClientUpdateStepSeconds);
    }

    private void AdvanceClientSideSuperjumpCharging()
    {
        // Only run client-side charging in online mode (client prediction)
        // In offline mode, the server-side simulation handles everything
        if (!_networkClient.IsConnected)
        {
            return;
        }

        var localPlayer = _world.LocalPlayer;
        if (localPlayer == null || !localPlayer.IsAlive || localPlayer.ClassId != PlayerClass.Spy)
        {
            return;
        }

        var input = _latestPredictedLocalInput;
        var previousInput = _previousPredictedLocalInput;
        localPlayer.ObserveSpySuperjumpAbilityInput(input.UseAbility);

        // Calculate aim direction
        var degrees = MathF.Atan2(input.AimWorldY - localPlayer.Y, input.AimWorldX - localPlayer.X) * (180f / MathF.PI);
        if (degrees < 0f)
        {
            degrees += 360f;
        }
        var aimDirectionDegrees = degrees;

        // Detect ability button edge (transition from off to on)
        var abilityPressed = input.UseAbility && !previousInput.UseAbility;
        var jumpPressed = input.Up && !previousInput.Up;

        // Don't allow spy superjump if special abilities are disabled.
        if (!_world.ExperimentalGameplaySettings.EnableSecondaryAbilities)
        {
            return;
        }

        if (jumpPressed && input.UseAbility && localPlayer.SpySuperjumpChargeTicks > 0)
        {
            localPlayer.CancelSpySuperjumpCharge(blockRestartUntilAbilityRelease: true);
            _predictedLocalActionState.SpySuperjumpChargeTicks = 0;
            _predictedLocalActionState.IsSpySuperjumping = false;
            _predictedLocalActionState.SpySuperjumpHorizontalVelocity = 0f;
            return;
        }

        // Start charging when UseAbility is first pressed (edge detection)
        // Don't start during backstab animation (TryStartSpySuperjumpCharge also checks this)
        if (abilityPressed && !localPlayer.IsSpyBackstabAnimating)
        {
            localPlayer.TryStartSpySuperjumpCharge(aimDirectionDegrees, input.Left, input.Right, input.Up, input.Down);
        }
        // Also start charging if UseAbility is being held and not already charging (handles holding space while landing)
        else if (input.UseAbility && localPlayer.SpySuperjumpChargeTicks == 0 && !localPlayer.IsSpySuperjumping && !localPlayer.IsSpyBackstabAnimating)
        {
            localPlayer.TryStartSpySuperjumpCharge(aimDirectionDegrees, input.Left, input.Right, input.Up, input.Down);
        }

        // Process charging state
        if (localPlayer.SpySuperjumpChargeTicks > 0)
        {
            // Cancel if NEW movement buttons are pressed (not ones held when charging started)
            var heldButtons = localPlayer.SpySuperjumpChargeStartMovementButtons;
            var leftWasHeld = (heldButtons & 0x01) != 0;
            var rightWasHeld = (heldButtons & 0x02) != 0;
            var upWasHeld = (heldButtons & 0x04) != 0;
            var downWasHeld = (heldButtons & 0x08) != 0;

            var newButtonPressed = (input.Left && !leftWasHeld)
                || (input.Right && !rightWasHeld)
                || (input.Up && !upWasHeld)
                || (input.Down && !downWasHeld);

            if (newButtonPressed)
            {
                localPlayer.CancelSpySuperjumpCharge();
            }
            // Cancel if backstab starts
            else if (localPlayer.IsSpyBackstabAnimating)
            {
                localPlayer.CancelSpySuperjumpCharge();
            }
            // Continue charging while UseAbility is held
            else if (input.UseAbility)
            {
                localPlayer.IncrementSpySuperjumpCharge(aimDirectionDegrees);
            }
            // Release when UseAbility is released (server will handle actual jump, only if grounded)
            else
            {
                localPlayer.CancelSpySuperjumpCharge();
            }
        }
    }

    private void AdvanceSpySuperjumpTrajectoryAnimation()
    {
        var localPlayer = _world.LocalPlayer;
        if (localPlayer != null && localPlayer.ClassId == PlayerClass.Spy && localPlayer.SpySuperjumpChargeTicks > 0 && !localPlayer.IsSpyBackstabAnimating && !localPlayer.IsCarryingIntel)
        {
            // Pre-populate 8 dots when charging first starts
            if (_spySuperjumpTrajectoryAnimationTicks == 0)
            {
                // Start at a tick value that makes 8 dots spawn and be spread along trajectory
                // With dotSpawnInterval=13.33 and dotSpeed=0.3, spacing dots ~13.33 ticks apart:
                // Last dot (index 7) spawns at 93.31, needs to travel 280 ticks, requiring dotAge=933
                // So animationProgress = 93.31 + 933 = 1026
                _spySuperjumpTrajectoryAnimationTicks = 1020;
            }
            else
            {
                _spySuperjumpTrajectoryAnimationTicks++;
            }
        }
        else
        {
            _spySuperjumpTrajectoryAnimationTicks = 0;
        }
    }

    private void AdvanceSpySuperjumpCloakReveal()
    {
        if (!_networkClient.IsConnected)
        {
            _spySuperjumpCloakRevealTicks.Clear();
            _prevSuperjumpingPlayerIds.Clear();
        }

        AdvanceLocalSpySuperjumpCloakReveal(_world.LocalPlayer);
        if (_networkClient.IsConnected)
        {
            for (var i = 0; i < _world.RemoteSnapshotPlayers.Count; i += 1)
            {
                AdvanceRemoteSpySuperjumpCloakReveal(_world.RemoteSnapshotPlayers[i]);
            }
        }

        if (!_networkClient.IsConnected)
        {
            return;
        }

        // Clean up entries for players that are no longer tracked
        var toRemove = new List<int>();
        foreach (var id in _spySuperjumpCloakRevealTicks.Keys)
        {
            var found = _world.LocalPlayer?.Id == id;
            if (!found)
            {
                for (var i = 0; i < _world.RemoteSnapshotPlayers.Count; i += 1)
                {
                    if (_world.RemoteSnapshotPlayers[i].Id == id)
                    {
                        found = true;
                        break;
                    }
                }
            }
            if (!found)
            {
                toRemove.Add(id);
            }
        }
        foreach (var id in toRemove)
        {
            _spySuperjumpCloakRevealTicks.Remove(id);
            _prevSuperjumpingPlayerIds.Remove(id);
        }
    }

    private void AdvanceLocalSpySuperjumpCloakReveal(PlayerEntity? player)
    {
        if (player is null || player.ClassId != PlayerClass.Spy)
        {
            _localSpySuperjumpCloakRevealAlpha = 0f;
            _wasLocalSpySuperjumping = false;
            return;
        }

        var isSuperjumping = GetPlayerIsSpySuperjumping(player);

        if (isSuperjumping && !_wasLocalSpySuperjumping && GetPlayerIsSpyCloaked(player))
        {
            _localSpySuperjumpCloakRevealAlpha = SpySuperjumpLocalCloakRevealStartAlpha;
        }

        _wasLocalSpySuperjumping = isSuperjumping;

        if (!GetPlayerIsSpyCloaked(player))
        {
            _localSpySuperjumpCloakRevealAlpha = 0f;
            return;
        }

        if (_localSpySuperjumpCloakRevealAlpha <= 0f)
        {
            return;
        }

        var baseCloakAlpha = Math.Clamp(GetPlayerSpyCloakAlpha(player), 0f, 1f);
        if (_localSpySuperjumpCloakRevealAlpha <= baseCloakAlpha)
        {
            _localSpySuperjumpCloakRevealAlpha = 0f;
            return;
        }

        _localSpySuperjumpCloakRevealAlpha = Math.Max(
            baseCloakAlpha,
            _localSpySuperjumpCloakRevealAlpha - PlayerEntity.SpyCloakFadePerTick);
    }

    private bool GetPlayerIsSpySuperjumping(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.IsSpySuperjumping
            : player.IsSpySuperjumping;
    }

    private void AdvanceRemoteSpySuperjumpCloakReveal(PlayerEntity? player)
    {
        if (player is null || player.ClassId != PlayerClass.Spy)
        {
            return;
        }

        var id = player.Id;
        var wasSuperjumping = _prevSuperjumpingPlayerIds.Contains(id);
        var isSuperjumping = player.IsSpySuperjumping;

        // Transition: just started superjumping while cloaked → start reveal
        if (isSuperjumping && !wasSuperjumping && player.IsSpyCloaked)
        {
            _spySuperjumpCloakRevealTicks[id] = SpySuperjumpCloakRevealTicks;
        }

        if (isSuperjumping)
        {
            _prevSuperjumpingPlayerIds.Add(id);
        }
        else
        {
            _prevSuperjumpingPlayerIds.Remove(id);
        }

        // If the spy uncloaked, the reveal is irrelevant — cancel it
        if (!player.IsSpyCloaked)
        {
            _spySuperjumpCloakRevealTicks.Remove(id);
            return;
        }

        if (_spySuperjumpCloakRevealTicks.TryGetValue(id, out var ticks))
        {
            if (ticks <= 0)
            {
                _spySuperjumpCloakRevealTicks.Remove(id);
            }
            else
            {
                _spySuperjumpCloakRevealTicks[id] = ticks - 1;
            }
        }
    }

    private void AdvanceGoreSourceTicks()
    {
        _goreSourceTickAccumulator += (float)(ClientUpdateStepSeconds * LegacyMovementModel.SourceTicksPerSecond);
        while (_goreSourceTickAccumulator >= 1f)
        {
            _goreSourceTickAccumulator -= 1f;
            AdvanceBloodVisuals();
            AdvanceSniperTracerParticles();
        }
    }

    private float GetLegacyUiStepCount()
    {
        return _clientUpdateElapsedSeconds <= 0f
            ? 0f
            : _clientUpdateElapsedSeconds * LegacyMovementModel.SourceTicksPerSecond;
    }

    private float AdvanceOpeningAlpha(float alpha, float minAlpha, float maxAlpha)
    {
        var stepCount = GetLegacyUiStepCount();
        if (stepCount <= 0f)
        {
            return alpha;
        }

        var exponent = MathF.Pow(0.7f, stepCount);
        return MathF.Min(maxAlpha, MathF.Pow(MathF.Max(alpha, minAlpha), exponent));
    }

    private float AdvanceClosingAlpha(float alpha, float minAlpha)
    {
        var stepCount = GetLegacyUiStepCount();
        if (stepCount <= 0f)
        {
            return alpha;
        }

        var exponent = MathF.Pow(0.7f, stepCount);
        return MathF.Max(minAlpha, MathF.Pow(alpha, 1f / exponent));
    }

    private float ScaleLegacyUiDistance(float distancePerTick)
    {
        return distancePerTick * GetLegacyUiStepCount();
    }

    private bool TryGetLocalPlayerAimDirection(PlayerEntity player, out float aimDirectionDegrees)
    {
        if (!ReferenceEquals(player, _world.LocalPlayer) || !_hasLatestLocalAimWorldPosition)
        {
            aimDirectionDegrees = 0f;
            return false;
        }

        var aimDeltaX = _latestLocalAimWorldX - player.X;
        var aimDeltaY = _latestLocalAimWorldY - player.Y;
        aimDirectionDegrees = MathF.Atan2(aimDeltaY, aimDeltaX) * (180f / MathF.PI);
        return true;
    }
}
