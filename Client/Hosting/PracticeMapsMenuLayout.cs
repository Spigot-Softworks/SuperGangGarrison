#nullable enable

using Microsoft.Xna.Framework;
using System;

namespace OpenGarrison.Client;

internal readonly record struct PracticeMapsMenuLayout(
    Rectangle Panel,
    Rectangle MapsLabelBounds,
    Rectangle ColumnPanelBounds,
    Rectangle ModeFilterBounds,
    Rectangle FiltersButtonBounds,
    Rectangle ListRowsBounds,
    Rectangle MapPreviewBounds,
    Rectangle ConfirmBounds,
    Rectangle BackBounds,
    bool CompactLayout,
    int RowHeight,
    int ScrollbarWidth,
    int PanelInnerPadding)
{
    public int AvailableVisibleRowCapacity => Math.Max(1, ListRowsBounds.Height / RowHeight);

    public Rectangle GetModeFilterDropdownBounds(int optionCount)
    {
        var optionHeight = CompactLayout ? 24 : 28;
        return new Rectangle(
            ModeFilterBounds.X,
            ModeFilterBounds.Bottom + 4,
            ModeFilterBounds.Width,
            Math.Max(optionHeight, optionCount * optionHeight));
    }

    public HostSetupMapsFiltersPopupLayout GetFiltersPopupLayout()
    {
        const int gapRightOfButton = 8;
        var popupWidth = CompactLayout ? 220 : 252;
        var popupHeight = CompactLayout ? 196 : 216;
        var popupBounds = new Rectangle(
            FiltersButtonBounds.Right + gapRightOfButton,
            FiltersButtonBounds.Y,
            popupWidth,
            popupHeight);

        var padding = CompactLayout ? 10 : 12;
        var labelRowHeight = CompactLayout ? 18 : 20;
        var inputHeight = CompactLayout ? 26 : 28;
        var checkboxRowHeight = CompactLayout ? 24 : 26;
        var buttonHeight = CompactLayout ? 28 : 32;
        var buttonGap = CompactLayout ? 6 : 8;
        var contentWidth = popupWidth - (padding * 2);
        var checkboxSize = CompactLayout ? 16 : 18;
        var contentLeft = popupBounds.X + padding;

        var nameLabelY = popupBounds.Y + padding;
        var nameSearchBounds = new Rectangle(
            contentLeft,
            nameLabelY + labelRowHeight,
            contentWidth,
            inputHeight);

        var typeLabelY = nameSearchBounds.Bottom + 10;
        var customRowY = typeLabelY + labelRowHeight;
        var customCheckboxBounds = new Rectangle(contentLeft, customRowY, checkboxSize, checkboxSize);
        var customRowBounds = new Rectangle(
            contentLeft,
            customRowY,
            contentWidth,
            checkboxRowHeight);
        var baseRowY = customRowY + checkboxRowHeight + 4;
        var baseCheckboxBounds = new Rectangle(contentLeft, baseRowY, checkboxSize, checkboxSize);
        var baseRowBounds = new Rectangle(
            contentLeft,
            baseRowY,
            contentWidth,
            checkboxRowHeight);

        var buttonRowWidth = (contentWidth - buttonGap) / 2;
        var buttonY = popupBounds.Bottom - padding - buttonHeight;
        var resetBounds = new Rectangle(contentLeft, buttonY, buttonRowWidth, buttonHeight);
        var closeBounds = new Rectangle(resetBounds.Right + buttonGap, buttonY, buttonRowWidth, buttonHeight);

        return new HostSetupMapsFiltersPopupLayout(
            popupBounds,
            nameLabelY,
            nameSearchBounds,
            typeLabelY,
            customCheckboxBounds,
            customRowBounds,
            baseCheckboxBounds,
            baseRowBounds,
            resetBounds,
            closeBounds);
    }
}

internal static class PracticeMapsMenuLayoutCalculator
{
    public static PracticeMapsMenuLayout Create(int viewportWidth, int viewportHeight)
    {
        var compactViewport = viewportWidth <= 864 || viewportHeight <= 624;
        var panelWidth = compactViewport
            ? Math.Min(520, viewportWidth - 40)
            : Math.Min(560, viewportWidth - 48);
        var panelHeight = compactViewport
            ? Math.Min(520, viewportHeight - 40)
            : Math.Min(580, viewportHeight - 48);
        var panel = new Rectangle(
            (viewportWidth - panelWidth) / 2,
            (viewportHeight - panelHeight) / 2,
            panelWidth,
            panelHeight);

        return compactViewport
            ? CreateLayout(panel, compactLayout: true)
            : CreateLayout(panel, compactLayout: false);
    }

    private static PracticeMapsMenuLayout CreateLayout(Rectangle panel, bool compactLayout)
    {
        var padding = compactLayout ? 18 : 24;
        var panelInnerPadding = compactLayout ? 8 : 10;
        const int scrollbarWidth = 10;
        var rowHeight = compactLayout ? 22 : 26;
        var actionButtonHeight = compactLayout ? 36 : 42;
        var actionButtonWidth = compactLayout ? 120 : 140;
        var actionPaddingBottom = compactLayout ? 18 : 20;
        var labelHeight = compactLayout ? 18 : 20;
        var modeFilterHeight = compactLayout ? 26 : 30;
        var sectionGap = compactLayout ? 8 : 10;
        var footerHeight = compactLayout ? 72 : 84;
        var actionGap = compactLayout ? 10 : 12;

        var confirmBounds = new Rectangle(
            panel.Right - padding - actionButtonWidth,
            panel.Bottom - actionPaddingBottom - actionButtonHeight,
            actionButtonWidth,
            actionButtonHeight);
        var backBounds = new Rectangle(
            confirmBounds.X - actionGap - actionButtonWidth,
            confirmBounds.Y,
            actionButtonWidth,
            actionButtonHeight);

        var contentTop = panel.Y + (compactLayout ? 58 : 64);
        var columnLabelsY = contentTop + 4;
        var columnsTop = columnLabelsY + labelHeight + sectionGap;
        var columnsBottom = confirmBounds.Y - sectionGap - 8;
        var columnsHeight = columnsBottom - columnsTop;

        var columnPanel = new Rectangle(panel.X + padding, columnsTop, panel.Width - (padding * 2), columnsHeight);
        var mapsLabelBounds = new Rectangle(columnPanel.X, columnLabelsY, columnPanel.Width, labelHeight);

        var interiorWidth = columnPanel.Width - (panelInnerPadding * 2);
        var listAreaWidth = interiorWidth - scrollbarWidth - 6;

        const int filterBarGap = 6;
        var modeFilterWidth = Math.Max(compactLayout ? 72 : 88, (interiorWidth - filterBarGap) / 2);
        var modeFilterBounds = new Rectangle(
            columnPanel.X + panelInnerPadding,
            columnPanel.Y + panelInnerPadding,
            modeFilterWidth,
            modeFilterHeight);
        var filtersButtonBounds = new Rectangle(
            modeFilterBounds.Right + filterBarGap,
            modeFilterBounds.Y,
            interiorWidth - modeFilterWidth - filterBarGap,
            modeFilterHeight);

        var previewBounds = new Rectangle(
            columnPanel.X + panelInnerPadding,
            columnPanel.Bottom - panelInnerPadding - footerHeight,
            interiorWidth,
            footerHeight);

        var listRowsBounds = new Rectangle(
            columnPanel.X + panelInnerPadding,
            modeFilterBounds.Bottom + 6,
            listAreaWidth,
            Math.Max(40, previewBounds.Y - (modeFilterBounds.Bottom + 6)));

        return new PracticeMapsMenuLayout(
            panel,
            mapsLabelBounds,
            columnPanel,
            modeFilterBounds,
            filtersButtonBounds,
            listRowsBounds,
            previewBounds,
            confirmBounds,
            backBounds,
            compactLayout,
            rowHeight,
            scrollbarWidth,
            panelInnerPadding);
    }
}
