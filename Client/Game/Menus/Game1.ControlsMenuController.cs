#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using System;
using System.Collections.Generic;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class ControlsMenuController
    {
        private readonly Game1 _game;

        public ControlsMenuController(Game1 game)
        {
            _game = game;
        }

        public void OpenControlsMenu(bool fromGameplay)
        {
            _game._controlsMenuOpen = true;
            _game._controlsMenuOpenedFromGameplay = fromGameplay;
            _game._controlsHoverIndex = -1;
            _game._controlsScrollOffset = 0;
            _game._pendingControlsBinding = null;
            _game._optionsMenuOpen = false;
            _game._pluginOptionsMenuOpen = false;
            _game._editingPlayerName = false;
        }

        public void CloseControlsMenu()
        {
            var reopenInGameMenu = _game._controlsMenuOpenedFromGameplay && !_game._mainMenuOpen;
            _game._controlsMenuOpen = false;
            _game._controlsMenuOpenedFromGameplay = false;
            _game._controlsHoverIndex = -1;
            _game._controlsScrollOffset = 0;
            _game._pendingControlsBinding = null;

            if (_game._mainMenuOpen || reopenInGameMenu)
            {
                _game.OpenOptionsMenu(reopenInGameMenu);
            }
        }

        public void UpdateControlsMenu(KeyboardState keyboard, MouseState mouse)
        {
            var bindingItems = _game.GetControlsMenuBindings();

            if (_game._pendingControlsBinding.HasValue)
            {
                if (_game.IsKeyPressed(keyboard, Keys.Escape))
                {
                    _game._pendingControlsBinding = null;
                    return;
                }

                foreach (var key in keyboard.GetPressedKeys())
                {
                    if (_game._previousKeyboard.IsKeyDown(key))
                    {
                        continue;
                    }

                    _game.ApplyControlsBinding(_game._pendingControlsBinding.Value, key);
                    _game.PersistInputBindings();
                    _game._pendingControlsBinding = null;
                    return;
                }

                return;
            }

            if (_game.IsKeyPressed(keyboard, Keys.Escape))
            {
                CloseControlsMenu();
                return;
            }

            GetControlsMenuPanelLayout(out _, out var listBounds, out var backBounds, out _, out var rowHeight);
            var visibleRowCount = Math.Max(1, listBounds.Height / rowHeight);
            ClampControlsScrollOffset(bindingItems.Count, visibleRowCount);

            var wheelDelta = mouse.ScrollWheelValue - _game._previousMouse.ScrollWheelValue;
            if (wheelDelta != 0 && listBounds.Contains(mouse.Position))
            {
                var stepCount = Math.Max(1, Math.Abs(wheelDelta) / 120);
                _game._controlsScrollOffset = Math.Clamp(
                    _game._controlsScrollOffset + (wheelDelta > 0 ? -stepCount : stepCount),
                    0,
                    Math.Max(0, bindingItems.Count - visibleRowCount));
            }

            _game._controlsHoverIndex = -1;
            if (backBounds.Contains(mouse.Position))
            {
                _game._controlsHoverIndex = bindingItems.Count;
            }
            else if (listBounds.Contains(mouse.Position))
            {
                var visibleIndex = (mouse.Y - listBounds.Y) / rowHeight;
                var hoverIndex = _game._controlsScrollOffset + visibleIndex;
                if (visibleIndex >= 0 && hoverIndex >= 0 && hoverIndex < bindingItems.Count)
                {
                    _game._controlsHoverIndex = hoverIndex;
                }
            }

            var clickPressed = mouse.LeftButton == ButtonState.Pressed && _game._previousMouse.LeftButton != ButtonState.Pressed;
            if (!clickPressed || _game._controlsHoverIndex < 0)
            {
                return;
            }

            if (_game._controlsHoverIndex == bindingItems.Count)
            {
                CloseControlsMenu();
                return;
            }

            _game._pendingControlsBinding = bindingItems[_game._controlsHoverIndex].Binding;
        }

        public void DrawControlsMenu()
        {
            var viewportWidth = _game.ViewportWidth;
            var viewportHeight = _game.ViewportHeight;
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.78f);

            // Draw bottom bar and runners (in animated mode only) - behind everything else
            if (_game._menuBackgroundMode != MenuBackgroundMode.Static)
            {
                const int bottomBarHeight = 76;
                var barY = viewportHeight - bottomBarHeight;
                var bottomBarBounds = new Rectangle(0, barY, viewportWidth, bottomBarHeight);
                _game._spriteBatch.Draw(_game._pixel, bottomBarBounds, new Color(0x57, 0x4f, 0x47));
                _game._menuBottomBarRunners.Draw(bottomBarBounds);
            }

            var items = _game.GetControlsMenuBindings();
            GetControlsMenuPanelLayout(out var panel, out var listBounds, out var backBounds, out var compactLayout, out var rowHeight);
            var visibleRowCount = Math.Max(1, listBounds.Height / rowHeight);
            ClampControlsScrollOffset(items.Count, visibleRowCount);

            _game.DrawRoundedRectangleOutline(panel, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 2, radius: 8);

            const float headerScale = 1.15f;
            const float rowTextScale = 1f;
            const float compactRowTextScale = 1f;
            const float rowHorizontalPadding = 14f;
            const float rowColumnGap = 20f;

            var title = _game._pendingControlsBinding.HasValue
                ? $"Press a key for {_game.GetControlsBindingLabel(_game._pendingControlsBinding.Value)}"
                : "Controls";
            _game.DrawBitmapFontText(title, new Vector2(listBounds.X, panel.Y + 14f), Color.White, headerScale);

            if (items.Count > visibleRowCount)
            {
                var visibleStart = _game._controlsScrollOffset + 1;
                var visibleEnd = Math.Min(items.Count, _game._controlsScrollOffset + visibleRowCount);
                _game.DrawBitmapFontText(
                    $"{visibleStart}-{visibleEnd}/{items.Count}",
                    new Vector2(listBounds.Right - (compactLayout ? 78f : 96f), panel.Y + 14f),
                    new Color(186, 186, 186),
                    compactLayout ? 0.92f : 1f);
            }

            var endIndex = Math.Min(items.Count, _game._controlsScrollOffset + visibleRowCount);
            for (var index = _game._controlsScrollOffset; index < endIndex; index += 1)
            {
                var item = items[index];
                var visibleRow = index - _game._controlsScrollOffset;
                var rowBounds = new Rectangle(listBounds.X, listBounds.Y + (visibleRow * rowHeight), listBounds.Width, rowHeight - 2);
                var isHovered = index == _game._controlsHoverIndex;
                var rowFill = isHovered ? new Color(75, 67, 62) : new Color(54, 47, 41);
                _game._spriteBatch.Draw(_game._pixel, rowBounds, rowFill);

                var textScale = compactLayout ? compactRowTextScale : rowTextScale;
                var textY = rowBounds.Y + ((_game.MeasureBitmapFontHeight(textScale) < rowBounds.Height)
                    ? (rowBounds.Height - _game.MeasureBitmapFontHeight(textScale)) * 0.5f
                    : 0f);

                var labelX = rowBounds.X + rowHorizontalPadding;
                var valueRightX = rowBounds.Right - rowHorizontalPadding;
                var valueText = Game1.GetBindingDisplayName(item.Key);
                var trimmedValue = _game.TrimBitmapMenuText(valueText, rowBounds.Width * 0.42f, textScale);
                var valueWidth = _game.MeasureBitmapFontWidth(trimmedValue, textScale);
                var valueX = valueRightX - valueWidth;
                var labelMaxWidth = Math.Max(40f, valueX - labelX - rowColumnGap);
                var trimmedLabel = _game.TrimBitmapMenuText(item.Label, labelMaxWidth, textScale);
                var color = _game._pendingControlsBinding == item.Binding ? Color.Orange : Color.White;

                _game.DrawBitmapFontText(trimmedLabel, new Vector2(labelX, textY), color, textScale);
                _game.DrawBitmapFontText(trimmedValue, new Vector2(valueX, textY), color, textScale);
            }

            if (items.Count > visibleRowCount)
            {
                var trackBounds = new Rectangle(panel.Right - 20, listBounds.Y, 8, listBounds.Height);
                _game._spriteBatch.Draw(_game._pixel, trackBounds, new Color(30, 32, 38));

                var maxOffset = Math.Max(1, items.Count - visibleRowCount);
                var thumbHeight = Math.Max(24, (int)MathF.Round(trackBounds.Height * (visibleRowCount / (float)items.Count)));
                var thumbTravel = Math.Max(0, trackBounds.Height - thumbHeight);
                var thumbY = trackBounds.Y + (int)MathF.Round((_game._controlsScrollOffset / (float)maxOffset) * thumbTravel);
                var thumbBounds = new Rectangle(trackBounds.X, thumbY, trackBounds.Width, thumbHeight);
                _game._spriteBatch.Draw(_game._pixel, thumbBounds, new Color(125, 125, 125));
            }

            var backHovered = _game._controlsHoverIndex == items.Count;
            _game.DrawMenuButtonScaled(backBounds, "Back", backHovered, 1f);
        }

        private void GetControlsMenuPanelLayout(out Rectangle panel, out Rectangle listBounds, out Rectangle backBounds, out bool compactLayout, out int rowHeight)
        {
            var panelWidth = Math.Min(_game.ViewportWidth - 32, 760);
            var panelHeight = Math.Min(_game.ViewportHeight - 32, _game.ViewportHeight < 540 ? _game.ViewportHeight - 36 : 620);
            panel = new Rectangle(
                (_game.ViewportWidth - panelWidth) / 2,
                (_game.ViewportHeight - panelHeight) / 2,
                panelWidth,
                panelHeight);

            compactLayout = panel.Height < 540 || panel.Width < 700;
            var padding = compactLayout ? 20 : 28;
            var baseRowHeight = compactLayout ? 24 : 26;
            var rowSpacing = compactLayout ? 4 : 6;
            rowHeight = baseRowHeight + rowSpacing;
            var headerHeight = compactLayout ? 48 : 56;
            var footerHeight = compactLayout ? 56 : 64;
            var scrollbarPadding = 18;

            listBounds = new Rectangle(
                panel.X + padding,
                panel.Y + headerHeight,
                panel.Width - (padding * 2) - scrollbarPadding,
                Math.Max(rowHeight, panel.Height - headerHeight - footerHeight - 10));

            var backWidth = compactLayout ? 150 : 180;
            var backHeight = compactLayout ? 36 : 42;
            backBounds = new Rectangle(panel.Right - padding - backWidth, panel.Bottom - padding - backHeight, backWidth, backHeight);
        }

        private void ClampControlsScrollOffset(int rowCount, int visibleRowCount)
        {
            _game._controlsScrollOffset = Math.Clamp(
                _game._controlsScrollOffset,
                0,
                Math.Max(0, rowCount - visibleRowCount));
        }
    }
}
