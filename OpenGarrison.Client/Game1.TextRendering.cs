#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly record struct SpriteFontDefinition(
        string SpriteName,
        char FirstCharacter,
        bool IsProportional,
        int CharacterSeparator);

    private readonly record struct SpriteFontMetrics(float CellWidth, float MaxHeight);
    private readonly record struct SpriteFontGlyphMetrics(float LeftTrim, float VisibleWidth);

    private static readonly SpriteFontDefinition BitmapFontDefinition = new("gg2FontS", '!', false, 0);
    private static readonly SpriteFontDefinition CountFontDefinition = new("countFontS", '0', false, 2);
    private static readonly SpriteFontDefinition TimerFontDefinition = new("timerFontS", '0', true, 5);

    private void DrawBitmapFontText(string text, Vector2 position, Color color, float scale = 1f)
    {
        DrawSpriteFontText(BitmapFontDefinition, text, position, color, scale);
    }

    private void DrawCountFontText(string text, Vector2 position, Color color, float scale = 1f)
    {
        DrawSpriteFontText(CountFontDefinition, text, position, color, scale);
    }

    private void DrawCountFontTextCentered(string text, Vector2 position, Color color, float scale = 1f)
    {
        var width = MeasureSpriteFontWidth(CountFontDefinition, text, scale);
        DrawCountFontText(text, new Vector2(position.X - (width / 2f), position.Y), color, scale);
    }

    private void DrawTimerFontText(string text, Vector2 position, Color color, float scale = 1f)
    {
        DrawSpriteFontText(TimerFontDefinition, text, position, color, scale);
    }

    private void DrawTimerFontTextRightAligned(string text, Vector2 position, Color color, float scale = 1f)
    {
        var width = MeasureSpriteFontWidth(TimerFontDefinition, text, scale);
        DrawTimerFontText(text, new Vector2(position.X - width, position.Y), color, scale);
    }

    private void DrawTimerFontTextRightAlignedCenteredY(string text, Vector2 position, Color color, float scale = 1f)
    {
        var width = MeasureSpriteFontWidth(TimerFontDefinition, text, scale);
        var height = MeasureSpriteFontHeight(TimerFontDefinition, scale);
        DrawTimerFontText(text, new Vector2(position.X - width, position.Y - (height / 2f)), color, scale);
    }

    private float MeasureBitmapFontWidth(string text, float scale)
    {
        return MeasureSpriteFontWidth(BitmapFontDefinition, text, scale);
    }

    private float MeasureBitmapFontHeight(float scale)
    {
        return MeasureSpriteFontHeight(BitmapFontDefinition, scale);
    }

    private void DrawSpriteFontText(SpriteFontDefinition definition, string text, Vector2 position, Color color, float scale)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (!TryGetSpriteFont(definition, out var fontSprite))
        {
            DrawConsoleTextLeftAligned(text, position, color, scale);
            return;
        }

        var metrics = GetSpriteFontMetrics(definition, fontSprite);
        var cursor = SnapTextPosition(position);
        for (var index = 0; index < text.Length; index += 1)
        {
            var character = text[index];
            var isLastCharacter = index == text.Length - 1;
            if (character == ' ')
            {
                cursor.X += GetSpriteFontSpaceAdvance(metrics) * scale;
                continue;
            }

            var frameIndex = character - definition.FirstCharacter;
            if (frameIndex < 0 || frameIndex >= fontSprite.Frames.Count)
            {
                cursor.X += GetSpriteFontSpaceAdvance(metrics) * scale;
                continue;
            }

            var frame = fontSprite.Frames[frameIndex];
            var glyphMetrics = GetSpriteFontGlyphMetrics(definition, frame);
            var drawPosition = definition.IsProportional
                ? new Vector2(cursor.X - (glyphMetrics.LeftTrim * scale), cursor.Y)
                : cursor;
            _spriteBatch.Draw(frame, SnapTextPosition(drawPosition), null, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            cursor.X += GetSpriteFontCharacterAdvance(definition, metrics, glyphMetrics.VisibleWidth, isLastCharacter) * scale;
        }
    }

    private float MeasureSpriteFontWidth(SpriteFontDefinition definition, string text, float scale)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        if (!TryGetSpriteFont(definition, out var fontSprite))
        {
            return _consoleFont.MeasureString(text).X * scale;
        }

        var metrics = GetSpriteFontMetrics(definition, fontSprite);
        var width = 0f;
        for (var index = 0; index < text.Length; index += 1)
        {
            var character = text[index];
            var isLastCharacter = index == text.Length - 1;
            if (character == ' ')
            {
                width += GetSpriteFontSpaceAdvance(metrics) * scale;
                continue;
            }

            var frameIndex = character - definition.FirstCharacter;
            if (frameIndex < 0 || frameIndex >= fontSprite.Frames.Count)
            {
                width += GetSpriteFontSpaceAdvance(metrics) * scale;
                continue;
            }

            var glyphMetrics = GetSpriteFontGlyphMetrics(definition, fontSprite.Frames[frameIndex]);
            width += GetSpriteFontCharacterAdvance(definition, metrics, glyphMetrics.VisibleWidth, isLastCharacter) * scale;
        }

        return width;
    }

    private float MeasureSpriteFontHeight(SpriteFontDefinition definition, float scale)
    {
        if (!TryGetSpriteFont(definition, out var fontSprite) || fontSprite.Frames.Count == 0)
        {
            return _consoleFont.LineSpacing * scale;
        }

        return GetSpriteFontMetrics(definition, fontSprite).MaxHeight * scale;
    }

    private bool TryGetSpriteFont(SpriteFontDefinition definition, out LoadedGameMakerSprite fontSprite)
    {
        try
        {
            var candidate = _runtimeAssets.GetSprite(definition.SpriteName);
            if (candidate is null || candidate.Frames.Count == 0)
            {
                fontSprite = null!;
                return false;
            }

            fontSprite = candidate;
            return true;
        }
        catch
        {
            fontSprite = null!;
            return false;
        }
    }

    private static Vector2 SnapTextPosition(Vector2 position)
    {
        return new Vector2(MathF.Round(position.X), MathF.Round(position.Y));
    }

    private static SpriteFontMetrics GetSpriteFontMetrics(SpriteFontDefinition definition, LoadedGameMakerSprite fontSprite)
    {
        var maxWidth = 0f;
        var maxHeight = 0f;
        for (var index = 0; index < fontSprite.Frames.Count; index += 1)
        {
            maxWidth = MathF.Max(maxWidth, fontSprite.Frames[index].Width);
            maxHeight = MathF.Max(maxHeight, fontSprite.Frames[index].Height);
        }

        var cellWidth = MathF.Max(1f, maxWidth + definition.CharacterSeparator);
        return new SpriteFontMetrics(cellWidth, MathF.Max(1f, maxHeight));
    }

    private static float GetSpriteFontCharacterAdvance(
        SpriteFontDefinition definition,
        SpriteFontMetrics metrics,
        float visibleWidth,
        bool isLastCharacter)
    {
        if (!definition.IsProportional)
        {
            return metrics.CellWidth;
        }

        return MathF.Max(1f, visibleWidth) + (isLastCharacter ? 0f : definition.CharacterSeparator);
    }

    private static float GetSpriteFontSpaceAdvance(SpriteFontMetrics metrics)
    {
        return metrics.CellWidth;
    }

    private SpriteFontGlyphMetrics GetSpriteFontGlyphMetrics(SpriteFontDefinition definition, Texture2D frame)
    {
        if (!definition.IsProportional)
        {
            return new SpriteFontGlyphMetrics(0f, frame.Width);
        }

        var bounds = GetSpriteFontOpaqueBounds(frame);
        return new SpriteFontGlyphMetrics(bounds.X, bounds.Width);
    }

    private Rectangle GetSpriteFontOpaqueBounds(Texture2D frame)
    {
        if (_spriteFontOpaqueBoundsCache.TryGetValue(frame, out var cached))
        {
            return cached;
        }

        var pixelData = new Color[frame.Width * frame.Height];
        frame.GetData(pixelData);

        var minX = frame.Width;
        var minY = frame.Height;
        var maxX = -1;
        var maxY = -1;
        for (var y = 0; y < frame.Height; y += 1)
        {
            for (var x = 0; x < frame.Width; x += 1)
            {
                if (pixelData[(y * frame.Width) + x].A <= 0)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        cached = maxX >= minX && maxY >= minY
            ? new Rectangle(minX, minY, (maxX - minX) + 1, (maxY - minY) + 1)
            : new Rectangle(0, 0, frame.Width, frame.Height);
        _spriteFontOpaqueBoundsCache[frame] = cached;
        return cached;
    }
}
