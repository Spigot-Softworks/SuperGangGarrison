#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayAimHudController
    {
        private readonly Game1 _game;

        public GameplayAimHudController(Game1 game)
        {
            _game = game;
        }

        public void DrawSniperHud(MouseState mouse)
        {
            if (!_game._world.LocalPlayer.HasScopedSniperWeaponEquipped || !_game.GetPlayerIsSniperScoped(_game._world.LocalPlayer))
            {
                return;
            }

            var damage = _game.GetPlayerSniperRifleDamage(_game._world.LocalPlayer);
            var chargeScaleX = IsFacingLeftByAim(_game._world.LocalPlayer) ? 1f : -1f;
            if (damage < 85)
            {
                _game.TryDrawScreenSprite("ChargeS", 0, new Vector2(mouse.X + 15f * chargeScaleX, mouse.Y - 10f), Color.White * 0.25f, new Vector2(chargeScaleX, 1f));
            }
            else
            {
                _game.TryDrawScreenSprite("FullChargeS", 0, new Vector2(mouse.X + 65f * chargeScaleX, mouse.Y), Color.White, Vector2.One);
            }

            var chargeWidth = (int)MathF.Ceiling(damage * 40f / 85f);
            if (chargeWidth <= 0)
            {
                return;
            }

            _game.TryDrawScreenSpritePart("ChargeS", 1, new Rectangle(0, 0, chargeWidth, 20), new Vector2(mouse.X + 15f * chargeScaleX, mouse.Y - 10f), Color.White * 0.8f, new Vector2(chargeScaleX, 1f));
        }

        public void DrawCrosshair(MouseState mouse)
        {
            var crosshair = _game._runtimeAssets.GetSprite("CrosshairS");
            if (crosshair is null || crosshair.Frames.Count == 0)
            {
                return;
            }

            _game._spriteBatch.Draw(crosshair.Frames[0], new Vector2(mouse.X, mouse.Y), null, Color.White, 0f, crosshair.Origin.ToVector2(), Vector2.One, SpriteEffects.None, 0f);
        }
    }
}
