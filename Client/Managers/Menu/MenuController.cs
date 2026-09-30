#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using OpenGarrison.Core;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class MenuController
    {
        private readonly IMenuContext _context;

        public MenuController(IMenuContext context)
        {
            _context = context;
        }

        public void Update(GameTime gameTime, KeyboardState keyboard, MouseState mouse)
        {
            EnsureMenuMusicPlaying();
            _context.StopFaucetMusic();
            _context.StopIngameMusic();
            // Returning to any main-menu page must also release LTD's
            // gameplay loop.  The menu track and LTD gameplay track are
            // separate instances, so stopping only the generic loop can
            // leave ingame_l2d playing underneath the menu.
            _context.StopLastToDieIngameMusic();

            // Update animated menu background if enabled
            if (_context._menuBackgroundMode != MenuBackgroundMode.Static)
            {
                _context.Menus.AnimatedMenuBackground.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
            }

            _context.AdvanceBrandLogoFlame((float)gameTime.ElapsedGameTime.TotalSeconds);

            // Update bottom bar runners
            _context.Menus.MenuBottomBarRunners.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

            _context.UpdateLobbyBrowserResponses();
            _context.UpdateGarrisonBuilderEditor(keyboard, mouse, 1f / 60f);
            if (_context._builderEditorEnabled)
            {
                return;
            }

            if (_context._mainMenuChromeHidden)
            {
                _context._mainMenuHoverIndex = -1;
                _context._mainMenuBottomBarHover = false;
                return;
            }

            _context.EnsurePlayerNamePrompt();

            if (_context.UpdateDevMessagePopup(keyboard, mouse))
            {
                return;
            }

            if (_context._quitPromptOpen)
            {
                _context.UpdateQuitPrompt(keyboard, mouse);
                return;
            }

            if (!_context.Menus.MainMenuOverlay.TryUpdate(keyboard, mouse))
            {
                UpdateMainMenu(keyboard, mouse);
            }
        }

        public void Draw()
        {
            var viewportWidth = _context.ViewportWidth;
            var viewportHeight = _context.ViewportHeight;

            DrawBackground(viewportWidth, viewportHeight);

            if (_context._mainMenuChromeHidden && !_context._builderEditorEnabled)
            {
                _context.DrawMainMenuBottomBar();
                return;
            }

            DrawMenuBackgroundAttribution();

            // The brand intro draws the same logo while it moves into this
            // exact destination. Suppress the menu-owned copy until handoff.
            if (!_context._brandIntroActive)
            {
                DrawAnimatedMenuLogo(viewportWidth);
            }

            if (_context._builderEditorEnabled)
            {
                var mouse = _context.GetFrameMouseState();
                _context.DrawGarrisonBuilderEditorOverlay(mouse);
                _context.DrawDevMessagePopup();
                return;
            }

            var activeOverlay = _context.Menus.MainMenuOverlay.GetActiveOverlay();
            if (activeOverlay != MainMenuOverlayKind.None)
            {
                var buttons = _context.BuildMainMenuButtons();
                _context.LogBrowserMenuState(buttons.Count);
                _context.DrawCurrentMainMenuPage(buttons);
                _context.Menus.MainMenuOverlay.TryDraw();
            }
            else
            {
                var buttons = _context.BuildMainMenuButtons();
                _context.LogBrowserMenuState(buttons.Count);
                _context.DrawCurrentMainMenuPage(buttons);
                _context.DrawMenuStatusText();
                _context.DrawQuitPrompt();
            }

            _context.DrawDevMessagePopup();
        }

        public void DrawBackground(int viewportWidth, int viewportHeight)
        {
            // Keep modal menu surfaces on the exact same user-selected background
            // path as the main menu. In particular, do not reuse the gameplay map
            // that happens to be loaded behind a hosted Last to Die lobby.
            if (_context._menuBackgroundMode != MenuBackgroundMode.Static)
            {
                _context.Menus.AnimatedMenuBackground.Draw(viewportWidth, viewportHeight);
            }
            else
            {
                EnsureMenuBackgroundTexture(viewportWidth, viewportHeight);

                if (_context._menuBackgroundTexture is not null)
                {
                    _context.DrawLoadedSpriteFrame(_context._menuBackgroundTexture, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.White);
                }
                else if (!_context.TryDrawScreenSprite("MenuBackgroundS", _context._menuImageFrame, new Vector2(viewportWidth / 2f, viewportHeight / 2f), Color.White, Vector2.One))
                {
                    _context._spriteBatch.Draw(_context._pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), new Color(26, 24, 20));
                }
            }
        }

        public MainMenuOverlayKind GetActiveOverlay()
        {
            return _context.Menus.MainMenuOverlay.GetActiveOverlay();
        }

        private void UpdateMainMenu(KeyboardState keyboard, MouseState mouse)
        {
            if (_context.IsKeyPressed(keyboard, Keys.Escape) || _context.IsControllerMenuBackPressed())
            {
                if (_context._optionsMenuOpen)
                {
                    _context.CloseOptionsMenu();
                    return;
                }

                if (_context._mainMenuPage != MainMenuPage.Root)
                {
                    _context.OpenMainMenuPage(MainMenuPage.Root);
                }
                else
                {
                    _context.OpenQuitPrompt();
                }

                return;
            }

            if (_context._optionsMenuOpen)
            {
                return;
            }

            var buttons = _context.BuildMainMenuButtons();
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

            if (_context.ShouldUseMouseMenuHover(mouse) && mouseHoverIndex >= 0)
            {
                _context._mainMenuHoverIndex = mouseHoverIndex;
                _context._mainMenuBottomBarHover = mouseBottomBarHover;
            }
            else if (!_context.IsControllerMenuInputActive())
            {
                _context._mainMenuHoverIndex = -1;
                _context._mainMenuBottomBarHover = false;
            }

            if (_context.TryConsumeControllerMenuNavigation(out var horizontalStep, out var verticalStep) && buttons.Count > 0)
            {
                var step = verticalStep != 0 ? verticalStep : horizontalStep;
                if (step != 0)
                {
                    _context._mainMenuHoverIndex = MoveControllerMenuSelection(_context._mainMenuHoverIndex, buttons.Count, step);
                    _context._mainMenuBottomBarHover = buttons[_context._mainMenuHoverIndex].IsBottomBarButton;
                }
            }
            else if (_context.IsControllerMenuInputActive() && buttons.Count > 0 && _context._mainMenuHoverIndex < 0)
            {
                _context._mainMenuHoverIndex = 0;
                _context._mainMenuBottomBarHover = buttons[0].IsBottomBarButton;
            }

            var clickPressed = mouse.LeftButton == ButtonState.Pressed && _context._previousMouse.LeftButton != ButtonState.Pressed;
            if ((clickPressed || _context.IsControllerMenuConfirmPressed()) && _context._mainMenuHoverIndex >= 0)
            {
                buttons[_context._mainMenuHoverIndex].Activate();
            }
        }

        private void EnsureMenuMusicPlaying()
        {
            _context.EnsureMenuMusicPlaying();
        }

        private void EnsureMenuBackgroundTexture(int viewportWidth, int viewportHeight)
        {
            var (path, attributionText) = GetMenuBackgroundSelection(viewportWidth, viewportHeight);
            _context._menuBackgroundAttributionText = attributionText;
            if (string.IsNullOrWhiteSpace(path))
            {
                DisposeMenuBackgroundTexture();
                _context._menuBackgroundFailedPath = null;
                return;
            }

            if (string.Equals(_context._menuBackgroundTexturePath, path, StringComparison.OrdinalIgnoreCase))
            {
                if (_context._menuBackgroundTexture is not null
                    || string.Equals(_context._menuBackgroundFailedPath, path, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            DisposeMenuBackgroundTexture();
            _context._menuBackgroundTexturePath = path;
            _context._menuBackgroundFailedPath = null;

            try
            {
                _context._menuBackgroundTexture = _context.LoadSpriteFrameFromPath(path);
                if (_context._menuBackgroundTexture is null)
                {
                    throw new InvalidOperationException("The menu background bytes were unavailable.");
                }
            }
            catch (Exception ex)
            {
                _context._menuBackgroundFailedPath = path;
                _context._menuBackgroundAttributionText = string.Empty;
                _context.AddConsoleLine($"plugin menu background failed to load from \"{path}\": {ex.Message}");
            }
        }

        private (string? Path, string AttributionText) GetMenuBackgroundSelection(int viewportWidth, int viewportHeight)
        {
            var pluginOverride = _context.GetClientPluginMainMenuBackgroundOverride();
            if (pluginOverride is not null
                && !string.IsNullOrWhiteSpace(pluginOverride.ImagePath)
                && _context.CanLoadSpriteFrameFromPath(pluginOverride.ImagePath))
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
            _context._menuBackgroundTexture?.Dispose();
            _context._menuBackgroundTexture = null;
            _context._menuBackgroundTexturePath = null;
        }

        private void DrawMenuBackgroundAttribution()
        {
            if (string.IsNullOrWhiteSpace(_context._menuBackgroundAttributionText))
            {
                return;
            }

            var scale = _context.ViewportHeight < 540 ? 0.82f : 0.95f;
            var position = new Vector2(_context.ViewportWidth - 18f, _context.ViewportHeight - 18f);
            _context.DrawBitmapFontTextRightAligned(_context._menuBackgroundAttributionText, position + Vector2.One, Color.Black * 0.75f, scale);
            _context.DrawBitmapFontTextRightAligned(_context._menuBackgroundAttributionText, position, Color.White, scale);
        }

        private void DrawAnimatedMenuLogo(int viewportWidth)
        {
            if (ClientDistribution.IsRestricted || ClientDistribution.IsGg2Only)
            {
                var sprite = _context.GetResolvedSprite("OpenGarrisonLogoS");
                if (sprite is null || sprite.Frames.Count == 0)
                {
                    return;
                }

                var frame = sprite.Frames[0];
                var source = new Rectangle(0, 0, Math.Min(169, frame.Width), Math.Min(40, frame.Height));
                var scale = MathF.Min(3f, MathF.Max(1f, viewportWidth - 40f) / Math.Max(1, source.Width));
                _context.DrawLoadedSpriteFrame(
                    frame,
                    new Vector2(MathF.Max(20f, viewportWidth - source.Width * scale - 20f), 20f),
                    source, Color.White, 0f, Vector2.Zero, new Vector2(scale), SpriteEffects.None, 0f);
                return;
            }

            var destination = Game1.GetPermanentBrandLogoBounds(viewportWidth, _context.ViewportHeight);
            _context.DrawFlamingBrandLogo(destination);
        }

}
