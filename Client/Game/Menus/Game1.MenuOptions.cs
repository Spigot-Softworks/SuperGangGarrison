#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Globalization;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool IsGameplayMenuOpen()
    {
        return _clientPowersOpen
            || _practiceSetupOpen
            || _lastToDiePerkMenuOpen
            || IsLastToDieStageClearOverlayActive()
            || IsLastToDieDeathFocusPresentationActive()
            || IsLastToDieFailureOverlayActive()
            || _inGameMenuOpen
            || _optionsMenuOpen
            || _pluginOptionsMenuOpen
            || _controlsMenuOpen
            || _quitPromptOpen
            || ShouldBlockGameplayForNavEditor();
    }

    private bool IsGameplayInputBlocked()
    {
        return !IsActive
            || IsGameplayMenuOpen()
            || _consoleOpen
            || _chatOpen
            || _teamSelectOpen
            || _classSelectOpen
            || _passwordPromptOpen;
    }

    private void UpdateGameplayMenuState(KeyboardState keyboard, MouseState mouse)
    {
        if (IsLastToDieFailureOverlayActive())
        {
            UpdateLastToDieFailureOverlay(keyboard, mouse);
            return;
        }

        if (IsLastToDieDeathFocusPresentationActive())
        {
            UpdateLastToDieDeathFocusPresentation();
            return;
        }

        if (IsLastToDieStageClearOverlayActive())
        {
            UpdateLastToDieStageClearOverlay(keyboard, mouse);
            return;
        }

        if (_lastToDiePerkMenuOpen)
        {
            UpdateLastToDiePerkMenu(keyboard, mouse);
            return;
        }

        if (_quitPromptOpen)
        {
            UpdateQuitPrompt(keyboard, mouse);
            return;
        }

        if (_controlsMenuOpen)
        {
            UpdateControlsMenu(keyboard, mouse);
            return;
        }

        if (_clientPowersOpen)
        {
            UpdateClientPowersMenu(keyboard, mouse);
            return;
        }

        if (_practiceSetupOpen)
        {
            UpdatePracticeSetupMenu(keyboard, mouse);
            return;
        }

        if (_pluginOptionsMenuOpen)
        {
            UpdatePluginOptionsMenu(keyboard, mouse);
            return;
        }

        if (_optionsMenuOpen)
        {
            UpdateOptionsMenu(keyboard, mouse);
            return;
        }

        if (_inGameMenuOpen)
        {
            UpdateInGameMenu(keyboard, mouse);
        }
    }

    private void OpenOptionsMenu(bool fromGameplay)
    {
        _optionsMenuOpen = true;
        _optionsMenuOpenedFromGameplay = fromGameplay;
        _pluginOptionsMenuOpen = false;
        _pendingPluginOptionsKeyItem = null;
        _selectedPluginOptionsPluginId = null;
        _controlsMenuOpen = false;
        _pendingControlsBinding = null;
        _optionsHoverIndex = -1;
        _pluginOptionsHoverIndex = -1;
        _controlsHoverIndex = -1;
        _editingPlayerName = false;
        _playerNameEditBuffer = _world.LocalPlayer.DisplayName;
    }

    private void CloseOptionsMenu()
    {
        var reopenInGameMenu = _optionsMenuOpenedFromGameplay && !_mainMenuOpen;
        _optionsMenuOpen = false;
        _optionsMenuOpenedFromGameplay = false;
        _pluginOptionsMenuOpen = false;
        _pluginOptionsMenuOpenedFromGameplay = false;
        _pendingPluginOptionsKeyItem = null;
        _selectedPluginOptionsPluginId = null;
        _optionsHoverIndex = -1;
        _pluginOptionsHoverIndex = -1;
        _editingPlayerName = false;
        _playerNameEditBuffer = _world.LocalPlayer.DisplayName;
        if (reopenInGameMenu)
        {
            OpenInGameMenu();
        }
    }

    private void OpenPluginOptionsMenu(bool fromGameplay)
    {
        _pluginOptionsMenuOpen = true;
        _pluginOptionsMenuOpenedFromGameplay = fromGameplay;
        _pendingPluginOptionsKeyItem = null;
        _selectedPluginOptionsPluginId = null;
        _optionsMenuOpen = false;
        _pluginOptionsHoverIndex = -1;
        _pluginOptionsScrollOffset = 0;
        _optionsHoverIndex = -1;
        _editingPlayerName = false;
        _playerNameEditBuffer = _world.LocalPlayer.DisplayName;
    }

    private void ClosePluginOptionsMenu()
    {
        var reopenFromGameplay = _pluginOptionsMenuOpenedFromGameplay;
        _pluginOptionsMenuOpen = false;
        _pluginOptionsMenuOpenedFromGameplay = false;
        _pendingPluginOptionsKeyItem = null;
        _selectedPluginOptionsPluginId = null;
        _pluginOptionsHoverIndex = -1;
        _pluginOptionsScrollOffset = 0;
        OpenOptionsMenu(reopenFromGameplay);
    }

    private void OpenControlsMenu(bool fromGameplay)
    {
        _controlsMenuOpen = true;
        _controlsMenuOpenedFromGameplay = fromGameplay;
        _controlsHoverIndex = -1;
        _pendingControlsBinding = null;
        _optionsMenuOpen = false;
        _pluginOptionsMenuOpen = false;
        _editingPlayerName = false;
    }

    private void CloseControlsMenu()
    {
        var reopenInGameMenu = _controlsMenuOpenedFromGameplay && !_mainMenuOpen;
        _controlsMenuOpen = false;
        _controlsMenuOpenedFromGameplay = false;
        _controlsHoverIndex = -1;
        _pendingControlsBinding = null;

        if (_mainMenuOpen || reopenInGameMenu)
        {
            OpenOptionsMenu(reopenInGameMenu);
        }
    }

    private void OpenInGameMenu()
    {
        _inGameMenuOpen = true;
        _inGameMenuAwaitingEscapeRelease = true;
        _inGameMenuHoverIndex = -1;
        _clientPowersOpen = false;
        _clientPowersOpenedFromGameplay = false;
        _optionsMenuOpen = false;
        _pluginOptionsMenuOpen = false;
        _controlsMenuOpen = false;
        _editingPlayerName = false;
        _pendingControlsBinding = null;
    }

    private void CloseInGameMenu()
    {
        _inGameMenuOpen = false;
        _inGameMenuAwaitingEscapeRelease = false;
        _inGameMenuHoverIndex = -1;
    }

    private void UpdateInGameMenu(KeyboardState keyboard, MouseState mouse)
    {
        const float xbegin = 40f;
        const float ybegin = 300f;
        const float spacing = 30f;
        const float width = 220f;
        var items = GetInGameMenuItems();

        if (_inGameMenuAwaitingEscapeRelease)
        {
            if (!keyboard.IsKeyDown(Keys.Escape))
            {
                _inGameMenuAwaitingEscapeRelease = false;
            }
        }
        else if (IsKeyPressed(keyboard, Keys.Escape))
        {
            CloseInGameMenu();
            return;
        }

        if (mouse.X > xbegin && mouse.X < xbegin + width)
        {
            _inGameMenuHoverIndex = (int)MathF.Round((mouse.Y - ybegin) / spacing);
            if (_inGameMenuHoverIndex < 0 || _inGameMenuHoverIndex >= items.Length)
            {
                _inGameMenuHoverIndex = -1;
            }
        }
        else
        {
            _inGameMenuHoverIndex = -1;
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed || _inGameMenuHoverIndex < 0)
        {
            return;
        }

        ActivateInGameMenuItem(_inGameMenuHoverIndex);
    }

    private string[] GetInGameMenuItems()
    {
        if (IsLastToDieSessionActive)
        {
            return
            [
                "Options",
                "Return to Game",
                "Leave Last To Die",
                "Quit Game",
            ];
        }

        if (IsPracticeSessionActive)
        {
            return
            [
                "Options",
                "Practice Setup",
                "Experimental Settings",
                "Restart Practice",
                "Return to Game",
                "Leave Practice",
                "Quit Game",
            ];
        }

        var leaveLabel = IsPracticeSessionActive ? "Leave Practice" : "Disconnect";
        return
        [
            "Options",
            "Return to Game",
            leaveLabel,
            "Quit Game",
        ];
    }

    private void ActivateInGameMenuItem(int menuIndex)
    {
        if (IsLastToDieSessionActive)
        {
            switch (menuIndex)
            {
                case 0:
                    OpenOptionsMenu(fromGameplay: true);
                    CloseInGameMenu();
                    return;
                case 1:
                    CloseInGameMenu();
                    return;
                case 2:
                    ReturnToLastToDieMenu("Last To Die ended.");
                    return;
                case 3:
                    OpenQuitPrompt();
                    return;
            }
        }

        if (IsPracticeSessionActive)
        {
            switch (menuIndex)
            {
                case 0:
                    OpenOptionsMenu(fromGameplay: true);
                    CloseInGameMenu();
                    return;
                case 1:
                    OpenPracticeSetupMenu();
                    return;
                case 2:
                    OpenClientPowersMenu(fromGameplay: true);
                    return;
                case 3:
                    CloseInGameMenu();
                    RestartPracticeSession();
                    return;
                case 4:
                    CloseInGameMenu();
                    return;
                case 5:
                    ReturnToMainMenu(GetGameplayExitStatusMessage());
                    return;
                case 6:
                    OpenQuitPrompt();
                    return;
            }
        }

        switch (menuIndex)
        {
            case 0:
                OpenOptionsMenu(fromGameplay: true);
                CloseInGameMenu();
                break;
            case 1:
                CloseInGameMenu();
                break;
            case 2:
                ReturnToMainMenu(GetGameplayExitStatusMessage());
                break;
            case 3:
                OpenQuitPrompt();
                break;
        }
    }

    private void DrawInGameMenu()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.7f);

        var items = GetInGameMenuItems();
        const float xbegin = 40f;
        const float ybegin = 300f;
        const float spacing = 30f;
        const float width = 220f;
        DrawMenuPanelBackdrop(new Rectangle((int)xbegin - 12, (int)ybegin - 24, (int)width + 28, items.Length * (int)spacing + 24), 0.82f);
        DrawMenuPlaqueRows(new Vector2(xbegin, ybegin), items.Length, spacing, width, 0.72f);

        var position = new Vector2(xbegin, ybegin);
        for (var index = 0; index < items.Length; index += 1)
        {
            var color = index == _inGameMenuHoverIndex ? Color.Red : Color.White;
            DrawBitmapFontText(items[index], position, color, 1f);
            position.Y += spacing;
        }
    }

    private void UpdateOptionsMenu(KeyboardState keyboard, MouseState mouse)
    {
        var hasPluginOptions = HasClientPluginOptions();
        var items = hasPluginOptions ? 17 : 16;
        GetOptionsMenuLayout(items, out var xbegin, out var ybegin, out var spacing, out var width, out _);

        if (IsKeyPressed(keyboard, Keys.Escape))
        {
            if (_editingPlayerName)
            {
                _editingPlayerName = false;
                _playerNameEditBuffer = _world.LocalPlayer.DisplayName;
                return;
            }

            CloseOptionsMenu();
            return;
        }

        if (_editingPlayerName)
        {
            return;
        }

        if (mouse.X > xbegin && mouse.X < xbegin + width)
        {
            _optionsHoverIndex = (int)MathF.Round((mouse.Y - ybegin) / spacing);
            if (_optionsHoverIndex < 0 || _optionsHoverIndex >= items)
            {
                _optionsHoverIndex = -1;
            }
        }
        else
        {
            _optionsHoverIndex = -1;
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed || _optionsHoverIndex < 0)
        {
            return;
        }

        switch (_optionsHoverIndex)
        {
            case 0:
                _editingPlayerName = true;
                _playerNameEditBuffer = _world.LocalPlayer.DisplayName;
                break;
            case 1:
                _clientSettings.Fullscreen = !_clientSettings.Fullscreen;
                ApplyGraphicsSettings();
                break;
            case 2:
                _musicMode = GetNextMusicMode(_musicMode);
                StopMenuMusic();
                StopFaucetMusic();
                StopIngameMusic();
                PersistClientSettings();
                break;
            case 3:
                _clientSettings.IngameResolution = GetNextIngameResolution(_clientSettings.IngameResolution);
                ApplyGraphicsSettings();
                break;
            case 4:
                _particleMode = (_particleMode + 2) % 3;
                PersistClientSettings();
                break;
            case 5:
                _gibLevel = _gibLevel switch
                {
                    0 => 1,
                    1 => 2,
                    2 => 3,
                    _ => 0,
                };
                PersistClientSettings();
                break;
            case 6:
                _corpseDurationMode = _corpseDurationMode == ClientSettings.CorpseDurationInfinite
                    ? ClientSettings.CorpseDurationDefault
                    : ClientSettings.CorpseDurationInfinite;
                if (_corpseDurationMode != ClientSettings.CorpseDurationInfinite)
                {
                    ResetRetainedDeadBodies();
                }

                PersistClientSettings();
                break;
            case 7:
                _healerRadarEnabled = !_healerRadarEnabled;
                PersistClientSettings();
                break;
            case 8:
                _showHealerEnabled = !_showHealerEnabled;
                PersistClientSettings();
                break;
            case 9:
                _showHealingEnabled = !_showHealingEnabled;
                PersistClientSettings();
                break;
            case 10:
                _showHealthBarEnabled = !_showHealthBarEnabled;
                PersistClientSettings();
                break;
            case 11:
                _showPersistentSelfNameEnabled = !_showPersistentSelfNameEnabled;
                PersistClientSettings();
                break;
            case 12:
                _killCamEnabled = !_killCamEnabled;
                PersistClientSettings();
                break;
            case 13:
                _clientSettings.VSync = !_clientSettings.VSync;
                ApplyGraphicsSettings();
                break;
            case 14:
                OpenControlsMenu(_optionsMenuOpenedFromGameplay);
                break;
            case 15:
                if (hasPluginOptions)
                {
                    OpenPluginOptionsMenu(_optionsMenuOpenedFromGameplay);
                }
                else
                {
                    CloseOptionsMenu();
                }
                break;
            case 16:
                CloseOptionsMenu();
                break;
        }
    }

    private void DrawOptionsMenu()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.8f);
        var compactLayout = ViewportHeight < 540;
        var textScale = compactLayout ? 0.92f : 1f;
        var hasPluginOptions = HasClientPluginOptions();

        var labels = new List<string>
        {
            "Player name:",
            "Fullscreen:",
            "Music:",
            "Ingame Aspect Ratio:",
            "Particles:",
            "Gibs:",
            "Corpses:",
            "Healer Radar:",
            "Show Healer:",
            "Show Healing:",
            "Additional Healthbar:",
            "Persistent Self Name:",
            "Kill Cam:",
            "V Sync:",
            "Controls",
        };
        var values = new List<string>
        {
            _editingPlayerName ? _playerNameEditBuffer + "_" : _world.LocalPlayer.DisplayName,
            _graphics.IsFullScreen ? "On" : "Off",
            GetMusicModeLabel(_musicMode),
            GetIngameResolutionLabel(_ingameResolution),
            GetParticleModeLabel(_particleMode),
            GetGibLevelLabel(_gibLevel),
            GetCorpseDurationLabel(_corpseDurationMode),
            _healerRadarEnabled ? "Enabled" : "Disabled",
            _showHealerEnabled ? "Enabled" : "Disabled",
            _showHealingEnabled ? "Enabled" : "Disabled",
            _showHealthBarEnabled ? "Enabled" : "Disabled",
            _showPersistentSelfNameEnabled ? "Enabled" : "Disabled",
            _killCamEnabled ? "Enabled" : "Disabled",
            _graphics.SynchronizeWithVerticalRetrace ? "Enabled" : "Disabled",
            string.Empty,
        };
        if (hasPluginOptions)
        {
            labels.Add("Plugin Options");
            values.Add(string.Empty);
        }

        labels.Add("Back");
        values.Add(string.Empty);

        GetOptionsMenuLayout(labels.Count, out var xbegin, out var ybegin, out var spacing, out var width, out var valueX);
        DrawMenuPanelBackdrop(
            new Rectangle(
                (int)xbegin - 12,
                (int)(ybegin - spacing),
                (int)(width + 132f),
                Math.Max((int)spacing, (int)(labels.Count * spacing + spacing * 0.5f))),
            0.82f);
        DrawMenuPlaqueRows(new Vector2(xbegin, ybegin), labels.Count, spacing, width + 116f, 0.7f);

        var labelPosition = new Vector2(xbegin, ybegin);
        for (var index = 0; index < labels.Count; index += 1)
        {
            var color = _editingPlayerName && index == 0
                ? Color.Orange
                : index == _optionsHoverIndex ? Color.Red : Color.White;
            DrawBitmapFontText(labels[index], labelPosition, color, textScale);
            DrawBitmapFontText(values[index], new Vector2(valueX, labelPosition.Y), color, textScale);
            labelPosition.Y += spacing;
        }
    }

    private void UpdatePluginOptionsMenu(KeyboardState keyboard, MouseState mouse)
    {
        var rows = BuildPluginOptionsMenuRows();
        var visibleRowCount = Math.Min(rows.Count, GetPluginOptionsVisibleRowCapacity());
        ClampPluginOptionsScrollOffset(rows.Count, visibleRowCount);
        GetOptionsMenuLayout(visibleRowCount, out var xbegin, out var ybegin, out var spacing, out var width, out _);
        var wheelDelta = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        var menuTop = ybegin - spacing;
        var menuHeight = Math.Max(spacing, visibleRowCount * spacing);
        var menuBounds = new Rectangle(
            (int)MathF.Floor(xbegin),
            (int)MathF.Floor(menuTop),
            (int)MathF.Ceiling(width),
            (int)MathF.Ceiling(menuHeight));

        if (_pendingPluginOptionsKeyItem is not null)
        {
            if (IsKeyPressed(keyboard, Keys.Escape))
            {
                _pendingPluginOptionsKeyItem = null;
                return;
            }

            foreach (var key in keyboard.GetPressedKeys())
            {
                if (_previousKeyboard.IsKeyDown(key))
                {
                    continue;
                }

                try
                {
                    _pendingPluginOptionsKeyItem.SetKey(key);
                }
                catch (Exception ex)
                {
                    AddConsoleLine($"plugin option apply failed for \"{_pendingPluginOptionsKeyItem.Label}\": {ex.Message}");
                }

                _pendingPluginOptionsKeyItem = null;
                return;
            }

            return;
        }

        if (IsKeyPressed(keyboard, Keys.Escape))
        {
            if (_selectedPluginOptionsPluginId is not null)
            {
                _selectedPluginOptionsPluginId = null;
                _pendingPluginOptionsKeyItem = null;
                _pluginOptionsHoverIndex = -1;
                _pluginOptionsScrollOffset = 0;
                return;
            }

            ClosePluginOptionsMenu();
            return;
        }

        if (wheelDelta != 0 && menuBounds.Contains(mouse.Position))
        {
            var stepCount = Math.Max(1, Math.Abs(wheelDelta) / 120);
            _pluginOptionsScrollOffset = Math.Clamp(
                _pluginOptionsScrollOffset + (wheelDelta > 0 ? -stepCount : stepCount),
                0,
                Math.Max(0, rows.Count - visibleRowCount));
        }

        if (mouse.X > xbegin && mouse.X < xbegin + width)
        {
            var visibleHoverIndex = (int)MathF.Round((mouse.Y - ybegin) / spacing);
            var hoverIndex = _pluginOptionsScrollOffset + visibleHoverIndex;
            var visibleStart = _pluginOptionsScrollOffset;
            var visibleEndExclusive = visibleStart + visibleRowCount;
            _pluginOptionsHoverIndex = visibleHoverIndex >= 0
                && hoverIndex >= visibleStart
                && hoverIndex < visibleEndExclusive
                && hoverIndex < rows.Count
                && rows[hoverIndex].Selectable
                    ? hoverIndex
                    : -1;
        }
        else
        {
            _pluginOptionsHoverIndex = -1;
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed || _pluginOptionsHoverIndex < 0)
        {
            return;
        }

        rows[_pluginOptionsHoverIndex].Activate?.Invoke();
    }

    private void DrawPluginOptionsMenu()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.82f);
        var compactLayout = ViewportHeight < 540;
        var textScale = compactLayout ? 0.92f : 1f;
        var rows = BuildPluginOptionsMenuRows();
        var visibleRowCount = Math.Min(rows.Count, GetPluginOptionsVisibleRowCapacity());
        ClampPluginOptionsScrollOffset(rows.Count, visibleRowCount);
        GetOptionsMenuLayout(visibleRowCount, out var xbegin, out var ybegin, out var spacing, out var width, out var valueX);
        DrawMenuPanelBackdrop(
            new Rectangle(
                (int)xbegin - 12,
                (int)(ybegin - spacing),
                (int)(width + 132f),
                Math.Max((int)spacing, (int)(visibleRowCount * spacing + spacing * 0.5f))),
            0.82f);
        DrawMenuPlaqueRows(new Vector2(xbegin, ybegin), visibleRowCount, spacing, width + 116f, 0.7f);

        var position = new Vector2(xbegin, ybegin);
        var endIndex = Math.Min(rows.Count, _pluginOptionsScrollOffset + visibleRowCount);
        for (var index = _pluginOptionsScrollOffset; index < endIndex; index += 1)
        {
            var row = rows[index];
            var color = row.IsHeader
                ? new Color(240, 200, 120)
                : index == _pluginOptionsHoverIndex ? Color.Red : Color.White;
            DrawBitmapFontText(row.Label, position, color, textScale);
            if (!string.IsNullOrWhiteSpace(row.Value))
            {
                DrawBitmapFontText(row.Value, new Vector2(valueX, position.Y), color, textScale);
            }

            position.Y += spacing;
        }

        if (rows.Count > visibleRowCount)
        {
            var visibleStart = _pluginOptionsScrollOffset + 1;
            var visibleEnd = Math.Min(rows.Count, _pluginOptionsScrollOffset + visibleRowCount);
            DrawBitmapFontText(
                $"{visibleStart}-{visibleEnd}/{rows.Count}",
                new Vector2(valueX + (compactLayout ? 32f : 56f), ybegin - spacing),
                new Color(186, 186, 186),
                compactLayout ? 0.72f : 0.8f);
        }

        if (_pendingPluginOptionsKeyItem is not null)
        {
            DrawBitmapFontText(
                $"Press a key for {_pendingPluginOptionsKeyItem.Label} (Esc to cancel)",
                new Vector2(xbegin, Math.Max(18f, ybegin - spacing * 1.5f)),
                Color.Orange,
                compactLayout ? 0.82f : 0.9f);
        }
    }

    private bool HasClientPluginOptions()
    {
        return GetClientPluginOptionsEntries().Count > 0;
    }

    private List<PluginOptionsMenuRow> BuildPluginOptionsMenuRows()
    {
        var rows = new List<PluginOptionsMenuRow>();
        var pluginEntries = GetClientPluginOptionsEntries();
        if (_selectedPluginOptionsPluginId is null)
        {
            rows.Add(new PluginOptionsMenuRow("Plugin Options", string.Empty, Selectable: false, IsHeader: true, Activate: null));
            for (var pluginIndex = 0; pluginIndex < pluginEntries.Count; pluginIndex += 1)
            {
                var entry = pluginEntries[pluginIndex];
                rows.Add(new PluginOptionsMenuRow(
                    entry.DisplayName,
                    GetClientPluginStatusLabel(entry),
                    Selectable: true,
                    IsHeader: false,
                    Activate: () => OpenPluginOptionsDetail(entry.PluginId)));
            }

            if (pluginEntries.Count == 0)
            {
                rows.Add(new PluginOptionsMenuRow("No plugin options available.", string.Empty, Selectable: false, IsHeader: false, Activate: null));
            }

            rows.Add(new PluginOptionsMenuRow("Back", string.Empty, Selectable: true, IsHeader: false, Activate: ClosePluginOptionsMenu));
            return rows;
        }

        var selectedEntry = GetSelectedPluginOptionsEntry();
        if (selectedEntry is null)
        {
            _selectedPluginOptionsPluginId = null;
            return BuildPluginOptionsMenuRows();
        }

        rows.Add(new PluginOptionsMenuRow(selectedEntry.DisplayName, string.Empty, Selectable: false, IsHeader: true, Activate: null));
        rows.Add(new PluginOptionsMenuRow("Version", FormatClientPluginVersion(selectedEntry.Version), Selectable: false, IsHeader: false, Activate: null));
        rows.Add(new PluginOptionsMenuRow(
            "Enabled",
            selectedEntry.IsEnabled ? "Enabled" : "Disabled",
            Selectable: true,
            IsHeader: false,
            Activate: () => _clientPluginHost?.SetPluginEnabled(selectedEntry.PluginId, !selectedEntry.IsEnabled)));
        if (selectedEntry.IsEnabled && !selectedEntry.IsLoaded)
        {
            rows.Add(new PluginOptionsMenuRow("Status", "Load failed", Selectable: false, IsHeader: false, Activate: null));
            rows.Add(new PluginOptionsMenuRow("See console for the plugin error.", string.Empty, Selectable: false, IsHeader: false, Activate: null));
        }
        else if (!selectedEntry.IsEnabled)
        {
            rows.Add(new PluginOptionsMenuRow("Enable this plugin to access its options.", string.Empty, Selectable: false, IsHeader: false, Activate: null));
        }

        var sections = selectedEntry.Sections;
        for (var sectionIndex = 0; selectedEntry.IsEnabled && selectedEntry.IsLoaded && sectionIndex < sections.Count; sectionIndex += 1)
        {
            var section = sections[sectionIndex];
            if (section.Items.Count == 0)
            {
                continue;
            }

            var shouldShowSectionHeader = sections.Count > 1
                || !string.Equals(section.Title, selectedEntry.DisplayName, StringComparison.Ordinal);
            if (shouldShowSectionHeader)
            {
                rows.Add(new PluginOptionsMenuRow(section.Title, string.Empty, Selectable: false, IsHeader: true, Activate: null));
            }

            for (var itemIndex = 0; itemIndex < section.Items.Count; itemIndex += 1)
            {
                var item = section.Items[itemIndex];
                rows.Add(new PluginOptionsMenuRow(
                    item.Label,
                    GetPluginOptionValueLabel(item),
                    Selectable: true,
                    IsHeader: false,
                    Activate: () => ActivatePluginOption(item)));
            }
        }

        if (rows.Count == 3 && selectedEntry.IsEnabled && selectedEntry.IsLoaded)
        {
            rows.Add(new PluginOptionsMenuRow("No options available.", string.Empty, Selectable: false, IsHeader: false, Activate: null));
        }

        rows.Add(new PluginOptionsMenuRow("Back", string.Empty, Selectable: true, IsHeader: false, Activate: CloseSelectedPluginOptionsDetail));
        return rows;
    }

    private IReadOnlyList<ClientPluginOptionsEntry> GetClientPluginOptionsEntries()
    {
        return _clientPluginHost?.GetPluginOptionsEntries() ?? [];
    }

    private ClientPluginOptionsEntry? GetSelectedPluginOptionsEntry()
    {
        var selectedPluginId = _selectedPluginOptionsPluginId;
        if (string.IsNullOrWhiteSpace(selectedPluginId))
        {
            return null;
        }

        var entries = GetClientPluginOptionsEntries();
        for (var index = 0; index < entries.Count; index += 1)
        {
            if (string.Equals(entries[index].PluginId, selectedPluginId, StringComparison.Ordinal))
            {
                return entries[index];
            }
        }

        return null;
    }

    private void OpenPluginOptionsDetail(string pluginId)
    {
        _selectedPluginOptionsPluginId = pluginId;
        _pendingPluginOptionsKeyItem = null;
        _pluginOptionsHoverIndex = -1;
        _pluginOptionsScrollOffset = 0;
    }

    private void CloseSelectedPluginOptionsDetail()
    {
        _selectedPluginOptionsPluginId = null;
        _pendingPluginOptionsKeyItem = null;
        _pluginOptionsHoverIndex = -1;
        _pluginOptionsScrollOffset = 0;
    }

    private int GetPluginOptionsVisibleRowCapacity()
    {
        return ViewportHeight < 540 ? 14 : 16;
    }

    private void ClampPluginOptionsScrollOffset(int rowCount, int visibleRowCount)
    {
        _pluginOptionsScrollOffset = Math.Clamp(
            _pluginOptionsScrollOffset,
            0,
            Math.Max(0, rowCount - visibleRowCount));
    }

    private static string GetClientPluginStatusLabel(ClientPluginOptionsEntry entry)
    {
        if (!entry.IsEnabled)
        {
            return "Disabled";
        }

        return entry.IsLoaded ? "Enabled" : "Load failed";
    }

    private static string FormatClientPluginVersion(Version version)
    {
        return version.Revision >= 0
            ? version.ToString()
            : version.Build >= 0
                ? version.ToString(3)
                : $"{version.Major}.{version.Minor}";
    }

    private string GetPluginOptionValueLabel(ClientPluginOptionItem item)
    {
        try
        {
            return item.GetValueLabel();
        }
        catch (Exception ex)
        {
            AddConsoleLine($"plugin option read failed for \"{item.Label}\": {ex.Message}");
            return "<error>";
        }
    }

    private void ActivatePluginOption(ClientPluginOptionItem item)
    {
        if (item is ClientPluginKeyOptionItem keyItem)
        {
            _pendingPluginOptionsKeyItem = keyItem;
            return;
        }

        try
        {
            item.Activate();
        }
        catch (Exception ex)
        {
            AddConsoleLine($"plugin option apply failed for \"{item.Label}\": {ex.Message}");
        }
    }

    private void UpdateControlsMenu(KeyboardState keyboard, MouseState mouse)
    {
        const float xbegin = 40f;
        const float ybegin = 150f;
        const float spacing = 28f;
        const float width = 360f;
        var bindingItems = GetControlsMenuBindings();
        var items = bindingItems.Count + 1;

        if (_pendingControlsBinding.HasValue)
        {
            if (IsKeyPressed(keyboard, Keys.Escape))
            {
                _pendingControlsBinding = null;
                return;
            }

            foreach (var key in keyboard.GetPressedKeys())
            {
                if (_previousKeyboard.IsKeyDown(key))
                {
                    continue;
                }

                ApplyControlsBinding(_pendingControlsBinding.Value, key);
                PersistInputBindings();
                _pendingControlsBinding = null;
                return;
            }

            return;
        }

        if (IsKeyPressed(keyboard, Keys.Escape))
        {
            CloseControlsMenu();
            return;
        }

        if (mouse.X > xbegin && mouse.X < xbegin + width)
        {
            _controlsHoverIndex = (int)MathF.Round((mouse.Y - ybegin) / spacing);
            if (_controlsHoverIndex < 0 || _controlsHoverIndex >= items)
            {
                _controlsHoverIndex = -1;
            }
        }
        else
        {
            _controlsHoverIndex = -1;
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed || _controlsHoverIndex < 0)
        {
            return;
        }

        if (_controlsHoverIndex == bindingItems.Count)
        {
            CloseControlsMenu();
            return;
        }

        _pendingControlsBinding = bindingItems[_controlsHoverIndex].Binding;
    }

    private void DrawControlsMenu()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.82f);

        var title = _pendingControlsBinding.HasValue
            ? $"Press a key for {GetControlsBindingLabel(_pendingControlsBinding.Value)}"
            : "Controls";
        DrawBitmapFontText(title, new Vector2(40f, 110f), Color.White, 1.2f);

        var items = GetControlsMenuBindings();
        const float xbegin = 40f;
        const float ybegin = 150f;
        const float spacing = 28f;
        const float width = 360f;
        DrawMenuPanelBackdrop(new Rectangle((int)xbegin - 12, (int)ybegin - 36, (int)width + 44, (items.Count + 2) * (int)spacing), 0.82f);
        DrawMenuPlaqueRows(new Vector2(xbegin, ybegin), items.Count + 1, spacing, width, 0.72f);

        var position = new Vector2(xbegin, ybegin);
        for (var index = 0; index < items.Count; index += 1)
        {
            var item = items[index];
            var color = _pendingControlsBinding == item.Binding
                ? Color.Orange
                : index == _controlsHoverIndex ? Color.Red : Color.White;
            DrawBitmapFontText(item.Label, position, color, 1f);
            DrawBitmapFontText(GetBindingDisplayName(item.Key), new Vector2(280f, position.Y), color, 1f);
            position.Y += spacing;
        }

        var backColor = items.Count == _controlsHoverIndex ? Color.Red : Color.White;
        DrawBitmapFontText("Back", position, backColor, 1f);
    }

    private List<(ControlsMenuBinding Binding, string Label, Keys Key)> GetControlsMenuBindings()
    {
        var bubbleMenuBindingPrefix = GetBubbleMenuBindingPrefix();
        return
        [
            (ControlsMenuBinding.MoveUp, "Jump:", _inputBindings.MoveUp),
            (ControlsMenuBinding.MoveLeft, "Move Left:", _inputBindings.MoveLeft),
            (ControlsMenuBinding.MoveRight, "Move Right:", _inputBindings.MoveRight),
            (ControlsMenuBinding.MoveDown, "Move Down:", _inputBindings.MoveDown),
            (ControlsMenuBinding.Taunt, "Taunt:", _inputBindings.Taunt),
            (ControlsMenuBinding.CallMedic, "Call Medic:", _inputBindings.CallMedic),
            (ControlsMenuBinding.FireSecondaryWeapon, "Secondary Weapon:", _inputBindings.FireSecondaryWeapon),
            (ControlsMenuBinding.InteractWeapon, "Interact Weapon:", _inputBindings.InteractWeapon),
            (ControlsMenuBinding.ChangeTeam, "Change Team:", _inputBindings.ChangeTeam),
            (ControlsMenuBinding.ChangeClass, "Change Class:", _inputBindings.ChangeClass),
            (ControlsMenuBinding.ShowScoreboard, "Show Scores:", _inputBindings.ShowScoreboard),
            (ControlsMenuBinding.ToggleConsole, "Console:", _inputBindings.ToggleConsole),
            (ControlsMenuBinding.OpenBubbleMenuZ, $"{bubbleMenuBindingPrefix} Z:", _inputBindings.OpenBubbleMenuZ),
            (ControlsMenuBinding.OpenBubbleMenuX, $"{bubbleMenuBindingPrefix} X:", _inputBindings.OpenBubbleMenuX),
            (ControlsMenuBinding.OpenBubbleMenuC, $"{bubbleMenuBindingPrefix} C:", _inputBindings.OpenBubbleMenuC),
        ];
    }

    private void ApplyControlsBinding(ControlsMenuBinding binding, Keys key)
    {
        switch (binding)
        {
            case ControlsMenuBinding.MoveUp:
                _inputBindings.MoveUp = key;
                break;
            case ControlsMenuBinding.MoveLeft:
                _inputBindings.MoveLeft = key;
                break;
            case ControlsMenuBinding.MoveRight:
                _inputBindings.MoveRight = key;
                break;
            case ControlsMenuBinding.MoveDown:
                _inputBindings.MoveDown = key;
                break;
            case ControlsMenuBinding.Taunt:
                _inputBindings.Taunt = key;
                break;
            case ControlsMenuBinding.CallMedic:
                _inputBindings.CallMedic = key;
                break;
            case ControlsMenuBinding.FireSecondaryWeapon:
                _inputBindings.FireSecondaryWeapon = key;
                break;
            case ControlsMenuBinding.InteractWeapon:
                _inputBindings.InteractWeapon = key;
                break;
            case ControlsMenuBinding.ChangeTeam:
                _inputBindings.ChangeTeam = key;
                break;
            case ControlsMenuBinding.ChangeClass:
                _inputBindings.ChangeClass = key;
                break;
            case ControlsMenuBinding.ShowScoreboard:
                _inputBindings.ShowScoreboard = key;
                break;
            case ControlsMenuBinding.ToggleConsole:
                _inputBindings.ToggleConsole = key;
                break;
            case ControlsMenuBinding.OpenBubbleMenuZ:
                _inputBindings.OpenBubbleMenuZ = key;
                break;
            case ControlsMenuBinding.OpenBubbleMenuX:
                _inputBindings.OpenBubbleMenuX = key;
                break;
            case ControlsMenuBinding.OpenBubbleMenuC:
                _inputBindings.OpenBubbleMenuC = key;
                break;
        }
    }

    private string GetControlsBindingLabel(ControlsMenuBinding binding)
    {
        var bubbleMenuBindingPrefix = GetBubbleMenuBindingPrefix();
        return binding switch
        {
            ControlsMenuBinding.MoveUp => "Jump",
            ControlsMenuBinding.MoveLeft => "Move Left",
            ControlsMenuBinding.MoveRight => "Move Right",
            ControlsMenuBinding.MoveDown => "Move Down",
            ControlsMenuBinding.Taunt => "Taunt",
            ControlsMenuBinding.CallMedic => "Call Medic",
            ControlsMenuBinding.FireSecondaryWeapon => "Secondary Weapon",
            ControlsMenuBinding.InteractWeapon => "Interact Weapon",
            ControlsMenuBinding.ChangeTeam => "Change Team",
            ControlsMenuBinding.ChangeClass => "Change Class",
            ControlsMenuBinding.ShowScoreboard => "Show Scores",
            ControlsMenuBinding.ToggleConsole => "Console",
            ControlsMenuBinding.OpenBubbleMenuZ => $"{bubbleMenuBindingPrefix} Z",
            ControlsMenuBinding.OpenBubbleMenuX => $"{bubbleMenuBindingPrefix} X",
            ControlsMenuBinding.OpenBubbleMenuC => $"{bubbleMenuBindingPrefix} C",
            _ => "Binding",
        };
    }

    private string GetBubbleMenuBindingPrefix()
    {
        return HasClientPluginBubbleMenuOverride()
            ? "Bubble Wheel"
            : "Bubble Menu";
    }

    private static string GetBindingDisplayName(Keys key)
    {
        return key switch
        {
            Keys.LeftShift => "LShift",
            Keys.RightShift => "RShift",
            Keys.LeftControl => "LCtrl",
            Keys.RightControl => "RCtrl",
            Keys.LeftAlt => "LAlt",
            Keys.RightAlt => "RAlt",
            Keys.OemTilde => "~",
            Keys.OemComma => ",",
            Keys.OemPeriod => ".",
            Keys.OemQuestion => "/",
            Keys.OemSemicolon => ";",
            Keys.OemQuotes => "'",
            Keys.OemOpenBrackets => "[",
            Keys.OemCloseBrackets => "]",
            Keys.OemPipe => "\\",
            Keys.Space => "Space",
            Keys.PageUp => "PgUp",
            Keys.PageDown => "PgDn",
            _ => key.ToString(),
        };
    }

    private static string GetParticleModeLabel(int particleMode)
    {
        return particleMode switch
        {
            0 => "Normal",
            2 => "Alternative (faster)",
            _ => "Disabled",
        };
    }

    private static MusicMode GetNextMusicMode(MusicMode musicMode)
    {
        return musicMode switch
        {
            MusicMode.None => MusicMode.MenuOnly,
            MusicMode.MenuOnly => MusicMode.InGameOnly,
            MusicMode.InGameOnly => MusicMode.MenuAndInGame,
            _ => MusicMode.None,
        };
    }

    private static string GetMusicModeLabel(MusicMode musicMode)
    {
        return musicMode switch
        {
            MusicMode.None => "None",
            MusicMode.MenuOnly => "Menu Only",
            MusicMode.InGameOnly => "In-Game Only",
            _ => "Menu and In-Game",
        };
    }

    private static string GetGibLevelLabel(int gibLevel)
    {
        return gibLevel switch
        {
            0 => "0, No blood or gibs",
            1 => "1, Blood only",
            2 => "2, Blood and medium gibs",
            _ => $"{gibLevel.ToString(CultureInfo.InvariantCulture)}, Full blood and gibs",
        };
    }

    private static string GetCorpseDurationLabel(int corpseDurationMode)
    {
        return corpseDurationMode == ClientSettings.CorpseDurationInfinite
            ? "Infinite"
            : "300 ticks";
    }

    private void GetOptionsMenuLayout(int rowCount, out float xbegin, out float ybegin, out float spacing, out float width, out float valueX)
    {
        xbegin = 40f;
        valueX = 240f;
        width = 320f;
        if (ViewportHeight < 540)
        {
            spacing = 26f;
            width = 340f;
            valueX = 232f;
        }
        else
        {
            spacing = 30f;
        }

        var compactLayout = ViewportHeight < 540;
        var defaultY = compactLayout ? 104f : 170f;
        var minY = compactLayout ? 24f : 40f;
        var bottomPadding = compactLayout ? 18f : 40f;
        var estimatedTextHeight = compactLayout ? 18f : 22f;
        var totalHeight = Math.Max(0, rowCount - 1) * spacing + estimatedTextHeight;
        ybegin = MathF.Min(defaultY, MathF.Max(minY, ViewportHeight - bottomPadding - totalHeight));
    }

    private void DrawMenuPanelBackdrop(Rectangle rectangle, float alpha)
    {
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            return;
        }

        DrawInsetHudPanel(
            rectangle,
            new Color(184, 178, 160) * (alpha * 0.35f),
            new Color(24, 27, 32) * (alpha * 0.85f));
    }

    private void DrawMenuPlaqueRows(Vector2 position, int rowCount, float spacing, float width, float alpha)
    {
        for (var index = 0; index < rowCount; index += 1)
        {
            DrawMenuPlaque(position.X - 6f, position.Y + (index * spacing) - 4f, width, alpha);
        }
    }

    private void DrawMenuPlaque(float x, float y, float width, float alpha)
    {
        if (width <= 0f)
        {
            return;
        }

        var tint = Color.White * alpha;
        const float plaqueSpriteWidth = 17f;
        if (!TryDrawScreenSprite("gbMenuLayoutS", 0, new Vector2(x, y), tint, Vector2.One))
        {
            _spriteBatch.Draw(
                _pixel,
                new Rectangle((int)MathF.Round(x), (int)MathF.Round(y), Math.Max(1, (int)MathF.Round(width)), 17),
                new Color(70, 74, 82) * (alpha * 0.55f));
            return;
        }

        var middleScaleX = Math.Max(1f, (width / (plaqueSpriteWidth - 1f)) - 2f);
        TryDrawScreenSprite("gbMenuLayoutS", 1, new Vector2(x + plaqueSpriteWidth, y), tint, new Vector2(middleScaleX, 1f));
        TryDrawScreenSprite("gbMenuLayoutS", 2, new Vector2(x - plaqueSpriteWidth + width + 1f, y), tint, Vector2.One);
    }

    private readonly record struct PluginOptionsMenuRow(
        string Label,
        string Value,
        bool Selectable,
        bool IsHeader,
        Action? Activate);
}
