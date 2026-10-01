#nullable enable

using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    public enum MainMenuOverlayKind
    {
        None,
        NamePrompt,
        HostSetup,
        ClientPowers,
        PracticeSetup,
        Credits,
        FriendsMenu,
        LobbyBrowser,
        ManualConnect,
        ControlsMenu,
        LastToDieMenu,
        JumpMenu,
        PluginOptionsMenu,
        OptionsMenu,
        CustomBubbleEditor,
    }

    public enum GameplayOverlayKind
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
        CustomBubbleEditor,
        SocialMenu,
        HudEditor,
        VoteMenu,
        InGameMenu,
        LoadoutMenu,
        DebugMenu,
    }

    public bool HasOpenGameplayOverlay()
    {
        return GetActiveGameplayOverlay() != GameplayOverlayKind.None;
    }

    private bool HasOpenGameplayBlockingMenu()
    {
        return HasOpenGameplayOverlay() || ShouldBlockGameplayForGarrisonBuilder();
    }

    private bool IsGameplayDeathCamActive()
    {
        return !IsHostedLastToDieActive()
            && _gameplayManager.RuntimeSettings.KillCamEnabled
            && _world.LocalDeathCam is not null;
    }

    private bool IsGameplaySelectionOverlayVisible()
    {
        return _teamClassSelectionState.TeamSelectOpen
            || _teamClassSelectionState.TeamSelectAlpha > 0.02f
            || _teamClassSelectionState.ClassSelectOpen
            || _teamClassSelectionState.ClassSelectAlpha > 0.02f
            || _gameplayLoadoutMenuOpen;
    }

    private bool CanShowGameplayScoreboard()
    {
        return !_mainMenuOpen
            && !HasOpenGameplayOverlay()
            && !_consoleOpen
            && !_teamClassSelectionState.TeamSelectOpen
            && !_teamClassSelectionState.ClassSelectOpen
            && !_gameplayLoadoutMenuOpen;
    }

    private bool ShouldCloseBubbleMenuForGameplayState()
    {
        return _mainMenuOpen
            || HasOpenGameplayOverlay()
            || _consoleOpen
            || _chatOpen
            || _teamClassSelectionState.TeamSelectOpen
            || _teamClassSelectionState.ClassSelectOpen
            || _gameplayLoadoutMenuOpen
            || _passwordPromptOpen
            || _world.LocalPlayerAwaitingJoin
            || !_world.LocalPlayer.IsAlive
            || IsGameplayDeathCamActive();
    }

    private bool CanDrawGameplayBubbleHud()
    {
        return !IsLocalSpectatorPresentationActive()
            && _world.LocalPlayer.IsAlive
            && !IsGameplayDeathCamActive();
    }

    private bool ShouldCloseBuildMenuForGameplayState()
    {
        return _mainMenuOpen
            || HasOpenGameplayOverlay()
            || _consoleOpen
            || _chatOpen
            || _teamClassSelectionState.TeamSelectOpen
            || _teamClassSelectionState.ClassSelectOpen
            || _gameplayLoadoutMenuOpen
            || _passwordPromptOpen
            || IsLocalSpectatorPresentationActive()
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
        if (IsHostedLastToDieBlockingGameplay())
        {
            return true;
        }

        return GetActiveGameplayOverlay() switch
        {
            GameplayOverlayKind.None => false,
            GameplayOverlayKind.LastToDieStageClear => false,
            GameplayOverlayKind.QuitPrompt => false,
            GameplayOverlayKind.HudEditor => false,
            _ => true,
        };
    }

    private bool CanDrawGameplayCrosshair()
    {
        return !_teamClassSelectionState.TeamSelectOpen
            && _teamClassSelectionState.TeamSelectAlpha <= 0.02f
            && !_gameplayLoadoutMenuOpen
            && !IsLocalSpectatorPresentationActive()
            && _world.LocalPlayer.IsAlive
            && !IsGameplayDeathCamActive()
            && !ShouldBlockGameplayForGarrisonBuilder()
            && !_consoleOpen
            && !_hudEditorOpen
            && !ShouldSuppressGameplayHudForActiveOverlay();
    }

    public bool ShouldShowGameplayMouseCursor()
    {
        return _passwordPromptOpen
            || _scoreboardOpen
            || IsGameplaySelectionOverlayVisible()
            || HasOpenGameplayBlockingMenu()
            || IsHostedLastToDieBlockingGameplay()
            || ShouldBlockGameplayForGarrisonBuilder();
    }
}
