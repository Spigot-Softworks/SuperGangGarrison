#nullable enable

namespace OpenGarrison.Client;

public partial class Game1
{
    private void SetNetworkStatus(string statusMessage)
    {
        _menuStatusMessage = statusMessage;
    }

    private void AddNetworkConsoleLine(string message)
    {
        AddConsoleLine(message);
    }

    private void SetNetworkStatusAndConsole(string statusMessage, string consoleMessage)
    {
        SetNetworkStatus(statusMessage);
        AddNetworkConsoleLine(consoleMessage);
    }

    private void ReturnToMainMenuWithNetworkStatus(string statusMessage, string consoleMessage)
    {
        ReturnToMainMenu(statusMessage);
        AddNetworkConsoleLine(consoleMessage);
    }

    private void ReturnToMainMenuWithNetworkStatus(string statusMessage)
    {
        ReturnToMainMenu(statusMessage);
        AddNetworkConsoleLine(statusMessage);
    }
}
