#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenGarrison.Client;

public interface IGameplayContext
{
    MenuManager Menus { get; }
    SessionManager Session { get; }
    GameplayManager Gameplay { get; }
    string _activeReplayPath { get; set; }
    List<OpenGarrison.Client.Game1.AirBlastVisual> _airBlasts { get; }
    OpenGarrison.Core.GameMakerAssetManifest _assetManifest { get; }
    OpenGarrison.Client.AuthoritativeExplosionPresentationTracker _authoritativeExplosionPresentations { get; }
    string _autoBalanceNoticeText { get; set; }
    int _autoBalanceNoticeTicks { get; set; }
    List<OpenGarrison.Client.Game1.BackstabVisual> _backstabVisuals { get; }
    List<OpenGarrison.Client.Game1.BlastJumpFlameVisual> _blastJumpFlameVisuals { get; }
    Dictionary<(int X, int Y), float> _bloodBridgeScratch { get; }
    Dictionary<(int X, int Y), float> _bloodCryoDrawCellsScratch { get; }
    Dictionary<(int X, int Y), float> _bloodDrawCellsScratch { get; }
    int _bloodRenderMode { get; set; }
    List<OpenGarrison.Client.Game1.BloodSprayVisual> _bloodSprayVisuals { get; }
    List<OpenGarrison.Client.Game1.BloodSquibParticle> _bloodSquibParticles { get; }
    List<OpenGarrison.Client.Game1.BloodVisual> _bloodVisuals { get; }
    OpenGarrison.Core.BotBrain.BotControllerDiagnosticsSnapshot _botDiagnosticLatestSnapshot { get; set; }
    OpenGarrison.Client.BrowserAtlasTextureCache _browserAtlasTextureCache { get; set; }
    bool _browserBootstrapAssetsApplied { get; set; }
    OpenGarrison.Client.BrowserBootstrapAtlasTextureResolver _browserBootstrapAtlasResolver { get; set; }
    bool _bubbleMenuClosing { get; set; }
    OpenGarrison.Client.Game1.BubbleMenuKind _bubbleMenuKind { get; set; }
    List<OpenGarrison.Client.Game1.BubblePopVisual> _bubblePops { get; }
    bool _buildMenuOpen { get; set; }
    bool _chatOpen { get; set; }
    bool _chatSubmitAwaitingOpenKeyRelease { get; set; }
    float _classSelectAlpha { get; set; }
    bool _classSelectOpen { get; set; }
    OpenGarrison.ClientShared.ClientIdentityDocument _clientIdentity { get; }
    Microsoft.Xna.Framework.Input.KeyboardState _clientPluginKeyboard { get; set; }
    Microsoft.Xna.Framework.Input.KeyboardState _clientPluginPreviousKeyboard { get; set; }
    bool _clientPowersOpen { get; set; }
    bool _clientPowersOpenedFromGameplay { get; set; }
    OpenGarrison.ClientShared.ClientSettings _clientSettings { get; }
    OpenGarrison.Core.SimulationConfig _config { get; set; }
    Microsoft.Xna.Framework.Graphics.SpriteFont _consoleFont { get; set; }
    bool _consoleOpen { get; set; }
    bool _controlsMenuOpen { get; set; }
    bool _controlsMenuOpenedFromGameplay { get; set; }
    bool _customBubbleEditorOpen { get; set; }
    Microsoft.Xna.Framework.Graphics.RenderTarget2D _deathCamCaptureTarget { get; set; }
    bool _debugMenuOpen { get; set; }
    bool _editingPlayerName { get; set; }
    List<OpenGarrison.Client.Game1.ExplosionVisual> _explosions { get; }
    Microsoft.Xna.Framework.Audio.SoundEffect _faucetMusic { get; set; }
    Microsoft.Xna.Framework.Audio.SoundEffectInstance _faucetMusicInstance { get; set; }
    OpenGarrison.Client.FirstPlayHintSequence _firstPlayHints { get; set; }
    int _flameRenderMode { get; set; }
    List<OpenGarrison.Client.Game1.FlameSmokeVisual> _flameSmokeSecondaryVisuals { get; }
    List<OpenGarrison.Client.Game1.FlameSmokeVisual> _flameSmokeVisuals { get; }
    Microsoft.Xna.Framework.Input.MouseState _frameMouseState { get; set; }
    Microsoft.Xna.Framework.Input.MouseState _frameRawMouseState { get; set; }
    bool _friendsMenuOpen { get; set; }
    Task<OpenGarrison.ClientShared.GameplaySessionCreateResponse> _gameplayAccountSessionTask { get; set; }
    System.DateTimeOffset _gameplayAccountTokenExpiresAt { get; set; }
    bool _gameplayHudHidden { get; set; }
    OpenGarrison.Client.LoadedSpriteFrame _gameplayLoadoutBackButtonTexture { get; set; }
    OpenGarrison.Client.LoadedSpriteFrame _gameplayLoadoutBackgroundBarTexture { get; set; }
    OpenGarrison.Client.LoadedSpriteFrame _gameplayLoadoutClassSelectionTexture { get; set; }
    OpenGarrison.Client.LoadedSpriteFrame _gameplayLoadoutClassStripTexture { get; set; }
    OpenGarrison.Client.LoadedSpriteFrame _gameplayLoadoutDescriptionBoardTexture { get; set; }
    OpenGarrison.Client.LoadedSpriteFrame _gameplayLoadoutDogTagsTexture { get; set; }
    OpenGarrison.Client.LoadedSpriteFrame _gameplayLoadoutHelmetTexture { get; set; }
    bool _gameplayLoadoutMenuAwaitingEscapeRelease { get; set; }
    int _gameplayLoadoutMenuHoverIndex { get; set; }
    bool _gameplayLoadoutMenuOpen { get; set; }
    OpenGarrison.Client.LoadedSpriteFrame _gameplayLoadoutPageTexture { get; set; }
    OpenGarrison.Client.LoadedSpriteFrame _gameplayLoadoutScrollerTexture { get; set; }
    List<OpenGarrison.Client.LoadedSpriteFrame> _gameplayLoadoutSelectionAtlasChunks { get; }
    OpenGarrison.Client.LoadedSpriteFrame _gameplayLoadoutSelectionAtlasTexture { get; set; }
    OpenGarrison.Client.LoadedSpriteFrame _gameplayLoadoutSelectionTexture { get; set; }
    bool _gameplayModalOwnedInputThisFrame { get; set; }
    OpenGarrison.Client.GameplayModAssetCache _gameplayModAssets { get; set; }
    OpenGarrison.Client.Game1.GameplaySessionKind _gameplaySessionKind { get; set; }
    Microsoft.Xna.Framework.Graphics.RenderTarget2D _gameRenderTarget { get; set; }
    Microsoft.Xna.Framework.Graphics.Effect _grayscaleEffect { get; set; }
    bool _hasLatestLocalAimWorldPosition { get; set; }
    bool _hasLatestNetworkInputAimOrigin { get; set; }
    bool _hasLocalPlayerRenderTime { get; set; }
    bool _hasPredictedLocalActionState { get; set; }
    bool _hasPredictedLocalPlayerPosition { get; set; }
    bool _hasReceivedSnapshot { get; set; }
    bool _hasRemotePlayerRenderTime { get; set; }
    bool _hasSmoothedLocalPlayerRenderPosition { get; set; }
    bool _hudEditorOpen { get; set; }
    Microsoft.Xna.Framework.Graphics.RenderTarget2D _hudRenderTarget { get; set; }
    List<OpenGarrison.Client.Game1.ImpactVisual> _impactVisuals { get; }
    bool _inGameMenuOpen { get; set; }
    Microsoft.Xna.Framework.Audio.SoundEffect _ingameMusic { get; set; }
    Microsoft.Xna.Framework.Audio.SoundEffectInstance _ingameMusicInstance { get; set; }
    OpenGarrison.Client.InputBindingsSettings _inputBindings { get; }
    bool _jumpMenuOpen { get; set; }
    bool _killCamEnabled { get; set; }
    ulong _lastAppliedSnapshotFrame { get; set; }
    Nullable<int> _lastAppliedSnapshotLocalPlayerId { get; set; }
    ulong _lastBufferedSnapshotFrame { get; set; }
    string _lastGameplayWindowTitle { get; set; }
    Microsoft.Xna.Framework.Point _lastKnownMousePosition { get; set; }
    double _lastLocalPlayerRenderTimeClockSeconds { get; set; }
    double _lastPredictedRenderSmoothingTimeSeconds { get; set; }
    double _lastRemotePlayerRenderTimeClockSeconds { get; set; }
    double _lastSnapshotReceivedTimeSeconds { get; set; }
    bool _lastToDieConnectionPresentationPending { get; set; }
    bool _lastToDieFailureOverlayOpen { get; set; }
    int _lastToDieFailureOverlayTicks { get; set; }
    Microsoft.Xna.Framework.Audio.SoundEffect _lastToDieIngameMusic { get; set; }
    Microsoft.Xna.Framework.Audio.SoundEffectInstance _lastToDieIngameMusicInstance { get; set; }
    OpenGarrison.Client.LoadedSpriteFrame _lastToDieLogoTexture { get; set; }
    Microsoft.Xna.Framework.Audio.SoundEffect _lastToDieMenuMusic { get; set; }
    Microsoft.Xna.Framework.Audio.SoundEffectInstance _lastToDieMenuMusicInstance { get; set; }
    bool _lastToDieMenuOpen { get; set; }
    OpenGarrison.Client.LoadedSpriteFrame _lastToDieMenuPlaqueTexture { get; set; }
    OpenGarrison.Client.LoadedSpriteFrame _lastToDieMenuTextBoxSoloTexture { get; set; }
    int _lastToDiePerkHoverIndex { get; set; }
    bool _lastToDiePerkMenuOpen { get; set; }
    OpenGarrison.Client.Game1.LastToDieRunState _lastToDieRun { get; set; }
    bool _lastToDieStageClearOverlayOpen { get; set; }
    int _lastToDieStageClearOverlayTicks { get; set; }
    bool _lastToDieSurvivorMenuOpen { get; set; }
    float _latestLocalAimWorldX { get; set; }
    float _latestLocalAimWorldY { get; set; }
    float _latestNetworkInputAimOriginX { get; set; }
    float _latestNetworkInputAimOriginY { get; set; }
    double _latestSnapshotReceivedClockSeconds { get; set; }
    double _latestSnapshotServerTimeSeconds { get; set; }
    string _levelBackgroundFileFailedPath { get; set; }
    Microsoft.Xna.Framework.Graphics.Texture2D _levelBackgroundFileTexture { get; set; }
    OpenGarrison.Core.SimpleLevel _levelBackgroundFileTextureLevel { get; set; }
    string _levelBackgroundFileTexturePath { get; set; }
    bool _loadingOverlayVisible { get; set; }
    OpenGarrison.Client.Game1.OverheadChatMessage _localOverheadChatMessage { get; set; }
    float _localPlayerInterpolationBackTimeSeconds { get; set; }
    double _localPlayerRenderTimeSeconds { get; set; }
    Nullable<int> _localPlayerSnapshotEntityId { get; set; }
    List<OpenGarrison.Client.Game1.LooseSheetVisual> _looseSheetVisuals { get; }
    bool _mainMenuBottomBarHover { get; set; }
    bool _mainMenuChromeHidden { get; set; }
    int _mainMenuHoverIndex { get; set; }
    bool _mainMenuOpen { get; set; }
    OpenGarrison.Client.Game1.MainMenuPage _mainMenuPage { get; set; }
    OpenGarrison.Core.MenuBackgroundMode _menuBackgroundMode { get; set; }
    OpenGarrison.Client.LoadedSpriteFrame _menuBackgroundTexture { get; set; }
    string _menuBackgroundTexturePath { get; set; }
    Dictionary<char, OpenGarrison.Client.Game1.MenuBitmapGlyph> _menuBitmapFontGlyphs { get; }
    int _menuBitmapFontLineHeight { get; set; }
    OpenGarrison.Client.LoadedSpriteFrame _menuBitmapFontTexture { get; set; }
    Microsoft.Xna.Framework.Graphics.SpriteFont _menuFont { get; set; }
    int _menuImageFrame { get; set; }
    Microsoft.Xna.Framework.Audio.SoundEffect _menuMusic { get; set; }
    Microsoft.Xna.Framework.Audio.SoundEffectInstance _menuMusicInstance { get; set; }
    OpenGarrison.Client.LoadedSpriteFrame _menuPlaqueTallTexture { get; set; }
    OpenGarrison.Client.LoadedSpriteFrame _menuPlaqueTexture { get; set; }
    string _menuStatusMessage { get; set; }
    OpenGarrison.Client.LoadedSpriteFrame _menuTextBoxBottomTexture { get; set; }
    OpenGarrison.Client.LoadedSpriteFrame _menuTextBoxMiddleTexture { get; set; }
    OpenGarrison.Client.LoadedSpriteFrame _menuTextBoxSoloTexture { get; set; }
    OpenGarrison.Client.LoadedSpriteFrame _menuTextBoxTopTexture { get; set; }
    List<OpenGarrison.Client.Game1.MineTrailVisual> _mineTrailVisuals { get; }
    bool _namePromptPresented { get; set; }
    OpenGarrison.Client.NetworkGameClient _networkClient { get; }
    int _networkInterpolationWarmupSnapshotsRemaining { get; set; }
    double _networkInterpolationWarmupUntilClockSeconds { get; set; }
    Nullable<OpenGarrison.Protocol.LastToDieWirePhase> _networkPresentationObservedLastToDiePhase { get; set; }
    float _networkSnapshotInterpolationDurationSeconds { get; set; }
    bool _networkWorldWarmupAcceptNextAppliedSnapshotAsBaseline { get; set; }
    bool _networkWorldWarmupActive { get; set; }
    int _networkWorldWarmupAppliedSnapshotsAfterFull { get; set; }
    bool _networkWorldWarmupFullSnapshotApplied { get; set; }
    Dictionary<OpenGarrison.Client.LoadedSpriteFrame, OpenGarrison.Client.LoadedSpriteFrame> _neutralSpriteFrameCache { get; }
    int _nextBloodSquibSeed { get; set; }
    int _nextClientBackstabVisualId { get; set; }
    System.DateTimeOffset _nextGameplayAccountAttachAttemptAt { get; set; }
    string _observedGameplayLevelName { get; set; }
    int _observedGameplayMapAreaIndex { get; set; }
    Nullable<ValueTuple<string, int>> _offlinePracticeNextMap { get; set; }
    bool _offlinePracticeSpectatorMode { get; set; }
    OpenGarrison.Client.Game1.OnlineConnectionIntent _onlineConnectionIntent { get; set; }
    bool _optionsMenuOpen { get; set; }
    bool _optionsMenuOpenedFromGameplay { get; set; }
    int _optionsPageIndex { get; set; }
    Dictionary<byte, OpenGarrison.Client.Game1.OverheadChatMessage> _overheadChatMessagesBySlot { get; }
    int _particleMode { get; set; }
    bool _passwordPromptOpen { get; set; }
    OpenGarrison.Client.PlayerHostedRoomSession _peerRoomSession { get; set; }
    Nullable<OpenGarrison.Core.PlayerTeam> _pendingClassSelectTeam { get; set; }
    Nullable<OpenGarrison.Client.Game1.ControllerControlsMenuBinding> _pendingControllerControlsBinding { get; set; }
    Nullable<OpenGarrison.Client.Game1.ControlsMenuBinding> _pendingControlsBinding { get; set; }
    ulong _pendingGameplayAccountAttachRequestId { get; set; }
    int _pendingHostedConnectPort { get; set; }
    int _pendingHostedConnectTicks { get; set; }
    List<OpenGarrison.Protocol.SnapshotDamageEvent> _pendingNetworkDamageEvents { get; }
    List<OpenGarrison.Core.WorldSoundEvent> _pendingNetworkSoundEvents { get; }
    List<OpenGarrison.Protocol.SnapshotVisualEvent> _pendingNetworkVisualEvents { get; }
    List<OpenGarrison.Client.Game1.PredictedLocalInput> _pendingPredictedInputs { get; }
    List<(int X, int Y, float Amount, bool Cryo)> _pendingSettledBloodTransfers { get; }
    List<OpenGarrison.Client.Game1.PendingWeaponShellVisual> _pendingWeaponShellVisuals { get; }
    Microsoft.Xna.Framework.Graphics.Texture2D _pixel { get; set; }
    string _playerNameEditBuffer { get; set; }
    bool _pluginOptionsMenuOpen { get; set; }
    bool _pluginOptionsMenuOpenedFromGameplay { get; set; }
    int _practiceCapLimit { get; set; }
    List<OpenGarrison.Client.Game1.PracticeMapEntry> _practiceMapEntries { get; set; }
    int _practiceRespawnSeconds { get; set; }
    int _practiceSessionElapsedTicks { get; set; }
    bool _practiceSetupOpen { get; set; }
    bool _practiceStickyGibBloodEnabled { get; set; }
    int _practiceTickRate { get; set; }
    int _practiceTimeLimitMinutes { get; set; }
    Microsoft.Xna.Framework.Vector2 _predictedLocalPlayerRenderCorrectionOffset { get; set; }
    OpenGarrison.Core.PlayerEntity _predictedLocalPlayerShadow { get; set; }
    int _prePredictionFlameCount { get; set; }
    List<OpenGarrison.Client.Game1.PresentedExplosionVisual> _presentedExplosionVisualsThisFrame { get; }
    Microsoft.Xna.Framework.Input.KeyboardState _previousKeyboard { get; set; }
    Microsoft.Xna.Framework.Input.MouseState _previousMouse { get; set; }
    HashSet<int> _processedSettledBloodDropIds { get; }
    HashSet<int> _processedStickyGibBloodDropIds { get; }
    float _projectileInterpolationBackTimeSeconds { get; set; }
    int _quitPromptHoverIndex { get; set; }
    bool _quitPromptOpen { get; set; }
    float _remotePlayerInterpolationBackTimeSeconds { get; set; }
    double _remotePlayerRenderTimeSeconds { get; set; }
    bool _replaySeekCatchUpActive { get; set; }
    int _replaySeekTargetMilliseconds { get; set; }
    List<OpenGarrison.Client.Game1.RocketSmokeVisual> _rocketSmokeVisuals { get; }
    OpenGarrison.Client.RotatedWeaponSpriteCache _rotatedWeaponSprites { get; set; }
    OpenGarrison.Client.GameMakerRuntimeAssetCache _runtimeAssets { get; set; }
    OpenGarrison.ClientShared.ClientRuntimeComposition _runtimeComposition { get; set; }
    bool _scoreboardOpen { get; set; }
    bool _serverLocalPredictionEnabled { get; set; }
    Dictionary<(int X, int Y), OpenGarrison.Client.Game1.SettledBloodCell> _settledBloodCells { get; }
    List<OpenGarrison.Client.Game1.ShellVisual> _shellVisuals { get; }
    float _smoothedSnapshotIntervalSeconds { get; set; }
    float _smoothedSnapshotJitterSeconds { get; set; }
    Microsoft.Xna.Framework.Graphics.SpriteBatch _spriteBatch { get; set; }
    Dictionary<OpenGarrison.Client.LoadedSpriteFrame, Microsoft.Xna.Framework.Rectangle> _spriteFontOpaqueBoundsCache { get; }
    List<(int X, int Y)> _staleSettledBloodCellKeys { get; }
    List<int> _staleSettledBloodDropIds { get; }
    List<int> _staleStickyGibBloodDropIds { get; }
    List<int> _staleStickyGibBloodPlayerIds { get; }
    OpenGarrison.Client.GameStartupMode _startupMode { get; }
    bool _startupSplashOpen { get; set; }
    Dictionary<int, OpenGarrison.Client.Game1.StickyGibBloodCoating> _stickyGibBloodCoatings { get; }
    bool _stuckArrowsEnabled { get; set; }
    List<OpenGarrison.Client.Game1.StuckArrowVisual> _stuckArrowVisuals { get; }
    bool _suppressFullscreenToggleUntilRelease { get; set; }
    float _teamSelectAlpha { get; set; }
    bool _teamSelectOpen { get; set; }
    System.Random _visualRandom { get; }
    OpenGarrison.Client.VoiceChatClient _voiceChat { get; set; }
    bool _voteMenuOpen { get; set; }
    List<OpenGarrison.Client.Game1.WallspinDustVisual> _wallspinDustVisuals { get; }
    bool _wasDeathCamActive { get; set; }
    bool _wasMatchEnded { get; set; }
    bool _wasWindowActive { get; set; }
    OpenGarrison.Client.WindowInputFilter _windowInputFilter { get; }
    OpenGarrison.Core.SimulationWorld _world { get; set; }
    bool AreBloodVisualsEnabled { get; }
    Microsoft.Xna.Framework.Content.ContentManager Content { get; set; }
    Microsoft.Xna.Framework.Graphics.GraphicsDevice GraphicsDevice { get; }
    bool HasManagedRoom { get; }
    bool IsMouseVisible { get; set; }
    bool IsPracticeSessionActive { get; }
    bool IsWindowInputActive { get; }
    bool UseReducedBrowserEffects { get; }
    int ViewportHeight { get; }
    int ViewportWidth { get; }
    Microsoft.Xna.Framework.GameWindow Window { get; }
    void AccumulateProceduralFlameParticle(Dictionary<ValueTuple<int, int>, float> cells, int seed, float centerX, float centerY, float scale, float alphaScale, float motionX = 0f, float motionY = 0f, float trajectoryStretch = 1f, bool includeHornAccent = false);
    void AddConsoleLine(string line);
    void AddNetworkConsoleLine(string message);
    bool AdvanceBrowserGameplayWarmup();
    void AdvanceCorpseAcidDissolves();
    void AdvanceDynamicRagdolls();
    void AdvanceFlameSmokeVisuals();
    void AdvanceGameplaySimulation(Microsoft.Xna.Framework.GameTime gameTime, OpenGarrison.Core.PlayerInputSnapshot networkInput);
    void AdvanceMenuClientTicks(int ticks);
    void AdvanceRecentPredictedAirBlastVisuals();
    void AdvanceRecentPredictedExplosionVisuals();
    void AdvanceStartupSplashTicks(int ticks, Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void ApplyAudioMuteState();
    void ApplyLastToDieStageEnemyModifiers();
    OpenGarrison.Core.PlayerInputSnapshot ApplyPendingInputEdges(OpenGarrison.Core.PlayerInputSnapshot input);
    void ApplyPracticeDummyPreferencesBeforeJoin();
    void ApplySelectedLastToDieSurvivorToCurrentStage();
    void BeginBrowserGameplayWarmup();
    void BeginClosingBuildMenu();
    void BeginGameplayAccountAttach();
    void BeginGameplayWorldSpriteBatch(Microsoft.Xna.Framework.Graphics.RasterizerState rasterizerState);
    void BeginLogicalFrame(Microsoft.Xna.Framework.Color clearColor);
    void BeginNetworkWorldWarmup(string levelName);
    void BeginPendingHostedLocalConnect(int port, int delayTicks, string statusMessage);
    ValueTuple<OpenGarrison.Core.PlayerInputSnapshot, OpenGarrison.Core.PlayerInputSnapshot> BuildGameplayInputs(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse, Microsoft.Xna.Framework.Vector2 cameraPosition, float deltaSeconds);
    void CancelPracticeNavigationWarmup();
    bool CanOpenGameplayChat();
    bool CanOpenInGamePauseMenu();
    bool CanToggleGameplaySelectionMenus();
    bool CanUseGameplayChatShortcut();
    void CapturePendingPredictedInputEdges(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse, OpenGarrison.Core.PlayerInputSnapshot networkInput);
    void ClearHostedSocialPresenceEndpoint();
    void ClearLastToDieDeathFocusPresentation();
    void ClearManualPracticeBotRequests();
    void ClearOnlinePlayerSocialProfiles();
    void ClearPendingNetworkMapSync();
    void ClearPendingSecondaryAbilityPress();
    void ClearRemoteCustomBubbleStates();
    void ClearReplayQueue(bool clearActiveReplayPath);
    void ClearSocialPresenceNetworkEndpoint();
    void CloseGameplayOverlayState();
    void CloseGameplaySelectionMenus();
    void CloseLobbyBrowser(bool clearStatus);
    void CloseMainMenuOverlayState();
    int ConsumeClientTickCount(Microsoft.Xna.Framework.GameTime gameTime);
    void CycleGameplayCameraZoom();
    void DismissCustomBubbleEditor();
    void DispatchClientSemanticGameplayEvents();
    void DisposeBrandLogoAssets();
    void DisposeDamageVignetteTextures();
    void DisposeGameplayMissPopupFrame();
    void DisposeGarrisonBuilderEditorAssets();
    void DisposeLastToDieBuffIconFrame();
    void DisposeLastToDieSurvivorCarouselAssets();
    void DisposeReplayPlaybackControlAssets();
    void DrawClassSelectHud();
    bool DrawDeathCamCaptureOverlay(int viewportWidth, int viewportHeight);
    void DrawGameplayHudLayersOrComposite(Microsoft.Xna.Framework.Input.MouseState mouse, Microsoft.Xna.Framework.Vector2 cameraPosition);
    void DrawGameplayModalOverlays(Microsoft.Xna.Framework.Input.MouseState mouse, Microsoft.Xna.Framework.Vector2 cameraPosition);
    void DrawGameplayWorld(Microsoft.Xna.Framework.Vector2 cameraPosition, int viewportWidth, int viewportHeight, Microsoft.Xna.Framework.Rectangle worldRectangle, Microsoft.Xna.Framework.Rectangle playerRectangle, Microsoft.Xna.Framework.Rectangle centerLine, Microsoft.Xna.Framework.Rectangle centerColumn, Microsoft.Xna.Framework.Rectangle worldTopBorder, Microsoft.Xna.Framework.Rectangle worldBottomBorder, Microsoft.Xna.Framework.Rectangle worldLeftBorder, Microsoft.Xna.Framework.Rectangle worldRightBorder, Microsoft.Xna.Framework.Rectangle spawnRectangle, Nullable<int> skippedDeadBodySourcePlayerId = default);
    bool DrawLastToDieDeathFocusOverlay(int viewportWidth, int viewportHeight);
    void DrawLoadedSpriteFrame(OpenGarrison.Client.LoadedSpriteFrame frame, Microsoft.Xna.Framework.Rectangle destinationRectangle, Microsoft.Xna.Framework.Color tint);
    void DrawLoadedSpriteFrame(OpenGarrison.Client.LoadedSpriteFrame frame, Microsoft.Xna.Framework.Vector2 position, Nullable<Microsoft.Xna.Framework.Rectangle> sourceRectangle, Microsoft.Xna.Framework.Color tint, float rotation, Microsoft.Xna.Framework.Vector2 origin, Microsoft.Xna.Framework.Vector2 scale, Microsoft.Xna.Framework.Graphics.SpriteEffects effects, float layerDepth);
    void DrawLoadingOverlay();
    void DrawProceduralFlameParticles(Dictionary<ValueTuple<int, int>, float> cells, Microsoft.Xna.Framework.Vector2 cameraPosition, bool topOutlineOnly = false, float drawAlpha = 1f);
    void DrawSoftwareMenuCursor(Microsoft.Xna.Framework.Input.MouseState mouse);
    void DrawStabAnimation(OpenGarrison.Core.StabAnimEntity stabAnimation, Microsoft.Xna.Framework.Vector2 cameraPosition);
    void DrawStartupSplash();
    void DrawTeamSelectHud();
    void DrawVersionOverlay();
    void DrawVoiceParticipants();
    void DrawVotePresentationOverlay();
    void EndGameplayWorldSpriteBatch();
    void EndLogicalFrame();
    void EnsureAutomaticDemoRecordingForConnection(string serverLabel);
    void EnsureWindowInactiveInputReleased(bool windowActive, Microsoft.Xna.Framework.Input.MouseState releasedMouse);
    IEnumerable<OpenGarrison.Core.PlayerEntity> EnumerateRenderablePlayers();
    void FinalizeGameplayFrame(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    OpenGarrison.Core.PlayerEntity FindPlayerById(int playerId);
    float GetBloodPersistenceScale();
    string GetBrowserGameplayWarmupStatusMessage();
    Microsoft.Xna.Framework.Vector2 GetCameraTopLeft(int viewportWidth, int viewportHeight, int mouseX, int mouseY);
    Microsoft.Xna.Framework.Input.MouseState GetConstrainedMouseState(Microsoft.Xna.Framework.Input.MouseState rawMouse);
    Microsoft.Xna.Framework.Vector2 GetFlameScaledCenterOfMassWorldPosition(OpenGarrison.Core.FlameProjectileEntity flame);
    Microsoft.Xna.Framework.Input.MouseState GetFrameMouseState();
    Microsoft.Xna.Framework.Input.MouseState GetFrameRawMouseState();
    int GetGameplayCameraViewportHeight(int viewportHeight);
    Microsoft.Xna.Framework.Vector2 GetGameplayInputAimOrigin();
    Microsoft.Xna.Framework.Vector2 GetGameplayInputCameraTopLeft(int viewportWidth, int viewportHeight, int mouseX, int mouseY);
    Microsoft.Xna.Framework.Point GetGameplayWorldViewport(int viewportWidth, int viewportHeight);
    int GetLastToDieStageIntroDurationTicks();
    Microsoft.Xna.Framework.Rectangle GetLocalPlayerRectangle(Microsoft.Xna.Framework.Vector2 cameraPosition);
    float GetMinimumLocalPlayerInterpolationBackTimeSeconds();
    float GetMinimumRemotePlayerInterpolationBackTimeSeconds();
    int GetPlayerStateKey(OpenGarrison.Core.PlayerEntity player);
    float GetPlayerVisibilityAlpha(OpenGarrison.Core.PlayerEntity player);
    OpenGarrison.Core.ExperimentalGameplaySettings GetPracticeExperimentalGameplaySettings();
    double GetProjectileRenderTimeSeconds();
    Microsoft.Xna.Framework.Vector2 GetRenderPosition(int entityId, float x, float y, bool allowInterpolation = true);
    Microsoft.Xna.Framework.Vector2 GetRenderPosition(OpenGarrison.Core.PlayerEntity player, bool allowInterpolation = true);
    OpenGarrison.Client.LoadedGameMakerSprite GetResolvedSprite(string spriteName);
    Microsoft.Xna.Framework.Input.MouseState GetScaledMouseState(Microsoft.Xna.Framework.Input.MouseState rawMouse);
    OpenGarrison.Client.Game1.PracticeMapEntry GetSelectedPracticeMapEntry();
    Microsoft.Xna.Framework.Vector2 GetWeaponShellSpawnOrigin(OpenGarrison.Core.PlayerEntity player);
    bool HandleActiveTextFieldKeyboardShortcuts(Microsoft.Xna.Framework.Input.KeyboardState keyboard, double elapsedSeconds);
    void HandleBrowserTextInput(char character);
    void HandleWindowFocusLost(Microsoft.Xna.Framework.Input.MouseState releasedMouse);
    bool HasGameplayModalInputOwner();
    void HideLoadingOverlay();
    void InitializeClientPlugins();
    void InitializeConsoleInputCursor();
    void InitializePracticeBotNamePoolForMatch();
    void InitializeServerLauncherMode();
    void InvalidateDiscordRichPresenceRefresh();
    bool IsBindingPressed(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse, OpenGarrison.Client.InputBinding binding);
    bool IsBrowserGameplayWarmupComplete();
    bool IsChatShortcutPressed(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.Keys key);
    bool IsClientPerformanceDiagnosticsEnabled();
    bool IsControllerBindingPressed(OpenGarrison.Core.ControllerButtonBinding binding, Microsoft.Xna.Framework.Input.GamePadState current, Microsoft.Xna.Framework.Input.GamePadState previous);
    bool IsControllerBindingPressed(OpenGarrison.Core.ControllerButtonBinding binding);
    bool IsControllerMenuBackPressed();
    bool IsGameplayInputBlocked();
    bool IsGameplayLoadingForMenuInput();
    bool IsKeyPressed(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.Keys key);
    bool IsLastToDieDeathFocusPresentationActive();
    bool IsLastToDieFailureOverlayActive();
    bool IsLastToDieStageClearOverlayActive();
    bool IsLocalSpectatorPresentationActive();
    bool IsNetworkWorldWarmupBlockingPresentation();
    bool IsPracticeNavigationWarmupBlockingGameplay();
    bool IsSpyHiddenFromLocalViewer(int ownerId, OpenGarrison.Core.PlayerTeam ownerTeam, float spyX);
    bool IsSpyHiddenFromLocalViewer(OpenGarrison.Core.PlayerEntity player);
    void LeaveManagedRoom();
    void LeavePeerRoom();
    void LoadFaucetMusic();
    void LoadGameplayLoadoutMenuTextures();
    void LoadIngameMusic();
    Microsoft.Xna.Framework.Graphics.SpriteFont LoadInitialSpriteFont(string assetName);
    void LoadLastToDieIngameMusic();
    void LoadLastToDieMenuMusic();
    void LoadMenuBitmapFont();
    void LoadMenuMusic();
    void LoadMenuPlaqueTextures();
    void LogClientPerformanceLine(string line);
    void NormalizePracticeSetupState();
    void NotifyClientPluginsStarted();
    void ObserveLastToDieBotReactionState();
    void ObserveLastToDieCombatFeedbackState();
    void ObservePendingWorldHealingEventsForHealingCharacterEffects();
    void OnWindowTextInput(System.Object sender, Microsoft.Xna.Framework.TextInputEventArgs e);
    void OpenChat(bool teamOnly);
    void OpenGameplayClassSelection();
    void OpenInGameMenu();
    void PersistClientSettings();
    void PersistInputBindings();
    void PrepareDeathCamCaptureIfNeeded(int viewportWidth, int viewportHeight);
    void PrepareGameplayHudOpacityComposite(Microsoft.Xna.Framework.Input.MouseState mouse, Microsoft.Xna.Framework.Vector2 cameraPosition);
    void PrepareHostedServerLaunchUi(bool closeHostSetup, bool disconnectNetworkClient);
    void PrepareLastToDieDeathFocusOverlayIfNeeded(int viewportWidth, int viewportHeight);
    void ProcessNetworkMessages();
    void PumpEmbeddedSession(double elapsedSeconds);
    void PumpPeerRoom(double elapsed);
    void PumpRunUploads(double elapsedSeconds);
    void QueuePracticeNavigationWarmupForCurrentLevel();
    void QueueWelcomeAfterNetworkMapSync(OpenGarrison.Protocol.WelcomeMessage welcome);
    void RecordPresentedExplosionVisual(string effectName, float x, float y);
    void RecordRecentConnection(string host, int port);
    void ReinitializeSimulationForTickRate(int tickRate);
    void RememberPredictedExplosionVisual(OpenGarrison.Core.WorldVisualEvent visualEvent);
    void ResetBackstabVisuals();
    void ResetBotDiagnosticSample();
    void ResetBuffBannerReadySoundObservation();
    void ResetCameraPanningState();
    void ResetChatInputState(bool requireOpenKeyRelease = false);
    void ResetCivviePogoTrickPresentationObservation();
    void ResetClientTimingState();
    void ResetCorpseAcidDissolves();
    void ResetDynamicRagdollEffects();
    void ResetGameplayRuntimeState();
    void ResetGameplayTransitionEffects();
    void ResetHealingCharacterEffects();
    void ResetJumpState();
    void ResetLastToDieBotReactionState();
    void ResetLastToDieCombatFeedbackPresentation();
    void ResetLastToDieState();
    void ResetPracticeBotManagerState(bool releaseWorldSlots);
    void ResetPracticeRoundPoints();
    void ResetProcessedNetworkEventHistory();
    void ResetSmoothCameraState();
    void ResetSmoothCameraState(Microsoft.Xna.Framework.Vector2 position);
    void ResetSnapshotPresentationHistories(bool preserveRetainedProjectilePresentation = false);
    void ResetSnapshotStateHistory();
    void ResetSpectatorTracking(bool enableTracking);
    void ResetTransientPresentationEffects();
    void ResetVoiceChat();
    void ResetVotePresentation();
    void ReturnToMainMenu(string statusMessage = default);
    void ReturnToMainMenuWithNetworkStatus(string statusMessage, string consoleMessage);
    void ReturnToMainMenuWithNetworkStatus(string statusMessage);
    int ScaleBloodVisualCount(int maximumCount);
    bool SelectPracticeMapEntry(string levelName);
    void SetJoiningServerLoadingLabel(string serverLabel);
    void SetNetworkStatus(string statusMessage);
    void SetNetworkStatusAndConsole(string statusMessage, string consoleMessage);
    void SetPersistedMenuStatusMessage(string message);
    void SetScoreRouteRecorderCaptureInput(OpenGarrison.Core.PlayerInputSnapshot gameplayInput);
    void SetSocialPresenceNetworkEndpoint(OpenGarrison.Client.NetworkEndpoint endpoint);
    bool ShouldDrawSoftwareMenuCursor();
    bool ShouldPresentAuthoritativeExplosionVisual(OpenGarrison.Protocol.SnapshotVisualEvent visualEvent);
    bool ShouldShowGameplayMouseCursor();
    bool ShouldSuppressPredictedAirBlastVisualEcho(OpenGarrison.Protocol.SnapshotVisualEvent visualEvent);
    bool ShouldSuppressPredictedExplosionVisualEcho(OpenGarrison.Protocol.SnapshotVisualEvent visualEvent);
    bool ShouldUseSoftwareMenuCursor();
    void ShowJoiningServerLoadingOverlay(string serverLabel = default);
    void ShowLoadingOverlay(string message, Nullable<double> progress = default);
    void ShutdownClientPlugins();
    void SpawnBackstabVisual(int ownerId, OpenGarrison.Core.PlayerTeam team, float x, float y, float directionDegrees);
    void SpawnCivvieMoneyVisual(OpenGarrison.Core.CivvieMoneyTrailSpawn spawn);
    void SpawnCivvieMoneyVisual(float x, float y, float initialHorizontalSpeed);
    void SpawnLastToDieDroneSwarmForCurrentStage();
    void SpawnLooseSheetVisual(float x, float y, float initialHorizontalSpeed);
    void SpawnWallspinDustVisual(float x, float y, int emissionTicks = 1);
    void StopEmbeddedSession();
    void StopFaucetMusic();
    void StopHostedServer();
    void StopIngameMusic();
    void StopLastToDieGameOverSound();
    void StopLastToDieIngameMusic();
    void StopLastToDieMenuMusic();
    void StopLocalJukebox();
    void StopLocalRapidFireWeaponAudio();
    void StopMenuMusic();
    void SuppressMouseFireAfterGameplayInputUnblocks(bool wasGameplayInputBlocked, Microsoft.Xna.Framework.Input.MouseState mouse);
    void SyncDynamicRagdollsWithDeadBodies();
    void SyncPracticeBotRoster(OpenGarrison.Core.PlayerTeam localTeam);
    void ToggleAudioMute();
    void ToggleFullscreenHotkey();
    void ToggleGameplayClassSelection();
    void ToggleGameplayTeamSelection();
    OpenGarrison.Client.Game1.NetworkMapSyncStatus TryEnsureNetworkMapAvailable(string levelName, bool isCustomMap, string mapDownloadUrl, string mapContentHash, out string error);
    bool TryGetLevelBackgroundTexture(out Microsoft.Xna.Framework.Graphics.Texture2D texture);
    void TryHandleVoteShortcut(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    bool TryStartHostedServerBackground(string serverName, int port, int maxPlayers, string password, string rconPassword, int timeLimitMinutes, int capLimit, int respawnSeconds, bool lobbyAnnounce, bool autoBalance, bool secondaryAbilitiesEnabled, string requestedMap, string mapRotationFile, bool resetConsole, out string error);
    void UnloadCrtPresentation();
    void UpdateAccountOperation();
    void UpdateBotBrainCorridorRecorderHotkeys(Microsoft.Xna.Framework.Input.KeyboardState keyboard);
    void UpdateBubbleMenuState(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateChatScrollState(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateClientPowersMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateConsoleScrollState(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateControllerInputState(bool windowActive, Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateControlsMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateCrtUnlockSequence(Microsoft.Xna.Framework.Input.KeyboardState keyboard, System.TimeSpan totalGameTime);
    void UpdateCustomBubbleEditor(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateCustomBubbleHotkey(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateDebugMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateEmbeddedConsoleCommand();
    void UpdateFriendsMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateGameplayLoadoutMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateGameplayMenuState(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateGameplayPresentation(Microsoft.Xna.Framework.GameTime gameTime, Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse, int clientTicks);
    void UpdateGameplayScreenState(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateGameplayWindowState();
    void UpdateGarrisonBuilderEditor(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse, float deltaSeconds);
    void UpdateHitboxDebugHotkey(Microsoft.Xna.Framework.Input.KeyboardState keyboard);
    void UpdateHudEditor(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateInGameMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateLastToDieDeathFocusPresentation();
    void UpdateLastToDieFailureOverlay(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateLastToDiePerkMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateLastToDieStageClearOverlay(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateLastToDieSurvivorMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateMenuStatusMessageExpiry();
    void UpdateOfflinePracticeMapVote();
    void UpdateOptionsMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdatePluginOptionsMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    bool UpdatePracticeNavigationWarmup();
    void UpdatePracticeSetupMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    bool UpdateQuitPrompt(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateReplayPlaybackControls(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateRespawnCameraState(float deltaSeconds, Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateScoreboardState(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateSpectatorTrackingHotkeys(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateVoteMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UploadSelectedCustomBubbleState();
}
