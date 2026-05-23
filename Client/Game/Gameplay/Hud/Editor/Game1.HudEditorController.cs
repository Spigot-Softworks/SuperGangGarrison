#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class HudEditorController
    {
        private const float ElementScaleStep = 0.1f;

        private readonly Game1 _game;
        private string? _selectedElementId;
        private bool _dragging;
        private Vector2 _dragGrabOffset;

        public HudEditorController(Game1 game)
        {
            _game = game;
        }

        public void Open()
        {
            _selectedElementId = null;
            _dragging = false;
            _dragGrabOffset = Vector2.Zero;
        }

        public void Close()
        {
            _selectedElementId = null;
            _dragging = false;
            _dragGrabOffset = Vector2.Zero;
        }

        public void Update(KeyboardState keyboard, MouseState mouse)
        {
            if (_game.IsKeyPressed(keyboard, Keys.Escape))
            {
                _game.CloseHudEditor();
                return;
            }

            var clickPressed = mouse.LeftButton == ButtonState.Pressed && _game._previousMouse.LeftButton != ButtonState.Pressed;
            var clickReleased = mouse.LeftButton != ButtonState.Pressed && _game._previousMouse.LeftButton == ButtonState.Pressed;
            var mousePosition = mouse.Position.ToVector2();
            GetToolbarBounds(out var gridBounds, out var snapBounds, out var shrinkBounds, out var growBounds, out var resetBounds, out var doneBounds);

            if (clickPressed)
            {
                if (gridBounds.Contains(mouse.Position))
                {
                    _game._hudLayoutProfile.GridVisible = !_game._hudLayoutProfile.GridVisible;
                    _game.SaveHudLayout();
                    return;
                }

                if (snapBounds.Contains(mouse.Position))
                {
                    _game._hudLayoutProfile.SnapEnabled = !_game._hudLayoutProfile.SnapEnabled;
                    _game.SaveHudLayout();
                    return;
                }

                if (shrinkBounds.Contains(mouse.Position))
                {
                    ResizeSelectedElement(-ElementScaleStep);
                    return;
                }

                if (growBounds.Contains(mouse.Position))
                {
                    ResizeSelectedElement(ElementScaleStep);
                    return;
                }

                if (resetBounds.Contains(mouse.Position))
                {
                    _game.ResetHudLayoutElements();
                    _selectedElementId = null;
                    _dragging = false;
                    return;
                }

                if (doneBounds.Contains(mouse.Position))
                {
                    _game.CloseHudEditor();
                    return;
                }

                if (TryHitTest(mouse.Position, out var hit))
                {
                    _selectedElementId = hit.Layout.Id;
                    _dragging = true;
                    _dragGrabOffset = mousePosition - hit.Origin;
                    return;
                }

                _selectedElementId = null;
            }

            if (clickReleased)
            {
                if (_dragging)
                {
                    _game.SaveHudLayout();
                }

                _dragging = false;
            }

            if (!_dragging || _selectedElementId is null || mouse.LeftButton != ButtonState.Pressed)
            {
                return;
            }

            if (!_game.TryResolveHudElementEvenIfHidden(_selectedElementId, out var selected) || selected.Layout.Locked)
            {
                return;
            }

            var desiredOrigin = mousePosition - _dragGrabOffset;
            if (_game._hudLayoutProfile.SnapEnabled)
            {
                var otherElements = _game.GetHudEditorElements()
                    .Where(pair => !string.Equals(pair.Key, _selectedElementId, StringComparison.Ordinal))
                    .Select(pair => pair.Value);
                desiredOrigin = HudEditorSnapper.SnapOrigin(
                    desiredOrigin,
                    selected.Layout,
                    otherElements,
                    _game.ViewportWidth,
                    _game.ViewportHeight,
                    Math.Max(1, _game._hudLayoutProfile.MinorGridSize));
            }

            _game.SetHudElementOrigin(_selectedElementId, desiredOrigin);
        }

        public void Draw()
        {
            if (_game._hudLayoutProfile.GridVisible)
            {
                DrawGrid();
            }

            DrawElementOutlines();
            DrawToolbar();
        }

        private bool TryHitTest(Point mousePosition, out HudResolvedElement hit)
        {
            foreach (var element in _game.GetHudEditorElements()
                         .Values
                         .OrderByDescending(static element => element.Layout.Layer))
            {
                if (element.Layout.Locked || !element.Bounds.Contains(mousePosition))
                {
                    continue;
                }

                hit = element;
                return true;
            }

            hit = default;
            return false;
        }

        private void DrawGrid()
        {
            var minor = Math.Max(2, _game._hudLayoutProfile.MinorGridSize);
            var major = Math.Max(minor, _game._hudLayoutProfile.MajorGridSize);
            var minorColor = new Color(255, 255, 255) * 0.12f;
            var majorColor = new Color(255, 255, 255) * 0.24f;

            for (var x = 0; x <= _game.ViewportWidth; x += minor)
            {
                var color = x % major == 0 ? majorColor : minorColor;
                _game._spriteBatch.Draw(_game._pixel, new Rectangle(x, 0, 1, _game.ViewportHeight), color);
            }

            for (var y = 0; y <= _game.ViewportHeight; y += minor)
            {
                var color = y % major == 0 ? majorColor : minorColor;
                _game._spriteBatch.Draw(_game._pixel, new Rectangle(0, y, _game.ViewportWidth, 1), color);
            }

            var legacyGuide = new Rectangle(
                Math.Max(0, _game.ViewportWidth - 800),
                Math.Max(0, _game.ViewportHeight - 600),
                Math.Min(800, _game.ViewportWidth),
                Math.Min(600, _game.ViewportHeight));
            DrawRectangleOutline(legacyGuide, new Color(255, 236, 160) * 0.28f, 1);
        }

        private void DrawElementOutlines()
        {
            foreach (var element in _game.GetHudEditorElements().Values.OrderBy(static element => element.Layout.Layer))
            {
                var selected = string.Equals(_selectedElementId, element.Layout.Id, StringComparison.Ordinal);
                var color = selected ? new Color(255, 236, 160) : new Color(120, 210, 255);
                DrawRectangleOutline(element.Bounds, color * (selected ? 0.95f : 0.7f), selected ? 2 : 1);
            }
        }

        private void DrawToolbar()
        {
            GetToolbarBounds(out var gridBounds, out var snapBounds, out var shrinkBounds, out var growBounds, out var resetBounds, out var doneBounds);
            var mouse = Game1.GetCurrentMouseState();
            DrawToolbarPanel(gridBounds, $"Grid {(_game._hudLayoutProfile.GridVisible ? "On" : "Off")}", gridBounds.Contains(mouse.Position));
            DrawToolbarPanel(snapBounds, $"Snap {(_game._hudLayoutProfile.SnapEnabled ? "On" : "Off")}", snapBounds.Contains(mouse.Position));
            DrawToolbarPanel(shrinkBounds, "-", shrinkBounds.Contains(mouse.Position));
            DrawToolbarPanel(growBounds, "+", growBounds.Contains(mouse.Position));
            DrawToolbarPanel(resetBounds, "Reset", resetBounds.Contains(mouse.Position));
            DrawToolbarPanel(doneBounds, "Done", doneBounds.Contains(mouse.Position));
        }

        private void DrawToolbarPanel(Rectangle bounds, string label, bool hovered)
        {
            var textScale = bounds.Width >= 112 ? 1f : 0.86f;
            _game.DrawMenuButtonCentered(bounds, label, hovered, textScale);
        }

        private void GetToolbarBounds(out Rectangle gridBounds, out Rectangle snapBounds, out Rectangle shrinkBounds, out Rectangle growBounds, out Rectangle resetBounds, out Rectangle doneBounds)
        {
            const int gap = 8;
            const int resizeButtonWidth = 44;
            var availableWidth = Math.Max(0, _game.ViewportWidth - 16 - (gap * 5) - (resizeButtonWidth * 2));
            var maxButtonWidth = _game.ViewportWidth < 620 ? 104 : 132;
            var buttonWidth = Math.Clamp(availableWidth / 4, 68, maxButtonWidth);
            var buttonHeight = 36;
            var totalWidth = (buttonWidth * 4) + (resizeButtonWidth * 2) + (gap * 5);
            var x = Math.Max(8, (_game.ViewportWidth - totalWidth) / 2);
            var y = 12;
            gridBounds = new Rectangle(x, y, buttonWidth, buttonHeight);
            snapBounds = new Rectangle(gridBounds.Right + gap, y, buttonWidth, buttonHeight);
            shrinkBounds = new Rectangle(snapBounds.Right + gap, y, resizeButtonWidth, buttonHeight);
            growBounds = new Rectangle(shrinkBounds.Right + gap, y, resizeButtonWidth, buttonHeight);
            resetBounds = new Rectangle(growBounds.Right + gap, y, buttonWidth, buttonHeight);
            doneBounds = new Rectangle(resetBounds.Right + gap, y, buttonWidth, buttonHeight);
        }

        private void ResizeSelectedElement(float delta)
        {
            if (_selectedElementId is null
                || !_game.TryResolveHudElementEvenIfHidden(_selectedElementId, out var selected)
                || selected.Layout.Locked)
            {
                return;
            }

            if (_game.SetHudElementScale(_selectedElementId, selected.Layout.Scale + delta))
            {
                _game.SaveHudLayout();
            }
        }

        private void DrawRectangleOutline(Rectangle bounds, Color color, int thickness)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            thickness = Math.Max(1, thickness);
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
        }
    }
}
