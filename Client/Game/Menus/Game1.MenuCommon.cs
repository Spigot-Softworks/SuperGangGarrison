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
        _spriteBatch.Draw(_pixel, bounds, active ? new Color(67, 60, 55) : new Color(53, 47, 42));
        var borderColor = new Color(213, 205, 188);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), borderColor);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), borderColor);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), borderColor);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), borderColor);

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
        var fillColor = highlighted ? new Color(77, 69, 63) : new Color(54, 47, 41);
        var outlineColor = new Color(213, 205, 188);
        DrawRoundedRectangleOutline(bounds, fillColor, outlineColor, outlineThickness: 2, radius: 8);

        var trimmedLabel = TrimBitmapMenuText(label, bounds.Width - 28f, textScale);
        var textY = bounds.Y + MathF.Max(4f, ((bounds.Height - MeasureBitmapFontHeight(textScale)) * 0.5f) - 1f);
        DrawBitmapFontText(trimmedLabel, new Vector2(bounds.X + 14f, textY), Color.White, textScale);
    }

    private void DrawMenuButtonCentered(Rectangle bounds, string label, bool highlighted, float textScale)
    {
        var fillColor = highlighted ? new Color(77, 69, 63) : new Color(54, 47, 41);
        var outlineColor = new Color(213, 205, 188);
        DrawRoundedRectangleOutline(bounds, fillColor, outlineColor, outlineThickness: 2, radius: 8);

        var trimmedLabel = label.Length <= 1
            ? label
            : TrimBitmapMenuText(label, bounds.Width - 28f, textScale);
        var textX = bounds.X + ((bounds.Width - MeasureBitmapFontWidth(trimmedLabel, textScale)) * 0.5f);
        var textY = bounds.Y + MathF.Max(4f, ((bounds.Height - MeasureBitmapFontHeight(textScale)) * 0.5f) - 1f);
        DrawBitmapFontText(trimmedLabel, new Vector2(textX, textY), Color.White, textScale);
    }

    private void DrawRoundedRectangleOutline(Rectangle bounds, Color fillColor, Color outlineColor, int outlineThickness, int radius)
    {
        DrawRoundedRectangle(bounds, outlineColor, radius);

        var inner = new Rectangle(
            bounds.X + outlineThickness,
            bounds.Y + outlineThickness,
            bounds.Width - (outlineThickness * 2),
            bounds.Height - (outlineThickness * 2));

        if (inner.Width > 0 && inner.Height > 0)
        {
            DrawRoundedRectangle(inner, fillColor, Math.Max(0, radius - outlineThickness));
        }
    }

    private void DrawRoundedRectangle(Rectangle bounds, Color color, int radius)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        radius = Math.Clamp(radius, 0, Math.Min(bounds.Width, bounds.Height) / 2);
        var radiusSquared = radius * radius;

        for (var y = 0; y < bounds.Height; y += 1)
        {
            float inset;
            if (y < radius)
            {
                var dy = radius - y - 0.5f;
                inset = MathF.Round(radius - MathF.Sqrt(MathF.Max(0f, radiusSquared - (dy * dy))));
            }
            else if (y >= bounds.Height - radius)
            {
                var dy = y - (bounds.Height - radius) + 0.5f;
                inset = MathF.Round(radius - MathF.Sqrt(MathF.Max(0f, radiusSquared - (dy * dy))));
            }
            else
            {
                inset = 0f;
            }

            var rowX = bounds.X + (int)inset;
            var rowWidth = bounds.Width - ((int)inset * 2);

            if (rowWidth > 0)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(rowX, bounds.Y + y, rowWidth, 1), color);
            }
        }
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
