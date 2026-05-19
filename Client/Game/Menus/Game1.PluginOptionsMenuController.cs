#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;
using System;
using System.Collections.Generic;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class PluginOptionsMenuController
    {
        private readonly Game1 _game;

        public PluginOptionsMenuController(Game1 game)
        {
            _game = game;
        }

        public void UpdatePluginOptionsMenu(KeyboardState keyboard, MouseState mouse)
        {
            var rows = BuildPluginOptionsMenuRows();
            GetPluginOptionsPanelLayout(out _, out var listBounds, out var backBounds, out _, out var rowHeight);
            var visibleRowCount = Math.Max(1, Math.Min(rows.Count, listBounds.Height / rowHeight));
            ClampPluginOptionsScrollOffset(rows.Count, visibleRowCount);
            var wheelDelta = mouse.ScrollWheelValue - _game._previousMouse.ScrollWheelValue;

            if (_game._pendingPluginOptionsKeyItem is not null)
            {
                if (_game.IsKeyPressed(keyboard, Keys.Escape))
                {
                    _game._pendingPluginOptionsKeyItem = null;
                    return;
                }

                foreach (var key in keyboard.GetPressedKeys())
                {
                    if (_game._previousKeyboard.IsKeyDown(key))
                    {
                        continue;
                    }

                    try
                    {
                        _game._pendingPluginOptionsKeyItem.SetKey(key);
                    }
                    catch (Exception ex)
                    {
                        _game.AddConsoleLine($"plugin option apply failed for \"{_game._pendingPluginOptionsKeyItem.Label}\": {ex.Message}");
                    }

                    _game._pendingPluginOptionsKeyItem = null;
                    return;
                }

                return;
            }

            if (_game.IsKeyPressed(keyboard, Keys.Escape))
            {
                if (_game._selectedPluginOptionsPluginId is not null)
                {
                    CloseSelectedPluginOptionsDetail();
                    return;
                }

                _game.ClosePluginOptionsMenu();
                return;
            }

            if (wheelDelta != 0 && listBounds.Contains(mouse.Position))
            {
                var stepCount = Math.Max(1, Math.Abs(wheelDelta) / 120);
                _game._pluginOptionsScrollOffset = Math.Clamp(
                    _game._pluginOptionsScrollOffset + (wheelDelta > 0 ? -stepCount : stepCount),
                    0,
                    Math.Max(0, rows.Count - visibleRowCount));
            }

            if (backBounds.Contains(mouse.Position))
            {
                _game._pluginOptionsHoverIndex = rows.Count;
            }
            else if (listBounds.Contains(mouse.Position))
            {
                var visibleHoverIndex = (mouse.Y - listBounds.Y) / rowHeight;
                var hoverIndex = _game._pluginOptionsScrollOffset + visibleHoverIndex;
                var visibleStart = _game._pluginOptionsScrollOffset;
                var visibleEndExclusive = visibleStart + visibleRowCount;
                _game._pluginOptionsHoverIndex = visibleHoverIndex >= 0
                    && hoverIndex >= visibleStart
                    && hoverIndex < visibleEndExclusive
                    && hoverIndex < rows.Count
                    && rows[hoverIndex].Selectable
                        ? hoverIndex
                        : -1;
            }
            else
            {
                _game._pluginOptionsHoverIndex = -1;
            }

            var clickPressed = mouse.LeftButton == ButtonState.Pressed && _game._previousMouse.LeftButton != ButtonState.Pressed;
            if (!clickPressed || _game._pluginOptionsHoverIndex < 0)
            {
                return;
            }

            if (_game._pluginOptionsHoverIndex == rows.Count)
            {
                if (_game._selectedPluginOptionsPluginId is not null)
                {
                    CloseSelectedPluginOptionsDetail();
                }
                else
                {
                    _game.ClosePluginOptionsMenu();
                }

                return;
            }

            rows[_game._pluginOptionsHoverIndex].Activate?.Invoke();
        }

        public void DrawPluginOptionsMenu()
        {
            var viewportWidth = _game.ViewportWidth;
            var viewportHeight = _game.ViewportHeight;
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.78f);

            // Draw bottom bar and runners (in animated mode only) - behind everything else
            if (_game._menuBackgroundMode != MenuBackgroundMode.Static)
            {
                const int bottomBarHeight = 76;
                var barY = viewportHeight - bottomBarHeight;
                var bottomBarBounds = new Rectangle(0, barY, viewportWidth, bottomBarHeight);
                _game._spriteBatch.Draw(_game._pixel, bottomBarBounds, new Color(0x57, 0x4f, 0x47));
                _game._menuBottomBarRunners.Draw(bottomBarBounds);
            }

            var rows = BuildPluginOptionsMenuRows();
            GetPluginOptionsPanelLayout(out var panel, out var listBounds, out var backBounds, out var compactLayout, out var rowHeight);
            var visibleRowCount = Math.Max(1, Math.Min(rows.Count, listBounds.Height / rowHeight));
            ClampPluginOptionsScrollOffset(rows.Count, visibleRowCount);

            _game.DrawRoundedRectangleOutline(panel, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 2, radius: 8);

            const float headerScale = 1.15f;
            const float rowTextScale = 1f;
            const float compactRowTextScale = 1f;
            const float rowHorizontalPadding = 14f;
            const float rowColumnGap = 20f;

            var title = _game._selectedPluginOptionsPluginId is null ? "Plugin Options" : "Plugin Options Detail";
            _game.DrawBitmapFontText(title, new Vector2(listBounds.X, panel.Y + 14f), Color.White, headerScale);

            if (rows.Count > visibleRowCount)
            {
                var visibleStart = _game._pluginOptionsScrollOffset + 1;
                var visibleEnd = Math.Min(rows.Count, _game._pluginOptionsScrollOffset + visibleRowCount);
                _game.DrawBitmapFontText(
                    $"{visibleStart}-{visibleEnd}/{rows.Count}",
                    new Vector2(listBounds.Right - (compactLayout ? 78f : 96f), panel.Y + 14f),
                    new Color(186, 186, 186),
                    compactLayout ? 0.92f : 1f);
            }

            var endIndex = Math.Min(rows.Count, _game._pluginOptionsScrollOffset + visibleRowCount);
            for (var index = _game._pluginOptionsScrollOffset; index < endIndex; index += 1)
            {
                var row = rows[index];
                var visibleRow = index - _game._pluginOptionsScrollOffset;
                var rowBounds = new Rectangle(listBounds.X, listBounds.Y + (visibleRow * rowHeight), listBounds.Width, rowHeight - 2);
                var isHovered = index == _game._pluginOptionsHoverIndex;
                var rowFill = isHovered ? new Color(75, 67, 62) : new Color(54, 47, 41);
                _game._spriteBatch.Draw(_game._pixel, rowBounds, rowFill);

                var textScale = compactLayout ? compactRowTextScale : rowTextScale;
                var textY = rowBounds.Y + ((rowBounds.Height - _game.MeasureBitmapFontHeight(textScale)) * 0.5f);
                var labelX = rowBounds.X + rowHorizontalPadding;
                var valueRightX = rowBounds.Right - rowHorizontalPadding;

                var trimmedValue = _game.TrimBitmapMenuText(row.Value, rowBounds.Width * 0.42f, textScale);
                var valueWidth = _game.MeasureBitmapFontWidth(trimmedValue, textScale);
                var valueX = valueRightX - valueWidth;
                var labelMaxWidth = Math.Max(40f, valueX - labelX - rowColumnGap);
                var trimmedLabel = _game.TrimBitmapMenuText(row.Label, labelMaxWidth, textScale);
                var color = row.IsHeader ? new Color(240, 200, 120) : Color.White;

                _game.DrawBitmapFontText(trimmedLabel, new Vector2(labelX, textY), color, textScale);
                if (!string.IsNullOrWhiteSpace(trimmedValue))
                {
                    _game.DrawBitmapFontText(trimmedValue, new Vector2(valueX, textY), color, textScale);
                }
            }

            if (rows.Count > visibleRowCount)
            {
                var trackBounds = new Rectangle(panel.Right - 20, listBounds.Y, 8, listBounds.Height);
                _game._spriteBatch.Draw(_game._pixel, trackBounds, new Color(30, 32, 38));

                var maxOffset = Math.Max(1, rows.Count - visibleRowCount);
                var thumbHeight = Math.Max(24, (int)MathF.Round(trackBounds.Height * (visibleRowCount / (float)rows.Count)));
                var thumbTravel = Math.Max(0, trackBounds.Height - thumbHeight);
                var thumbY = trackBounds.Y + (int)MathF.Round((_game._pluginOptionsScrollOffset / (float)maxOffset) * thumbTravel);
                var thumbBounds = new Rectangle(trackBounds.X, thumbY, trackBounds.Width, thumbHeight);
                _game._spriteBatch.Draw(_game._pixel, thumbBounds, new Color(125, 125, 125));
            }

            if (_game._pendingPluginOptionsKeyItem is not null)
            {
                _game.DrawBitmapFontText(
                    $"Press a key for {_game._pendingPluginOptionsKeyItem.Label} (Esc to cancel)",
                    new Vector2(listBounds.X, panel.Y + 36f),
                    Color.Orange,
                    compactLayout ? 0.92f : 1f);
            }

            var backHovered = _game._pluginOptionsHoverIndex == rows.Count;
            _game.DrawMenuButtonScaled(backBounds, "Back", backHovered, 1f);
        }

        public bool HasClientPluginOptions()
        {
            return GetClientPluginOptionsEntries().Count > 0;
        }

        private List<PluginOptionsMenuRow> BuildPluginOptionsMenuRows()
        {
            var rows = new List<PluginOptionsMenuRow>();
            var pluginEntries = GetClientPluginOptionsEntries();
            if (_game._selectedPluginOptionsPluginId is null)
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

                return rows;
            }

            var selectedEntry = GetSelectedPluginOptionsEntry();
            if (selectedEntry is null)
            {
                _game._selectedPluginOptionsPluginId = null;
                return BuildPluginOptionsMenuRows();
            }

            rows.Add(new PluginOptionsMenuRow(selectedEntry.DisplayName, string.Empty, Selectable: false, IsHeader: true, Activate: null));
            rows.Add(new PluginOptionsMenuRow("Version", FormatClientPluginVersion(selectedEntry.Version), Selectable: false, IsHeader: false, Activate: null));
            rows.Add(new PluginOptionsMenuRow(
                "Enabled",
                selectedEntry.IsEnabled ? "Enabled" : "Disabled",
                Selectable: true,
                IsHeader: false,
                Activate: () => _game.SetClientPluginEnabled(selectedEntry.PluginId, !selectedEntry.IsEnabled)));
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

            return rows;
        }

        private void GetPluginOptionsPanelLayout(out Rectangle panel, out Rectangle listBounds, out Rectangle backBounds, out bool compactLayout, out int rowHeight)
        {
            var panelWidth = Math.Min(_game.ViewportWidth - 32, 760);
            var panelHeight = Math.Min(_game.ViewportHeight - 32, _game.ViewportHeight < 540 ? _game.ViewportHeight - 36 : 620);
            panel = new Rectangle(
                (_game.ViewportWidth - panelWidth) / 2,
                (_game.ViewportHeight - panelHeight) / 2,
                panelWidth,
                panelHeight);

            compactLayout = panel.Height < 540 || panel.Width < 700;
            var padding = compactLayout ? 20 : 28;
            var baseRowHeight = compactLayout ? 24 : 26;
            var rowSpacing = compactLayout ? 4 : 6;
            rowHeight = baseRowHeight + rowSpacing;
            var headerHeight = compactLayout ? 48 : 56;
            var footerHeight = compactLayout ? 56 : 64;
            var scrollbarPadding = 18;

            listBounds = new Rectangle(
                panel.X + padding,
                panel.Y + headerHeight,
                panel.Width - (padding * 2) - scrollbarPadding,
                Math.Max(rowHeight, panel.Height - headerHeight - footerHeight - 10));

            var backWidth = compactLayout ? 150 : 180;
            var backHeight = compactLayout ? 36 : 42;
            backBounds = new Rectangle(panel.Right - padding - backWidth, panel.Bottom - padding - backHeight, backWidth, backHeight);
        }

        private IReadOnlyList<ClientPluginOptionsEntry> GetClientPluginOptionsEntries()
        {
            return _game._clientPluginHost?.GetPluginOptionsEntries() ?? [];
        }

        private ClientPluginOptionsEntry? GetSelectedPluginOptionsEntry()
        {
            var selectedPluginId = _game._selectedPluginOptionsPluginId;
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
            _game._selectedPluginOptionsPluginId = pluginId;
            _game._pendingPluginOptionsKeyItem = null;
            _game._pluginOptionsHoverIndex = -1;
            _game._pluginOptionsScrollOffset = 0;
        }

        private void CloseSelectedPluginOptionsDetail()
        {
            _game._selectedPluginOptionsPluginId = null;
            _game._pendingPluginOptionsKeyItem = null;
            _game._pluginOptionsHoverIndex = -1;
            _game._pluginOptionsScrollOffset = 0;
        }

        private int GetPluginOptionsVisibleRowCapacity()
        {
            return _game.ViewportHeight < 540 ? 14 : 16;
        }

        private void ClampPluginOptionsScrollOffset(int rowCount, int visibleRowCount)
        {
            _game._pluginOptionsScrollOffset = Math.Clamp(
                _game._pluginOptionsScrollOffset,
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
                _game.AddConsoleLine($"plugin option read failed for \"{item.Label}\": {ex.Message}");
                return "<error>";
            }
        }

        private void ActivatePluginOption(ClientPluginOptionItem item)
        {
            if (item is ClientPluginKeyOptionItem keyItem)
            {
                _game._pendingPluginOptionsKeyItem = keyItem;
                return;
            }

            try
            {
                item.Activate();
            }
            catch (Exception ex)
            {
                _game.AddConsoleLine($"plugin option apply failed for \"{item.Label}\": {ex.Message}");
            }
        }

        private readonly record struct PluginOptionsMenuRow(
            string Label,
            string Value,
            bool Selectable,
            bool IsHeader,
            Action? Activate);
    }
}
