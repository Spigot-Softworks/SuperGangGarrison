#nullable enable

using System.Collections.Generic;
using System.Globalization;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    public enum OnlineConnectionIntent
    {
        Join,
        Watch,
    }

    public enum SpectatorCameraMode
    {
        Normal = 0,
        RedIntel = 1,
        BlueIntel = 2,
        Auto = 3,
    }

    public readonly UiShellState _uiShellState = new();
    public readonly GameplaySessionState _gameplaySessionState = new();

    private TeamClassSelectionState _teamClassSelectionState => _gameplayManager.OverlayState.TeamClassSelection;
    private GameplaySessionTransitionState _sessionTransitions => _gameplayManager.SessionState.SessionTransitions;
    private PendingHostedConnectState _pendingHostedConnectState => _gameplayManager.SessionState.PendingHostedConnect;
    private LoadingOverlayState _loadingOverlayState => _gameplayManager.Frame.LoadingOverlay;

    public int? _localPlayerSnapshotEntityId
    {
        get => _gameplaySessionState.LocalPlayerSnapshotEntityId;
        set => _gameplaySessionState.LocalPlayerSnapshotEntityId = value;
    }

    public int? _spectatorTrackedPlayerId
    {
        get => _gameplaySessionState.SpectatorTrackedPlayerId;
        set => _gameplaySessionState.SpectatorTrackedPlayerId = value;
    }

    public bool _spectatorTrackingEnabled
    {
        get => _gameplaySessionState.SpectatorTrackingEnabled;
        set => _gameplaySessionState.SpectatorTrackingEnabled = value;
    }

    public bool _offlinePracticeSpectatorMode
    {
        get => _gameplaySessionState.OfflinePracticeSpectatorMode;
        set => _gameplaySessionState.OfflinePracticeSpectatorMode = value;
    }

    public GameplaySessionKind _gameplaySessionKind
    {
        get => _gameplaySessionState.GameplaySessionKind;
        set => _gameplaySessionState.GameplaySessionKind = value;
    }

    public ExperimentalGameplaySettings _practiceExperimentalGameplaySettings
    {
        get => _gameplaySessionState.PracticeExperimentalGameplaySettings;
        set => _gameplaySessionState.PracticeExperimentalGameplaySettings = value;
    }

    public bool _practiceStickyGibBloodEnabled
    {
        get => _gameplaySessionState.PracticeStickyGibBloodEnabled;
        set => _gameplaySessionState.PracticeStickyGibBloodEnabled = value;
    }

    public string _autoBalanceNoticeText
    {
        get => _gameplaySessionState.AutoBalanceNoticeText;
        set => _gameplaySessionState.AutoBalanceNoticeText = value;
    }

    public int _autoBalanceNoticeTicks
    {
        get => _gameplaySessionState.AutoBalanceNoticeTicks;
        set => _gameplaySessionState.AutoBalanceNoticeTicks = value;
    }

    public int _gameplayLoadoutPortraitAnimationHoverIndex
    {
        get => _uiShellState.GameplayLoadoutPortraitAnimationHoverIndex;
        set => _uiShellState.GameplayLoadoutPortraitAnimationHoverIndex = value;
    }

    public PlayerTeam? _gameplayLoadoutPortraitAnimationTeam
    {
        get => _uiShellState.GameplayLoadoutPortraitAnimationTeam;
        set => _uiShellState.GameplayLoadoutPortraitAnimationTeam = value;
    }

    public float _gameplayLoadoutPortraitAnimationFrame
    {
        get => _uiShellState.GameplayLoadoutPortraitAnimationFrame;
        set => _uiShellState.GameplayLoadoutPortraitAnimationFrame = value;
    }

    public bool _scoreboardOpen
    {
        get => _uiShellState.ScoreboardOpen;
        set => _uiShellState.ScoreboardOpen = value;
    }

    public float _scoreboardAlpha
    {
        get => _uiShellState.ScoreboardAlpha;
        set => _uiShellState.ScoreboardAlpha = value;
    }

    public bool _chatOpen
    {
        get => _uiShellState.ChatOpen;
        set => _uiShellState.ChatOpen = value;
    }

    public bool _chatTeamOnly
    {
        get => _uiShellState.ChatTeamOnly;
        set => _uiShellState.ChatTeamOnly = value;
    }

    public bool _chatSubmitAwaitingOpenKeyRelease
    {
        get => _uiShellState.ChatSubmitAwaitingOpenKeyRelease;
        set => _uiShellState.ChatSubmitAwaitingOpenKeyRelease = value;
    }

    public int _chatScrollOffset
    {
        get => _uiShellState.ChatScrollOffset;
        set => _uiShellState.ChatScrollOffset = value;
    }

    public BubbleMenuKind _bubbleMenuKind
    {
        get => _uiShellState.BubbleMenuKind;
        set => _uiShellState.BubbleMenuKind = value;
    }

    public float _bubbleMenuAlpha
    {
        get => _uiShellState.BubbleMenuAlpha;
        set => _uiShellState.BubbleMenuAlpha = value;
    }

    public float _bubbleMenuX
    {
        get => _uiShellState.BubbleMenuX;
        set => _uiShellState.BubbleMenuX = value;
    }

    public bool _bubbleMenuClosing
    {
        get => _uiShellState.BubbleMenuClosing;
        set => _uiShellState.BubbleMenuClosing = value;
    }

    public int _bubbleMenuXPageIndex
    {
        get => _uiShellState.BubbleMenuXPageIndex;
        set => _uiShellState.BubbleMenuXPageIndex = value;
    }

    public bool _bubbleMenuSessionHadInteraction
    {
        get => _uiShellState.BubbleMenuSessionHadInteraction;
        set => _uiShellState.BubbleMenuSessionHadInteraction = value;
    }

    public int? _bubbleMenuPendingFrame
    {
        get => _uiShellState.BubbleMenuPendingFrame;
        set => _uiShellState.BubbleMenuPendingFrame = value;
    }

    public int _recentBubbleFrameZ
    {
        get => _uiShellState.RecentBubbleFrameZ;
        set => _uiShellState.RecentBubbleFrameZ = value;
    }

    public int _recentBubbleFrameX
    {
        get => _uiShellState.RecentBubbleFrameX;
        set => _uiShellState.RecentBubbleFrameX = value;
    }

    public int _recentBubbleFrameC
    {
        get => _uiShellState.RecentBubbleFrameC;
        set => _uiShellState.RecentBubbleFrameC = value;
    }

    public int _recentBubbleFrameCustom
    {
        get => _uiShellState.RecentBubbleFrameCustom;
        set => _uiShellState.RecentBubbleFrameCustom = value;
    }

    public bool _buildMenuOpen
    {
        get => _uiShellState.BuildMenuOpen;
        set => _uiShellState.BuildMenuOpen = value;
    }

    public bool _buildMenuClosing
    {
        get => _uiShellState.BuildMenuClosing;
        set => _uiShellState.BuildMenuClosing = value;
    }

    public float _buildMenuAlpha
    {
        get => _uiShellState.BuildMenuAlpha;
        set => _uiShellState.BuildMenuAlpha = value;
    }

    public float _buildMenuX
    {
        get => _uiShellState.BuildMenuX;
        set => _uiShellState.BuildMenuX = value;
    }

    public bool _startupSplashOpen
    {
        get => _uiShellState.StartupSplashOpen;
        set => _uiShellState.StartupSplashOpen = value;
    }

    public int _startupSplashTicks
    {
        get => _uiShellState.StartupSplashTicks;
        set => _uiShellState.StartupSplashTicks = value;
    }

    public float _startupSplashFrame
    {
        get => _uiShellState.StartupSplashFrame;
        set => _uiShellState.StartupSplashFrame = value;
    }

    public bool _mainMenuOpen
    {
        get => _uiShellState.MainMenuOpen;
        set => _uiShellState.MainMenuOpen = value;
    }

    public bool _mainMenuChromeHidden
    {
        get => _uiShellState.MainMenuChromeHidden;
        set => _uiShellState.MainMenuChromeHidden = value;
    }

    public bool _optionsMenuOpen
    {
        get => _uiShellState.OptionsMenuOpen;
        set => _uiShellState.OptionsMenuOpen = value;
    }

    public bool _optionsMenuOpenedFromGameplay
    {
        get => _uiShellState.OptionsMenuOpenedFromGameplay;
        set => _uiShellState.OptionsMenuOpenedFromGameplay = value;
    }

    public bool _pluginOptionsMenuOpen
    {
        get => _uiShellState.PluginOptionsMenuOpen;
        set => _uiShellState.PluginOptionsMenuOpen = value;
    }

    public bool _pluginOptionsMenuOpenedFromGameplay
    {
        get => _uiShellState.PluginOptionsMenuOpenedFromGameplay;
        set => _uiShellState.PluginOptionsMenuOpenedFromGameplay = value;
    }

    public string? _selectedPluginOptionsPluginId
    {
        get => _uiShellState.SelectedPluginOptionsPluginId;
        set => _uiShellState.SelectedPluginOptionsPluginId = value;
    }

    public bool _lobbyBrowserOpen
    {
        get => _uiShellState.LobbyBrowserOpen;
        set => _uiShellState.LobbyBrowserOpen = value;
    }

    public bool _manualConnectOpen
    {
        get => _uiShellState.ManualConnectOpen;
        set => _uiShellState.ManualConnectOpen = value;
    }

    public bool _hostSetupOpen
    {
        get => _uiShellState.HostSetupOpen;
        set => _uiShellState.HostSetupOpen = value;
    }

    public bool _practiceSetupOpen
    {
        get => _uiShellState.PracticeSetupOpen;
        set => _uiShellState.PracticeSetupOpen = value;
    }

    public bool _clientPowersOpen
    {
        get => _uiShellState.ClientPowersOpen;
        set => _uiShellState.ClientPowersOpen = value;
    }

    public bool _clientPowersOpenedFromGameplay
    {
        get => _uiShellState.ClientPowersOpenedFromGameplay;
        set => _uiShellState.ClientPowersOpenedFromGameplay = value;
    }

    public bool _creditsOpen
    {
        get => _uiShellState.CreditsOpen;
        set => _uiShellState.CreditsOpen = value;
    }

    public SpectatorCameraMode _spectatorCameraMode
    {
        get => _gameplaySessionState.SpectatorCameraMode;
        set => _gameplaySessionState.SpectatorCameraMode = value;
    }

    public OnlineConnectionIntent _onlineConnectionIntent
    {
        get => _gameplaySessionState.OnlineConnectionIntent;
        set => _gameplaySessionState.OnlineConnectionIntent = value;
    }

    public bool _friendsMenuOpen
    {
        get => _uiShellState.FriendsMenuOpen;
        set => _uiShellState.FriendsMenuOpen = value;
    }

    public int _friendsMenuHoverIndex
    {
        get => _uiShellState.FriendsMenuHoverIndex;
        set => _uiShellState.FriendsMenuHoverIndex = value;
    }

    public int _friendsMenuSelectedIndex
    {
        get => _uiShellState.FriendsMenuSelectedIndex;
        set => _uiShellState.FriendsMenuSelectedIndex = value;
    }

    public FriendsMenuTab _friendsMenuTab
    {
        get => _uiShellState.FriendsMenuTab;
        set => _uiShellState.FriendsMenuTab = value;
    }

    public bool _friendsContextMenuOpen
    {
        get => _uiShellState.FriendsContextMenuOpen;
        set => _uiShellState.FriendsContextMenuOpen = value;
    }

    public int _friendsContextMenuTargetIndex
    {
        get => _uiShellState.FriendsContextMenuTargetIndex;
        set => _uiShellState.FriendsContextMenuTargetIndex = value;
    }

    public int _friendsContextMenuX
    {
        get => _uiShellState.FriendsContextMenuX;
        set => _uiShellState.FriendsContextMenuX = value;
    }

    public int _friendsContextMenuY
    {
        get => _uiShellState.FriendsContextMenuY;
        set => _uiShellState.FriendsContextMenuY = value;
    }

    public bool _playerCardOwnOpen
    {
        get => _uiShellState.PlayerCardOwnOpen;
        set => _uiShellState.PlayerCardOwnOpen = value;
    }

    public bool _playerCardEditorOpen
    {
        get => _uiShellState.PlayerCardEditorOpen;
        set => _uiShellState.PlayerCardEditorOpen = value;
    }

    public bool _playerCardDraggingPortrait
    {
        get => _uiShellState.PlayerCardDraggingPortrait;
        set => _uiShellState.PlayerCardDraggingPortrait = value;
    }

    private int _playerCardActiveColorIndex
    {
        get => _uiShellState.PlayerCardActiveColorIndex;
        set => _uiShellState.PlayerCardActiveColorIndex = value;
    }

    public bool _editingFriendCode
    {
        get => _uiShellState.EditingFriendCode;
        set => _uiShellState.EditingFriendCode = value;
    }

    public bool _friendsMenuAddingFriend
    {
        get => _uiShellState.FriendsMenuAddingFriend;
        set => _uiShellState.FriendsMenuAddingFriend = value;
    }

    public bool _editingFriendNickname
    {
        get => _uiShellState.EditingFriendNickname;
        set => _uiShellState.EditingFriendNickname = value;
    }

    public bool _editingFriendMessage
    {
        get => _uiShellState.EditingFriendMessage;
        set => _uiShellState.EditingFriendMessage = value;
    }

    private float _creditsScrollY
    {
        get => _uiShellState.CreditsScrollY;
        set => _uiShellState.CreditsScrollY = value;
    }

    public bool _inGameMenuOpen
    {
        get => _uiShellState.InGameMenuOpen;
        set => _uiShellState.InGameMenuOpen = value;
    }

    public bool _gameplayLoadoutMenuOpen
    {
        get => _uiShellState.GameplayLoadoutMenuOpen;
        set => _uiShellState.GameplayLoadoutMenuOpen = value;
    }

    public bool _gameplayLoadoutMenuAwaitingEscapeRelease
    {
        get => _uiShellState.GameplayLoadoutMenuAwaitingEscapeRelease;
        set => _uiShellState.GameplayLoadoutMenuAwaitingEscapeRelease = value;
    }

    private PlayerClass _gameplayLoadoutMenuViewedClass
    {
        get => _uiShellState.GameplayLoadoutMenuViewedClass;
        set => _uiShellState.GameplayLoadoutMenuViewedClass = value;
    }

    private Dictionary<PlayerClass, string> _gameplayLoadoutMenuViewedLoadoutIds => _uiShellState.GameplayLoadoutMenuViewedLoadoutIds;

    public bool _quitPromptOpen
    {
        get => _uiShellState.QuitPromptOpen;
        set => _uiShellState.QuitPromptOpen = value;
    }

    public int _quitPromptHoverIndex
    {
        get => _uiShellState.QuitPromptHoverIndex;
        set => _uiShellState.QuitPromptHoverIndex = value;
    }

    public bool _controlsMenuOpen
    {
        get => _uiShellState.ControlsMenuOpen;
        set => _uiShellState.ControlsMenuOpen = value;
    }

    public bool _controlsMenuOpenedFromGameplay
    {
        get => _uiShellState.ControlsMenuOpenedFromGameplay;
        set => _uiShellState.ControlsMenuOpenedFromGameplay = value;
    }

    public bool _editingPlayerName
    {
        get => _uiShellState.EditingPlayerName;
        set => _uiShellState.EditingPlayerName = value;
    }

    public bool _namePromptOpen
    {
        get => _uiShellState.NamePromptOpen;
        set => _uiShellState.NamePromptOpen = value;
    }

    public bool _namePromptPresented
    {
        get => _uiShellState.NamePromptPresented;
        set => _uiShellState.NamePromptPresented = value;
    }

    public bool _editingConnectHost
    {
        get => _uiShellState.EditingConnectHost;
        set => _uiShellState.EditingConnectHost = value;
    }

    public bool _editingConnectPort
    {
        get => _uiShellState.EditingConnectPort;
        set => _uiShellState.EditingConnectPort = value;
    }

    public bool _passwordPromptOpen
    {
        get => _uiShellState.PasswordPromptOpen;
        set => _uiShellState.PasswordPromptOpen = value;
    }

    public string _passwordPromptMessage
    {
        get => _uiShellState.PasswordPromptMessage;
        set => _uiShellState.PasswordPromptMessage = value;
    }

    public MainMenuPage _mainMenuPage
    {
        get => _uiShellState.MainMenuPage;
        set => _uiShellState.MainMenuPage = value;
    }

    public int _mainMenuHoverIndex
    {
        get => _uiShellState.MainMenuHoverIndex;
        set => _uiShellState.MainMenuHoverIndex = value;
    }

    public bool _mainMenuBottomBarHover
    {
        get => _uiShellState.MainMenuBottomBarHover;
        set => _uiShellState.MainMenuBottomBarHover = value;
    }

    public int _optionsPageIndex
    {
        get => _uiShellState.OptionsPageIndex;
        set => _uiShellState.OptionsPageIndex = value;
    }

    public int _pluginOptionsHoverIndex
    {
        get => _uiShellState.PluginOptionsHoverIndex;
        set => _uiShellState.PluginOptionsHoverIndex = value;
    }

    public int _pluginOptionsScrollOffset
    {
        get => _uiShellState.PluginOptionsScrollOffset;
        set => _uiShellState.PluginOptionsScrollOffset = value;
    }

    public ClientPluginKeyOptionItem? _pendingPluginOptionsKeyItem
    {
        get => _uiShellState.PendingPluginOptionsKeyItem;
        set => _uiShellState.PendingPluginOptionsKeyItem = value;
    }

    public int _controlsHoverIndex
    {
        get => _uiShellState.ControlsHoverIndex;
        set => _uiShellState.ControlsHoverIndex = value;
    }

    public int _controlsScrollOffset
    {
        get => _uiShellState.ControlsScrollOffset;
        set => _uiShellState.ControlsScrollOffset = value;
    }

    public int _controlsPageIndex
    {
        get => _uiShellState.ControlsPageIndex;
        set => _uiShellState.ControlsPageIndex = value;
    }

    public int _lobbyBrowserHoverIndex
    {
        get => _uiShellState.LobbyBrowserHoverIndex;
        set => _uiShellState.LobbyBrowserHoverIndex = value;
    }

    public int _lobbyBrowserSelectedIndex
    {
        get => _uiShellState.LobbyBrowserSelectedIndex;
        set => _uiShellState.LobbyBrowserSelectedIndex = value;
    }

    private int _clientPowersScrollOffset
    {
        get => _uiShellState.ClientPowersScrollOffset;
        set => _uiShellState.ClientPowersScrollOffset = value;
    }

    public int _gameplayLoadoutMenuHoverIndex
    {
        get => _uiShellState.GameplayLoadoutMenuHoverIndex;
        set => _uiShellState.GameplayLoadoutMenuHoverIndex = value;
    }

    public string _playerNameEditBuffer
    {
        get => _uiShellState.PlayerNameEditBuffer;
        set => _uiShellState.PlayerNameEditBuffer = value;
    }

    public int _playerNameEditCursorIndex
    {
        get => _uiShellState.PlayerNameEditCursorIndex;
        set => _uiShellState.PlayerNameEditCursorIndex = value;
    }

    public int _playerNameEditSelectionStart
    {
        get => _uiShellState.PlayerNameEditSelectionStart;
        set => _uiShellState.PlayerNameEditSelectionStart = value;
    }

    public string _menuStatusMessage
    {
        get => _uiShellState.MenuStatusMessage;
        set => SetMenuStatusMessageInternal(value, persist: false);
    }

    public ControlsMenuBinding? _pendingControlsBinding
    {
        get => _uiShellState.PendingControlsBinding;
        set => _uiShellState.PendingControlsBinding = value;
    }

    public ControllerControlsMenuBinding? _pendingControllerControlsBinding
    {
        get => _uiShellState.PendingControllerControlsBinding;
        set => _uiShellState.PendingControllerControlsBinding = value;
    }

    public sealed class UiShellState
    {
        public int GameplayLoadoutPortraitAnimationHoverIndex = -1;
        public PlayerTeam? GameplayLoadoutPortraitAnimationTeam;
        public float GameplayLoadoutPortraitAnimationFrame;
        public bool ScoreboardOpen;
        public float ScoreboardAlpha = 0.02f;
        public bool ChatOpen;
        public bool ChatTeamOnly;
        public bool ChatSubmitAwaitingOpenKeyRelease;
        public int ChatScrollOffset;
        public BubbleMenuKind BubbleMenuKind;
        public float BubbleMenuAlpha = 0.01f;
        public float BubbleMenuX = -30f;
        public bool BubbleMenuClosing;
        public int BubbleMenuXPageIndex;
        public bool BubbleMenuSessionHadInteraction;
        public int? BubbleMenuPendingFrame;
        public int RecentBubbleFrameZ = 20;
        public int RecentBubbleFrameX = 29;
        public int RecentBubbleFrameC = 36;
        public int RecentBubbleFrameCustom = -1;
        public bool BuildMenuOpen;
        public bool BuildMenuClosing;
        public float BuildMenuAlpha = 0.01f;
        public float BuildMenuX = -37f;
        public bool StartupSplashOpen = true;
        public int StartupSplashTicks;
        public float StartupSplashFrame;
        public bool MainMenuOpen = true;
        public bool MainMenuChromeHidden;
        public bool NamePromptOpen;
        public bool NamePromptPresented;
        public bool OptionsMenuOpen;
        public bool OptionsMenuOpenedFromGameplay;
        public bool PluginOptionsMenuOpen;
        public bool PluginOptionsMenuOpenedFromGameplay;
        public string? SelectedPluginOptionsPluginId;
        public bool LobbyBrowserOpen;
        public bool ManualConnectOpen;
        public bool HostSetupOpen;
        public bool PracticeSetupOpen;
        public bool ClientPowersOpen;
        public bool ClientPowersOpenedFromGameplay;
        public bool CreditsOpen;
        public bool FriendsMenuOpen;
        public int FriendsMenuHoverIndex = -1;
        public int FriendsMenuSelectedIndex = -1;
        public FriendsMenuTab FriendsMenuTab = FriendsMenuTab.Friends;
        public bool FriendsContextMenuOpen;
        public int FriendsContextMenuTargetIndex = -1;
        public int FriendsContextMenuX;
        public int FriendsContextMenuY;
        public bool PlayerCardOwnOpen;
        public bool PlayerCardEditorOpen;
        public bool PlayerCardDraggingPortrait;
        public int PlayerCardActiveColorIndex;
        public bool EditingFriendCode;
        public bool FriendsMenuAddingFriend;
        public bool EditingFriendNickname;
        public bool EditingFriendMessage;
        public float CreditsScrollY;
        public bool InGameMenuOpen;
        public bool GameplayLoadoutMenuOpen;
        public bool GameplayLoadoutMenuAwaitingEscapeRelease;
        public PlayerClass GameplayLoadoutMenuViewedClass = PlayerClass.Scout;
        public Dictionary<PlayerClass, string> GameplayLoadoutMenuViewedLoadoutIds = [];
        public bool QuitPromptOpen;
        public int QuitPromptHoverIndex = -1;
        public bool ControlsMenuOpen;
        public bool ControlsMenuOpenedFromGameplay;
        public bool EditingPlayerName;
        public bool EditingConnectHost;
        public bool EditingConnectPort;
        public bool PasswordPromptOpen;
        public string PasswordPromptMessage = string.Empty;
        public MainMenuPage MainMenuPage = MainMenuPage.Root;
        public int MainMenuHoverIndex = -1;
        public bool MainMenuBottomBarHover;
        public int OptionsPageIndex;
        public int PluginOptionsHoverIndex = -1;
        public int PluginOptionsScrollOffset;
        public ClientPluginKeyOptionItem? PendingPluginOptionsKeyItem;
        public int ControlsHoverIndex = -1;
        public int ControlsScrollOffset;
        public int ControlsPageIndex;
        public int LobbyBrowserHoverIndex = -1;
        public int LobbyBrowserSelectedIndex = -1;
        public int ClientPowersScrollOffset;
        public int GameplayLoadoutMenuHoverIndex = -1;
        public string PlayerNameEditBuffer = string.Empty;
        public int PlayerNameEditCursorIndex;
        public int PlayerNameEditSelectionStart;
        public string MenuStatusMessage = string.Empty;
        public DateTime? MenuStatusMessageClearAtUtc;
        public ControlsMenuBinding? PendingControlsBinding;
        public ControllerControlsMenuBinding? PendingControllerControlsBinding;
    }

    public sealed class GameplaySessionState
    {
        public int? LocalPlayerSnapshotEntityId;
        public int? SpectatorTrackedPlayerId;
        public bool SpectatorTrackingEnabled;
        public bool OfflinePracticeSpectatorMode;
        public SpectatorCameraMode SpectatorCameraMode = SpectatorCameraMode.Auto;
        public OnlineConnectionIntent OnlineConnectionIntent;
        public GameplaySessionKind GameplaySessionKind;
        public ExperimentalGameplaySettings PracticeExperimentalGameplaySettings = new();
        public bool PracticeStickyGibBloodEnabled;
        public string AutoBalanceNoticeText = string.Empty;
        public int AutoBalanceNoticeTicks;
    }
}
