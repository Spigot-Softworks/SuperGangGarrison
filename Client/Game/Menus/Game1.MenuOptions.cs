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
            || _lastToDieSurvivorMenuOpen
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

        if (_lastToDieSurvivorMenuOpen)
        {
            UpdateLastToDieSurvivorMenu(keyboard, mouse);
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
        _optionsPageIndex = 0;
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

        var buttons = BuildOptionsMenuButtons();
        _optionsHoverIndex = -1;
        for (var index = 0; index < buttons.Count; index += 1)
        {
            if (!buttons[index].Bounds.Contains(mouse.Position))
            {
                continue;
            }

            _optionsHoverIndex = index;
            break;
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed || _optionsHoverIndex < 0)
        {
            return;
        }

        buttons[_optionsHoverIndex].Activate();
    }

    private void DrawOptionsMenu()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.8f);
        var actions = GetVisibleOptionsMenuActions(out _);
        var stackedActions = new List<MenuPageAction>(actions)
        {
            new("Next Page", AdvanceOptionsPage),
        };
        var layout = GetCenteredPlaqueMenuLayout(tall: true, stackedActions.Count, includeBottomBarButton: false);
        var hoveredStackedIndex = _optionsHoverIndex >= 0 && _optionsHoverIndex < stackedActions.Count
            ? _optionsHoverIndex
            : -1;
        var soloHovered = _optionsHoverIndex == stackedActions.Count;
        DrawPlaqueMenuLayout(layout, stackedActions, new MenuPageAction("Back", CloseOptionsMenu), false, string.Empty, hoveredStackedIndex, soloHovered, false);
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

    private List<MenuPageButton> BuildOptionsMenuButtons()
    {
        var buttons = new List<MenuPageButton>();
        var visibleActions = GetVisibleOptionsMenuActions(out _);
        var stackedCount = visibleActions.Count + 1;
        var layout = GetCenteredPlaqueMenuLayout(tall: true, stackedCount, includeBottomBarButton: false);

        for (var index = 0; index < visibleActions.Count && index < layout.StackedButtonBounds.Length; index += 1)
        {
            buttons.Add(new MenuPageButton(visibleActions[index].Label, layout.StackedButtonBounds[index], visibleActions[index].Activate));
        }

        if (visibleActions.Count < layout.StackedButtonBounds.Length)
        {
            buttons.Add(new MenuPageButton("Next Page", layout.StackedButtonBounds[visibleActions.Count], AdvanceOptionsPage));
        }

        buttons.Add(new MenuPageButton("Back", layout.SoloButtonBounds, CloseOptionsMenu));
        return buttons;
    }

    private List<MenuPageAction> GetVisibleOptionsMenuActions(out int pageCount)
    {
        const int itemsPerPage = 5;
        var allActions = BuildOptionsMenuActions();
        pageCount = Math.Max(1, (int)Math.Ceiling(allActions.Count / (float)itemsPerPage));
        _optionsPageIndex = ((_optionsPageIndex % pageCount) + pageCount) % pageCount;
        var startIndex = _optionsPageIndex * itemsPerPage;
        var count = Math.Min(itemsPerPage, Math.Max(0, allActions.Count - startIndex));
        return count > 0
            ? allActions.GetRange(startIndex, count)
            : [];
    }

    private List<MenuPageAction> BuildOptionsMenuActions()
    {
        var actions = new List<MenuPageAction>
        {
            new($"Player Name: {(_editingPlayerName ? _playerNameEditBuffer + "_" : _world.LocalPlayer.DisplayName)}", () =>
            {
                _editingPlayerName = true;
                _playerNameEditBuffer = _world.LocalPlayer.DisplayName;
            }),
            new($"Fullscreen: {(_graphics.IsFullScreen ? "On" : "Off")}", () =>
            {
                _clientSettings.Fullscreen = !_clientSettings.Fullscreen;
                ApplyGraphicsSettings();
            }),
            new($"Music: {GetMusicModeLabel(_musicMode)}", () =>
            {
                _musicMode = GetNextMusicMode(_musicMode);
                StopMenuMusic();
                StopFaucetMusic();
                StopIngameMusic();
                PersistClientSettings();
            }),
            new($"Aspect Ratio: {GetIngameResolutionLabel(_ingameResolution)}", () =>
            {
                _clientSettings.IngameResolution = GetNextIngameResolution(_clientSettings.IngameResolution);
                ApplyGraphicsSettings();
            }),
            new($"Particles: {GetParticleModeLabel(_particleMode)}", () =>
            {
                _particleMode = (_particleMode + 2) % 3;
                PersistClientSettings();
            }),
            new($"Gibs: {GetGibLevelLabel(_gibLevel)}", () =>
            {
                _gibLevel = _gibLevel switch
                {
                    0 => 1,
                    1 => 2,
                    2 => 3,
                    _ => 0,
                };
                PersistClientSettings();
            }),
            new($"Corpses: {GetCorpseDurationLabel(_corpseDurationMode)}", () =>
            {
                _corpseDurationMode = _corpseDurationMode == ClientSettings.CorpseDurationInfinite
                    ? ClientSettings.CorpseDurationDefault
                    : ClientSettings.CorpseDurationInfinite;
                if (_corpseDurationMode != ClientSettings.CorpseDurationInfinite)
                {
                    ResetRetainedDeadBodies();
                }

                PersistClientSettings();
            }),
            new($"Healer Radar: {(_healerRadarEnabled ? "Enabled" : "Disabled")}", () =>
            {
                _healerRadarEnabled = !_healerRadarEnabled;
                PersistClientSettings();
            }),
            new($"Show Healer: {(_showHealerEnabled ? "Enabled" : "Disabled")}", () =>
            {
                _showHealerEnabled = !_showHealerEnabled;
                PersistClientSettings();
            }),
            new($"Show Healing: {(_showHealingEnabled ? "Enabled" : "Disabled")}", () =>
            {
                _showHealingEnabled = !_showHealingEnabled;
                PersistClientSettings();
            }),
            new($"Healthbar: {(_showHealthBarEnabled ? "Enabled" : "Disabled")}", () =>
            {
                _showHealthBarEnabled = !_showHealthBarEnabled;
                PersistClientSettings();
            }),
            new($"Persistent Name: {(_showPersistentSelfNameEnabled ? "Enabled" : "Disabled")}", () =>
            {
                _showPersistentSelfNameEnabled = !_showPersistentSelfNameEnabled;
                PersistClientSettings();
            }),
            new($"Sprite Shadow: {(_spriteDropShadowEnabled ? "Enabled" : "Disabled")}", () =>
            {
                _spriteDropShadowEnabled = !_spriteDropShadowEnabled;
                PersistClientSettings();
            }),
            new($"Kill Cam: {(_killCamEnabled ? "Enabled" : "Disabled")}", () =>
            {
                _killCamEnabled = !_killCamEnabled;
                PersistClientSettings();
            }),
            new($"V Sync: {(_graphics.SynchronizeWithVerticalRetrace ? "Enabled" : "Disabled")}", () =>
            {
                _clientSettings.VSync = !_clientSettings.VSync;
                ApplyGraphicsSettings();
            }),
            new("Controls", () => OpenControlsMenu(_optionsMenuOpenedFromGameplay)),
        };

        if (HasClientPluginOptions())
        {
            actions.Add(new MenuPageAction("Plugin Options", () => OpenPluginOptionsMenu(_optionsMenuOpenedFromGameplay)));
        }

        return actions;
    }

    private void AdvanceOptionsPage()
    {
        GetVisibleOptionsMenuActions(out var pageCount);
        _optionsPageIndex = (_optionsPageIndex + 1) % Math.Max(1, pageCount);
        _optionsHoverIndex = -1;
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

}
