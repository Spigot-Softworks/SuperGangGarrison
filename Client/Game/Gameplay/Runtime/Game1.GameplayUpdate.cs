#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool IsGameplayBindingKey(Keys key)
    {
        return _inputBindings.MoveUp == key
            || _inputBindings.MoveDown == key
            || _inputBindings.MoveLeft == key
            || _inputBindings.MoveRight == key
            || _inputBindings.Taunt == key
            || _inputBindings.CallMedic == key
            || _inputBindings.FireSecondaryWeapon == key
            || _inputBindings.InteractWeapon == key
            || _inputBindings.ChangeTeam == key
            || _inputBindings.ChangeClass == key
            || _inputBindings.ShowScoreboard == key
            || _inputBindings.ToggleConsole == key
            || _inputBindings.OpenBubbleMenuZ == key
            || _inputBindings.OpenBubbleMenuX == key
            || _inputBindings.OpenBubbleMenuC == key;
    }

    private static bool IsChatShortcutHeld(KeyboardState keyboard)
    {
        return keyboard.IsKeyDown(Keys.Y)
            || keyboard.IsKeyDown(Keys.U);
    }

    private bool IsChatShortcutPressed(KeyboardState keyboard, Keys key)
    {
        return IsKeyPressed(keyboard, key);
    }

    private void UpdateGameplayScreenState(KeyboardState keyboard)
    {
        var escapePressed = keyboard.IsKeyDown(Keys.Escape) && !_previousKeyboard.IsKeyDown(Keys.Escape);
        var changeTeamPressed = IsKeyPressed(keyboard, _inputBindings.ChangeTeam);
        var changeClassPressed = IsKeyPressed(keyboard, _inputBindings.ChangeClass);
        if (_chatSubmitAwaitingOpenKeyRelease
            && !IsChatShortcutHeld(keyboard))
        {
            _chatSubmitAwaitingOpenKeyRelease = false;
        }

        var openPublicChatPressed = CanUseGameplayChatShortcut() && IsChatShortcutPressed(keyboard, Keys.Y);
        var openTeamChatPressed = CanUseGameplayChatShortcut() && IsChatShortcutPressed(keyboard, Keys.U);
        var pausePressed = GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || escapePressed;

        if (CanOpenGameplayChat()
            && (openPublicChatPressed || openTeamChatPressed))
        {
            OpenChat(teamOnly: openTeamChatPressed);
            return;
        }

        if (_chatOpen && escapePressed)
        {
            ResetChatInputState();
            return;
        }

        UpdateSpectatorTrackingHotkeys(keyboard);

        if (CanToggleGameplaySelectionMenus())
        {
            if (changeTeamPressed)
            {
                ToggleGameplayTeamSelection();
            }
            else if (!_world.LocalPlayerAwaitingJoin && changeClassPressed)
            {
                ToggleGameplayClassSelection();
            }
        }

        if (_consoleOpen && escapePressed)
        {
            _consoleOpen = false;
        }
        else if (_chatOpen && escapePressed)
        {
            ResetChatInputState();
        }
        else if (_teamSelectOpen && escapePressed && !_world.LocalPlayerAwaitingJoin)
        {
            CloseGameplaySelectionMenus();
        }
        else if (_classSelectOpen && escapePressed)
        {
            CloseGameplaySelectionMenus();
        }
        else if (CanOpenInGamePauseMenu() && pausePressed)
        {
            OpenInGameMenu();
        }

        if (_world.MatchState.IsEnded || (_killCamEnabled && _world.LocalDeathCam is not null))
        {
            CloseGameplaySelectionMenus();
        }

        if (_passwordPromptOpen)
        {
            CloseGameplaySelectionMenus();
        }
    }

    private void FinalizeGameplayFrame(KeyboardState keyboard, MouseState mouse)
    {
        _previousKeyboard = keyboard;
        _previousMouse = mouse;
        _wasLocalPlayerAlive = _world.LocalPlayer.IsAlive;
        _wasDeathCamActive = _killCamEnabled
            && !_world.LocalPlayer.IsAlive
            && _world.LocalDeathCam is not null
            && GetDeathCamElapsedTicks(_world.LocalDeathCam) >= DeathCamFocusDelayTicks;
        _wasMatchEnded = _world.MatchState.IsEnded;
    }
}
