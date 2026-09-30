#nullable enable

using Microsoft.Xna.Framework.Input;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class MainMenuOverlayController
    {
        private readonly IMenuContext _context;

        public MainMenuOverlayController(IMenuContext context)
        {
            _context = context;
        }

        public MainMenuOverlayKind GetActiveOverlay()
        {
            if (_context._namePromptOpen)
            {
                return MainMenuOverlayKind.NamePrompt;
            }

            if (_context._hostSetupOpen)
            {
                return MainMenuOverlayKind.HostSetup;
            }

            if (_context._clientPowersOpen)
            {
                return MainMenuOverlayKind.ClientPowers;
            }

            if (_context._practiceSetupOpen)
            {
                return MainMenuOverlayKind.PracticeSetup;
            }

            if (_context._creditsOpen)
            {
                return MainMenuOverlayKind.Credits;
            }

            if (_context._friendsMenuOpen)
            {
                return MainMenuOverlayKind.FriendsMenu;
            }

            if (_context._lobbyBrowserOpen)
            {
                return MainMenuOverlayKind.LobbyBrowser;
            }

            if (_context._manualConnectOpen)
            {
                return MainMenuOverlayKind.ManualConnect;
            }

            if (_context._controlsMenuOpen)
            {
                return MainMenuOverlayKind.ControlsMenu;
            }

            if (_context._lastToDieMenuOpen)
            {
                return MainMenuOverlayKind.LastToDieMenu;
            }

            if (_context._jumpMenuOpen)
            {
                return MainMenuOverlayKind.JumpMenu;
            }

            if (_context._pluginOptionsMenuOpen)
            {
                return MainMenuOverlayKind.PluginOptionsMenu;
            }

            if (_context._optionsMenuOpen)
            {
                return MainMenuOverlayKind.OptionsMenu;
            }

            if (_context._customBubbleEditorOpen)
            {
                return MainMenuOverlayKind.CustomBubbleEditor;
            }

            return MainMenuOverlayKind.None;
        }

        public bool TryUpdate(KeyboardState keyboard, MouseState mouse)
        {
            switch (GetActiveOverlay())
            {
                case MainMenuOverlayKind.NamePrompt:
                    _context.UpdatePlayerNamePrompt(keyboard);
                    return true;
                case MainMenuOverlayKind.HostSetup:
                    if ((keyboard.IsKeyDown(Keys.Escape) && !_context._previousKeyboard.IsKeyDown(Keys.Escape))
                        || _context.IsControllerMenuBackPressed())
                    {
                        if (_context._hostSetupEditField != HostSetupEditField.None)
                        {
                            _context._hostSetupState.CancelEditSnapshot();
                            _context.Hosting.HostSetup.ClearHostSetupFocus();
                            return true;
                        }

                        if (!_context.TryHandleServerLauncherBackAction())
                        {
                            _context.CloseHostSetupMenu();
                        }

                        return true;
                    }

                    _context.UpdateHostSetupMenu(mouse);
                    return true;
                case MainMenuOverlayKind.ClientPowers:
                    _context.UpdateClientPowersMenu(keyboard, mouse);
                    return true;
                case MainMenuOverlayKind.PracticeSetup:
                    _context.UpdatePracticeSetupMenu(keyboard, mouse);
                    return true;
                case MainMenuOverlayKind.Credits:
                    _context.UpdateCreditsMenu(keyboard, mouse);
                    return true;
                case MainMenuOverlayKind.FriendsMenu:
                    _context.UpdateFriendsMenu(keyboard, mouse);
                    return true;
                case MainMenuOverlayKind.LobbyBrowser:
                    _context.UpdateLobbyBrowserState(keyboard, mouse);
                    return true;
                case MainMenuOverlayKind.ManualConnect:
                    _context.UpdateManualConnectMenu(keyboard, mouse);
                    return true;
                case MainMenuOverlayKind.ControlsMenu:
                    _context.UpdateControlsMenu(keyboard, mouse);
                    return true;
                case MainMenuOverlayKind.LastToDieMenu:
                    _context.UpdateLastToDieMenu(keyboard, mouse);
                    return true;
                case MainMenuOverlayKind.JumpMenu:
                    _context.UpdateJumpMenu(keyboard, mouse);
                    return true;
                case MainMenuOverlayKind.PluginOptionsMenu:
                    _context.UpdatePluginOptionsMenu(keyboard, mouse);
                    return true;
                case MainMenuOverlayKind.OptionsMenu:
                    _context.UpdateOptionsMenu(keyboard, mouse);
                    return true;
                case MainMenuOverlayKind.CustomBubbleEditor:
                    _context.UpdateCustomBubbleEditor(keyboard, mouse);
                    return true;
                default:
                    return false;
            }
        }

        public bool TryDraw()
        {
            switch (GetActiveOverlay())
            {
                case MainMenuOverlayKind.NamePrompt:
                    _context.DrawPlayerNamePrompt();
                    return true;
                case MainMenuOverlayKind.OptionsMenu:
                    _context.DrawOptionsMenu();
                    return true;
                case MainMenuOverlayKind.CustomBubbleEditor:
                    _context.DrawCustomBubbleEditor();
                    return true;
                case MainMenuOverlayKind.PluginOptionsMenu:
                    _context.DrawPluginOptionsMenu();
                    return true;
                case MainMenuOverlayKind.ControlsMenu:
                    _context.DrawControlsMenu();
                    return true;
                case MainMenuOverlayKind.LastToDieMenu:
                    _context.DrawLastToDieMenu();
                    return true;
                case MainMenuOverlayKind.JumpMenu:
                    _context.DrawJumpMenu();
                    return true;
                case MainMenuOverlayKind.HostSetup:
                    _context.DrawHostSetupMenu();
                    return true;
                case MainMenuOverlayKind.ClientPowers:
                    _context.DrawClientPowersMenu();
                    return true;
                case MainMenuOverlayKind.PracticeSetup:
                    _context.DrawPracticeSetupMenu();
                    return true;
                case MainMenuOverlayKind.Credits:
                    _context.DrawCreditsMenu();
                    return true;
                case MainMenuOverlayKind.FriendsMenu:
                    _context.DrawFriendsMenu();
                    return true;
                case MainMenuOverlayKind.LobbyBrowser:
                    _context.DrawLobbyBrowserMenu();
                    return true;
                case MainMenuOverlayKind.ManualConnect:
                    _context.DrawManualConnectMenu();
                    return true;
                default:
                    return false;
            }
        }
}
