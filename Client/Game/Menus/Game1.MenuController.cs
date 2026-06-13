#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
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

        public void Update(GameTime gameTime, KeyboardState keyboard, MouseState mouse)
        {
            EnsureMenuMusicPlaying();
            _game.StopFaucetMusic();
            _game.StopIngameMusic();

            // Update animated menu background if enabled
            if (_game._menuBackgroundMode != MenuBackgroundMode.Static)
            {
                _game._animatedMenuBackgroundController.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
            }

            // Update bottom bar runners
            _game._menuBottomBarRunners.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

            _game.UpdateLobbyBrowserResponses();
            _game.UpdateGarrisonBuilderEditor(keyboard, mouse, 1f / 60f);
            if (_game._builderEditorEnabled)
            {
                return;
            }

            if (_game._mainMenuChromeHidden)
            {
                _game._mainMenuHoverIndex = -1;
                _game._mainMenuBottomBarHover = false;
                return;
            }

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

            // Draw animated or static background based on setting
            if (_game._menuBackgroundMode != MenuBackgroundMode.Static)
            {
                _game._animatedMenuBackgroundController.Draw(viewportWidth, viewportHeight);
            }
            else
            {
                EnsureMenuBackgroundTexture(viewportWidth, viewportHeight);

                if (_game._menuBackgroundTexture is not null)
                {
                    _game.DrawLoadedSpriteFrame(_game._menuBackgroundTexture, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.White);
                }
                else if (!_game.TryDrawScreenSprite("MenuBackgroundS", _game._menuImageFrame, new Vector2(viewportWidth / 2f, viewportHeight / 2f), Color.White, Vector2.One))
                {
                    _game._spriteBatch.Draw(_game._pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), new Color(26, 24, 20));
                }
            }

            if (_game._mainMenuChromeHidden && !_game._builderEditorEnabled)
            {
                _game.DrawMainMenuBottomBar();
                return;
            }

            DrawMenuBackgroundAttribution();

            // Draw OpenGarrison logo in top right when using animated background
            if (_game._menuBackgroundMode != MenuBackgroundMode.Static)
            {
                DrawAnimatedMenuLogo(viewportWidth);
            }

            if (_game._builderEditorEnabled)
            {
                var mouse = _game.GetScaledMouseState(_game.GetConstrainedMouseState(Game1.GetCurrentMouseState()));
                _game.DrawGarrisonBuilderEditorOverlay(mouse);
                _game.DrawDevMessagePopup();
                return;
            }

            var activeOverlay = _game._mainMenuOverlayController.GetActiveOverlay();
            if (activeOverlay != MainMenuOverlayKind.None)
            {
                var buttons = _game.BuildMainMenuButtons();
                _game.LogBrowserMenuState(buttons.Count);
                _game.DrawCurrentMainMenuPage(buttons);
                _game._mainMenuOverlayController.TryDraw();
            }
            else
            {
                var buttons = _game.BuildMainMenuButtons();
                _game.LogBrowserMenuState(buttons.Count);
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
            if (_game.IsKeyPressed(keyboard, Keys.Escape) || _game.IsControllerMenuBackPressed())
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
            var mouseHoverIndex = -1;
            var mouseBottomBarHover = false;
            for (var index = 0; index < buttons.Count; index += 1)
            {
                if (!buttons[index].Bounds.Contains(mouse.Position))
                {
                    continue;
                }

                mouseHoverIndex = index;
                mouseBottomBarHover = buttons[index].IsBottomBarButton;
                break;
            }

            if (_game.ShouldUseMouseMenuHover(mouse) && mouseHoverIndex >= 0)
            {
                _game._mainMenuHoverIndex = mouseHoverIndex;
                _game._mainMenuBottomBarHover = mouseBottomBarHover;
            }
            else if (!_game.IsControllerMenuInputActive())
            {
                _game._mainMenuHoverIndex = -1;
                _game._mainMenuBottomBarHover = false;
            }

            if (_game.TryConsumeControllerMenuNavigation(out var horizontalStep, out var verticalStep) && buttons.Count > 0)
            {
                var step = verticalStep != 0 ? verticalStep : horizontalStep;
                if (step != 0)
                {
                    _game._mainMenuHoverIndex = MoveControllerMenuSelection(_game._mainMenuHoverIndex, buttons.Count, step);
                    _game._mainMenuBottomBarHover = buttons[_game._mainMenuHoverIndex].IsBottomBarButton;
                }
            }
            else if (_game.IsControllerMenuInputActive() && buttons.Count > 0 && _game._mainMenuHoverIndex < 0)
            {
                _game._mainMenuHoverIndex = 0;
                _game._mainMenuBottomBarHover = buttons[0].IsBottomBarButton;
            }

            var clickPressed = mouse.LeftButton == ButtonState.Pressed && _game._previousMouse.LeftButton != ButtonState.Pressed;
            if ((clickPressed || _game.IsControllerMenuConfirmPressed()) && _game._mainMenuHoverIndex >= 0)
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
                _game._menuBackgroundTexture = _game.LoadSpriteFrameFromPath(path);
                if (_game._menuBackgroundTexture is null)
                {
                    throw new InvalidOperationException("The menu background bytes were unavailable.");
                }
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
                && _game.CanLoadSpriteFrameFromPath(pluginOverride.ImagePath))
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
            return ContentRoot.GetPath("Sprites", "Menu", "Title", fileName);
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

        private void DrawAnimatedMenuLogo(int viewportWidth)
        {
            var sprite = _game.GetResolvedSprite("OpenGarrisonLogoS");
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return;
            }

            const float padding = 20f;
            const float logoScale = 3f;
            const int logoSourceWidth = 169;
            const int logoSourceHeight = 40;

            var frame = sprite.Frames[0];
            var visibleLogoSource = new Rectangle(
                0,
                0,
                Math.Min(logoSourceWidth, frame.Width),
                Math.Min(logoSourceHeight, frame.Height));
            var logoX = MathF.Max(padding, viewportWidth - (visibleLogoSource.Width * logoScale) - padding);
            var logoY = padding;

            _game.DrawLoadedSpriteFrame(
                frame,
                new Vector2(logoX, logoY),
                visibleLogoSource,
                Color.White,
                0f,
                Vector2.Zero,
                new Vector2(logoScale, logoScale),
                SpriteEffects.None,
                0f);
        }

    }
}
