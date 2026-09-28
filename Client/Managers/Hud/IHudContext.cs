#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenGarrison.Client;

public interface IHudContext
{
    GameplayManager Gameplay { get; }
    HudManager Hud { get; }
    GameplayWeaponRenderController GameplayWeaponRenderer { get; }
    float _clientUpdateElapsedSeconds { get; set; }
    OpenGarrison.Core.SimulationConfig _config { get; set; }
    int _cursorSizePercent { get; set; }
    OpenGarrison.ClientShared.CustomBubbleDocument _customBubbleDocument { get; }
    bool _damageVignetteEnabled { get; set; }
    float _damageVignetteFlashIntensity { get; set; }
    float _damageVignetteIntensity { get; set; }
    int _damageVignetteIntensityPercent { get; set; }
    bool _healerRadarEnabled { get; set; }
    bool _hudEditorOpen { get; set; }
    OpenGarrison.Client.HudLayoutProfile _hudLayoutProfile { get; set; }
    bool _hudShowOnlyActiveWeapon { get; set; }
    Microsoft.Xna.Framework.Point _lastKnownMousePosition { get; set; }
    OpenGarrison.Core.PlayerInputSnapshot _latestPredictedLocalInput { get; set; }
    OpenGarrison.Core.LowHealthColorMode _lowHealthColorMode { get; set; }
    OpenGarrison.Client.NetworkGameClient _networkClient { get; }
    Microsoft.Xna.Framework.Graphics.Texture2D _pixel { get; set; }
    bool _portraitRumbleEnabled { get; set; }
    float _portraitRumbleIntensity { get; set; }
    float _portraitRumbleRemainingSeconds { get; set; }
    int _portraitRumbleSeed { get; set; }
    Microsoft.Xna.Framework.Input.MouseState _previousMouse { get; set; }
    bool _showHealerEnabled { get; set; }
    bool _showHealingEnabled { get; set; }
    bool _showPersistentSelfNameEnabled { get; set; }
    bool _showPlayerNamesEnabled { get; set; }
    Microsoft.Xna.Framework.Graphics.SpriteBatch _spriteBatch { get; set; }
    OpenGarrison.Core.SimulationWorld _world { get; set; }
    int ViewportHeight { get; }
    int ViewportWidth { get; }
    void AddHudEditorDummyAbilitySlot();
    Microsoft.Xna.Framework.Color ApplyCurrentHudElementOpacity(Microsoft.Xna.Framework.Color color);
    void CloseCustomBubbleEditor();
    void CloseHudEditor();
    Microsoft.Xna.Framework.Graphics.Texture2D CreateCustomBubbleShellTexture(byte[] pixels);
    void DrawBitmapFontText(string text, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color color, float scale, float rotation);
    void DrawBitmapFontText(string text, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color color, float scale = 1f);
    void DrawBitmapFontTextCentered(string text, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color color, float scale, float rotation);
    void DrawBitmapFontTextCentered(string text, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color color, float scale);
    void DrawBitmapFontTextRightAligned(string text, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color color, float scale);
    void DrawHudTextCentered(string text, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color color, float scale);
    void DrawHudTextLeftAligned(string text, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color color, float scale);
    void DrawHudTextRightAligned(string text, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color color, float scale);
    void DrawLoadedSpriteFrame(OpenGarrison.Client.LoadedSpriteFrame frame, Microsoft.Xna.Framework.Rectangle destinationRectangle, Microsoft.Xna.Framework.Color tint);
    void DrawLoadedSpriteFrame(OpenGarrison.Client.LoadedSpriteFrame frame, Microsoft.Xna.Framework.Vector2 position, Nullable<Microsoft.Xna.Framework.Rectangle> sourceRectangle, Microsoft.Xna.Framework.Color tint, float rotation, Microsoft.Xna.Framework.Vector2 origin, Microsoft.Xna.Framework.Vector2 scale, Microsoft.Xna.Framework.Graphics.SpriteEffects effects, float layerDepth);
    void DrawMenuBitmapFontText(string text, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color color, float scale, float rotation, Microsoft.Xna.Framework.Vector2 rotationCenter);
    void DrawMenuBitmapFontText(string text, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color color, float scale);
    void DrawMenuButtonCentered(Microsoft.Xna.Framework.Rectangle bounds, string label, bool highlighted, float textScale, bool enabled = true);
    void DrawRoundedRectangleFillThenBorder(Microsoft.Xna.Framework.Rectangle bounds, Microsoft.Xna.Framework.Color fillColor, Microsoft.Xna.Framework.Color outlineColor, int outlineThickness, int radius);
    void DrawRoundedRectangleHud(Microsoft.Xna.Framework.Rectangle bounds, Microsoft.Xna.Framework.Color color, int radius);
    void DrawRoundedRectangleOutline(Microsoft.Xna.Framework.Rectangle bounds, Microsoft.Xna.Framework.Color fillColor, Microsoft.Xna.Framework.Color outlineColor, int outlineThickness, int radius);
    void DrawScreenHealthBar(Microsoft.Xna.Framework.Rectangle rectangle, float value, float maxValue, bool useTeamColors, Nullable<Microsoft.Xna.Framework.Color> fillColor = default, Nullable<Microsoft.Xna.Framework.Color> backColor = default, OpenGarrison.Client.Game1.HudFillDirection fillDirection = OpenGarrison.Client.Game1.HudFillDirection.HorizontalLeftToRight);
    float DrawServerPlayerTitle(OpenGarrison.Protocol.PlayerServerTitleState title, Microsoft.Xna.Framework.Vector2 position, float alpha, float scale, bool includeTrailingSpace = true);
    IEnumerable<OpenGarrison.Core.PlayerEntity> EnumerateRenderablePlayers();
    OpenGarrison.Core.PlayerEntity FindPlayerById(int playerId);
    OpenGarrison.Client.LoadedSpriteFrame GetCustomBubbleShellFrame();
    Microsoft.Xna.Framework.Input.MouseState GetFrameMouseState();
    int GetHudEditorDummyAbilitySlotCount();
    Dictionary<string, OpenGarrison.Client.HudResolvedElement> GetHudEditorElements();
    int GetLocalDisplayedMainWeaponCooldownTicks();
    int GetLocalDisplayedMainWeaponCurrentShells();
    int GetLocalDisplayedMainWeaponMaxShells();
    int GetLocalDisplayedMainWeaponReloadTicks();
    OpenGarrison.Core.PrimaryWeaponDefinition GetLocalDisplayedMainWeaponStats();
    int GetPlayerBuffBannerMissingChargeDamage(OpenGarrison.Core.PlayerEntity player);
    int GetPlayerExperimentalGhostDashCooldownTicksRemaining(OpenGarrison.Core.PlayerEntity player);
    int GetPlayerHeavyEatCooldownDurationTicks(OpenGarrison.Core.PlayerEntity player);
    int GetPlayerHeavyEatCooldownTicksRemaining(OpenGarrison.Core.PlayerEntity player);
    Microsoft.Xna.Framework.Rectangle GetPlayerHudScreenBounds(OpenGarrison.Core.PlayerEntity player, Microsoft.Xna.Framework.Vector2 renderPosition, Microsoft.Xna.Framework.Vector2 cameraPosition);
    bool GetPlayerIsBuffBannerActive(OpenGarrison.Core.PlayerEntity player);
    bool GetPlayerIsBuffBannerDeploying(OpenGarrison.Core.PlayerEntity player);
    bool GetPlayerIsCarryingIntel(OpenGarrison.Core.PlayerEntity player);
    bool GetPlayerIsCivvieUmbrellaActive(OpenGarrison.Core.PlayerEntity player);
    bool GetPlayerIsExperimentalGhostDashing(OpenGarrison.Core.PlayerEntity player);
    bool GetPlayerIsMortarLauncherEquipped(OpenGarrison.Core.PlayerEntity player);
    bool GetPlayerIsSniperBowEquipped(OpenGarrison.Core.PlayerEntity player);
    bool GetPlayerIsSniperScoped(OpenGarrison.Core.PlayerEntity player);
    bool GetPlayerIsSpySuperjumpActive(OpenGarrison.Core.PlayerEntity player);
    int GetPlayerMedicHealDartCooldownTicks(OpenGarrison.Core.PlayerEntity player);
    int GetPlayerMedicNeedleRefillTicks(OpenGarrison.Core.PlayerEntity player);
    float GetPlayerMedicUberCharge(OpenGarrison.Core.PlayerEntity player);
    OpenGarrison.Core.MedicUberDeliveryMode GetPlayerMedicUberDeliveryMode(OpenGarrison.Core.PlayerEntity player);
    float GetPlayerMetal(OpenGarrison.Core.PlayerEntity player);
    OpenGarrison.Core.PlayerEntity GetPlayerPredictedPresentationState(OpenGarrison.Core.PlayerEntity player);
    int GetPlayerPyroFlareCooldownTicks(OpenGarrison.Core.PlayerEntity player);
    OpenGarrison.Client.PlayerSkinDefinition GetPlayerSkin(OpenGarrison.Core.PlayerEntity player);
    int GetPlayerSniperBowChargeTicks(OpenGarrison.Core.PlayerEntity player);
    int GetPlayerSniperChargeTicks(OpenGarrison.Core.PlayerEntity player);
    int GetPlayerSniperRifleDamage(OpenGarrison.Core.PlayerEntity player);
    int GetPlayerSpySuperjumpAvailableCharges(OpenGarrison.Core.PlayerEntity player);
    int GetPlayerSpySuperjumpCooldownTicksRemaining(OpenGarrison.Core.PlayerEntity player);
    int GetPlayerSpySuperjumpMaximumCharges(OpenGarrison.Core.PlayerEntity player);
    int GetPlayerStateKey(OpenGarrison.Core.PlayerEntity player);
    float GetPlayerVisibilityAlpha(OpenGarrison.Core.PlayerEntity player);
    Microsoft.Xna.Framework.Vector2 GetRenderPosition(int entityId, float x, float y, bool allowInterpolation = true);
    Microsoft.Xna.Framework.Vector2 GetRenderPosition(OpenGarrison.Core.PlayerEntity player, bool allowInterpolation = true);
    OpenGarrison.Client.LoadedGameMakerSprite GetResolvedSprite(string spriteName);
    Microsoft.Xna.Framework.Vector2 GetWorldHudScreenPosition(Microsoft.Xna.Framework.Vector2 worldPosition, Microsoft.Xna.Framework.Vector2 cameraPosition);
    Microsoft.Xna.Framework.Vector2 GetWorldHudScreenPosition(float worldX, float worldY, Microsoft.Xna.Framework.Vector2 cameraPosition);
    bool HasFreshPlayerRenderHistory(OpenGarrison.Core.PlayerEntity player);
    bool IsCustomBubbleCanvasPixelInsideShell(int pixelIndex);
    bool IsKeyPressed(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.Keys key);
    bool IsLocalSpectatorPresentationActive();
    bool IsPlayerMutedByScoreboardSlot(OpenGarrison.Core.PlayerEntity player);
    bool IsSpyHiddenFromLocalViewer(int ownerId, OpenGarrison.Core.PlayerTeam ownerTeam, float spyX);
    bool IsSpyHiddenFromLocalViewer(OpenGarrison.Core.PlayerEntity player);
    float MeasureBitmapFontHeight(float scale);
    float MeasureBitmapFontWidth(string text, float scale);
    float MeasureMenuBitmapFontHeight(float scale);
    float MeasureMenuBitmapFontWidth(string text, float scale);
    float MeasureServerPlayerTitle(OpenGarrison.Protocol.PlayerServerTitleState title, float scale, bool includeTrailingSpace = true);
    void ResetHudLayoutElements();
    void SaveCustomBubbleEditorPixels(int slotIndex, byte[] pixels);
    void SaveHudLayout();
    void SetHudElementOrigin(string id, Microsoft.Xna.Framework.Vector2 origin);
    void SetHudElementRuntimeDefault(OpenGarrison.Client.HudElementLayout layout);
    bool SetHudElementScale(string id, float scale);
    bool SetHudElementVisibility(string id, bool visible);
    string TrimBitmapMenuText(string text, float maxWidth, float scale);
    bool TryDrawScreenSprite(string spriteName, int frameIndex, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color tint, Microsoft.Xna.Framework.Vector2 scale, float rotation);
    bool TryDrawScreenSprite(string spriteName, int frameIndex, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color tint, Microsoft.Xna.Framework.Vector2 scale);
    bool TryDrawScreenSpritePart(string spriteName, int frameIndex, Microsoft.Xna.Framework.Rectangle sourceRectangle, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color tint, Microsoft.Xna.Framework.Vector2 scale, Microsoft.Xna.Framework.Graphics.SpriteEffects effects, Microsoft.Xna.Framework.Vector2 origin);
    bool TryDrawScreenSpritePart(string spriteName, int frameIndex, Microsoft.Xna.Framework.Rectangle sourceRectangle, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color tint, Microsoft.Xna.Framework.Vector2 scale, Microsoft.Xna.Framework.Graphics.SpriteEffects effects);
    bool TryDrawScreenSpritePart(string spriteName, int frameIndex, Microsoft.Xna.Framework.Rectangle sourceRectangle, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Color tint, Microsoft.Xna.Framework.Vector2 scale);
    bool TryEnsureDamageVignetteTexture(float intensity, out Microsoft.Xna.Framework.Graphics.Texture2D texture);
    bool TryGetOnlinePlayerServerTitle(byte slot, out OpenGarrison.Protocol.PlayerServerTitleState title);
    bool TryGetScoreboardPlayerNetworkSlot(OpenGarrison.Core.PlayerEntity player, out byte slot);
    bool TryResolveHudElement(string id, out OpenGarrison.Client.HudResolvedElement resolved);
    bool TryResolveHudElementEvenIfHidden(string id, out OpenGarrison.Client.HudResolvedElement resolved);
    void UpdateHudElementBounds(string id, Microsoft.Xna.Framework.Rectangle bounds);
}
