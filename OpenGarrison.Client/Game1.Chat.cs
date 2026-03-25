#nullable enable

using Microsoft.Xna.Framework;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void OpenChat(bool teamOnly)
    {
        _chatOpen = true;
        _chatTeamOnly = teamOnly;
        _chatInput = string.Empty;
    }

    private void ResetChatInputState(bool requireOpenKeyRelease = false)
    {
        _chatOpen = false;
        _chatTeamOnly = false;
        _chatSubmitAwaitingOpenKeyRelease = requireOpenKeyRelease;
        _chatInput = string.Empty;
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
        _chatLines.Add(new ChatLine(playerName, text, team, teamOnly));
        while (_chatLines.Count > 6)
        {
            _chatLines.RemoveAt(0);
        }

        AddConsoleLine(teamOnly ? $"[team chat] {line}" : $"[chat] {line}");
    }

    private void AdvanceChatHud()
    {
        for (var index = _chatLines.Count - 1; index >= 0; index -= 1)
        {
            _chatLines[index].TicksRemaining -= 1;
            if (_chatLines[index].TicksRemaining <= 0)
            {
                _chatLines.RemoveAt(index);
            }
        }
    }

    private void DrawChatHud()
    {
        var baseX = 18f;
        var lineHeight = Math.Max(16f, MeasureBitmapFontHeight(1f) + 10f);
        var promptRectangle = new Rectangle(
            12,
            ViewportHeight - 118,
            Math.Max(280, ViewportWidth / 3),
            24);
        var baseY = promptRectangle.Y - 14f - Math.Max(0f, (_chatLines.Count - 1) * lineHeight);
        for (var index = 0; index < _chatLines.Count; index += 1)
        {
            var line = _chatLines[index];
            var alpha = _chatOpen ? 1f : MathF.Min(1f, line.TicksRemaining / 120f);
            DrawChatLine(line, new Vector2(baseX, baseY + index * lineHeight), alpha);
        }

        if (!_chatOpen)
        {
            return;
        }

        var promptPrefix = _chatTeamOnly ? "(TEAM) > " : "> ";
        var promptText = $"{promptPrefix}{_chatInput}_";
        promptRectangle.Width = Math.Max(promptRectangle.Width, (int)MathF.Ceiling(MeasureBitmapFontWidth(promptText, 1f) + 18f));
        DrawInsetHudPanel(promptRectangle, new Color(0, 0, 0, 220), new Color(49, 45, 26, 220));
        DrawBitmapFontText(promptPrefix, new Vector2(promptRectangle.X + 8, promptRectangle.Y + 6), new Color(255, 245, 210), 1f);
        DrawBitmapFontText($"{_chatInput}_", new Vector2(promptRectangle.X + 8 + MeasureBitmapFontWidth(promptPrefix, 1f), promptRectangle.Y + 6), Color.White, 1f);
    }

    private void DrawChatLine(ChatLine line, Vector2 position, float alpha)
    {
        var channelPrefix = line.TeamOnly ? "(TEAM) " : string.Empty;
        var speakerPrefix = string.IsNullOrWhiteSpace(line.PlayerName)
            ? channelPrefix
            : $"{channelPrefix}{line.PlayerName}: ";
        var speakerWidth = MeasureBitmapFontWidth(speakerPrefix, 1f);
        var messageWidth = MeasureBitmapFontWidth(line.Text, 1f);
        var width = Math.Max(96f, speakerWidth + messageWidth + 14f);
        var height = Math.Max(16f, MeasureBitmapFontHeight(1f) + 8f);
        DrawInsetHudPanel(
            new Rectangle((int)(position.X - 6f), (int)(position.Y - 2f), (int)MathF.Ceiling(width), (int)MathF.Ceiling(height)),
            new Color(0, 0, 0) * (0.82f * alpha),
            new Color(49, 45, 26) * (0.82f * alpha));

        if (speakerPrefix.Length > 0)
        {
            DrawBitmapFontText(speakerPrefix, position, GetChatTeamColor(line.Team) * alpha, 1f);
        }

        DrawBitmapFontText(line.Text, new Vector2(position.X + speakerWidth, position.Y), new Color(235, 235, 235) * alpha, 1f);
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
