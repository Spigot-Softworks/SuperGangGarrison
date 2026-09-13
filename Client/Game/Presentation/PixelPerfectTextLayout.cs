#nullable enable

using System;
using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

internal static class PixelPerfectTextLayout
{
    internal const float NaturalScale = 1f;

    internal static string TrimToWidth(string? text, float maximumWidth, Func<string, float> measureWidth)
    {
        ArgumentNullException.ThrowIfNull(measureWidth);

        if (string.IsNullOrEmpty(text) || float.IsPositiveInfinity(maximumWidth))
        {
            return text ?? string.Empty;
        }

        if (float.IsNaN(maximumWidth) || maximumWidth <= 0f)
        {
            return string.Empty;
        }

        if (measureWidth(text) <= maximumWidth)
        {
            return text;
        }

        var ellipsis = "...";
        while (ellipsis.Length > 0 && measureWidth(ellipsis) > maximumWidth)
        {
            ellipsis = ellipsis[..^1];
        }

        if (ellipsis.Length == 0)
        {
            return string.Empty;
        }

        for (var prefixLength = text.Length; prefixLength >= 0; prefixLength -= 1)
        {
            var candidate = text[..prefixLength] + ellipsis;
            if (measureWidth(candidate) <= maximumWidth)
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    internal static Point CenterNaturalText(Rectangle bounds, float naturalWidth, float naturalHeight)
    {
        var pixelWidth = Math.Max(0, (int)MathF.Ceiling(naturalWidth));
        var pixelHeight = Math.Max(0, (int)MathF.Ceiling(naturalHeight));
        return new Point(
            (int)MathF.Round(bounds.X + ((bounds.Width - pixelWidth) * 0.5f)),
            (int)MathF.Round(bounds.Y + ((bounds.Height - pixelHeight) * 0.5f)));
    }

    internal static float SelectLargestIntegerScale(float naturalWidth, float maximumWidth, int preferredScale)
    {
        var largestScale = Math.Max(1, preferredScale);
        if (naturalWidth <= 0f)
        {
            return largestScale;
        }

        for (var scale = largestScale; scale > 1; scale -= 1)
        {
            if (naturalWidth * scale <= maximumWidth)
            {
                return scale;
            }
        }

        return NaturalScale;
    }
}
