#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using System;
using System.Collections.Generic;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void OpenLobbyBrowser()
    {
        _connectionFlowController.OpenLobbyBrowser();
    }

    private void OpenWatchBrowser()
    {
        _connectionFlowController.OpenWatchBrowser();
    }

    private void CloseLobbyBrowser(bool clearStatus)
    {
        _connectionFlowController.CloseLobbyBrowser(clearStatus);
    }

    private void RefreshLobbyBrowser()
    {
        _connectionFlowController.RefreshLobbyBrowser();
    }

    private void UpdateLobbyBrowserState(KeyboardState keyboard, MouseState mouse)
    {
        UpdateLobbyBrowserResponses();
        UpdateLobbyBrowserDetailsState();
        if (_lobbyBrowserPage == LobbyBrowserPage.Details)
        {
            UpdateLobbyBrowserDetailsInput(keyboard, mouse);
            return;
        }

        GetLobbyBrowserLayout(
            out _,
            out _,
            out var rowBounds,
            out var refreshBounds,
            out var joinBounds,
            out var manualBounds,
            out var backBounds,
            out _);

        if ((keyboard.IsKeyDown(Keys.Escape) && !_previousKeyboard.IsKeyDown(Keys.Escape))
            || IsControllerMenuBackPressed())
        {
            CloseLobbyBrowser(clearStatus: false);
            return;
        }

        if (TryConsumeControllerMenuNavigation(out _, out var verticalStep) && verticalStep != 0)
        {
            _lobbyBrowserSelectedIndex = MoveControllerMenuSelectionClamped(
                _lobbyBrowserSelectedIndex,
                _lobbyBrowserEntries.Count,
                verticalStep);
            _lobbyBrowserHoverIndex = _lobbyBrowserSelectedIndex;
        }
        else if (ShouldUseMouseMenuHover(mouse))
        {
            _lobbyBrowserHoverIndex = -1;
            for (var index = 0; index < rowBounds.Length; index += 1)
            {
                if (index >= _lobbyBrowserEntries.Count)
                {
                    break;
                }

                if (rowBounds[index].Contains(mouse.Position))
                {
                    _lobbyBrowserHoverIndex = index;
                    break;
                }
            }
        }
        else
        {
            _lobbyBrowserHoverIndex = _lobbyBrowserSelectedIndex;
        }

        if ((keyboard.IsKeyDown(Keys.Enter) && !_previousKeyboard.IsKeyDown(Keys.Enter))
            || IsControllerMenuConfirmPressed())
        {
            if (_lobbyBrowserMode == LobbyBrowserMode.Watch)
            {
                OpenSelectedLobbyEntryDetails();
            }
            else
            {
                JoinSelectedLobbyEntry();
            }
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed)
        {
            return;
        }

        if (_lobbyBrowserHoverIndex >= 0)
        {
            _lobbyBrowserSelectedIndex = _lobbyBrowserHoverIndex;
            if (_lobbyBrowserMode == LobbyBrowserMode.Watch)
            {
                OpenSelectedLobbyEntryDetails();
            }

            return;
        }

        var point = mouse.Position;
        if (refreshBounds.Contains(point))
        {
            RefreshLobbyBrowser();
        }
        else if (joinBounds.Contains(point))
        {
            if (_lobbyBrowserMode == LobbyBrowserMode.Watch)
            {
                OpenSelectedLobbyEntryDetails();
            }
            else
            {
                JoinSelectedLobbyEntry();
            }
        }
        else if (_lobbyBrowserMode == LobbyBrowserMode.Join && manualBounds.Contains(point))
        {
            _connectionFlowController.OpenManualConnectMenuFromLobbyBrowser();
        }
        else if (backBounds.Contains(point))
        {
            CloseLobbyBrowser(clearStatus: false);
        }
    }

    private void UpdateLobbyBrowserDetailsInput(KeyboardState keyboard, MouseState mouse)
    {
        GetLobbyBrowserLayout(
            out _,
            out _,
            out _,
            out var refreshBounds,
            out var watchBounds,
            out _,
            out var backBounds,
            out _);

        if ((keyboard.IsKeyDown(Keys.Escape) && !_previousKeyboard.IsKeyDown(Keys.Escape))
            || IsControllerMenuBackPressed())
        {
            CloseLobbyBrowserDetails();
            return;
        }

        if ((keyboard.IsKeyDown(Keys.Enter) && !_previousKeyboard.IsKeyDown(Keys.Enter))
            || IsControllerMenuConfirmPressed())
        {
            WatchSelectedLobbyEntry();
            return;
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed)
        {
            return;
        }

        var point = mouse.Position;
        if (refreshBounds.Contains(point))
        {
            RefreshLobbyBrowserDetails();
        }
        else if (watchBounds.Contains(point))
        {
            WatchSelectedLobbyEntry();
        }
        else if (backBounds.Contains(point))
        {
            CloseLobbyBrowserDetails();
        }
    }

    private void DrawLobbyBrowserMenu()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.86f);

        // Draw bottom bar and runners (in animated mode only) - behind everything else
        if (_menuBackgroundMode != MenuBackgroundMode.Static)
        {
            const int bottomBarHeight = 76;
            var barY = viewportHeight - bottomBarHeight;
            var bottomBarBounds = new Rectangle(0, barY, viewportWidth, bottomBarHeight);
            _spriteBatch.Draw(_pixel, bottomBarBounds, new Color(0x57, 0x4f, 0x47));
            _menuBottomBarRunners.Draw(bottomBarBounds);
        }

        GetLobbyBrowserLayout(
            out var panel,
            out var listBounds,
            out var rows,
            out var refreshBounds,
            out var joinBounds,
            out var manualBounds,
            out var backBounds,
            out var compactLayout);
        if (_lobbyBrowserPage == LobbyBrowserPage.Details)
        {
            DrawLobbyBrowserDetailsMenu(panel, refreshBounds, joinBounds, manualBounds, backBounds, compactLayout);
            return;
        }

        GetLobbyBrowserColumnLayout(listBounds, compactLayout, out var nameColumnX, out var nameColumnWidth, out var addressColumnX, out var addressColumnWidth, out var playersColumnX, out var playersColumnWidth, out var mapColumnX, out var mapColumnWidth, out var modeColumnX, out var modeColumnWidth, out var pingColumnX, out var pingColumnWidth);
        const float headerScale = 1f;
        const float rowScale = 1f;
        const float buttonScale = 1f;
        var mouse = GetScaledMouseState(GetConstrainedMouseState(Game1.GetCurrentMouseState()));
        DrawRoundedRectangleOutline(panel, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 2, radius: 8);
        DrawRoundedRectangleOutline(listBounds, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 1, radius: 6);

        var headerY = listBounds.Y - 22f;
        DrawBitmapFontText("NAME", new Vector2(nameColumnX, headerY), Color.White, headerScale);
        DrawBitmapFontText("ADDRESS", new Vector2(addressColumnX, headerY), Color.White, headerScale);
        DrawBitmapFontText("PLAYERS", new Vector2(playersColumnX, headerY), Color.White, headerScale);
        DrawBitmapFontText("MAP", new Vector2(mapColumnX, headerY), Color.White, headerScale);
        DrawBitmapFontText("MODE", new Vector2(modeColumnX, headerY), Color.White, headerScale);
        DrawBitmapFontText("PING", new Vector2(pingColumnX, headerY), Color.White, headerScale);

        _spriteBatch.Draw(_pixel, new Rectangle(listBounds.X, listBounds.Y - 4, listBounds.Width, 2), new Color(120, 120, 120));
        var title = _lobbyBrowserMode == LobbyBrowserMode.Watch ? "Watch Servers" : "Join Servers";
        DrawBitmapFontText(title, new Vector2(panel.X + 24f, panel.Y + 22f), Color.White, 1.18f);
        for (var index = 0; index < rows.Length && index < _lobbyBrowserEntries.Count; index += 1)
        {
            var entry = _lobbyBrowserEntries[index];
            var bounds = rows[index];
            var highlighted = index == _lobbyBrowserSelectedIndex;
            var hovered = index == _lobbyBrowserHoverIndex;
            var background = highlighted
                ? new Color(54, 40, 38)
                : hovered
                    ? new Color(36, 32, 29)
                    : new Color(54, 47, 41);
            _spriteBatch.Draw(_pixel, bounds, background);

            var statusColor = entry.HasResponse || entry.CanJoinDirectly
                ? Color.White
                : entry.HasTimedOut
                    ? new Color(220, 160, 120)
                    : new Color(190, 190, 140);
            var playerText = entry.HasResponse
                ? $"{entry.PlayerCount}/{entry.MaxPlayerCount} (+{entry.SpectatorCount})"
                : entry.StatusText;
            var rowTextY = bounds.Y + (compactLayout ? 8f : 9f);
            DrawBitmapFontText(TrimBitmapMenuText(entry.DisplayName, nameColumnWidth, rowScale), new Vector2(nameColumnX, rowTextY), Color.White, rowScale);
            DrawBitmapFontText(TrimBitmapMenuText(entry.AddressLabel, addressColumnWidth, rowScale), new Vector2(addressColumnX, rowTextY), new Color(210, 210, 210), rowScale);
            DrawBitmapFontText(TrimBitmapMenuText(playerText, playersColumnWidth, rowScale), new Vector2(playersColumnX, rowTextY), statusColor, rowScale);
            DrawBitmapFontText(TrimBitmapMenuText(entry.LevelName, mapColumnWidth, rowScale), new Vector2(mapColumnX, rowTextY), statusColor, rowScale);
            DrawBitmapFontText(TrimBitmapMenuText(entry.ModeLabel, modeColumnWidth, rowScale), new Vector2(modeColumnX, rowTextY), statusColor, rowScale);
            DrawBitmapFontText(TrimBitmapMenuText(entry.PingLabel, pingColumnWidth, rowScale), new Vector2(pingColumnX, rowTextY), statusColor, rowScale);
        }

        DrawMenuButtonScaled(refreshBounds, "Refresh", refreshBounds.Contains(mouse.Position), buttonScale);
        DrawMenuButtonScaled(joinBounds, _lobbyBrowserMode == LobbyBrowserMode.Watch ? "View" : "Join", joinBounds.Contains(mouse.Position), buttonScale);
        if (_lobbyBrowserMode == LobbyBrowserMode.Join)
        {
            DrawMenuButtonScaled(manualBounds, "Manual", manualBounds.Contains(mouse.Position), buttonScale);
        }
        DrawMenuButtonScaled(backBounds, "Back", backBounds.Contains(mouse.Position), buttonScale);

        if (!string.IsNullOrWhiteSpace(_menuStatusMessage))
        {
            DrawBitmapFontText(_menuStatusMessage, new Vector2(panel.X + 24f, refreshBounds.Y - (compactLayout ? 26f : 30f)), new Color(230, 220, 180), 1f);
        }
    }

    private void DrawLobbyBrowserDetailsMenu(
        Rectangle panel,
        Rectangle refreshBounds,
        Rectangle watchBounds,
        Rectangle manualBounds,
        Rectangle backBounds,
        bool compactLayout)
    {
        var entry = _lobbyBrowserDetailsEntry;
        var details = _lobbyBrowserDetailsResponse;
        var contentX = panel.X + (compactLayout ? 20 : 28);
        var contentY = panel.Y + (compactLayout ? 28 : 34);
        var contentWidth = panel.Width - (compactLayout ? 40 : 56);
        var rosterTop = contentY + (compactLayout ? 112 : 128);
        var rosterBottom = refreshBounds.Y - (compactLayout ? 32 : 38);
        var rosterBounds = new Rectangle(contentX, rosterTop, contentWidth, Math.Max(120, rosterBottom - rosterTop));
        var title = details?.ServerName ?? entry?.ServerName ?? entry?.DisplayName ?? "Server Details";
        var address = entry?.AddressLabel ?? string.Empty;
        var levelName = details?.LevelName ?? entry?.LevelName ?? "-";
        var mode = details is not null ? FormatGameModeLabel(details.GameMode) : entry?.ModeLabel ?? "-";
        var players = details is not null
            ? $"{details.PlayerCount}/{details.MaxPlayerCount} (+{details.SpectatorCount})"
            : entry is not null && entry.HasResponse
                ? $"{entry.PlayerCount}/{entry.MaxPlayerCount} (+{entry.SpectatorCount})"
                : "-";
        var score = details is not null ? $"{details.RedScore}-{details.BlueScore}" : "-";
        var time = details is not null ? FormatServerDetailsTime(details.TimeRemainingTicks, details.TickRate) : "-";

        DrawBitmapFontText("Watch Server", new Vector2(contentX, contentY), Color.White, 1.18f);
        DrawBitmapFontText(TrimBitmapMenuText(title, contentWidth - 220f, 1f), new Vector2(contentX, contentY + 28f), new Color(235, 230, 215), 1f);
        DrawBitmapFontText(TrimBitmapMenuText(address, contentWidth - 220f, 0.86f), new Vector2(contentX, contentY + 52f), new Color(205, 205, 195), 0.86f);
        DrawBitmapFontText($"Map {levelName}", new Vector2(contentX, contentY + 78f), new Color(230, 220, 180), 0.86f);
        DrawBitmapFontText($"Mode {mode}", new Vector2(contentX + 185f, contentY + 78f), new Color(230, 220, 180), 0.86f);
        DrawBitmapFontText($"Players {players}", new Vector2(contentX + 335f, contentY + 78f), new Color(230, 220, 180), 0.86f);
        DrawBitmapFontText($"Score {score}", new Vector2(contentX + 520f, contentY + 78f), new Color(230, 220, 180), 0.86f);
        DrawBitmapFontText($"Time {time}", new Vector2(contentX + 650f, contentY + 78f), new Color(230, 220, 180), 0.86f);

        DrawRoundedRectangleOutline(rosterBounds, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 1, radius: 6);
        DrawBitmapFontText("ROSTER", new Vector2(rosterBounds.X + 12f, rosterBounds.Y - 22f), Color.White, 1f);
        DrawServerDetailsRoster(details, rosterBounds, compactLayout);

        var mouse = GetScaledMouseState(GetConstrainedMouseState(Game1.GetCurrentMouseState()));
        DrawMenuButtonScaled(refreshBounds, "Refresh", refreshBounds.Contains(mouse.Position), 1f);
        DrawMenuButtonScaled(watchBounds, "Watch", watchBounds.Contains(mouse.Position), 1f);
        DrawMenuButtonScaled(backBounds, "Back", backBounds.Contains(mouse.Position), 1f);

        if (!string.IsNullOrWhiteSpace(_lobbyBrowserDetailsStatus))
        {
            DrawBitmapFontText(_lobbyBrowserDetailsStatus, new Vector2(contentX, refreshBounds.Y - (compactLayout ? 26f : 30f)), new Color(230, 220, 180), 1f);
        }
    }

    private void DrawServerDetailsRoster(ServerDetailsResponseMessage? details, Rectangle bounds, bool compactLayout)
    {
        if (details is null)
        {
            DrawBitmapFontText("Loading...", new Vector2(bounds.X + 12f, bounds.Y + 14f), new Color(220, 210, 190), 1f);
            return;
        }

        if (details.Roster.Count == 0)
        {
            DrawBitmapFontText("No players connected.", new Vector2(bounds.X + 12f, bounds.Y + 14f), new Color(220, 210, 190), 1f);
            return;
        }

        var rowHeight = compactLayout ? 24 : 28;
        var maxRows = Math.Max(1, bounds.Height / rowHeight);
        var nameWidth = bounds.Width * 0.42f;
        var statusX = bounds.X + 12f + nameWidth;
        var scoreX = statusX + 170f;
        var healthX = scoreX + 120f;
        for (var index = 0; index < details.Roster.Count && index < maxRows; index += 1)
        {
            var rosterEntry = details.Roster[index];
            var row = new Rectangle(bounds.X, bounds.Y + (index * rowHeight), bounds.Width, rowHeight - 2);
            var color = rosterEntry.Team switch
            {
                (byte)PlayerTeam.Red => new Color(170, 72, 66),
                (byte)PlayerTeam.Blue => new Color(78, 112, 180),
                _ => new Color(70, 64, 58),
            };
            _spriteBatch.Draw(_pixel, row, color * (index % 2 == 0 ? 0.35f : 0.24f));

            var label = rosterEntry.IsSpectator
                ? $"{rosterEntry.Name} (spectator)"
                : $"{rosterEntry.Name} [{FormatServerDetailsClass(rosterEntry.ClassId)}]";
            var status = rosterEntry.IsSpectator
                ? "WATCHING"
                : rosterEntry.IsAlive
                    ? "ALIVE"
                    : "RESPAWN";
            var health = rosterEntry.MaxHealth > 0
                ? $"{Math.Max(0, (int)rosterEntry.Health)}/{rosterEntry.MaxHealth}"
                : "-";
            DrawBitmapFontText(TrimBitmapMenuText(label, nameWidth - 8f, 0.86f), new Vector2(bounds.X + 12f, row.Y + 7f), Color.White, 0.86f);
            DrawBitmapFontText(status, new Vector2(statusX, row.Y + 7f), new Color(230, 220, 180), 0.86f);
            DrawBitmapFontText($"{rosterEntry.Kills}/{rosterEntry.Deaths}/{rosterEntry.Assists}", new Vector2(scoreX, row.Y + 7f), new Color(230, 230, 220), 0.86f);
            DrawBitmapFontText(health, new Vector2(healthX, row.Y + 7f), new Color(230, 230, 220), 0.86f);
        }
    }

    private void GetLobbyBrowserLayout(
        out Rectangle panel,
        out Rectangle listBounds,
        out Rectangle[] rowBounds,
        out Rectangle refreshBounds,
        out Rectangle joinBounds,
        out Rectangle manualBounds,
        out Rectangle backBounds,
        out bool compactLayout)
    {
        var panelWidth = System.Math.Min(ViewportWidth - 32, 960);
        var panelHeight = System.Math.Min(ViewportHeight - 32, ViewportHeight < 540 ? 430 : 530);
        panel = new Rectangle(
            (ViewportWidth - panelWidth) / 2,
            (ViewportHeight - panelHeight) / 2,
            panelWidth,
            panelHeight);

        compactLayout = panel.Width < 900 || panel.Height < 500;
        var padding = compactLayout ? 18 : 28;
        var actionGap = compactLayout ? 10 : 16;
        var actionButtonHeight = compactLayout ? 36 : 42;
        var availableActionWidth = panel.Width - (padding * 2) - (actionGap * 3);
        var actionButtonWidth = availableActionWidth / 4;
        var actionY = panel.Bottom - padding - actionButtonHeight;

        refreshBounds = new Rectangle(panel.X + padding, actionY, actionButtonWidth, actionButtonHeight);
        joinBounds = new Rectangle(refreshBounds.Right + actionGap, actionY, actionButtonWidth, actionButtonHeight);
        manualBounds = new Rectangle(joinBounds.Right + actionGap, actionY, actionButtonWidth, actionButtonHeight);
        backBounds = new Rectangle(manualBounds.Right + actionGap, actionY, actionButtonWidth, actionButtonHeight);

        var contentTop = panel.Y + (compactLayout ? 72 : 88);
        var contentBottom = refreshBounds.Y - (compactLayout ? 36 : 44);
        listBounds = new Rectangle(panel.X + 20, contentTop, panel.Width - 40, System.Math.Max(120, contentBottom - contentTop));
        var rowHeight = compactLayout ? 26 : 30;
        var visibleRowCount = System.Math.Clamp(listBounds.Height / rowHeight, 4, 8);
        rowBounds = new Rectangle[visibleRowCount];
        for (var index = 0; index < rowBounds.Length; index += 1)
        {
            rowBounds[index] = new Rectangle(listBounds.X, listBounds.Y + (index * rowHeight), listBounds.Width, rowHeight - 2);
        }
    }

    private static void GetLobbyBrowserColumnLayout(
        Rectangle listBounds,
        bool compactLayout,
        out float nameColumnX,
        out float nameColumnWidth,
        out float addressColumnX,
        out float addressColumnWidth,
        out float playersColumnX,
        out float playersColumnWidth,
        out float mapColumnX,
        out float mapColumnWidth,
        out float modeColumnX,
        out float modeColumnWidth,
        out float pingColumnX,
        out float pingColumnWidth)
    {
        var innerPadding = compactLayout ? 10f : 12f;
        var width = listBounds.Width - (innerPadding * 2f);
        var nameWidthFactor = compactLayout ? 0.24f : 0.25f;
        var addressWidthFactor = compactLayout ? 0.22f : 0.24f;
        var playersWidthFactor = compactLayout ? 0.15f : 0.14f;
        var mapWidthFactor = compactLayout ? 0.17f : 0.16f;
        var modeWidthFactor = compactLayout ? 0.12f : 0.11f;
        var pingWidthFactor = 1f - nameWidthFactor - addressWidthFactor - playersWidthFactor - mapWidthFactor - modeWidthFactor;

        nameColumnX = listBounds.X + innerPadding;
        nameColumnWidth = width * nameWidthFactor;
        addressColumnX = nameColumnX + nameColumnWidth;
        addressColumnWidth = width * addressWidthFactor;
        playersColumnX = addressColumnX + addressColumnWidth;
        playersColumnWidth = width * playersWidthFactor;
        mapColumnX = playersColumnX + playersColumnWidth;
        mapColumnWidth = width * mapWidthFactor;
        modeColumnX = mapColumnX + mapColumnWidth;
        modeColumnWidth = width * modeWidthFactor;
        pingColumnX = modeColumnX + modeColumnWidth;
        pingColumnWidth = width * pingWidthFactor;
    }

    private void JoinSelectedLobbyEntry()
    {
        _connectionFlowController.JoinSelectedLobbyEntry();
    }

    private void OpenSelectedLobbyEntryDetails()
    {
        _connectionFlowController.OpenSelectedLobbyEntryDetails();
    }

    private void WatchSelectedLobbyEntry()
    {
        _connectionFlowController.WatchSelectedLobbyEntry();
    }

    private bool CanJoinSelectedLobbyEntry()
    {
        return _connectionFlowController.CanJoinSelectedLobbyEntry();
    }

    private bool CanWatchLobbyBrowserDetails()
    {
        var entry = _lobbyBrowserDetailsEntry;
        if (entry is null
            && _lobbyBrowserSelectedIndex >= 0
            && _lobbyBrowserSelectedIndex < _lobbyBrowserEntries.Count)
        {
            entry = _lobbyBrowserEntries[_lobbyBrowserSelectedIndex];
        }

        return entry is not null && (entry.HasResponse || entry.CanJoinDirectly);
    }

    private IEnumerable<LobbyBrowserTarget> BuildLobbyBrowserTargets()
    {
        return _connectionFlowController.BuildLobbyBrowserTargets();
    }

    private static string FormatServerDetailsTime(int ticks, int tickRate)
    {
        if (ticks <= 0 || tickRate <= 0)
        {
            return "0:00";
        }

        var totalSeconds = (int)Math.Ceiling(ticks / (double)tickRate);
        return $"{totalSeconds / 60}:{totalSeconds % 60:00}";
    }

    private static string FormatServerDetailsClass(byte classId)
    {
        return classId switch
        {
            (byte)PlayerClass.Scout => "Scout",
            (byte)PlayerClass.Soldier => "Soldier",
            (byte)PlayerClass.Pyro => "Pyro",
            (byte)PlayerClass.Demoman => "Demo",
            (byte)PlayerClass.Heavy => "Heavy",
            (byte)PlayerClass.Engineer => "Engie",
            (byte)PlayerClass.Medic => "Medic",
            (byte)PlayerClass.Sniper => "Sniper",
            (byte)PlayerClass.Spy => "Spy",
            _ => "?",
        };
    }
}
