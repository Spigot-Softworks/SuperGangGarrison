#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool IsRespawnFreeCameraActive()
    {
        return ShouldBlockGameplayForNavEditor()
            || _networkClient.IsSpectator
            || (!_world.LocalPlayerAwaitingJoin
                && !_world.LocalPlayer.IsAlive
                && _world.LocalDeathCam is null);
    }

    private void UpdateRespawnCameraState(float deltaSeconds, KeyboardState keyboard)
    {
        if (!IsRespawnFreeCameraActive())
        {
            _respawnCameraDetached = false;
            _respawnCameraCenter = GetDefaultFreeCameraCenter();
            return;
        }

        if (!_respawnCameraDetached)
        {
            _respawnCameraCenter = GetDefaultFreeCameraCenter();
        }

        if (ShouldBlockGameplayForNavEditor() || !IsGameplayInputBlocked())
        {
            var moveAmount = 600f * deltaSeconds;
            var moved = false;

            if (keyboard.IsKeyDown(_inputBindings.MoveLeft))
            {
                _respawnCameraCenter.X -= moveAmount;
                moved = true;
            }
            else if (keyboard.IsKeyDown(_inputBindings.MoveRight))
            {
                _respawnCameraCenter.X += moveAmount;
                moved = true;
            }

            if (keyboard.IsKeyDown(_inputBindings.MoveUp))
            {
                _respawnCameraCenter.Y -= moveAmount;
                moved = true;
            }
            else if (keyboard.IsKeyDown(_inputBindings.MoveDown))
            {
                _respawnCameraCenter.Y += moveAmount;
                moved = true;
            }

            if (moved)
            {
                if (_networkClient.IsSpectator && _spectatorTrackingEnabled)
                {
                    _spectatorTrackingEnabled = false;
                    _spectatorTrackedPlayerId = null;
                    ShowNotice(NoticeKind.PlayerTrackDisable);
                }

                _respawnCameraDetached = true;
            }
        }

        _respawnCameraCenter = ClampRespawnCameraCenter(_respawnCameraCenter);
    }

    private Vector2 ClampRespawnCameraCenter(Vector2 position)
    {
        var halfViewportWidth = ViewportWidth / 2f;
        var halfViewportHeight = ViewportHeight / 2f;
        var maxX = System.Math.Max(halfViewportWidth, _world.Bounds.Width - halfViewportWidth);
        var maxY = System.Math.Max(halfViewportHeight, _world.Bounds.Height - halfViewportHeight);
        return new Vector2(
            System.Math.Clamp(position.X, halfViewportWidth, maxX),
            System.Math.Clamp(position.Y, halfViewportHeight, maxY));
    }
}
