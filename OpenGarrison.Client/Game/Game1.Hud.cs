#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void DrawBitmapFontTextCentered(string text, Vector2 position, Color color, float scale)
    {
        var width = MeasureBitmapFontWidth(text, scale);
        DrawBitmapFontText(text, new Vector2(position.X - (width / 2f), position.Y), color, scale);
    }

    private void DrawBitmapFontTextRightAligned(string text, Vector2 position, Color color, float scale)
    {
        var width = MeasureBitmapFontWidth(text, scale);
        DrawBitmapFontText(text, new Vector2(position.X - width, position.Y), color, scale);
    }

    private bool IsKeyPressed(KeyboardState keyboard, Keys key)
    {
        return keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
    }

    private void UpdateScoreboardState(KeyboardState keyboard)
    {
        _scoreboardOpen = !_mainMenuOpen
            && !_inGameMenuOpen
            && !_optionsMenuOpen
            && !_pluginOptionsMenuOpen
            && !_controlsMenuOpen
            && !_consoleOpen
            && !_teamSelectOpen
            && !_classSelectOpen
            && keyboard.IsKeyDown(_inputBindings.ShowScoreboard);

        if (_scoreboardOpen)
        {
            if (_scoreboardAlpha < 0.99f)
            {
                _scoreboardAlpha = AdvanceOpeningAlpha(_scoreboardAlpha, 0.02f, 0.99f);
            }

            return;
        }

        if (_scoreboardAlpha > 0.02f)
        {
            _scoreboardAlpha = AdvanceClosingAlpha(_scoreboardAlpha, 0.02f);
        }
    }
}
