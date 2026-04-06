#nullable enable

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool IsGameplayMenuOpen()
    {
        return HasOpenGameplayBlockingMenu();
    }

    private bool IsGameplayInputBlocked()
    {
        return !IsActive
            || IsGameplayMenuOpen()
            || _consoleOpen
            || _chatOpen
            || _teamSelectOpen
            || _classSelectOpen
            || _passwordPromptOpen;
    }

    private bool CanOpenGameplayChat()
    {
        return !_passwordPromptOpen
            && !_consoleOpen
            && !_teamSelectOpen
            && !_classSelectOpen
            && !_chatOpen;
    }

    private bool CanUseGameplayChatShortcut()
    {
        return !_chatSubmitAwaitingOpenKeyRelease
            && !IsGameplayMenuOpen();
    }

    private bool ShouldPreserveAimWhileBlocked()
    {
        return _chatOpen
            && !_consoleOpen
            && !_passwordPromptOpen
            && !_teamSelectOpen
            && !_classSelectOpen
            && !IsGameplayMenuOpen();
    }

    private bool CanUseSpectatorTrackingHotkeys()
    {
        return _networkClient.IsSpectator
            && !_consoleOpen
            && !_chatOpen
            && !_passwordPromptOpen
            && !_teamSelectOpen
            && !_classSelectOpen
            && !IsGameplayMenuOpen();
    }

    private bool CanToggleGameplaySelectionMenus()
    {
        return !_passwordPromptOpen
            && !HasOpenGameplayOverlay()
            && !_consoleOpen
            && !_chatOpen
            && !IsLastToDieSessionActive
            && !_world.MatchState.IsEnded
            && !IsGameplayDeathCamActive();
    }

    private bool CanOpenInGamePauseMenu()
    {
        return !_consoleOpen
            && !_teamSelectOpen
            && !_classSelectOpen
            && !HasOpenGameplayOverlay();
    }
}
