#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
            var gameplayViewportHeight = _game.GetGameplayCameraViewportHeight(viewportHeight);
            var rawMouse = _game.GetConstrainedMouseState(Game1.GetCurrentMouseState());
            var mouse = _game.GetScaledMouseState(rawMouse);
            var cameraPosition = _game.GetCameraTopLeft(viewportWidth, gameplayViewportHeight, mouse.X, mouse.Y);
            _game.PrepareLastToDieDeathFocusOverlayIfNeeded(viewportWidth, viewportHeight);
            if (!_game.IsLastToDieDeathFocusPresentationActive())
            {
                _game.PrepareDeathCamCaptureIfNeeded(viewportWidth, viewportHeight);
            }

            _game.BeginLogicalFrame(new Color(24, 32, 48));
            if (!_game.DrawLastToDieDeathFocusOverlay(viewportWidth, viewportHeight)
                && !_game.DrawDeathCamCaptureOverlay(viewportWidth, viewportHeight))
            {
                DrawGameplayWorldForViewport(cameraPosition, viewportWidth, gameplayViewportHeight, viewportHeight);
            }

            _game.DrawGameplayHudLayers(mouse, cameraPosition);
            _game.DrawGameplayModalOverlays(mouse);
            Game1.WriteGameplayRenderTrace("frame before endlogical");
            _game.EndLogicalFrame();
            Game1.WriteGameplayRenderTrace("frame after endlogical");
            _game.DrawNavEditorPresentationOverlay(rawMouse);
            Game1.WriteGameplayRenderTrace("frame after naveditorpresentation");
        }

        private void DrawGameplayWorldForViewport(Vector2 cameraPosition, int viewportWidth, int gameplayViewportHeight, int viewportHeight)
        {
            if (!_game._networkClient.IsSpectator || gameplayViewportHeight >= viewportHeight)
            {
                _worldDrawController.DrawGameplayWorldForCamera(cameraPosition, viewportWidth, gameplayViewportHeight);
                return;
            }

            _game._spriteBatch.End();
            var previousScissor = _game.GraphicsDevice.ScissorRectangle;
            using var scissorRasterizer = new RasterizerState
            {
                CullMode = CullMode.None,
                ScissorTestEnable = true,
            };

            _game.GraphicsDevice.ScissorRectangle = new Rectangle(0, 0, viewportWidth, gameplayViewportHeight);
            _game._spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: scissorRasterizer);
            _worldDrawController.DrawGameplayWorldForCamera(cameraPosition, viewportWidth, gameplayViewportHeight);
            _game._spriteBatch.End();
            _game.GraphicsDevice.ScissorRectangle = previousScissor;
            _game._spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
        }
    }
}
