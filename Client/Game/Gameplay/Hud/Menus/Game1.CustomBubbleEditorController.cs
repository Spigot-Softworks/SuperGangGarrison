#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.ClientShared;
using System;
using System.Collections.Generic;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class CustomBubbleEditorController
    {
        private const int MaxUndoStrokes = 128;
        private const int MinBrushSize = 1;
        private const int MaxBrushSize = 12;
        private const int DefaultBrushSize = 4;

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
            _undoStack.Clear();
            _redoStack.Clear();
            _activeStrokeChanges.Clear();
            _strokeActive = false;
            _dirty = false;
            _previewDirty = true;
        }

        public void Close()
        {
            _activeStrokeChanges.Clear();
            _strokeActive = false;
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
                out var gridToggleBounds,
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

            if (clickPressed)
            {
                if (pencilBounds.Contains(mouse.Position))
                {
                    _tool = CustomBubblePaintTool.Pencil;
                    return;
                }

                if (eraserBounds.Contains(mouse.Position))
                {
                    _tool = CustomBubblePaintTool.Eraser;
                    return;
                }

                if (eyedropperBounds.Contains(mouse.Position))
                {
                    _tool = CustomBubblePaintTool.Eyedropper;
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

                if (gridToggleBounds.Contains(mouse.Position))
                {
                    _gridVisible = !_gridVisible;
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
                if (TrySetColorChannelFromBar(redBar, mouse.Position, mouse.X, 0)
                    || TrySetColorChannelFromBar(greenBar, mouse.Position, mouse.X, 1)
                    || TrySetColorChannelFromBar(blueBar, mouse.Position, mouse.X, 2)
                    || TrySetColorChannelFromBar(alphaBar, mouse.Position, mouse.X, 3)
                    || TrySetBrushSizeFromBar(brushSizeBar, mouse.Position, mouse.X))
                {
                    return;
                }
            }

            if (clickPressed && TryGetGridPixel(gridBounds, mouse.Position, out var pixelIndex))
            {
                if (_tool == CustomBubblePaintTool.Eyedropper)
                {
                    _currentColor = ReadRgba64Color(_pixels, pixelIndex);
                    _tool = CustomBubblePaintTool.Pencil;
                    return;
                }

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
                out var gridToggleBounds,
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
            DrawToolButton(gridToggleBounds, _gridVisible ? "Grid On" : "Grid Off", _gridVisible, gridToggleBounds.Contains(mousePosition));
            DrawBrushSizeBar(brushSizeBar);
            DrawToolButton(saveBounds, _dirty ? "Save" : "Save", false, saveBounds.Contains(mousePosition));
            DrawToolButton(cancelBounds, "Cancel", false, cancelBounds.Contains(mousePosition));
            DrawCurrentColorSwatch(panel);
            DrawColorBar(redBar, "R", _currentColor.R, new Color(220, 82, 82));
            DrawColorBar(greenBar, "G", _currentColor.G, new Color(95, 188, 96));
            DrawColorBar(blueBar, "B", _currentColor.B, new Color(84, 128, 230));
            DrawColorBar(alphaBar, "A", _currentColor.A, new Color(220, 220, 220));
        }

        private void BeginStroke()
        {
            _activeStrokeChanges.Clear();
            _strokeActive = true;
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

        private void DrawOutline(Rectangle bounds, Color color, int thickness)
        {
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
        }

        private Rectangle GetPanelBounds()
        {
            var width = Math.Min(_game.ViewportWidth - 32, 900);
            var height = Math.Min(_game.ViewportHeight - 32, 600);
            return new Rectangle((_game.ViewportWidth - width) / 2, (_game.ViewportHeight - height) / 2, width, height);
        }

        private Rectangle GetPreviewBounds()
        {
            GetShellAndSideBounds(out _, out _, out var sideBounds);
            return new Rectangle(sideBounds.X, sideBounds.Y, sideBounds.Width, 122);
        }

        private void GetShellAndSideBounds(out Rectangle shellBounds, out Rectangle gridBounds, out Rectangle sideBounds)
        {
            var panel = GetPanelBounds();
            const int margin = 24;
            const int gap = 16;
            var reservedSideWidth = panel.Width >= 840 ? 220 : 140;
            var availableCanvasWidth = Math.Max(
                CustomBubbleDocument.BubbleWidth * 6,
                panel.Width - (margin * 2) - gap - reservedSideWidth);
            var availableCanvasHeight = Math.Max(
                CustomBubbleDocument.BubbleHeight * 6,
                panel.Height - 92);
            var cellSize = Math.Clamp(
                Math.Min(availableCanvasWidth / CustomBubbleDocument.BubbleWidth, availableCanvasHeight / CustomBubbleDocument.BubbleHeight),
                6,
                14);
            gridBounds = new Rectangle(
                panel.X + margin,
                panel.Y + 64,
                CustomBubbleDocument.BubbleWidth * cellSize,
                CustomBubbleDocument.BubbleHeight * cellSize);
            shellBounds = gridBounds;

            var sideX = gridBounds.Right + gap;
            var sideRight = panel.Right - margin;
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
            out Rectangle gridToggleBounds,
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
            var buttonHeight = 32;
            var gap = 7;
            var buttonWidth = Math.Max(1, (sideWidth - gap) / 2);
            var toolY = previewBounds.Bottom + 12;
            pencilBounds = new Rectangle(sideX, toolY, buttonWidth, buttonHeight);
            eraserBounds = new Rectangle(pencilBounds.Right + gap, toolY, buttonWidth, buttonHeight);
            eyedropperBounds = new Rectangle(sideX, pencilBounds.Bottom + gap, buttonWidth, buttonHeight);
            clearBounds = new Rectangle(eyedropperBounds.Right + gap, eyedropperBounds.Y, buttonWidth, buttonHeight);
            undoBounds = new Rectangle(sideX, eyedropperBounds.Bottom + gap, buttonWidth, buttonHeight);
            redoBounds = new Rectangle(undoBounds.Right + gap, undoBounds.Y, buttonWidth, buttonHeight);
            gridToggleBounds = new Rectangle(sideX, undoBounds.Bottom + gap, sideWidth, buttonHeight);

            var barWidth = Math.Max(1, sideWidth);
            var barHeight = 12;
            var barGap = 18;
            var barY = gridToggleBounds.Bottom + 24;
            brushSizeBar = new Rectangle(sideX, barY, barWidth, barHeight);
            redBar = new Rectangle(sideX, brushSizeBar.Bottom + barGap, barWidth, barHeight);
            greenBar = new Rectangle(sideX, redBar.Bottom + barGap, barWidth, barHeight);
            blueBar = new Rectangle(sideX, greenBar.Bottom + barGap, barWidth, barHeight);
            alphaBar = new Rectangle(sideX, blueBar.Bottom + barGap, barWidth, barHeight);

            saveBounds = new Rectangle(sideX, panel.Bottom - 52, buttonWidth, buttonHeight);
            cancelBounds = new Rectangle(saveBounds.Right + gap, saveBounds.Y, buttonWidth, buttonHeight);
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
