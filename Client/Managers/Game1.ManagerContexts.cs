#nullable enable

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Client;

public partial class Game1 : IAudioContext, IInputContext, IMenuContext, ISessionContext, IHostingContext, IPluginContext, IGameplayContext, IHudContext, IDiscordContext, IRenderContext
{
    GameplayManager IRenderContext.GameplayManager => _gameplayManager;
    GameplayPlayerRenderController IRenderContext.GameplayPlayerRenderer => _gameplayPlayerRenderController;
    GameplayDeadBodyRenderController IRenderContext.GameplayDeadBodyRenderer => _gameplayDeadBodyRenderController;
    GameplayPlayerSpriteRenderController IRenderContext.GameplayPlayerSpriteRenderer => _gameplayPlayerSpriteRenderController;
    GameplayPlayerStatusEffectRenderController IRenderContext.GameplayPlayerStatusEffectRenderer => _gameplayPlayerStatusEffectRenderController;
    GameplayWeaponRenderController IRenderContext.GameplayWeaponRenderer => _gameplayWeaponRenderController;
    int IRenderContext._corpseDurationMode { get => _corpseDurationMode; set => _corpseDurationMode = value; }
    bool IRenderContext._dynamicRagdollEnabled { get => _dynamicRagdollEnabled; set => _dynamicRagdollEnabled = value; }
    bool IRenderContext._pixelPerfectWeaponRotation { get => _pixelPerfectWeaponRotation; set => _pixelPerfectWeaponRotation = value; }
    bool IRenderContext._showHealthBarEnabled { get => _showHealthBarEnabled; set => _showHealthBarEnabled = value; }
    bool IRenderContext._showShieldBarEnabled { get => _showShieldBarEnabled; set => _showShieldBarEnabled = value; }
    bool IRenderContext._uberOutlineEnabled { get => _uberOutlineEnabled; set => _uberOutlineEnabled = value; }
    bool IRenderContext._useLocalWeaponRotation { get => _useLocalWeaponRotation; set => _useLocalWeaponRotation = value; }
    Dictionary<int, Game1.RetainedDeadBodyVisual> IRenderContext._trackedDeadBodyVisuals => _trackedDeadBodyVisuals;
    List<Game1.RetainedDeadBodyVisual> IRenderContext._retainedDeadBodies => _retainedDeadBodies;
    List<int> IRenderContext._staleTrackedDeadBodyIds => _staleTrackedDeadBodyIds;
    Dictionary<int, Game1.ImmediateNetworkDeadBodyVisual> IRenderContext._immediateNetworkDeadBodies => _immediateNetworkDeadBodies;
    List<int> IRenderContext._staleImmediateNetworkDeadBodyPlayerIds => _staleImmediateNetworkDeadBodyPlayerIds;
    IReadOnlyList<Game1.CivvieUmbrellaShieldBlockVisual> IRenderContext._civvieUmbrellaShieldBlockVisuals => _civvieUmbrellaShieldBlockVisuals;
    Dictionary<int, Game1.PlayerRenderState> IRenderContext._playerRenderStates => _playerRenderStates;
    Vector2 IRenderContext._predictedLocalPlayerVelocity => _predictedLocalPlayerVelocity;
    bool IRenderContext.ShouldMeasureClientPerformanceDurations() => ShouldMeasureClientPerformanceDurations();
    void IRenderContext.LogBrowserDrawFrameState(GameTime gameTime) => LogBrowserFrameState("draw", ref _browserDebugDrawCount, gameTime);
    void IRenderContext.ApplyFrameRateLimit() => ApplyFrameRateLimit();
    void IRenderContext.DrawBase(GameTime gameTime) => base.Draw(gameTime);
    void IRenderContext.RecordBrowserDrawDuration(long browserDrawStartTimestamp) => RecordBrowserDrawDuration(browserDrawStartTimestamp);
    bool IRenderContext._preLaunchSplashDismissed { get => _preLaunchSplashDismissed; set => _preLaunchSplashDismissed = value; }

    int IRenderContext.AllocateRemainsSortKey() => AllocateRemainsSortKey();
    Game1.WeaponRenderDefinition IRenderContext.ApplyPlayerSkinWeapon(PlayerEntity player, GameplayItemPresentationDefinition presentation, Game1.WeaponRenderDefinition definition, bool standing) => ApplyPlayerSkinWeapon(player, presentation, definition, standing);
    bool IRenderContext.CanUseLocalPrediction() => CanUseLocalPrediction();
    void IRenderContext.DrawAfterburnOverlay(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, float visibilityAlpha) => DrawAfterburnOverlay(player, renderPosition, cameraPosition, visibilityAlpha);
    void IRenderContext.DrawCapturedPointHealingGhosting(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, float visibilityAlpha, Game1.PlayerBodySpriteSelection bodySelection) => DrawCapturedPointHealingGhosting(player, renderPosition, cameraPosition, visibilityAlpha, bodySelection);
    void IRenderContext.DrawCenteredHudSprite(string spriteName, int frameIndex, Vector2 visualCenter, Color tint, Vector2 scale) => DrawCenteredHudSprite(spriteName, frameIndex, visualCenter, tint, scale);
    void IRenderContext.DrawChatBubble(PlayerEntity player, Vector2 cameraPosition) => DrawChatBubble(player, cameraPosition);
    void IRenderContext.DrawDominationIndicator(PlayerEntity player, Vector2 cameraPosition, float visibilityAlpha) => DrawDominationIndicator(player, cameraPosition, visibilityAlpha);
    void IRenderContext.DrawEvasionMissPopup(PlayerEntity player, Vector2 cameraPosition) => DrawEvasionMissPopup(player, cameraPosition);
    void IRenderContext.DrawExperimentalCryoOverlays(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, float visibilityAlpha, Game1.PlayerBodySpriteSelection bodySelection) => DrawExperimentalCryoOverlays(player, renderPosition, cameraPosition, visibilityAlpha, bodySelection);
    void IRenderContext.DrawExperimentalDemoknightChargeBlur(PlayerEntity player, Vector2 cameraPosition, Color spriteTint, float visibilityAlpha, Game1.PlayerBodySpriteSelection bodySelection) => DrawExperimentalDemoknightChargeBlur(player, cameraPosition, spriteTint, visibilityAlpha, bodySelection);
    void IRenderContext.DrawExperimentalEssenceExtractorOverlay(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, float visibilityAlpha, Game1.PlayerBodySpriteSelection bodySelection) => DrawExperimentalEssenceExtractorOverlay(player, renderPosition, cameraPosition, visibilityAlpha, bodySelection);
    void IRenderContext.DrawExperimentalStickyGibBloodOverlay(PlayerEntity player, Vector2 cameraPosition, float visibilityAlpha) => DrawExperimentalStickyGibBloodOverlay(player, cameraPosition, visibilityAlpha);
    void IRenderContext.DrawHealingCrossParticles(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, float visibilityAlpha) => DrawHealingCrossParticles(player, renderPosition, cameraPosition, visibilityAlpha);
    void IRenderContext.DrawHealthBar(PlayerEntity player, Vector2 cameraPosition, Color fillColor, Color backColor, Color borderColor) => DrawHealthBar(player, cameraPosition, fillColor, backColor, borderColor);
    void IRenderContext.DrawHeavyDashDodgePopup(PlayerEntity player, Vector2 cameraPosition) => DrawHeavyDashDodgePopup(player, cameraPosition);
    void IRenderContext.DrawLastToDieSniperAsceticGhosting(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, float visibilityAlpha, Game1.PlayerBodySpriteSelection bodySelection) => DrawLastToDieSniperAsceticGhosting(player, renderPosition, cameraPosition, visibilityAlpha, bodySelection);
    void IRenderContext.DrawOverheadChatMessage(PlayerEntity player, Vector2 cameraPosition) => DrawOverheadChatMessage(player, cameraPosition);
    void IRenderContext.DrawPracticeCombatDummyDps(PlayerEntity player, Vector2 cameraPosition) => DrawPracticeCombatDummyDps(player, cameraPosition);
    void IRenderContext.DrawShieldBar(PlayerEntity player, Vector2 cameraPosition, float fillFraction, Color fillColor, Color backColor, Color borderColor) => DrawShieldBar(player, cameraPosition, fillFraction, fillColor, backColor, borderColor);
    void IRenderContext.DrawSpriteFrameFlatColor(LoadedSpriteFrame frame, Vector2 position, Color tint, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects) => DrawSpriteFrameFlatColor(frame, position, tint, rotation, origin, scale, effects);
    void IRenderContext.DrawSpriteFrameMultiplyColor(LoadedSpriteFrame frame, Vector2 position, Color tint, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects) => DrawSpriteFrameMultiplyColor(frame, position, tint, rotation, origin, scale, effects);
    void IRenderContext.DrawSpriteFrameOutline(LoadedSpriteFrame frame, Vector2 position, Color outlineTint, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, IReadOnlyList<Vector2>? outlineOffsets) => DrawSpriteFrameOutline(frame, position, outlineTint, rotation, origin, scale, effects, outlineOffsets);
    void IRenderContext.DrawSpriteFrameShadow(LoadedSpriteFrame frame, Vector2 position, Color tint, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects) => DrawSpriteFrameShadow(frame, position, tint, rotation, origin, scale, effects);
    void IRenderContext.DrawSpriteFrameWithOptionalShadow(LoadedSpriteFrame frame, Vector2 position, Color tint, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects) => DrawSpriteFrameWithOptionalShadow(frame, position, tint, rotation, origin, scale, effects);
    void IRenderContext.DrawSpriteFrame(LoadedSpriteFrame frame, Vector2 position, Color tint, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects) => DrawSpriteFrame(frame, position, tint, rotation, origin, scale, effects);
    void IRenderContext.DrawTopDownPlayerShadow(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, Color tint) => DrawTopDownPlayerShadow(player, renderPosition, cameraPosition, tint);
    void IRenderContext.DrawWriteBubble(PlayerEntity player, Vector2 cameraPosition) => DrawWriteBubble(player, cameraPosition);
    IEnumerable<PlayerEntity> IRenderContext.EnumerateRemotePlayersForView() => EnumerateRemotePlayersForView();
    float IRenderContext.GetBackstabReplacementDirectionDegrees(PlayerEntity player) => GetBackstabReplacementDirectionDegrees(player);
    int IRenderContext.GetCivviePogoTrickPresentationFrameIndex(PlayerEntity player, int frameCount) => GetCivviePogoTrickPresentationFrameIndex(player, frameCount);
    float IRenderContext.GetCorpseFadeAlpha(int ticksRemaining) => GetCorpseFadeAlpha(ticksRemaining);
    Vector2 IRenderContext.GetPlayerAnchoredScreenPosition(Vector2 renderPosition, Vector2 cameraPosition, float anchoredWorldX, float anchoredWorldY) => GetPlayerAnchoredScreenPosition(renderPosition, cameraPosition, anchoredWorldX, anchoredWorldY);
    float IRenderContext.GetPlayerBodyAnimationLength(PlayerEntity player, float? horizontalSourceStepSpeed) => GetPlayerBodyAnimationLength(player, horizontalSourceStepSpeed);
    Game1.PlayerBodySpriteSelection IRenderContext.GetPlayerBodySpriteSelection(PlayerEntity player) => GetPlayerBodySpriteSelection(player);
    int IRenderContext.GetPlayerBuffBannerDeployDurationTicks(PlayerEntity player) => GetPlayerBuffBannerDeployDurationTicks(player);
    int IRenderContext.GetPlayerBuffBannerDeployTicksRemaining(PlayerEntity player) => GetPlayerBuffBannerDeployTicksRemaining(player);
    int IRenderContext.GetPlayerCivviePogoCrunchTicksRemaining(PlayerEntity player) => GetPlayerCivviePogoCrunchTicksRemaining(player);
    int IRenderContext.GetPlayerCivvieUmbrellaChargeTicks(PlayerEntity player) => GetPlayerCivvieUmbrellaChargeTicks(player);
    Color IRenderContext.GetPlayerColor(PlayerEntity player, Color baseColor) => GetPlayerColor(player, baseColor);
    float IRenderContext.GetPlayerIntelRechargeTicks(PlayerEntity player) => GetPlayerIntelRechargeTicks(player);
    bool IRenderContext.GetPlayerIsCivviePogoActive(PlayerEntity player) => GetPlayerIsCivviePogoActive(player);
    bool IRenderContext.GetPlayerIsCivviePogoTrickActive(PlayerEntity player) => GetPlayerIsCivviePogoTrickActive(player);
    bool IRenderContext.GetPlayerIsBuffBannerActive(PlayerEntity player) => GetPlayerIsBuffBannerActive(player);
    bool IRenderContext.GetPlayerIsBuffBannerDeploying(PlayerEntity player) => GetPlayerIsBuffBannerDeploying(player);
    bool IRenderContext.GetPlayerIsCivvieUmbrellaActive(PlayerEntity player) => GetPlayerIsCivvieUmbrellaActive(player);
    bool IRenderContext.GetPlayerIsExperimentalGhostDashing(PlayerEntity player) => GetPlayerIsExperimentalGhostDashing(player);
    bool IRenderContext.GetPlayerIsHeavyEating(PlayerEntity player) => GetPlayerIsHeavyEating(player);
    bool IRenderContext.GetPlayerIsSpyCloaked(PlayerEntity player) => GetPlayerIsSpyCloaked(player);
    bool IRenderContext.GetPlayerIsSpyVisibleToEnemies(PlayerEntity player) => GetPlayerIsSpyVisibleToEnemies(player);
    PlayerEntity IRenderContext.GetPlayerPredictedPresentationState(PlayerEntity player) => GetPlayerPredictedPresentationState(player);
    Rectangle IRenderContext.GetPlayerScreenBounds(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition) => GetPlayerScreenBounds(player, renderPosition, cameraPosition);
    Vector2 IRenderContext.GetPlayerSpriteOrigin(Vector2 renderPosition) => GetPlayerSpriteOrigin(renderPosition);
    Vector2 IRenderContext.GetPlayerSpriteScreenOrigin(Vector2 renderPosition, Vector2 cameraPosition) => GetPlayerSpriteScreenOrigin(renderPosition, cameraPosition);
    float IRenderContext.GetTorsoReplacementBobOffset(PlayerEntity player) => GetTorsoReplacementBobOffset(player);
    bool IRenderContext.HasFreshPlayerRenderHistory(PlayerEntity player) => HasFreshPlayerRenderHistory(player);
    bool IRenderContext.IsBackstabReplacementRenderActive(PlayerEntity player) => IsBackstabReplacementRenderActive(player);
    bool IRenderContext.IsKritzUberWeaponOnlyVisual(PlayerEntity player) => IsKritzUberWeaponOnlyVisual(player);
    bool IRenderContext.IsLastToDieSessionActive => IsLastToDieSessionActive;
    void IRenderContext.NoteRemainsSortCeiling(int sortKey) => NoteRemainsSortCeiling(sortKey);
    void IRenderContext.PlayPredictedGibSound(float worldX, float worldY) => PlayPredictedGibSound(worldX, worldY);
    bool IRenderContext.PlayerSkinIncludesCloakedWeapon(PlayerEntity player, Game1.PlayerBodySpriteSelection body) => PlayerSkinIncludesCloakedWeapon(player, body);
    void IRenderContext.RecordHeavyDashFrameState(PlayerEntity player, string spriteName, int frameIndex, Vector2 renderPosition, Vector2 origin, Vector2 scale, float bodyYOffset, Color tint, bool drawIntelOverlay) => RecordHeavyDashFrameState(player, spriteName, frameIndex, renderPosition, origin, scale, bodyYOffset, tint, drawIntelOverlay);
    void IRenderContext.RecordLastVisibleEnemySpyFrame(PlayerEntity player, string spriteName, int frameIndex, Vector2 renderPosition, Vector2 origin, Vector2 scale, float bodyYOffset, Color tint, bool drawIntelOverlay) => RecordLastVisibleEnemySpyFrame(player, spriteName, frameIndex, renderPosition, origin, scale, bodyYOffset, tint, drawIntelOverlay);
    void IRenderContext.ResolveCorpseDeathKnockback(float corpseX, float corpseY, bool facingLeft, int attackerPlayerId, float damageEventX, float damageEventY, out float knockbackX, out float knockbackY) => ResolveCorpseDeathKnockback(corpseX, corpseY, facingLeft, attackerPlayerId, damageEventX, damageEventY, out knockbackX, out knockbackY);
    bool IRenderContext.ShouldForceLastToDieSpecialEnemyHealthBar(PlayerEntity player) => ShouldForceLastToDieSpecialEnemyHealthBar(player);
    bool IRenderContext.ShouldHideLastToDieWeaponForPlayer(PlayerEntity player) => ShouldHideLastToDieWeaponForPlayer(player);
    void IRenderContext.SpawnDynamicRagdoll(int deadBodyId, int sourcePlayerId, PlayerClass classId, PlayerTeam team, DeadBodyAnimationKind animationKind, string gameplayClassId, bool facingLeft, float x, float y, float knockbackX, float knockbackY) => SpawnDynamicRagdoll(deadBodyId, sourcePlayerId, classId, team, animationKind, gameplayClassId, facingLeft, x, y, knockbackX, knockbackY);
    void IRenderContext.TryDrawAdditionalHealthBar(PlayerEntity player, Vector2 cameraPosition, float visibilityAlpha) => TryDrawAdditionalHealthBar(player, cameraPosition, visibilityAlpha);
    void IRenderContext.TryDrawCivvieUmbrellaShieldBar(PlayerEntity player, Vector2 cameraPosition, float visibilityAlpha) => TryDrawCivvieUmbrellaShieldBar(player, cameraPosition, visibilityAlpha);
    bool IRenderContext.TryDrawClientPluginDeadBody(Vector2 cameraTopLeft, ClientDeadBodyRenderState deadBody) => TryDrawClientPluginDeadBody(cameraTopLeft, deadBody);
    bool IRenderContext.TryDrawCorpseAcidDissolve(int corpseId, string gameplayClassId, PlayerClass classId, PlayerTeam team, DeadBodyAnimationKind animationKind, float worldX, float worldY, float corpseHeight, bool facingLeft, float rotationDegrees, int ticksRemaining, Vector2 cameraPosition) => TryDrawCorpseAcidDissolve(corpseId, gameplayClassId, classId, team, animationKind, worldX, worldY, corpseHeight, facingLeft, rotationDegrees, ticksRemaining, cameraPosition);
    bool IRenderContext.TryDrawDynamicRagdoll(int deadBodyId, int sourcePlayerId, PlayerClass classId, PlayerTeam team, DeadBodyAnimationKind animationKind, float x, float y, float width, float height, bool facingLeft, string gameplayClassId, int ticksRemaining, Vector2 cameraPosition) => TryDrawDynamicRagdoll(deadBodyId, sourcePlayerId, classId, team, animationKind, x, y, width, height, facingLeft, gameplayClassId, ticksRemaining, cameraPosition);
    bool IRenderContext.TryDrawPlayerSprite(PlayerEntity player, Vector2 cameraPosition, Color tint, Game1.PlayerBodySpriteSelection bodySelection) => TryDrawPlayerSprite(player, cameraPosition, tint, bodySelection);
    bool IRenderContext.TryDrawPlayerSpriteAtPosition(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, Color tint, Game1.PlayerBodySpriteSelection bodySelection, bool drawIntelOverlay, bool drawTopDownShadow) => TryDrawPlayerSpriteAtPosition(player, renderPosition, cameraPosition, tint, bodySelection, drawIntelOverlay, drawTopDownShadow);
    bool IRenderContext.TryDrawSprite(string spriteName, int frameIndex, float worldX, float worldY, Vector2 cameraPosition, Color tint, float rotation) => TryDrawSprite(spriteName, frameIndex, worldX, worldY, cameraPosition, tint, rotation);
    bool IRenderContext.TryDrawSprite(string spriteName, int frameIndex, float worldX, float worldY, Vector2 cameraPosition, Color tint, float rotation, float scale) => TryDrawSprite(spriteName, frameIndex, worldX, worldY, cameraPosition, tint, rotation, scale);
    bool IRenderContext.TryDrawSprite(string spriteName, int frameIndex, float worldX, float worldY, Vector2 cameraPosition, Color tint, float rotation, Vector2 scale) => TryDrawSprite(spriteName, frameIndex, worldX, worldY, cameraPosition, tint, rotation, scale);
    bool IRenderContext.TryDrawWeaponSprite(PlayerEntity player, Vector2 cameraPosition, Color tint, float visibilityAlpha, Game1.PlayerBodySpriteSelection bodySelection) => TryDrawWeaponSprite(player, cameraPosition, tint, visibilityAlpha, bodySelection);
    bool IRenderContext.TryDrawWeaponSpriteAtPosition(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, Color tint, float visibilityAlpha, Game1.PlayerBodySpriteSelection bodySelection) => TryDrawWeaponSpriteAtPosition(player, renderPosition, cameraPosition, tint, visibilityAlpha, bodySelection);
    bool IRenderContext.TryDrawWeaponSpriteBackdrop(PlayerEntity player, Vector2 cameraPosition, Color tint, float visibilityAlpha, Game1.PlayerBodySpriteSelection bodySelection) => TryDrawWeaponSpriteBackdrop(player, cameraPosition, tint, visibilityAlpha, bodySelection);
    bool IRenderContext.TryGetLastToDieHaxtonSpriteName(PlayerEntity player, out string spriteName) => TryGetLastToDieHaxtonSpriteName(player, out spriteName);
    bool IRenderContext.TryGetLocalPlayerAimDirection(PlayerEntity player, out float aimDirectionDegrees) => TryGetLocalPlayerAimDirection(player, out aimDirectionDegrees);
    bool IRenderContext.TryGetPlayerSkinBody(PlayerEntity player, out Game1.PlayerBodySpriteSelection selection) => TryGetPlayerSkinBody(player, out selection);
    bool IRenderContext.ShouldForceMapBotHealthBar(PlayerEntity player) => ShouldForceMapBotHealthBar(player);
    bool IRenderContext.ShouldPresentExperimentalEngineerEssenceExtractor(PlayerEntity player) => ShouldPresentExperimentalEngineerEssenceExtractor(player);
    bool IRenderContext.ShouldPresentExperimentalMedicKritzHealNeedles(PlayerEntity player) => ShouldPresentExperimentalMedicKritzHealNeedles(player);
    PlayerClass IRenderContext.GetRenderWeaponPresentationClassId(PlayerEntity player) => GetRenderWeaponPresentationClassId(player);
    float IRenderContext.GetPlayerAnimationSourceStepSpeed(float speedPerSecond) => GetPlayerAnimationSourceStepSpeed(speedPerSecond);
    float IRenderContext.WrapAnimationImage(float animationImage, float length) => WrapAnimationImage(animationImage, length);
    ClientDeadBodyAnimationKind IRenderContext.ToClientDeadBodyAnimationKind(DeadBodyAnimationKind animationKind) => ToClientDeadBodyAnimationKind(animationKind);
    string? IRenderContext.GetDeadBodySpriteName(string gameplayClassId, PlayerClass classId, PlayerTeam team, DeadBodyAnimationKind animationKind) => GetDeadBodySpriteName(gameplayClassId, classId, team, animationKind);
    Vector2 IRenderContext.RoundToSourcePixels(Vector2 value) => RoundToSourcePixels(value);

    string IDiscordContext.ResolveDiscordApplicationId() => ResolveDiscordApplicationId();
    Game1.GameplayHudCanvas IPluginContext.CreateGameplayHudCanvas(Microsoft.Xna.Framework.Vector2 cameraTopLeft)
        => new(this, cameraTopLeft);

    Game1.ClientPluginStateView IPluginContext.CreateClientPluginStateView()
        => new(this);

    bool IAudioContext._audioAvailable { get => _audioAvailable; set => _audioAvailable = value; }
    OpenGarrison.Core.SimulationConfig IAudioContext._config { get => _config; set => _config = value; }
    List<OpenGarrison.Client.Game1.ExplosionVisual> IAudioContext._explosions { get => _explosions; }
    ref Microsoft.Xna.Framework.Audio.SoundEffect IAudioContext._faucetMusic => ref _faucetMusic;
    ref Microsoft.Xna.Framework.Audio.SoundEffectInstance IAudioContext._faucetMusicInstance => ref _faucetMusicInstance;
    ref bool IAudioContext._faucetMusicLoadAttempted => ref _faucetMusicLoadAttempted;
    float IAudioContext._gameplayPresentationDeltaSeconds { get => _gameplayPresentationDeltaSeconds; set => _gameplayPresentationDeltaSeconds = value; }
    ref Microsoft.Xna.Framework.Audio.SoundEffect IAudioContext._ingameCombatMusic => ref _ingameCombatMusic;
    ref Microsoft.Xna.Framework.Audio.SoundEffectInstance IAudioContext._ingameCombatMusicInstance => ref _ingameCombatMusicInstance;
    ref Microsoft.Xna.Framework.Audio.SoundEffect IAudioContext._ingameMusic => ref _ingameMusic;
    ref Microsoft.Xna.Framework.Audio.SoundEffectInstance IAudioContext._ingameMusicInstance => ref _ingameMusicInstance;
    ref bool IAudioContext._ingameMusicLoadAttempted => ref _ingameMusicLoadAttempted;
    int IAudioContext._ingameMusicVolumePercent { get => _ingameMusicVolumePercent; set => _ingameMusicVolumePercent = value; }
    bool IAudioContext._killCamEnabled { get => _killCamEnabled; set => _killCamEnabled = value; }
    Microsoft.Xna.Framework.Audio.SoundEffect IAudioContext._lastToDieGameOverSound { get => _lastToDieGameOverSound; set => _lastToDieGameOverSound = value; }
    Microsoft.Xna.Framework.Audio.SoundEffectInstance IAudioContext._lastToDieGameOverSoundInstance { get => _lastToDieGameOverSoundInstance; set => _lastToDieGameOverSoundInstance = value; }
    bool IAudioContext._lastToDieGameOverSoundLoadAttempted { get => _lastToDieGameOverSoundLoadAttempted; set => _lastToDieGameOverSoundLoadAttempted = value; }
    ref Microsoft.Xna.Framework.Audio.SoundEffect IAudioContext._lastToDieIngameMusic => ref _lastToDieIngameMusic;
    ref Microsoft.Xna.Framework.Audio.SoundEffectInstance IAudioContext._lastToDieIngameMusicInstance => ref _lastToDieIngameMusicInstance;
    ref bool IAudioContext._lastToDieIngameMusicLoadAttempted => ref _lastToDieIngameMusicLoadAttempted;
    ref Microsoft.Xna.Framework.Audio.SoundEffect IAudioContext._lastToDieMenuMusic => ref _lastToDieMenuMusic;
    ref Microsoft.Xna.Framework.Audio.SoundEffectInstance IAudioContext._lastToDieMenuMusicInstance => ref _lastToDieMenuMusicInstance;
    ref bool IAudioContext._lastToDieMenuMusicLoadAttempted => ref _lastToDieMenuMusicLoadAttempted;
    float IAudioContext._localBuffBannerReadyCueEchoSuppressionSeconds { get => _localBuffBannerReadyCueEchoSuppressionSeconds; set => _localBuffBannerReadyCueEchoSuppressionSeconds = value; }
    OpenGarrison.Client.BuffBannerReadyCueTracker IAudioContext._localBuffBannerReadyCueTracker { get => _localBuffBannerReadyCueTracker; }
    ref Microsoft.Xna.Framework.Audio.SoundEffectInstance IAudioContext._localChaingunSoundInstance => ref _localChaingunSoundInstance;
    ref Microsoft.Xna.Framework.Audio.SoundEffectInstance IAudioContext._localFlamethrowerSoundInstance => ref _localFlamethrowerSoundInstance;
    ref Microsoft.Xna.Framework.Audio.SoundEffectInstance IAudioContext._localMedigunSoundInstance => ref _localMedigunSoundInstance;
    ref Microsoft.Xna.Framework.Audio.SoundEffectInstance IAudioContext._localUberIdleSoundInstance => ref _localUberIdleSoundInstance;
    bool IAudioContext._mainMenuOpen { get => _mainMenuOpen; set => _mainMenuOpen = value; }
    ref Microsoft.Xna.Framework.Audio.SoundEffect IAudioContext._menuMusic => ref _menuMusic;
    ref Microsoft.Xna.Framework.Audio.SoundEffectInstance IAudioContext._menuMusicInstance => ref _menuMusicInstance;
    ref bool IAudioContext._menuMusicLoadAttempted => ref _menuMusicLoadAttempted;
    OpenGarrison.Client.NetworkGameClient IAudioContext._networkClient { get => _networkClient; }
    List<OpenGarrison.Client.Game1.PendingBrowserSoundEvent> IAudioContext._pendingBrowserSoundEvents { get => _pendingBrowserSoundEvents; }
    Nullable<OpenGarrison.Core.PrimaryWeaponKind> IAudioContext._pendingImmediateRapidFireWeaponKind { get => _pendingImmediateRapidFireWeaponKind; set => _pendingImmediateRapidFireWeaponKind = value; }
    List<OpenGarrison.Core.WorldSoundEvent> IAudioContext._pendingNetworkSoundEvents { get => _pendingNetworkSoundEvents; }
    int IAudioContext._previousLocalDemoknightChargeTicks { get => _previousLocalDemoknightChargeTicks; set => _previousLocalDemoknightChargeTicks = value; }
    HashSet<ulong> IAudioContext._processedKillFeedEventIds { get => _processedKillFeedEventIds; }
    Queue<ulong> IAudioContext._processedKillFeedEventOrder { get => _processedKillFeedEventOrder; }
    HashSet<ulong> IAudioContext._processedNetworkSoundEventIds { get => _processedNetworkSoundEventIds; }
    Queue<ulong> IAudioContext._processedNetworkSoundEventOrder { get => _processedNetworkSoundEventOrder; }
    OpenGarrison.Client.GameMakerRuntimeAssetCache IAudioContext._runtimeAssets { get => _runtimeAssets; set => _runtimeAssets = value; }
    bool IAudioContext._wasDeathCamActive { get => _wasDeathCamActive; set => _wasDeathCamActive = value; }
    bool IAudioContext._wasMatchEnded { get => _wasMatchEnded; set => _wasMatchEnded = value; }
    OpenGarrison.Core.SimulationWorld IAudioContext._world { get => _world; set => _world = value; }
    bool IAudioContext.IsAnyLastToDieSessionActive { get => IsAnyLastToDieSessionActive; }
    bool IAudioContext.IsServerLauncherMode { get => IsServerLauncherMode; }
    void IAudioContext.AddConsoleLine(string line) { AddConsoleLine(line); }
    void IAudioContext.AdvanceLocalWeaponSoundFocus() { AdvanceLocalWeaponSoundFocus(); }
    void IAudioContext.AdvanceLowPriorityWorldSoundThrottle() { AdvanceLowPriorityWorldSoundThrottle(); }
    void IAudioContext.AdvanceRecentGibSoundEvents() { AdvanceRecentGibSoundEvents(); }
    void IAudioContext.AdvanceRecentProjectileSoundEvents() { AdvanceRecentProjectileSoundEvents(); }
    bool IAudioContext.AllowsIngameMusic() => AllowsIngameMusic();
    bool IAudioContext.AllowsMenuMusic() => AllowsMenuMusic();
    void IAudioContext.ApplyAudioVolumeState() { ApplyAudioVolumeState(); }
    void IAudioContext.BeginExplosionSoundDeduplicationFrame() { BeginExplosionSoundDeduplicationFrame(); }
    void IAudioContext.DisableAudio(string reason, System.Exception ex) { DisableAudio(reason, ex); }
    void IAudioContext.EnqueuePendingBrowserSoundEvent(string soundName, float x, float y) { EnqueuePendingBrowserSoundEvent(soundName, x, y); }
    void IAudioContext.ForgetPresentedExplosionVisualForSoundEvent(OpenGarrison.Core.WorldSoundEvent soundEvent) { ForgetPresentedExplosionVisualForSoundEvent(soundEvent); }
    OpenGarrison.Core.PlayerEntity IAudioContext.GetImmediatePrimaryPresentationPlayer() => GetImmediatePrimaryPresentationPlayer();
    ValueTuple<float, float> IAudioContext.GetLoopedWorldSoundMix(string soundName, float worldX, float worldY, bool isLocalSource) => GetLoopedWorldSoundMix(soundName, worldX, worldY, isLocalSource);
    int IAudioContext.GetPlayerBuffBannerChargeDamage(OpenGarrison.Core.PlayerEntity player) => GetPlayerBuffBannerChargeDamage(player);
    int IAudioContext.GetPlayerBuffBannerMaxChargeDamage(OpenGarrison.Core.PlayerEntity player) => GetPlayerBuffBannerMaxChargeDamage(player);
    bool IAudioContext.GetPlayerIsHeavyEating(OpenGarrison.Core.PlayerEntity player) => GetPlayerIsHeavyEating(player);
    double IAudioContext.GetProjectileRenderTimeSeconds() => GetProjectileRenderTimeSeconds();
    int IAudioContext.GetResolvedLocalPlayerId() => GetResolvedLocalPlayerId();
    float IAudioContext.GetSoundEffectsVolumeScale() => GetSoundEffectsVolumeScale();
    Microsoft.Xna.Framework.Vector2 IAudioContext.GetWorldSoundListenerPosition() => GetWorldSoundListenerPosition();
    ValueTuple<float, float> IAudioContext.GetWorldSoundMix(OpenGarrison.Core.WorldSoundEvent soundEvent) => GetWorldSoundMix(soundEvent);
    ValueTuple<float, float> IAudioContext.GetWorldSoundMix(float worldX, float worldY) => GetWorldSoundMix(worldX, worldY);
    bool IAudioContext.HasPlayedExplosionSoundThisFrame(float x, float y) => HasPlayedExplosionSoundThisFrame(x, y);
    bool IAudioContext.HasPresentedExplosionVisualForSoundEvent(OpenGarrison.Core.WorldSoundEvent soundEvent) => HasPresentedExplosionVisualForSoundEvent(soundEvent);
    bool IAudioContext.HasPresentedExplosionVisualThisFrame(float x, float y) => HasPresentedExplosionVisualThisFrame(x, y);
    bool IAudioContext.HasRecentPredictedExplosionVisual(float x, float y) => HasRecentPredictedExplosionVisual(x, y);
    bool IAudioContext.IsHostedLastToDieMenuMusicPhase() => IsHostedLastToDieMenuMusicPhase();
    bool IAudioContext.IsLastToDieDeathFocusPresentationActive() => IsLastToDieDeathFocusPresentationActive();
    bool IAudioContext.IsLastToDieFailurePresentationActive() => IsLastToDieFailurePresentationActive();
    bool IAudioContext.IsLastToDieMenuActive() => IsLastToDieMenuActive();
    bool IAudioContext.IsLocalPlayerSoundSource(int sourcePlayerId) => IsLocalPlayerSoundSource(sourcePlayerId);
    void IAudioContext.NotifyClientPluginsWorldSound(OpenGarrison.Core.WorldSoundEvent soundEvent) { NotifyClientPluginsWorldSound(soundEvent); }
    void IAudioContext.RecordPlayedExplosionSoundThisFrame(float x, float y) { RecordPlayedExplosionSoundThisFrame(x, y); }
    void IAudioContext.RememberPlayedGibSound(OpenGarrison.Core.WorldSoundEvent soundEvent) { RememberPlayedGibSound(soundEvent); }
    void IAudioContext.RememberPlayedLowPriorityWorldSound(string resolvedSoundName, OpenGarrison.Core.WorldSoundEvent soundEvent) { RememberPlayedLowPriorityWorldSound(resolvedSoundName, soundEvent); }
    void IAudioContext.RememberPlayedProjectileSound(string resolvedSoundName, OpenGarrison.Core.WorldSoundEvent soundEvent) { RememberPlayedProjectileSound(resolvedSoundName, soundEvent); }
    void IAudioContext.RememberPresentedExplosionVisualForSoundEvent(OpenGarrison.Core.WorldSoundEvent soundEvent) { RememberPresentedExplosionVisualForSoundEvent(soundEvent); }
    bool IAudioContext.ShouldPresentAuthoritativeExplosionSound(OpenGarrison.Core.WorldSoundEvent soundEvent) => ShouldPresentAuthoritativeExplosionSound(soundEvent);
    bool IAudioContext.ShouldSuppressManagedRapidFireSound(OpenGarrison.Core.WorldSoundEvent soundEvent) => ShouldSuppressManagedRapidFireSound(soundEvent);
    bool IAudioContext.ShouldSuppressPredictedGibSoundEcho(OpenGarrison.Core.WorldSoundEvent soundEvent) => ShouldSuppressPredictedGibSoundEcho(soundEvent);
    bool IAudioContext.ShouldSuppressPredictedProjectileSoundEcho(string resolvedSoundName, OpenGarrison.Core.WorldSoundEvent soundEvent) => ShouldSuppressPredictedProjectileSoundEcho(resolvedSoundName, soundEvent);
    bool IAudioContext.ShouldThrottleLowPriorityWorldSound(string resolvedSoundName, OpenGarrison.Core.WorldSoundEvent soundEvent) => ShouldThrottleLowPriorityWorldSound(resolvedSoundName, soundEvent);
    void IAudioContext.StopFaucetMusic() { StopFaucetMusic(); }
    void IAudioContext.StopIngameMusic() { StopIngameMusic(); }
    void IAudioContext.StopLastToDieIngameMusic() { StopLastToDieIngameMusic(); }
    void IAudioContext.StopLastToDieMenuMusic() { StopLastToDieMenuMusic(); }
    void IAudioContext.StopMenuMusic() { StopMenuMusic(); }
    void IAudioContext.TriggerLocalConfirmedWeaponFireFeedback(string soundName, OpenGarrison.Core.WorldSoundEvent soundEvent) { TriggerLocalConfirmedWeaponFireFeedback(soundName, soundEvent); }
    bool IAudioContext.TryCreateExplosionVisual(OpenGarrison.Core.WorldSoundEvent soundEvent, out OpenGarrison.Client.Game1.ExplosionVisual explosion) => TryCreateExplosionVisual(soundEvent, out explosion);
    bool IAudioContext.TryPlaySound(Microsoft.Xna.Framework.Audio.SoundEffect sound, float volume, float pitch, float pan) => TryPlaySound(sound, volume, pitch, pan);
    string IInputContext._chatInput { get => _chatInput; set => _chatInput = value; }
    int IInputContext._chatInputCursorIndex { get => _chatInputCursorIndex; set => _chatInputCursorIndex = value; }
    int IInputContext._chatInputSelectionStart { get => _chatInputSelectionStart; set => _chatInputSelectionStart = value; }
    bool IInputContext._chatOpen { get => _chatOpen; set => _chatOpen = value; }
    string IInputContext._connectHostBuffer { get => _connectHostBuffer; set => _connectHostBuffer = value; }
    int IInputContext._connectHostCursorIndex { get => _connectHostCursorIndex; set => _connectHostCursorIndex = value; }
    int IInputContext._connectHostSelectionStart { get => _connectHostSelectionStart; set => _connectHostSelectionStart = value; }
    string IInputContext._connectPortBuffer { get => _connectPortBuffer; set => _connectPortBuffer = value; }
    int IInputContext._connectPortCursorIndex { get => _connectPortCursorIndex; set => _connectPortCursorIndex = value; }
    int IInputContext._connectPortSelectionStart { get => _connectPortSelectionStart; set => _connectPortSelectionStart = value; }
    string IInputContext._consoleInput { get => _consoleInput; set => _consoleInput = value; }
    int IInputContext._consoleInputCursorIndex { get => _consoleInputCursorIndex; set => _consoleInputCursorIndex = value; }
    int IInputContext._consoleInputSelectionStart { get => _consoleInputSelectionStart; set => _consoleInputSelectionStart = value; }
    bool IInputContext._consoleOpen { get => _consoleOpen; set => _consoleOpen = value; }
    bool IInputContext._editingConnectPort { get => _editingConnectPort; set => _editingConnectPort = value; }
    bool IInputContext._editingFriendCode { get => _editingFriendCode; set => _editingFriendCode = value; }
    bool IInputContext._editingFriendMessage { get => _editingFriendMessage; set => _editingFriendMessage = value; }
    bool IInputContext._editingFriendNickname { get => _editingFriendNickname; set => _editingFriendNickname = value; }
    bool IInputContext._editingPlayerName { get => _editingPlayerName; set => _editingPlayerName = value; }
    int IInputContext._friendCodeCursorIndex { get => _friendCodeCursorIndex; set => _friendCodeCursorIndex = value; }
    string IInputContext._friendCodeInputBuffer { get => _friendCodeInputBuffer; set => _friendCodeInputBuffer = value; }
    int IInputContext._friendCodeSelectionStart { get => _friendCodeSelectionStart; set => _friendCodeSelectionStart = value; }
    int IInputContext._friendMessageCursorIndex { get => _friendMessageCursorIndex; set => _friendMessageCursorIndex = value; }
    string IInputContext._friendMessageInputBuffer { get => _friendMessageInputBuffer; set => _friendMessageInputBuffer = value; }
    int IInputContext._friendMessageSelectionStart { get => _friendMessageSelectionStart; set => _friendMessageSelectionStart = value; }
    int IInputContext._friendNicknameCursorIndex { get => _friendNicknameCursorIndex; set => _friendNicknameCursorIndex = value; }
    string IInputContext._friendNicknameInputBuffer { get => _friendNicknameInputBuffer; set => _friendNicknameInputBuffer = value; }
    int IInputContext._friendNicknameSelectionStart { get => _friendNicknameSelectionStart; set => _friendNicknameSelectionStart = value; }
    bool IInputContext._friendsMenuOpen { get => _friendsMenuOpen; set => _friendsMenuOpen = value; }
    bool IInputContext._hostSetupOpen { get => _hostSetupOpen; set => _hostSetupOpen = value; }
    bool IInputContext._mainMenuOpen { get => _mainMenuOpen; set => _mainMenuOpen = value; }
    bool IInputContext._manualConnectOpen { get => _manualConnectOpen; set => _manualConnectOpen = value; }
    bool IInputContext._namePromptOpen { get => _namePromptOpen; set => _namePromptOpen = value; }
    OpenGarrison.Client.NetworkGameClient IInputContext._networkClient { get => _networkClient; }
    bool IInputContext._optionsMenuOpen { get => _optionsMenuOpen; set => _optionsMenuOpen = value; }
    string IInputContext._passwordEditBuffer { get => _passwordEditBuffer; set => _passwordEditBuffer = value; }
    int IInputContext._passwordEditCursorIndex { get => _passwordEditCursorIndex; set => _passwordEditCursorIndex = value; }
    int IInputContext._passwordEditSelectionStart { get => _passwordEditSelectionStart; set => _passwordEditSelectionStart = value; }
    string IInputContext._passwordPromptMessage { get => _passwordPromptMessage; set => _passwordPromptMessage = value; }
    bool IInputContext._passwordPromptOpen { get => _passwordPromptOpen; set => _passwordPromptOpen = value; }
    bool IInputContext._playerCardEditorOpen { get => _playerCardEditorOpen; set => _playerCardEditorOpen = value; }
    string IInputContext._playerNameEditBuffer { get => _playerNameEditBuffer; set => _playerNameEditBuffer = value; }
    int IInputContext._playerNameEditCursorIndex { get => _playerNameEditCursorIndex; set => _playerNameEditCursorIndex = value; }
    int IInputContext._playerNameEditSelectionStart { get => _playerNameEditSelectionStart; set => _playerNameEditSelectionStart = value; }
    bool IInputContext._practiceSetupOpen { get => _practiceSetupOpen; set => _practiceSetupOpen = value; }
    bool IInputContext.IsWindowInputActive { get => IsWindowInputActive; }
    InputManager IInputContext.Input => _inputManager;
    SessionManager IInputContext.Session => _sessionManager;
    HostingManager IInputContext.Hosting => _hostingManager;
    void IInputContext.CommitPlayerNamePrompt() { CommitPlayerNamePrompt(); }
    ValueTuple<string, int, int> IInputContext.DeleteTextSelectionOrBackspace(string text, int cursorIndex, int selectionStart) => DeleteTextSelectionOrBackspace(text, cursorIndex, selectionStart);
    void IInputContext.ExecuteConsoleCommand() { ExecuteConsoleCommand(); }
    void IInputContext.ExecuteConsoleCommand(string commandText) { ExecuteConsoleCommand(commandText); }
    bool IInputContext.HandleGarrisonBuilderTextInput(char character) => HandleGarrisonBuilderTextInput(character);
    bool IInputContext.HandleManagedRoomText(char character) => HandleManagedRoomText(character);
    ValueTuple<string, int, int> IInputContext.InsertTextCharacterAtCursor(string text, char character, int cursorIndex, int selectionStart, int maxLength) => InsertTextCharacterAtCursor(text, character, cursorIndex, selectionStart, maxLength);
    void IInputContext.SaveFriendNicknameFromInput() { SaveFriendNicknameFromInput(); }
    void IInputContext.SetLocalPlayerNameFromSettings(string playerName) { SetLocalPlayerNameFromSettings(playerName); }
    void IInputContext.SubmitChatMessage() { SubmitChatMessage(); }
    void IInputContext.TryConnectFromMenu() { TryConnectFromMenu(); }
    bool IInputContext.TryHandleAccountDialogTextInput(char character) => TryHandleAccountDialogTextInput(character);
    bool IInputContext.TryHandlePlayerCardBioTextInput(char character) => TryHandlePlayerCardBioTextInput(character);
    bool IInputContext.TryHandlePracticeMapBrowserTextInput(char character) => TryHandlePracticeMapBrowserTextInput(character);
    void IInputContext.TrySendFriendRequestFromInput() { TrySendFriendRequestFromInput(); }
    bool IInputContext.TrySendSelectedFriendDirectMessageFromInput() => TrySendSelectedFriendDirectMessageFromInput();
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
    string ISessionContext._autoBalanceNoticeText { get => _autoBalanceNoticeText; set => _autoBalanceNoticeText = value; }
    int ISessionContext._autoBalanceNoticeTicks { get => _autoBalanceNoticeTicks; set => _autoBalanceNoticeTicks = value; }
    bool ISessionContext._classSelectOpen { get => _classSelectOpen; set => _classSelectOpen = value; }
    OpenGarrison.Core.SimulationConfig ISessionContext._config { get => _config; set => _config = value; }
    string ISessionContext._connectHostBuffer { get => _connectHostBuffer; set => _connectHostBuffer = value; }
    string ISessionContext._connectPortBuffer { get => _connectPortBuffer; set => _connectPortBuffer = value; }
    bool ISessionContext._consoleOpen { get => _consoleOpen; set => _consoleOpen = value; }
    bool ISessionContext._controlsMenuOpen { get => _controlsMenuOpen; set => _controlsMenuOpen = value; }
    bool ISessionContext._creditsOpen { get => _creditsOpen; set => _creditsOpen = value; }
    bool ISessionContext._editingConnectHost { get => _editingConnectHost; set => _editingConnectHost = value; }
    bool ISessionContext._editingConnectPort { get => _editingConnectPort; set => _editingConnectPort = value; }
    bool ISessionContext._editingPlayerName { get => _editingPlayerName; set => _editingPlayerName = value; }
    bool ISessionContext._inGameMenuOpen { get => _inGameMenuOpen; set => _inGameMenuOpen = value; }
    bool ISessionContext._lastToDieConnectionPresentationPending { get => _lastToDieConnectionPresentationPending; set => _lastToDieConnectionPresentationPending = value; }
    bool ISessionContext._lastToDieRoomCodeJoinOpen { get => _lastToDieRoomCodeJoinOpen; set => _lastToDieRoomCodeJoinOpen = value; }
    OpenGarrison.Client.Game1.LobbyBrowserEntry ISessionContext._lobbyBrowserDetailsEntry { get => _lobbyBrowserDetailsEntry; set => _lobbyBrowserDetailsEntry = value; }
    string ISessionContext._lobbyBrowserDetailsStatus { get => _lobbyBrowserDetailsStatus; set => _lobbyBrowserDetailsStatus = value; }
    List<OpenGarrison.Client.Game1.LobbyBrowserEntry> ISessionContext._lobbyBrowserEntries { get => _lobbyBrowserEntries; }
    int ISessionContext._lobbyBrowserHoverIndex { get => _lobbyBrowserHoverIndex; set => _lobbyBrowserHoverIndex = value; }
    OpenGarrison.Client.Game1.LobbyBrowserMode ISessionContext._lobbyBrowserMode { get => _lobbyBrowserMode; set => _lobbyBrowserMode = value; }
    bool ISessionContext._lobbyBrowserOpen { get => _lobbyBrowserOpen; set => _lobbyBrowserOpen = value; }
    OpenGarrison.Client.Game1.LobbyBrowserPage ISessionContext._lobbyBrowserPage { get => _lobbyBrowserPage; set => _lobbyBrowserPage = value; }
    Task<List<OpenGarrison.Client.Game1.LobbyRegistryServerEntry>> ISessionContext._lobbyBrowserRegistryRequestTask { get => _lobbyBrowserRegistryRequestTask; set => _lobbyBrowserRegistryRequestTask = value; }
    int ISessionContext._lobbyBrowserScrollOffset { get => _lobbyBrowserScrollOffset; set => _lobbyBrowserScrollOffset = value; }
    int ISessionContext._lobbyBrowserSelectedIndex { get => _lobbyBrowserSelectedIndex; set => _lobbyBrowserSelectedIndex = value; }
    OpenGarrison.Client.Game1.LobbyBrowserSource ISessionContext._lobbyBrowserSource { get => _lobbyBrowserSource; set => _lobbyBrowserSource = value; }
    int ISessionContext._manualConnectControllerIndex { get => _manualConnectControllerIndex; set => _manualConnectControllerIndex = value; }
    bool ISessionContext._manualConnectOpen { get => _manualConnectOpen; set => _manualConnectOpen = value; }
    string ISessionContext._menuStatusMessage { get => _menuStatusMessage; set => _menuStatusMessage = value; }
    OpenGarrison.Client.NetworkGameClient ISessionContext._networkClient { get => _networkClient; }
    bool ISessionContext._optionsMenuOpen { get => _optionsMenuOpen; set => _optionsMenuOpen = value; }
    string ISessionContext._passwordEditBuffer { get => _passwordEditBuffer; set => _passwordEditBuffer = value; }
    string ISessionContext._passwordPromptMessage { get => _passwordPromptMessage; set => _passwordPromptMessage = value; }
    bool ISessionContext._passwordPromptOpen { get => _passwordPromptOpen; set => _passwordPromptOpen = value; }
    Nullable<OpenGarrison.Client.Game1.ControllerControlsMenuBinding> ISessionContext._pendingControllerControlsBinding { get => _pendingControllerControlsBinding; set => _pendingControllerControlsBinding = value; }
    Nullable<OpenGarrison.Client.Game1.ControlsMenuBinding> ISessionContext._pendingControlsBinding { get => _pendingControlsBinding; set => _pendingControlsBinding = value; }
    bool ISessionContext._pluginOptionsMenuOpen { get => _pluginOptionsMenuOpen; set => _pluginOptionsMenuOpen = value; }
    string ISessionContext._recentConnectHost { get => _recentConnectHost; set => _recentConnectHost = value; }
    int ISessionContext._recentConnectPort { get => _recentConnectPort; set => _recentConnectPort = value; }
    bool ISessionContext._teamSelectOpen { get => _teamSelectOpen; set => _teamSelectOpen = value; }
    OpenGarrison.Client.Game1.LobbyBrowserEntry ISessionContext.AddLobbyBrowserEntry(string displayName, OpenGarrison.Client.NetworkEndpoint endpoint, bool isPrivate, bool isLobbyEntry) => AddLobbyBrowserEntry(displayName, endpoint, isPrivate, isLobbyEntry);
    void ISessionContext.BeginFriendCodeJoin(string friendCode) { BeginFriendCodeJoin(friendCode); }
    void ISessionContext.BeginRelayRoomJoin(string roomCode) { BeginRelayRoomJoin(roomCode); }
    void ISessionContext.CancelFriendCodeJoin() { CancelFriendCodeJoin(); }
    void ISessionContext.CancelLegacyGg2LobbyRequest() { CancelLegacyGg2LobbyRequest(); }
    void ISessionContext.ClearLobbyBrowserDetails() { ClearLobbyBrowserDetails(); }
    void ISessionContext.CloseLobbyBrowserLobbyClient() { CloseLobbyBrowserLobbyClient(); }
    void ISessionContext.EnsureLobbyBrowserClient() { EnsureLobbyBrowserClient(); }
    void ISessionContext.InitializeConnectHostCursor() { InitializeConnectHostCursor(); }
    void ISessionContext.InitializeConnectPortCursor() { InitializeConnectPortCursor(); }
    void ISessionContext.InitializePasswordEditCursor() { InitializePasswordEditCursor(); }
    bool ISessionContext.IsWatchOnlySession() => IsWatchOnlySession();
    void ISessionContext.OpenLobbyBrowserDetails(OpenGarrison.Client.Game1.LobbyBrowserEntry entry) { OpenLobbyBrowserDetails(entry); }
    void ISessionContext.ResetChatInputState(bool requireOpenKeyRelease = false) { ResetChatInputState(requireOpenKeyRelease); }
    void ISessionContext.ResetSpectatorTracking(bool enableTracking) { ResetSpectatorTracking(enableTracking); }
    void ISessionContext.StartLegacyGg2LobbyRequest() { StartLegacyGg2LobbyRequest(); }
    void ISessionContext.StartLobbyBrowserRegistryRequest() { StartLobbyBrowserRegistryRequest(); }
    bool ISessionContext.TryConnectLegacyGg2Server(string host, int port, bool addConsoleFeedback) => TryConnectLegacyGg2Server(host, port, addConsoleFeedback);
    bool ISessionContext.TryConnectToServer(OpenGarrison.Client.NetworkEndpoint endpoint, bool addConsoleFeedback, OpenGarrison.Client.Game1.OnlineConnectionIntent intent) => TryConnectToServer(endpoint, addConsoleFeedback, intent);
    bool ISessionContext.TryConnectToServer(OpenGarrison.Client.NetworkEndpoint endpoint, bool addConsoleFeedback) => TryConnectToServer(endpoint, addConsoleFeedback);
    bool ISessionContext.TryConnectToServer(string host, int port, bool addConsoleFeedback) => TryConnectToServer(host, port, addConsoleFeedback);
    bool IHostingContext._controlsMenuOpen { get => _controlsMenuOpen; set => _controlsMenuOpen = value; }
    OpenGarrison.Client.HostedServerConsoleState IHostingContext._hostedServerConsole { get => _hostedServerConsole; }
    OpenGarrison.Client.HostedServerRuntimeController IHostingContext._hostedServerRuntime { get => _hostedServerRuntime; }
    bool IHostingContext._hostLobbyAnnounceEnabled { get => _hostLobbyAnnounceEnabled; set => _hostLobbyAnnounceEnabled = value; }
    List<OpenGarrison.Core.OpenGarrisonMapRotationEntry> IHostingContext._hostMapEntries { get => _hostMapEntries; set => _hostMapEntries = value; }
    int IHostingContext._hostSetupContentScrollOffset { get => _hostSetupContentScrollOffset; set => _hostSetupContentScrollOffset = value; }
    OpenGarrison.Client.Game1.HostSetupEditField IHostingContext._hostSetupEditField { get => _hostSetupEditField; set => _hostSetupEditField = value; }
    OpenGarrison.Client.HostSetupScreen IHostingContext._hostSetupScreen { get => _hostSetupScreen; set => _hostSetupScreen = value; }
    OpenGarrison.Client.Game1.HostSetupFormState IHostingContext._hostSetupState { get => _hostSetupState; }
    OpenGarrison.Client.Game1.HostSetupTab IHostingContext._hostSetupTab { get => _hostSetupTab; set => _hostSetupTab = value; }
    bool IHostingContext._hostUsePlaylistFile { get => _hostUsePlaylistFile; set => _hostUsePlaylistFile = value; }
    bool IHostingContext._mainMenuOpen { get => _mainMenuOpen; set => _mainMenuOpen = value; }
    string IHostingContext._menuStatusMessage { get => _menuStatusMessage; set => _menuStatusMessage = value; }
    bool IHostingContext._optionsMenuOpen { get => _optionsMenuOpen; set => _optionsMenuOpen = value; }
    Nullable<OpenGarrison.Client.Game1.ControllerControlsMenuBinding> IHostingContext._pendingControllerControlsBinding { get => _pendingControllerControlsBinding; set => _pendingControllerControlsBinding = value; }
    Nullable<OpenGarrison.Client.Game1.ControlsMenuBinding> IHostingContext._pendingControlsBinding { get => _pendingControlsBinding; set => _pendingControlsBinding = value; }
    int IHostingContext._pendingHostedConnectPort { get => _pendingHostedConnectPort; set => _pendingHostedConnectPort = value; }
    bool IHostingContext._pluginOptionsMenuOpen { get => _pluginOptionsMenuOpen; set => _pluginOptionsMenuOpen = value; }
    Microsoft.Xna.Framework.Input.MouseState IHostingContext._previousMouse { get => _previousMouse; set => _previousMouse = value; }
    bool IHostingContext._startupSplashOpen { get => _startupSplashOpen; set => _startupSplashOpen = value; }
    bool IHostingContext.IsHostedServerRunning { get => IsHostedServerRunning; }
    bool IHostingContext.IsServerLauncherMode { get => IsServerLauncherMode; }
    OpenGarrison.Client.ScrollbarDragController IHostingContext.ScrollbarDrag { get => ScrollbarDrag; }
    int IHostingContext.ViewportHeight { get => ViewportHeight; }
    int IHostingContext.ViewportWidth { get => ViewportWidth; }
    void IHostingContext.AppendHostedServerLog(string source, string message) { AppendHostedServerLog(source, message); }
    string IHostingContext.BuildHostedServerExitMessage() => BuildHostedServerExitMessage();
    void IHostingContext.CancelPendingHostedLocalConnect(string statusMessage = default) { CancelPendingHostedLocalConnect(statusMessage); }
    void IHostingContext.ClampHostSetupContentScrollOffset(OpenGarrison.Client.HostSetupMenuLayout layout) { ClampHostSetupContentScrollOffset(layout); }
    void IHostingContext.ClearHostedServerConsoleView() { ClearHostedServerConsoleView(); }
    void IHostingContext.CloseAllHostSetupMapPreviews() { CloseAllHostSetupMapPreviews(); }
    void IHostingContext.CloseCreditsMenu() { CloseCreditsMenu(); }
    void IHostingContext.CloseHostSetupMenu(bool clearStatus = false) { CloseHostSetupMenu(clearStatus); }
    void IHostingContext.CloseManualConnectMenu(bool clearStatus) { CloseManualConnectMenu(clearStatus); }
    void IHostingContext.ExecuteHostedServerCommandFromUi(string command) { ExecuteHostedServerCommandFromUi(command); }
    void IHostingContext.Exit() { Exit(); }
    Microsoft.Xna.Framework.Rectangle IHostingContext.GetHostSetupScrolledContentBounds(Microsoft.Xna.Framework.Rectangle bounds) => GetHostSetupScrolledContentBounds(bounds);
    void IHostingContext.HandleHostSetupFieldBackspace() { HandleHostSetupFieldBackspace(); }
    void IHostingContext.HandleHostSetupFieldCharacterInput(char character) { HandleHostSetupFieldCharacterInput(character); }
    void IHostingContext.HandleHostSetupOptionsMenu(Microsoft.Xna.Framework.Input.MouseState mouse, bool clickPressed) { HandleHostSetupOptionsMenu(mouse, clickPressed); }
    void IHostingContext.InitializeHostedServerConsole(bool reset) { InitializeHostedServerConsole(reset); }
    void IHostingContext.InitializeHostSetupFieldCursor(OpenGarrison.Client.Game1.HostSetupEditField field) { InitializeHostSetupFieldCursor(field); }
    bool IHostingContext.IsTextFieldDoubleClick(OpenGarrison.Client.Game1.TextFieldClickTarget target) => IsTextFieldDoubleClick(target);
    void IHostingContext.OpenHostSetupMenu() { OpenHostSetupMenu(); }
    void IHostingContext.PrepareHostedServerConsoleLaunchState(string serverName, int port, int maxPlayers, int timeLimitMinutes, int capLimit, int respawnSeconds, bool lobbyAnnounce, bool autoBalance, bool secondaryAbilitiesEnabled, bool resetConsole, string launcherLogMessage = default) { PrepareHostedServerConsoleLaunchState(serverName, port, maxPlayers, timeLimitMinutes, capLimit, respawnSeconds, lobbyAnnounce, autoBalance, secondaryAbilitiesEnabled, resetConsole, launcherLogMessage); }
    void IHostingContext.PrepareHostedServerLaunchUi(bool closeHostSetup, bool disconnectNetworkClient) { PrepareHostedServerLaunchUi(closeHostSetup, disconnectNetworkClient); }
    void IHostingContext.ResetTextFieldClickTarget() { ResetTextFieldClickTarget(); }
    void IHostingContext.SelectAllTextInActiveField(OpenGarrison.Client.Game1.TextFieldClickTarget clickTarget) { SelectAllTextInActiveField(clickTarget); }
    void IHostingContext.StopHostedServer() { StopHostedServer(); }
    bool IHostingContext.TryHandleScrollbarRangeDrag(Microsoft.Xna.Framework.Input.MouseState mouse, Microsoft.Xna.Framework.Input.MouseState previousMouse, System.Object owner, Microsoft.Xna.Framework.Rectangle trackBounds, ref int scrollOffset, int maxScrollOffset, int viewportSize, int contentSize, int minThumbHeight = 24) => TryHandleScrollbarRangeDrag(mouse, previousMouse, owner, trackBounds, ref scrollOffset, maxScrollOffset, viewportSize, contentSize, minThumbHeight);
    void IHostingContext.TryHostFromSetup(bool runInTerminal = false) { TryHostFromSetup(runInTerminal); }
    bool IHostingContext.TryResumeHostedServerSession(bool loadExistingLog, Nullable<int> expectedProcessId = default) => TryResumeHostedServerSession(loadExistingLog, expectedProcessId);
    bool IHostingContext.TryStartHostedServerBackground(string serverName, int port, int maxPlayers, string password, string rconPassword, int timeLimitMinutes, int capLimit, int respawnSeconds, bool lobbyAnnounce, bool autoBalance, bool secondaryAbilitiesEnabled, string requestedMap, string mapRotationFile, bool resetConsole, out string error) => TryStartHostedServerBackground(serverName, port, maxPlayers, password, rconPassword, timeLimitMinutes, capLimit, respawnSeconds, lobbyAnnounce, autoBalance, secondaryAbilitiesEnabled, requestedMap, mapRotationFile, resetConsole, out error);
    bool IHostingContext.TryStartHostedServerInTerminal(string serverName, int port, int maxPlayers, string password, string rconPassword, int timeLimitMinutes, int capLimit, int respawnSeconds, bool lobbyAnnounce, bool autoBalance, bool secondaryAbilitiesEnabled, string requestedMap, string mapRotationFile, out string error) => TryStartHostedServerInTerminal(serverName, port, maxPlayers, password, rconPassword, timeLimitMinutes, capLimit, respawnSeconds, lobbyAnnounce, autoBalance, secondaryAbilitiesEnabled, requestedMap, mapRotationFile, out error);
    void IHostingContext.UpdateHostSetupMapsMenu(Microsoft.Xna.Framework.Input.MouseState mouse, bool clickPressed, bool rightClickPressed, OpenGarrison.Client.HostSetupMapsMenuLayout layout) { UpdateHostSetupMapsMenu(mouse, clickPressed, rightClickPressed, layout); }
    int IPluginContext._bloodRenderMode { get => _bloodRenderMode; set => _bloodRenderMode = value; }
    OpenGarrison.Client.ClientPluginHost IPluginContext._clientPluginHost { get => _clientPluginHost; set => _clientPluginHost = value; }
    ref ValueTuple<bool, bool, OpenGarrison.Client.Plugins.ClientPluginTeam, float, float, float> IPluginContext._clientPluginPreviousBlueIntelState => ref _clientPluginPreviousBlueIntelState;
    Dictionary<OpenGarrison.Core.PlayerTeam, ValueTuple<int, int, bool>> IPluginContext._clientPluginPreviousGeneratorStates { get => _clientPluginPreviousGeneratorStates; }
    int IPluginContext._clientPluginPreviousKillFeedCount { get => _clientPluginPreviousKillFeedCount; set => _clientPluginPreviousKillFeedCount = value; }
    bool IPluginContext._clientPluginPreviousLocalAlive { get => _clientPluginPreviousLocalAlive; set => _clientPluginPreviousLocalAlive = value; }
    int IPluginContext._clientPluginPreviousLocalAmmo { get => _clientPluginPreviousLocalAmmo; set => _clientPluginPreviousLocalAmmo = value; }
    bool IPluginContext._clientPluginPreviousLocalBurning { get => _clientPluginPreviousLocalBurning; set => _clientPluginPreviousLocalBurning = value; }
    bool IPluginContext._clientPluginPreviousLocalCarryingIntel { get => _clientPluginPreviousLocalCarryingIntel; set => _clientPluginPreviousLocalCarryingIntel = value; }
    int IPluginContext._clientPluginPreviousLocalPrimaryCooldownTicks { get => _clientPluginPreviousLocalPrimaryCooldownTicks; set => _clientPluginPreviousLocalPrimaryCooldownTicks = value; }
    OpenGarrison.Client.Plugins.ClientRoundPhase IPluginContext._clientPluginPreviousMatchPhase { get => _clientPluginPreviousMatchPhase; set => _clientPluginPreviousMatchPhase = value; }
    Dictionary<int, ValueTuple<OpenGarrison.Client.Plugins.ClientPluginTeam, OpenGarrison.Client.Plugins.ClientPluginTeam, float, bool>> IPluginContext._clientPluginPreviousObjectiveStates { get => _clientPluginPreviousObjectiveStates; }
    ref ValueTuple<bool, bool, OpenGarrison.Client.Plugins.ClientPluginTeam, float, float, float> IPluginContext._clientPluginPreviousRedIntelState => ref _clientPluginPreviousRedIntelState;
    OpenGarrison.Client.Game1.ClientPluginStateView IPluginContext._clientPluginStateView { get => _clientPluginStateView; set => _clientPluginStateView = value; }
    Microsoft.Xna.Framework.Vector2 IPluginContext._gameplayCameraTopLeft { get => _gameplayCameraTopLeft; set => _gameplayCameraTopLeft = value; }
    bool IPluginContext._hasGameplayCameraTopLeft { get => _hasGameplayCameraTopLeft; set => _hasGameplayCameraTopLeft = value; }
    Nullable<int> IPluginContext._localPlayerSnapshotEntityId { get => _localPlayerSnapshotEntityId; set => _localPlayerSnapshotEntityId = value; }
    bool IPluginContext._mainMenuOpen { get => _mainMenuOpen; set => _mainMenuOpen = value; }
    OpenGarrison.Client.NetworkGameClient IPluginContext._networkClient { get => _networkClient; }
    List<OpenGarrison.Protocol.SnapshotDamageEvent> IPluginContext._pendingNetworkDamageEvents { get => _pendingNetworkDamageEvents; }
    HashSet<ulong> IPluginContext._processedNetworkDamageEventIds { get => _processedNetworkDamageEventIds; }
    Queue<ulong> IPluginContext._processedNetworkDamageEventOrder { get => _processedNetworkDamageEventOrder; }
    OpenGarrison.Client.GameMakerRuntimeAssetCache IPluginContext._runtimeAssets { get => _runtimeAssets; set => _runtimeAssets = value; }
    bool IPluginContext._startupSplashOpen { get => _startupSplashOpen; set => _startupSplashOpen = value; }
    OpenGarrison.Core.SimulationWorld IPluginContext._world { get => _world; set => _world = value; }
    bool IPluginContext.AreBloodVisualsEnabled { get => AreBloodVisualsEnabled; }
    int IPluginContext.ViewportHeight { get => ViewportHeight; }
    int IPluginContext.ViewportWidth { get => ViewportWidth; }
    void IPluginContext.AddConsoleLine(string line) { AddConsoleLine(line); }
    OpenGarrison.Client.ClientPluginHost IPluginContext.CreateClientPluginHost(string pluginsDirectory, string pluginConfigRoot, string pluginStatePath) => CreateClientPluginHost(pluginsDirectory, pluginConfigRoot, pluginStatePath);
    IEnumerable<OpenGarrison.Core.PlayerEntity> IPluginContext.EnumerateRemotePlayersForView() => EnumerateRemotePlayersForView();
    Nullable<int> IPluginContext.GetClientPluginLocalPlayerId() => GetClientPluginLocalPlayerId();
    Microsoft.Xna.Framework.Input.MouseState IPluginContext.GetFrameMouseState() => GetFrameMouseState();
    Microsoft.Xna.Framework.Vector2 IPluginContext.GetRenderPosition(int entityId, float x, float y, bool allowInterpolation = true) => GetRenderPosition(entityId, x, y, allowInterpolation);
    Microsoft.Xna.Framework.Vector2 IPluginContext.GetRenderPosition(OpenGarrison.Core.PlayerEntity player, bool allowInterpolation = true) => GetRenderPosition(player, allowInterpolation);
    Microsoft.Xna.Framework.Vector2 IPluginContext.GetUntrackedCameraTopLeft(int viewportWidth, int viewportHeight, int mouseX, int mouseY) => GetUntrackedCameraTopLeft(viewportWidth, viewportHeight, mouseX, mouseY);
    bool IPluginContext.IsClientPerformanceDiagnosticsEnabled() => IsClientPerformanceDiagnosticsEnabled();
    bool IPluginContext.IsLocalSpectatorPresentationActive() => IsLocalSpectatorPresentationActive();
    void IPluginContext.ObserveCivvieUmbrellaShieldBlockDamageEvent(OpenGarrison.Core.WorldDamageEvent damageEvent) { ObserveCivvieUmbrellaShieldBlockDamageEvent(damageEvent); }
    void IPluginContext.ObserveCivvieUmbrellaShieldBlockDamageEvent(OpenGarrison.Protocol.SnapshotDamageEvent damageEvent) { ObserveCivvieUmbrellaShieldBlockDamageEvent(damageEvent); }
    void IPluginContext.ObserveDynamicMusicDamageEvent(OpenGarrison.Core.WorldDamageEvent damageEvent) { ObserveDynamicMusicDamageEvent(damageEvent); }
    void IPluginContext.ObserveDynamicMusicDamageEvent(OpenGarrison.Protocol.SnapshotDamageEvent damageEvent) { ObserveDynamicMusicDamageEvent(damageEvent); }
    void IPluginContext.ObserveEvasionMissDamageEvent(OpenGarrison.Core.WorldDamageEvent damageEvent) { ObserveEvasionMissDamageEvent(damageEvent); }
    void IPluginContext.ObserveEvasionMissDamageEvent(OpenGarrison.Protocol.SnapshotDamageEvent damageEvent) { ObserveEvasionMissDamageEvent(damageEvent); }
    void IPluginContext.ObserveHeavyDashDodgeDamageEvent(OpenGarrison.Core.WorldDamageEvent damageEvent) { ObserveHeavyDashDodgeDamageEvent(damageEvent); }
    void IPluginContext.ObserveHeavyDashDodgeDamageEvent(OpenGarrison.Protocol.SnapshotDamageEvent damageEvent) { ObserveHeavyDashDodgeDamageEvent(damageEvent); }
    void IPluginContext.QueueImmediateNetworkDeathPresentation(OpenGarrison.Protocol.SnapshotMessage resolvedSnapshot, OpenGarrison.Protocol.SnapshotDamageEvent damageEvent) { QueueImmediateNetworkDeathPresentation(resolvedSnapshot, damageEvent); }
    void IPluginContext.RecordClientPerformanceMetric(OpenGarrison.Client.Game1.ClientPerformanceMetric metric, double milliseconds) { RecordClientPerformanceMetric(metric, milliseconds); }
    void IPluginContext.RegisterLastToDieLocalDamageDealt(int amount) { RegisterLastToDieLocalDamageDealt(amount); }
    void IPluginContext.ResetClientPluginGameplayEventState() { ResetClientPluginGameplayEventState(); }
    void IPluginContext.TriggerLocalHudDamageVignette(int damageAmount) { TriggerLocalHudDamageVignette(damageAmount); }
    void IPluginContext.TriggerLocalHudPortraitDamageFeedback(int damageAmount) { TriggerLocalHudPortraitDamageFeedback(damageAmount); }
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
    void IGameplayContext.OnWindowTextInput(System.Object sender, Microsoft.Xna.Framework.TextInputEventArgs e) { OnWindowTextInput(sender, e); }
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
    float IHudContext._clientUpdateElapsedSeconds { get => _clientUpdateElapsedSeconds; set => _clientUpdateElapsedSeconds = value; }
    OpenGarrison.Core.SimulationConfig IHudContext._config { get => _config; set => _config = value; }
    int IHudContext._cursorSizePercent { get => _cursorSizePercent; set => _cursorSizePercent = value; }
    OpenGarrison.ClientShared.CustomBubbleDocument IHudContext._customBubbleDocument { get => _customBubbleDocument; }
    bool IHudContext._damageVignetteEnabled { get => _damageVignetteEnabled; set => _damageVignetteEnabled = value; }
    float IHudContext._damageVignetteFlashIntensity { get => _damageVignetteFlashIntensity; set => _damageVignetteFlashIntensity = value; }
    float IHudContext._damageVignetteIntensity { get => _damageVignetteIntensity; set => _damageVignetteIntensity = value; }
    int IHudContext._damageVignetteIntensityPercent { get => _damageVignetteIntensityPercent; set => _damageVignetteIntensityPercent = value; }
    bool IHudContext._healerRadarEnabled { get => _healerRadarEnabled; set => _healerRadarEnabled = value; }
    bool IHudContext._hudEditorOpen { get => _hudEditorOpen; set => _hudEditorOpen = value; }
    OpenGarrison.Client.HudLayoutProfile IHudContext._hudLayoutProfile { get => _hudLayoutProfile; set => _hudLayoutProfile = value; }
    bool IHudContext._hudShowOnlyActiveWeapon { get => _hudShowOnlyActiveWeapon; set => _hudShowOnlyActiveWeapon = value; }
    Microsoft.Xna.Framework.Point IHudContext._lastKnownMousePosition { get => _lastKnownMousePosition; set => _lastKnownMousePosition = value; }
    OpenGarrison.Core.PlayerInputSnapshot IHudContext._latestPredictedLocalInput { get => _latestPredictedLocalInput; set => _latestPredictedLocalInput = value; }
    OpenGarrison.Core.LowHealthColorMode IHudContext._lowHealthColorMode { get => _lowHealthColorMode; set => _lowHealthColorMode = value; }
    OpenGarrison.Client.NetworkGameClient IHudContext._networkClient { get => _networkClient; }
    Microsoft.Xna.Framework.Graphics.Texture2D IHudContext._pixel { get => _pixel; set => _pixel = value; }
    bool IHudContext._portraitRumbleEnabled { get => _portraitRumbleEnabled; set => _portraitRumbleEnabled = value; }
    float IHudContext._portraitRumbleIntensity { get => _portraitRumbleIntensity; set => _portraitRumbleIntensity = value; }
    float IHudContext._portraitRumbleRemainingSeconds { get => _portraitRumbleRemainingSeconds; set => _portraitRumbleRemainingSeconds = value; }
    int IHudContext._portraitRumbleSeed { get => _portraitRumbleSeed; set => _portraitRumbleSeed = value; }
    Microsoft.Xna.Framework.Input.MouseState IHudContext._previousMouse { get => _previousMouse; set => _previousMouse = value; }
    bool IHudContext._showHealerEnabled { get => _showHealerEnabled; set => _showHealerEnabled = value; }
    bool IHudContext._showHealingEnabled { get => _showHealingEnabled; set => _showHealingEnabled = value; }
    bool IHudContext._showPersistentSelfNameEnabled { get => _showPersistentSelfNameEnabled; set => _showPersistentSelfNameEnabled = value; }
    bool IHudContext._showPlayerNamesEnabled { get => _showPlayerNamesEnabled; set => _showPlayerNamesEnabled = value; }
    Microsoft.Xna.Framework.Graphics.SpriteBatch IHudContext._spriteBatch { get => _spriteBatch; set => _spriteBatch = value; }
    OpenGarrison.Core.SimulationWorld IHudContext._world { get => _world; set => _world = value; }
    int IHudContext.ViewportHeight { get => ViewportHeight; }
    int IHudContext.ViewportWidth { get => ViewportWidth; }
    GameplayManager IHudContext.Gameplay => _gameplayManager;
    HudManager IHudContext.Hud => _hudManager;
    GameplayWeaponRenderController IHudContext.GameplayWeaponRenderer => _gameplayWeaponRenderController;
    void IHudContext.AddHudEditorDummyAbilitySlot() { AddHudEditorDummyAbilitySlot(); }
    Microsoft.Xna.Framework.Color IHudContext.ApplyCurrentHudElementOpacity(Microsoft.Xna.Framework.Color color) => ApplyCurrentHudElementOpacity(color);
    void IHudContext.CloseCustomBubbleEditor() { CloseCustomBubbleEditor(); }
    void IHudContext.CloseHudEditor() { CloseHudEditor(); }
    Microsoft.Xna.Framework.Graphics.Texture2D IHudContext.CreateCustomBubbleShellTexture(byte[] pixels) => CreateCustomBubbleShellTexture(pixels);
    void IHudContext.DrawBitmapFontText(string text, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color color, float scale, float rotation) { DrawBitmapFontText(text, position, color, scale, rotation); }
    void IHudContext.DrawBitmapFontText(string text, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color color, float scale = 1f) { DrawBitmapFontText(text, position, color, scale); }
    void IHudContext.DrawBitmapFontTextCentered(string text, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color color, float scale, float rotation) { DrawBitmapFontTextCentered(text, position, color, scale, rotation); }
    void IHudContext.DrawBitmapFontTextCentered(string text, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color color, float scale) { DrawBitmapFontTextCentered(text, position, color, scale); }
    void IHudContext.DrawBitmapFontTextRightAligned(string text, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color color, float scale) { DrawBitmapFontTextRightAligned(text, position, color, scale); }
    void IHudContext.DrawHudTextCentered(string text, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color color, float scale) { DrawHudTextCentered(text, position, color, scale); }
    void IHudContext.DrawHudTextLeftAligned(string text, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color color, float scale) { DrawHudTextLeftAligned(text, position, color, scale); }
    void IHudContext.DrawHudTextRightAligned(string text, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color color, float scale) { DrawHudTextRightAligned(text, position, color, scale); }
    void IHudContext.DrawLoadedSpriteFrame(OpenGarrison.Client.LoadedSpriteFrame frame, Microsoft.Xna.Framework.Rectangle destinationRectangle, Microsoft.Xna.Framework.Color tint) { DrawLoadedSpriteFrame(frame, destinationRectangle, tint); }
    void IHudContext.DrawLoadedSpriteFrame(OpenGarrison.Client.LoadedSpriteFrame frame, Microsoft.Xna.Framework.Vector2 position, Nullable<Microsoft.Xna.Framework.Rectangle> sourceRectangle, Microsoft.Xna.Framework.Color tint, float rotation, Microsoft.Xna.Framework.Vector2 origin, Microsoft.Xna.Framework.Vector2 scale, Microsoft.Xna.Framework.Graphics.SpriteEffects effects, float layerDepth) { DrawLoadedSpriteFrame(frame, position, sourceRectangle, tint, rotation, origin, scale, effects, layerDepth); }
    void IHudContext.DrawMenuBitmapFontText(string text, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color color, float scale, float rotation, Microsoft.Xna.Framework.Vector2 rotationCenter) { DrawMenuBitmapFontText(text, position, color, scale, rotation, rotationCenter); }
    void IHudContext.DrawMenuBitmapFontText(string text, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color color, float scale) { DrawMenuBitmapFontText(text, position, color, scale); }
    void IHudContext.DrawMenuButtonCentered(Microsoft.Xna.Framework.Rectangle bounds, string label, bool highlighted, float textScale, bool enabled = true) { DrawMenuButtonCentered(bounds, label, highlighted, textScale, enabled); }
    void IHudContext.DrawRoundedRectangleFillThenBorder(Microsoft.Xna.Framework.Rectangle bounds, Microsoft.Xna.Framework.Color fillColor, Microsoft.Xna.Framework.Color outlineColor, int outlineThickness, int radius) { DrawRoundedRectangleFillThenBorder(bounds, fillColor, outlineColor, outlineThickness, radius); }
    void IHudContext.DrawRoundedRectangleHud(Microsoft.Xna.Framework.Rectangle bounds, Microsoft.Xna.Framework.Color color, int radius) { DrawRoundedRectangleHud(bounds, color, radius); }
    void IHudContext.DrawRoundedRectangleOutline(Microsoft.Xna.Framework.Rectangle bounds, Microsoft.Xna.Framework.Color fillColor, Microsoft.Xna.Framework.Color outlineColor, int outlineThickness, int radius) { DrawRoundedRectangleOutline(bounds, fillColor, outlineColor, outlineThickness, radius); }
    void IHudContext.DrawScreenHealthBar(Microsoft.Xna.Framework.Rectangle rectangle, float value, float maxValue, bool useTeamColors, Nullable<Microsoft.Xna.Framework.Color> fillColor = default, Nullable<Microsoft.Xna.Framework.Color> backColor = default, OpenGarrison.Client.Game1.HudFillDirection fillDirection = OpenGarrison.Client.Game1.HudFillDirection.HorizontalLeftToRight) { DrawScreenHealthBar(rectangle, value, maxValue, useTeamColors, fillColor, backColor, fillDirection); }
    float IHudContext.DrawServerPlayerTitle(OpenGarrison.Protocol.PlayerServerTitleState title, Microsoft.Xna.Framework.Vector2 position, float alpha, float scale, bool includeTrailingSpace = true) => DrawServerPlayerTitle(title, position, alpha, scale, includeTrailingSpace);
    IEnumerable<OpenGarrison.Core.PlayerEntity> IHudContext.EnumerateRenderablePlayers() => EnumerateRenderablePlayers();
    OpenGarrison.Core.PlayerEntity IHudContext.FindPlayerById(int playerId) => FindPlayerById(playerId);
    OpenGarrison.Client.LoadedSpriteFrame IHudContext.GetCustomBubbleShellFrame() => GetCustomBubbleShellFrame();
    Microsoft.Xna.Framework.Input.MouseState IHudContext.GetFrameMouseState() => GetFrameMouseState();
    int IHudContext.GetHudEditorDummyAbilitySlotCount() => GetHudEditorDummyAbilitySlotCount();
    Dictionary<string, OpenGarrison.Client.HudResolvedElement> IHudContext.GetHudEditorElements() => GetHudEditorElements();
    int IHudContext.GetLocalDisplayedMainWeaponCooldownTicks() => GetLocalDisplayedMainWeaponCooldownTicks();
    int IHudContext.GetLocalDisplayedMainWeaponCurrentShells() => GetLocalDisplayedMainWeaponCurrentShells();
    int IHudContext.GetLocalDisplayedMainWeaponMaxShells() => GetLocalDisplayedMainWeaponMaxShells();
    int IHudContext.GetLocalDisplayedMainWeaponReloadTicks() => GetLocalDisplayedMainWeaponReloadTicks();
    OpenGarrison.Core.PrimaryWeaponDefinition IHudContext.GetLocalDisplayedMainWeaponStats() => GetLocalDisplayedMainWeaponStats();
    int IHudContext.GetPlayerBuffBannerMissingChargeDamage(OpenGarrison.Core.PlayerEntity player) => GetPlayerBuffBannerMissingChargeDamage(player);
    int IHudContext.GetPlayerExperimentalGhostDashCooldownTicksRemaining(OpenGarrison.Core.PlayerEntity player) => GetPlayerExperimentalGhostDashCooldownTicksRemaining(player);
    int IHudContext.GetPlayerHeavyEatCooldownDurationTicks(OpenGarrison.Core.PlayerEntity player) => GetPlayerHeavyEatCooldownDurationTicks(player);
    int IHudContext.GetPlayerHeavyEatCooldownTicksRemaining(OpenGarrison.Core.PlayerEntity player) => GetPlayerHeavyEatCooldownTicksRemaining(player);
    Microsoft.Xna.Framework.Rectangle IHudContext.GetPlayerHudScreenBounds(OpenGarrison.Core.PlayerEntity player, Microsoft.Xna.Framework.Vector2 renderPosition, Microsoft.Xna.Framework.Vector2 cameraPosition) => GetPlayerHudScreenBounds(player, renderPosition, cameraPosition);
    bool IHudContext.GetPlayerIsBuffBannerActive(OpenGarrison.Core.PlayerEntity player) => GetPlayerIsBuffBannerActive(player);
    bool IHudContext.GetPlayerIsBuffBannerDeploying(OpenGarrison.Core.PlayerEntity player) => GetPlayerIsBuffBannerDeploying(player);
    bool IHudContext.GetPlayerIsCarryingIntel(OpenGarrison.Core.PlayerEntity player) => GetPlayerIsCarryingIntel(player);
    bool IHudContext.GetPlayerIsCivvieUmbrellaActive(OpenGarrison.Core.PlayerEntity player) => GetPlayerIsCivvieUmbrellaActive(player);
    bool IHudContext.GetPlayerIsExperimentalGhostDashing(OpenGarrison.Core.PlayerEntity player) => GetPlayerIsExperimentalGhostDashing(player);
    bool IHudContext.GetPlayerIsMortarLauncherEquipped(OpenGarrison.Core.PlayerEntity player) => GetPlayerIsMortarLauncherEquipped(player);
    bool IHudContext.GetPlayerIsSniperBowEquipped(OpenGarrison.Core.PlayerEntity player) => GetPlayerIsSniperBowEquipped(player);
    bool IHudContext.GetPlayerIsSniperScoped(OpenGarrison.Core.PlayerEntity player) => GetPlayerIsSniperScoped(player);
    bool IHudContext.GetPlayerIsSpySuperjumpActive(OpenGarrison.Core.PlayerEntity player) => GetPlayerIsSpySuperjumpActive(player);
    int IHudContext.GetPlayerMedicHealDartCooldownTicks(OpenGarrison.Core.PlayerEntity player) => GetPlayerMedicHealDartCooldownTicks(player);
    int IHudContext.GetPlayerMedicNeedleRefillTicks(OpenGarrison.Core.PlayerEntity player) => GetPlayerMedicNeedleRefillTicks(player);
    float IHudContext.GetPlayerMedicUberCharge(OpenGarrison.Core.PlayerEntity player) => GetPlayerMedicUberCharge(player);
    OpenGarrison.Core.MedicUberDeliveryMode IHudContext.GetPlayerMedicUberDeliveryMode(OpenGarrison.Core.PlayerEntity player) => GetPlayerMedicUberDeliveryMode(player);
    float IHudContext.GetPlayerMetal(OpenGarrison.Core.PlayerEntity player) => GetPlayerMetal(player);
    OpenGarrison.Core.PlayerEntity IHudContext.GetPlayerPredictedPresentationState(OpenGarrison.Core.PlayerEntity player) => GetPlayerPredictedPresentationState(player);
    int IHudContext.GetPlayerPyroFlareCooldownTicks(OpenGarrison.Core.PlayerEntity player) => GetPlayerPyroFlareCooldownTicks(player);
    OpenGarrison.Client.PlayerSkinDefinition IHudContext.GetPlayerSkin(OpenGarrison.Core.PlayerEntity player) => GetPlayerSkin(player);
    int IHudContext.GetPlayerSniperBowChargeTicks(OpenGarrison.Core.PlayerEntity player) => GetPlayerSniperBowChargeTicks(player);
    int IHudContext.GetPlayerSniperChargeTicks(OpenGarrison.Core.PlayerEntity player) => GetPlayerSniperChargeTicks(player);
    int IHudContext.GetPlayerSniperRifleDamage(OpenGarrison.Core.PlayerEntity player) => GetPlayerSniperRifleDamage(player);
    int IHudContext.GetPlayerSpySuperjumpAvailableCharges(OpenGarrison.Core.PlayerEntity player) => GetPlayerSpySuperjumpAvailableCharges(player);
    int IHudContext.GetPlayerSpySuperjumpCooldownTicksRemaining(OpenGarrison.Core.PlayerEntity player) => GetPlayerSpySuperjumpCooldownTicksRemaining(player);
    int IHudContext.GetPlayerSpySuperjumpMaximumCharges(OpenGarrison.Core.PlayerEntity player) => GetPlayerSpySuperjumpMaximumCharges(player);
    int IHudContext.GetPlayerStateKey(OpenGarrison.Core.PlayerEntity player) => GetPlayerStateKey(player);
    float IHudContext.GetPlayerVisibilityAlpha(OpenGarrison.Core.PlayerEntity player) => GetPlayerVisibilityAlpha(player);
    Microsoft.Xna.Framework.Vector2 IHudContext.GetRenderPosition(int entityId, float x, float y, bool allowInterpolation = true) => GetRenderPosition(entityId, x, y, allowInterpolation);
    Microsoft.Xna.Framework.Vector2 IHudContext.GetRenderPosition(OpenGarrison.Core.PlayerEntity player, bool allowInterpolation = true) => GetRenderPosition(player, allowInterpolation);
    OpenGarrison.Client.LoadedGameMakerSprite IHudContext.GetResolvedSprite(string spriteName) => GetResolvedSprite(spriteName);
    Microsoft.Xna.Framework.Vector2 IHudContext.GetWorldHudScreenPosition(Microsoft.Xna.Framework.Vector2 worldPosition, Microsoft.Xna.Framework.Vector2 cameraPosition) => GetWorldHudScreenPosition(worldPosition, cameraPosition);
    Microsoft.Xna.Framework.Vector2 IHudContext.GetWorldHudScreenPosition(float worldX, float worldY, Microsoft.Xna.Framework.Vector2 cameraPosition) => GetWorldHudScreenPosition(worldX, worldY, cameraPosition);
    bool IHudContext.HasFreshPlayerRenderHistory(OpenGarrison.Core.PlayerEntity player) => HasFreshPlayerRenderHistory(player);
    bool IHudContext.IsCustomBubbleCanvasPixelInsideShell(int pixelIndex) => IsCustomBubbleCanvasPixelInsideShell(pixelIndex);
    bool IHudContext.IsKeyPressed(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.Keys key) => IsKeyPressed(keyboard, key);
    bool IHudContext.IsLocalSpectatorPresentationActive() => IsLocalSpectatorPresentationActive();
    bool IHudContext.IsPlayerMutedByScoreboardSlot(OpenGarrison.Core.PlayerEntity player) => IsPlayerMutedByScoreboardSlot(player);
    bool IHudContext.IsSpyHiddenFromLocalViewer(int ownerId, OpenGarrison.Core.PlayerTeam ownerTeam, float spyX) => IsSpyHiddenFromLocalViewer(ownerId, ownerTeam, spyX);
    bool IHudContext.IsSpyHiddenFromLocalViewer(OpenGarrison.Core.PlayerEntity player) => IsSpyHiddenFromLocalViewer(player);
    float IHudContext.MeasureBitmapFontHeight(float scale) => MeasureBitmapFontHeight(scale);
    float IHudContext.MeasureBitmapFontWidth(string text, float scale) => MeasureBitmapFontWidth(text, scale);
    float IHudContext.MeasureMenuBitmapFontHeight(float scale) => MeasureMenuBitmapFontHeight(scale);
    float IHudContext.MeasureMenuBitmapFontWidth(string text, float scale) => MeasureMenuBitmapFontWidth(text, scale);
    float IHudContext.MeasureServerPlayerTitle(OpenGarrison.Protocol.PlayerServerTitleState title, float scale, bool includeTrailingSpace = true) => MeasureServerPlayerTitle(title, scale, includeTrailingSpace);
    void IHudContext.ResetHudLayoutElements() { ResetHudLayoutElements(); }
    void IHudContext.SaveCustomBubbleEditorPixels(int slotIndex, byte[] pixels) { SaveCustomBubbleEditorPixels(slotIndex, pixels); }
    void IHudContext.SaveHudLayout() { SaveHudLayout(); }
    void IHudContext.SetHudElementOrigin(string id, Microsoft.Xna.Framework.Vector2 origin) { SetHudElementOrigin(id, origin); }
    void IHudContext.SetHudElementRuntimeDefault(OpenGarrison.Client.HudElementLayout layout) { SetHudElementRuntimeDefault(layout); }
    bool IHudContext.SetHudElementScale(string id, float scale) => SetHudElementScale(id, scale);
    bool IHudContext.SetHudElementVisibility(string id, bool visible) => SetHudElementVisibility(id, visible);
    string IHudContext.TrimBitmapMenuText(string text, float maxWidth, float scale) => TrimBitmapMenuText(text, maxWidth, scale);
    bool IHudContext.TryDrawScreenSprite(string spriteName, int frameIndex, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color tint, Microsoft.Xna.Framework.Vector2 scale, float rotation) => TryDrawScreenSprite(spriteName, frameIndex, position, tint, scale, rotation);
    bool IHudContext.TryDrawScreenSprite(string spriteName, int frameIndex, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color tint, Microsoft.Xna.Framework.Vector2 scale) => TryDrawScreenSprite(spriteName, frameIndex, position, tint, scale);
    bool IHudContext.TryDrawScreenSpritePart(string spriteName, int frameIndex, Microsoft.Xna.Framework.Rectangle sourceRectangle, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color tint, Microsoft.Xna.Framework.Vector2 scale, Microsoft.Xna.Framework.Graphics.SpriteEffects effects, Microsoft.Xna.Framework.Vector2 origin) => TryDrawScreenSpritePart(spriteName, frameIndex, sourceRectangle, position, tint, scale, effects, origin);
    bool IHudContext.TryDrawScreenSpritePart(string spriteName, int frameIndex, Microsoft.Xna.Framework.Rectangle sourceRectangle, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color tint, Microsoft.Xna.Framework.Vector2 scale, Microsoft.Xna.Framework.Graphics.SpriteEffects effects) => TryDrawScreenSpritePart(spriteName, frameIndex, sourceRectangle, position, tint, scale, effects);
    bool IHudContext.TryDrawScreenSpritePart(string spriteName, int frameIndex, Microsoft.Xna.Framework.Rectangle sourceRectangle, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color tint, Microsoft.Xna.Framework.Vector2 scale) => TryDrawScreenSpritePart(spriteName, frameIndex, sourceRectangle, position, tint, scale);
    bool IHudContext.TryEnsureDamageVignetteTexture(float intensity, out Microsoft.Xna.Framework.Graphics.Texture2D texture) => TryEnsureDamageVignetteTexture(intensity, out texture);
    bool IHudContext.TryGetOnlinePlayerServerTitle(byte slot, out OpenGarrison.Protocol.PlayerServerTitleState title) => TryGetOnlinePlayerServerTitle(slot, out title);
    bool IHudContext.TryGetScoreboardPlayerNetworkSlot(OpenGarrison.Core.PlayerEntity player, out byte slot) => TryGetScoreboardPlayerNetworkSlot(player, out slot);
    bool IHudContext.TryResolveHudElement(string id, out OpenGarrison.Client.HudResolvedElement resolved) => TryResolveHudElement(id, out resolved);
    bool IHudContext.TryResolveHudElementEvenIfHidden(string id, out OpenGarrison.Client.HudResolvedElement resolved) => TryResolveHudElementEvenIfHidden(id, out resolved);
    void IHudContext.UpdateHudElementBounds(string id, Microsoft.Xna.Framework.Rectangle bounds) { UpdateHudElementBounds(id, bounds); }
    DiscordRPC.RichPresence IDiscordContext.BuildDiscordRichPresencePayload(System.DateTime startTimestampUtc) => BuildDiscordRichPresencePayload(startTimestampUtc);
    string IDiscordContext.BuildDiscordRichPresenceState() => BuildDiscordRichPresenceState();
}
