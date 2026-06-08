#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace OpenGarrison.Client;

public partial class Game1
{
    private enum GarrisonBuilderPathField
    {
        None,
        OpenMap,
        Background,
        Walkmask,
        Save,
        ResourceName,
        ResourcePath,
        ResourceOutputDirectory,
    }

    private enum GarrisonBuilderPropertyTarget
    {
        None,
        MapProperties,
        PlacementEntity,
        SelectedMapEntity,
    }

    private const string GarrisonBuilderMapPropertyNameKey = "$name";
    private const string GarrisonBuilderMapPropertyVisualScaleKey = "$visualScale";
    private const string GarrisonBuilderMapPropertyWalkmaskScaleKey = "$walkmaskScale";

    private static readonly string[] GarrisonBuilderMapPropertyRowOrder =
    [
        GarrisonBuilderMapPropertyNameKey,
        GarrisonBuilderMapPropertyVisualScaleKey,
        GarrisonBuilderMapPropertyWalkmaskScaleKey,
        ScrMapSettingsMetadata.ShowControlPointsPropertyKey,
        ScrMapSettingsMetadata.ScoreToWinPropertyKey,
        ScrMapSettingsMetadata.WinWhenScorePropertyKey,
        ScrMapSettingsMetadata.RoundEndWinPropertyKey,
        ScrMapSettingsMetadata.RedStartingScorePropertyKey,
        ScrMapSettingsMetadata.BlueStartingScorePropertyKey,
        ControlPointMapSettingsMetadata.OverrideInitialCpsPropertyKey,
        "background",
        "void",
    ];

    private enum GarrisonBuilderPropertyEditMode
    {
        List,
        NewKey,
        EditValue,
    }

    private enum GarrisonBuilderLayerParallaxEditField
    {
        None,
        XFactor,
        YFactor,
    }

    private enum LegacyBuilderPanelDragTarget
    {
        None,
        Action,
        Entity,
        Layer,
    }

    private const int BuilderPanelWidth = 360;
    private const int BuilderPanelPadding = 10;
    private const int BuilderButtonHeight = 26;
    private const int BuilderButtonGap = 6;
    private const int BuilderEntityButtonSize = 30;
    private const int LegacyBuilderEntityButtonSize = 28;
    private const int LegacyBuilderButtonWidth = 115;
    private const int LegacyBuilderHeaderWidth = 122;
    private const int LegacyBuilderButtonHeight = 22;
    private const int LegacyBuilderButtonSpriteWidth = 17;
    private const int LegacyBuilderLayerWidth = 160;
    private const int LegacyBuilderLayerHeight = 80;
    private const int LegacyBuilderEntityColumns = 5;
    private const int LegacyBuilderVisibleActionRows = 5;
    private const int LegacyBuilderResourceWidth = 160;
    private const int LegacyBuilderResourceVisibleRows = 6;
    private const float BuilderTextScaleMultiplier = 1.22f;
    private static readonly JsonSerializerOptions BuilderMetadataJsonOptions = new() { WriteIndented = true };
    private bool _builderEditorEnabled;
    private bool _builderPendingCameraCenter = true;
    private readonly Dictionary<string, LoadedGameMakerSprite> _builderCatalogSpriteCache = new(StringComparer.OrdinalIgnoreCase);
    private bool _builderShowBackground = true;
    private bool _builderShowWalkmask = true;
    private bool _builderShowGrid;
    private bool _builderGridAlign = true;
    private string _builderTransientStatus = string.Empty;
    private float _builderTransientStatusSecondsRemaining;
    private bool _builderDirty;
    private CustomMapBuilderDocument _builderDocument = CustomMapBuilderDocument.CreateEmpty("new_map");
    private readonly List<CustomMapBuilderEntity> _builderEntities = new();
    private readonly HashSet<int> _builderHiddenEntityIndices = new();
    private string _builderStatus = "builder disabled";
    private string _builderSavePath = string.Empty;
    private Vector2 _builderCamera;
    private string _builderSelectedEntityType = string.Empty;
    private Texture2D? _builderBackgroundTexture;
    private Texture2D? _builderWalkmaskTexture;
    private Texture2D? _builderEmbeddedWalkmaskTexture;
    private Texture2D? _builderDefaultBackgroundTexture;
    private Texture2D? _builderDefaultWalkmaskTexture;
    private string _builderLoadedBackgroundPath = string.Empty;
    private string _builderLoadedWalkmaskPath = string.Empty;
    private string _builderLoadedEmbeddedWalkmaskSection = string.Empty;
    private LoadedGameMakerSprite? _builderEntityButtonSprite;
    private LoadedGameMakerSprite? _builderButtonSprite;
    private LoadedGameMakerSprite? _builderMenuLayoutSprite;
    private LoadedGameMakerSprite? _builderGridSprite;
    private LoadedGameMakerSprite? _builderScrollButtonSprite;
    private bool _builderOwnsEntityButtonSprite;
    private bool _builderOwnsButtonSprite;
    private bool _builderOwnsMenuLayoutSprite;
    private bool _builderOwnsGridSprite;
    private bool _builderOwnsScrollButtonSprite;
    private GarrisonBuilderPathField _builderActivePathField;
    private string _builderOpenMapBuffer = string.Empty;
    private string _builderBackgroundPathBuffer = string.Empty;
    private string _builderWalkmaskPathBuffer = string.Empty;
    private string _builderSavePathBuffer = string.Empty;
    private int _builderPathCursorIndex;
    private int _builderPathSelectionStart;
    private int _builderActionScrollIndex;
    private int _builderLayerIndex = 7;
    private int _builderTooltipIndex = -1;
    private int _builderMapHoverEntityIndex = -1;
    private CustomMapBuilderGameMode _builderSelectedGameMode = CustomMapBuilderGameMode.Free;
    private bool _builderSymmetry;
    private bool _builderScaleMode = true;
    private bool _builderFastScrolling;
    private bool _builderPlacementDragging;
    private bool _builderPlacementScaleLock;
    private bool _builderEraseDragging;
    private Vector2 _builderPlacementStartWorld;
    private Vector2 _builderPlacementCurrentWorld;
    private Vector2 _builderPlacementPreviewWorld;
    private int _builderPlacementSnapXEntityIndex = -1;
    private int _builderPlacementSnapYEntityIndex = -1;
    private bool _builderShiftHeld;
    private bool _builderCtrlHeld;
    private bool _builderAltHeld;
    private float _builderEntityDragStartX;
    private float _builderEntityDragStartY;
    private Vector2 _builderEraseStartWorld;
    private Vector2 _builderEraseCurrentWorld;
    private bool _builderShowForeground = true;
    private bool _builderEditingLayerOffsets;
    private bool _builderLayerParallaxDialogOpen;
    private int _builderLayerParallaxDialogLayerIndex = -1;
    private GarrisonBuilderLayerParallaxEditField _builderLayerParallaxEditField;
    private string _builderLayerParallaxXBuffer = "1";
    private string _builderLayerParallaxYBuffer = "1";
    private int _builderLayerParallaxCursorIndex;
    private int _builderLayerParallaxSelectionStart;
    private bool _builderEntityCoordinatesAreWalkmaskPixels;
    private bool _builderLayerMarkModeEnabled;
    private bool _garrisonBuilderQuickTestActive;
    private bool _builderLayerOffsetDragging;
    private Point _builderLayerOffsetHoldMouse;
    private int _builderResourceScrollIndex;
    private string _builderPendingResourceName = string.Empty;
    private CustomMapBuilderResourceKind _builderPendingResourceKind = CustomMapBuilderResourceKind.GenericImage;
    private string _builderSelectedResourceName = string.Empty;
    private string _builderResourceNameBuffer = string.Empty;
    private string _builderResourcePathBuffer = string.Empty;
    private string _builderResourceOutputDirectoryBuffer = string.Empty;
    private readonly Dictionary<string, Texture2D> _builderResourceTextureCache = new(StringComparer.OrdinalIgnoreCase);
    private GarrisonBuilderPropertyTarget _builderPropertyTarget;
    private GarrisonBuilderPropertyEditMode _builderPropertyEditMode;
    private readonly Dictionary<string, string> _builderPlacementPropertyOverrides = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _builderPropertyEditorValues = new(StringComparer.OrdinalIgnoreCase);
    private string _builderPropertyEditorTitle = string.Empty;
    private string _builderPropertyEditKey = string.Empty;
    private string _builderPropertyEditBuffer = string.Empty;
    private int _builderPropertyCursorIndex;
    private int _builderPropertySelectionStart;
    private int _builderPropertyScrollIndex;
    private bool _builderObjectiveMapPickActive;
    private string _builderObjectiveMapPickTargetPropertyKey = string.Empty;
    private const int GarrisonBuilderHistoryCapacity = 30;
    private readonly List<GarrisonBuilderHistorySnapshot> _builderUndoStack = new();
    private readonly List<GarrisonBuilderHistorySnapshot> _builderRedoStack = new();
    private readonly List<int> _builderEntityOverlapPickScratch = new();
    private bool _builderHistoryCaptureSuspended;
    private readonly IReadOnlyList<CustomMapBuilderEntityDefinition> _builderEntityDefinitions = CustomMapBuilderEntityCatalog.Definitions;
    private Point _builderActionPanelOffset = Point.Zero;
    private Point _builderEntityPanelOffset = Point.Zero;
    private Point _builderLayerPanelOffset = Point.Zero;
    private int _builderActionVisibleRows = LegacyBuilderVisibleActionRows;
    private bool _builderActionExpanded = true;
    private float _builderActionExpandProgress = 1f;
    private bool _builderGameModeMenuOpen;
    private LegacyBuilderPanelDragTarget _builderPanelDragTarget = LegacyBuilderPanelDragTarget.None;
    private Point _builderPanelDragLastMouse;
    private bool _builderPanelDragHeaderToggleCandidate;
    private readonly LegacyBuilderActionDefinition[] _builderActionDefinitions =
    [
        new("Load map", false),
        new("Load BG", false),
        new("Load WM", false),
        new("Show BG", true),
        new("Show WM", true),
        new("Show grid", true),
        new("Show FG", true),
        new("Save & test", false),
        new("Test w/o save", false),
        new("Symmetry mode", true),
        new("Scale mode", true),
        new("Fast scrolling", true),
        new("Map properties", false),
        new("Add resource", false),
        new("Get resources", false),
        new("Load entities", false),
        new("Save entities", false),
        new("Clear entities", false),
    ];

    private void UpdateGarrisonBuilderEditor(KeyboardState keyboard, MouseState mouse, float deltaSeconds)
    {
        if (!_builderEditorEnabled)
        {
            return;
        }

        LoadGarrisonBuilderEditorAssets();
        UpdateGarrisonBuilderTransientStatus(deltaSeconds);
        _builderShiftHeld = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
        _builderCtrlHeld = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);
        _builderAltHeld = keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt);
        if (TryHandleGarrisonBuilderSaveShortcuts(keyboard))
        {
            return;
        }

        TryApplyPendingGarrisonBuilderCameraCenter();
        UpdateLegacyGarrisonBuilderAnimation(deltaSeconds);
        if (_builderUseModernUi)
        {
            SyncGarrisonBuilderActiveTool();
            UpdateModernGarrisonBuilderCamera(keyboard, mouse, deltaSeconds);
            UpdateModernGarrisonBuilderZoom(mouse, keyboard);
            if (TryHandleGarrisonBuilderHistoryShortcuts(keyboard))
            {
                return;
            }
        }

        if (UpdateGarrisonBuilderPropertyEditor(keyboard, mouse))
        {
            return;
        }

        if (UpdateGarrisonBuilderLayerParallaxDialog(keyboard, mouse))
        {
            return;
        }

        if (UpdateGarrisonBuilderPathKeyboard(keyboard))
        {
            return;
        }

        TryHandleGarrisonBuilderGridAlignShortcut(keyboard);

        if (!_builderUseModernUi && UpdateGarrisonBuilderLayerOffsetEditing(mouse))
        {
            return;
        }

        if (!_builderUseModernUi && UpdateLegacyGarrisonBuilderPanelDrag(mouse))
        {
            return;
        }

        if (_builderUseModernUi)
        {
            UpdateModernGarrisonBuilderEditor(keyboard, mouse, deltaSeconds);
            return;
        }

        if (IsKeyPressed(keyboard, Keys.Escape))
        {
            _builderGameModeMenuOpen = true;
            _builderStatus = "builder menu";
            return;
        }

        var moveSpeed = _builderFastScrolling ? 760f : 380f;
        if (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift))
        {
            moveSpeed *= 1.6f;
        }

        var delta = moveSpeed * Math.Max(0.001f, deltaSeconds);
        if (keyboard.IsKeyDown(Keys.A) || keyboard.IsKeyDown(Keys.Left))
        {
            _builderCamera.X -= delta;
        }

        if (keyboard.IsKeyDown(Keys.D) || keyboard.IsKeyDown(Keys.Right))
        {
            _builderCamera.X += delta;
        }

        if (keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.Up))
        {
            _builderCamera.Y -= delta;
        }

        if (keyboard.IsKeyDown(Keys.S) || keyboard.IsKeyDown(Keys.Down))
        {
            _builderCamera.Y += delta;
        }

        ClampGarrisonBuilderCamera();

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

        if (IsKeyPressed(keyboard, Keys.F7))
        {
            SaveGarrisonBuilderDocument();
        }

        UpdateLegacyGarrisonBuilderScroll(mouse);
        _builderTooltipIndex = GetLegacyGarrisonBuilderEntityHoverIndex(mouse.Position);
        UpdateGarrisonBuilderPlacementPreview(mouse);
        UpdateGarrisonBuilderMapEntityHover(mouse);

        if (IsKeyPressed(keyboard, Keys.Delete) && _builderEntities.Count > 0)
        {
            var removedIndex = _builderEntities.Count - 1;
            NotifyGarrisonBuilderEntityRemoved(removedIndex);
            _builderEntities.RemoveAt(removedIndex);
            UpdateGarrisonBuilderDocumentEntities();
            _builderDirty = true;
            _builderStatus = "removed latest entity";
        }

        if (mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released)
        {
            if (TryHandleLegacyGarrisonBuilderUiClick(mouse.Position, leftClick: true))
            {
                return;
            }

            if (_builderObjectiveMapPickActive)
            {
                var world = mouse.Position.ToVector2() + _builderCamera;
                if (TryPickGarrisonBuilderObjectiveAtWorld(world))
                {
                    return;
                }

                CancelGarrisonBuilderObjectiveMapPick();
                _builderStatus = "objective pick cancelled";
                return;
            }

            if (_builderLogicMapPickActive)
            {
                var world = mouse.Position.ToVector2() + _builderCamera;
                if (TryPickGarrisonBuilderLogicSourceAtWorld(world, out var logicSource))
                {
                    ApplyGarrisonBuilderLogicMapPick(logicSource);
                    return;
                }

                CancelGarrisonBuilderLogicMapPick();
                _builderStatus = "logic pick cancelled";
                return;
            }

            if (_builderEntityMapPickActive)
            {
                var world = mouse.Position.ToVector2() + _builderCamera;
                if (TryPickGarrisonBuilderEntityMapPickAtWorld(world, out var targetEntity))
                {
                    ApplyGarrisonBuilderEntityMapPick(targetEntity);
                    return;
                }

                CancelGarrisonBuilderEntityMapPick();
                _builderStatus = "entity pick cancelled";
                return;
            }

            if (_builderSelectedEntityType.Length > 0)
            {
                BeginGarrisonBuilderPlacement(mouse.Position.ToVector2() + _builderCamera);
            }
        }
        else if (mouse.LeftButton == ButtonState.Released && _previousMouse.LeftButton == ButtonState.Pressed && _builderPlacementDragging)
        {
            CommitGarrisonBuilderPlacement(mouse.Position.ToVector2() + _builderCamera);
        }
        else if (mouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Released)
        {
            if (TryHandleLegacyGarrisonBuilderUiClick(mouse.Position, leftClick: false))
            {
                return;
            }

            BeginGarrisonBuilderErase(mouse.Position.ToVector2() + _builderCamera);
        }
        else if (mouse.RightButton == ButtonState.Released && _previousMouse.RightButton == ButtonState.Pressed && _builderEraseDragging)
        {
            CommitGarrisonBuilderErase(mouse.Position.ToVector2() + _builderCamera);
        }
    }

    private void DrawGarrisonBuilderEditorOverlay(MouseState mouse)
    {
        if (!_builderEditorEnabled)
        {
            return;
        }

        LoadGarrisonBuilderEditorAssets();
        if (_builderUseModernUi)
        {
            DrawModernGarrisonBuilderEditorOverlay(mouse);
            return;
        }

        var mapArea = new Rectangle(0, 0, BuilderViewportWidth, BuilderViewportHeight);
        _spriteBatch.Draw(_pixel, mapArea, _builderShowBackground ? new Color(190, 190, 190, 255) : new Color(20, 20, 20, 255));
        DrawGarrisonBuilderMap(mapArea);
        DrawGarrisonBuilderEntityLinks();
        DrawLegacyGarrisonBuilderActionMenu(mouse);
        DrawLegacyGarrisonBuilderLayerMenu(mouse);
        if (_builderDocument.Resources.Count > 0)
        {
            DrawLegacyGarrisonBuilderResourceList(mouse);
        }

        DrawLegacyGarrisonBuilderEntityMenu(mouse);
        DrawGarrisonBuilderGameModeMenu(mouse);
        DrawLegacyGarrisonBuilderPathPrompt();
        DrawGarrisonBuilderPropertyEditor(mouse);
        DrawGarrisonBuilderLayerOffsetOverlay(mouse);
        DrawGarrisonBuilderLayerParallaxDialog(mouse);
        DrawGarrisonBuilderTransientStatus();
    }

    private float GetGarrisonBuilderEditorZoom()
    {
        return _builderUseModernUi ? _builderZoom : 1f;
    }

    private float GetGarrisonBuilderVisualDrawScale()
    {
        return _builderDocument.VisualScale * GetGarrisonBuilderEditorZoom();
    }

    private float GetGarrisonBuilderWalkmaskDrawScale()
    {
        return _builderDocument.Scale * GetGarrisonBuilderEditorZoom();
    }

    private Vector2 GetGarrisonBuilderBackgroundMapOffset()
    {
        return _builderUseModernUi
            ? BuilderWorldToScreen(Vector2.Zero)
            : -_builderCamera;
    }

    private Vector2 GetGarrisonBuilderWalkmaskWorldOffset()
    {
        var backgroundTexture = _builderBackgroundTexture ?? _builderDefaultBackgroundTexture;
        var walkmaskTexture = ResolveGarrisonBuilderWalkmaskTexture();
        if (backgroundTexture is null || walkmaskTexture is null)
        {
            return Vector2.Zero;
        }

        var offsetX = (backgroundTexture.Width * _builderDocument.VisualScale - walkmaskTexture.Width * _builderDocument.Scale) * 0.5f;
        var offsetY = (backgroundTexture.Height * _builderDocument.VisualScale - walkmaskTexture.Height * _builderDocument.Scale) * 0.5f;
        return new Vector2(offsetX, offsetY);
    }

    private Vector2 GetGarrisonBuilderWalkmaskMapOffset(Vector2 backgroundOffset)
    {
        if (_builderUseModernUi)
        {
            return BuilderWorldToScreen(GetGarrisonBuilderWalkmaskWorldOffset());
        }

        var worldOffset = GetGarrisonBuilderWalkmaskWorldOffset();
        return backgroundOffset + (worldOffset * GetGarrisonBuilderEditorZoom());
    }

    private void DrawGarrisonBuilderMap(Rectangle mapArea)
    {
        var visualDrawScale = GetGarrisonBuilderVisualDrawScale();
        var walkmaskDrawScale = GetGarrisonBuilderWalkmaskDrawScale();
        var backgroundOffset = GetGarrisonBuilderBackgroundMapOffset();
        var walkmaskOffset = GetGarrisonBuilderWalkmaskMapOffset(backgroundOffset);
        DrawGarrisonBuilderParallaxLayers(backgroundOffset, visualDrawScale);
        for (var parallaxLayer = 0; parallaxLayer <= 6; parallaxLayer += 1)
        {
            DrawGarrisonBuilderCustomSpritesForLayer((CustomMapSpriteLayerKind)(parallaxLayer + 1));
        }

        var backgroundTexture = _builderBackgroundTexture ?? _builderDefaultBackgroundTexture;
        if (_builderShowBackground && backgroundTexture is not null)
        {
            _spriteBatch.Draw(
                backgroundTexture,
                backgroundOffset,
                null,
                Color.White,
                0f,
                Vector2.Zero,
                new Vector2(visualDrawScale),
                SpriteEffects.None,
                0f);
            if (ShouldDrawGarrisonBuilderLayerMark(7))
            {
                DrawGarrisonBuilderLayerMarkOverlay(backgroundTexture, backgroundOffset, visualDrawScale);
            }
        }

        DrawGarrisonBuilderCustomSpritesForLayer(CustomMapSpriteLayerKind.Bg);

        var walkmaskTexture = ResolveGarrisonBuilderWalkmaskTexture();
        if (_builderShowWalkmask && walkmaskTexture is not null)
        {
            _spriteBatch.Draw(
                walkmaskTexture,
                walkmaskOffset,
                null,
                Color.White * 0.45f,
                0f,
                Vector2.Zero,
                new Vector2(walkmaskDrawScale),
                SpriteEffects.None,
                0f);
        }

        if (_builderShowGrid)
        {
            DrawGarrisonBuilderGrid(mapArea);
        }

        DrawGarrisonBuilderPlacementPreview();
        DrawGarrisonBuilderErasePreview();

        for (var entityIndex = 0; entityIndex < _builderEntities.Count; entityIndex += 1)
        {
            var entity = _builderEntities[entityIndex];
            if (IsGarrisonBuilderEntityHidden(entityIndex)
                || CustomMapCustomSpriteMetadata.IsCustomSpriteEntityType(entity.Type))
            {
                continue;
            }

            var screen = _builderUseModernUi
                ? BuilderWorldToScreen(new Vector2(entity.X, entity.Y))
                : new Vector2(entity.X, entity.Y) - _builderCamera;
            if (CustomMapBuilderEntityCatalog.TryGetDefinition(entity.Type, out var definition)
                && TryDrawGarrisonBuilderEntitySprite(definition, entity, Color.White * 0.9f))
            {
                if (ControlPointOwnershipResolver.IsControlPointEntity(entity.Type))
                {
                    DrawGarrisonBuilderControlPointIndexOverlay(entity);
                }

                if (entityIndex == _builderMapHoverEntityIndex)
                {
                    DrawGarrisonBuilderEntityMapLabel(entity);
                }

                continue;
            }

            var markerSize = Math.Max(8, (int)MathF.Round(10f * (_builderUseModernUi ? _builderZoom : 1f)));
            var half = markerSize / 2;
            var bounds = new Rectangle((int)MathF.Round(screen.X) - half, (int)MathF.Round(screen.Y) - half, markerSize, markerSize);
            _spriteBatch.Draw(_pixel, bounds, GetGarrisonBuilderEntityColor(entity.Type));
            if (entityIndex == _builderMapHoverEntityIndex)
            {
                DrawGarrisonBuilderEntityMapLabel(entity, screen + new Vector2(half + 2f, -16f));
            }
        }

        DrawGarrisonBuilderPlacementAlignmentGuides();
        DrawGarrisonBuilderCustomSpritesForLayer(CustomMapSpriteLayerKind.Fg);
        DrawGarrisonBuilderForeground(backgroundOffset, visualDrawScale);
    }

    private void DrawGarrisonBuilderParallaxLayers(Vector2 mapOffset, float mapScale)
    {
        foreach (var layer in _builderDocument.ParallaxLayers.OrderBy(static entry => entry.Index))
        {
            if (!layer.Visible || string.IsNullOrWhiteSpace(layer.ResourceName))
            {
                continue;
            }

            var texture = GetGarrisonBuilderResourceTexture(layer.ResourceName);
            if (texture is null)
            {
                continue;
            }

            var drawPosition = GetGarrisonBuilderLayerAlignedMapPosition(mapOffset, mapScale, texture);
            _spriteBatch.Draw(
                texture,
                drawPosition,
                null,
                Color.White,
                0f,
                Vector2.Zero,
                new Vector2(mapScale),
                SpriteEffects.None,
                0f);
            if (ShouldDrawGarrisonBuilderLayerMark(layer.Index))
            {
                DrawGarrisonBuilderLayerMarkOverlay(texture, drawPosition, mapScale);
            }
        }
    }

    private bool ShouldDrawGarrisonBuilderLayerMark(int layerIndex)
    {
        return _builderLayerMarkModeEnabled && _builderLayerIndex == layerIndex;
    }

    private void DrawGarrisonBuilderLayerMarkOverlay(Texture2D texture, Vector2 position, float mapScale)
    {
        DrawGarrisonBuilderLayerMarkOverlay(texture, position, new Vector2(mapScale));
    }

    private void DrawGarrisonBuilderLayerMarkOverlay(Texture2D texture, Vector2 position, Vector2 scale)
    {
        _spriteBatch.Draw(
            texture,
            position,
            null,
            new Color(255, 64, 64) * 0.4f,
            0f,
            Vector2.Zero,
            scale,
            SpriteEffects.None,
            0f);
    }

    private void ToggleGarrisonBuilderLayerMarkMode()
    {
        _builderLayerMarkModeEnabled = !_builderLayerMarkModeEnabled;
        _builderStatus = _builderLayerMarkModeEnabled
            ? $"marking {GetLegacyGarrisonBuilderLayerName()}"
            : "mark off";
    }

    private bool IsGarrisonBuilderLayerHidden(int layerIndex)
    {
        return layerIndex switch
        {
            7 => !_builderShowBackground,
            8 => !_builderShowForeground,
            >= 0 and <= 6 => !GetGarrisonBuilderLayer(layerIndex).Visible,
            _ => false,
        };
    }

    private bool IsGarrisonBuilderSelectedLayerHidden()
    {
        return IsGarrisonBuilderLayerHidden(_builderLayerIndex);
    }

    private void ToggleGarrisonBuilderLayerHide(int layerIndex)
    {
        RecordGarrisonBuilderHistory();
        if (layerIndex == 7)
        {
            _builderShowBackground = !_builderShowBackground;
            _builderStatus = _builderShowBackground ? "background shown" : "background hidden";
            return;
        }

        if (layerIndex == 8)
        {
            _builderShowForeground = !_builderShowForeground;
            _builderStatus = _builderShowForeground ? "foreground shown" : "foreground hidden";
            return;
        }

        if (layerIndex is < 0 or > 6)
        {
            _builderStatus = "select a layer";
            return;
        }

        var layer = GetGarrisonBuilderLayer(layerIndex);
        var nowVisible = !layer.Visible;
        SetGarrisonBuilderLayerVisible(layerIndex, nowVisible);
        _builderStatus = nowVisible
            ? $"layer {layerIndex + 1} shown"
            : $"layer {layerIndex + 1} hidden";
    }

    private void ToggleGarrisonBuilderSelectedLayerHide()
    {
        ToggleGarrisonBuilderLayerHide(_builderLayerIndex);
    }

    private void SetGarrisonBuilderLayerVisible(int index, bool visible)
    {
        var layer = GetGarrisonBuilderLayer(index);
        var layers = _builderDocument.ParallaxLayers
            .Where(existing => existing.Index != index)
            .Append((layer with { Visible = visible }).NormalizeForEditing())
            .OrderBy(existing => existing.Index)
            .ToArray();
        _builderDocument = _builderDocument with { ParallaxLayers = layers };
        _builderDirty = true;
    }

    private Vector2 GetGarrisonBuilderLayerAlignedMapPosition(Vector2 mapOffset, float mapScale, Texture2D layerTexture)
    {
        var backgroundTexture = _builderBackgroundTexture ?? _builderDefaultBackgroundTexture;
        if (backgroundTexture is null)
        {
            return mapOffset;
        }

        if (_builderUseModernUi)
        {
            var worldOffset = new Vector2(
                (backgroundTexture.Width - layerTexture.Width) * _builderDocument.VisualScale * 0.5f,
                (backgroundTexture.Height - layerTexture.Height) * _builderDocument.VisualScale * 0.5f);
            return BuilderWorldToScreen(worldOffset);
        }

        var offsetX = (backgroundTexture.Width - layerTexture.Width) * mapScale * 0.5f;
        var offsetY = (backgroundTexture.Height - layerTexture.Height) * mapScale * 0.5f;
        return mapOffset + new Vector2(offsetX, offsetY);
    }

    private void BeginLoadForGarrisonBuilderLayer(int layerIndex)
    {
        if (layerIndex == 7)
        {
            BeginEditingGarrisonBuilderPath(GarrisonBuilderPathField.Background);
            return;
        }

        if (layerIndex == 8)
        {
            BeginAddingGarrisonBuilderResource(CustomMapBuilderResourceKind.Foreground, "bg_foreground");
            return;
        }

        if (layerIndex is >= 0 and <= 6)
        {
            BeginAddingGarrisonBuilderResource(CustomMapBuilderResourceKind.ParallaxLayer, $"bg_layer{layerIndex}");
            return;
        }

        _builderStatus = "select a layer to load";
    }

    private void BeginLoadForCurrentGarrisonBuilderLayer()
    {
        BeginLoadForGarrisonBuilderLayer(_builderLayerIndex);
    }

    private void DrawGarrisonBuilderForeground(Vector2 mapOffset, float mapScale)
    {
        var foreground = GetGarrisonBuilderForegroundResource();
        if (foreground is null)
        {
            return;
        }

        var texture = GetGarrisonBuilderResourceTexture(foreground.Value.Name);
        if (texture is null)
        {
            return;
        }

        if (!_builderShowForeground)
        {
            return;
        }

        _spriteBatch.Draw(
            texture,
            mapOffset,
            null,
            Color.White,
            0f,
            Vector2.Zero,
            new Vector2(mapScale),
            SpriteEffects.None,
            0f);
        if (ShouldDrawGarrisonBuilderLayerMark(8))
        {
            DrawGarrisonBuilderLayerMarkOverlay(texture, mapOffset, mapScale);
        }
    }

    private void DrawGarrisonBuilderGrid(Rectangle mapArea)
    {
        const int gridSize = 12;
        var visibleWidth = _builderUseModernUi ? mapArea.Width / _builderZoom : mapArea.Width;
        var visibleHeight = _builderUseModernUi ? mapArea.Height / _builderZoom : mapArea.Height;
        var gridWorldSize = gridSize / (_builderUseModernUi ? GetGarrisonBuilderEntityCoordinateScale() : 1f);
        var startX = (int)MathF.Floor(_builderCamera.X / gridSize) * gridWorldSize;
        var endX = (int)MathF.Ceiling((_builderCamera.X + visibleWidth) / gridSize) * gridWorldSize;
        var startY = (int)MathF.Floor(_builderCamera.Y / gridSize) * gridWorldSize;
        var endY = (int)MathF.Ceiling((_builderCamera.Y + visibleHeight) / gridSize) * gridWorldSize;
        for (var x = startX; x <= endX; x += gridWorldSize)
        {
            for (var y = startY; y <= endY; y += gridWorldSize)
            {
                var screen = _builderUseModernUi
                    ? BuilderWorldToScreen(new Vector2(x, y))
                    : new Vector2(x - _builderCamera.X, y - _builderCamera.Y);
                if (_builderGridSprite is not null && _builderGridSprite.Frames.Count > 0)
                {
                    var gridScale = _builderUseModernUi ? _builderZoom : 1f;
                    DrawLoadedSpriteFrame(
                        _builderGridSprite.Frames[0],
                        screen,
                        null,
                        Color.White,
                        0f,
                        Vector2.Zero,
                        new Vector2(gridScale),
                        SpriteEffects.None,
                        0f);
                }
                else
                {
                    _spriteBatch.Draw(_pixel, new Rectangle((int)screen.X, (int)screen.Y, 1, 1), new Color(255, 255, 255, 80));
                }
            }
        }
    }

    private void DrawLegacyGarrisonBuilderActionMenu(MouseState mouse)
    {
        var bounds = GetLegacyGarrisonBuilderActionBounds();
        var bodyHeight = bounds.Height;
        DrawLegacyBuilderPanelHeader(bounds.X, bounds.Y - LegacyBuilderButtonHeight, LegacyBuilderHeaderWidth);
        if (bodyHeight > 0)
        {
            DrawLegacyBuilderPanelBody(new Rectangle(bounds.X, bounds.Y, LegacyBuilderHeaderWidth, bodyHeight));
        }

        var visibleRows = GetLegacyGarrisonBuilderVisibleActionRowsForInput();
        _builderActionScrollIndex = Math.Clamp(_builderActionScrollIndex, 0, Math.Max(0, _builderActionDefinitions.Length - visibleRows));
        for (var row = 0; row < visibleRows; row += 1)
        {
            var actionIndex = _builderActionScrollIndex + (visibleRows - 1 - row);
            var action = _builderActionDefinitions[actionIndex];
            var y = bounds.Bottom - ((row + 1) * LegacyBuilderButtonHeight);
            var toggled = IsLegacyBuilderActionToggled(action.Label);
            DrawLegacyBuilderButton(new Rectangle(bounds.X, y, LegacyBuilderButtonWidth, LegacyBuilderButtonHeight), action.Label, toggled, mouse);
        }

        DrawLegacyBuilderScrollbar(bounds, visibleRows);
    }

    private void DrawLegacyGarrisonBuilderEntityMenu(MouseState mouse)
    {
        var bounds = GetLegacyGarrisonBuilderEntityBounds();
        var definitions = GetActiveGarrisonBuilderEntityDefinitions();
        DrawLegacyBuilderPanelHeader(bounds.X, bounds.Y, bounds.Width);
        DrawLegacyBuilderPanelBody(new Rectangle(bounds.X, bounds.Y + LegacyBuilderButtonHeight, bounds.Width, bounds.Height - LegacyBuilderButtonHeight));
        DrawGarrisonBuilderText(GetGarrisonBuilderModeLabel(_builderSelectedGameMode), bounds.X + 5, bounds.Y + 2, Color.Black, 0.78f);

        for (var index = 0; index < definitions.Count; index += 1)
        {
            var column = index % LegacyBuilderEntityColumns;
            var row = index / LegacyBuilderEntityColumns;
            var buttonBounds = new Rectangle(
                bounds.X + (column * LegacyBuilderEntityButtonSize),
                bounds.Y + LegacyBuilderButtonHeight + (row * LegacyBuilderEntityButtonSize),
                LegacyBuilderEntityButtonSize,
                LegacyBuilderEntityButtonSize);
            var definition = definitions[index];
            var selected = string.Equals(_builderSelectedEntityType, definition.Type, StringComparison.OrdinalIgnoreCase);
            if (_builderEntityButtonSprite is not null
                && definition.IconFrame + (selected ? 1 : 0) >= 0
                && definition.IconFrame + (selected ? 1 : 0) < _builderEntityButtonSprite.Frames.Count)
            {
                DrawLoadedSpriteFrame(_builderEntityButtonSprite.Frames[definition.IconFrame + (selected ? 1 : 0)], buttonBounds, Color.White);
            }
            else
            {
                _spriteBatch.Draw(_pixel, buttonBounds, selected ? new Color(190, 190, 190) : new Color(120, 120, 120));
                DrawGarrisonBuilderText(definition.Type[..Math.Min(2, definition.Type.Length)].ToUpperInvariant(), buttonBounds.X + 6, buttonBounds.Y + 7, Color.Black, 0.7f);
            }
        }

        if (_builderTooltipIndex >= 0 && _builderTooltipIndex < definitions.Count)
        {
            DrawLegacyGarrisonBuilderTooltip(mouse.Position, definitions[_builderTooltipIndex].Description);
        }
    }

    private void DrawLegacyGarrisonBuilderLayerMenu(MouseState mouse)
    {
        var bounds = GetLegacyGarrisonBuilderLayerBounds();
        DrawLegacyBuilderPanelHeader(bounds.X, bounds.Y, LegacyBuilderLayerWidth);
        DrawLegacyBuilderPanelBody(new Rectangle(bounds.X, bounds.Y + LegacyBuilderButtonHeight, LegacyBuilderLayerWidth, LegacyBuilderLayerHeight));

        var upBounds = new Rectangle(bounds.X + 3, bounds.Y + LegacyBuilderButtonHeight + 3, 20, 20);
        var downBounds = new Rectangle(bounds.X + 3, bounds.Y + LegacyBuilderButtonHeight + LegacyBuilderLayerHeight - 23, 20, 20);
        DrawLegacyBuilderSquareButton(upBounds, _builderLayerIndex > 0, scrollFrame: 0);
        DrawLegacyBuilderSquareButton(downBounds, _builderLayerIndex < 8, scrollFrame: 1);

        var clearBounds = new Rectangle(bounds.X + 27, bounds.Y + LegacyBuilderButtonHeight + LegacyBuilderLayerHeight - 23, 63, 20);
        var offsetsBounds = new Rectangle(bounds.X + 97, bounds.Y + LegacyBuilderButtonHeight + LegacyBuilderLayerHeight - 23, 60, 20);
        DrawLegacyBuilderRectButton(clearBounds, "Clear", mouse);
        DrawLegacyBuilderRectButton(offsetsBounds, _builderEditingLayerOffsets ? "Save" : "Offsets", mouse);

        DrawGarrisonBuilderText(GetLegacyGarrisonBuilderLayerName(), bounds.X + 6, bounds.Y + LegacyBuilderButtonHeight + 35, Color.Black, 1f);
        var previewBounds = new Rectangle(bounds.X + 27, bounds.Y + LegacyBuilderButtonHeight + 3, 127, 51);
        _spriteBatch.Draw(_pixel, previewBounds, new Color(210, 210, 210));
        if (_builderLayerIndex == 7 && (_builderBackgroundTexture ?? _builderDefaultBackgroundTexture) is { } backgroundTexture)
        {
            var scale = Math.Min(previewBounds.Width / (float)backgroundTexture.Width, previewBounds.Height / (float)backgroundTexture.Height);
            _spriteBatch.Draw(backgroundTexture, previewBounds.Location.ToVector2(), null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
        else if (_builderLayerIndex == 7)
        {
            DrawGarrisonBuilderText("BG", previewBounds.X + 46, previewBounds.Y + 18, Color.Black, 1f);
        }
        else if (_builderLayerIndex == 8)
        {
            var foreground = GetGarrisonBuilderForegroundResource();
            var texture = foreground is null ? null : GetGarrisonBuilderResourceTexture(foreground.Value.Name);
            if (_builderShowForeground && texture is not null)
            {
                DrawGarrisonBuilderResourcePreview(texture, previewBounds);
            }
            else
            {
                DrawGarrisonBuilderText(_builderShowForeground ? "FG" : "Off", previewBounds.X + 46, previewBounds.Y + 18, Color.Black, 1f);
            }
        }
        else
        {
            var layer = GetGarrisonBuilderLayer(_builderLayerIndex);
            var texture = string.IsNullOrWhiteSpace(layer.ResourceName) ? null : GetGarrisonBuilderResourceTexture(layer.ResourceName);
            if (texture is not null)
            {
                DrawGarrisonBuilderResourcePreview(texture, previewBounds);
            }
            else
            {
                DrawGarrisonBuilderText($"Layer {_builderLayerIndex + 1}", previewBounds.X + 36, previewBounds.Y + 18, Color.Black, 0.8f);
            }
        }

        if (!string.IsNullOrWhiteSpace(_builderSelectedResourceName))
        {
            DrawGarrisonBuilderText(_builderSelectedResourceName, bounds.X + 6, bounds.Y + LegacyBuilderButtonHeight + 18, Color.Black, 0.58f);
        }
    }

    private void DrawLegacyGarrisonBuilderResourceList(MouseState mouse)
    {
        var bounds = GetLegacyGarrisonBuilderResourceBounds();
        DrawLegacyBuilderPanelHeader(bounds.X, bounds.Y, bounds.Width);
        DrawLegacyBuilderPanelBody(new Rectangle(bounds.X, bounds.Y + LegacyBuilderButtonHeight, bounds.Width, bounds.Height - LegacyBuilderButtonHeight));
        DrawGarrisonBuilderText("Resources", bounds.X + 6, bounds.Y + 2, Color.Black, 0.95f);

        var resources = GetGarrisonBuilderResourceRows();
        _builderResourceScrollIndex = Math.Clamp(_builderResourceScrollIndex, 0, Math.Max(0, resources.Count - LegacyBuilderResourceVisibleRows));
        for (var row = 0; row < LegacyBuilderResourceVisibleRows; row += 1)
        {
            var index = _builderResourceScrollIndex + row;
            var rowBounds = new Rectangle(bounds.X + 3, bounds.Y + LegacyBuilderButtonHeight + (row * LegacyBuilderButtonHeight), bounds.Width - 6, LegacyBuilderButtonHeight);
            if (index >= resources.Count)
            {
                _spriteBatch.Draw(_pixel, rowBounds, new Color(178, 178, 178));
                continue;
            }

            var resource = resources[index];
            var selected = resource.Name.Equals(_builderSelectedResourceName, StringComparison.OrdinalIgnoreCase);
            _spriteBatch.Draw(_pixel, rowBounds, selected ? new Color(230, 230, 230) : rowBounds.Contains(mouse.Position) ? new Color(205, 205, 205) : new Color(190, 190, 190));
            var prefix = resource.Kind switch
            {
                CustomMapBuilderResourceKind.ParallaxLayer => "L",
                CustomMapBuilderResourceKind.Foreground => "F",
                CustomMapBuilderResourceKind.EntitySprite => "E",
                _ => "R",
            };
            var label = $"{prefix} {resource.Name}";
            DrawGarrisonBuilderText(label, rowBounds.X + 4, rowBounds.Y + 2, Color.Black, 0.95f);
        }
    }

    private void DrawGarrisonBuilderGameModeMenu(MouseState mouse)
    {
        if (!_builderGameModeMenuOpen)
        {
            return;
        }

        var items = GetGarrisonBuilderModeMenuItems();
        var bounds = GetGarrisonBuilderGameModeMenuBounds();
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, BuilderViewportWidth, BuilderViewportHeight), new Color(0, 0, 0, 145));
        DrawMenuPanelBackdrop(bounds, 1f);
        var uniformScale = GetGarrisonBuilderBitmapFontScale();
        DrawBitmapFontText("Garrison Builder", new Vector2(bounds.X + 18f, bounds.Y + 16f), Color.White, uniformScale);

        var modeItemScale = uniformScale;
        for (var index = 0; index < items.Length; index += 1)
        {
            var rowBounds = GetGarrisonBuilderModeMenuItemBounds(bounds, index);
            var item = items[index];
            var selected = item.Mode is { } mode && mode == _builderSelectedGameMode;
            var hovered = rowBounds.Contains(mouse.Position);
            DrawMenuButtonScaled(rowBounds, item.Label, selected || hovered, modeItemScale);
        }
    }

    private void DrawLegacyGarrisonBuilderPathPrompt()
    {
        if (_builderActivePathField == GarrisonBuilderPathField.None)
        {
            return;
        }

        var width = Math.Min(BuilderUi(620), Math.Max(BuilderUi(320), BuilderViewportWidth - BuilderUi(80)));
        var bounds = new Rectangle((BuilderViewportWidth - width) / 2, BuilderUi(24), width, BuilderUi(66));
        DrawLegacyBuilderPanelBody(bounds);
        DrawGarrisonBuilderText(GetLegacyGarrisonBuilderPathPromptTitle(), bounds.X + 8, bounds.Y + 8, Color.Black, 0.9f);
        var fieldBounds = new Rectangle(bounds.X + 8, bounds.Y + 32, bounds.Width - 16, 22);
        _spriteBatch.Draw(_pixel, fieldBounds, new Color(240, 240, 240));
        _spriteBatch.Draw(_pixel, new Rectangle(fieldBounds.X, fieldBounds.Y, fieldBounds.Width, 1), Color.Black);
        _spriteBatch.Draw(_pixel, new Rectangle(fieldBounds.X, fieldBounds.Bottom - 1, fieldBounds.Width, 1), Color.Black);
        DrawGarrisonBuilderText(GetTextWithCursor(GetGarrisonBuilderPathFieldBuffer(_builderActivePathField), _builderPathCursorIndex), fieldBounds.Location.ToVector2() + new Vector2(4f, 3f), Color.Black, 0.95f);
    }

    private void DrawGarrisonBuilderPropertyEditor(MouseState mouse)
    {
        if (_builderPropertyTarget == GarrisonBuilderPropertyTarget.None)
        {
            return;
        }

        if ((_builderObjectiveMapPickActive || _builderLogicMapPickActive || _builderEntityMapPickActive) && _builderUseModernUi)
        {
            return;
        }

        var bounds = GetGarrisonBuilderPropertyEditorBounds();
        var popupScale = GetGarrisonBuilderBitmapFontScale();
        if (_builderUseModernUi)
        {
            DrawGarrisonBuilderBrownPanel(bounds);
            DrawBitmapFontText(_builderPropertyEditorTitle, new Vector2(bounds.X + 12f, bounds.Y + 8f), Color.White, popupScale);
        }
        else
        {
            DrawLegacyBuilderPanelBody(bounds);
            DrawGarrisonBuilderText(_builderPropertyEditorTitle, bounds.X + 8, bounds.Y + 8, Color.Black, 0.92f);
        }

        if (_builderPropertyEditMode == GarrisonBuilderPropertyEditMode.NewKey)
        {
            DrawGarrisonBuilderPropertyEditorTextMode(
                bounds,
                mouse,
                "New property",
                _builderPropertyEditBuffer,
                _builderUseModernUi ? "Enter: value  Esc: cancel" : "Enter: value  Esc: cancel");
            return;
        }

        if (_builderPropertyEditMode == GarrisonBuilderPropertyEditMode.EditValue)
        {
            DrawGarrisonBuilderPropertyEditorTextMode(
                bounds,
                mouse,
                _builderPropertyEditKey,
                _builderPropertyEditBuffer,
                _builderUseModernUi ? "Enter: save  Empty: delete  Esc: cancel" : "Enter: save  Empty: delete  Esc: cancel");
            return;
        }

        var rows = GetGarrisonBuilderPropertyRows();
        var visibleRows = GetGarrisonBuilderPropertyVisibleRows(bounds);
        _builderPropertyScrollIndex = Math.Clamp(_builderPropertyScrollIndex, 0, Math.Max(0, rows.Count - visibleRows));
        var rowHeight = GetGarrisonBuilderPropertyRowHeight();
        var y = GetGarrisonBuilderPropertyListTop(bounds);
        for (var visibleIndex = 0; visibleIndex < visibleRows; visibleIndex += 1)
        {
            var index = _builderPropertyScrollIndex + visibleIndex;
            var rowBounds = GetGarrisonBuilderPropertyRowBounds(bounds, y);
            if (index >= rows.Count)
            {
                if (!_builderUseModernUi)
                {
                    _spriteBatch.Draw(_pixel, rowBounds, new Color(178, 178, 178));
                }

                y += rowHeight;
                continue;
            }

            var key = rows[index];
            if (!_builderPropertyEditorValues.TryGetValue(key, out var value))
            {
                value = string.Empty;
            }

            DrawGarrisonBuilderPropertyRow(rowBounds, key, value, mouse, popupScale);
            y += rowHeight;
        }

        if (rows.Count > visibleRows)
        {
            var scrollLabel = $"{_builderPropertyScrollIndex + 1}-{Math.Min(rows.Count, _builderPropertyScrollIndex + visibleRows)} / {rows.Count}";
            if (_builderUseModernUi)
            {
                DrawBitmapFontText(scrollLabel, new Vector2(bounds.Right - BuilderUi(92), bounds.Y + BuilderUi(8)), new Color(200, 190, 168), popupScale);
            }
            else
            {
                DrawGarrisonBuilderText(scrollLabel, bounds.Right - 84, bounds.Y + 8, Color.Black, 0.66f);
            }
        }

        var addBounds = GetGarrisonBuilderPropertyAddBounds(bounds);
        if (_builderUseModernUi)
        {
            DrawBuilderMenuButton(addBounds, "Add new property", addBounds.Contains(mouse.Position));
            DrawBitmapFontText(
                GetGarrisonBuilderPropertyEditorFooterHint(),
                new Vector2(bounds.X + BuilderUi(12), bounds.Bottom - BuilderUi(20)),
                new Color(200, 190, 168),
                popupScale);
        }
        else
        {
            _spriteBatch.Draw(_pixel, addBounds, addBounds.Contains(mouse.Position) ? new Color(220, 220, 220) : new Color(190, 190, 190));
            DrawGarrisonBuilderText("Add new property", addBounds.Location.ToVector2() + new Vector2(4f, 2f), Color.Black, 0.95f);
            DrawGarrisonBuilderText("Click bool to toggle, other values to edit. Esc closes.", bounds.X + 8, bounds.Bottom - 22, Color.Black, 0.66f);
        }
    }

    private void DrawGarrisonBuilderPropertyEditorTextMode(
        Rectangle bounds,
        MouseState mouse,
        string fieldLabel,
        string buffer,
        string footerHint)
    {
        if (_builderUseModernUi)
        {
            var scale = GetGarrisonBuilderBitmapFontScale();
            DrawBitmapFontText(fieldLabel, new Vector2(bounds.X + 12f, bounds.Y + 34f), Color.White, scale);
            var fieldHeight = GetGarrisonBuilderMenuRowHeight();
            var fieldBounds = new Rectangle(bounds.X + 8, bounds.Y + 56, bounds.Width - 16, fieldHeight);
            DrawBuilderMenuButton(fieldBounds, GetTextWithCursor(buffer, _builderPropertyCursorIndex), fieldBounds.Contains(mouse.Position));
            DrawBitmapFontText(footerHint, new Vector2(bounds.X + 12f, bounds.Bottom - 20f), new Color(200, 190, 168), scale);
            return;
        }

        DrawGarrisonBuilderText(fieldLabel, bounds.X + 8, bounds.Y + 34, Color.Black, 0.82f);
        DrawGarrisonBuilderPropertyInputBox(new Rectangle(bounds.X + 8, bounds.Y + 56, bounds.Width - 16, 24), buffer);
        DrawGarrisonBuilderText(footerHint, bounds.X + 8, bounds.Bottom - 22, Color.Black, 0.72f);
    }

    private void DrawGarrisonBuilderPropertyInputBox(Rectangle bounds, string text)
    {
        _spriteBatch.Draw(_pixel, bounds, new Color(240, 240, 240));
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), Color.Black);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), Color.Black);
        DrawGarrisonBuilderText(GetTextWithCursor(text, _builderPropertyCursorIndex), bounds.Location.ToVector2() + new Vector2(4f, 3f), Color.Black, 0.95f);
    }

    private void DrawLegacyGarrisonBuilderStatus()
    {
        var validation = CustomMapBuilderValidator.Validate(_builderDocument with { Entities = _builderEntities.ToArray() }, _builderSelectedGameMode);
        var text = $"{GetGarrisonBuilderModeLabel(validation.Mode)} | {(_builderDirty ? "dirty" : "saved")} | {_builderStatus}";
        var width = Math.Min(BuilderViewportWidth - BuilderUi(8), (int)MathF.Ceiling(MeasureGarrisonBuilderText(text, 1f).X) + BuilderUi(12));
        var bounds = new Rectangle(4, 4, width, 18);
        _spriteBatch.Draw(_pixel, bounds, new Color(159, 159, 159));
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), new Color(63, 63, 63));
        DrawGarrisonBuilderText(text, bounds.Location.ToVector2() + new Vector2(4f, 2f), Color.Black, 0.95f);
    }

    private void DrawLegacyGarrisonBuilderTooltip(Point mousePosition, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var width = (int)MathF.Ceiling(MeasureGarrisonBuilderText(text, 1f).X) + 8;
        var x = mousePosition.X + width + BuilderUi(14) > BuilderViewportWidth ? mousePosition.X - width - BuilderUi(12) : mousePosition.X + BuilderUi(12);
        var y = mousePosition.Y - 8;
        var bounds = new Rectangle(x - 2, y - 2, width, 18);
        _spriteBatch.Draw(_pixel, bounds, new Color(159, 159, 159));
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), new Color(63, 63, 63));
        DrawGarrisonBuilderText(text, new Vector2(x, y), Color.White, 0.95f);
    }

    private void DrawLegacyBuilderPanelHeader(int x, int y, int width)
    {
        if (_builderMenuLayoutSprite is not null && _builderMenuLayoutSprite.Frames.Count >= 3)
        {
            DrawLoadedSpriteFrame(_builderMenuLayoutSprite.Frames[0], new Vector2(x, y), null, Color.White, 0f, Vector2.Zero, Vector2.One, SpriteEffects.None, 0f);
            DrawLoadedSpriteFrame(_builderMenuLayoutSprite.Frames[1], new Vector2(x + LegacyBuilderButtonSpriteWidth, y), null, Color.White, 0f, Vector2.Zero, new Vector2(width / (float)(LegacyBuilderButtonSpriteWidth - 1) - 2f, 1f), SpriteEffects.None, 0f);
            DrawLoadedSpriteFrame(_builderMenuLayoutSprite.Frames[2], new Vector2(x - LegacyBuilderButtonSpriteWidth + width + 1, y), null, Color.White, 0f, Vector2.Zero, Vector2.One, SpriteEffects.None, 0f);
            return;
        }

        _spriteBatch.Draw(_pixel, new Rectangle(x, y, width, LegacyBuilderButtonHeight), new Color(63, 63, 63));
    }

    private void DrawLegacyBuilderPanelBody(Rectangle bounds)
    {
        _spriteBatch.Draw(_pixel, bounds, new Color(159, 159, 159));
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), new Color(63, 63, 63));
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), new Color(63, 63, 63));
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), new Color(63, 63, 63));
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), new Color(63, 63, 63));
    }

    private void DrawLegacyBuilderButton(Rectangle bounds, string label, bool toggled, MouseState mouse)
    {
        if (_builderButtonSprite is not null && _builderButtonSprite.Frames.Count >= 6)
        {
            var offset = toggled ? 3 : 0;
            DrawLoadedSpriteFrame(_builderButtonSprite.Frames[offset], new Vector2(bounds.X, bounds.Y), null, Color.White, 0f, Vector2.Zero, Vector2.One, SpriteEffects.None, 0f);
            DrawLoadedSpriteFrame(_builderButtonSprite.Frames[offset + 1], new Vector2(bounds.X + LegacyBuilderButtonSpriteWidth, bounds.Y), null, Color.White, 0f, Vector2.Zero, new Vector2(bounds.Width / (float)LegacyBuilderButtonSpriteWidth - 2f, 1f), SpriteEffects.None, 0f);
            DrawLoadedSpriteFrame(_builderButtonSprite.Frames[offset + 2], new Vector2(bounds.X - LegacyBuilderButtonSpriteWidth + bounds.Width, bounds.Y), null, Color.White, 0f, Vector2.Zero, Vector2.One, SpriteEffects.None, 0f);
        }
        else
        {
            _spriteBatch.Draw(_pixel, bounds, toggled ? new Color(110, 110, 110) : new Color(210, 210, 210));
        }

        var fittedLabel = TrimGarrisonBuilderTextToWidth(label, bounds.Width - 4, 1f);
        DrawGarrisonBuilderText(fittedLabel, bounds.Location.ToVector2() + new Vector2(2f, 2f), Color.Black, 1f);
    }

    private void DrawLegacyBuilderScrollbar(Rectangle actionBounds, int visibleRows)
    {
        if (_builderActionDefinitions.Length <= visibleRows)
        {
            return;
        }

        var height = visibleRows * LegacyBuilderButtonHeight;
        var sectionHeight = Math.Max(2, (int)MathF.Round(visibleRows / (float)_builderActionDefinitions.Length * (height - 5)));
        var scrollHeight = ((height - 5f) - sectionHeight) / (_builderActionDefinitions.Length - visibleRows);
        var y = actionBounds.Y + 2 + (int)MathF.Round(scrollHeight * _builderActionScrollIndex);
        _spriteBatch.Draw(_pixel, new Rectangle(actionBounds.X + LegacyBuilderButtonWidth + 1, y, LegacyBuilderHeaderWidth - LegacyBuilderButtonWidth - 3, sectionHeight), new Color(63, 63, 63));
    }

    private void DrawLegacyBuilderSquareButton(Rectangle bounds, bool enabled, int scrollFrame)
    {
        _spriteBatch.Draw(_pixel, bounds, enabled ? new Color(190, 190, 190) : new Color(120, 120, 120));
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), Color.Black);
        if (enabled && _builderScrollButtonSprite is not null && scrollFrame < _builderScrollButtonSprite.Frames.Count)
        {
            DrawLoadedSpriteFrame(_builderScrollButtonSprite.Frames[scrollFrame], bounds, Color.White);
        }
    }

    private void DrawLegacyBuilderRectButton(Rectangle bounds, string label, MouseState mouse)
    {
        _spriteBatch.Draw(_pixel, bounds, bounds.Contains(mouse.Position) ? new Color(220, 220, 220) : new Color(190, 190, 190));
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), Color.Black);
        var textScale = MeasureGarrisonBuilderText(label, 1f).X <= bounds.Width - 6 ? 1f : 0.86f;
        DrawGarrisonBuilderText(label, bounds.Location.ToVector2() + new Vector2(4f, 2f), Color.Black, textScale);
    }

    private void DrawGarrisonBuilderPanel(MouseState mouse)
    {
        var panel = GetGarrisonBuilderPanelBounds();
        _spriteBatch.Draw(_pixel, panel, new Color(18, 20, 24, 242));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, 3, panel.Height), new Color(80, 180, 210));
        var x = panel.X + BuilderPanelPadding;
        var y = panel.Y + BuilderPanelPadding;
        DrawGarrisonBuilderText("Garrison Builder", x, y, Color.White, 1f);
        y += 28;
        DrawGarrisonBuilderText(_builderDirty ? "dirty" : "saved", x, y, _builderDirty ? new Color(255, 214, 118) : new Color(150, 224, 160), 0.86f);
        y += 24;

        var buttons = CreateGarrisonBuilderButtonRow(x, y, panel.Width - (BuilderPanelPadding * 2), 4);
        DrawGarrisonBuilderButton(buttons[0], _builderShowBackground ? "BG On" : "BG Off", _builderShowBackground, true, mouse);
        DrawGarrisonBuilderButton(buttons[1], _builderShowWalkmask ? "WM On" : "WM Off", _builderShowWalkmask, true, mouse);
        DrawGarrisonBuilderButton(buttons[2], _builderShowGrid ? "Grid On" : "Grid Off", _builderShowGrid, true, mouse);
        DrawGarrisonBuilderButton(buttons[3], "Save Ctrl+S", false, CanSaveGarrisonBuilderDocument(), mouse);
        y += buttons[0].Height + 14;

        DrawGarrisonBuilderText("File", x, y, new Color(180, 214, 230), 0.9f);
        y += 22;
        DrawGarrisonBuilderPathRow(GarrisonBuilderPathField.OpenMap, "Open", x, y, panel.Width - (BuilderPanelPadding * 2), mouse);
        y += 32;
        DrawGarrisonBuilderPathRow(GarrisonBuilderPathField.Background, "BG", x, y, panel.Width - (BuilderPanelPadding * 2), mouse);
        y += 32;
        DrawGarrisonBuilderPathRow(GarrisonBuilderPathField.Walkmask, "WM", x, y, panel.Width - (BuilderPanelPadding * 2), mouse);
        y += 32;
        DrawGarrisonBuilderPathRow(GarrisonBuilderPathField.Save, "Save", x, y, panel.Width - (BuilderPanelPadding * 2), mouse);
        y += 42;

        DrawGarrisonBuilderText("Entities", x, y, new Color(180, 214, 230), 0.9f);
        y += 22;
        var columns = Math.Max(1, (panel.Width - (BuilderPanelPadding * 2)) / BuilderEntityButtonSize);
        for (var index = 0; index < _builderEntityDefinitions.Count; index += 1)
        {
            var column = index % columns;
            var row = index / columns;
            var bounds = new Rectangle(x + (column * BuilderEntityButtonSize), y + (row * BuilderEntityButtonSize), BuilderEntityButtonSize - 2, BuilderEntityButtonSize - 2);
            var definition = _builderEntityDefinitions[index];
            var selected = string.Equals(_builderSelectedEntityType, definition.Type, StringComparison.OrdinalIgnoreCase);
            _spriteBatch.Draw(_pixel, bounds, selected ? new Color(84, 176, 216, 220) : new Color(42, 46, 52, 220));
            DrawGarrisonBuilderEntityIcon(definition, bounds);
        }

        y += ((int)MathF.Ceiling(_builderEntityDefinitions.Count / (float)columns) * BuilderEntityButtonSize) + 14;
        DrawGarrisonBuilderText("Properties", x, y, new Color(180, 214, 230), 0.9f);
        y += 22;
        DrawGarrisonBuilderText($"Selected: {_builderSelectedEntityType}", x, y, Color.White, 0.8f);
        y += 18;
        DrawGarrisonBuilderText($"Placed: {_builderEntities.Count}", x, y, Color.White, 0.8f);
        y += 18;
        var validation = CustomMapBuilderValidator.Validate(_builderDocument with { Entities = _builderEntities.ToArray() }, _builderSelectedGameMode);
        DrawGarrisonBuilderText($"Mode: {GetGarrisonBuilderModeLabel(validation.Mode)}", x, y, validation.IsValid ? new Color(150, 224, 160) : new Color(255, 214, 118), 0.8f);
        y += 18;
        DrawGarrisonBuilderText(validation.IsValid ? "Validation: OK" : $"Validation: {validation.Issues.Count} issue(s)", x, y, validation.IsValid ? new Color(150, 224, 160) : new Color(255, 214, 118), 0.8f);
        foreach (var issue in validation.Issues.Take(2))
        {
            y += 16;
            DrawGarrisonBuilderWrapped(issue.Message, x, y, panel.Width - 22, new Color(255, 190, 150));
        }

        y += 28;
        DrawGarrisonBuilderText("Layers", x, y, new Color(180, 214, 230), 0.9f);
        y += 22;
        DrawGarrisonBuilderText($"Visual scale: {_builderDocument.VisualScale:0.##}", x, y, Color.White, 0.8f);
        y += 18;
        DrawGarrisonBuilderText($"Walkmask scale: {_builderDocument.Scale:0.##}", x, y, Color.White, 0.8f);
        y += 18;
        DrawGarrisonBuilderText($"Parallax layers: {_builderDocument.ParallaxLayers.Count}", x, y, Color.White, 0.8f);
        y += 28;
        DrawGarrisonBuilderText("Metadata", x, y, new Color(180, 214, 230), 0.9f);
        y += 22;
        foreach (var pair in _builderDocument.BuildExportMetadata())
        {
            if (y > panel.Bottom - 72)
            {
                break;
            }

            DrawGarrisonBuilderText($"{pair.Key}: {pair.Value}", x, y, new Color(216, 216, 216), 0.74f);
            y += 16;
        }

        DrawGarrisonBuilderWrapped(_builderStatus, x, panel.Bottom - 48, panel.Width - 22, new Color(255, 226, 140));
    }

    private int GetGarrisonBuilderEntityIconFrameIndex(CustomMapBuilderEntityDefinition definition, bool selected)
    {
        if (definition.IconFrame < 0)
        {
            return -1;
        }

        if (!selected)
        {
            return definition.IconFrame;
        }

        var selectedFrame = definition.IconFrame + 1;
        if (_builderEntityButtonSprite is not null
            && selectedFrame >= 0
            && selectedFrame < _builderEntityButtonSprite.Frames.Count)
        {
            return selectedFrame;
        }

        return definition.IconFrame;
    }

    private void DrawGarrisonBuilderEntityIcon(CustomMapBuilderEntityDefinition definition, Rectangle bounds, bool selected = false)
    {
        var frameIndex = GetGarrisonBuilderEntityIconFrameIndex(definition, selected);
        if (_builderEntityButtonSprite is not null
            && frameIndex >= 0
            && frameIndex < _builderEntityButtonSprite.Frames.Count)
        {
            var frame = _builderEntityButtonSprite.Frames[frameIndex];
            var source = frame.SourceRectangle ?? new Rectangle(0, 0, frame.Texture.Width, frame.Texture.Height);
            var scale = MathF.Min(bounds.Width / (float)source.Width, bounds.Height / (float)source.Height);
            var drawWidth = MathF.Max(1f, source.Width * scale);
            var drawHeight = MathF.Max(1f, source.Height * scale);
            var drawBounds = new Rectangle(
                bounds.X + (int)MathF.Round((bounds.Width - drawWidth) * 0.5f),
                bounds.Y + (int)MathF.Round((bounds.Height - drawHeight) * 0.5f),
                (int)MathF.Round(drawWidth),
                (int)MathF.Round(drawHeight));
            DrawLoadedSpriteFrame(frame, drawBounds, Color.White);
            return;
        }

        DrawGarrisonBuilderText(
            definition.Type[..Math.Min(2, definition.Type.Length)].ToUpperInvariant(),
            bounds.X + BuilderUi(4),
            bounds.Y + BuilderUi(4),
            Color.White,
            0.65f);
    }

    private bool TryGetGarrisonBuilderEntityWorldBounds(
        CustomMapBuilderEntity entity,
        out float left,
        out float top,
        out float width,
        out float height)
    {
        left = top = width = height = 0f;
        if (TryGetGarrisonBuilderLogicNodeWorldBounds(entity, out left, out top, out width, out height))
        {
            return true;
        }

        if (TryGetGarrisonBuilderCustomSpriteWorldBounds(entity, out left, out top, out width, out height))
        {
            return true;
        }

        if (!CustomMapBuilderEntityCatalog.TryGetDefinition(entity.Type, out var definition))
        {
            const float fallbackSize = 32f;
            var half = fallbackSize * 0.5f;
            left = entity.X - half;
            top = entity.Y - half;
            width = height = fallbackSize;
            return true;
        }

        if (TryGetGarrisonBuilderAnchorSizedEntityMetrics(
                entity.Type,
                entity.Properties,
                entity.XScale,
                entity.YScale,
                out var sizedMetrics))
        {
            left = entity.X;
            top = entity.Y;
            width = sizedMetrics.Width;
            height = sizedMetrics.Height;
            return true;
        }

        var metrics = GetGarrisonBuilderEntityMetrics(definition, entity.Properties, 1f, 1f);
        GetGarrisonBuilderEntityMinimumWorldSize(entity.Type, metrics.Width, metrics.Height, out var minWidth, out var minHeight);
        width = MathF.Max(minWidth, metrics.Width * entity.XScale);
        height = MathF.Max(minHeight, metrics.Height * entity.YScale);
        if (TryGetGarrisonBuilderEntityFrame(definition, entity, out _, out var origin))
        {
            left = entity.X - (origin.X * entity.XScale);
            top = entity.Y - (origin.Y * entity.YScale);
        }
        else
        {
            left = entity.X - (metrics.CenterX * entity.XScale);
            top = entity.Y - (metrics.CenterY * entity.YScale);
        }

        return true;
    }

    private bool TryGetGarrisonBuilderEntityPickBounds(CustomMapBuilderEntity entity, out RectangleF bounds)
    {
        if (!TryGetGarrisonBuilderEntityWorldBounds(entity, out var left, out var top, out var width, out var height))
        {
            bounds = default;
            return false;
        }

        bounds = new RectangleF(left, top, width, height);
        return true;
    }

    private bool TryGetGarrisonBuilderEntitySelectionMarkerScreen(CustomMapBuilderEntity entity, out Vector2 screen)
    {
        if (CustomMapCustomSpriteMetadata.IsCustomSpriteEntityType(entity.Type)
            && TryGetGarrisonBuilderCustomSpriteDrawBounds(entity, out var centerX, out var centerY, out _, out _))
        {
            screen = BuilderWorldToScreen(new Vector2(centerX, centerY));
            return true;
        }

        if (MapLogicMetadata.IsLogicEntityType(entity.Type))
        {
            screen = BuilderWorldToScreen(new Vector2(entity.X, entity.Y));
            return true;
        }

        if (TryGetGarrisonBuilderEntityWorldBounds(entity, out var left, out var top, out var width, out var height))
        {
            screen = BuilderWorldToScreen(new Vector2(left + (width * 0.5f), top + (height * 0.5f)));
            return true;
        }

        screen = BuilderWorldToScreen(new Vector2(entity.X, entity.Y));
        return true;
    }

    private void UpdateGarrisonBuilderMapEntityHover(MouseState mouse)
    {
        _builderMapHoverEntityIndex = -1;
        Vector2 world;
        if (_builderUseModernUi)
        {
            if (!GetModernGarrisonBuilderMapViewport().Contains(mouse.Position))
            {
                return;
            }

            world = BuilderScreenToWorld(mouse.Position);
        }
        else
        {
            world = mouse.Position.ToVector2() + _builderCamera;
        }

        CollectGarrisonBuilderEntitiesAtWorld(world, _builderEntityOverlapPickScratch);
        if (_builderEntityOverlapPickScratch.Count > 0)
        {
            _builderMapHoverEntityIndex = _builderEntityOverlapPickScratch[0];
        }
    }

    private void CollectGarrisonBuilderEntitiesAtWorld(Vector2 world, List<int> results)
    {
        results.Clear();
        for (var index = 0; index < _builderEntities.Count; index += 1)
        {
            var entity = _builderEntities[index];
            if (IsGarrisonBuilderEntityHidden(index))
            {
                continue;
            }

            if (TryGetGarrisonBuilderEntityPickBounds(entity, out var bounds)
                && bounds.Contains(world.X, world.Y))
            {
                results.Add(index);
            }
        }

        if (results.Count > 0)
        {
            results.Sort((leftIndex, rightIndex) =>
            {
                var leftArea = TryGetGarrisonBuilderEntityPickBounds(_builderEntities[leftIndex], out var leftBounds)
                    ? leftBounds.Width * leftBounds.Height
                    : float.MaxValue;
                var rightArea = TryGetGarrisonBuilderEntityPickBounds(_builderEntities[rightIndex], out var rightBounds)
                    ? rightBounds.Width * rightBounds.Height
                    : float.MaxValue;
                return leftArea.CompareTo(rightArea);
            });
            return;
        }

        var pickRadius = MathF.Max(12f, 24f / (_builderUseModernUi ? _builderZoom : 1f));
        var pickRadiusSquared = pickRadius * pickRadius;
        var bestDistance = pickRadiusSquared;
        var nearestIndex = -1;
        for (var index = 0; index < _builderEntities.Count; index += 1)
        {
            var entity = _builderEntities[index];
            if (IsGarrisonBuilderEntityHidden(index))
            {
                continue;
            }

            var distance = Vector2.DistanceSquared(new Vector2(entity.X, entity.Y), world);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                nearestIndex = index;
            }
        }

        if (nearestIndex >= 0)
        {
            results.Add(nearestIndex);
        }
    }

    private bool TryPickGarrisonBuilderEntityAtWorld(Vector2 world, out int entityIndex)
    {
        entityIndex = -1;
        if (_builderSelectedEntityIndices.Count > 0)
        {
            foreach (var selectedIndex in _builderSelectedEntityIndices.OrderByDescending(index => index))
            {
                if (!IsValidGarrisonBuilderMapEntityIndex(selectedIndex))
                {
                    continue;
                }

                if (TryGetGarrisonBuilderEntityPickBounds(_builderEntities[selectedIndex], out var selectedBounds)
                    && selectedBounds.Contains(world.X, world.Y))
                {
                    entityIndex = selectedIndex;
                    return true;
                }
            }
        }
        else if (_builderSelectedEntityIndex >= 0
            && _builderSelectedEntityIndex < _builderEntities.Count
            && !IsGarrisonBuilderEntityHidden(_builderSelectedEntityIndex)
            && TryGetGarrisonBuilderEntityPickBounds(_builderEntities[_builderSelectedEntityIndex], out var focusedBounds)
            && focusedBounds.Contains(world.X, world.Y))
        {
            entityIndex = _builderSelectedEntityIndex;
            return true;
        }

        CollectGarrisonBuilderEntitiesAtWorld(world, _builderEntityOverlapPickScratch);
        if (_builderEntityOverlapPickScratch.Count == 0)
        {
            return false;
        }

        entityIndex = _builderEntityOverlapPickScratch[0];
        return true;
    }

    private enum GarrisonBuilderEntityPickResult
    {
        None,
        Picked,
        OverlapPickerOpened,
    }

    private GarrisonBuilderEntityPickResult TryBeginGarrisonBuilderEntityPick(
        Vector2 world,
        Point screenPosition,
        out int entityIndex)
    {
        entityIndex = -1;
        CollectGarrisonBuilderEntitiesAtWorld(world, _builderEntityOverlapPickScratch);
        if (_builderEntityOverlapPickScratch.Count == 0)
        {
            return GarrisonBuilderEntityPickResult.None;
        }

        if (_builderEntityOverlapPickScratch.Count > 1
            && (_builderSelectedEntityIndices.Count == 0
                || !_builderEntityOverlapPickScratch.Any(index => IsGarrisonBuilderMapEntitySelected(index))))
        {
            OpenGarrisonBuilderEntityOverlapPicker(screenPosition, _builderEntityOverlapPickScratch);
            return GarrisonBuilderEntityPickResult.OverlapPickerOpened;
        }

        entityIndex = _builderEntityOverlapPickScratch[0];
        return GarrisonBuilderEntityPickResult.Picked;
    }

    private void DrawGarrisonBuilderPathRow(GarrisonBuilderPathField field, string label, int x, int y, int width, MouseState mouse)
    {
        const int labelWidth = 44;
        const int actionWidth = 58;
        var rowHeight = GetGarrisonBuilderMenuRowHeight();
        var fieldBounds = new Rectangle(x + labelWidth, y, Math.Max(1, width - labelWidth - actionWidth - BuilderButtonGap), rowHeight);
        var actionBounds = new Rectangle(fieldBounds.Right + BuilderButtonGap, y, actionWidth, rowHeight);
        DrawGarrisonBuilderText(label, x, y + 5, new Color(216, 216, 216), 0.72f);
        var active = _builderActivePathField == field;
        _spriteBatch.Draw(_pixel, fieldBounds, active ? new Color(54, 68, 78, 235) : new Color(28, 31, 36, 230));
        _spriteBatch.Draw(_pixel, new Rectangle(fieldBounds.X, fieldBounds.Bottom - 1, fieldBounds.Width, 1), active ? new Color(116, 210, 230) : new Color(86, 92, 98));
        var text = GetGarrisonBuilderPathFieldBuffer(field);
        var displayText = active ? GetTextWithCursor(text, _builderPathCursorIndex) : ShortenBuilderPath(text);
        DrawGarrisonBuilderText(displayText, fieldBounds.Location.ToVector2() + new Vector2(6f, 4f), Color.White, 0.95f);
        DrawGarrisonBuilderButton(actionBounds, field == GarrisonBuilderPathField.Save ? "Write" : "Apply", false, true, mouse);
    }

    private void HandleGarrisonBuilderPanelClick(Point position)
    {
        var panel = GetGarrisonBuilderPanelBounds();
        var x = panel.X + BuilderPanelPadding;
        var y = panel.Y + BuilderPanelPadding + 52;
        var buttons = CreateGarrisonBuilderButtonRow(x, y, panel.Width - (BuilderPanelPadding * 2), 4);
        if (buttons[0].Contains(position))
        {
            _builderShowBackground = !_builderShowBackground;
            return;
        }

        if (buttons[1].Contains(position))
        {
            _builderShowWalkmask = !_builderShowWalkmask;
            return;
        }

        if (buttons[2].Contains(position))
        {
            _builderShowGrid = !_builderShowGrid;
            return;
        }

        if (buttons[3].Contains(position))
        {
            SaveGarrisonBuilderDocument();
            return;
        }

        if (TryHandleGarrisonBuilderPathClick(position, panel))
        {
            return;
        }

        var paletteY = panel.Y + BuilderPanelPadding + 52 + BuilderButtonHeight + 14 + 22 + (32 * 4) + 42 + 22;
        var columns = Math.Max(1, (panel.Width - (BuilderPanelPadding * 2)) / BuilderEntityButtonSize);
        for (var index = 0; index < _builderEntityDefinitions.Count; index += 1)
        {
            var column = index % columns;
            var row = index / columns;
            var bounds = new Rectangle(x + (column * BuilderEntityButtonSize), paletteY + (row * BuilderEntityButtonSize), BuilderEntityButtonSize - 2, BuilderEntityButtonSize - 2);
            if (bounds.Contains(position))
            {
                SelectGarrisonBuilderEntity(_builderEntityDefinitions[index], updateStatus: true);
                return;
            }
        }
    }

    private void UpdateLegacyGarrisonBuilderScroll(MouseState mouse)
    {
        var actionBounds = GetLegacyGarrisonBuilderActionBounds();
        var visibleRows = _builderActionVisibleRows;
        if (_builderActionDefinitions.Length > visibleRows)
        {
            var height = visibleRows * LegacyBuilderButtonHeight;
            var trackBounds = new Rectangle(
                actionBounds.X + LegacyBuilderButtonWidth + 1,
                actionBounds.Y + 2,
                LegacyBuilderHeaderWidth - LegacyBuilderButtonWidth - 3,
                Math.Max(4, height - 4));
            if (TryHandleScrollbarDrag(
                    mouse,
                    _previousMouse,
                    ScrollbarOwners.GarrisonBuilderActions,
                    trackBounds,
                    ref _builderActionScrollIndex,
                    _builderActionDefinitions.Length,
                    visibleRows,
                    minThumbHeight: 8))
            {
                return;
            }
        }

        var wheelDelta = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        if (wheelDelta == 0)
        {
            return;
        }

        if (GetLegacyGarrisonBuilderActionBounds().Contains(mouse.Position))
        {
            _builderActionScrollIndex = Math.Clamp(
                _builderActionScrollIndex + (wheelDelta > 0 ? -1 : 1),
                0,
                Math.Max(0, _builderActionDefinitions.Length - _builderActionVisibleRows));
            return;
        }

        if (GetLegacyGarrisonBuilderLayerBounds().Contains(mouse.Position))
        {
            _builderLayerIndex = Math.Clamp(_builderLayerIndex + (wheelDelta > 0 ? -1 : 1), 0, 8);
            return;
        }

        if (_builderDocument.Resources.Count > 0 && GetLegacyGarrisonBuilderResourceBounds().Contains(mouse.Position))
        {
            _builderResourceScrollIndex = Math.Clamp(
                _builderResourceScrollIndex + (wheelDelta > 0 ? -1 : 1),
                0,
                Math.Max(0, _builderDocument.Resources.Count - LegacyBuilderResourceVisibleRows));
        }
    }

    private void UpdateLegacyGarrisonBuilderAnimation(float deltaSeconds)
    {
        var target = _builderActionExpanded ? 1f : 0f;
        var step = MathF.Max(0.05f, deltaSeconds * 9f);
        if (_builderActionExpandProgress < target)
        {
            _builderActionExpandProgress = MathF.Min(target, _builderActionExpandProgress + step);
        }
        else if (_builderActionExpandProgress > target)
        {
            _builderActionExpandProgress = MathF.Max(target, _builderActionExpandProgress - step);
        }
    }

    private bool UpdateLegacyGarrisonBuilderPanelDrag(MouseState mouse)
    {
        if (mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released)
        {
            if (TryBeginLegacyGarrisonBuilderPanelDrag(mouse.Position))
            {
                return true;
            }
        }

        if (_builderPanelDragTarget == LegacyBuilderPanelDragTarget.None)
        {
            return false;
        }

        if (mouse.LeftButton == ButtonState.Released)
        {
            if (_builderPanelDragTarget == LegacyBuilderPanelDragTarget.Action && _builderPanelDragHeaderToggleCandidate)
            {
                _builderActionExpanded = !_builderActionExpanded;
            }

            _builderPanelDragTarget = LegacyBuilderPanelDragTarget.None;
            _builderPanelDragHeaderToggleCandidate = false;
            return true;
        }

        var delta = mouse.Position - _builderPanelDragLastMouse;
        if (Math.Abs(delta.X) > 3 || Math.Abs(delta.Y) > 3)
        {
            _builderPanelDragHeaderToggleCandidate = false;
        }

        switch (_builderPanelDragTarget)
        {
            case LegacyBuilderPanelDragTarget.Action:
                _builderActionPanelOffset.X = Math.Clamp(_builderActionPanelOffset.X + delta.X, 0, Math.Max(0, BuilderViewportWidth - LegacyBuilderHeaderWidth));
                if (!_builderPanelDragHeaderToggleCandidate)
                {
                    _builderActionVisibleRows = Math.Clamp(_builderActionVisibleRows - (int)MathF.Round(delta.Y / (float)LegacyBuilderButtonHeight), 1, _builderActionDefinitions.Length);
                }
                break;
            case LegacyBuilderPanelDragTarget.Entity:
                _builderEntityPanelOffset.X += delta.X;
                _builderEntityPanelOffset.Y += delta.Y;
                break;
            case LegacyBuilderPanelDragTarget.Layer:
                _builderLayerPanelOffset.X += delta.X;
                _builderLayerPanelOffset.Y += delta.Y;
                break;
        }

        _builderPanelDragLastMouse = mouse.Position;
        return true;
    }

    private bool TryBeginLegacyGarrisonBuilderPanelDrag(Point position)
    {
        var actionBounds = GetLegacyGarrisonBuilderActionBounds();
        var actionHeader = new Rectangle(actionBounds.X, actionBounds.Y - LegacyBuilderButtonHeight, LegacyBuilderHeaderWidth, LegacyBuilderButtonHeight);
        if (actionHeader.Contains(position))
        {
            _builderPanelDragTarget = LegacyBuilderPanelDragTarget.Action;
            _builderPanelDragLastMouse = position;
            _builderPanelDragHeaderToggleCandidate = true;
            return true;
        }

        var entityBounds = GetLegacyGarrisonBuilderEntityBounds();
        var entityHeader = new Rectangle(entityBounds.X, entityBounds.Y, entityBounds.Width, LegacyBuilderButtonHeight);
        if (entityHeader.Contains(position))
        {
            _builderPanelDragTarget = LegacyBuilderPanelDragTarget.Entity;
            _builderPanelDragLastMouse = position;
            _builderPanelDragHeaderToggleCandidate = false;
            return true;
        }

        var layerBounds = GetLegacyGarrisonBuilderLayerBounds();
        var layerHeader = new Rectangle(layerBounds.X, layerBounds.Y, LegacyBuilderLayerWidth, LegacyBuilderButtonHeight);
        if (layerHeader.Contains(position))
        {
            _builderPanelDragTarget = LegacyBuilderPanelDragTarget.Layer;
            _builderPanelDragLastMouse = position;
            _builderPanelDragHeaderToggleCandidate = false;
            return true;
        }

        return false;
    }

    private bool TryHandleLegacyGarrisonBuilderUiClick(Point position, bool leftClick)
    {
        if (_builderPropertyTarget != GarrisonBuilderPropertyTarget.None)
        {
            return HandleGarrisonBuilderPropertyEditorClick(position, leftClick);
        }

        if (_builderGameModeMenuOpen)
        {
            return TryHandleGarrisonBuilderGameModeMenuClick(position, leftClick);
        }

        var actionBounds = GetLegacyGarrisonBuilderActionBounds();
        if (new Rectangle(actionBounds.X, actionBounds.Y - LegacyBuilderButtonHeight, LegacyBuilderHeaderWidth, actionBounds.Height + LegacyBuilderButtonHeight).Contains(position))
        {
            if (leftClick && _builderActionExpanded && position.Y >= actionBounds.Y)
            {
                var visibleRows = GetLegacyGarrisonBuilderVisibleActionRowsForInput();
                var rowFromBottom = (actionBounds.Bottom - position.Y) / LegacyBuilderButtonHeight;
                if (rowFromBottom >= 0 && rowFromBottom < visibleRows && position.X < actionBounds.X + LegacyBuilderButtonWidth)
                {
                    var actionIndex = _builderActionScrollIndex + (visibleRows - 1 - rowFromBottom);
                    if (actionIndex >= 0 && actionIndex < _builderActionDefinitions.Length)
                    {
                        ApplyLegacyGarrisonBuilderAction(_builderActionDefinitions[actionIndex].Label);
                    }
                }
            }

            return true;
        }

        var entityBounds = GetLegacyGarrisonBuilderEntityBounds();
        if (entityBounds.Contains(position))
        {
            var definitions = GetActiveGarrisonBuilderEntityDefinitions();
            var index = GetLegacyGarrisonBuilderEntityHoverIndex(position);
            if (index >= 0 && index < definitions.Count)
            {
                SelectGarrisonBuilderEntity(definitions[index], updateStatus: leftClick);
                if (!leftClick)
                {
                    BeginEditingGarrisonBuilderPlacementProperties(definitions[index]);
                }
            }

            return true;
        }

        var layerBounds = GetLegacyGarrisonBuilderLayerBounds();
        if (layerBounds.Contains(position))
        {
            if (!leftClick)
            {
                return true;
            }

            var upBounds = new Rectangle(layerBounds.X + 3, layerBounds.Y + LegacyBuilderButtonHeight + 3, 20, 20);
            var downBounds = new Rectangle(layerBounds.X + 3, layerBounds.Y + LegacyBuilderButtonHeight + LegacyBuilderLayerHeight - 23, 20, 20);
            var clearBounds = new Rectangle(layerBounds.X + 27, layerBounds.Y + LegacyBuilderButtonHeight + LegacyBuilderLayerHeight - 23, 63, 20);
            var offsetsBounds = new Rectangle(layerBounds.X + 97, layerBounds.Y + LegacyBuilderButtonHeight + LegacyBuilderLayerHeight - 23, 60, 20);
            if (upBounds.Contains(position))
            {
                _builderLayerIndex = Math.Max(0, _builderLayerIndex - 1);
            }
            else if (downBounds.Contains(position))
            {
                _builderLayerIndex = Math.Min(8, _builderLayerIndex + 1);
            }
            else if (clearBounds.Contains(position))
            {
                ApplyLegacyGarrisonBuilderLayerClear();
            }
            else if (offsetsBounds.Contains(position))
            {
                ToggleGarrisonBuilderLayerOffsetEditing();
            }
            else if (position.Y > layerBounds.Y + LegacyBuilderButtonHeight)
            {
                ApplyLegacyGarrisonBuilderLayerBodyClick();
            }

            return true;
        }

        var resourceBounds = GetLegacyGarrisonBuilderResourceBounds();
        if (_builderDocument.Resources.Count > 0 && resourceBounds.Contains(position))
        {
            if (!leftClick)
            {
                return true;
            }

            var resources = GetGarrisonBuilderResourceRows();
            var rowIndex = (position.Y - resourceBounds.Y - LegacyBuilderButtonHeight) / LegacyBuilderButtonHeight;
            if (rowIndex >= 0 && rowIndex < LegacyBuilderResourceVisibleRows)
            {
                var resourceIndex = _builderResourceScrollIndex + rowIndex;
                if (resourceIndex >= 0 && resourceIndex < resources.Count)
                {
                    _builderSelectedResourceName = resources[resourceIndex].Name;
                    _builderStatus = $"selected resource {_builderSelectedResourceName}";
                }
            }

            return true;
        }

        return false;
    }

    private int GetLegacyGarrisonBuilderEntityHoverIndex(Point position)
    {
        var bounds = GetLegacyGarrisonBuilderEntityBounds();
        if (!bounds.Contains(position) || position.Y < bounds.Y + LegacyBuilderButtonHeight)
        {
            return -1;
        }

        var column = (position.X - bounds.X) / LegacyBuilderEntityButtonSize;
        var row = (position.Y - bounds.Y - LegacyBuilderButtonHeight) / LegacyBuilderEntityButtonSize;
        var index = (row * LegacyBuilderEntityColumns) + column;
        var definitions = GetActiveGarrisonBuilderEntityDefinitions();
        return index >= 0 && index < definitions.Count ? index : -1;
    }

    private void SelectGarrisonBuilderEntity(CustomMapBuilderEntityDefinition definition, bool updateStatus)
    {
        if (!string.Equals(_builderSelectedEntityType, definition.Type, StringComparison.OrdinalIgnoreCase))
        {
            _builderPlacementPropertyOverrides.Clear();
        }

        _builderSelectedEntityType = definition.Type;
        if (updateStatus)
        {
            _builderStatus = $"selected {definition.Label}";
        }
    }

    private void ApplyLegacyGarrisonBuilderAction(string label)
    {
        switch (label)
        {
            case "Load map":
                BeginEditingGarrisonBuilderPath(GarrisonBuilderPathField.OpenMap);
                break;
            case "Load BG":
                BeginEditingGarrisonBuilderPath(GarrisonBuilderPathField.Background);
                break;
            case "Load WM":
                BeginEditingGarrisonBuilderPath(GarrisonBuilderPathField.Walkmask);
                break;
            case "Show BG":
                _builderShowBackground = !_builderShowBackground;
                break;
            case "Show WM":
                _builderShowWalkmask = !_builderShowWalkmask;
                break;
            case "Show grid":
                _builderShowGrid = !_builderShowGrid;
                break;
            case "Show FG":
                _builderShowForeground = !_builderShowForeground;
                break;
            case "Save & test":
            case "Test w/o save":
                SaveGarrisonBuilderDocument();
                break;
            case "Symmetry mode":
                _builderSymmetry = !_builderSymmetry;
                _builderStatus = _builderSymmetry ? "symmetry mode enabled" : "symmetry mode disabled";
                break;
            case "Scale mode":
                _builderScaleMode = !_builderScaleMode;
                _builderStatus = _builderScaleMode ? "scale mode enabled" : "scale mode disabled";
                break;
            case "Fast scrolling":
                _builderFastScrolling = !_builderFastScrolling;
                _builderStatus = _builderFastScrolling ? "fast scrolling enabled" : "fast scrolling disabled";
                break;
            case "Map properties":
                BeginEditingGarrisonBuilderMapProperties();
                break;
            case "Add resource":
                BeginAddingGarrisonBuilderResource(CustomMapBuilderResourceKind.GenericImage, suggestedName: string.Empty);
                break;
            case "Get resources":
                BeginEditingGarrisonBuilderPath(GarrisonBuilderPathField.ResourceOutputDirectory);
                break;
            case "Load entities":
                BeginEditingGarrisonBuilderPath(GarrisonBuilderPathField.OpenMap);
                break;
            case "Save entities":
                BeginEditingGarrisonBuilderPath(GarrisonBuilderPathField.Save);
                break;
            case "Clear entities":
                _builderEntities.Clear();
                ClearGarrisonBuilderHiddenEntities();
                UpdateGarrisonBuilderDocumentEntities();
                _builderDirty = true;
                _builderStatus = "entities cleared";
                break;
        }
    }

    private bool IsLegacyBuilderActionToggled(string label)
    {
        return label switch
        {
            "Show BG" => _builderShowBackground,
            "Show WM" => _builderShowWalkmask,
            "Show grid" => _builderShowGrid,
            "Show FG" => _builderShowForeground,
            "Symmetry mode" => _builderSymmetry,
            "Scale mode" => _builderScaleMode,
            "Fast scrolling" => _builderFastScrolling,
            _ => false,
        };
    }

    private void ApplyLegacyGarrisonBuilderLayerClear()
    {
        if (_builderLayerIndex == 7)
        {
            _builderDocument = _builderDocument with { BackgroundImagePath = string.Empty };
            _builderBackgroundPathBuffer = string.Empty;
            _builderDirty = true;
            _builderLoadedBackgroundPath = string.Empty;
            _builderBackgroundTexture?.Dispose();
            _builderBackgroundTexture = null;
            _builderStatus = "background cleared";
        }
        else if (_builderLayerIndex == 8)
        {
            RemoveGarrisonBuilderForegroundResource();
            _builderShowForeground = false;
            _builderDirty = true;
            _builderStatus = "foreground hidden";
        }
        else
        {
            _builderDocument = _builderDocument with
            {
                ParallaxLayers = _builderDocument.ParallaxLayers
                    .Where(layer => layer.Index != _builderLayerIndex)
                    .ToArray(),
            };
            _builderDirty = true;
            _builderStatus = $"layer {_builderLayerIndex + 1} cleared";
        }
    }

    private void ApplyLegacyGarrisonBuilderLayerBodyClick()
    {
        if (_builderLayerIndex == 7)
        {
            BeginEditingGarrisonBuilderPath(GarrisonBuilderPathField.Background);
            return;
        }

        if (_builderLayerIndex == 8)
        {
            BeginAddingGarrisonBuilderResource(CustomMapBuilderResourceKind.Foreground, "bg_foreground");
            return;
        }

        BeginAddingGarrisonBuilderResource(CustomMapBuilderResourceKind.ParallaxLayer, $"bg_layer{_builderLayerIndex}");
    }

    private void BeginAddingGarrisonBuilderResource(CustomMapBuilderResourceKind kind, string suggestedName)
    {
        _builderPendingResourceKind = kind;
        _builderPendingResourceName = suggestedName.Trim();
        _builderResourceNameBuffer = suggestedName;
        _builderResourcePathBuffer = string.Empty;
        BeginEditingGarrisonBuilderPath(string.IsNullOrWhiteSpace(suggestedName)
            ? GarrisonBuilderPathField.ResourceName
            : GarrisonBuilderPathField.ResourcePath);
    }

    private void CommitGarrisonBuilderResourceName()
    {
        var name = _builderResourceNameBuffer.Trim();
        if (name.Length == 0)
        {
            _builderStatus = "resource name is required";
            return;
        }

        _builderPendingResourceName = name;
        BeginEditingGarrisonBuilderPath(GarrisonBuilderPathField.ResourcePath);
    }

    private void CommitGarrisonBuilderResourcePath()
    {
        var path = _builderResourcePathBuffer.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(_builderPendingResourceName))
        {
            _builderStatus = "resource name is required";
            BeginEditingGarrisonBuilderPath(GarrisonBuilderPathField.ResourceName);
            return;
        }

        try
        {
            var resource = CustomMapBuilderResourceCodec.FromFile(_builderPendingResourceName, path, _builderPendingResourceKind);
            var resources = new Dictionary<string, CustomMapBuilderResource>(_builderDocument.Resources, StringComparer.OrdinalIgnoreCase)
            {
                [resource.Name] = resource,
            };

            _builderDocument = _builderDocument with { Resources = resources };
            _builderSelectedResourceName = resource.Name;
            _builderPendingResourceName = string.Empty;
            RemoveGarrisonBuilderResourceTexture(resource.Name);
            if (resource.Kind == CustomMapBuilderResourceKind.ParallaxLayer && _builderLayerIndex is >= 0 and <= 6)
            {
                AssignGarrisonBuilderResourceToLayer(_builderLayerIndex, resource.Name);
            }
            else if (resource.Kind == CustomMapBuilderResourceKind.Foreground)
            {
                AssignGarrisonBuilderResourceToForeground(resource.Name);
            }
            else
            {
                _builderDirty = true;
            }

            _builderStatus = $"resource loaded: {resource.Name}";
            AddConsoleLine($"builder resource: {resource.Name} <- {path}");
        }
        catch (Exception ex)
        {
            _builderStatus = $"resource load failed: {ex.Message}";
            AddConsoleLine(_builderStatus);
        }
    }

    private bool TryAssignSelectedGarrisonBuilderResourceToLayer(int index)
    {
        if (string.IsNullOrWhiteSpace(_builderSelectedResourceName)
            || !_builderDocument.Resources.TryGetValue(_builderSelectedResourceName, out var resource))
        {
            return false;
        }

        if (resource.Kind == CustomMapBuilderResourceKind.Foreground)
        {
            resource = resource with { Kind = CustomMapBuilderResourceKind.ParallaxLayer };
            var resources = new Dictionary<string, CustomMapBuilderResource>(_builderDocument.Resources, StringComparer.OrdinalIgnoreCase)
            {
                [resource.Name] = resource,
            };
            _builderDocument = _builderDocument with { Resources = resources };
        }

        AssignGarrisonBuilderResourceToLayer(index, resource.Name);
        return true;
    }

    private void AssignGarrisonBuilderResourceToLayer(int index, string resourceName)
    {
        var layers = _builderDocument.ParallaxLayers
            .Where(layer => layer.Index != index)
            .Append(new CustomMapBuilderParallaxLayer(index, resourceName, GetGarrisonBuilderLayer(index).XFactor, GetGarrisonBuilderLayer(index).YFactor))
            .OrderBy(layer => layer.Index)
            .ToArray();
        _builderDocument = _builderDocument with { ParallaxLayers = layers };
        _builderDirty = true;
        _builderStatus = $"assigned {resourceName} to layer {index + 1}";
    }

    private bool TryAssignSelectedGarrisonBuilderResourceToForeground()
    {
        if (string.IsNullOrWhiteSpace(_builderSelectedResourceName)
            || !_builderDocument.Resources.ContainsKey(_builderSelectedResourceName))
        {
            return false;
        }

        AssignGarrisonBuilderResourceToForeground(_builderSelectedResourceName);
        return true;
    }

    private void AssignGarrisonBuilderResourceToForeground(string resourceName)
    {
        if (!_builderDocument.Resources.TryGetValue(resourceName, out var resource))
        {
            return;
        }

        RemoveGarrisonBuilderForegroundResource();
        var resources = new Dictionary<string, CustomMapBuilderResource>(_builderDocument.Resources, StringComparer.OrdinalIgnoreCase)
        {
            [resource.Name] = resource with { Kind = CustomMapBuilderResourceKind.Foreground },
        };
        _builderDocument = _builderDocument with { Resources = resources };
        _builderShowForeground = true;
        _builderDirty = true;
        _builderStatus = $"assigned {resourceName} to foreground";
    }

    private void RemoveGarrisonBuilderForegroundResource()
    {
        var resources = new Dictionary<string, CustomMapBuilderResource>(_builderDocument.Resources, StringComparer.OrdinalIgnoreCase);
        foreach (var resource in _builderDocument.Resources.Values.Where(resource => resource.Kind == CustomMapBuilderResourceKind.Foreground).ToArray())
        {
            resources[resource.Name] = resource with { Kind = CustomMapBuilderResourceKind.GenericImage };
        }

        _builderDocument = _builderDocument with { Resources = resources };
    }

    private CustomMapBuilderParallaxLayer GetGarrisonBuilderLayer(int index)
    {
        foreach (var layer in _builderDocument.ParallaxLayers)
        {
            if (layer.Index == index)
            {
                return layer;
            }
        }

        return new CustomMapBuilderParallaxLayer(index, string.Empty);
    }

    private CustomMapBuilderResource? GetGarrisonBuilderForegroundResource()
    {
        foreach (var resource in _builderDocument.Resources.Values)
        {
            if (resource.Kind == CustomMapBuilderResourceKind.Foreground)
            {
                return resource;
            }
        }

        return null;
    }

    private Texture2D? GetGarrisonBuilderResourceTexture(string resourceName)
    {
        if (_builderResourceTextureCache.TryGetValue(resourceName, out var cached))
        {
            return cached;
        }

        if (!_builderDocument.Resources.TryGetValue(resourceName, out var resource)
            || !CustomMapBuilderResourceCodec.TryGetResourceBytes(resource, out var bytes)
            || bytes.Length == 0)
        {
            return null;
        }

        try
        {
            var texture = TextureDecodeUtility.LoadTexture(GraphicsDevice, bytes, applyLegacyChromaKey: false);
            _builderResourceTextureCache[resourceName] = texture;
            return texture;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void DrawGarrisonBuilderResourcePreview(Texture2D texture, Rectangle bounds, Color? tint = null)
    {
        var scale = Math.Min(bounds.Width / (float)texture.Width, bounds.Height / (float)texture.Height);
        _spriteBatch.Draw(texture, bounds.Location.ToVector2(), null, tint ?? Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void RemoveGarrisonBuilderResourceTexture(string resourceName)
    {
        if (_builderResourceTextureCache.Remove(resourceName, out var texture))
        {
            texture.Dispose();
        }
    }

    private void ClearGarrisonBuilderResourceTextureCache()
    {
        foreach (var texture in _builderResourceTextureCache.Values)
        {
            texture.Dispose();
        }

        _builderResourceTextureCache.Clear();
    }

    private void OpenGarrisonBuilderLayerParallaxDialog(int layerIndex)
    {
        if (layerIndex is < 0 or > 6)
        {
            _builderStatus = "select a parallax layer (L1-L7)";
            return;
        }

        _builderLayerIndex = layerIndex;
        _builderEditingLayerOffsets = false;
        _builderLayerOffsetDragging = false;
        var layer = GetGarrisonBuilderLayer(layerIndex);
        _builderLayerParallaxDialogOpen = true;
        _builderLayerParallaxDialogLayerIndex = layerIndex;
        _builderLayerParallaxEditField = GarrisonBuilderLayerParallaxEditField.None;
        _builderLayerParallaxXBuffer = layer.XFactor.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _builderLayerParallaxYBuffer = layer.YFactor.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _builderLayerParallaxCursorIndex = 0;
        _builderLayerParallaxSelectionStart = 0;
        _builderStatus = $"parallax strength for layer {layerIndex + 1}";
    }

    private void OpenGarrisonBuilderLayerParallaxDialog()
    {
        OpenGarrisonBuilderLayerParallaxDialog(_builderLayerIndex);
    }

    private void CloseGarrisonBuilderLayerParallaxDialog(bool applyChanges)
    {
        if (applyChanges && _builderLayerParallaxDialogLayerIndex is >= 0 and <= 6)
        {
            if (!float.TryParse(_builderLayerParallaxXBuffer.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var xFactor)
                || !float.TryParse(_builderLayerParallaxYBuffer.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var yFactor))
            {
                _builderStatus = "enter numeric X and Y parallax strength values";
                return;
            }

            RecordGarrisonBuilderHistory();
            SetGarrisonBuilderLayerFactors(_builderLayerParallaxDialogLayerIndex, xFactor, yFactor);
        }

        _builderLayerParallaxDialogOpen = false;
        _builderLayerParallaxDialogLayerIndex = -1;
        _builderLayerParallaxEditField = GarrisonBuilderLayerParallaxEditField.None;
    }

    private Rectangle GetGarrisonBuilderLayerParallaxDialogBounds()
    {
        var width = Math.Min(BuilderUi(420), Math.Max(BuilderUi(300), BuilderViewportWidth - BuilderUi(80)));
        var height = BuilderUi(188);
        return new Rectangle((BuilderViewportWidth - width) / 2, (BuilderViewportHeight - height) / 2, width, height);
    }

    private void DrawGarrisonBuilderLayerParallaxDialog(MouseState mouse)
    {
        if (!_builderLayerParallaxDialogOpen)
        {
            return;
        }

        var bounds = GetGarrisonBuilderLayerParallaxDialogBounds();
        DrawMenuPanelBackdrop(bounds, 0.98f);
        var scale = GetGarrisonBuilderBitmapFontScale();
        var layerNumber = _builderLayerParallaxDialogLayerIndex + 1;
        DrawBitmapFontText($"Layer {layerNumber} parallax strength", new Vector2(bounds.X + 14f, bounds.Y + 10f), Color.White, scale);
        DrawBitmapFontText(
            "1.0 = normal. Higher = stronger scroll parallax in-game.",
            new Vector2(bounds.X + 14f, bounds.Y + 30f),
            new Color(200, 190, 168),
            GetGarrisonBuilderBitmapFontScale());

        var fieldHeight = GetGarrisonBuilderMenuRowHeight();
        var xFieldBounds = new Rectangle(bounds.X + 12, bounds.Y + 54, bounds.Width - 24, fieldHeight);
        var yFieldBounds = new Rectangle(bounds.X + 12, bounds.Y + 88, bounds.Width - 24, fieldHeight);
        DrawBuilderMenuButton(
            xFieldBounds,
            $"X strength: {GetGarrisonBuilderLayerParallaxFieldDisplay(_builderLayerParallaxXBuffer, GarrisonBuilderLayerParallaxEditField.XFactor)}",
            _builderLayerParallaxEditField == GarrisonBuilderLayerParallaxEditField.XFactor || xFieldBounds.Contains(mouse.Position));
        DrawBuilderMenuButton(
            yFieldBounds,
            $"Y strength: {GetGarrisonBuilderLayerParallaxFieldDisplay(_builderLayerParallaxYBuffer, GarrisonBuilderLayerParallaxEditField.YFactor)}",
            _builderLayerParallaxEditField == GarrisonBuilderLayerParallaxEditField.YFactor || yFieldBounds.Contains(mouse.Position));

        var actionHeight = GetGarrisonBuilderMenuRowHeight();
        var saveBounds = new Rectangle(bounds.X + 12, bounds.Bottom - 38, 96, actionHeight);
        var cancelBounds = new Rectangle(saveBounds.Right + 8, saveBounds.Y, 96, actionHeight);
        DrawBuilderMenuButton(saveBounds, "Save", saveBounds.Contains(mouse.Position));
        DrawBuilderMenuButton(cancelBounds, "Cancel", cancelBounds.Contains(mouse.Position));
    }

    private string GetGarrisonBuilderLayerParallaxFieldDisplay(string buffer, GarrisonBuilderLayerParallaxEditField field)
    {
        if (_builderLayerParallaxEditField != field)
        {
            return buffer.Length > 0 ? buffer : "1";
        }

        return GetTextWithCursor(buffer, _builderLayerParallaxCursorIndex);
    }

    private bool UpdateGarrisonBuilderLayerParallaxDialog(KeyboardState keyboard, MouseState mouse)
    {
        if (!_builderLayerParallaxDialogOpen)
        {
            return false;
        }

        if (IsKeyPressed(keyboard, Keys.Escape))
        {
            if (_builderLayerParallaxEditField != GarrisonBuilderLayerParallaxEditField.None)
            {
                _builderLayerParallaxEditField = GarrisonBuilderLayerParallaxEditField.None;
                return true;
            }

            CloseGarrisonBuilderLayerParallaxDialog(applyChanges: false);
            return true;
        }

        if (_builderLayerParallaxEditField != GarrisonBuilderLayerParallaxEditField.None)
        {
            if (IsKeyPressed(keyboard, Keys.Enter))
            {
                _builderLayerParallaxEditField = GarrisonBuilderLayerParallaxEditField.None;
                return true;
            }

            if (IsKeyPressed(keyboard, Keys.Back))
            {
                var buffer = _builderLayerParallaxEditField == GarrisonBuilderLayerParallaxEditField.XFactor
                    ? _builderLayerParallaxXBuffer
                    : _builderLayerParallaxYBuffer;
                var result = DeleteTextSelectionOrBackspace(buffer, _builderLayerParallaxCursorIndex, _builderLayerParallaxSelectionStart);
                ApplyGarrisonBuilderLayerParallaxEditBuffers(result, _builderLayerParallaxEditField);
                return true;
            }

            return true;
        }

        if (mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released)
        {
            HandleGarrisonBuilderLayerParallaxDialogClick(mouse.Position);
            return true;
        }

        return true;
    }

    private void ApplyGarrisonBuilderLayerParallaxEditBuffers((string Text, int CursorIndex, int SelectionStart) result, GarrisonBuilderLayerParallaxEditField field)
    {
        if (field == GarrisonBuilderLayerParallaxEditField.XFactor)
        {
            _builderLayerParallaxXBuffer = result.Text;
        }
        else
        {
            _builderLayerParallaxYBuffer = result.Text;
        }

        _builderLayerParallaxCursorIndex = result.CursorIndex;
        _builderLayerParallaxSelectionStart = result.SelectionStart;
    }

    private void HandleGarrisonBuilderLayerParallaxDialogClick(Point position)
    {
        var bounds = GetGarrisonBuilderLayerParallaxDialogBounds();
        if (!bounds.Contains(position))
        {
            CloseGarrisonBuilderLayerParallaxDialog(applyChanges: false);
            return;
        }

        var saveBounds = new Rectangle(bounds.X + 12, bounds.Bottom - 38, 96, 26);
        var cancelBounds = new Rectangle(saveBounds.Right + 8, saveBounds.Y, 96, 26);
        if (saveBounds.Contains(position))
        {
            CloseGarrisonBuilderLayerParallaxDialog(applyChanges: true);
            return;
        }

        if (cancelBounds.Contains(position))
        {
            CloseGarrisonBuilderLayerParallaxDialog(applyChanges: false);
            return;
        }

        var xFieldBounds = new Rectangle(bounds.X + 12, bounds.Y + 54, bounds.Width - 24, 26);
        var yFieldBounds = new Rectangle(bounds.X + 12, bounds.Y + 88, bounds.Width - 24, 26);
        if (xFieldBounds.Contains(position))
        {
            BeginGarrisonBuilderLayerParallaxFieldEdit(GarrisonBuilderLayerParallaxEditField.XFactor);
            return;
        }

        if (yFieldBounds.Contains(position))
        {
            BeginGarrisonBuilderLayerParallaxFieldEdit(GarrisonBuilderLayerParallaxEditField.YFactor);
        }
    }

    private void BeginGarrisonBuilderLayerParallaxFieldEdit(GarrisonBuilderLayerParallaxEditField field)
    {
        _builderLayerParallaxEditField = field;
        if (field == GarrisonBuilderLayerParallaxEditField.XFactor)
        {
            _builderLayerParallaxCursorIndex = _builderLayerParallaxXBuffer.Length;
            _builderLayerParallaxSelectionStart = _builderLayerParallaxCursorIndex;
            return;
        }

        _builderLayerParallaxCursorIndex = _builderLayerParallaxYBuffer.Length;
        _builderLayerParallaxSelectionStart = _builderLayerParallaxCursorIndex;
    }

    private bool HandleGarrisonBuilderLayerParallaxTextInput(char character)
    {
        if (!_builderLayerParallaxDialogOpen || _builderLayerParallaxEditField == GarrisonBuilderLayerParallaxEditField.None)
        {
            return false;
        }

        var buffer = _builderLayerParallaxEditField == GarrisonBuilderLayerParallaxEditField.XFactor
            ? _builderLayerParallaxXBuffer
            : _builderLayerParallaxYBuffer;
        var result = InsertTextCharacterAtCursor(buffer, character, _builderLayerParallaxCursorIndex, _builderLayerParallaxSelectionStart, 16);
        ApplyGarrisonBuilderLayerParallaxEditBuffers(result, _builderLayerParallaxEditField);
        return true;
    }

    private void BeginEditingGarrisonBuilderMapProperties()
    {
        var normalized = _builderDocument.NormalizeForEditing();
        _builderPropertyTarget = GarrisonBuilderPropertyTarget.MapProperties;
        _builderPropertyEditMode = GarrisonBuilderPropertyEditMode.List;
        _builderPropertyEditorTitle = "Map properties";
        _builderPropertyScrollIndex = 0;
        _builderPropertyEditorValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GarrisonBuilderMapPropertyNameKey] = normalized.Name,
            [GarrisonBuilderMapPropertyVisualScaleKey] = normalized.VisualScale.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [GarrisonBuilderMapPropertyWalkmaskScaleKey] = normalized.Scale.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [ScrMapSettingsMetadata.ShowControlPointsPropertyKey] = ScrMapSettingsMetadata.ToShowControlPointsPropertyValue(
                ScrMapSettingsMetadata.ParseShowControlPoints(normalized.Metadata)),
            [ScrMapSettingsMetadata.ScoreToWinPropertyKey] = ScrMapSettingsMetadata.ClampScore(
                ScrMapSettingsMetadata.ParseScoreToWin(normalized.Metadata)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            [ScrMapSettingsMetadata.WinWhenScorePropertyKey] = ScrMapSettingsMetadata.ToWinWhenScorePropertyValue(
                ScrMapSettingsMetadata.ParseWinWhenScore(normalized.Metadata)),
            [ScrMapSettingsMetadata.RoundEndWinPropertyKey] = ScrMapSettingsMetadata.ToRoundEndWinPropertyValue(
                ScrMapSettingsMetadata.ParseRoundEndWin(normalized.Metadata)),
            [ScrMapSettingsMetadata.RedStartingScorePropertyKey] = ScrMapSettingsMetadata.ClampScore(
                ScrMapSettingsMetadata.ParseScrSettings(normalized.Metadata).RedStartingScore).ToString(System.Globalization.CultureInfo.InvariantCulture),
            [ScrMapSettingsMetadata.BlueStartingScorePropertyKey] = ScrMapSettingsMetadata.ClampScore(
                ScrMapSettingsMetadata.ParseScrSettings(normalized.Metadata).BlueStartingScore).ToString(System.Globalization.CultureInfo.InvariantCulture),
            [ControlPointMapSettingsMetadata.OverrideInitialCpsPropertyKey] = ControlPointMapSettingsMetadata.ToPropertyValue(
                ControlPointMapSettingsMetadata.ParseOverrideInitialCps(normalized.Metadata)),
            ["background"] = normalized.Metadata.TryGetValue("background", out var background)
                ? background
                : CustomMapBuilderDocument.DefaultBackgroundColor,
            ["void"] = normalized.Metadata.TryGetValue("void", out var voidColor)
                ? voidColor
                : CustomMapBuilderDocument.DefaultVoidColor,
        };

        foreach (var pair in normalized.Metadata)
        {
            if (IsGarrisonBuilderMapPropertiesManagedMetadataKey(pair.Key)
                || !ControlPointMapSettingsMetadata.IsEditableMapMetadataKey(pair.Key))
            {
                continue;
            }

            _builderPropertyEditorValues[pair.Key] = pair.Value;
        }

        _builderStatus = "editing map properties";
    }

    private static bool IsGarrisonBuilderMapPropertiesManagedMetadataKey(string key)
    {
        return key.Equals("background", StringComparison.OrdinalIgnoreCase)
            || key.Equals("void", StringComparison.OrdinalIgnoreCase)
            || key.Equals(CustomMapEntityRuntimeRegistry.EntitySchemaMetadataKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapGameModeMetadata.GameModePropertyKey, StringComparison.OrdinalIgnoreCase)
            || IsSkippedGarrisonBuilderPropertyKey(key);
    }

    private bool ShouldShowGarrisonBuilderMapPropertyRow(string key)
    {
        if (ScrMapSettingsMetadata.IsScrOnlyMapMetadataKey(key))
        {
            return _builderSelectedGameMode == CustomMapBuilderGameMode.Scr;
        }

        return true;
    }

    private static bool IsGarrisonBuilderMapPropertyEditorKey(string key)
    {
        return key.StartsWith("$", StringComparison.Ordinal)
            || key.Equals(GarrisonBuilderMapPropertyNameKey, StringComparison.Ordinal)
            || key.Equals(GarrisonBuilderMapPropertyVisualScaleKey, StringComparison.Ordinal)
            || key.Equals(GarrisonBuilderMapPropertyWalkmaskScaleKey, StringComparison.Ordinal);
    }

    private bool TryApplyGarrisonBuilderMapPropertiesFromEditorValues()
    {
        if (!_builderPropertyEditorValues.TryGetValue(GarrisonBuilderMapPropertyNameKey, out var nameBuffer))
        {
            _builderStatus = "map name is required";
            return false;
        }

        var name = nameBuffer.Trim();
        if (name.Length == 0)
        {
            _builderStatus = "map name is required";
            return false;
        }

        if (!_builderPropertyEditorValues.TryGetValue(GarrisonBuilderMapPropertyVisualScaleKey, out var visualScaleBuffer)
            || !float.TryParse(visualScaleBuffer.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var visualScale)
            || visualScale <= 0f)
        {
            _builderStatus = "enter a positive visual scale";
            return false;
        }

        if (!_builderPropertyEditorValues.TryGetValue(GarrisonBuilderMapPropertyWalkmaskScaleKey, out var walkmaskScaleBuffer)
            || !float.TryParse(walkmaskScaleBuffer.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var walkmaskScale)
            || walkmaskScale <= 0f)
        {
            _builderStatus = "enter a positive walkmask scale";
            return false;
        }

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in _builderPropertyEditorValues)
        {
            if (IsGarrisonBuilderMapPropertyEditorKey(pair.Key)
                || IsGarrisonBuilderMapPropertiesManagedMetadataKey(pair.Key))
            {
                continue;
            }

            metadata[pair.Key] = pair.Value;
        }

        if (_builderDocument.Metadata.TryGetValue(CustomMapEntityRuntimeRegistry.EntitySchemaMetadataKey, out var entitySchema))
        {
            metadata[CustomMapEntityRuntimeRegistry.EntitySchemaMetadataKey] = entitySchema;
        }

        _builderPropertyEditorValues.TryGetValue("background", out var backgroundBuffer);
        _builderPropertyEditorValues.TryGetValue("void", out var voidBuffer);
        metadata["background"] = NormalizeGarrisonBuilderHexColor(backgroundBuffer ?? string.Empty, CustomMapBuilderDocument.DefaultBackgroundColor);
        metadata["void"] = NormalizeGarrisonBuilderHexColor(voidBuffer ?? string.Empty, CustomMapBuilderDocument.DefaultVoidColor);
        RecordGarrisonBuilderHistory();
        _builderDocument = _builderDocument with
        {
            Name = name,
            Scale = walkmaskScale,
            VisualScale = visualScale,
            Metadata = metadata,
        };
        _builderDocument = _builderDocument.NormalizeForEditing();
        _builderDirty = true;
        _builderStatus = $"map properties updated (visual {visualScale:0.##}, walkmask {walkmaskScale:0.##})";
        RequestGarrisonBuilderCameraCenter();
        return true;
    }

    private static string NormalizeGarrisonBuilderHexColor(string value, string fallback)
    {
        var trimmed = value.Trim().TrimStart('#');
        return trimmed.Length == 6 ? trimmed : fallback;
    }

    private void ToggleGarrisonBuilderLayerOffsetEditing()
    {
        OpenGarrisonBuilderLayerParallaxDialog();
    }

    private bool UpdateGarrisonBuilderLayerOffsetEditing(MouseState mouse)
    {
        if (!_builderEditingLayerOffsets || _builderLayerIndex is < 0 or > 6)
        {
            return false;
        }

        if (!_builderUseModernUi
            && GetLegacyGarrisonBuilderLayerBounds().Contains(mouse.Position)
            && mouse.LeftButton == ButtonState.Pressed
            && _previousMouse.LeftButton == ButtonState.Released)
        {
            return false;
        }

        if (mouse.LeftButton == ButtonState.Pressed)
        {
            var layer = GetGarrisonBuilderLayer(_builderLayerIndex);
            if (!_builderLayerOffsetDragging)
            {
                _builderLayerOffsetHoldMouse = mouse.Position;
                _builderLayerOffsetDragging = true;
                return true;
            }

            var xFactor = layer.XFactor - ((_builderLayerOffsetHoldMouse.X - mouse.X) / 100f);
            var yFactor = layer.YFactor + ((_builderLayerOffsetHoldMouse.Y - mouse.Y) / 100f);
            SetGarrisonBuilderLayerFactors(_builderLayerIndex, xFactor, yFactor);
            _builderLayerOffsetHoldMouse = mouse.Position;
            return true;
        }

        if (mouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Released)
        {
            _builderLayerOffsetDragging = false;
            _builderEditingLayerOffsets = false;
            _builderStatus = "layer offsets saved";
            return true;
        }

        _builderLayerOffsetDragging = false;
        return true;
    }

    private void SetGarrisonBuilderLayerFactors(int index, float xFactor, float yFactor)
    {
        var layer = GetGarrisonBuilderLayer(index);
        var layers = _builderDocument.ParallaxLayers
            .Where(existing => existing.Index != index)
            .Append(new CustomMapBuilderParallaxLayer(index, layer.ResourceName, xFactor, yFactor, layer.Visible).NormalizeForEditing())
            .OrderBy(existing => existing.Index)
            .ToArray();
        _builderDocument = _builderDocument with { ParallaxLayers = layers };
        _builderDirty = true;
        _builderStatus = $"layer {index + 1} offsets {xFactor:0.00}, {yFactor:0.00}";
    }

    private void DrawGarrisonBuilderLayerOffsetOverlay(MouseState mouse)
    {
        if (!_builderEditingLayerOffsets || _builderLayerIndex is < 0 or > 6)
        {
            return;
        }

        var layer = GetGarrisonBuilderLayer(_builderLayerIndex);
        var x = (BuilderViewportWidth / 2) + (int)MathF.Round((layer.XFactor - 1f) * BuilderUi(100f));
        var y = (BuilderViewportHeight / 2) + (int)MathF.Round((layer.YFactor - 1f) * BuilderUi(100f));
        var color = _builderLayerOffsetDragging ? new Color(255, 232, 80) : new Color(120, 220, 255);
        _spriteBatch.Draw(_pixel, new Rectangle(0, y, BuilderViewportWidth, 1), color);
        _spriteBatch.Draw(_pixel, new Rectangle(x, 0, 1, BuilderViewportHeight), color);
        _spriteBatch.Draw(_pixel, new Rectangle((BuilderViewportWidth / 2) - BuilderUi(5), BuilderViewportHeight / 2, BuilderUi(10), 1), Color.White);
        _spriteBatch.Draw(_pixel, new Rectangle(BuilderViewportWidth / 2, (BuilderViewportHeight / 2) - BuilderUi(5), 1, BuilderUi(10)), Color.White);

        var tooltip = $"X:{layer.XFactor:0.00} Y:{layer.YFactor:0.00}";
        var tooltipPosition = mouse.Position.ToVector2() + new Vector2(12f, 12f);
        var size = MeasureGarrisonBuilderText(tooltip, 0.95f);
        var background = new Rectangle((int)tooltipPosition.X - 4, (int)tooltipPosition.Y - 3, (int)size.X + 8, (int)size.Y + 6);
        _spriteBatch.Draw(_pixel, background, new Color(0, 0, 0, 190));
        DrawGarrisonBuilderText(tooltip, tooltipPosition, Color.White, 0.95f);
    }

    private void ExportGarrisonBuilderResources(string outputDirectory)
    {
        outputDirectory = outputDirectory.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            _builderStatus = "resource output directory is required";
            return;
        }

        try
        {
            UpdateGarrisonBuilderDocumentEntities();
            var manifestPath = Path.Combine(outputDirectory, $"{_builderDocument.NormalizeForEditing().Name}.json");
            CustomMapPackageExporter.Export(_builderDocument, manifestPath);
            _builderResourceOutputDirectoryBuffer = outputDirectory;
            _builderStatus = $"exported package {Path.GetFileName(manifestPath)}";
            AddConsoleLine($"builder package exported: {manifestPath}");
        }
        catch (Exception ex)
        {
            _builderStatus = $"package export failed: {ex.Message}";
            AddConsoleLine(_builderStatus);
        }
    }

    private static void WriteGarrisonBuilderWalkmaskPng(string embeddedWalkmaskSection, string outputPath)
    {
        var lines = embeddedWalkmaskSection
            .Trim()
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 3
            || !int.TryParse(lines[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var width)
            || !int.TryParse(lines[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var height)
            || width <= 0
            || height <= 0)
        {
            throw new InvalidOperationException("Embedded walkmask section is invalid.");
        }

        var packed = string.Concat(lines.Skip(2));
        using var image = new SixLabors.ImageSharp.Image<Rgba32>(width, height);
        var pixelIndex = 0;
        foreach (var character in packed)
        {
            var value = character - 32;
            for (var bit = 5; bit >= 0 && pixelIndex < width * height; bit -= 1)
            {
                if (((value >> bit) & 1) != 0)
                {
                    var x = pixelIndex % width;
                    var y = pixelIndex / width;
                    image[x, y] = new Rgba32(255, 255, 255, 255);
                }

                pixelIndex += 1;
            }
        }

        using var output = File.Create(outputPath);
        image.Save(output, new PngEncoder());
    }

    private Rectangle GetLegacyGarrisonBuilderActionBounds()
    {
        var rows = Math.Clamp(_builderActionVisibleRows, 1, _builderActionDefinitions.Length);
        var eased = _builderActionExpandProgress <= 0f
            ? 0f
            : MathF.Sqrt(1f - MathF.Pow(1f - _builderActionExpandProgress, 2f));
        var height = (int)MathF.Round(rows * LegacyBuilderButtonHeight * eased);
        return new Rectangle(
            Math.Clamp(_builderActionPanelOffset.X, 0, Math.Max(0, BuilderViewportWidth - LegacyBuilderHeaderWidth)),
            Math.Clamp(BuilderViewportHeight - height + _builderActionPanelOffset.Y, LegacyBuilderButtonHeight, Math.Max(LegacyBuilderButtonHeight, BuilderViewportHeight - height)),
            LegacyBuilderHeaderWidth,
            height);
    }

    private int GetLegacyGarrisonBuilderVisibleActionRowsForInput()
    {
        if (_builderActionExpandProgress < 0.95f)
        {
            return 0;
        }

        return Math.Min(_builderActionVisibleRows, _builderActionDefinitions.Length);
    }

    private Rectangle GetLegacyGarrisonBuilderEntityBounds()
    {
        var rows = (int)MathF.Ceiling(GetActiveGarrisonBuilderEntityDefinitions().Count / (float)LegacyBuilderEntityColumns);
        var width = LegacyBuilderEntityColumns * LegacyBuilderEntityButtonSize;
        var height = LegacyBuilderButtonHeight + (rows * LegacyBuilderEntityButtonSize);
        return new Rectangle(
            Math.Clamp(BuilderViewportWidth - width - 1 + _builderEntityPanelOffset.X, 1, Math.Max(1, BuilderViewportWidth - width - 1)),
            Math.Clamp(_builderEntityPanelOffset.Y, 0, Math.Max(0, BuilderViewportHeight - (2 * LegacyBuilderEntityButtonSize) - 1)),
            width,
            height);
    }

    private Rectangle GetLegacyGarrisonBuilderLayerBounds()
    {
        return new Rectangle(
            Math.Clamp(BuilderViewportWidth - LegacyBuilderLayerWidth - 1 + _builderLayerPanelOffset.X, 1, Math.Max(1, BuilderViewportWidth - LegacyBuilderLayerWidth - 1)),
            Math.Clamp(BuilderViewportHeight - LegacyBuilderLayerHeight - LegacyBuilderButtonHeight - 1 + _builderLayerPanelOffset.Y, 0, Math.Max(0, BuilderViewportHeight - LegacyBuilderLayerHeight - LegacyBuilderButtonHeight - 1)),
            LegacyBuilderLayerWidth,
            LegacyBuilderLayerHeight + LegacyBuilderButtonHeight);
    }

    private Rectangle GetLegacyGarrisonBuilderResourceBounds()
    {
        var layerBounds = GetLegacyGarrisonBuilderLayerBounds();
        var height = LegacyBuilderButtonHeight + (LegacyBuilderResourceVisibleRows * LegacyBuilderButtonHeight);
        return new Rectangle(
            layerBounds.X,
            Math.Max(GetLegacyGarrisonBuilderEntityBounds().Bottom + 4, layerBounds.Y - height - 4),
            LegacyBuilderResourceWidth,
            height);
    }

    private Rectangle GetGarrisonBuilderGameModeMenuBounds()
    {
        var items = GetGarrisonBuilderModeMenuItems();
        var width = Math.Min(BuilderUi(420), Math.Max(BuilderUi(300), BuilderViewportWidth - BuilderUi(80)));
        var height = BuilderUi(64) + (items.Length * BuilderUi(34));
        return new Rectangle(
            Math.Clamp((BuilderViewportWidth - width) / 2, BuilderUi(20), Math.Max(BuilderUi(20), BuilderViewportWidth - width - BuilderUi(20))),
            Math.Clamp((BuilderViewportHeight - height) / 2, BuilderUi(20), Math.Max(BuilderUi(20), BuilderViewportHeight - height - BuilderUi(20))),
            width,
            height);
    }

    private static Rectangle GetGarrisonBuilderModeMenuItemBounds(Rectangle menuBounds, int index)
    {
        return new Rectangle(menuBounds.X + BuilderUi(18), menuBounds.Y + BuilderUi(50) + (index * BuilderUi(34)), menuBounds.Width - BuilderUi(36), BuilderUi(28));
    }

    private List<CustomMapBuilderResource> GetGarrisonBuilderResourceRows()
    {
        return _builderDocument.Resources.Values
            .OrderBy(resource => resource.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string GetLegacyGarrisonBuilderLayerName()
    {
        return _builderLayerIndex switch
        {
            8 => "FG",
            7 => "BG",
            _ => $"L{_builderLayerIndex + 1}",
        };
    }

    private string GetLegacyGarrisonBuilderPathPromptTitle()
    {
        return _builderActivePathField switch
        {
            GarrisonBuilderPathField.OpenMap => "Load map",
            GarrisonBuilderPathField.Background => "Load BG",
            GarrisonBuilderPathField.Walkmask => "Load WM",
            GarrisonBuilderPathField.Save => "Save entities / map",
            GarrisonBuilderPathField.ResourceName => "Add resource",
            GarrisonBuilderPathField.ResourcePath => "Load resource",
            GarrisonBuilderPathField.ResourceOutputDirectory => "Get resources",
            _ => "Path",
        };
    }

    private void PlaceGarrisonBuilderEntity(Vector2 worldPosition)
    {
        var snapped = SnapGarrisonBuilderPoint(worldPosition);
        var snappedX = snapped.X;
        var snappedY = snapped.Y;
        var entity = CustomMapBuilderEntityCatalog.TryGetDefinition(_builderSelectedEntityType, out var definition)
            ? CreateGarrisonBuilderEntityFromDefinition(definition, snappedX, snappedY)
            : CustomMapBuilderEntity.Create(_builderSelectedEntityType, snappedX, snappedY).NormalizeForEditing();
        _builderEntities.Add(entity);
        UpdateGarrisonBuilderDocumentEntities();
        _builderDirty = true;
        _builderStatus = $"placed {_builderSelectedEntityType} at {snappedX:0}, {snappedY:0}";
    }

    private void BeginGarrisonBuilderPlacement(Vector2 worldPosition)
    {
        RecordGarrisonBuilderHistory();
        _builderPlacementDragging = true;
        _builderPlacementScaleLock = true;
        _builderPlacementStartWorld = SnapGarrisonBuilderPoint(worldPosition);
        _builderPlacementCurrentWorld = _builderPlacementStartWorld;
    }

    private void UpdateGarrisonBuilderPlacementPreview(MouseState mouse)
    {
        var placementWorld = _builderUseModernUi
            ? BuilderScreenToWorld(mouse.Position)
            : mouse.Position.ToVector2() + _builderCamera;
        var showPlacementHoverPreview = _builderUseModernUi
            && _builderActiveTool == GarrisonBuilderTool.Place
            && !string.IsNullOrWhiteSpace(_builderSelectedEntityType)
            && !_builderPlacementDragging
            && GetModernGarrisonBuilderMapViewport().Contains(mouse.Position);

        if (showPlacementHoverPreview)
        {
            var previewWorld = SnapGarrisonBuilderPoint(placementWorld);
            ApplyGarrisonBuilderPlacementAlignmentSnap(ref previewWorld);
            _builderPlacementPreviewWorld = previewWorld;
        }
        else if (!_builderPlacementDragging)
        {
            ClearGarrisonBuilderPlacementAlignmentSnap();
        }

        if (_builderPlacementDragging)
        {
            var currentWorld = SnapGarrisonBuilderPoint(placementWorld);
            ApplyGarrisonBuilderPlacementAlignmentSnap(ref currentWorld);
            _builderPlacementCurrentWorld = currentWorld;
            if (_builderPlacementScaleLock
                && (MathF.Abs(_builderPlacementCurrentWorld.X - _builderPlacementStartWorld.X) > 3f
                    || MathF.Abs(_builderPlacementCurrentWorld.Y - _builderPlacementStartWorld.Y) > 3f))
            {
                _builderPlacementScaleLock = false;
            }
        }

        if (_builderEraseDragging)
        {
            _builderEraseCurrentWorld = SnapGarrisonBuilderPoint(
                _builderUseModernUi
                    ? BuilderScreenToWorld(mouse.Position)
                    : mouse.Position.ToVector2() + _builderCamera);
        }
    }

    private void CommitGarrisonBuilderPlacement(Vector2 worldPosition)
    {
        if (!_builderPlacementDragging || string.IsNullOrWhiteSpace(_builderSelectedEntityType))
        {
            _builderPlacementDragging = false;
            return;
        }

        var currentWorld = SnapGarrisonBuilderPoint(worldPosition);
        ApplyGarrisonBuilderPlacementAlignmentSnap(ref currentWorld);
        _builderPlacementCurrentWorld = currentWorld;
        var placements = BuildGarrisonBuilderPlacementEntities();
        if (placements.Count == 0)
        {
            _builderPlacementDragging = false;
            return;
        }

        _builderEntities.AddRange(placements);
        UpdateGarrisonBuilderDocumentEntities();
        _builderDirty = true;
        _builderStatus = placements.Count == 1
            ? $"placed {_builderSelectedEntityType}"
            : $"placed {placements.Count} entities";
        _builderPlacementDragging = false;
    }

    private List<CustomMapBuilderEntity> BuildGarrisonBuilderPlacementEntities()
    {
        var entities = new List<CustomMapBuilderEntity>();
        var placementStartWorld = _builderPlacementDragging
            ? _builderPlacementStartWorld
            : _builderPlacementPreviewWorld;
        var placementCurrentWorld = _builderPlacementDragging
            ? _builderPlacementCurrentWorld
            : _builderPlacementPreviewWorld;
        if (!CustomMapBuilderEntityCatalog.TryGetDefinition(_builderSelectedEntityType, out var definition))
        {
            var entity = CustomMapBuilderEntity.Create(_builderSelectedEntityType, placementStartWorld.X, placementStartWorld.Y).NormalizeForEditing();
            entities.Add(entity);
            if (_builderSymmetry)
            {
                entities.Add(CustomMapBuilderEntity.Create(_builderSelectedEntityType, MirrorGarrisonBuilderWorldX(entity.X), entity.Y).NormalizeForEditing());
            }

            return entities;
        }

        var metrics = GetGarrisonBuilderEntityMetrics(definition);
        if (_builderScaleMode && IsGarrisonBuilderDefinitionScalable(definition) && !_builderPlacementScaleLock)
        {
            var right = MathF.Max(placementStartWorld.X + 6f, placementCurrentWorld.X + metrics.CenterX);
            var bottom = MathF.Max(placementStartWorld.Y + 6f, placementCurrentWorld.Y + metrics.CenterY);
            var xScale = MathF.Max(0.01f, (right - placementStartWorld.X) / metrics.Width);
            var yScale = MathF.Max(0.01f, (bottom - placementStartWorld.Y) / metrics.Height);
            var entityX = placementStartWorld.X - metrics.CenterX + (metrics.OffsetX * xScale);
            var entityY = placementStartWorld.Y - metrics.CenterY + (metrics.OffsetY * yScale);
            AddGarrisonBuilderPlacementWithSymmetry(entities, definition, metrics, entityX, entityY, xScale, yScale);
            return entities;
        }

        var endX = placementStartWorld.X + MathF.Max(metrics.Width, MathF.Ceiling((placementCurrentWorld.X - placementStartWorld.X) / metrics.Width) * metrics.Width);
        var endY = placementStartWorld.Y + MathF.Max(metrics.Height, MathF.Ceiling((placementCurrentWorld.Y - placementStartWorld.Y) / metrics.Height) * metrics.Height);
        for (var x = placementStartWorld.X - metrics.CenterX; x + metrics.CenterX < endX; x += metrics.Width)
        {
            for (var y = placementStartWorld.Y - metrics.CenterY; y + metrics.CenterY < endY; y += metrics.Height)
            {
                AddGarrisonBuilderPlacementWithSymmetry(entities, definition, metrics, x + metrics.OffsetX, y + metrics.OffsetY, 1f, 1f);
            }
        }

        return entities;
    }

    private void AddGarrisonBuilderPlacementWithSymmetry(
        List<CustomMapBuilderEntity> entities,
        CustomMapBuilderEntityDefinition definition,
        GarrisonBuilderEntityMetrics metrics,
        float x,
        float y,
        float xScale,
        float yScale)
    {
        AddGarrisonBuilderPlacementEntity(entities, definition, x, y, xScale, yScale, mirrored: false);
        if (!_builderSymmetry || !TryGetGarrisonBuilderMirroredDefinition(definition, out var mirroredDefinition))
        {
            return;
        }

        AddGarrisonBuilderPlacementEntity(
            entities,
            mirroredDefinition,
            MirrorGarrisonBuilderPlacementX(x, metrics),
            y,
            xScale,
            yScale,
            mirrored: true);
    }

    private float MirrorGarrisonBuilderPlacementX(float entityX, in GarrisonBuilderEntityMetrics metrics)
    {
        var gridX = entityX - metrics.OffsetX;
        var centerX = GetGarrisonBuilderMapSymmetryCenterX();
        return (2f * centerX) - gridX + metrics.MirroredOffsetX;
    }

    private float MirrorGarrisonBuilderWorldX(float worldX)
    {
        var centerX = GetGarrisonBuilderMapSymmetryCenterX();
        return (2f * centerX) - worldX;
    }

    private float GetGarrisonBuilderVisualWorldWidth()
    {
        var backgroundTexture = _builderBackgroundTexture ?? _builderDefaultBackgroundTexture;
        if (backgroundTexture is not null)
        {
            return backgroundTexture.Width * _builderDocument.VisualScale;
        }

        var walkmaskTexture = ResolveGarrisonBuilderWalkmaskTexture();
        if (walkmaskTexture is not null)
        {
            return walkmaskTexture.Width * _builderDocument.Scale;
        }

        return Math.Max(1f, _builderEntities.Count == 0 ? ViewportWidth : _builderEntities.Max(static entity => entity.X) + 200f);
    }

    private float GetGarrisonBuilderVisualWorldHeight()
    {
        var backgroundTexture = _builderBackgroundTexture ?? _builderDefaultBackgroundTexture;
        if (backgroundTexture is not null)
        {
            return backgroundTexture.Height * _builderDocument.VisualScale;
        }

        var walkmaskTexture = ResolveGarrisonBuilderWalkmaskTexture();
        if (walkmaskTexture is not null)
        {
            return walkmaskTexture.Height * _builderDocument.Scale;
        }

        return Math.Max(1f, _builderEntities.Count == 0 ? ViewportHeight : _builderEntities.Max(static entity => entity.Y) + 200f);
    }

    private float GetGarrisonBuilderMapSymmetryCenterX()
    {
        return GetGarrisonBuilderVisualWorldWidth() * 0.5f;
    }

    private float GetGarrisonBuilderMapSymmetryCenterY()
    {
        return GetGarrisonBuilderVisualWorldHeight() * 0.5f;
    }

    private float GetGarrisonBuilderMapSymmetryWidth()
    {
        return GetGarrisonBuilderVisualWorldWidth();
    }

    private float GetGarrisonBuilderMapSymmetryHeight()
    {
        return GetGarrisonBuilderVisualWorldHeight();
    }

    private void AddGarrisonBuilderPlacementEntity(
        List<CustomMapBuilderEntity> entities,
        CustomMapBuilderEntityDefinition definition,
        float x,
        float y,
        float xScale,
        float yScale,
        bool mirrored)
    {
        var properties = new Dictionary<string, string>(definition.DefaultProperties, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in _builderPlacementPropertyOverrides)
        {
            if (string.IsNullOrWhiteSpace(pair.Value))
            {
                properties.Remove(pair.Key);
            }
            else
            {
                properties[pair.Key] = pair.Value;
            }
        }

        if (mirrored)
        {
            ApplyGarrisonBuilderMirroredPlacementProperties(definition.Type, properties);
        }

        if (CustomMapCustomSpriteMetadata.IsCustomSpriteEntityType(definition.Type))
        {
            CustomMapCustomSpriteMetadata.EnsurePlacementDefaults(properties, _builderDocument.VisualScale);
        }

        var normalized = CustomMapBuilderEntity.Create(definition.Type, x, y, properties, xScale, yScale).NormalizeForEditing();
        entities.Add(normalized);
    }

    private void BeginGarrisonBuilderErase(Vector2 worldPosition)
    {
        _builderEraseDragging = true;
        _builderEraseStartWorld = SnapGarrisonBuilderPoint(worldPosition);
        _builderEraseCurrentWorld = _builderEraseStartWorld;
        _builderSelectedEntityType = string.Empty;
    }

    private void CommitGarrisonBuilderErase(Vector2 worldPosition)
    {
        if (!_builderEraseDragging)
        {
            return;
        }

        _builderEraseCurrentWorld = SnapGarrisonBuilderPoint(worldPosition);
        var eraseRect = CreateWorldRectangle(_builderEraseStartWorld, _builderEraseCurrentWorld);
        if (eraseRect.Width <= 0f || eraseRect.Height <= 0f)
        {
            _builderEraseDragging = false;
            _builderStatus = "no entities in erase area";
            return;
        }

        RecordGarrisonBuilderHistory();
        var removed = RemoveGarrisonBuilderEntitiesInRectangle(eraseRect);
        if (_builderSymmetry)
        {
            var width = GetGarrisonBuilderMapSymmetryWidth();
            var mirroredRect = new RectangleF(width - eraseRect.Right, eraseRect.Y, eraseRect.Width, eraseRect.Height);
            removed += RemoveGarrisonBuilderEntitiesInRectangle(mirroredRect);
        }

        _builderEraseDragging = false;
        SanitizeGarrisonBuilderSelectionAfterEntityChanges();
        if (removed > 0)
        {
            UpdateGarrisonBuilderDocumentEntities();
            _builderDirty = true;
            _builderStatus = $"removed {removed} entities";
        }
        else
        {
            TryUndoGarrisonBuilder();
            _builderStatus = "no entities in erase area";
        }
    }

    private int RemoveGarrisonBuilderEntitiesInRectangle(RectangleF rectangle)
    {
        var removed = 0;
        for (var index = _builderEntities.Count - 1; index >= 0; index -= 1)
        {
            var entity = _builderEntities[index];
            if (IsGarrisonBuilderEntityHidden(index)
                || !DoesGarrisonBuilderEntityIntersectRectangle(entity, rectangle))
            {
                continue;
            }

            NotifyGarrisonBuilderEntityRemoved(index);
            _builderEntities.RemoveAt(index);
            removed += 1;
        }

        return removed;
    }

    private bool DoesGarrisonBuilderEntityIntersectRectangle(CustomMapBuilderEntity entity, RectangleF rectangle)
    {
        if (rectangle.Contains(entity.X, entity.Y))
        {
            return true;
        }

        if (!TryGetGarrisonBuilderEntityWorldBounds(entity, out var left, out var top, out var width, out var height))
        {
            return false;
        }

        var entityRect = new RectangleF(left, top, width, height);
        return rectangle.X < entityRect.Right
            && rectangle.Right > entityRect.X
            && rectangle.Y < entityRect.Bottom
            && rectangle.Bottom > entityRect.Y;
    }

    private void AdjustGarrisonBuilderSelectedEntityIndexAfterRemoval(int removedIndex)
    {
        if (_builderSelectedEntityIndex < 0)
        {
            return;
        }

        if (_builderSelectedEntityIndex == removedIndex)
        {
            _builderSelectedEntityIndex = -1;
            return;
        }

        if (_builderSelectedEntityIndex > removedIndex)
        {
            _builderSelectedEntityIndex -= 1;
        }
    }

    private void NotifyGarrisonBuilderEntityRemoved(int removedIndex)
    {
        AdjustGarrisonBuilderMapEntitySelectionAfterRemoval(removedIndex);
        AdjustGarrisonBuilderHiddenEntityIndicesAfterRemoval(removedIndex);
        AdjustGarrisonBuilderEntityOverlapPickerIndicesAfterRemoval(removedIndex);
    }

    private bool IsValidGarrisonBuilderMapEntityIndex(int entityIndex)
    {
        return entityIndex >= 0
            && entityIndex < _builderEntities.Count
            && !IsGarrisonBuilderEntityHidden(entityIndex);
    }

    private bool IsGarrisonBuilderEntityHidden(int entityIndex)
    {
        return entityIndex >= 0 && _builderHiddenEntityIndices.Contains(entityIndex);
    }

    private bool HasGarrisonBuilderHiddenEntities() => _builderHiddenEntityIndices.Count > 0;

    private void ClearGarrisonBuilderHiddenEntities()
    {
        _builderHiddenEntityIndices.Clear();
    }

    private void AdjustGarrisonBuilderHiddenEntityIndicesAfterRemoval(int removedIndex)
    {
        if (_builderHiddenEntityIndices.Count == 0)
        {
            return;
        }

        var adjusted = new HashSet<int>();
        foreach (var index in _builderHiddenEntityIndices)
        {
            if (index < removedIndex)
            {
                adjusted.Add(index);
            }
            else if (index > removedIndex)
            {
                adjusted.Add(index - 1);
            }
        }

        _builderHiddenEntityIndices.Clear();
        foreach (var index in adjusted)
        {
            _builderHiddenEntityIndices.Add(index);
        }
    }

    private void HideGarrisonBuilderSelectedEntity()
    {
        if (_builderSelectedEntityIndex < 0 || _builderSelectedEntityIndex >= _builderEntities.Count)
        {
            _builderStatus = "select an entity to hide";
            return;
        }

        if (IsGarrisonBuilderEntityHidden(_builderSelectedEntityIndex))
        {
            _builderStatus = "entity already hidden";
            return;
        }

        _builderHiddenEntityIndices.Add(_builderSelectedEntityIndex);
        _builderSelectedEntityIndices.Remove(_builderSelectedEntityIndex);
        _builderSelectedEntityIndex = _builderSelectedEntityIndices.Count > 0
            ? _builderSelectedEntityIndices.Min()
            : -1;
        _builderEntityDragging = false;
        _builderMultiDragSnapshots.Clear();
        _builderActiveResizeHandle = GarrisonBuilderResizeHandle.None;
        CloseGarrisonBuilderPropertyEditor(applyChanges: false);
        CloseGarrisonBuilderEntityContextMenu();
        CloseGarrisonBuilderEntityOverlapPicker();
        _builderMapHoverEntityIndex = -1;
        _builderStatus = "entity hidden (H)";
    }

    private void UnhideAllGarrisonBuilderEntities()
    {
        if (_builderHiddenEntityIndices.Count == 0)
        {
            _builderStatus = "no hidden entities";
            return;
        }

        _builderHiddenEntityIndices.Clear();
        _builderStatus = "all entities unhidden";
    }

    private void SanitizeGarrisonBuilderSelectionAfterEntityChanges()
    {
        _builderSelectedEntityIndices.RemoveWhere(index => index < 0 || index >= _builderEntities.Count);
        if (_builderSelectedEntityIndex >= 0
            && (_builderSelectedEntityIndex >= _builderEntities.Count
                || !_builderSelectedEntityIndices.Contains(_builderSelectedEntityIndex)))
        {
            _builderSelectedEntityIndex = _builderSelectedEntityIndices.Count > 0
                ? _builderSelectedEntityIndices.Min()
                : -1;
        }

        if (_builderSelectedEntityIndex < 0 && _builderSelectedEntityIndices.Count == 0)
        {
            _builderEntityDragging = false;
            _builderAreaSelectDragging = false;
            _builderMultiDragSnapshots.Clear();
            _builderActiveResizeHandle = GarrisonBuilderResizeHandle.None;
            CloseGarrisonBuilderPropertyEditor(applyChanges: false);
            CloseGarrisonBuilderEntityContextMenu();
        }
    }

    private bool ShouldDrawGarrisonBuilderPlacementPreview()
    {
        if (string.IsNullOrWhiteSpace(_builderSelectedEntityType))
        {
            return false;
        }

        if (_builderPlacementDragging)
        {
            return true;
        }

        return _builderUseModernUi
            && _builderActiveTool == GarrisonBuilderTool.Place;
    }

    private void DrawGarrisonBuilderPlacementPreview()
    {
        if (!ShouldDrawGarrisonBuilderPlacementPreview())
        {
            return;
        }

        if (!CustomMapBuilderEntityCatalog.TryGetDefinition(_builderSelectedEntityType, out var definition))
        {
            return;
        }

        const float placementPreviewAlpha = 0.5f;
        var previewTint = Color.White * placementPreviewAlpha;
        var primaryOutlineColor = new Color(60, 220, 90) * placementPreviewAlpha;
        var mirroredOutlineColor = new Color(235, 80, 80) * placementPreviewAlpha;
        foreach (var entity in BuildGarrisonBuilderPlacementEntities())
        {
            var previewDefinition = CustomMapBuilderEntityCatalog.TryGetDefinition(entity.Type, out var entityDefinition)
                ? entityDefinition
                : definition;
            if (TryDrawGarrisonBuilderEntitySprite(previewDefinition, entity, previewTint))
            {
                continue;
            }

            var metrics = GetGarrisonBuilderEntityMetrics(previewDefinition);
            var screen = _builderUseModernUi
                ? BuilderWorldToScreen(new Vector2(entity.X, entity.Y))
                : new Vector2(entity.X - _builderCamera.X, entity.Y - _builderCamera.Y);
            var rect = new Rectangle(
                (int)MathF.Round(screen.X),
                (int)MathF.Round(screen.Y),
                Math.Max(6, (int)MathF.Round(metrics.Width * entity.XScale * (_builderUseModernUi ? GetGarrisonBuilderMapVisualScale() : 1f))),
                Math.Max(6, (int)MathF.Round(metrics.Height * entity.YScale * (_builderUseModernUi ? GetGarrisonBuilderMapVisualScale() : 1f))));
            DrawGarrisonBuilderRectangleOutline(
                rect,
                entity.Type.Equals(_builderSelectedEntityType, StringComparison.OrdinalIgnoreCase)
                    ? primaryOutlineColor
                    : mirroredOutlineColor);
        }
    }

    private void DrawGarrisonBuilderErasePreview()
    {
        if (!_builderEraseDragging)
        {
            return;
        }

        var rect = CreateWorldRectangle(_builderEraseStartWorld, _builderEraseCurrentWorld);
        DrawGarrisonBuilderRectangleOutline(ToScreenRectangle(rect), new Color(60, 220, 90, 180));
        _spriteBatch.Draw(_pixel, ToScreenRectangle(rect), new Color(60, 220, 90, 45));
        if (_builderSymmetry)
        {
            var mirrored = new RectangleF(GetGarrisonBuilderMapSymmetryWidth() - rect.Right, rect.Y, rect.Width, rect.Height);
            DrawGarrisonBuilderRectangleOutline(ToScreenRectangle(mirrored), new Color(235, 80, 80, 160));
            _spriteBatch.Draw(_pixel, ToScreenRectangle(mirrored), new Color(235, 80, 80, 35));
        }
    }

    private void DrawGarrisonBuilderRectangleOutline(Rectangle rectangle, Color color)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, 1), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Bottom, rectangle.Width, 1), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, 1, rectangle.Height), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.Right, rectangle.Y, 1, rectangle.Height), color);
    }

    private Rectangle ToScreenRectangle(RectangleF rectangle)
    {
        Rectangle screen;
        if (_builderUseModernUi)
        {
            var topLeft = BuilderWorldToScreen(new Vector2(rectangle.X, rectangle.Y));
            var bottomRight = BuilderWorldToScreen(new Vector2(rectangle.Right, rectangle.Bottom));
            var left = (int)MathF.Round(MathF.Min(topLeft.X, bottomRight.X));
            var top = (int)MathF.Round(MathF.Min(topLeft.Y, bottomRight.Y));
            var right = (int)MathF.Round(MathF.Max(topLeft.X, bottomRight.X));
            var bottom = (int)MathF.Round(MathF.Max(topLeft.Y, bottomRight.Y));
            screen = new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
            var viewport = GetModernGarrisonBuilderMapViewport();
            return Rectangle.Intersect(screen, viewport);
        }

        screen = new Rectangle(
            (int)MathF.Round(rectangle.X - _builderCamera.X),
            (int)MathF.Round(rectangle.Y - _builderCamera.Y),
            Math.Max(1, (int)MathF.Round(rectangle.Width)),
            Math.Max(1, (int)MathF.Round(rectangle.Height)));
        return screen;
    }

    private static RectangleF CreateWorldRectangle(Vector2 a, Vector2 b)
    {
        var left = MathF.Min(a.X, b.X);
        var top = MathF.Min(a.Y, b.Y);
        var right = MathF.Max(a.X, b.X);
        var bottom = MathF.Max(a.Y, b.Y);
        return new RectangleF(left, top, MathF.Max(1f, right - left), MathF.Max(1f, bottom - top));
    }

    private const float GarrisonBuilderGridSnapSize = 6f;
    private const float GarrisonBuilderTransientStatusDurationSeconds = 2f;

    private Vector2 SnapGarrisonBuilderPoint(Vector2 worldPosition)
    {
        if (!_builderGridAlign)
        {
            return worldPosition;
        }

        return new Vector2(
            SnapGarrisonBuilderCoordinate(worldPosition.X),
            SnapGarrisonBuilderCoordinate(worldPosition.Y));
    }

    private static float SnapGarrisonBuilderCoordinate(float value)
    {
        return MathF.Round(value / GarrisonBuilderGridSnapSize) * GarrisonBuilderGridSnapSize;
    }

    private const float GarrisonBuilderPlacementAlignmentSnapThreshold = 6f;

    private void ClearGarrisonBuilderPlacementAlignmentSnap()
    {
        _builderPlacementSnapXEntityIndex = -1;
        _builderPlacementSnapYEntityIndex = -1;
    }

    private void ApplyGarrisonBuilderPlacementAlignmentSnap(ref Vector2 world)
    {
        ClearGarrisonBuilderPlacementAlignmentSnap();
        if (!_builderUseModernUi || !_builderCtrlHeld)
        {
            return;
        }

        var bestXDistance = GarrisonBuilderPlacementAlignmentSnapThreshold;
        var bestYDistance = GarrisonBuilderPlacementAlignmentSnapThreshold;
        for (var index = 0; index < _builderEntities.Count; index += 1)
        {
            var entity = _builderEntities[index];
            if (IsGarrisonBuilderEntityHidden(index))
            {
                continue;
            }

            var xDistance = MathF.Abs(entity.X - world.X);
            if (xDistance < bestXDistance)
            {
                bestXDistance = xDistance;
                world.X = entity.X;
                _builderPlacementSnapXEntityIndex = index;
            }

            var yDistance = MathF.Abs(entity.Y - world.Y);
            if (yDistance < bestYDistance)
            {
                bestYDistance = yDistance;
                world.Y = entity.Y;
                _builderPlacementSnapYEntityIndex = index;
            }
        }
    }

    private void DrawGarrisonBuilderPlacementAlignmentGuides()
    {
        if (!_builderUseModernUi
            || !_builderCtrlHeld
            || !ShouldDrawGarrisonBuilderPlacementPreview()
            || (_builderPlacementSnapXEntityIndex < 0 && _builderPlacementSnapYEntityIndex < 0))
        {
            return;
        }

        var placementWorld = _builderPlacementDragging
            ? _builderPlacementCurrentWorld
            : _builderPlacementPreviewWorld;
        var placementScreen = BuilderWorldToScreen(placementWorld);
        var guideColor = new Color(255, 220, 80, 210);

        if (_builderPlacementSnapXEntityIndex >= 0 && _builderPlacementSnapXEntityIndex < _builderEntities.Count)
        {
            var reference = _builderEntities[_builderPlacementSnapXEntityIndex];
            var referenceScreen = BuilderWorldToScreen(new Vector2(reference.X, reference.Y));
            var guideStart = new Vector2(referenceScreen.X, placementScreen.Y);
            DrawGarrisonBuilderDashedLine(guideStart, referenceScreen, guideColor);
        }

        if (_builderPlacementSnapYEntityIndex >= 0 && _builderPlacementSnapYEntityIndex < _builderEntities.Count)
        {
            var reference = _builderEntities[_builderPlacementSnapYEntityIndex];
            var referenceScreen = BuilderWorldToScreen(new Vector2(reference.X, reference.Y));
            var guideStart = new Vector2(placementScreen.X, referenceScreen.Y);
            DrawGarrisonBuilderDashedLine(guideStart, referenceScreen, guideColor);
        }
    }

    private float GetGarrisonBuilderMapWidth()
    {
        var worldWidth = GetGarrisonBuilderVisualWorldWidth();
        return _builderUseModernUi ? worldWidth : worldWidth * GetGarrisonBuilderEditorZoom();
    }

    private float GetGarrisonBuilderMapHeight()
    {
        var worldHeight = GetGarrisonBuilderVisualWorldHeight();
        return _builderUseModernUi ? worldHeight : worldHeight * GetGarrisonBuilderEditorZoom();
    }

    private void UpdateGarrisonBuilderEntityCoordinateMode()
    {
        _builderEntityCoordinatesAreWalkmaskPixels = false;
        var walkmaskTexture = ResolveGarrisonBuilderWalkmaskTexture();
        if (walkmaskTexture is null || _builderEntities.Count == 0)
        {
            return;
        }

        var maxX = _builderEntities.Max(static entity => entity.X);
        var maxY = _builderEntities.Max(static entity => entity.Y);
        var pixelWidth = walkmaskTexture.Width + 1f;
        var pixelHeight = walkmaskTexture.Height + 1f;
        if (maxX <= pixelWidth && maxY <= pixelHeight)
        {
            _builderEntityCoordinatesAreWalkmaskPixels = true;
        }
    }

    private void ClampGarrisonBuilderCamera()
    {
        if (_builderUseModernUi)
        {
            return;
        }

        var viewportWidth = BuilderViewportWidth;
        var viewportHeight = BuilderViewportHeight;
        var visibleWidth = viewportWidth;
        var visibleHeight = viewportHeight;
        var mapWidth = GetGarrisonBuilderMapWidth();
        var mapHeight = GetGarrisonBuilderMapHeight();
        var minX = Math.Min(0f, mapWidth - visibleWidth);
        var maxX = Math.Max(0f, mapWidth - visibleWidth);
        var minY = Math.Min(0f, mapHeight - visibleHeight);
        var maxY = Math.Max(0f, mapHeight - visibleHeight);
        _builderCamera.X = Math.Clamp(_builderCamera.X, minX, maxX);
        _builderCamera.Y = Math.Clamp(_builderCamera.Y, minY, maxY);
    }

    private void CenterGarrisonBuilderCameraOnMap()
    {
        var viewportWidth = _builderUseModernUi ? GetModernGarrisonBuilderMapViewport().Width : BuilderViewportWidth;
        var viewportHeight = _builderUseModernUi ? GetModernGarrisonBuilderMapViewport().Height : BuilderViewportHeight;
        var zoom = _builderUseModernUi ? _builderZoom : 1f;
        var visibleWidth = viewportWidth / zoom;
        var visibleHeight = viewportHeight / zoom;
        _builderCamera.X = (GetGarrisonBuilderMapWidth() - visibleWidth) * 0.5f;
        _builderCamera.Y = (GetGarrisonBuilderMapHeight() - visibleHeight) * 0.5f;
        ClampGarrisonBuilderCamera();
    }

    private void RequestGarrisonBuilderCameraCenter()
    {
        _builderPendingCameraCenter = true;
    }

    private void TryApplyPendingGarrisonBuilderCameraCenter()
    {
        if (!_builderPendingCameraCenter || !_builderUseModernUi)
        {
            return;
        }

        if (_builderBackgroundTexture is null && _builderWalkmaskTexture is null)
        {
            return;
        }

        CenterGarrisonBuilderCameraOnMap();
        _builderPendingCameraCenter = false;
    }

    private LoadedGameMakerSprite? GetGarrisonBuilderCatalogSprite(string spriteName)
    {
        if (_builderCatalogSpriteCache.TryGetValue(spriteName, out var cached))
        {
            return cached;
        }

        var sprite = GetResolvedSprite(spriteName);
        if (sprite is null && _runtimeAssets is not null)
        {
            sprite = _runtimeAssets.GetSprite(spriteName);
        }

        if (sprite is not null)
        {
            _builderCatalogSpriteCache[spriteName] = sprite;
        }

        return sprite;
    }

    private bool TryDrawGarrisonBuilderEntitySprite(CustomMapBuilderEntityDefinition definition, CustomMapBuilderEntity entity, Color tint)
    {
        if (TryDrawGarrisonBuilderCustomSpriteEntity(definition, entity))
        {
            return true;
        }

        if (TryDrawGarrisonBuilderLogicEntity(definition, entity))
        {
            return true;
        }

        if (definition.Type.Equals("directionalWall", StringComparison.OrdinalIgnoreCase))
        {
            DrawGarrisonBuilderDirectionalWallPattern(entity, tint);
            return true;
        }

        if (definition.Type.Equals("barrier", StringComparison.OrdinalIgnoreCase))
        {
            DrawGarrisonBuilderBarrierPattern(entity, tint);
            return true;
        }

        if (definition.Type.Equals(TeleportMetadata.TeleportEntityType, StringComparison.OrdinalIgnoreCase))
        {
            DrawGarrisonBuilderTeleportZonePattern(entity, tint);
            return true;
        }

        if (!TryGetGarrisonBuilderEntityFrame(definition, entity, out var frame, out var origin))
        {
            return false;
        }

        var screen = _builderUseModernUi
            ? BuilderWorldToScreen(new Vector2(entity.X, entity.Y))
            : new Vector2(entity.X - _builderCamera.X, entity.Y - _builderCamera.Y);
        var visualScale = _builderUseModernUi ? GetGarrisonBuilderMapVisualScale() : 1f;
        var spriteScale = new Vector2(entity.XScale * visualScale, entity.YScale * visualScale);
        DrawLoadedSpriteFrame(
            frame,
            screen,
            null,
            GetGarrisonBuilderEntityDrawTint(definition, entity, tint),
            0f,
            origin,
            spriteScale,
            SpriteEffects.None,
            0f);
        return true;
    }

    private static Color GetGarrisonBuilderEntityDrawTint(
        CustomMapBuilderEntityDefinition definition,
        CustomMapBuilderEntity entity,
        Color baseTint)
    {
        if (definition.Type.Equals("barrier", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveGarrisonBuilderBarrierTint(BarrierConfiguration.FromProperties(entity.Properties).Targets, baseTint);
        }

        if (definition.Type.Equals("directionalWall", StringComparison.OrdinalIgnoreCase))
        {
            return Color.Lerp(new Color(72, 110, 82, 220), baseTint, 0.35f);
        }

        if (definition.Type.Equals("spawn", StringComparison.OrdinalIgnoreCase)
            && GetEntityProperty(entity.Properties, "team", "red").Equals("neutral", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveGarrisonBuilderNeutralTint(baseTint);
        }

        if (definition.Type.Equals(TeleportMetadata.TeleportExitEntityType, StringComparison.OrdinalIgnoreCase))
        {
            return ResolveGarrisonBuilderTeleportTint(baseTint);
        }

        return baseTint;
    }

    private GarrisonBuilderEntityMetrics GetGarrisonBuilderEntityMetrics(CustomMapBuilderEntityDefinition definition)
    {
        return GetGarrisonBuilderEntityMetrics(definition, _builderPlacementPropertyOverrides, 1f, 1f);
    }

    private GarrisonBuilderEntityMetrics GetGarrisonBuilderEntityMetrics(
        CustomMapBuilderEntityDefinition definition,
        IReadOnlyDictionary<string, string> properties,
        float xScale,
        float yScale)
    {
        if (TryGetGarrisonBuilderAnchorSizedEntityMetrics(definition.Type, properties, xScale, yScale, out var sizedMetrics))
        {
            return sizedMetrics;
        }

        if (TryGetGarrisonBuilderEntityFrame(definition, properties, out var frame, out var origin))
        {
            var width = Math.Max(1f, frame.Width);
            var height = Math.Max(1f, frame.Height);
            var usesTopLeftAnchor = UsesTopLeftBuilderAnchor(definition.Type);
            return new GarrisonBuilderEntityMetrics(
                width,
                height,
                usesTopLeftAnchor ? 0f : MathF.Round(width / 12f) * 6f,
                usesTopLeftAnchor ? 0f : MathF.Round(height / 12f) * 6f,
                origin.X,
                origin.Y,
                origin.X - width);
        }

        var (fallbackWidth, fallbackHeight) = GetGarrisonBuilderEntityBaseSize(definition.Type);
        var fallbackTopLeft = UsesTopLeftBuilderAnchor(definition.Type);
        return new GarrisonBuilderEntityMetrics(
            fallbackWidth,
            fallbackHeight,
            fallbackTopLeft ? 0f : MathF.Round(fallbackWidth / 12f) * 6f,
            fallbackTopLeft ? 0f : MathF.Round(fallbackHeight / 12f) * 6f,
            0f,
            0f,
            -fallbackWidth);
    }

    private bool TryGetGarrisonBuilderEntityFrame(CustomMapBuilderEntityDefinition definition, CustomMapBuilderEntity entity, out LoadedSpriteFrame frame, out Vector2 origin)
    {
        if (TryGetGarrisonBuilderEntityResourceFrame(definition, entity.Properties, out frame, out origin))
        {
            return true;
        }

        if (ControlPointOwnershipResolver.IsControlPointEntity(definition.Type)
            && TryResolveGarrisonBuilderControlPointFrame(entity, out frame, out origin))
        {
            return true;
        }

        return TryGetGarrisonBuilderEntityFrame(definition, entity.Properties, out frame, out origin);
    }

    private bool TryResolveGarrisonBuilderControlPointFrame(CustomMapBuilderEntity entity, out LoadedSpriteFrame frame, out Vector2 origin)
    {
        frame = default!;
        origin = Vector2.Zero;
        var spriteName = ControlPointOwnershipResolver.ResolveBuilderControlPointSpriteName(
            entity,
            _builderSelectedGameMode,
            _builderEntities,
            HasGarrisonBuilderControlPointSetupGates(),
            IsGarrisonBuilderControlPointOverrideEnabled());
        var sprite = GetGarrisonBuilderCatalogSprite(spriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return false;
        }

        var frameIndex = Math.Clamp(
            ControlPointOwnershipResolver.ResolveBuilderControlPointSpriteFrame(entity, spriteName),
            0,
            sprite.Frames.Count - 1);
        frame = sprite.Frames[frameIndex];
        origin = sprite.Origin.ToVector2();
        return true;
    }

    private bool HasGarrisonBuilderControlPointSetupGates()
    {
        for (var index = 0; index < _builderEntities.Count; index += 1)
        {
            if (_builderEntities[index].Type.Equals("SetupGate", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetGarrisonBuilderEntityFrame(CustomMapBuilderEntityDefinition definition, out LoadedSpriteFrame frame, out Vector2 origin)
    {
        return TryGetGarrisonBuilderEntityFrame(definition, _builderPlacementPropertyOverrides, out frame, out origin);
    }

    private bool TryGetGarrisonBuilderEntityFrame(
        CustomMapBuilderEntityDefinition definition,
        IReadOnlyDictionary<string, string> properties,
        out LoadedSpriteFrame frame,
        out Vector2 origin)
    {
        if (TryGetGarrisonBuilderEntityResourceFrame(definition, properties, out frame, out origin))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(definition.EntitySpriteName))
        {
            var spriteName = definition.EntitySpriteName;

            var frameIndex = Math.Clamp(definition.EntityImage, 0, 5);
            if (definition.Type.Equals(TeleportMetadata.TeleportExitEntityType, StringComparison.OrdinalIgnoreCase))
            {
                frameIndex = 0;
            }
            else if (spriteName.Equals("spawnS", StringComparison.OrdinalIgnoreCase))
            {
                frameIndex = ResolveGarrisonBuilderSpawnSpriteFrame(properties);
            }

            var sprite = GetGarrisonBuilderCatalogSprite(spriteName);
            if (sprite is not null && sprite.Frames.Count > 0)
            {
                frame = sprite.Frames[frameIndex];
                origin = sprite.Origin.ToVector2();
                if (UsesTopLeftBuilderAnchor(definition.Type))
                {
                    origin = Vector2.Zero;
                }

                return true;
            }
        }

        frame = default!;
        origin = Vector2.Zero;
        return false;
    }

    private static bool UsesGarrisonBuilderCenterPlacementAnchor(string type)
    {
        return type.Equals(AreaExtensionMetadata.AreaEntityType, StringComparison.OrdinalIgnoreCase)
            || type.Equals(PlayerTriggerMetadata.PlayerTriggerEntityType, StringComparison.OrdinalIgnoreCase)
            || type.Equals(DamageableMetadata.DamageableEntityType, StringComparison.OrdinalIgnoreCase);
    }

    private static bool UsesTopLeftBuilderAnchor(string type)
    {
        return type.Equals("GeneratorRed", StringComparison.OrdinalIgnoreCase)
            || type.Equals("GeneratorBlue", StringComparison.OrdinalIgnoreCase)
            || type.Equals(TeleportMetadata.TeleportExitEntityType, StringComparison.OrdinalIgnoreCase)
            || (IsGarrisonBuilderAnchorSizedEntityType(type)
                && !UsesGarrisonBuilderCenterPlacementAnchor(type));
    }

    private static bool IsGarrisonBuilderAnchorSizedEntityType(string type)
    {
        return type.Equals(TeleportMetadata.TeleportEntityType, StringComparison.OrdinalIgnoreCase)
            || type.Equals(PlayerTriggerMetadata.PlayerTriggerEntityType, StringComparison.OrdinalIgnoreCase)
            || type.Equals(DamageableMetadata.DamageableEntityType, StringComparison.OrdinalIgnoreCase)
            || type.Equals("barrier", StringComparison.OrdinalIgnoreCase)
            || type.Equals("directionalWall", StringComparison.OrdinalIgnoreCase)
            || type.Equals("redteamgate", StringComparison.OrdinalIgnoreCase)
            || type.Equals("blueteamgate", StringComparison.OrdinalIgnoreCase)
            || type.Equals("redintelgate", StringComparison.OrdinalIgnoreCase)
            || type.Equals("blueintelgate", StringComparison.OrdinalIgnoreCase)
            || type.Equals("intelgatevertical", StringComparison.OrdinalIgnoreCase)
            || type.Equals("playerwall", StringComparison.OrdinalIgnoreCase)
            || type.Equals("bulletwall", StringComparison.OrdinalIgnoreCase)
            || type.Equals("leftdoor", StringComparison.OrdinalIgnoreCase)
            || type.Equals("rightdoor", StringComparison.OrdinalIgnoreCase)
            || type.Equals("redteamgate2", StringComparison.OrdinalIgnoreCase)
            || type.Equals("blueteamgate2", StringComparison.OrdinalIgnoreCase)
            || type.Equals("redintelgate2", StringComparison.OrdinalIgnoreCase)
            || type.Equals("blueintelgate2", StringComparison.OrdinalIgnoreCase)
            || type.Equals("intelgatehorizontal", StringComparison.OrdinalIgnoreCase)
            || type.Equals("playerwall_horizontal", StringComparison.OrdinalIgnoreCase)
            || type.Equals("bulletwall_horizontal", StringComparison.OrdinalIgnoreCase)
            || type.Equals("dropdownplatform", StringComparison.OrdinalIgnoreCase)
            || type.Equals("setupgate", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetGarrisonBuilderAnchorSizedEntityMetrics(
        string type,
        IReadOnlyDictionary<string, string> properties,
        float xScale,
        float yScale,
        out GarrisonBuilderEntityMetrics metrics)
    {
        if (type.Equals(TeleportMetadata.TeleportEntityType, StringComparison.OrdinalIgnoreCase))
        {
            var (zoneWidth, zoneHeight) = TeleportMetadata.ResolveZoneDimensions(xScale, yScale);
            metrics = new GarrisonBuilderEntityMetrics(zoneWidth, zoneHeight, 0f, 0f, 0f, 0f, -zoneWidth);
            return true;
        }

        if (type.Equals(PlayerTriggerMetadata.PlayerTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            var (zoneWidth, zoneHeight) = PlayerTriggerMetadata.ResolveZoneDimensions(xScale, yScale);
            metrics = new GarrisonBuilderEntityMetrics(
                zoneWidth,
                zoneHeight,
                zoneWidth * 0.5f,
                zoneHeight * 0.5f,
                0f,
                0f,
                -zoneWidth * 0.5f);
            return true;
        }

        if (type.Equals(AreaExtensionMetadata.AreaEntityType, StringComparison.OrdinalIgnoreCase))
        {
            var (zoneWidth, zoneHeight) = AreaExtensionMetadata.ResolveZoneDimensions(xScale, yScale);
            metrics = new GarrisonBuilderEntityMetrics(
                zoneWidth,
                zoneHeight,
                zoneWidth * 0.5f,
                zoneHeight * 0.5f,
                0f,
                0f,
                -zoneWidth * 0.5f);
            return true;
        }

        if (type.Equals(DamageableMetadata.DamageableEntityType, StringComparison.OrdinalIgnoreCase))
        {
            var (zoneWidth, zoneHeight) = DamageableMetadata.ResolveZoneDimensions(xScale, yScale);
            metrics = new GarrisonBuilderEntityMetrics(
                zoneWidth,
                zoneHeight,
                zoneWidth * 0.5f,
                zoneHeight * 0.5f,
                0f,
                0f,
                -zoneWidth * 0.5f);
            return true;
        }

        if (!IsGarrisonBuilderAnchorSizedEntityType(type))
        {
            metrics = default;
            return false;
        }

        var floor = type.Equals("barrier", StringComparison.OrdinalIgnoreCase)
            && BarrierConfiguration.IsFloorOrientation(properties);
        var (width, height) = type.Equals("directionalWall", StringComparison.OrdinalIgnoreCase)
            ? BarrierConfiguration.ResolveDimensions(xScale, yScale)
            : BarrierConfiguration.ResolveDimensions(xScale, yScale, floor);
        metrics = new GarrisonBuilderEntityMetrics(width, height, 0f, 0f, 0f, 0f, -width);
        return true;
    }

    private static int ResolveGarrisonBuilderSpawnSpriteFrame(IReadOnlyDictionary<string, string> properties)
    {
        var team = GetEntityProperty(properties, "team", "red");
        var forward = GetEntityBool(properties, "forward", false);
        if (!forward)
        {
            return team.Equals("blue", StringComparison.OrdinalIgnoreCase) ? 5 : 0;
        }

        var priority = ForwardSpawnMetadata.ParsePriority(properties);
        var frameOffset = ForwardSpawnPriorityMetadata.ResolveBuilderSpriteFrameOffset(priority);
        return team.Equals("blue", StringComparison.OrdinalIgnoreCase)
            ? 5 + frameOffset
            : frameOffset;
    }

    private bool TryGetGarrisonBuilderEntityResourceFrame(CustomMapBuilderEntityDefinition definition, IReadOnlyDictionary<string, string> properties, out LoadedSpriteFrame frame, out Vector2 origin)
    {
        frame = default!;
        origin = Vector2.Zero;

        if (!properties.TryGetValue("resource", out var resourceName)
            || string.IsNullOrWhiteSpace(resourceName))
        {
            return false;
        }

        var texture = GetGarrisonBuilderResourceTexture(resourceName.Trim());
        if (texture is null)
        {
            return false;
        }

        frame = new LoadedSpriteFrame(texture, OwnsTexture: false);
        return true;
    }

    private static void GetGarrisonBuilderResizeMinimumExtents(string type, float baseWidth, float baseHeight, out float minWidth, out float minHeight)
    {
        GetGarrisonBuilderEntityMinimumWorldSize(type, baseWidth, baseHeight, out minWidth, out minHeight);
        if (type.Equals("barrier", StringComparison.OrdinalIgnoreCase)
            || type.Equals("directionalWall", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        minWidth = baseWidth * 0.25f;
        minHeight = baseHeight * 0.25f;
    }

    private static void GetGarrisonBuilderEntityMinimumWorldSize(string type, float baseWidth, float baseHeight, out float minWidth, out float minHeight)
    {
        if (type.Equals("barrier", StringComparison.OrdinalIgnoreCase)
            || type.Equals("directionalWall", StringComparison.OrdinalIgnoreCase))
        {
            minWidth = BarrierConfiguration.MinExtent;
            minHeight = BarrierConfiguration.MinExtent;
            return;
        }

        if (type.Equals(TeleportMetadata.TeleportEntityType, StringComparison.OrdinalIgnoreCase)
            || type.Equals(PlayerTriggerMetadata.PlayerTriggerEntityType, StringComparison.OrdinalIgnoreCase)
            || type.Equals(DamageableMetadata.DamageableEntityType, StringComparison.OrdinalIgnoreCase))
        {
            minWidth = PlayerTriggerMetadata.MinZoneExtent;
            minHeight = PlayerTriggerMetadata.MinZoneExtent;
            return;
        }

        minWidth = baseWidth * 0.25f;
        minHeight = baseHeight * 0.25f;
    }

    private void DrawGarrisonBuilderTeleportZonePattern(CustomMapBuilderEntity entity, Color tint)
    {
        if (!TryGetGarrisonBuilderEntityWorldBounds(entity, out var left, out var top, out var width, out var height))
        {
            return;
        }

        var topLeft = BuilderWorldToScreen(new Vector2(left, top));
        var bottomRight = BuilderWorldToScreen(new Vector2(left + width, top + height));
        var screenBounds = new Rectangle(
            (int)MathF.Floor(MathF.Min(topLeft.X, bottomRight.X)),
            (int)MathF.Floor(MathF.Min(topLeft.Y, bottomRight.Y)),
            Math.Max(1, (int)MathF.Ceiling(MathF.Abs(bottomRight.X - topLeft.X))),
            Math.Max(1, (int)MathF.Ceiling(MathF.Abs(bottomRight.Y - topLeft.Y))));
        var fillColor = Color.Lerp(new Color(48, 150, 145, 190), tint, 0.35f);
        var borderColor = Color.Lerp(new Color(24, 96, 92, 255), tint, 0.15f);
        _spriteBatch.Draw(_pixel, screenBounds, fillColor);
        DrawGarrisonBuilderRectangleOutline(screenBounds, borderColor);
    }

    private void DrawGarrisonBuilderBarrierPattern(CustomMapBuilderEntity entity, Color tint)
    {
        if (!TryGetGarrisonBuilderEntityWorldBounds(entity, out var left, out var top, out var width, out var height))
        {
            return;
        }

        var topLeft = BuilderWorldToScreen(new Vector2(left, top));
        var bottomRight = BuilderWorldToScreen(new Vector2(left + width, top + height));
        var screenBounds = new Rectangle(
            (int)MathF.Floor(MathF.Min(topLeft.X, bottomRight.X)),
            (int)MathF.Floor(MathF.Min(topLeft.Y, bottomRight.Y)),
            Math.Max(1, (int)MathF.Ceiling(MathF.Abs(bottomRight.X - topLeft.X))),
            Math.Max(1, (int)MathF.Ceiling(MathF.Abs(bottomRight.Y - topLeft.Y))));
        var fillColor = Color.Lerp(new Color(72, 110, 82, 200), tint, 0.35f);
        var borderColor = Color.Lerp(new Color(40, 64, 48, 255), tint, 0.15f);
        _spriteBatch.Draw(_pixel, screenBounds, fillColor);
        DrawGarrisonBuilderRectangleOutline(screenBounds, borderColor);
    }

    private void DrawGarrisonBuilderDirectionalWallPattern(CustomMapBuilderEntity entity, Color tint)
    {
        if (!TryGetGarrisonBuilderEntityWorldBounds(entity, out var left, out var top, out var width, out var height))
        {
            return;
        }

        var configuration = DirectionalWallConfiguration.FromProperties(entity.Properties);
        var topLeft = BuilderWorldToScreen(new Vector2(left, top));
        var bottomRight = BuilderWorldToScreen(new Vector2(left + width, top + height));
        var screenLeft = MathF.Min(topLeft.X, bottomRight.X);
        var screenTop = MathF.Min(topLeft.Y, bottomRight.Y);
        var screenRight = MathF.Max(topLeft.X, bottomRight.X);
        var screenBottom = MathF.Max(topLeft.Y, bottomRight.Y);
        var screenBounds = new Rectangle(
            (int)MathF.Floor(screenLeft),
            (int)MathF.Floor(screenTop),
            Math.Max(1, (int)MathF.Ceiling(screenRight - screenLeft)),
            Math.Max(1, (int)MathF.Ceiling(screenBottom - screenTop)));

        var fillColor = Color.Lerp(new Color(58, 92, 68, 190), tint, 0.25f);
        var arrowColor = Color.Lerp(new Color(130, 210, 145, 230), tint, 0.2f);
        var borderColor = Color.Lerp(new Color(40, 64, 48, 255), tint, 0.15f);
        _spriteBatch.Draw(_pixel, screenBounds, fillColor);
        DrawGarrisonBuilderRectangleOutline(screenBounds, borderColor);

        const float tileSize = 10f;
        var visualScale = _builderUseModernUi ? GetGarrisonBuilderMapVisualScale() : 1f;
        var worldTile = tileSize * visualScale;
        for (var cellY = top; cellY < top + height - 0.01f; cellY += tileSize)
        {
            for (var cellX = left; cellX < left + width - 0.01f; cellX += tileSize)
            {
                DrawGarrisonBuilderDirectionalWallArrowCell(
                    configuration.PassDirection,
                    cellX,
                    cellY,
                    MathF.Min(tileSize, left + width - cellX),
                    MathF.Min(tileSize, top + height - cellY),
                    arrowColor,
                    visualScale);
            }
        }
    }

    private void DrawGarrisonBuilderDirectionalWallArrowCell(
        DirectionalWallPassDirection passDirection,
        float worldX,
        float worldY,
        float cellWidth,
        float cellHeight,
        Color arrowColor,
        float visualScale)
    {
        var inset = MathF.Max(1f, MathF.Min(cellWidth, cellHeight) * 0.15f);
        var left = worldX + inset;
        var top = worldY + inset;
        var right = worldX + cellWidth - inset;
        var bottom = worldY + cellHeight - inset;
        var centerX = (left + right) * 0.5f;
        var centerY = (top + bottom) * 0.5f;

        switch (passDirection)
        {
            case DirectionalWallPassDirection.Right:
                DrawGarrisonBuilderFilledTriangle(
                    BuilderWorldToScreen(new Vector2(right, centerY)),
                    BuilderWorldToScreen(new Vector2(left, top)),
                    BuilderWorldToScreen(new Vector2(left, bottom)),
                    arrowColor);
                break;
            case DirectionalWallPassDirection.Left:
                DrawGarrisonBuilderFilledTriangle(
                    BuilderWorldToScreen(new Vector2(left, centerY)),
                    BuilderWorldToScreen(new Vector2(right, top)),
                    BuilderWorldToScreen(new Vector2(right, bottom)),
                    arrowColor);
                break;
            case DirectionalWallPassDirection.Up:
                DrawGarrisonBuilderFilledTriangle(
                    BuilderWorldToScreen(new Vector2(centerX, top)),
                    BuilderWorldToScreen(new Vector2(left, bottom)),
                    BuilderWorldToScreen(new Vector2(right, bottom)),
                    arrowColor);
                break;
            default:
                DrawGarrisonBuilderFilledTriangle(
                    BuilderWorldToScreen(new Vector2(centerX, bottom)),
                    BuilderWorldToScreen(new Vector2(left, top)),
                    BuilderWorldToScreen(new Vector2(right, top)),
                    arrowColor);
                break;
        }
    }

    private void DrawGarrisonBuilderFilledTriangle(Vector2 tipScreen, Vector2 baseAScreen, Vector2 baseBScreen, Color color)
    {
        var minY = MathF.Min(tipScreen.Y, MathF.Min(baseAScreen.Y, baseBScreen.Y));
        var maxY = MathF.Max(tipScreen.Y, MathF.Max(baseAScreen.Y, baseBScreen.Y));
        for (var y = (int)MathF.Floor(minY); y <= (int)MathF.Ceiling(maxY); y += 1)
        {
            var scanY = y + 0.5f;
            var intersections = new List<float>(3);
            AddGarrisonBuilderTriangleEdgeIntersection(tipScreen, baseAScreen, scanY, intersections);
            AddGarrisonBuilderTriangleEdgeIntersection(baseAScreen, baseBScreen, scanY, intersections);
            AddGarrisonBuilderTriangleEdgeIntersection(baseBScreen, tipScreen, scanY, intersections);
            if (intersections.Count < 2)
            {
                continue;
            }

            intersections.Sort();
            var spanLeft = (int)MathF.Floor(intersections[0]);
            var spanRight = (int)MathF.Ceiling(intersections[^1]);
            _spriteBatch.Draw(_pixel, new Rectangle(spanLeft, y, Math.Max(1, spanRight - spanLeft), 1), color);
        }
    }

    private static void AddGarrisonBuilderTriangleEdgeIntersection(Vector2 start, Vector2 end, float y, List<float> intersections)
    {
        if (MathF.Abs(start.Y - end.Y) <= 0.01f)
        {
            return;
        }

        if ((y < MathF.Min(start.Y, end.Y)) || (y > MathF.Max(start.Y, end.Y)))
        {
            return;
        }

        var t = (y - start.Y) / (end.Y - start.Y);
        intersections.Add(start.X + ((end.X - start.X) * t));
    }

    private static (float Width, float Height) GetGarrisonBuilderEntityBaseSize(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "barrier" or "directionalWall" or "redteamgate" or "blueteamgate" or "redintelgate" or "blueintelgate" or "intelgatevertical" or "playerwall" or "bulletwall" or "leftdoor" or "rightdoor" => (6f, 60f),
            "redteamgate2" or "blueteamgate2" or "redintelgate2" or "blueintelgate2" or "intelgatehorizontal" or "playerwall_horizontal" or "bulletwall_horizontal" or "dropdownplatform" or "setupgate" => (60f, 6f),
            "medcabinet" => (32f, 48f),
            _ => (42f, 42f),
        };
    }

    private static bool IsGarrisonBuilderDefinitionScalable(CustomMapBuilderEntityDefinition definition)
    {
        return definition.DefaultProperties.ContainsKey("xscale") && definition.DefaultProperties.ContainsKey("yscale");
    }

    private static void ApplyGarrisonBuilderMirroredPlacementProperties(string type, Dictionary<string, string> properties)
    {
        if (type.Equals("spawn", StringComparison.OrdinalIgnoreCase)
            && properties.TryGetValue("team", out var team)
            && !team.Equals("neutral", StringComparison.OrdinalIgnoreCase))
        {
            properties["team"] = team.Equals("red", StringComparison.OrdinalIgnoreCase) ? "blue" : "red";
            return;
        }

        if (type.Equals("barrier", StringComparison.OrdinalIgnoreCase)
            || type.Equals("directionalWall", StringComparison.OrdinalIgnoreCase))
        {
            MirrorGarrisonBuilderBarrierTargetProperties(properties);
        }

    }

    private static void MirrorGarrisonBuilderBarrierTargetProperties(Dictionary<string, string> properties)
    {
        MirrorGarrisonBuilderBarrierPropertyPair(properties, BarrierTargetFilterMetadata.RedPlayersPropertyKey, BarrierTargetFilterMetadata.BluePlayersPropertyKey);
        MirrorGarrisonBuilderBarrierPropertyPair(properties, BarrierTargetFilterMetadata.RedShotsPropertyKey, BarrierTargetFilterMetadata.BlueShotsPropertyKey);
        MirrorGarrisonBuilderBarrierPropertyPair(properties, BarrierTargetFilterMetadata.RedIntelPropertyKey, BarrierTargetFilterMetadata.BlueIntelPropertyKey);
    }

    private static void MirrorGarrisonBuilderBarrierPropertyPair(Dictionary<string, string> properties, string leftKey, string rightKey)
    {
        if (!properties.TryGetValue(leftKey, out var leftValue))
        {
            return;
        }

        properties.TryGetValue(rightKey, out var rightValue);
        properties[leftKey] = rightValue ?? leftValue;
        properties[rightKey] = leftValue;
    }

    private static Color ResolveGarrisonBuilderBarrierTint(in BarrierTargetFilters targets, Color baseTint)
    {
        return BarrierConfiguration.ResolveDisplayTeam(targets) switch
        {
            PlayerTeam.Red => new Color((byte)255, (byte)0, (byte)0, baseTint.A),
            PlayerTeam.Blue => new Color((byte)0, (byte)0, (byte)255, baseTint.A),
            _ => ResolveGarrisonBuilderNeutralTint(baseTint),
        };
    }

    private static Color ResolveGarrisonBuilderNeutralTint(Color baseTint)
    {
        return new Color((byte)0, (byte)200, (byte)0, baseTint.A);
    }

    private static Color ResolveGarrisonBuilderTeleportTint(Color baseTint)
    {
        return new Color((byte)32, (byte)190, (byte)180, baseTint.A);
    }

    private static bool TryGetGarrisonBuilderMirroredDefinition(CustomMapBuilderEntityDefinition definition, out CustomMapBuilderEntityDefinition mirroredDefinition)
    {
        var mirroredType = GetGarrisonBuilderMirroredEntityType(definition.Type);
        return CustomMapBuilderEntityCatalog.TryGetDefinition(mirroredType, out mirroredDefinition);
    }

    private static string GetGarrisonBuilderMirroredEntityType(string type)
    {
        return type switch
        {
            "spawn" => "spawn",
            "barrier" => "barrier",
            "directionalWall" => "directionalWall",
            "controlPoint" => "controlPoint",
            "redspawn" => "bluespawn",
            "bluespawn" => "redspawn",
            "redspawn1" => "bluespawn1",
            "bluespawn1" => "redspawn1",
            "redspawn2" => "bluespawn2",
            "bluespawn2" => "redspawn2",
            "redspawn3" => "bluespawn3",
            "bluespawn3" => "redspawn3",
            "redspawn4" => "bluespawn4",
            "bluespawn4" => "redspawn4",
            "redintel" => "blueintel",
            "blueintel" => "redintel",
            "redteamgate" => "blueteamgate",
            "blueteamgate" => "redteamgate",
            "redteamgate2" => "blueteamgate2",
            "blueteamgate2" => "redteamgate2",
            "redintelgate" => "blueintelgate",
            "blueintelgate" => "redintelgate",
            "redintelgate2" => "blueintelgate2",
            "blueintelgate2" => "redintelgate2",
            "GeneratorRed" => "GeneratorBlue",
            "GeneratorBlue" => "GeneratorRed",
            "KothRedControlPoint" => "KothBlueControlPoint",
            "KothBlueControlPoint" => "KothRedControlPoint",
            "leftdoor" => "rightdoor",
            "rightdoor" => "leftdoor",
            _ => type,
        };
    }

    private CustomMapBuilderEntity CreateGarrisonBuilderEntityFromDefinition(CustomMapBuilderEntityDefinition definition, float x, float y)
    {
        var properties = new Dictionary<string, string>(definition.DefaultProperties, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in _builderPlacementPropertyOverrides)
        {
            if (string.IsNullOrWhiteSpace(pair.Value))
            {
                properties.Remove(pair.Key);
            }
            else
            {
                properties[pair.Key] = pair.Value;
            }
        }

        return ApplyGarrisonBuilderLogicDefaults(
            CustomMapBuilderEntity.Create(definition.Type, x, y, properties).NormalizeForEditing());
    }

    private bool TryHandleGarrisonBuilderPathClick(Point position, Rectangle panel)
    {
        var x = panel.X + BuilderPanelPadding;
        var y = panel.Y + BuilderPanelPadding + 52 + BuilderButtonHeight + 14 + 22;
        var width = panel.Width - (BuilderPanelPadding * 2);
        foreach (var field in new[] { GarrisonBuilderPathField.OpenMap, GarrisonBuilderPathField.Background, GarrisonBuilderPathField.Walkmask, GarrisonBuilderPathField.Save })
        {
            const int labelWidth = 44;
            const int actionWidth = 58;
            var fieldBounds = new Rectangle(x + labelWidth, y, Math.Max(1, width - labelWidth - actionWidth - BuilderButtonGap), 24);
            var actionBounds = new Rectangle(fieldBounds.Right + BuilderButtonGap, y, actionWidth, 24);
            if (fieldBounds.Contains(position))
            {
                BeginEditingGarrisonBuilderPath(field);
                return true;
            }

            if (actionBounds.Contains(position))
            {
                ApplyGarrisonBuilderPathField(field);
                return true;
            }

            y += 32;
        }

        return false;
    }

    private bool UpdateGarrisonBuilderPathKeyboard(KeyboardState keyboard)
    {
        if (_builderActivePathField == GarrisonBuilderPathField.None)
        {
            return false;
        }

        if (IsKeyPressed(keyboard, Keys.Escape))
        {
            _builderActivePathField = GarrisonBuilderPathField.None;
            _builderStatus = "path edit canceled";
            return true;
        }

        if (IsKeyPressed(keyboard, Keys.Enter))
        {
            var field = _builderActivePathField;
            _builderActivePathField = GarrisonBuilderPathField.None;
            ApplyGarrisonBuilderPathField(field);
            return true;
        }

        if (IsKeyPressed(keyboard, Keys.Back))
        {
            var text = GetGarrisonBuilderPathFieldBuffer(_builderActivePathField);
            var result = DeleteTextSelectionOrBackspace(text, _builderPathCursorIndex, _builderPathSelectionStart);
            SetGarrisonBuilderPathFieldBuffer(_builderActivePathField, result.Text);
            _builderPathCursorIndex = result.CursorIndex;
            _builderPathSelectionStart = result.SelectionStart;
            return true;
        }

        var shiftHeld = IsShiftHeld(keyboard);
        if (IsKeyPressed(keyboard, Keys.Left))
        {
            var result = MoveTextCursorLeft(_builderPathCursorIndex, _builderPathSelectionStart, shiftHeld);
            _builderPathCursorIndex = result.CursorIndex;
            _builderPathSelectionStart = result.SelectionStart;
            return true;
        }

        if (IsKeyPressed(keyboard, Keys.Right))
        {
            var text = GetGarrisonBuilderPathFieldBuffer(_builderActivePathField);
            var result = MoveTextCursorRight(_builderPathCursorIndex, _builderPathSelectionStart, text, shiftHeld);
            _builderPathCursorIndex = result.CursorIndex;
            _builderPathSelectionStart = result.SelectionStart;
            return true;
        }

        return true;
    }

    private bool HandleGarrisonBuilderTextInput(char character)
    {
        if (HandleGarrisonBuilderPropertyTextInput(character))
        {
            return true;
        }

        if (HandleGarrisonBuilderLayerParallaxTextInput(character))
        {
            return true;
        }

        if (!_builderEditorEnabled || _builderActivePathField == GarrisonBuilderPathField.None)
        {
            return false;
        }

        var text = GetGarrisonBuilderPathFieldBuffer(_builderActivePathField);
        var result = InsertTextCharacterAtCursor(text, character, _builderPathCursorIndex, _builderPathSelectionStart, 260);
        SetGarrisonBuilderPathFieldBuffer(_builderActivePathField, result.Text);
        _builderPathCursorIndex = result.CursorIndex;
        _builderPathSelectionStart = result.SelectionStart;
        return true;
    }

    private void BeginEditingGarrisonBuilderPath(GarrisonBuilderPathField field)
    {
        if (OperatingSystem.IsWindows())
        {
            if (!TryApplyGarrisonBuilderNativePathDialog(field)
                && field is GarrisonBuilderPathField.ResourcePath or GarrisonBuilderPathField.ResourceName)
            {
                ClearGarrisonBuilderPendingResourceImport();
            }

            return;
        }

        _builderActivePathField = field;
        var text = GetGarrisonBuilderPathFieldBuffer(field);
        _builderPathCursorIndex = text.Length;
        _builderPathSelectionStart = _builderPathCursorIndex;
        _builderStatus = field switch
        {
            GarrisonBuilderPathField.OpenMap => "enter a PNG or JSON map path to open",
            GarrisonBuilderPathField.Background => "enter a background PNG path",
            GarrisonBuilderPathField.Walkmask => "enter a walkmask PNG path",
            GarrisonBuilderPathField.Save => "enter an output PNG or JSON package path",
            GarrisonBuilderPathField.ResourceName => "enter a resource name",
            GarrisonBuilderPathField.ResourcePath => "enter a PNG/GIF resource path",
            GarrisonBuilderPathField.ResourceOutputDirectory => "enter a directory for decompiled resources",
            _ => _builderStatus,
        };
    }

    private void ClearGarrisonBuilderPendingResourceImport()
    {
        _builderPendingResourceName = string.Empty;
        _builderPendingResourceKind = CustomMapBuilderResourceKind.GenericImage;
        _builderResourcePathBuffer = string.Empty;
    }

    private void ApplyGarrisonBuilderPathField(GarrisonBuilderPathField field)
    {
        _builderActivePathField = GarrisonBuilderPathField.None;
        switch (field)
        {
            case GarrisonBuilderPathField.OpenMap:
                OpenGarrisonBuilderMap(_builderOpenMapBuffer);
                break;
            case GarrisonBuilderPathField.Background:
                SetGarrisonBuilderBackgroundPath(_builderBackgroundPathBuffer);
                break;
            case GarrisonBuilderPathField.Walkmask:
                SetGarrisonBuilderWalkmaskPath(_builderWalkmaskPathBuffer);
                break;
            case GarrisonBuilderPathField.Save:
                _builderSavePath = _builderSavePathBuffer.Trim().Trim('"');
                SaveGarrisonBuilderDocument();
                break;
            case GarrisonBuilderPathField.ResourceName:
                CommitGarrisonBuilderResourceName();
                break;
            case GarrisonBuilderPathField.ResourcePath:
                CommitGarrisonBuilderResourcePath();
                break;
            case GarrisonBuilderPathField.ResourceOutputDirectory:
                ExportGarrisonBuilderResources(_builderResourceOutputDirectoryBuffer);
                break;
        }
    }

    private bool TryApplyGarrisonBuilderNativePathDialog(GarrisonBuilderPathField field)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        switch (field)
        {
            case GarrisonBuilderPathField.OpenMap:
                return TryChooseGarrisonBuilderFile("Load map", "Map files (*.png;*.json)|*.png;*.json|PNG files (*.png)|*.png|JSON packages (*.json)|*.json|All files (*.*)|*.*", _builderOpenMapBuffer, out _builderOpenMapBuffer)
                    && ApplyChosenGarrisonBuilderPath(field);
            case GarrisonBuilderPathField.Background:
                return TryChooseGarrisonBuilderFile("Load background PNG", "PNG files (*.png)|*.png|All files (*.*)|*.*", _builderBackgroundPathBuffer, out _builderBackgroundPathBuffer)
                    && ApplyChosenGarrisonBuilderPath(field);
            case GarrisonBuilderPathField.Walkmask:
                return TryChooseGarrisonBuilderFile("Load walkmask PNG", "PNG files (*.png)|*.png|All files (*.*)|*.*", _builderWalkmaskPathBuffer, out _builderWalkmaskPathBuffer)
                    && ApplyChosenGarrisonBuilderPath(field);
            case GarrisonBuilderPathField.ResourcePath:
                return TryChooseGarrisonBuilderFile("Load builder resource", "Image files (*.png;*.gif)|*.png;*.gif|PNG files (*.png)|*.png|GIF files (*.gif)|*.gif|All files (*.*)|*.*", _builderResourcePathBuffer, out _builderResourcePathBuffer)
                    && ApplyChosenGarrisonBuilderPath(field);
            case GarrisonBuilderPathField.ResourceOutputDirectory:
                return TryChooseGarrisonBuilderFolder("Choose resource output directory", _builderResourceOutputDirectoryBuffer, out _builderResourceOutputDirectoryBuffer)
                    && ApplyChosenGarrisonBuilderPath(field);
            case GarrisonBuilderPathField.Save:
                return TryChooseGarrisonBuilderSaveFile("Save map", "Package manifests (*.json)|*.json|Legacy PNG files (*.png)|*.png|All files (*.*)|*.*", _builderSavePathBuffer, out _builderSavePathBuffer)
                    && ApplyChosenGarrisonBuilderPath(field);
            default:
                return false;
        }
    }

    private bool ApplyChosenGarrisonBuilderPath(GarrisonBuilderPathField field)
    {
        ApplyGarrisonBuilderPathField(field);
        return true;
    }

    private bool TryChooseGarrisonBuilderFile(string title, string filter, string initialPath, out string selectedPath)
    {
        var script = string.Concat(
            "Add-Type -AssemblyName System.Windows.Forms;",
            "$d=New-Object System.Windows.Forms.OpenFileDialog;",
            "$d.Title=", ToPowerShellSingleQuotedString(title), ";",
            "$d.Filter=", ToPowerShellSingleQuotedString(filter), ";",
            SetInitialDialogDirectoryScript(initialPath),
            "if($d.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK){[Console]::Write($d.FileName)}");
        return TryRunGarrisonBuilderDialogScript(script, out selectedPath);
    }

    private bool TryChooseGarrisonBuilderSaveFile(string title, string filter, string initialPath, out string selectedPath)
    {
        var script = string.Concat(
            "Add-Type -AssemblyName System.Windows.Forms;",
            "$d=New-Object System.Windows.Forms.SaveFileDialog;",
            "$d.Title=", ToPowerShellSingleQuotedString(title), ";",
            "$d.Filter=", ToPowerShellSingleQuotedString(filter), ";",
            "$d.DefaultExt='png';",
            "$d.AddExtension=$true;",
            SetInitialDialogDirectoryScript(initialPath),
            "if($d.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK){[Console]::Write($d.FileName)}");
        return TryRunGarrisonBuilderDialogScript(script, out selectedPath);
    }

    private bool TryChooseGarrisonBuilderFolder(string title, string initialPath, out string selectedPath)
    {
        var script = string.Concat(
            "Add-Type -AssemblyName System.Windows.Forms;",
            "$d=New-Object System.Windows.Forms.FolderBrowserDialog;",
            "$d.Description=", ToPowerShellSingleQuotedString(title), ";",
            "$p=", ToPowerShellSingleQuotedString(initialPath.Trim().Trim('"')), ";",
            "if($p -and (Test-Path -LiteralPath $p)){$d.SelectedPath=$p};",
            "if($d.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK){[Console]::Write($d.SelectedPath)}");
        return TryRunGarrisonBuilderDialogScript(script, out selectedPath);
    }

    private static string SetInitialDialogDirectoryScript(string initialPath)
    {
        var trimmed = initialPath.Trim().Trim('"');
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        return string.Concat(
            "$p=", ToPowerShellSingleQuotedString(trimmed), ";",
            "if(Test-Path -LiteralPath $p){",
            "$i=Get-Item -LiteralPath $p;",
            "if($i.PSIsContainer){$d.InitialDirectory=$i.FullName}else{$d.InitialDirectory=$i.DirectoryName;$d.FileName=$i.Name}",
            "};");
    }

    private bool TryRunGarrisonBuilderDialogScript(string script, out string selectedPath)
    {
        selectedPath = string.Empty;
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -STA -Command {ToCommandLineArgument(script)}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });
            if (process is null)
            {
                return false;
            }

            selectedPath = process.StandardOutput.ReadToEnd().Trim();
            var error = process.StandardError.ReadToEnd().Trim();
            process.WaitForExit();
            if (selectedPath.Length == 0)
            {
                if (error.Length > 0)
                {
                    AddConsoleLine($"builder file dialog failed: {error}");
                }

                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            AddConsoleLine($"builder file dialog failed: {ex.Message}");
            selectedPath = string.Empty;
            return false;
        }
    }

    private static string ToPowerShellSingleQuotedString(string value)
    {
        return string.Concat("'", value.Replace("'", "''", StringComparison.Ordinal), "'");
    }

    private static string ToCommandLineArgument(string value)
    {
        return string.Concat("\"", value.Replace("\"", "\\\"", StringComparison.Ordinal), "\"");
    }

    private string GetGarrisonBuilderPathFieldBuffer(GarrisonBuilderPathField field)
    {
        return field switch
        {
            GarrisonBuilderPathField.OpenMap => _builderOpenMapBuffer,
            GarrisonBuilderPathField.Background => _builderBackgroundPathBuffer,
            GarrisonBuilderPathField.Walkmask => _builderWalkmaskPathBuffer,
            GarrisonBuilderPathField.Save => _builderSavePathBuffer,
            GarrisonBuilderPathField.ResourceName => _builderResourceNameBuffer,
            GarrisonBuilderPathField.ResourcePath => _builderResourcePathBuffer,
            GarrisonBuilderPathField.ResourceOutputDirectory => _builderResourceOutputDirectoryBuffer,
            _ => string.Empty,
        };
    }

    private void SetGarrisonBuilderPathFieldBuffer(GarrisonBuilderPathField field, string value)
    {
        switch (field)
        {
            case GarrisonBuilderPathField.OpenMap:
                _builderOpenMapBuffer = value;
                break;
            case GarrisonBuilderPathField.Background:
                _builderBackgroundPathBuffer = value;
                break;
            case GarrisonBuilderPathField.Walkmask:
                _builderWalkmaskPathBuffer = value;
                break;
            case GarrisonBuilderPathField.Save:
                _builderSavePathBuffer = value;
                break;
            case GarrisonBuilderPathField.ResourceName:
                _builderResourceNameBuffer = value;
                break;
            case GarrisonBuilderPathField.ResourcePath:
                _builderResourcePathBuffer = value;
                break;
            case GarrisonBuilderPathField.ResourceOutputDirectory:
                _builderResourceOutputDirectoryBuffer = value;
                break;
        }
    }

    private bool UpdateGarrisonBuilderPropertyEditor(KeyboardState keyboard, MouseState mouse)
    {
        if (_builderPropertyTarget == GarrisonBuilderPropertyTarget.None)
        {
            return false;
        }

        if (_builderObjectiveMapPickActive)
        {
            if (IsKeyPressed(keyboard, Keys.Escape))
            {
                CancelGarrisonBuilderObjectiveMapPick();
            }

            return false;
        }

        if (_builderLogicMapPickActive)
        {
            if (IsKeyPressed(keyboard, Keys.Escape))
            {
                CancelGarrisonBuilderLogicMapPick();
            }

            return false;
        }

        if (_builderEntityMapPickActive)
        {
            if (IsKeyPressed(keyboard, Keys.Escape))
            {
                CancelGarrisonBuilderEntityMapPick();
            }

            return false;
        }

        if (IsKeyPressed(keyboard, Keys.Escape))
        {

            if (_builderPropertyEditMode == GarrisonBuilderPropertyEditMode.List)
            {
                CloseGarrisonBuilderPropertyEditor(applyChanges: true);
            }
            else
            {
                _builderPropertyEditMode = GarrisonBuilderPropertyEditMode.List;
                _builderPropertyEditKey = string.Empty;
                _builderPropertyEditBuffer = string.Empty;
            }

            return true;
        }

        if (_builderPropertyEditMode == GarrisonBuilderPropertyEditMode.List)
        {
            var wheelDelta = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
            if (wheelDelta != 0 && GetGarrisonBuilderPropertyEditorBounds().Contains(mouse.Position))
            {
                var rows = GetGarrisonBuilderPropertyRows();
                var visibleRows = GetGarrisonBuilderPropertyVisibleRows(GetGarrisonBuilderPropertyEditorBounds());
                _builderPropertyScrollIndex = Math.Clamp(
                    _builderPropertyScrollIndex + (wheelDelta > 0 ? -1 : 1),
                    0,
                    Math.Max(0, rows.Count - visibleRows));
                return true;
            }

            if (mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released)
            {
                HandleGarrisonBuilderPropertyEditorClick(mouse.Position, leftClick: true);
            }
            else if (mouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Released)
            {
                HandleGarrisonBuilderPropertyEditorClick(mouse.Position, leftClick: false);
            }

            return true;
        }

        if (IsKeyPressed(keyboard, Keys.Enter))
        {
            CommitGarrisonBuilderPropertyEditorText();
            return true;
        }

        if (IsKeyPressed(keyboard, Keys.Back))
        {
            var result = DeleteTextSelectionOrBackspace(_builderPropertyEditBuffer, _builderPropertyCursorIndex, _builderPropertySelectionStart);
            _builderPropertyEditBuffer = result.Text;
            _builderPropertyCursorIndex = result.CursorIndex;
            _builderPropertySelectionStart = result.SelectionStart;
            return true;
        }

        var shiftHeld = IsShiftHeld(keyboard);
        if (IsKeyPressed(keyboard, Keys.Left))
        {
            var result = MoveTextCursorLeft(_builderPropertyCursorIndex, _builderPropertySelectionStart, shiftHeld);
            _builderPropertyCursorIndex = result.CursorIndex;
            _builderPropertySelectionStart = result.SelectionStart;
            return true;
        }

        if (IsKeyPressed(keyboard, Keys.Right))
        {
            var result = MoveTextCursorRight(_builderPropertyCursorIndex, _builderPropertySelectionStart, _builderPropertyEditBuffer, shiftHeld);
            _builderPropertyCursorIndex = result.CursorIndex;
            _builderPropertySelectionStart = result.SelectionStart;
            return true;
        }

        return true;
    }

    private bool HandleGarrisonBuilderPropertyTextInput(char character)
    {
        if (_builderPropertyTarget == GarrisonBuilderPropertyTarget.None
            || _builderPropertyEditMode == GarrisonBuilderPropertyEditMode.List)
        {
            return false;
        }

        if (IsGarrisonBuilderNodePriorityTextEditActive() && !char.IsDigit(character))
        {
            return false;
        }

        if (IsGarrisonBuilderScoreTriggerValueTextEditActive() && !char.IsDigit(character))
        {
            return false;
        }

        if (IsGarrisonBuilderCapTimeMultiplierTextEditActive()
            && !IsGarrisonBuilderCapTimeMultiplierTextCharacterAllowed(character))
        {
            return false;
        }

        var maxLength = IsGarrisonBuilderNodePriorityTextEditActive() || IsGarrisonBuilderScoreTriggerValueTextEditActive()
            ? 3
            : 180;
        var result = InsertTextCharacterAtCursor(_builderPropertyEditBuffer, character, _builderPropertyCursorIndex, _builderPropertySelectionStart, maxLength);
        _builderPropertyEditBuffer = result.Text;
        _builderPropertyCursorIndex = result.CursorIndex;
        _builderPropertySelectionStart = result.SelectionStart;
        return true;
    }

    private void BeginEditingGarrisonBuilderPlacementProperties(CustomMapBuilderEntityDefinition definition)
    {
        _builderPropertyTarget = GarrisonBuilderPropertyTarget.PlacementEntity;
        _builderPropertyEditMode = GarrisonBuilderPropertyEditMode.List;
        _builderPropertyEditorTitle = $"{definition.Label} properties";
        _builderPropertyScrollIndex = 0;
        _builderPropertyEditorValues = new Dictionary<string, string>(definition.DefaultProperties, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in _builderPlacementPropertyOverrides)
        {
            if (string.IsNullOrWhiteSpace(pair.Value))
            {
                _builderPropertyEditorValues.Remove(pair.Key);
            }
            else
            {
                _builderPropertyEditorValues[pair.Key] = pair.Value;
            }
        }

        SyncGarrisonBuilderSpawnPropertyEditorFields();
        SyncGarrisonBuilderControlPointPropertyEditorFields();

        if (definition.Type.Equals("spawn", StringComparison.OrdinalIgnoreCase)
            && (_builderSelectedGameMode & (CustomMapBuilderGameMode.KingOfTheHill | CustomMapBuilderGameMode.DualKingOfTheHill)) != 0
            && IsGarrisonBuilderEditedSpawnForwardEnabled()
            && !_builderPlacementPropertyOverrides.ContainsKey("linkObjective"))
        {
            TryApplyDefaultGarrisonBuilderPlacementObjectiveLink();
        }

        _builderStatus = $"editing {_builderSelectedEntityType} properties";
    }

    private bool HandleGarrisonBuilderPropertyEditorClick(Point position, bool leftClick)
    {
        if (!leftClick)
        {
            CloseGarrisonBuilderPropertyEditor(applyChanges: true);
            return true;
        }

        var bounds = GetGarrisonBuilderPropertyEditorBounds();
        if (!bounds.Contains(position))
        {
            CloseGarrisonBuilderPropertyEditor(applyChanges: true);
            return true;
        }

        if (_builderPropertyEditMode != GarrisonBuilderPropertyEditMode.List)
        {
            return true;
        }

        var addBounds = GetGarrisonBuilderPropertyAddBounds(bounds);
        if (addBounds.Contains(position))
        {
            BeginGarrisonBuilderPropertyTextEdit(string.Empty, string.Empty, GarrisonBuilderPropertyEditMode.NewKey);
            return true;
        }

        var rows = GetGarrisonBuilderPropertyRows();
        var visibleRows = GetGarrisonBuilderPropertyVisibleRows(bounds);
        _builderPropertyScrollIndex = Math.Clamp(_builderPropertyScrollIndex, 0, Math.Max(0, rows.Count - visibleRows));
        var visibleRowIndex = (position.Y - GetGarrisonBuilderPropertyListTop(bounds)) / GetGarrisonBuilderPropertyRowHeight();
        if (visibleRowIndex < 0 || visibleRowIndex >= visibleRows)
        {
            return true;
        }

        var rowIndex = _builderPropertyScrollIndex + visibleRowIndex;
        if (rowIndex < 0 || rowIndex >= rows.Count)
        {
            return true;
        }

        var key = rows[rowIndex];
        if (IsGarrisonBuilderControlPointSectionProperty(key))
        {
            return true;
        }

        if (!_builderPropertyEditorValues.TryGetValue(key, out var value))
        {
            value = string.Empty;
        }

        var rowHeight = GetGarrisonBuilderPropertyRowHeight();
        var rowY = GetGarrisonBuilderPropertyListTop(bounds) + visibleRowIndex * rowHeight;
        var rowBounds = GetGarrisonBuilderPropertyRowBounds(bounds, rowY);
        if (TryClearGarrisonBuilderPropertyConnectionClick(key, value, rowBounds, position))
        {
            return true;
        }

        if (key.Equals("team", StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var teleportTeamEntity)
            && teleportTeamEntity.Equals(TeleportMetadata.TeleportEntityType, StringComparison.OrdinalIgnoreCase))
        {
            _builderPropertyEditorValues[key] = TeleportMetadata.CycleTeamPropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if (key.Equals(MapLogicSignalMetadata.SignalPropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var signalEntity)
            && MapLogicSignalMetadata.SupportsSignalProperty(signalEntity))
        {
            var nextSignal = MapLogicSignalMetadata.CycleSignalPropertyValue(value);
            MapLogicSignalMetadata.ApplySignalModeSelection(
                _builderPropertyEditorValues,
                signalEntity,
                MapLogicSignalMetadata.ParseSignalMode(nextSignal));
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if (key.Equals(MapLogicSignalMetadata.DetectPropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var detectEntity)
            && MapLogicSignalMetadata.ParseSignalMode(_builderPropertyEditorValues) == MapLogicSignalMode.Impulse)
        {
            if (detectEntity.Equals(MapLogicMetadata.CpTriggerEntityType, StringComparison.OrdinalIgnoreCase))
            {
                _builderPropertyEditorValues[key] = MapLogicSignalMetadata.CycleCpCaptureDetectPropertyValue(value);
            }
            else if (detectEntity.Equals(MapLogicMetadata.PlayerTriggerEntityType, StringComparison.OrdinalIgnoreCase))
            {
                _builderPropertyEditorValues[key] = MapLogicSignalMetadata.CyclePlayerDetectPropertyValue(value);
            }
            else
            {
                return true;
            }

            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if (key.Equals("team", StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var playerTriggerTeamEntity)
            && playerTriggerTeamEntity.Equals(MapLogicMetadata.PlayerTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            _builderPropertyEditorValues[key] = PlayerTriggerMetadata.CycleTeamPropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if (key.Equals(IntelTriggerMetadata.IntelPropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var intelTriggerEntity)
            && intelTriggerEntity.Equals(MapLogicMetadata.IntelTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            _builderPropertyEditorValues[key] = IntelTriggerMetadata.CycleIntelPropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if (key.Equals(IntelTriggerMetadata.TriggerWhenPropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var intelLatchEntity)
            && intelLatchEntity.Equals(MapLogicMetadata.IntelTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            _builderPropertyEditorValues[key] = IntelTriggerMetadata.CycleLatchStatePropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if (key.Equals(ScrMapSettingsMetadata.WinWhenScorePropertyKey, StringComparison.OrdinalIgnoreCase)
            && _builderPropertyTarget == GarrisonBuilderPropertyTarget.MapProperties)
        {
            _builderPropertyEditorValues[key] = ScrMapSettingsMetadata.CycleWinWhenScorePropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if (key.Equals(ScrMapSettingsMetadata.RoundEndWinPropertyKey, StringComparison.OrdinalIgnoreCase)
            && _builderPropertyTarget == GarrisonBuilderPropertyTarget.MapProperties)
        {
            _builderPropertyEditorValues[key] = ScrMapSettingsMetadata.CycleRoundEndWinPropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if (key.Equals(MapLogicScoreTriggerMetadata.ScoreTeamPropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var scoreTeamEntity)
            && scoreTeamEntity.Equals(MapLogicMetadata.ScoreTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            _builderPropertyEditorValues[key] = MapLogicScoreTriggerMetadata.CycleScoreTeamPropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if (key.Equals(MapLogicScoreTriggerMetadata.ChangePropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var scoreChangeEntity)
            && scoreChangeEntity.Equals(MapLogicMetadata.ScoreTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            _builderPropertyEditorValues[key] = MapLogicScoreTriggerMetadata.CycleChangePropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if (TryHandleGarrisonBuilderScoreTriggerValueClick(key, value, rowBounds, position))
        {
        }
        else if (TryHandleGarrisonBuilderScrScorePropertyClick(key, value, rowBounds, position))
        {
        }
        else if (key.Equals("team", StringComparison.OrdinalIgnoreCase))
        {
            _builderPropertyEditorValues[key] = CycleGarrisonBuilderTeamPropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if (BarrierTargetFilterMetadata.IsTargetPropertyKey(key)
            && IsEditingGarrisonBuilderBarrierEntity())
        {
            _builderPropertyEditorValues[key] = BarrierTargetFilterMetadata.CyclePropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if (key.Equals(DirectionalWallConfiguration.PassDirectionPropertyKey, StringComparison.OrdinalIgnoreCase)
            && IsEditingGarrisonBuilderDirectionalWallEntity())
        {
            _builderPropertyEditorValues[key] = DirectionalWallConfiguration.CyclePassDirectionValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if ((key.Equals(DirectionalWallConfiguration.PlayersPropertyKey, StringComparison.OrdinalIgnoreCase)
                || key.Equals(DirectionalWallConfiguration.ProjectilesPropertyKey, StringComparison.OrdinalIgnoreCase))
            && IsEditingGarrisonBuilderDirectionalWallEntity())
        {
            _builderPropertyEditorValues[key] = DirectionalWallConfiguration.CycleAffectPropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if (key.Equals(ControlPointIndexMetadata.PropertyKey, StringComparison.OrdinalIgnoreCase)
            && IsEditingGarrisonBuilderControlPointEntity())
        {
            _builderPropertyEditorValues[key] = ControlPointIndexMetadata.CyclePropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if (key.Equals(ControlPointInitialOwnershipMetadata.PropertyKey, StringComparison.OrdinalIgnoreCase)
            && IsEditingGarrisonBuilderControlPointEntity()
            && IsGarrisonBuilderControlPointOverrideEnabled())
        {
            _builderPropertyEditorValues[key] = ControlPointInitialOwnershipMetadata.CycleOverridePropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if ((key.Equals(ControlPointLockDependencyMetadata.LockedWhenTeamPropertyKey, StringComparison.OrdinalIgnoreCase)
                || key.Equals(ControlPointLockDependencyMetadata.UnlockedWhenTeamPropertyKey, StringComparison.OrdinalIgnoreCase))
            && IsEditingGarrisonBuilderControlPointEntity()
            && IsGarrisonBuilderControlPointOverrideEnabled())
        {
            _builderPropertyEditorValues[key] = ControlPointLockDependencyMetadata.CycleTeamPropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if (key.Equals(ControlPointInitialLockStateMetadata.PropertyKey, StringComparison.OrdinalIgnoreCase)
            && IsEditingGarrisonBuilderControlPointEntity()
            && IsGarrisonBuilderControlPointOverrideEnabled())
        {
            _builderPropertyEditorValues[key] = ControlPointInitialLockStateMetadata.CyclePropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if ((key.Equals(ControlPointLockDependencyMetadata.LockedWhenCpPropertyKey, StringComparison.OrdinalIgnoreCase)
                || key.Equals(ControlPointLockDependencyMetadata.UnlockedWhenCpPropertyKey, StringComparison.OrdinalIgnoreCase))
            && IsEditingGarrisonBuilderControlPointEntity()
            && IsGarrisonBuilderControlPointOverrideEnabled())
        {
            BeginGarrisonBuilderObjectiveMapPick(key);
        }
        else if (key.Equals(ForwardSpawnPriorityMetadata.PropertyKey, StringComparison.OrdinalIgnoreCase)
            && IsEditingGarrisonBuilderSpawnEntity()
            && IsGarrisonBuilderEditedSpawnForwardEnabled())
        {
            _builderPropertyEditorValues[key] = ForwardSpawnPriorityMetadata.CyclePropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if (key.Equals(ForwardSpawnMetadata.UseWhenPropertyKey, StringComparison.OrdinalIgnoreCase)
            && IsEditingGarrisonBuilderSpawnEntity()
            && IsGarrisonBuilderEditedSpawnForwardEnabled()
            && !IsGarrisonBuilderSpawnUsingLogicSignal())
        {
            _builderPropertyEditorValues[key] = ForwardSpawnMetadata.CycleUseWhenPropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if (key.Equals(MapLogicMetadata.GateTypePropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var gateTypeEntity)
            && gateTypeEntity.Equals(MapLogicMetadata.GateEntityType, StringComparison.OrdinalIgnoreCase))
        {
            _builderPropertyEditorValues[key] = MapLogicMetadata.CycleGateTypePropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if (key.Equals(MapLogicMetadata.InitialValuePropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var oscillatorInitialEntity)
            && oscillatorInitialEntity.Equals(MapLogicMetadata.OscillatorEntityType, StringComparison.OrdinalIgnoreCase))
        {
            _builderPropertyEditorValues[key] = MapLogicMetadata.CycleInitialValuePropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if (key.Equals(MapLogicMetadata.RequiredOwnerPropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var cpTriggerEntity)
            && cpTriggerEntity.Equals(MapLogicMetadata.CpTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            _builderPropertyEditorValues[key] = MapLogicMetadata.CycleRequiredOwnerPropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if (key.Equals(MapLogicMetadata.ActivatorBehaviorPropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var activatorEntity)
            && activatorEntity.Equals(MapLogicMetadata.ActivatorEntityType, StringComparison.OrdinalIgnoreCase))
        {
            _builderPropertyEditorValues[key] = MapLogicMetadata.CycleActivatorBehaviorPropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if (IsGarrisonBuilderActivatorEntityMapPickProperty(key)
            && TryGetGarrisonBuilderEditedEntityType(out var activatorPickEntity)
            && activatorPickEntity.Equals(MapLogicMetadata.ActivatorEntityType, StringComparison.OrdinalIgnoreCase))
        {
            BeginGarrisonBuilderEntityMapPick(key);
        }
        else if (IsGarrisonBuilderTeleportExitMapPickProperty(key)
            && TryGetGarrisonBuilderEditedEntityType(out var teleportPickEntity)
            && teleportPickEntity.Equals(TeleportMetadata.TeleportEntityType, StringComparison.OrdinalIgnoreCase))
        {
            BeginGarrisonBuilderEntityMapPick(key);
        }
        else if (IsGarrisonBuilderAreaExtendsMapPickProperty(key)
            && TryGetGarrisonBuilderEditedEntityType(out var areaExtendsEntity)
            && areaExtendsEntity.Equals(AreaExtensionMetadata.AreaEntityType, StringComparison.OrdinalIgnoreCase))
        {
            BeginGarrisonBuilderEntityMapPick(key);
        }
        else if (IsGarrisonBuilderDamageableEntityMapPickProperty(key)
            && TryGetGarrisonBuilderEditedEntityType(out var damageTriggerEntity)
            && damageTriggerEntity.Equals(DamageTriggerMetadata.DamageTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            BeginGarrisonBuilderEntityMapPick(key);
        }
        else if (key.Equals(DamageTriggerMetadata.AffectedByTeamPropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var damageTriggerTeamEntity)
            && damageTriggerTeamEntity.Equals(DamageTriggerMetadata.DamageTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            _builderPropertyEditorValues[key] = DamageTriggerMetadata.CycleAffectedByTeamPropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if (DamageTriggerMetadata.IsExclusiveModePropertyKey(key)
            && TryGetGarrisonBuilderEditedEntityType(out var damageTriggerToggleEntity)
            && damageTriggerToggleEntity.Equals(DamageTriggerMetadata.DamageTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            ToggleGarrisonBuilderDamageTriggerExclusiveMode(key, value);
        }
        else if ((key.Equals(DamageableMetadata.ShowHealthBarPropertyKey, StringComparison.OrdinalIgnoreCase)
                || key.Equals(DamageableMetadata.BlockPlayersPropertyKey, StringComparison.OrdinalIgnoreCase)
                || key.Equals(DamageableMetadata.DisableWhenDestroyedPropertyKey, StringComparison.OrdinalIgnoreCase)
                || key.Equals(DamageableMetadata.SentryTargetPropertyKey, StringComparison.OrdinalIgnoreCase)
                || key.Equals(DamageableMetadata.StabbablePropertyKey, StringComparison.OrdinalIgnoreCase))
            && TryGetGarrisonBuilderEditedEntityType(out var damageableToggleEntity)
            && damageableToggleEntity.Equals(DamageableMetadata.DamageableEntityType, StringComparison.OrdinalIgnoreCase))
        {
            _builderPropertyEditorValues[key] = DamageableMetadata.CycleBooleanPropertyValue(value);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if (IsGarrisonBuilderLogicMapPickProperty(key))
        {
            BeginGarrisonBuilderLogicMapPick(key);
        }
        else if (key.Equals("linkObjective", StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var cpTriggerLinkEntity)
            && cpTriggerLinkEntity.Equals(MapLogicMetadata.CpTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            BeginGarrisonBuilderObjectiveMapPick("linkObjective");
        }
        else if (key.Equals("linkObjective", StringComparison.OrdinalIgnoreCase)
            && IsEditingGarrisonBuilderSpawnEntity()
            && IsGarrisonBuilderEditedSpawnForwardEnabled()
            && !IsGarrisonBuilderSpawnUsingLogicSignal())
        {
            BeginGarrisonBuilderObjectiveMapPick("linkObjective");
        }
        else if (value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            var newValue = value.Equals("true", StringComparison.OrdinalIgnoreCase) ? "false" : "true";
            _builderPropertyEditorValues[key] = newValue;
            if (key.Equals(ControlPointMapSettingsMetadata.OverrideInitialCpsPropertyKey, StringComparison.OrdinalIgnoreCase)
                && _builderPropertyTarget == GarrisonBuilderPropertyTarget.MapProperties)
            {
                SyncGarrisonBuilderControlPointPropertyEditorFields();
            }

            if (key.Equals("forward", StringComparison.OrdinalIgnoreCase) && IsEditingGarrisonBuilderSpawnEntity())
            {
                SyncGarrisonBuilderSpawnPropertyEditorFields();
                if (IsGarrisonBuilderEditedSpawnForwardEnabled() && !IsGarrisonBuilderSpawnUsingLogicSignal())
                {
                    if (_builderPropertyTarget == GarrisonBuilderPropertyTarget.PlacementEntity
                        && (_builderSelectedGameMode & (CustomMapBuilderGameMode.KingOfTheHill | CustomMapBuilderGameMode.DualKingOfTheHill)) != 0
                        && !TryGetGarrisonBuilderSpawnObjectiveLink(out _))
                    {
                        TryApplyDefaultGarrisonBuilderPlacementObjectiveLink();
                    }
                    else if (!TryGetGarrisonBuilderSpawnObjectiveLink(out _))
                    {
                        BeginGarrisonBuilderObjectiveMapPick("linkObjective");
                    }
                }

                ClampGarrisonBuilderPropertyEditorScrollIndex();
            }

            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if (IsGarrisonBuilderObjectiveMapPickProperty(key)
            && ((IsEditingGarrisonBuilderSpawnEntity()
                    && IsGarrisonBuilderEditedSpawnForwardEnabled()
                    && !IsGarrisonBuilderSpawnUsingLogicSignal())
                || (TryGetGarrisonBuilderEditedEntityType(out var objectivePickEntity)
                    && objectivePickEntity.Equals(MapLogicMetadata.CpTriggerEntityType, StringComparison.OrdinalIgnoreCase))
                || (IsEditingGarrisonBuilderControlPointEntity() && IsGarrisonBuilderControlPointOverrideEnabled())))
        {
            BeginGarrisonBuilderObjectiveMapPick(key);
        }
        else if (TryBeginGarrisonBuilderCapTimeMultiplierTextEdit(key, value, rowBounds, position))
        {
        }
        else if (TryHandleGarrisonBuilderCustomSpritePropertyClick(key, value))
        {
        }
        else if (!TryHandleGarrisonBuilderCustomSpriteZOrderClick(key, value, rowBounds, position)
            && !IsGarrisonBuilderCustomSpriteZOrderPropertyRow(key)
            && !TryHandleGarrisonBuilderScoreTriggerValueClick(key, value, rowBounds, position)
            && !IsGarrisonBuilderScoreTriggerValuePropertyRow(key)
            && !TryHandleGarrisonBuilderNodePriorityClick(key, value, rowBounds, position)
            && !IsGarrisonBuilderNodePriorityPropertyRow(key)
            && !TryHandleGarrisonBuilderTriggerBelowPercentClick(key, value, rowBounds, position)
            && !IsGarrisonBuilderTriggerBelowPercentPropertyRow(key))
        {
            BeginGarrisonBuilderPropertyTextEdit(key, value, GarrisonBuilderPropertyEditMode.EditValue);
        }

        return true;
    }

    private static string CycleGarrisonBuilderTeamPropertyValue(string current)
    {
        if (current.Equals("red", StringComparison.OrdinalIgnoreCase))
        {
            return "blue";
        }

        if (current.Equals("blue", StringComparison.OrdinalIgnoreCase))
        {
            return "neutral";
        }

        return "red";
    }

    private static string FormatGarrisonBuilderBarrierTargetFilterLabel(string key, string value)
    {
        var action = BarrierTargetFilterMetadata.Parse(value) == BarrierTargetFilter.Block ? "Block" : "Allow";
        var label = key switch
        {
            _ when key.Equals(BarrierTargetFilterMetadata.RedPlayersPropertyKey, StringComparison.OrdinalIgnoreCase) => "Red players",
            _ when key.Equals(BarrierTargetFilterMetadata.BluePlayersPropertyKey, StringComparison.OrdinalIgnoreCase) => "Blue players",
            _ when key.Equals(BarrierTargetFilterMetadata.RedShotsPropertyKey, StringComparison.OrdinalIgnoreCase) => "Red shots",
            _ when key.Equals(BarrierTargetFilterMetadata.BlueShotsPropertyKey, StringComparison.OrdinalIgnoreCase) => "Blue shots",
            _ when key.Equals(BarrierTargetFilterMetadata.RedIntelPropertyKey, StringComparison.OrdinalIgnoreCase) => "Red intel",
            _ when key.Equals(BarrierTargetFilterMetadata.BlueIntelPropertyKey, StringComparison.OrdinalIgnoreCase) => "Blue intel",
            _ => FormatGarrisonBuilderPropertyKeyLabel(key),
        };

        return $"{label}: {action}";
    }

    private void BeginGarrisonBuilderPropertyTextEdit(string key, string value, GarrisonBuilderPropertyEditMode mode)
    {
        _builderPropertyEditKey = key;
        _builderPropertyEditBuffer = value;
        _builderPropertyCursorIndex = value.Length;
        _builderPropertySelectionStart = _builderPropertyCursorIndex;
        _builderPropertyEditMode = mode;
    }

    private void CommitGarrisonBuilderPropertyEditorText()
    {
        if (_builderPropertyEditMode == GarrisonBuilderPropertyEditMode.NewKey)
        {
            var key = _builderPropertyEditBuffer.Trim();
            if (key.Length == 0
                || key.Equals("type", StringComparison.OrdinalIgnoreCase)
                || key.Equals("xscale", StringComparison.OrdinalIgnoreCase)
                || key.Equals("yscale", StringComparison.OrdinalIgnoreCase)
                || IsGarrisonBuilderMapPropertyEditorKey(key)
                || (_builderPropertyTarget == GarrisonBuilderPropertyTarget.MapProperties
                    && IsGarrisonBuilderMapPropertiesManagedMetadataKey(key)))
            {
                _builderPropertyEditMode = GarrisonBuilderPropertyEditMode.List;
                return;
            }

            BeginGarrisonBuilderPropertyTextEdit(key, string.Empty, GarrisonBuilderPropertyEditMode.EditValue);
            return;
        }

        if (_builderPropertyEditMode == GarrisonBuilderPropertyEditMode.EditValue)
        {
            var value = _builderPropertyEditBuffer.Trim();
            if (IsGarrisonBuilderNodePriorityPropertyRow(_builderPropertyEditKey))
            {
                _builderPropertyEditorValues[_builderPropertyEditKey] =
                    MapLogicMetadata.ToNodePriorityPropertyValue(
                        value.Length == 0 ? 0 : MapLogicMetadata.ParseNodePriority(value));
            }
            else if (IsGarrisonBuilderScoreTriggerValuePropertyRow(_builderPropertyEditKey))
            {
                var parsedScore = value.Length == 0
                    ? MapLogicScoreTriggerMetadata.MinValue
                    : int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                        ? parsed
                        : MapLogicScoreTriggerMetadata.DefaultValue;
                _builderPropertyEditorValues[_builderPropertyEditKey] =
                    MapLogicScoreTriggerMetadata.ToValuePropertyValue(parsedScore);
            }
            else if (IsGarrisonBuilderCustomSpriteZOrderPropertyRow(_builderPropertyEditKey))
            {
                _builderPropertyEditorValues[_builderPropertyEditKey] =
                    CustomMapCustomSpriteMetadata.ToZOrderPropertyValue(
                        value.Length == 0 ? 0 : CustomMapCustomSpriteMetadata.ParseZOrder(value));
            }
            else if (_builderPropertyEditKey.Equals(CustomMapCustomSpriteMetadata.ScalePropertyKey, StringComparison.OrdinalIgnoreCase)
                && TryGetGarrisonBuilderEditedEntityType(out var scaleEntityType)
                && CustomMapCustomSpriteMetadata.IsCustomSpriteEntityType(scaleEntityType))
            {
                _builderPropertyEditorValues[_builderPropertyEditKey] =
                    CustomMapCustomSpriteMetadata.ToScalePropertyValue(
                        value.Length == 0 ? 1f : CustomMapCustomSpriteMetadata.ParseScale(value));
            }
            else if (IsGarrisonBuilderCapTimeMultiplierPropertyRow(_builderPropertyEditKey))
            {
                var multiplier = value.Length == 0 ? 1f : ControlPointCapTimeMultiplierMetadata.ParseMultiplier(value);
                ControlPointCapTimeMultiplierMetadata.MarkCustom(_builderPropertyEditorValues, multiplier);
            }
            else if (_builderPropertyEditKey.Equals(MapLogicMetadata.CountdownSecondsPropertyKey, StringComparison.OrdinalIgnoreCase))
            {
                _builderPropertyEditorValues[_builderPropertyEditKey] =
                    MapLogicMetadata.ToCountdownSecondsPropertyValue(
                        value.Length == 0 ? 1f : MapLogicMetadata.ParseCountdownSeconds(value));
            }
            else if (_builderPropertyEditKey.Equals(MapLogicSignalMetadata.PeriodPropertyKey, StringComparison.OrdinalIgnoreCase))
            {
                _builderPropertyEditorValues[_builderPropertyEditKey] =
                    MapLogicSignalMetadata.ToPeriodPropertyValue(
                        value.Length == 0
                            ? MapLogicSignalMetadata.DefaultPeriodSeconds
                            : MapLogicSignalMetadata.ParsePeriodSeconds(value));
            }
            else if (_builderPropertyEditKey.Equals(DamageTriggerMetadata.TriggerBelowPercentPropertyKey, StringComparison.OrdinalIgnoreCase))
            {
                _builderPropertyEditorValues[_builderPropertyEditKey] =
                    DamageTriggerMetadata.ToTriggerBelowPercentPropertyValue(
                        value.Length == 0
                            ? DamageTriggerMetadata.DefaultTriggerBelowPercent
                            : DamageTriggerMetadata.ParseTriggerBelowPercent(value));
            }
            else if (_builderPropertyEditKey.Equals(DamageableMetadata.HealthPropertyKey, StringComparison.OrdinalIgnoreCase))
            {
                _builderPropertyEditorValues[_builderPropertyEditKey] =
                    DamageableMetadata.ToHealthPropertyValue(
                        value.Length == 0
                            ? DamageableMetadata.DefaultHealth
                            : float.TryParse(value, out var parsed) ? parsed : DamageableMetadata.DefaultHealth);
            }
            else if (_builderPropertyEditKey.Equals(DamageTriggerMetadata.AnyDamageCooldownPropertyKey, StringComparison.OrdinalIgnoreCase)
                && TryGetGarrisonBuilderEditedEntityType(out var damageTriggerCooldownEditEntity)
                && damageTriggerCooldownEditEntity.Equals(DamageTriggerMetadata.DamageTriggerEntityType, StringComparison.OrdinalIgnoreCase))
            {
                _builderPropertyEditorValues[_builderPropertyEditKey] =
                    DamageTriggerMetadata.ToAnyDamageCooldownPropertyValue(
                        value.Length == 0
                            ? DamageTriggerMetadata.DefaultAnyDamageCooldownSeconds
                            : DamageTriggerMetadata.ParseAnyDamageCooldownSecondsValue(value));
            }
            else if (_builderPropertyEditKey.Equals(MapLogicMetadata.TrueTimePropertyKey, StringComparison.OrdinalIgnoreCase)
                && TryGetGarrisonBuilderEditedEntityType(out var damageTriggerTrueTimeEditEntity)
                && damageTriggerTrueTimeEditEntity.Equals(DamageTriggerMetadata.DamageTriggerEntityType, StringComparison.OrdinalIgnoreCase))
            {
                _builderPropertyEditorValues[_builderPropertyEditKey] =
                    DamageTriggerMetadata.ToAnyDamageTrueTimePropertyValue(
                        value.Length == 0
                            ? DamageTriggerMetadata.DefaultAnyDamageTrueTimeSeconds
                            : DamageTriggerMetadata.ParseAnyDamageTrueTimeSeconds(
                                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                                {
                                    [MapLogicMetadata.TrueTimePropertyKey] = value,
                                }));
            }
            else if (_builderPropertyEditKey.Equals(MapLogicMetadata.TrueTimePropertyKey, StringComparison.OrdinalIgnoreCase)
                || _builderPropertyEditKey.Equals(MapLogicMetadata.FalseTimePropertyKey, StringComparison.OrdinalIgnoreCase))
            {
                _builderPropertyEditorValues[_builderPropertyEditKey] =
                    MapLogicMetadata.ToCountdownSecondsPropertyValue(
                        value.Length == 0 ? 1f : MapLogicMetadata.ParseOscillatorIntervalSeconds(value));
            }
            else if (value.Length == 0)
            {
                if (_builderPropertyTarget != GarrisonBuilderPropertyTarget.MapProperties
                    || !IsGarrisonBuilderMapPropertyEditorKey(_builderPropertyEditKey))
                {
                    _builderPropertyEditorValues.Remove(_builderPropertyEditKey);
                }
            }
            else
            {
                _builderPropertyEditorValues[_builderPropertyEditKey] = value;
            }

            _builderPropertyEditMode = GarrisonBuilderPropertyEditMode.List;
            MarkGarrisonBuilderPropertyEditorChanged();
        }
    }

    private void CloseGarrisonBuilderPropertyEditor(bool applyChanges)
    {
        if (applyChanges)
        {
            if (_builderPropertyTarget == GarrisonBuilderPropertyTarget.MapProperties)
            {
                if (!TryApplyGarrisonBuilderMapPropertiesFromEditorValues())
                {
                    return;
                }
            }
            else
            {
                RecordGarrisonBuilderHistory();
                ApplyGarrisonBuilderPropertyEditorChanges();
            }
        }

        CancelGarrisonBuilderObjectiveMapPick();
        CancelGarrisonBuilderLogicMapPick();
        CancelGarrisonBuilderEntityMapPick();
        CloseGarrisonBuilderLogicRecolorDialog();
        _builderPropertyTarget = GarrisonBuilderPropertyTarget.None;
        _builderPropertyEditMode = GarrisonBuilderPropertyEditMode.List;
        _builderPropertyEditKey = string.Empty;
        _builderPropertyEditBuffer = string.Empty;
        _builderPropertyEditorTitle = string.Empty;
        _builderPropertyScrollIndex = 0;
    }

    private static void PreserveGarrisonBuilderHiddenEntityProperties(
        IReadOnlyDictionary<string, string> sourceProperties,
        IDictionary<string, string> destinationProperties)
    {
        if (sourceProperties.TryGetValue(MapLogicNodeColorMetadata.PropertyKey, out var logicColor)
            && !string.IsNullOrWhiteSpace(logicColor))
        {
            destinationProperties[MapLogicNodeColorMetadata.PropertyKey] = logicColor;
        }
    }

    private void ApplyGarrisonBuilderPropertyEditorChanges()
    {
        if (_builderPropertyTarget == GarrisonBuilderPropertyTarget.MapProperties)
        {
            return;
        }

        if (_builderPropertyTarget == GarrisonBuilderPropertyTarget.PlacementEntity)
        {
            _builderPlacementPropertyOverrides.Clear();
            foreach (var pair in _builderPropertyEditorValues)
            {
                if (!IsSkippedGarrisonBuilderProperty(pair.Key))
                {
                    _builderPlacementPropertyOverrides[pair.Key] = pair.Value;
                }
            }

            if (TryGetGarrisonBuilderEditedEntityType(out var placementEntityType))
            {
                PruneGarrisonBuilderControlPointPropertiesForSave(
                    placementEntityType,
                    _builderPlacementPropertyOverrides);
                PruneGarrisonBuilderLogicPropertiesForSave(
                    placementEntityType,
                    _builderPlacementPropertyOverrides);
            }

            _builderStatus = "placement properties updated";
            return;
        }

        if (_builderPropertyTarget == GarrisonBuilderPropertyTarget.SelectedMapEntity
            && _builderSelectedEntityIndex >= 0
            && _builderSelectedEntityIndex < _builderEntities.Count)
        {
            var entity = _builderEntities[_builderSelectedEntityIndex];
            var properties = new Dictionary<string, string>(_builderPropertyEditorValues, StringComparer.OrdinalIgnoreCase);
            PreserveGarrisonBuilderHiddenEntityProperties(entity.Properties, properties);
            PruneGarrisonBuilderControlPointPropertiesForSave(entity.Type, properties);
            PruneGarrisonBuilderLogicPropertiesForSave(entity.Type, properties);
            properties["type"] = entity.Type;
            properties["x"] = entity.X.ToString(System.Globalization.CultureInfo.InvariantCulture);
            properties["y"] = entity.Y.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (Math.Abs(entity.XScale - 1f) > float.Epsilon)
            {
                properties["xscale"] = entity.XScale.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            if (Math.Abs(entity.YScale - 1f) > float.Epsilon)
            {
                properties["yscale"] = entity.YScale.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            _builderEntities[_builderSelectedEntityIndex] = CustomMapBuilderEntity.Create(
                entity.Type,
                entity.X,
                entity.Y,
                properties,
                entity.XScale,
                entity.YScale).NormalizeForEditing();
            UpdateGarrisonBuilderDocumentEntities();
            _builderDirty = true;
            _builderStatus = "entity properties updated";
        }
    }

    private void MarkGarrisonBuilderPropertyEditorChanged()
    {
        if (_builderPropertyTarget == GarrisonBuilderPropertyTarget.MapProperties)
        {
            _builderDirty = true;
        }
    }

    private List<string> GetGarrisonBuilderPropertyRows()
    {
        if (_builderPropertyTarget == GarrisonBuilderPropertyTarget.MapProperties)
        {
            var rows = new List<string>();
            foreach (var key in GarrisonBuilderMapPropertyRowOrder)
            {
                if (_builderPropertyEditorValues.ContainsKey(key)
                    && ShouldShowGarrisonBuilderMapPropertyRow(key))
                {
                    rows.Add(key);
                }
            }

            rows.AddRange(_builderPropertyEditorValues.Keys
                .Where(key => !GarrisonBuilderMapPropertyRowOrder.Contains(key, StringComparer.OrdinalIgnoreCase)
                    && ControlPointMapSettingsMetadata.IsEditableMapMetadataKey(key)
                    && ShouldShowGarrisonBuilderMapPropertyRow(key))
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase));
            return rows;
        }

        var propertyRows = _builderPropertyEditorValues.Keys
            .Where(key => !ShouldSkipGarrisonBuilderPropertyRow(key))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (IsEditingGarrisonBuilderBarrierEntity())
        {
            return OrderGarrisonBuilderBarrierPropertyRows(propertyRows);
        }

        if (IsEditingGarrisonBuilderDirectionalWallEntity())
        {
            return OrderGarrisonBuilderDirectionalWallPropertyRows(propertyRows);
        }

        if (IsEditingGarrisonBuilderTeleportEntity())
        {
            return OrderGarrisonBuilderTeleportPropertyRows(propertyRows);
        }

        if (IsEditingGarrisonBuilderSpawnEntity())
        {
            return OrderGarrisonBuilderSpawnPropertyRowsWithLogic(propertyRows, IsGarrisonBuilderSpawnUsingLogicSignal());
        }

        if (TryGetGarrisonBuilderEditedEntityType(out var editedEntityType)
            && MapLogicMetadata.IsLogicEntityType(editedEntityType))
        {
            return OrderGarrisonBuilderLogicPropertyRows(editedEntityType, propertyRows);
        }

        if (TryGetGarrisonBuilderEditedEntityType(out var customSpriteEntityType)
            && CustomMapCustomSpriteMetadata.IsCustomSpriteEntityType(customSpriteEntityType))
        {
            return OrderGarrisonBuilderCustomSpritePropertyRows(propertyRows);
        }

        if (IsEditingGarrisonBuilderControlPointEntity())
        {
            return BuildGarrisonBuilderControlPointPropertyRowsWithLogic(propertyRows);
        }

        return propertyRows;
    }

    private static readonly string[] GarrisonBuilderSpawnPropertyRowOrder =
    [
        "team",
        "forward",
        ForwardSpawnPriorityMetadata.PropertyKey,
        "linkObjective",
        ForwardSpawnMetadata.UseWhenPropertyKey,
    ];

    private List<string> BuildGarrisonBuilderControlPointPropertyRows(List<string> rows)
    {
        var ordered = new List<string>
        {
            ControlPointIndexMetadata.PropertyKey,
            ControlPointCapTimeMultiplierMetadata.PropertyKey,
        };
        if (!IsGarrisonBuilderControlPointOverrideEnabled())
        {
            return ordered;
        }

        if (rows.Any(static key => key.Equals(ControlPointInitialOwnershipMetadata.PropertyKey, StringComparison.OrdinalIgnoreCase)))
        {
            ordered.Add(ControlPointInitialOwnershipMetadata.PropertyKey);
        }

        ordered.Add(ControlPointLockDependencyMetadata.LockedWhenSectionKey);
        ordered.Add(ControlPointLockDependencyMetadata.LockedWhenCpPropertyKey);
        ordered.Add(ControlPointLockDependencyMetadata.LockedWhenTeamPropertyKey);
        ordered.Add(ControlPointLockDependencyMetadata.UnlockedWhenSectionKey);
        ordered.Add(ControlPointLockDependencyMetadata.UnlockedWhenCpPropertyKey);
        ordered.Add(ControlPointLockDependencyMetadata.UnlockedWhenTeamPropertyKey);
        ordered.Add(ControlPointInitialLockStateMetadata.PropertyKey);
        return ordered;
    }

    private bool IsGarrisonBuilderControlPointOverrideEnabled()
    {
        return ControlPointMapSettingsMetadata.ParseOverrideInitialCps(_builderDocument.Metadata);
    }

    private void PruneGarrisonBuilderControlPointPropertiesForSave(
        string entityType,
        IDictionary<string, string> properties)
    {
        if (!ControlPointOwnershipResolver.IsControlPointEntity(entityType))
        {
            return;
        }

        if (IsGarrisonBuilderControlPointOverrideEnabled())
        {
            return;
        }

        properties.Remove(ControlPointInitialOwnershipMetadata.PropertyKey);
        properties.Remove(ControlPointLockDependencyMetadata.LockedWhenCpPropertyKey);
        properties.Remove(ControlPointLockDependencyMetadata.LockedWhenTeamPropertyKey);
        properties.Remove(ControlPointLockDependencyMetadata.UnlockedWhenCpPropertyKey);
        properties.Remove(ControlPointLockDependencyMetadata.UnlockedWhenTeamPropertyKey);
        properties.Remove(MapLogicMetadata.LockedWhenLogicPropertyKey);
        properties.Remove(MapLogicMetadata.UnlockedWhenLogicPropertyKey);
        properties.Remove(ControlPointInitialLockStateMetadata.PropertyKey);
        properties.Remove("startLocked");
    }

    private static void PruneGarrisonBuilderLogicPropertiesForSave(
        string entityType,
        IDictionary<string, string> properties)
    {
        if (!MapLogicMetadata.IsLogicEntityType(entityType))
        {
            return;
        }

        MapLogicMetadata.EnsureNodePriorityProperty(properties);
    }

    private static List<string> OrderGarrisonBuilderSpawnPropertyRows(List<string> rows)
    {
        var ordered = new List<string>(rows.Count);
        foreach (var key in GarrisonBuilderSpawnPropertyRowOrder)
        {
            var match = rows.FirstOrDefault(existing => existing.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                ordered.Add(match);
            }
        }

        foreach (var key in rows)
        {
            if (!ordered.Any(existing => existing.Equals(key, StringComparison.OrdinalIgnoreCase)))
            {
                ordered.Add(key);
            }
        }

        return ordered;
    }

    private static readonly string[] GarrisonBuilderBarrierPropertyRowOrder = BarrierTargetFilterMetadata.TargetPropertyKeys;

    private static readonly string[] GarrisonBuilderDirectionalWallPropertyRowOrder =
    [
        DirectionalWallConfiguration.PassDirectionPropertyKey,
        DirectionalWallConfiguration.PlayersPropertyKey,
        DirectionalWallConfiguration.ProjectilesPropertyKey,
    ];

    private List<string> OrderGarrisonBuilderBarrierPropertyRows(List<string> rows)
    {
        return OrderGarrisonBuilderPropertyRows(rows, GarrisonBuilderBarrierPropertyRowOrder, []);
    }

    private List<string> OrderGarrisonBuilderDirectionalWallPropertyRows(List<string> rows)
    {
        return OrderGarrisonBuilderPropertyRows(rows, GarrisonBuilderDirectionalWallPropertyRowOrder, []);
    }

    private static List<string> OrderGarrisonBuilderPropertyRows(
        List<string> rows,
        IReadOnlyList<string> baseOrder,
        IReadOnlyList<string> trailingOrder)
    {
        var ordered = new List<string>(rows.Count);
        foreach (var key in baseOrder)
        {
            var match = rows.FirstOrDefault(existing => existing.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                ordered.Add(match);
            }
        }

        foreach (var key in trailingOrder)
        {
            var match = rows.FirstOrDefault(existing => existing.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                ordered.Add(match);
            }
        }

        foreach (var key in rows)
        {
            if (!ordered.Any(existing => existing.Equals(key, StringComparison.OrdinalIgnoreCase)))
            {
                ordered.Add(key);
            }
        }

        return ordered;
    }

    private bool ShouldSkipGarrisonBuilderPropertyRow(string key)
    {
        if (IsSkippedGarrisonBuilderProperty(key))
        {
            return true;
        }

        if (ShouldSkipGarrisonBuilderSignalModePropertyRow(key))
        {
            return true;
        }

        if (TryGetGarrisonBuilderEditedEntityType(out var editedEntityType)
            && MapLogicMetadata.IsLogicEntityType(editedEntityType)
            && key.Equals(MapLogicMetadata.LogicKeyPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (TryGetGarrisonBuilderEditedEntityType(out var timerEntityType)
            && timerEntityType.Equals(MapLogicMetadata.TimerEntityType, StringComparison.OrdinalIgnoreCase)
            && key.Equals(MapLogicMetadata.LogicInputPropertyKey, StringComparison.OrdinalIgnoreCase)
            && MapLogicMetadata.ParseTriggerOnStart(_builderPropertyEditorValues))
        {
            return true;
        }

        if (TryGetGarrisonBuilderEditedEntityType(out var oscillatorEntityType)
            && oscillatorEntityType.Equals(MapLogicMetadata.OscillatorEntityType, StringComparison.OrdinalIgnoreCase)
            && key.Equals(MapLogicMetadata.StartWhenPropertyKey, StringComparison.OrdinalIgnoreCase)
            && MapLogicMetadata.ParseAutostart(_builderPropertyEditorValues))
        {
            return true;
        }

        if (TryGetGarrisonBuilderEditedEntityType(out var damageTriggerEntityType)
            && damageTriggerEntityType.Equals(DamageTriggerMetadata.DamageTriggerEntityType, StringComparison.OrdinalIgnoreCase)
            && key.Equals(DamageTriggerMetadata.TriggerBelowPercentPropertyKey, StringComparison.OrdinalIgnoreCase)
            && !DamageTriggerMetadata.ParseTriggerBelowThreshold(_builderPropertyEditorValues))
        {
            return true;
        }

        if (TryGetGarrisonBuilderEditedEntityType(out var damageTriggerAnyDamageEntityType)
            && damageTriggerAnyDamageEntityType.Equals(DamageTriggerMetadata.DamageTriggerEntityType, StringComparison.OrdinalIgnoreCase)
            && (key.Equals(MapLogicMetadata.TrueTimePropertyKey, StringComparison.OrdinalIgnoreCase)
                || key.Equals(DamageTriggerMetadata.AnyDamageCooldownPropertyKey, StringComparison.OrdinalIgnoreCase))
            && !DamageTriggerMetadata.ParseTriggerOnAnyDamage(_builderPropertyEditorValues))
        {
            return true;
        }

        if ((IsEditingGarrisonBuilderBarrierEntity() || IsEditingGarrisonBuilderDirectionalWallEntity())
            && key is "team" or "orientation" or "blockPlayers" or "blockBullets" or "blockIntel" or "blockLeft" or "blockRight" or "allowTeam" or "mode" or "shape")
        {
            return true;
        }

        if (IsEditingGarrisonBuilderDirectionalWallEntity()
            && (BarrierTargetFilterMetadata.IsTargetPropertyKey(key)
                || key is "axis" or "fromLeft" or "fromRight" or "fromTop" or "fromBottom"))
        {
            return true;
        }

        if (IsEditingGarrisonBuilderControlPointEntity())
        {
            if (!IsGarrisonBuilderControlPointOverrideEnabled()
                && (key.Equals(ControlPointInitialOwnershipMetadata.PropertyKey, StringComparison.OrdinalIgnoreCase)
                    || key.Equals(ControlPointLockDependencyMetadata.LockedWhenCpPropertyKey, StringComparison.OrdinalIgnoreCase)
                    || key.Equals(ControlPointLockDependencyMetadata.LockedWhenTeamPropertyKey, StringComparison.OrdinalIgnoreCase)
                    || key.Equals(ControlPointLockDependencyMetadata.UnlockedWhenCpPropertyKey, StringComparison.OrdinalIgnoreCase)
                    || key.Equals(ControlPointLockDependencyMetadata.UnlockedWhenTeamPropertyKey, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (IsGarrisonBuilderControlPointSectionProperty(key))
            {
                return !IsGarrisonBuilderControlPointOverrideEnabled();
            }
        }

        if (!IsEditingGarrisonBuilderSpawnEntity())
        {
            return false;
        }

        if (key.Equals("objectiveIndex", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IsGarrisonBuilderEditedSpawnForwardEnabled())
        {
            return key.Equals(ForwardSpawnPriorityMetadata.PropertyKey, StringComparison.OrdinalIgnoreCase)
                || key.Equals("linkObjective", StringComparison.OrdinalIgnoreCase)
                || key.Equals(ForwardSpawnMetadata.UseWhenPropertyKey, StringComparison.OrdinalIgnoreCase)
                || key.Equals(MapLogicMetadata.LogicSignalPropertyKey, StringComparison.OrdinalIgnoreCase);
        }

        if (IsGarrisonBuilderSpawnUsingLogicSignal())
        {
            return key.Equals("linkObjective", StringComparison.OrdinalIgnoreCase)
                || key.Equals(ForwardSpawnMetadata.UseWhenPropertyKey, StringComparison.OrdinalIgnoreCase);
        }

        if (IsEditingGarrisonBuilderControlPointEntity() && IsGarrisonBuilderControlPointOverrideEnabled())
        {
            if (key.Equals(ControlPointLockDependencyMetadata.LockedWhenCpPropertyKey, StringComparison.OrdinalIgnoreCase)
                || key.Equals(ControlPointLockDependencyMetadata.LockedWhenTeamPropertyKey, StringComparison.OrdinalIgnoreCase)
                || key.Equals(ControlPointLockDependencyMetadata.UnlockedWhenCpPropertyKey, StringComparison.OrdinalIgnoreCase)
                || key.Equals(ControlPointLockDependencyMetadata.UnlockedWhenTeamPropertyKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (IsEditingGarrisonBuilderControlPointEntity()
            && (!IsGarrisonBuilderControlPointOverrideEnabled()
                || key.Equals(MapLogicMetadata.LockedWhenLogicPropertyKey, StringComparison.OrdinalIgnoreCase)
                || key.Equals(MapLogicMetadata.UnlockedWhenLogicPropertyKey, StringComparison.OrdinalIgnoreCase)))
        {
            if (key.Equals(MapLogicMetadata.LockedWhenLogicPropertyKey, StringComparison.OrdinalIgnoreCase)
                || key.Equals(MapLogicMetadata.UnlockedWhenLogicPropertyKey, StringComparison.OrdinalIgnoreCase))
            {
                return !IsGarrisonBuilderControlPointOverrideEnabled();
            }
        }

        return false;
    }

    private bool IsGarrisonBuilderEditedSpawnForwardEnabled()
    {
        return _builderPropertyEditorValues.TryGetValue("forward", out var forward)
            && IsGarrisonBuilderBooleanPropertyChecked(forward);
    }

    private void SyncGarrisonBuilderSpawnPropertyEditorFields()
    {
        if (!IsEditingGarrisonBuilderSpawnEntity())
        {
            return;
        }

        if (IsGarrisonBuilderEditedSpawnForwardEnabled())
        {
            if (!_builderPropertyEditorValues.ContainsKey("linkObjective"))
            {
                if (CustomMapBuilderEntityCatalog.TryGetDefinition("spawn", out var definition)
                    && definition.DefaultProperties.TryGetValue("linkObjective", out var defaultLink)
                    && !string.IsNullOrWhiteSpace(defaultLink))
                {
                    _builderPropertyEditorValues["linkObjective"] = defaultLink;
                }
                else
                {
                    _builderPropertyEditorValues["linkObjective"] = string.Empty;
                }
            }

            if (!_builderPropertyEditorValues.ContainsKey(ForwardSpawnPriorityMetadata.PropertyKey))
            {
                if (TryGetGarrisonBuilderEditedEntity(out var spawnEntity))
                {
                    var priority = ForwardSpawnMetadata.ParsePriority(spawnEntity.Properties);
                    if (priority == ForwardSpawnPriorityMetadata.MinPriority
                        && TryGetLegacyForwardSpawnObjectiveIndex(spawnEntity.Type, out var legacyPriority))
                    {
                        priority = legacyPriority;
                    }

                    _builderPropertyEditorValues[ForwardSpawnPriorityMetadata.PropertyKey] =
                        ForwardSpawnPriorityMetadata.ToPropertyValue(priority);
                }
                else
                {
                    _builderPropertyEditorValues[ForwardSpawnPriorityMetadata.PropertyKey] =
                        ForwardSpawnPriorityMetadata.DefaultPropertyValue;
                }
            }

            if (!_builderPropertyEditorValues.ContainsKey(MapLogicMetadata.LogicSignalPropertyKey))
            {
                _builderPropertyEditorValues[MapLogicMetadata.LogicSignalPropertyKey] = string.Empty;
            }

            if (!_builderPropertyEditorValues.ContainsKey(ForwardSpawnMetadata.UseWhenPropertyKey))
            {
                if (CustomMapBuilderEntityCatalog.TryGetDefinition("spawn", out var definition)
                    && definition.DefaultProperties.TryGetValue(ForwardSpawnMetadata.UseWhenPropertyKey, out var defaultUseWhen)
                    && !string.IsNullOrWhiteSpace(defaultUseWhen))
                {
                    _builderPropertyEditorValues[ForwardSpawnMetadata.UseWhenPropertyKey] = defaultUseWhen;
                }
                else
                {
                    _builderPropertyEditorValues[ForwardSpawnMetadata.UseWhenPropertyKey] = ForwardSpawnMetadata.DefaultUseWhenValue;
                }
            }

            return;
        }

        _builderPropertyEditorValues.Remove("linkObjective");
        _builderPropertyEditorValues.Remove("objectiveIndex");
        _builderPropertyEditorValues.Remove(ForwardSpawnPriorityMetadata.PropertyKey);
        _builderPropertyEditorValues.Remove(ForwardSpawnMetadata.UseWhenPropertyKey);
        CancelGarrisonBuilderObjectiveMapPick();
    }

    private void ClampGarrisonBuilderPropertyEditorScrollIndex()
    {
        if (_builderPropertyTarget == GarrisonBuilderPropertyTarget.None)
        {
            return;
        }

        var bounds = GetGarrisonBuilderPropertyEditorBounds();
        var rows = GetGarrisonBuilderPropertyRows();
        var visibleRows = GetGarrisonBuilderPropertyVisibleRows(bounds);
        _builderPropertyScrollIndex = Math.Clamp(_builderPropertyScrollIndex, 0, Math.Max(0, rows.Count - visibleRows));
    }

    private bool IsEditingGarrisonBuilderSpawnEntity()
    {
        return TryGetGarrisonBuilderEditedEntityType(out var entityType)
            && entityType.Equals("spawn", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsEditingGarrisonBuilderBarrierEntity()
    {
        return TryGetGarrisonBuilderEditedEntityType(out var entityType)
            && entityType.Equals("barrier", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsEditingGarrisonBuilderTeleportEntity()
    {
        return TryGetGarrisonBuilderEditedEntityType(out var entityType)
            && entityType.Equals(TeleportMetadata.TeleportEntityType, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsEditingGarrisonBuilderDirectionalWallEntity()
    {
        return TryGetGarrisonBuilderEditedEntityType(out var entityType)
            && entityType.Equals("directionalWall", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsEditingGarrisonBuilderControlPointEntity()
    {
        return TryGetGarrisonBuilderEditedEntityType(out var entityType)
            && ControlPointOwnershipResolver.IsControlPointEntity(entityType);
    }

    private bool TryGetGarrisonBuilderEditedEntityType(out string entityType)
    {
        if (_builderPropertyTarget == GarrisonBuilderPropertyTarget.PlacementEntity)
        {
            entityType = _builderSelectedEntityType;
            return entityType.Length > 0;
        }

        if (_builderPropertyTarget == GarrisonBuilderPropertyTarget.SelectedMapEntity
            && _builderSelectedEntityIndex >= 0
            && _builderSelectedEntityIndex < _builderEntities.Count)
        {
            entityType = _builderEntities[_builderSelectedEntityIndex].Type;
            return true;
        }

        entityType = string.Empty;
        return false;
    }

    private static bool IsGarrisonBuilderObjectiveMapPickProperty(string key)
    {
        return key.Equals("linkObjective", StringComparison.OrdinalIgnoreCase)
            || key.Equals(ControlPointLockDependencyMetadata.LockedWhenCpPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(ControlPointLockDependencyMetadata.UnlockedWhenCpPropertyKey, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGarrisonBuilderControlPointSectionProperty(string key)
    {
        return key.Equals(ControlPointLockDependencyMetadata.LockedWhenSectionKey, StringComparison.Ordinal)
            || key.Equals(ControlPointLockDependencyMetadata.UnlockedWhenSectionKey, StringComparison.Ordinal);
    }

    private static bool IsGarrisonBuilderSpawnUseWhenProperty(string key)
    {
        return key.Equals(ForwardSpawnMetadata.UseWhenPropertyKey, StringComparison.OrdinalIgnoreCase);
    }

    private static float GetGarrisonBuilderPropertyRowRelativeTextScale()
    {
        return 1f;
    }

    private float GetGarrisonBuilderPropertyRowTextScale()
    {
        return GetGarrisonBuilderRelativeBitmapFontScale(GetGarrisonBuilderPropertyRowRelativeTextScale());
    }

    private void DrawGarrisonBuilderPropertyRow(Rectangle rowBounds, string key, string value, MouseState mouse, float textScale)
    {
        if (IsGarrisonBuilderControlPointSectionProperty(key))
        {
            var sectionLabel = FormatGarrisonBuilderPropertyRowLabel(key, value);
            var sectionY = rowBounds.Y + MathF.Max(2f, ((rowBounds.Height - MeasureBitmapFontHeight(textScale)) * 0.5f) - 1f);
            DrawBitmapFontText(sectionLabel, new Vector2(rowBounds.X + 6f, sectionY), new Color(220, 200, 160), textScale);
            return;
        }

        var label = FormatGarrisonBuilderPropertyRowLabel(key, value);
        var isBarrierTargetFilter = BarrierTargetFilterMetadata.IsTargetPropertyKey(key)
            && IsEditingGarrisonBuilderBarrierEntity();
        var isDirectionalWallCyclicProperty = IsEditingGarrisonBuilderDirectionalWallEntity()
            && (key.Equals(DirectionalWallConfiguration.PassDirectionPropertyKey, StringComparison.OrdinalIgnoreCase)
                || key.Equals(DirectionalWallConfiguration.PlayersPropertyKey, StringComparison.OrdinalIgnoreCase)
                || key.Equals(DirectionalWallConfiguration.ProjectilesPropertyKey, StringComparison.OrdinalIgnoreCase));
        var isNodePriority = IsGarrisonBuilderNodePriorityPropertyRow(key);
        var isTriggerBelowPercent = IsGarrisonBuilderTriggerBelowPercentPropertyRow(key);
        var isCustomSpriteZOrder = IsGarrisonBuilderCustomSpriteZOrderPropertyRow(key);
        var isScoreTriggerValue = IsGarrisonBuilderScoreTriggerValuePropertyRow(key);
        var isCapTimeMultiplier = IsGarrisonBuilderCapTimeMultiplierPropertyRow(key);
        var isBoolean = IsGarrisonBuilderBooleanProperty(key, value) || isBarrierTargetFilter || isDirectionalWallCyclicProperty;
        var checkboxBounds = isBoolean ? GetGarrisonBuilderPropertyCheckboxBounds(rowBounds) : Rectangle.Empty;
        var hasClearableConnection = GarrisonBuilderPropertyHasClearableConnection(key, value);
        var clearBounds = hasClearableConnection ? GetGarrisonBuilderPropertyClearButtonBounds(rowBounds) : Rectangle.Empty;
        var capTimeSliderBounds = isCapTimeMultiplier
            ? GetGarrisonBuilderCapTimeMultiplierSliderBounds(rowBounds, value, textScale, hasClearableConnection)
            : Rectangle.Empty;
        var hovered = rowBounds.Contains(mouse.Position);
        if (_builderUseModernUi)
        {
            if (hovered)
            {
                _spriteBatch.Draw(_pixel, rowBounds, new Color(77, 69, 63));
            }

            if (isNodePriority)
            {
                DrawGarrisonBuilderNodePriorityPropertyRow(rowBounds, value, mouse, textScale, hovered);
                return;
            }

            if (isTriggerBelowPercent)
            {
                DrawGarrisonBuilderTriggerBelowPercentPropertyRow(rowBounds, value, mouse, textScale, hovered);
                return;
            }

            if (isCustomSpriteZOrder)
            {
                DrawGarrisonBuilderCustomSpriteZOrderPropertyRow(rowBounds, value, mouse, textScale, hovered);
                return;
            }

            if (isScoreTriggerValue)
            {
                DrawGarrisonBuilderScoreTriggerValuePropertyRow(rowBounds, value, mouse, textScale, hovered);
                return;
            }

            if (isCapTimeMultiplier)
            {
                DrawGarrisonBuilderCapTimeMultiplierPropertyRow(
                    rowBounds,
                    value,
                    mouse,
                    textScale,
                    hovered,
                    capTimeSliderBounds,
                    clearBounds,
                    hasClearableConnection);
                return;
            }

            var labelMaxWidth = isBoolean
                ? checkboxBounds.X - rowBounds.X - 10f
                : hasClearableConnection
                    ? clearBounds.X - rowBounds.X - 10f
                    : rowBounds.Width - 12f;
            var displayLabel = TruncateGarrisonBuilderPropertyLabel(label, labelMaxWidth, textScale);
            var textY = rowBounds.Y + MathF.Max(2f, ((rowBounds.Height - MeasureBitmapFontHeight(textScale)) * 0.5f) - 1f);
            var labelColor = ShouldDimGarrisonBuilderDamageTriggerModeLabel(key)
                ? new Color(140, 140, 140)
                : Color.White;
            DrawBitmapFontText(displayLabel, new Vector2(rowBounds.X + 6f, textY), labelColor, textScale);
            if (isBoolean)
            {
                DrawGarrisonBuilderPropertyCheckbox(
                    checkboxBounds,
                    IsGarrisonBuilderPropertyRowChecked(key, value, isBarrierTargetFilter, isDirectionalWallCyclicProperty),
                    hovered || checkboxBounds.Contains(mouse.Position));
            }

            if (hasClearableConnection)
            {
                DrawGarrisonBuilderPropertyClearButton(
                    clearBounds,
                    hovered || clearBounds.Contains(mouse.Position));
            }

            return;
        }

        _spriteBatch.Draw(_pixel, rowBounds, hovered ? new Color(210, 210, 210) : new Color(184, 184, 184));
        if (isNodePriority)
        {
            DrawGarrisonBuilderNodePriorityPropertyRow(rowBounds, value, mouse, textScale, hovered);
            return;
        }

        if (isTriggerBelowPercent)
        {
            DrawGarrisonBuilderTriggerBelowPercentPropertyRow(rowBounds, value, mouse, textScale, hovered);
            return;
        }

        if (isCustomSpriteZOrder)
        {
            DrawGarrisonBuilderCustomSpriteZOrderPropertyRow(rowBounds, value, mouse, textScale, hovered);
            return;
        }

        if (isScoreTriggerValue)
        {
            DrawGarrisonBuilderScoreTriggerValuePropertyRow(rowBounds, value, mouse, textScale, hovered);
            return;
        }

        if (isCapTimeMultiplier)
        {
            DrawGarrisonBuilderCapTimeMultiplierPropertyRow(
                rowBounds,
                value,
                mouse,
                textScale,
                hovered,
                capTimeSliderBounds,
                clearBounds,
                hasClearableConnection);
            return;
        }

        var legacyLabelMaxWidth = isBoolean
            ? checkboxBounds.X - rowBounds.X - 8f
            : hasClearableConnection
                ? clearBounds.X - rowBounds.X - 8f
                : rowBounds.Width - 8f;
        var legacyLabel = TruncateGarrisonBuilderPropertyLabel(label, legacyLabelMaxWidth, textScale, useLegacyMeasure: true);
        var legacyLabelColor = ShouldDimGarrisonBuilderDamageTriggerModeLabel(key)
            ? new Color(120, 120, 120)
            : Color.Black;
        DrawGarrisonBuilderText(legacyLabel, rowBounds.Location.ToVector2() + new Vector2(4f, 3f), legacyLabelColor, textScale);
        if (isBoolean)
        {
            DrawGarrisonBuilderPropertyCheckbox(
                checkboxBounds,
                IsGarrisonBuilderPropertyRowChecked(key, value, isBarrierTargetFilter, isDirectionalWallCyclicProperty),
                hovered || checkboxBounds.Contains(mouse.Position),
                legacy: true);
        }

        if (hasClearableConnection)
        {
            DrawGarrisonBuilderPropertyClearButton(
                clearBounds,
                hovered || clearBounds.Contains(mouse.Position),
                legacy: true);
        }
    }

    private static bool IsGarrisonBuilderPropertyRowChecked(string key, string value, bool isBarrierTargetFilter, bool isDirectionalWallCyclicProperty)
    {
        if (isBarrierTargetFilter)
        {
            return BarrierTargetFilterMetadata.Parse(value) == BarrierTargetFilter.Block;
        }

        if (key.Equals(DirectionalWallConfiguration.PassDirectionPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (isDirectionalWallCyclicProperty)
        {
            return value.Equals(DirectionalWallConfiguration.AffectValue, StringComparison.OrdinalIgnoreCase);
        }

        return IsGarrisonBuilderBooleanPropertyChecked(value);
    }

    private string TruncateGarrisonBuilderPropertyLabel(string label, float maxWidth, float scale, bool useLegacyMeasure = false)
    {
        if (maxWidth <= 0f)
        {
            return label;
        }

        var measuredWidth = useLegacyMeasure
            ? MeasureGarrisonBuilderText(label, scale).X
            : MeasureBitmapFontWidth(label, scale);
        if (measuredWidth <= maxWidth)
        {
            return label;
        }

        const string ellipsis = "...";
        for (var length = label.Length - 1; length > 0; length -= 1)
        {
            var candidate = string.Concat(label.AsSpan(0, length), ellipsis);
            measuredWidth = useLegacyMeasure
                ? MeasureGarrisonBuilderText(candidate, scale).X
                : MeasureBitmapFontWidth(candidate, scale);
            if (measuredWidth <= maxWidth)
            {
                return candidate;
            }
        }

        return ellipsis;
    }

    private static Rectangle GetGarrisonBuilderPropertyCheckboxBounds(Rectangle rowBounds)
    {
        const int checkboxSize = 14;
        const int checkboxMargin = 8;
        return new Rectangle(
            rowBounds.Right - checkboxMargin - checkboxSize,
            rowBounds.Y + Math.Max(2, (rowBounds.Height - checkboxSize) / 2),
            checkboxSize,
            checkboxSize);
    }

    private static Rectangle GetGarrisonBuilderPropertyClearButtonBounds(Rectangle rowBounds)
    {
        const int buttonSize = 14;
        const int buttonMargin = 8;
        return new Rectangle(
            rowBounds.Right - buttonMargin - buttonSize,
            rowBounds.Y + Math.Max(2, (rowBounds.Height - buttonSize) / 2),
            buttonSize,
            buttonSize);
    }

    private static bool IsGarrisonBuilderClearableConnectionProperty(string key)
    {
        return IsGarrisonBuilderObjectiveMapPickProperty(key)
            || IsGarrisonBuilderLogicMapPickProperty(key)
            || IsGarrisonBuilderEntityMapPickProperty(key)
            || key.Equals(ControlPointCapTimeMultiplierMetadata.PropertyKey, StringComparison.OrdinalIgnoreCase);
    }

    private bool GarrisonBuilderPropertyHasClearableConnection(string key, string value)
    {
        if (key.Equals(ControlPointCapTimeMultiplierMetadata.PropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return ControlPointCapTimeMultiplierMetadata.IsCustom(_builderPropertyEditorValues);
        }

        return IsGarrisonBuilderClearableConnectionProperty(key)
            && !string.IsNullOrWhiteSpace(value);
    }

    private bool TryClearGarrisonBuilderPropertyConnectionClick(
        string key,
        string value,
        Rectangle rowBounds,
        Point position)
    {
        if (!GarrisonBuilderPropertyHasClearableConnection(key, value))
        {
            return false;
        }

        if (!GetGarrisonBuilderPropertyClearButtonBounds(rowBounds).Contains(position))
        {
            return false;
        }

        ClearGarrisonBuilderPropertyConnection(key);
        return true;
    }

    private void ClearGarrisonBuilderPropertyConnection(string key)
    {
        if (key.Equals(ControlPointCapTimeMultiplierMetadata.PropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntity(out var controlPointEntity))
        {
            ControlPointCapTimeMultiplierMetadata.ClearCustom(_builderPropertyEditorValues);
            SyncGarrisonBuilderControlPointCapTimeMultiplierField(controlPointEntity);
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            MarkGarrisonBuilderPropertyEditorChanged();
            return;
        }

        _builderPropertyEditorValues[key] = string.Empty;
        if (_builderObjectiveMapPickActive
            && key.Equals(_builderObjectiveMapPickTargetPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            CancelGarrisonBuilderObjectiveMapPick();
        }

        if (_builderLogicMapPickActive
            && key.Equals(_builderLogicMapPickTargetPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            CancelGarrisonBuilderLogicMapPick();
        }

        if (_builderEntityMapPickActive
            && key.Equals(_builderEntityMapPickTargetPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            CancelGarrisonBuilderEntityMapPick();
        }

        ApplyGarrisonBuilderPropertyEditorLivePreview();
        MarkGarrisonBuilderPropertyEditorChanged();
        _builderStatus = "connection cleared";
    }

    private void DrawGarrisonBuilderPropertyClearButton(Rectangle bounds, bool highlighted, bool legacy = false)
    {
        var border = legacy
            ? highlighted ? new Color(120, 40, 40) : new Color(80, 20, 20)
            : highlighted ? new Color(220, 120, 110) : new Color(120, 90, 86);
        var fill = legacy
            ? highlighted ? new Color(255, 220, 220) : new Color(245, 235, 235)
            : highlighted ? new Color(92, 52, 50) : new Color(52, 47, 42);
        var cross = legacy ? new Color(140, 20, 20) : new Color(240, 150, 140);

        _spriteBatch.Draw(_pixel, bounds, fill);
        DrawGarrisonBuilderRectangleOutline(bounds, border);
        var inset = 4;
        var left = bounds.X + inset;
        var top = bounds.Y + inset;
        var right = bounds.Right - inset;
        var bottom = bounds.Bottom - inset;
        DrawGarrisonBuilderLine(new Vector2(left, top), new Vector2(right, bottom), cross);
        DrawGarrisonBuilderLine(new Vector2(right, top), new Vector2(left, bottom), cross);
    }

    private void DrawGarrisonBuilderPropertyCheckbox(Rectangle bounds, bool isChecked, bool highlighted, bool legacy = false)
    {
        var border = legacy
            ? highlighted ? new Color(40, 40, 40) : new Color(20, 20, 20)
            : highlighted ? new Color(213, 205, 188) : new Color(120, 110, 98);
        var fill = legacy
            ? new Color(250, 250, 250)
            : new Color(52, 47, 42);
        var checkColor = legacy ? new Color(30, 120, 40) : new Color(120, 220, 130);

        _spriteBatch.Draw(_pixel, bounds, fill);
        DrawGarrisonBuilderRectangleOutline(bounds, border);
        if (isChecked)
        {
            var inset = 3;
            var checkLeft = bounds.X + inset;
            var checkTop = bounds.Y + inset;
            var checkRight = bounds.Right - inset;
            var checkBottom = bounds.Bottom - inset;
            DrawGarrisonBuilderLine(new Vector2(checkLeft, checkTop + ((checkBottom - checkTop) * 0.55f)), new Vector2(checkLeft + ((checkRight - checkLeft) * 0.35f), checkBottom), checkColor);
            DrawGarrisonBuilderLine(new Vector2(checkLeft + ((checkRight - checkLeft) * 0.35f), checkBottom), new Vector2(checkRight, checkTop + ((checkBottom - checkTop) * 0.2f)), checkColor);
        }
    }

    private static bool IsGarrisonBuilderBooleanProperty(string key, string value)
    {
        if (key.Equals(DirectionalWallConfiguration.PassDirectionPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(DirectionalWallConfiguration.PlayersPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(DirectionalWallConfiguration.ProjectilesPropertyKey, StringComparison.OrdinalIgnoreCase)
            || BarrierTargetFilterMetadata.IsTargetPropertyKey(key)
            || key.Equals(ControlPointInitialOwnershipMetadata.PropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(ControlPointIndexMetadata.PropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(ForwardSpawnPriorityMetadata.PropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.NodePriorityPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.SignalPriorityPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.CountdownSecondsPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicSignalMetadata.PeriodPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.TrueTimePropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.FalseTimePropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicSignalMetadata.SignalPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicSignalMetadata.DetectPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(ControlPointCapTimeMultiplierMetadata.PropertyKey, StringComparison.OrdinalIgnoreCase)
            || IsGarrisonBuilderObjectiveMapPickProperty(key)
            || IsGarrisonBuilderSpawnUseWhenProperty(key))
        {
            return false;
        }

        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGarrisonBuilderBooleanPropertyChecked(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    private string FormatGarrisonBuilderPropertyRowLabel(string key, string value)
    {
        if (TryGetGarrisonBuilderEditedEntityType(out var customSpriteType)
            && CustomMapCustomSpriteMetadata.IsCustomSpriteEntityType(customSpriteType)
            && IsGarrisonBuilderCustomSpritePropertyKey(key))
        {
            return GetGarrisonBuilderCustomSpritePropertyDisplayLabel(key, value);
        }

        if (IsGarrisonBuilderLogicMapPickProperty(key)
            || IsGarrisonBuilderEntityMapPickProperty(key)
            || key.Equals(MapLogicMetadata.GateTypePropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.RequiredOwnerPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicSignalMetadata.SignalPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicSignalMetadata.DetectPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicSignalMetadata.PeriodPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.ActivatorBehaviorPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.ActivateOnStartPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.CountdownSecondsPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.TriggerOnStartPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.DelayedTruePropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.DelayedFalsePropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.TrueTimePropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.FalseTimePropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.InitialValuePropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.AutostartPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.StartWhenPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicMetadata.EndWhenPropertyKey, StringComparison.OrdinalIgnoreCase)
            || (key.Equals("linkObjective", StringComparison.OrdinalIgnoreCase)
                && TryGetGarrisonBuilderEditedEntityType(out var linkEntity)
                && linkEntity.Equals(MapLogicMetadata.CpTriggerEntityType, StringComparison.OrdinalIgnoreCase)))
        {
            return FormatGarrisonBuilderLogicPropertyRowLabel(key, value);
        }

        if (key.Equals("linkObjective", StringComparison.OrdinalIgnoreCase))
        {
            var summary = DescribeGarrisonBuilderObjectiveLink(value);
            return $"Objective: {summary}";
        }

        if (key.Equals(ForwardSpawnPriorityMetadata.PropertyKey, StringComparison.OrdinalIgnoreCase)
            && IsEditingGarrisonBuilderSpawnEntity())
        {
            return $"{ForwardSpawnPriorityMetadata.GetDisplayLabel(value)}";
        }

        if (IsGarrisonBuilderSpawnUseWhenProperty(key))
        {
            return $"Use when: {ForwardSpawnMetadata.GetUseConditionDisplayLabel(value)}";
        }

        if (BarrierTargetFilterMetadata.IsTargetPropertyKey(key)
            && IsEditingGarrisonBuilderBarrierEntity())
        {
            return FormatGarrisonBuilderBarrierTargetFilterLabel(key, value);
        }

        if (key.Equals(DirectionalWallConfiguration.PassDirectionPropertyKey, StringComparison.OrdinalIgnoreCase)
            && IsEditingGarrisonBuilderDirectionalWallEntity())
        {
            return $"Pass direction: {DirectionalWallConfiguration.GetPassDirectionDisplayLabel(value)}";
        }

        if (key.Equals(DirectionalWallConfiguration.PlayersPropertyKey, StringComparison.OrdinalIgnoreCase)
            && IsEditingGarrisonBuilderDirectionalWallEntity())
        {
            return $"Players: {DirectionalWallConfiguration.GetAffectDisplayLabel(value)}";
        }

        if (key.Equals(DirectionalWallConfiguration.ProjectilesPropertyKey, StringComparison.OrdinalIgnoreCase)
            && IsEditingGarrisonBuilderDirectionalWallEntity())
        {
            return $"Projectiles: {DirectionalWallConfiguration.GetAffectDisplayLabel(value)}";
        }

        if (DamageTriggerMetadata.IsExclusiveModePropertyKey(key)
            && TryGetGarrisonBuilderEditedEntityType(out var damageTriggerLabelEntity)
            && damageTriggerLabelEntity.Equals(DamageTriggerMetadata.DamageTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            return DamageTriggerMetadata.GetExclusiveModeDisplayLabel(key);
        }

        if (key.Equals(DamageTriggerMetadata.AffectedByTeamPropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var damageTriggerAffectedTeamEntity)
            && damageTriggerAffectedTeamEntity.Equals(DamageTriggerMetadata.DamageTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            return $"Affected by team: {DamageTriggerMetadata.GetAffectedByTeamDisplayLabel(value)}";
        }

        if (key.Equals(DamageTriggerMetadata.TriggerBelowPercentPropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var damageTriggerPercentEntity)
            && damageTriggerPercentEntity.Equals(DamageTriggerMetadata.DamageTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            return $"Trigger below: {DamageTriggerMetadata.GetTriggerBelowDisplayLabel(value)}";
        }

        if (key.Equals(DamageTriggerMetadata.AnyDamageCooldownPropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var damageTriggerCooldownEntity)
            && damageTriggerCooldownEntity.Equals(DamageTriggerMetadata.DamageTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            return $"Cooldown (sec): {DamageTriggerMetadata.ToAnyDamageCooldownPropertyValue(DamageTriggerMetadata.ParseAnyDamageCooldownSeconds(_builderPropertyEditorValues))}";
        }

        if (key.Equals(MapLogicMetadata.TrueTimePropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var damageTriggerTrueTimeEntity)
            && damageTriggerTrueTimeEntity.Equals(DamageTriggerMetadata.DamageTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            return $"TRUE time (sec): {DamageTriggerMetadata.ToAnyDamageTrueTimePropertyValue(DamageTriggerMetadata.ParseAnyDamageTrueTimeSeconds(_builderPropertyEditorValues))}";
        }

        if (IsGarrisonBuilderBooleanProperty(key, value))
        {
            return FormatGarrisonBuilderPropertyKeyLabel(key);
        }

        if (key.Equals("team", StringComparison.OrdinalIgnoreCase)
            && IsEditingGarrisonBuilderTeleportEntity())
        {
            return $"Team: {TeleportMetadata.GetTeamDisplayLabel(value)}";
        }

        if (key.Equals("team", StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var playerTriggerLabelEntity)
            && playerTriggerLabelEntity.Equals(MapLogicMetadata.PlayerTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            return $"Team: {PlayerTriggerMetadata.GetTeamDisplayLabel(value)}";
        }

        if (key.Equals("team", StringComparison.OrdinalIgnoreCase))
        {
            return $"Team: {FormatGarrisonBuilderTeamPropertyLabel(value)}";
        }

        if (key.Equals(TeleportMetadata.TeleportExitPropertyKey, StringComparison.OrdinalIgnoreCase)
            && IsEditingGarrisonBuilderTeleportEntity())
        {
            return FormatGarrisonBuilderLogicPropertyRowLabel(key, value);
        }

        if (key.Equals(ControlPointIndexMetadata.PropertyKey, StringComparison.OrdinalIgnoreCase)
            && IsEditingGarrisonBuilderControlPointEntity())
        {
            return $"Control point: {ControlPointIndexMetadata.GetDisplayLabel(value)}";
        }

        if (key.Equals(ControlPointInitialOwnershipMetadata.PropertyKey, StringComparison.OrdinalIgnoreCase)
            && IsEditingGarrisonBuilderControlPointEntity())
        {
            return $"Initial ownership: {ControlPointInitialOwnershipMetadata.GetDisplayLabel(value)}";
        }

        if (key.Equals(ControlPointMapSettingsMetadata.OverrideInitialCpsPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return "Override CP logic";
        }

        if (key.Equals(ScrMapSettingsMetadata.ShowControlPointsPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return "Show CPs";
        }

        if (key.Equals(ScrMapSettingsMetadata.ScoreToWinPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Score to win: {ScrMapSettingsMetadata.ClampScore(ScrMapSettingsMetadata.StepScorePropertyValue(value, 0))}";
        }

        if (key.Equals(ScrMapSettingsMetadata.WinWhenScorePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Win when score: {ScrMapSettingsMetadata.GetWinWhenScoreDisplayLabel(value)}";
        }

        if (key.Equals(ScrMapSettingsMetadata.RoundEndWinPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Round-end win: {ScrMapSettingsMetadata.GetRoundEndWinDisplayLabel(value)}";
        }

        if (key.Equals(ScrMapSettingsMetadata.RedStartingScorePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Red starting score: {ScrMapSettingsMetadata.ClampScore(ScrMapSettingsMetadata.StepScorePropertyValue(value, 0))}";
        }

        if (key.Equals(ScrMapSettingsMetadata.BlueStartingScorePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Blue starting score: {ScrMapSettingsMetadata.ClampScore(ScrMapSettingsMetadata.StepScorePropertyValue(value, 0))}";
        }

        if (key.Equals(ControlPointInitialLockStateMetadata.PropertyKey, StringComparison.OrdinalIgnoreCase)
            && IsEditingGarrisonBuilderControlPointEntity())
        {
            return $"Initial state: {ControlPointInitialLockStateMetadata.GetDisplayLabel(value)}";
        }

        if (key.Equals(ControlPointLockDependencyMetadata.LockedWhenSectionKey, StringComparison.Ordinal))
        {
            return "Lock when";
        }

        if (key.Equals(ControlPointLockDependencyMetadata.UnlockedWhenSectionKey, StringComparison.Ordinal))
        {
            return "Unlock when";
        }

        if (key.Equals(ControlPointLockDependencyMetadata.LockedWhenCpPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(ControlPointLockDependencyMetadata.UnlockedWhenCpPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            var label = key.Equals(ControlPointLockDependencyMetadata.LockedWhenCpPropertyKey, StringComparison.OrdinalIgnoreCase)
                ? "Control point"
                : "Control point";
            return $"{label}: {DescribeGarrisonBuilderObjectiveLink(value)}";
        }

        if (key.Equals(ControlPointLockDependencyMetadata.LockedWhenTeamPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(ControlPointLockDependencyMetadata.UnlockedWhenTeamPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Team: {ControlPointLockDependencyMetadata.GetTeamDisplayLabel(value)}";
        }

        var displayValue = value.Length > 36 ? string.Concat(value.AsSpan(0, 33), "...") : value;
        return $"{FormatGarrisonBuilderPropertyKeyLabel(key)}: {displayValue}";
    }

    private static string FormatGarrisonBuilderPropertyKeyLabel(string key)
    {
        if (key.Equals(GarrisonBuilderMapPropertyNameKey, StringComparison.Ordinal))
        {
            return "Map name";
        }

        if (key.Equals(GarrisonBuilderMapPropertyVisualScaleKey, StringComparison.Ordinal))
        {
            return "Visual scale";
        }

        if (key.Equals(GarrisonBuilderMapPropertyWalkmaskScaleKey, StringComparison.Ordinal))
        {
            return "Walkmask scale";
        }

        if (key.Equals(PlayerTriggerMetadata.IntelCarriersOnlyPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return "Intel carriers only";
        }

        if (key.Equals(IntelTriggerMetadata.IntelPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return "Intel";
        }

        if (key.Equals(IntelTriggerMetadata.TriggerWhenPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return "Trigger when";
        }

        if (key.Equals(IntelTriggerMetadata.OnPickupPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return "On pickup";
        }

        if (key.Equals(IntelTriggerMetadata.OnDropPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return "On drop";
        }

        if (key.Equals(IntelTriggerMetadata.OnCapturePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return "On capture";
        }

        if (key.Equals(IntelTriggerMetadata.OnResetPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return "On reset";
        }

        if (key.Equals(DamageableMetadata.SentryTargetPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return "Sentry target";
        }

        if (key.Equals(DamageableMetadata.StabbablePropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return "Stabbable";
        }

        if (key.Equals(DamageTriggerMetadata.AnyDamageCooldownPropertyKey, StringComparison.OrdinalIgnoreCase))
        {
            return "Cooldown";
        }

        if (key.Length == 0)
        {
            return key;
        }

        return char.ToUpperInvariant(key[0]) + key[1..];
    }

    private static string FormatGarrisonBuilderTeamPropertyLabel(string team)
    {
        if (team.Equals("red", StringComparison.OrdinalIgnoreCase))
        {
            return "Red";
        }

        if (team.Equals("blue", StringComparison.OrdinalIgnoreCase))
        {
            return "Blue";
        }

        if (team.Equals("neutral", StringComparison.OrdinalIgnoreCase))
        {
            return "Neutral";
        }

        return team;
    }

    private string DescribeGarrisonBuilderObjectiveLink(string linkObjective)
    {
        if (string.IsNullOrWhiteSpace(linkObjective))
        {
            return "none";
        }

        foreach (var entity in _builderEntities)
        {
            if (entity.Type.Equals(linkObjective, StringComparison.OrdinalIgnoreCase))
            {
                return GetGarrisonBuilderObjectiveDisplayName(entity);
            }

            if (entity.Type.Equals("controlPoint", StringComparison.OrdinalIgnoreCase)
                && linkObjective.StartsWith("controlPoint", StringComparison.OrdinalIgnoreCase)
                && GetEntityInt(entity, "index", 0) == ParseGarrisonBuilderTrailingIndex(linkObjective, "controlPoint"))
            {
                return GetGarrisonBuilderObjectiveDisplayName(entity);
            }
        }

        return linkObjective;
    }

    private static string GetGarrisonBuilderObjectiveDisplayName(CustomMapBuilderEntity entity)
    {
        if (CustomMapBuilderEntityCatalog.TryGetDefinition(entity.Type, out var definition))
        {
            var index = GetEntityInt(entity, "index", 0);
            return index > 0 ? $"{definition.Label} #{index}" : definition.Label;
        }

        return entity.Type;
    }

    private string GetGarrisonBuilderPropertyEditorFooterHint()
    {
        if (_builderObjectiveMapPickActive)
        {
            return "Click a control point or KOTH objective on the map. Esc cancels.";
        }

        if (_builderLogicMapPickActive)
        {
            return "Click a logic node output on the map. Esc cancels.";
        }

        if (_builderEntityMapPickActive)
        {
            return "Click an entity on the map. Esc cancels.";
        }

        if (_builderPropertyTarget == GarrisonBuilderPropertyTarget.MapProperties)
        {
            return "Visual scale: layers/background. Walkmask scale: collision. Esc saves.";
        }

        if (IsEditingGarrisonBuilderSpawnEntity())
        {
            return IsGarrisonBuilderEditedSpawnForwardEnabled()
                ? "Use checkboxes, click Team/Use when to cycle, click Objective to pick on map. Esc closes."
                : "Use checkboxes, click Team to cycle. Esc closes.";
        }

        return "Use checkboxes, click Team to cycle, other values to edit. Esc closes.";
    }

    private void TryApplyDefaultGarrisonBuilderPlacementObjectiveLink()
    {
        var bestIndex = -1;
        var bestDistanceSquared = float.MaxValue;
        for (var index = 0; index < _builderEntities.Count; index += 1)
        {
            var entity = _builderEntities[index];
            if (IsGarrisonBuilderEntityHidden(index)
                || !IsGarrisonBuilderLinkableObjectiveEntity(entity))
            {
                continue;
            }

            if (bestIndex < 0)
            {
                bestIndex = index;
                continue;
            }

            var reference = _builderPlacementPreviewWorld;
            if (_builderPlacementDragging)
            {
                reference = _builderPlacementStartWorld;
            }

            var distanceSquared = Vector2.DistanceSquared(reference, new Vector2(entity.X, entity.Y));
            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                bestIndex = index;
            }
        }

        if (bestIndex < 0)
        {
            return;
        }

        var objective = _builderEntities[bestIndex];
        _builderPropertyEditorValues["linkObjective"] = ResolveGarrisonBuilderObjectiveLinkValue(objective);
        var objectiveIndex = ResolveGarrisonBuilderObjectiveIndexFromEntity(objective);
        if (objectiveIndex > 0)
        {
            _builderPropertyEditorValues["objectiveIndex"] = objectiveIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private void BeginGarrisonBuilderObjectiveMapPick(string targetPropertyKey)
    {
        _builderObjectiveMapPickActive = true;
        _builderObjectiveMapPickTargetPropertyKey = targetPropertyKey;
        _builderStatus = "pick a control point on the map";
    }

    private void CancelGarrisonBuilderObjectiveMapPick()
    {
        _builderObjectiveMapPickActive = false;
        _builderObjectiveMapPickTargetPropertyKey = string.Empty;
    }

    private Rectangle GetGarrisonBuilderObjectiveMapPickPromptBounds()
    {
        var strip = GetModernGarrisonBuilderLayerStripBounds();
        var buttonWidth = BuilderUi(240);
        var buttonHeight = (int)GetGarrisonBuilderMinimumButtonHeight() + BuilderUi(10);
        var x = (BuilderViewportWidth - buttonWidth) / 2;
        var y = strip.Y - buttonHeight - BuilderUi(10);
        return new Rectangle(x, y, buttonWidth, buttonHeight);
    }

    private void DrawGarrisonBuilderObjectiveMapPickPrompt(MouseState mouse)
    {
        if (!_builderObjectiveMapPickActive || !_builderUseModernUi)
        {
            return;
        }

        var bounds = GetGarrisonBuilderObjectiveMapPickPromptBounds();
        var highlighted = bounds.Contains(mouse.Position);
        DrawGarrisonBuilderBrownPanel(bounds);
        DrawBuilderMenuButton(bounds, "Pick a control point", highlighted);
    }

    private bool TryGetGarrisonBuilderSpawnObjectiveLink(out string linkObjective)
    {
        linkObjective = string.Empty;
        if (!_builderPropertyEditorValues.TryGetValue("linkObjective", out var value)
            || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        linkObjective = value.Trim();
        return true;
    }

    private bool TryPickGarrisonBuilderObjectiveAtWorld(Vector2 world)
    {
        var bestIndex = -1;
        var bestArea = float.MaxValue;
        for (var index = 0; index < _builderEntities.Count; index += 1)
        {
            var entity = _builderEntities[index];
            if (IsGarrisonBuilderEntityHidden(index)
                || !IsGarrisonBuilderLinkableObjectiveEntity(entity))
            {
                continue;
            }

            if (!TryGetGarrisonBuilderEntityPickBounds(entity, out var bounds)
                || !bounds.Contains(world.X, world.Y))
            {
                continue;
            }

            var area = bounds.Width * bounds.Height;
            if (area < bestArea)
            {
                bestArea = area;
                bestIndex = index;
            }
        }

        if (bestIndex < 0)
        {
            var pickRadius = MathF.Max(12f, 24f / (_builderUseModernUi ? _builderZoom : 1f));
            var pickRadiusSquared = pickRadius * pickRadius;
            var bestDistanceSquared = pickRadiusSquared;
            for (var index = 0; index < _builderEntities.Count; index += 1)
            {
                var entity = _builderEntities[index];
                if (IsGarrisonBuilderEntityHidden(index)
                    || !IsGarrisonBuilderLinkableObjectiveEntity(entity))
                {
                    continue;
                }

                var distanceSquared = Vector2.DistanceSquared(GetGarrisonBuilderEntityLinkAnchor(entity), world);
                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    bestIndex = index;
                }
            }
        }

        if (bestIndex < 0)
        {
            return false;
        }

        ApplyGarrisonBuilderObjectiveMapPick(bestIndex);
        return true;
    }

    private void ApplyGarrisonBuilderObjectiveMapPick(int objectiveEntityIndex)
    {
        var objective = _builderEntities[objectiveEntityIndex];
        var targetKey = string.IsNullOrWhiteSpace(_builderObjectiveMapPickTargetPropertyKey)
            ? "linkObjective"
            : _builderObjectiveMapPickTargetPropertyKey;
        _builderPropertyEditorValues[targetKey] = ResolveGarrisonBuilderObjectiveLinkValue(objective);
        var objectiveIndex = ResolveGarrisonBuilderObjectiveIndexFromEntity(objective);
        if (targetKey.Equals("linkObjective", StringComparison.OrdinalIgnoreCase))
        {
            if (objectiveIndex > 0)
            {
                _builderPropertyEditorValues["objectiveIndex"] = objectiveIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                _builderPropertyEditorValues.Remove("objectiveIndex");
            }

            if (IsEditingGarrisonBuilderSpawnEntity())
            {
                _builderPropertyEditorValues["forward"] = "true";
            }
        }

        CancelGarrisonBuilderObjectiveMapPick();
        ApplyGarrisonBuilderPropertyEditorLivePreview();
        MarkGarrisonBuilderPropertyEditorChanged();
        _builderStatus = $"linked objective: {GetGarrisonBuilderObjectiveDisplayName(objective)}";
    }

    private Vector2 GetGarrisonBuilderEntityLinkAnchor(CustomMapBuilderEntity entity)
    {
        if (TryGetGarrisonBuilderEntityWorldBounds(entity, out var left, out var top, out var width, out var height))
        {
            return new Vector2(left + (width * 0.5f), top + (height * 0.5f));
        }

        return new Vector2(entity.X, entity.Y);
    }

    private bool TryFindNearestGarrisonBuilderControlPoint(CustomMapBuilderEntity source, out CustomMapBuilderEntity controlPoint)
    {
        controlPoint = default!;
        var sourceAnchor = GetGarrisonBuilderEntityLinkAnchor(source);
        var bestDistanceSquared = float.MaxValue;
        var found = false;
        for (var index = 0; index < _builderEntities.Count; index += 1)
        {
            var entity = _builderEntities[index];
            if (!IsGarrisonBuilderControlPointEntity(entity) || IsGarrisonBuilderEntityHidden(index))
            {
                continue;
            }

            var targetAnchor = GetGarrisonBuilderEntityLinkAnchor(entity);
            var distanceSquared = Vector2.DistanceSquared(sourceAnchor, targetAnchor);
            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                controlPoint = entity;
                found = true;
            }
        }

        return found;
    }

    private bool TryFindGarrisonBuilderSpawnObjective(CustomMapBuilderEntity spawn, out CustomMapBuilderEntity objective)
    {
        objective = default!;
        if (TryGetLegacyForwardSpawnObjectiveIndex(spawn.Type, out var legacySpawnSlot))
        {
            var team = spawn.Type.StartsWith("bluespawn", StringComparison.OrdinalIgnoreCase)
                ? PlayerTeam.Blue
                : PlayerTeam.Red;
            return TryFindGarrisonBuilderForwardSpawnObjective(team, legacySpawnSlot, out objective);
        }

        if (!spawn.Type.Equals("spawn", StringComparison.OrdinalIgnoreCase)
            || !GetEntityBool(spawn, "forward", false))
        {
            return false;
        }

        var spawnTeam = GetEntityProperty(spawn, "team", "red");
        if (spawnTeam.Equals("blue", StringComparison.OrdinalIgnoreCase))
        {
            var spawnSlot = ForwardSpawnMetadata.ParsePriority(spawn.Properties);
            if (spawnSlot <= 0)
            {
                spawnSlot = ForwardSpawnMetadata.ParseLinkedControlPointIndex(spawn.Properties);
            }

            if (spawnSlot > 0 && TryFindGarrisonBuilderForwardSpawnObjective(PlayerTeam.Blue, spawnSlot, out objective))
            {
                return true;
            }
        }

        if (TryFindGarrisonBuilderObjectiveByLink(spawn, out objective))
        {
            return true;
        }

        return spawnTeam.Equals("red", StringComparison.OrdinalIgnoreCase)
            && TryFindNearestGarrisonBuilderControlPoint(spawn, out objective);
    }

    private bool TryFindGarrisonBuilderForwardSpawnObjective(
        PlayerTeam team,
        int spawnSlotIndex,
        out CustomMapBuilderEntity objective)
    {
        var linkedIndex = ForwardSpawnMetadata.ResolveForwardSpawnControlPointIndex(
            team,
            spawnSlotIndex,
            CountGarrisonBuilderMapControlPoints());
        return TryFindGarrisonBuilderObjectiveByLink($"controlPoint{linkedIndex}", linkedIndex, out objective);
    }

    private int CountGarrisonBuilderMapControlPoints()
    {
        return ForwardSpawnMetadata.CountMapControlPointsFromEditorEntities(_builderEntities);
    }

    private bool TryFindGarrisonBuilderObjectiveByLink(CustomMapBuilderEntity spawn, out CustomMapBuilderEntity objective)
    {
        var link = GetEntityProperty(spawn, "linkObjective", string.Empty);
        var index = GetEntityInt(spawn, "objectiveIndex", 0);
        return TryFindGarrisonBuilderObjectiveByLink(link, index, out objective);
    }

    private bool TryFindGarrisonBuilderObjectiveByLink(string link, int objectiveIndex, out CustomMapBuilderEntity objective)
    {
        objective = default!;
        if (link.Length == 0 && objectiveIndex > 0)
        {
            link = $"controlPoint{objectiveIndex}";
        }

        if (link.Length == 0)
        {
            return false;
        }

        for (var index = 0; index < _builderEntities.Count; index += 1)
        {
            if (IsGarrisonBuilderEntityHidden(index))
            {
                continue;
            }

            var entity = _builderEntities[index];
            if (entity.Type.Equals(link, StringComparison.OrdinalIgnoreCase))
            {
                objective = entity;
                return true;
            }

            if (entity.Type.Equals("controlPoint", StringComparison.OrdinalIgnoreCase)
                && link.StartsWith("controlPoint", StringComparison.OrdinalIgnoreCase)
                && GetEntityInt(entity, "index", 0) == ParseGarrisonBuilderTrailingIndex(link, "controlPoint"))
            {
                objective = entity;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetLegacyForwardSpawnObjectiveIndex(string type, out int objectiveIndex)
    {
        objectiveIndex = 0;
        if (type.StartsWith("redspawn", StringComparison.OrdinalIgnoreCase) && type.Length > "redspawn".Length)
        {
            return int.TryParse(type["redspawn".Length..], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out objectiveIndex)
                && objectiveIndex > 0;
        }

        if (type.StartsWith("bluespawn", StringComparison.OrdinalIgnoreCase) && type.Length > "bluespawn".Length)
        {
            return int.TryParse(type["bluespawn".Length..], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out objectiveIndex)
                && objectiveIndex > 0;
        }

        return false;
    }

    private static bool IsGarrisonBuilderForwardSpawnEntity(CustomMapBuilderEntity entity)
    {
        if (entity.Type.Equals("spawn", StringComparison.OrdinalIgnoreCase))
        {
            return GetEntityBool(entity, "forward", false);
        }

        return TryGetLegacyForwardSpawnObjectiveIndex(entity.Type, out _);
    }

    private static bool IsGarrisonBuilderLegacyForwardSpawnEntity(CustomMapBuilderEntity entity)
    {
        return TryGetLegacyForwardSpawnObjectiveIndex(entity.Type, out _);
    }

    private static bool IsGarrisonBuilderControlPointEntity(CustomMapBuilderEntity entity)
    {
        return IsGarrisonBuilderLinkableObjectiveEntity(entity);
    }

    private static bool IsGarrisonBuilderLinkableObjectiveEntity(CustomMapBuilderEntity entity)
    {
        var type = entity.Type;
        if (type.StartsWith("controlPoint", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return type.Equals("KothControlPoint", StringComparison.OrdinalIgnoreCase)
            || type.Equals("KothRedControlPoint", StringComparison.OrdinalIgnoreCase)
            || type.Equals("KothBlueControlPoint", StringComparison.OrdinalIgnoreCase)
            || type.Equals("ArenaControlPoint", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveGarrisonBuilderObjectiveLinkValue(CustomMapBuilderEntity entity)
    {
        if (entity.Type.StartsWith("controlPoint", StringComparison.OrdinalIgnoreCase)
            && entity.Type.Length > "controlPoint".Length)
        {
            return entity.Type;
        }

        var index = ResolveGarrisonBuilderObjectiveIndexFromEntity(entity);
        if (index > 0)
        {
            return $"controlPoint{index}";
        }

        return entity.Type;
    }

    private static int ResolveGarrisonBuilderObjectiveIndexFromEntity(CustomMapBuilderEntity entity)
    {
        if (entity.Type.StartsWith("controlPoint", StringComparison.OrdinalIgnoreCase)
            && entity.Type.Length > "controlPoint".Length)
        {
            return ParseGarrisonBuilderTrailingIndex(entity.Type, "controlPoint");
        }

        if (entity.Type.Equals("controlPoint", StringComparison.OrdinalIgnoreCase))
        {
            return GetEntityInt(entity, "index", 0);
        }

        if (entity.Type.Equals("KothControlPoint", StringComparison.OrdinalIgnoreCase)
            || entity.Type.Equals("KothRedControlPoint", StringComparison.OrdinalIgnoreCase)
            || entity.Type.Equals("KothBlueControlPoint", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 0;
    }

    private static int ParseGarrisonBuilderTrailingIndex(string value, string prefix)
    {
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || value.Length <= prefix.Length)
        {
            return 0;
        }

        var suffix = value[prefix.Length..];
        return int.TryParse(suffix, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var index)
            ? index
            : 0;
    }

    private void ApplyGarrisonBuilderPropertyEditorLivePreview()
    {
        if (_builderPropertyTarget != GarrisonBuilderPropertyTarget.SelectedMapEntity
            || _builderSelectedEntityIndex < 0
            || _builderSelectedEntityIndex >= _builderEntities.Count)
        {
            return;
        }

        var entity = _builderEntities[_builderSelectedEntityIndex];
        var properties = new Dictionary<string, string>(_builderPropertyEditorValues, StringComparer.OrdinalIgnoreCase);
        PreserveGarrisonBuilderHiddenEntityProperties(entity.Properties, properties);
        _builderEntities[_builderSelectedEntityIndex] = CustomMapBuilderEntity.Create(
            entity.Type,
            entity.X,
            entity.Y,
            properties,
            entity.XScale,
            entity.YScale).NormalizeForEditing();
        UpdateGarrisonBuilderDocumentEntities();
        _builderDirty = true;
    }

    private void DrawGarrisonBuilderObjectiveMapPickHighlights()
    {
        if (!_builderObjectiveMapPickActive)
        {
            return;
        }

        foreach (var entity in _builderEntities)
        {
            if (!IsGarrisonBuilderLinkableObjectiveEntity(entity))
            {
                continue;
            }

            if (!TryGetGarrisonBuilderEntityWorldBounds(entity, out var left, out var top, out var width, out var height))
            {
                continue;
            }

            var highlight = new RectangleF(left - 4f, top - 4f, width + 8f, height + 8f);
            DrawGarrisonBuilderRectangleOutline(ToScreenRectangle(highlight), new Color(255, 220, 80, 220));
            _spriteBatch.Draw(_pixel, ToScreenRectangle(highlight), new Color(255, 220, 80, 28));
        }
    }

    private int GetGarrisonBuilderPropertyRowHeight()
    {
        return GetGarrisonBuilderMenuRowHeight(GetGarrisonBuilderPropertyRowRelativeTextScale());
    }

    private int GetGarrisonBuilderPropertyListTop(Rectangle editorBounds)
    {
        return editorBounds.Y + 34;
    }

    private Rectangle GetGarrisonBuilderPropertyListBounds(Rectangle editorBounds)
    {
        var top = GetGarrisonBuilderPropertyListTop(editorBounds);
        var bottom = GetGarrisonBuilderPropertyAddBounds(editorBounds).Y - 6;
        return new Rectangle(editorBounds.X + 8, top, editorBounds.Width - 16, Math.Max(1, bottom - top));
    }

    private Rectangle GetGarrisonBuilderPropertyRowBounds(Rectangle editorBounds, int rowY)
    {
        var listBounds = GetGarrisonBuilderPropertyListBounds(editorBounds);
        const int rowInset = 4;
        return new Rectangle(
            listBounds.X + rowInset,
            rowY + 2,
            listBounds.Width - (rowInset * 2),
            GetGarrisonBuilderPropertyRowHeight() - 4);
    }

    private int GetGarrisonBuilderPropertyVisibleRows(Rectangle editorBounds)
    {
        var rowHeight = GetGarrisonBuilderPropertyRowHeight();
        var availableHeight = GetGarrisonBuilderPropertyAddBounds(editorBounds).Y - GetGarrisonBuilderPropertyListTop(editorBounds) - 6;
        var rowCount = GetGarrisonBuilderPropertyRows().Count;
        var fitted = Math.Max(1, availableHeight / rowHeight);
        return Math.Max(1, Math.Min(Math.Max(1, rowCount), fitted));
    }

    private static bool IsSkippedGarrisonBuilderPropertyKey(string key)
    {
        return key.Equals("type", StringComparison.OrdinalIgnoreCase)
            || key.Equals("x", StringComparison.OrdinalIgnoreCase)
            || key.Equals("y", StringComparison.OrdinalIgnoreCase)
            || key.Equals("xscale", StringComparison.OrdinalIgnoreCase)
            || key.Equals("yscale", StringComparison.OrdinalIgnoreCase)
            || key.Equals("scale", StringComparison.OrdinalIgnoreCase)
            || key.Equals("visualScale", StringComparison.OrdinalIgnoreCase)
            || key.Equals("walkmaskScale", StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLogicNodeColorMetadata.PropertyKey, StringComparison.OrdinalIgnoreCase)
            || IsGarrisonBuilderControlPointSectionProperty(key);
    }

    private bool IsSkippedGarrisonBuilderProperty(string key)
    {
        if (key.Equals(CustomMapCustomSpriteMetadata.ScalePropertyKey, StringComparison.OrdinalIgnoreCase)
            && TryGetGarrisonBuilderEditedEntityType(out var entityType)
            && CustomMapCustomSpriteMetadata.IsCustomSpriteEntityType(entityType))
        {
            return false;
        }

        return IsSkippedGarrisonBuilderPropertyKey(key);
    }

    private Rectangle GetGarrisonBuilderPropertyEditorBounds()
    {
        var width = Math.Min(BuilderUi(420), Math.Max(BuilderUi(280), BuilderViewportWidth - BuilderUi(80)));
        var height = GetGarrisonBuilderPropertyEditorHeight(width);
        return new Rectangle((BuilderViewportWidth - width) / 2, (BuilderViewportHeight - height) / 2, width, height);
    }

    private int GetGarrisonBuilderPropertyEditorHeight(int width)
    {
        const int titleArea = 32;
        const int footerArea = 52;
        const int editModeHeight = 118;
        if (_builderPropertyEditMode != GarrisonBuilderPropertyEditMode.List)
        {
            return editModeHeight;
        }

        if (!_builderUseModernUi)
        {
            return 340;
        }

        var rowHeight = GetGarrisonBuilderPropertyRowHeight();
        var rowCount = Math.Max(1, GetGarrisonBuilderPropertyRows().Count);
        const int listChrome = 88;
        var idealHeight = listChrome + (rowCount * rowHeight);
        return Math.Min(BuilderViewportHeight - 24, idealHeight);
    }

    private Rectangle GetGarrisonBuilderPropertyAddBounds(Rectangle editorBounds)
    {
        var buttonHeight = GetGarrisonBuilderMenuRowHeight();
        return new Rectangle(editorBounds.X + 8, editorBounds.Bottom - 48, editorBounds.Width - 16, buttonHeight);
    }

    private void RemoveNearestGarrisonBuilderEntity(Vector2 worldPosition)
    {
        var bestIndex = -1;
        var bestDistanceSquared = 20f * 20f;
        for (var index = 0; index < _builderEntities.Count; index += 1)
        {
            var entity = _builderEntities[index];
            var distanceSquared = Vector2.DistanceSquared(new Vector2(entity.X, entity.Y), worldPosition);
            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                bestIndex = index;
            }
        }

        if (bestIndex < 0)
        {
            _builderStatus = "no nearby entity to remove";
            return;
        }

        var removed = _builderEntities[bestIndex].Type;
        NotifyGarrisonBuilderEntityRemoved(bestIndex);
        _builderEntities.RemoveAt(bestIndex);
        UpdateGarrisonBuilderDocumentEntities();
        _builderDirty = true;
        _builderStatus = $"removed {removed}";
    }

    private void UpdateGarrisonBuilderDocumentEntities()
    {
        EnsureGarrisonBuilderLogicEntityKeys();
        SyncGarrisonBuilderCustomSpriteResources();
        _builderDocument = _builderDocument with
        {
            Entities = _builderEntities.ToArray(),
        };
    }

    private void EnableGarrisonBuilderEditor()
    {
        _builderEditorEnabled = true;
        SyncGarrisonBuilderPathBuffers();
        _builderStatus = "builder enabled";
        RequestGarrisonBuilderCameraCenter();
        LoadGarrisonBuilderEditorAssets();
        InvalidateDiscordRichPresenceRefresh();
    }

    private void DisableGarrisonBuilderEditor(string reason)
    {
        _builderEditorEnabled = false;
        _builderLayerParallaxDialogOpen = false;
        _builderEditingLayerOffsets = false;
        _builderStatus = reason;
        InvalidateDiscordRichPresenceRefresh();
    }

    private const string GarrisonBuilderQuickTestFolderPrefix = "_garrison_quicktest_";

    private bool CanQuickTestGarrisonBuilderDocument()
    {
        return !string.IsNullOrWhiteSpace(_builderDocument.BackgroundImagePath)
            && (!string.IsNullOrWhiteSpace(_builderDocument.WalkmaskImagePath)
                || !string.IsNullOrWhiteSpace(_builderDocument.EmbeddedWalkmaskSection));
    }

    private static string SanitizeGarrisonBuilderQuickTestName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return "map";
        }

        var chars = trimmed
            .Select(character => char.IsAsciiLetterOrDigit(character) ? character : '_')
            .ToArray();
        return new string(chars).Trim('_');
    }

    private bool TryExportGarrisonBuilderQuickTestPackage(out string levelName, out string error)
    {
        error = string.Empty;
        levelName = string.Empty;
        if (!CanQuickTestGarrisonBuilderDocument())
        {
            error = "quick test needs a background and walkmask";
            return false;
        }

        try
        {
            UpdateGarrisonBuilderDocumentEntities();
            ApplyGarrisonBuilderMapModeMetadata();
            ApplyGarrisonBuilderEntitySchemaMetadata();
            var safeName = SanitizeGarrisonBuilderQuickTestName(_builderDocument.Name);
            if (safeName.Length == 0)
            {
                safeName = "map";
            }

            var packageDirectory = Path.Combine(RuntimePaths.MapsDirectory, GarrisonBuilderQuickTestFolderPrefix + safeName);
            if (Directory.Exists(packageDirectory))
            {
                Directory.Delete(packageDirectory, recursive: true);
            }

            Directory.CreateDirectory(RuntimePaths.MapsDirectory);
            var exportDocument = _builderDocument with
            {
                Entities = CustomMapBuilderEntityNormalization.ResolveForExport(_builderDocument.Entities),
            };
            CustomMapPackageExporter.Export(exportDocument, packageDirectory);
            var manifestPath = CustomMapPackageExporter.ResolveManifestOutputPath(_builderDocument, packageDirectory);
            levelName = Path.GetFileNameWithoutExtension(manifestPath);
            SimpleLevelFactory.ClearCachedCatalog();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private void QuickTestGarrisonBuilderMap()
    {
        if (!_bootstrapController.CanEnterGameplaySession(out var bootstrapReason))
        {
            _builderStatus = bootstrapReason ?? "assets still loading";
            AddConsoleLine(_builderStatus);
            return;
        }

        if (!TryExportGarrisonBuilderQuickTestPackage(out var levelName, out var error))
        {
            _builderStatus = error;
            AddConsoleLine($"builder quick test failed: {error}");
            return;
        }

        DisableGarrisonBuilderEditor("quick test");
        _garrisonBuilderQuickTestActive = true;
        if (!_gameplaySessionController.TryBeginOfflineBotSession(
                levelName,
                GameplaySessionKind.Practice,
                _practiceTickRate,
                GetPracticeExperimentalGameplaySettings(),
                timeLimitMinutes: _practiceTimeLimitMinutes,
                capLimit: _practiceCapLimit,
                respawnSeconds: _practiceRespawnSeconds,
                enableInstantRedTeamIntelCaptureWin: false,
                openJoinMenus: false,
                consoleSessionName: "builder quick test"))
        {
            _garrisonBuilderQuickTestActive = false;
            _builderStatus = $"quick test failed to load {levelName}";
            AddConsoleLine(_builderStatus);
            EnableGarrisonBuilderEditor();
            _builderStatus = "returned to builder";
            return;
        }

        PrepareGarrisonBuilderQuickTestPlayer();
        _builderStatus = "quick test: soldier on map";
        AddConsoleLine(_builderStatus);
    }

    private void ReturnToGarrisonBuilderFromQuickTest()
    {
        if (!_garrisonBuilderQuickTestActive)
        {
            return;
        }

        _garrisonBuilderQuickTestActive = false;
        CloseInGameMenu();
        _gameplaySessionController.ResetActiveSessionState();
        _gameplaySessionKind = GameplaySessionKind.None;
        _mainMenuOpen = true;
        _teamSelectOpen = false;
        CloseGameplayOverlayState();
        EnableGarrisonBuilderEditor();
        _builderStatus = "returned to builder";
        AddConsoleLine(_builderStatus);
    }

    private void PrepareGarrisonBuilderQuickTestPlayer()
    {
        _world.DespawnEnemyDummy();
        _world.DespawnFriendlyDummy();
        _world.SetLocalPlayerTeam(PlayerTeam.Red);
        _world.PrepareLocalPlayerJoin();
        _world.CompleteLocalPlayerJoin(PlayerClass.Soldier);
    }

    private void LoadGarrisonBuilderEditorAssets()
    {
        _builderEntityButtonSprite ??= LoadGarrisonBuilderEntityButtonSprite();
        _builderButtonSprite ??= LoadGarrisonBuilderSprite("gbButtonS", ContentRoot.GetPath("Sprites", "HUDs", "Builder", "gbButtonS.images"), ref _builderOwnsButtonSprite);
        _builderMenuLayoutSprite ??= LoadGarrisonBuilderSprite("gbMenuLayoutS", ContentRoot.GetPath("Sprites", "HUDs", "Builder", "gbMenuLayoutS.images"), ref _builderOwnsMenuLayoutSprite);
        _builderGridSprite ??= LoadGarrisonBuilderSprite("GridS", ContentRoot.GetPath("Sprites", "HUDs", "Builder", "GridS.images"), ref _builderOwnsGridSprite);
        _builderScrollButtonSprite ??= LoadGarrisonBuilderSprite("ScrollButtonS", ContentRoot.GetPath("Sprites", "HUDs", "Lobby", "ScrollButtonS.images"), ref _builderOwnsScrollButtonSprite);
        _builderDefaultBackgroundTexture ??= TryLoadGarrisonBuilderTexture(ContentRoot.GetPath("Builder", "BuilderBGB.png"));
        _builderDefaultWalkmaskTexture ??= TryLoadGarrisonBuilderTexture(ContentRoot.GetPath("Builder", "BuilderWMB.png"));
        if (!string.Equals(_builderLoadedBackgroundPath, _builderDocument.BackgroundImagePath, StringComparison.OrdinalIgnoreCase))
        {
            _builderBackgroundTexture?.Dispose();
            _builderBackgroundTexture = TryLoadGarrisonBuilderTexture(_builderDocument.BackgroundImagePath);
            _builderLoadedBackgroundPath = _builderDocument.BackgroundImagePath;
        }

        if (!string.Equals(_builderLoadedWalkmaskPath, _builderDocument.WalkmaskImagePath, StringComparison.OrdinalIgnoreCase))
        {
            _builderWalkmaskTexture?.Dispose();
            _builderWalkmaskTexture = TryLoadGarrisonBuilderTexture(_builderDocument.WalkmaskImagePath);
            _builderLoadedWalkmaskPath = _builderDocument.WalkmaskImagePath;
        }

        RefreshGarrisonBuilderEmbeddedWalkmaskTexture(skipWhenFileWalkmaskLoaded: _builderWalkmaskTexture is not null);
    }

    private bool HasGarrisonBuilderWalkmaskData()
    {
        return !string.IsNullOrWhiteSpace(_builderDocument.WalkmaskImagePath)
            || !string.IsNullOrWhiteSpace(_builderDocument.EmbeddedWalkmaskSection);
    }

    private Texture2D? ResolveGarrisonBuilderWalkmaskTexture()
    {
        if (_builderWalkmaskTexture is not null)
        {
            return _builderWalkmaskTexture;
        }

        if (_builderEmbeddedWalkmaskTexture is not null)
        {
            return _builderEmbeddedWalkmaskTexture;
        }

        return HasGarrisonBuilderWalkmaskData()
            ? null
            : _builderDefaultWalkmaskTexture;
    }

    private void RefreshGarrisonBuilderEmbeddedWalkmaskTexture(bool skipWhenFileWalkmaskLoaded)
    {
        if (skipWhenFileWalkmaskLoaded)
        {
            DisposeGarrisonBuilderEmbeddedWalkmaskTexture();
            return;
        }

        var section = _builderDocument.EmbeddedWalkmaskSection.Trim();
        if (section.Length == 0)
        {
            DisposeGarrisonBuilderEmbeddedWalkmaskTexture();
            return;
        }

        if (string.Equals(_builderLoadedEmbeddedWalkmaskSection, section, StringComparison.Ordinal)
            && _builderEmbeddedWalkmaskTexture is not null)
        {
            return;
        }

        DisposeGarrisonBuilderEmbeddedWalkmaskTexture();
        if (!EmbeddedWalkmaskDecoder.TryDecodeSolidCells(section, out var width, out var height, out var cells))
        {
            return;
        }

        var pixels = new Microsoft.Xna.Framework.Color[width * height];
        for (var index = 0; index < cells.Length; index += 1)
        {
            pixels[index] = cells[index]
                ? Microsoft.Xna.Framework.Color.White
                : Microsoft.Xna.Framework.Color.Transparent;
        }

        var texture = new Texture2D(GraphicsDevice, width, height);
        texture.SetData(pixels);
        _builderEmbeddedWalkmaskTexture = texture;
        _builderLoadedEmbeddedWalkmaskSection = section;
    }

    private void DisposeGarrisonBuilderEmbeddedWalkmaskTexture()
    {
        _builderEmbeddedWalkmaskTexture?.Dispose();
        _builderEmbeddedWalkmaskTexture = null;
        _builderLoadedEmbeddedWalkmaskSection = string.Empty;
    }

    private LoadedGameMakerSprite? LoadGarrisonBuilderEntityButtonSprite()
    {
        var sprite = _runtimeAssets?.GetSprite("entityButtonS");
        if (sprite is not null)
        {
            return sprite;
        }

        var directory = ContentRoot.GetPath("Builder", "entityButtonS.images");
        if (!Directory.Exists(directory))
        {
            return null;
        }

        var frames = Directory
            .GetFiles(directory, "*.png", SearchOption.TopDirectoryOnly)
            .OrderBy(path => ExtractBuilderFrameIndex(path))
            .Select(path => new LoadedSpriteFrame(TextureDecodeUtility.LoadTexture(GraphicsDevice, File.ReadAllBytes(path), applyLegacyChromaKey: false)))
            .ToArray();
        if (frames.Length == 0)
        {
            return null;
        }

        _builderOwnsEntityButtonSprite = true;
        return new LoadedGameMakerSprite(frames, Point.Zero);
    }

    private LoadedGameMakerSprite? LoadGarrisonBuilderSprite(string name, string directory, ref bool ownsSprite)
    {
        var sprite = _runtimeAssets?.GetSprite(name);
        if (sprite is not null)
        {
            return sprite;
        }

        if (!Directory.Exists(directory))
        {
            return null;
        }

        var frames = Directory
            .GetFiles(directory, "*.png", SearchOption.TopDirectoryOnly)
            .OrderBy(path => ExtractBuilderFrameIndex(path))
            .Select(path => new LoadedSpriteFrame(TextureDecodeUtility.LoadTexture(GraphicsDevice, File.ReadAllBytes(path), applyLegacyChromaKey: false)))
            .ToArray();
        if (frames.Length == 0)
        {
            return null;
        }

        ownsSprite = true;
        return new LoadedGameMakerSprite(frames, Point.Zero);
    }

    private Texture2D? TryLoadGarrisonBuilderTexture(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        return TextureDecodeUtility.LoadTexture(GraphicsDevice, File.ReadAllBytes(path), applyLegacyChromaKey: false);
    }

    private bool TryHandleGarrisonBuilderConsoleCommand(string commandText, string[] parts)
    {
        if (parts.Length < 2)
        {
            AddConsoleLine(_builderEditorEnabled ? "builder: enabled" : "builder: disabled");
            AddConsoleLine("usage: builder <on|off|new|open|bg|wm|save|status> [path]");
            return true;
        }

        var argument = commandText[(parts[0].Length + parts[1].Length)..].Trim();
        switch (parts[1].ToLowerInvariant())
        {
            case "on":
                if (!GarrisonBuilderFeature.CanOpenFromConsole)
                {
                    AddConsoleLine("builder: disabled (console access is off)");
                    return true;
                }

                EnableGarrisonBuilderEditor();
                AddConsoleLine("builder enabled");
                return true;
            case "off":
                DisableGarrisonBuilderEditor("builder disabled");
                AddConsoleLine("builder disabled");
                return true;
            case "new":
                _builderDocument = CustomMapBuilderDocument.CreateEmpty(argument.Length == 0 ? "new_map" : argument);
                _builderEntities.Clear();
                ClearGarrisonBuilderHiddenEntities();
                _builderOpenMapBuffer = string.Empty;
                _builderSavePath = string.Empty;
                SyncGarrisonBuilderPathBuffers();
                _builderDirty = false;
                RequestGarrisonBuilderCameraCenter();
                _builderStatus = "new builder document";
                AddConsoleLine("builder document reset");
                return true;
            case "open":
                OpenGarrisonBuilderMap(argument);
                return true;
            case "bg":
                SetGarrisonBuilderBackgroundPath(argument);
                return true;
            case "wm":
                SetGarrisonBuilderWalkmaskPath(argument);
                return true;
            case "save":
                if (!string.IsNullOrWhiteSpace(argument))
                {
                    _builderSavePath = argument.Trim('"');
                    _builderSavePathBuffer = _builderSavePath;
                }

                SaveGarrisonBuilderDocument();
                return true;
            case "status":
                AddConsoleLine(_builderEditorEnabled ? "builder: enabled" : "builder: disabled");
                AddConsoleLine($"bg: {_builderDocument.BackgroundImagePath}");
                AddConsoleLine($"wm: {_builderDocument.WalkmaskImagePath}");
                AddConsoleLine($"out: {_builderSavePath}");
                AddConsoleLine($"entities: {_builderEntities.Count}");
                AddConsoleLine(_builderStatus);
                return true;
            default:
                AddConsoleLine("usage: builder <on|off|new|open|bg|wm|save|status> [path]");
                return true;
        }
    }

    private void OpenGarrisonBuilderMap(string path)
    {
        path = path.Trim().Trim('"');
        if (!File.Exists(path))
        {
            AddConsoleLine($"builder map file not found: {path}");
            return;
        }

        var isPackage = Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase);
        var editableDocument = isPackage
            ? CustomMapPackageImporter.ImportDocument(path)
            : CustomMapBuilderPngImporter.Import(path);
        if (isPackage && editableDocument is null)
        {
            _builderStatus = "package open failed";
            AddConsoleLine(_builderStatus);
            return;
        }

        var runtimeImport = editableDocument is null ? CustomMapPngImporter.Import(path) : null;
        _builderDocument = editableDocument?.NormalizeForEditing() ?? CustomMapBuilderDocument.CreateEmpty(Path.GetFileNameWithoutExtension(path)) with
        {
            BackgroundImagePath = path,
        };
        ClearGarrisonBuilderResourceTextureCache();
        _builderSelectedResourceName = string.Empty;
        _builderEntities.Clear();
        ClearGarrisonBuilderHiddenEntities();
        _builderEntities.AddRange(_builderDocument.Entities);
        EnsureGarrisonBuilderLogicEntityKeys();
        ClearGarrisonBuilderMapEntitySelection();
        _builderSavePath = path;
        SyncGarrisonBuilderPathBuffers();
        _builderOpenMapBuffer = path;
        _builderDirty = false;
        _builderLoadedBackgroundPath = string.Empty;
        _builderLoadedWalkmaskPath = string.Empty;
        _builderLoadedEmbeddedWalkmaskSection = string.Empty;
        LoadGarrisonBuilderEditorAssets();
        UpdateGarrisonBuilderEntityCoordinateMode();
        _builderSelectedGameMode = MapGameModeMetadata.ResolveBuilderGameMode(
            _builderDocument.Metadata,
            _builderEntities);
        ClearGarrisonBuilderHistory();
        _builderStatus = editableDocument is not null
            ? isPackage
                ? $"opened package map with {_builderEntities.Count} entities"
                : $"opened editable map with {_builderEntities.Count} entities"
            : runtimeImport is null
                ? "opened PNG as background"
                : "opened compiled map as background";
        RequestGarrisonBuilderCameraCenter();
        AddConsoleLine($"builder opened: {path}");
        AddConsoleLine(_builderStatus);
    }

    private void SetGarrisonBuilderBackgroundPath(string path)
    {
        path = path.Trim().Trim('"');
        if (!File.Exists(path))
        {
            AddConsoleLine($"builder bg file not found: {path}");
            return;
        }

        _builderDocument = _builderDocument with { BackgroundImagePath = path };
        _builderBackgroundPathBuffer = path;
        _builderLoadedBackgroundPath = string.Empty;
        _builderDirty = true;
        LoadGarrisonBuilderEditorAssets();
        UpdateGarrisonBuilderEntityCoordinateMode();
        _builderStatus = "background loaded";
        RequestGarrisonBuilderCameraCenter();
        AddConsoleLine($"builder bg: {path}");
    }

    private void SetGarrisonBuilderWalkmaskPath(string path)
    {
        path = path.Trim().Trim('"');
        if (!File.Exists(path))
        {
            AddConsoleLine($"builder wm file not found: {path}");
            return;
        }

        _builderDocument = _builderDocument with
        {
            WalkmaskImagePath = path,
            EmbeddedWalkmaskSection = string.Empty,
        };
        _builderWalkmaskPathBuffer = path;
        _builderLoadedWalkmaskPath = string.Empty;
        _builderLoadedEmbeddedWalkmaskSection = string.Empty;
        _builderDirty = true;
        LoadGarrisonBuilderEditorAssets();
        UpdateGarrisonBuilderEntityCoordinateMode();
        _builderStatus = "walkmask loaded";
        RequestGarrisonBuilderCameraCenter();
        AddConsoleLine($"builder wm: {path}");
    }

    private void SaveGarrisonBuilderDocument()
    {
        if (!CanSaveGarrisonBuilderDocument())
        {
            _builderStatus = "set bg, wm, and output path before saving";
            AddConsoleLine(_builderStatus);
            return;
        }

        try
        {
            UpdateGarrisonBuilderDocumentEntities();
            ApplyGarrisonBuilderMapModeMetadata();
            ApplyGarrisonBuilderEntitySchemaMetadata();
            var validation = CustomMapBuilderValidator.Validate(_builderDocument, _builderSelectedGameMode);
            if (!validation.IsValid)
            {
                _builderStatus = validation.Issues[0].Message;
                AddConsoleLine($"builder validation failed: {_builderStatus}");
                return;
            }

            if (CustomMapPackageExporter.IsPackageOutputPath(_builderSavePath))
            {
                CustomMapPackageExporter.Export(_builderDocument, _builderSavePath);
                _builderSavePath = CustomMapPackageExporter.ResolveManifestOutputPath(_builderDocument, _builderSavePath);
            }
            else
            {
                CustomMapPngExporter.Export(_builderDocument, _builderSavePath);
            }

            _builderSavePathBuffer = _builderSavePath;
            _builderDirty = false;
            _builderStatus = $"saved {Path.GetFileName(_builderSavePath)}";
            AddConsoleLine($"builder saved: {_builderSavePath}");
        }
        catch (Exception ex)
        {
            _builderStatus = $"save failed: {ex.Message}";
            AddConsoleLine(_builderStatus);
        }
    }

    private void ApplyGarrisonBuilderEntitySchemaMetadata()
    {
        var metadata = new Dictionary<string, string>(_builderDocument.Metadata, StringComparer.OrdinalIgnoreCase);
        metadata[CustomMapEntityRuntimeRegistry.EntitySchemaMetadataKey] = CustomMapEntityRuntimeRegistry.ModernEntitySchemaValue;
        _builderDocument = _builderDocument with { Metadata = metadata };
    }

    private void ApplyGarrisonBuilderMapModeMetadata()
    {
        var metadata = new Dictionary<string, string>(_builderDocument.Metadata, StringComparer.OrdinalIgnoreCase);
        var propertyValue = MapGameModeMetadata.ToPropertyValue(_builderSelectedGameMode);
        if (string.IsNullOrEmpty(propertyValue))
        {
            metadata.Remove(MapGameModeMetadata.GameModePropertyKey);
        }
        else
        {
            metadata[MapGameModeMetadata.GameModePropertyKey] = propertyValue;
        }

        _builderDocument = _builderDocument with { Metadata = metadata };
    }

    private bool TryHandleGarrisonBuilderScrScorePropertyClick(
        string key,
        string value,
        Rectangle rowBounds,
        Point position)
    {
        if (!IsGarrisonBuilderScrMapScorePropertyKey(key))
        {
            return false;
        }

        if (_builderPropertyTarget != GarrisonBuilderPropertyTarget.MapProperties)
        {
            return false;
        }

        var delta = position.X < rowBounds.X + (rowBounds.Width / 2) ? -1 : 1;
        var nextValue = ScrMapSettingsMetadata.StepScorePropertyValue(value, delta);
        _builderPropertyEditorValues[key] = nextValue.ToString(CultureInfo.InvariantCulture);
        ApplyGarrisonBuilderPropertyEditorLivePreview();
        MarkGarrisonBuilderPropertyEditorChanged();
        return true;
    }

    private static bool IsGarrisonBuilderScrMapScorePropertyKey(string key)
    {
        return key.Equals(ScrMapSettingsMetadata.ScoreToWinPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(ScrMapSettingsMetadata.RedStartingScorePropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(ScrMapSettingsMetadata.BlueStartingScorePropertyKey, StringComparison.OrdinalIgnoreCase);
    }

    private bool CanSaveGarrisonBuilderDocument()
    {
        return !string.IsNullOrWhiteSpace(_builderDocument.BackgroundImagePath)
            && (!string.IsNullOrWhiteSpace(_builderDocument.WalkmaskImagePath)
                || !string.IsNullOrWhiteSpace(_builderDocument.EmbeddedWalkmaskSection))
            && !string.IsNullOrWhiteSpace(_builderSavePath);
    }

    private void OpenGarrisonBuilderFromMainMenu()
    {
        if (!GarrisonBuilderFeature.CanOpenFromMainMenu)
        {
            return;
        }

        EnableGarrisonBuilderEditor();
        _builderGameModeMenuOpen = true;
        _builderStatus = "map builder";
    }

    private void SyncGarrisonBuilderPathBuffers()
    {
        _builderBackgroundPathBuffer = _builderDocument.BackgroundImagePath;
        _builderWalkmaskPathBuffer = _builderDocument.WalkmaskImagePath;
        _builderSavePathBuffer = _builderSavePath;
    }

    private void DisposeGarrisonBuilderEditorAssets()
    {
        _builderBackgroundTexture?.Dispose();
        _builderWalkmaskTexture?.Dispose();
        DisposeGarrisonBuilderEmbeddedWalkmaskTexture();
        _builderDefaultBackgroundTexture?.Dispose();
        _builderDefaultWalkmaskTexture?.Dispose();
        _builderDefaultBackgroundTexture = null;
        _builderDefaultWalkmaskTexture = null;
        if (_builderOwnsEntityButtonSprite && _builderEntityButtonSprite is not null)
        {
            foreach (var frame in _builderEntityButtonSprite.Frames)
            {
                frame.Dispose();
            }
        }

        DisposeOwnedGarrisonBuilderSprite(_builderButtonSprite, _builderOwnsButtonSprite);
        DisposeOwnedGarrisonBuilderSprite(_builderMenuLayoutSprite, _builderOwnsMenuLayoutSprite);
        DisposeOwnedGarrisonBuilderSprite(_builderGridSprite, _builderOwnsGridSprite);
        DisposeOwnedGarrisonBuilderSprite(_builderScrollButtonSprite, _builderOwnsScrollButtonSprite);
        ClearGarrisonBuilderResourceTextureCache();
        _builderBackgroundTexture = null;
        _builderWalkmaskTexture = null;
        DisposeGarrisonBuilderEmbeddedWalkmaskTexture();
        _builderDefaultBackgroundTexture = null;
        _builderDefaultWalkmaskTexture = null;
        _builderEntityButtonSprite = null;
        _builderButtonSprite = null;
        _builderMenuLayoutSprite = null;
        _builderGridSprite = null;
        _builderScrollButtonSprite = null;
        _builderOwnsEntityButtonSprite = false;
        _builderOwnsButtonSprite = false;
        _builderOwnsMenuLayoutSprite = false;
        _builderOwnsGridSprite = false;
        _builderOwnsScrollButtonSprite = false;
        _builderLoadedBackgroundPath = string.Empty;
        _builderLoadedWalkmaskPath = string.Empty;
    }

    private static void DisposeOwnedGarrisonBuilderSprite(LoadedGameMakerSprite? sprite, bool ownsSprite)
    {
        if (!ownsSprite || sprite is null)
        {
            return;
        }

        foreach (var frame in sprite.Frames)
        {
            frame.Dispose();
        }
    }

    private Rectangle GetGarrisonBuilderPanelBounds()
    {
        return new Rectangle(Math.Max(0, BuilderViewportWidth - BuilderPanelWidth), 0, BuilderPanelWidth, BuilderViewportHeight);
    }

    private Rectangle[] CreateGarrisonBuilderButtonRow(int x, int y, int width, int count)
    {
        var buttons = new Rectangle[count];
        var buttonHeight = GetGarrisonBuilderMenuRowHeight();
        var buttonWidth = (width - (BuilderButtonGap * (count - 1))) / count;
        for (var index = 0; index < count; index += 1)
        {
            buttons[index] = new Rectangle(x + (index * (buttonWidth + BuilderButtonGap)), y, buttonWidth, buttonHeight);
        }

        return buttons;
    }

    private void DrawGarrisonBuilderButton(Rectangle bounds, string label, bool active, bool enabled, MouseState mouse)
    {
        var hovered = bounds.Contains(mouse.Position);
        var fillColor = !enabled
            ? new Color(54, 56, 60, 190)
            : active
                ? new Color(92, 184, 212, 230)
                : hovered
                    ? new Color(58, 70, 80, 230)
                    : new Color(38, 42, 48, 230);
        _spriteBatch.Draw(_pixel, bounds, fillColor);
        if (!string.IsNullOrWhiteSpace(label))
        {
            var fittedScale = GetGarrisonBuilderBitmapFontScaleToFit(
                label,
                Math.Max(8f, bounds.Width - 12f),
                Math.Max(8f, bounds.Height - 8f),
                1f);
            var measuredWidth = MeasureBitmapFontWidth(label, fittedScale);
            var measuredHeight = MeasureBitmapFontHeight(fittedScale);
            var textColor = enabled ? Color.White : new Color(140, 144, 148);
            DrawBitmapFontText(
                label,
                new Vector2(
                    bounds.X + ((bounds.Width - measuredWidth) * 0.5f),
                    bounds.Y + MathF.Max(4f, (bounds.Height - measuredHeight) * 0.5f)),
                textColor,
                fittedScale);
        }
    }

    private void DrawGarrisonBuilderText(string text, int x, int y, Color color, float scale)
    {
        DrawGarrisonBuilderText(text, new Vector2(x, y), color, scale);
    }

    private void DrawGarrisonBuilderText(string text, Vector2 position, Color color, float relativeScale)
    {
        var snapped = new Vector2(MathF.Round(position.X), MathF.Round(position.Y));
        var adjustedScale = Math.Clamp(GetGarrisonBuilderRelativeBitmapFontScale(relativeScale), 0.5f, 16f);
        DrawBitmapFontText(text, snapped, color, adjustedScale);
    }

    private void DrawGarrisonBuilderControlPointIndexOverlay(CustomMapBuilderEntity entity)
    {
        if (!TryGetGarrisonBuilderEntityWorldBounds(entity, out var left, out var top, out var width, out var height))
        {
            return;
        }

        var index = ControlPointOwnershipResolver.ResolveControlPointIndex(entity);
        var label = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var visualScale = _builderUseModernUi ? GetGarrisonBuilderMapVisualScale() : 1f;
        const float labelBaseRelativeScale = 0.72f;
        var labelScale = labelBaseRelativeScale * visualScale;
        var labelSize = MeasureGarrisonBuilderText(label, labelScale);
        var bottomRight = _builderUseModernUi
            ? BuilderWorldToScreen(new Vector2(left + width, top + height))
            : new Vector2(left + width, top + height) - _builderCamera;
        var marginX = 3f * visualScale;
        var marginY = 2f * visualScale;
        var labelPosition = new Vector2(
            bottomRight.X - labelSize.X - marginX,
            bottomRight.Y - labelSize.Y - marginY);
        DrawGarrisonBuilderText(label, labelPosition, Color.Black, labelScale);
    }

    private void SyncGarrisonBuilderControlPointPropertyEditorFields()
    {
        if (!IsEditingGarrisonBuilderControlPointEntity())
        {
            return;
        }

        if (!TryGetGarrisonBuilderEditedEntity(out var entity))
        {
            return;
        }

        _builderPropertyEditorValues[ControlPointIndexMetadata.PropertyKey] =
            ControlPointIndexMetadata.ToPropertyValue(ControlPointOwnershipResolver.ResolveControlPointIndex(entity));

        SyncGarrisonBuilderControlPointCapTimeMultiplierField(entity);

        if (!IsGarrisonBuilderControlPointOverrideEnabled())
        {
            _builderPropertyEditorValues.Remove(ControlPointInitialOwnershipMetadata.PropertyKey);
            _builderPropertyEditorValues.Remove(ControlPointLockDependencyMetadata.LockedWhenCpPropertyKey);
            _builderPropertyEditorValues.Remove(ControlPointLockDependencyMetadata.LockedWhenTeamPropertyKey);
            _builderPropertyEditorValues.Remove(ControlPointLockDependencyMetadata.UnlockedWhenCpPropertyKey);
            _builderPropertyEditorValues.Remove(ControlPointLockDependencyMetadata.UnlockedWhenTeamPropertyKey);
            _builderPropertyEditorValues.Remove(ControlPointInitialLockStateMetadata.PropertyKey);
            return;
        }

        if (!_builderPropertyEditorValues.ContainsKey(ControlPointInitialOwnershipMetadata.PropertyKey))
        {
            var ownership = ControlPointOwnershipResolver.ParseEntityInitialOwnership(entity);
            if (ownership == ControlPointInitialOwnership.ModeDefault)
            {
                ownership = ControlPointInitialOwnership.Neutral;
            }

            _builderPropertyEditorValues[ControlPointInitialOwnershipMetadata.PropertyKey] =
                ControlPointInitialOwnershipMetadata.ToPropertyValue(ownership);
        }

        EnsureGarrisonBuilderControlPointLockProperty(
            ControlPointLockDependencyMetadata.LockedWhenCpPropertyKey,
            entity.Properties);
        EnsureGarrisonBuilderControlPointLockProperty(
            ControlPointLockDependencyMetadata.LockedWhenTeamPropertyKey,
            entity.Properties,
            ControlPointLockDependencyMetadata.ToTeamPropertyValue(PlayerTeam.Red));
        EnsureGarrisonBuilderControlPointLockProperty(
            ControlPointLockDependencyMetadata.UnlockedWhenCpPropertyKey,
            entity.Properties);
        EnsureGarrisonBuilderControlPointLockProperty(
            ControlPointLockDependencyMetadata.UnlockedWhenTeamPropertyKey,
            entity.Properties,
            ControlPointLockDependencyMetadata.ToTeamPropertyValue(PlayerTeam.Blue));
        if (!_builderPropertyEditorValues.ContainsKey(ControlPointInitialLockStateMetadata.PropertyKey))
        {
            _builderPropertyEditorValues[ControlPointInitialLockStateMetadata.PropertyKey] =
                ControlPointInitialLockStateMetadata.ToPropertyValue(
                    ControlPointInitialLockStateMetadata.ParseLocked(entity.Properties));
        }
    }

    private void EnsureGarrisonBuilderControlPointLockProperty(
        string key,
        IReadOnlyDictionary<string, string> sourceProperties,
        string defaultValue = "")
    {
        if (_builderPropertyEditorValues.ContainsKey(key))
        {
            return;
        }

        _builderPropertyEditorValues[key] = sourceProperties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : defaultValue;
    }

    private void SyncGarrisonBuilderControlPointCapTimeMultiplierField(CustomMapBuilderEntity entity)
    {
        if (ControlPointCapTimeMultiplierMetadata.IsCustom(entity.Properties))
        {
            if (!_builderPropertyEditorValues.ContainsKey(ControlPointCapTimeMultiplierMetadata.PropertyKey))
            {
                var (multiplier, _) = ControlPointCapTimeMultiplierMetadata.Parse(entity.Properties);
                _builderPropertyEditorValues[ControlPointCapTimeMultiplierMetadata.PropertyKey] =
                    ControlPointCapTimeMultiplierMetadata.ToPropertyValue(multiplier);
            }

            return;
        }

        var totalControlPoints = CountGarrisonBuilderControlPointsForCapTimeRules();
        var controlPointIndex = ControlPointOwnershipResolver.ResolveControlPointIndex(entity);
        var autoMultiplier = ControlPointCapTimeMultiplierMetadata.ResolveAutoMultiplier(
            totalControlPoints,
            controlPointIndex,
            HasGarrisonBuilderControlPointSetupGates());
        _builderPropertyEditorValues[ControlPointCapTimeMultiplierMetadata.PropertyKey] =
            ControlPointCapTimeMultiplierMetadata.ToPropertyValue(autoMultiplier);
        _builderPropertyEditorValues.Remove(ControlPointCapTimeMultiplierMetadata.CustomPropertyKey);
    }

    private int CountGarrisonBuilderControlPointsForCapTimeRules()
    {
        var count = 0;
        for (var index = 0; index < _builderEntities.Count; index += 1)
        {
            if (ControlPointOwnershipResolver.IsControlPointEntity(_builderEntities[index].Type))
            {
                count += 1;
            }
        }

        return Math.Max(1, count);
    }

    private bool IsGarrisonBuilderCapTimeMultiplierPropertyRow(string key)
    {
        return key.Equals(ControlPointCapTimeMultiplierMetadata.PropertyKey, StringComparison.OrdinalIgnoreCase)
            && IsEditingGarrisonBuilderControlPointEntity();
    }

    private bool IsGarrisonBuilderCapTimeMultiplierTextEditActive()
    {
        return _builderPropertyEditMode == GarrisonBuilderPropertyEditMode.EditValue
            && IsGarrisonBuilderCapTimeMultiplierPropertyRow(_builderPropertyEditKey);
    }

    private static bool IsGarrisonBuilderCapTimeMultiplierTextCharacterAllowed(char character)
    {
        return char.IsDigit(character) || character == '.';
    }

    private Rectangle GetGarrisonBuilderCapTimeMultiplierSliderBounds(
        Rectangle rowBounds,
        string value,
        float textScale,
        bool hasClearButton)
    {
        var sliderDisplay = FormatGarrisonBuilderCapTimeMultiplierSliderDisplay(value);
        var sliderWidth = MeasureBitmapFontWidth(sliderDisplay, textScale);
        const float rightPadding = 8f;
        var rightEdge = hasClearButton
            ? GetGarrisonBuilderPropertyClearButtonBounds(rowBounds).X - 6f
            : rowBounds.Right - rightPadding;
        var sliderX = rightEdge - sliderWidth;
        var textHeight = MeasureBitmapFontHeight(textScale);
        var textY = rowBounds.Y + MathF.Max(2f, ((rowBounds.Height - textHeight) * 0.5f) - 1f);
        return new Rectangle(
            (int)MathF.Floor(sliderX),
            (int)MathF.Floor(textY),
            (int)MathF.Ceiling(sliderWidth),
            (int)MathF.Ceiling(textHeight));
    }

    private static string FormatGarrisonBuilderCapTimeMultiplierSliderDisplay(string value)
    {
        var multiplier = ControlPointCapTimeMultiplierMetadata.ParseMultiplier(value);
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"< {ControlPointCapTimeMultiplierMetadata.ToPropertyValue(multiplier)} >");
    }

    private void DrawGarrisonBuilderCapTimeMultiplierPropertyRow(
        Rectangle rowBounds,
        string value,
        MouseState mouse,
        float textScale,
        bool hovered,
        Rectangle sliderBounds,
        Rectangle clearBounds,
        bool hasClearButton)
    {
        var label = ControlPointCapTimeMultiplierMetadata.IsCustom(_builderPropertyEditorValues)
            ? "Cap time multiplier (custom)"
            : "Cap time multiplier (auto)";
        var textY = rowBounds.Y + MathF.Max(2f, ((rowBounds.Height - MeasureBitmapFontHeight(textScale)) * 0.5f) - 1f);
        var labelMaxWidth = MathF.Max(40f, sliderBounds.X - rowBounds.X - 10f);
        var displayLabel = TruncateGarrisonBuilderPropertyLabel(label, labelMaxWidth, textScale);
        var labelColor = _builderUseModernUi ? Color.White : Color.Black;
        if (_builderUseModernUi)
        {
            DrawBitmapFontText(displayLabel, new Vector2(rowBounds.X + 6f, textY), labelColor, textScale);
        }
        else
        {
            DrawGarrisonBuilderText(displayLabel, rowBounds.Location.ToVector2() + new Vector2(4f, 3f), labelColor, textScale);
        }

        var sliderDisplay = FormatGarrisonBuilderCapTimeMultiplierSliderDisplay(value);
        var sliderHovered = hovered || sliderBounds.Contains(mouse.Position);
        var sliderColor = sliderHovered
            ? (_builderUseModernUi ? new Color(220, 220, 220) : Color.Black)
            : (_builderUseModernUi ? new Color(186, 186, 186) : new Color(64, 64, 64));
        if (_builderUseModernUi)
        {
            DrawBitmapFontText(sliderDisplay, new Vector2(sliderBounds.X, textY), sliderColor, textScale);
        }
        else
        {
            DrawGarrisonBuilderText(sliderDisplay, new Vector2(sliderBounds.X, rowBounds.Y + 3f), sliderColor, textScale);
        }

        if (hasClearButton)
        {
            DrawGarrisonBuilderPropertyClearButton(
                clearBounds,
                hovered || clearBounds.Contains(mouse.Position));
        }
    }

    private bool TryHandleGarrisonBuilderCapTimeMultiplierClick(
        string key,
        string value,
        Rectangle rowBounds,
        Point position,
        Rectangle sliderBounds)
    {
        if (!sliderBounds.Contains(position))
        {
            return false;
        }

        GetGarrisonBuilderCapTimeMultiplierDigitBounds(rowBounds, value, GetGarrisonBuilderPropertyRowTextScale(), out var digitBounds);
        if (digitBounds.Contains(position))
        {
            BeginGarrisonBuilderCapTimeMultiplierTextEdit(key, value);
            return true;
        }

        var delta = position.X < sliderBounds.X + (sliderBounds.Width / 2) ? -0.25f : 0.25f;
        var nextValue = ControlPointCapTimeMultiplierMetadata.ParseMultiplier(value) + delta;
        ControlPointCapTimeMultiplierMetadata.MarkCustom(_builderPropertyEditorValues, MathF.Max(0.25f, nextValue));
        ApplyGarrisonBuilderPropertyEditorLivePreview();
        MarkGarrisonBuilderPropertyEditorChanged();
        return true;
    }

    private bool TryBeginGarrisonBuilderCapTimeMultiplierTextEdit(string key, string value, Rectangle rowBounds, Point position)
    {
        if (!IsGarrisonBuilderCapTimeMultiplierPropertyRow(key))
        {
            return false;
        }

        var textScale = GetGarrisonBuilderPropertyRowTextScale();
        var hasClear = GarrisonBuilderPropertyHasClearableConnection(key, value);
        var sliderBounds = GetGarrisonBuilderCapTimeMultiplierSliderBounds(rowBounds, value, textScale, hasClear);
        if (TryHandleGarrisonBuilderCapTimeMultiplierClick(key, value, rowBounds, position, sliderBounds))
        {
            return true;
        }

        return sliderBounds.Contains(position);
    }

    private void BeginGarrisonBuilderCapTimeMultiplierTextEdit(string key, string value)
    {
        BeginGarrisonBuilderPropertyTextEdit(
            key,
            ControlPointCapTimeMultiplierMetadata.ToPropertyValue(
                ControlPointCapTimeMultiplierMetadata.ParseMultiplier(value)),
            GarrisonBuilderPropertyEditMode.EditValue);
    }

    private void GetGarrisonBuilderCapTimeMultiplierDigitBounds(
        Rectangle rowBounds,
        string value,
        float textScale,
        out Rectangle digitBounds)
    {
        var sliderDisplay = FormatGarrisonBuilderCapTimeMultiplierSliderDisplay(value);
        var sliderWidth = MeasureBitmapFontWidth(sliderDisplay, textScale);
        const float rightPadding = 8f;
        var sliderX = rowBounds.Right - rightPadding - sliderWidth;
        var textHeight = MeasureBitmapFontHeight(textScale);
        var textY = rowBounds.Y + MathF.Max(2f, ((rowBounds.Height - textHeight) * 0.5f) - 1f);
        var digitText = ControlPointCapTimeMultiplierMetadata.ToPropertyValue(
            ControlPointCapTimeMultiplierMetadata.ParseMultiplier(value));
        var digitWidth = MeasureBitmapFontWidth(digitText, textScale);
        var digitX = sliderX + ((sliderWidth - digitWidth) * 0.5f);
        digitBounds = new Rectangle(
            (int)MathF.Floor(digitX),
            (int)MathF.Floor(textY),
            (int)MathF.Ceiling(digitWidth),
            (int)MathF.Ceiling(textHeight));
    }

    private bool TryGetGarrisonBuilderEditedEntity(out CustomMapBuilderEntity entity)
    {
        entity = default;
        if (_builderPropertyTarget == GarrisonBuilderPropertyTarget.SelectedMapEntity
            && _builderSelectedEntityIndex >= 0
            && _builderSelectedEntityIndex < _builderEntities.Count)
        {
            entity = _builderEntities[_builderSelectedEntityIndex];
            return true;
        }

        if (_builderPropertyTarget == GarrisonBuilderPropertyTarget.PlacementEntity
            && !string.IsNullOrWhiteSpace(_builderSelectedEntityType)
            && CustomMapBuilderEntityCatalog.TryGetDefinition(_builderSelectedEntityType, out _))
        {
            var properties = new Dictionary<string, string>(_builderPropertyEditorValues, StringComparer.OrdinalIgnoreCase);
            entity = CustomMapBuilderEntity.Create(_builderSelectedEntityType, 0f, 0f, properties);
            return true;
        }

        return false;
    }

    private void DrawGarrisonBuilderEntityMapLabel(CustomMapBuilderEntity entity, Vector2? markerLabelAnchor = null)
    {
        const float labelRelativeScale = 1f;
        if (markerLabelAnchor.HasValue)
        {
            DrawGarrisonBuilderText(entity.Type, markerLabelAnchor.Value, Color.White, labelRelativeScale);
            return;
        }

        if (_builderUseModernUi
            && TryGetGarrisonBuilderEntityWorldBounds(entity, out var left, out var top, out var width, out var height))
        {
            var labelSize = MeasureGarrisonBuilderText(entity.Type, labelRelativeScale);
            var bottomCenter = BuilderWorldToScreen(new Vector2(left + (width * 0.5f), top + height));
            var labelPosition = new Vector2(
                bottomCenter.X - (labelSize.X * 0.5f),
                bottomCenter.Y + 4f);
            DrawGarrisonBuilderText(entity.Type, labelPosition, Color.White, labelRelativeScale);
            return;
        }

        var screen = _builderUseModernUi
            ? BuilderWorldToScreen(new Vector2(entity.X, entity.Y))
            : new Vector2(entity.X, entity.Y) - _builderCamera;
        DrawGarrisonBuilderText(entity.Type, screen + new Vector2(7f, -8f), Color.White, labelRelativeScale);
    }

    private Vector2 MeasureGarrisonBuilderText(string text, float relativeScale)
    {
        var adjustedScale = Math.Clamp(GetGarrisonBuilderRelativeBitmapFontScale(relativeScale), 0.5f, 16f);
        return new Vector2(
            MeasureBitmapFontWidth(text, adjustedScale),
            MeasureBitmapFontHeight(adjustedScale));
    }

    private void DrawGarrisonBuilderWrapped(string text, int x, int y, int maxWidth, Color color)
    {
        var line = TrimGarrisonBuilderTextToWidth(text, maxWidth, 1f);
        DrawGarrisonBuilderText(line, x, y, color, 0.95f);
    }

    private string TrimGarrisonBuilderTextToWidth(string text, float maxWidth, float scale)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0f || MeasureGarrisonBuilderText(text, scale).X <= maxWidth)
        {
            return text;
        }

        const string ellipsis = "...";
        var trimmed = text;
        while (trimmed.Length > 0 && MeasureGarrisonBuilderText(trimmed + ellipsis, scale).X > maxWidth)
        {
            trimmed = trimmed[..^1];
        }

        return trimmed.Length == 0 ? ellipsis : trimmed + ellipsis;
    }

    private static Color GetGarrisonBuilderEntityColor(string type)
    {
        if (type.Contains("red", StringComparison.OrdinalIgnoreCase))
        {
            return new Color(224, 88, 76);
        }

        if (type.Contains("blue", StringComparison.OrdinalIgnoreCase))
        {
            return new Color(88, 130, 224);
        }

        return new Color(235, 214, 116);
    }

    private static int ExtractBuilderFrameIndex(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var spaceIndex = name.LastIndexOf(' ');
        return spaceIndex >= 0 && int.TryParse(name[(spaceIndex + 1)..], out var index) ? index : int.MaxValue;
    }

    private static string ShortenBuilderPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "(unset)";
        }

        var fileName = Path.GetFileName(path);
        return fileName.Length > 0 ? fileName : path;
    }

    private IReadOnlyList<CustomMapBuilderEntityDefinition> GetActiveGarrisonBuilderEntityDefinitions()
    {
        IEnumerable<CustomMapBuilderEntityDefinition> definitions = _builderEntityDefinitions;
        if (_builderUseModernUi)
        {
            definitions = definitions.Where(definition =>
                !CustomMapBuilderEntityNormalization.LegacyPaletteHiddenTypes.Contains(definition.Type)
                && !MapLogicMetadata.IsLogicEntityType(definition.Type));
        }

        if (_builderSelectedGameMode != CustomMapBuilderGameMode.Free)
        {
            definitions = definitions.Where(definition =>
                definition.Modes == CustomMapBuilderGameMode.Free
                || (definition.Modes & _builderSelectedGameMode) != 0);
        }

        return definitions.ToArray();
    }

    private bool TryHandleGarrisonBuilderGameModeMenuClick(Point position, bool leftClick)
    {
        if (!leftClick)
        {
            return _builderGameModeMenuOpen;
        }

        var bounds = GetGarrisonBuilderGameModeMenuBounds();
        if (!bounds.Contains(position))
        {
            _builderGameModeMenuOpen = false;
            return true;
        }

        var items = GetGarrisonBuilderModeMenuItems();
        for (var rowIndex = 0; rowIndex < items.Length; rowIndex += 1)
        {
            if (!GetGarrisonBuilderModeMenuItemBounds(bounds, rowIndex).Contains(position))
            {
                continue;
            }

            var item = items[rowIndex];
            if (item.Mode is { } mode)
            {
                SelectGarrisonBuilderGameMode(mode);
            }
            else if (item.Action == GarrisonBuilderMenuAction.MainMenu)
            {
                DisableGarrisonBuilderEditor("builder disabled");
            }
        }

        _builderGameModeMenuOpen = false;
        return true;
    }

    private void SelectGarrisonBuilderGameMode(CustomMapBuilderGameMode mode)
    {
        _builderSelectedGameMode = mode;
        _builderSelectedEntityType = string.Empty;
        _builderTooltipIndex = -1;
        RefreshGarrisonBuilderControlPointCapTimeMultipliersOnMap();
        if (IsEditingGarrisonBuilderControlPointEntity())
        {
            SyncGarrisonBuilderControlPointPropertyEditorFields();
        }

        _builderStatus = $"gamemode: {GetGarrisonBuilderModeLabel(_builderSelectedGameMode)}";
    }

    private void RefreshGarrisonBuilderControlPointCapTimeMultipliersOnMap()
    {
        var totalControlPoints = CountGarrisonBuilderControlPointsForCapTimeRules();
        var setupMode = HasGarrisonBuilderControlPointSetupGates();
        for (var index = 0; index < _builderEntities.Count; index += 1)
        {
            var entity = _builderEntities[index];
            if (!ControlPointOwnershipResolver.IsControlPointEntity(entity.Type)
                || ControlPointCapTimeMultiplierMetadata.IsCustom(entity.Properties))
            {
                continue;
            }

            var properties = new Dictionary<string, string>(entity.Properties, StringComparer.OrdinalIgnoreCase);
            var controlPointIndex = ControlPointOwnershipResolver.ResolveControlPointIndex(entity);
            properties[ControlPointCapTimeMultiplierMetadata.PropertyKey] =
                ControlPointCapTimeMultiplierMetadata.ToPropertyValue(
                    ControlPointCapTimeMultiplierMetadata.ResolveAutoMultiplier(
                        totalControlPoints,
                        controlPointIndex,
                        setupMode));
            properties.Remove(ControlPointCapTimeMultiplierMetadata.CustomPropertyKey);
            _builderEntities[index] = entity with { Properties = properties };
        }
    }

    private static CustomMapBuilderGameMode[] GetGarrisonBuilderModeMenuModes()
    {
        return
        [
            CustomMapBuilderGameMode.Free,
            CustomMapBuilderGameMode.Scr,
            CustomMapBuilderGameMode.CaptureTheFlag,
            CustomMapBuilderGameMode.ControlPoint,
            CustomMapBuilderGameMode.AttackDefenseControlPoint,
            CustomMapBuilderGameMode.KingOfTheHill,
            CustomMapBuilderGameMode.DualKingOfTheHill,
            CustomMapBuilderGameMode.Arena,
            CustomMapBuilderGameMode.Generator,
        ];
    }

    private static GarrisonBuilderModeMenuItem[] GetGarrisonBuilderModeMenuItems()
    {
        return
        [
            new("Free", CustomMapBuilderGameMode.Free),
            new("SCR", CustomMapBuilderGameMode.Scr),
            new("CTF", CustomMapBuilderGameMode.CaptureTheFlag),
            new("Control Point", CustomMapBuilderGameMode.ControlPoint),
            new("A/D CP", CustomMapBuilderGameMode.AttackDefenseControlPoint),
            new("KOTH", CustomMapBuilderGameMode.KingOfTheHill),
            new("Dual KOTH", CustomMapBuilderGameMode.DualKingOfTheHill),
            new("Arena", CustomMapBuilderGameMode.Arena),
            new("Generator", CustomMapBuilderGameMode.Generator),
            new("Main menu", null, GarrisonBuilderMenuAction.MainMenu),
            new("Back", null, GarrisonBuilderMenuAction.Back),
        ];
    }

    private static string GetGarrisonBuilderModeLabel(CustomMapBuilderGameMode mode)
    {
        return mode switch
        {
            CustomMapBuilderGameMode.CaptureTheFlag => "CTF",
            CustomMapBuilderGameMode.ControlPoint => "CP",
            CustomMapBuilderGameMode.AttackDefenseControlPoint => "A/D CP",
            CustomMapBuilderGameMode.KingOfTheHill => "KOTH",
            CustomMapBuilderGameMode.DualKingOfTheHill => "DKOTH",
            CustomMapBuilderGameMode.Arena => "Arena",
            CustomMapBuilderGameMode.Generator => "Generator",
            CustomMapBuilderGameMode.Scr => "SCR",
            _ => "Free",
        };
    }

    private bool ShouldBlockGameplayForGarrisonBuilder()
    {
        return _builderEditorEnabled;
    }

    private readonly record struct GarrisonBuilderHistorySnapshot(
        CustomMapBuilderDocument Document,
        CustomMapBuilderEntity[] Entities);

    private void ClearGarrisonBuilderHistory()
    {
        _builderUndoStack.Clear();
        _builderRedoStack.Clear();
    }

    private void RecordGarrisonBuilderHistory()
    {
        if (_builderHistoryCaptureSuspended || !_builderEditorEnabled)
        {
            return;
        }

        _builderUndoStack.Add(CreateGarrisonBuilderHistorySnapshot());
        while (_builderUndoStack.Count > GarrisonBuilderHistoryCapacity)
        {
            _builderUndoStack.RemoveAt(0);
        }

        _builderRedoStack.Clear();
    }

    private GarrisonBuilderHistorySnapshot CreateGarrisonBuilderHistorySnapshot()
    {
        return new GarrisonBuilderHistorySnapshot(
            _builderDocument,
            _builderEntities.Select(static entity => entity).ToArray());
    }

    private void RestoreGarrisonBuilderHistorySnapshot(GarrisonBuilderHistorySnapshot snapshot)
    {
        _builderHistoryCaptureSuspended = true;
        try
        {
            _builderDocument = snapshot.Document;
            _builderEntities.Clear();
            _builderEntities.AddRange(snapshot.Entities);
            ClearGarrisonBuilderMapEntitySelection();
            _builderEntityDragging = false;
            _builderAreaSelectDragging = false;
            _builderActiveResizeHandle = GarrisonBuilderResizeHandle.None;
            ClearGarrisonBuilderHiddenEntities();
            CloseGarrisonBuilderPropertyEditor(applyChanges: false);
            CloseGarrisonBuilderEntityContextMenu();
            _builderLayerParallaxDialogOpen = false;
            _builderLayerParallaxDialogLayerIndex = -1;
            ClearGarrisonBuilderResourceTextureCache();
            _builderLoadedBackgroundPath = string.Empty;
            _builderLoadedWalkmaskPath = string.Empty;
            _builderLoadedEmbeddedWalkmaskSection = string.Empty;
            LoadGarrisonBuilderEditorAssets();
            _builderDirty = true;
            _builderStatus = "builder history restored";
        }
        finally
        {
            _builderHistoryCaptureSuspended = false;
        }
    }

    private bool TryUndoGarrisonBuilder()
    {
        if (_builderUndoStack.Count == 0)
        {
            _builderStatus = "nothing to undo";
            return false;
        }

        _builderRedoStack.Add(CreateGarrisonBuilderHistorySnapshot());
        while (_builderRedoStack.Count > GarrisonBuilderHistoryCapacity)
        {
            _builderRedoStack.RemoveAt(0);
        }

        var snapshot = _builderUndoStack[^1];
        _builderUndoStack.RemoveAt(_builderUndoStack.Count - 1);
        RestoreGarrisonBuilderHistorySnapshot(snapshot);
        _builderStatus = "undo";
        return true;
    }

    private bool TryRedoGarrisonBuilder()
    {
        if (_builderRedoStack.Count == 0)
        {
            _builderStatus = "nothing to redo";
            return false;
        }

        _builderUndoStack.Add(CreateGarrisonBuilderHistorySnapshot());
        while (_builderUndoStack.Count > GarrisonBuilderHistoryCapacity)
        {
            _builderUndoStack.RemoveAt(0);
        }

        var snapshot = _builderRedoStack[^1];
        _builderRedoStack.RemoveAt(_builderRedoStack.Count - 1);
        RestoreGarrisonBuilderHistorySnapshot(snapshot);
        _builderStatus = "redo";
        return true;
    }

    private void UpdateGarrisonBuilderTransientStatus(float deltaSeconds)
    {
        if (_builderTransientStatusSecondsRemaining <= 0f)
        {
            return;
        }

        _builderTransientStatusSecondsRemaining = MathF.Max(0f, _builderTransientStatusSecondsRemaining - deltaSeconds);
        if (_builderTransientStatusSecondsRemaining <= 0f)
        {
            _builderTransientStatus = string.Empty;
        }
    }

    private void ShowGarrisonBuilderTransientStatus(string message)
    {
        _builderTransientStatus = message;
        _builderTransientStatusSecondsRemaining = GarrisonBuilderTransientStatusDurationSeconds;
    }

    private void DrawGarrisonBuilderTransientStatus()
    {
        if (_builderTransientStatusSecondsRemaining <= 0f || string.IsNullOrEmpty(_builderTransientStatus))
        {
            return;
        }

        const float scale = 1.05f;
        var textWidth = MeasureBitmapFontWidth(_builderTransientStatus, scale);
        var mapArea = _builderUseModernUi
            ? GetModernGarrisonBuilderMapViewport()
            : new Rectangle(0, 0, BuilderViewportWidth, BuilderViewportHeight);
        const int padding = 10;
        var boxWidth = (int)MathF.Ceiling(textWidth) + (padding * 2);
        var boxHeight = 28;
        var box = new Rectangle(
            mapArea.X + ((mapArea.Width - boxWidth) / 2),
            mapArea.Y + ((mapArea.Height - boxHeight) / 2),
            boxWidth,
            boxHeight);
        _spriteBatch.Draw(_pixel, box, new Color(0, 0, 0, 190));
        DrawBitmapFontText(
            _builderTransientStatus,
            new Vector2(box.X + padding, box.Y + 7f),
            new Color(255, 240, 200),
            scale);
    }

    private void TryHandleGarrisonBuilderGridAlignShortcut(KeyboardState keyboard)
    {
        if (!IsKeyPressed(keyboard, Keys.Space))
        {
            return;
        }

        _builderGridAlign = !_builderGridAlign;
        ShowGarrisonBuilderTransientStatus(_builderGridAlign ? "Grid align: On" : "Grid align: Off");
    }

    private bool TryHandleGarrisonBuilderSaveShortcuts(KeyboardState keyboard)
    {
        if (!_builderCtrlHeld || !IsKeyPressed(keyboard, Keys.S))
        {
            return false;
        }

        if (_builderShiftHeld)
        {
            BeginEditingGarrisonBuilderPath(GarrisonBuilderPathField.Save);
        }
        else
        {
            SaveGarrisonBuilderDocument();
        }

        return true;
    }

    private bool TryHandleGarrisonBuilderHistoryShortcuts(KeyboardState keyboard)
    {
        var controlHeld = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);
        if (!controlHeld)
        {
            return false;
        }

        if (IsKeyPressed(keyboard, Keys.Z))
        {
            return TryUndoGarrisonBuilder();
        }

        if (IsKeyPressed(keyboard, Keys.Y))
        {
            return TryRedoGarrisonBuilder();
        }

        if (IsKeyPressed(keyboard, Keys.C)
            && _builderSelectedEntityIndex >= 0
            && _builderSelectedEntityIndex < _builderEntities.Count)
        {
            CloneGarrisonBuilderSelectedEntity();
            return true;
        }

        return false;
    }

    private readonly record struct LegacyBuilderActionDefinition(string Label, bool Toggle);

    private enum GarrisonBuilderMenuAction
    {
        None,
        MainMenu,
        Back,
    }

    private readonly record struct GarrisonBuilderModeMenuItem(
        string Label,
        CustomMapBuilderGameMode? Mode = null,
        GarrisonBuilderMenuAction Action = GarrisonBuilderMenuAction.None);

    private readonly record struct GarrisonBuilderEntityMetrics(
        float Width,
        float Height,
        float CenterX,
        float CenterY,
        float OffsetX,
        float OffsetY,
        float MirroredOffsetX);

    private readonly record struct RectangleF(float X, float Y, float Width, float Height)
    {
        public float Right => X + Width;

        public float Bottom => Y + Height;

        public bool Contains(float x, float y) => x >= X && x <= Right && y >= Y && y <= Bottom;
    }
}
