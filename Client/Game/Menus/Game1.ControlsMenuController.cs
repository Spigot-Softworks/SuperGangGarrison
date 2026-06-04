#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class ControlsMenuController
    {
        private static readonly string[] ControlsMenuTabLabels =
        [
            "Keyboard",
            "Controller",
        ];

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
            _game._controlsPageIndex = 0;
            _game._pendingControlsBinding = null;
            _game._pendingControllerControlsBinding = null;
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
            _game._pendingControllerControlsBinding = null;

            if (_game._mainMenuOpen || reopenInGameMenu)
            {
                _game.OpenOptionsMenu(reopenInGameMenu);
            }
        }

        public void UpdateControlsMenu(KeyboardState keyboard, MouseState mouse)
        {
            if (_game._pendingControlsBinding.HasValue)
            {
                UpdateKeyboardBindingCapture(keyboard, mouse);
                return;
            }

            if (_game._pendingControllerControlsBinding.HasValue)
            {
                UpdateControllerBindingCapture(keyboard);
                return;
            }

            if (_game.IsKeyPressed(keyboard, Keys.Escape) || _game.IsControllerMenuBackPressed())
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
            var controlsScrollOffset = _game._controlsScrollOffset;
            if (_game.TryHandleScrollbarDrag(
                    mouse,
                    _game._previousMouse,
                    ScrollbarOwners.ControlsMenu,
                    trackBounds,
                    ref controlsScrollOffset,
                    itemCount,
                    visibleRowCount))
            {
                _game._controlsScrollOffset = controlsScrollOffset;
                return;
            }

            _game._controlsScrollOffset = controlsScrollOffset;

            var wheelDelta = mouse.ScrollWheelValue - _game._previousMouse.ScrollWheelValue;
            if (wheelDelta != 0 && listBounds.Contains(mouse.Position))
            {
                var stepCount = Math.Max(1, Math.Abs(wheelDelta) / 120);
                _game._controlsScrollOffset = Math.Clamp(
                    _game._controlsScrollOffset + (wheelDelta > 0 ? -stepCount : stepCount),
                    0,
                    Math.Max(0, itemCount - visibleRowCount));
            }

            var clickPressed = mouse.LeftButton == ButtonState.Pressed && _game._previousMouse.LeftButton != ButtonState.Pressed;
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

            if (_game.IsControllerMenuInputActive())
            {
                if (_game._controlsHoverIndex < 0 && itemCount > 0)
                {
                    _game._controlsHoverIndex = 0;
                }
            }
            else
            {
                _game._controlsHoverIndex = -1;
            }

            if (_game.ShouldUseMouseMenuHover(mouse) && backBounds.Contains(mouse.Position))
            {
                _game._controlsHoverIndex = itemCount;
            }
            else if (_game.ShouldUseMouseMenuHover(mouse) && listBounds.Contains(mouse.Position))
            {
                var visibleIndex = (mouse.Y - listBounds.Y) / rowHeight;
                var hoverIndex = _game._controlsScrollOffset + visibleIndex;
                if (visibleIndex >= 0 && hoverIndex >= 0 && hoverIndex < itemCount)
                {
                    _game._controlsHoverIndex = hoverIndex;
                }
            }

            if (!clickPressed || _game._controlsHoverIndex < 0)
            {
                return;
            }

            if (_game._controlsHoverIndex == itemCount)
            {
                CloseControlsMenu();
                return;
            }

            BeginBindingCaptureForHoveredRow();
        }

        private void UpdateKeyboardBindingCapture(KeyboardState keyboard, MouseState mouse)
        {
            if (!_game._pendingControlsBinding.HasValue)
            {
                return;
            }

            var pendingBinding = _game._pendingControlsBinding.Value;
            if (_game.IsKeyPressed(keyboard, Keys.Escape))
            {
                _game._pendingControlsBinding = null;
                return;
            }

            if (InputBindingInput.TryGetPressedBindableMouseButton(mouse, _game._previousMouse, out var mouseBinding))
            {
                _game.ApplyControlsBinding(pendingBinding, mouseBinding);
                _game.PersistInputBindings();
                _game._pendingControlsBinding = null;
                return;
            }

            foreach (var key in keyboard.GetPressedKeys())
            {
                if (_game._previousKeyboard.IsKeyDown(key))
                {
                    continue;
                }

                _game.ApplyControlsBinding(pendingBinding, InputBinding.FromKey(key));
                _game.PersistInputBindings();
                _game._pendingControlsBinding = null;
                return;
            }
        }

        private void UpdateControllerBindingCapture(KeyboardState keyboard)
        {
            if (!_game._pendingControllerControlsBinding.HasValue)
            {
                return;
            }

            var pendingBinding = _game._pendingControllerControlsBinding.Value;
            if (_game.IsKeyPressed(keyboard, Keys.Escape))
            {
                _game._pendingControllerControlsBinding = null;
                return;
            }

            if (!_game.TryGetPressedControllerButtonBinding(out var binding))
            {
                return;
            }

            _game.ApplyControllerControlsBinding(pendingBinding, binding);
            _game._pendingControllerControlsBinding = null;
        }

        private bool TryUpdateControlsControllerInput(int itemCount, int visibleRowCount)
        {
            if (!_game.IsControllerMenuInputActive())
            {
                return false;
            }

            var handled = false;
            if (_game.TryConsumeControllerMenuNavigation(out var horizontalStep, out var verticalStep))
            {
                if (verticalStep != 0)
                {
                    _game._controlsHoverIndex = MoveControllerMenuSelection(
                        _game._controlsHoverIndex,
                        itemCount + 1,
                        verticalStep);
                    EnsureControlsControllerSelectionVisible(itemCount, visibleRowCount);
                    handled = true;
                }
                else if (horizontalStep != 0)
                {
                    SwitchControlsPage(Math.Clamp(_game._controlsPageIndex + horizontalStep, 0, ControlsMenuTabLabels.Length - 1));
                    handled = true;
                }
            }

            if (_game.IsControllerMenuConfirmPressed())
            {
                if (_game._controlsHoverIndex < 0 && itemCount > 0)
                {
                    _game._controlsHoverIndex = 0;
                }

                if (_game._controlsHoverIndex == itemCount)
                {
                    CloseControlsMenu();
                    return true;
                }

                if (_game._controlsHoverIndex >= 0 && _game._controlsHoverIndex < itemCount)
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
                var controllerItems = _game.GetControllerControlsMenuBindings();
                if (_game._controlsHoverIndex >= 0 && _game._controlsHoverIndex < controllerItems.Count)
                {
                    _game._pendingControllerControlsBinding = controllerItems[_game._controlsHoverIndex].Binding;
                }

                return;
            }

            var keyboardItems = _game.GetControlsMenuBindings();
            if (_game._controlsHoverIndex >= 0 && _game._controlsHoverIndex < keyboardItems.Count)
            {
                _game._pendingControlsBinding = keyboardItems[_game._controlsHoverIndex].Binding;
            }
        }

        private void EnsureControlsControllerSelectionVisible(int itemCount, int visibleRowCount)
        {
            if (_game._controlsHoverIndex < 0 || _game._controlsHoverIndex >= itemCount)
            {
                return;
            }

            if (_game._controlsHoverIndex < _game._controlsScrollOffset)
            {
                _game._controlsScrollOffset = _game._controlsHoverIndex;
            }
            else if (_game._controlsHoverIndex >= _game._controlsScrollOffset + visibleRowCount)
            {
                _game._controlsScrollOffset = _game._controlsHoverIndex - visibleRowCount + 1;
            }
        }

        public void DrawControlsMenu()
        {
            var viewportWidth = _game.ViewportWidth;
            var viewportHeight = _game.ViewportHeight;
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.86f);

            // Draw bottom bar and runners (in animated mode only) - behind everything else
            if (_game._menuBackgroundMode != MenuBackgroundMode.Static)
            {
                const int bottomBarHeight = 76;
                var barY = viewportHeight - bottomBarHeight;
                var bottomBarBounds = new Rectangle(0, barY, viewportWidth, bottomBarHeight);
                _game._spriteBatch.Draw(_game._pixel, bottomBarBounds, new Color(0x57, 0x4f, 0x47));
                _game._menuBottomBarRunners.Draw(bottomBarBounds);
            }

            var keyboardItems = _game.GetControlsMenuBindings();
            var controllerItems = _game.GetControllerControlsMenuBindings();
            var controllerPage = IsControllerControlsPage();
            var itemCount = controllerPage ? controllerItems.Count : keyboardItems.Count;
            GetControlsMenuPanelLayout(out var panel, out var listBounds, out var backBounds, out var compactLayout, out var rowHeight);
            var mouse = _game.GetScaledMouseState(_game.GetConstrainedMouseState(Game1.GetCurrentMouseState()));
            var visibleRowCount = Math.Max(1, listBounds.Height / rowHeight);
            ClampControlsScrollOffset(itemCount, visibleRowCount);

            _game.DrawRoundedRectangleOutline(panel, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 2, radius: 8);

            const float headerScale = 1.15f;
            const float rowTextScale = 1f;
            const float compactRowTextScale = 1f;
            const float rowHorizontalPadding = 14f;
            const float rowColumnGap = 20f;

            var title = GetControlsMenuTitle();
            _game.DrawBitmapFontText(title, new Vector2(listBounds.X, panel.Y + 14f), Color.White, headerScale);
            DrawControlsMenuTabs(panel, compactLayout);

            if (itemCount > visibleRowCount)
            {
                var visibleStart = _game._controlsScrollOffset + 1;
                var visibleEnd = Math.Min(itemCount, _game._controlsScrollOffset + visibleRowCount);
                _game.DrawBitmapFontText(
                    $"{visibleStart}-{visibleEnd}/{itemCount}",
                    new Vector2(listBounds.Right - (compactLayout ? 78f : 96f), panel.Y + 14f),
                    new Color(186, 186, 186),
                    compactLayout ? 0.92f : 1f);
            }

            var endIndex = Math.Min(itemCount, _game._controlsScrollOffset + visibleRowCount);
            for (var index = _game._controlsScrollOffset; index < endIndex; index += 1)
            {
                var visibleRow = index - _game._controlsScrollOffset;
                var rowBounds = new Rectangle(listBounds.X, listBounds.Y + (visibleRow * rowHeight), listBounds.Width, rowHeight - 2);
                var isHovered = index == _game._controlsHoverIndex || rowBounds.Contains(mouse.Position);
                var rowFill = isHovered ? new Color(36, 32, 29) : new Color(54, 47, 41);
                _game._spriteBatch.Draw(_game._pixel, rowBounds, rowFill);

                var textScale = compactLayout ? compactRowTextScale : rowTextScale;
                var textY = rowBounds.Y + ((_game.MeasureBitmapFontHeight(textScale) < rowBounds.Height)
                    ? (rowBounds.Height - _game.MeasureBitmapFontHeight(textScale)) * 0.5f
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
                    color = _game._pendingControllerControlsBinding == item.Binding ? Color.Orange : Color.White;
                }
                else
                {
                    var item = keyboardItems[index];
                    label = item.Label;
                    valueText = Game1.GetBindingDisplayName(item.Input);
                    color = _game._pendingControlsBinding == item.Binding ? Color.Orange : Color.White;
                }

                var trimmedValue = _game.TrimBitmapMenuText(valueText, rowBounds.Width * 0.42f, textScale);
                var valueWidth = _game.MeasureBitmapFontWidth(trimmedValue, textScale);
                var valueX = valueRightX - valueWidth;
                var labelMaxWidth = Math.Max(40f, valueX - labelX - rowColumnGap);
                var trimmedLabel = _game.TrimBitmapMenuText(label, labelMaxWidth, textScale);

                _game.DrawBitmapFontText(trimmedLabel, new Vector2(labelX, textY), color, textScale);
                _game.DrawBitmapFontText(trimmedValue, new Vector2(valueX, textY), color, textScale);
            }

            if (itemCount > visibleRowCount)
            {
                var trackBounds = new Rectangle(panel.Right - 20, listBounds.Y, 8, listBounds.Height);
                _game._spriteBatch.Draw(_game._pixel, trackBounds, new Color(30, 32, 38));

                var maxOffset = Math.Max(1, itemCount - visibleRowCount);
                var thumbHeight = Math.Max(24, (int)MathF.Round(trackBounds.Height * (visibleRowCount / (float)itemCount)));
                var thumbTravel = Math.Max(0, trackBounds.Height - thumbHeight);
                var thumbY = trackBounds.Y + (int)MathF.Round((_game._controlsScrollOffset / (float)maxOffset) * thumbTravel);
                var thumbBounds = new Rectangle(trackBounds.X, thumbY, trackBounds.Width, thumbHeight);
                _game._spriteBatch.Draw(_game._pixel, thumbBounds, new Color(125, 125, 125));
            }

            var backHovered = _game._controlsHoverIndex == itemCount || backBounds.Contains(mouse.Position);
            _game.DrawMenuButtonScaled(backBounds, "Back", backHovered, 1f);
        }

        private string GetControlsMenuTitle()
        {
            if (_game._pendingControlsBinding.HasValue)
            {
                return $"Press key or mouse for {_game.GetControlsBindingLabel(_game._pendingControlsBinding.Value)}";
            }

            if (_game._pendingControllerControlsBinding.HasValue)
            {
                return $"Press controller button for {Game1.GetControllerControlsBindingLabel(_game._pendingControllerControlsBinding.Value)}";
            }

            return "Controls";
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
                var selected = i == _game._controlsPageIndex;
                _game.DrawMenuButtonScaled(tabBounds[i], ControlsMenuTabLabels[i], selected, 1f);
            }
        }

        private int GetActiveControlsMenuItemCount()
        {
            return IsControllerControlsPage()
                ? _game.GetControllerControlsMenuBindings().Count
                : _game.GetControlsMenuBindings().Count;
        }

        private bool IsControllerControlsPage()
        {
            return _game._controlsPageIndex == 1;
        }

        private void SwitchControlsPage(int pageIndex)
        {
            pageIndex = Math.Clamp(pageIndex, 0, ControlsMenuTabLabels.Length - 1);
            if (_game._controlsPageIndex == pageIndex)
            {
                return;
            }

            _game._controlsPageIndex = pageIndex;
            _game._controlsHoverIndex = -1;
            _game._controlsScrollOffset = 0;
            _game._pendingControlsBinding = null;
            _game._pendingControllerControlsBinding = null;
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
