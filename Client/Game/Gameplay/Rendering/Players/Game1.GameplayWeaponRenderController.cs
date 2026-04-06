#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;

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
                WeaponAnimationMode.Reload when weaponDefinition.ReloadSpriteName is not null => weaponDefinition.ReloadSpriteName,
                WeaponAnimationMode.Recoil when weaponDefinition.RecoilSpriteName is not null => weaponDefinition.RecoilSpriteName,
                _ => weaponDefinition.NormalSpriteName,
            };
            if (spriteName is null)
            {
                return false;
            }

            var sprite = _game._runtimeAssets.GetSprite(spriteName);
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return false;
            }

            var facingScale = GameplayPlayerSpriteRenderController.GetPlayerFacingScale(player);
            var frameIndex = GetWeaponSpriteFrameIndex(player, weaponAnimationMode, weaponDefinition, sprite.Frames.Count);
            var rotation = GetWeaponRotation(player);
            var roundedOrigin = GetRoundedPlayerSpriteOrigin(renderPosition);
            var anchorOrigin = GetWeaponAnchorOrigin(weaponDefinition, sprite);
            var drawX = roundedOrigin.X + (weaponDefinition.XOffset + anchorOrigin.X) * facingScale;
            var drawY = roundedOrigin.Y + weaponDefinition.YOffset + bodySelection.EquipmentOffset + anchorOrigin.Y;
            var position = new Vector2(drawX - cameraPosition.X, drawY - cameraPosition.Y);
            var scale = new Vector2(facingScale, 1f);
            _game.DrawSpriteFrameWithOptionalShadow(sprite.Frames[frameIndex], position, tint, rotation, sprite.Origin.ToVector2(), scale);
            if (player.IsUbered)
            {
                _game.DrawSpriteFrameWithOptionalShadow(sprite.Frames[frameIndex], position, GameplayPlayerStatusEffectRenderController.GetUberOverlayColor(player.Team) * 0.7f, rotation, sprite.Origin.ToVector2(), scale);
            }

            return true;
        }

        public Vector2 GetWeaponShellSpawnOrigin(PlayerEntity player)
        {
            var renderPosition = _game.GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _game._world.LocalPlayer));
            return GetRoundedPlayerSpriteOrigin(renderPosition);
        }

        public static float GetWeaponRotation(PlayerEntity player)
        {
            var radians = System.MathF.PI * player.AimDirectionDegrees / 180f;
            return GameplayPlayerSpriteRenderController.IsFacingLeftByAim(player) ? radians + System.MathF.PI : radians;
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
                var normalSprite = _game._runtimeAssets.GetSprite(weaponDefinition.NormalSpriteName);
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
                if (player.ClassId == PlayerClass.Medic && player.IsMedicHealing && frameCount >= 4)
                {
                    return player.Team == PlayerTeam.Blue ? 3 : 2;
                }

                return System.Math.Clamp(player.Team == PlayerTeam.Blue ? 1 : 0, 0, frameCount - 1);
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
            var teamOffset = player.Team == PlayerTeam.Blue ? perTeamFrames : 0;
            return System.Math.Clamp(teamOffset + animationFrame, 0, frameCount - 1);
        }

        private static WeaponRenderDefinition GetWeaponRenderDefinition(PlayerEntity player)
        {
            var presentation = player.IsExperimentalDemoknightEnabled
                ? StockGameplayModCatalog.GetExperimentalDemoknightEyelanderItem().Presentation
                : StockGameplayModCatalog.GetPrimaryItem(GetRenderWeaponPresentationClassId(player)).Presentation;
            return new WeaponRenderDefinition(
                presentation.WorldSpriteName,
                presentation.RecoilSpriteName,
                presentation.ReloadSpriteName,
                presentation.WeaponOffsetX,
                presentation.WeaponOffsetY,
                GetSourceTicksAsSeconds(presentation.RecoilDurationSourceTicks),
                GetSourceTicksAsSeconds(presentation.ReloadDurationSourceTicks),
                GetSourceTicksAsSeconds(presentation.ScopedRecoilDurationSourceTicks),
                presentation.LoopRecoilWhileActive);
        }

        private static float GetSourceTicksAsSeconds(float ticks)
        {
            return ticks / (float)LegacyMovementModel.SourceTicksPerSecond;
        }
    }
}
