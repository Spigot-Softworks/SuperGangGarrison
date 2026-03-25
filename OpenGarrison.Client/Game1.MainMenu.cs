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
        const int items = 6;

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
                OpenLobbyBrowser();
                break;
            case 2:
                _manualConnectOpen = true;
                _editingConnectHost = true;
                _editingConnectPort = false;
                _optionsMenuOpen = false;
                _controlsMenuOpen = false;
                CloseLobbyBrowser(clearStatus: false);
                _creditsOpen = false;
                _editingPlayerName = false;
                _menuStatusMessage = string.Empty;
                break;
            case 3:
                _manualConnectOpen = false;
                CloseLobbyBrowser(clearStatus: false);
                CloseCreditsMenu();
                OpenOptionsMenu(fromGameplay: false);
                break;
            case 4:
                _manualConnectOpen = false;
                CloseLobbyBrowser(clearStatus: false);
                _optionsMenuOpen = false;
                _controlsMenuOpen = false;
                _editingPlayerName = false;
                _menuStatusMessage = string.Empty;
                OpenCreditsMenu();
                break;
            case 5:
                OpenQuitPrompt();
                break;
        }
    }

    private void DrawMainMenu()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;

        if (_menuBackgroundTexture is null)
        {
            var path = ContentRoot.GetPath("Sprites", "Menu", "Title", "background.jpg");
            if (path is not null && File.Exists(path))
            {
                using var stream = File.OpenRead(path);
                _menuBackgroundTexture = Texture2D.FromStream(GraphicsDevice, stream);
            }
        }

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

        string[] items = ["Host Game", "Join (lobby)", "Join (manual)", "Options", "Credits", "Quit"];
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
            var creditsX = (viewportWidth - creditsFrame.Width * creditsScale) / 2f;
            var additionalCreditsScale = viewportHeight < 540 ? 1.35f : 1.5f;
            var additionalCreditsY = _creditsScrollY + creditsFrame.Height * creditsScale + 22f;
            var additionalCreditsColor = new Color(240, 228, 196);
            var lineHeight = MeasureBitmapFontHeight(additionalCreditsScale) + 4f;
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
        return MathF.Max(40f, ViewportHeight - 540f);
    }

    private float GetCreditsMinimumScrollY()
    {
        var creditsSprite = _runtimeAssets.GetSprite("CreditsS");
        if (creditsSprite is null || creditsSprite.Frames.Count == 0)
        {
            return GetCreditsInitialScrollY();
        }

        const float creditsScale = 2f;
        var additionalCreditsScale = ViewportHeight < 540 ? 1.35f : 1.5f;
        var additionalCreditsHeight = GetAdditionalCreditsLines().Length * (MeasureBitmapFontHeight(additionalCreditsScale) + 4f);
        var contentHeight = creditsSprite.Frames[0].Height * creditsScale + 22f + additionalCreditsHeight + 28f;
        return Math.Min(GetCreditsInitialScrollY(), ViewportHeight - 100f - contentHeight);
    }

    private static string[] GetAdditionalCreditsLines()
    {
        return
        [
            "MonoGame Port by Graves",
            "with help from Soumez",
            "and KevinKuntz",
        ];
    }
}
