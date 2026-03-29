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
    private void UpdateMainMenu(MouseState mouse)
    {
        const float xbegin = 40f;
        const float ybegin = 300f;
        const float spacing = 30f;
        const float width = 200f;
        const int items = 7;

        if (mouse.X > xbegin && mouse.X < xbegin + width)
        {
            _mainMenuHoverIndex = (int)MathF.Round((mouse.Y - ybegin) / spacing);
            if (_mainMenuHoverIndex < 0 || _mainMenuHoverIndex >= items)
            {
                _mainMenuHoverIndex = -1;
            }
        }
        else
        {
            _mainMenuHoverIndex = -1;
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed || _mainMenuHoverIndex < 0)
        {
            return;
        }

        switch (_mainMenuHoverIndex)
        {
            case 0:
                OpenHostSetupMenu();
                break;
            case 1:
                OpenPracticeSetupMenu();
                break;
            case 2:
                OpenLobbyBrowser();
                break;
            case 3:
                _manualConnectOpen = true;
                _editingConnectHost = true;
                _editingConnectPort = false;
                _optionsMenuOpen = false;
                _pluginOptionsMenuOpen = false;
                _controlsMenuOpen = false;
                CloseLobbyBrowser(clearStatus: false);
                _creditsOpen = false;
                _editingPlayerName = false;
                _menuStatusMessage = string.Empty;
                break;
            case 4:
                _manualConnectOpen = false;
                CloseLobbyBrowser(clearStatus: false);
                CloseCreditsMenu();
                OpenOptionsMenu(fromGameplay: false);
                break;
            case 5:
                _manualConnectOpen = false;
                CloseLobbyBrowser(clearStatus: false);
                _optionsMenuOpen = false;
                _pluginOptionsMenuOpen = false;
                _controlsMenuOpen = false;
                _editingPlayerName = false;
                _menuStatusMessage = string.Empty;
                OpenCreditsMenu();
                break;
            case 6:
                OpenQuitPrompt();
                break;
        }
    }

    private void DrawMainMenu()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;

        EnsureMenuBackgroundTexture(viewportWidth, viewportHeight);

        if (_menuBackgroundTexture is not null)
        {
            _spriteBatch.Draw(_menuBackgroundTexture, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.White);
        }
        else if (!TryDrawScreenSprite("MenuBackgroundS", _menuImageFrame, new Vector2(viewportWidth / 2f, viewportHeight / 2f), Color.White, Vector2.One))
        {
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), new Color(26, 24, 20));
        }

        if (_optionsMenuOpen)
        {
            DrawOptionsMenu();
            DrawDevMessagePopup();
            return;
        }

        if (_pluginOptionsMenuOpen)
        {
            DrawPluginOptionsMenu();
            DrawDevMessagePopup();
            return;
        }

        if (_controlsMenuOpen)
        {
            DrawControlsMenu();
            DrawDevMessagePopup();
            return;
        }

        if (_hostSetupOpen)
        {
            DrawHostSetupMenu();
            DrawDevMessagePopup();
            return;
        }

        if (_practiceSetupOpen)
        {
            DrawPracticeSetupMenu();
            DrawDevMessagePopup();
            return;
        }

        if (_creditsOpen)
        {
            DrawCreditsMenu();
            DrawDevMessagePopup();
            return;
        }

        if (_lobbyBrowserOpen)
        {
            DrawLobbyBrowserMenu();
            DrawDevMessagePopup();
            return;
        }

        if (_manualConnectOpen)
        {
            DrawManualConnectMenu();
            DrawDevMessagePopup();
            return;
        }

        string[] items = ["Host Game", "Practice", "Join (lobby)", "Join (manual)", "Options", "Credits", "Quit"];
        var position = new Vector2(40f, 300f);
        for (var index = 0; index < items.Length; index += 1)
        {
            var color = index == _mainMenuHoverIndex ? Color.Red : Color.White;
            DrawBitmapFontText(items[index], position, color, 1f);
            position.Y += 30f;
        }

        DrawMenuStatusText();
        DrawQuitPrompt();
        DrawDevMessagePopup();
    }

    private void EnsureMenuBackgroundTexture(int viewportWidth, int viewportHeight)
    {
        var path = GetMenuBackgroundPath(viewportWidth, viewportHeight);
        if (string.IsNullOrWhiteSpace(path))
        {
            DisposeMenuBackgroundTexture();
            return;
        }

        if (_menuBackgroundTexture is not null
            && string.Equals(_menuBackgroundTexturePath, path, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        DisposeMenuBackgroundTexture();
        using var stream = File.OpenRead(path);
        _menuBackgroundTexture = Texture2D.FromStream(GraphicsDevice, stream);
        _menuBackgroundTexturePath = path;
    }

    private string? GetMenuBackgroundPath(int viewportWidth, int viewportHeight)
    {
        var aspectRatio = viewportHeight <= 0 ? (16f / 9f) : viewportWidth / (float)viewportHeight;
        var fileName = aspectRatio <= 1.27f
            ? "background-5x4.jpg"
            : aspectRatio <= 1.4f
                ? "background-4x3.jpg"
                : "background.jpg";
        var path = ContentRoot.GetPath("Sprites", "Menu", "Title", fileName);
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            return path;
        }

        var fallbackPath = ContentRoot.GetPath("Sprites", "Menu", "Title", "background.jpg");
        return !string.IsNullOrWhiteSpace(fallbackPath) && File.Exists(fallbackPath)
            ? fallbackPath
            : null;
    }

    private void DisposeMenuBackgroundTexture()
    {
        _menuBackgroundTexture?.Dispose();
        _menuBackgroundTexture = null;
        _menuBackgroundTexturePath = null;
    }

    private void UpdateCreditsMenu(KeyboardState keyboard, MouseState mouse)
    {
        EnsureCreditsViewState();
        if (keyboard.IsKeyDown(Keys.Escape) && !_previousKeyboard.IsKeyDown(Keys.Escape))
        {
            CloseCreditsMenu();
            return;
        }

        var panel = GetCreditsPanelBounds();
        var backBounds = new Rectangle(panel.X + 30, panel.Bottom - 62, 180, 42);
        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (clickPressed && backBounds.Contains(mouse.Position))
        {
            CloseCreditsMenu();
            return;
        }

        var wheelDelta = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        var scrollStep = 30f;
        if (wheelDelta > 0)
        {
            _creditsScrollY = Math.Min(GetCreditsInitialScrollY(), _creditsScrollY + scrollStep);
        }
        else if (wheelDelta < 0)
        {
            _creditsScrollY = Math.Max(GetCreditsMinimumScrollY(), _creditsScrollY - scrollStep);
        }
        else
        {
            _creditsScrollY = Math.Max(GetCreditsMinimumScrollY(), _creditsScrollY - 2f);
        }
    }

    private void DrawCreditsMenu()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.82f);

        var panel = GetCreditsPanelBounds();
        var creditsSprite = _runtimeAssets.GetSprite("CreditsS");
        if (creditsSprite is not null && creditsSprite.Frames.Count > 0)
        {
            var creditsFrame = creditsSprite.Frames[0];
            const float creditsScale = 2f;
            var additionalCreditsScale = GetCreditsAdditionalTextScale();
            var additionalCreditsGap = GetCreditsAdditionalTextGap();
            var lineHeight = GetCreditsAdditionalLineHeight(additionalCreditsScale);
            var creditsX = (viewportWidth - creditsFrame.Width * creditsScale) / 2f;
            var additionalCreditsY = _creditsScrollY + creditsFrame.Height * creditsScale + additionalCreditsGap;
            var additionalCreditsColor = new Color(240, 228, 196);
            _spriteBatch.Draw(
                creditsFrame,
                new Vector2(creditsX, _creditsScrollY),
                null,
                Color.White,
                0f,
                Vector2.Zero,
                new Vector2(creditsScale, creditsScale),
                SpriteEffects.None,
                0f);

            foreach (var line in GetAdditionalCreditsLines())
            {
                DrawBitmapFontTextCentered(
                    line,
                    new Vector2(viewportWidth / 2f, additionalCreditsY),
                    additionalCreditsColor,
                    additionalCreditsScale);
                additionalCreditsY += lineHeight;
            }
        }
        else
        {
            DrawBitmapFontTextCentered("Credits unavailable", new Vector2(viewportWidth / 2f, viewportHeight / 2f), Color.White, 1.2f);
        }

        DrawMenuButton(new Rectangle(panel.X + 30, panel.Bottom - 62, 180, 42), "Back", false);
    }

    private void DrawMenuStatusText()
    {
        if (string.IsNullOrWhiteSpace(_menuStatusMessage))
        {
            return;
        }

        DrawBitmapFontText(_menuStatusMessage, new Vector2(40f, 520f), new Color(235, 225, 180), 1f);
    }

    private Rectangle GetCreditsPanelBounds()
    {
        return new Rectangle(0, 0, ViewportWidth, ViewportHeight);
    }

    private void OpenCreditsMenu()
    {
        _creditsOpen = true;
        _creditsScrollInitialized = false;
    }

    private void CloseCreditsMenu()
    {
        _creditsOpen = false;
        _creditsScrollInitialized = false;
    }

    private void EnsureCreditsViewState()
    {
        if (_creditsScrollInitialized)
        {
            return;
        }

        _creditsScrollY = GetCreditsInitialScrollY();
        _creditsScrollInitialized = true;
    }

    private float GetCreditsInitialScrollY()
    {
        var panel = GetCreditsPanelBounds();
        var availableTop = 28f;
        var availableBottom = panel.Bottom - 92f;
        var contentHeight = GetCreditsContentHeight();
        return availableTop + MathF.Max(0f, (availableBottom - availableTop - contentHeight) * 0.5f);
    }

    private float GetCreditsMinimumScrollY()
    {
        var panel = GetCreditsPanelBounds();
        var availableBottom = panel.Bottom - 92f;
        return Math.Min(GetCreditsInitialScrollY(), availableBottom - GetCreditsContentHeight());
    }

    private float GetCreditsContentHeight()
    {
        var creditsSprite = _runtimeAssets.GetSprite("CreditsS");
        if (creditsSprite is null || creditsSprite.Frames.Count == 0)
        {
            return 0f;
        }

        const float creditsScale = 2f;
        var contentHeight = creditsSprite.Frames[0].Height * creditsScale;
        if (GetAdditionalCreditsLines().Length == 0)
        {
            return contentHeight;
        }

        return contentHeight
            + GetCreditsAdditionalTextGap()
            + (GetAdditionalCreditsLines().Length * GetCreditsAdditionalLineHeight(GetCreditsAdditionalTextScale()));
    }

    private float GetCreditsAdditionalTextScale()
    {
        return ViewportHeight < 540 ? 1.35f : 1.5f;
    }

    private float GetCreditsAdditionalLineHeight(float scale)
    {
        return MeasureBitmapFontHeight(scale) + 4f;
    }

    private static float GetCreditsAdditionalTextGap()
    {
        return 22f;
    }

    private static string[] GetAdditionalCreditsLines()
    {
        return
        [
            "MonoGame Port by Graves",
            "with help from Soumeh",
            "and KevinKuntz",
        ];
    }
}
