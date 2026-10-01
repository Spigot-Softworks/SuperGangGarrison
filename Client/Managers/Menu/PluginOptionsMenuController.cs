#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;
using System;
using System.Collections.Generic;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class PluginOptionsMenuController
    {
        private readonly IMenuContext _context;

        public PluginOptionsMenuController(IMenuContext context)
        {
            _context = context;
        }

        public void UpdatePluginOptionsMenu(KeyboardState keyboard, MouseState mouse)
        {
            var rows = BuildPluginOptionsMenuRows();
            GetPluginOptionsPanelLayout(out var panel, out var listBounds, out var backBounds, out _, out var rowHeight);
            var visibleRowCount = Math.Max(1, Math.Min(rows.Count, listBounds.Height / rowHeight));
            ClampPluginOptionsScrollOffset(rows.Count, visibleRowCount);

            var trackBounds = new Rectangle(panel.Right - 20, listBounds.Y, 8, listBounds.Height);
            var pluginOptionsScrollOffset = _context._pluginOptionsScrollOffset;
            if (_context.TryHandleScrollbarDrag(
                    mouse,
                    _context._previousMouse,
                    ScrollbarOwners.PluginOptionsMenu,
                    trackBounds,
                    ref pluginOptionsScrollOffset,
                    rows.Count,
                    visibleRowCount))
            {
                _context._pluginOptionsScrollOffset = pluginOptionsScrollOffset;
                return;
            }

            _context._pluginOptionsScrollOffset = pluginOptionsScrollOffset;

            var wheelDelta = mouse.ScrollWheelValue - _context._previousMouse.ScrollWheelValue;

            if (_context._pendingPluginOptionsKeyItem is not null)
            {
                if (_context.IsKeyPressed(keyboard, Keys.Escape) || _context.IsControllerMenuBackPressed())
                {
                    _context._pendingPluginOptionsKeyItem = null;
                    return;
                }

                foreach (var key in keyboard.GetPressedKeys())
                {
                    if (_context._previousKeyboard.IsKeyDown(key))
                    {
                        continue;
                    }

                    try
                    {
                        _context._pendingPluginOptionsKeyItem.SetKey(key);
                    }
                    catch (Exception ex)
                    {
                        _context.AddConsoleLine($"plugin option apply failed for \"{_context._pendingPluginOptionsKeyItem.Label}\": {ex.Message}");
                    }

                    _context._pendingPluginOptionsKeyItem = null;
                    return;
                }

                return;
            }

            if (_context.IsKeyPressed(keyboard, Keys.Escape) || _context.IsControllerMenuBackPressed())
            {
                if (_context._selectedPluginOptionsPluginId is not null)
                {
                    CloseSelectedPluginOptionsDetail();
                    return;
                }

                _context.ClosePluginOptionsMenu();
                return;
            }

            if (TryUpdatePluginOptionsControllerInput(rows, visibleRowCount))
            {
                return;
            }

            if (wheelDelta != 0 && listBounds.Contains(mouse.Position))
            {
                var stepCount = Math.Max(1, Math.Abs(wheelDelta) / 120);
                _context._pluginOptionsScrollOffset = Math.Clamp(
                    _context._pluginOptionsScrollOffset + (wheelDelta > 0 ? -stepCount : stepCount),
                    0,
                    Math.Max(0, rows.Count - visibleRowCount));
            }

            if (_context.IsControllerMenuInputActive())
            {
                if (_context._pluginOptionsHoverIndex < 0)
                {
                    _context._pluginOptionsHoverIndex = FindNextSelectablePluginOptionsIndex(rows, -1, 1);
                }
            }
            else
            {
                _context._pluginOptionsHoverIndex = -1;
            }

            if (_context.ShouldUseMouseMenuHover(mouse) && backBounds.Contains(mouse.Position))
            {
                _context._pluginOptionsHoverIndex = rows.Count;
            }
            else if (_context.ShouldUseMouseMenuHover(mouse) && listBounds.Contains(mouse.Position))
            {
                var visibleHoverIndex = (mouse.Y - listBounds.Y) / rowHeight;
                var hoverIndex = _context._pluginOptionsScrollOffset + visibleHoverIndex;
                var visibleStart = _context._pluginOptionsScrollOffset;
                var visibleEndExclusive = visibleStart + visibleRowCount;
                _context._pluginOptionsHoverIndex = visibleHoverIndex >= 0
                    && hoverIndex >= visibleStart
                    && hoverIndex < visibleEndExclusive
                    && hoverIndex < rows.Count
                    && rows[hoverIndex].Selectable
                        ? hoverIndex
                        : -1;
            }
            else
            {
                _context._pluginOptionsHoverIndex = -1;
            }

            var clickPressed = mouse.LeftButton == ButtonState.Pressed && _context._previousMouse.LeftButton != ButtonState.Pressed;
            if (!clickPressed || _context._pluginOptionsHoverIndex < 0)
            {
                return;
            }

            if (_context._pluginOptionsHoverIndex == rows.Count)
            {
                if (_context._selectedPluginOptionsPluginId is not null)
                {
                    CloseSelectedPluginOptionsDetail();
                }
                else
                {
                    _context.ClosePluginOptionsMenu();
                }

                return;
            }

            rows[_context._pluginOptionsHoverIndex].Activate?.Invoke();
        }

        private bool TryUpdatePluginOptionsControllerInput(IReadOnlyList<PluginOptionsMenuRow> rows, int visibleRowCount)
        {
            if (!_context.IsControllerMenuInputActive())
            {
                return false;
            }

            var handled = false;
            if (_context.TryConsumeControllerMenuNavigation(out _, out var verticalStep) && verticalStep != 0)
            {
                _context._pluginOptionsHoverIndex = MovePluginOptionsControllerSelection(rows, _context._pluginOptionsHoverIndex, verticalStep);
                EnsurePluginOptionsControllerSelectionVisible(rows.Count, visibleRowCount);
                handled = true;
            }

            if (_context.IsControllerMenuConfirmPressed())
            {
                if (_context._pluginOptionsHoverIndex < 0)
                {
                    _context._pluginOptionsHoverIndex = FindNextSelectablePluginOptionsIndex(rows, -1, 1);
                }

                if (_context._pluginOptionsHoverIndex == rows.Count)
                {
                    if (_context._selectedPluginOptionsPluginId is not null)
                    {
                        CloseSelectedPluginOptionsDetail();
                    }
                    else
                    {
                        _context.ClosePluginOptionsMenu();
                    }

                    return true;
                }

                if (_context._pluginOptionsHoverIndex >= 0 && _context._pluginOptionsHoverIndex < rows.Count)
                {
                    rows[_context._pluginOptionsHoverIndex].Activate?.Invoke();
                    return true;
                }
            }

            return handled;
        }

        private void EnsurePluginOptionsControllerSelectionVisible(int rowCount, int visibleRowCount)
        {
            if (_context._pluginOptionsHoverIndex < 0 || _context._pluginOptionsHoverIndex >= rowCount)
            {
                return;
            }

            if (_context._pluginOptionsHoverIndex < _context._pluginOptionsScrollOffset)
            {
                _context._pluginOptionsScrollOffset = _context._pluginOptionsHoverIndex;
            }
            else if (_context._pluginOptionsHoverIndex >= _context._pluginOptionsScrollOffset + visibleRowCount)
            {
                _context._pluginOptionsScrollOffset = _context._pluginOptionsHoverIndex - visibleRowCount + 1;
            }
        }

        private static int MovePluginOptionsControllerSelection(IReadOnlyList<PluginOptionsMenuRow> rows, int currentIndex, int step)
        {
            var itemCount = rows.Count + 1;
            if (itemCount <= 0 || step == 0)
            {
                return currentIndex;
            }

            var index = currentIndex < 0
                ? (step > 0 ? -1 : itemCount)
                : currentIndex;

            for (var checkedCount = 0; checkedCount < itemCount; checkedCount += 1)
            {
                index = (index + step + itemCount) % itemCount;
                if (index == rows.Count || (index >= 0 && rows[index].Selectable))
                {
                    return index;
                }
            }

            return currentIndex;
        }

        private static int FindNextSelectablePluginOptionsIndex(IReadOnlyList<PluginOptionsMenuRow> rows, int currentIndex, int step)
        {
            return MovePluginOptionsControllerSelection(rows, currentIndex, step);
        }

        public void DrawPluginOptionsMenu()
        {
            var viewportWidth = _context.ViewportWidth;
            var viewportHeight = _context.ViewportHeight;
            _context._spriteBatch.Draw(_context._pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.86f);

            // Draw bottom bar and runners (in animated mode only) - behind everything else
            if (_context.GameplayRuntimeSettings.MenuBackgroundMode != MenuBackgroundMode.Static)
            {
                const int bottomBarHeight = 76;
                var barY = viewportHeight - bottomBarHeight;
                var bottomBarBounds = new Rectangle(0, barY, viewportWidth, bottomBarHeight);
                _context._spriteBatch.Draw(_context._pixel, bottomBarBounds, new Color(0x57, 0x4f, 0x47));
                _context.Menus.MenuBottomBarRunners.Draw(bottomBarBounds);
            }

            var rows = BuildPluginOptionsMenuRows();
            GetPluginOptionsPanelLayout(out var panel, out var listBounds, out var backBounds, out var compactLayout, out var rowHeight);
            var mouse = _context.GetFrameMouseState();
            var visibleRowCount = Math.Max(1, Math.Min(rows.Count, listBounds.Height / rowHeight));
            ClampPluginOptionsScrollOffset(rows.Count, visibleRowCount);

            _context.DrawRoundedRectangleOutline(panel, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 2, radius: 8);

            const float headerScale = 1.15f;
            const float rowTextScale = 1f;
            const float compactRowTextScale = 1f;
            const float rowHorizontalPadding = 14f;
            const float rowColumnGap = 20f;

            var title = _context._selectedPluginOptionsPluginId is null ? "Plugin Options" : "Plugin Options Detail";
            _context.DrawBitmapFontText(title, new Vector2(listBounds.X, panel.Y + 14f), Color.White, headerScale);

            if (rows.Count > visibleRowCount)
            {
                var visibleStart = _context._pluginOptionsScrollOffset + 1;
                var visibleEnd = Math.Min(rows.Count, _context._pluginOptionsScrollOffset + visibleRowCount);
                _context.DrawBitmapFontText(
                    $"{visibleStart}-{visibleEnd}/{rows.Count}",
                    new Vector2(listBounds.Right - (compactLayout ? 78f : 96f), panel.Y + 14f),
                    new Color(186, 186, 186),
                    compactLayout ? 0.92f : 1f);
            }

            var endIndex = Math.Min(rows.Count, _context._pluginOptionsScrollOffset + visibleRowCount);
            for (var index = _context._pluginOptionsScrollOffset; index < endIndex; index += 1)
            {
                var row = rows[index];
                var visibleRow = index - _context._pluginOptionsScrollOffset;
                var rowBounds = new Rectangle(listBounds.X, listBounds.Y + (visibleRow * rowHeight), listBounds.Width, rowHeight - 2);
                var isHovered = index == _context._pluginOptionsHoverIndex || rowBounds.Contains(mouse.Position);
                var rowFill = isHovered ? new Color(36, 32, 29) : new Color(54, 47, 41);
                _context._spriteBatch.Draw(_context._pixel, rowBounds, rowFill);

                var textScale = compactLayout ? compactRowTextScale : rowTextScale;
                var textY = rowBounds.Y + ((rowBounds.Height - _context.MeasureBitmapFontHeight(textScale)) * 0.5f);
                var labelX = rowBounds.X + rowHorizontalPadding;
                var valueRightX = rowBounds.Right - rowHorizontalPadding;

                var trimmedValue = _context.TrimBitmapMenuText(row.Value, rowBounds.Width * 0.42f, textScale);
                var valueWidth = _context.MeasureBitmapFontWidth(trimmedValue, textScale);
                var valueX = valueRightX - valueWidth;
                var labelMaxWidth = Math.Max(40f, valueX - labelX - rowColumnGap);
                var trimmedLabel = _context.TrimBitmapMenuText(row.Label, labelMaxWidth, textScale);
                var color = row.IsHeader ? new Color(240, 200, 120) : Color.White;

                _context.DrawBitmapFontText(trimmedLabel, new Vector2(labelX, textY), color, textScale);
                if (!string.IsNullOrWhiteSpace(trimmedValue))
                {
                    _context.DrawBitmapFontText(trimmedValue, new Vector2(valueX, textY), color, textScale);
                }
            }

            if (rows.Count > visibleRowCount)
            {
                var trackBounds = new Rectangle(panel.Right - 20, listBounds.Y, 8, listBounds.Height);
                _context._spriteBatch.Draw(_context._pixel, trackBounds, new Color(30, 32, 38));

                var maxOffset = Math.Max(1, rows.Count - visibleRowCount);
                var thumbHeight = Math.Max(24, (int)MathF.Round(trackBounds.Height * (visibleRowCount / (float)rows.Count)));
                var thumbTravel = Math.Max(0, trackBounds.Height - thumbHeight);
                var thumbY = trackBounds.Y + (int)MathF.Round((_context._pluginOptionsScrollOffset / (float)maxOffset) * thumbTravel);
                var thumbBounds = new Rectangle(trackBounds.X, thumbY, trackBounds.Width, thumbHeight);
                _context._spriteBatch.Draw(_context._pixel, thumbBounds, new Color(125, 125, 125));
            }

            if (_context._pendingPluginOptionsKeyItem is not null)
            {
                _context.DrawBitmapFontText(
                    $"Press a key for {_context._pendingPluginOptionsKeyItem.Label} (Esc to cancel)",
                    new Vector2(listBounds.X, panel.Y + 36f),
                    Color.Orange,
                    compactLayout ? 0.92f : 1f);
            }

            var backHovered = _context._pluginOptionsHoverIndex == rows.Count || backBounds.Contains(mouse.Position);
            _context.DrawMenuButtonScaled(backBounds, "Back", backHovered, 1f);
        }

        public bool HasClientPluginOptions()
        {
            return GetClientPluginOptionsEntries().Count > 0;
        }

        private List<PluginOptionsMenuRow> BuildPluginOptionsMenuRows()
        {
            var rows = new List<PluginOptionsMenuRow>();
            var pluginEntries = GetClientPluginOptionsEntries();
            if (_context._selectedPluginOptionsPluginId is null)
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
                _context._selectedPluginOptionsPluginId = null;
                return BuildPluginOptionsMenuRows();
            }

            rows.Add(new PluginOptionsMenuRow(selectedEntry.DisplayName, string.Empty, Selectable: false, IsHeader: true, Activate: null));
            rows.Add(new PluginOptionsMenuRow("Version", FormatClientPluginVersion(selectedEntry.Version), Selectable: false, IsHeader: false, Activate: null));
            rows.Add(new PluginOptionsMenuRow(
                "Enabled",
                selectedEntry.IsEnabled ? "Enabled" : "Disabled",
                Selectable: true,
                IsHeader: false,
                Activate: () => _context.SetClientPluginEnabled(selectedEntry.PluginId, !selectedEntry.IsEnabled)));
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
            var panelWidth = Math.Min(_context.ViewportWidth - 32, 760);
            var panelHeight = Math.Min(_context.ViewportHeight - 32, _context.ViewportHeight < 540 ? _context.ViewportHeight - 36 : 620);
            panel = new Rectangle(
                (_context.ViewportWidth - panelWidth) / 2,
                (_context.ViewportHeight - panelHeight) / 2,
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
            return _context._clientPluginHost?.GetPluginOptionsEntries() ?? [];
        }

        private ClientPluginOptionsEntry? GetSelectedPluginOptionsEntry()
        {
            var selectedPluginId = _context._selectedPluginOptionsPluginId;
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
            _context._selectedPluginOptionsPluginId = pluginId;
            _context._pendingPluginOptionsKeyItem = null;
            _context._pluginOptionsHoverIndex = -1;
            _context._pluginOptionsScrollOffset = 0;
        }

        private void CloseSelectedPluginOptionsDetail()
        {
            _context._selectedPluginOptionsPluginId = null;
            _context._pendingPluginOptionsKeyItem = null;
            _context._pluginOptionsHoverIndex = -1;
            _context._pluginOptionsScrollOffset = 0;
        }


        private void ClampPluginOptionsScrollOffset(int rowCount, int visibleRowCount)
        {
            _context._pluginOptionsScrollOffset = Math.Clamp(
                _context._pluginOptionsScrollOffset,
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
                _context.AddConsoleLine($"plugin option read failed for \"{item.Label}\": {ex.Message}");
                return "<error>";
            }
        }

        private void ActivatePluginOption(ClientPluginOptionItem item)
        {
            if (item is ClientPluginKeyOptionItem keyItem)
            {
                _context._pendingPluginOptionsKeyItem = keyItem;
                return;
            }

            try
            {
                item.Activate();
            }
            catch (Exception ex)
            {
                _context.AddConsoleLine($"plugin option apply failed for \"{item.Label}\": {ex.Message}");
            }
        }

        private readonly record struct PluginOptionsMenuRow(
            string Label,
            string Value,
            bool Selectable,
            bool IsHeader,
            Action? Activate);
}
