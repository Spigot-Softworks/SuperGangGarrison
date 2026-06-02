#nullable enable

using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

internal sealed class HostSetupOptionsMenuLayout
{
    public Rectangle Panel { get; init; }

    public Rectangle ListBounds { get; init; }

    public Rectangle ConfirmBounds { get; init; }

    public Rectangle ResetBounds { get; init; }

    public Rectangle[] TabBounds { get; init; } = [];

    public bool CompactLayout { get; init; }

    public int RowHeight { get; init; }

    public int VisibleRowCount { get; init; }

    public static HostSetupOptionsMenuLayout Create(int viewportWidth, int viewportHeight)
    {
        var panelWidth = System.Math.Min(viewportWidth - 32, 760);
        var panelHeight = System.Math.Min(viewportHeight - 32, viewportHeight < 540 ? viewportHeight - 36 : 620);
        var panel = new Rectangle(
            (viewportWidth - panelWidth) / 2,
            (viewportHeight - panelHeight) / 2,
            panelWidth,
            panelHeight);

        var compactLayout = panel.Height < 540 || panel.Width < 700;
        var padding = compactLayout ? 20 : 28;
        var baseRowHeight = compactLayout ? 24 : 26;
        var rowSpacing = compactLayout ? 4 : 6;
        var rowHeight = baseRowHeight + rowSpacing;
        var titleHeight = compactLayout ? 48 : 56;
        var tabRowHeight = compactLayout ? 44 : 48;
        var headerHeight = titleHeight + tabRowHeight;
        var footerHeight = compactLayout ? 56 : 64;
        var scrollbarPadding = 18;
        var listTopPadding = compactLayout ? 8 : 10;
        var listBounds = new Rectangle(
            panel.X + padding,
            panel.Y + headerHeight + listTopPadding,
            panel.Width - (padding * 2) - scrollbarPadding,
            System.Math.Max(rowHeight, panel.Height - headerHeight - footerHeight - 10 - listTopPadding));

        var buttonWidth = compactLayout ? 150 : 180;
        var buttonHeight = compactLayout ? 36 : 42;
        var buttonSpacing = compactLayout ? 10 : 14;
        var confirmBounds = new Rectangle(
            panel.Right - padding - (buttonWidth * 2) - buttonSpacing,
            panel.Bottom - padding - buttonHeight,
            buttonWidth,
            buttonHeight);
        var resetBounds = new Rectangle(
            panel.Right - padding - buttonWidth,
            panel.Bottom - padding - buttonHeight,
            buttonWidth,
            buttonHeight);

        var tabCount = 2;
        var tabSpacing = compactLayout ? 8 : 12;
        var tabButtonWidth = System.Math.Min(
            160,
            System.Math.Max(100, (panel.Width - (padding * 2) - tabSpacing) / tabCount));
        var tabStartX = panel.X + padding;
        var tabY = panel.Y + (compactLayout ? 52 : 60);
        var tabBounds = new Rectangle[tabCount];
        for (var index = 0; index < tabCount; index += 1)
        {
            tabBounds[index] = new Rectangle(
                tabStartX + index * (tabButtonWidth + tabSpacing),
                tabY,
                tabButtonWidth,
                compactLayout ? 34 : 42);
        }

        var visibleRowCount = System.Math.Max(1, listBounds.Height / rowHeight);
        return new HostSetupOptionsMenuLayout
        {
            Panel = panel,
            ListBounds = listBounds,
            ConfirmBounds = confirmBounds,
            ResetBounds = resetBounds,
            TabBounds = tabBounds,
            CompactLayout = compactLayout,
            RowHeight = rowHeight,
            VisibleRowCount = visibleRowCount,
        };
    }
}
