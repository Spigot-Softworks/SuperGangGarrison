#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using OpenGarrison.Client.Plugins;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly record struct ScoreboardSpectatorToken(string Text, ulong BadgeMask);
    private sealed record ScoreboardSpectatorLine(string Prefix, List<ScoreboardSpectatorToken> Tokens);
    private readonly record struct ScoreboardLayout(float XOffset, float YOffset, float XSize, float XCenter, Rectangle Bounds);
    private readonly record struct ScoreboardPlayerRow(PlayerEntity Player, byte Slot, bool IsLocal, Rectangle Bounds);

    private ScoreboardPlayerRow? _scoreboardHoveredPlayerRow;

    private void DrawScoreboardHud()
    {
        if (_scoreboardAlpha <= 0.02f)
        {
            return;
        }

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
        var layout = GetScoreboardLayout();
        var xoffset = layout.XOffset;
        var yoffset = layout.YOffset;
        var xsize = layout.XSize;
        var xcenter = layout.XCenter;
        var scoreboardBounds = layout.Bounds;

        if (!TryDrawScreenSprite("Scoreboard", 0, new Vector2(xoffset, yoffset), Color.White * (alpha * 0.8f), Vector2.One))
        {
            _spriteBatch.Draw(_pixel, scoreboardBounds, new Color(26, 29, 34) * (alpha * 0.93f));
        }

        DrawCountFontTextCentered(redTeam.Count.ToString(CultureInfo.InvariantCulture), new Vector2(xcenter - 161f, yoffset + 23f), Color.White * alpha, 1f);
        DrawCountFontTextCentered(blueTeam.Count.ToString(CultureInfo.InvariantCulture), new Vector2(xcenter + 83f, yoffset + 23f), Color.White * alpha, 1f);
        const float scoreboardRedScoreOutwardOffset = 4f;
        const float scoreboardBlueScoreOutwardOffset = 8f;
        if (isKothMode)
        {
            DrawBitmapFontTextCentered(redCenterText, new Vector2(xcenter - 16f - scoreboardRedScoreOutwardOffset, yoffset + 6f), Color.White * alpha, 1.3f);
            DrawBitmapFontTextCentered(blueCenterText, new Vector2(xcenter + 12f + scoreboardBlueScoreOutwardOffset, yoffset + 6f), Color.White * alpha, 1.3f);
        }
        else
        {
            DrawBitmapFontTextCentered(redCenterText, new Vector2(xcenter - 16f - scoreboardRedScoreOutwardOffset, yoffset), Color.White * alpha, 4f);
            DrawBitmapFontTextCentered(blueCenterText, new Vector2(xcenter + 12f + scoreboardBlueScoreOutwardOffset, yoffset), Color.White * alpha, 4f);
        }

        DrawBitmapFontText($"Server: {serverMetaLabel}", new Vector2(xoffset + 8f, yoffset + 48f), Color.White * alpha, 1f);
        DrawBitmapFontText($"    Map: {mapMetaLabel}", new Vector2(xoffset + (xsize / 2f) + 16f, yoffset + 48f), Color.White * alpha, 1f);

        DrawScoreboardTeam(redTeam, PlayerTeam.Red, alpha, xoffset, yoffset, xsize);
        DrawScoreboardTeam(blueTeam, PlayerTeam.Blue, alpha, xoffset, yoffset, xsize);

        var spectatorLines = BuildScoreboardSpectatorLines(525f, 1f);
        var footerLineHeight = MeasureBitmapFontHeight(1f) + 2f;
        var spectatorY = yoffset + 370f;
        for (var lineIndex = 0; lineIndex < spectatorLines.Count; lineIndex += 1)
        {
            DrawScoreboardSpectatorLine(
                spectatorLines[lineIndex],
                new Vector2(xoffset + 5f, spectatorY + (footerLineHeight * lineIndex)),
                Color.White,
                alpha,
                1f);
        }

        if (!OperatingSystem.IsBrowser())
        {
            NotifyClientPluginsScoreboardDraw(
                scoreboardBounds,
                alpha,
                serverMetaLabel,
                mapMetaLabel,
                redTeam.Count,
                blueTeam.Count,
                redCenterText,
                blueCenterText);
        }

        DrawScoreboardHoveredPlayerCard();
    }

    private ScoreboardLayout GetScoreboardLayout()
    {
        var xoffset = (ViewportWidth / 2f) - 280f;
        var yoffset = (ViewportHeight / 2f) - 190f;
        var xsize = 480f;
        return new ScoreboardLayout(
            xoffset,
            yoffset,
            xsize,
            ViewportWidth / 2f,
            new Rectangle(
                (int)MathF.Round(xoffset),
                (int)MathF.Round(yoffset),
                560,
                400));
    }

    private List<PlayerEntity> GetScoreboardPlayers(PlayerTeam team)
    {
        var players = new List<PlayerEntity>();
        if (_networkClient.IsConnected)
        {
            if (!_networkClient.IsSpectator
                && !_world.LocalPlayerAwaitingJoin
                && _world.LocalPlayer.Team == team)
            {
                players.Add(_world.LocalPlayer);
            }

            foreach (var player in EnumerateRemotePlayersForView())
            {
                if (player.Team != team)
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

    private void UpdateScoreboardPlayerCardState(MouseState mouse)
    {
        if (!_scoreboardOpen || _scoreboardAlpha <= 0.02f)
        {
            _scoreboardHoveredPlayerRow = null;
            return;
        }

        var rows = BuildScoreboardPlayerRows();
        if (TryGetScoreboardPlayerRowAtPoint(rows, mouse.Position, out var hoveredRow))
        {
            _scoreboardHoveredPlayerRow = hoveredRow;
        }
        else if (_scoreboardHoveredPlayerRow is { } previousRow
            && TryGetScoreboardPlayerRowBySlot(rows, previousRow.Slot, out var currentRow)
            && GetScoreboardPlayerCardLayout(currentRow.Bounds).Bounds.Contains(mouse.Position))
        {
            _scoreboardHoveredPlayerRow = currentRow;
        }
        else
        {
            _scoreboardHoveredPlayerRow = null;
        }

        if (mouse.LeftButton == ButtonState.Pressed
            && _previousMouse.LeftButton != ButtonState.Pressed)
        {
            TryHandleScoreboardPlayerCardClick(mouse.Position);
        }
    }

    private List<ScoreboardPlayerRow> BuildScoreboardPlayerRows()
    {
        var layout = GetScoreboardLayout();
        var rows = new List<ScoreboardPlayerRow>(24);
        AddScoreboardPlayerRows(rows, GetScoreboardPlayers(PlayerTeam.Red), PlayerTeam.Red, layout);
        AddScoreboardPlayerRows(rows, GetScoreboardPlayers(PlayerTeam.Blue), PlayerTeam.Blue, layout);
        return rows;
    }

    private void AddScoreboardPlayerRows(List<ScoreboardPlayerRow> rows, List<PlayerEntity> players, PlayerTeam team, ScoreboardLayout layout)
    {
        for (var index = 0; index < players.Count && index < 12; index += 1)
        {
            var player = players[index];
            if (!TryGetScoreboardPlayerNetworkSlot(player, out var slot))
            {
                continue;
            }

            rows.Add(new ScoreboardPlayerRow(
                player,
                slot,
                ReferenceEquals(player, _world.LocalPlayer),
                GetScoreboardPlayerRowBounds(team, index, layout.XOffset, layout.YOffset, layout.XSize)));
        }
    }

    private static bool TryGetScoreboardPlayerRowAtPoint(IReadOnlyList<ScoreboardPlayerRow> rows, Point point, out ScoreboardPlayerRow row)
    {
        for (var index = 0; index < rows.Count; index += 1)
        {
            if (rows[index].Bounds.Contains(point))
            {
                row = rows[index];
                return true;
            }
        }

        row = default;
        return false;
    }

    private static bool TryGetScoreboardPlayerRowBySlot(IReadOnlyList<ScoreboardPlayerRow> rows, byte slot, out ScoreboardPlayerRow row)
    {
        for (var index = 0; index < rows.Count; index += 1)
        {
            if (rows[index].Slot == slot)
            {
                row = rows[index];
                return true;
            }
        }

        row = default;
        return false;
    }

    private bool TryGetScoreboardPlayerNetworkSlot(PlayerEntity player, out byte slot)
    {
        if (ReferenceEquals(player, _world.LocalPlayer))
        {
            slot = _networkClient.IsConnected && _networkClient.LocalPlayerSlot != 0
                ? _networkClient.LocalPlayerSlot
                : SimulationWorld.LocalPlayerSlot;
            return true;
        }

        return _world.TryGetPlayerNetworkSlot(player, out slot);
    }

    private void TryHandleScoreboardPlayerCardClick(Point point)
    {
        if (_scoreboardHoveredPlayerRow is not { } row
            || !TryGetScoreboardAddFriendCode(row, out var friendCode))
        {
            return;
        }

        var cardLayout = GetScoreboardPlayerCardLayout(row.Bounds);
        if (!cardLayout.EditButtonBounds.Contains(point))
        {
            return;
        }

        if (TrySendFriendRequestToCode(friendCode))
        {
            _suppressPrimaryFireUntilMouseRelease = true;
        }
    }

    private void DrawScoreboardHoveredPlayerCard()
    {
        if (_scoreboardHoveredPlayerRow is not { } row)
        {
            return;
        }

        var profile = ResolveScoreboardPlayerCardProfile(row);
        var actionLabel = TryGetScoreboardAddFriendCode(row, out _) ? "Add Friend" : string.Empty;
        DrawPlayerCard(
            GetScoreboardPlayerCardLayout(row.Bounds),
            profile,
            row.Player.DisplayName,
            actionLabel,
            actionActive: false);
    }

    private PlayerCardProfile ResolveScoreboardPlayerCardProfile(ScoreboardPlayerRow row)
    {
        if (row.IsLocal)
        {
            return _clientIdentity.PlayerCard;
        }

        if (TryGetOnlinePlayerSocialProfile(row.Slot, out var socialProfile))
        {
            if (!string.IsNullOrWhiteSpace(socialProfile.PlayerCardJson))
            {
                return PlayerCardProfile.Deserialize(socialProfile.PlayerCardJson);
            }

            if (!string.IsNullOrWhiteSpace(socialProfile.FriendCode))
            {
                return CreateFallbackPlayerCard(socialProfile.FriendCode);
            }
        }

        return CreateFallbackPlayerCard($"{row.Slot}:{row.Player.DisplayName}");
    }

    private bool TryGetScoreboardAddFriendCode(ScoreboardPlayerRow row, out string friendCode)
    {
        friendCode = string.Empty;
        return !row.IsLocal
            && TryGetOnlinePlayerSocialProfile(row.Slot, out var socialProfile)
            && CanSendFriendRequestToCode(socialProfile.FriendCode, out friendCode, out _);
    }

    private PlayerCardLayout GetScoreboardPlayerCardLayout(Rectangle rowBounds)
    {
        var maxWidth = Math.Max(300, ViewportWidth - 24);
        var baseWidth = Math.Clamp((int)MathF.Round(ViewportWidth * 0.42f), 300, Math.Min(430, maxWidth));
        var width = Math.Clamp((int)MathF.Round(baseWidth * GetPlayerCardSizeScale()), 210, baseWidth);
        var height = (int)MathF.Round(width * 0.61f);
        var preferredRight = rowBounds.Center.X < ViewportWidth / 2;
        var x = preferredRight
            ? rowBounds.Right + 18
            : rowBounds.X - width - 18;
        if (x < 12 || x + width > ViewportWidth - 12)
        {
            x = preferredRight
                ? rowBounds.X - width - 18
                : rowBounds.Right + 18;
        }

        x = Math.Clamp(x, 12, Math.Max(12, ViewportWidth - width - 12));
        var y = Math.Clamp(rowBounds.Center.Y - (height / 2), 12, Math.Max(12, ViewportHeight - height - 16));
        return CreatePlayerCardLayout(new Rectangle(x, y, width, height));
    }

    private void DrawScoreboardTeam(List<PlayerEntity> players, PlayerTeam team, float alpha, float xoffset, float yoffset, float xsize)
    {
        var teamColor = team == PlayerTeam.Red
            ? new Color(225, 110, 103)
            : new Color(94, 170, 255);
        var iconX = team == PlayerTeam.Red
            ? xoffset + 14f
            : xoffset + (xsize / 2f) + 49f;
        var nameX = team == PlayerTeam.Red
            ? xoffset + 28f
            : xoffset + (xsize / 2f) + 60f;
        var pointsRight = team == PlayerTeam.Red
            ? xoffset + (xsize / 2f) - 15f
            : xoffset + xsize + 25f;
        var dominationX = team == PlayerTeam.Red
            ? xoffset + (xsize / 2f) - 5f
            : xoffset + xsize + 35f;
        var relationX = xoffset + xsize + 55f;
        var deadX = team == PlayerTeam.Red
            ? xoffset + 195f
            : xoffset + 472f;

        for (var index = 0; index < players.Count && index < 12; index += 1)
        {
            var player = players[index];
            var rowY = yoffset + 70f + (20f * (index + 1));
            if (TryGetScoreboardPlayerNetworkSlot(player, out var slot)
                && _scoreboardHoveredPlayerRow is { } hoveredRow
                && hoveredRow.Slot == slot)
            {
                _spriteBatch.Draw(_pixel, GetScoreboardPlayerRowBounds(team, index, xoffset, yoffset, xsize), teamColor * (alpha * 0.16f));
            }

            if (!_networkClient.IsSpectator && _world.LocalPlayer.Team == player.Team)
            {
                TryDrawScreenSprite("Icon", GetScoreboardIconFrame(player.ClassId), new Vector2(iconX, rowY), Color.White * alpha, Vector2.One);
                TryDrawScreenSprite("Icon", GetScoreboardIconFrame(player.ClassId), new Vector2(iconX, rowY), teamColor * (alpha * 0.2f), Vector2.One);
            }

            const float badgeScale = 1f;
            var badgeWidth = MeasureScoreboardBadgeWidth(player.BadgeMask, badgeScale);
            var nameMaxWidth = Math.Max(24f, pointsRight - nameX - 12f);
            var displayName = TrimBitmapMenuText(
                SanitizeScoreboardText(player.DisplayName),
                Math.Max(24f, nameMaxWidth - badgeWidth),
                1f);
            DrawScoreboardNameWithBadges(displayName, player.BadgeMask, new Vector2(nameX, rowY), teamColor, alpha, 1f, badgeScale);
            DrawBitmapFontTextRightAligned(MathF.Floor(player.Points).ToString(CultureInfo.InvariantCulture), new Vector2(pointsRight, rowY), teamColor * alpha, 1f);
            DrawScoreboardDominationBadges(player, team, rowY, alpha, dominationX, relationX);

            if (!player.IsAlive)
            {
                TryDrawScreenSprite("DeadS", 0, new Vector2(deadX, rowY + 5f), Color.White * alpha, Vector2.One);
            }
        }
    }

    private static Rectangle GetScoreboardPlayerRowBounds(PlayerTeam team, int index, float xoffset, float yoffset, float xsize)
    {
        var rowY = yoffset + 70f + (20f * (index + 1));
        var x = team == PlayerTeam.Red
            ? xoffset + 8f
            : xoffset + (xsize / 2f) + 40f;
        var width = (xsize / 2f) + 35f;
        return new Rectangle(
            (int)MathF.Round(x),
            (int)MathF.Round(rowY - 2f),
            (int)MathF.Round(width),
            20);
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
            var sprite = GetResolvedSprite("HaxxyBadgeS");
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
