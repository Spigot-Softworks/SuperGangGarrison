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
            _game.UpdateBotBrainCorridorRecorderHotkeys(keyboard);
            var cameraPosition = _game.GetCameraTopLeft(_game.ViewportWidth, _game.ViewportHeight, mouse.X, mouse.Y);
            _game.UpdateGarrisonBuilderEditor(keyboard, rawMouse, (float)gameTime.ElapsedGameTime.TotalSeconds);
            _game.UpdateNavEditor(keyboard, mouse, rawMouse, cameraPosition, (float)gameTime.ElapsedGameTime.TotalSeconds);
            var (gameplayInput, networkInput) = _game.BuildGameplayInputs(keyboard, mouse, cameraPosition);
            _game._latestLocalAimWorldX = cameraPosition.X + mouse.X;
            _game._latestLocalAimWorldY = cameraPosition.Y + mouse.Y;
            _game._hasLatestLocalAimWorldPosition = true;
            _game.SetNavEditorTraversalCaptureInput(gameplayInput);
            _game.SetScoreRouteRecorderCaptureInput(gameplayInput);
            _game.ResolveNavEditorGameplayInput(gameplayInput);
            _game.CapturePendingPredictedInputEdges(keyboard, mouse, networkInput);
            if (_game.IsGameplayInputBlocked())
            {
                _game.ClearPendingSecondaryAbilityPress();
            }

            networkInput = _game.ApplyPendingInputEdges(networkInput);
            // Use networkInput for local input state (needed for medic beam visuals, etc.)
            // even though gameplayInput is default in multiplayer (server authoritative)
            _game._world.SetLocalInput(networkInput);
            _game.UpdateBubbleMenuState(keyboard, mouse);
            _game.UpdateScoreboardState(keyboard);
            return networkInput;
        }
    }
}
