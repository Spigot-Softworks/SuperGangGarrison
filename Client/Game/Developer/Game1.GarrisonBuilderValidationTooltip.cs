using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private Rectangle _builderValidationHoverBounds;
    private bool _builderValidationTooltipVisible;
    private int _builderValidationScroll;
    private int _builderValidationMaximumScroll;

    private void DrawGarrisonBuilderValidationTooltip(MouseState mouse)
    {
        if (!_builderEntityPaletteVisible || _builderPropertyTarget != GarrisonBuilderPropertyTarget.None
            || _builderEntityDragging || _builderAreaSelectDragging || _builderPlacementDragging
            || _builderMapPanDragging || _builderGameplayMessageImageDragging
            || _builderActiveResizeHandle != GarrisonBuilderResizeHandle.None
            || _builderGameplayMessageImageActiveResizeHandle != GarrisonBuilderResizeHandle.None
            || _builderGameModeMenuOpen || _builderMenuBarOpenMenu != GarrisonBuilderMenuBarMenu.None
            || _builderEntityContextMenuOpen || _builderEntityOverlapPickerOpen || _builderLayerContextMenuOpen
            || _builderLayerParallaxDialogOpen || _builderMapNameCollisionDialogOpen || _builderLogicRecolorDialogOpen)
        {
            _builderValidationTooltipVisible = false;
            return;
        }
        var sidebarWidth = GetModernGarrisonBuilderSidebarWidth();
        var sidebar = new Rectangle(0, GetModernBuilderMenuBarHeight(), sidebarWidth,
            BuilderViewportHeight - GetModernBuilderMenuBarHeight() - GetModernGarrisonBuilderLayerStripHeight());
        TryGetModernGarrisonBuilderSidebarModeButtonBounds(sidebar, out var modeBounds);
        var status = new Rectangle(10, modeBounds.Bottom + 6, sidebarWidth - 20,
            (int)GetGarrisonBuilderMinimumButtonHeight());
        if (!status.Contains(mouse.Position)
            && !(_builderValidationTooltipVisible && _builderValidationHoverBounds.Contains(mouse.Position)))
        {
            _builderValidationTooltipVisible = false;
            _builderValidationScroll = 0;
            return;
        }
        var width = Math.Min(600, BuilderViewportWidth - 24);
        var lineHeight = (int)MathF.Ceiling(MeasureBitmapFontHeight(1f)) + 6;
        var lines = new List<string>();
        var validation = GetGarrisonBuilderValidation();
        if (validation.Issues.Count == 0) lines.Add("No map issues found.");
        foreach (var issue in validation.Issues)
            AddGarrisonBuilderValidationLines(lines,
                issue.Code == "validation_pending" ? issue.Message : $"{issue.Severity}: {issue.Message}", width - 24);
        var availableHeight = Math.Max(lineHeight * 3 + 24, BuilderViewportHeight - status.Bottom - 20);
        var visibleLines = Math.Max(1, Math.Min(lines.Count, (availableHeight - 24) / lineHeight - 1));
        _builderValidationMaximumScroll = Math.Max(0, lines.Count - visibleLines);
        _builderValidationScroll = Math.Clamp(_builderValidationScroll, 0, _builderValidationMaximumScroll);
        var height = 24 + lineHeight * (visibleLines + (_builderValidationMaximumScroll > 0 ? 1 : 0));
        var panel = new Rectangle(12, Math.Min(status.Bottom + 4, BuilderViewportHeight - height - 8), width, height);
        _builderValidationHoverBounds = Rectangle.Union(status, panel);
        _builderValidationTooltipVisible = true;
        DrawMenuPanelBackdrop(panel, 1f);
        for (var index = 0; index < visibleLines; index++)
            DrawBitmapFontText(lines[index + _builderValidationScroll],
                new Vector2(panel.X + 12, panel.Y + 12 + index * lineHeight), Color.White, 1f);
        if (_builderValidationMaximumScroll > 0)
            DrawBitmapFontText($"Scroll to read more ({_builderValidationScroll + 1}-{_builderValidationScroll + visibleLines}/{lines.Count})",
                new Vector2(panel.X + 12, panel.Bottom - 12 - lineHeight), new Color(255, 214, 118), 1f);
    }

    private void AddGarrisonBuilderValidationLines(List<string> lines, string text, int width)
    {
        foreach (var paragraph in text.Replace("\r", "").Split('\n'))
        {
            var remaining = paragraph.Trim();
            while (remaining.Length > 0)
            {
                var count = remaining.Length;
                while (count > 1 && MeasureBitmapFontWidth(remaining[..count], 1f) > width) count--;
                if (count < remaining.Length)
                {
                    var space = remaining.LastIndexOf(' ', count - 1, count);
                    if (space > 0) count = space;
                }
                lines.Add(remaining[..count]);
                remaining = remaining[count..].TrimStart();
            }
        }
    }
}
