#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using OpenGarrison.Client.Plugins;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int MaxChatHistoryLines = 128;
    private const int ClosedChatVisibleLineLimit = 6;
    private const int OpenChatFixedLineCount = 7;
    private const int ChatScrollStep = 3;
    private const float ChatHudPanelMargin = 12f;
    private const float ChatHudPanelHorizontalPadding = 6f;
    private const float ChatHudPanelVerticalPadding = 4f;
    private const float ChatHudPanelSpacing = 4f;
    private const int OverheadChatMessageLifetimeTicks = 300;
    private const int OverheadChatMessageFadeTicks = 45;

    private void OpenChat(bool teamOnly)
    {
        _chatOpen = true;
        _chatTeamOnly = teamOnly;
        _chatInput = string.Empty;
        _chatScrollOffset = 0;
        InitializeChatInputCursor();
    }

    private void ResetChatInputState(bool requireOpenKeyRelease = false)
    {
        _chatOpen = false;
        _chatTeamOnly = false;
        _chatSubmitAwaitingOpenKeyRelease = requireOpenKeyRelease;
        _chatInput = string.Empty;
        _chatScrollOffset = 0;
        InitializeChatInputCursor();
    }

    private void SubmitChatMessage()
    {
        var text = _chatInput.Trim();
        var teamOnly = _chatTeamOnly;
        if (!string.IsNullOrWhiteSpace(text))
        {
            if (TrySubmitDirectMessageChatCommand(text))
            {
                ResetChatInputState(requireOpenKeyRelease: true);
                return;
            }

            if (_clientPluginHost is not null)
            {
                var pluginChatResult = _clientPluginHost.ProcessOutgoingChat(new ClientChatSubmitContext(text, teamOnly));
                if (pluginChatResult.IsCancelled || pluginChatResult.IsHandled)
                {
                    ResetChatInputState(requireOpenKeyRelease: true);
                    return;
                }

                text = pluginChatResult.Text.Trim();
                teamOnly = pluginChatResult.TeamOnly;
                if (string.IsNullOrWhiteSpace(text))
                {
                    ResetChatInputState(requireOpenKeyRelease: true);
                    return;
                }
            }

            if (_networkClient.IsConnected)
            {
                _networkClient.SendChat(text, teamOnly);
                ShowLocalOverheadChatMessage(text, teamOnly);
            }
            else
            {
                AppendChatLine(_world.LocalPlayer.DisplayName, text, (byte)_world.LocalPlayer.Team, teamOnly);
                ShowLocalOverheadChatMessage(text, teamOnly);
            }
        }

        ResetChatInputState(requireOpenKeyRelease: true);
    }

    private bool TrySubmitDirectMessageChatCommand(string text)
    {
        if (!text.StartsWith("/dm ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = text[4..].Trim();
        if (string.IsNullOrWhiteSpace(payload))
        {
            AppendDirectMessageChatLine("DM", "Enter a message.", incoming: true);
            return true;
        }

        if (TryResolveDirectMessageTarget(payload, out var targetFriendCode, out var messageText))
        {
            TrySendDirectMessage(targetFriendCode, messageText, echoToChat: true);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(_lastDirectMessageSenderFriendCode))
        {
            TrySendDirectMessage(_lastDirectMessageSenderFriendCode, payload, echoToChat: true);
            return true;
        }

        AppendDirectMessageChatLine("DM", "No recent sender.", incoming: true);
        return true;
    }

    private bool TryResolveDirectMessageTarget(string payload, out string targetFriendCode, out string messageText)
    {
        targetFriendCode = string.Empty;
        messageText = string.Empty;

        var firstSpace = payload.IndexOf(' ', StringComparison.Ordinal);
        if (payload.StartsWith('['))
        {
            var closingBracket = payload.IndexOf(']', StringComparison.Ordinal);
            if (closingBracket > 1 && closingBracket + 1 < payload.Length && char.IsWhiteSpace(payload[closingBracket + 1]))
            {
                var bracketName = payload[1..closingBracket].Trim();
                var bracketMatch = _friendList.Friends.FirstOrDefault(friend =>
                    string.Equals(GetFriendDisplayName(friend.FriendCode, friend.DisplayName), bracketName, StringComparison.OrdinalIgnoreCase));
                if (bracketMatch is not null)
                {
                    targetFriendCode = bracketMatch.FriendCode;
                    messageText = payload[(closingBracket + 2)..].Trim();
                    return !string.IsNullOrWhiteSpace(messageText);
                }
            }
        }

        if (firstSpace > 0
            && ClientIdentityDocument.TryNormalizeFriendCode(payload[..firstSpace], out var codeFromPrefix)
            && _friendList.Friends.Any(friend => string.Equals(friend.FriendCode, codeFromPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            targetFriendCode = codeFromPrefix;
            messageText = payload[(firstSpace + 1)..].Trim();
            return !string.IsNullOrWhiteSpace(messageText);
        }

        var match = _friendList.Friends
            .Select(friend => new
            {
                friend.FriendCode,
                Label = GetFriendDisplayName(friend.FriendCode, friend.DisplayName),
            })
            .Where(friend => !string.IsNullOrWhiteSpace(friend.Label)
                && payload.StartsWith(friend.Label + " ", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(friend => friend.Label.Length)
            .FirstOrDefault();
        if (match is null)
        {
            return false;
        }

        targetFriendCode = match.FriendCode;
        messageText = payload[(match.Label.Length + 1)..].Trim();
        return !string.IsNullOrWhiteSpace(messageText);
    }

    private void AppendChatLine(string playerName, string text, byte team, bool teamOnly, byte playerSlot = 0)
    {
        if (IsScoreboardSlotMuted(playerSlot))
        {
            return;
        }

        var channelPrefix = teamOnly ? "(TEAM) " : string.Empty;
        var line = string.IsNullOrWhiteSpace(playerName)
            ? $"{channelPrefix}{text}"
            : $"{channelPrefix}{playerName}: {text}";
        if (_chatOpen && _chatScrollOffset > 0)
        {
            _chatScrollOffset += 1;
        }

        _chatLines.Add(new ChatLine(playerName, text, team, teamOnly, playerSlot: playerSlot));
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

    private void AppendDirectMessageChatLine(string playerName, string text, bool incoming)
    {
        var line = $"[{playerName}]: {text}";
        if (_chatOpen && _chatScrollOffset > 0)
        {
            _chatScrollOffset += 1;
        }

        _chatLines.Add(new ChatLine(playerName, text, 0, teamOnly: false, directMessage: true));
        while (_chatLines.Count > MaxChatHistoryLines)
        {
            _chatLines.RemoveAt(0);
            if (_chatScrollOffset > 0)
            {
                _chatScrollOffset -= 1;
            }
        }

        ClampChatScrollOffset();
        AddConsoleLine(incoming ? $"[dm] {line}" : $"[dm sent] {line}");
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

        AdvanceOverheadChatMessages();
    }

    private void TryShowOverheadChatMessage(ChatRelayMessage chatRelay)
    {
        if (!_overheadChatEnabled)
        {
            return;
        }

        if (!TryResolveOverheadChatPlayerSlot(chatRelay, out var slot))
        {
            return;
        }

        if (IsScoreboardSlotMuted(slot))
        {
            return;
        }

        ShowOverheadChatMessage(slot, chatRelay.Text, chatRelay.TeamOnly);
    }

    private void ShowLocalOverheadChatMessage(string text, bool teamOnly)
    {
        if (!_overheadChatEnabled)
        {
            return;
        }

        var normalizedText = NormalizeOverheadChatText(text);
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return;
        }

        _localOverheadChatMessage = new OverheadChatMessage(
            normalizedText,
            teamOnly,
            OverheadChatMessageLifetimeTicks);
    }

    private void ShowOverheadChatMessage(byte slot, string text, bool teamOnly)
    {
        if (!_overheadChatEnabled || slot == 0 || IsScoreboardSlotMuted(slot))
        {
            return;
        }

        var normalizedText = NormalizeOverheadChatText(text);
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return;
        }

        _overheadChatMessagesBySlot[slot] = new OverheadChatMessage(
            normalizedText,
            teamOnly,
            OverheadChatMessageLifetimeTicks);
    }

    private void AdvanceOverheadChatMessages()
    {
        if (_localOverheadChatMessage is not null)
        {
            _localOverheadChatMessage.TicksRemaining -= 1;
            if (_localOverheadChatMessage.TicksRemaining <= 0)
            {
                _localOverheadChatMessage = null;
            }
        }

        if (_overheadChatMessagesBySlot.Count == 0)
        {
            return;
        }

        _staleOverheadChatSlots.Clear();
        foreach (var entry in _overheadChatMessagesBySlot)
        {
            entry.Value.TicksRemaining -= 1;
            if (entry.Value.TicksRemaining <= 0)
            {
                _staleOverheadChatSlots.Add(entry.Key);
            }
        }

        for (var index = 0; index < _staleOverheadChatSlots.Count; index += 1)
        {
            _overheadChatMessagesBySlot.Remove(_staleOverheadChatSlots[index]);
        }
    }

    private bool TryResolveOverheadChatPlayerSlot(ChatRelayMessage chatRelay, out byte slot)
    {
        if (chatRelay.PlayerSlot != 0)
        {
            if (IsOverheadChatPlayerSlotActive(chatRelay.PlayerSlot))
            {
                slot = chatRelay.PlayerSlot;
                return true;
            }

            slot = 0;
            return false;
        }

        return TryResolveOverheadChatPlayerSlotByName(chatRelay.PlayerName, chatRelay.Team, out slot);
    }

    private bool IsOverheadChatPlayerSlotActive(byte slot)
    {
        foreach (var candidate in EnumerateOverheadChatPlayerSlotCandidates())
        {
            if (candidate.Slot == slot)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryResolveOverheadChatPlayerSlotByName(string playerName, byte team, out byte slot)
    {
        slot = 0;
        if (string.IsNullOrWhiteSpace(playerName) || playerName[0] == '[')
        {
            return false;
        }

        foreach (var candidate in EnumerateOverheadChatPlayerSlotCandidates())
        {
            if (team != 0 && (byte)candidate.Player.Team != team)
            {
                continue;
            }

            if (!string.Equals(candidate.Player.DisplayName, playerName, StringComparison.Ordinal))
            {
                continue;
            }

            if (slot != 0)
            {
                slot = 0;
                return false;
            }

            slot = candidate.Slot;
        }

        return slot != 0;
    }

    private IEnumerable<(byte Slot, PlayerEntity Player)> EnumerateOverheadChatPlayerSlotCandidates()
    {
        if (_networkClient.IsConnected && !_networkClient.IsSpectator && _networkClient.LocalPlayerSlot != 0)
        {
            yield return (_networkClient.LocalPlayerSlot, _world.LocalPlayer);
        }
        else if (!_networkClient.IsConnected)
        {
            yield return (SimulationWorld.LocalPlayerSlot, _world.LocalPlayer);
        }

        foreach (var player in EnumerateRemotePlayersForView())
        {
            if (_world.TryGetPlayerNetworkSlot(player, out var slot))
            {
                yield return (slot, player);
            }
        }
    }

    private static string NormalizeOverheadChatText(string text)
    {
        return (text ?? string.Empty)
            .Trim()
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ');
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
        _chatScrollOffset = Math.Clamp(_chatScrollOffset, 0, Math.Max(0, _chatLines.Count - OpenChatFixedLineCount));
    }

    private void DrawChatHud()
    {
        var baseX = 18f;
        var lineHeight = GetChatHudLineHeight();
        var promptPrefix = _chatTeamOnly ? "(TEAM) > " : "> ";
        var maxInputWidth = Math.Max(24f, Math.Max(280, ViewportWidth / 3) - 18f - MeasureBitmapFontWidth(promptPrefix, 1f));
        var promptLines = WrapBitmapFontText(GetTextWithCursor(_chatInput, _chatInputCursorIndex), maxInputWidth, maxInputWidth);
        var promptHeight = Math.Max(24, (int)MathF.Ceiling(promptLines.Count * lineHeight + 12f));
        var promptRectangle = new Rectangle(
            12,
            ViewportHeight - 94 - promptHeight,
            Math.Max(280, ViewportWidth / 3),
            promptHeight);
        var maxPanelWidth = promptRectangle.Width;
        var fixedOpenContentHeight = OpenChatFixedLineCount * lineHeight;
        var maxChatHeight = _chatOpen ? fixedOpenContentHeight : Math.Max(72f, promptRectangle.Y - 24f);
        var visibleLines = GetVisibleChatLines(maxPanelWidth, maxChatHeight, _chatOpen);

        if (visibleLines.Count > 0 || _chatOpen)
        {
            int panelHeight;
            if (_chatOpen)
            {
                panelHeight = (int)MathF.Ceiling(fixedOpenContentHeight + ChatHudPanelVerticalPadding * 2f);
            }
            else
            {
                var totalChatContentHeight = 0f;
                for (var index = 0; index < visibleLines.Count; index += 1)
                {
                    totalChatContentHeight += MeasureChatLineHeight(visibleLines[index], maxPanelWidth);
                }

                panelHeight = (int)MathF.Ceiling(totalChatContentHeight + ChatHudPanelVerticalPadding * 2f);
            }

            var panelY = promptRectangle.Y - 10 - panelHeight;
            var chatPanelRect = new Rectangle(promptRectangle.X, panelY, promptRectangle.Width, panelHeight);

            float fillAlpha, borderAlpha;
            if (_chatOpen)
            {
                fillAlpha = 0.70f;
                borderAlpha = 0.70f;
            }
            else
            {
                var maxLineAlpha = 0f;
                for (var index = 0; index < visibleLines.Count; index += 1)
                {
                    maxLineAlpha = MathF.Max(maxLineAlpha, MathF.Min(1f, visibleLines[index].TicksRemaining / 120f));
                }

                fillAlpha = 0.40f * maxLineAlpha;
                borderAlpha = 0.40f * maxLineAlpha;
            }

            DrawRoundedRectangleOutline(
                chatPanelRect,
                new Color(0, 0, 0) * fillAlpha,
                new Color(49, 45, 26) * borderAlpha,
                outlineThickness: 2,
                radius: 6);

            float startTextY;
            if (_chatOpen)
            {
                // Fill from bottom: compute total content height and start below panel bottom
                var totalContentHeight = 0f;
                for (var index = 0; index < visibleLines.Count; index += 1)
                {
                    totalContentHeight += MeasureChatLineHeight(visibleLines[index], maxPanelWidth);
                }

                startTextY = chatPanelRect.Bottom - ChatHudPanelVerticalPadding - totalContentHeight;
            }
            else
            {
                startTextY = panelY + ChatHudPanelVerticalPadding;
            }

            var textY = startTextY;
            for (var index = 0; index < visibleLines.Count; index += 1)
            {
                var line = visibleLines[index];
                var alpha = _chatOpen ? 1f : MathF.Min(1f, line.TicksRemaining / 120f);
                DrawChatLine(line, new Vector2(baseX, textY), alpha, maxPanelWidth);
                textY += MeasureChatLineHeight(line, maxPanelWidth);
            }

            if (_chatOpen && _chatLines.Count > OpenChatFixedLineCount)
            {
                const int scrollbarWidth = 4;
                const int scrollbarMargin = 3;
                var trackX = chatPanelRect.Right - scrollbarWidth - scrollbarMargin;
                var trackY = chatPanelRect.Y + scrollbarMargin;
                var trackHeight = chatPanelRect.Height - scrollbarMargin * 2;
                var thumbHeightRatio = (float)OpenChatFixedLineCount / MathF.Max(1f, _chatLines.Count);
                var thumbHeight = (int)MathF.Max(8f, trackHeight * thumbHeightRatio);
                var scrollFraction = _chatScrollOffset / MathF.Max(1f, _chatLines.Count - OpenChatFixedLineCount);
                var thumbY = trackY + (int)MathF.Round((trackHeight - thumbHeight) * (1f - scrollFraction));
                thumbY = Math.Clamp(thumbY, trackY, trackY + trackHeight - thumbHeight);

                DrawInsetHudPanel(new Rectangle(trackX, trackY, scrollbarWidth, trackHeight),
                    new Color(30, 30, 30, 180), new Color(20, 20, 20, 180));
                DrawInsetHudPanel(new Rectangle(trackX, thumbY, scrollbarWidth, thumbHeight),
                    new Color(130, 120, 95, 220), new Color(160, 150, 120, 220));
            }
        }

        if (!_chatOpen)
        {
            return;
        }

        DrawRoundedRectangleOutline(promptRectangle, new Color(0, 0, 0) * 0.70f, new Color(49, 45, 26) * 0.70f, outlineThickness: 2, radius: 6);
        DrawChatPrompt(promptRectangle, promptPrefix, promptLines);
    }

    private void DrawChatLine(ChatLine line, Vector2 position, float alpha, float maxPanelWidth)
    {
        var directMessageColor = new Color(138, 218, 255);
        var channelPrefix = line.TeamOnly ? "(TEAM) " : string.Empty;
        var speakerPrefix = string.IsNullOrWhiteSpace(line.PlayerName)
            ? channelPrefix
            : line.DirectMessage
                ? $"[{line.PlayerName}]: "
                : $"{channelPrefix}{line.PlayerName}: ";
        var speakerWidth = MeasureBitmapFontWidth(speakerPrefix, 1f);
        var maxContentWidth = Math.Max(48f, maxPanelWidth - (ChatHudPanelHorizontalPadding * 2f));
        var wrappedMessageLines = WrapBitmapFontText(
            line.Text,
            Math.Max(24f, maxContentWidth - speakerWidth),
            maxContentWidth);
        var lineHeight = GetChatHudLineHeight();

        for (var lineIndex = 0; lineIndex < wrappedMessageLines.Count; lineIndex += 1)
        {
            var textPosition = new Vector2(position.X, position.Y + lineIndex * lineHeight);
            if (lineIndex == 0 && speakerPrefix.Length > 0)
            {
                DrawBitmapFontText(speakerPrefix, textPosition, (line.DirectMessage ? directMessageColor : GetChatTeamColor(line.Team)) * alpha, 1f);
            }

            DrawBitmapFontText(
                wrappedMessageLines[lineIndex],
                new Vector2(textPosition.X + (lineIndex == 0 ? speakerWidth : 0f), textPosition.Y),
                (line.DirectMessage ? directMessageColor : new Color(235, 235, 235)) * alpha,
                1f);
        }
    }

    private void DrawChatPrompt(Rectangle promptRectangle, string promptPrefix, List<string> promptLines)
    {
        var prefixWidth = MeasureBitmapFontWidth(promptPrefix, 1f);
        var promptPosition = new Vector2(promptRectangle.X + 8f, promptRectangle.Y + 6f);
        DrawBitmapFontText(promptPrefix, promptPosition, new Color(255, 245, 210), 1f);

        var inputX = promptPosition.X + prefixWidth;
        var inputY = promptPosition.Y;
        var lineHeight = GetChatHudLineHeight();

        if (promptLines.Count == 1 && HasTextSelection(_chatInputCursorIndex, _chatInputSelectionStart))
        {
            var maxInputWidth = Math.Max(24f, promptRectangle.Width - 18f - prefixWidth);
            var (visibleInputWithOffset, visibleStartIndex) = GetTrailingBitmapFontTextThatFitsWithOffset(_chatInput, maxInputWidth);
            var (selectionStart, selectionLength) = GetTextSelectionRange(_chatInputCursorIndex, _chatInputSelectionStart);
            var visibleSelectionStart = Math.Max(0, Math.Min(visibleInputWithOffset.Length, selectionStart - visibleStartIndex));
            var visibleSelectionEnd = Math.Max(visibleSelectionStart, Math.Min(visibleInputWithOffset.Length, selectionStart + selectionLength - visibleStartIndex));
            var visibleSelectionLength = Math.Max(0, visibleSelectionEnd - visibleSelectionStart);
            if (visibleSelectionLength > 0)
            {
                DrawBitmapFontTextWithSelection(
                    visibleInputWithOffset,
                    new Vector2(inputX, inputY),
                    visibleSelectionStart + visibleSelectionLength,
                    visibleSelectionStart,
                    Color.White,
                    Color.Black,
                    Color.White,
                    1f);
                return;
            }
        }

        for (var lineIndex = 0; lineIndex < promptLines.Count; lineIndex += 1)
        {
            DrawBitmapFontText(
                promptLines[lineIndex],
                new Vector2(inputX, inputY + (lineIndex * lineHeight)),
                Color.White,
                1f);
        }
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
        var lineHeight = GetChatHudLineHeight();
        var statusRectangle = new Rectangle(
            promptRectangle.X,
            promptRectangle.Bottom + 6,
            promptRectangle.Width,
            (int)MathF.Ceiling(lineHeight + 10f));
        DrawInsetHudPanel(statusRectangle, new Color(0, 0, 0, 220), new Color(49, 45, 26, 220));

        var textWidth = MeasureBitmapFontWidth(statusText, 1f);
        var textPosition = new Vector2(
            Math.Max(statusRectangle.X + 8f, statusRectangle.Right - textWidth - 10f),
            statusRectangle.Y + 5f);
        DrawBitmapFontText(statusText, textPosition, new Color(235, 235, 235), 1f);
    }

    private float MeasureChatLineHeight(ChatLine line, float maxPanelWidth)
    {
        var channelPrefix = line.TeamOnly ? "(TEAM) " : string.Empty;
        var speakerPrefix = string.IsNullOrWhiteSpace(line.PlayerName)
            ? channelPrefix
            : line.DirectMessage
                ? $"[{line.PlayerName}]: "
                : $"{channelPrefix}{line.PlayerName}: ";
        var maxContentWidth = Math.Max(48f, maxPanelWidth - (ChatHudPanelHorizontalPadding * 2f));
        var wrappedMessageLines = WrapBitmapFontText(
            line.Text,
            Math.Max(24f, maxContentWidth - MeasureBitmapFontWidth(speakerPrefix, 1f)),
            maxContentWidth);
        var lineHeight = GetChatHudLineHeight();
        return Math.Max(lineHeight, wrappedMessageLines.Count * lineHeight);
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

    private List<ChatLine> GetVisibleChatLines(float maxPanelWidth, float maxChatHeight, bool includeHistory)
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
            if (startIndex < newestVisibleIndex && totalHeight + lineHeight > maxChatHeight)
            {
                break;
            }

            totalHeight += lineHeight;
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

    private (string Text, int StartIndex) GetTrailingBitmapFontTextThatFitsWithOffset(string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text) || MeasureBitmapFontWidth(text, 1f) <= maxWidth)
        {
            return (text, 0);
        }

        var startIndex = text.Length - 1;
        while (startIndex > 0)
        {
            var candidate = text[startIndex..];
            if (MeasureBitmapFontWidth(candidate, 1f) <= maxWidth)
            {
                return (candidate, startIndex);
            }

            startIndex -= 1;
        }

        return (text[^1].ToString(), text.Length - 1);
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
