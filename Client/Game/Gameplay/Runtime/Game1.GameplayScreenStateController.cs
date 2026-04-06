#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayScreenStateController
    {
        private readonly Game1 _game;

        public GameplayScreenStateController(Game1 game)
        {
            _game = game;
        }

        public void UpdateGameplayScreenState(KeyboardState keyboard)
        {
            var escapePressed = keyboard.IsKeyDown(Keys.Escape) && !_game._previousKeyboard.IsKeyDown(Keys.Escape);
            var changeTeamPressed = _game.IsKeyPressed(keyboard, _game._inputBindings.ChangeTeam);
            var changeClassPressed = _game.IsKeyPressed(keyboard, _game._inputBindings.ChangeClass);
            if (_game._chatSubmitAwaitingOpenKeyRelease
                && !Game1.IsChatShortcutHeld(keyboard))
            {
                _game._chatSubmitAwaitingOpenKeyRelease = false;
            }

            var openPublicChatPressed = _game.CanUseGameplayChatShortcut() && _game.IsChatShortcutPressed(keyboard, Keys.Y);
            var openTeamChatPressed = _game.CanUseGameplayChatShortcut() && _game.IsChatShortcutPressed(keyboard, Keys.U);
            var pausePressed = GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || escapePressed;

            if (_game.CanOpenGameplayChat()
                && (openPublicChatPressed || openTeamChatPressed))
            {
                _game.OpenChat(teamOnly: openTeamChatPressed);
                return;
            }

            if (_game._chatOpen && escapePressed)
            {
                _game.ResetChatInputState();
                return;
            }

            _game.UpdateSpectatorTrackingHotkeys(keyboard);

            if (_game.CanToggleGameplaySelectionMenus())
            {
                if (changeTeamPressed)
                {
                    _game.ToggleGameplayTeamSelection();
                }
                else if (!_game._world.LocalPlayerAwaitingJoin && changeClassPressed)
                {
                    _game.ToggleGameplayClassSelection();
                }
            }

            if (_game._consoleOpen && escapePressed)
            {
                _game._consoleOpen = false;
            }
            else if (_game._chatOpen && escapePressed)
            {
                _game.ResetChatInputState();
            }
            else if (_game._teamSelectOpen && escapePressed && !_game._world.LocalPlayerAwaitingJoin)
            {
                _game.CloseGameplaySelectionMenus();
            }
            else if (_game._classSelectOpen && escapePressed)
            {
                _game.CloseGameplaySelectionMenus();
            }
            else if (_game.CanOpenInGamePauseMenu() && pausePressed)
            {
                _game.OpenInGameMenu();
            }

            if (_game._world.MatchState.IsEnded || (_game._killCamEnabled && _game._world.LocalDeathCam is not null))
            {
                _game.CloseGameplaySelectionMenus();
            }

            if (_game._passwordPromptOpen)
            {
                _game.CloseGameplaySelectionMenus();
            }
        }
    }
}
