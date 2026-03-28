#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool ShouldUseSoftwareMenuCursor()
    {
        return _clientSettings.Fullscreen;
    }

    private bool ShouldDrawSoftwareMenuCursor()
    {
        if (!ShouldUseSoftwareMenuCursor())
        {
            return false;
        }

        return _mainMenuOpen
            || _passwordPromptOpen
            || _quitPromptOpen
            || _teamSelectOpen
            || _teamSelectAlpha > 0.02f
            || _classSelectOpen
            || _classSelectAlpha > 0.02f
            || _practiceSetupOpen
            || _inGameMenuOpen
            || _optionsMenuOpen
            || _pluginOptionsMenuOpen
            || _controlsMenuOpen;
    }

    private void DrawSoftwareMenuCursor(MouseState mouse)
    {
        var x = mouse.X;
        var y = mouse.Y;
        var fillColor = new Color(92, 213, 255);
        var shadowColor = Color.Black;

        DrawCursorSpan(x + 1, y + 1, 1, 9, shadowColor * 0.85f);
        DrawCursorSpan(x + 2, y + 2, 2, 1, shadowColor * 0.85f);
        DrawCursorSpan(x + 2, y + 3, 3, 1, shadowColor * 0.85f);
        DrawCursorSpan(x + 2, y + 4, 4, 1, shadowColor * 0.85f);
        DrawCursorSpan(x + 2, y + 5, 5, 1, shadowColor * 0.85f);
        DrawCursorSpan(x + 2, y + 6, 6, 1, shadowColor * 0.85f);
        DrawCursorSpan(x + 4, y + 7, 4, 1, shadowColor * 0.85f);
        DrawCursorSpan(x + 4, y + 8, 3, 1, shadowColor * 0.85f);
        DrawCursorSpan(x + 4, y + 9, 2, 1, shadowColor * 0.85f);
        DrawCursorSpan(x + 4, y + 10, 1, 1, shadowColor * 0.85f);

        DrawCursorSpan(x, y, 1, 8, fillColor);
        DrawCursorSpan(x + 1, y + 1, 2, 1, fillColor);
        DrawCursorSpan(x + 1, y + 2, 3, 1, fillColor);
        DrawCursorSpan(x + 1, y + 3, 4, 1, fillColor);
        DrawCursorSpan(x + 1, y + 4, 5, 1, fillColor);
        DrawCursorSpan(x + 1, y + 5, 6, 1, fillColor);
        DrawCursorSpan(x + 3, y + 6, 4, 1, fillColor);
        DrawCursorSpan(x + 3, y + 7, 3, 1, fillColor);
        DrawCursorSpan(x + 3, y + 8, 2, 1, fillColor);
    }

    private void DrawCursorSpan(int x, int y, int width, int height, Color color)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(x, y, width, height), color);
    }
}
