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
    private readonly HashSet<int> _builderSelectedEntityIndices = new();
    private readonly List<int> _builderAreaSelectScratch = new();
    private readonly List<(int Index, float X, float Y)> _builderMultiDragSnapshots = new();

    private bool _builderAreaSelectDragging;
    private Point _builderAreaSelectStartScreen;
    private Point _builderAreaSelectCurrentScreen;

    private const int GarrisonBuilderAreaSelectMinScreenSize = 4;
    private const float GarrisonBuilderAreaSelectFillOpacity = 0.1f;
    private const float GarrisonBuilderAreaSelectOutlineOpacity = 0.4f;
    private const int GarrisonBuilderSelectionMarkerMinScreenSize = 7;
    private const int GarrisonBuilderSelectionMarkerMaxScreenSize = 10;
    private const float GarrisonBuilderSelectionMarkerBaseScreenSize = 9f;
    private const float GarrisonBuilderSelectionMarkerFillOpacity = 0.42f;

    private int GetGarrisonBuilderSelectedEntityCount() => _builderSelectedEntityIndices.Count;

    private bool IsGarrisonBuilderMapEntitySelected(int entityIndex)
    {
        return entityIndex >= 0 && _builderSelectedEntityIndices.Contains(entityIndex);
    }

    private void ClearGarrisonBuilderMapEntitySelection()
    {
        _builderSelectedEntityIndices.Clear();
        _builderSelectedEntityIndex = -1;
    }

    private void SetGarrisonBuilderMapEntitySelection(IReadOnlyList<int> entityIndices, int primaryEntityIndex)
    {
        _builderSelectedEntityIndices.Clear();
        for (var index = 0; index < entityIndices.Count; index += 1)
        {
            var entityIndex = entityIndices[index];
            if (IsValidGarrisonBuilderMapEntityIndex(entityIndex))
            {
                _builderSelectedEntityIndices.Add(entityIndex);
            }
        }

        if (primaryEntityIndex >= 0 && _builderSelectedEntityIndices.Contains(primaryEntityIndex))
        {
            _builderSelectedEntityIndex = primaryEntityIndex;
        }
        else if (_builderSelectedEntityIndices.Count > 0)
        {
            _builderSelectedEntityIndex = _builderSelectedEntityIndices.Min();
        }
        else
        {
            _builderSelectedEntityIndex = -1;
        }
    }

    private void SelectSingleGarrisonBuilderMapEntity(int entityIndex)
    {
        if (!IsValidGarrisonBuilderMapEntityIndex(entityIndex))
        {
            ClearGarrisonBuilderMapEntitySelection();
            return;
        }

        SetGarrisonBuilderMapEntitySelection([entityIndex], entityIndex);
    }

    private void AdjustGarrisonBuilderMapEntitySelectionAfterRemoval(int removedIndex)
    {
        if (_builderSelectedEntityIndices.Remove(removedIndex))
        {
        }

        if (_builderSelectedEntityIndices.Count == 0)
        {
            AdjustGarrisonBuilderSelectedEntityIndexAfterRemoval(removedIndex);
            return;
        }

        var adjusted = new HashSet<int>();
        foreach (var entityIndex in _builderSelectedEntityIndices)
        {
            adjusted.Add(entityIndex > removedIndex ? entityIndex - 1 : entityIndex);
        }

        _builderSelectedEntityIndices.Clear();
        foreach (var entityIndex in adjusted)
        {
            _builderSelectedEntityIndices.Add(entityIndex);
        }

        if (_builderSelectedEntityIndex == removedIndex)
        {
            _builderSelectedEntityIndex = _builderSelectedEntityIndices.Count > 0
                ? _builderSelectedEntityIndices.Min()
                : -1;
        }
        else
        {
            AdjustGarrisonBuilderSelectedEntityIndexAfterRemoval(removedIndex);
            if (_builderSelectedEntityIndex >= 0
                && !_builderSelectedEntityIndices.Contains(_builderSelectedEntityIndex))
            {
                _builderSelectedEntityIndex = _builderSelectedEntityIndices.Min();
            }
        }
    }

    private void BeginGarrisonBuilderAreaSelect(Point screenPosition)
    {
        _builderAreaSelectDragging = true;
        _builderAreaSelectStartScreen = screenPosition;
        _builderAreaSelectCurrentScreen = screenPosition;
        _builderEntityDragging = false;
        _builderActiveResizeHandle = GarrisonBuilderResizeHandle.None;
        CloseGarrisonBuilderEntityContextMenu();
        CloseGarrisonBuilderEntityOverlapPicker();
    }

    private void UpdateGarrisonBuilderAreaSelect(Point screenPosition)
    {
        if (!_builderAreaSelectDragging)
        {
            return;
        }

        _builderAreaSelectCurrentScreen = screenPosition;
    }

    private void CommitGarrisonBuilderAreaSelect(Point screenPosition)
    {
        if (!_builderAreaSelectDragging)
        {
            return;
        }

        _builderAreaSelectCurrentScreen = screenPosition;
        _builderAreaSelectDragging = false;

        var screenRect = GetGarrisonBuilderAreaSelectScreenRectangle();
        if (screenRect.Width < GarrisonBuilderAreaSelectMinScreenSize
            && screenRect.Height < GarrisonBuilderAreaSelectMinScreenSize)
        {
            ClearGarrisonBuilderMapEntitySelection();
            _builderStatus = "selection cleared";
            if (!GetGarrisonBuilderPropertyEditorBounds().Contains(screenPosition))
            {
                CloseGarrisonBuilderPropertyEditor(applyChanges: true);
            }

            return;
        }

        CollectGarrisonBuilderEntitiesInScreenRectangle(screenRect, _builderAreaSelectScratch);
        if (_builderAreaSelectScratch.Count == 0)
        {
            ClearGarrisonBuilderMapEntitySelection();
            _builderStatus = "selection cleared";
            return;
        }

        var primary = _builderAreaSelectScratch[^1];
        SetGarrisonBuilderMapEntitySelection(_builderAreaSelectScratch, primary);
        _builderSelectedEntityType = string.Empty;
        _builderActiveTool = GarrisonBuilderTool.Select;
        _builderStatus = _builderAreaSelectScratch.Count == 1
            ? $"selected {_builderEntities[primary].Type}"
            : $"selected {_builderAreaSelectScratch.Count} entities";
    }

    private Rectangle GetGarrisonBuilderAreaSelectScreenRectangle()
    {
        var left = Math.Min(_builderAreaSelectStartScreen.X, _builderAreaSelectCurrentScreen.X);
        var top = Math.Min(_builderAreaSelectStartScreen.Y, _builderAreaSelectCurrentScreen.Y);
        var right = Math.Max(_builderAreaSelectStartScreen.X, _builderAreaSelectCurrentScreen.X);
        var bottom = Math.Max(_builderAreaSelectStartScreen.Y, _builderAreaSelectCurrentScreen.Y);
        return new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    private void CollectGarrisonBuilderEntitiesInScreenRectangle(Rectangle screenRect, IList<int> results)
    {
        results.Clear();
        var worldRect = ScreenRectangleToGarrisonBuilderWorldRectangle(screenRect);
        for (var index = 0; index < _builderEntities.Count; index += 1)
        {
            if (IsGarrisonBuilderEntityHidden(index))
            {
                continue;
            }

            if (DoesGarrisonBuilderEntityIntersectRectangle(_builderEntities[index], worldRect))
            {
                results.Add(index);
            }
        }
    }

    private RectangleF ScreenRectangleToGarrisonBuilderWorldRectangle(Rectangle screenRect)
    {
        var topLeft = BuilderScreenToWorld(new Vector2(screenRect.Left, screenRect.Top));
        var bottomRight = BuilderScreenToWorld(new Vector2(screenRect.Right, screenRect.Bottom));
        var left = MathF.Min(topLeft.X, bottomRight.X);
        var top = MathF.Min(topLeft.Y, bottomRight.Y);
        var right = MathF.Max(topLeft.X, bottomRight.X);
        var bottom = MathF.Max(topLeft.Y, bottomRight.Y);
        return new RectangleF(left, top, MathF.Max(0.001f, right - left), MathF.Max(0.001f, bottom - top));
    }

    private void DrawGarrisonBuilderAreaSelectionRectangle()
    {
        if (!_builderAreaSelectDragging)
        {
            return;
        }

        var rect = GetGarrisonBuilderAreaSelectScreenRectangle();
        if (rect.Width > 0 && rect.Height > 0)
        {
            _spriteBatch.End();
            _spriteBatch.Begin(
                samplerState: SamplerState.PointClamp,
                blendState: BlendState.Additive,
                rasterizerState: RasterizerState.CullNone);
            _spriteBatch.Draw(_pixel, rect, Color.White * GarrisonBuilderAreaSelectFillOpacity);
            _spriteBatch.End();
            _spriteBatch.Begin(
                samplerState: SamplerState.PointClamp,
                rasterizerState: RasterizerState.CullNone);
        }

        DrawGarrisonBuilderDashedRectangleOutline(rect, Color.Black * GarrisonBuilderAreaSelectOutlineOpacity);
    }

    private void DrawGarrisonBuilderDashedRectangleOutline(Rectangle rectangle, Color color)
    {
        var topLeft = new Vector2(rectangle.Left, rectangle.Top);
        var topRight = new Vector2(rectangle.Right, rectangle.Top);
        var bottomRight = new Vector2(rectangle.Right, rectangle.Bottom);
        var bottomLeft = new Vector2(rectangle.Left, rectangle.Bottom);
        DrawGarrisonBuilderDashedLine(topLeft, topRight, color);
        DrawGarrisonBuilderDashedLine(topRight, bottomRight, color);
        DrawGarrisonBuilderDashedLine(bottomRight, bottomLeft, color);
        DrawGarrisonBuilderDashedLine(bottomLeft, topLeft, color);
    }

    private int GetGarrisonBuilderSelectionMarkerScreenSize()
    {
        var zoom = _builderUseModernUi ? _builderZoom : 1f;
        return Math.Clamp(
            (int)MathF.Round(GarrisonBuilderSelectionMarkerBaseScreenSize * zoom),
            GarrisonBuilderSelectionMarkerMinScreenSize,
            GarrisonBuilderSelectionMarkerMaxScreenSize);
    }

    private void DrawGarrisonBuilderSelectionMarker(Rectangle bounds)
    {
        _spriteBatch.Draw(_pixel, bounds, Color.White * GarrisonBuilderSelectionMarkerFillOpacity);
    }

    private void DrawGarrisonBuilderMapEntitySelectionHighlights()
    {
        var markerSize = GetGarrisonBuilderSelectionMarkerScreenSize();
        var half = markerSize / 2;

        foreach (var entityIndex in GetGarrisonBuilderSelectedEntityIndicesForDisplay())
        {
            if (!IsValidGarrisonBuilderMapEntityIndex(entityIndex))
            {
                continue;
            }

            if (!TryGetGarrisonBuilderEntitySelectionMarkerScreen(_builderEntities[entityIndex], out var screen))
            {
                continue;
            }

            var bounds = new Rectangle(
                (int)MathF.Round(screen.X) - half,
                (int)MathF.Round(screen.Y) - half,
                markerSize,
                markerSize);
            DrawGarrisonBuilderSelectionMarker(bounds);
        }
    }

    private IEnumerable<int> GetGarrisonBuilderSelectedEntityIndicesForDisplay()
    {
        if (_builderSelectedEntityIndices.Count > 0)
        {
            return _builderSelectedEntityIndices.OrderBy(index => index);
        }

        if (_builderSelectedEntityIndex >= 0)
        {
            return [_builderSelectedEntityIndex];
        }

        return Array.Empty<int>();
    }

    private IReadOnlyList<int> GetGarrisonBuilderDragEntityIndices(int anchorEntityIndex)
    {
        if (IsGarrisonBuilderMapEntitySelected(anchorEntityIndex) && _builderSelectedEntityIndices.Count > 1)
        {
            return _builderSelectedEntityIndices.OrderBy(index => index).ToArray();
        }

        return [anchorEntityIndex];
    }

    private bool TryBeginGarrisonBuilderEntityDrag(Vector2 world, int anchorEntityIndex)
    {
        if (!IsValidGarrisonBuilderMapEntityIndex(anchorEntityIndex))
        {
            return false;
        }

        var entity = _builderEntities[anchorEntityIndex];
        if (!TryGetGarrisonBuilderEntityWorldBounds(entity, out var left, out var top, out var width, out var height)
            || !new RectangleF(left, top, width, height).Contains(world.X, world.Y))
        {
            return false;
        }

        CloseGarrisonBuilderEntityContextMenu();
        RecordGarrisonBuilderHistory();

        var dragIndices = GetGarrisonBuilderDragEntityIndices(anchorEntityIndex).ToList();
        if (_builderAltHeld)
        {
            if (!TryDuplicateGarrisonBuilderEntities(dragIndices, placeAtSourcePosition: true, out var duplicated))
            {
                return false;
            }

            var anchorSourcePosition = dragIndices.IndexOf(anchorEntityIndex);
            dragIndices = duplicated;
            anchorEntityIndex = anchorSourcePosition >= 0
                ? duplicated[anchorSourcePosition]
                : duplicated[^1];
            SetGarrisonBuilderMapEntitySelection(duplicated, anchorEntityIndex);
            entity = _builderEntities[anchorEntityIndex];
        }

        _builderMultiDragSnapshots.Clear();
        for (var index = 0; index < dragIndices.Count; index += 1)
        {
            var dragIndex = dragIndices[index];
            var dragEntity = _builderEntities[dragIndex];
            _builderMultiDragSnapshots.Add((dragIndex, dragEntity.X, dragEntity.Y));
        }

        _builderSelectedEntityIndex = anchorEntityIndex;
        _builderEntityDragging = true;
        _builderEntityDragPointerOffsetWorld = new Vector2(entity.X, entity.Y) - world;
        _builderEntityDragStartX = entity.X;
        _builderEntityDragStartY = entity.Y;
        return true;
    }

    private void ApplyGarrisonBuilderMultiEntityDrag(Vector2 world)
    {
        if (!_builderEntityDragging || _builderMultiDragSnapshots.Count == 0)
        {
            return;
        }

        var anchorIndex = _builderSelectedEntityIndex;
        if (anchorIndex < 0 || anchorIndex >= _builderEntities.Count)
        {
            return;
        }

        var targetX = world.X + _builderEntityDragPointerOffsetWorld.X;
        var targetY = world.Y + _builderEntityDragPointerOffsetWorld.Y;
        if (_builderShiftHeld)
        {
            var deltaX = MathF.Abs(targetX - _builderEntityDragStartX);
            var deltaY = MathF.Abs(targetY - _builderEntityDragStartY);
            if (deltaX >= deltaY)
            {
                targetY = _builderEntityDragStartY;
            }
            else
            {
                targetX = _builderEntityDragStartX;
            }
        }

        var snappedAnchor = SnapGarrisonBuilderPoint(new Vector2(targetX, targetY));
        var delta = new Vector2(
            snappedAnchor.X - _builderEntityDragStartX,
            snappedAnchor.Y - _builderEntityDragStartY);

        for (var index = 0; index < _builderMultiDragSnapshots.Count; index += 1)
        {
            var snapshot = _builderMultiDragSnapshots[index];
            if (snapshot.Index < 0 || snapshot.Index >= _builderEntities.Count)
            {
                continue;
            }

            var snapped = SnapGarrisonBuilderPoint(new Vector2(snapshot.X + delta.X, snapshot.Y + delta.Y));
            var entity = _builderEntities[snapshot.Index];
            _builderEntities[snapshot.Index] = (entity with
            {
                X = snapped.X,
                Y = snapped.Y,
            }).NormalizeForEditing();
        }

        RefreshGarrisonBuilderEntityRefsAfterMove(_builderMultiDragSnapshots);
    }

    private void RemoveGarrisonBuilderSelectedEntities()
    {
        var indices = (_builderSelectedEntityIndices.Count > 0
            ? _builderSelectedEntityIndices.ToArray()
            : _builderSelectedEntityIndex >= 0
                ? new[] { _builderSelectedEntityIndex }
                : Array.Empty<int>())
            .OrderByDescending(index => index)
            .ToArray();
        if (indices.Length == 0)
        {
            return;
        }

        RecordGarrisonBuilderHistory();
        var removedCount = 0;
        foreach (var removedIndex in indices)
        {
            if (removedIndex < 0 || removedIndex >= _builderEntities.Count)
            {
                continue;
            }

            NotifyGarrisonBuilderEntityRemoved(removedIndex);
            _builderEntities.RemoveAt(removedIndex);
            removedCount += 1;
        }

        ClearGarrisonBuilderMapEntitySelection();
        CloseGarrisonBuilderPropertyEditor(applyChanges: false);
        UpdateGarrisonBuilderDocumentEntities();
        _builderDirty = true;
        _builderStatus = removedCount == 1
            ? "removed selected entity"
            : $"removed {removedCount} entities";
    }

    private void CloneGarrisonBuilderSelectedEntities()
    {
        var sourceIndices = (_builderSelectedEntityIndices.Count > 0
            ? _builderSelectedEntityIndices.OrderBy(index => index).ToArray()
            : _builderSelectedEntityIndex >= 0
                ? new[] { _builderSelectedEntityIndex }
                : Array.Empty<int>());
        if (sourceIndices.Length == 0)
        {
            return;
        }

        RecordGarrisonBuilderHistory();
        if (!TryDuplicateGarrisonBuilderEntities(sourceIndices, placeAtSourcePosition: false, out var clones))
        {
            return;
        }

        SetGarrisonBuilderMapEntitySelection(clones, clones[^1]);
        UpdateGarrisonBuilderDocumentEntities();
        _builderDirty = true;
        _builderStatus = clones.Count == 1
            ? $"cloned {_builderEntities[clones[0]].Type}"
            : $"cloned {clones.Count} entities";
    }
}
