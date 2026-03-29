#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void OpenQuitPrompt()
    {
        _quitPromptOpen = true;
        _quitPromptHoverIndex = -1;
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

        if (IsKeyPressed(keyboard, Keys.Escape))
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
        _quitPromptHoverIndex = confirmBounds.Contains(mouse.Position)
            ? 0
            : cancelBounds.Contains(mouse.Position)
                ? 1
                : -1;

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
        var titleScale = compactLayout ? 0.94f : 1f;
        var textScale = compactLayout ? 0.84f : 0.92f;
        var buttonScale = compactLayout ? 0.88f : 1f;
        _spriteBatch.Draw(_pixel, panel, new Color(34, 35, 39, 242));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, panel.Width, 3), new Color(210, 210, 210));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Bottom - 3, panel.Width, 3), new Color(76, 76, 76));

        DrawBitmapFontTextCentered(
            "Are you sure you want to quit?",
            new Vector2(panel.Center.X, panel.Y + (compactLayout ? 34f : 38f)),
            Color.White,
            titleScale);

        DrawMenuButtonScaled(confirmBounds, "Quit", _quitPromptHoverIndex == 0, buttonScale);
        DrawMenuButtonScaled(cancelBounds, "Cancel", _quitPromptHoverIndex == 1, buttonScale);
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
        var buttonHeight = compactLayout ? 36 : 42;
        var buttonWidth = (panel.Width - (padding * 2) - gap) / 2;
        var buttonY = panel.Bottom - padding - buttonHeight;
        confirmBounds = new Rectangle(panel.X + padding, buttonY, buttonWidth, buttonHeight);
        cancelBounds = new Rectangle(confirmBounds.Right + gap, buttonY, buttonWidth, buttonHeight);
    }
}
