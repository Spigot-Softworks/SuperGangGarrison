#nullable enable

using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayWorldDrawController
    {
        private readonly Game1 _game;

        public GameplayWorldDrawController(Game1 game)
        {
            _game = game;
        }

        public void DrawGameplayWorldForCamera(Vector2 cameraPosition, int viewportWidth, int viewportHeight, int? skippedDeadBodySourcePlayerId = null)
        {
            var worldRectangle = new Rectangle(
                (int)-cameraPosition.X,
                (int)-cameraPosition.Y,
                (int)_game._world.Bounds.Width,
                (int)_game._world.Bounds.Height);

            var playerRectangle = _game.GetLocalPlayerRectangle(cameraPosition);

            var mapCenterX = (int)(_game._world.Bounds.Width / 2f - cameraPosition.X);
            var mapCenterY = (int)(_game._world.Bounds.Height / 2f - cameraPosition.Y);
            var centerLine = new Rectangle(mapCenterX - 1, 0, 2, viewportHeight);
            var centerColumn = new Rectangle(0, mapCenterY - 1, viewportWidth, 2);
            var worldTopBorder = new Rectangle(worldRectangle.X, worldRectangle.Y, worldRectangle.Width, 4);
            var worldBottomBorder = new Rectangle(worldRectangle.X, worldRectangle.Bottom - 4, worldRectangle.Width, 4);
            var worldLeftBorder = new Rectangle(worldRectangle.X, worldRectangle.Y, 4, worldRectangle.Height);
            var worldRightBorder = new Rectangle(worldRectangle.Right - 4, worldRectangle.Y, 4, worldRectangle.Height);
            var spawnRectangle = new Rectangle(
                (int)(_game._world.Level.LocalSpawn.X - 8f - cameraPosition.X),
                (int)(_game._world.Level.LocalSpawn.Y - 8f - cameraPosition.Y),
                16,
                16);

            _game.DrawGameplayWorld(
                cameraPosition,
                worldRectangle,
                playerRectangle,
                centerLine,
                centerColumn,
                worldTopBorder,
                worldBottomBorder,
                worldLeftBorder,
                worldRightBorder,
                spawnRectangle,
                skippedDeadBodySourcePlayerId);
        }
    }
}
