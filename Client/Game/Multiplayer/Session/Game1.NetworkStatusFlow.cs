#nullable enable

namespace OpenGarrison.Client;

public partial class Game1
{
    public void SetNetworkStatus(string statusMessage)
    {
        _menuStatusMessage = statusMessage;
    }

    public void AddNetworkConsoleLine(string message)
    {
        AddConsoleLine(message);
    }

    public void SetNetworkStatusAndConsole(string statusMessage, string consoleMessage)
    {
        SetNetworkStatus(statusMessage);
        AddNetworkConsoleLine(consoleMessage);
    }

    public void ReturnToMainMenuWithNetworkStatus(string statusMessage, string consoleMessage)
    {
        _gameplayManager.Session.ClearPendingConnectionCandidates();
        CancelNetworkWorldWarmup();
        HideLoadingOverlay();
        ClearPendingNetworkMapSync();
        ReturnToMainMenu(statusMessage);
        AddNetworkConsoleLine(consoleMessage);
    }

    public void ReturnToMainMenuWithNetworkStatus(string statusMessage)
    {
        _gameplayManager.Session.ClearPendingConnectionCandidates();
        CancelNetworkWorldWarmup();
        HideLoadingOverlay();
        ClearPendingNetworkMapSync();
        ReturnToMainMenu(statusMessage);
        AddNetworkConsoleLine(statusMessage);
    }
}
