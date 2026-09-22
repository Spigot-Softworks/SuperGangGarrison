using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private GarrisonBuilderHistorySnapshot? _builderPropertyEditSnapshot;
    private bool _builderPropertyHistoryRecorded;
    private GarrisonBuilderHistorySnapshot? _builderSavedSnapshot;
    private CustomMapBuilderValidationResult? _builderCachedValidation;
    private GarrisonBuilderHistorySnapshot? _builderValidationSnapshot;
    private Task<CustomMapBuilderValidationResult>? _builderValidationTask;
    private readonly Dictionary<string, (CustomMapBuilderResource Resource, Task<TextureDecodeUtility.DecodedTextureData>? Task)> _builderResourceDecodeTasks = new(StringComparer.OrdinalIgnoreCase);
    private Action? _builderUnsavedAction;
    private bool _builderAllowDiscard;
    private float _builderRecoveryElapsed;
    private Task? _builderRecoveryTask;
    private string _builderRecoveryId = Guid.NewGuid().ToString("N");
    private static string BuilderRecoveryDirectory => Path.Combine(RuntimePaths.ConfigDirectory, "builder-recovery");

    private static bool TryGetGarrisonBuilderResourceBytes(CustomMapBuilderResource resource, out byte[] bytes)
    {
        // Editor resources are immutable snapshots. Reading their bytes does not need a defensive copy each draw.
        if (resource.EmbeddedBytes is { Length: > 0 }) { bytes = resource.EmbeddedBytes; return true; }
        return CustomMapBuilderResourceCodec.TryGetResourceBytes(resource, out bytes);
    }

    private void FinishGarrisonBuilderGestures()
    {
        if (_builderEntityDragging || _builderActiveResizeHandle != GarrisonBuilderResizeHandle.None || _builderGameplayMessageImageDragging
            || _builderGameplayMessageImageActiveResizeHandle != GarrisonBuilderResizeHandle.None)
        {
            UpdateGarrisonBuilderDocumentEntities();
            _builderDirty = true;
        }
        _builderEntityDragging = _builderAreaSelectDragging = _builderPlacementDragging = _builderEraseDragging = false;
        _builderLayerOffsetDragging = _builderMapPanDragging = false;
        _builderActiveResizeHandle = GarrisonBuilderResizeHandle.None;
        _builderMultiDragSnapshots.Clear();
        ClearGarrisonBuilderGameplayMessageImageInteraction();
        _builderMultiEntityMapPickAreaSelectDragging = false;
    }

    private bool CommitGarrisonBuilderActiveEdits()
    {
        if (_builderPropertyTarget != GarrisonBuilderPropertyTarget.None)
        {
            if (_builderPropertyEditMode != GarrisonBuilderPropertyEditMode.List)
            {
                CommitGarrisonBuilderPropertyEditorText();
                if (_builderPropertyEditMode != GarrisonBuilderPropertyEditMode.List) return false;
            }
            CloseGarrisonBuilderPropertyEditor(applyChanges: true);
            if (_builderPropertyTarget != GarrisonBuilderPropertyTarget.None) return false;
        }
        if (_builderLayerParallaxDialogOpen)
        {
            CloseGarrisonBuilderLayerParallaxDialog(applyChanges: true);
            if (_builderLayerParallaxDialogOpen) return false;
        }
        FinishGarrisonBuilderGestures();
        return true;
    }

    private void BeginGarrisonBuilderPropertyTransaction()
    {
        if (_builderPropertyEditSnapshot.HasValue) return;
        _builderPropertyEditSnapshot = CreateGarrisonBuilderHistorySnapshot();
        _builderPropertyHistoryRecorded = false;
    }

    private void CompleteGarrisonBuilderPropertyTransaction(bool apply)
    {
        var original = _builderPropertyEditSnapshot;
        _builderPropertyEditSnapshot = null;
        if (!apply && original.HasValue)
        {
            _builderDocument = original.Value.Document;
            _builderEntities.Clear(); _builderEntities.AddRange(original.Value.Entities);
            _builderSelectedGameMode = original.Value.Mode;
            if (_builderPropertyHistoryRecorded && _builderUndoStack.Count > 0) _builderUndoStack.RemoveAt(_builderUndoStack.Count - 1);
            ClearGarrisonBuilderResourceTextureCache();
        }
        _builderPropertyHistoryRecorded = false;
        RefreshGarrisonBuilderDirtyState();
    }

    private void MarkGarrisonBuilderSaved()
    {
        _builderSavedSnapshot = CreateGarrisonBuilderHistorySnapshot();
        _builderDirty = false;
    }

    private void RefreshGarrisonBuilderDirtyState()
    {
        _builderDirty = !_builderSavedSnapshot.HasValue
            || !SameGarrisonBuilderSnapshot(_builderSavedSnapshot.Value, CreateGarrisonBuilderHistorySnapshot());
    }

    private static bool SameGarrisonBuilderSnapshot(GarrisonBuilderHistorySnapshot a, GarrisonBuilderHistorySnapshot b) =>
        a.Mode == b.Mode && BuilderDocumentComparer.ContentEquals(a.Document with { Entities = a.Entities }, b.Document with { Entities = b.Entities });

    private CustomMapBuilderValidationResult GetGarrisonBuilderValidation() =>
        _builderCachedValidation ?? new CustomMapBuilderValidationResult(_builderSelectedGameMode,
            [new(CustomMapBuilderValidationSeverity.Warning, "validation_pending", "Checking map...")]);

    private void UpdateGarrisonBuilderMaintenance(float seconds)
    {
        UpdateGarrisonBuilderResourceImages();
        if (_builderValidationTask?.IsCompleted == true)
        {
            if (_builderValidationTask.IsCompletedSuccessfully && _builderValidationSnapshot.HasValue
                && ReferenceEquals(_builderValidationSnapshot.Value.Document, _builderDocument)
                && _builderValidationSnapshot.Value.Mode == _builderSelectedGameMode
                && _builderValidationSnapshot.Value.Entities.SequenceEqual(_builderEntities)) _builderCachedValidation = _builderValidationTask.Result;
            else if (_builderValidationTask.IsFaulted)
                _builderCachedValidation = new CustomMapBuilderValidationResult(_builderSelectedGameMode,
                    [new(CustomMapBuilderValidationSeverity.Error, "validation_failed",
                        "Could not validate the map: " + _builderValidationTask.Exception?.GetBaseException().Message)]);
            _builderValidationTask = null;
        }
        if (_builderValidationTask is null && (!_builderValidationSnapshot.HasValue
            || !ReferenceEquals(_builderValidationSnapshot.Value.Document, _builderDocument)
            || _builderValidationSnapshot.Value.Mode != _builderSelectedGameMode
            || !_builderValidationSnapshot.Value.Entities.SequenceEqual(_builderEntities)))
        {
            var snapshot = CreateGarrisonBuilderHistorySnapshot();
            _builderValidationSnapshot = snapshot;
            _builderCachedValidation = null;
            _builderValidationTask = Task.Run(() => CustomMapBuilderValidator.Validate(snapshot.Document with { Entities = snapshot.Entities }, snapshot.Mode));
        }
        _builderRecoveryElapsed += seconds;
        if (_builderRecoveryTask?.IsFaulted == true)
        {
            _builderStatus = "Recovery draft could not be written: " + _builderRecoveryTask.Exception?.GetBaseException().Message;
            _builderRecoveryTask = null;
        }
        if (_builderDirty && _builderRecoveryElapsed >= 30 && (_builderRecoveryTask is null || _builderRecoveryTask.IsCompleted)
            && _builderPropertyTarget == GarrisonBuilderPropertyTarget.None && !_builderEntityDragging)
        {
            var document = _builderDocument with { Entities = _builderEntities.ToArray() };
            var path = Path.Combine(BuilderRecoveryDirectory, _builderRecoveryId + BuilderProjectStore.Extension);
            _builderRecoveryElapsed = 0;
            _builderRecoveryTask = Task.Run(() => BuilderProjectStore.Save(document, path));
        }
    }

    private void UpdateGarrisonBuilderResourceImages()
    {
        foreach (var pair in _builderDocument.Resources)
        {
            if (!CustomMapBuilderResourceCodec.IsImageResourceKind(pair.Value.Kind)) continue;
            if (!_builderResourceDecodeTasks.TryGetValue(pair.Key, out var entry) || entry.Resource != pair.Value)
            {
                if (_builderResourceDecodeTasks.Values.Count(v => v.Task is { IsCompleted: false }) >= 2) continue;
                RemoveGarrisonBuilderResourceTexture(pair.Key);
                var resource = pair.Value;
                entry = (resource, Task.Run(() =>
                {
                    var bytes = resource.EmbeddedBytes ?? CustomMapBuilderResourceCodec.GetResourceBytes(resource);
                    BuilderImageValidation.Validate(bytes);
                    return TextureDecodeUtility.DecodeTextureData(bytes, false);
                }));
                _builderResourceDecodeTasks[pair.Key] = entry;
            }
            if (entry.Task?.IsCompletedSuccessfully == true && !_builderResourceTextureCache.ContainsKey(pair.Key))
            {
                var image = entry.Task.Result;
                _builderResourceTextureCache[pair.Key] = TextureDecodeUtility.CreateTexture(GraphicsDevice, image.PixelData, image.Width, image.Height);
                _builderResourceDecodeTasks[pair.Key] = (entry.Resource, null);
            }
            else if (entry.Task?.IsFaulted == true) { _ = entry.Task.Exception; _builderResourceDecodeTasks[pair.Key] = (entry.Resource, null); } // Already surfaced by import/export validation; do not retry each draw.
        }
        foreach (var name in _builderResourceDecodeTasks.Keys.Where(name => !_builderDocument.Resources.ContainsKey(name)).ToArray())
        { _builderResourceDecodeTasks.Remove(name); RemoveGarrisonBuilderResourceTexture(name); }
    }

    private bool GuardGarrisonBuilderUnsavedAction(Action action)
    {
        FinishGarrisonBuilderGestures();
        RefreshGarrisonBuilderDirtyState();
        if (_builderAllowDiscard || !_builderDirty) return false;
        _builderUnsavedAction = action;
        return true;
    }

    private Rectangle GetGarrisonBuilderUnsavedBounds() => new(Math.Max(8, (BuilderViewportWidth - 460) / 2), Math.Max(8, (BuilderViewportHeight - 150) / 2), Math.Min(460, BuilderViewportWidth - 16), 150);

    private void DrawGarrisonBuilderUnsavedPrompt(MouseState mouse)
    {
        if (_builderUnsavedAction is null) return;
        var bounds = GetGarrisonBuilderUnsavedBounds();
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, BuilderViewportWidth, BuilderViewportHeight), new Color(0, 0, 0, 170));
        DrawMenuPanelBackdrop(bounds, 1f);
        DrawGarrisonBuilderText("Save changes to this map?", bounds.X + 16, bounds.Y + 20, Color.White, 1f);
        var buttons = CreateGarrisonBuilderButtonRow(bounds.X + 12, bounds.Bottom - 50, bounds.Width - 24, 3);
        DrawGarrisonBuilderButton(buttons[0], "Save", false, true, mouse);
        DrawGarrisonBuilderButton(buttons[1], "Discard", false, true, mouse);
        DrawGarrisonBuilderButton(buttons[2], "Cancel", false, true, mouse);
    }

    private bool UpdateGarrisonBuilderUnsavedPrompt(KeyboardState keyboard, MouseState mouse)
    {
        if (_builderUnsavedAction is null) return false;
        if (IsKeyPressed(keyboard, Keys.Escape)) { _builderUnsavedAction = null; return true; }
        var bounds = GetGarrisonBuilderUnsavedBounds();
        var buttons = CreateGarrisonBuilderButtonRow(bounds.X + 12, bounds.Bottom - 50, bounds.Width - 24, 3);
        if (!IsLeftMouseClickPressed(mouse, _previousMouse)) return true;
        if (buttons[2].Contains(mouse.Position)) { _builderUnsavedAction = null; return true; }
        if (buttons[0].Contains(mouse.Position))
        {
            _builderResumeAfterSave = true;
            SaveGarrisonBuilderDocument();
            return true;
        }
        else if (!buttons[1].Contains(mouse.Position)) return true;
        ContinueGarrisonBuilderUnsavedAction();
        return true;
    }

    private void ContinueGarrisonBuilderUnsavedAction()
    {
        _builderResumeAfterSave = false;
        var action = _builderUnsavedAction; _builderUnsavedAction = null;
        _builderAllowDiscard = true;
        try { action?.Invoke(); } finally { _builderAllowDiscard = false; }
    }

    private void RebaseGarrisonBuilderSavedAssets(CustomMapBuilderDocument original, CustomMapBuilderDocument saved)
    {
        CustomMapBuilderDocument Rebase(CustomMapBuilderDocument document)
        {
            var resources = document.Resources.ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
            foreach (var pair in resources.ToArray())
                if (original.Resources.TryGetValue(pair.Key, out var old) && saved.Resources.TryGetValue(pair.Key, out var replacement)
                    && pair.Value.SourcePath == old.SourcePath && ReferenceEquals(pair.Value.EmbeddedBytes, old.EmbeddedBytes))
                    resources[pair.Key] = replacement;
            return document with
            {
                BackgroundImagePath = document.BackgroundImagePath == original.BackgroundImagePath ? saved.BackgroundImagePath : document.BackgroundImagePath,
                WalkmaskImagePath = document.WalkmaskImagePath == original.WalkmaskImagePath && document.EmbeddedWalkmaskSection == original.EmbeddedWalkmaskSection ? saved.WalkmaskImagePath : document.WalkmaskImagePath,
                EmbeddedWalkmaskSection = document.WalkmaskImagePath == original.WalkmaskImagePath && document.EmbeddedWalkmaskSection == original.EmbeddedWalkmaskSection ? saved.EmbeddedWalkmaskSection : document.EmbeddedWalkmaskSection,
                Resources = resources,
            };
        }
        _builderDocument = Rebase(_builderDocument);
        for (var i = 0; i < _builderUndoStack.Count; i++) _builderUndoStack[i] = _builderUndoStack[i] with { Document = Rebase(_builderUndoStack[i].Document) };
        for (var i = 0; i < _builderRedoStack.Count; i++) _builderRedoStack[i] = _builderRedoStack[i] with { Document = Rebase(_builderRedoStack[i].Document) };
        // Textures already represent these images; only their backing files have changed.
        _builderLoadedBackgroundPath = _builderDocument.BackgroundImagePath;
        _builderLoadedWalkmaskPath = _builderDocument.WalkmaskImagePath;
    }

    private void ResetGarrisonBuilderDocumentSession()
    {
        FinishGarrisonBuilderGestures();
        _builderPropertyEditSnapshot = null;
        CloseGarrisonBuilderPropertyEditor(applyChanges: false);
        _builderActivePathField = GarrisonBuilderPathField.None;
        _builderLayerParallaxDialogOpen = false;
        _builderEditingLayerOffsets = false;
        CloseGarrisonBuilderEntityContextMenu();
        CloseGarrisonBuilderEntityOverlapPicker();
        ClearGarrisonBuilderPendingResourceImport();
        ClearGarrisonBuilderHistory();
        ClearGarrisonBuilderResourceTextureCache();
        _builderValidationSnapshot = null;
        _builderCachedValidation = null;
        _builderResourceDecodeTasks.Clear();
        _builderRecoveryId = Guid.NewGuid().ToString("N");
        _builderRecoveryElapsed = 0;
    }
}
