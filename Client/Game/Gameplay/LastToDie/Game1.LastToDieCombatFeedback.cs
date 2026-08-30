#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using System;
using System.Globalization;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int LastToDieComboBounceTicks = 16;
    private const int LastToDieComboMilestonePopupTicks = 120;
    private const int LastToDieRageAnnouncementTicks = 28;
    private const int LastToDieRageShakeTicks = 18;
    private const float LastToDieComboScaleBonusDecayPerTick = 0.035f;
    private const float LastToDieComboMilestoneScaleBonusDecayPerTick = 0.05f;
    private const float LastToDieComboBaseScale = 4.4f;
    private const float LastToDieComboScaleStepPerHit = 0.08f;
    private const float LastToDieComboMaxScaleGrowth = 1.15f;
    private const float LastToDieComboMaxScaleBonus = 0.8f;
    private const float LastToDieComboMilestoneBaseScale = 1.9f;
    private const float LastToDieComboMilestoneMaxScaleBonus = 0.65f;
    private const float LastToDieRagePopupBaseScale = 5.2f;
    private const float LastToDieRageShakeMagnitude = 10f;
    private const int LastToDieRageBarWidth = 170;
    private const int LastToDieRageBarHeight = 18;
    private const int LastToDieSpyCloakBarWidth = 170;
    private const int LastToDieSpyCloakBarHeight = 14;

    private readonly record struct LastToDieComboMilestoneDefinition(int Threshold, string Text);

    private static readonly LastToDieComboMilestoneDefinition[] LastToDieComboMilestones =
    [
        new(5, "Ok!"),
        new(10, "Bussin'!"),
        new(25, "Good!"),
        new(35, "Neat!"),
        new(45, "Not Bad!"),
        new(55, "Solid!"),
        new(65, "Smooth!"),
        new(80, "Great!"),
        new(95, "Tasty!"),
        new(105, "Sweet!"),
        new(115, "Tubular!"),
        new(125, "Far out!"),
        new(150, "Wicked!"),
        new(175, "Fantastic!"),
        new(185, "Bombastic!"),
        new(200, "Sicknasty!"),
        new(225, "Epic!"),
        new(250, "Unbelievable!"),
        new(275, "Finger Lickin'!"),
    ];

    private int _lastToDieObservedCombo;
    private bool _lastToDieObservedRageActive;
    private ulong _lastToDieObservedCombatAnnouncementEventId;
    private int _lastToDieComboBounceTicksRemaining;
    private float _lastToDieComboScaleBonus;
    private string? _lastToDieComboMilestoneText;
    private int _lastToDieComboMilestoneTicksRemaining;
    private float _lastToDieComboMilestoneScaleBonus;
    private int _lastToDieRageAnnouncementTicksRemaining;
    private int _lastToDieRageShakeTicksRemaining;
    private Vector2 _lastToDieRageCurrentShakeOffset;

    private void ResetLastToDieCombatFeedbackPresentation()
    {
        _lastToDieObservedCombo = 0;
        _lastToDieObservedRageActive = false;
        _lastToDieObservedCombatAnnouncementEventId = 0;
        _lastToDieComboBounceTicksRemaining = 0;
        _lastToDieComboScaleBonus = 0f;
        _lastToDieComboMilestoneText = null;
        _lastToDieComboMilestoneTicksRemaining = 0;
        _lastToDieComboMilestoneScaleBonus = 0f;
        _lastToDieRageAnnouncementTicksRemaining = 0;
        _lastToDieRageShakeTicksRemaining = 0;
        _lastToDieRageCurrentShakeOffset = Vector2.Zero;
    }

    private void ObserveLastToDieCombatFeedbackState()
    {
        if (!IsCombatPerformanceFeedbackSessionActive || _world.LocalPlayerAwaitingJoin)
        {
            _lastToDieObservedCombo = 0;
            _lastToDieObservedRageActive = false;
            _lastToDieObservedCombatAnnouncementEventId = 0;
            return;
        }

        _lastToDieObservedCombo = _world.LocalPlayer.CurrentCombo;
        _lastToDieObservedRageActive = _world.LocalPlayer.IsRaging;
        _lastToDieObservedCombatAnnouncementEventId = GetLatestObservedLastToDieCombatAnnouncementEventId();
    }

    private void UpdateLastToDieCombatFeedbackPresentation()
    {
        if (!IsCombatPerformanceFeedbackSessionActive)
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

        if (_lastToDieComboMilestoneTicksRemaining > 0)
        {
            _lastToDieComboMilestoneTicksRemaining -= 1;
            if (_lastToDieComboMilestoneTicksRemaining <= 0)
            {
                _lastToDieComboMilestoneText = null;
                _lastToDieComboMilestoneTicksRemaining = 0;
            }
        }

        if (_lastToDieComboMilestoneScaleBonus > 0f)
        {
            _lastToDieComboMilestoneScaleBonus = float.Max(
                0f,
                _lastToDieComboMilestoneScaleBonus - LastToDieComboMilestoneScaleBonusDecayPerTick);
        }

        if (_lastToDieRageAnnouncementTicksRemaining > 0)
        {
            _lastToDieRageAnnouncementTicksRemaining -= 1;
        }

        if (_lastToDieRageShakeTicksRemaining > 0)
        {
            var shakeLife = _lastToDieRageShakeTicksRemaining / (float)LastToDieRageShakeTicks;
            var magnitude = LastToDieRageShakeMagnitude * shakeLife;
            _lastToDieRageCurrentShakeOffset = new Vector2(
                (_visualRandom.NextSingle() * 2f - 1f) * magnitude,
                (_visualRandom.NextSingle() * 2f - 1f) * magnitude);
            _lastToDieRageShakeTicksRemaining -= 1;
        }
        else
        {
            _lastToDieRageCurrentShakeOffset = Vector2.Zero;
        }

        if (_world.LocalPlayerAwaitingJoin)
        {
            ObserveLastToDieCombatFeedbackState();
            return;
        }

        ObserveLastToDieCombatAnnouncementPopup();

        if (!IsLastToDieSessionActive || _lastToDieRun is null)
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

            if (TryGetLatestLastToDieComboMilestoneText(_lastToDieObservedCombo, currentCombo, out var milestoneText))
            {
                TriggerLastToDieCalloutPopup(milestoneText);
            }
        }
        else if (currentCombo <= 0)
        {
            _lastToDieComboBounceTicksRemaining = 0;
            _lastToDieComboScaleBonus = 0f;
        }

        var isRageActive = _world.LocalPlayer.IsRaging;
        if (isRageActive && !_lastToDieObservedRageActive)
        {
            _lastToDieRageAnnouncementTicksRemaining = LastToDieRageAnnouncementTicks;
            _lastToDieRageShakeTicksRemaining = LastToDieRageShakeTicks;
        }

        ObserveLastToDieCombatFeedbackState();
    }

    private void DrawLastToDieCombatFeedbackHud()
    {
        if (!ShouldDrawCombatPerformanceFeedbackHud())
        {
            return;
        }

        if (IsLastToDieSessionActive)
        {
            DrawLastToDieComboOverlay();
            DrawLastToDieRageOverlay();
        }

        DrawLastToDieCombatAnnouncementPopup();
    }

    private bool ShouldDrawLastToDieCombatFeedbackHud()
    {
        if (IsHostedLastToDieActive())
        {
            return ShouldPresentHostedLastToDieCombatFeedbackHud(
                _networkClient.LastToDieState.Snapshot?.Phase,
                _world.LocalPlayer.IsAlive,
                _world.LocalPlayerAwaitingJoin);
        }

        return IsLastToDieSessionActive
            && _lastToDieRun is not null
            && !_lastToDiePerkMenuOpen
            && !IsLastToDieFailurePresentationActive()
            && !_world.LocalPlayerAwaitingJoin;
    }

    internal static bool ShouldPresentHostedLastToDieCombatFeedbackHud(
        LastToDieWirePhase? phase,
        bool localPlayerAlive,
        bool localPlayerAwaitingJoin)
    {
        return phase == LastToDieWirePhase.Playing
            && localPlayerAlive
            && !localPlayerAwaitingJoin;
    }

    private bool IsCombatPerformanceFeedbackSessionActive =>
        IsPracticeSessionActive || IsLastToDieSessionActive;

    private bool ShouldDrawCombatPerformanceFeedbackHud()
    {
        return IsCombatPerformanceFeedbackSessionActive
            && !_world.LocalPlayerAwaitingJoin
            && (!IsLastToDieSessionActive || ShouldDrawLastToDieCombatFeedbackHud());
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

    private void DrawLastToDieCombatAnnouncementPopup()
    {
        if (_lastToDieComboMilestoneTicksRemaining <= 0 || string.IsNullOrWhiteSpace(_lastToDieComboMilestoneText))
        {
            return;
        }

        var localPlayer = _world.LocalPlayer;
        var popupAnchor = new Vector2(ViewportWidth * 0.5f, Math.Max(104f, ViewportHeight * 0.28f));
        var popupAnchorOffsetX = 0f;
        var popupAlpha = Math.Clamp(
            _lastToDieComboMilestoneTicksRemaining / (float)LastToDieComboMilestonePopupTicks,
            0f,
            1f);
        if (localPlayer.CurrentCombo >= 2)
        {
            var comboTimeoutTicks = Math.Max(
                1,
                (int)MathF.Round(_config.TicksPerSecond * ExperimentalGameplaySettings.ComboTimeoutSeconds));
            var comboLife = Math.Clamp(localPlayer.ComboTicksRemaining / (float)comboTimeoutTicks, 0f, 1f);
            var comboGrowthScale = MathF.Min(
                LastToDieComboMaxScaleGrowth,
                Math.Max(0, localPlayer.CurrentCombo - 2) * LastToDieComboScaleStepPerHit);
            var comboScale = LastToDieComboBaseScale + comboGrowthScale + _lastToDieComboScaleBonus;
            var comboText = "x" + localPlayer.CurrentCombo.ToString(CultureInfo.InvariantCulture);
            popupAnchor = new Vector2(
                48f + (MeasureBitmapFontWidth(comboText, comboScale) * 0.5f),
                Math.Max(104f, ViewportHeight * 0.18f));
            popupAnchorOffsetX = (MeasureBitmapFontWidth(comboText, comboScale) * 0.58f) + 54f;
            popupAlpha = (0.45f + (comboLife * 0.55f))
                * Math.Clamp(
                    _lastToDieComboMilestoneTicksRemaining / (float)LastToDieComboMilestonePopupTicks,
                    0f,
                    1f);
        }

        var popupProgress = 1f - (_lastToDieComboMilestoneTicksRemaining / (float)LastToDieComboMilestonePopupTicks);
        var popupBounceScale = MathF.Sin(popupProgress * MathF.PI) * 0.28f;
        var popupScale = LastToDieComboMilestoneBaseScale + _lastToDieComboMilestoneScaleBonus + popupBounceScale;
        var popupRiseOffset = (1f - popupProgress) * 16f;
        var popupRotation = GetLastToDieComboMilestoneRotation(popupProgress);
        var popupPosition = new Vector2(
            popupAnchor.X + popupAnchorOffsetX,
            popupAnchor.Y + 4f - popupRiseOffset);
        var popupShadowOffset = new Vector2(4f, 4f);

        DrawBitmapFontTextCentered(_lastToDieComboMilestoneText, popupPosition + popupShadowOffset, Color.White * popupAlpha, popupScale, popupRotation);
        DrawBitmapFontTextCentered(_lastToDieComboMilestoneText, popupPosition, new Color(214, 24, 24) * popupAlpha, popupScale, popupRotation);
    }

    private void DrawLastToDieRageHud()
    {
        var localPlayer = _world.LocalPlayer;
        if (!TryResolveHudElement(HudElementId.LastToDieRage, out var resolved))
        {
            return;
        }

        var origin = resolved.Origin;
        var barRectangle = new Rectangle(
            (int)MathF.Round(origin.X),
            (int)MathF.Round(origin.Y),
            LastToDieRageBarWidth,
            LastToDieRageBarHeight);
        var shadowRectangle = new Rectangle(barRectangle.X + 4, barRectangle.Y + 4, barRectangle.Width, barRectangle.Height);
        var frameRectangle = new Rectangle(barRectangle.X - 3, barRectangle.Y - 3, barRectangle.Width + 6, barRectangle.Height + 6);
        var troughRectangle = new Rectangle(barRectangle.X + 2, barRectangle.Y + 2, barRectangle.Width - 4, barRectangle.Height - 4);
        var shineRectangle = new Rectangle(troughRectangle.X, troughRectangle.Y, troughRectangle.Width, Math.Max(3, troughRectangle.Height / 3));
        var fillRectangle = new Rectangle(
            troughRectangle.X,
            troughRectangle.Y,
            (int)MathF.Round(troughRectangle.Width * Math.Clamp(localPlayer.RageCharge / ExperimentalGameplaySettings.RageMaxCharge, 0f, 1f)),
            troughRectangle.Height);
        var readyPulse = localPlayer.IsRageReady
            ? 0.78f + (MathF.Sin((float)(_world.Frame * 0.23f)) * 0.22f)
            : 0f;
        var frameColor = localPlayer.IsRaging
            ? new Color(255, 236, 236)
            : localPlayer.IsRageReady
                ? Color.Lerp(new Color(255, 222, 222), new Color(255, 255, 255), readyPulse)
                : new Color(166, 132, 132);
        var fillColor = localPlayer.IsRaging
            ? new Color(255, 72, 72)
            : localPlayer.IsRageReady
                ? Color.Lerp(new Color(214, 24, 24), new Color(255, 86, 86), readyPulse)
                : new Color(164, 28, 28);

        _spriteBatch.Draw(_pixel, shadowRectangle, ApplyCurrentHudElementOpacity(Color.Black * 0.4f));
        _spriteBatch.Draw(_pixel, frameRectangle, ApplyCurrentHudElementOpacity(frameColor));
        _spriteBatch.Draw(_pixel, barRectangle, ApplyCurrentHudElementOpacity(new Color(24, 8, 8)));
        _spriteBatch.Draw(_pixel, troughRectangle, ApplyCurrentHudElementOpacity(new Color(54, 10, 10)));
        _spriteBatch.Draw(_pixel, shineRectangle, ApplyCurrentHudElementOpacity(new Color(255, 222, 222) * 0.12f));
        if (fillRectangle.Width > 0)
        {
            _spriteBatch.Draw(_pixel, fillRectangle, ApplyCurrentHudElementOpacity(fillColor));
            _spriteBatch.Draw(
                _pixel,
                new Rectangle(fillRectangle.X, fillRectangle.Y, fillRectangle.Width, Math.Max(2, fillRectangle.Height / 3)),
                ApplyCurrentHudElementOpacity(new Color(255, 214, 214) * 0.18f));
        }

        var rageLabelPosition = new Vector2(barRectangle.Center.X, barRectangle.Y - 18f);
        DrawBitmapFontTextCentered("RAGE", rageLabelPosition + new Vector2(2f, 2f), Color.Black * 0.75f, 1f);
        DrawBitmapFontTextCentered("RAGE", rageLabelPosition, new Color(240, 240, 240), 1f);
        var renderedBounds = Rectangle.Union(
            frameRectangle,
            new Rectangle(barRectangle.X - 8, barRectangle.Y - 28, barRectangle.Width + 16, barRectangle.Height + 48));

        if (!localPlayer.IsRaging && !localPlayer.IsRageReady)
        {
            UpdateHudElementBounds(HudElementId.LastToDieRage, renderedBounds);
            return;
        }

        var stateText = localPlayer.IsRaging ? "ACTIVE" : "PRESS F";
        var stateColor = localPlayer.IsRaging
            ? new Color(255, 236, 236)
            : new Color(255, 214, 214);
        const float stateScale = 0.96f;
        DrawBitmapFontTextCentered(stateText, new Vector2(barRectangle.Center.X, barRectangle.Bottom + 8f), Color.Black * 0.7f, stateScale);
        DrawBitmapFontTextCentered(stateText, new Vector2(barRectangle.Center.X, barRectangle.Bottom + 6f), stateColor, stateScale);
        UpdateHudElementBounds(HudElementId.LastToDieRage, renderedBounds);
    }

    private bool ShouldDrawLastToDieSpyCloakHud()
    {
        var localPlayer = _world.LocalPlayer;
        return ShouldDrawLastToDieActionStatusHud()
            && localPlayer.ClassId == PlayerClass.Spy
            && (localPlayer.LastToDieRogueCommanderEnabled || localPlayer.LastToDieProfessionalEnabled);
    }

    private void DrawLastToDieSpyCloakHud()
    {
        var localPlayer = _world.LocalPlayer;
        if (!TryResolveHudElement(HudElementId.LastToDieSpyCloak, out var resolved))
        {
            return;
        }

        var origin = resolved.Origin;
        var barRectangle = new Rectangle(
            (int)MathF.Round(origin.X),
            (int)MathF.Round(origin.Y),
            LastToDieSpyCloakBarWidth,
            LastToDieSpyCloakBarHeight);
        var shadowRectangle = new Rectangle(barRectangle.X + 4, barRectangle.Y + 4, barRectangle.Width, barRectangle.Height);
        var frameRectangle = new Rectangle(barRectangle.X - 3, barRectangle.Y - 3, barRectangle.Width + 6, barRectangle.Height + 6);
        var troughRectangle = new Rectangle(barRectangle.X + 2, barRectangle.Y + 2, barRectangle.Width - 4, barRectangle.Height - 4);
        var meterFraction = GetPlayerLastToDieSpyCloakMeterFraction(localPlayer);
        var fillRectangle = new Rectangle(
            troughRectangle.X,
            troughRectangle.Y,
            (int)MathF.Round(troughRectangle.Width * meterFraction),
            troughRectangle.Height);
        var fillColor = GetPlayerIsSpyCloaked(localPlayer)
            ? new Color(124, 218, 255)
            : new Color(74, 155, 204);

        _spriteBatch.Draw(_pixel, shadowRectangle, ApplyCurrentHudElementOpacity(Color.Black * 0.4f));
        _spriteBatch.Draw(_pixel, frameRectangle, ApplyCurrentHudElementOpacity(new Color(204, 231, 241)));
        _spriteBatch.Draw(_pixel, barRectangle, ApplyCurrentHudElementOpacity(new Color(10, 22, 31)));
        _spriteBatch.Draw(_pixel, troughRectangle, ApplyCurrentHudElementOpacity(new Color(18, 48, 67)));
        if (fillRectangle.Width > 0)
        {
            _spriteBatch.Draw(_pixel, fillRectangle, ApplyCurrentHudElementOpacity(fillColor));
        }

        var meterPercent = (int)MathF.Round(meterFraction * 100f);
        var labelPosition = new Vector2(barRectangle.Center.X, barRectangle.Y - 18f);
        var label = $"CLOAK {meterPercent.ToString(CultureInfo.InvariantCulture)}%";
        DrawBitmapFontTextCentered(label, labelPosition + new Vector2(2f, 2f), Color.Black * 0.75f, 1f);
        DrawBitmapFontTextCentered(label, labelPosition, new Color(230, 246, 255), 1f);

        var renderedBounds = Rectangle.Union(
            frameRectangle,
            new Rectangle(barRectangle.X - 8, barRectangle.Y - 28, barRectangle.Width + 16, barRectangle.Height + 48));
        var rampStacks = GetPlayerLastToDieSpyRogueRampStacks(localPlayer);
        if (localPlayer.LastToDieRogueCommanderEnabled && rampStacks > 0)
        {
            var rampPercent = rampStacks * 5;
            var rampText = $"POWER +{rampPercent.ToString(CultureInfo.InvariantCulture)}%";
            const float rampScale = 0.9f;
            var rampPosition = new Vector2(barRectangle.Center.X, barRectangle.Bottom + 7f);
            DrawBitmapFontTextCentered(rampText, rampPosition + new Vector2(2f, 2f), Color.Black * 0.7f, rampScale);
            DrawBitmapFontTextCentered(rampText, rampPosition, new Color(185, 232, 255), rampScale);
        }

        UpdateHudElementBounds(HudElementId.LastToDieSpyCloak, renderedBounds);
    }

    private void DrawLastToDieRageOverlay()
    {
        if (_lastToDieRageAnnouncementTicksRemaining <= 0)
        {
            return;
        }

        var progress = 1f - (_lastToDieRageAnnouncementTicksRemaining / (float)LastToDieRageAnnouncementTicks);
        var bounceScale = MathF.Sin(progress * MathF.PI) * 0.34f;
        var scale = LastToDieRagePopupBaseScale + bounceScale;
        var alpha = Math.Clamp(_lastToDieRageAnnouncementTicksRemaining / (float)LastToDieRageAnnouncementTicks, 0f, 1f);
        var drawPosition = new Vector2(ViewportWidth / 2f, ViewportHeight * 0.28f);
        var shadowOffset = new Vector2(6f, 6f);

        DrawBitmapFontTextCentered("RAGE!", drawPosition + shadowOffset, Color.White * alpha, scale);
        DrawBitmapFontTextCentered("RAGE!", drawPosition, new Color(214, 24, 24) * alpha, scale);
    }

    private Vector2 GetLastToDieCameraShakeOffset()
    {
        return _lastToDieRageCurrentShakeOffset;
    }

    private static bool TryGetLatestLastToDieComboMilestoneText(int previousCombo, int currentCombo, out string text)
    {
        text = string.Empty;
        if (currentCombo <= previousCombo)
        {
            return false;
        }

        var highestThreshold = -1;
        for (var index = 0; index < LastToDieComboMilestones.Length; index += 1)
        {
            var milestone = LastToDieComboMilestones[index];
            if (previousCombo >= milestone.Threshold || currentCombo < milestone.Threshold || milestone.Threshold <= highestThreshold)
            {
                continue;
            }

            highestThreshold = milestone.Threshold;
            text = milestone.Text;
        }

        if (TryGetLatestLastToDieRecurringUnholyThreshold(previousCombo, currentCombo, out var unholyThreshold)
            && unholyThreshold > highestThreshold)
        {
            text = "Unholy!";
            return true;
        }

        return highestThreshold >= 0;
    }

    private static bool TryGetLatestLastToDieRecurringUnholyThreshold(int previousCombo, int currentCombo, out int threshold)
    {
        threshold = -1;
        if (currentCombo < 300)
        {
            return false;
        }

        var currentStep = (currentCombo - 300) / 25;
        threshold = 300 + (currentStep * 25);
        return threshold > previousCombo;
    }

    private void ObserveLastToDieCombatAnnouncementPopup()
    {
        var localPlayerId = _world.LocalPlayer.Id;
        var latestObservedEventId = _lastToDieObservedCombatAnnouncementEventId;
        string? latestAnnouncementText = null;

        foreach (var entry in _world.KillFeed)
        {
            if (entry.EventId <= _lastToDieObservedCombatAnnouncementEventId)
            {
                continue;
            }

            latestObservedEventId = Math.Max(latestObservedEventId, entry.EventId);
            if ((IsLastToDieSessionActive
                    ? entry.KillerPlayerId != localPlayerId
                    : entry.KillerPlayerId < 0)
                || entry.VictimPlayerId >= 0
                || entry.MessageHighlightLength <= 0
                || string.IsNullOrWhiteSpace(entry.MessageText))
            {
                continue;
            }

            latestAnnouncementText = BuildLastToDieCombatAnnouncementPopupText(entry);
        }

        _lastToDieObservedCombatAnnouncementEventId = latestObservedEventId;
        if (!string.IsNullOrWhiteSpace(latestAnnouncementText))
        {
            TriggerLastToDieCalloutPopup(latestAnnouncementText);
        }
    }

    private static string BuildLastToDieCombatAnnouncementPopupText(KillFeedEntry entry)
    {
        var messageHighlight = entry.MessageText.Substring(entry.MessageHighlightStart, entry.MessageHighlightLength).Trim();
        if (string.IsNullOrWhiteSpace(messageHighlight))
        {
            return string.Empty;
        }

        return messageHighlight.EndsWith('!') ? messageHighlight : messageHighlight + "!";
    }

    private ulong GetLatestObservedLastToDieCombatAnnouncementEventId()
    {
        ulong latestEventId = 0;
        var localPlayerId = _world.LocalPlayer.Id;
        foreach (var entry in _world.KillFeed)
        {
            if ((IsLastToDieSessionActive
                    ? entry.KillerPlayerId == localPlayerId
                    : entry.KillerPlayerId >= 0)
                && entry.VictimPlayerId < 0
                && entry.MessageHighlightLength > 0)
            {
                latestEventId = Math.Max(latestEventId, entry.EventId);
            }
        }

        return latestEventId;
    }

    private void TriggerLastToDieCalloutPopup(string text)
    {
        _lastToDieComboMilestoneText = text;
        _lastToDieComboMilestoneTicksRemaining = LastToDieComboMilestonePopupTicks;
        _lastToDieComboMilestoneScaleBonus = LastToDieComboMilestoneMaxScaleBonus;
    }

    private static float GetLastToDieComboMilestoneRotation(float popupProgress)
    {
        const float introProgress = 0.22f;
        const float outroProgress = 0.76f;

        if (popupProgress < introProgress)
        {
            var introT = 1f - (popupProgress / introProgress);
            return introT * introT * MathF.PI * 1.4f;
        }

        if (popupProgress > outroProgress)
        {
            var outroT = (popupProgress - outroProgress) / (1f - outroProgress);
            return -(outroT * outroT) * MathF.PI * 1.15f;
        }

        return 0f;
    }
}
