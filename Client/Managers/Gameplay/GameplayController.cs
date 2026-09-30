#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class GameplayController
    {
        private readonly IGameplayContext _context;

        public GameplayController(IGameplayContext context)
        {
            _context = context;
        }

        public void UpdateFrame(GameTime gameTime, KeyboardState keyboard, MouseState mouse, MouseState rawMouse, int clientTicks)
        {
            _context.Gameplay.Update.UpdateFrame(gameTime, keyboard, mouse, rawMouse, clientTicks);
        }

        public void DrawFrame(GameTime gameTime)
        {
            _context.Gameplay.Draw.DrawFrame(gameTime);
        }

        public void DrawGameplayWorldForCamera(Vector2 cameraPosition, int viewportWidth, int viewportHeight, int? skippedDeadBodySourcePlayerId = null)
        {
            _context.Gameplay.Draw.DrawGameplayWorldForCamera(cameraPosition, viewportWidth, viewportHeight, skippedDeadBodySourcePlayerId);
        }
}
