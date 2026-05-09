#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayWeaponRenderController
    {
        private readonly Game1 _game;

        public GameplayWeaponRenderController(Game1 game)
        {
            _game = game;
        }

        public bool TryDrawWeaponSprite(PlayerEntity player, Vector2 cameraPosition, Color tint, float visibilityAlpha, PlayerBodySpriteSelection bodySelection)
        {
            var renderPosition = _game.GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _game._world.LocalPlayer));
            return TryDrawWeaponSpriteAtPosition(player, renderPosition, cameraPosition, tint, visibilityAlpha, bodySelection);
        }

        public bool TryDrawWeaponSpriteBackdrop(PlayerEntity player, Vector2 cameraPosition, Color tint, float visibilityAlpha, PlayerBodySpriteSelection bodySelection)
        {
            if (_game.GetPlayerIsSpyCloaked(player) && visibilityAlpha <= PlayerEntity.SpyCloakToggleThreshold)
            {
                return false;
            }

            var renderPosition = _game.GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _game._world.LocalPlayer));
            var weaponDefinition = GetWeaponRenderDefinition(player);
            if (weaponDefinition.NormalSpriteName is null)
            {
                return false;
            }

            var weaponAnimationMode = GetPlayerWeaponAnimationMode(player);
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
            var drawX = roundedOrigin.X + ((weaponDefinition.XOffset + anchorOrigin.X) * facingScale * playerScale);
            var drawY = roundedOrigin.Y + ((weaponDefinition.YOffset + bodySelection.EquipmentOffset + anchorOrigin.Y) * playerScale);
            var rotation = GetRenderWeaponRotation(player);

            if (TryGetLocalFlamethrowerAimWorldPosition(player, cameraPosition, out var aimWorldX, out var aimWorldY))
            {
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
                    drawX = roundedOrigin.X + ((weaponDefinition.XOffset + anchorOrigin.X) * facingScale * playerScale);
                    aimRadians = System.MathF.Atan2(aimWorldY - drawY, aimWorldX - drawX);
                }

                rotation = GetWeaponRotationFromAim(aimRadians, facingScale);
            }

            if (!_game._uberOutlineEnabled)
            {
                return false;
            }

            var position = new Vector2(drawX - cameraPosition.X, drawY - cameraPosition.Y);
            var scale = new Vector2(facingScale * playerScale, playerScale);
            var teamColor = GameplayPlayerStatusEffectRenderController.GetUberOverlayColor(player.Team);
            var outlineTint = Color.Lerp(teamColor, Color.White, 0.75f);
            _game.DrawSpriteFrameShadow(sprite.Frames[frameIndex], position, tint, rotation, sprite.Origin.ToVector2(), scale);
            _game.DrawSpriteFrameOutline(sprite.Frames[frameIndex], position, outlineTint, rotation, sprite.Origin.ToVector2(), scale);
            return true;
        }

        public bool TryDrawWeaponSpriteAtPosition(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, Color tint, float visibilityAlpha, PlayerBodySpriteSelection bodySelection)
        {
            if (_game.GetPlayerIsSpyCloaked(player) && visibilityAlpha <= PlayerEntity.SpyCloakToggleThreshold)
            {
                return false;
            }

            var weaponDefinition = GetWeaponRenderDefinition(player);
            if (weaponDefinition.NormalSpriteName is null)
            {
                return false;
            }

            var weaponAnimationMode = GetPlayerWeaponAnimationMode(player);
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

            var facingScale = GetRenderFacingScale(player);
            var playerScale = player.PlayerScale;
            var frameIndex = GetWeaponSpriteFrameIndex(player, weaponAnimationMode, weaponDefinition, sprite.Frames.Count);
            var roundedOrigin = GetRoundedPlayerSpriteOrigin(renderPosition);
            var anchorOrigin = GetWeaponAnchorOrigin(weaponDefinition, sprite);
            var drawX = roundedOrigin.X + ((weaponDefinition.XOffset + anchorOrigin.X) * facingScale * playerScale);
            var drawY = roundedOrigin.Y + ((weaponDefinition.YOffset + bodySelection.EquipmentOffset + anchorOrigin.Y) * playerScale);
            var rotation = GetRenderWeaponRotation(player);

            if (TryGetLocalFlamethrowerAimWorldPosition(player, cameraPosition, out var aimWorldX, out var aimWorldY))
            {
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
                    drawX = roundedOrigin.X + ((weaponDefinition.XOffset + anchorOrigin.X) * facingScale * playerScale);
                    aimRadians = System.MathF.Atan2(aimWorldY - drawY, aimWorldX - drawX);
                }

                rotation = GetWeaponRotationFromAim(aimRadians, facingScale);
            }

            var position = new Vector2(drawX - cameraPosition.X, drawY - cameraPosition.Y);
            var scale = new Vector2(facingScale * playerScale, playerScale);
            if (_game.IsKritzUberWeaponOnlyVisual(player) && _game._uberOutlineEnabled)
            {
                var teamColor = GameplayPlayerStatusEffectRenderController.GetUberOverlayColor(player.Team);
                var outlineTint = Color.Lerp(teamColor, Color.White, 0.75f);
                _game.DrawSpriteFrameOutline(sprite.Frames[frameIndex], position, outlineTint, rotation, sprite.Origin.ToVector2(), scale);
            }

            if (player.IsUbered)
            {
                _game.DrawSpriteFrameWithOptionalShadow(sprite.Frames[frameIndex], position, tint, rotation, sprite.Origin.ToVector2(), scale);
                var teamColor = GameplayPlayerStatusEffectRenderController.GetUberOverlayColor(player.Team);
                _game.DrawSpriteFrameFlatColor(sprite.Frames[frameIndex], position, teamColor * 0.45f, rotation, sprite.Origin.ToVector2(), scale);
            }
            else
            {
                _game.DrawSpriteFrameWithOptionalShadow(sprite.Frames[frameIndex], position, tint, rotation, sprite.Origin.ToVector2(), scale);
            }

            DrawWeaponAnimationOverlay(player, weaponAnimationMode, weaponDefinition, roundedOrigin, cameraPosition, tint, bodySelection, facingScale);

            return true;
        }

        public Vector2 GetWeaponShellSpawnOrigin(PlayerEntity player)
        {
            var renderPosition = _game.GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _game._world.LocalPlayer));
            return GetRoundedPlayerSpriteOrigin(renderPosition);
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
                && _game._hasLatestLocalAimWorldPosition)
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
                && _game._hasLatestLocalAimWorldPosition)
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

        private bool TryGetLocalFlamethrowerAimWorldPosition(PlayerEntity player, Vector2 cameraPosition, out float aimWorldX, out float aimWorldY)
        {
            aimWorldX = 0f;
            aimWorldY = 0f;

            if (!ReferenceEquals(player, _game._world.LocalPlayer)
                || _game._networkClient.IsSpectator
                || GetRenderWeaponStats(player).Kind != PrimaryWeaponKind.FlameThrower)
            {
                return false;
            }

            var mouse = _game.GetScaledMouseState(_game.GetConstrainedMouseState(Game1.GetCurrentMouseState()));
            aimWorldX = cameraPosition.X + mouse.X;
            aimWorldY = cameraPosition.Y + mouse.Y;
            return true;
        }

        public Vector2 GetWeaponAnchorOrigin(WeaponRenderDefinition weaponDefinition, LoadedGameMakerSprite currentSprite)
        {
            return GetWeaponAnchorOriginCore(weaponDefinition, currentSprite);
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

        private static WeaponRenderDefinition GetWeaponRenderDefinition(PlayerEntity player)
        {
            var presentation = ResolveRenderPresentation(player);
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

        private static GameplayItemPresentationDefinition ResolveRenderPresentation(PlayerEntity player)
        {
            if (player.IsExperimentalDemoknightEnabled)
            {
                return StockGameplayModCatalog.GetExperimentalDemoknightEyelanderItem().Presentation;
            }

            if (ShouldPresentExperimentalEngineerEssenceExtractor(player))
            {
                return CharacterClassCatalog.RuntimeRegistry.GetPrimaryItem(PlayerClass.Medic).Presentation;
            }

            var equippedItemId = player.GameplayLoadoutState.EquippedItemId;
            if (!string.IsNullOrWhiteSpace(equippedItemId))
            {
                return CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(equippedItemId).Presentation;
            }

            return CharacterClassCatalog.RuntimeRegistry.GetPrimaryItem(GetRenderWeaponPresentationClassId(player)).Presentation;
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
            var drawX = roundedOrigin.X + ((weaponDefinition.XOffset + overlayDefinition.OffsetX + overlaySprite.Origin.X) * facingScale * playerScale);
            var drawY = roundedOrigin.Y + ((weaponDefinition.YOffset + overlayDefinition.OffsetY + bodySelection.EquipmentOffset + overlaySprite.Origin.Y) * playerScale);
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
    }
}
