#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenGarrison.Client;

public partial class Game1
{
    private HostSetupMapPreviewState? _hostMapPreviewState;
    private HostSetupMapPreviewState? _hostSetupInlineMapPreview;
    private string? _hostSetupInlineMapPreviewLevelName;

    private static string GetHostSetupMapModeFilterLabel(GameModeKind? modeFilter)
    {
        return modeFilter switch
        {
            null => "All",
            _ => GetHostSetupMapModeShortLabel(modeFilter.Value),
        };
    }

    private static string GetHostSetupMapModeShortLabel(GameModeKind mode)
    {
        return mode switch
        {
            GameModeKind.Arena => "Arena",
            GameModeKind.ControlPoint => "CP",
            GameModeKind.Generator => "Gen",
            GameModeKind.KingOfTheHill => "KOTH",
            GameModeKind.DoubleKingOfTheHill => "DKOTH",
            GameModeKind.TeamDeathmatch => "TDM",
            GameModeKind.Scr => "SCR",
            GameModeKind.Vip => "VIP",
            _ => "CTF",
        };
    }

    private static IReadOnlyList<GameModeKind?> GetHostSetupMapModeFilterOptions()
    {
        return
        [
            null,
            GameModeKind.CaptureTheFlag,
            GameModeKind.Arena,
            GameModeKind.ControlPoint,
            GameModeKind.KingOfTheHill,
            GameModeKind.DoubleKingOfTheHill,
            GameModeKind.TeamDeathmatch,
            GameModeKind.Generator,
            GameModeKind.Scr,
            GameModeKind.Vip,
        ];
    }

    private void DrawHostSetupMapsScreen(HostSetupMapsMenuLayout layout, float buttonScale)
    {
        _hostSetupContentScrollOffset = 0;
        var availableMaps = _hostSetupState.GetAvailableMapsForDisplay();
        var playlistMaps = _hostSetupState.GetPlaylistMaps();
        _hostSetupState.ClampAvailableMapScroll(availableMaps.Count, layout.AvailableVisibleRowCapacity);
        _hostSetupState.ClampPlaylistMapScroll(playlistMaps.Count, layout.PlaylistVisibleRowCapacity);

        const float labelScale = 1f;
        var centerButtonScale = buttonScale * (layout.CompactLayout ? 0.88f : 0.92f);

        DrawHostSetupMapsColumnLabels(layout, labelScale);
        DrawHostSetupColumnPanelOutline(layout.AvailableColumnPanelBounds);
        DrawHostSetupColumnPanelOutline(layout.PlaylistColumnPanelBounds);
        DrawHostSetupModeFilter(layout, buttonScale);
        DrawMenuButtonScaled(
            layout.FiltersButtonBounds,
            "Filters",
            _hostSetupState.FiltersPopupOpen,
            buttonScale);
        DrawHostSetupMapListContent(
            layout.AvailableListRowsBounds,
            layout.ScrollbarWidth,
            availableMaps,
            _hostSetupState.AvailableMapScrollOffset,
            _hostSetupState.AvailableMapIndex,
            _hostSetupHoverIndex,
            layout.RowHeight,
            showMode: true,
            favouriteLevelNames: _hostSetupState.FavouriteLevelNames,
            grayIfInPlaylist: true);
        DrawHostSetupInlineMapPreview(layout.AvailableMapPreviewBounds);
        DrawHostSetupMapListContent(
            layout.PlaylistListRowsBounds,
            layout.ScrollbarWidth,
            playlistMaps,
            _hostSetupState.PlaylistMapScrollOffset,
            _hostSetupState.PlaylistMapIndex,
            _hostSetupPlaylistHoverIndex,
            layout.RowHeight,
            showMode: true,
            favouriteLevelNames: null,
            showOrderNumber: true);

        var centerTransferScale = centerButtonScale * 0.95f;
        var canAddSelectedToPlaylist = CanAddSelectedAvailableMapToPlaylist();
        DrawMenuButtonCentered(layout.AddToPlaylistBounds, ">", false, centerTransferScale, canAddSelectedToPlaylist);
        DrawMenuButtonCentered(layout.RemoveFromPlaylistBounds, "<", false, centerTransferScale);
        DrawMenuButtonScaled(layout.PlaylistUpBounds, "Up", false, buttonScale);
        DrawMenuButtonScaled(layout.PlaylistDownBounds, "Down", false, buttonScale);
        DrawMenuButtonScaled(layout.ImportBounds, "Import", false, buttonScale);
        DrawMenuButtonScaled(layout.ExportBounds, "Export", false, buttonScale);
        DrawMenuButtonScaled(layout.ConfirmBounds, "Confirm", false, buttonScale);

        if (_hostSetupState.ModeFilterDropdownOpen)
        {
            DrawHostSetupModeFilterDropdown(layout);
        }

        DrawHostSetupMapContextMenu();

        if (_hostSetupState.FiltersPopupOpen)
        {
            DrawHostSetupFiltersPopup(layout, buttonScale);
        }
        DrawHostSetupMapPreviewOverlay(layout.Panel);
    }

    private void DrawHostSetupColumnPanelOutline(Rectangle panelBounds)
    {
        DrawRoundedRectangleOutline(
            panelBounds,
            new Color(59, 51, 46),
            new Color(213, 205, 188),
            outlineThickness: 2,
            radius: 8);
    }

    private bool CanAddSelectedAvailableMapToPlaylist()
    {
        var selected = _hostSetupState.GetSelectedAvailableMap();
        return selected is not null;
    }

    private string? GetHostSetupMapsSelectedLevelName()
    {
        var available = _hostSetupState.GetSelectedAvailableMap();
        if (available is not null)
        {
            return available.LevelName;
        }

        return _hostSetupState.GetSelectedPlaylistMap()?.LevelName;
    }

    private void DrawHostSetupMapsColumnLabels(HostSetupMapsMenuLayout layout, float labelScale)
    {
        var availableHeader = layout.AvailableMapsLabelBounds;
        var playlistHeader = layout.PlaylistMapsLabelBounds;
        var availableTextY = availableHeader.Y + ((availableHeader.Height - MeasureBitmapFontHeight(labelScale)) * 0.5f);
        var playlistTextY = playlistHeader.Y + ((playlistHeader.Height - MeasureBitmapFontHeight(labelScale)) * 0.5f);
        DrawBitmapFontText("AVAILABLE MAPS", new Vector2(availableHeader.X, availableTextY), Color.White, labelScale);

        const string playlistLabel = "PLAYLIST";
        var playlistLabelWidth = MeasureBitmapFontWidth(playlistLabel, labelScale);
        DrawBitmapFontText(
            playlistLabel,
            new Vector2(playlistHeader.Right - playlistLabelWidth, playlistTextY),
            Color.White,
            labelScale);
    }

    private void DrawHostSetupModeFilter(HostSetupMapsMenuLayout layout, float buttonScale)
    {
        const float arrowColumnWidth = 24f;
        var bounds = layout.ModeFilterBounds;
        var highlighted = _hostSetupState.ModeFilterDropdownOpen;
        var fillColor = highlighted ? new Color(77, 69, 63) : new Color(54, 47, 41);
        DrawRoundedRectangleOutline(bounds, fillColor, new Color(213, 205, 188), outlineThickness: 2, radius: 8);

        var filterLabel = GetHostSetupMapModeFilterLabel(_hostSetupState.AvailableMapModeFilter);
        var textScale = buttonScale;
        var textY = bounds.Y + MathF.Max(4f, ((bounds.Height - MeasureBitmapFontHeight(textScale)) * 0.5f) - 1f);
        var trimmedLabel = TrimBitmapMenuText(filterLabel, bounds.Width - arrowColumnWidth - 20f, textScale);
        DrawBitmapFontText(trimmedLabel, new Vector2(bounds.X + 12f, textY), Color.White, textScale);
        DrawHostSetupDropdownArrow(bounds, textY, textScale);

    }

    private void DrawHostSetupModeFilterDropdown(HostSetupMapsMenuLayout layout)
    {
        var options = GetHostSetupMapModeFilterOptions();
        var optionHeight = layout.CompactLayout ? 24 : 28;
        var dropdownBounds = layout.GetModeFilterDropdownBounds(options.Count);
        _spriteBatch.Draw(_pixel, dropdownBounds, new Color(46, 40, 35, 255));
        DrawRoundedRectangleOutline(dropdownBounds, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 2, radius: 6);

        var mousePosition = GetScaledMouseState(GetConstrainedMouseState(Game1.GetCurrentMouseState())).Position;
        for (var index = 0; index < options.Count; index += 1)
        {
            var optionBounds = new Rectangle(
                dropdownBounds.X + 4,
                dropdownBounds.Y + (index * optionHeight),
                dropdownBounds.Width - 8,
                optionHeight - 2);
            var isSelected = _hostSetupState.AvailableMapModeFilter == options[index];
            var isHovered = optionBounds.Contains(mousePosition);
            var fillColor = isSelected
                ? new Color(75, 67, 62)
                : isHovered
                    ? new Color(96, 88, 82)
                    : new Color(54, 47, 41);
            _spriteBatch.Draw(_pixel, optionBounds, fillColor);
            var text = GetHostSetupMapModeFilterLabel(options[index]);
            var optionTextY = optionBounds.Y + ((optionBounds.Height - MeasureBitmapFontHeight(1f)) * 0.5f);
            DrawBitmapFontText(text, new Vector2(optionBounds.X + 8f, optionTextY), Color.White, 1f);
        }
    }

    private void DrawHostSetupFiltersPopup(HostSetupMapsMenuLayout layout, float buttonScale)
    {
        var popupLayout = layout.GetFiltersPopupLayout();
        var popupBounds = popupLayout.PopupBounds;
        _spriteBatch.Draw(_pixel, popupBounds, new Color(46, 40, 35, 250));
        DrawRoundedRectangleOutline(popupBounds, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 2, radius: 8);

        const float labelScale = 0.92f;
        DrawBitmapFontText(
            "Name",
            new Vector2(popupLayout.NameSearchBounds.X, popupLayout.NameLabelY),
            new Color(210, 210, 210),
            labelScale);
        DrawMenuInputBoxScaled(
            popupLayout.NameSearchBounds,
            _hostSetupState.AvailableMapNameFilterBuffer,
            _hostSetupEditField == HostSetupEditField.MapNameFilter,
            buttonScale,
            _hostSetupState.AvailableMapNameFilterCursorIndex,
            _hostSetupState.AvailableMapNameFilterSelectionStart);

        DrawBitmapFontText(
            "Map type",
            new Vector2(popupLayout.NameSearchBounds.X, popupLayout.TypeLabelY),
            new Color(210, 210, 210),
            labelScale);
        DrawHostSetupFilterCheckboxRow(
            popupLayout.CustomCheckboxBounds,
            popupLayout.CustomRowBounds,
            "Custom",
            _hostSetupState.IncludeCustomMaps,
            labelScale);
        DrawHostSetupFilterCheckboxRow(
            popupLayout.BaseCheckboxBounds,
            popupLayout.BaseRowBounds,
            "Base",
            _hostSetupState.IncludeBaseMaps,
            labelScale);

        DrawMenuButtonScaled(popupLayout.ResetBounds, "Reset", false, buttonScale);
        DrawMenuButtonScaled(popupLayout.CloseBounds, "Close", false, buttonScale);
    }

    private void DrawHostSetupFilterCheckboxRow(
        Rectangle checkboxBounds,
        Rectangle rowBounds,
        string label,
        bool isChecked,
        float textScale)
    {
        var fillColor = isChecked ? new Color(95, 85, 72) : new Color(54, 47, 41);
        DrawRoundedRectangleOutline(checkboxBounds, fillColor, new Color(180, 172, 158), outlineThickness: 1, radius: 3);
        if (isChecked)
        {
            const string checkMark = "x";
            var markWidth = MeasureBitmapFontWidth(checkMark, textScale);
            var markY = checkboxBounds.Y + ((checkboxBounds.Height - MeasureBitmapFontHeight(textScale)) * 0.5f);
            DrawBitmapFontText(
                checkMark,
                new Vector2(checkboxBounds.X + ((checkboxBounds.Width - markWidth) * 0.5f), markY),
                Color.White,
                textScale);
        }

        var textY = rowBounds.Y + ((rowBounds.Height - MeasureBitmapFontHeight(textScale)) * 0.5f);
        DrawBitmapFontText(label, new Vector2(checkboxBounds.Right + 10f, textY), Color.White, textScale);
    }

    private void DrawHostSetupDropdownArrow(Rectangle bounds, float textY, float textScale)
    {
        const string arrow = "^";
        var arrowWidth = MeasureBitmapFontWidth(arrow, textScale);
        var arrowHeight = MeasureBitmapFontHeight(textScale);
        var center = new Vector2(bounds.Right - 18f - (arrowWidth * 0.5f), textY + (arrowHeight * 0.5f));
        DrawBitmapFontTextCentered(arrow, center, new Color(210, 210, 210), textScale, MathF.PI);
    }

    private void DrawHostSetupMapListContent(
        Rectangle listRowsBounds,
        int scrollbarWidth,
        List<OpenGarrisonMapRotationEntry> maps,
        int scrollOffset,
        int selectedIndex,
        int hoverIndex,
        int rowHeight,
        bool showMode,
        HashSet<string>? favouriteLevelNames,
        bool showOrderNumber = false,
        bool grayIfInPlaylist = false)
    {
        _spriteBatch.Draw(_pixel, listRowsBounds, new Color(34, 30, 28, 200));

        const float rowTextScale = 1f;
        var orderColumnWidth = showOrderNumber ? MeasureBitmapFontWidth("#99", rowTextScale) + 6f : 0f;
        var namePaddingLeft = 8f + orderColumnWidth;

        var visibleRowCount = Math.Max(1, listRowsBounds.Height / rowHeight);
        var endIndex = Math.Min(maps.Count, scrollOffset + visibleRowCount);
        for (var index = scrollOffset; index < endIndex; index += 1)
        {
            var entry = maps[index];
            var visibleRow = index - scrollOffset;
            var rowBounds = new Rectangle(
                listRowsBounds.X + 4,
                listRowsBounds.Y + (visibleRow * rowHeight),
                listRowsBounds.Width - 8,
                rowHeight - 2);
            var isFavourite = favouriteLevelNames?.Contains(entry.LevelName) == true;
            var inPlaylist = grayIfInPlaylist && _hostSetupState.IsMapInPlaylist(entry.LevelName);
            var rowColor = index == selectedIndex
                ? new Color(95, 72, 68)
                : index == hoverIndex
                    ? new Color(75, 67, 62)
                    : isFavourite
                        ? new Color(64, 58, 54)
                        : new Color(54, 47, 41);
            _spriteBatch.Draw(_pixel, rowBounds, rowColor);

            var displayName = entry.IsCustomMap ? $"{entry.DisplayName} (Custom)" : entry.DisplayName;
            var rowTextY = rowBounds.Y + 4f;
            if (showOrderNumber)
            {
                var orderLabel = $"#{entry.Order}";
                DrawBitmapFontText(
                    orderLabel,
                    new Vector2(rowBounds.X + 8f, rowTextY),
                    new Color(180, 175, 165),
                    rowTextScale);
            }

            var modeReserve = showMode ? MeasureBitmapFontWidth("DKOTH", rowTextScale) + 16f : 0f;
            var nameMaxWidth = Math.Max(40f, rowBounds.Width - namePaddingLeft - 8f - modeReserve);
            var trimmedName = TrimBitmapMenuText(displayName, nameMaxWidth, rowTextScale);
            var nameColor = inPlaylist ? new Color(122, 118, 112) : Color.White;
            DrawBitmapFontText(trimmedName, new Vector2(rowBounds.X + namePaddingLeft, rowTextY), nameColor, rowTextScale);
            if (showMode)
            {
                var modeLabel = GetHostSetupMapModeShortLabel(entry.Mode);
                var modeWidth = MeasureBitmapFontWidth(modeLabel, rowTextScale);
                var modeColor = inPlaylist ? new Color(100, 96, 92) : new Color(210, 210, 210);
                DrawBitmapFontText(modeLabel, new Vector2(rowBounds.Right - modeWidth - 8f, rowTextY), modeColor, rowTextScale);
            }
        }

        if (maps.Count > visibleRowCount)
        {
            DrawHostSetupListScrollbar(listRowsBounds, scrollbarWidth, scrollOffset, maps.Count, visibleRowCount);
        }
    }

    private void DrawHostSetupInlineMapPreview(Rectangle bounds)
    {
        var levelName = GetHostSetupMapsSelectedLevelName();
        EnsureHostSetupInlineMapPreview(levelName);

        var innerBounds = new Rectangle(bounds.X + 4, bounds.Y + 4, bounds.Width - 8, bounds.Height - 8);
        DrawRoundedRectangleOutline(innerBounds, new Color(46, 40, 35), new Color(180, 172, 158), outlineThickness: 1, radius: 6);
        _spriteBatch.Draw(_pixel, innerBounds, new Color(22, 24, 28));

        if (_hostSetupInlineMapPreview is null)
        {
            const string placeholder = "Select a map";
            var textWidth = MeasureBitmapFontWidth(placeholder, 0.85f);
            var textY = innerBounds.Y + ((innerBounds.Height - MeasureBitmapFontHeight(0.85f)) * 0.5f);
            DrawBitmapFontText(
                placeholder,
                new Vector2(innerBounds.X + ((innerBounds.Width - textWidth) * 0.5f), textY),
                new Color(150, 150, 150),
                0.85f);
            return;
        }

        _hostSetupInlineMapPreview.Draw(_spriteBatch, innerBounds, interactiveView: false);
    }

    private void EnsureHostSetupInlineMapPreview(string? levelName)
    {
        if (string.IsNullOrWhiteSpace(levelName))
        {
            CloseHostSetupInlineMapPreview();
            return;
        }

        if (string.Equals(_hostSetupInlineMapPreviewLevelName, levelName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CloseHostSetupInlineMapPreview();
        var level = SimpleLevelFactory.CreateImportedLevel(levelName);
        if (level is null)
        {
            return;
        }

        _hostSetupInlineMapPreview = CreateHostSetupMapPreviewState(level);
        _hostSetupInlineMapPreviewLevelName = levelName;
    }

    private void DrawHostSetupListScrollbar(
        Rectangle listRowsBounds,
        int scrollbarWidth,
        int scrollOffset,
        int itemCount,
        int visibleRowCount)
    {
        var trackBounds = new Rectangle(listRowsBounds.Right + 4, listRowsBounds.Y, scrollbarWidth, listRowsBounds.Height);
        _spriteBatch.Draw(_pixel, trackBounds, new Color(22, 24, 28));

        var maxOffset = Math.Max(1, itemCount - visibleRowCount);
        var thumbHeight = Math.Max(24, (int)MathF.Round(trackBounds.Height * (visibleRowCount / (float)itemCount)));
        var thumbTravel = Math.Max(0, trackBounds.Height - thumbHeight);
        var thumbY = trackBounds.Y + (int)MathF.Round((scrollOffset / (float)maxOffset) * thumbTravel);
        var thumbBounds = new Rectangle(trackBounds.X, thumbY, trackBounds.Width, thumbHeight);
        _spriteBatch.Draw(_pixel, thumbBounds, new Color(105, 105, 105));
    }

    private void DrawHostSetupMapContextMenu()
    {
        var menu = _hostSetupState.MapContextMenu;
        if (menu is null)
        {
            return;
        }

        _spriteBatch.Draw(_pixel, menu.MenuBounds, new Color(46, 40, 35, 245));
        DrawRoundedRectangleOutline(menu.MenuBounds, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 1, radius: 6);
        var favouriteLabel = menu.IsFavourite ? "Unfavourite" : "Favourite";
        DrawHostSetupContextMenuRow(menu.FavouriteBounds, favouriteLabel, false);
        if (!string.IsNullOrWhiteSpace(menu.VipVariantLabel))
        {
            DrawHostSetupContextMenuRow(menu.VipVariantBounds, menu.VipVariantLabel, false);
        }

        DrawHostSetupContextMenuRow(menu.PreviewBounds, "Preview", false);
    }

    private void DrawHostSetupContextMenuRow(Rectangle bounds, string label, bool highlighted)
    {
        var fillColor = highlighted ? new Color(77, 69, 63) : new Color(54, 47, 41);
        DrawRoundedRectangleOutline(bounds, fillColor, new Color(180, 172, 158), outlineThickness: 1, radius: 4);
        var textY = bounds.Y + ((bounds.Height - MeasureBitmapFontHeight(1f)) * 0.5f);
        DrawBitmapFontText(label, new Vector2(bounds.X + 10f, textY), Color.White, 1f);
    }

    private void OpenHostSetupMapPreview(string levelName)
    {
        CloseHostSetupMapPreview();
        var level = SimpleLevelFactory.CreateImportedLevel(levelName);
        if (level is null)
        {
            _menuStatusMessage = $"Unable to preview map '{levelName}'.";
            return;
        }

        _hostMapPreviewState = CreateHostSetupMapPreviewState(level);
        _hostSetupState.PreviewMapLevelName = levelName;
        _hostMapPreviewPanActive = false;
        _hostMapPreviewViewInitialized = false;
        _menuStatusMessage = string.Empty;
    }

    public void CloseHostSetupMapPreview()
    {
        _hostMapPreviewState?.Dispose();
        _hostMapPreviewState = null;
        _hostSetupState.PreviewMapLevelName = null;
        _hostMapPreviewViewInitialized = false;
        _hostMapPreviewPanActive = false;
    }

    public void CloseAllHostSetupMapPreviews()
    {
        CloseHostSetupMapPreview();
        CloseHostSetupInlineMapPreview();
    }

    private void CloseHostSetupInlineMapPreview()
    {
        _hostSetupInlineMapPreview?.Dispose();
        _hostSetupInlineMapPreview = null;
        _hostSetupInlineMapPreviewLevelName = null;
    }

    private void DrawHostSetupMapPreviewOverlay(Rectangle hostPanel)
    {
        if (_hostMapPreviewState is null)
        {
            return;
        }

        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.72f);

        var maxWidth = Math.Min(hostPanel.Width - 80, viewportWidth - 80);
        var maxHeight = Math.Min(hostPanel.Height - 120, viewportHeight - 120);
        var previewSize = _hostMapPreviewState.GetPreviewSize(maxWidth, maxHeight);
        var previewBounds = new Rectangle(
            (viewportWidth - previewSize.X) / 2,
            (viewportHeight - previewSize.Y) / 2,
            previewSize.X,
            previewSize.Y);
        var panelBounds = new Rectangle(
            previewBounds.X - 20,
            previewBounds.Y - 36,
            previewBounds.Width + 40,
            previewBounds.Height + 72);

        DrawRoundedRectangleOutline(panelBounds, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 2, radius: 8);
        _spriteBatch.Draw(_pixel, previewBounds, new Color(22, 24, 28));
        _hostMapPreviewViewportBounds = previewBounds;
        if (!_hostMapPreviewViewInitialized)
        {
            _hostMapPreviewState.ResetView(previewBounds);
            _hostMapPreviewViewInitialized = true;
        }

        _hostMapPreviewState.Draw(_spriteBatch, previewBounds, interactiveView: true);

        const int zoomButtonSize = 24;
        const int zoomButtonGap = 4;
        var zoomRowY = previewBounds.Y + 6;
        _hostMapPreviewResetBounds = new Rectangle(previewBounds.Right - zoomButtonSize - 6, zoomRowY, zoomButtonSize, zoomButtonSize);
        _hostMapPreviewZoomOutBounds = new Rectangle(
            _hostMapPreviewResetBounds.X - zoomButtonGap - zoomButtonSize,
            zoomRowY,
            zoomButtonSize,
            zoomButtonSize);
        _hostMapPreviewZoomInBounds = new Rectangle(
            _hostMapPreviewZoomOutBounds.X - zoomButtonGap - zoomButtonSize,
            zoomRowY,
            zoomButtonSize,
            zoomButtonSize);
        DrawMenuButtonCentered(_hostMapPreviewZoomInBounds, "+", false, 1f);
        DrawMenuButtonCentered(_hostMapPreviewZoomOutBounds, "-", false, 1f);
        DrawMenuButtonCentered(_hostMapPreviewResetBounds, "[]", false, 0.9f);

        var closeWidth = Math.Clamp(previewBounds.Width / 4, 90, 140);
        _hostSetupMapPreviewCloseBounds = new Rectangle(panelBounds.Right - closeWidth - 16, panelBounds.Bottom - 42, closeWidth, 34);
        DrawMenuButtonScaled(_hostSetupMapPreviewCloseBounds, "Close", false, 1f);
    }

    private Rectangle _hostSetupMapPreviewCloseBounds;
    private Rectangle _hostMapPreviewViewportBounds;
    private Rectangle _hostMapPreviewZoomInBounds;
    private Rectangle _hostMapPreviewZoomOutBounds;
    private Rectangle _hostMapPreviewResetBounds;
    private bool _hostMapPreviewPanActive;
    private bool _hostMapPreviewViewInitialized;
    private Point _hostMapPreviewPanAnchor;

    private void UpdateHostSetupMapPreviewOverlayInput(MouseState mouse, bool clickPressed)
    {
        var previewState = _hostMapPreviewState;
        if (previewState is null)
        {
            return;
        }

        if (clickPressed)
        {
            if (_hostSetupMapPreviewCloseBounds.Contains(mouse.Position))
            {
                CloseHostSetupMapPreview();
                return;
            }

            if (_hostMapPreviewZoomInBounds.Contains(mouse.Position))
            {
                previewState.ZoomIn(_hostMapPreviewViewportBounds);
                return;
            }

            if (_hostMapPreviewZoomOutBounds.Contains(mouse.Position))
            {
                previewState.ZoomOut(_hostMapPreviewViewportBounds);
                return;
            }

            if (_hostMapPreviewResetBounds.Contains(mouse.Position))
            {
                previewState.ResetView(_hostMapPreviewViewportBounds);
                _hostMapPreviewPanActive = false;
                return;
            }

            if (_hostMapPreviewViewportBounds.Contains(mouse.Position)
                && previewState.CanPan(_hostMapPreviewViewportBounds))
            {
                _hostMapPreviewPanActive = true;
                _hostMapPreviewPanAnchor = mouse.Position;
            }

            return;
        }

        if (_hostMapPreviewPanActive)
        {
            if (mouse.LeftButton == ButtonState.Pressed)
            {
                var delta = new Vector2(
                    mouse.Position.X - _hostMapPreviewPanAnchor.X,
                    mouse.Position.Y - _hostMapPreviewPanAnchor.Y);
                previewState.Pan(delta, _hostMapPreviewViewportBounds);
                _hostMapPreviewPanAnchor = mouse.Position;
            }
            else
            {
                _hostMapPreviewPanActive = false;
            }
        }
    }

    private HostSetupMapPreviewState CreateHostSetupMapPreviewState(SimpleLevel level)
    {
        Texture2D? stockBackground = null;
        if (TryGetLevelBackgroundFileTexture(level.BackgroundAssetName, out var fileBackground))
        {
            stockBackground = fileBackground;
        }
        else if (!string.IsNullOrWhiteSpace(level.BackgroundAssetName) && _runtimeAssets is not null)
        {
            stockBackground = _runtimeAssets.GetBackground(level.BackgroundAssetName);
        }

        return HostSetupMapPreviewState.Create(this, level, _pixel, stockBackground);
    }

    private HostSetupMapContextMenuState CreateHostSetupMapContextMenu(Point position, OpenGarrisonMapRotationEntry entry, bool isPlaylistList)
    {
        const int rowHeight = 30;
        const float textScale = 1f;
        var levelName = entry.LevelName;
        var isFavourite = _hostSetupState.FavouriteLevelNames.Contains(levelName);
        var hasVipVariant = TryGetHostSetupVipVariantLevelName(entry, out var vipVariantLevelName);
        var vipVariantLabel = hasVipVariant
            ? isPlaylistList ? "Make VIP" : "Add VIP"
            : null;
        var rowCount = hasVipVariant ? 3 : 2;
        var menuWidth = (int)MathF.Ceiling(MathF.Max(
            MathF.Max(
                MeasureBitmapFontWidth("Unfavourite", textScale),
                MeasureBitmapFontWidth("Favourite", textScale)),
            MathF.Max(
                MeasureBitmapFontWidth("Preview", textScale),
                string.IsNullOrWhiteSpace(vipVariantLabel) ? 0f : MeasureBitmapFontWidth(vipVariantLabel, textScale))) + 40f);
        var menuBounds = new Rectangle(position.X, position.Y, menuWidth, (rowHeight * rowCount) - 2);
        var previewRowIndex = hasVipVariant ? 2 : 1;
        return new HostSetupMapContextMenuState
        {
            LevelName = levelName,
            Entry = entry,
            IsPlaylistList = isPlaylistList,
            IsFavourite = isFavourite,
            VipVariantLevelName = vipVariantLevelName,
            VipVariantLabel = vipVariantLabel,
            MenuBounds = menuBounds,
            FavouriteBounds = new Rectangle(menuBounds.X + 4, menuBounds.Y + 4, menuBounds.Width - 8, rowHeight - 4),
            VipVariantBounds = new Rectangle(menuBounds.X + 4, menuBounds.Y + rowHeight, menuBounds.Width - 8, rowHeight - 4),
            PreviewBounds = new Rectangle(menuBounds.X + 4, menuBounds.Y + (previewRowIndex * rowHeight), menuBounds.Width - 8, rowHeight - 4),
        };
    }

    private static bool TryGetHostSetupVipVariantLevelName(OpenGarrisonMapRotationEntry entry, out string vipLevelName)
    {
        vipLevelName = string.Empty;
        if (!OpenGarrisonStockMapCatalog.IsCpPrefixedIniKey(entry.IniKey) || entry.Mode == GameModeKind.Vip)
        {
            return false;
        }

        vipLevelName = OpenGarrisonStockMapCatalog.GetVipIniKeyForRotationEntry(entry);
        return true;
    }

    private void UpdateHostSetupMapsMenu(MouseState mouse, bool clickPressed, bool rightClickPressed, HostSetupMapsMenuLayout layout)
    {
        _hostSetupContentScrollOffset = 0;
        if (_hostMapPreviewState is not null)
        {
            UpdateHostSetupMapPreviewOverlayInput(mouse, clickPressed);
            return;
        }

        var availableMaps = _hostSetupState.GetAvailableMapsForDisplay();
        var playlistMaps = _hostSetupState.GetPlaylistMaps();
        _hostSetupState.ClampAvailableMapScroll(availableMaps.Count, layout.AvailableVisibleRowCapacity);
        _hostSetupState.ClampPlaylistMapScroll(playlistMaps.Count, layout.PlaylistVisibleRowCapacity);

        var availableScrollOffset = _hostSetupState.AvailableMapScrollOffset;
        if (TryHandleScrollbarDrag(
                mouse,
                _previousMouse,
                ScrollbarOwners.HostSetupAvailableMaps,
                GetHostSetupMapListScrollbarTrackBounds(layout.AvailableListRowsBounds, layout.ScrollbarWidth),
                ref availableScrollOffset,
                availableMaps.Count,
                layout.AvailableVisibleRowCapacity))
        {
            _hostSetupState.AvailableMapScrollOffset = availableScrollOffset;
            return;
        }

        _hostSetupState.AvailableMapScrollOffset = availableScrollOffset;

        var playlistScrollOffset = _hostSetupState.PlaylistMapScrollOffset;
        if (TryHandleScrollbarDrag(
                mouse,
                _previousMouse,
                ScrollbarOwners.HostSetupPlaylistMaps,
                GetHostSetupMapListScrollbarTrackBounds(layout.PlaylistListRowsBounds, layout.ScrollbarWidth),
                ref playlistScrollOffset,
                playlistMaps.Count,
                layout.PlaylistVisibleRowCapacity))
        {
            _hostSetupState.PlaylistMapScrollOffset = playlistScrollOffset;
            return;
        }

        _hostSetupState.PlaylistMapScrollOffset = playlistScrollOffset;

        if (ScrollbarDrag.IsActive)
        {
            return;
        }

        if (_hostSetupState.MapContextMenu is not null)
        {
            if (clickPressed)
            {
                if (_hostSetupState.MapContextMenu.FavouriteBounds.Contains(mouse.Position))
                {
                    _hostSetupState.ToggleFavourite(_hostSetupState.MapContextMenu.LevelName);
                    _hostSetupState.MapContextMenu = null;
                }
                else if (!string.IsNullOrWhiteSpace(_hostSetupState.MapContextMenu.VipVariantLevelName)
                    && _hostSetupState.MapContextMenu.VipVariantBounds.Contains(mouse.Position))
                {
                    if (_hostSetupState.MapContextMenu.IsPlaylistList
                        && _hostSetupState.MapContextMenu.Entry is not null)
                    {
                        _hostSetupState.ConvertPlaylistMapTo(
                            _hostSetupState.MapContextMenu.Entry,
                            _hostSetupState.MapContextMenu.VipVariantLevelName);
                    }
                    else
                    {
                        _hostSetupState.AddMapToPlaylist(_hostSetupState.MapContextMenu.VipVariantLevelName);
                    }

                    _hostSetupState.MapContextMenu = null;
                }
                else if (_hostSetupState.MapContextMenu.PreviewBounds.Contains(mouse.Position))
                {
                    OpenHostSetupMapPreview(_hostSetupState.MapContextMenu.LevelName);
                    _hostSetupState.MapContextMenu = null;
                }
                else
                {
                    _hostSetupState.MapContextMenu = null;
                }
            }

            return;
        }

        var wheelDelta = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        if (wheelDelta != 0)
        {
            var stepCount = Math.Max(1, Math.Abs(wheelDelta) / 120);
            if (GetHostSetupMapListInteractionBounds(layout.AvailableListRowsBounds, layout.ScrollbarWidth).Contains(mouse.Position))
            {
                _hostSetupState.AvailableMapScrollOffset = Math.Clamp(
                    _hostSetupState.AvailableMapScrollOffset + (wheelDelta > 0 ? -stepCount : stepCount),
                    0,
                    Math.Max(0, availableMaps.Count - layout.AvailableVisibleRowCapacity));
            }
            else if (GetHostSetupMapListInteractionBounds(layout.PlaylistListRowsBounds, layout.ScrollbarWidth).Contains(mouse.Position))
            {
                _hostSetupState.PlaylistMapScrollOffset = Math.Clamp(
                    _hostSetupState.PlaylistMapScrollOffset + (wheelDelta > 0 ? -stepCount : stepCount),
                    0,
                    Math.Max(0, playlistMaps.Count - layout.PlaylistVisibleRowCapacity));
            }
        }

        if (rightClickPressed)
        {
            if (TryGetHostSetupMapListHit(mouse.Position, layout, availableMaps, playlistMaps, out var hitEntry, out var isPlaylistList))
            {
                _hostSetupState.MapContextMenu = CreateHostSetupMapContextMenu(mouse.Position, hitEntry, isPlaylistList);
            }

            return;
        }

        UpdateHostSetupMapsHover(mouse, layout, availableMaps, playlistMaps);

        if (!clickPressed)
        {
            return;
        }

        if (layout.ModeFilterBounds.Contains(mouse.Position))
        {
            _hostSetupState.ModeFilterDropdownOpen = !_hostSetupState.ModeFilterDropdownOpen;
            if (_hostSetupState.ModeFilterDropdownOpen)
            {
                _hostSetupState.FiltersPopupOpen = false;
                _hostSetupEditField = HostSetupEditField.None;
            }

            return;
        }

        if (layout.FiltersButtonBounds.Contains(mouse.Position))
        {
            _hostSetupState.FiltersPopupOpen = !_hostSetupState.FiltersPopupOpen;
            _hostSetupState.ModeFilterDropdownOpen = false;
            if (_hostSetupState.FiltersPopupOpen)
            {
                _hostSetupEditField = HostSetupEditField.MapNameFilter;
                InitializeHostSetupFieldCursor(HostSetupEditField.MapNameFilter);
            }
            else
            {
                _hostSetupEditField = HostSetupEditField.None;
            }

            return;
        }

        if (_hostSetupState.FiltersPopupOpen)
        {
            if (TryHandleHostSetupFiltersPopupClick(mouse, layout))
            {
                return;
            }
        }

        if (_hostSetupState.ModeFilterDropdownOpen)
        {
            if (TryHandleHostSetupModeFilterSelection(mouse, layout))
            {
                return;
            }

            _hostSetupState.ModeFilterDropdownOpen = false;
        }

        if (layout.ConfirmBounds.Contains(mouse.Position))
        {
            CloseAllHostSetupMapPreviews();
            _hostSetupState.ConfirmMapsScreen();
            _hostSetupEditField = HostSetupEditField.ServerName;
            return;
        }

        if (layout.AddToPlaylistBounds.Contains(mouse.Position) && CanAddSelectedAvailableMapToPlaylist())
        {
            _hostSetupState.AddSelectedAvailableMapToPlaylist();
            _hostSetupState.EnsurePlaylistMapVisible(layout.PlaylistVisibleRowCapacity);
            return;
        }

        if (layout.RemoveFromPlaylistBounds.Contains(mouse.Position))
        {
            _hostSetupState.RemoveSelectedPlaylistMap();
            return;
        }

        if (layout.PlaylistUpBounds.Contains(mouse.Position))
        {
            _hostSetupState.MoveSelectedPlaylistMap(-1);
            _hostSetupState.EnsurePlaylistMapVisible(layout.PlaylistVisibleRowCapacity);
            return;
        }

        if (layout.PlaylistDownBounds.Contains(mouse.Position))
        {
            _hostSetupState.MoveSelectedPlaylistMap(1);
            _hostSetupState.EnsurePlaylistMapVisible(layout.PlaylistVisibleRowCapacity);
            return;
        }

        if (layout.ImportBounds.Contains(mouse.Position))
        {
            TryImportHostSetupPlaylist();
            return;
        }

        if (layout.ExportBounds.Contains(mouse.Position))
        {
            TryExportHostSetupPlaylist();
            return;
        }

        if (TryGetHostSetupMapListHit(mouse.Position, layout, availableMaps, playlistMaps, out _, out var hitPlaylist))
        {
            if (hitPlaylist)
            {
                _hostSetupState.SelectPlaylistMap(_hostSetupPlaylistHoverIndex);
            }
            else
            {
                _hostSetupState.SelectAvailableMap(_hostSetupHoverIndex);
            }
        }
    }

    private void UpdateHostSetupMapsHover(
        MouseState mouse,
        HostSetupMapsMenuLayout layout,
        List<OpenGarrisonMapRotationEntry> availableMaps,
        List<OpenGarrisonMapRotationEntry> playlistMaps)
    {
        _hostSetupHoverIndex = -1;
        _hostSetupPlaylistHoverIndex = -1;

        if (_hostSetupState.ModeFilterDropdownOpen)
        {
            var options = GetHostSetupMapModeFilterOptions();
            if (layout.GetModeFilterDropdownBounds(options.Count).Contains(mouse.Position))
            {
                return;
            }
        }

        if (_hostSetupState.FiltersPopupOpen)
        {
            if (layout.GetFiltersPopupLayout().PopupBounds.Contains(mouse.Position))
            {
                return;
            }
        }

        if (GetHostSetupMapListInteractionBounds(layout.AvailableListRowsBounds, layout.ScrollbarWidth).Contains(mouse.Position))
        {
            var visibleIndex = (mouse.Y - layout.AvailableListRowsBounds.Y) / layout.RowHeight;
            var hoverIndex = _hostSetupState.AvailableMapScrollOffset + visibleIndex;
            if (visibleIndex >= 0 && hoverIndex >= 0 && hoverIndex < availableMaps.Count)
            {
                _hostSetupHoverIndex = hoverIndex;
            }
        }
        else if (GetHostSetupMapListInteractionBounds(layout.PlaylistListRowsBounds, layout.ScrollbarWidth).Contains(mouse.Position))
        {
            var visibleIndex = (mouse.Y - layout.PlaylistListRowsBounds.Y) / layout.RowHeight;
            var hoverIndex = _hostSetupState.PlaylistMapScrollOffset + visibleIndex;
            if (visibleIndex >= 0 && hoverIndex >= 0 && hoverIndex < playlistMaps.Count)
            {
                _hostSetupPlaylistHoverIndex = hoverIndex;
            }
        }
    }

    private bool TryGetHostSetupMapListHit(
        Point mousePosition,
        HostSetupMapsMenuLayout layout,
        List<OpenGarrisonMapRotationEntry> availableMaps,
        List<OpenGarrisonMapRotationEntry> playlistMaps,
        out OpenGarrisonMapRotationEntry entry,
        out bool isPlaylistList)
    {
        entry = null!;
        isPlaylistList = false;
        if (GetHostSetupMapListInteractionBounds(layout.AvailableListRowsBounds, layout.ScrollbarWidth).Contains(mousePosition))
        {
            var visibleIndex = (mousePosition.Y - layout.AvailableListRowsBounds.Y) / layout.RowHeight;
            var mapIndex = _hostSetupState.AvailableMapScrollOffset + visibleIndex;
            if (visibleIndex >= 0 && mapIndex >= 0 && mapIndex < availableMaps.Count)
            {
                entry = availableMaps[mapIndex];
                _hostSetupHoverIndex = mapIndex;
                return true;
            }
        }

        if (GetHostSetupMapListInteractionBounds(layout.PlaylistListRowsBounds, layout.ScrollbarWidth).Contains(mousePosition))
        {
            var visibleIndex = (mousePosition.Y - layout.PlaylistListRowsBounds.Y) / layout.RowHeight;
            var mapIndex = _hostSetupState.PlaylistMapScrollOffset + visibleIndex;
            if (visibleIndex >= 0 && mapIndex >= 0 && mapIndex < playlistMaps.Count)
            {
                entry = playlistMaps[mapIndex];
                isPlaylistList = true;
                _hostSetupPlaylistHoverIndex = mapIndex;
                return true;
            }
        }

        return false;
    }

    private static Rectangle GetHostSetupMapListInteractionBounds(Rectangle listRowsBounds, int scrollbarWidth)
    {
        return new Rectangle(
            listRowsBounds.X,
            listRowsBounds.Y,
            listRowsBounds.Width + scrollbarWidth + 8,
            listRowsBounds.Height);
    }

    private bool TryHandleHostSetupModeFilterSelection(MouseState mouse, HostSetupMapsMenuLayout layout)
    {
        var options = GetHostSetupMapModeFilterOptions();
        var optionHeight = layout.CompactLayout ? 24 : 28;
        var dropdownBounds = layout.GetModeFilterDropdownBounds(options.Count);
        if (!dropdownBounds.Contains(mouse.Position))
        {
            return false;
        }

        var optionIndex = (mouse.Y - dropdownBounds.Y) / optionHeight;
        if (optionIndex < 0 || optionIndex >= options.Count)
        {
            return false;
        }

        _hostSetupState.AvailableMapModeFilter = options[optionIndex];
        _hostSetupState.ModeFilterDropdownOpen = false;
        _hostSetupState.NotifyAvailableFiltersChanged();
        return true;
    }

    private bool TryHandleHostSetupFiltersPopupClick(MouseState mouse, HostSetupMapsMenuLayout layout)
    {
        var popupLayout = layout.GetFiltersPopupLayout();
        if (!popupLayout.PopupBounds.Contains(mouse.Position))
        {
            return false;
        }

        if (popupLayout.NameSearchBounds.Contains(mouse.Position))
        {
            _hostSetupEditField = HostSetupEditField.MapNameFilter;
            InitializeHostSetupFieldCursor(HostSetupEditField.MapNameFilter);
            return true;
        }

        if (popupLayout.CustomRowBounds.Contains(mouse.Position))
        {
            _hostSetupState.IncludeCustomMaps = !_hostSetupState.IncludeCustomMaps;
            _hostSetupState.NotifyAvailableFiltersChanged();
            return true;
        }

        if (popupLayout.BaseRowBounds.Contains(mouse.Position))
        {
            _hostSetupState.IncludeBaseMaps = !_hostSetupState.IncludeBaseMaps;
            _hostSetupState.NotifyAvailableFiltersChanged();
            return true;
        }

        if (popupLayout.ResetBounds.Contains(mouse.Position))
        {
            _hostSetupState.ResetAvailableMapFilters();
            _hostSetupEditField = HostSetupEditField.MapNameFilter;
            InitializeHostSetupFieldCursor(HostSetupEditField.MapNameFilter);
            return true;
        }

        if (popupLayout.CloseBounds.Contains(mouse.Position))
        {
            _hostSetupState.FiltersPopupOpen = false;
            _hostSetupEditField = HostSetupEditField.None;
            return true;
        }

        return true;
    }

    private void TryImportHostSetupPlaylist()
    {
        var initialPath = HostSetupDefaultPlaylist.TryGetBundledPath(out var defaultPlaylistPath)
            ? defaultPlaylistPath
            : _hostMapRotationFileBuffer;
        if (!HostSetupFileDialogs.TryOpenPlaylistFile(initialPath, out var selectedPath))
        {
            return;
        }

        if (!_hostSetupState.TryImportPlaylist(selectedPath, out var error))
        {
            _menuStatusMessage = error;
            return;
        }

        _menuStatusMessage = "Playlist imported.";
    }

    private void TryExportHostSetupPlaylist()
    {
        if (!HostSetupFileDialogs.TrySavePlaylistFile(_hostMapRotationFileBuffer, out var selectedPath))
        {
            return;
        }

        if (!_hostSetupState.TryExportPlaylist(selectedPath, out var error))
        {
            _menuStatusMessage = error;
            return;
        }

        _menuStatusMessage = $"Playlist exported to {selectedPath}";
    }
}
