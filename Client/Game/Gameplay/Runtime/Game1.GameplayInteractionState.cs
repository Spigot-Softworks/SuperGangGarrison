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
        return !IsGameplayWindowInputActive()
            || IsGameplayMenuOpen()
            || ShouldBlockGameplayForGarrisonBuilder()
            || _consoleOpen
            || _chatOpen
            || _teamSelectOpen
            || _classSelectOpen
            || _passwordPromptOpen;
    }

    private bool IsGameplayWindowInputActive()
    {
        return OperatingSystem.IsBrowser()
            ? BrowserInputBridge.IsFocused
            : IsActive;
    }

    private bool CanOpenGameplayChat()
    {
        return !_passwordPromptOpen
            && !ShouldBlockGameplayForGarrisonBuilder()
            && !_consoleOpen
            && !_teamSelectOpen
            && !_classSelectOpen
            && !_chatOpen;
    }

    private bool CanUseGameplayChatShortcut()
    {
        return !_chatSubmitAwaitingOpenKeyRelease
            && !ShouldBlockGameplayForGarrisonBuilder()
            && !IsGameplayMenuOpen();
    }

    private bool ShouldPreserveAimWhileBlocked()
    {
        return !IsGameplayWindowInputActive()
            || (_chatOpen
                && !_consoleOpen
                && !_passwordPromptOpen
                && !_teamSelectOpen
                && !_classSelectOpen
                && !IsGameplayMenuOpen());
    }

    private bool CanUseSpectatorTrackingHotkeys()
    {
        return _networkClient.IsSpectator
            && !_consoleOpen
            && !ShouldBlockGameplayForGarrisonBuilder()
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
            && !ShouldBlockGameplayForGarrisonBuilder()
            && !_consoleOpen
            && !_chatOpen
            && !IsLastToDieSessionActive
            && !_world.MatchState.IsEnded
            && !IsGameplayDeathCamActive();
    }

    private bool CanOpenInGamePauseMenu()
    {
        return !_consoleOpen
            && !ShouldBlockGameplayForGarrisonBuilder()
            && !_teamSelectOpen
            && !_classSelectOpen
            && !HasOpenGameplayOverlay();
    }
}
