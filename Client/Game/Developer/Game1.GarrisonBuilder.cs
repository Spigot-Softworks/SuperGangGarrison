#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        Metadata,
        PlacementEntity,
    }

    private enum GarrisonBuilderPropertyEditMode
    {
        List,
        NewKey,
        EditValue,
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
    private const int LegacyBuilderButtonHeight = 17;
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
    private bool _builderShowBackground = true;
    private bool _builderShowWalkmask = true;
    private bool _builderShowGrid;
    private bool _builderDirty;
    private CustomMapBuilderDocument _builderDocument = CustomMapBuilderDocument.CreateEmpty("new_map");
    private readonly List<CustomMapBuilderEntity> _builderEntities = new();
    private string _builderStatus = "builder disabled";
    private string _builderSavePath = string.Empty;
    private Vector2 _builderCamera;
    private string _builderSelectedEntityType = "redspawn";
    private Texture2D? _builderBackgroundTexture;
    private Texture2D? _builderWalkmaskTexture;
    private Texture2D? _builderDefaultBackgroundTexture;
    private Texture2D? _builderDefaultWalkmaskTexture;
    private string _builderLoadedBackgroundPath = string.Empty;
    private string _builderLoadedWalkmaskPath = string.Empty;
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
    private CustomMapBuilderGameMode _builderSelectedGameMode = CustomMapBuilderGameMode.Free;
    private bool _builderSymmetry;
    private bool _builderScaleMode = true;
    private bool _builderFastScrolling;
    private bool _builderPlacementDragging;
    private bool _builderPlacementScaleLock;
    private bool _builderEraseDragging;
    private Vector2 _builderPlacementStartWorld;
    private Vector2 _builderPlacementCurrentWorld;
    private Vector2 _builderEraseStartWorld;
    private Vector2 _builderEraseCurrentWorld;
    private bool _builderShowForeground = true;
    private bool _builderEditingLayerOffsets;
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
        new("Edit metadata", false),
        new("Add resource", false),
        new("Get resources", false),
        new("Load entities", false),
        new("Save entities", false),
        new("Clear entities", false),
    ];

    private void UpdateGarrisonBuilderEditor(KeyboardState keyboard, MouseState mouse, float deltaSeconds)
    {
        if (IsKeyPressed(keyboard, Keys.F4))
        {
            if (_builderEditorEnabled)
            {
                DisableGarrisonBuilderEditor("builder disabled");
            }
            else if (CanOpenGarrisonBuilderFromShortcut())
            {
                EnableGarrisonBuilderEditor();
            }
        }

        if (!_builderEditorEnabled)
        {
            return;
        }

        LoadGarrisonBuilderEditorAssets();
        UpdateLegacyGarrisonBuilderAnimation(deltaSeconds);
        if (UpdateGarrisonBuilderPropertyEditor(keyboard, mouse))
        {
            return;
        }

        if (UpdateGarrisonBuilderPathKeyboard(keyboard))
        {
            return;
        }

        if (UpdateGarrisonBuilderLayerOffsetEditing(mouse))
        {
            return;
        }

        if (UpdateLegacyGarrisonBuilderPanelDrag(mouse))
        {
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

        if (IsKeyPressed(keyboard, Keys.Delete) && _builderEntities.Count > 0)
        {
            _builderEntities.RemoveAt(_builderEntities.Count - 1);
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
        var mapArea = new Rectangle(0, 0, ViewportWidth, ViewportHeight);
        _spriteBatch.Draw(_pixel, mapArea, _builderShowBackground ? new Color(190, 190, 190, 255) : new Color(20, 20, 20, 255));
        DrawGarrisonBuilderMap(mapArea);
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
    }

    private void DrawGarrisonBuilderMap(Rectangle mapArea)
    {
        var mapOffset = -_builderCamera;
        var backgroundTexture = _builderBackgroundTexture ?? _builderDefaultBackgroundTexture;
        if (_builderShowBackground && backgroundTexture is not null)
        {
            _spriteBatch.Draw(
                backgroundTexture,
                mapOffset,
                null,
                Color.White,
                0f,
                Vector2.Zero,
                new Vector2(_builderDocument.Scale),
                SpriteEffects.None,
                0f);
        }

        var walkmaskTexture = _builderWalkmaskTexture ?? _builderDefaultWalkmaskTexture;
        if (_builderShowWalkmask && walkmaskTexture is not null)
        {
            _spriteBatch.Draw(
                walkmaskTexture,
                mapOffset,
                null,
                Color.White * 0.45f,
                0f,
                Vector2.Zero,
                new Vector2(_builderDocument.Scale),
                SpriteEffects.None,
                0f);
        }

        if (_builderShowGrid)
        {
            DrawGarrisonBuilderGrid(mapArea);
        }

        DrawGarrisonBuilderPlacementPreview();
        DrawGarrisonBuilderErasePreview();

        foreach (var entity in _builderEntities)
        {
            var screen = new Vector2(entity.X, entity.Y) - _builderCamera;
            if (CustomMapBuilderEntityCatalog.TryGetDefinition(entity.Type, out var definition)
                && TryDrawGarrisonBuilderEntitySprite(definition, entity, Color.White * 0.9f))
            {
                DrawGarrisonBuilderText(entity.Type, screen + new Vector2(7f, -8f), Color.White, 0.95f);
                continue;
            }

            var bounds = new Rectangle((int)MathF.Round(screen.X) - 5, (int)MathF.Round(screen.Y) - 5, 10, 10);
            _spriteBatch.Draw(_pixel, bounds, GetGarrisonBuilderEntityColor(entity.Type));
            DrawGarrisonBuilderText(entity.Type, screen + new Vector2(7f, -8f), Color.White, 0.95f);
        }
    }

    private void DrawGarrisonBuilderGrid(Rectangle mapArea)
    {
        const int gridSize = 12;
        var startX = (int)MathF.Floor(_builderCamera.X / gridSize) * gridSize;
        var endX = (int)MathF.Ceiling((_builderCamera.X + mapArea.Width) / gridSize) * gridSize;
        var startY = (int)MathF.Floor(_builderCamera.Y / gridSize) * gridSize;
        var endY = (int)MathF.Ceiling((_builderCamera.Y + mapArea.Height) / gridSize) * gridSize;
        for (var x = startX; x <= endX; x += gridSize)
        {
            for (var y = startY; y <= endY; y += gridSize)
            {
                var screen = new Vector2(x - _builderCamera.X, y - _builderCamera.Y);
                if (_builderGridSprite is not null && _builderGridSprite.Frames.Count > 0)
                {
                    DrawLoadedSpriteFrame(_builderGridSprite.Frames[0], screen, null, Color.White, 0f, Vector2.Zero, Vector2.One, SpriteEffects.None, 0f);
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
            if (label.Length > 20)
            {
                label = string.Concat(label.AsSpan(0, 17), "...");
            }

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
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewportWidth, ViewportHeight), new Color(0, 0, 0, 145));
        DrawMenuPanelBackdrop(bounds, 1f);
        DrawBitmapFontText("Garrison Builder", new Vector2(bounds.X + 18f, bounds.Y + 16f), Color.White, 1.15f);

        for (var index = 0; index < items.Length; index += 1)
        {
            var rowBounds = GetGarrisonBuilderModeMenuItemBounds(bounds, index);
            var item = items[index];
            var selected = item.Mode is { } mode && mode == _builderSelectedGameMode;
            var hovered = rowBounds.Contains(mouse.Position);
            DrawMenuButtonScaled(rowBounds, item.Label, selected || hovered, 0.74f);
        }
    }

    private void DrawLegacyGarrisonBuilderPathPrompt()
    {
        if (_builderActivePathField == GarrisonBuilderPathField.None)
        {
            return;
        }

        var width = Math.Min(620, Math.Max(320, ViewportWidth - 80));
        var bounds = new Rectangle((ViewportWidth - width) / 2, 24, width, 66);
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

        var bounds = GetGarrisonBuilderPropertyEditorBounds();
        DrawLegacyBuilderPanelBody(bounds);
        DrawGarrisonBuilderText(_builderPropertyEditorTitle, bounds.X + 8, bounds.Y + 8, Color.Black, 0.92f);

        if (_builderPropertyEditMode == GarrisonBuilderPropertyEditMode.NewKey)
        {
            DrawGarrisonBuilderText("New property", bounds.X + 8, bounds.Y + 34, Color.Black, 0.82f);
            DrawGarrisonBuilderPropertyInputBox(new Rectangle(bounds.X + 8, bounds.Y + 56, bounds.Width - 16, 24), _builderPropertyEditBuffer);
            DrawGarrisonBuilderText("Enter: value  Esc: cancel", bounds.X + 8, bounds.Bottom - 22, Color.Black, 0.72f);
            return;
        }

        if (_builderPropertyEditMode == GarrisonBuilderPropertyEditMode.EditValue)
        {
            DrawGarrisonBuilderText(_builderPropertyEditKey, bounds.X + 8, bounds.Y + 34, Color.Black, 0.82f);
            DrawGarrisonBuilderPropertyInputBox(new Rectangle(bounds.X + 8, bounds.Y + 56, bounds.Width - 16, 24), _builderPropertyEditBuffer);
            DrawGarrisonBuilderText("Enter: save  Empty: delete  Esc: cancel", bounds.X + 8, bounds.Bottom - 22, Color.Black, 0.72f);
            return;
        }

        var rows = GetGarrisonBuilderPropertyRows();
        var visibleRows = GetGarrisonBuilderPropertyVisibleRows(bounds);
        _builderPropertyScrollIndex = Math.Clamp(_builderPropertyScrollIndex, 0, Math.Max(0, rows.Count - visibleRows));
        var y = bounds.Y + 34;
        for (var visibleIndex = 0; visibleIndex < visibleRows; visibleIndex += 1)
        {
            var index = _builderPropertyScrollIndex + visibleIndex;
            var rowBounds = new Rectangle(bounds.X + 8, y, bounds.Width - 16, 20);
            if (index >= rows.Count)
            {
                _spriteBatch.Draw(_pixel, rowBounds, new Color(178, 178, 178));
                y += 22;
                continue;
            }

            var hovered = rowBounds.Contains(mouse.Position);
            _spriteBatch.Draw(_pixel, rowBounds, hovered ? new Color(210, 210, 210) : new Color(184, 184, 184));
            var key = rows[index];
            var value = _builderPropertyEditorValues[key];
            var displayValue = value.Length > 36 ? string.Concat(value.AsSpan(0, 33), "...") : value;
            DrawGarrisonBuilderText($"{key}: {displayValue}", rowBounds.Location.ToVector2() + new Vector2(4f, 2f), Color.Black, 0.95f);
            y += 22;
        }

        if (rows.Count > visibleRows)
        {
            DrawGarrisonBuilderText($"{_builderPropertyScrollIndex + 1}-{Math.Min(rows.Count, _builderPropertyScrollIndex + visibleRows)} / {rows.Count}", bounds.Right - 84, bounds.Y + 8, Color.Black, 0.66f);
        }

        var addBounds = GetGarrisonBuilderPropertyAddBounds(bounds);
        _spriteBatch.Draw(_pixel, addBounds, addBounds.Contains(mouse.Position) ? new Color(220, 220, 220) : new Color(190, 190, 190));
        DrawGarrisonBuilderText("Add new property", addBounds.Location.ToVector2() + new Vector2(4f, 2f), Color.Black, 0.95f);
        DrawGarrisonBuilderText("Click bool to toggle, other values to edit. Esc closes.", bounds.X + 8, bounds.Bottom - 22, Color.Black, 0.66f);
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
        var width = Math.Min(ViewportWidth - 8, (int)MathF.Ceiling(MeasureGarrisonBuilderText(text, 1f).X) + 12);
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
        var x = mousePosition.X + width + 14 > ViewportWidth ? mousePosition.X - width - 12 : mousePosition.X + 12;
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
        DrawGarrisonBuilderButton(buttons[3], "Save F7", false, CanSaveGarrisonBuilderDocument(), mouse);
        y += BuilderButtonHeight + 14;

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
        DrawGarrisonBuilderText($"Scale: {_builderDocument.Scale:0.##}", x, y, Color.White, 0.8f);
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

    private void DrawGarrisonBuilderEntityIcon(CustomMapBuilderEntityDefinition definition, Rectangle bounds)
    {
        if (_builderEntityButtonSprite is not null
            && definition.IconFrame >= 0
            && definition.IconFrame < _builderEntityButtonSprite.Frames.Count)
        {
            DrawLoadedSpriteFrame(_builderEntityButtonSprite.Frames[definition.IconFrame], bounds, Color.White);
            return;
        }

        DrawGarrisonBuilderText(definition.Type[..Math.Min(2, definition.Type.Length)].ToUpperInvariant(), bounds.X + 6, bounds.Y + 7, Color.White, 0.7f);
    }

    private void DrawGarrisonBuilderPathRow(GarrisonBuilderPathField field, string label, int x, int y, int width, MouseState mouse)
    {
        const int labelWidth = 44;
        const int actionWidth = 58;
        var fieldBounds = new Rectangle(x + labelWidth, y, Math.Max(1, width - labelWidth - actionWidth - BuilderButtonGap), 24);
        var actionBounds = new Rectangle(fieldBounds.Right + BuilderButtonGap, y, actionWidth, 24);
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
                _builderActionPanelOffset.X = Math.Clamp(_builderActionPanelOffset.X + delta.X, 0, Math.Max(0, ViewportWidth - LegacyBuilderHeaderWidth));
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
            case "Edit metadata":
                BeginEditingGarrisonBuilderMetadata();
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

    private void DrawGarrisonBuilderResourcePreview(Texture2D texture, Rectangle bounds)
    {
        var scale = Math.Min(bounds.Width / (float)texture.Width, bounds.Height / (float)texture.Height);
        _spriteBatch.Draw(texture, bounds.Location.ToVector2(), null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
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

    private void ToggleGarrisonBuilderLayerOffsetEditing()
    {
        if (_builderLayerIndex is < 0 or > 6)
        {
            _builderStatus = "select a parallax layer for offsets";
            return;
        }

        _builderEditingLayerOffsets = !_builderEditingLayerOffsets;
        _builderLayerOffsetDragging = false;
        if (_builderEditingLayerOffsets)
        {
            _builderLayerOffsetHoldMouse = Mouse.GetState().Position;
        }

        _builderStatus = _builderEditingLayerOffsets
            ? $"editing layer {_builderLayerIndex + 1} offsets"
            : "layer offsets saved";
    }

    private bool UpdateGarrisonBuilderLayerOffsetEditing(MouseState mouse)
    {
        if (!_builderEditingLayerOffsets || _builderLayerIndex is < 0 or > 6)
        {
            return false;
        }

        if (GetLegacyGarrisonBuilderLayerBounds().Contains(mouse.Position)
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
        var x = (ViewportWidth / 2) + (int)MathF.Round((layer.XFactor - 1f) * 100f);
        var y = (ViewportHeight / 2) + (int)MathF.Round((layer.YFactor - 1f) * 100f);
        var color = _builderLayerOffsetDragging ? new Color(255, 232, 80) : new Color(120, 220, 255);
        _spriteBatch.Draw(_pixel, new Rectangle(0, y, ViewportWidth, 1), color);
        _spriteBatch.Draw(_pixel, new Rectangle(x, 0, 1, ViewportHeight), color);
        _spriteBatch.Draw(_pixel, new Rectangle((ViewportWidth / 2) - 5, ViewportHeight / 2, 10, 1), Color.White);
        _spriteBatch.Draw(_pixel, new Rectangle(ViewportWidth / 2, (ViewportHeight / 2) - 5, 1, 10), Color.White);

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
            Math.Clamp(_builderActionPanelOffset.X, 0, Math.Max(0, ViewportWidth - LegacyBuilderHeaderWidth)),
            Math.Clamp(ViewportHeight - height + _builderActionPanelOffset.Y, LegacyBuilderButtonHeight, Math.Max(LegacyBuilderButtonHeight, ViewportHeight - height)),
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
            Math.Clamp(ViewportWidth - width - 1 + _builderEntityPanelOffset.X, 1, Math.Max(1, ViewportWidth - width - 1)),
            Math.Clamp(_builderEntityPanelOffset.Y, 0, Math.Max(0, ViewportHeight - (2 * LegacyBuilderEntityButtonSize) - 1)),
            width,
            height);
    }

    private Rectangle GetLegacyGarrisonBuilderLayerBounds()
    {
        return new Rectangle(
            Math.Clamp(ViewportWidth - LegacyBuilderLayerWidth - 1 + _builderLayerPanelOffset.X, 1, Math.Max(1, ViewportWidth - LegacyBuilderLayerWidth - 1)),
            Math.Clamp(ViewportHeight - LegacyBuilderLayerHeight - LegacyBuilderButtonHeight - 1 + _builderLayerPanelOffset.Y, 0, Math.Max(0, ViewportHeight - LegacyBuilderLayerHeight - LegacyBuilderButtonHeight - 1)),
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
        var width = Math.Min(420, Math.Max(300, ViewportWidth - 80));
        var height = 64 + (items.Length * 34);
        return new Rectangle(
            Math.Clamp((ViewportWidth - width) / 2, 20, Math.Max(20, ViewportWidth - width - 20)),
            Math.Clamp((ViewportHeight - height) / 2, 20, Math.Max(20, ViewportHeight - height - 20)),
            width,
            height);
    }

    private static Rectangle GetGarrisonBuilderModeMenuItemBounds(Rectangle menuBounds, int index)
    {
        return new Rectangle(menuBounds.X + 18, menuBounds.Y + 50 + (index * 34), menuBounds.Width - 36, 28);
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
        var snappedX = MathF.Round(worldPosition.X / 6f) * 6f;
        var snappedY = MathF.Round(worldPosition.Y / 6f) * 6f;
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
        _builderPlacementDragging = true;
        _builderPlacementScaleLock = true;
        _builderPlacementStartWorld = SnapGarrisonBuilderPoint(worldPosition);
        _builderPlacementCurrentWorld = _builderPlacementStartWorld;
    }

    private void UpdateGarrisonBuilderPlacementPreview(MouseState mouse)
    {
        if (_builderPlacementDragging)
        {
            _builderPlacementCurrentWorld = SnapGarrisonBuilderPoint(mouse.Position.ToVector2() + _builderCamera);
            if (_builderPlacementScaleLock
                && (MathF.Abs(_builderPlacementCurrentWorld.X - _builderPlacementStartWorld.X) > 3f
                    || MathF.Abs(_builderPlacementCurrentWorld.Y - _builderPlacementStartWorld.Y) > 3f))
            {
                _builderPlacementScaleLock = false;
            }
        }

        if (_builderEraseDragging)
        {
            _builderEraseCurrentWorld = SnapGarrisonBuilderPoint(mouse.Position.ToVector2() + _builderCamera);
        }
    }

    private void CommitGarrisonBuilderPlacement(Vector2 worldPosition)
    {
        if (!_builderPlacementDragging || string.IsNullOrWhiteSpace(_builderSelectedEntityType))
        {
            _builderPlacementDragging = false;
            return;
        }

        _builderPlacementCurrentWorld = SnapGarrisonBuilderPoint(worldPosition);
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
        if (!CustomMapBuilderEntityCatalog.TryGetDefinition(_builderSelectedEntityType, out var definition))
        {
            entities.Add(CustomMapBuilderEntity.Create(_builderSelectedEntityType, _builderPlacementStartWorld.X, _builderPlacementStartWorld.Y).NormalizeForEditing());
            return entities;
        }

        var metrics = GetGarrisonBuilderEntityMetrics(definition);
        if (_builderScaleMode && IsGarrisonBuilderDefinitionScalable(definition) && !_builderPlacementScaleLock)
        {
            var right = MathF.Max(_builderPlacementStartWorld.X + 6f, _builderPlacementCurrentWorld.X + metrics.CenterX);
            var bottom = MathF.Max(_builderPlacementStartWorld.Y + 6f, _builderPlacementCurrentWorld.Y + metrics.CenterY);
            var xScale = MathF.Max(0.01f, (right - _builderPlacementStartWorld.X) / metrics.Width);
            var yScale = MathF.Max(0.01f, (bottom - _builderPlacementStartWorld.Y) / metrics.Height);
            AddGarrisonBuilderPlacementEntity(entities, definition, _builderPlacementStartWorld.X - metrics.CenterX + (metrics.OffsetX * xScale), _builderPlacementStartWorld.Y - metrics.CenterY + (metrics.OffsetY * yScale), xScale, yScale, mirrored: false);

            if (_builderSymmetry && TryGetGarrisonBuilderMirroredDefinition(definition, out var mirroredDefinition))
            {
                AddGarrisonBuilderPlacementEntity(
                    entities,
                    mirroredDefinition,
                    GetGarrisonBuilderMapWidth() - _builderPlacementStartWorld.X + metrics.CenterX + (metrics.OffsetX * xScale) - (right - _builderPlacementStartWorld.X),
                    _builderPlacementStartWorld.Y - metrics.CenterY + (metrics.OffsetY * yScale),
                    xScale,
                    yScale,
                    mirrored: true);
            }

            return entities;
        }

        var endX = _builderPlacementStartWorld.X + MathF.Max(metrics.Width, MathF.Ceiling((_builderPlacementCurrentWorld.X - _builderPlacementStartWorld.X) / metrics.Width) * metrics.Width);
        var endY = _builderPlacementStartWorld.Y + MathF.Max(metrics.Height, MathF.Ceiling((_builderPlacementCurrentWorld.Y - _builderPlacementStartWorld.Y) / metrics.Height) * metrics.Height);
        for (var x = _builderPlacementStartWorld.X - metrics.CenterX; x + metrics.CenterX < endX; x += metrics.Width)
        {
            for (var y = _builderPlacementStartWorld.Y - metrics.CenterY; y + metrics.CenterY < endY; y += metrics.Height)
            {
                AddGarrisonBuilderPlacementEntity(entities, definition, x + metrics.OffsetX, y + metrics.OffsetY, 1f, 1f, mirrored: false);
                if (_builderSymmetry && TryGetGarrisonBuilderMirroredDefinition(definition, out var mirroredDefinition))
                {
                    AddGarrisonBuilderPlacementEntity(entities, mirroredDefinition, GetGarrisonBuilderMapWidth() - x + metrics.MirroredOffsetX, y + metrics.OffsetY, 1f, 1f, mirrored: true);
                }
            }
        }

        return entities;
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
        var removed = RemoveGarrisonBuilderEntitiesInRectangle(eraseRect);
        if (_builderSymmetry)
        {
            var width = GetGarrisonBuilderMapWidth();
            var mirroredRect = new RectangleF(width - eraseRect.Right, eraseRect.Y, eraseRect.Width, eraseRect.Height);
            removed += RemoveGarrisonBuilderEntitiesInRectangle(mirroredRect);
        }

        _builderEraseDragging = false;
        if (removed > 0)
        {
            UpdateGarrisonBuilderDocumentEntities();
            _builderDirty = true;
            _builderStatus = $"removed {removed} entities";
        }
        else
        {
            _builderStatus = "no entities in erase area";
        }
    }

    private int RemoveGarrisonBuilderEntitiesInRectangle(RectangleF rectangle)
    {
        var removed = 0;
        for (var index = _builderEntities.Count - 1; index >= 0; index -= 1)
        {
            var entity = _builderEntities[index];
            if (rectangle.Contains(entity.X, entity.Y))
            {
                _builderEntities.RemoveAt(index);
                removed += 1;
            }
        }

        return removed;
    }

    private void DrawGarrisonBuilderPlacementPreview()
    {
        if (!_builderPlacementDragging || string.IsNullOrWhiteSpace(_builderSelectedEntityType))
        {
            return;
        }

        if (!CustomMapBuilderEntityCatalog.TryGetDefinition(_builderSelectedEntityType, out var definition))
        {
            return;
        }

        var color = new Color(60, 220, 90, 150);
        foreach (var entity in BuildGarrisonBuilderPlacementEntities())
        {
            var previewDefinition = CustomMapBuilderEntityCatalog.TryGetDefinition(entity.Type, out var entityDefinition)
                ? entityDefinition
                : definition;
            if (TryDrawGarrisonBuilderEntitySprite(previewDefinition, entity, entity.Type.Equals(_builderSelectedEntityType, StringComparison.OrdinalIgnoreCase) ? Color.White * 0.7f : Color.White * 0.45f))
            {
                continue;
            }

            var metrics = GetGarrisonBuilderEntityMetrics(previewDefinition);
            var rect = new Rectangle(
                (int)MathF.Round(entity.X - _builderCamera.X),
                (int)MathF.Round(entity.Y - _builderCamera.Y),
                Math.Max(6, (int)MathF.Round(metrics.Width * entity.XScale)),
                Math.Max(6, (int)MathF.Round(metrics.Height * entity.YScale)));
            DrawGarrisonBuilderRectangleOutline(rect, entity.Type.Equals(_builderSelectedEntityType, StringComparison.OrdinalIgnoreCase) ? color : new Color(235, 80, 80, 140));
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
            var mirrored = new RectangleF(GetGarrisonBuilderMapWidth() - rect.Right, rect.Y, rect.Width, rect.Height);
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
        return new Rectangle(
            (int)MathF.Round(rectangle.X - _builderCamera.X),
            (int)MathF.Round(rectangle.Y - _builderCamera.Y),
            Math.Max(1, (int)MathF.Round(rectangle.Width)),
            Math.Max(1, (int)MathF.Round(rectangle.Height)));
    }

    private static RectangleF CreateWorldRectangle(Vector2 a, Vector2 b)
    {
        var left = MathF.Min(a.X, b.X);
        var top = MathF.Min(a.Y, b.Y);
        var right = MathF.Max(a.X, b.X);
        var bottom = MathF.Max(a.Y, b.Y);
        return new RectangleF(left, top, MathF.Max(1f, right - left), MathF.Max(1f, bottom - top));
    }

    private static Vector2 SnapGarrisonBuilderPoint(Vector2 worldPosition)
    {
        return new Vector2(MathF.Round(worldPosition.X / 6f) * 6f, MathF.Round(worldPosition.Y / 6f) * 6f);
    }

    private float GetGarrisonBuilderMapWidth()
    {
        if (_builderBackgroundTexture is not null)
        {
            return _builderBackgroundTexture.Width * _builderDocument.Scale;
        }

        if (_builderWalkmaskTexture is not null)
        {
            return _builderWalkmaskTexture.Width * _builderDocument.Scale;
        }

        return Math.Max(ViewportWidth, _builderEntities.Count == 0 ? ViewportWidth : _builderEntities.Max(static entity => entity.X) + 200f);
    }

    private float GetGarrisonBuilderMapHeight()
    {
        if (_builderBackgroundTexture is not null)
        {
            return _builderBackgroundTexture.Height * _builderDocument.Scale;
        }

        if (_builderWalkmaskTexture is not null)
        {
            return _builderWalkmaskTexture.Height * _builderDocument.Scale;
        }

        return Math.Max(ViewportHeight, _builderEntities.Count == 0 ? ViewportHeight : _builderEntities.Max(static entity => entity.Y) + 200f);
    }

    private void ClampGarrisonBuilderCamera()
    {
        var maxX = Math.Max(0f, GetGarrisonBuilderMapWidth() - ViewportWidth);
        var maxY = Math.Max(0f, GetGarrisonBuilderMapHeight() - ViewportHeight);
        _builderCamera.X = Math.Clamp(_builderCamera.X, 0f, maxX);
        _builderCamera.Y = Math.Clamp(_builderCamera.Y, 0f, maxY);
    }

    private bool TryDrawGarrisonBuilderEntitySprite(CustomMapBuilderEntityDefinition definition, CustomMapBuilderEntity entity, Color tint)
    {
        if (!TryGetGarrisonBuilderEntityFrame(definition, entity, out var frame, out var origin))
        {
            return false;
        }

        DrawLoadedSpriteFrame(
            frame,
            new Vector2(entity.X - _builderCamera.X, entity.Y - _builderCamera.Y),
            null,
            tint,
            0f,
            origin,
            new Vector2(entity.XScale, entity.YScale),
            SpriteEffects.None,
            0f);
        return true;
    }

    private GarrisonBuilderEntityMetrics GetGarrisonBuilderEntityMetrics(CustomMapBuilderEntityDefinition definition)
    {
        if (TryGetGarrisonBuilderEntityFrame(definition, out var frame, out var origin))
        {
            var width = Math.Max(1f, frame.Width);
            var height = Math.Max(1f, frame.Height);
            return new GarrisonBuilderEntityMetrics(
                width,
                height,
                MathF.Round(width / 12f) * 6f,
                MathF.Round(height / 12f) * 6f,
                origin.X,
                origin.Y,
                origin.X - width);
        }

        var (fallbackWidth, fallbackHeight) = GetGarrisonBuilderEntityBaseSize(definition.Type);
        return new GarrisonBuilderEntityMetrics(
            fallbackWidth,
            fallbackHeight,
            MathF.Round(fallbackWidth / 12f) * 6f,
            MathF.Round(fallbackHeight / 12f) * 6f,
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

        return TryGetGarrisonBuilderEntityFrame(definition, out frame, out origin);
    }

    private bool TryGetGarrisonBuilderEntityFrame(CustomMapBuilderEntityDefinition definition, out LoadedSpriteFrame frame, out Vector2 origin)
    {
        if (TryGetGarrisonBuilderEntityResourceFrame(definition, _builderPlacementPropertyOverrides, out frame, out origin))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(definition.EntitySpriteName))
        {
            var sprite = GetResolvedSprite(definition.EntitySpriteName);
            if (sprite is not null && sprite.Frames.Count > 0)
            {
                var frameIndex = Math.Clamp(definition.EntityImage, 0, sprite.Frames.Count - 1);
                frame = sprite.Frames[frameIndex];
                origin = sprite.Origin.ToVector2();
                return true;
            }
        }

        frame = default!;
        origin = Vector2.Zero;
        return false;
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

    private static (float Width, float Height) GetGarrisonBuilderEntityBaseSize(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "redteamgate" or "blueteamgate" or "redintelgate" or "blueintelgate" or "intelgatevertical" or "playerwall" or "bulletwall" or "leftdoor" or "rightdoor" => (6f, 60f),
            "redteamgate2" or "blueteamgate2" or "redintelgate2" or "blueintelgate2" or "intelgatehorizontal" or "playerwall_horizontal" or "bulletwall_horizontal" or "dropdownplatform" or "setupgate" => (60f, 6f),
            "medcabinet" => (32f, 48f),
            _ => (42f, 42f),
        };
    }

    private static bool IsGarrisonBuilderDefinitionScalable(CustomMapBuilderEntityDefinition definition)
    {
        return definition.DefaultProperties.ContainsKey("xscale") && definition.DefaultProperties.ContainsKey("yscale");
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

        return CustomMapBuilderEntity.Create(definition.Type, x, y, properties).NormalizeForEditing();
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
        if (TryApplyGarrisonBuilderNativePathDialog(field))
        {
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

        var result = InsertTextCharacterAtCursor(_builderPropertyEditBuffer, character, _builderPropertyCursorIndex, _builderPropertySelectionStart, 180);
        _builderPropertyEditBuffer = result.Text;
        _builderPropertyCursorIndex = result.CursorIndex;
        _builderPropertySelectionStart = result.SelectionStart;
        return true;
    }

    private void BeginEditingGarrisonBuilderMetadata()
    {
        _builderPropertyTarget = GarrisonBuilderPropertyTarget.Metadata;
        _builderPropertyEditMode = GarrisonBuilderPropertyEditMode.List;
        _builderPropertyEditorTitle = "Edit metadata";
        _builderPropertyScrollIndex = 0;
        _builderPropertyEditorValues = new Dictionary<string, string>(_builderDocument.Metadata, StringComparer.OrdinalIgnoreCase);
        _builderPropertyEditorValues.TryAdd("background", CustomMapBuilderDocument.DefaultBackgroundColor);
        _builderPropertyEditorValues.TryAdd("void", CustomMapBuilderDocument.DefaultVoidColor);
        _builderPropertyEditorValues["scale"] = _builderDocument.Scale.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _builderStatus = "editing metadata";
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
        var visibleRowIndex = (position.Y - (bounds.Y + 34)) / 22;
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
        var value = _builderPropertyEditorValues[key];
        if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            _builderPropertyEditorValues[key] = "false";
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else if (value.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            _builderPropertyEditorValues[key] = "true";
            MarkGarrisonBuilderPropertyEditorChanged();
        }
        else
        {
            BeginGarrisonBuilderPropertyTextEdit(key, value, GarrisonBuilderPropertyEditMode.EditValue);
        }

        return true;
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
                || key.Equals("yscale", StringComparison.OrdinalIgnoreCase))
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
            if (value.Length == 0)
            {
                _builderPropertyEditorValues.Remove(_builderPropertyEditKey);
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
            ApplyGarrisonBuilderPropertyEditorChanges();
        }

        _builderPropertyTarget = GarrisonBuilderPropertyTarget.None;
        _builderPropertyEditMode = GarrisonBuilderPropertyEditMode.List;
        _builderPropertyEditKey = string.Empty;
        _builderPropertyEditBuffer = string.Empty;
        _builderPropertyEditorTitle = string.Empty;
        _builderPropertyScrollIndex = 0;
    }

    private void ApplyGarrisonBuilderPropertyEditorChanges()
    {
        if (_builderPropertyTarget == GarrisonBuilderPropertyTarget.Metadata)
        {
            var metadata = new Dictionary<string, string>(_builderPropertyEditorValues, StringComparer.OrdinalIgnoreCase);
            var scale = _builderDocument.Scale;
            if (metadata.TryGetValue("scale", out var scaleText)
                && float.TryParse(scaleText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedScale)
                && parsedScale > 0f)
            {
                scale = parsedScale;
            }

            _builderDocument = _builderDocument with
            {
                Metadata = metadata,
                Scale = scale,
            };
            _builderDirty = true;
            _builderStatus = "metadata updated";
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

            _builderStatus = "placement properties updated";
        }
    }

    private void MarkGarrisonBuilderPropertyEditorChanged()
    {
        if (_builderPropertyTarget == GarrisonBuilderPropertyTarget.Metadata)
        {
            _builderDirty = true;
        }
    }

    private List<string> GetGarrisonBuilderPropertyRows()
    {
        return _builderPropertyEditorValues.Keys
            .Where(key => !IsSkippedGarrisonBuilderProperty(key))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int GetGarrisonBuilderPropertyVisibleRows(Rectangle editorBounds)
    {
        var availableHeight = GetGarrisonBuilderPropertyAddBounds(editorBounds).Y - (editorBounds.Y + 34) - 6;
        return Math.Max(1, availableHeight / 22);
    }

    private static bool IsSkippedGarrisonBuilderProperty(string key)
    {
        return key.Equals("type", StringComparison.OrdinalIgnoreCase)
            || key.Equals("xscale", StringComparison.OrdinalIgnoreCase)
            || key.Equals("yscale", StringComparison.OrdinalIgnoreCase);
    }

    private Rectangle GetGarrisonBuilderPropertyEditorBounds()
    {
        var width = Math.Min(420, Math.Max(320, ViewportWidth - 80));
        var height = 340;
        return new Rectangle((ViewportWidth - width) / 2, (ViewportHeight - height) / 2, width, height);
    }

    private static Rectangle GetGarrisonBuilderPropertyAddBounds(Rectangle editorBounds)
    {
        return new Rectangle(editorBounds.X + 8, editorBounds.Bottom - 48, editorBounds.Width - 16, 20);
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
        _builderEntities.RemoveAt(bestIndex);
        UpdateGarrisonBuilderDocumentEntities();
        _builderDirty = true;
        _builderStatus = $"removed {removed}";
    }

    private void UpdateGarrisonBuilderDocumentEntities()
    {
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
        LoadGarrisonBuilderEditorAssets();
        InvalidateDiscordRichPresenceRefresh();
    }

    private void DisableGarrisonBuilderEditor(string reason)
    {
        _builderEditorEnabled = false;
        _builderStatus = reason;
        InvalidateDiscordRichPresenceRefresh();
    }

    private bool CanOpenGarrisonBuilderFromShortcut()
    {
        return _mainMenuOpen;
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
                _builderOpenMapBuffer = string.Empty;
                _builderSavePath = string.Empty;
                SyncGarrisonBuilderPathBuffers();
                _builderDirty = false;
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
        _builderEntities.AddRange(_builderDocument.Entities);
        _builderSavePath = path;
        SyncGarrisonBuilderPathBuffers();
        _builderOpenMapBuffer = path;
        _builderDirty = false;
        _builderStatus = editableDocument is not null
            ? isPackage
                ? $"opened package map with {_builderEntities.Count} entities"
                : $"opened editable map with {_builderEntities.Count} entities"
            : runtimeImport is null
                ? "opened PNG as background"
                : "opened compiled map as background";
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
        _builderDirty = true;
        _builderStatus = "background loaded";
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
        _builderDirty = true;
        _builderStatus = "walkmask loaded";
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

    private bool CanSaveGarrisonBuilderDocument()
    {
        return !string.IsNullOrWhiteSpace(_builderDocument.BackgroundImagePath)
            && (!string.IsNullOrWhiteSpace(_builderDocument.WalkmaskImagePath)
                || !string.IsNullOrWhiteSpace(_builderDocument.EmbeddedWalkmaskSection))
            && !string.IsNullOrWhiteSpace(_builderSavePath);
    }

    private void OpenGarrisonBuilderFromOptions()
    {
        _optionsMenuOpen = false;
        _pluginOptionsMenuOpen = false;
        _controlsMenuOpen = false;
        _editingPlayerName = false;
        EnableGarrisonBuilderEditor();
        _builderGameModeMenuOpen = true;
        _builderStatus = "map builder opened from settings";
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
        return new Rectangle(Math.Max(0, ViewportWidth - BuilderPanelWidth), 0, BuilderPanelWidth, ViewportHeight);
    }

    private static Rectangle[] CreateGarrisonBuilderButtonRow(int x, int y, int width, int count)
    {
        var buttons = new Rectangle[count];
        var buttonWidth = (width - (BuilderButtonGap * (count - 1))) / count;
        for (var index = 0; index < count; index += 1)
        {
            buttons[index] = new Rectangle(x + (index * (buttonWidth + BuilderButtonGap)), y, buttonWidth, BuilderButtonHeight);
        }

        return buttons;
    }

    private void DrawGarrisonBuilderButton(Rectangle bounds, string label, bool active, bool enabled, MouseState mouse)
    {
        var hovered = bounds.Contains(mouse.Position);
        var color = !enabled
            ? new Color(54, 56, 60, 190)
            : active
                ? new Color(92, 184, 212, 230)
                : hovered
                    ? new Color(58, 70, 80, 230)
                    : new Color(38, 42, 48, 230);
        var textColor = enabled ? Color.White : new Color(140, 144, 148);
        _spriteBatch.Draw(_pixel, bounds, color);
        DrawGarrisonBuilderText(label, bounds.Location.ToVector2() + new Vector2(6f, 5f), textColor, 0.95f);
    }

    private void DrawGarrisonBuilderText(string text, int x, int y, Color color, float scale)
    {
        DrawGarrisonBuilderText(text, new Vector2(x, y), color, scale);
    }

    private void DrawGarrisonBuilderText(string text, Vector2 position, Color color, float scale)
    {
        var snapped = new Vector2(MathF.Round(position.X), MathF.Round(position.Y));
        var adjustedScale = Math.Clamp(scale, 0.5f, 4f);
        DrawBitmapFontText(text, snapped, color, adjustedScale);
    }

    private Vector2 MeasureGarrisonBuilderText(string text, float scale)
    {
        var adjustedScale = Math.Clamp(scale, 0.5f, 4f);
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
        if (_builderSelectedGameMode == CustomMapBuilderGameMode.Free)
        {
            return _builderEntityDefinitions;
        }

        return _builderEntityDefinitions
            .Where(definition => definition.Modes == CustomMapBuilderGameMode.Free || (definition.Modes & _builderSelectedGameMode) != 0)
            .ToArray();
    }

    private bool TryHandleGarrisonBuilderGameModeMenuClick(Point position, bool leftClick)
    {
        if (!leftClick)
        {
            _builderGameModeMenuOpen = false;
            return true;
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
        _builderStatus = $"gamemode: {GetGarrisonBuilderModeLabel(_builderSelectedGameMode)}";
    }

    private static CustomMapBuilderGameMode[] GetGarrisonBuilderModeMenuModes()
    {
        return
        [
            CustomMapBuilderGameMode.Free,
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
            new("Free mode", CustomMapBuilderGameMode.Free),
            new("Capture the flag (ctf)", CustomMapBuilderGameMode.CaptureTheFlag),
            new("Control points (cp)", CustomMapBuilderGameMode.ControlPoint),
            new("A/D control points (adcp)", CustomMapBuilderGameMode.AttackDefenseControlPoint),
            new("King of the hill (koth)", CustomMapBuilderGameMode.KingOfTheHill),
            new("Dual king of the hill (dkoth)", CustomMapBuilderGameMode.DualKingOfTheHill),
            new("Arena (arena)", CustomMapBuilderGameMode.Arena),
            new("Generator (gen)", CustomMapBuilderGameMode.Generator),
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
            _ => "Free",
        };
    }

    private bool ShouldBlockGameplayForGarrisonBuilder()
    {
        return _builderEditorEnabled;
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
