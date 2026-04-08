#nullable enable

namespace OpenGarrison.Client;

public partial class Game1
{
    private void OpenNetworkPasswordPrompt(string message)
    {
        _connectionFlowController.OpenNetworkPasswordPrompt(message);
    }

    private void CloseNetworkPasswordPrompt()
    {
        _connectionFlowController.CloseNetworkPasswordPrompt();
    }

    private void EnterOnlineSpectatorState(string statusMessage)
    {
        _connectionFlowController.EnterOnlineSpectatorState(statusMessage);
    }

    private void EnterOnlineClassSelectionState(string statusMessage)
    {
        _connectionFlowController.EnterOnlineClassSelectionState(statusMessage);
    }

    private void OpenOnlineTeamSelection(bool clearPendingSelections, string statusMessage)
    {
        _connectionFlowController.OpenOnlineTeamSelection(clearPendingSelections, statusMessage);
    }
}
