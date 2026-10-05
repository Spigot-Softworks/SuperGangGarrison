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
    public const string WindowTitle = "OpenGarrison";
#else
    public const string WindowTitle = "Super Gang Garrison";
#endif

    public enum BubbleMenuKind
    {
        None,
        Z,
        X,
        C,
        Custom,
    }

    public enum NoticeKind
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

    public enum HostSetupEditField
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

    public enum PracticeEditField
    {
        None,
        MapNameFilter,
    }

    public enum HostSetupTab
    {
        Settings,
        ServerConsole,
    }

    public enum GameplaySessionKind
    {
        None,
        Online,
        Practice,
        LastToDie,
        Jump,
    }

    public enum MainMenuPage
    {
        Root,
        PlayOnline,
        PlayOffline,
    }

    public enum ControlsMenuBinding
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

    public enum ControllerControlsMenuBinding
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

    public const int ProcessedNetworkEventHistoryLimit = 4096;
    public readonly GameStartupMode _startupMode;
    public readonly ClientServiceContainer _services = new();
    private GameplayManager _gameplayManager => _services.Get<GameplayManager>();
    private GameplayLoadoutResources _gameplayLoadoutResources => _gameplayManager.Bootstrap.GameplayLoadoutResources;
    private MenuResources _menuResources => _gameplayManager.Bootstrap.MenuResources;
    private AudioManager _audioManager => _services.Get<AudioManager>();
    private HudManager _hudManager => _services.Get<HudManager>();
    private SessionManager _sessionManager => _services.Get<SessionManager>();
    private HostingManager _hostingManager => _services.Get<HostingManager>();
    private PluginManager _pluginManager => _services.Get<PluginManager>();
    private MenuManager _menuManager => _services.Get<MenuManager>();
    private InputManager _inputManager => _services.Get<InputManager>();
    private GameplayPlayerRenderController _gameplayPlayerRenderController => _services.Get<GameplayPlayerRenderController>();
    private GameplayDeadBodyRenderController _gameplayDeadBodyRenderController => _services.Get<GameplayDeadBodyRenderController>();
    private GameplayPlayerSpriteRenderController _gameplayPlayerSpriteRenderController => _services.Get<GameplayPlayerSpriteRenderController>();
    private GameplayWeaponRenderController _gameplayWeaponRenderController => _services.Get<GameplayWeaponRenderController>();
    private GameplayPlayerStatusEffectRenderController _gameplayPlayerStatusEffectRenderController => _services.Get<GameplayPlayerStatusEffectRenderController>();
    private RenderPipeline _renderPipeline => _services.Get<RenderPipeline>();
    public bool _debugMenuEnabled;
    public bool _debugMenuOpen;
    public bool _debugRocketCollisionsEnabled;
    public LastToDieStatsDocument _lastToDieStats => _services.Get<LastToDieStatsDocument>();
    public readonly ClientIdentityDocument _clientIdentity;
    public readonly FriendListDocument _friendList;
    public readonly OpenGarrisonPresenceClient _presenceClient;
    public GraphicsDeviceManager _graphics => _services.Get<GraphicsDeviceManager>();
    public readonly bool _crtStartupForcedOff;
    private RenderTargetResources _renderTargetResources => _gameplayManager.Bootstrap.RenderTargetResources;
    public bool _hudOpacityCompositePending;
    public bool _deferDamageVignetteForHudOpacityComposite;
    public bool _damageVignetteCompositeDeferred;
    public float _activeHudElementOpacity = 1f;
    public SimulationConfig _config = null!;
    public SimulationWorld _world = null!;
    public FixedStepSimulator _simulator = null!;
    public readonly NetworkGameClient _networkClient = new();
    public readonly GameMakerAssetManifest _assetManifest;
    public SpriteBatch _spriteBatch = null!;
    public Texture2D _pixel = null!;
    public Effect _grayscaleEffect = null!;
    private LevelBackgroundResources _levelBackgroundResources => _gameplayManager.Bootstrap.LevelBackgroundResources;
    public SpriteFont _consoleFont = null!;
    public SpriteFont _menuFont = null!;
    public GameMakerRuntimeAssetCache _runtimeAssets = null!;
    public GameplayModAssetCache _gameplayModAssets = null!;
    public RotatedWeaponSpriteCache? _rotatedWeaponSprites;
    private SpriteFrameCacheResources _spriteFrameCacheResources => _gameplayManager.Bootstrap.SpriteFrameCacheResources;
    public KeyboardState _previousKeyboard;
    public readonly Dictionary<int, PlayerRenderState> _playerRenderStates = new();
    public readonly Dictionary<int, Vector2> _playerPreviousRenderPositions = new();
    public readonly Dictionary<int, double> _playerPreviousRenderSampleTimes = new();
    public readonly Random _visualRandom = new(1337);
    public bool _wasDeathCamActive;
    public bool _wasMatchEnded;
    public MouseState _previousMouse;
    public Point _lastKnownMousePosition;
    public bool _suppressPrimaryFireUntilMouseRelease;
    public bool _suppressSecondaryFireUntilMouseRelease;
    public bool _autoFireActive;
    public Vector2 _respawnCameraCenter;
    public bool _respawnCameraDetached;
    public NoticeState? _notice;
    public bool _hadLocalSentry;
    public bool _wasCarryingIntel;
    public readonly Queue<QueuedPluginNotice> _queuedPluginNotices = new();
    public readonly HostSetupFormState _hostSetupState = new();
    public readonly PracticeSetupState _practiceSetupState = new();
    public readonly HostedServerConsoleState _hostedServerConsole = new();
    public HostedServerRuntimeController _hostedServerRuntime => _services.Get<HostedServerRuntimeController>();
    private bool _devMessageCheckStarted;
    private bool _devMessageCheckFinished;
    private Task<DevMessageFetchResult>? _devMessageFetchTask;
    private readonly Queue<DevMessagePopupState> _pendingDevMessagePopups = new();
    private DevMessagePopupState? _activeDevMessagePopup;
    private readonly Queue<string> _queuedReplayPaths = new();
    public string? _activeReplayPath;
    private bool _hasSmoothCamera;
    private Vector2 _smoothCamera;
    public bool _hasGameplayCameraTopLeft;
    public Vector2 _gameplayCameraTopLeft;
    private int _gameplayCameraZoomIndex;
    private bool _gameplayWorldSpriteBatchActive;
    private Point? _lastWindowedPosition;
    private long _lastDrawTimestamp;
    private BubbleWheelBehavior _bubbleWheelBehavior = OpenGarrisonPreferencesDocument.DefaultBubbleWheelBehavior;
    private DateTime _bubbleWheelPluginConfigLastWriteUtc;
    private string? _bubbleWheelPluginConfigPath;
    private string? _bubbleWheelPluginConfigPathUserDataRootOverride;
    private readonly Dictionary<int, Texture2D> _damageVignetteTexturesByBucket = new();
    private int _damageVignetteTextureWidth;
    private int _damageVignetteTextureHeight;
    private bool _windowInputActive = true;
    private readonly List<ChatLine> _chatLines = new();
    public OverheadChatMessage? _localOverheadChatMessage;
    public readonly Dictionary<byte, OverheadChatMessage> _overheadChatMessagesBySlot = new();
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
        _services.Register(new GameplayManager(this));
        _services.Register(new AudioManager(this));
        _services.Register(new HudManager(this));
        _services.Register(new GameplayPlayerRenderController(this));
        _services.Register(new GameplayDeadBodyRenderController(this));
        _services.Register(new GameplayPlayerSpriteRenderController(this));
        _services.Register(new GameplayWeaponRenderController(this));
        _services.Register(new GameplayPlayerStatusEffectRenderController(this));
        _services.Register(new RenderPipeline(this));
        _services.Register(new SessionManager(this));
        _services.Register(new PluginManager(this));
        _services.Register(new MenuManager(this));
        _services.Register(new HostingManager(this));
        _services.Register(new InputManager(this));

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
        ApplyPreferredBackBufferSize(_menuManager.DisplaySettings.DisplayMode, _menuManager.DisplaySettings.IngameResolution, _menuManager.DisplaySettings.WindowSize);

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
        _gameplayManager.Bootstrap.Initialize();
        base.Initialize();

        if (!OperatingSystem.IsBrowser())
        {
            Window.AllowUserResizing = IsUserResizableDisplayMode(_menuManager.DisplaySettings.DisplayMode);
        }

        // Subscribe to game exit event to ensure proper server disconnection
        Exiting += OnGameExiting;
        Activated += OnGameActivated;
        Deactivated += OnGameDeactivated;
    }

    private void OnGameActivated(object? sender, EventArgs e)
    {
        _windowInputActive = true;
        _gameplayManager.Frame.LoseFocus();
        // Rebase the edge detector on the first active frame. A button held while
        // another window was focused must not become a new click on refocus.
        _gameplayManager.Frame.RebaseWindowActivation();
    }

    private void OnGameDeactivated(object? sender, EventArgs e)
    {
        _voiceChat?.SuspendCapture();
        _windowInputActive = false;
        HandleWindowFocusLost(default);
    }

    private void OnGameExiting(object? sender, EventArgs e)
    {
        FlushClientFrameCapture();
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
        _gameplayManager.Bootstrap.Initialize();
        _gameplayManager.Bootstrap.LoadContent();
    }

    protected override void LoadContent()
    {
        _gameplayManager.Bootstrap.LoadContent();
    }

    protected override void UnloadContent()
    {
        FlushClientFrameCapture();
        _gameplayLightmap.Dispose();
        _builderLightmap.Dispose();
        _voiceChat?.Dispose();
        _voiceChat = null;
        _runUploads?.Dispose();
        _runUploads = null;
        _lobbyBrowserClient?.Dispose();
        _lobbyBrowserClient = null;
        ShutdownDiscordRichPresence();
        _gameplayManager.Bootstrap.UnloadContent();
        base.UnloadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        var browserUpdateStartTimestamp = ShouldMeasureClientPerformanceDurations() ? Stopwatch.GetTimestamp() : 0L;
        LogBrowserFrameState("update", ref _browserDebugUpdateCount, gameTime);
        PollBrowserBootstrapAssetPreload();
        _gameplayManager.Bootstrap.AdvanceDeferredContentBootstrap();
        BeginNetworkDiagnosticsFrame(gameTime);
        EnsureTextureUploadSyncPolicy();
        BeginClientPerformanceDiagnosticsFrame(gameTime);
        _networkInterpolationClockSeconds = _networkInterpolationClock.Elapsed.TotalSeconds;
        var clientTicks = _gameplayManager.Frame.Update(gameTime);
        UpdateVoiceChat(_gameplayManager.Frame.ClientPluginKeyboard, _gameplayManager.Frame.FrameMouseState, _gameplayManager.Frame.WasWindowActive);
        PumpDiscordRichPresence(gameTime.ElapsedGameTime.TotalSeconds);
        PumpSocialPresence(gameTime.ElapsedGameTime.TotalSeconds);
        PumpManagedRoomOperation();
        NotifyClientPluginsFrame(gameTime, clientTicks);
        AdvanceClientPerformanceAutomation();
        FinalizeNetworkDiagnosticsFrame();

        UpdateGameplayCursorConfinement();
        base.Update(gameTime);
        RecordBrowserUpdateDuration(browserUpdateStartTimestamp);
        FinalizeClientPerformanceDiagnosticsFrame();
    }

    protected override void Draw(GameTime gameTime)
    {
        _renderPipeline.Draw(gameTime);
        _completedDrawCount += 1;
    }

    protected override void EndDraw()
    {
        if (!ShouldCaptureClientFrameFrameworkEndDrawDuration())
        {
            base.EndDraw();
            return;
        }

        var startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            base.EndDraw();
        }
        finally
        {
            RecordClientFrameCaptureFrameworkEndDrawDuration(startTimestamp);
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
            $"Browser frame {phase} #{counter}: startupSplash={_startupSplashOpen} mainMenu={_mainMenuOpen} bootstrapComplete={_gameplayManager.Bootstrap.IsContentBootstrapComplete} elapsed={gameTime.ElapsedGameTime.TotalMilliseconds:0.##}ms");
    }

    private void ApplyFrameRateLimit()
    {
        if (OperatingSystem.IsBrowser()
            || _menuManager.DisplaySettings.FrameRateLimit <= 0
            || ShouldLetVSyncPaceFrames(_menuManager.DisplaySettings.FrameRateLimit))
        {
            _lastDrawTimestamp = Stopwatch.GetTimestamp();
            return;
        }

        var currentTimestamp = Stopwatch.GetTimestamp();
        var periodTicks = Math.Max(1L, (long)Math.Round(Stopwatch.Frequency / (double)_menuManager.DisplaySettings.FrameRateLimit));
        if (_lastDrawTimestamp == 0)
        {
            _lastDrawTimestamp = currentTimestamp;
            return;
        }

        // _lastDrawTimestamp is the previous frame's scheduled start. Pace to
        // fixed deadlines rather than "one period after the last wake-up":
        // the latter adds every oversleep to the next period, so the average
        // rate settles below the limit and beats against the display refresh
        // as a periodic repeated frame.
        var deadline = _lastDrawTimestamp + periodTicks;
        var remainingTicks = deadline - currentTimestamp;
        if (remainingTicks > 0)
        {
            // Sleep coarsely, leaving ~1.5 ms to spin so timer granularity
            // cannot carry the wake-up past the deadline.
            var sleepMilliseconds = (int)Math.Floor((remainingTicks * 1000d / Stopwatch.Frequency) - 1.5d);
            if (sleepMilliseconds > 0)
            {
                Thread.Sleep(sleepMilliseconds);
            }

            while (Stopwatch.GetTimestamp() < deadline)
            {
                Thread.Sleep(0);
            }

            _lastDrawTimestamp = deadline;
            return;
        }

        // Late frame: keep the schedule unless we have fallen a whole period
        // behind, in which case restart it from now instead of rushing frames.
        _lastDrawTimestamp = -remainingTicks >= periodTicks ? currentTimestamp : deadline;
    }

    /// <summary>
    /// With vsync on, a software cap at or above display rates only adds risk:
    /// a sleep deadline landing just after a vblank pushes that frame to the
    /// next one (a 33 ms hitch at 60 Hz). Let presentation pace those frames.
    /// </summary>
    private bool ShouldLetVSyncPaceFrames(int frameRateLimit)
        => frameRateLimit >= 60 && _graphics.SynchronizeWithVerticalRetrace;

    public void LogBrowserMenuState(int buttonCount)
    {
        if (!OperatingSystem.IsBrowser() || _browserDebugMenuCount >= 6)
        {
            return;
        }

        _browserDebugMenuCount += 1;
        Console.WriteLine(
            $"Browser menu draw #{_browserDebugMenuCount}: page={_mainMenuPage} overlay={GetActiveMainMenuOverlay()} buttons={buttonCount} plaque={_menuResources.PlaqueTexture is not null} solo={_menuResources.TextBoxSoloTexture is not null} bitmapFont={_menuResources.BitmapFontTexture is not null && _menuResources.BitmapFontGlyphs.Count > 0} menuFontLineSpacing={_menuFont.LineSpacing}");
    }

    private void DrawGameplayWorldForCamera(Vector2 cameraPosition, int viewportWidth, int viewportHeight, int? skippedDeadBodySourcePlayerId = null)
    {
        _gameplayManager.Frame.DrawGameplayWorldForCamera(cameraPosition, viewportWidth, viewportHeight, skippedDeadBodySourcePlayerId);
    }

    public static KeyboardState GetCurrentKeyboardState()
    {
        return OperatingSystem.IsBrowser()
            ? BrowserInputBridge.GetKeyboardState()
            : Keyboard.GetState();
    }

    public static MouseState GetCurrentMouseState()
    {
        return OperatingSystem.IsBrowser()
            ? BrowserInputBridge.GetMouseState()
            : Mouse.GetState();
    }

    internal MouseState GetFrameMouseState()
    {
        return _gameplayManager.Frame.FrameMouseState;
    }

    internal MouseState GetFrameRawMouseState()
    {
        return _gameplayManager.Frame.FrameRawMouseState;
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
        return _menuManager.Menu.GetActiveOverlay();
    }

    public void OpenOptionsMenu(bool fromGameplay)
    {
        _menuManager.OptionsMenu.OpenOptionsMenu(fromGameplay);
    }

    public void CloseOptionsMenu()
    {
        _menuManager.OptionsMenu.CloseOptionsMenu();
    }

    private void OpenPluginOptionsMenu(bool fromGameplay)
    {
        _menuManager.OptionsMenu.OpenPluginOptionsMenu(fromGameplay);
    }

    public void ClosePluginOptionsMenu()
    {
        _menuManager.OptionsMenu.ClosePluginOptionsMenu();
    }

    private void OpenControlsMenu(bool fromGameplay)
    {
        _menuManager.ControlsMenu.OpenControlsMenu(fromGameplay);
    }

    private void CloseControlsMenu()
    {
        _menuManager.ControlsMenu.CloseControlsMenu();
    }

    public void UpdateOptionsMenu(KeyboardState keyboard, MouseState mouse)
    {
        _menuManager.OptionsMenu.UpdateOptionsMenu(keyboard, mouse);
    }

    public void DrawOptionsMenu()
    {
        _menuManager.OptionsMenu.DrawOptionsMenu();
    }

    public void UpdatePluginOptionsMenu(KeyboardState keyboard, MouseState mouse)
    {
        _menuManager.PluginOptionsMenu.UpdatePluginOptionsMenu(keyboard, mouse);
    }

    public void DrawPluginOptionsMenu()
    {
        _menuManager.PluginOptionsMenu.DrawPluginOptionsMenu();
    }

    public bool HasClientPluginOptions()
    {
        return _menuManager.PluginOptionsMenu.HasClientPluginOptions();
    }

    public void UpdateControlsMenu(KeyboardState keyboard, MouseState mouse)
    {
        _menuManager.ControlsMenu.UpdateControlsMenu(keyboard, mouse);
    }

    public void DrawControlsMenu()
    {
        _menuManager.ControlsMenu.DrawControlsMenu();
    }

    public void OpenInGameMenu()
    {
        _menuManager.InGameMenu.OpenInGameMenu();
    }

    private void CloseInGameMenu()
    {
        _menuManager.InGameMenu.CloseInGameMenu();
    }

    public void UpdateInGameMenu(KeyboardState keyboard, MouseState mouse)
    {
        _menuManager.InGameMenu.UpdateInGameMenu(keyboard, mouse);
    }

    private void DrawInGameMenu()
    {
        _menuManager.InGameMenu.DrawInGameMenu();
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
        return _gameplayManager.Overlay.GetActiveOverlay();
    }

    public void UpdateGameplayMenuState(KeyboardState keyboard, MouseState mouse)
    {
        _gameplayManager.Overlay.Update(keyboard, mouse);
    }

    public void OpenMainMenuPage(MainMenuPage page)
    {
        _menuManager.MainMenuPage.OpenMainMenuPage(page);
    }

    public List<MenuPageButton> BuildMainMenuButtons()
    {
        return _menuManager.MainMenuPage.BuildMainMenuButtons();
    }

    public void DrawCurrentMainMenuPage(IReadOnlyList<MenuPageButton> buttons)
    {
        _menuManager.MainMenuPage.DrawCurrentMainMenuPage(buttons);
    }

    public void AddPluginMenuActions(List<MenuPageAction> actions, ClientPluginMenuLocation location, int insertIndex = -1)
    {
        _menuManager.MainMenuPage.AddPluginMenuActions(actions, location, insertIndex);
    }
















    public sealed class NoticeState
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

    public sealed class QueuedPluginNotice(string text, int ticksRemaining, bool playSound)
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

    public sealed class OverheadChatMessage(string text, bool teamOnly, int ticksRemaining)
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

    public sealed class PracticeMapEntry
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
