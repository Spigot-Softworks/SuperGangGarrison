#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class GameplayOverlayDrawController
    {
        private readonly IGameplayContext _context;

        public GameplayOverlayDrawController(IGameplayContext context)
        {
            _context = context;
        }

        public void DrawFrame(GameTime gameTime)
        {
            if (_context.IsNetworkWorldWarmupBlockingPresentation())
            {
                var warmupMouse = _context.GetFrameMouseState();
                _context.BeginLogicalFrame(new Color(24, 32, 48));
                _context.DrawLoadingOverlay();
                if (!_context._gameplayHudHidden)
                {
                    if (_context._teamSelectOpen || _context._teamSelectAlpha > 0.02f)
                    {
                        _context.DrawTeamSelectHud();
                    }

                    if (_context._classSelectOpen || _context._classSelectAlpha > 0.02f)
                    {
                        _context.DrawClassSelectHud();
                    }

                    if (_context.ShouldDrawSoftwareMenuCursor())
                    {
                        _context.DrawSoftwareMenuCursor(warmupMouse);
                    }

                    _context.DrawVersionOverlay();
                }

                _context.EndLogicalFrame();
                return;
            }

            if (_context.IsPracticeNavigationWarmupBlockingGameplay())
            {
                _context.BeginLogicalFrame(new Color(24, 32, 48));
                _context.DrawLoadingOverlay();
                if (!_context._gameplayHudHidden)
                {
                    _context.DrawVersionOverlay();
                }

                _context.EndLogicalFrame();
                return;
            }

            var viewportWidth = _context.ViewportWidth;
            var viewportHeight = _context.ViewportHeight;
            var gameplayViewportHeight = _context.GetGameplayCameraViewportHeight(viewportHeight);
            var worldViewport = _context.GetGameplayWorldViewport(viewportWidth, gameplayViewportHeight);
            var rawMouse = _context.GetFrameRawMouseState();
            var mouse = _context.GetFrameMouseState();
            var cameraPosition = _context.GetCameraTopLeft(viewportWidth, gameplayViewportHeight, mouse.X, mouse.Y);
            _context.PrepareLastToDieDeathFocusOverlayIfNeeded(viewportWidth, viewportHeight);
            if (!_context.IsLastToDieDeathFocusPresentationActive())
            {
                _context.PrepareDeathCamCaptureIfNeeded(viewportWidth, viewportHeight);
            }

            _context.PrepareGameplayHudOpacityComposite(mouse, cameraPosition);

            _context.BeginLogicalFrame(new Color(24, 32, 48));
            if (!_context.DrawLastToDieDeathFocusOverlay(viewportWidth, viewportHeight)
                && !_context.DrawDeathCamCaptureOverlay(viewportWidth, viewportHeight))
            {
                DrawGameplayWorldForViewport(
                    cameraPosition,
                    worldViewport.X,
                    worldViewport.Y,
                    viewportWidth,
                    gameplayViewportHeight,
                    viewportHeight);
            }

            _context.DrawGameplayHudLayersOrComposite(mouse, cameraPosition);
            _context.DrawVoiceParticipants();
            _context.DrawGameplayModalOverlays(mouse, cameraPosition);
            _context.DrawVotePresentationOverlay();
            _context.DrawLoadingOverlay();
            if (!_context._gameplayHudHidden)
            {
                _context.DrawVersionOverlay();
            }
            Game1.WriteGameplayRenderTrace("frame before endlogical");
            _context.EndLogicalFrame();
            Game1.WriteGameplayRenderTrace("frame after endlogical");
        }

        private void DrawGameplayWorldForViewport(
            Vector2 cameraPosition,
            int worldViewportWidth,
            int worldViewportHeight,
            int viewportWidth,
            int gameplayViewportHeight,
            int viewportHeight)
        {
            if (!_context.IsLocalSpectatorPresentationActive() || gameplayViewportHeight >= viewportHeight)
            {
                _context.BeginGameplayWorldSpriteBatch(RasterizerState.CullNone);
                _context.Gameplay.WorldDraw.DrawGameplayWorldForCamera(cameraPosition, worldViewportWidth, worldViewportHeight);
                _context.EndGameplayWorldSpriteBatch();
                return;
            }

            var previousScissor = _context.GraphicsDevice.ScissorRectangle;
            using var scissorRasterizer = new RasterizerState
            {
                CullMode = CullMode.None,
                ScissorTestEnable = true,
            };

            _context.GraphicsDevice.ScissorRectangle = new Rectangle(0, 0, viewportWidth, gameplayViewportHeight);
            _context.BeginGameplayWorldSpriteBatch(scissorRasterizer);
            _context.Gameplay.WorldDraw.DrawGameplayWorldForCamera(cameraPosition, worldViewportWidth, worldViewportHeight);
            _context.EndGameplayWorldSpriteBatch();
            _context.GraphicsDevice.ScissorRectangle = previousScissor;
        }
}
