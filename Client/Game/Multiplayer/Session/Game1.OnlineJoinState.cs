#nullable enable

namespace OpenGarrison.Client;

public partial class Game1
{
    private void OpenNetworkPasswordPrompt(string message)
    {
        _passwordPromptOpen = true;
        _passwordEditBuffer = string.Empty;
        _passwordPromptMessage = message;
        _consoleOpen = false;
        _inGameMenuOpen = false;
        _optionsMenuOpen = false;
        _pluginOptionsMenuOpen = false;
        _controlsMenuOpen = false;
        _teamSelectOpen = false;
        _classSelectOpen = false;
    }

    private void CloseNetworkPasswordPrompt()
    {
        _passwordPromptOpen = false;
        _passwordEditBuffer = string.Empty;
        _passwordPromptMessage = string.Empty;
    }

    private void EnterOnlineSpectatorState(string statusMessage)
    {
        ResetSpectatorTracking(enableTracking: true);
        _teamSelectOpen = false;
        _classSelectOpen = false;
        _menuStatusMessage = statusMessage;
    }

    private void EnterOnlineClassSelectionState(string statusMessage)
    {
        ResetSpectatorTracking(enableTracking: false);
        _teamSelectOpen = false;
        _classSelectOpen = true;
        _menuStatusMessage = statusMessage;
    }

    private void OpenOnlineTeamSelection(bool clearPendingSelections, string statusMessage)
    {
        if (clearPendingSelections)
        {
            _networkClient.ClearPendingTeamSelection();
            _networkClient.ClearPendingClassSelection();
        }

        _teamSelectOpen = true;
        _classSelectOpen = false;
        ResetChatInputState();
        _consoleOpen = false;
        _menuStatusMessage = statusMessage;
    }
}
