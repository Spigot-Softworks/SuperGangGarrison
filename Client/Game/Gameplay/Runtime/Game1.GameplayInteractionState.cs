#nullable enable

namespace OpenGarrison.Client;

public partial class Game1
{

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
        => !_gameplayManager.InputUpdate.GameplayModalOwnedInputThisFrame && !HasGameplayModalInputOwner();

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
            || _teamClassSelectionState.TeamSelectOpen
            || _teamClassSelectionState.ClassSelectOpen
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
            && !_teamClassSelectionState.TeamSelectOpen
            && !_teamClassSelectionState.ClassSelectOpen
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
                && !_teamClassSelectionState.TeamSelectOpen
                && !_teamClassSelectionState.ClassSelectOpen
                && !IsGameplayMenuOpen());
    }

    private bool CanUseSpectatorTrackingHotkeys()
    {
        return IsLocalSpectatorPresentationActive()
            && !_consoleOpen
            && !ShouldBlockGameplayForGarrisonBuilder()
            && !_chatOpen
            && !_passwordPromptOpen
            && !_teamClassSelectionState.TeamSelectOpen
            && !_teamClassSelectionState.ClassSelectOpen
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
            && !_teamClassSelectionState.TeamSelectOpen
            && !_teamClassSelectionState.ClassSelectOpen
            && !ShouldConsumeHostedLastToDieBackInput()
            && !HasOpenGameplayOverlay();
    }

    // Pausing before warmup finishes prevents the next required snapshots arriving.
    public bool IsGameplayLoadingForMenuInput()
        => IsNetworkWorldWarmupBlockingPresentation()
            || IsPracticeNavigationWarmupBlockingGameplay()
            || _loadingOverlayState.Visible
            || _networkClient.LastToDieState.Snapshot?.Phase == OpenGarrison.Protocol.LastToDieWirePhase.LoadingStage;
}
