#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayDrawController
    {
        private readonly GameplayWorldDrawController _worldDrawController;
        private readonly GameplayOverlayDrawController _overlayDrawController;

        public GameplayDrawController(Game1 game)
        {
            _worldDrawController = new GameplayWorldDrawController(game);
            _overlayDrawController = new GameplayOverlayDrawController(game, _worldDrawController);
        }

        public void DrawFrame(GameTime gameTime)
        {
            _overlayDrawController.DrawFrame(gameTime);
        }

        public void DrawGameplayWorldForCamera(Vector2 cameraPosition, int viewportWidth, int viewportHeight, int? skippedDeadBodySourcePlayerId = null)
        {
            _worldDrawController.DrawGameplayWorldForCamera(cameraPosition, viewportWidth, viewportHeight, skippedDeadBodySourcePlayerId);
        }
    }
}
