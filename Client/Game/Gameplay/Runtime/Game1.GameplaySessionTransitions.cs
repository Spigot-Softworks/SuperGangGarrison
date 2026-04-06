#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void ResetGameplayRuntimeState()
    {
        _gameplayResetController.ResetGameplayRuntimeState();
    }

    private void ResetGameplayTransitionEffects()
    {
        _gameplayResetController.ResetGameplayTransitionEffects();
    }

    private void CloseGameplayOverlayState()
    {
        _gameplayOverlayStateController.CloseGameplayOverlayState();
    }

    private void CloseMainMenuOverlayState()
    {
        _gameplayOverlayStateController.CloseMainMenuOverlayState();
    }

    private void EnterGameplaySession(GameplaySessionKind sessionKind, bool openJoinMenus, string? statusMessage = null)
    {
        _gameplaySessionController.EnterGameplaySession(sessionKind, openJoinMenus, statusMessage);
    }

    private void ResetToMainMenuState(string? statusMessage)
    {
        _gameplaySessionController.ResetToMainMenuState(statusMessage);
    }
}
