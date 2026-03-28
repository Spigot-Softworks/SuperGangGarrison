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
    }

    private enum ControlsMenuBinding
    {
        MoveUp,
        MoveLeft,
        MoveRight,
        MoveDown,
        Taunt,
        ChangeTeam,
        ChangeClass,
        ShowScoreboard,
        ToggleConsole,
    }

    private const int ProcessedNetworkEventHistoryLimit = 4096;
    private readonly GameStartupMode _startupMode;
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
    private SpriteFont _consoleFont = null!;
    private GameMakerRuntimeAssetCache _runtimeAssets = null!;
    private readonly Dictionary<Texture2D, Rectangle> _spriteFontOpaqueBoundsCache = new();
    private KeyboardState _previousKeyboard;
    private readonly Dictionary<int, PlayerRenderState> _playerRenderStates = new();
    private readonly Dictionary<int, Vector2> _playerPreviousRenderPositions = new();
    private readonly Dictionary<int, double> _playerPreviousRenderSampleTimes = new();
    private int? _localPlayerSnapshotEntityId;
    private readonly Random _visualRandom = new(1337);
    private bool _wasLocalPlayerAlive = true;
    private bool _wasDeathCamActive;
    private bool _wasMatchEnded;
    private string _observedGameplayLevelName = string.Empty;
    private int _observedGameplayMapAreaIndex = -1;
    private MouseState _previousMouse;
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
    private bool _buildMenuOpen;
    private bool _buildMenuClosing;
    private float _buildMenuAlpha = 0.01f;
    private float _buildMenuX = -37f;
    private NoticeState? _notice;
    private bool _hadLocalSentry;
    private bool _wasCarryingIntel;
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
    private int _mainMenuHoverIndex = -1;
    private int _optionsHoverIndex = -1;
    private int _pluginOptionsHoverIndex = -1;
    private int _controlsHoverIndex = -1;
    private int _lobbyBrowserHoverIndex = -1;
    private int _lobbyBrowserSelectedIndex = -1;
    private int _practiceMapIndex;
    private int _inGameMenuHoverIndex = -1;
    private List<PracticeMapEntry> _practiceMapEntries = new();
    private GameplaySessionKind _gameplaySessionKind;
    private readonly HostSetupFormState _hostSetupState = new();
    private readonly HostedServerConsoleState _hostedServerConsole = new();
    private readonly HostedServerRuntimeController _hostedServerRuntime;
    private string _playerNameEditBuffer = string.Empty;
    private string _connectHostBuffer = "127.0.0.1";
    private string _connectPortBuffer = "8190";
    private string _menuStatusMessage = string.Empty;
    private int _practiceTickRate = SimulationConfig.DefaultTicksPerSecond;
    private int _practiceTimeLimitMinutes = 15;
    private int _practiceCapLimit = 5;
    private int _practiceRespawnSeconds = 5;
    private int _practiceEnemyBotCount;
    private int _practiceFriendlyBotCount;
    private bool _practiceEnemyDummyEnabled = true;
    private bool _practiceFriendlyDummyEnabled;
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
    private bool _wasWindowActive = true;
    private int _menuImageFrame;
    private ControlsMenuBinding? _pendingControlsBinding;
    private readonly List<ChatLine> _chatLines = new();

    public Game1(GameStartupMode startupMode = GameStartupMode.Client)
    {
        _startupMode = startupMode;
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
            ? $"OpenGarrison.ServerLauncher - Proto (Protocol v{ProtocolVersion.Current})"
            : $"OpenGarrison.Client - Proto (Protocol v{ProtocolVersion.Current})";
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
        _runtimeAssets = new GameMakerRuntimeAssetCache(GraphicsDevice, _assetManifest);
        _spriteFontOpaqueBoundsCache.Clear();
        LoadMenuMusic();
        LoadFaucetMusic();
        LoadIngameMusic();
        ApplyAudioMuteState();
        AddConsoleLine($"gm assets sprites={_assetManifest.Sprites.Count} backgrounds={_assetManifest.Backgrounds.Count} sounds={_assetManifest.Sounds.Count}");
        NotifyClientPluginsStarted();
    }

    protected override void UnloadContent()
    {
        ShutdownClientPlugins();
        _menuMusicInstance?.Dispose();
        _menuMusic?.Dispose();
        _faucetMusicInstance?.Dispose();
        _faucetMusic?.Dispose();
        _ingameMusicInstance?.Dispose();
        _ingameMusic?.Dispose();
        StopHostedServer();
        _networkClient.Dispose();
        _runtimeAssets?.Dispose();
        _spriteFontOpaqueBoundsCache.Clear();
        _menuBackgroundTexture?.Dispose();
        _gameRenderTarget?.Dispose();
        _gameRenderTarget = null;
        _deathCamCaptureTarget?.Dispose();
        _deathCamCaptureTarget = null;
        _menuBackgroundTexture = null;
        _menuBackgroundTexturePath = null;
        PersistClientSettings();
        PersistInputBindings();
        base.UnloadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        BeginNetworkDiagnosticsFrame(gameTime);
        _networkInterpolationClockSeconds = _networkInterpolationClock.Elapsed.TotalSeconds;
        var clientTicks = ConsumeClientTickCount(gameTime);
        var windowActive = IsActive;
        var keyboard = windowActive ? Keyboard.GetState() : default;
        var mouse = windowActive ? GetScaledMouseState(GetConstrainedMouseState(Mouse.GetState())) : default;
        if (!_wasWindowActive && windowActive)
        {
            _previousKeyboard = keyboard;
            _previousMouse = mouse;
        }

        _wasWindowActive = windowActive;
        if (TryHandlePasswordPromptCancel(keyboard, mouse))
        {
            NotifyClientPluginsFrame(gameTime, clientTicks);
            FinalizeNetworkDiagnosticsFrame();
            base.Update(gameTime);
            return;
        }

        var muteAudioPressed = keyboard.IsKeyDown(Keys.F12) && !_previousKeyboard.IsKeyDown(Keys.F12);
        if (muteAudioPressed)
        {
            ToggleAudioMute();
        }

        var toggleConsolePressed = keyboard.IsKeyDown(_inputBindings.ToggleConsole) && !_previousKeyboard.IsKeyDown(_inputBindings.ToggleConsole);
        if (toggleConsolePressed && !_mainMenuOpen)
        {
            _consoleOpen = !_consoleOpen;
        }

        if (TryUpdateNonGameplayFrame(gameTime, keyboard, mouse, clientTicks))
        {
            NotifyClientPluginsFrame(gameTime, clientTicks);
            FinalizeNetworkDiagnosticsFrame();
            base.Update(gameTime);
            return;
        }

        UpdateGameplayFrame(gameTime, keyboard, mouse, clientTicks);
        NotifyClientPluginsFrame(gameTime, clientTicks);
        FinalizeNetworkDiagnosticsFrame();

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _networkInterpolationClockSeconds = _networkInterpolationClock.Elapsed.TotalSeconds;
        GraphicsDevice.Clear(new Color(24, 32, 48));

        if (TryDrawNonGameplayFrame())
        {
            base.Draw(gameTime);
            return;
        }
        DrawGameplayFrame(gameTime);

        base.Draw(gameTime);
    }
















    private sealed class NoticeState
    {
        public NoticeState(NoticeKind kind, float alpha, bool done, int ticksRemaining)
        {
            Kind = kind;
            Alpha = alpha;
            Done = done;
            TicksRemaining = ticksRemaining;
        }

        public NoticeKind Kind { get; set; }

        public float Alpha { get; set; }

        public bool Done { get; set; }

        public int TicksRemaining { get; set; }
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
