#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using OpenGarrison.Client.Plugins;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void UpdatePluginOptionsMenu(KeyboardState keyboard, MouseState mouse)
    {
        var rows = BuildPluginOptionsMenuRows();
        var visibleRowCount = Math.Min(rows.Count, GetPluginOptionsVisibleRowCapacity());
        ClampPluginOptionsScrollOffset(rows.Count, visibleRowCount);
        GetOptionsMenuLayout(visibleRowCount, out var xbegin, out var ybegin, out var spacing, out var width, out _);
        var wheelDelta = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        var menuTop = ybegin - spacing;
        var menuHeight = Math.Max(spacing, visibleRowCount * spacing);
        var menuBounds = new Rectangle(
            (int)MathF.Floor(xbegin),
            (int)MathF.Floor(menuTop),
            (int)MathF.Ceiling(width),
            (int)MathF.Ceiling(menuHeight));

        if (_pendingPluginOptionsKeyItem is not null)
        {
            if (IsKeyPressed(keyboard, Keys.Escape))
            {
                _pendingPluginOptionsKeyItem = null;
                return;
            }

            foreach (var key in keyboard.GetPressedKeys())
            {
                if (_previousKeyboard.IsKeyDown(key))
                {
                    continue;
                }

                try
                {
                    _pendingPluginOptionsKeyItem.SetKey(key);
                }
                catch (Exception ex)
                {
                    AddConsoleLine($"plugin option apply failed for \"{_pendingPluginOptionsKeyItem.Label}\": {ex.Message}");
                }

                _pendingPluginOptionsKeyItem = null;
                return;
            }

            return;
        }

        if (IsKeyPressed(keyboard, Keys.Escape))
        {
            if (_selectedPluginOptionsPluginId is not null)
            {
                _selectedPluginOptionsPluginId = null;
                _pendingPluginOptionsKeyItem = null;
                _pluginOptionsHoverIndex = -1;
                _pluginOptionsScrollOffset = 0;
                return;
            }

            ClosePluginOptionsMenu();
            return;
        }

        if (wheelDelta != 0 && menuBounds.Contains(mouse.Position))
        {
            var stepCount = Math.Max(1, Math.Abs(wheelDelta) / 120);
            _pluginOptionsScrollOffset = Math.Clamp(
                _pluginOptionsScrollOffset + (wheelDelta > 0 ? -stepCount : stepCount),
                0,
                Math.Max(0, rows.Count - visibleRowCount));
        }

        if (mouse.X > xbegin && mouse.X < xbegin + width)
        {
            var visibleHoverIndex = (int)MathF.Round((mouse.Y - ybegin) / spacing);
            var hoverIndex = _pluginOptionsScrollOffset + visibleHoverIndex;
            var visibleStart = _pluginOptionsScrollOffset;
            var visibleEndExclusive = visibleStart + visibleRowCount;
            _pluginOptionsHoverIndex = visibleHoverIndex >= 0
                && hoverIndex >= visibleStart
                && hoverIndex < visibleEndExclusive
                && hoverIndex < rows.Count
                && rows[hoverIndex].Selectable
                    ? hoverIndex
                    : -1;
        }
        else
        {
            _pluginOptionsHoverIndex = -1;
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed || _pluginOptionsHoverIndex < 0)
        {
            return;
        }

        rows[_pluginOptionsHoverIndex].Activate?.Invoke();
    }

    private void DrawPluginOptionsMenu()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.82f);
        var compactLayout = ViewportHeight < 540;
        var textScale = compactLayout ? 0.92f : 1f;
        var rows = BuildPluginOptionsMenuRows();
        var visibleRowCount = Math.Min(rows.Count, GetPluginOptionsVisibleRowCapacity());
        ClampPluginOptionsScrollOffset(rows.Count, visibleRowCount);
        GetOptionsMenuLayout(visibleRowCount, out var xbegin, out var ybegin, out var spacing, out var width, out var valueX);
        DrawMenuPanelBackdrop(
            new Rectangle(
                (int)xbegin - 12,
                (int)(ybegin - spacing),
                (int)(width + 132f),
                Math.Max((int)spacing, (int)(visibleRowCount * spacing + spacing * 0.5f))),
            0.82f);
        DrawMenuPlaqueRows(new Vector2(xbegin, ybegin), visibleRowCount, spacing, width + 116f, 0.7f);

        var position = new Vector2(xbegin, ybegin);
        var endIndex = Math.Min(rows.Count, _pluginOptionsScrollOffset + visibleRowCount);
        for (var index = _pluginOptionsScrollOffset; index < endIndex; index += 1)
        {
            var row = rows[index];
            var color = row.IsHeader
                ? new Color(240, 200, 120)
                : index == _pluginOptionsHoverIndex ? Color.Red : Color.White;
            DrawBitmapFontText(row.Label, position, color, textScale);
            if (!string.IsNullOrWhiteSpace(row.Value))
            {
                DrawBitmapFontText(row.Value, new Vector2(valueX, position.Y), color, textScale);
            }

            position.Y += spacing;
        }

        if (rows.Count > visibleRowCount)
        {
            var visibleStart = _pluginOptionsScrollOffset + 1;
            var visibleEnd = Math.Min(rows.Count, _pluginOptionsScrollOffset + visibleRowCount);
            DrawBitmapFontText(
                $"{visibleStart}-{visibleEnd}/{rows.Count}",
                new Vector2(valueX + (compactLayout ? 32f : 56f), ybegin - spacing),
                new Color(186, 186, 186),
                compactLayout ? 0.72f : 0.8f);
        }

        if (_pendingPluginOptionsKeyItem is not null)
        {
            DrawBitmapFontText(
                $"Press a key for {_pendingPluginOptionsKeyItem.Label} (Esc to cancel)",
                new Vector2(xbegin, Math.Max(18f, ybegin - spacing * 1.5f)),
                Color.Orange,
                compactLayout ? 0.82f : 0.9f);
        }
    }

    private bool HasClientPluginOptions()
    {
        return GetClientPluginOptionsEntries().Count > 0;
    }

    private List<PluginOptionsMenuRow> BuildPluginOptionsMenuRows()
    {
        var rows = new List<PluginOptionsMenuRow>();
        var pluginEntries = GetClientPluginOptionsEntries();
        if (_selectedPluginOptionsPluginId is null)
        {
            rows.Add(new PluginOptionsMenuRow("Plugin Options", string.Empty, Selectable: false, IsHeader: true, Activate: null));
            for (var pluginIndex = 0; pluginIndex < pluginEntries.Count; pluginIndex += 1)
            {
                var entry = pluginEntries[pluginIndex];
                rows.Add(new PluginOptionsMenuRow(
                    entry.DisplayName,
                    GetClientPluginStatusLabel(entry),
                    Selectable: true,
                    IsHeader: false,
                    Activate: () => OpenPluginOptionsDetail(entry.PluginId)));
            }

            if (pluginEntries.Count == 0)
            {
                rows.Add(new PluginOptionsMenuRow("No plugin options available.", string.Empty, Selectable: false, IsHeader: false, Activate: null));
            }

            rows.Add(new PluginOptionsMenuRow("Back", string.Empty, Selectable: true, IsHeader: false, Activate: ClosePluginOptionsMenu));
            return rows;
        }

        var selectedEntry = GetSelectedPluginOptionsEntry();
        if (selectedEntry is null)
        {
            _selectedPluginOptionsPluginId = null;
            return BuildPluginOptionsMenuRows();
        }

        rows.Add(new PluginOptionsMenuRow(selectedEntry.DisplayName, string.Empty, Selectable: false, IsHeader: true, Activate: null));
        rows.Add(new PluginOptionsMenuRow("Version", FormatClientPluginVersion(selectedEntry.Version), Selectable: false, IsHeader: false, Activate: null));
        rows.Add(new PluginOptionsMenuRow(
            "Enabled",
            selectedEntry.IsEnabled ? "Enabled" : "Disabled",
            Selectable: true,
            IsHeader: false,
            Activate: () => _clientPluginHost?.SetPluginEnabled(selectedEntry.PluginId, !selectedEntry.IsEnabled)));
        if (selectedEntry.IsEnabled && !selectedEntry.IsLoaded)
        {
            rows.Add(new PluginOptionsMenuRow("Status", "Load failed", Selectable: false, IsHeader: false, Activate: null));
            rows.Add(new PluginOptionsMenuRow("See console for the plugin error.", string.Empty, Selectable: false, IsHeader: false, Activate: null));
        }
        else if (!selectedEntry.IsEnabled)
        {
            rows.Add(new PluginOptionsMenuRow("Enable this plugin to access its options.", string.Empty, Selectable: false, IsHeader: false, Activate: null));
        }

        var sections = selectedEntry.Sections;
        for (var sectionIndex = 0; selectedEntry.IsEnabled && selectedEntry.IsLoaded && sectionIndex < sections.Count; sectionIndex += 1)
        {
            var section = sections[sectionIndex];
            if (section.Items.Count == 0)
            {
                continue;
            }

            var shouldShowSectionHeader = sections.Count > 1
                || !string.Equals(section.Title, selectedEntry.DisplayName, StringComparison.Ordinal);
            if (shouldShowSectionHeader)
            {
                rows.Add(new PluginOptionsMenuRow(section.Title, string.Empty, Selectable: false, IsHeader: true, Activate: null));
            }

            for (var itemIndex = 0; itemIndex < section.Items.Count; itemIndex += 1)
            {
                var item = section.Items[itemIndex];
                rows.Add(new PluginOptionsMenuRow(
                    item.Label,
                    GetPluginOptionValueLabel(item),
                    Selectable: true,
                    IsHeader: false,
                    Activate: () => ActivatePluginOption(item)));
            }
        }

        if (rows.Count == 3 && selectedEntry.IsEnabled && selectedEntry.IsLoaded)
        {
            rows.Add(new PluginOptionsMenuRow("No options available.", string.Empty, Selectable: false, IsHeader: false, Activate: null));
        }

        rows.Add(new PluginOptionsMenuRow("Back", string.Empty, Selectable: true, IsHeader: false, Activate: CloseSelectedPluginOptionsDetail));
        return rows;
    }

    private IReadOnlyList<ClientPluginOptionsEntry> GetClientPluginOptionsEntries()
    {
        return _clientPluginHost?.GetPluginOptionsEntries() ?? [];
    }

    private ClientPluginOptionsEntry? GetSelectedPluginOptionsEntry()
    {
        var selectedPluginId = _selectedPluginOptionsPluginId;
        if (string.IsNullOrWhiteSpace(selectedPluginId))
        {
            return null;
        }

        var entries = GetClientPluginOptionsEntries();
        for (var index = 0; index < entries.Count; index += 1)
        {
            if (string.Equals(entries[index].PluginId, selectedPluginId, StringComparison.Ordinal))
            {
                return entries[index];
            }
        }

        return null;
    }

    private void OpenPluginOptionsDetail(string pluginId)
    {
        _selectedPluginOptionsPluginId = pluginId;
        _pendingPluginOptionsKeyItem = null;
        _pluginOptionsHoverIndex = -1;
        _pluginOptionsScrollOffset = 0;
    }

    private void CloseSelectedPluginOptionsDetail()
    {
        _selectedPluginOptionsPluginId = null;
        _pendingPluginOptionsKeyItem = null;
        _pluginOptionsHoverIndex = -1;
        _pluginOptionsScrollOffset = 0;
    }

    private int GetPluginOptionsVisibleRowCapacity()
    {
        return ViewportHeight < 540 ? 14 : 16;
    }

    private void ClampPluginOptionsScrollOffset(int rowCount, int visibleRowCount)
    {
        _pluginOptionsScrollOffset = Math.Clamp(
            _pluginOptionsScrollOffset,
            0,
            Math.Max(0, rowCount - visibleRowCount));
    }

    private static string GetClientPluginStatusLabel(ClientPluginOptionsEntry entry)
    {
        if (!entry.IsEnabled)
        {
            return "Disabled";
        }

        return entry.IsLoaded ? "Enabled" : "Load failed";
    }

    private static string FormatClientPluginVersion(Version version)
    {
        return version.Revision >= 0
            ? version.ToString()
            : version.Build >= 0
                ? version.ToString(3)
                : $"{version.Major}.{version.Minor}";
    }

    private string GetPluginOptionValueLabel(ClientPluginOptionItem item)
    {
        try
        {
            return item.GetValueLabel();
        }
        catch (Exception ex)
        {
            AddConsoleLine($"plugin option read failed for \"{item.Label}\": {ex.Message}");
            return "<error>";
        }
    }

    private void ActivatePluginOption(ClientPluginOptionItem item)
    {
        if (item is ClientPluginKeyOptionItem keyItem)
        {
            _pendingPluginOptionsKeyItem = keyItem;
            return;
        }

        try
        {
            item.Activate();
        }
        catch (Exception ex)
        {
            AddConsoleLine($"plugin option apply failed for \"{item.Label}\": {ex.Message}");
        }
    }

    private readonly record struct PluginOptionsMenuRow(
        string Label,
        string Value,
        bool Selectable,
        bool IsHeader,
        Action? Activate);
}
