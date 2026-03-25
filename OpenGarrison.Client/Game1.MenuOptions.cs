#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Globalization;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool IsGameplayMenuOpen()
    {
        return _inGameMenuOpen || _optionsMenuOpen || _controlsMenuOpen || _quitPromptOpen;
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
        _controlsMenuOpen = false;
        _pendingControlsBinding = null;
        _optionsHoverIndex = -1;
        _controlsHoverIndex = -1;
        _editingPlayerName = false;
        _playerNameEditBuffer = _world.LocalPlayer.DisplayName;
    }

    private void CloseOptionsMenu()
    {
        var reopenInGameMenu = _optionsMenuOpenedFromGameplay && !_mainMenuOpen;
        _optionsMenuOpen = false;
        _optionsMenuOpenedFromGameplay = false;
        _optionsHoverIndex = -1;
        _editingPlayerName = false;
        _playerNameEditBuffer = _world.LocalPlayer.DisplayName;
        if (reopenInGameMenu)
        {
            OpenInGameMenu();
        }
    }

    private void OpenControlsMenu(bool fromGameplay)
    {
        _controlsMenuOpen = true;
        _controlsMenuOpenedFromGameplay = fromGameplay;
        _controlsHoverIndex = -1;
        _pendingControlsBinding = null;
        _optionsMenuOpen = false;
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
        _optionsMenuOpen = false;
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
        const int items = 4;

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
            if (_inGameMenuHoverIndex < 0 || _inGameMenuHoverIndex >= items)
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

        switch (_inGameMenuHoverIndex)
        {
            case 0:
                OpenOptionsMenu(fromGameplay: true);
                CloseInGameMenu();
                break;
            case 1:
                CloseInGameMenu();
                break;
            case 2:
                ReturnToMainMenu("Disconnected.");
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

        string[] items = ["Options", "Return to Game", "Disconnect", "Quit Game"];
        var position = new Vector2(40f, 300f);
        for (var index = 0; index < items.Length; index += 1)
        {
            var color = index == _inGameMenuHoverIndex ? Color.Red : Color.White;
            DrawBitmapFontText(items[index], position, color, 1f);
            position.Y += 30f;
        }
    }

    private void UpdateOptionsMenu(KeyboardState keyboard, MouseState mouse)
    {
        const int items = 14;
        GetOptionsMenuLayout(out var xbegin, out var ybegin, out var spacing, out var width, out _);

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
                _healerRadarEnabled = !_healerRadarEnabled;
                PersistClientSettings();
                break;
            case 7:
                _showHealerEnabled = !_showHealerEnabled;
                PersistClientSettings();
                break;
            case 8:
                _showHealingEnabled = !_showHealingEnabled;
                PersistClientSettings();
                break;
            case 9:
                _showHealthBarEnabled = !_showHealthBarEnabled;
                PersistClientSettings();
                break;
            case 10:
                _killCamEnabled = !_killCamEnabled;
                PersistClientSettings();
                break;
            case 11:
                _clientSettings.VSync = !_clientSettings.VSync;
                ApplyGraphicsSettings();
                break;
            case 12:
                OpenControlsMenu(_optionsMenuOpenedFromGameplay);
                break;
            case 13:
                CloseOptionsMenu();
                break;
        }
    }

    private void DrawOptionsMenu()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.8f);
        GetOptionsMenuLayout(out var xbegin, out var ybegin, out var spacing, out _, out var valueX);
        var compactLayout = ViewportHeight < 540;
        var textScale = compactLayout ? 0.92f : 1f;

        string[] labels =
        [
            "Player name:",
            "Fullscreen:",
            "Music:",
            "Ingame Aspect Ratio:",
            "Particles:",
            "Gibs:",
            "Healer Radar:",
            "Show Healer:",
            "Show Healing:",
            "Additional Healthbar:",
            "Kill Cam:",
            "V Sync:",
            "Controls",
            "Back",
        ];
        string[] values =
        [
            _editingPlayerName ? _playerNameEditBuffer + "_" : _world.LocalPlayer.DisplayName,
            _graphics.IsFullScreen ? "On" : "Off",
            GetMusicModeLabel(_musicMode),
            GetIngameResolutionLabel(_ingameResolution),
            GetParticleModeLabel(_particleMode),
            GetGibLevelLabel(_gibLevel),
            _healerRadarEnabled ? "Enabled" : "Disabled",
            _showHealerEnabled ? "Enabled" : "Disabled",
            _showHealingEnabled ? "Enabled" : "Disabled",
            _showHealthBarEnabled ? "Enabled" : "Disabled",
            _killCamEnabled ? "Enabled" : "Disabled",
            _graphics.SynchronizeWithVerticalRetrace ? "Enabled" : "Disabled",
            string.Empty,
            string.Empty,
        ];

        var labelPosition = new Vector2(xbegin, ybegin);
        for (var index = 0; index < labels.Length; index += 1)
        {
            var color = _editingPlayerName && index == 0
                ? Color.Orange
                : index == _optionsHoverIndex ? Color.Red : Color.White;
            DrawBitmapFontText(labels[index], labelPosition, color, textScale);
            DrawBitmapFontText(values[index], new Vector2(valueX, labelPosition.Y), color, textScale);
            labelPosition.Y += spacing;
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
        var position = new Vector2(40f, 150f);
        for (var index = 0; index < items.Count; index += 1)
        {
            var item = items[index];
            var color = _pendingControlsBinding == item.Binding
                ? Color.Orange
                : index == _controlsHoverIndex ? Color.Red : Color.White;
            DrawBitmapFontText(item.Label, position, color, 1f);
            DrawBitmapFontText(GetBindingDisplayName(item.Key), new Vector2(280f, position.Y), color, 1f);
            position.Y += 28f;
        }

        var backColor = items.Count == _controlsHoverIndex ? Color.Red : Color.White;
        DrawBitmapFontText("Back", position, backColor, 1f);
    }

    private List<(ControlsMenuBinding Binding, string Label, Keys Key)> GetControlsMenuBindings()
    {
        return
        [
            (ControlsMenuBinding.MoveUp, "Jump:", _inputBindings.MoveUp),
            (ControlsMenuBinding.MoveLeft, "Move Left:", _inputBindings.MoveLeft),
            (ControlsMenuBinding.MoveRight, "Move Right:", _inputBindings.MoveRight),
            (ControlsMenuBinding.MoveDown, "Move Down:", _inputBindings.MoveDown),
            (ControlsMenuBinding.Taunt, "Taunt:", _inputBindings.Taunt),
            (ControlsMenuBinding.ChangeTeam, "Change Team:", _inputBindings.ChangeTeam),
            (ControlsMenuBinding.ChangeClass, "Change Class:", _inputBindings.ChangeClass),
            (ControlsMenuBinding.ShowScoreboard, "Show Scores:", _inputBindings.ShowScoreboard),
            (ControlsMenuBinding.ToggleConsole, "Console:", _inputBindings.ToggleConsole),
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
        }
    }

    private static string GetControlsBindingLabel(ControlsMenuBinding binding)
    {
        return binding switch
        {
            ControlsMenuBinding.MoveUp => "Jump",
            ControlsMenuBinding.MoveLeft => "Move Left",
            ControlsMenuBinding.MoveRight => "Move Right",
            ControlsMenuBinding.MoveDown => "Move Down",
            ControlsMenuBinding.Taunt => "Taunt",
            ControlsMenuBinding.ChangeTeam => "Change Team",
            ControlsMenuBinding.ChangeClass => "Change Class",
            ControlsMenuBinding.ShowScoreboard => "Show Scores",
            ControlsMenuBinding.ToggleConsole => "Console",
            _ => "Binding",
        };
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

    private void GetOptionsMenuLayout(out float xbegin, out float ybegin, out float spacing, out float width, out float valueX)
    {
        xbegin = 40f;
        valueX = 240f;
        width = 320f;
        if (ViewportHeight < 540)
        {
            ybegin = 104f;
            spacing = 26f;
            width = 340f;
            valueX = 232f;
            return;
        }

        ybegin = 170f;
        spacing = 30f;
    }
}
