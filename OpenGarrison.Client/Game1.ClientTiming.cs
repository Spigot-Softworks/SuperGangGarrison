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
    private bool _pendingPredictedJumpPress;
    private bool _pendingPredictedSecondaryPress;
    private uint _latchedJumpPressSequence;

    private int ConsumeClientTickCount(GameTime gameTime)
    {
        _clientUpdateElapsedSeconds = (float)Math.Clamp(gameTime.ElapsedGameTime.TotalSeconds, 0d, 0.1d);
        _clientTickAccumulatorSeconds += _clientUpdateElapsedSeconds;

        var ticks = 0;
        var maxCatchUpTicks = 8;
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
        _pendingPredictedJumpPress = false;
        _pendingPredictedSecondaryPress = false;
        _latchedJumpPressSequence = 0;
    }

    private void CapturePendingPredictedInputEdges(KeyboardState keyboard, MouseState mouse, PlayerInputSnapshot networkInput)
    {
        _latestPredictedLocalInput = networkInput;

        if (!networkInput.Up && !networkInput.FireSecondary)
        {
            return;
        }

        var jumpPressed = networkInput.Up
            && ((keyboard.IsKeyDown(_inputBindings.MoveUp) && !_previousKeyboard.IsKeyDown(_inputBindings.MoveUp))
                || (keyboard.IsKeyDown(Keys.Up) && !_previousKeyboard.IsKeyDown(Keys.Up)));
        if (jumpPressed)
        {
            _pendingPredictedJumpPress = true;
        }

        var secondaryPressed = networkInput.FireSecondary
            && mouse.RightButton == ButtonState.Pressed
            && _previousMouse.RightButton != ButtonState.Pressed;
        if (secondaryPressed)
        {
            _pendingPredictedSecondaryPress = true;
        }
    }

    private void AdvanceNetworkInputLane(PlayerInputSnapshot networkInput)
    {
        _networkInputAccumulatorSeconds += _clientUpdateElapsedSeconds;
        while (_networkInputAccumulatorSeconds >= _config.FixedDeltaSeconds)
        {
            _networkInputAccumulatorSeconds -= _config.FixedDeltaSeconds;
            var outboundNetworkInput = networkInput;
            if (_latchedJumpPressSequence != 0 && !outboundNetworkInput.Up)
            {
                // Keep jump held in the outbound stream until authority confirms it
                // processed one matching input, so brief tap timing can't lose the edge.
                outboundNetworkInput = outboundNetworkInput with { Up = true };
            }

            var sentInputSequence = _networkClient.SendInput(outboundNetworkInput);
            if (_pendingPredictedJumpPress && sentInputSequence != 0)
            {
                _latchedJumpPressSequence = sentInputSequence;
            }

            RecordPredictedInput(
                sentInputSequence,
                outboundNetworkInput,
                _pendingPredictedJumpPress,
                _pendingPredictedSecondaryPress);
            _pendingPredictedJumpPress = false;
            _pendingPredictedSecondaryPress = false;
        }
    }

    private void AcknowledgeLatchedPredictedInputs(uint lastProcessedInputSequence)
    {
        if (_latchedJumpPressSequence != 0 && lastProcessedInputSequence >= _latchedJumpPressSequence)
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
            AdvanceChatHud();
            UpdateNoticeState();
            AdvanceExplosionVisuals();
            AdvanceImpactVisuals();
            AdvanceBloodVisuals();
            AdvanceShellVisuals();
            AdvanceRocketSmokeVisuals();
            AdvanceMineTrailVisuals();
            AdvanceFlameSmokeVisuals();
            AdvanceLooseSheetVisuals();

            if (_autoBalanceNoticeTicks > 0)
            {
                _autoBalanceNoticeTicks = Math.Max(0, _autoBalanceNoticeTicks - 1);
                if (_autoBalanceNoticeTicks == 0)
                {
                    _autoBalanceNoticeText = string.Empty;
                }
            }
        }

        AdvanceBackstabVisuals();
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
}
