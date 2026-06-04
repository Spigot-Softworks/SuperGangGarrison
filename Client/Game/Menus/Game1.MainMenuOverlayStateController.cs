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
            _game.CloseAllHostSetupMapPreviews();
            _game._hostSetupState.NavigateToMainScreen();
            if (clearStatus)
            {
                _game._menuStatusMessage = string.Empty;
            }
        }

        public void OpenManualConnectMenu()
        {
            PrepareForExclusiveMainMenuOverlayOpen();
            _game._manualConnectOpen = true;
            _game._manualConnectControllerIndex = 0;
            _game._connectionFlowController.SetManualConnectEditingField(editHost: true);
            _game._menuStatusMessage = string.Empty;
        }

        public void OpenCreditsMenu()
        {
            PrepareForExclusiveMainMenuOverlayOpen();
            _game._creditsOpen = true;
            _game._creditsScrollInitialized = false;
        }

        public void OpenFriendsMenu()
        {
            PrepareForExclusiveMainMenuOverlayOpen();
            _game._friendsMenuOpen = true;
            _game._mainMenuHoverIndex = -1;
            _game._mainMenuBottomBarHover = false;
            _game._friendsMenuHoverIndex = -1;
            _game._friendsMenuSelectedIndex = _game._friendList.Friends.Count > 0 ? 0 : -1;
            _game._friendsMenuTab = FriendsMenuTab.Friends;
            _game.CloseFriendsContextMenu();
            _game.ClosePlayerCardOverlay();
            _game._friendNicknameInputBuffer = _game.GetFriendNicknameInputDefault();
            _game.InitializeFriendNicknameCursor();
            var needsNickname = string.IsNullOrWhiteSpace(_game._clientIdentity.DisplayName);
            _game._editingFriendNickname = needsNickname;
            _game._friendsMenuAddingFriend = false;
            _game._editingFriendCode = false;
            _game.InitializeFriendCodeCursor();
            if (needsNickname)
            {
                _game.SetPersistedMenuStatusMessage("Choose a nickname for friends.");
            }
            else
            {
                _game._menuStatusMessage = string.Empty;
            }
            _game.RefreshFriendPresence();
        }

        public void CloseFriendsMenu(bool clearStatus = false)
        {
            _game._friendsMenuOpen = false;
            _game._friendsMenuHoverIndex = -1;
            _game.CloseFriendsContextMenu();
            _game.ClosePlayerCardOverlay();
            _game._editingFriendCode = false;
            _game._friendsMenuAddingFriend = false;
            _game._editingFriendNickname = false;
            if (clearStatus)
            {
                _game._menuStatusMessage = string.Empty;
            }
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
            _game._lastToDieMenuPage = LastToDieMenuPage.Root;
            _game._lastToDieMenuHoverIndex = 0;
            _game._menuStatusMessage = statusMessage ?? string.Empty;
        }

        public void CloseLastToDieMenu(bool clearStatus = false)
        {
            _game._lastToDieMenuOpen = false;
            _game._lastToDieMenuPage = LastToDieMenuPage.Root;
            _game._lastToDieMenuHoverIndex = -1;
            if (clearStatus)
            {
                _game._menuStatusMessage = string.Empty;
            }
        }

        public void OpenJumpMenu(string? statusMessage = null)
        {
            PrepareForExclusiveMainMenuOverlayOpen();
            _game.PrepareJumpMenuMapEntries();
            _game._jumpMenuOpen = true;
            _game._jumpMenuHoverIndex = 0;
            _game._menuStatusMessage = statusMessage ?? string.Empty;
        }

        public void CloseJumpMenu(bool clearStatus = false)
        {
            _game._jumpMenuOpen = false;
            _game._jumpMenuHoverIndex = -1;
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
            CloseFriendsMenu(clearStatus: false);
            CloseJumpMenu(clearStatus: false);
            _game._connectionFlowController.DisableManualConnectEditing();
        }

        private void PrepareForExclusiveMainMenuOverlayOpen()
        {
            CloseMainMenuTransientOverlays();
            CloseLastToDieMenu(clearStatus: false);
            CloseJumpMenu(clearStatus: false);
            _game._practiceSetupOpen = false;
            _game._clientPowersOpen = false;
            _game._clientPowersOpenedFromGameplay = false;
            _game._optionsMenuOpen = false;
            _game._pluginOptionsMenuOpen = false;
            _game._controlsMenuOpen = false;
            _game._pendingControlsBinding = null;
            _game._pendingControllerControlsBinding = null;
            _game.DismissCustomBubbleEditor();
            _game._editingPlayerName = false;
        }
    }
}
