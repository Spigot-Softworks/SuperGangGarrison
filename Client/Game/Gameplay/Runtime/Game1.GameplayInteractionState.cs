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
        return IsLocalSpectatorPresentationActive()
            && !_consoleOpen
            && !ShouldBlockGameplayForGarrisonBuilder()
            && !_chatOpen
            && !_passwordPromptOpen
            && !_teamSelectOpen
            && !_classSelectOpen
            && !IsGameplayMenuOpen();
    }

    private bool IsLocalSpectatorPresentationActive()
    {
        return _networkClient.IsSpectator || _offlinePracticeSpectatorMode;
    }

    private bool IsWatchOnlySession()
    {
        return _networkClient.IsConnected
            && _onlineConnectionIntent == OnlineConnectionIntent.Watch;
    }

    private bool CanToggleGameplaySelectionMenus()
    {
        return !_passwordPromptOpen
            && !IsWatchOnlySession()
            && !HasOpenGameplayOverlay()
            && !ShouldBlockGameplayForGarrisonBuilder()
            && !_consoleOpen
            && !_chatOpen
            && !IsLastToDieSessionActive
            && !_world.MatchState.IsEnded
            && !IsGameplayDeathCamActive();
    }

    private bool CanOfferGameplaySelectionMenusFromInGameMenu()
    {
        return !_passwordPromptOpen
            && !IsWatchOnlySession()
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
