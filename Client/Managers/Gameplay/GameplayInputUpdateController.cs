#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class GameplayInputUpdateController
    {
        private readonly IGameplayContext _context;

        public GameplayInputUpdateController(IGameplayContext context)
        {
            _context = context;
        }

        public PlayerInputSnapshot PrepareFrame(GameTime gameTime, KeyboardState keyboard, MouseState mouse, MouseState rawMouse)
        {
            // Closing an overlay must not pass that same Escape/click through
            // to the hosted choice screen underneath it later in this frame.
            _context._gameplayModalOwnedInputThisFrame = _context.HasGameplayModalInputOwner();
            var wasGameplayInputBlocked = _context.IsGameplayInputBlocked();
            _context.UpdateGameplayScreenState(keyboard, mouse);
            _context.TryHandleVoteShortcut(keyboard, mouse);
            _context.UpdateGameplayMenuState(keyboard, mouse);
            _context.UpdateReplayPlaybackControls(keyboard, mouse);
            _context.SuppressMouseFireAfterGameplayInputUnblocks(wasGameplayInputBlocked, mouse);
            if (mouse.MiddleButton == ButtonState.Pressed
                && _context._previousMouse.MiddleButton != ButtonState.Pressed
                && !_context.IsGameplayInputBlocked())
            {
                _context.CycleGameplayCameraZoom();
            }

            _context.UpdateRespawnCameraState((float)gameTime.ElapsedGameTime.TotalSeconds, keyboard, mouse);
            _context.UpdateHitboxDebugHotkey(keyboard);
            _context.UpdateBotBrainCorridorRecorderHotkeys(keyboard);
            var gameplayCameraViewportHeight = _context.GetGameplayCameraViewportHeight(_context.ViewportHeight);
            var cameraPosition = _context.GetGameplayInputCameraTopLeft(
                _context.ViewportWidth,
                gameplayCameraViewportHeight,
                mouse.X,
                mouse.Y);
            var aimOrigin = _context.GetGameplayInputAimOrigin();
            if (_context._networkClient.IsLegacyGg2Connection)
            {
                // GG2 encodes mouse angle and distance from the center of the
                // current view, including the camera offset near map edges.
                var worldViewport = _context.GetGameplayWorldViewport(
                    _context.ViewportWidth, gameplayCameraViewportHeight);
                aimOrigin = cameraPosition + new Vector2(
                    worldViewport.X / 2f, worldViewport.Y / 2f);
            }
            _context._latestNetworkInputAimOriginX = aimOrigin.X;
            _context._latestNetworkInputAimOriginY = aimOrigin.Y;
            _context._hasLatestNetworkInputAimOrigin = true;
            _context.UpdateGarrisonBuilderEditor(keyboard, mouse, (float)gameTime.ElapsedGameTime.TotalSeconds);
            _context.UpdateScoreboardState(keyboard, mouse);
            var (gameplayInput, networkInput) = _context.BuildGameplayInputs(keyboard, mouse, cameraPosition, (float)gameTime.ElapsedGameTime.TotalSeconds);
            _context.SetScoreRouteRecorderCaptureInput(gameplayInput);
            _context._latestLocalAimWorldX = networkInput.AimWorldX;
            _context._latestLocalAimWorldY = networkInput.AimWorldY;
            _context._hasLatestLocalAimWorldPosition = true;
            _context.CapturePendingPredictedInputEdges(keyboard, mouse, networkInput);
            if (_context.IsGameplayInputBlocked())
            {
                _context.ClearPendingSecondaryAbilityPress();
            }

            networkInput = _context.ApplyPendingInputEdges(networkInput);
            // Use networkInput for local input state (needed for medic beam visuals, etc.)
            // even though gameplayInput is default in multiplayer (server authoritative)
            _context._world.SetLocalInput(networkInput);
            _context.UpdateBubbleMenuState(keyboard, mouse);
            _context.UpdateCustomBubbleHotkey(keyboard, mouse);
            return networkInput;
        }
}
