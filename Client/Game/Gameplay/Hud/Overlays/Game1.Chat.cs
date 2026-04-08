#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int MaxChatHistoryLines = 128;
    private const int ClosedChatVisibleLineLimit = 6;
    private const int ChatScrollStep = 3;
    private const float ChatHudPanelMargin = 12f;
    private const float ChatHudPanelHorizontalPadding = 6f;
    private const float ChatHudPanelVerticalPadding = 4f;
    private const float ChatHudPanelSpacing = 4f;

    private void OpenChat(bool teamOnly)
    {
        _chatOpen = true;
        _chatTeamOnly = teamOnly;
        _chatInput = string.Empty;
        _chatScrollOffset = 0;
    }

    private void ResetChatInputState(bool requireOpenKeyRelease = false)
    {
        _chatOpen = false;
        _chatTeamOnly = false;
        _chatSubmitAwaitingOpenKeyRelease = requireOpenKeyRelease;
        _chatInput = string.Empty;
        _chatScrollOffset = 0;
    }

    private void SubmitChatMessage()
    {
        var text = _chatInput.Trim();
        var teamOnly = _chatTeamOnly;
        if (!string.IsNullOrWhiteSpace(text))
        {
            if (_networkClient.IsConnected)
            {
                _networkClient.SendChat(text, teamOnly);
            }
            else
            {
                AppendChatLine(_world.LocalPlayer.DisplayName, text, (byte)_world.LocalPlayer.Team, teamOnly);
            }
        }

        ResetChatInputState(requireOpenKeyRelease: true);
    }

    private void AppendChatLine(string playerName, string text, byte team, bool teamOnly)
    {
        var channelPrefix = teamOnly ? "(TEAM) " : string.Empty;
        var line = string.IsNullOrWhiteSpace(playerName)
            ? $"{channelPrefix}{text}"
            : $"{channelPrefix}{playerName}: {text}";
        if (_chatOpen && _chatScrollOffset > 0)
        {
            _chatScrollOffset += 1;
        }

        _chatLines.Add(new ChatLine(playerName, text, team, teamOnly));
        while (_chatLines.Count > MaxChatHistoryLines)
        {
            _chatLines.RemoveAt(0);
            if (_chatScrollOffset > 0)
            {
                _chatScrollOffset -= 1;
            }
        }

        ClampChatScrollOffset();
        AddConsoleLine(teamOnly ? $"[team chat] {line}" : $"[chat] {line}");
    }

    private void AdvanceChatHud()
    {
        for (var index = 0; index < _chatLines.Count; index += 1)
        {
            _chatLines[index].TicksRemaining -= 1;
            if (_chatLines[index].TicksRemaining < 0)
            {
                _chatLines[index].TicksRemaining = 0;
            }
        }
    }

    private void UpdateChatScrollState(KeyboardState keyboard, MouseState mouse)
    {
        var wheelDelta = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        if (wheelDelta != 0)
        {
            var stepCount = Math.Max(1, Math.Abs(wheelDelta) / 120);
            ScrollChatHistory(wheelDelta > 0 ? stepCount : -stepCount);
        }

        if (IsKeyPressed(keyboard, Keys.PageUp))
        {
            ScrollChatHistory(ChatScrollStep);
        }
        else if (IsKeyPressed(keyboard, Keys.PageDown))
        {
            ScrollChatHistory(-ChatScrollStep);
        }
        else if (IsKeyPressed(keyboard, Keys.Home))
        {
            _chatScrollOffset = Math.Max(0, _chatLines.Count - 1);
        }
        else if (IsKeyPressed(keyboard, Keys.End))
        {
            _chatScrollOffset = 0;
        }

        ClampChatScrollOffset();
    }

    private void ScrollChatHistory(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        _chatScrollOffset = Math.Max(0, _chatScrollOffset + delta);
        ClampChatScrollOffset();
    }

    private void ClampChatScrollOffset()
    {
        _chatScrollOffset = Math.Clamp(_chatScrollOffset, 0, Math.Max(0, _chatLines.Count - 1));
    }

    private void DrawChatHud()
    {
        var baseX = 18f;
        var maxPanelWidth = GetChatHudMaxPanelWidth(baseX);
        var promptPrefix = _chatTeamOnly ? "(TEAM) > " : "> ";
        var promptRectangle = new Rectangle(
            12,
            ViewportHeight - 118,
            Math.Max(280, ViewportWidth / 3),
            24);
        var maxChatHeight = Math.Max(72f, promptRectangle.Y - 24f);
        var visibleLines = GetVisibleChatLines(maxPanelWidth, maxChatHeight, _chatOpen);
        var totalChatHeight = 0f;
        for (var index = 0; index < visibleLines.Count; index += 1)
        {
            totalChatHeight += MeasureChatLineHeight(visibleLines[index], maxPanelWidth);
            if (index < visibleLines.Count - 1)
            {
                totalChatHeight += ChatHudPanelSpacing;
            }
        }

        var baseY = promptRectangle.Y - 14f - totalChatHeight;
        for (var index = 0; index < visibleLines.Count; index += 1)
        {
            var line = visibleLines[index];
            var alpha = _chatOpen ? 1f : MathF.Min(1f, line.TicksRemaining / 120f);
            var linePosition = new Vector2(baseX, baseY);
            DrawChatLine(line, linePosition, alpha, maxPanelWidth);
            baseY += MeasureChatLineHeight(line, maxPanelWidth) + ChatHudPanelSpacing;
        }

        if (!_chatOpen)
        {
            return;
        }

        DrawInsetHudPanel(promptRectangle, new Color(0, 0, 0, 220), new Color(49, 45, 26, 220));
        DrawChatPrompt(promptRectangle, promptPrefix);
        DrawChatScrollStatus(promptRectangle);
    }

    private void DrawChatLine(ChatLine line, Vector2 position, float alpha, float maxPanelWidth)
    {
        var channelPrefix = line.TeamOnly ? "(TEAM) " : string.Empty;
        var speakerPrefix = string.IsNullOrWhiteSpace(line.PlayerName)
            ? channelPrefix
            : $"{channelPrefix}{line.PlayerName}: ";
        var speakerWidth = MeasureBitmapFontWidth(speakerPrefix, 1f);
        var maxContentWidth = Math.Max(48f, maxPanelWidth - (ChatHudPanelHorizontalPadding * 2f));
        var wrappedMessageLines = WrapBitmapFontText(
            line.Text,
            Math.Max(24f, maxContentWidth - speakerWidth),
            maxContentWidth);
        var lineHeight = GetChatHudLineHeight();
        var width = Math.Max(
            96f,
            GetWrappedChatPanelWidth(wrappedMessageLines, speakerWidth) + (ChatHudPanelHorizontalPadding * 2f));
        var height = Math.Max(
            16f,
            GetWrappedChatPanelHeight(wrappedMessageLines.Count, lineHeight));
        DrawInsetHudPanel(
            new Rectangle((int)(position.X - 6f), (int)(position.Y - 2f), (int)MathF.Ceiling(width), (int)MathF.Ceiling(height)),
            new Color(0, 0, 0) * (0.82f * alpha),
            new Color(49, 45, 26) * (0.82f * alpha));

        for (var lineIndex = 0; lineIndex < wrappedMessageLines.Count; lineIndex += 1)
        {
            var textPosition = new Vector2(position.X, position.Y + lineIndex * lineHeight);
            if (lineIndex == 0 && speakerPrefix.Length > 0)
            {
                DrawBitmapFontText(speakerPrefix, textPosition, GetChatTeamColor(line.Team) * alpha, 1f);
            }

            DrawBitmapFontText(
                wrappedMessageLines[lineIndex],
                new Vector2(textPosition.X + (lineIndex == 0 ? speakerWidth : 0f), textPosition.Y),
                new Color(235, 235, 235) * alpha,
                1f);
        }
    }

    private void DrawChatPrompt(Rectangle promptRectangle, string promptPrefix)
    {
        var prefixWidth = MeasureBitmapFontWidth(promptPrefix, 1f);
        var maxInputWidth = Math.Max(24f, promptRectangle.Width - 18f - prefixWidth);
        var visibleInput = GetTrailingBitmapFontTextThatFits($"{_chatInput}_", maxInputWidth);
        var promptPosition = new Vector2(promptRectangle.X + 8f, promptRectangle.Y + 6f);
        DrawBitmapFontText(promptPrefix, promptPosition, new Color(255, 245, 210), 1f);
        DrawBitmapFontText(
            visibleInput,
            new Vector2(promptPosition.X + prefixWidth, promptPosition.Y),
            Color.White,
            1f);
    }

    private void DrawChatScrollStatus(Rectangle promptRectangle)
    {
        if (_chatLines.Count <= ClosedChatVisibleLineLimit)
        {
            return;
        }

        var statusText = _chatScrollOffset > 0
            ? $"Scroll: {_chatScrollOffset} older | PgUp/PgDn, Home/End, Wheel"
            : $"History: {_chatLines.Count} lines | PgUp/PgDn, Home/End, Wheel";
        var textWidth = MeasureBitmapFontWidth(statusText, 1f);
        var textPosition = new Vector2(
            Math.Max(18f, promptRectangle.Right - textWidth - 10f),
            promptRectangle.Y - GetChatHudLineHeight() - 8f);
        DrawBitmapFontText(statusText, textPosition, new Color(235, 235, 235), 1f);
    }

    private float MeasureChatLineHeight(ChatLine line, float maxPanelWidth)
    {
        var channelPrefix = line.TeamOnly ? "(TEAM) " : string.Empty;
        var speakerPrefix = string.IsNullOrWhiteSpace(line.PlayerName)
            ? channelPrefix
            : $"{channelPrefix}{line.PlayerName}: ";
        var maxContentWidth = Math.Max(48f, maxPanelWidth - (ChatHudPanelHorizontalPadding * 2f));
        var wrappedMessageLines = WrapBitmapFontText(
            line.Text,
            Math.Max(24f, maxContentWidth - MeasureBitmapFontWidth(speakerPrefix, 1f)),
            maxContentWidth);
        return GetWrappedChatPanelHeight(wrappedMessageLines.Count, GetChatHudLineHeight());
    }

    private float GetWrappedChatPanelWidth(List<string> wrappedLines, float firstLinePrefixWidth)
    {
        var width = 0f;
        for (var lineIndex = 0; lineIndex < wrappedLines.Count; lineIndex += 1)
        {
            var lineWidth = MeasureBitmapFontWidth(wrappedLines[lineIndex], 1f);
            if (lineIndex == 0)
            {
                lineWidth += firstLinePrefixWidth;
            }

            width = MathF.Max(width, lineWidth);
        }

        return width;
    }

    private static float GetWrappedChatPanelHeight(int wrappedLineCount, float lineHeight)
    {
        return Math.Max(16f, (wrappedLineCount * lineHeight) + (ChatHudPanelVerticalPadding * 2f));
    }

    private float GetChatHudMaxPanelWidth(float baseX)
    {
        return Math.Max(220f, ViewportWidth - baseX - ChatHudPanelMargin);
    }

    private float GetChatHudLineHeight()
    {
        return Math.Max(16f, MeasureBitmapFontHeight(1f) + 2f);
    }

    private IReadOnlyList<ChatLine> GetVisibleChatLines(float maxPanelWidth, float maxChatHeight, bool includeHistory)
    {
        if (_chatLines.Count == 0)
        {
            return [];
        }

        var sourceLines = new List<ChatLine>(_chatLines.Count);
        if (includeHistory)
        {
            sourceLines.AddRange(_chatLines);
        }
        else
        {
            for (var index = 0; index < _chatLines.Count; index += 1)
            {
                if (_chatLines[index].TicksRemaining > 0)
                {
                    sourceLines.Add(_chatLines[index]);
                }
            }

            if (sourceLines.Count > ClosedChatVisibleLineLimit)
            {
                sourceLines.RemoveRange(0, sourceLines.Count - ClosedChatVisibleLineLimit);
            }
        }

        if (sourceLines.Count == 0)
        {
            return [];
        }

        var newestVisibleIndex = Math.Max(0, sourceLines.Count - 1 - (includeHistory ? _chatScrollOffset : 0));
        var startIndex = newestVisibleIndex;
        var totalHeight = 0f;
        while (startIndex >= 0)
        {
            var lineHeight = MeasureChatLineHeight(sourceLines[startIndex], maxPanelWidth);
            var additionalHeight = totalHeight > 0f ? ChatHudPanelSpacing : 0f;
            if (startIndex < newestVisibleIndex && totalHeight + additionalHeight + lineHeight > maxChatHeight)
            {
                break;
            }

            totalHeight += additionalHeight + lineHeight;
            startIndex -= 1;
        }

        startIndex += 1;
        var resultCount = newestVisibleIndex - startIndex + 1;
        if (resultCount <= 0)
        {
            return [];
        }

        return sourceLines.GetRange(startIndex, resultCount);
    }

    private List<string> WrapBitmapFontText(string text, float firstLineWidth, float continuationLineWidth)
    {
        var wrappedLines = new List<string>();
        var normalizedText = (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var paragraphs = normalizedText.Split('\n');
        var currentLineWidth = Math.Max(1f, firstLineWidth);
        var wrappedAnyParagraph = false;

        for (var paragraphIndex = 0; paragraphIndex < paragraphs.Length; paragraphIndex += 1)
        {
            var paragraph = paragraphs[paragraphIndex];
            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var currentLine = string.Empty;

            if (words.Length == 0)
            {
                wrappedLines.Add(string.Empty);
                currentLineWidth = Math.Max(1f, continuationLineWidth);
                wrappedAnyParagraph = true;
                continue;
            }

            for (var wordIndex = 0; wordIndex < words.Length; wordIndex += 1)
            {
                var word = words[wordIndex];
                var candidate = currentLine.Length == 0 ? word : $"{currentLine} {word}";
                if (MeasureBitmapFontWidth(candidate, 1f) <= currentLineWidth)
                {
                    currentLine = candidate;
                    continue;
                }

                if (currentLine.Length > 0)
                {
                    wrappedLines.Add(currentLine);
                    currentLine = string.Empty;
                    currentLineWidth = Math.Max(1f, continuationLineWidth);
                }

                while (MeasureBitmapFontWidth(word, 1f) > currentLineWidth && word.Length > 0)
                {
                    var splitIndex = GetWrappedBitmapFontChunkLength(word, currentLineWidth);
                    wrappedLines.Add(word[..splitIndex]);
                    word = word[splitIndex..];
                    currentLineWidth = Math.Max(1f, continuationLineWidth);
                }

                currentLine = word;
            }

            if (currentLine.Length > 0)
            {
                wrappedLines.Add(currentLine);
            }

            currentLineWidth = Math.Max(1f, continuationLineWidth);
            wrappedAnyParagraph = true;
        }

        if (!wrappedAnyParagraph)
        {
            wrappedLines.Add(string.Empty);
        }

        return wrappedLines;
    }

    private int GetWrappedBitmapFontChunkLength(string text, float maxWidth)
    {
        var bestLength = 1;
        for (var length = 1; length <= text.Length; length += 1)
        {
            if (MeasureBitmapFontWidth(text[..length], 1f) > maxWidth)
            {
                break;
            }

            bestLength = length;
        }

        return bestLength;
    }

    private string GetTrailingBitmapFontTextThatFits(string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text) || MeasureBitmapFontWidth(text, 1f) <= maxWidth)
        {
            return text;
        }

        var startIndex = text.Length - 1;
        while (startIndex > 0)
        {
            var candidate = text[startIndex..];
            if (MeasureBitmapFontWidth(candidate, 1f) <= maxWidth)
            {
                return candidate;
            }

            startIndex -= 1;
        }

        return text[^1].ToString();
    }

    private static Color GetChatTeamColor(byte team)
    {
        return team switch
        {
            (byte)OpenGarrison.Core.PlayerTeam.Blue => new Color(150, 200, 255),
            (byte)OpenGarrison.Core.PlayerTeam.Red => new Color(255, 180, 170),
            _ => new Color(255, 245, 210),
        };
    }
}
