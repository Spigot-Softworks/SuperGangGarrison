#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.ClientShared;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class CustomBubbleEditorController
    {
        private const int MaxUndoStrokes = 128;
        private const int MinBrushSize = 1;
        private const int MaxBrushSize = 12;
        private const int DefaultBrushSize = 4;
        private const int PaletteColumns = 8;
        private const int PaletteRows = 8;
        private const int PaletteCellSize = 14;

        private readonly Game1 _game;
        private readonly Stack<CustomBubbleStroke> _undoStack = new();
        private readonly Stack<CustomBubbleStroke> _redoStack = new();
        private readonly Dictionary<int, CustomBubblePixelChange> _activeStrokeChanges = new();
        private byte[] _pixels = new byte[CustomBubbleDocument.Rgba64ByteCount];
        private Texture2D? _previewTexture;
        private CustomBubblePaintTool _tool = CustomBubblePaintTool.Pencil;
        private Color _currentColor = Color.White;
        private int _slotIndex;
        private bool _strokeActive;
        private bool _dirty;
        private bool _gridVisible = true;
        private bool _previewDirty = true;
        private int _brushSize = DefaultBrushSize;
        private readonly Color[] _customPalette = new Color[CustomBubbleDocument.PaletteColorCount];
        private bool _customPaletteTab;
        private bool _customPaletteDirty;
        private int _selectedCustomPaletteIndex;
        private int? _lineStartPixelIndex;

        private static readonly Color[] PresetPaletteColors = CreatePresetPaletteColors();

        public CustomBubbleEditorController(Game1 game)
        {
            _game = game;
        }

        public void Open(int slotIndex)
        {
            _slotIndex = CustomBubbleDocument.NormalizeSlotIndex(slotIndex);
            _pixels = _game._customBubbleDocument.TryGetSlotPixels(_slotIndex, out var savedPixels, out _)
                ? (byte[])savedPixels.Clone()
                : new byte[CustomBubbleDocument.Rgba64ByteCount];
            _tool = CustomBubblePaintTool.Pencil;
            _currentColor = Color.White;
            _brushSize = DefaultBrushSize;
            _customPaletteTab = false;
            _selectedCustomPaletteIndex = 0;
            _customPaletteDirty = false;
            _lineStartPixelIndex = null;
            LoadCustomPalette();
            _undoStack.Clear();
            _redoStack.Clear();
            _activeStrokeChanges.Clear();
            _strokeActive = false;
            _dirty = false;
            _previewDirty = true;
        }

        public void Close()
        {
            SaveCustomPaletteIfDirty();
            _activeStrokeChanges.Clear();
            _strokeActive = false;
            _lineStartPixelIndex = null;
            _previewTexture?.Dispose();
            _previewTexture = null;
        }

        public void Update(KeyboardState keyboard, MouseState mouse)
        {
            if (_game.IsKeyPressed(keyboard, Keys.Escape))
            {
                _game.CloseCustomBubbleEditor();
                return;
            }

            if ((keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl))
                && _game.IsKeyPressed(keyboard, Keys.Z))
            {
                Undo();
                return;
            }

            if ((keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl))
                && _game.IsKeyPressed(keyboard, Keys.Y))
            {
                Redo();
                return;
            }

            GetLayout(
                out var gridBounds,
                out var pencilBounds,
                out var eraserBounds,
                out var eyedropperBounds,
                out var undoBounds,
                out var redoBounds,
                out var clearBounds,
                out var fillBounds,
                out var gridToggleBounds,
                out var presetPaletteTabBounds,
                out var customPaletteTabBounds,
                out var paletteGridBounds,
                out var brushSizeBar,
                out var saveBounds,
                out var cancelBounds,
                out var redBar,
                out var greenBar,
                out var blueBar,
                out var alphaBar);

            var clickPressed = mouse.LeftButton == ButtonState.Pressed && _game._previousMouse.LeftButton != ButtonState.Pressed;
            var clickReleased = mouse.LeftButton != ButtonState.Pressed && _game._previousMouse.LeftButton == ButtonState.Pressed;
            var leftDown = mouse.LeftButton == ButtonState.Pressed;
            var shiftDown = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);

            if (clickPressed)
            {
                if (pencilBounds.Contains(mouse.Position))
                {
                    SetTool(CustomBubblePaintTool.Pencil);
                    return;
                }

                if (eraserBounds.Contains(mouse.Position))
                {
                    SetTool(CustomBubblePaintTool.Eraser);
                    return;
                }

                if (eyedropperBounds.Contains(mouse.Position))
                {
                    SetTool(CustomBubblePaintTool.Eyedropper);
                    return;
                }

                if (undoBounds.Contains(mouse.Position))
                {
                    Undo();
                    return;
                }

                if (redoBounds.Contains(mouse.Position))
                {
                    Redo();
                    return;
                }

                if (clearBounds.Contains(mouse.Position))
                {
                    Clear();
                    return;
                }

                if (fillBounds.Contains(mouse.Position))
                {
                    Fill();
                    return;
                }

                if (gridToggleBounds.Contains(mouse.Position))
                {
                    _gridVisible = !_gridVisible;
                    return;
                }

                if (presetPaletteTabBounds.Contains(mouse.Position))
                {
                    _customPaletteTab = false;
                    return;
                }

                if (customPaletteTabBounds.Contains(mouse.Position))
                {
                    _customPaletteTab = true;
                    _currentColor = _customPalette[_selectedCustomPaletteIndex];
                    return;
                }

                if (TryGetPaletteIndex(paletteGridBounds, mouse.Position, out var paletteIndex))
                {
                    if (_customPaletteTab)
                    {
                        _selectedCustomPaletteIndex = paletteIndex;
                        _currentColor = _customPalette[paletteIndex];
                    }
                    else
                    {
                        _currentColor = PresetPaletteColors[paletteIndex];
                    }

                    SetTool(CustomBubblePaintTool.Pencil);
                    return;
                }

                if (saveBounds.Contains(mouse.Position))
                {
                    _game.SaveCustomBubbleEditorPixels(_slotIndex, (byte[])_pixels.Clone());
                    _game.CloseCustomBubbleEditor();
                    return;
                }

                if (cancelBounds.Contains(mouse.Position))
                {
                    _game.CloseCustomBubbleEditor();
                    return;
                }
            }

            if (leftDown)
            {
                var changedColor = _customPaletteTab
                    && (TrySetColorChannelFromBar(redBar, mouse.Position, mouse.X, 0)
                        || TrySetColorChannelFromBar(greenBar, mouse.Position, mouse.X, 1)
                        || TrySetColorChannelFromBar(blueBar, mouse.Position, mouse.X, 2)
                        || TrySetColorChannelFromBar(alphaBar, mouse.Position, mouse.X, 3));
                if (changedColor || TrySetBrushSizeFromBar(brushSizeBar, mouse.Position, mouse.X))
                {
                    return;
                }
            }

            if (clickPressed && TryGetGridPixel(gridBounds, mouse.Position, out var pixelIndex))
            {
                if (_tool == CustomBubblePaintTool.Eyedropper)
                {
                    _currentColor = ReadRgba64Color(_pixels, pixelIndex);
                    SetTool(CustomBubblePaintTool.Pencil);
                    return;
                }

                if (shiftDown)
                {
                    HandleLineClick(pixelIndex);
                    return;
                }

                _lineStartPixelIndex = null;
                BeginStroke();
                PaintBrush(pixelIndex);
                return;
            }

            if (_strokeActive && leftDown && TryGetGridPixel(gridBounds, mouse.Position, out pixelIndex))
            {
                PaintBrush(pixelIndex);
            }

            if (clickReleased && _strokeActive)
            {
                CommitStroke();
            }
        }

        public void Draw()
        {
            GetLayout(
                out var gridBounds,
                out var pencilBounds,
                out var eraserBounds,
                out var eyedropperBounds,
                out var undoBounds,
                out var redoBounds,
                out var clearBounds,
                out var fillBounds,
                out var gridToggleBounds,
                out var presetPaletteTabBounds,
                out var customPaletteTabBounds,
                out var paletteGridBounds,
                out var brushSizeBar,
                out var saveBounds,
                out var cancelBounds,
                out var redBar,
                out var greenBar,
                out var blueBar,
                out var alphaBar);

            var panel = GetPanelBounds();
            var mousePosition = _game._lastKnownMousePosition;
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(0, 0, _game.ViewportWidth, _game.ViewportHeight), Color.Black * 0.78f);
            _game.DrawRoundedRectangleOutline(panel, new Color(52, 47, 42), new Color(213, 205, 188), outlineThickness: 2, radius: 8);
            _game.DrawBitmapFontText($"Edit {GetCustomBubbleSlotLabel(_slotIndex)}", new Vector2(panel.X + 22f, panel.Y + 16f), Color.White, 1f);

            DrawGrid(gridBounds, mousePosition);
            DrawPreview();
            DrawToolButton(pencilBounds, "Pencil", _tool == CustomBubblePaintTool.Pencil, pencilBounds.Contains(mousePosition));
            DrawToolButton(eraserBounds, "Eraser", _tool == CustomBubblePaintTool.Eraser, eraserBounds.Contains(mousePosition));
            DrawToolButton(eyedropperBounds, "Pick", _tool == CustomBubblePaintTool.Eyedropper, eyedropperBounds.Contains(mousePosition));
            DrawToolButton(undoBounds, "Undo", false, undoBounds.Contains(mousePosition), _undoStack.Count > 0);
            DrawToolButton(redoBounds, "Redo", false, redoBounds.Contains(mousePosition), _redoStack.Count > 0);
            DrawToolButton(clearBounds, "Clear", false, clearBounds.Contains(mousePosition));
            DrawToolButton(fillBounds, "Fill", false, fillBounds.Contains(mousePosition));
            DrawToolButton(gridToggleBounds, _gridVisible ? "Grid On" : "Grid Off", _gridVisible, gridToggleBounds.Contains(mousePosition));
            DrawPaletteTabs(presetPaletteTabBounds, customPaletteTabBounds, mousePosition);
            DrawPaletteGrid(paletteGridBounds, mousePosition);
            DrawBrushSizeBar(brushSizeBar);
            DrawToolButton(saveBounds, _dirty ? "Save" : "Save", false, saveBounds.Contains(mousePosition));
            DrawToolButton(cancelBounds, "Cancel", false, cancelBounds.Contains(mousePosition));
            DrawCurrentColorSwatch(panel);
            if (_customPaletteTab)
            {
                DrawColorBar(redBar, "R", _currentColor.R, new Color(220, 82, 82));
                DrawColorBar(greenBar, "G", _currentColor.G, new Color(95, 188, 96));
                DrawColorBar(blueBar, "B", _currentColor.B, new Color(84, 128, 230));
                DrawColorBar(alphaBar, "A", _currentColor.A, new Color(220, 220, 220));
            }
        }

        private void BeginStroke()
        {
            _activeStrokeChanges.Clear();
            _strokeActive = true;
        }

        private void SetTool(CustomBubblePaintTool tool)
        {
            _tool = tool;
            _lineStartPixelIndex = null;
        }

        private void HandleLineClick(int pixelIndex)
        {
            if (!_game.IsCustomBubbleCanvasPixelInsideShell(pixelIndex))
            {
                return;
            }

            if (_lineStartPixelIndex is not int startPixelIndex)
            {
                _lineStartPixelIndex = pixelIndex;
                return;
            }

            BeginStroke();
            foreach (var linePixelIndex in EnumerateLinePixels(startPixelIndex, pixelIndex))
            {
                PaintBrush(linePixelIndex);
            }

            CommitStroke();
            _lineStartPixelIndex = null;
        }

        private void PaintBrush(int centerPixelIndex)
        {
            var centerX = centerPixelIndex % CustomBubbleDocument.BubbleWidth;
            var centerY = centerPixelIndex / CustomBubbleDocument.BubbleWidth;
            var before = (_brushSize - 1) / 2;
            var after = _brushSize / 2;
            for (var y = centerY - before; y <= centerY + after; y += 1)
            {
                if (y < 0 || y >= CustomBubbleDocument.BubbleHeight)
                {
                    continue;
                }

                for (var x = centerX - before; x <= centerX + after; x += 1)
                {
                    if (x < 0 || x >= CustomBubbleDocument.BubbleWidth)
                    {
                        continue;
                    }

                    PaintPixel((y * CustomBubbleDocument.BubbleWidth) + x);
                }
            }
        }

        private void PaintPixel(int pixelIndex)
        {
            var newValue = _tool == CustomBubblePaintTool.Eraser
                ? 0UL
                : PackRgba64Color(_currentColor);
            if (newValue != 0UL && !_game.IsCustomBubbleCanvasPixelInsideShell(pixelIndex))
            {
                return;
            }

            var oldValue = ReadRgba64Pixel(_pixels, pixelIndex);
            if (oldValue == newValue)
            {
                return;
            }

            if (!_activeStrokeChanges.TryGetValue(pixelIndex, out var change))
            {
                change = new CustomBubblePixelChange(pixelIndex, oldValue, newValue);
            }
            else
            {
                change = change with { NewValue = newValue };
            }

            _activeStrokeChanges[pixelIndex] = change;
            WriteRgba64Pixel(_pixels, pixelIndex, newValue);
            _dirty = true;
            _previewDirty = true;
        }

        private void CommitStroke()
        {
            _strokeActive = false;
            if (_activeStrokeChanges.Count == 0)
            {
                return;
            }

            _undoStack.Push(new CustomBubbleStroke([.. _activeStrokeChanges.Values]));
            while (_undoStack.Count > MaxUndoStrokes)
            {
                TrimOldestUndoStroke();
            }

            _redoStack.Clear();
            _activeStrokeChanges.Clear();
        }

        private void Undo()
        {
            if (_undoStack.Count == 0)
            {
                return;
            }

            var stroke = _undoStack.Pop();
            ApplyStroke(stroke, useNewValue: false);
            _redoStack.Push(stroke);
        }

        private void Redo()
        {
            if (_redoStack.Count == 0)
            {
                return;
            }

            var stroke = _redoStack.Pop();
            ApplyStroke(stroke, useNewValue: true);
            _undoStack.Push(stroke);
        }

        private void Clear()
        {
            _lineStartPixelIndex = null;
            BeginStroke();
            for (var pixelIndex = 0; pixelIndex < CustomBubbleDocument.BubbleWidth * CustomBubbleDocument.BubbleHeight; pixelIndex += 1)
            {
                if (ReadRgba64Pixel(_pixels, pixelIndex) != 0UL)
                {
                    PaintPixelAsValue(pixelIndex, 0UL);
                }
            }

            CommitStroke();
        }

        private void Fill()
        {
            _lineStartPixelIndex = null;
            BeginStroke();
            var newValue = PackRgba64Color(_currentColor);
            for (var pixelIndex = 0; pixelIndex < CustomBubbleDocument.BubbleWidth * CustomBubbleDocument.BubbleHeight; pixelIndex += 1)
            {
                if (_game.IsCustomBubbleCanvasPixelInsideShell(pixelIndex))
                {
                    PaintPixelAsValue(pixelIndex, newValue);
                }
            }

            CommitStroke();
        }

        private void PaintPixelAsValue(int pixelIndex, ulong newValue)
        {
            var oldValue = ReadRgba64Pixel(_pixels, pixelIndex);
            if (oldValue == newValue)
            {
                return;
            }

            _activeStrokeChanges[pixelIndex] = new CustomBubblePixelChange(pixelIndex, oldValue, newValue);
            WriteRgba64Pixel(_pixels, pixelIndex, newValue);
            _dirty = true;
            _previewDirty = true;
        }

        private void ApplyStroke(CustomBubbleStroke stroke, bool useNewValue)
        {
            foreach (var change in stroke.Changes)
            {
                WriteRgba64Pixel(_pixels, change.PixelIndex, useNewValue ? change.NewValue : change.OldValue);
            }

            _dirty = true;
            _previewDirty = true;
        }

        private void TrimOldestUndoStroke()
        {
            if (_undoStack.Count <= MaxUndoStrokes)
            {
                return;
            }

            var retained = _undoStack.ToArray();
            _undoStack.Clear();
            for (var index = retained.Length - 2; index >= 0; index -= 1)
            {
                _undoStack.Push(retained[index]);
            }
        }

        private static IEnumerable<int> EnumerateLinePixels(int startPixelIndex, int endPixelIndex)
        {
            var x0 = startPixelIndex % CustomBubbleDocument.BubbleWidth;
            var y0 = startPixelIndex / CustomBubbleDocument.BubbleWidth;
            var x1 = endPixelIndex % CustomBubbleDocument.BubbleWidth;
            var y1 = endPixelIndex / CustomBubbleDocument.BubbleWidth;
            var dx = Math.Abs(x1 - x0);
            var dy = -Math.Abs(y1 - y0);
            var stepX = x0 < x1 ? 1 : -1;
            var stepY = y0 < y1 ? 1 : -1;
            var error = dx + dy;

            while (true)
            {
                yield return (y0 * CustomBubbleDocument.BubbleWidth) + x0;
                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                var doubleError = 2 * error;
                if (doubleError >= dy)
                {
                    error += dy;
                    x0 += stepX;
                }

                if (doubleError <= dx)
                {
                    error += dx;
                    y0 += stepY;
                }
            }
        }

        private static bool IsMouseOnBar(Rectangle bar, Point mousePosition)
        {
            return bar.Contains(mousePosition);
        }

        private bool TrySetColorChannelFromBar(Rectangle bar, Point mousePosition, int mouseX, int channel)
        {
            if (!IsMouseOnBar(bar, mousePosition))
            {
                return false;
            }

            var value = (byte)Math.Clamp((int)MathF.Round(((mouseX - bar.X) / (float)Math.Max(1, bar.Width - 1)) * 255f), 0, 255);
            _currentColor = channel switch
            {
                0 => new Color(value, _currentColor.G, _currentColor.B, _currentColor.A),
                1 => new Color(_currentColor.R, value, _currentColor.B, _currentColor.A),
                2 => new Color(_currentColor.R, _currentColor.G, value, _currentColor.A),
                _ => new Color(_currentColor.R, _currentColor.G, _currentColor.B, value),
            };
            ApplyCurrentColorToSelectedCustomPalette();
            return true;
        }

        private bool TrySetBrushSizeFromBar(Rectangle bar, Point mousePosition, int mouseX)
        {
            if (!IsMouseOnBar(bar, mousePosition))
            {
                return false;
            }

            var fraction = Math.Clamp((mouseX - bar.X) / (float)Math.Max(1, bar.Width - 1), 0f, 1f);
            _brushSize = Math.Clamp(
                (int)MathF.Round(MinBrushSize + (fraction * (MaxBrushSize - MinBrushSize))),
                MinBrushSize,
                MaxBrushSize);
            return true;
        }

        private void LoadCustomPalette()
        {
            Array.Copy(PresetPaletteColors, _customPalette, Math.Min(PresetPaletteColors.Length, _customPalette.Length));
            for (var index = 0; index < CustomBubbleDocument.PaletteColorCount; index += 1)
            {
                if (_game._customBubbleDocument.TryGetCustomPaletteColorHex(index, out var colorHex)
                    && TryParseRgbaHex(colorHex, out var color))
                {
                    _customPalette[index] = color;
                }
            }
        }

        private void ApplyCurrentColorToSelectedCustomPalette()
        {
            if (!_customPaletteTab)
            {
                return;
            }

            _customPalette[_selectedCustomPaletteIndex] = _currentColor;
            _game._customBubbleDocument.SetCustomPaletteColorHex(_selectedCustomPaletteIndex, FormatRgbaHex(_currentColor));
            _customPaletteDirty = true;
        }

        private void SaveCustomPaletteIfDirty()
        {
            if (!_customPaletteDirty)
            {
                return;
            }

            _game._customBubbleDocument.Save();
            _customPaletteDirty = false;
        }

        private static bool TryGetPaletteIndex(Rectangle paletteBounds, Point mousePosition, out int paletteIndex)
        {
            paletteIndex = -1;
            if (!paletteBounds.Contains(mousePosition))
            {
                return false;
            }

            var cellSize = Math.Max(1, Math.Min(paletteBounds.Width / PaletteColumns, paletteBounds.Height / PaletteRows));
            var x = (mousePosition.X - paletteBounds.X) / cellSize;
            var y = (mousePosition.Y - paletteBounds.Y) / cellSize;
            if (x < 0 || y < 0 || x >= PaletteColumns || y >= PaletteRows)
            {
                return false;
            }

            paletteIndex = (y * PaletteColumns) + x;
            return paletteIndex >= 0 && paletteIndex < CustomBubbleDocument.PaletteColorCount;
        }

        private bool TryGetGridPixel(Rectangle gridBounds, Point mousePosition, out int pixelIndex)
        {
            pixelIndex = -1;
            if (!gridBounds.Contains(mousePosition))
            {
                return false;
            }

            var cellSize = Math.Max(1, gridBounds.Width / CustomBubbleDocument.BubbleWidth);
            var x = (mousePosition.X - gridBounds.X) / cellSize;
            var y = (mousePosition.Y - gridBounds.Y) / cellSize;
            if (x < 0 || y < 0 || x >= CustomBubbleDocument.BubbleWidth || y >= CustomBubbleDocument.BubbleHeight)
            {
                return false;
            }

            pixelIndex = (y * CustomBubbleDocument.BubbleWidth) + x;
            return true;
        }

        private void DrawGrid(Rectangle gridBounds, Point mousePosition)
        {
            var cellSize = Math.Max(1, gridBounds.Width / CustomBubbleDocument.BubbleWidth);
            var shellFrame = _game.GetCustomBubbleShellFrame();
            if (shellFrame is not null)
            {
                _game.DrawLoadedSpriteFrame(shellFrame, gridBounds, Color.White);
            }
            else
            {
                _game._spriteBatch.Draw(_game._pixel, gridBounds, new Color(44, 40, 36));
                DrawOutline(gridBounds, new Color(255, 236, 160) * 0.8f, 2);
                return;
            }

            for (var y = 0; y < CustomBubbleDocument.BubbleHeight; y += 1)
            {
                for (var x = 0; x < CustomBubbleDocument.BubbleWidth; x += 1)
                {
                    var pixelIndex = (y * CustomBubbleDocument.BubbleWidth) + x;
                    if (!_game.IsCustomBubbleCanvasPixelInsideShell(pixelIndex))
                    {
                        continue;
                    }

                    var cell = new Rectangle(gridBounds.X + (x * cellSize), gridBounds.Y + (y * cellSize), cellSize, cellSize);
                    var color = ReadRgba64ColorPremultiplied(_pixels, pixelIndex);
                    if (color.A > 0)
                    {
                        _game._spriteBatch.Draw(_game._pixel, cell, color);
                    }
                }
            }

            if (_gridVisible)
            {
                var gridColor = Color.Black * 0.38f;
                for (var line = 0; line <= CustomBubbleDocument.BubbleWidth; line += 1)
                {
                    var thickness = line % 8 == 0 ? 2 : 1;
                    var x = gridBounds.X + (line * cellSize);
                    _game._spriteBatch.Draw(_game._pixel, new Rectangle(x, gridBounds.Y, thickness, gridBounds.Height), gridColor);
                }

                for (var line = 0; line <= CustomBubbleDocument.BubbleHeight; line += 1)
                {
                    var thickness = line % 8 == 0 ? 2 : 1;
                    var y = gridBounds.Y + (line * cellSize);
                    _game._spriteBatch.Draw(_game._pixel, new Rectangle(gridBounds.X, y, gridBounds.Width, thickness), gridColor);
                }
            }

            DrawLinePreview(gridBounds, mousePosition, cellSize);

            if (TryGetGridPixel(gridBounds, mousePosition, out var hoverIndex)
                && _game.IsCustomBubbleCanvasPixelInsideShell(hoverIndex))
            {
                var hoverX = hoverIndex % CustomBubbleDocument.BubbleWidth;
                var hoverY = hoverIndex / CustomBubbleDocument.BubbleWidth;
                var before = (_brushSize - 1) / 2;
                var after = _brushSize / 2;
                var left = Math.Clamp(hoverX - before, 0, CustomBubbleDocument.BubbleWidth - 1);
                var top = Math.Clamp(hoverY - before, 0, CustomBubbleDocument.BubbleHeight - 1);
                var right = Math.Clamp(hoverX + after + 1, 0, CustomBubbleDocument.BubbleWidth);
                var bottom = Math.Clamp(hoverY + after + 1, 0, CustomBubbleDocument.BubbleHeight);
                var hover = new Rectangle(
                    gridBounds.X + (left * cellSize),
                    gridBounds.Y + (top * cellSize),
                    Math.Max(cellSize, (right - left) * cellSize),
                    Math.Max(cellSize, (bottom - top) * cellSize));
                DrawOutline(hover, new Color(255, 236, 160), 2);
            }

            DrawOutline(gridBounds, new Color(255, 236, 160) * 0.8f, 2);
        }

        private void DrawLinePreview(Rectangle gridBounds, Point mousePosition, int cellSize)
        {
            if (_lineStartPixelIndex is not int startPixelIndex)
            {
                return;
            }

            var endpoint = startPixelIndex;
            if (TryGetGridPixel(gridBounds, mousePosition, out var hoverIndex)
                && _game.IsCustomBubbleCanvasPixelInsideShell(hoverIndex))
            {
                endpoint = hoverIndex;
            }

            foreach (var pixelIndex in EnumerateLinePixels(startPixelIndex, endpoint))
            {
                if (!_game.IsCustomBubbleCanvasPixelInsideShell(pixelIndex))
                {
                    continue;
                }

                var x = pixelIndex % CustomBubbleDocument.BubbleWidth;
                var y = pixelIndex / CustomBubbleDocument.BubbleWidth;
                var cell = new Rectangle(
                    gridBounds.X + (x * cellSize),
                    gridBounds.Y + (y * cellSize),
                    cellSize,
                    cellSize);
                _game._spriteBatch.Draw(_game._pixel, cell, new Color(255, 236, 160) * 0.28f);
                DrawOutline(cell, new Color(255, 236, 160), 1);
            }
        }

        private void DrawPreview()
        {
            var shellFrame = _game.GetCustomBubbleShellFrame();
            var previewBounds = GetPreviewBounds();
            _game._spriteBatch.Draw(_game._pixel, previewBounds, new Color(36, 33, 30));
            DrawOutline(previewBounds, new Color(213, 205, 188), 1);
            if (shellFrame is null)
            {
                return;
            }

            if (_previewDirty || _previewTexture is null)
            {
                _previewTexture?.Dispose();
                _previewTexture = _game.CreateCustomBubbleShellTexture(_pixels);
                _previewDirty = false;
            }

            var previewScale = Math.Max(
                1,
                Math.Min(
                    (previewBounds.Width - 16) / CustomBubbleShellPixelWidth,
                    (previewBounds.Height - 6) / CustomBubbleShellPixelHeight));
            var shellWidth = CustomBubbleShellPixelWidth * previewScale;
            var shellHeight = CustomBubbleShellPixelHeight * previewScale;
            var shellBounds = new Rectangle(
                previewBounds.X + ((previewBounds.Width - shellWidth) / 2),
                previewBounds.Y + ((previewBounds.Height - shellHeight) / 2),
                shellWidth,
                shellHeight);
            _game.DrawLoadedSpriteFrame(shellFrame, shellBounds, Color.White);

            _game._spriteBatch.Draw(
                _previewTexture,
                shellBounds,
                Color.White);
        }

        private void DrawToolButton(Rectangle bounds, string label, bool selected, bool hovered, bool enabled = true)
        {
            var fill = selected
                ? new Color(91, 87, 70)
                : enabled
                    ? hovered ? new Color(75, 67, 62) : new Color(54, 47, 41)
                    : new Color(38, 36, 34);
            var outline = selected ? new Color(255, 236, 160) : new Color(213, 205, 188);
            _game.DrawRoundedRectangleOutline(bounds, fill, outline * (enabled ? 1f : 0.45f), outlineThickness: 2, radius: 8);
            var color = enabled ? Color.White : new Color(140, 140, 140);
            var textScale = 1f;
            var trimmed = _game.TrimBitmapMenuText(label, bounds.Width - 16f, textScale);
            var textX = bounds.X + ((bounds.Width - _game.MeasureBitmapFontWidth(trimmed, textScale)) * 0.5f);
            var textY = bounds.Y + ((bounds.Height - _game.MeasureBitmapFontHeight(textScale)) * 0.5f) - 1f;
            _game.DrawBitmapFontText(trimmed, new Vector2(textX, textY), color, textScale);
        }

        private void DrawPaletteTabs(Rectangle presetTabBounds, Rectangle customTabBounds, Point mousePosition)
        {
            DrawToolButton(presetTabBounds, "Preset", !_customPaletteTab, presetTabBounds.Contains(mousePosition));
            DrawToolButton(customTabBounds, "Custom", _customPaletteTab, customTabBounds.Contains(mousePosition));
        }

        private void DrawPaletteGrid(Rectangle paletteBounds, Point mousePosition)
        {
            _game._spriteBatch.Draw(_game._pixel, paletteBounds, new Color(28, 26, 24));
            var colors = _customPaletteTab ? _customPalette : PresetPaletteColors;
            var cellSize = Math.Max(1, Math.Min(paletteBounds.Width / PaletteColumns, paletteBounds.Height / PaletteRows));
            TryGetPaletteIndex(paletteBounds, mousePosition, out var hoveredIndex);

            for (var y = 0; y < PaletteRows; y += 1)
            {
                for (var x = 0; x < PaletteColumns; x += 1)
                {
                    var index = (y * PaletteColumns) + x;
                    var cell = new Rectangle(
                        paletteBounds.X + (x * cellSize),
                        paletteBounds.Y + (y * cellSize),
                        cellSize,
                        cellSize);
                    DrawCheckerCell(cell, x, y, 0.55f);
                    _game._spriteBatch.Draw(_game._pixel, cell, ToPremultipliedColor(colors[index]));
                    DrawOutline(cell, new Color(18, 18, 18) * 0.65f, 1);

                    var selected = _customPaletteTab
                        ? index == _selectedCustomPaletteIndex
                        : colors[index].Equals(_currentColor);
                    if (selected)
                    {
                        DrawOutline(cell, new Color(255, 236, 160), 2);
                    }
                    else if (index == hoveredIndex)
                    {
                        DrawOutline(cell, Color.White * 0.75f, 1);
                    }
                }
            }

            DrawOutline(paletteBounds, new Color(213, 205, 188), 1);
        }

        private void DrawCurrentColorSwatch(Rectangle panel)
        {
            var swatch = new Rectangle(panel.Right - 62, panel.Y + 16, 34, 34);
            DrawCheckerCell(swatch, 0, 0);
            _game._spriteBatch.Draw(_game._pixel, swatch, ToPremultipliedColor(_currentColor));
            DrawOutline(swatch, new Color(213, 205, 188), 2);
        }

        private void DrawColorBar(Rectangle bounds, string label, byte value, Color color)
        {
            DrawSliderBar(bounds, $"{label} {value}", value / 255f, color);
        }

        private void DrawBrushSizeBar(Rectangle bounds)
        {
            DrawSliderBar(
                bounds,
                $"Brush {_brushSize}px",
                (_brushSize - MinBrushSize) / (float)(MaxBrushSize - MinBrushSize),
                new Color(255, 236, 160));
        }

        private void DrawSliderBar(Rectangle bounds, string label, float fraction, Color color)
        {
            var labelScale = 1f;
            _game.DrawBitmapFontText(label, new Vector2(bounds.X, bounds.Y - 18), Color.White, labelScale);
            _game._spriteBatch.Draw(_game._pixel, bounds, new Color(28, 28, 28));
            var fillWidth = (int)MathF.Round(bounds.Width * Math.Clamp(fraction, 0f, 1f));
            if (fillWidth > 0)
            {
                _game._spriteBatch.Draw(_game._pixel, new Rectangle(bounds.X, bounds.Y, fillWidth, bounds.Height), color);
            }

            var markerX = bounds.X + Math.Clamp(fillWidth, 0, bounds.Width);
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(markerX - 1, bounds.Y - 2, 2, bounds.Height + 4), Color.White);
            DrawOutline(bounds, new Color(213, 205, 188), 1);
        }

        private void DrawCheckerCell(Rectangle bounds, int x, int y, float alpha = 1f)
        {
            var color = ((x + y) & 1) == 0 ? new Color(190, 190, 190) : new Color(130, 130, 130);
            _game._spriteBatch.Draw(_game._pixel, bounds, color * alpha);
        }

        private static Color ToPremultipliedColor(Color color)
        {
            return color.A == 0
                ? Color.Transparent
                : new Color(
                    (byte)((color.R * color.A + 127) / 255),
                    (byte)((color.G * color.A + 127) / 255),
                    (byte)((color.B * color.A + 127) / 255),
                    color.A);
        }

        private static string FormatRgbaHex(Color color)
        {
            return FormattableString.Invariant($"{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}");
        }

        private static bool TryParseRgbaHex(string colorHex, out Color color)
        {
            color = Color.White;
            var normalized = colorHex.Trim().TrimStart('#');
            if (normalized.Length != 8
                || !uint.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            {
                return false;
            }

            color = new Color(
                (byte)((value >> 24) & 0xff),
                (byte)((value >> 16) & 0xff),
                (byte)((value >> 8) & 0xff),
                (byte)(value & 0xff));
            return true;
        }

        private static Color[] CreatePresetPaletteColors()
        {
            return
            [
                new Color(0, 0, 0), new Color(32, 32, 32), new Color(64, 64, 64), new Color(96, 96, 96),
                new Color(128, 128, 128), new Color(160, 160, 160), new Color(200, 200, 200), new Color(255, 255, 255),
                new Color(86, 0, 0), new Color(132, 0, 0), new Color(180, 0, 0), new Color(220, 32, 32),
                new Color(255, 64, 64), new Color(255, 112, 112), new Color(255, 168, 168), new Color(255, 220, 220),
                new Color(92, 42, 0), new Color(130, 64, 0), new Color(176, 92, 16), new Color(224, 128, 32),
                new Color(255, 168, 56), new Color(255, 200, 104), new Color(126, 82, 46), new Color(196, 140, 84),
                new Color(92, 84, 0), new Color(140, 126, 0), new Color(190, 170, 16), new Color(235, 210, 32),
                new Color(255, 236, 64), new Color(255, 246, 128), new Color(180, 160, 72), new Color(110, 98, 38),
                new Color(0, 64, 24), new Color(0, 104, 42), new Color(0, 150, 64), new Color(24, 190, 86),
                new Color(64, 225, 120), new Color(128, 245, 172), new Color(30, 110, 84), new Color(88, 160, 124),
                new Color(0, 70, 84), new Color(0, 112, 132), new Color(0, 164, 190), new Color(24, 204, 224),
                new Color(84, 232, 244), new Color(164, 248, 255), new Color(40, 118, 150), new Color(100, 174, 202),
                new Color(0, 28, 92), new Color(0, 52, 142), new Color(24, 84, 200), new Color(64, 120, 242),
                new Color(112, 164, 255), new Color(176, 210, 255), new Color(54, 74, 120), new Color(102, 124, 180),
                new Color(50, 0, 80), new Color(82, 0, 128), new Color(126, 32, 178), new Color(168, 70, 220),
                new Color(210, 112, 245), new Color(238, 174, 255), new Color(140, 54, 112), new Color(220, 92, 160),
            ];
        }

        private void DrawOutline(Rectangle bounds, Color color, int thickness)
        {
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
        }

        private static bool UseStackedEditorLayout(Rectangle panel)
        {
            return panel.Width < 760;
        }

        private static void GetStackedControlColumns(
            Rectangle sideBounds,
            out Rectangle leftColumn,
            out Rectangle rightColumn,
            out bool singleColumn)
        {
            const int columnGap = 12;
            singleColumn = sideBounds.Width < 340;
            if (singleColumn)
            {
                leftColumn = sideBounds;
                rightColumn = sideBounds;
                return;
            }

            var leftWidth = Math.Max(1, (sideBounds.Width - columnGap) / 2);
            var rightWidth = Math.Max(1, sideBounds.Width - columnGap - leftWidth);
            leftColumn = new Rectangle(sideBounds.X, sideBounds.Y, leftWidth, sideBounds.Height);
            rightColumn = new Rectangle(leftColumn.Right + columnGap, sideBounds.Y, rightWidth, sideBounds.Height);
        }

        private static Rectangle GetCanvasToolButtonArea(Rectangle panel, Rectangle gridBounds)
        {
            if (!UseStackedEditorLayout(panel))
            {
                return new Rectangle(gridBounds.X, gridBounds.Bottom + 14, gridBounds.Width, 1);
            }

            var margin = panel.Width < 420 ? 10 : 16;
            return new Rectangle(
                panel.X + margin,
                gridBounds.Bottom + 14,
                Math.Max(1, panel.Width - (margin * 2)),
                1);
        }

        private static int GetCanvasToolColumnCount(Rectangle buttonAreaBounds)
        {
            return buttonAreaBounds.Width >= 420 ? 4 : 3;
        }

        private static int GetCanvasToolStackHeight(Rectangle buttonAreaBounds, int buttonCount)
        {
            const int buttonHeight = 28;
            const int buttonGap = 8;
            var columns = GetCanvasToolColumnCount(buttonAreaBounds);
            var rows = (buttonCount + columns - 1) / columns;
            return (rows * buttonHeight) + ((rows - 1) * buttonGap);
        }

        private static Rectangle GetCanvasToolButtonBounds(Rectangle buttonAreaBounds, int buttonIndex, int buttonCount)
        {
            const int buttonHeight = 28;
            const int buttonGap = 8;
            var columns = GetCanvasToolColumnCount(buttonAreaBounds);
            var row = buttonIndex / columns;
            var rowStart = row * columns;
            var rowCount = Math.Min(columns, buttonCount - rowStart);
            var buttonWidth = Math.Max(1, (buttonAreaBounds.Width - (buttonGap * (columns - 1))) / columns);
            var rowWidth = (buttonWidth * rowCount) + (buttonGap * (rowCount - 1));
            var rowX = buttonAreaBounds.X + ((buttonAreaBounds.Width - rowWidth) / 2);
            return new Rectangle(
                rowX + ((buttonIndex - rowStart) * (buttonWidth + buttonGap)),
                buttonAreaBounds.Y + (row * (buttonHeight + buttonGap)),
                buttonWidth,
                buttonHeight);
        }

        private Rectangle GetPanelBounds()
        {
            var outerMargin = _game.ViewportWidth < 520 || _game.ViewportHeight < 420 ? 8 : 16;
            var width = Math.Max(1, Math.Min(_game.ViewportWidth - (outerMargin * 2), 900));
            var height = Math.Max(1, Math.Min(_game.ViewportHeight - (outerMargin * 2), 600));
            return new Rectangle((_game.ViewportWidth - width) / 2, (_game.ViewportHeight - height) / 2, width, height);
        }

        private Rectangle GetPreviewBounds()
        {
            var panel = GetPanelBounds();
            GetShellAndSideBounds(out _, out _, out var sideBounds);
            if (UseStackedEditorLayout(panel))
            {
                GetStackedControlColumns(sideBounds, out var leftColumn, out _, out var singleColumn);
                return singleColumn
                    ? new Rectangle(sideBounds.X, sideBounds.Y, sideBounds.Width, 64)
                    : new Rectangle(leftColumn.X, leftColumn.Y, leftColumn.Width, 74);
            }

            return new Rectangle(sideBounds.X, sideBounds.Y, sideBounds.Width, 74);
        }

        private void GetShellAndSideBounds(out Rectangle shellBounds, out Rectangle gridBounds, out Rectangle sideBounds)
        {
            var panel = GetPanelBounds();
            const int wideMargin = 24;
            const int gap = 16;
            if (UseStackedEditorLayout(panel))
            {
                var margin = panel.Width < 420 ? 10 : 16;
                var titleHeight = 54;
                var reservedControlsHeight = _customPaletteTab ? 270 : 220;
                var reservedToolRowsHeight = 104;
                var compactAvailableCanvasWidth = Math.Max(1, panel.Width - (margin * 2));
                var compactAvailableCanvasHeight = Math.Max(1, panel.Height - titleHeight - reservedToolRowsHeight - reservedControlsHeight);
                var compactCellSize = Math.Clamp(
                    Math.Min(
                        compactAvailableCanvasWidth / CustomBubbleDocument.BubbleWidth,
                        compactAvailableCanvasHeight / CustomBubbleDocument.BubbleHeight),
                    2,
                    14);
                gridBounds = new Rectangle(
                    panel.X + ((panel.Width - (CustomBubbleDocument.BubbleWidth * compactCellSize)) / 2),
                    panel.Y + titleHeight,
                    CustomBubbleDocument.BubbleWidth * compactCellSize,
                    CustomBubbleDocument.BubbleHeight * compactCellSize);
                shellBounds = gridBounds;

                var compactButtonArea = GetCanvasToolButtonArea(panel, gridBounds);
                var toolStackHeight = GetCanvasToolStackHeight(compactButtonArea, 7);
                var sideY = compactButtonArea.Y + toolStackHeight + 12;
                var sideBottom = panel.Bottom - 50;
                sideBounds = new Rectangle(
                    panel.X + margin,
                    sideY,
                    Math.Max(1, panel.Width - (margin * 2)),
                    Math.Max(1, sideBottom - sideY));
                return;
            }

            var reservedSideWidth = panel.Width >= 840 ? 240 : 200;
            var availableCanvasWidth = Math.Max(
                CustomBubbleDocument.BubbleWidth * 6,
                panel.Width - (wideMargin * 2) - gap - reservedSideWidth);
            var availableCanvasHeight = Math.Max(
                CustomBubbleDocument.BubbleHeight * 6,
                panel.Height - 160);
            var cellSize = Math.Clamp(
                Math.Min(availableCanvasWidth / CustomBubbleDocument.BubbleWidth, availableCanvasHeight / CustomBubbleDocument.BubbleHeight),
                6,
                14);
            gridBounds = new Rectangle(
                panel.X + wideMargin,
                panel.Y + 64,
                CustomBubbleDocument.BubbleWidth * cellSize,
                CustomBubbleDocument.BubbleHeight * cellSize);
            shellBounds = gridBounds;

            var sideX = gridBounds.Right + gap;
            var sideRight = panel.Right - wideMargin;
            sideBounds = new Rectangle(
                sideX,
                panel.Y + 54,
                Math.Max(1, sideRight - sideX),
                Math.Max(1, panel.Bottom - panel.Y - 78));
        }

        private void GetLayout(
            out Rectangle gridBounds,
            out Rectangle pencilBounds,
            out Rectangle eraserBounds,
            out Rectangle eyedropperBounds,
            out Rectangle undoBounds,
            out Rectangle redoBounds,
            out Rectangle clearBounds,
            out Rectangle fillBounds,
            out Rectangle gridToggleBounds,
            out Rectangle presetPaletteTabBounds,
            out Rectangle customPaletteTabBounds,
            out Rectangle paletteGridBounds,
            out Rectangle brushSizeBar,
            out Rectangle saveBounds,
            out Rectangle cancelBounds,
            out Rectangle redBar,
            out Rectangle greenBar,
            out Rectangle blueBar,
            out Rectangle alphaBar)
        {
            var panel = GetPanelBounds();
            GetShellAndSideBounds(out _, out gridBounds, out var sideBounds);
            var previewBounds = GetPreviewBounds();
            var sideX = sideBounds.X;
            var sideWidth = sideBounds.Width;
            var buttonHeight = 26;
            var gap = 6;
            const int canvasButtonCount = 7;
            var canvasButtonArea = GetCanvasToolButtonArea(panel, gridBounds);
            pencilBounds = GetCanvasToolButtonBounds(canvasButtonArea, 0, canvasButtonCount);
            eraserBounds = GetCanvasToolButtonBounds(canvasButtonArea, 1, canvasButtonCount);
            eyedropperBounds = GetCanvasToolButtonBounds(canvasButtonArea, 2, canvasButtonCount);
            fillBounds = GetCanvasToolButtonBounds(canvasButtonArea, 3, canvasButtonCount);
            clearBounds = GetCanvasToolButtonBounds(canvasButtonArea, 4, canvasButtonCount);
            undoBounds = GetCanvasToolButtonBounds(canvasButtonArea, 5, canvasButtonCount);
            redoBounds = GetCanvasToolButtonBounds(canvasButtonArea, 6, canvasButtonCount);

            if (UseStackedEditorLayout(panel))
            {
                GetStackedControlColumns(sideBounds, out var leftColumn, out var rightColumn, out var singleColumn);
                var saveTotalWidth = Math.Min(sideWidth, 220);
                var saveButtonWidth = Math.Max(1, (saveTotalWidth - gap) / 2);
                var saveX = sideX + ((sideWidth - ((saveButtonWidth * 2) + gap)) / 2);
                saveBounds = new Rectangle(saveX, panel.Bottom - 42, saveButtonWidth, buttonHeight);
                cancelBounds = new Rectangle(saveBounds.Right + gap, saveBounds.Y, saveButtonWidth, buttonHeight);

                var controlsColumn = singleColumn ? sideBounds : rightColumn;
                if (singleColumn)
                {
                    gridToggleBounds = new Rectangle(sideX, previewBounds.Bottom + 8, sideWidth, buttonHeight);
                }
                else
                {
                    gridToggleBounds = new Rectangle(leftColumn.X, previewBounds.Bottom + 8, leftColumn.Width, buttonHeight);
                }

                var compactTabY = singleColumn ? gridToggleBounds.Bottom + 8 : controlsColumn.Y;
                var compactTabWidth = Math.Max(1, (controlsColumn.Width - gap) / 2);
                presetPaletteTabBounds = new Rectangle(controlsColumn.X, compactTabY, compactTabWidth, 22);
                customPaletteTabBounds = new Rectangle(presetPaletteTabBounds.Right + gap, compactTabY, compactTabWidth, 22);
                var compactSliderStackHeight = GetSliderStackHeight();
                var compactPaletteHeightBudget = saveBounds.Y
                    - 24
                    - presetPaletteTabBounds.Bottom
                    - 10
                    - 34
                    - compactSliderStackHeight;
                var compactPaletteCellSize = Math.Max(
                    4,
                    Math.Min(
                        PaletteCellSize,
                        Math.Min(
                            controlsColumn.Width / PaletteColumns,
                            compactPaletteHeightBudget / PaletteRows)));
                var compactPaletteWidth = PaletteColumns * compactPaletteCellSize;
                paletteGridBounds = new Rectangle(
                    controlsColumn.X + Math.Max(0, (controlsColumn.Width - compactPaletteWidth) / 2),
                    presetPaletteTabBounds.Bottom + 10,
                    compactPaletteWidth,
                    PaletteRows * compactPaletteCellSize);

                SetSliderBounds(
                    controlsColumn.X,
                    controlsColumn.Width,
                    paletteGridBounds.Bottom + 34,
                    saveBounds.Y - 24,
                    out brushSizeBar,
                    out redBar,
                    out greenBar,
                    out blueBar,
                    out alphaBar);
                return;
            }

            gridToggleBounds = new Rectangle(sideX, previewBounds.Bottom + 8, sideWidth, buttonHeight);

            var tabY = gridToggleBounds.Bottom + 8;
            var tabWidth = Math.Max(1, (sideWidth - gap) / 2);
            presetPaletteTabBounds = new Rectangle(sideX, tabY, tabWidth, 22);
            customPaletteTabBounds = new Rectangle(presetPaletteTabBounds.Right + gap, tabY, tabWidth, 22);
            var paletteWidth = PaletteColumns * PaletteCellSize;
            paletteGridBounds = new Rectangle(
                sideX + Math.Max(0, (sideWidth - paletteWidth) / 2),
                presetPaletteTabBounds.Bottom + 10,
                paletteWidth,
                PaletteRows * PaletteCellSize);

            var buttonWidth = Math.Max(1, (sideWidth - gap) / 2);
            saveBounds = new Rectangle(sideX, panel.Bottom - 52, buttonWidth, buttonHeight);
            cancelBounds = new Rectangle(saveBounds.Right + gap, saveBounds.Y, buttonWidth, buttonHeight);

            SetSliderBounds(
                sideX,
                sideWidth,
                paletteGridBounds.Bottom + 40,
                saveBounds.Y - 24,
                out brushSizeBar,
                out redBar,
                out greenBar,
                out blueBar,
                out alphaBar);
        }

        private void SetSliderBounds(
            int x,
            int width,
            int desiredY,
            int maxBottom,
            out Rectangle brushSizeBar,
            out Rectangle redBar,
            out Rectangle greenBar,
            out Rectangle blueBar,
            out Rectangle alphaBar)
        {
            var barWidth = Math.Max(1, width);
            var barHeight = 10;
            var barGap = 17;
            var sliderStackHeight = GetSliderStackHeight();
            var minimumY = desiredY - 8;
            var maxY = maxBottom - sliderStackHeight;
            var barY = maxY >= minimumY
                ? Math.Min(desiredY, maxY)
                : maxY;
            brushSizeBar = new Rectangle(x, barY, barWidth, barHeight);
            if (_customPaletteTab)
            {
                redBar = new Rectangle(x, brushSizeBar.Bottom + barGap, barWidth, barHeight);
                greenBar = new Rectangle(x, redBar.Bottom + barGap, barWidth, barHeight);
                blueBar = new Rectangle(x, greenBar.Bottom + barGap, barWidth, barHeight);
                alphaBar = new Rectangle(x, blueBar.Bottom + barGap, barWidth, barHeight);
            }
            else
            {
                redBar = Rectangle.Empty;
                greenBar = Rectangle.Empty;
                blueBar = Rectangle.Empty;
                alphaBar = Rectangle.Empty;
            }
        }

        private int GetSliderStackHeight()
        {
            const int barHeight = 10;
            const int barGap = 17;
            return _customPaletteTab
                ? barHeight + ((barGap + barHeight) * 4)
                : barHeight;
        }

        private readonly record struct CustomBubblePixelChange(int PixelIndex, ulong OldValue, ulong NewValue);

        private sealed record CustomBubbleStroke(IReadOnlyList<CustomBubblePixelChange> Changes);

        private enum CustomBubblePaintTool
        {
            Pencil,
            Eraser,
            Eyedropper,
        }
    }
}
