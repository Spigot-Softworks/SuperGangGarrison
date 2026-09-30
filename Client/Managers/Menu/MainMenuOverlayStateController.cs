#nullable enable

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class MainMenuOverlayStateController
    {
        private readonly IMenuContext _context;

        public MainMenuOverlayStateController(IMenuContext context)
        {
            _context = context;
        }

        public void OpenHostSetupMenu()
        {
            if (IsRestrictedBrowserEdition) return;
            PrepareForExclusiveMainMenuOverlayOpen();
            _context._hostSetupOpen = true;
            _context._menuStatusMessage = string.Empty;
            _context._hostSetupState.PrepareForOpen(_context._clientSettings.HostDefaults);
            _context.EnsureSelectedHostMapVisible();
        }

        public void CloseHostSetupMenu(bool clearStatus = false)
        {
            _context._hostSetupOpen = false;
            _context._hostSetupEditField = HostSetupEditField.None;
            _context.CloseAllHostSetupMapPreviews();
            _context._hostSetupState.NavigateToMainScreen();
            if (clearStatus)
            {
                _context._menuStatusMessage = string.Empty;
            }
        }

        public void OpenManualConnectMenu()
        {
            if (IsRestrictedBrowserEdition) return;
            PrepareForExclusiveMainMenuOverlayOpen();
            _context._lastToDieRoomCodeJoinOpen = false;
            _context.CancelFriendCodeJoin();
            _context._manualConnectOpen = true;
            _context._manualConnectControllerIndex = 0;
            _context.Session.Connection.SetManualConnectEditingField(editHost: true);
            _context._menuStatusMessage = string.Empty;
        }

        public void OpenCreditsMenu()
        {
            PrepareForExclusiveMainMenuOverlayOpen();
            _context._creditsOpen = true;
            _context._creditsScrollInitialized = false;
        }

        public void OpenFriendsMenu()
        {
            if (IsRestrictedBrowserEdition) return;
            PrepareForExclusiveMainMenuOverlayOpen();
            _context._friendsMenuOpen = true;
            _context._mainMenuHoverIndex = -1;
            _context._mainMenuBottomBarHover = false;
            _context._friendsMenuHoverIndex = -1;
            _context._friendsMenuSelectedIndex = _context._friendList.Friends.Count > 0 ? 0 : -1;
            _context._friendsMenuTab = FriendsMenuTab.Friends;
            _context.CloseFriendsContextMenu();
            _context.ClosePlayerCardOverlay();
            _context._friendNicknameInputBuffer = _context.GetFriendNicknameInputDefault();
            _context.InitializeFriendNicknameCursor();
            var needsNickname = string.IsNullOrWhiteSpace(_context._clientIdentity.DisplayName);
            _context._editingFriendNickname = needsNickname;
            _context._friendsMenuAddingFriend = false;
            _context._editingFriendCode = false;
            _context.InitializeFriendCodeCursor();
            if (needsNickname)
            {
                _context.SetPersistedMenuStatusMessage("Choose a nickname for friends.");
            }
            else
            {
                _context._menuStatusMessage = string.Empty;
            }
            _context.RefreshFriendPresence();
        }

        public void CloseFriendsMenu(bool clearStatus = false)
        {
            _context._friendsMenuOpen = false;
            _context._friendsMenuHoverIndex = -1;
            _context.CloseFriendsContextMenu();
            _context.ClosePlayerCardOverlay();
            _context._editingFriendCode = false;
            _context._friendsMenuAddingFriend = false;
            _context._editingFriendNickname = false;
            if (clearStatus)
            {
                _context._menuStatusMessage = string.Empty;
            }
        }

        public void CloseCreditsMenu()
        {
            _context._creditsOpen = false;
            _context._creditsScrollInitialized = false;
        }

        public void OpenLastToDieMenu(string? statusMessage = null)
        {
            PrepareForExclusiveMainMenuOverlayOpen();
            _context._lastToDieMenuOpen = true;
            _context._lastToDieMenuPage = LastToDieMenuPage.Root;
            _context._lastToDieMenuHoverIndex = 0;
            _context._menuStatusMessage = statusMessage ?? string.Empty;
        }

        public void CloseLastToDieMenu(bool clearStatus = false)
        {
            _context.CancelManagedRoomRequest();
            _context.CancelPendingHostedLastToDieRelayLaunch();
            _context._lastToDieMenuOpen = false;
            _context._lastToDieMenuPage = LastToDieMenuPage.Root;
            _context._lastToDieMenuHoverIndex = -1;
            if (clearStatus)
            {
                _context._menuStatusMessage = string.Empty;
            }
        }

        public void OpenJumpMenu(string? statusMessage = null)
        {
            if (IsRestrictedBrowserEdition) return;
            PrepareForExclusiveMainMenuOverlayOpen();
            _context.PrepareJumpMenuMapEntries();
            _context._jumpMenuOpen = true;
            _context._jumpMenuHoverIndex = 0;
            _context._menuStatusMessage = statusMessage ?? string.Empty;
        }

        public void CloseJumpMenu(bool clearStatus = false)
        {
            _context._jumpMenuOpen = false;
            _context._jumpMenuHoverIndex = -1;
            if (clearStatus)
            {
                _context._menuStatusMessage = string.Empty;
            }
        }

        public void CloseMainMenuTransientOverlays()
        {
            _context.CloseLobbyBrowser(clearStatus: false);
            _context._manualConnectOpen = false;
            _context._lastToDieRoomCodeJoinOpen = false;
            _context._namePromptOpen = false;
            _context.CancelFriendCodeJoin();
            CloseHostSetupMenu(clearStatus: false);
            CloseCreditsMenu();
            CloseFriendsMenu(clearStatus: false);
            CloseJumpMenu(clearStatus: false);
            _context.Session.Connection.DisableManualConnectEditing();
        }

        private void PrepareForExclusiveMainMenuOverlayOpen()
        {
            // Scrollbar ownership is transient UI state. Do not let a drag from the
            // previously active overlay consume the first click in the next one.
            _context.ScrollbarDrag.Clear();
            CloseMainMenuTransientOverlays();
            CloseLastToDieMenu(clearStatus: false);
            CloseJumpMenu(clearStatus: false);
            _context._practiceSetupOpen = false;
            _context._clientPowersOpen = false;
            _context._clientPowersOpenedFromGameplay = false;
            _context._optionsMenuOpen = false;
            _context._pluginOptionsMenuOpen = false;
            _context._controlsMenuOpen = false;
            _context._pendingControlsBinding = null;
            _context._pendingControllerControlsBinding = null;
            _context.DismissCustomBubbleEditor();
            _context._editingPlayerName = false;
        }
}
