#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    internal const int SniperChargeHudFillMaxWidth = 40;

    internal static int GetSniperChargeHudFillWidthForTicks(int chargeTicks)
    {
        if (chargeTicks <= 0)
        {
            return 0;
        }

        return Math.Clamp(
            (int)MathF.Ceiling(chargeTicks * (SniperChargeHudFillMaxWidth / (float)PlayerEntity.SniperChargeMaxTicks)),
            0,
            SniperChargeHudFillMaxWidth);
    }

    private sealed class GameplayAimHudController
    {
        private readonly Game1 _game;

        public GameplayAimHudController(Game1 game)
        {
            _game = game;
        }

        public void DrawSniperHud(Vector2 screenAimPosition)
        {
            if (!_game._world.LocalPlayer.HasScopedSniperWeaponEquipped || !_game.GetPlayerIsSniperScoped(_game._world.LocalPlayer))
            {
                return;
            }

            DrawSniperChargeHud(_game._world.LocalPlayer, screenAimPosition);
        }

        public void DrawSpectatorSniperHud(PlayerEntity player, Vector2 screenAimPosition)
        {
            if (!player.HasScopedSniperWeaponEquipped || !_game.GetPlayerIsSniperScoped(player))
            {
                return;
            }

            DrawSniperChargeHud(player, screenAimPosition);
        }

        private void DrawSniperChargeHud(PlayerEntity player, Vector2 screenAimPosition)
        {
            var damage = _game.GetPlayerSniperRifleDamage(player);
            var facingLeft = IsFacingLeftByAim(player);
            var chargeScaleX = facingLeft ? 1f : -1f;
            var chargePosition = screenAimPosition + new Vector2(15f * chargeScaleX, -10f);
            if (damage < 85)
            {
                _game.TryDrawScreenSprite("ChargeS", 0, chargePosition, Color.White * 0.25f, new Vector2(chargeScaleX, 1f));
            }
            else
            {
                _game.TryDrawScreenSprite("FullChargeS", 0, screenAimPosition + new Vector2(65f * chargeScaleX, 0f), Color.White, Vector2.One);
            }

            var chargeWidth = GetSniperChargeHudFillWidthForTicks(_game.GetPlayerSniperChargeTicks(player));
            if (chargeWidth <= 0)
            {
                return;
            }

            DrawSniperChargeFill(chargePosition, chargeWidth, facingLeft);
        }

        private void DrawSniperChargeFill(Vector2 chargePosition, int chargeWidth, bool facingLeft)
        {
            var sprite = _game.GetResolvedSprite("ChargeS");
            if (sprite is null || sprite.Frames.Count <= 1)
            {
                return;
            }

            var frame = sprite.Frames[1];
            chargeWidth = Math.Clamp(chargeWidth, 0, frame.Width);
            if (chargeWidth <= 0)
            {
                return;
            }

            if (facingLeft)
            {
                _game.TryDrawScreenSpritePart(
                    "ChargeS",
                    1,
                    new Rectangle(0, 0, chargeWidth, frame.Height),
                    chargePosition,
                    Color.White * 0.8f,
                    Vector2.One);
                return;
            }

            var sourceX = frame.Width - chargeWidth;
            var drawPosition = new Vector2(chargePosition.X - chargeWidth, chargePosition.Y);
            _game.TryDrawScreenSpritePart(
                "ChargeS",
                1,
                new Rectangle(sourceX, 0, chargeWidth, frame.Height),
                drawPosition,
                Color.White * 0.8f,
                Vector2.One);
        }

        public void DrawCrosshair(Vector2 screenPosition)
        {
            var crosshair = _game.GetResolvedSprite("CrosshairS");
            if (crosshair is null || crosshair.Frames.Count == 0)
            {
                return;
            }

            _game.DrawLoadedSpriteFrame(crosshair.Frames[0], screenPosition, null, Color.White, 0f, crosshair.Origin.ToVector2(), Vector2.One, SpriteEffects.None, 0f);
        }

        public void DrawControllerAimLine(Vector2 cameraPosition, Vector2 screenAimPosition)
        {
            if (!_game._world.LocalPlayer.IsAlive)
            {
                return;
            }

            var playerScreenPosition = _game.GetRenderPosition(_game._world.LocalPlayer) - cameraPosition;
            var delta = screenAimPosition - playerScreenPosition;
            var length = delta.Length();
            if (length <= 1f)
            {
                return;
            }

            var direction = delta / length;
            var lineStart = playerScreenPosition + (direction * MathF.Min(12f, length * 0.25f));
            var lineEnd = screenAimPosition;
            var lineLength = (lineEnd - lineStart).Length();
            if (lineLength <= 1f)
            {
                return;
            }

            const int segments = 12;
            const float fadeStart = 0.58f;
            for (var index = 0; index < segments; index += 1)
            {
                var segmentStartT = index / (float)segments;
                var segmentEndT = (index + 1) / (float)segments;
                var segmentStart = Vector2.Lerp(lineStart, lineEnd, segmentStartT);
                var segmentEnd = Vector2.Lerp(lineStart, lineEnd, segmentEndT);
                var fadeT = Math.Clamp((segmentEndT - fadeStart) / (1f - fadeStart), 0f, 1f);
                var alpha = MathHelper.Lerp(0.9f, 0f, fadeT);
                if (alpha <= 0.01f)
                {
                    continue;
                }

                DrawScreenLine(segmentStart, segmentEnd, Color.White * alpha, 1f);
            }
        }

        private void DrawScreenLine(Vector2 start, Vector2 end, Color color, float thickness)
        {
            var delta = end - start;
            var length = delta.Length();
            if (length <= 0f)
            {
                return;
            }

            var angle = MathF.Atan2(delta.Y, delta.X);
            _game._spriteBatch.Draw(_game._pixel, start, null, color, angle, Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
        }
    }
}
