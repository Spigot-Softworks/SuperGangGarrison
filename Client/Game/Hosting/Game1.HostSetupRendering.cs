#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void DrawHostSetupMenu()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.78f);

        var layout = HostSetupMenuLayoutCalculator.CreateMenuLayout(ViewportWidth, ViewportHeight, _hostMapEntries.Count, IsServerLauncherMode);
        var panel = layout.Panel;
        var compactLayout = layout.CompactLayout;
        var titleScale = compactLayout ? 0.92f : 1f;
        var subtitleScale = compactLayout ? 0.78f : 0.9f;
        var headerScale = compactLayout ? 0.72f : 0.8f;
        var rowScale = compactLayout ? 0.78f : 0.9f;
        var fieldLabelScale = compactLayout ? 0.74f : 0.9f;
        var infoScale = compactLayout ? 0.74f : 0.85f;
        var inputScale = compactLayout ? 0.86f : 1f;
        var buttonScale = compactLayout ? 0.84f : 1f;
        _spriteBatch.Draw(_pixel, panel, new Color(34, 35, 39, 235));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, panel.Width, 3), new Color(210, 210, 210));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Bottom - 3, panel.Width, 3), new Color(76, 76, 76));

        DrawBitmapFontText(GetHostSetupTitle(), new Vector2(panel.X + 28f, panel.Y + 22f), Color.White, titleScale);
        DrawBitmapFontText(GetHostSetupSubtitle(), new Vector2(panel.X + 28f, panel.Y + 48f), new Color(200, 200, 200), subtitleScale);
        if (!string.IsNullOrWhiteSpace(_menuStatusMessage) && compactLayout)
        {
            DrawBitmapFontText(_menuStatusMessage, layout.StatusPosition, new Color(230, 220, 180), 0.82f);
        }

        if (IsServerLauncherMode)
        {
            var tabLayout = HostSetupMenuLayoutCalculator.CreateServerLauncherTabLayout(panel);
            DrawMenuButton(tabLayout.SettingsTabBounds, "Settings", _hostSetupTab == HostSetupTab.Settings);
            DrawMenuButton(tabLayout.ConsoleTabBounds, "Server Console", _hostSetupTab == HostSetupTab.ServerConsole);
        }

        if (IsServerLauncherMode && _hostSetupTab == HostSetupTab.ServerConsole)
        {
            DrawHostedServerConsoleTab(layout);
            return;
        }

        if (compactLayout)
        {
            var listSection = new Rectangle(
                layout.ListBounds.X - 10,
                layout.ListBounds.Y - 30,
                layout.ListBounds.Width + 20,
                layout.MoveDownBounds.Bottom - layout.ListBounds.Y + 52);
            var settingsSection = new Rectangle(
                layout.ServerNameBounds.X - 10,
                layout.ListBounds.Y - 30,
                layout.ServerNameBounds.Width + 20,
                layout.AutoBalanceBounds.Bottom - layout.ListBounds.Y + 42);
            _spriteBatch.Draw(_pixel, listSection, new Color(26, 28, 33, 170));
            _spriteBatch.Draw(_pixel, settingsSection, new Color(26, 28, 33, 170));
        }

        DrawBitmapFontText("Stock Map Rotation", new Vector2(layout.ListBounds.X, layout.ListBounds.Y - 24f), Color.White, compactLayout ? 0.88f : 0.95f);
        DrawBitmapFontText("ORDER", new Vector2(layout.ListBounds.X + 10f, layout.ListBounds.Y - 2f), new Color(210, 210, 210), headerScale);
        DrawBitmapFontText("MAP", new Vector2(layout.ListBounds.X + (compactLayout ? 54f : 78f), layout.ListBounds.Y - 2f), new Color(210, 210, 210), headerScale);
        DrawBitmapFontText("MODE", new Vector2(layout.ListBounds.Right - (compactLayout ? 98f : 112f), layout.ListBounds.Y - 2f), new Color(210, 210, 210), headerScale);
        DrawBitmapFontText("ON", new Vector2(layout.ListBounds.Right - (compactLayout ? 38f : 48f), layout.ListBounds.Y - 2f), new Color(210, 210, 210), headerScale);

        for (var index = 0; index < _hostMapEntries.Count; index += 1)
        {
            var entry = _hostMapEntries[index];
            var rowBounds = new Rectangle(
                layout.ListBounds.X - 6,
                layout.ListBounds.Y + layout.ListHeaderHeight + (index * layout.RowHeight),
                layout.ListBounds.Width + 12,
                layout.RowHeight - 2);
            if (index == _hostMapIndex)
            {
                _spriteBatch.Draw(_pixel, rowBounds, new Color(90, 64, 64));
            }
            else if (index == _hostSetupHoverIndex)
            {
                _spriteBatch.Draw(_pixel, rowBounds, new Color(60, 60, 70));
            }
            else
            {
                _spriteBatch.Draw(_pixel, rowBounds, new Color(44, 46, 52, 170));
            }

            var modeLabel = entry.Mode switch
            {
                GameModeKind.Arena => "Arena",
                GameModeKind.ControlPoint => "CP",
                GameModeKind.Generator => "Gen",
                _ => "CTF",
            };
            var orderLabel = entry.Order > 0 ? $"#{entry.Order}" : "--";
            var enabledLabel = entry.Order > 0 ? "ON" : "OFF";
            var enabledColor = entry.Order > 0 ? new Color(178, 228, 155) : new Color(140, 140, 140);

            var rowTextY = rowBounds.Y + (compactLayout ? 4f : 6f);
            DrawBitmapFontText(orderLabel, new Vector2(layout.ListBounds.X + 10f, rowTextY), Color.White, rowScale);
            DrawBitmapFontText(entry.DisplayName, new Vector2(layout.ListBounds.X + (compactLayout ? 54f : 78f), rowTextY), Color.White, rowScale);
            DrawBitmapFontText(modeLabel, new Vector2(layout.ListBounds.Right - (compactLayout ? 98f : 112f), rowTextY), new Color(210, 210, 210), rowScale);
            DrawBitmapFontText(enabledLabel, new Vector2(layout.ListBounds.Right - (compactLayout ? 40f : 50f), rowTextY), enabledColor, rowScale);
        }

        var selectedMap = GetSelectedHostMapEntry();
        var selectedIncluded = selectedMap is not null && selectedMap.Order > 0;
        DrawMenuButtonScaled(layout.ToggleBounds, selectedIncluded ? "Exclude" : "Include", selectedIncluded, buttonScale);
        DrawMenuButtonScaled(layout.MoveUpBounds, "Move Up", false, buttonScale);
        DrawMenuButtonScaled(layout.MoveDownBounds, "Move Down", false, buttonScale);

        var stockRotationLabel = GetHostStockRotationSummary(compactLayout ? 3 : 4);
        DrawBitmapFontText(stockRotationLabel, new Vector2(layout.ListBounds.X, layout.ToggleBounds.Bottom + (compactLayout ? 12f : 18f)), new Color(220, 220, 220), compactLayout ? 0.74f : 0.85f);
        if (!compactLayout && !string.IsNullOrWhiteSpace(_hostMapRotationFileBuffer))
        {
            DrawBitmapFontText("Custom rotation file overrides the stock list below.", new Vector2(layout.ListBounds.X, layout.ToggleBounds.Bottom + 40f), new Color(226, 204, 164), 0.82f);
        }

        var labelColor = new Color(210, 210, 210);
        DrawBitmapFontText("Server Name", new Vector2(layout.ServerNameBounds.X, layout.ServerNameBounds.Y - 16f), labelColor, fieldLabelScale);
        DrawMenuInputBoxScaled(layout.ServerNameBounds, _hostServerNameBuffer, _hostSetupEditField == HostSetupEditField.ServerName, inputScale);

        DrawBitmapFontText("Password", new Vector2(layout.PasswordBounds.X, layout.PasswordBounds.Y - 16f), labelColor, fieldLabelScale);
        var maskedPassword = string.IsNullOrEmpty(_hostPasswordBuffer) ? string.Empty : new string('*', _hostPasswordBuffer.Length);
        DrawMenuInputBoxScaled(layout.PasswordBounds, maskedPassword, _hostSetupEditField == HostSetupEditField.Password, inputScale);

        DrawBitmapFontText(compactLayout ? "Rotation File" : "Custom Rotation File", new Vector2(layout.RotationFileBounds.X, layout.RotationFileBounds.Y - 16f), labelColor, fieldLabelScale);
        DrawMenuInputBoxScaled(layout.RotationFileBounds, _hostMapRotationFileBuffer, _hostSetupEditField == HostSetupEditField.MapRotationFile, inputScale);

        DrawBitmapFontText("Port", new Vector2(layout.PortBounds.X, layout.PortBounds.Y - 16f), labelColor, fieldLabelScale);
        DrawMenuInputBoxScaled(layout.PortBounds, _hostPortBuffer, _hostSetupEditField == HostSetupEditField.Port, inputScale);

        DrawBitmapFontText("Slots", new Vector2(layout.SlotsBounds.X, layout.SlotsBounds.Y - 16f), labelColor, fieldLabelScale);
        DrawMenuInputBoxScaled(layout.SlotsBounds, _hostSlotsBuffer, _hostSetupEditField == HostSetupEditField.Slots, inputScale);

        DrawBitmapFontText(compactLayout ? "Time (mins)" : "Time Limit (mins)", new Vector2(layout.TimeLimitBounds.X, layout.TimeLimitBounds.Y - 16f), labelColor, fieldLabelScale);
        DrawMenuInputBoxScaled(layout.TimeLimitBounds, _hostTimeLimitBuffer, _hostSetupEditField == HostSetupEditField.TimeLimit, inputScale);

        DrawBitmapFontText("Cap Limit", new Vector2(layout.CapLimitBounds.X, layout.CapLimitBounds.Y - 16f), labelColor, fieldLabelScale);
        DrawMenuInputBoxScaled(layout.CapLimitBounds, _hostCapLimitBuffer, _hostSetupEditField == HostSetupEditField.CapLimit, inputScale);

        DrawBitmapFontText(compactLayout ? "Respawn (sec)" : "Respawn Time (secs)", new Vector2(layout.RespawnBounds.X, layout.RespawnBounds.Y - 16f), labelColor, fieldLabelScale);
        DrawMenuInputBoxScaled(layout.RespawnBounds, _hostRespawnSecondsBuffer, _hostSetupEditField == HostSetupEditField.RespawnSeconds, inputScale);

        DrawMenuButtonScaled(layout.LobbyBounds, _hostLobbyAnnounceEnabled ? "Lobby Announce: On" : "Lobby Announce: Off", _hostLobbyAnnounceEnabled, buttonScale);
        DrawMenuButtonScaled(layout.AutoBalanceBounds, _hostAutoBalanceEnabled ? "Auto-balance: On" : "Auto-balance: Off", _hostAutoBalanceEnabled, buttonScale);

        DrawMenuButtonScaled(layout.HostBounds, GetHostSetupPrimaryButtonLabel(), false, buttonScale);
        DrawMenuButtonScaled(layout.BackBounds, GetHostSetupSecondaryButtonLabel(), IsServerLauncherMode && IsHostedServerRunning, buttonScale);
        if (IsServerLauncherMode && !IsHostedServerRunning)
        {
            DrawMenuButtonScaled(layout.TerminalButtonBounds, "Run In Terminal", false, buttonScale);
        }

        if (IsServerLauncherMode && IsHostedServerRunning)
        {
            DrawBitmapFontText(
                "Use Stop Server to end the active dedicated server session.",
                new Vector2(panel.X + 28f, panel.Bottom - 62f),
                new Color(210, 210, 210),
                infoScale);
        }

        if (!compactLayout && !string.IsNullOrWhiteSpace(_menuStatusMessage))
        {
            DrawBitmapFontText(_menuStatusMessage, layout.StatusPosition, new Color(230, 220, 180), 1f);
        }
    }
}
