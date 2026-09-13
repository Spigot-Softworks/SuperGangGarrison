#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private float MeasureServerPlayerTitle(PlayerServerTitleState title, float scale, bool includeTrailingSpace = true)
        => MeasureBitmapFontWidth(title.Text, scale)
            + (includeTrailingSpace ? MeasureBitmapFontWidth(" ", scale) : 0f);

    private float DrawServerPlayerTitle(
        PlayerServerTitleState title,
        Vector2 position,
        float alpha,
        float scale,
        bool includeTrailingSpace = true)
    {
        var cursorX = position.X;
        var text = title.Text ?? string.Empty;
        if (!title.Rainbow)
        {
            var color = new Color(
                (byte)((title.ColorRgb >> 16) & 0xFF),
                (byte)((title.ColorRgb >> 8) & 0xFF),
                (byte)(title.ColorRgb & 0xFF));
            DrawBitmapFontText(text, position, color * alpha, scale);
            cursorX += MeasureBitmapFontWidth(text, scale);
        }
        else
        {
            for (var index = 0; index < text.Length; index += 1)
            {
                var character = text[index].ToString();
                DrawBitmapFontText(
                    character,
                    new Vector2(cursorX, position.Y),
                    GetServerTitleRainbowColor(index, text.Length) * alpha,
                    scale);
                cursorX += MeasureBitmapFontWidth(character, scale);
            }
        }

        if (includeTrailingSpace) cursorX += MeasureBitmapFontWidth(" ", scale);
        return cursorX;
    }

    internal static Color GetServerTitleRainbowColor(int index, int count)
    {
        var hue = count <= 1 ? 0f : (Math.Max(0, index) % count) / (float)count;
        var sector = hue * 6f;
        var whole = (int)MathF.Floor(sector) % 6;
        var fraction = sector - MathF.Floor(sector);
        var rising = (byte)MathF.Round(255f * fraction);
        var falling = (byte)(255 - rising);
        return whole switch
        {
            0 => new Color(255, rising, 0),
            1 => new Color(falling, 255, 0),
            2 => new Color(0, 255, rising),
            3 => new Color(0, falling, 255),
            4 => new Color(rising, 0, 255),
            _ => new Color(255, 0, falling),
        };
    }
}
