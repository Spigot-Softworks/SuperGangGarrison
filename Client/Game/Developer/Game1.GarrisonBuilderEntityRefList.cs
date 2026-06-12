#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GarrisonBuilderMultiEntityRefPropertyDescriptor
    {
        public required string PropertyKey { get; init; }

        public required string RowLabelPrefix { get; init; }

        public required string PickPromptLabel { get; init; }
    }

    private static readonly GarrisonBuilderMultiEntityRefPropertyDescriptor[] GarrisonBuilderMultiEntityRefPropertyDescriptors =
    [
        new GarrisonBuilderMultiEntityRefPropertyDescriptor
        {
            PropertyKey = MapLogicMetadata.ActivatorEntityPropertyKey,
            RowLabelPrefix = "Entity",
            PickPromptLabel = "Pick entities on the map",
        },
    ];

    private bool _builderEntityRefListDropdownOpen;
    private string _builderEntityRefListDropdownPropertyKey = string.Empty;
    private Rectangle _builderEntityRefListDropdownAnchorBounds;
    private int _builderEntityRefListDropdownScrollIndex;
    private bool _builderMultiEntityMapPickActive;
    private string _builderMultiEntityMapPickPropertyKey = string.Empty;
    private readonly HashSet<int> _builderMultiEntityMapPickSelectedIndices = new();
    private readonly List<int> _builderMultiEntityMapPickSelectionOrder = new();
    private bool _builderMultiEntityMapPickAreaSelectDragging;
    private Point _builderMultiEntityMapPickAreaSelectStartScreen;
    private Point _builderMultiEntityMapPickAreaSelectCurrentScreen;

    private static bool IsGarrisonBuilderMultiEntityRefProperty(string key)
    {
        for (var index = 0; index < GarrisonBuilderMultiEntityRefPropertyDescriptors.Length; index += 1)
        {
            if (key.Equals(
                    GarrisonBuilderMultiEntityRefPropertyDescriptors[index].PropertyKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetGarrisonBuilderMultiEntityRefPropertyDescriptor(
        string key,
        out GarrisonBuilderMultiEntityRefPropertyDescriptor descriptor)
    {
        for (var index = 0; index < GarrisonBuilderMultiEntityRefPropertyDescriptors.Length; index += 1)
        {
            var candidate = GarrisonBuilderMultiEntityRefPropertyDescriptors[index];
            if (key.Equals(candidate.PropertyKey, StringComparison.OrdinalIgnoreCase))
            {
                descriptor = candidate;
                return true;
            }
        }

        descriptor = null!;
        return false;
    }

    private int GetGarrisonBuilderMultiEntityRefEditingEntityIndex()
    {
        return _builderPropertyTarget == GarrisonBuilderPropertyTarget.SelectedMapEntity
            ? _builderSelectedEntityIndex
            : -1;
    }

    private bool CanPickGarrisonBuilderMultiEntityRefTarget(string propertyKey, int entityIndex)
    {
        if (!TryGetGarrisonBuilderMultiEntityRefPropertyDescriptor(propertyKey, out _)
            || entityIndex < 0
            || entityIndex >= _builderEntities.Count
            || IsGarrisonBuilderEntityHidden(entityIndex))
        {
            return false;
        }

        if (propertyKey.Equals(MapLogicMetadata.ActivatorEntityPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return CanPickGarrisonBuilderActivatorMapPickTarget(entityIndex);
        }

        return false;
    }

    private static bool ShouldUseGarrisonBuilderEntityRefListDropdown(string value)
    {
        return !MapLogicEntityReferenceList.IsEmpty(value);
    }

    private string GetGarrisonBuilderEntityRefListCollapsedLabel(
        GarrisonBuilderMultiEntityRefPropertyDescriptor descriptor,
        string value)
    {
        var refs = MapLogicEntityReferenceList.Parse(value);
        if (refs.Count == 0)
        {
            return $"{descriptor.RowLabelPrefix}: none";
        }

        if (refs.Count == 1)
        {
            return $"{descriptor.RowLabelPrefix}: {GetGarrisonBuilderEntityReferenceDisplayValue(refs[0])}";
        }

        return $"{descriptor.RowLabelPrefix}: {refs.Count} targets";
    }

    private string GetGarrisonBuilderEntityRefListDropdownItemLabel(string entityRef)
    {
        return GetGarrisonBuilderEntityReferenceDisplayValue(entityRef);
    }

    private void CloseGarrisonBuilderEntityRefListDropdown()
    {
        if (ScrollbarDrag.IsOwnedBy(ScrollbarOwners.GarrisonBuilderEntityRefListDropdown))
        {
            ScrollbarDrag.Clear();
        }

        _builderEntityRefListDropdownOpen = false;
        _builderEntityRefListDropdownPropertyKey = string.Empty;
        _builderEntityRefListDropdownAnchorBounds = Rectangle.Empty;
        _builderEntityRefListDropdownScrollIndex = 0;
    }

    private void OpenGarrisonBuilderEntityRefListDropdown(string propertyKey, Rectangle anchorBounds)
    {
        _builderEntityRefListDropdownOpen = true;
        _builderEntityRefListDropdownPropertyKey = propertyKey;
        _builderEntityRefListDropdownAnchorBounds = anchorBounds;
        _builderEntityRefListDropdownScrollIndex = 0;
        CloseGarrisonBuilderEntityOverlapPicker();
    }

    private void ToggleGarrisonBuilderEntityRefListDropdown(string propertyKey, Rectangle anchorBounds)
    {
        if (_builderEntityRefListDropdownOpen
            && propertyKey.Equals(_builderEntityRefListDropdownPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            CloseGarrisonBuilderEntityRefListDropdown();
            return;
        }

        OpenGarrisonBuilderEntityRefListDropdown(propertyKey, anchorBounds);
    }

    private void BeginGarrisonBuilderMultiEntityMapPick(string propertyKey)
    {
        CloseGarrisonBuilderEntityRefListDropdown();
        CloseGarrisonBuilderEntityOverlapPicker();
        _builderMultiEntityMapPickActive = true;
        _builderMultiEntityMapPickPropertyKey = propertyKey;
        _builderMultiEntityMapPickSelectedIndices.Clear();
        _builderMultiEntityMapPickSelectionOrder.Clear();
        _builderMultiEntityMapPickAreaSelectDragging = false;
        _builderActiveTool = GarrisonBuilderTool.Select;
        _builderSelectedEntityType = string.Empty;
        _builderStatus = GetGarrisonBuilderMultiEntityMapPickStatusLabel();
    }

    private void CancelGarrisonBuilderMultiEntityMapPick()
    {
        _builderMultiEntityMapPickActive = false;
        _builderMultiEntityMapPickPropertyKey = string.Empty;
        _builderMultiEntityMapPickSelectedIndices.Clear();
        _builderMultiEntityMapPickSelectionOrder.Clear();
        _builderMultiEntityMapPickAreaSelectDragging = false;
    }

    private void CommitGarrisonBuilderMultiEntityMapPick()
    {
        if (!_builderMultiEntityMapPickActive)
        {
            return;
        }

        var propertyKey = _builderMultiEntityMapPickPropertyKey;
        if (string.IsNullOrWhiteSpace(propertyKey))
        {
            CancelGarrisonBuilderMultiEntityMapPick();
            return;
        }

        var existing = _builderPropertyEditorValues.TryGetValue(propertyKey, out var current)
            ? current
            : string.Empty;
        var newRefs = new List<string>(_builderMultiEntityMapPickSelectionOrder.Count);
        for (var index = 0; index < _builderMultiEntityMapPickSelectionOrder.Count; index += 1)
        {
            var entityIndex = _builderMultiEntityMapPickSelectionOrder[index];
            if (!IsValidGarrisonBuilderMapEntityIndex(entityIndex))
            {
                continue;
            }

            newRefs.Add(MapLogicEntityReference.FormatEntityRef(_builderEntities[entityIndex]));
        }

        _builderPropertyEditorValues[propertyKey] = MapLogicEntityReferenceList.AppendDistinct(existing, newRefs);
        ApplyGarrisonBuilderPropertyEditorLivePreview();
        MarkGarrisonBuilderPropertyEditorChanged();
        var addedCount = newRefs.Count;
        var firstAddedType = addedCount == 1 && _builderMultiEntityMapPickSelectionOrder.Count > 0
            ? _builderEntities[_builderMultiEntityMapPickSelectionOrder[0]].Type
            : string.Empty;
        CancelGarrisonBuilderMultiEntityMapPick();
        _builderStatus = addedCount switch
        {
            0 => "no targets added",
            1 => $"added target {firstAddedType}",
            _ => $"added {addedCount} targets",
        };
    }

    private void ToggleGarrisonBuilderMultiEntityMapPickSelection(int entityIndex)
    {
        if (!CanPickGarrisonBuilderMultiEntityRefTarget(_builderMultiEntityMapPickPropertyKey, entityIndex))
        {
            return;
        }

        if (_builderMultiEntityMapPickSelectedIndices.Remove(entityIndex))
        {
            _builderMultiEntityMapPickSelectionOrder.Remove(entityIndex);
        }
        else
        {
            _builderMultiEntityMapPickSelectedIndices.Add(entityIndex);
            _builderMultiEntityMapPickSelectionOrder.Add(entityIndex);
        }

        _builderStatus = _builderMultiEntityMapPickSelectedIndices.Count == 0
            ? "select entities on the map"
            : $"selected {_builderMultiEntityMapPickSelectedIndices.Count} entities";
    }

    private void AddGarrisonBuilderMultiEntityMapPickAreaSelection(IReadOnlyList<int> entityIndices)
    {
        for (var index = 0; index < entityIndices.Count; index += 1)
        {
            var entityIndex = entityIndices[index];
            if (!_builderMultiEntityMapPickSelectedIndices.Contains(entityIndex)
                && CanPickGarrisonBuilderMultiEntityRefTarget(_builderMultiEntityMapPickPropertyKey, entityIndex))
            {
                _builderMultiEntityMapPickSelectedIndices.Add(entityIndex);
                _builderMultiEntityMapPickSelectionOrder.Add(entityIndex);
            }
        }

        _builderStatus = _builderMultiEntityMapPickSelectedIndices.Count == 0
            ? "select entities on the map"
            : $"selected {_builderMultiEntityMapPickSelectedIndices.Count} entities";
    }

    private bool TryToggleGarrisonBuilderMultiEntityMapPickAtWorld(Vector2 world)
    {
        CollectGarrisonBuilderEntitiesAtWorld(world, _builderEntityOverlapPickScratch);
        for (var pickIndex = 0; pickIndex < _builderEntityOverlapPickScratch.Count; pickIndex += 1)
        {
            var entityIndex = _builderEntityOverlapPickScratch[pickIndex];
            if (!CanPickGarrisonBuilderMultiEntityRefTarget(_builderMultiEntityMapPickPropertyKey, entityIndex))
            {
                continue;
            }

            ToggleGarrisonBuilderMultiEntityMapPickSelection(entityIndex);
            return true;
        }

        return false;
    }

    private void BeginGarrisonBuilderMultiEntityMapPickAreaSelect(Point screenPosition)
    {
        _builderMultiEntityMapPickAreaSelectDragging = true;
        _builderMultiEntityMapPickAreaSelectStartScreen = screenPosition;
        _builderMultiEntityMapPickAreaSelectCurrentScreen = screenPosition;
    }

    private void UpdateGarrisonBuilderMultiEntityMapPickAreaSelect(Point screenPosition)
    {
        if (!_builderMultiEntityMapPickAreaSelectDragging)
        {
            return;
        }

        _builderMultiEntityMapPickAreaSelectCurrentScreen = screenPosition;
    }

    private void CommitGarrisonBuilderMultiEntityMapPickAreaSelect(Point screenPosition)
    {
        if (!_builderMultiEntityMapPickAreaSelectDragging)
        {
            return;
        }

        _builderMultiEntityMapPickAreaSelectCurrentScreen = screenPosition;
        _builderMultiEntityMapPickAreaSelectDragging = false;

        var screenRect = GetGarrisonBuilderMultiEntityMapPickAreaSelectScreenRectangle();
        if (screenRect.Width < GarrisonBuilderAreaSelectMinScreenSize
            && screenRect.Height < GarrisonBuilderAreaSelectMinScreenSize)
        {
            return;
        }

        CollectGarrisonBuilderEntitiesInScreenRectangle(screenRect, _builderAreaSelectScratch);
        AddGarrisonBuilderMultiEntityMapPickAreaSelection(_builderAreaSelectScratch);
    }

    private Rectangle GetGarrisonBuilderMultiEntityMapPickAreaSelectScreenRectangle()
    {
        var left = Math.Min(_builderMultiEntityMapPickAreaSelectStartScreen.X, _builderMultiEntityMapPickAreaSelectCurrentScreen.X);
        var top = Math.Min(_builderMultiEntityMapPickAreaSelectStartScreen.Y, _builderMultiEntityMapPickAreaSelectCurrentScreen.Y);
        var right = Math.Max(_builderMultiEntityMapPickAreaSelectStartScreen.X, _builderMultiEntityMapPickAreaSelectCurrentScreen.X);
        var bottom = Math.Max(_builderMultiEntityMapPickAreaSelectStartScreen.Y, _builderMultiEntityMapPickAreaSelectCurrentScreen.Y);
        return new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - bottom));
    }

    private void DrawGarrisonBuilderMultiEntityMapPickAreaSelectionRectangle()
    {
        if (!_builderMultiEntityMapPickAreaSelectDragging)
        {
            return;
        }

        var rect = GetGarrisonBuilderMultiEntityMapPickAreaSelectScreenRectangle();
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        if (_builderUseModernUi)
        {
            _spriteBatch.Draw(_pixel, rect, Color.White * GarrisonBuilderAreaSelectFillOpacity);
        }
        else
        {
            _spriteBatch.Draw(_pixel, rect, new Color(120, 180, 255, 40));
        }

        DrawGarrisonBuilderDashedRectangleOutline(rect, Color.Black * GarrisonBuilderAreaSelectOutlineOpacity);
    }

    private void DrawGarrisonBuilderMultiEntityMapPickSelectionHighlights()
    {
        if (!_builderMultiEntityMapPickActive || _builderMultiEntityMapPickSelectedIndices.Count == 0)
        {
            return;
        }

        foreach (var entityIndex in _builderMultiEntityMapPickSelectedIndices)
        {
            if (!IsValidGarrisonBuilderMapEntityIndex(entityIndex))
            {
                continue;
            }

            if (!TryGetGarrisonBuilderEntityWorldBounds(_builderEntities[entityIndex], out var left, out var top, out var width, out var height))
            {
                continue;
            }

            var topLeft = BuilderWorldToScreen(new Vector2(left, top));
            var bottomRight = BuilderWorldToScreen(new Vector2(left + width, top + height));
            var bounds = new Rectangle(
                (int)MathF.Floor(MathF.Min(topLeft.X, bottomRight.X)),
                (int)MathF.Floor(MathF.Min(topLeft.Y, bottomRight.Y)),
                Math.Max(1, (int)MathF.Ceiling(MathF.Abs(bottomRight.X - topLeft.X))),
                Math.Max(1, (int)MathF.Ceiling(MathF.Abs(bottomRight.Y - topLeft.Y))));
            DrawGarrisonBuilderSelectionMarker(bounds);
        }
    }

    private Rectangle GetGarrisonBuilderMultiEntityMapPickPromptBounds()
    {
        var strip = GetModernGarrisonBuilderLayerStripBounds();
        var height = (int)GetGarrisonBuilderMinimumButtonHeight() + BuilderUi(10);
        var width = BuilderUi(420);
        var x = (BuilderViewportWidth - width) / 2;
        var y = strip.Y - height - BuilderUi(10);
        return new Rectangle(x, y, width, height);
    }

    private Rectangle GetGarrisonBuilderMultiEntityMapPickConfirmBounds(Rectangle promptBounds)
    {
        var buttonWidth = BuilderUi(96);
        var buttonHeight = promptBounds.Height - BuilderUi(8);
        return new Rectangle(
            promptBounds.Right - buttonWidth - BuilderUi(6),
            promptBounds.Y + BuilderUi(4),
            buttonWidth,
            buttonHeight);
    }

    private string GetGarrisonBuilderMultiEntityMapPickStatusLabel()
    {
        if (TryGetGarrisonBuilderMultiEntityRefPropertyDescriptor(
                _builderMultiEntityMapPickPropertyKey,
                out var descriptor))
        {
            return descriptor.PickPromptLabel;
        }

        return "Pick entities on the map";
    }

    private void DrawGarrisonBuilderMultiEntityMapPickPrompt(MouseState mouse)
    {
        if (!_builderMultiEntityMapPickActive)
        {
            return;
        }

        var bounds = GetGarrisonBuilderMultiEntityMapPickPromptBounds();
        var confirmBounds = GetGarrisonBuilderMultiEntityMapPickConfirmBounds(bounds);
        var confirmHighlighted = confirmBounds.Contains(mouse.Position);
        DrawGarrisonBuilderBrownPanel(bounds);

        var baseLabel = GetGarrisonBuilderMultiEntityMapPickStatusLabel();
        var label = _builderMultiEntityMapPickSelectedIndices.Count == 0
            ? baseLabel
            : $"{baseLabel} ({_builderMultiEntityMapPickSelectedIndices.Count} selected)";
        var textScale = GetGarrisonBuilderBitmapFontScale();
        var textY = bounds.Y + MathF.Max(4f, ((bounds.Height - MeasureBitmapFontHeight(textScale)) * 0.5f) - 1f);
        var labelMaxWidth = confirmBounds.X - bounds.X - BuilderUi(16);
        DrawBitmapFontText(
            TruncateGarrisonBuilderPropertyLabel(label, labelMaxWidth, textScale),
            new Vector2(bounds.X + BuilderUi(12), textY),
            Color.White,
            textScale);
        DrawBuilderMenuButton(confirmBounds, "Confirm", confirmHighlighted);
    }

    private bool TryHandleGarrisonBuilderMultiEntityMapPickUiClick(Point position, bool leftClick)
    {
        if (!_builderMultiEntityMapPickActive || !leftClick)
        {
            return false;
        }

        if (GetGarrisonBuilderMultiEntityMapPickPromptBounds().Contains(position))
        {
            var confirmBounds = GetGarrisonBuilderMultiEntityMapPickConfirmBounds(GetGarrisonBuilderMultiEntityMapPickPromptBounds());
            if (confirmBounds.Contains(position))
            {
                CommitGarrisonBuilderMultiEntityMapPick();
            }

            return true;
        }

        return false;
    }

    private void UpdateGarrisonBuilderMultiEntityMapPickInteraction(MouseState mouse)
    {
        if (!_builderMultiEntityMapPickActive)
        {
            return;
        }

        var leftClick = IsLeftMouseClickPressed(mouse, _previousMouse);

        if (_builderMultiEntityMapPickAreaSelectDragging && mouse.LeftButton == ButtonState.Released
            && _previousMouse.LeftButton == ButtonState.Pressed)
        {
            CommitGarrisonBuilderMultiEntityMapPickAreaSelect(mouse.Position);
            return;
        }

        if (_builderMultiEntityMapPickAreaSelectDragging && mouse.LeftButton == ButtonState.Pressed)
        {
            UpdateGarrisonBuilderMultiEntityMapPickAreaSelect(mouse.Position);
            return;
        }

        if (!leftClick)
        {
            return;
        }

        if (_builderUseModernUi)
        {
            if (TryHandleGarrisonBuilderMultiEntityMapPickUiClick(mouse.Position, leftClick: true))
            {
                return;
            }

            if (!GetModernGarrisonBuilderMapViewport().Contains(mouse.Position))
            {
                return;
            }

            var world = BuilderScreenToWorld(mouse.Position);
            if (TryToggleGarrisonBuilderMultiEntityMapPickAtWorld(world))
            {
                return;
            }

            BeginGarrisonBuilderMultiEntityMapPickAreaSelect(mouse.Position);
            return;
        }

        var legacyWorld = mouse.Position.ToVector2() + _builderCamera;
        TryToggleGarrisonBuilderMultiEntityMapPickAtWorld(legacyWorld);
    }

    private Rectangle GetGarrisonBuilderEntityRefListDropdownClipBounds()
    {
        var editorBounds = GetGarrisonBuilderPropertyEditorBounds();
        var listBounds = GetGarrisonBuilderPropertyListBounds(editorBounds);
        var addBounds = GetGarrisonBuilderPropertyAddBounds(editorBounds);
        var top = _builderEntityRefListDropdownAnchorBounds.Bottom + 2;
        var bottom = addBounds.Y - 2;
        return new Rectangle(listBounds.X, top, listBounds.Width, Math.Max(1, bottom - top));
    }

    private static int GetGarrisonBuilderEntityRefListDropdownItemCount(IReadOnlyList<string> entityRefs)
    {
        return entityRefs.Count + 1;
    }

    private int GetGarrisonBuilderEntityRefListDropdownVisibleItemCount(Rectangle menuBounds, bool hasScrollbar)
    {
        const int padding = 4;
        const int rowGap = 2;
        var rowHeight = GetGarrisonBuilderMenuRowHeight();
        var scrollbarWidth = hasScrollbar ? GetGarrisonBuilderEntityRefListDropdownScrollbarWidth() : 0;
        var available = menuBounds.Height - (padding * 2);
        return Math.Max(1, (available + rowGap) / (rowHeight + rowGap));
    }

    private int GetGarrisonBuilderEntityRefListDropdownIdealHeight(int itemCount)
    {
        const int padding = 4;
        const int rowGap = 2;
        var rowHeight = GetGarrisonBuilderMenuRowHeight();
        return padding
            + (itemCount * rowHeight)
            + (Math.Max(0, itemCount - 1) * rowGap)
            + padding;
    }

    private int GetGarrisonBuilderEntityRefListDropdownScrollbarWidth()
    {
        return 8;
    }

    private bool TryGetGarrisonBuilderEntityRefListDropdownLayout(
        out Rectangle menuBounds,
        out IReadOnlyList<string> entityRefs,
        out int itemCount,
        out int visibleItems,
        out bool hasScrollbar,
        out Rectangle trackBounds)
    {
        menuBounds = Rectangle.Empty;
        entityRefs = Array.Empty<string>();
        itemCount = 0;
        visibleItems = 0;
        hasScrollbar = false;
        trackBounds = Rectangle.Empty;
        if (!_builderEntityRefListDropdownOpen)
        {
            return false;
        }

        if (!_builderPropertyEditorValues.TryGetValue(_builderEntityRefListDropdownPropertyKey, out var value))
        {
            value = string.Empty;
        }

        entityRefs = MapLogicEntityReferenceList.Parse(value);
        menuBounds = GetGarrisonBuilderEntityRefListDropdownBounds(
            _builderEntityRefListDropdownPropertyKey,
            _builderEntityRefListDropdownAnchorBounds,
            entityRefs);
        itemCount = GetGarrisonBuilderEntityRefListDropdownItemCount(entityRefs);
        hasScrollbar = itemCount > GetGarrisonBuilderEntityRefListDropdownVisibleItemCount(menuBounds, hasScrollbar: false);
        visibleItems = GetGarrisonBuilderEntityRefListDropdownVisibleItemCount(menuBounds, hasScrollbar);
        ClampGarrisonBuilderEntityRefListDropdownScrollIndex(entityRefs, menuBounds);
        trackBounds = GetGarrisonBuilderEntityRefListDropdownScrollbarTrackBounds(menuBounds);
        return true;
    }

    private Rectangle GetGarrisonBuilderEntityRefListDropdownScrollbarTrackBounds(Rectangle menuBounds)
    {
        const int padding = 4;
        var trackWidth = GetGarrisonBuilderEntityRefListDropdownScrollbarWidth() - 2;
        return new Rectangle(
            menuBounds.Right - trackWidth - padding,
            menuBounds.Y + padding,
            trackWidth,
            Math.Max(1, menuBounds.Height - (padding * 2)));
    }

    private bool IsGarrisonBuilderEntityRefListDropdownScrollInteractionActive()
    {
        return ScrollbarDrag.IsOwnedBy(ScrollbarOwners.GarrisonBuilderEntityRefListDropdown);
    }

    private void UpdateGarrisonBuilderEntityRefListDropdownScrollbar(MouseState mouse)
    {
        if (!TryGetGarrisonBuilderEntityRefListDropdownLayout(
                out _,
                out _,
                out var itemCount,
                out var visibleItems,
                out var hasScrollbar,
                out var trackBounds)
            || !hasScrollbar)
        {
            return;
        }

        TryHandleScrollbarDrag(
            mouse,
            _previousMouse,
            ScrollbarOwners.GarrisonBuilderEntityRefListDropdown,
            trackBounds,
            ref _builderEntityRefListDropdownScrollIndex,
            itemCount,
            visibleItems,
            minThumbHeight: 12);
    }

    private bool ShouldSuppressGarrisonBuilderMapZoomForEntityRefListDropdown(Point mousePosition)
    {
        return _builderEntityRefListDropdownOpen
            && TryGetGarrisonBuilderEntityRefListDropdownLayout(
                out var menuBounds,
                out _,
                out _,
                out _,
                out _,
                out var trackBounds)
            && (menuBounds.Contains(mousePosition) || trackBounds.Contains(mousePosition));
    }

    private void ClampGarrisonBuilderEntityRefListDropdownScrollIndex(IReadOnlyList<string> entityRefs, Rectangle menuBounds)
    {
        var itemCount = GetGarrisonBuilderEntityRefListDropdownItemCount(entityRefs);
        var hasScrollbar = itemCount > GetGarrisonBuilderEntityRefListDropdownVisibleItemCount(menuBounds, hasScrollbar: false);
        var visibleItems = GetGarrisonBuilderEntityRefListDropdownVisibleItemCount(menuBounds, hasScrollbar);
        _builderEntityRefListDropdownScrollIndex = Math.Clamp(
            _builderEntityRefListDropdownScrollIndex,
            0,
            Math.Max(0, itemCount - visibleItems));
    }

    private Rectangle GetGarrisonBuilderEntityRefListDropdownBounds(
        string propertyKey,
        Rectangle anchorBounds,
        IReadOnlyList<string> entityRefs)
    {
        _ = propertyKey;
        var clipBounds = GetGarrisonBuilderEntityRefListDropdownClipBounds();
        var itemCount = GetGarrisonBuilderEntityRefListDropdownItemCount(entityRefs);
        var idealHeight = GetGarrisonBuilderEntityRefListDropdownIdealHeight(itemCount);
        var provisionalBounds = new Rectangle(clipBounds.X, clipBounds.Y, clipBounds.Width, Math.Min(idealHeight, clipBounds.Height));
        var hasScrollbar = itemCount > GetGarrisonBuilderEntityRefListDropdownVisibleItemCount(provisionalBounds, hasScrollbar: false);
        var height = hasScrollbar
            ? clipBounds.Height
            : Math.Min(idealHeight, clipBounds.Height);
        return new Rectangle(clipBounds.X, clipBounds.Y, clipBounds.Width, Math.Max(GetGarrisonBuilderMenuRowHeight() + 8, height));
    }

    private Rectangle GetGarrisonBuilderEntityRefListDropdownItemBounds(Rectangle menuBounds, int visibleIndex, bool hasScrollbar)
    {
        var rowHeight = GetGarrisonBuilderMenuRowHeight();
        const int rowGap = 2;
        const int padding = 4;
        var scrollbarWidth = hasScrollbar ? GetGarrisonBuilderEntityRefListDropdownScrollbarWidth() : 0;
        var y = menuBounds.Y + padding + (visibleIndex * (rowHeight + rowGap));
        return new Rectangle(menuBounds.X + padding, y, menuBounds.Width - (padding * 2) - scrollbarWidth, rowHeight);
    }

    private Rectangle GetGarrisonBuilderEntityRefListDropdownRemoveBounds(Rectangle itemBounds)
    {
        var size = Math.Max(14, (int)MathF.Round(GetGarrisonBuilderMenuRowHeight() * 0.72f));
        var x = itemBounds.Right - size - 4;
        var y = itemBounds.Y + ((itemBounds.Height - size) / 2);
        return new Rectangle(x, y, size, size);
    }

    private void DrawGarrisonBuilderEntityRefListDropdownScrollbar(
        Rectangle menuBounds,
        int itemCount,
        int visibleItems,
        int scrollIndex)
    {
        if (itemCount <= visibleItems)
        {
            return;
        }

        var trackBounds = GetGarrisonBuilderEntityRefListDropdownScrollbarTrackBounds(menuBounds);
        var metrics = ScrollbarLayout.Compute(
            trackBounds,
            scrollIndex,
            itemCount,
            visibleItems,
            minThumbHeight: 12);
        _spriteBatch.Draw(_pixel, trackBounds, new Color(40, 36, 32));
        _spriteBatch.Draw(_pixel, metrics.ThumbBounds, new Color(120, 110, 98));
    }

    private bool TryScrollGarrisonBuilderEntityRefListDropdown(int wheelDelta, Point mousePosition)
    {
        if (wheelDelta == 0
            || !TryGetGarrisonBuilderEntityRefListDropdownLayout(
                out var menuBounds,
                out _,
                out var itemCount,
                out var visibleItems,
                out _,
                out var trackBounds))
        {
            return false;
        }

        if (!menuBounds.Contains(mousePosition) && !trackBounds.Contains(mousePosition))
        {
            return false;
        }

        _builderEntityRefListDropdownScrollIndex = Math.Clamp(
            _builderEntityRefListDropdownScrollIndex + (wheelDelta > 0 ? -1 : 1),
            0,
            Math.Max(0, itemCount - visibleItems));
        return true;
    }

    private void DrawGarrisonBuilderEntityRefListPropertyRow(
        Rectangle rowBounds,
        string key,
        string value,
        MouseState mouse,
        float textScale,
        bool hovered)
    {
        if (!TryGetGarrisonBuilderMultiEntityRefPropertyDescriptor(key, out var descriptor))
        {
            return;
        }

        if (_builderUseModernUi && hovered)
        {
            _spriteBatch.Draw(_pixel, rowBounds, new Color(77, 69, 63));
        }
        else if (!_builderUseModernUi)
        {
            _spriteBatch.Draw(_pixel, rowBounds, hovered ? new Color(210, 210, 210) : new Color(184, 184, 184));
        }

        var label = GetGarrisonBuilderEntityRefListCollapsedLabel(descriptor, value);
        var dropdownOpen = _builderEntityRefListDropdownOpen
            && key.Equals(_builderEntityRefListDropdownPropertyKey, StringComparison.OrdinalIgnoreCase);
        var suffix = dropdownOpen ? " v" : " >";
        var textY = rowBounds.Y + MathF.Max(2f, ((rowBounds.Height - MeasureBitmapFontHeight(textScale)) * 0.5f) - 1f);
        DrawBitmapFontText(
            TruncateGarrisonBuilderPropertyLabel(label + suffix, rowBounds.Width - 12f, textScale),
            new Vector2(rowBounds.X + 6f, textY),
            Color.White,
            textScale);
    }

    private void DrawGarrisonBuilderEntityRefListDropdown(MouseState mouse)
    {
        if (!_builderEntityRefListDropdownOpen
            || !TryGetGarrisonBuilderMultiEntityRefPropertyDescriptor(
                _builderEntityRefListDropdownPropertyKey,
                out _))
        {
            return;
        }

        if (!_builderPropertyEditorValues.TryGetValue(_builderEntityRefListDropdownPropertyKey, out var value))
        {
            value = string.Empty;
        }

        var entityRefs = MapLogicEntityReferenceList.Parse(value);
        var menuBounds = GetGarrisonBuilderEntityRefListDropdownBounds(
            _builderEntityRefListDropdownPropertyKey,
            _builderEntityRefListDropdownAnchorBounds,
            entityRefs);
        var itemCount = GetGarrisonBuilderEntityRefListDropdownItemCount(entityRefs);
        var hasScrollbar = itemCount > GetGarrisonBuilderEntityRefListDropdownVisibleItemCount(menuBounds, hasScrollbar: false);
        var visibleItems = GetGarrisonBuilderEntityRefListDropdownVisibleItemCount(menuBounds, hasScrollbar);
        ClampGarrisonBuilderEntityRefListDropdownScrollIndex(entityRefs, menuBounds);
        DrawGarrisonBuilderBrownPanel(menuBounds);
        var textScale = GetGarrisonBuilderBitmapFontScale();

        for (var visibleIndex = 0; visibleIndex < visibleItems; visibleIndex += 1)
        {
            var itemIndex = _builderEntityRefListDropdownScrollIndex + visibleIndex;
            if (itemIndex >= itemCount)
            {
                break;
            }

            var itemBounds = GetGarrisonBuilderEntityRefListDropdownItemBounds(menuBounds, visibleIndex, hasScrollbar);
            var itemHovered = itemBounds.Contains(mouse.Position);
            if (itemHovered)
            {
                _spriteBatch.Draw(_pixel, itemBounds, new Color(96, 88, 82));
            }

            if (itemIndex == 0)
            {
                DrawBitmapFontText(
                    "Add new",
                    new Vector2(itemBounds.X + 6f, itemBounds.Y + MathF.Max(2f, (itemBounds.Height - MeasureBitmapFontHeight(textScale)) * 0.5f)),
                    new Color(180, 220, 180),
                    textScale);
                continue;
            }

            var entityRef = entityRefs[itemIndex - 1];
            var itemLabel = GetGarrisonBuilderEntityRefListDropdownItemLabel(entityRef);
            DrawBitmapFontText(
                itemLabel,
                new Vector2(itemBounds.X + 6f, itemBounds.Y + MathF.Max(2f, (itemBounds.Height - MeasureBitmapFontHeight(textScale)) * 0.5f)),
                Color.White,
                textScale);

            var removeBounds = GetGarrisonBuilderEntityRefListDropdownRemoveBounds(itemBounds);
            var removeHovered = removeBounds.Contains(mouse.Position);
            DrawGarrisonBuilderPropertyClearButton(removeBounds, removeHovered);
        }

        DrawGarrisonBuilderEntityRefListDropdownScrollbar(
            menuBounds,
            itemCount,
            visibleItems,
            _builderEntityRefListDropdownScrollIndex);
    }

    private bool TryHandleGarrisonBuilderEntityRefListPropertyClick(
        Rectangle rowBounds,
        string key,
        string value,
        Point position)
    {
        if (!IsGarrisonBuilderMultiEntityRefProperty(key))
        {
            return false;
        }

        if (!ShouldUseGarrisonBuilderEntityRefListDropdown(value))
        {
            BeginGarrisonBuilderMultiEntityMapPick(key);
            return true;
        }

        if (_builderEntityRefListDropdownOpen
            && key.Equals(_builderEntityRefListDropdownPropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryHandleGarrisonBuilderEntityRefListDropdownClick(position))
        {
            return true;
        }

        ToggleGarrisonBuilderEntityRefListDropdown(key, rowBounds);
        return true;
    }

    private bool TryHandleGarrisonBuilderEntityRefListDropdownClick(Point position)
    {
        if (!_builderEntityRefListDropdownOpen || IsGarrisonBuilderEntityRefListDropdownScrollInteractionActive())
        {
            return false;
        }

        if (!TryGetGarrisonBuilderEntityRefListDropdownLayout(
                out var menuBounds,
                out var entityRefs,
                out var itemCount,
                out var visibleItems,
                out var hasScrollbar,
                out var trackBounds))
        {
            return false;
        }

        if (trackBounds.Contains(position))
        {
            return true;
        }

        if (!menuBounds.Contains(position))
        {
            CloseGarrisonBuilderEntityRefListDropdown();
            return false;
        }

        for (var visibleIndex = 0; visibleIndex < visibleItems; visibleIndex += 1)
        {
            var itemIndex = _builderEntityRefListDropdownScrollIndex + visibleIndex;
            if (itemIndex >= itemCount)
            {
                break;
            }

            var itemBounds = GetGarrisonBuilderEntityRefListDropdownItemBounds(menuBounds, visibleIndex, hasScrollbar);
            if (!itemBounds.Contains(position))
            {
                continue;
            }

            if (itemIndex == 0)
            {
                BeginGarrisonBuilderMultiEntityMapPick(_builderEntityRefListDropdownPropertyKey);
                return true;
            }

            var removeBounds = GetGarrisonBuilderEntityRefListDropdownRemoveBounds(itemBounds);
            if (removeBounds.Contains(position))
            {
                var currentValue = _builderPropertyEditorValues.TryGetValue(
                        _builderEntityRefListDropdownPropertyKey,
                        out var storedValue)
                    ? storedValue
                    : string.Empty;
                _builderPropertyEditorValues[_builderEntityRefListDropdownPropertyKey] =
                    MapLogicEntityReferenceList.RemoveAt(currentValue, itemIndex - 1);
                ApplyGarrisonBuilderPropertyEditorLivePreview();
                MarkGarrisonBuilderPropertyEditorChanged();
                if (MapLogicEntityReferenceList.IsEmpty(_builderPropertyEditorValues[_builderEntityRefListDropdownPropertyKey]))
                {
                    CloseGarrisonBuilderEntityRefListDropdown();
                }
                else
                {
                    ClampGarrisonBuilderEntityRefListDropdownScrollIndex(
                        MapLogicEntityReferenceList.Parse(_builderPropertyEditorValues[_builderEntityRefListDropdownPropertyKey]),
                        menuBounds);
                }

                _builderStatus = "target removed";
                return true;
            }

            return true;
        }

        return true;
    }

    private bool IsGarrisonBuilderEntityRefListDropdownClick(Point position)
    {
        if (!_builderEntityRefListDropdownOpen)
        {
            return false;
        }

        if (!_builderPropertyEditorValues.TryGetValue(_builderEntityRefListDropdownPropertyKey, out var value))
        {
            value = string.Empty;
        }

        var entityRefs = MapLogicEntityReferenceList.Parse(value);
        var menuBounds = GetGarrisonBuilderEntityRefListDropdownBounds(
            _builderEntityRefListDropdownPropertyKey,
            _builderEntityRefListDropdownAnchorBounds,
            entityRefs);
        return menuBounds.Contains(position);
    }

    private bool TryRefreshGarrisonBuilderMultiEntityRefValue(
        string propertyKey,
        string value,
        IReadOnlyList<(string OldRef, string NewRef, string MapEntityId)> movedUpdates,
        out string refreshedValue)
    {
        refreshedValue = value;
        if (!IsGarrisonBuilderMultiEntityRefProperty(propertyKey))
        {
            return false;
        }

        refreshedValue = MapLogicEntityReferenceList.RefreshMovedRefs(value, movedUpdates);
        return !refreshedValue.Equals(value, StringComparison.OrdinalIgnoreCase);
    }

    private bool TryUpgradeGarrisonBuilderMultiEntityRefValue(
        string propertyKey,
        string value,
        out string upgradedValue)
    {
        upgradedValue = value;
        if (!IsGarrisonBuilderMultiEntityRefProperty(propertyKey))
        {
            return false;
        }

        upgradedValue = MapLogicEntityReferenceList.UpgradeStableRefs(value, _builderEntities);
        return !upgradedValue.Equals(value, StringComparison.OrdinalIgnoreCase);
    }
}
