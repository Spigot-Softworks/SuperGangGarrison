#nullable enable

using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void ReturnToMainMenu(string? statusMessage = null)
    {
        _garrisonBuilderQuickTestActive = false;
        _gameplaySessionController.ReturnToMainMenu(statusMessage);
    }

    private void ResetActiveSessionState()
    {
        _gameplaySessionController.ResetActiveSessionState();
    }
}
