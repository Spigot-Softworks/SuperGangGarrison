#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;
using System;
using System.Globalization;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int LastToDieComboBounceTicks = 16;
    private const float LastToDieComboScaleBonusDecayPerTick = 0.035f;
    private const float LastToDieComboBaseScale = 4.4f;
    private const float LastToDieComboScaleStepPerHit = 0.08f;
    private const float LastToDieComboMaxScaleGrowth = 1.15f;
    private const float LastToDieComboMaxScaleBonus = 0.8f;

    private int _lastToDieObservedCombo;
    private int _lastToDieComboBounceTicksRemaining;
    private float _lastToDieComboScaleBonus;

    private void ResetLastToDieCombatFeedbackPresentation()
    {
        _lastToDieObservedCombo = 0;
        _lastToDieComboBounceTicksRemaining = 0;
        _lastToDieComboScaleBonus = 0f;
    }

    private void ObserveLastToDieCombatFeedbackState()
    {
        _lastToDieObservedCombo = !IsLastToDieSessionActive || _world.LocalPlayerAwaitingJoin
            ? 0
            : _world.LocalPlayer.CurrentCombo;
    }

    private void UpdateLastToDieCombatFeedbackPresentation()
    {
        if (!IsLastToDieSessionActive || _lastToDieRun is null)
        {
            ResetLastToDieCombatFeedbackPresentation();
            return;
        }

        if (_lastToDieComboBounceTicksRemaining > 0)
        {
            _lastToDieComboBounceTicksRemaining -= 1;
        }

        if (_lastToDieComboScaleBonus > 0f)
        {
            _lastToDieComboScaleBonus = float.Max(0f, _lastToDieComboScaleBonus - LastToDieComboScaleBonusDecayPerTick);
        }

        if (_world.LocalPlayerAwaitingJoin)
        {
            ObserveLastToDieCombatFeedbackState();
            return;
        }

        var currentCombo = _world.LocalPlayer.CurrentCombo;
        if (currentCombo > _lastToDieObservedCombo)
        {
            _lastToDieComboBounceTicksRemaining = LastToDieComboBounceTicks;
            _lastToDieComboScaleBonus = float.Min(
                LastToDieComboMaxScaleBonus,
                _lastToDieComboScaleBonus + 0.12f);
        }
        else if (currentCombo <= 0)
        {
            _lastToDieComboBounceTicksRemaining = 0;
            _lastToDieComboScaleBonus = 0f;
        }

        ObserveLastToDieCombatFeedbackState();
    }

    private void DrawLastToDieCombatFeedbackHud()
    {
        if (!IsLastToDieSessionActive
            || _lastToDieRun is null
            || _lastToDiePerkMenuOpen
            || IsLastToDieFailurePresentationActive()
            || _world.LocalPlayerAwaitingJoin)
        {
            return;
        }

        DrawLastToDieComboOverlay();
    }

    private void DrawLastToDieComboOverlay()
    {
        var localPlayer = _world.LocalPlayer;
        if (localPlayer.CurrentCombo < 2)
        {
            return;
        }

        var comboTimeoutTicks = Math.Max(
            1,
            (int)MathF.Round(_config.TicksPerSecond * ExperimentalGameplaySettings.ComboTimeoutSeconds));
        var comboLife = Math.Clamp(localPlayer.ComboTicksRemaining / (float)comboTimeoutTicks, 0f, 1f);
        var bounceProgress = 1f - (_lastToDieComboBounceTicksRemaining / (float)Math.Max(1, LastToDieComboBounceTicks));
        var bounceScale = MathF.Sin(bounceProgress * MathF.PI) * 0.24f;
        var comboGrowthScale = MathF.Min(
            LastToDieComboMaxScaleGrowth,
            Math.Max(0, localPlayer.CurrentCombo - 2) * LastToDieComboScaleStepPerHit);
        var scale = LastToDieComboBaseScale + comboGrowthScale + _lastToDieComboScaleBonus + bounceScale;
        var alpha = 0.45f + (comboLife * 0.55f);
        var comboText = "x" + localPlayer.CurrentCombo.ToString(CultureInfo.InvariantCulture);
        var drawPosition = new Vector2(
            48f + (MeasureBitmapFontWidth(comboText, scale) * 0.5f),
            Math.Max(104f, ViewportHeight * 0.18f));
        var shadowOffset = new Vector2(5f, 5f);

        DrawBitmapFontTextCentered(comboText, drawPosition + shadowOffset, Color.White * alpha, scale);
        DrawBitmapFontTextCentered(comboText, drawPosition, new Color(214, 24, 24) * alpha, scale);
    }
}
