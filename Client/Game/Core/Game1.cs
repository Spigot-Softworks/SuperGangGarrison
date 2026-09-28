#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using OpenGarrison.Client.Plugins;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using OpenGarrison.Protocol;


namespace OpenGarrison.Client;

public partial class Game1 : Game
{
#if GG2_ONLY
    private const string WindowTitle = "OpenGarrison";
#else
    private const string WindowTitle = "Super Gang Garrison";
#endif

    private enum BubbleMenuKind
    {
        None,
        Z,
        X,
        C,
        Custom,
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
        RconPassword,
        MapRotationFile,
        TimeLimit,
        CapLimit,
        RespawnSeconds,
        AdvancedCvar,
        ServerConsoleCommand,
        MapNameFilter,
    }

    private enum PracticeEditField
    {
        None,
        MapNameFilter,
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
        Jump,
    }

    private enum MainMenuPage
    {
        Root,
        PlayOnline,
        PlayOffline,
    }

    private enum ControlsMenuBinding
    {
        MoveUp,
        MoveLeft,
        MoveRight,
        MoveDown,
        Taunt,
        CallMedic,
        UseAbility,
        SwapWeaponsCustom,
        InteractWeapon,
        ChangeTeam,
        ChangeClass,
        ShowScoreboard,
        PushToTalk,
        VoteYes,
        VoteNo,
        OpenVoteMenu,
        ToggleConsole,
        OpenBubbleMenuZ,
        OpenBubbleMenuX,
        OpenBubbleMenuC,
        CustomBubble,
    }

    private enum ControllerControlsMenuBinding
    {
        Jump,
        PrimaryFire,
        SecondaryFire,
        UseAbility,
        Interact,
        SwapWeapon,
        Scoreboard,
        Pause,
        AimDistance,
        ChangeTeam,
        ChangeClass,
    }

    private const int ProcessedNetworkEventHistoryLimit = 4096;
    private readonly GameStartupMode _startupMode;
    private readonly ClientServiceContainer _services = new();
    private FrameController _frameController => _services.Get<FrameController>();
    private GameplayController _gameplayController => _services.Get<GameplayController>();
    private GameplayScreenStateController _gameplayScreenStateController => _services.Get<GameplayScreenStateController>();
    private GameplayPresentationStateController _gameplayPresentationStateController => _services.Get<GameplayPresentationStateController>();
    private GameplayImpactEffectsController _gameplayImpactEffectsController => _services.Get<GameplayImpactEffectsController>();
    private GameplayGoreEffectsController _gameplayGoreEffectsController => _services.Get<GameplayGoreEffectsController>();
    private GameplaySmokeEffectsController _gameplaySmokeEffectsController => _services.Get<GameplaySmokeEffectsController>();
    private GameplayMaterialEffectsController _gameplayMaterialEffectsController => _services.Get<GameplayMaterialEffectsController>();
    private GameplayVisualEventController _gameplayVisualEventController => _services.Get<GameplayVisualEventController>();
    private GameplayAudioMusicController _gameplayAudioMusicController => _services.Get<GameplayAudioMusicController>();
    private GameplayAudioEventController _gameplayAudioEventController => _services.Get<GameplayAudioEventController>();
    private GameplayRapidFireAudioController _gameplayRapidFireAudioController => _services.Get<GameplayRapidFireAudioController>();
    private GameplayLocalStatusHudController _gameplayLocalStatusHudController => _services.Get<GameplayLocalStatusHudController>();
    private GameplayMedicHudController _gameplayMedicHudController => _services.Get<GameplayMedicHudController>();
    private GameplayEngineerHudController _gameplayEngineerHudController => _services.Get<GameplayEngineerHudController>();
    private GameplayAimHudController _gameplayAimHudController => _services.Get<GameplayAimHudController>();
    private GameplayPlayerNameHudController _gameplayPlayerNameHudController => _services.Get<GameplayPlayerNameHudController>();
    private GameplayPlayerRenderController _gameplayPlayerRenderController => _services.Get<GameplayPlayerRenderController>();
    private GameplayDeadBodyRenderController _gameplayDeadBodyRenderController => _services.Get<GameplayDeadBodyRenderController>();
    private GameplayPlayerSpriteRenderController _gameplayPlayerSpriteRenderController => _services.Get<GameplayPlayerSpriteRenderController>();
    private GameplayWeaponRenderController _gameplayWeaponRenderController => _services.Get<GameplayWeaponRenderController>();
    private GameplayPlayerStatusEffectRenderController _gameplayPlayerStatusEffectRenderController => _services.Get<GameplayPlayerStatusEffectRenderController>();
    private GameplaySessionController _gameplaySessionController => _services.Get<GameplaySessionController>();
    private GameplayOverlayStateController _gameplayOverlayStateController => _services.Get<GameplayOverlayStateController>();
    private GameplayResetController _gameplayResetController => _services.Get<GameplayResetController>();
    private ClientPluginRuntimeController _clientPluginRuntimeController => _services.Get<ClientPluginRuntimeController>();
    private ClientPluginEventController _clientPluginEventController => _services.Get<ClientPluginEventController>();
    private ClientPluginUiBridgeController _clientPluginUiBridgeController => _services.Get<ClientPluginUiBridgeController>();
    private ClientPluginMarkerController _clientPluginMarkerController => _services.Get<ClientPluginMarkerController>();
    private MenuController _menuController => _services.Get<MenuController>();
    private AnimatedMenuBackgroundController _animatedMenuBackgroundController => _services.Get<AnimatedMenuBackgroundController>();
    private MenuBottomBarRunners _menuBottomBarRunners => _services.Get<MenuBottomBarRunners>();
    private ConnectionFlowController _connectionFlowController => _services.Get<ConnectionFlowController>();
    private MainMenuOverlayController _mainMenuOverlayController => _services.Get<MainMenuOverlayController>();
    private MainMenuOverlayStateController _mainMenuOverlayStateController => _services.Get<MainMenuOverlayStateController>();
    private HostSetupFlowController _hostSetupFlowController => _services.Get<HostSetupFlowController>();
    private WindowTextInputController _windowTextInputController => _services.Get<WindowTextInputController>();
    private MenuTextInputController _menuTextInputController => _services.Get<MenuTextInputController>();
    private NetworkPromptTextInputController _networkPromptTextInputController => _services.Get<NetworkPromptTextInputController>();
    private ChatTextInputController _chatTextInputController => _services.Get<ChatTextInputController>();
    private ConsoleTextInputController _consoleTextInputController => _services.Get<ConsoleTextInputController>();
    private BootstrapController _bootstrapController => _services.Get<BootstrapController>();
    private OptionsMenuController _optionsMenuController => _services.Get<OptionsMenuController>();
    private MainMenuPageController _mainMenuPageController => _services.Get<MainMenuPageController>();
    private PluginOptionsMenuController _pluginOptionsMenuController => _services.Get<PluginOptionsMenuController>();
    private ControlsMenuController _controlsMenuController => _services.Get<ControlsMenuController>();
    private InGameMenuController _inGameMenuController => _services.Get<InGameMenuController>();
    private DebugMenuController _debugMenuController => _services.Get<DebugMenuController>();
    private bool _debugMenuEnabled;
    private bool _debugMenuOpen;
    private bool _debugMenuAwaitingEscapeRelease;
    private int _debugMenuHoverIndex;
    private bool _debugRocketCollisionsEnabled;
    private GameplayOverlayController _gameplayOverlayController => _services.Get<GameplayOverlayController>();
    private LastToDieStatsDocument _lastToDieStats => _services.Get<LastToDieStatsDocument>();
    private readonly ClientIdentityDocument _clientIdentity;
    private readonly FriendListDocument _friendList;
    private readonly OpenGarrisonPresenceClient _presenceClient;
    private GraphicsDeviceManager _graphics => _services.Get<GraphicsDeviceManager>();
    private readonly bool _crtStartupForcedOff;
    private RenderTarget2D? _gameRenderTarget;
    private RenderTarget2D? _hudRenderTarget;
    private bool _hudOpacityCompositePending;
    private bool _deferDamageVignetteForHudOpacityComposite;
    private bool _damageVignetteCompositeDeferred;
    private float _activeHudElementOpacity = 1f;
    private bool _preLaunchSplashDismissed;
    private SimulationConfig _config = null!;
    private SimulationWorld _world = null!;
    private FixedStepSimulator _simulator = null!;
    private readonly NetworkGameClient _networkClient = new();
    private readonly GameMakerAssetManifest _assetManifest;
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private Effect _grayscaleEffect = null!;
    private Texture2D? _levelBackgroundFileTexture;
    private string? _levelBackgroundFileTexturePath;
    private string? _levelBackgroundFileFailedPath;
    private SimpleLevel? _levelBackgroundFileTextureLevel;
    private LoadedSpriteFrame? _menuBackgroundTexture;
    private string? _menuBackgroundTexturePath;
    private string? _menuBackgroundFailedPath;
    private string _menuBackgroundAttributionText = string.Empty;
    private SpriteFont _consoleFont = null!;
    private SpriteFont _menuFont = null!;
    private LoadedSpriteFrame? _menuBitmapFontTexture;
    private readonly Dictionary<char, MenuBitmapGlyph> _menuBitmapFontGlyphs = new();
    private int _menuBitmapFontLineHeight;
    private int _menuBitmapFontSpacing = 1;
    private LoadedSpriteFrame? _menuPlaqueTexture;
    private LoadedSpriteFrame? _menuPlaqueTallTexture;
    private LoadedSpriteFrame? _menuTextBoxTopTexture;
    private LoadedSpriteFrame? _menuTextBoxMiddleTexture;
    private LoadedSpriteFrame? _menuTextBoxBottomTexture;
    private LoadedSpriteFrame? _menuTextBoxSoloTexture;
    private LoadedSpriteFrame? _lastToDieMenuPlaqueTexture;
    private LoadedSpriteFrame? _lastToDieMenuTextBoxSoloTexture;
    private LoadedSpriteFrame? _gameplayLoadoutClassStripTexture;
    private LoadedSpriteFrame? _gameplayLoadoutClassSelectionTexture;
    private LoadedSpriteFrame? _gameplayLoadoutBackgroundBarTexture;
    private LoadedSpriteFrame? _gameplayLoadoutDescriptionBoardTexture;
    private LoadedSpriteFrame? _gameplayLoadoutSelectionAtlasTexture;
    private readonly List<LoadedSpriteFrame> _gameplayLoadoutSelectionAtlasChunks = [];
    private LoadedSpriteFrame? _gameplayLoadoutSelectionTexture;
    private LoadedSpriteFrame? _gameplayLoadoutScrollerTexture;
    private LoadedSpriteFrame? _gameplayLoadoutPageTexture;
    private LoadedSpriteFrame? _gameplayLoadoutBackButtonTexture;
    private LoadedSpriteFrame? _gameplayLoadoutHelmetTexture;
    private LoadedSpriteFrame? _gameplayLoadoutDogTagsTexture;
    private GameMakerRuntimeAssetCache _runtimeAssets = null!;
    private GameplayModAssetCache _gameplayModAssets = null!;
    private RotatedWeaponSpriteCache? _rotatedWeaponSprites;
    private ClientRuntimeComposition? _runtimeComposition;
    private readonly Dictionary<LoadedSpriteFrame, Rectangle> _spriteFontOpaqueBoundsCache = new();
    private KeyboardState _previousKeyboard;
    private KeyboardState _clientPluginPreviousKeyboard;
    private KeyboardState _clientPluginKeyboard;
    private readonly Dictionary<int, PlayerRenderState> _playerRenderStates = new();
    private readonly Dictionary<int, Vector2> _playerPreviousRenderPositions = new();
    private readonly Dictionary<int, double> _playerPreviousRenderSampleTimes = new();
    private readonly Random _visualRandom = new(1337);
    private bool _wasDeathCamActive;
    private bool _wasMatchEnded;
    private int _previousLocalDemoknightChargeTicks = PlayerEntity.ExperimentalDemoknightChargeMaxTicks;
    private readonly BuffBannerReadyCueTracker _localBuffBannerReadyCueTracker = new();
    private float _localBuffBannerReadyCueEchoSuppressionSeconds;
    private MouseState _previousMouse;
    // Draw code must use the same focus-sanitized mouse sample as Update. Reading
    // Mouse.GetState directly during Draw lets an inactive window click through.
    private MouseState _frameMouseState;
    private MouseState _frameRawMouseState;
    private Point _lastKnownMousePosition;
    private bool _suppressPrimaryFireUntilMouseRelease;
    private bool _suppressSecondaryFireUntilMouseRelease;
    private bool _autoFireActive;
    private Vector2 _respawnCameraCenter;
    private bool _respawnCameraDetached;
    private NoticeState? _notice;
    private bool _hadLocalSentry;
    private bool _wasCarryingIntel;
    private readonly Queue<QueuedPluginNotice> _queuedPluginNotices = new();
    private readonly HostSetupFormState _hostSetupState = new();
    private readonly PracticeSetupState _practiceSetupState = new();
    private readonly HostedServerConsoleState _hostedServerConsole = new();
    private HostedServerRuntimeController _hostedServerRuntime => _services.Get<HostedServerRuntimeController>();
    private bool _devMessageCheckStarted;
    private bool _devMessageCheckFinished;
    private Task<DevMessageFetchResult>? _devMessageFetchTask;
    private readonly Queue<DevMessagePopupState> _pendingDevMessagePopups = new();
    private DevMessagePopupState? _activeDevMessagePopup;
    private readonly Queue<string> _queuedReplayPaths = new();
    private string? _activeReplayPath;
    private bool _killCamEnabled = true;
    private bool _positionSmoothingEnabled = false;
    private bool _enablePrediction = true;
    private bool _cameraPanningEnabled = OpenGarrisonPreferencesDocument.DefaultCameraPanningEnabled;
    private float _smoothCameraMultiplier = ClientSettings.DefaultSmoothCameraMultiplier;
    private bool _hasSmoothCamera;
    private Vector2 _smoothCamera;
    private bool _hasGameplayCameraTopLeft;
    private Vector2 _gameplayCameraTopLeft;
    private int _gameplayCameraZoomIndex;
    private bool _gameplayWorldSpriteBatchActive;
    private string _lastGameplayWindowTitle = string.Empty;
    private DisplayModeKind _displayMode = OpenGarrisonPreferencesDocument.DefaultDisplayMode;
    private IngameResolutionKind _ingameResolution = OpenGarrisonPreferencesDocument.DefaultIngameResolution;
    private WindowSizeKind _windowSize = OpenGarrisonPreferencesDocument.DefaultWindowSize;
    private DisplayScaleModeKind _displayScaleMode = OpenGarrisonPreferencesDocument.DefaultDisplayScaleMode;
    private Point? _lastWindowedPosition;
    private int _particleMode;
    private int _flameRenderMode;
    private int _bloodRenderMode;
    private bool _dynamicRagdollEnabled = true;
    private int _bloodPersistenceSeconds = OpenGarrisonPreferencesDocument.DefaultBloodPersistenceSeconds;
    private int _corpseFadeMode = OpenGarrisonPreferencesDocument.DefaultCorpseFadeMode;
    private MenuBackgroundMode _menuBackgroundMode = MenuBackgroundMode.DefaultMaps;
    private int _gibLevel = 3;
    private int _bloodAmountLevel = 5;
    private int _corpseDurationMode;
    private int _frameRateLimit;
    private long _lastDrawTimestamp;
    private bool _healerRadarEnabled = true;
    private bool _showHealerEnabled = true;
    private bool _showHealingEnabled = true;
    private bool _showHealthBarEnabled;
    private bool _showShieldBarEnabled = true;
    private bool _hudShowOnlyActiveWeapon;
    private bool _overheadChatEnabled = OpenGarrisonPreferencesDocument.DefaultOverheadChatEnabled;
    private BubbleWheelBehavior _bubbleWheelBehavior = OpenGarrisonPreferencesDocument.DefaultBubbleWheelBehavior;
    private DateTime _bubbleWheelPluginConfigLastWriteUtc;
    private bool _portraitRumbleEnabled = true;
    private bool _postGameMvpArtEnabled;
    private float _portraitRumbleRemainingSeconds;
    private float _portraitRumbleIntensity;
    private int _portraitRumbleSeed;
    private bool _damageVignetteEnabled = true;
    private int _damageVignetteIntensityPercent = ClientSettings.DefaultDamageVignetteIntensityPercent;
    private LowHealthColorMode _lowHealthColorMode = LowHealthColorMode.Red;
    private float _damageVignetteIntensity;
    private float _damageVignetteFlashIntensity;
    private readonly Dictionary<int, Texture2D> _damageVignetteTexturesByBucket = new();
    private int _damageVignetteTextureWidth;
    private int _damageVignetteTextureHeight;
    private bool _showPersistentSelfNameEnabled;
    private bool _showPlayerNamesEnabled = true;
    private bool _spriteDropShadowEnabled;
    private bool _stuckArrowsEnabled = true;
    private bool _pixelPerfectWeaponRotation = true;
    private bool _useLocalWeaponRotation = false;
    private int _playerCardSizeMode = ClientSettings.PlayerCardSizeSmall;
    private int _cursorSizePercent = ClientSettings.DefaultCursorSizePercent;
    private bool _uberOutlineEnabled = true;
    private bool _projectileTeamTintEnabled = true;
    private bool _wasWindowActive = true;
    private bool _windowInputActive = true;
    private readonly WindowInputFilter _windowInputFilter = new();
    private bool _suppressFullscreenToggleUntilRelease;
    private int _menuImageFrame;
    private readonly List<ChatLine> _chatLines = new();
    private OverheadChatMessage? _localOverheadChatMessage;
    private readonly Dictionary<byte, OverheadChatMessage> _overheadChatMessagesBySlot = new();
    private readonly List<byte> _staleOverheadChatSlots = new();
    private readonly HashSet<string> _browserLoggedCriticalHudSpriteEvents = new(StringComparer.Ordinal);
    private ClientPluginOverlayMenuState? _clientPluginOverlayMenu;
    private int _browserDebugUpdateCount;
    private int _browserDebugDrawCount;
    private int _browserDebugMenuCount;
    private float _binocularsFocusX;
    private float _binocularsFocusY;
    private bool _wasBinocularsActive;
    private const float BinocularsMovementSpeed = 600f;
    // Local focus may run this far ahead of the server-echoed focus at full speed.
    // Beyond this, speed scales linearly to zero at BinocularsLocalAdvanceMaxDistance.
    private const float BinocularsLocalAdvanceSlowdownStart = 300f;
    // Local focus is fully stopped at this many pixels ahead of the server-echoed focus.
    private const float BinocularsLocalAdvanceMaxDistance = 400f;
    private Texture2D? _binocularOverlayMask;
    private int _binocularOverlayMaskWidth;
    private int _binocularOverlayMaskHeight;
    private int _browserHostLifecycleEnsureCallCount;

    public Game1(GameStartupMode startupMode = GameStartupMode.Client)
    {
        _startupMode = startupMode;
        var (frameController,
            gameplayController,
            gameplayScreenStateController,
            gameplayPresentationStateController,
            gameplayImpactEffectsController,
            gameplayGoreEffectsController,
            gameplaySmokeEffectsController,
            gameplayMaterialEffectsController,
            gameplayVisualEventController,
            gameplayAudioMusicController,
            gameplayAudioEventController,
            gameplayRapidFireAudioController,
            gameplayLocalStatusHudController,
            gameplayMedicHudController,
            gameplayEngineerHudController,
            gameplayAimHudController,
            gameplayPlayerNameHudController,
            gameplayPlayerRenderController,
            gameplayDeadBodyRenderController,
            gameplayPlayerSpriteRenderController,
            gameplayWeaponRenderController,
            gameplayPlayerStatusEffectRenderController,
            gameplaySessionController,
            gameplayOverlayStateController,
            gameplayResetController) = CreateGameplayControllerBundle(this);
        _services.Register(frameController);
        _services.Register(gameplayController);
        _services.Register(gameplayScreenStateController);
        _services.Register(gameplayPresentationStateController);
        _services.Register(gameplayImpactEffectsController);
        _services.Register(gameplayGoreEffectsController);
        _services.Register(gameplaySmokeEffectsController);
        _services.Register(gameplayMaterialEffectsController);
        _services.Register(gameplayVisualEventController);
        _services.Register(gameplayAudioMusicController);
        _services.Register(gameplayAudioEventController);
        _services.Register(gameplayRapidFireAudioController);
        _services.Register(gameplayLocalStatusHudController);
        _services.Register(gameplayMedicHudController);
        _services.Register(gameplayEngineerHudController);
        _services.Register(gameplayAimHudController);
        _services.Register(gameplayPlayerNameHudController);
        _services.Register(gameplayPlayerRenderController);
        _services.Register(gameplayDeadBodyRenderController);
        _services.Register(gameplayPlayerSpriteRenderController);
        _services.Register(gameplayWeaponRenderController);
        _services.Register(gameplayPlayerStatusEffectRenderController);
        _services.Register(gameplaySessionController);
        _services.Register(gameplayOverlayStateController);
        _services.Register(gameplayResetController);

        var (clientPluginRuntimeController,
            clientPluginEventController,
            clientPluginUiBridgeController,
            clientPluginMarkerController,
            menuController,
            connectionFlowController,
            mainMenuOverlayController,
            mainMenuOverlayStateController,
            hostSetupFlowController,
            windowTextInputController,
            menuTextInputController,
            networkPromptTextInputController,
            chatTextInputController,
            consoleTextInputController,
            bootstrapController,
            optionsMenuController,
            mainMenuPageController,
            pluginOptionsMenuController,
            controlsMenuController,
            inGameMenuController,
            debugMenuController,
            gameplayOverlayController,
            animatedMenuBackgroundController,
            menuBottomBarRunners) = CreateShellControllerBundle(this);
        _services.Register(clientPluginRuntimeController);
        _services.Register(clientPluginEventController);
        _services.Register(clientPluginUiBridgeController);
        _services.Register(clientPluginMarkerController);
        _services.Register(menuController);
        _services.Register(connectionFlowController);
        _services.Register(mainMenuOverlayController);
        _services.Register(mainMenuOverlayStateController);
        _services.Register(hostSetupFlowController);
        _services.Register(windowTextInputController);
        _services.Register(menuTextInputController);
        _services.Register(networkPromptTextInputController);
        _services.Register(chatTextInputController);
        _services.Register(consoleTextInputController);
        _services.Register(bootstrapController);
        _services.Register(optionsMenuController);
        _services.Register(mainMenuPageController);
        _services.Register(pluginOptionsMenuController);
        _services.Register(controlsMenuController);
        _services.Register(inGameMenuController);
        _services.Register(debugMenuController);
        _services.Register(gameplayOverlayController);
        _services.Register(animatedMenuBackgroundController);
        _services.Register(menuBottomBarRunners);

        var (clientSettings,
            inputBindings,
            lastToDieStats,
            hostedServerRuntime,
            graphics) = CreateRuntimeServices(this, _hostedServerConsole);
        _services.Register(clientSettings);
        _services.Register(inputBindings);
        _services.Register(lastToDieStats);
        _services.Register(hostedServerRuntime);
        _services.Register(graphics);
        _clientIdentity = ClientIdentityDocument.LoadOrCreate();
        _lastDirectMessageId = Math.Max(0L, _clientIdentity.LastDirectMessageId);
        _directMessagesInitialPollCompleted = _clientIdentity.DirectMessageCursorInitialized;
        _friendList = FriendListDocument.Load();
        _presenceClient = new OpenGarrisonPresenceClient();
        _graphics.HardwareModeSwitch = false;
        _crtStartupForcedOff = !OperatingSystem.IsBrowser()
            && string.Equals(Environment.GetEnvironmentVariable("OPENGARRISON_CRT"), "off", StringComparison.OrdinalIgnoreCase);
        var forceReach = !OperatingSystem.IsBrowser()
            && string.Equals(Environment.GetEnvironmentVariable("OPENGARRISON_FORCE_REACH"), "1", StringComparison.Ordinal);
        var forceHighDef = OperatingSystem.IsBrowser()
            || string.Equals(Environment.GetEnvironmentVariable("OPENGARRISON_FORCE_HIGHDEF"), "1", StringComparison.Ordinal);
        // Desktop HiDef is the intended profile for the optional presentation
        // resources. OPENGARRISON_FORCE_REACH remains available as a startup
        // recovery switch for machines where HiDef device creation fails;
        // OPENGARRISON_FORCE_HIGHDEF remains a supported diagnostic override.
        _graphics.GraphicsProfile = forceReach ? GraphicsProfile.Reach : GraphicsProfile.HiDef;
        Console.WriteLine($"Graphics profile selected: {_graphics.GraphicsProfile} (forceReach={forceReach}, forceHighDef={forceHighDef})");
        Content.RootDirectory = "Content";
        // ContentRoot also drives stock-map source selection. Resolve it from
        // the executable, because launching OG2 from a different working
        // directory otherwise selects the sparse source PNG over packaged art.
        ClientRuntimeBootstrap.InitializeContentRoot(OperatingSystem.IsBrowser()
            ? Content.RootDirectory
            : Path.Combine(AppContext.BaseDirectory, Content.RootDirectory));
        InitializeLocalDistributionAtlasManifestsIfPresent();
        IsMouseVisible = false;
        ApplyDisplayMode(_clientSettings.DisplayMode);
        ApplyIngameResolution(_clientSettings.IngameResolution);
        ApplyWindowSize(_clientSettings.WindowSize);
        ApplyPreferredBackBufferSize(_displayMode, _ingameResolution, _windowSize);

        ReinitializeSimulationForTickRate(SimulationConfig.DefaultTicksPerSecond);
        _assetManifest = OperatingSystem.IsBrowser()
            ? ClientRuntimeBootstrap.GetBrowserRuntimeAssetManifest() ?? GameMakerAssetManifestImporter.ImportProjectAssets()
            : GameMakerRuntimeAssetManifestLoader.LoadPackagedOrProjectAssets();
        StartBrowserBootstrapAssetPreloadIfNeeded();
        ApplyLoadedSettings();
        ApplyLoadedCustomBubbleSettings();
        LoadHudLayout();

        if (OperatingSystem.IsBrowser())
        {
            IsFixedTimeStep = false;
            InactiveSleepTime = TimeSpan.Zero;
        }
        else
        {
            IsFixedTimeStep = false;
            TargetElapsedTime = TimeSpan.FromSeconds(1d / ClientUpdateTicksPerSecond);
            InactiveSleepTime = TimeSpan.Zero;
        }
    }

    protected override void Initialize()
    {
        _bootstrapController.Initialize();
        base.Initialize();

        if (!OperatingSystem.IsBrowser())
        {
            Window.AllowUserResizing = IsUserResizableDisplayMode(_displayMode);
        }

        // Subscribe to game exit event to ensure proper server disconnection
        Exiting += OnGameExiting;
        Activated += OnGameActivated;
        Deactivated += OnGameDeactivated;
    }

    private void OnGameActivated(object? sender, EventArgs e)
    {
        _windowInputActive = true;
        _windowInputFilter.LoseFocus();
        // Rebase the edge detector on the first active frame. A button held while
        // another window was focused must not become a new click on refocus.
        _wasWindowActive = false;
    }

    private void OnGameDeactivated(object? sender, EventArgs e)
    {
        _voiceChat?.SuspendCapture();
        _windowInputActive = false;
        HandleWindowFocusLost(default);
    }

    private void OnGameExiting(object? sender, EventArgs e)
    {
        PreserveGarrisonBuilderOnExit();
        ResetVoiceChat();
        // Ensure we disconnect from the server before exiting
        // This sends a proper close message (WebSocket close frame or UDP socket closure)
        // so the server can immediately remove the player instead of waiting for timeout
        SendSocialPresenceOffline();
        _networkClient.SendLastToDieLeave();
        _networkClient.Disconnect();
        // Last to Die and other local hosted sessions own a hidden server
        // process. Stop it while the runtime controller is still alive; relying
        // on Game.Dispose/UnloadContent is not sufficient on every fatal-exit
        // path.
        StopHostedServer();
    }

    public void EnsureBrowserHostLifecycleInitialized()
    {
        if (!OperatingSystem.IsBrowser())
        {
            return;
        }

        _browserHostLifecycleEnsureCallCount += 1;
        _bootstrapController.Initialize();
        _bootstrapController.LoadContent();
    }

    protected override void LoadContent()
    {
        _bootstrapController.LoadContent();
    }

    protected override void UnloadContent()
    {
        _voiceChat?.Dispose();
        _voiceChat = null;
        _runUploads?.Dispose();
        _runUploads = null;
        ShutdownDiscordRichPresence();
        _bootstrapController.UnloadContent();
        base.UnloadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        var browserUpdateStartTimestamp = ShouldMeasureClientPerformanceDurations() ? Stopwatch.GetTimestamp() : 0L;
        LogBrowserFrameState("update", ref _browserDebugUpdateCount, gameTime);
        PollBrowserBootstrapAssetPreload();
        _bootstrapController.AdvanceDeferredContentBootstrap();
        BeginNetworkDiagnosticsFrame(gameTime);
        BeginClientPerformanceDiagnosticsFrame(gameTime);
        _networkInterpolationClockSeconds = _networkInterpolationClock.Elapsed.TotalSeconds;
        var clientTicks = _frameController.Update(gameTime);
        UpdateVoiceChat(_clientPluginKeyboard, _frameMouseState, _wasWindowActive);
        PumpDiscordRichPresence(gameTime.ElapsedGameTime.TotalSeconds);
        PumpSocialPresence(gameTime.ElapsedGameTime.TotalSeconds);
        PumpManagedRoomOperation();
        NotifyClientPluginsFrame(gameTime, clientTicks);
        AdvanceClientPerformanceAutomation();
        FinalizeNetworkDiagnosticsFrame();

        base.Update(gameTime);
        RecordBrowserUpdateDuration(browserUpdateStartTimestamp);
        FinalizeClientPerformanceDiagnosticsFrame();
    }

    protected override void Draw(GameTime gameTime)
    {
        var browserDrawStartTimestamp = ShouldMeasureClientPerformanceDurations() ? Stopwatch.GetTimestamp() : 0L;
        LogBrowserFrameState("draw", ref _browserDebugDrawCount, gameTime);
        // Use interpolation clock value from Update() - don't re-sample during Draw()
        ApplyFrameRateLimit();
        GraphicsDevice.Clear(new Color(24, 32, 48));
        _frameController.Draw(gameTime);

        base.Draw(gameTime);
        RecordBrowserDrawDuration(browserDrawStartTimestamp);

        if (!_preLaunchSplashDismissed)
        {
            _preLaunchSplashDismissed = true;
            PreLaunchSplash.Close();
        }
    }

    private void LogBrowserFrameState(string phase, ref int counter, GameTime gameTime)
    {
        if (!OperatingSystem.IsBrowser() || counter >= 8)
        {
            return;
        }

        counter += 1;
        Console.WriteLine(
            $"Browser frame {phase} #{counter}: startupSplash={_startupSplashOpen} mainMenu={_mainMenuOpen} bootstrapComplete={_bootstrapController.IsContentBootstrapComplete} elapsed={gameTime.ElapsedGameTime.TotalMilliseconds:0.##}ms");
    }

    private void ApplyFrameRateLimit()
    {
        if (OperatingSystem.IsBrowser() || _frameRateLimit <= 0)
        {
            _lastDrawTimestamp = Stopwatch.GetTimestamp();
            return;
        }

        var currentTimestamp = Stopwatch.GetTimestamp();
        if (_lastDrawTimestamp == 0)
        {
            _lastDrawTimestamp = currentTimestamp;
            return;
        }

        var elapsedSeconds = (currentTimestamp - _lastDrawTimestamp) / (double)Stopwatch.Frequency;
        var targetSeconds = 1d / _frameRateLimit;
        if (elapsedSeconds < targetSeconds)
        {
            var sleepMilliseconds = (int)Math.Floor((targetSeconds - elapsedSeconds) * 1000d);
            if (sleepMilliseconds > 0)
            {
                Thread.Sleep(sleepMilliseconds);
            }

            while ((Stopwatch.GetTimestamp() - _lastDrawTimestamp) / (double)Stopwatch.Frequency < targetSeconds)
            {
                Thread.Sleep(0);
            }
        }

        _lastDrawTimestamp = Stopwatch.GetTimestamp();
    }

    private void LogBrowserMenuState(int buttonCount)
    {
        if (!OperatingSystem.IsBrowser() || _browserDebugMenuCount >= 6)
        {
            return;
        }

        _browserDebugMenuCount += 1;
        Console.WriteLine(
            $"Browser menu draw #{_browserDebugMenuCount}: page={_mainMenuPage} overlay={GetActiveMainMenuOverlay()} buttons={buttonCount} plaque={_menuPlaqueTexture is not null} solo={_menuTextBoxSoloTexture is not null} bitmapFont={_menuBitmapFontTexture is not null && _menuBitmapFontGlyphs.Count > 0} menuFontLineSpacing={_menuFont.LineSpacing}");
    }

    private void DrawGameplayWorldForCamera(Vector2 cameraPosition, int viewportWidth, int viewportHeight, int? skippedDeadBodySourcePlayerId = null)
    {
        _frameController.DrawGameplayWorldForCamera(cameraPosition, viewportWidth, viewportHeight, skippedDeadBodySourcePlayerId);
    }

    private static KeyboardState GetCurrentKeyboardState()
    {
        return OperatingSystem.IsBrowser()
            ? BrowserInputBridge.GetKeyboardState()
            : Keyboard.GetState();
    }

    private static MouseState GetCurrentMouseState()
    {
        return OperatingSystem.IsBrowser()
            ? BrowserInputBridge.GetMouseState()
            : Mouse.GetState();
    }

    internal MouseState GetFrameMouseState()
    {
        return _frameMouseState;
    }

    internal MouseState GetFrameRawMouseState()
    {
        return _frameRawMouseState;
    }

    internal bool IsWindowInputActive => OperatingSystem.IsBrowser()
        ? BrowserInputBridge.IsFocused
        : _windowInputActive && IsActive && DesktopInputFocus.IsCurrentProcessForeground();

    internal static bool ShouldDeferFullscreenToggle(
        bool startupSplashOpen,
        bool loadingOverlayVisible,
        bool contentBootstrapComplete,
        bool loadingPresentationPending)
    {
        return startupSplashOpen
            || loadingOverlayVisible
            || !contentBootstrapComplete
            || loadingPresentationPending;
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


    private void CloseGameplayLoadoutMenu()
    {
        _gameplayLoadoutMenuOpen = false;
        _gameplayLoadoutMenuAwaitingEscapeRelease = false;
        _gameplayLoadoutMenuHoverIndex = -1;
        _gameplayLoadoutMenuViewedClass = _world.LocalPlayer.ClassId;
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
        public ChatLine(string playerName, string text, byte team, bool teamOnly, bool directMessage = false, byte playerSlot = 0)
        {
            PlayerName = playerName;
            Text = text;
            Team = team;
            TeamOnly = teamOnly;
            DirectMessage = directMessage;
            PlayerSlot = playerSlot;
            TicksRemaining = 600;
        }

        public string PlayerName { get; }

        public string Text { get; }

        public byte Team { get; }

        public bool TeamOnly { get; }

        public bool DirectMessage { get; }

        public byte PlayerSlot { get; }

        public int TicksRemaining { get; set; }
    }

    private sealed class OverheadChatMessage(string text, bool teamOnly, int ticksRemaining)
    {
        public string Text { get; } = text;

        public bool TeamOnly { get; } = teamOnly;

        public int TicksRemaining { get; set; } = ticksRemaining;
    }

    private sealed class ClientPluginOverlayMenuState(
        string pluginId,
        string title,
        string subtitle,
        string breadcrumb,
        IReadOnlyList<string> entries)
    {
        public string PluginId { get; } = pluginId;

        public string Title { get; } = title;

        public string Subtitle { get; } = subtitle;

        public string Breadcrumb { get; } = breadcrumb;

        public IReadOnlyList<string> Entries { get; } = entries;
    }

    private sealed class PracticeMapEntry
    {
        public PracticeMapEntry(string levelName, string displayName, GameModeKind mode, bool isCustomMap, string? iniKey = null)
        {
            LevelName = levelName;
            DisplayName = displayName;
            Mode = mode;
            IsCustomMap = isCustomMap;
            IniKey = iniKey ?? levelName;
        }

        public string LevelName { get; }

        public string DisplayName { get; }

        public string IniKey { get; }

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
