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
        RconPassword,
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
    private readonly GameplayMaterialEffectsController _gameplayMaterialEffectsController;
    private readonly GameplayVisualEventController _gameplayVisualEventController;
    private readonly GameplayAudioMusicController _gameplayAudioMusicController;
    private readonly GameplayAudioEventController _gameplayAudioEventController;
    private readonly GameplayRapidFireAudioController _gameplayRapidFireAudioController;
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
    private readonly ConnectionFlowController _connectionFlowController;
    private readonly MainMenuOverlayController _mainMenuOverlayController;
    private readonly MainMenuOverlayStateController _mainMenuOverlayStateController;
    private readonly HostSetupFlowController _hostSetupFlowController;
    private readonly WindowTextInputController _windowTextInputController;
    private readonly MenuTextInputController _menuTextInputController;
    private readonly NetworkPromptTextInputController _networkPromptTextInputController;
    private readonly ChatTextInputController _chatTextInputController;
    private readonly ConsoleTextInputController _consoleTextInputController;
    private readonly BootstrapController _bootstrapController;
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
    private string? _menuBackgroundFailedPath;
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
    private Texture2D? _gameplayLoadoutClassStripTexture;
    private Texture2D? _gameplayLoadoutClassSelectionTexture;
    private Texture2D? _gameplayLoadoutBackgroundBarTexture;
    private Texture2D? _gameplayLoadoutDescriptionBoardTexture;
    private Texture2D? _gameplayLoadoutSelectionAtlasTexture;
    private Texture2D? _gameplayLoadoutSelectionTexture;
    private Texture2D? _gameplayLoadoutScrollerTexture;
    private Texture2D? _gameplayLoadoutPageTexture;
    private Texture2D? _gameplayLoadoutBackButtonTexture;
    private GameMakerRuntimeAssetCache _runtimeAssets = null!;
    private GameplayModAssetCache _gameplayModAssets = null!;
    private readonly Dictionary<Texture2D, Rectangle> _spriteFontOpaqueBoundsCache = new();
    private KeyboardState _previousKeyboard;
    private KeyboardState _clientPluginPreviousKeyboard;
    private KeyboardState _clientPluginKeyboard;
    private readonly Dictionary<int, PlayerRenderState> _playerRenderStates = new();
    private readonly Dictionary<int, Vector2> _playerPreviousRenderPositions = new();
    private readonly Dictionary<int, double> _playerPreviousRenderSampleTimes = new();
    private readonly Random _visualRandom = new(1337);
    private bool _wasLocalPlayerAlive = true;
    private bool _wasDeathCamActive;
    private bool _wasMatchEnded;
    private int _previousLocalDemoknightChargeTicks = PlayerEntity.ExperimentalDemoknightChargeMaxTicks;
    private MouseState _previousMouse;
    private bool _suppressPrimaryFireUntilMouseRelease;
    private Vector2 _respawnCameraCenter;
    private bool _respawnCameraDetached;
    private NoticeState? _notice;
    private bool _hadLocalSentry;
    private bool _wasCarryingIntel;
    private readonly Queue<QueuedPluginNotice> _queuedPluginNotices = new();
    private readonly HostSetupFormState _hostSetupState = new();
    private readonly PracticeSetupState _practiceSetupState = new();
    private readonly HostedServerConsoleState _hostedServerConsole = new();
    private readonly HostedServerRuntimeController _hostedServerRuntime;
    private bool _devMessageCheckStarted;
    private bool _devMessageCheckFinished;
    private Task<DevMessageFetchResult>? _devMessageFetchTask;
    private readonly Queue<DevMessagePopupState> _pendingDevMessagePopups = new();
    private DevMessagePopupState? _activeDevMessagePopup;
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
    private readonly List<ChatLine> _chatLines = new();

    public Game1(GameStartupMode startupMode = GameStartupMode.Client)
    {
        _startupMode = startupMode;
        (_frameController,
            _gameplayController,
            _gameplayScreenStateController,
            _gameplayPresentationStateController,
            _gameplayImpactEffectsController,
            _gameplayGoreEffectsController,
            _gameplaySmokeEffectsController,
            _gameplayMaterialEffectsController,
            _gameplayVisualEventController,
            _gameplayAudioMusicController,
            _gameplayAudioEventController,
            _gameplayRapidFireAudioController,
            _gameplayLocalStatusHudController,
            _gameplayMedicHudController,
            _gameplayEngineerHudController,
            _gameplayAimHudController,
            _gameplayPlayerNameHudController,
            _gameplayPlayerRenderController,
            _gameplayDeadBodyRenderController,
            _gameplayPlayerSpriteRenderController,
            _gameplayWeaponRenderController,
            _gameplayPlayerStatusEffectRenderController,
            _gameplaySessionController,
            _gameplayOverlayStateController,
            _gameplayResetController) = CreateGameplayControllerBundle(this);
        (_clientPluginRuntimeController,
            _clientPluginEventController,
            _clientPluginUiBridgeController,
            _clientPluginMarkerController,
            _menuController,
            _connectionFlowController,
            _mainMenuOverlayController,
            _mainMenuOverlayStateController,
            _hostSetupFlowController,
            _windowTextInputController,
            _menuTextInputController,
            _networkPromptTextInputController,
            _chatTextInputController,
            _consoleTextInputController,
            _bootstrapController,
            _optionsMenuController,
            _mainMenuPageController,
            _pluginOptionsMenuController,
            _controlsMenuController,
            _inGameMenuController,
            _gameplayOverlayController) = CreateShellControllerBundle(this);
        (_clientSettings,
            _inputBindings,
            _hostedServerRuntime,
            _graphics) = CreateRuntimeServices(this, _hostedServerConsole);
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
        _bootstrapController.Initialize();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _bootstrapController.LoadContent();
    }

    protected override void UnloadContent()
    {
        _bootstrapController.UnloadContent();
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

    private void OpenGameplayLoadoutMenu()
    {
        if (!CanOpenGameplayLoadoutMenu())
        {
            return;
        }

        _inGameMenuOpen = false;
        _inGameMenuAwaitingEscapeRelease = false;
        _inGameMenuHoverIndex = -1;
        _gameplayLoadoutMenuOpen = true;
        _gameplayLoadoutMenuAwaitingEscapeRelease = true;
        _gameplayLoadoutMenuHoverIndex = -1;
        _gameplayLoadoutMenuViewedClass = _world.LocalPlayer.ClassId;
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
