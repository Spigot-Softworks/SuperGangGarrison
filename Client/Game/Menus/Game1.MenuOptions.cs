#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool IsGameplayMenuOpen()
    {
        return HasOpenGameplayBlockingMenu();
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
        switch (GetActiveGameplayOverlay())
        {
            case GameplayOverlayKind.LastToDieFailure:
                UpdateLastToDieFailureOverlay(keyboard, mouse);
                return;
            case GameplayOverlayKind.LastToDieDeathFocusPresentation:
                UpdateLastToDieDeathFocusPresentation();
                return;
            case GameplayOverlayKind.LastToDieStageClear:
                UpdateLastToDieStageClearOverlay(keyboard, mouse);
                return;
            case GameplayOverlayKind.LastToDieSurvivorMenu:
                UpdateLastToDieSurvivorMenu(keyboard, mouse);
                return;
            case GameplayOverlayKind.LastToDiePerkMenu:
                UpdateLastToDiePerkMenu(keyboard, mouse);
                return;
            case GameplayOverlayKind.QuitPrompt:
                UpdateQuitPrompt(keyboard, mouse);
                return;
            case GameplayOverlayKind.ControlsMenu:
                UpdateControlsMenu(keyboard, mouse);
                return;
            case GameplayOverlayKind.ClientPowers:
                UpdateClientPowersMenu(keyboard, mouse);
                return;
            case GameplayOverlayKind.PracticeSetup:
                UpdatePracticeSetupMenu(keyboard, mouse);
                return;
            case GameplayOverlayKind.PluginOptionsMenu:
                UpdatePluginOptionsMenu(keyboard, mouse);
                return;
            case GameplayOverlayKind.OptionsMenu:
                UpdateOptionsMenu(keyboard, mouse);
                return;
            case GameplayOverlayKind.InGameMenu:
                UpdateInGameMenu(keyboard, mouse);
                return;
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

}
