#nullable enable

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayWeaponRenderController
    {
        private const string StockMedigunWorldSpriteName = "MedigunS";
        private static readonly Color OffensiveKritzBeamYellowBright = new(225, 255, 107, 255);
        private static readonly Color OffensiveKritzBeamYellowDeep = new(231, 218, 10, 255);

        private readonly Game1 _game;
        private readonly Dictionary<int, LoadedSpriteFrame> _offensiveKritzMedigunAttackFrameCache = new();

        public GameplayWeaponRenderController(Game1 game)
        {
            _game = game;
        }

        public bool TryDrawWeaponSprite(PlayerEntity player, Vector2 cameraPosition, Color tint, float visibilityAlpha, PlayerBodySpriteSelection bodySelection)
        {
            var renderPosition = _game.GetRenderPosition(player);
            return TryDrawWeaponSpriteAtPosition(player, renderPosition, cameraPosition, tint, visibilityAlpha, bodySelection);
        }

        public bool TryDrawWeaponSpriteBackdrop(PlayerEntity player, Vector2 cameraPosition, Color tint, float visibilityAlpha, PlayerBodySpriteSelection bodySelection)
        {
            if (_game.GetPlayerIsCivviePogoActive(player))
            {
                return false;
            }

            if (_game.ShouldHideLastToDieWeaponForPlayer(player))
            {
                return false;
            }

            if (_game.GetPlayerIsSpyCloaked(player) && visibilityAlpha <= PlayerEntity.SpyCloakToggleThreshold)
            {
                return false;
            }

            var renderPosition = _game.GetRenderPosition(player);
            var weaponAnimationMode = GetPlayerWeaponAnimationMode(player);
            var weaponDefinition = GetWeaponRenderDefinition(player, IsCivvieUmbrellaAnimationMode(weaponAnimationMode));
            if (weaponDefinition.NormalSpriteName is null)
            {
                return false;
            }

            var spriteName = weaponAnimationMode switch
            {
                WeaponAnimationMode.ScopedRecoil when weaponDefinition.ReloadSpriteName is not null => weaponDefinition.ReloadSpriteName,
                WeaponAnimationMode.Reload when weaponDefinition.ReloadOverlay.CarrierSpriteName is not null => weaponDefinition.ReloadOverlay.CarrierSpriteName,
                WeaponAnimationMode.Reload when weaponDefinition.ReloadSpriteName is not null => weaponDefinition.ReloadSpriteName,
                WeaponAnimationMode.Recoil when weaponDefinition.RecoilOverlay.CarrierSpriteName is not null => weaponDefinition.RecoilOverlay.CarrierSpriteName,
                WeaponAnimationMode.Recoil when weaponDefinition.RecoilSpriteName is not null => weaponDefinition.RecoilSpriteName,
                _ => weaponDefinition.NormalSpriteName,
            };
            if (spriteName is null)
            {
                return false;
            }

            var sprite = _game.GetResolvedSprite(spriteName);
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return false;
            }

            if (!player.IsUbered)
            {
                return false;
            }

            if (_game.IsKritzUberWeaponOnlyVisual(player))
            {
                return false;
            }

            var facingScale = GetRenderFacingScale(player);
            var playerScale = player.PlayerScale;
            var frameIndex = GetWeaponSpriteFrameIndex(player, weaponAnimationMode, weaponDefinition, sprite.Frames.Count);
            var roundedOrigin = GetRoundedPlayerSpriteOrigin(renderPosition);
            var anchorOrigin = GetWeaponAnchorOrigin(weaponDefinition, sprite);
            var weaponAnchorOffsetX = weaponDefinition.XOffset + anchorOrigin.X;
            var drawX = roundedOrigin.X + (weaponAnchorOffsetX * facingScale * playerScale);
            var drawY = roundedOrigin.Y + ((weaponDefinition.YOffset + bodySelection.EquipmentOffset + anchorOrigin.Y) * playerScale);
            var rotation = GetRenderWeaponRotation(player);
            if (TryApplyLocalWeaponAim(player, roundedOrigin, weaponAnchorOffsetX, playerScale, ref facingScale, ref drawX, drawY, out var localAimRotation))
            {
                rotation = localAimRotation;
            }

            if (!_game._uberOutlineEnabled)
            {
                return false;
            }

            var position = new Vector2(drawX - cameraPosition.X, drawY - cameraPosition.Y);
            ResolveBakedFrame(player, spriteName, frameIndex, rotation,
                sprite, facingScale, playerScale,
                out var drawFrame, out var drawOrigin, out var drawRotation, out var scale);
            var teamColor = GameplayPlayerStatusEffectRenderController.GetUberOverlayColor(player.Team);
            var outlineTint = Color.Lerp(teamColor, Color.White, 0.75f);
            _game.DrawSpriteFrameShadow(drawFrame, position, tint, drawRotation, drawOrigin, scale);
            _game.DrawSpriteFrameOutline(drawFrame, position, outlineTint, drawRotation, drawOrigin, scale);
            return true;
        }

        public bool TryDrawWeaponSpriteAtPosition(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, Color tint, float visibilityAlpha, PlayerBodySpriteSelection bodySelection)
        {
            if (_game.GetPlayerIsCivviePogoActive(player))
            {
                return false;
            }

            if (_game.ShouldHideLastToDieWeaponForPlayer(player))
            {
                return false;
            }

            if (_game.GetPlayerIsSpyCloaked(player) && visibilityAlpha <= PlayerEntity.SpyCloakToggleThreshold)
            {
                return false;
            }

            var weaponAnimationMode = GetPlayerWeaponAnimationMode(player);
            var weaponDefinition = GetWeaponRenderDefinition(player, IsCivvieUmbrellaAnimationMode(weaponAnimationMode));
            if (weaponDefinition.NormalSpriteName is null)
            {
                return false;
            }

            var spriteName = weaponAnimationMode switch
            {
                WeaponAnimationMode.ScopedRecoil when weaponDefinition.ReloadSpriteName is not null => weaponDefinition.ReloadSpriteName,
                WeaponAnimationMode.Reload when weaponDefinition.ReloadOverlay.CarrierSpriteName is not null => weaponDefinition.ReloadOverlay.CarrierSpriteName,
                WeaponAnimationMode.Reload when weaponDefinition.ReloadSpriteName is not null => weaponDefinition.ReloadSpriteName,
                WeaponAnimationMode.Recoil when weaponDefinition.RecoilOverlay.CarrierSpriteName is not null => weaponDefinition.RecoilOverlay.CarrierSpriteName,
                WeaponAnimationMode.Recoil when weaponDefinition.RecoilSpriteName is not null => weaponDefinition.RecoilSpriteName,
                _ => weaponDefinition.NormalSpriteName,
            };
            if (spriteName is null)
            {
                return false;
            }

            var sprite = _game.GetResolvedSprite(spriteName);
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return false;
            }

            var useOffensiveKritzAttackSprite = IsOffensiveKritzBeamPresentation(player);
            if (useOffensiveKritzAttackSprite)
            {
                spriteName = StockMedigunWorldSpriteName;
                sprite = _game.GetResolvedSprite(spriteName);
                if (sprite is null || sprite.Frames.Count == 0)
                {
                    return false;
                }
            }

            if (!TryGetWeaponDrawTransform(
                    player,
                    renderPosition,
                    bodySelection,
                    weaponAnimationMode,
                    weaponDefinition,
                    sprite,
                    out var worldDrawX,
                    out var worldDrawY,
                    out var rotation,
                    out var facingScale,
                    out var playerScale))
            {
                return false;
            }

            var frameIndex = useOffensiveKritzAttackSprite
                ? GetOffensiveKritzMedigunAttackFrameIndex(player, sprite.Frames.Count)
                : GetWeaponSpriteFrameIndex(player, weaponAnimationMode, weaponDefinition, sprite.Frames.Count);
            var roundedOrigin = GetRoundedPlayerSpriteOrigin(renderPosition);
            var position = new Vector2(worldDrawX - cameraPosition.X, worldDrawY - cameraPosition.Y);
            ResolveBakedFrame(player, spriteName, frameIndex, rotation,
                sprite, facingScale, playerScale,
                out var drawFrame, out var drawOrigin, out var drawRotation, out var scale);
            if (useOffensiveKritzAttackSprite)
            {
                drawFrame = GetOffensiveKritzMedigunAttackFrame(sprite, frameIndex);
                drawOrigin = sprite.Origin.ToVector2();
                drawRotation = rotation;
                scale = new Vector2(facingScale * playerScale, playerScale);
            }

            if (_game.IsKritzUberWeaponOnlyVisual(player) && _game._uberOutlineEnabled)
            {
                var teamColor = GameplayPlayerStatusEffectRenderController.GetUberOverlayColor(player.Team);
                var outlineTint = Color.Lerp(teamColor, Color.White, 0.75f);
                _game.DrawSpriteFrameOutline(drawFrame, position, outlineTint, drawRotation, drawOrigin, scale);
            }

            if (player.IsUbered)
            {
                _game.DrawSpriteFrameWithOptionalShadow(drawFrame, position, tint, drawRotation, drawOrigin, scale);
                var teamColor = GameplayPlayerStatusEffectRenderController.GetUberOverlayColor(player.Team);
                _game.DrawSpriteFrameFlatColor(drawFrame, position, teamColor * 0.45f, drawRotation, drawOrigin, scale);
            }
            else
            {
                _game.DrawSpriteFrameWithOptionalShadow(drawFrame, position, tint, drawRotation, drawOrigin, scale);
            }

            DrawWeaponAnimationOverlay(player, weaponAnimationMode, weaponDefinition, roundedOrigin, cameraPosition, tint, bodySelection, facingScale);

            return true;
        }

        public bool TryDrawPlayerHealingWeaponOutlineAtPosition(
            PlayerEntity player,
            Vector2 renderPosition,
            Vector2 cameraPosition,
            Color outlineTint,
            PlayerBodySpriteSelection bodySelection)
        {
            if (outlineTint.A <= 0
                || _game.GetPlayerIsCivviePogoActive(player)
                || _game.ShouldHideLastToDieWeaponForPlayer(player)
                || _game.GetPlayerIsHeavyEating(player)
                || player.IsTaunting
                || _game._world.IsPlayerHumiliated(player))
            {
                return false;
            }

            var weaponAnimationMode = GetPlayerWeaponAnimationMode(player);
            var weaponDefinition = GetWeaponRenderDefinition(player, IsCivvieUmbrellaAnimationMode(weaponAnimationMode));
            if (weaponDefinition.NormalSpriteName is null)
            {
                return false;
            }

            var spriteName = weaponAnimationMode switch
            {
                WeaponAnimationMode.ScopedRecoil when weaponDefinition.ReloadSpriteName is not null => weaponDefinition.ReloadSpriteName,
                WeaponAnimationMode.Reload when weaponDefinition.ReloadOverlay.CarrierSpriteName is not null => weaponDefinition.ReloadOverlay.CarrierSpriteName,
                WeaponAnimationMode.Reload when weaponDefinition.ReloadSpriteName is not null => weaponDefinition.ReloadSpriteName,
                WeaponAnimationMode.Recoil when weaponDefinition.RecoilOverlay.CarrierSpriteName is not null => weaponDefinition.RecoilOverlay.CarrierSpriteName,
                WeaponAnimationMode.Recoil when weaponDefinition.RecoilSpriteName is not null => weaponDefinition.RecoilSpriteName,
                _ => weaponDefinition.NormalSpriteName,
            };
            if (spriteName is null)
            {
                return false;
            }

            var sprite = _game.GetResolvedSprite(spriteName);
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return false;
            }

            if (!TryGetWeaponDrawTransform(
                    player,
                    renderPosition,
                    bodySelection,
                    weaponAnimationMode,
                    weaponDefinition,
                    sprite,
                    out var worldDrawX,
                    out var worldDrawY,
                    out var rotation,
                    out var facingScale,
                    out var playerScale))
            {
                return false;
            }

            var frameIndex = GetWeaponSpriteFrameIndex(player, weaponAnimationMode, weaponDefinition, sprite.Frames.Count);
            var position = new Vector2(worldDrawX - cameraPosition.X, worldDrawY - cameraPosition.Y);
            ResolveBakedFrame(player, spriteName, frameIndex, rotation,
                sprite, facingScale, playerScale,
                out var drawFrame, out var drawOrigin, out var drawRotation, out var scale);
            _game.DrawSpriteFrameOutline(drawFrame, position, outlineTint, drawRotation, drawOrigin, scale);
            return true;
        }

        public Vector2 GetWeaponShellSpawnOrigin(PlayerEntity player)
        {
            var renderPosition = _game.GetRenderPosition(player);
            return GetRoundedPlayerSpriteOrigin(renderPosition);
        }

        public void DrawCivvieUmbrellaShieldBlockVisuals(
            PlayerEntity player,
            Vector2 cameraPosition,
            float visibilityAlpha,
            PlayerBodySpriteSelection bodySelection)
        {
            if (visibilityAlpha <= 0f || _game.GetPlayerIsCivviePogoActive(player))
            {
                return;
            }

            var hasVisual = false;
            for (var index = 0; index < _game._civvieUmbrellaShieldBlockVisuals.Count; index += 1)
            {
                if (_game._civvieUmbrellaShieldBlockVisuals[index].PlayerId == player.Id)
                {
                    hasVisual = true;
                    break;
                }
            }

            if (!hasVisual)
            {
                return;
            }

            const string shieldBlockSpriteName = "CivvieUmbrellaShieldBlockS";
            var shieldSprite = _game.GetResolvedSprite(shieldBlockSpriteName);
            if (shieldSprite is null || shieldSprite.Frames.Count == 0)
            {
                return;
            }

            var renderPosition = _game.GetRenderPosition(player);
            var weaponAnimationMode = _game.GetPlayerIsCivvieUmbrellaActive(player)
                ? GetPlayerWeaponAnimationMode(player)
                : WeaponAnimationMode.CivvieUmbrellaHold;
            var weaponDefinition = GetWeaponRenderDefinition(player, forceCivvieUmbrellaPresentation: true);
            var umbrellaSpriteName = weaponDefinition.NormalSpriteName;
            if (umbrellaSpriteName is null)
            {
                return;
            }

            var umbrellaSprite = _game.GetResolvedSprite(umbrellaSpriteName);
            if (umbrellaSprite is null || umbrellaSprite.Frames.Count == 0)
            {
                return;
            }

            if (!TryGetWeaponDrawTransform(
                    player,
                    renderPosition,
                    bodySelection,
                    weaponAnimationMode,
                    weaponDefinition,
                    umbrellaSprite,
                    out var worldDrawX,
                    out var worldDrawY,
                    out var rotation,
                    out var facingScale,
                    out var playerScale))
            {
                return;
            }

            var umbrellaFrameIndex = GetWeaponSpriteFrameIndex(
                player,
                weaponAnimationMode,
                weaponDefinition,
                umbrellaSprite.Frames.Count);
            ResolveBakedFrame(
                player,
                umbrellaSpriteName,
                umbrellaFrameIndex,
                rotation,
                umbrellaSprite,
                facingScale,
                playerScale,
                out var umbrellaOverlayFrame,
                out var drawOrigin,
                out var drawRotation,
                out var scale);
            var screenPosition = new Vector2(worldDrawX - cameraPosition.X, worldDrawY - cameraPosition.Y);
            for (var index = 0; index < _game._civvieUmbrellaShieldBlockVisuals.Count; index += 1)
            {
                var visual = _game._civvieUmbrellaShieldBlockVisuals[index];
                if (visual.PlayerId != player.Id)
                {
                    continue;
                }

                var frameIndex = System.Math.Clamp(
                    visual.ElapsedTicks * shieldSprite.Frames.Count / CivvieUmbrellaShieldBlockVisual.LifetimeTicks,
                    0,
                    shieldSprite.Frames.Count - 1);
                var alpha = System.Math.Clamp(visual.TicksRemaining / (float)CivvieUmbrellaShieldBlockVisual.FadeTicks, 0f, 1f)
                    * visibilityAlpha;
                ResolveBakedFrame(
                    player,
                    shieldBlockSpriteName,
                    frameIndex,
                    rotation,
                    shieldSprite,
                    facingScale,
                    playerScale,
                    out var drawFrame,
                    out _,
                    out _,
                    out _);
                drawFrame = TrimShieldBlockFrameToUmbrellaOverlay(umbrellaOverlayFrame, drawFrame);
                _game.DrawSpriteFrame(
                    drawFrame,
                    screenPosition,
                    Color.White * alpha,
                    drawRotation,
                    drawOrigin,
                    scale);
            }
        }

        private static LoadedSpriteFrame TrimShieldBlockFrameToUmbrellaOverlay(
            LoadedSpriteFrame umbrellaFrame,
            LoadedSpriteFrame shieldFrame)
        {
            var trimWidth = System.Math.Min(shieldFrame.Width, umbrellaFrame.Width);
            var trimHeight = System.Math.Min(shieldFrame.Height, umbrellaFrame.Height);
            var sourceRect = shieldFrame.SourceRectangle ?? new Rectangle(0, 0, shieldFrame.Width, shieldFrame.Height);
            if (trimWidth == sourceRect.Width && trimHeight == sourceRect.Height)
            {
                return shieldFrame;
            }

            return new LoadedSpriteFrame(
                shieldFrame.Texture,
                SourceRectangle: new Rectangle(sourceRect.X, sourceRect.Y, trimWidth, trimHeight),
                OwnsTexture: false,
                OpaqueBounds: shieldFrame.OpaqueBounds,
                PixelSource: shieldFrame.PixelSource);
        }

        private bool TryGetWeaponDrawTransform(
            PlayerEntity player,
            Vector2 renderPosition,
            PlayerBodySpriteSelection bodySelection,
            WeaponAnimationMode weaponAnimationMode,
            WeaponRenderDefinition weaponDefinition,
            LoadedGameMakerSprite sprite,
            out float worldDrawX,
            out float worldDrawY,
            out float rotation,
            out float facingScale,
            out float playerScale)
        {
            worldDrawX = 0f;
            worldDrawY = 0f;
            rotation = 0f;
            facingScale = 0f;
            playerScale = 0f;

            if (sprite.Frames.Count == 0)
            {
                return false;
            }

            facingScale = GetRenderFacingScale(player);
            playerScale = player.PlayerScale;
            var roundedOrigin = GetRoundedPlayerSpriteOrigin(renderPosition);
            var anchorOrigin = GetWeaponAnchorOrigin(weaponDefinition, sprite);
            var weaponAnchorOffsetX = weaponDefinition.XOffset + anchorOrigin.X;
            worldDrawX = roundedOrigin.X + (weaponAnchorOffsetX * facingScale * playerScale);
            worldDrawY = roundedOrigin.Y + ((weaponDefinition.YOffset + bodySelection.EquipmentOffset + anchorOrigin.Y) * playerScale);
            rotation = GetRenderWeaponRotation(player);
            if (TryApplyLocalWeaponAim(player, roundedOrigin, weaponAnchorOffsetX, playerScale, ref facingScale, ref worldDrawX, worldDrawY, out var localAimRotation))
            {
                rotation = localAimRotation;
            }

            return true;
        }

        public static float GetWeaponRotation(PlayerEntity player)
        {
            var fallbackRadians = System.MathF.PI * player.AimDirectionDegrees / 180f;
            var facingScale = GameplayPlayerSpriteRenderController.IsFacingLeftByAim(player) ? -1f : 1f;
            return GetWeaponRotationFromAim(fallbackRadians, facingScale);
        }

        private static float GetWeaponRotationFromAim(float aimRadians, float facingScale)
        {
            if (facingScale >= 0f)
            {
                return aimRadians;
            }

            return aimRadians + System.MathF.PI;
        }

        private float GetRenderFacingScale(PlayerEntity player)
        {
            if (_game.IsBackstabReplacementRenderActive(player))
            {
                var radians = System.MathF.PI * _game.GetBackstabReplacementDirectionDegrees(player) / 180f;
                return System.MathF.Cos(radians) < 0f ? -1f : 1f;
            }

            if (ReferenceEquals(player, _game._world.LocalPlayer)
                && _game._hasLatestLocalAimWorldPosition
                && _game._useLocalWeaponRotation)
            {
                var aimDeltaX = _game._latestLocalAimWorldX - player.X;
                if (System.MathF.Abs(aimDeltaX) > 0.001f)
                {
                    return aimDeltaX < 0f ? -1f : 1f;
                }
                return player.FacingDirectionX < 0f ? -1f : 1f;
            }

            return GameplayPlayerSpriteRenderController.GetPlayerFacingScale(player);
        }

        private float GetRenderWeaponRotation(PlayerEntity player)
        {
            if (_game.IsBackstabReplacementRenderActive(player))
            {
                var radians = System.MathF.PI * _game.GetBackstabReplacementDirectionDegrees(player) / 180f;
                return GetRenderFacingScale(player) < 0f ? System.MathF.PI - radians : radians;
            }

            if (ReferenceEquals(player, _game._world.LocalPlayer)
                && _game._hasLatestLocalAimWorldPosition
                && _game._useLocalWeaponRotation)
            {
                var aimDeltaX = _game._latestLocalAimWorldX - player.X;
                var aimDeltaY = _game._latestLocalAimWorldY - player.Y;
                var aimRadians = System.MathF.Atan2(aimDeltaY, aimDeltaX);
                var facingScale = System.MathF.Abs(aimDeltaX) > 0.001f
                    ? (aimDeltaX < 0f ? -1f : 1f)
                    : (player.FacingDirectionX < 0f ? -1f : 1f);
                return GetWeaponRotationFromAim(aimRadians, facingScale);
            }

            return GetWeaponRotation(player);
        }

        private bool TryApplyLocalWeaponAim(
            PlayerEntity player,
            Vector2 roundedOrigin,
            float anchorOffsetX,
            float playerScale,
            ref float facingScale,
            ref float drawX,
            float drawY,
            out float rotation)
        {
            rotation = 0f;
            if (!TryGetLocalWeaponAimWorldPosition(player, out var aimWorldX, out var aimWorldY))
            {
                return false;
            }

            var aimRadians = System.MathF.Atan2(aimWorldY - drawY, aimWorldX - drawX);
            var desiredFacingScale = facingScale;
            var aimDeltaX = aimWorldX - drawX;
            if (System.MathF.Abs(aimDeltaX) > 0.001f)
            {
                desiredFacingScale = aimDeltaX < 0f ? -1f : 1f;
            }

            if (desiredFacingScale != facingScale)
            {
                facingScale = desiredFacingScale;
                drawX = roundedOrigin.X + (anchorOffsetX * facingScale * playerScale);
                aimRadians = System.MathF.Atan2(aimWorldY - drawY, aimWorldX - drawX);
            }

            rotation = GetWeaponRotationFromAim(aimRadians, facingScale);
            return true;
        }

        private bool TryGetLocalWeaponAimWorldPosition(PlayerEntity player, out float aimWorldX, out float aimWorldY)
        {
            aimWorldX = 0f;
            aimWorldY = 0f;

            if (!ReferenceEquals(player, _game._world.LocalPlayer)
                || _game.IsLocalSpectatorPresentationActive()
                || _game.IsBackstabReplacementRenderActive(player)
                || !_game._hasLatestLocalAimWorldPosition
                || !_game._useLocalWeaponRotation)
            {
                return false;
            }

            aimWorldX = _game._latestLocalAimWorldX;
            aimWorldY = _game._latestLocalAimWorldY;
            return true;
        }

        public Vector2 GetWeaponAnchorOrigin(WeaponRenderDefinition weaponDefinition, LoadedGameMakerSprite currentSprite)
        {
            return GetWeaponAnchorOriginCore(weaponDefinition, currentSprite);
        }

        /// <summary>
        /// Selects the pre-baked rotation frame when available, otherwise falls back to the
        /// original sprite frame.  Backstab is always rendered via the original path because
        /// it only flips left/right with no angular rotation.
        /// </summary>
        private void ResolveBakedFrame(
            PlayerEntity player,
            string spriteName,
            int frameIndex,
            float rotation,
            LoadedGameMakerSprite sprite,
            float facingScale,
            float playerScale,
            out LoadedSpriteFrame drawFrame,
            out Vector2 drawOrigin,
            out float drawRotation,
            out Vector2 drawScale)
        {
            if (!_game.IsBackstabReplacementRenderActive(player)
                && _game._pixelPerfectWeaponRotation
                && _game._rotatedWeaponSprites is not null)
            {
                // For left-facing weapons GetWeaponRotationFromAim adds +π to rotation so that
                // the unflipped sprite renders correctly mirrored.  For the baked-sprite lookup we
                // need the horizontally-mirrored angle instead: lookupRot = 2π − rotation, wrapped
                // to (−π, π].  The negative X scale on the baked sprite handles the visual flip.
                //
                // Remote players receive AimDirectionDegrees quantized to [0°, 360°), so the raw
                // rotation can be in (π, 2π] for top-right aim angles (e.g. 315° = −45°).
                // Normalise to (−π, π] first so the baked-angle index is always in range.
                var lookupRotation = rotation;
                if (lookupRotation >  System.MathF.PI) lookupRotation -= 2f * System.MathF.PI;
                if (lookupRotation < -System.MathF.PI) lookupRotation += 2f * System.MathF.PI;
                if (facingScale < 0f)
                {
                    lookupRotation = 2f * System.MathF.PI - lookupRotation;
                    if (lookupRotation >  System.MathF.PI) lookupRotation -= 2f * System.MathF.PI;
                    if (lookupRotation < -System.MathF.PI) lookupRotation += 2f * System.MathF.PI;
                }

                if (_game._rotatedWeaponSprites.TryGetBakedFrame(spriteName, frameIndex, lookupRotation,
                        out var bakedFrame, out var bakedOrigin))
                {
                    drawFrame = bakedFrame;
                    drawOrigin = bakedOrigin;
                    drawRotation = 0f;
                    drawScale = new Vector2(facingScale * playerScale * 2f, playerScale * 2f);
                    return;
                }
            }

            drawFrame = sprite.Frames[frameIndex];
            drawOrigin = sprite.Origin.ToVector2();
            drawRotation = rotation;
            drawScale = new Vector2(facingScale * playerScale, playerScale);
        }

        public WeaponAnimationMode GetPlayerWeaponAnimationMode(PlayerEntity player)
        {
            return GetPlayerWeaponAnimationModeCore(player);
        }

        public int GetWeaponSpriteFrameIndex(PlayerEntity player, WeaponAnimationMode weaponAnimationMode, WeaponRenderDefinition weaponDefinition, int frameCount)
        {
            return GetWeaponSpriteFrameIndexCore(player, weaponAnimationMode, weaponDefinition, frameCount);
        }

        public static WeaponRenderDefinition GetWeaponRenderDefinitionProxy(PlayerEntity player) => GetWeaponRenderDefinition(player);
        public static float GetSourceTicksAsSecondsProxy(float ticks) => GetSourceTicksAsSeconds(ticks);

        private Vector2 GetWeaponAnchorOriginCore(WeaponRenderDefinition weaponDefinition, LoadedGameMakerSprite currentSprite)
        {
            if (weaponDefinition.NormalSpriteName is not null)
            {
                var normalSprite = _game.GetResolvedSprite(weaponDefinition.NormalSpriteName);
                if (normalSprite is not null)
                {
                    return normalSprite.Origin.ToVector2();
                }
            }

            return currentSprite.Origin.ToVector2();
        }

        private WeaponAnimationMode GetPlayerWeaponAnimationModeCore(PlayerEntity player)
        {
            return _game._playerRenderStates.TryGetValue(_game.GetPlayerStateKey(player), out var renderState) ? renderState.WeaponAnimationMode : WeaponAnimationMode.Idle;
        }

        private int GetWeaponSpriteFrameIndexCore(PlayerEntity player, WeaponAnimationMode weaponAnimationMode, WeaponRenderDefinition weaponDefinition, int frameCount)
        {
            if (frameCount <= 0)
            {
                return 0;
            }

            if (IsCivvieUmbrellaAnimationMode(weaponAnimationMode))
            {
                return GetCivvieUmbrellaFrameIndex(player, weaponAnimationMode, frameCount);
            }

            if (weaponAnimationMode == WeaponAnimationMode.Idle)
            {
                var useBlueTeamMedigunFrames = ShouldUseBlueTeamMedigunFrames(player);
                if (IsMedigunPresentationUser(player) && player.IsMedicHealing && frameCount >= 4)
                {
                    return useBlueTeamMedigunFrames ? 3 : 2;
                }

                return System.Math.Clamp(useBlueTeamMedigunFrames ? 1 : 0, 0, frameCount - 1);
            }

            if (!_game._playerRenderStates.TryGetValue(_game.GetPlayerStateKey(player), out var renderState))
            {
                return 0;
            }

            var perTeamFrames = System.Math.Max(1, frameCount / 2);
            var durationSeconds = System.MathF.Max(renderState.WeaponAnimationDurationSeconds, 0.0001f);
            var animationPosition = (renderState.WeaponAnimationElapsedSeconds / durationSeconds) * perTeamFrames;
            var animationFrame = weaponAnimationMode == WeaponAnimationMode.Recoil && weaponDefinition.LoopRecoilWhileActive
                ? System.Math.Clamp((int)System.MathF.Floor(WrapAnimationImage(animationPosition, perTeamFrames)), 0, perTeamFrames - 1)
                : System.Math.Clamp((int)System.MathF.Floor(animationPosition), 0, perTeamFrames - 1);
            var teamOffset = ShouldUseBlueTeamMedigunFrames(player) ? perTeamFrames : 0;
            return System.Math.Clamp(teamOffset + animationFrame, 0, frameCount - 1);
        }

        private static WeaponRenderDefinition GetWeaponRenderDefinition(PlayerEntity player, bool forceCivvieUmbrellaPresentation = false)
        {
            var presentation = ResolveRenderPresentation(player, forceCivvieUmbrellaPresentation);
            return new WeaponRenderDefinition(
                presentation.WorldSpriteName,
                presentation.RecoilSpriteName,
                presentation.ReloadSpriteName,
                new WeaponAnimationOverlayDefinition(
                    presentation.RecoilCarrierSpriteName,
                    presentation.RecoilOverlaySpriteName,
                    presentation.RecoilOverlayOffsetX,
                    presentation.RecoilOverlayOffsetY,
                    presentation.RecoilOverlayRotationDegrees),
                new WeaponAnimationOverlayDefinition(
                    presentation.ReloadCarrierSpriteName,
                    presentation.ReloadOverlaySpriteName,
                    presentation.ReloadOverlayOffsetX,
                    presentation.ReloadOverlayOffsetY,
                    presentation.ReloadOverlayRotationDegrees),
                presentation.WeaponOffsetX,
                presentation.WeaponOffsetY,
                GetSourceTicksAsSeconds(presentation.RecoilDurationSourceTicks),
                GetSourceTicksAsSeconds(presentation.ReloadDurationSourceTicks),
                GetSourceTicksAsSeconds(presentation.ScopedRecoilDurationSourceTicks),
                presentation.LoopRecoilWhileActive);
        }

        private static GameplayItemPresentationDefinition ResolveRenderPresentation(PlayerEntity player, bool forceCivvieUmbrellaPresentation = false)
        {
            if ((player.IsCivvieUmbrellaActive || forceCivvieUmbrellaPresentation)
                && !string.IsNullOrWhiteSpace(player.GameplayLoadoutState.SecondaryItemId))
            {
                var secondaryItem = CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(player.GameplayLoadoutState.SecondaryItemId);
                if (string.Equals(secondaryItem.BehaviorId, BuiltInGameplayBehaviorIds.CivvieUmbrella, System.StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(secondaryItem.Presentation.WorldSpriteName))
                {
                    return secondaryItem.Presentation;
                }
            }

            if (player.IsExperimentalDemoknightEnabled)
            {
                return StockGameplayModCatalog.GetExperimentalDemoknightEyelanderItem().Presentation;
            }

            if (ShouldPresentExperimentalEngineerEssenceExtractor(player))
            {
                return CharacterClassCatalog.RuntimeRegistry.GetPrimaryItem(PlayerClass.Medic).Presentation;
            }

            if (ShouldPresentExperimentalMedicKritzHealNeedles(player)
                && !string.IsNullOrWhiteSpace(player.GameplayLoadoutState.SecondaryItemId))
            {
                var kritzItem = CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(player.GameplayLoadoutState.SecondaryItemId);
                if (string.Equals(kritzItem.BehaviorId, BuiltInGameplayBehaviorIds.MedigunCrit, StringComparison.Ordinal))
                {
                    return kritzItem.Presentation;
                }
            }

            // Check if experimental offhand weapon is equipped (e.g., Soldier shotgun, Demoman grenade launcher)
            if (player.IsExperimentalOffhandEquipped && player.ExperimentalOffhandWeapon is not null)
            {
                var offhandItemId = CharacterClassCatalog.RuntimeRegistry.TryResolvePrimaryWeaponItemId(player.ExperimentalOffhandWeapon, out var itemId)
                    ? itemId
                    : null;

                // Guard against stale ExperimentalOffhandWeapon persisting across class changes
                // online: only use the offhand presentation if the weapon's item ID still matches
                // the player's current secondary or utility loadout slot.
                if (!string.IsNullOrWhiteSpace(offhandItemId)
                    && (string.Equals(offhandItemId, player.GameplayLoadoutState.SecondaryItemId, StringComparison.Ordinal)
                        || string.Equals(offhandItemId, player.GameplayLoadoutState.UtilityItemId, StringComparison.Ordinal)))
                {
                    return CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(offhandItemId).Presentation;
                }
            }

            var equippedItemId = player.GameplayLoadoutState.EquippedItemId;
            if (!string.IsNullOrWhiteSpace(equippedItemId))
            {
                return CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(equippedItemId).Presentation;
            }

            return CharacterClassCatalog.RuntimeRegistry.GetPrimaryItem(GetRenderWeaponPresentationClassId(player)).Presentation;
        }

        private static bool IsCivvieUmbrellaAnimationMode(WeaponAnimationMode mode)
        {
            return mode is WeaponAnimationMode.CivvieUmbrellaOpening
                or WeaponAnimationMode.CivvieUmbrellaHold
                or WeaponAnimationMode.CivvieUmbrellaClosing;
        }

        private int GetCivvieUmbrellaFrameIndex(PlayerEntity player, WeaponAnimationMode mode, int frameCount)
        {
            if (frameCount <= 0)
            {
                return 0;
            }

            var perTeamFrames = System.Math.Max(1, frameCount / 2);
            var teamOffset = ShouldUseBlueTeamMedigunFrames(player) ? perTeamFrames : 0;
            var elapsedSeconds = 0f;
            var durationSeconds = 0f;
            if (_game._playerRenderStates.TryGetValue(_game.GetPlayerStateKey(player), out var renderState))
            {
                elapsedSeconds = renderState.WeaponAnimationElapsedSeconds;
                durationSeconds = renderState.WeaponAnimationDurationSeconds;
            }

            var localFrame = mode switch
            {
                WeaponAnimationMode.CivvieUmbrellaOpening => GetCivvieUmbrellaTimedFrame(
                    elapsedSeconds,
                    durationSeconds,
                    startFrame: 0,
                    frameCount: System.Math.Min(PlayerEntity.CivvieUmbrellaOpeningFrameCount, perTeamFrames)),
                WeaponAnimationMode.CivvieUmbrellaClosing => GetCivvieUmbrellaTimedFrame(
                    elapsedSeconds,
                    durationSeconds,
                    startFrame: 4,
                    frameCount: System.Math.Max(1, System.Math.Min(2, perTeamFrames - 4))),
                _ => System.Math.Min(3, perTeamFrames - 1),
            };

            return System.Math.Clamp(teamOffset + System.Math.Clamp(localFrame, 0, perTeamFrames - 1), 0, frameCount - 1);
        }

        private static int GetCivvieUmbrellaTimedFrame(float elapsedSeconds, float durationSeconds, int startFrame, int frameCount)
        {
            if (frameCount <= 1 || durationSeconds <= 0f)
            {
                return startFrame + System.Math.Max(0, frameCount - 1);
            }

            var progress = System.Math.Clamp(elapsedSeconds / durationSeconds, 0f, 0.9999f);
            return startFrame + System.Math.Clamp((int)System.MathF.Floor(progress * frameCount), 0, frameCount - 1);
        }

        private static float GetSourceTicksAsSeconds(float ticks)
        {
            return ticks / (float)LegacyMovementModel.SourceTicksPerSecond;
        }

        private void DrawWeaponAnimationOverlay(
            PlayerEntity player,
            WeaponAnimationMode weaponAnimationMode,
            WeaponRenderDefinition weaponDefinition,
            Vector2 roundedOrigin,
            Vector2 cameraPosition,
            Color tint,
            PlayerBodySpriteSelection bodySelection,
            float facingScale)
        {
            var overlayDefinition = weaponAnimationMode switch
            {
                WeaponAnimationMode.Reload => weaponDefinition.ReloadOverlay,
                WeaponAnimationMode.Recoil => weaponDefinition.RecoilOverlay,
                _ => default,
            };
            if (overlayDefinition.OverlaySpriteName is null)
            {
                return;
            }

            var overlaySprite = _game.GetResolvedSprite(overlayDefinition.OverlaySpriteName);
            if (overlaySprite is null || overlaySprite.Frames.Count == 0)
            {
                return;
            }

            var overlayFrameIndex = GetWeaponAnimationOverlayFrameIndex(player, overlaySprite.Frames.Count);
            var overlayRotation = GetRenderWeaponRotation(player) + MathHelper.ToRadians(overlayDefinition.RotationDegrees * facingScale);
            var playerScale = player.PlayerScale;
            var overlayAnchorOffsetX = weaponDefinition.XOffset + overlayDefinition.OffsetX + overlaySprite.Origin.X;
            var drawX = roundedOrigin.X + (overlayAnchorOffsetX * facingScale * playerScale);
            var drawY = roundedOrigin.Y + ((weaponDefinition.YOffset + overlayDefinition.OffsetY + bodySelection.EquipmentOffset + overlaySprite.Origin.Y) * playerScale);
            if (TryApplyLocalWeaponAim(player, roundedOrigin, overlayAnchorOffsetX, playerScale, ref facingScale, ref drawX, drawY, out var localAimRotation))
            {
                overlayRotation = localAimRotation + MathHelper.ToRadians(overlayDefinition.RotationDegrees * facingScale);
            }

            var position = new Vector2(drawX - cameraPosition.X, drawY - cameraPosition.Y);
            var scale = new Vector2(facingScale * playerScale, playerScale);
            _game.DrawSpriteFrameWithOptionalShadow(overlaySprite.Frames[overlayFrameIndex], position, tint, overlayRotation, overlaySprite.Origin.ToVector2(), scale);
            if (player.IsUbered)
            {
                _game.DrawSpriteFrameWithOptionalShadow(overlaySprite.Frames[overlayFrameIndex], position, GameplayPlayerStatusEffectRenderController.GetUberOverlayColor(player.Team) * 0.7f, overlayRotation, overlaySprite.Origin.ToVector2(), scale);
            }
        }

        private static int GetWeaponAnimationOverlayFrameIndex(PlayerEntity player, int frameCount)
        {
            if (frameCount <= 1)
            {
                return 0;
            }

            return Math.Clamp(ShouldUseBlueTeamMedigunFrames(player) ? 1 : 0, 0, frameCount - 1);
        }

        private static bool ShouldUseBlueTeamMedigunFrames(PlayerEntity player)
        {
            return player.IsExperimentalEngineerFreezeRayPresented || player.Team == PlayerTeam.Blue;
        }

        private bool IsOffensiveKritzBeamPresentation(PlayerEntity player)
        {
            if (!player.HasEquippedBehavior(BuiltInGameplayBehaviorIds.MedigunCrit)
                || !player.IsMedicHealing
                || !player.MedicHealTargetId.HasValue)
            {
                return false;
            }

            var healTarget = _game.FindPlayerById(player.MedicHealTargetId.Value);
            return healTarget is not null
                && healTarget.IsAlive
                && healTarget.Team != player.Team;
        }

        private static int GetOffensiveKritzMedigunAttackFrameIndex(PlayerEntity player, int frameCount)
        {
            if (frameCount < 4)
            {
                return System.Math.Clamp(ShouldUseBlueTeamMedigunFrames(player) ? 1 : 0, 0, frameCount - 1);
            }

            return ShouldUseBlueTeamMedigunFrames(player) ? 3 : 2;
        }

        private LoadedSpriteFrame GetOffensiveKritzMedigunAttackFrame(LoadedGameMakerSprite sprite, int attackFrameIndex)
        {
            if (_offensiveKritzMedigunAttackFrameCache.TryGetValue(attackFrameIndex, out var cachedFrame))
            {
                return cachedFrame;
            }

            var idleFrameIndex = attackFrameIndex - 2;
            var attackFrame = sprite.Frames[attackFrameIndex];
            var idleFrame = sprite.Frames[System.Math.Clamp(idleFrameIndex, 0, sprite.Frames.Count - 1)];
            var width = attackFrame.Width;
            var height = attackFrame.Height;
            var pixelCount = width * height;
            var attackPixels = new Color[pixelCount];
            var idlePixels = new Color[pixelCount];
            if (!TryReadSpriteFramePixels(attackFrame, attackPixels)
                || !TryReadSpriteFramePixels(idleFrame, idlePixels))
            {
                return attackFrame;
            }

            for (var index = 0; index < pixelCount; index += 1)
            {
                var attackPixel = attackPixels[index];
                if (attackPixel.A == 0 || attackPixel == idlePixels[index])
                {
                    continue;
                }

                attackPixels[index] = MapOffensiveKritzFlarePixel(attackPixel);
            }

            var texture = new Texture2D(_game.GraphicsDevice, width, height);
            texture.SetData(attackPixels);
            cachedFrame = new LoadedSpriteFrame(
                texture,
                OwnsTexture: true,
                PixelSource: new LoadedSpriteFramePixelSource(attackPixels, width, height));
            _offensiveKritzMedigunAttackFrameCache[attackFrameIndex] = cachedFrame;
            return cachedFrame;
        }

        private static bool TryReadSpriteFramePixels(LoadedSpriteFrame frame, Color[] destination)
        {
            if (frame.TryCopyPixelData(destination))
            {
                return true;
            }

            var sourceRectangle = frame.SourceRectangle ?? new Rectangle(0, 0, frame.Width, frame.Height);
            if (destination.Length < sourceRectangle.Width * sourceRectangle.Height)
            {
                return false;
            }

            frame.Texture.GetData(0, sourceRectangle, destination, 0, sourceRectangle.Width * sourceRectangle.Height);
            return true;
        }

        private static Color MapOffensiveKritzFlarePixel(Color pixel)
        {
            return (pixel.R, pixel.G, pixel.B) switch
            {
                (255, 148, 148) => OffensiveKritzBeamYellowBright,
                (255, 106, 106) => OffensiveKritzBeamYellowBright,
                (154, 0, 0) => OffensiveKritzBeamYellowDeep,
                (148, 190, 255) => OffensiveKritzBeamYellowBright,
                (106, 165, 255) => OffensiveKritzBeamYellowBright,
                (15, 21, 131) => OffensiveKritzBeamYellowDeep,
                _ => pixel.R + pixel.G + pixel.B >= 500
                    ? OffensiveKritzBeamYellowBright
                    : OffensiveKritzBeamYellowDeep,
            };
        }
    }
}
