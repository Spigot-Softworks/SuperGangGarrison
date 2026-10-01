#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using System;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class GameplayPlayerNameHudController
    {
        private readonly IHudContext _context;

        public GameplayPlayerNameHudController(IHudContext context)
        {
            _context = context;
        }

        public void DrawPersistentSelfNameHud(Vector2 cameraPosition)
        {
            if (!_context.HudRuntimeSettings.ShowPlayerNamesEnabled
                || !_context.HudRuntimeSettings.ShowPersistentSelfNameEnabled
                || _context.IsLocalSpectatorPresentationActive()
                || !_context._world.LocalPlayer.IsAlive)
            {
                return;
            }

            DrawPlayerNameHud(_context._world.LocalPlayer, cameraPosition);
        }

        public void DrawForcedPlayerNameHuds(Vector2 cameraPosition)
        {
            if (!_context.HudRuntimeSettings.ShowPlayerNamesEnabled)
            {
                return;
            }

            foreach (var player in _context.EnumerateRenderablePlayers())
            {
                if (!player.IsAlive
                    || ReferenceEquals(player, _context._world.LocalPlayer)
                    || !Game1.ShouldForceMapBotNameplate(player))
                {
                    continue;
                }

                DrawPlayerNameHud(player, cameraPosition, forceVisible: true);
            }
        }

        public void DrawHoveredPlayerNameHud(MouseState mouse, Vector2 cameraPosition)
        {
            if (!_context.HudRuntimeSettings.ShowPlayerNamesEnabled)
            {
                return;
            }

            var hoveredPlayer = GetHoveredPlayerForNameHud(mouse, cameraPosition);
            if (hoveredPlayer is null)
            {
                return;
            }

            if (_context.HudRuntimeSettings.ShowPersistentSelfNameEnabled && ReferenceEquals(hoveredPlayer, _context._world.LocalPlayer))
            {
                return;
            }

            if (Game1.ShouldForceMapBotNameplate(hoveredPlayer))
            {
                return;
            }

            DrawPlayerNameHud(hoveredPlayer, cameraPosition);
        }

        public void DrawPlayerNameHud(PlayerEntity player, Vector2 cameraPosition, bool forceVisible = false)
        {
            if (!_context.HasFreshPlayerRenderHistory(player))
            {
                return;
            }

            var label = GetHudPlayerLabel(player);
            if (string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            var visibilityAlpha = forceVisible ? 1f : _context.GetPlayerVisibilityAlpha(player);
            if (visibilityAlpha <= 0f)
            {
                return;
            }

            var renderPosition = _context.GetRenderPosition(player);
            var bounds = _context.GetPlayerHudScreenBounds(player, renderPosition, cameraPosition);
            var alpha = Math.Clamp(visibilityAlpha, 0.55f, 1f);
            var teamFillColor = player.Team == PlayerTeam.Blue ? new Color(0x48, 0x5C, 0x67) : new Color(0xA5, 0x46, 0x40);
            var teamOutlineColor = player.Team == PlayerTeam.Blue ? new Color(0x35, 0x44, 0x4D) : new Color(0x7E, 0x35, 0x30);
            var textColor = new Color(0xD9, 0xD9, 0xB7);
            PlayerServerTitleState? serverTitle = null;
            if (_context.TryGetScoreboardPlayerNetworkSlot(player, out var slot)
                && _context.TryGetOnlinePlayerServerTitle(slot, out var resolvedTitle))
            {
                serverTitle = resolvedTitle;
            }

            const int horizontalPadding = 2;
            const int verticalPadding = 2;
            var titleWidth = serverTitle is null ? 0f : _context.MeasureServerPlayerTitle(serverTitle, 1f);
            var textWidth = titleWidth + _context.MeasureBitmapFontWidth(label, 1f);
            var textHeight = _context.MeasureBitmapFontHeight(1f);
            var panelWidth = (int)MathF.Ceiling(textWidth) + (horizontalPadding * 2);
            var panelHeight = (int)MathF.Ceiling(textHeight) + (verticalPadding * 2);

            var panelCenterX = bounds.Left + (bounds.Width * 0.5f);
            var panelBottom = bounds.Top - 13f;
            var panelX = (int)MathF.Round(panelCenterX - (panelWidth / 2f));
            var panelY = (int)MathF.Round(panelBottom - panelHeight);
            var panelBounds = new Rectangle(panelX, panelY, panelWidth, panelHeight);

            _context.DrawRoundedRectangleOutline(panelBounds, teamFillColor * (alpha * 0.6f), teamOutlineColor * (alpha * 0.6f), outlineThickness: 2, radius: 4);

            var textPosition = new Vector2(
                panelBounds.X + ((panelBounds.Width - textWidth) / 2f),
                panelBounds.Y + verticalPadding);
            var textX = textPosition.X;
            if (serverTitle is not null)
            {
                textX = _context.DrawServerPlayerTitle(serverTitle, textPosition, alpha * 0.6f, 1f);
            }
            _context.DrawBitmapFontText(label, new Vector2(textX, textPosition.Y), textColor * (alpha * 0.6f), 1f);
        }

        public PlayerEntity? GetHoveredPlayerForNameHud(MouseState mouse, Vector2 cameraPosition)
        {
            const float hoverRadius = 25f;
            var bestDistanceSquared = hoverRadius * hoverRadius;
            PlayerEntity? hoveredPlayer = null;

            foreach (var player in _context.EnumerateRenderablePlayers())
            {
                if (!player.IsAlive || _context.GetPlayerVisibilityAlpha(player) <= 0f || _context.IsSpyHiddenFromLocalViewer(player))
                {
                    continue;
                }

                var renderPosition = _context.GetRenderPosition(player);
                var screenPosition = _context.GetWorldHudScreenPosition(renderPosition, cameraPosition);
                var deltaX = screenPosition.X - mouse.X;
                var deltaY = screenPosition.Y - mouse.Y;
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
