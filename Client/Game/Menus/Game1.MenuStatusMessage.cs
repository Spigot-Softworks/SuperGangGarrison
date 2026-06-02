#nullable enable

using Microsoft.Xna.Framework;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const float MenuStatusMessageAutoClearSeconds = 5f;
    private const float MenuStatusMessageDefaultScale = 1f;
    private const float MenuStatusMessagePaddingX = 10f;
    private const float MenuStatusMessagePaddingY = 6f;

    private enum MenuStatusMessageAnchor
    {
        Center,
        TopLeft,
    }

    private void SetMenuStatusMessageInternal(string? message, bool persist)
    {
        var normalized = message ?? string.Empty;
        var changed = !string.Equals(normalized, _uiShellState.MenuStatusMessage, StringComparison.Ordinal);
        _uiShellState.MenuStatusMessage = normalized;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            _uiShellState.MenuStatusMessageClearAtUtc = null;
            return;
        }

        if (persist)
        {
            _uiShellState.MenuStatusMessageClearAtUtc = null;
            return;
        }

        if (changed)
        {
            _uiShellState.MenuStatusMessageClearAtUtc = DateTime.UtcNow.AddSeconds(MenuStatusMessageAutoClearSeconds);
        }
    }

    private void SetPersistedMenuStatusMessage(string? message)
    {
        SetMenuStatusMessageInternal(message, persist: true);
    }

    private void UpdateMenuStatusMessageExpiry()
    {
        if (_uiShellState.MenuStatusMessageClearAtUtc is not { } clearAtUtc)
        {
            return;
        }

        if (DateTime.UtcNow < clearAtUtc)
        {
            return;
        }

        _uiShellState.MenuStatusMessage = string.Empty;
        _uiShellState.MenuStatusMessageClearAtUtc = null;
    }

    private void DrawMenuStatusText()
    {
        if (string.IsNullOrWhiteSpace(_menuStatusMessage))
        {
            return;
        }

        var center = new Vector2(ViewportWidth * 0.5f, ViewportHeight - 104f);
        DrawMenuStatusMessageBanner(_menuStatusMessage, center, MenuStatusMessageAnchor.Center, MenuStatusMessageDefaultScale);
    }

    private void DrawHostSetupMenuStatusMessage(HostSetupMenuLayout layout)
    {
        if (string.IsNullOrWhiteSpace(_menuStatusMessage))
        {
            return;
        }

        var maxWidth = layout.Panel.Width - 56f;
        var scale = MenuStatusMessageDefaultScale;
        var text = TrimBitmapMenuText(_menuStatusMessage, maxWidth, scale);
        DrawMenuStatusMessageBanner(text, layout.StatusPosition, MenuStatusMessageAnchor.TopLeft, scale);
    }

    private void DrawMenuStatusMessageBanner(
        string text,
        Vector2 anchor,
        MenuStatusMessageAnchor anchorKind,
        float scale)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var textWidth = MeasureBitmapFontWidth(text, scale);
        var textHeight = MeasureBitmapFontHeight(scale);
        var panelWidth = (int)MathF.Ceiling(textWidth + (MenuStatusMessagePaddingX * 2f));
        var panelHeight = (int)MathF.Ceiling(textHeight + (MenuStatusMessagePaddingY * 2f));
        var panelX = anchorKind switch
        {
            MenuStatusMessageAnchor.Center => (int)MathF.Round(anchor.X - (panelWidth * 0.5f)),
            _ => (int)MathF.Round(anchor.X),
        };
        var panelY = anchorKind switch
        {
            MenuStatusMessageAnchor.Center => (int)MathF.Round(anchor.Y - (panelHeight * 0.5f)),
            _ => (int)MathF.Round(anchor.Y),
        };
        var panelBounds = new Rectangle(panelX, panelY, panelWidth, panelHeight);
        DrawRoundedRectangleOutline(
            panelBounds,
            new Color(59, 51, 46, 200),
            new Color(213, 205, 188, 220),
            outlineThickness: 1,
            radius: 8);

        var textPosition = new Vector2(
            panelBounds.X + MenuStatusMessagePaddingX,
            panelBounds.Y + MenuStatusMessagePaddingY);
        DrawBitmapFontText(text, textPosition, new Color(235, 225, 180), scale);
    }
}
