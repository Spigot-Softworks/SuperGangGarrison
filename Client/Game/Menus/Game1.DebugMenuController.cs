#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class DebugMenuController
    {
        private readonly record struct DebugMenuRow(string Label, string Value, Action? Activate);

        private readonly Game1 _game;

        public DebugMenuController(Game1 game)
        {
            _game = game;
        }

        public void OpenDebugMenu()
        {
            _game._debugMenuOpen = true;
            _game._debugMenuAwaitingEscapeRelease = true;
            _game._debugMenuHoverIndex = -1;
        }

        public void CloseDebugMenu()
        {
            _game._debugMenuOpen = false;
            _game._debugMenuAwaitingEscapeRelease = false;
            _game._debugMenuHoverIndex = -1;
        }

        public void UpdateDebugMenu(KeyboardState keyboard, MouseState mouse)
        {
            var rows = BuildDebugMenuRows();
            GetDebugMenuLayout(rows.Count, out var _, out var rowBounds, out var rowHeight);

            if (_game._debugMenuAwaitingEscapeRelease)
            {
                if (!keyboard.IsKeyDown(Keys.Escape))
                {
                    _game._debugMenuAwaitingEscapeRelease = false;
                }
            }
            else if (_game.IsKeyPressed(keyboard, Keys.Escape))
            {
                CloseDebugMenu();
                return;
            }

            _game._debugMenuHoverIndex = -1;
            for (var index = 0; index < rowBounds.Length; index += 1)
            {
                if (rowBounds[index].Contains(mouse.Position))
                {
                    _game._debugMenuHoverIndex = index;
                    break;
                }
            }

            var clickPressed = mouse.LeftButton == ButtonState.Pressed && _game._previousMouse.LeftButton != ButtonState.Pressed;
            if (!clickPressed || _game._debugMenuHoverIndex < 0 || _game._debugMenuHoverIndex >= rows.Count)
            {
                return;
            }

            var activate = rows[_game._debugMenuHoverIndex].Activate;
            activate?.Invoke();
        }

        public void DrawDebugMenu()
        {
            var viewportWidth = _game.ViewportWidth;
            var viewportHeight = _game.ViewportHeight;
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.78f);

            var rows = BuildDebugMenuRows();
            GetDebugMenuLayout(rows.Count, out var panel, out var rowBounds, out var rowHeight);

            _game._spriteBatch.Draw(_game._pixel, panel, new Color(34, 35, 39, 235));
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(panel.X, panel.Y, panel.Width, 3), new Color(210, 210, 210));
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(panel.X, panel.Bottom - 3, panel.Width, 3), new Color(76, 76, 76));

            const float debugHeaderScale = 1.15f;
            const float debugRowTextScale = 1.39f;
            const float debugRowHorizontalPadding = 14f;
            const float debugRowColumnGap = 20f;

            _game.DrawBitmapFontText("Debug Options", new Vector2(panel.X + debugRowHorizontalPadding, panel.Y + 14f), Color.White, debugHeaderScale);

            for (var index = 0; index < rows.Count; index += 1)
            {
                var rowRect = rowBounds[index];
                var isHovered = index == _game._debugMenuHoverIndex;
                _game._spriteBatch.Draw(_game._pixel, rowRect, isHovered ? new Color(60, 60, 70) : new Color(44, 46, 52, 170));

                var textScale = debugRowTextScale;
                var textY = rowRect.Y + ((rowRect.Height - _game.MeasureBitmapFontHeight(textScale)) * 0.5f);

                var row = rows[index];
                var labelX = rowRect.X + debugRowHorizontalPadding;
                var valueRightX = rowRect.Right - debugRowHorizontalPadding;
                var trimmedValue = _game.TrimBitmapMenuText(row.Value, rowRect.Width * 0.42f, textScale);
                var valueWidth = _game.MeasureBitmapFontWidth(trimmedValue, textScale);
                var valueX = valueRightX - valueWidth;
                var labelMaxWidth = Math.Max(40f, valueX - labelX - debugRowColumnGap);
                var trimmedLabel = _game.TrimBitmapMenuText(row.Label, labelMaxWidth, textScale);

                var labelColor = row.Activate is null ? new Color(150, 150, 150) : Color.White;
                _game.DrawBitmapFontText(trimmedLabel, new Vector2(labelX, textY), labelColor, textScale);
                if (!string.IsNullOrWhiteSpace(trimmedValue))
                {
                    _game.DrawBitmapFontText(trimmedValue, new Vector2(valueX, textY), Color.White, textScale);
                }
            }
        }

        private void GetDebugMenuLayout(int rowCount, out Rectangle panel, out Rectangle[] rowBounds, out int rowHeight)
        {
            rowCount = Math.Max(1, rowCount);
            rowHeight = 48;
            var panelWidth = 400;
            var panelHeight = 60 + (rowCount * rowHeight);

            var panelX = (_game.ViewportWidth - panelWidth) / 2;
            var panelY = (_game.ViewportHeight - panelHeight) / 2;
            panel = new Rectangle(panelX, panelY, panelWidth, panelHeight);

            rowBounds = new Rectangle[rowCount];
            var currentY = panel.Y + 50;
            for (var index = 0; index < rowCount; index += 1)
            {
                rowBounds[index] = new Rectangle(panel.X, currentY, panelWidth, rowHeight - 2);
                currentY += rowHeight;
            }
        }

        private List<DebugMenuRow> BuildDebugMenuRows()
        {
            var rows = new List<DebugMenuRow>
            {
                new("Rocket Collisions", _game._debugRocketCollisionsEnabled ? "Enabled" : "Disabled", () =>
                {
                    _game._debugRocketCollisionsEnabled = !_game._debugRocketCollisionsEnabled;
                }),
                new("Back", string.Empty, CloseDebugMenu),
            };

            return rows;
        }
    }
}
