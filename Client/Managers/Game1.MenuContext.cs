#nullable enable

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Client;

public partial class Game1 : IMenuContext
{
    bool IMenuContext._accountDialogOpen { get => _accountDialogOpen; set => _accountDialogOpen = value; }

    int IMenuContext._accountGlobalRank { get => _accountGlobalRank; set => _accountGlobalRank = value; }

    bool IMenuContext._accountGlobalRankKnown { get => _accountGlobalRankKnown; set => _accountGlobalRankKnown = value; }

    bool IMenuContext._accountIsProtected { get => _accountIsProtected; set => _accountIsProtected = value; }

    long IMenuContext._accountLifetimePoints { get => _accountLifetimePoints; set => _accountLifetimePoints = value; }

    long IMenuContext._accountWalletBalance { get => _accountWalletBalance; set => _accountWalletBalance = value; }

    bool IMenuContext._audioMuted { get => _audioMuted; set => _audioMuted = value; }

    int IMenuContext._bloodAmountLevel { get => _bloodAmountLevel; set => _bloodAmountLevel = value; }

    int IMenuContext._bloodPersistenceSeconds { get => _bloodPersistenceSeconds; set => _bloodPersistenceSeconds = value; }

    int IMenuContext._bloodRenderMode { get => _bloodRenderMode; set => _bloodRenderMode = value; }

    bool IMenuContext._brandIntroActive { get => _brandIntroActive; set => _brandIntroActive = value; }

    bool IMenuContext._builderEditorEnabled { get => _builderEditorEnabled; set => _builderEditorEnabled = value; }

    bool IMenuContext._cameraPanningEnabled { get => _cameraPanningEnabled; set => _cameraPanningEnabled = value; }

    OpenGarrison.ClientShared.ClientIdentityDocument IMenuContext._clientIdentity { get => _clientIdentity; }

    OpenGarrison.Client.ClientPluginHost IMenuContext._clientPluginHost { get => _clientPluginHost; set => _clientPluginHost = value; }

    bool IMenuContext._clientPowersOpen { get => _clientPowersOpen; set => _clientPowersOpen = value; }

    bool IMenuContext._clientPowersOpenedFromGameplay { get => _clientPowersOpenedFromGameplay; set => _clientPowersOpenedFromGameplay = value; }

    OpenGarrison.ClientShared.ClientSettings IMenuContext._clientSettings { get => _clientSettings; }

    int IMenuContext._combatMusicVolumePercent { get => _combatMusicVolumePercent; set => _combatMusicVolumePercent = value; }

    int IMenuContext._controlsHoverIndex { get => _controlsHoverIndex; set => _controlsHoverIndex = value; }

    bool IMenuContext._controlsMenuOpen { get => _controlsMenuOpen; set => _controlsMenuOpen = value; }

    bool IMenuContext._controlsMenuOpenedFromGameplay { get => _controlsMenuOpenedFromGameplay; set => _controlsMenuOpenedFromGameplay = value; }

    int IMenuContext._controlsPageIndex { get => _controlsPageIndex; set => _controlsPageIndex = value; }

    int IMenuContext._controlsScrollOffset { get => _controlsScrollOffset; set => _controlsScrollOffset = value; }

    int IMenuContext._corpseDurationMode { get => _corpseDurationMode; set => _corpseDurationMode = value; }

    int IMenuContext._corpseFadeMode { get => _corpseFadeMode; set => _corpseFadeMode = value; }

    bool IMenuContext._creditsOpen { get => _creditsOpen; set => _creditsOpen = value; }

    bool IMenuContext._creditsScrollInitialized { get => _creditsScrollInitialized; set => _creditsScrollInitialized = value; }

    int IMenuContext._cursorSizePercent { get => _cursorSizePercent; set => _cursorSizePercent = value; }

    bool IMenuContext._customBubbleEditorOpen { get => _customBubbleEditorOpen; set => _customBubbleEditorOpen = value; }

    bool IMenuContext._damageVignetteEnabled { get => _damageVignetteEnabled; set => _damageVignetteEnabled = value; }

    int IMenuContext._damageVignetteIntensityPercent { get => _damageVignetteIntensityPercent; set => _damageVignetteIntensityPercent = value; }

    bool IMenuContext._debugMenuAwaitingEscapeRelease { get => _debugMenuAwaitingEscapeRelease; set => _debugMenuAwaitingEscapeRelease = value; }

    bool IMenuContext._debugMenuEnabled { get => _debugMenuEnabled; set => _debugMenuEnabled = value; }

    int IMenuContext._debugMenuHoverIndex { get => _debugMenuHoverIndex; set => _debugMenuHoverIndex = value; }

    bool IMenuContext._debugMenuOpen { get => _debugMenuOpen; set => _debugMenuOpen = value; }

    bool IMenuContext._debugRocketCollisionsEnabled { get => _debugRocketCollisionsEnabled; set => _debugRocketCollisionsEnabled = value; }

    OpenGarrison.Core.DisplayModeKind IMenuContext._displayMode { get => _displayMode; set => _displayMode = value; }

    bool IMenuContext._dynamicMusicEnabled { get => _dynamicMusicEnabled; set => _dynamicMusicEnabled = value; }

    bool IMenuContext._dynamicRagdollEnabled { get => _dynamicRagdollEnabled; set => _dynamicRagdollEnabled = value; }

    bool IMenuContext._editingFriendCode { get => _editingFriendCode; set => _editingFriendCode = value; }

    bool IMenuContext._editingFriendNickname { get => _editingFriendNickname; set => _editingFriendNickname = value; }

    bool IMenuContext._editingPlayerName { get => _editingPlayerName; set => _editingPlayerName = value; }

    bool IMenuContext._enablePrediction { get => _enablePrediction; set => _enablePrediction = value; }

    int IMenuContext._flameRenderMode { get => _flameRenderMode; set => _flameRenderMode = value; }

    int IMenuContext._frameRateLimit { get => _frameRateLimit; set => _frameRateLimit = value; }

    OpenGarrison.ClientShared.FriendListDocument IMenuContext._friendList { get => _friendList; }

    string IMenuContext._friendNicknameInputBuffer { get => _friendNicknameInputBuffer; set => _friendNicknameInputBuffer = value; }

    bool IMenuContext._friendsMenuAddingFriend { get => _friendsMenuAddingFriend; set => _friendsMenuAddingFriend = value; }

    int IMenuContext._friendsMenuHoverIndex { get => _friendsMenuHoverIndex; set => _friendsMenuHoverIndex = value; }

    bool IMenuContext._friendsMenuOpen { get => _friendsMenuOpen; set => _friendsMenuOpen = value; }

    int IMenuContext._friendsMenuSelectedIndex { get => _friendsMenuSelectedIndex; set => _friendsMenuSelectedIndex = value; }

    OpenGarrison.Client.Game1.FriendsMenuTab IMenuContext._friendsMenuTab { get => _friendsMenuTab; set => _friendsMenuTab = value; }

    OpenGarrison.Client.Game1.GameplaySessionKind IMenuContext._gameplaySessionKind { get => _gameplaySessionKind; set => _gameplaySessionKind = value; }

    bool IMenuContext._garrisonBuilderQuickTestActive { get => _garrisonBuilderQuickTestActive; set => _garrisonBuilderQuickTestActive = value; }

    int IMenuContext._gibLevel { get => _gibLevel; set => _gibLevel = value; }

    Microsoft.Xna.Framework.GraphicsDeviceManager IMenuContext._graphics { get => _graphics; }

    Microsoft.Xna.Framework.Graphics.Effect IMenuContext._grayscaleEffect { get => _grayscaleEffect; set => _grayscaleEffect = value; }

    bool IMenuContext._healerRadarEnabled { get => _healerRadarEnabled; set => _healerRadarEnabled = value; }

    OpenGarrison.Client.Game1.HostSetupEditField IMenuContext._hostSetupEditField { get => _hostSetupEditField; set => _hostSetupEditField = value; }

    bool IMenuContext._hostSetupOpen { get => _hostSetupOpen; set => _hostSetupOpen = value; }

    OpenGarrison.Client.Game1.HostSetupFormState IMenuContext._hostSetupState { get => _hostSetupState; }

    bool IMenuContext._hudShowOnlyActiveWeapon { get => _hudShowOnlyActiveWeapon; set => _hudShowOnlyActiveWeapon = value; }

    bool IMenuContext._inGameMenuAwaitingEscapeRelease { get => _inGameMenuAwaitingEscapeRelease; set => _inGameMenuAwaitingEscapeRelease = value; }

    int IMenuContext._inGameMenuHoverIndex { get => _inGameMenuHoverIndex; set => _inGameMenuHoverIndex = value; }

    bool IMenuContext._inGameMenuOpen { get => _inGameMenuOpen; set => _inGameMenuOpen = value; }

    int IMenuContext._ingameMusicVolumePercent { get => _ingameMusicVolumePercent; set => _ingameMusicVolumePercent = value; }

    OpenGarrison.Core.IngameResolutionKind IMenuContext._ingameResolution { get => _ingameResolution; set => _ingameResolution = value; }

    OpenGarrison.Client.InputBindingsSettings IMenuContext._inputBindings { get => _inputBindings; }

    bool IMenuContext._jukeboxMenuOpen { get => _jukeboxMenuOpen; set => _jukeboxMenuOpen = value; }

    int IMenuContext._jumpMenuHoverIndex { get => _jumpMenuHoverIndex; set => _jumpMenuHoverIndex = value; }

    bool IMenuContext._jumpMenuOpen { get => _jumpMenuOpen; set => _jumpMenuOpen = value; }

    bool IMenuContext._killCamEnabled { get => _killCamEnabled; set => _killCamEnabled = value; }

    int IMenuContext._lastToDieMenuHoverIndex { get => _lastToDieMenuHoverIndex; set => _lastToDieMenuHoverIndex = value; }

    bool IMenuContext._lastToDieMenuOpen { get => _lastToDieMenuOpen; set => _lastToDieMenuOpen = value; }

    OpenGarrison.Client.Game1.LastToDieMenuPage IMenuContext._lastToDieMenuPage { get => _lastToDieMenuPage; set => _lastToDieMenuPage = value; }

    bool IMenuContext._lastToDieRoomCodeJoinOpen { get => _lastToDieRoomCodeJoinOpen; set => _lastToDieRoomCodeJoinOpen = value; }

    bool IMenuContext._lobbyBrowserOpen { get => _lobbyBrowserOpen; set => _lobbyBrowserOpen = value; }

    OpenGarrison.Core.LowHealthColorMode IMenuContext._lowHealthColorMode { get => _lowHealthColorMode; set => _lowHealthColorMode = value; }

    bool IMenuContext._mainMenuBottomBarHover { get => _mainMenuBottomBarHover; set => _mainMenuBottomBarHover = value; }

    bool IMenuContext._mainMenuChromeHidden { get => _mainMenuChromeHidden; set => _mainMenuChromeHidden = value; }

    int IMenuContext._mainMenuHoverIndex { get => _mainMenuHoverIndex; set => _mainMenuHoverIndex = value; }

    bool IMenuContext._mainMenuOpen { get => _mainMenuOpen; set => _mainMenuOpen = value; }

    OpenGarrison.Client.Game1.MainMenuPage IMenuContext._mainMenuPage { get => _mainMenuPage; set => _mainMenuPage = value; }

    int IMenuContext._manualConnectControllerIndex { get => _manualConnectControllerIndex; set => _manualConnectControllerIndex = value; }

    bool IMenuContext._manualConnectOpen { get => _manualConnectOpen; set => _manualConnectOpen = value; }

    int IMenuContext._masterVolumePercent { get => _masterVolumePercent; set => _masterVolumePercent = value; }

    string IMenuContext._menuBackgroundAttributionText { get => _menuBackgroundAttributionText; set => _menuBackgroundAttributionText = value; }

    string IMenuContext._menuBackgroundFailedPath { get => _menuBackgroundFailedPath; set => _menuBackgroundFailedPath = value; }

    OpenGarrison.Core.MenuBackgroundMode IMenuContext._menuBackgroundMode { get => _menuBackgroundMode; set => _menuBackgroundMode = value; }

    OpenGarrison.Client.LoadedSpriteFrame IMenuContext._menuBackgroundTexture { get => _menuBackgroundTexture; set => _menuBackgroundTexture = value; }

    string IMenuContext._menuBackgroundTexturePath { get => _menuBackgroundTexturePath; set => _menuBackgroundTexturePath = value; }

    int IMenuContext._menuImageFrame { get => _menuImageFrame; set => _menuImageFrame = value; }

    int IMenuContext._menuMusicVolumePercent { get => _menuMusicVolumePercent; set => _menuMusicVolumePercent = value; }

    OpenGarrison.Client.LoadedSpriteFrame IMenuContext._menuPlaqueTallTexture { get => _menuPlaqueTallTexture; set => _menuPlaqueTallTexture = value; }

    OpenGarrison.Client.LoadedSpriteFrame IMenuContext._menuPlaqueTexture { get => _menuPlaqueTexture; set => _menuPlaqueTexture = value; }

    string IMenuContext._menuStatusMessage { get => _menuStatusMessage; set => _menuStatusMessage = value; }

    OpenGarrison.Core.MusicMode IMenuContext._musicMode { get => _musicMode; set => _musicMode = value; }

    bool IMenuContext._namePromptOpen { get => _namePromptOpen; set => _namePromptOpen = value; }

    OpenGarrison.Client.NetworkGameClient IMenuContext._networkClient { get => _networkClient; }

    int IMenuContext._optionsHoverIndex { get => _optionsHoverIndex; set => _optionsHoverIndex = value; }

    bool IMenuContext._optionsMenuOpen { get => _optionsMenuOpen; set => _optionsMenuOpen = value; }

    bool IMenuContext._optionsMenuOpenedFromGameplay { get => _optionsMenuOpenedFromGameplay; set => _optionsMenuOpenedFromGameplay = value; }

    int IMenuContext._optionsPageIndex { get => _optionsPageIndex; set => _optionsPageIndex = value; }

    int IMenuContext._optionsScrollOffset { get => _optionsScrollOffset; set => _optionsScrollOffset = value; }

    bool IMenuContext._overheadChatEnabled { get => _overheadChatEnabled; set => _overheadChatEnabled = value; }

    int IMenuContext._particleMode { get => _particleMode; set => _particleMode = value; }

    OpenGarrison.Client.PlayerHostedRoomSession IMenuContext._peerRoomSession { get => _peerRoomSession; set => _peerRoomSession = value; }

    Nullable<OpenGarrison.Client.Game1.ControllerControlsMenuBinding> IMenuContext._pendingControllerControlsBinding { get => _pendingControllerControlsBinding; set => _pendingControllerControlsBinding = value; }

    Nullable<OpenGarrison.Client.Game1.ControlsMenuBinding> IMenuContext._pendingControlsBinding { get => _pendingControlsBinding; set => _pendingControlsBinding = value; }

    OpenGarrison.Client.Plugins.ClientPluginKeyOptionItem IMenuContext._pendingPluginOptionsKeyItem { get => _pendingPluginOptionsKeyItem; set => _pendingPluginOptionsKeyItem = value; }

    Microsoft.Xna.Framework.Graphics.Texture2D IMenuContext._pixel { get => _pixel; set => _pixel = value; }

    bool IMenuContext._pixelPerfectWeaponRotation { get => _pixelPerfectWeaponRotation; set => _pixelPerfectWeaponRotation = value; }

    int IMenuContext._playerCardSizeMode { get => _playerCardSizeMode; set => _playerCardSizeMode = value; }

    string IMenuContext._playerNameEditBuffer { get => _playerNameEditBuffer; set => _playerNameEditBuffer = value; }

    int IMenuContext._playerNameEditCursorIndex { get => _playerNameEditCursorIndex; set => _playerNameEditCursorIndex = value; }

    int IMenuContext._playerNameEditSelectionStart { get => _playerNameEditSelectionStart; set => _playerNameEditSelectionStart = value; }

    int IMenuContext._pluginOptionsHoverIndex { get => _pluginOptionsHoverIndex; set => _pluginOptionsHoverIndex = value; }

    bool IMenuContext._pluginOptionsMenuOpen { get => _pluginOptionsMenuOpen; set => _pluginOptionsMenuOpen = value; }

    bool IMenuContext._pluginOptionsMenuOpenedFromGameplay { get => _pluginOptionsMenuOpenedFromGameplay; set => _pluginOptionsMenuOpenedFromGameplay = value; }

    int IMenuContext._pluginOptionsScrollOffset { get => _pluginOptionsScrollOffset; set => _pluginOptionsScrollOffset = value; }

    bool IMenuContext._portraitRumbleEnabled { get => _portraitRumbleEnabled; set => _portraitRumbleEnabled = value; }

    bool IMenuContext._positionSmoothingEnabled { get => _positionSmoothingEnabled; set => _positionSmoothingEnabled = value; }

    bool IMenuContext._postGameMvpArtEnabled { get => _postGameMvpArtEnabled; set => _postGameMvpArtEnabled = value; }

    bool IMenuContext._practiceSetupOpen { get => _practiceSetupOpen; set => _practiceSetupOpen = value; }

    Microsoft.Xna.Framework.Input.KeyboardState IMenuContext._previousKeyboard { get => _previousKeyboard; set => _previousKeyboard = value; }

    Microsoft.Xna.Framework.Input.MouseState IMenuContext._previousMouse { get => _previousMouse; set => _previousMouse = value; }

    bool IMenuContext._projectileTeamTintEnabled { get => _projectileTeamTintEnabled; set => _projectileTeamTintEnabled = value; }

    bool IMenuContext._quitPromptOpen { get => _quitPromptOpen; set => _quitPromptOpen = value; }

    OpenGarrison.Client.GameMakerRuntimeAssetCache IMenuContext._runtimeAssets { get => _runtimeAssets; set => _runtimeAssets = value; }

    string IMenuContext._selectedPluginOptionsPluginId { get => _selectedPluginOptionsPluginId; set => _selectedPluginOptionsPluginId = value; }

    bool IMenuContext._showHealerEnabled { get => _showHealerEnabled; set => _showHealerEnabled = value; }

    bool IMenuContext._showHealingEnabled { get => _showHealingEnabled; set => _showHealingEnabled = value; }

    bool IMenuContext._showHealthBarEnabled { get => _showHealthBarEnabled; set => _showHealthBarEnabled = value; }

    bool IMenuContext._showPersistentSelfNameEnabled { get => _showPersistentSelfNameEnabled; set => _showPersistentSelfNameEnabled = value; }

    bool IMenuContext._showPlayerNamesEnabled { get => _showPlayerNamesEnabled; set => _showPlayerNamesEnabled = value; }

    bool IMenuContext._showShieldBarEnabled { get => _showShieldBarEnabled; set => _showShieldBarEnabled = value; }

    int IMenuContext._soundEffectsVolumePercent { get => _soundEffectsVolumePercent; set => _soundEffectsVolumePercent = value; }

    Microsoft.Xna.Framework.Graphics.SpriteBatch IMenuContext._spriteBatch { get => _spriteBatch; set => _spriteBatch = value; }

    bool IMenuContext._spriteDropShadowEnabled { get => _spriteDropShadowEnabled; set => _spriteDropShadowEnabled = value; }

    bool IMenuContext._stuckArrowsEnabled { get => _stuckArrowsEnabled; set => _stuckArrowsEnabled = value; }

    bool IMenuContext._uberOutlineEnabled { get => _uberOutlineEnabled; set => _uberOutlineEnabled = value; }

    bool IMenuContext._useLocalWeaponRotation { get => _useLocalWeaponRotation; set => _useLocalWeaponRotation = value; }

    OpenGarrison.Client.VoiceChatSettings IMenuContext._voiceSettings { get => _voiceSettings; set => _voiceSettings = value; }

    OpenGarrison.Core.WindowSizeKind IMenuContext._windowSize { get => _windowSize; set => _windowSize = value; }

    OpenGarrison.Core.SimulationWorld IMenuContext._world { get => _world; set => _world = value; }

    bool IMenuContext.CanShortenAccountFriendCode { get => CanShortenAccountFriendCode; }

    Microsoft.Xna.Framework.Graphics.GraphicsDevice IMenuContext.GraphicsDevice { get => GraphicsDevice; }

    bool IMenuContext.IsAccountOperationPending { get => IsAccountOperationPending; }

    bool IMenuContext.IsCrtSettingsUnlockedForSession { get => IsCrtSettingsUnlockedForSession; }

    bool IMenuContext.IsEmbeddedSessionOwner { get => IsEmbeddedSessionOwner; }

    bool IMenuContext.IsLastToDieSessionActive { get => IsLastToDieSessionActive; }

    bool IMenuContext.IsPeerRoomOwner { get => IsPeerRoomOwner; }

    bool IMenuContext.IsPracticeSessionActive { get => IsPracticeSessionActive; }

    OpenGarrison.Client.ScrollbarDragController IMenuContext.ScrollbarDrag { get => ScrollbarDrag; }

    int IMenuContext.ViewportHeight { get => ViewportHeight; }

    int IMenuContext.ViewportWidth { get => ViewportWidth; }

    MenuManager IMenuContext.Menus => _menuManager;

    SessionManager IMenuContext.Session => _sessionManager;

    HostingManager IMenuContext.Hosting => _hostingManager;

    void IMenuContext.AddConsoleLine(string line) { AddConsoleLine(line); }

    void IMenuContext.AddPluginMenuActions(List<OpenGarrison.Client.Game1.MenuPageAction> actions, OpenGarrison.Client.Plugins.ClientPluginMenuLocation location, int insertIndex = -1) { AddPluginMenuActions(actions, location, insertIndex); }

    void IMenuContext.AdjustBloodPersistenceSeconds(int step) { AdjustBloodPersistenceSeconds(step); }

    void IMenuContext.AdjustCombatMusicVolume(int deltaPercent) { AdjustCombatMusicVolume(deltaPercent); }

    void IMenuContext.AdjustControllerAimAssistStrengthSetting(float delta) { AdjustControllerAimAssistStrengthSetting(delta); }

    void IMenuContext.AdjustControllerAimDeadzoneSetting(float delta) { AdjustControllerAimDeadzoneSetting(delta); }

    void IMenuContext.AdjustControllerAimDistanceTier1Setting(float delta) { AdjustControllerAimDistanceTier1Setting(delta); }

    void IMenuContext.AdjustControllerAimDistanceTier2Setting(float delta) { AdjustControllerAimDistanceTier2Setting(delta); }

    void IMenuContext.AdjustControllerAimDistanceTier3Setting(float delta) { AdjustControllerAimDistanceTier3Setting(delta); }

    void IMenuContext.AdjustControllerScopedPrecisionSpeedSetting(float delta) { AdjustControllerScopedPrecisionSpeedSetting(delta); }

    void IMenuContext.AdjustCrtBrightnessSetting(int delta) { AdjustCrtBrightnessSetting(delta); }

    void IMenuContext.AdjustCursorSizeSetting(int step) { AdjustCursorSizeSetting(step); }

    void IMenuContext.AdjustIngameMusicVolume(int deltaPercent) { AdjustIngameMusicVolume(deltaPercent); }

    void IMenuContext.AdjustMasterVolume(int deltaPercent) { AdjustMasterVolume(deltaPercent); }

    void IMenuContext.AdjustMenuMusicVolume(int deltaPercent) { AdjustMenuMusicVolume(deltaPercent); }

    void IMenuContext.AdjustSoundEffectsVolume(int deltaPercent) { AdjustSoundEffectsVolume(deltaPercent); }

    void IMenuContext.AdjustVoiceSetting(string setting, int amount) { AdjustVoiceSetting(setting, amount); }

    void IMenuContext.AdvanceBrandLogoFlame(float elapsedSeconds) { AdvanceBrandLogoFlame(elapsedSeconds); }

    void IMenuContext.ApplyControllerControlsBinding(OpenGarrison.Client.Game1.ControllerControlsMenuBinding binding, OpenGarrison.Core.ControllerButtonBinding input) { ApplyControllerControlsBinding(binding, input); }

    void IMenuContext.ApplyControlsBinding(OpenGarrison.Client.Game1.ControlsMenuBinding binding, OpenGarrison.Client.InputBinding input) { ApplyControlsBinding(binding, input); }

    void IMenuContext.BeginAccountProfileRefresh(bool silent) { BeginAccountProfileRefresh(silent); }

    void IMenuContext.BeginEditingPlayerName() { BeginEditingPlayerName(); }

    void IMenuContext.BeginProtectAccount() { BeginProtectAccount(); }

    void IMenuContext.BeginShortenAccountFriendCode() { BeginShortenAccountFriendCode(); }

    List<OpenGarrison.Client.Game1.MenuPageButton> IMenuContext.BuildMainMenuButtons() => BuildMainMenuButtons();

    void IMenuContext.CancelFriendCodeJoin() { CancelFriendCodeJoin(); }

    void IMenuContext.CancelManagedRoomRequest() { CancelManagedRoomRequest(); }

    void IMenuContext.CancelPendingHostedLastToDieRelayLaunch() { CancelPendingHostedLastToDieRelayLaunch(); }

    bool IMenuContext.CanLoadSpriteFrameFromPath(string path) => CanLoadSpriteFrameFromPath(path);

    bool IMenuContext.CanOfferGameplaySelectionMenusFromInGameMenu() => CanOfferGameplaySelectionMenusFromInGameMenu();

    void IMenuContext.CloseAccountDialog() { CloseAccountDialog(); }

    void IMenuContext.CloseAllHostSetupMapPreviews() { CloseAllHostSetupMapPreviews(); }

    void IMenuContext.CloseFriendsContextMenu() { CloseFriendsContextMenu(); }

    void IMenuContext.CloseHostSetupMenu(bool clearStatus = false) { CloseHostSetupMenu(clearStatus); }

    void IMenuContext.CloseLobbyBrowser(bool clearStatus) { CloseLobbyBrowser(clearStatus); }

    void IMenuContext.CloseOptionsMenu() { CloseOptionsMenu(); }

    void IMenuContext.ClosePlayerCardOverlay() { ClosePlayerCardOverlay(); }

    void IMenuContext.ClosePluginOptionsMenu() { ClosePluginOptionsMenu(); }

    void IMenuContext.ConsumeControllerMenuConfirmPress() { ConsumeControllerMenuConfirmPress(); }

    void IMenuContext.CycleBloodAmountSetting() { CycleBloodAmountSetting(); }

    void IMenuContext.CycleBloodRenderModeSetting() { CycleBloodRenderModeSetting(); }

    void IMenuContext.CycleBuildMenuStyleSetting() { CycleBuildMenuStyleSetting(); }

    void IMenuContext.CycleControllerAimAssistStrengthSetting() { CycleControllerAimAssistStrengthSetting(); }

    void IMenuContext.CycleControllerAimDeadzoneSetting() { CycleControllerAimDeadzoneSetting(); }

    void IMenuContext.CycleControllerAimDistanceTier1Setting() { CycleControllerAimDistanceTier1Setting(); }

    void IMenuContext.CycleControllerAimDistanceTier2Setting() { CycleControllerAimDistanceTier2Setting(); }

    void IMenuContext.CycleControllerAimDistanceTier3Setting() { CycleControllerAimDistanceTier3Setting(); }

    void IMenuContext.CycleControllerInputModeSetting() { CycleControllerInputModeSetting(); }

    void IMenuContext.CycleControllerReticleModeSetting() { CycleControllerReticleModeSetting(); }

    void IMenuContext.CycleControllerScopedPrecisionSpeedSetting() { CycleControllerScopedPrecisionSpeedSetting(); }

    void IMenuContext.CycleCorpseDurationSetting() { CycleCorpseDurationSetting(); }

    void IMenuContext.CycleCorpseFadeModeSetting() { CycleCorpseFadeModeSetting(); }

    void IMenuContext.CycleCrtBrightnessSetting() { CycleCrtBrightnessSetting(); }

    void IMenuContext.CycleCrtPresetSetting() { CycleCrtPresetSetting(); }

    void IMenuContext.CycleCrtQualitySetting() { CycleCrtQualitySetting(); }

    void IMenuContext.CycleCrtSignalModeSetting() { CycleCrtSignalModeSetting(); }

    void IMenuContext.CycleCursorSizeSetting() { CycleCursorSizeSetting(); }

    void IMenuContext.CycleDamageVignetteIntensitySetting() { CycleDamageVignetteIntensitySetting(); }

    void IMenuContext.CycleDisplayModeSetting() { CycleDisplayModeSetting(); }

    void IMenuContext.CycleFlameRenderModeSetting() { CycleFlameRenderModeSetting(); }

    void IMenuContext.CycleFrameRateLimitSetting() { CycleFrameRateLimitSetting(); }

    void IMenuContext.CycleGibLevelSetting() { CycleGibLevelSetting(); }

    void IMenuContext.CycleIngameResolutionSetting() { CycleIngameResolutionSetting(); }

    void IMenuContext.CycleLowHealthColorModeSetting() { CycleLowHealthColorModeSetting(); }

    void IMenuContext.CycleMenuBackgroundModeSetting() { CycleMenuBackgroundModeSetting(); }

    void IMenuContext.CycleMusicModeSetting() { CycleMusicModeSetting(); }

    void IMenuContext.CycleParticleModeSetting() { CycleParticleModeSetting(); }

    void IMenuContext.CyclePlayerCardSizeSetting() { CyclePlayerCardSizeSetting(); }

    void IMenuContext.CycleSwapWeaponsBindingSetting() { CycleSwapWeaponsBindingSetting(); }

    void IMenuContext.CycleVoiceMicrophone() { CycleVoiceMicrophone(); }

    void IMenuContext.CycleVoiceMode() { CycleVoiceMode(); }

    void IMenuContext.CycleWindowSizeSetting() { CycleWindowSizeSetting(); }

    void IMenuContext.DismissCustomBubbleEditor() { DismissCustomBubbleEditor(); }

    void IMenuContext.DrawAccountDialog() { DrawAccountDialog(); }

    void IMenuContext.DrawBitmapFontText(string text, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color color, float scale, float rotation) { DrawBitmapFontText(text, position, color, scale, rotation); }

    void IMenuContext.DrawBitmapFontText(string text, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color color, float scale = 1f) { DrawBitmapFontText(text, position, color, scale); }

    void IMenuContext.DrawBitmapFontTextRightAligned(string text, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color color, float scale) { DrawBitmapFontTextRightAligned(text, position, color, scale); }

    void IMenuContext.DrawBottomCenterPlaqueButton(OpenGarrison.Client.Game1.PlaqueMenuLayout layout, string label, bool hovered, float textScaleMultiplier) { DrawBottomCenterPlaqueButton(layout, label, hovered, textScaleMultiplier); }

    void IMenuContext.DrawBottomRightPlaqueButton(OpenGarrison.Client.Game1.PlaqueMenuLayout layout, string label, bool hovered, float textScaleMultiplier) { DrawBottomRightPlaqueButton(layout, label, hovered, textScaleMultiplier); }

    void IMenuContext.DrawClientPowersMenu() { DrawClientPowersMenu(); }

    void IMenuContext.DrawControlsMenu() { DrawControlsMenu(); }

    void IMenuContext.DrawCreditsMenu() { DrawCreditsMenu(); }

    void IMenuContext.DrawCurrentMainMenuPage(IReadOnlyList<OpenGarrison.Client.Game1.MenuPageButton> buttons) { DrawCurrentMainMenuPage(buttons); }

    void IMenuContext.DrawCustomBubbleEditor() { DrawCustomBubbleEditor(); }

    void IMenuContext.DrawDevMessagePopup() { DrawDevMessagePopup(); }

    bool IMenuContext.DrawFlamingBrandLogo(Microsoft.Xna.Framework.Rectangle destination, float flameBlend = 1f, float opacity = 1f, float flashAmount = 0f) => DrawFlamingBrandLogo(destination, flameBlend, opacity, flashAmount);

    void IMenuContext.DrawFriendsMenu() { DrawFriendsMenu(); }

    void IMenuContext.DrawGarrisonBuilderEditorOverlay(Microsoft.Xna.Framework.Input.MouseState mouse) { DrawGarrisonBuilderEditorOverlay(mouse); }

    void IMenuContext.DrawHostSetupMenu() { DrawHostSetupMenu(); }

    void IMenuContext.DrawJumpMenu() { DrawJumpMenu(); }

    void IMenuContext.DrawLastToDieMenu() { DrawLastToDieMenu(); }

    void IMenuContext.DrawLoadedSpriteFrame(OpenGarrison.Client.LoadedSpriteFrame frame, Microsoft.Xna.Framework.Rectangle destinationRectangle, Microsoft.Xna.Framework.Color tint) { DrawLoadedSpriteFrame(frame, destinationRectangle, tint); }

    void IMenuContext.DrawLoadedSpriteFrame(OpenGarrison.Client.LoadedSpriteFrame frame, Microsoft.Xna.Framework.Vector2 position, Nullable<Microsoft.Xna.Framework.Rectangle> sourceRectangle, Microsoft.Xna.Framework.Color tint, float rotation, Microsoft.Xna.Framework.Vector2 origin, Microsoft.Xna.Framework.Vector2 scale, Microsoft.Xna.Framework.Graphics.SpriteEffects effects, float layerDepth) { DrawLoadedSpriteFrame(frame, position, sourceRectangle, tint, rotation, origin, scale, effects, layerDepth); }

    void IMenuContext.DrawLobbyBrowserMenu() { DrawLobbyBrowserMenu(); }

    void IMenuContext.DrawMainMenuBottomBar() { DrawMainMenuBottomBar(); }

    void IMenuContext.DrawManualConnectMenu() { DrawManualConnectMenu(); }

    void IMenuContext.DrawMenuButtonScaled(Microsoft.Xna.Framework.Rectangle bounds, string label, bool highlighted, float textScale, bool enabled = true) { DrawMenuButtonScaled(bounds, label, highlighted, textScale, enabled); }

    void IMenuContext.DrawMenuInputBoxScaled(Microsoft.Xna.Framework.Rectangle bounds, string text, bool active, float textScale, int cursorIndex = -1, int selectionStart = -1, bool enabled = true) { DrawMenuInputBoxScaled(bounds, text, active, textScale, cursorIndex, selectionStart, enabled); }

    void IMenuContext.DrawMenuPanelBackdrop(Microsoft.Xna.Framework.Rectangle rectangle, float alpha) { DrawMenuPanelBackdrop(rectangle, alpha); }

    void IMenuContext.DrawMenuStatusText() { DrawMenuStatusText(); }

    void IMenuContext.DrawOptionsMenu() { DrawOptionsMenu(); }

    void IMenuContext.DrawPlaqueMenuButton(OpenGarrison.Client.LoadedSpriteFrame texture, Microsoft.Xna.Framework.Rectangle bounds, string label, bool hovered, float plaqueScale, float textScaleMultiplier) { DrawPlaqueMenuButton(texture, bounds, label, hovered, plaqueScale, textScaleMultiplier); }

    void IMenuContext.DrawPlaqueMenuLayout(OpenGarrison.Client.Game1.PlaqueMenuLayout layout, IReadOnlyList<OpenGarrison.Client.Game1.MenuPageAction> stackedActions, Nullable<OpenGarrison.Client.Game1.MenuPageAction> soloAction, bool drawBottomBarButton, string bottomBarLabel, int hoveredStackedIndex, bool soloHovered, bool bottomBarHovered, float textScaleMultiplier = 1f) { DrawPlaqueMenuLayout(layout, stackedActions, soloAction, drawBottomBarButton, bottomBarLabel, hoveredStackedIndex, soloHovered, bottomBarHovered, textScaleMultiplier); }

    void IMenuContext.DrawPlayerNamePrompt() { DrawPlayerNamePrompt(); }

    void IMenuContext.DrawPluginOptionsMenu() { DrawPluginOptionsMenu(); }

    void IMenuContext.DrawPracticeSetupMenu() { DrawPracticeSetupMenu(); }

    void IMenuContext.DrawQuitPrompt() { DrawQuitPrompt(); }

    void IMenuContext.DrawRoundedRectangleOutline(Microsoft.Xna.Framework.Rectangle bounds, Microsoft.Xna.Framework.Color fillColor, Microsoft.Xna.Framework.Color outlineColor, int outlineThickness, int radius) { DrawRoundedRectangleOutline(bounds, fillColor, outlineColor, outlineThickness, radius); }

    void IMenuContext.DrawSpriteFrame(OpenGarrison.Client.LoadedSpriteFrame frame, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color tint, float rotation, Microsoft.Xna.Framework.Vector2 origin, Microsoft.Xna.Framework.Vector2 scale, Microsoft.Xna.Framework.Graphics.SpriteEffects effects = Microsoft.Xna.Framework.Graphics.SpriteEffects.None) { DrawSpriteFrame(frame, position, tint, rotation, origin, scale, effects); }

    void IMenuContext.EnsureMenuMusicPlaying() { EnsureMenuMusicPlaying(); }

    void IMenuContext.EnsurePlayerNamePrompt() { EnsurePlayerNamePrompt(); }

    void IMenuContext.EnsureSelectedHostMapVisible() { EnsureSelectedHostMapVisible(); }

    string IMenuContext.GetAccountRecoveryKeyDisplay() => GetAccountRecoveryKeyDisplay();

    string IMenuContext.GetAccountStatusDisplay() => GetAccountStatusDisplay();

    Microsoft.Xna.Framework.Rectangle IMenuContext.GetBottomCenterPlaqueButtonBounds(OpenGarrison.Client.Game1.PlaqueMenuLayout layout) => GetBottomCenterPlaqueButtonBounds(layout);

    Microsoft.Xna.Framework.Rectangle IMenuContext.GetBottomRightPlaqueButtonBounds(OpenGarrison.Client.Game1.PlaqueMenuLayout layout) => GetBottomRightPlaqueButtonBounds(layout);

    OpenGarrison.Client.Game1.PlaqueMenuLayout IMenuContext.GetCenteredPlaqueMenuLayout(bool tall, int stackedButtonCount, bool includeSoloButton, bool includeBottomBarButton) => GetCenteredPlaqueMenuLayout(tall, stackedButtonCount, includeSoloButton, includeBottomBarButton);

    OpenGarrison.Client.Plugins.ClientPluginMainMenuBackgroundOverride IMenuContext.GetClientPluginMainMenuBackgroundOverride() => GetClientPluginMainMenuBackgroundOverride();

    List<ValueTuple<OpenGarrison.Client.Game1.ControllerControlsMenuBinding, string, OpenGarrison.Core.ControllerButtonBinding>> IMenuContext.GetControllerControlsMenuBindings() => GetControllerControlsMenuBindings();

    string IMenuContext.GetControlsBindingLabel(OpenGarrison.Client.Game1.ControlsMenuBinding binding) => GetControlsBindingLabel(binding);

    List<ValueTuple<OpenGarrison.Client.Game1.ControlsMenuBinding, string, OpenGarrison.Client.InputBinding>> IMenuContext.GetControlsMenuBindings() => GetControlsMenuBindings();

    string IMenuContext.GetCrtPresetLabel() => GetCrtPresetLabel();

    string IMenuContext.GetCrtQualityStatusLabel() => GetCrtQualityStatusLabel();

    string IMenuContext.GetCrtSignalModeLabel() => GetCrtSignalModeLabel();

    Microsoft.Xna.Framework.Input.MouseState IMenuContext.GetFrameMouseState() => GetFrameMouseState();

    string IMenuContext.GetFriendNicknameInputDefault() => GetFriendNicknameInputDefault();

    string IMenuContext.GetGameplayExitStatusMessage() => GetGameplayExitStatusMessage();

    OpenGarrison.Client.LoadedSpriteFrame IMenuContext.GetMenuStackedButtonTexture(int index, int count) => GetMenuStackedButtonTexture(index, count);

    OpenGarrison.Client.LoadedGameMakerSprite IMenuContext.GetResolvedSprite(string spriteName) => GetResolvedSprite(spriteName);

    List<OpenGarrison.Client.Game1.MenuPageAction> IMenuContext.GetSessionJukeboxActions() => GetSessionJukeboxActions();

    string IMenuContext.GetSwapWeaponsBindingLabel() => GetSwapWeaponsBindingLabel();

    string IMenuContext.GetVoiceChannelActionLabel() => GetVoiceChannelActionLabel();

    string IMenuContext.GetVoiceChannelLabel() => GetVoiceChannelLabel();

    string IMenuContext.GetVoiceModeLabel() => GetVoiceModeLabel();

    string IMenuContext.GetVoiceMuteActionLabel() => GetVoiceMuteActionLabel();

    string IMenuContext.GetVoiceStatusLabel() => GetVoiceStatusLabel();

    bool IMenuContext.HasClientPluginOptions() => HasClientPluginOptions();

    void IMenuContext.InitializeFriendCodeCursor() { InitializeFriendCodeCursor(); }

    void IMenuContext.InitializeFriendNicknameCursor() { InitializeFriendNicknameCursor(); }

    bool IMenuContext.IsControllerMenuBackPressed() => IsControllerMenuBackPressed();

    bool IMenuContext.IsControllerMenuConfirmPressed() => IsControllerMenuConfirmPressed();

    bool IMenuContext.IsControllerMenuInputActive() => IsControllerMenuInputActive();

    bool IMenuContext.IsCoopLastToDieActive() => IsCoopLastToDieActive();

    bool IMenuContext.IsHostedLastToDieActive() => IsHostedLastToDieActive();

    bool IMenuContext.IsKeyPressed(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.Keys key) => IsKeyPressed(keyboard, key);

    bool IMenuContext.IsTextFieldDoubleClick(OpenGarrison.Client.Game1.TextFieldClickTarget target) => IsTextFieldDoubleClick(target);

    OpenGarrison.Client.LoadedSpriteFrame IMenuContext.LoadSpriteFrameFromPath(string path) => LoadSpriteFrameFromPath(path);

    void IMenuContext.LogBrowserMenuState(int buttonCount) { LogBrowserMenuState(buttonCount); }

    void IMenuContext.ManageJukeboxLibrary() { ManageJukeboxLibrary(); }

    float IMenuContext.MeasureBitmapFontHeight(float scale) => MeasureBitmapFontHeight(scale);

    float IMenuContext.MeasureBitmapFontWidth(string text, float scale) => MeasureBitmapFontWidth(text, scale);

    void IMenuContext.OpenAccountLoginDialog() { OpenAccountLoginDialog(); }

    void IMenuContext.OpenCreditsMenu() { OpenCreditsMenu(); }

    void IMenuContext.OpenDebugMenu() { OpenDebugMenu(); }

    void IMenuContext.OpenFriendsMenu() { OpenFriendsMenu(); }

    void IMenuContext.OpenGameplayClassSelection() { OpenGameplayClassSelection(); }

    void IMenuContext.OpenGameplayTeamSelection() { OpenGameplayTeamSelection(); }

    void IMenuContext.OpenGarrisonBuilderFromMainMenu() { OpenGarrisonBuilderFromMainMenu(); }

    void IMenuContext.OpenGg2LobbyBrowser() { OpenGg2LobbyBrowser(); }

    void IMenuContext.OpenHostSetupMenu() { OpenHostSetupMenu(); }

    void IMenuContext.OpenHudEditor(bool openedFromOptions) { OpenHudEditor(openedFromOptions); }

    void IMenuContext.OpenInGameMenu() { OpenInGameMenu(); }

    void IMenuContext.OpenLastToDieMenu(string statusMessage = default) { OpenLastToDieMenu(statusMessage); }

    void IMenuContext.OpenLobbyBrowser() { OpenLobbyBrowser(); }

    void IMenuContext.OpenMainMenuPage(OpenGarrison.Client.Game1.MainMenuPage page) { OpenMainMenuPage(page); }

    void IMenuContext.OpenManualConnectMenu() { OpenManualConnectMenu(); }

    void IMenuContext.OpenOptionsMenu(bool fromGameplay) { OpenOptionsMenu(fromGameplay); }

    void IMenuContext.OpenPracticeSetupMenu() { OpenPracticeSetupMenu(); }

    void IMenuContext.OpenPracticeVoteMenu() { OpenPracticeVoteMenu(); }

    void IMenuContext.OpenQuitPrompt() { OpenQuitPrompt(); }

    void IMenuContext.OpenSessionJukebox() { OpenSessionJukebox(); }

    void IMenuContext.OpenWatchBrowser() { OpenWatchBrowser(); }

    void IMenuContext.PersistInputBindings() { PersistInputBindings(); }

    void IMenuContext.PrepareJumpMenuMapEntries() { PrepareJumpMenuMapEntries(); }

    void IMenuContext.RefreshFriendPresence() { RefreshFriendPresence(); }

    void IMenuContext.ResetCrtSettings() { ResetCrtSettings(); }

    void IMenuContext.ResetTextFieldClickTarget() { ResetTextFieldClickTarget(); }

    void IMenuContext.ResetWindowSize() { ResetWindowSize(); }

    void IMenuContext.ReturnToGarrisonBuilderFromQuickTest() { ReturnToGarrisonBuilderFromQuickTest(); }

    void IMenuContext.ReturnToLastToDieMenu(string statusMessage = default) { ReturnToLastToDieMenu(statusMessage); }

    void IMenuContext.ReturnToMainMenu(string statusMessage = default) { ReturnToMainMenu(statusMessage); }

    void IMenuContext.SelectAllTextInActiveField(OpenGarrison.Client.Game1.TextFieldClickTarget clickTarget) { SelectAllTextInActiveField(clickTarget); }

    bool IMenuContext.SetClientPluginEnabled(string pluginId, bool enabled) => SetClientPluginEnabled(pluginId, enabled);

    void IMenuContext.SetPersistedMenuStatusMessage(string message) { SetPersistedMenuStatusMessage(message); }

    bool IMenuContext.ShouldUseMouseMenuHover(Microsoft.Xna.Framework.Input.MouseState mouse) => ShouldUseMouseMenuHover(mouse);

    void IMenuContext.StopFaucetMusic() { StopFaucetMusic(); }

    void IMenuContext.StopIngameMusic() { StopIngameMusic(); }

    void IMenuContext.StopLastToDieIngameMusic() { StopLastToDieIngameMusic(); }

    void IMenuContext.ToggleAlwaysRecordGames() { ToggleAlwaysRecordGames(); }

    void IMenuContext.ToggleAudioMuteSetting() { ToggleAudioMuteSetting(); }

    void IMenuContext.ToggleCameraPanningSetting() { ToggleCameraPanningSetting(); }

    void IMenuContext.ToggleControllerAimAssistSetting() { ToggleControllerAimAssistSetting(); }

    void IMenuContext.ToggleControllerFlickToChangeDirectionsSetting() { ToggleControllerFlickToChangeDirectionsSetting(); }

    void IMenuContext.ToggleCrtCurvatureSetting() { ToggleCrtCurvatureSetting(); }

    void IMenuContext.ToggleDamageVignetteSetting() { ToggleDamageVignetteSetting(); }

    void IMenuContext.ToggleDynamicMusicSetting() { ToggleDynamicMusicSetting(); }

    void IMenuContext.ToggleDynamicRagdollSetting() { ToggleDynamicRagdollSetting(); }

    void IMenuContext.ToggleHealerRadarSetting() { ToggleHealerRadarSetting(); }

    void IMenuContext.ToggleHudWeaponDisplayModeSetting() { ToggleHudWeaponDisplayModeSetting(); }

    void IMenuContext.ToggleJukeboxMute() { ToggleJukeboxMute(); }

    void IMenuContext.ToggleKillCamSetting() { ToggleKillCamSetting(); }

    void IMenuContext.ToggleOverheadChatSetting() { ToggleOverheadChatSetting(); }

    void IMenuContext.TogglePersistentSelfNameSetting() { TogglePersistentSelfNameSetting(); }

    void IMenuContext.TogglePortraitRumbleSetting() { TogglePortraitRumbleSetting(); }

    void IMenuContext.TogglePositionSmoothingSetting() { TogglePositionSmoothingSetting(); }

    void IMenuContext.TogglePostGameMvpArtSetting() { TogglePostGameMvpArtSetting(); }

    void IMenuContext.TogglePredictionSetting() { TogglePredictionSetting(); }

    void IMenuContext.ToggleProjectileTeamTintSetting() { ToggleProjectileTeamTintSetting(); }

    void IMenuContext.ToggleScrollWheelWeaponSwapSetting() { ToggleScrollWheelWeaponSwapSetting(); }

    void IMenuContext.ToggleShowHealerSetting() { ToggleShowHealerSetting(); }

    void IMenuContext.ToggleShowHealingSetting() { ToggleShowHealingSetting(); }

    void IMenuContext.ToggleShowHealthBarSetting() { ToggleShowHealthBarSetting(); }

    void IMenuContext.ToggleShowPlayerNamesSetting() { ToggleShowPlayerNamesSetting(); }

    void IMenuContext.ToggleShowShieldBarSetting() { ToggleShowShieldBarSetting(); }

    void IMenuContext.ToggleSpatialVoice() { ToggleSpatialVoice(); }

    void IMenuContext.ToggleSpriteDropShadowSetting() { ToggleSpriteDropShadowSetting(); }

    void IMenuContext.ToggleStuckArrowsSetting() { ToggleStuckArrowsSetting(); }

    void IMenuContext.ToggleUberOutlinesSetting() { ToggleUberOutlinesSetting(); }

    void IMenuContext.ToggleVoiceChannelMembership() { ToggleVoiceChannelMembership(); }

    void IMenuContext.ToggleVoiceMute() { ToggleVoiceMute(); }

    void IMenuContext.ToggleVoiceTeamOnly() { ToggleVoiceTeamOnly(); }

    void IMenuContext.ToggleVSyncSetting() { ToggleVSyncSetting(); }

    void IMenuContext.ToggleWeaponRotationSourceSetting() { ToggleWeaponRotationSourceSetting(); }

    void IMenuContext.ToggleWeaponRotationStyleSetting() { ToggleWeaponRotationStyleSetting(); }

    string IMenuContext.TrimBitmapMenuText(string text, float maxWidth, float scale) => TrimBitmapMenuText(text, maxWidth, scale);

    bool IMenuContext.TryConsumeControllerMenuNavigation(out int horizontal, out int vertical) => TryConsumeControllerMenuNavigation(out horizontal, out vertical);

    bool IMenuContext.TryDrawScreenSprite(string spriteName, int frameIndex, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color tint, Microsoft.Xna.Framework.Vector2 scale, float rotation) => TryDrawScreenSprite(spriteName, frameIndex, position, tint, scale, rotation);

    bool IMenuContext.TryDrawScreenSprite(string spriteName, int frameIndex, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color tint, Microsoft.Xna.Framework.Vector2 scale) => TryDrawScreenSprite(spriteName, frameIndex, position, tint, scale);

    bool IMenuContext.TryGetPressedControllerButtonBinding(out OpenGarrison.Core.ControllerButtonBinding binding) => TryGetPressedControllerButtonBinding(out binding);

    bool IMenuContext.TryHandleScrollbarDrag(Microsoft.Xna.Framework.Input.MouseState mouse, Microsoft.Xna.Framework.Input.MouseState previousMouse, System.Object owner, Microsoft.Xna.Framework.Rectangle trackBounds, ref int scrollOffset, int itemCount, int visibleItemCount, int minThumbHeight = 24) => TryHandleScrollbarDrag(mouse, previousMouse, owner, trackBounds, ref scrollOffset, itemCount, visibleItemCount, minThumbHeight);

    bool IMenuContext.TryHandleServerLauncherBackAction() => TryHandleServerLauncherBackAction();

    bool IMenuContext.TryPlayLegacyReplay(string replayPath, bool addConsoleFeedback, bool clearQueuedReplays = true) => TryPlayLegacyReplay(replayPath, addConsoleFeedback, clearQueuedReplays);

    bool IMenuContext.TryPlayOpenGarrisonDemo(string demoPath, bool addConsoleFeedback) => TryPlayOpenGarrisonDemo(demoPath, addConsoleFeedback);

    void IMenuContext.UpdateAccountDialog(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateAccountDialog(keyboard, mouse); }

    void IMenuContext.UpdateClientPowersMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateClientPowersMenu(keyboard, mouse); }

    void IMenuContext.UpdateControlsMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateControlsMenu(keyboard, mouse); }

    void IMenuContext.UpdateCreditsMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateCreditsMenu(keyboard, mouse); }

    void IMenuContext.UpdateCustomBubbleEditor(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateCustomBubbleEditor(keyboard, mouse); }

    bool IMenuContext.UpdateDevMessagePopup(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) => UpdateDevMessagePopup(keyboard, mouse);

    void IMenuContext.UpdateFriendsMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateFriendsMenu(keyboard, mouse); }

    void IMenuContext.UpdateGarrisonBuilderEditor(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse, float deltaSeconds) { UpdateGarrisonBuilderEditor(keyboard, mouse, deltaSeconds); }

    void IMenuContext.UpdateHostSetupMenu(Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateHostSetupMenu(mouse); }

    void IMenuContext.UpdateJumpMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateJumpMenu(keyboard, mouse); }

    void IMenuContext.UpdateLastToDieMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateLastToDieMenu(keyboard, mouse); }

    void IMenuContext.UpdateLobbyBrowserResponses() { UpdateLobbyBrowserResponses(); }

    void IMenuContext.UpdateLobbyBrowserState(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateLobbyBrowserState(keyboard, mouse); }

    void IMenuContext.UpdateManualConnectMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateManualConnectMenu(keyboard, mouse); }

    void IMenuContext.UpdateOptionsMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdateOptionsMenu(keyboard, mouse); }

    void IMenuContext.UpdatePlayerNamePrompt(Microsoft.Xna.Framework.Input.KeyboardState keyboard) { UpdatePlayerNamePrompt(keyboard); }

    void IMenuContext.UpdatePluginOptionsMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdatePluginOptionsMenu(keyboard, mouse); }

    void IMenuContext.UpdatePracticeSetupMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) { UpdatePracticeSetupMenu(keyboard, mouse); }

    bool IMenuContext.UpdateQuitPrompt(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse) => UpdateQuitPrompt(keyboard, mouse);

}
