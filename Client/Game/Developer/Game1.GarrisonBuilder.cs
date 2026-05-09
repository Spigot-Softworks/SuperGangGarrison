#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

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
    }

    private const int BuilderPanelWidth = 360;
    private const int BuilderPanelPadding = 10;
    private const int BuilderButtonHeight = 26;
    private const int BuilderButtonGap = 6;
    private const int BuilderEntityButtonSize = 30;
    private bool _builderEditorEnabled;
    private bool _builderShowBackground = true;
    private bool _builderShowWalkmask = true;
    private bool _builderShowGrid = true;
    private bool _builderDirty;
    private CustomMapBuilderDocument _builderDocument = CustomMapBuilderDocument.CreateEmpty("new_map");
    private readonly List<CustomMapBuilderEntity> _builderEntities = new();
    private string _builderStatus = "builder disabled";
    private string _builderSavePath = string.Empty;
    private Vector2 _builderCamera;
    private string _builderSelectedEntityType = "redspawn";
    private Texture2D? _builderBackgroundTexture;
    private Texture2D? _builderWalkmaskTexture;
    private string _builderLoadedBackgroundPath = string.Empty;
    private string _builderLoadedWalkmaskPath = string.Empty;
    private LoadedGameMakerSprite? _builderEntityButtonSprite;
    private bool _builderOwnsEntityButtonSprite;
    private GarrisonBuilderPathField _builderActivePathField;
    private string _builderOpenMapBuffer = string.Empty;
    private string _builderBackgroundPathBuffer = string.Empty;
    private string _builderWalkmaskPathBuffer = string.Empty;
    private string _builderSavePathBuffer = string.Empty;
    private int _builderPathCursorIndex;
    private int _builderPathSelectionStart;
    private readonly IReadOnlyList<CustomMapBuilderEntityDefinition> _builderEntityDefinitions = CustomMapBuilderEntityCatalog.Definitions;

    private void UpdateGarrisonBuilderEditor(KeyboardState keyboard, MouseState mouse, float deltaSeconds)
    {
        if (IsKeyPressed(keyboard, Keys.F4))
        {
            if (_builderEditorEnabled)
            {
                DisableGarrisonBuilderEditor("builder disabled");
            }
            else
            {
                EnableGarrisonBuilderEditor();
            }
        }

        if (!_builderEditorEnabled)
        {
            return;
        }

        LoadGarrisonBuilderEditorAssets();
        if (UpdateGarrisonBuilderPathKeyboard(keyboard))
        {
            return;
        }

        if (IsKeyPressed(keyboard, Keys.Escape))
        {
            DisableGarrisonBuilderEditor("builder disabled");
            return;
        }

        var moveSpeed = (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift)) ? 900f : 420f;
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

        if (IsKeyPressed(keyboard, Keys.Delete) && _builderEntities.Count > 0)
        {
            _builderEntities.RemoveAt(_builderEntities.Count - 1);
            UpdateGarrisonBuilderDocumentEntities();
            _builderDirty = true;
            _builderStatus = "removed latest entity";
        }

        if (mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released)
        {
            if (GetGarrisonBuilderPanelBounds().Contains(mouse.Position))
            {
                HandleGarrisonBuilderPanelClick(mouse.Position);
            }
            else
            {
                PlaceGarrisonBuilderEntity(mouse.Position.ToVector2() + _builderCamera);
            }
        }
        else if (mouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Released)
        {
            RemoveNearestGarrisonBuilderEntity(mouse.Position.ToVector2() + _builderCamera);
        }
    }

    private void DrawGarrisonBuilderEditorOverlay(MouseState mouse)
    {
        if (!_builderEditorEnabled)
        {
            return;
        }

        LoadGarrisonBuilderEditorAssets();
        var mapArea = new Rectangle(0, 0, Math.Max(1, ViewportWidth - BuilderPanelWidth), ViewportHeight);
        _spriteBatch.Draw(_pixel, mapArea, new Color(20, 24, 28, 232));
        DrawGarrisonBuilderMap(mapArea);
        DrawGarrisonBuilderPanel(mouse);
    }

    private void DrawGarrisonBuilderMap(Rectangle mapArea)
    {
        var mapOffset = -_builderCamera;
        if (_builderShowBackground && _builderBackgroundTexture is not null)
        {
            _spriteBatch.Draw(_builderBackgroundTexture, mapOffset, Color.White);
        }

        if (_builderShowWalkmask && _builderWalkmaskTexture is not null)
        {
            _spriteBatch.Draw(_builderWalkmaskTexture, mapOffset, new Color(80, 220, 230, 110));
        }

        if (_builderShowGrid)
        {
            DrawGarrisonBuilderGrid(mapArea);
        }

        foreach (var entity in _builderEntities)
        {
            var screen = new Vector2(entity.X, entity.Y) - _builderCamera;
            var bounds = new Rectangle((int)MathF.Round(screen.X) - 5, (int)MathF.Round(screen.Y) - 5, 10, 10);
            _spriteBatch.Draw(_pixel, bounds, GetGarrisonBuilderEntityColor(entity.Type));
            _spriteBatch.DrawString(_consoleFont, entity.Type, screen + new Vector2(7f, -8f), Color.White);
        }
    }

    private void DrawGarrisonBuilderGrid(Rectangle mapArea)
    {
        const int gridSize = 24;
        var startX = (int)MathF.Floor(_builderCamera.X / gridSize) * gridSize;
        var endX = (int)MathF.Ceiling((_builderCamera.X + mapArea.Width) / gridSize) * gridSize;
        var startY = (int)MathF.Floor(_builderCamera.Y / gridSize) * gridSize;
        var endY = (int)MathF.Ceiling((_builderCamera.Y + mapArea.Height) / gridSize) * gridSize;
        var color = new Color(255, 255, 255, 30);
        for (var x = startX; x <= endX; x += gridSize)
        {
            var screenX = (int)MathF.Round(x - _builderCamera.X);
            _spriteBatch.Draw(_pixel, new Rectangle(screenX, mapArea.Y, 1, mapArea.Height), color);
        }

        for (var y = startY; y <= endY; y += gridSize)
        {
            var screenY = (int)MathF.Round(y - _builderCamera.Y);
            _spriteBatch.Draw(_pixel, new Rectangle(mapArea.X, screenY, mapArea.Width, 1), color);
        }
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
        var validation = CustomMapBuilderValidator.Validate(_builderDocument with { Entities = _builderEntities.ToArray() });
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
        _spriteBatch.DrawString(_consoleFont, displayText, new Vector2(fieldBounds.X + 6f, fieldBounds.Y + 4f), Color.White, 0f, Vector2.Zero, 0.62f, SpriteEffects.None, 0f);
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
                _builderSelectedEntityType = _builderEntityDefinitions[index].Type;
                _builderStatus = $"selected {_builderEntityDefinitions[index].Label}";
                return;
            }
        }
    }

    private void PlaceGarrisonBuilderEntity(Vector2 worldPosition)
    {
        var snappedX = MathF.Round(worldPosition.X / 6f) * 6f;
        var snappedY = MathF.Round(worldPosition.Y / 6f) * 6f;
        var entity = CustomMapBuilderEntityCatalog.TryGetDefinition(_builderSelectedEntityType, out var definition)
            ? definition.CreateEntity(snappedX, snappedY)
            : CustomMapBuilderEntity.Create(_builderSelectedEntityType, snappedX, snappedY).NormalizeForEditing();
        _builderEntities.Add(entity);
        UpdateGarrisonBuilderDocumentEntities();
        _builderDirty = true;
        _builderStatus = $"placed {_builderSelectedEntityType} at {snappedX:0}, {snappedY:0}";
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
        _builderActivePathField = field;
        var text = GetGarrisonBuilderPathFieldBuffer(field);
        _builderPathCursorIndex = text.Length;
        _builderPathSelectionStart = _builderPathCursorIndex;
        _builderStatus = field switch
        {
            GarrisonBuilderPathField.OpenMap => "enter a PNG map path to open",
            GarrisonBuilderPathField.Background => "enter a background PNG path",
            GarrisonBuilderPathField.Walkmask => "enter a walkmask PNG path",
            GarrisonBuilderPathField.Save => "enter an output PNG path",
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
        }
    }

    private string GetGarrisonBuilderPathFieldBuffer(GarrisonBuilderPathField field)
    {
        return field switch
        {
            GarrisonBuilderPathField.OpenMap => _builderOpenMapBuffer,
            GarrisonBuilderPathField.Background => _builderBackgroundPathBuffer,
            GarrisonBuilderPathField.Walkmask => _builderWalkmaskPathBuffer,
            GarrisonBuilderPathField.Save => _builderSavePathBuffer,
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
        }
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
    }

    private void DisableGarrisonBuilderEditor(string reason)
    {
        _builderEditorEnabled = false;
        _builderStatus = reason;
    }

    private void LoadGarrisonBuilderEditorAssets()
    {
        _builderEntityButtonSprite ??= LoadGarrisonBuilderEntityButtonSprite();
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

        var editableDocument = CustomMapBuilderPngImporter.Import(path);
        var runtimeImport = editableDocument is null ? CustomMapPngImporter.Import(path) : null;
        _builderDocument = editableDocument?.NormalizeForEditing() ?? CustomMapBuilderDocument.CreateEmpty(Path.GetFileNameWithoutExtension(path)) with
        {
            BackgroundImagePath = path,
        };
        _builderEntities.Clear();
        _builderEntities.AddRange(_builderDocument.Entities);
        _builderSavePath = path;
        SyncGarrisonBuilderPathBuffers();
        _builderOpenMapBuffer = path;
        _builderDirty = false;
        _builderStatus = editableDocument is not null
            ? $"opened editable map with {_builderEntities.Count} entities"
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
            var validation = CustomMapBuilderValidator.Validate(_builderDocument);
            if (!validation.IsValid)
            {
                _builderStatus = validation.Issues[0].Message;
                AddConsoleLine($"builder validation failed: {_builderStatus}");
                return;
            }

            CustomMapPngExporter.Export(_builderDocument, _builderSavePath);
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
        if (_builderOwnsEntityButtonSprite && _builderEntityButtonSprite is not null)
        {
            foreach (var frame in _builderEntityButtonSprite.Frames)
            {
                frame.Dispose();
            }
        }

        _builderBackgroundTexture = null;
        _builderWalkmaskTexture = null;
        _builderEntityButtonSprite = null;
        _builderOwnsEntityButtonSprite = false;
        _builderLoadedBackgroundPath = string.Empty;
        _builderLoadedWalkmaskPath = string.Empty;
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
        _spriteBatch.DrawString(_consoleFont, label, new Vector2(bounds.X + 6f, bounds.Y + 5f), textColor);
    }

    private void DrawGarrisonBuilderText(string text, int x, int y, Color color, float scale)
    {
        _spriteBatch.DrawString(_consoleFont, text, new Vector2(x, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawGarrisonBuilderWrapped(string text, int x, int y, int maxWidth, Color color)
    {
        var line = text.Length > 52 ? string.Concat(text.AsSpan(0, 49), "...") : text;
        _spriteBatch.DrawString(_consoleFont, line, new Vector2(x, y), color, 0f, Vector2.Zero, 0.72f, SpriteEffects.None, 0f);
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

}
