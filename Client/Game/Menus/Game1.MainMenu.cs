#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void UpdateCreditsMenu(KeyboardState keyboard, MouseState mouse)
    {
        EnsureCreditsViewState();
        if ((keyboard.IsKeyDown(Keys.Escape) && !_previousKeyboard.IsKeyDown(Keys.Escape))
            || IsControllerMenuBackPressed()
            || IsControllerMenuConfirmPressed())
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
        else if (TryConsumeControllerMenuNavigation(out _, out var verticalStep) && verticalStep != 0)
        {
            _creditsScrollY = Math.Clamp(
                _creditsScrollY - (verticalStep * scrollStep),
                GetCreditsMinimumScrollY(),
                GetCreditsInitialScrollY());
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
        var creditsSprite = GetResolvedSprite("CreditsS");
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
            DrawLoadedSpriteFrame(
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

        // Draw bottom bar and runners (in animated mode only)
        if (_menuBackgroundMode != MenuBackgroundMode.Static)
        {
            DrawMainMenuBottomBar();
        }
    }

    private void DrawMainMenuBottomBar()
    {
        if (_menuBackgroundMode == MenuBackgroundMode.Static)
        {
            return;
        }

        const int bottomBarHeight = 76;
        var bottomBarBounds = new Rectangle(0, ViewportHeight - bottomBarHeight, ViewportWidth, bottomBarHeight);
        _spriteBatch.Draw(_pixel, bottomBarBounds, new Color(0x57, 0x4f, 0x47));
        _menuBottomBarRunners.Draw(bottomBarBounds);
    }

    private void DrawMenuStatusText()
    {
        if (string.IsNullOrWhiteSpace(_menuStatusMessage))
        {
            return;
        }

        var position = new Vector2(ViewportWidth * 0.5f, ViewportHeight - 104f);
        var scale = ViewportHeight < 540 ? 0.92f : 1f;
        var text = _menuStatusMessage;
        DrawBitmapFontTextCentered(text, position + Vector2.One * 2f, Color.Black * 0.6f, scale);
        DrawBitmapFontTextCentered(text, position, new Color(235, 225, 180), scale);
    }

    private void OpenManualConnectMenu()
    {
        _mainMenuOverlayStateController.OpenManualConnectMenu();
    }

    private void OpenFriendsMenu()
    {
        _mainMenuOverlayStateController.OpenFriendsMenu();
    }

    private Rectangle GetCreditsPanelBounds()
    {
        return new Rectangle(0, 0, ViewportWidth, ViewportHeight);
    }

    private void OpenCreditsMenu()
    {
        _mainMenuOverlayStateController.OpenCreditsMenu();
    }

    private void CloseCreditsMenu()
    {
        _mainMenuOverlayStateController.CloseCreditsMenu();
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
        var creditsSprite = GetResolvedSprite("CreditsS");
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
