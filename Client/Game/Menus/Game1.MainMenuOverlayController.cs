#nullable enable

using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class MainMenuOverlayController
    {
        private readonly Game1 _game;

        public MainMenuOverlayController(Game1 game)
        {
            _game = game;
        }

        public MainMenuOverlayKind GetActiveOverlay()
        {
            if (_game._hostSetupOpen)
            {
                return MainMenuOverlayKind.HostSetup;
            }

            if (_game._clientPowersOpen)
            {
                return MainMenuOverlayKind.ClientPowers;
            }

            if (_game._practiceSetupOpen)
            {
                return MainMenuOverlayKind.PracticeSetup;
            }

            if (_game._creditsOpen)
            {
                return MainMenuOverlayKind.Credits;
            }

            if (_game._lobbyBrowserOpen)
            {
                return MainMenuOverlayKind.LobbyBrowser;
            }

            if (_game._manualConnectOpen)
            {
                return MainMenuOverlayKind.ManualConnect;
            }

            if (_game._controlsMenuOpen)
            {
                return MainMenuOverlayKind.ControlsMenu;
            }

            if (_game._lastToDieMenuOpen)
            {
                return MainMenuOverlayKind.LastToDieMenu;
            }

            if (_game._pluginOptionsMenuOpen)
            {
                return MainMenuOverlayKind.PluginOptionsMenu;
            }

            if (_game._optionsMenuOpen)
            {
                return MainMenuOverlayKind.OptionsMenu;
            }

            return MainMenuOverlayKind.None;
        }

        public bool TryUpdate(KeyboardState keyboard, MouseState mouse)
        {
            switch (GetActiveOverlay())
            {
                case MainMenuOverlayKind.HostSetup:
                    if (keyboard.IsKeyDown(Keys.Escape) && !_game._previousKeyboard.IsKeyDown(Keys.Escape))
                    {
                        if (!_game.TryHandleServerLauncherBackAction())
                        {
                            _game.CloseHostSetupMenu();
                        }

                        return true;
                    }

                    _game.UpdateHostSetupMenu(mouse);
                    return true;
                case MainMenuOverlayKind.ClientPowers:
                    _game.UpdateClientPowersMenu(keyboard, mouse);
                    return true;
                case MainMenuOverlayKind.PracticeSetup:
                    _game.UpdatePracticeSetupMenu(keyboard, mouse);
                    return true;
                case MainMenuOverlayKind.Credits:
                    _game.UpdateCreditsMenu(keyboard, mouse);
                    return true;
                case MainMenuOverlayKind.LobbyBrowser:
                    _game.UpdateLobbyBrowserState(keyboard, mouse);
                    return true;
                case MainMenuOverlayKind.ManualConnect:
                    _game.UpdateManualConnectMenu(keyboard, mouse);
                    return true;
                case MainMenuOverlayKind.ControlsMenu:
                    _game.UpdateControlsMenu(keyboard, mouse);
                    return true;
                case MainMenuOverlayKind.LastToDieMenu:
                    _game.UpdateLastToDieMenu(keyboard, mouse);
                    return true;
                case MainMenuOverlayKind.PluginOptionsMenu:
                    _game.UpdatePluginOptionsMenu(keyboard, mouse);
                    return true;
                case MainMenuOverlayKind.OptionsMenu:
                    _game.UpdateOptionsMenu(keyboard, mouse);
                    return true;
                default:
                    return false;
            }
        }

        public bool TryDraw()
        {
            switch (GetActiveOverlay())
            {
                case MainMenuOverlayKind.OptionsMenu:
                    _game.DrawOptionsMenu();
                    return true;
                case MainMenuOverlayKind.PluginOptionsMenu:
                    _game.DrawPluginOptionsMenu();
                    return true;
                case MainMenuOverlayKind.ControlsMenu:
                    _game.DrawControlsMenu();
                    return true;
                case MainMenuOverlayKind.LastToDieMenu:
                    _game.DrawLastToDieMenu();
                    return true;
                case MainMenuOverlayKind.HostSetup:
                    _game.DrawHostSetupMenu();
                    return true;
                case MainMenuOverlayKind.ClientPowers:
                    _game.DrawClientPowersMenu();
                    return true;
                case MainMenuOverlayKind.PracticeSetup:
                    _game.DrawPracticeSetupMenu();
                    return true;
                case MainMenuOverlayKind.Credits:
                    _game.DrawCreditsMenu();
                    return true;
                case MainMenuOverlayKind.LobbyBrowser:
                    _game.DrawLobbyBrowserMenu();
                    return true;
                case MainMenuOverlayKind.ManualConnect:
                    _game.DrawManualConnectMenu();
                    return true;
                default:
                    return false;
            }
        }
    }
}
