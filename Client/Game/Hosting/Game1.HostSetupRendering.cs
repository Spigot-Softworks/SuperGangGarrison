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

        if (_menuBackgroundMode != MenuBackgroundMode.Static)
        {
            const int bottomBarHeight = 76;
            var barY = viewportHeight - bottomBarHeight;
            var bottomBarBounds = new Rectangle(0, barY, viewportWidth, bottomBarHeight);
            _spriteBatch.Draw(_pixel, bottomBarBounds, new Color(0x57, 0x4f, 0x47));
            _menuBottomBarRunners.Draw(bottomBarBounds);
        }

        if (_hostSetupScreen == HostSetupScreen.Options)
        {
            DrawHostSetupOptionsMenuOverlay();
            return;
        }

        var layout = HostSetupMenuLayoutCalculator.CreateMenuLayout(
            ViewportWidth,
            ViewportHeight,
            _hostMapEntries.Count,
            IsServerLauncherMode,
            _hostSetupScreen);
        _hostSetupState.ClampMapScrollOffset(layout.VisibleRowCapacity);
        ClampHostSetupContentScrollOffset(layout);
        var panel = layout.Panel;
        var compactLayout = layout.CompactLayout;
        const float headerScale = 1f;
        const float rowScale = 1f;
        const float fieldLabelScale = 1f;
        const float infoScale = 1f;
        const float inputScale = 1f;
        const float buttonScale = 1f;

        DrawRoundedRectangleOutline(panel, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 2, radius: 8);

        var headerText = _hostSetupScreen switch
        {
            HostSetupScreen.Options => "HOST OPTIONS",
            HostSetupScreen.Maps => "MAP ROTATION",
            _ => "HOST A SERVER",
        };
        var hostHeaderX = panel.Right - 24f - MeasureBitmapFontWidth(headerText, headerScale);
        DrawBitmapFontText(headerText, new Vector2(hostHeaderX, panel.Y + (compactLayout ? 16f : 20f)), Color.White, headerScale);

        DrawRoundedRectangleOutline(layout.ContentViewportBounds, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 1, radius: 6);

        if (!string.IsNullOrWhiteSpace(_menuStatusMessage) && compactLayout)
        {
            DrawHostSetupMenuStatusMessage(layout);
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

        switch (_hostSetupScreen)
        {
            case HostSetupScreen.Maps:
            {
                var mapsLayout = HostSetupMapsMenuLayoutCalculator.Create(ViewportWidth, ViewportHeight, IsServerLauncherMode);
                DrawHostSetupMapsScreen(mapsLayout, buttonScale);
                break;
            }
            default:
                DrawHostSetupMainScreen(layout, compactLayout, fieldLabelScale, inputScale, buttonScale);
                break;
        }

        DrawHostSetupFooter(layout, compactLayout, buttonScale, infoScale);

        if (!compactLayout && !string.IsNullOrWhiteSpace(_menuStatusMessage))
        {
            DrawHostSetupMenuStatusMessage(layout);
        }
    }

    private void DrawHostSetupMainScreen(
        HostSetupMenuLayout layout,
        bool compactLayout,
        float fieldLabelScale,
        float inputScale,
        float buttonScale)
    {
        var serverNameBounds = GetHostSetupScrolledContentBounds(layout.ServerNameBounds);
        var passwordBounds = GetHostSetupScrolledContentBounds(layout.PasswordBounds);
        var rconPasswordBounds = GetHostSetupScrolledContentBounds(layout.RconPasswordBounds);
        var rotationFileBounds = GetHostSetupScrolledContentBounds(layout.RotationFileBounds);
        var portBounds = GetHostSetupScrolledContentBounds(layout.PortBounds);
        var slotsBounds = GetHostSetupScrolledContentBounds(layout.SlotsBounds);
        var lobbyBounds = GetHostSetupScrolledContentBounds(layout.LobbyBounds);
        var optionsButtonBounds = GetHostSetupScrolledContentBounds(layout.OptionsButtonBounds);
        var mapsButtonBounds = GetHostSetupScrolledContentBounds(layout.MapsButtonBounds);

        var labelColor = new Color(210, 210, 210);
        DrawHostSetupContentText(layout, "Server Name", new Vector2(serverNameBounds.X, serverNameBounds.Y - 16f), labelColor, fieldLabelScale);
        DrawHostSetupContentInput(layout, serverNameBounds, _hostServerNameBuffer, _hostSetupEditField == HostSetupEditField.ServerName, inputScale, _hostServerNameCursorIndex, _hostServerNameSelectionStart);

        DrawHostSetupContentText(layout, "Password", new Vector2(passwordBounds.X, passwordBounds.Y - 16f), labelColor, fieldLabelScale);
        var maskedPassword = string.IsNullOrEmpty(_hostPasswordBuffer) ? string.Empty : new string('*', _hostPasswordBuffer.Length);
        DrawHostSetupContentInput(layout, passwordBounds, maskedPassword, _hostSetupEditField == HostSetupEditField.Password, inputScale, _hostPasswordCursorIndex, _hostPasswordSelectionStart);

        DrawHostSetupContentText(layout, "RCON Password", new Vector2(rconPasswordBounds.X, rconPasswordBounds.Y - 16f), labelColor, fieldLabelScale);
        var maskedRconPassword = string.IsNullOrEmpty(_hostRconPasswordBuffer) ? string.Empty : new string('*', _hostRconPasswordBuffer.Length);
        DrawHostSetupContentInput(layout, rconPasswordBounds, maskedRconPassword, _hostSetupEditField == HostSetupEditField.RconPassword, inputScale, _hostRconPasswordCursorIndex, _hostRconPasswordSelectionStart);

        var usePlaylistFileToggleBounds = HostSetupMainScreenLayout.GetUsePlaylistFileToggleBounds(rotationFileBounds, compactLayout);
        DrawHostSetupUsePlaylistFileCheckbox(
            layout,
            usePlaylistFileToggleBounds,
            _hostUsePlaylistFile,
            compactLayout,
            inputScale);
        var playlistFileInputEnabled = _hostUsePlaylistFile;
        DrawHostSetupContentText(layout, "Playlist file", new Vector2(rotationFileBounds.X, rotationFileBounds.Y - 16f), labelColor, fieldLabelScale);
        DrawHostSetupContentInput(
            layout,
            rotationFileBounds,
            _hostMapRotationFileBuffer,
            playlistFileInputEnabled && _hostSetupEditField == HostSetupEditField.MapRotationFile,
            inputScale,
            _hostMapRotationFileCursorIndex,
            _hostMapRotationFileSelectionStart,
            playlistFileInputEnabled);

        DrawHostSetupContentText(layout, "Port", new Vector2(portBounds.X, portBounds.Y - 16f), labelColor, fieldLabelScale);
        DrawHostSetupContentInput(layout, portBounds, _hostPortBuffer, _hostSetupEditField == HostSetupEditField.Port, inputScale, _hostPortCursorIndex, _hostPortSelectionStart);

        DrawHostSetupContentText(layout, "Slots", new Vector2(slotsBounds.X, slotsBounds.Y - 16f), labelColor, fieldLabelScale);
        DrawHostSetupContentInput(layout, slotsBounds, _hostSlotsBuffer, _hostSetupEditField == HostSetupEditField.Slots, inputScale, _hostSlotsCursorIndex, _hostSlotsSelectionStart);

        DrawHostSetupContentButton(layout, lobbyBounds, _hostLobbyAnnounceEnabled ? "Lobby Announce: On" : "Lobby Announce: Off", _hostLobbyAnnounceEnabled, buttonScale);

        var navButtonScale = compactLayout ? 1.08f : 1.15f;
        DrawHostSetupContentButton(layout, optionsButtonBounds, "Options", false, navButtonScale, centerText: true);
        DrawHostSetupContentButton(
            layout,
            mapsButtonBounds,
            "Maps",
            false,
            navButtonScale,
            centerText: true,
            enabled: !_hostUsePlaylistFile);

        DrawHostSetupContentScrollbar(layout);
    }

    private void DrawHostSetupFooter(HostSetupMenuLayout layout, bool compactLayout, float buttonScale, float infoScale)
    {
        var panel = layout.Panel;
        if (_hostSetupScreen == HostSetupScreen.Main)
        {
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
        }
    }

    private void DrawHostSetupContentScrollbar(HostSetupMenuLayout layout)
    {
        var contentHeight = GetHostSetupContentHeight(layout);
        if (contentHeight <= layout.ContentViewportBounds.Height)
        {
            return;
        }

        var trackBounds = layout.ContentScrollbarTrackBounds;
        _spriteBatch.Draw(_pixel, trackBounds, new Color(22, 24, 28));

        var maxOffset = Math.Max(1, contentHeight - layout.ContentViewportBounds.Height);
        var thumbHeight = Math.Max(24, (int)MathF.Round(trackBounds.Height * (layout.ContentViewportBounds.Height / (float)contentHeight)));
        var thumbTravel = Math.Max(0, trackBounds.Height - thumbHeight);
        var thumbY = trackBounds.Y + (int)MathF.Round((_hostSetupContentScrollOffset / (float)maxOffset) * thumbTravel);
        var thumbBounds = new Rectangle(trackBounds.X, thumbY, trackBounds.Width, thumbHeight);
        _spriteBatch.Draw(_pixel, thumbBounds, new Color(105, 105, 105));
    }

    private void ClampHostOptionsScrollOffset(int optionCount, int visibleRowCount)
    {
        _hostOptionsScrollOffset = Math.Clamp(
            _hostOptionsScrollOffset,
            0,
            Math.Max(0, optionCount - visibleRowCount));
    }

    private static int GetHostSetupContentHeight(HostSetupMenuLayout layout)
    {
        var contentBottom = layout.Screen switch
        {
            HostSetupScreen.Options => Math.Max(
                layout.SecondaryAbilitiesBounds.Bottom,
                layout.OptionsListBounds.Bottom),
            HostSetupScreen.Maps => layout.ContentViewportBounds.Bottom,
            _ => Math.Max(
                Math.Max(layout.MapsButtonBounds.Bottom, layout.OptionsButtonBounds.Bottom),
                layout.LobbyBounds.Bottom),
        };

        return Math.Max(layout.ContentViewportBounds.Height, (contentBottom - layout.ContentTop) + 12);
    }

    private void ClampHostSetupContentScrollOffset(HostSetupMenuLayout layout)
    {
        if (layout.Screen == HostSetupScreen.Maps)
        {
            _hostSetupContentScrollOffset = 0;
            return;
        }

        _hostSetupContentScrollOffset = Math.Clamp(
            _hostSetupContentScrollOffset,
            0,
            Math.Max(0, GetHostSetupContentHeight(layout) - layout.ContentViewportBounds.Height));
    }

    private Rectangle GetHostSetupScrolledContentBounds(Rectangle bounds)
    {
        return new Rectangle(bounds.X, bounds.Y - _hostSetupContentScrollOffset, bounds.Width, bounds.Height);
    }

    private static bool IsHostSetupContentBoundsVisible(HostSetupMenuLayout layout, Rectangle bounds)
    {
        return bounds.Bottom > layout.ContentViewportBounds.Y && bounds.Y < layout.ContentViewportBounds.Bottom;
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

    private void DrawHostSetupContentInput(
        HostSetupMenuLayout layout,
        Rectangle bounds,
        string text,
        bool active,
        float scale,
        int cursorIndex = -1,
        int selectionStart = -1,
        bool enabled = true)
    {
        if (!IsHostSetupContentBoundsVisible(layout, bounds))
        {
            return;
        }

        DrawMenuInputBoxScaled(bounds, text, active, scale, cursorIndex, selectionStart, enabled);
    }

    private void DrawHostSetupUsePlaylistFileCheckbox(
        HostSetupMenuLayout layout,
        Rectangle toggleBounds,
        bool enabled,
        bool compactLayout,
        float textScale)
    {
        if (!IsHostSetupContentBoundsVisible(layout, toggleBounds))
        {
            return;
        }

        var checkboxSize = compactLayout ? 16 : 18;
        var checkboxY = toggleBounds.Y + (int)((toggleBounds.Height - checkboxSize) * 0.5f);
        var checkboxBounds = new Rectangle(toggleBounds.X, checkboxY, checkboxSize, checkboxSize);
        var label = compactLayout ? "Use file" : "Use playlist file";
        DrawHostSetupFilterCheckboxRow(checkboxBounds, toggleBounds, label, enabled, textScale);
    }

    private void DrawHostSetupContentButton(
        HostSetupMenuLayout layout,
        Rectangle bounds,
        string label,
        bool highlighted,
        float scale,
        bool centerText = false,
        bool enabled = true)
    {
        if (!IsHostSetupContentBoundsVisible(layout, bounds))
        {
            return;
        }

        if (centerText)
        {
            DrawMenuButtonCentered(bounds, label, highlighted, scale, enabled);
        }
        else
        {
            DrawMenuButtonScaled(bounds, label, highlighted, scale, enabled);
        }
    }
}
