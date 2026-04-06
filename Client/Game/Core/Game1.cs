#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;
using OpenGarrison.Protocol;


namespace OpenGarrison.Client;

public partial class Game1 : Game
{
    private enum BubbleMenuKind
    {
        None,
        Z,
        X,
        C,
    }

    private enum NoticeKind
    {
        NutsNBolts = 0,
        TooClose = 1,
        AutogunScrapped = 2,
        AutogunExists = 3,
        HaveIntel = 4,
        SetCheckpoint = 5,
        DestroyCheckpoint = 6,
        PlayerTrackEnable = 7,
        PlayerTrackDisable = 8,
    }

    private enum HostSetupEditField
    {
        None,
        ServerName,
        Port,
        Slots,
        Password,
        MapRotationFile,
        TimeLimit,
        CapLimit,
        RespawnSeconds,
        ServerConsoleCommand,
    }

    private enum HostSetupTab
    {
        Settings,
        ServerConsole,
    }

    private enum GameplaySessionKind
    {
        None,
        Online,
        Practice,
        LastToDie,
    }

    private enum MainMenuPage
    {
        Root,
        PlayOnline,
        PlayOffline,
        Minigames,
        Credits,
    }

    private enum ControlsMenuBinding
    {
        MoveUp,
        MoveLeft,
        MoveRight,
        MoveDown,
        Taunt,
        CallMedic,
        FireSecondaryWeapon,
        InteractWeapon,
        ChangeTeam,
        ChangeClass,
        ShowScoreboard,
        ToggleConsole,
        OpenBubbleMenuZ,
        OpenBubbleMenuX,
        OpenBubbleMenuC,
    }

    private const int ProcessedNetworkEventHistoryLimit = 4096;
    private readonly GameStartupMode _startupMode;
    private readonly FrameController _frameController;
    private readonly GameplayController _gameplayController;
    private readonly GameplayScreenStateController _gameplayScreenStateController;
    private readonly GameplayPresentationStateController _gameplayPresentationStateController;
    private readonly GameplayImpactEffectsController _gameplayImpactEffectsController;
    private readonly GameplayGoreEffectsController _gameplayGoreEffectsController;
    private readonly GameplaySmokeEffectsController _gameplaySmokeEffectsController;
    private readonly GameplayLocalStatusHudController _gameplayLocalStatusHudController;
    private readonly GameplayMedicHudController _gameplayMedicHudController;
    private readonly GameplayEngineerHudController _gameplayEngineerHudController;
    private readonly GameplayAimHudController _gameplayAimHudController;
    private readonly GameplayPlayerNameHudController _gameplayPlayerNameHudController;
    private readonly GameplayPlayerRenderController _gameplayPlayerRenderController;
    private readonly GameplayDeadBodyRenderController _gameplayDeadBodyRenderController;
    private readonly GameplayPlayerSpriteRenderController _gameplayPlayerSpriteRenderController;
    private readonly GameplayWeaponRenderController _gameplayWeaponRenderController;
    private readonly GameplayPlayerStatusEffectRenderController _gameplayPlayerStatusEffectRenderController;
    private readonly GameplaySessionController _gameplaySessionController;
    private readonly GameplayOverlayStateController _gameplayOverlayStateController;
    private readonly GameplayResetController _gameplayResetController;
    private readonly ClientPluginRuntimeController _clientPluginRuntimeController;
    private readonly ClientPluginEventController _clientPluginEventController;
    private readonly ClientPluginUiBridgeController _clientPluginUiBridgeController;
    private readonly ClientPluginMarkerController _clientPluginMarkerController;
    private readonly MenuController _menuController;
    private readonly OptionsMenuController _optionsMenuController;
    private readonly MainMenuPageController _mainMenuPageController;
    private readonly PluginOptionsMenuController _pluginOptionsMenuController;
    private readonly ControlsMenuController _controlsMenuController;
    private readonly InGameMenuController _inGameMenuController;
    private readonly GameplayOverlayController _gameplayOverlayController;
    private readonly GraphicsDeviceManager _graphics;
    private RenderTarget2D? _gameRenderTarget;
    private SimulationConfig _config = null!;
    private SimulationWorld _world = null!;
    private FixedStepSimulator _simulator = null!;
    private readonly NetworkGameClient _networkClient = new();
    private readonly GameMakerAssetManifest _assetManifest;
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private Texture2D? _menuBackgroundTexture;
    private string? _menuBackgroundTexturePath;
    private string _menuBackgroundAttributionText = string.Empty;
    private SpriteFont _consoleFont = null!;
    private SpriteFont _menuFont = null!;
    private Texture2D? _menuBitmapFontTexture;
    private readonly Dictionary<char, MenuBitmapGlyph> _menuBitmapFontGlyphs = new();
    private int _menuBitmapFontLineHeight;
    private int _menuBitmapFontSpacing = 1;
    private Texture2D? _menuPlaqueTexture;
    private Texture2D? _menuPlaqueTallTexture;
    private Texture2D? _menuTextBoxTopTexture;
    private Texture2D? _menuTextBoxMiddleTexture;
    private Texture2D? _menuTextBoxBottomTexture;
    private Texture2D? _menuTextBoxSoloTexture;
    private GameMakerRuntimeAssetCache _runtimeAssets = null!;
    private readonly Dictionary<Texture2D, Rectangle> _spriteFontOpaqueBoundsCache = new();
    private KeyboardState _previousKeyboard;
    private KeyboardState _clientPluginPreviousKeyboard;
    private KeyboardState _clientPluginKeyboard;
    private readonly Dictionary<int, PlayerRenderState> _playerRenderStates = new();
    private readonly Dictionary<int, Vector2> _playerPreviousRenderPositions = new();
    private readonly Dictionary<int, double> _playerPreviousRenderSampleTimes = new();
    private int? _localPlayerSnapshotEntityId;
    private int? _spectatorTrackedPlayerId;
    private bool _spectatorTrackingEnabled;
    private readonly Random _visualRandom = new(1337);
    private bool _wasLocalPlayerAlive = true;
    private bool _wasDeathCamActive;
    private bool _wasMatchEnded;
    private int _previousLocalDemoknightChargeTicks = PlayerEntity.ExperimentalDemoknightChargeMaxTicks;
    private string _observedGameplayLevelName = string.Empty;
    private int _observedGameplayMapAreaIndex = -1;
    private MouseState _previousMouse;
    private bool _suppressPrimaryFireUntilMouseRelease;
    private Vector2 _respawnCameraCenter;
    private bool _respawnCameraDetached;
    private bool _teamSelectOpen;
    private float _teamSelectAlpha = 0.01f;
    private float _teamSelectPanelY = -120f;
    private int _teamSelectHoverIndex = -1;
    private PlayerTeam? _pendingClassSelectTeam;
    private bool _classSelectOpen;
    private float _classSelectAlpha = 0.01f;
    private float _classSelectPanelY = -120f;
    private int _classSelectHoverIndex = -1;
    private int _classSelectPortraitAnimationHoverIndex = -1;
    private PlayerTeam? _classSelectPortraitAnimationTeam;
    private float _classSelectPortraitAnimationFrame;
    private bool _scoreboardOpen;
    private float _scoreboardAlpha = 0.02f;
    private bool _chatOpen;
    private bool _chatTeamOnly;
    private bool _chatSubmitAwaitingOpenKeyRelease;
    private string _chatInput = string.Empty;
    private BubbleMenuKind _bubbleMenuKind;
    private float _bubbleMenuAlpha = 0.01f;
    private float _bubbleMenuX = -30f;
    private bool _bubbleMenuClosing;
    private int _bubbleMenuXPageIndex;
    private bool _bubbleMenuSessionHadInteraction;
    private int? _bubbleMenuPendingFrame;
    private int _recentBubbleFrameZ = 20;
    private int _recentBubbleFrameX = 29;
    private int _recentBubbleFrameC = 36;
    private bool _buildMenuOpen;
    private bool _buildMenuClosing;
    private float _buildMenuAlpha = 0.01f;
    private float _buildMenuX = -37f;
    private NoticeState? _notice;
    private bool _hadLocalSentry;
    private bool _wasCarryingIntel;
    private readonly Queue<QueuedPluginNotice> _queuedPluginNotices = new();
    private bool _startupSplashOpen = true;
    private int _startupSplashTicks;
    private float _startupSplashFrame;
    private bool _mainMenuOpen = true;
    private bool _optionsMenuOpen;
    private bool _optionsMenuOpenedFromGameplay;
    private bool _pluginOptionsMenuOpen;
    private bool _pluginOptionsMenuOpenedFromGameplay;
    private string? _selectedPluginOptionsPluginId;
    private bool _lobbyBrowserOpen;
    private bool _manualConnectOpen;
    private bool _hostSetupOpen;
    private bool _practiceSetupOpen;
    private bool _clientPowersOpen;
    private bool _clientPowersOpenedFromGameplay;
    private bool _creditsOpen;
    private bool _creditsScrollInitialized;
    private float _creditsScrollY;
    private bool _inGameMenuOpen;
    private bool _inGameMenuAwaitingEscapeRelease;
    private bool _quitPromptOpen;
    private int _quitPromptHoverIndex = -1;
    private bool _controlsMenuOpen;
    private bool _controlsMenuOpenedFromGameplay;
    private bool _editingPlayerName;
    private bool _editingConnectHost;
    private bool _editingConnectPort;
    private bool _passwordPromptOpen;
    private string _passwordEditBuffer = string.Empty;
    private string _passwordPromptMessage = string.Empty;
    private MainMenuPage _mainMenuPage = MainMenuPage.Root;
    private int _mainMenuHoverIndex = -1;
    private bool _mainMenuBottomBarHover;
    private int _optionsHoverIndex = -1;
    private int _optionsPageIndex;
    private int _pluginOptionsHoverIndex = -1;
    private int _pluginOptionsScrollOffset;
    private ClientPluginKeyOptionItem? _pendingPluginOptionsKeyItem;
    private int _controlsHoverIndex = -1;
    private int _lobbyBrowserHoverIndex = -1;
    private int _lobbyBrowserSelectedIndex = -1;
    private int _clientPowersScrollOffset;
    private int _inGameMenuHoverIndex = -1;
    private GameplaySessionKind _gameplaySessionKind;
    private readonly HostSetupFormState _hostSetupState = new();
    private readonly PracticeSetupState _practiceSetupState = new();
    private readonly HostedServerConsoleState _hostedServerConsole = new();
    private readonly HostedServerRuntimeController _hostedServerRuntime;
    private string _playerNameEditBuffer = string.Empty;
    private string _connectHostBuffer = "127.0.0.1";
    private string _connectPortBuffer = "8190";
    private string _menuStatusMessage = string.Empty;
    private ExperimentalGameplaySettings _practiceExperimentalGameplaySettings = new();
    private bool _practiceStickyGibBloodEnabled;
    private bool _devMessageCheckStarted;
    private bool _devMessageCheckFinished;
    private Task<DevMessageFetchResult>? _devMessageFetchTask;
    private readonly Queue<DevMessagePopupState> _pendingDevMessagePopups = new();
    private DevMessagePopupState? _activeDevMessagePopup;
    private string _autoBalanceNoticeText = string.Empty;
    private int _autoBalanceNoticeTicks;
    private bool _killCamEnabled = true;
    private IngameResolutionKind _ingameResolution = IngameResolutionKind.Aspect4x3;
    private int _particleMode;
    private int _gibLevel = 3;
    private int _corpseDurationMode;
    private bool _healerRadarEnabled = true;
    private bool _showHealerEnabled = true;
    private bool _showHealingEnabled = true;
    private bool _showHealthBarEnabled;
    private bool _showPersistentSelfNameEnabled;
    private bool _spriteDropShadowEnabled;
    private bool _wasWindowActive = true;
    private int _menuImageFrame;
    private ControlsMenuBinding? _pendingControlsBinding;
    private readonly List<ChatLine> _chatLines = new();

    public Game1(GameStartupMode startupMode = GameStartupMode.Client)
    {
        _startupMode = startupMode;
        _frameController = new FrameController(this);
        _gameplayController = new GameplayController(this);
        _gameplayScreenStateController = new GameplayScreenStateController(this);
        _gameplayPresentationStateController = new GameplayPresentationStateController(this);
        _gameplayImpactEffectsController = new GameplayImpactEffectsController(this);
        _gameplayGoreEffectsController = new GameplayGoreEffectsController(this);
        _gameplaySmokeEffectsController = new GameplaySmokeEffectsController(this);
        _gameplayLocalStatusHudController = new GameplayLocalStatusHudController(this);
        _gameplayMedicHudController = new GameplayMedicHudController(this);
        _gameplayEngineerHudController = new GameplayEngineerHudController(this);
        _gameplayAimHudController = new GameplayAimHudController(this);
        _gameplayPlayerNameHudController = new GameplayPlayerNameHudController(this);
        _gameplayPlayerRenderController = new GameplayPlayerRenderController(this);
        _gameplayDeadBodyRenderController = new GameplayDeadBodyRenderController(this);
        _gameplayPlayerSpriteRenderController = new GameplayPlayerSpriteRenderController(this);
        _gameplayWeaponRenderController = new GameplayWeaponRenderController(this);
        _gameplayPlayerStatusEffectRenderController = new GameplayPlayerStatusEffectRenderController(this);
        _gameplaySessionController = new GameplaySessionController(this);
        _gameplayOverlayStateController = new GameplayOverlayStateController(this);
        _gameplayResetController = new GameplayResetController(this);
        _clientPluginRuntimeController = new ClientPluginRuntimeController(this);
        _clientPluginEventController = new ClientPluginEventController(this);
        _clientPluginUiBridgeController = new ClientPluginUiBridgeController(this);
        _clientPluginMarkerController = new ClientPluginMarkerController(this);
        _menuController = new MenuController(this);
        _optionsMenuController = new OptionsMenuController(this);
        _mainMenuPageController = new MainMenuPageController(this);
        _pluginOptionsMenuController = new PluginOptionsMenuController(this);
        _controlsMenuController = new ControlsMenuController(this);
        _inGameMenuController = new InGameMenuController(this);
        _gameplayOverlayController = new GameplayOverlayController(this);
        _clientSettings = ClientSettings.Load();
        _inputBindings = InputBindingsSettings.Load();
        _hostedServerRuntime = new HostedServerRuntimeController(_hostedServerConsole);
        _graphics = new GraphicsDeviceManager(this);
        _graphics.HardwareModeSwitch = false;
        Content.RootDirectory = "Content";
        ContentRoot.Initialize(Content.RootDirectory);
        IsMouseVisible = false;
        ApplyIngameResolution(_clientSettings.IngameResolution);
        ApplyPreferredBackBufferSize(_clientSettings.Fullscreen, _ingameResolution);

        ReinitializeSimulationForTickRate(SimulationConfig.DefaultTicksPerSecond);
        _assetManifest = GameMakerAssetManifestImporter.ImportProjectAssets();
        ApplyLoadedSettings();

        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1d / ClientUpdateTicksPerSecond);
    }

    protected override void Initialize()
    {
        Window.TextInput += OnWindowTextInput;
        Window.Title = _startupMode == GameStartupMode.ServerLauncher
            ? $"OG2.ServerLauncher - Proto (Protocol v{ProtocolVersion.Current})"
            : $"OG2 - Proto (Protocol v{ProtocolVersion.Current})";
        _menuImageFrame = _visualRandom.Next(2);
        _playerNameEditBuffer = _world.LocalPlayer.DisplayName;
        AddConsoleLine("debug console ready (`)");
        InitializeClientPlugins();
        if (_startupMode == GameStartupMode.ServerLauncher)
        {
            InitializeServerLauncherMode();
        }

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _consoleFont = Content.Load<SpriteFont>("ConsoleFont");
        _menuFont = Content.Load<SpriteFont>("MenuFont");
        _runtimeAssets = new GameMakerRuntimeAssetCache(GraphicsDevice, _assetManifest);
        _spriteFontOpaqueBoundsCache.Clear();
        LoadMenuPlaqueTextures();
        LoadMenuBitmapFont();
        LoadMenuMusic();
        LoadLastToDieMenuMusic();
        LoadFaucetMusic();
        LoadIngameMusic();
        LoadLastToDieIngameMusic();
        ApplyAudioMuteState();
        AddConsoleLine($"gm assets sprites={_assetManifest.Sprites.Count} backgrounds={_assetManifest.Backgrounds.Count} sounds={_assetManifest.Sounds.Count}");
        NotifyClientPluginsStarted();
    }

    protected override void UnloadContent()
    {
        ShutdownClientPlugins();
        _menuMusicInstance?.Dispose();
        _menuMusic?.Dispose();
        _lastToDieMenuMusicInstance?.Dispose();
        _lastToDieMenuMusic?.Dispose();
        _faucetMusicInstance?.Dispose();
        _faucetMusic?.Dispose();
        _ingameMusicInstance?.Dispose();
        _ingameMusic?.Dispose();
        _lastToDieIngameMusicInstance?.Dispose();
        _lastToDieIngameMusic?.Dispose();
        StopHostedServer();
        _networkClient.Dispose();
        _runtimeAssets?.Dispose();
        _spriteFontOpaqueBoundsCache.Clear();
        _menuBackgroundTexture?.Dispose();
        _menuBitmapFontTexture?.Dispose();
        _menuPlaqueTexture?.Dispose();
        _menuPlaqueTallTexture?.Dispose();
        _menuTextBoxTopTexture?.Dispose();
        _menuTextBoxMiddleTexture?.Dispose();
        _menuTextBoxBottomTexture?.Dispose();
        _menuTextBoxSoloTexture?.Dispose();
        _lastToDieLogoTexture?.Dispose();
        _gameRenderTarget?.Dispose();
        _gameRenderTarget = null;
        _deathCamCaptureTarget?.Dispose();
        _deathCamCaptureTarget = null;
        _menuBackgroundTexture = null;
        _menuBackgroundTexturePath = null;
        _menuBitmapFontTexture = null;
        _menuBitmapFontGlyphs.Clear();
        _menuBitmapFontLineHeight = 0;
        _menuPlaqueTexture = null;
        _menuPlaqueTallTexture = null;
        _menuTextBoxTopTexture = null;
        _menuTextBoxMiddleTexture = null;
        _menuTextBoxBottomTexture = null;
        _menuTextBoxSoloTexture = null;
        PersistClientSettings();
        PersistInputBindings();
        base.UnloadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        BeginNetworkDiagnosticsFrame(gameTime);
        _networkInterpolationClockSeconds = _networkInterpolationClock.Elapsed.TotalSeconds;
        var clientTicks = _frameController.Update(gameTime);
        NotifyClientPluginsFrame(gameTime, clientTicks);
        FinalizeNetworkDiagnosticsFrame();

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _networkInterpolationClockSeconds = _networkInterpolationClock.Elapsed.TotalSeconds;
        GraphicsDevice.Clear(new Color(24, 32, 48));
        _frameController.Draw(gameTime);

        base.Draw(gameTime);
    }

    private void DrawGameplayWorldForCamera(Vector2 cameraPosition, int viewportWidth, int viewportHeight, int? skippedDeadBodySourcePlayerId = null)
    {
        _frameController.DrawGameplayWorldForCamera(cameraPosition, viewportWidth, viewportHeight, skippedDeadBodySourcePlayerId);
    }

    private MainMenuOverlayKind GetActiveMainMenuOverlay()
    {
        return _menuController.GetActiveOverlay();
    }

    private void OpenOptionsMenu(bool fromGameplay)
    {
        _optionsMenuController.OpenOptionsMenu(fromGameplay);
    }

    private void CloseOptionsMenu()
    {
        _optionsMenuController.CloseOptionsMenu();
    }

    private void OpenPluginOptionsMenu(bool fromGameplay)
    {
        _optionsMenuController.OpenPluginOptionsMenu(fromGameplay);
    }

    private void ClosePluginOptionsMenu()
    {
        _optionsMenuController.ClosePluginOptionsMenu();
    }

    private void OpenControlsMenu(bool fromGameplay)
    {
        _controlsMenuController.OpenControlsMenu(fromGameplay);
    }

    private void CloseControlsMenu()
    {
        _controlsMenuController.CloseControlsMenu();
    }

    private void UpdateOptionsMenu(KeyboardState keyboard, MouseState mouse)
    {
        _optionsMenuController.UpdateOptionsMenu(keyboard, mouse);
    }

    private void DrawOptionsMenu()
    {
        _optionsMenuController.DrawOptionsMenu();
    }

    private void UpdatePluginOptionsMenu(KeyboardState keyboard, MouseState mouse)
    {
        _pluginOptionsMenuController.UpdatePluginOptionsMenu(keyboard, mouse);
    }

    private void DrawPluginOptionsMenu()
    {
        _pluginOptionsMenuController.DrawPluginOptionsMenu();
    }

    private bool HasClientPluginOptions()
    {
        return _pluginOptionsMenuController.HasClientPluginOptions();
    }

    private void UpdateControlsMenu(KeyboardState keyboard, MouseState mouse)
    {
        _controlsMenuController.UpdateControlsMenu(keyboard, mouse);
    }

    private void DrawControlsMenu()
    {
        _controlsMenuController.DrawControlsMenu();
    }

    private void OpenInGameMenu()
    {
        _inGameMenuController.OpenInGameMenu();
    }

    private void CloseInGameMenu()
    {
        _inGameMenuController.CloseInGameMenu();
    }

    private void UpdateInGameMenu(KeyboardState keyboard, MouseState mouse)
    {
        _inGameMenuController.UpdateInGameMenu(keyboard, mouse);
    }

    private void DrawInGameMenu()
    {
        _inGameMenuController.DrawInGameMenu();
    }

    private GameplayOverlayKind GetActiveGameplayOverlay()
    {
        return _gameplayOverlayController.GetActiveOverlay();
    }

    private void UpdateGameplayMenuState(KeyboardState keyboard, MouseState mouse)
    {
        _gameplayOverlayController.Update(keyboard, mouse);
    }

    private void OpenMainMenuPage(MainMenuPage page)
    {
        _mainMenuPageController.OpenMainMenuPage(page);
    }

    private List<MenuPageButton> BuildMainMenuButtons()
    {
        return _mainMenuPageController.BuildMainMenuButtons();
    }

    private void DrawCurrentMainMenuPage(IReadOnlyList<MenuPageButton> buttons)
    {
        _mainMenuPageController.DrawCurrentMainMenuPage(buttons);
    }

    private void AddPluginMenuActions(List<MenuPageAction> actions, ClientPluginMenuLocation location, int insertIndex = -1)
    {
        _mainMenuPageController.AddPluginMenuActions(actions, location, insertIndex);
    }
















    private sealed class NoticeState
    {
        public NoticeState(string text, float alpha, bool done, int ticksRemaining, bool playSound)
        {
            Text = text;
            Alpha = alpha;
            Done = done;
            TicksRemaining = ticksRemaining;
            PlaySound = playSound;
        }

        public string Text { get; set; }

        public float Alpha { get; set; }

        public bool Done { get; set; }

        public int TicksRemaining { get; set; }

        public bool PlaySound { get; set; }
    }

    private sealed class QueuedPluginNotice(string text, int ticksRemaining, bool playSound)
    {
        public string Text { get; } = text;

        public int TicksRemaining { get; } = ticksRemaining;

        public bool PlaySound { get; } = playSound;
    }

    private sealed class ChatLine
    {
        public ChatLine(string playerName, string text, byte team, bool teamOnly)
        {
            PlayerName = playerName;
            Text = text;
            Team = team;
            TeamOnly = teamOnly;
            TicksRemaining = 600;
        }

        public string PlayerName { get; }

        public string Text { get; }

        public byte Team { get; }

        public bool TeamOnly { get; }

        public int TicksRemaining { get; set; }
    }

    private sealed class PracticeMapEntry
    {
        public PracticeMapEntry(string levelName, string displayName, GameModeKind mode, bool isCustomMap)
        {
            LevelName = levelName;
            DisplayName = displayName;
            Mode = mode;
            IsCustomMap = isCustomMap;
        }

        public string LevelName { get; }

        public string DisplayName { get; }

        public GameModeKind Mode { get; }

        public bool IsCustomMap { get; }
    }

    private sealed class DevMessagePopupState
    {
        public DevMessagePopupState(
            string title,
            string message,
            string primaryButtonLabel,
            string secondaryButtonLabel,
            bool canRunPrimaryAction,
            string? primaryActionPath = null)
        {
            Title = title;
            Message = message;
            PrimaryButtonLabel = primaryButtonLabel;
            SecondaryButtonLabel = secondaryButtonLabel;
            CanRunPrimaryAction = canRunPrimaryAction;
            PrimaryActionPath = primaryActionPath;
        }

        public string Title { get; }

        public string Message { get; }

        public string PrimaryButtonLabel { get; }

        public string SecondaryButtonLabel { get; }

        public bool CanRunPrimaryAction { get; }

        public string? PrimaryActionPath { get; }
    }
}
