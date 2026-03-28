#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
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
        var redCenterValue = _world.MatchRules.Mode == GameModeKind.Arena ? _world.ArenaRedConsecutiveWins : _world.RedCaps;
        var blueCenterValue = _world.MatchRules.Mode == GameModeKind.Arena ? _world.ArenaBlueConsecutiveWins : _world.BlueCaps;
        var serverLabel = _networkClient.IsConnected
            ? _networkClient.ServerDescription ?? "Connected"
            : "Offline";
        var modeLabel = _world.MatchRules.Mode switch
        {
            GameModeKind.Arena => "Arena",
            GameModeKind.ControlPoint => "CP",
            GameModeKind.Generator => "Gen",
            _ => "CTF",
        };

        var panelWidth = Math.Clamp(viewportWidth - 48, 600, 1040);
        var panelHeight = Math.Clamp(viewportHeight - 44, 360, 640);
        var panel = new Rectangle((viewportWidth - panelWidth) / 2, (viewportHeight - panelHeight) / 2, panelWidth, panelHeight);
        var topBannerHeight = 52;
        var topMargin = 10;
        var metaLineHeight = 24;
        var footerHeight = 46;
        var contentGap = 8;
        var contentTop = panel.Y + topMargin + topBannerHeight + contentGap + metaLineHeight + 10;
        var contentBottom = panel.Bottom - footerHeight - 12;
        var teamPanelWidth = (panel.Width - 36) / 2;
        var leftTeamPanel = new Rectangle(panel.X + 12, contentTop, teamPanelWidth, Math.Max(120, contentBottom - contentTop));
        var rightTeamPanel = new Rectangle(panel.Right - 12 - teamPanelWidth, contentTop, teamPanelWidth, Math.Max(120, contentBottom - contentTop));
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
        DrawBitmapFontTextCentered(redCenterValue.ToString(CultureInfo.InvariantCulture), new Vector2(panel.Center.X - 38f, headerY + 6f), Color.White * alpha, 2.7f);
        DrawBitmapFontTextCentered(blueCenterValue.ToString(CultureInfo.InvariantCulture), new Vector2(panel.Center.X + 38f, headerY + 6f), Color.White * alpha, 2.7f);
        DrawCountFontTextCentered(redTeam.Count.ToString(CultureInfo.InvariantCulture), new Vector2(redBanner.Center.X - 34f, redBanner.Center.Y + 17f), Color.White * alpha, 1f);
        DrawCountFontTextCentered(blueTeam.Count.ToString(CultureInfo.InvariantCulture), new Vector2(blueBanner.Center.X - 34f, blueBanner.Center.Y + 17f), Color.White * alpha, 1f);
        DrawBitmapFontTextCentered($"PLAYER{(redTeam.Count == 1 ? string.Empty : "S")}", new Vector2(redBanner.Center.X + 18f, redBanner.Center.Y + 15f), Color.White * alpha, 1f);
        DrawBitmapFontTextCentered($"PLAYER{(blueTeam.Count == 1 ? string.Empty : "S")}", new Vector2(blueBanner.Center.X + 18f, blueBanner.Center.Y + 15f), Color.White * alpha, 1f);

        var metaY = panel.Y + topMargin + topBannerHeight + 10f;
        DrawBitmapFontText($"Server: {serverLabel}", new Vector2(panel.X + 16f, metaY), Color.White * alpha, 1.04f);
        DrawBitmapFontTextRightAligned($"Map: {_world.Level.Name}   Mode: {modeLabel}", new Vector2(panel.Right - 16f, metaY), Color.White * alpha, 1.04f);

        _spriteBatch.Draw(_pixel, new Rectangle(panel.Center.X - 1, contentTop, 2, contentBottom - contentTop), new Color(219, 212, 190) * (alpha * 0.65f));

        DrawScoreboardTeam(redTeam, leftTeamPanel, PlayerTeam.Red, alpha);
        DrawScoreboardTeam(blueTeam, rightTeamPanel, PlayerTeam.Blue, alpha);

        var spectatorCountText = $"{_world.SpectatorCount} spectator(s)";
        DrawBitmapFontText(spectatorCountText, new Vector2(panel.X + 16f, panel.Bottom - footerHeight + 2f), Color.White * alpha, 1f);
        if (_world.SpectatorNames.Count > 0)
        {
            var spectatorNamesText = "Spectators: " + string.Join(", ", _world.SpectatorNames);
            var trimmedSpectatorNames = TrimBitmapMenuText(spectatorNamesText, panel.Width - 32f, 0.9f);
            DrawBitmapFontText(trimmedSpectatorNames, new Vector2(panel.X + 16f, panel.Bottom - 24f), new Color(215, 215, 215) * alpha, 0.9f);
        }
    }

    private List<PlayerEntity> GetScoreboardPlayers(PlayerTeam team)
    {
        var players = new List<PlayerEntity>();
        foreach (var player in EnumerateRenderablePlayers())
        {
            if (player.Team == team)
            {
                players.Add(player);
            }
        }

        players.Sort((left, right) =>
        {
            var scoreCompare = GetScoreboardScore(right).CompareTo(GetScoreboardScore(left));
            if (scoreCompare != 0)
            {
                return scoreCompare;
            }

            return string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
        });
        return players;
    }

    private static int GetScoreboardScore(PlayerEntity player)
    {
        return player.Kills + (2 * player.Caps) + player.HealPoints;
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
        var scoreRight = dominationX - 12f;
        var capsRight = scoreRight - 44f;
        var deathsRight = capsRight - 44f;
        var killsRight = deathsRight - 44f;
        var headerY = panel.Y + 4f;
        DrawBitmapFontText("NAME", new Vector2(nameX, headerY), Color.White * alpha, 1.04f);
        DrawBitmapFontTextRightAligned("K", new Vector2(killsRight, headerY), Color.White * alpha, 1.04f);
        DrawBitmapFontTextRightAligned("D", new Vector2(deathsRight, headerY), Color.White * alpha, 1.04f);
        DrawBitmapFontTextRightAligned("CAP", new Vector2(capsRight, headerY), Color.White * alpha, 1.04f);
        DrawBitmapFontTextRightAligned("SCR", new Vector2(scoreRight, headerY), Color.White * alpha, 1.04f);

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

            TryDrawScreenSprite("Icon", GetScoreboardIconFrame(player.ClassId), new Vector2(iconX, rowY + 8f), Color.White * alpha, Vector2.One);
            if (_world.LocalPlayer.Team == player.Team)
            {
                TryDrawScreenSprite("Icon", GetScoreboardIconFrame(player.ClassId), new Vector2(iconX, rowY + 8f), teamColor * (alpha * 0.2f), Vector2.One);
            }

            DrawBitmapFontText(player.DisplayName, new Vector2(nameX, rowY), teamColor * alpha, 1.1f);
            DrawBitmapFontTextRightAligned(player.Kills.ToString(CultureInfo.InvariantCulture), new Vector2(killsRight, rowY), Color.White * alpha, 1.1f);
            DrawBitmapFontTextRightAligned(player.Deaths.ToString(CultureInfo.InvariantCulture), new Vector2(deathsRight, rowY), Color.White * alpha, 1.1f);
            DrawBitmapFontTextRightAligned(player.Caps.ToString(CultureInfo.InvariantCulture), new Vector2(capsRight, rowY), Color.White * alpha, 1.1f);
            DrawBitmapFontTextRightAligned(GetScoreboardScore(player).ToString(CultureInfo.InvariantCulture), new Vector2(scoreRight, rowY), Color.White * alpha, 1.1f);
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
