#nullable enable

using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool TryUpdateOpenMenuOverlay(KeyboardState keyboard, MouseState mouse)
    {
        if (_hostSetupOpen)
        {
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
        }

        if (_clientPowersOpen)
        {
            UpdateClientPowersMenu(keyboard, mouse);
            return true;
        }

        if (_practiceSetupOpen)
        {
            UpdatePracticeSetupMenu(keyboard, mouse);
            return true;
        }

        if (_creditsOpen)
        {
            UpdateCreditsMenu(keyboard, mouse);
            return true;
        }

        if (_lobbyBrowserOpen)
        {
            UpdateLobbyBrowserState(keyboard, mouse);
            return true;
        }

        if (_manualConnectOpen)
        {
            UpdateManualConnectMenu(keyboard, mouse);
            return true;
        }

        if (_controlsMenuOpen)
        {
            UpdateControlsMenu(keyboard, mouse);
            return true;
        }

        if (_lastToDieMenuOpen)
        {
            UpdateLastToDieMenu(keyboard, mouse);
            return true;
        }

        if (_pluginOptionsMenuOpen)
        {
            UpdatePluginOptionsMenu(keyboard, mouse);
            return true;
        }

        if (_optionsMenuOpen)
        {
            UpdateOptionsMenu(keyboard, mouse);
            return true;
        }

        return false;
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
