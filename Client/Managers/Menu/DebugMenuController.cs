#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using System;
using System.Collections.Generic;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class DebugMenuController
    {
        private readonly record struct DebugMenuRow(string Label, string Value, Action? Activate);

        private readonly IMenuContext _context;

        public DebugMenuController(IMenuContext context)
        {
            _context = context;
        }

        public void OpenDebugMenu()
        {
            _context._debugMenuOpen = true;
            _context._debugMenuAwaitingEscapeRelease = true;
            _context._debugMenuHoverIndex = -1;
        }

        public void CloseDebugMenu()
        {
            _context._debugMenuOpen = false;
            _context._debugMenuAwaitingEscapeRelease = false;
            _context._debugMenuHoverIndex = -1;
        }

        public void UpdateDebugMenu(KeyboardState keyboard, MouseState mouse)
        {
            var rows = BuildDebugMenuRows();
            GetDebugMenuLayout(rows.Count, out var _, out var rowBounds, out var rowHeight);

            if (_context._debugMenuAwaitingEscapeRelease)
            {
                if (!keyboard.IsKeyDown(Keys.Escape))
                {
                    _context._debugMenuAwaitingEscapeRelease = false;
                }
            }
            else if (_context.IsKeyPressed(keyboard, Keys.Escape) || _context.IsControllerMenuBackPressed())
            {
                CloseDebugMenu();
                return;
            }

            if (_context.TryConsumeControllerMenuNavigation(out _, out var verticalStep) && verticalStep != 0)
            {
                _context._debugMenuHoverIndex = MoveControllerMenuSelection(
                    _context._debugMenuHoverIndex,
                    rows.Count,
                    verticalStep);
            }
            else if (_context.IsControllerMenuInputActive())
            {
                if (_context._debugMenuHoverIndex < 0 && rows.Count > 0)
                {
                    _context._debugMenuHoverIndex = 0;
                }
            }
            else
            {
                _context._debugMenuHoverIndex = -1;
            }

            if (_context.ShouldUseMouseMenuHover(mouse))
            {
                _context._debugMenuHoverIndex = -1;
                for (var index = 0; index < rowBounds.Length; index += 1)
                {
                    if (rowBounds[index].Contains(mouse.Position))
                    {
                        _context._debugMenuHoverIndex = index;
                        break;
                    }
                }
            }

            if (_context.IsControllerMenuConfirmPressed()
                && _context._debugMenuHoverIndex >= 0
                && _context._debugMenuHoverIndex < rows.Count)
            {
                rows[_context._debugMenuHoverIndex].Activate?.Invoke();
                return;
            }

            var clickPressed = mouse.LeftButton == ButtonState.Pressed && _context._previousMouse.LeftButton != ButtonState.Pressed;
            if (!clickPressed || _context._debugMenuHoverIndex < 0 || _context._debugMenuHoverIndex >= rows.Count)
            {
                return;
            }

            var activate = rows[_context._debugMenuHoverIndex].Activate;
            activate?.Invoke();
        }

        public void DrawDebugMenu()
        {
            var viewportWidth = _context.ViewportWidth;
            var viewportHeight = _context.ViewportHeight;
            _context._spriteBatch.Draw(_context._pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.78f);

            var rows = BuildDebugMenuRows();
            GetDebugMenuLayout(rows.Count, out var panel, out var rowBounds, out var rowHeight);

            _context._spriteBatch.Draw(_context._pixel, panel, new Color(34, 35, 39, 235));
            _context._spriteBatch.Draw(_context._pixel, new Rectangle(panel.X, panel.Y, panel.Width, 3), new Color(210, 210, 210));
            _context._spriteBatch.Draw(_context._pixel, new Rectangle(panel.X, panel.Bottom - 3, panel.Width, 3), new Color(76, 76, 76));

            const float debugHeaderScale = 1.15f;
            const float debugRowTextScale = 1.39f;
            const float debugRowHorizontalPadding = 14f;
            const float debugRowColumnGap = 20f;

            _context.DrawBitmapFontText("Debug Options", new Vector2(panel.X + debugRowHorizontalPadding, panel.Y + 14f), Color.White, debugHeaderScale);

            for (var index = 0; index < rows.Count && index < rowBounds.Length; index += 1)
            {
                var rowRect = rowBounds[index];
                var isHovered = index == _context._debugMenuHoverIndex;
                _context._spriteBatch.Draw(_context._pixel, rowRect, isHovered ? new Color(60, 60, 70) : new Color(44, 46, 52, 170));

                var textScale = debugRowTextScale;
                var textY = rowRect.Y + ((rowRect.Height - _context.MeasureBitmapFontHeight(textScale)) * 0.5f);

                var row = rows[index];
                var labelX = rowRect.X + debugRowHorizontalPadding;
                var valueRightX = rowRect.Right - debugRowHorizontalPadding;
                var trimmedValue = _context.TrimBitmapMenuText(row.Value, rowRect.Width * 0.42f, textScale);
                var valueWidth = _context.MeasureBitmapFontWidth(trimmedValue, textScale);
                var valueX = valueRightX - valueWidth;
                var labelMaxWidth = Math.Max(40f, valueX - labelX - debugRowColumnGap);
                var trimmedLabel = _context.TrimBitmapMenuText(row.Label, labelMaxWidth, textScale);

                var labelColor = row.Activate is null ? new Color(150, 150, 150) : Color.White;
                _context.DrawBitmapFontText(trimmedLabel, new Vector2(labelX, textY), labelColor, textScale);
                if (!string.IsNullOrWhiteSpace(trimmedValue))
                {
                    _context.DrawBitmapFontText(trimmedValue, new Vector2(valueX, textY), Color.White, textScale);
                }
            }
        }

        private void GetDebugMenuLayout(int rowCount, out Rectangle panel, out Rectangle[] rowBounds, out int rowHeight)
        {
            rowCount = Math.Max(1, rowCount);
            rowHeight = 48;
            var panelWidth = Math.Max(1, Math.Min(400, _context.ViewportWidth - 24));
            var panelHeight = Math.Max(1, Math.Min(60 + (rowCount * rowHeight), _context.ViewportHeight - 24));

            var panelX = (_context.ViewportWidth - panelWidth) / 2;
            var panelY = (_context.ViewportHeight - panelHeight) / 2;
            panel = new Rectangle(panelX, panelY, panelWidth, panelHeight);

            var visibleRowCount = Math.Max(1, Math.Min(rowCount, (panel.Height - 50) / rowHeight));
            rowBounds = new Rectangle[visibleRowCount];
            var currentY = panel.Y + 50;
            for (var index = 0; index < rowBounds.Length; index += 1)
            {
                rowBounds[index] = new Rectangle(panel.X, currentY, panelWidth, rowHeight - 2);
                currentY += rowHeight;
            }
        }

        private List<DebugMenuRow> BuildDebugMenuRows()
        {
            var localPlayer = _context._world.LocalPlayer;
            var canFillUber = localPlayer.IsAlive && localPlayer.ClassId == PlayerClass.Medic;
            var fillUberValue = canFillUber
                ? $"{localPlayer.MedicUberCharge:F0}/{PlayerEntity.MedicUberMaxCharge:F0}"
                : "Medic only";

            var rows = new List<DebugMenuRow>
            {
                new("Rocket Collisions", _context._debugRocketCollisionsEnabled ? "Enabled" : "Disabled", () =>
                {
                    _context._debugRocketCollisionsEnabled = !_context._debugRocketCollisionsEnabled;
                }),
                new("Fill Ubercharge", fillUberValue, canFillUber ? () => localPlayer.FillMedicUberCharge() : null),
                new("Back", string.Empty, CloseDebugMenu),
            };

            return rows;
        }
}
