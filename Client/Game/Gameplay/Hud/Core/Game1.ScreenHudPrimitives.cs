#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void DrawScreenHealthBar(Rectangle rectangle, float value, float maxValue, bool useTeamColors, Color? fillColor = null, Color? backColor = null, HudFillDirection fillDirection = HudFillDirection.HorizontalLeftToRight)
    {
        var resolvedBackColor = backColor ?? Color.Black;
        _spriteBatch.Draw(_pixel, rectangle, resolvedBackColor);
        if (maxValue <= 0f)
        {
            return;
        }

        var fillWidth = Math.Clamp((int)MathF.Round(rectangle.Width * MathF.Max(0f, value) / maxValue), 0, rectangle.Width);
        if (fillWidth <= 0)
        {
            return;
        }

        var resolvedFillColor = fillColor ?? (useTeamColors
            ? GetUberOverlayColor(_world.LocalPlayer.Team)
            : Color.Lerp(Color.Red, Color.LimeGreen, MathF.Min(1f, MathF.Max(0f, value / maxValue))));
        var fillFraction = MathF.Min(1f, MathF.Max(0f, value / maxValue));
        Rectangle fillRectangle;
        if (fillDirection == HudFillDirection.VerticalBottomToTop)
        {
            var fillHeight = Math.Clamp((int)MathF.Round(rectangle.Height * fillFraction), 0, rectangle.Height);
            if (fillHeight <= 0)
            {
                return;
            }

            fillRectangle = new Rectangle(rectangle.X, rectangle.Bottom - fillHeight, rectangle.Width, fillHeight);
        }
        else
        {
            fillRectangle = new Rectangle(rectangle.X, rectangle.Y, fillWidth, rectangle.Height);
        }

        _spriteBatch.Draw(_pixel, fillRectangle, resolvedFillColor);
    }

    private enum HudFillDirection
    {
        HorizontalLeftToRight,
        VerticalBottomToTop,
    }

    private void DrawHudTextCentered(string text, Vector2 position, Color color, float scale)
    {
        var width = MeasureBitmapFontWidth(text, scale);
        var height = MeasureBitmapFontHeight(scale);
        DrawBitmapFontText(text, new Vector2(position.X - (width / 2f), position.Y - (height / 2f)), color, scale);
    }

    private void DrawMenuTextCentered(string text, Vector2 position, Color color, float scale)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (_menuBitmapFontTexture is not null && _menuBitmapFontGlyphs.Count > 0)
        {
            var width = MeasureMenuBitmapFontWidth(text, scale);
            var height = MeasureMenuBitmapFontHeight(scale);
            var drawPosition = new Vector2(
                MathF.Round(position.X - (width / 2f)),
                MathF.Round(position.Y - (height / 2f)));
            DrawMenuBitmapFontText(text, drawPosition, color, scale);
            return;
        }

        var scaledSize = _menuFont.MeasureString(text) * scale;
        var fallbackPosition = new Vector2(
            MathF.Round(position.X - (scaledSize.X / 2f)),
            MathF.Round(position.Y - (scaledSize.Y / 2f)));
        _spriteBatch.DrawString(_menuFont, text, fallbackPosition, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawHudTextLeftAligned(string text, Vector2 position, Color color, float scale)
    {
        DrawBitmapFontText(text, position, color, scale);
    }

    private void DrawHudTextRightAligned(string text, Vector2 position, Color color, float scale)
    {
        var width = MeasureBitmapFontWidth(text, scale);
        DrawBitmapFontText(text, new Vector2(position.X - width, position.Y), color, scale);
    }

    private void DrawHudTextRightAlignedCenteredY(string text, Vector2 position, Color color, float scale)
    {
        var width = MeasureBitmapFontWidth(text, scale);
        var height = MeasureBitmapFontHeight(scale);
        DrawBitmapFontText(text, new Vector2(position.X - width, position.Y - (height / 2f)), color, scale);
    }

    private void DrawConsoleTextCentered(string text, Vector2 position, Color color, float scale)
    {
        var origin = _consoleFont.MeasureString(text) / 2f;
        _spriteBatch.DrawString(_consoleFont, text, position, color, 0f, origin, scale, SpriteEffects.None, 0f);
    }

    private void DrawConsoleTextLeftAligned(string text, Vector2 position, Color color, float scale)
    {
        _spriteBatch.DrawString(_consoleFont, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawConsoleTextRightAligned(string text, Vector2 position, Color color, float scale)
    {
        var size = _consoleFont.MeasureString(text);
        _spriteBatch.DrawString(_consoleFont, text, position, color, 0f, size, scale, SpriteEffects.None, 0f);
    }

    private void DrawConsoleTextRightAlignedCenteredY(string text, Vector2 position, Color color, float scale)
    {
        var size = _consoleFont.MeasureString(text);
        var origin = new Vector2(size.X, size.Y / 2f);
        _spriteBatch.DrawString(_consoleFont, text, position, color, 0f, origin, scale, SpriteEffects.None, 0f);
    }

    private void DrawCenteredHudSprite(string spriteName, int frameIndex, Vector2 visualCenter, Color tint, Vector2 scale)
    {
        var sprite = GetResolvedSprite(spriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return;
        }

        var clampedFrameIndex = Math.Clamp(frameIndex, 0, sprite.Frames.Count - 1);
        var frame = sprite.Frames[clampedFrameIndex];
        var drawPosition = new Vector2(
            visualCenter.X + ((sprite.Origin.X - (frame.Width / 2f)) * scale.X),
            visualCenter.Y + ((sprite.Origin.Y - (frame.Height / 2f)) * scale.Y));
        DrawLoadedSpriteFrame(
            frame,
            drawPosition,
            null,
            tint,
            0f,
            sprite.Origin.ToVector2(),
            scale,
            SpriteEffects.None,
            0f);
    }

    private bool TryDrawScreenSprite(string spriteName, int frameIndex, Vector2 position, Color tint, Vector2 scale)
    {
        return TryDrawScreenSprite(spriteName, frameIndex, position, tint, scale, 0f);
    }

    private bool TryDrawScreenSprite(string spriteName, int frameIndex, Vector2 position, Color tint, Vector2 scale, float rotation)
    {
        var sprite = GetResolvedSprite(spriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            LogCriticalBrowserHudSpriteState(
                spriteName,
                sprite is null ? "missing" : "empty",
                $"frameIndex={frameIndex} position={position} teamSelectOpen={_teamSelectOpen} classSelectOpen={_classSelectOpen} awaitingJoin={_world.LocalPlayerAwaitingJoin}");
            return false;
        }

        var clampedFrameIndex = Math.Clamp(frameIndex, 0, sprite.Frames.Count - 1);
        LogCriticalBrowserHudSpriteState(
            spriteName,
            "drawn",
            $"requestedFrame={frameIndex} drawnFrame={clampedFrameIndex} frames={sprite.Frames.Count} position={position} teamSelectOpen={_teamSelectOpen} classSelectOpen={_classSelectOpen} awaitingJoin={_world.LocalPlayerAwaitingJoin}");
        DrawLoadedSpriteFrame(
            sprite.Frames[clampedFrameIndex],
            position,
            null,
            tint,
            rotation,
            sprite.Origin.ToVector2(),
            scale,
            SpriteEffects.None,
            0f);
        return true;
    }

    private void LogCriticalBrowserHudSpriteState(string spriteName, string state, string details)
    {
        if (!OperatingSystem.IsBrowser() || !CriticalBrowserHudSprites.Contains(spriteName))
        {
            return;
        }

        var eventKey = $"{spriteName}:{state}";
        if (!_browserLoggedCriticalHudSpriteEvents.Add(eventKey))
        {
            return;
        }

        Console.WriteLine($"Browser critical HUD sprite {spriteName} {state}: {details}");
    }

    private static readonly HashSet<string> CriticalBrowserHudSprites = new(StringComparer.Ordinal)
    {
        "TeamSelectS",
        "ClassSelectS",
        "TeamDoorS",
        "DoorTopLightUpS",
        "TVLightUpS",
        "ClassSelectPortraitS",
        "ClassSelectSpritesS",
        "TimerHudS",
        "TimerS",
    };

    private bool TryDrawScreenSpritePart(string spriteName, int frameIndex, Rectangle sourceRectangle, Vector2 position, Color tint, Vector2 scale)
    {
        return TryDrawScreenSpritePart(spriteName, frameIndex, sourceRectangle, position, tint, scale, SpriteEffects.None);
    }

    private bool TryDrawScreenSpritePart(string spriteName, int frameIndex, Rectangle sourceRectangle, Vector2 position, Color tint, Vector2 scale, SpriteEffects effects)
    {
        var sprite = GetResolvedSprite(spriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return false;
        }

        var clampedFrameIndex = Math.Clamp(frameIndex, 0, sprite.Frames.Count - 1);
        var frame = sprite.Frames[clampedFrameIndex];
        var safeWidth = Math.Clamp(sourceRectangle.Width, 0, frame.Width);
        var safeHeight = Math.Clamp(sourceRectangle.Height, 0, frame.Height);
        if (safeWidth == 0 || safeHeight == 0)
        {
            return false;
        }

        var clampedSourceRectangle = new Rectangle(sourceRectangle.X, sourceRectangle.Y, safeWidth, safeHeight);
        DrawLoadedSpriteFrame(
            frame,
            position,
            clampedSourceRectangle,
            tint,
            0f,
            sprite.Origin.ToVector2(),
            scale,
            effects,
            0f);
        return true;
    }

    private void DrawInsetHudPanel(Rectangle rectangle, Color outerColor, Color innerColor)
    {
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            return;
        }

        _spriteBatch.Draw(_pixel, rectangle, outerColor);
        if (rectangle.Width <= 4 || rectangle.Height <= 4)
        {
            return;
        }

        _spriteBatch.Draw(
            _pixel,
            new Rectangle(rectangle.X + 2, rectangle.Y + 2, rectangle.Width - 4, rectangle.Height - 4),
            innerColor);
    }

    private void DrawInsetRoundedHudPanel(Rectangle rectangle, Color outerColor, Color innerColor, int radius)
    {
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            return;
        }

        DrawRoundedRectangleHud(rectangle, outerColor, radius);
        if (rectangle.Width <= 4 || rectangle.Height <= 4)
        {
            return;
        }

        var inner = new Rectangle(rectangle.X + 2, rectangle.Y + 2, rectangle.Width - 4, rectangle.Height - 4);
        if (inner.Width > 0 && inner.Height > 0)
        {
            DrawRoundedRectangleHud(inner, innerColor, Math.Max(0, radius - 2));
        }
    }

    private void DrawRoundedRectangleHud(Rectangle bounds, Color color, int radius)
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
}
