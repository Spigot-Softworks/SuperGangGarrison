#nullable enable

using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayOverlayController
    {
        private readonly Game1 _game;

        public GameplayOverlayController(Game1 game)
        {
            _game = game;
        }

        public GameplayOverlayKind GetActiveOverlay()
        {
            if (_game.IsLastToDieFailureOverlayActive())
            {
                return GameplayOverlayKind.LastToDieFailure;
            }

            if (_game.IsLastToDieDeathFocusPresentationActive())
            {
                return GameplayOverlayKind.LastToDieDeathFocusPresentation;
            }

            if (_game.IsLastToDieStageClearOverlayActive())
            {
                return GameplayOverlayKind.LastToDieStageClear;
            }

            if (_game._lastToDieSurvivorMenuOpen)
            {
                return GameplayOverlayKind.LastToDieSurvivorMenu;
            }

            if (_game._lastToDiePerkMenuOpen)
            {
                return GameplayOverlayKind.LastToDiePerkMenu;
            }

            if (_game._quitPromptOpen)
            {
                return GameplayOverlayKind.QuitPrompt;
            }

            if (_game._controlsMenuOpen)
            {
                return GameplayOverlayKind.ControlsMenu;
            }

            if (_game._clientPowersOpen)
            {
                return GameplayOverlayKind.ClientPowers;
            }

            if (_game._practiceSetupOpen)
            {
                return GameplayOverlayKind.PracticeSetup;
            }

            if (_game._pluginOptionsMenuOpen)
            {
                return GameplayOverlayKind.PluginOptionsMenu;
            }

            if (_game._optionsMenuOpen)
            {
                return GameplayOverlayKind.OptionsMenu;
            }

            if (_game._inGameMenuOpen)
            {
                return GameplayOverlayKind.InGameMenu;
            }

            if (_game._gameplayLoadoutMenuOpen)
            {
                return GameplayOverlayKind.LoadoutMenu;
            }

            return GameplayOverlayKind.None;
        }

        public void Update(KeyboardState keyboard, MouseState mouse)
        {
            switch (GetActiveOverlay())
            {
                case GameplayOverlayKind.LastToDieFailure:
                    _game.UpdateLastToDieFailureOverlay(keyboard, mouse);
                    return;
                case GameplayOverlayKind.LastToDieDeathFocusPresentation:
                    _game.UpdateLastToDieDeathFocusPresentation();
                    return;
                case GameplayOverlayKind.LastToDieStageClear:
                    _game.UpdateLastToDieStageClearOverlay(keyboard, mouse);
                    return;
                case GameplayOverlayKind.LastToDieSurvivorMenu:
                    _game.UpdateLastToDieSurvivorMenu(keyboard, mouse);
                    return;
                case GameplayOverlayKind.LastToDiePerkMenu:
                    _game.UpdateLastToDiePerkMenu(keyboard, mouse);
                    return;
                case GameplayOverlayKind.QuitPrompt:
                    _game.UpdateQuitPrompt(keyboard, mouse);
                    return;
                case GameplayOverlayKind.ControlsMenu:
                    _game.UpdateControlsMenu(keyboard, mouse);
                    return;
                case GameplayOverlayKind.ClientPowers:
                    _game.UpdateClientPowersMenu(keyboard, mouse);
                    return;
                case GameplayOverlayKind.PracticeSetup:
                    _game.UpdatePracticeSetupMenu(keyboard, mouse);
                    return;
                case GameplayOverlayKind.PluginOptionsMenu:
                    _game.UpdatePluginOptionsMenu(keyboard, mouse);
                    return;
                case GameplayOverlayKind.OptionsMenu:
                    _game.UpdateOptionsMenu(keyboard, mouse);
                    return;
                case GameplayOverlayKind.InGameMenu:
                    _game.UpdateInGameMenu(keyboard, mouse);
                    return;
                case GameplayOverlayKind.LoadoutMenu:
                    _game.UpdateGameplayLoadoutMenu(keyboard, mouse);
                    return;
            }
        }
    }
}
