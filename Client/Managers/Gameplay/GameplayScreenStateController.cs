#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class GameplayScreenStateController
    {
        private readonly IGameplayContext _context;

        public GameplayScreenStateController(IGameplayContext context)
        {
            _context = context;
        }

        public void UpdateGameplayScreenState(KeyboardState keyboard, MouseState mouse)
        {
            var escapePressed = _context.IsKeyPressed(keyboard, Keys.Escape);
            var changeTeamPressed = _context.IsBindingPressed(keyboard, mouse, _context._inputBindings.ChangeTeam);
            var changeClassPressed = _context.IsBindingPressed(keyboard, mouse, _context._inputBindings.ChangeClass);
            if (_context._chatSubmitAwaitingOpenKeyRelease
                && !Game1.IsChatShortcutHeld(keyboard))
            {
                _context._chatSubmitAwaitingOpenKeyRelease = false;
            }

            var openPublicChatPressed = _context.CanUseGameplayChatShortcut() && _context.IsChatShortcutPressed(keyboard, Keys.Y);
            var openTeamChatPressed = _context.CanUseGameplayChatShortcut() && _context.IsChatShortcutPressed(keyboard, Keys.U);
            var controllerChangeTeamPressed = _context.IsControllerBindingPressed(_context._clientSettings.ControllerChangeTeamButton);
            var controllerChangeClassPressed = _context.IsControllerBindingPressed(_context._clientSettings.ControllerChangeClassButton);
            var controllerPausePressed = _context.IsControllerBindingPressed(_context._clientSettings.ControllerPauseButton);

            if (_context.CanOpenGameplayChat()
                && (openPublicChatPressed || openTeamChatPressed))
            {
                _context.OpenChat(teamOnly: openTeamChatPressed);
                return;
            }

            if (_context._chatOpen && escapePressed)
            {
                _context.ResetChatInputState();
                return;
            }

            if (_context._chatOpen)
            {
                _context.UpdateChatScrollState(keyboard, mouse);
            }

            _context.UpdateSpectatorTrackingHotkeys(keyboard, mouse);

            if (_context.CanToggleGameplaySelectionMenus())
            {
                if (changeTeamPressed || controllerChangeTeamPressed)
                {
                    _context.ToggleGameplayTeamSelection();
                }
                else if (!_context._world.LocalPlayerAwaitingJoin && (changeClassPressed || controllerChangeClassPressed))
                {
                    _context.ToggleGameplayClassSelection();
                }
            }

            if (_context._consoleOpen && escapePressed)
            {
                _context._consoleOpen = false;
            }
            else if (_context._chatOpen && escapePressed)
            {
                _context.ResetChatInputState();
            }
            else if (_context._teamSelectOpen && escapePressed)
            {
                _context.DismissGameplayTeamSelection();
            }
            else if (_context._classSelectOpen && escapePressed)
            {
                _context.CloseGameplaySelectionMenus();
            }
            else if (_context._buildMenuOpen && escapePressed)
            {
                _context.BeginClosingBuildMenu();
            }
            else if (Game1.ShouldOpenInGamePauseMenu(
                    escapePressed,
                    controllerPausePressed,
                    _context.CanOpenInGamePauseMenu()))
            {
                _context.OpenInGameMenu();
            }

            if (_context._world.MatchState.IsEnded || (_context._killCamEnabled && _context._world.LocalDeathCam is not null))
            {
                _context.CloseGameplaySelectionMenus();
            }

            if (_context._passwordPromptOpen)
            {
                _context.CloseGameplaySelectionMenus();
            }
        }
}
