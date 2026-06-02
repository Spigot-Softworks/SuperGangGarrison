#nullable enable

using Microsoft.Xna.Framework;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const float VersionOverlayTextScale = 1f;
    private const float VersionOverlayMargin = 6f;

    private void DrawVersionOverlay()
    {
        var version = GetApplicationVersionLabel();
        if (string.IsNullOrWhiteSpace(version))
        {
            return;
        }

        var textWidth = MeasureBitmapFontWidth(version, VersionOverlayTextScale);
        var textHeight = MeasureBitmapFontHeight(VersionOverlayTextScale);
        var position = new Vector2(
            MathF.Max(0f, ViewportWidth - textWidth - VersionOverlayMargin),
            MathF.Max(0f, ViewportHeight - textHeight - VersionOverlayMargin));

        DrawBitmapFontText(version, position, Color.White, VersionOverlayTextScale);
    }
}
