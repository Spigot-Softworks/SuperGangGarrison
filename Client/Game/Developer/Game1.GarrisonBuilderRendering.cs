#nullable enable

using System;
using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const float GarrisonBuilderChatFontScale = 1f;
    private const float GarrisonBuilderLinkLineThickness = 2.5f;
    private const int GarrisonBuilderLinkEndpointSize = 7;

    private float GetGarrisonBuilderLinkVisualScale()
    {
        if (!_builderUseModernUi)
        {
            return 1f;
        }

        return MathF.Min(1f, _builderZoom);
    }

    private int BuilderViewportWidth => ViewportWidth;

    private int BuilderViewportHeight => ViewportHeight;

    private static int BuilderUi(int pixels) => pixels;

    private static float BuilderUi(float pixels) => pixels;

    private float GetGarrisonBuilderBitmapFontScale() => GarrisonBuilderChatFontScale;

    private float GetGarrisonBuilderRelativeBitmapFontScale(float relativeScale = 1f)
    {
        return GarrisonBuilderChatFontScale * relativeScale;
    }

    private float GetGarrisonBuilderMinimumButtonHeight(float relativeTextScale = 1f)
    {
        return MathF.Ceiling(MeasureBitmapFontHeight(GetGarrisonBuilderRelativeBitmapFontScale(relativeTextScale)) + 10f);
    }

    private int GetGarrisonBuilderMenuRowHeight(float relativeTextScale = 1f)
    {
        return (int)GetGarrisonBuilderMinimumButtonHeight(relativeTextScale);
    }

    private float GetGarrisonBuilderBitmapFontScaleToFit(string text, float maxWidth, float maxHeight, float relativeScale = 1f)
    {
        var baseScale = GetGarrisonBuilderRelativeBitmapFontScale(relativeScale);
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
        var fitted = MathF.Min(baseScale, MathF.Min(widthScale, heightScale));
        return MathF.Max(GarrisonBuilderChatFontScale, fitted);
    }

    private void DrawGarrisonBuilderMenuButtonCentered(Rectangle bounds, string label, bool highlighted, float relativeTextScale = 1f, bool enabled = true)
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

        const float horizontalPadding = 12f;
        const float verticalPadding = 6f;
        var fittedScale = GetGarrisonBuilderBitmapFontScaleToFit(
            label,
            Math.Max(8f, bounds.Width - (horizontalPadding * 2f)),
            Math.Max(8f, bounds.Height - (verticalPadding * 2f)),
            relativeTextScale);
        var measuredWidth = MeasureBitmapFontWidth(label, fittedScale);
        var measuredHeight = MeasureBitmapFontHeight(fittedScale);
        var textX = bounds.X + ((bounds.Width - measuredWidth) * 0.5f);
        var textY = bounds.Y + MathF.Max(verticalPadding, (bounds.Height - measuredHeight) * 0.5f);
        var textColor = enabled ? Color.White : new Color(118, 114, 108);
        DrawBitmapFontText(label, new Vector2(textX, textY), textColor, fittedScale);
    }

    private const string GarrisonBuilderCollapseChevronSymbol = "^";

    private void DrawGarrisonBuilderCollapseChevronButton(Rectangle bounds, bool expanded, bool highlighted, bool enabled = true)
    {
        DrawGarrisonBuilderMenuButtonCentered(bounds, string.Empty, highlighted, relativeTextScale: 0.85f, enabled: enabled);
        var textColor = enabled ? Color.White : new Color(118, 114, 108);
        DrawGarrisonBuilderCollapseChevron(bounds, expanded, textColor, relativeTextScale: 0.85f);
    }

    private void DrawGarrisonBuilderCollapseChevron(Rectangle bounds, bool expanded, Color textColor, float relativeTextScale = 0.85f)
    {
        var rotation = expanded ? 0f : MathF.PI;
        var fittedScale = GetGarrisonBuilderBitmapFontScaleToFit(
            GarrisonBuilderCollapseChevronSymbol,
            Math.Max(8f, bounds.Width - 4f),
            Math.Max(8f, bounds.Height - 4f),
            relativeTextScale);
        var center = new Vector2(bounds.X + (bounds.Width * 0.5f), bounds.Y + (bounds.Height * 0.5f));
        DrawBitmapFontTextCentered(GarrisonBuilderCollapseChevronSymbol, center, textColor, fittedScale, rotation);
    }
}
