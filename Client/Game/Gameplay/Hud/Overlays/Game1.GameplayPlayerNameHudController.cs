#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using System;

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

            var renderPosition = _game.GetRenderPosition(player);
            var bounds = GetPlayerScreenBounds(player, renderPosition, cameraPosition);
            var alpha = Math.Clamp(visibilityAlpha, 0.55f, 1f);
            var teamFillColor = player.Team == PlayerTeam.Blue ? new Color(0x48, 0x5C, 0x67) : new Color(0xA5, 0x46, 0x40);
            var teamOutlineColor = player.Team == PlayerTeam.Blue ? new Color(0x35, 0x44, 0x4D) : new Color(0x7E, 0x35, 0x30);
            var textColor = new Color(0xD9, 0xD9, 0xB7);

            const int horizontalPadding = 2;
            const int verticalPadding = 2;
            var textWidth = _game.MeasureBitmapFontWidth(label, 1f);
            var textHeight = _game.MeasureBitmapFontHeight(1f);
            var panelWidth = (int)MathF.Ceiling(textWidth) + (horizontalPadding * 2);
            var panelHeight = (int)MathF.Ceiling(textHeight) + (verticalPadding * 2);

            var panelCenterX = bounds.Left + (bounds.Width * 0.5f);
            var panelBottom = bounds.Top - 13f;
            var panelX = (int)MathF.Round(panelCenterX - (panelWidth / 2f));
            var panelY = (int)MathF.Round(panelBottom - panelHeight);
            var panelBounds = new Rectangle(panelX, panelY, panelWidth, panelHeight);

            _game.DrawRoundedRectangleOutline(panelBounds, teamFillColor * (alpha * 0.6f), teamOutlineColor * (alpha * 0.6f), outlineThickness: 2, radius: 4);

            var textPosition = new Vector2(
                panelBounds.X + ((panelBounds.Width - textWidth) / 2f),
                panelBounds.Y + verticalPadding);
            _game.DrawBitmapFontText(label, textPosition, textColor * (alpha * 0.6f), 1f);
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

                var renderPosition = _game.GetRenderPosition(player);
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
