#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void DrawChatBubble(PlayerEntity player, Vector2 cameraPosition)
    {
        if (!player.IsChatBubbleVisible)
        {
            return;
        }

        var renderPosition = GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _world.LocalPlayer));
        var sprite = _runtimeAssets.GetSprite("BubblesS");
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return;
        }

        var frameIndex = Math.Clamp(player.ChatBubbleFrameIndex, 0, sprite.Frames.Count - 1);
        var alpha = Math.Clamp(player.ChatBubbleAlpha, 0f, 1f) * GetPlayerVisibilityAlpha(player);

        if (alpha <= 0f)
        {
            return;
        }

        _spriteBatch.Draw(
            sprite.Frames[frameIndex],
            new Vector2(MathF.Round(renderPosition.X) + 10f - cameraPosition.X, MathF.Round(renderPosition.Y) - 18f - cameraPosition.Y),
            null,
            Color.White * alpha,
            0f,
            sprite.Origin.ToVector2(),
            Vector2.One,
            SpriteEffects.None,
            0f);
    }

    private void DrawHealthBar(PlayerEntity player, Vector2 cameraPosition, Color fillColor, Color backColor, Color borderColor)
    {
        if (GetPlayerVisibilityAlpha(player) <= 0f)
        {
            return;
        }

        var renderPosition = GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _world.LocalPlayer));
        var bounds = GetPlayerScreenBounds(player, renderPosition, cameraPosition);
        var barWidth = Math.Max(14, bounds.Width + 2);
        const int barHeight = 5;
        const int verticalOffset = 14;
        var barX = (int)MathF.Round((renderPosition.X - cameraPosition.X) - (barWidth * 0.5f));
        var barY = (int)MathF.Round(renderPosition.Y + player.CollisionTopOffset - cameraPosition.Y) - verticalOffset;
        var borderRectangle = new Rectangle(
            barX - 1,
            barY - 1,
            barWidth + 2,
            barHeight + 2);
        var backRectangle = new Rectangle(
            barX,
            barY,
            barWidth,
            barHeight);
        _spriteBatch.Draw(_pixel, borderRectangle, borderColor);
        _spriteBatch.Draw(_pixel, backRectangle, backColor);

        if (player.MaxHealth <= 0)
        {
            return;
        }

        var fillWidth = (int)MathF.Round(barWidth * (player.Health / (float)player.MaxHealth));
        if (fillWidth <= 0)
        {
            return;
        }

        var fillRectangle = new Rectangle(backRectangle.X, backRectangle.Y, fillWidth, backRectangle.Height);
        _spriteBatch.Draw(_pixel, fillRectangle, fillColor);
    }
}
