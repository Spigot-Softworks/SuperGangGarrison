#nullable enable

using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public partial class Game1
{
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

        if (_hostSetupOpen)
        {
            if (keyboard.IsKeyDown(Keys.Escape) && !_previousKeyboard.IsKeyDown(Keys.Escape))
            {
                if (!TryHandleServerLauncherBackAction())
                {
                    _hostSetupOpen = false;
                    _hostSetupEditField = HostSetupEditField.None;
                }
                return;
            }

            UpdateHostSetupMenu(mouse);
        }
        else if (_practiceSetupOpen)
        {
            UpdatePracticeSetupMenu(keyboard, mouse);
        }
        else if (_creditsOpen)
        {
            UpdateCreditsMenu(keyboard, mouse);
        }
        else if (_lobbyBrowserOpen)
        {
            UpdateLobbyBrowserState(keyboard, mouse);
        }
        else if (_manualConnectOpen)
        {
            UpdateManualConnectMenu(keyboard, mouse);
        }
        else if (_controlsMenuOpen)
        {
            UpdateControlsMenu(keyboard, mouse);
        }
        else if (_pluginOptionsMenuOpen)
        {
            UpdatePluginOptionsMenu(keyboard, mouse);
        }
        else if (_optionsMenuOpen)
        {
            UpdateOptionsMenu(keyboard, mouse);
        }
        else
        {
            UpdateMainMenu(mouse);
        }
    }
}
