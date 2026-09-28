#nullable enable

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Client;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Client;

public interface IRenderContext : IGameplayContext
{
    GameplayManager GameplayManager { get; }
    GameplayPlayerRenderController GameplayPlayerRenderer { get; }
    GameplayDeadBodyRenderController GameplayDeadBodyRenderer { get; }
    GameplayPlayerSpriteRenderController GameplayPlayerSpriteRenderer { get; }
    GameplayPlayerStatusEffectRenderController GameplayPlayerStatusEffectRenderer { get; }
    GameplayWeaponRenderController GameplayWeaponRenderer { get; }

    int _corpseDurationMode { get; set; }
    bool _dynamicRagdollEnabled { get; set; }
    bool _pixelPerfectWeaponRotation { get; set; }
    bool _showHealthBarEnabled { get; set; }
    bool _showShieldBarEnabled { get; set; }
    bool _uberOutlineEnabled { get; set; }
    bool _useLocalWeaponRotation { get; set; }

    Dictionary<int, Game1.RetainedDeadBodyVisual> _trackedDeadBodyVisuals { get; }
    List<Game1.RetainedDeadBodyVisual> _retainedDeadBodies { get; }
    List<int> _staleTrackedDeadBodyIds { get; }
    Dictionary<int, Game1.ImmediateNetworkDeadBodyVisual> _immediateNetworkDeadBodies { get; }
    List<int> _staleImmediateNetworkDeadBodyPlayerIds { get; }
    IReadOnlyList<Game1.CivvieUmbrellaShieldBlockVisual> _civvieUmbrellaShieldBlockVisuals { get; }
    Dictionary<int, Game1.PlayerRenderState> _playerRenderStates { get; }
    Vector2 _predictedLocalPlayerVelocity { get; }

    int AllocateRemainsSortKey();
    Game1.WeaponRenderDefinition ApplyPlayerSkinWeapon(PlayerEntity player, GameplayItemPresentationDefinition presentation, Game1.WeaponRenderDefinition definition, bool standing = false);
    bool CanUseLocalPrediction();
    void DrawAfterburnOverlay(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, float visibilityAlpha);
    void DrawCapturedPointHealingGhosting(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, float visibilityAlpha, Game1.PlayerBodySpriteSelection bodySelection);
    void DrawCenteredHudSprite(string spriteName, int frameIndex, Vector2 visualCenter, Color tint, Vector2 scale);
    void DrawChatBubble(PlayerEntity player, Vector2 cameraPosition);
    void DrawDominationIndicator(PlayerEntity player, Vector2 cameraPosition, float visibilityAlpha);
    void DrawEvasionMissPopup(PlayerEntity player, Vector2 cameraPosition);
    void DrawExperimentalCryoOverlays(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, float visibilityAlpha, Game1.PlayerBodySpriteSelection bodySelection);
    void DrawExperimentalDemoknightChargeBlur(PlayerEntity player, Vector2 cameraPosition, Color spriteTint, float visibilityAlpha, Game1.PlayerBodySpriteSelection bodySelection);
    void DrawExperimentalEssenceExtractorOverlay(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, float visibilityAlpha, Game1.PlayerBodySpriteSelection bodySelection);
    void DrawExperimentalStickyGibBloodOverlay(PlayerEntity player, Vector2 cameraPosition, float visibilityAlpha);
    void DrawHealingCrossParticles(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, float visibilityAlpha);
    void DrawHealthBar(PlayerEntity player, Vector2 cameraPosition, Color fillColor, Color backColor, Color borderColor);
    void DrawHeavyDashDodgePopup(PlayerEntity player, Vector2 cameraPosition);
    void DrawLastToDieSniperAsceticGhosting(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, float visibilityAlpha, Game1.PlayerBodySpriteSelection bodySelection);
    void DrawOverheadChatMessage(PlayerEntity player, Vector2 cameraPosition);
    void DrawPracticeCombatDummyDps(PlayerEntity player, Vector2 cameraPosition);
    void DrawShieldBar(PlayerEntity player, Vector2 cameraPosition, float fillFraction, Color fillColor, Color backColor, Color borderColor);
    void DrawSpriteFrameFlatColor(LoadedSpriteFrame frame, Vector2 position, Color tint, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects = SpriteEffects.None);
    void DrawSpriteFrameMultiplyColor(LoadedSpriteFrame frame, Vector2 position, Color tint, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects = SpriteEffects.None);
    void DrawSpriteFrameOutline(LoadedSpriteFrame frame, Vector2 position, Color outlineTint, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects = SpriteEffects.None, IReadOnlyList<Vector2>? outlineOffsets = null);
    void DrawSpriteFrameShadow(LoadedSpriteFrame frame, Vector2 position, Color tint, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects = SpriteEffects.None);
    void DrawSpriteFrameWithOptionalShadow(LoadedSpriteFrame frame, Vector2 position, Color tint, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects = SpriteEffects.None);
    void DrawSpriteFrame(LoadedSpriteFrame frame, Vector2 position, Color tint, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects = SpriteEffects.None);
    void DrawTopDownPlayerShadow(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, Color tint);
    void DrawWriteBubble(PlayerEntity player, Vector2 cameraPosition);
    IEnumerable<PlayerEntity> EnumerateRemotePlayersForView();
    float GetBackstabReplacementDirectionDegrees(PlayerEntity player);
    int GetCivviePogoTrickPresentationFrameIndex(PlayerEntity player, int frameCount);
    float GetCorpseFadeAlpha(int ticksRemaining);
    Vector2 GetPlayerAnchoredScreenPosition(Vector2 renderPosition, Vector2 cameraPosition, float anchoredWorldX, float anchoredWorldY);
    float GetPlayerBodyAnimationLength(PlayerEntity player, float? horizontalSourceStepSpeed = null);
    Game1.PlayerBodySpriteSelection GetPlayerBodySpriteSelection(PlayerEntity player);
    int GetPlayerBuffBannerDeployDurationTicks(PlayerEntity player);
    int GetPlayerBuffBannerDeployTicksRemaining(PlayerEntity player);
    int GetPlayerCivviePogoCrunchTicksRemaining(PlayerEntity player);
    int GetPlayerCivvieUmbrellaChargeTicks(PlayerEntity player);
    Color GetPlayerColor(PlayerEntity player, Color baseColor);
    int GetPlayerHeavyEatTicksRemaining(PlayerEntity player);
    float GetPlayerIntelRechargeTicks(PlayerEntity player);
    bool GetPlayerIsCivviePogoActive(PlayerEntity player);
    bool GetPlayerIsCivviePogoTrickActive(PlayerEntity player);
    bool GetPlayerIsBuffBannerActive(PlayerEntity player);
    bool GetPlayerIsBuffBannerDeploying(PlayerEntity player);
    bool GetPlayerIsCivvieUmbrellaActive(PlayerEntity player);
    bool GetPlayerIsExperimentalGhostDashing(PlayerEntity player);
    bool GetPlayerIsHeavyEating(PlayerEntity player);
    bool GetPlayerIsSpyCloaked(PlayerEntity player);
    bool GetPlayerIsSpyVisibleToEnemies(PlayerEntity player);
    PlayerEntity GetPlayerPredictedPresentationState(PlayerEntity player);
    Rectangle GetPlayerScreenBounds(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition);
    Vector2 GetPlayerSpriteOrigin(Vector2 renderPosition);
    Vector2 GetPlayerSpriteScreenOrigin(Vector2 renderPosition, Vector2 cameraPosition);
    float GetTorsoReplacementBobOffset(PlayerEntity player);
    bool HasFreshPlayerRenderHistory(PlayerEntity player);
    bool IsBackstabReplacementRenderActive(PlayerEntity player);
    bool IsKritzUberWeaponOnlyVisual(PlayerEntity player);
    bool IsLastToDieSessionActive { get; }
    void NoteRemainsSortCeiling(int sortKey);
    void PlayPredictedGibSound(float worldX, float worldY);
    bool PlayerSkinIncludesCloakedWeapon(PlayerEntity player, Game1.PlayerBodySpriteSelection body);
    void RecordHeavyDashFrameState(PlayerEntity player, string spriteName, int frameIndex, Vector2 renderPosition, Vector2 origin, Vector2 scale, float bodyYOffset, Color tint, bool drawIntelOverlay);
    void RecordLastVisibleEnemySpyFrame(PlayerEntity player, string spriteName, int frameIndex, Vector2 renderPosition, Vector2 origin, Vector2 scale, float bodyYOffset, Color tint, bool drawIntelOverlay);
    void ResolveCorpseDeathKnockback(float corpseX, float corpseY, bool facingLeft, int attackerPlayerId, float damageEventX, float damageEventY, out float knockbackX, out float knockbackY);
    bool ShouldForceLastToDieSpecialEnemyHealthBar(PlayerEntity player);
    bool ShouldHideLastToDieWeaponForPlayer(PlayerEntity player);
    void SpawnDynamicRagdoll(int deadBodyId, int sourcePlayerId, PlayerClass classId, PlayerTeam team, DeadBodyAnimationKind animationKind, string gameplayClassId, bool facingLeft, float x, float y, float knockbackX, float knockbackY);
    void TryDrawAdditionalHealthBar(PlayerEntity player, Vector2 cameraPosition, float visibilityAlpha);
    void TryDrawCivvieUmbrellaShieldBar(PlayerEntity player, Vector2 cameraPosition, float visibilityAlpha);
    bool TryDrawClientPluginDeadBody(Vector2 cameraTopLeft, ClientDeadBodyRenderState deadBody);
    bool TryDrawCorpseAcidDissolve(int corpseId, string gameplayClassId, PlayerClass classId, PlayerTeam team, DeadBodyAnimationKind animationKind, float worldX, float worldY, float corpseHeight, bool facingLeft, float rotationDegrees, int ticksRemaining, Vector2 cameraPosition);
    bool TryDrawDynamicRagdoll(int deadBodyId, int sourcePlayerId, PlayerClass classId, PlayerTeam team, DeadBodyAnimationKind animationKind, float x, float y, float width, float height, bool facingLeft, string gameplayClassId, int ticksRemaining, Vector2 cameraPosition);
    bool TryDrawPlayerSprite(PlayerEntity player, Vector2 cameraPosition, Color tint, Game1.PlayerBodySpriteSelection bodySelection);
    bool TryDrawPlayerSpriteAtPosition(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, Color tint, Game1.PlayerBodySpriteSelection bodySelection, bool drawIntelOverlay, bool drawTopDownShadow = false);
    bool TryDrawSprite(string spriteName, int frameIndex, float worldX, float worldY, Vector2 cameraPosition, Color tint, float rotation = 0f);
    bool TryDrawSprite(string spriteName, int frameIndex, float worldX, float worldY, Vector2 cameraPosition, Color tint, float rotation, float scale);
    bool TryDrawSprite(string spriteName, int frameIndex, float worldX, float worldY, Vector2 cameraPosition, Color tint, float rotation, Vector2 scale);
    bool TryDrawWeaponSprite(PlayerEntity player, Vector2 cameraPosition, Color tint, float visibilityAlpha, Game1.PlayerBodySpriteSelection bodySelection);
    bool TryDrawWeaponSpriteAtPosition(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, Color tint, float visibilityAlpha, Game1.PlayerBodySpriteSelection bodySelection);
    bool TryDrawWeaponSpriteBackdrop(PlayerEntity player, Vector2 cameraPosition, Color tint, float visibilityAlpha, Game1.PlayerBodySpriteSelection bodySelection);
    bool TryGetLastToDieHaxtonSpriteName(PlayerEntity player, out string spriteName);
    bool TryGetLocalPlayerAimDirection(PlayerEntity player, out float aimDirectionDegrees);
    bool TryGetPlayerSkinBody(PlayerEntity player, out Game1.PlayerBodySpriteSelection selection);
    bool ShouldForceMapBotHealthBar(PlayerEntity player);
    bool ShouldPresentExperimentalEngineerEssenceExtractor(PlayerEntity player);
    bool ShouldPresentExperimentalMedicKritzHealNeedles(PlayerEntity player);
    PlayerClass GetRenderWeaponPresentationClassId(PlayerEntity player);
    float GetPlayerAnimationSourceStepSpeed(float speedPerSecond);
    float WrapAnimationImage(float animationImage, float length);
    ClientDeadBodyAnimationKind ToClientDeadBodyAnimationKind(DeadBodyAnimationKind animationKind);
    string? GetDeadBodySpriteName(string gameplayClassId, PlayerClass classId, PlayerTeam team, DeadBodyAnimationKind animationKind = DeadBodyAnimationKind.Default);
    Vector2 RoundToSourcePixels(Vector2 value);

    bool ShouldMeasureClientPerformanceDurations();
    void LogBrowserDrawFrameState(GameTime gameTime);
    void ApplyFrameRateLimit();
    void DrawBase(GameTime gameTime);
    void RecordBrowserDrawDuration(long browserDrawStartTimestamp);
    bool _preLaunchSplashDismissed { get; set; }
}
