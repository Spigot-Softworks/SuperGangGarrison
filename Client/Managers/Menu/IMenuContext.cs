#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenGarrison.Client;

public interface IMenuContext
{
    MenuManager Menus { get; }
    SessionManager Session { get; }
    HostingManager Hosting { get; }
    bool _accountDialogOpen { get; set; }
    int _accountGlobalRank { get; set; }
    bool _accountGlobalRankKnown { get; set; }
    bool _accountIsProtected { get; set; }
    long _accountLifetimePoints { get; set; }
    long _accountWalletBalance { get; set; }
    bool _audioMuted { get; set; }
    int _bloodAmountLevel { get; set; }
    int _bloodPersistenceSeconds { get; set; }
    int _bloodRenderMode { get; set; }
    bool _brandIntroActive { get; set; }
    bool _builderEditorEnabled { get; set; }
    bool _cameraPanningEnabled { get; set; }
    OpenGarrison.ClientShared.ClientIdentityDocument _clientIdentity { get; }
    OpenGarrison.Client.ClientPluginHost _clientPluginHost { get; set; }
    bool _clientPowersOpen { get; set; }
    bool _clientPowersOpenedFromGameplay { get; set; }
    OpenGarrison.ClientShared.ClientSettings _clientSettings { get; }
    int _combatMusicVolumePercent { get; set; }
    int _controlsHoverIndex { get; set; }
    bool _controlsMenuOpen { get; set; }
    bool _controlsMenuOpenedFromGameplay { get; set; }
    int _controlsPageIndex { get; set; }
    int _controlsScrollOffset { get; set; }
    int _corpseDurationMode { get; set; }
    int _corpseFadeMode { get; set; }
    bool _creditsOpen { get; set; }
    bool _creditsScrollInitialized { get; set; }
    int _cursorSizePercent { get; set; }
    bool _customBubbleEditorOpen { get; set; }
    bool _damageVignetteEnabled { get; set; }
    int _damageVignetteIntensityPercent { get; set; }
    bool _debugMenuAwaitingEscapeRelease { get; set; }
    bool _debugMenuEnabled { get; set; }
    int _debugMenuHoverIndex { get; set; }
    bool _debugMenuOpen { get; set; }
    bool _debugRocketCollisionsEnabled { get; set; }
    OpenGarrison.Core.DisplayModeKind _displayMode { get; set; }
    bool _dynamicMusicEnabled { get; set; }
    bool _burnCharredCorpsesEnabled { get; set; }
    bool _dynamicRagdollEnabled { get; set; }
    bool _editingFriendCode { get; set; }
    bool _editingFriendNickname { get; set; }
    bool _editingPlayerName { get; set; }
    bool _enablePrediction { get; set; }
    int _flameRenderMode { get; set; }
    int _frameRateLimit { get; set; }
    OpenGarrison.ClientShared.FriendListDocument _friendList { get; }
    string _friendNicknameInputBuffer { get; set; }
    bool _friendsMenuAddingFriend { get; set; }
    int _friendsMenuHoverIndex { get; set; }
    bool _friendsMenuOpen { get; set; }
    int _friendsMenuSelectedIndex { get; set; }
    OpenGarrison.Client.Game1.FriendsMenuTab _friendsMenuTab { get; set; }
    OpenGarrison.Client.Game1.GameplaySessionKind _gameplaySessionKind { get; set; }
    bool _garrisonBuilderQuickTestActive { get; set; }
    int _gibLevel { get; set; }
    Microsoft.Xna.Framework.GraphicsDeviceManager _graphics { get; }
    Microsoft.Xna.Framework.Graphics.Effect _grayscaleEffect { get; set; }
    bool _healerRadarEnabled { get; set; }
    OpenGarrison.Client.Game1.HostSetupEditField _hostSetupEditField { get; set; }
    bool _hostSetupOpen { get; set; }
    OpenGarrison.Client.Game1.HostSetupFormState _hostSetupState { get; }
    bool _hudShowOnlyActiveWeapon { get; set; }
    bool _inGameMenuAwaitingEscapeRelease { get; set; }
    int _inGameMenuHoverIndex { get; set; }
    bool _inGameMenuOpen { get; set; }
    int _ingameMusicVolumePercent { get; set; }
    OpenGarrison.Core.IngameResolutionKind _ingameResolution { get; set; }
    OpenGarrison.Client.InputBindingsSettings _inputBindings { get; }
    bool _jukeboxMenuOpen { get; set; }
    int _jumpMenuHoverIndex { get; set; }
    bool _jumpMenuOpen { get; set; }
    bool _killCamEnabled { get; set; }
    int _lastToDieMenuHoverIndex { get; set; }
    bool _lastToDieMenuOpen { get; set; }
    OpenGarrison.Client.Game1.LastToDieMenuPage _lastToDieMenuPage { get; set; }
    bool _lastToDieRoomCodeJoinOpen { get; set; }
    bool _lobbyBrowserOpen { get; set; }
    OpenGarrison.Core.LowHealthColorMode _lowHealthColorMode { get; set; }
    bool _mainMenuBottomBarHover { get; set; }
    bool _mainMenuChromeHidden { get; set; }
    int _mainMenuHoverIndex { get; set; }
    bool _mainMenuOpen { get; set; }
    OpenGarrison.Client.Game1.MainMenuPage _mainMenuPage { get; set; }
    int _manualConnectControllerIndex { get; set; }
    bool _manualConnectOpen { get; set; }
    int _masterVolumePercent { get; set; }
    string _menuBackgroundAttributionText { get; set; }
    string _menuBackgroundFailedPath { get; set; }
    OpenGarrison.Core.MenuBackgroundMode _menuBackgroundMode { get; set; }
    OpenGarrison.Client.LoadedSpriteFrame _menuBackgroundTexture { get; set; }
    string _menuBackgroundTexturePath { get; set; }
    int _menuImageFrame { get; set; }
    int _menuMusicVolumePercent { get; set; }
    OpenGarrison.Client.LoadedSpriteFrame _menuPlaqueTallTexture { get; set; }
    OpenGarrison.Client.LoadedSpriteFrame _menuPlaqueTexture { get; set; }
    string _menuStatusMessage { get; set; }
    OpenGarrison.Core.MusicMode _musicMode { get; set; }
    bool _namePromptOpen { get; set; }
    OpenGarrison.Client.NetworkGameClient _networkClient { get; }
    int _optionsHoverIndex { get; set; }
    bool _optionsMenuOpen { get; set; }
    bool _optionsMenuOpenedFromGameplay { get; set; }
    int _optionsPageIndex { get; set; }
    int _optionsScrollOffset { get; set; }
    bool _overheadChatEnabled { get; set; }
    int _particleMode { get; set; }
    OpenGarrison.Client.PlayerHostedRoomSession _peerRoomSession { get; set; }
    Nullable<OpenGarrison.Client.Game1.ControllerControlsMenuBinding> _pendingControllerControlsBinding { get; set; }
    Nullable<OpenGarrison.Client.Game1.ControlsMenuBinding> _pendingControlsBinding { get; set; }
    OpenGarrison.Client.Plugins.ClientPluginKeyOptionItem _pendingPluginOptionsKeyItem { get; set; }
    Microsoft.Xna.Framework.Graphics.Texture2D _pixel { get; set; }
    bool _pixelPerfectWeaponRotation { get; set; }
    int _playerCardSizeMode { get; set; }
    string _playerNameEditBuffer { get; set; }
    int _playerNameEditCursorIndex { get; set; }
    int _playerNameEditSelectionStart { get; set; }
    int _pluginOptionsHoverIndex { get; set; }
    bool _pluginOptionsMenuOpen { get; set; }
    bool _pluginOptionsMenuOpenedFromGameplay { get; set; }
    int _pluginOptionsScrollOffset { get; set; }
    bool _portraitRumbleEnabled { get; set; }
    bool _positionSmoothingEnabled { get; set; }
    bool _postGameMvpArtEnabled { get; set; }
    bool _practiceSetupOpen { get; set; }
    Microsoft.Xna.Framework.Input.KeyboardState _previousKeyboard { get; set; }
    Microsoft.Xna.Framework.Input.MouseState _previousMouse { get; set; }
    bool _projectileTeamTintEnabled { get; set; }
    bool _quitPromptOpen { get; set; }
    OpenGarrison.Client.GameMakerRuntimeAssetCache _runtimeAssets { get; set; }
    string _selectedPluginOptionsPluginId { get; set; }
    bool _showHealerEnabled { get; set; }
    bool _showHealingEnabled { get; set; }
    bool _showHealthBarEnabled { get; set; }
    bool _showPersistentSelfNameEnabled { get; set; }
    bool _showPlayerNamesEnabled { get; set; }
    bool _showShieldBarEnabled { get; set; }
    int _soundEffectsVolumePercent { get; set; }
    Microsoft.Xna.Framework.Graphics.SpriteBatch _spriteBatch { get; set; }
    bool _spriteDropShadowEnabled { get; set; }
    bool _stuckArrowsEnabled { get; set; }
    OpenGarrison.Core.WeaponBobMode _weaponBobMode { get; set; }
    bool _uberOutlineEnabled { get; set; }
    bool _useLocalWeaponRotation { get; set; }
    OpenGarrison.Client.VoiceChatSettings _voiceSettings { get; set; }
    OpenGarrison.Core.WindowSizeKind _windowSize { get; set; }
    OpenGarrison.Core.SimulationWorld _world { get; set; }
    bool CanShortenAccountFriendCode { get; }
    Microsoft.Xna.Framework.Graphics.GraphicsDevice GraphicsDevice { get; }
    bool IsAccountOperationPending { get; }
    bool IsCrtSettingsUnlockedForSession { get; }
    bool IsEmbeddedSessionOwner { get; }
    bool IsLastToDieSessionActive { get; }
    bool IsPeerRoomOwner { get; }
    bool IsPracticeSessionActive { get; }
    OpenGarrison.Client.ScrollbarDragController ScrollbarDrag { get; }
    int ViewportHeight { get; }
    int ViewportWidth { get; }
    void AddConsoleLine(string line);
    void AddPluginMenuActions(List<OpenGarrison.Client.Game1.MenuPageAction> actions, OpenGarrison.Client.Plugins.ClientPluginMenuLocation location, int insertIndex = -1);
    void AdjustBloodPersistenceSeconds(int step);
    void AdjustCombatMusicVolume(int deltaPercent);
    void AdjustControllerAimAssistStrengthSetting(float delta);
    void AdjustControllerAimDeadzoneSetting(float delta);
    void AdjustControllerAimDistanceTier1Setting(float delta);
    void AdjustControllerAimDistanceTier2Setting(float delta);
    void AdjustControllerAimDistanceTier3Setting(float delta);
    void AdjustControllerScopedPrecisionSpeedSetting(float delta);
    void AdjustCrtBrightnessSetting(int delta);
    void AdjustCursorSizeSetting(int step);
    void AdjustIngameMusicVolume(int deltaPercent);
    void AdjustMasterVolume(int deltaPercent);
    void AdjustMenuMusicVolume(int deltaPercent);
    void AdjustSoundEffectsVolume(int deltaPercent);
    void AdjustVoiceSetting(string setting, int amount);
    void AdvanceBrandLogoFlame(float elapsedSeconds);
    void ApplyControllerControlsBinding(OpenGarrison.Client.Game1.ControllerControlsMenuBinding binding, OpenGarrison.Core.ControllerButtonBinding input);
    void ApplyControlsBinding(OpenGarrison.Client.Game1.ControlsMenuBinding binding, OpenGarrison.Client.InputBinding input);
    void BeginAccountProfileRefresh(bool silent);
    void BeginEditingPlayerName();
    void BeginProtectAccount();
    void BeginShortenAccountFriendCode();
    List<OpenGarrison.Client.Game1.MenuPageButton> BuildMainMenuButtons();
    void CancelFriendCodeJoin();
    void CancelManagedRoomRequest();
    void CancelPendingHostedLastToDieRelayLaunch();
    bool CanLoadSpriteFrameFromPath(string path);
    bool CanOfferGameplaySelectionMenusFromInGameMenu();
    void CloseAccountDialog();
    void CloseAllHostSetupMapPreviews();
    void CloseFriendsContextMenu();
    void CloseHostSetupMenu(bool clearStatus = false);
    void CloseLobbyBrowser(bool clearStatus);
    void CloseOptionsMenu();
    void ClosePlayerCardOverlay();
    void ClosePluginOptionsMenu();
    void ConsumeControllerMenuConfirmPress();
    void CycleBloodAmountSetting();
    void CycleBloodRenderModeSetting();
    void CycleBuildMenuStyleSetting();
    void CycleControllerAimAssistStrengthSetting();
    void CycleControllerAimDeadzoneSetting();
    void CycleControllerAimDistanceTier1Setting();
    void CycleControllerAimDistanceTier2Setting();
    void CycleControllerAimDistanceTier3Setting();
    void CycleControllerInputModeSetting();
    void CycleControllerReticleModeSetting();
    void CycleControllerScopedPrecisionSpeedSetting();
    void CycleCorpseDurationSetting();
    void CycleCorpseFadeModeSetting();
    void CycleCrtBrightnessSetting();
    void CycleCrtPresetSetting();
    void CycleCrtQualitySetting();
    void CycleCrtSignalModeSetting();
    void CycleCursorSizeSetting();
    void CycleDamageVignetteIntensitySetting();
    void CycleDisplayModeSetting();
    void CycleFlameRenderModeSetting();
    void CycleFrameRateLimitSetting();
    void CycleGibLevelSetting();
    void CycleIngameResolutionSetting();
    void CycleLowHealthColorModeSetting();
    void CycleMenuBackgroundModeSetting();
    void CycleMusicModeSetting();
    void CycleParticleModeSetting();
    void CyclePlayerCardSizeSetting();
    void CycleSwapWeaponsBindingSetting();
    void CycleVoiceMicrophone();
    void CycleVoiceMode();
    void CycleWindowSizeSetting();
    void DismissCustomBubbleEditor();
    void DrawAccountDialog();
    void DrawBitmapFontText(string text, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color color, float scale, float rotation);
    void DrawBitmapFontText(string text, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color color, float scale = 1f);
    void DrawBitmapFontTextRightAligned(string text, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color color, float scale);
    void DrawBottomCenterPlaqueButton(OpenGarrison.Client.Game1.PlaqueMenuLayout layout, string label, bool hovered, float textScaleMultiplier);
    void DrawBottomRightPlaqueButton(OpenGarrison.Client.Game1.PlaqueMenuLayout layout, string label, bool hovered, float textScaleMultiplier);
    void DrawClientPowersMenu();
    void DrawControlsMenu();
    void DrawCreditsMenu();
    void DrawCurrentMainMenuPage(IReadOnlyList<OpenGarrison.Client.Game1.MenuPageButton> buttons);
    void DrawCustomBubbleEditor();
    void DrawDevMessagePopup();
    bool DrawFlamingBrandLogo(Microsoft.Xna.Framework.Rectangle destination, float flameBlend = 1f, float opacity = 1f, float flashAmount = 0f);
    void DrawFriendsMenu();
    void DrawGarrisonBuilderEditorOverlay(Microsoft.Xna.Framework.Input.MouseState mouse);
    void DrawHostSetupMenu();
    void DrawJumpMenu();
    void DrawLastToDieMenu();
    void DrawLoadedSpriteFrame(OpenGarrison.Client.LoadedSpriteFrame frame, Microsoft.Xna.Framework.Rectangle destinationRectangle, Microsoft.Xna.Framework.Color tint);
    void DrawLoadedSpriteFrame(OpenGarrison.Client.LoadedSpriteFrame frame, Microsoft.Xna.Framework.Vector2 position, Nullable<Microsoft.Xna.Framework.Rectangle> sourceRectangle, Microsoft.Xna.Framework.Color tint, float rotation, Microsoft.Xna.Framework.Vector2 origin, Microsoft.Xna.Framework.Vector2 scale, Microsoft.Xna.Framework.Graphics.SpriteEffects effects, float layerDepth);
    void DrawLobbyBrowserMenu();
    void DrawMainMenuBottomBar();
    void DrawManualConnectMenu();
    void DrawMenuButtonScaled(Microsoft.Xna.Framework.Rectangle bounds, string label, bool highlighted, float textScale, bool enabled = true);
    void DrawMenuInputBoxScaled(Microsoft.Xna.Framework.Rectangle bounds, string text, bool active, float textScale, int cursorIndex = -1, int selectionStart = -1, bool enabled = true);
    void DrawMenuPanelBackdrop(Microsoft.Xna.Framework.Rectangle rectangle, float alpha);
    void DrawMenuStatusText();
    void DrawOptionsMenu();
    void DrawPlaqueMenuButton(OpenGarrison.Client.LoadedSpriteFrame texture, Microsoft.Xna.Framework.Rectangle bounds, string label, bool hovered, float plaqueScale, float textScaleMultiplier);
    void DrawPlaqueMenuLayout(OpenGarrison.Client.Game1.PlaqueMenuLayout layout, IReadOnlyList<OpenGarrison.Client.Game1.MenuPageAction> stackedActions, Nullable<OpenGarrison.Client.Game1.MenuPageAction> soloAction, bool drawBottomBarButton, string bottomBarLabel, int hoveredStackedIndex, bool soloHovered, bool bottomBarHovered, float textScaleMultiplier = 1f);
    void DrawPlayerNamePrompt();
    void DrawPluginOptionsMenu();
    void DrawPracticeSetupMenu();
    void DrawQuitPrompt();
    void DrawRoundedRectangleOutline(Microsoft.Xna.Framework.Rectangle bounds, Microsoft.Xna.Framework.Color fillColor, Microsoft.Xna.Framework.Color outlineColor, int outlineThickness, int radius);
    void DrawSpriteFrame(OpenGarrison.Client.LoadedSpriteFrame frame, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color tint, float rotation, Microsoft.Xna.Framework.Vector2 origin, Microsoft.Xna.Framework.Vector2 scale, Microsoft.Xna.Framework.Graphics.SpriteEffects effects = Microsoft.Xna.Framework.Graphics.SpriteEffects.None);
    void EnsureMenuMusicPlaying();
    void EnsurePlayerNamePrompt();
    void EnsureSelectedHostMapVisible();
    string GetAccountRecoveryKeyDisplay();
    string GetAccountStatusDisplay();
    Microsoft.Xna.Framework.Rectangle GetBottomCenterPlaqueButtonBounds(OpenGarrison.Client.Game1.PlaqueMenuLayout layout);
    Microsoft.Xna.Framework.Rectangle GetBottomRightPlaqueButtonBounds(OpenGarrison.Client.Game1.PlaqueMenuLayout layout);
    OpenGarrison.Client.Game1.PlaqueMenuLayout GetCenteredPlaqueMenuLayout(bool tall, int stackedButtonCount, bool includeSoloButton, bool includeBottomBarButton);
    OpenGarrison.Client.Plugins.ClientPluginMainMenuBackgroundOverride GetClientPluginMainMenuBackgroundOverride();
    List<(OpenGarrison.Client.Game1.ControllerControlsMenuBinding Binding, string Label, OpenGarrison.Core.ControllerButtonBinding Input)> GetControllerControlsMenuBindings();
    string GetControlsBindingLabel(OpenGarrison.Client.Game1.ControlsMenuBinding binding);
    List<(OpenGarrison.Client.Game1.ControlsMenuBinding Binding, string Label, OpenGarrison.Client.InputBinding Input)> GetControlsMenuBindings();
    string GetCrtPresetLabel();
    string GetCrtQualityStatusLabel();
    string GetCrtSignalModeLabel();
    Microsoft.Xna.Framework.Input.MouseState GetFrameMouseState();
    string GetFriendNicknameInputDefault();
    string GetGameplayExitStatusMessage();
    OpenGarrison.Client.LoadedSpriteFrame GetMenuStackedButtonTexture(int index, int count);
    OpenGarrison.Client.LoadedGameMakerSprite GetResolvedSprite(string spriteName);
    List<OpenGarrison.Client.Game1.MenuPageAction> GetSessionJukeboxActions();
    string GetSwapWeaponsBindingLabel();
    string GetVoiceChannelActionLabel();
    string GetVoiceChannelLabel();
    string GetVoiceModeLabel();
    string GetVoiceMuteActionLabel();
    string GetVoiceStatusLabel();
    bool HasClientPluginOptions();
    void InitializeFriendCodeCursor();
    void InitializeFriendNicknameCursor();
    bool IsControllerMenuBackPressed();
    bool IsControllerMenuConfirmPressed();
    bool IsControllerMenuInputActive();
    bool IsCoopLastToDieActive();
    bool IsHostedLastToDieActive();
    bool IsKeyPressed(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.Keys key);
    bool IsTextFieldDoubleClick(OpenGarrison.Client.Game1.TextFieldClickTarget target);
    OpenGarrison.Client.LoadedSpriteFrame LoadSpriteFrameFromPath(string path);
    void LogBrowserMenuState(int buttonCount);
    void ManageJukeboxLibrary();
    float MeasureBitmapFontHeight(float scale);
    float MeasureBitmapFontWidth(string text, float scale);
    void OpenAccountLoginDialog();
    void OpenCreditsMenu();
    void OpenDebugMenu();
    void OpenFriendsMenu();
    void OpenGameplayClassSelection();
    void OpenGameplayTeamSelection();
    void OpenGarrisonBuilderFromMainMenu();
    void OpenGg2LobbyBrowser();
    void OpenHostSetupMenu();
    void OpenHudEditor(bool openedFromOptions);
    void OpenInGameMenu();
    void OpenLastToDieMenu(string statusMessage = default);
    void OpenLobbyBrowser();
    void OpenMainMenuPage(OpenGarrison.Client.Game1.MainMenuPage page);
    void OpenManualConnectMenu();
    void OpenOptionsMenu(bool fromGameplay);
    void OpenPracticeSetupMenu();
    void OpenPracticeVoteMenu();
    void OpenQuitPrompt();
    void OpenSessionJukebox();
    void OpenWatchBrowser();
    void PersistInputBindings();
    void PrepareJumpMenuMapEntries();
    void RefreshFriendPresence();
    void ResetCrtSettings();
    void ResetTextFieldClickTarget();
    void ResetWindowSize();
    void ReturnToGarrisonBuilderFromQuickTest();
    void ReturnToLastToDieMenu(string statusMessage = default);
    void ReturnToMainMenu(string statusMessage = default);
    void SelectAllTextInActiveField(OpenGarrison.Client.Game1.TextFieldClickTarget clickTarget);
    bool SetClientPluginEnabled(string pluginId, bool enabled);
    void SetPersistedMenuStatusMessage(string message);
    bool ShouldUseMouseMenuHover(Microsoft.Xna.Framework.Input.MouseState mouse);
    void StopFaucetMusic();
    void StopIngameMusic();
    void StopLastToDieIngameMusic();
    void ToggleAlwaysRecordGames();
    void ToggleAudioMuteSetting();
    void ToggleCameraPanningSetting();
    void ToggleControllerAimAssistSetting();
    void ToggleControllerFlickToChangeDirectionsSetting();
    void ToggleCrtCurvatureSetting();
    void ToggleDamageVignetteSetting();
    void ToggleDynamicMusicSetting();
    void ToggleBurnCharredCorpsesSetting();
    void ToggleDynamicRagdollSetting();
    void ToggleHealerRadarSetting();
    void ToggleHudWeaponDisplayModeSetting();
    void ToggleJukeboxMute();
    void ToggleKillCamSetting();
    void ToggleOverheadChatSetting();
    void TogglePersistentSelfNameSetting();
    void TogglePortraitRumbleSetting();
    void TogglePositionSmoothingSetting();
    void TogglePostGameMvpArtSetting();
    void TogglePredictionSetting();
    void ToggleProjectileTeamTintSetting();
    void ToggleScrollWheelWeaponSwapSetting();
    void ToggleShowHealerSetting();
    void ToggleShowHealingSetting();
    void ToggleShowHealthBarSetting();
    void ToggleShowPlayerNamesSetting();
    void ToggleShowShieldBarSetting();
    void ToggleSpatialVoice();
    void ToggleSpriteDropShadowSetting();
    void CycleWeaponBobSetting();
    void ToggleStuckArrowsSetting();
    void ToggleUberOutlinesSetting();
    void ToggleVoiceChannelMembership();
    void ToggleVoiceMute();
    void ToggleVoiceTeamOnly();
    void ToggleVSyncSetting();
    void ToggleWeaponRotationSourceSetting();
    void ToggleWeaponRotationStyleSetting();
    string TrimBitmapMenuText(string text, float maxWidth, float scale);
    bool TryConsumeControllerMenuNavigation(out int horizontal, out int vertical);
    bool TryDrawScreenSprite(string spriteName, int frameIndex, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color tint, Microsoft.Xna.Framework.Vector2 scale, float rotation);
    bool TryDrawScreenSprite(string spriteName, int frameIndex, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color tint, Microsoft.Xna.Framework.Vector2 scale);
    bool TryGetPressedControllerButtonBinding(out OpenGarrison.Core.ControllerButtonBinding binding);
    bool TryHandleScrollbarDrag(Microsoft.Xna.Framework.Input.MouseState mouse, Microsoft.Xna.Framework.Input.MouseState previousMouse, System.Object owner, Microsoft.Xna.Framework.Rectangle trackBounds, ref int scrollOffset, int itemCount, int visibleItemCount, int minThumbHeight = 24);
    bool TryHandleServerLauncherBackAction();
    bool TryPlayLegacyReplay(string replayPath, bool addConsoleFeedback, bool clearQueuedReplays = true);
    bool TryPlayOpenGarrisonDemo(string demoPath, bool addConsoleFeedback);
    void UpdateAccountDialog(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateClientPowersMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateControlsMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateCreditsMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateCustomBubbleEditor(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    bool UpdateDevMessagePopup(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateFriendsMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateGarrisonBuilderEditor(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse, float deltaSeconds);
    void UpdateHostSetupMenu(Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateJumpMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateLastToDieMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateLobbyBrowserResponses();
    void UpdateLobbyBrowserState(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateManualConnectMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdateOptionsMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdatePlayerNamePrompt(Microsoft.Xna.Framework.Input.KeyboardState keyboard);
    void UpdatePluginOptionsMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    void UpdatePracticeSetupMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
    bool UpdateQuitPrompt(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse);
}
