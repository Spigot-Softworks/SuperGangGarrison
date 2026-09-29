#nullable enable

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Client;

public partial class Game1 : IGameplayContext
{
    string IGameplayContext._activeReplayPath { get => _activeReplayPath; set => _activeReplayPath = value; }

    List<OpenGarrison.Client.Game1.AirBlastVisual> IGameplayContext._airBlasts { get => _airBlasts; }

    OpenGarrison.Core.GameMakerAssetManifest IGameplayContext._assetManifest { get => _assetManifest; }

    OpenGarrison.Client.AuthoritativeExplosionPresentationTracker IGameplayContext._authoritativeExplosionPresentations { get => _authoritativeExplosionPresentations; }

    string IGameplayContext._autoBalanceNoticeText { get => _autoBalanceNoticeText; set => _autoBalanceNoticeText = value; }

    int IGameplayContext._autoBalanceNoticeTicks { get => _autoBalanceNoticeTicks; set => _autoBalanceNoticeTicks = value; }

    List<OpenGarrison.Client.Game1.BackstabVisual> IGameplayContext._backstabVisuals { get => _backstabVisuals; }

    List<OpenGarrison.Client.Game1.BlastJumpFlameVisual> IGameplayContext._blastJumpFlameVisuals { get => _blastJumpFlameVisuals; }

    Dictionary<ValueTuple<int, int>, float> IGameplayContext._bloodBridgeScratch { get => _bloodBridgeScratch; }

    Dictionary<ValueTuple<int, int>, float> IGameplayContext._bloodCryoDrawCellsScratch { get => _bloodCryoDrawCellsScratch; }

    Dictionary<ValueTuple<int, int>, float> IGameplayContext._bloodDrawCellsScratch { get => _bloodDrawCellsScratch; }

    int IGameplayContext._bloodRenderMode { get => _bloodRenderMode; set => _bloodRenderMode = value; }

    List<OpenGarrison.Client.Game1.BloodSprayVisual> IGameplayContext._bloodSprayVisuals { get => _bloodSprayVisuals; }

    List<OpenGarrison.Client.Game1.BloodSquibParticle> IGameplayContext._bloodSquibParticles { get => _bloodSquibParticles; }

    List<OpenGarrison.Client.Game1.BloodVisual> IGameplayContext._bloodVisuals { get => _bloodVisuals; }

    OpenGarrison.Core.BotBrain.BotControllerDiagnosticsSnapshot IGameplayContext._botDiagnosticLatestSnapshot { get => _botDiagnosticLatestSnapshot; set => _botDiagnosticLatestSnapshot = value; }

    OpenGarrison.Client.BrowserAtlasTextureCache IGameplayContext._browserAtlasTextureCache { get => _browserAtlasTextureCache; set => _browserAtlasTextureCache = value; }

    bool IGameplayContext._browserBootstrapAssetsApplied { get => _browserBootstrapAssetsApplied; set => _browserBootstrapAssetsApplied = value; }

    OpenGarrison.Client.BrowserBootstrapAtlasTextureResolver IGameplayContext._browserBootstrapAtlasResolver { get => _browserBootstrapAtlasResolver; set => _browserBootstrapAtlasResolver = value; }

    bool IGameplayContext._bubbleMenuClosing { get => _bubbleMenuClosing; set => _bubbleMenuClosing = value; }

    OpenGarrison.Client.Game1.BubbleMenuKind IGameplayContext._bubbleMenuKind { get => _bubbleMenuKind; set => _bubbleMenuKind = value; }

    List<OpenGarrison.Client.Game1.BubblePopVisual> IGameplayContext._bubblePops { get => _bubblePops; }

    bool IGameplayContext._buildMenuOpen { get => _buildMenuOpen; set => _buildMenuOpen = value; }

    bool IGameplayContext._chatOpen { get => _chatOpen; set => _chatOpen = value; }

    bool IGameplayContext._chatSubmitAwaitingOpenKeyRelease { get => _chatSubmitAwaitingOpenKeyRelease; set => _chatSubmitAwaitingOpenKeyRelease = value; }

    float IGameplayContext._classSelectAlpha { get => _classSelectAlpha; set => _classSelectAlpha = value; }

    bool IGameplayContext._classSelectOpen { get => _classSelectOpen; set => _classSelectOpen = value; }

    OpenGarrison.ClientShared.ClientIdentityDocument IGameplayContext._clientIdentity { get => _clientIdentity; }

    Microsoft.Xna.Framework.Input.KeyboardState IGameplayContext._clientPluginKeyboard { get => _clientPluginKeyboard; set => _clientPluginKeyboard = value; }

    Microsoft.Xna.Framework.Input.KeyboardState IGameplayContext._clientPluginPreviousKeyboard { get => _clientPluginPreviousKeyboard; set => _clientPluginPreviousKeyboard = value; }

    bool IGameplayContext._clientPowersOpen { get => _clientPowersOpen; set => _clientPowersOpen = value; }

    bool IGameplayContext._clientPowersOpenedFromGameplay { get => _clientPowersOpenedFromGameplay; set => _clientPowersOpenedFromGameplay = value; }

    OpenGarrison.ClientShared.ClientSettings IGameplayContext._clientSettings { get => _clientSettings; }

    OpenGarrison.Core.SimulationConfig IGameplayContext._config { get => _config; set => _config = value; }

    Microsoft.Xna.Framework.Graphics.SpriteFont IGameplayContext._consoleFont { get => _consoleFont; set => _consoleFont = value; }

    bool IGameplayContext._consoleOpen { get => _consoleOpen; set => _consoleOpen = value; }

    bool IGameplayContext._controlsMenuOpen { get => _controlsMenuOpen; set => _controlsMenuOpen = value; }

    bool IGameplayContext._controlsMenuOpenedFromGameplay { get => _controlsMenuOpenedFromGameplay; set => _controlsMenuOpenedFromGameplay = value; }

    bool IGameplayContext._customBubbleEditorOpen { get => _customBubbleEditorOpen; set => _customBubbleEditorOpen = value; }

    Microsoft.Xna.Framework.Graphics.RenderTarget2D IGameplayContext._deathCamCaptureTarget { get => _deathCamCaptureTarget; set => _deathCamCaptureTarget = value; }

    bool IGameplayContext._debugMenuOpen { get => _debugMenuOpen; set => _debugMenuOpen = value; }

    bool IGameplayContext._editingPlayerName { get => _editingPlayerName; set => _editingPlayerName = value; }

    List<OpenGarrison.Client.Game1.ExplosionVisual> IGameplayContext._explosions { get => _explosions; }

    Microsoft.Xna.Framework.Audio.SoundEffect IGameplayContext._faucetMusic { get => _faucetMusic; set => _faucetMusic = value; }

    Microsoft.Xna.Framework.Audio.SoundEffectInstance IGameplayContext._faucetMusicInstance { get => _faucetMusicInstance; set => _faucetMusicInstance = value; }

    OpenGarrison.Client.FirstPlayHintSequence IGameplayContext._firstPlayHints { get => _firstPlayHints; set => _firstPlayHints = value; }

    int IGameplayContext._flameRenderMode { get => _flameRenderMode; set => _flameRenderMode = value; }

    List<OpenGarrison.Client.Game1.FlameSmokeVisual> IGameplayContext._flameSmokeSecondaryVisuals { get => _flameSmokeSecondaryVisuals; }

    List<OpenGarrison.Client.Game1.FlameSmokeVisual> IGameplayContext._flameSmokeVisuals { get => _flameSmokeVisuals; }

    Microsoft.Xna.Framework.Input.MouseState IGameplayContext._frameMouseState { get => _frameMouseState; set => _frameMouseState = value; }

    Microsoft.Xna.Framework.Input.MouseState IGameplayContext._frameRawMouseState { get => _frameRawMouseState; set => _frameRawMouseState = value; }

    bool IGameplayContext._friendsMenuOpen { get => _friendsMenuOpen; set => _friendsMenuOpen = value; }

    Task<OpenGarrison.ClientShared.GameplaySessionCreateResponse> IGameplayContext._gameplayAccountSessionTask { get => _gameplayAccountSessionTask; set => _gameplayAccountSessionTask = value; }

    System.DateTimeOffset IGameplayContext._gameplayAccountTokenExpiresAt { get => _gameplayAccountTokenExpiresAt; set => _gameplayAccountTokenExpiresAt = value; }

    bool IGameplayContext._gameplayHudHidden { get => _gameplayHudHidden; set => _gameplayHudHidden = value; }

    OpenGarrison.Client.LoadedSpriteFrame IGameplayContext._gameplayLoadoutBackButtonTexture { get => _gameplayLoadoutBackButtonTexture; set => _gameplayLoadoutBackButtonTexture = value; }

    OpenGarrison.Client.LoadedSpriteFrame IGameplayContext._gameplayLoadoutBackgroundBarTexture { get => _gameplayLoadoutBackgroundBarTexture; set => _gameplayLoadoutBackgroundBarTexture = value; }

    OpenGarrison.Client.LoadedSpriteFrame IGameplayContext._gameplayLoadoutClassSelectionTexture { get => _gameplayLoadoutClassSelectionTexture; set => _gameplayLoadoutClassSelectionTexture = value; }

    OpenGarrison.Client.LoadedSpriteFrame IGameplayContext._gameplayLoadoutClassStripTexture { get => _gameplayLoadoutClassStripTexture; set => _gameplayLoadoutClassStripTexture = value; }

    OpenGarrison.Client.LoadedSpriteFrame IGameplayContext._gameplayLoadoutDescriptionBoardTexture { get => _gameplayLoadoutDescriptionBoardTexture; set => _gameplayLoadoutDescriptionBoardTexture = value; }

    OpenGarrison.Client.LoadedSpriteFrame IGameplayContext._gameplayLoadoutDogTagsTexture { get => _gameplayLoadoutDogTagsTexture; set => _gameplayLoadoutDogTagsTexture = value; }

    OpenGarrison.Client.LoadedSpriteFrame IGameplayContext._gameplayLoadoutHelmetTexture { get => _gameplayLoadoutHelmetTexture; set => _gameplayLoadoutHelmetTexture = value; }

    bool IGameplayContext._gameplayLoadoutMenuAwaitingEscapeRelease { get => _gameplayLoadoutMenuAwaitingEscapeRelease; set => _gameplayLoadoutMenuAwaitingEscapeRelease = value; }

    int IGameplayContext._gameplayLoadoutMenuHoverIndex { get => _gameplayLoadoutMenuHoverIndex; set => _gameplayLoadoutMenuHoverIndex = value; }

    bool IGameplayContext._gameplayLoadoutMenuOpen { get => _gameplayLoadoutMenuOpen; set => _gameplayLoadoutMenuOpen = value; }

    OpenGarrison.Client.LoadedSpriteFrame IGameplayContext._gameplayLoadoutPageTexture { get => _gameplayLoadoutPageTexture; set => _gameplayLoadoutPageTexture = value; }

    OpenGarrison.Client.LoadedSpriteFrame IGameplayContext._gameplayLoadoutScrollerTexture { get => _gameplayLoadoutScrollerTexture; set => _gameplayLoadoutScrollerTexture = value; }

    List<OpenGarrison.Client.LoadedSpriteFrame> IGameplayContext._gameplayLoadoutSelectionAtlasChunks { get => _gameplayLoadoutSelectionAtlasChunks; }

    OpenGarrison.Client.LoadedSpriteFrame IGameplayContext._gameplayLoadoutSelectionAtlasTexture { get => _gameplayLoadoutSelectionAtlasTexture; set => _gameplayLoadoutSelectionAtlasTexture = value; }

    OpenGarrison.Client.LoadedSpriteFrame IGameplayContext._gameplayLoadoutSelectionTexture { get => _gameplayLoadoutSelectionTexture; set => _gameplayLoadoutSelectionTexture = value; }

    bool IGameplayContext._gameplayModalOwnedInputThisFrame { get => _gameplayModalOwnedInputThisFrame; set => _gameplayModalOwnedInputThisFrame = value; }

    OpenGarrison.Client.GameplayModAssetCache IGameplayContext._gameplayModAssets { get => _gameplayModAssets; set => _gameplayModAssets = value; }

    OpenGarrison.Client.Game1.GameplaySessionKind IGameplayContext._gameplaySessionKind { get => _gameplaySessionKind; set => _gameplaySessionKind = value; }

    Microsoft.Xna.Framework.Graphics.RenderTarget2D IGameplayContext._gameRenderTarget { get => _gameRenderTarget; set => _gameRenderTarget = value; }

    Microsoft.Xna.Framework.Graphics.Effect IGameplayContext._grayscaleEffect { get => _grayscaleEffect; set => _grayscaleEffect = value; }

    bool IGameplayContext._hasLatestLocalAimWorldPosition { get => _hasLatestLocalAimWorldPosition; set => _hasLatestLocalAimWorldPosition = value; }

    bool IGameplayContext._hasLatestNetworkInputAimOrigin { get => _hasLatestNetworkInputAimOrigin; set => _hasLatestNetworkInputAimOrigin = value; }

    bool IGameplayContext._hasLocalPlayerRenderTime { get => _hasLocalPlayerRenderTime; set => _hasLocalPlayerRenderTime = value; }

    bool IGameplayContext._hasPredictedLocalActionState { get => _hasPredictedLocalActionState; set => _hasPredictedLocalActionState = value; }

    bool IGameplayContext._hasPredictedLocalPlayerPosition { get => _hasPredictedLocalPlayerPosition; set => _hasPredictedLocalPlayerPosition = value; }

    bool IGameplayContext._hasReceivedSnapshot { get => _hasReceivedSnapshot; set => _hasReceivedSnapshot = value; }

    bool IGameplayContext._hasRemotePlayerRenderTime { get => _hasRemotePlayerRenderTime; set => _hasRemotePlayerRenderTime = value; }

    bool IGameplayContext._hasSmoothedLocalPlayerRenderPosition { get => _hasSmoothedLocalPlayerRenderPosition; set => _hasSmoothedLocalPlayerRenderPosition = value; }

    bool IGameplayContext._hudEditorOpen { get => _hudEditorOpen; set => _hudEditorOpen = value; }

    Microsoft.Xna.Framework.Graphics.RenderTarget2D IGameplayContext._hudRenderTarget { get => _hudRenderTarget; set => _hudRenderTarget = value; }

    List<OpenGarrison.Client.Game1.ImpactVisual> IGameplayContext._impactVisuals { get => _impactVisuals; }

    bool IGameplayContext._inGameMenuOpen { get => _inGameMenuOpen; set => _inGameMenuOpen = value; }

    Microsoft.Xna.Framework.Audio.SoundEffect IGameplayContext._ingameMusic { get => _ingameMusic; set => _ingameMusic = value; }

    Microsoft.Xna.Framework.Audio.SoundEffectInstance IGameplayContext._ingameMusicInstance { get => _ingameMusicInstance; set => _ingameMusicInstance = value; }

    OpenGarrison.Client.InputBindingsSettings IGameplayContext._inputBindings { get => _inputBindings; }

    bool IGameplayContext._jumpMenuOpen { get => _jumpMenuOpen; set => _jumpMenuOpen = value; }

    bool IGameplayContext._killCamEnabled { get => _killCamEnabled; set => _killCamEnabled = value; }

    ulong IGameplayContext._lastAppliedSnapshotFrame { get => _lastAppliedSnapshotFrame; set => _lastAppliedSnapshotFrame = value; }

    Nullable<int> IGameplayContext._lastAppliedSnapshotLocalPlayerId { get => _lastAppliedSnapshotLocalPlayerId; set => _lastAppliedSnapshotLocalPlayerId = value; }

    ulong IGameplayContext._lastBufferedSnapshotFrame { get => _lastBufferedSnapshotFrame; set => _lastBufferedSnapshotFrame = value; }

    string IGameplayContext._lastGameplayWindowTitle { get => _lastGameplayWindowTitle; set => _lastGameplayWindowTitle = value; }

    Microsoft.Xna.Framework.Point IGameplayContext._lastKnownMousePosition { get => _lastKnownMousePosition; set => _lastKnownMousePosition = value; }

    double IGameplayContext._lastLocalPlayerRenderTimeClockSeconds { get => _lastLocalPlayerRenderTimeClockSeconds; set => _lastLocalPlayerRenderTimeClockSeconds = value; }

    double IGameplayContext._lastPredictedRenderSmoothingTimeSeconds { get => _lastPredictedRenderSmoothingTimeSeconds; set => _lastPredictedRenderSmoothingTimeSeconds = value; }

    double IGameplayContext._lastRemotePlayerRenderTimeClockSeconds { get => _lastRemotePlayerRenderTimeClockSeconds; set => _lastRemotePlayerRenderTimeClockSeconds = value; }

    double IGameplayContext._lastSnapshotReceivedTimeSeconds { get => _lastSnapshotReceivedTimeSeconds; set => _lastSnapshotReceivedTimeSeconds = value; }

    bool IGameplayContext._lastToDieConnectionPresentationPending { get => _lastToDieConnectionPresentationPending; set => _lastToDieConnectionPresentationPending = value; }

    bool IGameplayContext._lastToDieFailureOverlayOpen { get => _lastToDieFailureOverlayOpen; set => _lastToDieFailureOverlayOpen = value; }

    int IGameplayContext._lastToDieFailureOverlayTicks { get => _lastToDieFailureOverlayTicks; set => _lastToDieFailureOverlayTicks = value; }

    Microsoft.Xna.Framework.Audio.SoundEffect IGameplayContext._lastToDieIngameMusic { get => _lastToDieIngameMusic; set => _lastToDieIngameMusic = value; }

    Microsoft.Xna.Framework.Audio.SoundEffectInstance IGameplayContext._lastToDieIngameMusicInstance { get => _lastToDieIngameMusicInstance; set => _lastToDieIngameMusicInstance = value; }

    OpenGarrison.Client.LoadedSpriteFrame IGameplayContext._lastToDieLogoTexture { get => _lastToDieLogoTexture; set => _lastToDieLogoTexture = value; }

    Microsoft.Xna.Framework.Audio.SoundEffect IGameplayContext._lastToDieMenuMusic { get => _lastToDieMenuMusic; set => _lastToDieMenuMusic = value; }

    Microsoft.Xna.Framework.Audio.SoundEffectInstance IGameplayContext._lastToDieMenuMusicInstance { get => _lastToDieMenuMusicInstance; set => _lastToDieMenuMusicInstance = value; }

    bool IGameplayContext._lastToDieMenuOpen { get => _lastToDieMenuOpen; set => _lastToDieMenuOpen = value; }

    OpenGarrison.Client.LoadedSpriteFrame IGameplayContext._lastToDieMenuPlaqueTexture { get => _lastToDieMenuPlaqueTexture; set => _lastToDieMenuPlaqueTexture = value; }

    OpenGarrison.Client.LoadedSpriteFrame IGameplayContext._lastToDieMenuTextBoxSoloTexture { get => _lastToDieMenuTextBoxSoloTexture; set => _lastToDieMenuTextBoxSoloTexture = value; }

    int IGameplayContext._lastToDiePerkHoverIndex { get => _lastToDiePerkHoverIndex; set => _lastToDiePerkHoverIndex = value; }

    bool IGameplayContext._lastToDiePerkMenuOpen { get => _lastToDiePerkMenuOpen; set => _lastToDiePerkMenuOpen = value; }

    OpenGarrison.Client.Game1.LastToDieRunState IGameplayContext._lastToDieRun { get => _lastToDieRun; set => _lastToDieRun = value; }

    bool IGameplayContext._lastToDieStageClearOverlayOpen { get => _lastToDieStageClearOverlayOpen; set => _lastToDieStageClearOverlayOpen = value; }

    int IGameplayContext._lastToDieStageClearOverlayTicks { get => _lastToDieStageClearOverlayTicks; set => _lastToDieStageClearOverlayTicks = value; }

    bool IGameplayContext._lastToDieSurvivorMenuOpen { get => _lastToDieSurvivorMenuOpen; set => _lastToDieSurvivorMenuOpen = value; }

    float IGameplayContext._latestLocalAimWorldX { get => _latestLocalAimWorldX; set => _latestLocalAimWorldX = value; }

    float IGameplayContext._latestLocalAimWorldY { get => _latestLocalAimWorldY; set => _latestLocalAimWorldY = value; }

    float IGameplayContext._latestNetworkInputAimOriginX { get => _latestNetworkInputAimOriginX; set => _latestNetworkInputAimOriginX = value; }

    float IGameplayContext._latestNetworkInputAimOriginY { get => _latestNetworkInputAimOriginY; set => _latestNetworkInputAimOriginY = value; }

    double IGameplayContext._latestSnapshotReceivedClockSeconds { get => _latestSnapshotReceivedClockSeconds; set => _latestSnapshotReceivedClockSeconds = value; }

    double IGameplayContext._latestSnapshotServerTimeSeconds { get => _latestSnapshotServerTimeSeconds; set => _latestSnapshotServerTimeSeconds = value; }

    string IGameplayContext._levelBackgroundFileFailedPath { get => _levelBackgroundFileFailedPath; set => _levelBackgroundFileFailedPath = value; }

    Microsoft.Xna.Framework.Graphics.Texture2D IGameplayContext._levelBackgroundFileTexture { get => _levelBackgroundFileTexture; set => _levelBackgroundFileTexture = value; }

    OpenGarrison.Core.SimpleLevel IGameplayContext._levelBackgroundFileTextureLevel { get => _levelBackgroundFileTextureLevel; set => _levelBackgroundFileTextureLevel = value; }

    string IGameplayContext._levelBackgroundFileTexturePath { get => _levelBackgroundFileTexturePath; set => _levelBackgroundFileTexturePath = value; }

    bool IGameplayContext._loadingOverlayVisible { get => _loadingOverlayVisible; set => _loadingOverlayVisible = value; }

    OpenGarrison.Client.Game1.OverheadChatMessage IGameplayContext._localOverheadChatMessage { get => _localOverheadChatMessage; set => _localOverheadChatMessage = value; }

    float IGameplayContext._localPlayerInterpolationBackTimeSeconds { get => _localPlayerInterpolationBackTimeSeconds; set => _localPlayerInterpolationBackTimeSeconds = value; }

    double IGameplayContext._localPlayerRenderTimeSeconds { get => _localPlayerRenderTimeSeconds; set => _localPlayerRenderTimeSeconds = value; }

    Nullable<int> IGameplayContext._localPlayerSnapshotEntityId { get => _localPlayerSnapshotEntityId; set => _localPlayerSnapshotEntityId = value; }

    List<OpenGarrison.Client.Game1.LooseSheetVisual> IGameplayContext._looseSheetVisuals { get => _looseSheetVisuals; }

    bool IGameplayContext._mainMenuBottomBarHover { get => _mainMenuBottomBarHover; set => _mainMenuBottomBarHover = value; }

    bool IGameplayContext._mainMenuChromeHidden { get => _mainMenuChromeHidden; set => _mainMenuChromeHidden = value; }

    int IGameplayContext._mainMenuHoverIndex { get => _mainMenuHoverIndex; set => _mainMenuHoverIndex = value; }

    bool IGameplayContext._mainMenuOpen { get => _mainMenuOpen; set => _mainMenuOpen = value; }

    OpenGarrison.Client.Game1.MainMenuPage IGameplayContext._mainMenuPage { get => _mainMenuPage; set => _mainMenuPage = value; }

    OpenGarrison.Core.MenuBackgroundMode IGameplayContext._menuBackgroundMode { get => _menuBackgroundMode; set => _menuBackgroundMode = value; }

    OpenGarrison.Client.LoadedSpriteFrame IGameplayContext._menuBackgroundTexture { get => _menuBackgroundTexture; set => _menuBackgroundTexture = value; }

    string IGameplayContext._menuBackgroundTexturePath { get => _menuBackgroundTexturePath; set => _menuBackgroundTexturePath = value; }

    Dictionary<char, OpenGarrison.Client.Game1.MenuBitmapGlyph> IGameplayContext._menuBitmapFontGlyphs { get => _menuBitmapFontGlyphs; }

    int IGameplayContext._menuBitmapFontLineHeight { get => _menuBitmapFontLineHeight; set => _menuBitmapFontLineHeight = value; }

    OpenGarrison.Client.LoadedSpriteFrame IGameplayContext._menuBitmapFontTexture { get => _menuBitmapFontTexture; set => _menuBitmapFontTexture = value; }

    Microsoft.Xna.Framework.Graphics.SpriteFont IGameplayContext._menuFont { get => _menuFont; set => _menuFont = value; }

    int IGameplayContext._menuImageFrame { get => _menuImageFrame; set => _menuImageFrame = value; }

    Microsoft.Xna.Framework.Audio.SoundEffect IGameplayContext._menuMusic { get => _menuMusic; set => _menuMusic = value; }

    Microsoft.Xna.Framework.Audio.SoundEffectInstance IGameplayContext._menuMusicInstance { get => _menuMusicInstance; set => _menuMusicInstance = value; }

    OpenGarrison.Client.LoadedSpriteFrame IGameplayContext._menuPlaqueTallTexture { get => _menuPlaqueTallTexture; set => _menuPlaqueTallTexture = value; }

    OpenGarrison.Client.LoadedSpriteFrame IGameplayContext._menuPlaqueTexture { get => _menuPlaqueTexture; set => _menuPlaqueTexture = value; }

    string IGameplayContext._menuStatusMessage { get => _menuStatusMessage; set => _menuStatusMessage = value; }

    OpenGarrison.Client.LoadedSpriteFrame IGameplayContext._menuTextBoxBottomTexture { get => _menuTextBoxBottomTexture; set => _menuTextBoxBottomTexture = value; }

    OpenGarrison.Client.LoadedSpriteFrame IGameplayContext._menuTextBoxMiddleTexture { get => _menuTextBoxMiddleTexture; set => _menuTextBoxMiddleTexture = value; }

    OpenGarrison.Client.LoadedSpriteFrame IGameplayContext._menuTextBoxSoloTexture { get => _menuTextBoxSoloTexture; set => _menuTextBoxSoloTexture = value; }

    OpenGarrison.Client.LoadedSpriteFrame IGameplayContext._menuTextBoxTopTexture { get => _menuTextBoxTopTexture; set => _menuTextBoxTopTexture = value; }

    List<OpenGarrison.Client.Game1.MineTrailVisual> IGameplayContext._mineTrailVisuals { get => _mineTrailVisuals; }

    bool IGameplayContext._namePromptPresented { get => _namePromptPresented; set => _namePromptPresented = value; }

    OpenGarrison.Client.NetworkGameClient IGameplayContext._networkClient { get => _networkClient; }

    int IGameplayContext._networkInterpolationWarmupSnapshotsRemaining { get => _networkInterpolationWarmupSnapshotsRemaining; set => _networkInterpolationWarmupSnapshotsRemaining = value; }

    double IGameplayContext._networkInterpolationWarmupUntilClockSeconds { get => _networkInterpolationWarmupUntilClockSeconds; set => _networkInterpolationWarmupUntilClockSeconds = value; }

    Nullable<OpenGarrison.Protocol.LastToDieWirePhase> IGameplayContext._networkPresentationObservedLastToDiePhase { get => _networkPresentationObservedLastToDiePhase; set => _networkPresentationObservedLastToDiePhase = value; }

    float IGameplayContext._networkSnapshotInterpolationDurationSeconds { get => _networkSnapshotInterpolationDurationSeconds; set => _networkSnapshotInterpolationDurationSeconds = value; }

    bool IGameplayContext._networkWorldWarmupAcceptNextAppliedSnapshotAsBaseline { get => _networkWorldWarmupAcceptNextAppliedSnapshotAsBaseline; set => _networkWorldWarmupAcceptNextAppliedSnapshotAsBaseline = value; }

    bool IGameplayContext._networkWorldWarmupActive { get => _networkWorldWarmupActive; set => _networkWorldWarmupActive = value; }

    int IGameplayContext._networkWorldWarmupAppliedSnapshotsAfterFull { get => _networkWorldWarmupAppliedSnapshotsAfterFull; set => _networkWorldWarmupAppliedSnapshotsAfterFull = value; }

    bool IGameplayContext._networkWorldWarmupFullSnapshotApplied { get => _networkWorldWarmupFullSnapshotApplied; set => _networkWorldWarmupFullSnapshotApplied = value; }

    Dictionary<OpenGarrison.Client.LoadedSpriteFrame, OpenGarrison.Client.LoadedSpriteFrame> IGameplayContext._neutralSpriteFrameCache { get => _neutralSpriteFrameCache; }

    int IGameplayContext._nextBloodSquibSeed { get => _nextBloodSquibSeed; set => _nextBloodSquibSeed = value; }

    int IGameplayContext._nextClientBackstabVisualId { get => _nextClientBackstabVisualId; set => _nextClientBackstabVisualId = value; }

    System.DateTimeOffset IGameplayContext._nextGameplayAccountAttachAttemptAt { get => _nextGameplayAccountAttachAttemptAt; set => _nextGameplayAccountAttachAttemptAt = value; }

    string IGameplayContext._observedGameplayLevelName { get => _observedGameplayLevelName; set => _observedGameplayLevelName = value; }

    int IGameplayContext._observedGameplayMapAreaIndex { get => _observedGameplayMapAreaIndex; set => _observedGameplayMapAreaIndex = value; }

    Nullable<ValueTuple<string, int>> IGameplayContext._offlinePracticeNextMap { get => _offlinePracticeNextMap; set => _offlinePracticeNextMap = value; }

    bool IGameplayContext._offlinePracticeSpectatorMode { get => _offlinePracticeSpectatorMode; set => _offlinePracticeSpectatorMode = value; }

    OpenGarrison.Client.Game1.OnlineConnectionIntent IGameplayContext._onlineConnectionIntent { get => _onlineConnectionIntent; set => _onlineConnectionIntent = value; }

    bool IGameplayContext._optionsMenuOpen { get => _optionsMenuOpen; set => _optionsMenuOpen = value; }

    bool IGameplayContext._optionsMenuOpenedFromGameplay { get => _optionsMenuOpenedFromGameplay; set => _optionsMenuOpenedFromGameplay = value; }

    int IGameplayContext._optionsPageIndex { get => _optionsPageIndex; set => _optionsPageIndex = value; }

    Dictionary<byte, OpenGarrison.Client.Game1.OverheadChatMessage> IGameplayContext._overheadChatMessagesBySlot { get => _overheadChatMessagesBySlot; }

    int IGameplayContext._particleMode { get => _particleMode; set => _particleMode = value; }

    bool IGameplayContext._passwordPromptOpen { get => _passwordPromptOpen; set => _passwordPromptOpen = value; }

    OpenGarrison.Client.PlayerHostedRoomSession IGameplayContext._peerRoomSession { get => _peerRoomSession; set => _peerRoomSession = value; }

    Nullable<OpenGarrison.Core.PlayerTeam> IGameplayContext._pendingClassSelectTeam { get => _pendingClassSelectTeam; set => _pendingClassSelectTeam = value; }

    Nullable<OpenGarrison.Client.Game1.ControllerControlsMenuBinding> IGameplayContext._pendingControllerControlsBinding { get => _pendingControllerControlsBinding; set => _pendingControllerControlsBinding = value; }

    Nullable<OpenGarrison.Client.Game1.ControlsMenuBinding> IGameplayContext._pendingControlsBinding { get => _pendingControlsBinding; set => _pendingControlsBinding = value; }

    ulong IGameplayContext._pendingGameplayAccountAttachRequestId { get => _pendingGameplayAccountAttachRequestId; set => _pendingGameplayAccountAttachRequestId = value; }

    int IGameplayContext._pendingHostedConnectPort { get => _pendingHostedConnectPort; set => _pendingHostedConnectPort = value; }

    int IGameplayContext._pendingHostedConnectTicks { get => _pendingHostedConnectTicks; set => _pendingHostedConnectTicks = value; }

    List<OpenGarrison.Protocol.SnapshotDamageEvent> IGameplayContext._pendingNetworkDamageEvents { get => _pendingNetworkDamageEvents; }

    List<OpenGarrison.Core.WorldSoundEvent> IGameplayContext._pendingNetworkSoundEvents { get => _pendingNetworkSoundEvents; }

    List<OpenGarrison.Protocol.SnapshotVisualEvent> IGameplayContext._pendingNetworkVisualEvents { get => _pendingNetworkVisualEvents; }

    List<OpenGarrison.Client.Game1.PredictedLocalInput> IGameplayContext._pendingPredictedInputs { get => _pendingPredictedInputs; }

    List<ValueTuple<int, int, float, bool>> IGameplayContext._pendingSettledBloodTransfers { get => _pendingSettledBloodTransfers; }

    List<OpenGarrison.Client.Game1.PendingWeaponShellVisual> IGameplayContext._pendingWeaponShellVisuals { get => _pendingWeaponShellVisuals; }

    Microsoft.Xna.Framework.Graphics.Texture2D IGameplayContext._pixel { get => _pixel; set => _pixel = value; }

    string IGameplayContext._playerNameEditBuffer { get => _playerNameEditBuffer; set => _playerNameEditBuffer = value; }

    bool IGameplayContext._pluginOptionsMenuOpen { get => _pluginOptionsMenuOpen; set => _pluginOptionsMenuOpen = value; }

    bool IGameplayContext._pluginOptionsMenuOpenedFromGameplay { get => _pluginOptionsMenuOpenedFromGameplay; set => _pluginOptionsMenuOpenedFromGameplay = value; }

    int IGameplayContext._practiceCapLimit { get => _practiceCapLimit; set => _practiceCapLimit = value; }

    List<OpenGarrison.Client.Game1.PracticeMapEntry> IGameplayContext._practiceMapEntries { get => _practiceMapEntries; set => _practiceMapEntries = value; }

    int IGameplayContext._practiceRespawnSeconds { get => _practiceRespawnSeconds; set => _practiceRespawnSeconds = value; }

    int IGameplayContext._practiceSessionElapsedTicks { get => _practiceSessionElapsedTicks; set => _practiceSessionElapsedTicks = value; }

    bool IGameplayContext._practiceSetupOpen { get => _practiceSetupOpen; set => _practiceSetupOpen = value; }

    bool IGameplayContext._practiceStickyGibBloodEnabled { get => _practiceStickyGibBloodEnabled; set => _practiceStickyGibBloodEnabled = value; }

    int IGameplayContext._practiceTickRate { get => _practiceTickRate; set => _practiceTickRate = value; }

    int IGameplayContext._practiceTimeLimitMinutes { get => _practiceTimeLimitMinutes; set => _practiceTimeLimitMinutes = value; }

    Microsoft.Xna.Framework.Vector2 IGameplayContext._predictedLocalPlayerRenderCorrectionOffset { get => _predictedLocalPlayerRenderCorrectionOffset; set => _predictedLocalPlayerRenderCorrectionOffset = value; }

    OpenGarrison.Core.PlayerEntity IGameplayContext._predictedLocalPlayerShadow { get => _predictedLocalPlayerShadow; set => _predictedLocalPlayerShadow = value; }

    int IGameplayContext._prePredictionFlameCount { get => _prePredictionFlameCount; set => _prePredictionFlameCount = value; }

    List<OpenGarrison.Client.Game1.PresentedExplosionVisual> IGameplayContext._presentedExplosionVisualsThisFrame { get => _presentedExplosionVisualsThisFrame; }

    Microsoft.Xna.Framework.Input.KeyboardState IGameplayContext._previousKeyboard { get => _previousKeyboard; set => _previousKeyboard = value; }

    Microsoft.Xna.Framework.Input.MouseState IGameplayContext._previousMouse { get => _previousMouse; set => _previousMouse = value; }

    HashSet<int> IGameplayContext._processedSettledBloodDropIds { get => _processedSettledBloodDropIds; }

    HashSet<int> IGameplayContext._processedStickyGibBloodDropIds { get => _processedStickyGibBloodDropIds; }

    float IGameplayContext._projectileInterpolationBackTimeSeconds { get => _projectileInterpolationBackTimeSeconds; set => _projectileInterpolationBackTimeSeconds = value; }

    int IGameplayContext._quitPromptHoverIndex { get => _quitPromptHoverIndex; set => _quitPromptHoverIndex = value; }

    bool IGameplayContext._quitPromptOpen { get => _quitPromptOpen; set => _quitPromptOpen = value; }

    float IGameplayContext._remotePlayerInterpolationBackTimeSeconds { get => _remotePlayerInterpolationBackTimeSeconds; set => _remotePlayerInterpolationBackTimeSeconds = value; }

    double IGameplayContext._remotePlayerRenderTimeSeconds { get => _remotePlayerRenderTimeSeconds; set => _remotePlayerRenderTimeSeconds = value; }

    bool IGameplayContext._replaySeekCatchUpActive { get => _replaySeekCatchUpActive; set => _replaySeekCatchUpActive = value; }

    int IGameplayContext._replaySeekTargetMilliseconds { get => _replaySeekTargetMilliseconds; set => _replaySeekTargetMilliseconds = value; }

    List<OpenGarrison.Client.Game1.RocketSmokeVisual> IGameplayContext._rocketSmokeVisuals { get => _rocketSmokeVisuals; }

    OpenGarrison.Client.RotatedWeaponSpriteCache IGameplayContext._rotatedWeaponSprites { get => _rotatedWeaponSprites; set => _rotatedWeaponSprites = value; }

    OpenGarrison.Client.GameMakerRuntimeAssetCache IGameplayContext._runtimeAssets { get => _runtimeAssets; set => _runtimeAssets = value; }

    OpenGarrison.ClientShared.ClientRuntimeComposition IGameplayContext._runtimeComposition { get => _runtimeComposition; set => _runtimeComposition = value; }

    bool IGameplayContext._scoreboardOpen { get => _scoreboardOpen; set => _scoreboardOpen = value; }

    bool IGameplayContext._serverLocalPredictionEnabled { get => _serverLocalPredictionEnabled; set => _serverLocalPredictionEnabled = value; }

    Dictionary<ValueTuple<int, int>, OpenGarrison.Client.Game1.SettledBloodCell> IGameplayContext._settledBloodCells { get => _settledBloodCells; }

    List<OpenGarrison.Client.Game1.ShellVisual> IGameplayContext._shellVisuals { get => _shellVisuals; }

    float IGameplayContext._smoothedSnapshotIntervalSeconds { get => _smoothedSnapshotIntervalSeconds; set => _smoothedSnapshotIntervalSeconds = value; }

    float IGameplayContext._smoothedSnapshotJitterSeconds { get => _smoothedSnapshotJitterSeconds; set => _smoothedSnapshotJitterSeconds = value; }

    Microsoft.Xna.Framework.Graphics.SpriteBatch IGameplayContext._spriteBatch { get => _spriteBatch; set => _spriteBatch = value; }

    Dictionary<OpenGarrison.Client.LoadedSpriteFrame, Microsoft.Xna.Framework.Rectangle> IGameplayContext._spriteFontOpaqueBoundsCache { get => _spriteFontOpaqueBoundsCache; }

    List<ValueTuple<int, int>> IGameplayContext._staleSettledBloodCellKeys { get => _staleSettledBloodCellKeys; }

    List<int> IGameplayContext._staleSettledBloodDropIds { get => _staleSettledBloodDropIds; }

    List<int> IGameplayContext._staleStickyGibBloodDropIds { get => _staleStickyGibBloodDropIds; }

    List<int> IGameplayContext._staleStickyGibBloodPlayerIds { get => _staleStickyGibBloodPlayerIds; }

    OpenGarrison.Client.GameStartupMode IGameplayContext._startupMode { get => _startupMode; }

    bool IGameplayContext._startupSplashOpen { get => _startupSplashOpen; set => _startupSplashOpen = value; }

    Dictionary<int, OpenGarrison.Client.Game1.StickyGibBloodCoating> IGameplayContext._stickyGibBloodCoatings { get => _stickyGibBloodCoatings; }

    bool IGameplayContext._stuckArrowsEnabled { get => _stuckArrowsEnabled; set => _stuckArrowsEnabled = value; }

    List<OpenGarrison.Client.Game1.StuckArrowVisual> IGameplayContext._stuckArrowVisuals { get => _stuckArrowVisuals; }

    bool IGameplayContext._suppressFullscreenToggleUntilRelease { get => _suppressFullscreenToggleUntilRelease; set => _suppressFullscreenToggleUntilRelease = value; }

    float IGameplayContext._teamSelectAlpha { get => _teamSelectAlpha; set => _teamSelectAlpha = value; }

    bool IGameplayContext._teamSelectOpen { get => _teamSelectOpen; set => _teamSelectOpen = value; }

    System.Random IGameplayContext._visualRandom { get => _visualRandom; }

    OpenGarrison.Client.VoiceChatClient IGameplayContext._voiceChat { get => _voiceChat; set => _voiceChat = value; }

    bool IGameplayContext._voteMenuOpen { get => _voteMenuOpen; set => _voteMenuOpen = value; }

    List<OpenGarrison.Client.Game1.WallspinDustVisual> IGameplayContext._wallspinDustVisuals { get => _wallspinDustVisuals; }

    bool IGameplayContext._wasDeathCamActive { get => _wasDeathCamActive; set => _wasDeathCamActive = value; }

    bool IGameplayContext._wasMatchEnded { get => _wasMatchEnded; set => _wasMatchEnded = value; }

    bool IGameplayContext._wasWindowActive { get => _wasWindowActive; set => _wasWindowActive = value; }

    OpenGarrison.Client.WindowInputFilter IGameplayContext._windowInputFilter { get => _windowInputFilter; }

    OpenGarrison.Core.SimulationWorld IGameplayContext._world { get => _world; set => _world = value; }

    bool IGameplayContext.AreBloodVisualsEnabled { get => AreBloodVisualsEnabled; }

    Microsoft.Xna.Framework.Content.ContentManager IGameplayContext.Content { get => Content; set => Content = value; }

    Microsoft.Xna.Framework.Graphics.GraphicsDevice IGameplayContext.GraphicsDevice { get => GraphicsDevice; }

    bool IGameplayContext.HasManagedRoom { get => HasManagedRoom; }

    bool IGameplayContext.IsMouseVisible { get => IsMouseVisible; set => IsMouseVisible = value; }

    bool IGameplayContext.IsPracticeSessionActive { get => IsPracticeSessionActive; }

    bool IGameplayContext.IsWindowInputActive { get => IsWindowInputActive; }

    bool IGameplayContext.UseReducedBrowserEffects { get => UseReducedBrowserEffects; }

    int IGameplayContext.ViewportHeight { get => ViewportHeight; }

    int IGameplayContext.ViewportWidth { get => ViewportWidth; }

    Microsoft.Xna.Framework.GameWindow IGameplayContext.Window { get => Window; }

    MenuManager IGameplayContext.Menus => _menuManager;

    SessionManager IGameplayContext.Session => _sessionManager;

    GameplayManager IGameplayContext.Gameplay => _gameplayManager;

    void IGameplayContext.AccumulateProceduralFlameParticle(Dictionary<ValueTuple<int, int>, float> cells, int seed, float centerX, float centerY, float scale, float alphaScale, float motionX = 0f, float motionY = 0f, float trajectoryStretch = 1f, bool includeHornAccent = false) { AccumulateProceduralFlameParticle(cells, seed, centerX, centerY, scale, alphaScale, motionX, motionY, trajectoryStretch, includeHornAccent); }

    void IGameplayContext.AddConsoleLine(string line) { AddConsoleLine(line); }

    void IGameplayContext.AddNetworkConsoleLine(string message) { AddNetworkConsoleLine(message); }

    bool IGameplayContext.AdvanceBrowserGameplayWarmup() => AdvanceBrowserGameplayWarmup();

    void IGameplayContext.AdvanceCorpseAcidDissolves() { AdvanceCorpseAcidDissolves(); }

    void IGameplayContext.AdvanceDynamicRagdolls() { AdvanceDynamicRagdolls(); }

    void IGameplayContext.AdvanceFlameSmokeVisuals() { AdvanceFlameSmokeVisuals(); }

    void IGameplayContext.AdvanceGameplaySimulation(Microsoft.Xna.Framework.GameTime gameTime, OpenGarrison.Core.PlayerInputSnapshot networkInput) { AdvanceGameplaySimulation(gameTime, networkInput); }

    void IGameplayContext.AdvanceMenuClientTicks(int ticks) { AdvanceMenuClientTicks(ticks); }

    void IGameplayContext.AdvanceRecentPredictedAirBlastVisuals() { AdvanceRecentPredictedAirBlastVisuals(); }

    void IGameplayContext.AdvanceRecentPredictedExplosionVisuals() { AdvanceRecentPredictedExplosionVisuals(); }

    void IGameplayContext.AdvanceStartupSplashTicks(int ticks, Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { AdvanceStartupSplashTicks(ticks, keyboard, mouse); }

    void IGameplayContext.ApplyAudioMuteState() { ApplyAudioMuteState(); }

    void IGameplayContext.ApplyLastToDieStageEnemyModifiers() { ApplyLastToDieStageEnemyModifiers(); }

    OpenGarrison.Core.PlayerInputSnapshot IGameplayContext.ApplyPendingInputEdges(OpenGarrison.Core.PlayerInputSnapshot input) => ApplyPendingInputEdges(input);

    void IGameplayContext.ApplyPracticeDummyPreferencesBeforeJoin() { ApplyPracticeDummyPreferencesBeforeJoin(); }

    void IGameplayContext.ApplySelectedLastToDieSurvivorToCurrentStage() { ApplySelectedLastToDieSurvivorToCurrentStage(); }

    void IGameplayContext.BeginBrowserGameplayWarmup() { BeginBrowserGameplayWarmup(); }

    void IGameplayContext.BeginClosingBuildMenu() { BeginClosingBuildMenu(); }

    void IGameplayContext.BeginGameplayAccountAttach() { BeginGameplayAccountAttach(); }

    void IGameplayContext.BeginGameplayWorldSpriteBatch(Microsoft.Xna.Framework.Graphics.RasterizerState rasterizerState) { BeginGameplayWorldSpriteBatch(rasterizerState); }

    void IGameplayContext.BeginLogicalFrame(Microsoft.Xna.Framework.Color clearColor) { BeginLogicalFrame(clearColor); }

    void IGameplayContext.BeginNetworkWorldWarmup(string levelName) { BeginNetworkWorldWarmup(levelName); }

    void IGameplayContext.BeginPendingHostedLocalConnect(int port, int delayTicks, string statusMessage) { BeginPendingHostedLocalConnect(port, delayTicks, statusMessage); }

    ValueTuple<OpenGarrison.Core.PlayerInputSnapshot, OpenGarrison.Core.PlayerInputSnapshot> IGameplayContext.BuildGameplayInputs(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse, Microsoft.Xna.Framework.Vector2 cameraPosition, float deltaSeconds) => BuildGameplayInputs(keyboard, mouse, cameraPosition, deltaSeconds);

    void IGameplayContext.CancelPracticeNavigationWarmup() { CancelPracticeNavigationWarmup(); }

    bool IGameplayContext.CanOpenGameplayChat() => CanOpenGameplayChat();

    bool IGameplayContext.CanOpenInGamePauseMenu() => CanOpenInGamePauseMenu();

    bool IGameplayContext.CanToggleGameplaySelectionMenus() => CanToggleGameplaySelectionMenus();

    bool IGameplayContext.CanUseGameplayChatShortcut() => CanUseGameplayChatShortcut();

    void IGameplayContext.CapturePendingPredictedInputEdges(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse, OpenGarrison.Core.PlayerInputSnapshot networkInput) { CapturePendingPredictedInputEdges(keyboard, mouse, networkInput); }

    void IGameplayContext.ClearHostedSocialPresenceEndpoint() { ClearHostedSocialPresenceEndpoint(); }

    void IGameplayContext.ClearLastToDieDeathFocusPresentation() { ClearLastToDieDeathFocusPresentation(); }

    void IGameplayContext.ClearManualPracticeBotRequests() { ClearManualPracticeBotRequests(); }

    void IGameplayContext.ClearOnlinePlayerSocialProfiles() { ClearOnlinePlayerSocialProfiles(); }

    void IGameplayContext.ClearPendingNetworkMapSync() { ClearPendingNetworkMapSync(); }

    void IGameplayContext.ClearPendingSecondaryAbilityPress() { ClearPendingSecondaryAbilityPress(); }

    void IGameplayContext.ClearRemoteCustomBubbleStates() { ClearRemoteCustomBubbleStates(); }

    void IGameplayContext.ClearReplayQueue(bool clearActiveReplayPath) { ClearReplayQueue(clearActiveReplayPath); }

    void IGameplayContext.ClearSocialPresenceNetworkEndpoint() { ClearSocialPresenceNetworkEndpoint(); }

    void IGameplayContext.CloseGameplayOverlayState() { CloseGameplayOverlayState(); }

    void IGameplayContext.CloseGameplaySelectionMenus() { CloseGameplaySelectionMenus(); }

    void IGameplayContext.CloseLobbyBrowser(bool clearStatus) { CloseLobbyBrowser(clearStatus); }

    void IGameplayContext.CloseMainMenuOverlayState() { CloseMainMenuOverlayState(); }

    int IGameplayContext.ConsumeClientTickCount(Microsoft.Xna.Framework.GameTime gameTime) => ConsumeClientTickCount(gameTime);

    void IGameplayContext.CycleGameplayCameraZoom() { CycleGameplayCameraZoom(); }

    void IGameplayContext.DismissCustomBubbleEditor() { DismissCustomBubbleEditor(); }

    void IGameplayContext.DispatchClientSemanticGameplayEvents() { DispatchClientSemanticGameplayEvents(); }

    void IGameplayContext.DisposeBrandLogoAssets() { DisposeBrandLogoAssets(); }

    void IGameplayContext.DisposeDamageVignetteTextures() { DisposeDamageVignetteTextures(); }

    void IGameplayContext.DisposeGameplayMissPopupFrame() { DisposeGameplayMissPopupFrame(); }

    void IGameplayContext.DisposeGarrisonBuilderEditorAssets() { DisposeGarrisonBuilderEditorAssets(); }

    void IGameplayContext.DisposeLastToDieBuffIconFrame() { DisposeLastToDieBuffIconFrame(); }

    void IGameplayContext.DisposeLastToDieSurvivorCarouselAssets() { DisposeLastToDieSurvivorCarouselAssets(); }

    void IGameplayContext.DisposeReplayPlaybackControlAssets() { DisposeReplayPlaybackControlAssets(); }

    void IGameplayContext.DrawClassSelectHud() { DrawClassSelectHud(); }

    bool IGameplayContext.DrawDeathCamCaptureOverlay(int viewportWidth, int viewportHeight) => DrawDeathCamCaptureOverlay(viewportWidth, viewportHeight);

    void IGameplayContext.DrawGameplayHudLayersOrComposite(Microsoft.Xna.Framework.Input.MouseState mouse, Microsoft.Xna.Framework.Vector2 cameraPosition) { DrawGameplayHudLayersOrComposite(mouse, cameraPosition); }

    void IGameplayContext.DrawGameplayModalOverlays(Microsoft.Xna.Framework.Input.MouseState mouse, Microsoft.Xna.Framework.Vector2 cameraPosition) { DrawGameplayModalOverlays(mouse, cameraPosition); }

    void IGameplayContext.DrawGameplayWorld(Microsoft.Xna.Framework.Vector2 cameraPosition, int viewportWidth, int viewportHeight, Microsoft.Xna.Framework.Rectangle worldRectangle, Microsoft.Xna.Framework.Rectangle playerRectangle, Microsoft.Xna.Framework.Rectangle centerLine, Microsoft.Xna.Framework.Rectangle centerColumn, Microsoft.Xna.Framework.Rectangle worldTopBorder, Microsoft.Xna.Framework.Rectangle worldBottomBorder, Microsoft.Xna.Framework.Rectangle worldLeftBorder, Microsoft.Xna.Framework.Rectangle worldRightBorder, Microsoft.Xna.Framework.Rectangle spawnRectangle, Nullable<int> skippedDeadBodySourcePlayerId = default) { DrawGameplayWorld(cameraPosition, viewportWidth, viewportHeight, worldRectangle, playerRectangle, centerLine, centerColumn, worldTopBorder, worldBottomBorder, worldLeftBorder, worldRightBorder, spawnRectangle, skippedDeadBodySourcePlayerId); }

    bool IGameplayContext.DrawLastToDieDeathFocusOverlay(int viewportWidth, int viewportHeight) => DrawLastToDieDeathFocusOverlay(viewportWidth, viewportHeight);

    void IGameplayContext.DrawLoadedSpriteFrame(OpenGarrison.Client.LoadedSpriteFrame frame, Microsoft.Xna.Framework.Rectangle destinationRectangle, Microsoft.Xna.Framework.Color tint) { DrawLoadedSpriteFrame(frame, destinationRectangle, tint); }

    void IGameplayContext.DrawLoadedSpriteFrame(OpenGarrison.Client.LoadedSpriteFrame frame, Microsoft.Xna.Framework.Vector2 position, Nullable<Microsoft.Xna.Framework.Rectangle> sourceRectangle, Microsoft.Xna.Framework.Color tint, float rotation, Microsoft.Xna.Framework.Vector2 origin, Microsoft.Xna.Framework.Vector2 scale, Microsoft.Xna.Framework.Graphics.SpriteEffects effects, float layerDepth) { DrawLoadedSpriteFrame(frame, position, sourceRectangle, tint, rotation, origin, scale, effects, layerDepth); }

    void IGameplayContext.DrawLoadingOverlay() { DrawLoadingOverlay(); }

    void IGameplayContext.DrawProceduralFlameParticles(Dictionary<ValueTuple<int, int>, float> cells, Microsoft.Xna.Framework.Vector2 cameraPosition, bool topOutlineOnly = false, float drawAlpha = 1f) { DrawProceduralFlameParticles(cells, cameraPosition, topOutlineOnly, drawAlpha); }

    void IGameplayContext.DrawSoftwareMenuCursor(Microsoft.Xna.Framework.Input.MouseState mouse) { DrawSoftwareMenuCursor(mouse); }

    void IGameplayContext.DrawStabAnimation(OpenGarrison.Core.StabAnimEntity stabAnimation, Microsoft.Xna.Framework.Vector2 cameraPosition) { DrawStabAnimation(stabAnimation, cameraPosition); }

    void IGameplayContext.DrawStartupSplash() { DrawStartupSplash(); }

    void IGameplayContext.DrawTeamSelectHud() { DrawTeamSelectHud(); }

    void IGameplayContext.DrawVersionOverlay() { DrawVersionOverlay(); }

    void IGameplayContext.DrawVoiceParticipants() { DrawVoiceParticipants(); }

    void IGameplayContext.DrawVotePresentationOverlay() { DrawVotePresentationOverlay(); }

    void IGameplayContext.EndGameplayWorldSpriteBatch() { EndGameplayWorldSpriteBatch(); }

    void IGameplayContext.EndLogicalFrame() { EndLogicalFrame(); }

    void IGameplayContext.EnsureAutomaticDemoRecordingForConnection(string serverLabel) { EnsureAutomaticDemoRecordingForConnection(serverLabel); }

    void IGameplayContext.EnsureWindowInactiveInputReleased(bool windowActive, Microsoft.Xna.Framework.Input.MouseState releasedMouse) { EnsureWindowInactiveInputReleased(windowActive, releasedMouse); }

    IEnumerable<OpenGarrison.Core.PlayerEntity> IGameplayContext.EnumerateRenderablePlayers() => EnumerateRenderablePlayers();

    void IGameplayContext.FinalizeGameplayFrame(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { FinalizeGameplayFrame(keyboard, mouse); }

    OpenGarrison.Core.PlayerEntity IGameplayContext.FindPlayerById(int playerId) => FindPlayerById(playerId);

    float IGameplayContext.GetBloodPersistenceScale() => GetBloodPersistenceScale();

    string IGameplayContext.GetBrowserGameplayWarmupStatusMessage() => GetBrowserGameplayWarmupStatusMessage();

    Microsoft.Xna.Framework.Vector2 IGameplayContext.GetCameraTopLeft(int viewportWidth, int viewportHeight, int mouseX, int mouseY) => GetCameraTopLeft(viewportWidth, viewportHeight, mouseX, mouseY);

    Microsoft.Xna.Framework.Input.MouseState IGameplayContext.GetConstrainedMouseState(Microsoft.Xna.Framework.Input.MouseState rawMouse) => GetConstrainedMouseState(rawMouse);

    Microsoft.Xna.Framework.Vector2 IGameplayContext.GetFlameScaledCenterOfMassWorldPosition(OpenGarrison.Core.FlameProjectileEntity flame) => GetFlameScaledCenterOfMassWorldPosition(flame);

    Microsoft.Xna.Framework.Input.MouseState IGameplayContext.GetFrameMouseState() => GetFrameMouseState();

    Microsoft.Xna.Framework.Input.MouseState IGameplayContext.GetFrameRawMouseState() => GetFrameRawMouseState();

    int IGameplayContext.GetGameplayCameraViewportHeight(int viewportHeight) => GetGameplayCameraViewportHeight(viewportHeight);

    Microsoft.Xna.Framework.Vector2 IGameplayContext.GetGameplayInputAimOrigin() => GetGameplayInputAimOrigin();

    Microsoft.Xna.Framework.Vector2 IGameplayContext.GetGameplayInputCameraTopLeft(int viewportWidth, int viewportHeight, int mouseX, int mouseY) => GetGameplayInputCameraTopLeft(viewportWidth, viewportHeight, mouseX, mouseY);

    Microsoft.Xna.Framework.Point IGameplayContext.GetGameplayWorldViewport(int viewportWidth, int viewportHeight) => GetGameplayWorldViewport(viewportWidth, viewportHeight);

    int IGameplayContext.GetLastToDieStageIntroDurationTicks() => GetLastToDieStageIntroDurationTicks();

    Microsoft.Xna.Framework.Rectangle IGameplayContext.GetLocalPlayerRectangle(Microsoft.Xna.Framework.Vector2 cameraPosition) => GetLocalPlayerRectangle(cameraPosition);

    float IGameplayContext.GetMinimumLocalPlayerInterpolationBackTimeSeconds() => GetMinimumLocalPlayerInterpolationBackTimeSeconds();

    float IGameplayContext.GetMinimumRemotePlayerInterpolationBackTimeSeconds() => GetMinimumRemotePlayerInterpolationBackTimeSeconds();

    int IGameplayContext.GetPlayerStateKey(OpenGarrison.Core.PlayerEntity player) => GetPlayerStateKey(player);

    float IGameplayContext.GetPlayerVisibilityAlpha(OpenGarrison.Core.PlayerEntity player) => GetPlayerVisibilityAlpha(player);

    OpenGarrison.Core.ExperimentalGameplaySettings IGameplayContext.GetPracticeExperimentalGameplaySettings() => GetPracticeExperimentalGameplaySettings();

    double IGameplayContext.GetProjectileRenderTimeSeconds() => GetProjectileRenderTimeSeconds();

    Microsoft.Xna.Framework.Vector2 IGameplayContext.GetRenderPosition(int entityId, float x, float y, bool allowInterpolation = true) => GetRenderPosition(entityId, x, y, allowInterpolation);

    Microsoft.Xna.Framework.Vector2 IGameplayContext.GetRenderPosition(OpenGarrison.Core.PlayerEntity player, bool allowInterpolation = true) => GetRenderPosition(player, allowInterpolation);

    OpenGarrison.Client.LoadedGameMakerSprite IGameplayContext.GetResolvedSprite(string spriteName) => GetResolvedSprite(spriteName);

    Microsoft.Xna.Framework.Input.MouseState IGameplayContext.GetScaledMouseState(Microsoft.Xna.Framework.Input.MouseState rawMouse) => GetScaledMouseState(rawMouse);

    OpenGarrison.Client.Game1.PracticeMapEntry IGameplayContext.GetSelectedPracticeMapEntry() => GetSelectedPracticeMapEntry();

    Microsoft.Xna.Framework.Vector2 IGameplayContext.GetWeaponShellSpawnOrigin(OpenGarrison.Core.PlayerEntity player) => GetWeaponShellSpawnOrigin(player);

    bool IGameplayContext.HandleActiveTextFieldKeyboardShortcuts(Microsoft.Xna.Framework.Input.KeyboardState keyboard, double elapsedSeconds) => HandleActiveTextFieldKeyboardShortcuts(keyboard, elapsedSeconds);

    void IGameplayContext.HandleBrowserTextInput(char character) { HandleBrowserTextInput(character); }

    void IGameplayContext.HandleWindowFocusLost(Microsoft.Xna.Framework.Input.MouseState releasedMouse) { HandleWindowFocusLost(releasedMouse); }

    bool IGameplayContext.HasGameplayModalInputOwner() => HasGameplayModalInputOwner();

    void IGameplayContext.HideLoadingOverlay() { HideLoadingOverlay(); }

    void IGameplayContext.InitializeClientPlugins() { InitializeClientPlugins(); }

    void IGameplayContext.InitializeConsoleInputCursor() { InitializeConsoleInputCursor(); }

    void IGameplayContext.InitializePracticeBotNamePoolForMatch() { InitializePracticeBotNamePoolForMatch(); }

    void IGameplayContext.InitializeServerLauncherMode() { InitializeServerLauncherMode(); }

    void IGameplayContext.InvalidateDiscordRichPresenceRefresh() { InvalidateDiscordRichPresenceRefresh(); }

    bool IGameplayContext.IsBindingPressed(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse, OpenGarrison.Client.InputBinding binding) => IsBindingPressed(keyboard, mouse, binding);

    bool IGameplayContext.IsBrowserGameplayWarmupComplete() => IsBrowserGameplayWarmupComplete();

    bool IGameplayContext.IsChatShortcutPressed(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.Keys key) => IsChatShortcutPressed(keyboard, key);

    bool IGameplayContext.IsClientPerformanceDiagnosticsEnabled() => IsClientPerformanceDiagnosticsEnabled();

    bool IGameplayContext.IsControllerBindingPressed(OpenGarrison.Core.ControllerButtonBinding binding, Microsoft.Xna.Framework.Input.GamePadState current, Microsoft.Xna.Framework.Input.GamePadState previous) => Game1.IsControllerBindingPressed(binding, current, previous);

    bool IGameplayContext.IsControllerBindingPressed(OpenGarrison.Core.ControllerButtonBinding binding) => IsControllerBindingPressed(binding);

    bool IGameplayContext.IsControllerMenuBackPressed() => IsControllerMenuBackPressed();

    bool IGameplayContext.IsGameplayInputBlocked() => IsGameplayInputBlocked();

    bool IGameplayContext.IsGameplayLoadingForMenuInput() => IsGameplayLoadingForMenuInput();

    bool IGameplayContext.IsKeyPressed(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.Keys key) => IsKeyPressed(keyboard, key);

    bool IGameplayContext.IsLastToDieDeathFocusPresentationActive() => IsLastToDieDeathFocusPresentationActive();

    bool IGameplayContext.IsLastToDieFailureOverlayActive() => IsLastToDieFailureOverlayActive();

    bool IGameplayContext.IsLastToDieStageClearOverlayActive() => IsLastToDieStageClearOverlayActive();

    bool IGameplayContext.IsLocalSpectatorPresentationActive() => IsLocalSpectatorPresentationActive();

    bool IGameplayContext.IsNetworkWorldWarmupBlockingPresentation() => IsNetworkWorldWarmupBlockingPresentation();

    bool IGameplayContext.IsPracticeNavigationWarmupBlockingGameplay() => IsPracticeNavigationWarmupBlockingGameplay();

    bool IGameplayContext.IsSpyHiddenFromLocalViewer(int ownerId, OpenGarrison.Core.PlayerTeam ownerTeam, float spyX) => IsSpyHiddenFromLocalViewer(ownerId, ownerTeam, spyX);

    bool IGameplayContext.IsSpyHiddenFromLocalViewer(OpenGarrison.Core.PlayerEntity player) => IsSpyHiddenFromLocalViewer(player);

    void IGameplayContext.LeaveManagedRoom() { LeaveManagedRoom(); }

    void IGameplayContext.LeavePeerRoom() { LeavePeerRoom(); }

    void IGameplayContext.LoadFaucetMusic() { LoadFaucetMusic(); }

    void IGameplayContext.LoadGameplayLoadoutMenuTextures() { LoadGameplayLoadoutMenuTextures(); }

    void IGameplayContext.LoadIngameMusic() { LoadIngameMusic(); }

    Microsoft.Xna.Framework.Graphics.SpriteFont IGameplayContext.LoadInitialSpriteFont(string assetName) => LoadInitialSpriteFont(assetName);

    void IGameplayContext.LoadLastToDieIngameMusic() { LoadLastToDieIngameMusic(); }

    void IGameplayContext.LoadLastToDieMenuMusic() { LoadLastToDieMenuMusic(); }

    void IGameplayContext.LoadMenuBitmapFont() { LoadMenuBitmapFont(); }

    void IGameplayContext.LoadMenuMusic() { LoadMenuMusic(); }

    void IGameplayContext.LoadMenuPlaqueTextures() { LoadMenuPlaqueTextures(); }

    void IGameplayContext.LogClientPerformanceLine(string line) { LogClientPerformanceLine(line); }

    void IGameplayContext.NormalizePracticeSetupState() { NormalizePracticeSetupState(); }

    void IGameplayContext.NotifyClientPluginsStarted() { NotifyClientPluginsStarted(); }

    void IGameplayContext.ObserveLastToDieBotReactionState() { ObserveLastToDieBotReactionState(); }

    void IGameplayContext.ObserveLastToDieCombatFeedbackState() { ObserveLastToDieCombatFeedbackState(); }

    void IGameplayContext.ObservePendingWorldHealingEventsForHealingCharacterEffects() { ObservePendingWorldHealingEventsForHealingCharacterEffects(); }

    void IGameplayContext.OnWindowTextInput(System.Object sender, TextInputEventArgs e) { OnWindowTextInput(sender, e); }

    void IGameplayContext.OpenChat(bool teamOnly) { OpenChat(teamOnly); }

    void IGameplayContext.OpenGameplayClassSelection() { OpenGameplayClassSelection(); }

    void IGameplayContext.OpenInGameMenu() { OpenInGameMenu(); }

    void IGameplayContext.PersistClientSettings() { PersistClientSettings(); }

    void IGameplayContext.PersistInputBindings() { PersistInputBindings(); }

    void IGameplayContext.PrepareDeathCamCaptureIfNeeded(int viewportWidth, int viewportHeight) { PrepareDeathCamCaptureIfNeeded(viewportWidth, viewportHeight); }

    void IGameplayContext.PrepareGameplayHudOpacityComposite(Microsoft.Xna.Framework.Input.MouseState mouse, Microsoft.Xna.Framework.Vector2 cameraPosition) { PrepareGameplayHudOpacityComposite(mouse, cameraPosition); }

    void IGameplayContext.PrepareHostedServerLaunchUi(bool closeHostSetup, bool disconnectNetworkClient) { PrepareHostedServerLaunchUi(closeHostSetup, disconnectNetworkClient); }

    void IGameplayContext.PrepareLastToDieDeathFocusOverlayIfNeeded(int viewportWidth, int viewportHeight) { PrepareLastToDieDeathFocusOverlayIfNeeded(viewportWidth, viewportHeight); }

    void IGameplayContext.ProcessNetworkMessages() { ProcessNetworkMessages(); }

    void IGameplayContext.PumpEmbeddedSession(double elapsedSeconds) { PumpEmbeddedSession(elapsedSeconds); }

    void IGameplayContext.PumpPeerRoom(double elapsed) { PumpPeerRoom(elapsed); }

    void IGameplayContext.PumpRunUploads(double elapsedSeconds) { PumpRunUploads(elapsedSeconds); }

    void IGameplayContext.QueuePracticeNavigationWarmupForCurrentLevel() { QueuePracticeNavigationWarmupForCurrentLevel(); }

    void IGameplayContext.QueueWelcomeAfterNetworkMapSync(OpenGarrison.Protocol.WelcomeMessage welcome) { QueueWelcomeAfterNetworkMapSync(welcome); }

    void IGameplayContext.RecordPresentedExplosionVisual(string effectName, float x, float y) { RecordPresentedExplosionVisual(effectName, x, y); }

    void IGameplayContext.RecordRecentConnection(string host, int port) { RecordRecentConnection(host, port); }

    void IGameplayContext.ReinitializeSimulationForTickRate(int tickRate) { ReinitializeSimulationForTickRate(tickRate); }

    void IGameplayContext.RememberPredictedExplosionVisual(OpenGarrison.Core.WorldVisualEvent visualEvent) { RememberPredictedExplosionVisual(visualEvent); }

    void IGameplayContext.ResetBackstabVisuals() { ResetBackstabVisuals(); }

    void IGameplayContext.ResetBotDiagnosticSample() { ResetBotDiagnosticSample(); }

    void IGameplayContext.ResetBuffBannerReadySoundObservation() { ResetBuffBannerReadySoundObservation(); }

    void IGameplayContext.ResetCameraPanningState() { ResetCameraPanningState(); }

    void IGameplayContext.ResetChatInputState(bool requireOpenKeyRelease = false) { ResetChatInputState(requireOpenKeyRelease); }

    void IGameplayContext.ResetCivviePogoTrickPresentationObservation() { ResetCivviePogoTrickPresentationObservation(); }

    void IGameplayContext.ResetClientTimingState() { ResetClientTimingState(); }

    void IGameplayContext.ResetCorpseAcidDissolves() { ResetCorpseAcidDissolves(); }

    void IGameplayContext.ResetDynamicRagdollEffects() { ResetDynamicRagdollEffects(); }

    void IGameplayContext.ResetGameplayRuntimeState() { ResetGameplayRuntimeState(); }

    void IGameplayContext.ResetGameplayTransitionEffects() { ResetGameplayTransitionEffects(); }

    void IGameplayContext.ResetHealingCharacterEffects() { ResetHealingCharacterEffects(); }

    void IGameplayContext.ResetJumpState() { ResetJumpState(); }

    void IGameplayContext.ResetLastToDieBotReactionState() { ResetLastToDieBotReactionState(); }

    void IGameplayContext.ResetLastToDieCombatFeedbackPresentation() { ResetLastToDieCombatFeedbackPresentation(); }

    void IGameplayContext.ResetLastToDieState() { ResetLastToDieState(); }

    void IGameplayContext.ResetPracticeBotManagerState(bool releaseWorldSlots) { ResetPracticeBotManagerState(releaseWorldSlots); }

    void IGameplayContext.ResetPracticeRoundPoints() { ResetPracticeRoundPoints(); }

    void IGameplayContext.ResetProcessedNetworkEventHistory() { ResetProcessedNetworkEventHistory(); }

    void IGameplayContext.ResetSmoothCameraState() { ResetSmoothCameraState(); }

    void IGameplayContext.ResetSmoothCameraState(Microsoft.Xna.Framework.Vector2 position) { ResetSmoothCameraState(position); }

    void IGameplayContext.ResetSnapshotPresentationHistories(bool preserveRetainedProjectilePresentation = false) { ResetSnapshotPresentationHistories(preserveRetainedProjectilePresentation); }

    void IGameplayContext.ResetSnapshotStateHistory() { ResetSnapshotStateHistory(); }

    void IGameplayContext.ResetSpectatorTracking(bool enableTracking) { ResetSpectatorTracking(enableTracking); }

    void IGameplayContext.ResetTransientPresentationEffects() { ResetTransientPresentationEffects(); }

    void IGameplayContext.ResetVoiceChat() { ResetVoiceChat(); }

    void IGameplayContext.ResetVotePresentation() { ResetVotePresentation(); }

    void IGameplayContext.ReturnToMainMenu(string statusMessage = default) { ReturnToMainMenu(statusMessage); }

    void IGameplayContext.ReturnToMainMenuWithNetworkStatus(string statusMessage, string consoleMessage) { ReturnToMainMenuWithNetworkStatus(statusMessage, consoleMessage); }

    void IGameplayContext.ReturnToMainMenuWithNetworkStatus(string statusMessage) { ReturnToMainMenuWithNetworkStatus(statusMessage); }

    int IGameplayContext.ScaleBloodVisualCount(int maximumCount) => ScaleBloodVisualCount(maximumCount);

    bool IGameplayContext.SelectPracticeMapEntry(string levelName) => SelectPracticeMapEntry(levelName);

    void IGameplayContext.SetJoiningServerLoadingLabel(string serverLabel) { SetJoiningServerLoadingLabel(serverLabel); }

    void IGameplayContext.SetNetworkStatus(string statusMessage) { SetNetworkStatus(statusMessage); }

    void IGameplayContext.SetNetworkStatusAndConsole(string statusMessage, string consoleMessage) { SetNetworkStatusAndConsole(statusMessage, consoleMessage); }

    void IGameplayContext.SetPersistedMenuStatusMessage(string message) { SetPersistedMenuStatusMessage(message); }

    void IGameplayContext.SetScoreRouteRecorderCaptureInput(OpenGarrison.Core.PlayerInputSnapshot gameplayInput) { SetScoreRouteRecorderCaptureInput(gameplayInput); }

    void IGameplayContext.SetSocialPresenceNetworkEndpoint(OpenGarrison.Client.NetworkEndpoint endpoint) { SetSocialPresenceNetworkEndpoint(endpoint); }

    bool IGameplayContext.ShouldDrawSoftwareMenuCursor() => ShouldDrawSoftwareMenuCursor();

    bool IGameplayContext.ShouldPresentAuthoritativeExplosionVisual(OpenGarrison.Protocol.SnapshotVisualEvent visualEvent) => ShouldPresentAuthoritativeExplosionVisual(visualEvent);

    bool IGameplayContext.ShouldShowGameplayMouseCursor() => ShouldShowGameplayMouseCursor();

    bool IGameplayContext.ShouldSuppressPredictedAirBlastVisualEcho(OpenGarrison.Protocol.SnapshotVisualEvent visualEvent) => ShouldSuppressPredictedAirBlastVisualEcho(visualEvent);

    bool IGameplayContext.ShouldSuppressPredictedExplosionVisualEcho(OpenGarrison.Protocol.SnapshotVisualEvent visualEvent) => ShouldSuppressPredictedExplosionVisualEcho(visualEvent);

    bool IGameplayContext.ShouldUseSoftwareMenuCursor() => ShouldUseSoftwareMenuCursor();

    void IGameplayContext.ShowJoiningServerLoadingOverlay(string serverLabel = default) { ShowJoiningServerLoadingOverlay(serverLabel); }

    void IGameplayContext.ShowLoadingOverlay(string message, Nullable<double> progress = default) { ShowLoadingOverlay(message, progress); }

    void IGameplayContext.ShutdownClientPlugins() { ShutdownClientPlugins(); }

    void IGameplayContext.SpawnBackstabVisual(int ownerId, OpenGarrison.Core.PlayerTeam team, float x, float y, float directionDegrees) { SpawnBackstabVisual(ownerId, team, x, y, directionDegrees); }

    void IGameplayContext.SpawnCivvieMoneyVisual(OpenGarrison.Core.CivvieMoneyTrailSpawn spawn) { SpawnCivvieMoneyVisual(spawn); }

    void IGameplayContext.SpawnCivvieMoneyVisual(float x, float y, float initialHorizontalSpeed) { SpawnCivvieMoneyVisual(x, y, initialHorizontalSpeed); }

    void IGameplayContext.SpawnLastToDieDroneSwarmForCurrentStage() { SpawnLastToDieDroneSwarmForCurrentStage(); }

    void IGameplayContext.SpawnLooseSheetVisual(float x, float y, float initialHorizontalSpeed) { SpawnLooseSheetVisual(x, y, initialHorizontalSpeed); }

    void IGameplayContext.SpawnWallspinDustVisual(float x, float y, int emissionTicks = 1) { SpawnWallspinDustVisual(x, y, emissionTicks); }

    void IGameplayContext.StopEmbeddedSession() { StopEmbeddedSession(); }

    void IGameplayContext.StopFaucetMusic() { StopFaucetMusic(); }

    void IGameplayContext.StopHostedServer() { StopHostedServer(); }

    void IGameplayContext.StopIngameMusic() { StopIngameMusic(); }

    void IGameplayContext.StopLastToDieGameOverSound() { StopLastToDieGameOverSound(); }

    void IGameplayContext.StopLastToDieIngameMusic() { StopLastToDieIngameMusic(); }

    void IGameplayContext.StopLastToDieMenuMusic() { StopLastToDieMenuMusic(); }

    void IGameplayContext.StopLocalJukebox() { StopLocalJukebox(); }

    void IGameplayContext.StopLocalRapidFireWeaponAudio() { StopLocalRapidFireWeaponAudio(); }

    void IGameplayContext.StopMenuMusic() { StopMenuMusic(); }

    void IGameplayContext.SuppressMouseFireAfterGameplayInputUnblocks(bool wasGameplayInputBlocked, Microsoft.Xna.Framework.Input.MouseState mouse) { SuppressMouseFireAfterGameplayInputUnblocks(wasGameplayInputBlocked, mouse); }

    void IGameplayContext.SyncDynamicRagdollsWithDeadBodies() { SyncDynamicRagdollsWithDeadBodies(); }

    void IGameplayContext.SyncPracticeBotRoster(OpenGarrison.Core.PlayerTeam localTeam) { SyncPracticeBotRoster(localTeam); }

    void IGameplayContext.ToggleAudioMute() { ToggleAudioMute(); }

    void IGameplayContext.ToggleFullscreenHotkey() { ToggleFullscreenHotkey(); }

    void IGameplayContext.ToggleGameplayClassSelection() { ToggleGameplayClassSelection(); }

    void IGameplayContext.ToggleGameplayTeamSelection() { ToggleGameplayTeamSelection(); }

    OpenGarrison.Client.Game1.NetworkMapSyncStatus IGameplayContext.TryEnsureNetworkMapAvailable(string levelName, bool isCustomMap, string mapDownloadUrl, string mapContentHash, out string error) => TryEnsureNetworkMapAvailable(levelName, isCustomMap, mapDownloadUrl, mapContentHash, out error);

    bool IGameplayContext.TryGetLevelBackgroundTexture(out Microsoft.Xna.Framework.Graphics.Texture2D texture) => TryGetLevelBackgroundTexture(out texture);

    void IGameplayContext.TryHandleVoteShortcut(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { TryHandleVoteShortcut(keyboard, mouse); }

    bool IGameplayContext.TryStartHostedServerBackground(string serverName, int port, int maxPlayers, string password, string rconPassword, int timeLimitMinutes, int capLimit, int respawnSeconds, bool lobbyAnnounce, bool autoBalance, bool secondaryAbilitiesEnabled, string requestedMap, string mapRotationFile, bool resetConsole, out string error) => TryStartHostedServerBackground(serverName, port, maxPlayers, password, rconPassword, timeLimitMinutes, capLimit, respawnSeconds, lobbyAnnounce, autoBalance, secondaryAbilitiesEnabled, requestedMap, mapRotationFile, resetConsole, out error);

    void IGameplayContext.UnloadCrtPresentation() { UnloadCrtPresentation(); }

    void IGameplayContext.UpdateAccountOperation() { UpdateAccountOperation(); }

    void IGameplayContext.UpdateBotBrainCorridorRecorderHotkeys(Microsoft.Xna.Framework.Input.KeyboardState keyboard) { UpdateBotBrainCorridorRecorderHotkeys(keyboard); }

    void IGameplayContext.UpdateBubbleMenuState(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateBubbleMenuState(keyboard, mouse); }

    void IGameplayContext.UpdateChatScrollState(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateChatScrollState(keyboard, mouse); }

    void IGameplayContext.UpdateClientPowersMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateClientPowersMenu(keyboard, mouse); }

    void IGameplayContext.UpdateConsoleScrollState(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateConsoleScrollState(keyboard, mouse); }

    void IGameplayContext.UpdateControllerInputState(bool windowActive, Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateControllerInputState(windowActive, keyboard, mouse); }

    void IGameplayContext.UpdateControlsMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateControlsMenu(keyboard, mouse); }

    void IGameplayContext.UpdateCrtUnlockSequence(Microsoft.Xna.Framework.Input.KeyboardState keyboard, System.TimeSpan totalGameTime) { UpdateCrtUnlockSequence(keyboard, totalGameTime); }

    void IGameplayContext.UpdateCustomBubbleEditor(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateCustomBubbleEditor(keyboard, mouse); }

    void IGameplayContext.UpdateCustomBubbleHotkey(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateCustomBubbleHotkey(keyboard, mouse); }

    void IGameplayContext.UpdateDebugMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateDebugMenu(keyboard, mouse); }

    void IGameplayContext.UpdateEmbeddedConsoleCommand() { UpdateEmbeddedConsoleCommand(); }

    void IGameplayContext.UpdateFriendsMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateFriendsMenu(keyboard, mouse); }

    void IGameplayContext.UpdateGameplayLoadoutMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateGameplayLoadoutMenu(keyboard, mouse); }

    void IGameplayContext.UpdateGameplayMenuState(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateGameplayMenuState(keyboard, mouse); }

    void IGameplayContext.UpdateGameplayPresentation(Microsoft.Xna.Framework.GameTime gameTime, Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse, int clientTicks) { UpdateGameplayPresentation(gameTime, keyboard, mouse, clientTicks); }

    void IGameplayContext.UpdateGameplayScreenState(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateGameplayScreenState(keyboard, mouse); }

    void IGameplayContext.UpdateGameplayWindowState() { UpdateGameplayWindowState(); }

    void IGameplayContext.UpdateGarrisonBuilderEditor(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse, float deltaSeconds) { UpdateGarrisonBuilderEditor(keyboard, mouse, deltaSeconds); }

    void IGameplayContext.UpdateHitboxDebugHotkey(Microsoft.Xna.Framework.Input.KeyboardState keyboard) { UpdateHitboxDebugHotkey(keyboard); }

    void IGameplayContext.UpdateHudEditor(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateHudEditor(keyboard, mouse); }

    void IGameplayContext.UpdateInGameMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateInGameMenu(keyboard, mouse); }

    void IGameplayContext.UpdateLastToDieDeathFocusPresentation() { UpdateLastToDieDeathFocusPresentation(); }

    void IGameplayContext.UpdateLastToDieFailureOverlay(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateLastToDieFailureOverlay(keyboard, mouse); }

    void IGameplayContext.UpdateLastToDiePerkMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateLastToDiePerkMenu(keyboard, mouse); }

    void IGameplayContext.UpdateLastToDieStageClearOverlay(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateLastToDieStageClearOverlay(keyboard, mouse); }

    void IGameplayContext.UpdateLastToDieSurvivorMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateLastToDieSurvivorMenu(keyboard, mouse); }

    void IGameplayContext.UpdateMenuStatusMessageExpiry() { UpdateMenuStatusMessageExpiry(); }

    void IGameplayContext.UpdateOfflinePracticeMapVote() { UpdateOfflinePracticeMapVote(); }

    void IGameplayContext.UpdateOptionsMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateOptionsMenu(keyboard, mouse); }

    void IGameplayContext.UpdatePluginOptionsMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdatePluginOptionsMenu(keyboard, mouse); }

    bool IGameplayContext.UpdatePracticeNavigationWarmup() => UpdatePracticeNavigationWarmup();

    void IGameplayContext.UpdatePracticeSetupMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdatePracticeSetupMenu(keyboard, mouse); }

    bool IGameplayContext.UpdateQuitPrompt(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) => UpdateQuitPrompt(keyboard, mouse);

    void IGameplayContext.UpdateReplayPlaybackControls(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateReplayPlaybackControls(keyboard, mouse); }

    void IGameplayContext.UpdateRespawnCameraState(float deltaSeconds, Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateRespawnCameraState(deltaSeconds, keyboard, mouse); }

    void IGameplayContext.UpdateScoreboardState(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateScoreboardState(keyboard, mouse); }

    void IGameplayContext.UpdateSpectatorTrackingHotkeys(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateSpectatorTrackingHotkeys(keyboard, mouse); }

    void IGameplayContext.UpdateVoteMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateVoteMenu(keyboard, mouse); }

    void IGameplayContext.UploadSelectedCustomBubbleState() { UploadSelectedCustomBubbleState(); }

}
