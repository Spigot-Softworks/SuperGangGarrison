#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int SpectatorPanelSourceHeight = 36 * 6;
    private const int SpectatorBoardRowHeight = 36;
    private const int SpectatorBoardIconSize = 25;
    private const int SpectatorBoardPad = 5;
    private const float SpectatorOffscreenArrowInset = 38f;
    private static readonly Color SpectatorRed = new(255, 0, 0);
    private static readonly Color SpectatorBlue = new(0, 0, 255);
    private static readonly Color SpectatorRedLight = new(255, 191, 191);
    private static readonly Color SpectatorRedDark = new(64, 0, 0);
    private static readonly Color SpectatorBlueLight = new(191, 191, 255);
    private static readonly Color SpectatorBlueDark = new(0, 0, 64);
    private static readonly Color SpectatorHudGray = new(128, 128, 128);

    private int GetGameplayCameraViewportHeight(int viewportHeight)
    {
        if (!_networkClient.IsSpectator)
        {
            return viewportHeight;
        }

        return Math.Max(1, viewportHeight - GetSpectatorPanelHeight(viewportHeight));
    }

    private static int GetSpectatorPanelHeight(int viewportHeight)
    {
        return Math.Min(SpectatorPanelSourceHeight, Math.Max(0, viewportHeight - 1));
    }

    private void DrawSpectatorBaselineHud(Vector2 cameraPosition)
    {
        if (!_networkClient.IsSpectator)
        {
            return;
        }

        var gameHeight = GetGameplayCameraViewportHeight(ViewportHeight);
        var panelHeight = Math.Max(0, ViewportHeight - gameHeight);
        DrawSpectatorOffscreenIndicators(cameraPosition, gameHeight);
        if (panelHeight <= 0)
        {
            return;
        }

        DrawSpectatorPersistentBoards(gameHeight, panelHeight);
        DrawSpectatorMinimap(new Rectangle(ViewportWidth / 4, gameHeight, ViewportWidth / 2, panelHeight));
    }

    private void DrawSpectatorPersistentBoards(int panelY, int panelHeight)
    {
        var boardWidth = ViewportWidth / 4;
        if (boardWidth <= 0)
        {
            return;
        }

        var redBounds = new Rectangle(0, panelY, boardWidth, panelHeight);
        var blueBounds = new Rectangle(ViewportWidth * 3 / 4, panelY, boardWidth, panelHeight);
        _spriteBatch.Draw(_pixel, redBounds, Color.Black);
        _spriteBatch.Draw(_pixel, blueBounds, Color.Black);
        DrawSpectatorTeamBoard(redBounds.X, redBounds.Y, redBounds.Width, redBounds.Height, PlayerTeam.Red);
        DrawSpectatorTeamBoard(blueBounds.X, blueBounds.Y, blueBounds.Width, blueBounds.Height, PlayerTeam.Blue);
    }

    private void DrawSpectatorTeamBoard(int boardX, int boardY, int boardWidth, int boardHeight, PlayerTeam team)
    {
        var players = GetSpectatorTeamPlayers(team);
        var maxRows = Math.Max(0, boardHeight / SpectatorBoardRowHeight);
        for (var index = 0; index < players.Count && index < maxRows; index += 1)
        {
            var player = players[index];
            var rowY = boardY + (SpectatorBoardRowHeight * index);
            DrawBitmapFontText(
                SanitizeScoreboardText(player.DisplayName),
                new Vector2(boardX, rowY),
                Color.White,
                1f);

            var iconPosition = new Vector2(
                boardX + ((SpectatorBoardPad + SpectatorBoardIconSize) / 2f),
                rowY + SpectatorBoardIconSize - (SpectatorBoardPad / 2f));
            TryDrawScreenSprite("MedAlert", GetSpectatorMedAlertFrame(player), iconPosition, Color.White, Vector2.One);

            if (!player.IsAlive)
            {
                DrawSpectatorDeadBoardRow(player, boardX, rowY, iconPosition);
                continue;
            }

            DrawSpectatorAliveBoardRow(player, boardX, rowY);
        }
    }

    private void DrawSpectatorDeadBoardRow(PlayerEntity player, int boardX, int rowY, Vector2 iconPosition)
    {
        if (_world.TryGetPlayerNetworkSlot(player, out var slot))
        {
            var respawnTicks = _world.GetNetworkPlayerRespawnTicks(slot);
            if (respawnTicks > 0)
            {
                var respawnSeconds = MathF.Ceiling(respawnTicks / (float)Math.Max(1, _config.TicksPerSecond));
                DrawBitmapFontText(
                    respawnSeconds.ToString("0", CultureInfo.InvariantCulture),
                    new Vector2(boardX + 35f, rowY + 12f),
                    SpectatorHudGray,
                    1f);
            }
        }

        var overlay = new Rectangle(
            (int)MathF.Round(iconPosition.X - 13f),
            rowY + 10,
            SpectatorBoardIconSize,
            SpectatorBoardIconSize);
        _spriteBatch.Draw(_pixel, overlay, SpectatorHudGray * 0.5f);
        DrawSpectatorBoardStats(player, boardX, rowY);
    }

    private void DrawSpectatorAliveBoardRow(PlayerEntity player, int boardX, int rowY)
    {
        var healthTextColor = GetSpectatorBoardHealthColor(player);
        DrawSpectatorMultilineText(
            $"{Math.Max(0, player.Health).ToString(CultureInfo.InvariantCulture)}\n/{Math.Max(1, player.MaxHealth).ToString(CultureInfo.InvariantCulture)}",
            new Vector2(boardX + 39f, rowY + 12f),
            healthTextColor,
            1f);

        var healthBar = new Rectangle(boardX + (SpectatorBoardPad / 2) + SpectatorBoardIconSize, rowY + 12, 5, 20);
        DrawScreenHealthBar(
            healthBar,
            player.Health,
            Math.Max(1, player.MaxHealth),
            false,
            healthTextColor,
            Color.Black,
            HudFillDirection.VerticalBottomToTop);

        if (TryGetSpectatorSecondaryBoardRatio(player, out var secondaryRatio, out var secondaryColor))
        {
            DrawScreenHealthBar(
                new Rectangle(healthBar.Right + 1, healthBar.Y, 5, healthBar.Height),
                secondaryRatio,
                1f,
                false,
                secondaryColor,
                Color.Black,
                HudFillDirection.VerticalBottomToTop);
        }

        DrawSpectatorBoardStats(player, boardX, rowY);
    }

    private void DrawSpectatorBoardStats(PlayerEntity player, int boardX, int rowY)
    {
        var statText = player.ClassId switch
        {
            PlayerClass.Medic => $"{player.Kills}/{player.Deaths}/{player.Assists}\n{MathF.Floor(Math.Max(0, player.HealPoints) / 1000f):0}kHp {GetSpectatorMedicUberCountFallback(player)} Ubr",
            PlayerClass.Engineer => $"{player.Kills}/{player.Deaths}/{player.Assists}\n{GetSpectatorEngineerStatusText(player)}",
            _ => $"{player.Kills}/{player.Deaths}/{player.Assists}\n{player.Caps} Cap ",
        };
        DrawSpectatorMultilineText(
            statText,
            new Vector2(boardX + (2 * SpectatorBoardPad) + SpectatorBoardIconSize + 50f, rowY + (SpectatorBoardRowHeight / 3f)),
            Color.White,
            1f);
    }

    private static int GetSpectatorMedicUberCountFallback(PlayerEntity player)
    {
        return player.IsMedicUbering ? 1 : 0;
    }

    private string GetSpectatorEngineerStatusText(PlayerEntity player)
    {
        return TryFindSpectatorOwnedSentry(player, out var sentry)
            ? $"Gun: {Math.Max(0, sentry.Health).ToString(CultureInfo.InvariantCulture)}/{Math.Max(1, sentry.MaxHealth).ToString(CultureInfo.InvariantCulture)}"
            : "<no autogun>";
    }

    private bool TryGetSpectatorSecondaryBoardRatio(PlayerEntity player, out float ratio, out Color color)
    {
        color = new Color(220, 184, 72);
        switch (player.ClassId)
        {
            case PlayerClass.Medic:
                ratio = Math.Clamp(player.MedicUberCharge / Math.Max(1f, PlayerEntity.MedicUberMaxCharge), 0f, 1f);
                return true;
            case PlayerClass.Engineer:
                if (TryFindSpectatorOwnedSentry(player, out var sentry))
                {
                    ratio = Math.Clamp(sentry.Health / (float)Math.Max(1, sentry.MaxHealth), 0f, 1f);
                    return true;
                }

                ratio = Math.Clamp(player.Metal / Math.Max(1f, player.MaxMetal), 0f, 1f);
                return true;
            case PlayerClass.Heavy:
                var heavyCooldownDuration = player.HeavyEatCooldownTicksRemaining > 0
                    ? player.HeavyEatCooldownDurationTicks
                    : PlayerEntity.HeavySandvichCooldownTicks;
                ratio = 1f - Math.Clamp(player.HeavyEatCooldownTicksRemaining / (float)Math.Max(1, heavyCooldownDuration), 0f, 1f);
                return true;
            default:
                ratio = 0f;
                return false;
        }
    }

    private bool TryFindSpectatorOwnedSentry(PlayerEntity player, out SentryEntity sentry)
    {
        foreach (var candidate in _world.Sentries)
        {
            if (candidate.OwnerPlayerId == player.Id)
            {
                sentry = candidate;
                return true;
            }
        }

        sentry = null!;
        return false;
    }

    private static Color GetSpectatorBoardHealthColor(PlayerEntity player)
    {
        if (player.IsUbered || player.IsKritzCritBoosted || player.IsMedicUbering)
        {
            return Color.Aqua;
        }

        if (player.Health < 50)
        {
            return Color.Red;
        }

        return player.Health < 75 ? Color.Orange : Color.Lime;
    }

    private void DrawSpectatorMultilineText(string text, Vector2 position, Color color, float scale)
    {
        var lineY = position.Y;
        foreach (var line in text.Split('\n'))
        {
            DrawBitmapFontText(line, new Vector2(position.X, lineY), color, scale);
            lineY += Math.Max(1f, MeasureBitmapFontHeight(scale));
        }
    }

    private List<PlayerEntity> GetSpectatorTeamPlayers(PlayerTeam team)
    {
        var players = new List<PlayerEntity>();
        foreach (var player in EnumerateRemotePlayersForView())
        {
            if (player.Team == team)
            {
                players.Add(player);
            }
        }

        players.Sort(static (left, right) =>
        {
            var leftPriority = GetSpectatorLegacyClassPriority(left.ClassId) * 100f + left.Points;
            var rightPriority = GetSpectatorLegacyClassPriority(right.ClassId) * 100f + right.Points;
            var priorityCompare = rightPriority.CompareTo(leftPriority);
            return priorityCompare != 0
                ? priorityCompare
                : left.Id.CompareTo(right.Id);
        });
        return players;
    }

    private static int GetSpectatorLegacyClassPriority(PlayerClass playerClass)
    {
        return playerClass switch
        {
            PlayerClass.Medic => 10,
            PlayerClass.Heavy => 9,
            _ => 5,
        };
    }

    private void DrawSpectatorMinimap(Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        _spriteBatch.Draw(_pixel, bounds, Color.Black);
        if (_world.Bounds.Width <= 0f || _world.Bounds.Height <= 0f)
        {
            return;
        }

        var mapBounds = GetSpectatorMinimapMapBounds(bounds);
        if (TryGetLevelBackgroundTexture(out var background))
        {
            _spriteBatch.Draw(background, mapBounds, Color.White);
        }
        else
        {
            _spriteBatch.Draw(_pixel, mapBounds, new Color(36, 36, 36));
        }

        DrawSpectatorMinimapObjectives(mapBounds);
        DrawSpectatorMinimapIntel(mapBounds, _world.RedIntel);
        DrawSpectatorMinimapIntel(mapBounds, _world.BlueIntel);
        DrawSpectatorMinimapGenerators(mapBounds);
        DrawSpectatorMinimapSentries(mapBounds);
        DrawSpectatorMinimapPlayers(mapBounds);
    }

    private Rectangle GetSpectatorMinimapMapBounds(Rectangle bounds)
    {
        var scale = Math.Min(bounds.Width / Math.Max(1f, _world.Bounds.Width), bounds.Height / Math.Max(1f, _world.Bounds.Height));
        var width = Math.Max(1, (int)MathF.Round(_world.Bounds.Width * scale));
        var height = Math.Max(1, (int)MathF.Round(_world.Bounds.Height * scale));
        return new Rectangle(bounds.X + ((bounds.Width - width) / 2), bounds.Y + ((bounds.Height - height) / 2), width, height);
    }

    private Vector2 ProjectSpectatorMinimapPoint(Rectangle mapBounds, float worldX, float worldY)
    {
        return new Vector2(
            mapBounds.X + (worldX / Math.Max(1f, _world.Bounds.Width) * mapBounds.Width),
            mapBounds.Y + (worldY / Math.Max(1f, _world.Bounds.Height) * mapBounds.Height));
    }

    private void DrawSpectatorMinimapObjectives(Rectangle mapBounds)
    {
        foreach (var point in _world.ControlPoints)
        {
            var frameIndex = 8;
            var size = point.IsLocked ? 0.25f : 1f;
            if (point.IsLocked)
            {
                frameIndex += 1;
            }

            if (point.Team == PlayerTeam.Red)
            {
                frameIndex += 2;
            }
            else if (point.Team == PlayerTeam.Blue)
            {
                frameIndex += 4;
            }

            var rate = point.CapTimeTicks <= 0 ? 0f : Math.Clamp(point.CappingTicks / point.CapTimeTicks, 0f, 1f);
            var capColor = point.CappingTeam switch
            {
                PlayerTeam.Red => SpectatorRed,
                PlayerTeam.Blue => SpectatorBlue,
                _ => Color.White,
            };

            DrawSpectatorMapSprite(
                mapBounds,
                "SpectatorHudIcons",
                frameIndex,
                point.Marker.X,
                point.Marker.Y,
                1f,
                size,
                Color.White,
                capColor,
                rate,
                Color.Transparent,
                hasOutline: false);
        }
    }

    private void DrawSpectatorMinimapIntel(Rectangle mapBounds, TeamIntelligenceState intel)
    {
        var drawX = intel.X;
        var drawY = intel.Y;
        if (intel.IsCarried && TryFindSpectatorIntelCarrier(intel, out var carrier))
        {
            var carrierPosition = GetRenderPosition(carrier);
            drawX = carrierPosition.X;
            drawY = carrierPosition.Y;
        }

        var frameIndex = intel.Team == PlayerTeam.Red ? 0 : 1;
        var rate = 0f;
        if (intel.IsDropped)
        {
            frameIndex = intel.Team == PlayerTeam.Red ? 6 : 7;
            rate = 1f - Math.Clamp(intel.ReturnTicksRemaining / (float)PlayerEntity.IntelRechargeMaxTicks, 0f, 1f);
        }
        else if (!intel.IsAtBase)
        {
            frameIndex = intel.Team == PlayerTeam.Red ? 4 : 5;
        }

        DrawSpectatorMapSprite(
            mapBounds,
            "SpectatorHudIcons",
            frameIndex,
            drawX,
            drawY,
            1f,
            1f,
            Color.White,
            SpectatorHudGray,
            rate,
            Color.Transparent,
            hasOutline: false);

        if (!intel.IsAtBase)
        {
            DrawSpectatorMapSprite(
                mapBounds,
                "SpectatorHudIcons",
                intel.Team == PlayerTeam.Red ? 2 : 3,
                intel.HomeX,
                intel.HomeY,
                1f,
                1f,
                Color.White,
                SpectatorHudGray,
                0f,
                Color.Transparent,
                hasOutline: false);
        }
    }

    private bool TryFindSpectatorIntelCarrier(TeamIntelligenceState intel, out PlayerEntity carrier)
    {
        foreach (var player in EnumerateRemotePlayersForView())
        {
            if (player.IsAlive && player.Team != intel.Team && player.IsCarryingIntel)
            {
                carrier = player;
                return true;
            }
        }

        carrier = null!;
        return false;
    }

    private void DrawSpectatorMinimapGenerators(Rectangle mapBounds)
    {
        foreach (var generator in _world.Generators)
        {
            var frameIndex = generator.Team == PlayerTeam.Red ? 14 : 16;
            DrawSpectatorMapSprite(
                mapBounds,
                "SpectatorHudIcons",
                frameIndex,
                generator.Marker.X,
                generator.Marker.Y,
                1f,
                1f,
                Color.White,
                SpectatorHudGray,
                1f - generator.HealthFraction,
                Color.Transparent,
                hasOutline: false);
        }
    }

    private void DrawSpectatorMinimapSentries(Rectangle mapBounds)
    {
        foreach (var sentry in _world.Sentries)
        {
            var teamColors = GetSpectatorMinimapTeamColors(sentry.Team);
            DrawSpectatorMapSprite(
                mapBounds,
                "SpectatorHudIcons",
                sentry.Team == PlayerTeam.Red ? 18 : 19,
                sentry.X,
                sentry.Y,
                sentry.FacingDirectionX < 0f ? -1f : 1f,
                0.8f,
                teamColors.Light,
                teamColors.Dark,
                1f - Math.Clamp(sentry.Health / (float)Math.Max(1, sentry.MaxHealth), 0f, 1f),
                Color.Transparent,
                hasOutline: false);
        }
    }

    private void DrawSpectatorMinimapPlayers(Rectangle mapBounds)
    {
        foreach (var player in EnumerateRemotePlayersForView())
        {
            if (!player.IsAlive || IsSpectatorHiddenSpy(player))
            {
                continue;
            }

            var position = GetRenderPosition(player);
            var teamColors = GetSpectatorMinimapTeamColors(player.Team);
            DrawSpectatorMapSprite(
                mapBounds,
                "SpectatorClassHeads",
                GetSpectatorClassHeadFrame(player),
                position.X,
                position.Y - 24f,
                player.FacingDirectionX < 0f ? -1f : 1f,
                1f,
                teamColors.Light,
                teamColors.Dark,
                1f - Math.Clamp(player.Health / (float)Math.Max(1, player.MaxHealth), 0f, 1f),
                GetSpectatorPureTeamColor(player.Team),
                hasOutline: true);
        }
    }

    private void DrawSpectatorMapSprite(
        Rectangle mapBounds,
        string spriteName,
        int frameIndex,
        float worldX,
        float worldY,
        float xscale,
        float size,
        Color color1,
        Color color2,
        float rate,
        Color outlineColor,
        bool hasOutline)
    {
        if (size <= 0f)
        {
            return;
        }

        var sprite = GetResolvedSprite(spriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return;
        }

        var frame = sprite.Frames[Math.Clamp(frameIndex, 0, sprite.Frames.Count - 1)];
        var projected = ProjectSpectatorMinimapPoint(mapBounds, worldX, worldY);
        if (projected.X < mapBounds.Left
            || projected.Y < mapBounds.Top
            || projected.X > mapBounds.Right
            || projected.Y > mapBounds.Bottom)
        {
            return;
        }

        var origin = sprite.Origin.ToVector2();
        var scale = new Vector2(xscale * size, size);
        if (hasOutline)
        {
            DrawLoadedSpriteFrame(frame, projected + new Vector2(-1f, 0f), null, outlineColor, 0f, origin, scale, SpriteEffects.None, 0f);
            DrawLoadedSpriteFrame(frame, projected + new Vector2(1f, 0f), null, outlineColor, 0f, origin, scale, SpriteEffects.None, 0f);
            DrawLoadedSpriteFrame(frame, projected + new Vector2(0f, -1f), null, outlineColor, 0f, origin, scale, SpriteEffects.None, 0f);
            DrawLoadedSpriteFrame(frame, projected + new Vector2(0f, 1f), null, outlineColor, 0f, origin, scale, SpriteEffects.None, 0f);
        }

        DrawLoadedSpriteFrame(frame, projected, null, color1, 0f, origin, scale, SpriteEffects.None, 0f);

        rate = Math.Clamp(rate, 0f, 1f);
        var rateWidth = Math.Clamp((int)MathF.Round(frame.Width * rate), 0, frame.Width);
        if (rateWidth <= 0)
        {
            return;
        }

        var sourceX = frame.Width - rateWidth;
        var source = new Rectangle(sourceX, 0, rateWidth, frame.Height);
        var partPosition = new Vector2(
            projected.X + ((sourceX - origin.X) * xscale * size),
            projected.Y - (origin.Y * size));
        DrawLoadedSpriteFrame(frame, partPosition, source, color2 * 0.8f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawSpectatorOffscreenIndicators(Vector2 cameraPosition, int gameHeight)
    {
        if (ViewportWidth <= 0 || gameHeight <= 0)
        {
            return;
        }

        var viewBounds = new Rectangle(0, 0, ViewportWidth, gameHeight);
        var cornerRadians = MathF.Asin(Math.Clamp(gameHeight / (float)Math.Max(1, ViewportWidth), 0f, 1f));
        foreach (var player in EnumerateRemotePlayersForView())
        {
            if (!player.IsAlive || IsSpectatorHiddenSpy(player))
            {
                continue;
            }

            var screen = GetRenderPosition(player) - cameraPosition;
            if (viewBounds.Contains((int)screen.X, (int)screen.Y))
            {
                continue;
            }

            var theta = MathF.Atan2((gameHeight / 2f) - screen.Y, screen.X - (ViewportWidth / 2f));
            if (theta < 0f)
            {
                theta += MathF.PI * 2f;
            }

            var healthRatio = player.Health / (float)Math.Max(1, player.MaxHealth);
            var arrowFrame = Math.Clamp((int)MathF.Floor(healthRatio * 19f), 0, 19);
            var textColor = GetSpectatorPureTeamColor(player.Team);
            var label = SanitizeScoreboardText(player.DisplayName);

            if (theta <= cornerRadians || theta > (MathF.PI * 2f) - cornerRadians)
            {
                var unknown = ((ViewportWidth / 2f) - (SpectatorOffscreenArrowInset * MathF.Cos(theta))) * MathF.Tan(theta);
                var drawX = ViewportWidth - (MathF.Cos(theta) * SpectatorOffscreenArrowInset);
                var drawY = (gameHeight / 2f) - unknown;
                DrawSpectatorOffscreenBubble(player, arrowFrame, drawX, drawY, theta);
                DrawBitmapFontTextRightAligned(label, new Vector2(ViewportWidth, theta < MathF.PI ? drawY + 20f : drawY - 20f), textColor, 1f);
            }
            else if (theta > cornerRadians && theta <= MathF.PI - cornerRadians)
            {
                var unknown = ((gameHeight / 2f) - (SpectatorOffscreenArrowInset * MathF.Sin(theta))) / MathF.Tan(theta);
                var drawX = unknown + (ViewportWidth / 2f);
                var drawY = SpectatorOffscreenArrowInset * MathF.Sin(theta);
                DrawSpectatorOffscreenBubble(player, arrowFrame, drawX, drawY, theta);
                DrawBitmapFontTextCentered(label, new Vector2(drawX, drawY + 20f), textColor, 1f);
            }
            else if (theta > MathF.PI - cornerRadians && theta <= MathF.PI + cornerRadians)
            {
                var unknown = ((ViewportWidth / 2f) + (SpectatorOffscreenArrowInset * MathF.Cos(theta))) * MathF.Tan(theta);
                var drawX = -(SpectatorOffscreenArrowInset * MathF.Cos(theta));
                var drawY = unknown + (gameHeight / 2f);
                DrawSpectatorOffscreenBubble(player, arrowFrame, drawX, drawY, theta);
                DrawBitmapFontText(label, new Vector2(0f, theta < MathF.PI ? drawY + 20f : drawY - 20f), textColor, 1f);
            }
            else
            {
                var unknown = ((gameHeight / 2f) + (SpectatorOffscreenArrowInset * MathF.Sin(theta))) / MathF.Tan(theta);
                var drawX = (ViewportWidth / 2f) - unknown;
                var drawY = gameHeight + (SpectatorOffscreenArrowInset * MathF.Sin(theta));
                DrawSpectatorOffscreenBubble(player, arrowFrame, drawX, drawY, theta);
                DrawBitmapFontTextCentered(label, new Vector2(drawX, drawY - 20f), textColor, 1f);
            }
        }
    }

    private void DrawSpectatorOffscreenBubble(PlayerEntity player, int arrowFrame, float drawX, float drawY, float theta)
    {
        var position = new Vector2(drawX, drawY);
        TryDrawScreenSprite("MedRadarArrow", arrowFrame, position, Color.White, Vector2.One, theta);
        TryDrawScreenSprite("MedAlert", GetSpectatorMedAlertFrame(player), position, Color.White, Vector2.One);
    }

    private static (Color Light, Color Dark) GetSpectatorMinimapTeamColors(PlayerTeam team)
    {
        return team == PlayerTeam.Blue
            ? (SpectatorBlueLight, SpectatorBlueDark)
            : (SpectatorRedLight, SpectatorRedDark);
    }

    private static Color GetSpectatorPureTeamColor(PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? SpectatorBlue : SpectatorRed;
    }

    private static int GetSpectatorMedAlertFrame(PlayerEntity player)
    {
        return (GetSpectatorLegacyTeamIndex(player.Team) * 10) + GetSpectatorLegacyClassIndex(player.ClassId) + 2;
    }

    private static int GetSpectatorClassHeadFrame(PlayerEntity player)
    {
        return (GetSpectatorLegacyClassIndex(player.ClassId) * 2) + (player.Team == PlayerTeam.Blue ? 1 : 0);
    }

    private static int GetSpectatorLegacyTeamIndex(PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? 1 : 0;
    }

    private static int GetSpectatorLegacyClassIndex(PlayerClass playerClass)
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
            PlayerClass.Quote => 9,
            _ => 0,
        };
    }
}
