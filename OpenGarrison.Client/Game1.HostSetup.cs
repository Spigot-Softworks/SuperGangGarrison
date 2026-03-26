#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void OpenHostSetupMenu()
    {
        _hostSetupOpen = true;
        _hostSetupHoverIndex = -1;
        _hostSetupTab = HostSetupTab.Settings;
        _hostSetupEditField = HostSetupEditField.ServerName;
        _menuStatusMessage = string.Empty;
        _manualConnectOpen = false;
        CloseLobbyBrowser(clearStatus: false);
        _optionsMenuOpen = false;
        _pluginOptionsMenuOpen = false;
        _creditsOpen = false;
        _editingPlayerName = false;

        if (string.IsNullOrWhiteSpace(_hostServerNameBuffer))
        {
            _hostServerNameBuffer = "My Server";
        }

        if (string.IsNullOrWhiteSpace(_hostPortBuffer))
        {
            _hostPortBuffer = "8190";
        }

        if (string.IsNullOrWhiteSpace(_hostSlotsBuffer))
        {
            _hostSlotsBuffer = "10";
        }

        if (string.IsNullOrWhiteSpace(_hostTimeLimitBuffer))
        {
            _hostTimeLimitBuffer = "15";
        }

        if (string.IsNullOrWhiteSpace(_hostCapLimitBuffer))
        {
            _hostCapLimitBuffer = "5";
        }

        if (string.IsNullOrWhiteSpace(_hostRespawnSecondsBuffer))
        {
            _hostRespawnSecondsBuffer = "5";
        }

        _hostMapEntries = BuildHostSetupMapEntries();
        if (_hostMapEntries.Count == 0)
        {
            _hostMapIndex = 0;
            return;
        }

        var configuredStartMapName = _clientSettings.HostDefaults.GetFirstIncludedMapLevelName();
        if (!SelectHostMapEntry(configuredStartMapName))
        {
            _hostMapIndex = FindDefaultHostMapIndex();
        }
    }

    private void UpdateHostSetupMenu(MouseState mouse)
    {
        GetHostSetupLayout(
            out var panel,
            out var listBounds,
            out var toggleBounds,
            out var moveUpBounds,
            out var moveDownBounds,
            out var serverNameBounds,
            out var portBounds,
            out var slotsBounds,
            out var passwordBounds,
            out var rotationFileBounds,
            out var timeLimitBounds,
            out var capLimitBounds,
            out var respawnBounds,
            out var lobbyBounds,
            out var autoBalanceBounds,
            out var hostBounds,
            out var backBounds);
        var compactLayout = IsCompactHostSetupLayout(panel);
        var listHeaderHeight = compactLayout ? 18 : 20;
        var rowHeight = compactLayout ? 20 : 28;
        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;

        if (IsServerLauncherMode)
        {
            GetServerLauncherTabBounds(panel, out var settingsTabBounds, out var consoleTabBounds);
            if (clickPressed)
            {
                if (settingsTabBounds.Contains(mouse.Position))
                {
                    _hostSetupTab = HostSetupTab.Settings;
                    _hostSetupEditField = HostSetupEditField.ServerName;
                    return;
                }

                if (consoleTabBounds.Contains(mouse.Position))
                {
                    _hostSetupTab = HostSetupTab.ServerConsole;
                    _hostSetupEditField = HostSetupEditField.ServerConsoleCommand;
                    return;
                }
            }
        }

        if (IsServerLauncherMode && _hostSetupTab == HostSetupTab.ServerConsole)
        {
            GetHostedServerConsoleLayout(
                panel,
                out _,
                out _,
                out var commandBounds,
                out var sendBounds,
                out var clearBounds,
                out var statusCommandBounds,
                out var playersCommandBounds,
                out var rotationCommandBounds,
                out var helpCommandBounds,
                out hostBounds,
                out backBounds);
            var terminalBounds = GetHostSetupTerminalButtonBounds(panel);

            if (!clickPressed)
            {
                return;
            }

            if (commandBounds.Contains(mouse.Position))
            {
                _hostSetupEditField = HostSetupEditField.ServerConsoleCommand;
                return;
            }

            if (sendBounds.Contains(mouse.Position))
            {
                ExecuteHostedServerCommandFromUi(_hostedServerCommandInput);
                return;
            }

            if (clearBounds.Contains(mouse.Position))
            {
                ClearHostedServerConsoleView();
                _menuStatusMessage = "Console view cleared.";
                return;
            }

            if (statusCommandBounds.Contains(mouse.Position))
            {
                ExecuteHostedServerCommandFromUi("status");
                return;
            }

            if (playersCommandBounds.Contains(mouse.Position))
            {
                ExecuteHostedServerCommandFromUi("players");
                return;
            }

            if (rotationCommandBounds.Contains(mouse.Position))
            {
                ExecuteHostedServerCommandFromUi("rotation");
                return;
            }

            if (helpCommandBounds.Contains(mouse.Position))
            {
                ExecuteHostedServerCommandFromUi("help");
                return;
            }

            if (!IsHostedServerRunning && hostBounds.Contains(mouse.Position))
            {
                TryHostFromSetup();
            }
            else if (!IsHostedServerRunning && terminalBounds.Contains(mouse.Position))
            {
                TryHostFromSetup(runInTerminal: true);
            }
            else if (backBounds.Contains(mouse.Position))
            {
                if (!TryHandleServerLauncherBackAction())
                {
                    _hostSetupOpen = false;
                    _hostSetupEditField = HostSetupEditField.None;
                }
            }

            return;
        }

        var launchTerminalBounds = GetHostSetupTerminalButtonBounds(panel);

        _hostSetupHoverIndex = -1;
        var listRowsBounds = new Rectangle(listBounds.X, listBounds.Y + listHeaderHeight, listBounds.Width, listBounds.Height - listHeaderHeight);
        if (listRowsBounds.Contains(mouse.Position))
        {
            var row = (mouse.Y - listRowsBounds.Y) / rowHeight;
            if (row >= 0 && row < _hostMapEntries.Count)
            {
                _hostSetupHoverIndex = row;
            }
        }

        if (!clickPressed)
        {
            return;
        }

        if (serverNameBounds.Contains(mouse.Position))
        {
            _hostSetupEditField = HostSetupEditField.ServerName;
            return;
        }

        if (portBounds.Contains(mouse.Position))
        {
            _hostSetupEditField = HostSetupEditField.Port;
            return;
        }

        if (slotsBounds.Contains(mouse.Position))
        {
            _hostSetupEditField = HostSetupEditField.Slots;
            return;
        }

        if (passwordBounds.Contains(mouse.Position))
        {
            _hostSetupEditField = HostSetupEditField.Password;
            return;
        }

        if (rotationFileBounds.Contains(mouse.Position))
        {
            _hostSetupEditField = HostSetupEditField.MapRotationFile;
            return;
        }

        if (timeLimitBounds.Contains(mouse.Position))
        {
            _hostSetupEditField = HostSetupEditField.TimeLimit;
            return;
        }

        if (capLimitBounds.Contains(mouse.Position))
        {
            _hostSetupEditField = HostSetupEditField.CapLimit;
            return;
        }

        if (respawnBounds.Contains(mouse.Position))
        {
            _hostSetupEditField = HostSetupEditField.RespawnSeconds;
            return;
        }

        if (_hostSetupHoverIndex >= 0 && listRowsBounds.Contains(mouse.Position))
        {
            _hostMapIndex = _hostSetupHoverIndex;
            _hostSetupEditField = HostSetupEditField.None;
            return;
        }

        if (toggleBounds.Contains(mouse.Position))
        {
            ToggleSelectedHostMap();
            return;
        }

        if (moveUpBounds.Contains(mouse.Position))
        {
            MoveSelectedHostMap(-1);
            return;
        }

        if (moveDownBounds.Contains(mouse.Position))
        {
            MoveSelectedHostMap(1);
            return;
        }

        if (lobbyBounds.Contains(mouse.Position))
        {
            _hostLobbyAnnounceEnabled = !_hostLobbyAnnounceEnabled;
            return;
        }

        if (autoBalanceBounds.Contains(mouse.Position))
        {
            _hostAutoBalanceEnabled = !_hostAutoBalanceEnabled;
            return;
        }

        if (!IsHostedServerRunning && hostBounds.Contains(mouse.Position))
        {
            TryHostFromSetup();
        }
        else if (IsServerLauncherMode && !IsHostedServerRunning && launchTerminalBounds.Contains(mouse.Position))
        {
            TryHostFromSetup(runInTerminal: true);
        }
        else if (backBounds.Contains(mouse.Position))
        {
            if (!TryHandleServerLauncherBackAction())
            {
                _hostSetupOpen = false;
                _hostSetupEditField = HostSetupEditField.None;
            }
        }
    }

    private void DrawHostSetupMenu()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.78f);

        GetHostSetupLayout(
            out var panel,
            out var listBounds,
            out var toggleBounds,
            out var moveUpBounds,
            out var moveDownBounds,
            out var serverNameBounds,
            out var portBounds,
            out var slotsBounds,
            out var passwordBounds,
            out var rotationFileBounds,
            out var timeLimitBounds,
            out var capLimitBounds,
            out var respawnBounds,
            out var lobbyBounds,
            out var autoBalanceBounds,
            out var hostBounds,
            out var backBounds);
        var compactLayout = IsCompactHostSetupLayout(panel);
        var titleScale = compactLayout ? 0.92f : 1f;
        var subtitleScale = compactLayout ? 0.78f : 0.9f;
        var headerScale = compactLayout ? 0.72f : 0.8f;
        var rowScale = compactLayout ? 0.78f : 0.9f;
        var fieldLabelScale = compactLayout ? 0.74f : 0.9f;
        var infoScale = compactLayout ? 0.74f : 0.85f;
        var inputScale = compactLayout ? 0.86f : 1f;
        var buttonScale = compactLayout ? 0.84f : 1f;
        var statusPosition = GetHostSetupStatusPosition(panel);
        _spriteBatch.Draw(_pixel, panel, new Color(34, 35, 39, 235));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, panel.Width, 3), new Color(210, 210, 210));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Bottom - 3, panel.Width, 3), new Color(76, 76, 76));

        DrawBitmapFontText(GetHostSetupTitle(), new Vector2(panel.X + 28f, panel.Y + 22f), Color.White, titleScale);
        DrawBitmapFontText(GetHostSetupSubtitle(), new Vector2(panel.X + 28f, panel.Y + 48f), new Color(200, 200, 200), subtitleScale);
        if (!string.IsNullOrWhiteSpace(_menuStatusMessage) && compactLayout)
        {
            DrawBitmapFontText(_menuStatusMessage, statusPosition, new Color(230, 220, 180), 0.82f);
        }

        if (IsServerLauncherMode)
        {
            GetServerLauncherTabBounds(panel, out var settingsTabBounds, out var consoleTabBounds);
            DrawMenuButton(settingsTabBounds, "Settings", _hostSetupTab == HostSetupTab.Settings);
            DrawMenuButton(consoleTabBounds, "Server Console", _hostSetupTab == HostSetupTab.ServerConsole);
        }

        if (IsServerLauncherMode && _hostSetupTab == HostSetupTab.ServerConsole)
        {
            DrawHostedServerConsoleTab(panel);
            return;
        }

        if (compactLayout)
        {
            var listSection = new Rectangle(
                listBounds.X - 10,
                listBounds.Y - 30,
                listBounds.Width + 20,
                moveDownBounds.Bottom - listBounds.Y + 52);
            var settingsSection = new Rectangle(
                serverNameBounds.X - 10,
                listBounds.Y - 30,
                serverNameBounds.Width + 20,
                autoBalanceBounds.Bottom - listBounds.Y + 42);
            _spriteBatch.Draw(_pixel, listSection, new Color(26, 28, 33, 170));
            _spriteBatch.Draw(_pixel, settingsSection, new Color(26, 28, 33, 170));
        }

        DrawBitmapFontText("Stock Map Rotation", new Vector2(listBounds.X, listBounds.Y - 24f), Color.White, compactLayout ? 0.88f : 0.95f);
        DrawBitmapFontText("ORDER", new Vector2(listBounds.X + 10f, listBounds.Y - 2f), new Color(210, 210, 210), headerScale);
        DrawBitmapFontText("MAP", new Vector2(listBounds.X + (compactLayout ? 54f : 78f), listBounds.Y - 2f), new Color(210, 210, 210), headerScale);
        DrawBitmapFontText("MODE", new Vector2(listBounds.Right - (compactLayout ? 98f : 112f), listBounds.Y - 2f), new Color(210, 210, 210), headerScale);
        DrawBitmapFontText("ON", new Vector2(listBounds.Right - (compactLayout ? 38f : 48f), listBounds.Y - 2f), new Color(210, 210, 210), headerScale);

        var listHeaderHeight = compactLayout ? 18 : 20;
        var rowHeight = compactLayout ? 20 : 28;
        for (var index = 0; index < _hostMapEntries.Count; index += 1)
        {
            var entry = _hostMapEntries[index];
            var rowBounds = new Rectangle(listBounds.X - 6, listBounds.Y + listHeaderHeight + (index * rowHeight), listBounds.Width + 12, rowHeight - 2);
            if (index == _hostMapIndex)
            {
                _spriteBatch.Draw(_pixel, rowBounds, new Color(90, 64, 64));
            }
            else if (index == _hostSetupHoverIndex)
            {
                _spriteBatch.Draw(_pixel, rowBounds, new Color(60, 60, 70));
            }
            else
            {
                _spriteBatch.Draw(_pixel, rowBounds, new Color(44, 46, 52, 170));
            }

            var modeLabel = entry.Mode switch
            {
                GameModeKind.Arena => "Arena",
                GameModeKind.ControlPoint => "CP",
                GameModeKind.Generator => "Gen",
                _ => "CTF",
            };
            var orderLabel = entry.Order > 0 ? $"#{entry.Order}" : "--";
            var enabledLabel = entry.Order > 0 ? "ON" : "OFF";
            var enabledColor = entry.Order > 0 ? new Color(178, 228, 155) : new Color(140, 140, 140);

            var rowTextY = rowBounds.Y + (compactLayout ? 4f : 6f);
            DrawBitmapFontText(orderLabel, new Vector2(listBounds.X + 10f, rowTextY), Color.White, rowScale);
            DrawBitmapFontText(entry.DisplayName, new Vector2(listBounds.X + (compactLayout ? 54f : 78f), rowTextY), Color.White, rowScale);
            DrawBitmapFontText(modeLabel, new Vector2(listBounds.Right - (compactLayout ? 98f : 112f), rowTextY), new Color(210, 210, 210), rowScale);
            DrawBitmapFontText(enabledLabel, new Vector2(listBounds.Right - (compactLayout ? 40f : 50f), rowTextY), enabledColor, rowScale);
        }

        var selectedMap = GetSelectedHostMapEntry();
        var selectedIncluded = selectedMap is not null && selectedMap.Order > 0;
        DrawMenuButtonScaled(toggleBounds, selectedIncluded ? "Exclude" : "Include", selectedIncluded, buttonScale);
        DrawMenuButtonScaled(moveUpBounds, "Move Up", false, buttonScale);
        DrawMenuButtonScaled(moveDownBounds, "Move Down", false, buttonScale);

        var stockRotationLabel = GetHostStockRotationSummary(compactLayout ? 3 : 4);
        DrawBitmapFontText(stockRotationLabel, new Vector2(listBounds.X, toggleBounds.Bottom + (compactLayout ? 12f : 18f)), new Color(220, 220, 220), compactLayout ? 0.74f : 0.85f);
        if (!compactLayout && !string.IsNullOrWhiteSpace(_hostMapRotationFileBuffer))
        {
            DrawBitmapFontText("Custom rotation file overrides the stock list below.", new Vector2(listBounds.X, toggleBounds.Bottom + 40f), new Color(226, 204, 164), 0.82f);
        }

        var labelColor = new Color(210, 210, 210);
        DrawBitmapFontText("Server Name", new Vector2(serverNameBounds.X, serverNameBounds.Y - 16f), labelColor, fieldLabelScale);
        DrawMenuInputBoxScaled(serverNameBounds, _hostServerNameBuffer, _hostSetupEditField == HostSetupEditField.ServerName, inputScale);

        DrawBitmapFontText(compactLayout ? "Password" : "Password", new Vector2(passwordBounds.X, passwordBounds.Y - 16f), labelColor, fieldLabelScale);
        var maskedPassword = string.IsNullOrEmpty(_hostPasswordBuffer) ? string.Empty : new string('*', _hostPasswordBuffer.Length);
        DrawMenuInputBoxScaled(passwordBounds, maskedPassword, _hostSetupEditField == HostSetupEditField.Password, inputScale);

        DrawBitmapFontText(compactLayout ? "Rotation File" : "Custom Rotation File", new Vector2(rotationFileBounds.X, rotationFileBounds.Y - 16f), labelColor, fieldLabelScale);
        DrawMenuInputBoxScaled(rotationFileBounds, _hostMapRotationFileBuffer, _hostSetupEditField == HostSetupEditField.MapRotationFile, inputScale);

        DrawBitmapFontText("Port", new Vector2(portBounds.X, portBounds.Y - 16f), labelColor, fieldLabelScale);
        DrawMenuInputBoxScaled(portBounds, _hostPortBuffer, _hostSetupEditField == HostSetupEditField.Port, inputScale);

        DrawBitmapFontText("Slots", new Vector2(slotsBounds.X, slotsBounds.Y - 16f), labelColor, fieldLabelScale);
        DrawMenuInputBoxScaled(slotsBounds, _hostSlotsBuffer, _hostSetupEditField == HostSetupEditField.Slots, inputScale);

        DrawBitmapFontText(compactLayout ? "Time (mins)" : "Time Limit (mins)", new Vector2(timeLimitBounds.X, timeLimitBounds.Y - 16f), labelColor, fieldLabelScale);
        DrawMenuInputBoxScaled(timeLimitBounds, _hostTimeLimitBuffer, _hostSetupEditField == HostSetupEditField.TimeLimit, inputScale);

        DrawBitmapFontText("Cap Limit", new Vector2(capLimitBounds.X, capLimitBounds.Y - 16f), labelColor, fieldLabelScale);
        DrawMenuInputBoxScaled(capLimitBounds, _hostCapLimitBuffer, _hostSetupEditField == HostSetupEditField.CapLimit, inputScale);

        DrawBitmapFontText(compactLayout ? "Respawn (sec)" : "Respawn Time (secs)", new Vector2(respawnBounds.X, respawnBounds.Y - 16f), labelColor, fieldLabelScale);
        DrawMenuInputBoxScaled(respawnBounds, _hostRespawnSecondsBuffer, _hostSetupEditField == HostSetupEditField.RespawnSeconds, inputScale);

        DrawMenuButtonScaled(lobbyBounds, _hostLobbyAnnounceEnabled ? "Lobby Announce: On" : "Lobby Announce: Off", _hostLobbyAnnounceEnabled, buttonScale);
        DrawMenuButtonScaled(autoBalanceBounds, _hostAutoBalanceEnabled ? "Auto-balance: On" : "Auto-balance: Off", _hostAutoBalanceEnabled, buttonScale);

        DrawMenuButtonScaled(hostBounds, GetHostSetupPrimaryButtonLabel(), false, buttonScale);
        DrawMenuButtonScaled(backBounds, GetHostSetupSecondaryButtonLabel(), IsServerLauncherMode && IsHostedServerRunning, buttonScale);
        if (IsServerLauncherMode && !IsHostedServerRunning)
        {
            DrawMenuButtonScaled(GetHostSetupTerminalButtonBounds(panel), "Run In Terminal", false, buttonScale);
        }

        if (IsServerLauncherMode && IsHostedServerRunning)
        {
            DrawBitmapFontText(
                "Use Stop Server to end the active dedicated server session.",
                new Vector2(panel.X + 28f, panel.Bottom - 62f),
                new Color(210, 210, 210),
                infoScale);
        }

        if (!compactLayout && !string.IsNullOrWhiteSpace(_menuStatusMessage))
        {
            DrawBitmapFontText(_menuStatusMessage, statusPosition, new Color(230, 220, 180), 1f);
        }
    }

    private void DrawHostedServerConsoleTab(Rectangle panel)
    {
        GetHostedServerConsoleLayout(
            panel,
            out var logBounds,
            out var summaryBounds,
            out var commandBounds,
            out var sendBounds,
            out var clearBounds,
            out var statusCommandBounds,
            out var playersCommandBounds,
            out var rotationCommandBounds,
            out var helpCommandBounds,
            out var hostBounds,
            out var backBounds);

        _spriteBatch.Draw(_pixel, logBounds, new Color(24, 25, 30, 230));
        _spriteBatch.Draw(_pixel, summaryBounds, new Color(28, 30, 34, 230));
        DrawBitmapFontText("Recent Output", new Vector2(logBounds.X + 10f, logBounds.Y + 8f), Color.White, 0.95f);
        DrawBitmapFontText("Live Status", new Vector2(summaryBounds.X + 10f, summaryBounds.Y + 8f), Color.White, 0.95f);

        var consoleLines = GetHostedServerConsoleLinesSnapshot();
        var availableLineCount = Math.Max(1, (logBounds.Height - 38) / 18);
        var firstLineIndex = Math.Max(0, consoleLines.Count - availableLineCount);
        var drawY = logBounds.Y + 30f;
        if (consoleLines.Count == 0)
        {
            _spriteBatch.DrawString(_consoleFont, "No server output yet.", new Vector2(logBounds.X + 12f, drawY), new Color(200, 200, 200));
        }
        else
        {
            for (var index = firstLineIndex; index < consoleLines.Count; index += 1)
            {
                var line = TrimConsoleText(consoleLines[index], logBounds.Width - 24f);
                _spriteBatch.DrawString(_consoleFont, line, new Vector2(logBounds.X + 12f, drawY), new Color(230, 232, 235));
                drawY += 18f;
            }
        }

        var summaryRows = new (string Label, string Value)[]
        {
            ("Server", _hostedServerStatusName),
            ("Port", _hostedServerStatusPort),
            ("Players", _hostedServerStatusPlayers),
            ("Lobby", _hostedServerStatusLobby),
            ("Map", _hostedServerStatusMap),
            ("Rules", _hostedServerStatusRules),
            ("Runtime", _hostedServerStatusRuntime),
            ("World", _hostedServerStatusWorld),
        };
        var compactLayout = IsCompactHostSetupLayout(panel);
        var summaryRowGap = compactLayout ? 4 : 5;
        var availableSummaryHeight = Math.Max(1, summaryBounds.Height - 32 - (summaryRowGap * (summaryRows.Length - 1)));
        var summaryRowHeight = Math.Max(compactLayout ? 24 : 40, availableSummaryHeight / summaryRows.Length);

        for (var index = 0; index < summaryRows.Length; index += 1)
        {
            var rowBounds = new Rectangle(
                summaryBounds.X + 10,
                summaryBounds.Y + 32 + (index * (summaryRowHeight + summaryRowGap)),
                summaryBounds.Width - 20,
                summaryRowHeight);
            DrawHostedServerSummaryRow(rowBounds, summaryRows[index].Label, summaryRows[index].Value);
        }

        DrawBitmapFontText("Console Command", new Vector2(commandBounds.X, commandBounds.Y - 20f), new Color(210, 210, 210), 0.9f);
        DrawMenuInputBox(commandBounds, _hostedServerCommandInput, _hostSetupEditField == HostSetupEditField.ServerConsoleCommand);
        DrawMenuButton(sendBounds, "Send", false);
        DrawMenuButton(clearBounds, "Clear", false);
        DrawMenuButton(statusCommandBounds, "Status", false);
        DrawMenuButton(playersCommandBounds, "Players", false);
        DrawMenuButton(rotationCommandBounds, "Rotation", false);
        DrawMenuButton(helpCommandBounds, "Help", false);
        DrawMenuButton(hostBounds, GetHostSetupPrimaryButtonLabel(), false);
        DrawMenuButton(backBounds, GetHostSetupSecondaryButtonLabel(), IsServerLauncherMode && IsHostedServerRunning);
        if (!IsHostedServerRunning)
        {
            DrawMenuButton(GetHostSetupTerminalButtonBounds(panel), "Run In Terminal", false);
        }

        DrawBitmapFontText(
            "Use Enter or Send to dispatch a server command to the dedicated process.",
            new Vector2(panel.X + 28f, panel.Bottom - 90f),
            new Color(210, 210, 210),
            0.82f);

        if (!string.IsNullOrWhiteSpace(_menuStatusMessage))
        {
            DrawBitmapFontText(_menuStatusMessage, new Vector2(panel.X + 28f, panel.Bottom - 38f), new Color(230, 220, 180), 1f);
        }
    }

    private void DrawHostedServerSummaryRow(Rectangle bounds, string label, string value)
    {
        _spriteBatch.Draw(_pixel, bounds, new Color(44, 46, 52, 180));
        var compactBounds = bounds.Height < 34;
        DrawBitmapFontText(label.ToUpperInvariant(), new Vector2(bounds.X + 8f, bounds.Y + 4f), new Color(210, 210, 210), compactBounds ? 0.7f : 0.82f);
        var valueY = bounds.Y + MathF.Max(12f, bounds.Height - 18f);
        _spriteBatch.DrawString(_consoleFont, TrimConsoleText(value, bounds.Width - 16f), new Vector2(bounds.X + 10f, valueY), Color.White);
    }

    private void ExecuteHostedServerCommandFromUi(string command)
    {
        if (TrySendHostedServerCommand(command, out var error))
        {
            _menuStatusMessage = "Command sent.";
        }
        else
        {
            _menuStatusMessage = error;
        }
    }

    private string TrimConsoleText(string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text) || _consoleFont.MeasureString(text).X <= maxWidth)
        {
            return text;
        }

        const string ellipsis = "...";
        var trimmed = text;
        while (trimmed.Length > 0 && _consoleFont.MeasureString(trimmed + ellipsis).X > maxWidth)
        {
            trimmed = trimmed[..^1];
        }

        return trimmed.Length == 0 ? ellipsis : trimmed + ellipsis;
    }

    private static void GetServerLauncherTabBounds(Rectangle panel, out Rectangle settingsTabBounds, out Rectangle consoleTabBounds)
    {
        settingsTabBounds = new Rectangle(panel.Right - 332, panel.Y + 18, 146, 32);
        consoleTabBounds = new Rectangle(panel.Right - 176, panel.Y + 18, 146, 32);
    }

    private static void GetHostedServerConsoleLayout(
        Rectangle panel,
        out Rectangle logBounds,
        out Rectangle summaryBounds,
        out Rectangle commandBounds,
        out Rectangle sendBounds,
        out Rectangle clearBounds,
        out Rectangle statusCommandBounds,
        out Rectangle playersCommandBounds,
        out Rectangle rotationCommandBounds,
        out Rectangle helpCommandBounds,
        out Rectangle hostBounds,
        out Rectangle backBounds)
    {
        var compactLayout = IsCompactHostSetupLayout(panel);
        var padding = compactLayout ? 20 : 28;
        var sectionGap = compactLayout ? 12 : 18;
        var actionGap = compactLayout ? 12 : 20;
        var actionButtonHeight = compactLayout ? 38 : 42;
        var actionPaddingBottom = compactLayout ? 12 : 20;
        var actionButtonWidth = compactLayout ? 124 : 140;
        var commandButtonHeight = compactLayout ? 30 : 34;
        var commandButtonGap = compactLayout ? 8 : 10;
        var contentTop = panel.Y + (compactLayout ? 84 : 96);

        backBounds = new Rectangle(
            panel.Right - padding - actionButtonWidth,
            panel.Bottom - actionPaddingBottom - actionButtonHeight,
            actionButtonWidth,
            actionButtonHeight);
        hostBounds = new Rectangle(
            backBounds.X - actionGap - actionButtonWidth,
            backBounds.Y,
            actionButtonWidth,
            actionButtonHeight);

        var commandY = backBounds.Y - (compactLayout ? 46 : 52);
        var availableWidth = panel.Width - (padding * 2) - sectionGap;
        var logWidth = compactLayout
            ? Math.Clamp((int)MathF.Floor(availableWidth * 0.62f), 360, Math.Max(360, availableWidth - 190))
            : 574;
        var summaryWidth = Math.Max(compactLayout ? 190 : 220, availableWidth - logWidth);
        logWidth = availableWidth - summaryWidth;
        var contentHeight = Math.Max(compactLayout ? 220 : 320, commandY - contentTop - 12);

        logBounds = new Rectangle(panel.X + padding, contentTop, logWidth, contentHeight);
        summaryBounds = new Rectangle(logBounds.Right + sectionGap, contentTop, summaryWidth, contentHeight);
        commandBounds = new Rectangle(logBounds.X, commandY, Math.Max(180, logBounds.Width - (compactLayout ? 168 : 178)), commandButtonHeight);
        sendBounds = new Rectangle(commandBounds.Right + commandButtonGap, commandBounds.Y, compactLayout ? 68 : 78, commandButtonHeight);
        clearBounds = new Rectangle(sendBounds.Right + commandButtonGap, commandBounds.Y, compactLayout ? 68 : 78, commandButtonHeight);
        statusCommandBounds = new Rectangle(summaryBounds.X, commandBounds.Y, compactLayout ? 58 : 64, commandButtonHeight);
        playersCommandBounds = new Rectangle(statusCommandBounds.Right + 8, statusCommandBounds.Y, compactLayout ? 66 : 72, commandButtonHeight);
        rotationCommandBounds = new Rectangle(playersCommandBounds.Right + 8, statusCommandBounds.Y, compactLayout ? 72 : 78, commandButtonHeight);
        helpCommandBounds = new Rectangle(rotationCommandBounds.Right + 8, statusCommandBounds.Y, compactLayout ? 56 : 60, commandButtonHeight);
    }

    private static Rectangle GetHostSetupTerminalButtonBounds(Rectangle panel)
    {
        var compactLayout = IsCompactHostSetupLayout(panel);
        var padding = compactLayout ? 18 : 36;
        var actionGap = compactLayout ? 12 : 20;
        var actionButtonHeight = compactLayout ? 36 : 42;
        var actionPaddingBottom = compactLayout ? 18 : 20;
        var actionButtonWidth = compactLayout ? 120 : 140;
        var terminalWidth = compactLayout ? 136 : 150;
        var y = panel.Bottom - actionPaddingBottom - actionButtonHeight;
        var backX = panel.Right - padding - actionButtonWidth;
        var hostX = backX - actionGap - actionButtonWidth;
        return new Rectangle(hostX - actionGap - terminalWidth, y, terminalWidth, actionButtonHeight);
    }

    private void GetHostSetupLayout(
        out Rectangle panel,
        out Rectangle listBounds,
        out Rectangle toggleBounds,
        out Rectangle moveUpBounds,
        out Rectangle moveDownBounds,
        out Rectangle serverNameBounds,
        out Rectangle portBounds,
        out Rectangle slotsBounds,
        out Rectangle passwordBounds,
        out Rectangle rotationFileBounds,
        out Rectangle timeLimitBounds,
        out Rectangle capLimitBounds,
        out Rectangle respawnBounds,
        out Rectangle lobbyBounds,
        out Rectangle autoBalanceBounds,
        out Rectangle hostBounds,
        out Rectangle backBounds)
    {
        var compactViewport = ViewportWidth <= 864 || ViewportHeight <= 624;
        var panelWidth = compactViewport
            ? Math.Min(760, ViewportWidth - 40)
            : Math.Min(960, ViewportWidth - 48);
        var panelHeight = compactViewport
            ? Math.Min(520, ViewportHeight - 40)
            : Math.Min(620, ViewportHeight - 48);
        panel = new Rectangle(
            (ViewportWidth - panelWidth) / 2,
            (ViewportHeight - panelHeight) / 2,
            panelWidth,
            panelHeight);

        var compactLayout = IsCompactHostSetupLayout(panel);
        if (compactLayout)
        {
            var padding = 18;
            var listHeaderHeight = 18;
            var rowHeight = 20;
            var listWidth = Math.Min(288, Math.Max(248, (panel.Width - (padding * 2) - 20) / 2));
            var listX = panel.X + padding;
            var contentTop = panel.Y + (IsServerLauncherMode ? 92 : 84);
            var availableListHeight = Math.Max(170, panel.Bottom - 156 - contentTop);
            var maxListHeight = listHeaderHeight + (Math.Max(1, _hostMapEntries.Count) * rowHeight);
            var listHeight = Math.Min(Math.Min(236, maxListHeight), availableListHeight);
            listBounds = new Rectangle(listX, contentTop, listWidth, listHeight);

            var listButtonGap = 8;
            var listButtonHeight = 28;
            var listButtonWidth = Math.Max(78, (listBounds.Width - (listButtonGap * 2)) / 3);
            toggleBounds = new Rectangle(listBounds.X, listBounds.Bottom + 10, listButtonWidth, listButtonHeight);
            moveUpBounds = new Rectangle(toggleBounds.Right + listButtonGap, toggleBounds.Y, listButtonWidth, listButtonHeight);
            moveDownBounds = new Rectangle(moveUpBounds.Right + listButtonGap, toggleBounds.Y, listButtonWidth, listButtonHeight);

            var fieldX = listBounds.Right + 20;
            var fieldWidth = panel.Right - fieldX - padding;
            var fieldHeight = 26;
            var smallGap = 8;
            var smallRowSpacing = 44;

            serverNameBounds = new Rectangle(fieldX, contentTop, fieldWidth, fieldHeight);
            passwordBounds = new Rectangle(fieldX, serverNameBounds.Bottom + 10, fieldWidth, fieldHeight);
            rotationFileBounds = new Rectangle(fieldX, passwordBounds.Bottom + 10, fieldWidth, fieldHeight);

            var tripleFieldWidth = Math.Max(70, (fieldWidth - (smallGap * 2)) / 3);
            portBounds = new Rectangle(fieldX, rotationFileBounds.Bottom + 18, tripleFieldWidth, fieldHeight);
            slotsBounds = new Rectangle(portBounds.Right + smallGap, portBounds.Y, tripleFieldWidth, fieldHeight);
            timeLimitBounds = new Rectangle(slotsBounds.Right + smallGap, portBounds.Y, fieldWidth - (tripleFieldWidth * 2) - (smallGap * 2), fieldHeight);

            var doubleFieldWidth = Math.Max(100, (fieldWidth - smallGap) / 2);
            capLimitBounds = new Rectangle(fieldX, portBounds.Y + smallRowSpacing, doubleFieldWidth, fieldHeight);
            respawnBounds = new Rectangle(capLimitBounds.Right + smallGap, capLimitBounds.Y, fieldWidth - doubleFieldWidth - smallGap, fieldHeight);

            var listButtonScaleHeight = 28;
            lobbyBounds = new Rectangle(fieldX, capLimitBounds.Bottom + 18, fieldWidth, listButtonScaleHeight);
            autoBalanceBounds = new Rectangle(fieldX, lobbyBounds.Bottom + 6, fieldWidth, listButtonScaleHeight);

            var actionButtonHeight = 36;
            var actionButtonWidth = 120;
            var actionGap = 12;
            var actionPaddingBottom = 18;
            backBounds = new Rectangle(
                panel.Right - padding - actionButtonWidth,
                panel.Bottom - actionPaddingBottom - actionButtonHeight,
                actionButtonWidth,
                actionButtonHeight);
            hostBounds = new Rectangle(
                backBounds.X - actionGap - actionButtonWidth,
                backBounds.Y,
                actionButtonWidth,
                actionButtonHeight);
            return;
        }

        var roomyPadding = 36;
        var roomyInterColumnGap = 46;
        var roomyListWidth = 392;
        var roomyMinFieldWidth = 410;
        var roomyMaxListWidth = panel.Width - (roomyPadding * 2) - roomyInterColumnGap - roomyMinFieldWidth;
        if (roomyMaxListWidth < roomyListWidth)
        {
            roomyListWidth = Math.Max(320, roomyMaxListWidth);
        }

        listBounds = new Rectangle(panel.X + roomyPadding, panel.Y + 96, roomyListWidth, 328);

        toggleBounds = new Rectangle(listBounds.X, listBounds.Bottom + 14, 116, 34);
        moveUpBounds = new Rectangle(toggleBounds.Right + 12, toggleBounds.Y, 116, 34);
        moveDownBounds = new Rectangle(moveUpBounds.Right + 12, toggleBounds.Y, 116, 34);

        var roomyFieldX = listBounds.Right + roomyInterColumnGap;
        var roomyFieldWidth = panel.Right - roomyFieldX - roomyPadding;
        serverNameBounds = new Rectangle(roomyFieldX, panel.Y + 100, roomyFieldWidth, 32);
        portBounds = new Rectangle(roomyFieldX, panel.Y + 150, roomyFieldWidth, 32);
        slotsBounds = new Rectangle(roomyFieldX, panel.Y + 200, roomyFieldWidth, 32);
        passwordBounds = new Rectangle(roomyFieldX, panel.Y + 250, roomyFieldWidth, 32);
        rotationFileBounds = new Rectangle(roomyFieldX, panel.Y + 300, roomyFieldWidth, 32);
        timeLimitBounds = new Rectangle(roomyFieldX, panel.Y + 350, roomyFieldWidth, 32);
        capLimitBounds = new Rectangle(roomyFieldX, panel.Y + 400, roomyFieldWidth, 32);
        respawnBounds = new Rectangle(roomyFieldX, panel.Y + 450, roomyFieldWidth, 32);

        backBounds = new Rectangle(panel.Right - roomyPadding - 140, panel.Bottom - 20 - 42, 140, 42);
        hostBounds = new Rectangle(backBounds.X - 20 - 140, backBounds.Y, 140, 42);
        lobbyBounds = new Rectangle(roomyFieldX, hostBounds.Y - 88, roomyFieldWidth, 34);
        autoBalanceBounds = new Rectangle(roomyFieldX, lobbyBounds.Bottom + 6, roomyFieldWidth, 34);
    }

    private static bool IsCompactHostSetupLayout(Rectangle panel)
    {
        return panel.Width <= 760 || panel.Height <= 520;
    }

    private static Vector2 GetHostSetupStatusPosition(Rectangle panel)
    {
        return IsCompactHostSetupLayout(panel)
            ? new Vector2(panel.X + 28f, panel.Y + 62f)
            : new Vector2(panel.X + 28f, panel.Bottom - 38f);
    }

    private void TryHostFromSetup(bool runInTerminal = false)
    {
        var trimmedRotationFile = _hostMapRotationFileBuffer.Trim();
        if (_hostMapEntries.Count == 0)
        {
            _menuStatusMessage = "No stock maps are available.";
            return;
        }

        if (string.IsNullOrWhiteSpace(trimmedRotationFile)
            && !_hostMapEntries.Any(entry => entry.Order > 0))
        {
            _menuStatusMessage = "Include at least one stock map or set a custom rotation file.";
            return;
        }

        var serverName = _hostServerNameBuffer.Trim();
        if (string.IsNullOrWhiteSpace(serverName))
        {
            _menuStatusMessage = "Server name is required.";
            return;
        }

        if (!int.TryParse(_hostPortBuffer.Trim(), out var port) || port is <= 0 or > 65535)
        {
            _menuStatusMessage = "Port must be 1-65535.";
            return;
        }

        if (!int.TryParse(_hostSlotsBuffer.Trim(), out var maxPlayers)
            || maxPlayers < 1
            || maxPlayers > SimulationWorld.MaxPlayableNetworkPlayers)
        {
            _menuStatusMessage = $"Slots must be 1-{SimulationWorld.MaxPlayableNetworkPlayers}.";
            return;
        }

        if (!int.TryParse(_hostTimeLimitBuffer.Trim(), out var timeLimitMinutes)
            || timeLimitMinutes < 1
            || timeLimitMinutes > 255)
        {
            _menuStatusMessage = "Time limit must be 1-255 minutes.";
            return;
        }

        if (!int.TryParse(_hostCapLimitBuffer.Trim(), out var capLimit)
            || capLimit < 1
            || capLimit > 255)
        {
            _menuStatusMessage = "Cap limit must be 1-255.";
            return;
        }

        if (!int.TryParse(_hostRespawnSecondsBuffer.Trim(), out var respawnSeconds)
            || respawnSeconds < 0
            || respawnSeconds > 255)
        {
            _menuStatusMessage = "Respawn time must be 0-255 seconds.";
            return;
        }

        PersistClientSettings();
        if (IsServerLauncherMode)
        {
            if (runInTerminal)
            {
                BeginDedicatedServerTerminalLaunch(
                    serverName,
                    port,
                    maxPlayers,
                    _hostPasswordBuffer.Trim(),
                    timeLimitMinutes,
                    capLimit,
                    respawnSeconds,
                    _hostLobbyAnnounceEnabled,
                    _hostAutoBalanceEnabled);
            }
            else
            {
                BeginDedicatedServerLaunch(
                    serverName,
                    port,
                    maxPlayers,
                    _hostPasswordBuffer.Trim(),
                    timeLimitMinutes,
                    capLimit,
                    respawnSeconds,
                    _hostLobbyAnnounceEnabled,
                    _hostAutoBalanceEnabled);
            }
            return;
        }

        BeginHostedGame(
            serverName,
            port,
            maxPlayers,
            _hostPasswordBuffer.Trim(),
            timeLimitMinutes,
            capLimit,
            respawnSeconds,
            _hostLobbyAnnounceEnabled,
            _hostAutoBalanceEnabled);
    }

    private List<OpenGarrisonMapRotationEntry> BuildHostSetupMapEntries()
    {
        var configuredEntries = _clientSettings.HostDefaults.StockMapRotation
            .ToDictionary(entry => entry.IniKey, entry => entry, StringComparer.OrdinalIgnoreCase);
        var mergedEntries = new List<OpenGarrisonMapRotationEntry>(OpenGarrisonStockMapCatalog.Definitions.Count);
        foreach (var definition in OpenGarrisonStockMapCatalog.Definitions)
        {
            if (configuredEntries.TryGetValue(definition.IniKey, out var existing))
            {
                mergedEntries.Add(existing.Clone());
            }
            else
            {
                mergedEntries.Add(new OpenGarrisonMapRotationEntry
                {
                    IniKey = definition.IniKey,
                    LevelName = definition.LevelName,
                    DisplayName = definition.DisplayName,
                    Mode = definition.Mode,
                    DefaultOrder = definition.DefaultOrder,
                    Order = definition.DefaultOrder,
                });
            }
        }

        return OpenGarrisonStockMapCatalog.GetOrderedEntries(mergedEntries)
            .Select(entry => entry.Clone())
            .ToList();
    }

    private void ToggleSelectedHostMap()
    {
        var selected = GetSelectedHostMapEntry();
        if (selected is null)
        {
            return;
        }

        if (selected.Order > 0)
        {
            selected.Order = 0;
        }
        else
        {
            selected.Order = _hostMapEntries.Where(entry => entry.Order > 0).Select(entry => entry.Order).DefaultIfEmpty().Max() + 1;
        }

        SortHostMapEntries(selected.LevelName);
        _menuStatusMessage = string.Empty;
    }

    private void MoveSelectedHostMap(int direction)
    {
        var selected = GetSelectedHostMapEntry();
        if (selected is null || selected.Order <= 0)
        {
            return;
        }

        var includedEntries = _hostMapEntries
            .Where(entry => entry.Order > 0)
            .OrderBy(entry => entry.Order)
            .ToList();
        var currentIndex = includedEntries.FindIndex(entry => string.Equals(entry.LevelName, selected.LevelName, StringComparison.OrdinalIgnoreCase));
        var targetIndex = currentIndex + direction;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= includedEntries.Count)
        {
            return;
        }

        var swapTarget = includedEntries[targetIndex];
        (selected.Order, swapTarget.Order) = (swapTarget.Order, selected.Order);
        SortHostMapEntries(selected.LevelName);
        _menuStatusMessage = string.Empty;
    }

    private void SortHostMapEntries(string? selectedLevelName = null)
    {
        var desiredSelection = selectedLevelName ?? GetSelectedHostMapEntry()?.LevelName;
        _hostMapEntries = OpenGarrisonStockMapCatalog.GetOrderedEntries(_hostMapEntries)
            .Select(entry => entry.Clone())
            .ToList();
        if (!SelectHostMapEntry(desiredSelection))
        {
            _hostMapIndex = Math.Clamp(_hostMapIndex, 0, Math.Max(0, _hostMapEntries.Count - 1));
        }
    }

    private bool SelectHostMapEntry(string? levelName)
    {
        if (string.IsNullOrWhiteSpace(levelName))
        {
            return false;
        }

        var index = _hostMapEntries.FindIndex(entry => entry.LevelName.Equals(levelName, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return false;
        }

        _hostMapIndex = index;
        return true;
    }

    private int FindDefaultHostMapIndex()
    {
        var truefortIndex = _hostMapEntries.FindIndex(entry => entry.LevelName.Equals("Truefort", StringComparison.OrdinalIgnoreCase));
        return truefortIndex >= 0 ? truefortIndex : 0;
    }

    private OpenGarrisonMapRotationEntry? GetSelectedHostMapEntry()
    {
        return _hostMapIndex >= 0 && _hostMapIndex < _hostMapEntries.Count
            ? _hostMapEntries[_hostMapIndex]
            : null;
    }

    private string GetHostStockRotationSummary(int previewCount = 4)
    {
        var orderedNames = OpenGarrisonStockMapCatalog.GetOrderedIncludedMapLevelNames(_hostMapEntries);
        if (orderedNames.Count == 0)
        {
            return "Stock rotation: no maps selected.";
        }

        var preview = string.Join(" -> ", orderedNames.Take(Math.Max(1, previewCount)));
        if (orderedNames.Count > Math.Max(1, previewCount))
        {
            preview += " ...";
        }

        return $"Stock rotation: {preview}";
    }
}
