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
    }

    private MainMenuOverlayKind GetActiveMainMenuOverlay()
    {
        if (_hostSetupOpen)
        {
            return MainMenuOverlayKind.HostSetup;
        }

        if (_clientPowersOpen)
        {
            return MainMenuOverlayKind.ClientPowers;
        }

        if (_practiceSetupOpen)
        {
            return MainMenuOverlayKind.PracticeSetup;
        }

        if (_creditsOpen)
        {
            return MainMenuOverlayKind.Credits;
        }

        if (_lobbyBrowserOpen)
        {
            return MainMenuOverlayKind.LobbyBrowser;
        }

        if (_manualConnectOpen)
        {
            return MainMenuOverlayKind.ManualConnect;
        }

        if (_controlsMenuOpen)
        {
            return MainMenuOverlayKind.ControlsMenu;
        }

        if (_lastToDieMenuOpen)
        {
            return MainMenuOverlayKind.LastToDieMenu;
        }

        if (_pluginOptionsMenuOpen)
        {
            return MainMenuOverlayKind.PluginOptionsMenu;
        }

        if (_optionsMenuOpen)
        {
            return MainMenuOverlayKind.OptionsMenu;
        }

        return MainMenuOverlayKind.None;
    }

    private GameplayOverlayKind GetActiveGameplayOverlay()
    {
        if (IsLastToDieFailureOverlayActive())
        {
            return GameplayOverlayKind.LastToDieFailure;
        }

        if (IsLastToDieDeathFocusPresentationActive())
        {
            return GameplayOverlayKind.LastToDieDeathFocusPresentation;
        }

        if (IsLastToDieStageClearOverlayActive())
        {
            return GameplayOverlayKind.LastToDieStageClear;
        }

        if (_lastToDieSurvivorMenuOpen)
        {
            return GameplayOverlayKind.LastToDieSurvivorMenu;
        }

        if (_lastToDiePerkMenuOpen)
        {
            return GameplayOverlayKind.LastToDiePerkMenu;
        }

        if (_quitPromptOpen)
        {
            return GameplayOverlayKind.QuitPrompt;
        }

        if (_controlsMenuOpen)
        {
            return GameplayOverlayKind.ControlsMenu;
        }

        if (_clientPowersOpen)
        {
            return GameplayOverlayKind.ClientPowers;
        }

        if (_practiceSetupOpen)
        {
            return GameplayOverlayKind.PracticeSetup;
        }

        if (_pluginOptionsMenuOpen)
        {
            return GameplayOverlayKind.PluginOptionsMenu;
        }

        if (_optionsMenuOpen)
        {
            return GameplayOverlayKind.OptionsMenu;
        }

        if (_inGameMenuOpen)
        {
            return GameplayOverlayKind.InGameMenu;
        }

        return GameplayOverlayKind.None;
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
            || _classSelectAlpha > 0.02f;
    }

    private bool CanShowGameplayScoreboard()
    {
        return !_mainMenuOpen
            && !HasOpenGameplayOverlay()
            && !_consoleOpen
            && !_teamSelectOpen
            && !_classSelectOpen;
    }

    private bool ShouldCloseBubbleMenuForGameplayState()
    {
        return _mainMenuOpen
            || HasOpenGameplayOverlay()
            || _consoleOpen
            || _chatOpen
            || _teamSelectOpen
            || _classSelectOpen
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
