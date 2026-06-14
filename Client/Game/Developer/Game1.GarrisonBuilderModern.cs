#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private enum GarrisonBuilderTool
    {
        Select,
        Place,
        Erase,
    }

    private enum GarrisonBuilderResizeHandle
    {
        None,
        TopLeft,
        Top,
        TopRight,
        Right,
        BottomRight,
        Bottom,
        BottomLeft,
        Left,
    }

    private const int ModernBuilderExpandedSidebarWidth = 248;
    private const int ModernBuilderCollapsedPaletteWidth = 22;
    private const int ModernBuilderLayerStripExpandedHeight = 88;
    private const int ModernBuilderLayerStripCollapsedHeight = 22;
    private const int ModernBuilderLayerTabWidth = 68;
    private const int ModernBuilderLayerTabHeight = 44;
    private const int ModernBuilderLayerMarkButtonWidth = 56;
    private const int ModernBuilderMenuTabWidth = 56;

    private enum GarrisonBuilderMenuBarMenu
    {
        None,
        File,
        Edit,
        View,
        Map,
    }

    private GarrisonBuilderMenuBarMenu _builderMenuBarOpenMenu = GarrisonBuilderMenuBarMenu.None;
    private const float ModernBuilderMinZoom = 0.05f;
    private const float ModernBuilderMaxZoom = 8f;

    private bool _builderUseModernUi = true;
    private bool _builderEntityPaletteVisible = true;
    private GarrisonBuilderTool _builderActiveTool = GarrisonBuilderTool.Select;
    private float _builderZoom = 1f;
    private int _builderSelectedEntityIndex = -1;
    private bool _builderMapPanDragging;
    private Point _builderMapPanAnchorMouse;
    private Vector2 _builderMapPanAnchorCamera;
    private GarrisonBuilderResizeHandle _builderActiveResizeHandle = GarrisonBuilderResizeHandle.None;
    private Vector2 _builderResizeAnchorWorld;
    private float _builderResizeStartXScale = 1f;
    private float _builderResizeStartYScale = 1f;
    private float _builderResizeStartWidth;
    private float _builderResizeStartHeight;
    private float _builderResizeStartLeft;
    private float _builderResizeStartTop;
    private float _builderResizeOriginX;
    private float _builderResizeOriginY;
    private bool _builderEntityDragging;
    private Vector2 _builderEntityDragPointerOffsetWorld;
    private enum GarrisonBuilderEntityPaletteCategory
    {
        Gameplay,
        Logic,
    }

    private const int ModernBuilderPaletteScrollbarWidth = 8;
    private const int ModernBuilderPaletteCategorySpacing = 4;
    private const int ModernBuilderPaletteItemSpacing = 2;

    private static readonly (GarrisonBuilderEntityPaletteCategory Category, string Label)[] EntityPaletteCategories =
    [
        (GarrisonBuilderEntityPaletteCategory.Gameplay, "Gameplay"),
        (GarrisonBuilderEntityPaletteCategory.Logic, "Logic"),
    ];

    private readonly Dictionary<GarrisonBuilderEntityPaletteCategory, bool> _builderEntityPaletteCategoryExpanded = new()
    {
        [GarrisonBuilderEntityPaletteCategory.Gameplay] = false,
        [GarrisonBuilderEntityPaletteCategory.Logic] = false,
    };

    private int _builderEntityPaletteScrollOffset;
    private GarrisonBuilderEntityPaletteCategory? _builderPaletteHoverCategory;
    private int _builderPaletteHoverEntityIndex = -1;
    private int _builderPaletteHoverLogicEntityIndex = -1;
    private bool _builderEntityContextMenuOpen;
    private Point _builderEntityContextMenuPosition;
    private bool _builderEntityOverlapPickerOpen;
    private Point _builderEntityOverlapPickerPosition;
    private readonly List<int> _builderEntityOverlapPickerIndices = new();
    private bool _builderLayerStripExpanded = true;
    private bool _builderLayerContextMenuOpen;
    private Point _builderLayerContextMenuPosition;
    private int _builderLayerContextMenuLayerIndex = -1;

    private enum GarrisonBuilderLayerContextAction
    {
        Load,
        ToggleHide,
        Parallax,
    }

    private enum GarrisonBuilderEntityContextAction
    {
        Properties,
        Recolor,
        Clone,
        Remove,
    }

    private readonly List<GarrisonBuilderEntityContextAction> _builderEntityContextMenuActionsScratch = new();

    private int GetModernBuilderMenuBarHeight()
    {
        return Math.Max(28, GetGarrisonBuilderMenuRowHeight() + 8);
    }

    private int GetModernBuilderToolbarHeight()
    {
        return Math.Max(40, GetGarrisonBuilderMenuRowHeight() + 16);
    }

    private int GetModernGarrisonBuilderLayerStripHeight()
    {
        return _builderLayerStripExpanded
            ? ModernBuilderLayerStripExpandedHeight
            : ModernBuilderLayerStripCollapsedHeight;
    }

    private int GetModernGarrisonBuilderChromeTop()
    {
        return GetModernBuilderMenuBarHeight() + GetModernBuilderToolbarHeight();
    }

    private int GetModernGarrisonBuilderSidebarWidth()
    {
        return _builderEntityPaletteVisible
            ? ModernBuilderExpandedSidebarWidth
            : ModernBuilderCollapsedPaletteWidth;
    }

    private Rectangle GetModernGarrisonBuilderMapViewport()
    {
        var sidebarWidth = GetModernGarrisonBuilderSidebarWidth();
        return new Rectangle(
            sidebarWidth,
            GetModernGarrisonBuilderChromeTop(),
            Math.Max(1, BuilderViewportWidth - sidebarWidth),
            Math.Max(1, BuilderViewportHeight - GetModernGarrisonBuilderChromeTop() - GetModernGarrisonBuilderLayerStripHeight()));
    }

    private float GetGarrisonBuilderEntityCoordinateScale()
    {
        return _builderEntityCoordinatesAreWalkmaskPixels
            ? MathF.Max(0.001f, _builderDocument.Scale)
            : 1f;
    }

    private float GetGarrisonBuilderMapVisualScale()
    {
        return GetGarrisonBuilderEntityCoordinateScale() * (_builderUseModernUi ? _builderZoom : 1f);
    }

    private Vector2 BuilderWorldToDisplay(Vector2 world)
    {
        return world * GetGarrisonBuilderEntityCoordinateScale();
    }

    private Vector2 BuilderDisplayToWorld(Vector2 display)
    {
        return display / GetGarrisonBuilderEntityCoordinateScale();
    }

    private Vector2 BuilderWorldToScreen(Vector2 world)
    {
        if (!_builderUseModernUi)
        {
            return world - _builderCamera;
        }

        var viewport = GetModernGarrisonBuilderMapViewport();
        var display = BuilderWorldToDisplay(world);
        return viewport.Location.ToVector2() + ((display - _builderCamera) * _builderZoom);
    }

    private Vector2 BuilderScreenToWorld(Vector2 screen)
    {
        var viewport = GetModernGarrisonBuilderMapViewport();
        var local = screen - viewport.Location.ToVector2();
        var display = _builderCamera + (local / _builderZoom);
        return BuilderDisplayToWorld(display);
    }

    private Vector2 BuilderScreenToWorld(Point screen)
    {
        return BuilderScreenToWorld(screen.ToVector2());
    }

    private void UpdateModernGarrisonBuilderEditor(KeyboardState keyboard, MouseState mouse, float deltaSeconds)
    {
        if (IsKeyPressed(keyboard, Keys.V))
        {
            _builderActiveTool = GarrisonBuilderTool.Select;
            _builderSelectedEntityType = string.Empty;
            _builderStatus = "select tool";
        }

        if (IsKeyPressed(keyboard, Keys.P))
        {
            _builderActiveTool = GarrisonBuilderTool.Place;
            _builderStatus = "place tool";
        }

        if (IsKeyPressed(keyboard, Keys.E) && !keyboard.IsKeyDown(Keys.LeftControl) && !keyboard.IsKeyDown(Keys.RightControl))
        {
            _builderActiveTool = GarrisonBuilderTool.Erase;
            _builderSelectedEntityType = string.Empty;
            _builderStatus = "erase tool";
        }

        if (IsKeyPressed(keyboard, Keys.G))
        {
            _builderShowGrid = !_builderShowGrid;
        }

        if (IsKeyPressed(keyboard, Keys.B))
        {
            _builderShowBackground = !_builderShowBackground;
        }

        if (IsKeyPressed(keyboard, Keys.M))
        {
            _builderShowWalkmask = !_builderShowWalkmask;
        }

        if (IsKeyPressed(keyboard, Keys.Tab))
        {
            ToggleGarrisonBuilderEntityPalette();
        }

        if (IsKeyPressed(keyboard, Keys.F7))
        {
            SaveGarrisonBuilderDocument();
        }

        if (_builderMultiEntityMapPickActive && IsKeyPressed(keyboard, Keys.Enter))
        {
            CommitGarrisonBuilderMultiEntityMapPick();
            return;
        }

        if (IsKeyPressed(keyboard, Keys.Escape))
        {
            if (_builderEntityOverlapPickerOpen)
            {
                CloseGarrisonBuilderEntityOverlapPicker();
                return;
            }

            if (_builderEntityContextMenuOpen)
            {
                CloseGarrisonBuilderEntityContextMenu();
                return;
            }

            if (_builderLayerContextMenuOpen)
            {
                CloseGarrisonBuilderLayerContextMenu();
                return;
            }

            if (_builderLayerParallaxDialogOpen)
            {
                CloseGarrisonBuilderLayerParallaxDialog(applyChanges: false);
                return;
            }

            if (_builderMapNameCollisionDialogOpen)
            {
                CloseGarrisonBuilderMapNameCollisionDialog(saveAs: false);
                return;
            }

            if (_builderLogicRecolorDialogOpen)
            {
                CloseGarrisonBuilderLogicRecolorDialog();
                return;
            }

            if (_builderObjectiveMapPickActive)
            {
                CancelGarrisonBuilderObjectiveMapPick();
                return;
            }

            if (_builderLogicMapPickActive)
            {
                CancelGarrisonBuilderLogicMapPick();
                return;
            }

            if (_builderEntityMapPickActive)
            {
                CancelGarrisonBuilderEntityMapPick();
                return;
            }

            if (_builderMultiEntityMapPickActive)
            {
                CancelGarrisonBuilderMultiEntityMapPick();
                return;
            }

            if (_builderEntityRefListDropdownOpen)
            {
                CloseGarrisonBuilderEntityRefListDropdown();
                return;
            }

            if (_builderPropertyTarget != GarrisonBuilderPropertyTarget.None)
            {
                CloseGarrisonBuilderPropertyEditor(applyChanges: true);
                return;
            }

            if (_builderMenuBarOpenMenu != GarrisonBuilderMenuBarMenu.None)
            {
                _builderMenuBarOpenMenu = GarrisonBuilderMenuBarMenu.None;
                _builderStatus = "builder";
            }
            else if (_builderGameModeMenuOpen)
            {
                _builderGameModeMenuOpen = false;
                _builderStatus = "builder";
            }
            else
            {
                _builderGameModeMenuOpen = true;
                _builderStatus = "builder menu";
            }

            return;
        }

        if (IsKeyPressed(keyboard, Keys.H))
        {
            if (_builderCtrlHeld)
            {
                UnhideAllGarrisonBuilderEntities();
            }
            else
            {
                HideGarrisonBuilderSelectedEntity();
            }
        }

        if (IsKeyPressed(keyboard, Keys.Delete))
        {
            if (GetGarrisonBuilderSelectedEntityCount() > 0 || _builderSelectedEntityIndex >= 0)
            {
                RemoveGarrisonBuilderSelectedEntities();
            }
            else if (_builderEntities.Count > 0)
            {
                var removedIndex = _builderEntities.Count - 1;
                NotifyGarrisonBuilderEntityRemoved(removedIndex);
                _builderEntities.RemoveAt(removedIndex);
                UpdateGarrisonBuilderDocumentEntities();
                _builderDirty = true;
                _builderStatus = "removed latest entity";
            }
        }

        UpdateGarrisonBuilderPlacementPreview(mouse);
        UpdateModernGarrisonBuilderPaletteScroll(mouse);
        UpdateModernGarrisonBuilderPaletteHover(mouse);
        UpdateGarrisonBuilderMapEntityHover(mouse);
        UpdateModernGarrisonBuilderMapInteraction(mouse);
    }

    private void SyncGarrisonBuilderActiveTool()
    {
        if (_builderActiveTool == GarrisonBuilderTool.Erase)
        {
            return;
        }

        _builderActiveTool = string.IsNullOrWhiteSpace(_builderSelectedEntityType)
            ? GarrisonBuilderTool.Select
            : GarrisonBuilderTool.Place;
    }

    private void UpdateModernGarrisonBuilderCamera(KeyboardState keyboard, MouseState mouse, float deltaSeconds)
    {
        var mapViewport = GetModernGarrisonBuilderMapViewport();
        var middleDown = mouse.MiddleButton == ButtonState.Pressed;
        if (middleDown
            && _previousMouse.MiddleButton == ButtonState.Released
            && mapViewport.Contains(mouse.Position))
        {
            _builderMapPanDragging = true;
            _builderMapPanAnchorMouse = mouse.Position;
            _builderMapPanAnchorCamera = _builderCamera;
        }

        if (_builderMapPanDragging)
        {
            if (middleDown)
            {
                var delta = mouse.Position - _builderMapPanAnchorMouse;
                _builderCamera = _builderMapPanAnchorCamera - (delta.ToVector2() / _builderZoom);
            }
            else
            {
                _builderMapPanDragging = false;
            }
        }

        var moveSpeed = (_builderFastScrolling ? 760f : 380f) / _builderZoom;
        if (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift))
        {
            moveSpeed *= 1.6f;
        }

        var deltaMove = moveSpeed * Math.Max(0.001f, deltaSeconds);
        if (keyboard.IsKeyDown(Keys.A) || keyboard.IsKeyDown(Keys.Left))
        {
            _builderCamera.X -= deltaMove;
        }

        if (keyboard.IsKeyDown(Keys.D) || keyboard.IsKeyDown(Keys.Right))
        {
            _builderCamera.X += deltaMove;
        }

        if (keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.Up))
        {
            _builderCamera.Y -= deltaMove;
        }

        if (keyboard.IsKeyDown(Keys.S) || keyboard.IsKeyDown(Keys.Down))
        {
            _builderCamera.Y += deltaMove;
        }

    }

    private void UpdateModernGarrisonBuilderZoom(MouseState mouse, KeyboardState keyboard)
    {
        var wheelDelta = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        if (wheelDelta == 0)
        {
            return;
        }

        if (_builderPropertyTarget != GarrisonBuilderPropertyTarget.None
            && GetGarrisonBuilderPropertyEditorBounds().Contains(mouse.Position))
        {
            return;
        }

        if (ShouldSuppressGarrisonBuilderMapZoomForEntityRefListDropdown(mouse.Position))
        {
            return;
        }

        var mapViewport = GetModernGarrisonBuilderMapViewport();
        if (!mapViewport.Contains(mouse.Position))
        {
            return;
        }

        var direction = wheelDelta > 0 ? 1.1f : 0.9f;
        var previousZoom = _builderZoom;
        _builderZoom = Math.Clamp(_builderZoom * direction, ModernBuilderMinZoom, ModernBuilderMaxZoom);
        var local = mouse.Position.ToVector2() - mapViewport.Location.ToVector2();
        if (Math.Abs(previousZoom - _builderZoom) > float.Epsilon && previousZoom > 0f && _builderZoom > 0f)
        {
            _builderCamera += local * ((1f / previousZoom) - (1f / _builderZoom));
        }
    }

    private static bool IsLeftMouseClickPressed(MouseState mouse, MouseState previousMouse)
    {
        return mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released;
    }

    private void UpdateModernGarrisonBuilderMapInteraction(MouseState mouse)
    {
        if (_builderMultiEntityMapPickActive)
        {
            UpdateGarrisonBuilderMultiEntityMapPickInteraction(mouse);
            return;
        }

        var leftClick = IsLeftMouseClickPressed(mouse, _previousMouse);
        var rightClick = mouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Released;
        var mapViewport = GetModernGarrisonBuilderMapViewport();
        var onMap = mapViewport.Contains(mouse.Position);

        if (TryHandleModernGarrisonBuilderUiClick(mouse.Position, leftClick, rightClick))
        {
            return;
        }

        var world = BuilderScreenToWorld(mouse.Position);

        if (_builderEntityDragging
            && mouse.LeftButton == ButtonState.Released
            && _previousMouse.LeftButton == ButtonState.Pressed)
        {
            _builderEntityDragging = false;
            UpdateGarrisonBuilderDocumentEntities();
            _builderDirty = true;
            return;
        }

        if (_builderEntityDragging && mouse.LeftButton == ButtonState.Pressed)
        {
            ApplyGarrisonBuilderMultiEntityDrag(world);
            return;
        }

        if (_builderAreaSelectDragging && mouse.LeftButton == ButtonState.Released
            && _previousMouse.LeftButton == ButtonState.Pressed)
        {
            CommitGarrisonBuilderAreaSelect(mouse.Position);
            return;
        }

        if (_builderAreaSelectDragging && mouse.LeftButton == ButtonState.Pressed)
        {
            UpdateGarrisonBuilderAreaSelect(mouse.Position);
            return;
        }

        if (_builderActiveResizeHandle != GarrisonBuilderResizeHandle.None
            && mouse.LeftButton == ButtonState.Released
            && _previousMouse.LeftButton == ButtonState.Pressed)
        {
            _builderActiveResizeHandle = GarrisonBuilderResizeHandle.None;
            UpdateGarrisonBuilderDocumentEntities();
            _builderDirty = true;
            return;
        }

        if (_builderActiveResizeHandle != GarrisonBuilderResizeHandle.None && mouse.LeftButton == ButtonState.Pressed)
        {
            ApplyGarrisonBuilderResizeDrag(world);
            return;
        }

        if (!onMap)
        {
            return;
        }

        if (_builderObjectiveMapPickActive && leftClick)
        {
            if (TryPickGarrisonBuilderObjectiveAtWorld(world))
            {
                return;
            }

            CancelGarrisonBuilderObjectiveMapPick();
            _builderStatus = "objective pick cancelled";
            return;
        }

        if (_builderLogicMapPickActive && leftClick)
        {
            if (TryPickGarrisonBuilderLogicSourceAtWorld(world, out var logicSource))
            {
                ApplyGarrisonBuilderLogicMapPick(logicSource);
                return;
            }

            CancelGarrisonBuilderLogicMapPick();
            _builderStatus = "logic pick cancelled";
            return;
        }

        if (_builderEntityMapPickActive && leftClick)
        {
            if (TryPickGarrisonBuilderEntityMapPickAtWorld(world, out var targetEntity))
            {
                ApplyGarrisonBuilderEntityMapPick(targetEntity);
                return;
            }

            CancelGarrisonBuilderEntityMapPick();
            _builderStatus = "entity pick cancelled";
            return;
        }

        if (leftClick)
        {
            if (_builderActiveTool == GarrisonBuilderTool.Place && !string.IsNullOrWhiteSpace(_builderSelectedEntityType))
            {
                BeginGarrisonBuilderPlacement(world);
                return;
            }

            if (_builderActiveTool == GarrisonBuilderTool.Erase)
            {
                BeginGarrisonBuilderErase(world);
                return;
            }

            if (_builderActiveTool == GarrisonBuilderTool.Select
                && GetGarrisonBuilderSelectedEntityCount() <= 1
                && _builderSelectedEntityIndex >= 0
                && TryBeginGarrisonBuilderResize(mouse.Position, world))
            {
                return;
            }

            var pickResult = TryBeginGarrisonBuilderEntityPick(world, mouse.Position, out var pickedEntityIndex);
            if (pickResult == GarrisonBuilderEntityPickResult.OverlapPickerOpened)
            {
                return;
            }

            if (pickResult == GarrisonBuilderEntityPickResult.Picked)
            {
                if (_builderActiveTool == GarrisonBuilderTool.Select)
                {
                    if (IsGarrisonBuilderMapEntitySelected(pickedEntityIndex)
                        && TryBeginGarrisonBuilderEntityDrag(world, pickedEntityIndex))
                    {
                        return;
                    }
                }

                SelectGarrisonBuilderMapEntity(pickedEntityIndex);
                return;
            }

            if (_builderActiveTool == GarrisonBuilderTool.Select && onMap)
            {
                BeginGarrisonBuilderAreaSelect(mouse.Position);
            }
        }
        else if (mouse.LeftButton == ButtonState.Released && _previousMouse.LeftButton == ButtonState.Pressed)
        {
            if (_builderPlacementDragging)
            {
                CommitGarrisonBuilderPlacement(world);
            }
            else if (_builderEraseDragging)
            {
                CommitGarrisonBuilderErase(world);
            }
        }
        else if (mouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Released)
        {
            if (_builderActiveTool == GarrisonBuilderTool.Place)
            {
                _builderActiveTool = GarrisonBuilderTool.Select;
                _builderSelectedEntityType = string.Empty;
                _builderStatus = "select tool";
                return;
            }

            var contextPickResult = TryBeginGarrisonBuilderEntityPick(world, mouse.Position, out var contextEntityIndex);
            if (contextPickResult == GarrisonBuilderEntityPickResult.OverlapPickerOpened)
            {
                return;
            }

            if (contextPickResult == GarrisonBuilderEntityPickResult.Picked)
            {
                if (GetGarrisonBuilderSelectedEntityCount() <= 1
                    || !IsGarrisonBuilderMapEntitySelected(contextEntityIndex))
                {
                    SelectGarrisonBuilderMapEntity(contextEntityIndex);
                }
                else
                {
                    _builderSelectedEntityIndex = contextEntityIndex;
                }

                OpenGarrisonBuilderEntityContextMenu(mouse.Position);
                return;
            }

            CloseGarrisonBuilderEntityContextMenu();
            CloseGarrisonBuilderLayerContextMenu();
            if (_builderActiveTool == GarrisonBuilderTool.Place
                && CustomMapBuilderEntityCatalog.TryGetDefinition(_builderSelectedEntityType, out var definition))
            {
                BeginEditingGarrisonBuilderPlacementProperties(definition);
            }
        }
    }

    private bool TryHandleModernGarrisonBuilderUiClick(Point position, bool leftClick, bool rightClick)
    {
        if (TryHandleGarrisonBuilderEntityOverlapPickerClick(position, leftClick))
        {
            return true;
        }

        if (TryHandleGarrisonBuilderEntityContextMenuClick(position, leftClick))
        {
            return true;
        }

        if (TryHandleGarrisonBuilderLayerContextMenuClick(position, leftClick))
        {
            return true;
        }

        if (rightClick && TryHandleModernGarrisonBuilderLayerStripRightClick(position))
        {
            return true;
        }

        if (_builderLayerParallaxDialogOpen)
        {
            if (leftClick)
            {
                HandleGarrisonBuilderLayerParallaxDialogClick(position);
            }

            return true;
        }

        if (_builderMapNameCollisionDialogOpen)
        {
            return true;
        }

        if (TryHandleGarrisonBuilderLogicRecolorDialogClick(position, leftClick))
        {
            return true;
        }

        if (_builderObjectiveMapPickActive && leftClick)
        {
            if (GetGarrisonBuilderObjectiveMapPickPromptBounds().Contains(position))
            {
                return true;
            }

            if (!GetModernGarrisonBuilderMapViewport().Contains(position))
            {
                CancelGarrisonBuilderObjectiveMapPick();
                _builderStatus = "objective pick cancelled";
                return true;
            }

            return false;
        }

        if (_builderLogicMapPickActive && leftClick)
        {
            if (GetGarrisonBuilderLogicMapPickPromptBounds().Contains(position))
            {
                return true;
            }

            if (!GetModernGarrisonBuilderMapViewport().Contains(position))
            {
                CancelGarrisonBuilderLogicMapPick();
                _builderStatus = "logic pick cancelled";
                return true;
            }

            return false;
        }

        if (_builderEntityMapPickActive && leftClick)
        {
            if (GetGarrisonBuilderEntityMapPickPromptBounds().Contains(position))
            {
                return true;
            }

            if (!GetModernGarrisonBuilderMapViewport().Contains(position))
            {
                CancelGarrisonBuilderEntityMapPick();
                _builderStatus = "entity pick cancelled";
                return true;
            }

            return false;
        }

        if (_builderMultiEntityMapPickActive)
        {
            return true;
        }

        if (_builderEntityRefListDropdownOpen && leftClick)
        {
            if (!IsGarrisonBuilderEntityRefListDropdownScrollInteractionActive())
            {
                TryHandleGarrisonBuilderEntityRefListDropdownClick(position);
            }

            return true;
        }

        if (_builderPropertyTarget != GarrisonBuilderPropertyTarget.None
            && !_builderObjectiveMapPickActive
            && !_builderLogicMapPickActive
            && !_builderEntityMapPickActive
            && !_builderMultiEntityMapPickActive
            && GetGarrisonBuilderPropertyEditorBounds().Contains(position))
        {
            if (leftClick)
            {
                HandleGarrisonBuilderPropertyEditorClick(position, leftClick: true);
            }

            return true;
        }

        if (_builderGameModeMenuOpen)
        {
            return TryHandleGarrisonBuilderGameModeMenuClick(position, leftClick);
        }

        if (TryHandleModernGarrisonBuilderMenuBarClick(position, leftClick))
        {
            return true;
        }

        if (_builderMenuBarOpenMenu != GarrisonBuilderMenuBarMenu.None && leftClick)
        {
            if (TryHandleModernGarrisonBuilderMenuDropdownClick(position))
            {
                return true;
            }

            _builderMenuBarOpenMenu = GarrisonBuilderMenuBarMenu.None;
            return false;
        }

        if (!leftClick)
        {
            return false;
        }

        if (TryHandleModernGarrisonBuilderToolbarClick(position))
        {
            return true;
        }

        if (TryHandleModernGarrisonBuilderUnhideButtonClick(position))
        {
            return true;
        }

        if (TryHandleModernGarrisonBuilderSidebarClick(position))
        {
            return true;
        }

        return TryHandleModernGarrisonBuilderLayerStripClick(position);
    }

    private Rectangle GetModernGarrisonBuilderLayerStripBounds()
    {
        var height = GetModernGarrisonBuilderLayerStripHeight();
        return new Rectangle(0, BuilderViewportHeight - height, BuilderViewportWidth, height);
    }

    private Rectangle GetModernGarrisonBuilderLayerStripCollapseBounds()
    {
        var strip = GetModernGarrisonBuilderLayerStripBounds();
        return new Rectangle(strip.Right - 30, strip.Y + 3, 24, 16);
    }

    private int GetModernGarrisonBuilderLayerStripTabTop()
    {
        return BuilderViewportHeight - GetModernGarrisonBuilderLayerStripHeight() + 26;
    }

    private Rectangle GetModernGarrisonBuilderLayerTabBounds(int index)
    {
        return new Rectangle(
            12 + (index * ModernBuilderLayerTabWidth),
            GetModernGarrisonBuilderLayerStripTabTop(),
            ModernBuilderLayerTabWidth - 4,
            ModernBuilderLayerTabHeight);
    }

    private int GetModernGarrisonBuilderLayerStripActionsRight()
    {
        return GetModernGarrisonBuilderLayerStripBounds().Right - 12;
    }

    private Rectangle GetModernGarrisonBuilderLayerStripMarkBounds()
    {
        var top = GetModernGarrisonBuilderLayerStripTabTop();
        var right = GetModernGarrisonBuilderLayerStripActionsRight();
        right -= ModernBuilderLayerMarkButtonWidth;
        var buttonHeight = (int)GetGarrisonBuilderMinimumButtonHeight();
        return new Rectangle(right, top, ModernBuilderLayerMarkButtonWidth, buttonHeight);
    }

    private bool TryGetModernGarrisonBuilderLayerTabIndexAt(Point position, out int tabIndex)
    {
        tabIndex = -1;
        for (var index = 0; index <= 8; index += 1)
        {
            if (GetModernGarrisonBuilderLayerTabBounds(index).Contains(position))
            {
                tabIndex = index;
                return true;
            }
        }

        return false;
    }

    private Rectangle GetModernGarrisonBuilderToolbarBounds()
    {
        var sidebarWidth = GetModernGarrisonBuilderSidebarWidth();
        return new Rectangle(
            sidebarWidth,
            GetModernBuilderMenuBarHeight(),
            BuilderViewportWidth - sidebarWidth,
            GetModernBuilderToolbarHeight());
    }

    private Rectangle GetModernGarrisonBuilderMenuBarBounds()
    {
        return new Rectangle(0, 0, BuilderViewportWidth, GetModernBuilderMenuBarHeight());
    }

    private void GetModernGarrisonBuilderToolbarButtonBounds(
        out Rectangle selectBounds,
        out Rectangle placeBounds,
        out Rectangle eraseBounds,
        out Rectangle centerBounds)
    {
        var toolbar = GetModernGarrisonBuilderToolbarBounds();
        const int toolButtonWidth = 84;
        const int toolButtonGap = 6;
        const int centerButtonWidth = 92;
        const int edgePadding = 8;
        var buttonHeight = (int)GetGarrisonBuilderMinimumButtonHeight();
        var y = toolbar.Y + Math.Max(4, (toolbar.Height - buttonHeight) / 2);
        centerBounds = new Rectangle(toolbar.Right - edgePadding - centerButtonWidth, y, centerButtonWidth, buttonHeight);
        eraseBounds = new Rectangle(centerBounds.X - toolButtonGap - toolButtonWidth, y, toolButtonWidth, buttonHeight);
        placeBounds = new Rectangle(eraseBounds.X - toolButtonGap - toolButtonWidth, y, toolButtonWidth, buttonHeight);
        selectBounds = new Rectangle(placeBounds.X - toolButtonGap - toolButtonWidth, y, toolButtonWidth, buttonHeight);
    }

    private bool TryHandleModernGarrisonBuilderToolbarClick(Point position)
    {
        var toolbar = GetModernGarrisonBuilderToolbarBounds();
        if (!toolbar.Contains(position))
        {
            return false;
        }

        GetModernGarrisonBuilderToolbarButtonBounds(out var selectBounds, out var placeBounds, out var eraseBounds, out var centerBounds);
        if (TryHitModernToolButton(position, selectBounds, GarrisonBuilderTool.Select))
        {
            return true;
        }

        if (TryHitModernToolButton(position, placeBounds, GarrisonBuilderTool.Place))
        {
            return true;
        }

        if (TryHitModernToolButton(position, eraseBounds, GarrisonBuilderTool.Erase))
        {
            return true;
        }

        if (centerBounds.Contains(position))
        {
            CenterGarrisonBuilderCameraOnMap();
            _builderStatus = "centered on map";
            return true;
        }

        return false;
    }

    private Rectangle GetModernGarrisonBuilderUnhideButtonBounds()
    {
        GetModernGarrisonBuilderToolbarButtonBounds(out _, out _, out _, out var centerBounds);
        const int edgePadding = 8;
        var buttonHeight = (int)GetGarrisonBuilderMinimumButtonHeight();
        var toolbar = GetModernGarrisonBuilderToolbarBounds();
        var width = centerBounds.Width;
        var x = toolbar.Right - edgePadding - width;
        var y = toolbar.Bottom + 4;
        return new Rectangle(x, y, width, buttonHeight);
    }

    private bool TryHandleModernGarrisonBuilderUnhideButtonClick(Point position)
    {
        if (!HasGarrisonBuilderHiddenEntities())
        {
            return false;
        }

        var bounds = GetModernGarrisonBuilderUnhideButtonBounds();
        if (!bounds.Contains(position))
        {
            return false;
        }

        UnhideAllGarrisonBuilderEntities();
        return true;
    }

    private void DrawModernGarrisonBuilderUnhideButton(MouseState mouse)
    {
        if (!HasGarrisonBuilderHiddenEntities())
        {
            return;
        }

        var bounds = GetModernGarrisonBuilderUnhideButtonBounds();
        DrawBuilderMenuButton(bounds, "Unhide", bounds.Contains(mouse.Position));
    }

    private bool TryHandleModernGarrisonBuilderMenuBarClick(Point position, bool leftClick)
    {
        var menuBar = GetModernGarrisonBuilderMenuBarBounds();
        if (!menuBar.Contains(position))
        {
            return false;
        }

        if (!leftClick)
        {
            return true;
        }

        var tabs = new (GarrisonBuilderMenuBarMenu Menu, string Label)[]
        {
            (GarrisonBuilderMenuBarMenu.File, "File"),
            (GarrisonBuilderMenuBarMenu.Edit, "Edit"),
            (GarrisonBuilderMenuBarMenu.View, "View"),
            (GarrisonBuilderMenuBarMenu.Map, "Map"),
        };
        var x = 8;
        foreach (var (menu, _) in tabs)
        {
            var tabBounds = new Rectangle(x, 2, ModernBuilderMenuTabWidth, GetModernBuilderMenuBarHeight() - 4);
            if (tabBounds.Contains(position))
            {
                _builderMenuBarOpenMenu = _builderMenuBarOpenMenu == menu ? GarrisonBuilderMenuBarMenu.None : menu;
                return true;
            }

            x += ModernBuilderMenuTabWidth + 4;
        }

        return true;
    }

    private bool TryHandleModernGarrisonBuilderMenuDropdownClick(Point position)
    {
        var dropdown = GetModernGarrisonBuilderMenuDropdownBounds(_builderMenuBarOpenMenu);
        if (!dropdown.Contains(position))
        {
            return false;
        }

        var items = GetModernGarrisonBuilderMenuItems(_builderMenuBarOpenMenu);
        var rowY = dropdown.Y + 6;
        var selectedIndex = -1;
        for (var index = 0; index < items.Count; index += 1)
        {
            var rowHeight = (int)GetGarrisonBuilderMinimumButtonHeight();
            if (position.Y >= rowY && position.Y < rowY + rowHeight)
            {
                selectedIndex = index;
                break;
            }

            rowY += rowHeight + 4;
        }

        if (selectedIndex < 0)
        {
            return true;
        }

        items[selectedIndex].Action();
        _builderMenuBarOpenMenu = GarrisonBuilderMenuBarMenu.None;
        return true;
    }

    private Rectangle GetModernGarrisonBuilderMenuDropdownBounds(GarrisonBuilderMenuBarMenu menu)
    {
        if (menu == GarrisonBuilderMenuBarMenu.None)
        {
            return Rectangle.Empty;
        }

        var menuIndex = menu switch
        {
            GarrisonBuilderMenuBarMenu.File => 0,
            GarrisonBuilderMenuBarMenu.Edit => 1,
            GarrisonBuilderMenuBarMenu.View => 2,
            _ => 3,
        };
        var x = 8 + (menuIndex * (ModernBuilderMenuTabWidth + 4));
        var items = GetModernGarrisonBuilderMenuItems(menu);
        var rowHeight = (int)GetGarrisonBuilderMinimumButtonHeight();
        var height = 12 + (items.Count * rowHeight) + (Math.Max(0, items.Count - 1) * 4);
        var minWidth = ModernBuilderMenuTabWidth + 40;
        var labelScale = GetGarrisonBuilderBitmapFontScale();
        var width = minWidth;
        foreach (var (label, _) in items)
        {
            width = Math.Max(width, (int)MathF.Ceiling(MeasureBitmapFontWidth(label, labelScale) + 36f));
        }

        return new Rectangle(x, GetModernBuilderMenuBarHeight(), width, height);
    }

    private List<(string Label, Action Action)> GetModernGarrisonBuilderMenuItems(GarrisonBuilderMenuBarMenu menu)
    {
        return menu switch
        {
            GarrisonBuilderMenuBarMenu.File =>
            [
                ("New map", CreateNewGarrisonBuilderDocument),
                ("Open map...", () => BeginEditingGarrisonBuilderPath(GarrisonBuilderPathField.OpenMap)),
                ("Save (Ctrl+S)", SaveGarrisonBuilderDocument),
                ("Save as... (Ctrl+Shift+S)", () => BeginEditingGarrisonBuilderPath(GarrisonBuilderPathField.Save)),
                ("Load background...", () => BeginEditingGarrisonBuilderPath(GarrisonBuilderPathField.Background)),
                ("Load walkmask...", () => BeginEditingGarrisonBuilderPath(GarrisonBuilderPathField.Walkmask)),
                ("Load layer image...", BeginLoadForCurrentGarrisonBuilderLayer),
                ("Quick test", QuickTestGarrisonBuilderMap),
            ],
            GarrisonBuilderMenuBarMenu.Edit =>
            [
                ("Undo", () => TryUndoGarrisonBuilder()),
                ("Redo", () => TryRedoGarrisonBuilder()),
                ("Clear entities", ClearGarrisonBuilderEntities),
                ("Map properties...", BeginEditingGarrisonBuilderMapProperties),
                ($"Fast scrolling{(_builderFastScrolling ? " *" : string.Empty)}", ToggleGarrisonBuilderFastScrolling),
            ],
            GarrisonBuilderMenuBarMenu.View =>
            [
                ($"Show background{(_builderShowBackground ? " *" : string.Empty)}", ToggleGarrisonBuilderShowBackground),
                ($"Show walkmask{(_builderShowWalkmask ? " *" : string.Empty)}", ToggleGarrisonBuilderShowWalkmask),
                ($"Show grid{(_builderShowGrid ? " *" : string.Empty)}", ToggleGarrisonBuilderShowGrid),
                ($"Show foreground{(_builderShowForeground ? " *" : string.Empty)}", ToggleGarrisonBuilderShowForeground),
                ($"Symmetry mode{(_builderSymmetry ? " *" : string.Empty)}", ToggleGarrisonBuilderSymmetryMode),
                ($"Scale mode{(_builderScaleMode ? " *" : string.Empty)}", ToggleGarrisonBuilderScaleMode),
                ($"Entity palette{(_builderEntityPaletteVisible ? " *" : string.Empty)}", ToggleGarrisonBuilderEntityPalette),
            ],
            GarrisonBuilderMenuBarMenu.Map =>
            [
                ("Set game mode...", () => _builderGameModeMenuOpen = true),
                ("Center on map", () =>
                {
                    CenterGarrisonBuilderCameraOnMap();
                    _builderStatus = "centered on map";
                }),
            ],
            _ => [],
        };
    }

    private void CreateNewGarrisonBuilderDocument()
    {
        _builderDocument = CustomMapBuilderDocument.CreateEmpty("new_map");
        _builderEntityCoordinatesAreWalkmaskPixels = false;
        _builderEntities.Clear();
        ClearGarrisonBuilderHiddenEntities();
        _builderOpenMapBuffer = string.Empty;
        _builderSavePath = string.Empty;
        ClearGarrisonBuilderMapEntitySelection();
        SyncGarrisonBuilderPathBuffers();
        _builderDirty = false;
        RequestGarrisonBuilderCameraCenter();
        _builderStatus = "new map";
    }

    private void ClearGarrisonBuilderEntities()
    {
        _builderEntities.Clear();
        ClearGarrisonBuilderHiddenEntities();
        UpdateGarrisonBuilderDocumentEntities();
        ClearGarrisonBuilderMapEntitySelection();
        _builderDirty = true;
        _builderStatus = "entities cleared";
    }

    private void ToggleGarrisonBuilderFastScrolling()
    {
        _builderFastScrolling = !_builderFastScrolling;
        _builderStatus = _builderFastScrolling ? "fast scrolling on" : "fast scrolling off";
    }

    private void ToggleGarrisonBuilderShowBackground()
    {
        _builderShowBackground = !_builderShowBackground;
    }

    private void ToggleGarrisonBuilderShowWalkmask()
    {
        _builderShowWalkmask = !_builderShowWalkmask;
    }

    private void ToggleGarrisonBuilderShowGrid()
    {
        _builderShowGrid = !_builderShowGrid;
    }

    private void ToggleGarrisonBuilderShowForeground()
    {
        _builderShowForeground = !_builderShowForeground;
    }

    private void ToggleGarrisonBuilderSymmetryMode()
    {
        _builderSymmetry = !_builderSymmetry;
        _builderStatus = _builderSymmetry ? "symmetry mode enabled" : "symmetry mode disabled";
    }

    private void ToggleGarrisonBuilderScaleMode()
    {
        _builderScaleMode = !_builderScaleMode;
    }

    private void ToggleGarrisonBuilderEntityPalette()
    {
        _builderEntityPaletteVisible = !_builderEntityPaletteVisible;
        _builderStatus = _builderEntityPaletteVisible ? "entity palette shown" : "entity palette hidden";
    }

    private bool TryHitModernToolButton(Point position, Rectangle bounds, GarrisonBuilderTool tool)
    {
        if (!bounds.Contains(position))
        {
            return false;
        }

        _builderActiveTool = tool;
        if (tool == GarrisonBuilderTool.Erase || tool == GarrisonBuilderTool.Select)
        {
            _builderSelectedEntityType = string.Empty;
        }

        _builderStatus = $"{tool.ToString().ToLowerInvariant()} tool";
        return true;
    }

    private bool TryHandleModernGarrisonBuilderSidebarClick(Point position)
    {
        var sidebarWidth = GetModernGarrisonBuilderSidebarWidth();
        var sidebar = new Rectangle(0, GetModernBuilderMenuBarHeight(), sidebarWidth, BuilderViewportHeight - GetModernBuilderMenuBarHeight() - GetModernGarrisonBuilderLayerStripHeight());
        if (!sidebar.Contains(position))
        {
            return false;
        }

        if (!_builderEntityPaletteVisible)
        {
            ToggleGarrisonBuilderEntityPalette();
            return true;
        }

        var definitions = GetActiveGarrisonBuilderEntityDefinitions();
        var layout = GetModernGarrisonBuilderPaletteLayout(sidebar, definitions);
        if (!layout.ContentBounds.Contains(position))
        {
            var hideBounds = new Rectangle(sidebar.Right - 30, sidebar.Y + 6, 24, 20);
            if (hideBounds.Contains(position))
            {
                ToggleGarrisonBuilderEntityPalette();
                return true;
            }

            if (TryGetModernGarrisonBuilderSidebarModeButtonBounds(sidebar, out var modeBounds)
                && modeBounds.Contains(position))
            {
                _builderGameModeMenuOpen = true;
            }

            return true;
        }

        if (!TryHitEntityPaletteAt(position, sidebar, definitions, out var hit))
        {
            return true;
        }

        if (hit.IsCategoryHeader)
        {
            _builderEntityPaletteCategoryExpanded[hit.Category] = !_builderEntityPaletteCategoryExpanded[hit.Category];
            ClampEntityPaletteScrollOffset(sidebar, definitions);
            _builderStatus = _builderEntityPaletteCategoryExpanded[hit.Category]
                ? $"{EntityPaletteCategories.First(pair => pair.Category == hit.Category).Label} expanded"
                : $"{EntityPaletteCategories.First(pair => pair.Category == hit.Category).Label} collapsed";
            return true;
        }

        var paletteDefinitions = hit.Category == GarrisonBuilderEntityPaletteCategory.Logic
            ? GetLogicGarrisonBuilderEntityDefinitions()
            : definitions;
        if (hit.EntityDefinitionIndex < 0 || hit.EntityDefinitionIndex >= paletteDefinitions.Count)
        {
            return true;
        }

        SelectGarrisonBuilderEntity(paletteDefinitions[hit.EntityDefinitionIndex], updateStatus: true);
        _builderActiveTool = GarrisonBuilderTool.Place;
        return true;
    }

    private bool TryHandleModernGarrisonBuilderLayerStripClick(Point position)
    {
        var strip = GetModernGarrisonBuilderLayerStripBounds();
        if (!strip.Contains(position))
        {
            return false;
        }

        if (GetModernGarrisonBuilderLayerStripCollapseBounds().Contains(position))
        {
            _builderLayerStripExpanded = !_builderLayerStripExpanded;
            CloseGarrisonBuilderLayerContextMenu();
            _builderStatus = _builderLayerStripExpanded ? "layers panel shown" : "layers panel hidden";
            return true;
        }

        if (_builderLayerStripExpanded && GetModernGarrisonBuilderLayerStripMarkBounds().Contains(position))
        {
            ToggleGarrisonBuilderLayerMarkMode();
            return true;
        }

        if (_builderLayerStripExpanded
            && TryGetModernGarrisonBuilderLayerTabIndexAt(position, out var tabIndex))
        {
            _builderLayerIndex = tabIndex;
            _builderStatus = $"selected {GetLegacyGarrisonBuilderLayerName()}";
            return true;
        }

        return true;
    }

    private bool TryHandleModernGarrisonBuilderLayerStripRightClick(Point position)
    {
        if (!_builderLayerStripExpanded)
        {
            return false;
        }

        var strip = GetModernGarrisonBuilderLayerStripBounds();
        if (!strip.Contains(position))
        {
            return false;
        }

        if (!TryGetModernGarrisonBuilderLayerTabIndexAt(position, out var tabIndex))
        {
            return false;
        }

        _builderLayerIndex = tabIndex;
        OpenGarrisonBuilderLayerContextMenu(position, tabIndex);
        return true;
    }

    private void SelectGarrisonBuilderMapEntity(int entityIndex)
    {
        _builderEntityDragging = false;
        _builderAreaSelectDragging = false;
        CloseGarrisonBuilderEntityContextMenu();
        CloseGarrisonBuilderEntityOverlapPicker();
        if (!IsValidGarrisonBuilderMapEntityIndex(entityIndex))
        {
            ClearGarrisonBuilderMapEntitySelection();
            _builderSelectedEntityType = string.Empty;
            _builderStatus = "selection cleared";
            return;
        }

        SelectSingleGarrisonBuilderMapEntity(entityIndex);
        _builderSelectedEntityType = string.Empty;
        _builderActiveTool = GarrisonBuilderTool.Select;
        var entity = _builderEntities[entityIndex];
        _builderStatus = $"selected {entity.Type}";
    }

    private void OpenGarrisonBuilderEntityContextMenu(Point position)
    {
        CloseGarrisonBuilderLayerContextMenu();
        RebuildGarrisonBuilderEntityContextMenuActions();
        _builderEntityContextMenuOpen = true;
        _builderEntityContextMenuPosition = position;
        _builderMenuBarOpenMenu = GarrisonBuilderMenuBarMenu.None;
    }

    private void RebuildGarrisonBuilderEntityContextMenuActions()
    {
        _builderEntityContextMenuActionsScratch.Clear();
        _builderEntityContextMenuActionsScratch.Add(GarrisonBuilderEntityContextAction.Properties);
        if (_builderSelectedEntityIndex >= 0 && _builderSelectedEntityIndex < _builderEntities.Count)
        {
            var entity = _builderEntities[_builderSelectedEntityIndex];
            if (MapLogicNodeColorMetadata.SupportsRecolor(entity.Type))
            {
                _builderEntityContextMenuActionsScratch.Add(GarrisonBuilderEntityContextAction.Recolor);
            }
        }

        _builderEntityContextMenuActionsScratch.Add(GarrisonBuilderEntityContextAction.Clone);
        _builderEntityContextMenuActionsScratch.Add(GarrisonBuilderEntityContextAction.Remove);
    }

    private void CloseGarrisonBuilderEntityContextMenu()
    {
        _builderEntityContextMenuOpen = false;
    }

    private void OpenGarrisonBuilderLayerContextMenu(Point position, int layerIndex)
    {
        CloseGarrisonBuilderEntityContextMenu();
        _builderLayerContextMenuOpen = true;
        _builderLayerContextMenuPosition = position;
        _builderLayerContextMenuLayerIndex = layerIndex;
        _builderMenuBarOpenMenu = GarrisonBuilderMenuBarMenu.None;
    }

    private void CloseGarrisonBuilderLayerContextMenu()
    {
        _builderLayerContextMenuOpen = false;
        _builderLayerContextMenuLayerIndex = -1;
    }

    private IReadOnlyList<GarrisonBuilderLayerContextAction> GetGarrisonBuilderLayerContextMenuActions(int layerIndex)
    {
        if (layerIndex is >= 0 and <= 6)
        {
            return
            [
                GarrisonBuilderLayerContextAction.Load,
                GarrisonBuilderLayerContextAction.ToggleHide,
                GarrisonBuilderLayerContextAction.Parallax,
            ];
        }

        return
        [
            GarrisonBuilderLayerContextAction.Load,
            GarrisonBuilderLayerContextAction.ToggleHide,
        ];
    }

    private string GetGarrisonBuilderLayerContextMenuLabel(GarrisonBuilderLayerContextAction action, int layerIndex)
    {
        return action switch
        {
            GarrisonBuilderLayerContextAction.Load => "Load image...",
            GarrisonBuilderLayerContextAction.ToggleHide => IsGarrisonBuilderLayerHidden(layerIndex) ? "Show layer" : "Hide layer",
            GarrisonBuilderLayerContextAction.Parallax => "Parallax...",
            _ => string.Empty,
        };
    }

    private Rectangle GetGarrisonBuilderLayerContextMenuBounds()
    {
        const int rowGap = 2;
        const int padding = 4;
        const int width = 168;
        var rowHeight = GetGarrisonBuilderMenuRowHeight();
        var actions = GetGarrisonBuilderLayerContextMenuActions(_builderLayerContextMenuLayerIndex);
        var height = padding + (actions.Count * rowHeight)
            + (Math.Max(0, actions.Count - 1) * rowGap) + padding;
        var x = Math.Clamp(_builderLayerContextMenuPosition.X, 4, Math.Max(4, BuilderViewportWidth - width - 4));
        var y = Math.Clamp(_builderLayerContextMenuPosition.Y, 4, Math.Max(4, BuilderViewportHeight - height - 4));
        return new Rectangle(x, y, width, height);
    }

    private Rectangle GetGarrisonBuilderLayerContextMenuItemBounds(Rectangle menuBounds, int index)
    {
        var rowHeight = GetGarrisonBuilderMenuRowHeight();
        const int rowGap = 2;
        var y = menuBounds.Y + 4 + (index * (rowHeight + rowGap));
        return new Rectangle(menuBounds.X + 4, y, menuBounds.Width - 8, rowHeight);
    }

    private bool TryHandleGarrisonBuilderLayerContextMenuClick(Point position, bool leftClick)
    {
        if (!_builderLayerContextMenuOpen)
        {
            return false;
        }

        if (!leftClick)
        {
            return true;
        }

        var menuBounds = GetGarrisonBuilderLayerContextMenuBounds();
        if (!menuBounds.Contains(position))
        {
            CloseGarrisonBuilderLayerContextMenu();
            return false;
        }

        var actions = GetGarrisonBuilderLayerContextMenuActions(_builderLayerContextMenuLayerIndex);
        for (var index = 0; index < actions.Count; index += 1)
        {
            var itemBounds = GetGarrisonBuilderLayerContextMenuItemBounds(menuBounds, index);
            if (!itemBounds.Contains(position))
            {
                continue;
            }

            ExecuteGarrisonBuilderLayerContextAction(actions[index], _builderLayerContextMenuLayerIndex);
            CloseGarrisonBuilderLayerContextMenu();
            return true;
        }

        CloseGarrisonBuilderLayerContextMenu();
        return true;
    }

    private void ExecuteGarrisonBuilderLayerContextAction(GarrisonBuilderLayerContextAction action, int layerIndex)
    {
        switch (action)
        {
            case GarrisonBuilderLayerContextAction.Load:
                BeginLoadForGarrisonBuilderLayer(layerIndex);
                break;
            case GarrisonBuilderLayerContextAction.ToggleHide:
                ToggleGarrisonBuilderLayerHide(layerIndex);
                break;
            case GarrisonBuilderLayerContextAction.Parallax:
                OpenGarrisonBuilderLayerParallaxDialog(layerIndex);
                break;
        }
    }

    private void OpenGarrisonBuilderEntityOverlapPicker(Point position, IReadOnlyList<int> entityIndices)
    {
        _builderEntityOverlapPickerIndices.Clear();
        for (var index = 0; index < entityIndices.Count; index += 1)
        {
            var entityIndex = entityIndices[index];
            if (IsValidGarrisonBuilderMapEntityIndex(entityIndex))
            {
                _builderEntityOverlapPickerIndices.Add(entityIndex);
            }
        }

        if (_builderEntityOverlapPickerIndices.Count == 0)
        {
            return;
        }

        _builderEntityOverlapPickerOpen = true;
        _builderEntityOverlapPickerPosition = position;
        _builderMenuBarOpenMenu = GarrisonBuilderMenuBarMenu.None;
        CloseGarrisonBuilderEntityContextMenu();
        CloseGarrisonBuilderLayerContextMenu();
        _builderStatus = $"choose entity ({_builderEntityOverlapPickerIndices.Count})";
    }

    private void CloseGarrisonBuilderEntityOverlapPicker()
    {
        _builderEntityOverlapPickerOpen = false;
        _builderEntityOverlapPickerIndices.Clear();
    }

    private void AdjustGarrisonBuilderEntityOverlapPickerIndicesAfterRemoval(int removedIndex)
    {
        if (!_builderEntityOverlapPickerOpen || _builderEntityOverlapPickerIndices.Count == 0)
        {
            return;
        }

        for (var index = _builderEntityOverlapPickerIndices.Count - 1; index >= 0; index -= 1)
        {
            var entityIndex = _builderEntityOverlapPickerIndices[index];
            if (entityIndex == removedIndex)
            {
                _builderEntityOverlapPickerIndices.RemoveAt(index);
                continue;
            }

            if (entityIndex > removedIndex)
            {
                _builderEntityOverlapPickerIndices[index] = entityIndex - 1;
            }
        }

        if (_builderEntityOverlapPickerIndices.Count == 0)
        {
            CloseGarrisonBuilderEntityOverlapPicker();
        }
    }

    private void PruneGarrisonBuilderEntityOverlapPickerIndices()
    {
        for (var index = _builderEntityOverlapPickerIndices.Count - 1; index >= 0; index -= 1)
        {
            if (!IsValidGarrisonBuilderMapEntityIndex(_builderEntityOverlapPickerIndices[index]))
            {
                _builderEntityOverlapPickerIndices.RemoveAt(index);
            }
        }

        if (_builderEntityOverlapPickerOpen && _builderEntityOverlapPickerIndices.Count == 0)
        {
            CloseGarrisonBuilderEntityOverlapPicker();
        }
    }

    private string GetGarrisonBuilderEntityOverlapPickerLabel(int entityIndex)
    {
        if (!IsValidGarrisonBuilderMapEntityIndex(entityIndex))
        {
            return "(unavailable)";
        }

        var entity = _builderEntities[entityIndex];
        return $"{entity.Type} @ {entity.X:0},{entity.Y:0}";
    }

    private Rectangle GetGarrisonBuilderEntityOverlapPickerBounds()
    {
        const int rowGap = 2;
        const int padding = 4;
        const int minWidth = 168;
        var rowHeight = GetGarrisonBuilderMenuRowHeight();
        var labelScale = GetGarrisonBuilderBitmapFontScale();
        var width = minWidth;
        foreach (var entityIndex in _builderEntityOverlapPickerIndices)
        {
            var label = GetGarrisonBuilderEntityOverlapPickerLabel(entityIndex);
            width = Math.Max(width, (int)MathF.Ceiling(MeasureBitmapFontWidth(label, labelScale) + 28f));
        }

        var height = padding
            + (_builderEntityOverlapPickerIndices.Count * rowHeight)
            + (Math.Max(0, _builderEntityOverlapPickerIndices.Count - 1) * rowGap)
            + padding;
        var x = Math.Clamp(_builderEntityOverlapPickerPosition.X, 4, Math.Max(4, BuilderViewportWidth - width - 4));
        var y = Math.Clamp(_builderEntityOverlapPickerPosition.Y, 4, Math.Max(4, BuilderViewportHeight - height - 4));
        return new Rectangle(x, y, width, height);
    }

    private Rectangle GetGarrisonBuilderEntityOverlapPickerItemBounds(Rectangle menuBounds, int index)
    {
        var rowHeight = GetGarrisonBuilderMenuRowHeight();
        const int rowGap = 2;
        var y = menuBounds.Y + 4 + (index * (rowHeight + rowGap));
        return new Rectangle(menuBounds.X + 4, y, menuBounds.Width - 8, rowHeight);
    }

    private bool TryHandleGarrisonBuilderEntityOverlapPickerClick(Point position, bool leftClick)
    {
        if (!_builderEntityOverlapPickerOpen)
        {
            return false;
        }

        if (!leftClick)
        {
            return true;
        }

        var menuBounds = GetGarrisonBuilderEntityOverlapPickerBounds();
        if (!menuBounds.Contains(position))
        {
            CloseGarrisonBuilderEntityOverlapPicker();
            return false;
        }

        for (var index = 0; index < _builderEntityOverlapPickerIndices.Count; index += 1)
        {
            var itemBounds = GetGarrisonBuilderEntityOverlapPickerItemBounds(menuBounds, index);
            if (!itemBounds.Contains(position))
            {
                continue;
            }

            var entityIndex = _builderEntityOverlapPickerIndices[index];
            if (!IsValidGarrisonBuilderMapEntityIndex(entityIndex))
            {
                PruneGarrisonBuilderEntityOverlapPickerIndices();
                _builderStatus = "entity no longer available";
                return true;
            }

            SelectGarrisonBuilderMapEntity(entityIndex);
            return true;
        }

        CloseGarrisonBuilderEntityOverlapPicker();
        return true;
    }

    private string GetGarrisonBuilderEntityContextMenuLabel(GarrisonBuilderEntityContextAction action)
    {
        return action switch
        {
            GarrisonBuilderEntityContextAction.Properties => "Properties",
            GarrisonBuilderEntityContextAction.Recolor => "Recolor",
            GarrisonBuilderEntityContextAction.Clone => "Clone",
            GarrisonBuilderEntityContextAction.Remove => "Remove",
            _ => string.Empty,
        };
    }

    private Rectangle GetGarrisonBuilderEntityContextMenuBounds()
    {
        const int rowGap = 2;
        const int padding = 4;
        const int width = 148;
        var rowHeight = GetGarrisonBuilderMenuRowHeight();
        var actionCount = Math.Max(1, _builderEntityContextMenuActionsScratch.Count);
        var height = padding + (actionCount * rowHeight)
            + ((actionCount - 1) * rowGap) + padding;
        var x = Math.Clamp(_builderEntityContextMenuPosition.X, 4, Math.Max(4, BuilderViewportWidth - width - 4));
        var y = Math.Clamp(_builderEntityContextMenuPosition.Y, 4, Math.Max(4, BuilderViewportHeight - height - 4));
        return new Rectangle(x, y, width, height);
    }

    private Rectangle GetGarrisonBuilderEntityContextMenuItemBounds(Rectangle menuBounds, int index)
    {
        var rowHeight = GetGarrisonBuilderMenuRowHeight();
        const int rowGap = 2;
        var y = menuBounds.Y + 4 + (index * (rowHeight + rowGap));
        return new Rectangle(menuBounds.X + 4, y, menuBounds.Width - 8, rowHeight);
    }

    private bool TryHandleGarrisonBuilderEntityContextMenuClick(Point position, bool leftClick)
    {
        if (!_builderEntityContextMenuOpen)
        {
            return false;
        }

        if (!leftClick)
        {
            return true;
        }

        var menuBounds = GetGarrisonBuilderEntityContextMenuBounds();
        if (!menuBounds.Contains(position))
        {
            CloseGarrisonBuilderEntityContextMenu();
            return false;
        }

        for (var index = 0; index < _builderEntityContextMenuActionsScratch.Count; index += 1)
        {
            var itemBounds = GetGarrisonBuilderEntityContextMenuItemBounds(menuBounds, index);
            if (!itemBounds.Contains(position))
            {
                continue;
            }

            ExecuteGarrisonBuilderEntityContextAction(_builderEntityContextMenuActionsScratch[index]);
            CloseGarrisonBuilderEntityContextMenu();
            return true;
        }

        CloseGarrisonBuilderEntityContextMenu();
        return true;
    }

    private void ExecuteGarrisonBuilderEntityContextAction(GarrisonBuilderEntityContextAction action)
    {
        switch (action)
        {
            case GarrisonBuilderEntityContextAction.Properties:
                CloseGarrisonBuilderLogicRecolorDialog();
                BeginEditingGarrisonBuilderSelectedEntity();
                break;
            case GarrisonBuilderEntityContextAction.Recolor:
                OpenGarrisonBuilderLogicRecolorDialog(_builderSelectedEntityIndex);
                break;
            case GarrisonBuilderEntityContextAction.Clone:
                CloneGarrisonBuilderSelectedEntity();
                break;
            case GarrisonBuilderEntityContextAction.Remove:
                RemoveGarrisonBuilderSelectedEntity();
                break;
        }
    }

    private void CloneGarrisonBuilderSelectedEntity()
    {
        CloneGarrisonBuilderSelectedEntities();
    }

    private bool TryDuplicateGarrisonBuilderEntity(int sourceIndex, bool placeAtSourcePosition, out int duplicateIndex)
    {
        duplicateIndex = -1;
        if (!TryDuplicateGarrisonBuilderEntities([sourceIndex], placeAtSourcePosition, out var duplicates)
            || duplicates.Count == 0)
        {
            return false;
        }

        duplicateIndex = duplicates[0];
        return true;
    }

    private void RemoveGarrisonBuilderSelectedEntity()
    {
        RemoveGarrisonBuilderSelectedEntities();
    }

    private readonly record struct ModernGarrisonBuilderPaletteLayout(
        Rectangle ContentBounds,
        int HeaderHeight,
        int ItemHeight,
        bool HasScrollbar);

    private readonly record struct EntityPaletteHit(
        bool IsHit,
        bool IsCategoryHeader,
        GarrisonBuilderEntityPaletteCategory Category,
        int EntityDefinitionIndex);

    private bool TryGetModernGarrisonBuilderSidebarModeButtonBounds(Rectangle sidebar, out Rectangle modeBounds)
    {
        var compactButtonHeight = (int)GetGarrisonBuilderMinimumButtonHeight();
        modeBounds = new Rectangle(10, sidebar.Y + 38, sidebar.Width - 20, compactButtonHeight);
        return true;
    }

    private int GetModernGarrisonBuilderSidebarPaletteTop(int sidebarY)
    {
        var compactButtonHeight = (int)GetGarrisonBuilderMinimumButtonHeight();
        return sidebarY + 38 + compactButtonHeight + 6 + compactButtonHeight + 8;
    }

    private ModernGarrisonBuilderPaletteLayout GetModernGarrisonBuilderPaletteLayout(
        Rectangle sidebar,
        IReadOnlyList<CustomMapBuilderEntityDefinition> definitions)
    {
        const int statusReserve = 28;
        var paletteTop = GetModernGarrisonBuilderSidebarPaletteTop(sidebar.Y);
        var contentBounds = new Rectangle(
            sidebar.X + 6,
            paletteTop,
            sidebar.Width - 12,
            sidebar.Bottom - paletteTop - statusReserve);
        var headerHeight = (int)GetGarrisonBuilderMinimumButtonHeight(0.9f);
        var itemHeight = GetGarrisonBuilderMenuRowHeight(0.88f);
        var contentHeight = GetEntityPaletteContentHeight( headerHeight, itemHeight);
        var hasScrollbar = contentHeight > contentBounds.Height;
        return new ModernGarrisonBuilderPaletteLayout(contentBounds, headerHeight, itemHeight, hasScrollbar);
    }

    private static int GetEntityPaletteListWidth(ModernGarrisonBuilderPaletteLayout layout)
    {
        var width = layout.ContentBounds.Width;
        if (layout.HasScrollbar)
        {
            width -= ModernBuilderPaletteScrollbarWidth + 4;
        }

        return Math.Max(1, width);
    }

    private static Rectangle GetEntityPaletteScrollbarTrackBounds(ModernGarrisonBuilderPaletteLayout layout)
    {
        var listWidth = GetEntityPaletteListWidth(layout);
        return new Rectangle(
            layout.ContentBounds.X + listWidth + 4,
            layout.ContentBounds.Y,
            ModernBuilderPaletteScrollbarWidth,
            layout.ContentBounds.Height);
    }

    private int GetEntityPaletteContentHeight(int headerHeight, int itemHeight)
    {
        var height = 0;
        foreach (var (category, _) in EntityPaletteCategories)
        {
            height += headerHeight + ModernBuilderPaletteCategorySpacing;
            if (!IsEntityPaletteCategoryExpanded(category))
            {
                continue;
            }

            var categoryCount = category == GarrisonBuilderEntityPaletteCategory.Logic
                ? GetLogicGarrisonBuilderEntityDefinitions().Count
                : GetActiveGarrisonBuilderEntityDefinitions().Count;
            if (categoryCount == 0)
            {
                continue;
            }

            height += (categoryCount * itemHeight)
                + ((categoryCount - 1) * ModernBuilderPaletteItemSpacing);
        }

        return Math.Max(0, height - ModernBuilderPaletteCategorySpacing);
    }

    private bool IsEntityPaletteCategoryExpanded(GarrisonBuilderEntityPaletteCategory category)
    {
        return _builderEntityPaletteCategoryExpanded.GetValueOrDefault(category, false);
    }

    private void ClampEntityPaletteScrollOffset(Rectangle sidebar, IReadOnlyList<CustomMapBuilderEntityDefinition> definitions)
    {
        var layout = GetModernGarrisonBuilderPaletteLayout(sidebar, definitions);
        var contentHeight = GetEntityPaletteContentHeight( layout.HeaderHeight, layout.ItemHeight);
        var maxScroll = Math.Max(0, contentHeight - layout.ContentBounds.Height);
        _builderEntityPaletteScrollOffset = Math.Clamp(_builderEntityPaletteScrollOffset, 0, maxScroll);
    }

    private bool TryHitEntityPaletteAt(
        Point position,
        Rectangle sidebar,
        IReadOnlyList<CustomMapBuilderEntityDefinition> definitions,
        out EntityPaletteHit hit)
    {
        hit = default;
        var layout = GetModernGarrisonBuilderPaletteLayout(sidebar, definitions);
        if (!layout.ContentBounds.Contains(position))
        {
            return false;
        }

        var listWidth = GetEntityPaletteListWidth(layout);
        var y = layout.ContentBounds.Y - _builderEntityPaletteScrollOffset;
        foreach (var (category, _) in EntityPaletteCategories)
        {
            var headerBounds = new Rectangle(layout.ContentBounds.X, y, listWidth, layout.HeaderHeight);
            if (headerBounds.Contains(position))
            {
                hit = new EntityPaletteHit(true, true, category, -1);
                return true;
            }

            y += layout.HeaderHeight + ModernBuilderPaletteCategorySpacing;
            if (!IsEntityPaletteCategoryExpanded(category))
            {
                continue;
            }

            var categoryDefinitions = category == GarrisonBuilderEntityPaletteCategory.Logic
                ? GetLogicGarrisonBuilderEntityDefinitions()
                : definitions;
            for (var index = 0; index < categoryDefinitions.Count; index += 1)
            {
                var rowBounds = new Rectangle(layout.ContentBounds.X, y, listWidth, layout.ItemHeight);
                if (rowBounds.Contains(position))
                {
                    hit = new EntityPaletteHit(true, false, category, index);
                    return true;
                }

                y += layout.ItemHeight + ModernBuilderPaletteItemSpacing;
            }
        }

        return false;
    }

    private static bool EntityPaletteRowIntersectsClip(Rectangle row, Rectangle clip)
    {
        return row.Bottom > clip.Y && row.Y < clip.Bottom;
    }

    private void UpdateModernGarrisonBuilderPaletteScroll(MouseState mouse)
    {
        if (!_builderEntityPaletteVisible)
        {
            return;
        }

        var sidebarWidth = GetModernGarrisonBuilderSidebarWidth();
        var sidebar = new Rectangle(
            0,
            GetModernBuilderMenuBarHeight(),
            sidebarWidth,
            BuilderViewportHeight - GetModernBuilderMenuBarHeight() - GetModernGarrisonBuilderLayerStripHeight());
        var definitions = GetActiveGarrisonBuilderEntityDefinitions();
        ClampEntityPaletteScrollOffset(sidebar, definitions);
        var layout = GetModernGarrisonBuilderPaletteLayout(sidebar, definitions);
        if (!layout.HasScrollbar)
        {
            return;
        }

        var contentHeight = GetEntityPaletteContentHeight( layout.HeaderHeight, layout.ItemHeight);
        var maxScroll = Math.Max(0, contentHeight - layout.ContentBounds.Height);
        var trackBounds = GetEntityPaletteScrollbarTrackBounds(layout);
        if (TryHandleScrollbarRangeDrag(
                mouse,
                _previousMouse,
                ScrollbarOwners.GarrisonBuilderEntityPalette,
                trackBounds,
                ref _builderEntityPaletteScrollOffset,
                maxScroll,
                layout.ContentBounds.Height,
                contentHeight,
                minThumbHeight: 16))
        {
            return;
        }

        var wheelDelta = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        if (wheelDelta == 0)
        {
            return;
        }

        if (!layout.ContentBounds.Contains(mouse.Position) && !trackBounds.Contains(mouse.Position))
        {
            return;
        }

        var scrollStep = Math.Max(16, layout.ItemHeight);
        var delta = wheelDelta > 0 ? -scrollStep : scrollStep;
        _builderEntityPaletteScrollOffset = Math.Clamp(_builderEntityPaletteScrollOffset + delta, 0, maxScroll);
    }

    private void UpdateModernGarrisonBuilderPaletteHover(MouseState mouse)
    {
        _builderPaletteHoverCategory = null;
        _builderPaletteHoverEntityIndex = -1;
        _builderPaletteHoverLogicEntityIndex = -1;
        if (!_builderEntityPaletteVisible)
        {
            return;
        }

        var sidebarWidth = GetModernGarrisonBuilderSidebarWidth();
        var sidebar = new Rectangle(
            0,
            GetModernBuilderMenuBarHeight(),
            sidebarWidth,
            BuilderViewportHeight - GetModernBuilderMenuBarHeight() - GetModernGarrisonBuilderLayerStripHeight());
        var definitions = GetActiveGarrisonBuilderEntityDefinitions();
        if (!TryHitEntityPaletteAt(mouse.Position, sidebar, definitions, out var hit))
        {
            return;
        }

        if (hit.IsCategoryHeader)
        {
            _builderPaletteHoverCategory = hit.Category;
            return;
        }

        if (hit.Category == GarrisonBuilderEntityPaletteCategory.Logic)
        {
            _builderPaletteHoverLogicEntityIndex = hit.EntityDefinitionIndex;
            return;
        }

        _builderPaletteHoverEntityIndex = hit.EntityDefinitionIndex;
    }

    private void DrawEntityPaletteCategoryHeader(Rectangle bounds, string label, bool expanded, bool highlighted, MouseState mouse)
    {
        DrawGarrisonBuilderMenuButtonCentered(bounds, label, highlighted, relativeTextScale: 0.92f);
        var arrowBounds = new Rectangle(bounds.Right - 24, bounds.Y + 3, 20, bounds.Height - 6);
        DrawGarrisonBuilderCollapseChevronButton(arrowBounds, expanded, arrowBounds.Contains(mouse.Position));
    }

    private void DrawEntityPaletteEntityRow(
        Rectangle bounds,
        string label,
        bool selected,
        bool hovered,
        Rectangle clip)
    {
        if (!EntityPaletteRowIntersectsClip(bounds, clip))
        {
            return;
        }

        if (selected || hovered)
        {
            _spriteBatch.Draw(_pixel, bounds, selected ? new Color(88, 82, 74) : new Color(62, 58, 52));
        }

        const float horizontalPadding = 8f;
        const float relativeTextScale = 0.88f;
        var textScale = GetGarrisonBuilderBitmapFontScaleToFit(
            label,
            Math.Max(8f, bounds.Width - (horizontalPadding * 2f)),
            Math.Max(8f, bounds.Height - 4f),
            relativeTextScale);
        var textColor = selected ? Color.White : new Color(228, 220, 204);
        var measuredHeight = MeasureBitmapFontHeight(textScale);
        DrawBitmapFontText(
            label,
            new Vector2(bounds.X + horizontalPadding, bounds.Y + MathF.Max(2f, (bounds.Height - measuredHeight) * 0.5f)),
            textColor,
            textScale);
    }

    private void DrawEntityPaletteScrollbar(
        ModernGarrisonBuilderPaletteLayout layout,
        IReadOnlyList<CustomMapBuilderEntityDefinition> definitions)
    {
        if (!layout.HasScrollbar)
        {
            return;
        }

        var contentHeight = GetEntityPaletteContentHeight( layout.HeaderHeight, layout.ItemHeight);
        var trackBounds = GetEntityPaletteScrollbarTrackBounds(layout);
        _spriteBatch.Draw(_pixel, trackBounds, new Color(34, 32, 28));

        var maxOffset = Math.Max(1, contentHeight - layout.ContentBounds.Height);
        var thumbHeight = Math.Max(
            16,
            (int)MathF.Round(trackBounds.Height * (layout.ContentBounds.Height / (float)contentHeight)));
        var thumbTravel = Math.Max(0, trackBounds.Height - thumbHeight);
        var thumbY = trackBounds.Y + (int)MathF.Round((_builderEntityPaletteScrollOffset / (float)maxOffset) * thumbTravel);
        _spriteBatch.Draw(_pixel, new Rectangle(trackBounds.X, thumbY, trackBounds.Width, thumbHeight), new Color(120, 112, 100));
    }

    private void DrawModernGarrisonBuilderEntityPalette(Rectangle sidebar, MouseState mouse)
    {
        var definitions = GetActiveGarrisonBuilderEntityDefinitions();
        var layout = GetModernGarrisonBuilderPaletteLayout(sidebar, definitions);
        var listWidth = GetEntityPaletteListWidth(layout);
        var y = layout.ContentBounds.Y - _builderEntityPaletteScrollOffset;
        foreach (var (category, label) in EntityPaletteCategories)
        {
            var expanded = IsEntityPaletteCategoryExpanded(category);
            var headerBounds = new Rectangle(layout.ContentBounds.X, y, listWidth, layout.HeaderHeight);
            var headerHighlighted = _builderPaletteHoverCategory == category
                || headerBounds.Contains(mouse.Position);
            if (EntityPaletteRowIntersectsClip(headerBounds, layout.ContentBounds))
            {
                DrawEntityPaletteCategoryHeader(headerBounds, label, expanded, headerHighlighted, mouse);
            }

            y += layout.HeaderHeight + ModernBuilderPaletteCategorySpacing;
            if (!expanded)
            {
                continue;
            }

            var categoryDefinitions = category == GarrisonBuilderEntityPaletteCategory.Logic
                ? GetLogicGarrisonBuilderEntityDefinitions()
                : definitions;
            for (var index = 0; index < categoryDefinitions.Count; index += 1)
            {
                var definition = categoryDefinitions[index];
                var rowBounds = new Rectangle(layout.ContentBounds.X, y, listWidth, layout.ItemHeight);
                var selected = string.Equals(_builderSelectedEntityType, definition.Type, StringComparison.OrdinalIgnoreCase);
                var hovered = category == GarrisonBuilderEntityPaletteCategory.Logic
                    ? _builderPaletteHoverLogicEntityIndex == index
                    : _builderPaletteHoverEntityIndex == index;
                DrawEntityPaletteEntityRow(rowBounds, definition.Label, selected, hovered, layout.ContentBounds);
                y += layout.ItemHeight + ModernBuilderPaletteItemSpacing;
            }
        }

        DrawEntityPaletteScrollbar(layout, definitions);
    }

    private bool TryBeginGarrisonBuilderResize(Point screenPosition, Vector2 world)
    {
        if (_builderSelectedEntityIndex < 0 || _builderSelectedEntityIndex >= _builderEntities.Count)
        {
            return false;
        }

        var entity = _builderEntities[_builderSelectedEntityIndex];
        if (IsGarrisonBuilderForegroundSpriteResizable(entity))
        {
            if (!TryGetGarrisonBuilderForegroundSpriteWorldBounds(entity, out var spriteLeft, out var spriteTop, out var spriteWidth, out var spriteHeight))
            {
                return false;
            }

            var spriteHandles = GetGarrisonBuilderResizeHandlePoints(spriteLeft, spriteTop, spriteWidth, spriteHeight);
            foreach (var pair in spriteHandles)
            {
                var screen = BuilderWorldToScreen(pair.Value);
                var handleBounds = new Rectangle((int)screen.X - 5, (int)screen.Y - 5, 10, 10);
                if (!handleBounds.Contains(screenPosition))
                {
                    continue;
                }

                RecordGarrisonBuilderHistory();
                _builderActiveResizeHandle = pair.Key;
                _builderResizeAnchorWorld = world;
                _builderResizeStartLeft = spriteLeft;
                _builderResizeStartTop = spriteTop;
                _builderResizeStartWidth = spriteWidth;
                _builderResizeStartHeight = spriteHeight;
                return true;
            }

            return false;
        }

        if (IsGarrisonBuilderCustomSpriteResizable(entity))
        {
            if (!TryGetGarrisonBuilderCustomSpriteWorldBounds(entity, out var spriteLeft, out var spriteTop, out var spriteWidth, out var spriteHeight))
            {
                return false;
            }

            var spriteHandles = GetGarrisonBuilderResizeHandlePoints(spriteLeft, spriteTop, spriteWidth, spriteHeight);
            foreach (var pair in spriteHandles)
            {
                var screen = BuilderWorldToScreen(pair.Value);
                var handleBounds = new Rectangle((int)screen.X - 5, (int)screen.Y - 5, 10, 10);
                if (!handleBounds.Contains(screenPosition))
                {
                    continue;
                }

                RecordGarrisonBuilderHistory();
                _builderActiveResizeHandle = pair.Key;
                _builderResizeAnchorWorld = world;
                _builderResizeStartLeft = spriteLeft;
                _builderResizeStartTop = spriteTop;
                _builderResizeStartWidth = spriteWidth;
                _builderResizeStartHeight = spriteHeight;
                return true;
            }

            return false;
        }

        if (IsGarrisonBuilderSpritesheetResizable(entity))
        {
            if (!TryGetGarrisonBuilderSpritesheetWorldBounds(entity, out var sheetLeft, out var sheetTop, out var sheetWidth, out var sheetHeight))
            {
                return false;
            }

            var sheetHandles = GetGarrisonBuilderResizeHandlePoints(sheetLeft, sheetTop, sheetWidth, sheetHeight);
            foreach (var pair in sheetHandles)
            {
                var screen = BuilderWorldToScreen(pair.Value);
                var handleBounds = new Rectangle((int)screen.X - 5, (int)screen.Y - 5, 10, 10);
                if (!handleBounds.Contains(screenPosition))
                {
                    continue;
                }

                RecordGarrisonBuilderHistory();
                _builderActiveResizeHandle = pair.Key;
                _builderResizeAnchorWorld = world;
                _builderResizeStartLeft = sheetLeft;
                _builderResizeStartTop = sheetTop;
                _builderResizeStartWidth = sheetWidth;
                _builderResizeStartHeight = sheetHeight;
                return true;
            }

            return false;
        }

        if (!CustomMapBuilderEntityCatalog.TryGetDefinition(entity.Type, out var definition)
            || !IsGarrisonBuilderDefinitionScalable(definition))
        {
            return false;
        }

        if (!TryGetGarrisonBuilderEntityWorldBounds(entity, out var left, out var top, out var width, out var height))
        {
            return false;
        }

        var handles = GetGarrisonBuilderResizeHandlePoints(left, top, width, height);
        foreach (var pair in handles)
        {
            var screen = BuilderWorldToScreen(pair.Value);
            var handleBounds = new Rectangle((int)screen.X - 5, (int)screen.Y - 5, 10, 10);
            if (!handleBounds.Contains(screenPosition))
            {
                continue;
            }

            if (TryGetGarrisonBuilderEntityFrame(definition, entity, out _, out var origin))
            {
                _builderResizeOriginX = origin.X;
                _builderResizeOriginY = origin.Y;
            }
            else
            {
                var metrics = GetGarrisonBuilderEntityMetrics(definition, entity.Properties, 1f, 1f);
                if (UsesGarrisonBuilderCenterPlacementAnchor(entity.Type))
                {
                    _builderResizeOriginX = metrics.CenterX;
                    _builderResizeOriginY = metrics.CenterY;
                }
                else
                {
                    _builderResizeOriginX = metrics.OffsetX;
                    _builderResizeOriginY = metrics.OffsetY;
                }
            }

            RecordGarrisonBuilderHistory();
            _builderActiveResizeHandle = pair.Key;
            _builderResizeAnchorWorld = world;
            _builderResizeStartXScale = entity.XScale;
            _builderResizeStartYScale = entity.YScale;
            _builderResizeStartWidth = width;
            _builderResizeStartHeight = height;
            _builderResizeStartLeft = left;
            _builderResizeStartTop = top;
            return true;
        }

        return false;
    }

    private void ApplyGarrisonBuilderResizeDrag(Vector2 world)
    {
        if (_builderSelectedEntityIndex < 0 || _builderSelectedEntityIndex >= _builderEntities.Count)
        {
            return;
        }

        world = SnapGarrisonBuilderPoint(world);
        var entity = _builderEntities[_builderSelectedEntityIndex];
        if (IsGarrisonBuilderForegroundSpriteResizable(entity))
        {
            ApplyGarrisonBuilderForegroundSpriteResizeDrag(world);
            return;
        }

        if (IsGarrisonBuilderCustomSpriteResizable(entity))
        {
            ApplyGarrisonBuilderCustomSpriteResizeDrag(world);
            return;
        }

        if (IsGarrisonBuilderSpritesheetResizable(entity))
        {
            ApplyGarrisonBuilderSpritesheetResizeDrag(world);
            return;
        }

        if (!CustomMapBuilderEntityCatalog.TryGetDefinition(entity.Type, out var definition))
        {
            return;
        }

        var metrics = GetGarrisonBuilderEntityMetrics(definition, entity.Properties, 1f, 1f);
        GetGarrisonBuilderResizeMinimumExtents(entity.Type, metrics.Width, metrics.Height, out var minWidth, out var minHeight);
        var startRight = _builderResizeStartLeft + _builderResizeStartWidth;
        var startBottom = _builderResizeStartTop + _builderResizeStartHeight;
        var centerX = _builderResizeStartLeft + (_builderResizeStartWidth * 0.5f);
        var centerY = _builderResizeStartTop + (_builderResizeStartHeight * 0.5f);
        var newLeft = _builderResizeStartLeft;
        var newTop = _builderResizeStartTop;
        var newWidth = _builderResizeStartWidth;
        var newHeight = _builderResizeStartHeight;
        var lockAspect = _builderShiftHeld && IsGarrisonBuilderCornerResizeHandle(_builderActiveResizeHandle);
        var aspectRatio = _builderResizeStartWidth / MathF.Max(0.01f, _builderResizeStartHeight);

        if (_builderCtrlHeld)
        {
            ApplyGarrisonBuilderCenterResize(
                world,
                centerX,
                centerY,
                minWidth,
                minHeight,
                lockAspect,
                aspectRatio,
                ref newLeft,
                ref newTop,
                ref newWidth,
                ref newHeight);
        }
        else
        {
            switch (_builderActiveResizeHandle)
            {
                case GarrisonBuilderResizeHandle.TopLeft:
                    newLeft = world.X;
                    newTop = world.Y;
                    newWidth = startRight - newLeft;
                    newHeight = startBottom - newTop;
                    break;
                case GarrisonBuilderResizeHandle.Top:
                    newTop = world.Y;
                    newHeight = startBottom - newTop;
                    break;
                case GarrisonBuilderResizeHandle.TopRight:
                    newTop = world.Y;
                    newWidth = world.X - _builderResizeStartLeft;
                    newHeight = startBottom - newTop;
                    break;
                case GarrisonBuilderResizeHandle.Right:
                    newWidth = world.X - _builderResizeStartLeft;
                    break;
                case GarrisonBuilderResizeHandle.BottomRight:
                    newWidth = world.X - _builderResizeStartLeft;
                    newHeight = world.Y - _builderResizeStartTop;
                    break;
                case GarrisonBuilderResizeHandle.Bottom:
                    newHeight = world.Y - _builderResizeStartTop;
                    break;
                case GarrisonBuilderResizeHandle.BottomLeft:
                    newLeft = world.X;
                    newWidth = startRight - newLeft;
                    newHeight = world.Y - _builderResizeStartTop;
                    break;
                case GarrisonBuilderResizeHandle.Left:
                    newLeft = world.X;
                    newWidth = startRight - newLeft;
                    break;
            }

            if (lockAspect)
            {
                ApplyGarrisonBuilderAspectLockedCornerResize(
                    ref newLeft,
                    ref newTop,
                    ref newWidth,
                    ref newHeight,
                    _builderActiveResizeHandle,
                    _builderResizeStartLeft,
                    _builderResizeStartTop,
                    startRight,
                    startBottom,
                    aspectRatio);
            }
        }

        ClampGarrisonBuilderResizeBounds(
            minWidth,
            minHeight,
            startRight,
            startBottom,
            centerX,
            centerY,
            ref newLeft,
            ref newTop,
            ref newWidth,
            ref newHeight);

        var newXScale = MathF.Max(0.05f, newWidth / metrics.Width);
        var newYScale = MathF.Max(0.05f, newHeight / metrics.Height);
        float newX;
        float newY;
        if (UsesGarrisonBuilderCenterPlacementAnchor(entity.Type))
        {
            newX = newLeft + (newWidth * 0.5f);
            newY = newTop + (newHeight * 0.5f);
        }
        else
        {
            newX = newLeft + (_builderResizeOriginX * newXScale);
            newY = newTop + (_builderResizeOriginY * newYScale);
        }

        var updated = entity with
        {
            X = newX,
            Y = newY,
            XScale = newXScale,
            YScale = newYScale,
        };
        _builderEntities[_builderSelectedEntityIndex] = updated.NormalizeForEditing();
    }

    private static bool IsGarrisonBuilderCornerResizeHandle(GarrisonBuilderResizeHandle handle)
    {
        return handle is GarrisonBuilderResizeHandle.TopLeft
            or GarrisonBuilderResizeHandle.TopRight
            or GarrisonBuilderResizeHandle.BottomRight
            or GarrisonBuilderResizeHandle.BottomLeft;
    }

    private void ApplyGarrisonBuilderCenterResize(
        Vector2 world,
        float centerX,
        float centerY,
        float minWidth,
        float minHeight,
        bool lockAspect,
        float aspectRatio,
        ref float left,
        ref float top,
        ref float width,
        ref float height)
    {
        var minHalfWidth = minWidth * 0.5f;
        var minHalfHeight = minHeight * 0.5f;
        switch (_builderActiveResizeHandle)
        {
            case GarrisonBuilderResizeHandle.Right:
                width = MathF.Max(minWidth, (world.X - centerX) * 2f);
                left = centerX - (width * 0.5f);
                top = _builderResizeStartTop;
                height = _builderResizeStartHeight;
                return;
            case GarrisonBuilderResizeHandle.Left:
                width = MathF.Max(minWidth, (centerX - world.X) * 2f);
                left = centerX - (width * 0.5f);
                top = _builderResizeStartTop;
                height = _builderResizeStartHeight;
                return;
            case GarrisonBuilderResizeHandle.Top:
                height = MathF.Max(minHeight, (centerY - world.Y) * 2f);
                top = centerY - (height * 0.5f);
                left = _builderResizeStartLeft;
                width = _builderResizeStartWidth;
                return;
            case GarrisonBuilderResizeHandle.Bottom:
                height = MathF.Max(minHeight, (world.Y - centerY) * 2f);
                top = centerY - (height * 0.5f);
                left = _builderResizeStartLeft;
                width = _builderResizeStartWidth;
                return;
        }

        var halfWidth = minHalfWidth;
        var halfHeight = minHalfHeight;
        switch (_builderActiveResizeHandle)
        {
            case GarrisonBuilderResizeHandle.TopLeft:
                halfWidth = MathF.Max(minHalfWidth, centerX - world.X);
                halfHeight = MathF.Max(minHalfHeight, centerY - world.Y);
                break;
            case GarrisonBuilderResizeHandle.TopRight:
                halfWidth = MathF.Max(minHalfWidth, world.X - centerX);
                halfHeight = MathF.Max(minHalfHeight, centerY - world.Y);
                break;
            case GarrisonBuilderResizeHandle.BottomRight:
                halfWidth = MathF.Max(minHalfWidth, world.X - centerX);
                halfHeight = MathF.Max(minHalfHeight, world.Y - centerY);
                break;
            case GarrisonBuilderResizeHandle.BottomLeft:
                halfWidth = MathF.Max(minHalfWidth, centerX - world.X);
                halfHeight = MathF.Max(minHalfHeight, world.Y - centerY);
                break;
            default:
                return;
        }

        if (lockAspect)
        {
            if (halfWidth / MathF.Max(0.01f, aspectRatio) > halfHeight)
            {
                halfHeight = halfWidth / aspectRatio;
            }
            else
            {
                halfWidth = halfHeight * aspectRatio;
            }
        }

        width = halfWidth * 2f;
        height = halfHeight * 2f;
        left = centerX - halfWidth;
        top = centerY - halfHeight;
    }

    private static void ApplyGarrisonBuilderAspectLockedCornerResize(
        ref float left,
        ref float top,
        ref float width,
        ref float height,
        GarrisonBuilderResizeHandle handle,
        float startLeft,
        float startTop,
        float startRight,
        float startBottom,
        float aspectRatio)
    {
        var startWidth = startRight - startLeft;
        var startHeight = startBottom - startTop;
        var scale = MathF.Max(width / MathF.Max(0.01f, startWidth), height / MathF.Max(0.01f, startHeight));
        width = startWidth * scale;
        height = startHeight * scale;
        switch (handle)
        {
            case GarrisonBuilderResizeHandle.TopLeft:
                left = startRight - width;
                top = startBottom - height;
                break;
            case GarrisonBuilderResizeHandle.TopRight:
                left = startLeft;
                top = startBottom - height;
                break;
            case GarrisonBuilderResizeHandle.BottomRight:
                left = startLeft;
                top = startTop;
                break;
            case GarrisonBuilderResizeHandle.BottomLeft:
                left = startRight - width;
                top = startTop;
                break;
        }
    }

    private void ClampGarrisonBuilderResizeBounds(
        float minWidth,
        float minHeight,
        float startRight,
        float startBottom,
        float centerX,
        float centerY,
        ref float left,
        ref float top,
        ref float width,
        ref float height)
    {
        if (width < minWidth)
        {
            if (_builderCtrlHeld)
            {
                width = minWidth;
                left = centerX - (width * 0.5f);
            }
            else if (_builderActiveResizeHandle is GarrisonBuilderResizeHandle.Left
                or GarrisonBuilderResizeHandle.TopLeft
                or GarrisonBuilderResizeHandle.BottomLeft)
            {
                left = startRight - minWidth;
                width = minWidth;
            }
            else
            {
                width = minWidth;
            }
        }

        if (height < minHeight)
        {
            if (_builderCtrlHeld)
            {
                height = minHeight;
                top = centerY - (height * 0.5f);
            }
            else if (_builderActiveResizeHandle is GarrisonBuilderResizeHandle.Top
                or GarrisonBuilderResizeHandle.TopLeft
                or GarrisonBuilderResizeHandle.TopRight)
            {
                top = startBottom - minHeight;
                height = minHeight;
            }
            else
            {
                height = minHeight;
            }
        }
    }

    private void DrawGarrisonBuilderDashedLine(Vector2 screenStart, Vector2 screenEnd, Color color, float thickness = 1.5f)
    {
        var delta = screenEnd - screenStart;
        var length = delta.Length();
        if (length < 1f)
        {
            return;
        }

        var direction = delta / length;
        const float dashLength = 6f;
        const float gapLength = 4f;
        var traveled = 0f;
        while (traveled < length)
        {
            var segmentEnd = MathF.Min(traveled + dashLength, length);
            DrawGarrisonBuilderLine(
                screenStart + (direction * traveled),
                screenStart + (direction * segmentEnd),
                color,
                thickness);
            traveled += dashLength + gapLength;
        }
    }

    private static Dictionary<GarrisonBuilderResizeHandle, Vector2> GetGarrisonBuilderResizeHandlePoints(float left, float top, float width, float height)
    {
        var right = left + width;
        var bottom = top + height;
        var centerX = left + (width * 0.5f);
        var centerY = top + (height * 0.5f);
        return new Dictionary<GarrisonBuilderResizeHandle, Vector2>
        {
            [GarrisonBuilderResizeHandle.TopLeft] = new(left, top),
            [GarrisonBuilderResizeHandle.Top] = new(centerX, top),
            [GarrisonBuilderResizeHandle.TopRight] = new(right, top),
            [GarrisonBuilderResizeHandle.Right] = new(right, centerY),
            [GarrisonBuilderResizeHandle.BottomRight] = new(right, bottom),
            [GarrisonBuilderResizeHandle.Bottom] = new(centerX, bottom),
            [GarrisonBuilderResizeHandle.BottomLeft] = new(left, bottom),
            [GarrisonBuilderResizeHandle.Left] = new(left, centerY),
        };
    }

    private void BeginEditingGarrisonBuilderSelectedEntity()
    {
        if (_builderSelectedEntityIndex < 0 || _builderSelectedEntityIndex >= _builderEntities.Count)
        {
            return;
        }

        var entity = _builderEntities[_builderSelectedEntityIndex];
        _builderPropertyTarget = GarrisonBuilderPropertyTarget.SelectedMapEntity;
        _builderPropertyEditMode = GarrisonBuilderPropertyEditMode.List;
        _builderPropertyEditorTitle = $"{entity.Type} @ {entity.X:0},{entity.Y:0}";
        _builderPropertyScrollIndex = 0;
        _builderPropertyEditorValues = new Dictionary<string, string>(entity.Properties, StringComparer.OrdinalIgnoreCase);
        foreach (var key in _builderPropertyEditorValues.Keys.ToArray())
        {
            if (IsSkippedGarrisonBuilderProperty(key))
            {
                _builderPropertyEditorValues.Remove(key);
            }
        }

        if (CustomMapBuilderEntityCatalog.TryGetDefinition(entity.Type, out var definition))
        {
            foreach (var pair in definition.DefaultProperties)
            {
                _builderPropertyEditorValues.TryAdd(pair.Key, pair.Value);
            }
        }

        if (CustomMapCustomSpriteMetadata.IsCustomSpriteEntityType(entity.Type))
        {
            CustomMapCustomSpriteMetadata.EnsurePlacementDefaults(
                _builderPropertyEditorValues,
                _builderDocument.VisualScale);
        }

        if (SpritesheetMetadata.IsSpritesheetEntityType(entity.Type))
        {
            SpritesheetMetadata.EnsurePlacementDefaults(
                _builderPropertyEditorValues,
                _builderDocument.VisualScale);
        }

        SyncGarrisonBuilderSpawnPropertyEditorFields();
        SyncGarrisonBuilderControlPointPropertyEditorFields();
    }

    private void DrawModernGarrisonBuilderEditorOverlay(MouseState mouse)
    {
        var backdrop = new Color(26, 24, 20);
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, BuilderViewportWidth, BuilderViewportHeight), backdrop);

        var mapViewport = GetModernGarrisonBuilderMapViewport();
        _spriteBatch.Draw(_pixel, mapViewport, new Color(32, 30, 28));
        DrawGarrisonBuilderMap(mapViewport);
        DrawGarrisonBuilderMapPickDimming();

        DrawModernGarrisonBuilderEntityDecorations();
        DrawModernGarrisonBuilderSelectionAndLinks();
        DrawGarrisonBuilderObjectiveMapPickHighlights();
        DrawGarrisonBuilderLogicMapPickHighlights();
        DrawGarrisonBuilderExtendableAreaMapPickHighlights();
        DrawModernGarrisonBuilderMenuBarTabs(mouse);
        DrawModernGarrisonBuilderSidebar(mouse);
        DrawModernGarrisonBuilderToolbar(mouse);
        DrawModernGarrisonBuilderLayerStrip(mouse);
        DrawGarrisonBuilderGameModeMenu(mouse);
        DrawLegacyGarrisonBuilderPathPrompt();
        DrawGarrisonBuilderLayerOffsetOverlay(mouse);
        DrawModernGarrisonBuilderMenuDropdown(mouse);
        DrawGarrisonBuilderEntityOverlapPicker(mouse);
        DrawGarrisonBuilderEntityContextMenu(mouse);
        DrawGarrisonBuilderLayerContextMenu(mouse);
        DrawGarrisonBuilderObjectiveMapPickPrompt(mouse);
        DrawGarrisonBuilderLogicMapPickPrompt(mouse);
        DrawGarrisonBuilderEntityMapPickPrompt(mouse);
        DrawGarrisonBuilderMultiEntityMapPickPrompt(mouse);
        DrawGarrisonBuilderPropertyEditor(mouse);
        DrawGarrisonBuilderLogicRecolorDialog(mouse);
        DrawGarrisonBuilderLayerParallaxDialog(mouse);
        DrawGarrisonBuilderMapNameCollisionDialog(mouse);
        DrawGarrisonBuilderTransientStatus();
    }

    private void DrawGarrisonBuilderEntityOverlapPicker(MouseState mouse)
    {
        if (!_builderEntityOverlapPickerOpen)
        {
            return;
        }

        PruneGarrisonBuilderEntityOverlapPickerIndices();
        if (!_builderEntityOverlapPickerOpen)
        {
            return;
        }

        var menuBounds = GetGarrisonBuilderEntityOverlapPickerBounds();
        DrawMenuPanelBackdrop(menuBounds, 0.98f);
        for (var index = 0; index < _builderEntityOverlapPickerIndices.Count; index += 1)
        {
            var entityIndex = _builderEntityOverlapPickerIndices[index];
            var itemBounds = GetGarrisonBuilderEntityOverlapPickerItemBounds(menuBounds, index);
            var label = GetGarrisonBuilderEntityOverlapPickerLabel(entityIndex);
            var highlighted = itemBounds.Contains(mouse.Position)
                || entityIndex == _builderSelectedEntityIndex;
            DrawBuilderMenuButton(itemBounds, label, highlighted);
        }
    }

    private void DrawGarrisonBuilderEntityContextMenu(MouseState mouse)
    {
        if (!_builderEntityContextMenuOpen)
        {
            return;
        }

        var menuBounds = GetGarrisonBuilderEntityContextMenuBounds();
        DrawMenuPanelBackdrop(menuBounds, 0.98f);
        for (var index = 0; index < _builderEntityContextMenuActionsScratch.Count; index += 1)
        {
            var itemBounds = GetGarrisonBuilderEntityContextMenuItemBounds(menuBounds, index);
            var label = GetGarrisonBuilderEntityContextMenuLabel(_builderEntityContextMenuActionsScratch[index]);
            DrawBuilderMenuButton(itemBounds, label, itemBounds.Contains(mouse.Position));
        }
    }

    private void DrawGarrisonBuilderLayerContextMenu(MouseState mouse)
    {
        if (!_builderLayerContextMenuOpen)
        {
            return;
        }

        var menuBounds = GetGarrisonBuilderLayerContextMenuBounds();
        DrawMenuPanelBackdrop(menuBounds, 0.98f);
        var actions = GetGarrisonBuilderLayerContextMenuActions(_builderLayerContextMenuLayerIndex);
        for (var index = 0; index < actions.Count; index += 1)
        {
            var itemBounds = GetGarrisonBuilderLayerContextMenuItemBounds(menuBounds, index);
            var label = GetGarrisonBuilderLayerContextMenuLabel(actions[index], _builderLayerContextMenuLayerIndex);
            DrawBuilderMenuButton(itemBounds, label, itemBounds.Contains(mouse.Position));
        }
    }

    private float GetModernBuilderTextScale(float relativeScale)
    {
        return GetGarrisonBuilderRelativeBitmapFontScale(relativeScale);
    }

    private void DrawModernGarrisonBuilderMenuBarTabs(MouseState mouse)
    {
        var menuBar = GetModernGarrisonBuilderMenuBarBounds();
        DrawMenuPanelBackdrop(menuBar, 0.96f);
        var tabs = new (GarrisonBuilderMenuBarMenu Menu, string Label)[]
        {
            (GarrisonBuilderMenuBarMenu.File, "File"),
            (GarrisonBuilderMenuBarMenu.Edit, "Edit"),
            (GarrisonBuilderMenuBarMenu.View, "View"),
            (GarrisonBuilderMenuBarMenu.Map, "Map"),
        };
        var x = 8;
        foreach (var (menu, label) in tabs)
        {
            var tabBounds = new Rectangle(x, 2, ModernBuilderMenuTabWidth, GetModernBuilderMenuBarHeight() - 4);
            var open = _builderMenuBarOpenMenu == menu;
            DrawBuilderMenuButton(tabBounds, label, open || tabBounds.Contains(mouse.Position));
            x += ModernBuilderMenuTabWidth + 4;
        }

        if (_builderMenuBarOpenMenu != GarrisonBuilderMenuBarMenu.None)
        {
            return;
        }

        const string panHint = "WASD/MMB pan  wheel zoom  Tab palette";
        var hintScale = GetModernBuilderTextScale(0.9f);
        var hintWidth = MeasureBitmapFontWidth(panHint, hintScale);
        var hintLeft = x + 12;
        var hintMaxWidth = menuBar.Right - hintLeft - 8;
        if (hintMaxWidth >= hintWidth)
        {
            DrawBitmapFontText(
                panHint,
                new Vector2(hintLeft, menuBar.Y + 6f),
                new Color(200, 190, 168),
                hintScale);
        }
        else if (hintMaxWidth > 48f)
        {
            var fittedHintScale = GetGarrisonBuilderBitmapFontScaleToFit(panHint, hintMaxWidth, GetModernBuilderMenuBarHeight() - 8f, 0.9f);
            DrawBitmapFontText(
                panHint,
                new Vector2(hintLeft, menuBar.Y + 6f),
                new Color(200, 190, 168),
                fittedHintScale);
        }
    }

    private void DrawModernGarrisonBuilderMenuDropdown(MouseState mouse)
    {
        if (_builderMenuBarOpenMenu == GarrisonBuilderMenuBarMenu.None)
        {
            return;
        }

        var dropdown = GetModernGarrisonBuilderMenuDropdownBounds(_builderMenuBarOpenMenu);
        DrawMenuPanelBackdrop(dropdown, 0.98f);
        var items = GetModernGarrisonBuilderMenuItems(_builderMenuBarOpenMenu);
        var rowY = dropdown.Y + 6;
        foreach (var (label, _) in items)
        {
            var rowHeight = (int)GetGarrisonBuilderMinimumButtonHeight();
            var rowBounds = new Rectangle(dropdown.X + 4, rowY, dropdown.Width - 8, rowHeight);
            DrawBuilderMenuButton(rowBounds, label, rowBounds.Contains(mouse.Position));
            rowY += rowHeight + 4;
        }
    }

    private void DrawModernGarrisonBuilderToolbar(MouseState mouse)
    {
        var toolbar = GetModernGarrisonBuilderToolbarBounds();
        DrawMenuPanelBackdrop(toolbar, 0.92f);
        GetModernGarrisonBuilderToolbarButtonBounds(out var selectBounds, out var placeBounds, out var eraseBounds, out var centerBounds);
        var statusText = $"{GetGarrisonBuilderModeLabel(_builderSelectedGameMode)}  {_builderEntities.Count} ents  {_builderZoom:0.##}x";
        var statusMaxWidth = Math.Max(40f, selectBounds.X - toolbar.X - 20f);
        var statusScale = GetGarrisonBuilderBitmapFontScaleToFit(statusText, statusMaxWidth, GetModernBuilderToolbarHeight() - 8f, 0.88f);
        DrawBitmapFontText(
            statusText,
            new Vector2(toolbar.X + 12f, toolbar.Y + 10f),
            new Color(240, 228, 196),
            statusScale);

        DrawModernToolbarToolButton(selectBounds, "Select", _builderActiveTool == GarrisonBuilderTool.Select, mouse);
        DrawModernToolbarToolButton(placeBounds, "Place", _builderActiveTool == GarrisonBuilderTool.Place, mouse);
        DrawModernToolbarToolButton(eraseBounds, "Erase", _builderActiveTool == GarrisonBuilderTool.Erase, mouse);
        DrawBuilderMenuButton(centerBounds, "Center", centerBounds.Contains(mouse.Position));
        DrawModernGarrisonBuilderUnhideButton(mouse);
    }

    private void DrawModernToolbarToolButton(Rectangle bounds, string label, bool active, MouseState mouse)
    {
        DrawBuilderMenuButton(bounds, label, active || bounds.Contains(mouse.Position));
    }

    private void DrawBuilderMenuButton(Rectangle bounds, string label, bool highlighted, bool enabled = true)
    {
        DrawGarrisonBuilderMenuButtonCentered(bounds, label, highlighted, 1f, enabled);
    }

    private void DrawGarrisonBuilderBrownPanel(Rectangle bounds)
    {
        DrawRoundedRectangleOutline(
            bounds,
            new Color(54, 47, 41),
            new Color(213, 205, 188),
            outlineThickness: 2,
            radius: 8);
    }

    private void DrawModernGarrisonBuilderSidebar(MouseState mouse)
    {
        var sidebarWidth = GetModernGarrisonBuilderSidebarWidth();
        var sidebar = new Rectangle(0, GetModernBuilderMenuBarHeight(), sidebarWidth, BuilderViewportHeight - GetModernBuilderMenuBarHeight() - GetModernGarrisonBuilderLayerStripHeight());
        DrawMenuPanelBackdrop(sidebar, 0.96f);
        if (!_builderEntityPaletteVisible)
        {
            var expandBounds = new Rectangle(sidebar.X + 2, sidebar.Y + 8, sidebar.Width - 4, sidebar.Height - 16);
            DrawBuilderMenuButton(expandBounds, ">>", expandBounds.Contains(mouse.Position));
            return;
        }

        var labelScale = GetGarrisonBuilderBitmapFontScale();
        DrawBitmapFontText("Entities", new Vector2(10f, sidebar.Y + 10f), Color.White, labelScale);
        var compactButtonHeight = (int)GetGarrisonBuilderMinimumButtonHeight();
        var hideBounds = new Rectangle(sidebar.Right - 34, sidebar.Y + 6, 30, compactButtonHeight);
        DrawBuilderMenuButton(hideBounds, "<<", hideBounds.Contains(mouse.Position));
        TryGetModernGarrisonBuilderSidebarModeButtonBounds(sidebar, out var modeBounds);
        DrawBuilderMenuButton(modeBounds, "Mode " + GetGarrisonBuilderModeLabel(_builderSelectedGameMode), modeBounds.Contains(mouse.Position));

        var validation = CustomMapBuilderValidator.Validate(_builderDocument with { Entities = _builderEntities.ToArray() }, _builderSelectedGameMode);
        var validationColor = validation.IsValid ? new Color(150, 224, 160) : new Color(255, 214, 118);
        var validationText = validation.IsValid ? "Validation OK" : $"{validation.Issues.Count} issue(s)";
        var validationScale = GetModernBuilderTextScale(0.82f);
        var validationBounds = new Rectangle(
            10,
            modeBounds.Bottom + 6,
            sidebar.Width - 20,
            compactButtonHeight);
        var fittedValidationScale = GetGarrisonBuilderBitmapFontScaleToFit(validationText, validationBounds.Width, validationBounds.Height - 4f, 0.82f);
        var validationWidth = MeasureBitmapFontWidth(validationText, fittedValidationScale);
        var validationX = validationBounds.X + MathF.Max(0f, validationBounds.Width - validationWidth);
        var validationY = validationBounds.Y + ((validationBounds.Height - MeasureBitmapFontHeight(fittedValidationScale)) * 0.5f);
        DrawBitmapFontText(validationText, new Vector2(validationX, validationY), validationColor, fittedValidationScale);

        DrawModernGarrisonBuilderEntityPalette(sidebar, mouse);

        var textScale = GetGarrisonBuilderBitmapFontScale();
        DrawBitmapFontText(_builderStatus, new Vector2(12f, sidebar.Bottom - 24f), new Color(255, 226, 140), textScale);
    }

    private void DrawModernGarrisonBuilderLayerStrip(MouseState mouse)
    {
        var strip = GetModernGarrisonBuilderLayerStripBounds();
        DrawMenuPanelBackdrop(strip, 0.94f);
        DrawBitmapFontText("Layers", new Vector2(14f, strip.Y + 5f), Color.White, GetModernBuilderTextScale(0.95f));

        var collapseBounds = GetModernGarrisonBuilderLayerStripCollapseBounds();
        DrawGarrisonBuilderCollapseChevronButton(
            collapseBounds,
            _builderLayerStripExpanded,
            collapseBounds.Contains(mouse.Position));

        if (!_builderLayerStripExpanded)
        {
            return;
        }

        for (var index = 0; index <= 8; index += 1)
        {
            var label = index switch
            {
                7 => "BG",
                8 => "FG",
                _ => $"L{index + 1}",
            };
            var tabBounds = GetModernGarrisonBuilderLayerTabBounds(index);
            var selected = _builderLayerIndex == index;
            var hidden = IsGarrisonBuilderLayerHidden(index);
            var previewBounds = new Rectangle(tabBounds.X + 4, tabBounds.Y + 4, 24, tabBounds.Height - 8);
            var labelBounds = new Rectangle(
                previewBounds.Right + 2,
                tabBounds.Y + 8,
                Math.Max(12, tabBounds.Right - previewBounds.Right - 6),
                tabBounds.Height - 16);
            var highlighted = (selected || tabBounds.Contains(mouse.Position)) && !hidden;
            DrawBuilderMenuButton(labelBounds, label, highlighted, enabled: !hidden);
            DrawLayerStripPreview(previewBounds, index, hidden);
        }

        var markBounds = GetModernGarrisonBuilderLayerStripMarkBounds();
        DrawBuilderMenuButton(markBounds, "Mark", _builderLayerMarkModeEnabled || markBounds.Contains(mouse.Position));
    }

    private void DrawLayerStripPreview(Rectangle previewBounds, int layerIndex, bool hidden)
    {
        var backdrop = hidden ? new Color(40, 36, 32) : new Color(53, 47, 42);
        _spriteBatch.Draw(_pixel, previewBounds, backdrop);
        var previewTint = hidden ? Color.White * 0.35f : Color.White;
        if (layerIndex == 7)
        {
            var backgroundTexture = _builderBackgroundTexture ?? _builderDefaultBackgroundTexture;
            if (backgroundTexture is not null)
            {
                DrawGarrisonBuilderResourcePreview(backgroundTexture, previewBounds, previewTint);
                if (ShouldDrawGarrisonBuilderLayerMark(7))
                {
                    var previewScale = Math.Min(previewBounds.Width / (float)backgroundTexture.Width, previewBounds.Height / (float)backgroundTexture.Height);
                    DrawGarrisonBuilderLayerMarkOverlay(backgroundTexture, previewBounds.Location.ToVector2(), previewScale);
                }

                return;
            }

            DrawBitmapFontText("BG", previewBounds.Location.ToVector2() + new Vector2(4f, 4f), previewTint, GetModernBuilderTextScale(0.65f));
            return;
        }

        if (layerIndex == 8)
        {
            var foreground = GetGarrisonBuilderForegroundResource();
            var texture = foreground is null ? null : GetGarrisonBuilderResourceTexture(foreground.Value.Name);
            if (_builderShowForeground && texture is not null)
            {
                DrawGarrisonBuilderResourcePreview(texture, previewBounds, previewTint);
                if (ShouldDrawGarrisonBuilderLayerMark(8))
                {
                    var previewScale = Math.Min(previewBounds.Width / (float)texture.Width, previewBounds.Height / (float)texture.Height);
                    DrawGarrisonBuilderLayerMarkOverlay(texture, previewBounds.Location.ToVector2(), previewScale);
                }

                return;
            }

            DrawBitmapFontText(_builderShowForeground ? "FG" : "Off", previewBounds.Location.ToVector2() + new Vector2(4f, 4f), previewTint, GetModernBuilderTextScale(0.65f));
            return;
        }

        var layer = GetGarrisonBuilderLayer(layerIndex);
        if (!string.IsNullOrWhiteSpace(layer.ResourceName))
        {
            var texture = GetGarrisonBuilderResourceTexture(layer.ResourceName);
            if (texture is not null)
            {
                DrawGarrisonBuilderResourcePreview(texture, previewBounds, previewTint);
                if (ShouldDrawGarrisonBuilderLayerMark(layerIndex))
                {
                    var previewScale = Math.Min(previewBounds.Width / (float)texture.Width, previewBounds.Height / (float)texture.Height);
                    DrawGarrisonBuilderLayerMarkOverlay(texture, previewBounds.Location.ToVector2(), previewScale);
                }

                return;
            }
        }

        DrawBitmapFontText("—", previewBounds.Location.ToVector2() + new Vector2(8f, 6f), previewTint, GetModernBuilderTextScale(0.65f));
    }

    private void DrawModernGarrisonBuilderEntityDecorations()
    {
        for (var index = 0; index < _builderEntities.Count; index += 1)
        {
            if (IsGarrisonBuilderEntityHidden(index))
            {
                continue;
            }

            var entity = _builderEntities[index];
            if (entity.Type.Equals("directionalWall", StringComparison.OrdinalIgnoreCase))
            {
                if (index == _builderMapHoverEntityIndex)
                {
                    DrawGarrisonBuilderDirectionalWallHoverBadge(entity);
                }

                continue;
            }

            if (entity.Type.Equals("barrier", StringComparison.OrdinalIgnoreCase))
            {
                if (index == _builderMapHoverEntityIndex)
                {
                    DrawGarrisonBuilderBarrierHoverOverlay(entity);
                }

                continue;
            }

            if (TryBuildGarrisonBuilderEntityMapFlags(entity, out var team, out var flags)
                && (team.HasValue || flags.Length > 0))
            {
                DrawGarrisonBuilderEntityPropertyBadgeOverlay(entity, team, flags);
            }
        }
    }

    private static bool TryBuildGarrisonBuilderEntityMapFlags(
        CustomMapBuilderEntity entity,
        out Color? teamColor,
        out string flags)
    {
        teamColor = null;
        flags = string.Empty;
        var type = entity.Type;

        if (type.Equals("spawn", StringComparison.OrdinalIgnoreCase))
        {
            teamColor = GetGarrisonBuilderTeamBadgeColor(GetEntityProperty(entity, "team", "neutral"));
            AppendGarrisonBuilderEntityFlag(ref flags, GetEntityBool(entity, "forward", false), "F");
            if (GetEntityBool(entity, "forward", false))
            {
                flags += ForwardSpawnMetadata.ParsePriority(entity.Properties).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            return true;
        }

        if (type.Equals("medCabinet", StringComparison.OrdinalIgnoreCase))
        {
            AppendGarrisonBuilderEntityFlag(ref flags, GetEntityBool(entity, "heal", false), "H");
            AppendGarrisonBuilderEntityFlag(ref flags, GetEntityBool(entity, "refill", false), "R");
            AppendGarrisonBuilderEntityFlag(ref flags, GetEntityBool(entity, "uber", false), "U");
            return flags.Length > 0;
        }

        return false;
    }

    private static void AppendGarrisonBuilderEntityFlag(ref string flags, bool enabled, string token)
    {
        if (enabled)
        {
            flags += token;
        }
    }

    private static Color GetGarrisonBuilderTeamBadgeColor(string team)
    {
        return team.Equals("red", StringComparison.OrdinalIgnoreCase)
            ? new Color(224, 88, 76)
            : team.Equals("blue", StringComparison.OrdinalIgnoreCase)
                ? new Color(88, 130, 224)
                : new Color(0, 200, 0);
    }

    private void DrawGarrisonBuilderBarrierHoverOverlay(CustomMapBuilderEntity entity)
    {
        if (!TryGetGarrisonBuilderEntityWorldBounds(entity, out var left, out var top, out var _, out var _))
        {
            return;
        }

        var textScale = GetGarrisonBuilderBitmapFontScale();
        var letterFlags = string.Empty;
        var targets = BarrierConfiguration.FromProperties(entity.Properties).Targets;
        AppendGarrisonBuilderEntityFlag(ref letterFlags, targets.Blocks(BarrierTargetKind.RedPlayers), "R");
        AppendGarrisonBuilderEntityFlag(ref letterFlags, targets.Blocks(BarrierTargetKind.BluePlayers), "B");
        AppendGarrisonBuilderEntityFlag(ref letterFlags, targets.Blocks(BarrierTargetKind.RedShots), "r");
        AppendGarrisonBuilderEntityFlag(ref letterFlags, targets.Blocks(BarrierTargetKind.BlueShots), "b");
        AppendGarrisonBuilderEntityFlag(ref letterFlags, targets.Blocks(BarrierTargetKind.RedIntel), "I");
        AppendGarrisonBuilderEntityFlag(ref letterFlags, targets.Blocks(BarrierTargetKind.BlueIntel), "i");
        if (letterFlags.Length > 0)
        {
            var topLeft = BuilderWorldToScreen(new Vector2(left, top));
            DrawBitmapFontText(
                letterFlags,
                new Vector2((int)MathF.Round(topLeft.X) + 2, (int)MathF.Round(topLeft.Y) + 1),
                Color.White,
                textScale);
        }
    }

    private void DrawGarrisonBuilderDirectionalWallHoverBadge(CustomMapBuilderEntity entity)
    {
        if (!TryGetGarrisonBuilderEntityWorldBounds(entity, out var left, out var top, out var _, out var _))
        {
            return;
        }

        var configuration = DirectionalWallConfiguration.FromProperties(entity.Properties);
        var flags = DirectionalWallConfiguration.GetPassDirectionDisplayLabel(configuration.PassDirection)[0].ToString();
        if (configuration.AffectsPlayers)
        {
            flags += "P";
        }

        if (configuration.AffectsProjectiles)
        {
            flags += "S";
        }

        var topLeft = BuilderWorldToScreen(new Vector2(left, top));
        DrawBitmapFontText(
            flags,
            new Vector2((int)MathF.Round(topLeft.X) + 2, (int)MathF.Round(topLeft.Y) + 1),
            Color.White,
            GetGarrisonBuilderBitmapFontScale());
    }

    private void DrawGarrisonBuilderBarrierEdgeIndicators(
        float worldStartX,
        float worldStartY,
        float span,
        float step,
        bool vertical,
        string symbol,
        float textScale)
    {
        var symbolWidth = MeasureBitmapFontWidth(symbol, textScale);
        var symbolHeight = MeasureBitmapFontHeight(textScale);
        for (var offset = step * 0.5f; offset < span; offset += step)
        {
            var world = vertical
                ? new Vector2(worldStartX, worldStartY + offset)
                : new Vector2(worldStartX + offset, worldStartY);
            var screen = BuilderWorldToScreen(world);
            var drawX = vertical
                ? symbol.Equals(">", StringComparison.Ordinal) ? screen.X : screen.X - symbolWidth
                : screen.X - (symbolWidth * 0.5f);
            var drawY = vertical
                ? screen.Y - (symbolHeight * 0.5f)
                : symbol.Equals("v", StringComparison.Ordinal) ? screen.Y : screen.Y - symbolHeight;
            DrawBitmapFontText(symbol, new Vector2(drawX, drawY), Color.White, textScale);
        }
    }

    private void DrawGarrisonBuilderEntityPropertyBadgeOverlay(CustomMapBuilderEntity entity, Color? teamColor, string flags)
    {
        if (!TryGetGarrisonBuilderEntityWorldBounds(entity, out var left, out var top, out _, out _))
        {
            return;
        }

        const int badgeSize = 8;
        const int inset = 2;
        var topLeft = BuilderWorldToScreen(new Vector2(left, top));
        var badgeX = (int)MathF.Round(topLeft.X) + inset;
        var badgeY = (int)MathF.Round(topLeft.Y) + inset;
        var flagTextX = badgeX;
        if (teamColor.HasValue)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(badgeX, badgeY, badgeSize, badgeSize), teamColor.Value);
            flagTextX = badgeX + badgeSize + 3;
        }

        if (flags.Length > 0)
        {
            DrawBitmapFontText(
                flags,
                new Vector2(flagTextX, badgeY + 1f),
                Color.White,
                GetModernBuilderTextScale(0.68f));
        }
    }

    private void DrawModernGarrisonBuilderSelectionAndLinks()
    {
        DrawGarrisonBuilderEntityLinks();
        DrawGarrisonBuilderMapEntitySelectionHighlights();
        DrawGarrisonBuilderMultiEntityMapPickSelectionHighlights();
        DrawGarrisonBuilderAreaSelectionRectangle();
        DrawGarrisonBuilderMultiEntityMapPickAreaSelectionRectangle();

        if (GetGarrisonBuilderSelectedEntityCount() == 1
            && _builderSelectedEntityIndex >= 0
            && _builderSelectedEntityIndex < _builderEntities.Count
            && !IsGarrisonBuilderEntityHidden(_builderSelectedEntityIndex))
        {
            DrawSelectionResizeHandles(_builderEntities[_builderSelectedEntityIndex]);
        }
    }

    private void DrawGarrisonBuilderEntityLinks()
    {
        DrawGarrisonBuilderLogicSignalLinks();
        var selectedIndex = _builderSelectedEntityIndex;
        for (var entityIndex = 0; entityIndex < _builderEntities.Count; entityIndex += 1)
        {
            if (IsGarrisonBuilderEntityHidden(entityIndex))
            {
                continue;
            }

            var entity = _builderEntities[entityIndex];
            if (entity.Type.Equals("CapturePoint", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryFindNearestGarrisonBuilderControlPoint(entity, out var controlPoint))
                {
                    continue;
                }

                var highlighted = selectedIndex >= 0
                    && !IsGarrisonBuilderEntityHidden(selectedIndex)
                    && (_builderEntities[selectedIndex] == entity
                        || _builderEntities[selectedIndex] == controlPoint);
                var color = highlighted
                    ? new Color(140, 220, 255, 255)
                    : new Color(90, 180, 255, 200);
                DrawGarrisonBuilderEntityLink(entity, controlPoint, color);
                continue;
            }

            if (!IsGarrisonBuilderForwardSpawnEntity(entity)
                || IsGarrisonBuilderLegacyForwardSpawnEntity(entity))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(GetEntityProperty(entity.Properties, MapLogicMetadata.LogicSignalPropertyKey, string.Empty)))
            {
                continue;
            }

            if (!TryFindGarrisonBuilderSpawnObjective(entity, out var objective))
            {
                continue;
            }

            var spawnHighlighted = selectedIndex >= 0
                && !IsGarrisonBuilderEntityHidden(selectedIndex)
                && (_builderEntities[selectedIndex] == objective
                    || _builderEntities[selectedIndex] == entity
                    || IsLinkedObjectiveSelected(entity));
            var spawnColor = spawnHighlighted
                ? new Color(255, 230, 140, 255)
                : new Color(255, 210, 100, 200);
            DrawGarrisonBuilderEntityLink(entity, objective, spawnColor);
        }
    }

    private void DrawSelectionResizeHandles(CustomMapBuilderEntity entity)
    {
        if (!CustomMapBuilderEntityCatalog.TryGetDefinition(entity.Type, out var definition))
        {
            return;
        }

        if (!IsGarrisonBuilderDefinitionScalable(definition)
            && !IsGarrisonBuilderCustomSpriteResizable(entity)
            && !IsGarrisonBuilderForegroundSpriteResizable(entity)
            && !IsGarrisonBuilderSpritesheetResizable(entity))
        {
            return;
        }

        if (!TryGetGarrisonBuilderEntityWorldBounds(entity, out var left, out var top, out var width, out var height))
        {
            return;
        }

        foreach (var point in GetGarrisonBuilderResizeHandlePoints(left, top, width, height).Values)
        {
            var screen = BuilderWorldToScreen(point);
            _spriteBatch.Draw(_pixel, new Rectangle((int)screen.X - 4, (int)screen.Y - 4, 8, 8), new Color(213, 205, 188));
        }
    }

    private bool IsLinkedObjectiveSelected(CustomMapBuilderEntity spawn)
    {
        if (_builderSelectedEntityIndex < 0 || _builderSelectedEntityIndex >= _builderEntities.Count)
        {
            return false;
        }

        var selected = _builderEntities[_builderSelectedEntityIndex];
        var link = GetEntityProperty(spawn, "linkObjective", string.Empty);
        if (link.Length == 0)
        {
            var index = GetEntityInt(spawn, "objectiveIndex", 0);
            link = index > 0 ? $"controlPoint{index}" : string.Empty;
        }

        return selected.Type.Equals(link, StringComparison.OrdinalIgnoreCase)
            || (selected.Type.Equals("controlPoint", StringComparison.OrdinalIgnoreCase)
                && GetEntityInt(selected, "index", 0).ToString() == GetEntityProperty(spawn, "objectiveIndex", string.Empty));
    }

    private void DrawGarrisonBuilderEntityLink(CustomMapBuilderEntity source, CustomMapBuilderEntity target, Color color)
    {
        var start = BuilderWorldToScreen(GetGarrisonBuilderEntityLinkAnchor(source));
        var end = BuilderWorldToScreen(GetGarrisonBuilderEntityLinkAnchor(target));
        var linkScale = GetGarrisonBuilderLinkVisualScale();
        DrawGarrisonBuilderLine(start, end, color, GarrisonBuilderLinkLineThickness * linkScale);
        DrawGarrisonBuilderLinkEndpoint(start, color, linkScale);
        DrawGarrisonBuilderLinkEndpoint(end, color, linkScale);
    }

    private void DrawGarrisonBuilderLinkEndpoint(Vector2 point, Color color, float linkScale)
    {
        var size = Math.Max(2, (int)MathF.Round(GarrisonBuilderLinkEndpointSize * linkScale));
        var outlinePad = Math.Max(1, (int)MathF.Round(linkScale));
        var half = size / 2;
        var x = (int)MathF.Round(point.X) - half;
        var y = (int)MathF.Round(point.Y) - half;
        _spriteBatch.Draw(_pixel, new Rectangle(x - outlinePad, y - outlinePad, size + (outlinePad * 2), size + (outlinePad * 2)), Color.Black * 0.55f);
        _spriteBatch.Draw(_pixel, new Rectangle(x, y, size, size), color);
    }

    private void DrawGarrisonBuilderLine(Vector2 start, Vector2 end, Color color, float thickness = 2f)
    {
        var delta = end - start;
        var length = delta.Length();
        if (length < 0.5f)
        {
            return;
        }

        var angle = MathF.Atan2(delta.Y, delta.X);
        var midpoint = (start + end) * 0.5f;
        _spriteBatch.Draw(
            _pixel,
            midpoint,
            null,
            color,
            angle,
            new Vector2(0.5f, 0.5f),
            new Vector2(length, thickness),
            SpriteEffects.None,
            0f);
    }

    private static string GetEntityProperty(CustomMapBuilderEntity entity, string key, string fallback)
    {
        return GetEntityProperty(entity.Properties, key, fallback);
    }

    private static string GetEntityProperty(IReadOnlyDictionary<string, string> properties, string key, string fallback)
    {
        return properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;
    }

    private static bool GetEntityBool(CustomMapBuilderEntity entity, string key, bool fallback)
    {
        return GetEntityBool(entity.Properties, key, fallback);
    }

    private static bool GetEntityBool(IReadOnlyDictionary<string, string> properties, string key, bool fallback)
    {
        if (!properties.TryGetValue(key, out var value))
        {
            return fallback;
        }

        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetEntityInt(CustomMapBuilderEntity entity, string key, int fallback)
    {
        return GetEntityInt(entity.Properties, key, fallback);
    }

    private static int GetEntityInt(IReadOnlyDictionary<string, string> properties, string key, int fallback)
    {
        if (properties.TryGetValue(key, out var value)
            && int.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

}
