#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Client.Plugins;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayHudCanvas(Game1 game, Vector2 cameraTopLeft) : IOpenGarrisonClientHudCanvas
    {
        public int ViewportWidth => game.ViewportWidth;
        public int ViewportHeight => game.ViewportHeight;
        public Vector2 CameraTopLeft => cameraTopLeft;

        public Vector2 WorldToScreen(Vector2 worldPosition) => worldPosition - cameraTopLeft;
        public float MeasureBitmapTextWidth(string text, float scale) => game.MeasureBitmapFontWidth(text, scale);
        public float MeasureBitmapTextHeight(float scale) => game.MeasureBitmapFontHeight(scale);
        public void DrawBitmapText(string text, Vector2 position, Color color, float scale = 1f) => game.DrawBitmapFontText(text, position, color, scale);
        public void DrawBitmapTextCentered(string text, Vector2 position, Color color, float scale = 1f) => game.DrawHudTextCentered(text, position, color, scale);
        public void FillScreenRectangle(Rectangle rectangle, Color color) => game._spriteBatch.Draw(game._pixel, rectangle, color);

        public void DrawScreenRectangleOutline(Rectangle rectangle, Color color, int thickness = 1)
        {
            var safeThickness = Math.Max(1, thickness);
            game._spriteBatch.Draw(game._pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, safeThickness), color);
            game._spriteBatch.Draw(game._pixel, new Rectangle(rectangle.X, rectangle.Bottom - safeThickness, rectangle.Width, safeThickness), color);
            game._spriteBatch.Draw(game._pixel, new Rectangle(rectangle.X, rectangle.Y, safeThickness, rectangle.Height), color);
            game._spriteBatch.Draw(game._pixel, new Rectangle(rectangle.Right - safeThickness, rectangle.Y, safeThickness, rectangle.Height), color);
        }

        public void DrawScreenLine(Vector2 start, Vector2 endPoint, Color color, float thickness = 1f)
        {
            var edge = endPoint - start;
            var angle = MathF.Atan2(edge.Y, edge.X);
            var length = edge.Length();
            if (length <= 0.01f)
            {
                return;
            }

            game._spriteBatch.Draw(game._pixel, start, null, color, angle, Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        public bool TryDrawScreenSprite(string spriteName, int frameIndex, Vector2 position, Color tint, Vector2 scale)
            => game.TryDrawScreenSprite(spriteName, frameIndex, position, tint, scale);

        public bool TryDrawWorldSprite(string spriteName, int frameIndex, Vector2 worldPosition, Color tint, float rotation = 0f)
            => game.TryDrawSprite(spriteName, frameIndex, worldPosition.X, worldPosition.Y, cameraTopLeft, tint, rotation);

        public bool TryGetLevelBackgroundTexture(out Texture2D texture)
        {
            texture = game.GetClientPluginLevelBackgroundTexture()!;
            return texture is not null;
        }

        public void DrawScreenTexture(Texture2D texture, Vector2 position, Color tint, Vector2 scale, Rectangle? sourceRectangle = null, float rotation = 0f, Vector2? origin = null)
        {
            game._spriteBatch.Draw(texture, position, sourceRectangle, tint, rotation, origin ?? Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        public void DrawWorldTexture(Texture2D texture, Vector2 worldPosition, Color tint, Vector2 scale, Rectangle? sourceRectangle = null, float rotation = 0f, Vector2? origin = null)
        {
            DrawScreenTexture(texture, worldPosition - cameraTopLeft, tint, scale, sourceRectangle, rotation, origin);
        }
    }

    private sealed class ScoreboardCanvas(Game1 game) : IOpenGarrisonClientScoreboardCanvas
    {
        public int ViewportWidth => game.ViewportWidth;
        public int ViewportHeight => game.ViewportHeight;
        public Vector2 CameraTopLeft => Vector2.Zero;

        public Vector2 WorldToScreen(Vector2 worldPosition) => worldPosition;
        public float MeasureBitmapTextWidth(string text, float scale) => game.MeasureBitmapFontWidth(text, scale);
        public float MeasureBitmapTextHeight(float scale) => game.MeasureBitmapFontHeight(scale);
        public void DrawBitmapText(string text, Vector2 position, Color color, float scale = 1f) => game.DrawBitmapFontText(text, position, color, scale);
        public void DrawBitmapTextCentered(string text, Vector2 position, Color color, float scale = 1f) => game.DrawBitmapFontTextCentered(text, position, color, scale);
        public void DrawBitmapTextRightAligned(string text, Vector2 position, Color color, float scale = 1f) => game.DrawBitmapFontTextRightAligned(text, position, color, scale);
        public void FillScreenRectangle(Rectangle rectangle, Color color) => game._spriteBatch.Draw(game._pixel, rectangle, color);

        public void DrawScreenRectangleOutline(Rectangle rectangle, Color color, int thickness = 1)
        {
            var safeThickness = Math.Max(1, thickness);
            game._spriteBatch.Draw(game._pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, safeThickness), color);
            game._spriteBatch.Draw(game._pixel, new Rectangle(rectangle.X, rectangle.Bottom - safeThickness, rectangle.Width, safeThickness), color);
            game._spriteBatch.Draw(game._pixel, new Rectangle(rectangle.X, rectangle.Y, safeThickness, rectangle.Height), color);
            game._spriteBatch.Draw(game._pixel, new Rectangle(rectangle.Right - safeThickness, rectangle.Y, safeThickness, rectangle.Height), color);
        }

        public void DrawScreenLine(Vector2 start, Vector2 endPoint, Color color, float thickness = 1f)
        {
            var edge = endPoint - start;
            var angle = MathF.Atan2(edge.Y, edge.X);
            var length = edge.Length();
            if (length <= 0.01f)
            {
                return;
            }

            game._spriteBatch.Draw(game._pixel, start, null, color, angle, Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        public bool TryDrawScreenSprite(string spriteName, int frameIndex, Vector2 position, Color tint, Vector2 scale)
            => game.TryDrawScreenSprite(spriteName, frameIndex, position, tint, scale);

        public bool TryDrawWorldSprite(string spriteName, int frameIndex, Vector2 worldPosition, Color tint, float rotation = 0f)
            => game.TryDrawSprite(spriteName, frameIndex, worldPosition.X, worldPosition.Y, Vector2.Zero, tint, rotation);

        public bool TryGetLevelBackgroundTexture(out Texture2D texture)
        {
            texture = game.GetClientPluginLevelBackgroundTexture()!;
            return texture is not null;
        }

        public void DrawScreenTexture(Texture2D texture, Vector2 position, Color tint, Vector2 scale, Rectangle? sourceRectangle = null, float rotation = 0f, Vector2? origin = null)
        {
            game._spriteBatch.Draw(texture, position, sourceRectangle, tint, rotation, origin ?? Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        public void DrawWorldTexture(Texture2D texture, Vector2 worldPosition, Color tint, Vector2 scale, Rectangle? sourceRectangle = null, float rotation = 0f, Vector2? origin = null)
        {
            DrawScreenTexture(texture, worldPosition, tint, scale, sourceRectangle, rotation, origin);
        }
    }
}
