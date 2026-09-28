#nullable enable

using Microsoft.Xna.Framework.Input;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class GameplayOverlayController
    {
        private readonly IGameplayContext _context;

        public GameplayOverlayController(IGameplayContext context)
        {
            _context = context;
        }

        public GameplayOverlayKind GetActiveOverlay()
        {
            if (_context.IsLastToDieFailureOverlayActive())
            {
                return GameplayOverlayKind.LastToDieFailure;
            }

            if (_context.IsLastToDieDeathFocusPresentationActive())
            {
                return GameplayOverlayKind.LastToDieDeathFocusPresentation;
            }

            if (_context.IsLastToDieStageClearOverlayActive())
            {
                return GameplayOverlayKind.LastToDieStageClear;
            }

            if (_context._lastToDieSurvivorMenuOpen)
            {
                return GameplayOverlayKind.LastToDieSurvivorMenu;
            }

            if (_context._lastToDiePerkMenuOpen)
            {
                return GameplayOverlayKind.LastToDiePerkMenu;
            }

            if (_context._quitPromptOpen)
            {
                return GameplayOverlayKind.QuitPrompt;
            }

            if (_context._controlsMenuOpen)
            {
                return GameplayOverlayKind.ControlsMenu;
            }

            if (_context._clientPowersOpen)
            {
                return GameplayOverlayKind.ClientPowers;
            }

            if (_context._practiceSetupOpen)
            {
                return GameplayOverlayKind.PracticeSetup;
            }

            if (_context._pluginOptionsMenuOpen)
            {
                return GameplayOverlayKind.PluginOptionsMenu;
            }

            if (_context._optionsMenuOpen)
            {
                return GameplayOverlayKind.OptionsMenu;
            }

            if (_context._customBubbleEditorOpen)
            {
                return GameplayOverlayKind.CustomBubbleEditor;
            }

            if (_context._friendsMenuOpen && !_context._inGameMenuOpen)
            {
                return GameplayOverlayKind.SocialMenu;
            }

            if (_context._hudEditorOpen)
            {
                return GameplayOverlayKind.HudEditor;
            }

            if (_context._voteMenuOpen)
            {
                return GameplayOverlayKind.VoteMenu;
            }

            if (_context._inGameMenuOpen)
            {
                return GameplayOverlayKind.InGameMenu;
            }

            if (_context._debugMenuOpen)
            {
                return GameplayOverlayKind.DebugMenu;
            }

            if (_context._gameplayLoadoutMenuOpen)
            {
                return GameplayOverlayKind.LoadoutMenu;
            }

            return GameplayOverlayKind.None;
        }

        public void Update(KeyboardState keyboard, MouseState mouse)
        {
            // Loading owns input until it can render the ordinary menus again.
            if (_context.IsGameplayLoadingForMenuInput()) return;
            if (_context._friendsMenuOpen && _context._inGameMenuOpen)
            {
                _context.UpdateFriendsMenu(keyboard, mouse);
                return;
            }

            switch (GetActiveOverlay())
            {
                case GameplayOverlayKind.LastToDieFailure:
                    _context.UpdateLastToDieFailureOverlay(keyboard, mouse);
                    return;
                case GameplayOverlayKind.LastToDieDeathFocusPresentation:
                    _context.UpdateLastToDieDeathFocusPresentation();
                    return;
                case GameplayOverlayKind.LastToDieStageClear:
                    _context.UpdateLastToDieStageClearOverlay(keyboard, mouse);
                    return;
                case GameplayOverlayKind.LastToDieSurvivorMenu:
                    _context.UpdateLastToDieSurvivorMenu(keyboard, mouse);
                    return;
                case GameplayOverlayKind.LastToDiePerkMenu:
                    _context.UpdateLastToDiePerkMenu(keyboard, mouse);
                    return;
                case GameplayOverlayKind.QuitPrompt:
                    _context.UpdateQuitPrompt(keyboard, mouse);
                    return;
                case GameplayOverlayKind.ControlsMenu:
                    _context.UpdateControlsMenu(keyboard, mouse);
                    return;
                case GameplayOverlayKind.ClientPowers:
                    _context.UpdateClientPowersMenu(keyboard, mouse);
                    return;
                case GameplayOverlayKind.PracticeSetup:
                    _context.UpdatePracticeSetupMenu(keyboard, mouse);
                    return;
                case GameplayOverlayKind.PluginOptionsMenu:
                    _context.UpdatePluginOptionsMenu(keyboard, mouse);
                    return;
                case GameplayOverlayKind.OptionsMenu:
                    _context.UpdateOptionsMenu(keyboard, mouse);
                    return;
                case GameplayOverlayKind.CustomBubbleEditor:
                    _context.UpdateCustomBubbleEditor(keyboard, mouse);
                    return;
                case GameplayOverlayKind.SocialMenu:
                    _context.UpdateFriendsMenu(keyboard, mouse);
                    return;
                case GameplayOverlayKind.HudEditor:
                    _context.UpdateHudEditor(keyboard, mouse);
                    return;
                case GameplayOverlayKind.VoteMenu:
                    _context.UpdateVoteMenu(keyboard, mouse);
                    return;
                case GameplayOverlayKind.InGameMenu:
                    _context.UpdateInGameMenu(keyboard, mouse);
                    return;
                case GameplayOverlayKind.DebugMenu:
                    _context.UpdateDebugMenu(keyboard, mouse);
                    return;
                case GameplayOverlayKind.LoadoutMenu:
                    _context.UpdateGameplayLoadoutMenu(keyboard, mouse);
                    return;
            }
        }
}
