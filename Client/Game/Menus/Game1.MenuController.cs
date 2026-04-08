#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.IO;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class MenuController
    {
        private readonly Game1 _game;

        public MenuController(Game1 game)
        {
            _game = game;
        }

        public void Update(KeyboardState keyboard, MouseState mouse)
        {
            EnsureMenuMusicPlaying();
            _game.StopFaucetMusic();
            _game.StopIngameMusic();

            _game.UpdateLobbyBrowserResponses();
            if (_game.UpdateDevMessagePopup(keyboard, mouse))
            {
                return;
            }

            if (_game._quitPromptOpen)
            {
                _game.UpdateQuitPrompt(keyboard, mouse);
                return;
            }

            if (!_game._mainMenuOverlayController.TryUpdate(keyboard, mouse))
            {
                UpdateMainMenu(keyboard, mouse);
            }
        }

        public void Draw()
        {
            var viewportWidth = _game.ViewportWidth;
            var viewportHeight = _game.ViewportHeight;

            EnsureMenuBackgroundTexture(viewportWidth, viewportHeight);

            if (_game._menuBackgroundTexture is not null)
            {
                _game._spriteBatch.Draw(_game._menuBackgroundTexture, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.White);
            }
            else if (!_game.TryDrawScreenSprite("MenuBackgroundS", _game._menuImageFrame, new Vector2(viewportWidth / 2f, viewportHeight / 2f), Color.White, Vector2.One))
            {
                _game._spriteBatch.Draw(_game._pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), new Color(26, 24, 20));
            }

            DrawMenuBackgroundAttribution();

            if (!_game._mainMenuOverlayController.TryDraw())
            {
                var buttons = _game.BuildMainMenuButtons();
                _game.DrawCurrentMainMenuPage(buttons);
                _game.DrawMenuStatusText();
                _game.DrawQuitPrompt();
            }

            _game.DrawDevMessagePopup();
        }

        public MainMenuOverlayKind GetActiveOverlay()
        {
            return _game._mainMenuOverlayController.GetActiveOverlay();
        }

        private void UpdateMainMenu(KeyboardState keyboard, MouseState mouse)
        {
            if (_game.IsKeyPressed(keyboard, Keys.Escape))
            {
                if (_game._optionsMenuOpen)
                {
                    _game.CloseOptionsMenu();
                    return;
                }

                if (_game._mainMenuPage != MainMenuPage.Root)
                {
                    _game.OpenMainMenuPage(MainMenuPage.Root);
                }
                else
                {
                    _game.OpenQuitPrompt();
                }

                return;
            }

            if (_game._optionsMenuOpen)
            {
                return;
            }

            var buttons = _game.BuildMainMenuButtons();
            _game._mainMenuHoverIndex = -1;
            _game._mainMenuBottomBarHover = false;
            for (var index = 0; index < buttons.Count; index += 1)
            {
                if (!buttons[index].Bounds.Contains(mouse.Position))
                {
                    continue;
                }

                _game._mainMenuHoverIndex = index;
                _game._mainMenuBottomBarHover = buttons[index].IsBottomBarButton;
                break;
            }

            var clickPressed = mouse.LeftButton == ButtonState.Pressed && _game._previousMouse.LeftButton != ButtonState.Pressed;
            if (clickPressed && _game._mainMenuHoverIndex >= 0)
            {
                buttons[_game._mainMenuHoverIndex].Activate();
            }
        }

        private void EnsureMenuMusicPlaying()
        {
            _game.EnsureMenuMusicPlaying();
        }

        private void EnsureMenuBackgroundTexture(int viewportWidth, int viewportHeight)
        {
            var (path, attributionText) = GetMenuBackgroundSelection(viewportWidth, viewportHeight);
            _game._menuBackgroundAttributionText = attributionText;
            if (string.IsNullOrWhiteSpace(path))
            {
                DisposeMenuBackgroundTexture();
                _game._menuBackgroundFailedPath = null;
                return;
            }

            if (string.Equals(_game._menuBackgroundTexturePath, path, StringComparison.OrdinalIgnoreCase))
            {
                if (_game._menuBackgroundTexture is not null
                    || string.Equals(_game._menuBackgroundFailedPath, path, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            DisposeMenuBackgroundTexture();
            _game._menuBackgroundTexturePath = path;
            _game._menuBackgroundFailedPath = null;

            try
            {
                using var stream = File.OpenRead(path);
                _game._menuBackgroundTexture = Texture2D.FromStream(_game.GraphicsDevice, stream);
            }
            catch (Exception ex)
            {
                _game._menuBackgroundFailedPath = path;
                _game._menuBackgroundAttributionText = string.Empty;
                _game.AddConsoleLine($"plugin menu background failed to load from \"{path}\": {ex.Message}");
            }
        }

        private (string? Path, string AttributionText) GetMenuBackgroundSelection(int viewportWidth, int viewportHeight)
        {
            var pluginOverride = _game.GetClientPluginMainMenuBackgroundOverride();
            if (pluginOverride is not null
                && !string.IsNullOrWhiteSpace(pluginOverride.ImagePath)
                && File.Exists(pluginOverride.ImagePath))
            {
                return (pluginOverride.ImagePath, pluginOverride.AttributionText);
            }

            return (GetDefaultMenuBackgroundPath(viewportWidth, viewportHeight), string.Empty);
        }

        private static string? GetDefaultMenuBackgroundPath(int viewportWidth, int viewportHeight)
        {
            var aspectRatio = viewportHeight <= 0 ? (16f / 9f) : viewportWidth / (float)viewportHeight;
            var fileName = aspectRatio <= 1.27f
                ? "background-5x4.png"
                : aspectRatio <= 1.4f
                    ? "background-4x3.png"
                    : "background.png";
            var path = ContentRoot.GetPath("Sprites", "Menu", "Title", fileName);
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                return path;
            }

            var fallbackPath = ContentRoot.GetPath("Sprites", "Menu", "Title", "background.png");
            return !string.IsNullOrWhiteSpace(fallbackPath) && File.Exists(fallbackPath)
                ? fallbackPath
                : null;
        }

        private void DisposeMenuBackgroundTexture()
        {
            _game._menuBackgroundTexture?.Dispose();
            _game._menuBackgroundTexture = null;
            _game._menuBackgroundTexturePath = null;
        }

        private void DrawMenuBackgroundAttribution()
        {
            if (string.IsNullOrWhiteSpace(_game._menuBackgroundAttributionText))
            {
                return;
            }

            var scale = _game.ViewportHeight < 540 ? 0.82f : 0.95f;
            var position = new Vector2(_game.ViewportWidth - 18f, _game.ViewportHeight - 18f);
            _game.DrawBitmapFontTextRightAligned(_game._menuBackgroundAttributionText, position + Vector2.One, Color.Black * 0.75f, scale);
            _game.DrawBitmapFontTextRightAligned(_game._menuBackgroundAttributionText, position, Color.White, scale);
        }

    }
}
