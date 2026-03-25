#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Diagnostics;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void EnsureDevMessageCheckStarted()
    {
        if (_devMessageCheckStarted || IsServerLauncherMode)
        {
            return;
        }

        _devMessageCheckStarted = true;
        _devMessageFetchTask = DevMessageService.FetchAsync(default);
        AddConsoleLine($"devmessages: checking against source parity v{DevMessageService.SourceParityVersionLabel}");
    }

    private void UpdateDevMessageState()
    {
        EnsureDevMessageCheckStarted();
        if (_devMessageFetchTask is null || !_devMessageFetchTask.IsCompleted)
        {
            return;
        }

        if (_devMessageCheckFinished)
        {
            return;
        }

        _devMessageCheckFinished = true;
        DevMessageFetchResult result;
        try
        {
            result = _devMessageFetchTask.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            AddConsoleLine($"devmessages: check failed ({ex.Message})");
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            AddConsoleLine($"devmessages: {result.Error}");
        }

        var updaterPath = DevMessageService.FindBundledUpdaterPath();
        for (var index = 0; index < result.Entries.Count; index += 1)
        {
            var entry = result.Entries[index];
            switch (entry.Kind)
            {
                case DevMessageEntryKind.ShowMessage:
                    _pendingDevMessagePopups.Enqueue(new DevMessagePopupState(
                        entry.Title,
                        entry.Message,
                        "OK",
                        string.Empty,
                        canRunPrimaryAction: false));
                    break;
                case DevMessageEntryKind.UpdateAvailable:
                    if (string.IsNullOrWhiteSpace(updaterPath))
                    {
                        AddConsoleLine($"devmessages: update {entry.VersionLabel} advertised from {result.SourceDescription}, but OpenGarrisonupdater.exe was not found.");
                        break;
                    }

                    _pendingDevMessagePopups.Enqueue(new DevMessagePopupState(
                        entry.Title,
                        entry.Message,
                        "Launch Updater",
                        "Later",
                        canRunPrimaryAction: true,
                        primaryActionPath: updaterPath));
                    break;
            }
        }

        ActivateNextDevMessagePopup();
    }

    private bool UpdateDevMessagePopup(KeyboardState keyboard, MouseState mouse)
    {
        ActivateNextDevMessagePopup();
        if (_activeDevMessagePopup is null)
        {
            return false;
        }

        if (keyboard.IsKeyDown(Keys.Escape) && !_previousKeyboard.IsKeyDown(Keys.Escape))
        {
            DismissDevMessagePopup();
            return true;
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed)
        {
            return true;
        }

        var panel = GetDevMessagePanelBounds();
        var primaryBounds = new Rectangle(panel.X + 34, panel.Bottom - 62, 220, 42);
        var secondaryBounds = new Rectangle(panel.Right - 214, panel.Bottom - 62, 180, 42);
        if (primaryBounds.Contains(mouse.Position))
        {
            if (_activeDevMessagePopup.CanRunPrimaryAction && TryLaunchDevMessageAction(_activeDevMessagePopup))
            {
                Exit();
                return true;
            }

            DismissDevMessagePopup();
            return true;
        }

        if ((_activeDevMessagePopup.CanRunPrimaryAction && secondaryBounds.Contains(mouse.Position))
            || (!_activeDevMessagePopup.CanRunPrimaryAction && primaryBounds.Contains(mouse.Position)))
        {
            DismissDevMessagePopup();
            return true;
        }

        return true;
    }

    private void DrawDevMessagePopup()
    {
        if (_activeDevMessagePopup is null)
        {
            return;
        }

        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.84f);

        var panel = GetDevMessagePanelBounds();
        _spriteBatch.Draw(_pixel, panel, new Color(31, 33, 38, 242));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, panel.Width, 3), new Color(210, 210, 210));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Bottom - 3, panel.Width, 3), new Color(76, 76, 76));

        DrawBitmapFontText(_activeDevMessagePopup.Title, new Vector2(panel.X + 30f, panel.Y + 26f), Color.White, 1.2f);

        var drawY = panel.Y + 82f;
        var wrappedLines = WrapMenuParagraph(_activeDevMessagePopup.Message, maxCharacters: 52);
        for (var index = 0; index < wrappedLines.Length; index += 1)
        {
            var line = wrappedLines[index];
            if (line.Length == 0)
            {
                drawY += 16f;
                continue;
            }

            DrawBitmapFontText(line, new Vector2(panel.X + 30f, drawY), Color.White, 0.92f);
            drawY += 20f;
        }

        var primaryBounds = new Rectangle(panel.X + 34, panel.Bottom - 62, 220, 42);
        DrawMenuButton(primaryBounds, _activeDevMessagePopup.PrimaryButtonLabel, _activeDevMessagePopup.CanRunPrimaryAction);
        if (_activeDevMessagePopup.CanRunPrimaryAction)
        {
            var secondaryBounds = new Rectangle(panel.Right - 214, panel.Bottom - 62, 180, 42);
            DrawMenuButton(secondaryBounds, _activeDevMessagePopup.SecondaryButtonLabel, false);
        }
    }

    private Rectangle GetDevMessagePanelBounds()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        var width = Math.Min(720, viewportWidth - 120);
        var height = Math.Min(360, viewportHeight - 160);
        return new Rectangle((viewportWidth - width) / 2, (viewportHeight - height) / 2, width, height);
    }

    private static string[] WrapMenuParagraph(string text, int maxCharacters)
    {
        var paragraphs = text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');
        var lines = new System.Collections.Generic.List<string>();
        for (var paragraphIndex = 0; paragraphIndex < paragraphs.Length; paragraphIndex += 1)
        {
            var paragraph = paragraphs[paragraphIndex].Trim();
            if (paragraph.Length == 0)
            {
                lines.Add(string.Empty);
                continue;
            }

            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var current = string.Empty;
            for (var wordIndex = 0; wordIndex < words.Length; wordIndex += 1)
            {
                var next = current.Length == 0 ? words[wordIndex] : current + " " + words[wordIndex];
                if (next.Length <= maxCharacters)
                {
                    current = next;
                    continue;
                }

                if (current.Length > 0)
                {
                    lines.Add(current);
                }

                current = words[wordIndex];
            }

            if (current.Length > 0)
            {
                lines.Add(current);
            }
        }

        return lines.ToArray();
    }

    private void ActivateNextDevMessagePopup()
    {
        if (_activeDevMessagePopup is null && _pendingDevMessagePopups.Count > 0)
        {
            _activeDevMessagePopup = _pendingDevMessagePopups.Dequeue();
        }
    }

    private void DismissDevMessagePopup()
    {
        _activeDevMessagePopup = null;
        ActivateNextDevMessagePopup();
    }

    private bool TryLaunchDevMessageAction(DevMessagePopupState popup)
    {
        if (!popup.CanRunPrimaryAction || string.IsNullOrWhiteSpace(popup.PrimaryActionPath))
        {
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = popup.PrimaryActionPath,
                WorkingDirectory = System.IO.Path.GetDirectoryName(popup.PrimaryActionPath) ?? AppContext.BaseDirectory,
                UseShellExecute = true,
            };
            Process.Start(startInfo);
            AddConsoleLine($"devmessages: launched updater at {popup.PrimaryActionPath}");
            return true;
        }
        catch (Exception ex)
        {
            AddConsoleLine($"devmessages: updater launch failed ({ex.Message})");
            _menuStatusMessage = "Unable to launch OpenGarrisonupdater.exe.";
            return false;
        }
    }
}
