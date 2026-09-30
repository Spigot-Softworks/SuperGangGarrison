#nullable enable

namespace OpenGarrison.Client;

public partial class Game1
{
    private void OpenNetworkPasswordPrompt(string message)
    {
        _sessionManager.Connection.OpenNetworkPasswordPrompt(message);
    }

    private void CloseNetworkPasswordPrompt()
    {
        _sessionManager.Connection.CloseNetworkPasswordPrompt();
    }

    private void EnterOnlineSpectatorState(string statusMessage)
    {
        _sessionManager.Connection.EnterOnlineSpectatorState(statusMessage);
    }

    private void EnterOnlineClassSelectionState(string statusMessage)
    {
        _sessionManager.Connection.EnterOnlineClassSelectionState(statusMessage);
    }

    private void OpenOnlineTeamSelection(bool clearPendingSelections, string statusMessage)
    {
        _sessionManager.Connection.OpenOnlineTeamSelection(clearPendingSelections, statusMessage);
    }
}
