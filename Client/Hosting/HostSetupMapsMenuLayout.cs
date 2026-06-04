#nullable enable

using Microsoft.Xna.Framework;
using System;

namespace OpenGarrison.Client;

internal readonly record struct HostSetupMapsMenuLayout(
    Rectangle Panel,
    Rectangle AvailableMapsLabelBounds,
    Rectangle PlaylistMapsLabelBounds,
    Rectangle AvailableColumnPanelBounds,
    Rectangle PlaylistColumnPanelBounds,
    Rectangle ModeFilterBounds,
    Rectangle FiltersButtonBounds,
    Rectangle AvailableListRowsBounds,
    Rectangle AvailableMapPreviewBounds,
    Rectangle PlaylistListRowsBounds,
    Rectangle AddToPlaylistBounds,
    Rectangle RemoveFromPlaylistBounds,
    Rectangle PlaylistUpBounds,
    Rectangle PlaylistDownBounds,
    Rectangle ImportBounds,
    Rectangle ExportBounds,
    Rectangle ConfirmBounds,
    bool CompactLayout,
    int RowHeight,
    int ScrollbarWidth,
    int PanelInnerPadding)
{
    public int ContentTop => Panel.Y + (CompactLayout ? 58 : 64);

    public int FooterTop => ConfirmBounds.Y - (CompactLayout ? 16 : 20);

    public Rectangle ContentViewportBounds => new(
        Panel.X + (CompactLayout ? 12 : 20),
        ContentTop,
        Panel.Width - (CompactLayout ? 28 : 44),
        Math.Max(1, FooterTop - ContentTop));

    public int AvailableVisibleRowCapacity => Math.Max(1, AvailableListRowsBounds.Height / RowHeight);

    public int PlaylistVisibleRowCapacity => Math.Max(1, PlaylistListRowsBounds.Height / RowHeight);

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

internal readonly record struct HostSetupMapsFiltersPopupLayout(
    Rectangle PopupBounds,
    int NameLabelY,
    Rectangle NameSearchBounds,
    int TypeLabelY,
    Rectangle CustomCheckboxBounds,
    Rectangle CustomRowBounds,
    Rectangle BaseCheckboxBounds,
    Rectangle BaseRowBounds,
    Rectangle ResetBounds,
    Rectangle CloseBounds);

internal static class HostSetupMapsMenuLayoutCalculator
{
    public static HostSetupMapsMenuLayout Create(int viewportWidth, int viewportHeight, bool isServerLauncherMode)
    {
        var compactViewport = viewportWidth <= 864 || viewportHeight <= 624;
        var panelWidth = compactViewport
            ? Math.Min(760, viewportWidth - 40)
            : Math.Min(960, viewportWidth - 48);
        var panelHeight = compactViewport
            ? Math.Min(520, viewportHeight - 40)
            : Math.Min(620, viewportHeight - 48);
        var panel = new Rectangle(
            (viewportWidth - panelWidth) / 2,
            (viewportHeight - panelHeight) / 2,
            panelWidth,
            panelHeight);

        return compactViewport
            ? CreateCompact(panel, isServerLauncherMode)
            : CreateRoomy(panel, isServerLauncherMode);
    }

    private static HostSetupMapsMenuLayout CreateCompact(Rectangle panel, bool isServerLauncherMode)
    {
        const int padding = 18;
        const int panelInnerPadding = 8;
        const int scrollbarWidth = 10;
        const int columnGap = 18;
        const int centerButtonWidth = 34;
        const int centerButtonHeight = 24;
        const int rowHeight = 22;
        const int actionButtonHeight = 36;
        const int actionButtonWidth = 120;
        const int actionPaddingBottom = 18;
        const int smallButtonHeight = 26;
        const int smallButtonGap = 6;
        const int labelHeight = 18;
        const int modeFilterHeight = 26;
        const int sectionGap = 8;
        const int footerHeight = (smallButtonHeight * 2) + smallButtonGap + panelInnerPadding;

        return CreateLayout(
            panel,
            isServerLauncherMode,
            padding,
            panelInnerPadding,
            scrollbarWidth,
            columnGap,
            centerButtonWidth,
            centerButtonHeight,
            rowHeight,
            actionButtonHeight,
            actionButtonWidth,
            actionPaddingBottom,
            smallButtonHeight,
            smallButtonGap,
            labelHeight,
            modeFilterHeight,
            sectionGap,
            footerHeight,
            compactLayout: true);
    }

    private static HostSetupMapsMenuLayout CreateRoomy(Rectangle panel, bool isServerLauncherMode)
    {
        const int padding = 36;
        const int panelInnerPadding = 10;
        const int scrollbarWidth = 10;
        const int columnGap = 22;
        const int centerButtonWidth = 40;
        const int centerButtonHeight = 28;
        const int rowHeight = 26;
        const int actionButtonHeight = 42;
        const int actionButtonWidth = 140;
        const int actionPaddingBottom = 20;
        const int smallButtonHeight = 30;
        const int smallButtonGap = 8;
        const int labelHeight = 20;
        const int modeFilterHeight = 30;
        const int sectionGap = 10;
        const int footerHeight = (smallButtonHeight * 2) + smallButtonGap + panelInnerPadding;

        return CreateLayout(
            panel,
            isServerLauncherMode,
            padding,
            panelInnerPadding,
            scrollbarWidth,
            columnGap,
            centerButtonWidth,
            centerButtonHeight,
            rowHeight,
            actionButtonHeight,
            actionButtonWidth,
            actionPaddingBottom,
            smallButtonHeight,
            smallButtonGap,
            labelHeight,
            modeFilterHeight,
            sectionGap,
            footerHeight,
            compactLayout: false);
    }

    private static HostSetupMapsMenuLayout CreateLayout(
        Rectangle panel,
        bool isServerLauncherMode,
        int padding,
        int panelInnerPadding,
        int scrollbarWidth,
        int columnGap,
        int centerButtonWidth,
        int centerButtonHeight,
        int rowHeight,
        int actionButtonHeight,
        int actionButtonWidth,
        int actionPaddingBottom,
        int smallButtonHeight,
        int smallButtonGap,
        int labelHeight,
        int modeFilterHeight,
        int sectionGap,
        int footerHeight,
        bool compactLayout)
    {
        var confirmBounds = new Rectangle(
            panel.Right - padding - actionButtonWidth,
            panel.Bottom - actionPaddingBottom - actionButtonHeight,
            actionButtonWidth,
            actionButtonHeight);

        var contentTop = panel.Y + (isServerLauncherMode ? (compactLayout ? 102 : 106) : (compactLayout ? 68 : 74));
        var columnLabelsY = contentTop + 4;
        var columnsTop = columnLabelsY + labelHeight + sectionGap;
        var columnsBottom = confirmBounds.Y - sectionGap - 8;
        var columnsHeight = columnsBottom - columnsTop;

        var contentWidth = panel.Width - (padding * 2) - 20;
        var centerBlockWidth = centerButtonWidth + (columnGap * 2);
        var sideWidth = Math.Max(compactLayout ? 180 : 220, (contentWidth - centerBlockWidth) / 2);
        var leftX = panel.X + padding;
        var leftPanel = new Rectangle(leftX, columnsTop, sideWidth, columnsHeight);
        var rightPanel = new Rectangle(leftPanel.Right + centerBlockWidth, columnsTop, sideWidth, columnsHeight);

        var availableMapsLabelBounds = new Rectangle(leftX, columnLabelsY, sideWidth, labelHeight);
        var playlistMapsLabelBounds = new Rectangle(rightPanel.X, columnLabelsY, sideWidth, labelHeight);

        var interiorWidth = sideWidth - (panelInnerPadding * 2);
        var listAreaWidth = interiorWidth - scrollbarWidth - 6;

        const int filterBarGap = 6;
        var modeFilterWidth = Math.Max(compactLayout ? 72 : 88, (interiorWidth - filterBarGap) / 2);
        var modeFilterBounds = new Rectangle(
            leftPanel.X + panelInnerPadding,
            leftPanel.Y + panelInnerPadding,
            modeFilterWidth,
            modeFilterHeight);
        var filtersButtonBounds = new Rectangle(
            modeFilterBounds.Right + filterBarGap,
            modeFilterBounds.Y,
            interiorWidth - modeFilterWidth - filterBarGap,
            modeFilterHeight);

        var availablePreviewBounds = new Rectangle(
            leftPanel.X + panelInnerPadding,
            leftPanel.Bottom - panelInnerPadding - footerHeight,
            interiorWidth,
            footerHeight);

        var availableListRowsBounds = new Rectangle(
            leftPanel.X + panelInnerPadding,
            modeFilterBounds.Bottom + 6,
            listAreaWidth,
            Math.Max(40, availablePreviewBounds.Y - (modeFilterBounds.Bottom + 6)));

        var playlistFooterTop = rightPanel.Bottom - panelInnerPadding - footerHeight;
        var playlistListRowsBounds = new Rectangle(
            rightPanel.X + panelInnerPadding,
            rightPanel.Y + panelInnerPadding,
            listAreaWidth,
            Math.Max(40, playlistFooterTop - (rightPanel.Y + panelInnerPadding)));

        var playlistButtonRowWidth = Math.Max(compactLayout ? 70 : 90, (interiorWidth - smallButtonGap) / 2);
        var playlistUpBounds = new Rectangle(
            rightPanel.X + panelInnerPadding,
            playlistFooterTop + 4,
            playlistButtonRowWidth,
            smallButtonHeight);
        var playlistDownBounds = new Rectangle(
            playlistUpBounds.Right + smallButtonGap,
            playlistUpBounds.Y,
            interiorWidth - playlistButtonRowWidth - smallButtonGap,
            smallButtonHeight);
        var importBounds = new Rectangle(
            rightPanel.X + panelInnerPadding,
            playlistUpBounds.Bottom + smallButtonGap,
            playlistButtonRowWidth,
            smallButtonHeight);
        var exportBounds = new Rectangle(
            importBounds.Right + smallButtonGap,
            importBounds.Y,
            interiorWidth - playlistButtonRowWidth - smallButtonGap,
            smallButtonHeight);

        var centerX = leftPanel.Right + columnGap;
        var centerBlockHeight = (centerButtonHeight * 2) + smallButtonGap;
        var centerY = columnsTop + Math.Max(0, (columnsHeight - centerBlockHeight) / 2);
        var addToPlaylistBounds = new Rectangle(centerX, centerY, centerButtonWidth, centerButtonHeight);
        var removeFromPlaylistBounds = new Rectangle(
            centerX,
            addToPlaylistBounds.Bottom + smallButtonGap,
            centerButtonWidth,
            centerButtonHeight);

        return new HostSetupMapsMenuLayout(
            panel,
            availableMapsLabelBounds,
            playlistMapsLabelBounds,
            leftPanel,
            rightPanel,
            modeFilterBounds,
            filtersButtonBounds,
            availableListRowsBounds,
            availablePreviewBounds,
            playlistListRowsBounds,
            addToPlaylistBounds,
            removeFromPlaylistBounds,
            playlistUpBounds,
            playlistDownBounds,
            importBounds,
            exportBounds,
            confirmBounds,
            compactLayout,
            rowHeight,
            scrollbarWidth,
            panelInnerPadding);
    }
}
