#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly record struct ScoreboardSpectatorToken(string Text, ulong BadgeMask);
    private sealed record ScoreboardSpectatorLine(string Prefix, List<ScoreboardSpectatorToken> Tokens);

    private void DrawScoreboardHud()
    {
        if (_scoreboardAlpha <= 0.02f)
        {
            return;
        }

        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        var alpha = Math.Clamp(_scoreboardAlpha, 0.02f, 0.99f);
        var redTeam = GetScoreboardPlayers(PlayerTeam.Red);
        var blueTeam = GetScoreboardPlayers(PlayerTeam.Blue);
        var isKothMode = _world.MatchRules.Mode is GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill;
        var redCenterValue = _world.MatchRules.Mode == GameModeKind.Arena ? _world.ArenaRedConsecutiveWins : _world.RedCaps;
        var blueCenterValue = _world.MatchRules.Mode == GameModeKind.Arena ? _world.ArenaBlueConsecutiveWins : _world.BlueCaps;
        var redCenterText = isKothMode
            ? FormatHudTimerText(_world.KothRedTimerTicksRemaining)
            : redCenterValue.ToString(CultureInfo.InvariantCulture);
        var blueCenterText = isKothMode
            ? FormatHudTimerText(_world.KothBlueTimerTicksRemaining)
            : blueCenterValue.ToString(CultureInfo.InvariantCulture);
        var serverLabel = _networkClient.IsConnected
            ? _networkClient.ServerDescription ?? "Connected"
            : "Offline";
        var serverMetaLabel = TruncateScoreboardMetaText(serverLabel, 25);
        var mapMetaLabel = TruncateScoreboardMetaText(_world.Level.Name, 25);

        var panelWidth = Math.Clamp(viewportWidth - 48, 600, 1040);
        var panelHeight = Math.Clamp(viewportHeight - 44, 360, 640);
        var spectatorLines = BuildScoreboardSpectatorLines(panelWidth - 32f, 0.9f);
        var footerLineHeight = MeasureBitmapFontHeight(0.9f) + 2f;
        var footerHeight = 12f + (footerLineHeight * Math.Max(1, spectatorLines.Count));
        var panel = new Rectangle((viewportWidth - panelWidth) / 2, (viewportHeight - panelHeight) / 2, panelWidth, panelHeight);
        var topBannerHeight = 52;
        var topMargin = 10;
        var metaLineHeight = 24;
        var contentGap = 8;
        var contentTop = panel.Y + topMargin + topBannerHeight + contentGap + metaLineHeight + 10;
        var contentBottom = panel.Bottom - footerHeight - 12;
        var teamPanelWidth = (panel.Width - 36) / 2;
        var leftTeamPanel = new Rectangle(panel.X + 12, contentTop, teamPanelWidth, (int)MathF.Max(120f, contentBottom - contentTop));
        var rightTeamPanel = new Rectangle(panel.Right - 12 - teamPanelWidth, contentTop, teamPanelWidth, (int)MathF.Max(120f, contentBottom - contentTop));
        var redBanner = new Rectangle(leftTeamPanel.X, panel.Y + topMargin, leftTeamPanel.Width, topBannerHeight);
        var blueBanner = new Rectangle(rightTeamPanel.X, panel.Y + topMargin, rightTeamPanel.Width, topBannerHeight);

        _spriteBatch.Draw(_pixel, panel, new Color(26, 29, 34) * (alpha * 0.93f));
        DrawScoreboardBorder(panel, new Color(219, 212, 190) * alpha);

        _spriteBatch.Draw(_pixel, redBanner, new Color(153, 83, 79) * alpha);
        _spriteBatch.Draw(_pixel, blueBanner, new Color(92, 117, 140) * alpha);
        DrawScoreboardBorder(redBanner, new Color(230, 220, 196) * alpha);
        DrawScoreboardBorder(blueBanner, new Color(230, 220, 196) * alpha);

        var headerY = panel.Y + topMargin + 12f;
        DrawBitmapFontTextCentered("RED", new Vector2(redBanner.X + 56f, headerY + 9f), Color.White * alpha, 1.7f);
        DrawBitmapFontTextCentered("BLU", new Vector2(blueBanner.Right - 56f, headerY + 9f), Color.White * alpha, 1.7f);
        DrawBitmapFontTextCentered(redCenterText, new Vector2(panel.Center.X - 38f, headerY + 6f), Color.White * alpha, isKothMode ? 1.35f : 2.7f);
        DrawBitmapFontTextCentered(blueCenterText, new Vector2(panel.Center.X + 38f, headerY + 6f), Color.White * alpha, isKothMode ? 1.35f : 2.7f);
        DrawCountFontTextCentered(redTeam.Count.ToString(CultureInfo.InvariantCulture), new Vector2(redBanner.Center.X - 34f, redBanner.Center.Y + 17f), Color.White * alpha, 1f);
        DrawCountFontTextCentered(blueTeam.Count.ToString(CultureInfo.InvariantCulture), new Vector2(blueBanner.Center.X - 34f, blueBanner.Center.Y + 17f), Color.White * alpha, 1f);
        DrawBitmapFontTextCentered($"PLAYER{(redTeam.Count == 1 ? string.Empty : "S")}", new Vector2(redBanner.Center.X + 18f, redBanner.Center.Y + 15f), Color.White * alpha, 1f);
        DrawBitmapFontTextCentered($"PLAYER{(blueTeam.Count == 1 ? string.Empty : "S")}", new Vector2(blueBanner.Center.X + 18f, blueBanner.Center.Y + 15f), Color.White * alpha, 1f);

        var metaY = panel.Y + topMargin + topBannerHeight + 10f;
        DrawBitmapFontText($"Server: {serverMetaLabel}", new Vector2(panel.X + 16f, metaY), Color.White * alpha, 1.04f);
        DrawBitmapFontTextRightAligned($"Map: {mapMetaLabel}", new Vector2(panel.Right - 16f, metaY), Color.White * alpha, 1.04f);

        _spriteBatch.Draw(_pixel, new Rectangle(panel.Center.X - 1, contentTop, 2, (int)(contentBottom - contentTop)), new Color(219, 212, 190) * (alpha * 0.65f));

        DrawScoreboardTeam(redTeam, leftTeamPanel, PlayerTeam.Red, alpha);
        DrawScoreboardTeam(blueTeam, rightTeamPanel, PlayerTeam.Blue, alpha);

        var spectatorY = panel.Bottom - footerHeight + 2f;
        for (var lineIndex = 0; lineIndex < spectatorLines.Count; lineIndex += 1)
        {
            DrawScoreboardSpectatorLine(
                spectatorLines[lineIndex],
                new Vector2(panel.X + 16f, spectatorY + (footerLineHeight * lineIndex)),
                lineIndex == 0 ? Color.White : new Color(215, 215, 215),
                alpha,
                0.9f);
        }
    }

    private List<PlayerEntity> GetScoreboardPlayers(PlayerTeam team)
    {
        var players = new List<PlayerEntity>();
        if (_networkClient.IsConnected)
        {
            foreach (var (slot, player) in _world.EnumerateReplicatedNetworkPlayers())
            {
                if (_world.IsNetworkPlayerAwaitingJoin(slot) || player.Team != team)
                {
                    continue;
                }

                players.Add(player);
            }
        }
        else
        {
            foreach (var player in EnumerateRenderablePlayers())
            {
                if (player.Team == team)
                {
                    players.Add(player);
                }
            }
        }

        players.Sort((left, right) =>
        {
            var scoreCompare = right.Points.CompareTo(left.Points);
            if (scoreCompare != 0)
            {
                return scoreCompare;
            }

            return string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
        });
        return players;
    }

    private void DrawScoreboardTeam(List<PlayerEntity> players, Rectangle panel, PlayerTeam team, float alpha)
    {
        var teamColor = team == PlayerTeam.Red ? new Color(225, 110, 103) : new Color(94, 170, 255);
        var rowHeight = 29;
        var iconX = panel.X + 14f;
        var nameX = panel.X + 34f;
        var deadX = panel.Right - 14f;
        var relationX = deadX - 19f;
        var dominationX = relationX - 38f;
        var pointsRight = dominationX - 12f;
        var dividerY = panel.Y + 24;
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, dividerY, panel.Width, 2), new Color(219, 212, 190) * (alpha * 0.6f));

        for (var index = 0; index < players.Count && index < 12; index += 1)
        {
            var player = players[index];
            var rowY = dividerY + 8f + (rowHeight * index);
            var isLocalPlayer = ReferenceEquals(player, _world.LocalPlayer);

            if (isLocalPlayer)
            {
                var highlightRectangle = new Rectangle(panel.X + 2, (int)(rowY - 2f), panel.Width - 4, rowHeight - 2);
                _spriteBatch.Draw(_pixel, highlightRectangle, teamColor * (alpha * 0.16f));
            }

            if (!_networkClient.IsSpectator && _world.LocalPlayer.Team == player.Team)
            {
                TryDrawScreenSprite("Icon", GetScoreboardIconFrame(player.ClassId), new Vector2(iconX, rowY + 8f), Color.White * alpha, Vector2.One);
                TryDrawScreenSprite("Icon", GetScoreboardIconFrame(player.ClassId), new Vector2(iconX, rowY + 8f), teamColor * (alpha * 0.2f), Vector2.One);
            }

            const float badgeScale = 1f;
            var badgeWidth = MeasureScoreboardBadgeWidth(player.BadgeMask, badgeScale);
            var nameMaxWidth = Math.Max(24f, pointsRight - nameX - 12f);
            var displayName = TrimBitmapMenuText(
                SanitizeScoreboardText(player.DisplayName),
                Math.Max(24f, nameMaxWidth - badgeWidth),
                1.1f);
            DrawScoreboardNameWithBadges(displayName, player.BadgeMask, new Vector2(nameX, rowY), teamColor, alpha, 1.1f, badgeScale);
            DrawBitmapFontTextRightAligned(MathF.Floor(player.Points).ToString(CultureInfo.InvariantCulture), new Vector2(pointsRight, rowY), Color.White * alpha, 1.1f);
            DrawScoreboardDominationBadges(player, team, rowY, alpha, dominationX, relationX);

            if (!player.IsAlive)
            {
                TryDrawScreenSprite("DeadS", 0, new Vector2(deadX, rowY + 9f), Color.White * alpha, Vector2.One);
            }
        }
    }

    private void DrawScoreboardDominationBadges(PlayerEntity player, PlayerTeam team, float rowY, float alpha, float dominationX, float relationX)
    {
        if (player.ActiveDominationCount > 0)
        {
            TryDrawScreenSprite("MedalS", 0, new Vector2(dominationX, rowY + 8f), Color.White * alpha, Vector2.One);
            DrawBitmapFontText(
                player.ActiveDominationCount.ToString(CultureInfo.InvariantCulture),
                new Vector2(dominationX + 16f, rowY),
                new Color(227, 226, 225) * alpha,
                1f);
        }

        if (player.IsDominatedByLocalViewer)
        {
            TryDrawScreenSprite("MedalS", 3, new Vector2(relationX, rowY + 8f), Color.White * alpha, Vector2.One);
            return;
        }

        if (player.IsDominatingLocalViewer)
        {
            var frameIndex = team == PlayerTeam.Red ? 5 : 6;
            TryDrawScreenSprite("MedalS", frameIndex, new Vector2(relationX, rowY + 8f), Color.White * alpha, Vector2.One);
        }
    }

    private List<ScoreboardSpectatorLine> BuildScoreboardSpectatorLines(float maxWidth, float scale)
    {
        var lines = new List<ScoreboardSpectatorLine>();
        var spectatorCount = Math.Max(0, _world.SpectatorCount);
        var prefix = $"{spectatorCount} spectator(s):";
        if (_world.Spectators.Count == 0)
        {
            lines.Add(new ScoreboardSpectatorLine(prefix, []));
            return lines;
        }

        var currentLine = new ScoreboardSpectatorLine(prefix + " ", []);
        var currentWidth = MeasureBitmapFontWidth(currentLine.Prefix, scale);
        for (var index = 0; index < _world.Spectators.Count; index += 1)
        {
            var spectator = _world.Spectators[index];
            var suffix = index < _world.Spectators.Count - 1 ? ", " : string.Empty;
            var token = new ScoreboardSpectatorToken(
                FormatScoreboardSpectatorLabel(spectator) + suffix,
                spectator.BadgeMask);
            var tokenWidth = MeasureScoreboardNameWithBadges(token.Text, token.BadgeMask, scale, scale);
            if (currentLine.Tokens.Count == 0 || currentWidth + tokenWidth <= maxWidth)
            {
                currentLine.Tokens.Add(token);
                currentWidth += tokenWidth;
                continue;
            }

            lines.Add(currentLine);
            currentLine = new ScoreboardSpectatorLine(string.Empty, [token]);
            currentWidth = tokenWidth;
        }

        if (!string.IsNullOrWhiteSpace(currentLine.Prefix) || currentLine.Tokens.Count > 0)
        {
            lines.Add(currentLine);
        }

        return lines;
    }

    private void DrawScoreboardSpectatorLine(ScoreboardSpectatorLine line, Vector2 position, Color color, float alpha, float scale)
    {
        var cursorX = position.X;
        if (!string.IsNullOrEmpty(line.Prefix))
        {
            DrawBitmapFontText(line.Prefix, position, color * alpha, scale);
            cursorX += MeasureBitmapFontWidth(line.Prefix, scale);
        }

        for (var index = 0; index < line.Tokens.Count; index += 1)
        {
            cursorX = DrawScoreboardNameWithBadges(
                line.Tokens[index].Text,
                line.Tokens[index].BadgeMask,
                new Vector2(cursorX, position.Y),
                color,
                alpha,
                scale,
                scale);
        }
    }

    private float DrawScoreboardNameWithBadges(
        string text,
        ulong badgeMask,
        Vector2 position,
        Color textColor,
        float alpha,
        float textScale,
        float badgeScale)
    {
        var cursorX = position.X;
        var badgeAdvance = GetScoreboardBadgeAdvance(badgeScale);
        if (badgeAdvance > 0f)
        {
            foreach (var badgeIndex in BadgeCatalog.EnumerateBadgeIndices(badgeMask))
            {
                if (TryDrawScreenSprite("HaxxyBadgeS", badgeIndex, new Vector2(cursorX, position.Y - 1f), Color.White * alpha, new Vector2(badgeScale, badgeScale)))
                {
                    cursorX += badgeAdvance;
                }
            }
        }

        DrawBitmapFontText(text, new Vector2(cursorX, position.Y), textColor * alpha, textScale);
        return cursorX + MeasureBitmapFontWidth(text, textScale);
    }

    private float MeasureScoreboardNameWithBadges(string text, ulong badgeMask, float textScale, float badgeScale)
    {
        return MeasureBitmapFontWidth(text, textScale) + MeasureScoreboardBadgeWidth(badgeMask, badgeScale);
    }

    private float MeasureScoreboardBadgeWidth(ulong badgeMask, float badgeScale)
    {
        return BadgeCatalog.CountBadges(badgeMask) * GetScoreboardBadgeAdvance(badgeScale);
    }

    private float GetScoreboardBadgeAdvance(float badgeScale)
    {
        try
        {
            var sprite = _runtimeAssets.GetSprite("HaxxyBadgeS");
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return 0f;
            }

            return sprite.Frames[0].Width * badgeScale;
        }
        catch
        {
            return 0f;
        }
    }

    private static string FormatScoreboardSpectatorLabel(ScoreboardSpectatorEntry spectator)
    {
        var label = SanitizeScoreboardText(spectator.DisplayName);
        return spectator.IsAwaitingJoin ? $"[{label}]" : label;
    }

    private static string SanitizeScoreboardText(string text)
    {
        return string.IsNullOrEmpty(text)
            ? string.Empty
            : text.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    }

    private static string TruncateScoreboardMetaText(string text, int maximumLength)
    {
        var sanitized = SanitizeScoreboardText(text);
        if (maximumLength <= 0 || sanitized.Length <= maximumLength)
        {
            return sanitized;
        }

        return sanitized[..maximumLength];
    }

    private void DrawScoreboardBorder(Rectangle rectangle, Color color)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, 2), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Bottom - 2, rectangle.Width, 2), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, 2, rectangle.Height), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.Right - 2, rectangle.Y, 2, rectangle.Height), color);
    }

    private static int GetScoreboardIconFrame(PlayerClass playerClass)
    {
        return playerClass switch
        {
            PlayerClass.Scout => 0,
            PlayerClass.Soldier => 1,
            PlayerClass.Sniper => 2,
            PlayerClass.Demoman => 3,
            PlayerClass.Medic => 4,
            PlayerClass.Engineer => 5,
            PlayerClass.Heavy => 6,
            PlayerClass.Spy => 7,
            PlayerClass.Pyro => 8,
            _ => 0,
        };
    }
}
