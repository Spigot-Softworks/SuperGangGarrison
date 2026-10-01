#nullable enable

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Client;

public partial class Game1 : IRenderContext
{
    HudRuntimeSettings IRenderContext.HudRuntimeSettings { get => _hudManager.RuntimeSettings; }
    GameplayManager IRenderContext.GameplayManager => _gameplayManager;

    GameplayPlayerRenderController IRenderContext.GameplayPlayerRenderer => _gameplayPlayerRenderController;

    GameplayDeadBodyRenderController IRenderContext.GameplayDeadBodyRenderer => _gameplayDeadBodyRenderController;

    GameplayPlayerSpriteRenderController IRenderContext.GameplayPlayerSpriteRenderer => _gameplayPlayerSpriteRenderController;

    GameplayPlayerStatusEffectRenderController IRenderContext.GameplayPlayerStatusEffectRenderer => _gameplayPlayerStatusEffectRenderController;

    GameplayWeaponRenderController IRenderContext.GameplayWeaponRenderer => _gameplayWeaponRenderController;









    List<Game1.RetainedDeadBodyVisual> IRenderContext._retainedDeadBodies => _retainedDeadBodies;


    Dictionary<int, Game1.ImmediateNetworkDeadBodyVisual> IRenderContext._immediateNetworkDeadBodies => _immediateNetworkDeadBodies;


    IReadOnlyList<Game1.CivvieUmbrellaShieldBlockVisual> IRenderContext._civvieUmbrellaShieldBlockVisuals => _civvieUmbrellaShieldBlockVisuals;

    Dictionary<int, Game1.PlayerRenderState> IRenderContext._playerRenderStates => _playerRenderStates;

    bool IRenderContext.ShouldMeasureClientPerformanceDurations() => ShouldMeasureClientPerformanceDurations();

    void IRenderContext.LogBrowserDrawFrameState(GameTime gameTime) => LogBrowserFrameState("draw", ref _browserDebugDrawCount, gameTime);

    void IRenderContext.ApplyFrameRateLimit() => ApplyFrameRateLimit();

    void IRenderContext.DrawBase(GameTime gameTime) => base.Draw(gameTime);

    void IRenderContext.RecordBrowserDrawDuration(long browserDrawStartTimestamp) => RecordBrowserDrawDuration(browserDrawStartTimestamp);


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

    void IRenderContext.SpawnDynamicRagdoll(int deadBodyId, int sourcePlayerId, PlayerClass classId, PlayerTeam team, DeadBodyAnimationKind animationKind, string gameplayClassId, bool facingLeft, float x, float y, float knockbackX, float knockbackY, bool diedToFire) => SpawnDynamicRagdoll(deadBodyId, sourcePlayerId, classId, team, animationKind, gameplayClassId, facingLeft, x, y, knockbackX, knockbackY, diedToFire);
    bool IRenderContext.ResolveDeadBodyDiedToFire(int deadBodyId, int sourcePlayerId) => ResolveDeadBodyDiedToFire(deadBodyId, sourcePlayerId);
    bool IRenderContext.TryDrawBurnCharredCorpse(int corpseId, int sourcePlayerId, bool diedToFire, float worldX, float worldY, float corpseHeight, bool facingLeft, string gameplayClassId, PlayerClass classId, PlayerTeam team, DeadBodyAnimationKind animationKind, int ticksRemaining, Vector2 cameraPosition) => TryDrawBurnCharredCorpse(corpseId, sourcePlayerId, diedToFire, worldX, worldY, corpseHeight, facingLeft, gameplayClassId, classId, team, animationKind, ticksRemaining, cameraPosition);
    PlayerSkinDefinition? IRenderContext.GetPlayerSkin(PlayerEntity player) => GetPlayerSkin(player);
    int IRenderContext.GetPlayerStrongDrinkChargeTicks(PlayerEntity player) => GetPlayerStrongDrinkChargeTicks(player);

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

}
