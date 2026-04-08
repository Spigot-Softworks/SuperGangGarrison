#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayInputUpdateController
    {
        private readonly Game1 _game;

        public GameplayInputUpdateController(Game1 game)
        {
            _game = game;
        }

        public PlayerInputSnapshot PrepareFrame(GameTime gameTime, KeyboardState keyboard, MouseState mouse, MouseState rawMouse)
        {
            _game.UpdateGameplayScreenState(keyboard, mouse);
            _game.UpdateGameplayMenuState(keyboard, mouse);
            _game.UpdateRespawnCameraState((float)gameTime.ElapsedGameTime.TotalSeconds, keyboard);
            var cameraPosition = _game.GetCameraTopLeft(_game.ViewportWidth, _game.ViewportHeight, mouse.X, mouse.Y);
            _game.UpdateNavEditor(keyboard, mouse, rawMouse, cameraPosition, (float)gameTime.ElapsedGameTime.TotalSeconds);
            var (gameplayInput, networkInput) = _game.BuildGameplayInputs(keyboard, mouse, cameraPosition);
            _game.SetNavEditorTraversalCaptureInput(gameplayInput);
            var localGameplayInput = _game.ResolveNavEditorGameplayInput(gameplayInput);
            _game.CapturePendingPredictedInputEdges(keyboard, mouse, networkInput);
            _game._world.SetLocalInput(localGameplayInput);
            _game.UpdateBubbleMenuState(keyboard, mouse);
            _game.UpdateScoreboardState(keyboard);
            return networkInput;
        }
    }
}
