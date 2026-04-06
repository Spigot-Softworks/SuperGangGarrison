#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayPlayerNameHudController
    {
        private readonly Game1 _game;

        public GameplayPlayerNameHudController(Game1 game)
        {
            _game = game;
        }

        public void DrawPersistentSelfNameHud(Vector2 cameraPosition)
        {
            if (!_game._showPersistentSelfNameEnabled || _game._networkClient.IsSpectator || !_game._world.LocalPlayer.IsAlive)
            {
                return;
            }

            DrawPlayerNameHud(_game._world.LocalPlayer, cameraPosition);
        }

        public void DrawHoveredPlayerNameHud(MouseState mouse, Vector2 cameraPosition)
        {
            var hoveredPlayer = GetHoveredPlayerForNameHud(mouse, cameraPosition);
            if (hoveredPlayer is null)
            {
                return;
            }

            if (_game._showPersistentSelfNameEnabled && ReferenceEquals(hoveredPlayer, _game._world.LocalPlayer))
            {
                return;
            }

            DrawPlayerNameHud(hoveredPlayer, cameraPosition);
        }

        public void DrawPlayerNameHud(PlayerEntity player, Vector2 cameraPosition)
        {
            var label = GetHudPlayerLabel(player);
            if (string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            var visibilityAlpha = _game.GetPlayerVisibilityAlpha(player);
            if (visibilityAlpha <= 0f)
            {
                return;
            }

            var renderPosition = _game.GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _game._world.LocalPlayer));
            var bounds = GetPlayerScreenBounds(player, renderPosition, cameraPosition);
            var screenPosition = new Vector2(bounds.Left + (bounds.Width * 0.5f), bounds.Top - 13f);
            var textHeight = _game.MeasureBitmapFontHeight(1f);
            var labelPosition = new Vector2(screenPosition.X, screenPosition.Y - textHeight);
            var teamColor = player.Team == PlayerTeam.Blue ? new Color(80, 150, 240) : new Color(210, 90, 90);
            var alpha = Math.Clamp(visibilityAlpha, 0.55f, 1f);
            _game.DrawBitmapFontTextCentered(label, labelPosition + Vector2.One, Color.Black * alpha, 1f);
            _game.DrawBitmapFontTextCentered(label, labelPosition, teamColor * alpha, 1f);
        }

        public PlayerEntity? GetHoveredPlayerForNameHud(MouseState mouse, Vector2 cameraPosition)
        {
            const float hoverRadius = 25f;
            var bestDistanceSquared = hoverRadius * hoverRadius;
            PlayerEntity? hoveredPlayer = null;

            foreach (var player in _game.EnumerateRenderablePlayers())
            {
                if (!player.IsAlive || _game.GetPlayerVisibilityAlpha(player) <= 0f || _game.IsSpyHiddenFromLocalViewer(player))
                {
                    continue;
                }

                var renderPosition = _game.GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _game._world.LocalPlayer));
                var deltaX = (renderPosition.X - cameraPosition.X) - mouse.X;
                var deltaY = (renderPosition.Y - cameraPosition.Y) - mouse.Y;
                var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
                if (distanceSquared > bestDistanceSquared)
                {
                    continue;
                }

                bestDistanceSquared = distanceSquared;
                hoveredPlayer = player;
            }

            return hoveredPlayer;
        }
    }
}
