#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void OpenLobbyBrowser()
    {
        _lobbyBrowserOpen = true;
        _manualConnectOpen = false;
        _optionsMenuOpen = false;
        _pluginOptionsMenuOpen = false;
        _creditsOpen = false;
        _editingPlayerName = false;
        _editingConnectHost = false;
        _editingConnectPort = false;
        _lobbyBrowserSelectedIndex = -1;
        _lobbyBrowserHoverIndex = -1;
        RefreshLobbyBrowser();
    }

    private void CloseLobbyBrowser(bool clearStatus)
    {
        _lobbyBrowserOpen = false;
        _lobbyBrowserHoverIndex = -1;
        CloseLobbyBrowserLobbyClient();
        if (clearStatus)
        {
            _menuStatusMessage = string.Empty;
        }
    }

    private void RefreshLobbyBrowser()
    {
        EnsureLobbyBrowserClient();
        _lobbyBrowserEntries.Clear();
        StartLobbyBrowserLobbyRequest();

        foreach (var target in BuildLobbyBrowserTargets())
        {
            AddLobbyBrowserEntry(target.DisplayName, target.Host, target.Port, isPrivate: false, isLobbyEntry: false);
        }

        _lobbyBrowserSelectedIndex = _lobbyBrowserEntries.Count > 0 ? 0 : -1;
        _menuStatusMessage = _lobbyBrowserEntries.Count > 0
            ? "Refreshing server list..."
            : _lobbyBrowserLobbyClient is not null
                ? "Contacting lobby server..."
                : "No browser targets yet. Use Join (manual) once to seed one.";
    }

    private void UpdateLobbyBrowserState(KeyboardState keyboard, MouseState mouse)
    {
        UpdateLobbyBrowserResponses();
        GetLobbyBrowserLayout(
            out _,
            out _,
            out var rowBounds,
            out var refreshBounds,
            out var joinBounds,
            out var manualBounds,
            out var backBounds,
            out _);

        if (keyboard.IsKeyDown(Keys.Escape) && !_previousKeyboard.IsKeyDown(Keys.Escape))
        {
            CloseLobbyBrowser(clearStatus: false);
            return;
        }

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

        if (keyboard.IsKeyDown(Keys.Enter) && !_previousKeyboard.IsKeyDown(Keys.Enter))
        {
            JoinSelectedLobbyEntry();
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed)
        {
            return;
        }

        if (_lobbyBrowserHoverIndex >= 0)
        {
            _lobbyBrowserSelectedIndex = _lobbyBrowserHoverIndex;
            return;
        }

        var point = mouse.Position;
        if (refreshBounds.Contains(point))
        {
            RefreshLobbyBrowser();
        }
        else if (joinBounds.Contains(point))
        {
            JoinSelectedLobbyEntry();
        }
        else if (manualBounds.Contains(point))
        {
            CloseLobbyBrowser(clearStatus: false);
            _manualConnectOpen = true;
            _editingConnectHost = true;
            _editingConnectPort = false;
            _menuStatusMessage = string.Empty;
        }
        else if (backBounds.Contains(point))
        {
            CloseLobbyBrowser(clearStatus: false);
        }
    }

    private void DrawLobbyBrowserMenu()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.78f);

        GetLobbyBrowserLayout(
            out var panel,
            out var listBounds,
            out var rows,
            out var refreshBounds,
            out var joinBounds,
            out var manualBounds,
            out var backBounds,
            out var compactLayout);
        GetLobbyBrowserColumnLayout(listBounds, compactLayout, out var nameColumnX, out var nameColumnWidth, out var addressColumnX, out var addressColumnWidth, out var playersColumnX, out var playersColumnWidth, out var mapColumnX, out var mapColumnWidth, out var modeColumnX, out var modeColumnWidth, out var pingColumnX, out var pingColumnWidth);
        var titleScale = compactLayout ? 0.94f : 1f;
        var subtitleScale = compactLayout ? 0.78f : 0.9f;
        var headerScale = compactLayout ? 0.74f : 1f;
        var rowScale = compactLayout ? 0.74f : 0.9f;
        var buttonScale = compactLayout ? 0.86f : 1f;
        _spriteBatch.Draw(_pixel, panel, new Color(34, 35, 39, 235));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, panel.Width, 3), new Color(210, 210, 210));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Bottom - 3, panel.Width, 3), new Color(76, 76, 76));

        DrawBitmapFontText("Join (browser)", new Vector2(panel.X + 24f, panel.Y + 22f), Color.White, titleScale);
        DrawBitmapFontText("Known servers with live status", new Vector2(panel.X + 24f, panel.Y + 48f), new Color(210, 210, 210), subtitleScale);

        var headerY = listBounds.Y - 22f;
        DrawBitmapFontText("NAME", new Vector2(nameColumnX, headerY), Color.White, headerScale);
        DrawBitmapFontText("ADDRESS", new Vector2(addressColumnX, headerY), Color.White, headerScale);
        DrawBitmapFontText("PLAYERS", new Vector2(playersColumnX, headerY), Color.White, headerScale);
        DrawBitmapFontText("MAP", new Vector2(mapColumnX, headerY), Color.White, headerScale);
        DrawBitmapFontText("MODE", new Vector2(modeColumnX, headerY), Color.White, headerScale);
        DrawBitmapFontText("PING", new Vector2(pingColumnX, headerY), Color.White, headerScale);

        _spriteBatch.Draw(_pixel, new Rectangle(listBounds.X, listBounds.Y - 4, listBounds.Width, 2), new Color(120, 120, 120));
        for (var index = 0; index < rows.Length && index < _lobbyBrowserEntries.Count; index += 1)
        {
            var entry = _lobbyBrowserEntries[index];
            var bounds = rows[index];
            var highlighted = index == _lobbyBrowserSelectedIndex;
            var hovered = index == _lobbyBrowserHoverIndex;
            var background = highlighted
                ? new Color(110, 53, 53)
                : hovered
                    ? new Color(64, 66, 72)
                    : new Color(44, 46, 52);
            _spriteBatch.Draw(_pixel, bounds, background);

            var statusColor = entry.HasResponse
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

        DrawMenuButtonScaled(refreshBounds, "Refresh", false, buttonScale);
        DrawMenuButtonScaled(joinBounds, "Join", CanJoinSelectedLobbyEntry(), buttonScale);
        DrawMenuButtonScaled(manualBounds, "Manual", false, buttonScale);
        DrawMenuButtonScaled(backBounds, "Back", false, buttonScale);

        if (!string.IsNullOrWhiteSpace(_menuStatusMessage))
        {
            DrawBitmapFontText(_menuStatusMessage, new Vector2(panel.X + 24f, refreshBounds.Y - (compactLayout ? 26f : 30f)), new Color(230, 220, 180), subtitleScale);
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

        var contentTop = panel.Y + (compactLayout ? 106 : 132);
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
        if (!CanJoinSelectedLobbyEntry())
        {
            _menuStatusMessage = "Select an online server first.";
            return;
        }

        var entry = _lobbyBrowserEntries[_lobbyBrowserSelectedIndex];
        TryConnectToServer(entry.Host, entry.Port, addConsoleFeedback: false);
    }

    private bool CanJoinSelectedLobbyEntry()
    {
        return _lobbyBrowserSelectedIndex >= 0
            && _lobbyBrowserSelectedIndex < _lobbyBrowserEntries.Count
            && _lobbyBrowserEntries[_lobbyBrowserSelectedIndex].HasResponse;
    }

    private IEnumerable<LobbyBrowserTarget> BuildLobbyBrowserTargets()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var target in new[]
                 {
                     new LobbyBrowserTarget("Localhost", "127.0.0.1", 8190),
                     new LobbyBrowserTarget("Manual target", _connectHostBuffer.Trim(), TryParseBrowserPort(_connectPortBuffer)),
                     new LobbyBrowserTarget("Recent", _recentConnectHost ?? string.Empty, _recentConnectPort),
                 })
        {
            if (string.IsNullOrWhiteSpace(target.Host) || target.Port <= 0)
            {
                continue;
            }

            var key = $"{target.Host}:{target.Port}";
            if (seen.Add(key))
            {
                yield return target;
            }
        }
    }

    private static int TryParseBrowserPort(string text)
    {
        return int.TryParse(text.Trim(), out var port) && port is > 0 and <= 65535 ? port : 0;
    }
}
