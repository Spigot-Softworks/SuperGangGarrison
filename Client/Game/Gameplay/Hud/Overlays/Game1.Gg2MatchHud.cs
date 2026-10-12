#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>
/// GG2-mode match HUD, ported from GG2 Modern's Draw events: draw_generictimer /
/// draw_roundtimer, drawControlPointStatus, CTFHUD, KothHUD / DKothHUD, ArenaHUD,
/// GeneratorHUD, TeamDeathmatchHUD and KillLog. Positions are GG2's 800x600 view
/// numbers; the movable HUD elements start at GG2's spots (see HudLayoutDefaults.CreateGg2)
/// and can still be moved and scaled.
/// </summary>
public partial class Game1
{
    private static readonly Color Gg2KillLogInvolvedColor = new(217, 217, 183);
    private static readonly Color Gg2KillLogOtherColor = new(49, 45, 26);
    private static readonly Color Gg2KillLogRedText = new(171, 78, 70);
    private static readonly Color Gg2KillLogBlueText = new(100, 116, 132);
    private static readonly Color Gg2HudBarColor = new(217, 217, 183);

    /// <summary>GG2's format_timer_value: ceil(ticks / 30) as m:ss.</summary>
    private string FormatGg2TimerValue(int ticks)
    {
        var totalSeconds = (int)MathF.Ceiling(Math.Max(0, ticks) / (float)Math.Max(1, _config.TicksPerSecond));
        return (totalSeconds / 60).ToString(CultureInfo.InvariantCulture)
            + ":"
            + (totalSeconds % 60).ToString("00", CultureInfo.InvariantCulture);
    }

    private int GetGg2LocalTimerTeamOffset()
    {
        return _world.LocalPlayerTeam == PlayerTeam.Blue ? 1 : 0;
    }

    /// <summary>
    /// draw_roundtimer → draw_generictimer mode 0: the big top-centre timer, with GG2's
    /// "Setup" line during control point setup. Hidden once the round is won (WinBanner).
    /// </summary>
    private void DrawGg2RoundTimer(float centerX, float centerY = 30f)
    {
        if (_world.MatchState.IsEnded)
        {
            return;
        }

        var teamOffset = GetGg2LocalTimerTeamOffset();
        var hudPosition = new Vector2(centerX, centerY);
        if (_world.MatchState.IsOvertime)
        {
            TryDrawScreenSprite("TimerHudS", 2 + teamOffset, hudPosition, Color.White, new Vector2(3f, 3f));
            DrawHudTextCentered("OVERTIME", hudPosition, Color.White, 1f);
            return;
        }

        TryDrawScreenSprite("TimerHudS", teamOffset, hudPosition, Color.White, new Vector2(3f, 3f));
        var setup = _world.ControlPointSetupActive;
        int ticks;
        int tickLimit;
        float textY;
        if (setup)
        {
            ticks = _world.ControlPointSetupTicksRemaining;
            tickLimit = Math.Max(1, _world.ObjectiveRules.ControlPointSetupDurationTicks);
            textY = 27f;
        }
        else
        {
            ticks = _world.MatchState.TimeRemainingTicks;
            tickLimit = Math.Max(1, _world.MatchRules.TimeLimitTicks);
            textY = 32f;
        }

        var frame = Math.Clamp((int)MathF.Floor(12f * ticks / tickLimit), 0, 12);
        TryDrawScreenSprite("TimerS", frame, new Vector2(centerX + 39f, centerY), Color.White, new Vector2(3f, 3f));
        DrawTimerFontTextRightAlignedCenteredY(FormatGg2TimerValue(ticks), new Vector2(centerX + 20f, textY + centerY - 30f), Color.White, 1f);
        if (setup)
        {
            DrawHudTextCentered("Setup", new Vector2(centerX - 3f, centerY + 10f), Color.White, 1f);
        }
    }

    /// <summary>
    /// draw_generictimer modes 1 and 2: the small 2x timer KOTH uses per team, outlined
    /// while that team holds its point. At or below zero it reads OVERTIME.
    /// </summary>
    private void DrawGg2SmallTimer(Vector2 position, float scale, int ticks, int teamOffset, bool outlined)
    {
        var spriteScale = new Vector2(2f * scale, 2f * scale);
        if (outlined)
        {
            TryDrawScreenSprite("TimerOutlineS", 0, position, Color.White, spriteScale);
        }

        TryDrawScreenSprite("TimerHudS", 2 + teamOffset, position, Color.White, spriteScale);
        if (ticks <= 0)
        {
            DrawHudTextCentered("OVERTIME", position + new Vector2(0f, 2f * scale), Color.White, scale);
            return;
        }

        DrawTimerFontTextRightAlignedCenteredY(FormatGg2TimerValue(ticks), position + new Vector2(24f * scale, 2f * scale), Color.White, scale);
    }

    private readonly record struct Gg2ControlPointIcon(int Frame, bool Locked, int Cappers);

    private static int GetGg2ControlPointFrame(PlayerTeam? owner, PlayerTeam? cappingTeam, float cappingTicks, int capTimeTicks)
    {
        int teamOffset;
        if (owner == PlayerTeam.Red)
        {
            teamOffset = 60;
        }
        else if (owner == PlayerTeam.Blue)
        {
            teamOffset = 90;
        }
        else
        {
            teamOffset = cappingTeam == PlayerTeam.Red ? 30 : 0;
        }

        if (cappingTicks != 0f && capTimeTicks > 0)
        {
            return teamOffset + Math.Clamp((int)MathF.Floor(cappingTicks / capTimeTicks * 30f), 0, 29);
        }

        return teamOffset;
    }

    /// <summary>
    /// drawControlPointStatus: the row of point icons centred on the objective element,
    /// 60 px apart at 3x. A locked point shows the lock, or its unlock countdown in its
    /// last five seconds.
    /// </summary>
    private void DrawGg2ControlPointRow(IReadOnlyList<Gg2ControlPointIcon> icons, int unlockTicks)
    {
        if (icons.Count == 0 || !TryResolveHudElement(HudElementId.MatchObjectiveStatus, out var resolved))
        {
            return;
        }

        var origin = resolved.Origin;
        var scale = resolved.Layout.Scale;
        var spriteScale = new Vector2(3f * scale, 3f * scale);
        var drawX = origin.X - ((icons.Count - 1) / 2f * 60f * scale);
        var firstX = drawX;
        var unlockFiveSeconds = Math.Max(1, _config.TicksPerSecond) * 5;
        foreach (var icon in icons)
        {
            var position = new Vector2(drawX, origin.Y);
            TryDrawScreenSprite("ControlPointStatusS", icon.Frame, position, Color.White, spriteScale);
            if (icon.Locked)
            {
                if (unlockTicks >= unlockFiveSeconds || unlockTicks <= 0)
                {
                    TryDrawScreenSprite("ControlPointLockS", 0, position, Color.White, spriteScale);
                }
                else
                {
                    var seconds = (int)MathF.Ceiling(unlockTicks / (float)Math.Max(1, _config.TicksPerSecond));
                    DrawHudTextCentered(seconds.ToString(CultureInfo.InvariantCulture), position + new Vector2(2f * scale, 2f * scale), Color.White, 3f * scale);
                }
            }
            else if (icon.Cappers > 0)
            {
                TryDrawScreenSprite("ControlPointCappersS", 0, position, Color.White, spriteScale);
                DrawHudTextCentered(icon.Cappers.ToString(CultureInfo.InvariantCulture), position + new Vector2(13f * scale, 2f * scale), Color.Black, 1.5f * scale);
            }

            drawX += 60f * scale;
        }

        UpdateHudElementBounds(
            HudElementId.MatchObjectiveStatus,
            new Rectangle(
                (int)MathF.Round(firstX - (24f * scale)),
                (int)MathF.Round(origin.Y - (24f * scale)),
                Math.Max(1, (int)MathF.Round((drawX - 60f * scale - firstX) + (48f * scale))),
                Math.Max(1, (int)MathF.Round(48f * scale))));
    }

    private List<Gg2ControlPointIcon> CollectGg2ControlPointIcons()
    {
        var icons = new List<Gg2ControlPointIcon>(_world.ControlPoints.Count);
        foreach (var point in _world.ControlPoints)
        {
            icons.Add(new Gg2ControlPointIcon(
                GetGg2ControlPointFrame(point.Team, point.CappingTeam, point.CappingTicks, point.CapTimeTicks),
                point.IsLocked,
                point.Cappers));
        }

        return icons;
    }

    /// <summary>ControlPointHUD: the point row (locks never count down) and the round timer.</summary>
    private void DrawGg2ControlPointHud()
    {
        DrawGg2ControlPointRow(CollectGg2ControlPointIcons(), unlockTicks: 0);
        DrawGg2RoundTimer(ViewportWidth / 2f);
    }

    /// <summary>
    /// KothHUD / DKothHUD: the point row with its unlock countdown and one small timer per
    /// team at the top (no round timer), each outlined while that team holds its point.
    /// </summary>
    private void DrawGg2KothHud()
    {
        DrawGg2ControlPointRow(CollectGg2ControlPointIcons(), _world.ObjectiveRules.KothUnlockTicksRemaining);
        if (_world.MatchState.IsEnded)
        {
            return;
        }

        var isConquest = _world.MatchRules.Mode == GameModeKind.Conquest;
        DrawGg2KothTeamTimer(HudElementId.MatchKothRedTimer, PlayerTeam.Red, isConquest ? _world.RedCaps : _world.ObjectiveRules.KothRedTimerTicksRemaining);
        DrawGg2KothTeamTimer(HudElementId.MatchKothBlueTimer, PlayerTeam.Blue, isConquest ? _world.BlueCaps : _world.ObjectiveRules.KothBlueTimerTicksRemaining);
        if (isConquest)
        {
            DrawHudTextCentered($"First to {ConquestRules.ScoreLimit}", new Vector2(ViewportWidth / 2f, 64f), Color.White, 1f);
            DrawGg2RoundTimer(ViewportWidth / 2f, centerY: 100f);
        }
    }

    private void DrawGg2KothTeamTimer(string elementId, PlayerTeam team, int ticks)
    {
        var center = new Vector2(ViewportWidth / 2f + (team == PlayerTeam.Red ? -Gg2KothTimerOffsetX : Gg2KothTimerOffsetX), 36f);
        var scale = 1f;
        if (TryResolveHudElement(elementId, out var resolved))
        {
            center = resolved.Origin;
            scale = resolved.Layout.Scale;
            UpdateHudElementBounds(
                elementId,
                new Rectangle(
                    (int)MathF.Round(center.X - (46f * scale)),
                    (int)MathF.Round(center.Y - (18f * scale)),
                    Math.Max(1, (int)MathF.Round(92f * scale)),
                    Math.Max(1, (int)MathF.Round(36f * scale))));
        }

        if (_world.MatchRules.Mode == GameModeKind.Conquest)
        {
            var spriteScale = new Vector2(2f * scale);
            if (IsKothTimerActive(team))
            {
                TryDrawScreenSprite("TimerOutlineS", 0, center, Color.White, spriteScale);
            }

            TryDrawScreenSprite("TimerHudS", team == PlayerTeam.Blue ? 3 : 2, center, Color.White, spriteScale);
            DrawTimerFontTextRightAlignedCenteredY(ticks.ToString(CultureInfo.InvariantCulture), center + new Vector2(24f * scale, 2f * scale), Color.White, scale);
            return;
        }

        DrawGg2SmallTimer(center, scale, ticks, team == PlayerTeam.Blue ? 1 : 0, IsKothTimerActive(team));
    }

    /// <summary>GG2's default KOTH timer spacing: 43 * 1.15 either side of centre.</summary>
    internal const float Gg2KothTimerOffsetX = 43f * 1.15f;

    /// <summary>
    /// ArenaHUD: the single point (counting down its unlock), the round timer, and the
    /// alive-player counters under the timer.
    /// </summary>
    private void DrawGg2ArenaHud()
    {
        var centerX = ViewportWidth / 2f;
        var icons = new[]
        {
            new Gg2ControlPointIcon(
                GetGg2ControlPointFrame(_world.ArenaPointTeam, _world.ArenaCappingTeam, _world.ArenaCappingTicks, Math.Max(1, _world.ArenaPointCapTimeTicks)),
                _world.ArenaPointLocked,
                _world.ArenaCappers),
        };
        DrawGg2ControlPointRow(icons, _world.ArenaUnlockTicksRemaining);
        DrawGg2RoundTimer(centerX);

        if (_world.LocalPlayerAwaitingJoin)
        {
            return;
        }

        TryDrawScreenSprite("ArenaPlayerCountS", 0, new Vector2(centerX, 71f), Color.White, new Vector2(2f, 2f));
        DrawHudTextCentered(_world.ArenaRedAliveCount.ToString(CultureInfo.InvariantCulture), new Vector2(centerX + 15f, 73f), Color.Black, 1f);
        TryDrawScreenSprite("ArenaPlayerCountS", 1, new Vector2(centerX, 104f), Color.White, new Vector2(2f, 2f));
        DrawHudTextCentered(_world.ArenaBlueAliveCount.ToString(CultureInfo.InvariantCulture), new Vector2(centerX + 15f, 106f), Color.Black, 1f);
    }

    /// <summary>
    /// GeneratorHUD: the panel at 2x, each team's generator at 1x either side with its
    /// health bar above. (GG2's shield bar below needs generator shields, which the GG2
    /// ruleset brings.)
    /// </summary>
    private void DrawGg2GeneratorHud()
    {
        var centerX = ViewportWidth / 2f;
        var hudY = ViewportHeight - 50f;
        TryDrawScreenSprite("GeneratorHUDS", 0, new Vector2(centerX, hudY), Color.White, new Vector2(2f, 2f));
        DrawGg2GeneratorSide(_world.ObjectiveRules.GetGenerator(PlayerTeam.Red), new Vector2(centerX - 50f, hudY), centerX - 77f);
        DrawGg2GeneratorSide(_world.ObjectiveRules.GetGenerator(PlayerTeam.Blue), new Vector2(centerX + 50f, hudY), centerX + 23f);
        DrawGg2RoundTimer(centerX);
    }

    private void DrawGg2GeneratorSide(GeneratorState? generator, Vector2 position, float barLeft)
    {
        if (generator is null)
        {
            return;
        }

        var spriteName = generator.Team == PlayerTeam.Blue ? "GeneratorBlueS" : "GeneratorRedS";
        TryDrawScreenSprite(spriteName, GetGeneratorAnimationFrame(generator), position, Color.White, Vector2.One);
        DrawScreenHealthBar(
            new Rectangle((int)MathF.Round(barLeft), (int)MathF.Round(ViewportHeight - 75f), 52, 7),
            generator.Health,
            Math.Max(1, generator.MaxHealth),
            useTeamColors: false,
            fillColor: Gg2HudBarColor,
            backColor: Color.Black);
    }

    /// <summary>
    /// TeamDeathmatchHUD: the panel at 3x on the bottom edge, kills at ±width/3.5, the
    /// kill limit under them, all in black at 2x; then the round timer.
    /// </summary>
    private void DrawGg2TeamDeathmatchHud()
    {
        var panelOrigin = new Vector2(ViewportWidth / 2f, ViewportHeight);
        var panelScale = 1f;
        if (TryResolveHudElement(HudElementId.MatchCtfPanel, out var panel))
        {
            panelOrigin = panel.Origin;
            panelScale = panel.Layout.Scale;
        }

        var sprite = ResolveTeamDeathmatchScorePanelSprite();
        var width = (sprite?.Frames.Count > 0 ? sprite.Frames[0].Width : 112) * 3f * panelScale;
        var height = (sprite?.Frames.Count > 0 ? sprite.Frames[0].Height : 21) * 3f * panelScale;
        if (TryDrawTeamDeathmatchScorePanelSprite(panelOrigin, panelScale, out var bounds))
        {
            UpdateHudElementBounds(HudElementId.MatchCtfPanel, bounds);
        }

        var scoreY = panelOrigin.Y - (height / 2f) + (6f * panelScale);
        DrawHudTextCentered(_world.RedCaps.ToString(CultureInfo.InvariantCulture), new Vector2(panelOrigin.X - (width / 3.5f), scoreY), Color.Black, 2f * panelScale);
        DrawHudTextCentered(_world.BlueCaps.ToString(CultureInfo.InvariantCulture), new Vector2(panelOrigin.X + (width / 3.5f), scoreY), Color.Black, 2f * panelScale);
        DrawHudTextCentered(_world.MatchRules.CapLimit.ToString(CultureInfo.InvariantCulture), new Vector2(panelOrigin.X, panelOrigin.Y - (14f * panelScale)), Color.Black, 2f * panelScale);
        DrawGg2RoundTimer(ViewportWidth / 2f);
    }

    /// <summary>
    /// KillLog: one row per kill, 20 px apart, right-aligned to the element's edge. Each row
    /// is GG2's notched box (cream when you're involved, dark otherwise), the killer's name
    /// in team colour, the weapon icon, any special text, then the victim.
    /// </summary>
    private void DrawGg2KillFeedHud()
    {
        if (!TryResolveHudElement(HudElementId.MatchKillFeed, out var resolved))
        {
            return;
        }

        var scale = resolved.Layout.Scale;
        var right = resolved.Origin.X;
        var top = resolved.Origin.Y;
        var localPlayerId = GetResolvedLocalPlayerId();
        var minX = right - (340f * scale);
        var row = 0;
        KillFeedEntry? previousEntry = null;
        foreach (var entry in _world.KillFeedEntries)
        {
            if (previousEntry is not null && ShouldSuppressDuplicateKillFeedEntry(previousEntry, entry))
            {
                continue;
            }

            previousEntry = entry;
            var involved = IsLocalPlayerInKillFeedEntry(entry, localPlayerId);
            var killer = entry.KillerName ?? string.Empty;
            var victim = ShouldSuppressKillFeedVictimName(entry) ? string.Empty : entry.VictimName ?? string.Empty;
            var special = entry.MessageText ?? string.Empty;
            var weaponSpriteName = ResolveKillFeedWeaponSpriteName(entry.WeaponSpriteName);
            var weaponSprite = string.IsNullOrEmpty(weaponSpriteName) ? null : GetResolvedSprite(weaponSpriteName);
            var weaponWidth = weaponSprite is { Frames.Count: > 0 } ? weaponSprite.Frames[0].Width * scale : 0f;
            float Width(string text) => string.IsNullOrEmpty(text) ? 0f : MeasureBitmapFontWidth(text, scale);
            var allWidth = Width(killer + victim + special);
            var rowCenterY = top + ((row * 20f + 24f) * scale);
            var rowTop = top + ((row * 20f + 14f) * scale);
            var boxColor = involved ? Gg2KillLogInvolvedColor : Gg2KillLogOtherColor;

            var outerLeft = right - allWidth - (12f * scale) - weaponWidth;
            _spriteBatch.Draw(_pixel, ToGg2Rectangle(outerLeft, rowTop, right - (8f * scale), top + ((row * 20f + 32f) * scale)), boxColor);
            _spriteBatch.Draw(_pixel, ToGg2Rectangle(outerLeft - (2f * scale), top + ((row * 20f + 16f) * scale), right - (6f * scale), top + ((row * 20f + 30f) * scale)), boxColor);
            minX = MathF.Min(minX, outerLeft - (2f * scale));

            var textHalfHeight = MeasureBitmapFontHeight(scale) / 2f;
            if (killer.Length > 0)
            {
                DrawBitmapFontText(killer, new Vector2(outerLeft, rowCenterY - textHalfHeight), GetGg2KillLogTeamColor(entry.KillerTeam), scale);
            }

            if (weaponSprite is { Frames.Count: > 0 })
            {
                var frame = weaponSprite.Frames.Count > 1 && involved ? 1 : 0;
                var iconX = MathF.Floor(right - Width(victim + special) - (10f * scale) - (weaponWidth / 2f));
                TryDrawScreenSprite(weaponSpriteName, frame, new Vector2(iconX, MathF.Floor(rowCenterY)), Color.White, new Vector2(scale, scale));
            }

            if (special.Length > 0)
            {
                DrawBitmapFontText(special, new Vector2(right - Width(victim + special) - (8f * scale), rowCenterY - textHalfHeight), involved ? Color.Black : Color.White, scale);
            }

            if (victim.Length > 0)
            {
                DrawBitmapFontText(victim, new Vector2(right - Width(victim) - (8f * scale), rowCenterY - textHalfHeight), GetGg2KillLogTeamColor(entry.VictimTeam), scale);
            }

            row += 1;
        }

        UpdateHudElementBounds(
            HudElementId.MatchKillFeed,
            new Rectangle(
                (int)MathF.Round(minX),
                (int)MathF.Round(top + (14f * scale)),
                Math.Max(1, (int)MathF.Round(right - minX)),
                Math.Max(1, (int)MathF.Round(Math.Max(1, row) * 20f * scale))));
    }

    private static Color GetGg2KillLogTeamColor(PlayerTeam? team)
    {
        return team switch
        {
            PlayerTeam.Red => Gg2KillLogRedText,
            PlayerTeam.Blue => Gg2KillLogBlueText,
            _ => Color.White,
        };
    }

    private static Rectangle ToGg2Rectangle(float left, float top, float right, float bottom)
    {
        var x = (int)MathF.Floor(left);
        var y = (int)MathF.Floor(top);
        return new Rectangle(x, y, Math.Max(1, (int)MathF.Ceiling(right) - x), Math.Max(1, (int)MathF.Ceiling(bottom) - y));
    }
}
