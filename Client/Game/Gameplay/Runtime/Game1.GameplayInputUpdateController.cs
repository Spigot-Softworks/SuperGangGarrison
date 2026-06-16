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
            var wasGameplayInputBlocked = _game.IsGameplayInputBlocked();
            _game.UpdateGameplayScreenState(keyboard, mouse);
            _game.UpdateGameplayMenuState(keyboard, mouse);
            _game.SuppressMouseFireAfterGameplayInputUnblocks(wasGameplayInputBlocked, mouse);
            _game.UpdateRespawnCameraState((float)gameTime.ElapsedGameTime.TotalSeconds, keyboard, mouse);
            _game.UpdateBotBrainCorridorRecorderHotkeys(keyboard);
            var cameraPosition = _game.GetGameplayInputCameraTopLeft(
                _game.ViewportWidth,
                _game.GetGameplayCameraViewportHeight(_game.ViewportHeight),
                mouse.X,
                mouse.Y);
            var gameplayCameraViewportHeight = _game.GetGameplayCameraViewportHeight(_game.ViewportHeight);
            _game._latestNetworkInputAimOriginX = cameraPosition.X + (_game.ViewportWidth / 2f);
            _game._latestNetworkInputAimOriginY = cameraPosition.Y + (gameplayCameraViewportHeight / 2f);
            _game._hasLatestNetworkInputAimOrigin = true;
            _game.UpdateGarrisonBuilderEditor(keyboard, mouse, (float)gameTime.ElapsedGameTime.TotalSeconds);
            _game.UpdateNavEditor(keyboard, mouse, rawMouse, cameraPosition, (float)gameTime.ElapsedGameTime.TotalSeconds);
            _game.UpdateScoreboardState(keyboard, mouse);
            var (gameplayInput, networkInput) = _game.BuildGameplayInputs(keyboard, mouse, cameraPosition, (float)gameTime.ElapsedGameTime.TotalSeconds);
            _game._latestLocalAimWorldX = networkInput.AimWorldX;
            _game._latestLocalAimWorldY = networkInput.AimWorldY;
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
            _game.UpdateCustomBubbleHotkey(keyboard, mouse);
            return networkInput;
        }
    }
}
