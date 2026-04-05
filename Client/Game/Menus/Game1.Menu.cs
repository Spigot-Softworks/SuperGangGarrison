#nullable enable

using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool TryUpdateOpenMenuOverlay(KeyboardState keyboard, MouseState mouse)
    {
        switch (GetActiveMainMenuOverlay())
        {
            case MainMenuOverlayKind.HostSetup:
                if (keyboard.IsKeyDown(Keys.Escape) && !_previousKeyboard.IsKeyDown(Keys.Escape))
                {
                    if (!TryHandleServerLauncherBackAction())
                    {
                        _hostSetupOpen = false;
                        _hostSetupEditField = HostSetupEditField.None;
                    }

                    return true;
                }

                UpdateHostSetupMenu(mouse);
                return true;
            case MainMenuOverlayKind.ClientPowers:
                UpdateClientPowersMenu(keyboard, mouse);
                return true;
            case MainMenuOverlayKind.PracticeSetup:
                UpdatePracticeSetupMenu(keyboard, mouse);
                return true;
            case MainMenuOverlayKind.Credits:
                UpdateCreditsMenu(keyboard, mouse);
                return true;
            case MainMenuOverlayKind.LobbyBrowser:
                UpdateLobbyBrowserState(keyboard, mouse);
                return true;
            case MainMenuOverlayKind.ManualConnect:
                UpdateManualConnectMenu(keyboard, mouse);
                return true;
            case MainMenuOverlayKind.ControlsMenu:
                UpdateControlsMenu(keyboard, mouse);
                return true;
            case MainMenuOverlayKind.LastToDieMenu:
                UpdateLastToDieMenu(keyboard, mouse);
                return true;
            case MainMenuOverlayKind.PluginOptionsMenu:
                UpdatePluginOptionsMenu(keyboard, mouse);
                return true;
            case MainMenuOverlayKind.OptionsMenu:
                UpdateOptionsMenu(keyboard, mouse);
                return true;
            default:
                return false;
        }
    }

    private void UpdateMenuState(KeyboardState keyboard, MouseState mouse)
    {
        EnsureMenuMusicPlaying();
        StopFaucetMusic();
        StopIngameMusic();

        UpdateLobbyBrowserResponses();
        if (UpdateDevMessagePopup(keyboard, mouse))
        {
            return;
        }

        if (_quitPromptOpen)
        {
            UpdateQuitPrompt(keyboard, mouse);
            return;
        }

        if (!TryUpdateOpenMenuOverlay(keyboard, mouse))
        {
            UpdateMainMenu(keyboard, mouse);
        }
    }
}
