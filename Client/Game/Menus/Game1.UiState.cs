#nullable enable

using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private enum MainMenuOverlayKind
    {
        None,
        HostSetup,
        ClientPowers,
        PracticeSetup,
        Credits,
        LobbyBrowser,
        ManualConnect,
        ControlsMenu,
        LastToDieMenu,
        PluginOptionsMenu,
        OptionsMenu,
    }

    private enum GameplayOverlayKind
    {
        None,
        LastToDieFailure,
        LastToDieDeathFocusPresentation,
        LastToDieStageClear,
        LastToDieSurvivorMenu,
        LastToDiePerkMenu,
        QuitPrompt,
        ControlsMenu,
        ClientPowers,
        PracticeSetup,
        PluginOptionsMenu,
        OptionsMenu,
        InGameMenu,
        LoadoutMenu,
    }

    private bool HasOpenGameplayOverlay()
    {
        return GetActiveGameplayOverlay() != GameplayOverlayKind.None;
    }

    private bool HasOpenGameplayBlockingMenu()
    {
        return HasOpenGameplayOverlay() || ShouldBlockGameplayForNavEditor();
    }

    private bool IsGameplayDeathCamActive()
    {
        return _killCamEnabled && _world.LocalDeathCam is not null;
    }

    private bool IsGameplaySelectionOverlayVisible()
    {
        return _teamSelectOpen
            || _teamSelectAlpha > 0.02f
            || _classSelectOpen
            || _classSelectAlpha > 0.02f
            || _gameplayLoadoutMenuOpen;
    }

    private bool CanShowGameplayScoreboard()
    {
        return !_mainMenuOpen
            && !HasOpenGameplayOverlay()
            && !_consoleOpen
            && !_teamSelectOpen
            && !_classSelectOpen
            && !_gameplayLoadoutMenuOpen;
    }

    private bool ShouldCloseBubbleMenuForGameplayState()
    {
        return _mainMenuOpen
            || HasOpenGameplayOverlay()
            || _consoleOpen
            || _chatOpen
            || _teamSelectOpen
            || _classSelectOpen
            || _gameplayLoadoutMenuOpen
            || _passwordPromptOpen
            || _world.LocalPlayerAwaitingJoin
            || !_world.LocalPlayer.IsAlive
            || _world.MatchState.IsEnded
            || IsGameplayDeathCamActive();
    }

    private bool CanDrawGameplayBubbleHud()
    {
        return !_networkClient.IsSpectator
            && _world.LocalPlayer.IsAlive
            && !IsGameplayDeathCamActive();
    }

    private bool ShouldCloseBuildMenuForGameplayState()
    {
        return _mainMenuOpen
            || HasOpenGameplayOverlay()
            || _consoleOpen
            || _chatOpen
            || _teamSelectOpen
            || _classSelectOpen
            || _gameplayLoadoutMenuOpen
            || _passwordPromptOpen
            || _networkClient.IsSpectator
            || _world.LocalPlayerAwaitingJoin
            || !_world.LocalPlayer.IsAlive
            || _world.LocalPlayer.ClassId != PlayerClass.Engineer
            || _world.IsPlayerHumiliated(_world.LocalPlayer);
    }

    private bool CanDrawGameplayBuildHud()
    {
        return !IsGameplayDeathCamActive();
    }

    private bool ShouldSuppressGameplayHudForActiveOverlay()
    {
        return GetActiveGameplayOverlay() switch
        {
            GameplayOverlayKind.None => false,
            GameplayOverlayKind.LastToDieStageClear => false,
            GameplayOverlayKind.QuitPrompt => false,
            _ => true,
        };
    }

    private bool CanDrawGameplayCrosshair()
    {
        return !_teamSelectOpen
            && _teamSelectAlpha <= 0.02f
            && !_gameplayLoadoutMenuOpen
            && !_networkClient.IsSpectator
            && _world.LocalPlayer.IsAlive
            && !IsGameplayDeathCamActive()
            && !ShouldBlockGameplayForNavEditor()
            && !_consoleOpen
            && !ShouldSuppressGameplayHudForActiveOverlay();
    }

    private bool ShouldShowGameplayMouseCursor()
    {
        return _passwordPromptOpen
            || IsGameplaySelectionOverlayVisible()
            || HasOpenGameplayBlockingMenu();
    }
}
