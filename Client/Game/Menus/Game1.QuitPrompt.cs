#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void OpenQuitPrompt()
    {
        _quitPromptOpen = true;
        _quitPromptHoverIndex = IsControllerMenuInputActive() ? 1 : -1;
    }

    private void CloseQuitPrompt()
    {
        _quitPromptOpen = false;
        _quitPromptHoverIndex = -1;
    }

    private bool UpdateQuitPrompt(KeyboardState keyboard, MouseState mouse)
    {
        if (!_quitPromptOpen)
        {
            return false;
        }

        if (IsKeyPressed(keyboard, Keys.Escape) || IsControllerMenuBackPressed())
        {
            CloseQuitPrompt();
            return true;
        }

        if (IsKeyPressed(keyboard, Keys.Enter))
        {
            Exit();
            return true;
        }

        GetQuitPromptLayout(out _, out var confirmBounds, out var cancelBounds, out _);
        if (TryConsumeControllerMenuNavigation(out var horizontalStep, out _) && horizontalStep != 0)
        {
            _quitPromptHoverIndex = MoveControllerMenuSelectionClamped(_quitPromptHoverIndex, 2, horizontalStep);
        }
        else if (IsControllerMenuInputActive() && _quitPromptHoverIndex < 0)
        {
            _quitPromptHoverIndex = 1;
        }

        if (ShouldUseMouseMenuHover(mouse))
        {
            _quitPromptHoverIndex = confirmBounds.Contains(mouse.Position)
                ? 0
                : cancelBounds.Contains(mouse.Position)
                    ? 1
                    : _quitPromptHoverIndex;
        }

        if (IsControllerMenuConfirmPressed())
        {
            if (_quitPromptHoverIndex == 0)
            {
                Exit();
                return true;
            }

            CloseQuitPrompt();
            return true;
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed)
        {
            return true;
        }

        if (_quitPromptHoverIndex == 0)
        {
            Exit();
            return true;
        }

        if (_quitPromptHoverIndex == 1)
        {
            CloseQuitPrompt();
            return true;
        }

        CloseQuitPrompt();
        return true;
    }

    private void DrawQuitPrompt()
    {
        if (!_quitPromptOpen)
        {
            return;
        }

        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.76f);

        GetQuitPromptLayout(out var panel, out var confirmBounds, out var cancelBounds, out var compactLayout);
        const float titleScale = 1f;
        const float buttonScale = 1f;
        DrawRoundedRectangleOutline(panel, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 2, radius: 8);

        DrawBitmapFontTextCentered(
            "Are you sure you want to quit?",
            new Vector2(panel.Center.X, panel.Y + (compactLayout ? 34f : 42f)),
            Color.White,
            titleScale);

        DrawQuitPromptButton(confirmBounds, "Quit", _quitPromptHoverIndex == 0, buttonScale);
        DrawQuitPromptButton(cancelBounds, "Cancel", _quitPromptHoverIndex == 1, buttonScale);
    }

    private void GetQuitPromptLayout(
        out Rectangle panel,
        out Rectangle confirmBounds,
        out Rectangle cancelBounds,
        out bool compactLayout)
    {
        var maxWidth = ViewportWidth < 860 ? 420 : 460;
        var maxHeight = ViewportHeight < 540 ? 170 : 190;
        panel = new Rectangle(
            (ViewportWidth - System.Math.Min(maxWidth, ViewportWidth - 32)) / 2,
            (ViewportHeight - System.Math.Min(maxHeight, ViewportHeight - 32)) / 2,
            System.Math.Min(maxWidth, ViewportWidth - 32),
            System.Math.Min(maxHeight, ViewportHeight - 32));

        compactLayout = panel.Width < 440 || panel.Height < 182;
        var padding = compactLayout ? 20 : 24;
        var gap = compactLayout ? 12 : 16;
        var buttonHeight = compactLayout ? 44 : 50;
        var buttonWidth = (panel.Width - (padding * 2) - gap) / 2;
        var buttonY = panel.Bottom - padding - buttonHeight;
        confirmBounds = new Rectangle(panel.X + padding, buttonY, buttonWidth, buttonHeight);
        cancelBounds = new Rectangle(confirmBounds.Right + gap, buttonY, buttonWidth, buttonHeight);
    }

    private void DrawQuitPromptButton(Rectangle bounds, string label, bool highlighted, float textScale)
    {
        var fillColor = highlighted ? new Color(97, 89, 82) : new Color(74, 67, 61);
        var outlineColor = new Color(213, 205, 188);
        DrawRoundedRectangleOutline(bounds, fillColor, outlineColor, outlineThickness: 2, radius: 8);

        var trimmedLabel = TrimBitmapMenuText(label, bounds.Width - 28f, textScale);
        DrawBitmapFontTextCentered(
            trimmedLabel,
            new Vector2(bounds.Center.X, bounds.Y + ((bounds.Height - MeasureBitmapFontHeight(textScale)) * 0.5f) - 1f),
            Color.White,
            textScale);
    }
}
