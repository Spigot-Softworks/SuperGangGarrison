#nullable enable

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayOverlayStateController
    {
        private readonly Game1 _game;

        public GameplayOverlayStateController(Game1 game)
        {
            _game = game;
        }

        public void CloseGameplayOverlayState()
        {
            CloseGameplayMenuStack();
            CloseJoinAndCommunicationOverlays();
            CloseModalGameplayPrompts();
        }

        public void CloseMainMenuOverlayState()
        {
            _game._mainMenuOverlayStateController.CloseMainMenuTransientOverlays();
        }

        private void CloseGameplayMenuStack()
        {
            _game._practiceSetupOpen = false;
            _game._lastToDieMenuOpen = false;
            _game._lastToDiePerkMenuOpen = false;
            _game._clientPowersOpen = false;
            _game._clientPowersOpenedFromGameplay = false;
            _game._optionsMenuOpen = false;
            _game._optionsMenuOpenedFromGameplay = false;
            _game._pluginOptionsMenuOpen = false;
            _game._pluginOptionsMenuOpenedFromGameplay = false;
            _game._controlsMenuOpen = false;
            _game._controlsMenuOpenedFromGameplay = false;
            _game._inGameMenuOpen = false;
            _game._gameplayLoadoutMenuOpen = false;
            _game._gameplayLoadoutMenuAwaitingEscapeRelease = false;
            _game._gameplayLoadoutMenuHoverIndex = -1;
            _game._quitPromptOpen = false;
            _game._quitPromptHoverIndex = -1;
            _game._pendingControlsBinding = null;
        }

        private void CloseJoinAndCommunicationOverlays()
        {
            _game._teamSelectOpen = false;
            _game._classSelectOpen = false;
            _game._pendingClassSelectTeam = null;
            _game._consoleOpen = false;
            _game._scoreboardOpen = false;
            _game.ResetChatInputState();
            _game._bubbleMenuKind = BubbleMenuKind.None;
            _game._bubbleMenuClosing = false;
        }

        private void CloseModalGameplayPrompts()
        {
            _game._connectionFlowController.CloseNetworkPasswordPrompt();
        }
    }
}
