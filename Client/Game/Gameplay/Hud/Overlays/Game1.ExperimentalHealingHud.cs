#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int ExperimentalHealingHudIndicatorLifetimeTicks = ClientUpdateTicksPerSecond;
    private const int ExperimentalHealingHudIndicatorMergeTicks = 9;
    private const float ExperimentalHealingHudIndicatorRisePerTick = 0.35f;
    private const float ExperimentalHealingHudIndicatorTextScale = 1.8f;
    private static readonly Color ExperimentalHealingHudTextColor = new(112, 238, 120);
    private readonly List<ExperimentalHealingHudIndicator> _experimentalHealingHudIndicators = new();

    private void ResetExperimentalHealingHudIndicators()
    {
        _experimentalHealingHudIndicators.Clear();
    }

    private void QueuePendingExperimentalHealingHudIndicators()
    {
        foreach (var healingEvent in _world.DrainPendingHealingEvents())
        {
            if (healingEvent.TargetPlayerId != _world.LocalPlayer.Id || healingEvent.Amount <= 0)
            {
                continue;
            }

            QueueExperimentalHealingHudIndicator(healingEvent.Amount);
        }
    }

    private void QueueExperimentalHealingHudIndicator(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        var index = _experimentalHealingHudIndicators.Count;
        while (index > 0)
        {
            var indicatorIndex = index - 1;
            index -= 1;
            var indicator = _experimentalHealingHudIndicators[indicatorIndex];
            if (indicator.AgeTicks > ExperimentalHealingHudIndicatorMergeTicks)
            {
                break;
            }

            indicator.Amount += amount;
            indicator.AgeTicks = 0;
            indicator.YOffset = 0f;
            _experimentalHealingHudIndicators[indicatorIndex] = indicator;
            return;
        }

        _experimentalHealingHudIndicators.Add(new ExperimentalHealingHudIndicator(amount));
    }

    private void AdvanceExperimentalHealingHudIndicators()
    {
        for (var index = _experimentalHealingHudIndicators.Count - 1; index >= 0; index -= 1)
        {
            var indicator = _experimentalHealingHudIndicators[index];
            indicator.AgeTicks += 1;
            indicator.YOffset -= ExperimentalHealingHudIndicatorRisePerTick;
            if (indicator.AgeTicks >= ExperimentalHealingHudIndicatorLifetimeTicks)
            {
                _experimentalHealingHudIndicators.RemoveAt(index);
                continue;
            }

            _experimentalHealingHudIndicators[index] = indicator;
        }
    }

    private void DrawExperimentalHealingHudIndicators()
    {
        if (_experimentalHealingHudIndicators.Count == 0)
        {
            return;
        }

        for (var index = 0; index < _experimentalHealingHudIndicators.Count; index += 1)
        {
            var indicator = _experimentalHealingHudIndicators[index];
            var alpha = Math.Clamp(1f - (indicator.AgeTicks / (float)ExperimentalHealingHudIndicatorLifetimeTicks), 0f, 1f);
            if (alpha <= 0f)
            {
                continue;
            }

            var text = $"+{indicator.Amount}";
            var drawY = (ViewportHeight - 58f) + indicator.YOffset - MeasureBitmapFontHeight(ExperimentalHealingHudIndicatorTextScale);
            var drawPosition = new Vector2(66f, drawY);
            DrawBitmapFontTextCentered(text, drawPosition + Vector2.One, Color.Black * alpha, ExperimentalHealingHudIndicatorTextScale);
            DrawBitmapFontTextCentered(text, drawPosition, ExperimentalHealingHudTextColor * alpha, ExperimentalHealingHudIndicatorTextScale);
        }
    }

    private struct ExperimentalHealingHudIndicator(int amount)
    {
        public int Amount { get; set; } = amount;

        public int AgeTicks { get; set; }

        public float YOffset { get; set; }
    }
}
