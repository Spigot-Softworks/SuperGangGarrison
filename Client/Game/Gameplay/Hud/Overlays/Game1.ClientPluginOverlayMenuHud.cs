#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenGarrison.Client;

public partial class Game1
{
    private static readonly Color ClientPluginOverlayTitleColor = new(255, 245, 210);
    private static readonly Color ClientPluginOverlaySubtitleColor = new(220, 220, 220);
    private static readonly Color ClientPluginOverlayBreadcrumbColor = new(196, 182, 126);
    private static readonly Color ClientPluginOverlayEntryColor = new(235, 235, 235);
    private const float ClientPluginOverlayGapFromChat = 70f;
    private const int ClientPluginOverlayMaxVisibleEntries = 6;

    private void ShowClientPluginOverlayMenu(string pluginId, string title, string subtitle, string breadcrumb, IReadOnlyList<string> entries)
    {
        var sanitizedEntries = (entries ?? Array.Empty<string>())
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Take(ClientPluginOverlayMaxVisibleEntries)
            .Select(entry => entry.Trim())
            .ToArray();
        if (sanitizedEntries.Length == 0)
        {
            _clientPluginOverlayMenu = null;
            return;
        }

        _clientPluginOverlayMenu = new ClientPluginOverlayMenuState(
            pluginId,
            title?.Trim() ?? string.Empty,
            subtitle?.Trim() ?? string.Empty,
            breadcrumb?.Trim() ?? string.Empty,
            sanitizedEntries);
    }

    private void HideClientPluginOverlayMenu(string pluginId)
    {
        if (_clientPluginOverlayMenu is null
            || !string.Equals(_clientPluginOverlayMenu.PluginId, pluginId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _clientPluginOverlayMenu = null;
    }

    private void DrawClientPluginOverlayMenuHud()
    {
        if (_clientPluginOverlayMenu is not { Entries.Count: > 0 } menu)
        {
            return;
        }

        var promptRectangle = new Rectangle(
            12,
            ViewportHeight - 118,
            Math.Max(280, ViewportWidth / 3),
            24);
        var baseX = 18f;
        var maxPanelWidth = GetChatHudMaxPanelWidth(baseX);
        var lines = BuildClientPluginOverlayMenuLines(menu);
        if (lines.Count == 0)
        {
            return;
        }

        var totalHeight = 0f;
        for (var index = 0; index < lines.Count; index += 1)
        {
            totalHeight += MeasureClientPluginOverlayMenuLineHeight(lines[index].Text, maxPanelWidth);
            if (index < lines.Count - 1)
            {
                totalHeight += ChatHudPanelSpacing;
            }
        }

        var lineY = Math.Max(12f, promptRectangle.Y - ClientPluginOverlayGapFromChat - totalHeight);
        for (var index = 0; index < lines.Count; index += 1)
        {
            DrawClientPluginOverlayMenuLine(lines[index].Text, new Vector2(baseX, lineY), lines[index].Color, maxPanelWidth);
            lineY += MeasureClientPluginOverlayMenuLineHeight(lines[index].Text, maxPanelWidth) + ChatHudPanelSpacing;
        }
    }

    private static List<ClientPluginOverlayMenuLine> BuildClientPluginOverlayMenuLines(ClientPluginOverlayMenuState menu)
    {
        var lines = new List<ClientPluginOverlayMenuLine>(menu.Entries.Count + 3);
        if (!string.IsNullOrWhiteSpace(menu.Title))
        {
            lines.Add(new ClientPluginOverlayMenuLine(menu.Title, ClientPluginOverlayTitleColor));
        }

        if (!string.IsNullOrWhiteSpace(menu.Breadcrumb))
        {
            lines.Add(new ClientPluginOverlayMenuLine(menu.Breadcrumb, ClientPluginOverlayBreadcrumbColor));
        }

        if (!string.IsNullOrWhiteSpace(menu.Subtitle))
        {
            lines.Add(new ClientPluginOverlayMenuLine(menu.Subtitle, ClientPluginOverlaySubtitleColor));
        }

        for (var index = 0; index < Math.Min(ClientPluginOverlayMaxVisibleEntries, menu.Entries.Count); index += 1)
        {
            lines.Add(new ClientPluginOverlayMenuLine($"{index + 1}.) {menu.Entries[index]}", ClientPluginOverlayEntryColor));
        }

        return lines;
    }

    private float MeasureClientPluginOverlayMenuLineHeight(string text, float maxPanelWidth)
    {
        var maxContentWidth = Math.Max(48f, maxPanelWidth - (ChatHudPanelHorizontalPadding * 2f));
        var wrappedLines = WrapBitmapFontText(text, maxContentWidth, maxContentWidth);
        return GetWrappedChatPanelHeight(wrappedLines.Count, GetChatHudLineHeight());
    }

    private void DrawClientPluginOverlayMenuLine(string text, Vector2 position, Color color, float maxPanelWidth)
    {
        var maxContentWidth = Math.Max(48f, maxPanelWidth - (ChatHudPanelHorizontalPadding * 2f));
        var wrappedLines = WrapBitmapFontText(text, maxContentWidth, maxContentWidth);
        var lineHeight = GetChatHudLineHeight();
        var width = Math.Max(
            96f,
            GetWrappedChatPanelWidth(wrappedLines, 0f) + (ChatHudPanelHorizontalPadding * 2f));
        var height = Math.Max(
            16f,
            GetWrappedChatPanelHeight(wrappedLines.Count, lineHeight));
        DrawInsetHudPanel(
            new Rectangle((int)(position.X - 6f), (int)(position.Y - 2f), (int)MathF.Ceiling(width), (int)MathF.Ceiling(height)),
            new Color(0, 0, 0, 220),
            new Color(49, 45, 26, 220));

        for (var lineIndex = 0; lineIndex < wrappedLines.Count; lineIndex += 1)
        {
            DrawBitmapFontText(
                wrappedLines[lineIndex],
                new Vector2(position.X, position.Y + lineIndex * lineHeight),
                color,
                1f);
        }
    }

    private readonly record struct ClientPluginOverlayMenuLine(string Text, Color Color);
}
