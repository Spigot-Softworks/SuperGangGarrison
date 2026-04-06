#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayController
    {
        private readonly GameplayUpdateController _updateController;
        private readonly GameplayDrawController _drawController;

        public GameplayController(Game1 game)
        {
            _updateController = new GameplayUpdateController(game);
            _drawController = new GameplayDrawController(game);
        }

        public void UpdateFrame(GameTime gameTime, KeyboardState keyboard, MouseState mouse, MouseState rawMouse, int clientTicks)
        {
            _updateController.UpdateFrame(gameTime, keyboard, mouse, rawMouse, clientTicks);
        }

        public void DrawFrame(GameTime gameTime)
        {
            _drawController.DrawFrame(gameTime);
        }

        public void DrawGameplayWorldForCamera(Vector2 cameraPosition, int viewportWidth, int viewportHeight, int? skippedDeadBodySourcePlayerId = null)
        {
            _drawController.DrawGameplayWorldForCamera(cameraPosition, viewportWidth, viewportHeight, skippedDeadBodySourcePlayerId);
        }
    }
}
