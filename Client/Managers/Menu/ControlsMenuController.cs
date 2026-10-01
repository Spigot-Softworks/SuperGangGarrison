#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using System;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class ControlsMenuController
    {
        private static readonly string[] ControlsMenuTabLabels =
        [
            "Keyboard",
            "Controller",
        ];

        private readonly IMenuContext _context;

        public ControlsMenuController(IMenuContext context)
        {
            _context = context;
        }

        public void OpenControlsMenu(bool fromGameplay)
        {
            _context._controlsMenuOpen = true;
            _context._controlsMenuOpenedFromGameplay = fromGameplay;
            _context._controlsHoverIndex = -1;
            _context._controlsScrollOffset = 0;
            _context._controlsPageIndex = 0;
            _context._pendingControlsBinding = null;
            _context._pendingControllerControlsBinding = null;
            _context._optionsMenuOpen = false;
            _context._pluginOptionsMenuOpen = false;
            _context._editingPlayerName = false;
        }

        public void CloseControlsMenu()
        {
            var reopenInGameMenu = _context._controlsMenuOpenedFromGameplay && !_context._mainMenuOpen;
            _context._controlsMenuOpen = false;
            _context._controlsMenuOpenedFromGameplay = false;
            _context._controlsHoverIndex = -1;
            _context._controlsScrollOffset = 0;
            _context._pendingControlsBinding = null;
            _context._pendingControllerControlsBinding = null;

            if (_context._mainMenuOpen || reopenInGameMenu)
            {
                _context.OpenOptionsMenu(reopenInGameMenu);
            }
        }

        public void UpdateControlsMenu(KeyboardState keyboard, MouseState mouse)
        {
            if (_context._pendingControlsBinding.HasValue)
            {
                UpdateKeyboardBindingCapture(keyboard, mouse);
                return;
            }

            if (_context._pendingControllerControlsBinding.HasValue)
            {
                UpdateControllerBindingCapture(keyboard);
                return;
            }

            if (_context.IsKeyPressed(keyboard, Keys.Escape) || _context.IsControllerMenuBackPressed())
            {
                CloseControlsMenu();
                return;
            }

            var itemCount = GetActiveControlsMenuItemCount();
            GetControlsMenuPanelLayout(out var panel, out var listBounds, out var backBounds, out var compactLayout, out var rowHeight);
            var tabBounds = GetControlsMenuTabButtonBounds(panel, compactLayout);
            var visibleRowCount = Math.Max(1, listBounds.Height / rowHeight);
            ClampControlsScrollOffset(itemCount, visibleRowCount);

            if (TryUpdateControlsControllerInput(itemCount, visibleRowCount))
            {
                return;
            }

            var trackBounds = new Rectangle(panel.Right - 20, listBounds.Y, 8, listBounds.Height);
            var controlsScrollOffset = _context._controlsScrollOffset;
            if (_context.TryHandleScrollbarDrag(
                    mouse,
                    _context._previousMouse,
                    ScrollbarOwners.ControlsMenu,
                    trackBounds,
                    ref controlsScrollOffset,
                    itemCount,
                    visibleRowCount))
            {
                _context._controlsScrollOffset = controlsScrollOffset;
                return;
            }

            _context._controlsScrollOffset = controlsScrollOffset;

            var wheelDelta = mouse.ScrollWheelValue - _context._previousMouse.ScrollWheelValue;
            if (wheelDelta != 0 && listBounds.Contains(mouse.Position))
            {
                var stepCount = Math.Max(1, Math.Abs(wheelDelta) / 120);
                _context._controlsScrollOffset = Math.Clamp(
                    _context._controlsScrollOffset + (wheelDelta > 0 ? -stepCount : stepCount),
                    0,
                    Math.Max(0, itemCount - visibleRowCount));
            }

            var clickPressed = mouse.LeftButton == ButtonState.Pressed && _context._previousMouse.LeftButton != ButtonState.Pressed;
            if (clickPressed)
            {
                for (var tabIndex = 0; tabIndex < tabBounds.Length; tabIndex += 1)
                {
                    if (tabBounds[tabIndex].Contains(mouse.Position))
                    {
                        SwitchControlsPage(tabIndex);
                        return;
                    }
                }
            }

            if (_context.IsControllerMenuInputActive())
            {
                if (_context._controlsHoverIndex < 0 && itemCount > 0)
                {
                    _context._controlsHoverIndex = 0;
                }
            }
            else
            {
                _context._controlsHoverIndex = -1;
            }

            if (_context.ShouldUseMouseMenuHover(mouse) && backBounds.Contains(mouse.Position))
            {
                _context._controlsHoverIndex = itemCount;
            }
            else if (_context.ShouldUseMouseMenuHover(mouse) && listBounds.Contains(mouse.Position))
            {
                var visibleIndex = (mouse.Y - listBounds.Y) / rowHeight;
                var hoverIndex = _context._controlsScrollOffset + visibleIndex;
                if (visibleIndex >= 0 && hoverIndex >= 0 && hoverIndex < itemCount)
                {
                    _context._controlsHoverIndex = hoverIndex;
                }
            }

            if (!clickPressed || _context._controlsHoverIndex < 0)
            {
                return;
            }

            if (_context._controlsHoverIndex == itemCount)
            {
                CloseControlsMenu();
                return;
            }

            BeginBindingCaptureForHoveredRow();
        }

        private void UpdateKeyboardBindingCapture(KeyboardState keyboard, MouseState mouse)
        {
            if (!_context._pendingControlsBinding.HasValue)
            {
                return;
            }

            var pendingBinding = _context._pendingControlsBinding.Value;
            if (_context.IsKeyPressed(keyboard, Keys.Escape))
            {
                _context._pendingControlsBinding = null;
                return;
            }

            if (InputBindingInput.TryGetPressedBindableMouseButton(mouse, _context._previousMouse, out var mouseBinding))
            {
                _context.ApplyControlsBinding(pendingBinding, mouseBinding);
                _context.PersistInputBindings();
                _context._pendingControlsBinding = null;
                return;
            }

            foreach (var key in keyboard.GetPressedKeys())
            {
                if (_context._previousKeyboard.IsKeyDown(key))
                {
                    continue;
                }

                _context.ApplyControlsBinding(pendingBinding, InputBinding.FromKey(key));
                _context.PersistInputBindings();
                _context._pendingControlsBinding = null;
                return;
            }
        }

        private void UpdateControllerBindingCapture(KeyboardState keyboard)
        {
            if (!_context._pendingControllerControlsBinding.HasValue)
            {
                return;
            }

            var pendingBinding = _context._pendingControllerControlsBinding.Value;
            if (_context.IsKeyPressed(keyboard, Keys.Escape))
            {
                _context._pendingControllerControlsBinding = null;
                return;
            }

            if (!_context.TryGetPressedControllerButtonBinding(out var binding))
            {
                return;
            }

            _context.ApplyControllerControlsBinding(pendingBinding, binding);
            _context._pendingControllerControlsBinding = null;
        }

        private bool TryUpdateControlsControllerInput(int itemCount, int visibleRowCount)
        {
            if (!_context.IsControllerMenuInputActive())
            {
                return false;
            }

            var handled = false;
            if (_context.TryConsumeControllerMenuNavigation(out var horizontalStep, out var verticalStep))
            {
                if (verticalStep != 0)
                {
                    _context._controlsHoverIndex = MoveControllerMenuSelection(
                        _context._controlsHoverIndex,
                        itemCount + 1,
                        verticalStep);
                    EnsureControlsControllerSelectionVisible(itemCount, visibleRowCount);
                    handled = true;
                }
                else if (horizontalStep != 0)
                {
                    SwitchControlsPage(Math.Clamp(_context._controlsPageIndex + horizontalStep, 0, ControlsMenuTabLabels.Length - 1));
                    handled = true;
                }
            }

            if (_context.IsControllerMenuConfirmPressed())
            {
                if (_context._controlsHoverIndex < 0 && itemCount > 0)
                {
                    _context._controlsHoverIndex = 0;
                }

                if (_context._controlsHoverIndex == itemCount)
                {
                    CloseControlsMenu();
                    return true;
                }

                if (_context._controlsHoverIndex >= 0 && _context._controlsHoverIndex < itemCount)
                {
                    BeginBindingCaptureForHoveredRow();
                    return true;
                }
            }

            return handled;
        }

        private void BeginBindingCaptureForHoveredRow()
        {
            if (IsControllerControlsPage())
            {
                var controllerItems = _context.GetControllerControlsMenuBindings();
                if (_context._controlsHoverIndex >= 0 && _context._controlsHoverIndex < controllerItems.Count)
                {
                    _context._pendingControllerControlsBinding = controllerItems[_context._controlsHoverIndex].Binding;
                }

                return;
            }

            var keyboardItems = _context.GetControlsMenuBindings();
            if (_context._controlsHoverIndex >= 0 && _context._controlsHoverIndex < keyboardItems.Count)
            {
                _context._pendingControlsBinding = keyboardItems[_context._controlsHoverIndex].Binding;
            }
        }

        private void EnsureControlsControllerSelectionVisible(int itemCount, int visibleRowCount)
        {
            if (_context._controlsHoverIndex < 0 || _context._controlsHoverIndex >= itemCount)
            {
                return;
            }

            if (_context._controlsHoverIndex < _context._controlsScrollOffset)
            {
                _context._controlsScrollOffset = _context._controlsHoverIndex;
            }
            else if (_context._controlsHoverIndex >= _context._controlsScrollOffset + visibleRowCount)
            {
                _context._controlsScrollOffset = _context._controlsHoverIndex - visibleRowCount + 1;
            }
        }

        public void DrawControlsMenu()
        {
            var viewportWidth = _context.ViewportWidth;
            var viewportHeight = _context.ViewportHeight;
            _context._spriteBatch.Draw(_context._pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.86f);

            // Draw bottom bar and runners (in animated mode only) - behind everything else
            if (_context.GameplayRuntimeSettings.MenuBackgroundMode != MenuBackgroundMode.Static)
            {
                const int bottomBarHeight = 76;
                var barY = viewportHeight - bottomBarHeight;
                var bottomBarBounds = new Rectangle(0, barY, viewportWidth, bottomBarHeight);
                _context._spriteBatch.Draw(_context._pixel, bottomBarBounds, new Color(0x57, 0x4f, 0x47));
                _context.Menus.MenuBottomBarRunners.Draw(bottomBarBounds);
            }

            var keyboardItems = _context.GetControlsMenuBindings();
            var controllerItems = _context.GetControllerControlsMenuBindings();
            var controllerPage = IsControllerControlsPage();
            var itemCount = controllerPage ? controllerItems.Count : keyboardItems.Count;
            GetControlsMenuPanelLayout(out var panel, out var listBounds, out var backBounds, out var compactLayout, out var rowHeight);
            var mouse = _context.GetFrameMouseState();
            var visibleRowCount = Math.Max(1, listBounds.Height / rowHeight);
            ClampControlsScrollOffset(itemCount, visibleRowCount);

            _context.DrawRoundedRectangleOutline(panel, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 2, radius: 8);

            const float headerScale = 1.15f;
            const float rowTextScale = 1f;
            const float compactRowTextScale = 1f;
            const float rowHorizontalPadding = 14f;
            const float rowColumnGap = 20f;

            var title = GetControlsMenuTitle();
            _context.DrawBitmapFontText(title, new Vector2(listBounds.X, panel.Y + 14f), Color.White, headerScale);
            DrawControlsMenuTabs(panel, compactLayout);

            if (itemCount > visibleRowCount)
            {
                var visibleStart = _context._controlsScrollOffset + 1;
                var visibleEnd = Math.Min(itemCount, _context._controlsScrollOffset + visibleRowCount);
                _context.DrawBitmapFontText(
                    $"{visibleStart}-{visibleEnd}/{itemCount}",
                    new Vector2(listBounds.Right - (compactLayout ? 78f : 96f), panel.Y + 14f),
                    new Color(186, 186, 186),
                    compactLayout ? 0.92f : 1f);
            }

            var endIndex = Math.Min(itemCount, _context._controlsScrollOffset + visibleRowCount);
            for (var index = _context._controlsScrollOffset; index < endIndex; index += 1)
            {
                var visibleRow = index - _context._controlsScrollOffset;
                var rowBounds = new Rectangle(listBounds.X, listBounds.Y + (visibleRow * rowHeight), listBounds.Width, rowHeight - 2);
                var isHovered = index == _context._controlsHoverIndex || rowBounds.Contains(mouse.Position);
                var rowFill = isHovered ? new Color(36, 32, 29) : new Color(54, 47, 41);
                _context._spriteBatch.Draw(_context._pixel, rowBounds, rowFill);

                var textScale = compactLayout ? compactRowTextScale : rowTextScale;
                var textY = rowBounds.Y + ((_context.MeasureBitmapFontHeight(textScale) < rowBounds.Height)
                    ? (rowBounds.Height - _context.MeasureBitmapFontHeight(textScale)) * 0.5f
                    : 0f);

                var labelX = rowBounds.X + rowHorizontalPadding;
                var valueRightX = rowBounds.Right - rowHorizontalPadding;
                string label;
                string valueText;
                Color color;
                if (controllerPage)
                {
                    var item = controllerItems[index];
                    label = item.Label;
                    valueText = Game1.GetControllerButtonBindingLabel(item.Input);
                    color = _context._pendingControllerControlsBinding == item.Binding ? Color.Orange : Color.White;
                }
                else
                {
                    var item = keyboardItems[index];
                    label = item.Label;
                    valueText = Game1.GetBindingDisplayName(item.Input);
                    color = _context._pendingControlsBinding == item.Binding ? Color.Orange : Color.White;
                }

                var trimmedValue = _context.TrimBitmapMenuText(valueText, rowBounds.Width * 0.42f, textScale);
                var valueWidth = _context.MeasureBitmapFontWidth(trimmedValue, textScale);
                var valueX = valueRightX - valueWidth;
                var labelMaxWidth = Math.Max(40f, valueX - labelX - rowColumnGap);
                var trimmedLabel = _context.TrimBitmapMenuText(label, labelMaxWidth, textScale);

                _context.DrawBitmapFontText(trimmedLabel, new Vector2(labelX, textY), color, textScale);
                _context.DrawBitmapFontText(trimmedValue, new Vector2(valueX, textY), color, textScale);
            }

            if (itemCount > visibleRowCount)
            {
                var trackBounds = new Rectangle(panel.Right - 20, listBounds.Y, 8, listBounds.Height);
                _context._spriteBatch.Draw(_context._pixel, trackBounds, new Color(30, 32, 38));

                var maxOffset = Math.Max(1, itemCount - visibleRowCount);
                var thumbHeight = Math.Max(24, (int)MathF.Round(trackBounds.Height * (visibleRowCount / (float)itemCount)));
                var thumbTravel = Math.Max(0, trackBounds.Height - thumbHeight);
                var thumbY = trackBounds.Y + (int)MathF.Round((_context._controlsScrollOffset / (float)maxOffset) * thumbTravel);
                var thumbBounds = new Rectangle(trackBounds.X, thumbY, trackBounds.Width, thumbHeight);
                _context._spriteBatch.Draw(_context._pixel, thumbBounds, new Color(125, 125, 125));
            }

            var backHovered = _context._controlsHoverIndex == itemCount || backBounds.Contains(mouse.Position);
            _context.DrawMenuButtonScaled(backBounds, "Back", backHovered, 1f);
        }

        private string GetControlsMenuTitle()
        {
            if (_context._pendingControlsBinding.HasValue)
            {
                return $"Press key or mouse for {_context.GetControlsBindingLabel(_context._pendingControlsBinding.Value)}";
            }

            if (_context._pendingControllerControlsBinding.HasValue)
            {
                return $"Press controller button for {Game1.GetControllerControlsBindingLabel(_context._pendingControllerControlsBinding.Value)}";
            }

            return "Controls";
        }

        private void GetControlsMenuPanelLayout(out Rectangle panel, out Rectangle listBounds, out Rectangle backBounds, out bool compactLayout, out int rowHeight)
        {
            var panelWidth = Math.Min(_context.ViewportWidth - 32, 760);
            var panelHeight = Math.Min(_context.ViewportHeight - 32, _context.ViewportHeight < 540 ? _context.ViewportHeight - 36 : 620);
            panel = new Rectangle(
                (_context.ViewportWidth - panelWidth) / 2,
                (_context.ViewportHeight - panelHeight) / 2,
                panelWidth,
                panelHeight);

            compactLayout = panel.Height < 540 || panel.Width < 700;
            var padding = compactLayout ? 20 : 28;
            var baseRowHeight = compactLayout ? 24 : 26;
            var rowSpacing = compactLayout ? 4 : 6;
            rowHeight = baseRowHeight + rowSpacing;
            var titleHeight = compactLayout ? 48 : 56;
            var tabRowHeight = compactLayout ? 44 : 48;
            var headerHeight = titleHeight + tabRowHeight;
            var footerHeight = compactLayout ? 56 : 64;
            var scrollbarPadding = 18;
            var listTopPadding = compactLayout ? 8 : 10;

            listBounds = new Rectangle(
                panel.X + padding,
                panel.Y + headerHeight + listTopPadding,
                panel.Width - (padding * 2) - scrollbarPadding,
                Math.Max(rowHeight, panel.Height - headerHeight - footerHeight - 10 - listTopPadding));

            var backWidth = compactLayout ? 150 : 180;
            var backHeight = compactLayout ? 36 : 42;
            backBounds = new Rectangle(panel.Right - padding - backWidth, panel.Bottom - padding - backHeight, backWidth, backHeight);
        }

        private static Rectangle[] GetControlsMenuTabButtonBounds(Rectangle panel, bool compactLayout)
        {
            var padding = compactLayout ? 20 : 28;
            var buttonHeight = compactLayout ? 34 : 42;
            var tabCount = ControlsMenuTabLabels.Length;
            var spacing = compactLayout ? 8 : 12;
            var buttonWidth = Math.Min(160, Math.Max(120, (panel.Width - (padding * 2) - ((tabCount - 1) * spacing)) / tabCount));
            var startX = panel.X + padding;
            var y = panel.Y + (compactLayout ? 52 : 60);
            var bounds = new Rectangle[tabCount];

            for (var i = 0; i < tabCount; i += 1)
            {
                bounds[i] = new Rectangle(startX + i * (buttonWidth + spacing), y, buttonWidth, buttonHeight);
            }

            return bounds;
        }

        private void DrawControlsMenuTabs(Rectangle panel, bool compactLayout)
        {
            var tabBounds = GetControlsMenuTabButtonBounds(panel, compactLayout);
            for (var i = 0; i < tabBounds.Length; i += 1)
            {
                var selected = i == _context._controlsPageIndex;
                _context.DrawMenuButtonScaled(tabBounds[i], ControlsMenuTabLabels[i], selected, 1f);
            }
        }

        private int GetActiveControlsMenuItemCount()
        {
            return IsControllerControlsPage()
                ? _context.GetControllerControlsMenuBindings().Count
                : _context.GetControlsMenuBindings().Count;
        }

        private bool IsControllerControlsPage()
        {
            return _context._controlsPageIndex == 1;
        }

        private void SwitchControlsPage(int pageIndex)
        {
            pageIndex = Math.Clamp(pageIndex, 0, ControlsMenuTabLabels.Length - 1);
            if (_context._controlsPageIndex == pageIndex)
            {
                return;
            }

            _context._controlsPageIndex = pageIndex;
            _context._controlsHoverIndex = -1;
            _context._controlsScrollOffset = 0;
            _context._pendingControlsBinding = null;
            _context._pendingControllerControlsBinding = null;
        }

        private void ClampControlsScrollOffset(int rowCount, int visibleRowCount)
        {
            _context._controlsScrollOffset = Math.Clamp(
                _context._controlsScrollOffset,
                0,
                Math.Max(0, rowCount - visibleRowCount));
        }
}
