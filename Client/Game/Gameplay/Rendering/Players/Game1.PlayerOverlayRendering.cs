#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Globalization;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const float OverheadChatMinWrapWidth = 96f;
    private const float OverheadChatPreferredWrapWidth = 128f;
    private const float OverheadChatMaxWrapWidth = 160f;

    private void DrawChatBubble(PlayerEntity player, Vector2 cameraPosition)
    {
        if (!player.IsChatBubbleVisible || IsPlayerMutedByScoreboardSlot(player))
        {
            return;
        }

        var renderPosition = GetRenderPosition(player);
        if (ChatBubbleFrameCatalog.TryGetCustomBubbleSlot(player.ChatBubbleFrameIndex, out var customBubbleSlot))
        {
            DrawCustomChatBubble(player, customBubbleSlot, renderPosition, cameraPosition);
            return;
        }

        var sprite = GetResolvedSprite("BubblesS");
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

        DrawLoadedSpriteFrame(
            sprite.Frames[frameIndex],
            new Vector2(MathF.Round(renderPosition.X, MidpointRounding.AwayFromZero) + 10f - cameraPosition.X, MathF.Round(renderPosition.Y, MidpointRounding.AwayFromZero) - 18f - cameraPosition.Y),
            null,
            Color.White * alpha,
            0f,
            sprite.Origin.ToVector2(),
            Vector2.One,
            SpriteEffects.None,
            0f);
    }

    private void DrawCustomChatBubble(PlayerEntity player, int slotIndex, Vector2 renderPosition, Vector2 cameraPosition)
    {
        if (!_showCustomBubbles)
        {
            return;
        }

        if (!TryGetCustomBubbleTextureForPlayer(player, slotIndex, out var artTexture))
        {
            return;
        }

        var alpha = Math.Clamp(player.ChatBubbleAlpha, 0f, 1f) * GetPlayerVisibilityAlpha(player);
        if (alpha <= 0f)
        {
            return;
        }

        var anchor = new Vector2(
            MathF.Round(renderPosition.X) + 10f - cameraPosition.X,
            MathF.Round(renderPosition.Y) - 18f - cameraPosition.Y);
        var shellOrigin = new Vector2(CustomBubbleShellOriginX, CustomBubbleShellOriginY);
        var shellScale = new Vector2(CustomBubbleGameplayScale);
        var shellTopLeft = anchor - (shellOrigin * CustomBubbleGameplayScale);
        var shellBounds = new Rectangle(
            (int)MathF.Round(shellTopLeft.X),
            (int)MathF.Round(shellTopLeft.Y),
            (int)MathF.Round(CustomBubbleShellPixelWidth * CustomBubbleGameplayScale),
            (int)MathF.Round(CustomBubbleShellPixelHeight * CustomBubbleGameplayScale));
        var shellFrame = GetCustomBubbleShellFrame();
        if (shellFrame is null)
        {
            return;
        }

        DrawLoadedSpriteFrame(
            shellFrame,
            anchor,
            null,
            Color.White * alpha,
            0f,
            shellOrigin,
            shellScale,
            SpriteEffects.None,
            0f);

        _spriteBatch.Draw(artTexture, shellBounds, Color.White * alpha);
    }

    private void DrawOverheadChatMessage(PlayerEntity player, Vector2 cameraPosition)
    {
        if (IsPlayerMutedByScoreboardSlot(player)
            || !_overheadChatEnabled
            || !TryGetOverheadChatMessageForPlayer(player, out var message))
        {
            return;
        }

        var visibilityAlpha = GetPlayerVisibilityAlpha(player);
        if (visibilityAlpha <= 0f)
        {
            return;
        }

        var text = message.TeamOnly ? $"(TEAM) {message.Text}" : message.Text;
        var maxTextWidth = GetOverheadChatTextWrapWidth(text);
        var wrappedLines = WrapBitmapFontText(text, maxTextWidth, maxTextWidth);
        if (wrappedLines.Count == 0)
        {
            return;
        }

        var lineHeight = MathF.Max(12f, MeasureBitmapFontHeight(1f) + 1f);
        var textWidth = 0f;
        for (var index = 0; index < wrappedLines.Count; index += 1)
        {
            textWidth = MathF.Max(textWidth, MeasureBitmapFontWidth(wrappedLines[index], 1f));
        }

        const int horizontalPadding = 5;
        const int verticalPadding = 3;
        var panelWidth = Math.Max(12, (int)MathF.Ceiling(textWidth) + (horizontalPadding * 2));
        var panelHeight = Math.Max(12, (int)MathF.Ceiling((wrappedLines.Count * lineHeight) + (verticalPadding * 2)));
        var renderPosition = GetRenderPosition(player);
        var playerBounds = GetPlayerScreenBounds(player, renderPosition, cameraPosition);
        var messageAlpha = GetOverheadChatMessageAlpha(message);
        if (messageAlpha <= 0f)
        {
            return;
        }

        var fadeProgress = 1f - messageAlpha;
        var isWriteBubbleVisible = player.IsAlive && (ReferenceEquals(player, _world.LocalPlayer) ? _chatOpen : player.IsTypingChatMessage);
        var targetY = playerBounds.Top - panelHeight - 16f - ((player.IsChatBubbleVisible || isWriteBubbleVisible) ? 14f : 0f) - (fadeProgress * 8f);
        var panelX = playerBounds.Center.X - (panelWidth / 2f);
        var maxPanelX = MathF.Max(4f, ViewportWidth - panelWidth - 4f);
        panelX = Math.Clamp(panelX, 4f, maxPanelX);
        var panelY = MathF.Max(4f, targetY);
        var panelBounds = new Rectangle(
            (int)MathF.Round(panelX),
            (int)MathF.Round(panelY),
            panelWidth,
            panelHeight);

        var overlayAlpha = Math.Clamp(visibilityAlpha * messageAlpha, 0f, 1f);
        var teamFillColor = player.Team == PlayerTeam.Red
            ? new Color(0xA5, 0x46, 0x40)
            : new Color(0x48, 0x5C, 0x67);
        var textAndOutlineColor = new Color(0xD9, 0xD9, 0xB7);
        var outlineColor = player.Team == PlayerTeam.Red
            ? new Color(0x7E, 0x35, 0x30)
            : new Color(0x35, 0x44, 0x4D);
        DrawRoundedRectangleOutline(
            panelBounds,
            teamFillColor * overlayAlpha,
            outlineColor * overlayAlpha,
            outlineThickness: 2,
            radius: 6);
        for (var index = 0; index < wrappedLines.Count; index += 1)
        {
            var line = wrappedLines[index];
            var lineWidth = MeasureBitmapFontWidth(line, 1f);
            var textPosition = new Vector2(
                panelBounds.X + ((panelBounds.Width - lineWidth) / 2f),
                panelBounds.Y + verticalPadding + (index * lineHeight));
            DrawBitmapFontText(line, textPosition, textAndOutlineColor * overlayAlpha, 1f);
        }
    }

    private static float GetOverheadChatMessageAlpha(OverheadChatMessage message)
    {
        if (message.TicksRemaining >= OverheadChatMessageFadeTicks)
        {
            return 1f;
        }

        return Math.Clamp(message.TicksRemaining / (float)OverheadChatMessageFadeTicks, 0f, 1f);
    }

    private float GetOverheadChatTextWrapWidth(string text)
    {
        var availableWidth = MathF.Max(48f, ViewportWidth - 32f);
        var maxWidth = MathF.Min(OverheadChatMaxWrapWidth, availableWidth);
        var preferredWidth = MathF.Min(OverheadChatPreferredWrapWidth, maxWidth);
        var textWidth = MeasureBitmapFontWidth(text, 1f);
        if (textWidth <= preferredWidth)
        {
            return MathF.Max(1f, textWidth);
        }

        var desiredLineCount = Math.Max(2, (int)MathF.Ceiling(textWidth / preferredWidth));
        var balancedWidth = textWidth / desiredLineCount;
        var longestWordWidth = MeasureLongestOverheadChatWordWidth(text);
        var minWrapWidth = MathF.Min(OverheadChatMinWrapWidth, maxWidth);
        var targetWidth = MathF.Max(balancedWidth, MathF.Min(longestWordWidth, maxWidth));
        return Math.Clamp(targetWidth, minWrapWidth, maxWidth);
    }

    private float MeasureLongestOverheadChatWordWidth(string text)
    {
        var longestWidth = 0f;
        var wordStartIndex = 0;
        for (var index = 0; index <= text.Length; index += 1)
        {
            if (index < text.Length && !char.IsWhiteSpace(text[index]))
            {
                continue;
            }

            if (index > wordStartIndex)
            {
                longestWidth = MathF.Max(longestWidth, MeasureBitmapFontWidth(text[wordStartIndex..index], 1f));
            }

            wordStartIndex = index + 1;
        }

        return longestWidth;
    }

    private bool TryGetOverheadChatMessageForPlayer(PlayerEntity player, out OverheadChatMessage message)
    {
        if (ReferenceEquals(player, _world.LocalPlayer) && _localOverheadChatMessage is not null)
        {
            message = _localOverheadChatMessage;
            return true;
        }

        if (TryGetOverheadChatPlayerSlot(player, out var slot)
            && _overheadChatMessagesBySlot.TryGetValue(slot, out var foundMessage))
        {
            message = foundMessage;
            return true;
        }

        message = null!;
        return false;
    }

    private bool TryGetOverheadChatPlayerSlot(PlayerEntity player, out byte slot)
    {
        if (ReferenceEquals(player, _world.LocalPlayer))
        {
            if (_networkClient.IsConnected)
            {
                slot = !_networkClient.IsSpectator ? _networkClient.LocalPlayerSlot : (byte)0;
                return slot != 0;
            }

            slot = SimulationWorld.LocalPlayerSlot;
            return true;
        }

        return _world.TryGetPlayerNetworkSlot(player, out slot);
    }

    private void DrawWriteBubble(PlayerEntity player, Vector2 cameraPosition)
    {
        var isTyping = ReferenceEquals(player, _world.LocalPlayer) ? _chatOpen : player.IsTypingChatMessage;
        if (!isTyping || !player.IsAlive)
        {
            return;
        }

        var sprite = GetResolvedSprite("BubbleWrite");
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return;
        }

        const int ticksPerFrame = 15;
        var frameIndex = (_writeBubbleTick / ticksPerFrame) % sprite.Frames.Count;
        var renderPosition = GetRenderPosition(player);
        var alpha = GetPlayerVisibilityAlpha(player);
        if (alpha <= 0f)
        {
            return;
        }

        DrawLoadedSpriteFrame(
            sprite.Frames[frameIndex],
            new Vector2(MathF.Round(renderPosition.X, MidpointRounding.AwayFromZero) + 10f - cameraPosition.X, MathF.Round(renderPosition.Y, MidpointRounding.AwayFromZero) - 18f - cameraPosition.Y),
            null,
            Color.White * alpha,
            0f,
            sprite.Origin.ToVector2(),
            Vector2.One,
            SpriteEffects.None,
            0f);
    }

    private void DrawPracticeCombatDummyDps(PlayerEntity player, Vector2 cameraPosition)
    {
        if (!_world.IsPracticeDpsDummy(player) || !player.IsAlive || !_world.PracticeCombatDummyDpsVisible)
        {
            return;
        }

        var visibilityAlpha = GetPlayerVisibilityAlpha(player);
        if (visibilityAlpha <= 0f)
        {
            return;
        }

        var roundedDps = Math.Max(0, (int)Math.Round(_world.PracticeCombatDummyDps, MidpointRounding.AwayFromZero));
        var text = $"{roundedDps.ToString(CultureInfo.InvariantCulture)} DPS";
        var intensity = _world.PracticeCombatDummyDamageIntensity;
        var scale = 1f + (0.55f * intensity);
        var textColor = Color.Lerp(Color.White, new Color(255, 48, 34), intensity) * visibilityAlpha;
        var shadowColor = Color.Black * (0.75f * visibilityAlpha);
        var renderPosition = GetRenderPosition(player);
        var playerBounds = GetPlayerScreenBounds(player, renderPosition, cameraPosition);
        var textY = MathF.Max(4f, playerBounds.Top - 30f - (8f * intensity));
        var textPosition = new Vector2(playerBounds.Center.X, textY);
        DrawBitmapFontTextCentered(text, textPosition + new Vector2(2f, 2f), shadowColor, scale);
        DrawBitmapFontTextCentered(text, textPosition, textColor, scale);
    }

    private void DrawHealthBar(PlayerEntity player, Vector2 cameraPosition, Color fillColor, Color backColor, Color borderColor)
    {
        if (player.MaxHealth <= 0)
        {
            return;
        }

        DrawPlayerMeterBar(
            player,
            cameraPosition,
            player.Health / (float)player.MaxHealth,
            fillColor,
            backColor,
            borderColor,
            placeBelow: false);
    }

    private void DrawShieldBar(PlayerEntity player, Vector2 cameraPosition, float fillFraction, Color fillColor, Color backColor, Color borderColor)
    {
        DrawPlayerMeterBar(
            player,
            cameraPosition,
            fillFraction,
            fillColor,
            backColor,
            borderColor,
            placeBelow: true);
    }

    private void DrawPlayerMeterBar(
        PlayerEntity player,
        Vector2 cameraPosition,
        float fillFraction,
        Color fillColor,
        Color backColor,
        Color borderColor,
        bool placeBelow)
    {
        if (GetPlayerVisibilityAlpha(player) <= 0f)
        {
            return;
        }

        var renderPosition = GetRenderPosition(player);
        var bounds = GetPlayerScreenBounds(player, renderPosition, cameraPosition);
        var barWidth = Math.Max(18, bounds.Width + 4);
        const int barHeight = 4;
        const int aboveVerticalOffset = 14;
        const int belowVerticalOffset = 8;
        var barX = bounds.Left + ((bounds.Width - barWidth) / 2);
        var barY = placeBelow
            ? bounds.Bottom + belowVerticalOffset
            : bounds.Top - aboveVerticalOffset;
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

        var fillWidth = (int)MathF.Round(barWidth * Math.Clamp(fillFraction, 0f, 1f));
        if (fillWidth <= 0)
        {
            return;
        }

        var fillRectangle = new Rectangle(backRectangle.X, backRectangle.Y, fillWidth, backRectangle.Height);
        _spriteBatch.Draw(_pixel, fillRectangle, fillColor);
    }
}
