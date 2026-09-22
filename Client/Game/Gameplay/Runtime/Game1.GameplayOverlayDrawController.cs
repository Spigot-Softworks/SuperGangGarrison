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
            if (_game.IsNetworkWorldWarmupBlockingPresentation())
            {
                var warmupMouse = _game.GetFrameMouseState();
                _game.BeginLogicalFrame(new Color(24, 32, 48));
                _game.DrawLoadingOverlay();
                if (!_game._gameplayHudHidden)
                {
                    if (_game._teamSelectOpen || _game._teamSelectAlpha > 0.02f)
                    {
                        _game.DrawTeamSelectHud();
                    }

                    if (_game._classSelectOpen || _game._classSelectAlpha > 0.02f)
                    {
                        _game.DrawClassSelectHud();
                    }

                    if (_game.ShouldDrawSoftwareMenuCursor())
                    {
                        _game.DrawSoftwareMenuCursor(warmupMouse);
                    }

                    _game.DrawVersionOverlay();
                }

                _game.EndLogicalFrame();
                return;
            }

            if (_game.IsPracticeNavigationWarmupBlockingGameplay())
            {
                _game.BeginLogicalFrame(new Color(24, 32, 48));
                _game.DrawLoadingOverlay();
                if (!_game._gameplayHudHidden)
                {
                    _game.DrawVersionOverlay();
                }

                _game.EndLogicalFrame();
                return;
            }

            var viewportWidth = _game.ViewportWidth;
            var viewportHeight = _game.ViewportHeight;
            var gameplayViewportHeight = _game.GetGameplayCameraViewportHeight(viewportHeight);
            var worldViewport = _game.GetGameplayWorldViewport(viewportWidth, gameplayViewportHeight);
            var rawMouse = _game.GetFrameRawMouseState();
            var mouse = _game.GetFrameMouseState();
            var cameraPosition = _game.GetCameraTopLeft(viewportWidth, gameplayViewportHeight, mouse.X, mouse.Y);
            _game.PrepareLastToDieDeathFocusOverlayIfNeeded(viewportWidth, viewportHeight);
            if (!_game.IsLastToDieDeathFocusPresentationActive())
            {
                _game.PrepareDeathCamCaptureIfNeeded(viewportWidth, viewportHeight);
            }

            _game.PrepareGameplayHudOpacityComposite(mouse, cameraPosition);

            _game.BeginLogicalFrame(new Color(24, 32, 48));
            if (!_game.DrawLastToDieDeathFocusOverlay(viewportWidth, viewportHeight)
                && !_game.DrawDeathCamCaptureOverlay(viewportWidth, viewportHeight))
            {
                DrawGameplayWorldForViewport(
                    cameraPosition,
                    worldViewport.X,
                    worldViewport.Y,
                    viewportWidth,
                    gameplayViewportHeight,
                    viewportHeight);
            }

            _game.DrawGameplayHudLayersOrComposite(mouse, cameraPosition);
            _game.DrawVoiceParticipants();
            _game.DrawGameplayModalOverlays(mouse, cameraPosition);
            _game.DrawVotePresentationOverlay();
            _game.DrawLoadingOverlay();
            if (!_game._gameplayHudHidden)
            {
                _game.DrawVersionOverlay();
            }
            Game1.WriteGameplayRenderTrace("frame before endlogical");
            _game.EndLogicalFrame();
            Game1.WriteGameplayRenderTrace("frame after endlogical");
            _game.DrawNavEditorPresentationOverlay(rawMouse);
            Game1.WriteGameplayRenderTrace("frame after naveditorpresentation");
        }

        private void DrawGameplayWorldForViewport(
            Vector2 cameraPosition,
            int worldViewportWidth,
            int worldViewportHeight,
            int viewportWidth,
            int gameplayViewportHeight,
            int viewportHeight)
        {
            if (!_game.IsLocalSpectatorPresentationActive() || gameplayViewportHeight >= viewportHeight)
            {
                _game.BeginGameplayWorldSpriteBatch(RasterizerState.CullNone);
                _worldDrawController.DrawGameplayWorldForCamera(cameraPosition, worldViewportWidth, worldViewportHeight);
                _game.EndGameplayWorldSpriteBatch();
                return;
            }

            var previousScissor = _game.GraphicsDevice.ScissorRectangle;
            using var scissorRasterizer = new RasterizerState
            {
                CullMode = CullMode.None,
                ScissorTestEnable = true,
            };

            _game.GraphicsDevice.ScissorRectangle = new Rectangle(0, 0, viewportWidth, gameplayViewportHeight);
            _game.BeginGameplayWorldSpriteBatch(scissorRasterizer);
            _worldDrawController.DrawGameplayWorldForCamera(cameraPosition, worldViewportWidth, worldViewportHeight);
            _game.EndGameplayWorldSpriteBatch();
            _game.GraphicsDevice.ScissorRectangle = previousScissor;
        }
    }
}
