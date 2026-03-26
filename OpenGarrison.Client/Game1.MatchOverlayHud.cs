#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Globalization;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void DrawControlPointHud()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        var centerX = viewportWidth / 2f;

        DrawControlPointTimer(centerX);

        if (_world.ControlPoints.Count == 0)
        {
            return;
        }

        var drawX = centerX - MathF.Floor(_world.ControlPoints.Count / 2f) * 48f;
        for (var index = 0; index < _world.ControlPoints.Count; index += 1)
        {
            var point = _world.ControlPoints[index];
            var teamOffset = point.Team switch
            {
                PlayerTeam.Red => 60,
                PlayerTeam.Blue => 90,
                _ => point.CappingTeam == PlayerTeam.Red ? 30 : 0,
            };

            var progressFrame = teamOffset;
            if (point.CappingTicks > 0f && point.CapTimeTicks > 0)
            {
                var progress = point.CappingTicks / point.CapTimeTicks;
                progressFrame = teamOffset + Math.Clamp((int)MathF.Floor(progress * 30f), 0, 30);
            }

            TryDrawScreenSprite("ControlPointStatusS", progressFrame, new Vector2(drawX, 560f), Color.White, new Vector2(3f, 3f));

            if (point.IsLocked)
            {
                TryDrawScreenSprite("ControlPointLockS", 0, new Vector2(drawX, 560f), Color.White, new Vector2(3f, 3f));
            }

            if (point.Cappers > 0 && !point.IsLocked)
            {
                TryDrawScreenSprite("ControlPointCappersS", 0, new Vector2(drawX, 560f), Color.White, new Vector2(3f, 3f));
                DrawHudTextCentered(point.Cappers.ToString(CultureInfo.InvariantCulture), new Vector2(drawX + 13f, 563f), Color.Black, 1.5f);
            }

            drawX += 60f;
        }
    }

    private void DrawControlPointTimer(float centerX)
    {
        var teamOffset = _world.LocalPlayerTeam == PlayerTeam.Blue ? 1 : 0;
        var timerPosition = new Vector2(centerX, 30f);

        if (_world.MatchState.IsOvertime)
        {
            TryDrawScreenSprite("TimerHudS", 2 + teamOffset, timerPosition, Color.White, new Vector2(3f, 3f));
            DrawHudTextCentered("OVERTIME", timerPosition, Color.White, 1f);
            return;
        }

        if (_world.ControlPointSetupActive && _world.ControlPointSetupTicksRemaining > 0)
        {
            TryDrawScreenSprite("TimerHudS", teamOffset, timerPosition, Color.White, new Vector2(3f, 3f));
            var setupFrame = Math.Clamp((int)MathF.Floor((_world.ControlPointSetupTicksRemaining / 1800f) * 12f), 0, 12);
            TryDrawScreenSprite("TimerS", setupFrame, new Vector2(centerX + 39f, 30f), Color.White, new Vector2(3f, 3f));

            DrawHudTimerText(centerX, FormatHudTimerText(_world.ControlPointSetupTicksRemaining), 20f);
            DrawHudTextCentered("Setup", new Vector2(centerX - 3f, 40f), Color.White, 1f);
            return;
        }

        TryDrawScreenSprite("TimerHudS", teamOffset, timerPosition, Color.White, new Vector2(3f, 3f));
        var timeLimitTicks = Math.Max(1, _world.MatchRules.TimeLimitTicks);
        var timerFrame = Math.Clamp((int)MathF.Floor((_world.MatchState.TimeRemainingTicks / (float)timeLimitTicks) * 12f), 0, 12);
        TryDrawScreenSprite("TimerS", timerFrame, new Vector2(centerX + 39f, 30f), Color.White, new Vector2(3f, 3f));

        DrawHudTimerText(centerX, FormatHudTimerText(_world.MatchState.TimeRemainingTicks));
    }

    private void DrawArenaHud()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        var centerX = viewportWidth / 2f;
        DrawMatchTimerHud(centerX);

        var statusBaseFrame = _world.ArenaPointTeam switch
        {
            PlayerTeam.Red => 60,
            PlayerTeam.Blue => 90,
            _ => _world.ArenaCappingTeam == PlayerTeam.Red ? 30 : 0,
        };
        var progressFrame = _world.ArenaCappingTicks > 0f
            ? statusBaseFrame + Math.Clamp((int)MathF.Floor((_world.ArenaCappingTicks / Math.Max(1f, _world.ArenaPointCapTimeTicks)) * 30f), 0, 30)
            : statusBaseFrame;

        TryDrawScreenSprite("ControlPointStatusS", progressFrame, new Vector2(centerX, 560f), Color.White, new Vector2(3f, 3f));

        if (_world.ArenaPointLocked)
        {
            var unlockSeconds = Math.Max(1, (int)MathF.Ceiling(_world.ArenaUnlockTicksRemaining / (float)_config.TicksPerSecond));
            DrawHudTextCentered(unlockSeconds.ToString(CultureInfo.InvariantCulture), new Vector2(centerX, 562f), Color.White, 1f);
        }
        else if (_world.ArenaCappers > 0)
        {
            TryDrawScreenSprite("ControlPointCappersS", 0, new Vector2(centerX, 560f), Color.White, new Vector2(3f, 3f));
            DrawHudTextCentered(_world.ArenaCappers.ToString(CultureInfo.InvariantCulture), new Vector2(centerX + 13f, 563f), Color.Black, 1f);
        }

        TryDrawScreenSprite("ArenaPlayerCountS", 0, new Vector2(centerX, 71f), Color.White, new Vector2(2f, 2f));
        DrawHudTextCentered(_world.ArenaRedAliveCount.ToString(CultureInfo.InvariantCulture), new Vector2(centerX + 15f, 73f), Color.Black, 1f);
        TryDrawScreenSprite("ArenaPlayerCountS", 1, new Vector2(centerX, 104f), Color.White, new Vector2(2f, 2f));
        DrawHudTextCentered(_world.ArenaBlueAliveCount.ToString(CultureInfo.InvariantCulture), new Vector2(centerX + 15f, 106f), Color.Black, 1f);
    }

    private void DrawGeneratorHud()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        var centerX = viewportWidth / 2f;
        var hudY = viewportHeight - 50f;
        DrawMatchTimerHud(centerX);

        TryDrawScreenSprite("GeneratorHUDS", 0, new Vector2(centerX, hudY), Color.White, new Vector2(2f, 2f));

        DrawGeneratorHudElement(_world.GetGenerator(PlayerTeam.Red), new Vector2(centerX - 50f, hudY), alignLeft: true);
        DrawGeneratorHudElement(_world.GetGenerator(PlayerTeam.Blue), new Vector2(centerX + 50f, hudY), alignLeft: false);
    }

    private void DrawGeneratorHudElement(GeneratorState? generator, Vector2 position, bool alignLeft)
    {
        const float barWidth = 52f;
        const float barHeight = 7f;
        var barColor = new Color(217, 217, 183);
        var barX = alignLeft ? position.X - 27f : position.X - 27f;
        var barRectangle = new Rectangle((int)barX, (int)(position.Y - 20f), (int)barWidth, (int)barHeight);
        DrawScreenHealthBar(barRectangle, generator?.Health ?? 0, generator?.MaxHealth ?? 1, useTeamColors: false, fillColor: barColor, backColor: Color.Black);

        if (generator is null || generator.IsDestroyed)
        {
            return;
        }

        var spriteName = generator.Team == PlayerTeam.Blue ? "GeneratorBlueS" : "GeneratorRedS";
        var frameIndex = GetGeneratorAnimationFrame(generator);
        if (TryDrawScreenSprite(spriteName, frameIndex, position, Color.White, Vector2.One))
        {
            return;
        }

        var fallbackRectangle = new Rectangle((int)(position.X - 12f), (int)(position.Y - 12f), 24, 24);
        var fallbackColor = generator.Team == PlayerTeam.Blue
            ? new Color(100, 160, 235)
            : new Color(220, 110, 90);
        _spriteBatch.Draw(_pixel, fallbackRectangle, fallbackColor);
    }

    private void DrawScorePanelCapLimit(float centerX, int viewportHeight)
    {
        if (_world.MatchRules.CapLimit <= 9)
        {
            DrawHudTextCentered(_world.MatchRules.CapLimit.ToString(CultureInfo.InvariantCulture), new Vector2(centerX - 2f, viewportHeight - 15f), Color.Black, 2f);
            return;
        }

        if (_world.MatchRules.CapLimit > 999)
        {
            DrawCenteredHudSprite("infinity", 0, new Vector2(centerX - 3f, viewportHeight - 17f), Color.White, new Vector2(2f, 2f));
            return;
        }

        DrawHudTextCentered(_world.MatchRules.CapLimit.ToString(CultureInfo.InvariantCulture), new Vector2(centerX - 2f, viewportHeight - 15f), Color.Black, 1f);
    }

    private void DrawMatchTimerHud(float centerX)
    {
        var teamOffset = _world.LocalPlayerTeam == PlayerTeam.Blue ? 1 : 0;
        var timerPosition = new Vector2(centerX, 30f);
        if (_world.MatchState.IsOvertime)
        {
            TryDrawScreenSprite("TimerHudS", 2 + teamOffset, timerPosition, Color.White, new Vector2(3f, 3f));
            DrawHudTextCentered("OVERTIME", timerPosition, Color.White, 1f);
            return;
        }

        TryDrawScreenSprite("TimerHudS", teamOffset, timerPosition, Color.White, new Vector2(3f, 3f));
        var timeLimitTicks = Math.Max(1, _world.MatchRules.TimeLimitTicks);
        var timerFrame = Math.Clamp((int)MathF.Floor((_world.MatchState.TimeRemainingTicks / (float)timeLimitTicks) * 12f), 0, 12);
        TryDrawScreenSprite("TimerS", timerFrame, new Vector2(centerX + 39f, 30f), Color.White, new Vector2(3f, 3f));

        DrawHudTimerText(centerX, FormatHudTimerText(_world.MatchState.TimeRemainingTicks));
    }

    private string FormatHudTimerText(int timeRemainingTicks)
    {
        var totalSeconds = (int)MathF.Ceiling(Math.Max(0, timeRemainingTicks) / (float)_config.TicksPerSecond);
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return minutes.ToString(CultureInfo.InvariantCulture) + ":" + seconds.ToString("00", CultureInfo.InvariantCulture);
    }

    private void DrawHudTimerText(float centerX, string timeText, float timerTextY = 20f)
    {
        DrawTimerFontTextRightAligned(timeText, new Vector2(centerX + 20f, timerTextY), Color.White, 1f);
    }

    private void DrawKillFeedHud()
    {
        const float rowHeight = 20f;
        var viewportWidth = ViewportWidth;
        var y = 59f;
        KillFeedEntry? previousEntry = null;
        foreach (var entry in _world.KillFeed)
        {
            if (previousEntry is not null && ShouldSuppressDuplicateKillFeedEntry(previousEntry, entry))
            {
                continue;
            }

            DrawKillFeedEntry(entry, viewportWidth, y);
            y += rowHeight;
            previousEntry = entry;
        }
    }

    private void DrawKillFeedEntry(KillFeedEntry entry, int viewportWidth, float y)
    {
        const float leftPadding = 8f;
        const float rightPadding = 10f;
        const float iconSpacing = 6f;
        var localPlayerId = _world.LocalPlayer.Id;
        var isLocalInvolved = entry.KillerPlayerId == localPlayerId || entry.VictimPlayerId == localPlayerId;
        var hideVictimName = ShouldSuppressKillFeedVictimName(entry);
        var victimName = hideVictimName ? string.Empty : entry.VictimName;
        var killerWidth = string.IsNullOrEmpty(entry.KillerName) ? 0f : MeasureBitmapFontWidth(entry.KillerName, 1f);
        var messageWidth = string.IsNullOrEmpty(entry.MessageText) ? 0f : MeasureBitmapFontWidth(entry.MessageText, 1f);
        var victimWidth = MeasureBitmapFontWidth(victimName, 1f);
        var weaponSprite = _runtimeAssets.GetSprite(entry.WeaponSpriteName);
        var weaponWidth = weaponSprite?.Frames.Count > 0 ? weaponSprite.Frames[0].Width : 0f;
        var hasWeaponIcon = weaponSprite is not null && weaponSprite.Frames.Count > 0;
        var contentWidth = killerWidth
            + (hasWeaponIcon ? weaponWidth + iconSpacing : 0f)
            + messageWidth
            + victimWidth;
        var backgroundWidth = contentWidth + leftPadding + rightPadding;
        var backgroundLeft = viewportWidth - backgroundWidth - 4f;
        var backgroundHeight = Math.Max(16f, MeasureBitmapFontHeight(1f) + 8f);
        DrawInsetHudPanel(
            new Rectangle(
                (int)MathF.Floor(backgroundLeft),
                (int)MathF.Floor(y - 3f),
                (int)MathF.Ceiling(backgroundWidth),
                (int)MathF.Ceiling(backgroundHeight)),
            isLocalInvolved ? new Color(217, 217, 183) : new Color(49, 45, 26),
            isLocalInvolved ? new Color(235, 232, 198) : new Color(68, 61, 38));

        var currentX = backgroundLeft + leftPadding;
        if (!string.IsNullOrEmpty(entry.KillerName))
        {
            DrawBitmapFontText(entry.KillerName, new Vector2(currentX, y), GetKillFeedTextColor(entry.KillerTeam), 1f);
            currentX += killerWidth;
        }

        if (hasWeaponIcon)
        {
            var resolvedWeaponSprite = weaponSprite!;
            var frameIndex = resolvedWeaponSprite.Frames.Count > 1 && isLocalInvolved ? 1 : 0;
            currentX += iconSpacing * 0.5f;
            var iconCenterX = currentX + (weaponWidth / 2f);
            DrawCenteredHudSprite(entry.WeaponSpriteName, frameIndex, new Vector2(iconCenterX, y + 5f), Color.White, Vector2.One);
            currentX += weaponWidth + (iconSpacing * 0.5f);
        }

        if (!string.IsNullOrEmpty(entry.MessageText))
        {
            DrawBitmapFontText(entry.MessageText, new Vector2(currentX, y), isLocalInvolved ? Color.Black : Color.White, 1f);
            currentX += messageWidth;
        }

        if (!string.IsNullOrEmpty(victimName))
        {
            DrawBitmapFontText(victimName, new Vector2(currentX, y), GetKillFeedTextColor(entry.VictimTeam), 1f);
        }
    }

    private static bool ShouldSuppressKillFeedVictimName(KillFeedEntry entry)
    {
        return !string.IsNullOrEmpty(entry.MessageText)
            && string.IsNullOrEmpty(entry.KillerName)
            && string.Equals(entry.WeaponSpriteName, "DeadKL", StringComparison.Ordinal);
    }

    private static bool ShouldSuppressDuplicateKillFeedEntry(KillFeedEntry previousEntry, KillFeedEntry entry)
    {
        return previousEntry.KillerName == entry.KillerName
            && previousEntry.KillerTeam == entry.KillerTeam
            && previousEntry.KillerPlayerId == entry.KillerPlayerId
            && previousEntry.VictimPlayerId == entry.VictimPlayerId
            && previousEntry.WeaponSpriteName == entry.WeaponSpriteName
            && previousEntry.VictimName == entry.VictimName
            && previousEntry.VictimTeam == entry.VictimTeam
            && previousEntry.MessageText == entry.MessageText
            && previousEntry.SpecialType == entry.SpecialType;
    }

    private static Color GetKillFeedTextColor(PlayerTeam team)
    {
        return team == PlayerTeam.Blue
            ? new Color(100, 116, 132)
            : new Color(171, 78, 70);
    }

    private void DrawDeathCamHud()
    {
        if (!_killCamEnabled || _world.LocalPlayer.IsAlive || _world.LocalDeathCam is null)
        {
            return;
        }

        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, 100), Color.Black);
        _spriteBatch.Draw(_pixel, new Rectangle(0, viewportHeight - 100, viewportWidth, 100), Color.Black);

        var killerColor = _world.LocalDeathCam.KillerTeam.HasValue
            ? GetKillFeedTextColor(_world.LocalDeathCam.KillerTeam.Value)
            : Color.White;
        DrawHudTextCentered(_world.LocalDeathCam.KillMessage, new Vector2(viewportWidth / 2f, 30f), killerColor, 2f);
        if (!string.IsNullOrEmpty(_world.LocalDeathCam.KillerName))
        {
            DrawHudTextCentered(_world.LocalDeathCam.KillerName, new Vector2(viewportWidth / 2f, 60f), killerColor, 2f);
        }

        if (_world.LocalDeathCam.MaxHealth > 0)
        {
            DrawScreenHealthBar(
                new Rectangle((viewportWidth / 2) - 18, viewportHeight - 68, 36, 36),
                _world.LocalDeathCam.Health,
                _world.LocalDeathCam.MaxHealth,
                false,
                fillDirection: HudFillDirection.VerticalBottomToTop);
            DrawCenteredHudSprite("DeathCamHealthBarS", 0, new Vector2(viewportWidth / 2f, viewportHeight - 50f), Color.White, new Vector2(2f, 2f));
        }
    }

    private void DrawWinBannerHud()
    {
        if (!_world.MatchState.IsEnded)
        {
            return;
        }

        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        var frameIndex = _world.MatchState.WinnerTeam switch
        {
            PlayerTeam.Red => 0,
            PlayerTeam.Blue => 1,
            _ => 2,
        };

        DrawCenteredHudSprite(
            "WinBannerS",
            frameIndex,
            new Vector2(viewportWidth / 2f, viewportHeight / 9f),
            Color.White,
            new Vector2(2f, 2f));
    }

    private void DrawAutoBalanceNotice()
    {
        if (_autoBalanceNoticeTicks <= 0 || string.IsNullOrWhiteSpace(_autoBalanceNoticeText) || _mainMenuOpen)
        {
            return;
        }

        var viewportWidth = ViewportWidth;
        var fadeSeconds = Math.Max(1, _config.TicksPerSecond);
        var alpha = Math.Clamp(_autoBalanceNoticeTicks / (float)fadeSeconds, 0.25f, 1f);
        var color = new Color(245, 210, 120) * alpha;
        DrawHudTextCentered(_autoBalanceNoticeText, new Vector2(viewportWidth / 2f, 80f), color, 1f);
    }

    private void DrawRespawnHud()
    {
        if (_world.LocalDeathCam is not null
            || _world.LocalPlayerAwaitingJoin
            || _world.LocalPlayer.IsAlive)
        {
            return;
        }

        const float respawnTextX = 10f;
        const float respawnTextY = 10f;
        var respawnTextPosition = new Vector2(respawnTextX, respawnTextY - (MeasureBitmapFontHeight(1f) / 2f));
        if (_world.MatchRules.Mode == GameModeKind.Arena && !_world.MatchState.IsEnded)
        {
            DrawHudTextLeftAligned(
                "No Respawning in Arena",
                respawnTextPosition,
                Color.White,
                1f);
            return;
        }

        var respawnSeconds = Math.Max(0f, MathF.Ceiling(_world.LocalPlayerRespawnTicks / (float)_config.TicksPerSecond));
        DrawHudTextLeftAligned(
            $"Respawn in {respawnSeconds:0} second(s).",
            respawnTextPosition,
            Color.White,
            1f);
    }

    private void DrawIntelPanelElement(TeamIntelligenceState intelState, Vector2 position)
    {
        var isEnemyIntelForLocalPlayer = intelState.Team != _world.LocalPlayer.Team;
        var localPlayerCarryingEnemyIntel = isEnemyIntelForLocalPlayer && _world.LocalPlayer.IsCarryingIntel;
        var sourcePosition = _world.LocalPlayer.IsAlive
            ? GetLocalViewPosition()
            : position;
        var sourceX = sourcePosition.X;
        var sourceY = sourcePosition.Y;
        var targetX = intelState.X;
        var targetY = intelState.Y;
        var directionDegrees = MathF.Atan2(targetY - sourceY, targetX - sourceX) * 180f / MathF.PI;
        var statusFrame = localPlayerCarryingEnemyIntel
            ? 3
            : intelState.IsAtBase
                ? 2
                : intelState.IsDropped
                    ? 0
                    : 1;
        var arrowFrame = intelState.Team == PlayerTeam.Blue ? 1 : 0;

        TryDrawScreenSprite(
            "IntelReturnTimeS",
            GetIntelReturnTimerFrameIndex(intelState),
            new Vector2(position.X + (intelState.Team == PlayerTeam.Blue ? -26f : -27f), position.Y - 27f),
            Color.White,
            new Vector2(3f, 3f));
        TryDrawScreenSprite(
            "IntelArrowS",
            arrowFrame,
            position,
            Color.White,
            new Vector2(3f, 3f),
            directionDegrees * (MathF.PI / 180f));
        TryDrawScreenSprite(
            "IntelStatusS",
            statusFrame,
            position,
            Color.White,
            new Vector2(2f, 2f));
    }

    private static int GetIntelReturnTimerFrameIndex(TeamIntelligenceState intelState)
    {
        if (!intelState.IsDropped)
        {
            return intelState.Team == PlayerTeam.Blue ? 16 : 33;
        }

        const float totalReturnTicks = 900f;
        var frame = Math.Clamp((int)MathF.Floor((intelState.ReturnTicksRemaining / totalReturnTicks) * 17f), 1, 17);
        return intelState.Team == PlayerTeam.Blue
            ? frame
            : Math.Clamp(frame + 17, 18, 33);
    }
}
