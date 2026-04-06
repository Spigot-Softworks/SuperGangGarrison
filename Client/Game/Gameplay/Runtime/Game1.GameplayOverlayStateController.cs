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
            _game._quitPromptOpen = false;
            _game._quitPromptHoverIndex = -1;
            _game._pendingControlsBinding = null;
            _game._teamSelectOpen = false;
            _game._classSelectOpen = false;
            _game._pendingClassSelectTeam = null;
            _game._consoleOpen = false;
            _game._scoreboardOpen = false;
            _game.ResetChatInputState();
            _game._bubbleMenuKind = BubbleMenuKind.None;
            _game._bubbleMenuClosing = false;
            _game._passwordPromptOpen = false;
            _game._passwordEditBuffer = string.Empty;
            _game._passwordPromptMessage = string.Empty;
        }

        public void CloseMainMenuOverlayState()
        {
            _game.CloseLobbyBrowser(clearStatus: false);
            _game._manualConnectOpen = false;
            _game._hostSetupOpen = false;
            _game._hostSetupEditField = HostSetupEditField.None;
            _game._creditsOpen = false;
            _game._editingConnectHost = false;
            _game._editingConnectPort = false;
        }
    }
}
