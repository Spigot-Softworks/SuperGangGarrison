#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void EnsurePlayerNamePrompt()
    {
        if (!_mainMenuOpen
            || _namePromptOpen
            || _namePromptPresented
            || _mainMenuOverlayController.GetActiveOverlay() != MainMenuOverlayKind.None
            || !string.Equals(_world.LocalPlayer.DisplayName, "Player", StringComparison.Ordinal))
        {
            return;
        }

        _namePromptPresented = true;
        _namePromptOpen = true;
        _editingPlayerName = true;
        _playerNameEditBuffer = string.Empty;
        InitializePlayerNameEditCursor();
    }

    private void UpdatePlayerNamePrompt(KeyboardState keyboard)
    {
        if (IsKeyPressed(keyboard, Keys.Enter))
        {
            CommitPlayerNamePrompt();
        }
    }

    private void CommitPlayerNamePrompt()
    {
        if (!_namePromptOpen || string.IsNullOrWhiteSpace(_playerNameEditBuffer))
        {
            return;
        }

        SetLocalPlayerNameFromSettings(_playerNameEditBuffer);
        _editingPlayerName = false;
        _namePromptOpen = false;
    }

    private void DrawPlayerNamePrompt()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.86f);

        var panelWidth = Math.Min(viewportWidth - 32, 560);
        var panelHeight = Math.Min(viewportHeight - 32, 220);
        var panel = new Rectangle(
            (viewportWidth - panelWidth) / 2,
            (viewportHeight - panelHeight) / 2,
            Math.Max(1, panelWidth),
            Math.Max(1, panelHeight));
        DrawRoundedRectangleOutline(panel, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 2, radius: 8);

        DrawBitmapFontText("Set Your Name", new Vector2(panel.X + 24f, panel.Y + 22f), Color.White, 1.15f);
        DrawMenuInputBoxScaled(
            new Rectangle(panel.X + 24, panel.Y + 82, Math.Max(1, panel.Width - 48), 36),
            _playerNameEditBuffer,
            true,
            1f,
            _playerNameEditCursorIndex,
            _playerNameEditSelectionStart);
    }
}
