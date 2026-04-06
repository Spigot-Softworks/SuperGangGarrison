#nullable enable

using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void HandleWelcomeMessage(WelcomeMessage welcome)
    {
        _gameplaySessionController.HandleWelcomeMessage(welcome);
    }

    private void HandleConnectionDeniedMessage(ConnectionDeniedMessage denied)
    {
        ReturnToMainMenuWithNetworkStatus(denied.Reason, $"connect denied: {denied.Reason}");
    }

    private void HandlePasswordRequestMessage()
    {
        OpenNetworkPasswordPrompt("Server requires a password.");
        AddNetworkConsoleLine("server requires a password");
    }

    private void HandlePasswordResultMessage(PasswordResultMessage passwordResult)
    {
        if (passwordResult.Accepted)
        {
            CloseNetworkPasswordPrompt();
            if (!_networkClient.IsSpectator)
            {
                OpenOnlineTeamSelection(clearPendingSelections: false, statusMessage: string.Empty);
            }

            AddNetworkConsoleLine("password accepted");
            return;
        }

        var rejectionReason = string.IsNullOrWhiteSpace(passwordResult.Reason) ? "Password rejected." : passwordResult.Reason;
        ReturnToMainMenuWithNetworkStatus(rejectionReason, $"password rejected: {passwordResult.Reason}");
    }
}
