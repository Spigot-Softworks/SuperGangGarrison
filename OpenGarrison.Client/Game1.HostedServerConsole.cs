#nullable enable

using Microsoft.Xna.Framework;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void DrawHostedServerConsoleTab(HostSetupMenuLayout menuLayout)
    {
        var consoleSnapshot = GetHostedServerConsoleSnapshot();
        var consoleLayout = HostSetupMenuLayoutCalculator.CreateHostedServerConsoleLayout(menuLayout.Panel);

        _spriteBatch.Draw(_pixel, consoleLayout.LogBounds, new Color(24, 25, 30, 230));
        _spriteBatch.Draw(_pixel, consoleLayout.SummaryBounds, new Color(28, 30, 34, 230));
        DrawBitmapFontText("Recent Output", new Vector2(consoleLayout.LogBounds.X + 10f, consoleLayout.LogBounds.Y + 8f), Color.White, 0.95f);
        DrawBitmapFontText("Live Status", new Vector2(consoleLayout.SummaryBounds.X + 10f, consoleLayout.SummaryBounds.Y + 8f), Color.White, 0.95f);

        var consoleLines = consoleSnapshot.ConsoleLines;
        var availableLineCount = Math.Max(1, (consoleLayout.LogBounds.Height - 38) / 18);
        var firstLineIndex = Math.Max(0, consoleLines.Count - availableLineCount);
        var drawY = consoleLayout.LogBounds.Y + 30f;
        if (consoleLines.Count == 0)
        {
            _spriteBatch.DrawString(_consoleFont, "No server output yet.", new Vector2(consoleLayout.LogBounds.X + 12f, drawY), new Color(200, 200, 200));
        }
        else
        {
            for (var index = firstLineIndex; index < consoleLines.Count; index += 1)
            {
                var line = TrimConsoleText(consoleLines[index], consoleLayout.LogBounds.Width - 24f);
                _spriteBatch.DrawString(_consoleFont, line, new Vector2(consoleLayout.LogBounds.X + 12f, drawY), new Color(230, 232, 235));
                drawY += 18f;
            }
        }

        var summaryRows = new (string Label, string Value)[]
        {
            ("Server", consoleSnapshot.StatusName),
            ("Port", consoleSnapshot.StatusPort),
            ("Players", consoleSnapshot.StatusPlayers),
            ("Lobby", consoleSnapshot.StatusLobby),
            ("Map", consoleSnapshot.StatusMap),
            ("Rules", consoleSnapshot.StatusRules),
            ("Runtime", consoleSnapshot.StatusRuntime),
            ("World", consoleSnapshot.StatusWorld),
        };
        var summaryRowGap = consoleLayout.CompactLayout ? 4 : 5;
        var availableSummaryHeight = Math.Max(1, consoleLayout.SummaryBounds.Height - 32 - (summaryRowGap * (summaryRows.Length - 1)));
        var summaryRowHeight = Math.Max(consoleLayout.CompactLayout ? 24 : 40, availableSummaryHeight / summaryRows.Length);

        for (var index = 0; index < summaryRows.Length; index += 1)
        {
            var rowBounds = new Rectangle(
                consoleLayout.SummaryBounds.X + 10,
                consoleLayout.SummaryBounds.Y + 32 + (index * (summaryRowHeight + summaryRowGap)),
                consoleLayout.SummaryBounds.Width - 20,
                summaryRowHeight);
            DrawHostedServerSummaryRow(rowBounds, summaryRows[index].Label, summaryRows[index].Value);
        }

        DrawBitmapFontText("Console Command", new Vector2(consoleLayout.CommandBounds.X, consoleLayout.CommandBounds.Y - 20f), new Color(210, 210, 210), 0.9f);
        DrawMenuInputBox(consoleLayout.CommandBounds, consoleSnapshot.CommandInput, _hostSetupEditField == HostSetupEditField.ServerConsoleCommand);
        DrawMenuButton(consoleLayout.SendBounds, "Send", false);
        DrawMenuButton(consoleLayout.ClearBounds, "Clear", false);
        DrawMenuButton(consoleLayout.StatusCommandBounds, "Status", false);
        DrawMenuButton(consoleLayout.PlayersCommandBounds, "Players", false);
        DrawMenuButton(consoleLayout.RotationCommandBounds, "Rotation", false);
        DrawMenuButton(consoleLayout.HelpCommandBounds, "Help", false);
        DrawMenuButton(consoleLayout.HostBounds, GetHostSetupPrimaryButtonLabel(), false);
        DrawMenuButton(consoleLayout.BackBounds, GetHostSetupSecondaryButtonLabel(), IsServerLauncherMode && IsHostedServerRunning);
        if (!IsHostedServerRunning)
        {
            DrawMenuButton(menuLayout.TerminalButtonBounds, "Run In Terminal", false);
        }

        DrawBitmapFontText(
            "Use Enter or Send to dispatch a server command to the dedicated process.",
            new Vector2(menuLayout.Panel.X + 28f, menuLayout.Panel.Bottom - 90f),
            new Color(210, 210, 210),
            0.82f);

        if (!string.IsNullOrWhiteSpace(_menuStatusMessage))
        {
            DrawBitmapFontText(_menuStatusMessage, new Vector2(menuLayout.Panel.X + 28f, menuLayout.Panel.Bottom - 38f), new Color(230, 220, 180), 1f);
        }
    }

    private void DrawHostedServerSummaryRow(Rectangle bounds, string label, string value)
    {
        _spriteBatch.Draw(_pixel, bounds, new Color(44, 46, 52, 180));
        var compactBounds = bounds.Height < 34;
        DrawBitmapFontText(label.ToUpperInvariant(), new Vector2(bounds.X + 8f, bounds.Y + 4f), new Color(210, 210, 210), compactBounds ? 0.7f : 0.82f);
        var valueY = bounds.Y + MathF.Max(12f, bounds.Height - 18f);
        _spriteBatch.DrawString(_consoleFont, TrimConsoleText(value, bounds.Width - 16f), new Vector2(bounds.X + 10f, valueY), Color.White);
    }

    private void ExecuteHostedServerCommandFromUi(string command)
    {
        if (TrySendHostedServerCommand(command, out var error))
        {
            _menuStatusMessage = "Command sent.";
        }
        else
        {
            _menuStatusMessage = error;
        }
    }

    private string TrimConsoleText(string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text) || _consoleFont.MeasureString(text).X <= maxWidth)
        {
            return text;
        }

        const string ellipsis = "...";
        var trimmed = text;
        while (trimmed.Length > 0 && _consoleFont.MeasureString(trimmed + ellipsis).X > maxWidth)
        {
            trimmed = trimmed[..^1];
        }

        return trimmed.Length == 0 ? ellipsis : trimmed + ellipsis;
    }
}
