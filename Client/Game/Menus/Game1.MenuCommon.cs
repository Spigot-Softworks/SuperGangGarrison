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

    private void DrawMenuInputBoxScaled(
        Rectangle bounds,
        string text,
        bool active,
        float textScale,
        int cursorIndex = -1,
        int selectionStart = -1,
        bool enabled = true)
    {
        var fillColor = !enabled
            ? new Color(42, 38, 36)
            : active
                ? new Color(67, 60, 55)
                : new Color(53, 47, 42);
        var borderColor = enabled ? new Color(213, 205, 188) : new Color(120, 114, 108);
        _spriteBatch.Draw(_pixel, bounds, fillColor);
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

        var display = active && enabled ? GetTextWithCursor(text, cursorIndex) : text;
        var trimmedDisplay = TrimBitmapMenuText(display, bounds.Width - 16f, textScale);
        var textColor = enabled ? Color.White : new Color(118, 114, 108);
        DrawBitmapFontText(trimmedDisplay, new Vector2(textAreaX, textY), textColor, textScale);
    }

    private void DrawMenuSelectorValueScaled(Rectangle bounds, string text, bool highlighted, float textScale, bool enabled = true)
    {
        var fillColor = !enabled
            ? new Color(42, 38, 36)
            : highlighted
                ? new Color(77, 69, 63)
                : new Color(54, 47, 41);
        var outlineColor = enabled ? new Color(213, 205, 188) : new Color(120, 114, 108);
        DrawRoundedRectangleOutline(bounds, fillColor, outlineColor, outlineThickness: 2, radius: 8);

        var textAreaX = bounds.X + 8f;
        var textY = bounds.Y + MathF.Max(4f, ((bounds.Height - MeasureBitmapFontHeight(textScale)) * 0.5f) - 1f);
        var trimmedDisplay = TrimBitmapMenuText(text, bounds.Width - 16f, textScale);
        var textColor = enabled ? Color.White : new Color(118, 114, 108);
        DrawBitmapFontText(trimmedDisplay, new Vector2(textAreaX, textY), textColor, textScale);
    }

    private void DrawMenuButton(Rectangle bounds, string label, bool highlighted)
    {
        DrawMenuButtonScaled(bounds, label, highlighted, 1f);
    }

    private void DrawMenuButtonScaled(Rectangle bounds, string label, bool highlighted, float textScale, bool enabled = true)
    {
        var fillColor = !enabled
            ? new Color(42, 38, 36)
            : highlighted
                ? new Color(77, 69, 63)
                : new Color(54, 47, 41);
        var outlineColor = enabled ? new Color(213, 205, 188) : new Color(120, 114, 108);
        DrawRoundedRectangleOutline(bounds, fillColor, outlineColor, outlineThickness: 2, radius: 8);

        var fittedScale = GetBitmapFontScaleToFit(label, bounds.Width - 28f, bounds.Height - 8f, textScale);
        var textY = bounds.Y + MathF.Max(4f, ((bounds.Height - MeasureBitmapFontHeight(fittedScale)) * 0.5f) - 1f);
        var textColor = enabled ? Color.White : new Color(118, 114, 108);
        DrawBitmapFontText(label, new Vector2(bounds.X + 14f, textY), textColor, fittedScale);
    }

    private void DrawMenuButtonCentered(Rectangle bounds, string label, bool highlighted, float textScale, bool enabled = true)
    {
        var fillColor = !enabled
            ? new Color(42, 38, 36)
            : highlighted
                ? new Color(77, 69, 63)
                : new Color(54, 47, 41);
        var outlineColor = enabled ? new Color(213, 205, 188) : new Color(120, 114, 108);
        DrawRoundedRectangleOutline(bounds, fillColor, outlineColor, outlineThickness: 2, radius: 8);

        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        var fittedScale = GetBitmapFontScaleToFit(label, bounds.Width - 16f, bounds.Height - 8f, textScale);
        var measuredWidth = MeasureBitmapFontWidth(label, fittedScale);
        var textX = bounds.X + ((bounds.Width - measuredWidth) * 0.5f);
        var textY = bounds.Y + MathF.Max(4f, ((bounds.Height - MeasureBitmapFontHeight(fittedScale)) * 0.5f) - 1f);
        var textColor = enabled ? Color.White : new Color(118, 114, 108);
        DrawBitmapFontText(label, new Vector2(textX, textY), textColor, fittedScale);
    }

    private float GetBitmapFontScaleToFit(string text, float maxWidth, float maxHeight, float baseScale = 1f)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return baseScale;
        }

        var measuredWidth = MeasureBitmapFontWidth(text, 1f);
        var measuredHeight = MeasureBitmapFontHeight(1f);
        if (measuredWidth <= 0f || measuredHeight <= 0f)
        {
            return baseScale;
        }

        var widthScale = maxWidth / measuredWidth;
        var heightScale = maxHeight / measuredHeight;
        var uniformScale = GetUniformBitmapFontScale();
        var fitted = MathF.Min(baseScale, MathF.Min(widthScale, heightScale));
        return MathF.Max(uniformScale, fitted);
    }

    private float GetMinimumLegibleBitmapFontScale()
    {
        return GetUniformBitmapFontScale();
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

    // Draws fill first, then only the border ring pixels on top (outline does not appear behind the fill).
    private void DrawRoundedRectangleFillThenBorder(Rectangle bounds, Color fillColor, Color outlineColor, int outlineThickness, int radius)
    {
        var innerRadius = Math.Max(0, radius - outlineThickness);
        var inner = new Rectangle(
            bounds.X + outlineThickness,
            bounds.Y + outlineThickness,
            bounds.Width - (outlineThickness * 2),
            bounds.Height - (outlineThickness * 2));

        if (inner.Width > 0 && inner.Height > 0)
        {
            DrawRoundedRectangle(inner, fillColor, innerRadius);
        }

        DrawRoundedRectangleBorder(bounds, outlineColor, outlineThickness, radius);
    }

    // Draws only the border ring pixels of a rounded rectangle (no fill).
    private void DrawRoundedRectangleBorder(Rectangle bounds, Color color, int outlineThickness, int radius)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var drawColor = ApplyCurrentHudElementOpacity(color);
        radius = Math.Clamp(radius, 0, Math.Min(bounds.Width, bounds.Height) / 2);
        var radiusSquared = radius * radius;

        var inner = new Rectangle(
            bounds.X + outlineThickness,
            bounds.Y + outlineThickness,
            bounds.Width - (outlineThickness * 2),
            bounds.Height - (outlineThickness * 2));
        var innerRadius = Math.Clamp(radius - outlineThickness, 0, inner.Width > 0 && inner.Height > 0 ? Math.Min(inner.Width, inner.Height) / 2 : 0);
        var innerRadiusSquared = innerRadius * innerRadius;

        for (var y = 0; y < bounds.Height; y += 1)
        {
            float outerInset;
            if (y < radius)
            {
                var dy = radius - y - 0.5f;
                outerInset = MathF.Round(radius - MathF.Sqrt(MathF.Max(0f, radiusSquared - (dy * dy))));
            }
            else if (y >= bounds.Height - radius)
            {
                var dy = y - (bounds.Height - radius) + 0.5f;
                outerInset = MathF.Round(radius - MathF.Sqrt(MathF.Max(0f, radiusSquared - (dy * dy))));
            }
            else
            {
                outerInset = 0f;
            }

            var outerRowX = bounds.X + (int)outerInset;
            var outerRowEndX = bounds.X + bounds.Width - (int)outerInset;
            var outerRowWidth = outerRowEndX - outerRowX;
            if (outerRowWidth <= 0)
            {
                continue;
            }

            var innerY = y - outlineThickness;
            int innerRowX;
            int innerRowEndX;
            if (inner.Width > 0 && inner.Height > 0 && innerY >= 0 && innerY < inner.Height)
            {
                float innerInset;
                if (innerY < innerRadius)
                {
                    var dy = innerRadius - innerY - 0.5f;
                    innerInset = MathF.Round(innerRadius - MathF.Sqrt(MathF.Max(0f, innerRadiusSquared - (dy * dy))));
                }
                else if (innerY >= inner.Height - innerRadius)
                {
                    var dy = innerY - (inner.Height - innerRadius) + 0.5f;
                    innerInset = MathF.Round(innerRadius - MathF.Sqrt(MathF.Max(0f, innerRadiusSquared - (dy * dy))));
                }
                else
                {
                    innerInset = 0f;
                }

                innerRowX = inner.X + (int)innerInset;
                innerRowEndX = inner.X + inner.Width - (int)innerInset;
            }
            else
            {
                innerRowX = outerRowEndX;
                innerRowEndX = outerRowEndX;
            }

            var leftWidth = innerRowX - outerRowX;
            if (leftWidth > 0)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(outerRowX, bounds.Y + y, leftWidth, 1), drawColor);
            }

            var rightWidth = outerRowEndX - innerRowEndX;
            if (rightWidth > 0)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(innerRowEndX, bounds.Y + y, rightWidth, 1), drawColor);
            }
        }
    }

    private void DrawRoundedRectangle(Rectangle bounds, Color color, int radius)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var drawColor = ApplyCurrentHudElementOpacity(color);
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
                _spriteBatch.Draw(_pixel, new Rectangle(rowX, bounds.Y + y, rowWidth, 1), drawColor);
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
