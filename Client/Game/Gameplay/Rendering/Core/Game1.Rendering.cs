#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private static float RoundToSourcePixel(float value)
    {
        return MathF.Round(value, MidpointRounding.AwayFromZero);
    }

    private static Vector2 RoundToSourcePixels(Vector2 value)
    {
        return new Vector2(
            RoundToSourcePixel(value.X),
            RoundToSourcePixel(value.Y));
    }

    private void DrawSniperTracers(Vector2 cameraPosition)
    {
        foreach (var trace in _world.CombatTraces)
        {
            if (!trace.IsSniperTracer)
            {
                continue;
            }

            var alpha = 0.8f * (trace.TicksRemaining / 3f);
            var color = trace.Team == PlayerTeam.Blue
                ? Color.Blue * alpha
                : Color.Red * alpha;
            DrawWorldLine(trace.StartX, trace.StartY, trace.EndX, trace.EndY, cameraPosition, color, 2f);
        }
    }

    private bool DrawLevelBackground(Rectangle worldRectangle)
    {
        var backgroundName = _world.Level.BackgroundAssetName;
        if (_runtimeAssets is null)
        {
            _spriteBatch.Draw(_pixel, worldRectangle, new Color(34, 44, 60));
            return false;
        }

        var background = string.IsNullOrWhiteSpace(backgroundName)
            ? null
            : _runtimeAssets.GetBackground(backgroundName);
        if (background is null)
        {
            _spriteBatch.Draw(_pixel, worldRectangle, new Color(34, 44, 60));
            return false;
        }

        _spriteBatch.Draw(background, worldRectangle, Color.White);
        return true;
    }

    private void DrawWorldLine(float startX, float startY, float endX, float endY, Vector2 cameraPosition, Color color, float thickness)
    {
        var start = new Vector2(startX - cameraPosition.X, startY - cameraPosition.Y);
        var end = new Vector2(endX - cameraPosition.X, endY - cameraPosition.Y);
        var edge = end - start;
        var angle = MathF.Atan2(edge.Y, edge.X);
        var length = edge.Length();
        if (length <= 0.01f)
        {
            return;
        }

        _spriteBatch.Draw(
            _pixel,
            start,
            null,
            color,
            angle,
            Vector2.Zero,
            new Vector2(length, thickness),
            SpriteEffects.None,
            0f);
    }

    private bool TryDrawSprite(string spriteName, int frameIndex, float worldX, float worldY, Vector2 cameraPosition, Color tint, float rotation = 0f)
    {
        var sprite = GetResolvedSprite(spriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return false;
        }

        var clampedFrameIndex = Math.Clamp(frameIndex, 0, sprite.Frames.Count - 1);
        DrawSpriteFrameWithOptionalShadow(
            sprite.Frames[clampedFrameIndex],
            new Vector2(worldX - cameraPosition.X, worldY - cameraPosition.Y),
            tint,
            rotation,
            sprite.Origin.ToVector2(),
            Vector2.One);
        return true;
    }

    private void DrawSpriteFrameWithOptionalShadow(
        LoadedSpriteFrame frame,
        Vector2 position,
        Color tint,
        float rotation,
        Vector2 origin,
        Vector2 scale,
        SpriteEffects effects = SpriteEffects.None)
    {
        if (!OperatingSystem.IsBrowser() && _spriteDropShadowEnabled && tint.A > 0)
        {
            var shadowAlpha = ((tint.A / 255f) * 0.32f);
            var shadowTint = new Color(0, 0, 0) * shadowAlpha;
            _spriteBatch.Draw(
                frame.Texture,
                position + new Vector2(1f, 1f),
                frame.SourceRectangle,
                shadowTint,
                rotation,
                origin,
                scale,
                effects,
                0f);
        }

        _spriteBatch.Draw(
            frame.Texture,
            position,
            frame.SourceRectangle,
            tint,
            rotation,
            origin,
            scale,
            effects,
            0f);
    }

    private void DrawLoadedSpriteFrame(
        LoadedSpriteFrame frame,
        Vector2 position,
        Rectangle? sourceRectangle,
        Color tint,
        float rotation,
        Vector2 origin,
        Vector2 scale,
        SpriteEffects effects,
        float layerDepth)
    {
        _spriteBatch.Draw(
            frame.Texture,
            position,
            CombineSourceRectangles(frame.SourceRectangle, sourceRectangle),
            tint,
            rotation,
            origin,
            scale,
            effects,
            layerDepth);
    }

    private void DrawLoadedSpriteFrame(LoadedSpriteFrame frame, Rectangle destinationRectangle, Color tint)
    {
        _spriteBatch.Draw(
            frame.Texture,
            destinationRectangle,
            frame.SourceRectangle,
            tint);
    }

    private static Rectangle? CombineSourceRectangles(Rectangle? frameSourceRectangle, Rectangle? requestedSourceRectangle)
    {
        if (requestedSourceRectangle is null)
        {
            return frameSourceRectangle;
        }

        if (frameSourceRectangle is null)
        {
            return requestedSourceRectangle;
        }

        var requested = requestedSourceRectangle.Value;
        var frameSource = frameSourceRectangle.Value;
        return new Rectangle(
            frameSource.X + requested.X,
            frameSource.Y + requested.Y,
            requested.Width,
            requested.Height);
    }

    private static float GetVelocityRotation(float velocityX, float velocityY)
    {
        return MathF.Atan2(velocityY, velocityX);
    }

    private static float GetTravelRotation(float previousX, float previousY, float x, float y)
    {
        return MathF.Atan2(y - previousY, x - previousX);
    }
}
