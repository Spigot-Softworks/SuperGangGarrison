#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayOverlayDrawController
    {
        private readonly Game1 _game;
        private readonly GameplayWorldDrawController _worldDrawController;

        public GameplayOverlayDrawController(Game1 game, GameplayWorldDrawController worldDrawController)
        {
            _game = game;
            _worldDrawController = worldDrawController;
        }

        public void DrawFrame(GameTime gameTime)
        {
            var viewportWidth = _game.ViewportWidth;
            var viewportHeight = _game.ViewportHeight;
            var rawMouse = _game.GetConstrainedMouseState(Mouse.GetState());
            var mouse = _game.GetScaledMouseState(rawMouse);
            var cameraPosition = _game.GetCameraTopLeft(viewportWidth, viewportHeight, mouse.X, mouse.Y);
            _game.PrepareLastToDieDeathFocusOverlayIfNeeded(viewportWidth, viewportHeight);
            if (!_game.IsLastToDieDeathFocusPresentationActive())
            {
                _game.PrepareDeathCamCaptureIfNeeded(viewportWidth, viewportHeight);
            }

            _game.BeginLogicalFrame(new Color(24, 32, 48));
            if (!_game.DrawLastToDieDeathFocusOverlay(viewportWidth, viewportHeight)
                && !_game.DrawDeathCamCaptureOverlay(viewportWidth, viewportHeight))
            {
                _worldDrawController.DrawGameplayWorldForCamera(cameraPosition, viewportWidth, viewportHeight);
            }

            _game.DrawGameplayHudLayers(mouse, cameraPosition);
            _game.DrawGameplayModalOverlays(mouse);
            Game1.WriteGameplayRenderTrace("frame before endlogical");
            _game.EndLogicalFrame();
            Game1.WriteGameplayRenderTrace("frame after endlogical");
            _game.DrawNavEditorPresentationOverlay(rawMouse);
            Game1.WriteGameplayRenderTrace("frame after naveditorpresentation");
        }
    }
}
