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

    private void DrawBitmapFontTextCentered(string text, Vector2 position, Color color, float scale, float rotation)
    {
        var width = MeasureBitmapFontWidth(text, scale);
        var height = MeasureBitmapFontHeight(scale);
        var topLeft = new Vector2(position.X - (width / 2f), position.Y);
        var rotationCenter = new Vector2(position.X, position.Y + (height / 2f));
        DrawSpriteFontText(BitmapFontDefinition, text, topLeft, color, scale, rotation, rotationCenter);
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
        _scoreboardOpen = CanShowGameplayScoreboard()
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
