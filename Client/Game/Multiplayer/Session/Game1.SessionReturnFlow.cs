#nullable enable

using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    public void ReturnToMainMenu(string? statusMessage = null)
    {
        _garrisonBuilderQuickTestActive = false;
        _gameplayManager.Session.ReturnToMainMenu(statusMessage);
    }

    private void ResetActiveSessionState()
    {
        _gameplayManager.Session.ResetActiveSessionState();
    }
}
