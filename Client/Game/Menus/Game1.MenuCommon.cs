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

    private void DrawMenuInputBoxScaled(Rectangle bounds, string text, bool active, float textScale, int cursorIndex = -1, int selectionStart = -1)
    {
        _spriteBatch.Draw(_pixel, bounds, active ? new Color(64, 68, 74) : new Color(44, 46, 52));
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 2), active ? new Color(255, 116, 116) : new Color(125, 125, 125));
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2), new Color(20, 20, 20));

        if (active)
        {
            var borderColor = new Color(255, 255, 255, 48);
            _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), borderColor);
            _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), borderColor);
            _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), borderColor);
            _spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), borderColor);
        }

        var textAreaX = bounds.X + 8f;
        var textY = bounds.Y + MathF.Max(4f, ((bounds.Height - MeasureBitmapFontHeight(textScale)) * 0.5f) - 1f);

        if (active && HasTextSelection(cursorIndex, selectionStart))
        {
            var visibleText = TrimBitmapMenuText(text, bounds.Width - 16f, textScale);
            var (start, length) = GetTextSelectionRange(cursorIndex, selectionStart);
            if (start < visibleText.Length && length > 0)
            {
                length = Math.Min(length, visibleText.Length - start);
                var before = visibleText[..start];
                var selected = visibleText.Substring(start, length);
                var after = visibleText[(start + length)..];
                DrawBitmapFontText(before, new Vector2(textAreaX, textY), Color.White, textScale);
                var selectionX = textAreaX + MeasureBitmapFontWidth(before, textScale);
                var selectionWidth = MeasureBitmapFontWidth(selected, textScale);
                _spriteBatch.Draw(
                    _pixel,
                    new Rectangle(
                        (int)MathF.Floor(selectionX),
                        (int)MathF.Floor(textY),
                        Math.Max(1, (int)MathF.Ceiling(selectionWidth)),
                        (int)MathF.Ceiling(MeasureBitmapFontHeight(textScale))),
                    Color.White);
                DrawBitmapFontText(selected, new Vector2(selectionX, textY), Color.Black, textScale);
                DrawBitmapFontText(after, new Vector2(selectionX + selectionWidth, textY), Color.White, textScale);
                return;
            }
        }

        var display = active ? GetTextWithCursor(text, cursorIndex) : text;
        var trimmedDisplay = TrimBitmapMenuText(display, bounds.Width - 16f, textScale);
        DrawBitmapFontText(trimmedDisplay, new Vector2(textAreaX, textY), Color.White, textScale);
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
