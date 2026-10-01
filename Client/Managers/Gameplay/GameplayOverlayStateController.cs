#nullable enable

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class GameplayOverlayStateController
    {
        private readonly IGameplayContext _context;

        public GameplayOverlayStateController(IGameplayContext context)
        {
            _context = context;
        }

        public TeamClassSelectionState TeamClassSelection { get; } = new();

        public void CloseGameplayOverlayState()
        {
            CloseGameplayMenuStack();
            CloseJoinAndCommunicationOverlays();
            CloseModalGameplayPrompts();
        }

        public void CloseMainMenuOverlayState()
        {
            _context.Menus.MainMenuOverlayState.CloseMainMenuTransientOverlays();
        }

        private void CloseGameplayMenuStack()
        {
            _context._practiceSetupOpen = false;
            _context._lastToDieMenuOpen = false;
            _context._jumpMenuOpen = false;
            _context._lastToDiePerkMenuOpen = false;
            _context._clientPowersOpen = false;
            _context._clientPowersOpenedFromGameplay = false;
            _context._optionsMenuOpen = false;
            _context._optionsMenuOpenedFromGameplay = false;
            _context._pluginOptionsMenuOpen = false;
            _context._pluginOptionsMenuOpenedFromGameplay = false;
            _context._controlsMenuOpen = false;
            _context._controlsMenuOpenedFromGameplay = false;
            _context.DismissCustomBubbleEditor();
            _context._inGameMenuOpen = false;
            _context._gameplayLoadoutMenuOpen = false;
            _context._gameplayLoadoutMenuAwaitingEscapeRelease = false;
            _context._gameplayLoadoutMenuHoverIndex = -1;
            _context._quitPromptOpen = false;
            _context._quitPromptHoverIndex = -1;
            _context._pendingControlsBinding = null;
            _context._pendingControllerControlsBinding = null;
        }

        private void CloseJoinAndCommunicationOverlays()
        {
            TeamClassSelection.TeamSelectOpen = false;
            TeamClassSelection.ClassSelectOpen = false;
            TeamClassSelection.PendingClassSelectTeam = null;
            _context._consoleOpen = false;
            _context._scoreboardOpen = false;
            _context.ResetChatInputState();
            _context._bubbleMenuKind = BubbleMenuKind.None;
            _context._bubbleMenuClosing = false;
        }

        private void CloseModalGameplayPrompts()
        {
            _context.Session.Connection.CloseNetworkPasswordPrompt();
        }
}
