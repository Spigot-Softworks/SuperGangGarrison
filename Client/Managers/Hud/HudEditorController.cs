#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class HudEditorController
    {
        private const float ElementScaleStep = 0.1f;

        private readonly IHudContext _context;
        private string? _selectedElementId;
        private bool _dragging;
        private Vector2 _dragGrabOffset;

        public HudEditorController(IHudContext context)
        {
            _context = context;
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
            if (_context.IsKeyPressed(keyboard, Keys.Escape))
            {
                _context.CloseHudEditor();
                return;
            }

            var clickPressed = mouse.LeftButton == ButtonState.Pressed && _context._previousMouse.LeftButton != ButtonState.Pressed;
            var clickReleased = mouse.LeftButton != ButtonState.Pressed && _context._previousMouse.LeftButton == ButtonState.Pressed;
            var mousePosition = mouse.Position.ToVector2();
            GetToolbarBounds(out var gridBounds, out var snapBounds, out var shrinkBounds, out var growBounds, out var opacityBounds, out var visibilityBounds, out var addAbilityBounds, out var resetBounds, out var doneBounds);

            if (clickPressed)
            {
                if (gridBounds.Contains(mouse.Position))
                {
                    _context._hudLayoutProfile.GridVisible = !_context._hudLayoutProfile.GridVisible;
                    _context.SaveHudLayout();
                    return;
                }

                if (snapBounds.Contains(mouse.Position))
                {
                    _context._hudLayoutProfile.SnapEnabled = !_context._hudLayoutProfile.SnapEnabled;
                    _context.SaveHudLayout();
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

                if (opacityBounds.Contains(mouse.Position))
                {
                    CycleHudOpacity();
                    return;
                }

                if (visibilityBounds.Contains(mouse.Position))
                {
                    ToggleSelectedElementVisibility();
                    return;
                }

                if (addAbilityBounds.Contains(mouse.Position))
                {
                    _context.AddHudEditorDummyAbilitySlot();
                    return;
                }

                if (resetBounds.Contains(mouse.Position))
                {
                    _context.ResetHudLayoutElements();
                    _selectedElementId = null;
                    _dragging = false;
                    return;
                }

                if (doneBounds.Contains(mouse.Position))
                {
                    _context.CloseHudEditor();
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
                    _context.SaveHudLayout();
                }

                _dragging = false;
            }

            if (!_dragging || _selectedElementId is null || mouse.LeftButton != ButtonState.Pressed)
            {
                return;
            }

            if (!_context.TryResolveHudElementEvenIfHidden(_selectedElementId, out var selected) || selected.Layout.Locked)
            {
                return;
            }

            var desiredOrigin = mousePosition - _dragGrabOffset;
            if (_context._hudLayoutProfile.SnapEnabled)
            {
                var otherElements = _context.GetHudEditorElements()
                    .Where(pair => !string.Equals(pair.Key, _selectedElementId, StringComparison.Ordinal))
                    .Select(pair => pair.Value);
                desiredOrigin = HudEditorSnapper.SnapOrigin(
                    desiredOrigin,
                    selected.Layout,
                    otherElements,
                    _context.ViewportWidth,
                    _context.ViewportHeight,
                    Math.Max(1, _context._hudLayoutProfile.MinorGridSize));
            }

            _context.SetHudElementOrigin(_selectedElementId, desiredOrigin);
        }

        public void Draw()
        {
            if (_context._hudLayoutProfile.GridVisible)
            {
                DrawGrid();
            }

            DrawElementOutlines();
            DrawToolbar();
        }

        private bool TryHitTest(Point mousePosition, out HudResolvedElement hit)
        {
            foreach (var element in _context.GetHudEditorElements()
                         .Values
                         .OrderByDescending(static element => element.Layout.Layer))
            {
                if (element.Layout.Locked || !GetEditorElementBounds(element).Contains(mousePosition))
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
            var minor = Math.Max(2, _context._hudLayoutProfile.MinorGridSize);
            var major = Math.Max(minor, _context._hudLayoutProfile.MajorGridSize);
            var minorColor = new Color(255, 255, 255) * 0.12f;
            var majorColor = new Color(255, 255, 255) * 0.24f;

            for (var x = 0; x <= _context.ViewportWidth; x += minor)
            {
                var color = x % major == 0 ? majorColor : minorColor;
                _context._spriteBatch.Draw(_context._pixel, new Rectangle(x, 0, 1, _context.ViewportHeight), color);
            }

            for (var y = 0; y <= _context.ViewportHeight; y += minor)
            {
                var color = y % major == 0 ? majorColor : minorColor;
                _context._spriteBatch.Draw(_context._pixel, new Rectangle(0, y, _context.ViewportWidth, 1), color);
            }

            var legacyGuide = new Rectangle(
                Math.Max(0, _context.ViewportWidth - 800),
                Math.Max(0, _context.ViewportHeight - 600),
                Math.Min(800, _context.ViewportWidth),
                Math.Min(600, _context.ViewportHeight));
            DrawRectangleOutline(legacyGuide, new Color(255, 236, 160) * 0.28f, 1);
        }

        private void DrawElementOutlines()
        {
            foreach (var element in _context.GetHudEditorElements().Values.OrderBy(static element => element.Layout.Layer))
            {
                var selected = string.Equals(_selectedElementId, element.Layout.Id, StringComparison.Ordinal);
                var color = selected ? new Color(255, 236, 160) : new Color(120, 210, 255);
                DrawRectangleOutline(GetEditorElementBounds(element), color * (selected ? 0.95f : 0.7f), selected ? 2 : 1);
            }
        }

        private Rectangle GetEditorElementBounds(HudResolvedElement element)
        {
            var bounds = element.Bounds.Width > 0 && element.Bounds.Height > 0
                ? element.Bounds
                : element.Layout.ResolveBounds(element.Origin);
            var viewportBounds = new Rectangle(0, 0, _context.ViewportWidth, _context.ViewportHeight);
            bounds = Rectangle.Intersect(bounds, viewportBounds);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return Rectangle.Empty;
            }

            const int minimumHandleSize = 8;
            if (bounds.Width < minimumHandleSize)
            {
                var inflateX = (minimumHandleSize - bounds.Width + 1) / 2;
                bounds.Inflate(inflateX, 0);
            }

            if (bounds.Height < minimumHandleSize)
            {
                var inflateY = (minimumHandleSize - bounds.Height + 1) / 2;
                bounds.Inflate(0, inflateY);
            }

            return Rectangle.Intersect(bounds, viewportBounds);
        }

        private void DrawToolbar()
        {
            GetToolbarBounds(out var gridBounds, out var snapBounds, out var shrinkBounds, out var growBounds, out var opacityBounds, out var visibilityBounds, out var addAbilityBounds, out var resetBounds, out var doneBounds);
            var mouse = _context.GetFrameMouseState();
            DrawToolbarPanel(gridBounds, $"Grid {(_context._hudLayoutProfile.GridVisible ? "On" : "Off")}", gridBounds.Contains(mouse.Position));
            DrawToolbarPanel(snapBounds, $"Snap {(_context._hudLayoutProfile.SnapEnabled ? "On" : "Off")}", snapBounds.Contains(mouse.Position));
            DrawResizeToolbarButton(shrinkBounds, plus: false, shrinkBounds.Contains(mouse.Position));
            DrawResizeToolbarButton(growBounds, plus: true, growBounds.Contains(mouse.Position));
            DrawToolbarPanel(opacityBounds, $"Opacity {GetHudOpacityPercent()}%", opacityBounds.Contains(mouse.Position));
            DrawToolbarPanel(visibilityBounds, GetSelectedVisibilityLabel(), visibilityBounds.Contains(mouse.Position));
            DrawToolbarPanel(addAbilityBounds, "Ability +", addAbilityBounds.Contains(mouse.Position));
            DrawToolbarPanel(resetBounds, "Reset", resetBounds.Contains(mouse.Position));
            DrawToolbarPanel(doneBounds, "Done", doneBounds.Contains(mouse.Position));
        }

        private void DrawToolbarPanel(Rectangle bounds, string label, bool hovered)
        {
            var textScale = bounds.Width >= 112 ? 1f : bounds.Width >= 92 ? 0.86f : 0.74f;
            _context.DrawMenuButtonCentered(bounds, label, hovered, textScale);
        }

        private void DrawResizeToolbarButton(Rectangle bounds, bool plus, bool hovered)
        {
            _context.DrawMenuButtonCentered(bounds, string.Empty, hovered, 1f);

            var color = Color.White;
            var thickness = Math.Max(2, bounds.Height / 12);
            var length = Math.Max(12, Math.Min(bounds.Width, bounds.Height) / 2);
            var centerX = bounds.X + (bounds.Width / 2);
            var centerY = bounds.Y + (bounds.Height / 2);
            _context._spriteBatch.Draw(
                _context._pixel,
                new Rectangle(centerX - (length / 2), centerY - (thickness / 2), length, thickness),
                color);
            if (plus)
            {
                _context._spriteBatch.Draw(
                    _context._pixel,
                    new Rectangle(centerX - (thickness / 2), centerY - (length / 2), thickness, length),
                    color);
            }
        }

        private void GetToolbarBounds(out Rectangle gridBounds, out Rectangle snapBounds, out Rectangle shrinkBounds, out Rectangle growBounds, out Rectangle opacityBounds, out Rectangle visibilityBounds, out Rectangle addAbilityBounds, out Rectangle resetBounds, out Rectangle doneBounds)
        {
            const int gap = 8;
            const int resizeButtonWidth = 44;
            var availableWidth = Math.Max(0, _context.ViewportWidth - 16 - (gap * 8) - (resizeButtonWidth * 2));
            var maxButtonWidth = _context.ViewportWidth < 620 ? 104 : 132;
            var buttonWidth = Math.Clamp(availableWidth / 7, 64, maxButtonWidth);
            var buttonHeight = 36;
            var totalWidth = (buttonWidth * 7) + (resizeButtonWidth * 2) + (gap * 8);
            var x = Math.Max(8, (_context.ViewportWidth - totalWidth) / 2);
            var y = 12;
            gridBounds = new Rectangle(x, y, buttonWidth, buttonHeight);
            snapBounds = new Rectangle(gridBounds.Right + gap, y, buttonWidth, buttonHeight);
            shrinkBounds = new Rectangle(snapBounds.Right + gap, y, resizeButtonWidth, buttonHeight);
            growBounds = new Rectangle(shrinkBounds.Right + gap, y, resizeButtonWidth, buttonHeight);
            opacityBounds = new Rectangle(growBounds.Right + gap, y, buttonWidth, buttonHeight);
            visibilityBounds = new Rectangle(opacityBounds.Right + gap, y, buttonWidth, buttonHeight);
            addAbilityBounds = new Rectangle(visibilityBounds.Right + gap, y, buttonWidth, buttonHeight);
            resetBounds = new Rectangle(addAbilityBounds.Right + gap, y, buttonWidth, buttonHeight);
            doneBounds = new Rectangle(resetBounds.Right + gap, y, buttonWidth, buttonHeight);
        }

        private const float HudOpacityStep = 0.1f;

        private int GetHudOpacityPercent()
        {
            return (int)MathF.Round(_context._hudLayoutProfile.HudOpacity * 100f);
        }

        private void CycleHudOpacity()
        {
            var next = _context._hudLayoutProfile.HudOpacity - HudOpacityStep;
            if (next < HudLayoutProfile.MinHudOpacity - 0.001f)
            {
                next = HudLayoutProfile.MaxHudOpacity;
            }

            _context._hudLayoutProfile.HudOpacity = next;
            _context.SaveHudLayout();
        }

        private string GetSelectedVisibilityLabel()
        {
            if (_selectedElementId is null
                || !_context.TryResolveHudElementEvenIfHidden(_selectedElementId, out var selected))
            {
                return "Hide";
            }

            return selected.Layout.Visible ? "Hide" : "Show";
        }

        private void ToggleSelectedElementVisibility()
        {
            if (_selectedElementId is null
                || !_context.TryResolveHudElementEvenIfHidden(_selectedElementId, out var selected)
                || selected.Layout.Locked)
            {
                return;
            }

            if (_context.SetHudElementVisibility(_selectedElementId, !selected.Layout.Visible))
            {
                _context.SaveHudLayout();
            }
        }

        private void ResizeSelectedElement(float delta)
        {
            if (_selectedElementId is null
                || !_context.TryResolveHudElementEvenIfHidden(_selectedElementId, out var selected)
                || selected.Layout.Locked)
            {
                return;
            }

            if (_context.SetHudElementScale(_selectedElementId, selected.Layout.Scale + delta))
            {
                _context.SaveHudLayout();
            }
        }

        private void DrawRectangleOutline(Rectangle bounds, Color color, int thickness)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            thickness = Math.Max(1, thickness);
            _context._spriteBatch.Draw(_context._pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
            _context._spriteBatch.Draw(_context._pixel, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
            _context._spriteBatch.Draw(_context._pixel, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
            _context._spriteBatch.Draw(_context._pixel, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
        }
}
