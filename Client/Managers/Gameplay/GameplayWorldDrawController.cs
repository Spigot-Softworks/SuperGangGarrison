#nullable enable

using Microsoft.Xna.Framework;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class GameplayWorldDrawController
    {
        private readonly IGameplayContext _context;

        public GameplayWorldDrawController(IGameplayContext context)
        {
            _context = context;
        }

        public void DrawGameplayWorldForCamera(Vector2 cameraPosition, int viewportWidth, int viewportHeight, int? skippedDeadBodySourcePlayerId = null)
        {
            var worldLeft = (int)MathF.Floor(-cameraPosition.X);
            var worldTop = (int)MathF.Floor(-cameraPosition.Y);
            var worldRight = (int)MathF.Ceiling(_context._world.Bounds.Width - cameraPosition.X);
            var worldBottom = (int)MathF.Ceiling(_context._world.Bounds.Height - cameraPosition.Y);
            var worldRectangle = new Rectangle(
                worldLeft,
                worldTop,
                Math.Max(0, worldRight - worldLeft),
                Math.Max(0, worldBottom - worldTop));

            var playerRectangle = _context.GetLocalPlayerRectangle(cameraPosition);

            var mapCenterX = (int)(_context._world.Bounds.Width / 2f - cameraPosition.X);
            var mapCenterY = (int)(_context._world.Bounds.Height / 2f - cameraPosition.Y);
            var centerLine = new Rectangle(mapCenterX - 1, 0, 2, viewportHeight);
            var centerColumn = new Rectangle(0, mapCenterY - 1, viewportWidth, 2);
            var worldTopBorder = new Rectangle(worldRectangle.X, worldRectangle.Y, worldRectangle.Width, 4);
            var worldBottomBorder = new Rectangle(worldRectangle.X, worldRectangle.Bottom - 4, worldRectangle.Width, 4);
            var worldLeftBorder = new Rectangle(worldRectangle.X, worldRectangle.Y, 4, worldRectangle.Height);
            var worldRightBorder = new Rectangle(worldRectangle.Right - 4, worldRectangle.Y, 4, worldRectangle.Height);
            var spawnRectangle = new Rectangle(
                (int)(_context._world.Level.LocalSpawn.X - 8f - cameraPosition.X),
                (int)(_context._world.Level.LocalSpawn.Y - 8f - cameraPosition.Y),
                16,
                16);

            _context.DrawGameplayWorld(
                cameraPosition,
                viewportWidth,
                viewportHeight,
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
