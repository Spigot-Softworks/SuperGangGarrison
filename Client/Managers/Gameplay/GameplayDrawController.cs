#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class GameplayDrawController
    {
        private readonly IGameplayContext _context;

        public GameplayDrawController(IGameplayContext context)
        {
            _context = context;
        }

        public void DrawFrame(GameTime gameTime)
        {
            _context.Gameplay.OverlayDraw.DrawFrame(gameTime);
        }

        public void DrawGameplayWorldForCamera(Vector2 cameraPosition, int viewportWidth, int viewportHeight, int? skippedDeadBodySourcePlayerId = null)
        {
            _context.Gameplay.WorldDraw.DrawGameplayWorldForCamera(cameraPosition, viewportWidth, viewportHeight, skippedDeadBodySourcePlayerId);
        }
}
