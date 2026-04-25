#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void DrawHostSetupMenu()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.78f);

        var layout = HostSetupMenuLayoutCalculator.CreateMenuLayout(ViewportWidth, ViewportHeight, _hostMapEntries.Count, IsServerLauncherMode);
        _hostSetupState.ClampMapScrollOffset(layout.VisibleRowCapacity);
        ClampHostSetupContentScrollOffset(layout);
        var panel = layout.Panel;
        var compactLayout = layout.CompactLayout;
        var footerBounds = new Rectangle(panel.X, layout.FooterTop, panel.Width, panel.Bottom - layout.FooterTop);
        const float headerScale = 1f;
        const float rowScale = 1f;
        const float fieldLabelScale = 1f;
        const float infoScale = 1f;
        const float inputScale = 1f;
        const float buttonScale = 1f;
        _spriteBatch.Draw(_pixel, panel, new Color(34, 35, 39, 235));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, panel.Width, 3), new Color(210, 210, 210));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Bottom - 3, panel.Width, 3), new Color(76, 76, 76));

        if (!string.IsNullOrWhiteSpace(_menuStatusMessage) && compactLayout)
        {
            DrawBitmapFontText(_menuStatusMessage, layout.StatusPosition, new Color(230, 220, 180), 1f);
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

        var listBounds = GetHostSetupScrolledContentBounds(layout.ListBounds);
        var listRowsBounds = GetHostSetupScrolledContentBounds(layout.ListRowsBounds);
        var toggleBounds = GetHostSetupScrolledContentBounds(layout.ToggleBounds);
        var moveUpBounds = GetHostSetupScrolledContentBounds(layout.MoveUpBounds);
        var moveDownBounds = GetHostSetupScrolledContentBounds(layout.MoveDownBounds);
        var serverNameBounds = GetHostSetupScrolledContentBounds(layout.ServerNameBounds);
        var passwordBounds = GetHostSetupScrolledContentBounds(layout.PasswordBounds);
        var rconPasswordBounds = GetHostSetupScrolledContentBounds(layout.RconPasswordBounds);
        var rotationFileBounds = GetHostSetupScrolledContentBounds(layout.RotationFileBounds);
        var portBounds = GetHostSetupScrolledContentBounds(layout.PortBounds);
        var slotsBounds = GetHostSetupScrolledContentBounds(layout.SlotsBounds);
        var timeLimitBounds = GetHostSetupScrolledContentBounds(layout.TimeLimitBounds);
        var capLimitBounds = GetHostSetupScrolledContentBounds(layout.CapLimitBounds);
        var respawnBounds = GetHostSetupScrolledContentBounds(layout.RespawnBounds);
        var lobbyBounds = GetHostSetupScrolledContentBounds(layout.LobbyBounds);
        var autoBalanceBounds = GetHostSetupScrolledContentBounds(layout.AutoBalanceBounds);
        var secondaryAbilitiesBounds = GetHostSetupScrolledContentBounds(layout.SecondaryAbilitiesBounds);

        if (compactLayout)
        {
            var listSection = new Rectangle(
                listBounds.X - 10,
                listBounds.Y - 30,
                listBounds.Width + 20,
                moveDownBounds.Bottom - listBounds.Y + 52);
            var settingsSection = new Rectangle(
                serverNameBounds.X - 10,
                listBounds.Y - 30,
                serverNameBounds.Width + 20,
                autoBalanceBounds.Bottom - listBounds.Y + 42);
            DrawHostSetupContentBlock(layout, listSection, new Color(26, 28, 33, 170));
            DrawHostSetupContentBlock(layout, settingsSection, new Color(26, 28, 33, 170));
        }

        DrawHostSetupContentText(layout, "Stock Map Rotation", new Vector2(listBounds.X, listBounds.Y - 24f), Color.White, 1f);
        if (_hostMapEntries.Count > layout.VisibleRowCapacity)
        {
            var visibleStart = _hostMapScrollOffset + 1;
            var visibleEnd = Math.Min(_hostMapEntries.Count, _hostMapScrollOffset + layout.VisibleRowCapacity);
            DrawHostSetupContentText(
                layout,
                $"{visibleStart}-{visibleEnd}/{_hostMapEntries.Count}",
                new Vector2(listBounds.Right - (compactLayout ? 70f : 86f), listBounds.Y - 24f),
                new Color(186, 186, 186),
                1f);
        }
        DrawHostSetupContentText(layout, "ORDER", new Vector2(listBounds.X + 10f, listBounds.Y - 2f), new Color(210, 210, 210), headerScale);
        DrawHostSetupContentText(layout, "MAP", new Vector2(listBounds.X + (compactLayout ? 54f : 78f), listBounds.Y - 2f), new Color(210, 210, 210), headerScale);
        DrawHostSetupContentText(layout, "MODE", new Vector2(listBounds.Right - (compactLayout ? 98f : 112f), listBounds.Y - 2f), new Color(210, 210, 210), headerScale);
        DrawHostSetupContentText(layout, "ON", new Vector2(listBounds.Right - (compactLayout ? 38f : 48f), listBounds.Y - 2f), new Color(210, 210, 210), headerScale);

        var firstVisibleRow = _hostMapScrollOffset;
        var visibleRowCount = layout.VisibleRowCapacity;
        var endIndex = Math.Min(_hostMapEntries.Count, firstVisibleRow + visibleRowCount);
        for (var index = firstVisibleRow; index < endIndex; index += 1)
        {
            var entry = _hostMapEntries[index];
            var visibleRow = index - firstVisibleRow;
            var rowBounds = new Rectangle(
                listBounds.X - 6,
                listBounds.Y + layout.ListHeaderHeight + (visibleRow * layout.RowHeight),
                listBounds.Width + 12,
                layout.RowHeight - 2);
            if (!IsHostSetupContentBoundsVisible(layout, rowBounds))
            {
                continue;
            }

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
                GameModeKind.KingOfTheHill => "KOTH",
                GameModeKind.DoubleKingOfTheHill => "DKOTH",
                GameModeKind.TeamDeathmatch => "TDM",
                _ => "CTF",
            };
            var orderLabel = entry.Order > 0 ? $"#{entry.Order}" : "--";
            var enabledLabel = entry.Order > 0 ? "ON" : "OFF";
            var enabledColor = entry.Order > 0 ? new Color(178, 228, 155) : new Color(140, 140, 140);

            var rowTextY = rowBounds.Y + (compactLayout ? 4f : 6f);
            DrawHostSetupContentText(layout, orderLabel, new Vector2(listBounds.X + 10f, rowTextY), Color.White, rowScale);
            DrawHostSetupContentText(layout, entry.DisplayName, new Vector2(listBounds.X + (compactLayout ? 54f : 78f), rowTextY), Color.White, rowScale);
            DrawHostSetupContentText(layout, modeLabel, new Vector2(listBounds.Right - (compactLayout ? 98f : 112f), rowTextY), new Color(210, 210, 210), rowScale);
            DrawHostSetupContentText(layout, enabledLabel, new Vector2(listBounds.Right - (compactLayout ? 40f : 50f), rowTextY), enabledColor, rowScale);
        }

        if (_hostMapEntries.Count > layout.VisibleRowCapacity && IsHostSetupContentBoundsVisible(layout, listRowsBounds))
        {
            var trackBounds = new Rectangle(listBounds.Right + 8, listRowsBounds.Y, 8, listRowsBounds.Height);
            _spriteBatch.Draw(_pixel, trackBounds, new Color(30, 32, 38));

            var maxOffset = Math.Max(1, _hostMapEntries.Count - layout.VisibleRowCapacity);
            var thumbHeight = Math.Max(24, (int)MathF.Round(trackBounds.Height * (layout.VisibleRowCapacity / (float)_hostMapEntries.Count)));
            var thumbTravel = Math.Max(0, trackBounds.Height - thumbHeight);
            var thumbY = trackBounds.Y + (int)MathF.Round((_hostMapScrollOffset / (float)maxOffset) * thumbTravel);
            var thumbBounds = new Rectangle(trackBounds.X, thumbY, trackBounds.Width, thumbHeight);
            _spriteBatch.Draw(_pixel, thumbBounds, new Color(125, 125, 125));
        }

        var selectedMap = GetSelectedHostMapEntry();
        var selectedIncluded = selectedMap is not null && selectedMap.Order > 0;
        DrawHostSetupContentButton(layout, toggleBounds, selectedIncluded ? "Exclude" : "Include", selectedIncluded, buttonScale);
        DrawHostSetupContentButton(layout, moveUpBounds, "Up", false, buttonScale);
        DrawHostSetupContentButton(layout, moveDownBounds, "Down", false, buttonScale);

        var labelColor = new Color(210, 210, 210);
        DrawHostSetupContentText(layout, "Server Name", new Vector2(serverNameBounds.X, serverNameBounds.Y - 16f), labelColor, fieldLabelScale);
        DrawHostSetupContentInput(layout, serverNameBounds, _hostServerNameBuffer, _hostSetupEditField == HostSetupEditField.ServerName, inputScale, _hostServerNameCursorIndex, _hostServerNameSelectionStart);

        DrawHostSetupContentText(layout, "Password", new Vector2(passwordBounds.X, passwordBounds.Y - 16f), labelColor, fieldLabelScale);
        var maskedPassword = string.IsNullOrEmpty(_hostPasswordBuffer) ? string.Empty : new string('*', _hostPasswordBuffer.Length);
        DrawHostSetupContentInput(layout, passwordBounds, maskedPassword, _hostSetupEditField == HostSetupEditField.Password, inputScale, _hostPasswordCursorIndex, _hostPasswordSelectionStart);

        DrawHostSetupContentText(layout, "RCON Password", new Vector2(rconPasswordBounds.X, rconPasswordBounds.Y - 16f), labelColor, fieldLabelScale);
        var maskedRconPassword = string.IsNullOrEmpty(_hostRconPasswordBuffer) ? string.Empty : new string('*', _hostRconPasswordBuffer.Length);
        DrawHostSetupContentInput(layout, rconPasswordBounds, maskedRconPassword, _hostSetupEditField == HostSetupEditField.RconPassword, inputScale, _hostRconPasswordCursorIndex, _hostRconPasswordSelectionStart);

        DrawHostSetupContentText(layout, compactLayout ? "Rotation File" : "Custom Rotation File", new Vector2(rotationFileBounds.X, rotationFileBounds.Y - 16f), labelColor, fieldLabelScale);
        DrawHostSetupContentInput(layout, rotationFileBounds, _hostMapRotationFileBuffer, _hostSetupEditField == HostSetupEditField.MapRotationFile, inputScale, _hostMapRotationFileCursorIndex, _hostMapRotationFileSelectionStart);

        DrawHostSetupContentText(layout, "Port", new Vector2(portBounds.X, portBounds.Y - 16f), labelColor, fieldLabelScale);
        DrawHostSetupContentInput(layout, portBounds, _hostPortBuffer, _hostSetupEditField == HostSetupEditField.Port, inputScale, _hostPortCursorIndex, _hostPortSelectionStart);

        DrawHostSetupContentText(layout, "Slots", new Vector2(slotsBounds.X, slotsBounds.Y - 16f), labelColor, fieldLabelScale);
        DrawHostSetupContentInput(layout, slotsBounds, _hostSlotsBuffer, _hostSetupEditField == HostSetupEditField.Slots, inputScale, _hostSlotsCursorIndex, _hostSlotsSelectionStart);

        DrawHostSetupContentText(layout, compactLayout ? "Time (mins)" : "Time Limit (mins)", new Vector2(timeLimitBounds.X, timeLimitBounds.Y - 16f), labelColor, fieldLabelScale);
        DrawHostSetupContentInput(layout, timeLimitBounds, _hostTimeLimitBuffer, _hostSetupEditField == HostSetupEditField.TimeLimit, inputScale, _hostTimeLimitCursorIndex, _hostTimeLimitSelectionStart);

        DrawHostSetupContentText(layout, "Cap Limit", new Vector2(capLimitBounds.X, capLimitBounds.Y - 16f), labelColor, fieldLabelScale);
        DrawHostSetupContentInput(layout, capLimitBounds, _hostCapLimitBuffer, _hostSetupEditField == HostSetupEditField.CapLimit, inputScale, _hostCapLimitCursorIndex, _hostCapLimitSelectionStart);

        DrawHostSetupContentText(layout, compactLayout ? "Respawn (sec)" : "Respawn Time (secs)", new Vector2(respawnBounds.X, respawnBounds.Y - 16f), labelColor, fieldLabelScale);
        DrawHostSetupContentInput(layout, respawnBounds, _hostRespawnSecondsBuffer, _hostSetupEditField == HostSetupEditField.RespawnSeconds, inputScale, _hostRespawnSecondsCursorIndex, _hostRespawnSecondsSelectionStart);

        DrawHostSetupContentButton(layout, lobbyBounds, _hostLobbyAnnounceEnabled ? "Lobby Announce: On" : "Lobby Announce: Off", _hostLobbyAnnounceEnabled, buttonScale);
        DrawHostSetupContentButton(layout, autoBalanceBounds, _hostAutoBalanceEnabled ? "Auto-balance: On" : "Auto-balance: Off", _hostAutoBalanceEnabled, buttonScale);
        DrawHostSetupContentButton(layout, secondaryAbilitiesBounds, _hostSecondaryAbilitiesEnabled ? "Secondary Abilities: On" : "Secondary Abilities: Off", _hostSecondaryAbilitiesEnabled, buttonScale);

        var contentHeight = GetHostSetupContentHeight(layout);
        if (contentHeight > layout.ContentViewportBounds.Height)
        {
            var trackBounds = layout.ContentScrollbarTrackBounds;
            _spriteBatch.Draw(_pixel, trackBounds, new Color(30, 32, 38));

            var maxOffset = Math.Max(1, contentHeight - layout.ContentViewportBounds.Height);
            var thumbHeight = Math.Max(24, (int)MathF.Round(trackBounds.Height * (layout.ContentViewportBounds.Height / (float)contentHeight)));
            var thumbTravel = Math.Max(0, trackBounds.Height - thumbHeight);
            var thumbY = trackBounds.Y + (int)MathF.Round((_hostSetupContentScrollOffset / (float)maxOffset) * thumbTravel);
            var thumbBounds = new Rectangle(trackBounds.X, thumbY, trackBounds.Width, thumbHeight);
            _spriteBatch.Draw(_pixel, thumbBounds, new Color(125, 125, 125));
        }

        _spriteBatch.Draw(_pixel, footerBounds, new Color(28, 30, 35, 235));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, layout.FooterTop, panel.Width, 2), new Color(76, 76, 76));

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

    private int GetHostSetupContentHeight(HostSetupMenuLayout layout)
    {
        var contentBottom = Math.Max(
            Math.Max(layout.MoveDownBounds.Bottom, layout.ListBounds.Bottom),
            Math.Max(layout.SecondaryAbilitiesBounds.Bottom, layout.RespawnBounds.Bottom));
        return Math.Max(layout.ContentViewportBounds.Height, (contentBottom - layout.ContentTop) + 12);
    }

    private void ClampHostSetupContentScrollOffset(HostSetupMenuLayout layout)
    {
        _hostSetupContentScrollOffset = Math.Clamp(
            _hostSetupContentScrollOffset,
            0,
            Math.Max(0, GetHostSetupContentHeight(layout) - layout.ContentViewportBounds.Height));
    }

    private Rectangle GetHostSetupScrolledContentBounds(Rectangle bounds)
    {
        return new Rectangle(bounds.X, bounds.Y - _hostSetupContentScrollOffset, bounds.Width, bounds.Height);
    }

    private bool IsHostSetupContentBoundsVisible(HostSetupMenuLayout layout, Rectangle bounds)
    {
        return bounds.Bottom > layout.ContentViewportBounds.Y && bounds.Y < layout.ContentViewportBounds.Bottom;
    }

    private void DrawHostSetupContentBlock(HostSetupMenuLayout layout, Rectangle bounds, Color color)
    {
        if (!IsHostSetupContentBoundsVisible(layout, bounds))
        {
            return;
        }

        var clipped = Rectangle.Intersect(bounds, layout.ContentViewportBounds);
        if (clipped.Width > 0 && clipped.Height > 0)
        {
            _spriteBatch.Draw(_pixel, clipped, color);
        }
    }

    private void DrawHostSetupContentText(HostSetupMenuLayout layout, string text, Vector2 position, Color color, float scale)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var bounds = new Rectangle(
            (int)MathF.Floor(position.X),
            (int)MathF.Floor(position.Y),
            (int)MathF.Ceiling(MeasureBitmapFontWidth(text, scale)),
            (int)MathF.Ceiling(MeasureBitmapFontHeight(scale)));
        if (!IsHostSetupContentBoundsVisible(layout, bounds))
        {
            return;
        }

        DrawBitmapFontText(text, position, color, scale);
    }

    private void DrawHostSetupContentInput(HostSetupMenuLayout layout, Rectangle bounds, string text, bool active, float scale, int cursorIndex = -1, int selectionStart = -1)
    {
        if (!IsHostSetupContentBoundsVisible(layout, bounds))
        {
            return;
        }

        DrawMenuInputBoxScaled(bounds, text, active, scale, cursorIndex, selectionStart);
    }

    private void DrawHostSetupContentButton(HostSetupMenuLayout layout, Rectangle bounds, string label, bool highlighted, float scale)
    {
        if (!IsHostSetupContentBoundsVisible(layout, bounds))
        {
            return;
        }

        DrawMenuButtonScaled(bounds, label, highlighted, scale);
    }
}
