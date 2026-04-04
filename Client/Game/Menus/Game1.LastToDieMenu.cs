#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using System;
using System.IO;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool _lastToDieMenuOpen;
    private int _lastToDieMenuHoverIndex = -1;
    private Texture2D? _lastToDieLogoTexture;
    private string? _lastToDieLogoTexturePath;

    private bool IsLastToDieMenuActive()
    {
        return _mainMenuOpen && _lastToDieMenuOpen;
    }

    private void OpenLastToDieMenu(string? statusMessage = null)
    {
        _lastToDieMenuOpen = true;
        _lastToDieMenuHoverIndex = -1;
        _hostSetupOpen = false;
        _hostSetupEditField = HostSetupEditField.None;
        _practiceSetupOpen = false;
        _clientPowersOpen = false;
        _clientPowersOpenedFromGameplay = false;
        _optionsMenuOpen = false;
        _pluginOptionsMenuOpen = false;
        _controlsMenuOpen = false;
        _creditsOpen = false;
        _manualConnectOpen = false;
        CloseLobbyBrowser(clearStatus: false);
        _editingPlayerName = false;
        _menuStatusMessage = statusMessage ?? string.Empty;
    }

    private void CloseLastToDieMenu(bool clearStatus = false)
    {
        _lastToDieMenuOpen = false;
        _lastToDieMenuHoverIndex = -1;
        if (clearStatus)
        {
            _menuStatusMessage = string.Empty;
        }
    }

    private void ReturnToLastToDieMenu(string? statusMessage = null)
    {
        StopLastToDieGameOverSound();
        ReturnToMainMenu(statusMessage);
        OpenLastToDieMenu(statusMessage);
    }

    private void UpdateLastToDieMenu(KeyboardState keyboard, MouseState mouse)
    {
        const float xbegin = 40f;
        const float ybegin = 320f;
        const float spacing = 34f;
        const float width = 180f;
        const int items = 2;

        if (IsKeyPressed(keyboard, Keys.Escape))
        {
            CloseLastToDieMenu();
            return;
        }

        if (mouse.X > xbegin && mouse.X < xbegin + width)
        {
            _lastToDieMenuHoverIndex = (int)MathF.Round((mouse.Y - ybegin) / spacing);
            if (_lastToDieMenuHoverIndex < 0 || _lastToDieMenuHoverIndex >= items)
            {
                _lastToDieMenuHoverIndex = -1;
            }
        }
        else
        {
            _lastToDieMenuHoverIndex = -1;
        }

        if (IsKeyPressed(keyboard, Keys.Enter))
        {
            TryStartLastToDieRun();
            return;
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed || _lastToDieMenuHoverIndex < 0)
        {
            return;
        }

        switch (_lastToDieMenuHoverIndex)
        {
            case 0:
                TryStartLastToDieRun();
                break;
            case 1:
                CloseLastToDieMenu();
                break;
        }
    }

    private void DrawLastToDieMenu()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black);

        DrawLastToDieMenuLogo(viewportWidth);

        string[] items = ["Play", "Back"];
        var position = new Vector2(40f, 320f);
        const float itemSpacing = 34f;
        const float itemWidth = 188f;
        DrawMenuPanelBackdrop(
            new Rectangle(
                (int)MathF.Round(position.X - 12f),
                (int)MathF.Round(position.Y - 24f),
                (int)MathF.Round(itemWidth + 28f),
                (int)MathF.Round((items.Length * itemSpacing) + 24f)),
            0.84f);
        DrawMenuPlaqueRows(position, items.Length, itemSpacing, itemWidth, 0.74f);
        for (var index = 0; index < items.Length; index += 1)
        {
            var color = index == _lastToDieMenuHoverIndex ? Color.Red : Color.White;
            DrawBitmapFontText(items[index], position, color, 1f);
            position.Y += itemSpacing;
        }

        if (!string.IsNullOrWhiteSpace(_menuStatusMessage))
        {
            DrawBitmapFontText(_menuStatusMessage, new Vector2(40f, viewportHeight - 42f), new Color(226, 214, 180), 1f);
        }
    }

    private void DrawLastToDieMenuLogo(int viewportWidth)
    {
        EnsureLastToDieLogoTexture();
        if (_lastToDieLogoTexture is null)
        {
            return;
        }

        const float targetWidth = 300f;
        var scale = targetWidth / Math.Max(1f, _lastToDieLogoTexture.Width);
        var targetHeight = _lastToDieLogoTexture.Height * scale;
        var destination = new Rectangle(
            (int)MathF.Round(viewportWidth - targetWidth - 36f),
            32,
            (int)MathF.Round(targetWidth),
            (int)MathF.Round(targetHeight));
        _spriteBatch.Draw(_lastToDieLogoTexture, destination, Color.White);
    }

    private void EnsureLastToDieLogoTexture()
    {
        var path = ContentRoot.GetPath("Sprites", "Menu", "LastToDie", "last2die.png");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            DisposeLastToDieLogoTexture();
            return;
        }

        if (_lastToDieLogoTexture is not null
            && string.Equals(_lastToDieLogoTexturePath, path, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        DisposeLastToDieLogoTexture();
        using var stream = File.OpenRead(path);
        _lastToDieLogoTexture = Texture2D.FromStream(GraphicsDevice, stream);
        _lastToDieLogoTexturePath = path;
    }

    private void DisposeLastToDieLogoTexture()
    {
        _lastToDieLogoTexture?.Dispose();
        _lastToDieLogoTexture = null;
        _lastToDieLogoTexturePath = null;
    }
}
