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
        return keyboard.IsKeyDown(key)
            && !_previousKeyboard.IsKeyDown(key)
            && !(_clientPluginHost?.IsCapturedHotkeyPressed(key) ?? false);
    }

    private static bool IsBindingDown(KeyboardState keyboard, MouseState mouse, InputBinding binding)
    {
        return InputBindingInput.IsDown(binding, keyboard, mouse);
    }

    private bool IsBindingPressed(KeyboardState keyboard, MouseState mouse, InputBinding binding)
    {
        if (binding.Kind == InputBindingKind.Keyboard
            && (_clientPluginHost?.IsCapturedHotkeyPressed(binding.Key) ?? false))
        {
            return false;
        }

        return InputBindingInput.IsPressed(binding, keyboard, _previousKeyboard, mouse, _previousMouse);
    }

    private void UpdateScoreboardState(KeyboardState keyboard, MouseState mouse)
    {
        _scoreboardOpen = CanShowGameplayScoreboard()
            && (IsBindingDown(keyboard, mouse, _inputBindings.ShowScoreboard)
                || IsControllerBindingDown(_clientSettings.ControllerScoreboardButton));

        if (_scoreboardOpen)
        {
            UpdateScoreboardPlayerCardState(mouse);
            if (_scoreboardAlpha < 0.99f)
            {
                _scoreboardAlpha = AdvanceOpeningAlpha(_scoreboardAlpha, 0.02f, 0.99f);
            }

            return;
        }

        ClearScoreboardPlayerInteractionState();
        if (_scoreboardAlpha > 0.02f)
        {
            _scoreboardAlpha = AdvanceClosingAlpha(_scoreboardAlpha, 0.02f);
        }
    }
}
