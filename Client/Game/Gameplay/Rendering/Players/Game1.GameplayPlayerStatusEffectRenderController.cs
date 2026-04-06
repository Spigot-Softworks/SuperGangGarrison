#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayPlayerStatusEffectRenderController
    {
        private readonly Game1 _game;

        public GameplayPlayerStatusEffectRenderController(Game1 game)
        {
            _game = game;
        }

        public void TryDrawAdditionalHealthBar(PlayerEntity player, Vector2 cameraPosition, float visibilityAlpha)
        {
            if (!_game._showHealthBarEnabled
                || visibilityAlpha <= 0f
                || (!ReferenceEquals(player, _game._world.LocalPlayer) && player.Team != _game._world.LocalPlayer.Team))
            {
                return;
            }

            _game.DrawHealthBar(player, cameraPosition, new Color(105, 215, 95), Color.Black, Color.Black);
        }

        public void DrawExperimentalDemoknightChargeBlur(PlayerEntity player, Vector2 cameraPosition, Color spriteTint, float visibilityAlpha, PlayerBodySpriteSelection bodySelection)
        {
            if (!player.IsExperimentalDemoknightCharging || visibilityAlpha <= 0.05f)
            {
                return;
            }

            var renderPosition = _game.GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _game._world.LocalPlayer));
            var blurDirection = GetExperimentalDemoknightChargeBlurDirection(player);
            if (blurDirection.LengthSquared() <= 0.0001f)
            {
                return;
            }

            var blurTint = spriteTint * 0.45f;
            for (var blurIndex = 0; blurIndex < 2; blurIndex += 1)
            {
                var offsetDistance = 5f + (blurIndex * 6f);
                var blurPosition = renderPosition - blurDirection * offsetDistance;
                var blurAlpha = visibilityAlpha * (blurIndex == 0 ? 0.18f : 0.1f);
                var tint = blurTint * blurAlpha;
                _game.TryDrawPlayerSpriteAtPosition(player, blurPosition, cameraPosition, tint, bodySelection, drawIntelOverlay: false);
                if (!_game.GetPlayerIsHeavyEating(player) && !player.IsTaunting && !_game._world.IsPlayerHumiliated(player))
                {
                    _game.TryDrawWeaponSpriteAtPosition(player, blurPosition, cameraPosition, tint, 1f, bodySelection);
                }
            }
        }

        public void DrawCapturedPointHealingGhosting(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, float visibilityAlpha, PlayerBodySpriteSelection bodySelection)
        {
            if (!_game._world.IsPlayerInsideCapturedPointHealingAuraForVisuals(player) || visibilityAlpha <= 0.05f)
            {
                return;
            }

            var blurDirection = GetExperimentalDemoknightChargeBlurDirection(player);
            if (blurDirection.LengthSquared() <= 0.0001f)
            {
                blurDirection = new Vector2(player.FacingDirectionX, 0f);
            }

            var pulse = 0.5f + (0.5f * MathF.Sin((float)(_game._world.Frame * 0.16f) + player.Id));
            var blurTint = new Color(150, 245, 165);
            for (var blurIndex = 0; blurIndex < 2; blurIndex += 1)
            {
                var offsetDistance = 2f + (blurIndex * 2.5f);
                var blurPosition = renderPosition - blurDirection * offsetDistance;
                var blurAlpha = visibilityAlpha * ((blurIndex == 0 ? 0.12f : 0.07f) + (pulse * 0.05f));
                var tint = blurTint * blurAlpha;
                _game.TryDrawPlayerSpriteAtPosition(player, blurPosition, cameraPosition, tint, bodySelection, drawIntelOverlay: false);
                if (!_game.GetPlayerIsHeavyEating(player) && !player.IsTaunting && !_game._world.IsPlayerHumiliated(player))
                {
                    _game.TryDrawWeaponSpriteAtPosition(player, blurPosition, cameraPosition, tint, 1f, bodySelection);
                }
            }
        }

        public Color GetPlayerColor(PlayerEntity player, Color baseColor)
        {
            return baseColor * _game.GetPlayerVisibilityAlpha(player);
        }

        public void DrawAfterburnOverlay(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, float visibilityAlpha)
        {
            if (!player.IsAlive || !player.IsBurning)
            {
                return;
            }

            var alpha = player.BurnVisualAlpha * visibilityAlpha;
            if (alpha <= 0f)
            {
                return;
            }

            var count = player.BurnVisualCount;
            if (count <= 0)
            {
                return;
            }

            var sprite = _game._runtimeAssets.GetSprite("FlameS");
            var sourceFrame = (int)((_game._world.Frame * LegacyMovementModel.SourceTicksPerSecond) / _game._config.TicksPerSecond);
            var flameColor = Color.White * alpha;
            for (var flameIndex = 0; flameIndex < count; flameIndex += 1)
            {
                player.GetBurnVisualOffset(flameIndex, sourceFrame, out var offsetX, out var offsetY);
                if (sprite is not null && sprite.Frames.Count > 0)
                {
                    var frameIndex = player.GetBurnVisualFrameIndex(flameIndex, sourceFrame, sprite.Frames.Count);
                    _game._spriteBatch.Draw(sprite.Frames[frameIndex], new Vector2(renderPosition.X + offsetX - cameraPosition.X, renderPosition.Y + offsetY - cameraPosition.Y), null, flameColor, 0f, sprite.Origin.ToVector2(), Vector2.One, SpriteEffects.None, 0f);
                    continue;
                }

                var flameRectangle = new Rectangle((int)(renderPosition.X + offsetX - 2f - cameraPosition.X), (int)(renderPosition.Y + offsetY - 2f - cameraPosition.Y), 4, 4);
                _game._spriteBatch.Draw(_game._pixel, flameRectangle, flameColor);
            }
        }

        public void DrawDominationIndicator(PlayerEntity player, Vector2 cameraPosition, float visibilityAlpha)
        {
            if (!player.IsDominatingLocalViewer || visibilityAlpha <= 0f)
            {
                return;
            }

            var renderPosition = _game.GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _game._world.LocalPlayer));
            var frameIndex = player.Team == PlayerTeam.Blue ? 1 : 0;
            _game.DrawCenteredHudSprite("DominationS", frameIndex, new Vector2(renderPosition.X - cameraPosition.X, renderPosition.Y - cameraPosition.Y - 35f), Color.White * visibilityAlpha, Vector2.One);
        }

        public static Color GetUberOverlayColor(PlayerTeam team)
        {
            return team == PlayerTeam.Blue ? Color.Blue : Color.Red;
        }

        public Vector2 GetExperimentalDemoknightChargeBlurDirection(PlayerEntity player)
        {
            return GetExperimentalDemoknightChargeBlurDirectionCore(player);
        }

        private Vector2 GetExperimentalDemoknightChargeBlurDirectionCore(PlayerEntity player)
        {
            var velocity = ReferenceEquals(player, _game._world.LocalPlayer) && _game._hasPredictedLocalPlayerPosition
                ? _game._predictedLocalPlayerVelocity
                : new Vector2(player.HorizontalSpeed, player.VerticalSpeed);
            if (velocity.LengthSquared() <= 0.01f)
            {
                velocity = new Vector2(player.FacingDirectionX, 0f);
            }

            if (velocity.LengthSquared() <= 0.01f)
            {
                return Vector2.Zero;
            }

            velocity.Normalize();
            return velocity;
        }
    }
}
