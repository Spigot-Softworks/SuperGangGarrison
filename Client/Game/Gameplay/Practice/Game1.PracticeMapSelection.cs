#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using System.Collections.Generic;

namespace OpenGarrison.Client;

public partial class Game1
{
    private int _practiceMapHoverIndex = -1;
    private PracticeEditField _practiceEditField;
    private HostSetupMapContextMenuState? _practiceMapContextMenu;

    private void DrawPracticeMapSelectionOverlay(PracticeMapsMenuLayout layout, float buttonScale)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewportWidth, ViewportHeight), Color.Black * 0.58f);

        var panel = layout.Panel;
        DrawRoundedRectangleOutline(panel, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 2, radius: 8);

        const float labelScale = 1f;
        var headerText = "PRACTICE MAPS";
        var headerX = panel.X + ((panel.Width - MeasureBitmapFontWidth(headerText, labelScale)) * 0.5f);
        DrawBitmapFontText(headerText, new Vector2(headerX, panel.Y + (layout.CompactLayout ? 16f : 20f)), Color.White, labelScale);

        var mapsHeader = layout.MapsLabelBounds;
        var mapsTextY = mapsHeader.Y + ((mapsHeader.Height - MeasureBitmapFontHeight(labelScale)) * 0.5f);
        DrawBitmapFontText("AVAILABLE MAPS", new Vector2(mapsHeader.X, mapsTextY), Color.White, labelScale);

        DrawHostSetupColumnPanelOutline(layout.ColumnPanelBounds);
        DrawPracticeMapSelectionModeFilter(layout, buttonScale);
        DrawMenuButtonScaled(
            layout.FiltersButtonBounds,
            "Filters",
            _practiceSetupState.FiltersPopupOpen,
            buttonScale);

        var availableMaps = _practiceSetupState.GetAvailableMapsForDisplay();
        DrawHostSetupMapListContent(
            layout.ListRowsBounds,
            layout.ScrollbarWidth,
            availableMaps,
            _practiceSetupState.AvailableMapScrollOffset,
            _practiceSetupState.AvailableMapIndex,
            _practiceMapHoverIndex,
            layout.RowHeight,
            showMode: true,
            favouriteLevelNames: _practiceSetupState.FavouriteLevelNames,
            grayIfInPlaylist: false);
        DrawPracticeMapSelectionInlinePreview(layout.MapPreviewBounds);

        DrawMenuButtonScaled(layout.ConfirmBounds, "Confirm", false, buttonScale);
        DrawMenuButtonScaled(layout.BackBounds, "Back", false, buttonScale);

        if (_practiceSetupState.ModeFilterDropdownOpen)
        {
            DrawPracticeMapSelectionModeFilterDropdown(layout);
        }

        DrawPracticeMapContextMenu();

        if (_practiceSetupState.FiltersPopupOpen)
        {
            DrawPracticeMapSelectionFiltersPopup(layout, buttonScale);
        }

        DrawHostSetupMapPreviewOverlay(panel);
    }

    private void DrawPracticeMapSelectionModeFilter(PracticeMapsMenuLayout layout, float buttonScale)
    {
        const float arrowColumnWidth = 24f;
        var bounds = layout.ModeFilterBounds;
        var highlighted = _practiceSetupState.ModeFilterDropdownOpen;
        var fillColor = highlighted ? new Color(77, 69, 63) : new Color(54, 47, 41);
        DrawRoundedRectangleOutline(bounds, fillColor, new Color(213, 205, 188), outlineThickness: 2, radius: 8);

        var filterLabel = GetHostSetupMapModeFilterLabel(_practiceSetupState.AvailableMapModeFilter);
        var textY = bounds.Y + MathF.Max(4f, ((bounds.Height - MeasureBitmapFontHeight(buttonScale)) * 0.5f) - 1f);
        var trimmedLabel = TrimBitmapMenuText(filterLabel, bounds.Width - arrowColumnWidth - 20f, buttonScale);
        DrawBitmapFontText(trimmedLabel, new Vector2(bounds.X + 12f, textY), Color.White, buttonScale);
        DrawHostSetupDropdownArrow(bounds, textY, buttonScale);
    }

    private void DrawPracticeMapSelectionModeFilterDropdown(PracticeMapsMenuLayout layout)
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
            var isSelected = _practiceSetupState.AvailableMapModeFilter == options[index];
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

    private void DrawPracticeMapSelectionFiltersPopup(PracticeMapsMenuLayout layout, float buttonScale)
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
            _practiceSetupState.AvailableMapNameFilterBuffer,
            _practiceEditField == PracticeEditField.MapNameFilter,
            buttonScale,
            _practiceSetupState.AvailableMapNameFilterCursorIndex,
            _practiceSetupState.AvailableMapNameFilterSelectionStart);

        DrawBitmapFontText(
            "Map type",
            new Vector2(popupLayout.NameSearchBounds.X, popupLayout.TypeLabelY),
            new Color(210, 210, 210),
            labelScale);
        DrawHostSetupFilterCheckboxRow(
            popupLayout.CustomCheckboxBounds,
            popupLayout.CustomRowBounds,
            "Custom",
            _practiceSetupState.IncludeCustomMaps,
            labelScale);
        DrawHostSetupFilterCheckboxRow(
            popupLayout.BaseCheckboxBounds,
            popupLayout.BaseRowBounds,
            "Base",
            _practiceSetupState.IncludeBaseMaps,
            labelScale);

        DrawMenuButtonScaled(popupLayout.ResetBounds, "Reset", false, buttonScale);
        DrawMenuButtonScaled(popupLayout.CloseBounds, "Close", false, buttonScale);
    }

    private void DrawPracticeMapSelectionInlinePreview(Rectangle bounds)
    {
        var selected = _practiceSetupState.GetSelectedAvailableMapEntry();
        EnsurePracticeMapBrowserPreview(selected?.LevelName);

        var innerBounds = new Rectangle(bounds.X + 4, bounds.Y + 4, bounds.Width - 8, bounds.Height - 8);
        DrawRoundedRectangleOutline(innerBounds, new Color(46, 40, 35), new Color(180, 172, 158), outlineThickness: 1, radius: 6);
        _spriteBatch.Draw(_pixel, innerBounds, new Color(22, 24, 28));

        if (_practiceMapBrowserPreviewState is null)
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

        _practiceMapBrowserPreviewState.Draw(_spriteBatch, innerBounds, interactiveView: false);
    }

    private void DrawPracticeMapContextMenu()
    {
        var menu = _practiceMapContextMenu;
        if (menu is null)
        {
            return;
        }

        _spriteBatch.Draw(_pixel, menu.MenuBounds, new Color(46, 40, 35, 245));
        DrawRoundedRectangleOutline(menu.MenuBounds, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 1, radius: 6);
        var favouriteLabel = menu.IsFavourite ? "Unfavourite" : "Favourite";
        DrawHostSetupContextMenuRow(menu.FavouriteBounds, favouriteLabel, false);
        DrawHostSetupContextMenuRow(menu.PreviewBounds, "Preview", false);
    }

    private void UpdatePracticeMapSelectionMenu(KeyboardState keyboard, MouseState mouse, PracticeMapsMenuLayout layout)
    {
        if (_hostMapPreviewState is not null)
        {
            UpdateHostSetupMapPreviewOverlayInput(mouse, mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed);
            return;
        }

        var availableMaps = _practiceSetupState.GetAvailableMapsForDisplay();
        _practiceSetupState.ClampAvailableMapScroll(availableMaps.Count, layout.AvailableVisibleRowCapacity);

        if (ScrollbarDrag.IsActive)
        {
            return;
        }

        var availableScrollOffset = _practiceSetupState.AvailableMapScrollOffset;
        if (TryHandleScrollbarDrag(
                mouse,
                _previousMouse,
                ScrollbarOwners.PracticeAvailableMaps,
                GetHostSetupMapListScrollbarTrackBounds(layout.ListRowsBounds, layout.ScrollbarWidth),
                ref availableScrollOffset,
                availableMaps.Count,
                layout.AvailableVisibleRowCapacity))
        {
            _practiceSetupState.AvailableMapScrollOffset = availableScrollOffset;
            return;
        }

        _practiceSetupState.AvailableMapScrollOffset = availableScrollOffset;

        if (_practiceMapContextMenu is not null)
        {
            var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
            if (clickPressed)
            {
                if (_practiceMapContextMenu.FavouriteBounds.Contains(mouse.Position))
                {
                    _practiceSetupState.ToggleFavourite(_practiceMapContextMenu.LevelName);
                    _practiceMapContextMenu = null;
                }
                else if (_practiceMapContextMenu.PreviewBounds.Contains(mouse.Position))
                {
                    OpenHostSetupMapPreview(_practiceMapContextMenu.LevelName);
                    _practiceMapContextMenu = null;
                }
                else
                {
                    _practiceMapContextMenu = null;
                }
            }

            return;
        }

        var wheelDelta = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        if (wheelDelta != 0)
        {
            var stepCount = Math.Max(1, Math.Abs(wheelDelta) / 120);
            if (GetPracticeMapListInteractionBounds(layout).Contains(mouse.Position))
            {
                _practiceSetupState.AvailableMapScrollOffset = Math.Clamp(
                    _practiceSetupState.AvailableMapScrollOffset + (wheelDelta > 0 ? -stepCount : stepCount),
                    0,
                    Math.Max(0, availableMaps.Count - layout.AvailableVisibleRowCapacity));
            }
        }

        var rightClickPressed = mouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton != ButtonState.Pressed;
        if (rightClickPressed)
        {
            if (TryGetPracticeMapListHit(mouse.Position, layout, availableMaps, out var hitIndex))
            {
                _practiceSetupState.SelectAvailableMap(hitIndex);
                _practiceMapContextMenu = CreatePracticeMapContextMenu(mouse.Position, availableMaps[hitIndex]);
            }

            return;
        }

        UpdatePracticeMapSelectionHover(mouse, layout, availableMaps);

        var clickPressedMain = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressedMain)
        {
            return;
        }

        if (layout.ModeFilterBounds.Contains(mouse.Position))
        {
            _practiceSetupState.ModeFilterDropdownOpen = !_practiceSetupState.ModeFilterDropdownOpen;
            if (_practiceSetupState.ModeFilterDropdownOpen)
            {
                _practiceSetupState.FiltersPopupOpen = false;
                _practiceEditField = PracticeEditField.None;
            }

            return;
        }

        if (layout.FiltersButtonBounds.Contains(mouse.Position))
        {
            _practiceSetupState.FiltersPopupOpen = !_practiceSetupState.FiltersPopupOpen;
            _practiceSetupState.ModeFilterDropdownOpen = false;
            if (_practiceSetupState.FiltersPopupOpen)
            {
                _practiceEditField = PracticeEditField.MapNameFilter;
                InitializePracticeMapNameFilterCursor();
            }
            else
            {
                _practiceEditField = PracticeEditField.None;
            }

            return;
        }

        if (_practiceSetupState.FiltersPopupOpen)
        {
            if (TryHandlePracticeMapSelectionFiltersPopupClick(mouse, layout))
            {
                return;
            }
        }

        if (_practiceSetupState.ModeFilterDropdownOpen)
        {
            if (TryHandlePracticeMapSelectionModeFilterSelection(mouse, layout))
            {
                return;
            }

            _practiceSetupState.ModeFilterDropdownOpen = false;
        }

        if (layout.ConfirmBounds.Contains(mouse.Position))
        {
            ConfirmPracticeMapBrowserSelection();
            return;
        }

        if (layout.BackBounds.Contains(mouse.Position))
        {
            ClosePracticeMapBrowser();
            return;
        }

        if (TryGetPracticeMapListHit(mouse.Position, layout, availableMaps, out var mapIndex))
        {
            _practiceSetupState.SelectAvailableMap(mapIndex);
        }
    }

    private void UpdatePracticeMapSelectionHover(MouseState mouse, PracticeMapsMenuLayout layout, List<OpenGarrisonMapRotationEntry> availableMaps)
    {
        _practiceMapHoverIndex = -1;

        if (_practiceSetupState.ModeFilterDropdownOpen)
        {
            var options = GetHostSetupMapModeFilterOptions();
            if (layout.GetModeFilterDropdownBounds(options.Count).Contains(mouse.Position))
            {
                return;
            }
        }

        if (_practiceSetupState.FiltersPopupOpen)
        {
            if (layout.GetFiltersPopupLayout().PopupBounds.Contains(mouse.Position))
            {
                return;
            }
        }

        if (GetPracticeMapListInteractionBounds(layout).Contains(mouse.Position))
        {
            var visibleIndex = (mouse.Y - layout.ListRowsBounds.Y) / layout.RowHeight;
            var hoverIndex = _practiceSetupState.AvailableMapScrollOffset + visibleIndex;
            if (visibleIndex >= 0 && hoverIndex >= 0 && hoverIndex < availableMaps.Count)
            {
                _practiceMapHoverIndex = hoverIndex;
            }
        }
    }

    private bool TryGetPracticeMapListHit(
        Point mousePosition,
        PracticeMapsMenuLayout layout,
        List<OpenGarrisonMapRotationEntry> availableMaps,
        out int mapIndex)
    {
        mapIndex = -1;
        if (!GetPracticeMapListInteractionBounds(layout).Contains(mousePosition))
        {
            return false;
        }

        var visibleIndex = (mousePosition.Y - layout.ListRowsBounds.Y) / layout.RowHeight;
        mapIndex = _practiceSetupState.AvailableMapScrollOffset + visibleIndex;
        return visibleIndex >= 0 && mapIndex >= 0 && mapIndex < availableMaps.Count;
    }

    private static Rectangle GetPracticeMapListInteractionBounds(PracticeMapsMenuLayout layout)
    {
        return new Rectangle(
            layout.ListRowsBounds.X,
            layout.ListRowsBounds.Y,
            layout.ListRowsBounds.Width + layout.ScrollbarWidth + 8,
            layout.ListRowsBounds.Height);
    }

    private bool TryHandlePracticeMapSelectionModeFilterSelection(MouseState mouse, PracticeMapsMenuLayout layout)
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

        _practiceSetupState.AvailableMapModeFilter = options[optionIndex];
        _practiceSetupState.ModeFilterDropdownOpen = false;
        _practiceSetupState.NotifyAvailableMapFiltersChanged();
        return true;
    }

    private bool TryHandlePracticeMapSelectionFiltersPopupClick(MouseState mouse, PracticeMapsMenuLayout layout)
    {
        var popupLayout = layout.GetFiltersPopupLayout();
        if (!popupLayout.PopupBounds.Contains(mouse.Position))
        {
            return false;
        }

        if (popupLayout.NameSearchBounds.Contains(mouse.Position))
        {
            _practiceEditField = PracticeEditField.MapNameFilter;
            InitializePracticeMapNameFilterCursor();
            return true;
        }

        if (popupLayout.CustomRowBounds.Contains(mouse.Position))
        {
            _practiceSetupState.IncludeCustomMaps = !_practiceSetupState.IncludeCustomMaps;
            _practiceSetupState.NotifyAvailableMapFiltersChanged();
            return true;
        }

        if (popupLayout.BaseRowBounds.Contains(mouse.Position))
        {
            _practiceSetupState.IncludeBaseMaps = !_practiceSetupState.IncludeBaseMaps;
            _practiceSetupState.NotifyAvailableMapFiltersChanged();
            return true;
        }

        if (popupLayout.ResetBounds.Contains(mouse.Position))
        {
            _practiceSetupState.ResetAvailableMapFilters();
            _practiceEditField = PracticeEditField.MapNameFilter;
            InitializePracticeMapNameFilterCursor();
            return true;
        }

        if (popupLayout.CloseBounds.Contains(mouse.Position))
        {
            _practiceSetupState.FiltersPopupOpen = false;
            _practiceEditField = PracticeEditField.None;
            return true;
        }

        return true;
    }

    private HostSetupMapContextMenuState CreatePracticeMapContextMenu(Point position, OpenGarrisonMapRotationEntry entry)
    {
        const int rowHeight = 30;
        const float textScale = 1f;
        var isFavourite = _practiceSetupState.FavouriteLevelNames.Contains(entry.LevelName);
        var menuWidth = (int)MathF.Ceiling(MathF.Max(
            MathF.Max(MeasureBitmapFontWidth("Unfavourite", textScale), MeasureBitmapFontWidth("Favourite", textScale)),
            MeasureBitmapFontWidth("Preview", textScale)) + 40f);
        var menuBounds = new Rectangle(position.X, position.Y, menuWidth, (rowHeight * 2) - 2);
        return new HostSetupMapContextMenuState
        {
            LevelName = entry.LevelName,
            Entry = null,
            IsPlaylistList = false,
            IsFavourite = isFavourite,
            MenuBounds = menuBounds,
            FavouriteBounds = new Rectangle(menuBounds.X + 4, menuBounds.Y + 4, menuBounds.Width - 8, rowHeight - 4),
            PreviewBounds = new Rectangle(menuBounds.X + 4, menuBounds.Y + rowHeight, menuBounds.Width - 8, rowHeight - 4),
        };
    }

    private void InitializePracticeMapNameFilterCursor()
    {
        _practiceSetupState.AvailableMapNameFilterCursorIndex = _practiceSetupState.AvailableMapNameFilterBuffer.Length;
        _practiceSetupState.AvailableMapNameFilterSelectionStart = _practiceSetupState.AvailableMapNameFilterCursorIndex;
    }
}
