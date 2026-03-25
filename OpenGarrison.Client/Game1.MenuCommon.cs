#nullable enable

using Microsoft.Xna.Framework;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void DrawMenuInputBox(Rectangle bounds, string text, bool active)
    {
        DrawMenuInputBoxScaled(bounds, text, active, 1f);
    }

    private void DrawMenuInputBoxScaled(Rectangle bounds, string text, bool active, float textScale)
    {
        _spriteBatch.Draw(_pixel, bounds, active ? new Color(64, 68, 74) : new Color(44, 46, 52));
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 2), active ? new Color(255, 116, 116) : new Color(125, 125, 125));
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2), new Color(20, 20, 20));
        var display = active ? text + "_" : text;
        var trimmedDisplay = TrimBitmapMenuText(display, bounds.Width - 16f, textScale);
        var textY = bounds.Y + MathF.Max(4f, ((bounds.Height - MeasureBitmapFontHeight(textScale)) * 0.5f) - 1f);
        DrawBitmapFontText(trimmedDisplay, new Vector2(bounds.X + 8f, textY), Color.White, textScale);
    }

    private void DrawMenuButton(Rectangle bounds, string label, bool highlighted)
    {
        DrawMenuButtonScaled(bounds, label, highlighted, 1f);
    }

    private void DrawMenuButtonScaled(Rectangle bounds, string label, bool highlighted, float textScale)
    {
        _spriteBatch.Draw(_pixel, bounds, highlighted ? new Color(120, 50, 50) : new Color(56, 58, 64));
        var trimmedLabel = TrimBitmapMenuText(label, bounds.Width - 28f, textScale);
        var textY = bounds.Y + MathF.Max(4f, ((bounds.Height - MeasureBitmapFontHeight(textScale)) * 0.5f) - 1f);
        DrawBitmapFontText(trimmedLabel, new Vector2(bounds.X + 14f, textY), Color.White, textScale);
    }

    private string TrimBitmapMenuText(string text, float maxWidth, float scale)
    {
        if (string.IsNullOrEmpty(text) || MeasureBitmapFontWidth(text, scale) <= maxWidth)
        {
            return text;
        }

        const string ellipsis = "...";
        var trimmed = text;
        while (trimmed.Length > 0 && MeasureBitmapFontWidth(trimmed + ellipsis, scale) > maxWidth)
        {
            trimmed = trimmed[..^1];
        }

        return trimmed.Length == 0 ? ellipsis : trimmed + ellipsis;
    }
}
