#nullable enable

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Client;

public partial class Game1 : IHudContext
{
    HudRuntimeSettings IHudContext.HudRuntimeSettings { get => _hudManager.RuntimeSettings; }
    float IHudContext._clientUpdateElapsedSeconds { get => _clientUpdateElapsedSeconds; set => _clientUpdateElapsedSeconds = value; }

    OpenGarrison.Core.SimulationConfig IHudContext._config { get => _config; set => _config = value; }


    OpenGarrison.ClientShared.CustomBubbleDocument IHudContext._customBubbleDocument { get => _customBubbleDocument; }






    bool IHudContext._hudEditorOpen { get => _hudEditorOpen; set => _hudEditorOpen = value; }

    OpenGarrison.Client.HudLayoutProfile IHudContext._hudLayoutProfile { get => _hudLayoutProfile; set => _hudLayoutProfile = value; }


    Microsoft.Xna.Framework.Point IHudContext._lastKnownMousePosition { get => _lastKnownMousePosition; set => _lastKnownMousePosition = value; }


    OpenGarrison.Client.NetworkGameClient IHudContext._networkClient { get => _networkClient; }

    Microsoft.Xna.Framework.Graphics.Texture2D IHudContext._pixel { get => _pixel; set => _pixel = value; }





    Microsoft.Xna.Framework.Input.MouseState IHudContext._previousMouse { get => _previousMouse; set => _previousMouse = value; }





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

}
