#nullable enable

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class MainMenuOverlayStateController
    {
        private readonly Game1 _game;

        public MainMenuOverlayStateController(Game1 game)
        {
            _game = game;
        }

        public void OpenHostSetupMenu()
        {
            PrepareForExclusiveMainMenuOverlayOpen();
            _game._hostSetupOpen = true;
            _game._menuStatusMessage = string.Empty;
            _game._hostSetupState.PrepareForOpen(_game._clientSettings.HostDefaults);
            _game.EnsureSelectedHostMapVisible();
        }

        public void CloseHostSetupMenu(bool clearStatus = false)
        {
            _game._hostSetupOpen = false;
            _game._hostSetupEditField = HostSetupEditField.None;
            if (clearStatus)
            {
                _game._menuStatusMessage = string.Empty;
            }
        }

        public void OpenManualConnectMenu()
        {
            PrepareForExclusiveMainMenuOverlayOpen();
            _game._manualConnectOpen = true;
            _game._connectionFlowController.SetManualConnectEditingField(editHost: true);
            _game._menuStatusMessage = string.Empty;
        }

        public void OpenCreditsMenu()
        {
            PrepareForExclusiveMainMenuOverlayOpen();
            _game._creditsOpen = true;
            _game._creditsScrollInitialized = false;
        }

        public void CloseCreditsMenu()
        {
            _game._creditsOpen = false;
            _game._creditsScrollInitialized = false;
        }

        public void OpenLastToDieMenu(string? statusMessage = null)
        {
            PrepareForExclusiveMainMenuOverlayOpen();
            _game._lastToDieMenuOpen = true;
            _game._lastToDieMenuHoverIndex = -1;
            _game._menuStatusMessage = statusMessage ?? string.Empty;
        }

        public void CloseLastToDieMenu(bool clearStatus = false)
        {
            _game._lastToDieMenuOpen = false;
            _game._lastToDieMenuHoverIndex = -1;
            if (clearStatus)
            {
                _game._menuStatusMessage = string.Empty;
            }
        }

        public void CloseMainMenuTransientOverlays()
        {
            _game.CloseLobbyBrowser(clearStatus: false);
            _game._manualConnectOpen = false;
            CloseHostSetupMenu(clearStatus: false);
            CloseCreditsMenu();
            _game._connectionFlowController.DisableManualConnectEditing();
        }

        private void PrepareForExclusiveMainMenuOverlayOpen()
        {
            CloseMainMenuTransientOverlays();
            CloseLastToDieMenu(clearStatus: false);
            _game._practiceSetupOpen = false;
            _game._clientPowersOpen = false;
            _game._clientPowersOpenedFromGameplay = false;
            _game._optionsMenuOpen = false;
            _game._pluginOptionsMenuOpen = false;
            _game._controlsMenuOpen = false;
            _game._editingPlayerName = false;
        }
    }
}
