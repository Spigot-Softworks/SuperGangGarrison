using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed record BuilderDialogResult(string Path, string Error, bool Unavailable = false);
    private Task<BuilderDialogResult>? _builderDialogTask;
    private CancellationTokenSource? _builderDialogCancellation;
    private Action<string>? _builderDialogAccept;
    private bool _builderDialogFallback;
    private string _builderDialogInitialPath = "";
    private string _builderDialogTitle = "";
    private string _builderDialogPreviousResourcePath = "";
    private DisplayModeKind? _builderDialogRestoreMode;
    private Task<Action>? _builderFileWorkTask;
    private string _builderFileWorkGeneration = "";
    private bool _builderResumeAfterSave;
    private string? _builderSaveAsOriginalPath;

    private void CancelGarrisonBuilderSaveAs()
    {
        if (_builderSaveAsOriginalPath is not null)
        {
            _builderSavePath = _builderSaveAsOriginalPath;
            _builderSaveAsOriginalPath = null;
            _builderSavePathBuffer = _builderSavePath;
        }
        _builderResumeAfterSave = false;
    }

    private void BeginGarrisonBuilderFileDialog(string title, string filter, string initialPath, bool save, bool folder, Action<string> accepted)
    {
        if (_builderDialogTask is not null || _builderDialogFallback || _builderFileWorkTask is not null) return;
        FinishGarrisonBuilderGestures();
        _builderDialogAccept = accepted;
        _builderDialogInitialPath = initialPath;
        _builderDialogTitle = title;
        _builderStatus = title;
        var transition = ResolveGarrisonBuilderDialogDisplayMode(_displayMode, _clientSettings.DisplayMode);
        if (transition.TemporarilyWindowed)
        {
            _builderDialogRestoreMode = transition.RequestedMode;
            _clientSettings.DisplayMode = DisplayModeKind.Windowed;
            ApplyGraphicsSettings(persist: false);
        }
        var script = CreateGarrisonBuilderDialogScript(title, filter, initialPath, save, folder);
        _builderDialogCancellation = new CancellationTokenSource();
        var cancellation = _builderDialogCancellation.Token;
        _builderDialogTask = Task.Run(async () =>
        {
            try
            {
                var start = new ProcessStartInfo { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8 };
                if (OperatingSystem.IsWindows())
                {
                    start.FileName = "powershell.exe";
                    foreach (var argument in new[] { "-NoProfile", "-STA", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) }) start.ArgumentList.Add(argument);
                }
                else if (OperatingSystem.IsLinux())
                {
                    start.FileName = "zenity";
                    start.ArgumentList.Add("--file-selection"); start.ArgumentList.Add("--title=" + title);
                    if (folder) start.ArgumentList.Add("--directory");
                    if (save) { start.ArgumentList.Add("--save"); start.ArgumentList.Add("--confirm-overwrite"); }
                    if (!string.IsNullOrWhiteSpace(initialPath)) start.ArgumentList.Add("--filename=" + initialPath);
                }
                else return new BuilderDialogResult("", "Enter a path below.", true);
                using var process = Process.Start(start);
                if (process is null) return new BuilderDialogResult("", "File picker unavailable.", true);
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                try { await process.WaitForExitAsync(cancellation); }
                catch (OperationCanceledException)
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync();
                    await Task.WhenAll(output, error);
                    return new BuilderDialogResult("", "");
                }
                var path = (await output).TrimEnd('\r', '\n');
                var details = (await error).Trim();
                if (process.ExitCode == 0) return new BuilderDialogResult(path, "");
                if (process.ExitCode == 1 && string.IsNullOrEmpty(details)) return new BuilderDialogResult("", "");
                return new BuilderDialogResult("", "File picker failed: " + details, true);
            }
            catch (Exception ex) { return new BuilderDialogResult("", "File picker unavailable: " + ex.Message, true); }
        });
    }

    private bool UpdateGarrisonBuilderFileDialog(KeyboardState keyboard)
    {
        if (_builderDialogFallback) { UpdateGarrisonBuilderPathKeyboard(keyboard); return true; }
        if (_builderDialogTask is null) return false;
        if (!_builderDialogTask.IsCompleted) return true;
        var result = _builderDialogTask.GetAwaiter().GetResult();
        _builderDialogTask = null;
        _builderDialogCancellation?.Dispose(); _builderDialogCancellation = null;
        RestoreGarrisonBuilderDialogDisplay();
        FinishGarrisonBuilderGestures();
        if (result.Unavailable)
        {
            _builderDialogFallback = true;
            _builderDialogPreviousResourcePath = _builderResourcePathBuffer;
            _builderResourcePathBuffer = _builderDialogInitialPath;
            _builderActivePathField = GarrisonBuilderPathField.ResourcePath;
            _builderPathCursorIndex = _builderPathSelectionStart = _builderResourcePathBuffer.Length;
            _builderStatus = result.Error + " Enter the path; Enter accepts, Escape cancels.";
            return true;
        }
        var callback = _builderDialogAccept; _builderDialogAccept = null;
        if (!string.IsNullOrWhiteSpace(result.Path)) callback?.Invoke(result.Path);
        else { CancelGarrisonBuilderSaveAs(); _builderStatus = "File selection cancelled."; }
        return true;
    }

    private void RestoreGarrisonBuilderDialogDisplay()
    {
        var previous = _builderDialogRestoreMode; _builderDialogRestoreMode = null;
        if (!previous.HasValue) return;
        _clientSettings.DisplayMode = previous.Value;
        ApplyGraphicsSettings(persist: false);
    }

    private void CompleteGarrisonBuilderDialogFallback()
    {
        var path = _builderResourcePathBuffer.Trim().Trim('"');
        var callback = _builderDialogAccept;
        CancelGarrisonBuilderDialogFallback();
        if (path.Length > 0) callback?.Invoke(path);
    }

    private void CancelGarrisonBuilderDialogFallback()
    {
        if (!_builderDialogFallback) return;
        _builderDialogFallback = false;
        _builderDialogAccept = null;
        _builderResourcePathBuffer = _builderDialogPreviousResourcePath;
        _builderActivePathField = GarrisonBuilderPathField.None;
    }

    private void StartGarrisonBuilderFileWork(string status, Func<Action> prepare)
    {
        if (_builderFileWorkTask is not null) return;
        FinishGarrisonBuilderGestures();
        _builderStatus = status;
        _builderFileWorkGeneration = _builderRecoveryId;
        _builderFileWorkTask = Task.Run(prepare);
    }

    private bool UpdateGarrisonBuilderFileWork()
    {
        if (_builderFileWorkTask is null) return false;
        if (!_builderFileWorkTask.IsCompleted) return true;
        var task = _builderFileWorkTask; _builderFileWorkTask = null;
        try
        {
            var complete = task.GetAwaiter().GetResult();
            if (_builderFileWorkGeneration == _builderRecoveryId) complete();
        }
        catch (Exception ex)
        {
            _builderStatus = "Builder operation failed: " + ex.Message;
            AddConsoleLine(_builderStatus);
            CancelGarrisonBuilderSaveAs();
        }
        return true;
    }

    private void DrawGarrisonBuilderStabilityOverlays(MouseState mouse)
    {
        DrawGarrisonBuilderUnsavedPrompt(mouse);
        // Save As may need a name-conflict decision while an unsaved transition is pending.
        if (_builderMapNameCollisionDialogOpen) DrawGarrisonBuilderMapNameCollisionDialog(mouse);
        if (_builderDialogFallback)
        {
            var bounds = GetGarrisonBuilderUnsavedBounds();
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, BuilderViewportWidth, BuilderViewportHeight), new Color(0, 0, 0, 180));
            DrawMenuPanelBackdrop(bounds, 1f);
            DrawGarrisonBuilderText(_builderDialogTitle, bounds.X + 12, bounds.Y + 12, Color.White, 1f);
            var text = _builderResourcePathBuffer;
            DrawGarrisonBuilderText(text.Length > 52 ? "..." + text[^52..] : text, bounds.X + 12, bounds.Y + 55, Color.Yellow, 1f);
            DrawGarrisonBuilderText("Enter: accept   Escape: cancel", bounds.X + 12, bounds.Bottom - 30, Color.White, 1f);
        }
        else if (_builderFileWorkTask is not null || _builderDialogTask is not null)
        {
            var bounds = GetGarrisonBuilderUnsavedBounds();
            DrawMenuPanelBackdrop(bounds, 1f);
            DrawGarrisonBuilderText(_builderStatus, bounds.X + 12, bounds.Y + 40, Color.White, 1f);
        }
    }

    private void OpenGarrisonBuilderRecovery()
    {
        Directory.CreateDirectory(BuilderRecoveryDirectory);
        BeginChooseGarrisonBuilderFile("Recover a Builder draft", "Builder drafts|*.ogmap", BuilderRecoveryDirectory + Path.DirectorySeparatorChar, OpenGarrisonBuilderMap);
    }

    private void PreserveGarrisonBuilderOnExit()
    {
        _builderDialogCancellation?.Cancel();
        if (!_builderEditorEnabled || !_builderDirty) return;
        try
        {
            CommitGarrisonBuilderActiveEdits();
            // Shutdown cannot depend on a future frame to finish this last recovery write.
            _builderRecoveryTask?.GetAwaiter().GetResult();
            BuilderProjectStore.Save(_builderDocument with { Entities = _builderEntities.ToArray() }, Path.Combine(BuilderRecoveryDirectory, _builderRecoveryId + BuilderProjectStore.Extension));
        }
        catch (Exception ex) { AddConsoleLine("Builder recovery failed: " + ex.Message); }
    }
}
