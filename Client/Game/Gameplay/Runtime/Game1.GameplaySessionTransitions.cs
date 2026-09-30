#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    public void ResetGameplayRuntimeState()
    {
        _gameplayManager.Reset.ResetGameplayRuntimeState();
    }

    public void ResetGameplayTransitionEffects()
    {
        _gameplayManager.Reset.ResetGameplayTransitionEffects();
    }

    public void CloseGameplayOverlayState()
    {
        _gameplayManager.OverlayState.CloseGameplayOverlayState();
    }

    public void CloseMainMenuOverlayState()
    {
        _gameplayManager.OverlayState.CloseMainMenuOverlayState();
    }

    private void EnterGameplaySession(GameplaySessionKind sessionKind, bool openJoinMenus, string? statusMessage = null)
    {
        _gameplayManager.Session.EnterGameplaySession(sessionKind, openJoinMenus, statusMessage);
    }

    private void ResetToMainMenuState(string? statusMessage)
    {
        _gameplayManager.Session.ResetToMainMenuState(statusMessage);
    }
}
