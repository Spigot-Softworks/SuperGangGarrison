#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
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
            _game._pendingControlsBinding = null;

            if (_game._mainMenuOpen || reopenInGameMenu)
            {
                _game.OpenOptionsMenu(reopenInGameMenu);
            }
        }

        public void UpdateControlsMenu(KeyboardState keyboard, MouseState mouse)
        {
            const float xbegin = 40f;
            const float ybegin = 150f;
            const float spacing = 28f;
            const float width = 360f;
            var bindingItems = _game.GetControlsMenuBindings();
            var items = bindingItems.Count + 1;

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

            if (mouse.X > xbegin && mouse.X < xbegin + width)
            {
                _game._controlsHoverIndex = (int)MathF.Round((mouse.Y - ybegin) / spacing);
                if (_game._controlsHoverIndex < 0 || _game._controlsHoverIndex >= items)
                {
                    _game._controlsHoverIndex = -1;
                }
            }
            else
            {
                _game._controlsHoverIndex = -1;
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
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.82f);

            var title = _game._pendingControlsBinding.HasValue
                ? $"Press a key for {_game.GetControlsBindingLabel(_game._pendingControlsBinding.Value)}"
                : "Controls";
            _game.DrawBitmapFontText(title, new Vector2(40f, 110f), Color.White, 1.2f);

            var items = _game.GetControlsMenuBindings();
            const float xbegin = 40f;
            const float ybegin = 150f;
            const float spacing = 28f;
            const float width = 360f;
            _game.DrawMenuPanelBackdrop(new Rectangle((int)xbegin - 12, (int)ybegin - 36, (int)width + 44, (items.Count + 2) * (int)spacing), 0.82f);
            _game.DrawMenuPlaqueRows(new Vector2(xbegin, ybegin), items.Count + 1, spacing, width, 0.72f);

            var position = new Vector2(xbegin, ybegin);
            for (var index = 0; index < items.Count; index += 1)
            {
                var item = items[index];
                var color = _game._pendingControlsBinding == item.Binding
                    ? Color.Orange
                    : index == _game._controlsHoverIndex ? Color.Red : Color.White;
                _game.DrawBitmapFontText(item.Label, position, color, 1f);
                _game.DrawBitmapFontText(Game1.GetBindingDisplayName(item.Key), new Vector2(280f, position.Y), color, 1f);
                position.Y += spacing;
            }

            var backColor = items.Count == _game._controlsHoverIndex ? Color.Red : Color.White;
            _game.DrawBitmapFontText("Back", position, backColor, 1f);
        }
    }
}
