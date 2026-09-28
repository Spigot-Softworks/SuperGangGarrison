#nullable enable

namespace OpenGarrison.Client;

public partial class Game1
{
    public bool _gameplayModalOwnedInputThisFrame;

    public static bool ShouldOpenInGamePauseMenu(
        bool escapePressed,
        bool controllerPausePressed,
        bool canOpenInGamePauseMenu)
    {
        return canOpenInGamePauseMenu && (escapePressed || controllerPausePressed);
    }

    public bool HasGameplayModalInputOwner()
        => _consoleOpen || _chatOpen || _passwordPromptOpen || HasOpenGameplayOverlay();

    public bool CanUpdateHostedLastToDieMenuInput()
        => !_gameplayModalOwnedInputThisFrame && !HasGameplayModalInputOwner();

    public bool IsGameplayMenuOpen()
    {
        return HasOpenGameplayBlockingMenu();
    }

    public bool IsGameplayInputBlocked()
    {
        return !IsGameplayWindowInputActive()
            || IsHostedLastToDieBlockingGameplay()
            || IsGameplayMenuOpen()
            || ShouldBlockGameplayForGarrisonBuilder()
            || _consoleOpen
            || _chatOpen
            || _teamSelectOpen
            || _classSelectOpen
            || _passwordPromptOpen;
    }

    public bool IsGameplayWindowInputActive()
    {
        return IsWindowInputActive;
    }

    public bool CanOpenGameplayChat()
    {
        return !_passwordPromptOpen
            && !ShouldBlockGameplayForGarrisonBuilder()
            && !_consoleOpen
            && !_teamSelectOpen
            && !_classSelectOpen
            && !_chatOpen;
    }

    public bool CanUseGameplayChatShortcut()
    {
        return !_chatSubmitAwaitingOpenKeyRelease
            && !ShouldBlockGameplayForGarrisonBuilder()
            && !IsGameplayMenuOpen();
    }

    public bool ShouldPreserveAimWhileBlocked()
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

    public bool IsLocalSpectatorPresentationActive()
    {
        return _networkClient.IsSpectator || _offlinePracticeSpectatorMode;
    }

    public bool IsWatchOnlySession()
    {
        return _networkClient.IsConnected
            && _onlineConnectionIntent == OnlineConnectionIntent.Watch;
    }

    public bool CanToggleGameplaySelectionMenus()
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

    public bool CanOfferGameplaySelectionMenusFromInGameMenu()
    {
        return !_passwordPromptOpen
            && !IsWatchOnlySession()
            && !ShouldBlockGameplayForGarrisonBuilder()
            && !_consoleOpen
            && !_chatOpen
            && !IsAnyLastToDieSessionActive
            && !_world.MatchState.IsEnded
            && !IsGameplayDeathCamActive();
    }

    public bool CanOpenInGamePauseMenu()
    {
        return !_consoleOpen
            && !IsGameplayLoadingForMenuInput()
            && !ShouldBlockGameplayForGarrisonBuilder()
            && !_teamSelectOpen
            && !_classSelectOpen
            && !ShouldConsumeHostedLastToDieBackInput()
            && !HasOpenGameplayOverlay();
    }

    // Pausing before warmup finishes prevents the next required snapshots arriving.
    public bool IsGameplayLoadingForMenuInput()
        => IsNetworkWorldWarmupBlockingPresentation()
            || IsPracticeNavigationWarmupBlockingGameplay()
            || _loadingOverlayVisible
            || _networkClient.LastToDieState.Snapshot?.Phase == OpenGarrison.Protocol.LastToDieWirePhase.LoadingStage;
}
