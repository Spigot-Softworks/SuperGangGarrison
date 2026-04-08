#nullable enable

using System.Collections.Generic;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly UiShellState _uiShellState = new();
    private readonly GameplaySessionState _gameplaySessionState = new();

    private int? _localPlayerSnapshotEntityId
    {
        get => _gameplaySessionState.LocalPlayerSnapshotEntityId;
        set => _gameplaySessionState.LocalPlayerSnapshotEntityId = value;
    }

    private int? _spectatorTrackedPlayerId
    {
        get => _gameplaySessionState.SpectatorTrackedPlayerId;
        set => _gameplaySessionState.SpectatorTrackedPlayerId = value;
    }

    private bool _spectatorTrackingEnabled
    {
        get => _gameplaySessionState.SpectatorTrackingEnabled;
        set => _gameplaySessionState.SpectatorTrackingEnabled = value;
    }

    private string _observedGameplayLevelName
    {
        get => _gameplaySessionState.ObservedGameplayLevelName;
        set => _gameplaySessionState.ObservedGameplayLevelName = value;
    }

    private int _observedGameplayMapAreaIndex
    {
        get => _gameplaySessionState.ObservedGameplayMapAreaIndex;
        set => _gameplaySessionState.ObservedGameplayMapAreaIndex = value;
    }

    private GameplaySessionKind _gameplaySessionKind
    {
        get => _gameplaySessionState.GameplaySessionKind;
        set => _gameplaySessionState.GameplaySessionKind = value;
    }

    private ExperimentalGameplaySettings _practiceExperimentalGameplaySettings
    {
        get => _gameplaySessionState.PracticeExperimentalGameplaySettings;
        set => _gameplaySessionState.PracticeExperimentalGameplaySettings = value;
    }

    private bool _practiceStickyGibBloodEnabled
    {
        get => _gameplaySessionState.PracticeStickyGibBloodEnabled;
        set => _gameplaySessionState.PracticeStickyGibBloodEnabled = value;
    }

    private string _autoBalanceNoticeText
    {
        get => _gameplaySessionState.AutoBalanceNoticeText;
        set => _gameplaySessionState.AutoBalanceNoticeText = value;
    }

    private int _autoBalanceNoticeTicks
    {
        get => _gameplaySessionState.AutoBalanceNoticeTicks;
        set => _gameplaySessionState.AutoBalanceNoticeTicks = value;
    }

    private int _pendingHostedConnectTicks
    {
        get => _gameplaySessionState.PendingHostedConnectTicks;
        set => _gameplaySessionState.PendingHostedConnectTicks = value;
    }

    private int _pendingHostedConnectPort
    {
        get => _gameplaySessionState.PendingHostedConnectPort;
        set => _gameplaySessionState.PendingHostedConnectPort = value;
    }

    private string? _recentConnectHost
    {
        get => _gameplaySessionState.RecentConnectHost;
        set => _gameplaySessionState.RecentConnectHost = value;
    }

    private int _recentConnectPort
    {
        get => _gameplaySessionState.RecentConnectPort;
        set => _gameplaySessionState.RecentConnectPort = value;
    }

    private bool _teamSelectOpen
    {
        get => _uiShellState.TeamSelectOpen;
        set => _uiShellState.TeamSelectOpen = value;
    }

    private float _teamSelectAlpha
    {
        get => _uiShellState.TeamSelectAlpha;
        set => _uiShellState.TeamSelectAlpha = value;
    }

    private float _teamSelectPanelY
    {
        get => _uiShellState.TeamSelectPanelY;
        set => _uiShellState.TeamSelectPanelY = value;
    }

    private int _teamSelectHoverIndex
    {
        get => _uiShellState.TeamSelectHoverIndex;
        set => _uiShellState.TeamSelectHoverIndex = value;
    }

    private PlayerTeam? _pendingClassSelectTeam
    {
        get => _uiShellState.PendingClassSelectTeam;
        set => _uiShellState.PendingClassSelectTeam = value;
    }

    private bool _classSelectOpen
    {
        get => _uiShellState.ClassSelectOpen;
        set => _uiShellState.ClassSelectOpen = value;
    }

    private float _classSelectAlpha
    {
        get => _uiShellState.ClassSelectAlpha;
        set => _uiShellState.ClassSelectAlpha = value;
    }

    private float _classSelectPanelY
    {
        get => _uiShellState.ClassSelectPanelY;
        set => _uiShellState.ClassSelectPanelY = value;
    }

    private int _classSelectHoverIndex
    {
        get => _uiShellState.ClassSelectHoverIndex;
        set => _uiShellState.ClassSelectHoverIndex = value;
    }

    private int _classSelectPortraitAnimationHoverIndex
    {
        get => _uiShellState.ClassSelectPortraitAnimationHoverIndex;
        set => _uiShellState.ClassSelectPortraitAnimationHoverIndex = value;
    }

    private PlayerTeam? _classSelectPortraitAnimationTeam
    {
        get => _uiShellState.ClassSelectPortraitAnimationTeam;
        set => _uiShellState.ClassSelectPortraitAnimationTeam = value;
    }

    private float _classSelectPortraitAnimationFrame
    {
        get => _uiShellState.ClassSelectPortraitAnimationFrame;
        set => _uiShellState.ClassSelectPortraitAnimationFrame = value;
    }

    private int _gameplayLoadoutPortraitAnimationHoverIndex
    {
        get => _uiShellState.GameplayLoadoutPortraitAnimationHoverIndex;
        set => _uiShellState.GameplayLoadoutPortraitAnimationHoverIndex = value;
    }

    private PlayerTeam? _gameplayLoadoutPortraitAnimationTeam
    {
        get => _uiShellState.GameplayLoadoutPortraitAnimationTeam;
        set => _uiShellState.GameplayLoadoutPortraitAnimationTeam = value;
    }

    private float _gameplayLoadoutPortraitAnimationFrame
    {
        get => _uiShellState.GameplayLoadoutPortraitAnimationFrame;
        set => _uiShellState.GameplayLoadoutPortraitAnimationFrame = value;
    }

    private bool _scoreboardOpen
    {
        get => _uiShellState.ScoreboardOpen;
        set => _uiShellState.ScoreboardOpen = value;
    }

    private float _scoreboardAlpha
    {
        get => _uiShellState.ScoreboardAlpha;
        set => _uiShellState.ScoreboardAlpha = value;
    }

    private bool _chatOpen
    {
        get => _uiShellState.ChatOpen;
        set => _uiShellState.ChatOpen = value;
    }

    private bool _chatTeamOnly
    {
        get => _uiShellState.ChatTeamOnly;
        set => _uiShellState.ChatTeamOnly = value;
    }

    private bool _chatSubmitAwaitingOpenKeyRelease
    {
        get => _uiShellState.ChatSubmitAwaitingOpenKeyRelease;
        set => _uiShellState.ChatSubmitAwaitingOpenKeyRelease = value;
    }

    private string _chatInput
    {
        get => _uiShellState.ChatInput;
        set => _uiShellState.ChatInput = value;
    }

    private int _chatScrollOffset
    {
        get => _uiShellState.ChatScrollOffset;
        set => _uiShellState.ChatScrollOffset = value;
    }

    private BubbleMenuKind _bubbleMenuKind
    {
        get => _uiShellState.BubbleMenuKind;
        set => _uiShellState.BubbleMenuKind = value;
    }

    private float _bubbleMenuAlpha
    {
        get => _uiShellState.BubbleMenuAlpha;
        set => _uiShellState.BubbleMenuAlpha = value;
    }

    private float _bubbleMenuX
    {
        get => _uiShellState.BubbleMenuX;
        set => _uiShellState.BubbleMenuX = value;
    }

    private bool _bubbleMenuClosing
    {
        get => _uiShellState.BubbleMenuClosing;
        set => _uiShellState.BubbleMenuClosing = value;
    }

    private int _bubbleMenuXPageIndex
    {
        get => _uiShellState.BubbleMenuXPageIndex;
        set => _uiShellState.BubbleMenuXPageIndex = value;
    }

    private bool _bubbleMenuSessionHadInteraction
    {
        get => _uiShellState.BubbleMenuSessionHadInteraction;
        set => _uiShellState.BubbleMenuSessionHadInteraction = value;
    }

    private int? _bubbleMenuPendingFrame
    {
        get => _uiShellState.BubbleMenuPendingFrame;
        set => _uiShellState.BubbleMenuPendingFrame = value;
    }

    private int _recentBubbleFrameZ
    {
        get => _uiShellState.RecentBubbleFrameZ;
        set => _uiShellState.RecentBubbleFrameZ = value;
    }

    private int _recentBubbleFrameX
    {
        get => _uiShellState.RecentBubbleFrameX;
        set => _uiShellState.RecentBubbleFrameX = value;
    }

    private int _recentBubbleFrameC
    {
        get => _uiShellState.RecentBubbleFrameC;
        set => _uiShellState.RecentBubbleFrameC = value;
    }

    private bool _buildMenuOpen
    {
        get => _uiShellState.BuildMenuOpen;
        set => _uiShellState.BuildMenuOpen = value;
    }

    private bool _buildMenuClosing
    {
        get => _uiShellState.BuildMenuClosing;
        set => _uiShellState.BuildMenuClosing = value;
    }

    private float _buildMenuAlpha
    {
        get => _uiShellState.BuildMenuAlpha;
        set => _uiShellState.BuildMenuAlpha = value;
    }

    private float _buildMenuX
    {
        get => _uiShellState.BuildMenuX;
        set => _uiShellState.BuildMenuX = value;
    }

    private bool _startupSplashOpen
    {
        get => _uiShellState.StartupSplashOpen;
        set => _uiShellState.StartupSplashOpen = value;
    }

    private int _startupSplashTicks
    {
        get => _uiShellState.StartupSplashTicks;
        set => _uiShellState.StartupSplashTicks = value;
    }

    private float _startupSplashFrame
    {
        get => _uiShellState.StartupSplashFrame;
        set => _uiShellState.StartupSplashFrame = value;
    }

    private bool _mainMenuOpen
    {
        get => _uiShellState.MainMenuOpen;
        set => _uiShellState.MainMenuOpen = value;
    }

    private bool _optionsMenuOpen
    {
        get => _uiShellState.OptionsMenuOpen;
        set => _uiShellState.OptionsMenuOpen = value;
    }

    private bool _optionsMenuOpenedFromGameplay
    {
        get => _uiShellState.OptionsMenuOpenedFromGameplay;
        set => _uiShellState.OptionsMenuOpenedFromGameplay = value;
    }

    private bool _pluginOptionsMenuOpen
    {
        get => _uiShellState.PluginOptionsMenuOpen;
        set => _uiShellState.PluginOptionsMenuOpen = value;
    }

    private bool _pluginOptionsMenuOpenedFromGameplay
    {
        get => _uiShellState.PluginOptionsMenuOpenedFromGameplay;
        set => _uiShellState.PluginOptionsMenuOpenedFromGameplay = value;
    }

    private string? _selectedPluginOptionsPluginId
    {
        get => _uiShellState.SelectedPluginOptionsPluginId;
        set => _uiShellState.SelectedPluginOptionsPluginId = value;
    }

    private bool _lobbyBrowserOpen
    {
        get => _uiShellState.LobbyBrowserOpen;
        set => _uiShellState.LobbyBrowserOpen = value;
    }

    private bool _manualConnectOpen
    {
        get => _uiShellState.ManualConnectOpen;
        set => _uiShellState.ManualConnectOpen = value;
    }

    private bool _hostSetupOpen
    {
        get => _uiShellState.HostSetupOpen;
        set => _uiShellState.HostSetupOpen = value;
    }

    private bool _practiceSetupOpen
    {
        get => _uiShellState.PracticeSetupOpen;
        set => _uiShellState.PracticeSetupOpen = value;
    }

    private bool _clientPowersOpen
    {
        get => _uiShellState.ClientPowersOpen;
        set => _uiShellState.ClientPowersOpen = value;
    }

    private bool _clientPowersOpenedFromGameplay
    {
        get => _uiShellState.ClientPowersOpenedFromGameplay;
        set => _uiShellState.ClientPowersOpenedFromGameplay = value;
    }

    private bool _creditsOpen
    {
        get => _uiShellState.CreditsOpen;
        set => _uiShellState.CreditsOpen = value;
    }

    private bool _creditsScrollInitialized
    {
        get => _uiShellState.CreditsScrollInitialized;
        set => _uiShellState.CreditsScrollInitialized = value;
    }

    private float _creditsScrollY
    {
        get => _uiShellState.CreditsScrollY;
        set => _uiShellState.CreditsScrollY = value;
    }

    private bool _inGameMenuOpen
    {
        get => _uiShellState.InGameMenuOpen;
        set => _uiShellState.InGameMenuOpen = value;
    }

    private bool _inGameMenuAwaitingEscapeRelease
    {
        get => _uiShellState.InGameMenuAwaitingEscapeRelease;
        set => _uiShellState.InGameMenuAwaitingEscapeRelease = value;
    }

    private bool _gameplayLoadoutMenuOpen
    {
        get => _uiShellState.GameplayLoadoutMenuOpen;
        set => _uiShellState.GameplayLoadoutMenuOpen = value;
    }

    private bool _gameplayLoadoutMenuAwaitingEscapeRelease
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

    private bool _quitPromptOpen
    {
        get => _uiShellState.QuitPromptOpen;
        set => _uiShellState.QuitPromptOpen = value;
    }

    private int _quitPromptHoverIndex
    {
        get => _uiShellState.QuitPromptHoverIndex;
        set => _uiShellState.QuitPromptHoverIndex = value;
    }

    private bool _controlsMenuOpen
    {
        get => _uiShellState.ControlsMenuOpen;
        set => _uiShellState.ControlsMenuOpen = value;
    }

    private bool _controlsMenuOpenedFromGameplay
    {
        get => _uiShellState.ControlsMenuOpenedFromGameplay;
        set => _uiShellState.ControlsMenuOpenedFromGameplay = value;
    }

    private bool _editingPlayerName
    {
        get => _uiShellState.EditingPlayerName;
        set => _uiShellState.EditingPlayerName = value;
    }

    private bool _editingConnectHost
    {
        get => _uiShellState.EditingConnectHost;
        set => _uiShellState.EditingConnectHost = value;
    }

    private bool _editingConnectPort
    {
        get => _uiShellState.EditingConnectPort;
        set => _uiShellState.EditingConnectPort = value;
    }

    private bool _passwordPromptOpen
    {
        get => _uiShellState.PasswordPromptOpen;
        set => _uiShellState.PasswordPromptOpen = value;
    }

    private string _passwordEditBuffer
    {
        get => _uiShellState.PasswordEditBuffer;
        set => _uiShellState.PasswordEditBuffer = value;
    }

    private string _passwordPromptMessage
    {
        get => _uiShellState.PasswordPromptMessage;
        set => _uiShellState.PasswordPromptMessage = value;
    }

    private MainMenuPage _mainMenuPage
    {
        get => _uiShellState.MainMenuPage;
        set => _uiShellState.MainMenuPage = value;
    }

    private int _mainMenuHoverIndex
    {
        get => _uiShellState.MainMenuHoverIndex;
        set => _uiShellState.MainMenuHoverIndex = value;
    }

    private bool _mainMenuBottomBarHover
    {
        get => _uiShellState.MainMenuBottomBarHover;
        set => _uiShellState.MainMenuBottomBarHover = value;
    }

    private int _optionsHoverIndex
    {
        get => _uiShellState.OptionsHoverIndex;
        set => _uiShellState.OptionsHoverIndex = value;
    }

    private int _optionsPageIndex
    {
        get => _uiShellState.OptionsPageIndex;
        set => _uiShellState.OptionsPageIndex = value;
    }

    private int _pluginOptionsHoverIndex
    {
        get => _uiShellState.PluginOptionsHoverIndex;
        set => _uiShellState.PluginOptionsHoverIndex = value;
    }

    private int _pluginOptionsScrollOffset
    {
        get => _uiShellState.PluginOptionsScrollOffset;
        set => _uiShellState.PluginOptionsScrollOffset = value;
    }

    private ClientPluginKeyOptionItem? _pendingPluginOptionsKeyItem
    {
        get => _uiShellState.PendingPluginOptionsKeyItem;
        set => _uiShellState.PendingPluginOptionsKeyItem = value;
    }

    private int _controlsHoverIndex
    {
        get => _uiShellState.ControlsHoverIndex;
        set => _uiShellState.ControlsHoverIndex = value;
    }

    private int _lobbyBrowserHoverIndex
    {
        get => _uiShellState.LobbyBrowserHoverIndex;
        set => _uiShellState.LobbyBrowserHoverIndex = value;
    }

    private int _lobbyBrowserSelectedIndex
    {
        get => _uiShellState.LobbyBrowserSelectedIndex;
        set => _uiShellState.LobbyBrowserSelectedIndex = value;
    }

    private int _clientPowersScrollOffset
    {
        get => _uiShellState.ClientPowersScrollOffset;
        set => _uiShellState.ClientPowersScrollOffset = value;
    }

    private int _inGameMenuHoverIndex
    {
        get => _uiShellState.InGameMenuHoverIndex;
        set => _uiShellState.InGameMenuHoverIndex = value;
    }

    private int _gameplayLoadoutMenuHoverIndex
    {
        get => _uiShellState.GameplayLoadoutMenuHoverIndex;
        set => _uiShellState.GameplayLoadoutMenuHoverIndex = value;
    }

    private string _playerNameEditBuffer
    {
        get => _uiShellState.PlayerNameEditBuffer;
        set => _uiShellState.PlayerNameEditBuffer = value;
    }

    private string _connectHostBuffer
    {
        get => _uiShellState.ConnectHostBuffer;
        set => _uiShellState.ConnectHostBuffer = value;
    }

    private string _connectPortBuffer
    {
        get => _uiShellState.ConnectPortBuffer;
        set => _uiShellState.ConnectPortBuffer = value;
    }

    private string _menuStatusMessage
    {
        get => _uiShellState.MenuStatusMessage;
        set => _uiShellState.MenuStatusMessage = value;
    }

    private ControlsMenuBinding? _pendingControlsBinding
    {
        get => _uiShellState.PendingControlsBinding;
        set => _uiShellState.PendingControlsBinding = value;
    }

    private sealed class UiShellState
    {
        public bool TeamSelectOpen;
        public float TeamSelectAlpha = 0.01f;
        public float TeamSelectPanelY = -120f;
        public int TeamSelectHoverIndex = -1;
        public PlayerTeam? PendingClassSelectTeam;
        public bool ClassSelectOpen;
        public float ClassSelectAlpha = 0.01f;
        public float ClassSelectPanelY = -120f;
        public int ClassSelectHoverIndex = -1;
        public int ClassSelectPortraitAnimationHoverIndex = -1;
        public PlayerTeam? ClassSelectPortraitAnimationTeam;
        public float ClassSelectPortraitAnimationFrame;
        public int GameplayLoadoutPortraitAnimationHoverIndex = -1;
        public PlayerTeam? GameplayLoadoutPortraitAnimationTeam;
        public float GameplayLoadoutPortraitAnimationFrame;
        public bool ScoreboardOpen;
        public float ScoreboardAlpha = 0.02f;
        public bool ChatOpen;
        public bool ChatTeamOnly;
        public bool ChatSubmitAwaitingOpenKeyRelease;
        public string ChatInput = string.Empty;
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
        public bool BuildMenuOpen;
        public bool BuildMenuClosing;
        public float BuildMenuAlpha = 0.01f;
        public float BuildMenuX = -37f;
        public bool StartupSplashOpen = true;
        public int StartupSplashTicks;
        public float StartupSplashFrame;
        public bool MainMenuOpen = true;
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
        public bool CreditsScrollInitialized;
        public float CreditsScrollY;
        public bool InGameMenuOpen;
        public bool InGameMenuAwaitingEscapeRelease;
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
        public string PasswordEditBuffer = string.Empty;
        public string PasswordPromptMessage = string.Empty;
        public MainMenuPage MainMenuPage = MainMenuPage.Root;
        public int MainMenuHoverIndex = -1;
        public bool MainMenuBottomBarHover;
        public int OptionsHoverIndex = -1;
        public int OptionsPageIndex;
        public int PluginOptionsHoverIndex = -1;
        public int PluginOptionsScrollOffset;
        public ClientPluginKeyOptionItem? PendingPluginOptionsKeyItem;
        public int ControlsHoverIndex = -1;
        public int LobbyBrowserHoverIndex = -1;
        public int LobbyBrowserSelectedIndex = -1;
        public int ClientPowersScrollOffset;
        public int InGameMenuHoverIndex = -1;
        public int GameplayLoadoutMenuHoverIndex = -1;
        public string PlayerNameEditBuffer = string.Empty;
        public string ConnectHostBuffer = "127.0.0.1";
        public string ConnectPortBuffer = "8190";
        public string MenuStatusMessage = string.Empty;
        public ControlsMenuBinding? PendingControlsBinding;
    }

    private sealed class GameplaySessionState
    {
        public int? LocalPlayerSnapshotEntityId;
        public int? SpectatorTrackedPlayerId;
        public bool SpectatorTrackingEnabled;
        public string ObservedGameplayLevelName = string.Empty;
        public int ObservedGameplayMapAreaIndex = -1;
        public GameplaySessionKind GameplaySessionKind;
        public ExperimentalGameplaySettings PracticeExperimentalGameplaySettings = new();
        public bool PracticeStickyGibBloodEnabled;
        public string AutoBalanceNoticeText = string.Empty;
        public int AutoBalanceNoticeTicks;
        public int PendingHostedConnectTicks = -1;
        public int PendingHostedConnectPort = 8190;
        public string? RecentConnectHost;
        public int RecentConnectPort;
    }
}
